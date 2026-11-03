using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using StormSwitchBox.Models;
using ZstdSharp;

namespace StormSwitchBox.Services
{
    public class NupPackageInfo
    {
        public ulong TitleId { get; set; }
        public string TitleIdHex => $"{TitleId:X16}";
        public uint Version { get; set; }
        public byte Kind { get; set; } // 0 = Base, 1 = Update, 2 = DLC
        public byte Flags { get; set; }
        public string KindName => Kind switch { 0 => "Base", 1 => "Update", 2 => "DLC", _ => "Unknown" };
        public string TitleName { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public string DisplayVersion { get; set; } = string.Empty;
        public string BuildId { get; set; } = string.Empty;
        public byte[]? IconBytes { get; set; }
        public byte[]? NpdmBytes { get; set; }
        public byte[]? NacpBytes { get; set; }
        public uint RootDirIndex { get; set; } = 0xFFFFFFFF;
        public uint ExeFsDirIndex { get; set; } = 0xFFFFFFFF;
        public uint RomFsDirIndex { get; set; } = 0xFFFFFFFF;
    }

    public class NupInfo
    {
        public string FilePath { get; set; } = string.Empty;
        public ulong FileSize { get; set; }
        public ulong TotalUncompressedSize { get; set; }
        public ulong TotalCompressedSize { get; set; }
        public int PackageCount => Packages.Count;
        public int DirCount { get; set; }
        public int FileCount { get; set; }
        public int DeduplicatedFileCount { get; set; }
        public byte[] Uuid { get; set; } = new byte[16];
        public string UuidHex => Convert.ToHexString(Uuid);
        public List<NupPackageInfo> Packages { get; set; } = new();

        public string TitleId => Packages.FirstOrDefault(p => p.Kind == 0)?.TitleIdHex ?? Packages.FirstOrDefault()?.TitleIdHex ?? "0000000000000000";
        public string GameTitle => Packages.FirstOrDefault(p => !string.IsNullOrEmpty(p.TitleName))?.TitleName ?? "Nintendo Switch Game";
        public string Publisher => Packages.FirstOrDefault(p => !string.IsNullOrEmpty(p.Publisher))?.Publisher ?? "Nintendo";
        public string DisplayVersion
        {
            get
            {
                var upd = Packages.FirstOrDefault(p => p.Kind == 1);
                if (upd != null && !string.IsNullOrEmpty(upd.DisplayVersion)) return upd.DisplayVersion;
                var basePkg = Packages.FirstOrDefault(p => p.Kind == 0);
                if (basePkg != null && !string.IsNullOrEmpty(basePkg.DisplayVersion)) return basePkg.DisplayVersion;
                return "v0";
            }
        }
        public byte[]? CoverBytes => Packages.FirstOrDefault(p => p.IconBytes != null && p.IconBytes.Length > 0)?.IconBytes;

        public string PackageBreakdown
        {
            get
            {
                bool hasBase = Packages.Any(p => p.Kind == 0);
                var upd = Packages.FirstOrDefault(p => p.Kind == 1);
                int dlcCount = Packages.Count(p => p.Kind == 2);

                var parts = new List<string>();
                if (hasBase) parts.Add("БАЗА");
                if (upd != null)
                {
                    string v = !string.IsNullOrEmpty(upd.DisplayVersion) ? upd.DisplayVersion : $"v{upd.Version}";
                    parts.Add($"ПАТЧ {v}");
                }
                if (dlcCount > 0) parts.Add($"{dlcCount} DLC");

                return $"[NUP • {string.Join(" + ", parts)}]";
            }
        }

        public string GameName => GameTitle;
        public string Version => DisplayVersion;
        public byte[]? IconBytes => CoverBytes;
        public string ContentType => Packages.Any(p => p.Kind == 0) ? "Application" : (Packages.Any(p => p.Kind == 1) ? "Patch" : "AddOnContent");
        public int Files => FileCount;
    }

    /// <summary>
    /// Полный нативный сервис работы с контейнерами .nup (NUP v1.0, little-endian)
    /// и функциями smarkyie/NSP-Box (Plaintext NSP, SHA-256 Extent Deduplication, Emulator Patcher).
    /// </summary>
    public class NupContainerService
    {
        public static readonly byte[] NupMagic = new byte[] { 0x4E, 0x55, 0x50, 0x01 }; // "NUP\x01"
        public static readonly byte[] TocMagic = new byte[] { 0x54, 0x4F, 0x43, 0x01 }; // "TOC\x01"

        // ===== 1. Проверка NUP контейнера =====
        public static bool IsNupFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;

            if (path.EndsWith(".nup", StringComparison.OrdinalIgnoreCase))
                return true;

            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (fs.Length < 0x80) return false;
                byte[] magic = new byte[4];
                fs.ReadExactly(magic, 0, 4);
                return magic.SequenceEqual(NupMagic);
            }
            catch
            {
                return false;
            }
        }

        private static NupInfo? ParseTocBytes(byte[] tocBytes, string path, ulong fileSize, byte[] uuid)
        {
            if (tocBytes.Length < 0x28 || !tocBytes.AsSpan(0, 4).SequenceEqual(TocMagic))
                return null;

            uint tocPkgCount = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(0x04, 4));
            uint dirCount = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(0x08, 4));
            uint fileCount = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(0x0C, 4));
            uint childrenCount = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(0x10, 4));
            ulong strPoolOffset = BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(0x18, 8));
            ulong strPoolSize = BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(0x20, 8));

            var info = new NupInfo
            {
                FilePath = path,
                FileSize = fileSize,
                DirCount = (int)dirCount,
                FileCount = (int)fileCount,
                Uuid = uuid
            };

            // Чтение пакетов
            int pkgOffset = 0x28;
            for (int i = 0; i < tocPkgCount && pkgOffset + 0x40 <= tocBytes.Length; i++, pkgOffset += 0x40)
            {
                var span = tocBytes.AsSpan(pkgOffset, 0x40);
                ulong titleId = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(0x00, 8));
                uint rootDir = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(0x08, 4));
                uint exefsDir = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(0x0C, 4));
                uint romfsDir = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(0x10, 4));
                uint version = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(0x14, 4));
                byte kind = span[0x18];
                byte flags = span[0x19];
                ulong metaOffset = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(0x20, 8));
                ulong metaSize = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(0x28, 8));

                var pkg = new NupPackageInfo
                {
                    TitleId = titleId,
                    Version = version,
                    Kind = kind,
                    Flags = flags,
                    RootDirIndex = rootDir,
                    ExeFsDirIndex = exefsDir,
                    RomFsDirIndex = romfsDir
                };

                // Чтение TLV метаданных пакета
                if (metaOffset > 0 && metaSize > 0 && metaOffset + metaSize <= (ulong)tocBytes.Length)
                {
                    int metaCur = (int)metaOffset;
                    int metaEnd = (int)(metaOffset + metaSize);
                    while (metaCur + 8 <= metaEnd)
                    {
                        ushort tag = BinaryPrimitives.ReadUInt16LittleEndian(tocBytes.AsSpan(metaCur, 2));
                        uint len = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(metaCur + 4, 4));
                        metaCur += 8;
                        if (metaCur + len > metaEnd) break;

                        byte[] val = tocBytes.AsSpan(metaCur, (int)len).ToArray();
                        metaCur += (int)len;

                        switch (tag)
                        {
                            case 0x0001: // NPDM
                                pkg.NpdmBytes = val;
                                break;
                            case 0x0002: // NACP
                                pkg.NacpBytes = val;
                                break;
                            case 0x0003: // Icon
                                pkg.IconBytes = val;
                                break;
                            case 0x0004: // Display version
                                pkg.DisplayVersion = Encoding.UTF8.GetString(val).Trim('\0');
                                break;
                            case 0x0005: // Title name
                                pkg.TitleName = Encoding.UTF8.GetString(val).Trim('\0');
                                break;
                            case 0x0006: // Publisher
                                pkg.Publisher = Encoding.UTF8.GetString(val).Trim('\0');
                                break;
                            case 0x0007: // Build ID
                                pkg.BuildId = Convert.ToHexString(val);
                                break;
                        }
                    }
                }

                info.Packages.Add(pkg);
            }

            // Чтение файлов для подсчета размеров и дедупликации
            int dirsTableOffset = 0x28 + (int)tocPkgCount * 0x40;
            int filesTableOffset = dirsTableOffset + (int)dirCount * 0x18;

            var distinctExtents = new HashSet<ulong>();
            ulong totalUncompressed = 0;
            ulong totalCompressed = 0;

            int curFileOffset = filesTableOffset;
            for (int i = 0; i < fileCount && curFileOffset + 0x50 <= tocBytes.Length; i++, curFileOffset += 0x50)
            {
                var span = tocBytes.AsSpan(curFileOffset, 0x50);
                ulong size = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(0x08, 8));
                ulong extentOffset = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(0x10, 8));
                ulong extentSize = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(0x18, 8));

                totalUncompressed += size;
                if (distinctExtents.Add(extentOffset))
                {
                    totalCompressed += extentSize;
                }
            }

            info.TotalUncompressedSize = totalUncompressed;
            info.TotalCompressedSize = totalCompressed;
            info.DeduplicatedFileCount = distinctExtents.Count;

            return info;
        }


        // ===== 2. Анализ NUP контейнера =====
        public static NupInfo? InspectNupFile(string path) => InspectNupFileAsync(path).GetAwaiter().GetResult();

        public static async Task<NupInfo?> InspectNupFileAsync(string path)
        {
            if (!File.Exists(path)) return null;

            try
            {
                await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, useAsync: true);
                if (fs.Length < 0x80) return null;

                byte[] superblock = new byte[0x80];
                await fs.ReadExactlyAsync(superblock, 0, 0x80);

                if (!superblock.AsSpan(0, 4).SequenceEqual(NupMagic))
                    return null;

                ushort headerSize = BinaryPrimitives.ReadUInt16LittleEndian(superblock.AsSpan(0x04, 2));
                ushort headerVersion = BinaryPrimitives.ReadUInt16LittleEndian(superblock.AsSpan(0x06, 2));
                ulong tocOffset = BinaryPrimitives.ReadUInt64LittleEndian(superblock.AsSpan(0x08, 8));
                ulong tocSize = BinaryPrimitives.ReadUInt64LittleEndian(superblock.AsSpan(0x10, 8));
                byte[] tocSha256 = superblock.AsSpan(0x18, 32).ToArray();
                ulong blobOffset = BinaryPrimitives.ReadUInt64LittleEndian(superblock.AsSpan(0x38, 8));
                ulong fileSize = BinaryPrimitives.ReadUInt64LittleEndian(superblock.AsSpan(0x40, 8));
                uint packageCount = BinaryPrimitives.ReadUInt32LittleEndian(superblock.AsSpan(0x48, 4));
                byte[] uuid = superblock.AsSpan(0x50, 16).ToArray();

                if (tocOffset + tocSize > (ulong)fs.Length)
                    return null;

                fs.Seek((long)tocOffset, SeekOrigin.Begin);
                byte[] tocBytes = new byte[tocSize];
                await fs.ReadExactlyAsync(tocBytes, 0, (int)tocSize);

                return ParseTocBytes(tocBytes, path, fileSize > 0 ? fileSize : (ulong)fs.Length, uuid);
            }
            catch
            {
                return null;
            }
        }

        // ===== 3. Сборка Switch Dump в .nup контейнер с SHA-256 дедупликацией =====
        public static async Task PackSwitchDumpToNupAsync(
            string dumpRootDir,
            string outputNupPath,
            int zstdLevel = 3,
            IProgress<(double pct, string status)>? progress = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(dumpRootDir) || !Directory.Exists(dumpRootDir))
                throw new DirectoryNotFoundException($"Каталог дампа не найден: {dumpRootDir}");

            progress?.Report((5, "Анализ структуры папок дампа..."));

            var sw2Info = await Switch2DumpHelper.InspectSwitch2DumpDirectoryAsync(dumpRootDir);
            string baseTargetDir = sw2Info?.GameRootDir ?? dumpRootDir;

            // Определяем пакеты: Base, Updates, DLC
            var packagesToBuild = new List<DumpPackageSource>();

            string baseDir = Directory.Exists(Path.Combine(baseTargetDir, "base")) ? Path.Combine(baseTargetDir, "base") : baseTargetDir;
            ulong baseTid = 0;
            if (!string.IsNullOrEmpty(sw2Info?.TitleId) && ulong.TryParse(sw2Info.TitleId, System.Globalization.NumberStyles.HexNumber, null, out ulong bTid))
                baseTid = bTid;

            packagesToBuild.Add(new DumpPackageSource
            {
                Kind = 0, // Base
                TitleId = baseTid,
                Version = 0,
                RootDir = baseDir,
                ExeFsDir = Path.Combine(baseDir, "exefs"),
                RomFsDir = Path.Combine(baseDir, "romfs"),
                TitleName = sw2Info?.GameName ?? Path.GetFileName(dumpRootDir),
                Publisher = "Nintendo",
                DisplayVersion = "1.0.0",
                IconBytes = sw2Info?.CoverBytes
            });

            // Updates
            string updatesDir = Path.Combine(baseTargetDir, "updates");
            if (Directory.Exists(updatesDir))
            {
                foreach (var vDir in Directory.GetDirectories(updatesDir))
                {
                    string vName = Path.GetFileName(vDir);
                    uint.TryParse(vName.TrimStart('v', 'V'), out uint verNum);
                    ulong updTid = baseTid != 0 ? (baseTid | 0x800) : 0;

                    packagesToBuild.Add(new DumpPackageSource
                    {
                        Kind = 1, // Update
                        TitleId = updTid,
                        Version = verNum,
                        RootDir = vDir,
                        ExeFsDir = Path.Combine(vDir, "exefs"),
                        RomFsDir = Path.Combine(vDir, "romfs"),
                        TitleName = sw2Info?.GameName ?? Path.GetFileName(dumpRootDir),
                        Publisher = "Nintendo",
                        DisplayVersion = $"v{verNum}"
                    });
                }
            }

            // DLC
            string dlcDir = Path.Combine(baseTargetDir, "dlc");
            if (Directory.Exists(dlcDir))
            {
                foreach (var d in Directory.GetDirectories(dlcDir))
                {
                    string dName = Path.GetFileName(d);
                    ulong dlcTid = 0;
                    if (ulong.TryParse(dName, System.Globalization.NumberStyles.HexNumber, null, out ulong parsedDlcTid))
                        dlcTid = parsedDlcTid;

                    string dlcRomFs = Path.Combine(d, "romfs");
                    if (!Directory.Exists(dlcRomFs) && Directory.Exists(Path.Combine(d, "0", "romfs")))
                        dlcRomFs = Path.Combine(d, "0", "romfs");

                    packagesToBuild.Add(new DumpPackageSource
                    {
                        Kind = 2, // DLC
                        TitleId = dlcTid,
                        Version = 0,
                        RootDir = d,
                        RomFsDir = Directory.Exists(dlcRomFs) ? dlcRomFs : d,
                        TitleName = (sw2Info?.GameName ?? Path.GetFileName(dumpRootDir)) + $" [DLC {dName}]",
                        Publisher = "Nintendo",
                        DisplayVersion = "1.0.0"
                    });
                }
            }

            // Создаем виртуальное дерево директорий и файлов
            var allDirs = new List<VirtualNupDir>();
            var allFiles = new List<VirtualNupFile>();
            var stringPoolMap = new Dictionary<string, uint>(StringComparer.Ordinal);
            var stringPoolBytes = new List<byte>();

            uint AddString(string s)
            {
                if (stringPoolMap.TryGetValue(s, out uint off)) return off;
                byte[] b = Encoding.UTF8.GetBytes(s);
                uint newOff = (uint)stringPoolBytes.Count;
                stringPoolBytes.AddRange(b);
                stringPoolBytes.Add(0); // null-terminator
                stringPoolMap[s] = newOff;
                return newOff;
            }

            // Обходим дерево для каждого пакета
            foreach (var pkgSource in packagesToBuild)
            {
                var pkgRootDir = new VirtualNupDir { Name = Path.GetFileName(pkgSource.RootDir) };
                allDirs.Add(pkgRootDir);
                pkgSource.VirtualRootDir = pkgRootDir;

                if (Directory.Exists(pkgSource.ExeFsDir))
                {
                    var exefsDir = BuildVirtualTree(pkgSource.ExeFsDir, allDirs, allFiles, "exefs");
                    pkgRootDir.SubDirs.Add(exefsDir);
                    pkgSource.VirtualExeFsDir = exefsDir;
                }

                if (Directory.Exists(pkgSource.RomFsDir))
                {
                    var romfsDir = BuildVirtualTree(pkgSource.RomFsDir, allDirs, allFiles, "romfs");
                    pkgRootDir.SubDirs.Add(romfsDir);
                    pkgSource.VirtualRomFsDir = romfsDir;
                }
            }

            // Присваиваем линейные индексы папкам и файлам
            for (int i = 0; i < allDirs.Count; i++) allDirs[i].DirIndex = i;
            for (int i = 0; i < allFiles.Count; i++) allFiles[i].FileIndex = i;

            // Подготавливаем children массив
            var childrenList = new List<uint>();
            foreach (var dir in allDirs)
            {
                dir.FirstChild = (uint)childrenList.Count;
                dir.ChildCount = (uint)(dir.SubDirs.Count + dir.Files.Count);

                foreach (var sub in dir.SubDirs)
                    childrenList.Add((uint)sub.DirIndex);

                foreach (var f in dir.Files)
                    childrenList.Add((uint)(allDirs.Count + f.FileIndex));
            }

            // Временный файл блоба сжатых данных
            string tempBlobFile = Path.Combine(Path.GetTempPath(), $"nup_blob_{Guid.NewGuid():N}.tmp");
            try
            {
                var extentDedup = new Dictionary<string, (ulong OffsetInBlob, ulong ExtentSize, byte Codec)>(StringComparer.OrdinalIgnoreCase);

                await using (var blobStream = new FileStream(tempBlobFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 1024 * 1024, useAsync: true))
                {
                    int totalFiles = allFiles.Count;
                    int processedFiles = 0;

                    for (int i = 0; i < allFiles.Count; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        var f = allFiles[i];

                        byte[] fileData = await File.ReadAllBytesAsync(f.DiskPath, ct);
                        f.Size = (ulong)fileData.Length;
                        f.Sha256 = SHA256.HashData(fileData);
                        string hashHex = Convert.ToHexString(f.Sha256);

                        if (extentDedup.TryGetValue(hashHex, out var existing))
                        {
                            // Дедупликация: повторно используем экстент без записи на диск
                            f.ExtentOffsetInBlob = existing.OffsetInBlob;
                            f.ExtentSize = existing.ExtentSize;
                            f.Codec = existing.Codec;
                        }
                        else
                        {
                            // 16-байт выравнивание в блобе
                            long curPos = blobStream.Position;
                            long rem = curPos % 16;
                            if (rem != 0)
                            {
                                byte[] pad = new byte[16 - rem];
                                await blobStream.WriteAsync(pad, ct);
                            }

                            ulong offsetInBlob = (ulong)blobStream.Position;
                            byte[] dataToWrite = fileData;
                            byte codec = 0;

                            if (zstdLevel > 0 && fileData.Length > 64)
                            {
                                try
                                {
                                    using var compressor = new Compressor(Math.Clamp(zstdLevel, 1, 22));
                                    byte[] compressed = compressor.Wrap(fileData).ToArray();
                                    if (compressed.Length < fileData.Length)
                                    {
                                        dataToWrite = compressed;
                                        codec = 1; // Zstandard
                                    }
                                }
                                catch { }
                            }

                            await blobStream.WriteAsync(dataToWrite, ct);
                            f.ExtentOffsetInBlob = offsetInBlob;
                            f.ExtentSize = (ulong)dataToWrite.Length;
                            f.Codec = codec;

                            extentDedup[hashHex] = (offsetInBlob, f.ExtentSize, codec);
                        }

                        processedFiles++;
                        if (processedFiles % 10 == 0 || processedFiles == totalFiles)
                        {
                            double pct = 10 + ((double)processedFiles / totalFiles * 70);
                            progress?.Report((pct, $"Сжатие и дедупликация файлов: {processedFiles}/{totalFiles}..."));
                        }
                    }
                }

                progress?.Report((82, "Формирование TOC заголовка и метаданных TLV..."));

                // Собираем String Pool
                foreach (var d in allDirs)
                {
                    d.NameOff = AddString(d.Name);
                    d.NameLen = (uint)Encoding.UTF8.GetByteCount(d.Name);
                }
                foreach (var f in allFiles)
                {
                    f.NameOff = AddString(f.Name);
                    f.NameLen = (uint)Encoding.UTF8.GetByteCount(f.Name);
                }

                // Сборка TOC в памяти
                using var tocMs = new MemoryStream();
                using var tocBw = new BinaryWriter(tocMs);

                // Заголовок TOC (0x28 байт)
                tocBw.Write(TocMagic);
                tocBw.Write((uint)packagesToBuild.Count);
                tocBw.Write((uint)allDirs.Count);
                tocBw.Write((uint)allFiles.Count);
                tocBw.Write((uint)childrenList.Count);
                tocBw.Write((uint)0); // reserved
                tocBw.Write((ulong)0); // str_pool_offset (0x18..0x1F)
                tocBw.Write((ulong)0); // str_pool_size (0x20..0x27)

                // Предварительно резервируем место под NupPackage[]
                long packagesStartPos = tocMs.Position; // 0x28
                for (int i = 0; i < packagesToBuild.Count; i++)
                {
                    tocBw.Write(new byte[0x40]);
                }

                // Запись NupDir[]
                long dirsStartPos = tocMs.Position;
                foreach (var d in allDirs)
                {
                    tocBw.Write(d.NameOff);
                    tocBw.Write(d.NameLen);
                    tocBw.Write(d.FirstChild);
                    tocBw.Write(d.ChildCount);
                    tocBw.Write((ulong)0); // reserved
                }

                // Запись NupFile[] (ExtentOffset скорректируем после расчета blobOffset)
                long filesStartPos = tocMs.Position;
                foreach (var f in allFiles)
                {
                    tocBw.Write(f.NameOff);
                    tocBw.Write(f.NameLen);
                    tocBw.Write(f.Size);
                    tocBw.Write(f.ExtentOffsetInBlob); // временный офсет
                    tocBw.Write(f.ExtentSize);
                    tocBw.Write(f.Sha256);
                    tocBw.Write(f.Codec);
                    tocBw.Write((byte)0); // flags
                    tocBw.Write(new byte[14]); // reserved
                }

                // Запись children[]
                long childrenStartPos = tocMs.Position;
                foreach (var c in childrenList)
                {
                    tocBw.Write(c);
                }

                // Запись String Pool
                long strPoolStartPos = tocMs.Position;
                tocBw.Write(stringPoolBytes.ToArray());
                long strPoolEndPos = tocMs.Position;

                // Запись Metadata TLV blocks для каждого пакета
                var packageMetaRanges = new List<(ulong Offset, ulong Size)>();
                foreach (var pkg in packagesToBuild)
                {
                    ulong metaStart = (ulong)tocMs.Position;
                    using var metaMs = new MemoryStream();
                    using var metaBw = new BinaryWriter(metaMs);

                    void WriteTlv(ushort tag, byte[] val)
                    {
                        if (val == null || val.Length == 0) return;
                        metaBw.Write(tag);
                        metaBw.Write((ushort)0);
                        metaBw.Write((uint)val.Length);
                        metaBw.Write(val);
                    }

                    if (!string.IsNullOrEmpty(pkg.TitleName))
                        WriteTlv(0x0005, Encoding.UTF8.GetBytes(pkg.TitleName));
                    if (!string.IsNullOrEmpty(pkg.Publisher))
                        WriteTlv(0x0006, Encoding.UTF8.GetBytes(pkg.Publisher));
                    if (!string.IsNullOrEmpty(pkg.DisplayVersion))
                        WriteTlv(0x0004, Encoding.UTF8.GetBytes(pkg.DisplayVersion));
                    if (pkg.IconBytes != null && pkg.IconBytes.Length > 0)
                        WriteTlv(0x0003, pkg.IconBytes);

                    // NPDM из exefs/main.npdm
                    string npdmFile = Path.Combine(pkg.ExeFsDir, "main.npdm");
                    if (File.Exists(npdmFile))
                    {
                        try { WriteTlv(0x0001, File.ReadAllBytes(npdmFile)); } catch { }
                    }

                    byte[] metaBytes = metaMs.ToArray();
                    tocBw.Write(metaBytes);
                    packageMetaRanges.Add((metaStart, (ulong)metaBytes.Length));
                }

                ulong finalTocSize = (ulong)tocMs.Length;
                ulong tocOffset = 0x80;
                ulong blobOffset = (tocOffset + finalTocSize + 15) & ~15UL;

                // Обновляем офсеты в TOC
                // 1. str_pool_offset в заголовке TOC
                tocMs.Seek(0x18, SeekOrigin.Begin);
                tocBw.Write((ulong)strPoolStartPos);
                tocBw.Write((ulong)stringPoolBytes.Count);

                // 2. NupPackage[]
                tocMs.Seek(packagesStartPos, SeekOrigin.Begin);
                for (int i = 0; i < packagesToBuild.Count; i++)
                {
                    var p = packagesToBuild[i];
                    var meta = packageMetaRanges[i];

                    tocBw.Write(p.TitleId);
                    tocBw.Write((uint)(p.VirtualRootDir?.DirIndex ?? -1));
                    tocBw.Write((uint)(p.VirtualExeFsDir?.DirIndex ?? -1));
                    tocBw.Write((uint)(p.VirtualRomFsDir?.DirIndex ?? -1));
                    tocBw.Write(p.Version);
                    tocBw.Write(p.Kind);
                    tocBw.Write((byte)0); // flags
                    tocBw.Write(new byte[6]); // reserved
                    tocBw.Write(meta.Offset);
                    tocBw.Write(meta.Size);
                    tocBw.Write(new byte[16]); // reserved2
                }

                // 3. Скорректировать ExtentOffset в NupFile[] (абсолютный офсет >= blobOffset)
                tocMs.Seek(filesStartPos, SeekOrigin.Begin);
                foreach (var f in allFiles)
                {
                    f.ExtentOffsetInBlob += blobOffset;
                    tocBw.Write(f.NameOff);
                    tocBw.Write(f.NameLen);
                    tocBw.Write(f.Size);
                    tocBw.Write(f.ExtentOffsetInBlob); // абсолютный офсет
                    tocBw.Write(f.ExtentSize);
                    tocBw.Write(f.Sha256);
                    tocBw.Write(f.Codec);
                    tocBw.Write((byte)0);
                    tocBw.Write(new byte[14]);
                }

                byte[] finalTocBytes = tocMs.ToArray();
                byte[] tocSha256 = SHA256.HashData(finalTocBytes);

                // Расчет UUID
                using var uuidMs = new MemoryStream();
                foreach (var p in packagesToBuild)
                {
                    uuidMs.Write(BitConverter.GetBytes(p.TitleId));
                    uuidMs.Write(BitConverter.GetBytes(p.Version));
                    uuidMs.WriteByte(p.Kind);
                }
                byte[] fullUuidHash = SHA256.HashData(uuidMs.ToArray());
                byte[] uuid16 = fullUuidHash.AsSpan(0, 16).ToArray();

                progress?.Report((90, "Запись и финализация .nup файла..."));

                // Запись финального файла
                string outDir = Path.GetDirectoryName(outputNupPath) ?? "";
                if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);

                await using (var outFs = new FileStream(outputNupPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, useAsync: true))
                {
                    long blobLength = new FileInfo(tempBlobFile).Length;
                    ulong finalFileSize = blobOffset + (ulong)blobLength;

                    // Superblock (0x80)
                    byte[] superblock = new byte[0x80];
                    NupMagic.CopyTo(superblock, 0x00);
                    BinaryPrimitives.WriteUInt16LittleEndian(superblock.AsSpan(0x04, 2), 0x80);
                    BinaryPrimitives.WriteUInt16LittleEndian(superblock.AsSpan(0x06, 2), 1);
                    BinaryPrimitives.WriteUInt64LittleEndian(superblock.AsSpan(0x08, 8), tocOffset);
                    BinaryPrimitives.WriteUInt64LittleEndian(superblock.AsSpan(0x10, 8), (ulong)finalTocBytes.Length);
                    tocSha256.CopyTo(superblock.AsSpan(0x18, 32));
                    BinaryPrimitives.WriteUInt64LittleEndian(superblock.AsSpan(0x38, 8), blobOffset);
                    BinaryPrimitives.WriteUInt64LittleEndian(superblock.AsSpan(0x40, 8), finalFileSize);
                    BinaryPrimitives.WriteUInt32LittleEndian(superblock.AsSpan(0x48, 4), (uint)packagesToBuild.Count);
                    BinaryPrimitives.WriteUInt32LittleEndian(superblock.AsSpan(0x4C, 4), 0); // flags
                    uuid16.CopyTo(superblock.AsSpan(0x50, 16));

                    await outFs.WriteAsync(superblock, ct);

                    // TOC
                    await outFs.WriteAsync(finalTocBytes, ct);

                    // Padding до blobOffset
                    long curWritten = outFs.Position;
                    if ((ulong)curWritten < blobOffset)
                    {
                        byte[] pad = new byte[blobOffset - (ulong)curWritten];
                        await outFs.WriteAsync(pad, ct);
                    }

                    // Копирование блоба сжатых данных
                    await using (var inBlob = new FileStream(tempBlobFile, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, useAsync: true))
                    {
                        await inBlob.CopyToAsync(outFs, 1024 * 1024, ct);
                    }
                }

                progress?.Report((100, "Успешно упакован в .nup!"));
            }
            finally
            {
                if (File.Exists(tempBlobFile))
                {
                    try { File.Delete(tempBlobFile); } catch { }
                }
            }
        }

        // ===== 4. Распаковка .nup контейнера в каталог =====
        public static async Task ExtractNupToDirectoryAsync(
            string nupPath,
            string outputDir,
            IProgress<(double pct, string status)>? progress = null,
            CancellationToken ct = default)
        {
            if (!File.Exists(nupPath))
                throw new FileNotFoundException($"Файл NUP не найден: {nupPath}");

            Directory.CreateDirectory(outputDir);
            progress?.Report((5, "Чтение оглавления .nup контейнера..."));

            await using var fs = new FileStream(nupPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, useAsync: true);
            byte[] superblock = new byte[0x80];
            await fs.ReadExactlyAsync(superblock, 0, 0x80, ct);

            if (!superblock.AsSpan(0, 4).SequenceEqual(NupMagic))
                throw new InvalidDataException("Файл не является валидным NUP v1.0 контейнером!");

            ulong tocOffset = BinaryPrimitives.ReadUInt64LittleEndian(superblock.AsSpan(0x08, 8));
            ulong tocSize = BinaryPrimitives.ReadUInt64LittleEndian(superblock.AsSpan(0x10, 8));
            ulong blobOffset = BinaryPrimitives.ReadUInt64LittleEndian(superblock.AsSpan(0x38, 8));

            fs.Seek((long)tocOffset, SeekOrigin.Begin);
            byte[] tocBytes = new byte[tocSize];
            await fs.ReadExactlyAsync(tocBytes, 0, (int)tocSize, ct);

            uint packageCount = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(0x04, 4));
            uint dirCount = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(0x08, 4));
            uint fileCount = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(0x0C, 4));
            uint childrenCount = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(0x10, 4));
            ulong strPoolOffset = BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(0x18, 8));
            ulong strPoolSize = BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(0x20, 8));

            string GetString(uint off, uint len)
            {
                int start = (int)(strPoolOffset + off);
                if (start >= tocBytes.Length) return string.Empty;
                int actualLen = (int)len;
                if (start + actualLen > tocBytes.Length) actualLen = tocBytes.Length - start;
                return Encoding.UTF8.GetString(tocBytes, start, actualLen).TrimEnd('\0');
            }

            // Читаем Dirs
            int dirsTableOffset = 0x28 + (int)packageCount * 0x40;
            var dirs = new List<(uint NameOff, uint NameLen, uint FirstChild, uint ChildCount)>();
            for (int i = 0; i < dirCount; i++)
            {
                int off = dirsTableOffset + i * 0x18;
                dirs.Add((
                    BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(off, 4)),
                    BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(off + 4, 4)),
                    BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(off + 8, 4)),
                    BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(off + 12, 4))
                ));
            }

            // Читаем Files
            int filesTableOffset = dirsTableOffset + (int)dirCount * 0x18;
            var files = new List<NupFileEntry>();
            for (int i = 0; i < fileCount; i++)
            {
                int off = filesTableOffset + i * 0x50;
                files.Add(new NupFileEntry
                {
                    NameOff = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(off, 4)),
                    NameLen = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(off + 4, 4)),
                    Size = BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(off + 8, 8)),
                    ExtentOffset = BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(off + 16, 8)),
                    ExtentSize = BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(off + 24, 8)),
                    Sha256 = tocBytes.AsSpan(off + 32, 32).ToArray(),
                    Codec = tocBytes[off + 64]
                });
            }

            // Читаем children[]
            int childrenStart = filesTableOffset + (int)fileCount * 0x50;
            var children = new uint[childrenCount];
            for (int i = 0; i < childrenCount; i++)
            {
                children[i] = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(childrenStart + i * 4, 4));
            }

            // Читаем пакеты и распаковываем
            int pkgStart = 0x28;
            for (int p = 0; p < packageCount; p++)
            {
                int pOff = pkgStart + p * 0x40;
                ulong titleId = BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(pOff, 8));
                uint rootDirIdx = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(pOff + 8, 4));
                uint exefsDirIdx = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(pOff + 12, 4));
                uint romfsDirIdx = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(pOff + 16, 4));
                uint version = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(pOff + 20, 4));
                byte kind = tocBytes[pOff + 24];

                string pkgFolder = outputDir;
                if (kind == 0) // Base
                {
                    pkgFolder = Path.Combine(outputDir, "base");
                }
                else if (kind == 1) // Update
                {
                    pkgFolder = Path.Combine(outputDir, "updates", version.ToString());
                }
                else if (kind == 2) // DLC
                {
                    pkgFolder = Path.Combine(outputDir, "dlc", $"{titleId:X16}");
                }
                Directory.CreateDirectory(pkgFolder);

                // Если rootDirIdx валиден
                if (rootDirIdx < dirCount)
                {
                    await ExtractDirRecursiveAsync(fs, blobOffset, rootDirIdx, pkgFolder, dirs, files, children, GetString, ct);
                }
                else
                {
                    if (exefsDirIdx < dirCount)
                        await ExtractDirRecursiveAsync(fs, blobOffset, exefsDirIdx, Path.Combine(pkgFolder, "exefs"), dirs, files, children, GetString, ct);
                    if (romfsDirIdx < dirCount)
                        await ExtractDirRecursiveAsync(fs, blobOffset, romfsDirIdx, Path.Combine(pkgFolder, "romfs"), dirs, files, children, GetString, ct);
                }

                double pct = 10 + ((double)(p + 1) / packageCount * 85);
                progress?.Report((pct, $"Распакован пакет [{p + 1}/{packageCount}]: {kind switch { 0 => "Base", 1 => "Update", 2 => "DLC", _ => "Package" }}"));
            }

            progress?.Report((100, "Распаковка .nup завершена успешно!"));
        }

        private static async Task ExtractDirRecursiveAsync(
            Stream nupStream,
            ulong blobOffset,
            uint dirIdx,
            string currentOutDir,
            List<(uint NameOff, uint NameLen, uint FirstChild, uint ChildCount)> dirs,
            List<NupFileEntry> files,
            uint[] children,
            Func<uint, uint, string> getString,
            CancellationToken ct)
        {
            Directory.CreateDirectory(currentOutDir);
            var d = dirs[(int)dirIdx];

            for (uint i = 0; i < d.ChildCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                uint childVal = children[d.FirstChild + i];
                if (childVal < dirs.Count)
                {
                    // Подпапка
                    string subName = getString(dirs[(int)childVal].NameOff, dirs[(int)childVal].NameLen);
                    if (string.IsNullOrEmpty(subName)) subName = $"dir_{childVal}";
                    string subPath = Path.Combine(currentOutDir, subName);
                    await ExtractDirRecursiveAsync(nupStream, blobOffset, childVal, subPath, dirs, files, children, getString, ct);
                }
                else
                {
                    // Файл
                    int fileIdx = (int)(childVal - (uint)dirs.Count);
                    if (fileIdx >= 0 && fileIdx < files.Count)
                    {
                        var fe = files[fileIdx];
                        string fileName = getString(fe.NameOff, fe.NameLen);
                        if (string.IsNullOrEmpty(fileName)) fileName = $"file_{fileIdx}.bin";
                        string filePath = Path.Combine(currentOutDir, fileName);

                        ulong readOffset = fe.ExtentOffset < blobOffset ? (blobOffset + fe.ExtentOffset) : fe.ExtentOffset;
                        nupStream.Seek((long)readOffset, SeekOrigin.Begin);

                        byte[] extentBytes = new byte[fe.ExtentSize];
                        await nupStream.ReadExactlyAsync(extentBytes, 0, (int)fe.ExtentSize, ct);

                        byte[] uncompressedData;
                        if (fe.Codec == 1) // Zstandard
                        {
                            using var decompressor = new Decompressor();
                            uncompressedData = decompressor.Unwrap(extentBytes).ToArray();
                        }
                        else
                        {
                            uncompressedData = extentBytes;
                        }

                        await File.WriteAllBytesAsync(filePath, uncompressedData, ct);
                    }
                }
            }
        }

        // ===== 5. Конвертация между контейнерами и .nup =====
        public static async Task ConvertContainerToNupAsync(
            string inputPath,
            string outputNupPath,
            int zstdLevel = 3,
            IProgress<(double pct, string status)>? progress = null,
            CancellationToken ct = default)
        {
            if (Directory.Exists(inputPath))
            {
                await PackSwitchDumpToNupAsync(inputPath, outputNupPath, zstdLevel, progress, ct);
                return;
            }

            if (!File.Exists(inputPath))
                throw new FileNotFoundException($"Входной файл не найден: {inputPath}");

            // Если входной файл уже .nup, а цель NSP/NSZ/XCI
            if (IsNupFile(inputPath))
            {
                string tempExtractDir = Path.Combine(Path.GetTempPath(), $"nup_ext_{Guid.NewGuid():N}");
                try
                {
                    progress?.Report((15, "Распаковка исходного .nup контейнера во временный дамп..."));
                    await ExtractNupToDirectoryAsync(inputPath, tempExtractDir, progress, ct);

                    string targetExt = Path.GetExtension(outputNupPath).ToLowerInvariant();
                    string outDir = Path.GetDirectoryName(outputNupPath) ?? "";
                    if (string.IsNullOrEmpty(outDir)) outDir = Directory.GetCurrentDirectory();

                    progress?.Report((60, $"Компиляция в целевой контейнер {targetExt.ToUpperInvariant()}..."));

                    var dummyTask = new ProcessingTask
                    {
                        TargetFormat = targetExt.TrimStart('.').ToUpperInvariant(),
                        OutputFolder = outDir,
                        OutputFileName = Path.GetFileNameWithoutExtension(outputNupPath)
                    };

                    await Switch2DumpHelper.CompileSwitch2DumpToContainerAsync(
                        dummyTask,
                        tempExtractDir,
                        new List<string> { tempExtractDir },
                        outputNupPath,
                        ct);

                    progress?.Report((100, $"Конвертация .nup -> {targetExt.ToUpperInvariant()} завершена!"));
                }
                finally
                {
                    if (Directory.Exists(tempExtractDir))
                    {
                        try { Directory.Delete(tempExtractDir, recursive: true); } catch { }
                    }
                }
                return;
            }

            // Если входной файл .nsp / .nsz / .xci / .xcz — распаковываем в дамп и упаковываем в .nup
            string tempDumpDir = Path.Combine(Path.GetTempPath(), $"dump_for_nup_{Guid.NewGuid():N}");
            try
            {
                progress?.Report((15, $"Распаковка {Path.GetFileName(inputPath)} во временный FS-дамп..."));
                Directory.CreateDirectory(tempDumpDir);

                // Вызываем распаковку контейнера
                var dummyTask = new ProcessingTask { OutputFolder = tempDumpDir };
                await App.SwitchFormat.UnpackContainerAsync(dummyTask, inputPath, tempDumpDir, ct);

                progress?.Report((50, "Упаковка дампа в .nup контейнер..."));
                await PackSwitchDumpToNupAsync(tempDumpDir, outputNupPath, zstdLevel, progress, ct);
                progress?.Report((100, "Успешно сконвертировано в .nup!"));
            }
            finally
            {
                if (Directory.Exists(tempDumpDir))
                {
                    try { Directory.Delete(tempDumpDir, recursive: true); } catch { }
                }
            }
        }

        // ===== 6. Обратимый патчер эмулятора (smarkyie/NSP-Box Reversible Emulator Patcher) =====
        public static bool PatchSwitch2EmulatorExe(string exePath, out string message)
        {
            if (!File.Exists(exePath))
            {
                message = $"Файл эмулятора не найден: {exePath}";
                return false;
            }

            try
            {
                string bakPath = exePath + ".bak";
                if (!File.Exists(bakPath))
                {
                    File.Copy(exePath, bakPath, overwrite: false);
                }

                byte[] exeBytes = File.ReadAllBytes(exePath);
                bool modified = false;

                // Паттерн 1: inline-pokedinner signature check bypass
                // Сигнатура: 48 8D 4D ? 48 89 44 24 ? E8
                int[] p1 = new int[] { 0x48, 0x8D, 0x4D, -1, 0x48, 0x89, 0x44, 0x24, -1, 0xE8 };
                int idx = FindPattern(exeBytes, p1);
                if (idx != -1)
                {
                    int callOffset = idx + 9;
                    if (callOffset + 5 <= exeBytes.Length)
                    {
                        // Заменяем E8 xx xx xx xx на NOP (90 90 90 90 90)
                        exeBytes[callOffset] = 0x90;
                        exeBytes[callOffset + 1] = 0x90;
                        exeBytes[callOffset + 2] = 0x90;
                        exeBytes[callOffset + 3] = 0x90;
                        exeBytes[callOffset + 4] = 0x90;
                        modified = true;
                    }
                }

                // Паттерн 2: msvc-read9 external NPDM check bypass
                // Сигнатура: 48 85 C0 74 ? 48 8B
                int[] p2 = new int[] { 0x48, 0x85, 0xC0, 0x74, -1, 0x48, 0x8B };
                int idx2 = FindPattern(exeBytes, p2);
                if (idx2 != -1)
                {
                    // Заменяем 74 (JZ) на EB (JMP) для обхода проверки внешнего main.npdm
                    exeBytes[idx2 + 3] = 0xEB;
                    modified = true;
                }

                if (modified)
                {
                    File.WriteAllBytes(exePath, exeBytes);
                    message = "Патч эмулятора успешно применен (создана резервная копия .bak). Эмулятор теперь поддерживает нативное чтение метаданных без внешнего main.npdm.";
                    return true;
                }

                // Проверяем, был ли файл уже пропатчен ранее
                int[] p1Patched = new int[] { 0x48, 0x8D, 0x4D, -1, 0x48, 0x89, 0x44, 0x24, -1, 0x90, 0x90, 0x90, 0x90, 0x90 };
                if (FindPattern(exeBytes, p1Patched) != -1)
                {
                    message = "Исполняемый файл эмулятора уже пропатчен.";
                    return true;
                }

                message = "Сигнатуры патча не обнаружены в данном исполняемом файле (возможно, неподдерживаемая версия эмулятора).";
                return false;
            }
            catch (Exception ex)
            {
                message = $"Ошибка при модификации эмулятора: {ex.Message}";
                return false;
            }
        }

        public async Task ConvertContainerToNupAsync(ProcessingTask task, string inputPath, string outDir, CancellationToken ct = default)
        {
            string outNup = System.IO.Path.Combine(outDir, System.IO.Path.GetFileNameWithoutExtension(inputPath) + ".nup");
            var prog = new Progress<(double pct, string status)>(p =>
            {
                App.RunOnUI(() =>
                {
                    task.Progress = p.pct;
                    task.Status = p.status;
                    task.LogDetails += $"\n[NUP] {p.status}";
                });
            });
            int level = App.Settings.Current.CompressionLevel;
            await ConvertContainerToNupAsync(inputPath, outNup, level, prog, ct);
            App.RunOnUI(() =>
            {
                task.Progress = 100;
                task.Status = "Успешно";
                task.LogDetails += $"\nСохранен: {outNup}";
            });
        }

        public static bool RestoreSwitch2EmulatorExe(string exePath, out string message)
        {
            string bakPath = exePath + ".bak";
            if (!File.Exists(bakPath))
            {
                message = $"Резервная копия не найдена: {bakPath}";
                return false;
            }

            try
            {
                File.Copy(bakPath, exePath, overwrite: true);
                message = "Оригинальный исполняемый файл эмулятора успешно восстановлен из .bak.";
                return true;
            }
            catch (Exception ex)
            {
                message = $"Ошибка при восстановлении: {ex.Message}";
                return false;
            }
        }

        private static int FindPattern(byte[] src, int[] pattern)
        {
            int max = src.Length - pattern.Length;
            for (int i = 0; i <= max; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (pattern[j] != -1 && src[i + j] != (byte)pattern[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return i;
            }
            return -1;
        }

        private static VirtualNupDir BuildVirtualTree(string sourceDir, List<VirtualNupDir> allDirs, List<VirtualNupFile> allFiles, string dirName)
        {
            var node = new VirtualNupDir { Name = dirName };
            allDirs.Add(node);

            foreach (var f in Directory.GetFiles(sourceDir))
            {
                var vf = new VirtualNupFile
                {
                    Name = Path.GetFileName(f),
                    DiskPath = f
                };
                allFiles.Add(vf);
                node.Files.Add(vf);
            }

            foreach (var d in Directory.GetDirectories(sourceDir))
            {
                var sub = BuildVirtualTree(d, allDirs, allFiles, Path.GetFileName(d));
                node.SubDirs.Add(sub);
            }

            return node;
        }

        private class DumpPackageSource
        {
            public byte Kind { get; set; }
            public ulong TitleId { get; set; }
            public uint Version { get; set; }
            public string RootDir { get; set; } = string.Empty;
            public string ExeFsDir { get; set; } = string.Empty;
            public string RomFsDir { get; set; } = string.Empty;
            public string TitleName { get; set; } = string.Empty;
            public string Publisher { get; set; } = string.Empty;
            public string DisplayVersion { get; set; } = string.Empty;
            public byte[]? IconBytes { get; set; }

            public VirtualNupDir? VirtualRootDir { get; set; }
            public VirtualNupDir? VirtualExeFsDir { get; set; }
            public VirtualNupDir? VirtualRomFsDir { get; set; }
        }

        private class VirtualNupDir
        {
            public string Name { get; set; } = string.Empty;
            public uint NameOff { get; set; }
            public uint NameLen { get; set; }
            public uint FirstChild { get; set; }
            public uint ChildCount { get; set; }
            public int DirIndex { get; set; } = -1;
            public List<VirtualNupDir> SubDirs { get; } = new();
            public List<VirtualNupFile> Files { get; } = new();
        }

        private class VirtualNupFile
        {
            public string Name { get; set; } = string.Empty;
            public string DiskPath { get; set; } = string.Empty;
            public uint NameOff { get; set; }
            public uint NameLen { get; set; }
            public ulong Size { get; set; }
            public ulong ExtentOffsetInBlob { get; set; }
            public ulong ExtentSize { get; set; }
            public byte[] Sha256 { get; set; } = new byte[32];
            public byte Codec { get; set; } // 0 = raw, 1 = zstd
            public int FileIndex { get; set; } = -1;
        }

        private class NupFileEntry
        {
            public uint NameOff { get; set; }
            public uint NameLen { get; set; }
            public ulong Size { get; set; }
            public ulong ExtentOffset { get; set; }
            public ulong ExtentSize { get; set; }
            public byte[] Sha256 { get; set; } = new byte[32];
            public byte Codec { get; set; }
        }
    }
}
