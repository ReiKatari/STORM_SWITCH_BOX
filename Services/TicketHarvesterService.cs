using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using Path = System.IO.Path;

namespace StormSwitchBox.Services
{
    public class TicketHarvesterService
    {
        private readonly HashSet<string> _processedFiles = new();
        private readonly object _lockObj = new();

        public void HarvestTicketsBackground(IEnumerable<string> filePaths)
        {
            var filesToProcess = filePaths.Where(f => !Directory.Exists(f) && (f.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase))).ToList();

            if (filesToProcess.Count == 0) return;

            foreach (var file in filesToProcess)
            {
                lock (_lockObj)
                {
                    if (_processedFiles.Contains(file)) continue;
                    _processedFiles.Add(file);
                }

                try
                {
                    HarvestFromFile(file);
                }
                catch (Exception ex)
                {
                    App.Logger.Log($"[Ticket Harvester] Ошибка извлечения билетов из {Path.GetFileName(file)}: {ex.Message}", Models.LogLevel.Warning);
                }
            }
        }

        private void HarvestFromFile(string filePath)
        {
            string titleKeysPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "title.keys");

            using var container = SwitchContainerHandle.Open(filePath);
            var pfs = container.FileSystem;

            var entries = pfs.EnumerateEntries().Where(e => e.Name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase)).ToList();

            if (entries.Count > 0)
            {
                var keysToAdd = new List<string>();

                foreach (var entry in entries)
                {
                    using var tikFileRef = new UniqueRef<IFile>();
                    using var entryPath = new LibHac.Fs.Path();
                    entryPath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(entry.FullPath))).ThrowIfFailure();
                    pfs.OpenFile(ref tikFileRef.Ref, in entryPath, OpenMode.Read).ThrowIfFailure();

                    using IFile tikFile = tikFileRef.Release();
                    tikFile.GetSize(out long actualSize).ThrowIfFailure();
                    long targetSize = Math.Max(actualSize, entry.Size);
                    if (targetSize < 0x100) continue;

                    int readLen = (int)Math.Min(targetSize, 1024 * 1024);
                    byte[] tikBytes = new byte[readLen];
                    long totalRead = 0;
                    while (totalRead < readLen)
                    {
                        tikFile.Read(out long chunkRead, totalRead, tikBytes.AsSpan((int)totalRead, readLen - (int)totalRead)).ThrowIfFailure();
                        if (chunkRead <= 0) break;
                        totalRead += chunkRead;
                    }

                    if (totalRead >= 0x100)
                    {
                        // В title.keys и ExternalKeySet (для LibHac / hactoolnet / yanu-cli) обязан храниться сырой TitleKeyBlock из .tik (зашифрованный TitleKek),
                        // так как LibHac и hactoolnet всегда расшифровывают ключ из title.keys через TitleKek!
                        var rawInfo = ExtractTicketInfo(tikBytes, (int)totalRead);
                        if (rawInfo.HasValue && !string.IsNullOrEmpty(rawInfo.Value.RightsId) && !string.IsNullOrEmpty(rawInfo.Value.TitleKey))
                        {
                            string rIdNorm = rawInfo.Value.RightsId.Trim().ToLowerInvariant();
                            string rawKeyHex = rawInfo.Value.TitleKey.Trim().ToLowerInvariant();
                            keysToAdd.Add($"{rIdNorm} = {rawKeyHex}");
                            try
                            {
                                var keyset = App.Keys?.CurrentKeyset;
                                if (keyset?.ExternalKeySet != null)
                                {
                                    byte[] ridB = Convert.FromHexString(rIdNorm);
                                    byte[] keyB = Convert.FromHexString(rawKeyHex);
                                    var rIdObj = new LibHac.Fs.RightsId(ridB);
                                    var aKeyObj = new LibHac.Spl.AccessKey(keyB);
                                    KeysService.TryAddExternalKey(keyset, rIdObj, aKeyObj);
                                }
                            }
                            catch { }
                        }

                        var currentKeyset = App.Keys?.CurrentKeyset;
                        if (currentKeyset != null)
                        {
                            var decTicketInfo = ExtractDecryptedTicket(tikBytes, (int)totalRead, currentKeyset);
                            if (decTicketInfo.HasValue && !string.IsNullOrEmpty(decTicketInfo.Value.RightsId) && decTicketInfo.Value.TitleKey != null && decTicketInfo.Value.TitleKey.Length == 16)
                            {
                                lock (Core.NSZ.StormNczCompressor.TitleKeysCache)
                                {
                                    Core.NSZ.StormNczCompressor.TitleKeysCache[decTicketInfo.Value.RightsId] = decTicketInfo.Value.TitleKey;
                                }
                            }
                        }
                    }
                }

                if (keysToAdd.Count > 0)
                {
                    lock (KeysService.TitleKeysFileLock)
                    {
                        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        try
                        {
                            if (File.Exists(titleKeysPath))
                            {
                                var lines = File.ReadAllLines(titleKeysPath);
                                foreach (var l in lines)
                                {
                                    var parts = l.Split('=');
                                    if (parts.Length == 2)
                                    {
                                        dict[parts[0].Trim().ToLowerInvariant()] = parts[1].Trim().ToLowerInvariant();
                                    }
                                }
                            }
                            else
                            {
                                string? tkDir = Path.GetDirectoryName(titleKeysPath);
                                if (!string.IsNullOrEmpty(tkDir))
                                    Directory.CreateDirectory(tkDir);
                            }
                        }
                        catch { }

                        bool changed = false;
                        int newCount = 0;
                        foreach (var keyLine in keysToAdd)
                        {
                            var parts = keyLine.Split('=');
                            if (parts.Length == 2)
                            {
                                string rid = parts[0].Trim().ToLowerInvariant();
                                string val = parts[1].Trim().ToLowerInvariant();
                                if (!dict.TryGetValue(rid, out var existingVal) || existingVal != val)
                                {
                                    dict[rid] = val;
                                    changed = true;
                                    newCount++;
                                }
                            }
                        }

                        if (changed)
                        {
                            var outputLines = dict.Select(kv => $"{kv.Key} = {kv.Value}").ToList();
                            File.WriteAllLines(titleKeysPath, outputLines);
                            App.Logger.Log($"[Ticket Harvester] Найдено и обновлено {newCount} билетов из {Path.GetFileName(filePath)}", Models.LogLevel.Success);
                            App.RunOnUI(() =>
                            {
                                if (App.Keys?.KeysFilePath != null && File.Exists(App.Keys.KeysFilePath))
                                {
                                    App.Keys.LoadKeys(App.Keys.KeysFilePath);
                                    App.Logger.Log("[Ticket Harvester] Ключи LibHac были автоматически обновлены.", Models.LogLevel.Info);
                                }
                            });
                        }
                    }
                }
            }
        }

        private static bool IsValidSwitchRightsIdBytes(ReadOnlySpan<byte> rIdBytes)
        {
            if (rIdBytes.Length < 16) return false;
            bool allZero = true;
            for (int i = 0; i < 16; i++)
            {
                if (rIdBytes[i] != 0) { allZero = false; break; }
            }
            if (allZero) return false;
            for (int i = 8; i < 14; i++)
            {
                if (rIdBytes[i] != 0) return false;
            }
            return true;
        }

        /// <summary>
        /// Извлекает и расшифровывает TitleKey из билета Switch с использованием TitleKek.
        /// При отсутствии TitleKek возвращает null, не подставляя сырой зашифрованный ключ.
        /// </summary>
        public static (string RightsId, byte[] TitleKey)? ExtractDecryptedTicket(byte[] tikBytes, int length, KeySet? keyset)
        {
            if (tikBytes == null || length < 0x100 || keyset == null) return null;

            string rId = string.Empty;
            byte[]? encKeyBytes = null;
            byte[]? rIdBytes = null;
            bool parsedValidTicketHeader = false;

            try
            {
                using var ms = new MemoryStream(tikBytes, 0, length);
                var ticket = new LibHac.Tools.Es.Ticket(ms);
                parsedValidTicketHeader = true;
                if ((int)ticket.TitleKeyType != 0)
                    return null;

                rIdBytes = ticket.RightsId;
                if (rIdBytes != null && rIdBytes.Length == 16 && IsValidSwitchRightsIdBytes(rIdBytes))
                {
                    rId = BitConverter.ToString(rIdBytes).Replace("-", "").ToLowerInvariant();
                }
                encKeyBytes = ticket.TitleKeyBlock;
            }
            catch { }

            if (!parsedValidTicketHeader && (encKeyBytes == null || encKeyBytes.Length < 16 || string.IsNullOrEmpty(rId)))
            {
                var rawInfo = ExtractTicketInfo(tikBytes, length);
                if (rawInfo.HasValue && !string.IsNullOrEmpty(rawInfo.Value.RightsId))
                {
                    rId = rawInfo.Value.RightsId;
                    try { rIdBytes = Convert.FromHexString(rId); } catch { }
                    try { encKeyBytes = Convert.FromHexString(rawInfo.Value.TitleKey); } catch { }
                }
            }

            if (encKeyBytes == null || encKeyBytes.Length < 16 || rIdBytes == null || rIdBytes.Length < 16 ||
                string.IsNullOrEmpty(rId) || !IsValidSwitchRightsIdBytes(rIdBytes))
            {
                return null;
            }

            try
            {
                int masterKeyRev = Math.Max(0, (int)rIdBytes[15] - 1);
                byte[]? titleKek = null;
                if (keyset.TitleKeks != null && masterKeyRev < keyset.TitleKeks.Length && keyset.TitleKeks[masterKeyRev].DataRo.Length >= 16)
                {
                    var tkCandidate = keyset.TitleKeks[masterKeyRev].DataRo.ToArray();
                    if (tkCandidate != null && tkCandidate.Length == 16 && (tkCandidate[0] != 0 || tkCandidate[1] != 0))
                    {
                        titleKek = tkCandidate;
                    }
                }

                if (titleKek == null && keyset.MasterKeys != null && masterKeyRev < keyset.MasterKeys.Length && keyset.MasterKeys[masterKeyRev].DataRo.Length >= 16)
                {
                    byte[] mKey = keyset.MasterKeys[masterKeyRev].DataRo.ToArray();
                    if (mKey != null && mKey.Length == 16 && (mKey[0] != 0 || mKey[1] != 0))
                    {
                        byte[] titleKekSource = Convert.FromHexString("1EDC7B3B60E6B4D878B81715985E629B");
                        using var aesKek = System.Security.Cryptography.Aes.Create();
                        aesKek.Mode = System.Security.Cryptography.CipherMode.ECB;
                        aesKek.Padding = System.Security.Cryptography.PaddingMode.None;
                        aesKek.Key = mKey;
                        using var decKek = aesKek.CreateDecryptor();
                        titleKek = decKek.TransformFinalBlock(titleKekSource, 0, 16);
                    }
                }

                if (titleKek != null && titleKek.Length == 16 && (titleKek[0] != 0 || titleKek[1] != 0))
                {
                    byte[] decryptedKey = new byte[16];
                    using var aes = System.Security.Cryptography.Aes.Create();
                    aes.Mode = System.Security.Cryptography.CipherMode.ECB;
                    aes.Padding = System.Security.Cryptography.PaddingMode.None;
                    aes.Key = titleKek;
                    using var dec = aes.CreateDecryptor();
                    dec.TransformBlock(encKeyBytes, 0, 16, decryptedKey, 0);
                    return (rId, decryptedKey);
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Глубокий анализатор билетов Switch (Ticket Harvester / Deep Scanner).
        /// Поддерживает стандартные форматы RSA-2048, RSA-4096, ECDSA, а также бинарный эвристический поиск
        /// для повреждённых или нестандартных дампов.
        /// </summary>
        public static (string RightsId, string TitleKey)? ExtractTicketInfo(byte[] tikBytes, int length)
        {
            if (tikBytes == null || length < 0x100) return null;

            // 1. Попытка через LibHac.Tools.Es.Ticket
            try
            {
                using var ms = new MemoryStream(tikBytes, 0, length);
                var ticket = new LibHac.Tools.Es.Ticket(ms);
                if ((int)ticket.TitleKeyType != 0)
                    return null;

                byte[]? rIdBytes = ticket.RightsId;
                if (rIdBytes == null || rIdBytes.Length < 16 || !IsValidSwitchRightsIdBytes(rIdBytes))
                    return null;

                string rId = BitConverter.ToString(rIdBytes).Replace("-", "").ToLowerInvariant();
                byte[] tKeyBytes = ticket.TitleKeyBlock;
                if (tKeyBytes != null && tKeyBytes.Length >= 16)
                {
                    string tKey = BitConverter.ToString(tKeyBytes, 0, 16).Replace("-", "").ToLowerInvariant();
                    if (!IsAllZero(tKey))
                    {
                        return (rId, tKey);
                    }
                }
                return null;
            }
            catch { }

            // 2. Определение смещения тела билета по типу цифровой подписи (Signature Type)
            int bodyOffset = -1;
            if (length >= 4)
            {
                uint sigType = BitConverter.ToUInt32(tikBytes, 0);
                switch (sigType)
                {
                    case 0x10000: // RSA-4096
                    case 0x10003:
                        bodyOffset = 0x240;
                        break;
                    case 0x10001: // RSA-2048
                    case 0x10004:
                        bodyOffset = 0x140;
                        break;
                    case 0x10002: // ECDSA
                    case 0x10005:
                        bodyOffset = 0x80;
                        break;
                }
            }

            if (bodyOffset > 0 && length >= bodyOffset + 0x170)
            {
                // TitleKeyType == 0 (Common) at bodyOffset + 0x141
                if (tikBytes[bodyOffset + 0x141] != 0)
                    return null;

                byte[] tKeyBytes = new byte[16];
                Array.Copy(tikBytes, bodyOffset + 0x40, tKeyBytes, 0, 16);

                byte[] rIdBytes = new byte[16];
                Array.Copy(tikBytes, bodyOffset + 0x160, rIdBytes, 0, 16);

                if (!IsValidSwitchRightsIdBytes(rIdBytes))
                    return null;

                string tKey = BitConverter.ToString(tKeyBytes).Replace("-", "").ToLowerInvariant();
                string rId = BitConverter.ToString(rIdBytes).Replace("-", "").ToLowerInvariant();

                if (!IsAllZero(tKey))
                {
                    return (rId, tKey);
                }
                return null;
            }

            // 3. Эвристический глубокий поиск (Deep Scanner)
            // Ищем 16-байтный паттерн RightsId: 8 байт TitleID (начинается с 0100...) + 6 нулевых байт [8..14] + KeyGeneration
            for (int offset = 0; offset <= length - 16; offset++)
            {
                if (tikBytes[offset] == 0x01 && tikBytes[offset + 1] == 0x00)
                {
                    byte[] rIdBytes = new byte[16];
                    Array.Copy(tikBytes, offset, rIdBytes, 0, 16);
                    if (!IsValidSwitchRightsIdBytes(rIdBytes))
                        continue;

                    // Если заголовок помещается перед RightsId, проверяем TitleKeyType == 0 (body + 0x141, т.е. offset - 0x1F)
                    if (offset >= 0x1F && tikBytes[offset - 0x1F] != 0)
                        continue;

                    string rId = BitConverter.ToString(rIdBytes).Replace("-", "").ToLowerInvariant();

                    // TitleKey обычно расположен на 0x120 байт перед RightsId (в стандартном теле: 0x40 против 0x160)
                    int expectedKeyOffset = offset - 0x120;
                    if (expectedKeyOffset >= 0 && expectedKeyOffset + 16 <= length)
                    {
                        byte[] tKeyBytes = new byte[16];
                        Array.Copy(tikBytes, expectedKeyOffset, tKeyBytes, 0, 16);
                        string tKey = BitConverter.ToString(tKeyBytes).Replace("-", "").ToLowerInvariant();

                        if (!IsAllZero(tKey))
                        {
                            return (rId, tKey);
                        }
                    }
                }
            }

            return null;
        }

        private static bool IsAllZero(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return true;
            foreach (char c in hex)
            {
                if (c != '0') return false;
            }
            return true;
        }
    }
}
