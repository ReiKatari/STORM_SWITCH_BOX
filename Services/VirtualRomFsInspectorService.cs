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
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using LibHac.Tools.FsSystem.RomFs;
using StormSwitchBox.Models;
using Path = System.IO.Path;

namespace StormSwitchBox.Services
{
    public class VirtualArchiveEntry
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string Section { get; set; } = "RomFS";
        public long Size { get; set; }
        public string SizeFormatted => ProcessingTask.FormatSize(Size);
        public string SourceNcaName { get; set; } = string.Empty;
        public int NcaSectionIndex { get; set; } = -1;
        public string TitleId { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public bool Is3dsDirectSlice { get; set; }
        public long SliceOffset { get; set; }
        public long SliceLength { get; set; }
    }

    public static class VirtualRomFsInspectorService
    {
        public static async Task<List<VirtualArchiveEntry>> InspectContainerAsync(string containerPath, CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                var results = new List<VirtualArchiveEntry>();
                if (string.IsNullOrEmpty(containerPath) || !File.Exists(containerPath))
                    return results;

                string ext = Path.GetExtension(containerPath).ToLowerInvariant();
                if (Nintendo3dsService.Is3dsExtension(ext))
                    Inspect3dsContainer(containerPath, results, ct);
                else
                    InspectSwitchContainer(containerPath, results, ct);

                return results
                    .OrderBy(e => e.Section)
                    .ThenBy(e => e.FullPath, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }, ct);
        }

        private static void InspectSwitchContainer(string containerPath, List<VirtualArchiveEntry> results, CancellationToken ct)
        {
            string ext = Path.GetExtension(containerPath).ToLowerInvariant();
            if (ext == ".nca")
            {
                using var fs = new FileStream(containerPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                InspectSingleNca(fs.AsStorage(), Path.GetFileName(containerPath), results, ct);
                return;
            }

            using var container = SwitchContainerHandle.Open(containerPath);
            IFileSystem rootFs = container.FileSystem;

            SmartShrinkService.HarvestTicketsFromFileSystem(rootFs, App.Keys.CurrentKeyset);

            var titleKeyMap = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var tikEntry in rootFs.EnumerateEntries().Where(e => e.Name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    using var fRef = new UniqueRef<IFile>();
                    using var pth = new LibHac.Fs.Path();
                    pth.Initialize(new U8Span(Encoding.UTF8.GetBytes(tikEntry.FullPath))).ThrowIfFailure();
                    rootFs.OpenFile(ref fRef.Ref, in pth, OpenMode.Read).ThrowIfFailure();
                    using IFile tikFile = fRef.Release();
                    using var ms = new MemoryStream();
                    tikFile.AsStream().CopyTo(ms);
                    byte[] tikBytes = ms.ToArray();
                    var decInfo = TicketHarvesterService.ExtractDecryptedTicket(tikBytes, tikBytes.Length, App.Keys.CurrentKeyset);
                    if (decInfo.HasValue)
                    {
                        titleKeyMap[decInfo.Value.RightsId] = decInfo.Value.TitleKey;
                    }
                }
                catch { }
            }

            foreach (var entry in rootFs.EnumerateEntries("/", "*"))
            {
                ct.ThrowIfCancellationRequested();
                if (entry.Type == DirectoryEntryType.Directory) continue;

                results.Add(new VirtualArchiveEntry
                {
                    Name = entry.Name,
                    FullPath = "/" + entry.Name,
                    Section = "Контейнер",
                    Size = entry.Size,
                    SourceNcaName = entry.Name,
                    NcaSectionIndex = -1
                });

                if (entry.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) ||
                    entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using var fRef = new UniqueRef<IFile>();
                        using var pth = new LibHac.Fs.Path();
                        pth.Initialize(new U8Span(Encoding.UTF8.GetBytes(entry.FullPath))).ThrowIfFailure();
                        rootFs.OpenFile(ref fRef.Ref, in pth, OpenMode.Read).ThrowIfFailure();
                        using IFile ncaFile = fRef.Release();
                        IStorage rawStorage = ncaFile.AsStorage();

                        using var nczStorage = entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase)
                            ? new Core.NSZ.StormNczStorage(rawStorage, titleKeyMap, null, App.Keys.CurrentKeyset)
                            : null;

                        InspectSingleNca(nczStorage ?? rawStorage, entry.Name, results, ct);
                    }
                    catch { }
                }
            }
        }

        private static void InspectSingleNca(IStorage ncaStorage, string ncaName, List<VirtualArchiveEntry> results, CancellationToken ct)
        {
            var nca = new Nca(App.Keys.CurrentKeyset, ncaStorage);
            string tid = nca.Header.TitleId.ToString("X16");
            string cType = nca.Header.ContentType.ToString();

            for (int secIdx = 0; secIdx < 4; secIdx++)
            {
                if (!nca.SectionExists(secIdx) || !nca.CanOpenSection(secIdx)) continue;

                string secLabel = cType == "Control" ? "Control" : (secIdx == 0 && cType == "Program" ? "ExeFS" : "RomFS");
                try
                {
                    IFileSystem? secFs = OpenNcaSectionFileSystem(nca, secIdx);
                    if (secFs == null) continue;
                    using var secFsDisp = secFs as IDisposable;

                    foreach (var subEntry in secFs.EnumerateEntries("/", "*", SearchOptions.RecurseSubdirectories))
                    {
                        ct.ThrowIfCancellationRequested();
                        if (subEntry.Type == DirectoryEntryType.Directory) continue;

                        results.Add(new VirtualArchiveEntry
                        {
                            Name = subEntry.Name,
                            FullPath = subEntry.FullPath,
                            Section = secLabel,
                            Size = subEntry.Size,
                            SourceNcaName = ncaName,
                            NcaSectionIndex = secIdx,
                            TitleId = tid,
                            ContentType = cType
                        });
                    }
                }
                catch { }
            }
        }

        private static IFileSystem? OpenNcaSectionFileSystem(Nca nca, int secIdx)
        {
            try
            {
                var fmt = nca.Header.GetFsHeader(secIdx).FormatType;
                var st = nca.OpenStorage(secIdx, IntegrityCheckLevel.None);
                if (fmt == NcaFormatType.Romfs)
                    return new RomFsFileSystem(new UnalignedStorageWrapper(st));
                return new PartitionFileSystem(new UnalignedStorageWrapper(st));
            }
            catch
            {
                try { return nca.OpenFileSystem(secIdx, IntegrityCheckLevel.None); }
                catch { return null; }
            }
        }

        private static void Inspect3dsContainer(string containerPath, List<VirtualArchiveEntry> results, CancellationToken ct)
        {
            try
            {
                using var fs = new FileStream(containerPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var br = new BinaryReader(fs);

                long ncchOffset = FindFirstNcchOffset(fs, br);
                if (ncchOffset >= 0 && ncchOffset + 0x200 <= fs.Length)
                {
                    fs.Position = ncchOffset;
                    byte[] ncch = br.ReadBytes(0x200);
                    ulong programId = BitConverter.ToUInt64(ncch, 0x118);
                    string tid = programId.ToString("X16");
                    bool noCrypto = (ncch[0x18F] & 0x04) != 0;

                    uint exHeaderSize = BitConverter.ToUInt32(ncch, 0x180);
                    if (exHeaderSize > 0)
                    {
                        results.Add(new VirtualArchiveEntry
                        {
                            Name = "exheader.bin",
                            FullPath = "/exheader.bin",
                            Section = "NCCH",
                            Size = 0x800,
                            TitleId = tid,
                            Is3dsDirectSlice = noCrypto,
                            SliceOffset = ncchOffset + 0x200,
                            SliceLength = 0x800
                        });
                    }

                    uint exefsOffsetUnits = BitConverter.ToUInt32(ncch, 0x1A0);
                    uint exefsSizeUnits = BitConverter.ToUInt32(ncch, 0x1A4);
                    if (exefsOffsetUnits > 0 && exefsSizeUnits > 0)
                    {
                        long exefsPos = ncchOffset + (long)exefsOffsetUnits * 0x200L;
                        if (noCrypto && exefsPos + 0x200 <= fs.Length)
                        {
                            fs.Position = exefsPos;
                            byte[] exefsHdr = br.ReadBytes(0x200);
                            for (int i = 0; i < 10; i++)
                            {
                                string name = Encoding.ASCII.GetString(exefsHdr, i * 16, 8).TrimEnd('\0');
                                uint fOff = BitConverter.ToUInt32(exefsHdr, i * 16 + 8);
                                uint fSize = BitConverter.ToUInt32(exefsHdr, i * 16 + 12);
                                if (!string.IsNullOrEmpty(name) && fSize > 0)
                                {
                                    results.Add(new VirtualArchiveEntry
                                    {
                                        Name = name,
                                        FullPath = "/exefs/" + name,
                                        Section = "ExeFS",
                                        Size = fSize,
                                        TitleId = tid,
                                        Is3dsDirectSlice = true,
                                        SliceOffset = exefsPos + 0x200 + fOff,
                                        SliceLength = fSize
                                    });
                                }
                            }
                        }
                        else
                        {
                            results.Add(new VirtualArchiveEntry
                            {
                                Name = "exefs.bin (зашифрован - используйте Дешифровку 3DS)",
                                FullPath = "/exefs.bin",
                                Section = "ExeFS",
                                Size = (long)exefsSizeUnits * 0x200L,
                                TitleId = tid,
                                Is3dsDirectSlice = true,
                                SliceOffset = exefsPos,
                                SliceLength = (long)exefsSizeUnits * 0x200L
                            });
                        }
                    }

                    uint romfsOffsetUnits = BitConverter.ToUInt32(ncch, 0x1B0);
                    uint romfsSizeUnits = BitConverter.ToUInt32(ncch, 0x1B4);
                    if (romfsOffsetUnits > 0 && romfsSizeUnits > 0)
                    {
                        long romfsPos = ncchOffset + (long)romfsOffsetUnits * 0x200L;
                        long romfsByteLen = (long)romfsSizeUnits * 0x200L;
                        if (noCrypto && romfsPos + 0x1000 <= fs.Length)
                        {
                            if (!TryParse3dsIvfcRomFs(fs, br, romfsPos, romfsByteLen, tid, results))
                            {
                                results.Add(new VirtualArchiveEntry
                                {
                                    Name = "romfs.bin",
                                    FullPath = "/romfs.bin",
                                    Section = "RomFS",
                                    Size = romfsByteLen,
                                    TitleId = tid,
                                    Is3dsDirectSlice = true,
                                    SliceOffset = romfsPos,
                                    SliceLength = romfsByteLen
                                });
                            }
                        }
                        else
                        {
                            results.Add(new VirtualArchiveEntry
                            {
                                Name = "romfs.bin (зашифрован - используйте Дешифровку 3DS)",
                                FullPath = "/romfs.bin",
                                Section = "RomFS",
                                Size = romfsByteLen,
                                TitleId = tid,
                                Is3dsDirectSlice = true,
                                SliceOffset = romfsPos,
                                SliceLength = romfsByteLen
                            });
                        }
                    }
                }

                long smdhPos = FindMagicInFile(fs, "SMDH", 16 * 1024 * 1024);
                if (smdhPos >= 0 && smdhPos + 0x36C0 <= fs.Length)
                {
                    results.Add(new VirtualArchiveEntry
                    {
                        Name = "icon.smdh",
                        FullPath = "/control/icon.smdh",
                        Section = "Control",
                        Size = 0x36C0,
                        Is3dsDirectSlice = true,
                        SliceOffset = smdhPos,
                        SliceLength = 0x36C0
                    });
                }
            }
            catch { }
        }

        private static bool TryParse3dsIvfcRomFs(FileStream fs, BinaryReader br, long romfsStart, long romfsLen, string tid, List<VirtualArchiveEntry> results)
        {
            try
            {
                var candidateOffsets = new List<long>();
                if (romfsStart + 0x60 <= fs.Length)
                {
                    fs.Position = romfsStart;
                    byte[] ivfcHdr = br.ReadBytes(0x60);
                    if (ivfcHdr.Length >= 0x60 &&
                        ivfcHdr[0] == (byte)'I' && ivfcHdr[1] == (byte)'V' && ivfcHdr[2] == (byte)'F' && ivfcHdr[3] == (byte)'C')
                    {
                        uint masterHashSize = BitConverter.ToUInt32(ivfcHdr, 0x08);
                        uint l3BlockLog2 = BitConverter.ToUInt32(ivfcHdr, 0x44);
                        if (l3BlockLog2 >= 9 && l3BlockLog2 <= 20)
                        {
                            long blockSize = 1L << (int)l3BlockLog2;
                            long alignedOff = (0x60L + masterHashSize + blockSize - 1L) & ~(blockSize - 1L);
                            candidateOffsets.Add(romfsStart + alignedOff);
                        }
                    }
                }
                if (!candidateOffsets.Contains(romfsStart + 0x1000))
                    candidateOffsets.Add(romfsStart + 0x1000);
                if (!candidateOffsets.Contains(romfsStart))
                    candidateOffsets.Add(romfsStart);

                foreach (long l3Pos in candidateOffsets)
                {
                    if (l3Pos + 0x28 > fs.Length) continue;
                    fs.Position = l3Pos;
                    uint hdrLen = br.ReadUInt32();
                    if (hdrLen != 0x28) continue;

                    uint dirHashOff = br.ReadUInt32();
                    uint dirHashLen = br.ReadUInt32();
                    uint dirMetaOff = br.ReadUInt32();
                    uint dirMetaLen = br.ReadUInt32();
                    uint fileHashOff = br.ReadUInt32();
                    uint fileHashLen = br.ReadUInt32();
                    uint fileMetaOff = br.ReadUInt32();
                    uint fileMetaLen = br.ReadUInt32();
                    uint fileDataOff = br.ReadUInt32();

                    if (dirMetaLen == 0 || fileMetaLen == 0 || l3Pos + fileMetaOff + fileMetaLen > fs.Length)
                        continue;

                    fs.Position = l3Pos + dirMetaOff;
                    byte[] dirMeta = br.ReadBytes((int)Math.Min(dirMetaLen, 8 * 1024 * 1024));
                    fs.Position = l3Pos + fileMetaOff;
                    byte[] fileMeta = br.ReadBytes((int)Math.Min(fileMetaLen, 16 * 1024 * 1024));

                    Traverse3dsRomFsDir(dirMeta, fileMeta, 0, "", l3Pos + fileDataOff, tid, results, 0);
                    return results.Any(r => r.Section == "RomFS");
                }
            }
            catch { }
            return false;
        }

        private static void Traverse3dsRomFsDir(byte[] dirMeta, byte[] fileMeta, uint dirOffset, string currentPath, long dataBaseOffset, string tid, List<VirtualArchiveEntry> results, int depth)
        {
            if (depth > 32 || dirOffset + 24 > dirMeta.Length) return;
            uint firstChildDir = BitConverter.ToUInt32(dirMeta, (int)dirOffset + 8);
            uint firstFile = BitConverter.ToUInt32(dirMeta, (int)dirOffset + 12);

            uint fOff = firstFile;
            int safety = 0;
            while (fOff != 0xFFFFFFFF && fOff + 32 <= fileMeta.Length && safety++ < 20000)
            {
                uint nextSiblingFile = BitConverter.ToUInt32(fileMeta, (int)fOff + 4);
                ulong dataOff = BitConverter.ToUInt64(fileMeta, (int)fOff + 8);
                ulong dataLen = BitConverter.ToUInt64(fileMeta, (int)fOff + 16);
                uint nameLen = BitConverter.ToUInt32(fileMeta, (int)fOff + 28);

                if (fOff + 32 + nameLen <= fileMeta.Length && nameLen > 0 && nameLen < 512)
                {
                    string fName = Encoding.Unicode.GetString(fileMeta, (int)fOff + 32, (int)nameLen).TrimEnd('\0');
                    results.Add(new VirtualArchiveEntry
                    {
                        Name = fName,
                        FullPath = currentPath + "/" + fName,
                        Section = "RomFS",
                        Size = (long)dataLen,
                        TitleId = tid,
                        Is3dsDirectSlice = true,
                        SliceOffset = dataBaseOffset + (long)dataOff,
                        SliceLength = (long)dataLen
                    });
                }
                fOff = nextSiblingFile;
            }

            uint cOff = firstChildDir;
            int dirSafety = 0;
            while (cOff != 0xFFFFFFFF && cOff + 24 <= dirMeta.Length && dirSafety++ < 5000)
            {
                uint nextSiblingDir = BitConverter.ToUInt32(dirMeta, (int)cOff + 4);
                uint nameLen = BitConverter.ToUInt32(dirMeta, (int)cOff + 20);
                string dName = "";
                if (cOff + 24 + nameLen <= dirMeta.Length && nameLen > 0 && nameLen < 512)
                    dName = Encoding.Unicode.GetString(dirMeta, (int)cOff + 24, (int)nameLen).TrimEnd('\0');
                string subPath = string.IsNullOrEmpty(dName) ? currentPath : (currentPath + "/" + dName);
                Traverse3dsRomFsDir(dirMeta, fileMeta, cOff, subPath, dataBaseOffset, tid, results, depth + 1);
                cOff = nextSiblingDir;
            }
        }

        private static long FindFirstNcchOffset(FileStream fs, BinaryReader br)
        {
            if (fs.Length >= 0x4200)
            {
                fs.Position = 0x100;
                if (Encoding.ASCII.GetString(br.ReadBytes(4)) == "NCSD")
                {
                    fs.Position = 0x120;
                    uint offUnits = br.ReadUInt32();
                    return offUnits > 0 ? (long)offUnits * 0x200L : 0x4000L;
                }
            }
            if (fs.Length >= 0x200)
            {
                fs.Position = 0x100;
                if (Encoding.ASCII.GetString(br.ReadBytes(4)) == "NCCH")
                    return 0;
            }
            return FindMagicAt100(fs, "NCCH", 8 * 1024 * 1024);
        }

        private static long FindMagicAt100(FileStream fs, string magic, long maxScan)
        {
            byte[] buf = new byte[64 * 1024];
            long limit = Math.Min(fs.Length, maxScan);
            long pos = 0;
            while (pos < limit - 0x200)
            {
                fs.Position = pos;
                int r = fs.Read(buf, 0, buf.Length);
                for (int i = 0x100; i <= r - 4; i += 0x40)
                {
                    if (buf[i] == magic[0] && buf[i + 1] == magic[1] && buf[i + 2] == magic[2] && buf[i + 3] == magic[3])
                        return pos + i - 0x100;
                }
                pos += r - 0x200;
            }
            return -1;
        }

        private static long FindMagicInFile(FileStream fs, string magic, long maxScan)
        {
            byte[] buf = new byte[64 * 1024];
            long limit = Math.Min(fs.Length, maxScan);
            long pos = 0;
            while (pos < limit - 4)
            {
                fs.Position = pos;
                int r = fs.Read(buf, 0, buf.Length);
                if (r < 4) break;
                for (int i = 0; i <= r - 4; i++)
                {
                    if (buf[i] == magic[0] && buf[i + 1] == magic[1] && buf[i + 2] == magic[2] && buf[i + 3] == magic[3])
                        return pos + i;
                }
                pos += r - 4;
            }
            return -1;
        }

        public static async Task<byte[]> ReadEntryBytesAsync(string containerPath, VirtualArchiveEntry entry, int maxBytes = 0)
        {
            return await Task.Run(() =>
            {
                if (string.IsNullOrEmpty(containerPath) || !File.Exists(containerPath) || entry == null)
                    return Array.Empty<byte>();

                if (entry.Is3dsDirectSlice)
                {
                    using var fs3ds = new FileStream(containerPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    if (entry.SliceOffset < 0 || entry.SliceOffset >= fs3ds.Length) return Array.Empty<byte>();
                    long len = entry.SliceLength;
                    if (maxBytes > 0 && len > maxBytes) len = maxBytes;
                    if (entry.SliceOffset + len > fs3ds.Length) len = fs3ds.Length - entry.SliceOffset;
                    long cap = maxBytes > 0 ? len : Math.Min(len, 64L * 1024 * 1024);
                    byte[] data = new byte[(int)cap];
                    fs3ds.Position = entry.SliceOffset;
                    int total = 0;
                    while (total < data.Length)
                    {
                        int r = fs3ds.Read(data, total, data.Length - total);
                        if (r <= 0) break;
                        total += r;
                    }
                    return total == data.Length ? data : data.Take(total).ToArray();
                }

                string ext = Path.GetExtension(containerPath).ToLowerInvariant();
                if (ext == ".nca")
                {
                    using var fsNca = new FileStream(containerPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    var nca = new Nca(App.Keys.CurrentKeyset, fsNca.AsStorage());
                    using var secFs = OpenNcaSectionFileSystem(nca, entry.NcaSectionIndex) as IDisposable;
                    return ReadFromFileSystem(secFs as IFileSystem, entry.FullPath, maxBytes);
                }

                using var container = SwitchContainerHandle.Open(containerPath);
                IFileSystem rootFs = container.FileSystem;
                SmartShrinkService.HarvestTicketsFromFileSystem(rootFs, App.Keys.CurrentKeyset);

                if (entry.NcaSectionIndex < 0)
                    return ReadFromFileSystem(rootFs, entry.FullPath, maxBytes);

                using var ncaRef = new UniqueRef<IFile>();
                using var ncaPath = new LibHac.Fs.Path();
                ncaPath.Initialize(new U8Span(Encoding.UTF8.GetBytes("/" + entry.SourceNcaName))).ThrowIfFailure();
                rootFs.OpenFile(ref ncaRef.Ref, in ncaPath, OpenMode.Read).ThrowIfFailure();
                using IFile ncaFile = ncaRef.Release();
                IStorage rawStorage = ncaFile.AsStorage();

                Dictionary<string, byte[]>? titleKeyMap = null;
                if (entry.SourceNcaName.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase))
                {
                    titleKeyMap = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
                    foreach (var tikEntry in rootFs.EnumerateEntries("/", "*.tik"))
                    {
                        try
                        {
                            using var fRef = new UniqueRef<IFile>();
                            using var pth = new LibHac.Fs.Path();
                            pth.Initialize(new U8Span(Encoding.UTF8.GetBytes(tikEntry.FullPath))).ThrowIfFailure();
                            rootFs.OpenFile(ref fRef.Ref, in pth, OpenMode.Read).ThrowIfFailure();
                            using IFile tikFile = fRef.Release();
                            using var ms = new MemoryStream();
                            tikFile.AsStream().CopyTo(ms);
                            byte[] tikBytes = ms.ToArray();
                            var decInfo = TicketHarvesterService.ExtractDecryptedTicket(tikBytes, tikBytes.Length, App.Keys.CurrentKeyset);
                            if (decInfo.HasValue)
                            {
                                titleKeyMap[decInfo.Value.RightsId] = decInfo.Value.TitleKey;
                            }
                        }
                        catch { }
                    }
                }

                using var nczStorage = titleKeyMap != null
                    ? new Core.NSZ.StormNczStorage(rawStorage, titleKeyMap, null, App.Keys.CurrentKeyset)
                    : null;

                var switchNca = new Nca(App.Keys.CurrentKeyset, nczStorage ?? rawStorage);
                var switchSecFs = OpenNcaSectionFileSystem(switchNca, entry.NcaSectionIndex);
                using var switchSecFsDisp = switchSecFs as IDisposable;
                return ReadFromFileSystem(switchSecFs, entry.FullPath, maxBytes);
            });
        }

        private static byte[] ReadFromFileSystem(IFileSystem? fs, string fullPath, int maxBytes)
        {
            if (fs == null) return Array.Empty<byte>();
            using var fRef = new UniqueRef<IFile>();
            using var pth = new LibHac.Fs.Path();
            pth.Initialize(new U8Span(Encoding.UTF8.GetBytes(fullPath))).ThrowIfFailure();
            fs.OpenFile(ref fRef.Ref, in pth, OpenMode.Read).ThrowIfFailure();
            using IFile file = fRef.Release();
            file.GetSize(out long fSize).ThrowIfFailure();
            long limit = maxBytes > 0 ? Math.Min(fSize, maxBytes) : Math.Min(fSize, 64L * 1024 * 1024);
            byte[] buf = new byte[(int)limit];
            file.Read(out long bytesRead, 0, buf).ThrowIfFailure();
            return bytesRead == buf.Length ? buf : buf.Take((int)bytesRead).ToArray();
        }

        private static async Task CopyIFileToStreamAsync(IFile sourceFile, Stream destStream, byte[] buffer, CancellationToken ct)
        {
            sourceFile.GetSize(out long totalSize).ThrowIfFailure();
            long offset = 0;
            while (offset < totalSize)
            {
                ct.ThrowIfCancellationRequested();
                int toRead = (int)Math.Min(buffer.Length, totalSize - offset);
                sourceFile.Read(out long bytesRead, offset, buffer.AsSpan(0, toRead)).ThrowIfFailure();
                if (bytesRead <= 0) break;
                await destStream.WriteAsync(buffer.AsMemory(0, (int)bytesRead), ct);
                offset += bytesRead;
            }
        }

        public static async Task<int> ExtractEntriesAsync(
            string containerPath,
            IEnumerable<VirtualArchiveEntry> entries,
            string destinationFolder,
            IProgress<(int Done, int Total, string CurrentFile)>? progress,
            CancellationToken ct)
        {
            const int ChunkSize = 1024 * 1024;
            var list = entries.ToList();
            if (list.Count == 0 || string.IsNullOrEmpty(containerPath) || !File.Exists(containerPath))
                return 0;

            int extracted = 0;
            byte[] buffer = new byte[ChunkSize];
            string ext = Path.GetExtension(containerPath).ToLowerInvariant();
            bool is3ds = Nintendo3dsService.Is3dsExtension(ext);

            if (is3ds)
            {
                using var srcFs = new FileStream(containerPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                for (int i = 0; i < list.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var entry = list[i];
                    progress?.Report((i + 1, list.Count, entry.FullPath));

                    string sectionRoot = StormToolResolver.EnsureSafeSubPath(destinationFolder, StormToolResolver.SanitizeFileName(entry.Section, "Section"));
                    string outPath = StormToolResolver.EnsureSafeSubPath(sectionRoot, entry.FullPath);
                    string? dir = Path.GetDirectoryName(outPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    if (entry.SliceOffset < 0 || entry.SliceOffset >= srcFs.Length) continue;
                    long remaining = Math.Min(entry.SliceLength, srcFs.Length - entry.SliceOffset);
                    srcFs.Position = entry.SliceOffset;

                    using var outFs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None, ChunkSize);
                    while (remaining > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        int toRead = (int)Math.Min(buffer.Length, remaining);
                        int read = await srcFs.ReadAsync(buffer.AsMemory(0, toRead), ct);
                        if (read <= 0) break;
                        await outFs.WriteAsync(buffer.AsMemory(0, read), ct);
                        remaining -= read;
                    }
                    extracted++;
                }
                return extracted;
            }

            if (ext == ".nca")
            {
                using var fsNca = new FileStream(containerPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var nca = new Nca(App.Keys.CurrentKeyset, fsNca.AsStorage());
                for (int i = 0; i < list.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    var entry = list[i];
                    progress?.Report((i + 1, list.Count, entry.FullPath));

                    string sectionRoot = StormToolResolver.EnsureSafeSubPath(destinationFolder, StormToolResolver.SanitizeFileName(entry.Section, "Section"));
                    string outPath = StormToolResolver.EnsureSafeSubPath(sectionRoot, entry.FullPath);
                    string? dir = Path.GetDirectoryName(outPath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);

                    var secFs = OpenNcaSectionFileSystem(nca, entry.NcaSectionIndex);
                    if (secFs == null) continue;
                    using var secFsDisp = secFs as IDisposable;

                    using IFile srcFile = OpenIFileSync(secFs, entry.FullPath);

                    using var outFs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None, ChunkSize);
                    await CopyIFileToStreamAsync(srcFile, outFs, buffer, ct);
                    extracted++;
                }
                return extracted;
            }

            using var container = SwitchContainerHandle.Open(containerPath);
            IFileSystem rootFs = container.FileSystem;
            SmartShrinkService.HarvestTicketsFromFileSystem(rootFs, App.Keys.CurrentKeyset);

            var titleKeyMap = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var tikEntry in rootFs.EnumerateEntries("/", "*.tik"))
            {
                try
                {
                    using IFile tikFile = OpenIFileSync(rootFs, tikEntry.FullPath);
                    using var ms = new MemoryStream();
                    tikFile.AsStream().CopyTo(ms);
                    byte[] tikBytes = ms.ToArray();
                    var decInfo = TicketHarvesterService.ExtractDecryptedTicket(tikBytes, tikBytes.Length, App.Keys.CurrentKeyset);
                    if (decInfo.HasValue)
                    {
                        titleKeyMap[decInfo.Value.RightsId] = decInfo.Value.TitleKey;
                    }
                }
                catch { }
            }

            for (int i = 0; i < list.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var entry = list[i];
                progress?.Report((i + 1, list.Count, entry.FullPath));

                string sectionRoot = StormToolResolver.EnsureSafeSubPath(destinationFolder, StormToolResolver.SanitizeFileName(entry.Section, "Section"));
                string outPath = StormToolResolver.EnsureSafeSubPath(sectionRoot, entry.FullPath);
                string? dir = Path.GetDirectoryName(outPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                if (entry.NcaSectionIndex < 0)
                {
                    using IFile srcFile = OpenIFileSync(rootFs, entry.FullPath);

                    using var outFs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None, ChunkSize);
                    await CopyIFileToStreamAsync(srcFile, outFs, buffer, ct);
                    extracted++;
                }
                else
                {
                    using IFile ncaFile = OpenIFileSync(rootFs, "/" + entry.SourceNcaName);
                    IStorage rawStorage = ncaFile.AsStorage();

                    using var nczStorage = entry.SourceNcaName.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase)
                        ? new Core.NSZ.StormNczStorage(rawStorage, titleKeyMap, null, App.Keys.CurrentKeyset)
                        : null;

                    var nca = new Nca(App.Keys.CurrentKeyset, nczStorage ?? rawStorage);
                    var secFs = OpenNcaSectionFileSystem(nca, entry.NcaSectionIndex);
                    if (secFs == null) continue;
                    using var secFsDisp = secFs as IDisposable;

                    using IFile srcFile = OpenIFileSync(secFs, entry.FullPath);

                    using var outFs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None, ChunkSize);
                    await CopyIFileToStreamAsync(srcFile, outFs, buffer, ct);
                    extracted++;
                }
            }

            return extracted;
        }

        private static IFile OpenIFileSync(IFileSystem fs, string fullPath)
        {
            using var fRef = new UniqueRef<IFile>();
            using var pth = new LibHac.Fs.Path();
            pth.Initialize(new U8Span(Encoding.UTF8.GetBytes(fullPath))).ThrowIfFailure();
            fs.OpenFile(ref fRef.Ref, in pth, OpenMode.Read).ThrowIfFailure();
            return fRef.Release();
        }
    }
}
