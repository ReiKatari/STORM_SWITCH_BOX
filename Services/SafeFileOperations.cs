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

            string srcRoot = Path.GetPathRoot(sourcePath) ?? "";
            string destRoot = Path.GetPathRoot(destinationPath) ?? "";
            bool isCrossVolume = !string.IsNullOrEmpty(srcRoot) && !string.IsNullOrEmpty(destRoot) && 
                                 !string.Equals(srcRoot, destRoot, StringComparison.OrdinalIgnoreCase);

            // 1. Попытка перемещения или замены (для разных дисков — надёжное копирование с заменой)
            for (int attempt = 1; attempt <= 12; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (isCrossVolume)
                    {
                        if (File.Exists(destinationPath))
                        {
                            ResetFileAttributesSafe(destinationPath);
                            try { File.Delete(destinationPath); } catch { }
                        }
                        await CopyFileAsync(sourcePath, destinationPath, bufferSize: 4 * 1024 * 1024, cancellationToken: cancellationToken);
                        ResetFileAttributesSafe(destinationPath);
                        SafeDeleteFile(sourcePath);
                        return destinationPath;
                    }
                    else
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
                }
                catch (IOException)
                {
                    if (attempt < 12)
                    {
                        if (attempt % 3 == 0)
                        {
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                        }
                        await Task.Delay(250, cancellationToken);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    ResetFileAttributesSafe(destinationPath);
                    if (File.Exists(destinationPath))
                    {
                        try { File.Delete(destinationPath); } catch { }
                    }
                    if (attempt < 12)
                    {
                        if (attempt % 3 == 0)
                        {
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                        }
                        await Task.Delay(300, cancellationToken);
                    }
                }
                catch (Exception)
                {
                    if (attempt < 12)
                    {
                        await Task.Delay(300, cancellationToken);
                    }
                }
            }

            // 2. Если целевой файл заблокирован на прямую перезапись, пробуем убрать его во временное имя
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
                            if (isCrossVolume)
                            {
                                File.Copy(sourcePath, destinationPath, overwrite: true);
                                SafeDeleteFile(sourcePath);
                            }
                            else
                            {
                                File.Move(sourcePath, destinationPath);
                            }
                            SafeDeleteFile(tempBackup);
                            return destinationPath;
                        }
                        catch (Exception)
                        {
                            if (mAttempt < 4)
                            {
                                await Task.Delay(250, cancellationToken);
                            }
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

            // 3. Крайний случай: целевой файл жестко захвачен внешним процессом (проводником, эмулятором, антивирусом).
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
                if (isCrossVolume)
                {
                    File.Copy(sourcePath, altPath, overwrite: true);
                    SafeDeleteFile(sourcePath);
                }
                else
                {
                    File.Move(sourcePath, altPath);
                }
            }
            catch
            {
                CopyFileAsync(sourcePath, altPath, bufferSize: 4 * 1024 * 1024).GetAwaiter().GetResult();
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

        /// <summary>
        /// Высокопроизводительное асинхронное копирование больших файлов с FileOptions.Asynchronous
        /// и оптимизированным 4 МБ буфером для максимальной утилизации скорости NVMe / SSD.
        /// </summary>
        public static async Task CopyFileAsync(string sourcePath, string destinationPath, int bufferSize = 4 * 1024 * 1024, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            var fileOptions = FileOptions.Asynchronous | FileOptions.SequentialScan;
            var sourceOptions = new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                Options = fileOptions,
                BufferSize = bufferSize
            };

            var destOptions = new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = fileOptions,
                BufferSize = bufferSize
            };

            using var sourceStream = new FileStream(sourcePath, sourceOptions);
            using var destStream = new FileStream(destinationPath, destOptions);

            long totalBytes = sourceStream.Length;
            long totalRead = 0;
            byte[] buffer = new byte[bufferSize];

            int bytesRead;
            while ((bytesRead = await sourceStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                await destStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                totalRead += bytesRead;
                if (totalBytes > 0 && progress != null)
                {
                    progress.Report((double)totalRead / totalBytes * 100.0);
                }
            }
        }
    }
}
