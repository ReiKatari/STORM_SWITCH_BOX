#nullable disable

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using LibHac;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Common.Keys;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using ZstdSharp;
using StormSwitchBox.Models;

namespace StormSwitchBox.Core.NSZ
{
    /// <summary>
    /// Высокопроизводительный нативный Zstandard-компрессор с поддержкой многопоточности.
    /// </summary>
    public static class StormNczCompressor
    {
        public static readonly Dictionary<string, (byte[] Key, byte CryptoType, byte[] Counter)[]> NcaKeysCache = new Dictionary<string, (byte[] Key, byte CryptoType, byte[] Counter)[]>(StringComparer.OrdinalIgnoreCase);

        private const long NCA_HEADER_SIZE = 0x4000;
        private const int BLOCK_SIZE_EXPONENT = 18; // 2^18 = 256 KB per block
        private const int BLOCK_SIZE = 1 << BLOCK_SIZE_EXPONENT;

        public static void CompressNcaToNcz(
            IStorage ncaStorage,
            string outputNczPath,
            int compressionLevel,
            KeySet keyset,
            ProcessingTask task,
            CancellationToken cancellationToken)
        {
            ncaStorage.GetSize(out long ncaSize).ThrowIfFailure();

            if (ncaSize <= NCA_HEADER_SIZE)
            {
                using var fsOutSmall = new FileStream(outputNczPath, FileMode.Create, FileAccess.Write);
                ncaStorage.AsStream().CopyTo(fsOutSmall);
                return;
            }

            LibHac.Tools.FsSystem.NcaUtils.Nca nca;
            try { nca = new LibHac.Tools.FsSystem.NcaUtils.Nca(keyset, ncaStorage); }
            catch {
                using var fsOutFallback = new FileStream(outputNczPath, FileMode.Create, FileAccess.Write);
                ncaStorage.AsStream().CopyTo(fsOutFallback);
                return;
            }

            using var fsOut = new FileStream(outputNczPath, FileMode.Create, FileAccess.ReadWrite);

            // 1. NCA Header
            byte[] headerBuffer = new byte[NCA_HEADER_SIZE];
            ncaStorage.Read(0, headerBuffer).ThrowIfFailure();
            fsOut.Write(headerBuffer, 0, headerBuffer.Length);

            // 2. NCZSECTN
            var sections = new List<NczSection>();
            
            // Дешифруем заголовок NCA (0xC00 байт: 0x400 заголовок + 4 секционных заголовка по 0x200)
            // напрямую с использованием AES-128-XTS и ключа HeaderKey. Это полностью избавляет от
            // нестабильной рефлексии и гарантирует 100% корректные смещения секций.
            byte[] decryptedHeader = new byte[0xC00];
            byte[] headerEncrypted = new byte[0xC00];
            Array.Copy(headerBuffer, headerEncrypted, 0xC00);

            byte[] headerKey = new byte[32];
            ((ReadOnlySpan<byte>)keyset.HeaderKey).CopyTo(headerKey);

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

                for (int i = 0; i < encryptedLength / 0x200; i++)
                {
                    transform.TransformBlock(decryptedHeader, 0x200 + i * 0x200, 0x200, (ulong)(1 + i));
                }
            }

            string ncaUniqueId = null;
            byte[] rightsIdBytes = new byte[16];
            Array.Copy(decryptedHeader, 0x230, rightsIdBytes, 0, 16);
            bool hasRightsId = false;
            for (int j = 0; j < 16; j++) if (rightsIdBytes[j] != 0) { hasRightsId = true; break; }
            if (hasRightsId)
            {
                ncaUniqueId = BitConverter.ToString(rightsIdBytes).Replace("-", "").ToLowerInvariant();
            }
            else
            {
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(decryptedHeader, 0, 0x200);
                    ncaUniqueId = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }

            var enabledSections = new List<TempSectionInfo>();
            for (int i = 0; i < 4; i++)
            {
                if (nca.Header.IsSectionEnabled(i))
                {
                    uint startOffsetUnits = BitConverter.ToUInt32(decryptedHeader, 0x240 + i * 16);
                    uint endOffsetUnits = BitConverter.ToUInt32(decryptedHeader, 0x240 + i * 16 + 4);
                    enabledSections.Add(new TempSectionInfo
                    {
                        Index = i,
                        VOffset = (long)startOffsetUnits * 0x200,
                        VSize = (long)(endOffsetUnits - startOffsetUnits) * 0x200
                    });
                }
            }

            enabledSections.Sort((a, b) => a.VOffset.CompareTo(b.VOffset));

            long currentPhysicalOffset = 0x4000;
            var decryptInfos = new List<SectionDecryptInfo>();

            foreach (var secInfo in enabledSections)
            {
                int i = secInfo.Index;
                long vOffset = secInfo.VOffset;
                long vSize = secInfo.VSize;
                long pSize = (vOffset < 0x4000) ? Math.Max(0, vSize - (0x4000 - vOffset)) : vSize;
                long pOffset = currentPhysicalOffset;
                currentPhysicalOffset += pSize;

                    byte[] startMagic = new byte[16];
                    try
                    {
                        ncaStorage.Read(pOffset, startMagic).ThrowIfFailure();
                        Console.WriteLine($"[StormNczCompressor Debug] Section {i} start bytes: {BitConverter.ToString(startMagic)}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[StormNczCompressor Debug] Section {i} read error: {ex.Message}");
                    }

                    byte[] fsHeader = new byte[0x200];
                    Array.Copy(decryptedHeader, 0x400 + i * 0x200, fsHeader, 0, 0x200);
                    Console.WriteLine($"[StormNczCompressor Debug] FS Header Section {i}: Bytes={BitConverter.ToString(fsHeader, 0, 16)}");
                    // NCA FS Header layout: [0x02]=FormatType, [0x03]=HashType, [0x04]=EncryptionType
                    // EncryptionType: 1=None, 2=XTS, 3=AesCtr, 4=AesCtrEx(BKTR)
                    byte encryptionType = fsHeader[0x4];
                    byte[] cryptoCounter = new byte[16];
                    if (encryptionType == 2 || encryptionType == 3 || encryptionType == 4)
                    {
                        for (int j = 0; j < 8; j++)
                        {
                            cryptoCounter[j] = fsHeader[0x140 + 7 - j];
                        }
                    }

                    ulong baseUpperIv = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(new ReadOnlySpan<byte>(fsHeader, 0x140, 8));
                    byte[] cryptoKey = new byte[16];
                    byte nczCryptoType = 1; // NCZ CryptoType (NSZ standard): 1=None, 3=AesCtr, 4=BKTR

                    byte[] cachedKey = null;
                    byte cachedCryptoType = 1;
                    byte[] cachedCounter = null;
                    if (ncaUniqueId != null)
                    {
                        lock (NcaKeysCache)
                        {
                            if (NcaKeysCache.TryGetValue(ncaUniqueId, out var cachedKeys))
                            {
                                if (i >= 0 && i < 4 && cachedKeys[i].Key != null)
                                {
                                    // Используем кэш только если оригинальный раздел был зашифрован (CryptoType != 1)
                                    if (cachedKeys[i].CryptoType != 1)
                                    {
                                        cachedKey = cachedKeys[i].Key;
                                        cachedCryptoType = cachedKeys[i].CryptoType;
                                        cachedCounter = cachedKeys[i].Counter;
                                    }
                                }
                            }
                        }
                    }

                    if (cachedKey != null)
                    {
                        Array.Copy(cachedKey, cryptoKey, 16);
                        nczCryptoType = cachedCryptoType;
                        
                        if (cachedCounter != null && cachedCounter.Length == 16)
                        {
                            Array.Copy(cachedCounter, cryptoCounter, 16);
                            baseUpperIv = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(new ReadOnlySpan<byte>(cachedCounter, 0, 8));
                        }
                        
                        Console.WriteLine($"[StormNczCompressor] Retrieved cached key, type and IV for Section={i} from NcaKeysCache: Key={BitConverter.ToString(cryptoKey).Replace("-", "").ToLowerInvariant()}, Type={nczCryptoType}, UpperIv={baseUpperIv:X16}");
                    }
                    else if (encryptionType != 1)
                    {
                        if (nca.Header.HasRightsId)
                        {
                            nczCryptoType = encryptionType; // Use actual NCA encryption type (3=CTR, 4=BKTR)
                            string ridHex = BitConverter.ToString(nca.Header.RightsId.ToArray()).Replace("-", "").ToLowerInvariant();
                            Console.WriteLine($"[StormNczCompressor] NCA HasRightsId={nca.Header.HasRightsId}, Section={i} uses TitleKey, RightsId={ridHex}");
                            try
                            {
                                byte[] tKey = GetTitleKeyFromKeyset(keyset, nca.Header.RightsId.ToArray());
                                if (tKey != null)
                                {
                                    tKey.CopyTo((Span<byte>)cryptoKey);
                                    Console.WriteLine($"[StormNczCompressor] Found TitleKey={BitConverter.ToString(tKey).Replace("-", "").ToLowerInvariant()}");
                                }
                                else
                                {
                                    Console.WriteLine($"[StormNczCompressor] WARNING: TitleKey NOT found for RightsId {ridHex}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[StormNczCompressor] ERROR finding TitleKey: {ex.Message}");
                            }
                        }
                        else
                        {
                            nczCryptoType = encryptionType; // Use actual NCA encryption type (3=CTR, 4=BKTR)
                            Console.WriteLine($"[StormNczCompressor] Section={i} uses KeyAreaKey");
                            try
                            {
                                nca.GetDecryptedKey(2).CopyTo((Span<byte>)cryptoKey); // Always use key index 2 (CTR key area entry) per nsz standard
                                
                                bool isZeroKey = true;
                                for (int k = 0; k < 16; k++)
                                {
                                    if (cryptoKey[k] != 0)
                                    {
                                        isZeroKey = false;
                                        break;
                                    }
                                }
                                
                                if (isZeroKey)
                                {
                                    int keysetIndex = nca.Header.KeyGeneration;
                                    int keyAreaKeyIndex = nca.Header.KeyAreaKeyIndex;
                                    byte[] areaKey = new byte[16];
                                    ((ReadOnlySpan<byte>)keyset.KeyAreaKeys[keysetIndex][keyAreaKeyIndex]).CopyTo(areaKey);
                                    
                                    int keyOffset = 0x300 + keyAreaKeyIndex * 16;
                                    byte[] encryptedKey = new byte[16];
                                    Array.Copy(decryptedHeader, keyOffset, encryptedKey, 0, 16);
                                    
                                    Console.WriteLine($"[StormNczCompressor Debug] Manual key decryption info for Section={i}: KeyGeneration={keysetIndex}, KeyAreaKeyIndex={keyAreaKeyIndex}, keyOffset=0x{keyOffset:X}, AreaKey={BitConverter.ToString(areaKey).Replace("-","").ToLowerInvariant()}, EncryptedKey={BitConverter.ToString(encryptedKey).Replace("-","").ToLowerInvariant()}");
                                    
                                    byte[] decryptedKey = AesEcbDecrypt(encryptedKey, areaKey);
                                    decryptedKey.CopyTo((Span<byte>)cryptoKey);
                                }
                                
                                Console.WriteLine($"[StormNczCompressor] Decrypted key area key: {BitConverter.ToString(cryptoKey).Replace("-", "").ToLowerInvariant()}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[StormNczCompressor] Error in key area decryption: {ex.Message}");
                            }
                        }
                    }

                    // baseUpperIv already read at line 174 — do NOT overwrite (it may have been updated by cache)
                    ushort sparseGen = BitConverter.ToUInt16(fsHeader, 0x170);
                    
                    bool isSparse = (sparseGen != 0);
                    long sparsePhysicalOffset = 0;
                    long sparseBucketStart = 0;
                    long sparseBucketEnd = 0;
                    ulong sparseUpperIv = 0;

                    if (isSparse)
                    {
                        sparsePhysicalOffset = BitConverter.ToInt64(fsHeader, 0x168);
                        long bucketOffset = BitConverter.ToInt64(fsHeader, 0x148);
                        long bucketSize = BitConverter.ToInt64(fsHeader, 0x150);
                        
                        sparseBucketStart = sparsePhysicalOffset + bucketOffset;
                        sparseBucketEnd = sparseBucketStart + bucketSize;
                        
                        uint secureValue = (uint)(baseUpperIv >> 32);
                        sparseUpperIv = ((ulong)secureValue << 32) | ((ulong)sparseGen << 16);
                    }

                    long skipBytes = 0;
                    byte hashType = fsHeader[0x03]; // HashType at offset 0x03 (NOT 0x04 which is EncryptionType)
                    // HashType: 1=HierarchicalSha256, 2=HierarchicalSha256, 3=HierarchicalIntegrity(IVFC)
                    if (hashType == 2)
                    {
                        skipBytes = 0; // HierarchicalSha256 has no SkipBytes
                    }
                    else if (hashType == 3)
                    {
                        skipBytes = BitConverter.ToInt64(fsHeader, 0x90);
                    }

                    long metadataSize = 0;
                    long section1Size = vSize;
                    if (encryptionType == 4) // AesCtrEx (BKTR) — use encryptionType, not hashType
                    {
                        long aesCtrExOffset = BitConverter.ToInt64(fsHeader, 0x120);
                        if (aesCtrExOffset > 0 && aesCtrExOffset < vSize)
                        {
                            section1Size = aesCtrExOffset;
                            metadataSize = vSize - section1Size;
                            Console.WriteLine($"[StormNczCompressor Debug] BKTR found for Section {i}: aesCtrExOffset={aesCtrExOffset}, section1Size={section1Size}, metadataSize={metadataSize}");
                        }
                    }

                    if (metadataSize > 0 && metadataSize < vSize)
                    {
                        Console.WriteLine($"[StormNczCompressor Debug] Splitting Section {i}: vSize={vSize}, metadataSize={metadataSize}");
                        
                        decryptInfos.Add(new SectionDecryptInfo
                        {
                            Offset = vOffset,
                            Size = section1Size,
                            PhysicalOffset = pOffset,
                            PhysicalSize = section1Size,
                            EncryptionType = nczCryptoType,
                            CryptoKey = cryptoKey,
                            BaseUpperIv = baseUpperIv,
                            IsSparse = isSparse,
                            SparsePhysicalOffset = sparsePhysicalOffset,
                            SparseBucketStart = sparseBucketStart,
                            SparseBucketEnd = sparseBucketEnd,
                            SparseUpperIv = sparseUpperIv,
                            SkipBytes = skipBytes,
                            CryptoCounter = (byte[])cryptoCounter.Clone()
                        });
                        
                        sections.Add(new NczSection
                        {
                            Offset = vOffset,
                            Size = section1Size,
                            PhysicalOffset = pOffset,
                            CryptoType = nczCryptoType,
                            CryptoKey = cryptoKey,
                            CryptoCounter = cryptoCounter,
                            SolidStreamOffset = i
                        });

                        byte[] secondCryptoCounter = new byte[16];
                        Array.Copy(cryptoCounter, secondCryptoCounter, 8); // Nonce
                        
                        long secondSectionOffset = vOffset + section1Size;
                        ulong secondCounterLow = ((ulong)(secondSectionOffset / 0x200)) << 32;
                        System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(new Span<byte>(secondCryptoCounter, 8, 8), secondCounterLow);

                        decryptInfos.Add(new SectionDecryptInfo
                        {
                            Offset = secondSectionOffset,
                            Size = metadataSize,
                            PhysicalOffset = pOffset + section1Size,
                            PhysicalSize = metadataSize,
                            EncryptionType = nczCryptoType,
                            CryptoKey = cryptoKey,
                            BaseUpperIv = baseUpperIv,
                            IsSparse = false,
                            SkipBytes = 0,
                            CryptoCounter = (byte[])secondCryptoCounter.Clone()
                        });
                        
                        sections.Add(new NczSection
                        {
                            Offset = secondSectionOffset,
                            Size = metadataSize,
                            PhysicalOffset = pOffset + section1Size,
                            CryptoType = nczCryptoType,
                            CryptoKey = cryptoKey,
                            CryptoCounter = secondCryptoCounter,
                            SolidStreamOffset = i
                        });
                    }
                    else
                    {
                        bool isAlreadyDecrypted = false;
                        if (encryptionType == 3 || encryptionType == 4) // AesCtr or AesCtrEx(BKTR)
                        {
                            uint magicVal = BitConverter.ToUInt32(startMagic, 0);
                            if (magicVal == 0x30534650 || magicVal == 0x43465649) // 'PFS0' or 'IVFC'
                            {
                                isAlreadyDecrypted = true;
                                Console.WriteLine($"[StormNczCompressor] Section {i} is already decrypted/clear. Bypassing CTR decryption during compression.");
                                nczCryptoType = 1;
                                Array.Clear(cryptoKey, 0, cryptoKey.Length);
                            }
                        }

                        bool isCryptoKeyZero = true;
                        for (int k = 0; k < 16; k++)
                        {
                            if (cryptoKey[k] != 0)
                            {
                                isCryptoKeyZero = false;
                                break;
                            }
                        }
                        if (isCryptoKeyZero && (nczCryptoType == 3 || nczCryptoType == 4))
                        {
                            Console.WriteLine($"[StormNczCompressor] Section {i} has an all-zero key but is marked encrypted. Forcing nczCryptoType = 1 to pass-through original data.");
                            nczCryptoType = 1;
                        }

                        decryptInfos.Add(new SectionDecryptInfo
                        {
                            Offset = vOffset,
                            Size = vSize,
                            PhysicalOffset = pOffset,
                            PhysicalSize = pSize,
                            EncryptionType = nczCryptoType,
                            CryptoKey = cryptoKey,
                            BaseUpperIv = baseUpperIv,
                            IsSparse = isSparse,
                            SparsePhysicalOffset = sparsePhysicalOffset,
                            SparseBucketStart = sparseBucketStart,
                            SparseBucketEnd = sparseBucketEnd,
                            SparseUpperIv = sparseUpperIv,
                            SkipBytes = skipBytes,
                            IsAlreadyDecrypted = isAlreadyDecrypted,
                            CryptoCounter = (byte[])cryptoCounter.Clone()
                        });

                        sections.Add(new NczSection
                        {
                            Offset = vOffset,
                            Size = vSize,
                            PhysicalOffset = pOffset,
                            CryptoType = nczCryptoType,
                            CryptoKey = cryptoKey,
                            CryptoCounter = cryptoCounter,
                            SolidStreamOffset = i
                        });
                    }
                }

            // Sort sections and decryptInfos by physical Offset ascending to match physical ZSTD block stream layout
            sections.Sort((a, b) => a.PhysicalOffset.CompareTo(b.PhysicalOffset));
            decryptInfos.Sort((a, b) => a.PhysicalOffset.CompareTo(b.PhysicalOffset));

            byte[] sectnMagic = Encoding.ASCII.GetBytes("NCZSECTN");
            fsOut.Write(sectnMagic, 0, 8);
            fsOut.Write(BitConverter.GetBytes((long)sections.Count), 0, 8);
            foreach (var sec in sections)
            {
                fsOut.Write(BitConverter.GetBytes(sec.Offset), 0, 8);
                fsOut.Write(BitConverter.GetBytes(sec.Size), 0, 8);
                fsOut.Write(BitConverter.GetBytes(sec.CryptoType), 0, 8);
                fsOut.Write(new byte[8], 0, 8);
                fsOut.Write(sec.CryptoKey, 0, 16);
                fsOut.Write(sec.CryptoCounter, 0, 16);
            }

            // 3. NCZBLOCK
            long bytesToCompress = ncaSize - NCA_HEADER_SIZE;
            int blocksToCompress = (int)(bytesToCompress / BLOCK_SIZE + (bytesToCompress % BLOCK_SIZE > 0 ? 1 : 0));

            byte[] blockMagic = Encoding.ASCII.GetBytes("NCZBLOCK");
            fsOut.Write(blockMagic, 0, 8);
            fsOut.WriteByte(2);
            fsOut.WriteByte(1);
            fsOut.WriteByte(0);
            fsOut.WriteByte(BLOCK_SIZE_EXPONENT);
            fsOut.Write(BitConverter.GetBytes(blocksToCompress), 0, 4);
            fsOut.Write(BitConverter.GetBytes(bytesToCompress), 0, 8);

            long sizesTableOffset = fsOut.Position;
            fsOut.Write(new byte[blocksToCompress * 4], 0, blocksToCompress * 4);

            // ═══════════════════════════════════════════════════════════════
            // 4. МНОГОПОТОЧНОЕ СЖАТИЕ (Вариант А)
            // ═══════════════════════════════════════════════════════════════
            int usedCores = 0;
            try
            {
                string settingsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ssb_native.settings.json");
                if (File.Exists(settingsPath))
                {
                    string json = File.ReadAllText(settingsPath);
                    using (var doc = System.Text.Json.JsonDocument.Parse(json))
                    {
                        if (doc.RootElement.TryGetProperty("UsedCores", out var prop))
                        {
                            usedCores = prop.GetInt32();
                        }
                    }
                }
            }
            catch { }
            
            // Limit parallelism to prevent WinUI 3 render thread starvation and system freezes
            int maxParallelism = Math.Max(1, usedCores > 0 ? usedCores : Environment.ProcessorCount);
            int batchSize = maxParallelism * 4; // Размер окна для обработки (баланс памяти и скорости)
            
            int[] compressedSizes = new int[blocksToCompress];
            long currentReadOffset = NCA_HEADER_SIZE;
            long totalCompressedSize = 0;
            
            DateTime startTime = DateTime.UtcNow;
            long lastReportTicks = 0;

            // Предварительное выделение памяти для блоков, чтобы избежать огромной нагрузки на GC
            byte[][] preallocatedRaw = new byte[batchSize][];
            for (int i = 0; i < batchSize; i++) preallocatedRaw[i] = new byte[BLOCK_SIZE];

            using (var threadCompressor = new ThreadLocal<Compressor>(() => new Compressor(compressionLevel), trackAllValues: true))
            {
                for (int batchStart = 0; batchStart < blocksToCompress; batchStart += batchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    
                    int currentBatchCount = Math.Min(batchSize, blocksToCompress - batchStart);
                    int[] currentRawSizes = new int[currentBatchCount];
                    byte[][] batchCompressedBlocks = new byte[currentBatchCount][];

                    // Чтение пачки блоков с диска (последовательно)
                    for (int i = 0; i < currentBatchCount; i++)
                    {
                        int toRead = (int)Math.Min(BLOCK_SIZE, ncaSize - currentReadOffset);
                        currentRawSizes[i] = toRead;
                        ncaStorage.Read(currentReadOffset, new Span<byte>(preallocatedRaw[i], 0, toRead)).ThrowIfFailure();

                        // Дешифровка секций "на лету" перед компрессией через прямой AES-CTR (с учётом sparse metadata)
                        for (int secIdx = 0; secIdx < decryptInfos.Count; secIdx++)
                        {
                            var sec = decryptInfos[secIdx];
                            if (sec.EncryptionType == 3 || sec.EncryptionType == 4) // AesCtr or BKTR
                            {
                                long overlapStart = Math.Max(currentReadOffset, sec.PhysicalOffset);
                                long overlapEnd = Math.Min(currentReadOffset + toRead, sec.PhysicalOffset + sec.PhysicalSize);
                                
                                if (overlapStart < overlapEnd)
                                {
                                    int overlapLen = (int)(overlapEnd - overlapStart);
                                    int offsetInBlock = (int)(overlapStart - currentReadOffset);
                                    try
                                    {
                                        bool isZeroKey = true;
                                        for (int k = 0; k < 16; k++)
                                        {
                                            if (sec.CryptoKey[k] != 0)
                                            {
                                                isZeroKey = false;
                                                break;
                                            }
                                        }
                                        if (i == 0 && batchStart == 0)
                                        {
                                            Console.WriteLine($"[StormNczCompressor] Decrypt check: isZeroKey={isZeroKey}, cryptoKey={BitConverter.ToString(sec.CryptoKey).Replace("-","").ToLowerInvariant()}, type={sec.EncryptionType}");
                                        }
                                          if (!isZeroKey && (sec.EncryptionType == 3 || sec.EncryptionType == 4) && !sec.IsAlreadyDecrypted)
                                          {
                                              DecryptSectionRegion(preallocatedRaw[i], offsetInBlock, overlapLen, overlapStart, sec);
                                          }
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"[StormNczCompressor] DecryptSectionRegion error: {ex.Message}");
                                    }
                                }
                            }
                        }

                        currentReadOffset += toRead;
                    }

                    // Параллельное сжатие пачки блоков в памяти с повторным использованием компрессора
                    Parallel.For(0, currentBatchCount, new ParallelOptions { MaxDegreeOfParallelism = maxParallelism }, i =>
                    {
                        var compressor = threadCompressor.Value;
                        if (compressor != null)
                        {
                            var rawSpan = new ReadOnlySpan<byte>(preallocatedRaw[i], 0, currentRawSizes[i]);
                            byte[] compressed = compressor.Wrap(rawSpan).ToArray();
                            
                            if (compressed.Length < currentRawSizes[i])
                            {
                                batchCompressedBlocks[i] = compressed;
                            }
                            else
                            {
                                batchCompressedBlocks[i] = rawSpan.ToArray(); // Не сжимается
                            }
                        }
                    });

                    // Yield to prevent complete thread starvation
                    Thread.Sleep(1);

                    // Запись сжатой пачки на диск (последовательно)
                    for (int i = 0; i < currentBatchCount; i++)
                    {
                        byte[] data = batchCompressedBlocks[i];
                        fsOut.Write(data, 0, data.Length);
                        compressedSizes[batchStart + i] = data.Length;
                        totalCompressedSize += data.Length;
                    }

                    // Обновление UI и статистики скорости (интервал увеличен до 1 секунды для максимальной плавности UI)
                    long nowTicks = DateTime.UtcNow.Ticks;
                    if (nowTicks - lastReportTicks > System.TimeSpan.TicksPerSecond * 1.0 || batchStart + batchSize >= blocksToCompress)
                    {
                        lastReportTicks = nowTicks;
                        double elapsedSec = (DateTime.UtcNow - startTime).TotalSeconds;
                        double mbPerSec = (currentReadOffset - NCA_HEADER_SIZE) / 1024.0 / 1024.0 / (elapsedSec > 0 ? elapsedSec : 1);
                        double progress = (double)currentReadOffset / ncaSize * 100.0;

                        bool dispatched = false;
                        try
                        {
                            var appType = Type.GetType("StormSwitchBox.App, StormSwitchBox");
                            if (appType != null)
                            {
                                var mainDispatcherProp = appType.GetProperty("MainDispatcher", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                                if (mainDispatcherProp != null)
                                {
                                    var dispatcher = mainDispatcherProp.GetValue(null);
                                    if (dispatcher != null)
                                    {
                                        var tryEnqueueMethod = dispatcher.GetType().GetMethod("TryEnqueue", new Type[] { typeof(Microsoft.UI.Dispatching.DispatcherQueueHandler) });
                                        if (tryEnqueueMethod != null)
                                        {
                                            Action updateAction = () =>
                                            {
                                                task.Progress = progress;
                                                task.Speed = $"{mbPerSec:F1} MB/s";
                                                task.Status = $"Сжатие: {batchStart + currentBatchCount}/{blocksToCompress} блоков";
                                            };
                                            var handlerType = tryEnqueueMethod.GetParameters()[0].ParameterType;
                                            var handlerDelegate = Delegate.CreateDelegate(handlerType, updateAction.Target, updateAction.Method);
                                            tryEnqueueMethod.Invoke(dispatcher, new object[] { handlerDelegate });
                                            dispatched = true;
                                        }
                                    }
                                }
                            }
                        }
                        catch { }

                        if (!dispatched)
                        {
                            task.Progress = progress;
                            task.Speed = $"{mbPerSec:F1} MB/s";
                            task.Status = $"Сжатие: {batchStart + currentBatchCount}/{blocksToCompress} блоков";
                        }
                    }
                }

                // Освобождаем все созданные thread-local инстансы компрессоров
                foreach (var comp in threadCompressor.Values)
                {
                    comp?.Dispose();
                }
            }

            // 5. Update Block Sizes Table
            long endPos = fsOut.Position;
            fsOut.Seek(sizesTableOffset, SeekOrigin.Begin);
            for (int i = 0; i < blocksToCompress; i++)
            {
                fsOut.Write(BitConverter.GetBytes(compressedSizes[i]), 0, 4);
            }
            fsOut.Seek(endPos, SeekOrigin.Begin);
        }

        private static void DecryptSectionRegion(byte[] blockData, int blockOffset, int length, long globalOffset, SectionDecryptInfo sec)
        {
            long virtualOffset = Math.Max(sec.Offset, 0x4000) + (globalOffset - sec.PhysicalOffset);
            long secOffset = virtualOffset - sec.Offset;
            
            if (secOffset + length <= sec.SkipBytes)
            {
                return; // Весь блок лежит в нешифрованной области (skipBytes)
            }
            
            if (secOffset < sec.SkipBytes)
            {
                long diff = sec.SkipBytes - secOffset;
                blockOffset += (int)diff;
                length -= (int)diff;
                virtualOffset += diff;
            }

            if (sec.IsSparse)
            {
                long currentVirtualOffset = virtualOffset;
                int remainingLength = length;
                int currentBlockOffset = blockOffset;
                
                while (remainingLength > 0)
                {
                    long currentPhysicalOffset = sec.PhysicalOffset + (currentVirtualOffset - Math.Max(sec.Offset, 0x4000));
                    if (currentPhysicalOffset >= sec.SparseBucketStart && currentPhysicalOffset < sec.SparseBucketEnd)
                    {
                        int chunkLen = (int)Math.Min(remainingLength, sec.SparseBucketEnd - currentPhysicalOffset);
                        ushort sparseGen = (ushort)(sec.SparseUpperIv >> 16);
                        AesCtrExXorDirect(blockData, currentBlockOffset, chunkLen, sec.CryptoKey, sec.SparseUpperIv, sparseGen, currentVirtualOffset, sec.Offset);
                        
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
                        
                        AesCtrXorDirect(blockData, currentBlockOffset, chunkLen, sec.CryptoKey, sec.CryptoCounter, currentVirtualOffset, sec.Offset);
                        
                        currentVirtualOffset += chunkLen;
                        remainingLength -= chunkLen;
                        currentBlockOffset += chunkLen;
                    }
                }
            }
            else
            {
                AesCtrXorDirect(blockData, blockOffset, length, sec.CryptoKey, sec.CryptoCounter, virtualOffset, sec.Offset);
            }
        }

        private static void AesCtrExXorDirect(byte[] data, int dataOffset, int length, byte[] key, ulong upperIv, ushort generation, long globalOffset, long originalSectionOffset)
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
            
            int i = 0;
            while (i < length)
            {
                if (i == 0 || offsetInBlock == 0)
                {
                    if (i > 0)
                    {
                        blockIndex++;
                    }
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
                
                int remaining = 16 - offsetInBlock;
                int toProcess = Math.Min(remaining, length - i);
                for (int j = 0; j < toProcess; j++)
                {
                    data[dataOffset + i + j] ^= encryptedCounter[offsetInBlock + j];
                }
                
                i += toProcess;
                offsetInBlock += toProcess;
                if (offsetInBlock >= 16)
                {
                    offsetInBlock = 0;
                }
            }
        }

        private static void AesCtrXorDirect(byte[] data, int dataOffset, int length, byte[] key, byte[] iv, long globalOffset, long originalSectionOffset)
        {
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Mode = System.Security.Cryptography.CipherMode.ECB;
            aes.Padding = System.Security.Cryptography.PaddingMode.None;
            aes.Key = key;

            using var encryptor = aes.CreateEncryptor();
            
            byte[] counter = new byte[16];
            byte[] encryptedCounter = new byte[16];
            
            long blockIndex = globalOffset / 16;
            int offsetInBlock = (int)(globalOffset % 16);
            
            Array.Copy(iv, counter, 16);
            AddCounter(counter, blockIndex);
            
            int i = 0;
            while (i < length)
            {
                // Generate encrypted counter for current block
                if (i == 0 || offsetInBlock == 0)
                {
                    if (i > 0)
                    {
                        blockIndex++;
                        Array.Copy(iv, counter, 16);
                        AddCounter(counter, blockIndex);
                    }
                    encryptor.TransformBlock(counter, 0, 16, encryptedCounter, 0);
                }

                // XOR as many bytes as possible within current 16-byte AES block
                int remaining = 16 - offsetInBlock;
                int toProcess = Math.Min(remaining, length - i);
                for (int j = 0; j < toProcess; j++)
                {
                    data[dataOffset + i + j] ^= encryptedCounter[offsetInBlock + j];
                }
                i += toProcess;
                offsetInBlock += toProcess;
                if (offsetInBlock >= 16)
                {
                    offsetInBlock = 0;
                }
            }
        }

        private static void AddCounter(byte[] counter, long value)
        {
            long val = value;
            for (int i = 15; i >= 0; i--)
            {
                int sum = counter[i] + (int)(val & 0xFF);
                counter[i] = (byte)sum;
                val >>= 8;
                val += (sum >> 8);
                if (val == 0) break;
            }
        }

        private class SectionDecryptInfo
        {
            public long Offset { get; set; }
            public long Size { get; set; }
            public long PhysicalOffset { get; set; }
            public long PhysicalSize { get; set; }
            public byte EncryptionType { get; set; }
            public byte[] CryptoKey { get; set; }
            public ulong BaseUpperIv { get; set; }
            public bool IsSparse { get; set; }
            public long SparsePhysicalOffset { get; set; }
            public long SparseBucketStart { get; set; }
            public long SparseBucketEnd { get; set; }
            public ulong SparseUpperIv { get; set; }
            public long SkipBytes { get; set; }
            public bool IsAlreadyDecrypted { get; set; }
            public byte[] CryptoCounter { get; set; }
        }

        private static byte[] GetTitleKeyFromKeyset(KeySet keyset, byte[] targetRightsIdBytes)
        {
            try
            {
                string targetRightsIdHex = BitConverter.ToString(targetRightsIdBytes).Replace("-", "").ToLowerInvariant();
                
                string titleKeysPath1 = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "title.keys");
                string titleKeysPath2 = "title.keys"; // Current directory fallback
                string[] pathsToCheck = { titleKeysPath1, titleKeysPath2 };

                foreach (var path in pathsToCheck)
                {
                    if (System.IO.File.Exists(path))
                    {
                        var lines = System.IO.File.ReadAllLines(path);
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

        private static byte[] ExtractBytesFromRightsId(object keyObj)
        {
            try
            {
                var valField = keyObj.GetType().GetField("Value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (valField == null) return null;
                
                var valObj = valField.GetValue(keyObj);
                if (valObj == null) return null;
                
                byte[] bytes = new byte[16];
                var valType = valObj.GetType();
                for (int i = 0; i < 16; i++)
                {
                    var elemField = valType.GetField($"Element{i}", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (elemField != null)
                    {
                        bytes[i] = (byte)elemField.GetValue(valObj);
                    }
                }
                return bytes;
            }
            catch
            {
                string hex = keyObj.ToString()?.Replace("-", "").Trim().ToLowerInvariant();
                if (!string.IsNullOrEmpty(hex) && hex.Length == 32)
                {
                    byte[] bytes = new byte[16];
                    for (int i = 0; i < 16; i++)
                    {
                        bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
                    }
                    return bytes;
                }
            }
            return null;
        }

        private static byte[] ExtractBytesFromAccessKey(object valueObj)
        {
            try
            {
                var valProp = valueObj.GetType().GetProperty("Value", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (valProp != null)
                {
                    var spanObj = valProp.GetValue(valueObj);
                    if (spanObj != null)
                    {
                        var toArrayMethod = spanObj.GetType().GetMethod("ToArray");
                        if (toArrayMethod != null)
                        {
                            return toArrayMethod.Invoke(spanObj, null) as byte[];
                        }
                    }
                }
            }
            catch {}
            
            try
            {
                var keyField = valueObj.GetType().GetField("Key", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (keyField != null)
                {
                    var keyObj = keyField.GetValue(valueObj);
                    if (keyObj != null)
                    {
                        byte[] bytes = new byte[16];
                        var keyType = keyObj.GetType();
                        for (int i = 0; i < 16; i++)
                        {
                            var elemField = keyType.GetField($"Element{i}", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            if (elemField != null)
                            {
                                bytes[i] = (byte)elemField.GetValue(keyObj);
                            }
                        }
                        return bytes;
                    }
                }
            }
            catch {}
            return null;
        }

        private static byte[] AesEcbDecrypt(byte[] data, byte[] key)
        {
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Mode = System.Security.Cryptography.CipherMode.ECB;
            aes.Padding = System.Security.Cryptography.PaddingMode.None;
            aes.Key = key;
            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(data, 0, 16);
        }

        private struct TempSectionInfo
        {
            public int Index;
            public long VOffset;
            public long VSize;
        }
    }
}
