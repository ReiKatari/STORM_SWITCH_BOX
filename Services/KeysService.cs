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

        public KeysService()
        {
            CurrentKeyset = new KeySet();
        }

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
                
                App.Logger.Log($"Ключи успешно загружены!", LogLevel.Success);
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
