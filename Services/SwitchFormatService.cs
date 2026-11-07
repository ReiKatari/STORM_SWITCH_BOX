using Path = System.IO.Path;
using StormNczStorage = StormSwitchBox.Core.NSZ.StormNczStorage;
using ContentMetaType = LibHac.Ncm.ContentMetaType;
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

        public PartitionFileSystem OpenSwitchContainerFileSystem(IStorage rootStorage, string filePath)
        {
            bool isXci = filePath.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) ||
                         filePath.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase);

            if (isXci)
            {
                try
                {
                    rootStorage.GetSize(out long storageSize).ThrowIfFailure();
                    if (storageSize > 0x10000)
                    {
                        var rootStorageSub = new SubStorage(rootStorage, 0x10000, storageSize - 0x10000);
                        var rootPfs = new PartitionFileSystem(rootStorageSub);
                        using var secureFile = new UniqueRef<IFile>();
                        using var securePath = new LibHac.Fs.Path();
                        securePath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes("/secure"))).ThrowIfFailure();
                        if (rootPfs.OpenFile(ref secureFile.Ref, in securePath, OpenMode.Read).IsSuccess())
                        {
                            return new PartitionFileSystem(secureFile.Release().AsStorage());
                        }
                    }
                }
                catch { }
            }

            return new PartitionFileSystem(rootStorage);
        }

        public Models.SwitchFormatInfo ParseNsp(string filePath)
        {
            var info = new Models.SwitchFormatInfo { SizeBytes = new FileInfo(filePath).Length };

            if (filePath.EndsWith(".nup", StringComparison.OrdinalIgnoreCase) || NupContainerService.IsNupFile(filePath))
            {
                var nupMeta = NupContainerService.InspectNupFile(filePath);
                return new Models.SwitchFormatInfo
                {
                    SizeBytes = new FileInfo(filePath).Length,
                    TitleId = nupMeta.TitleId,
                    ContentType = nupMeta.ContentType,
                    Version = nupMeta.Version,
                    GameName = nupMeta.GameName,
                    Publisher = nupMeta.Publisher,
                    IconBytes = nupMeta.IconBytes
                };
            }

            _keysService.EnsureLoadedWithAutoDiscovery(filePath);
            if (!_keysService.IsLoaded) return info;

            try
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                IStorage storage = fileStream.AsStorage();
                IFileSystem fileSystem = OpenSwitchContainerFileSystem(storage, filePath);

                SmartShrinkService.HarvestTicketsFromFileSystem(fileSystem, _keysService.CurrentKeyset);

                foreach (var tikEntry in fileSystem.EnumerateEntries().Where(e => e.Name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        using var tikFileRef = new UniqueRef<IFile>();
                        using var tikPath = new LibHac.Fs.Path();
                        tikPath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(tikEntry.FullPath))).ThrowIfFailure();
                        if (fileSystem.OpenFile(ref tikFileRef.Ref, in tikPath, OpenMode.Read).IsSuccess())
                        {
                            using var tikFile = tikFileRef.Release();
                            using var ms = new MemoryStream();
                            tikFile.AsStream().CopyTo(ms);
                            ms.Position = 0;
                            var ticket = new LibHac.Tools.Es.Ticket(ms);
                            byte[] tKey = ticket.GetTitleKey(_keysService.CurrentKeyset);
                            var rId = new LibHac.Fs.RightsId(ticket.RightsId);
                            if (!_keysService.CurrentKeyset.ExternalKeySet.Contains(rId))
                            {
                                _keysService.CurrentKeyset.ExternalKeySet.Add(rId, new LibHac.Spl.AccessKey(tKey));
                            }
                            string rightsIdStr = BitConverter.ToString(ticket.RightsId).Replace("-", "").ToLowerInvariant();
                            lock (Core.NSZ.StormNczCompressor.TitleKeysCache)
                            {
                                Core.NSZ.StormNczCompressor.TitleKeysCache[rightsIdStr] = tKey;
                            }
                        }
                    }
                    catch { }
                }

                var baseCnmts = new List<(string Tid, uint Ver)>();
                var patchCnmts = new List<(string Tid, uint Ver)>();
                var dlcCnmts = new List<(string Tid, uint Ver)>();
                var programNcas = new List<(string NcaName, ulong Tid)>();
                var allContainedTids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var targetEntries = fileSystem.EnumerateEntries()
                    .Where(e => e.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) ||
                                e.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var entry in targetEntries)
                {
                    using var ncaFileRef = new UniqueRef<IFile>();
                    using var ncaPath = new LibHac.Fs.Path();
                    ncaPath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(entry.FullPath))).ThrowIfFailure();
                    if (fileSystem.OpenFile(ref ncaFileRef.Ref, in ncaPath, OpenMode.Read).IsFailure())
                        continue;

                    using var ncaFile = ncaFileRef.Release();
                    IStorage rawStorage = ncaFile.AsStorage();

                    IStorage ncaStorage;
                    IDisposable? nczDisposable = null;
                    bool isNcz = entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase);

                    if (isNcz)
                    {
                        if (entry.Size >= 64L * 1024 * 1024)
                        {
                            // Huge Program NCA in NCZ (e.g. 14 GB) -> read ONLY the uncompressed 0x4000 header in 0 ms!
                            ncaStorage = new SubStorage(rawStorage, 0, 0x4000);
                        }
                        else
                        {
                            var ncz = new StormNczStorage(rawStorage, null, null, _keysService.CurrentKeyset);
                            nczDisposable = ncz;
                            ncaStorage = ncz;
                        }
                    }
                    else
                    {
                        ncaStorage = rawStorage;
                    }

                    try
                    {
                        var nca = new Nca(_keysService.CurrentKeyset, ncaStorage);
                        KeysService.RegisterRightsIdFallback(_keysService.CurrentKeyset, nca);

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
                                    if (fs.OpenFile(ref cnmtFileRef.Ref, in cnmtPath, OpenMode.Read).IsSuccess())
                                    {
                                        using var cnmtFile = cnmtFileRef.Release();
                                        var cnmt = new LibHac.Tools.Ncm.Cnmt(cnmtFile.AsStream());
                                        allContainedTids.Add(cnmt.TitleId.ToString("X16"));

                                        if (cnmt.Type == ContentMetaType.Application)
                                            baseCnmts.Add((cnmt.TitleId.ToString("X16"), cnmt.TitleVersion.Version));
                                        else if (cnmt.Type == ContentMetaType.Patch)
                                            patchCnmts.Add((cnmt.TitleId.ToString("X16"), cnmt.TitleVersion.Version));
                                        else if (cnmt.Type == ContentMetaType.AddOnContent)
                                            dlcCnmts.Add((cnmt.TitleId.ToString("X16"), cnmt.TitleVersion.Version));
                                    }
                                }
                            }
                        }
                        else if (nca.Header.ContentType == NcaContentType.Program)
                        {
                            programNcas.Add((entry.Name, nca.Header.TitleId));
                            allContainedTids.Add(nca.Header.TitleId.ToString("X16"));
                        }
                        else if (nca.Header.ContentType == NcaContentType.Control && string.IsNullOrEmpty(info.GameName))
                        {
                            allContainedTids.Add(nca.Header.TitleId.ToString("X16"));
                            IFileSystem? fs = null;
                            try { fs = nca.OpenFileSystem(0, IntegrityCheckLevel.None); } catch { }
                            if (fs == null)
                            {
                                try { fs = nca.OpenFileSystem(1, IntegrityCheckLevel.None); } catch { }
                            }

                            if (fs != null)
                            {
                                var iconEntry = fs.EnumerateEntries("/", "icon_*.dat").FirstOrDefault();
                                if (iconEntry != null)
                                {
                                    using var iconFileRef = new UniqueRef<IFile>();
                                    using var iconPath = new LibHac.Fs.Path();
                                    iconPath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(iconEntry.FullPath))).ThrowIfFailure();
                                    if (fs.OpenFile(ref iconFileRef.Ref, in iconPath, OpenMode.Read).IsSuccess())
                                    {
                                        using var iconFile = iconFileRef.Release();
                                        using var iconStream = new MemoryStream();
                                        iconFile.AsStream().CopyTo(iconStream);
                                        info.IconBytes = iconStream.ToArray();
                                    }
                                }

                                var nacpEntry = fs.EnumerateEntries("/", "control.nacp").FirstOrDefault();
                                if (nacpEntry != null)
                                {
                                    try
                                    {
                                        using var nacpFileRef = new UniqueRef<IFile>();
                                        using var nacpPath = new LibHac.Fs.Path();
                                        nacpPath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(nacpEntry.FullPath))).ThrowIfFailure();
                                        if (fs.OpenFile(ref nacpFileRef.Ref, in nacpPath, OpenMode.Read).IsSuccess())
                                        {
                                            using var nacpFile = nacpFileRef.Release();
                                            using var nacpStream = new MemoryStream();
                                            nacpFile.AsStream().CopyTo(nacpStream);
                                            byte[] nacpData = nacpStream.ToArray();
                                            if (nacpData.Length >= 0x3080)
                                            {
                                                string ReadNacpString(int offset, int maxLen)
                                                {
                                                    if (offset >= nacpData.Length) return "";
                                                    int len = 0;
                                                    while (len < maxLen && offset + len < nacpData.Length && nacpData[offset + len] != 0) len++;
                                                    return len > 0 ? System.Text.Encoding.UTF8.GetString(nacpData, offset, len).Trim() : "";
                                                }

                                                string ruTitle = ReadNacpString(11 * 0x300, 0x200);
                                                string enTitle = ReadNacpString(0, 0x200);
                                                string title = !string.IsNullOrEmpty(ruTitle) ? ruTitle : (!string.IsNullOrEmpty(enTitle) ? enTitle : "");
                                                if (string.IsNullOrEmpty(title))
                                                {
                                                    for (int i = 0; i < 16; i++)
                                                    {
                                                        title = ReadNacpString(i * 0x300, 0x200);
                                                        if (!string.IsNullOrEmpty(title)) break;
                                                    }
                                                }
                                                if (!string.IsNullOrEmpty(title)) info.GameName = title;

                                                string ruPub = ReadNacpString(11 * 0x300 + 0x200, 0x100);
                                                string enPub = ReadNacpString(0x200, 0x100);
                                                string pub = !string.IsNullOrEmpty(ruPub) ? ruPub : (!string.IsNullOrEmpty(enPub) ? enPub : "");
                                                if (!string.IsNullOrEmpty(pub)) info.Publisher = pub;
                                            }
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        nczDisposable?.Dispose();
                    }
                }

                info.ContainedTitleIds = allContainedTids.ToList();
                info.HasBaseCnmt = baseCnmts.Count > 0;
                info.HasPatchCnmt = patchCnmts.Count > 0;
                info.HasProgramNca = programNcas.Count > 0;

                bool hasSeparatePatchProg = programNcas.Any(p => p.Tid.ToString("X16").EndsWith("800", StringComparison.OrdinalIgnoreCase)) ||
                                            (programNcas.Count >= 2 && patchCnmts.Count > 0);
                info.HasPatchProgramNca = hasSeparatePatchProg;

                if (baseCnmts.Count > 0 && patchCnmts.Count > 0)
                {
                    var b = baseCnmts[0];
                    var p = patchCnmts.OrderByDescending(x => x.Ver).First();

                    info.TitleId = b.Tid.ToUpperInvariant();
                    info.ContentType = "Application";
                    info.EmbeddedPatchVersion = p.Ver;
                    info.Version = p.Ver > 0 ? p.Ver.ToString() : b.Ver.ToString();
                    info.IsMonolithicHardPatch = !hasSeparatePatchProg;
                    info.BaseTitleId = b.Tid.ToUpperInvariant();
                    info.PatchTitleId = p.Tid.ToUpperInvariant();
                    App.Logger.Log($"[LibHac] Контейнер распознан: {info.TitleId} | Монолит: {info.IsMonolithicHardPatch} | Base v{b.Ver} | Embedded Patch v{info.EmbeddedPatchVersion}", Models.LogLevel.Info);
                }
                else if (baseCnmts.Count > 0)
                {
                    var b = baseCnmts[0];
                    info.TitleId = b.Tid.ToUpperInvariant();
                    info.ContentType = "Application";
                    info.Version = b.Ver.ToString();
                    info.EmbeddedPatchVersion = b.Ver > 0 ? b.Ver : 0;
                    info.IsMonolithicHardPatch = b.Ver > 0 && !hasSeparatePatchProg;
                    info.BaseTitleId = b.Tid.ToUpperInvariant();
                    App.Logger.Log($"[LibHac] Распознан Base контент: {info.TitleId} | v{info.Version} (Монолит: {info.IsMonolithicHardPatch})", Models.LogLevel.Info);
                }
                else if (patchCnmts.Count > 0)
                {
                    var p = patchCnmts.OrderByDescending(x => x.Ver).First();
                    info.TitleId = p.Tid.ToUpperInvariant();
                    info.ContentType = "Patch";
                    info.Version = p.Ver.ToString();
                    info.EmbeddedPatchVersion = p.Ver;
                    info.IsMonolithicHardPatch = false;
                    info.PatchTitleId = p.Tid.ToUpperInvariant();
                    App.Logger.Log($"[LibHac] Распознано обновление: {info.TitleId} | v{info.Version}", Models.LogLevel.Info);
                }
                else if (dlcCnmts.Count > 0)
                {
                    var d = dlcCnmts[0];
                    info.TitleId = d.Tid.ToUpperInvariant();
                    info.ContentType = "AddOnContent";
                    info.Version = d.Ver.ToString();
                    info.IsMonolithicHardPatch = false;
                    App.Logger.Log($"[LibHac] Распознано дополнение: {info.TitleId} | v{info.Version}", Models.LogLevel.Info);
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
            _keysService.EnsureLoadedWithAutoDiscovery(filePath);
            if (!_keysService.IsLoaded) throw new Exception("Криптографические ключи prod.keys не загружены. Укажите файл prod.keys в Параметрах.");

            string? outRoot = Path.GetPathRoot(Path.GetFullPath(baseOutFolder));
            if (!string.IsNullOrEmpty(outRoot))
            {
                try
                {
                    var dInfo = new DriveInfo(outRoot);
                    if (dInfo.IsReady && dInfo.AvailableFreeSpace < 512L * 1024 * 1024)
                    {
                        throw new IOException($"Недостаточно свободного места на диске {outRoot} для распаковки: доступно менее 512 МБ.");
                    }
                }
                catch (IOException) { throw; }
                catch { }
            }

            App.RunOnUI(() => { task.Status = "Распаковка..."; });
            App.RunOnUI(() => { 
                task.LogDetails += $"\nРаспаковка контейнера с помощью LibHac (Zero-Disk-IO)..."; 
            });

            string baseDataDir = System.IO.Path.Combine(baseOutFolder, "basedata");
            string dlcDataDir = System.IO.Path.Combine(baseOutFolder, "dlcdata");
            System.IO.Directory.CreateDirectory(baseDataDir);

            string preferredTemp = !string.IsNullOrEmpty(outRoot) ? Path.Combine(outRoot, "STORM_TMP") : Path.GetTempPath();

            await System.Threading.Tasks.Task.Run(() =>
            {
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                IStorage storage = fileStream.AsStorage();
                IFileSystem fileSystem = OpenSwitchContainerFileSystem(storage, filePath);

                SmartShrinkService.HarvestTicketsFromFileSystem(fileSystem, _keysService.CurrentKeyset);

                var titleKeyMap = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                string ticketsDir = System.IO.Path.Combine(baseDataDir, "tickets");

                foreach (var entry in fileSystem.EnumerateEntries().Where(e => e.Name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase) || e.Name.EndsWith(".cert", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        using var fRef = new UniqueRef<IFile>();
                        using var pRef = new LibHac.Fs.Path();
                        pRef.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(entry.FullPath))).ThrowIfFailure();
                        if (fileSystem.OpenFile(ref fRef.Ref, in pRef, OpenMode.Read).IsSuccess())
                        {
                            using var iFile = fRef.Release();
                            using var ms = new MemoryStream();
                            iFile.AsStream().CopyTo(ms);
                            byte[] rawBytes = ms.ToArray();

                            if (entry.Name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase))
                            {
                                ms.Position = 0;
                                var ticket = new LibHac.Tools.Es.Ticket(ms);
                                byte[] tKey = ticket.GetTitleKey(_keysService.CurrentKeyset);
                                string rightsIdStr = BitConverter.ToString(ticket.RightsId).Replace("-", "").ToLowerInvariant();
                                titleKeyMap[rightsIdStr] = tKey;
                                var rId = new LibHac.Fs.RightsId(ticket.RightsId);
                                if (!_keysService.CurrentKeyset.ExternalKeySet.Contains(rId))
                                {
                                    _keysService.CurrentKeyset.ExternalKeySet.Add(rId, new LibHac.Spl.AccessKey(tKey));
                                }
                                lock (Core.NSZ.StormNczCompressor.TitleKeysCache)
                                {
                                    Core.NSZ.StormNczCompressor.TitleKeysCache[rightsIdStr] = tKey;
                                }
                            }

                            System.IO.Directory.CreateDirectory(ticketsDir);
                            System.IO.File.WriteAllBytes(System.IO.Path.Combine(ticketsDir, entry.Name), rawBytes);
                        }
                    }
                    catch { }
                }

                var openedDisposables = new List<IDisposable>();
                try
                {
                    var baseProgList = new List<(Nca Nca, IStorage Storage, string Name)>();
                    var patchProgList = new List<(Nca Nca, IStorage Storage, string Name)>();
                    var controlList = new List<(Nca Nca, IStorage Storage, string Name)>();
                    var dlcList = new List<(Nca Nca, IStorage Storage, string Name)>();

                    var allEntries = fileSystem.EnumerateEntries().ToList();
                    foreach (var entry in allEntries)
                    {
                        token.ThrowIfCancellationRequested();
                        if (entry.Type == DirectoryEntryType.Directory) continue;
                        if (entry.Name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase) || entry.Name.EndsWith(".cert", StringComparison.OrdinalIgnoreCase))
                            continue;

                        bool isNca = entry.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase);
                        bool isNcz = entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase);

                        if (!isNca && !isNcz)
                        {
                            using var rawFileRef = new UniqueRef<IFile>();
                            using var rawPath = new LibHac.Fs.Path();
                            rawPath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(entry.FullPath))).ThrowIfFailure();
                            if (fileSystem.OpenFile(ref rawFileRef.Ref, in rawPath, OpenMode.Read).IsSuccess())
                            {
                                SaveStorageToFile(rawFileRef.Release().AsStorage(), System.IO.Path.Combine(baseDataDir, entry.Name));
                            }
                            continue;
                        }

                        using var ncaFileRef = new UniqueRef<IFile>();
                        using var ncaPath = new LibHac.Fs.Path();
                        ncaPath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(entry.FullPath))).ThrowIfFailure();
                        if (fileSystem.OpenFile(ref ncaFileRef.Ref, in ncaPath, OpenMode.Read).IsFailure())
                            continue;

                        IFile ncaFile = ncaFileRef.Release();
                        openedDisposables.Add(ncaFile);
                        IStorage rawStorage = ncaFile.AsStorage();

                        byte[] hdrBytes = new byte[0xC00];
                        if (rawStorage.Read(0, hdrBytes).IsSuccess())
                        {
                            if (hdrBytes.Length >= 0x240)
                            {
                                ReadOnlySpan<byte> rIdSpan = new ReadOnlySpan<byte>(hdrBytes, 0x230, 16);
                                KeysService.RegisterRightsIdFallback(_keysService.CurrentKeyset, rIdSpan);
                            }
                        }

                        IStorage effectiveStorage;
                        if (isNcz)
                        {
                            var ncz = new StormNczStorage(rawStorage, titleKeyMap, null, _keysService.CurrentKeyset, preferredTemp);
                            openedDisposables.Add(ncz);
                            effectiveStorage = ncz;
                        }
                        else
                        {
                            effectiveStorage = rawStorage;
                        }

                        Nca nca;
                        try
                        {
                            nca = new Nca(_keysService.CurrentKeyset, effectiveStorage);
                            KeysService.RegisterRightsIdFallback(_keysService.CurrentKeyset, nca);
                        }
                        catch (Exception ex)
                        {
                            App.Logger.Log($"[Unpack] Не удалось открыть NCA {entry.Name}: {ex.Message}", LogLevel.Warning);
                            SaveStorageToFile(effectiveStorage, System.IO.Path.Combine(baseDataDir, entry.Name));
                            continue;
                        }

                        if (nca.Header.ContentType == NcaContentType.Program)
                        {
                            bool hasData = nca.SectionExists(NcaSectionType.Data);
                            bool canOpenData = nca.CanOpenSection(NcaSectionType.Data);
                            bool isPatchProg = !canOpenData || (nca.Header.TitleId & 0xFFFUL) == 0x800;

                            if (hasData && !isPatchProg)
                            {
                                baseProgList.Add((nca, effectiveStorage, entry.Name));
                            }
                            else
                            {
                                patchProgList.Add((nca, effectiveStorage, entry.Name));
                            }
                        }
                        else if (nca.Header.ContentType == NcaContentType.Control)
                        {
                            controlList.Add((nca, effectiveStorage, entry.Name));
                        }
                        else if (nca.Header.ContentType == NcaContentType.PublicData ||
                                 ((nca.Header.TitleId & 0xFFFUL) > 0 && nca.Header.ContentType != NcaContentType.Meta))
                        {
                            dlcList.Add((nca, effectiveStorage, entry.Name));
                        }
                        else
                        {
                            SaveStorageToFile(effectiveStorage, System.IO.Path.Combine(baseDataDir, entry.Name));
                        }
                    }

                    // Process Program NCAs
                    if (baseProgList.Count > 0 && patchProgList.Count > 0)
                    {
                        var baseP = baseProgList[0];
                        var patchP = patchProgList[0];
                        App.RunOnUI(() => task.LogDetails += "\n📦 Обнаружен объединённый контейнер 1G+1U: распаковка обновлённого RomFS (BkTR) и ExeFS...");
                        
                        var codeNca = patchP.Nca.CanOpenSection(NcaSectionType.Code) ? patchP.Nca : baseP.Nca;
                        var codeStorage = patchP.Nca.CanOpenSection(NcaSectionType.Code) ? patchP.Storage : baseP.Storage;
                        ExtractNcaSection(codeNca, NcaSectionType.Code, System.IO.Path.Combine(baseDataDir, "exefs"), token, ncaStorage: codeStorage);
                        ExtractNcaSection(baseP.Nca, patchP.Nca, NcaSectionType.Data, System.IO.Path.Combine(baseDataDir, "romfs"), token);
                    }
                    else if (baseProgList.Count > 0)
                    {
                        var baseP = baseProgList[0];
                        App.RunOnUI(() => task.LogDetails += "\n📦 Распаковка базовой игры 1G (RomFS и ExeFS)...");
                        if (baseP.Nca.CanOpenSection(NcaSectionType.Code))
                        {
                            ExtractNcaSection(baseP.Nca, NcaSectionType.Code, System.IO.Path.Combine(baseDataDir, "exefs"), token, ncaStorage: baseP.Storage);
                        }
                        if (baseP.Nca.CanOpenSection(NcaSectionType.Data))
                        {
                            ExtractNcaSection(baseP.Nca, NcaSectionType.Data, System.IO.Path.Combine(baseDataDir, "romfs"), token, ncaStorage: baseP.Storage);
                        }
                    }
                    else if (patchProgList.Count > 0)
                    {
                        var patchP = patchProgList[0];
                        ulong baseTid = patchP.Nca.Header.TitleId & ~0xFFFUL;

                        string? inputDir = Path.GetDirectoryName(filePath);
                        string? siblingBasePath = null;
                        Nca? siblingBaseProgNca = null;
                        IFile? siblingFileRef = null;
                        IDisposable? siblingNcz = null;

                        if (!string.IsNullOrEmpty(inputDir) && Directory.Exists(inputDir))
                        {
                            var siblings = Directory.EnumerateFiles(inputDir, "*.*")
                                .Where(f => !f.Equals(filePath, StringComparison.OrdinalIgnoreCase) &&
                                            (f.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) ||
                                             f.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) ||
                                             f.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) ||
                                             f.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase)))
                                .ToList();

                            foreach (var sib in siblings)
                            {
                                try
                                {
                                    var sibInfo = ParseNsp(sib);
                                    if (!string.IsNullOrEmpty(sibInfo.TitleId) &&
                                        ulong.TryParse(sibInfo.TitleId, System.Globalization.NumberStyles.HexNumber, null, out ulong sTid) &&
                                        (sTid & ~0xFFFUL) == baseTid && sibInfo.HasProgramNca)
                                    {
                                        using var sibFs = new FileStream(sib, FileMode.Open, FileAccess.Read, FileShare.Read);
                                        var sibStorage = sibFs.AsStorage();
                                        var sibContainerFs = OpenSwitchContainerFileSystem(sibStorage, sib);
                                        foreach (var sEntry in sibContainerFs.EnumerateEntries().Where(e => e.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) || e.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase)))
                                        {
                                            using var sFileRef = new UniqueRef<IFile>();
                                            using var sPath = new LibHac.Fs.Path();
                                            sPath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(sEntry.FullPath))).ThrowIfFailure();
                                            if (sibContainerFs.OpenFile(ref sFileRef.Ref, in sPath, OpenMode.Read).IsSuccess())
                                            {
                                                var sFile = sFileRef.Release();
                                                IStorage sRaw = sFile.AsStorage();
                                                IStorage sEff = sRaw;
                                                if (sEntry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    var sNcz = new StormNczStorage(sRaw, null, null, _keysService.CurrentKeyset, preferredTemp);
                                                    siblingNcz = sNcz;
                                                    sEff = sNcz;
                                                }
                                                var sNca = new Nca(_keysService.CurrentKeyset, sEff);
                                                if (sNca.Header.ContentType == NcaContentType.Program && sNca.CanOpenSection(NcaSectionType.Data) && (sNca.Header.TitleId & 0xFFFUL) == 0)
                                                {
                                                    siblingBasePath = sib;
                                                    siblingBaseProgNca = sNca;
                                                    siblingFileRef = sFile;
                                                    break;
                                                }
                                                else
                                                {
                                                    sFile.Dispose();
                                                    if (siblingNcz is IDisposable sn) { sn.Dispose(); siblingNcz = null; }
                                                }
                                            }
                                        }
                                        if (siblingBaseProgNca != null) break;
                                    }
                                }
                                catch { }
                            }
                        }

                        if (siblingBaseProgNca != null)
                        {
                            try
                            {
                                App.RunOnUI(() => task.LogDetails += $"\n✅ Найдена базовая игра: {Path.GetFileName(siblingBasePath)}! RomFS успешно пропатчен и извлечён.");
                                if (patchP.Nca.CanOpenSection(NcaSectionType.Code))
                                {
                                    ExtractNcaSection(patchP.Nca, NcaSectionType.Code, System.IO.Path.Combine(baseDataDir, "exefs"), token, ncaStorage: patchP.Storage);
                                }
                                ExtractNcaSection(siblingBaseProgNca, patchP.Nca, NcaSectionType.Data, System.IO.Path.Combine(baseDataDir, "romfs"), token);
                            }
                            finally
                            {
                                siblingFileRef?.Dispose();
                                if (siblingNcz is IDisposable sn) sn.Dispose();
                            }
                        }
                        else
                        {
                            if (patchP.Nca.CanOpenSection(NcaSectionType.Code))
                            {
                                ExtractNcaSection(patchP.Nca, NcaSectionType.Code, System.IO.Path.Combine(baseDataDir, "exefs"), token, ncaStorage: patchP.Storage);
                            }
                            string warnMsg = "⚠️ Данный файл является патчем обновления (BkTR) без базовой игры: извлечены ExeFS, Control и метаданные; для распаковки полного RomFS поместите базовую игру [v0] рядом или распакуйте объединённый 1G+1U контейнер.";
                            App.Logger.Log(warnMsg, LogLevel.Warning);
                            App.RunOnUI(() => task.LogDetails += $"\n{warnMsg}");
                        }
                    }

                    // Process Control NCAs
                    if (controlList.Count > 0)
                    {
                        var ctrl = controlList.Last();
                        ExtractNcaSection(ctrl.Nca, NcaSectionType.Data, System.IO.Path.Combine(baseDataDir, "control"), token, ncaStorage: ctrl.Storage);
                    }

                    // Process DLC NCAs
                    if (dlcList.Count == 1)
                    {
                        var dlc = dlcList[0];
                        System.IO.Directory.CreateDirectory(dlcDataDir);
                        ExtractNcaSection(dlc.Nca, NcaSectionType.Data, System.IO.Path.Combine(dlcDataDir, "romfs"), token, ncaStorage: dlc.Storage);
                    }
                    else if (dlcList.Count > 1)
                    {
                        System.IO.Directory.CreateDirectory(dlcDataDir);
                        for (int i = 0; i < dlcList.Count; i++)
                        {
                            var dlc = dlcList[i];
                            string dlcSub = i == 0
                                ? System.IO.Path.Combine(dlcDataDir, "romfs")
                                : System.IO.Path.Combine(dlcDataDir, dlc.Nca.Header.TitleId.ToString("X16"), "romfs");
                            System.IO.Directory.CreateDirectory(dlcSub);
                            ExtractNcaSection(dlc.Nca, NcaSectionType.Data, dlcSub, token, ncaStorage: dlc.Storage);
                            App.RunOnUI(() => task.LogDetails += $"\nDLC [{dlc.Nca.Header.TitleId:X16}] распакован в: {dlcSub}");
                        }
                    }
                }
                finally
                {
                    foreach (var d in openedDisposables)
                    {
                        try { d.Dispose(); } catch { }
                    }
                }
            }, token);
        }

        public async Task ConvertContainerAsync(ProcessingTask task, string inputPath, string outDir, string targetFormat, CancellationToken cancellationToken)
        {
            if (targetFormat.Equals("NUP", StringComparison.OrdinalIgnoreCase))
            {
                await App.NupContainer.ConvertContainerToNupAsync(task, inputPath, outDir, cancellationToken);
                return;
            }

            if (inputPath.EndsWith(".nup", StringComparison.OrdinalIgnoreCase) || NupContainerService.IsNupFile(inputPath))
            {
                App.RunOnUI(() =>
                {
                    task.LogDetails += "\n[NUP] Распаковка контейнера NUP v1.0 перед конвертацией...";
                    task.Status = "Распаковка NUP...";
                });
                string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "StormNupExtract_" + Guid.NewGuid().ToString("N"));
                System.IO.Directory.CreateDirectory(tempDir);
                TempCleanupService.RegisterActiveTempDirectory(tempDir);
                try
                {
                    var prog = new Progress<(double pct, string status)>(p => App.RunOnUI(() =>
                    {
                        task.Progress = p.pct * 0.5;
                        task.Status = p.status;
                    }));
                    await NupContainerService.ExtractNupToDirectoryAsync(inputPath, tempDir, prog, cancellationToken);
                    var ncaFiles = System.IO.Directory.GetFiles(tempDir, "*.nc*", System.IO.SearchOption.AllDirectories);
                    if (ncaFiles.Length > 0)
                    {
                        string tempNsp = System.IO.Path.Combine(tempDir, System.IO.Path.GetFileNameWithoutExtension(inputPath) + ".nsp");
                        var pfsBuilder = new PartitionFileSystemBuilder();
                        foreach (var nca in ncaFiles)
                        {
                            pfsBuilder.AddFile(System.IO.Path.GetFileName(nca), new LocalFile(nca, OpenMode.Read));
                        }
                        using (var built = pfsBuilder.Build(PartitionFileSystemType.Standard))
                        using (var outStream = new FileStream(tempNsp, FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024 * 1024))
                        {
                            built.AsStream().CopyTo(outStream);
                        }
                        await ConvertContainerAsync(task, tempNsp, outDir, targetFormat, cancellationToken);
                        return;
                    }
                    else
                    {
                        App.RunOnUI(() =>
                        {
                            task.LogDetails += "\n[NUP] Контейнер распакован в " + tempDir;
                            task.Progress = 100;
                            task.Status = "Успешно";
                        });
                        return;
                    }
                }
                finally
                {
                    TempCleanupService.ForceDeleteDirectory(tempDir);
                }
            }

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

        public void ExtractNcaSection(Nca nca, NcaSectionType sectionType, string outDir, System.Threading.CancellationToken token, string? ncaFilePath = null, IStorage? ncaStorage = null)
        {
            System.IO.Directory.CreateDirectory(outDir);
            
            if (nca.CanOpenSection(sectionType))
            {
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
            }

            // Fallback: use hactoolnet.exe
            if (!string.IsNullOrEmpty(ncaFilePath) && System.IO.File.Exists(ncaFilePath))
            {
                ExtractNcaSectionViaHactool(ncaFilePath, null, sectionType, outDir, token);
            }
            else if (ncaStorage != null)
            {
                string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "StormNcaExtract_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                System.IO.Directory.CreateDirectory(tempDir);
                string tempNcaPath = System.IO.Path.Combine(tempDir, "temp.nca");
                try
                {
                    SaveStorageToFile(ncaStorage, tempNcaPath);
                    if (System.IO.File.Exists(tempNcaPath) && new FileInfo(tempNcaPath).Length > 0)
                    {
                        ExtractNcaSectionViaHactool(tempNcaPath, null, sectionType, outDir, token);
                    }
                    else
                    {
                        throw new Exception($"Не удалось извлечь секцию {sectionType} через LibHac API (дамп временного NCA пуст).");
                    }
                }
                finally
                {
                    try { System.IO.Directory.Delete(tempDir, true); } catch { }
                }
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
            if (!patchNca.SectionExists(sectionType))
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
            string userKeys = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "prod.keys");
            if (!File.Exists(userKeys) && !string.IsNullOrEmpty(_keysService.KeysFilePath) && File.Exists(_keysService.KeysFilePath))
            {
                try
                {
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(userKeys)!);
                    File.Copy(_keysService.KeysFilePath, userKeys, true);
                }
                catch { }
            }

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
                System.IO.Path.Combine(baseDir, "tools", "hactoolnet.exe"),
                System.IO.Path.Combine(baseDir, "tools", "com.github.nozwock.yanu", "hactoolnet.exe"),
                System.IO.Path.Combine(baseDir, "..", "tools", "hactoolnet.exe"),
                System.IO.Path.Combine(baseDir, "..", "tools", "com.github.nozwock.yanu", "hactoolnet.exe"),
                System.IO.Path.Combine(baseDir, "..", "..", "tools", "hactoolnet.exe"),
                System.IO.Path.Combine(baseDir, "..", "..", "tools", "com.github.nozwock.yanu", "hactoolnet.exe"),
                System.IO.Path.Combine(baseDir, "..", "..", "..", "..", "tools", "hactoolnet.exe"),
                System.IO.Path.Combine(baseDir, "..", "..", "..", "..", "tools", "com.github.nozwock.yanu", "hactoolnet.exe")
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

                string controlDir = System.IO.Path.Combine(inputFolder, "control");
                if (!System.IO.Directory.Exists(controlDir))
                {
                    var cDirs = System.IO.Directory.GetDirectories(inputFolder, "control", System.IO.SearchOption.AllDirectories);
                    if (cDirs.Length > 0) controlDir = cDirs[0];
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
                                KeysService.RegisterRightsIdFallback(_keysService.CurrentKeyset, nca);
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

                    string keysPath = _keysService.KeysFilePath ?? App.Settings.Current.KeysPath;
                    if (string.IsNullOrEmpty(keysPath) || !File.Exists(keysPath))
                    {
                        keysPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "keys.txt");
                    }
                    if (string.IsNullOrEmpty(keysPath) || !File.Exists(keysPath))
                    {
                        keysPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "prod.keys");
                    }
                    string keyfileFlag = (!string.IsNullOrEmpty(keysPath) && File.Exists(keysPath)) ? $"-k \"{keysPath}\" " : "";

                    string yanuCliPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "yanu-cli.exe");
                    if (!System.IO.File.Exists(yanuCliPath))
                    {
                        string fallback = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "tools", "yanu-cli.exe");
                        if (System.IO.File.Exists(fallback)) yanuCliPath = fallback;
                    }

                    string tempOut = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "StormPack_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                    System.IO.Directory.CreateDirectory(tempOut);

                    string localAppDataDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StormSwitchBox");
                    string isolatedUserProfile = System.IO.Path.Combine(localAppDataDir, "user_profile");
                    string isolatedSwitchDir = System.IO.Path.Combine(isolatedUserProfile, ".switch");
                    System.IO.Directory.CreateDirectory(isolatedSwitchDir);
                    if (!string.IsNullOrEmpty(keysPath) && File.Exists(keysPath))
                    {
                        try { File.Copy(keysPath, System.IO.Path.Combine(isolatedSwitchDir, "prod.keys"), true); } catch { }
                    }
                    string userTitleKeys = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "title.keys");
                    if (File.Exists(userTitleKeys))
                    {
                        try { File.Copy(userTitleKeys, System.IO.Path.Combine(isolatedSwitchDir, "title.keys"), true); } catch { }
                    }

                    try
                    {
                        string builtNspPath = "";
                        bool yanuSuccess = false;

                        // Сценарий 1: Если есть базовые control.nca и baseProgramNca, и доступен yanu-cli — пробуем yanu-cli pack (без дедлока пайпа)
                        if (!string.IsNullOrEmpty(controlNca) && !string.IsNullOrEmpty(baseProgramNca) && File.Exists(yanuCliPath))
                        {
                            if (string.IsNullOrEmpty(titleId)) titleId = "0100000000000000";
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
                            packPsi.EnvironmentVariables["USERPROFILE"] = isolatedUserProfile;
                            packPsi.EnvironmentVariables["LOCALAPPDATA"] = System.IO.Path.Combine(localAppDataDir, "cache");
                            packPsi.EnvironmentVariables["APPDATA"] = System.IO.Path.Combine(localAppDataDir, "cache");
                            packPsi.EnvironmentVariables["TEMP"] = tempOut;
                            packPsi.EnvironmentVariables["TMP"] = tempOut;
                            
                            using var packProcess = System.Diagnostics.Process.Start(packPsi);
                            if (packProcess != null)
                            {
                                var stdoutTask = packProcess.StandardOutput.ReadToEndAsync(cancellationToken);
                                var stderrTask = packProcess.StandardError.ReadToEndAsync(cancellationToken);
                                await Task.WhenAll(stdoutTask, stderrTask);
                                await packProcess.WaitForExitAsync(cancellationToken);

                                var genFiles = System.IO.Directory.GetFiles(tempOut, "*.nsp");
                                if (packProcess.ExitCode == 0 && genFiles.Length > 0)
                                {
                                    builtNspPath = genFiles[0];
                                    yanuSuccess = true;
                                }
                            }
                        }

                        // Сценарий 2: Если yanu-cli не сработал или нет controlNca/baseProgramNca — собираем через hacpack
                        if (!yanuSuccess)
                        {
                            string hacpackPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "hacpack.exe");
                            if (!File.Exists(hacpackPath))
                            {
                                throw new Exception("Для перепаковки распакованной игры (romfs/exefs) требуется hacpack.exe или базовый control.nca.");
                            }

                            if (string.IsNullOrEmpty(titleId)) titleId = "0100000000000000";

                            // Подготавливаем control: если нет папки control, создаем синтетический control.nacp
                            string effectiveControlDir = controlDir;
                            if (string.IsNullOrEmpty(effectiveControlDir) || !System.IO.Directory.Exists(effectiveControlDir))
                            {
                                effectiveControlDir = System.IO.Path.Combine(tempOut, "synthetic_control");
                                System.IO.Directory.CreateDirectory(effectiveControlDir);
                                byte[] nacpBytes = GenerateNacp("Storm Game", "Storm Team", "1.0.0", titleId);
                                File.WriteAllBytes(System.IO.Path.Combine(effectiveControlDir, "control.nacp"), nacpBytes);
                            }

                            // 1. Сборка Program NCA
                            string progOut = System.IO.Path.Combine(tempOut, "prog_out");
                            System.IO.Directory.CreateDirectory(progOut);
                            string progArgs = $"{keyfileFlag}--type nca --ncatype program --titleid {titleId} --exefsdir \"{exefsDir}\" --romfsdir \"{romfsDir}\" -o \"{progOut}\"";
                            await RunHacpackAsync(hacpackPath, progArgs, tempOut, cancellationToken, isolatedUserProfile);
                            var progNcas = System.IO.Directory.GetFiles(progOut, "*.nca");
                            if (progNcas.Length == 0) throw new Exception("hacpack не создал Program NCA при перепаковке.");
                            string genProgNca = progNcas[0];

                            // 2. Сборка Control NCA
                            string ctrlOut = System.IO.Path.Combine(tempOut, "ctrl_out");
                            System.IO.Directory.CreateDirectory(ctrlOut);
                            string ctrlArgs = $"{keyfileFlag}--type nca --ncatype control --titleid {titleId} --romfsdir \"{effectiveControlDir}\" -o \"{ctrlOut}\"";
                            await RunHacpackAsync(hacpackPath, ctrlArgs, tempOut, cancellationToken, isolatedUserProfile);
                            var ctrlNcas = System.IO.Directory.GetFiles(ctrlOut, "*.nca");
                            if (ctrlNcas.Length == 0) throw new Exception("hacpack не создал Control NCA при перепаковке.");
                            string genCtrlNca = ctrlNcas[0];

                            // 3. Сборка Meta NCA
                            string metaOut = System.IO.Path.Combine(tempOut, "meta_out");
                            System.IO.Directory.CreateDirectory(metaOut);
                            string metaArgs = $"{keyfileFlag}--type nca --ncatype meta --titletype application --titleid {titleId} --titleversion 0x0 --programnca \"{genProgNca}\" --controlnca \"{genCtrlNca}\" -o \"{metaOut}\"";
                            await RunHacpackAsync(hacpackPath, metaArgs, tempOut, cancellationToken, isolatedUserProfile);
                            var metaNcas = System.IO.Directory.GetFiles(metaOut, "*.nca");
                            if (metaNcas.Length == 0) throw new Exception("hacpack не создал Meta NCA при перепаковке.");

                            // 4. Сборка финального NSP
                            var allNcasDir = System.IO.Path.Combine(tempOut, "all_ncas");
                            System.IO.Directory.CreateDirectory(allNcasDir);
                            File.Copy(genProgNca, System.IO.Path.Combine(allNcasDir, System.IO.Path.GetFileName(genProgNca)), true);
                            File.Copy(genCtrlNca, System.IO.Path.Combine(allNcasDir, System.IO.Path.GetFileName(genCtrlNca)), true);
                            foreach (var m in metaNcas)
                            {
                                File.Copy(m, System.IO.Path.Combine(allNcasDir, System.IO.Path.GetFileName(m)), true);
                            }
                            foreach (var extra in System.IO.Directory.GetFiles(inputFolder, "*.*", System.IO.SearchOption.AllDirectories)
                                .Where(f => f.EndsWith(".tik", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".cert", StringComparison.OrdinalIgnoreCase)))
                            {
                                try { File.Copy(extra, System.IO.Path.Combine(allNcasDir, System.IO.Path.GetFileName(extra)), true); } catch { }
                            }

                            string packedNspPath = System.IO.Path.Combine(tempOut, "built.nsp");
                            var nspPfsBuilder = new PartitionFileSystemBuilder();
                            var openedPfsStreams = new List<FileStream>();
                            try
                            {
                                foreach (var ncaFile in System.IO.Directory.GetFiles(allNcasDir))
                                {
                                    var fs = new FileStream(ncaFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                                    openedPfsStreams.Add(fs);
                                    nspPfsBuilder.AddFile(System.IO.Path.GetFileName(ncaFile), new StorageFile(fs.AsStorage(), OpenMode.Read));
                                }
                                using var outFs = new FileStream(packedNspPath, FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024 * 1024);
                                using var builtPfs = nspPfsBuilder.Build(PartitionFileSystemType.Standard);
                                builtPfs.AsStream().CopyTo(outFs);
                            }
                            finally
                            {
                                foreach (var s in openedPfsStreams) s.Dispose();
                            }
                            builtNspPath = packedNspPath;
                        }

                        if (!string.IsNullOrEmpty(builtNspPath) && File.Exists(builtNspPath))
                        {
                            outPath = await SafeFileOperations.SafeMoveOrReplaceFileAsync(builtNspPath, outPath, task, cancellationToken);
                        }
                        else
                        {
                            throw new Exception("Сборка пакета не выдала готовый .nsp файл.");
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
                    KeysService.RegisterRightsIdFallback(_keysService.CurrentKeyset, nca);
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

        private static byte[] GenerateNacp(string title, string author, string version, string titleIdStr)
        {
            byte[] nacp = new byte[0x4000];
            byte[] titleBytes = System.Text.Encoding.UTF8.GetBytes(title);
            byte[] authorBytes = System.Text.Encoding.UTF8.GetBytes(author);

            for (int i = 0; i < 16; i++)
            {
                int offset = i * 0x300;
                Array.Copy(titleBytes, 0, nacp, offset, Math.Min(titleBytes.Length, 0x200));
                Array.Copy(authorBytes, 0, nacp, offset + 0x200, Math.Min(authorBytes.Length, 0x100));
            }

            byte[] verBytes = System.Text.Encoding.UTF8.GetBytes(version);
            Array.Copy(verBytes, 0, nacp, 0x3060, Math.Min(verBytes.Length, 0x10));

            if (ulong.TryParse(titleIdStr, System.Globalization.NumberStyles.HexNumber, null, out ulong tid))
            {
                BitConverter.GetBytes(tid).CopyTo(nacp, 0x3078);
                BitConverter.GetBytes(tid).CopyTo(nacp, 0x3038);
                BitConverter.GetBytes(tid).CopyTo(nacp, 0x3070);
            }

            return nacp;
        }

        private static async Task<int> RunHacpackAsync(string exe, string args, string workDir, CancellationToken ct, string? userProfile = null)
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                WorkingDirectory = workDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };
            if (!string.IsNullOrEmpty(userProfile))
            {
                psi.EnvironmentVariables["USERPROFILE"] = userProfile;
            }
            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) return -1;
            var outTask = proc.StandardOutput.ReadToEndAsync(ct);
            var errTask = proc.StandardError.ReadToEndAsync(ct);
            await Task.WhenAll(outTask, errTask);
            await proc.WaitForExitAsync(ct);
            return proc.ExitCode;
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
