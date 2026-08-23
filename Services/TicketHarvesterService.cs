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

            bool isXci = filePath.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase);

            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            IStorage storage = fileStream.AsStorage();
            
            PartitionFileSystem? pfs = null;

            if (isXci)
            {
                storage.GetSize(out long storageSize).ThrowIfFailure();
                var rootStorage = new SubStorage(storage, 0x10000, storageSize - 0x10000);
                var rootPfs = new PartitionFileSystem(rootStorage);
                
                
                using var secureFile = new UniqueRef<IFile>();
                using var securePath = new LibHac.Fs.Path();
                securePath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes("/secure"))).ThrowIfFailure();
                rootPfs.OpenFile(ref secureFile.Ref, in securePath, OpenMode.Read).ThrowIfFailure();
                
                pfs = new PartitionFileSystem(secureFile.Release().AsStorage());
                
            }
            else
            {
                pfs = new PartitionFileSystem(storage);
                
            }

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
                    
                    IFile tikFile = tikFileRef.Release();
                    byte[] tikBytes = new byte[0x300];
                    tikFile.Read(out long bytesRead, 0, tikBytes).ThrowIfFailure();

                    if (bytesRead >= 0x100)
                    {
                        var ticketInfo = ExtractDecryptedTicket(tikBytes, (int)bytesRead, App.Keys.CurrentKeyset);
                        if (ticketInfo.HasValue && !string.IsNullOrEmpty(ticketInfo.Value.RightsId) && ticketInfo.Value.TitleKey != null && ticketInfo.Value.TitleKey.Length == 16)
                        {
                            string tKeyHex = BitConverter.ToString(ticketInfo.Value.TitleKey).Replace("-", "").ToLowerInvariant();
                            keysToAdd.Add($"{ticketInfo.Value.RightsId} = {tKeyHex}");
                            lock (Core.NSZ.StormNczCompressor.TitleKeysCache)
                            {
                                Core.NSZ.StormNczCompressor.TitleKeysCache[ticketInfo.Value.RightsId] = ticketInfo.Value.TitleKey;
                            }
                        }
                    }
                }

                if (keysToAdd.Count > 0)
                {
                    lock (_lockObj)
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
                                Directory.CreateDirectory(Path.GetDirectoryName(titleKeysPath)!);
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
                                if (App.Keys.KeysFilePath != null && File.Exists(App.Keys.KeysFilePath))
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

        /// <summary>
        /// Извлекает и расшифровывает TitleKey из билета Switch с использованием TitleKek.
        /// </summary>
        public static (string RightsId, byte[] TitleKey)? ExtractDecryptedTicket(byte[] tikBytes, int length, KeySet keyset)
        {
            if (tikBytes == null || length < 0x100) return null;

            string rId = string.Empty;
            byte[]? encKeyBytes = null;
            byte[]? rIdBytes = null;

            try
            {
                using var ms = new MemoryStream(tikBytes, 0, length);
                var ticket = new LibHac.Tools.Es.Ticket(ms);
                rIdBytes = ticket.RightsId;
                if (rIdBytes != null && rIdBytes.Length == 16)
                {
                    rId = BitConverter.ToString(rIdBytes).Replace("-", "").ToLowerInvariant();
                }
                encKeyBytes = ticket.TitleKeyBlock;
            }
            catch { }

            if (encKeyBytes == null || encKeyBytes.Length < 16 || string.IsNullOrEmpty(rId) || IsAllZero(rId))
            {
                var rawInfo = ExtractTicketInfo(tikBytes, length);
                if (rawInfo.HasValue && !string.IsNullOrEmpty(rawInfo.Value.RightsId))
                {
                    rId = rawInfo.Value.RightsId;
                    try { rIdBytes = Convert.FromHexString(rId); } catch { }
                    try { encKeyBytes = Convert.FromHexString(rawInfo.Value.TitleKey); } catch { }
                }
            }

            if (encKeyBytes == null || encKeyBytes.Length < 16 || rIdBytes == null || rIdBytes.Length < 16 || string.IsNullOrEmpty(rId) || IsAllZero(rId))
            {
                return null;
            }

            byte[] decryptedKey = new byte[16];
            Array.Copy(encKeyBytes, 0, decryptedKey, 0, 16);

            try
            {
                int masterKeyRev = Math.Max(0, (int)rIdBytes[15] - 1);
                if (keyset != null && keyset.TitleKeks != null)
                {
                    if (masterKeyRev >= keyset.TitleKeks.Length || keyset.TitleKeks[masterKeyRev].DataRo.Length < 16 || (keyset.TitleKeks[masterKeyRev].DataRo[0] == 0 && keyset.TitleKeks[masterKeyRev].DataRo[1] == 0))
                    {
                        masterKeyRev = 0;
                    }

                    if (masterKeyRev < keyset.TitleKeks.Length && keyset.TitleKeks[masterKeyRev].DataRo.Length >= 16)
                    {
                        byte[] titleKek = keyset.TitleKeks[masterKeyRev].DataRo.ToArray();
                        if (titleKek != null && titleKek.Length == 16 && (titleKek[0] != 0 || titleKek[1] != 0))
                        {
                            using var aes = System.Security.Cryptography.Aes.Create();
                            aes.Mode = System.Security.Cryptography.CipherMode.ECB;
                            aes.Padding = System.Security.Cryptography.PaddingMode.None;
                            aes.Key = titleKek;
                            using var dec = aes.CreateDecryptor();
                            dec.TransformBlock(encKeyBytes, 0, 16, decryptedKey, 0);
                        }
                    }
                }
            }
            catch { }

            return (rId, decryptedKey);
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
                string rId = BitConverter.ToString(ticket.RightsId).Replace("-", "").ToLowerInvariant();
                byte[] tKeyBytes = ticket.TitleKeyBlock;
                if (tKeyBytes != null && tKeyBytes.Length >= 16)
                {
                    string tKey = BitConverter.ToString(tKeyBytes, 0, 16).Replace("-", "").ToLowerInvariant();
                    if (!IsAllZero(tKey) && !IsAllZero(rId))
                    {
                        return (rId, tKey);
                    }
                }
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
                byte[] tKeyBytes = new byte[16];
                Array.Copy(tikBytes, bodyOffset + 0x40, tKeyBytes, 0, 16);
                
                byte[] rIdBytes = new byte[16];
                Array.Copy(tikBytes, bodyOffset + 0x160, rIdBytes, 0, 16);

                string tKey = BitConverter.ToString(tKeyBytes).Replace("-", "").ToLowerInvariant();
                string rId = BitConverter.ToString(rIdBytes).Replace("-", "").ToLowerInvariant();

                if (!IsAllZero(tKey) && !IsAllZero(rId))
                {
                    return (rId, tKey);
                }
            }

            // 3. Эвристический глубокий поиск (Deep Scanner)
            // Ищем 16-байтный паттерн RightsId: 8 байт TitleID (начинается с 0100...) + 8 байт Key Generation/Padding
            for (int offset = 0; offset <= length - 16; offset++)
            {
                // Проверяем префикс TitleID в Big-Endian (0100...)
                if (tikBytes[offset] == 0x01 && tikBytes[offset + 1] == 0x00)
                {
                    byte[] rIdBytes = new byte[16];
                    Array.Copy(tikBytes, offset, rIdBytes, 0, 16);
                    string rId = BitConverter.ToString(rIdBytes).Replace("-", "").ToLowerInvariant();

                    // TitleKey обычно расположен на 0x120 байт перед RightsId (в стандартном теле: 0x40 против 0x160)
                    int expectedKeyOffset = offset - 0x120;
                    if (expectedKeyOffset >= 0 && expectedKeyOffset + 16 <= length)
                    {
                        byte[] tKeyBytes = new byte[16];
                        Array.Copy(tikBytes, expectedKeyOffset, tKeyBytes, 0, 16);
                        string tKey = BitConverter.ToString(tKeyBytes).Replace("-", "").ToLowerInvariant();

                        if (!IsAllZero(tKey) && !IsAllZero(rId))
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
