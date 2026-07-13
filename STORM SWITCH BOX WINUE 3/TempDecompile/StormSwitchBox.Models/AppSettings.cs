using System.Collections.Generic;

namespace StormSwitchBox.Models;

public class AppSettings
{
	public int WindowX { get; set; } = 100;

	public int WindowY { get; set; } = 100;

	public int WindowWidth { get; set; } = 1200;

	public int WindowHeight { get; set; } = 800;

	public string WindowState { get; set; } = "Normal";

	public int CompressionLevel { get; set; } = 22;

	public int KeyGeneration { get; set; } = 19;

	public bool UnpackStitched { get; set; } = false;

	public bool ComplexFolders { get; set; } = false;

	public bool ForceMultiRebuild { get; set; } = true;

	public bool DeleteSourceOnSuccess { get; set; } = false;

	public bool TrimXci { get; set; } = true;

	public List<string> KeepLanguages { get; set; } = new List<string> { "ru", "ru-RU", "en-US", "en-GB", "en" };

	public int UsedCores { get; set; } = 16;

	public int ConcurrentTasks { get; set; } = 3;

	public string KeysVersion { get; set; } = "22.1.0";

	public string KeysPath { get; set; } = "";

	public int SelectedFormatIndex { get; set; } = 0;

	public string LastOutPath_Convert { get; set; } = "";

	public string LastOutPath_Multi { get; set; } = "";

	public string LastOutPath_Pack { get; set; } = "";

	public string LastOutPath_Update { get; set; } = "";

	public string LastOutPath_Unpack { get; set; } = "";

	public string OutputFolder { get; set; } = "";

	public List<string> CatalogFolders { get; set; } = new List<string>();

	public Dictionary<string, string> VersionOverrides { get; set; } = new Dictionary<string, string>();

	public bool TaskPanelVisible { get; set; } = true;

	public double LogPanelHeight { get; set; } = 130.0;

	public Dictionary<string, bool> ColumnVisibility { get; set; } = new Dictionary<string, bool>();

	public Dictionary<string, int> ColumnWidths { get; set; } = new Dictionary<string, int>();
}
