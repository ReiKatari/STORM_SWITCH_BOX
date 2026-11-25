using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StormSwitchBox.Models;
using ZstdSharp;

namespace StormSwitchBox.Services
{
    public sealed class Z3dsHeader
    {
        public static readonly byte[] ExpectedMagic = { (byte)'Z', (byte)'3', (byte)'D', (byte)'S' };
        public const byte ExpectedVersion = 1;
        public const ushort DefaultHeaderSize = 0x20;

        public byte[] Magic { get; init; } = new byte[4];
        public byte[] UnderlyingMagic { get; init; } = new byte[4];
        public byte Version { get; init; }
        public byte Reserved { get; init; }
        public ushort HeaderSize { get; init; }
        public uint MetadataSize { get; init; }
        public ulong CompressedSize { get; init; }
        public ulong UncompressedSize { get; init; }

        public bool IsValid =>
            Magic.Length == 4 &&
            Magic[0] == (byte)'Z' && Magic[1] == (byte)'3' && Magic[2] == (byte)'D' && Magic[3] == (byte)'S' &&
            Version == ExpectedVersion &&
            HeaderSize >= DefaultHeaderSize;

        public long PayloadOffset => (long)HeaderSize + MetadataSize;

        public string UnderlyingMagicString
        {
            get
            {
                if (UnderlyingMagic.Length < 4) return string.Empty;
                return Encoding.ASCII.GetString(UnderlyingMagic).TrimEnd('\0');
            }
        }

        public string RecommendedPlainExtension => UnderlyingMagicString switch
        {
            "NCSD" => ".cci",
            "NCCH" => ".cxi",
            "CIA" => ".cia",
            "3DSX" => ".3dsx",
            _ => ".bin"
        };

        public string RecommendedCompressedExtension => UnderlyingMagicString switch
        {
            "NCSD" => ".zcci",
            "NCCH" => ".zcxi",
            "CIA" => ".zcia",
            "3DSX" => ".z3dsx",
            _ => ".z3ds"
        };

        public string UnderlyingFormatDisplay => UnderlyingMagicString switch
        {
            "NCSD" => "3DS / CCI (NCSD)",
            "NCCH" => "CXI (NCCH)",
            "CIA" => "CIA",
            "3DSX" => "3DSX Homebrew",
            _ => UnderlyingMagicString
        };

        public byte[] ToBytes()
        {
            byte[] buf = new byte[DefaultHeaderSize];
            Buffer.BlockCopy(Magic, 0, buf, 0x00, Math.Min(4, Magic.Length));
            Buffer.BlockCopy(UnderlyingMagic, 0, buf, 0x04, Math.Min(4, UnderlyingMagic.Length));
            buf[0x08] = Version;
            buf[0x09] = Reserved;
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0x0A, 2), HeaderSize);
            BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x0C, 4), MetadataSize);
            BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x10, 8), CompressedSize);
            BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x18, 8), UncompressedSize);
            return buf;
        }

        public static Z3dsHeader Parse(ReadOnlySpan<byte> buf)
        {
            if (buf.Length < DefaultHeaderSize)
                return new Z3dsHeader();

            return new Z3dsHeader
            {
                Magic = buf.Slice(0x00, 4).ToArray(),
                UnderlyingMagic = buf.Slice(0x04, 4).ToArray(),
                Version = buf[0x08],
                Reserved = buf[0x09],
                HeaderSize = BinaryPrimitives.ReadUInt16LittleEndian(buf.Slice(0x0A, 2)),
                MetadataSize = BinaryPrimitives.ReadUInt32LittleEndian(buf.Slice(0x0C, 4)),
                CompressedSize = BinaryPrimitives.ReadUInt64LittleEndian(buf.Slice(0x10, 8)),
                UncompressedSize = BinaryPrimitives.ReadUInt64LittleEndian(buf.Slice(0x18, 8))
            };
        }
    }

    public sealed class Z3dsMetadata
    {
        public const byte MetadataVersion = 1;
        private const byte TypeEnd = 0;
        private const byte TypeBinary = 1;

        public Dictionary<string, byte[]> Items { get; } = new(StringComparer.Ordinal);

        public void SetString(string key, string value)
        {
            Items[key] = Encoding.UTF8.GetBytes(value ?? string.Empty);
        }

        public void SetBytes(string key, byte[] data)
        {
            Items[key] = data ?? Array.Empty<byte>();
        }

        public string? GetString(string key)
        {
            if (!Items.TryGetValue(key, out var bytes) || bytes == null) return null;
            return Encoding.UTF8.GetString(bytes).TrimEnd('\0');
        }

        public byte[]? GetBytes(string key)
        {
            return Items.TryGetValue(key, out var bytes) ? bytes : null;
        }

        public static Z3dsMetadata Parse(ReadOnlySpan<byte> source)
        {
            var meta = new Z3dsMetadata();
            if (source.Length < 1) return meta;
            if (source[0] != MetadataVersion) return meta;

            int pos = 1;
            while (pos + 4 <= source.Length)
            {
                byte type = source[pos];
                byte nameLen = source[pos + 1];
                ushort dataLen = BinaryPrimitives.ReadUInt16LittleEndian(source.Slice(pos + 2, 2));
                pos += 4;

                if (type == TypeEnd) break;
                if (pos + nameLen + dataLen > source.Length) break;

                if (type == TypeBinary)
                {
                    string name = Encoding.UTF8.GetString(source.Slice(pos, nameLen));
                    byte[] payload = source.Slice(pos + nameLen, dataLen).ToArray();
                    meta.Items[name] = payload;
                }
                pos += nameLen + dataLen;
            }
            return meta;
        }

        public byte[] SerializePadded16()
        {
            if (Items.Count == 0) return Array.Empty<byte>();

            using var ms = new MemoryStream();
            ms.WriteByte(MetadataVersion);

            byte[] itemHdr = new byte[4];
            foreach (var kv in Items)
            {
                byte[] nameBytes = Encoding.UTF8.GetBytes(kv.Key);
                byte nameLen = (byte)Math.Min(0xFF, nameBytes.Length);
                byte[] valBytes = kv.Value ?? Array.Empty<byte>();
                ushort dataLen = (ushort)Math.Min(0xFFFF, valBytes.Length);

                itemHdr[0] = TypeBinary;
                itemHdr[1] = nameLen;
                BinaryPrimitives.WriteUInt16LittleEndian(itemHdr.AsSpan(2, 2), dataLen);
                ms.Write(itemHdr, 0, 4);
                ms.Write(nameBytes, 0, nameLen);
                ms.Write(valBytes, 0, dataLen);
            }

            // End marker (4 zero bytes)
            ms.Write(stackalloc byte[4]);

            long paddedLen = (ms.Length + 0xFL) & ~0xFL;
            while (ms.Length < paddedLen)
            {
                ms.WriteByte(0);
            }
            return ms.ToArray();
        }
    }

    public readonly struct Z3dsSeekEntry
    {
        public long CompressedOffset { get; init; }
        public uint CompressedSize { get; init; }
        public long DecompressedOffset { get; init; }
        public uint DecompressedSize { get; init; }
    }

    public sealed class Z3dsReadStream : Stream
    {
        public const uint SkippableMagic = 0x184D2A5EU;
        public const uint SeekableMagic = 0x8F92EAB1U;
        private const int MaxCachedFrames = 8;

        private readonly Stream _baseStream;
        private readonly bool _leaveOpen;
        private readonly object _ioLock = new();
        private readonly List<Z3dsSeekEntry> _entries;
        private readonly Dictionary<int, byte[]> _lruMap = new();
        private readonly LinkedList<int> _lruOrder = new();
        private long _position;
        private bool _disposed;

        public Z3dsHeader Header { get; }
        public Z3dsMetadata Metadata { get; }
        public int FrameCount => _entries.Count;
        public IReadOnlyList<Z3dsSeekEntry> SeekTable => _entries;

        public Z3dsReadStream(string filePath)
            : this(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete), leaveOpen: false)
        {
        }

        public Z3dsReadStream(Stream baseStream, bool leaveOpen = false)
        {
            _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
            _leaveOpen = leaveOpen;

            if (_baseStream.Length < Z3dsHeader.DefaultHeaderSize)
                throw new InvalidDataException("Файл слишком мал для заголовка Z3DS.");

            byte[] hdrBuf = new byte[Z3dsHeader.DefaultHeaderSize];
            _baseStream.Position = 0;
            _baseStream.ReadExactly(hdrBuf);
            Header = Z3dsHeader.Parse(hdrBuf);

            if (!Header.IsValid)
                throw new InvalidDataException("Некорректный заголовок или неподдерживаемая версия Z3DS.");

            if (Header.PayloadOffset > _baseStream.Length)
                throw new InvalidDataException("Повреждена область метаданных Z3DS.");

            if (Header.MetadataSize > 0)
            {
                int metaLen = (int)Math.Min(Header.MetadataSize, 16 * 1024 * 1024);
                byte[] metaBuf = new byte[metaLen];
                _baseStream.Position = Header.HeaderSize;
                _baseStream.ReadExactly(metaBuf);
                Metadata = Z3dsMetadata.Parse(metaBuf);
            }
            else
            {
                Metadata = new Z3dsMetadata();
            }

            _entries = ParseSeekTable(_baseStream, Header);
        }

        private static List<Z3dsSeekEntry> ParseSeekTable(Stream stream, Z3dsHeader header)
        {
            long expectedEnd = header.PayloadOffset + (long)header.CompressedSize;
            long seekEnd = (header.CompressedSize > 0 && expectedEnd <= stream.Length) ? expectedEnd : stream.Length;
            if (seekEnd < header.PayloadOffset + 17)
                throw new InvalidDataException("Отсутствует Seekable-таблица Z3DS в конце файла.");

            byte[] footer = new byte[9];
            stream.Position = seekEnd - 9;
            stream.ReadExactly(footer);

            uint frameCount = BinaryPrimitives.ReadUInt32LittleEndian(footer.AsSpan(0, 4));
            byte descriptor = footer[4];
            uint footerMagic = BinaryPrimitives.ReadUInt32LittleEndian(footer.AsSpan(5, 4));

            if (footerMagic != SeekableMagic)
                throw new InvalidDataException($"Отсутствует Seekable-сигнатура 0x{SeekableMagic:X8} в конце Z3DS.");

            bool hasChecksum = (descriptor & 0x80) != 0;
            int entrySize = hasChecksum ? 12 : 8;
            long frameBodySize = (long)frameCount * entrySize + 9;
            long skippableStart = seekEnd - 8 - frameBodySize;
            if (skippableStart < header.PayloadOffset)
                throw new InvalidDataException("Некорректное смещение таблицы Seek Table в Z3DS.");

            byte[] skippableHdr = new byte[8];
            stream.Position = skippableStart;
            stream.ReadExactly(skippableHdr);

            uint skipMagic = BinaryPrimitives.ReadUInt32LittleEndian(skippableHdr.AsSpan(0, 4));
            uint skipLen = BinaryPrimitives.ReadUInt32LittleEndian(skippableHdr.AsSpan(4, 4));
            if (skipMagic != SkippableMagic || skipLen != frameBodySize)
                throw new InvalidDataException("Повреждён skippable-кадр Seek Table в Z3DS.");

            int tableBytesLen = checked((int)((long)frameCount * entrySize));
            byte[] tableBytes = new byte[tableBytesLen];
            if (tableBytesLen > 0)
                stream.ReadExactly(tableBytes);

            var entries = new List<Z3dsSeekEntry>((int)frameCount);
            long compOff = header.PayloadOffset;
            long decompOff = 0;

            for (int i = 0; i < frameCount; i++)
            {
                int idx = i * entrySize;
                uint compSize = BinaryPrimitives.ReadUInt32LittleEndian(tableBytes.AsSpan(idx, 4));
                uint decompSize = BinaryPrimitives.ReadUInt32LittleEndian(tableBytes.AsSpan(idx + 4, 4));
                if (compOff + compSize > skippableStart)
                    throw new InvalidDataException($"Кадр Z3DS #{i} выходит за пределы сжатой области.");

                entries.Add(new Z3dsSeekEntry
                {
                    CompressedOffset = compOff,
                    CompressedSize = compSize,
                    DecompressedOffset = decompOff,
                    DecompressedSize = decompSize
                });

                compOff += compSize;
                decompOff += decompSize;
            }

            if (header.UncompressedSize > 0 && decompOff != (long)header.UncompressedSize)
                throw new InvalidDataException($"Несовпадение распакованного размера Z3DS: таблица={decompOff}, заголовок={header.UncompressedSize}.");

            return entries;
        }

        private byte[] GetDecompressedFrame(int frameIndex)
        {
            if (_lruMap.TryGetValue(frameIndex, out var cached))
            {
                _lruOrder.Remove(frameIndex);
                _lruOrder.AddLast(frameIndex);
                return cached;
            }

            var entry = _entries[frameIndex];
            byte[] compBuf = ArrayPool<byte>.Shared.Rent((int)entry.CompressedSize);
            try
            {
                _baseStream.Position = entry.CompressedOffset;
                _baseStream.ReadExactly(compBuf, 0, (int)entry.CompressedSize);

                byte[] decompBuf = new byte[entry.DecompressedSize];
                if (entry.DecompressedSize > 0)
                {
                    using var decompressor = new Decompressor();
                    int written = decompressor.Unwrap(compBuf.AsSpan(0, (int)entry.CompressedSize), decompBuf.AsSpan());
                    if (written != entry.DecompressedSize)
                        throw new InvalidDataException($"Z3DS кадр #{frameIndex}: ожидалось {entry.DecompressedSize} байт, распаковано {written}.");
                }

                if (_lruMap.Count >= MaxCachedFrames && _lruOrder.First != null)
                {
                    int oldest = _lruOrder.First.Value;
                    _lruOrder.RemoveFirst();
                    _lruMap.Remove(oldest);
                }
                _lruMap[frameIndex] = decompBuf;
                _lruOrder.AddLast(frameIndex);
                return decompBuf;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(compBuf);
            }
        }

        private int FindFrameIndex(long offset)
        {
            int lo = 0;
            int hi = _entries.Count - 1;
            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                var e = _entries[mid];
                if (offset < e.DecompressedOffset)
                {
                    hi = mid - 1;
                }
                else if (offset >= e.DecompressedOffset + e.DecompressedSize)
                {
                    lo = mid + 1;
                }
                else
                {
                    return mid;
                }
            }
            return -1;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => (long)Header.UncompressedSize;

        public override long Position
        {
            get => _position;
            set => Seek(value, SeekOrigin.Begin);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long target = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                SeekOrigin.End => Length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };
            if (target < 0)
                throw new IOException("Попытка перемещения до начала потока Z3DS.");
            _position = target;
            return _position;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateBufferArguments(buffer, offset, count);
            return Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            if (buffer.IsEmpty || _position >= Length || _entries.Count == 0)
                return 0;

            lock (_ioLock)
            {
                int totalRead = 0;
                int remaining = (int)Math.Min(buffer.Length, Length - _position);
                while (remaining > 0)
                {
                    int frameIdx = FindFrameIndex(_position);
                    if (frameIdx < 0) break;

                    var entry = _entries[frameIdx];
                    byte[] frameData = GetDecompressedFrame(frameIdx);
                    int inFrameOff = (int)(_position - entry.DecompressedOffset);
                    int copyLen = Math.Min(remaining, frameData.Length - inFrameOff);
                    if (copyLen <= 0) break;

                    frameData.AsSpan(inFrameOff, copyLen).CopyTo(buffer.Slice(totalRead, copyLen));
                    totalRead += copyLen;
                    _position += copyLen;
                    remaining -= copyLen;
                }
                return totalRead;
            }
        }

        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _disposed = true;
                _lruMap.Clear();
                _lruOrder.Clear();
                if (!_leaveOpen)
                    _baseStream.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    public static class Z3dsService
    {
        public const uint DefaultFrameSize = 256 * 1024;           // 256 KiB (NCSD, NCCH, 3DSX)
        public const uint DefaultCiaFrameSize = 32 * 1024 * 1024;  // 32 MiB (CIA)

        private static readonly HashSet<string> Z3dsExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".zcci", ".zcxi", ".zcia", ".z3dsx", ".z3ds"
        };

        public static bool IsZ3dsExtension(string? ext)
        {
            if (string.IsNullOrWhiteSpace(ext)) return false;
            string norm = ext.StartsWith('.') ? ext : "." + ext;
            return Z3dsExtensions.Contains(norm);
        }

        public static bool IsZ3dsFile(string? filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return false;
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (fs.Length < Z3dsHeader.DefaultHeaderSize) return false;
                byte[] buf = new byte[Z3dsHeader.DefaultHeaderSize];
                fs.ReadExactly(buf);
                return Z3dsHeader.Parse(buf).IsValid;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryReadHeaderAndMetadata(string filePath, out Z3dsHeader header, out Z3dsMetadata metadata, out int frameCount)
        {
            header = new Z3dsHeader();
            metadata = new Z3dsMetadata();
            frameCount = 0;
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return false;

            try
            {
                using var zstream = new Z3dsReadStream(filePath);
                header = zstream.Header;
                metadata = zstream.Metadata;
                frameCount = zstream.FrameCount;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Возвращает прозрачный поток для чтения: Z3dsReadStream для сжатых .zcci/.zcxi/.zcia/.z3dsx
        /// или обычный FileStream для несжатых файлов.
        /// </summary>
        public static Stream OpenTransparentReadStream(string filePath)
        {
            if (ThreeDsBundleService.TryOpenMainEntryTransparentStream(filePath, out var bundledStream, out _) && bundledStream != null)
            {
                return bundledStream;
            }

            var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            try
            {
                if (fs.Length >= Z3dsHeader.DefaultHeaderSize)
                {
                    Span<byte> hdr = stackalloc byte[Z3dsHeader.DefaultHeaderSize];
                    fs.Position = 0;
                    fs.ReadExactly(hdr);
                    fs.Position = 0;
                    if (Z3dsHeader.Parse(hdr).IsValid)
                    {
                        return new Z3dsReadStream(fs, leaveOpen: false);
                    }
                }
                return fs;
            }
            catch
            {
                fs.Position = 0;
                return fs;
            }
        }

        public static (byte[] UnderlyingMagic, string RecommendedZ3dsExt, uint DefaultFrameSize, bool IsEncrypted) DetectPlain3dsContainer(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("Исходный файл 3DS не найден.", filePath);

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: true);

            if (fs.Length >= 0x20)
            {
                fs.Position = 0;
                byte[] magic0 = br.ReadBytes(4);
                if (magic0.Length == 4 && magic0[0] == (byte)'3' && magic0[1] == (byte)'D' && magic0[2] == (byte)'S' && magic0[3] == (byte)'X')
                {
                    return (new[] { (byte)'3', (byte)'D', (byte)'S', (byte)'X' }, ".z3dsx", DefaultFrameSize, false);
                }
                if (magic0.Length == 4 && magic0[0] == (byte)'Z' && magic0[1] == (byte)'3' && magic0[2] == (byte)'D' && magic0[3] == (byte)'S')
                {
                    throw new InvalidOperationException("Исходный файл уже сжат в формат Z3DS.");
                }
            }

            if (fs.Length >= 0x200)
            {
                fs.Position = 0x100;
                string magic100 = Encoding.ASCII.GetString(br.ReadBytes(4));
                if (magic100 == "NCSD")
                {
                    bool enc = Nintendo3dsCryptoService.Is3dsFileEncrypted(filePath);
                    return (new[] { (byte)'N', (byte)'C', (byte)'S', (byte)'D' }, ".zcci", DefaultFrameSize, enc);
                }
                if (magic100 == "NCCH")
                {
                    bool enc = Nintendo3dsCryptoService.Is3dsFileEncrypted(filePath);
                    return (new[] { (byte)'N', (byte)'C', (byte)'C', (byte)'H' }, ".zcxi", DefaultFrameSize, enc);
                }
            }

            if (fs.Length >= 0x40)
            {
                fs.Position = 0;
                uint headerSize = br.ReadUInt32();
                br.ReadUInt16(); // type
                br.ReadUInt16(); // version
                uint certSize = br.ReadUInt32();
                uint ticketSize = br.ReadUInt32();
                uint tmdSize = br.ReadUInt32();
                br.ReadUInt32(); // metaSize
                ulong contentSize = br.ReadUInt64();

                long contentOff = Align64(headerSize) + Align64(certSize) + Align64(ticketSize) + Align64(tmdSize);
                if (headerSize >= 0x20 && headerSize <= 0x4000 && certSize > 0 && tmdSize > 0 && contentSize > 0 && contentOff < fs.Length)
                {
                    bool enc = Nintendo3dsCryptoService.Is3dsFileEncrypted(filePath);
                    return (new[] { (byte)'C', (byte)'I', (byte)'A', (byte)0 }, ".zcia", DefaultCiaFrameSize, enc);
                }
            }

            throw new InvalidDataException($"Не удалось распознать контейнер Nintendo 3DS для Z3DS-сжатия: {Path.GetFileName(filePath)}");
        }

        public static Z3dsMetadata ExtractContainerMetadata(string plainPath, byte[] underlyingMagic, uint frameSize)
        {
            var meta = new Z3dsMetadata();
            string ver = !string.IsNullOrWhiteSpace(App.AppVersionString) ? App.AppVersionString : "8.2.3";
            meta.SetString("compressor", $"STORM SWITCH BOX {ver}");
            meta.SetString("date", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            meta.SetString("maxframesize", frameSize.ToString());

            string magicStr = Encoding.ASCII.GetString(underlyingMagic).TrimEnd('\0');
            try
            {
                using var fs = new FileStream(plainPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: true);

                if (magicStr == "NCSD" && fs.Length >= 0x4200)
                {
                    fs.Position = 0x120;
                    uint part0Off = br.ReadUInt32();
                    long ncchOff = part0Off > 0 ? (long)part0Off * 0x200L : 0x4000L;
                    if (ncchOff + 0x200 <= fs.Length)
                    {
                        fs.Position = ncchOff + 0x100;
                        if (Encoding.ASCII.GetString(br.ReadBytes(4)) == "NCCH")
                        {
                            uint contentUnits = br.ReadUInt32();
                            fs.Position = ncchOff + 0x112;
                            ushort ncchVer = br.ReadUInt16();
                            fs.Position = ncchOff + 0x118;
                            ulong progId = br.ReadUInt64();
                            ulong byteSize = (ulong)contentUnits * 0x200UL;
                            meta.SetBytes("titleinfo", BuildTitleInfoBytes(progId, byteSize, ncchVer, 0));
                        }
                    }
                }
                else if (magicStr == "NCCH" && fs.Length >= 0x200)
                {
                    fs.Position = 0x100;
                    if (Encoding.ASCII.GetString(br.ReadBytes(4)) == "NCCH")
                    {
                        uint contentUnits = br.ReadUInt32();
                        fs.Position = 0x112;
                        ushort ncchVer = br.ReadUInt16();
                        fs.Position = 0x118;
                        ulong progId = br.ReadUInt64();
                        ulong byteSize = (ulong)contentUnits * 0x200UL;
                        meta.SetBytes("titleinfo", BuildTitleInfoBytes(progId, byteSize, ncchVer, 0));
                    }
                }
                else if (magicStr == "CIA" && fs.Length >= 0x40)
                {
                    fs.Position = 0;
                    uint headerSize = br.ReadUInt32();
                    br.ReadUInt16();
                    br.ReadUInt16();
                    uint certSize = br.ReadUInt32();
                    uint ticketSize = br.ReadUInt32();
                    uint tmdSize = br.ReadUInt32();
                    uint metaSize = br.ReadUInt32();
                    ulong ciaContentSize = br.ReadUInt64();

                    long tmdOff = Align64(headerSize) + Align64(certSize) + Align64(ticketSize);
                    long contentOff = tmdOff + Align64(tmdSize);

                    if (tmdOff + 0x104 <= fs.Length)
                    {
                        fs.Position = tmdOff;
                        uint sigType = BinaryPrimitives.ReadUInt32BigEndian(br.ReadBytes(4));
                        int sigSize = sigType switch
                        {
                            0x00010000 or 0x00010003 => 0x240,
                            0x00010001 or 0x00010004 => 0x140,
                            0x00010002 or 0x00010005 => 0x80,
                            _ => 0x140
                        };
                        long tmdBody = tmdOff + sigSize;
                        if (tmdBody + 0x9C4 <= fs.Length)
                        {
                            fs.Position = tmdBody + 0x4C;
                            ulong tid = BinaryPrimitives.ReadUInt64BigEndian(br.ReadBytes(8));
                            fs.Position = tmdBody + 0x54;
                            uint tType = BinaryPrimitives.ReadUInt32BigEndian(br.ReadBytes(4));
                            fs.Position = tmdBody + 0x9C;
                            ushort tVer = BinaryPrimitives.ReadUInt16BigEndian(br.ReadBytes(2));
                            ushort cCount = BinaryPrimitives.ReadUInt16BigEndian(br.ReadBytes(2));

                            ulong totalSize = 0;
                            long chunkOff = tmdBody + 0x9C4;
                            int safeCount = Math.Clamp((int)cCount, 1, 256);
                            for (int c = 0; c < safeCount && chunkOff + c * 0x30 + 16 <= contentOff; c++)
                            {
                                fs.Position = chunkOff + c * 0x30 + 8;
                                totalSize += BinaryPrimitives.ReadUInt64BigEndian(br.ReadBytes(8));
                            }
                            if (totalSize == 0) totalSize = ciaContentSize;
                            meta.SetBytes("titleinfo", BuildTitleInfoBytes(tid, totalSize, tVer, tType));
                        }
                    }

                    if (metaSize >= 0x3AC0)
                    {
                        long smdhOff = contentOff + Align64((long)ciaContentSize) + 0x400;
                        if (smdhOff + 0x36C0 <= fs.Length)
                        {
                            fs.Position = smdhOff;
                            byte[] smdh = br.ReadBytes(0x36C0);
                            if (smdh.Length == 0x36C0 && smdh[0] == (byte)'S' && smdh[1] == (byte)'M' && smdh[2] == (byte)'D' && smdh[3] == (byte)'H')
                            {
                                meta.SetBytes("smdh", smdh);
                            }
                        }
                    }
                }
            }
            catch { }

            return meta;
        }

        private static byte[] BuildTitleInfoBytes(ulong tid, ulong size, ushort version, uint type)
        {
            byte[] buf = new byte[0x18];
            BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x00, 8), tid);
            BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(0x08, 8), size);
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0x10, 2), version);
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(0x12, 2), 0);
            BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(0x14, 4), type);
            return buf;
        }

        public static async Task CompressToZ3dsAsync(
            string inputPath,
            string outputPath,
            int compressionLevel = 18,
            int maxThreads = 0,
            uint? customFrameSize = null,
            ProcessingTask? task = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
                throw new FileNotFoundException("Исходный файл для сжатия Z3DS не найден.", inputPath);

            var (underlyingMagic, _, defaultFrameSize, isEncrypted) = DetectPlain3dsContainer(inputPath);
            uint frameSize = customFrameSize.HasValue && customFrameSize.Value >= 4096 ? customFrameSize.Value : defaultFrameSize;
            int level = Math.Clamp(compressionLevel <= 0 ? (App.Settings?.Current?.CompressionLevel ?? 18) : compressionLevel, 1, 22);

            string magicStr = Encoding.ASCII.GetString(underlyingMagic).TrimEnd('\0');
            string tempWorkDir = Path.Combine(Path.GetTempPath(), $"StormZ3DS_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempWorkDir);
            TempCleanupService.RegisterActiveTempDirectory(tempWorkDir);

            string? outDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                Directory.CreateDirectory(outDir);

            string tempOut = outputPath + $".tmp_{Guid.NewGuid():N}";
            string workingInput = inputPath;

            try
            {
                if (isEncrypted)
                {
                    App.RunOnUI(() =>
                    {
                        if (task != null)
                        {
                            task.IsRunning = true;
                            task.Progress = 5;
                            task.Status = "Дешифровка 3DS перед Z3DS...";
                            task.LogDetails += $"\n🔓 [Z3DS] Образ зашифрован - выполняется автоматическая дешифровка перед сжатием в Z3DS...";
                        }
                    });
                    workingInput = await Nintendo3dsCryptoService.EnsureDecrypted3dsFileAsync(inputPath, tempWorkDir, task, ct).ConfigureAwait(false);
                    if (Nintendo3dsCryptoService.Is3dsFileEncrypted(workingInput))
                    {
                        throw new InvalidOperationException("Z3DS требует расшифрованный образ 3DS, но расшифровка не завершена (проверьте aes_keys.txt / seeddb.bin).");
                    }
                }

                if (magicStr == "NCSD")
                {
                    // Trim trailing 0xFF padding on a temp copy so we never mutate the user's source file
                    string trimmedCopy = Path.Combine(tempWorkDir, "trimmed_" + Path.GetFileName(workingInput));
                    if (!string.Equals(workingInput, trimmedCopy, StringComparison.OrdinalIgnoreCase))
                    {
                        long trimCheck = EstimateNcsdPaddingBytes(workingInput);
                        if (trimCheck > 0)
                        {
                            File.Copy(workingInput, trimmedCopy, overwrite: true);
                            workingInput = trimmedCopy;
                            SmartShrinkService.Trim3dsCardImage(workingInput, task);
                        }
                    }
                }

                Z3dsMetadata metadata = ExtractContainerMetadata(workingInput, underlyingMagic, frameSize);
                byte[] metaBytes = metadata.SerializePadded16();

                long uncompressedSize = new FileInfo(workingInput).Length;
                int frameCount = uncompressedSize == 0 ? 0 : (int)((uncompressedSize + frameSize - 1) / frameSize);
                int windowLog = (int)BitOperations.Log2(BitOperations.RoundUpToPowerOf2(frameSize));
                windowLog = Math.Clamp(windowLog, 10, 25);

                int configuredCores = maxThreads > 0 ? maxThreads : (App.Settings?.Current?.UsedCores ?? 0);
                int optimalWorkers = NszCompressionService.GetOptimalCompressionThreads(configuredCores > 0 ? configuredCores : null);
                int parallelWorkers = frameSize >= 16 * 1024 * 1024 ? Math.Clamp(optimalWorkers, 1, 4) : optimalWorkers;
                int batchSize = frameCount == 0 ? 1 : Math.Min(frameCount, frameSize >= 16 * 1024 * 1024 ? parallelWorkers : Math.Max(4, parallelWorkers * 4));
                int maxCompBound = Compressor.GetCompressBound((int)frameSize);

                App.RunOnUI(() =>
                {
                    if (task != null)
                    {
                        task.IsRunning = true;
                        task.Progress = 12;
                        task.Status = $"Сжатие в {Path.GetExtension(outputPath).TrimStart('.').ToUpperInvariant()} (Z3DS)...";
                        task.LogDetails += $"\n🗜️ [Z3DS] Формат: {magicStr} -> {Path.GetFileName(outputPath)} | Кадр: {frameSize / 1024} КиБ ({frameCount} кадр.) | Уровень Zstd: {level} | Потоков: {parallelWorkers}";
                    }
                });

                var seekEntries = new List<Z3dsSeekEntry>(frameCount);
                ulong compressedPayloadSize = 0;

                await Task.Run(() =>
                {
                    byte[][] rawBuffers = new byte[batchSize][];
                    byte[][] compBuffers = new byte[batchSize][];
                    int[] rawLens = new int[batchSize];
                    int[] compLens = new int[batchSize];

                    for (int i = 0; i < batchSize; i++)
                    {
                        rawBuffers[i] = ArrayPool<byte>.Shared.Rent((int)frameSize);
                        compBuffers[i] = ArrayPool<byte>.Shared.Rent(maxCompBound);
                    }

                    using var compressors = new ThreadLocal<Compressor>(() =>
                    {
                        var c = new Compressor(level);
                        try { c.SetParameter(ZstdSharp.Unsafe.ZSTD_cParameter.ZSTD_c_windowLog, windowLog); } catch { }
                        return c;
                    }, trackAllValues: true);

                    try
                    {
                        using var inFs = new FileStream(workingInput, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1024 * 1024);
                        using var outFs = new FileStream(tempOut, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024);

                        var placeholderHdr = new Z3dsHeader
                        {
                            Magic = (byte[])Z3dsHeader.ExpectedMagic.Clone(),
                            UnderlyingMagic = (byte[])underlyingMagic.Clone(),
                            Version = Z3dsHeader.ExpectedVersion,
                            Reserved = 0,
                            HeaderSize = Z3dsHeader.DefaultHeaderSize,
                            MetadataSize = (uint)metaBytes.Length,
                            CompressedSize = 0,
                            UncompressedSize = (ulong)uncompressedSize
                        };
                        outFs.Write(placeholderHdr.ToBytes());
                        if (metaBytes.Length > 0)
                        {
                            outFs.Write(metaBytes);
                        }

                        long processedBytes = 0;
                        long lastUiReportMs = 0;

                        for (int batchStart = 0; batchStart < frameCount; batchStart += batchSize)
                        {
                            task?.PauseEvent.Wait(ct);
                            ct.ThrowIfCancellationRequested();

                            int curCount = Math.Min(batchSize, frameCount - batchStart);
                            for (int i = 0; i < curCount; i++)
                            {
                                int toRead = (int)Math.Min(frameSize, uncompressedSize - inFs.Position);
                                rawLens[i] = toRead;
                                inFs.ReadExactly(rawBuffers[i], 0, toRead);
                            }

                            Parallel.For(0, curCount, new ParallelOptions { MaxDegreeOfParallelism = parallelWorkers, CancellationToken = ct }, i =>
                            {
                                var comp = compressors.Value!;
                                int cLen = comp.Wrap(rawBuffers[i].AsSpan(0, rawLens[i]), compBuffers[i].AsSpan(0, maxCompBound));
                                compLens[i] = cLen;
                            });

                            for (int i = 0; i < curCount; i++)
                            {
                                long cOff = outFs.Position;
                                outFs.Write(compBuffers[i], 0, compLens[i]);
                                seekEntries.Add(new Z3dsSeekEntry
                                {
                                    CompressedOffset = cOff,
                                    CompressedSize = (uint)compLens[i],
                                    DecompressedOffset = processedBytes,
                                    DecompressedSize = (uint)rawLens[i]
                                });
                                processedBytes += rawLens[i];
                                compressedPayloadSize += (ulong)compLens[i];
                            }

                            long nowMs = Environment.TickCount64;
                            if (task != null && (nowMs - lastUiReportMs >= 150 || processedBytes == uncompressedSize))
                            {
                                lastUiReportMs = nowMs;
                                double pct = 12.0 + 86.0 * processedBytes / Math.Max(1L, uncompressedSize);
                                App.RunOnUI(() =>
                                {
                                    task.Progress = Math.Min(98.0, pct);
                                    task.Status = $"Сжатие Z3DS: {pct:F0}%";
                                });
                            }
                        }

                        // Append standard Seekable Zstandard skippable Seek Table frame
                        uint seekFrameBodySize = checked((uint)(frameCount * 8 + 9));
                        byte[] seekFrame = new byte[8 + seekFrameBodySize];
                        BinaryPrimitives.WriteUInt32LittleEndian(seekFrame.AsSpan(0, 4), Z3dsReadStream.SkippableMagic);
                        BinaryPrimitives.WriteUInt32LittleEndian(seekFrame.AsSpan(4, 4), seekFrameBodySize);

                        int wPos = 8;
                        for (int i = 0; i < frameCount; i++)
                        {
                            BinaryPrimitives.WriteUInt32LittleEndian(seekFrame.AsSpan(wPos, 4), seekEntries[i].CompressedSize);
                            BinaryPrimitives.WriteUInt32LittleEndian(seekFrame.AsSpan(wPos + 4, 4), seekEntries[i].DecompressedSize);
                            wPos += 8;
                        }
                        BinaryPrimitives.WriteUInt32LittleEndian(seekFrame.AsSpan(wPos, 4), (uint)frameCount);
                        seekFrame[wPos + 4] = 0; // Checksum_Flag = 0
                        BinaryPrimitives.WriteUInt32LittleEndian(seekFrame.AsSpan(wPos + 5, 4), Z3dsReadStream.SeekableMagic);

                        outFs.Write(seekFrame);
                        compressedPayloadSize += (ulong)seekFrame.Length;

                        // Finalize 32-byte Z3DSFileHeader at offset 0
                        var finalHdr = new Z3dsHeader
                        {
                            Magic = (byte[])Z3dsHeader.ExpectedMagic.Clone(),
                            UnderlyingMagic = (byte[])underlyingMagic.Clone(),
                            Version = Z3dsHeader.ExpectedVersion,
                            Reserved = 0,
                            HeaderSize = Z3dsHeader.DefaultHeaderSize,
                            MetadataSize = (uint)metaBytes.Length,
                            CompressedSize = compressedPayloadSize,
                            UncompressedSize = (ulong)uncompressedSize
                        };
                        outFs.Position = 0;
                        outFs.Write(finalHdr.ToBytes());
                        outFs.Flush();
                    }
                    finally
                    {
                        foreach (var c in compressors.Values)
                        {
                            try { c.Dispose(); } catch { }
                        }
                        for (int i = 0; i < batchSize; i++)
                        {
                            ArrayPool<byte>.Shared.Return(rawBuffers[i]);
                            ArrayPool<byte>.Shared.Return(compBuffers[i]);
                        }
                    }
                }, ct).ConfigureAwait(false);

                File.Move(tempOut, outputPath, overwrite: true);

                long finalOutBytes = new FileInfo(outputPath).Length;
                App.RunOnUI(() =>
                {
                    if (task != null)
                    {
                        task.Progress = 100;
                        task.Status = "Успешно";
                        task.IsRunning = false;
                        task.TargetSize = ProcessingTask.FormatSize(finalOutBytes);
                        long saved = Math.Max(0L, uncompressedSize - finalOutBytes);
                        double ratio = uncompressedSize > 0 ? (double)finalOutBytes * 100.0 / uncompressedSize : 100.0;
                        task.SizeDifference = saved > 0 ? $"-{ProcessingTask.FormatSize(saved)}" : "0 Б";
                        task.LogDetails += $"\n✅ [Z3DS Готово] {Path.GetFileName(outputPath)} ({ProcessingTask.FormatSize(finalOutBytes)}, {ratio:F1}% от {ProcessingTask.FormatSize(uncompressedSize)})";
                    }
                });
            }
            catch
            {
                try { if (File.Exists(tempOut)) File.Delete(tempOut); } catch { }
                throw;
            }
            finally
            {
                TempCleanupService.ForceDeleteDirectory(tempWorkDir);
            }
        }

        public static async Task DecompressZ3dsAsync(
            string inputPath,
            string outputPath,
            ProcessingTask? task = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(inputPath) || !File.Exists(inputPath))
                throw new FileNotFoundException("Исходный файл Z3DS не найден.", inputPath);

            string? outDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                Directory.CreateDirectory(outDir);

            string tempOut = outputPath + $".tmp_{Guid.NewGuid():N}";
            long totalUncompressed = 0;

            try
            {
                await Task.Run(() =>
                {
                    using var zstream = new Z3dsReadStream(inputPath);
                    totalUncompressed = (long)zstream.Header.UncompressedSize;

                    App.RunOnUI(() =>
                    {
                        if (task != null)
                        {
                            task.IsRunning = true;
                            task.Progress = 5;
                            task.Status = "Распаковка Z3DS...";
                            task.LogDetails += $"\n📦 [Z3DS Decompress] {Path.GetFileName(inputPath)} ({zstream.Header.UnderlyingFormatDisplay}) -> {Path.GetFileName(outputPath)}";
                        }
                    });

                    using var inFs = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1024 * 1024);
                    using var outFs = new FileStream(tempOut, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024);
                    using var decompressor = new Decompressor();

                    long writtenTotal = 0;
                    long lastUiReportMs = 0;

                    for (int i = 0; i < zstream.SeekTable.Count; i++)
                    {
                        task?.PauseEvent.Wait(ct);
                        ct.ThrowIfCancellationRequested();

                        var entry = zstream.SeekTable[i];
                        byte[] compBuf = ArrayPool<byte>.Shared.Rent((int)entry.CompressedSize);
                        byte[] decompBuf = ArrayPool<byte>.Shared.Rent((int)entry.DecompressedSize);
                        try
                        {
                            inFs.Position = entry.CompressedOffset;
                            inFs.ReadExactly(compBuf, 0, (int)entry.CompressedSize);
                            if (entry.DecompressedSize > 0)
                            {
                                int n = decompressor.Unwrap(compBuf.AsSpan(0, (int)entry.CompressedSize), decompBuf.AsSpan(0, (int)entry.DecompressedSize));
                                if (n != entry.DecompressedSize)
                                    throw new InvalidDataException($"Ошибка распаковки Z3DS на кадре #{i}: получено {n} вместо {entry.DecompressedSize}.");
                                outFs.Write(decompBuf, 0, n);
                                writtenTotal += n;
                            }
                        }
                        finally
                        {
                            ArrayPool<byte>.Shared.Return(compBuf);
                            ArrayPool<byte>.Shared.Return(decompBuf);
                        }

                        long nowMs = Environment.TickCount64;
                        if (task != null && (nowMs - lastUiReportMs >= 150 || i == zstream.SeekTable.Count - 1))
                        {
                            lastUiReportMs = nowMs;
                            double pct = 5.0 + 93.0 * writtenTotal / Math.Max(1L, totalUncompressed);
                            App.RunOnUI(() =>
                            {
                                task.Progress = Math.Min(98.0, pct);
                                task.Status = $"Распаковка Z3DS: {pct:F0}%";
                            });
                        }
                    }

                    if (writtenTotal != totalUncompressed)
                        throw new InvalidDataException($"Итоговый размер после распаковки Z3DS не совпадает с заголовком ({writtenTotal} != {totalUncompressed}).");
                    outFs.Flush();
                }, ct).ConfigureAwait(false);

                File.Move(tempOut, outputPath, overwrite: true);

                long finalBytes = new FileInfo(outputPath).Length;
                App.RunOnUI(() =>
                {
                    if (task != null)
                    {
                        task.Progress = 100;
                        task.Status = "Успешно";
                        task.IsRunning = false;
                        task.TargetSize = ProcessingTask.FormatSize(finalBytes);
                        task.LogDetails += $"\n✅ [Z3DS Распакован] {Path.GetFileName(outputPath)} ({ProcessingTask.FormatSize(finalBytes)})";
                    }
                });
            }
            catch
            {
                try { if (File.Exists(tempOut)) File.Delete(tempOut); } catch { }
                throw;
            }
        }

        public static (bool IsValid, string Details, Z3dsHeader? Header, Z3dsMetadata? Metadata, int FrameCount) VerifyZ3dsIntegrity(string filePath)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                return VerifyZ3dsIntegrity(fs);
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка Z3DS: {ex.Message}", null, null, 0);
            }
        }

        public static (bool IsValid, string Details, Z3dsHeader? Header, Z3dsMetadata? Metadata, int FrameCount) VerifyZ3dsIntegrity(Stream stream)
        {
            try
            {
                using var zstream = new Z3dsReadStream(stream, leaveOpen: true);
                if (zstream.FrameCount > 0)
                {
                    // Decompress first and last frame to validate actual Zstd payload integrity
                    byte[] probe = new byte[Math.Min(4096, (int)zstream.SeekTable[0].DecompressedSize)];
                    zstream.Position = 0;
                    _ = zstream.Read(probe, 0, probe.Length);

                    if (zstream.FrameCount > 1)
                    {
                        var last = zstream.SeekTable[^1];
                        byte[] probeLast = new byte[Math.Min(4096, (int)last.DecompressedSize)];
                        zstream.Position = last.DecompressedOffset;
                        _ = zstream.Read(probeLast, 0, probeLast.Length);
                    }
                }

                string comp = zstream.Metadata.GetString("compressor") ?? "-";
                string details = $"Формат Z3DS v{zstream.Header.Version} ({zstream.Header.UnderlyingFormatDisplay}), " +
                                 $"кадров: {zstream.FrameCount}, сжато: {ProcessingTask.FormatSize((long)zstream.Header.CompressedSize)}, " +
                                 $"оригинал: {ProcessingTask.FormatSize((long)zstream.Header.UncompressedSize)}, компрессор: {comp}";
                return (true, details, zstream.Header, zstream.Metadata, zstream.FrameCount);
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка Z3DS: {ex.Message}", null, null, 0);
            }
        }

        private static long EstimateNcsdPaddingBytes(string filePath)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (fs.Length <= 0x4000) return 0;
                using var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: true);
                fs.Position = 0x100;
                if (Encoding.ASCII.GetString(br.ReadBytes(4)) != "NCSD") return 0;
                fs.Position = 0x120;
                long maxEnd = 0x4000;
                for (int i = 0; i < 8; i++)
                {
                    uint off = br.ReadUInt32();
                    uint len = br.ReadUInt32();
                    if (off > 0 && len > 0)
                    {
                        long end = ((long)off + len) * 0x200L;
                        if (end > maxEnd) maxEnd = end;
                    }
                }
                maxEnd = (maxEnd + 0x1FFL) & ~0x1FFL;
                return maxEnd > 0x4000 && maxEnd < fs.Length ? (fs.Length - maxEnd) : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static long Align64(long val) => (val + 0x3FL) & ~0x3FL;
    }
}
