using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public class GraphicsPatchService
    {
        public GraphicsPatchService()
        {
        }

        /// <summary>
        /// Извлекает BuildID из NSO-файла main (хранится по смещению 0x40 в виде 32 байт SHA256 или 16/20/32 байт).
        /// </summary>
        public string? ExtractBuildId(string mainPath)
        {
            if (!File.Exists(mainPath)) return null;

            try
            {
                using var fs = new FileStream(mainPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (fs.Length < 0x60) return null;

                fs.Seek(0x40, SeekOrigin.Begin);
                byte[] buildIdBytes = new byte[32];
                int read = fs.Read(buildIdBytes, 0, 32);
                if (read >= 16)
                {
                    // Отрезаем завершающие нули
                    int validLen = 32;
                    while (validLen > 16 && buildIdBytes[validLen - 1] == 0) validLen--;

                    var sb = new StringBuilder(validLen * 2);
                    for (int i = 0; i < validLen; i++)
                    {
                        sb.Append(buildIdBytes[i].ToString("X2"));
                    }
                    return sb.ToString();
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Проверяет наличие внешних или встроенных патчей 60 FPS / твиков графики для TitleID и BuildID.
        /// При обнаружении применяет патч к targetExeFsDir или создает файл патча в exefs_patches.
        /// </summary>
        public bool TryInjectGraphicsPatches(string titleId, string targetExeFsDir, ProcessingTask? task)
        {
            if (string.IsNullOrEmpty(titleId) || !Directory.Exists(targetExeFsDir)) return false;

            string cleanTid = titleId.Trim().ToUpperInvariant();
            string toolsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "patches", cleanTid);

            string mainPath = Path.Combine(targetExeFsDir, "main");
            if (!File.Exists(mainPath))
            {
                mainPath = Directory.GetFiles(targetExeFsDir).FirstOrDefault(f => Path.GetFileName(f).StartsWith("main", StringComparison.OrdinalIgnoreCase) && !f.EndsWith(".npdm", StringComparison.OrdinalIgnoreCase)) ?? "";
            }
            if (!File.Exists(mainPath)) return false;

            string? currentBuildId = ExtractBuildId(mainPath);

            // 1. Поиск внешних IPS и PCHTXT патчей в папке tools/patches/{TitleId}/
            if (Directory.Exists(toolsDir))
            {
                bool appliedAny = false;
                var externalIps = Directory.GetFiles(toolsDir, "*.ips", SearchOption.AllDirectories);
                foreach (var ips in externalIps)
                {
                    string patchBaseName = Path.GetFileNameWithoutExtension(ips);
                    // Если имя файла патча является hex-идентификатором BuildID (16..64 hex-символа), проверяем точное совпадение с BuildID бинарника main
                    bool isBuildIdNamed = patchBaseName.Length >= 16 && patchBaseName.All(Uri.IsHexDigit);
                    if (isBuildIdNamed && (string.IsNullOrEmpty(currentBuildId) || !currentBuildId.StartsWith(patchBaseName, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    try
                    {
                        HardPatchEngine.ApplyIpsPatchToFile(ips, mainPath);
                        appliedAny = true;
                        App.RunOnUI(() =>
                        {
                            if (task != null)
                                task.LogDetails += $"\n⚡ [60 FPS и графика] Применен внешний IPS-патч: {Path.GetFileName(ips)}";
                        });
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Log($"[GraphicsPatch] Ошибка применения патча {ips}: {ex.Message}", LogLevel.Warning);
                    }
                }

                var externalPchtxt = Directory.GetFiles(toolsDir, "*.pchtxt", SearchOption.AllDirectories);
                foreach (var pchtxt in externalPchtxt)
                {
                    try
                    {
                        if (TryApplyPchtxtPatch(pchtxt, mainPath, currentBuildId))
                        {
                            appliedAny = true;
                            App.RunOnUI(() =>
                            {
                                if (task != null)
                                    task.LogDetails += $"\n⚡ [60 FPS и графика] Применен IPSwitch-патч: {Path.GetFileName(pchtxt)}";
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Log($"[GraphicsPatch] Ошибка применения pchtxt {pchtxt}: {ex.Message}", LogLevel.Warning);
                    }
                }

                if (appliedAny) return true;
            }

            return false;
        }

        private static bool TryApplyPchtxtPatch(string pchtxtPath, string mainBinaryPath, string? currentBuildId)
        {
            if (!File.Exists(pchtxtPath) || !File.Exists(mainBinaryPath)) return false;

            byte[] nsoBytes = File.ReadAllBytes(mainBinaryPath);
            if (nsoBytes.Length < 0x100 ||
                nsoBytes[0] != (byte)'N' || nsoBytes[1] != (byte)'S' || nsoBytes[2] != (byte)'O' || nsoBytes[3] != (byte)'0')
            {
                return false;
            }

            // Сверяем ModuleId (0x40..0x5F) с @nsobid- из pchtxt и с currentBuildId
            string moduleIdHex = Convert.ToHexString(nsoBytes.AsSpan(0x40, 32));
            string[] lines = File.ReadAllLines(pchtxtPath);
            foreach (var raw in lines)
            {
                string line = raw.Trim();
                if (line.StartsWith("@nsobid-", StringComparison.OrdinalIgnoreCase))
                {
                    string targetBid = line.Substring(8).Trim();
                    if (!string.IsNullOrEmpty(targetBid))
                    {
                        if (!moduleIdHex.StartsWith(targetBid, StringComparison.OrdinalIgnoreCase))
                            return false;
                        if (!string.IsNullOrEmpty(currentBuildId) && !currentBuildId.StartsWith(targetBid, StringComparison.OrdinalIgnoreCase))
                            return false;
                    }
                }
            }

            uint flags = BinaryPrimitives.ReadUInt32LittleEndian(nsoBytes.AsSpan(0x0C, 4));
            if ((flags & 0x07u) != 0)
            {
                nsoBytes = DecompressNso0Segments(nsoBytes);
                flags = BinaryPrimitives.ReadUInt32LittleEndian(nsoBytes.AsSpan(0x0C, 4));
                if ((flags & 0x07u) != 0)
                    return false;
            }

            bool enabledBlock = false;
            int appliedCount = 0;
            uint nsoHeaderShift = 0x100; // NSO0 header offset

            foreach (var raw in lines)
            {
                string line = raw.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("//") || line.StartsWith("#")) continue;

                if (line.StartsWith("@flag", StringComparison.OrdinalIgnoreCase))
                {
                    if (line.Contains("offset_shift", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 3)
                        {
                            string valStr = parts[2].Replace("0x", "", StringComparison.OrdinalIgnoreCase);
                            if (uint.TryParse(valStr, System.Globalization.NumberStyles.HexNumber, null, out uint parsedShift))
                                nsoHeaderShift = parsedShift;
                        }
                    }
                    continue;
                }
                if (line.Equals("@enabled", StringComparison.OrdinalIgnoreCase))
                {
                    enabledBlock = true;
                    continue;
                }
                if (line.Equals("@disabled", StringComparison.OrdinalIgnoreCase) || line.Equals("@stop", StringComparison.OrdinalIgnoreCase))
                {
                    enabledBlock = false;
                    if (line.Equals("@stop", StringComparison.OrdinalIgnoreCase)) break;
                    continue;
                }

                if (!enabledBlock) continue;

                var tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length >= 2 &&
                    uint.TryParse(tokens[0], System.Globalization.NumberStyles.HexNumber, null, out uint relOffset))
                {
                    string hexPayload = tokens[1];
                    if (hexPayload.Length % 2 == 0 && hexPayload.All(Uri.IsHexDigit))
                    {
                        byte[] patchBytes = Convert.FromHexString(hexPayload);
                        long fileOffset = (long)relOffset + nsoHeaderShift;
                        if (fileOffset >= 0x100 && fileOffset + patchBytes.Length <= nsoBytes.Length)
                        {
                            Buffer.BlockCopy(patchBytes, 0, nsoBytes, (int)fileOffset, patchBytes.Length);
                            appliedCount++;
                        }
                    }
                }
            }

            if (appliedCount == 0) return false;

            // Пересчитываем SHA-256 сегментов заголовка NSO0 (.text -> 0xA0, .rodata -> 0xC0, .data -> 0xE0)
            RecalculateNso0SegmentHashes(nsoBytes);
            File.WriteAllBytes(mainBinaryPath, nsoBytes);
            return true;
        }

        private static void RecalculateNso0SegmentHashes(byte[] nsoBytes)
        {
            (int SegHeaderOff, int HashOff)[] segments =
            {
                (0x10, 0xA0), // .text
                (0x20, 0xC0), // .rodata
                (0x30, 0xE0)  // .data
            };

            foreach (var (segOff, hashOff) in segments)
            {
                uint fileOff = BinaryPrimitives.ReadUInt32LittleEndian(nsoBytes.AsSpan(segOff, 4));
                uint size = BinaryPrimitives.ReadUInt32LittleEndian(nsoBytes.AsSpan(segOff + 8, 4));
                if (size > 0 && (long)fileOff + size <= nsoBytes.Length)
                {
                    byte[] hash = SHA256.HashData(nsoBytes.AsSpan((int)fileOff, (int)size));
                    Buffer.BlockCopy(hash, 0, nsoBytes, hashOff, 32);
                }
            }
        }

        private static byte[] DecompressNso0Segments(byte[] nso)
        {
            uint flags = BinaryPrimitives.ReadUInt32LittleEndian(nso.AsSpan(0x0C, 4));
            uint textFileOff = BinaryPrimitives.ReadUInt32LittleEndian(nso.AsSpan(0x10, 4));
            uint textMemOff = BinaryPrimitives.ReadUInt32LittleEndian(nso.AsSpan(0x14, 4));
            uint textSize = BinaryPrimitives.ReadUInt32LittleEndian(nso.AsSpan(0x18, 4));
            uint modNameOff = BinaryPrimitives.ReadUInt32LittleEndian(nso.AsSpan(0x1C, 4));

            uint roFileOff = BinaryPrimitives.ReadUInt32LittleEndian(nso.AsSpan(0x20, 4));
            uint roMemOff = BinaryPrimitives.ReadUInt32LittleEndian(nso.AsSpan(0x24, 4));
            uint roSize = BinaryPrimitives.ReadUInt32LittleEndian(nso.AsSpan(0x28, 4));
            uint modNameSize = BinaryPrimitives.ReadUInt32LittleEndian(nso.AsSpan(0x2C, 4));

            uint dataFileOff = BinaryPrimitives.ReadUInt32LittleEndian(nso.AsSpan(0x30, 4));
            uint dataMemOff = BinaryPrimitives.ReadUInt32LittleEndian(nso.AsSpan(0x34, 4));
            uint dataSize = BinaryPrimitives.ReadUInt32LittleEndian(nso.AsSpan(0x38, 4));

            uint textCompSize = BinaryPrimitives.ReadUInt32LittleEndian(nso.AsSpan(0x60, 4));
            uint roCompSize = BinaryPrimitives.ReadUInt32LittleEndian(nso.AsSpan(0x64, 4));
            uint dataCompSize = BinaryPrimitives.ReadUInt32LittleEndian(nso.AsSpan(0x68, 4));

            byte[] textRaw = (flags & 0x01u) != 0
                ? DecompressLz4Block(nso.AsSpan((int)textFileOff, (int)textCompSize), (int)textSize)
                : nso.AsSpan((int)textFileOff, (int)textSize).ToArray();
            byte[] roRaw = (flags & 0x02u) != 0
                ? DecompressLz4Block(nso.AsSpan((int)roFileOff, (int)roCompSize), (int)roSize)
                : nso.AsSpan((int)roFileOff, (int)roSize).ToArray();
            byte[] dataRaw = (flags & 0x04u) != 0
                ? DecompressLz4Block(nso.AsSpan((int)dataFileOff, (int)dataCompSize), (int)dataSize)
                : nso.AsSpan((int)dataFileOff, (int)dataSize).ToArray();

            uint newModNameOff = 0x100u;
            uint newTextOff = 0x100u + (modNameSize > 0 ? modNameSize : 0u);
            newTextOff = (newTextOff + 0xFFu) & ~0xFFu;
            if (modNameSize <= 1) newTextOff = 0x100u;

            uint newRoOff = roMemOff >= textMemOff
                ? newTextOff + (roMemOff - textMemOff)
                : newTextOff + textSize;
            if (newRoOff < newTextOff + textSize) newRoOff = newTextOff + textSize;

            uint newDataOff = dataMemOff >= roMemOff
                ? newRoOff + (dataMemOff - roMemOff)
                : newRoOff + roSize;
            if (newDataOff < newRoOff + roSize) newDataOff = newRoOff + roSize;

            uint totalLen = newDataOff + dataSize;
            byte[] outNso = new byte[totalLen];
            Buffer.BlockCopy(nso, 0, outNso, 0, 0x100);
            if (modNameSize > 0 && modNameOff >= 0x100 && modNameOff + modNameSize <= nso.Length && newModNameOff + modNameSize <= newTextOff)
            {
                Buffer.BlockCopy(nso, (int)modNameOff, outNso, (int)newModNameOff, (int)modNameSize);
                BinaryPrimitives.WriteUInt32LittleEndian(outNso.AsSpan(0x1C, 4), newModNameOff);
            }

            Buffer.BlockCopy(textRaw, 0, outNso, (int)newTextOff, textRaw.Length);
            Buffer.BlockCopy(roRaw, 0, outNso, (int)newRoOff, roRaw.Length);
            Buffer.BlockCopy(dataRaw, 0, outNso, (int)newDataOff, dataRaw.Length);

            BinaryPrimitives.WriteUInt32LittleEndian(outNso.AsSpan(0x0C, 4), flags & ~0x07u);
            BinaryPrimitives.WriteUInt32LittleEndian(outNso.AsSpan(0x10, 4), newTextOff);
            BinaryPrimitives.WriteUInt32LittleEndian(outNso.AsSpan(0x20, 4), newRoOff);
            BinaryPrimitives.WriteUInt32LittleEndian(outNso.AsSpan(0x30, 4), newDataOff);
            BinaryPrimitives.WriteUInt32LittleEndian(outNso.AsSpan(0x60, 4), textSize);
            BinaryPrimitives.WriteUInt32LittleEndian(outNso.AsSpan(0x64, 4), roSize);
            BinaryPrimitives.WriteUInt32LittleEndian(outNso.AsSpan(0x68, 4), dataSize);

            return outNso;
        }

        private static byte[] DecompressLz4Block(ReadOnlySpan<byte> src, int expectedLen)
        {
            byte[] dst = new byte[expectedLen];
            int s = 0, d = 0;

            while (s < src.Length)
            {
                byte token = src[s++];
                int litLen = token >> 4;
                if (litLen == 15)
                {
                    while (s < src.Length)
                    {
                        byte b = src[s++];
                        litLen += b;
                        if (b != 255) break;
                    }
                }

                if (litLen > 0)
                {
                    if (s + litLen > src.Length || d + litLen > dst.Length)
                        throw new InvalidDataException("Invalid LZ4 literal length in NSO0 segment.");
                    src.Slice(s, litLen).CopyTo(dst.AsSpan(d));
                    s += litLen;
                    d += litLen;
                }

                if (s >= src.Length) break;
                if (s + 2 > src.Length) throw new InvalidDataException("Truncated LZ4 match offset.");

                int matchOffset = src[s] | (src[s + 1] << 8);
                s += 2;
                if (matchOffset == 0 || matchOffset > d)
                    throw new InvalidDataException("Invalid LZ4 match offset in NSO0 segment.");

                int matchLen = (token & 0x0F) + 4;
                if ((token & 0x0F) == 15)
                {
                    while (s < src.Length)
                    {
                        byte b = src[s++];
                        matchLen += b;
                        if (b != 255) break;
                    }
                }

                for (int i = 0; i < matchLen; i++)
                {
                    if (d >= dst.Length) throw new InvalidDataException("LZ4 match exceeds decompressed NSO0 segment size.");
                    dst[d] = dst[d - matchOffset];
                    d++;
                }
            }

            return dst;
        }
    }
}
