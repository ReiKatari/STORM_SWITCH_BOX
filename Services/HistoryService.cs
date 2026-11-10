using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
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

public static string GetDeterministicSourceHash(ProcessingTask task)
        {
            string? src = task.InputFiles?.FirstOrDefault(f => !string.IsNullOrWhiteSpace(f)) ?? task.OutputFileName;
            if (string.IsNullOrWhiteSpace(src)) return string.Empty;
            try
            {
                string norm = src.Trim().ToLowerInvariant();
                byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(norm));
                return "src_" + Convert.ToHexString(hash)[..12].ToLowerInvariant();
            }
            catch
            {
                return string.Empty;
            }
        }

        public static void SaveTaskIconToCache(ProcessingTask task, byte[]? iconBytes)
        {
            if (task == null || iconBytes == null || iconBytes.Length < 64) return;
            task.IconBytes = iconBytes;

            // Set GameIcon and IconBitmap immediately on UI thread
            App.RunOnUI(async () =>
            {
                try
                {
                    using var ms = new MemoryStream(iconBytes);
                    var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                    await bmp.SetSourceAsync(ms.AsRandomAccessStream());
                    task.GameIcon = bmp;
                    task.IconBitmap = bmp;
                }
                catch { }
            });

            string iconsDir = GetIconsDirectory();
            try { Directory.CreateDirectory(iconsDir); } catch { }

            string? titleId = ExtractTitleId(task);
            string baseTitleId = !string.IsNullOrEmpty(titleId) ? NormalizeTitleIdIfSwitch(titleId) : string.Empty;
            string srcHash = GetDeterministicSourceHash(task);
            bool isGenericTaskId = !string.IsNullOrEmpty(task.Id) && Regex.IsMatch(task.Id, @"^T\d+$", RegexOptions.IgnoreCase);

            Task.Run(async () =>
            {
                try
                {
                    if (!string.IsNullOrEmpty(task.Id) && !isGenericTaskId)
                    {
                        await File.WriteAllBytesAsync(Path.Combine(iconsDir, $"{task.Id}.png"), iconBytes);
                    }
                    if (!string.IsNullOrEmpty(titleId))
                    {
                        await File.WriteAllBytesAsync(Path.Combine(iconsDir, $"{titleId}.png"), iconBytes);
                    }
                    if (!string.IsNullOrEmpty(baseTitleId) && !string.Equals(baseTitleId, titleId, StringComparison.OrdinalIgnoreCase))
                    {
                        await File.WriteAllBytesAsync(Path.Combine(iconsDir, $"{baseTitleId}.png"), iconBytes);
                    }
                    if (!string.IsNullOrEmpty(task.GroupId))
                    {
                        string safeGroupId = string.Join("_", task.GroupId.Split(Path.GetInvalidFileNameChars()));
                        if (!string.IsNullOrEmpty(safeGroupId))
                        {
                            await File.WriteAllBytesAsync(Path.Combine(iconsDir, $"{safeGroupId}.png"), iconBytes);
                        }
                    }
                    if (!string.IsNullOrEmpty(srcHash))
                    {
                        await File.WriteAllBytesAsync(Path.Combine(iconsDir, $"{srcHash}.png"), iconBytes);
                    }
                }
                catch { }
            });
        }

        public static string? ExtractTitleId(ProcessingTask task)
        {
            // 0. Прямое свойство TitleId задачи
            if (!string.IsNullOrWhiteSpace(task.TitleId))
            {
                var match = System.Text.RegularExpressions.Regex.Match(task.TitleId, @"(?i)(?<![0-9a-f])([0-9a-f]{16})(?![0-9a-f])");
                if (match.Success)
                {
                    string tid = match.Groups[1].Value.ToUpperInvariant();
                    return NormalizeTitleIdIfSwitch(tid);
                }
            }

            // 1. Поиск 16-значного hex TitleID в OutputFileName
            if (!string.IsNullOrEmpty(task.OutputFileName))
            {
                var match = System.Text.RegularExpressions.Regex.Match(task.OutputFileName, @"(?i)(?<![0-9a-f])([0-9a-f]{16})(?![0-9a-f])");
                if (match.Success)
                {
                    string tid = match.Groups[1].Value.ToUpperInvariant();
                    return NormalizeTitleIdIfSwitch(tid);
                }
            }

            // 2. Поиск в GameName
            if (!string.IsNullOrEmpty(task.GameName))
            {
                var match = System.Text.RegularExpressions.Regex.Match(task.GameName, @"(?i)(?<![0-9a-f])([0-9a-f]{16})(?![0-9a-f])");
                if (match.Success)
                {
                    string tid = match.Groups[1].Value.ToUpperInvariant();
                    return NormalizeTitleIdIfSwitch(tid);
                }
            }

            // 3. Поиск в GroupId (16 или 12 символов)
            if (!string.IsNullOrEmpty(task.GroupId))
            {
                var match16 = System.Text.RegularExpressions.Regex.Match(task.GroupId, @"(?i)(?<![0-9a-f])([0-9a-f]{16})(?![0-9a-f])");
                if (match16.Success)
                {
                    string tid = match16.Groups[1].Value.ToUpperInvariant();
                    return NormalizeTitleIdIfSwitch(tid);
                }

                var match12 = System.Text.RegularExpressions.Regex.Match(task.GroupId, @"(?i)^([0-9a-f]{12})");
                if (match12.Success) return match12.Groups[1].Value.ToUpperInvariant();
            }

            // 4. Поиск в InputFiles
            if (task.InputFiles != null)
            {
                foreach (var file in task.InputFiles)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(Path.GetFileName(file), @"(?i)(?<![0-9a-f])([0-9a-f]{16})(?![0-9a-f])");
                    if (match.Success)
                    {
                        string tid = match.Groups[1].Value.ToUpperInvariant();
                        return NormalizeTitleIdIfSwitch(tid);
                    }
                }
            }

            // 5. Поиск в RuTracker TorrentFiles
            if (task.RuTrackerPayload?.TorrentFiles != null)
            {
                foreach (var tf in task.RuTrackerPayload.TorrentFiles)
                {
                    var match = System.Text.RegularExpressions.Regex.Match(tf.RelativePath, @"(?i)(?<![0-9a-f])([0-9a-f]{16})(?![0-9a-f])");
                    if (match.Success)
                    {
                        string tid = match.Groups[1].Value.ToUpperInvariant();
                        return NormalizeTitleIdIfSwitch(tid);
                    }
                }
            }

            return null;
        }

        private static string NormalizeTitleIdIfSwitch(string tid)
        {
            if (string.IsNullOrWhiteSpace(tid)) return string.Empty;
            tid = tid.Trim();
            if (tid.Length == 16)
            {
                if (tid.StartsWith("0100", StringComparison.OrdinalIgnoreCase))
                {
                    string norm = RuTrackerModService.NormalizeSwitchBaseApplicationTitleId(tid);
                    if (!string.IsNullOrEmpty(norm)) return norm;
                }
                else if (tid.StartsWith("0400", StringComparison.OrdinalIgnoreCase) &&
                         ulong.TryParse(tid, System.Globalization.NumberStyles.HexNumber, null, out ulong tidVal))
                {
                    ulong baseVal;
                    if ((tidVal & 0xFFFUL) == 0x800UL)
                    {
                        baseVal = tidVal & ~0xFFFUL;
                    }
                    else if ((tidVal & 0xFFFUL) > 0UL)
                    {
                        baseVal = (tidVal - 0x1000UL) & ~0xFFFUL;
                    }
                    else
                    {
                        baseVal = tidVal;
                    }
                    return $"{baseVal:X16}";
                }
            }
            return tid;
        }

        private static int _titleDbSubscribed = 0;

        public static void TryLoadIconForTask(ProcessingTask task)
        {
            if (task.GameIcon != null) return;

            // 1. Проверяем task.IconBytes или кастомные метаданные
            byte[]? directBytes = task.IconBytes ?? task.CustomMetadata?.CustomIconBytes ?? task.CustomMetadata?.OriginalIconBytes;
            if (directBytes != null && directBytes.Length > 64)
            {
                SaveTaskIconToCache(task, directBytes);
                return;
            }

            string iconsDir = GetIconsDirectory();
            string? titleId = ExtractTitleId(task);
            string baseTitleId = !string.IsNullOrEmpty(titleId) ? NormalizeTitleIdIfSwitch(titleId) : string.Empty;
            string safeGroupId = !string.IsNullOrEmpty(task.GroupId) 
                ? string.Join("_", task.GroupId.Split(Path.GetInvalidFileNameChars())) 
                : "";
            string srcHash = GetDeterministicSourceHash(task);
            bool isGenericTaskId = !string.IsNullOrEmpty(task.Id) && Regex.IsMatch(task.Id, @"^T\d+$", RegexOptions.IgnoreCase);

            // 2. Список возможных путей к иконке в кэше
            var candidatePaths = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(srcHash))
            {
                candidatePaths.Add(Path.Combine(iconsDir, $"{srcHash}.png"));
            }
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
            if (!string.IsNullOrEmpty(baseTitleId) && !candidatePaths.Contains(Path.Combine(iconsDir, $"{baseTitleId}.png")))
            {
                candidatePaths.Add(Path.Combine(iconsDir, $"{baseTitleId}.png"));
            }
            if (!string.IsNullOrEmpty(safeGroupId))
            {
                candidatePaths.Add(Path.Combine(iconsDir, $"{safeGroupId}.png"));
            }
            if (!string.IsNullOrEmpty(task.GroupId))
            {
                candidatePaths.Add(Path.Combine(iconsDir, $"{task.GroupId}.png"));
            }
            if (!string.IsNullOrEmpty(task.Id) && !isGenericTaskId)
            {
                candidatePaths.Add(Path.Combine(iconsDir, $"{task.Id}.png"));
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

            // Подписка на DatabaseLoaded TitleDB если база еще загружается
            if (App.TitleDb != null && !App.TitleDb.IsLoaded)
            {
                if (System.Threading.Interlocked.CompareExchange(ref _titleDbSubscribed, 1, 0) == 0)
                {
                    App.TitleDb.DatabaseLoaded += () =>
                    {
                        if (App.TasksVM != null)
                        {
                            App.RunOnUI(() =>
                            {
                                foreach (var t in App.TasksVM.Tasks)
                                {
                                    if (t.GameIcon == null)
                                    {
                                        TryLoadIconForTask(t);
                                    }
                                }
                            });
                        }
                    };
                }
            }

            // Fallback: асинхронное извлечение из файлов, TitleDB или постера раздачи
            Task.Run(async () =>
            {
                try
                {
                    byte[]? iconBytes = null;

                    // Fallback 1: Попытка извлечь из существующих локальных файлов задачи
                    var filesToProbe = new List<string>();
                    if (task.InputFiles != null) filesToProbe.AddRange(task.InputFiles);
                    if (task.RuTrackerPayload != null && !string.IsNullOrWhiteSpace(task.RuTrackerPayload.TargetReleaseDir) && Directory.Exists(task.RuTrackerPayload.TargetReleaseDir))
                    {
                        try
                        {
                            filesToProbe.AddRange(Directory.GetFiles(task.RuTrackerPayload.TargetReleaseDir, "*.*", SearchOption.AllDirectories)
                                .Where(f => f.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) ||
                                            f.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) ||
                                            f.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) ||
                                            f.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase) ||
                                            f.EndsWith(".nup", StringComparison.OrdinalIgnoreCase) ||
                                            f.EndsWith(".3ds", StringComparison.OrdinalIgnoreCase) ||
                                            f.EndsWith(".cia", StringComparison.OrdinalIgnoreCase)));
                        }
                        catch { }
                    }

                    foreach (var f in filesToProbe)
                    {
                        if (Directory.Exists(f))
                        {
                            var iconDirs = new[] { Path.Combine(f, "control"), Path.Combine(f, "basedata", "control") };
                            foreach (var id in iconDirs)
                            {
                                if (Directory.Exists(id))
                                {
                                    var ic = Directory.GetFiles(id, "icon_*.dat").FirstOrDefault();
                                    if (ic != null && File.Exists(ic) && new FileInfo(ic).Length > 64)
                                    {
                                        try { iconBytes = File.ReadAllBytes(ic); break; } catch { }
                                    }
                                }
                            }
                            if (iconBytes == null)
                            {
                                var imgs = new[] { Path.Combine(f, "icon.jpg"), Path.Combine(f, "icon.png"), Path.Combine(f, "cover.jpg"), Path.Combine(f, "cover.png") };
                                foreach (var img in imgs)
                                {
                                    if (File.Exists(img) && new FileInfo(img).Length > 64)
                                    {
                                        try { iconBytes = File.ReadAllBytes(img); break; } catch { }
                                    }
                                }
                            }
                            if (iconBytes != null) break;
                        }
                        else if (File.Exists(f))
                        {
                            if (f.EndsWith(".nup", StringComparison.OrdinalIgnoreCase) || NupContainerService.IsNupFile(f))
                            {
                                if (NupContainerService.TryExtractIconAndMetadata(f, out var nupIcon, out _, out _, out _) && nupIcon != null && nupIcon.Length > 64)
                                {
                                    iconBytes = nupIcon;
                                    break;
                                }
                            }

                            if (Nintendo3dsService.Is3dsExtension(Path.GetExtension(f)))
                            {
                                var n3ds = App.Nintendo3ds.Parse3dsFile(f);
                                if (n3ds?.IconBytes != null && n3ds.IconBytes.Length > 64)
                                {
                                    iconBytes = n3ds.IconBytes;
                                    break;
                                }
                            }
                            else
                            {
                                var meta = App.SwitchFormat.ParseNsp(f);
                                if (meta?.IconBytes != null && meta.IconBytes.Length > 64)
                                {
                                    iconBytes = meta.IconBytes;
                                    break;
                                }
                                if (App.SwitchFormat.TryExtractControlNcaCoverAndMetadata(f, out var fbIcon, out _, out _, out _) && fbIcon != null && fbIcon.Length > 64)
                                {
                                    iconBytes = fbIcon;
                                    break;
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
                                if (n3ds?.IconBytes != null && n3ds.IconBytes.Length > 64) iconBytes = n3ds.IconBytes;
                            }
                            else
                            {
                                var meta = App.SwitchFormat.ParseNsp(existingOut);
                                if (meta?.IconBytes != null && meta.IconBytes.Length > 64) iconBytes = meta.IconBytes;
                            }
                        }
                    }

                    // Fallback 2: Поиск в TitleDB
                    if (iconBytes == null && App.TitleDb != null)
                    {
                        TitleDbEntry? entry = null;
                        if (!string.IsNullOrEmpty(baseTitleId) && App.TitleDb.TryGetTitleInfo(baseTitleId, out entry))
                        {
                        }
                        else if (!string.IsNullOrEmpty(titleId) && App.TitleDb.TryGetTitleInfo(titleId, out entry))
                        {
                        }
                        else if (!string.IsNullOrWhiteSpace(task.GameName) || !string.IsNullOrWhiteSpace(task.OutputFileName))
                        {
                            string rawName = !string.IsNullOrWhiteSpace(task.GameName) ? task.GameName : task.OutputFileName;
                            string cleanName = System.Text.RegularExpressions.Regex.Replace(rawName, @"\s*[\[\(].*?[\]\)]", "").Trim();
                            cleanName = System.Text.RegularExpressions.Regex.Replace(cleanName, @"\s*[-–—]\s*(?:Update|Патч|DLC|Обновление|v\d).*$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
                            cleanName = System.Text.RegularExpressions.Regex.Replace(cleanName, @"\s*\+\s*(?:DLC|Патч|Update).*$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
                            cleanName = cleanName.Trim(' ', '-', '–', '—', '+');

                            if (cleanName.Length >= 3)
                            {
                                entry = App.TitleDb.GetAllEntries().FirstOrDefault(e =>
                                    !string.IsNullOrEmpty(e.Name) &&
                                    (string.Equals(e.Name.Trim(), cleanName, StringComparison.OrdinalIgnoreCase) ||
                                     e.Name.Trim().StartsWith(cleanName, StringComparison.OrdinalIgnoreCase) ||
                                     cleanName.StartsWith(e.Name.Trim(), StringComparison.OrdinalIgnoreCase)));
                            }
                        }

                        if (entry != null && !string.IsNullOrEmpty(entry.IconUrl))
                        {
                            try
                            {
                                using var http = new System.Net.Http.HttpClient();
                                http.Timeout = TimeSpan.FromSeconds(6);
                                iconBytes = await http.GetByteArrayAsync(entry.IconUrl);
                            }
                            catch { }
                        }
                    }

                    // Fallback 3: PosterUrl из раздачи RuTracker
                    if (iconBytes == null)
                    {
                        string posterUrl = task.RuTrackerPayload?.PosterUrl ?? "";
                        if (string.IsNullOrWhiteSpace(posterUrl) && task.RuTrackerPayload?.Topic != null)
                        {
                            posterUrl = task.RuTrackerPayload.Topic.PosterUrl;
                            if (string.IsNullOrWhiteSpace(posterUrl))
                            {
                                string topicRef = !string.IsNullOrWhiteSpace(task.RuTrackerPayload.Topic.TopicUrl)
                                    ? task.RuTrackerPayload.Topic.TopicUrl
                                    : task.RuTrackerPayload.Topic.TopicId;
                                if (!string.IsNullOrWhiteSpace(topicRef))
                                {
                                    try
                                    {
                                        using var ctsTopic = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(8));
                                        var rtSvc = new RuTrackerModService();
                                        var details = await rtSvc.FetchTopicDetailsAsync(topicRef, ctsTopic.Token);
                                        if (details != null && !string.IsNullOrWhiteSpace(details.PosterUrl))
                                        {
                                            posterUrl = details.PosterUrl;
                                            task.RuTrackerPayload.PosterUrl = posterUrl;
                                            task.RuTrackerPayload.Topic.PosterUrl = posterUrl;
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                        if (!string.IsNullOrWhiteSpace(posterUrl))
                        {
                            try
                            {
                                using var http = new System.Net.Http.HttpClient();
                                http.Timeout = TimeSpan.FromSeconds(6);
                                iconBytes = await http.GetByteArrayAsync(posterUrl);
                            }
                            catch { }
                        }
                    }

                    // Если иконка получена — сохраняем в кэш и отображаем в UI
                    if (iconBytes != null && iconBytes.Length > 64)
                    {
                        SaveTaskIconToCache(task, iconBytes);
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
                    task.IconBytes = bytes;
                    App.RunOnUI(async () =>
                    {
                        try
                        {
                            using var ms = new MemoryStream(bytes);
                            var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                            await bitmap.SetSourceAsync(ms.AsRandomAccessStream());
                            task.GameIcon = bitmap;
                            task.IconBitmap = bitmap;
                        }
                        catch { }
                    });
                }
                catch { }
            });
        }

        private static readonly SemaphoreSlim _saveLock = new SemaphoreSlim(1, 1);

        public static async Task SaveHistoryAsync()
        {
            await _saveLock.WaitAsync();
            try
            {
                var json = JsonSerializer.Serialize(HistoryTasks, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(HistoryFilePath, json);
            }
            catch (Exception ex)
            {
                App.Logger.Log($"Ошибка сохранения истории: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                _saveLock.Release();
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
