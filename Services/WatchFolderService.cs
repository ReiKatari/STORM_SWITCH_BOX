using System;
using System.IO;
using System.Threading.Tasks;

namespace StormSwitchBox.Services
{
    public class WatchFolderService
    {
        private FileSystemWatcher? _watcher;
        private bool _isStarted = false;

        public void Start()
        {
            Stop();

            var settings = App.Settings.Current;
            if (!settings.EnableWatchFolder || string.IsNullOrWhiteSpace(settings.WatchFolder) || !Directory.Exists(settings.WatchFolder))
                return;

            try
            {
                _watcher = new FileSystemWatcher(settings.WatchFolder)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                    Filter = "*.*",
                    EnableRaisingEvents = true
                };

                _watcher.Created += OnFileCreated;
                _watcher.Renamed += OnFileCreated;

                _isStarted = true;
                App.Logger.Log($"[WatchFolder] Служба наблюдения за пакой запущена: {settings.WatchFolder}", Models.LogLevel.Success);
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[WatchFolder] Ошибка запуска наблюдения: {ex.Message}", Models.LogLevel.Warning);
            }
        }

        public void Stop()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Created -= OnFileCreated;
                _watcher.Renamed -= OnFileCreated;
                _watcher.Dispose();
                _watcher = null;
            }
            _isStarted = false;
        }

        private async void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            string ext = Path.GetExtension(e.FullPath).ToLowerInvariant();
            if (ext != ".nsp" && ext != ".nsz" && ext != ".xci" && ext != ".xcz")
                return;

            // Задержка ожидания освобождения файла от копирования/загрузки
            bool ready = await WaitForFileReadyAsync(e.FullPath);
            if (!ready) return;

            App.RunOnUI(async () =>
            {
                var settings = App.Settings.Current;
                string[] actions = new string[] { "Сжатие", "Распаковка", "Упаковка", "Конвертация", "Мульти-контент", "Проверка" };
                string[] formats = new string[] { "NSP", "NSZ", "XCI", "XCZ" };
                
                string actionStr = settings.WatchFolderAction >= 0 && settings.WatchFolderAction < actions.Length ? actions[settings.WatchFolderAction] : "Обработка";
                string formatStr = settings.WatchFolderFormat >= 0 && settings.WatchFolderFormat < formats.Length ? formats[settings.WatchFolderFormat] : "NSP";

                App.Logger.Log($"[WatchFolder] Авто-обработка «{Path.GetFileName(e.FullPath)}»: {actionStr} в {formatStr}", Models.LogLevel.Success);
                
                // Добавляем файл в очередь задач и автозапуск
                await App.TasksVM.AddDroppedFilesBatchAsync(new System.Collections.Generic.List<string> { e.FullPath });
                await App.TasksVM.StartAllTasksAsync();
            });
        }

        private static async Task<bool> WaitForFileReadyAsync(string filePath, int timeoutSeconds = 60)
        {
            var startTime = DateTime.Now;
            while ((DateTime.Now - startTime).TotalSeconds < timeoutSeconds)
            {
                try
                {
                    using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None))
                    {
                        if (stream.Length > 0) return true;
                    }
                }
                catch (IOException)
                {
                    // Файл занят другим процессом (загрузка/копирование)
                }
                await Task.Delay(1500);
            }
            return false;
        }
    }
}
