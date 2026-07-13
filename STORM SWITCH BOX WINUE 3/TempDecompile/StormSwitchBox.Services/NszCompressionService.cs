using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

public class NszCompressionService
{
	private readonly SwitchFormatService _formatService;

	public NszCompressionService(SwitchFormatService formatService)
	{
		_formatService = formatService;
	}

	public async Task CompressToNszAsync(ProcessingTask task, string inputPath, string outDir, CancellationToken cancellationToken)
	{
		FileStream fileStream = null;
		List<FileStream> tempStreams = new List<FileStream>();
		List<IFile> openedFiles = new List<IFile>();
		string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "StormCompress_" + Guid.NewGuid().ToString("N").Substring(0, 8));
		try
		{
			App.MainDispatcher?.TryEnqueue(delegate
			{
				task.Status = "Подготовка...";
				task.IsRunning = true;
				task.Progress = 0.0;
			});
			string fileName = System.IO.Path.GetFileNameWithoutExtension(inputPath);
			bool isXci = inputPath.EndsWith(".xci", StringComparison.OrdinalIgnoreCase);
			string expectedExt = (isXci ? ".xcz" : ".nsz");
			string outNszPath = System.IO.Path.Combine(outDir, fileName + expectedExt);
			App.Logger.Log("[NSZ Engine] Запуск нативного сжатия NSZ: " + fileName);
			long totalBytes = new FileInfo(inputPath).Length;
			App.MainDispatcher?.TryEnqueue(delegate
			{
				if (task.SourceSizeBytes <= 0)
				{
					task.SourceSizeBytes = totalBytes;
				}
				task.LogDetails = $"Входной файл: {System.IO.Path.GetFileName(inputPath)}\nОригинальный размер: {ProcessingTask.FormatSize(totalBytes)}\nНативное сжатие Zstd...";
				task.Status = "Сжатие NSZ...";
			});
			Directory.CreateDirectory(tempDir);
			fileStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
			IStorage storage = fileStream.AsStorage();
			PartitionFileSystem pfs = CreatePfsFromStorage(storage, isXci);
			PartitionFileSystemBuilder pfsBuilder = new PartitionFileSystemBuilder();
			int level = App.Settings.Current.CompressionLevel;
			if (level < 1)
			{
				level = 18;
			}
			if (level > 22)
			{
				level = 22;
			}
			List<DirectoryEntryEx> entries = pfs.EnumerateEntries().ToList();
			int totalEntries = entries.Count;
			int entryIdx = 0;
			foreach (DirectoryEntryEx entry in entries)
			{
				cancellationToken.ThrowIfCancellationRequested();
				int num = entryIdx;
				entryIdx = num + 1;
				if (entry.Type == DirectoryEntryType.Directory)
				{
					continue;
				}
				string entryName = entry.Name;
				bool isNca = entryName.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) && !entryName.EndsWith(".cnmt.nca", StringComparison.OrdinalIgnoreCase);
				bool shouldCompress = false;
				if (isNca)
				{
					try
					{
						using IFile entryFile = OpenFileSafe(pfs, entry.FullPath);
						Nca nca = new Nca(storage: entryFile.AsStorage(), keySet: App.Keys.CurrentKeyset);
						if (nca.Header.ContentType != NcaContentType.Control && nca.Header.ContentType != NcaContentType.Meta)
						{
							shouldCompress = true;
						}
						else
						{
							App.Logger.Log($"[NSZ Engine] Пропускаем сжатие для NCA типа {nca.Header.ContentType}: {entryName}");
						}
					}
					catch (Exception ex)
					{
						Exception ex2 = ex;
						App.Logger.Log($"[NSZ Engine] Не удалось прочитать NCA заголовок для {entryName}: {ex2.Message}. NCA будет сжат по умолчанию.", LogLevel.Warning);
						shouldCompress = true;
					}
				}
				if (isNca && shouldCompress)
				{
					string nczName = System.IO.Path.ChangeExtension(entryName, ".ncz");
					string tempNczPath = System.IO.Path.Combine(tempDir, nczName);
					App.MainDispatcher?.TryEnqueue(delegate
					{
						task.Status = "Сжатие " + entryName + "...";
						task.LogDetails += $"\n[{entryIdx}/{totalEntries}] Сжатие {entryName} -> {nczName}";
					});
					using (IFile entryFile2 = OpenFileSafe(pfs, entry.FullPath))
					{
						IStorage entryStorage = entryFile2.AsStorage();
						StormNczCompressor.CompressNcaToNcz(entryStorage, tempNczPath, level, App.Keys.CurrentKeyset, task, cancellationToken);
					}
					FileStream fs = new FileStream(tempNczPath, FileMode.Open, FileAccess.Read, FileShare.Read);
					tempStreams.Add(fs);
					pfsBuilder.AddFile(nczName, new StorageFile(new SafeStorageWrapper(fs.AsStorage()), OpenMode.Read));
				}
				else
				{
					App.MainDispatcher?.TryEnqueue(delegate
					{
						task.LogDetails += $"\n[{entryIdx}/{totalEntries}] Копирование метаданных: {entryName}";
					});
					IFile entryFile3 = OpenFileSafe(pfs, entry.FullPath);
					openedFiles.Add(entryFile3);
					pfsBuilder.AddFile(file: new StorageFile(new SafeStorageWrapper(entryFile3.AsStorage()), OpenMode.Read), filename: entryName);
				}
			}
			if (File.Exists(outNszPath))
			{
				try
				{
					File.Delete(outNszPath);
				}
				catch
				{
				}
			}
			App.MainDispatcher?.TryEnqueue(delegate
			{
				task.Status = "Сборка контейнера...";
				task.LogDetails += "\nЗапись сжатого контейнера на диск...";
			});
			using (IStorage builtPfs = pfsBuilder.Build(PartitionFileSystemType.Standard))
			{
				using FileStream destStream = new FileStream(outNszPath, FileMode.Create, FileAccess.Write, FileShare.None, 16777216);
				builtPfs.GetSize(out var totalPfsSize).ThrowIfFailure();
				long remaining = totalPfsSize;
				long offset = 0L;
				byte[] buffer = new byte[131072];
				while (remaining > 0)
				{
					cancellationToken.ThrowIfCancellationRequested();
					int toRead = (int)Math.Min(buffer.Length, remaining);
					builtPfs.Read(offset, buffer.AsSpan(0, toRead)).ThrowIfFailure();
					destStream.Write(buffer, 0, toRead);
					offset += toRead;
					remaining -= toRead;
					double packProgress = 99.0 + (double)offset / (double)totalPfsSize * 1.0;
					App.MainDispatcher?.TryEnqueue(delegate
					{
						task.Progress = Math.Min(99.9, packProgress);
					});
				}
			}
			long finalSize = new FileInfo(outNszPath).Length;
			double ratio = (double)finalSize / (double)totalBytes * 100.0;
			App.MainDispatcher?.TryEnqueue(delegate
			{
				task.Progress = 100.0;
				task.Status = "Успешно";
				task.IsRunning = false;
				task.LogDetails += $"\nСжатие успешно завершено!\nСжатый размер: {ProcessingTask.FormatSize(finalSize)} ({ratio:F1}% от оригинала)";
				task.TargetSize = ProcessingTask.FormatSize(finalSize);
				long num2 = totalBytes - finalSize;
				double value = (double)num2 / (double)totalBytes * 100.0;
				task.SizeDifference = $"{((num2 > 0) ? "-" : "+")}{ProcessingTask.FormatSize(Math.Abs(num2))} ({Math.Abs(value):F1}%)";
				HistoryService.AddToHistory(task);
			});
			App.Logger.Log($"[NSZ Engine] Сжатие завершено: {fileName}. Экономия: {100.0 - ratio:F1}%", LogLevel.Success);
		}
		catch (OperationCanceledException)
		{
			App.MainDispatcher?.TryEnqueue(delegate
			{
				task.Status = "Отменен";
				task.IsRunning = false;
				HistoryService.AddToHistory(task);
			});
		}
		catch (Exception ex4)
		{
			Exception ex5 = ex4;
			Exception ex6 = ex5;
			App.MainDispatcher?.TryEnqueue(delegate
			{
				task.Status = "Ошибка";
				task.IsRunning = false;
				ProcessingTask processingTask = task;
				processingTask.LogDetails = processingTask.LogDetails + "\nКритическая ошибка сжатия: " + ex6.Message;
				HistoryService.AddToHistory(task);
			});
			App.Logger.Log("[NSZ Engine] Ошибка нативного сжатия: " + ex6.Message, LogLevel.Error);
			throw;
		}
		finally
		{
			if (fileStream != null)
			{
				try
				{
					fileStream.Dispose();
				}
				catch
				{
				}
			}
			foreach (FileStream fs2 in tempStreams)
			{
				try
				{
					fs2.Dispose();
				}
				catch
				{
				}
			}
			foreach (IFile f in openedFiles)
			{
				try
				{
					f.Dispose();
				}
				catch
				{
				}
			}
			try
			{
				if (Directory.Exists(tempDir))
				{
					Directory.Delete(tempDir, recursive: true);
				}
			}
			catch
			{
			}
		}
	}

	public async Task<string?> DecompressNszAsync(ProcessingTask task, string inputPath, string outDir, CancellationToken cancellationToken)
	{
		string fileName = System.IO.Path.GetFileNameWithoutExtension(inputPath);
		string expectedExt = (inputPath.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase) ? ".xci" : ".nsp");
		string outNspPath = System.IO.Path.Combine(outDir, fileName + expectedExt);
		List<IFile> openedFiles = new List<IFile>();
		try
		{
			App.MainDispatcher?.TryEnqueue(delegate
			{
				task.Status = "Распаковка...";
				task.IsRunning = true;
				ProcessingTask processingTask = task;
				processingTask.LogDetails = processingTask.LogDetails + "\nНативная декомпрессия NSZ в памяти (Zero-Disk-IO): " + System.IO.Path.GetFileName(inputPath);
			});
			App.Logger.Log("[NSZ Engine] Нативная распаковка: " + fileName);
			using FileStream fileStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
			IStorage storage = fileStream.AsStorage();
			long pfsOffset = ((expectedExt == ".xci") ? 65536 : 0);
			long sz;
			IStorage pfsStorage = ((pfsOffset > 0) ? new SubStorage(storage, pfsOffset, storage.GetSize(out sz).IsSuccess() ? (sz - pfsOffset) : 0) : storage);
			PartitionFileSystem fileSystem = new PartitionFileSystem(pfsStorage);
			PartitionFileSystemBuilder pfsBuilder = new PartitionFileSystemBuilder();
			int entryIdx = 0;
			List<string> solidFiles = new List<string>();
			List<string> physicalFiles = new List<string>();
			List<DirectoryEntryEx> sortedEntries = fileSystem.EnumerateEntries().ToList();
			foreach (DirectoryEntryEx entry in sortedEntries)
			{
				if (!entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase) && !entry.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase))
				{
					physicalFiles.Add(entry.Name);
					continue;
				}
				using IFile entryFile = OpenFileSafe(fileSystem, entry.FullPath);
				IStorage entryStorage = entryFile.AsStorage();
				if (IsNczMagic(entryStorage) || entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase))
				{
					solidFiles.Add(entry.Name);
				}
				else
				{
					physicalFiles.Add(entry.Name);
				}
			}
			Dictionary<string, byte[]> titleKeyMap = new Dictionary<string, byte[]>();
			foreach (DirectoryEntryEx entry2 in sortedEntries)
			{
				if (!entry2.Name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				try
				{
					using IFile entryFile2 = OpenFileSafe(fileSystem, entry2.FullPath);
					IStorage tikStorage = entryFile2.AsStorage();
					tikStorage.GetSize(out var tikSize).ThrowIfFailure();
					byte[] tikData = new byte[tikSize];
					tikStorage.Read(0L, tikData).ThrowIfFailure();
					using MemoryStream stream = new MemoryStream(tikData);
					Ticket ticket = new Ticket(stream);
					byte[] tKey = ticket.GetTitleKey(App.Keys.CurrentKeyset);
					string rightsIdStr = BitConverter.ToString(ticket.RightsId).Replace("-", "").ToLowerInvariant();
					titleKeyMap[rightsIdStr] = tKey;
					App.MainDispatcher?.TryEnqueue(delegate
					{
						ProcessingTask processingTask = task;
						processingTask.LogDetails = processingTask.LogDetails + "\nУспешно извлечен TitleKey (Zero-Disk-IO) для " + rightsIdStr;
					});
				}
				catch (Exception ex)
				{
					Exception ex2 = ex;
					Exception ex3 = ex2;
					App.MainDispatcher?.TryEnqueue(delegate
					{
						ProcessingTask processingTask = task;
						processingTask.LogDetails = processingTask.LogDetails + "\n[ПРЕДУПРЕЖДЕНИЕ] Ошибка чтения ключа из " + entry2.Name + ": " + ex3.Message;
					});
				}
			}
			IStorage globalSolidStorage = null;
			DirectoryEntryEx solidEntry = sortedEntries.FirstOrDefault((DirectoryEntryEx e) => e.Name.EndsWith(".solid", StringComparison.OrdinalIgnoreCase));
			if (solidEntry != null)
			{
				IFile solidFile = OpenFileSafe(fileSystem, solidEntry.FullPath);
				openedFiles.Add(solidFile);
				globalSolidStorage = solidFile.AsStorage();
			}
			await Task.Run(delegate
			{
				foreach (DirectoryEntryEx item in sortedEntries)
				{
					cancellationToken.ThrowIfCancellationRequested();
					entryIdx++;
					string entryName = item.Name;
					int currentEntry = entryIdx;
					int totalEntries = sortedEntries.Count;
					App.MainDispatcher?.TryEnqueue(delegate
					{
						task.LogDetails += $"\n[{currentEntry}/{totalEntries}] Распаковка в памяти: {entryName}";
					});
					if (solidFiles.Contains(entryName))
					{
						string filename = (entryName.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase) ? System.IO.Path.ChangeExtension(entryName, ".nca") : entryName);
						IFile file = OpenFileSafe(fileSystem, item.FullPath);
						openedFiles.Add(file);
						IStorage baseStorage = file.AsStorage();
						StormNczStorage baseStorage2 = new StormNczStorage(baseStorage, titleKeyMap, globalSolidStorage);
						pfsBuilder.AddFile(filename, new StorageFile(new SafeStorageWrapper(baseStorage2), OpenMode.Read));
					}
					else
					{
						IFile file2 = OpenFileSafe(fileSystem, item.FullPath);
						openedFiles.Add(file2);
						IStorage baseStorage3 = file2.AsStorage();
						pfsBuilder.AddFile(entryName, new StorageFile(new SafeStorageWrapper(baseStorage3), OpenMode.Read));
					}
				}
				App.MainDispatcher?.TryEnqueue(delegate
				{
					task.LogDetails += "\nСборка декомпрессированного контейнера (запись на диск)...";
				});
				using IStorage storage2 = pfsBuilder.Build(PartitionFileSystemType.Standard);
				using FileStream fileStream2 = new FileStream(outNspPath, FileMode.Create, FileAccess.Write, FileShare.None, 16777216);
				storage2.GetSize(out var size).ThrowIfFailure();
				long num = size;
				long num2 = 0L;
				byte[] array = new byte[81920];
				while (num > 0)
				{
					int num3 = (int)Math.Min(array.Length, num);
					storage2.Read(num2, array.AsSpan(0, num3)).ThrowIfFailure();
					fileStream2.Write(array, 0, num3);
					num2 += num3;
					num -= num3;
				}
			}, cancellationToken);
			App.Logger.Log("[NSZ Engine] Нативная распаковка завершена: " + outNspPath, LogLevel.Success);
			if (globalSolidStorage != null)
			{
				try
				{
					((IDisposable)globalSolidStorage).Dispose();
				}
				catch
				{
				}
			}
			return outNspPath;
		}
		catch (OperationCanceledException)
		{
			try
			{
				if (File.Exists(outNspPath))
				{
					File.Delete(outNspPath);
				}
			}
			catch
			{
			}
			return null;
		}
		catch (Exception ex5)
		{
			Exception ex6 = ex5;
			App.Logger.Log("[NSZ Engine] Ошибка нативной распаковки:\n" + ex6.ToString(), LogLevel.Error);
			try
			{
				if (File.Exists(outNspPath))
				{
					File.Delete(outNspPath);
				}
			}
			catch
			{
			}
			return null;
		}
		finally
		{
			foreach (IFile f in openedFiles)
			{
				try
				{
					f.Dispose();
				}
				catch
				{
				}
			}
		}
		static bool IsNczMagic(IStorage fileStorage)
		{
			fileStorage.GetSize(out var size).ThrowIfFailure();
			if (size >= 8)
			{
				try
				{
					byte[] array = new byte[8];
					fileStorage.Read(0L, array);
					string text = Encoding.ASCII.GetString(array);
					DebugLogger.Log($"[IsNczMagic] Size: {size}, m1: {text}");
					if (text == "NCZSECTN" || text == "NCZBLOCK")
					{
						return true;
					}
					if (size >= 16392)
					{
						fileStorage.Read(16384L, array);
						string text2 = Encoding.ASCII.GetString(array);
						DebugLogger.Log($"[IsNczMagic] Size: {size}, m2: {text2}");
						if (text2 == "NCZSECTN" || text2 == "NCZBLOCK")
						{
							return true;
						}
					}
					return false;
				}
				catch (Exception ex7)
				{
					DebugLogger.Log("[IsNczMagic] Exception: " + ex7.Message);
					return false;
				}
			}
			DebugLogger.Log($"[IsNczMagic] Size {size} < 8. Returning false.");
			return false;
		}
	}

	private PartitionFileSystem CreatePfsFromStorage(IStorage storage, bool isXci)
	{
		if (isXci)
		{
			storage.GetSize(out var size).ThrowIfFailure();
			SubStorage storage2 = new SubStorage(storage, 65536L, size - 65536);
			PartitionFileSystem partitionFileSystem = new PartitionFileSystem(storage2);
			using UniqueRef<IFile> uniqueRef = default(UniqueRef<IFile>);
			using LibHac.Fs.Path path = new LibHac.Fs.Path();
			path.Initialize(new U8Span(Encoding.UTF8.GetBytes("/secure"))).ThrowIfFailure();
			partitionFileSystem.OpenFile(ref uniqueRef.Ref, in path, OpenMode.Read).ThrowIfFailure();
			return new PartitionFileSystem(uniqueRef.Release().AsStorage());
		}
		return new PartitionFileSystem(storage);
	}

	private static async Task<bool> HardPatchInternalAsync(string filePath, byte[] patchData)
	{
		return await Task.FromResult(result: true);
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
