using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public class SettingsService
    {
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

        public static SettingsService Instance { get; set; } = new SettingsService();

        public AppSettings Current { get; private set; }

        public SettingsService()
        {
            Current = new AppSettings();
            Instance = this;
        }

        public async Task LoadAsync()
        {
            if (File.Exists(SettingsPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(SettingsPath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        bool isDirty = false;
                        if (settings.AppVersion != "5.0.2")
                        {
                            settings.AppVersion = "5.0.2";
                            isDirty = true;
                        }
                        if (settings.EmulatorDirectories == null)
                        {
                            settings.EmulatorDirectories = new List<string>();
                            isDirty = true;
                        }
                        settings.ComplexFolders = true;
                        settings.SmartProcessing = true;
                        settings.TrimXci = false;
                        settings.RemoveTitlerights = false;
                        
                        Current = settings;
                        if (isDirty) await SaveAsync();
                    }
                }
                catch { }
            }
            else if (File.Exists(LegacySettingsPath))
            {
                // Миграция старых настроек (ssb.settings)
                MigrateLegacySettings();
            }
        }

        public async Task SaveAsync()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(Current, options);
                await File.WriteAllTextAsync(SettingsPath, json);
            }
            catch { }
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
