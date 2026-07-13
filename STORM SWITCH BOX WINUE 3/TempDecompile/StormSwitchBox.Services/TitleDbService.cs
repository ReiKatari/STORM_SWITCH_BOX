#define DEBUG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services;

public class TitleDbService
{
	private readonly string _dbPath;

	private Dictionary<string, TitleDbEntry> _db = new Dictionary<string, TitleDbEntry>();

	private bool _isLoaded = false;

	private readonly HttpClient _httpClient;

	private const string DbUrl = "https://tinfoil.media/repo/db/titles.json";

	public TitleDbService()
	{
		_dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "titledb.json");
		_httpClient = new HttpClient();
		_httpClient.DefaultRequestHeaders.Add("User-Agent", "StormSwitchBox/2.5");
		LoadLocalDbAsync();
	}

	public bool IsDatabaseFresh()
	{
		if (!File.Exists(_dbPath))
		{
			return false;
		}
		FileInfo fileInfo = new FileInfo(_dbPath);
		return (DateTime.Now - fileInfo.LastWriteTime).TotalDays < 1.0;
	}

	private async Task LoadLocalDbAsync()
	{
		if (!File.Exists(_dbPath))
		{
			return;
		}
		try
		{
			using FileStream stream = File.OpenRead(_dbPath);
			_db = (await JsonSerializer.DeserializeAsync<Dictionary<string, TitleDbEntry>>(stream)) ?? new Dictionary<string, TitleDbEntry>();
			foreach (KeyValuePair<string, TitleDbEntry> kvp in _db)
			{
				kvp.Value.Id = kvp.Key;
			}
			_isLoaded = true;
		}
		catch (Exception ex)
		{
			Debug.WriteLine("Ошибка загрузки локальной БД TitleDB: " + ex.Message);
		}
	}

	public async Task<bool> UpdateDatabaseAsync(IProgress<int>? progress = null)
	{
		try
		{
			HttpResponseMessage response = await _httpClient.GetAsync("https://tinfoil.media/repo/db/titles.json", HttpCompletionOption.ResponseHeadersRead);
			response.EnsureSuccessStatusCode();
			long totalBytes = response.Content.Headers.ContentLength ?? (-1);
			Directory.CreateDirectory(Path.GetDirectoryName(_dbPath));
			using FileStream fileStream = new FileStream(_dbPath, FileMode.Create, FileAccess.Write, FileShare.None);
			using Stream stream = await response.Content.ReadAsStreamAsync();
			byte[] buffer = new byte[8192];
			long totalRead = 0L;
			while (true)
			{
				int num;
				int read = (num = await stream.ReadAsync(buffer, 0, buffer.Length));
				if (num <= 0)
				{
					break;
				}
				await fileStream.WriteAsync(buffer, 0, read);
				totalRead += read;
				if (totalBytes > 0 && progress != null)
				{
					progress.Report((int)(totalRead * 100 / totalBytes));
				}
			}
			fileStream.Close();
			await LoadLocalDbAsync();
			return true;
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			Debug.WriteLine("Ошибка обновления TitleDB: " + ex2.Message);
			return false;
		}
	}

	public bool TryGetTitleInfo(string titleId, out TitleDbEntry? entry)
	{
		entry = null;
		if (!_isLoaded || string.IsNullOrEmpty(titleId))
		{
			return false;
		}
		string text = titleId.Trim().ToUpperInvariant();
		if (text.Length == 16 && text.EndsWith("800"))
		{
			string key = text.Substring(0, 13) + "000";
			if (_db.TryGetValue(key, out entry))
			{
				return true;
			}
		}
		return _db.TryGetValue(text, out entry);
	}

	public int GetDlcCount(string titleId)
	{
		if (!_isLoaded || string.IsNullOrEmpty(titleId) || titleId.Length != 16)
		{
			return 0;
		}
		string prefix = titleId.Substring(0, 13).ToUpperInvariant();
		return _db.Keys.Count((string k) => k.StartsWith(prefix) && !k.EndsWith("000") && !k.EndsWith("800"));
	}

	public void EnrichCatalogItem(CatalogItem item)
	{
		if (!TryGetTitleInfo(item.TitleId, out TitleDbEntry entry) || entry == null)
		{
			return;
		}
		App.MainDispatcher?.TryEnqueue(async delegate
		{
			if (item.TitleName == "Unknown Game" || item.TitleName == "Unknown" || string.IsNullOrEmpty(item.TitleName))
			{
				item.TitleName = entry.Name ?? item.TitleName;
			}
			if (!string.IsNullOrEmpty(entry.Description))
			{
				item.Description = entry.Description;
			}
			if (!string.IsNullOrEmpty(entry.Intro))
			{
				item.Intro = entry.Intro;
			}
			if (!string.IsNullOrEmpty(entry.Developer))
			{
				item.Developer = entry.Developer;
			}
			if (entry.ReleaseDate.HasValue)
			{
				string dateStr = entry.ReleaseDate.Value.ToString();
				if (dateStr.Length == 8)
				{
					string y = dateStr.Substring(0, 4);
					string m = dateStr.Substring(4, 2);
					string d = dateStr.Substring(6, 2);
					item.ReleaseDate = $"{d}.{m}.{y}";
				}
				else
				{
					item.ReleaseDate = dateStr;
				}
			}
			if (entry.Category != null && entry.Category.Count > 0)
			{
				item.Category = string.Join(", ", entry.Category);
			}
			if (entry.Languages != null && entry.Languages.Count > 0)
			{
				item.SupportedLanguages = string.Join(", ", entry.Languages).ToUpper();
			}
			if (entry.Rating.HasValue)
			{
				item.RatingAge = entry.Rating.Value + "+";
			}
			if (item.Regions == "UNKNOWN")
			{
				if (!string.IsNullOrEmpty(entry.Regions))
				{
					item.Regions = entry.Regions;
				}
				else
				{
					Match regMatch = Regex.Match(item.FileName ?? "", "\\[(US|WW|EU|JP|KR|UK|AS)\\]", RegexOptions.IgnoreCase);
					if (regMatch.Success)
					{
						item.Regions = regMatch.Groups[1].Value.ToUpper();
					}
					else
					{
						item.Regions = "WW";
					}
				}
			}
			if (!string.IsNullOrEmpty(entry.Publisher) && (item.Publisher == "Unknown" || string.IsNullOrEmpty(item.Publisher)))
			{
				item.Publisher = entry.Publisher;
			}
			item.DlcCount = GetDlcCount(item.TitleId);
			TitleDbEntry updateEntry = entry;
			bool hasUpdateEntry = false;
			if (item.TitleId.EndsWith("000") && item.TitleId.Length == 16)
			{
				string updateId = item.TitleId.Substring(0, 13) + "800";
				if (_db.TryGetValue(updateId, out TitleDbEntry upd))
				{
					updateEntry = upd;
					hasUpdateEntry = true;
				}
			}
			string remoteVerStr = null;
			string remoteVerCode = null;
			Dictionary<string, string> dictToUse = updateEntry?.VersionsDictionary ?? entry?.VersionsDictionary;
			string currentVersionCode = item.VersionCode ?? "0";
			if (App.Settings.Current.VersionOverrides.TryGetValue(item.TitleId ?? "", out string manualVer) && !string.IsNullOrEmpty(manualVer))
			{
				currentVersionCode = manualVer;
				App.MainDispatcher?.TryEnqueue(delegate
				{
					item.VersionCode = manualVer;
				});
			}
			if (currentVersionCode == "0" && dictToUse != null && !string.IsNullOrEmpty(item.Version))
			{
				string localVerClean = item.Version.Trim('v', 'V', ' ').ToLowerInvariant();
				KeyValuePair<string, string> matchingPair = dictToUse.FirstOrDefault<KeyValuePair<string, string>>((KeyValuePair<string, string> kv) => kv.Value != null && kv.Value.Trim('v', 'V', ' ').ToLowerInvariant() == localVerClean);
				if (matchingPair.Key != null)
				{
					currentVersionCode = matchingPair.Key;
					App.MainDispatcher?.TryEnqueue(delegate
					{
						item.VersionCode = matchingPair.Key;
					});
				}
			}
			if (dictToUse != null && dictToUse.Count > 0)
			{
				KeyValuePair<string, string> highest = dictToUse.LastOrDefault();
				remoteVerStr = highest.Value;
				remoteVerCode = highest.Key;
			}
			else
			{
				remoteVerStr = updateEntry?.Version ?? entry?.Version;
			}
			if (currentVersionCode == "0" && !hasUpdateEntry && !string.IsNullOrEmpty(remoteVerStr) && !remoteVerStr.Contains("."))
			{
				currentVersionCode = remoteVerStr;
				App.MainDispatcher?.TryEnqueue(delegate
				{
					item.VersionCode = remoteVerStr;
				});
			}
			if (!string.IsNullOrEmpty(remoteVerStr) && remoteVerStr != "0")
			{
				bool isOutdated = false;
				if (item.TitleId != null && item.TitleId.EndsWith("000") && !hasUpdateEntry)
				{
					isOutdated = false;
				}
				else
				{
					remoteVerStr = remoteVerStr.TrimStart('v', 'V');
					if (remoteVerStr.Contains("."))
					{
						string localVerStr = (item.Version ?? "").TrimStart('v', 'V');
						string[] remoteParts = remoteVerStr.Split('.');
						string[] localParts = localVerStr.Split('.');
						int maxParts = Math.Max(remoteParts.Length, localParts.Length);
						for (int i = 0; i < maxParts; i++)
						{
							int rv;
							int r = ((i < remoteParts.Length && int.TryParse(remoteParts[i], out rv)) ? rv : 0);
							int lv;
							int l = ((i < localParts.Length && int.TryParse(localParts[i], out lv)) ? lv : 0);
							if (r > l)
							{
								isOutdated = true;
								break;
							}
							if (r < l)
							{
								break;
							}
						}
					}
					else
					{
						string localCodeStr = currentVersionCode.TrimStart('v', 'V');
						if (long.TryParse(localCodeStr, out var localVerCodeVal) && long.TryParse(remoteVerStr, out var remoteVerCodeVal) && remoteVerCodeVal > localVerCodeVal)
						{
							isOutdated = true;
						}
					}
				}
				if (isOutdated)
				{
					item.IsOutdated = true;
					if (remoteVerStr.Contains("."))
					{
						item.UpdateVersion = remoteVerStr;
						item.UpdateVersionCode = remoteVerCode ?? "Неизвестно";
					}
					else
					{
						item.UpdateVersion = "";
						item.UpdateVersionCode = remoteVerStr;
					}
				}
			}
			if (!string.IsNullOrEmpty(entry?.IconUrl))
			{
				try
				{
					byte[] imgBytes = await _httpClient.GetByteArrayAsync(entry.IconUrl);
					BitmapImage bmp = new BitmapImage();
					using MemoryStream ms = new MemoryStream(imgBytes);
					await bmp.SetSourceAsync(ms.AsRandomAccessStream());
					item.CoverImage = bmp;
					item.CoverBytes = imgBytes;
				}
				catch
				{
				}
			}
		});
	}

	public IEnumerable<TitleDbEntry> SearchTitles(string query)
	{
		if (!_isLoaded || string.IsNullOrWhiteSpace(query))
		{
			yield break;
		}
		int count = 0;
		foreach (KeyValuePair<string, TitleDbEntry> item in _db)
		{
			TitleDbEntry entry = item.Value;
			bool matchName = entry.Name != null && entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase);
			bool matchId = entry.Id != null && entry.Id.Contains(query, StringComparison.OrdinalIgnoreCase);
			if (matchName || matchId)
			{
				if (count++ > 50)
				{
					yield break;
				}
				yield return entry;
			}
		}
	}
}
