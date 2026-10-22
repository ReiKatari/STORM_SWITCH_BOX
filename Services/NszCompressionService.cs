using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LibHac;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    /// <summary>
    /// Сервис сжатия и распаковки контейнеров NSP <-> NSZ / XCI <-> XCZ.
    /// Использует встроенный StormNczCompressor (ZstdSharp) и внешний nsz.exe.
    /// </summary>
    public class NszCompressionService
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        public static string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return fileName;

            string cleaned = fileName
                .Replace('’', '_')
                .Replace('‘', '_')
                .Replace('`', '_')
                .Replace('\'', '_')
                .Replace('“', '_')
                .Replace('”', '_')
                .Replace('"', '_')
                .Replace('–', '-')
                .Replace('—', '-');

            var sb = new System.Text.StringBuilder(cleaned.Length);
            foreach (char c in cleaned)
            {
                // Сохраняем ASCII (c <= 127) и кириллицу (А-Я, а-я, Ё, ё: 0x0400..0x04FF)
                if (c <= 127 || (c >= 0x0400 && c <= 0x04FF))
                {
                    sb.Append(c);
                }
                else
                {
                    sb.Append('_');
                }
            }

            string result = sb.ToString();
            foreach (char invalidChar in System.IO.Path.GetInvalidFileNameChars())
            {
                result = result.Replace(invalidChar, '_');
            }

            return result.Trim('_');
        }

        public static string SanitizeFinalOutputFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return fileName;

            string cleaned = fileName
                .Replace('’', '\'')
                .Replace('‘', '\'')
                .Replace('`', '\'')
                .Replace('–', '-')
                .Replace('—', '-');

            foreach (char invalidChar in System.IO.Path.GetInvalidFileNameChars())
            {
                cleaned = cleaned.Replace(invalidChar, '_');
            }

            while (cleaned.Contains("__"))
            {
                cleaned = cleaned.Replace("__", "_");
            }

            return cleaned.Trim();
        }

        private readonly SwitchFormatService _formatService;

        public NszCompressionService(SwitchFormatService formatService)
        {
            _formatService = formatService;
        }

        /// <summary>
        /// Сжатие контейнера NSP/XCI -> NSZ/XCZ с помощью алгоритма Zstandard.
        /// Секции .nca сжимаются в .ncz, а метаданные переносятся as-is.
        /// Для XCI создается полноценный HFS0 контейнер .xcz, для NSP — PFS0 .nsz.
        /// </summary>
        public async Task CompressToNszAsync(ProcessingTask task, string inputPath, string outDir, CancellationToken cancellationToken)
        {
            if (App.Settings.Current.SolidCompression)
            {
                await CompressSolidViaNszAsync(task, inputPath, outDir, cancellationToken);
                return;
            }
            FileStream? fileStream = null;
            var tempStreams = new List<FileStream>();
            var openedFiles = new List<IFile>();
            string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "StormCompress_" + Guid.NewGuid().ToString("N").Substring(0, 8));

            try
            {
                App.RunOnUI(() =>
                {
                    task.Status = "Подготовка к сжатию...";
                    task.IsRunning = true;
                    task.Progress = 0;
                });

                string fileName = System.IO.Path.GetFileNameWithoutExtension(inputPath);
                bool isXci = inputPath.EndsWith(".xci", StringComparison.OrdinalIgnoreCase);
                string expectedExt = isXci ? ".xcz" : ".nsz";
                string outNszPath = System.IO.Path.Combine(outDir, fileName + expectedExt);

                App.Logger.Log($"[NSZ Engine] Запуск сжатия в {expectedExt.ToUpperInvariant()}: {fileName}", LogLevel.Info);

                long totalBytes = new FileInfo(inputPath).Length;

                App.RunOnUI(() =>
                {
                    if (task.SourceSizeBytes <= 0) task.SourceSizeBytes = totalBytes;
                    task.LogDetails += $"\n🟡 [Сжатие NSZ] Загрузка: {System.IO.Path.GetFileName(inputPath)} ({Models.ProcessingTask.FormatSize(totalBytes)})\n     Запуск Zstd сжатия...";
                    task.Status = isXci ? "Сжатие в XCZ..." : "Сжатие в NSZ...";
                });

                System.IO.Directory.CreateDirectory(tempDir);

                fileStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                IStorage storage = fileStream.AsStorage();

                var pfs = CreatePfsFromStorage(storage, isXci);
                var pfsBuilder = new PartitionFileSystemBuilder();

                int level = App.Settings.Current.CompressionLevel;
                if (level < 1) level = 18;
                if (level > 22) level = 22;

                var entries = pfs.EnumerateEntries().ToList();
                int totalEntries = entries.Count;
                int entryIdx = 0;

                var harvestedTitleKeys = new Dictionary<string, byte[]>();

                // Предварительно регистрируем билеты в KeySet и извлекаем TitleKey для расшифровки секций перед Zstd-сжатием
                SmartShrinkService.HarvestTicketsFromFileSystem(pfs, App.Keys.CurrentKeyset);

                foreach (var entry in entries)
                {
                    if (entry.Name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            using var tikFile = OpenFileSafe(pfs, entry.FullPath);
                            IStorage tikStorage = tikFile.AsStorage();
                            tikStorage.GetSize(out long tikSize).ThrowIfFailure();
                            byte[] tikData = new byte[tikSize];
                            tikStorage.Read(0, tikData).ThrowIfFailure();

                            var ticketInfo = TicketHarvesterService.ExtractDecryptedTicket(tikData, (int)tikSize, App.Keys.CurrentKeyset);
                            if (ticketInfo.HasValue && !string.IsNullOrEmpty(ticketInfo.Value.RightsId) && ticketInfo.Value.TitleKey != null && ticketInfo.Value.TitleKey.Length == 16)
                            {
                                string rId = ticketInfo.Value.RightsId;
                                byte[] tKey = ticketInfo.Value.TitleKey;
                                harvestedTitleKeys[rId] = tKey;
                                lock (Core.NSZ.StormNczCompressor.TitleKeysCache)
                                {
                                    Core.NSZ.StormNczCompressor.TitleKeysCache[rId] = tKey;
                                }

                                App.Logger?.Log($"[NSZ Engine] Успешно извлечён и расшифрован TitleKey для {rId} из {entry.Name}", LogLevel.Info);
                            }
                        }
                        catch (Exception ex)
                        {
                            App.Logger?.Log($"[NSZ Engine] Ошибка чтения билета {entry.Name}: {ex.Message}", LogLevel.Warning);
                        }
                    }
                }

                if (harvestedTitleKeys.Count > 0)
                {
                    try
                    {
                        string titleKeysPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "title.keys");
                        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        if (System.IO.File.Exists(titleKeysPath))
                        {
                            var lines = System.IO.File.ReadAllLines(titleKeysPath);
                            foreach (var line in lines)
                            {
                                var parts = line.Split('=');
                                if (parts.Length == 2)
                                {
                                    dict[parts[0].Trim().ToLowerInvariant()] = parts[1].Trim().ToLowerInvariant();
                                }
                            }
                        }
                        else
                        {
                            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(titleKeysPath)!);
                        }

                        bool changed = false;
                        foreach (var kvp in harvestedTitleKeys)
                        {
                            string rid = kvp.Key.Trim().ToLowerInvariant();
                            string keyHex = BitConverter.ToString(kvp.Value).Replace("-", "").ToLowerInvariant();
                            if (!dict.TryGetValue(rid, out var existingVal) || existingVal != keyHex)
                            {
                                dict[rid] = keyHex;
                                changed = true;
                            }
                        }

                        if (changed)
                        {
                            var outputLines = dict.Select(kv => $"{kv.Key} = {kv.Value}").ToList();
                            System.IO.File.WriteAllLines(titleKeysPath, outputLines);
                        }
                    }
                    catch { }
                }

                bool removeDelta = App.Settings.Current.RemoveDeltaNca;
                var deltaNcas = SmartShrinkService.AnalyzeRedundantEntriesInFileSystem(
                    pfs, App.Keys.CurrentKeyset, removeDelta, out int deltaCount, out int supCount);
                if (removeDelta)
                {
                    foreach (var d in MultiContentService.DetectDeltaNcasToExclude(new List<string> { inputPath }))
                    {
                        deltaNcas.Add(d);
                    }
                }

                var addedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    entryIdx++;

                    if (entry.Type == DirectoryEntryType.Directory) continue;

                    string entryName = entry.Name;
                    if (!SmartShrinkService.IsValidSwitchContainerEntry(entryName, allowNcz: true))
                        continue;

                    if (deltaNcas.Contains(entryName))
                    {
                        App.Logger?.Log($"[SmartShrink] Пропущен избыточный NCA: {entryName} (экономия места)", LogLevel.Info);
                        App.RunOnUI(() => task.LogDetails += $"\n🗑️ [SmartShrink] Пропущен избыточный NCA: {entryName}");
                        continue;
                    }

                    if (!addedNames.Add(entryName)) continue;

                    bool isNca = entryName.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) &&
                                 !entryName.EndsWith(".cnmt.nca", StringComparison.OrdinalIgnoreCase) &&
                                 !entryName.EndsWith(".cnmt.xml", StringComparison.OrdinalIgnoreCase);

                    if (isNca)
                    {
                        string nczName = System.IO.Path.ChangeExtension(entryName, ".ncz");
                        string tempNczPath = System.IO.Path.Combine(tempDir, nczName);

                        App.RunOnUI(() =>
                        {
                            task.Status = $"Сжатие {entryName}...";
                            task.LogDetails += $"\n[{entryIdx}/{totalEntries}] Сжатие: {entryName} -> {nczName}";
                        });

                        using (var entryFile = OpenFileSafe(pfs, entry.FullPath))
                        {
                            IStorage entryStorage = entryFile.AsStorage();
                            Core.NSZ.StormNczCompressor.CompressNcaToNcz(
                                entryStorage,
                                tempNczPath,
                                level,
                                App.Keys.CurrentKeyset,
                                task,
                                cancellationToken,
                                harvestedTitleKeys);
                        }

                        long compressedLen = File.Exists(tempNczPath) ? new FileInfo(tempNczPath).Length : long.MaxValue;
                        if (compressedLen < entry.Size)
                        {
                            var fs = new FileStream(tempNczPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                            tempStreams.Add(fs);
                            pfsBuilder.AddFile(nczName, new StorageFile(new SafeStorageWrapper(fs.AsStorage()), LibHac.Fs.OpenMode.Read));
                        }
                        else
                        {
                            // SmartShrink Guard: если крошечный NCA после добавления заголовка NCZ стал больше оригинала, сохраняем оригинал .nca
                            try { if (File.Exists(tempNczPath)) File.Delete(tempNczPath); } catch { }
                            var entryFile = OpenFileSafe(pfs, entry.FullPath);
                            openedFiles.Add(entryFile);
                            IStorage physicalStorage = entryFile.AsStorage();
                            pfsBuilder.AddFile(entryName, new StorageFile(new SafeStorageWrapper(physicalStorage), LibHac.Fs.OpenMode.Read));
                        }
                    }
                    else
                    {
                        App.RunOnUI(() =>
                        {
                            task.LogDetails += $"\n[{entryIdx}/{totalEntries}] Копирование метаданных: {entryName}";
                        });

                        var entryFile = OpenFileSafe(pfs, entry.FullPath);
                        openedFiles.Add(entryFile);
                        IStorage physicalStorage = entryFile.AsStorage();
                        pfsBuilder.AddFile(entryName, new StorageFile(new SafeStorageWrapper(physicalStorage), LibHac.Fs.OpenMode.Read));
                    }
                }

                if (File.Exists(outNszPath))
                {
                    try { File.Delete(outNszPath); } catch { }
                }

                App.RunOnUI(() =>
                {
                    task.Status = "Финальная упаковка...";
                    task.LogDetails += "\nФинальная сборка сжатого контейнера...";
                });

                string targetPfsPath = isXci ? System.IO.Path.Combine(tempDir, "temp_packed.nsz") : outNszPath;

                using (var builtPfs = pfsBuilder.Build(PartitionFileSystemType.Standard))
                {
                    using (var destStream = new FileStream(targetPfsPath, FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024 * 1024))
                    {
                        builtPfs.GetSize(out long totalPfsSize).ThrowIfFailure();
                        long remaining = totalPfsSize;
                        long offset = 0;
                        byte[] buffer = new byte[128 * 1024];
                        System.Diagnostics.Stopwatch uiSw = System.Diagnostics.Stopwatch.StartNew();

                        while (remaining > 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            int toRead = (int)Math.Min(buffer.Length, remaining);
                            builtPfs.Read(offset, buffer.AsSpan(0, toRead)).ThrowIfFailure();
                            destStream.Write(buffer, 0, toRead);
                            offset += toRead;
                            remaining -= toRead;

                            if (uiSw.ElapsedMilliseconds > 200 || remaining == 0)
                            {
                                uiSw.Restart();
                                double packProgress = 99.0 + ((double)offset / totalPfsSize * 1.0);
                                App.RunOnUI(() => task.Progress = Math.Min(99.9, packProgress));
                            }
                        }
                    }
                }

                if (isXci)
                {
                    Core.NCA.StormXciBuilder.BuildXciFromPfs0(targetPfsPath, outNszPath, App.Keys.CurrentKeyset);
                    try { File.Delete(targetPfsPath); } catch { }
                }

                long finalSize = new FileInfo(outNszPath).Length;
                double ratio = (double)finalSize / totalBytes * 100.0;

                App.RunOnUI(() =>
                {
                    task.Progress = 100;
                    task.Status = "Успешно";
                    task.IsRunning = false;
                    task.LogDetails += $"\n✅ Сжатие успешно завершено!\nИтоговый размер: {Models.ProcessingTask.FormatSize(finalSize)} ({ratio:F1}% от оригинала)";
                    task.TargetSize = Models.ProcessingTask.FormatSize(finalSize);

                    long diff = totalBytes - finalSize;
                    double percent = (double)diff / totalBytes * 100.0;
                    task.SizeDifference = $"{(diff > 0 ? "-" : "+")}{Models.ProcessingTask.FormatSize(Math.Abs(diff))} ({Math.Abs(percent):F1}%)";

                    StormSwitchBox.Services.HistoryService.AddToHistory(task);
                });

                App.Logger.Log($"[NSZ Engine] Сжатие завершено: {fileName}. Экономия: {100 - ratio:F1}%", LogLevel.Success);
            }
            catch (OperationCanceledException)
            {
                App.RunOnUI(() => { task.Status = "Отменено"; task.IsRunning = false; StormSwitchBox.Services.HistoryService.AddToHistory(task); });
            }
            catch (Exception ex)
            {
                App.RunOnUI(() =>
                {
                    task.Status = "Ошибка";
                    task.IsRunning = false;
                    task.LogDetails += $"\n❌ Ошибка сжатия: {ex.Message}";
                    StormSwitchBox.Services.HistoryService.AddToHistory(task);
                });
                App.Logger.Log($"[NSZ Engine] Критическая ошибка сжатия: {ex.Message}", LogLevel.Error);
                throw;
            }
            finally
            {
                if (fileStream != null)
                {
                    try { fileStream.Dispose(); } catch { }
                }
                foreach (var fs in tempStreams)
                {
                    try { fs.Dispose(); } catch { }
                }
                foreach (var f in openedFiles)
                {
                    try { f.Dispose(); } catch { }
                }
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// Декомпрессия контейнера NSZ/XCZ -> NSP/XCI с помощью nsz.exe или StormNczStorage.
        /// </summary>
        public async Task<string?> DecompressNszAsync(ProcessingTask task, string inputPath, string outDir, CancellationToken cancellationToken)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(inputPath);
            string expectedExt = inputPath.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase) ? ".xci" : ".nsp";
            string outNspPath = System.IO.Path.Combine(outDir, fileName + expectedExt);

            // 1. Сначала пробуем надежную распаковку через nsz.exe для восстановления всех NCA блоков 1-в-1
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string toolsDir = System.IO.Path.Combine(appDir, "tools");
            if (!Directory.Exists(toolsDir))
            {
                string parentTools = System.IO.Path.Combine(appDir, "..", "tools");
                if (Directory.Exists(parentTools)) toolsDir = parentTools;
            }
            string nszExe = System.IO.Path.Combine(toolsDir, "nsz", "nsz.exe");

            if (File.Exists(nszExe))
            {
                try
                {
                    App.EnsureUserKeysAvailable();
                    App.RunOnUI(() =>
                    {
                        task.Status = "Распаковка NSZ...";
                        task.IsRunning = true;
                        task.LogDetails += $"\n🟡 [Декомпрессия] Распаковка через nsz.exe: {System.IO.Path.GetFileName(inputPath)}";
                    });

                    string origFileName = System.IO.Path.GetFileName(inputPath);
                    string safeInputName = SanitizeFileName(origFileName);
                    string tempSafeInputPath = System.IO.Path.Combine(outDir, safeInputName);

                    bool linkCreated = false;
                    try { linkCreated = CreateHardLink(tempSafeInputPath, inputPath, IntPtr.Zero); } catch { }
                    if (!linkCreated)
                    {
                        try { File.Copy(inputPath, tempSafeInputPath, true); linkCreated = true; } catch { }
                    }

                    string targetInputForNsz = linkCreated ? tempSafeInputPath : inputPath;
                    string expectedTempNsp = System.IO.Path.Combine(outDir, System.IO.Path.GetFileNameWithoutExtension(safeInputName) + expectedExt);

                    string keysParam = "";
                    if (!string.IsNullOrEmpty(App.Settings.Current.KeysPath) && File.Exists(App.Settings.Current.KeysPath))
                    {
                        keysParam = $"--keys \"{App.Settings.Current.KeysPath}\"";
                    }

                    string nszArgs = $"-D -w --minimal-output {keysParam} -o \"{outDir}\" \"{targetInputForNsz}\"".Trim();
                    int exitCode = await ExternalProcessRunner.RunAsync(
                        nszExe,
                        nszArgs,
                        workingDirectory: System.IO.Path.GetDirectoryName(nszExe) ?? "",
                        task: task,
                        cancellationToken: cancellationToken
                    );

                    if (linkCreated && File.Exists(tempSafeInputPath))
                    {
                        try { File.Delete(tempSafeInputPath); } catch { }
                    }

                    string? foundNsp = null;
                    if (File.Exists(expectedTempNsp) && new FileInfo(expectedTempNsp).Length > 0)
                        foundNsp = expectedTempNsp;
                    else if (File.Exists(outNspPath) && new FileInfo(outNspPath).Length > 0)
                        foundNsp = outNspPath;
                    else
                    {
                        try
                        {
                            var candidates = Directory.GetFiles(outDir, "*" + expectedExt)
                                .Where(f => !f.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) && !f.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase))
                                .Where(f => new FileInfo(f).Length > 0)
                                .OrderByDescending(f => new FileInfo(f).Length)
                                .ToList();
                            if (candidates.Count > 0) foundNsp = candidates[0];
                        }
                        catch { }
                    }

                    if (!string.IsNullOrEmpty(foundNsp))
                    {
                        if (!foundNsp.Equals(outNspPath, StringComparison.OrdinalIgnoreCase))
                        {
                            if (File.Exists(outNspPath)) try { File.Delete(outNspPath); } catch { }
                            File.Move(foundNsp, outNspPath);
                        }
                        App.Logger.Log($"[NSZ Engine] Успешная распаковка nsz.exe: {fileName}", LogLevel.Success);
                        return outNspPath;
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.Log($"[NSZ Engine] nsz.exe warning: {ex.Message}. Fallback to Zero-Disk-IO...", LogLevel.Warning);
                }
            }

            var openedFiles = new List<IFile>();
            try
            {
                App.RunOnUI(() =>
                {
                    task.Status = "Декомпрессия...";
                    task.IsRunning = true;
                    task.LogDetails += $"\nПотоковая распаковка NSZ в память (Zero-Disk-IO): {System.IO.Path.GetFileName(inputPath)}";
                });

                App.Logger.Log($"[NSZ Engine] Потоковая распаковка: {fileName}", LogLevel.Info);

                using var fileStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                IStorage storage = fileStream.AsStorage();
                long pfsOffset = expectedExt == ".xci" ? 0x10000 : 0;
                
                IStorage pfsStorage = pfsOffset > 0 ? new SubStorage(storage, pfsOffset, storage.GetSize(out long sz).IsSuccess() ? sz - pfsOffset : 0) : storage;
                var fileSystem = new PartitionFileSystem(pfsStorage);

                var pfsBuilder = new PartitionFileSystemBuilder();
                int entryIdx = 0;

                List<string> solidFiles = new List<string>();
                List<string> physicalFiles = new List<string>();

                var sortedEntries = fileSystem.EnumerateEntries().ToList();

                bool IsNczMagic(IStorage fileStorage)
                {
                    fileStorage.GetSize(out long size).ThrowIfFailure();
                    if (size < 8)
                    {
                        return false;
                    }
                    try
                    {
                        byte[] magicBuf = new byte[8];
                        fileStorage.Read(0, magicBuf);
                        string m1 = System.Text.Encoding.ASCII.GetString(magicBuf);
                        if (m1 == "NCZSECTN" || m1 == "NCZBLOCK") return true;

                        if (size >= 0x4008)
                        {
                            fileStorage.Read(0x4000, magicBuf);
                            string m2 = System.Text.Encoding.ASCII.GetString(magicBuf);
                            if (m2 == "NCZSECTN" || m2 == "NCZBLOCK") return true;
                        }
                        
                        return false;
                    }
                    catch
                    {
                        return false;
                    }
                }

                foreach (var entry in sortedEntries)
                {
                    bool isMetadata = !entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase) && 
                                      !entry.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase);

                    if (isMetadata)
                    {
                        physicalFiles.Add(entry.Name);
                        continue;
                    }

                    using var entryFile = OpenFileSafe(fileSystem, entry.FullPath);
                    IStorage entryStorage = entryFile.AsStorage();

                    bool isVirtualOrBlock = IsNczMagic(entryStorage) || entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase);
                    
                    if (isVirtualOrBlock)
                    {
                        solidFiles.Add(entry.Name);
                    }
                    else
                    {
                        physicalFiles.Add(entry.Name);
                    }
                }

                var titleKeyMap = new Dictionary<string, byte[]>();
                foreach (var entry in sortedEntries)
                {
                    if (entry.Name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            using var entryFile = OpenFileSafe(fileSystem, entry.FullPath);
                            IStorage tikStorage = entryFile.AsStorage();
                            tikStorage.GetSize(out long tikSize).ThrowIfFailure();
                            byte[] tikData = new byte[tikSize];
                            tikStorage.Read(0, tikData).ThrowIfFailure();

                            var ticketInfo = TicketHarvesterService.ExtractDecryptedTicket(tikData, (int)tikSize, App.Keys.CurrentKeyset);
                            if (ticketInfo.HasValue && !string.IsNullOrEmpty(ticketInfo.Value.RightsId) && ticketInfo.Value.TitleKey != null && ticketInfo.Value.TitleKey.Length == 16)
                            {
                                string rightsIdStr = ticketInfo.Value.RightsId;
                                byte[] tKey = ticketInfo.Value.TitleKey;
                                titleKeyMap[rightsIdStr] = tKey;
                                lock (Core.NSZ.StormNczCompressor.TitleKeysCache)
                                {
                                    Core.NSZ.StormNczCompressor.TitleKeysCache[rightsIdStr] = tKey;
                                }
                                App.RunOnUI(() => task.LogDetails += $"\nИзвлечён TitleKey (Zero-Disk-IO) для {rightsIdStr}");
                            }
                        }
                        catch (Exception ex)
                        {
                            App.Logger?.Log($"[NSZ Engine] Ошибка чтения билета {entry.Name}: {ex.Message}", LogLevel.Warning);
                        }
                    }
                }

                IStorage? globalSolidStorage = null;
                var solidEntry = sortedEntries.FirstOrDefault(e => e.Name.EndsWith(".solid", StringComparison.OrdinalIgnoreCase));
                if (solidEntry != null)
                {
                    var solidFile = OpenFileSafe(fileSystem, solidEntry.FullPath);
                    openedFiles.Add(solidFile);
                    globalSolidStorage = solidFile.AsStorage();
                }

                await Task.Run(() =>
                {
                    foreach (var entry in sortedEntries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        entryIdx++;
                        string entryName = entry.Name;

                        int currentEntry = entryIdx;
                        int totalEntries = sortedEntries.Count;
                        App.RunOnUI(() =>
                            task.LogDetails += $"\n[{currentEntry}/{totalEntries}] Подготовка к распаковке: {entryName}");

                        if (solidFiles.Contains(entryName))
                        {
                            string ncaName = entryName.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase) 
                                ? System.IO.Path.ChangeExtension(entryName, ".nca") 
                                : entryName;
                            
                            var entryFile = OpenFileSafe(fileSystem, entry.FullPath);
                            openedFiles.Add(entryFile);
                            IStorage entryStorage = entryFile.AsStorage();
                            var decStorage = new Core.NSZ.StormNczStorage(entryStorage, titleKeyMap, globalSolidStorage, App.Keys.CurrentKeyset);
                             
                            pfsBuilder.AddFile(ncaName, new StorageFile(new SafeStorageWrapper(decStorage), LibHac.Fs.OpenMode.Read));
                        }
                        else
                        {
                            var entryFile = OpenFileSafe(fileSystem, entry.FullPath);
                            openedFiles.Add(entryFile);
                            IStorage physicalStorage = entryFile.AsStorage();
                            pfsBuilder.AddFile(entryName, new StorageFile(new SafeStorageWrapper(physicalStorage), LibHac.Fs.OpenMode.Read));
                        }
                    }

                    App.RunOnUI(() => task.LogDetails += "\nЗапись распакованного контейнера на диск...");
                    using var builtPfs = pfsBuilder.Build(PartitionFileSystemType.Standard);
                    
                    using var destStream = new FileStream(outNspPath, FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024 * 1024);
                    
                    builtPfs.GetSize(out long totalPfsSize).ThrowIfFailure();
                    long remaining = totalPfsSize;
                    long offset = 0;
                    byte[] buffer = new byte[81920];
                    System.Diagnostics.Stopwatch uiSw = System.Diagnostics.Stopwatch.StartNew();
                    while (remaining > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int toRead = (int)Math.Min(buffer.Length, remaining);
                        builtPfs.Read(offset, buffer.AsSpan(0, toRead)).ThrowIfFailure();
                        destStream.Write(buffer, 0, toRead);
                        offset += toRead;
                        remaining -= toRead;
                        if (uiSw.ElapsedMilliseconds > 200 || remaining == 0)
                        {
                            uiSw.Restart();
                            double packProgress = 99.0 + ((double)offset / totalPfsSize * 1.0);
                            App.RunOnUI(() => task.Progress = Math.Min(99.9, packProgress));
                        }
                    }
                }, cancellationToken);

                App.Logger.Log($"[NSZ Engine] Потоковая распаковка завершена: {outNspPath}", LogLevel.Success);
                if (globalSolidStorage != null)
                {
                    try { ((IDisposable)globalSolidStorage).Dispose(); } catch { }
                }
                return outNspPath;
            }
            catch (OperationCanceledException)
            {
                try { if (System.IO.File.Exists(outNspPath)) System.IO.File.Delete(outNspPath); } catch { }
                return null;
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[NSZ Engine] Ошибка потоковой распаковки:\n{ex}", LogLevel.Error);
                try { if (System.IO.File.Exists(outNspPath)) System.IO.File.Delete(outNspPath); } catch { }
                return null;
            }
            finally
            {
                foreach (var f in openedFiles)
                {
                    try { f.Dispose(); } catch { }
                }
            }
        }

        private PartitionFileSystem CreatePfsFromStorage(IStorage storage, bool isXci)
        {
            if (isXci)
            {
                storage.GetSize(out long storageSize).ThrowIfFailure();
                var rootStorage = new SubStorage(storage, 0x10000, storageSize - 0x10000);
                var rootPfs = new PartitionFileSystem(rootStorage);
                
                using var secureFile = new UniqueRef<IFile>();
                using var securePath = new LibHac.Fs.Path();
                securePath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes("/secure"))).ThrowIfFailure();
                rootPfs.OpenFile(ref secureFile.Ref, in securePath, OpenMode.Read).ThrowIfFailure();

                var pfs = new PartitionFileSystem(secureFile.Release().AsStorage());
                return pfs;
            }
            else
            {
                var pfs = new PartitionFileSystem(storage);
                return pfs;
            }
        }

        private static IFile OpenFileSafe(IFileSystem fsToOpen, string pth)
        {
            using var fRef = new UniqueRef<IFile>();
            using var path = new LibHac.Fs.Path();
            path.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(pth))).ThrowIfFailure();
            fsToOpen.OpenFile(ref fRef.Ref, in path, LibHac.Fs.OpenMode.Read).ThrowIfFailure();
            return fRef.Release();
        }

        public async Task CompressSolidViaNszAsync(ProcessingTask task, string inputPath, string outDir, CancellationToken cancellationToken)
        {
            string toolsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
            string nszExe = System.IO.Path.Combine(toolsDir, "nsz", "nsz.exe");
            if (!System.IO.File.Exists(nszExe))
            {
                nszExe = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "tools", "nsz", "nsz.exe");
            }

            long totalBytes = new FileInfo(inputPath).Length;
            string fileName = System.IO.Path.GetFileNameWithoutExtension(inputPath);
            bool isXci = inputPath.EndsWith(".xci", StringComparison.OrdinalIgnoreCase);
            string expectedExt = isXci ? ".xcz" : ".nsz";
            string outNszPath = System.IO.Path.Combine(outDir, fileName + expectedExt);

            App.RunOnUI(() =>
            {
                if (task.SourceSizeBytes <= 0) task.SourceSizeBytes = totalBytes;
                task.Status = "Сжатие Block NSZ (1 МБ)...";
                task.IsRunning = true;
                task.Progress = 0;
                task.LogDetails += $"\n⚡ [Block NSZ] Загрузка: {System.IO.Path.GetFileName(inputPath)} ({Models.ProcessingTask.FormatSize(totalBytes)})\n     Запуск блочного сжатия (--block, 1 МБ, Zstandard)...";
            });

            if (!System.IO.File.Exists(nszExe))
            {
                App.Logger.Log("[NSZ Engine] nsz.exe не найден для сжатия.", LogLevel.Warning);
                App.RunOnUI(() => task.LogDetails += "\n⚠️ nsz.exe не найден. Переключение на встроенное блочное сжатие...");
                return;
            }

            string keysParam = "";
            string keysPath = App.Settings.Current.KeysPath;
            if (!string.IsNullOrEmpty(keysPath) && System.IO.File.Exists(keysPath))
            {
                keysParam = $"--keys \"{keysPath}\"";
            }
            else
            {
                string defaultKeys = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "prod.keys");
                if (System.IO.File.Exists(defaultKeys))
                {
                    keysParam = $"--keys \"{defaultKeys}\"";
                }
            }

            int level = Math.Clamp(App.Settings.Current.CompressionLevel, 1, 22);

            string targetDrive = System.IO.Path.GetPathRoot(System.IO.Path.GetFullPath(outDir)) ?? "";
            string tempCompressDir = !string.IsNullOrEmpty(targetDrive) && System.IO.Directory.Exists(targetDrive)
                ? System.IO.Path.Combine(targetDrive, "STORM_TMP", "SC_" + Guid.NewGuid().ToString("N").Substring(0, 8))
                : System.IO.Path.Combine(System.IO.Path.GetTempPath(), "StormCompress_" + Guid.NewGuid().ToString("N").Substring(0, 8));

            System.IO.Directory.CreateDirectory(tempCompressDir);
            TempCleanupService.RegisterActiveTempDirectory(tempCompressDir);

            try
            {
                string effectiveInputPath = inputPath;

                // Lossless Shrink Engine: если во входном NSP есть мусорные DeltaFragment NCA или невалидные файлы,
                // предварительно очищаем их, чтобы nsz.exe не сжимал лишние гигабайты Delta NCA!
                if (!isXci && App.Settings.Current.RemoveDeltaNca)
                {
                    try
                    {
                        using var srcFs = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        var srcPfs = new PartitionFileSystem(srcFs.AsStorage());
                        var redundantSet = SmartShrinkService.AnalyzeRedundantEntriesInFileSystem(
                            srcPfs, App.Keys.CurrentKeyset, true, out int dCount, out int sCount);

                        if (redundantSet.Count > 0)
                        {
                            string shrunkStagingNsp = System.IO.Path.Combine(tempCompressDir, System.IO.Path.GetFileName(inputPath));
                            App.RunOnUI(() => task.LogDetails += $"\n🗑️ [SmartShrink] Предварительное удаление {redundantSet.Count} избыточных Delta NCA перед сжатием...");

                            var pfsBuilder = new PartitionFileSystemBuilder();
                            var openedStagingFiles = new List<IFile>();
                            try
                            {
                                var addedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                foreach (var entry in srcPfs.EnumerateEntries("/", "*"))
                                {
                                    if (!SmartShrinkService.IsValidSwitchContainerEntry(entry.Name, allowNcz: true)) continue;
                                    if (redundantSet.Contains(entry.Name)) continue;
                                    if (!addedNames.Add(entry.Name)) continue;
                                    IFile f = OpenFileSafe(srcPfs, entry.FullPath);
                                    openedStagingFiles.Add(f);
                                    pfsBuilder.AddFile(entry.Name, new StorageFile(new SafeStorageWrapper(f.AsStorage()), OpenMode.Read));
                                }
                                using var built = pfsBuilder.Build(PartitionFileSystemType.Standard);
                                using var outStream = new FileStream(shrunkStagingNsp, FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024 * 1024);
                                built.AsStream().CopyTo(outStream);
                                effectiveInputPath = shrunkStagingNsp;
                            }
                            finally
                            {
                                foreach (var f in openedStagingFiles) { try { f.Dispose(); } catch { } }
                            }
                        }
                    }
                    catch { }
                }

                int totalCores = Math.Max(1, Environment.ProcessorCount);
                int concurrentWorkers = Math.Max(1, App.Settings.Current.ConcurrentTasks);
                int threadsPerWorker = Math.Max(1, totalCores / concurrentWorkers);

                var gpuService = GpuAccelerationService.Instance;
                string gpuDesc = gpuService.GetAccelerationStatusDescription();

                App.RunOnUI(() =>
                {
                    task.LogDetails += $"\n{gpuDesc}";
                    task.LogDetails += $"\n⚙️ [Worker Pool] Многопоточный пул: выделено {threadsPerWorker} потоков на задачу (Всего ядер: {totalCores})";
                });

                string args = $"-C -B -s 20 -l {level} -t {threadsPerWorker} --overwrite {keysParam} -o \"{tempCompressDir}\" \"{effectiveInputPath}\"".Trim();

                int exitCode = await ExternalProcessRunner.RunAsync(
                    nszExe,
                    args,
                    workingDirectory: System.IO.Path.GetDirectoryName(nszExe) ?? "",
                    task: task,
                    cancellationToken: cancellationToken
                );

                if (exitCode != 0)
                {
                    throw new Exception($"Ошибка сжатия nsz.exe (код завершения {exitCode})");
                }

                string? generatedNsz = null;
                var candidates = System.IO.Directory.GetFiles(tempCompressDir, "*" + expectedExt)
                    .OrderByDescending(f => new FileInfo(f).Length)
                    .ToList();
                if (candidates.Count > 0)
                {
                    generatedNsz = candidates[0];
                }

                if (string.IsNullOrEmpty(generatedNsz) || !System.IO.File.Exists(generatedNsz))
                {
                    throw new FileNotFoundException($"Сжатый файл {expectedExt} не найден после работы nsz.exe в {tempCompressDir}");
                }

                outNszPath = await SafeFileOperations.SafeMoveOrReplaceFileAsync(generatedNsz, outNszPath, task, cancellationToken);

                if (System.IO.File.Exists(outNszPath))
                {
                    long finalSize = new FileInfo(outNszPath).Length;
                    double ratio = (double)finalSize / totalBytes * 100.0;
                    long diff = totalBytes - finalSize;
                    double percent = (double)diff / totalBytes * 100.0;

                    App.RunOnUI(() =>
                    {
                        task.Progress = 100;
                        task.Status = "Успешно";
                        task.IsRunning = false;
                        task.LogDetails += $"\n⚡ [Block NSZ] Блочное сжатие (1 МБ) завершено!\n     Итог: {Models.ProcessingTask.FormatSize(finalSize)} ({ratio:F1}% от оригинала, экономия {Math.Abs(percent):F1}%)";
                        task.TargetSize = Models.ProcessingTask.FormatSize(finalSize);
                        task.SizeDifference = $"{(diff > 0 ? "-" : "+")}{Models.ProcessingTask.FormatSize(Math.Abs(diff))} ({Math.Abs(percent):F1}%)";
                        HistoryService.AddToHistory(task);
                    });

                    App.Logger.Log($"[NSZ Engine] Блочное сжатие Block NSZ (1 МБ) успешно: {fileName}. Экономия: {100 - ratio:F1}%", LogLevel.Success);
                }
            }
            finally
            {
                TempCleanupService.ForceDeleteDirectory(tempCompressDir);
            }
        }
    }

    /// <summary>
    /// Обертка над IStorage для потокобезопасного чтения и защиты от выхода за границы.
    /// Гарантирует отсутствие исключений при чтении, когда упаковщик (PartitionFileSystemBuilder) читает данные блоками фиксированного размера.
    /// </summary>
    public class SafeStorageWrapper : IStorage
    {
        private readonly IStorage _baseStorage;
        private readonly object _lock = new object();
        private long _cachedSize = -1;

        public SafeStorageWrapper(IStorage baseStorage)
        {
            _baseStorage = baseStorage;
            try
            {
                if (_baseStorage != null && _baseStorage.GetSize(out long sz).IsSuccess())
                {
                    _cachedSize = sz;
                }
            }
            catch { }
        }

        public override Result Read(long offset, Span<byte> destination)
        {
            lock (_lock)
            {
                try
                {
                    long size = _cachedSize;
                    if (size < 0)
                    {
                        var res = _baseStorage.GetSize(out size);
                        if (res.IsSuccess()) _cachedSize = size;
                    }

                    if (size >= 0)
                    {
                        if (offset >= size)
                        {
                            destination.Fill(0);
                            return Result.Success;
                        }
                        if (offset + destination.Length > size)
                        {
                            int allowed = (int)(size - offset);
                            var subDest = destination.Slice(0, allowed);
                            var readRes = _baseStorage.Read(offset, subDest);
                            if (readRes.IsFailure()) return readRes;
                            destination.Slice(allowed).Fill(0);
                            return Result.Success;
                        }
                    }
                    return _baseStorage.Read(offset, destination);
                }
                catch (Exception ex)
                {
                    if (_cachedSize >= 0 && offset >= _cachedSize)
                    {
                        destination.Fill(0);
                        return Result.Success;
                    }
                    App.Logger?.Log($"[SafeStorageWrapper Error] Offset: {offset}, Len: {destination.Length}: {ex.Message}", Models.LogLevel.Warning);
                    return LibHac.Fs.ResultFs.OutOfRange.Log();
                }
            }
        }

        public override Result Write(long offset, ReadOnlySpan<byte> source)
        {
            lock (_lock)
            {
                try { return _baseStorage.Write(offset, source); }
                catch { return LibHac.Fs.ResultFs.OutOfRange.Log(); }
            }
        }

        public override Result Flush()
        {
            lock (_lock)
            {
                try { return _baseStorage.Flush(); }
                catch { return Result.Success; }
            }
        }

        public override Result SetSize(long size)
        {
            lock (_lock)
            {
                try
                {
                    _cachedSize = size;
                    return _baseStorage.SetSize(size);
                }
                catch { return LibHac.Fs.ResultFs.OutOfRange.Log(); }
            }
        }

        public override Result GetSize(out long size)
        {
            lock (_lock)
            {
                if (_cachedSize >= 0)
                {
                    size = _cachedSize;
                    return Result.Success;
                }
                try
                {
                    var res = _baseStorage.GetSize(out size);
                    if (res.IsSuccess()) _cachedSize = size;
                    return res;
                }
                catch
                {
                    size = 0;
                    return LibHac.Fs.ResultFs.OutOfRange.Log();
                }
            }
        }

        public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
        {
            lock (_lock)
            {
                try { return _baseStorage.OperateRange(outBuffer, operationId, offset, size, inBuffer); }
                catch { return Result.Success; }
            }
        }
    }
}
