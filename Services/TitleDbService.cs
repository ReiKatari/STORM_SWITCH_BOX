using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using StormSwitchBox.Models;
using System.Text.Json.Serialization;

namespace StormSwitchBox.Services
{
    public class TitleDbEntry
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("intro")] public string? Intro { get; set; }
        [JsonPropertyName("iconUrl")] public string? IconUrl { get; set; }
        [JsonPropertyName("publisher")] public string? Publisher { get; set; }
        [JsonPropertyName("developer")] public string? Developer { get; set; }
        
        [JsonPropertyName("releaseDate")] public JsonElement? ReleaseDateElement { get; set; }
        [JsonPropertyName("version")] public JsonElement? VersionElement { get; set; }
        [JsonPropertyName("versions")] public Dictionary<string, string>? VersionsDictionary { get; set; }
        [JsonPropertyName("rating")] public JsonElement? RatingElement { get; set; }
        
        [JsonPropertyName("category")] public List<string>? Category { get; set; }
        [JsonPropertyName("languages")] public List<string>? Languages { get; set; }
        [JsonPropertyName("regions")] public JsonElement? RegionsElement { get; set; }
        [JsonPropertyName("region")] public JsonElement? RegionElement { get; set; }

        public string? Version
        {
            get
            {
                try
                {
                    // Сначала пробуем взять из словаря versions максимальную версию
                    if (VersionsDictionary != null && VersionsDictionary.Count > 0)
                    {
                        var highestVer = VersionsDictionary.Values.LastOrDefault();
                        if (!string.IsNullOrEmpty(highestVer)) return highestVer;
                    }
                    if (VersionElement?.ValueKind == JsonValueKind.Number) return VersionElement.Value.GetDouble().ToString();
                    if (VersionElement?.ValueKind == JsonValueKind.String) return VersionElement.Value.GetString();
                } catch { }
                return null;
            }
        }

        public int? ReleaseDate
        {
            get
            {
                try
                {
                    if (ReleaseDateElement?.ValueKind == JsonValueKind.Number) return (int)ReleaseDateElement.Value.GetDouble();
                } catch { }
                return null;
            }
        }

        public int? Rating
        {
            get
            {
                try
                {
                    if (RatingElement?.ValueKind == JsonValueKind.Number) return (int)RatingElement.Value.GetDouble();
                    if (RatingElement?.ValueKind == JsonValueKind.String && int.TryParse(RatingElement.Value.GetString(), out int r)) return r;
                } catch { }
                return null;
            }
        }

        public string? Regions
        {
            get
            {
                try
                {
                    if (RegionsElement?.ValueKind == JsonValueKind.String) return RegionsElement.Value.GetString();
                    if (RegionElement?.ValueKind == JsonValueKind.String) return RegionElement.Value.GetString();
                } catch { }
                return null;
            }
        }
    }

    public class TitleDbService
    {
        private readonly string _dbPath;
        private Dictionary<string, TitleDbEntry> _db = new();
        private bool _isLoaded = false;
        private readonly HttpClient _httpClient;

        // URL для загрузки базы (может быть переопределен в настройках)
        private const string DbUrl = "https://tinfoil.media/repo/db/titles.json";

        public TitleDbService()
        {
            _dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "titledb.json");
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "StormSwitchBox/2.5");
            
            // Асинхронно загружаем базу из локального кэша
            _ = LoadLocalDbAsync();
        }

        public bool IsDatabaseFresh()
        {
            if (!File.Exists(_dbPath)) return false;
            var fi = new FileInfo(_dbPath);
            return (DateTime.Now - fi.LastWriteTime).TotalDays < 1;
        }

        private async Task LoadLocalDbAsync()
        {
            if (File.Exists(_dbPath))
            {
                try
                {
                    using var stream = File.OpenRead(_dbPath);
                    _db = await JsonSerializer.DeserializeAsync<Dictionary<string, TitleDbEntry>>(stream) ?? new();
                    
                    // Устанавливаем Id из ключа словаря
                    foreach (var kvp in _db)
                    {
                        kvp.Value.Id = kvp.Key;
                    }
                    
                    _isLoaded = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка загрузки локальной БД TitleDB: {ex.Message}");
                }
            }
        }

        public async Task<bool> UpdateDatabaseAsync(IProgress<int>? progress = null)
        {
            try
            {
                var response = await _httpClient.GetAsync(DbUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                
                Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
                using var fileStream = new FileStream(_dbPath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var stream = await response.Content.ReadAsStreamAsync();

                var buffer = new byte[8192];
                long totalRead = 0;
                int read;

                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, read);
                    totalRead += read;
                    if (totalBytes > 0 && progress != null)
                    {
                        progress.Report((int)((totalRead * 100) / totalBytes));
                    }
                }

                // Перезагружаем в память
                fileStream.Close();
                await LoadLocalDbAsync();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обновления TitleDB: {ex.Message}");
                return false;
            }
        }

        public bool TryGetTitleInfo(string titleId, out TitleDbEntry? entry)
        {
            entry = null;
            if (!_isLoaded || string.IsNullOrEmpty(titleId)) return false;

            // TitleDB обычно хранит ID в верхнем регистре, без ".nsp" и т.д.
            string key = titleId.Trim().ToUpperInvariant();
            
            // Если ID обновления (заканчивается на 800), пытаемся найти базовую игру (000)
            if (key.Length == 16 && key.EndsWith("800"))
            {
                string baseKey = key.Substring(0, 13) + "000";
                if (_db.TryGetValue(baseKey, out entry)) return true;
            }

            return _db.TryGetValue(key, out entry);
        }

        public List<TitleDbEntry> GetDlcs(string titleId)
        {
            var dlcs = new List<TitleDbEntry>();
            if (!_isLoaded || string.IsNullOrEmpty(titleId) || titleId.Length != 16) return dlcs;
            
            if (!ulong.TryParse(titleId, System.Globalization.NumberStyles.HexNumber, null, out ulong idVal)) return dlcs;
            
            ulong baseMasked = idVal & 0xFFFFFFFFFFFFE000;
            
            foreach (var kvp in _db)
            {
                if (ulong.TryParse(kvp.Key, System.Globalization.NumberStyles.HexNumber, null, out ulong kVal))
                {
                    if ((kVal & 0xFFFFFFFFFFFFE000) == baseMasked && (kVal & 0x0000000000001FFF) >= 0x1000)
                    {
                        dlcs.Add(kvp.Value);
                    }
                }
            }
            
            return dlcs.OrderBy(d => d.Id).ToList();
        }

        public int GetDlcCount(string titleId)
        {
            return GetDlcs(titleId).Count;
        }

        public void EnrichCatalogItem(CatalogItem item)
        {
            if (TryGetTitleInfo(item.TitleId, out var entry) && entry != null)
            {
                App.MainDispatcher?.TryEnqueue(async () =>
                {
                    if (item.TitleName == "Unknown Game" || item.TitleName == "Unknown" || string.IsNullOrEmpty(item.TitleName) || HasGarbageCharacters(item.TitleName))
                        item.TitleName = entry.Name ?? item.TitleName;

                    if (!string.IsNullOrEmpty(entry.Description))
                        item.Description = entry.Description;

                    if (!string.IsNullOrEmpty(entry.Intro))
                        item.Intro = entry.Intro;

                    if (!string.IsNullOrEmpty(entry.Developer))
                        item.Developer = entry.Developer;
                        
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
                        item.Category = string.Join(", ", entry.Category);

                    if (entry.Languages != null && entry.Languages.Count > 0)
                        item.SupportedLanguages = string.Join(", ", entry.Languages).ToUpper();

                    if (entry.Rating.HasValue)
                        item.RatingAge = entry.Rating.Value.ToString() + "+";

                    // Условие 1: Из файла (уже стоит в item.Regions, если не UNKNOWN)
                    // Условие 2: База TitleDB
                    // Условие 3: Имя файла
                    if (item.Regions == "UNKNOWN")
                    {
                        if (!string.IsNullOrEmpty(entry.Regions))
                        {
                            item.Regions = entry.Regions; // Условие 2
                        }
                        else
                        {
                            // Условие 3
                            var regMatch = System.Text.RegularExpressions.Regex.Match(item.FileName ?? "", @"\[(US|WW|EU|JP|KR|UK|AS)\]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (regMatch.Success) item.Regions = regMatch.Groups[1].Value.ToUpper();
                            else item.Regions = "WW"; // Крайний случай
                        }
                    }

                    if (!string.IsNullOrEmpty(entry.Publisher) && (item.Publisher == "Unknown" || string.IsNullOrEmpty(item.Publisher)))
                        item.Publisher = entry.Publisher;

                    await System.Threading.Tasks.Task.Run(() => 
                    {
                        var dlcs = GetDlcs(item.TitleId);
                        App.MainDispatcher?.TryEnqueue(() =>
                        {
                            item.DlcCount = dlcs.Count;
                            item.DlcList.Clear();
                            foreach (var dlc in dlcs)
                            {
                                item.DlcList.Add(dlc);
                            }
                        });
                    });

                    // Проверка обновлений
                    TitleDbEntry updateEntry = entry;
                    bool hasUpdateEntry = false;
                    if (item.TitleId.EndsWith("000") && item.TitleId.Length == 16)
                    {
                        string updateId = item.TitleId.Substring(0, 13) + "800";
                        if (_db.TryGetValue(updateId, out var upd))
                        {
                            updateEntry = upd;
                            hasUpdateEntry = true;
                        }
                    }

                    string? remoteVerStr = null;
                    string? remoteVerCode = null;

                    var dictToUse = updateEntry?.VersionsDictionary ?? entry?.VersionsDictionary;
                    
                    // Хак для базовых игр: в оригинальном файле CNMT версия всегда 0, даже если это v1.0.16878.
                    // Эмуляторы и Tinfoil сопоставляют отображаемую версию с ее кодом из TitleDB. Мы сделаем то же самое!
                    string currentVersionCode = item.VersionCode ?? "0";
                    if (App.Settings.Current.VersionOverrides.TryGetValue(item.TitleId ?? "", out string? manualVer) && !string.IsNullOrEmpty(manualVer))
                    {
                        currentVersionCode = manualVer;
                        App.MainDispatcher?.TryEnqueue(() => item.VersionCode = manualVer);
                    }

                    if (currentVersionCode == "0" && dictToUse != null && !string.IsNullOrEmpty(item.Version))
                    {
                        string localVerClean = item.Version.Trim('v', 'V', ' ').ToLowerInvariant();
                        var matchingPair = dictToUse.FirstOrDefault(kv => 
                            kv.Value != null && kv.Value.Trim('v', 'V', ' ').ToLowerInvariant() == localVerClean);
                        
                        if (matchingPair.Key != null)
                        {
                            currentVersionCode = matchingPair.Key;
                            App.MainDispatcher?.TryEnqueue(() => item.VersionCode = matchingPair.Key);
                        }
                    }

                    if (dictToUse != null && dictToUse.Count > 0)
                    {
                        var highest = dictToUse.LastOrDefault();
                        remoteVerStr = highest.Value;
                        remoteVerCode = highest.Key;
                    }
                    else
                    {
                        remoteVerStr = updateEntry?.Version ?? entry?.Version;
                    }

                    // Эвристический хак для сшитых файлов, если словаря версий нет и TitleDB не знает про патчи
                    if (currentVersionCode == "0" && !hasUpdateEntry && !string.IsNullOrEmpty(remoteVerStr) && !remoteVerStr.Contains("."))
                    {
                        // Если база TitleDB не знает о патчах (800), то remoteVerStr - это просто CDN версия базовой игры.
                        // Поэтому мы можем смело назначить ее нашему файлу, чтобы не выводить "0".
                        currentVersionCode = remoteVerStr;
                        App.MainDispatcher?.TryEnqueue(() => item.VersionCode = remoteVerStr);
                    }

                    if (!string.IsNullOrEmpty(remoteVerStr) && remoteVerStr != "0")
                    {
                        // Если это базовая игра и у нее НЕТ записи обновления (800) в базе TitleDB, 
                        // то обновлений физически не существует. Разница в версиях (например, 393216 vs 0) 
                        // это просто внутренняя версия CDN.
                        bool isOutdated = false;
                        if (item.TitleId != null && item.TitleId.EndsWith("000") && !hasUpdateEntry)
                        {
                            isOutdated = false;
                        }
                        else
                        {
                            remoteVerStr = remoteVerStr.TrimStart('v', 'V');

                        // Если версия с точками (например, 1.9.13726.20.65)
                        if (remoteVerStr.Contains("."))
                        {
                            string localVerStr = (item.Version ?? "").TrimStart('v', 'V');
                            
                            // Сравниваем по сегментам, так как System.Version поддерживает максимум 4
                            var remoteParts = remoteVerStr.Split('.');
                            var localParts = localVerStr.Split('.');
                            
                            int maxParts = Math.Max(remoteParts.Length, localParts.Length);
                            for (int i = 0; i < maxParts; i++)
                            {
                                int r = i < remoteParts.Length && int.TryParse(remoteParts[i], out int rv) ? rv : 0;
                                int l = i < localParts.Length && int.TryParse(localParts[i], out int lv) ? lv : 0;
                                
                                if (r > l)
                                {
                                    isOutdated = true;
                                    break;
                                }
                                else if (r < l)
                                {
                                    break;
                                }
                            }
                        }
                        // Иначе это числовой код (например, 65536)
                        else
                        {
                            string localCodeStr = currentVersionCode.TrimStart('v', 'V');
                            if (long.TryParse(localCodeStr, out long localVerCodeVal) && long.TryParse(remoteVerStr, out long remoteVerCodeVal))
                            {
                                if (remoteVerCodeVal > localVerCodeVal) isOutdated = true;
                            }
                        }
                        } // Конец else (!hasUpdateEntry)

                        if (isOutdated)
                        {
                            item.IsOutdated = true;
                            if (remoteVerStr.Contains(".")) {
                                item.UpdateVersion = remoteVerStr;
                                item.UpdateVersionCode = remoteVerCode ?? "Неизвестно";
                            } else {
                                item.UpdateVersion = "";
                                item.UpdateVersionCode = remoteVerStr;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(entry?.IconUrl))
                    {
                        try
                        {
                            var imgBytes = await _httpClient.GetByteArrayAsync(entry!.IconUrl);
                            var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                            using var ms = new System.IO.MemoryStream(imgBytes);
                            await bmp.SetSourceAsync(ms.AsRandomAccessStream());
                            item.CoverImage = bmp;
                            item.CoverBytes = imgBytes;
                        }
                        catch { }
                    }
                });
            }
        }

        public IEnumerable<TitleDbEntry> SearchTitles(string query)
        {
            if (!_isLoaded || string.IsNullOrWhiteSpace(query)) yield break;
            
            int count = 0;
            
            foreach (var kvp in _db)
            {
                var entry = kvp.Value;
                bool matchName = entry.Name != null && entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase);
                bool matchId = entry.Id != null && entry.Id.Contains(query, StringComparison.OrdinalIgnoreCase);
                
                if (matchName || matchId)
                {
                    // Ограничиваем выдачу 50 результатами, чтобы не перегружать UI
                    if (count++ > 50) yield break;
                    yield return entry;
                }
            }
        }

        private static bool HasGarbageCharacters(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
            {
                if (c == '\uFFFD' || c == '✦' || char.IsControl(c))
                    return true;
            }
            return false;
        }
    }
}
