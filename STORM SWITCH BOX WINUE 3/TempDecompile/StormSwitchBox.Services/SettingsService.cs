using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services;

public class SettingsService
{
	private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ssb_native.settings.json");

	private static readonly string LegacySettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "ssb.settings");

	public AppSettings Current { get; private set; }

	public SettingsService()
	{
		Current = new AppSettings();
	}

	public async Task LoadAsync()
	{
		if (File.Exists(SettingsPath))
		{
			try
			{
				AppSettings settings = JsonSerializer.Deserialize<AppSettings>(await File.ReadAllTextAsync(SettingsPath));
				if (settings != null)
				{
					Current = settings;
				}
				return;
			}
			catch
			{
				return;
			}
		}
		if (File.Exists(LegacySettingsPath))
		{
			MigrateLegacySettings();
		}
	}

	public async Task SaveAsync()
	{
		try
		{
			await File.WriteAllTextAsync(contents: JsonSerializer.Serialize(options: new JsonSerializerOptions
			{
				WriteIndented = true
			}, value: Current), path: SettingsPath);
		}
		catch
		{
		}
	}

	private void MigrateLegacySettings()
	{
		try
		{
			string[] array = File.ReadAllLines(LegacySettingsPath);
			if (array.Length >= 2)
			{
				string[] array2 = array[0].Replace("\"", "").Split(',');
				string[] array3 = array[1].Replace("\"", "").Split(',');
				for (int i = 0; i < array2.Length && i < array3.Length; i++)
				{
					string text = array2[i];
					string text2 = array3[i];
					if (text == "CompressionLevel" && int.TryParse(text2, out var result))
					{
						Current.CompressionLevel = result;
					}
					if (text == "KeyGeneration" && int.TryParse(text2, out var result2))
					{
						Current.KeyGeneration = result2;
					}
					if (text == "UnpackStitched" && bool.TryParse(text2, out var result3))
					{
						Current.UnpackStitched = result3;
					}
					if (text == "ComplexFolders" && bool.TryParse(text2, out var result4))
					{
						Current.ComplexFolders = result4;
					}
					if (text == "ForceMultiRebuild" && bool.TryParse(text2, out var result5))
					{
						Current.ForceMultiRebuild = result5;
					}
					if (text == "UsedCores" && int.TryParse(text2, out var result6))
					{
						Current.UsedCores = result6;
					}
					if (text == "ConcurrentTasks" && int.TryParse(text2, out var result7))
					{
						Current.ConcurrentTasks = result7;
					}
					if (text == "KeysVersion")
					{
						Current.KeysVersion = text2;
					}
				}
			}
			SaveAsync();
		}
		catch
		{
		}
	}
}
