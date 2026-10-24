using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    /// <summary>
    /// Нативный криптографический движок Nintendo 3DS (NCCH AES-128-CTR + 128-bit Hardware KeyScrambler + SeedDB 9.6+).
    /// Выполняет полную автоматическую расшифровку образов .3ds, .cci, .cia и .cxi без внешних утилит.
    /// </summary>
    public static class Nintendo3dsCryptoService
    {
        // Аппаратная константа скремблера ключей Nintendo 3DS: 0x1FF9E9AAC5FE0408024591DC5D52768A
        private static readonly byte[] ScramblerConstant = Convert.FromHexString("1FF9E9AAC5FE0408024591DC5D52768A");

        // Стандартные ритейловые слоты KeyX Nintendo 3DS
        private static readonly byte[] DefaultSlot0x2CKeyX = Convert.FromHexString("B98E95CECA3E4D171F76A94DE934C053");
        private static readonly byte[] DefaultSlot0x25KeyX = Convert.FromHexString("CEE7D8AB30C00DAE850EF5E382AC5AF3");
        private static readonly byte[] DefaultSlot0x18KeyX = Convert.FromHexString("82E9C9BEBFB8BDB875ECC0A07D474374");
        private static readonly byte[] DefaultSlot0x1BKeyX = Convert.FromHexString("45AD04953992C7C893724A9A7BCE6182");
        private static readonly byte[] SystemFixedKey = Convert.FromHexString("527CE630A9CA305F3696F3CDE954194B");

        // Общие ключи eShop (Common Keys 0..5) для расшифровки TitleKey внутри CIA Ticket
        private static readonly byte[][] CommonKeys = new byte[][]
        {
            Convert.FromHexString("64C5FD55DD3AD988325BAAEC5243DB98"), // 0: eShop
            Convert.FromHexString("4AAA3D0E27D4D728D0B1B433F0F9CBC8"), // 1: System
            Convert.FromHexString("FBB0EF8CDBB0D8E453CD99344371697F"), // 2
            Convert.FromHexString("25959B7AD0409F72684198BA2ECD7DC6"), // 3
            Convert.FromHexString("7ADA22CAFFC476CC8297A0C7CEEEEEBE"), // 4
            Convert.FromHexString("A5051CA1B37DCF3AFBCF8CC1EDD9CE02")  // 5
        };

        private static readonly Dictionary<ulong, byte[]> _seedCache = new();
        private static bool _seedCacheLoaded;
        private static readonly object _seedLock = new();

        /// <summary>
        /// Проверяет, зашифрован ли образ Nintendo 3DS (.3ds, .cci, .cia, .cxi).
        /// </summary>
        public static bool Is3dsFileEncrypted(string filePath) => IsEncrypted3dsFile(filePath, out _, out _);

        public static async Task<bool> Decrypt3dsFileAsync(
            string sourcePath,
            string outputPath,
            ProcessingTask? task,
            IProgress<(double Percent, string Status)>? progress,
            CancellationToken ct)
        {
            progress?.Report((15, $"???????? ?????????? 3DS (SeedDB + NCCH): {Path.GetFileName(sourcePath)}..."));
            bool ok = await Decrypt3dsFileAsync(sourcePath, outputPath, task, ct);
            progress?.Report((100, ok ? "?????????? 3DS ??????? ?????????" : "????? ?? ???????? ??????????"));
            return ok;
        }

        public static bool IsEncrypted3dsFile(string filePath, out bool usesSeedCrypto, out string details)
        {
            usesSeedCrypto = false;
            details = "Не зашифрован (Decrypted)";
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return false;

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (fs.Length < 0x200) return false;

                using var reader = new BinaryReader(fs);
                long ncchOffset = LocatePrimaryNcchOffset(fs, reader, out bool ciaContentEncrypted);

                if (ciaContentEncrypted)
                {
                    details = "Зашифрован контейнер CIA (AES-CBC TitleKey)";
                    return true;
                }

                if (ncchOffset < 0 || ncchOffset + 0x200 > fs.Length) return false;

                fs.Seek(ncchOffset + 0x100, SeekOrigin.Begin);
                if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "NCCH") return false;

                fs.Seek(ncchOffset + 0x188, SeekOrigin.Begin);
                byte[] flags = reader.ReadBytes(8);
                byte cryptoFlag = flags[7];
                byte keySlotFlag = flags[3];

                bool noCrypto = (cryptoFlag & 0x04) != 0;
                usesSeedCrypto = (cryptoFlag & 0x20) != 0;
                bool fixedKey = (cryptoFlag & 0x01) != 0;

                if (noCrypto)
                {
                    details = "Образ уже расшифрован (NoCrypto Flag 0x04)";
                    return false;
                }

                string slotName = keySlotFlag switch
                {
                    0x00 => "Standard (0x2C)",
                    0x01 => "7.x Crypto (0x25)",
                    0x0A => "New3DS 9.3 (0x18)",
                    0x0B => "New3DS 9.6 (0x1B)",
                    _ => $"Slot 0x{keySlotFlag:X2}"
                };

                if (fixedKey)
                {
                    details = "Зашифрован фиксированным ключом (FixedKey 0x01)";
                }
                else if (usesSeedCrypto)
                {
                    details = $"Зашифрован NCCH + SeedDB ({slotName} + Seed 0x20)";
                }
                else
                {
                    details = $"Зашифрован NCCH ({slotName})";
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Автоматически расшифровывает образ 3DS, если он зашифрован, возвращая путь к расшифрованному файлу.
        /// Если образ уже расшифрован, возвращает исходный путь без лишнего копирования.
        /// </summary>
        public static async Task<string> EnsureDecrypted3dsFileAsync(
            string inputFilePath,
            string tempWorkDir,
            ProcessingTask? task,
            CancellationToken ct)
        {
            if (!IsEncrypted3dsFile(inputFilePath, out bool usesSeed, out string cryptoDesc))
            {
                return inputFilePath;
            }

            string decryptedPath = Path.Combine(tempWorkDir, "dec_" + Path.GetFileName(inputFilePath));
            App.RunOnUI(() =>
            {
                if (task != null)
                {
                    task.LogDetails += $"\n🔓 [3DS Auto-Decrypt] Обнаружено шифрование ({cryptoDesc}) в «{Path.GetFileName(inputFilePath)}». Запуск нативной дешифровки...";
                }
            });

            bool ok = await Decrypt3dsFileAsync(inputFilePath, decryptedPath, task, ct);
            if (ok && File.Exists(decryptedPath) && new FileInfo(decryptedPath).Length > 0)
            {
                App.RunOnUI(() =>
                {
                    if (task != null)
                    {
                        task.LogDetails += $"\n  ✓ [3DS Auto-Decrypt] Образ «{Path.GetFileName(inputFilePath)}» успешно расшифрован (NoCrypto 0x04).";
                    }
                });
                return decryptedPath;
            }

            return inputFilePath;
        }

        /// <summary>
        /// Выполняет полную расшифровку файла .3ds / .cci / .cia / .cxi в целевой файл.
        /// </summary>
        public static async Task<bool> Decrypt3dsFileAsync(
            string sourcePath,
            string outputPath,
            ProcessingTask? task,
            CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(sourcePath)) return false;

                    string? outDir = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                        Directory.CreateDirectory(outDir);

                    if (!string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(sourcePath, outputPath, overwrite: true);
                    }

                    EnsureSeedDbLoaded();
                    var customKeyX = LoadCustomKeyXSlots();

                    using var fs = new FileStream(outputPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
                    using var reader = new BinaryReader(fs);
                    using var writer = new BinaryWriter(fs);

                    if (fs.Length < 0x200) return false;

                    fs.Seek(0x100, SeekOrigin.Begin);
                    string magic = Encoding.ASCII.GetString(reader.ReadBytes(4));

                    if (magic == "NCSD")
                    {
                        // Расшифровываем все до 8 разделов картриджа .3ds / .cci
                        for (int p = 0; p < 8; p++)
                        {
                            ct.ThrowIfCancellationRequested();
                            fs.Seek(0x120 + p * 8, SeekOrigin.Begin);
                            uint partOffsetUnits = reader.ReadUInt32();
                            uint partSizeUnits = reader.ReadUInt32();
                            if (partOffsetUnits == 0 || partSizeUnits == 0) continue;

                            long ncchOffset = (long)partOffsetUnits * 0x200;
                            if (ncchOffset + 0x200 <= fs.Length)
                            {
                                DecryptNcchPartitionInPlace(fs, reader, writer, ncchOffset, customKeyX, task, ct);
                            }
                        }
                        return true;
                    }
                    else if (magic == "NCCH")
                    {
                        DecryptNcchPartitionInPlace(fs, reader, writer, 0, customKeyX, task, ct);
                        return true;
                    }
                    else
                    {
                        // Проверяем структуру CIA
                        return DecryptCiaContainerInPlace(fs, reader, writer, customKeyX, task, ct);
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.Log($"[3DS Crypto] Ошибка расшифровки {Path.GetFileName(sourcePath)}: {ex.Message}", LogLevel.Warning);
                    return false;
                }
            }, ct);
        }

        private static bool DecryptCiaContainerInPlace(
            FileStream fs,
            BinaryReader reader,
            BinaryWriter writer,
            Dictionary<byte, byte[]> customKeyX,
            ProcessingTask? task,
            CancellationToken ct)
        {
            fs.Seek(0, SeekOrigin.Begin);
            uint headerSize = reader.ReadUInt32();
            ushort type = reader.ReadUInt16();
            ushort version = reader.ReadUInt16();
            uint certSize = reader.ReadUInt32();
            uint ticketSize = reader.ReadUInt32();
            uint tmdSize = reader.ReadUInt32();
            uint metaSize = reader.ReadUInt32();
            ulong contentSize = reader.ReadUInt64();

            if (headerSize < 0x20 || headerSize > 0x4000 || certSize == 0 || tmdSize == 0)
                return false;

            long ticketOffset = Align64(headerSize) + Align64(certSize);
            long tmdOffset = ticketOffset + Align64(ticketSize);
            long contentOffset = tmdOffset + Align64(tmdSize);

            if (tmdOffset + 0x204 > fs.Length) return false;

            // Читаем количество контентов из TMD (смещение +0x1DE в TMD с RSA-2048 подписью)
            fs.Seek(tmdOffset + 0x1DE, SeekOrigin.Begin);
            ushort contentCount = BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));
            if (contentCount == 0 || contentCount > 256) contentCount = 1;

            // Проверяем, зашифрован ли сам CIA слой (AES-CBC по TitleKey из Ticket)
            byte[]? decryptedTitleKey = TryExtractCiaTitleKey(fs, reader, ticketOffset, ticketSize);

            long currentContentPos = contentOffset;
            for (int c = 0; c < contentCount; c++)
            {
                ct.ThrowIfCancellationRequested();
                long recordOffset = tmdOffset + 0xB04 + (long)c * 0x30;
                if (recordOffset + 0x30 > contentOffset) break;

                fs.Seek(recordOffset + 4, SeekOrigin.Begin);
                ushort contentIndex = BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));
                ushort contentTypeFlags = BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));
                ulong cSize = BinaryPrimitives.ReadUInt64BigEndian(reader.ReadBytes(8));

                if (currentContentPos + (long)cSize > fs.Length) break;

                // Если установлен флаг 0x0001 (Encrypted CIA Content Layer) и у нас есть TitleKey:
                if ((contentTypeFlags & 0x0001) != 0 && decryptedTitleKey != null)
                {
                    fs.Seek(currentContentPos + 0x100, SeekOrigin.Begin);
                    string innerMagic = (cSize >= 0x200) ? Encoding.ASCII.GetString(reader.ReadBytes(4)) : "";
                    if (innerMagic != "NCCH")
                    {
                        byte[] iv = new byte[16];
                        BinaryPrimitives.WriteUInt16BigEndian(iv.AsSpan(0, 2), contentIndex);
                        DecryptAesCbcRegionInPlace(fs, currentContentPos, (long)cSize, decryptedTitleKey, iv, ct);
                    }

                    // Снимаем бит шифрования CIA (0x0001) в записи TMD
                    ushort newFlags = (ushort)(contentTypeFlags & ~0x0001);
                    fs.Seek(recordOffset + 6, SeekOrigin.Begin);
                    byte[] flagBytes = new byte[2];
                    BinaryPrimitives.WriteUInt16BigEndian(flagBytes, newFlags);
                    writer.Write(flagBytes);
                }

                // Теперь проверяем и расшифровываем внутренний NCCH раздел
                if (cSize >= 0x200)
                {
                    fs.Seek(currentContentPos + 0x100, SeekOrigin.Begin);
                    if (Encoding.ASCII.GetString(reader.ReadBytes(4)) == "NCCH")
                    {
                        DecryptNcchPartitionInPlace(fs, reader, writer, currentContentPos, customKeyX, task, ct);
                    }
                }

                // Обновляем SHA-256 хеш контента в записи TMD после расшифровки NCCH
                if (cSize > 0 && currentContentPos + (long)cSize <= fs.Length)
                {
                    byte[] sha256 = ComputeRegionSha256(fs, currentContentPos, (long)cSize);
                    fs.Seek(recordOffset + 0x10, SeekOrigin.Begin);
                    writer.Write(sha256);
                }

                currentContentPos += Align64((long)cSize);
            }

            return true;
        }

        private static byte[]? TryExtractCiaTitleKey(FileStream fs, BinaryReader reader, long ticketOffset, uint ticketSize)
        {
            try
            {
                if (ticketSize < 0x210 || ticketOffset + 0x210 > fs.Length) return null;

                // Смещение данных Ticket после RSA-2048 подписи (0x140)
                long tikData = ticketOffset + 0x140;
                fs.Seek(tikData + 0x7F, SeekOrigin.Begin);
                byte[] encryptedTitleKey = reader.ReadBytes(16);

                fs.Seek(tikData + 0x9C, SeekOrigin.Begin);
                byte[] titleIdBytes = reader.ReadBytes(8);

                fs.Seek(tikData + 0xB1, SeekOrigin.Begin);
                byte commonKeyIndex = reader.ReadByte();
                if (commonKeyIndex >= CommonKeys.Length) commonKeyIndex = 0;

                byte[] iv = new byte[16];
                Array.Copy(titleIdBytes, 0, iv, 0, 8);

                using var aes = Aes.Create();
                aes.Key = CommonKeys[commonKeyIndex];
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.None;
                using var dec = aes.CreateDecryptor();
                return dec.TransformFinalBlock(encryptedTitleKey, 0, 16);
            }
            catch
            {
                return null;
            }
        }

        private static void DecryptNcchPartitionInPlace(
            FileStream fs,
            BinaryReader reader,
            BinaryWriter writer,
            long ncchOffset,
            Dictionary<byte, byte[]> customKeyX,
            ProcessingTask? task,
            CancellationToken ct)
        {
            fs.Seek(ncchOffset, SeekOrigin.Begin);
            byte[] header = reader.ReadBytes(0x200);
            if (Encoding.ASCII.GetString(header, 0x100, 4) != "NCCH") return;

            byte keySlotFlag = header[0x18B]; // Flags[3]
            byte cryptoFlag = header[0x18F];  // Flags[7]

            bool noCrypto = (cryptoFlag & 0x04) != 0;
            if (noCrypto) return; // Уже расшифрован

            bool fixedKey = (cryptoFlag & 0x01) != 0;
            bool usesSeed = (cryptoFlag & 0x20) != 0;

            ulong partitionId = BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(0x108, 8));
            ulong programId = BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(0x118, 8));
            ushort ncchVersion = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(0x112, 2));

            byte[] keyY0 = new byte[16];
            Array.Copy(header, 0, keyY0, 0, 16);

            byte[] keyY1 = (byte[])keyY0.Clone();
            if (usesSeed)
            {
                byte[]? seed = LookupSeed(programId, header.AsSpan(0x114, 4).ToArray());
                if (seed != null)
                {
                    byte[] combined = new byte[32];
                    Array.Copy(keyY0, 0, combined, 0, 16);
                    Array.Copy(seed, 0, combined, 16, 16);
                    byte[] sha = SHA256.HashData(combined);
                    Array.Copy(sha, 0, keyY1, 0, 16);
                }
                else
                {
                    App.RunOnUI(() =>
                    {
                        if (task != null)
                            task.LogDetails += $"\n  ⚠️ [SeedDB] Не найден Seed для ProgramID {programId:X16}. Поместите актуальный seeddb.bin в tools/3ds/.";
                    });
                }
            }

            byte[] normalKey0;
            byte[] normalKey1;

            if (fixedKey)
            {
                bool isSystemTitle = ((programId >> 32) & 0x10) != 0;
                normalKey0 = isSystemTitle ? SystemFixedKey : new byte[16];
                normalKey1 = normalKey0;
            }
            else
            {
                byte[] keyX0 = customKeyX.TryGetValue(0x2C, out var kx2c) ? kx2c : DefaultSlot0x2CKeyX;
                byte[] keyX1 = keySlotFlag switch
                {
                    0x00 => keyX0,
                    0x01 => customKeyX.TryGetValue(0x25, out var kx25) ? kx25 : DefaultSlot0x25KeyX,
                    0x0A => customKeyX.TryGetValue(0x18, out var kx18) ? kx18 : DefaultSlot0x18KeyX,
                    0x0B => customKeyX.TryGetValue(0x1B, out var kx1b) ? kx1b : DefaultSlot0x1BKeyX,
                    _ => keyX0
                };

                normalKey0 = Derive3dsNormalKey(keyX0, keyY0);
                normalKey1 = Derive3dsNormalKey(keyX1, keyY1);
            }

            // 1. Расшифровка ExHeader (0x800 байт на смещении ncchOffset + 0x200)
            uint exHeaderSize = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x180, 4));
            if (exHeaderSize > 0 && ncchOffset + 0x200 + 0x800 <= fs.Length)
            {
                byte[] ctrExh = BuildNcchCounter(partitionId, ncchVersion, sectionType: 1, ncchOffset + 0x200);
                TransformAesCtrRegionInPlace(fs, ncchOffset + 0x200, 0x800, normalKey0, ctrExh, ct);
            }

            // 2. Расшифровка ExeFS
            uint exefsOffsetUnits = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x1A0, 4));
            uint exefsSizeUnits = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x1A4, 4));
            if (exefsOffsetUnits > 0 && exefsSizeUnits > 0)
            {
                long exefsOffset = ncchOffset + (long)exefsOffsetUnits * 0x200;
                long exefsSize = (long)exefsSizeUnits * 0x200;
                if (exefsOffset + exefsSize <= fs.Length && exefsSize >= 0x200)
                {
                    byte[] baseExefsCtr = BuildNcchCounter(partitionId, ncchVersion, sectionType: 2, exefsOffset);

                    // Сначала расшифровываем заголовок ExeFS (0x200 байт) ключом normalKey0
                    TransformAesCtrRegionInPlace(fs, exefsOffset, 0x200, normalKey0, (byte[])baseExefsCtr.Clone(), ct);

                    // Читаем таблицу файлов ExeFS, чтобы узнать, использует ли .code другой ключ (normalKey1)
                    fs.Seek(exefsOffset, SeekOrigin.Begin);
                    byte[] exefsHdr = reader.ReadBytes(0x200);

                    if ( NormalKeysEqual(normalKey0, normalKey1) )
                    {
                        byte[] dataCtr = AddCounterBlocks(baseExefsCtr, 0x200 / 16);
                        TransformAesCtrRegionInPlace(fs, exefsOffset + 0x200, exefsSize - 0x200, normalKey0, dataCtr, ct);
                    }
                    else
                    {
                        // Сначала расшифровываем всю область данных ключом normalKey0, а для .code применяем разницу ключей
                        byte[] dataCtr = AddCounterBlocks(baseExefsCtr, 0x200 / 16);
                        TransformAesCtrRegionInPlace(fs, exefsOffset + 0x200, exefsSize - 0x200, normalKey0, dataCtr, ct);

                        for (int i = 0; i < 10; i++)
                        {
                            string fname = Encoding.ASCII.GetString(exefsHdr, i * 16, 8).TrimEnd('\0');
                            uint fOff = BinaryPrimitives.ReadUInt32LittleEndian(exefsHdr.AsSpan(i * 16 + 8, 4));
                            uint fSize = BinaryPrimitives.ReadUInt32LittleEndian(exefsHdr.AsSpan(i * 16 + 12, 4));
                            if ((fname == ".code" || fname == "code") && fSize > 0)
                            {
                                long alignedSize = (fSize + 0x1FF) & ~0x1FFL;
                                long codePos = exefsOffset + 0x200 + fOff;
                                if (codePos + alignedSize <= exefsOffset + exefsSize)
                                {
                                    long blockOffset = (0x200 + fOff) / 16;
                                    // Возвращаем исходный шифротекст и расшифровываем ключом normalKey1
                                    TransformAesCtrRegionInPlace(fs, codePos, alignedSize, normalKey0, AddCounterBlocks(baseExefsCtr, blockOffset), ct);
                                    TransformAesCtrRegionInPlace(fs, codePos, alignedSize, normalKey1, AddCounterBlocks(baseExefsCtr, blockOffset), ct);
                                }
                            }
                        }
                    }
                }
            }

            // 3. Расшифровка RomFS
            uint romfsOffsetUnits = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x1B0, 4));
            uint romfsSizeUnits = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0x1B4, 4));
            if (romfsOffsetUnits > 0 && romfsSizeUnits > 0)
            {
                long romfsOffset = ncchOffset + (long)romfsOffsetUnits * 0x200;
                long romfsSize = (long)romfsSizeUnits * 0x200;
                if (romfsOffset + romfsSize <= fs.Length)
                {
                    byte[] ctrRomfs = BuildNcchCounter(partitionId, ncchVersion, sectionType: 3, romfsOffset);
                    TransformAesCtrRegionInPlace(fs, romfsOffset, romfsSize, normalKey1, ctrRomfs, ct);
                }
            }

            // 4. Обновляем флаги NCCH заголовка: снимаем Seed/FixedKey и выставляем NoCrypto (0x04)
            fs.Seek(ncchOffset + 0x18B, SeekOrigin.Begin);
            writer.Write((byte)0x00); // Flags[3] = 0x00
            fs.Seek(ncchOffset + 0x18F, SeekOrigin.Begin);
            byte newCryptoFlag = (byte)((cryptoFlag & ~0x21) | 0x04);
            writer.Write(newCryptoFlag);
        }

        private static bool NormalKeysEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }

        /// <summary>
        /// Вычисляет 128-битный NormalKey по аппаратному алгоритму скремблера Nintendo 3DS:
        /// NormalKey = ROL128((ROL128(KeyX, 2) ^ KeyY) + 0x1FF9E9AAC5FE0408024591DC5D52768A, 87)
        /// </summary>
        public static byte[] Derive3dsNormalKey(byte[] keyX, byte[] keyY)
        {
            BigInteger x = ToUInt128BigEndian(keyX);
            BigInteger y = ToUInt128BigEndian(keyY);
            BigInteger c = ToUInt128BigEndian(ScramblerConstant);

            BigInteger mask128 = (BigInteger.One << 128) - 1;
            BigInteger rolX2 = ((x << 2) | (x >> 126)) & mask128;
            BigInteger xorXY = rolX2 ^ y;
            BigInteger sum = (xorXY + c) & mask128;
            BigInteger rol87 = ((sum << 87) | (sum >> (128 - 87))) & mask128;

            return FromUInt128BigEndian(rol87);
        }

        private static BigInteger ToUInt128BigEndian(byte[] bytes16)
        {
            byte[] littleWithZeroSign = new byte[17];
            for (int i = 0; i < 16; i++)
            {
                littleWithZeroSign[i] = bytes16[15 - i];
            }
            littleWithZeroSign[16] = 0;
            return new BigInteger(littleWithZeroSign);
        }

        private static byte[] FromUInt128BigEndian(BigInteger val)
        {
            byte[] little = val.ToByteArray(isUnsigned: true, isBigEndian: true);
            byte[] result = new byte[16];
            int copyLen = Math.Min(16, little.Length);
            Array.Copy(little, little.Length - copyLen, result, 16 - copyLen, copyLen);
            return result;
        }

        private static byte[] BuildNcchCounter(ulong partitionId, ushort ncchVersion, byte sectionType, long sectionByteOffset)
        {
            byte[] counter = new byte[16];
            if (ncchVersion == 0 || ncchVersion == 2)
            {
                // PartitionId в обратном порядке байт (Big-Endian от Little-Endian uint64)
                BinaryPrimitives.WriteUInt64BigEndian(counter.AsSpan(0, 8), partitionId);
                counter[8] = sectionType;
            }
            else if (ncchVersion == 1)
            {
                BinaryPrimitives.WriteUInt64LittleEndian(counter.AsSpan(0, 8), partitionId);
                BinaryPrimitives.WriteUInt32BigEndian(counter.AsSpan(12, 4), (uint)sectionByteOffset);
            }
            return counter;
        }

        private static byte[] AddCounterBlocks(byte[] baseCounter, long blocksToAdd)
        {
            if (blocksToAdd <= 0) return (byte[])baseCounter.Clone();
            BigInteger ctrInt = ToUInt128BigEndian(baseCounter);
            BigInteger mask128 = (BigInteger.One << 128) - 1;
            BigInteger next = (ctrInt + blocksToAdd) & mask128;
            return FromUInt128BigEndian(next);
        }

        private static void IncrementCounterInPlace(byte[] counter)
        {
            for (int i = 15; i >= 0; i--)
            {
                if (++counter[i] != 0) break;
            }
        }

        private static void TransformAesCtrRegionInPlace(
            FileStream fs,
            long offset,
            long length,
            byte[] key16,
            byte[] initialCounter16,
            CancellationToken ct)
        {
            if (length <= 0) return;

            using var aes = Aes.Create();
            aes.Key = key16;
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            using var encryptor = aes.CreateEncryptor();

            const int chunkSize = 1024 * 1024; // 1 МБ буфер
            byte[] dataBuffer = new byte[chunkSize];
            byte[] counterBlockBuffer = new byte[chunkSize];
            byte[] keystreamBuffer = new byte[chunkSize];
            byte[] currentCounter = (byte[])initialCounter16.Clone();

            long remaining = length;
            long pos = offset;

            while (remaining > 0)
            {
                ct.ThrowIfCancellationRequested();
                int toRead = (int)Math.Min(chunkSize, remaining);
                fs.Seek(pos, SeekOrigin.Begin);
                int read = fs.Read(dataBuffer, 0, toRead);
                if (read <= 0) break;

                int alignedBlocks = (read + 15) / 16;
                int alignedBytes = alignedBlocks * 16;

                for (int b = 0; b < alignedBlocks; b++)
                {
                    Buffer.BlockCopy(currentCounter, 0, counterBlockBuffer, b * 16, 16);
                    IncrementCounterInPlace(currentCounter);
                }

                encryptor.TransformBlock(counterBlockBuffer, 0, alignedBytes, keystreamBuffer, 0);

                for (int i = 0; i < read; i++)
                {
                    dataBuffer[i] ^= keystreamBuffer[i];
                }

                fs.Seek(pos, SeekOrigin.Begin);
                fs.Write(dataBuffer, 0, read);

                pos += read;
                remaining -= read;
            }
        }

        private static void DecryptAesCbcRegionInPlace(
            FileStream fs,
            long offset,
            long length,
            byte[] key16,
            byte[] iv16,
            CancellationToken ct)
        {
            long alignedLength = length & ~0xFL;
            if (alignedLength <= 0) return;

            using var aes = Aes.Create();
            aes.Key = key16;
            aes.IV = (byte[])iv16.Clone();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;
            using var decryptor = aes.CreateDecryptor();

            const int chunkSize = 1024 * 1024;
            byte[] buffer = new byte[chunkSize];
            byte[] outBuffer = new byte[chunkSize];

            long remaining = alignedLength;
            long pos = offset;

            while (remaining > 0)
            {
                ct.ThrowIfCancellationRequested();
                int toRead = (int)Math.Min(chunkSize, remaining);
                toRead &= ~0xF;
                if (toRead <= 0) break;

                fs.Seek(pos, SeekOrigin.Begin);
                int read = fs.Read(buffer, 0, toRead);
                read &= ~0xF;
                if (read <= 0) break;

                decryptor.TransformBlock(buffer, 0, read, outBuffer, 0);

                fs.Seek(pos, SeekOrigin.Begin);
                fs.Write(outBuffer, 0, read);

                pos += read;
                remaining -= read;
            }
        }

        private static byte[] ComputeRegionSha256(FileStream fs, long offset, long length)
        {
            using var sha = SHA256.Create();
            const int chunkSize = 1024 * 1024;
            byte[] buffer = new byte[chunkSize];
            long remaining = length;
            long pos = offset;
            fs.Seek(pos, SeekOrigin.Begin);

            while (remaining > 0)
            {
                int toRead = (int)Math.Min(chunkSize, remaining);
                int read = fs.Read(buffer, 0, toRead);
                if (read <= 0) break;
                sha.TransformBlock(buffer, 0, read, null, 0);
                remaining -= read;
            }
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            return sha.Hash ?? new byte[32];
        }

        private static long LocatePrimaryNcchOffset(FileStream fs, BinaryReader reader, out bool ciaContentEncrypted)
        {
            ciaContentEncrypted = false;
            fs.Seek(0x100, SeekOrigin.Begin);
            string magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
            if (magic == "NCSD")
            {
                fs.Seek(0x120, SeekOrigin.Begin);
                uint part0Offset = reader.ReadUInt32();
                return (long)part0Offset * 0x200;
            }
            if (magic == "NCCH")
            {
                return 0;
            }

            // Проверка на CIA
            fs.Seek(0, SeekOrigin.Begin);
            uint headerSize = reader.ReadUInt32();
            reader.ReadUInt16(); // type
            reader.ReadUInt16(); // ver
            uint certSize = reader.ReadUInt32();
            uint ticketSize = reader.ReadUInt32();
            uint tmdSize = reader.ReadUInt32();

            if (headerSize >= 0x20 && headerSize <= 0x4000 && certSize > 0 && tmdSize > 0)
            {
                long tmdOffset = Align64(headerSize) + Align64(certSize) + Align64(ticketSize);
                long contentOffset = tmdOffset + Align64(tmdSize);
                if (tmdOffset + 0xB10 <= fs.Length)
                {
                    fs.Seek(tmdOffset + 0xB04 + 6, SeekOrigin.Begin);
                    ushort contentFlags = BinaryPrimitives.ReadUInt16BigEndian(reader.ReadBytes(2));
                    if ((contentFlags & 0x0001) != 0)
                    {
                        // Проверим, виден ли "NCCH" несмотря на флаг
                        if (contentOffset + 0x200 <= fs.Length)
                        {
                            fs.Seek(contentOffset + 0x100, SeekOrigin.Begin);
                            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "NCCH")
                            {
                                ciaContentEncrypted = true;
                            }
                        }
                    }
                }
                return contentOffset;
            }
            return -1;
        }

        private static long Align64(long val) => (val + 0x3F) & ~0x3FL;

        private static void EnsureSeedDbLoaded()
        {
            lock (_seedLock)
            {
                if (_seedCacheLoaded) return;
                _seedCacheLoaded = true;

                var candidatePaths = new List<string>
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "3ds", "seeddb.bin"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "seeddb.bin"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".3ds", "seeddb.bin"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Citra", "sysdata", "seeddb.bin"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lime3DS", "sysdata", "seeddb.bin"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Azahar", "sysdata", "seeddb.bin")
                };

                if (!string.IsNullOrEmpty(App.Settings.Current.KeysPath3ds))
                {
                    string? customDir = Path.GetDirectoryName(App.Settings.Current.KeysPath3ds);
                    if (!string.IsNullOrEmpty(customDir))
                    {
                        candidatePaths.Insert(0, Path.Combine(customDir, "seeddb.bin"));
                    }
                }

                foreach (var path in candidatePaths)
                {
                    if (File.Exists(path))
                    {
                        LoadSeedDbFile(path);
                    }
                }
            }
        }

        public static void LoadSeedDbFile(string path)
        {
            try
            {
                byte[] data = File.ReadAllBytes(path);
                if (data.Length < 16) return;
                uint count = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(0, 4));
                int maxEntries = (data.Length - 16) / 32;
                int total = (int)Math.Min(count, (uint)maxEntries);

                for (int i = 0; i < total; i++)
                {
                    int entryOffset = 16 + i * 32;
                    ulong titleId = BinaryPrimitives.ReadUInt64LittleEndian(data.AsSpan(entryOffset, 8));
                    byte[] seed = new byte[16];
                    Buffer.BlockCopy(data, entryOffset + 8, seed, 0, 16);
                    _seedCache[titleId] = seed;
                }
            }
            catch { }
        }

        private static byte[]? LookupSeed(ulong programId, byte[] expectedSeedCheck4)
        {
            lock (_seedLock)
            {
                if (_seedCache.TryGetValue(programId, out byte[]? seed))
                {
                    if (VerifySeedCheck(programId, seed, expectedSeedCheck4))
                        return seed;
                }

                // Для патчей (0004000E) иногда используется Seed от базовой игры (00040000)
                ulong baseId = (programId & 0x0000FFFFFFFFFFFFUL) | 0x0004000000000000UL;
                if (_seedCache.TryGetValue(baseId, out byte[]? baseSeed))
                {
                    if (VerifySeedCheck(programId, baseSeed, expectedSeedCheck4))
                        return baseSeed;
                }
            }
            return null;
        }

        private static bool VerifySeedCheck(ulong programId, byte[] seed16, byte[] expected4)
        {
            if (expected4 == null || expected4.Length < 4) return true;
            byte[] buf = new byte[24];
            Buffer.BlockCopy(seed16, 0, buf, 0, 16);
            BinaryPrimitives.WriteUInt64LittleEndian(buf.AsSpan(16, 8), programId);
            byte[] hash = SHA256.HashData(buf);
            return hash[0] == expected4[0] && hash[1] == expected4[1] && hash[2] == expected4[2] && hash[3] == expected4[3];
        }

        private static Dictionary<byte, byte[]> LoadCustomKeyXSlots()
        {
            var dict = new Dictionary<byte, byte[]>();
            try
            {
                var paths = new List<string>();
                if (!string.IsNullOrEmpty(App.Settings.Current.KeysPath3ds) && File.Exists(App.Settings.Current.KeysPath3ds))
                    paths.Add(App.Settings.Current.KeysPath3ds);
                paths.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "3ds", "aes_keys.txt"));
                paths.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".3ds", "aes_keys.txt"));

                foreach (var p in paths)
                {
                    if (!File.Exists(p)) continue;
                    foreach (var line in File.ReadAllLines(p))
                    {
                        string trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#")) continue;
                        int eq = trimmed.IndexOf('=');
                        if (eq <= 0) continue;
                        string k = trimmed.Substring(0, eq).Trim().ToLowerInvariant();
                        string v = trimmed.Substring(eq + 1).Trim();
                        if (v.Length != 32) continue;

                        if (k.StartsWith("slot0x") && k.Contains("keyx"))
                        {
                            string hexSlot = k.Substring(6, 2);
                            if (byte.TryParse(hexSlot, System.Globalization.NumberStyles.HexNumber, null, out byte slot))
                            {
                                dict[slot] = Convert.FromHexString(v);
                            }
                        }
                    }
                }
            }
            catch { }
            return dict;
        }
    }
}
