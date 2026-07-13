using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using StormSwitchBox.Models;
using Windows.Storage.Streams;

namespace StormSwitchBox.Services;

public static class HistoryService
{
	private static readonly string HistoryFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "history.json");

	public static ObservableCollection<ProcessingTask> HistoryTasks { get; set; } = new ObservableCollection<ProcessingTask>();

	public static async Task LoadHistoryAsync()
	{
		try
		{
			if (!File.Exists(HistoryFilePath))
			{
				return;
			}
			ObservableCollection<ProcessingTask> items = JsonSerializer.Deserialize<ObservableCollection<ProcessingTask>>(await File.ReadAllTextAsync(HistoryFilePath));
			if (items == null)
			{
				return;
			}
			App.MainDispatcher?.TryEnqueue(delegate
			{
				HistoryTasks.Clear();
				foreach (ProcessingTask item in items)
				{
					HistoryTasks.Add(item);
					TryLoadIconForTask(item);
				}
			});
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			App.Logger.Log("Ошибка загрузки истории: " + ex2.Message, LogLevel.Error);
		}
	}

	private static void TryLoadIconForTask(ProcessingTask task)
	{
		if (string.IsNullOrEmpty(task.GroupId))
		{
			return;
		}
		string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons");
		string iconPath = Path.Combine(path, task.GroupId + ".png");
		if (!File.Exists(iconPath))
		{
			return;
		}
		Task.Run(async delegate
		{
			try
			{
				byte[] bytes = await File.ReadAllBytesAsync(iconPath);
				App.MainDispatcher?.TryEnqueue(async delegate
				{
					try
					{
						InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream();
						using (DataWriter writer = new DataWriter(stream.GetOutputStreamAt(0uL)))
						{
							writer.WriteBytes(bytes);
							await writer.StoreAsync();
						}
						BitmapImage bitmap = new BitmapImage();
						await bitmap.SetSourceAsync(stream);
						task.GameIcon = bitmap;
					}
					catch
					{
					}
				});
			}
			catch
			{
			}
		});
	}

	public static async Task SaveHistoryAsync()
	{
		try
		{
			await File.WriteAllTextAsync(contents: JsonSerializer.Serialize(HistoryTasks, new JsonSerializerOptions
			{
				WriteIndented = true
			}), path: HistoryFilePath);
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			App.Logger.Log("Ошибка сохранения истории: " + ex2.Message, LogLevel.Error);
		}
	}

	public static void AddToHistory(ProcessingTask task)
	{
		App.MainDispatcher?.TryEnqueue(async delegate
		{
			ProcessingTask copy = new ProcessingTask
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
				InputFiles = new List<string>(task.InputFiles),
				FilesList = new List<string>(task.FilesList),
				HasRomFs = task.HasRomFs,
				HasExeFs = task.HasExeFs,
				InputFolders = task.InputFolders,
				OutputFolder = task.OutputFolder,
				OutputFileName = task.OutputFileName,
				LogDetails = task.LogDetails,
				Progress = 100.0,
				GameIcon = task.GameIcon
			};
			HistoryTasks.Insert(0, copy);
			if (HistoryTasks.Count > 100)
			{
				HistoryTasks.RemoveAt(HistoryTasks.Count - 1);
			}
			await SaveHistoryAsync();
		});
	}
}
