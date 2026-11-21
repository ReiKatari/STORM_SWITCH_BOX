using System;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public class WatchFolderService
    {
        // Switch Watcher
        private FileSystemWatcher? _watcherSwitch;
        private bool _isSwitchStarted = false;
        private CancellationTokenSource? _ctsSwitch;
        private readonly ConcurrentDictionary<string, (long LastSize, DateTime LastChange, int StableCount)> _pendingFilesSwitch = new(StringComparer.OrdinalIgnoreCase);

        // 3DS Watcher
        private FileSystemWatcher? _watcher3ds;
        private bool _is3dsStarted = false;
        private CancellationTokenSource? _cts3ds;
        private readonly ConcurrentDictionary<string, (long LastSize, DateTime LastChange, int StableCount)> _pendingFiles3ds = new(StringComparer.OrdinalIgnoreCase);

        // Защита от бесконечного цикла перепаковки: недавно обработанные и созданные выходные файлы
        private readonly ConcurrentDictionary<string, DateTime> _recentlyProcessedOrOutputFiles = new(StringComparer.OrdinalIgnoreCase);

        public bool IsSwitchRunning => _isSwitchStarted;
        public bool Is3dsRunning => _is3dsStarted;

        public event Action? StateChanged;

        #region Switch Watcher

        public void Start()
        {
            StartSwitch();
            Start3ds();
        }

        public void Stop()
        {
            StopSwitch();
            Stop3ds();
        }

        public void StartSwitch()
        {
            StopSwitch();

            var settings = App.Settings.Current;
            if (string.IsNullOrWhiteSpace(settings.WatchFolderSwitch) || !Directory.Exists(settings.WatchFolderSwitch))
            {
                App.Logger?.Log("[WatchFolder Switch] Папка для отслеживания не указана или не существует.", Models.LogLevel.Warning);
                return;
            }

            try
            {
                _ctsSwitch = new CancellationTokenSource();
                _watcherSwitch = new FileSystemWatcher(settings.WatchFolderSwitch)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    Filter = "*.*",
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };

                _watcherSwitch.Created += OnSwitchFileDetected;
                _watcherSwitch.Changed += OnSwitchFileDetected;
                _watcherSwitch.Renamed += OnSwitchFileDetected;

                _isSwitchStarted = true;
                _ = MonitorPendingSwitchFilesLoopAsync(_ctsSwitch.Token);

                App.Logger?.Log($"[WatchFolder Switch] Отслеживание папки запущено: {settings.WatchFolderSwitch}", Models.LogLevel.Success);
                StateChanged?.Invoke();
            }
            catch (Exception ex)
            {
                App.Logger?.Log($"[WatchFolder Switch] Ошибка запуска: {ex.Message}", Models.LogLevel.Warning);
                _isSwitchStarted = false;
                StateChanged?.Invoke();
            }
        }

        public void StopSwitch()
        {
            if (_ctsSwitch != null)
            {
                _ctsSwitch.Cancel();
                _ctsSwitch.Dispose();
                _ctsSwitch = null;
            }

            if (_watcherSwitch != null)
            {
                _watcherSwitch.EnableRaisingEvents = false;
                _watcherSwitch.Created -= OnSwitchFileDetected;
                _watcherSwitch.Changed -= OnSwitchFileDetected;
                _watcherSwitch.Renamed -= OnSwitchFileDetected;
                _watcherSwitch.Dispose();
                _watcherSwitch = null;
            }
            _pendingFilesSwitch.Clear();
            _isSwitchStarted = false;
            App.Logger?.Log("[WatchFolder Switch] Отслеживание остановлено.", Models.LogLevel.Info);
            StateChanged?.Invoke();
        }

        private void OnSwitchFileDetected(object sender, FileSystemEventArgs e)
        {
            string ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
            if (ext != ".nsp" && ext != ".nsz" && ext != ".xci" && ext != ".xcz" && ext != ".zip" && ext != ".rar" && ext != ".7z")
                return;

            if (!File.Exists(e.FullPath))
                return;

            if (ShouldIgnoreWatchedFile(e.FullPath, App.Settings.Current.WatchFolderSwitch, App.Settings.Current.OutputFolder))
                return;

            _pendingFilesSwitch.AddOrUpdate(
                e.FullPath,
                path => (GetFileSizeSafe(path), DateTime.UtcNow, 0),
                (path, current) =>
                {
                    long newSize = GetFileSizeSafe(path);
                    if (newSize != current.LastSize)
                    {
                        return (newSize, DateTime.UtcNow, 0);
                    }
                    return current;
                });
        }

        private async Task MonitorPendingSwitchFilesLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(2000, ct);

                    foreach (var kvp in _pendingFilesSwitch.ToArray())
                    {
                        string filePath = kvp.Key;
                        var (lastSize, lastChange, stableCount) = kvp.Value;

                        if (!File.Exists(filePath))
                        {
                            _pendingFilesSwitch.TryRemove(filePath, out _);
                            continue;
                        }

                        if (IsFileLockedByActiveTask(filePath))
                        {
                            continue;
                        }

                        long currentSize = GetFileSizeSafe(filePath);
                        if (currentSize <= 0) continue;

                        if (currentSize == lastSize)
                        {
                            int newCount = stableCount + 1;
                            _pendingFilesSwitch[filePath] = (currentSize, lastChange, newCount);

                            if (newCount >= 3 && CanOpenExclusively(filePath))
                            {
                                _pendingFilesSwitch.TryRemove(filePath, out _);
                                if (!ShouldIgnoreWatchedFile(filePath, App.Settings.Current.WatchFolderSwitch, App.Settings.Current.OutputFolder))
                                {
                                    MarkRecentlyHandled(filePath);
                                    ProcessReadySwitchFile(filePath);
                                }
                            }
                        }
                        else
                        {
                            _pendingFilesSwitch[filePath] = (currentSize, DateTime.UtcNow, 0);
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    App.Logger?.Log($"[WatchFolder Switch] Ошибка очереди: {ex.Message}", Models.LogLevel.Warning);
                }
            }
        }

        private void ProcessReadySwitchFile(string filePath)
        {
            var settings = App.Settings.Current;
            string[] actions = new string[] { "Сжатие", "Распаковка", "Упаковка", "Конвертация", "Мульти-контент", "Проверка" };
            string[] pageTypes = new string[] { "Convert", "Unpack", "Pack", "Convert", "Multi", "Verify" };
            string[] formats = new string[] { "NSP", "NSZ", "XCI", "XCZ" };

            int actIdx = Math.Clamp(settings.WatchFolderActionSwitch, 0, actions.Length - 1);
            int fmtIdx = Math.Clamp(settings.WatchFolderFormatSwitch, 0, formats.Length - 1);
            string actionStr = actions[actIdx];
            string pageType = pageTypes[actIdx];
            string formatStr = (actIdx == 0 && fmtIdx == 0) ? "NSZ" : formats[fmtIdx];

            App.Logger?.Log($"[WatchFolder Switch] Авто-обработка «{Path.GetFileName(filePath)}»: {actionStr} в {formatStr}", LogLevel.Success);

            string outFolder = !string.IsNullOrWhiteSpace(settings.OutputFolder)
                ? settings.OutputFolder
                : (Path.GetDirectoryName(filePath) ?? string.Empty);

            var task = CreateWatchTask(filePath, pageType, formatStr, is3ds: false, outFolder);

            App.RunOnUI(() =>
            {
                if (pageType == "Verify")
                    App.TasksVM.VerifyTasks.Add(task);
                else
                    App.TasksVM.Tasks.Add(task);
            });

            _ = Task.Run(() => ExecuteWatchTaskAsync(task, CancellationToken.None));
        }

        #endregion

        #region 3DS Watcher

        public void Start3ds()
        {
            Stop3ds();

            var settings = App.Settings.Current;
            if (string.IsNullOrWhiteSpace(settings.WatchFolder3ds) || !Directory.Exists(settings.WatchFolder3ds))
            {
                App.Logger?.Log("[WatchFolder 3DS] Папка для отслеживания не указана или не существует.", Models.LogLevel.Warning);
                return;
            }

            try
            {
                _cts3ds = new CancellationTokenSource();
                _watcher3ds = new FileSystemWatcher(settings.WatchFolder3ds)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    Filter = "*.*",
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };

                _watcher3ds.Created += On3dsFileDetected;
                _watcher3ds.Changed += On3dsFileDetected;
                _watcher3ds.Renamed += On3dsFileDetected;

                _is3dsStarted = true;
                _ = MonitorPending3dsFilesLoopAsync(_cts3ds.Token);

                App.Logger?.Log($"[WatchFolder 3DS] Отслеживание папки запущено: {settings.WatchFolder3ds}", Models.LogLevel.Success);
                StateChanged?.Invoke();
            }
            catch (Exception ex)
            {
                App.Logger?.Log($"[WatchFolder 3DS] Ошибка запуска: {ex.Message}", Models.LogLevel.Warning);
                _is3dsStarted = false;
                StateChanged?.Invoke();
            }
        }

        public void Stop3ds()
        {
            if (_cts3ds != null)
            {
                _cts3ds.Cancel();
                _cts3ds.Dispose();
                _cts3ds = null;
            }

            if (_watcher3ds != null)
            {
                _watcher3ds.EnableRaisingEvents = false;
                _watcher3ds.Created -= On3dsFileDetected;
                _watcher3ds.Changed -= On3dsFileDetected;
                _watcher3ds.Renamed -= On3dsFileDetected;
                _watcher3ds.Dispose();
                _watcher3ds = null;
            }
            _pendingFiles3ds.Clear();
            _is3dsStarted = false;
            App.Logger?.Log("[WatchFolder 3DS] Отслеживание остановлено.", Models.LogLevel.Info);
            StateChanged?.Invoke();
        }

        private void On3dsFileDetected(object sender, FileSystemEventArgs e)
        {
            string ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
            if (ext != ".3ds" && ext != ".cci" && ext != ".cia" && ext != ".cxi" && ext != ".cfa" && ext != ".3dsx"
                && ext != ".zcci" && ext != ".zcxi" && ext != ".zcia" && ext != ".z3dsx" && ext != ".z3ds"
                && ext != ".zip" && ext != ".rar" && ext != ".7z")
                return;

            if (!File.Exists(e.FullPath))
                return;

            if (ShouldIgnoreWatchedFile(e.FullPath, App.Settings.Current.WatchFolder3ds, App.Settings.Current.OutputFolder3ds))
                return;

            _pendingFiles3ds.AddOrUpdate(
                e.FullPath,
                path => (GetFileSizeSafe(path), DateTime.UtcNow, 0),
                (path, current) =>
                {
                    long newSize = GetFileSizeSafe(path);
                    if (newSize != current.LastSize)
                    {
                        return (newSize, DateTime.UtcNow, 0);
                    }
                    return current;
                });
        }

        private async Task MonitorPending3dsFilesLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(2000, ct);

                    foreach (var kvp in _pendingFiles3ds.ToArray())
                    {
                        string filePath = kvp.Key;
                        var (lastSize, lastChange, stableCount) = kvp.Value;

                        if (!File.Exists(filePath))
                        {
                            _pendingFiles3ds.TryRemove(filePath, out _);
                            continue;
                        }

                        if (IsFileLockedByActiveTask(filePath))
                        {
                            continue;
                        }

                        long currentSize = GetFileSizeSafe(filePath);
                        if (currentSize <= 0) continue;

                        if (currentSize == lastSize)
                        {
                            int newCount = stableCount + 1;
                            _pendingFiles3ds[filePath] = (currentSize, lastChange, newCount);

                            if (newCount >= 3 && CanOpenExclusively(filePath))
                            {
                                _pendingFiles3ds.TryRemove(filePath, out _);
                                if (!ShouldIgnoreWatchedFile(filePath, App.Settings.Current.WatchFolder3ds, App.Settings.Current.OutputFolder3ds))
                                {
                                    MarkRecentlyHandled(filePath);
                                    ProcessReady3dsFile(filePath);
                                }
                            }
                        }
                        else
                        {
                            _pendingFiles3ds[filePath] = (currentSize, DateTime.UtcNow, 0);
                        }
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    App.Logger?.Log($"[WatchFolder 3DS] Ошибка очереди: {ex.Message}", Models.LogLevel.Warning);
                }
            }
        }

        private void ProcessReady3dsFile(string filePath)
        {
            var settings = App.Settings.Current;
            string[] actions = new string[] { "Конвертация", "Распаковка", "Упаковка", "Мульти-контент", "Проверка" };
            string[] pageTypes = new string[] { "Convert", "Unpack", "Pack", "Multi", "Verify" };
            string[] formats = new string[] { "3DS", "CIA", "CXI", "ZCCI", "ZCIA", "ZCXI", "Z3DSX", "Z3DS (Авто)" };

            int actIdx = Math.Clamp(settings.WatchFolderAction3ds, 0, actions.Length - 1);
            int fmtIdx = Math.Clamp(settings.WatchFolderFormat3ds, 0, formats.Length - 1);
            string actionStr = actions[actIdx];
            string pageType = pageTypes[actIdx];
            string formatStr = formats[fmtIdx];

            App.Logger?.Log($"[WatchFolder 3DS] Авто-обработка «{Path.GetFileName(filePath)}»: {actionStr} в {formatStr}", LogLevel.Success);

            string outFolder = !string.IsNullOrWhiteSpace(settings.OutputFolder3ds)
                ? settings.OutputFolder3ds
                : (!string.IsNullOrWhiteSpace(settings.OutputFolder) ? settings.OutputFolder : (Path.GetDirectoryName(filePath) ?? string.Empty));

            var task = CreateWatchTask(filePath, pageType, formatStr, is3ds: true, outFolder);

            App.RunOnUI(() =>
            {
                if (pageType == "Verify")
                    App.TasksVM.VerifyTasks.Add(task);
                else
                    App.TasksVM.Tasks.Add(task);
            });

            _ = Task.Run(() => ExecuteWatchTaskAsync(task, CancellationToken.None));
        }

        #endregion

        #region Isolated Task Execution & Loop Guard

        private static ProcessingTask CreateWatchTask(string filePath, string operation, string targetFormat, bool is3ds, string outputFolder)
        {
            long sizeBytes = GetFileSizeSafe(filePath);
            string fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);
            string sanitizedName = ProcessingTask.SanitizeFileName(fileNameNoExt);
            string srcExt = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant();

            return new ProcessingTask
            {
                Id = "W" + Guid.NewGuid().ToString("N").Substring(0, 6).ToUpperInvariant(),
                Operation = operation,
                Is3dsTask = is3ds,
                SourceFormat = srcExt,
                TargetFormat = targetFormat,
                SourceSizeBytes = Math.Max(0, sizeBytes),
                SourceSize = sizeBytes > 0 ? ProcessingTask.FormatSize(sizeBytes) : "-",
                TargetSize = "-",
                SizeDifference = "-",
                CompressionLevel = ProcessingTask.ComputeDefaultCompressionLevel(targetFormat, false),
                FilesCount = "1",
                InputFiles = new List<string> { filePath },
                FilesList = new List<string> { Path.GetFileName(filePath) },
                InputFolders = Path.GetDirectoryName(filePath) ?? string.Empty,
                OutputFolder = outputFolder,
                OutputFileName = sanitizedName,
                GameName = fileNameNoExt,
                Status = "Подготовка...",
                IsRunning = true,
                LogDetails = $"[WatchFolder] Автоматическая задача: {operation} ({targetFormat}) для {Path.GetFileName(filePath)}"
            };
        }

        private async Task ExecuteWatchTaskAsync(ProcessingTask task, CancellationToken outerCt)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(outerCt);
            task.Cts = cts;
            task.HasActiveWorker = true;
            string inputPath = task.InputFiles.FirstOrDefault() ?? string.Empty;

            try
            {
                if (string.IsNullOrEmpty(inputPath) || !File.Exists(inputPath))
                    throw new FileNotFoundException("Входной файл не найден", inputPath);

                if (!string.IsNullOrWhiteSpace(task.OutputFolder))
                {
                    Directory.CreateDirectory(task.OutputFolder);
                    string expectedOut = Path.Combine(task.OutputFolder, task.OutputFileName + task.GetPrimaryExtension());
                    MarkRecentlyHandled(expectedOut);
                }

                if (task.Is3dsTask)
                {
                    if (task.Operation == "Verify")
                    {
                        var meta = await App.Nintendo3ds.Parse3dsFileAsync(inputPath).ConfigureAwait(false);
                        App.RunOnUI(() =>
                        {
                            task.VerifyType = meta.ContentType;
                            task.VerifyStructure = meta.FileFormat;
                            task.VerifyTitleId = meta.TitleId;
                            task.VerifyVersion = meta.Version;
                            task.Status = "Корректна";
                            task.Progress = 100;
                            task.IsRunning = false;
                        });
                        return;
                    }
                    if (task.Operation == "Unpack")
                    {
                        string finalDir = Path.Combine(task.OutputFolder, $"{task.OutputFileName}_Unpacked");
                        Directory.CreateDirectory(finalDir);
                        await App.Nintendo3ds.Unpack3dsContainerAsync(task, inputPath, finalDir, cts.Token).ConfigureAwait(false);
                        task.EnsureFinalMetricsPopulated(finalDir);
                        return;
                    }
                    if (task.Operation == "Multi")
                    {
                        string outPath = Path.Combine(task.OutputFolder, task.OutputFileName + task.GetPrimaryExtension());
                        MarkRecentlyHandled(outPath);
                        await App.Nintendo3ds.Build3dsMultiContentAsync(task, task.InputFiles, outPath, task.TargetFormat, cts.Token).ConfigureAwait(false);
                        MarkRecentlyHandled(outPath);
                        return;
                    }
                    await App.Nintendo3ds.Convert3dsContainerAsync(task, inputPath, task.OutputFolder, task.TargetFormat, cts.Token).ConfigureAwait(false);
                    return;
                }

                if (task.Operation == "Verify")
                {
                    await App.SwitchFormat.VerifyNspAsync(task, inputPath, cts.Token).ConfigureAwait(false);
                    App.RunOnUI(() =>
                    {
                        task.Status = "Успешно";
                        task.Progress = 100;
                        task.IsRunning = false;
                    });
                    return;
                }

                if (task.Operation == "Unpack")
                {
                    string finalDir = Path.Combine(task.OutputFolder, $"{task.OutputFileName}_Unpacked");
                    Directory.CreateDirectory(finalDir);
                    await App.SwitchFormat.UnpackContainerAsync(task, inputPath, finalDir, cts.Token).ConfigureAwait(false);
                    task.EnsureFinalMetricsPopulated(finalDir);
                    App.RunOnUI(() =>
                    {
                        task.Status = "Готово";
                        task.Progress = 100;
                        task.IsRunning = false;
                    });
                    return;
                }

                if (task.Operation == "Multi")
                {
                    string outPath = Path.Combine(task.OutputFolder, task.OutputFileName + task.GetPrimaryExtension());
                    MarkRecentlyHandled(outPath);
                    await App.MultiContent.BuildMultiContentAsync(task, task.InputFiles, outPath, App.Settings.Current.ForceMultiRebuild, cts.Token).ConfigureAwait(false);
                    MarkRecentlyHandled(outPath);
                    return;
                }

                // Convert / Compress
                string workingInput = inputPath;
                if (task.TargetFormat.Equals("XCI", StringComparison.OrdinalIgnoreCase) ||
                    task.TargetFormat.Equals("XCZ", StringComparison.OrdinalIgnoreCase))
                {
                    await App.SwitchFormat.ConvertContainerAsync(task, workingInput, task.OutputFolder, "XCI", cts.Token).ConfigureAwait(false);
                    string xciPath = Path.ChangeExtension(Path.Combine(task.OutputFolder, Path.GetFileName(workingInput)), ".xci");
                    MarkRecentlyHandled(xciPath);
                    if (task.TargetFormat.Equals("XCZ", StringComparison.OrdinalIgnoreCase) && File.Exists(xciPath))
                    {
                        await App.NszCompression.CompressToNszAsync(task, xciPath, task.OutputFolder, cts.Token).ConfigureAwait(false);
                        string genNsz = Path.ChangeExtension(xciPath, ".nsz");
                        string finalXcz = Path.ChangeExtension(xciPath, ".xcz");
                        if (File.Exists(genNsz))
                        {
                            finalXcz = await SafeFileOperations.SafeMoveOrReplaceFileAsync(genNsz, finalXcz, task, cts.Token).ConfigureAwait(false);
                            MarkRecentlyHandled(finalXcz);
                        }
                    }
                }
                else if (task.TargetFormat.Equals("NSZ", StringComparison.OrdinalIgnoreCase))
                {
                    string targetNsp = workingInput;
                    if (!workingInput.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase))
                    {
                        await App.SwitchFormat.ConvertContainerAsync(task, workingInput, task.OutputFolder, "NSP", cts.Token).ConfigureAwait(false);
                        targetNsp = Path.ChangeExtension(Path.Combine(task.OutputFolder, Path.GetFileName(workingInput)), ".nsp");
                        MarkRecentlyHandled(targetNsp);
                    }
                    await App.NszCompression.CompressToNszAsync(task, targetNsp, task.OutputFolder, cts.Token).ConfigureAwait(false);
                    MarkRecentlyHandled(Path.ChangeExtension(targetNsp, ".nsz"));
                }
                else
                {
                    await App.SwitchFormat.ConvertContainerAsync(task, workingInput, task.OutputFolder, "NSP", cts.Token).ConfigureAwait(false);
                    MarkRecentlyHandled(Path.ChangeExtension(Path.Combine(task.OutputFolder, Path.GetFileName(workingInput)), ".nsp"));
                }

                App.RunOnUI(() =>
                {
                    task.Status = "Успешно";
                    task.Progress = 100;
                    task.IsRunning = false;
                });
            }
            catch (Exception ex)
            {
                App.RunOnUI(() =>
                {
                    task.IsRunning = false;
                    task.Status = "Ошибка";
                    task.LogDetails += $"\n❌ [WatchFolder] {ex.Message}";
                });
            }
            finally
            {
                task.HasActiveWorker = false;
                if (!string.IsNullOrEmpty(inputPath))
                    MarkRecentlyHandled(inputPath);
                if (!string.IsNullOrWhiteSpace(task.OutputFolder) && !string.IsNullOrWhiteSpace(task.OutputFileName))
                    MarkRecentlyHandled(Path.Combine(task.OutputFolder, task.OutputFileName + task.GetPrimaryExtension()));
            }
        }

        private void MarkRecentlyHandled(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) return;
            try
            {
                string full = Path.GetFullPath(filePath);
                _recentlyProcessedOrOutputFiles[full] = DateTime.UtcNow;

                // Чистим устаревшие записи (> 5 минут)
                DateTime cutoff = DateTime.UtcNow.AddMinutes(-5);
                foreach (var kv in _recentlyProcessedOrOutputFiles.ToArray())
                {
                    if (kv.Value < cutoff)
                        _recentlyProcessedOrOutputFiles.TryRemove(kv.Key, out _);
                }
            }
            catch { }
        }

        private bool ShouldIgnoreWatchedFile(string filePath, string? watchFolder, string? outputFolder)
        {
            try
            {
                string fullFile = Path.GetFullPath(filePath);
                string fileName = Path.GetFileName(fullFile);
                if (fileName.Contains(".storm_tmp", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                    return true;

                // 1. Исключаем файлы, недавно обработанные или созданные как результат (защита от цикла перепаковки)
                if (_recentlyProcessedOrOutputFiles.TryGetValue(fullFile, out var handledAt))
                {
                    if ((DateTime.UtcNow - handledAt).TotalMinutes < 3)
                        return true;
                }

                // 2. Исключаем пути внутри OutputFolder (если OutputFolder задан и находится внутри/не совпадает с WatchFolder)
                string[] configuredOutDirs = { outputFolder ?? "", App.Settings.Current.OutputFolder ?? "", App.Settings.Current.OutputFolder3ds ?? "" };
                string? normWatch = !string.IsNullOrWhiteSpace(watchFolder)
                    ? Path.GetFullPath(watchFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    : null;

                foreach (var outDirRaw in configuredOutDirs)
                {
                    if (string.IsNullOrWhiteSpace(outDirRaw)) continue;
                    string normOut = Path.GetFullPath(outDirRaw).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (normWatch != null && string.Equals(normOut, normWatch, StringComparison.OrdinalIgnoreCase))
                        continue;

                    string outPrefix = normOut + Path.DirectorySeparatorChar;
                    if (fullFile.StartsWith(outPrefix, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                // 3. Не перехватываем выходные файлы уже завершённых или активных задач
                if (App.TasksVM?.Tasks != null)
                {
                    foreach (var t in App.TasksVM.Tasks.ToArray())
                    {
                        if (string.IsNullOrWhiteSpace(t.OutputFolder) || string.IsNullOrWhiteSpace(t.OutputFileName))
                            continue;
                        string tOutDir = Path.GetFullPath(t.OutputFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        string? fileDir = Path.GetDirectoryName(fullFile)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                        if (string.Equals(tOutDir, fileDir, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(Path.GetFileNameWithoutExtension(fullFile), t.OutputFileName, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        #endregion

        #region Helpers

        private static long GetFileSizeSafe(string path)
        {
            try
            {
                var fi = new FileInfo(path);
                return fi.Exists ? fi.Length : -1;
            }
            catch { return -1; }
        }

        private static bool CanOpenExclusively(string filePath)
        {
            try
            {
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    return stream.Length > 0;
                }
            }
            catch { return false; }
        }

        private static bool IsFileLockedByActiveTask(string filePath)
        {
            try
            {
                if (App.TasksVM?.Tasks == null) return false;
                var runningTasks = App.TasksVM.Tasks.Where(t => t.IsRunning).ToList();
                if (runningTasks.Count == 0) return false;

                string normalizedTarget = Path.GetFullPath(filePath);

                foreach (var task in runningTasks)
                {

                    if (!string.IsNullOrEmpty(task.OutputFolder))
                    {
                        string targetDir = Path.GetFullPath(task.OutputFolder);
                        string fileDir = Path.GetDirectoryName(normalizedTarget) ?? "";

                        if (string.Equals(targetDir, fileDir, StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.IsNullOrEmpty(task.OutputFileName))
                            {
                                string fileNameNoExt = Path.GetFileNameWithoutExtension(normalizedTarget);
                                if (string.Equals(task.OutputFileName, fileNameNoExt, StringComparison.OrdinalIgnoreCase))
                                {
                                    return true;
                                }
                            }
                        }
                    }

                    if (task.InputFiles != null)
                    {
                        foreach (var input in task.InputFiles)
                        {
                            if (!string.IsNullOrEmpty(input) &&
                                string.Equals(Path.GetFullPath(input), normalizedTarget, StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            catch { }
            return false;
        }

        #endregion
    }
}
