using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LibHac.Common.Keys;
using LibHac.Tools.FsSystem.NcaUtils;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public class KeysService
    {
        private static readonly SemaphoreSlim _semaphore = new(1, 1);

        private static readonly HashSet<LibHac.Fs.RightsId> _fallbackRightsIds = new();
        private static readonly object _fallbackLock = new();

        public static bool IsFallbackRightsId(LibHac.Fs.RightsId rId)
        {
            lock (_fallbackLock) return _fallbackRightsIds.Contains(rId);
        }

        public static void RemoveFallbackRightsId(LibHac.Fs.RightsId rId)
        {
            lock (_fallbackLock) _fallbackRightsIds.Remove(rId);
        }

        /// <summary>
        /// Eden TitleKey fallback: Registers RightsId as TitleKey into ExternalKeySet for direct dumps without tickets/external keys.
        /// Only registers fallback if no real ticket key already exists for this RightsId.
        /// </summary>
        public static void RegisterRightsIdFallback(KeySet? keyset, ReadOnlySpan<byte> rIdBytes)
        {
            if (keyset?.ExternalKeySet == null || rIdBytes.Length < 16) return;
            try
            {
                bool isZero = true;
                for (int i = 0; i < 16; i++)
                {
                    if (rIdBytes[i] != 0) { isZero = false; break; }
                }
                if (isZero) return;

                var rId = new LibHac.Fs.RightsId(rIdBytes.Slice(0, 16));
                if (keyset.ExternalKeySet.Contains(rId)) return;

                var aKey = new LibHac.Spl.AccessKey(rIdBytes.Slice(0, 16));
                keyset.ExternalKeySet.Add(rId, aKey);
                lock (_fallbackLock)
                {
                    _fallbackRightsIds.Add(rId);
                }
            }
            catch { }
        }

        public static void RegisterRightsIdFallback(KeySet? keyset, LibHac.Fs.RightsId rId)
        {
            if (keyset?.ExternalKeySet == null) return;
            try
            {
                if (keyset.ExternalKeySet.Contains(rId)) return;

                var aKey = new LibHac.Spl.AccessKey(rId.Value);
                keyset.ExternalKeySet.Add(rId, aKey);
                lock (_fallbackLock)
                {
                    _fallbackRightsIds.Add(rId);
                }
            }
            catch { }
        }

        public static void RegisterRightsIdFallback(KeySet? keyset, Nca? nca)
        {
            if (keyset == null || nca == null || !nca.Header.HasRightsId) return;
            RegisterRightsIdFallback(keyset, (ReadOnlySpan<byte>)nca.Header.RightsId);
        }

        public KeySet CurrentKeyset { get; set; }
        public bool IsLoaded { get; private set; }
        public string? KeysFilePath { get; private set; }
        public int MaxMasterKeyRevision { get; private set; }
        public string MaxSupportedFirmware { get; private set; } = "Неизвестно";

        public KeysService()
        {
            CurrentKeyset = new KeySet();
        }

        public static string GetFirmwareByMasterKeyRevision(int revision) => revision switch
        {
            >= 0x16 => "23.0.0 - 23.0.1 (Atmosphere 1.12.1)",
            0x15 => "22.1.0 (Atmosphere 1.12.0)",
            0x14 => "22.0.0 (Atmosphere 1.11.x)",
            0x13 => "20.0.0 - 21.0.0",
            0x12 => "19.0.0 - 19.0.1",
            0x11 => "18.0.0 - 18.1.0",
            0x10 => "17.0.0 - 17.0.1",
            0x0F => "16.0.0 - 16.1.0",
            0x0E => "15.0.0 - 15.0.1",
            0x0D => "14.0.0 - 14.1.2",
            0x0C => "13.0.0 - 13.2.1",
            0x0B => "12.1.0",
            0x0A => "11.0.0 - 12.0.3",
            0x09 => "9.1.0 - 10.2.0",
            0x08 => "9.0.0 - 9.0.1",
            0x07 => "8.1.0",
            0x06 => "7.0.0 - 8.0.1",
            _ => "1.0.0 - 6.2.0"
        };

        public bool LoadKeys(string keysPath)
        {
            if (!File.Exists(keysPath))
            {
                App.Logger.Log($"Файл ключей не найден: {keysPath}", LogLevel.Error);
                return false;
            }

            try
            {
                App.Logger.Log("Загрузка криптографических ключей (prod.keys)...", LogLevel.Info);

                string? titleKeysPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(keysPath) ?? "", "title.keys");
                if (!File.Exists(titleKeysPath))
                {
                    titleKeysPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "title.keys");
                }
                if (!File.Exists(titleKeysPath)) titleKeysPath = null;
                
                CurrentKeyset = ExternalKeyReader.ReadKeyFile(keysPath, titleKeysPath);
                CurrentKeyset.DeriveKeys();
                IsLoaded = true;
                KeysFilePath = keysPath;

                // Определяем максимальную ревизию master_key_XX (включая прошивки 22.0.0 - 23.0.1 / Atmosphere 1.12.1)
                int maxRev = 0;
                try
                {
                    foreach (var line in File.ReadLines(keysPath))
                    {
                        string trimmed = line.Trim();
                        if (trimmed.StartsWith("master_key_", StringComparison.OrdinalIgnoreCase))
                        {
                            int eqIdx = trimmed.IndexOf('=');
                            if (eqIdx > 11)
                            {
                                string hexRev = trimmed.Substring(11, eqIdx - 11).Trim();
                                if (int.TryParse(hexRev, System.Globalization.NumberStyles.HexNumber, null, out int rev) && rev > maxRev)
                                {
                                    maxRev = rev;
                                }
                            }
                        }
                    }
                }
                catch { }
                MaxMasterKeyRevision = maxRev;
                MaxSupportedFirmware = GetFirmwareByMasterKeyRevision(maxRev);
                
                App.Logger.Log($"Ключи успешно загружены! Макс. master_key_{maxRev:X2} (Поддержка HOS до {MaxSupportedFirmware})", LogLevel.Success);
                return true;
            }
            catch (Exception ex)
            {
                IsLoaded = false;
                App.Logger.Log($"Ошибка при загрузке ключей: {ex.Message}", LogLevel.Error);
                return false;
            }
        }

        public bool EnsureLoadedWithAutoDiscovery(string? contextFilePath = null)
        {
            if (IsLoaded) return true;

            var candidates = new List<string>();

            // 1. App.Settings?.Current?.KeysPath
            string? configured = App.Settings?.Current?.KeysPath;
            if (!string.IsNullOrWhiteSpace(configured))
            {
                candidates.Add(configured);
            }

            // 2. Near contextFilePath (same directory prod.keys or keys\prod.keys) if provided
            if (!string.IsNullOrWhiteSpace(contextFilePath))
            {
                try
                {
                    string? ctxDir = Directory.Exists(contextFilePath) ? contextFilePath : Path.GetDirectoryName(contextFilePath);
                    if (!string.IsNullOrEmpty(ctxDir) && Directory.Exists(ctxDir))
                    {
                        candidates.Add(Path.Combine(ctxDir, "prod.keys"));
                        candidates.Add(Path.Combine(ctxDir, "keys", "prod.keys"));
                        candidates.Add(Path.Combine(ctxDir, "keys.txt"));
                    }
                }
                catch { }
            }

            // 3. AppDomain.CurrentDomain.BaseDirectory (prod.keys, keys\prod.keys, tools\prod.keys)
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            candidates.Add(Path.Combine(baseDir, "prod.keys"));
            candidates.Add(Path.Combine(baseDir, "keys", "prod.keys"));
            candidates.Add(Path.Combine(baseDir, "tools", "prod.keys"));
            candidates.Add(Path.Combine(baseDir, "keys.txt"));

            // 4. %USERPROFILE%\.switch\prod.keys
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            candidates.Add(Path.Combine(userProfile, ".switch", "prod.keys"));
            candidates.Add(Path.Combine(userProfile, ".switch", "keys.txt"));

            // 5. %LOCALAPPDATA%\StormSwitchBox\keys\prod.keys and %APPDATA%\StormSwitchBox\keys\prod.keys
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            candidates.Add(Path.Combine(localAppData, "StormSwitchBox", "keys", "prod.keys"));
            candidates.Add(Path.Combine(appData, "StormSwitchBox", "keys", "prod.keys"));

            // 6. %APPDATA%\STORM_SWITCH\keys\prod.keys, %APPDATA%\Eden\keys\prod.keys, %APPDATA%\citron\keys\prod.keys, %APPDATA%\yuzu\keys\prod.keys, %APPDATA%\suyu\keys\prod.keys, %APPDATA%\sudachi\keys\prod.keys, %APPDATA%\Ryujinx\system\prod.keys
            candidates.Add(Path.Combine(appData, "STORM_SWITCH", "keys", "prod.keys"));
            candidates.Add(Path.Combine(appData, "Eden", "keys", "prod.keys"));
            candidates.Add(Path.Combine(appData, "citron", "keys", "prod.keys"));
            candidates.Add(Path.Combine(appData, "Citron", "keys", "prod.keys"));
            candidates.Add(Path.Combine(appData, "yuzu", "keys", "prod.keys"));
            candidates.Add(Path.Combine(appData, "suyu", "keys", "prod.keys"));
            candidates.Add(Path.Combine(appData, "sudachi", "keys", "prod.keys"));
            candidates.Add(Path.Combine(appData, "Ryujinx", "system", "prod.keys"));

            // 7. Every folder in App.Settings?.Current?.EmulatorDirectories: <dir>\user\keys\prod.keys, <dir>\keys\prod.keys, <dir>\system\prod.keys
            try
            {
                var emuFolders = App.Settings?.Current?.EmulatorDirectories;
                if (emuFolders != null)
                {
                    foreach (var emuDir in emuFolders)
                    {
                        if (string.IsNullOrWhiteSpace(emuDir)) continue;
                        candidates.Add(Path.Combine(emuDir, "user", "keys", "prod.keys"));
                        candidates.Add(Path.Combine(emuDir, "keys", "prod.keys"));
                        candidates.Add(Path.Combine(emuDir, "system", "prod.keys"));
                        candidates.Add(Path.Combine(emuDir, "prod.keys"));
                    }
                }
            }
            catch { }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrWhiteSpace(candidate)) continue;
                string fullCandidate;
                try
                {
                    fullCandidate = Path.GetFullPath(candidate);
                }
                catch
                {
                    continue;
                }

                if (!visited.Add(fullCandidate)) continue;

                if (File.Exists(fullCandidate))
                {
                    if (LoadKeys(fullCandidate))
                    {
                        if (App.Settings?.Current != null && string.IsNullOrWhiteSpace(App.Settings.Current.KeysPath))
                        {
                            App.Settings.Current.KeysPath = fullCandidate;
                            try { App.Settings.Save(); } catch { }
                        }
                        return true;
                    }
                }
            }

            return IsLoaded;
        }

        public async Task<string> EnsureKeysPreparedAsync(string sourceKeysPath, string targetDirectory)
        {
            await _semaphore.WaitAsync();
            try
            {
                Directory.CreateDirectory(targetDirectory);
                string targetKeys = Path.Combine(targetDirectory, "prod.keys");

                if (File.Exists(sourceKeysPath))
                {
                    string[] lines = await File.ReadAllLinesAsync(sourceKeysPath);
                    var cleanLines = new List<string>();
                    foreach (var line in lines)
                    {
                        string trimmed = line.Trim();
                        if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("#"))
                        {
                            cleanLines.Add(trimmed);
                        }
                    }
                    await File.WriteAllLinesAsync(targetKeys, cleanLines);
                }
                return targetKeys;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public string PrepareKeysForYanu(string sourceKeysPath, string targetDirectory)
        {
            _semaphore.Wait();
            try
            {
                Directory.CreateDirectory(targetDirectory);
                string targetKeys = Path.Combine(targetDirectory, "prod.keys");

                if (File.Exists(sourceKeysPath))
                {
                    string[] lines = File.ReadAllLines(sourceKeysPath);
                    var cleanLines = new List<string>();
                    foreach (var line in lines)
                    {
                        string trimmed = line.Trim();
                        if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("#"))
                        {
                            cleanLines.Add(trimmed);
                        }
                    }
                    File.WriteAllLines(targetKeys, cleanLines);
                }
                return targetKeys;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        #region Nintendo Switch 2 (Ounce / NVN2) Keys & Firmware Inspection

        public static Switch2KeyInfo AnalyzeSwitch2KeysFile(string? keysPath, string? titleKeysPath = null)
        {
            var result = new Switch2KeyInfo();
            if (string.IsNullOrWhiteSpace(keysPath) || !File.Exists(keysPath))
            {
                result.IsLoaded = false;
                result.FirmwareDescription = "Ключи не выбраны";
                return result;
            }

            try
            {
                result.FilePath = keysPath;
                int keyCount = 0;
                int maxRev = -1;
                string highestKey = string.Empty;
                bool hasOunce = false;

                foreach (var line in File.ReadLines(keysPath))
                {
                    string trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#") || trimmed.StartsWith("//"))
                        continue;

                    int eqIdx = trimmed.IndexOf('=');
                    if (eqIdx <= 0) continue;

                    keyCount++;
                    string keyName = trimmed.Substring(0, eqIdx).Trim().ToLowerInvariant();
                    if (keyName.Contains("ounce"))
                    {
                        hasOunce = true;
                    }

                    if (keyName.StartsWith("master_key_") || keyName.StartsWith("ounce_master_key_"))
                    {
                        string[] parts = keyName.Split('_');
                        string hexRev = parts[parts.Length - 1];
                        if (int.TryParse(hexRev, System.Globalization.NumberStyles.HexNumber, null, out int rev))
                        {
                            if (rev > maxRev)
                            {
                                maxRev = rev;
                                highestKey = keyName;
                            }
                        }
                    }
                }

                result.KeyCount = keyCount;
                result.MaxMasterKeyRevision = maxRev;
                result.HighestKeyName = highestKey;
                result.HasOunceKeys = hasOunce;
                result.IsLoaded = keyCount > 0;

                // Build human-readable firmware description
                if (maxRev >= 0)
                {
                    string hosFw = GetFirmwareByMasterKeyRevision(maxRev);
                    string ounceTag = hasOunce ? "Ounce / " : (maxRev >= 0x13 ? "Ounce / " : "");
                    string revHex = maxRev.ToString("X2");
                    result.FirmwareDescription = $"Horizon OS {hosFw} ({ounceTag}Master Key {revHex})";
                }
                else if (hasOunce)
                {
                    result.FirmwareDescription = "Horizon OS 20.x–23.x (Ounce / NVN2)";
                }
                else
                {
                    result.FirmwareDescription = keyCount > 0 ? "Horizon OS (пользовательский набор ключей)" : "Ключи не обнаружены";
                }

                // Analyze title keys if available
                string? effectiveTitleKeys = titleKeysPath;
                if (string.IsNullOrWhiteSpace(effectiveTitleKeys) || !File.Exists(effectiveTitleKeys))
                {
                    string? dir = Path.GetDirectoryName(keysPath);
                    if (!string.IsNullOrEmpty(dir))
                    {
                        string candidate = Path.Combine(dir, "title.keys");
                        if (File.Exists(candidate)) effectiveTitleKeys = candidate;
                    }
                }
                if (string.IsNullOrWhiteSpace(effectiveTitleKeys) || !File.Exists(effectiveTitleKeys))
                {
                    string userHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "title.keys");
                    if (File.Exists(userHome)) effectiveTitleKeys = userHome;
                }

                if (!string.IsNullOrWhiteSpace(effectiveTitleKeys) && File.Exists(effectiveTitleKeys))
                {
                    result.TitleKeysFilePath = effectiveTitleKeys;
                    int tCount = 0;
                    foreach (var tLine in File.ReadLines(effectiveTitleKeys))
                    {
                        string t = tLine.Trim();
                        if (!string.IsNullOrEmpty(t) && !t.StartsWith("#") && t.Contains('='))
                            tCount++;
                    }
                    result.TitleKeyCount = tCount;
                }

                return result;
            }
            catch (Exception ex)
            {
                result.IsLoaded = false;
                result.FirmwareDescription = $"Ошибка анализа: {ex.Message}";
                return result;
            }
        }

        public static string? AutoSearchSwitch2Keys()
        {
            var candidates = new List<string>
            {
                @"Q:\CONSOLES\Nintendo Switch 2\prod.keys",
                @"Q:\CONSOLES\Nintendo Switch 2\ounce.keys",
                @"Q:\CONSOLES\Nintendo Switch 2\keys.txt",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "prod.keys"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "keys.txt"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StormSwitchBox", "keys", "prod.keys"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ryujinx", "system", "prod.keys"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Eden", "keys", "prod.keys"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Citron", "keys", "prod.keys"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "keys.dat"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "keys", "prod.keys")
            };

            if (!string.IsNullOrEmpty(App.Settings.Current.KeysPath) && File.Exists(App.Settings.Current.KeysPath))
            {
                candidates.Add(App.Settings.Current.KeysPath);
            }

            foreach (var cand in candidates)
            {
                if (File.Exists(cand))
                {
                    try
                    {
                        var info = AnalyzeSwitch2KeysFile(cand);
                        if (info.IsLoaded && info.KeyCount > 0)
                        {
                            return cand;
                        }
                    }
                    catch { }
                }
            }

            return null;
        }

        public static string? AutoSearchSwitch2TitleKeys(string? keysPath)
        {
            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(keysPath))
            {
                string? dir = Path.GetDirectoryName(keysPath);
                if (!string.IsNullOrEmpty(dir))
                {
                    candidates.Add(Path.Combine(dir, "title.keys"));
                    candidates.Add(Path.Combine(dir, "title.keys.txt"));
                }
            }
            candidates.Add(@"Q:\CONSOLES\Nintendo Switch 2\title.keys");
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "title.keys"));
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StormSwitchBox", "keys", "title.keys"));

            foreach (var cand in candidates)
            {
                if (File.Exists(cand)) return cand;
            }
            return null;
        }

        #endregion
    }

    public class Switch2KeyInfo
    {
        public bool IsLoaded { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public int KeyCount { get; set; }
        public int MaxMasterKeyRevision { get; set; } = -1;
        public string HighestKeyName { get; set; } = string.Empty;
        public string FirmwareDescription { get; set; } = "Не определена";
        public bool HasOunceKeys { get; set; }
        public int TitleKeyCount { get; set; }
        public string? TitleKeysFilePath { get; set; }
    }
}
