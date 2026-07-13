using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using StormSwitchBox.Core.NCA;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services;

public class NativePackEngine
{
	private readonly KeysService _keys;

	public NativePackEngine(KeysService keys)
	{
		_keys = keys;
	}

	public async Task<string> PackHybridNspAsync(ProcessingTask task, string titleId, string baseProgramNcaPath, string romfsDir, string exefsDir, string[] allNcas, string outDir, CancellationToken cancellationToken)
	{
		App.MainDispatcher?.TryEnqueue(delegate
		{
			task.LogDetails += "\n[INFO] Инициализация нативной сборки Program NCA (StormNcaBuilder)...";
		});
		string outNcaPath = System.IO.Path.Combine(outDir, titleId + ".nca");
		StormNcaBuilder builder = new StormNcaBuilder(_keys);
		await builder.BuildProgramNcaAsync(titleId, baseProgramNcaPath, exefsDir, romfsDir, outNcaPath);
		App.MainDispatcher?.TryEnqueue(delegate
		{
			task.LogDetails += "\n[INFO] Формирование итогового NSP контейнера...";
		});
		string outNspPath = System.IO.Path.Combine(outDir, titleId + ".nsp");
		PartitionFileSystemBuilder pfsBuilder = new PartitionFileSystemBuilder();
		List<FileStream> openStreams = new List<FileStream>();
		try
		{
			FileStream newNcaStream = new FileStream(outNcaPath, FileMode.Open, FileAccess.Read, FileShare.Read);
			openStreams.Add(newNcaStream);
			pfsBuilder.AddFile(titleId + ".nca", new StorageFile(new SafeStorageWrapper(newNcaStream.AsStorage()), OpenMode.Read));
			HashSet<string> addedNames = new HashSet<string>();
			foreach (string ncaPath in allNcas)
			{
				if (ncaPath.Equals(baseProgramNcaPath, StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				try
				{
					FileStream fs = new FileStream(ncaPath, FileMode.Open, FileAccess.Read, FileShare.Read);
					Nca nca = new Nca(_keys.CurrentKeyset, fs.AsStorage());
					if (nca.Header.ContentType == NcaContentType.Program)
					{
						fs.Dispose();
						continue;
					}
					string fileName = System.IO.Path.GetFileName(ncaPath);
					if (!addedNames.Contains(fileName))
					{
						openStreams.Add(fs);
						pfsBuilder.AddFile(fileName, new StorageFile(new SafeStorageWrapper(fs.AsStorage()), OpenMode.Read));
						addedNames.Add(fileName);
					}
					else
					{
						fs.Dispose();
					}
				}
				catch
				{
					string fileName2 = System.IO.Path.GetFileName(ncaPath);
					if (!addedNames.Contains(fileName2))
					{
						FileStream fs2 = new FileStream(ncaPath, FileMode.Open, FileAccess.Read, FileShare.Read);
						openStreams.Add(fs2);
						pfsBuilder.AddFile(fileName2, new StorageFile(new SafeStorageWrapper(fs2.AsStorage()), OpenMode.Read));
						addedNames.Add(fileName2);
					}
				}
			}
			string baseDir = System.IO.Path.GetDirectoryName(allNcas.FirstOrDefault()) ?? "";
			if (!string.IsNullOrEmpty(baseDir))
			{
				string parentTemp = System.IO.Path.GetDirectoryName(baseDir) ?? baseDir;
				string[] files = Directory.GetFiles(parentTemp, "*.*", SearchOption.AllDirectories);
				foreach (string extFile in files)
				{
					string ext = System.IO.Path.GetExtension(extFile).ToLower();
					if ((!(ext != ".tik") || !(ext != ".cert") || !(ext != ".xml")) && !extFile.Contains("extracted_romfs") && !extFile.Contains("extracted_exefs") && !extFile.Contains("output"))
					{
						string fileName3 = System.IO.Path.GetFileName(extFile);
						if (!addedNames.Contains(fileName3))
						{
							FileStream fs3 = new FileStream(extFile, FileMode.Open, FileAccess.Read, FileShare.Read);
							openStreams.Add(fs3);
							pfsBuilder.AddFile(fileName3, new StorageFile(new SafeStorageWrapper(fs3.AsStorage()), OpenMode.Read));
							addedNames.Add(fileName3);
						}
					}
				}
			}
			using IStorage builtPfs = pfsBuilder.Build(PartitionFileSystemType.Standard);
			using FileStream destStream = new FileStream(outNspPath, FileMode.Create, FileAccess.Write, FileShare.None, 16777216);
			builtPfs.GetSize(out var totalSize).ThrowIfFailure();
			long remaining = totalSize;
			long offset = 0L;
			byte[] buffer = new byte[81920];
			while (remaining > 0)
			{
				cancellationToken.ThrowIfCancellationRequested();
				int toRead = (int)Math.Min(remaining, buffer.Length);
				builtPfs.Read(offset, new Span<byte>(buffer, 0, toRead)).ThrowIfFailure();
				destStream.Write(buffer, 0, toRead);
				offset += toRead;
				remaining -= toRead;
				if (totalSize <= 0 || offset % 1048576 != 0)
				{
					continue;
				}
				int currentProgress = (int)((double)offset / (double)totalSize * 100.0);
				if (currentProgress > 0)
				{
					App.MainDispatcher?.TryEnqueue(delegate
					{
						task.Progress = Math.Min(currentProgress, 99);
					});
				}
			}
		}
		finally
		{
			foreach (FileStream stream in openStreams)
			{
				try
				{
					stream.Dispose();
				}
				catch
				{
				}
			}
		}
		App.MainDispatcher?.TryEnqueue(delegate
		{
			task.LogDetails += "\n[INFO] Успешная сборка NSP!";
		});
		return outNspPath;
	}
}
