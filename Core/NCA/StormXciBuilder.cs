using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;

namespace StormSwitchBox.Core.NCA
{
    public class StormXciBuilder
    {
        private const long RootHfs0Offset = 0xF000;
        private const long SecureAreaOffset = 0x10000;
        private const long PageSize = 0x200;

        public static void BuildXciFromPfs0(string nspPath, string xciPath, KeySet? keySet)
        {
            keySet ??= new KeySet();

            using var fs = new FileStream(nspPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var pfs = new PartitionFileSystem(fs.AsStorage());
            var disposables = new List<IDisposable>();

            try
            {
                bool removeDelta = StormSwitchBox.App.Settings?.Current?.RemoveDeltaNca ?? true;
                var redundantSet = StormSwitchBox.Services.SmartShrinkService.AnalyzeRedundantEntriesInFileSystem(
                    pfs, keySet, removeDelta, out _, out _);

                // Build Secure HFS0 (Trimmed XCI: without DeltaFragment, without XML/junk, without duplicated .cnmt.nca)
                var secureBuilder = new PartitionFileSystemBuilder();
                var addedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in pfs.EnumerateEntries("/", "*"))
                {
                    if (!StormSwitchBox.Services.SmartShrinkService.IsValidSwitchContainerEntry(entry.Name, allowNcz: true))
                        continue;
                    if (redundantSet.Contains(entry.Name))
                        continue;
                    if (!addedNames.Add(entry.Name))
                        continue;

                    using var fRef = new UniqueRef<IFile>();
                    using var pth = new LibHac.Fs.Path();
                    pth.Initialize(new U8Span(Encoding.UTF8.GetBytes(entry.FullPath))).ThrowIfFailure();
                    pfs.OpenFile(ref fRef.Ref, in pth, OpenMode.Read).ThrowIfFailure();

                    IFile entryFile = fRef.Release();
                    disposables.Add(entryFile);
                    IStorage entryStorage = entryFile.AsStorage();
                    secureBuilder.AddFile(entry.Name, new StorageFile(new StormSwitchBox.Services.SafeStorageWrapper(entryStorage), OpenMode.Read));
                }

                using var emptyUpdateHfs0 = new PartitionFileSystemBuilder().Build(PartitionFileSystemType.Hashed);
                using var emptyNormalHfs0 = new PartitionFileSystemBuilder().Build(PartitionFileSystemType.Hashed);
                using var secureHfs0 = secureBuilder.Build(PartitionFileSystemType.Hashed);

                emptyUpdateHfs0.GetSize(out long updateSize).ThrowIfFailure();
                emptyNormalHfs0.GetSize(out long normalSize).ThrowIfFailure();
                secureHfs0.GetSize(out long secureSize).ThrowIfFailure();

                // Build Root HFS0 header at 0xF000 with "update", "normal", and "secure" at 0x10000
                byte[] strTableRaw = Encoding.ASCII.GetBytes("update\0normal\0secure\0");
                int rawHeaderSize = 0x10 + (3 * 0x40) + strTableRaw.Length;
                int rootHfsHeaderAligned = (int)(((rawHeaderSize + (PageSize - 1)) / PageSize) * PageSize);
                int stringTableSize = rootHfsHeaderAligned - 0x10 - (3 * 0x40);

                long dataStartAbs = RootHfs0Offset + rootHfsHeaderAligned;
                long updateOffsetRel = 0;
                long normalOffsetRel = updateSize;
                long minSecureAbs = dataStartAbs + updateSize + normalSize;
                long effectiveSecureAbs = Math.Max(SecureAreaOffset, ((minSecureAbs + (PageSize - 1)) / PageSize) * PageSize);
                long secureOffsetRel = effectiveSecureAbs - dataStartAbs;

                byte[] rootHfsHeader = new byte[rootHfsHeaderAligned];
                Encoding.ASCII.GetBytes("HFS0").CopyTo(rootHfsHeader, 0x00);
                BinaryPrimitives.WriteInt32LittleEndian(rootHfsHeader.AsSpan(0x04, 4), 3);
                BinaryPrimitives.WriteInt32LittleEndian(rootHfsHeader.AsSpan(0x08, 4), stringTableSize);
                BinaryPrimitives.WriteInt32LittleEndian(rootHfsHeader.AsSpan(0x0C, 4), 0);

                WriteHfs0Entry(rootHfsHeader, 0, updateOffsetRel, updateSize, ComputeHfs0HeadHash(emptyUpdateHfs0, updateSize, out uint updHashed), updHashed, 0);
                WriteHfs0Entry(rootHfsHeader, 1, normalOffsetRel, normalSize, ComputeHfs0HeadHash(emptyNormalHfs0, normalSize, out uint normHashed), normHashed, 7);
                WriteHfs0Entry(rootHfsHeader, 2, secureOffsetRel, secureSize, ComputeHfs0HeadHash(secureHfs0, secureSize, out uint secHashed), secHashed, 14);
                strTableRaw.CopyTo(rootHfsHeader, 0x10 + (3 * 0x40));

                byte[] rootHeaderHash = SHA256.HashData(rootHfsHeader);

                long totalXciSize = effectiveSecureAbs + secureSize;
                long alignedTotalSize = ((totalXciSize + (PageSize - 1)) / PageSize) * PageSize;
                ulong validDataEndPage = (ulong)(((totalXciSize + (PageSize - 1)) / PageSize) - 1);
                byte gamecardSize = GetGamecardSizeByte(alignedTotalSize);

                string? outDir = System.IO.Path.GetDirectoryName(xciPath);
                if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                    Directory.CreateDirectory(outDir);

                using var outFs = new FileStream(xciPath, FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024 * 1024);

                byte[] header = new byte[RootHfs0Offset];
                // 0x100: magic "HEAD" (u32)
                Encoding.ASCII.GetBytes("HEAD").CopyTo(header, 0x100);
                // 0x104: secure_area_start (u32) in pages (0x200 bytes each)
                uint securePage = (uint)(effectiveSecureAbs / PageSize);
                BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x104, 4), securePage);
                // 0x108: backup_area_start (u32)
                BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x108, 4), 0xFFFFFFFFu);
                // 0x10C: kek_index (u8)
                header[0x10C] = 0x00;
                // 0x10D: GamecardSize (u8)
                header[0x10D] = gamecardSize;
                // 0x10E: header_version, 0x10F: flags
                header[0x10E] = 0x00;
                header[0x10F] = 0x00;
                // 0x110: package_id (u64)
                BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(0x110, 8), 0UL);
                // 0x118: ValidDataEndPage (u64) - index of the last 0x200-byte page
                BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(0x118, 8), validDataEndPage);
                // 0x130: hfs_offset (u64)
                BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(0x130, 8), (ulong)RootHfs0Offset);
                // 0x138: hfs_header_size (u64)
                BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(0x138, 8), (ulong)rootHfsHeaderAligned);
                // 0x140: hfs_header_hash (0x20 bytes)
                rootHeaderHash.CopyTo(header, 0x140);
                // 0x18C: normal_area_end (u32) in pages
                BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(0x18C, 4), securePage);

                outFs.Write(header, 0, header.Length);
                outFs.Write(rootHfsHeader, 0, rootHfsHeader.Length);

                byte[] buffer = new byte[4 * 1024 * 1024];
                CopyStorageToStream(emptyUpdateHfs0, updateSize, outFs, buffer);
                CopyStorageToStream(emptyNormalHfs0, normalSize, outFs, buffer);

                long padBeforeSecure = effectiveSecureAbs - outFs.Position;
                if (padBeforeSecure > 0)
                {
                    WriteZeroPadding(outFs, padBeforeSecure);
                }

                CopyStorageToStream(secureHfs0, secureSize, outFs, buffer);

                long tailPadding = alignedTotalSize - totalXciSize;
                if (tailPadding > 0)
                {
                    WriteZeroPadding(outFs, tailPadding);
                }
            }
            finally
            {
                foreach (var d in disposables)
                {
                    try { d.Dispose(); } catch { }
                }
                try { (pfs as IDisposable)?.Dispose(); } catch { }
            }
        }

        private static byte GetGamecardSizeByte(long totalXciSize)
        {
            const long GB = 1024L * 1024L * 1024L;
            if (totalXciSize <= 1 * GB) return 0xFA;
            if (totalXciSize <= 2 * GB) return 0xF8;
            if (totalXciSize <= 4 * GB) return 0xF0;
            if (totalXciSize <= 8 * GB) return 0xE0;
            if (totalXciSize <= 16 * GB) return 0xE1;
            return 0xE2;
        }

        private static byte[] ComputeHfs0HeadHash(IStorage storage, long size, out uint hashedSize)
        {
            hashedSize = (uint)Math.Min(PageSize, Math.Max(0L, size));
            if (hashedSize == 0)
                return SHA256.HashData(ReadOnlySpan<byte>.Empty);

            byte[] buf = new byte[hashedSize];
            storage.Read(0, buf).ThrowIfFailure();
            return SHA256.HashData(buf);
        }

        private static void WriteHfs0Entry(
            byte[] header,
            int index,
            long offset,
            long size,
            byte[] hash,
            uint hashedSize,
            uint nameOffset)
        {
            int entryPos = 0x10 + (index * 0x40);
            BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(entryPos + 0x00, 8), (ulong)offset);
            BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(entryPos + 0x08, 8), (ulong)size);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(entryPos + 0x10, 4), nameOffset);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(entryPos + 0x14, 4), hashedSize);
            BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(entryPos + 0x18, 8), 0UL);
            hash.CopyTo(header, entryPos + 0x20);
        }

        private static void CopyStorageToStream(IStorage storage, long size, Stream outStream, byte[] buffer)
        {
            long remaining = size;
            long offset = 0;
            while (remaining > 0)
            {
                int toRead = (int)Math.Min(remaining, buffer.Length);
                storage.Read(offset, new Span<byte>(buffer, 0, toRead)).ThrowIfFailure();
                outStream.Write(buffer, 0, toRead);
                offset += toRead;
                remaining -= toRead;
            }
        }

        private static void WriteZeroPadding(Stream outStream, long length)
        {
            byte[] zeros = new byte[(int)Math.Min(length, 64 * 1024)];
            long remaining = length;
            while (remaining > 0)
            {
                int chunk = (int)Math.Min(remaining, zeros.Length);
                outStream.Write(zeros, 0, chunk);
                remaining -= chunk;
            }
        }
    }
}
