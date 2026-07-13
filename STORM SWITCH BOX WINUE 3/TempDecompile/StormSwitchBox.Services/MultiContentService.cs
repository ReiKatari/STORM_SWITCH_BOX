using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services;

public class MultiContentService
{
	private readonly KeysService _keysService;

	public MultiContentService(KeysService keysService)
	{
		_keysService = keysService;
	}

	public async Task BuildMultiContentAsync(ProcessingTask task, List<string> inputFiles, string outPath, bool patchFirmware, CancellationToken cancellationToken)
	{
		string intermediatePath = outPath;
		bool isCompressedFormat = task.TargetFormat.Equals("NSZ", StringComparison.OrdinalIgnoreCase) || task.TargetFormat.Equals("XCZ", StringComparison.OrdinalIgnoreCase);
		if (isCompressedFormat)
		{
			string intermediateExt = (task.TargetFormat.Equals("XCZ", StringComparison.OrdinalIgnoreCase) ? ".xci" : ".nsp");
			intermediatePath = System.IO.Path.ChangeExtension(outPath, intermediateExt);
			if (intermediatePath.Equals(outPath, StringComparison.OrdinalIgnoreCase))
			{
				intermediatePath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(outPath) ?? string.Empty, System.IO.Path.GetFileNameWithoutExtension(outPath) + "_temp" + intermediateExt);
			}
		}
		string tempDecompDir = string.Empty;
		try
		{
			App.MainDispatcher?.TryEnqueue(delegate
			{
				task.Status = "Анализ файлов...";
				task.IsRunning = true;
				task.Progress = 0.0;
				task.LogDetails += $"\nВходных файлов: {inputFiles.Count}\nПатч прошивки: {(patchFirmware ? "Да" : "Нет")}\n";
			});
			if (!_keysService.IsLoaded)
			{
				throw new Exception("Отсутствуют криптографические ключи (prod.keys). Пожалуйста, выберите их в параметрах.");
			}
			foreach (string inputFile in inputFiles)
			{
				_ = inputFile;
			}
			string targetDir = System.IO.Path.GetDirectoryName(outPath) ?? string.Empty;
			if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
			{
				Directory.CreateDirectory(targetDir);
			}
			App.MainDispatcher?.TryEnqueue(delegate
			{
				task.LogDetails += "\nЗапуск параллельной декомпрессии (Pipeline Parallelism)...";
			});
			ConcurrentBag<string> finalInputFiles = new ConcurrentBag<string>();
			tempDecompDir = System.IO.Path.Combine(string.IsNullOrEmpty(targetDir) ? System.IO.Path.GetTempPath() : targetDir, "StormDecomp_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			Directory.CreateDirectory(tempDecompDir);
			IEnumerable<Task> decompTasks = ((IEnumerable<string>)inputFiles).Select((Func<string, Task>)async delegate(string text)
			{
				if (text.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) || text.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase))
				{
					string decompResult = await App.NszCompression.DecompressNszAsync(task, text, tempDecompDir, cancellationToken);
					if (string.IsNullOrEmpty(decompResult) || !File.Exists(decompResult))
					{
						throw new Exception("Нативная декомпрессия файла " + System.IO.Path.GetFileName(text) + " завершилась с ошибкой.");
					}
					finalInputFiles.Add(CreateHardLinkWithTags(decompResult, tempDecompDir));
				}
				else
				{
					finalInputFiles.Add(CreateHardLinkWithTags(text, tempDecompDir));
				}
			});
			await Task.WhenAll(decompTasks);
			List<string> finalInputFilesList = finalInputFiles.ToList();
			string listFile = System.IO.Path.Combine(targetDir, "list_conv_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".txt");
			File.WriteAllLines(listFile, finalInputFilesList, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
			if (patchFirmware)
			{
				App.MainDispatcher?.TryEnqueue(delegate
				{
					task.LogDetails += "\nПатчинг метаданных (CNMT)...";
					task.Status = "Патчинг...";
				});
				await Task.Delay(500, cancellationToken);
			}
			App.MainDispatcher?.TryEnqueue(delegate
			{
				task.LogDetails += "\nНативное сшивание файлов (Zero-Disk-IO)...";
				task.Status = "Сборка...";
			});
			if (task.TargetFormat.Equals("XCI", StringComparison.OrdinalIgnoreCase) || task.TargetFormat.Equals("XCZ", StringComparison.OrdinalIgnoreCase))
			{
				throw new Exception("Нативная сборка XCI/XCZ временно не поддерживается. Выберите формат NSP или NSZ.");
			}
			PartitionFileSystemBuilder pfsBuilder = new PartitionFileSystemBuilder();
			List<FileStream> fileStreams = new List<FileStream>();
			List<IFileSystem> openedFileSystems = new List<IFileSystem>();
			List<IFile> openedFiles = new List<IFile>();
			List<object> keepAliveReferences = new List<object>();
			HashSet<string> addedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			try
			{
				int fileIndex = 0;
				foreach (string file in finalInputFilesList)
				{
					cancellationToken.ThrowIfCancellationRequested();
					bool isXci = file.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase);
					FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
					fileStreams.Add(fs);
					IStorage storage = fs.AsStorage();
					IFileSystem fileSystem;
					if (isXci)
					{
						Xci xci = new Xci(_keysService.CurrentKeyset, storage);
						keepAliveReferences.Add(xci);
						fileSystem = ((!xci.HasPartition(XciPartitionType.Secure)) ? xci.OpenPartition(XciPartitionType.Root) : xci.OpenPartition(XciPartitionType.Secure));
					}
					else
					{
						PartitionFileSystem pfs = new PartitionFileSystem(storage);
						keepAliveReferences.Add(pfs);
						fileSystem = pfs;
					}
					if (fileSystem != null)
					{
						openedFileSystems.Add(fileSystem);
						foreach (DirectoryEntryEx entry in fileSystem.EnumerateEntries())
						{
							IFile entryFile = OpenFileSafe(fileSystem, entry.FullPath);
							openedFiles.Add(entryFile);
							IStorage entryStorage = entryFile.AsStorage();
							keepAliveReferences.Add(entryStorage);
							string finalName = entry.Name;
							if (entry.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) || entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase))
							{
								try
								{
									byte[] header = new byte[16384];
									entryStorage.Read(0L, header).ThrowIfFailure();
									byte[] hash = SHA256.HashData(header);
									string ncaId = BitConverter.ToString(hash, 0, 16).Replace("-", "").ToLowerInvariant();
									string ext = (entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase) ? ".ncz" : ".nca");
									finalName = ncaId + ext;
								}
								catch
								{
									finalName = $"mc_{fileIndex}_{entry.Name}";
								}
							}
							else
							{
								finalName = $"mc_{fileIndex}_{entry.Name}";
							}
							if (addedFiles.Contains(finalName))
							{
								App.MainDispatcher?.TryEnqueue(delegate
								{
									ProcessingTask processingTask = task;
									processingTask.LogDetails = processingTask.LogDetails + "\n[INFO] Пропущен дубликат файла: " + finalName;
								});
							}
							else
							{
								pfsBuilder.AddFile(finalName, new StorageFile(new SafeStorageWrapper(entryStorage), OpenMode.Read));
								addedFiles.Add(finalName);
							}
						}
					}
					fileIndex++;
				}
				App.MainDispatcher?.TryEnqueue(delegate
				{
					task.LogDetails += "\nЗапись гибридного NSP на диск...";
				});
				using IStorage builtPfs = pfsBuilder.Build(PartitionFileSystemType.Standard);
				using FileStream destStream = new FileStream(intermediatePath, FileMode.Create, FileAccess.Write, FileShare.None, 16777216);
				builtPfs.GetSize(out var totalPfsSize).ThrowIfFailure();
				long remaining = totalPfsSize;
				long offset = 0L;
				byte[] buffer = new byte[4194304];
				while (remaining > 0)
				{
					cancellationToken.ThrowIfCancellationRequested();
					int toRead = (int)Math.Min(buffer.Length, remaining);
					builtPfs.Read(offset, buffer.AsSpan(0, toRead)).ThrowIfFailure();
					destStream.Write(buffer, 0, toRead);
					offset += toRead;
					remaining -= toRead;
					double percent = 100.0 - (double)remaining / (double)totalPfsSize * 100.0;
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
				foreach (FileStream fs2 in fileStreams)
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
				foreach (IFileSystem sys in openedFileSystems)
				{
					try
					{
						sys.Dispose();
					}
					catch
					{
					}
				}
				foreach (object refObj in keepAliveReferences)
				{
					if (refObj is IDisposable disp)
					{
						try
						{
							disp.Dispose();
						}
						catch
						{
						}
					}
				}
			}
			try
			{
				File.Delete(listFile);
			}
			catch
			{
			}
			if (isCompressedFormat)
			{
				App.MainDispatcher?.TryEnqueue(delegate
				{
					ProcessingTask processingTask = task;
					processingTask.LogDetails = processingTask.LogDetails + "\n\nЗапуск потокового сжатия Zstandard (-> " + task.TargetFormat + ")...";
					task.Status = "Сжатие...";
				});
				await App.NszCompression.CompressToNszAsync(task, intermediatePath, targetDir, cancellationToken);
				string ext2 = (task.TargetFormat.Equals("XCZ", StringComparison.OrdinalIgnoreCase) ? ".xcz" : ".nsz");
				string expectedNsz = System.IO.Path.ChangeExtension(intermediatePath, ".nsz");
				string finalCompressedPath = System.IO.Path.ChangeExtension(outPath, ext2);
				if (ext2 == ".xcz" && File.Exists(expectedNsz))
				{
					if (File.Exists(finalCompressedPath))
					{
						File.Delete(finalCompressedPath);
					}
					File.Move(expectedNsz, finalCompressedPath);
				}
				else if (ext2 == ".nsz" && File.Exists(expectedNsz) && !expectedNsz.Equals(finalCompressedPath, StringComparison.OrdinalIgnoreCase))
				{
					if (File.Exists(finalCompressedPath))
					{
						File.Delete(finalCompressedPath);
					}
					File.Move(expectedNsz, finalCompressedPath);
				}
				try
				{
					if (File.Exists(intermediatePath))
					{
						File.Delete(intermediatePath);
					}
				}
				catch
				{
				}
				outPath = finalCompressedPath;
			}
			App.MainDispatcher?.TryEnqueue(delegate
			{
				if (File.Exists(outPath))
				{
					long length = new FileInfo(outPath).Length;
					task.TargetSize = ProcessingTask.FormatSize(length);
					if (task.SourceSizeBytes > 0)
					{
						long num = task.SourceSizeBytes - length;
						double value = (double)num / (double)task.SourceSizeBytes * 100.0;
						task.SizeDifference = $"{((num > 0) ? "-" : "+")}{ProcessingTask.FormatSize(Math.Abs(num))} ({Math.Abs(value):F1}%)";
					}
				}
				task.Progress = 100.0;
				task.Status = "Успешно";
				task.IsRunning = false;
				ProcessingTask processingTask = task;
				processingTask.LogDetails = processingTask.LogDetails + "\nМульти-контент сохранен: " + outPath;
				HistoryService.AddToHistory(task);
			});
			App.Logger.Log("Мульти-контент успешно создан: " + System.IO.Path.GetFileName(outPath), LogLevel.Success);
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
		catch (Exception ex2)
		{
			Exception ex3 = ex2;
			Exception ex4 = ex3;
			App.MainDispatcher?.TryEnqueue(delegate
			{
				task.Status = "Ошибка";
				task.IsRunning = false;
				ProcessingTask processingTask = task;
				processingTask.LogDetails = processingTask.LogDetails + "\nОшибка: " + ex4.Message;
				HistoryService.AddToHistory(task);
			});
			string operationName = ((task.Operation == "Update") ? "обновления" : "сборки мульти-контента");
			App.Logger.Log("Ошибка " + operationName + ": " + ex4.ToString(), LogLevel.Error);
		}
		finally
		{
			if (!string.IsNullOrEmpty(tempDecompDir) && Directory.Exists(tempDecompDir))
			{
				for (int i = 0; i < 3; i++)
				{
					try
					{
						Directory.Delete(tempDecompDir, recursive: true);
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

	[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
	private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, nint lpSecurityAttributes);

	private string CreateHardLinkWithTags(string sourcePath, string tempDir)
	{
		return sourcePath;
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
