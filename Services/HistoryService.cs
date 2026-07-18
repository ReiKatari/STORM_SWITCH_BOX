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
        private static readonly string HistoryFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "history.json");
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

        private static void TryLoadIconForTask(ProcessingTask task)
        {
            if (string.IsNullOrEmpty(task.GroupId)) return;
            
            string iconsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons");
            string iconPath = Path.Combine(iconsDir, $"{task.GroupId}.png");
            
            if (File.Exists(iconPath))
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
                                var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                                using (var writer = new Windows.Storage.Streams.DataWriter(stream.GetOutputStreamAt(0)))
                                {
                                    writer.WriteBytes(bytes);
                                    await writer.StoreAsync();
                                }
                                var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                                await bitmap.SetSourceAsync(stream);
                                task.GameIcon = bitmap;
                            }
                            catch { }
                        });
                    }
                    catch { }
                });
            }
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
                
                await SaveHistoryAsync();
                
                if (copy.Status == "Успешно" || copy.Status == "Готово")
                {
                    App.NotifyTaskCompleted(copy);
                }
            });
        }
    }
}
