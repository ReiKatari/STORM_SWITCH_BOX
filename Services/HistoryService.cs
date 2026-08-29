using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public static class HistoryService
    {
        private static string GetHistoryFilePath()
        {
            string localAppDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StormSwitchBox");
            Directory.CreateDirectory(localAppDataDir);
            string appDataPath = Path.Combine(localAppDataDir, "history.json");
            
            // Миграция старого history.json из BaseDirectory, если он есть
            string legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "history.json");
            if (!File.Exists(appDataPath) && File.Exists(legacyPath))
            {
                try { File.Copy(legacyPath, appDataPath, true); } catch { }
            }
            return appDataPath;
        }

        public static string GetIconsDirectory()
        {
            string localAppDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StormSwitchBox", "icons");
            Directory.CreateDirectory(localAppDataDir);

            // Миграция старых иконок из BaseDirectory, если они есть
            string legacyIconsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons");
            if (Directory.Exists(legacyIconsDir))
            {
                try
                {
                    foreach (var file in Directory.EnumerateFiles(legacyIconsDir, "*.png"))
                    {
                        string dest = Path.Combine(localAppDataDir, Path.GetFileName(file));
                        if (!File.Exists(dest))
                        {
                            File.Copy(file, dest, true);
                        }
                    }
                }
                catch { }
            }

            return localAppDataDir;
        }

        private static readonly string HistoryFilePath = GetHistoryFilePath();
        public static ObservableCollection<ProcessingTask> HistoryTasks { get; set; } = new ObservableCollection<ProcessingTask>();

        public static async Task LoadHistoryAsync()
        {
            try
            {
                if (File.Exists(HistoryFilePath))
                {
                    var json = await File.ReadAllTextAsync(HistoryFilePath);
                    var items = JsonSerializer.Deserialize<ObservableCollection<ProcessingTask>>(json);
                    if (items != null)
                    {
                        App.RunOnUI(() =>
                        {
                            HistoryTasks.Clear();
                            foreach (var item in items)
                            {
                                HistoryTasks.Add(item);
                                TryLoadIconForTask(item);
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"Ошибка загрузки истории: {ex.Message}", LogLevel.Error);
            }
        }

        public static string? ExtractTitleId(ProcessingTask task)
        {
            // 1. Поиск 16-значного hex TitleID в OutputFileName
            if (!string.IsNullOrEmpty(task.OutputFileName))
            {
                var match = System.Text.RegularExpressions.Regex.Match(task.OutputFileName, @"(?i)\b([0-9a-f]{16})\b");
                if (match.Success) return match.Groups[1].Value.ToUpperInvariant();
            }

            // 2. Поиск в GroupId (16 или 12 символов)
            if (!string.IsNullOrEmpty(task.GroupId))
            {
                var match16 = System.Text.RegularExpressions.Regex.Match(task.GroupId, @"(?i)\b([0-9a-f]{16})\b");
                if (match16.Success) return match16.Groups[1].Value.ToUpperInvariant();

                var match12 = System.Text.RegularExpressions.Regex.Match(task.GroupId, @"(?i)^([0-9a-f]{12})");
                if (match12.Success) return match12.Groups[1].Value.ToUpperInvariant();
            }

            // 3. Поиск в InputFiles
            if (task.InputFiles != null)
            {
                foreach (var file in task.InputFiles)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(Path.GetFileName(file), @"(?i)\b([0-9a-f]{16})\b");
                    if (match.Success) return match.Groups[1].Value.ToUpperInvariant();
                }
            }

            return null;
        }

        public static void TryLoadIconForTask(ProcessingTask task)
        {
            if (task.GameIcon != null) return;

            string iconsDir = GetIconsDirectory();
            string? titleId = ExtractTitleId(task);
            string safeGroupId = !string.IsNullOrEmpty(task.GroupId) 
                ? string.Join("_", task.GroupId.Split(Path.GetInvalidFileNameChars())) 
                : "";

            // Список возможных путей к иконке в кэше
            var candidatePaths = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(titleId))
            {
                candidatePaths.Add(Path.Combine(iconsDir, $"{titleId}.png"));
                if (titleId.Length == 16)
                {
                    string baseTid = titleId.Substring(0, 13) + "000";
                    candidatePaths.Add(Path.Combine(iconsDir, $"{baseTid}.png"));
                    candidatePaths.Add(Path.Combine(iconsDir, $"{titleId.Substring(0, 12)}.png"));
                }
            }
            if (!string.IsNullOrEmpty(safeGroupId))
            {
                candidatePaths.Add(Path.Combine(iconsDir, $"{safeGroupId}.png"));
            }
            if (!string.IsNullOrEmpty(task.GroupId))
            {
                candidatePaths.Add(Path.Combine(iconsDir, $"{task.GroupId}.png"));
            }

            // Проверяем наличие в локальном кэше
            foreach (var iconPath in candidatePaths)
            {
                if (File.Exists(iconPath))
                {
                    LoadBitmapFromPath(task, iconPath);
                    return;
                }
            }

            // Fallback: асинхронное извлечение из файлов или TitleDB в фоне
            Task.Run(async () =>
            {
                try
                {
                    byte[]? iconBytes = null;

                    // Fallback 1: Попытка извлечь из существующих локальных файлов задачи
                    if (task.InputFiles != null)
                    {
                        foreach (var f in task.InputFiles)
                        {
                            if (File.Exists(f))
                            {
                                if (Nintendo3dsService.Is3dsExtension(Path.GetExtension(f)))
                                {
                                    var n3ds = App.Nintendo3ds.Parse3dsFile(f);
                                    if (n3ds?.IconBytes != null && n3ds.IconBytes.Length > 0)
                                    {
                                        iconBytes = n3ds.IconBytes;
                                        break;
                                    }
                                }
                                else
                                {
                                    var meta = App.SwitchFormat.ParseNsp(f);
                                    if (meta?.IconBytes != null && meta.IconBytes.Length > 0)
                                    {
                                        iconBytes = meta.IconBytes;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    // Проверка выходного файла
                    if (iconBytes == null && !string.IsNullOrEmpty(task.OutputFolder) && !string.IsNullOrEmpty(task.OutputFileName))
                    {
                        string outNsp = Path.Combine(task.OutputFolder, task.OutputFileName + ".nsp");
                        string outNsz = Path.Combine(task.OutputFolder, task.OutputFileName + ".nsz");
                        string outXci = Path.Combine(task.OutputFolder, task.OutputFileName + ".xci");
                        string outCia = Path.Combine(task.OutputFolder, task.OutputFileName + ".cia");
                        string out3ds = Path.Combine(task.OutputFolder, task.OutputFileName + ".3ds");

                        string? existingOut = new[] { outNsp, outNsz, outXci, outCia, out3ds }.FirstOrDefault(File.Exists);
                        if (existingOut != null)
                        {
                            if (Nintendo3dsService.Is3dsExtension(Path.GetExtension(existingOut)))
                            {
                                var n3ds = App.Nintendo3ds.Parse3dsFile(existingOut);
                                if (n3ds?.IconBytes != null && n3ds.IconBytes.Length > 0) iconBytes = n3ds.IconBytes;
                            }
                            else
                            {
                                var meta = App.SwitchFormat.ParseNsp(existingOut);
                                if (meta?.IconBytes != null && meta.IconBytes.Length > 0) iconBytes = meta.IconBytes;
                            }
                        }
                    }

                    // Fallback 2: Поиск в TitleDB
                    if (iconBytes == null && !string.IsNullOrEmpty(titleId) && titleId.Length == 16)
                    {
                        if (App.TitleDb.TryGetTitleInfo(titleId, out var entry) && entry != null && !string.IsNullOrEmpty(entry.IconUrl))
                        {
                            try
                            {
                                using var http = new System.Net.Http.HttpClient();
                                http.Timeout = TimeSpan.FromSeconds(5);
                                iconBytes = await http.GetByteArrayAsync(entry.IconUrl);
                            }
                            catch { }
                        }
                    }

                    // Если иконка получена — сохраняем в кэш и отображаем в UI
                    if (iconBytes != null && iconBytes.Length > 0)
                    {
                        string saveKey = !string.IsNullOrEmpty(titleId) ? titleId : safeGroupId;
                        if (!string.IsNullOrEmpty(saveKey))
                        {
                            string savePath = Path.Combine(iconsDir, $"{saveKey}.png");
                            try { await File.WriteAllBytesAsync(savePath, iconBytes); } catch { }
                        }

                        App.RunOnUI(async () =>
                        {
                            try
                            {
                                using var ms = new MemoryStream(iconBytes);
                                var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                                await bitmap.SetSourceAsync(ms.AsRandomAccessStream());
                                task.GameIcon = bitmap;
                            }
                            catch { }
                        });
                    }
                }
                catch { }
            });
        }

        private static void LoadBitmapFromPath(ProcessingTask task, string iconPath)
        {
            Task.Run(async () =>
            {
                try
                {
                    var bytes = await File.ReadAllBytesAsync(iconPath);
                    App.RunOnUI(async () =>
                    {
                        try
                        {
                            using var ms = new MemoryStream(bytes);
                            var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                            await bitmap.SetSourceAsync(ms.AsRandomAccessStream());
                            task.GameIcon = bitmap;
                        }
                        catch { }
                    });
                }
                catch { }
            });
        }

        public static async Task SaveHistoryAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(HistoryTasks, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(HistoryFilePath, json);
            }
            catch (Exception ex)
            {
                App.Logger.Log($"Ошибка сохранения истории: {ex.Message}", LogLevel.Error);
            }
        }

        public static void AddToHistory(ProcessingTask task)
        {
            App.RunOnUI(async () =>
            {
                // Проверка на дублирование (если запись для этой задачи уже была добавлена недавно)
                var existing = HistoryTasks.FirstOrDefault(t => t.Id == task.Id && Math.Abs((DateTime.Now - t.FinishedAt).TotalSeconds) < 15);
                if (existing != null)
                {
                    existing.Status = task.Status;
                    existing.TargetSize = task.TargetSize;
                    existing.SizeDifference = task.SizeDifference;
                    existing.LogDetails = task.LogDetails;
                    await SaveHistoryAsync();
                    return;
                }

                // Клонируем задачу для истории
                var copy = new ProcessingTask
                {
                    Id = task.Id,
                    GroupId = task.GroupId,
                    FinishedAt = DateTime.Now,
                    Operation = task.Operation,
                    Status = task.Status,
                    SourceFormat = task.SourceFormat,
                    TargetFormat = task.TargetFormat,
                    SourceSizeBytes = task.SourceSizeBytes,
                    SourceSize = task.SourceSize,
                    TargetSize = task.TargetSize,
                    SizeDifference = task.SizeDifference,
                    CompressionLevel = task.CompressionLevel,
                    FilesCount = task.FilesCount,
                    InputFiles = new System.Collections.Generic.List<string>(task.InputFiles),
                    FilesList = new System.Collections.Generic.List<string>(task.FilesList),
                    HasRomFs = task.HasRomFs,
                    HasExeFs = task.HasExeFs,
                    InputFolders = task.InputFolders,
                    OutputFolder = task.OutputFolder,
                    OutputFileName = task.OutputFileName,
                    LogDetails = task.LogDetails,
                    Progress = 100,
                    GameIcon = task.GameIcon
                };

                HistoryTasks.Insert(0, copy);
                if (HistoryTasks.Count > 100) // Храним только последние 100 записей
                {
                    HistoryTasks.RemoveAt(HistoryTasks.Count - 1);
                }

                if (copy.GameIcon == null)
                {
                    TryLoadIconForTask(copy);
                }
                
                await SaveHistoryAsync();
                
                if (copy.Status == "Успешно" || copy.Status == "Готово")
                {
                    App.NotifyTaskCompleted(copy);
                }
            });
        }
    }
}
