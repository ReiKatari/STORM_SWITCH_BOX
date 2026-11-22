using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public class SettingsService
    {
        private const string DpapiPrefix = "dpapi:";

        private static string GetSettingsFilePath()
        {
            string localAppDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StormSwitchBox");
            Directory.CreateDirectory(localAppDataDir);
            string appDataPath = Path.Combine(localAppDataDir, "ssb_native.settings.json");
            
            string baseDirPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ssb_native.settings.json");
            if (File.Exists(baseDirPath) && !File.Exists(appDataPath))
            {
                try { File.Copy(baseDirPath, appDataPath, true); } catch { }
            }
            return appDataPath;
        }

        private static readonly string SettingsPath = GetSettingsFilePath();
        private static readonly string LegacySettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "ssb.settings");
        private static readonly object _saveLock = new object();
        private static readonly JsonSerializerOptions _saveJsonOptions = new JsonSerializerOptions { WriteIndented = true };
        private static SettingsService? _instance;
        private static AppSettings _sharedCurrent = CreateDefaultSettings();

        public static SettingsService Instance => _instance ?? CreateCanonicalInstance();

        private static SettingsService CreateCanonicalInstance()
        {
            var created = new SettingsService();
            return _instance ?? created;
        }

        private static AppSettings CreateDefaultSettings()
        {
            var s = new AppSettings { SwitchDefaultNszInitialized = true };
            if (string.IsNullOrWhiteSpace(s.Language) ||
                s.Language.Equals("system", StringComparison.OrdinalIgnoreCase) ||
                s.Language.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                s.Language = AppSettings.GetDefaultOsLanguage();
            }
            return s;
        }

        public AppSettings Current
        {
            get => _sharedCurrent;
            private set => _sharedCurrent = value;
        }

        public SettingsService()
        {
            Interlocked.CompareExchange(ref _instance, this, null);
        }

        private static string ProtectSecret(string? plainText)
        {
            if (string.IsNullOrWhiteSpace(plainText))
                return string.Empty;
            if (plainText.StartsWith(DpapiPrefix, StringComparison.Ordinal))
                return plainText;
            try
            {
                byte[] raw = Encoding.UTF8.GetBytes(plainText);
                byte[] encrypted = ProtectedData.Protect(raw, null, DataProtectionScope.CurrentUser);
                return DpapiPrefix + Convert.ToBase64String(encrypted);
            }
            catch
            {
                return plainText;
            }
        }

        private static string UnprotectSecret(string? storedValue, out bool wasPlaintext)
        {
            wasPlaintext = false;
            if (string.IsNullOrWhiteSpace(storedValue))
                return string.Empty;
            if (!storedValue.StartsWith(DpapiPrefix, StringComparison.Ordinal))
            {
                wasPlaintext = true;
                return storedValue;
            }
            try
            {
                string b64 = storedValue.Substring(DpapiPrefix.Length);
                byte[] encrypted = Convert.FromBase64String(b64);
                byte[] raw = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(raw);
            }
            catch
            {
                return string.Empty;
            }
        }

        public async Task LoadAsync()
        {
            if (File.Exists(SettingsPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(SettingsPath).ConfigureAwait(false);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        bool isDirty = false;
                        if (settings.AppVersion != App.AppVersionString)
                        {
                            settings.AppVersion = App.AppVersionString;
                            isDirty = true;
                        }

                        // Расшифровываем DPAPI-секреты и помечаем старые открытые строки для перезаписи в зашифрованном виде
                        settings.RuTrackerCookie = UnprotectSecret(settings.RuTrackerCookie, out bool cookiePlain);
                        settings.RuTrackerCfClearance = UnprotectSecret(settings.RuTrackerCfClearance, out bool cfPlain);
                        if (cookiePlain || cfPlain)
                        {
                            isDirty = true;
                        }

                        if (settings.EmulatorDirectories == null)
                        {
                            settings.EmulatorDirectories = new List<string>();
                            isDirty = true;
                        }
                        if (string.IsNullOrWhiteSpace(settings.Language) ||
                            settings.Language.Equals("system", StringComparison.OrdinalIgnoreCase) ||
                            settings.Language.Equals("auto", StringComparison.OrdinalIgnoreCase))
                        {
                            settings.Language = AppSettings.GetDefaultOsLanguage();
                            isDirty = true;
                        }
                        else
                        {
                            var normalized = AppSettings.NormalizeLanguageCode(settings.Language);
                            if (normalized != settings.Language)
                            {
                                settings.Language = normalized;
                                isDirty = true;
                            }
                        }

                        // Миграция / защита уровня сжатия Switch 2: по умолчанию 18
                        if (!settings.Switch2CompressionLevelInitialized || settings.Switch2CompressionLevel <= 1)
                        {
                            settings.Switch2CompressionLevel = 18;
                            settings.Switch2CompressionLevelInitialized = true;
                            isDirty = true;
                        }

                        // Миграция формата по умолчанию для Switch 1 и Switch 2 на NSZ
                        if (!settings.SwitchDefaultNszInitialized)
                        {
                            settings.SelectedFormatIndex = 1; // NSZ
                            settings.Switch2DefaultFormat = "NSZ";
                            settings.Switch2SelectedFormatIndex = 0; // NSZ
                            settings.WatchFolderFormatSwitch = 1; // NSZ
                            settings.SwitchDefaultNszInitialized = true;
                            isDirty = true;
                        }
                        
                        Current = settings;
                        if (isDirty) await SaveAsync().ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        File.Copy(SettingsPath, SettingsPath + ".bak", overwrite: true);
                    }
                    catch { }
                    App.Logger?.Log($"[SettingsService] Ошибка чтения файла настроек (создана резервная копия .bak): {ex.Message}", LogLevel.Warning);
                }
            }
            else if (File.Exists(LegacySettingsPath))
            {
                // Миграция старых настроек (ssb.settings)
                MigrateLegacySettings();
                if (string.IsNullOrWhiteSpace(Current.Language) ||
                    Current.Language.Equals("system", StringComparison.OrdinalIgnoreCase) ||
                    Current.Language.Equals("auto", StringComparison.OrdinalIgnoreCase))
                {
                    Current.Language = AppSettings.GetDefaultOsLanguage();
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(Current.Language) ||
                    Current.Language.Equals("system", StringComparison.OrdinalIgnoreCase) ||
                    Current.Language.Equals("auto", StringComparison.OrdinalIgnoreCase))
                {
                    Current.Language = AppSettings.GetDefaultOsLanguage();
                }
            }
        }

        public void Save()
        {
            lock (_saveLock)
            {
                try
                {
                    var node = JsonSerializer.SerializeToNode(Current, _saveJsonOptions) as JsonObject;
                    string json;
                    if (node != null)
                    {
                        node[nameof(AppSettings.RuTrackerCookie)] = ProtectSecret(Current.RuTrackerCookie);
                        node[nameof(AppSettings.RuTrackerCfClearance)] = ProtectSecret(Current.RuTrackerCfClearance);
                        json = node.ToJsonString(_saveJsonOptions);
                    }
                    else
                    {
                        json = JsonSerializer.Serialize(Current, _saveJsonOptions);
                    }

                    SafeFileOperations.WriteAllTextAtomic(SettingsPath, json, flushToDisk: true);
                }
                catch { }
            }
        }

        public async Task SaveAsync()
        {
            await Task.Run(() => Save());
        }

        private void MigrateLegacySettings()
        {
            try
            {
                var lines = File.ReadAllLines(LegacySettingsPath);
                if (lines.Length >= 2)
                {
                    var headers = lines[0].Replace("\"", "").Split(',');
                    var values = lines[1].Replace("\"", "").Split(',');

                    for (int i = 0; i < headers.Length && i < values.Length; i++)
                    {
                        var h = headers[i];
                        var v = values[i];

                        if (h == "CompressionLevel" && int.TryParse(v, out int cl)) Current.CompressionLevel = cl;
                        if (h == "KeyGeneration" && int.TryParse(v, out int kg)) Current.KeyGeneration = kg;
                        if (h == "UnpackStitched" && bool.TryParse(v, out bool us)) Current.UnpackStitched = us;
                        if (h == "ComplexFolders" && bool.TryParse(v, out bool cf)) Current.ComplexFolders = cf;
                        if (h == "ForceMultiRebuild" && bool.TryParse(v, out bool fmr)) Current.ForceMultiRebuild = fmr;
                        if (h == "UsedCores" && int.TryParse(v, out int uc)) Current.UsedCores = uc;
                        if (h == "ConcurrentTasks" && int.TryParse(v, out int ct)) Current.ConcurrentTasks = ct;
                        if (h == "KeysVersion") Current.KeysVersion = v;
                    }
                }
                // Асинхронное сохранение в фоне не блокирует запуск
                _ = SaveAsync();
            }
            catch { }
        }
    }
}
