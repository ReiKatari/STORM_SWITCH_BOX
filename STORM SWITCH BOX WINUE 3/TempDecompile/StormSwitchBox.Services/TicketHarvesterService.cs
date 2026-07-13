using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services;

public class TicketHarvesterService
{
	private readonly HashSet<string> _processedFiles = new HashSet<string>();

	private readonly object _lockObj = new object();

	public void HarvestTicketsBackground(IEnumerable<string> filePaths)
	{
		List<string> filesToProcess = filePaths.Where((string f) => !Directory.Exists(f) && (f.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase))).ToList();
		if (filesToProcess.Count == 0)
		{
			return;
		}
		Task.Run(delegate
		{
			foreach (string item in filesToProcess)
			{
				lock (_lockObj)
				{
					if (!_processedFiles.Contains(item))
					{
						_processedFiles.Add(item);
						goto IL_006e;
					}
				}
				continue;
				IL_006e:
				try
				{
					HarvestFromFile(item);
				}
				catch (Exception ex)
				{
					App.Logger.Log("[Ticket Harvester] Ошибка извлечения билетов из " + System.IO.Path.GetFileName(item) + ": " + ex.Message, LogLevel.Warning);
				}
			}
		});
	}

	private void HarvestFromFile(string filePath)
	{
		string path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "title.keys");
		bool flag = filePath.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase);
		using FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		IStorage storage = stream.AsStorage();
		PartitionFileSystem partitionFileSystem = null;
		if (flag)
		{
			storage.GetSize(out var size).ThrowIfFailure();
			SubStorage storage2 = new SubStorage(storage, 65536L, size - 65536);
			PartitionFileSystem partitionFileSystem2 = new PartitionFileSystem(storage2);
			using UniqueRef<IFile> uniqueRef = default(UniqueRef<IFile>);
			using LibHac.Fs.Path path2 = new LibHac.Fs.Path();
			path2.Initialize(new U8Span(Encoding.UTF8.GetBytes("/secure"))).ThrowIfFailure();
			partitionFileSystem2.OpenFile(ref uniqueRef.Ref, in path2, OpenMode.Read).ThrowIfFailure();
			partitionFileSystem = new PartitionFileSystem(uniqueRef.Release().AsStorage());
		}
		else
		{
			partitionFileSystem = new PartitionFileSystem(storage);
		}
		List<DirectoryEntryEx> list = (from e in partitionFileSystem.EnumerateEntries()
			where e.Name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase)
			select e).ToList();
		if (list.Count <= 0)
		{
			return;
		}
		List<string> list2 = new List<string>();
		foreach (DirectoryEntryEx item in list)
		{
			using UniqueRef<IFile> uniqueRef2 = default(UniqueRef<IFile>);
			using LibHac.Fs.Path path3 = new LibHac.Fs.Path();
			path3.Initialize(new U8Span(Encoding.UTF8.GetBytes(item.FullPath))).ThrowIfFailure();
			partitionFileSystem.OpenFile(ref uniqueRef2.Ref, in path3, OpenMode.Read).ThrowIfFailure();
			IFile file = uniqueRef2.Release();
			byte[] array = new byte[768];
			file.Read(out var bytesRead, 0L, array).ThrowIfFailure();
			if (bytesRead >= 688)
			{
				byte[] array2 = new byte[16];
				Array.Copy(array, 384, array2, 0, 16);
				byte[] array3 = new byte[16];
				Array.Copy(array, 672, array3, 0, 16);
				string text = BitConverter.ToString(array2).Replace("-", "").ToLower();
				string text2 = BitConverter.ToString(array3).Replace("-", "").ToLower();
				list2.Add(text2 + " = " + text);
			}
		}
		if (list2.Count <= 0)
		{
			return;
		}
		lock (_lockObj)
		{
			HashSet<string> existingKeys = new HashSet<string>();
			try
			{
				if (File.Exists(path))
				{
					string[] array4 = File.ReadAllLines(path);
					string[] array5 = array4;
					foreach (string text3 in array5)
					{
						existingKeys.Add(text3.Split('=')[0].Trim().ToLower());
					}
				}
				else
				{
					Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
				}
			}
			catch
			{
			}
			List<string> list3 = list2.Where((string k) => !existingKeys.Contains(k.Split('=')[0].Trim().ToLower())).ToList();
			if (list3.Count > 0)
			{
				File.AppendAllLines(path, list3);
				App.Logger.Log($"[Ticket Harvester] Найдено и добавлено {list3.Count} новых билетов из {System.IO.Path.GetFileName(filePath)}", LogLevel.Success);
			}
		}
	}
}
