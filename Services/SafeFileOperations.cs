using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public static class SafeFileOperations
    {
        /// <summary>
        /// Безопасное перемещение или замена файла с защитой от блокировок другими процессами (антивирус, проводник, эмуляторы).
        /// Поддерживает сброс атрибутов ReadOnly, повторные попытки (retry backoff), переименование заблокированного целевого файла и сохранение под резервным именем при жесткой блокировке.
        /// </summary>
        public static async Task<string> SafeMoveOrReplaceFileAsync(string sourcePath, string destinationPath, ProcessingTask? task = null, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException($"Исходный файл не найден для перемещения: {sourcePath}");
            }

            if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase))
            {
                return destinationPath;
            }

            string destDir = Path.GetDirectoryName(destinationPath) ?? "";
            if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            // Сбрасываем атрибуты ReadOnly и Hidden у исходного и целевого файлов
            ResetFileAttributesSafe(sourcePath);
            if (File.Exists(destinationPath))
            {
                ResetFileAttributesSafe(destinationPath);
            }

            // 1. Быстрая попытка атомарного перемещения/замены
            for (int attempt = 1; attempt <= 12; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (File.Exists(destinationPath))
                    {
                        ResetFileAttributesSafe(destinationPath);
                        File.Move(sourcePath, destinationPath, overwrite: true);
                    }
                    else
                    {
                        File.Move(sourcePath, destinationPath);
                    }
                    return destinationPath;
                }
                catch (IOException) when (attempt < 12)
                {
                    // Освобождаем зависшие дескрипторы в текущем процессе
                    if (attempt % 3 == 0)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                    await Task.Delay(250, cancellationToken);
                }
                catch (UnauthorizedAccessException) when (attempt < 12)
                {
                    ResetFileAttributesSafe(destinationPath);
                    if (attempt % 3 == 0)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                    await Task.Delay(300, cancellationToken);
                }
            }

            // 2. Если целевой файл заблокирован на перезапись, пробуем убрать его во временное имя
            if (File.Exists(destinationPath))
            {
                ResetFileAttributesSafe(destinationPath);
                string tempBackup = destinationPath + ".old_" + Guid.NewGuid().ToString("N").Substring(0, 6);
                bool backupMoved = false;
                try
                {
                    File.Move(destinationPath, tempBackup);
                    backupMoved = true;
                }
                catch
                {
                    backupMoved = false;
                }

                if (backupMoved)
                {
                    for (int mAttempt = 1; mAttempt <= 4; mAttempt++)
                    {
                        try
                        {
                            File.Move(sourcePath, destinationPath);
                            SafeDeleteFile(tempBackup);
                            return destinationPath;
                        }
                        catch (Exception) when (mAttempt < 4)
                        {
                            await Task.Delay(200, cancellationToken);
                        }
                    }

                    // Если перемещение нового файла не удалось, пробуем скопировать
                    try
                    {
                        File.Copy(sourcePath, destinationPath, overwrite: true);
                        SafeDeleteFile(sourcePath);
                        SafeDeleteFile(tempBackup);
                        return destinationPath;
                    }
                    catch
                    {
                        // Восстанавливаем бэкап при неудаче
                        try { if (!File.Exists(destinationPath)) File.Move(tempBackup, destinationPath); } catch { }
                    }
                }
            }

            // 3. Крайний случай: целевой файл жестко захвачен (например, запущен эмулятор, играющий эту игру).
            // Не уничтожаем результат работы, а сохраняем рядом под уникальным именем!
            string baseName = Path.GetFileNameWithoutExtension(destinationPath);
            string ext = Path.GetExtension(destinationPath);
            string altPath = "";
            int counter = 1;
            do
            {
                altPath = Path.Combine(destDir, $"{baseName} ({counter}){ext}");
                counter++;
            } while (File.Exists(altPath));

            try
            {
                File.Move(sourcePath, altPath);
            }
            catch
            {
                File.Copy(sourcePath, altPath, overwrite: true);
                SafeDeleteFile(sourcePath);
            }

            string warnMsg = $"\n⚠️ [Внимание] Файл {Path.GetFileName(destinationPath)} заблокирован другим процессом (например, эмулятором или проводником).\n     Результат успешно сохранен рядом как: {Path.GetFileName(altPath)}";
            App.Logger?.Log(warnMsg, LogLevel.Warning);
            if (task != null)
            {
                App.RunOnUI(() =>
                {
                    task.LogDetails += warnMsg;
                    task.OutputFileName = Path.GetFileNameWithoutExtension(altPath);
                });
            }

            return altPath;
        }

        /// <summary>
        /// Безопасное удаление файла с повторными попытками и сбросом атрибутов ReadOnly.
        /// </summary>
        public static void SafeDeleteFile(string path)
        {
            if (!File.Exists(path)) return;
            ResetFileAttributesSafe(path);
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    File.Delete(path);
                    return;
                }
                catch (IOException)
                {
                    Thread.Sleep(100);
                }
                catch (UnauthorizedAccessException)
                {
                    ResetFileAttributesSafe(path);
                    Thread.Sleep(100);
                }
                catch
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Сброс защитных атрибутов файла (ReadOnly, Hidden) для беспрепятственной перезаписи и удаления.
        /// </summary>
        public static void ResetFileAttributesSafe(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var attr = File.GetAttributes(path);
                    if ((attr & (FileAttributes.ReadOnly | FileAttributes.Hidden)) != 0)
                    {
                        File.SetAttributes(path, FileAttributes.Normal);
                    }
                }
            }
            catch { }
        }
    }
}
