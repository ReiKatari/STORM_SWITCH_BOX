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
        public string GameTitle => Packages.FirstOrDefault(p => p.Kind == 0 && !string.IsNullOrEmpty(p.TitleName))?.TitleName ?? Packages.FirstOrDefault(p => !string.IsNullOrEmpty(p.TitleName))?.TitleName ?? "Nintendo Switch Game";
        public string Publisher => Packages.FirstOrDefault(p => p.Kind == 0 && !string.IsNullOrEmpty(p.Publisher))?.Publisher ?? Packages.FirstOrDefault(p => !string.IsNullOrEmpty(p.Publisher))?.Publisher ?? "Nintendo";
        public string DisplayVersion
        {
            get
            {
                var upd = Packages.FirstOrDefault(p => p.Kind == 1);
                if (upd != null && !string.IsNullOrEmpty(upd.DisplayVersion)) return upd.DisplayVersion;
                var basePkg = Packages.FirstOrDefault(p => p.Kind == 0);
                if (basePkg != null && !string.IsNullOrEmpty(basePkg.DisplayVersion)) return basePkg.DisplayVersion;
                return Packages.FirstOrDefault(p => !string.IsNullOrEmpty(p.DisplayVersion))?.DisplayVersion ?? "1.0.0";
            }
        }
        public byte[]? CoverBytes => Packages.FirstOrDefault(p => p.Kind == 0 && p.IconBytes != null && p.IconBytes.Length > 64)?.IconBytes ?? Packages.FirstOrDefault(p => p.IconBytes != null && p.IconBytes.Length > 64)?.IconBytes;

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

        public static byte[] SynthesizeControlNacp(string title, string publisher, string version, ulong baseTitleId)
        {
            byte[] nacp = new byte[0x4000];
            byte[] titleBytes = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(title) ? "Nintendo Switch Game" : title);
            byte[] pubBytes = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(publisher) ? "Nintendo" : publisher);

            for (int i = 0; i < 16; i++)
            {
                int offset = i * 0x300;
                Array.Copy(titleBytes, 0, nacp, offset, Math.Min(titleBytes.Length, 0x200));
                Array.Copy(pubBytes, 0, nacp, offset + 0x200, Math.Min(pubBytes.Length, 0x100));
            }

            byte[] verBytes = Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(version) ? "1.0.0" : version);
            Array.Copy(verBytes, 0, nacp, 0x3060, Math.Min(verBytes.Length, 0x10));

            ulong aocBaseId = (baseTitleId & ~0xFFFUL) + 0x1000UL;
            BinaryPrimitives.WriteUInt64LittleEndian(nacp.AsSpan(0x3070, 8), aocBaseId);
            BinaryPrimitives.WriteUInt64LittleEndian(nacp.AsSpan(0x3078, 8), baseTitleId);
            BinaryPrimitives.WriteUInt64LittleEndian(nacp.AsSpan(0x3080, 8), 0x1000000UL); // SaveDataSize
            BinaryPrimitives.WriteUInt64LittleEndian(nacp.AsSpan(0x3088, 8), 0x1000000UL); // JournalSize
            BinaryPrimitives.WriteUInt64LittleEndian(nacp.AsSpan(0x3100, 8), baseTitleId);

            return nacp;
        }

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

        private static NupInfo? ParseTocBytes(byte[] tocBytes, string path, ulong fileSize, byte[] uuid, Stream? fs = null, ulong blobOffset = 0)
        {
            if (tocBytes.Length < 0x28 || !tocBytes.AsSpan(0, 4).SequenceEqual(TocMagic))
                return null;

            uint tocPkgCount = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(0x04, 4));
            uint c08 = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(0x08, 4));
            uint c0C = tocBytes.Length >= 0x10 ? BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(0x0C, 4)) : 0;
            uint c10 = tocBytes.Length >= 0x14 ? BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(0x10, 4)) : 0;
            ulong strPoolOffset = tocBytes.Length >= 0x20 ? BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(0x18, 8)) : 0;
            ulong strPoolSize = tocBytes.Length >= 0x28 ? BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(0x20, 8)) : 0;

            bool isLegacy28 = c0C > 0 &&
                (0x28UL + (ulong)tocPkgCount * 0x40UL + (ulong)c08 * 0x18UL + (ulong)c0C * 0x50UL + (ulong)c10 * 4UL) == strPoolOffset;

            int pkgOffset;
            ulong dirCount;
            ulong fileCount;

            if (isLegacy28)
            {
                pkgOffset = 0x28;
                dirCount = c08;
                fileCount = c0C;
            }
            else
            {
                pkgOffset = 0x30;
                dirCount = BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(0x08, 8));
                fileCount = BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(0x10, 8));
            }

            var info = new NupInfo
            {
                FilePath = path,
                FileSize = fileSize,
                DirCount = (int)Math.Min(dirCount, int.MaxValue),
                FileCount = (int)Math.Min(fileCount, int.MaxValue),
                Uuid = uuid
            };

            // Чтение пакетов
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
                if (metaOffset > 0 && metaSize > 0 && metaSize < 32 * 1024 * 1024)
                {
                    byte[]? metaData = null;
                    if (fs != null && metaOffset >= (ulong)tocBytes.Length && metaOffset + metaSize <= (ulong)fs.Length)
                    {
                        try
                        {
                            long oldPos = fs.Position;
                            fs.Seek((long)metaOffset, SeekOrigin.Begin);
                            byte[] buf = new byte[metaSize];
                            fs.ReadExactly(buf, 0, (int)metaSize);
                            fs.Seek(oldPos, SeekOrigin.Begin);
                            metaData = buf;
                        }
                        catch { }
                    }
                    else if (metaOffset + metaSize <= (ulong)tocBytes.Length)
                    {
                        metaData = tocBytes.AsSpan((int)metaOffset, (int)metaSize).ToArray();
                    }

                    if (metaData != null)
                    {
                        int metaCur = 0;
                        int metaEnd = metaData.Length;
                        while (metaCur + 8 <= metaEnd)
                        {
                            uint tag = BinaryPrimitives.ReadUInt32LittleEndian(metaData.AsSpan(metaCur, 4));
                            uint len = BinaryPrimitives.ReadUInt32LittleEndian(metaData.AsSpan(metaCur + 4, 4));
                            metaCur += 8;
                            if (metaCur + (int)len > metaEnd) break;

                            byte[] val = metaData.AsSpan(metaCur, (int)len).ToArray();
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
                                    if (val.Length > 64) pkg.IconBytes = val;
                                    break;
                                case 0x0004: // Display version
                                    pkg.DisplayVersion = Encoding.UTF8.GetString(val).TrimEnd('\0').Trim();
                                    break;
                                case 0x0005: // Title name
                                case 0x0008: // Title name (alternate tag)
                                    string name = Encoding.UTF8.GetString(val).TrimEnd('\0').Trim();
                                    if (!string.IsNullOrEmpty(name))
                                    {
                                        if (string.IsNullOrEmpty(pkg.TitleName) || tag == 0x0005)
                                            pkg.TitleName = name;
                                    }
                                    break;
                                case 0x0006: // Publisher
                                    pkg.Publisher = Encoding.UTF8.GetString(val).TrimEnd('\0').Trim();
                                    break;
                                case 0x0007: // Build ID
                                    pkg.BuildId = Convert.ToHexString(val);
                                    break;
                            }
                        }
                    }
                }

                info.Packages.Add(pkg);
            }

            // Чтение файлов для подсчета размеров и дедупликации
            int dirsTableOffset = (isLegacy28 ? 0x28 : 0x30) + (int)tocPkgCount * 0x40;
            int filesTableOffset = dirsTableOffset + (int)dirCount * 0x18;

            var distinctExtents = new HashSet<ulong>();
            ulong totalUncompressed = 0;
            ulong totalCompressed = 0;

            int curFileOffset = filesTableOffset;
            for (int i = 0; i < (int)Math.Min(fileCount, int.MaxValue) && curFileOffset + 0x50 <= tocBytes.Length; i++, curFileOffset += 0x50)
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

            // Если обложка или метаданные не были сохранены в TLV пакетов, читаем их напрямую из таблицы файлов
            if (fs != null && info.Packages.All(p => p.IconBytes == null || p.IconBytes.Length == 0))
            {
                try
                {
                    string GetString(uint off, uint len)
                    {
                        int start = (int)(strPoolOffset + off);
                        if (start < 0 || start >= tocBytes.Length) return string.Empty;
                        int actualLen = (int)len;
                        if (start + actualLen > tocBytes.Length) actualLen = tocBytes.Length - start;
                        if (actualLen <= 0) return string.Empty;
                        return Encoding.UTF8.GetString(tocBytes, start, actualLen).TrimEnd(' ');
                    }

                    int scanFileOffset = filesTableOffset;
                    for (int i = 0; i < (int)Math.Min(fileCount, int.MaxValue) && scanFileOffset + 0x50 <= tocBytes.Length; i++, scanFileOffset += 0x50)
                    {
                        var span = tocBytes.AsSpan(scanFileOffset, 0x50);
                        uint nameOff = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(0x00, 4));
                        uint nameLen = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(0x04, 4));
                        ulong extentOffset = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(0x10, 8));
                        ulong extentSize = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(0x18, 8));
                        byte codec = tocBytes[scanFileOffset + 64];

                        string fName = GetString(nameOff, nameLen);
                        bool isIcon = (fName.StartsWith("icon_", StringComparison.OrdinalIgnoreCase) && fName.EndsWith(".dat", StringComparison.OrdinalIgnoreCase)) ||
                                     fName.Equals("icon.jpg", StringComparison.OrdinalIgnoreCase) || fName.Equals("icon.png", StringComparison.OrdinalIgnoreCase) ||
                                     fName.Equals("cover.jpg", StringComparison.OrdinalIgnoreCase) || fName.Equals("cover.png", StringComparison.OrdinalIgnoreCase);

                        if (isIcon && extentSize > 64 && extentSize < 4 * 1024 * 1024)
                        {
                            ulong readOffset = extentOffset < blobOffset ? (blobOffset + extentOffset) : extentOffset;
                            fs.Seek((long)readOffset, SeekOrigin.Begin);
                            byte[] extentBytes = new byte[extentSize];
                            fs.ReadExactly(extentBytes, 0, (int)extentSize);

                            byte[] imgData;
                            if (codec == 1)
                            {
                                using var dec = new Decompressor();
                                imgData = dec.Unwrap(extentBytes).ToArray();
                            }
                            else
                            {
                                imgData = extentBytes;
                            }

                            if (imgData.Length > 64 &&
                                ((imgData[0] == 0xFF && imgData[1] == 0xD8 && imgData[2] == 0xFF) ||
                                 (imgData[0] == 0x89 && imgData[1] == 0x50 && imgData[2] == 0x4E)))
                            {
                                var targetPkg = info.Packages.FirstOrDefault(p => p.Kind == 0) ?? info.Packages.FirstOrDefault();
                                if (targetPkg != null) targetPkg.IconBytes = imgData;
                                break;
                            }
                        }
                    }
                }
                catch { }
            }

            return info;
        }

        // ===== 2. Анализ NUP контейнера =====
        public static NupInfo? InspectNupFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024);
                if (fs.Length < 0x80) return null;

                byte[] superblock = new byte[0x80];
                fs.ReadExactly(superblock, 0, 0x80);

                if (!superblock.AsSpan(0, 4).SequenceEqual(NupMagic))
                    return null;

                ulong tocOffset = BinaryPrimitives.ReadUInt64LittleEndian(superblock.AsSpan(0x08, 8));
                ulong tocSize = BinaryPrimitives.ReadUInt64LittleEndian(superblock.AsSpan(0x10, 8));
                ulong blobOffset = BinaryPrimitives.ReadUInt64LittleEndian(superblock.AsSpan(0x38, 8));
                ulong fileSize = BinaryPrimitives.ReadUInt64LittleEndian(superblock.AsSpan(0x40, 8));
                byte[] uuid = superblock.AsSpan(0x50, 16).ToArray();

                if (tocOffset + tocSize > (ulong)fs.Length)
                    return null;

                fs.Seek((long)tocOffset, SeekOrigin.Begin);
                byte[] tocBytes = new byte[tocSize];
                fs.ReadExactly(tocBytes, 0, (int)tocSize);

                return ParseTocBytes(tocBytes, path, fileSize > 0 ? fileSize : (ulong)fs.Length, uuid, fs, blobOffset);
            }
            catch
            {
                return null;
            }
        }

        public static async Task<NupInfo?> InspectNupFileAsync(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;

            try
            {
                await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024, useAsync: true);
                if (fs.Length < 0x80) return null;

                byte[] superblock = new byte[0x80];
                await fs.ReadExactlyAsync(superblock, 0, 0x80);

                if (!superblock.AsSpan(0, 4).SequenceEqual(NupMagic))
                    return null;

                ulong tocOffset = BinaryPrimitives.ReadUInt64LittleEndian(superblock.AsSpan(0x08, 8));
                ulong tocSize = BinaryPrimitives.ReadUInt64LittleEndian(superblock.AsSpan(0x10, 8));
                ulong blobOffset = BinaryPrimitives.ReadUInt64LittleEndian(superblock.AsSpan(0x38, 8));
                ulong fileSize = BinaryPrimitives.ReadUInt64LittleEndian(superblock.AsSpan(0x40, 8));
                byte[] uuid = superblock.AsSpan(0x50, 16).ToArray();

                if (tocOffset + tocSize > (ulong)fs.Length)
                    return null;

                fs.Seek((long)tocOffset, SeekOrigin.Begin);
                byte[] tocBytes = new byte[tocSize];
                await fs.ReadExactlyAsync(tocBytes, 0, (int)tocSize);

                return ParseTocBytes(tocBytes, path, fileSize > 0 ? fileSize : (ulong)fs.Length, uuid, fs, blobOffset);
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

                // Заголовок TOC (0x30 байт - канонический NUP v1.0)
                tocBw.Write(TocMagic);                    // 0x00..0x03 (4B)
                tocBw.Write((uint)packagesToBuild.Count); // 0x04..0x07 (4B)
                tocBw.Write((ulong)allDirs.Count);        // 0x08..0x0F (8B dirCount)
                tocBw.Write((ulong)allFiles.Count);       // 0x10..0x17 (8B fileCount)
                tocBw.Write((ulong)0);                    // 0x18..0x1F (8B str_pool_offset)
                tocBw.Write((ulong)0);                    // 0x20..0x27 (8B str_pool_size)
                tocBw.Write((ulong)0);                    // 0x28..0x2F (8B reserved)

                // Предварительно резервируем место под NupPackage[]
                long packagesStartPos = tocMs.Position; // 0x30
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

                    void WriteTlv(uint tag, byte[] val)
                    {
                        if (val == null || val.Length == 0) return;
                        metaBw.Write(tag);
                        metaBw.Write((uint)val.Length);
                        metaBw.Write(val);
                    }

                    if (!string.IsNullOrEmpty(pkg.TitleName))
                    {
                        byte[] tn = Encoding.UTF8.GetBytes(pkg.TitleName);
                        WriteTlv(0x0005, tn);
                        WriteTlv(0x0008, tn);
                    }
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

        // ===== 2.1 Быстрое извлечение обложки и метаданных =====
        public static bool TryExtractIconAndMetadata(
            string nupPath,
            out byte[]? iconBytes,
            out string? titleId,
            out string? titleName,
            out string? displayVersion)
        {
            iconBytes = null;
            titleId = null;
            titleName = null;
            displayVersion = null;

            if (string.IsNullOrWhiteSpace(nupPath) || !File.Exists(nupPath))
                return false;

            try
            {
                using var fs = new FileStream(nupPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 64 * 1024);
                if (fs.Length < 0x80) return false;

                byte[] sb = new byte[0x80];
                fs.ReadExactly(sb, 0, 0x80);
                if (!sb.AsSpan(0, 4).SequenceEqual(NupMagic))
                    return false;

                ulong tocOffset = BinaryPrimitives.ReadUInt64LittleEndian(sb.AsSpan(0x08, 8));
                ulong tocSize = BinaryPrimitives.ReadUInt64LittleEndian(sb.AsSpan(0x10, 8));
                if (tocOffset + 0x30 > (ulong)fs.Length) return false;

                fs.Seek((long)tocOffset, SeekOrigin.Begin);
                byte[] tocHdr = new byte[0x30];
                fs.ReadExactly(tocHdr, 0, 0x30);
                if (!tocHdr.AsSpan(0, 4).SequenceEqual(TocMagic)) return false;

                uint pkgCount = BinaryPrimitives.ReadUInt32LittleEndian(tocHdr.AsSpan(0x04, 4));
                if (pkgCount == 0 || pkgCount > 256) return false;

                // Dual detection for legacy 0x28 TOC
                uint c08 = BinaryPrimitives.ReadUInt32LittleEndian(tocHdr.AsSpan(0x08, 4));
                uint c0C = BinaryPrimitives.ReadUInt32LittleEndian(tocHdr.AsSpan(0x0C, 4));
                uint c10 = BinaryPrimitives.ReadUInt32LittleEndian(tocHdr.AsSpan(0x10, 4));
                ulong strPoolOff = BinaryPrimitives.ReadUInt64LittleEndian(tocHdr.AsSpan(0x18, 8));

                int pkgStart = (c0C > 0 && (0x28UL + (ulong)pkgCount * 0x40UL + (ulong)c08 * 0x18UL + (ulong)c0C * 0x50UL + (ulong)c10 * 4UL) == strPoolOff)
                    ? 0x28
                    : 0x30;

                fs.Seek((long)(tocOffset + (ulong)pkgStart), SeekOrigin.Begin);
                byte[] pkgBytes = new byte[pkgCount * 0x40];
                fs.ReadExactly(pkgBytes, 0, pkgBytes.Length);

                var pkgs = new List<NupPackageInfo>();
                for (int i = 0; i < pkgCount; i++)
                {
                    var span = pkgBytes.AsSpan(i * 0x40, 0x40);
                    ulong pTid = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(0x00, 8));
                    uint pVer = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(0x14, 4));
                    byte pKind = span[0x18];
                    byte pFlags = span[0x19];
                    ulong metaOff = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(0x20, 8));
                    ulong metaSz = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(0x28, 8));

                    var pkg = new NupPackageInfo
                    {
                        TitleId = pTid,
                        Version = pVer,
                        Kind = pKind,
                        Flags = pFlags
                    };

                    if (metaOff > 0 && metaSz > 0 && metaSz < 32 * 1024 * 1024 && metaOff + metaSz <= (ulong)fs.Length)
                    {
                        byte[] metaData = new byte[metaSz];
                        long savedPos = fs.Position;
                        fs.Seek((long)metaOff, SeekOrigin.Begin);
                        fs.ReadExactly(metaData, 0, (int)metaSz);
                        fs.Seek(savedPos, SeekOrigin.Begin);

                        int mCur = 0;
                        int mEnd = metaData.Length;
                        while (mCur + 8 <= mEnd)
                        {
                            uint tag = BinaryPrimitives.ReadUInt32LittleEndian(metaData.AsSpan(mCur, 4));
                            uint len = BinaryPrimitives.ReadUInt32LittleEndian(metaData.AsSpan(mCur + 4, 4));
                            mCur += 8;
                            if (mCur + (int)len > mEnd) break;

                            byte[] val = metaData.AsSpan(mCur, (int)len).ToArray();
                            mCur += (int)len;

                            switch (tag)
                            {
                                case 0x0001: pkg.NpdmBytes = val; break;
                                case 0x0002: pkg.NacpBytes = val; break;
                                case 0x0003: if (val.Length > 64) pkg.IconBytes = val; break;
                                case 0x0004: pkg.DisplayVersion = Encoding.UTF8.GetString(val).TrimEnd('\0').Trim(); break;
                                case 0x0005:
                                case 0x0008:
                                    string name = Encoding.UTF8.GetString(val).TrimEnd('\0').Trim();
                                    if (!string.IsNullOrEmpty(name))
                                    {
                                        if (string.IsNullOrEmpty(pkg.TitleName) || tag == 0x0005)
                                            pkg.TitleName = name;
                                    }
                                    break;
                                case 0x0006: pkg.Publisher = Encoding.UTF8.GetString(val).TrimEnd('\0').Trim(); break;
                                case 0x0007: pkg.BuildId = Convert.ToHexString(val); break;
                            }
                        }
                    }
                    pkgs.Add(pkg);
                }

                var basePkg = pkgs.FirstOrDefault(p => p.Kind == 0) ?? pkgs.FirstOrDefault();
                var updPkg = pkgs.FirstOrDefault(p => p.Kind == 1);

                titleId = basePkg?.TitleIdHex ?? "0000000000000000";
                titleName = basePkg != null && !string.IsNullOrEmpty(basePkg.TitleName)
                    ? basePkg.TitleName
                    : (pkgs.FirstOrDefault(p => !string.IsNullOrEmpty(p.TitleName))?.TitleName ?? Path.GetFileNameWithoutExtension(nupPath));
                displayVersion = updPkg != null && !string.IsNullOrEmpty(updPkg.DisplayVersion)
                    ? updPkg.DisplayVersion
                    : (basePkg != null && !string.IsNullOrEmpty(basePkg.DisplayVersion) ? basePkg.DisplayVersion : "1.0.0");
                iconBytes = pkgs.FirstOrDefault(p => p.Kind == 0 && p.IconBytes != null && p.IconBytes.Length > 64)?.IconBytes
                    ?? pkgs.FirstOrDefault(p => p.IconBytes != null && p.IconBytes.Length > 64)?.IconBytes;

                // Fallback to full TOC file scan only if icon was not found in TLVs
                if (iconBytes == null || iconBytes.Length <= 64)
                {
                    var fullInfo = InspectNupFile(nupPath);
                    if (fullInfo != null)
                    {
                        if (titleId == "0000000000000000" && !string.IsNullOrEmpty(fullInfo.TitleId))
                            titleId = fullInfo.TitleId;
                        if ((string.IsNullOrEmpty(titleName) || titleName == Path.GetFileNameWithoutExtension(nupPath)) && !string.IsNullOrEmpty(fullInfo.GameTitle))
                            titleName = fullInfo.GameTitle;
                        if ((string.IsNullOrEmpty(displayVersion) || displayVersion == "1.0.0") && !string.IsNullOrEmpty(fullInfo.DisplayVersion))
                            displayVersion = fullInfo.DisplayVersion;
                        if (iconBytes == null || iconBytes.Length <= 64)
                            iconBytes = fullInfo.CoverBytes;
                    }
                }

                return iconBytes != null && iconBytes.Length > 64;
            }
            catch
            {
                return false;
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

            if (tocBytes.Length < 0x28 || !tocBytes.AsSpan(0, 4).SequenceEqual(TocMagic))
                throw new InvalidDataException("Заголовок TOC поврежден или отсутствует!");

            uint packageCount = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(0x04, 4));
            uint c08 = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(0x08, 4));
            uint c0C = tocBytes.Length >= 0x10 ? BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(0x0C, 4)) : 0;
            uint c10 = tocBytes.Length >= 0x14 ? BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(0x10, 4)) : 0;
            ulong strPoolOffset = BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(0x18, 8));
            ulong strPoolSize = BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(0x20, 8));

            bool isLegacy28 = c0C > 0 &&
                (0x28UL + (ulong)packageCount * 0x40UL + (ulong)c08 * 0x18UL + (ulong)c0C * 0x50UL + (ulong)c10 * 4UL) == strPoolOffset;

            int pkgStart = isLegacy28 ? 0x28 : 0x30;
            ulong dirCount = isLegacy28 ? c08 : BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(0x08, 8));
            ulong fileCount = isLegacy28 ? c0C : BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(0x10, 8));

            string GetString(uint off, uint len)
            {
                int start = (int)(strPoolOffset + off);
                if (start >= tocBytes.Length) return string.Empty;
                int actualLen = (int)len;
                if (start + actualLen > tocBytes.Length) actualLen = tocBytes.Length - start;
                return Encoding.UTF8.GetString(tocBytes, start, actualLen).TrimEnd('\0');
            }

            // Читаем Dirs
            int dirsTableOffset = pkgStart + (int)packageCount * 0x40;
            var dirs = new List<(uint NameOff, uint NameLen, uint FirstChild, uint ChildCount)>();
            for (int i = 0; i < (int)dirCount; i++)
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
            for (int i = 0; i < (int)fileCount; i++)
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
            int childrenCount = (int)((strPoolOffset > (ulong)childrenStart) ? (strPoolOffset - (ulong)childrenStart) / 4 : 0);
            var children = new uint[childrenCount];
            for (int i = 0; i < childrenCount; i++)
            {
                children[i] = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(childrenStart + i * 4, 4));
            }

            // Читаем пакеты и распаковываем
            var packages = new List<NupPackageInfo>();
            for (int p = 0; p < packageCount; p++)
            {
                int pOff = pkgStart + p * 0x40;
                ulong titleId = BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(pOff, 8));
                uint rootDirIdx = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(pOff + 8, 4));
                uint exefsDirIdx = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(pOff + 12, 4));
                uint romfsDirIdx = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(pOff + 16, 4));
                uint version = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(pOff + 20, 4));
                byte kind = tocBytes[pOff + 24];
                byte flags = tocBytes[pOff + 25];
                ulong metaOffset = BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(pOff + 0x20, 8));
                ulong metaSize = BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(pOff + 0x28, 8));

                var pkgInfo = new NupPackageInfo
                {
                    TitleId = titleId,
                    Version = version,
                    Kind = kind,
                    Flags = flags,
                    RootDirIndex = rootDirIdx,
                    ExeFsDirIndex = exefsDirIdx,
                    RomFsDirIndex = romfsDirIdx
                };

                // Read TLV metadata
                if (metaOffset > 0 && metaSize > 0 && metaSize < 32 * 1024 * 1024)
                {
                    byte[]? metaData = null;
                    if (metaOffset >= (ulong)tocBytes.Length && metaOffset + metaSize <= (ulong)fs.Length)
                    {
                        try
                        {
                            long curPos = fs.Position;
                            fs.Seek((long)metaOffset, SeekOrigin.Begin);
                            byte[] buf = new byte[metaSize];
                            await fs.ReadExactlyAsync(buf, 0, (int)metaSize, ct);
                            fs.Seek(curPos, SeekOrigin.Begin);
                            metaData = buf;
                        }
                        catch { }
                    }
                    else if (metaOffset + metaSize <= (ulong)tocBytes.Length)
                    {
                        metaData = tocBytes.AsSpan((int)metaOffset, (int)metaSize).ToArray();
                    }

                    if (metaData != null)
                    {
                        int mCur = 0;
                        int mEnd = metaData.Length;
                        while (mCur + 8 <= mEnd)
                        {
                            uint tag = BinaryPrimitives.ReadUInt32LittleEndian(metaData.AsSpan(mCur, 4));
                            uint len = BinaryPrimitives.ReadUInt32LittleEndian(metaData.AsSpan(mCur + 4, 4));
                            mCur += 8;
                            if (mCur + (int)len > mEnd) break;
                            byte[] val = metaData.AsSpan(mCur, (int)len).ToArray();
                            mCur += (int)len;

                            switch (tag)
                            {
                                case 0x0001: pkgInfo.NpdmBytes = val; break;
                                case 0x0002: pkgInfo.NacpBytes = val; break;
                                case 0x0003: if (val.Length > 64) pkgInfo.IconBytes = val; break;
                                case 0x0004: pkgInfo.DisplayVersion = Encoding.UTF8.GetString(val).TrimEnd('\0').Trim(); break;
                                case 0x0005:
                                case 0x0008:
                                    string name = Encoding.UTF8.GetString(val).TrimEnd('\0').Trim();
                                    if (!string.IsNullOrEmpty(name))
                                    {
                                        if (string.IsNullOrEmpty(pkgInfo.TitleName) || tag == 0x0005)
                                            pkgInfo.TitleName = name;
                                    }
                                    break;
                                case 0x0006: pkgInfo.Publisher = Encoding.UTF8.GetString(val).TrimEnd('\0').Trim(); break;
                                case 0x0007: pkgInfo.BuildId = Convert.ToHexString(val); break;
                            }
                        }
                    }
                }

                packages.Add(pkgInfo);

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

                double pct = 10 + ((double)(p + 1) / packageCount * 80);
                progress?.Report((pct, $"Распакован пакет [{p + 1}/{packageCount}]: {kind switch { 0 => "Base", 1 => "Update", 2 => "DLC", _ => "Package" }}"));
            }

            // Материализация контрольной структуры (Control RomFS) для базового пакета
            var basePkg = packages.FirstOrDefault(p => p.Kind == 0) ?? packages.FirstOrDefault();
            if (basePkg != null)
            {
                string basePkgFolder = Path.Combine(outputDir, "base");
                string controlDir = Path.Combine(basePkgFolder, "control");
                Directory.CreateDirectory(controlDir);

                // 1. Иконка (tag 3) -> control/icon_AmericanEnglish.dat
                byte[]? iconData = basePkg.IconBytes ?? packages.FirstOrDefault(p => p.IconBytes != null && p.IconBytes.Length > 64)?.IconBytes;
                if (iconData != null && iconData.Length > 64)
                {
                    string iconTarget = Path.Combine(controlDir, "icon_AmericanEnglish.dat");
                    await File.WriteAllBytesAsync(iconTarget, iconData, ct);
                }

                // 2. NACP (tag 2) -> control/control.nacp или синтез NACP
                string nacpTarget = Path.Combine(controlDir, "control.nacp");
                if (basePkg.NacpBytes != null && basePkg.NacpBytes.Length >= 0x4000)
                {
                    await File.WriteAllBytesAsync(nacpTarget, basePkg.NacpBytes, ct);
                }
                else
                {
                    string finalTitle = !string.IsNullOrEmpty(basePkg.TitleName)
                        ? basePkg.TitleName
                        : (packages.FirstOrDefault(p => !string.IsNullOrEmpty(p.TitleName))?.TitleName ?? Path.GetFileNameWithoutExtension(nupPath));
                    string finalPub = !string.IsNullOrEmpty(basePkg.Publisher)
                        ? basePkg.Publisher
                        : (packages.FirstOrDefault(p => !string.IsNullOrEmpty(p.Publisher))?.Publisher ?? "Nintendo");
                    var upd = packages.FirstOrDefault(p => p.Kind == 1);
                    string finalVer = upd != null && !string.IsNullOrEmpty(upd.DisplayVersion)
                        ? upd.DisplayVersion
                        : (!string.IsNullOrEmpty(basePkg.DisplayVersion) ? basePkg.DisplayVersion : "1.0.0");

                    byte[] synthNacp = SynthesizeControlNacp(finalTitle, finalPub, finalVer, basePkg.TitleId);
                    await File.WriteAllBytesAsync(nacpTarget, synthNacp, ct);
                }

                // 3. NPDM (tag 1) -> exefs/main.npdm если отсутствует
                if (basePkg.NpdmBytes != null && basePkg.NpdmBytes.Length > 0)
                {
                    string mainNpdm = Path.Combine(basePkgFolder, "exefs", "main.npdm");
                    if (!File.Exists(mainNpdm))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(mainNpdm)!);
                        await File.WriteAllBytesAsync(mainNpdm, basePkg.NpdmBytes, ct);
                    }
                }
            }

            // NPDM для патчей
            foreach (var updPkg in packages.Where(p => p.Kind == 1 && p.NpdmBytes != null && p.NpdmBytes.Length > 0))
            {
                string updFolder = Path.Combine(outputDir, "updates", updPkg.Version.ToString());
                string updNpdm = Path.Combine(updFolder, "exefs", "main.npdm");
                if (!File.Exists(updNpdm))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(updNpdm)!);
                    await File.WriteAllBytesAsync(updNpdm, updPkg.NpdmBytes, ct);
                }
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
