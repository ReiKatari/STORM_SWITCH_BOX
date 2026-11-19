using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace StormSwitchBox.Services
{
    /// <summary>
    /// Сервис гарантированной очистки временных рабочих папок и файлов (STORM_TMP, StormDecomp и др.).
    /// Удаляет файлы напрямую мимо корзины со сбросом атрибутов и повторными попытками.
    /// </summary>
    public static class TempCleanupService
    {
        private static readonly ConcurrentDictionary<string, byte> _activeTempDirs = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Регистрация созданной временной папки для отслеживания и экстренного удаления при сбое/отмене/выходе.
        /// </summary>
        public static void RegisterActiveTempDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            _activeTempDirs.TryAdd(Path.GetFullPath(path), 0);
        }

        /// <summary>
        /// Снятие папки с отслеживания после успешного завершения задачи.
        /// </summary>
        public static void UnregisterActiveTempDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            _activeTempDirs.TryRemove(Path.GetFullPath(path), out _);
        }

        /// <summary>
        /// Принудительное удаление директории со всеми файлами и подпапками напрямую мимо корзины.
        /// Сбрасывает атрибуты ReadOnly/Hidden/System и повторяет попытку при временных блокировках.
        /// </summary>
        public static bool ForceDeleteDirectory(string? dirPath, int maxRetries = 4, int delayMs = 150)
        {
            if (string.IsNullOrWhiteSpace(dirPath)) return true;

            try
            {
                if (!Directory.Exists(dirPath))
                {
                    UnregisterActiveTempDirectory(dirPath);
                    return true;
                }

                for (int attempt = 1; attempt <= maxRetries; attempt++)
                {
                    try
                    {
                        // Рекурсивно сбрасываем атрибуты файлов
                        var dirInfo = new DirectoryInfo(dirPath);
                        if (dirInfo.Exists)
                        {
                            dirInfo.Attributes = FileAttributes.Normal;
                            foreach (var file in dirInfo.EnumerateFiles("*", SearchOption.AllDirectories))
                            {
                                try
                                {
                                    file.Attributes = FileAttributes.Normal;
                                    file.Delete(); // Удаление мимо корзины
                                }
                                catch { }
                            }

                            foreach (var subDir in dirInfo.EnumerateDirectories("*", SearchOption.AllDirectories))
                            {
                                try
                                {
                                    subDir.Attributes = FileAttributes.Normal;
                                }
                                catch { }
                            }
                        }

                        Directory.Delete(dirPath, true);
                        UnregisterActiveTempDirectory(dirPath);
                        return true;
                    }
                    catch (IOException) when (attempt < maxRetries)
                    {
                        Thread.Sleep(delayMs * attempt);
                    }
                    catch (UnauthorizedAccessException) when (attempt < maxRetries)
                    {
                        Thread.Sleep(delayMs * attempt);
                    }
                    catch (Exception) when (attempt < maxRetries)
                    {
                        Thread.Sleep(delayMs * attempt);
                    }
                }

                // Финальная попытка
                if (Directory.Exists(dirPath))
                {
                    Directory.Delete(dirPath, true);
                }
                UnregisterActiveTempDirectory(dirPath);
                return true;
            }
            catch (Exception ex)
            {
                App.Logger?.Log($"[TempCleanup] Не удалось удалить временную папку '{dirPath}': {ex.Message}", Models.LogLevel.Warning);
                return false;
            }
        }

        /// <summary>
        /// Принудительное удаление отдельного файла мимо корзины.
        /// </summary>
        public static bool ForceDeleteFile(string? filePath, int maxRetries = 3)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return true;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    File.SetAttributes(filePath, FileAttributes.Normal);
                    File.Delete(filePath);
                    return true;
                }
                catch when (attempt < maxRetries)
                {
                    Thread.Sleep(100 * attempt);
                }
                catch { }
            }
            return false;
        }

        /// <summary>
        /// Мгновенная очистка всех зарегистрированных активных временных папок (вызывается при отмене или завершении).
        /// </summary>
        public static void PurgeActiveTempDirectories()
        {
            var keys = _activeTempDirs.Keys.ToList();
            foreach (var path in keys)
            {
                ForceDeleteDirectory(path);
            }
        }

        /// <summary>
        /// Глобальная очистка временных папок STORM_TMP_* и StormDecomp_* в рабочих и системных каталогах без сканирования корней дисков и без сноса заблокированных (.lock) папок.
        /// </summary>
        public static void PurgeAllTempDirectories()
        {
            PurgeStaleTempDirectories(maxAgeHours: 0);
        }

        private static bool HasActiveLockFile(string dirPath)
        {
            try
            {
                string fullPath = Path.GetFullPath(dirPath);
                if (_activeTempDirs.ContainsKey(fullPath))
                    return true;

                foreach (var lockFile in Directory.EnumerateFiles(dirPath, "*.lock", SearchOption.AllDirectories))
                {
                    try
                    {
                        using var fs = new FileStream(lockFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                        // Если lock-файл свежий (<= 24 часов), считаем его активным маркером процесса
                        if ((DateTime.UtcNow - File.GetLastWriteTimeUtc(lockFile)).TotalHours < 24)
                            return true;
                    }
                    catch
                    {
                        return true;
                    }
                }
            }
            catch
            {
                return true;
            }
            return false;
        }

        private static bool IsOlderThanMaxAge(string path, bool isDirectory, int maxAgeHours)
        {
            if (maxAgeHours <= 0) return true;
            try
            {
                DateTime lastWriteUtc = isDirectory
                    ? Directory.GetLastWriteTimeUtc(path)
                    : File.GetLastWriteTimeUtc(path);
                return (DateTime.UtcNow - lastWriteUtc).TotalHours >= maxAgeHours;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Очистка устаревших временных директорий и файлов с учётом maxAgeHours по LastWriteTimeUtc.
        /// </summary>
        public static void PurgeStaleTempDirectories(int maxAgeHours = 0)
        {
            Task.Run(() =>
            {
                try
                {
                    if (maxAgeHours <= 0)
                    {
                        PurgeActiveTempDirectories();
                    }

                    string[] tempDirPatterns = new[]
                    {
                        "STORM_TMP_*",
                        "StormDecomp_*",
                        "Storm3DS_*",
                        "StormControl_*",
                        "StormCompress_*",
                        "StormHactool_*",
                        "StormPack_*",
                        "StormHB*",
                        "StormHomebrew_*"
                    };

                    string[] tempFilePatterns = new[]
                    {
                        "list_conv_*.txt",
                        "multi_out_*.nsp",
                        "aes_keys_temp_*.txt"
                    };

                    void TryDeleteTempDirIfEligible(string dir)
                    {
                        if (HasActiveLockFile(dir)) return;
                        if (!IsOlderThanMaxAge(dir, isDirectory: true, maxAgeHours)) return;
                        ForceDeleteDirectory(dir);
                    }

                    void TryDeleteTempFileIfEligible(string file)
                    {
                        if (!IsOlderThanMaxAge(file, isDirectory: false, maxAgeHours)) return;
                        ForceDeleteFile(file);
                    }

                    // 1. Сканирование папки temp внутри директории приложения
                    try
                    {
                        string appTemp = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
                        if (Directory.Exists(appTemp))
                        {
                            foreach (var pattern in tempDirPatterns)
                            {
                                foreach (var dir in Directory.GetDirectories(appTemp, pattern, SearchOption.AllDirectories))
                                {
                                    TryDeleteTempDirIfEligible(dir);
                                }
                            }

                            string archivesTemp = Path.Combine(appTemp, "archives");
                            if (Directory.Exists(archivesTemp))
                            {
                                foreach (var dir in Directory.GetDirectories(archivesTemp))
                                {
                                    TryDeleteTempDirIfEligible(dir);
                                }
                            }
                        }
                    }
                    catch { }

                    // 2. Сканирование системной папки временных файлов %TEMP%
                    try
                    {
                        string sysTemp = Path.GetTempPath();
                        if (Directory.Exists(sysTemp))
                        {
                            foreach (var pattern in tempDirPatterns)
                            {
                                foreach (var dir in Directory.GetDirectories(sysTemp, pattern))
                                {
                                    TryDeleteTempDirIfEligible(dir);
                                }
                            }

                            foreach (var pattern in tempFilePatterns)
                            {
                                foreach (var file in Directory.GetFiles(sysTemp, pattern))
                                {
                                    TryDeleteTempFileIfEligible(file);
                                }
                            }
                        }
                    }
                    catch { }

                    // 3. Сканирование настроенных пользователем выходных папок
                    try
                    {
                        var customFolders = new List<string>();
                        if (!string.IsNullOrWhiteSpace(App.Settings?.Current?.OutputFolder))
                            customFolders.Add(App.Settings.Current.OutputFolder);
                        if (!string.IsNullOrWhiteSpace(App.Settings?.Current?.OutputFolder3ds))
                            customFolders.Add(App.Settings.Current.OutputFolder3ds);
                        if (App.Settings?.Current?.CatalogFolders != null)
                            customFolders.AddRange(App.Settings.Current.CatalogFolders);

                        foreach (var folder in customFolders.Distinct(StringComparer.OrdinalIgnoreCase))
                        {
                            if (Directory.Exists(folder))
                            {
                                foreach (var pattern in tempDirPatterns)
                                {
                                    foreach (var dir in Directory.GetDirectories(folder, pattern))
                                    {
                                        TryDeleteTempDirIfEligible(dir);
                                    }
                                }

                                foreach (var pattern in tempFilePatterns)
                                {
                                    foreach (var file in Directory.GetFiles(folder, pattern))
                                    {
                                        TryDeleteTempFileIfEligible(file);
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }
                catch (Exception ex)
                {
                    App.Logger?.Log($"[TempCleanup] Ошибка общей очистки временных папок: {ex.Message}", Models.LogLevel.Warning);
                }
            });
        }
    }
}
