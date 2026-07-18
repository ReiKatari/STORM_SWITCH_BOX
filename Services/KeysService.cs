using System;
using System.IO;
using LibHac.Common.Keys;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public class KeysService
    {
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
    }
}
