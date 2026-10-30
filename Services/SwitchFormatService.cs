using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using LibHac;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public class SwitchFormatService
    {
        private readonly KeysService _keysService;

        public SwitchFormatService(KeysService keysService)
        {
            _keysService = keysService;
        }

        public void CleanKeysFile(string keysPath)
        {
            if (string.IsNullOrEmpty(keysPath) || !System.IO.File.Exists(keysPath)) return;

            try
            {
                var lines = System.IO.File.ReadAllLines(keysPath);
                bool modified = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#") || line.TrimStart().StartsWith(";")) continue;
                    
                    int equalsIndex = line.IndexOf('=');
                    if (equalsIndex > 0)
                    {
                        string keyName = line.Substring(0, equalsIndex).Trim();
                        string keyValue = line.Substring(equalsIndex + 1).Trim();
                        
                        if (keyValue.Length == 34 && keyValue.EndsWith("00") && System.Text.RegularExpressions.Regex.IsMatch(keyValue, "^[0-9a-fA-F]+$"))
                        {
                            // Fix 34-char hex key by removing trailing '00'
                            lines[i] = $"{keyName} = {keyValue.Substring(0, 32)}";
                            modified = true;
                        }
                    }
                }
                
                if (modified)
                {
                    System.IO.File.WriteAllLines(keysPath, lines);
                    App.Logger.Log($"[SwitchFormatService] Автоматически исправлены 34-символьные ключи в {System.IO.Path.GetFileName(keysPath)}", Models.LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[SwitchFormatService] Ошибка очистки файла ключей {System.IO.Path.GetFileName(keysPath)}: {ex.Message}", Models.LogLevel.Warning);
            }
        }

        public Models.SwitchFormatInfo ParseNsp(string filePath)
        {
            var info = new Models.SwitchFormatInfo { SizeBytes = new FileInfo(filePath).Length };

            if (!_keysService.IsLoaded) return info;

            try
            {
                bool isXci = filePath.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase);

                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                IStorage storage = fileStream.AsStorage();
                
                IFileSystem? fileSystem = null;
                PartitionFileSystem? pfs = null;

                if (isXci)
                {
                    storage.GetSize(out long storageSize).ThrowIfFailure();
                    var rootStorage = new SubStorage(storage, 0x10000, storageSize - 0x10000);
                    var rootPfs = new PartitionFileSystem(rootStorage);
                    
                    using var secureFile = new UniqueRef<IFile>();
                    using var securePath = new LibHac.Fs.Path();
                    securePath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes("/secure"))).ThrowIfFailure();
                    rootPfs.OpenFile(ref secureFile.Ref, in securePath, OpenMode.Read).ThrowIfFailure();
                    
                    pfs = new PartitionFileSystem(secureFile.Release().AsStorage());
                    fileSystem = pfs;
                }
                else
                {
                    pfs = new PartitionFileSystem(storage);
                    fileSystem = pfs;
                }

                SmartShrinkService.HarvestTicketsFromFileSystem(fileSystem, _keysService.CurrentKeyset);

                foreach (var entry in fileSystem.EnumerateEntries().Where(e => e.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase)))
                {
                    using var ncaFileRef = new UniqueRef<IFile>();
                    using var ncaPath = new LibHac.Fs.Path();
                    ncaPath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(entry.FullPath))).ThrowIfFailure();
                    fileSystem.OpenFile(ref ncaFileRef.Ref, in ncaPath, OpenMode.Read).ThrowIfFailure();
                    
                    IFile ncaFile = ncaFileRef.Release();
                    IStorage ncaStorage = ncaFile.AsStorage();
                    
                    var nca = new Nca(_keysService.CurrentKeyset, ncaStorage);
                    
                    if (nca.Header.ContentType == NcaContentType.Meta)
                    {
                        IFileSystem? fs = null;
                        try { fs = nca.OpenFileSystem(0, IntegrityCheckLevel.None); } catch { }
                        if (fs == null)
                        {
                            try { fs = nca.OpenFileSystem(1, IntegrityCheckLevel.None); } catch { }
                        }

                        if (fs != null)
                        {
                            foreach (var cnmtEntry in fs.EnumerateEntries("/", "*.cnmt"))
                            {
                                using var cnmtFileRef = new UniqueRef<IFile>();
                                using var cnmtPath = new LibHac.Fs.Path();
                                cnmtPath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(cnmtEntry.FullPath))).ThrowIfFailure();
                                fs.OpenFile(ref cnmtFileRef.Ref, in cnmtPath, OpenMode.Read).ThrowIfFailure();
                                
                                IFile cnmtFile = cnmtFileRef.Release();
                                
                                using var cnmtStream = new MemoryStream();
                                cnmtFile.AsStream().CopyTo(cnmtStream);
                                byte[] cnmtBytes = cnmtStream.ToArray();

                                ulong titleId = BitConverter.ToUInt64(cnmtBytes, 0x00);
                                uint version = BitConverter.ToUInt32(cnmtBytes, 0x08);
                                byte typeByte = cnmtBytes[0x0C];

                                info.TitleId = titleId.ToString("X16");
                                info.Version = version.ToString();
                                
                                if (typeByte == 0x80) info.ContentType = "Application";
                                else if (typeByte == 0x81) info.ContentType = "Patch";
                                else if (typeByte == 0x82) info.ContentType = "AddOnContent";
                                else info.ContentType = "Unknown";
                                
                                App.Logger.Log($"[LibHac] Распознан контент: {info.TitleId} | Тип: {info.ContentType} | v{info.Version}", Models.LogLevel.Debug);
                                // Не возвращаем info сразу, чтобы дождаться парсинга Control NCA
                            }
                        }
                    }
                    else if (nca.Header.ContentType == NcaContentType.Control)
                    {
                        IFileSystem? fs = null;
                        try { fs = nca.OpenFileSystem(0, IntegrityCheckLevel.None); } catch { }
                        
                        if (fs != null)
                        {
                            var iconEntry = fs.EnumerateEntries("/", "icon_*.dat").FirstOrDefault();
                            if (iconEntry != null)
                            {
                                using var iconFileRef = new UniqueRef<IFile>();
                                using var iconPath = new LibHac.Fs.Path();
                                iconPath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(iconEntry.FullPath))).ThrowIfFailure();
                                fs.OpenFile(ref iconFileRef.Ref, in iconPath, OpenMode.Read).ThrowIfFailure();
                                
                                IFile iconFile = iconFileRef.Release();
                                using var iconStream = new MemoryStream();
                                iconFile.AsStream().CopyTo(iconStream);
                                info.IconBytes = iconStream.ToArray();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"Ошибка парсинга {System.IO.Path.GetFileName(filePath)}: {ex.Message}", Models.LogLevel.Warning);
            }

            return info;
        }

        public async System.Threading.Tasks.Task UnpackContainerAsync(Models.ProcessingTask task, string filePath, string baseOutFolder, System.Threading.CancellationToken token)
        {
            if (!_keysService.IsLoaded) throw new Exception("Криптографические ключи не загружены.");

            App.RunOnUI(() => { task.Status = "Распаковка..."; });
            
            App.RunOnUI(() => { 
                task.LogDetails += $"\nРаспаковка контейнера с помощью LibHac (Zero-Disk-IO)..."; 
            });

            var info = ParseNsp(filePath);
            
            string subFolder = "basedata";
            if (info.ContentType.Contains("Patch", StringComparison.OrdinalIgnoreCase)) subFolder = "updatedata";
            if (info.ContentType.Contains("AddOnContent", StringComparison.OrdinalIgnoreCase)) subFolder = "dlcdata";

            string finalOutPath = System.IO.Path.Combine(baseOutFolder, subFolder);

            App.Logger.Log($"Целевая папка для распаковки: {finalOutPath}", Models.LogLevel.Info);
            System.IO.Directory.CreateDirectory(finalOutPath);

            await System.Threading.Tasks.Task.Run(() =>
            {
                bool isXci = filePath.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase);

                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                IStorage storage = fileStream.AsStorage();
                
                IFileSystem? fileSystem = null;
                PartitionFileSystem? pfs = null;

                if (isXci)
                {
                    storage.GetSize(out long storageSize).ThrowIfFailure();
                    var rootStorage = new SubStorage(storage, 0x10000, storageSize - 0x10000);
                    var rootPfs = new PartitionFileSystem(rootStorage);
                    
                    using var secureFile = new UniqueRef<IFile>();
                    using var securePath2 = new LibHac.Fs.Path();
                    securePath2.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes("/secure"))).ThrowIfFailure();
                    rootPfs.OpenFile(ref secureFile.Ref, in securePath2, OpenMode.Read).ThrowIfFailure();
                    
                    pfs = new PartitionFileSystem(secureFile.Release().AsStorage());
                    fileSystem = pfs;
                }
                else
                {
                    pfs = new PartitionFileSystem(storage);
                    fileSystem = pfs;
                }

                SmartShrinkService.HarvestTicketsFromFileSystem(fileSystem, _keysService.CurrentKeyset);

                var titleKeyMap = new System.Collections.Generic.Dictionary<string, byte[]>();
                foreach (var entry in fileSystem.EnumerateEntries().Where(e => e.Name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        using var tikFileRef = new UniqueRef<IFile>();
                        using var tikPath = new LibHac.Fs.Path();
                        tikPath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(entry.FullPath))).ThrowIfFailure();
                        fileSystem.OpenFile(ref tikFileRef.Ref, in tikPath, OpenMode.Read).ThrowIfFailure();
                        
                        IFile tikFile = tikFileRef.Release();
                        using var tikStream = new MemoryStream();
                        tikFile.AsStream().CopyTo(tikStream);
                        tikStream.Position = 0;
                        
                        var ticket = new LibHac.Tools.Es.Ticket(tikStream);
                        byte[] tKey = ticket.GetTitleKey(_keysService.CurrentKeyset);
                        string rightsIdStr = BitConverter.ToString(ticket.RightsId).Replace("-", "").ToLowerInvariant();
                        titleKeyMap[rightsIdStr] = tKey;
                    }
                    catch { }
                }

                var entries = fileSystem.EnumerateEntries().ToList();
                int totalFiles = entries.Count;
                int currentFile = 0;

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                foreach (var entry in entries)
                {
                    token.ThrowIfCancellationRequested();
                    currentFile++;
                    string entryName = entry.Name;
                    
                    if (stopwatch.ElapsedMilliseconds > 100 || currentFile == totalFiles)
                    {
                        App.RunOnUI(() => 
                        {
                            task.LogDetails = $"Извлечение: {entryName}";
                            task.Progress = (currentFile / (double)totalFiles) * 100.0;
                        });
                        stopwatch.Restart();
                    }

                    using var fileRefOut = new UniqueRef<IFile>();
                    using var entryPath = new LibHac.Fs.Path();
                    entryPath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(entry.FullPath))).ThrowIfFailure();
                    fileSystem.OpenFile(ref fileRefOut.Ref, in entryPath, OpenMode.Read).ThrowIfFailure();
                    IFile fileRef = fileRefOut.Release();
                    IStorage entryStorage = fileRef.AsStorage();

                    if (entryName.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) || entryName.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase))
                    {
                        bool isNcz = entryName.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase);
                        if (isNcz) 
                        {
                            entryStorage = new Core.NSZ.StormNczStorage(entryStorage, titleKeyMap, null, _keysService.CurrentKeyset);
                            entryName = System.IO.Path.ChangeExtension(entryName, ".nca");
                        }
                        
                        var nca = new Nca(_keysService.CurrentKeyset, entryStorage);
                        
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
                            SaveStorageToFile(entryStorage, System.IO.Path.Combine(finalOutPath, entryName));
                        }
                    }
                    else
                    {
                        SaveStorageToFile(entryStorage, System.IO.Path.Combine(finalOutPath, entryName));
                    }
                }
            }, token);
        }

        public async Task ConvertContainerAsync(ProcessingTask task, string inputPath, string outDir, string targetFormat, CancellationToken cancellationToken)
        {
            string targetExt = targetFormat.ToLower() == "xci" || targetFormat.ToLower() == "xcz" ? ".xci" : ".nsp";
            bool isInputXci = inputPath.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) || inputPath.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase);
            
            App.RunOnUI(() =>
            {
                task.LogDetails += $"\nНачало конвертации контейнера в {targetExt.ToUpper()}...";
                task.Status = $"Конвертация в {targetExt.ToUpper()}...";
            });

            string expectedNsp = System.IO.Path.ChangeExtension(System.IO.Path.Combine(outDir, System.IO.Path.GetFileName(inputPath)), ".nsp");

            // If input is NSP and target is XCI — build Trimmed XCI directly with SmartShrink filtering
            if (!isInputXci && targetExt == ".xci")
            {
                App.RunOnUI(() => task.LogDetails += "\nКонвертация NSP → XCI (HFS0 Trimmed)...");
                string expectedXci = System.IO.Path.ChangeExtension(expectedNsp, ".xci");
                Core.NCA.StormXciBuilder.BuildXciFromPfs0(inputPath, expectedXci, _keysService.CurrentKeyset);
                expectedNsp = expectedXci;
                
                App.RunOnUI(() =>
                {
                    task.LogDetails += $"\nСохранен: {expectedNsp}";
                    task.Progress = 100;
                    task.Status = "Успешно";
                });
                return;
            }
            
            // If input is NSP and target is NSP — rebuild with SmartShrink if Delta/junk exist, else copy
            if (!isInputXci && targetExt == ".nsp")
            {
                bool rebuiltShrunk = false;
                try
                {
                    using var srcFs = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    var srcPfs = new PartitionFileSystem(srcFs.AsStorage());
                    bool removeDelta = App.Settings?.Current?.RemoveDeltaNca ?? true;
                    var redundantSet = SmartShrinkService.AnalyzeRedundantEntriesInFileSystem(
                        srcPfs, _keysService.CurrentKeyset, removeDelta, out int dCount, out int sCount);
                    bool hasNonContainerJunk = srcPfs.EnumerateEntries("/", "*")
                        .Any(e => !SmartShrinkService.IsValidSwitchContainerEntry(e.Name, allowNcz: true));

                    if ((redundantSet.Count > 0 || hasNonContainerJunk) && !inputPath.Equals(expectedNsp, StringComparison.OrdinalIgnoreCase))
                    {
                        App.RunOnUI(() => task.LogDetails += $"\n[SmartShrink] Оптимизация NSP (удалено Delta={dCount}, устаревших={sCount})...");
                        var pfsBuilder = new PartitionFileSystemBuilder();
                        var openedFiles = new List<IFile>();
                        try
                        {
                            var addedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var entry in srcPfs.EnumerateEntries("/", "*"))
                            {
                                if (!SmartShrinkService.IsValidSwitchContainerEntry(entry.Name, allowNcz: true)) continue;
                                if (redundantSet.Contains(entry.Name)) continue;
                                if (!addedNames.Add(entry.Name)) continue;
                                IFile f = OpenFileSafe(srcPfs, entry.FullPath);
                                openedFiles.Add(f);
                                pfsBuilder.AddFile(entry.Name, new StorageFile(new SafeStorageWrapper(f.AsStorage()), OpenMode.Read));
                            }
                            using var built = pfsBuilder.Build(PartitionFileSystemType.Standard);
                            using var outStream = new FileStream(expectedNsp, FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024 * 1024);
                            built.AsStream().CopyTo(outStream);
                            rebuiltShrunk = true;
                        }
                        finally
                        {
                            foreach (var f in openedFiles) { try { f.Dispose(); } catch { } }
                        }
                    }
                }
                catch { }

                if (!rebuiltShrunk)
                {
                    App.RunOnUI(() => task.LogDetails += "\nКонтейнер NSP: копирование...");
                    if (!inputPath.Equals(expectedNsp, StringComparison.OrdinalIgnoreCase))
                    {
                        System.IO.File.Copy(inputPath, expectedNsp, true);
                    }
                }
                App.RunOnUI(() =>
                {
                    task.LogDetails += $"\nСохранен: {expectedNsp}";
                    task.Progress = 100;
                    task.Status = "Успешно";
                });
                return;
            }

            // XCI → NSP (or XCI → Trimmed XCI) conversion: parse HFS0 structure with Lossless Shrink filtering
            await Task.Run(() =>
            {
                var openedFileSystems = new List<IFileSystem>();
                var openedFiles = new List<IFile>();
                var keepAliveReferences = new List<object>();

                try
                {
                    using var fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    IStorage storage = fs.AsStorage();

                    storage.GetSize(out long storageSize).ThrowIfFailure();
                    var rootStorage = new SubStorage(storage, 0x10000, storageSize - 0x10000);
                    keepAliveReferences.Add(rootStorage);
                    var rootPfs = new PartitionFileSystem(rootStorage);
                    openedFileSystems.Add(rootPfs);
                    
                    IFile secureFile = OpenFileSafe(rootPfs, "/secure");
                    openedFiles.Add(secureFile);
                    IStorage secureStorage = secureFile.AsStorage();
                    keepAliveReferences.Add(secureStorage);
                    
                    var securePfs = new PartitionFileSystem(secureStorage);
                    openedFileSystems.Add(securePfs);

                    bool removeDelta = App.Settings?.Current?.RemoveDeltaNca ?? true;
                    var redundantSet = SmartShrinkService.AnalyzeRedundantEntriesInFileSystem(
                        securePfs, _keysService.CurrentKeyset, removeDelta, out int deltaRemoved, out int supRemoved);

                    if (deltaRemoved > 0 || supRemoved > 0)
                    {
                        App.RunOnUI(() => task.LogDetails += $"\n[SmartShrink] Исключено избыточных NCA: Delta={deltaRemoved}, устаревших патчей={supRemoved}");
                    }

                    var pfsBuilder = new PartitionFileSystemBuilder();
                    var addedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var entry in securePfs.EnumerateEntries())
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!SmartShrinkService.IsValidSwitchContainerEntry(entry.Name, allowNcz: true))
                            continue;
                        if (redundantSet.Contains(entry.Name))
                            continue;
                        if (!addedNames.Add(entry.Name))
                            continue;

                        IFile entryFile = OpenFileSafe(securePfs, entry.FullPath);
                        openedFiles.Add(entryFile);
                        IStorage entryStorage = entryFile.AsStorage();
                        keepAliveReferences.Add(entryStorage);
                        pfsBuilder.AddFile(entry.Name, new StorageFile(new StormSwitchBox.Services.SafeStorageWrapper(entryStorage), OpenMode.Read));
                    }

                    App.RunOnUI(() => task.LogDetails += "\nСборка NSP контейнера...");
                    using var builtPfs = pfsBuilder.Build(PartitionFileSystemType.Standard);
                    using var destStream = new FileStream(expectedNsp, FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024 * 1024);
                    
                    builtPfs.GetSize(out long totalPfsSize).ThrowIfFailure();
                    long remaining = totalPfsSize;
                    long offset = 0;
                    byte[] buffer = new byte[4 * 1024 * 1024];
                    while (remaining > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int toRead = (int)Math.Min(buffer.Length, remaining);
                        builtPfs.Read(offset, buffer.AsSpan(0, toRead)).ThrowIfFailure();
                        destStream.Write(buffer, 0, toRead);
                        offset += toRead;
                        remaining -= toRead;
                        
                        double percent = 100.0 - ((double)remaining / totalPfsSize * 100.0);
                        if (task.Progress != (int)percent)
                        {
                            App.RunOnUI(() => task.Progress = (int)percent);
                        }
                    }
                }
                finally
                {
                    foreach (var f in openedFiles)
                    {
                        try { f.Dispose(); } catch { }
                    }
                    foreach (var sys in openedFileSystems)
                    {
                        try { sys.Dispose(); } catch { }
                    }
                    foreach (var refObj in keepAliveReferences)
                    {
                        if (refObj is IDisposable disp)
                        {
                            try { disp.Dispose(); } catch { }
                        }
                    }
                }
            }, cancellationToken);

            if (targetExt == ".xci")
            {
                App.RunOnUI(() => task.LogDetails += "\nСборка HFS0 (XCI Trimmed)...");
                string expectedXci = System.IO.Path.ChangeExtension(expectedNsp, ".xci");
                Core.NCA.StormXciBuilder.BuildXciFromPfs0(expectedNsp, expectedXci, _keysService.CurrentKeyset);
                try { System.IO.File.Delete(expectedNsp); } catch { }
                expectedNsp = expectedXci;
            }

            App.RunOnUI(() =>
            {
                task.LogDetails += $"\nСохранен: {expectedNsp}";
                task.Progress = 100;
                task.Status = "Успешно";
            });
        }

        public void ExtractNcaSection(Nca nca, NcaSectionType sectionType, string outDir, System.Threading.CancellationToken token, string? ncaFilePath = null)
        {
            if (!nca.CanOpenSection(sectionType)) return;
            
            System.IO.Directory.CreateDirectory(outDir);
            
            try 
            {
                LibHac.Fs.Fsa.IFileSystem? fs = null;
                if (nca.Header.GetFsHeader((int)sectionType).FormatType == LibHac.Tools.FsSystem.NcaUtils.NcaFormatType.Romfs)
                {
                    try
                    {
                        var storage = nca.OpenStorage((int)sectionType, IntegrityCheckLevel.None);
                        var wrappedStorage = new UnalignedStorageWrapper(storage);
                        fs = new LibHac.Tools.FsSystem.RomFs.RomFsFileSystem(wrappedStorage);
                    }
                    catch
                    {
                        try { fs = nca.OpenFileSystem((int)sectionType, IntegrityCheckLevel.None); } catch { }
                        if (fs == null) try { fs = nca.OpenFileSystem((int)sectionType, IntegrityCheckLevel.IgnoreOnInvalid); } catch { }
                    }
                }
                else
                {
                    try
                    {
                        var storage = nca.OpenStorage((int)sectionType, IntegrityCheckLevel.None);
                        var wrappedStorage = new UnalignedStorageWrapper(storage);
                        var pfs = new LibHac.FsSystem.PartitionFileSystem(wrappedStorage);
                        fs = pfs;
                    }
                    catch
                    {
                        try { fs = nca.OpenFileSystem((int)sectionType, IntegrityCheckLevel.None); } catch { }
                        if (fs == null) try { fs = nca.OpenFileSystem((int)sectionType, IntegrityCheckLevel.IgnoreOnInvalid); } catch { }
                    }
                }
                
                if (fs != null)
                {
                    try
                    {
                        ExtractDirectoryRecursively(fs, "/", outDir, token);
                        return; // success via LibHac
                    }
                    finally
                    {
                        if (fs is IDisposable d) d.Dispose();
                    }
                }
            }
            catch { /* LibHac failed, will try hactoolnet fallback */ }

            // Fallback: use hactoolnet.exe
            if (!string.IsNullOrEmpty(ncaFilePath) && System.IO.File.Exists(ncaFilePath))
            {
                ExtractNcaSectionViaHactool(ncaFilePath, null, sectionType, outDir, token);
            }
            else
            {
                throw new Exception($"Не удалось извлечь секцию {sectionType} через LibHac API (путь к NCA не указан для fallback).");
            }
        }

        public void ExtractNcaSection(Nca baseNca, Nca patchNca, NcaSectionType sectionType, string outDir, System.Threading.CancellationToken token, string? baseNcaFilePath = null, string? patchNcaFilePath = null)
        {
            if (!baseNca.CanOpenSection(sectionType)) return;
            
            // If patch NCA does not have the section, extract from base NCA directly
            if (!patchNca.CanOpenSection(sectionType))
            {
                ExtractNcaSection(baseNca, sectionType, outDir, token, baseNcaFilePath);
                return;
            }
            
            System.IO.Directory.CreateDirectory(outDir);
            
            try 
            {
                LibHac.Fs.Fsa.IFileSystem? fs = null;
                if (baseNca.Header.GetFsHeader((int)sectionType).FormatType == LibHac.Tools.FsSystem.NcaUtils.NcaFormatType.Romfs)
                {
                    try
                    {
                        var storage = baseNca.OpenStorageWithPatch(patchNca, (int)sectionType, IntegrityCheckLevel.None);
                        var wrappedStorage = new UnalignedStorageWrapper(storage);
                        fs = new LibHac.Tools.FsSystem.RomFs.RomFsFileSystem(wrappedStorage);
                    }
                    catch
                    {
                        try { fs = baseNca.OpenFileSystemWithPatch(patchNca, (int)sectionType, IntegrityCheckLevel.None); } catch { }
                        if (fs == null) try { fs = baseNca.OpenFileSystemWithPatch(patchNca, (int)sectionType, IntegrityCheckLevel.IgnoreOnInvalid); } catch { }
                    }
                }
                else
                {
                    try
                    {
                        var storage = baseNca.OpenStorageWithPatch(patchNca, (int)sectionType, IntegrityCheckLevel.None);
                        var wrappedStorage = new UnalignedStorageWrapper(storage);
                        var pfs = new LibHac.FsSystem.PartitionFileSystem(wrappedStorage);
                        fs = pfs;
                    }
                    catch
                    {
                        try { fs = baseNca.OpenFileSystemWithPatch(patchNca, sectionType, IntegrityCheckLevel.None); } catch { }
                        if (fs == null) try { fs = baseNca.OpenFileSystemWithPatch(patchNca, sectionType, IntegrityCheckLevel.IgnoreOnInvalid); } catch { }
                    }
                }
                
                if (fs != null)
                {
                    try
                    {
                        ExtractDirectoryRecursively(fs, "/", outDir, token);
                        return; // success via LibHac
                    }
                    finally
                    {
                        if (fs is IDisposable d) d.Dispose();
                    }
                }
            }
            catch { /* LibHac failed, will try hactoolnet fallback */ }

            // Fallback: use hactoolnet.exe
            if (!string.IsNullOrEmpty(patchNcaFilePath) && System.IO.File.Exists(patchNcaFilePath))
            {
                ExtractNcaSectionViaHactool(patchNcaFilePath, baseNcaFilePath, sectionType, outDir, token);
            }
            else if (!string.IsNullOrEmpty(baseNcaFilePath) && System.IO.File.Exists(baseNcaFilePath))
            {
                ExtractNcaSectionViaHactool(baseNcaFilePath, null, sectionType, outDir, token);
            }
            else
            {
                throw new Exception($"Не удалось извлечь секцию {sectionType} через LibHac API (пути к NCA не указаны для fallback).");
            }
        }

        private void ExtractNcaSectionViaHactool(string ncaPath, string? baseNcaPath, NcaSectionType sectionType, string outDir, System.Threading.CancellationToken token)
        {
            string hactoolPath = FindHactoolnet();
            string keysPath = _keysService.KeysFilePath ?? System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "prod.keys");
            string baseArg = !string.IsNullOrEmpty(baseNcaPath) ? $"--basenca \"{baseNcaPath}\"" : "";

            // === Attempt 1: Direct extraction (--romfsdir / --exefsdir) ===
            try
            {
                string sectionArg = sectionType == NcaSectionType.Code
                    ? $"--exefsdir \"{outDir}\""
                    : $"--romfsdir \"{outDir}\"";

                string args1 = $"-t nca -k \"{keysPath}\" {baseArg} {sectionArg} --disablekeywarns \"{ncaPath}\"";
                App.Logger.Log($"[hactoolnet] Попытка 1 (прямое извлечение): {args1}", Models.LogLevel.Info);

                int exitCode1 = RunHactool(hactoolPath, args1, token);
                if (exitCode1 == 0 && System.IO.Directory.EnumerateFileSystemEntries(outDir).Any())
                {
                    App.Logger.Log("[hactoolnet] Попытка 1 успешна.", Models.LogLevel.Info);
                    return;
                }
            }
            catch { }

            // === Attempt 2: Two-step extraction (dump raw section → parse separately) ===
            App.Logger.Log("[hactoolnet] Попытка 2 (двухэтапный дамп)...", Models.LogLevel.Info);

            string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "StormHactool_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            System.IO.Directory.CreateDirectory(tempDir);

            try
            {
                int[] sectionIndicesToTry = sectionType == NcaSectionType.Code
                    ? new[] { 0, 1 }
                    : new[] { 1, 0 };

                foreach (int sectionIdx in sectionIndicesToTry)
                {
                    token.ThrowIfCancellationRequested();
                    string tempSectionFile = System.IO.Path.Combine(tempDir, $"section{sectionIdx}.bin");

                    string dumpArgs = $"-t nca -k \"{keysPath}\" {baseArg} --section{sectionIdx} \"{tempSectionFile}\" --disablekeywarns \"{ncaPath}\"";
                    App.Logger.Log($"[hactoolnet] Дамп секции {sectionIdx}: {dumpArgs}", Models.LogLevel.Info);

                    int dumpExit = RunHactool(hactoolPath, dumpArgs, token);
                    if (dumpExit != 0 || !System.IO.File.Exists(tempSectionFile) || new FileInfo(tempSectionFile).Length == 0)
                    {
                        App.Logger.Log($"[hactoolnet] Дамп секции {sectionIdx} не удался, пропуск.", Models.LogLevel.Debug);
                        continue;
                    }

                    string parseType = sectionType == NcaSectionType.Code ? "pfs0" : "romfs";
                    string parseOutArg = sectionType == NcaSectionType.Code
                        ? $"--outdir \"{outDir}\""
                        : $"--romfsdir \"{outDir}\"";

                    string parseArgs = $"-t {parseType} {parseOutArg} \"{tempSectionFile}\"";
                    App.Logger.Log($"[hactoolnet] Парсинг секции как {parseType}: {parseArgs}", Models.LogLevel.Info);

                    RunHactool(hactoolPath, parseArgs, token);
                    if (System.IO.Directory.EnumerateFileSystemEntries(outDir).Any())
                    {
                        App.Logger.Log($"[hactoolnet] Попытка 2 успешна (секция {sectionIdx} как {parseType}).", Models.LogLevel.Info);
                        return;
                    }

                    string altType = parseType == "romfs" ? "pfs0" : "romfs";
                    string altOutArg = altType == "pfs0" ? $"--outdir \"{outDir}\"" : $"--romfsdir \"{outDir}\"";
                    string altArgs = $"-t {altType} {altOutArg} \"{tempSectionFile}\"";
                    App.Logger.Log($"[hactoolnet] Попытка альтернативного парсинга как {altType}...", Models.LogLevel.Info);

                    RunHactool(hactoolPath, altArgs, token);
                    if (System.IO.Directory.EnumerateFileSystemEntries(outDir).Any())
                    {
                        App.Logger.Log($"[hactoolnet] Альтернативный парсинг успешен (секция {sectionIdx} как {altType}).", Models.LogLevel.Info);
                        return;
                    }
                }

                throw new Exception($"Не удалось извлечь секцию {sectionType} ни одним методом.");
            }
            finally
            {
                try { System.IO.Directory.Delete(tempDir, true); } catch { }
            }
        }

        private string FindHactoolnet()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] candidatePaths = new[]
            {
                System.IO.Path.Combine(baseDir, "tools", "com.github.nozwock.yanu", "hactoolnet.exe"),
                System.IO.Path.Combine(baseDir, "..", "tools", "com.github.nozwock.yanu", "hactoolnet.exe"),
                System.IO.Path.Combine(baseDir, "..", "..", "..", "..", "tools", "com.github.nozwock.yanu", "hactoolnet.exe"),
                System.IO.Path.Combine(baseDir, "..", "..", "tools", "com.github.nozwock.yanu", "hactoolnet.exe")
            };
            foreach (var candidate in candidatePaths)
            {
                string fullPath = System.IO.Path.GetFullPath(candidate);
                if (System.IO.File.Exists(fullPath)) return fullPath;
            }
            throw new Exception($"hactoolnet.exe не найден. Проверены пути: {string.Join(", ", candidatePaths.Select(p => System.IO.Path.GetFullPath(p)))}");
        }

        private int RunHactool(string hactoolPath, string arguments, System.Threading.CancellationToken token)
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = hactoolPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process == null) return -1;

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            token.ThrowIfCancellationRequested();

            if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(stderr))
            {
                App.Logger.Log($"[hactoolnet] exit={process.ExitCode} stderr: {stderr.Trim()}", Models.LogLevel.Debug);
            }

            return process.ExitCode;
        }

        public void TrimLanguages(string romfsDir)
        {
            try
            {
                var keepLangs = App.Settings.Current.KeepLanguages ?? new List<string> { "ru", "ru-RU", "en-US", "en-GB", "en" };
                var keepSet = new HashSet<string>(keepLangs, StringComparer.OrdinalIgnoreCase);

                bool ShouldKeepLanguageFolder(string folderName)
                {
                    if (keepSet.Contains(folderName)) return true;
                    string lower = folderName.ToLowerInvariant();
                    if ((lower == "russian" || lower.StartsWith("ru-")) && (keepSet.Contains("ru") || keepSet.Contains("ru-RU"))) return true;
                    if ((lower == "english" || lower.StartsWith("en-")) && (keepSet.Contains("en") || keepSet.Contains("en-US") || keepSet.Contains("en-GB"))) return true;
                    if ((lower == "japanese" || lower.StartsWith("ja-")) && (keepSet.Contains("ja") || keepSet.Contains("ja-JP"))) return true;
                    if ((lower == "spanish" || lower.StartsWith("es-")) && (keepSet.Contains("es") || keepSet.Contains("es-ES") || keepSet.Contains("es-MX"))) return true;
                    if ((lower == "french" || lower.StartsWith("fr-")) && (keepSet.Contains("fr") || keepSet.Contains("fr-FR") || keepSet.Contains("fr-CA"))) return true;
                    if ((lower == "german" || lower.StartsWith("de-")) && (keepSet.Contains("de") || keepSet.Contains("de-DE"))) return true;
                    if ((lower == "italian" || lower.StartsWith("it-")) && (keepSet.Contains("it") || keepSet.Contains("it-IT"))) return true;
                    if ((lower == "dutch" || lower.StartsWith("nl-")) && (keepSet.Contains("nl") || keepSet.Contains("nl-NL"))) return true;
                    if ((lower == "portuguese" || lower.StartsWith("pt-")) && (keepSet.Contains("pt") || keepSet.Contains("pt-BR") || keepSet.Contains("pt-PT"))) return true;
                    if ((lower == "korean" || lower.StartsWith("ko-")) && (keepSet.Contains("ko") || keepSet.Contains("ko-KR"))) return true;
                    if ((lower == "chinese" || lower.StartsWith("zh-")) && (keepSet.Contains("zh") || keepSet.Contains("zh-CN") || keepSet.Contains("zh-TW") || keepSet.Contains("zh-Hans") || keepSet.Contains("zh-Hant"))) return true;
                    return false;
                }

                var possibleLangDirs = new[] { "Message", "Voice", "Sound", "Localized", "Loc", "Text", "ui" };
                
                foreach (var dir in possibleLangDirs)
                {
                    string targetDir = System.IO.Path.Combine(romfsDir, dir);
                    if (System.IO.Directory.Exists(targetDir))
                    {
                        var subDirs = System.IO.Directory.GetDirectories(targetDir);
                        foreach (var subDir in subDirs)
                        {
                            string dirName = System.IO.Path.GetFileName(subDir);
                            if (dirName.Contains("-") && dirName.Length >= 4 && dirName.Length <= 5)
                            {
                                if (!ShouldKeepLanguageFolder(dirName))
                                {
                                    SafeDeleteDirectory(subDir);
                                }
                            }
                            else if (dirName.Equals("Japanese", StringComparison.OrdinalIgnoreCase) || 
                                     dirName.Equals("Spanish", StringComparison.OrdinalIgnoreCase) || 
                                     dirName.Equals("French", StringComparison.OrdinalIgnoreCase) || 
                                     dirName.Equals("German", StringComparison.OrdinalIgnoreCase) || 
                                     dirName.Equals("Italian", StringComparison.OrdinalIgnoreCase) || 
                                     dirName.Equals("Dutch", StringComparison.OrdinalIgnoreCase) || 
                                     dirName.Equals("Portuguese", StringComparison.OrdinalIgnoreCase) || 
                                     dirName.Equals("Korean", StringComparison.OrdinalIgnoreCase) || 
                                     dirName.Equals("Chinese", StringComparison.OrdinalIgnoreCase) ||
                                     dirName.Equals("es-ES", StringComparison.OrdinalIgnoreCase) ||
                                     dirName.Equals("es-MX", StringComparison.OrdinalIgnoreCase) ||
                                     dirName.Equals("fr-FR", StringComparison.OrdinalIgnoreCase) ||
                                     dirName.Equals("fr-CA", StringComparison.OrdinalIgnoreCase) ||
                                     dirName.Equals("it-IT", StringComparison.OrdinalIgnoreCase) ||
                                     dirName.Equals("de-DE", StringComparison.OrdinalIgnoreCase) ||
                                     dirName.Equals("nl-NL", StringComparison.OrdinalIgnoreCase) ||
                                     dirName.Equals("pt-BR", StringComparison.OrdinalIgnoreCase) ||
                                     dirName.Equals("pt-PT", StringComparison.OrdinalIgnoreCase) ||
                                     dirName.Equals("ja-JP", StringComparison.OrdinalIgnoreCase) ||
                                     dirName.Equals("ko-KR", StringComparison.OrdinalIgnoreCase) ||
                                     dirName.Equals("zh-CN", StringComparison.OrdinalIgnoreCase) ||
                                     dirName.Equals("zh-TW", StringComparison.OrdinalIgnoreCase) ||
                                     dirName.Equals("zh-Hant", StringComparison.OrdinalIgnoreCase) ||
                                     dirName.Equals("zh-Hans", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!ShouldKeepLanguageFolder(dirName))
                                {
                                    SafeDeleteDirectory(subDir);
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private void SafeDeleteDirectory(string path)
        {
            if (!System.IO.Directory.Exists(path)) return;
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    System.IO.Directory.Delete(path, true);
                    return;
                }
                catch (System.IO.IOException)
                {
                    System.Threading.Thread.Sleep(100);
                }
                catch (System.UnauthorizedAccessException)
                {
                    System.Threading.Thread.Sleep(100);
                }
                catch
                {
                    break;
                }
            }
        }

        private void ExtractDirectoryRecursively(LibHac.Fs.Fsa.IFileSystem fs, string currentPath, string outDir, System.Threading.CancellationToken token)
        {
            foreach (var entry in fs.EnumerateEntries(currentPath, "*"))
            {
                token.ThrowIfCancellationRequested();
                string entryFullPath = entry.FullPath;
                
                string safeName = entryFullPath.TrimStart('/');
                foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                {
                    if (c != '/' && c != '\\')
                        safeName = safeName.Replace(c, '_');
                }
                
                string targetPath = System.IO.Path.Combine(outDir, safeName);

                try
                {
                    if (entry.Type == LibHac.Fs.DirectoryEntryType.Directory)
                    {
                        System.IO.Directory.CreateDirectory(targetPath);
                        ExtractDirectoryRecursively(fs, entryFullPath, outDir, token);
                    }
                    else
                    {
                        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(targetPath)!);
                        
                        using var sourceFileRef = new LibHac.Common.UniqueRef<LibHac.Fs.Fsa.IFile>();
                        using var srcPath = new LibHac.Fs.Path();
                        srcPath.Initialize(new LibHac.Common.U8Span(System.Text.Encoding.UTF8.GetBytes(entryFullPath))).ThrowIfFailure();
                        fs.OpenFile(ref sourceFileRef.Ref, in srcPath, LibHac.Fs.OpenMode.Read).ThrowIfFailure();
                        
                        using (LibHac.Fs.Fsa.IFile sourceFile = sourceFileRef.Release())
                        {
                            using var destStream = System.IO.File.Create(targetPath);
                            sourceFile.AsStream().CopyTo(destStream);
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.Log($"Пропуск файла при извлечении {entryFullPath}: {ex.Message}", Models.LogLevel.Warning);
                }
            }
        }

        private void SaveStorageToFile(IStorage storage, string path)
        {
            try
            {
                using var destStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024 * 1024);
                storage.AsStream().CopyTo(destStream);
            }
            catch (Exception ex)
            {
                App.Logger.Log($"Ошибка сохранения {System.IO.Path.GetFileName(path)}: {ex.Message}", Models.LogLevel.Warning);
            }
        }

        public async System.Threading.Tasks.Task PackContainerAsync(Models.ProcessingTask task, string inputFolder, string outFolder, string outFileName, System.Threading.CancellationToken cancellationToken)
        {
            try
            {
                App.RunOnUI(() =>
                {
                    task.LogDetails = $"Упаковка папки: {inputFolder}";
                    task.Status = "Упаковка...";
                });

                System.IO.Directory.CreateDirectory(outFolder);
                string outPath = System.IO.Path.Combine(outFolder, string.IsNullOrEmpty(outFileName) ? "Packed.nsp" : outFileName);
                if (!outPath.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase)) outPath += ".nsp";

                string romfsDir = System.IO.Path.Combine(inputFolder, "romfs");
                string exefsDir = System.IO.Path.Combine(inputFolder, "exefs");
                
                if (!System.IO.Directory.Exists(romfsDir))
                {
                    var rDirs = System.IO.Directory.GetDirectories(inputFolder, "romfs", System.IO.SearchOption.AllDirectories);
                    if (rDirs.Length > 0) romfsDir = rDirs[0];
                }
                if (!System.IO.Directory.Exists(exefsDir))
                {
                    var eDirs = System.IO.Directory.GetDirectories(inputFolder, "exefs", System.IO.SearchOption.AllDirectories);
                    if (eDirs.Length > 0) exefsDir = eDirs[0];
                }

                bool hasMods = System.IO.Directory.Exists(romfsDir) || System.IO.Directory.Exists(exefsDir);

                if (hasMods)
                {
                    App.RunOnUI(() => task.LogDetails = "Обнаружены папки romfs/exefs. Запуск перепаковки в NSP...");

                    // Lossless Shrink Engine: очистка временного мусора и оптимизация PNG перед упаковкой RomFS
                    if (System.IO.Directory.Exists(romfsDir))
                    {
                        ModOptimizerService.CleanJunkFiles(romfsDir, task);
                        if (App.Settings?.Current?.OptimizeModAssets ?? true)
                        {
                            await ModOptimizerService.OptimizeTexturesAsync(romfsDir, task, cancellationToken);
                        }
                    }

                    string controlNca = "";
                    string baseProgramNca = "";
                    string titleId = "";
                    var allNcas = System.IO.Directory.GetFiles(inputFolder, "*.nca", System.IO.SearchOption.AllDirectories);
                    foreach (var ncaPath in allNcas)
                    {
                        try
                        {
                            using (var fs = new FileStream(ncaPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                            {
                                var nca = new Nca(_keysService.CurrentKeyset, fs.AsStorage());
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
                        }
                        catch { }
                    }

                    if (string.IsNullOrEmpty(controlNca))
                    {
                        throw new Exception("Не найден control.nca. Перепаковка romfs/exefs невозможна.");
                    }
                    if (string.IsNullOrEmpty(baseProgramNca))
                    {
                        throw new Exception("Не найден базовый Program NCA. Перепаковка romfs/exefs невозможна.");
                    }
                    if (string.IsNullOrEmpty(titleId)) titleId = "0100000000000000";

                    string yanuCliPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "yanu-cli.exe");
                    if (!System.IO.File.Exists(yanuCliPath))
                    {
                        string fallback = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "tools", "yanu-cli.exe");
                        if (System.IO.File.Exists(fallback)) yanuCliPath = fallback;
                        else throw new Exception("yanu-cli.exe не найден.");
                    }

                    string tempOut = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "StormPack_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                    System.IO.Directory.CreateDirectory(tempOut);

                    try
                    {
                        string keysPath = App.Settings.Current.KeysPath;
                        if (string.IsNullOrEmpty(keysPath) || !File.Exists(keysPath))
                        {
                            keysPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "keys.txt");
                        }
                        string keyfileFlag = (!string.IsNullOrEmpty(keysPath) && File.Exists(keysPath)) ? $"-k \"{keysPath}\" " : "";

                        string packArgs = $"{keyfileFlag}pack --titleid {titleId} --controlnca \"{controlNca}\" --romfsdir \"{romfsDir}\" --exefsdir \"{exefsDir}\" -o \"{tempOut}\"";
                        var packPsi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = yanuCliPath,
                            Arguments = packArgs,
                            WorkingDirectory = tempOut,
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true,
                            StandardOutputEncoding = System.Text.Encoding.UTF8,
                            StandardErrorEncoding = System.Text.Encoding.UTF8
                        };
                        string localAppDataDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StormSwitchBox");
                        packPsi.EnvironmentVariables["USERPROFILE"] = System.IO.Path.Combine(localAppDataDir, "user_profile");
                        packPsi.EnvironmentVariables["LOCALAPPDATA"] = System.IO.Path.Combine(localAppDataDir, "cache");
                        packPsi.EnvironmentVariables["APPDATA"] = System.IO.Path.Combine(localAppDataDir, "cache");
                        packPsi.EnvironmentVariables["TEMP"] = tempOut;
                        packPsi.EnvironmentVariables["TMP"] = tempOut;
                        
                        using var packProcess = System.Diagnostics.Process.Start(packPsi);
                        if (packProcess == null) throw new Exception("Не удалось запустить yanu-cli pack.");
                        
                        string stderr = await packProcess.StandardError.ReadToEndAsync();
                        await packProcess.WaitForExitAsync(cancellationToken);
                        
                        if (packProcess.ExitCode != 0) throw new Exception($"Ошибка yanu-cli pack:\n{stderr}");

                        var genFiles = System.IO.Directory.GetFiles(tempOut, "*.nsp");
                        if (genFiles.Length > 0)
                        {
                            outPath = await SafeFileOperations.SafeMoveOrReplaceFileAsync(genFiles[0], outPath, task, cancellationToken);
                        }
                        else
                        {
                            throw new Exception("Сборка через yanu-cli не выдала .nsp файл.");
                        }
                    }
                    finally
                    {
                        try { System.IO.Directory.Delete(tempOut, true); } catch { }
                    }
                }
                else
                {
                    App.RunOnUI(() => task.LogDetails = "Сборка PFS0 контейнера из NCA файлов (SmartShrink)...");
                    var pfsBuilder = new PartitionFileSystemBuilder();

                    await System.Threading.Tasks.Task.Run(() => 
                    {
                        var openedStreams = new List<FileStream>();
                        try
                        {
                            bool removeDelta = App.Settings?.Current?.RemoveDeltaNca ?? true;
                            var redundantSet = SmartShrinkService.AnalyzeRedundantEntriesInDirectory(
                                inputFolder, _keysService.CurrentKeyset, removeDelta, out int dCount, out int sCount);

                            if (dCount > 0 || sCount > 0)
                            {
                                App.RunOnUI(() => task.LogDetails += $"\n[SmartShrink] Исключено избыточных NCA: Delta={dCount}, устаревших={sCount}");
                            }

                            var addedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            var existingFiles = System.IO.Directory.GetFiles(inputFolder, "*.*", System.IO.SearchOption.AllDirectories);
                            foreach (var file in existingFiles)
                            {
                                string fName = System.IO.Path.GetFileName(file);
                                if (!SmartShrinkService.IsValidSwitchContainerEntry(fName, allowNcz: false)) continue;
                                if (redundantSet.Contains(fName)) continue;
                                if (!addedNames.Add(fName)) continue;

                                var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                                openedStreams.Add(fs);
                                pfsBuilder.AddFile(fName, new StorageFile(fs.AsStorage(), OpenMode.Read));
                            }

                            if (openedStreams.Count == 0)
                            {
                                throw new Exception("В выбранной папке нет валидных NCA файлов для упаковки.");
                            }

                            using var outStream = new FileStream(outPath, FileMode.Create, FileAccess.ReadWrite);
                            using var builtPfs = pfsBuilder.Build(PartitionFileSystemType.Standard);
                            builtPfs.AsStream().CopyTo(outStream);
                        }
                        finally
                        {
                            foreach (var fs in openedStreams)
                            {
                                try { fs.Dispose(); } catch { }
                            }
                        }
                    }, cancellationToken);
                }

                App.RunOnUI(() =>
                {
                    task.Progress = 100;
                    task.Status = "Успешно";
                    task.LogDetails += $"\nУспешно упаковано!\nФайл: {outPath}";
                });

                App.Logger.Log($"Упаковка успешно завершена: {System.IO.Path.GetFileName(outPath)}", Models.LogLevel.Success);
            }
            catch (Exception ex)
            {
                App.RunOnUI(() => task.Status = "Ошибка");
                App.Logger.Log($"Ошибка при упаковке: {ex.Message}", Models.LogLevel.Error);
                throw;
            }
        }

        private string ExtractTitleIdFromControlNca(string ncaPath)
        {
            try
            {
                using (var fs = new FileStream(ncaPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    var nca = new Nca(_keysService.CurrentKeyset, fs.AsStorage());
                    return nca.Header.TitleId.ToString("X16").ToLower();
                }
            }
            catch { }
            return "";
        }

        public async Task VerifyNspAsync(ProcessingTask task, string filePath, CancellationToken cancellationToken)
        {
            try
            {
                App.RunOnUI(() =>
                {
                    task.Status = "Проверка целостности...";
                    task.LogDetails = $"Запуск глубокой проверки файла: {System.IO.Path.GetFileName(filePath)}";
                });

                if (!_keysService.IsLoaded)
                    throw new Exception("Криптографические ключи не загружены.");

                string verifyType = "";
                string structureStatus = "";
                string titleId = "UNKNOWN";
                string version = "v0";
                string mergedStatus = "";
                
                bool hasBaseProgram = false;
                bool hasUpdateProgram = false;
                bool hasControl = false;
                bool hasDlc = false;
                bool hasCode = false;
                bool hasData = false;
                bool hasReadErrors = false;

                var sbDetails = new StringBuilder();
                sbDetails.AppendLine($"[Инспектор] {System.IO.Path.GetFileName(filePath)}");

                await Task.Run(() =>
                {
                    bool isXci = filePath.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase);
                    using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    IStorage storage = fileStream.AsStorage();

                    PartitionFileSystem pfs;
                    UniqueRef<IFile> secureFileRef = default;
                    PartitionFileSystem? rootPfs = null;

                    if (isXci)
                    {
                        storage.GetSize(out long storageSize).ThrowIfFailure();
                        var rootStorage = new SubStorage(storage, 0x10000, storageSize - 0x10000);
                        rootPfs = new PartitionFileSystem(rootStorage);
                        using var securePath = new LibHac.Fs.Path();
                        securePath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes("/secure"))).ThrowIfFailure();
                        rootPfs.OpenFile(ref secureFileRef.Ref, in securePath, OpenMode.Read).ThrowIfFailure();
                        pfs = new PartitionFileSystem(secureFileRef.Get.AsStorage());
                    }
                    else
                    {
                        pfs = new PartitionFileSystem(storage);
                    }

                    using (secureFileRef)
                    {
                        SmartShrinkService.HarvestTicketsFromFileSystem(pfs, _keysService.CurrentKeyset);

                        // Сбор Title Keys из билетов (.tik)
                        var titleKeyMap = new System.Collections.Generic.Dictionary<string, byte[]>();
                        foreach (var entry in pfs.EnumerateEntries().Where(e => e.Name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase)))
                        {
                            try
                            {
                                using var tikFileRef = new UniqueRef<IFile>();
                                using var tikPath = new LibHac.Fs.Path();
                                tikPath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(entry.FullPath))).ThrowIfFailure();
                                pfs.OpenFile(ref tikFileRef.Ref, in tikPath, OpenMode.Read).ThrowIfFailure();
                                
                                IFile tikFile = tikFileRef.Release();
                                using var tikStream = new MemoryStream();
                                tikFile.AsStream().CopyTo(tikStream);
                                tikStream.Position = 0;
                                
                                var ticket = new LibHac.Tools.Es.Ticket(tikStream);
                                byte[] tKey = ticket.GetTitleKey(_keysService.CurrentKeyset);
                                string rightsIdStr = BitConverter.ToString(ticket.RightsId).Replace("-", "").ToLowerInvariant();
                                titleKeyMap[rightsIdStr] = tKey;
                            }
                            catch { }
                        }

                        var entries = pfs.EnumerateEntries().ToList();
                        long totalSize = Math.Max(1, entries.Sum(e => e.Size));
                        long processedSize = 0;
                        bool containerHasBaseCnmt = entries.Any(e => e.Name.EndsWith(".cnmt.nca", StringComparison.OrdinalIgnoreCase) && (e.Name.Contains("000") || e.Name.Contains("base")));
                        bool containerHasUpdateCnmt = entries.Any(e => e.Name.EndsWith(".cnmt.nca", StringComparison.OrdinalIgnoreCase) && (e.Name.Contains("800") || e.Name.Contains("patch") || e.Name.Contains("update")));

                        sbDetails.AppendLine($"[Контейнер] Всего объектов внутри архива: {entries.Count}");

                        foreach (var entry in entries)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            string entryName = entry.Name;

                            if (entryName.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) || 
                                entryName.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase))
                            {
                                App.RunOnUI(() => task.LogDetails = $"[{processedSize * 100 / totalSize}%] Проверка секций: {entryName}");

                                using var fileRefOut = new UniqueRef<IFile>();
                                using var entryPath = new LibHac.Fs.Path();
                                entryPath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(entry.FullPath))).ThrowIfFailure();
                                pfs.OpenFile(ref fileRefOut.Ref, in entryPath, OpenMode.Read).ThrowIfFailure();
                                IStorage ncaStorage = fileRefOut.Release().AsStorage();
                                
                                bool isNcz = entryName.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase);
                                if (isNcz)
                                {
                                    ncaStorage = new Core.NSZ.StormNczStorage(ncaStorage, titleKeyMap, null, _keysService.CurrentKeyset);
                                }

                                try 
                                {
                                    var nca = new Nca(_keysService.CurrentKeyset, ncaStorage);
                                    string ncaTid = nca.Header.TitleId.ToString("X16").ToUpper();
                                    titleId = ncaTid;

                                    string typeName = nca.Header.ContentType.ToString();
                                    sbDetails.AppendLine($"  - Архив: {entryName} | Тип: {typeName} | Title ID: {ncaTid}");

                                    if (nca.Header.ContentType == NcaContentType.Program)
                                    {
                                        bool cCode = nca.CanOpenSection(NcaSectionType.Code);
                                        bool cData = nca.CanOpenSection(NcaSectionType.Data);
                                        
                                        if (cCode) hasCode = true;
                                        if (cData) hasData = true;

                                        sbDetails.AppendLine($"    * Раздел Code (ExeFS): {(cCode ? "Присутствует" : "Отсутствует")}");
                                        sbDetails.AppendLine($"    * Раздел Data (RomFS): {(cData ? "Присутствует" : "Отсутствует")}");

                                        string rightsIdHex = nca.Header.HasRightsId ? BitConverter.ToString(nca.Header.RightsId.ToArray()).Replace("-", "").ToUpperInvariant() : "";
                                        bool rightsIdIsUpdate = rightsIdHex.Length >= 16 && rightsIdHex.Substring(0, 16).EndsWith("800");

                                        bool hasBktr = false;
                                        for (int sIdx = 0; sIdx < 4; sIdx++)
                                        {
                                            if (nca.Header.IsSectionEnabled(sIdx) && nca.Header.GetFsHeader(sIdx).EncryptionType == NcaEncryptionType.AesCtrEx)
                                            {
                                                hasBktr = true;
                                                break;
                                            }
                                        }

                                        if (!ncaTid.EndsWith("000") || rightsIdIsUpdate || hasBktr || (!containerHasBaseCnmt && containerHasUpdateCnmt))
                                        {
                                            hasUpdateProgram = true;
                                        }
                                        else
                                        {
                                            hasBaseProgram = true;
                                        }
                                    }
                                    else if (nca.Header.ContentType == NcaContentType.Control)
                                    {
                                        hasControl = true;
                                        
                                        try 
                                        {
                                            var controlFs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);
                                            using var controlFile = new UniqueRef<IFile>();
                                            using var controlPath = new LibHac.Fs.Path();
                                            controlPath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes("/control.nacp"))).ThrowIfFailure();
                                            if (controlFs.OpenFile(ref controlFile.Ref, in controlPath, OpenMode.Read).IsSuccess())
                                            {
                                                byte[] nacpBytes = new byte[0x4000];
                                                controlFile.Release().AsStorage().Read(0, nacpBytes).ThrowIfFailure();
                                                string verStr = System.Text.Encoding.UTF8.GetString(nacpBytes, 0x3060, 16).Trim('\0', ' ', '\r', '\n');
                                                if (!string.IsNullOrEmpty(verStr)) version = verStr;
                                            }
                                        }
                                        catch { }
                                    }
                                    else if (nca.Header.ContentType == NcaContentType.PublicData || nca.Header.ContentType == NcaContentType.Data)
                                    {
                                        if (!ncaTid.EndsWith("000") && !ncaTid.EndsWith("800"))
                                        {
                                            hasDlc = true;
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    hasReadErrors = true;
                                    sbDetails.AppendLine($"  - [ОШИБКА ЧТЕНИЯ NCA] Файл: {entryName}. Не удалось расшифровать: {ex.Message}");
                                }

                                processedSize += entry.Size;
                            }
                            else
                            {
                                sbDetails.AppendLine($"  - Метаданные: {entryName} ({ProcessingTask.FormatSize(entry.Size)})");
                                processedSize += entry.Size;
                            }
                        }

                        if (hasBaseProgram)
                        {
                            if (hasUpdateProgram) verifyType = "Мульти-контент";
                            else verifyType = "Базовая игра";
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
                        sbDetails.AppendLine("=== ИТОГОВОЕ ЗАКЛЮЧЕНИЕ ИНСПЕКТОРА ===");
                        sbDetails.AppendLine($"Тип контента: {verifyType}");
                        sbDetails.AppendLine($"Версия релиза: {version}");
                        sbDetails.AppendLine($"Исполняемый код ExeFS: {(hasCode ? "Присутствует (ОК)" : "Отсутствует")}");
                        sbDetails.AppendLine($"Игровые ресурсы RomFS: {(hasData ? "Присутствует (ОК)" : "Отсутствует")}");
                        sbDetails.AppendLine($"Паспорт игры Control: {(hasControl ? "Присутствует (ОК)" : "Отсутствует")}");

                        if (hasReadErrors)
                        {
                            structureStatus = "ОШИБКА ЧТЕНИЯ (Поврежден)";
                            mergedStatus = "Ошибка расшифровки NCA";
                            sbDetails.AppendLine();
                            sbDetails.AppendLine("❌ КРИТИЧЕСКАЯ ОШИБКА: Один или несколько NCA архивов повреждены или требуют более новых ключей prod.keys.");
                        }
                        else if (verifyType == "Обновление")
                        {
                            structureStatus = "Целостен (Патч обновления)";
                            mergedStatus = "Требуется базовая игра";
                            sbDetails.AppendLine();
                            sbDetails.AppendLine("ℹ️ ИНФОРМАЦИЯ: Этот файл содержит только патч обновления (Update) и требует наличия базовой игры (Base Game).");
                            sbDetails.AppendLine("💡 Для создания единого автономного файла объедините базовую игру и этот патч в режиме «Мульти-контент».");
                        }
                        else if (verifyType == "Базовая игра" || verifyType == "Мульти-контент")
                        {
                            if (hasCode && hasData && hasControl)
                            {
                                structureStatus = "Целостен";
                                mergedStatus = verifyType == "Мульти-контент" ? "Объединен (Монолит)" : "Оригинал";
                                sbDetails.AppendLine();
                                sbDetails.AppendLine("✅ ВЕРДИКТ: Контейнер полностью исправен и готов к запуску на эмуляторах и консоли! Все секции NCA читаются без ошибок.");
                            }
                            else
                            {
                                structureStatus = "Неполный (Ошибка структуры)";
                                mergedStatus = "Поврежден (Нет ExeFS/RomFS)";
                                sbDetails.AppendLine();
                                sbDetails.AppendLine("❌ ВЕРДИКТ: Нарушена структура игры! Отсутствует исполняемый код (ExeFS), паспорт (Control) или RomFS.");
                            }
                        }
                        else if (verifyType == "DLC")
                        {
                            structureStatus = "Целостен";
                            mergedStatus = "Дополнение (DLC)";
                            sbDetails.AppendLine();
                            sbDetails.AppendLine("✅ ВЕРДИКТ: Это пакет дополнительного контента (DLC). Архив полностью исправен.");
                        }
                        else
                        {
                            structureStatus = "ОШИБКА СТРУКТУРЫ";
                            mergedStatus = "Неизвестный формат";
                            sbDetails.AppendLine();
                            sbDetails.AppendLine("⚠️ ВЕРДИКТ: Внутри архива не обнаружено валидных разделов Nintendo Switch NCA.");
                        }
                    }
                }, cancellationToken);

                App.RunOnUI(() =>
                {
                    task.VerifyType = verifyType;
                    task.VerifyStructure = structureStatus;
                    task.VerifyTitleId = titleId;
                    task.VerifyVersion = version;
                    task.VerifyMergedStatus = mergedStatus;
                    bool isBad = structureStatus.Contains("ОШИБКА") || structureStatus.Contains("Неполный") || structureStatus.Contains("Поврежден");
                    task.Status = isBad ? "Поврежден" : "Целостен";
                    task.Progress = 100;
                    task.LogDetails = sbDetails.ToString();
                });
            }
            catch (Exception ex)
            {
                App.RunOnUI(() =>
                {
                    task.Status = "Ошибка";
                    task.IsRunning = false;
                    task.LogDetails += $"\n[СБОЙ] Ошибка проверки: {ex.Message}";
                });
                throw;
            }
        }

        private static IFile OpenFileSafe(IFileSystem fsToOpen, string pth)
        {
            using var fRef = new UniqueRef<IFile>();
            using var path = new LibHac.Fs.Path();
            path.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(pth))).ThrowIfFailure();
            fsToOpen.OpenFile(ref fRef.Ref, in path, OpenMode.Read).ThrowIfFailure();
            return fRef.Release();
        }
    }

    /// <summary>
    /// Обертка над IStorage для обхода бага в LibHac (Aes128CtrStorage) 
    /// с 16-байтным выравниванием при чтении внутри RomFS. 
    /// Выравнивает запросы до кратности 16, используя буфер из пула.
    /// </summary>
    public class UnalignedStorageWrapper : IStorage
    {
        private readonly IStorage _baseStorage;
        
        public UnalignedStorageWrapper(IStorage baseStorage)
        {
            _baseStorage = baseStorage;
        }

        public override Result Read(long offset, Span<byte> destination)
        {
            long alignedOffset = offset & ~0xFL;
            long offsetDiff = offset - alignedOffset;
            int alignedSize = (int)((destination.Length + offsetDiff + 15) & ~0xFL);

            if (alignedOffset == offset && alignedSize == destination.Length)
            {
                return _baseStorage.Read(offset, destination);
            }

            byte[] tempBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(alignedSize);
            try
            {
                Result res = _baseStorage.Read(alignedOffset, tempBuffer.AsSpan(0, alignedSize));
                if (res.IsFailure()) return res;

                tempBuffer.AsSpan((int)offsetDiff, destination.Length).CopyTo(destination);
                return Result.Success;
            }
            finally
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(tempBuffer);
            }
        }

        public override Result Write(long offset, ReadOnlySpan<byte> source)
        {
            return _baseStorage.Write(offset, source);
        }

        public override Result Flush()
        {
            return _baseStorage.Flush();
        }

        public override Result SetSize(long size)
        {
            return _baseStorage.SetSize(size);
        }

        public override Result GetSize(out long size)
        {
            return _baseStorage.GetSize(out size);
        }

        public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
        {
            return _baseStorage.OperateRange(outBuffer, operationId, offset, size, inBuffer);
        }
    }
}
