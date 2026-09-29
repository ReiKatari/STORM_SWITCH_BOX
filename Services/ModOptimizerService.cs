using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    /// <summary>
    /// Сервис очистки мусора и оптимизации графических ассетов модов (Lossless PNG via Oxipng).
    /// Удаляет временные файлы (.bak, .tmp, .orig), системный мусор (Thumbs.db, .DS_Store, desktop.ini),
    /// и сжимает текстуры PNG без потери единого пикселя с экономией 20-45% веса.
    /// </summary>
    public static class ModOptimizerService
    {
        private static readonly HashSet<string> JunkExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".bak", ".tmp", ".orig", ".swp", ".old", ".temp"
        };

        private static readonly HashSet<string> JunkFileNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "thumbs.db", ".ds_store", "desktop.ini", "ehthumbs.db"
        };

        /// <summary>
        /// Проверяет, является ли файл мусорным (временным, системным или резервной копией).
        /// </summary>
        public static bool IsJunkFile(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return false;

            string fileName = Path.GetFileName(filePath);
            if (JunkFileNames.Contains(fileName)) return true;
            if (fileName.StartsWith("._", StringComparison.OrdinalIgnoreCase)) return true; // macOS resource fork

            string ext = Path.GetExtension(filePath);
            if (JunkExtensions.Contains(ext)) return true;
            if (fileName.EndsWith("~", StringComparison.Ordinal)) return true;

            return false;
        }

        /// <summary>
        /// Рекурсивно очищает каталог от всех мусорных файлов (.bak, .tmp, Thumbs.db, .DS_Store и т.д.).
        /// </summary>
        public static (int FilesDeleted, long BytesFreed) CleanJunkFiles(string directory, ProcessingTask? task = null)
        {
            if (!Directory.Exists(directory)) return (0, 0);

            int deletedCount = 0;
            long freedBytes = 0;

            try
            {
                var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    if (IsJunkFile(file))
                    {
                        try
                        {
                            var fi = new FileInfo(file);
                            long size = fi.Length;
                            fi.Attributes = FileAttributes.Normal;
                            File.Delete(file);
                            deletedCount++;
                            freedBytes += size;
                        }
                        catch { }
                    }
                }

                if (deletedCount > 0)
                {
                    string freedStr = ProcessingTask.FormatSize(freedBytes);
                    App.Logger.Log($"[Mod Optimizer] Удалено {deletedCount} мусорных файлов в «{Path.GetFileName(directory)}» (освобождено {freedStr})", LogLevel.Info);
                    if (task != null)
                    {
                        App.RunOnUI(() => task.LogDetails += $"\n🗑️ [Mod Optimizer] Удалено {deletedCount} мусорных файлов ({freedStr} сэкономлено)");
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[Mod Optimizer] Ошибка очистки мусора в {directory}: {ex.Message}", LogLevel.Warning);
            }

            return (deletedCount, freedBytes);
        }

        /// <summary>
        /// Находит исполняемый файл oxipng.exe.
        /// </summary>
        public static string? FindOxipngExe()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidatePaths = new[]
            {
                Path.Combine(baseDir, "tools", "oxipng.exe"),
                Path.Combine(baseDir, "tools", "oxipng", "oxipng.exe"),
                Path.Combine(baseDir, "..", "..", "..", "..", "tools", "oxipng.exe"),
                Path.Combine(AppContext.BaseDirectory, "tools", "oxipng.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cargo", "bin", "oxipng.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gemini", "antigravity", "bin", "oxipng.exe")
            };

            foreach (var p in candidatePaths)
            {
                if (File.Exists(p)) return Path.GetFullPath(p);
            }

            // Поиск в переменной PATH
            var pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(pathEnv))
            {
                foreach (var dir in pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    try
                    {
                        string candidate = Path.Combine(dir.Trim(), "oxipng.exe");
                        if (File.Exists(candidate)) return candidate;
                    }
                    catch { }
                }
            }

            return null;
        }

        /// <summary>
        /// Оптимизирует все PNG текстуры в указанной директории без потери качества через Oxipng.
        /// </summary>
        public static async Task<(int OptimizedCount, long BytesFreed)> OptimizeTexturesAsync(
            string directory, 
            ProcessingTask? task = null, 
            CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(directory)) return (0, 0);

            string? oxipngPath = FindOxipngExe();
            if (string.IsNullOrEmpty(oxipngPath) || !File.Exists(oxipngPath))
            {
                App.Logger.Log("[Mod Optimizer] oxipng.exe не найден — пропуск сжатия текстур", LogLevel.Warning);
                return (0, 0);
            }

            List<string> pngFiles;
            try
            {
                pngFiles = Directory.GetFiles(directory, "*.png", SearchOption.AllDirectories).ToList();
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[Mod Optimizer] Ошибка перечисления PNG в {directory}: {ex.Message}", LogLevel.Warning);
                return (0, 0);
            }

            if (pngFiles.Count == 0) return (0, 0);

            long initialTotalBytes = 0;
            foreach (var f in pngFiles)
            {
                try { initialTotalBytes += new FileInfo(f).Length; } catch { }
            }

            if (initialTotalBytes == 0) return (0, 0);

            App.Logger.Log($"[Mod Optimizer] Старт оптимизации {pngFiles.Count} PNG текстур через Oxipng (исходный вес: {ProcessingTask.FormatSize(initialTotalBytes)})...", LogLevel.Info);
            if (task != null)
            {
                App.RunOnUI(() => task.LogDetails += $"\n🎨 [Mod Optimizer] Сжатие {pngFiles.Count} PNG текстур без потерь (Oxipng)...");
            }

            // Обрабатываем файлы пачками по 30 штук, чтобы избежать переполнения командной строки Windows (8191 символ)
            const int batchSize = 30;
            for (int i = 0; i < pngFiles.Count; i += batchSize)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var batch = pngFiles.Skip(i).Take(batchSize).ToList();
                var formattedFiles = string.Join(" ", batch.Select(f => $"\"{f}\""));
                // Параметры: -o 4 (высокая степень сжатия без потерь), --strip safe (удаление метаданных без нарушения формата), -t 0 (все ядра)
                string arguments = $"-o 4 --strip safe -t 0 {formattedFiles}";

                var psi = new ProcessStartInfo
                {
                    FileName = oxipngPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                try
                {
                    using var proc = Process.Start(psi);
                    if (proc != null)
                    {
                        using (cancellationToken.Register(() => { try { proc.Kill(true); } catch { } }))
                        {
                            await proc.WaitForExitAsync(cancellationToken);
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.Log($"[Mod Optimizer] Ошибка выполнения Oxipng на пакете файлов: {ex.Message}", LogLevel.Warning);
                }
            }

            long finalTotalBytes = 0;
            foreach (var f in pngFiles)
            {
                try { finalTotalBytes += new FileInfo(f).Length; } catch { }
            }

            long freedBytes = Math.Max(0, initialTotalBytes - finalTotalBytes);
            double reductionPercent = initialTotalBytes > 0 ? ((double)freedBytes / initialTotalBytes * 100.0) : 0.0;

            if (freedBytes > 0)
            {
                string freedStr = ProcessingTask.FormatSize(freedBytes);
                App.Logger.Log($"[Mod Optimizer] Оптимизировано {pngFiles.Count} PNG текстур. Сэкономлено: {freedStr} (-{reductionPercent:F1}%)", LogLevel.Success);
                if (task != null)
                {
                    App.RunOnUI(() => task.LogDetails += $"\n✨ [Mod Optimizer] Сэкономлено {freedStr} (-{reductionPercent:F1}%) на оптимизации текстур");
                }
            }

            return (pngFiles.Count, freedBytes);
        }

        /// <summary>
        /// Комплексная оптимизация папки мода: очистка от мусора и lossless сжатие всех PNG текстур.
        /// </summary>
        public static async Task OptimizeModDirectoryAsync(
            string modDirectory, 
            ProcessingTask? task = null, 
            CancellationToken cancellationToken = default)
        {
            if (!Directory.Exists(modDirectory)) return;

            CleanJunkFiles(modDirectory, task);
            await OptimizeTexturesAsync(modDirectory, task, cancellationToken);
        }
    }
}
