using System;
using System.IO;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

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

                        long currentSize = GetFileSizeSafe(filePath);
                        if (currentSize <= 0) continue;

                        if (currentSize == lastSize)
                        {
                            int newCount = stableCount + 1;
                            _pendingFilesSwitch[filePath] = (currentSize, lastChange, newCount);

                            if (newCount >= 3 && CanOpenExclusively(filePath))
                            {
                                _pendingFilesSwitch.TryRemove(filePath, out _);
                                ProcessReadySwitchFile(filePath);
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
            App.RunOnUI(async () =>
            {
                var settings = App.Settings.Current;
                string[] actions = new string[] { "Сжатие", "Распаковка", "Упаковка", "Конвертация", "Мульти-контент", "Проверка" };
                string[] formats = new string[] { "NSP", "NSZ", "XCI", "XCZ" };
                
                string actionStr = settings.WatchFolderActionSwitch >= 0 && settings.WatchFolderActionSwitch < actions.Length ? actions[settings.WatchFolderActionSwitch] : "Мульти-контент";
                string formatStr = settings.WatchFolderFormatSwitch >= 0 && settings.WatchFolderFormatSwitch < formats.Length ? formats[settings.WatchFolderFormatSwitch] : "NSP";

                App.Logger?.Log($"[WatchFolder Switch] Авто-обработка «{Path.GetFileName(filePath)}»: {actionStr} в {formatStr}", Models.LogLevel.Success);
                
                App.TasksVM.SelectedPlatform = 0;
                await App.TasksVM.AddDroppedFilesBatchAsync(new System.Collections.Generic.List<string> { filePath });
                await App.TasksVM.StartAllTasksAsync();
            });
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
            if (ext != ".3ds" && ext != ".cci" && ext != ".cia" && ext != ".cxi" && ext != ".cfa" && ext != ".zip" && ext != ".rar" && ext != ".7z")
                return;

            if (!File.Exists(e.FullPath))
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

                        long currentSize = GetFileSizeSafe(filePath);
                        if (currentSize <= 0) continue;

                        if (currentSize == lastSize)
                        {
                            int newCount = stableCount + 1;
                            _pendingFiles3ds[filePath] = (currentSize, lastChange, newCount);

                            if (newCount >= 3 && CanOpenExclusively(filePath))
                            {
                                _pendingFiles3ds.TryRemove(filePath, out _);
                                ProcessReady3dsFile(filePath);
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
            App.RunOnUI(async () =>
            {
                var settings = App.Settings.Current;
                string[] actions = new string[] { "Конвертация", "Распаковка", "Упаковка", "Мульти-контент", "Проверка" };
                string[] formats = new string[] { "3DS", "CIA", "CXI" };
                
                string actionStr = settings.WatchFolderAction3ds >= 0 && settings.WatchFolderAction3ds < actions.Length ? actions[settings.WatchFolderAction3ds] : "Мульти-контент";
                string formatStr = settings.WatchFolderFormat3ds >= 0 && settings.WatchFolderFormat3ds < formats.Length ? formats[settings.WatchFolderFormat3ds] : "3DS";

                App.Logger?.Log($"[WatchFolder 3DS] Авто-обработка «{Path.GetFileName(filePath)}»: {actionStr} в {formatStr}", Models.LogLevel.Success);
                
                App.TasksVM.SelectedPlatform = 1;
                await App.TasksVM.AddDroppedFilesBatchAsync(new System.Collections.Generic.List<string> { filePath });
                await App.TasksVM.StartAllTasksAsync();
            });
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

        #endregion
    }
}
