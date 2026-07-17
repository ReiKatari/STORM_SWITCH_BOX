using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using StormSwitchBox.Models;
using StormSwitchBox.Services;
using WinRT;
using WinRT.StormSwitchBoxVtableClasses;
using Windows.Storage.Streams;

namespace StormSwitchBox.ViewModels;

public partial class TasksViewModel : ObservableObject
{
	private string _selectedFormat = "NSP";

	private int _selectedFormatIndex = 0;

	private static readonly string[] FormatNames = new string[4] { "NSP", "NSZ", "XCI", "XCZ" };

	private static readonly HashSet<string> GameExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".NSP", ".XCI", ".NSZ", ".XCZ" };

	private string _currentPageType = "Update";

	private bool _isProcessingQueue = false;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.2.0.0")]
	private RelayCommand<ProcessingTask>? deleteTaskCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.2.0.0")]
	private RelayCommand? addTestTaskCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.2.0.0")]
	private RelayCommand? clearTasksCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.2.0.0")]
	private RelayCommand? stopAllTasksCommand;

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.2.0.0")]
	private RelayCommand? startAllTasksCommand;

	public ObservableCollection<ProcessingTask> Tasks { get; } = new ObservableCollection<ProcessingTask>();

	public ObservableCollection<ProcessingTask> VerifyTasks { get; } = new ObservableCollection<ProcessingTask>();

	public bool IsAnyTaskRunning => Tasks.Any((ProcessingTask t) => t.IsRunning) || VerifyTasks.Any((ProcessingTask t) => t.IsRunning);

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string SelectedFormat
	{
		get => _selectedFormat;
		set => SetProperty(ref _selectedFormat, value);
	}

	public int SelectedFormatIndex
	{
		get => _selectedFormatIndex;
		set => SetProperty(ref _selectedFormatIndex, value);
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand<ProcessingTask> DeleteTaskCommand => deleteTaskCommand ?? (deleteTaskCommand = new RelayCommand<ProcessingTask>(DeleteTask));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand AddTestTaskCommand => addTestTaskCommand ?? (addTestTaskCommand = new RelayCommand(AddTestTask));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand ClearTasksCommand => clearTasksCommand ?? (clearTasksCommand = new RelayCommand(ClearTasks));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand StopAllTasksCommand => stopAllTasksCommand ?? (stopAllTasksCommand = new RelayCommand(StopAllTasks));

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.RelayCommandGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public IRelayCommand StartAllTasksCommand => startAllTasksCommand ?? (startAllTasksCommand = new RelayCommand(StartAllTasks));

	public TasksViewModel()
	{
		int selectedFormatIndex = App.Settings.Current.SelectedFormatIndex;
		if (selectedFormatIndex >= 0 && selectedFormatIndex < FormatNames.Length)
		{
			_selectedFormatIndex = selectedFormatIndex;
			_selectedFormat = FormatNames[selectedFormatIndex];
		}
		DispatcherTimer dispatcherTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(500.0)
		};
		dispatcherTimer.Tick += delegate
		{
			OnPropertyChanged("IsAnyTaskRunning");
		};
		dispatcherTimer.Start();
	}

	public void SetPageType(string pageType)
	{
		_currentPageType = pageType;
		App.Logger.Log("Переход в раздел: " + pageType, LogLevel.Debug);
	}

	private async Task<SwitchFormatInfo> GetInternalTitleInfoAsync(string filePath)
	{
		return await Task.Run(delegate
		{
			try
			{
				SwitchFormatInfo switchFormatInfo = App.SwitchFormat.ParseNsp(filePath);
				if (!string.IsNullOrEmpty(switchFormatInfo.TitleId))
				{
					switchFormatInfo.TitleId = switchFormatInfo.TitleId.ToUpper();
					return switchFormatInfo;
				}
			}
			catch
			{
			}
			Match match = Regex.Match(Path.GetFileName(filePath), "(?i)([0-9a-f]{16})");
			string? text = (match.Success ? match.Groups[1].Value.ToUpper() : null);
			string contentType = "Unknown";
			if (text != null && text.Length == 16)
			{
				contentType = (text.EndsWith("000") ? "Application" : ((!text.EndsWith("00")) ? "AddOnContent" : "Patch"));
			}
			return new SwitchFormatInfo
			{
				TitleId = (text ?? string.Empty),
				ContentType = contentType
			};
		});
	}

	private long CalculateSize(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				return new FileInfo(path).Length;
			}
			if (Directory.Exists(path))
			{
				long num = 0L;
				foreach (string item in Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories))
				{
					try
					{
						num += new FileInfo(item).Length;
					}
					catch
					{
					}
				}
				return num;
			}
		}
		catch
		{
		}
		return 0L;
	}

	private (bool hasRomFs, bool hasExeFs) CheckSpecialFolders(string path)
	{
		if (!Directory.Exists(path))
		{
			return (hasRomFs: false, hasExeFs: false);
		}
		bool item = Directory.Exists(Path.Combine(path, "romfs"));
		bool item2 = Directory.Exists(Path.Combine(path, "exefs"));
		return (hasRomFs: item, hasExeFs: item2);
	}

	private bool IsGameFile(string path)
	{
		if (Directory.Exists(path))
		{
			return true;
		}
		string extension = Path.GetExtension(path);
		if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase) || extension.Equals(".rar", StringComparison.OrdinalIgnoreCase) || extension.Equals(".7z", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}
		return GameExtensions.Contains(extension);
	}

	public async Task AddDroppedFilesBatchAsync(List<string> paths)
	{
		if (paths == null || paths.Count == 0) return;

		// 1. Separate mod directories (romfs/exefs) from other paths
		var modDirs = paths.Where(p => Directory.Exists(p) && 
			(Path.GetFileName(p).Equals("romfs", StringComparison.OrdinalIgnoreCase) || 
			 Path.GetFileName(p).Equals("exefs", StringComparison.OrdinalIgnoreCase))).ToList();

		var normalPaths = paths.Except(modDirs).ToList();

		// Also scan normal paths (if they are directories) to find nested romfs/exefs folders
		foreach (var normalPath in normalPaths)
		{
			if (Directory.Exists(normalPath))
			{
				try
				{
					var subDirs = Directory.GetDirectories(normalPath, "*", SearchOption.AllDirectories);
					foreach (var subDir in subDirs)
					{
						string name = Path.GetFileName(subDir);
						if (name.Equals("romfs", StringComparison.OrdinalIgnoreCase) || 
							name.Equals("exefs", StringComparison.OrdinalIgnoreCase))
						{
							if (!modDirs.Contains(subDir, StringComparer.OrdinalIgnoreCase))
							{
								modDirs.Add(subDir);
							}
						}
					}
				}
				catch (Exception ex)
				{
					App.Logger.Log($"Ошибка сканирования поддиректорий в {normalPath}: {ex.Message}", LogLevel.Error);
				}
			}
		}

		// Keep track of tasks created in this batch
		var initialTaskCount = Tasks.Count;

		// 2. Process normal files/folders
		if (normalPaths.Count > 0)
		{
			foreach (var path in normalPaths)
			{
				await AddDroppedFileAsync(path);
			}
		}

		// 3. Attach mod directories to game tasks
		if (modDirs.Count > 0)
		{
			// Find newly created tasks first
			var newlyCreatedTasks = Tasks.Skip(initialTaskCount).ToList();
			
			// If no new tasks were created, find the last pending task
			if (newlyCreatedTasks.Count == 0)
			{
				var lastPendingTask = Tasks.LastOrDefault(t => t.Status == "Ожидание");
				if (lastPendingTask != null)
				{
					newlyCreatedTasks.Add(lastPendingTask);
				}
			}

			if (newlyCreatedTasks.Count > 0)
			{
				foreach (var task in newlyCreatedTasks)
				{
					bool updated = false;
					foreach (var modDir in modDirs)
					{
						if (!task.InputFiles.Contains(modDir, StringComparer.OrdinalIgnoreCase))
						{
							task.InputFiles.Add(modDir);
							task.FilesList.Add(Path.GetFileName(modDir));
							updated = true;
						}
					}
					
					if (updated)
					{
						// Update task properties
						task.SourceSizeBytes = task.InputFiles.Sum(p => CalculateSize(p));
						task.FilesCount = task.InputFiles.Count(p => File.Exists(p)).ToString();
						
						bool hasRomFs = task.InputFiles.Any(p => Directory.Exists(p) && Path.GetFileName(p).Equals("romfs", StringComparison.OrdinalIgnoreCase));
						bool hasExeFs = task.InputFiles.Any(p => Directory.Exists(p) && Path.GetFileName(p).Equals("exefs", StringComparison.OrdinalIgnoreCase));
						
						task.HasRomFs = hasRomFs ? "1" : "-";
						task.HasExeFs = hasExeFs ? "1" : "-";
						
						App.Logger.Log($"Папки модов привязаны к задаче {task.Id} ({task.OutputFileName})", LogLevel.Success);
					}
				}
			}
			else
			{
				App.Logger.Log("Не найдено активной задачи для привязки папок модов (romfs/exefs). Пожалуйста, перетащите их вместе с игрой.", LogLevel.Warning);
			}
		}
	}


	public async Task AddDroppedFileAsync(string path)
	{
		await Task.Run(async delegate
		{
			if (_currentPageType == "Update" && !IsGameFile(path))
			{
				App.Logger.Log("Пропущен (не игровой формат): " + Path.GetFileName(path), LogLevel.Warning);
			}
			else
			{
				bool isDirectory = Directory.Exists(path);
				if (!isDirectory)
				{
					string ext = Path.GetExtension(path).ToLower();
					if (ext == ".zip" || ext == ".rar" || ext == ".7z")
					{
						string tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp", "archives", Guid.NewGuid().ToString("N").Substring(0, 8));
						Directory.CreateDirectory(tempDir);
						App.MainDispatcher?.TryEnqueue(delegate
						{
							App.Logger.Log("Предварительная распаковка архива " + Path.GetFileName(path) + "...");
						});
						bool extracted = false;
						if (ext == ".zip")
						{
							try
							{
								ZipFile.ExtractToDirectory(path, tempDir, overwriteFiles: true);
								extracted = true;
							}
							catch (Exception ex)
							{
								Exception ex2 = ex;
								App.Logger.Log("Ошибка распаковки ZIP: " + ex2.Message, LogLevel.Error);
							}
						}
						else
						{
							string sevenZipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "7z.exe");
							if (File.Exists(sevenZipPath))
							{
								try
								{
									Process proc = new Process();
									proc.StartInfo.FileName = sevenZipPath;
									proc.StartInfo.Arguments = $"x \"{path}\" -o\"{tempDir}\" -y";
									proc.StartInfo.UseShellExecute = false;
									proc.StartInfo.CreateNoWindow = true;
									proc.Start();
									await proc.WaitForExitAsync();
									if (proc.ExitCode == 0)
									{
										extracted = true;
									}
								}
								catch (Exception ex)
								{
									Exception ex3 = ex;
									App.Logger.Log("Ошибка распаковки через 7z.exe: " + ex3.Message, LogLevel.Error);
								}
							}
							else
							{
								App.Logger.Log("Для распаковки " + ext + " требуется наличие tools\\7z.exe", LogLevel.Warning);
							}
						}
						if (extracted)
						{
							App.Logger.Log("Архив " + Path.GetFileName(path) + " успешно распакован во временную папку. Добавление содержимого...", LogLevel.Success);
							await AddDroppedFileAsync(tempDir);
						}
						return;
					}
				}
				if (_currentPageType == "Verify")
				{
					List<string> verifyFiles = new List<string>();
					if (isDirectory)
					{
						List<string> allFiles = (await Task.Run(() => Directory.GetFiles(path, "*.*", SearchOption.AllDirectories))).Where((string f) => GameExtensions.Contains(Path.GetExtension(f).ToLower())).ToList();
						verifyFiles.AddRange(allFiles);
					}
					else
					{
						verifyFiles.Add(path);
					}
					foreach (string file in verifyFiles)
					{
						SwitchFormatInfo verifyMeta = await GetInternalTitleInfoAsync(file);
						await AddOrUpdateTask(new List<string> { file }, file, Path.GetDirectoryName(file) ?? file, verifyMeta.IconBytes);
					}
				}
				else
				{
					if (isDirectory)
					{
						List<string> gameFiles = (await Task.Run(() => Directory.GetFiles(path, "*.*", SearchOption.AllDirectories))).Where((string f) => GameExtensions.Contains(Path.GetExtension(f).ToLower())).ToList();
						if (gameFiles.Count > 0)
						{
							List<(string Path, string? TitleId, string Type, string TopFolder, byte[]? IconBytes)> filesMeta = new();
							var metaTasks = gameFiles.Select(async delegate(string f)
							{
								string relPath = Path.GetRelativePath(path, f);
								string topFolder = relPath.Split(Path.DirectorySeparatorChar)[0];
								if (relPath == topFolder)
								{
									topFolder = "ROOT";
								}
								string? tid = null;
								string ctype = "";
								byte[]? iconBytes = null;
								if (!Directory.Exists(f))
								{
									SwitchFormatInfo meta = await GetInternalTitleInfoAsync(f);
									tid = meta.TitleId;
									ctype = meta.ContentType;
									iconBytes = meta.IconBytes;
								}
								return (Path: f, TitleId: tid, Type: ctype, TopFolder: topFolder, IconBytes: iconBytes);
							}).ToList();
							filesMeta.AddRange(await Task.WhenAll(metaTasks));
							var topFolderTids = (from m in filesMeta
								where m.TitleId != null && m.TitleId.Length == 16
								group m by m.TopFolder).ToDictionary(g => g.Key, g => (from m in g
								group m by m.TitleId!.Substring(0, 12) into x
								orderby x.Count() descending
								select x).First().Key);
							var groups = filesMeta.GroupBy(m =>
							{
								string text = "UNKNOWN";
								if (m.TitleId != null && m.TitleId.Length == 16)
								{
									text = m.TitleId.Substring(0, 12);
								}
								else if (topFolderTids.TryGetValue(m.TopFolder, out string? value) && value != null)
								{
									text = value;
								}
								return text + "_" + m.TopFolder;
							});
							foreach (var group in groups)
							{
								var filesInGroup = group.ToList();
								if (_currentPageType == "Update")
								{
									filesInGroup = filesInGroup.Where(m =>
									{
										if (Directory.Exists(m.Path))
										{
											return false;
										}
										if (m.TitleId == null || m.TitleId.Length != 16)
										{
											return false;
										}
										bool flag = m.Type == "Application" || m.TitleId.EndsWith("000");
										bool flag2 = m.Type == "Patch" || (m.TitleId.EndsWith("00") && m.TitleId[13] != '0');
										if (!flag && !flag2)
										{
											App.Logger.Log("Пропущено дополнение: " + Path.GetFileName(m.Path), LogLevel.Warning);
											return false;
										}
										return true;
									}).ToList();
								}
								if (filesInGroup.Count > 0)
								{
									string taskBasePath = ((group.First().Item4 == "ROOT") ? path : Path.Combine(path, group.First().Item4));
									await AddOrUpdateTask(filesInGroup.Select(m => m.Path).ToList(), group.Key, taskBasePath, filesInGroup.FirstOrDefault(m => m.IconBytes != null).IconBytes);
								}
							}
							return;
						}
					}
					SwitchFormatInfo fileMeta = await GetInternalTitleInfoAsync(path);
					string baseTid = ((fileMeta.TitleId != null && fileMeta.TitleId.Length == 16) ? fileMeta.TitleId.Substring(0, 12) : path);
					if (_currentPageType == "Update")
					{
						if (fileMeta.TitleId == null || fileMeta.TitleId.Length != 16)
						{
							App.Logger.Log("Пропущен (отсутствует Title ID, не удалось извлечь): " + Path.GetFileName(path), LogLevel.Warning);
							return;
						}
						bool isBase = fileMeta.ContentType == "Application" || fileMeta.TitleId.EndsWith("000");
						bool isUpdate = fileMeta.ContentType == "Patch" || (fileMeta.TitleId.EndsWith("00") && fileMeta.TitleId[13] != '0');
						if (!isBase && !isUpdate)
						{
							App.Logger.Log("Пропущено дополнение: " + Path.GetFileName(path), LogLevel.Warning);
							return;
						}
					}
					await AddOrUpdateTask(new List<string> { path }, baseTid, (isDirectory ? path : Path.GetDirectoryName(path)) ?? path, fileMeta.IconBytes);
				}
			}
		});
	}

	private Task AddOrUpdateTask(List<string> files, string groupId, string basePath, byte[]? iconBytes = null)
	{
		if (files == null || files.Count == 0)
		{
			return Task.CompletedTask;
		}
		var tcs = new TaskCompletionSource();
		App.MainDispatcher?.TryEnqueue(async delegate
		{
			try
			{
				ProcessingTask? existingTask = null;
				string? groupBase = (groupId != null && groupId.Length >= 12) ? groupId.Substring(0, 12) : null;
				if ((_currentPageType == "Update" || _currentPageType == "Multi") && groupBase != null)
				{
					existingTask = Tasks.FirstOrDefault((ProcessingTask t) => t.GroupId != null && t.GroupId.Length >= 12 && t.GroupId.Substring(0, 12) == groupBase && t.Operation == _currentPageType && t.Status == "Ожидание");
				}
				if (existingTask != null)
				{
					int added = 0;
					foreach (string f in files)
					{
						if (!existingTask.InputFiles.Contains<string>(f, StringComparer.OrdinalIgnoreCase))
						{
							existingTask.InputFiles.Add(f);
							existingTask.FilesList.Add(Path.GetFileName(f));
							added++;
						}
					}
					if (added > 0)
					{
						existingTask.SourceSizeBytes = existingTask.InputFiles.Sum((string path) => CalculateSize(path));
						existingTask.FilesCount = existingTask.InputFiles.Count((string path) => File.Exists(path)).ToString();
						existingTask.SourceFormat = "MULTI";
						string[] dirs = existingTask.InputFiles.Select((string path) => Path.GetDirectoryName(path)).Where(d => d != null).Select(d => d!).Distinct().ToArray();
						existingTask.InputFolders = string.Join("; ", dirs);
						bool romFs = false;
						bool exeFs = false;
						foreach (string f2 in existingTask.InputFiles)
						{
							if (Directory.Exists(f2))
							{
								string dirName = Path.GetFileName(f2).ToLower();
								if (dirName == "romfs")
								{
									romFs = true;
								}
								if (dirName == "exefs")
								{
									exeFs = true;
								}
							}
						}
						existingTask.HasRomFs = (romFs ? "1" : "-");
						existingTask.HasExeFs = (exeFs ? "1" : "-");
						App.Logger.Log($"К задаче {existingTask.Id} добавлены новые файлы ({added} шт.)");
					}
				}
				else
				{
					string firstFile = files[0];
					bool isDirectory = Directory.Exists(firstFile);
					string ext = (isDirectory ? "DIR" : Path.GetExtension(firstFile).ToUpper().Trim('.'));
					string outputName;
					if (files.Count > 1)
					{
						string baseGame = files.FirstOrDefault(delegate(string filePath)
						{
							try
							{
								SwitchFormatInfo switchFormatInfo = App.SwitchFormat.ParseNsp(filePath);
								return switchFormatInfo.ContentType == "Application" || (switchFormatInfo.TitleId != null && switchFormatInfo.TitleId.EndsWith("000"));
							}
							catch
							{
								return false;
							}
						}) ?? files[0];
						outputName = Path.GetFileNameWithoutExtension(baseGame);
						ext = "MULTI";
					}
					else
					{
						outputName = (isDirectory ? Path.GetFileName(firstFile) : Path.GetFileNameWithoutExtension(firstFile));
					}
					if (App.Settings.Current.ComplexFolders && basePath != null)
					{
						string dirName2 = Path.GetFileName(basePath);
						if (dirName2.StartsWith("["))
						{
							string parentDir = Path.GetFileName(Path.GetDirectoryName(basePath) ?? string.Empty);
							if (!string.IsNullOrEmpty(parentDir))
							{
								outputName = parentDir + " " + dirName2;
							}
						}
						else
						{
							try
							{
								string[] subDirs = Directory.GetDirectories(basePath, "[*");
								if (subDirs.Length != 0)
								{
									string subDirName = Path.GetFileName(subDirs[0]);
									outputName = dirName2 + " " + subDirName;
								}
							}
							catch
							{
							}
						}
					}
					string dbLogInfo = "";
					if (groupId != null && groupId.Length >= 12 && Regex.IsMatch(groupId.Substring(0, 12), "^[0-9a-fA-F]{12}$"))
					{
						string tid16 = groupId.Substring(0, 12).ToUpper() + "000";
						if (App.TitleDb.TryGetTitleInfo(tid16, out var entry) && entry != null)
						{
							if (!string.IsNullOrEmpty(entry.Name))
							{
								string safeName = string.Join("_", entry.Name.Split(Path.GetInvalidFileNameChars())).Replace(" ", "_");
								outputName = safeName;
								if (_currentPageType == "Multi")
								{
									outputName += "_Multi";
								}
								else if (_currentPageType == "Update")
								{
									outputName += "_Update";
								}
							}
							int totalDlc = App.TitleDb.GetDlcCount(tid16);
							string remoteVer = entry.Version ?? "";
							dbLogInfo = dbLogInfo + "\n[NSWDB] Игра найдена: " + entry.Name;
							if (!string.IsNullOrEmpty(remoteVer))
							{
								dbLogInfo = dbLogInfo + "\n[NSWDB] Актуальная версия патча: " + remoteVer;
							}
							if (totalDlc > 0)
							{
								dbLogInfo += $"\n[NSWDB] Всего выпущено DLC: {totalDlc}";
							}
						}
					}
					long sizeBytes = files.Sum((string path) => CalculateSize(path));
					int filesCount = files.Count((string path) => File.Exists(path));
					List<string> inputFiles = new List<string>(files);
					List<string> filesList = files.Select((string path) => Path.GetFileName(path)).ToList();
					bool rFs = false;
					bool eFs = false;
					foreach (string f3 in files)
					{
						if (Directory.Exists(f3))
						{
							string dirName3 = Path.GetFileName(f3).ToLower();
							if (dirName3 == "romfs")
							{
								rFs = true;
							}
							if (dirName3 == "exefs")
							{
								eFs = true;
							}
						}
					}
					string outFolder = App.Settings.Current.OutputFolder;
					if (string.IsNullOrEmpty(outFolder))
					{
						outFolder = GetOutPathForPage(_currentPageType);
					}
					ObservableCollection<ProcessingTask> targetList = ((_currentPageType == "Verify") ? VerifyTasks : Tasks);
					ProcessingTask task = new ProcessingTask
					{
						Id = $"T{targetList.Count + 1:D3}",
						GroupId = (groupId ?? ""),
						Operation = _currentPageType,
						SourceFormat = ext,
						TargetFormat = SelectedFormat,
						SourceSizeBytes = sizeBytes,
						TargetSize = "-",
						SizeDifference = "-",
						CompressionLevel = App.Settings.Current.CompressionLevel.ToString(),
						FilesCount = filesCount.ToString(),
						InputFiles = inputFiles,
						FilesList = filesList,
						HasRomFs = (rFs ? "1" : "-"),
						HasExeFs = (eFs ? "1" : "-"),
						Status = "Ожидание",
						Progress = 0.0,
						InputFolders = string.Join("; ", inputFiles.Select((string path) => Path.GetDirectoryName(path)).Distinct()),
						OutputFolder = outFolder,
						OutputFileName = outputName,
						LogDetails = "Готов к обработке..." + dbLogInfo
					};
					targetList.Add(task);
					if (iconBytes != null && iconBytes.Length != 0)
					{
						try
						{
							InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream();
							using (DataWriter writer = new DataWriter(stream.GetOutputStreamAt(0uL)))
							{
								writer.WriteBytes(iconBytes);
								await writer.StoreAsync();
							}
							BitmapImage bitmap = new BitmapImage();
							await bitmap.SetSourceAsync(stream);
							task.GameIcon = bitmap;
						}
						catch
						{
						}
					}
					App.Logger.Log($"Добавлена задача: {outputName} ({filesCount} файлов)");
					App.TicketHarvester.HarvestTicketsBackground(inputFiles);
				}
				tcs.SetResult();
			}
			catch (Exception ex)
			{
				tcs.SetException(ex);
			}
		});
		if (iconBytes == null || iconBytes.Length == 0 || string.IsNullOrEmpty(groupId) || !(_currentPageType != "Verify"))
		{
			return tcs.Task;
		}
		string safeGroupId = string.Join("_", groupId.Split(Path.GetInvalidFileNameChars()));
		if (string.IsNullOrEmpty(safeGroupId))
		{
			return tcs.Task;
		}
		Task.Run(delegate
		{
			try
			{
				string text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons");
				if (!Directory.Exists(text))
				{
					Directory.CreateDirectory(text);
				}
				string path = Path.Combine(text, safeGroupId + ".png");
				File.WriteAllBytes(path, iconBytes);
			}
			catch
			{
			}
		});
		return tcs.Task;
	}

	private string GetOutPathForPage(string pageType)
	{
		if (1 == 0)
		{
		}
		string result = pageType switch
		{
			"Update" => App.Settings.Current.LastOutPath_Update, 
			"Unpack" => App.Settings.Current.LastOutPath_Unpack, 
			"Pack" => App.Settings.Current.LastOutPath_Pack, 
			"Convert" => App.Settings.Current.LastOutPath_Convert, 
			"Multi" => App.Settings.Current.LastOutPath_Multi, 
			_ => "", 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private void DeleteTask(ProcessingTask? task)
	{
		if (task == null)
		{
			return;
		}
		ObservableCollection<ProcessingTask> targetList = ((task.Operation == "Verify") ? VerifyTasks : Tasks);
		if (!targetList.Contains(task))
		{
			return;
		}
		App.MainDispatcher?.TryEnqueue(delegate
		{
			try
			{
				targetList.Remove(task);
				App.Logger.Log("Задача " + task.Id + " удалена");
				for (int i = 0; i < targetList.Count; i++)
				{
					targetList[i].Id = $"T{i + 1:D3}";
				}
			}
			catch (Exception ex)
			{
				App.Logger.Log("Ошибка при удалении задачи: " + ex.Message, LogLevel.Error);
			}
		});
	}

	private void AddTestTask()
	{
		bool flag = Tasks.Count % 2 == 0;
		Tasks.Add(new ProcessingTask
		{
			Id = $"T{Tasks.Count + 1:D3}",
			Operation = (flag ? "Multi" : "Convert"),
			SourceFormat = (flag ? "NSP" : "XCI"),
			TargetFormat = SelectedFormat,
			SourceSizeBytes = (flag ? 15400000000L : 8200000000L),
			TargetSize = "-",
			SizeDifference = "-",
			CompressionLevel = (flag ? "0" : App.Settings.Current.CompressionLevel.ToString()),
			FilesCount = (flag ? "3" : "1"),
			InputFiles = (flag ? new List<string> { "C:\\Games\\Zelda\\Base.nsp", "C:\\Games\\Updates\\Update_v2.nsp", "C:\\Games\\DLC\\DLC_Pack.nsp" } : new List<string> { "C:\\Games\\Dumps\\game.xci" }),
			FilesList = (flag ? new List<string> { "Base.nsp", "Update_v2.nsp", "DLC_Pack.nsp" } : new List<string> { "game.xci" }),
			HasRomFs = "-",
			HasExeFs = "-",
			Status = "Ожидание",
			Progress = 0.0,
			InputFolders = (flag ? "C:\\Games\\Zelda; C:\\Games\\Updates" : "C:\\Games\\Dumps"),
			OutputFolder = App.Settings.Current.OutputFolder,
			OutputFileName = (flag ? "Zelda_TotK_Multi" : "New_Game_Dump"),
			LogDetails = "Ожидает запуска"
		});
		App.Logger.Log($"Добавлена тестовая задача T{Tasks.Count:D3}", LogLevel.Debug);
	}

	private void ClearTasks()
	{
		if (_currentPageType == "Verify")
		{
			VerifyTasks.Clear();
			App.Logger.Log("Список проверок очищен");
		}
		else
		{
			Tasks.Clear();
			App.Logger.Log("Список задач очищен");
		}
	}

	private void StopAllTasks()
	{
		App.Logger.Log("Остановка всех запущенных задач...", LogLevel.Warning);
		List<ProcessingTask> list = Tasks.Concat(VerifyTasks).ToList();
		foreach (ProcessingTask item in list)
		{
			if (item.IsRunning && item.Cts != null)
			{
				item.Cts.Cancel();
				item.Status = "Отменен";
				item.IsRunning = false;
				item.LogDetails += "\nЗадача была отменена пользователем.";
			}
		}
	}

	private void StartAllTasks()
	{
		App.Logger.Log("Запуск фоновой обработки очереди задач...");
		if (_isProcessingQueue)
		{
			return;
		}
		_isProcessingQueue = true;
		bool flag = _currentPageType == "Verify";
		ObservableCollection<ProcessingTask> targetList = (flag ? VerifyTasks : Tasks);
		Task.Run(async delegate
		{
			try
			{
				while (_isProcessingQueue)
				{
					int maxConcurrent = App.Settings.Current.ConcurrentTasks;
					if (maxConcurrent < 1)
					{
						maxConcurrent = 1;
					}
					int runningCount = 0;
					ProcessingTask? nextTask = null;
					bool hasWaitTasks = false;
					TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>();
					App.MainDispatcher?.TryEnqueue(delegate
					{
						runningCount = targetList.Count((ProcessingTask t) => t.IsRunning);
						hasWaitTasks = targetList.Any((ProcessingTask t) => t.Status == "Ожидание");
						if (runningCount < maxConcurrent)
						{
							nextTask = targetList.FirstOrDefault((ProcessingTask t) => t.Status == "Ожидание");
							if (nextTask != null)
							{
								nextTask.Status = "Подготовка...";
								nextTask.IsRunning = true;
							}
						}
						tcs.SetResult(result: true);
					});
					if (App.MainDispatcher != null)
					{
						await tcs.Task;
					}
					if (nextTask != null)
					{
						_ = ExecuteTaskSafelyAsync(nextTask);
					}
					else
					{
						if (!hasWaitTasks && runningCount == 0)
						{
							break;
						}
						await Task.Delay(500);
					}
				}
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				App.Logger.Log("Критическая ошибка в главном цикле очереди: " + ex2.Message, LogLevel.Error);
			}
			finally
			{
				_isProcessingQueue = false;
			}
		});
	}

	private async Task ExecuteTaskSafelyAsync(ProcessingTask task)
	{
		try
		{
			await ExecuteTaskAsync(task);
			App.MainDispatcher?.TryEnqueue(delegate
			{
				if (task.Status == "Подготовка..." || task.Status == "Сжатие..." || task.Status == "Распаковка..." || task.Status == "Сборка..." || task.Status == "Ожидание" || string.IsNullOrEmpty(task.Status))
				{
					task.Status = "Успешно";
				}
				task.IsRunning = false;
				AppendStyledSummary(task, task.Status);
				HistoryService.AddToHistory(task);
			});
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			Exception ex3 = ex2;
			App.MainDispatcher?.TryEnqueue(delegate
			{
				task.Status = "Ошибка";
				task.IsRunning = false;
				ProcessingTask processingTask = task;
				processingTask.LogDetails = processingTask.LogDetails + "\nКритическая ошибка: " + ex3.Message;
				AppendStyledSummary(task, task.Status);
				HistoryService.AddToHistory(task);
			});
		}
	}

	private void AppendStyledSummary(ProcessingTask task, string status)
	{
		var sb = new System.Text.StringBuilder();
		sb.AppendLine();
		sb.AppendLine("╔════════════════════════════════════════════════════════════╗");
		sb.AppendLine("║                РЕЗУЛЬТАТ ВЫПОЛНЕНИЯ РАБОТЫ                 ║");
		sb.AppendLine("╚════════════════════════════════════════════════════════════╝");
		
		string opName = task.OperationDisplay;
		sb.AppendLine($"  ▶ Операция: {opName}");
		
		string statusEmoji = (status == "Успешно" || status == "Готово" || status == "Ок" || status == "Да") ? "✅" : "❌";
		sb.AppendLine($"  ▶ Статус: {statusEmoji} {status}");
		
		if (!string.IsNullOrEmpty(task.GameName))
		{
			sb.AppendLine($"  ▶ Игра: {task.GameName}");
		}
		
		if (task.InputFiles != null && task.InputFiles.Count > 0)
		{
			sb.AppendLine("  ▶ Входные файлы:");
			foreach (var file in task.InputFiles)
			{
				sb.AppendLine($"     • {System.IO.Path.GetFileName(file)}");
			}
		}
		else if (!string.IsNullOrEmpty(task.InputFolders))
		{
			sb.AppendLine("  ▶ Входные папки:");
			var folders = task.InputFolders.Split(';');
			foreach (var folder in folders)
			{
				if (!string.IsNullOrEmpty(folder.Trim()))
				{
					sb.AppendLine($"     • {folder.Trim()}");
				}
			}
		}
		
		if (task.Operation == "Unpack")
		{
			sb.AppendLine($"  ▶ Распаковано в папку: {task.OutputFolder}");
		}
		else if (!string.IsNullOrEmpty(task.OutputFolder))
		{
			string targetExt = string.IsNullOrEmpty(task.TargetFormat) ? "nsp" : task.TargetFormat.ToLower();
			string outName = string.IsNullOrEmpty(task.OutputFileName) ? "Packed" : task.OutputFileName;
			if (!outName.EndsWith("." + targetExt, StringComparison.OrdinalIgnoreCase))
			{
				outName += "." + targetExt;
			}
			string outPath = System.IO.Path.Combine(task.OutputFolder, outName);
			sb.AppendLine($"  ▶ Выходной файл: {outPath}");
		}
		
		sb.AppendLine("  ▶ Подробности:");
		if (status == "Успешно" || status == "Готово")
		{
			switch (task.Operation)
			{
				case "Unpack":
					sb.AppendLine("     ✓ Успешно произведена распаковка файлов игрового контейнера.");
					sb.AppendLine("     ✓ Файлы RomFS и ExeFS извлечены с заменой в указанную директорию.");
					break;
				case "Pack":
					sb.AppendLine($"     ✓ Произведена упаковка папки в контейнер {task.TargetFormat.ToUpper()}.");
					sb.AppendLine("     ✓ Все файлы RomFS/ExeFS успешно собраны в единый пакет.");
					break;
				case "Convert":
					sb.AppendLine($"     ✓ Произведена конвертация исходного файла в формат {task.TargetFormat.ToUpper()}.");
					sb.AppendLine("     ✓ Структура разделов и метаданные сохранены.");
					break;
				case "Update":
					sb.AppendLine("     ✓ Сшито обновление с базовым файлом игры (HardPatch).");
					sb.AppendLine("     ✓ Изменения успешно интегрированы в выходной файл.");
					break;
				case "Multi":
					sb.AppendLine("     ✓ Сшито базовое приложение, обновления и DLC в единый пакет.");
					sb.AppendLine($"     ✓ Сгенерирован мульти-контент в формате {task.TargetFormat.ToUpper()}.");
					break;
				case "Verify":
					sb.AppendLine("     ✓ Произведена верификация структуры и целостности контейнера.");
					sb.AppendLine($"     ✓ Тип: {task.VerifyType} | Структура: {task.VerifyStructure}");
					break;
				default:
					sb.AppendLine("     ✓ Обработка успешно завершена.");
					break;
			}
		}
		else
		{
			sb.AppendLine("     ❌ Во время обработки произошла ошибка. Проверьте лог выше.");
		}
		
		sb.AppendLine("══════════════════════════════════════════════════════════════");
		task.LogDetails += sb.ToString();
	}

	private async Task ExecuteTaskAsync(ProcessingTask task)
	{
		CancellationTokenSource cts = new CancellationTokenSource();
		task.Cts = cts;
		if (task.Operation == "Verify")
		{
			task.IsRunning = true;
			List<string> inputFiles = task.InputFiles;
			foreach (string f in inputFiles)
			{
				if (cts.IsCancellationRequested)
				{
					break;
				}
				await App.SwitchFormat.VerifyNspAsync(task, f, cts.Token);
			}
			if (!cts.IsCancellationRequested)
			{
				App.MainDispatcher?.TryEnqueue(delegate
				{
					task.Status = "Успешно";
					task.IsRunning = false;
				});
			}
			return;
		}
		if (task.Operation == "Update")
		{
			task.IsRunning = true;
			List<string> inputFiles2 = task.InputFiles;
			string outPath = Path.Combine(task.OutputFolder, task.OutputFileName + "." + task.TargetFormat.ToLower());
			await App.HardPatch.PatchUpdateAsync(task, inputFiles2, outPath, cts.Token);
			return;
		}
		if (task.Operation == "Multi")
		{
			task.IsRunning = true;
			List<string> inputFiles3 = task.InputFiles;
			string outPath2 = Path.Combine(task.OutputFolder, task.OutputFileName + "." + task.TargetFormat.ToLower());
			bool hasMods = task.HasRomFs == "1" || task.HasExeFs == "1";
			if (App.Settings.Current.ForceMultiRebuild || hasMods)
			{
				string tempPatchPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp", "prepatch_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".nsp");
				Directory.CreateDirectory(Path.GetDirectoryName(tempPatchPath)!);
				await App.HardPatch.PatchUpdateAsync(task, inputFiles3, tempPatchPath, cts.Token, isMultiContent: true);
				if (!File.Exists(tempPatchPath))
				{
					if (task.Status != "Отменен")
					{
						App.MainDispatcher?.TryEnqueue(delegate
						{
							task.Status = "Ошибка";
							task.LogDetails += "\nОшибка предварительной сборки (HardPatch)!";
						});
					}
					return;
				}
				string baseFile = "";
				string updateFile = "";
				foreach (string file in inputFiles3)
				{
					if (Directory.Exists(file))
					{
						continue;
					}
					try
					{
						SwitchFormatInfo info = App.SwitchFormat.ParseNsp(file);
						if (info.ContentType == "Application")
						{
							baseFile = file;
						}
						else if (info.ContentType == "Patch")
						{
							updateFile = file;
						}
					}
					catch
					{
					}
				}
				if (string.IsNullOrEmpty(baseFile))
				{
					baseFile = inputFiles3.FirstOrDefault((string text) => !Directory.Exists(text) && (text.Contains("[v0]") || text.Contains("v0"))) ?? "";
				}
				if (string.IsNullOrEmpty(updateFile))
				{
					updateFile = inputFiles3.FirstOrDefault((string text) => !Directory.Exists(text) && text != baseFile && text.Contains("v") && !text.Contains("v0")) ?? "";
				}
				List<string> finalInputs = new List<string> { tempPatchPath };
				foreach (string file2 in inputFiles3)
				{
					if (!Directory.Exists(file2) && !(file2 == baseFile) && !(file2 == updateFile))
					{
						finalInputs.Add(file2);
					}
				}
				await App.MultiContent.BuildMultiContentAsync(task, finalInputs, outPath2, patchFirmware: false, cts.Token);
				try
				{
					File.Delete(tempPatchPath);
				}
				catch
				{
				}
			}
			else
			{
				List<string> nspInputFiles = inputFiles3.Where((string path) => !Directory.Exists(path)).ToList();
				await App.MultiContent.BuildMultiContentAsync(task, nspInputFiles, outPath2, patchFirmware: false, cts.Token);
			}
			return;
		}
		if (task.Operation == "Unpack")
		{
			string? inputPath = task.InputFiles.FirstOrDefault();
			if (inputPath == null)
			{
				return;
			}
			string outBaseFolder = task.OutputFolder;
			if (string.IsNullOrEmpty(outBaseFolder))
			{
				outBaseFolder = "E:\\OUT\\Unpack";
			}
			task.IsRunning = true;
			task.Status = "Распаковка...";
			try
			{
				if (!File.Exists(inputPath))
				{
					Directory.CreateDirectory(Path.GetDirectoryName(inputPath)!);
					File.WriteAllBytes(inputPath, new byte[1024]);
				}
				await App.SwitchFormat.UnpackContainerAsync(task, inputPath, outBaseFolder, cts.Token);
				App.MainDispatcher?.TryEnqueue(delegate
				{
					task.IsRunning = false;
					task.Status = "Готово";
					task.LogDetails = "Распаковка успешно завершена.";
				});
			}
			catch (Exception ex)
			{
				Exception ex2 = ex;
				Exception ex3 = ex2;
				App.MainDispatcher?.TryEnqueue(delegate
				{
					task.IsRunning = false;
					task.Status = "Ошибка";
					task.LogDetails = ex3.Message;
				});
			}
			return;
		}
		if (task.Operation == "Pack")
		{
			string? inputFolder = task.InputFolders.Split(';').FirstOrDefault()?.Trim();
			string outBaseFolder2 = task.OutputFolder;
			if (string.IsNullOrEmpty(outBaseFolder2))
			{
				outBaseFolder2 = "E:\\OUT\\Pack";
			}
			task.IsRunning = true;
			task.Status = "Сборка...";
			try
			{
				string expectedOutPath = Path.Combine(outBaseFolder2, string.IsNullOrEmpty(task.OutputFileName) ? "Packed.nsp" : task.OutputFileName);
				if (!expectedOutPath.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase))
				{
					expectedOutPath += ".nsp";
				}
				await App.SwitchFormat.PackContainerAsync(task, inputFolder ?? "", outBaseFolder2, task.OutputFileName, cts.Token);
				if (task.TargetFormat.Equals("XCI", StringComparison.OrdinalIgnoreCase) || task.TargetFormat.Equals("XCZ", StringComparison.OrdinalIgnoreCase))
				{
					await App.SwitchFormat.ConvertContainerAsync(task, expectedOutPath, outBaseFolder2, "XCI", cts.Token);
					string expectedXci = Path.ChangeExtension(expectedOutPath, ".xci");
					if (task.TargetFormat.Equals("XCZ", StringComparison.OrdinalIgnoreCase) && File.Exists(expectedXci))
					{
						await App.NszCompression.CompressToNszAsync(task, expectedXci, outBaseFolder2, cts.Token);
						string finalXcz = Path.ChangeExtension(expectedXci, ".xcz");
						string generatedNsz = Path.ChangeExtension(expectedXci, ".nsz");
						if (File.Exists(generatedNsz))
						{
							if (File.Exists(finalXcz))
							{
								File.Delete(finalXcz);
							}
							File.Move(generatedNsz, finalXcz);
						}
						try
						{
							File.Delete(expectedXci);
						}
						catch
						{
						}
					}
				}
				else if (task.TargetFormat.Equals("NSZ", StringComparison.OrdinalIgnoreCase))
				{
					await App.NszCompression.CompressToNszAsync(task, expectedOutPath, outBaseFolder2, cts.Token);
				}
				App.MainDispatcher?.TryEnqueue(delegate
				{
					task.IsRunning = false;
					task.Status = "Готово";
					task.LogDetails += "\nУпаковка успешно завершена.";
				});
			}
			catch (Exception ex4)
			{
				Exception ex2 = ex4;
				Exception ex5 = ex2;
				App.MainDispatcher?.TryEnqueue(delegate
				{
					task.IsRunning = false;
					task.Status = "Ошибка";
					ProcessingTask processingTask = task;
					processingTask.LogDetails = processingTask.LogDetails + "\n" + ex5.Message;
				});
			}
			return;
		}
		task.IsRunning = true;
		string inputPath2 = task.InputFiles.FirstOrDefault() ?? Path.Combine(Path.GetTempPath(), task.OutputFileName);
		if (!File.Exists(inputPath2))
		{
			File.WriteAllBytes(inputPath2, new byte[52428800]);
		}
		string tempDecompDirConvert = "";
		try
		{
			string workingInput = inputPath2;
			if (inputPath2.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) || inputPath2.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase))
			{
				App.MainDispatcher?.TryEnqueue(delegate
				{
					task.LogDetails += "\nВходной файл сжат. Запуск предварительной декомпрессии...";
				});
				tempDecompDirConvert = Path.Combine(Path.GetTempPath(), "StormDecomp_" + Guid.NewGuid().ToString("N").Substring(0, 8));
				Directory.CreateDirectory(tempDecompDirConvert);
				string? decompResult = await App.NszCompression.DecompressNszAsync(task, inputPath2, tempDecompDirConvert, cts.Token);
				if (!string.IsNullOrEmpty(decompResult) && File.Exists(decompResult))
				{
					workingInput = decompResult;
				}
			}
			if (task.TargetFormat.Equals("XCI", StringComparison.OrdinalIgnoreCase) || task.TargetFormat.Equals("XCZ", StringComparison.OrdinalIgnoreCase))
			{
				await App.SwitchFormat.ConvertContainerAsync(task, workingInput, task.OutputFolder, "XCI", cts.Token);
				if (!task.TargetFormat.Equals("XCZ", StringComparison.OrdinalIgnoreCase))
				{
					return;
				}
				string expectedXci2 = Path.ChangeExtension(Path.Combine(task.OutputFolder, Path.GetFileName(workingInput)), ".xci");
				if (!File.Exists(expectedXci2))
				{
					return;
				}
				await App.NszCompression.CompressToNszAsync(task, expectedXci2, task.OutputFolder, cts.Token);
				string finalXcz2 = Path.ChangeExtension(expectedXci2, ".xcz");
				string generatedNsz2 = Path.ChangeExtension(expectedXci2, ".nsz");
				if (File.Exists(generatedNsz2))
				{
					if (File.Exists(finalXcz2))
					{
						File.Delete(finalXcz2);
					}
					File.Move(generatedNsz2, finalXcz2);
				}
				try
				{
					File.Delete(expectedXci2);
				}
				catch
				{
				}
				return;
			}
			if (task.TargetFormat.Equals("NSZ", StringComparison.OrdinalIgnoreCase))
			{
				await App.NszCompression.CompressToNszAsync(task, workingInput, task.OutputFolder, cts.Token);
			}
			else
			{
				if (!task.TargetFormat.Equals("NSP", StringComparison.OrdinalIgnoreCase))
				{
					return;
				}
				if (!workingInput.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase))
				{
					await App.SwitchFormat.ConvertContainerAsync(task, workingInput, task.OutputFolder, "NSP", cts.Token);
					return;
				}
				string destPath = Path.Combine(task.OutputFolder, Path.GetFileName(workingInput));
				if (workingInput != destPath)
				{
					App.MainDispatcher?.TryEnqueue(delegate
					{
						task.LogDetails += "\nКопирование в выходную директорию (с 16MB буферизацией)...";
					});
					using FileStream sourceStream = new FileStream(workingInput, FileMode.Open, FileAccess.Read, FileShare.Read, 16777216);
					using FileStream destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 16777216);
					await sourceStream.CopyToAsync(destStream);
				}
				App.MainDispatcher?.TryEnqueue(delegate
				{
					task.Status = "Успешно";
					task.IsRunning = false;
					task.Progress = 100.0;
				});
				return;
			}
		}
		catch (Exception ex6)
		{
			Exception ex2 = ex6;
			Exception ex7 = ex2;
			App.MainDispatcher?.TryEnqueue(delegate
			{
				task.Status = "Ошибка";
				task.IsRunning = false;
				ProcessingTask processingTask = task;
				processingTask.LogDetails = processingTask.LogDetails + "\n" + ex7.Message;
			});
		}
		finally
		{
			if (task.Operation == "Convert" && !string.IsNullOrEmpty(tempDecompDirConvert) && Directory.Exists(tempDecompDirConvert))
			{
				for (int i = 0; i < 3; i++)
				{
					try
					{
						Directory.Delete(tempDecompDirConvert, recursive: true);
					}
					catch
					{
						Thread.Sleep(500);
						continue;
					}
					break;
				}
			}
		}
	}
}
