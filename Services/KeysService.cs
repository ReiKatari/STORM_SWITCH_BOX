using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LibHac.Common.Keys;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public class KeysService
    {
        private static readonly SemaphoreSlim _semaphore = new(1, 1);

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
    }
}
