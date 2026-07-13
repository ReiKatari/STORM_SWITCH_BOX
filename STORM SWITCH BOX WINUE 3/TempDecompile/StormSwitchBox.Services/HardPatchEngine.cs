using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.Es;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services;

public class HardPatchEngine
{
	private readonly KeysService _keysService;

	private static readonly object _keysLock = new object();

	public HardPatchEngine(KeysService keysService)
	{
		_keysService = keysService;
	}

	public async Task PatchUpdateAsync(ProcessingTask task, List<string> inputFiles, string outPath, CancellationToken cancellationToken, bool isMultiContent = false)
	{
		App.MainDispatcher?.TryEnqueue(delegate
		{
			task.Status = "Хард-патчинг...";
			task.LogDetails += "\nЗапуск нативного движка глубокой пересборки...";
		});
		string tempDir = "";
		try
		{
			string userProfileSwitch = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch");
			string userProfileKeys = System.IO.Path.Combine(userProfileSwitch, "prod.keys");
			lock (_keysLock)
			{
				try
				{
					if (!Directory.Exists(userProfileSwitch))
					{
						Directory.CreateDirectory(userProfileSwitch);
					}
					if (!string.IsNullOrEmpty(App.Settings.Current.KeysPath) && File.Exists(App.Settings.Current.KeysPath))
					{
						File.Copy(App.Settings.Current.KeysPath, userProfileKeys, overwrite: true);
					}
				}
				catch
				{
				}
			}
			string[] keyFilesToClean = new string[2]
			{
				App.Settings.Current.KeysPath,
				userProfileKeys
			};
			App.MainDispatcher?.TryEnqueue(delegate
			{
				task.LogDetails += "\nПроверка и очистка файлов ключей (prod.keys)...";
			});
			foreach (string kPath in keyFilesToClean.Distinct())
			{
				if (string.IsNullOrEmpty(kPath) || !File.Exists(kPath))
				{
					continue;
				}
				try
				{
					lock (_keysLock)
					{
						string[] lines = File.ReadAllLines(kPath);
						List<string> newLines = new List<string>();
						bool keysModified = false;
						for (int i = 0; i < lines.Length; i++)
						{
							string line = lines[i].Trim();
							if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("#"))
							{
								if (!string.IsNullOrEmpty(line))
								{
									keysModified = true;
								}
								continue;
							}
							string[] parts = line.Split('=', 2);
							if (parts.Length != 2)
							{
								continue;
							}
							string keyName = parts[0].Trim();
							string val = parts[1].Trim().Split(' ')[0];
							if (val.Length != 32 && val.Length != 64)
							{
								keysModified = true;
								continue;
							}
							if (line != keyName + " = " + val)
							{
								keysModified = true;
							}
							newLines.Add(keyName + " = " + val);
						}
						if (keysModified)
						{
							File.WriteAllLines(kPath, newLines);
							App.MainDispatcher?.TryEnqueue(delegate
							{
								ProcessingTask processingTask = task;
								processingTask.LogDetails = processingTask.LogDetails + "\nФайл " + System.IO.Path.GetFileName(kPath) + " был автоматически исправлен.";
							});
						}
					}
				}
				catch (Exception ex)
				{
					Exception ex2 = ex;
					Exception ex3 = ex2;
					App.MainDispatcher?.TryEnqueue(delegate
					{
						ProcessingTask processingTask = task;
						processingTask.LogDetails = processingTask.LogDetails + "\nОшибка: " + ex3.Message;
					});
				}
			}
			if (inputFiles.Count < 1)
			{
				throw new Exception("Для хард-патчинга необходим хотя бы один файл (База).");
			}
			string baseFile = string.Empty;
			string updateFile = string.Empty;
			App.MainDispatcher?.TryEnqueue(delegate
			{
				task.LogDetails += "\nАнализ входных файлов для поиска Базы и Обновления...";
			});
			foreach (string file in inputFiles)
			{
				if (!Directory.Exists(file))
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
			}
			if (string.IsNullOrEmpty(baseFile))
			{
				baseFile = inputFiles.FirstOrDefault((string f) => !Directory.Exists(f) && (f.Contains("[v0]") || f.Contains("v0"))) ?? inputFiles.FirstOrDefault((string f) => !Directory.Exists(f)) ?? "";
			}
			if (string.IsNullOrEmpty(updateFile))
			{
				updateFile = inputFiles.FirstOrDefault((string f) => !Directory.Exists(f) && f != baseFile && f.Contains("v") && !f.Contains("v0")) ?? inputFiles.FirstOrDefault((string f) => !Directory.Exists(f) && f != baseFile) ?? "";
			}
			App.MainDispatcher?.TryEnqueue(delegate
			{
				ProcessingTask processingTask = task;
				processingTask.LogDetails = processingTask.LogDetails + "\nВыбрана База: " + System.IO.Path.GetFileName(baseFile) + "\nВыбрано Обновление: " + System.IO.Path.GetFileName(updateFile);
			});
			string titleId = string.Empty;
			string targetDir = System.IO.Path.GetDirectoryName(outPath);
			if (string.IsNullOrEmpty(targetDir))
			{
				targetDir = AppDomain.CurrentDomain.BaseDirectory;
			}
			tempDir = System.IO.Path.Combine(targetDir, "temp_hardpatch_" + Guid.NewGuid().ToString("N").Substring(0, 6));
			Directory.CreateDirectory(tempDir);
			if (baseFile.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) || baseFile.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase))
			{
				App.MainDispatcher?.TryEnqueue(delegate
				{
					ProcessingTask processingTask = task;
					processingTask.LogDetails = processingTask.LogDetails + "\nНативная декомпрессия " + System.IO.Path.GetExtension(baseFile) + " -> .nsp (Zero-Disk-IO)...";
				});
				string decompResult = await App.NszCompression.DecompressNszAsync(task, baseFile, tempDir, cancellationToken);
				if (string.IsNullOrEmpty(decompResult) || !File.Exists(decompResult))
				{
					throw new Exception("Нативная декомпрессия " + System.IO.Path.GetFileName(baseFile) + " завершилась с ошибкой.");
				}
				baseFile = decompResult;
			}
			if (updateFile.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) || updateFile.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase))
			{
				App.MainDispatcher?.TryEnqueue(delegate
				{
					ProcessingTask processingTask = task;
					processingTask.LogDetails = processingTask.LogDetails + "\nНативная декомпрессия " + System.IO.Path.GetExtension(updateFile) + " -> .nsp (Zero-Disk-IO)...";
				});
				string decompResult2 = await App.NszCompression.DecompressNszAsync(task, updateFile, tempDir, cancellationToken);
				if (string.IsNullOrEmpty(decompResult2) || !File.Exists(decompResult2))
				{
					throw new Exception("Нативная декомпрессия " + System.IO.Path.GetFileName(updateFile) + " завершилась с ошибкой.");
				}
				updateFile = decompResult2;
			}
			try
			{
				App.MainDispatcher?.TryEnqueue(delegate
				{
					ProcessingTask processingTask = task;
					processingTask.LogDetails = processingTask.LogDetails + "\nЧтение метаданных (TitleID) из файла " + System.IO.Path.GetFileName(baseFile) + "...";
				});
				SwitchFormatInfo info2 = App.SwitchFormat.ParseNsp(baseFile);
				if (!string.IsNullOrEmpty(info2.TitleId))
				{
					titleId = info2.TitleId;
					App.MainDispatcher?.TryEnqueue(delegate
					{
						ProcessingTask processingTask = task;
						processingTask.LogDetails = processingTask.LogDetails + "\nУспешно извлечен TitleID: " + titleId;
					});
				}
			}
			catch
			{
			}
			if (string.IsNullOrEmpty(titleId))
			{
				throw new Exception("Не удалось прочитать TitleID из файла. Файл поврежден или не содержит метаданных.");
			}
			string outDir = System.IO.Path.GetDirectoryName(outPath) ?? string.Empty;
			Directory.CreateDirectory(outDir);
			string[] array = new string[2] { baseFile, updateFile };
			foreach (string nspFile in array)
			{
				if (!string.IsNullOrEmpty(nspFile) && File.Exists(nspFile))
				{
					PreExtractTicketsNative(nspFile);
				}
			}
			KeySet localKeyset = _keysService.CurrentKeyset;
			try
			{
				string pKeys = App.Settings.Current.KeysPath;
				if (string.IsNullOrEmpty(pKeys) || !File.Exists(pKeys))
				{
					pKeys = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "prod.keys");
				}
				string[] tickets = Directory.GetFiles(tempDir, "*.tik", SearchOption.AllDirectories);
				string globTKeys = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "title.keys");
				if (!File.Exists(globTKeys))
				{
					Directory.CreateDirectory(System.IO.Path.GetDirectoryName(globTKeys));
					File.WriteAllText(globTKeys, "");
				}
				App.MainDispatcher?.TryEnqueue(delegate
				{
					task.LogDetails += $"\nНайдено билетов (.tik) для извлечения ключей: {tickets.Length}";
				});
				bool keysAdded = false;
				string[] array2 = tickets;
				foreach (string tik in array2)
				{
					try
					{
						using MemoryStream stream = new MemoryStream(File.ReadAllBytes(tik));
						Ticket ticket = new Ticket(stream);
						byte[] rightsIdBytes = ticket.RightsId;
						byte[] titleKeyBytes = ticket.GetTitleKey(localKeyset);
						string rightsId = BitConverter.ToString(rightsIdBytes).Replace("-", "").ToLowerInvariant();
						string titleKey = BitConverter.ToString(titleKeyBytes).Replace("-", "").ToLowerInvariant();
						if (string.IsNullOrEmpty(rightsId) || string.IsNullOrEmpty(titleKey) || titleKey.Length != 32)
						{
							continue;
						}
						string currentKeys = File.ReadAllText(globTKeys);
						if (!currentKeys.Contains(rightsId))
						{
							File.AppendAllText(globTKeys, rightsId + " = " + titleKey + "\n");
							keysAdded = true;
							App.MainDispatcher?.TryEnqueue(delegate
							{
								ProcessingTask processingTask = task;
								processingTask.LogDetails = processingTask.LogDetails + "\nУспешно извлечен TitleKey (LibHac) для Rights ID: " + rightsId;
							});
							continue;
						}
						string[] lines2 = File.ReadAllLines(globTKeys);
						bool updated = false;
						for (int i2 = 0; i2 < lines2.Length; i2++)
						{
							if (lines2[i2].Trim().StartsWith(rightsId, StringComparison.OrdinalIgnoreCase) && lines2[i2].Trim() != rightsId + " = " + titleKey)
							{
								lines2[i2] = rightsId + " = " + titleKey;
								updated = true;
							}
						}
						if (updated)
						{
							File.WriteAllLines(globTKeys, lines2);
							keysAdded = true;
							App.MainDispatcher?.TryEnqueue(delegate
							{
								ProcessingTask processingTask = task;
								processingTask.LogDetails = processingTask.LogDetails + "\nУспешно обновлен TitleKey (LibHac) для Rights ID: " + rightsId;
							});
						}
					}
					catch (Exception ex4)
					{
						Exception ex2 = ex4;
						Exception ex5 = ex2;
						App.MainDispatcher?.TryEnqueue(delegate
						{
							ProcessingTask processingTask = task;
							processingTask.LogDetails = processingTask.LogDetails + "\nОшибка: " + ex5.Message;
						});
					}
				}
				if (keysAdded || File.Exists(globTKeys))
				{
					if (keysAdded)
					{
						App.MainDispatcher?.TryEnqueue(delegate
						{
							task.LogDetails += "\nУспешно извлечены TitleKeys из файлов .tik (LibHac).";
						});
					}
					localKeyset = ExternalKeyReader.ReadKeyFile(pKeys, globTKeys);
					localKeyset.DeriveKeys();
					_keysService.CurrentKeyset = localKeyset;
				}
			}
			catch
			{
			}
			App.MainDispatcher?.TryEnqueue(delegate
			{
				task.LogDetails += "\nНативная распаковка NSP контейнеров...";
			});
			Array.Empty<string>();
			List<string> ncaList = new List<string>();
			ExtractNspNative(baseFile, "basedata");
			ExtractNspNative(updateFile, "updatedata");
			string[] allNcas = ncaList.ToArray();
			string controlNca = null;
			string[] array3 = allNcas;
			foreach (string ncaFile in array3)
			{
				try
				{
					using FileStream fileStream = new FileStream(ncaFile, FileMode.Open, FileAccess.Read, FileShare.Read);
					IStorage storage = fileStream.AsStorage();
					Nca nca = new Nca(localKeyset, storage);
					if (nca.Header.ContentType == NcaContentType.Control)
					{
						string tIdHex = nca.Header.TitleId.ToString("x16");
						if (controlNca == null || ncaFile.Contains("updatedata", StringComparison.OrdinalIgnoreCase))
						{
							controlNca = ncaFile;
						}
						if (tIdHex.EndsWith("000") || string.IsNullOrEmpty(titleId))
						{
							titleId = tIdHex;
						}
						else if (tIdHex.EndsWith("800") && titleId.EndsWith("800"))
						{
							titleId = tIdHex.Substring(0, tIdHex.Length - 3) + "000";
						}
					}
					else if (nca.Header.ContentType != NcaContentType.Program)
					{
					}
				}
				catch
				{
				}
			}
			if (controlNca == null)
			{
				throw new Exception("Не удалось найти или расшифровать control.nca. Вероятно, в вашем файле prod.keys отсутствуют мастер-ключи (key_area_key_application) для этой игры. Обновите prod.keys!");
			}
			List<string> programNcas = new List<string>();
			string[] array4 = allNcas;
			foreach (string ncaFile2 in array4)
			{
				try
				{
					using FileStream fileStream2 = new FileStream(ncaFile2, FileMode.Open, FileAccess.Read, FileShare.Read);
					Nca nca2 = new Nca(localKeyset, fileStream2.AsStorage());
					if (nca2.Header.ContentType == NcaContentType.Program)
					{
						programNcas.Add(ncaFile2);
					}
				}
				catch
				{
				}
			}
			string baseProgramNca = null;
			string patchProgramNca = null;
			foreach (string ncaFile3 in programNcas)
			{
				if (ncaFile3.Contains("basedata", StringComparison.OrdinalIgnoreCase))
				{
					baseProgramNca = ncaFile3;
				}
				else if (ncaFile3.Contains("updatedata", StringComparison.OrdinalIgnoreCase))
				{
					patchProgramNca = ncaFile3;
				}
			}
			if (baseProgramNca == null && patchProgramNca == null)
			{
				foreach (string ncaFile4 in programNcas)
				{
					try
					{
						using FileStream fileStream3 = new FileStream(ncaFile4, FileMode.Open, FileAccess.Read, FileShare.Read);
						Nca nca3 = new Nca(localKeyset, fileStream3.AsStorage());
						if (nca3.Header.ContentType == NcaContentType.Program)
						{
							string tIdHex2 = nca3.Header.TitleId.ToString("X16");
							if (tIdHex2.EndsWith("000"))
							{
								baseProgramNca = ncaFile4;
							}
							else
							{
								patchProgramNca = ncaFile4;
							}
						}
					}
					catch
					{
					}
				}
			}
			if (baseProgramNca == null)
			{
				baseProgramNca = programNcas.OrderByDescending((string f) => new FileInfo(f).Length).FirstOrDefault();
				if (programNcas.Count > 1 && patchProgramNca == null)
				{
					patchProgramNca = programNcas.OrderBy((string f) => new FileInfo(f).Length).FirstOrDefault((string f) => f != baseProgramNca);
				}
			}
			string modRomfsDir = inputFiles.FirstOrDefault((string f) => Directory.Exists(f) && System.IO.Path.GetFileName(f).Equals("romfs", StringComparison.OrdinalIgnoreCase)) ?? "";
			string modExefsDir = inputFiles.FirstOrDefault((string f) => Directory.Exists(f) && System.IO.Path.GetFileName(f).Equals("exefs", StringComparison.OrdinalIgnoreCase)) ?? "";
			string globalModsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mods", titleId);
			if (string.IsNullOrEmpty(modRomfsDir) && string.IsNullOrEmpty(modExefsDir))
			{
				Directory.Exists(globalModsDir);
			}
			App.MainDispatcher?.TryEnqueue(delegate
			{
				task.LogDetails += "\nИзвлечение данных через LibHac для подготовки сборки...";
			});
			string extractedExefsDir = System.IO.Path.Combine(tempDir, "extracted_exefs");
			string extractedRomfsDir = System.IO.Path.Combine(tempDir, "extracted_romfs");
			Directory.CreateDirectory(extractedExefsDir);
			Directory.CreateDirectory(extractedRomfsDir);
			if (!string.IsNullOrEmpty(baseProgramNca))
			{
				try
				{
					using FileStream baseFs = new FileStream(baseProgramNca, FileMode.Open, FileAccess.Read, FileShare.Read);
					Nca baseNca = new Nca(localKeyset, baseFs.AsStorage());
					FileStream patchFs = null;
					Nca patchNca = null;
					if (!string.IsNullOrEmpty(patchProgramNca))
					{
						patchFs = new FileStream(patchProgramNca, FileMode.Open, FileAccess.Read, FileShare.Read);
						patchNca = new Nca(localKeyset, patchFs.AsStorage());
					}
					if (patchNca != null)
					{
						if (baseNca.CanOpenSection(NcaSectionType.Code))
						{
							App.SwitchFormat.ExtractNcaSection(baseNca, patchNca, NcaSectionType.Code, extractedExefsDir, cancellationToken);
						}
						if (baseNca.CanOpenSection(NcaSectionType.Data))
						{
							App.SwitchFormat.ExtractNcaSection(baseNca, patchNca, NcaSectionType.Data, extractedRomfsDir, cancellationToken);
						}
					}
					else
					{
						if (baseNca.CanOpenSection(NcaSectionType.Code))
						{
							App.SwitchFormat.ExtractNcaSection(baseNca, NcaSectionType.Code, extractedExefsDir, cancellationToken);
						}
						if (baseNca.CanOpenSection(NcaSectionType.Data))
						{
							App.SwitchFormat.ExtractNcaSection(baseNca, NcaSectionType.Data, extractedRomfsDir, cancellationToken);
						}
					}
					patchFs?.Dispose();
				}
				catch (Exception ex6)
				{
					Exception ex7 = ex6;
					throw new Exception("Ошибка нативной распаковки NCA: " + ex7.Message, ex7);
				}
			}
			if (!string.IsNullOrEmpty(modRomfsDir))
			{
				App.MainDispatcher?.TryEnqueue(delegate
				{
					task.LogDetails += "\nИнтеграция мода (RomFS)...";
				});
				CopyDirectory(modRomfsDir, extractedRomfsDir, recursive: true);
			}
			if (!string.IsNullOrEmpty(modExefsDir))
			{
				App.MainDispatcher?.TryEnqueue(delegate
				{
					task.LogDetails += "\nИнтеграция мода (ExeFS)...";
				});
				CopyDirectory(modExefsDir, extractedExefsDir, recursive: true);
			}
			if (Directory.Exists(globalModsDir))
			{
				App.MainDispatcher?.TryEnqueue(delegate
				{
					ProcessingTask processingTask = task;
					processingTask.LogDetails = processingTask.LogDetails + "\nОбнаружены глобальные моды для " + titleId + "! Автоматическая инъекция...";
				});
				string globRomfs = System.IO.Path.Combine(globalModsDir, "romfs");
				string globExefs = System.IO.Path.Combine(globalModsDir, "exefs");
				if (Directory.Exists(globRomfs))
				{
					CopyDirectory(globRomfs, extractedRomfsDir, recursive: true);
				}
				if (Directory.Exists(globExefs))
				{
					CopyDirectory(globExefs, extractedExefsDir, recursive: true);
				}
			}
			if (App.Settings.Current.TrimXci && Directory.Exists(extractedRomfsDir))
			{
				App.MainDispatcher?.TryEnqueue(delegate
				{
					task.LogDetails += "\nУдаление неиспользуемых языков (Language Trimming)...";
				});
				App.SwitchFormat.TrimLanguages(extractedRomfsDir);
			}
			string exefsDir = extractedExefsDir;
			string romfsDir = extractedRomfsDir;
			if (!string.IsNullOrEmpty(romfsDir) && Directory.Exists(romfsDir))
			{
				PruneEmptyDirectories(romfsDir);
			}
			App.MainDispatcher?.TryEnqueue(delegate
			{
				task.LogDetails += "\nГенерация нового гибридного контейнера (NCA/NSP)...";
			});
			string packTempOut = System.IO.Path.Combine(tempDir, "output");
			Directory.CreateDirectory(packTempOut);
			if (string.IsNullOrEmpty(romfsDir) || !Directory.Exists(romfsDir))
			{
				romfsDir = System.IO.Path.Combine(tempDir, "romfs_empty");
				Directory.CreateDirectory(romfsDir);
			}
			if (string.IsNullOrEmpty(exefsDir) || !Directory.Exists(exefsDir))
			{
				exefsDir = System.IO.Path.Combine(tempDir, "exefs_empty");
				Directory.CreateDirectory(exefsDir);
			}
			try
			{
				if (File.Exists(await App.NativePack.PackHybridNspAsync(task, titleId, baseProgramNca, romfsDir, exefsDir, allNcas, packTempOut, cancellationToken)))
				{
					App.MainDispatcher?.TryEnqueue(delegate
					{
						task.Progress = 100.0;
					});
				}
			}
			catch (Exception ex8)
			{
				throw new Exception("Ошибка нативной сборки NSP: " + ex8.Message, ex8);
			}
			string[] generatedFiles = Directory.GetFiles(packTempOut, "*.nsp");
			if (generatedFiles.Length == 0)
			{
				generatedFiles = Directory.GetFiles(tempDir, "*.nsp");
				if (generatedFiles.Length == 0)
				{
					string parentTemp = System.IO.Path.GetDirectoryName(tempDir);
					if (parentTemp != null)
					{
						string[] parentFiles = (from f in Directory.GetFiles(parentTemp, "*.nsp")
							where f.IndexOf(titleId, StringComparison.OrdinalIgnoreCase) >= 0
							select f).ToArray();
						if (parentFiles.Length != 0)
						{
							generatedFiles = parentFiles;
						}
					}
				}
			}
			if (File.Exists(outPath))
			{
				File.Delete(outPath);
			}
			if (generatedFiles.Length != 0)
			{
				string genFile = generatedFiles.OrderByDescending((string f) => new FileInfo(f).CreationTime).First();
				string targetExt = System.IO.Path.GetExtension(outPath).ToLower();
				if (targetExt == ".nsp")
				{
					File.Move(genFile, outPath);
				}
				else if (targetExt == ".xci" || targetExt == ".xcz")
				{
					string outDirF = System.IO.Path.GetDirectoryName(outPath) ?? string.Empty;
					App.MainDispatcher?.TryEnqueue(delegate
					{
						task.LogDetails += "\nЗапуск пост-конвертации в XCI...";
					});
					await App.SwitchFormat.ConvertContainerAsync(task, genFile, outDirF, "XCI", cancellationToken);
					string expectedXci = System.IO.Path.ChangeExtension(System.IO.Path.Combine(outDirF, System.IO.Path.GetFileName(genFile)), ".xci");
					if (targetExt == ".xcz" && File.Exists(expectedXci))
					{
						App.MainDispatcher?.TryEnqueue(delegate
						{
							task.LogDetails += "\nЗапуск сжатия в XCZ...";
						});
						await App.NszCompression.CompressToNszAsync(task, expectedXci, outDirF, cancellationToken);
						string expectedNsz = System.IO.Path.ChangeExtension(expectedXci, ".nsz");
						if (File.Exists(expectedNsz))
						{
							File.Move(expectedNsz, outPath);
						}
						try
						{
							File.Delete(expectedXci);
						}
						catch
						{
						}
					}
					else if (File.Exists(expectedXci))
					{
						File.Move(expectedXci, outPath);
					}
				}
				else if (targetExt == ".nsz")
				{
					string outDirF2 = System.IO.Path.GetDirectoryName(outPath) ?? string.Empty;
					App.MainDispatcher?.TryEnqueue(delegate
					{
						task.LogDetails += "\nЗапуск сжатия в NSZ...";
					});
					await App.NszCompression.CompressToNszAsync(task, genFile, outDirF2, cancellationToken);
					string expectedNsz2 = System.IO.Path.ChangeExtension(System.IO.Path.Combine(outDirF2, System.IO.Path.GetFileName(genFile)), ".nsz");
					if (File.Exists(expectedNsz2))
					{
						File.Move(expectedNsz2, outPath);
					}
				}
				try
				{
					if (!string.IsNullOrEmpty(tempDir))
					{
						Directory.Delete(tempDir, recursive: true);
					}
				}
				catch
				{
				}
				App.MainDispatcher?.TryEnqueue(delegate
				{
					if (!isMultiContent && File.Exists(outPath))
					{
						long length = new FileInfo(outPath).Length;
						task.TargetSize = ProcessingTask.FormatSize(length);
						if (task.SourceSizeBytes > 0)
						{
							long num5 = task.SourceSizeBytes - length;
							double value = (double)num5 / (double)task.SourceSizeBytes * 100.0;
							task.SizeDifference = $"{((num5 > 0) ? "-" : "+")}{ProcessingTask.FormatSize(Math.Abs(num5))} ({Math.Abs(value):F1}%)";
						}
					}
					task.Progress = 50.0;
					task.Status = "Подготовка сборки...";
					ProcessingTask processingTask = task;
					processingTask.LogDetails = processingTask.LogDetails + "\nХард-патчинг завершен: " + System.IO.Path.GetFileName(outPath);
				});
				return;
			}
			throw new Exception("Не удалось найти сгенерированный NSP файл после сборки.");
			void ExtractNspNative(string nspPath, string outSubdir)
			{
				if (string.IsNullOrEmpty(nspPath) || !File.Exists(nspPath))
				{
					return;
				}
				string text = System.IO.Path.Combine(tempDir, outSubdir);
				Directory.CreateDirectory(text);
				using FileStream stream2 = new FileStream(nspPath, FileMode.Open, FileAccess.Read, FileShare.Read);
				PartitionFileSystem partitionFileSystem = new PartitionFileSystem(stream2.AsStorage());
				foreach (DirectoryEntryEx item in partitionFileSystem.EnumerateEntries())
				{
					if (item.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase))
					{
						string text2 = System.IO.Path.Combine(text, item.Name);
						using UniqueRef<IFile> uniqueRef = default(UniqueRef<IFile>);
						using LibHac.Fs.Path path = new LibHac.Fs.Path();
						path.Initialize(new U8Span(Encoding.UTF8.GetBytes(item.FullPath))).ThrowIfFailure();
						partitionFileSystem.OpenFile(ref uniqueRef.Ref, in path, OpenMode.Read).ThrowIfFailure();
						using FileStream destination = new FileStream(text2, FileMode.Create, FileAccess.Write, FileShare.None, 4194304);
						uniqueRef.Release().AsStream().CopyTo(destination);
						ncaList.Add(text2);
					}
				}
			}
		}
		catch (Exception ex9)
		{
			Exception ex2 = ex9;
			Exception ex10 = ex2;
			try
			{
				if (!string.IsNullOrEmpty(tempDir) && Directory.Exists(tempDir))
				{
					Directory.Delete(tempDir, recursive: true);
				}
			}
			catch
			{
			}
			App.MainDispatcher?.TryEnqueue(delegate
			{
				task.Status = "Ошибка";
				task.IsRunning = false;
				ProcessingTask processingTask = task;
				processingTask.LogDetails = processingTask.LogDetails + "\n[КРИТИЧЕСКАЯ ОШИБКА]: " + ex10.Message;
				HistoryService.AddToHistory(task);
			});
			App.Logger.Log("Ошибка хард-патчинга: " + ex10.ToString(), LogLevel.Error);
		}
		void PreExtractTicketsNative(string targetNsp)
		{
			try
			{
				using FileStream stream2 = new FileStream(targetNsp, FileMode.Open, FileAccess.Read, FileShare.Read);
				PartitionFileSystem partitionFileSystem = new PartitionFileSystem(stream2.AsStorage());
				foreach (DirectoryEntryEx item2 in partitionFileSystem.EnumerateEntries())
				{
					if (item2.Name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase))
					{
						using UniqueRef<IFile> uniqueRef = default(UniqueRef<IFile>);
						using LibHac.Fs.Path path = new LibHac.Fs.Path();
						path.Initialize(new U8Span(Encoding.UTF8.GetBytes(item2.FullPath))).ThrowIfFailure();
						partitionFileSystem.OpenFile(ref uniqueRef.Ref, in path, OpenMode.Read).ThrowIfFailure();
						IFile file2 = uniqueRef.Release();
						string path2 = System.IO.Path.Combine(tempDir, item2.Name);
						using FileStream destination = new FileStream(path2, FileMode.Create, FileAccess.Write);
						file2.AsStream().CopyTo(destination);
					}
				}
			}
			catch
			{
			}
		}
	}

	private static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
	{
		DirectoryInfo directoryInfo = new DirectoryInfo(sourceDir);
		if (!directoryInfo.Exists)
		{
			throw new DirectoryNotFoundException("Source directory not found: " + directoryInfo.FullName);
		}
		DirectoryInfo[] directories = directoryInfo.GetDirectories();
		Directory.CreateDirectory(destinationDir);
		FileInfo[] files = directoryInfo.GetFiles();
		foreach (FileInfo fileInfo in files)
		{
			string destFileName = System.IO.Path.Combine(destinationDir, fileInfo.Name);
			fileInfo.CopyTo(destFileName, overwrite: true);
		}
		if (recursive)
		{
			DirectoryInfo[] array = directories;
			foreach (DirectoryInfo directoryInfo2 in array)
			{
				string destinationDir2 = System.IO.Path.Combine(destinationDir, directoryInfo2.Name);
				CopyDirectory(directoryInfo2.FullName, destinationDir2, recursive: true);
			}
		}
	}

	private static void PruneEmptyDirectories(string startLocation)
	{
		string[] directories = Directory.GetDirectories(startLocation);
		foreach (string text in directories)
		{
			PruneEmptyDirectories(text);
			if (Directory.GetFiles(text).Length == 0 && Directory.GetDirectories(text).Length == 0)
			{
				try
				{
					Directory.Delete(text, recursive: false);
				}
				catch
				{
				}
			}
		}
	}
}
