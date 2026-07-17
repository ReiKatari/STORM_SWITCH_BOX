using System;
using System.IO;
using System.Linq;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;

namespace StormSwitchBox.Core.NCA
{
    public class StormXciBuilder
    {
        public static void BuildXciFromPfs0(string nspPath, string xciPath, KeySet keySet)
        {
            using var fs = new FileStream(nspPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            IFileSystem pfs = new PartitionFileSystem(fs.AsStorage());

            // Build Secure HFS0
            var secureBuilder = new PartitionFileSystemBuilder();
            foreach (var entry in pfs.EnumerateEntries("/", "*"))
            {
                using var fRef = new UniqueRef<IFile>();
                using var pth = new LibHac.Fs.Path();
                pth.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(entry.FullPath))).ThrowIfFailure();
                pfs.OpenFile(ref fRef.Ref, in pth, OpenMode.Read).ThrowIfFailure();
                
                IStorage entryStorage = fRef.Release().AsStorage();
                secureBuilder.AddFile(entry.Name, new StorageFile(new StormSwitchBox.Services.SafeStorageWrapper(entryStorage), OpenMode.Read));
            }

            using var secureHfs0 = secureBuilder.Build(PartitionFileSystemType.Hashed);

            // Build Root HFS0
            var rootBuilder = new PartitionFileSystemBuilder();
            rootBuilder.AddFile("secure", new StorageFile(new StormSwitchBox.Services.SafeStorageWrapper(secureHfs0), OpenMode.Read));
            
            using var rootHfs0 = rootBuilder.Build(PartitionFileSystemType.Hashed);
            rootHfs0.GetSize(out long rootSize).ThrowIfFailure();

            long rootOffset = 0x10000; // Standard XCI root partition offset
            
            // Compute actual HFS0 header size for hash: HFS0 header = 0x10 + 0x40 * numFiles + stringTableSize
            // Read the HFS0 magic + file count first to determine header size
            byte[] hfs0HeaderPrefix = new byte[0x10]; // HFS0 base header
            rootHfs0.Read(0, hfs0HeaderPrefix).ThrowIfFailure();
            int hfs0EntryCount = BitConverter.ToInt32(hfs0HeaderPrefix, 4);
            int hfs0StringTableSize = BitConverter.ToInt32(hfs0HeaderPrefix, 8);
            int hfs0HeaderSize = 0x10 + (0x40 * hfs0EntryCount) + hfs0StringTableSize;
            // Align header size to 0x200
            int hfs0HeaderAligned = (hfs0HeaderSize + 0x1FF) & ~0x1FF;

            byte[] rootHeaderHash;
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] rootHeader = new byte[hfs0HeaderAligned];
                rootHfs0.Read(0, rootHeader).ThrowIfFailure();
                rootHeaderHash = sha.ComputeHash(rootHeader);
            }

            // Write XCI
            using var outFs = new FileStream(xciPath, FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024 * 1024);
            
            byte[] header = new byte[rootOffset];
            
            // GamecardHeader structure (total 0x200 bytes, starting at offset 0x000):
            // 0x000: signature (0x100 bytes) - leave zeros for unsigned
            // 0x100: magic "HEAD" (u32)
            System.Text.Encoding.ASCII.GetBytes("HEAD").CopyTo(header, 0x100);
            // 0x104: secure_area_start (u32) - in pages (0x200 bytes each)
            long securePage = rootOffset / 0x200;
            Array.Copy(BitConverter.GetBytes((uint)securePage), 0, header, 0x104, 4);
            // 0x108: backup_area_start (u32) - set to max
            Array.Copy(BitConverter.GetBytes(0xFFFFFFFFu), 0, header, 0x108, 4);
            // 0x10C: kek_index (u8) - 0 = standard
            header[0x10C] = 0x00;
            // 0x10D: GamecardSize (u8) - 0xFA = 8GB (safe default)
            header[0x10D] = 0xFA;
            // 0x10E: header_version (u8)
            header[0x10E] = 0x00;
            // 0x10F: flags (u8)
            header[0x10F] = 0x00;
            // 0x110: package_id (u64)
            Array.Copy(BitConverter.GetBytes((ulong)0), 0, header, 0x110, 8);
            // 0x118: valid_data_end (u64) - total XCI size in 0x200-byte pages
            long totalXciSize = rootOffset + rootSize;
            long validDataEnd = (totalXciSize + 0x1FF) / 0x200;
            Array.Copy(BitConverter.GetBytes((ulong)validDataEnd), 0, header, 0x118, 8);
            // 0x120: info_iv (u128 = 16 bytes) - leave zeros
            // 0x130: hfs_offset (u64) - root HFS0 offset
            Array.Copy(BitConverter.GetBytes((ulong)rootOffset), 0, header, 0x130, 8);
            // 0x138: hfs_size (u64) - root HFS0 size
            Array.Copy(BitConverter.GetBytes((ulong)rootSize), 0, header, 0x138, 8);
            // 0x140: hfs_header_hash (0x20 bytes) - SHA256 of root HFS0 header
            Array.Copy(rootHeaderHash, 0, header, 0x140, 32);
            // 0x160: initial_data_hash (0x20 bytes) - leave zeros
            // 0x180: secure_mode_flag (u32)
            // 0x184: title_key_flag (u32)
            // 0x188: key_flag (u32)
            // 0x18C: normal_area_end (u32) - in pages
            long normalEnd = rootOffset / 0x200;
            Array.Copy(BitConverter.GetBytes((uint)normalEnd), 0, header, 0x18C, 4);

            outFs.Write(header, 0, (int)rootOffset);

            // Write Root HFS0
            byte[] buffer = new byte[4 * 1024 * 1024];
            long remaining = rootSize;
            long offset = 0;
            while (remaining > 0)
            {
                int toRead = (int)Math.Min(remaining, buffer.Length);
                rootHfs0.Read(offset, new Span<byte>(buffer, 0, toRead)).ThrowIfFailure();
                outFs.Write(buffer, 0, toRead);
                offset += toRead;
                remaining -= toRead;
            }
        }
    }
}
