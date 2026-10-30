#nullable enable

#nullable disable

using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LibHac;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using StormSwitchBox.Models;
using ZstdSharp;

namespace StormSwitchBox.Core.NSZ
{
    /// <summary>
    /// Высокопроизводительный нативный Zstandard-компрессор с поддержкой многопоточности,
    /// пулингом буферов ArrayPool (0 байт в LOH), пакетным AES-128-CTR (64 КБ батчи) и SIMD Vector256 XOR.
    /// </summary>
    public static class StormNczCompressor
    {
        public static readonly Dictionary<string, (byte[] Key, byte CryptoType, byte[] Counter)[]> NcaKeysCache =
            new Dictionary<string, (byte[] Key, byte CryptoType, byte[] Counter)[]>(StringComparer.OrdinalIgnoreCase);

        public static readonly Dictionary<string, byte[]> TitleKeysCache =
            new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        private const long NCA_HEADER_SIZE = 0x4000;
        private const int BLOCK_SIZE_EXPONENT = 20; // 2^20 = 1048576 (1 MB standard NSZ block size)
        private const int BLOCK_SIZE = 1 << BLOCK_SIZE_EXPONENT;
        private const int CTR_BATCH_BLOCKS = 4096;  // 4096 * 16 = 65536 bytes (64 KB per AES-NI TransformBlock call)
        private const int CTR_BATCH_BYTES = CTR_BATCH_BLOCKS * 16;

        private sealed class ThreadCompressionWorkspace : IDisposable
        {
            public readonly Compressor ZstdCompressor;
            public readonly byte[] CounterBatch = new byte[CTR_BATCH_BYTES];
            public readonly byte[] KeystreamBatch = new byte[CTR_BATCH_BYTES];
            private readonly Dictionary<SectionDecryptInfo, ICryptoTransform> _encryptors = new();

            public ThreadCompressionWorkspace(int compressionLevel)
            {
                ZstdCompressor = new Compressor(compressionLevel);
                // Каждый блок NCZBLOCK сжимается независимо (<= 1 МБ = 2^20).
                // Ограничиваем windowLog <= 20 (строго <= 27 стандарта Zstd), чтобы исключить ошибки лимита окна 128 МБ на уровнях 20..22.
                try { ZstdCompressor.SetParameter(ZstdSharp.Unsafe.ZSTD_cParameter.ZSTD_c_windowLog, BLOCK_SIZE_EXPONENT); } catch { }
            }

            public ICryptoTransform GetOrCreateEncryptor(SectionDecryptInfo sec)
            {
                if (_encryptors.TryGetValue(sec, out var enc))
                    return enc;

                using var aes = Aes.Create();
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                aes.Key = sec.CryptoKey;
                enc = aes.CreateEncryptor();
                _encryptors[sec] = enc;
                return enc;
            }

            public void Dispose()
            {
                ZstdCompressor?.Dispose();
                foreach (var enc in _encryptors.Values)
                {
                    try { enc?.Dispose(); } catch { }
                }
                _encryptors.Clear();
            }
        }

        public static void CompressNcaToNcz(
            IStorage ncaStorage,
            string outputNczPath,
            int compressionLevel,
            KeySet keyset,
            ProcessingTask task,
            CancellationToken cancellationToken,
            Dictionary<string, byte[]>? externalTitleKeys = null)
        {
            ncaStorage.GetSize(out long ncaSize).ThrowIfFailure();

            if (ncaSize <= NCA_HEADER_SIZE)
            {
                using var fsOutSmall = new FileStream(outputNczPath, FileMode.Create, FileAccess.Write);
                ncaStorage.AsStream().CopyTo(fsOutSmall);
                return;
            }

            Nca nca;
            try
            {
                nca = new Nca(keyset, ncaStorage);
            }
            catch
            {
                using var fsOutFallback = new FileStream(outputNczPath, FileMode.Create, FileAccess.Write);
                ncaStorage.AsStream().CopyTo(fsOutFallback);
                return;
            }

            using var fsOut = new FileStream(outputNczPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 4 * 1024 * 1024);

            // 1. NCA Header
            byte[] headerBuffer = new byte[NCA_HEADER_SIZE];
            ncaStorage.Read(0, headerBuffer).ThrowIfFailure();
            fsOut.Write(headerBuffer, 0, headerBuffer.Length);

            // 2. NCZSECTN
            var sections = new List<NczSection>();

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

                var transform = new Aes128XtsTransform(key1, key2, true);
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
            for (int j = 0; j < 16; j++)
            {
                if (rightsIdBytes[j] != 0)
                {
                    hasRightsId = true;
                    break;
                }
            }

            if (hasRightsId)
            {
                ncaUniqueId = BitConverter.ToString(rightsIdBytes).Replace("-", "").ToLowerInvariant();
            }
            else
            {
                using var sha = SHA256.Create();
                byte[] hash = sha.ComputeHash(decryptedHeader, 0, 0x200);
                ncaUniqueId = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
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

            var decryptInfos = new List<SectionDecryptInfo>();

            foreach (var secInfo in enabledSections)
            {
                int i = secInfo.Index;
                long vOffset = secInfo.VOffset;
                long vSize = secInfo.VSize;
                long pOffset = Math.Max(vOffset, NCA_HEADER_SIZE);
                long pSize = vSize - (pOffset - vOffset);

                byte[] startMagic = new byte[16];
                try
                {
                    ncaStorage.Read(pOffset, startMagic).ThrowIfFailure();
                }
                catch { }

                byte[] fsHeader = new byte[0x200];
                Array.Copy(decryptedHeader, 0x400 + i * 0x200, fsHeader, 0, 0x200);

                byte encryptionType = fsHeader[0x4];
                byte[] cryptoCounter = new byte[16];
                if (encryptionType == 2 || encryptionType == 3 || encryptionType == 4)
                {
                    for (int j = 0; j < 8; j++)
                    {
                        cryptoCounter[j] = fsHeader[0x140 + 7 - j];
                    }
                }

                ulong baseUpperIv = BinaryPrimitives.ReadUInt64LittleEndian(new ReadOnlySpan<byte>(fsHeader, 0x140, 8));
                byte[] cryptoKey = new byte[16];
                byte nczCryptoType = 1;

                byte[] cachedKey = null;
                byte cachedCryptoType = 1;
                byte[] cachedCounter = null;
                if (ncaUniqueId != null)
                {
                    lock (NcaKeysCache)
                    {
                        if (NcaKeysCache.TryGetValue(ncaUniqueId, out var cachedKeys))
                        {
                            if (i >= 0 && i < 4 && cachedKeys[i].Key != null && cachedKeys[i].CryptoType != 1)
                            {
                                cachedKey = cachedKeys[i].Key;
                                cachedCryptoType = cachedKeys[i].CryptoType;
                                cachedCounter = cachedKeys[i].Counter;
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
                        baseUpperIv = BinaryPrimitives.ReadUInt64BigEndian(new ReadOnlySpan<byte>(cachedCounter, 0, 8));
                    }
                }
                else if (encryptionType != 1)
                {
                    if (nca.Header.HasRightsId)
                    {
                        nczCryptoType = encryptionType;
                        try
                        {
                            byte[] tKey = GetTitleKeyFromKeyset(keyset, nca.Header.RightsId.ToArray(), externalTitleKeys);
                            if (tKey != null)
                            {
                                tKey.CopyTo((Span<byte>)cryptoKey);
                            }
                        }
                        catch { }
                    }
                    else
                    {
                        nczCryptoType = encryptionType;
                        try
                        {
                            try
                            {
                                nca.GetDecryptedKey(2).CopyTo((Span<byte>)cryptoKey);
                            }
                            catch { }

                            bool isZeroKey = IsAllZero(cryptoKey);
                            int keysetIndex = Math.Max(0, nca.Header.KeyGeneration - 1);
                            int keyAreaKeyIndex = nca.Header.KeyAreaKeyIndex;

                            byte[] encSlot = new byte[16];
                            Array.Copy(decryptedHeader, 0x320, encSlot, 0, 16);
                            if (IsAllZero(encSlot) && keyAreaKeyIndex >= 0 && keyAreaKeyIndex < 3)
                            {
                                Array.Copy(decryptedHeader, 0x300 + keyAreaKeyIndex * 16, encSlot, 0, 16);
                            }

                            var candidateKeyGens = new List<int> { keysetIndex, nca.Header.KeyGeneration };
                            for (int g = 0; g <= 32; g++)
                            {
                                if (!candidateKeyGens.Contains(g)) candidateKeyGens.Add(g);
                            }

                            var candidateKeyIndices = new List<int>();
                            if (keyAreaKeyIndex >= 0 && keyAreaKeyIndex < 3) candidateKeyIndices.Add(keyAreaKeyIndex);
                            for (int ki = 0; ki < 3; ki++)
                            {
                                if (!candidateKeyIndices.Contains(ki)) candidateKeyIndices.Add(ki);
                            }

                            byte[]? bestKey = null;
                            int lowestDistinctBytes = int.MaxValue;
                            bool foundValidMagic = false;
                            bool hasSample = !IsAllZero(startMagic);

                            foreach (int kGen in candidateKeyGens)
                            {
                                if (kGen < 0 || kGen >= keyset.KeyAreaKeys.Length) continue;

                                foreach (int kIdx in candidateKeyIndices)
                                {
                                    byte[] areaKey = new byte[16];
                                    ((ReadOnlySpan<byte>)keyset.KeyAreaKeys[kGen][kIdx]).CopyTo(areaKey);
                                    if (IsAllZero(areaKey)) continue;

                                    byte[] decKey = AesEcbDecrypt(encSlot, areaKey);
                                    if (IsAllZero(decKey)) continue;

                                    bool isHacpackKey = true;
                                    for (int b = 0; b < 16; b++)
                                    {
                                        if (decKey[b] != 0x04) { isHacpackKey = false; break; }
                                    }
                                    if (isHacpackKey)
                                    {
                                        bestKey = decKey;
                                        foundValidMagic = true;
                                        break;
                                    }

                                    if (hasSample)
                                    {
                                        byte[] testBlock = new byte[16];
                                        Array.Copy(startMagic, testBlock, 16);
                                        byte[] testCounter = new byte[16];
                                        Array.Copy(cryptoCounter, testCounter, 8);
                                        BinaryPrimitives.WriteUInt64BigEndian(new Span<byte>(testCounter, 8, 8), (ulong)(pOffset >> 4));
                                        using var testAes = Aes.Create();
                                        testAes.Mode = CipherMode.ECB;
                                        testAes.Padding = PaddingMode.None;
                                        testAes.Key = decKey;
                                        using var enc = testAes.CreateEncryptor();
                                        byte[] keystream = new byte[16];
                                        enc.TransformBlock(testCounter, 0, 16, keystream, 0);
                                        for (int b = 0; b < 16; b++) testBlock[b] ^= keystream[b];

                                        string magic4 = Encoding.ASCII.GetString(testBlock, 0, 4);
                                        if (magic4 == "IVFC" || magic4 == "PFS0" || magic4 == "PFC0" || magic4 == "HFS0")
                                        {
                                            bestKey = decKey;
                                            foundValidMagic = true;
                                            break;
                                        }

                                        int distinct = testBlock.Distinct().Count();
                                        if (distinct < lowestDistinctBytes)
                                        {
                                            lowestDistinctBytes = distinct;
                                            bestKey ??= decKey;
                                        }
                                    }
                                    else if (bestKey == null)
                                    {
                                        bestKey = decKey;
                                    }
                                }
                                if (foundValidMagic) break;
                            }

                            if (bestKey != null && (isZeroKey || foundValidMagic))
                            {
                                bestKey.CopyTo((Span<byte>)cryptoKey);
                            }
                        }
                        catch { }
                    }
                }

                ushort sparseGen = BitConverter.ToUInt16(fsHeader, 0x170);
                bool isSparse = sparseGen != 0;
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
                byte hashType = fsHeader[0x03];
                if (encryptionType == 5 || encryptionType == 6)
                {
                    if (hashType == 2 || hashType == 5)
                    {
                        int layerCount = BitConverter.ToInt32(fsHeader, 0x2C);
                        if (layerCount > 0 && layerCount <= 5)
                        {
                            skipBytes = BitConverter.ToInt64(fsHeader, 0x30 + (layerCount - 1) * 16);
                        }
                    }
                    else if (hashType == 3 || hashType == 6)
                    {
                        int maxLayers = BitConverter.ToInt32(fsHeader, 0x14);
                        if (maxLayers >= 2 && maxLayers <= 7)
                        {
                            skipBytes = BitConverter.ToInt64(fsHeader, 0x18 + (maxLayers - 2) * 24);
                        }
                    }
                }

                bool keyIsZero = IsAllZero(cryptoKey);
                bool handledBktr = false;

                if (encryptionType == 4 && !isSparse && !keyIsZero)
                {
                    long subRelOffset = BitConverter.ToInt64(fsHeader, 0x120);
                    long subSize = BitConverter.ToInt64(fsHeader, 0x128);

                    if (subRelOffset >= 0 && subSize >= 0x4000 && subSize <= 64L * 1024 * 1024 && vOffset + subRelOffset + subSize <= vOffset + vSize)
                    {
                        try
                        {
                            byte[] subBuf = new byte[subSize];
                            ncaStorage.Read(vOffset + subRelOffset, subBuf).ThrowIfFailure();

                            DecryptAesCtrDirect(subBuf, 0, subBuf.Length, vOffset + subRelOffset, cryptoKey, cryptoCounter);

                            int bucketCount = BitConverter.ToInt32(subBuf, 4);
                            if (bucketCount >= 1 && bucketCount <= 1000)
                            {
                                var subsectionEntries = new List<(long vOff, long nextVOff, int ctrVal)>();
                                long currentBucketCursor = 16;
                                for (int b = 0; b < bucketCount; b++)
                                {
                                    long bOff = (subSize >= 0x4000L * (1 + bucketCount)) ? (0x4000L + b * 0x4000L) : currentBucketCursor;
                                    if (bOff + 16 > subBuf.Length) break;
                                    int entryCount = BitConverter.ToInt32(subBuf, (int)bOff + 4);
                                    long bucketEnd = BitConverter.ToInt64(subBuf, (int)bOff + 8);
                                    if (entryCount < 1 || entryCount > 1023) break;
                                    long entriesEnd = bOff + 16 + entryCount * 16;
                                    if (entriesEnd > subBuf.Length) break;

                                    for (int e = 0; e < entryCount; e++)
                                    {
                                        int eOff = (int)(bOff + 16 + e * 16);
                                        long entryVOff = BitConverter.ToInt64(subBuf, eOff);
                                        int ctrVal = BitConverter.ToInt32(subBuf, eOff + 12);
                                        long nextVOff = (e + 1 < entryCount) ? BitConverter.ToInt64(subBuf, eOff + 16) : bucketEnd;
                                        if (nextVOff > entryVOff)
                                        {
                                            subsectionEntries.Add((entryVOff, nextVOff, ctrVal));
                                        }
                                    }
                                    currentBucketCursor = entriesEnd;
                                }

                                if (subsectionEntries.Count > 0)
                                {
                                    handledBktr = true;
                                    long encOffset = Math.Max(vOffset, NCA_HEADER_SIZE);
                                    long firstEntryAbsStart = Math.Max(vOffset + subsectionEntries[0].vOff, encOffset);
                                    if (encOffset < firstEntryAbsStart)
                                    {
                                        byte[] preCtr = (byte[])cryptoCounter.Clone();
                                        long preSz = firstEntryAbsStart - encOffset;
                                        decryptInfos.Add(new SectionDecryptInfo
                                        {
                                            Offset = encOffset,
                                            Size = preSz,
                                            PhysicalOffset = encOffset,
                                            PhysicalSize = preSz,
                                            EncryptionType = 3,
                                            CryptoKey = cryptoKey,
                                            IsZeroKey = keyIsZero,
                                            BaseUpperIv = baseUpperIv,
                                            CryptoCounter = preCtr
                                        });
                                        sections.Add(new NczSection
                                        {
                                            Offset = encOffset,
                                            Size = preSz,
                                            PhysicalOffset = encOffset,
                                            CryptoType = 3,
                                            CryptoKey = cryptoKey,
                                            CryptoCounter = preCtr,
                                            SolidStreamOffset = i
                                        });
                                    }

                                    foreach (var entry in subsectionEntries)
                                    {
                                        long absStart = Math.Max(vOffset + entry.vOff, encOffset);
                                        long absEnd = Math.Min(vOffset + entry.nextVOff, vOffset + vSize);
                                        if (absEnd > absStart)
                                        {
                                            byte[] subCtr = (byte[])cryptoCounter.Clone();
                                            BinaryPrimitives.WriteUInt32BigEndian(subCtr.AsSpan(4, 4), unchecked((uint)entry.ctrVal));
                                            long segSz = absEnd - absStart;
                                            decryptInfos.Add(new SectionDecryptInfo
                                            {
                                                Offset = absStart,
                                                Size = segSz,
                                                PhysicalOffset = absStart,
                                                PhysicalSize = segSz,
                                                EncryptionType = 3,
                                                CryptoKey = cryptoKey,
                                                IsZeroKey = keyIsZero,
                                                BaseUpperIv = baseUpperIv,
                                                CryptoCounter = subCtr
                                            });
                                            sections.Add(new NczSection
                                            {
                                                Offset = absStart,
                                                Size = segSz,
                                                PhysicalOffset = absStart,
                                                CryptoType = 3,
                                                CryptoKey = cryptoKey,
                                                CryptoCounter = subCtr,
                                                SolidStreamOffset = i
                                            });
                                        }
                                    }

                                    long lastRawEnd = vOffset + subsectionEntries[^1].nextVOff;
                                    long trailStart = Math.Max(lastRawEnd, encOffset);
                                    if (vOffset + vSize > trailStart)
                                    {
                                        long trailSz = (vOffset + vSize) - trailStart;
                                        byte[] trailCtr = (byte[])cryptoCounter.Clone();
                                        decryptInfos.Add(new SectionDecryptInfo
                                        {
                                            Offset = trailStart,
                                            Size = trailSz,
                                            PhysicalOffset = trailStart,
                                            PhysicalSize = trailSz,
                                            EncryptionType = 3,
                                            CryptoKey = cryptoKey,
                                            IsZeroKey = keyIsZero,
                                            BaseUpperIv = baseUpperIv,
                                            CryptoCounter = trailCtr
                                        });
                                        sections.Add(new NczSection
                                        {
                                            Offset = trailStart,
                                            Size = trailSz,
                                            PhysicalOffset = trailStart,
                                            CryptoType = 3,
                                            CryptoKey = cryptoKey,
                                            CryptoCounter = trailCtr,
                                            SolidStreamOffset = i
                                        });
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }

                if (!handledBktr)
                {
                    bool isAlreadyDecrypted = false;
                    if (encryptionType == 3 || encryptionType == 4)
                    {
                        uint magicVal = BitConverter.ToUInt32(startMagic, 0);
                        if (magicVal == 0x30534650 || magicVal == 0x43465649) // 'PFS0' or 'IVFC'
                        {
                            isAlreadyDecrypted = true;
                        }
                    }

                    if (keyIsZero && (nczCryptoType == 3 || nczCryptoType == 4))
                    {
                        nczCryptoType = 1;
                    }

                    long effNczOff = isSparse ? vOffset : pOffset;
                    long effNczSize = isSparse ? vSize : pSize;
                    if (effNczSize > 0)
                    {
                        decryptInfos.Add(new SectionDecryptInfo
                        {
                            Offset = vOffset,
                            Size = vSize,
                            PhysicalOffset = pOffset,
                            PhysicalSize = pSize,
                            EncryptionType = nczCryptoType,
                            CryptoKey = cryptoKey,
                            IsZeroKey = keyIsZero,
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
                            Offset = effNczOff,
                            Size = effNczSize,
                            PhysicalOffset = pOffset,
                            CryptoType = nczCryptoType,
                            CryptoKey = cryptoKey,
                            CryptoCounter = cryptoCounter,
                            SolidStreamOffset = i
                        });
                    }
                }
            }

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
            // 4. МНОГОПОТОЧНОЕ ДЕШИФРОВАНИЕ И СЖАТИЕ БЕЗ LOH-АЛЛОКАЦИЙ
            // ═══════════════════════════════════════════════════════════════
            int usedCores = 0;
            try
            {
                if (App.Settings?.Current != null && App.Settings.Current.UsedCores > 0)
                {
                    usedCores = App.Settings.Current.UsedCores;
                }
                else
                {
                    string settingsPath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "StormSwitchBox",
                        "ssb_native.settings.json");
                    if (File.Exists(settingsPath))
                    {
                        string json = File.ReadAllText(settingsPath);
                        using var doc = System.Text.Json.JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("UsedCores", out var prop))
                        {
                            usedCores = prop.GetInt32();
                        }
                    }
                }
            }
            catch { }

            int maxParallelism = Math.Max(1, usedCores > 0 ? usedCores : Environment.ProcessorCount);
            int batchSize = Math.Min(blocksToCompress, Math.Max(4, maxParallelism * 4));
            int maxCompressedBound = Compressor.GetCompressBound(BLOCK_SIZE);

            int[] compressedSizes = new int[blocksToCompress];
            long currentReadOffset = NCA_HEADER_SIZE;

            DateTime startTime = DateTime.UtcNow;
            long lastReportTicks = 0;

            // Арендуем буферы из ArrayPool<byte>.Shared один раз на всё время сжатия NCA -> 0 байт LOH-аллокаций
            byte[][] pooledRaw = new byte[batchSize][];
            byte[][] pooledComp = new byte[batchSize][];
            int[] currentRawSizes = new int[batchSize];
            long[] blockGlobalOffsets = new long[batchSize];
            int[] batchFinalSizes = new int[batchSize];
            bool[] batchUseCompressed = new bool[batchSize];

            for (int i = 0; i < batchSize; i++)
            {
                pooledRaw[i] = ArrayPool<byte>.Shared.Rent(BLOCK_SIZE);
                pooledComp[i] = ArrayPool<byte>.Shared.Rent(maxCompressedBound);
            }

            try
            {
                using var threadWorkspace = new ThreadLocal<ThreadCompressionWorkspace>(
                    () => new ThreadCompressionWorkspace(compressionLevel),
                    trackAllValues: true);

                for (int batchStart = 0; batchStart < blocksToCompress; batchStart += batchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    int currentBatchCount = Math.Min(batchSize, blocksToCompress - batchStart);

                    // 1. Чтение сырых зашифрованных блоков с хранилища в пулированные буферы
                    for (int i = 0; i < currentBatchCount; i++)
                    {
                        int toRead = (int)Math.Min(BLOCK_SIZE, ncaSize - currentReadOffset);
                        currentRawSizes[i] = toRead;
                        blockGlobalOffsets[i] = currentReadOffset;
                        ncaStorage.Read(currentReadOffset, new Span<byte>(pooledRaw[i], 0, toRead)).ThrowIfFailure();
                        currentReadOffset += toRead;
                    }

                    // 2. Параллельная дешифровка AES-CTR (64 КБ батчи + Vector256 SIMD) и Zstandard-компрессия во всех потоках
                    Parallel.For(0, currentBatchCount, new ParallelOptions { MaxDegreeOfParallelism = maxParallelism }, i =>
                    {
                        var ws = threadWorkspace.Value;
                        int toRead = currentRawSizes[i];
                        long blockOffset = blockGlobalOffsets[i];
                        byte[] rawBuf = pooledRaw[i];

                        for (int secIdx = 0; secIdx < decryptInfos.Count; secIdx++)
                        {
                            var sec = decryptInfos[secIdx];
                            if ((sec.EncryptionType == 3 || sec.EncryptionType == 4) && !sec.IsZeroKey && !sec.IsAlreadyDecrypted)
                            {
                                long overlapStart = Math.Max(blockOffset, sec.PhysicalOffset);
                                long overlapEnd = Math.Min(blockOffset + toRead, sec.PhysicalOffset + sec.PhysicalSize);

                                if (overlapStart < overlapEnd)
                                {
                                    int overlapLen = (int)(overlapEnd - overlapStart);
                                    int offsetInBlock = (int)(overlapStart - blockOffset);
                                    DecryptSectionRegion(rawBuf, offsetInBlock, overlapLen, overlapStart, sec, ws);
                                }
                            }
                        }

                        var rawSpan = new ReadOnlySpan<byte>(rawBuf, 0, toRead);
                        var compSpan = new Span<byte>(pooledComp[i], 0, maxCompressedBound);
                        int compLen = ws.ZstdCompressor.Wrap(rawSpan, compSpan);

                        if (compLen > 0 && compLen < toRead)
                        {
                            batchUseCompressed[i] = true;
                            batchFinalSizes[i] = compLen;
                        }
                        else
                        {
                            batchUseCompressed[i] = false;
                            batchFinalSizes[i] = toRead;
                        }
                    });

                    // 3. Последовательная запись сжатой пачки из пулированных буферов на диск
                    for (int i = 0; i < currentBatchCount; i++)
                    {
                        byte[] srcBuf = batchUseCompressed[i] ? pooledComp[i] : pooledRaw[i];
                        int finalLen = batchFinalSizes[i];
                        fsOut.Write(srcBuf, 0, finalLen);
                        compressedSizes[batchStart + i] = finalLen;
                    }

                    // 4. Обновление UI напрямую без рефлексии
                    long nowTicks = DateTime.UtcNow.Ticks;
                    if (nowTicks - lastReportTicks > System.TimeSpan.TicksPerSecond || batchStart + currentBatchCount >= blocksToCompress)
                    {
                        lastReportTicks = nowTicks;
                        double elapsedSec = (DateTime.UtcNow - startTime).TotalSeconds;
                        double mbPerSec = (currentReadOffset - NCA_HEADER_SIZE) / (1024.0 * 1024.0) / (elapsedSec > 0.001 ? elapsedSec : 0.001);
                        double progress = (double)currentReadOffset / ncaSize * 100.0;
                        int doneBlocks = batchStart + currentBatchCount;

                        if (task != null)
                        {
                            App.RunOnUI(() =>
                            {
                                task.Progress = progress;
                                task.Speed = $"{mbPerSec:F1} MB/s";
                                task.Status = $"Сжатие: {doneBlocks}/{blocksToCompress} блоков";
                            });
                        }
                    }
                }

                foreach (var ws in threadWorkspace.Values)
                {
                    ws?.Dispose();
                }
            }
            finally
            {
                for (int i = 0; i < batchSize; i++)
                {
                    if (pooledRaw[i] != null) ArrayPool<byte>.Shared.Return(pooledRaw[i]);
                    if (pooledComp[i] != null) ArrayPool<byte>.Shared.Return(pooledComp[i]);
                }
            }

            // 5. Запись таблицы размеров блоков одним буфером
            long endPos = fsOut.Position;
            fsOut.Seek(sizesTableOffset, SeekOrigin.Begin);
            byte[] sizeTableBytes = new byte[blocksToCompress * 4];
            Buffer.BlockCopy(compressedSizes, 0, sizeTableBytes, 0, sizeTableBytes.Length);
            fsOut.Write(sizeTableBytes, 0, sizeTableBytes.Length);
            fsOut.Seek(endPos, SeekOrigin.Begin);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsAllZero(ReadOnlySpan<byte> bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] != 0) return false;
            }
            return true;
        }

        private static void DecryptSectionRegion(
            byte[] blockData,
            int blockOffset,
            int length,
            long globalOffset,
            SectionDecryptInfo sec,
            ThreadCompressionWorkspace ws)
        {
            long virtualOffset = Math.Max(sec.Offset, 0x4000) + (globalOffset - sec.PhysicalOffset);
            long secOffset = virtualOffset - sec.Offset;

            if (secOffset + length <= sec.SkipBytes)
                return;

            if (secOffset < sec.SkipBytes)
            {
                long diff = sec.SkipBytes - secOffset;
                blockOffset += (int)diff;
                length -= (int)diff;
                virtualOffset += diff;
            }

            ICryptoTransform encryptor = ws.GetOrCreateEncryptor(sec);

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
                        AesCtrExXorBatched(blockData, currentBlockOffset, chunkLen, encryptor, ws, sec.SparseUpperIv, sparseGen, currentVirtualOffset, sec.Offset);

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
                        AesCtrXorBatched(blockData, currentBlockOffset, chunkLen, encryptor, ws, sec.CryptoCounter, currentVirtualOffset);

                        currentVirtualOffset += chunkLen;
                        remainingLength -= chunkLen;
                        currentBlockOffset += chunkLen;
                    }
                }
            }
            else
            {
                AesCtrXorBatched(blockData, blockOffset, length, encryptor, ws, sec.CryptoCounter, virtualOffset);
            }
        }

        private static void AesCtrXorBatched(
            byte[] data,
            int dataOffset,
            int length,
            ICryptoTransform encryptor,
            ThreadCompressionWorkspace ws,
            byte[] iv,
            long globalOffset)
        {
            ulong upperIv = BinaryPrimitives.ReadUInt64BigEndian(iv.AsSpan(0, 8));
            ulong baseLowIv = BinaryPrimitives.ReadUInt64BigEndian(iv.AsSpan(8, 8));

            long blockIndex = globalOffset >> 4;
            int offsetInFirstBlock = (int)(globalOffset & 15);

            byte[] counterBatch = ws.CounterBatch;
            byte[] keystreamBatch = ws.KeystreamBatch;

            int processed = 0;

            while (processed < length)
            {
                int remainingBytes = length - processed;
                int totalStreamBytesNeeded = offsetInFirstBlock + remainingBytes;
                int blocksNeeded = (totalStreamBytesNeeded + 15) >> 4;
                int blocksInBatch = Math.Min(blocksNeeded, CTR_BATCH_BLOCKS);
                int batchByteLen = blocksInBatch << 4;

                Span<ulong> counterU64 = MemoryMarshal.Cast<byte, ulong>(counterBatch.AsSpan(0, batchByteLen));
                ulong upperSwapped = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(upperIv) : upperIv;

                for (int b = 0; b < blocksInBatch; b++)
                {
                    ulong lowVal = baseLowIv + (ulong)(blockIndex + b);
                    counterU64[b * 2] = upperSwapped;
                    counterU64[b * 2 + 1] = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(lowVal) : lowVal;
                }

                encryptor.TransformBlock(counterBatch, 0, batchByteLen, keystreamBatch, 0);

                int usableStreamBytes = Math.Min(remainingBytes, batchByteLen - offsetInFirstBlock);
                XorSimd(
                    data.AsSpan(dataOffset + processed, usableStreamBytes),
                    keystreamBatch.AsSpan(offsetInFirstBlock, usableStreamBytes));

                processed += usableStreamBytes;
                blockIndex += blocksInBatch;
                offsetInFirstBlock = 0;
            }
        }

        private static void AesCtrExXorBatched(
            byte[] data,
            int dataOffset,
            int length,
            ICryptoTransform encryptor,
            ThreadCompressionWorkspace ws,
            ulong upperIv,
            ushort generation,
            long globalOffset,
            long originalSectionOffset)
        {
            long offsetFromSectionStart = globalOffset - originalSectionOffset;
            long blockIndex = offsetFromSectionStart >> 4;
            int offsetInFirstBlock = (int)(offsetFromSectionStart & 15);

            byte[] counterBatch = ws.CounterBatch;
            byte[] keystreamBatch = ws.KeystreamBatch;
            ulong upperSwapped = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(upperIv) : upperIv;

            int processed = 0;
            while (processed < length)
            {
                int remainingBytes = length - processed;
                int totalStreamBytesNeeded = offsetInFirstBlock + remainingBytes;
                int blocksNeeded = (totalStreamBytesNeeded + 15) >> 4;
                int blocksInBatch = Math.Min(blocksNeeded, CTR_BATCH_BLOCKS);
                int batchByteLen = blocksInBatch << 4;

                Span<ulong> counterU64 = MemoryMarshal.Cast<byte, ulong>(counterBatch.AsSpan(0, batchByteLen));
                for (int b = 0; b < blocksInBatch; b++)
                {
                    uint blockIdxU32 = (uint)(blockIndex + b);
                    ulong lowVal = ((ulong)generation << 48) | ((ulong)blockIdxU32 << 16);
                    counterU64[b * 2] = upperSwapped;
                    counterU64[b * 2 + 1] = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(lowVal) : lowVal;
                }

                encryptor.TransformBlock(counterBatch, 0, batchByteLen, keystreamBatch, 0);

                int usableStreamBytes = Math.Min(remainingBytes, batchByteLen - offsetInFirstBlock);
                XorSimd(
                    data.AsSpan(dataOffset + processed, usableStreamBytes),
                    keystreamBatch.AsSpan(offsetInFirstBlock, usableStreamBytes));

                processed += usableStreamBytes;
                blockIndex += blocksInBatch;
                offsetInFirstBlock = 0;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void XorSimd(Span<byte> dst, ReadOnlySpan<byte> src)
        {
            int i = 0;
            int len = dst.Length;

            if (Vector256.IsHardwareAccelerated && len >= 32)
            {
                ref byte dstRef = ref MemoryMarshal.GetReference(dst);
                ref byte srcRef = ref MemoryMarshal.GetReference(src);
                int simdLimit = len - 32;
                for (; i <= simdLimit; i += 32)
                {
                    Vector256<byte> vDst = Vector256.LoadUnsafe(ref Unsafe.Add(ref dstRef, i));
                    Vector256<byte> vSrc = Vector256.LoadUnsafe(ref Unsafe.Add(ref srcRef, i));
                    (vDst ^ vSrc).StoreUnsafe(ref Unsafe.Add(ref dstRef, i));
                }
            }
            else if (Vector.IsHardwareAccelerated && len >= Vector<byte>.Count)
            {
                int vecSize = Vector<byte>.Count;
                int vecLimit = len - vecSize;
                for (; i <= vecLimit; i += vecSize)
                {
                    var vDst = new Vector<byte>(dst.Slice(i, vecSize));
                    var vSrc = new Vector<byte>(src.Slice(i, vecSize));
                    (vDst ^ vSrc).CopyTo(dst.Slice(i, vecSize));
                }
            }

            for (; i < len; i++)
            {
                dst[i] ^= src[i];
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
            public bool IsZeroKey { get; set; }
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

        private static byte[] GetTitleKeyFromKeyset(KeySet keyset, byte[] targetRightsIdBytes, Dictionary<string, byte[]>? externalTitleKeys = null)
        {
            try
            {
                string targetRightsIdHex = BitConverter.ToString(targetRightsIdBytes).Replace("-", "").ToLowerInvariant();

                if (externalTitleKeys != null && externalTitleKeys.TryGetValue(targetRightsIdHex, out var extKey) && extKey != null && extKey.Length == 16)
                {
                    return extKey;
                }

                lock (TitleKeysCache)
                {
                    if (TitleKeysCache.TryGetValue(targetRightsIdHex, out var cachedKey) && cachedKey != null && cachedKey.Length == 16)
                    {
                        return cachedKey;
                    }
                }

                string titleKeysPath1 = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "title.keys");
                string titleKeysPath2 = "title.keys";
                string[] pathsToCheck = { titleKeysPath1, titleKeysPath2 };

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

                                if (rightsIdStr == targetRightsIdHex && keyStr.Length >= 32)
                                {
                                    byte[] keyBytes = new byte[16];
                                    for (int i = 0; i < 16; i++)
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

        private static void DecryptAesCtrDirect(byte[] buffer, int offset, int length, long globalOffset, byte[] key, byte[] iv)
        {
            if (length <= 0 || key == null || key.Length != 16 || IsAllZero(key)) return;
            using var aes = Aes.Create();
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            aes.Key = key;
            using var encryptor = aes.CreateEncryptor();

            ulong upperIv = BinaryPrimitives.ReadUInt64BigEndian(iv.AsSpan(0, 8));
            ulong baseLowIv = iv.Length >= 16 ? BinaryPrimitives.ReadUInt64BigEndian(iv.AsSpan(8, 8)) : 0;

            long blockIndex = globalOffset >> 4;
            int offsetInBlock = (int)(globalOffset & 15);
            byte[] counter = new byte[16];
            byte[] keystream = new byte[16];

            for (int i = 0; i < length; i++)
            {
                if (i == 0 || offsetInBlock == 0)
                {
                    BinaryPrimitives.WriteUInt64BigEndian(counter.AsSpan(0, 8), upperIv);
                    BinaryPrimitives.WriteUInt64BigEndian(counter.AsSpan(8, 8), baseLowIv + (ulong)blockIndex);
                    encryptor.TransformBlock(counter, 0, 16, keystream, 0);
                }

                buffer[offset + i] ^= keystream[offsetInBlock];
                offsetInBlock++;
                if (offsetInBlock == 16)
                {
                    offsetInBlock = 0;
                    blockIndex++;
                }
            }
        }

        private static byte[] AesEcbDecrypt(byte[] data, byte[] key)
        {
            using var aes = Aes.Create();
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
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
