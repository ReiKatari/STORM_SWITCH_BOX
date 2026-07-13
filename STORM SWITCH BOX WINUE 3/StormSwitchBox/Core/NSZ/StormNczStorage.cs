using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using LibHac;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;
using LibHac.Common.Keys;
using ZstdSharp;

namespace StormSwitchBox.Core.NSZ
{
    public class LruBlockCache
    {
        private readonly int _capacity;
        private readonly Dictionary<int, LinkedListNode<(int Index, byte[] Data)>> _cacheMap;
        private readonly LinkedList<(int Index, byte[] Data)> _lruList;
        private readonly object _lock = new object();

        public LruBlockCache(int capacity)
        {
            _capacity = capacity;
            _cacheMap = new Dictionary<int, LinkedListNode<(int, byte[])>>(capacity);
            _lruList = new LinkedList<(int, byte[])>();
        }

        public byte[]? Get(int index)
        {
            lock (_lock)
            {
                if (_cacheMap.TryGetValue(index, out var node))
                {
                    _lruList.Remove(node);
                    _lruList.AddFirst(node);
                    return node.Value.Data;
                }
                return null;
            }
        }

        public void Put(int index, byte[] data)
        {
            lock (_lock)
            {
                if (_cacheMap.TryGetValue(index, out var node))
                {
                    node.Value = (index, data);
                    _lruList.Remove(node);
                    _lruList.AddFirst(node);
                }
                else
                {
                    if (_cacheMap.Count >= _capacity)
                    {
                        var lastNode = _lruList.Last;
                        if (lastNode != null)
                        {
                            _cacheMap.Remove(lastNode.Value.Index);
                            _lruList.RemoveLast();
                        }
                    }
                    var newNode = new LinkedListNode<(int, byte[])>((index, data));
                    _lruList.AddFirst(newNode);
                    _cacheMap.Add(index, newNode);
                }
            }
        }
    }
    public class NczSection
    {
        public long Offset { get; set; }
        public long Size { get; set; }
        public long CryptoType { get; set; }
        public long OriginalCryptoType { get; set; }
        public byte[] CryptoKey { get; set; } = Array.Empty<byte>();
        public byte[]? ExpectedKey { get; set; }
        public byte[] CryptoCounter { get; set; } = Array.Empty<byte>();
        public long SolidStreamOffset { get; set; }
        public long SkipBytes { get; set; }
        public long PhysicalOffset { get; set; }
        public long PhysicalSize { get; set; }
        public bool IsSparse { get; set; }
        public long SparsePhysicalOffset { get; set; }
        public long SparseBucketStart { get; set; }
        public long SparseBucketEnd { get; set; }
        public ulong SparseUpperIv { get; set; }
        public int FsIndex { get; set; }
    }

    public class NczBlockHeader
    {
        public byte Version { get; set; }
        public byte Type { get; set; }
        public byte Unused { get; set; }
        public byte BlockSizeExponent { get; set; }
        public int NumberOfBlocks { get; set; }
        public long DecompressedSize { get; set; }
        public List<int> CompressedBlockSizeList { get; set; } = new List<int>();
        public List<long> CompressedBlockOffsetList { get; set; } = new List<long>();
    }

    /// <summary>
    /// Ядро нативного парсинга форматов NCZ (Nintendo Compressed Archive).
    /// Этот класс будет отвечать за чтение заголовков NCZSECTN, таблиц блоков
    /// и инициализацию потоковой декомпрессии Zstandard "на лету".
    /// </summary>
    public class StormNczStorage : IStorage
    {
        private readonly IStorage _baseStorage;
        private long _uncompressedSize;
        private bool _isSolid;

        public long PhysicalSize { get; private set; }
        public bool IsSolid => _isSolid;
        public long UncompressedSize => _uncompressedSize;

        private List<NczSection> _sections = new List<NczSection>();
        private NczBlockHeader? _blockHeader;
        private long _zstdStreamOffset;
        
        // Для Solid архивов
        private string? _tempSolidFile;
        private FileStream? _tempSolidStream;


        // Архитектура многопоточного чтения и кэширования
        private readonly LruBlockCache _blockCache = new LruBlockCache(256); // кэш на 256 блоков (обычно ~4MB)
        private readonly object _baseStorageLock = new object(); // защита потокобезопасности при чтении из базового стораджа

        public Stream? TempSolidStream => _tempSolidStream;

        private readonly IStorage? _solidStorage;
        private readonly Dictionary<string, byte[]>? _titleKeys;
        private readonly KeySet? _keyset;
        private bool _ncaHeaderInSolidStream = false;

        public StormNczStorage(IStorage baseStorage, Dictionary<string, byte[]>? titleKeys = null, IStorage? solidStorage = null, KeySet? keyset = null)
        {
            _baseStorage = baseStorage;
            _titleKeys = titleKeys;
            _solidStorage = solidStorage;
            _keyset = keyset ?? App.Keys?.CurrentKeyset;
            Initialize();
        }

        ~StormNczStorage()
        {
            try
            {
                if (_tempSolidStream != null)
                {
                    _tempSolidStream.Dispose();
                    _tempSolidStream = null;
                }
                if (_tempSolidFile != null && System.IO.File.Exists(_tempSolidFile))
                {
                    System.IO.File.Delete(_tempSolidFile);
                }
            }
            catch { }
        }


        private void Initialize()
        {
            long ncaHeaderSize = 0x4000;
            long currentOffset = 0;
            
            byte[] magicBytes = new byte[8];
            _baseStorage.GetSize(out long baseSize).ThrowIfFailure();
            if (baseSize < 8) throw new InvalidDataException($"File is too small to contain NCZ magic! Size: {baseSize}");

            _baseStorage.Read(currentOffset, magicBytes).ThrowIfFailure();
            string magic = Encoding.ASCII.GetString(magicBytes);
            
            if (magic != "NCZSECTN" && magic != "NCZBLOCK")
            {
                currentOffset = 0x4000;
                if (baseSize >= currentOffset + 8)
                {
                    _baseStorage.Read(currentOffset, magicBytes).ThrowIfFailure();
                    magic = Encoding.ASCII.GetString(magicBytes);
                }
                
                if (magic != "NCZSECTN" && magic != "NCZBLOCK")
                {
                    throw new InvalidDataException($"No NCZ magic found! Magic at 0x4000: {magic} (BaseSize: {baseSize})");
                }
            }

            long calculatedNcaSize = ncaHeaderSize;
            long currentSolidOffset = 0;
            long skipBytes0 = 0, skipBytes1 = 0, skipBytes2 = 0, skipBytes3 = 0;
            long startOffset0 = -1, startOffset1 = -1, startOffset2 = -1, startOffset3 = -1;
            bool[] isSparseArr = new bool[4];
            long[] sparsePhysOffsetArr = new long[4];
            long[] sparseBucketStartArr = new long[4];
            long[] sparseBucketEndArr = new long[4];
            ulong[] sparseUpperIvArr = new ulong[4];
            bool isHeaderUncompressed = (currentOffset == 0x4000);
            byte[] hashTypeArr = new byte[4];
            byte[] encTypeArr = new byte[4];

            Console.WriteLine($"[StormNczStorage Debug] Initialize: isHeaderUncompressed={isHeaderUncompressed}, keysetIsNull={_keyset == null}");

            string? ncaRightsIdHex = null;
            string? ncaUniqueId = null;
            byte[]? decryptedHeader = null;
            if (isHeaderUncompressed && _keyset != null)
            {
                try
                {
                    byte[] headerEncrypted = new byte[0xC00];
                    _baseStorage.Read(0, headerEncrypted).ThrowIfFailure();
                    
                    decryptedHeader = new byte[0xC00];
                    byte[] headerKey = new byte[32];
                    ((ReadOnlySpan<byte>)_keyset.HeaderKey).CopyTo(headerKey);
                    
                    if (headerEncrypted[0x200] == 'N' && headerEncrypted[0x201] == 'C' && headerEncrypted[0x202] == 'A' && headerEncrypted[0x203] == '3')
                    {
                        Array.Copy(headerEncrypted, decryptedHeader, 0xC00);
                    }
                    else
                    {
                        byte[] key1 = new byte[16];
                        byte[] key2 = new byte[16];
                        Array.Copy(headerKey, 0, key1, 0, 16);
                        Array.Copy(headerKey, 16, key2, 0, 16);
                        Array.Copy(headerEncrypted, 0, decryptedHeader, 0, 0x200);
                        var transform = new LibHac.Tools.FsSystem.Aes128XtsTransform(key1, key2, true);
                        int encryptedLength = 0xC00 - 0x200;
                        Array.Copy(headerEncrypted, 0x200, decryptedHeader, 0x200, encryptedLength);
                        for (int idx = 0; idx < encryptedLength / 0x200; idx++)
                        {
                            transform.TransformBlock(decryptedHeader, 0x200 + idx * 0x200, 0x200, (ulong)(1 + idx));
                        }
                    }

                    byte[] rightsId = new byte[16];
                    string magicStr = System.Text.Encoding.ASCII.GetString(decryptedHeader, 0x200, 4);
                    Console.WriteLine($"[StormNczStorage Debug] Decrypted Header Magic: '{magicStr}' (Hex: {BitConverter.ToString(decryptedHeader, 0x200, 4)})");
                    Array.Copy(decryptedHeader, 0x230, rightsId, 0, 16);
                    bool hasRightsId = false;
                    for (int j = 0; j < 16; j++) if (rightsId[j] != 0) { hasRightsId = true; break; }
                    if (hasRightsId)
                    {
                        ncaRightsIdHex = BitConverter.ToString(rightsId).Replace("-", "").ToLowerInvariant();
                        Console.WriteLine($"[StormNczStorage Debug] Found RightsId in NCA header: {ncaRightsIdHex}");
                    }
                    
                    if (ncaRightsIdHex != null)
                    {
                        ncaUniqueId = ncaRightsIdHex;
                    }
                    else
                    {
                        using (var sha = System.Security.Cryptography.SHA256.Create())
                        {
                            byte[] hash = sha.ComputeHash(decryptedHeader, 0, 0x200);
                            ncaUniqueId = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                        }
                    }
                    
                    for (int idx = 0; idx < 4; idx++)
                    {
                        uint startOffsetUnits = BitConverter.ToUInt32(decryptedHeader, 0x240 + idx * 16);
                        uint endOffsetUnits = BitConverter.ToUInt32(decryptedHeader, 0x240 + idx * 16 + 4);
                        if (startOffsetUnits != endOffsetUnits)
                        {
                            byte[] fsHeader = new byte[0x200];
                            Array.Copy(decryptedHeader, 0x400 + idx * 0x200, fsHeader, 0, 0x200);
                            
                            long skip = 0;
                            byte hashType = fsHeader[0x03];
                            byte encType = fsHeader[0x04];
                            
                            hashTypeArr[idx] = hashType;
                            encTypeArr[idx] = encType;
                            if (encType == 5 || encType == 6)
                            {
                                if (hashType == 2 || hashType == 5)
                                {
                                    int layerCount = BitConverter.ToInt32(fsHeader, 0x2C);
                                    if (layerCount > 0 && layerCount <= 5)
                                    {
                                        skip = BitConverter.ToInt64(fsHeader, 0x30 + (layerCount - 1) * 16);
                                    }
                                }
                                else if (hashType == 3 || hashType == 6)
                                {
                                    int maxLayers = BitConverter.ToInt32(fsHeader, 0x14);
                                    if (maxLayers >= 2 && maxLayers <= 7)
                                    {
                                        skip = BitConverter.ToInt64(fsHeader, 0x18 + (maxLayers - 2) * 24);
                                    }
                                }
                            }
                            
                            long currentStartOffset = (long)startOffsetUnits * 0x200;
                            Console.WriteLine($"[StormNczStorage Debug] Header Section {idx}: startOffsetUnits={startOffsetUnits}, startOffset={currentStartOffset}, endOffsetUnits={endOffsetUnits}");

                            if (idx == 0) { skipBytes0 = skip; startOffset0 = currentStartOffset; }
                            else if (idx == 1) { skipBytes1 = skip; startOffset1 = currentStartOffset; }
                            else if (idx == 2) { skipBytes2 = skip; startOffset2 = currentStartOffset; }
                            else if (idx == 3) { skipBytes3 = skip; startOffset3 = currentStartOffset; }

                            ulong baseUpperIv = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(new ReadOnlySpan<byte>(fsHeader, 0x140, 8));
                            ushort sparseGen = BitConverter.ToUInt16(fsHeader, 0x170);
                            bool isSparse = (sparseGen != 0);
                            isSparseArr[idx] = isSparse;
                            if (isSparse)
                            {
                                long sparsePhysicalOffset = BitConverter.ToInt64(fsHeader, 0x168);
                                long bucketOffset = BitConverter.ToInt64(fsHeader, 0x148);
                                long bucketSize = BitConverter.ToInt64(fsHeader, 0x150);
                                
                                sparsePhysOffsetArr[idx] = sparsePhysicalOffset;
                                sparseBucketStartArr[idx] = sparsePhysicalOffset + bucketOffset;
                                sparseBucketEndArr[idx] = sparseBucketStartArr[idx] + bucketSize;
                                
                                uint secureValue = (uint)(baseUpperIv >> 32);
                                sparseUpperIvArr[idx] = ((ulong)secureValue << 32) | ((ulong)sparseGen << 16);
                            }
                        }
                    }
                }
                catch { }
            }

            if (magic == "NCZSECTN")
            {
                if (currentOffset == 0)
                {
                    _ncaHeaderInSolidStream = true;
                    currentSolidOffset = 0x4000;
                }
                currentOffset += 8;
                
                byte[] countBytes = new byte[8];
                _baseStorage.Read(currentOffset, countBytes);
                long sectionCount = BitConverter.ToInt64(countBytes, 0);
                currentOffset += 8;
                
                _baseStorage.GetSize(out long baseSizeLimit).ThrowIfFailure();
                long currentPhysicalOffset = 0x4000;
                for (int i = 0; i < sectionCount; i++)
                {
                    if (currentOffset + 64 > baseSizeLimit) break;
                    byte[] sectionData = new byte[64];
                    _baseStorage.Read(currentOffset, sectionData);
                    Console.WriteLine($"[StormNczStorage Debug] Raw sectionData for i={i}: {BitConverter.ToString(sectionData).Replace("-","").ToLowerInvariant()}");
                    
                    NczSection section = new NczSection
                    {
                        Offset = BitConverter.ToInt64(sectionData, 0),
                        Size = BitConverter.ToInt64(sectionData, 8),
                        CryptoType = BitConverter.ToInt64(sectionData, 16),
                        OriginalCryptoType = BitConverter.ToInt64(sectionData, 16),
                        CryptoKey = new byte[16],
                        CryptoCounter = new byte[16],
                        SolidStreamOffset = currentSolidOffset
                    };
                    
                    Array.Copy(sectionData, 32, section.CryptoKey, 0, 16);
                    Array.Copy(sectionData, 48, section.CryptoCounter, 0, 16);
                    

                    
                    long skip = 0;
                    int targetIdx = -1;
                    if (section.Offset == startOffset0) { skip = skipBytes0; targetIdx = 0; }
                    else if (section.Offset == startOffset1) { skip = skipBytes1; targetIdx = 1; }
                    else if (section.Offset == startOffset2) { skip = skipBytes2; targetIdx = 2; }
                    else if (section.Offset == startOffset3) { skip = skipBytes3; targetIdx = 3; }

                    byte[]? expectedKey = null;
                    // TitleKey detection: use ncaRightsIdHex as primary signal
                    // CryptoType 2 = old Storm TitleKey format, 3/4 = standard NSZ CTR/BKTR
                    bool usesTitleKey = (ncaRightsIdHex != null) || (section.CryptoType == 2);
                    
                    if (usesTitleKey)
                    {
                        if (ncaRightsIdHex != null)
                        {
                            if (_titleKeys != null && _titleKeys.TryGetValue(ncaRightsIdHex, out expectedKey))
                            {
                                // Found in passed titleKeys
                            }
                            else
                            {
                                expectedKey = GetTitleKeyFromKeysetHex(ncaRightsIdHex);
                            }

                            if (expectedKey == null && _titleKeys != null && _titleKeys.Count > 0)
                            {
                                expectedKey = _titleKeys.Values.First();
                            }
                        }
                        else
                        {
                            if (_titleKeys != null && _titleKeys.Count > 0)
                            {
                                expectedKey = _titleKeys.Values.First();
                            }
                        }
                    }
                    else if ((section.CryptoType == 2 || section.CryptoType == 3 || section.CryptoType == 4) && _keyset != null)
                    {
                        try
                        {
                            byte[] headerEncrypted = new byte[0xC00];
                            _baseStorage.Read(0, headerEncrypted).ThrowIfFailure();
                            
                            decryptedHeader = new byte[0xC00];
                            byte[] headerKey = new byte[32];
                            ((ReadOnlySpan<byte>)_keyset.HeaderKey).CopyTo(headerKey);
                            
                            if (headerEncrypted[0x200] == 'N' && headerEncrypted[0x201] == 'C' && headerEncrypted[0x202] == 'A' && headerEncrypted[0x203] == '3')
                            {
                                Array.Copy(headerEncrypted, decryptedHeader, 0xC00);
                            }
                            else
                            {
                                byte[] key1 = new byte[16];
                                byte[] key2 = new byte[16];
                                Array.Copy(headerKey, 0, key1, 0, 16);
                                Array.Copy(headerKey, 16, key2, 0, 16);
                                Array.Copy(headerEncrypted, 0, decryptedHeader, 0, 0x200);
                                var transform = new LibHac.Tools.FsSystem.Aes128XtsTransform(key1, key2, true);
                                int encryptedLength = 0xC00 - 0x200;
                                Array.Copy(headerEncrypted, 0x200, decryptedHeader, 0x200, encryptedLength);
                                for (int idx = 0; idx < encryptedLength / 0x200; idx++)
                                {
                                    transform.TransformBlock(decryptedHeader, 0x200 + idx * 0x200, 0x200, (ulong)(1 + idx));
                                }
                            }

                            var nca = new LibHac.Tools.FsSystem.NcaUtils.Nca(_keyset, new MemoryStream(headerEncrypted).AsStorage());
                            
                            int keysetIndex = Math.Max(0, nca.Header.KeyGeneration - 1);
                            int keyAreaKeyIndex = nca.Header.KeyAreaKeyIndex;
                            int keyOffset = 0x320;
                            byte[] areaKey = new byte[16];
                            ((ReadOnlySpan<byte>)_keyset.KeyAreaKeys[keysetIndex][keyAreaKeyIndex]).CopyTo(areaKey);
                            
                            byte[] encryptedKey = new byte[16];
                            Array.Copy(decryptedHeader, keyOffset, encryptedKey, 0, 16);
                            expectedKey = AesEcbDecrypt(encryptedKey, areaKey);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[StormNczStorage Debug] Error manually decrypting Key Area Key for Section offset {section.Offset}: {ex.Message}");
                        }
                    }

                    if (expectedKey != null)
                    {
                        Array.Copy(expectedKey, section.CryptoKey, 16);
                    }
                    section.ExpectedKey = section.CryptoKey;
                    Console.WriteLine($"[StormNczStorage Debug] Section {section.Offset}: CompressorKey/ExpectedKey={BitConverter.ToString(section.CryptoKey).Replace("-","").ToLowerInvariant()}");
                    
                    section.SkipBytes = skip;
                    section.FsIndex = targetIdx != -1 ? targetIdx : i;
                    Console.WriteLine($"[StormNczStorage Debug] Match section: section.Offset={section.Offset}, startOffset0={startOffset0}, startOffset1={startOffset1}, startOffset2={startOffset2}, startOffset3={startOffset3}, matched targetIdx={targetIdx}");
                    
                    if (targetIdx != -1)
                    {
                        section.IsSparse = isSparseArr[targetIdx];
                        section.SparsePhysicalOffset = sparsePhysOffsetArr[targetIdx];
                        section.SparseBucketStart = sparseBucketStartArr[targetIdx];
                        section.SparseBucketEnd = sparseBucketEndArr[targetIdx];
                        section.SparseUpperIv = sparseUpperIvArr[targetIdx];
                        
                        if (decryptedHeader != null)
                        {
                            Console.WriteLine($"[StormNczStorage Debug] Section {targetIdx} header bytes 0x140-0x150: {BitConverter.ToString(decryptedHeader, 0x400 + targetIdx * 0x200 + 0x140, 16)}");
                            byte[] actualIv = new byte[16];
                            // NCA FS Header: 0x140 = Generation (uint32 LE), 0x144 = SecureValue (uint32 LE)
                            // CTR nonce: SecureValue (BE, bytes 0-3) | Generation (BE, bytes 4-7) | offset/16 (bytes 8-15)
                            int hdrBase = 0x400 + targetIdx * 0x200;
                            uint generation = BitConverter.ToUInt32(decryptedHeader, hdrBase + 0x140);
                            uint secureValue = BitConverter.ToUInt32(decryptedHeader, hdrBase + 0x144);
                            actualIv[0] = (byte)(secureValue >> 24);
                            actualIv[1] = (byte)(secureValue >> 16);
                            actualIv[2] = (byte)(secureValue >> 8);
                            actualIv[3] = (byte)(secureValue & 0xFF);
                            actualIv[4] = (byte)(generation >> 24);
                            actualIv[5] = (byte)(generation >> 16);
                            actualIv[6] = (byte)(generation >> 8);
                            actualIv[7] = (byte)(generation & 0xFF);
                            section.CryptoCounter = actualIv;
                            Console.WriteLine($"[StormNczStorage Debug] Section {section.Offset} (FsIndex={targetIdx}): CTR nonce (SecureValue={secureValue}, Generation={generation}): {BitConverter.ToString(actualIv).Replace("-","")}");
                        }
                    }
                    

                    
                    _sections.Add(section);
                    long bytesInZstd = Math.Max(0, (section.Offset + section.Size) - Math.Max(section.Offset, 0x4000));
                    currentSolidOffset += bytesInZstd;
                    
                    long sectionEnd = section.Offset + section.Size;
                    if (sectionEnd > calculatedNcaSize) calculatedNcaSize = sectionEnd;
                    
                    currentOffset += 64;
                }

                // Сортируем секции по виртуальному Offset для правильного расчета PhysicalOffset последовательно
                var physicalSortedSections = new List<NczSection>(_sections);
                physicalSortedSections.Sort((a, b) => a.Offset.CompareTo(b.Offset));
                
                currentPhysicalOffset = 0x4000;
                foreach (var section in physicalSortedSections)
                {
                    long vOffset = section.Offset;
                    long vSize = section.Size;
                    long pSize = (vOffset < 0x4000) ? Math.Max(0, vSize - (0x4000 - vOffset)) : vSize;
                    section.PhysicalOffset = currentPhysicalOffset;
                    section.PhysicalSize = pSize;
                    currentPhysicalOffset += pSize;
                }
                
                foreach (var section in _sections)
                {
                    Console.WriteLine($"[StormNczStorage Debug] Section FsIndex={section.FsIndex}: Offset={section.Offset}, Size={section.Size}, SkipBytes={section.SkipBytes}, CryptoType={section.CryptoType}, PhysicalOffset={section.PhysicalOffset}, PhysicalSize={section.PhysicalSize}, IsSparse={section.IsSparse}, CryptoKey={BitConverter.ToString(section.CryptoKey).Replace("-","").ToLowerInvariant()}");
                }

                if (ncaUniqueId != null)
                {
                    var cachedKeys = new (byte[] Key, byte CryptoType, byte[] Counter)[4];
                    for (int sIdx = 0; sIdx < 4; sIdx++)
                    {
                        var s = _sections.Find(sec => sec.FsIndex == sIdx);
                        if (s != null && s.CryptoKey != null && s.CryptoKey.Length == 16)
                        {
                            byte[] key = new byte[16];
                            Array.Copy(s.CryptoKey, key, 16);
                            byte[] ctr = new byte[16];
                            if (s.CryptoCounter != null && s.CryptoCounter.Length == 16)
                            {
                                Array.Copy(s.CryptoCounter, ctr, 16);
                            }
                            cachedKeys[sIdx] = (key, (byte)s.CryptoType, ctr);
                        }
                    }
                    lock (StormNczCompressor.NcaKeysCache)
                    {
                        StormNczCompressor.NcaKeysCache[ncaUniqueId] = cachedKeys;
                    }
                    Console.WriteLine($"[StormNczStorage] Cached {_sections.Count} keys and IVs for NCA={ncaUniqueId}");
                }

                // Ищем NCZBLOCK динамически
                byte[] searchBuffer = new byte[1024];
                _baseStorage.Read(currentOffset, searchBuffer);
                
                int blockMagicOffset = -1;
                byte[] targetMagic = Encoding.ASCII.GetBytes("NCZBLOCK");
                for (int i = 0; i < searchBuffer.Length - 8; i++)
                {
                    bool match = true;
                    for (int j = 0; j < 8; j++)
                    {
                        if (searchBuffer[i + j] != targetMagic[j])
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match)
                    {
                        blockMagicOffset = i;
                        break;
                    }
                }

                if (blockMagicOffset != -1)
                {
                    magic = "NCZBLOCK";
                    currentOffset += blockMagicOffset;
                }
                else
                {
                    _isSolid = true; // No NCZBLOCK found, assume pure Solid Zstd stream
                    byte[] zstdMagic = new byte[] { 0x28, 0xB5, 0x2F, 0xFD }; // Little Endian
                    int zstdMagicOffset = -1;
                    for (int i = 0; i < searchBuffer.Length - 4; i++)
                    {
                        if (searchBuffer[i] == zstdMagic[0] && searchBuffer[i+1] == zstdMagic[1] &&
                            searchBuffer[i+2] == zstdMagic[2] && searchBuffer[i+3] == zstdMagic[3])
                        {
                            zstdMagicOffset = i;
                            break;
                        }
                    }
                    
                    if (zstdMagicOffset != -1)
                    {
                        currentOffset += zstdMagicOffset;
                    }
                }
            }

            if (magic == "NCZBLOCK")
            {
                _isSolid = false;
                currentOffset += 8; // Смещение сразу после "NCZBLOCK"
                
                byte[] blockHeaderInfo = new byte[16];
                _baseStorage.Read(currentOffset, blockHeaderInfo);
                currentOffset += 16;
                
                _blockHeader = new NczBlockHeader
                {
                    Version = blockHeaderInfo[0],
                    Type = blockHeaderInfo[1],
                    Unused = blockHeaderInfo[2],
                    BlockSizeExponent = blockHeaderInfo[3],
                    NumberOfBlocks = BitConverter.ToInt32(blockHeaderInfo, 4),
                    DecompressedSize = BitConverter.ToInt64(blockHeaderInfo, 8)
                };
                
                // DecompressedSize stores only the body bytes AFTER the 0x4000 NCA header.
                // The full virtual NCA size = header + body.
                calculatedNcaSize = ncaHeaderSize + _blockHeader.DecompressedSize;

                byte[] blockSizes = new byte[_blockHeader.NumberOfBlocks * 4];
                _baseStorage.Read(currentOffset, blockSizes);
                currentOffset += blockSizes.Length;

                long tempBlockOffset = currentOffset;
                for(int i = 0; i < _blockHeader.NumberOfBlocks; i++)
                {
                    int compressedSize = BitConverter.ToInt32(blockSizes, i * 4);
                    _blockHeader.CompressedBlockSizeList.Add(compressedSize);
                    _blockHeader.CompressedBlockOffsetList.Add(tempBlockOffset);
                    tempBlockOffset += compressedSize;
                }
                PhysicalSize = tempBlockOffset;
            }
            
            _zstdStreamOffset = currentOffset;
            if (calculatedNcaSize % 0x200 != 0)
            {
                calculatedNcaSize += 0x200 - (calculatedNcaSize % 0x200);
            }
            _uncompressedSize = calculatedNcaSize;

            if (_isSolid)
            {
                // Solid архив: распаковываем целиком во временный файл
                _tempSolidFile = System.IO.Path.GetTempFileName();
                
                IStorage sourceStorage = _solidStorage ?? _baseStorage;
                sourceStorage.GetSize(out long sourceSize).ThrowIfFailure();
                
                long decompressOffset = _solidStorage != null ? 0 : _zstdStreamOffset;
                long decompressSize = sourceSize - decompressOffset;
                
                PhysicalSize = sourceSize; // In solid compression, the entire file is used

                using (var storageStream = new StorageStream(sourceStorage, decompressOffset, decompressSize))
                using (var zstdStream = new ZstdSharp.DecompressionStream(storageStream))
                using (var fs = new FileStream(_tempSolidFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    try
                    {
                        zstdStream.CopyTo(fs);
                    }
                    catch (Exception ex)
                    {
                        string errMsg = $"ZstdSharp failed to decompress solid stream. Offset: {decompressOffset}. Error: {ex.Message}";
                        throw new InvalidDataException(errMsg, ex);
                    }
                }
                
                _tempSolidStream = new FileStream(_tempSolidFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
        }


        public override LibHac.Result Read(long offset, Span<byte> destination)
        {
            long ncaHeaderSize = 0x4000;
            int bytesRead = 0;
            long currentOffset = offset;
            int remaining = destination.Length;
            
            while (remaining > 0)
            {
                // 1. Если запрос попадает в зону оригинального заголовка NCA (первые 0x4000)
                if (currentOffset < ncaHeaderSize)
                {
                    int toRead = (int)Math.Min((long)remaining, ncaHeaderSize - currentOffset);
                    lock (_baseStorageLock)
                    {
                        try
                        {
                            var slice = destination.Slice(bytesRead, toRead);
                            if (_isSolid && _ncaHeaderInSolidStream)
                            {
                                _tempSolidStream!.Position = currentOffset;
                                byte[] tempBuf = new byte[toRead];
                                _tempSolidStream.Read(tempBuf, 0, toRead);
                                tempBuf.AsSpan().CopyTo(slice);
                            }
                            else
                            {
                                _baseStorage.Read(currentOffset, slice);
                            }
                        }
                        catch (Exception) { }
                    }
                    bytesRead += toRead;
                    currentOffset += toRead;
                    remaining -= toRead;
                    continue;
                }
                
                // Ищем секцию, в которую попадает currentOffset (если они есть)
                NczSection? targetSection = null;
                if (_sections.Count > 0)
                {
                    foreach (var sec in _sections)
                    {
                        if (currentOffset >= sec.Offset && currentOffset < sec.Offset + sec.Size)
                        {
                            targetSection = sec;
                            break;
                        }
                    }

                    if (targetSection == null)
                    {
                        // Попали в "дыру" (uncompressed gap) - возвращаем нули
                        long nextSectionOffset = _uncompressedSize;
                        foreach (var sec in _sections)
                        {
                            if (sec.Offset > currentOffset && sec.Offset < nextSectionOffset)
                            {
                                nextSectionOffset = sec.Offset;
                            }
                        }
                        int toReadZeroes = (int)Math.Min((long)remaining, nextSectionOffset - currentOffset);
                        if (toReadZeroes <= 0) break; // EOF
                        
                        destination.Slice(bytesRead, toReadZeroes).Fill(0);
                        bytesRead += toReadZeroes;
                        currentOffset += toReadZeroes;
                        remaining -= toReadZeroes;
                        continue;
                    }
                }

                long offsetInSection = targetSection != null ? currentOffset - targetSection.Offset : 0;
                long mappedDataOffset = targetSection != null ? targetSection.SolidStreamOffset + offsetInSection : currentOffset;
                long dataRemainingInSection = targetSection != null ? targetSection.Size - offsetInSection : _uncompressedSize - currentOffset;

                // 2. Если Solid-архив (используем временный распакованный файл)
                if (_isSolid)
                {
                    lock (_baseStorageLock)
                    {
                        _tempSolidStream!.Position = mappedDataOffset;
                        long availableInStream = _tempSolidStream.Length - mappedDataOffset;
                        int toReadSolid = (int)Math.Min((long)remaining, Math.Min(dataRemainingInSection, availableInStream > 0 ? availableInStream : 0));
                        if (toReadSolid <= 0) break;
                        byte[] buf = new byte[toReadSolid];
                        _tempSolidStream.Read(buf, 0, toReadSolid);
                        
                        if (targetSection != null && currentOffset == targetSection.Offset)
                        {
                            Console.WriteLine($"[Diagnostic] Section Offset={currentOffset}: Raw ZSTD bytes={BitConverter.ToString(buf, 0, Math.Min(16, buf.Length))}");
                        }
                        
                        // Восстановление оригинального шифрования NCA (Re-encryption)
                        if (targetSection != null && (targetSection.CryptoType == 2 || targetSection.CryptoType == 3 || targetSection.CryptoType == 4))
                        {
                            EncryptSectionRegion(buf, 0, toReadSolid, currentOffset, targetSection);
                        }
                        
                        if (targetSection != null && currentOffset == targetSection.Offset)
                        {
                            Console.WriteLine($"[Diagnostic] Section Offset={currentOffset}: Encrypted bytes={BitConverter.ToString(buf, 0, Math.Min(16, buf.Length))}");
                        }
                        
                        try
                        {
                            buf.AsSpan().CopyTo(destination.Slice(bytesRead, toReadSolid));
                        }
                        catch (ArgumentOutOfRangeException ex)
                        {
                            throw new Exception($"CRITICAL SLICE ERROR: destLen={destination.Length}, bytesRead={bytesRead}, toReadSolid={toReadSolid}, remaining={remaining}, mappedOffset={mappedDataOffset}, streamLen={_tempSolidStream.Length}", ex);
                        }
                        bytesRead += toReadSolid;
                        currentOffset += toReadSolid;
                        remaining -= toReadSolid;
                    }
                    continue;
                }

                // 3. Вычисляем нужный блок Zstd
                long uncompressedBlockSize = 1L << _blockHeader!.BlockSizeExponent;
                
                // Проверяем выравнивание блоков (Concatenated vs Aligned)
                long expectedConcatBlocks = (_blockHeader.DecompressedSize + uncompressedBlockSize - 1) / uncompressedBlockSize;
                long expectedAlignedBlocks = 0;
                foreach (var sec in _sections)
                {
                    long bytesInZstd = sec.PhysicalSize;
                    expectedAlignedBlocks += (bytesInZstd + uncompressedBlockSize - 1) / uncompressedBlockSize;
                }
                bool isAligned = _blockHeader.NumberOfBlocks == expectedAlignedBlocks && expectedAlignedBlocks != expectedConcatBlocks;
                
                int blockIndex = -1;
                long offsetInBlock = 0;
                
                if (!isAligned)
                {
                    blockIndex = (int)(mappedDataOffset / uncompressedBlockSize);
                    offsetInBlock = mappedDataOffset % uncompressedBlockSize;
                }
                else
                {
                    long accumulatedDecompressedSize = 0;
                    int bIdx = 0;
                    foreach (var sec in _sections)
                    {
                        long bytesInZstd = sec.PhysicalSize;
                        long numBlocks = (bytesInZstd + uncompressedBlockSize - 1) / uncompressedBlockSize;
                        if (mappedDataOffset >= accumulatedDecompressedSize && mappedDataOffset < accumulatedDecompressedSize + bytesInZstd)
                        {
                            long offsetInSec = mappedDataOffset - accumulatedDecompressedSize;
                            blockIndex = bIdx + (int)(offsetInSec / uncompressedBlockSize);
                            offsetInBlock = offsetInSec % uncompressedBlockSize;
                            break;
                        }
                        accumulatedDecompressedSize += bytesInZstd;
                        bIdx += (int)numBlocks;
                    }
                }
                
                if (blockIndex == -1 || blockIndex >= _blockHeader.NumberOfBlocks)
                    break; // EOF
                
                int toReadFromBlock = (int)Math.Min((long)remaining, Math.Min(dataRemainingInSection, uncompressedBlockSize - offsetInBlock));
                
                // 4. Пытаемся получить блок из кэша
                byte[]? decompressedBlock = _blockCache.Get(blockIndex);
                
                // 5. Если блока нет в кэше — читаем с диска и распаковываем
                if (decompressedBlock == null)
                {
                    long compressedOffset = _blockHeader.CompressedBlockOffsetList[blockIndex];
                    int compressedSize = _blockHeader.CompressedBlockSizeList[blockIndex];
                    
                    _baseStorage.GetSize(out long baseSize).ThrowIfFailure();
                    if (compressedOffset + compressedSize > baseSize)
                    {
                        compressedSize = (int)Math.Max(0, baseSize - compressedOffset);
                    }
                    
                    byte[] compressedData = new byte[compressedSize];
                    lock (_baseStorageLock)
                    {
                        if (compressedSize > 0)
                        {
                            try
                            {
                                _baseStorage.Read(compressedOffset, compressedData);
                            }
                            catch (Exception)
                            {
                                // Файл физически усечен (truncated) и попытка чтения вышла за пределы физического FileStream.
                                // Заполняем нулями, чтобы не крашить парсер.
                                Array.Clear(compressedData, 0, compressedData.Length);
                            }
                        }
                    }
                    
                    long expectedDecompressedSize = uncompressedBlockSize;
                    if (!isAligned)
                    {
                        if (blockIndex == _blockHeader.NumberOfBlocks - 1)
                        {
                            expectedDecompressedSize = _blockHeader.DecompressedSize - (blockIndex * uncompressedBlockSize);
                        }
                    }
                    else
                    {
                        // Для aligned-режима вычисляем размер последнего блока секции
                        int bIdx = 0;
                        foreach (var sec in _sections)
                        {
                            long bytesInZstd = sec.PhysicalSize;
                            long numBlocks = (bytesInZstd + uncompressedBlockSize - 1) / uncompressedBlockSize;
                            if (blockIndex >= bIdx && blockIndex < bIdx + numBlocks)
                            {
                                if (blockIndex == bIdx + numBlocks - 1)
                                {
                                    expectedDecompressedSize = bytesInZstd % uncompressedBlockSize;
                                    if (expectedDecompressedSize == 0) expectedDecompressedSize = uncompressedBlockSize;
                                }
                                break;
                            }
                            bIdx += (int)numBlocks;
                        }
                    }
                    
                    if (expectedDecompressedSize <= 0) expectedDecompressedSize = uncompressedBlockSize;
                    decompressedBlock = new byte[(int)expectedDecompressedSize];
                    
                    if (compressedSize == expectedDecompressedSize)
                    {
                        Array.Copy(compressedData, decompressedBlock, compressedSize);
                    }
                    else
                    {
                        // Распаковка через ZstdSharp (потокобезопасно, создаем инстанс локально)
                        try
                        {
                            using (var decompressor = new Decompressor())
                            {
                                decompressor.Unwrap(new ReadOnlySpan<byte>(compressedData), new Span<byte>(decompressedBlock));
                            }
                        }
                        catch (Exception ex)
                        {
                            throw new Exception($"ZstdSharp failed to decompress block {blockIndex}. Compressed size: {compressedSize}. Offset: {compressedOffset}. Error: {ex.Message}", ex);
                        }
                    }
                    
                    _blockCache.Put(blockIndex, decompressedBlock);
                }
                
                // 6. Восстановление шифрования (Re-encryption) и копирование
                toReadFromBlock = Math.Min(toReadFromBlock, decompressedBlock.Length - (int)offsetInBlock);
                if (toReadFromBlock <= 0) break;
                byte[] finalBuf = new byte[toReadFromBlock];
                Array.Copy(decompressedBlock, (int)offsetInBlock, finalBuf, 0, toReadFromBlock);
                
                if (targetSection != null && currentOffset == targetSection.Offset)
                {
                    Console.WriteLine($"[Diagnostic] Block Offset={currentOffset}: Raw ZSTD bytes={BitConverter.ToString(finalBuf, 0, Math.Min(16, finalBuf.Length))}");
                }
                
                if (targetSection != null && (targetSection.CryptoType == 2 || targetSection.CryptoType == 3 || targetSection.CryptoType == 4))
                {
                    EncryptSectionRegion(finalBuf, 0, toReadFromBlock, currentOffset, targetSection);
                }
                
                if (targetSection != null && currentOffset == targetSection.Offset)
                {
                    Console.WriteLine($"[Diagnostic] Block Offset={currentOffset}: Encrypted bytes={BitConverter.ToString(finalBuf, 0, Math.Min(16, finalBuf.Length))}");
                }
                
                new Span<byte>(finalBuf, 0, toReadFromBlock).CopyTo(destination.Slice(bytesRead, toReadFromBlock));
                
                bytesRead += toReadFromBlock;
                currentOffset += toReadFromBlock;
                remaining -= toReadFromBlock;
            }
            
            // Заполняем остаток нулями, чтобы не возвращать неинициализированную память
            if (remaining > 0)
            {
                destination.Slice(bytesRead, (int)remaining).Fill(0);
            }
            
            return LibHac.Result.Success;
        }

        private void EncryptSectionRegion(byte[] data, int dataOffset, int length, long globalOffset, NczSection sec)
        {
            EncryptSectionRegionInternal(data, dataOffset, length, globalOffset, sec);
        }

        private void EncryptSectionRegionInternal(byte[] data, int dataOffset, int length, long globalOffset, NczSection sec)
        {
            long virtualOffset = globalOffset;
            long secOffset = virtualOffset - sec.Offset;
            if (secOffset + length <= sec.SkipBytes)
            {
                return; // Весь блок лежит в нешифрованной области (skipBytes)
            }
            
            if (secOffset < sec.SkipBytes)
            {
                long diff = sec.SkipBytes - secOffset;
                dataOffset += (int)diff;
                length -= (int)diff;
                globalOffset += diff;
                virtualOffset += diff;
            }

            if (sec.IsSparse)
            {
                long currentOffset = globalOffset;
                long currentVirtualOffset = virtualOffset;
                int remainingLength = length;
                int currentBlockOffset = dataOffset;
                
                while (remainingLength > 0)
                {
                    long currentPhysicalOffset = sec.PhysicalOffset + (currentVirtualOffset - Math.Max(sec.Offset, 0x4000));
                    
                    if (currentPhysicalOffset >= sec.SparseBucketStart && currentPhysicalOffset < sec.SparseBucketEnd)
                    {
                        int chunkLen = (int)Math.Min(remainingLength, sec.SparseBucketEnd - currentPhysicalOffset);
                        
                        ushort sparseGen = (ushort)(sec.SparseUpperIv >> 16);
                        AesCtrExXor(data, currentBlockOffset, chunkLen, sec.CryptoKey, sec.SparseUpperIv, sparseGen, currentVirtualOffset, sec.Offset);
                        
                        currentOffset += chunkLen;
                        currentVirtualOffset += chunkLen;
                        remainingLength -= chunkLen;
                        currentBlockOffset += chunkLen;
                    }
                    else
                    {
                        long nextTargetPhysical = sec.SparseBucketStart;
                        if (currentPhysicalOffset >= sec.SparseBucketEnd)
                        {
                            nextTargetPhysical = sec.PhysicalOffset + sec.PhysicalSize;
                        }
                        
                        int chunkLen = (int)Math.Min(remainingLength, nextTargetPhysical - currentPhysicalOffset);
                        
                        AesCtrXor(data, currentBlockOffset, chunkLen, sec.CryptoKey, sec.CryptoCounter, currentVirtualOffset, sec.Offset);
                        
                        currentOffset += chunkLen;
                        currentVirtualOffset += chunkLen;
                        remainingLength -= chunkLen;
                        currentBlockOffset += chunkLen;
                    }
                }
            }
            else
            {
                AesCtrXor(data, dataOffset, length, sec.CryptoKey, sec.CryptoCounter, virtualOffset, sec.Offset);
            }
        }

        private void AesCtrXor(byte[] data, int dataOffset, int length, byte[] key, byte[] iv, long globalOffset, long originalSectionOffset = 0)
        {
            if (globalOffset < 0x200 && length > 0)
            {
                App.Logger.Log($"[AesCtrXor Debug] Key={BitConverter.ToString(key).Replace("-","")}, IV={BitConverter.ToString(iv).Replace("-","")}, GlobalOffset={globalOffset}, SectionOffset={originalSectionOffset}, RelativeOffset={globalOffset - originalSectionOffset}");
            }
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Mode = System.Security.Cryptography.CipherMode.ECB;
            aes.Padding = System.Security.Cryptography.PaddingMode.None;
            aes.Key = key;

            using var encryptor = aes.CreateEncryptor();
            
            byte[] counter = new byte[16];
            byte[] encryptedCounter = new byte[16];
            
            long offsetFromSectionStart = globalOffset - originalSectionOffset;
            long blockIndex = offsetFromSectionStart / 16;
            int offsetInBlock = (int)(offsetFromSectionStart % 16);
            
            for (int i = 0; i < length; i++)
            {
                if (i == 0 || offsetInBlock == 0)
                {
                    Array.Copy(iv, counter, 16);
                    AddCounter(counter, blockIndex);
                    encryptor.TransformBlock(counter, 0, 16, encryptedCounter, 0);
                }
                
                data[dataOffset + i] ^= encryptedCounter[offsetInBlock];
                
                offsetInBlock++;
                if (offsetInBlock == 16)
                {
                    offsetInBlock = 0;
                    blockIndex++;
                }
            }
        }

        private void AddCounter(byte[] counter, long add)
        {
            ulong counterLow = 0;
            for (int j = 0; j < 8; j++)
            {
                counterLow = (counterLow << 8) | counter[8 + j];
            }
            counterLow += (ulong)add;
            for (int j = 7; j >= 0; j--)
            {
                counter[8 + j] = (byte)(counterLow & 0xFF);
                counterLow >>= 8;
            }
        }

        private void AesCtrExXor(byte[] data, int dataOffset, int length, byte[] key, ulong upperIv, ushort generation, long globalOffset, long originalSectionOffset = 0)
        {
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Mode = System.Security.Cryptography.CipherMode.ECB;
            aes.Padding = System.Security.Cryptography.PaddingMode.None;
            aes.Key = key;

            using var encryptor = aes.CreateEncryptor();
            
            byte[] counter = new byte[16];
            byte[] encryptedCounter = new byte[16];
            
            long offsetFromSectionStart = globalOffset - originalSectionOffset;
            long blockIndex = offsetFromSectionStart / 16;
            int offsetInBlock = (int)(offsetFromSectionStart % 16);
            
            for (int i = 0; i < length; i++)
            {
                if (i == 0 || offsetInBlock == 0)
                {
                    ulong tempUpper = upperIv;
                    for (int k = 0; k < 8; k++)
                    {
                        counter[7 - k] = (byte)(tempUpper & 0xFF);
                        tempUpper >>= 8;
                    }
                    
                    counter[8] = (byte)(generation >> 8);
                    counter[9] = (byte)(generation & 0xFF);
                    
                    uint blockIdxU32 = (uint)blockIndex;
                    counter[10] = (byte)(blockIdxU32 >> 24);
                    counter[11] = (byte)(blockIdxU32 >> 16);
                    counter[12] = (byte)(blockIdxU32 >> 8);
                    counter[13] = (byte)(blockIdxU32 & 0xFF);
                    
                    counter[14] = 0;
                    counter[15] = 0;
                    
                    encryptor.TransformBlock(counter, 0, 16, encryptedCounter, 0);
                }
                
                data[dataOffset + i] ^= encryptedCounter[offsetInBlock];
                
                offsetInBlock++;
                if (offsetInBlock == 16)
                {
                    offsetInBlock = 0;
                    blockIndex++;
                }
            }
        }

        public override LibHac.Result Write(long offset, ReadOnlySpan<byte> source)
        {
            return LibHac.Fs.ResultFs.NotImplemented.Value; // NCZ только для чтения
        }

        public override LibHac.Result Flush() => LibHac.Result.Success;
        public override LibHac.Result SetSize(long size) => LibHac.Fs.ResultFs.NotImplemented.Value;
        public override LibHac.Result GetSize(out long size)
        {
            size = _uncompressedSize;
            return LibHac.Result.Success;
        }
        public override LibHac.Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer) => LibHac.Result.Success;

        private static byte[] AesEcbDecrypt(byte[] data, byte[] key)
        {
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Mode = System.Security.Cryptography.CipherMode.ECB;
            aes.Padding = System.Security.Cryptography.PaddingMode.None;
            aes.Key = key;
            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(data, 0, 16);
        }

        private static byte[]? GetTitleKeyFromKeysetHex(string targetRightsIdHex)
        {
            try
            {
                string titleKeysPath1 = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "title.keys");
                string titleKeysPath2 = "title.keys"; // Current directory fallback
                string? titleKeysPath3 = null;
                try
                {
                    if (App.Settings?.Current?.KeysPath != null)
                    {
                        string? dir = System.IO.Path.GetDirectoryName(App.Settings.Current.KeysPath);
                        if (!string.IsNullOrEmpty(dir))
                        {
                            titleKeysPath3 = System.IO.Path.Combine(dir, "title.keys");
                        }
                    }
                }
                catch { }

                var pathsToCheck = new List<string> { titleKeysPath1, titleKeysPath2 };
                if (titleKeysPath3 != null) pathsToCheck.Add(titleKeysPath3);

                foreach (var path in pathsToCheck)
                {
                    if (File.Exists(path))
                    {
                        var lines = File.ReadAllLines(path);
                        foreach (var line in lines)
                        {
                            var parts = line.Split('=');
                            if (parts.Length == 2)
                            {
                                string rightsIdStr = parts[0].Trim().ToLowerInvariant();
                                string keyStr = parts[1].Trim().ToLowerInvariant();
                                
                                if (rightsIdStr == targetRightsIdHex)
                                {
                                    byte[] keyBytes = new byte[keyStr.Length / 2];
                                    for (int i = 0; i < keyBytes.Length; i++)
                                    {
                                        keyBytes[i] = Convert.ToByte(keyStr.Substring(i * 2, 2), 16);
                                    }
                                    return keyBytes;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return null;
        }
    }

    internal class StorageStream : Stream
    {
        private readonly IStorage _storage;
        private readonly long _baseOffset;
        private readonly long _length;
        private long _position;

        public StorageStream(IStorage storage, long baseOffset, long length)
        {
            _storage = storage;
            _baseOffset = baseOffset;
            _length = length;
            _position = 0;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _length;
        public override long Position
        {
            get => _position;
            set => _position = value;
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            long remaining = _length - _position;
            if (remaining <= 0) return 0;
            
            int toRead = (int)Math.Min(count, remaining);
            try
            {
                _storage.Read(_baseOffset + _position, buffer.AsSpan(offset, toRead)).ThrowIfFailure();
            }
            catch (Exception)
            {
                // Если мы вышли за пределы физического файла, просто возвращаем 0 (EOF)
                return 0;
            }
            _position += toRead;
            return toRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin: _position = offset; break;
                case SeekOrigin.Current: _position += offset; break;
                case SeekOrigin.End: _position = _length + offset; break;
            }
            return _position;
        }

        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

