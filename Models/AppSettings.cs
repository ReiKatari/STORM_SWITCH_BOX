using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StormSwitchBox.Models
{
    public class AppSettings
    {
        // Окно
        public string AppVersion { get; set; } = "3.9.9";
        public int WindowX { get; set; } = -1;
        public int WindowY { get; set; } = -1;
        public int WindowWidth { get; set; } = 1200;
        public int WindowHeight { get; set; } = 800;
        public string WindowState { get; set; } = "Normal";
        
        // Рабочие параметры алгоритмов
        public int CompressionLevel { get; set; } = 22;
        public int KeyGeneration { get; set; } = 19;
        public bool UnpackStitched { get; set; } = false;
        public bool ComplexFolders { get; set; } = true;   // По умолчанию ВКЛЮЧЕН
        public bool ForceMultiRebuild { get; set; } = true;  // По умолчанию ВКЛЮЧЕН
        public bool DeleteSourceOnSuccess { get; set; } = false;
        public bool TrimXci { get; set; } = false;         // По умолчанию ВЫКЛЮЧЕН
        public List<string> KeepLanguages { get; set; } = new List<string> { "ru", "ru-RU", "en-US", "en-GB", "en" };
        public int UsedCores { get; set; } = 16;
        public int ConcurrentTasks { get; set; } = 3;
        public string KeysVersion { get; set; } = "";
        public string KeysPath { get; set; } = "";

        // Выходной формат (по умолчанию NSP)
        public int SelectedFormatIndex { get; set; } = 0;

        // Пути сохранения (выходная папка для каждого режима)
        public string LastOutPath_Convert { get; set; } = "";
        public string LastOutPath_Multi { get; set; } = "";
        public string LastOutPath_Pack { get; set; } = "";
        public string LastOutPath_Update { get; set; } = "";
        public string LastOutPath_Unpack { get; set; } = "";
        
        // Общая выходная папка (текущая)
        public string OutputFolder { get; set; } = "";
        
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
