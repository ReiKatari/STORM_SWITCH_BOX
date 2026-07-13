using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.Es;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using StormSwitchBox.Core.NSZ;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services;

public class SwitchFormatService
{
	private readonly KeysService _keysService;

	public SwitchFormatService(KeysService keysService)
	{
		_keysService = keysService;
	}

	public SwitchFormatInfo ParseNsp(string filePath)
	{
		SwitchFormatInfo switchFormatInfo = new SwitchFormatInfo
		{
			SizeBytes = new FileInfo(filePath).Length
		};
		if (!_keysService.IsLoaded)
		{
			return switchFormatInfo;
		}
		try
		{
			bool flag = filePath.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase);
			bool flag2 = filePath.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase);
			using FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
			IStorage storage = stream.AsStorage();
			IFileSystem fileSystem = null;
			PartitionFileSystem partitionFileSystem = null;
			if (flag2)
			{
				storage.GetSize(out var size).ThrowIfFailure();
				SubStorage storage2 = new SubStorage(storage, 65536L, size - 65536);
				PartitionFileSystem partitionFileSystem2 = new PartitionFileSystem(storage2);
				using UniqueRef<IFile> uniqueRef = default(UniqueRef<IFile>);
				using LibHac.Fs.Path path = new LibHac.Fs.Path();
				path.Initialize(new U8Span(Encoding.UTF8.GetBytes("/secure"))).ThrowIfFailure();
				partitionFileSystem2.OpenFile(ref uniqueRef.Ref, in path, OpenMode.Read).ThrowIfFailure();
				partitionFileSystem = new PartitionFileSystem(uniqueRef.Release().AsStorage());
				fileSystem = partitionFileSystem;
			}
			else
			{
				partitionFileSystem = new PartitionFileSystem(storage);
				fileSystem = partitionFileSystem;
			}
			foreach (DirectoryEntryEx item in from e in fileSystem.EnumerateEntries()
				where e.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase)
				select e)
			{
				using UniqueRef<IFile> uniqueRef2 = default(UniqueRef<IFile>);
				using LibHac.Fs.Path path2 = new LibHac.Fs.Path();
				path2.Initialize(new U8Span(Encoding.UTF8.GetBytes(item.FullPath))).ThrowIfFailure();
				fileSystem.OpenFile(ref uniqueRef2.Ref, in path2, OpenMode.Read).ThrowIfFailure();
				IFile file = uniqueRef2.Release();
				IStorage storage3 = file.AsStorage();
				Nca nca = new Nca(_keysService.CurrentKeyset, storage3);
				if (nca.Header.ContentType == NcaContentType.Meta)
				{
					IFileSystem fileSystem2 = null;
					try
					{
						fileSystem2 = nca.OpenFileSystem(0, IntegrityCheckLevel.ErrorOnInvalid);
					}
					catch
					{
					}
					if (fileSystem2 == null)
					{
						try
						{
							fileSystem2 = nca.OpenFileSystem(1, IntegrityCheckLevel.ErrorOnInvalid);
						}
						catch
						{
						}
					}
					if (fileSystem2 == null)
					{
						continue;
					}
					foreach (DirectoryEntryEx item2 in fileSystem2.EnumerateEntries("/", "*.cnmt"))
					{
						using UniqueRef<IFile> uniqueRef3 = default(UniqueRef<IFile>);
						using LibHac.Fs.Path path3 = new LibHac.Fs.Path();
						path3.Initialize(new U8Span(Encoding.UTF8.GetBytes(item2.FullPath))).ThrowIfFailure();
						fileSystem2.OpenFile(ref uniqueRef3.Ref, in path3, OpenMode.Read).ThrowIfFailure();
						IFile file2 = uniqueRef3.Release();
						using MemoryStream memoryStream = new MemoryStream();
						file2.AsStream().CopyTo(memoryStream);
						byte[] array = memoryStream.ToArray();
						ulong num = BitConverter.ToUInt64(array, 0);
						uint num2 = BitConverter.ToUInt32(array, 8);
						byte b = array[12];
						switchFormatInfo.TitleId = num.ToString("X16");
						switchFormatInfo.Version = num2.ToString();
						switch (b)
						{
						case 128:
							switchFormatInfo.ContentType = "Application";
							break;
						case 129:
							switchFormatInfo.ContentType = "Patch";
							break;
						case 130:
							switchFormatInfo.ContentType = "AddOnContent";
							break;
						default:
							switchFormatInfo.ContentType = "Unknown";
							break;
						}
						App.Logger.Log($"[LibHac] Парсинг успешен: {switchFormatInfo.TitleId} | Тип: {switchFormatInfo.ContentType} | v{switchFormatInfo.Version}", LogLevel.Debug);
					}
					continue;
				}
				if (nca.Header.ContentType != NcaContentType.Control)
				{
					continue;
				}
				IFileSystem fileSystem3 = null;
				try
				{
					fileSystem3 = nca.OpenFileSystem(0, IntegrityCheckLevel.ErrorOnInvalid);
				}
				catch
				{
				}
				if (fileSystem3 == null)
				{
					continue;
				}
				DirectoryEntryEx directoryEntryEx = fileSystem3.EnumerateEntries("/", "icon_*.dat").FirstOrDefault();
				if (directoryEntryEx == null)
				{
					continue;
				}
				using UniqueRef<IFile> uniqueRef4 = default(UniqueRef<IFile>);
				using LibHac.Fs.Path path4 = new LibHac.Fs.Path();
				path4.Initialize(new U8Span(Encoding.UTF8.GetBytes(directoryEntryEx.FullPath))).ThrowIfFailure();
				fileSystem3.OpenFile(ref uniqueRef4.Ref, in path4, OpenMode.Read).ThrowIfFailure();
				IFile file3 = uniqueRef4.Release();
				using MemoryStream memoryStream2 = new MemoryStream();
				file3.AsStream().CopyTo(memoryStream2);
				switchFormatInfo.IconBytes = memoryStream2.ToArray();
			}
		}
		catch (Exception ex)
		{
			App.Logger.Log("Ошибка парсинга " + System.IO.Path.GetFileName(filePath) + ": " + ex.Message, LogLevel.Warning);
		}
		return switchFormatInfo;
	}

	public async Task UnpackContainerAsync(ProcessingTask task, string filePath, string baseOutFolder, CancellationToken token)
	{
		if (!_keysService.IsLoaded)
		{
			throw new Exception("Ключи не загружены.");
		}
		App.MainDispatcher?.TryEnqueue(delegate
		{
			task.Status = "Анализ...";
		});
		App.MainDispatcher?.TryEnqueue(delegate
		{
			task.LogDetails += "\nАнализ файла через нативный LibHac (Zero-Disk-IO)...";
		});
		SwitchFormatInfo info = ParseNsp(filePath);
		string subFolder = "basedata";
		if (info.ContentType.Contains("Patch", StringComparison.OrdinalIgnoreCase))
		{
			subFolder = "updatedata";
		}
		if (info.ContentType.Contains("AddOnContent", StringComparison.OrdinalIgnoreCase))
		{
			subFolder = "dlcdata";
		}
		string finalOutPath = System.IO.Path.Combine(baseOutFolder, subFolder);
		App.Logger.Log("Начало анатомической распаковки в: " + finalOutPath);
		Directory.CreateDirectory(finalOutPath);
		await Task.Run(delegate
		{
			bool flag = filePath.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase);
			using FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
			IStorage storage = stream.AsStorage();
			IFileSystem fileSystem = null;
			PartitionFileSystem partitionFileSystem = null;
			if (flag)
			{
				storage.GetSize(out var size).ThrowIfFailure();
				SubStorage storage2 = new SubStorage(storage, 65536L, size - 65536);
				PartitionFileSystem partitionFileSystem2 = new PartitionFileSystem(storage2);
				using UniqueRef<IFile> uniqueRef = default(UniqueRef<IFile>);
				using LibHac.Fs.Path path = new LibHac.Fs.Path();
				path.Initialize(new U8Span(Encoding.UTF8.GetBytes("/secure"))).ThrowIfFailure();
				partitionFileSystem2.OpenFile(ref uniqueRef.Ref, in path, OpenMode.Read).ThrowIfFailure();
				partitionFileSystem = new PartitionFileSystem(uniqueRef.Release().AsStorage());
				fileSystem = partitionFileSystem;
			}
			else
			{
				partitionFileSystem = new PartitionFileSystem(storage);
				fileSystem = partitionFileSystem;
			}
			Dictionary<string, byte[]> dictionary = new Dictionary<string, byte[]>();
			foreach (DirectoryEntryEx item in from e in fileSystem.EnumerateEntries()
				where e.Name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase)
				select e)
			{
				try
				{
					using UniqueRef<IFile> uniqueRef2 = default(UniqueRef<IFile>);
					using LibHac.Fs.Path path2 = new LibHac.Fs.Path();
					path2.Initialize(new U8Span(Encoding.UTF8.GetBytes(item.FullPath))).ThrowIfFailure();
					fileSystem.OpenFile(ref uniqueRef2.Ref, in path2, OpenMode.Read).ThrowIfFailure();
					IFile file = uniqueRef2.Release();
					using MemoryStream memoryStream = new MemoryStream();
					file.AsStream().CopyTo(memoryStream);
					memoryStream.Position = 0L;
					Ticket ticket = new Ticket(memoryStream);
					byte[] titleKey = ticket.GetTitleKey(_keysService.CurrentKeyset);
					string key = BitConverter.ToString(ticket.RightsId).Replace("-", "").ToLowerInvariant();
					dictionary[key] = titleKey;
				}
				catch
				{
				}
			}
			List<DirectoryEntryEx> list = fileSystem.EnumerateEntries().ToList();
			int totalFiles = list.Count;
			int currentFile = 0;
			Stopwatch stopwatch = Stopwatch.StartNew();
			foreach (DirectoryEntryEx item2 in list)
			{
				token.ThrowIfCancellationRequested();
				int num = currentFile;
				currentFile = num + 1;
				string entryName = item2.Name;
				if (stopwatch.ElapsedMilliseconds > 100 || currentFile == totalFiles)
				{
					App.MainDispatcher?.TryEnqueue(delegate
					{
						task.LogDetails = "Распаковка: " + entryName;
						task.Progress = (double)currentFile / (double)totalFiles * 100.0;
					});
					stopwatch.Restart();
				}
				using UniqueRef<IFile> uniqueRef3 = default(UniqueRef<IFile>);
				using LibHac.Fs.Path path3 = new LibHac.Fs.Path();
				path3.Initialize(new U8Span(Encoding.UTF8.GetBytes(item2.FullPath))).ThrowIfFailure();
				fileSystem.OpenFile(ref uniqueRef3.Ref, in path3, OpenMode.Read).ThrowIfFailure();
				IFile file2 = uniqueRef3.Release();
				IStorage storage3 = file2.AsStorage();
				if (entryName.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) || entryName.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase))
				{
					if (entryName.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase))
					{
						storage3 = new StormNczStorage(storage3, dictionary);
						entryName = System.IO.Path.ChangeExtension(entryName, ".nca");
					}
					Nca nca = new Nca(_keysService.CurrentKeyset, storage3);
					if (nca.Header.ContentType == NcaContentType.Program)
					{
						ExtractNcaSection(nca, NcaSectionType.Data, System.IO.Path.Combine(finalOutPath, "romfs"), token);
						ExtractNcaSection(nca, NcaSectionType.Code, System.IO.Path.Combine(finalOutPath, "exefs"), token);
					}
					else if (nca.Header.ContentType == NcaContentType.Control)
					{
						ExtractNcaSection(nca, NcaSectionType.Data, System.IO.Path.Combine(finalOutPath, "control"), token);
					}
					else
					{
						SaveStorageToFile(storage3, System.IO.Path.Combine(finalOutPath, entryName));
					}
				}
				else
				{
					SaveStorageToFile(storage3, System.IO.Path.Combine(finalOutPath, entryName));
				}
			}
		}, token);
	}

	public async Task ConvertContainerAsync(ProcessingTask task, string inputPath, string outDir, string targetFormat, CancellationToken cancellationToken)
	{
		string targetExt = ((targetFormat.ToLower() == "xci" || targetFormat.ToLower() == "xcz") ? ".xci" : ".nsp");
		App.MainDispatcher?.TryEnqueue(delegate
		{
			ProcessingTask processingTask = task;
			processingTask.LogDetails = processingTask.LogDetails + "\nНативная конвертация в формат " + targetExt.ToUpper() + "...";
			task.Status = "Сборка " + targetExt.ToUpper() + "...";
		});
		if (targetExt == ".xci")
		{
			throw new Exception("Нативная сборка XCI временно не поддерживается. Выберите NSP/NSZ.");
		}
		string expectedFile = System.IO.Path.ChangeExtension(System.IO.Path.Combine(outDir, System.IO.Path.GetFileName(inputPath)), ".nsp");
		await Task.Run(delegate
		{
			List<IFileSystem> list = new List<IFileSystem>();
			List<IFile> list2 = new List<IFile>();
			List<object> list3 = new List<object>();
			try
			{
				using FileStream stream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
				IStorage storage = stream.AsStorage();
				storage.GetSize(out var size).ThrowIfFailure();
				SubStorage subStorage = new SubStorage(storage, 65536L, size - 65536);
				list3.Add(subStorage);
				PartitionFileSystem partitionFileSystem = new PartitionFileSystem(subStorage);
				list.Add(partitionFileSystem);
				IFile file = OpenFileSafe(partitionFileSystem, "/secure");
				list2.Add(file);
				IStorage storage2 = file.AsStorage();
				list3.Add(storage2);
				PartitionFileSystem partitionFileSystem2 = new PartitionFileSystem(storage2);
				list.Add(partitionFileSystem2);
				PartitionFileSystemBuilder partitionFileSystemBuilder = new PartitionFileSystemBuilder();
				foreach (DirectoryEntryEx item in partitionFileSystem2.EnumerateEntries())
				{
					cancellationToken.ThrowIfCancellationRequested();
					IFile file2 = OpenFileSafe(partitionFileSystem2, item.FullPath);
					list2.Add(file2);
					IStorage storage3 = file2.AsStorage();
					list3.Add(storage3);
					partitionFileSystemBuilder.AddFile(item.Name, new StorageFile(new SafeStorageWrapper(storage3), OpenMode.Read));
				}
				App.MainDispatcher?.TryEnqueue(delegate
				{
					task.LogDetails += "\nЗапись NSP на диск...";
				});
				using IStorage storage4 = partitionFileSystemBuilder.Build(PartitionFileSystemType.Standard);
				using FileStream fileStream = new FileStream(expectedFile, FileMode.Create, FileAccess.Write, FileShare.None, 16777216);
				storage4.GetSize(out var size2).ThrowIfFailure();
				long num = size2;
				long num2 = 0L;
				byte[] array = new byte[81920];
				while (num > 0)
				{
					cancellationToken.ThrowIfCancellationRequested();
					int num3 = (int)Math.Min(array.Length, num);
					storage4.Read(num2, array.AsSpan(0, num3)).ThrowIfFailure();
					fileStream.Write(array, 0, num3);
					num2 += num3;
					num -= num3;
					double percent = 100.0 - (double)num / (double)size2 * 100.0;
					if (task.Progress != (double)(int)percent)
					{
						App.MainDispatcher?.TryEnqueue(delegate
						{
							task.Progress = (int)percent;
						});
					}
				}
			}
			finally
			{
				foreach (IFile item2 in list2)
				{
					try
					{
						item2.Dispose();
					}
					catch
					{
					}
				}
				foreach (IFileSystem item3 in list)
				{
					try
					{
						item3.Dispose();
					}
					catch
					{
					}
				}
				foreach (object item4 in list3)
				{
					if (item4 is IDisposable disposable)
					{
						try
						{
							disposable.Dispose();
						}
						catch
						{
						}
					}
				}
			}
		}, cancellationToken);
		App.MainDispatcher?.TryEnqueue(delegate
		{
			ProcessingTask processingTask = task;
			processingTask.LogDetails = processingTask.LogDetails + "\nКонвертация завершена: " + expectedFile;
			task.Progress = 100.0;
			task.Status = "Успешно";
		});
	}

	public void ExtractNcaSection(Nca nca, NcaSectionType sectionType, string outDir, CancellationToken token)
	{
		if (!nca.CanOpenSection(sectionType))
		{
			return;
		}
		Directory.CreateDirectory(outDir);
		try
		{
			IFileSystem fileSystem = null;
			if (nca.Header.GetFsHeader((int)sectionType).FormatType == NcaFormatType.Romfs)
			{
				IStorage baseStorage = nca.OpenStorage((int)sectionType, IntegrityCheckLevel.None);
				UnalignedStorageWrapper unalignedStorageWrapper = new UnalignedStorageWrapper(baseStorage);
				Type type = AppDomain.CurrentDomain.GetAssemblies().SelectMany((Assembly a) => a.GetTypes()).FirstOrDefault((Type t) => t.Name.Equals("RomFsFileSystem", StringComparison.OrdinalIgnoreCase) || t.Name.Equals("RomfsFileSystem", StringComparison.OrdinalIgnoreCase));
				if (type != null)
				{
					try
					{
						fileSystem = (IFileSystem)Activator.CreateInstance(type, unalignedStorageWrapper);
					}
					catch
					{
						fileSystem = nca.OpenFileSystem((int)sectionType, IntegrityCheckLevel.IgnoreOnInvalid);
					}
				}
				else
				{
					fileSystem = nca.OpenFileSystem((int)sectionType, IntegrityCheckLevel.IgnoreOnInvalid);
				}
			}
			else
			{
				try
				{
					IStorage baseStorage2 = nca.OpenStorage((int)sectionType, IntegrityCheckLevel.None);
					UnalignedStorageWrapper storage = new UnalignedStorageWrapper(baseStorage2);
					PartitionFileSystem partitionFileSystem = new PartitionFileSystem(storage);
					fileSystem = partitionFileSystem;
				}
				catch
				{
					fileSystem = nca.OpenFileSystem((int)sectionType, IntegrityCheckLevel.IgnoreOnInvalid);
				}
			}
			if (fileSystem != null)
			{
				try
				{
					ExtractDirectoryRecursively(fileSystem, "/", outDir, token);
					return;
				}
				finally
				{
					((IDisposable)fileSystem)?.Dispose();
				}
			}
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			Exception ex3 = ex2;
			App.MainDispatcher?.TryEnqueue(delegate
			{
				ProcessingTask processingTask = App.TasksVM.Tasks.FirstOrDefault((ProcessingTask t) => t.IsRunning);
				if (processingTask != null)
				{
					processingTask.LogDetails += $"\nОшибка при извлечении секции {sectionType} из NCA: {ex3.Message}";
				}
			});
			throw new Exception($"Ошибка при извлечении секции {sectionType}: {ex3.Message}", ex3);
		}
	}

	public void ExtractNcaSection(Nca baseNca, Nca patchNca, NcaSectionType sectionType, string outDir, CancellationToken token)
	{
		if (!baseNca.CanOpenSection(sectionType))
		{
			return;
		}
		Directory.CreateDirectory(outDir);
		try
		{
			IFileSystem fileSystem = null;
			if (baseNca.Header.GetFsHeader((int)sectionType).FormatType == NcaFormatType.Romfs)
			{
				IStorage baseStorage = baseNca.OpenStorageWithPatch(patchNca, (int)sectionType, IntegrityCheckLevel.None);
				UnalignedStorageWrapper unalignedStorageWrapper = new UnalignedStorageWrapper(baseStorage);
				Type type = AppDomain.CurrentDomain.GetAssemblies().SelectMany((Assembly a) => a.GetTypes()).FirstOrDefault((Type t) => t.Name.Equals("RomFsFileSystem", StringComparison.OrdinalIgnoreCase) || t.Name.Equals("RomfsFileSystem", StringComparison.OrdinalIgnoreCase));
				if (type != null)
				{
					try
					{
						fileSystem = (IFileSystem)Activator.CreateInstance(type, unalignedStorageWrapper);
					}
					catch
					{
						fileSystem = baseNca.OpenFileSystemWithPatch(patchNca, sectionType, IntegrityCheckLevel.IgnoreOnInvalid);
					}
				}
				else
				{
					fileSystem = baseNca.OpenFileSystemWithPatch(patchNca, sectionType, IntegrityCheckLevel.IgnoreOnInvalid);
				}
			}
			else
			{
				try
				{
					IStorage baseStorage2 = baseNca.OpenStorageWithPatch(patchNca, (int)sectionType, IntegrityCheckLevel.None);
					UnalignedStorageWrapper storage = new UnalignedStorageWrapper(baseStorage2);
					PartitionFileSystem partitionFileSystem = new PartitionFileSystem(storage);
					fileSystem = partitionFileSystem;
				}
				catch
				{
					fileSystem = baseNca.OpenFileSystemWithPatch(patchNca, sectionType, IntegrityCheckLevel.IgnoreOnInvalid);
				}
			}
			if (fileSystem != null)
			{
				try
				{
					ExtractDirectoryRecursively(fileSystem, "/", outDir, token);
					return;
				}
				finally
				{
					((IDisposable)fileSystem)?.Dispose();
				}
			}
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			Exception ex3 = ex2;
			App.MainDispatcher?.TryEnqueue(delegate
			{
				ProcessingTask processingTask = App.TasksVM.Tasks.FirstOrDefault((ProcessingTask t) => t.IsRunning);
				if (processingTask != null)
				{
					processingTask.LogDetails += $"\nОшибка при извлечении патченной секции {sectionType} из NCA: {ex3.Message}";
				}
			});
			throw new Exception($"Ошибка при извлечении патченной секции {sectionType}: {ex3.Message}", ex3);
		}
	}

	public void TrimLanguages(string romfsDir)
	{
		try
		{
			List<string> source = App.Settings.Current.KeepLanguages ?? new List<string> { "ru", "ru-RU", "en-US", "en-GB", "en" };
			string[] array = new string[7] { "Message", "Voice", "Sound", "Localized", "Loc", "Text", "ui" };
			string[] array2 = array;
			foreach (string path in array2)
			{
				string path2 = System.IO.Path.Combine(romfsDir, path);
				if (!Directory.Exists(path2))
				{
					continue;
				}
				string[] directories = Directory.GetDirectories(path2);
				string[] array3 = directories;
				foreach (string path3 in array3)
				{
					string fileName = System.IO.Path.GetFileName(path3);
					if (fileName.Contains("-") && fileName.Length >= 4 && fileName.Length <= 5)
					{
						if (!source.Contains<string>(fileName, StringComparer.OrdinalIgnoreCase))
						{
							SafeDeleteDirectory(path3);
						}
					}
					else if (fileName.Equals("Japanese", StringComparison.OrdinalIgnoreCase) || fileName.Equals("Spanish", StringComparison.OrdinalIgnoreCase) || fileName.Equals("French", StringComparison.OrdinalIgnoreCase) || fileName.Equals("German", StringComparison.OrdinalIgnoreCase) || fileName.Equals("Italian", StringComparison.OrdinalIgnoreCase) || fileName.Equals("Dutch", StringComparison.OrdinalIgnoreCase) || fileName.Equals("Portuguese", StringComparison.OrdinalIgnoreCase) || fileName.Equals("Korean", StringComparison.OrdinalIgnoreCase) || fileName.Equals("Chinese", StringComparison.OrdinalIgnoreCase) || fileName.Equals("es-ES", StringComparison.OrdinalIgnoreCase) || fileName.Equals("es-MX", StringComparison.OrdinalIgnoreCase) || fileName.Equals("fr-FR", StringComparison.OrdinalIgnoreCase) || fileName.Equals("fr-CA", StringComparison.OrdinalIgnoreCase) || fileName.Equals("it-IT", StringComparison.OrdinalIgnoreCase) || fileName.Equals("de-DE", StringComparison.OrdinalIgnoreCase) || fileName.Equals("nl-NL", StringComparison.OrdinalIgnoreCase) || fileName.Equals("pt-BR", StringComparison.OrdinalIgnoreCase) || fileName.Equals("pt-PT", StringComparison.OrdinalIgnoreCase) || fileName.Equals("ja-JP", StringComparison.OrdinalIgnoreCase) || fileName.Equals("ko-KR", StringComparison.OrdinalIgnoreCase) || fileName.Equals("zh-CN", StringComparison.OrdinalIgnoreCase) || fileName.Equals("zh-TW", StringComparison.OrdinalIgnoreCase) || fileName.Equals("zh-Hant", StringComparison.OrdinalIgnoreCase) || fileName.Equals("zh-Hans", StringComparison.OrdinalIgnoreCase))
					{
						SafeDeleteDirectory(path3);
					}
				}
			}
		}
		catch
		{
		}
	}

	private void SafeDeleteDirectory(string path)
	{
		if (!Directory.Exists(path))
		{
			return;
		}
		for (int i = 0; i < 5; i++)
		{
			try
			{
				Directory.Delete(path, recursive: true);
				break;
			}
			catch (IOException)
			{
				Thread.Sleep(100);
			}
			catch (UnauthorizedAccessException)
			{
				Thread.Sleep(100);
			}
			catch
			{
				break;
			}
		}
	}

	private void ExtractDirectoryRecursively(IFileSystem fs, string currentPath, string outDir, CancellationToken token)
	{
		foreach (DirectoryEntryEx item in fs.EnumerateEntries(currentPath, "*"))
		{
			token.ThrowIfCancellationRequested();
			string fullPath = item.FullPath;
			string text = fullPath.TrimStart('/');
			char[] invalidFileNameChars = System.IO.Path.GetInvalidFileNameChars();
			foreach (char c in invalidFileNameChars)
			{
				if (c != '/' && c != '\\')
				{
					text = text.Replace(c, '_');
				}
			}
			string path = System.IO.Path.Combine(outDir, text);
			try
			{
				if (item.Type == DirectoryEntryType.Directory)
				{
					Directory.CreateDirectory(path);
					ExtractDirectoryRecursively(fs, fullPath, outDir, token);
					continue;
				}
				Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
				using UniqueRef<IFile> uniqueRef = default(UniqueRef<IFile>);
				using LibHac.Fs.Path path2 = new LibHac.Fs.Path();
				path2.Initialize(new U8Span(Encoding.UTF8.GetBytes(fullPath))).ThrowIfFailure();
				fs.OpenFile(ref uniqueRef.Ref, in path2, OpenMode.Read).ThrowIfFailure();
				using IFile file = uniqueRef.Release();
				using FileStream destination = File.Create(path);
				file.AsStream().CopyTo(destination);
			}
			catch (Exception ex)
			{
				App.Logger.Log("Ошибка извлечения файла " + fullPath + ": " + ex.Message, LogLevel.Warning);
			}
		}
	}

	private void SaveStorageToFile(IStorage storage, string path)
	{
		try
		{
			using FileStream destination = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 16777216);
			storage.AsStream().CopyTo(destination);
		}
		catch (Exception ex)
		{
			App.Logger.Log("Ошибка сохранения " + System.IO.Path.GetFileName(path) + ": " + ex.Message, LogLevel.Warning);
		}
	}

	public async Task PackContainerAsync(ProcessingTask task, string inputFolder, string outFolder, string outFileName, CancellationToken cancellationToken)
	{
		try
		{
			App.MainDispatcher?.TryEnqueue(delegate
			{
				task.LogDetails = "Анализ директории: " + inputFolder;
				task.Status = "Сборка...";
			});
			Directory.CreateDirectory(outFolder);
			string outPath = System.IO.Path.Combine(outFolder, string.IsNullOrEmpty(outFileName) ? "Packed.nsp" : outFileName);
			if (!outPath.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase))
			{
				outPath += ".nsp";
			}
			string romfsDir = System.IO.Path.Combine(inputFolder, "romfs");
			string exefsDir = System.IO.Path.Combine(inputFolder, "exefs");
			if (!Directory.Exists(romfsDir))
			{
				string[] rDirs = Directory.GetDirectories(inputFolder, "romfs", SearchOption.AllDirectories);
				if (rDirs.Length != 0)
				{
					romfsDir = rDirs[0];
				}
			}
			if (!Directory.Exists(exefsDir))
			{
				string[] eDirs = Directory.GetDirectories(inputFolder, "exefs", SearchOption.AllDirectories);
				if (eDirs.Length != 0)
				{
					exefsDir = eDirs[0];
				}
			}
			if (Directory.Exists(romfsDir) || Directory.Exists(exefsDir))
			{
				App.MainDispatcher?.TryEnqueue(delegate
				{
					task.LogDetails = "Обнаружены распакованные romfs/exefs. Запуск нативной сборки NSP...";
				});
				string controlNca = "";
				string baseProgramNca = "";
				string titleId = "";
				string[] allNcas = Directory.GetFiles(inputFolder, "*.nca", SearchOption.AllDirectories);
				string[] array = allNcas;
				foreach (string ncaPath in array)
				{
					try
					{
						using FileStream fs = new FileStream(ncaPath, FileMode.Open, FileAccess.Read, FileShare.Read);
						Nca nca = new Nca(_keysService.CurrentKeyset, fs.AsStorage());
						if (nca.Header.ContentType == NcaContentType.Control && string.IsNullOrEmpty(controlNca))
						{
							controlNca = ncaPath;
							titleId = ExtractTitleIdFromControlNca(ncaPath);
						}
						else if (nca.Header.ContentType == NcaContentType.Program && string.IsNullOrEmpty(baseProgramNca))
						{
							baseProgramNca = ncaPath;
						}
					}
					catch
					{
					}
				}
				if (string.IsNullOrEmpty(controlNca))
				{
					throw new Exception("Не найден control.nca. Упаковка romfs/exefs невозможна.");
				}
				if (string.IsNullOrEmpty(baseProgramNca))
				{
					throw new Exception("Не найден базовый Program NCA. Упаковка romfs/exefs невозможна.");
				}
				if (string.IsNullOrEmpty(titleId))
				{
					titleId = "0100000000000000";
				}
				string tempOut = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "StormPack_" + Guid.NewGuid().ToString("N").Substring(0, 8));
				Directory.CreateDirectory(tempOut);
				try
				{
					string nativeOutPath = await App.NativePack.PackHybridNspAsync(task, titleId, baseProgramNca, romfsDir, exefsDir, allNcas, tempOut, cancellationToken);
					if (!File.Exists(nativeOutPath))
					{
						throw new Exception("Нативная сборка не создала ожидаемый файл .nsp");
					}
					if (File.Exists(outPath))
					{
						File.Delete(outPath);
					}
					File.Move(nativeOutPath, outPath);
				}
				finally
				{
					try
					{
						Directory.Delete(tempOut, recursive: true);
					}
					catch
					{
					}
				}
			}
			else
			{
				App.MainDispatcher?.TryEnqueue(delegate
				{
					task.LogDetails = "Сборка PFS0 из готовых NCA файлов...";
				});
				PartitionFileSystemBuilder pfsBuilder = new PartitionFileSystemBuilder();
				await Task.Run(delegate
				{
					string[] files = Directory.GetFiles(inputFolder, "*.*", SearchOption.TopDirectoryOnly);
					string[] array2 = files;
					foreach (string path in array2)
					{
						string text = System.IO.Path.GetExtension(path).ToLower();
						if (text == ".nca" || text == ".tik" || text == ".cert")
						{
							IStorage baseStorage = new FileStream(path, FileMode.Open, FileAccess.Read).AsStorage();
							pfsBuilder.AddFile(System.IO.Path.GetFileName(path), new StorageFile(baseStorage, OpenMode.Read));
						}
					}
					using FileStream destination = new FileStream(outPath, FileMode.Create, FileAccess.ReadWrite);
					IStorage storage = pfsBuilder.Build(PartitionFileSystemType.Standard);
					storage.AsStream().CopyTo(destination);
				}, cancellationToken);
			}
			App.MainDispatcher?.TryEnqueue(delegate
			{
				task.Progress = 100.0;
				ProcessingTask processingTask = task;
				processingTask.LogDetails = processingTask.LogDetails + "\nСборка завершена!\nФайл: " + outPath;
			});
			App.Logger.Log("Контейнер успешно собран: " + System.IO.Path.GetFileName(outPath), LogLevel.Success);
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			App.Logger.Log("Ошибка сборки контейнера: " + ex2.Message, LogLevel.Error);
			throw;
		}
	}

	private string ExtractTitleIdFromControlNca(string ncaPath)
	{
		try
		{
			Nca nca = new Nca(_keysService.CurrentKeyset, new FileStream(ncaPath, FileMode.Open, FileAccess.Read).AsStorage());
			IFileSystem fileSystem = nca.OpenFileSystem(0, IntegrityCheckLevel.IgnoreOnInvalid);
			using IEnumerator<DirectoryEntryEx> enumerator = fileSystem.EnumerateEntries("/", "*.cnmt").GetEnumerator();
			if (enumerator.MoveNext())
			{
				DirectoryEntryEx current = enumerator.Current;
				using UniqueRef<IFile> uniqueRef = default(UniqueRef<IFile>);
				using LibHac.Fs.Path path = new LibHac.Fs.Path();
				path.Initialize(new U8Span(Encoding.UTF8.GetBytes(current.FullPath))).ThrowIfFailure();
				fileSystem.OpenFile(ref uniqueRef.Ref, in path, OpenMode.Read).ThrowIfFailure();
				using MemoryStream memoryStream = new MemoryStream();
				uniqueRef.Release().AsStream().CopyTo(memoryStream);
				byte[] value = memoryStream.ToArray();
				return BitConverter.ToUInt64(value, 0).ToString("X16").ToLower();
			}
		}
		catch
		{
		}
		return "";
	}

	public async Task VerifyNspAsync(ProcessingTask task, string filePath, CancellationToken cancellationToken)
	{
		try
		{
			App.MainDispatcher?.TryEnqueue(delegate
			{
				task.Status = "Проверка...";
				task.LogDetails = "Начало верификации файла: " + System.IO.Path.GetFileName(filePath);
			});
			if (!_keysService.IsLoaded)
			{
				throw new Exception("Отсутствуют криптографические ключи для верификации.");
			}
			string verifyType = "Неизвестно";
			string structureStatus = "Ок";
			string titleId = "UNKNOWN";
			string version = "v0";
			string mergedStatus = "Да";
			bool hasBaseProgram = false;
			bool hasUpdateProgram = false;
			bool hasControl = false;
			bool hasDlc = false;
			bool hasCode = false;
			bool hasData = false;
			StringBuilder sbDetails = new StringBuilder();
			StringBuilder stringBuilder = sbDetails;
			StringBuilder.AppendInterpolatedStringHandler handler = new StringBuilder.AppendInterpolatedStringHandler(11, 1, stringBuilder);
			handler.AppendLiteral("[Проверка] ");
			handler.AppendFormatted(System.IO.Path.GetFileName(filePath));
			stringBuilder.AppendLine(ref handler);
			await Task.Run(delegate
			{
				using FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
				IStorage storage = stream.AsStorage();
				PartitionFileSystem partitionFileSystem = new PartitionFileSystem(storage);
				Dictionary<string, byte[]> dictionary = new Dictionary<string, byte[]>();
				foreach (DirectoryEntryEx item in from e in partitionFileSystem.EnumerateEntries()
					where e.Name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase)
					select e)
				{
					try
					{
						using UniqueRef<IFile> uniqueRef = default(UniqueRef<IFile>);
						using LibHac.Fs.Path path = new LibHac.Fs.Path();
						path.Initialize(new U8Span(Encoding.UTF8.GetBytes(item.FullPath))).ThrowIfFailure();
						partitionFileSystem.OpenFile(ref uniqueRef.Ref, in path, OpenMode.Read).ThrowIfFailure();
						IFile file = uniqueRef.Release();
						using MemoryStream memoryStream = new MemoryStream();
						file.AsStream().CopyTo(memoryStream);
						memoryStream.Position = 0L;
						Ticket ticket = new Ticket(memoryStream);
						byte[] titleKey = ticket.GetTitleKey(_keysService.CurrentKeyset);
						string key = BitConverter.ToString(ticket.RightsId).Replace("-", "").ToLowerInvariant();
						dictionary[key] = titleKey;
					}
					catch
					{
					}
				}
				List<DirectoryEntryEx> list = partitionFileSystem.EnumerateEntries().ToList();
				long totalSize = list.Sum((DirectoryEntryEx e) => e.Size);
				long processedSize = 0L;
				StringBuilder stringBuilder2 = sbDetails;
				StringBuilder stringBuilder3 = stringBuilder2;
				StringBuilder.AppendInterpolatedStringHandler handler2 = new StringBuilder.AppendInterpolatedStringHandler(39, 1, stringBuilder2);
				handler2.AppendLiteral("[PFS0] Обнаружено файлов в контейнере: ");
				handler2.AppendFormatted(list.Count);
				stringBuilder3.AppendLine(ref handler2);
				foreach (DirectoryEntryEx item2 in list)
				{
					cancellationToken.ThrowIfCancellationRequested();
					string entryName = item2.Name;
					if (entryName.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) || entryName.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase))
					{
						App.MainDispatcher?.TryEnqueue(delegate
						{
							task.LogDetails = $"[{processedSize * 100 / totalSize}%] Чтение блока: {entryName}";
						});
						using UniqueRef<IFile> uniqueRef2 = default(UniqueRef<IFile>);
						using LibHac.Fs.Path path2 = new LibHac.Fs.Path();
						path2.Initialize(new U8Span(Encoding.UTF8.GetBytes(item2.FullPath))).ThrowIfFailure();
						partitionFileSystem.OpenFile(ref uniqueRef2.Ref, in path2, OpenMode.Read).ThrowIfFailure();
						IStorage storage2 = uniqueRef2.Release().AsStorage();
						if (entryName.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase))
						{
							storage2 = new StormNczStorage(storage2, dictionary);
						}
						try
						{
							Nca nca = new Nca(_keysService.CurrentKeyset, storage2);
							string text = (titleId = nca.Header.TitleId.ToString("X16").ToUpper());
							string value = nca.Header.ContentType.ToString();
							stringBuilder2 = sbDetails;
							StringBuilder stringBuilder4 = stringBuilder2;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(31, 3, stringBuilder2);
							handler2.AppendLiteral("  - Блок: ");
							handler2.AppendFormatted(entryName);
							handler2.AppendLiteral(" | Тип: ");
							handler2.AppendFormatted(value);
							handler2.AppendLiteral(" | Title ID: ");
							handler2.AppendFormatted(text);
							stringBuilder4.AppendLine(ref handler2);
							if (nca.Header.ContentType == NcaContentType.Program)
							{
								bool flag = nca.CanOpenSection(NcaSectionType.Code);
								bool flag2 = nca.CanOpenSection(NcaSectionType.Data);
								if (flag)
								{
									hasCode = true;
								}
								if (flag2)
								{
									hasData = true;
								}
								stringBuilder2 = sbDetails;
								StringBuilder stringBuilder5 = stringBuilder2;
								handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 1, stringBuilder2);
								handler2.AppendLiteral("    * Секция Code (ExeFS): ");
								handler2.AppendFormatted(flag ? "Есть" : "Отсутствует");
								stringBuilder5.AppendLine(ref handler2);
								stringBuilder2 = sbDetails;
								StringBuilder stringBuilder6 = stringBuilder2;
								handler2 = new StringBuilder.AppendInterpolatedStringHandler(27, 1, stringBuilder2);
								handler2.AppendLiteral("    * Секция Data (RomFS): ");
								handler2.AppendFormatted(flag2 ? "Есть" : "Отсутствует");
								stringBuilder6.AppendLine(ref handler2);
								if (text.EndsWith("000"))
								{
									hasBaseProgram = true;
								}
								else
								{
									hasUpdateProgram = true;
								}
							}
							else if (nca.Header.ContentType == NcaContentType.Control)
							{
								hasControl = true;
								try
								{
									IFileSystem fileSystem = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);
									using UniqueRef<IFile> uniqueRef3 = default(UniqueRef<IFile>);
									using LibHac.Fs.Path path3 = new LibHac.Fs.Path();
									path3.Initialize(new U8Span(Encoding.UTF8.GetBytes("/control.nacp"))).ThrowIfFailure();
									if (fileSystem.OpenFile(ref uniqueRef3.Ref, in path3, OpenMode.Read).IsSuccess())
									{
										byte[] array = new byte[16384];
										uniqueRef3.Release().AsStorage().Read(0L, array)
											.ThrowIfFailure();
										string text2 = Encoding.UTF8.GetString(array, 12384, 16).Trim('\0', ' ', '\r', '\n');
										if (!string.IsNullOrEmpty(text2))
										{
											version = text2;
										}
									}
								}
								catch
								{
								}
							}
							else if (nca.Header.ContentType == NcaContentType.PublicData)
							{
								hasDlc = true;
							}
						}
						catch (Exception ex4)
						{
							stringBuilder2 = sbDetails;
							StringBuilder stringBuilder7 = stringBuilder2;
							handler2 = new StringBuilder.AppendInterpolatedStringHandler(60, 2, stringBuilder2);
							handler2.AppendLiteral("  - [Ошибка заголовка NCA] Блок: ");
							handler2.AppendFormatted(entryName);
							handler2.AppendLiteral(". Не удалось расшифровать: ");
							handler2.AppendFormatted(ex4.Message);
							stringBuilder7.AppendLine(ref handler2);
						}
						processedSize += item2.Size;
					}
					else
					{
						stringBuilder2 = sbDetails;
						StringBuilder stringBuilder8 = stringBuilder2;
						handler2 = new StringBuilder.AppendInterpolatedStringHandler(13, 2, stringBuilder2);
						handler2.AppendLiteral("  - Файл: ");
						handler2.AppendFormatted(entryName);
						handler2.AppendLiteral(" (");
						handler2.AppendFormatted(ProcessingTask.FormatSize(item2.Size));
						handler2.AppendLiteral(")");
						stringBuilder8.AppendLine(ref handler2);
						processedSize += item2.Size;
					}
				}
				if (hasBaseProgram)
				{
					if (hasUpdateProgram)
					{
						verifyType = "Сшитый Гибрид";
					}
					else
					{
						verifyType = "Базовая игра";
					}
				}
				else if (hasUpdateProgram)
				{
					verifyType = "Обновление";
				}
				else if (hasDlc)
				{
					verifyType = "DLC";
				}
				else
				{
					verifyType = "Неизвестно";
				}
				sbDetails.AppendLine();
				sbDetails.AppendLine("=== АНАЛИЗ СТРУКТУРЫ И СОВМЕСТИМОСТИ ===");
				stringBuilder2 = sbDetails;
				StringBuilder stringBuilder9 = stringBuilder2;
				handler2 = new StringBuilder.AppendInterpolatedStringHandler(18, 1, stringBuilder2);
				handler2.AppendLiteral("Определенный тип: ");
				handler2.AppendFormatted(verifyType);
				stringBuilder9.AppendLine(ref handler2);
				stringBuilder2 = sbDetails;
				StringBuilder stringBuilder10 = stringBuilder2;
				handler2 = new StringBuilder.AppendInterpolatedStringHandler(13, 1, stringBuilder2);
				handler2.AppendLiteral("Версия игры: ");
				handler2.AppendFormatted(version);
				stringBuilder10.AppendLine(ref handler2);
				stringBuilder2 = sbDetails;
				StringBuilder stringBuilder11 = stringBuilder2;
				handler2 = new StringBuilder.AppendInterpolatedStringHandler(33, 1, stringBuilder2);
				handler2.AppendLiteral("Наличие исполняемого кода ExeFS: ");
				handler2.AppendFormatted(hasCode ? "Да (Имеется)" : "Нет");
				stringBuilder11.AppendLine(ref handler2);
				stringBuilder2 = sbDetails;
				StringBuilder stringBuilder12 = stringBuilder2;
				handler2 = new StringBuilder.AppendInterpolatedStringHandler(24, 1, stringBuilder2);
				handler2.AppendLiteral("Наличие ресурсов RomFS: ");
				handler2.AppendFormatted(hasData ? "Да (Имеется)" : "Нет");
				stringBuilder12.AppendLine(ref handler2);
				stringBuilder2 = sbDetails;
				StringBuilder stringBuilder13 = stringBuilder2;
				handler2 = new StringBuilder.AppendInterpolatedStringHandler(28, 1, stringBuilder2);
				handler2.AppendLiteral("Наличие метаданных Control: ");
				handler2.AppendFormatted(hasControl ? "Да (Имеется)" : "Нет");
				stringBuilder13.AppendLine(ref handler2);
				if (verifyType == "Обновление")
				{
					structureStatus = "Только патч (ошибка запуска)";
					mergedStatus = "Нет (нужна база)";
					sbDetails.AppendLine();
					sbDetails.AppendLine("❌ ОШИБКА: Этот файл является изолированным файлом обновления (Update) и не содержит исполняемого ядра (ExeFS) базовой игры.");
					sbDetails.AppendLine("\ud83d\udc49 Эмуляторы выдадут ошибку 0007-001A (ErrorNoExeFS) при попытке запустить этот файл напрямую.");
					sbDetails.AppendLine("\ud83d\udc49 Для запуска объедините (сшейте) этот патч с базовой игрой во вкладках 'Обновление' или 'Мульти-контент'.");
				}
				else if (verifyType == "Базовая игра" || verifyType == "Сшитый Гибрид")
				{
					if (hasCode && hasData && hasControl)
					{
						structureStatus = "Корректна";
						mergedStatus = ((verifyType == "Сшитый Гибрид") ? "Да (Гибрид)" : "Да");
						sbDetails.AppendLine();
						sbDetails.AppendLine("✅ УСПЕХ: Файл полностью готов к запуску на эмуляторах yuzu/Ryujinx/консоли! Структура NCA правильная, исполняемый код и метаданные на месте.");
					}
					else
					{
						structureStatus = "Ошибка структуры";
						mergedStatus = "Нет (ошибка ExeFS)";
						sbDetails.AppendLine();
						sbDetails.AppendLine("❌ ОШИБКА: Структура файла некорректна! Отсутствует исполняемый код (ExeFS), метаданные (Control) или RomFS.");
						sbDetails.AppendLine("\ud83d\udc49 Убедитесь, что исходные дампы не были повреждены.");
					}
				}
				else if (verifyType == "DLC")
				{
					structureStatus = "Корректна";
					mergedStatus = "Не применимо";
					sbDetails.AppendLine();
					sbDetails.AppendLine("ℹ\ufe0f ИНФОРМАЦИЯ: Файл является DLC (дополнением). Дополнения устанавливаются в эмулятор отдельно и не запускаются напрямую.");
				}
				else
				{
					structureStatus = "Неизвестно";
					mergedStatus = "Ошибка структуры";
					sbDetails.AppendLine();
					sbDetails.AppendLine("⚠\ufe0f ПРЕДУПРЕЖДЕНИЕ: Не удалось корректно распознать структуру Nintendo Switch NCA в файле.");
				}
			}, cancellationToken);
			App.MainDispatcher?.TryEnqueue(delegate
			{
				task.VerifyType = verifyType;
				task.VerifyStructure = structureStatus;
				task.VerifyTitleId = titleId;
				task.VerifyVersion = version;
				task.VerifyMergedStatus = mergedStatus;
				task.Status = ((structureStatus.Contains("ошибка") || structureStatus.Contains("Ошибка") || structureStatus.Contains("Отсутствует")) ? "Ошибка" : "Успешно");
				task.Progress = 100.0;
				task.LogDetails = sbDetails.ToString();
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
				processingTask.LogDetails = processingTask.LogDetails + "\n[ОШИБКА] Проверка провалена: " + ex3.Message;
			});
			throw;
		}
	}

	private static IFile OpenFileSafe(IFileSystem fsToOpen, string pth)
	{
		using UniqueRef<IFile> uniqueRef = default(UniqueRef<IFile>);
		using LibHac.Fs.Path path = new LibHac.Fs.Path();
		path.Initialize(new U8Span(Encoding.UTF8.GetBytes(pth))).ThrowIfFailure();
		fsToOpen.OpenFile(ref uniqueRef.Ref, in path, OpenMode.Read).ThrowIfFailure();
		return uniqueRef.Release();
	}
}
