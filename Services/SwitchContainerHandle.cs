using System;
using System.Buffers.Binary;
using System.IO;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;

namespace StormSwitchBox.Services
{
    public sealed class SwitchContainerHandle : IDisposable
    {
        private readonly FileStream? _stream;
        private readonly PartitionFileSystem? _rootHfs0;
        private readonly IFile? _securePartitionFile;
        public IFileSystem FileSystem { get; }
        public IStorage RawStorage { get; }
        public bool IsXci { get; }

        private SwitchContainerHandle(
            FileStream? stream,
            IStorage rawStorage,
            PartitionFileSystem? rootHfs0,
            IFile? securePartitionFile,
            IFileSystem fileSystem,
            bool isXci)
        {
            _stream = stream;
            RawStorage = rawStorage;
            _rootHfs0 = rootHfs0;
            _securePartitionFile = securePartitionFile;
            FileSystem = fileSystem;
            IsXci = isXci;
        }

        public static SwitchContainerHandle Open(string containerPath)
        {
            var fs = new FileStream(containerPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            try
            {
                return OpenFromStorage(fs.AsStorage(), fs, containerPath);
            }
            catch
            {
                fs.Dispose();
                throw;
            }
        }

        public static SwitchContainerHandle OpenFromStorage(IStorage storage, FileStream? ownedStream = null, string? pathHint = null)
        {
            storage.GetSize(out long totalSize).ThrowIfFailure();
            long? hfs0Offset = TryDetectXciRootHfs0Offset(storage, totalSize);
            bool extLooksXci = pathHint != null &&
                (pathHint.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) ||
                 pathHint.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase));

            if (hfs0Offset.HasValue || extLooksXci)
            {
                long rootOff = hfs0Offset ?? 0xF000L;
                if (rootOff < 0 || rootOff >= totalSize)
                    throw new InvalidDataException("Invalid XCI Root HFS0 offset.");

                var subRoot = new SubStorage(storage, rootOff, totalSize - rootOff);
                var rootHfs = new PartitionFileSystem(subRoot);

                using var secureRef = new UniqueRef<IFile>();
                var openRes = rootHfs.OpenFile(ref secureRef.Ref, "/secure".ToU8Span(), OpenMode.Read);
                if (openRes.IsFailure())
                {
                    rootHfs.Dispose();
                    throw new InvalidDataException($"Failed to open /secure partition in XCI ({openRes}).");
                }

                IFile secureFile = secureRef.Release();
                try
                {
                    var securePfs = new PartitionFileSystem(secureFile.AsStorage());
                    return new SwitchContainerHandle(ownedStream, storage, rootHfs, secureFile, securePfs, isXci: true);
                }
                catch
                {
                    secureFile.Dispose();
                    rootHfs.Dispose();
                    throw;
                }
            }

            var pfs = new PartitionFileSystem(storage);
            return new SwitchContainerHandle(ownedStream, storage, null, null, pfs, isXci: false);
        }

        public static long? TryDetectXciRootHfs0Offset(IStorage storage, long totalSize)
        {
            Span<byte> probe = stackalloc byte[0x40];
            ReadOnlySpan<long> headCandidates = stackalloc long[] { 0x100L, 0x1100L };
            foreach (long headOff in headCandidates)
            {
                if (totalSize < headOff + 0x40) continue;
                if (storage.Read(headOff, probe).IsSuccess() &&
                    probe[0] == (byte)'H' && probe[1] == (byte)'E' && probe[2] == (byte)'A' && probe[3] == (byte)'D')
                {
                    ulong hfsOff = BinaryPrimitives.ReadUInt64LittleEndian(probe.Slice(0x30, 8));
                    if (hfsOff > 0 && (long)hfsOff < totalSize)
                        return (long)hfsOff;
                }
            }
            return null;
        }

        public void Dispose()
        {
            try { (FileSystem as IDisposable)?.Dispose(); } catch { }
            try { _securePartitionFile?.Dispose(); } catch { }
            try { _rootHfs0?.Dispose(); } catch { }
            try { _stream?.Dispose(); } catch { }
        }
    }
}
