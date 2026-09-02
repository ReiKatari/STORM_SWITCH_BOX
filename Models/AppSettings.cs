using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StormSwitchBox.Models
{
    public class AppSettings
    {
        // Окно
        public string AppVersion { get; set; } = "5.0.2";
        public string Language { get; set; } = "ru"; // ru, en, de, zh, ja
        public int WindowX { get; set; } = -1;
        public int WindowY { get; set; } = -1;
        public int WindowWidth { get; set; } = 1200;
        public int WindowHeight { get; set; } = 800;
        public string WindowState { get; set; } = "Normal";
        
        // Папки эмуляторов (STORM SWITCH, Yuzu, Ryujinx, Suyu и др.)
        public List<string> EmulatorDirectories { get; set; } = new();
        
        // Рабочие параметры алгоритмов
        public int CompressionLevel { get; set; } = 22;
        public int KeyGeneration { get; set; } = 19;
        public bool UnpackStitched { get; set; } = false;
        public bool ComplexFolders { get; set; } = true;   // По умолчанию ВКЛЮЧЕН
        public bool SmartProcessing { get; set; } = true;  // Умная обработка файлов (По умолчанию ВКЛЮЧЕНА)
        public int MultiContentBuildMode { get; set; } = 0; // 0 = Smart Auto, 1 = Force HardPatch, 2 = Fast Multi-Content
        public bool ForceMultiRebuild { get => SmartProcessing; set => SmartProcessing = value; }  // Для совместимости
        public bool DeleteSourceOnSuccess { get; set; } = false;
        public bool TrimXci { get; set; } = false;         // По умолчанию ВЫКЛЮЧЕН
        public bool RemoveTitlerights { get; set; } = false; // Удалить Titlerights (ticketless NSP)
        public bool RemoveDeltaNca { get; set; } = true;     // Удалить Delta NCA из обновлений
        public bool SplitFat32 { get; set; } = false;        // Разделить >4GB для FAT32
        public List<string> KeepLanguages { get; set; } = new List<string> { "ru", "ru-RU", "en-US", "en-GB", "en" };
        public int UsedCores { get; set; } = 16;
        public int ConcurrentTasks { get; set; } = 3;
        public string KeysVersion { get; set; } = "";
        public string KeysPath { get; set; } = "";

        // Настройки Nintendo 3DS
        public string KeysPath3ds { get; set; } = "";
        public string DefaultFormat3ds { get; set; } = "3DS"; // 3DS (CCI), CIA, CXI
        public bool HardPatch3ds { get; set; } = true;
        public int SelectedFormatIndex3ds { get; set; } = 0; // 0 = 3DS, 1 = CIA, 2 = CXI
        public int SelectedPlatformIndex { get; set; } = 0; // 0 = Switch, 1 = 3DS
        public int SelectedSettingsTab { get; set; } = 0; // 0 = General, 1 = Switch, 2 = 3DS

        // Понижение версии прошивки (RSV Cap)
        public int RsvCap { get; set; } = 268435656; // FW 18.0
        public bool EnableRsvCap { get; set; } = true;

        // Папка наблюдения Switch (Watch Folder Switch)
        public string WatchFolderSwitch { get; set; } = "";
        public bool EnableWatchFolderSwitch { get; set; } = false;
        public int WatchFolderActionSwitch { get; set; } = 0; // 0 = Сжатие, 1 = Распаковка, 2 = Упаковка, 3 = Конвертация, 4 = Мульти-контент, 5 = Проверка
        public int WatchFolderFormatSwitch { get; set; } = 0; // 0 = NSP, 1 = NSZ, 2 = XCI, 3 = XCZ

        // Папка наблюдения 3DS (Watch Folder 3DS)
        public string WatchFolder3ds { get; set; } = "";
        public bool EnableWatchFolder3ds { get; set; } = false;
        public int WatchFolderAction3ds { get; set; } = 0; // 0 = Конвертация, 1 = Распаковка, 2 = Упаковка, 3 = Мульти-контент, 4 = Проверка
        public int WatchFolderFormat3ds { get; set; } = 0; // 0 = 3DS, 1 = CIA, 2 = CXI

        // Совместимость со старыми настройками
        public string WatchFolder { get => WatchFolderSwitch; set => WatchFolderSwitch = value; }
        public bool EnableWatchFolder { get => EnableWatchFolderSwitch; set => EnableWatchFolderSwitch = value; }
        public int WatchFolderAction { get => WatchFolderActionSwitch; set => WatchFolderActionSwitch = value; }
        public int WatchFolderFormat { get => WatchFolderFormatSwitch; set => WatchFolderFormatSwitch = value; }

        // Уведомления и оформление
        public bool EnableSoundNotifications { get; set; } = true;
        public string AccentColorTheme { get; set; } = "Default";
        public string AppTheme { get; set; } = "STORM MIDNIGHT";

        // Выходной формат (по умолчанию NSP)
        public int SelectedFormatIndex { get; set; } = 0;

        // Пути сохранения (выходная папка для каждого режима)
        public string LastOutPath_Convert { get; set; } = "";
        public string LastOutPath_Multi { get; set; } = "";
        public string LastOutPath_Homebrew { get; set; } = "";
        public string LastOutPath_Pack { get; set; } = "";
        public string LastOutPath_Update { get; set; } = "";
        public string LastOutPath_Unpack { get; set; } = "";
        public string LastOutPath_3ds { get; set; } = "";
        
        // Выходная папка по умолчанию для Switch
        public string OutputFolder { get; set; } = "";
        // Выходная папка по умолчанию для 3DS
        public string OutputFolder3ds { get; set; } = "";
        
        // Каталог
        public List<string> CatalogFolders { get; set; } = new();
        public Dictionary<string, string> VersionOverrides { get; set; } = new();

        // Состояние интерфейса
        public bool TaskPanelVisible { get; set; } = true;
        public double LogPanelHeight { get; set; } = 130;
        public Dictionary<string, bool> ColumnVisibility { get; set; } = new();
        public Dictionary<string, int> ColumnWidths { get; set; } = new();
    }
}
