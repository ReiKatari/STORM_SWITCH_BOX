using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public class TitleDbEntry
    {
        internal static bool TryParseVersionKey(string? key, out uint versionNum)
        {
            versionNum = 0;
            if (string.IsNullOrWhiteSpace(key)) return false;
            string k = key.Trim();
            if (k.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return uint.TryParse(k.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out versionNum);
            if (uint.TryParse(k, NumberStyles.Integer, CultureInfo.InvariantCulture, out versionNum))
                return true;
            return uint.TryParse(k, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out versionNum);
        }

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
                    // Берём максимальную версию по числовому uint-ключу
                    if (VersionsDictionary != null && VersionsDictionary.Count > 0)
                    {
                        var highest = VersionsDictionary.MaxBy(kv => TryParseVersionKey(kv.Key, out uint v) ? v : 0u);
                        if (!string.IsNullOrEmpty(highest.Value)) return highest.Value;
                    }
                    if (VersionElement?.ValueKind == JsonValueKind.Number) return VersionElement.Value.GetDouble().ToString(CultureInfo.InvariantCulture);
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
        private Dictionary<string, TitleDbEntry> _byNormalizedName = new(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, TitleDbEntry> _byCoreName = new(StringComparer.OrdinalIgnoreCase);
        private bool _isLoaded = false;
        public bool IsLoaded => _isLoaded;
        private readonly SemaphoreSlim _dbLoadLock = new(1, 1);
        private readonly NintendoEShopService _eShopService;

        public event Action? DatabaseLoaded;

        // URL для загрузки базы TitleDB (RU мертва - tinfoil 523, github 404)
        // Используем общую базу + US зеркало
        private const string DbUrl = "https://tinfoil.media/repo/db/titles.json";
        private const string FallbackDbUrl = "https://raw.githubusercontent.com/blawar/titledb/master/US.en.json";

        public sealed class ObservedVersionEntry
        {
            public string Version { get; set; } = string.Empty;
            public bool Authoritative { get; set; }
        }

        private readonly ConcurrentDictionary<string, ObservedVersionEntry> _observedDisplayVersions = new(StringComparer.OrdinalIgnoreCase);
        private static readonly string _observedVersionsCachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "STORM SWITCH BOX",
            "cache",
            "known_display_versions_v2.json");
        private static readonly string _legacyObservedVersionsCachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "STORM SWITCH BOX",
            "cache",
            "known_display_versions.json");
        private int _observedVersionsSaveScheduled = 0;
        private readonly ConcurrentDictionary<string, byte> _stormOnlineLookupAttempted = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Regex StormTitleTripleRegex = new(
            @"\(\s*[vV]?\.?\s*(?<dv>\d{1,3}\.\d{1,4}(?:\.\d{1,6}){0,2})\s*-\s*(?<iv>\d{5,10})\s*-\s*(?<tid>[0-9A-Fa-f]{16})\s*\)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public TitleDbService()
        {
            _dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "titledb.json");
            _eShopService = new NintendoEShopService();
            LoadObservedDisplayVersions();
            
            // Асинхронно загружаем базу из локального кэша
            _ = LoadLocalDbAsync();
        }

        public static string EnsureRussianDescription(CatalogItem item, TitleDbEntry? entry = null)
        {
            // Приоритет: 1) eShop описание (русское), 2) TitleDB описание (русское), 3) TitleDB описание (оригинал), 4) заглушка
            string desc = entry?.Description ?? item.Description;
            
            // Если описание есть и оно не пустое/не заглушка - возвращаем как есть
            if (!string.IsNullOrWhiteSpace(desc) && desc != "Нет описания" && desc != "No description available.")
            {
                return desc.Trim();
            }
            
            return "Описание не найдено.";
        }


        private static bool ContainsRussian(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
            {
                if ((c >= 'а' && c <= 'я') || (c >= 'А' && c <= 'Я') || c == 'ё' || c == 'Ё')
                    return true;
            }
            return false;
        }

        public bool IsDatabaseFresh()
        {
            if (!File.Exists(_dbPath)) return false;
            var fi = new FileInfo(_dbPath);
            return (DateTime.Now - fi.LastWriteTime).TotalDays < 1;
        }

        public IReadOnlyDictionary<string, TitleDbEntry> Database => _db;
        public IEnumerable<TitleDbEntry> GetAllEntries() => _db.Values;

        public static string NormalizeTitleLookupKey(string? rawName, bool stripEditionWords = false)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return string.Empty;

            string s = rawName
                .Replace("™", " ")
                .Replace("®", " ")
                .Replace("©", " ")
                .Replace("&", " and ")
                .Replace("’", "'")
                .ToLowerInvariant();

            s = System.Text.RegularExpressions.Regex.Replace(s, @"\[[^\]]*\]|\([^)]*\)", " ");
            if (stripEditionWords)
            {
                s = System.Text.RegularExpressions.Regex.Replace(
                    s,
                    @"\b(definitive|deluxe|complete|ultimate|gold|goty|edition|remastered|hd|collection|nintendo|switch|version|digital)\b",
                    " ");
            }
            s = System.Text.RegularExpressions.Regex.Replace(s, @"[^a-z0-9а-яё]+", " ");
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();
            return s;
        }

        public bool TryFindTitleByName(string? gameName, out TitleDbEntry? entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(gameName) || !_isLoaded)
                return false;

            string norm = NormalizeTitleLookupKey(gameName, stripEditionWords: false);
            if (!string.IsNullOrEmpty(norm) && _byNormalizedName.TryGetValue(norm, out entry) && entry != null)
                return true;

            string core = NormalizeTitleLookupKey(gameName, stripEditionWords: true);
            if (!string.IsNullOrEmpty(core) && core.Length >= 3)
            {
                if (_byCoreName.TryGetValue(core, out entry) && entry != null)
                    return true;
                if (_byNormalizedName.TryGetValue(core, out entry) && entry != null)
                    return true;
            }

            return false;
        }


        private async Task LoadLocalDbAsync()
        {
            await _dbLoadLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var reindexedDb = new Dictionary<string, TitleDbEntry>(StringComparer.OrdinalIgnoreCase);

                void MergeEntries(Dictionary<string, TitleDbEntry>? rawDb)
                {
                    if (rawDb == null) return;
                    foreach (var kvp in rawDb)
                    {
                        var entry = kvp.Value;
                        if (entry == null) continue;
                        if (string.IsNullOrEmpty(entry.Id))
                        {
                            entry.Id = kvp.Key;
                        }
                        reindexedDb[kvp.Key] = entry;
                        if (!string.IsNullOrEmpty(entry.Id))
                        {
                            reindexedDb[entry.Id.ToUpperInvariant()] = entry;
                        }
                    }
                }

                async Task MergeJsonDbFileAsync(string filePath)
                {
                    if (!File.Exists(filePath)) return;
                    try
                    {
                        using var stream = File.OpenRead(filePath);
                        var rawDb = await JsonSerializer.DeserializeAsync<Dictionary<string, TitleDbEntry>>(stream).ConfigureAwait(false);
                        MergeEntries(rawDb);
                    }
                    catch { }
                }

                async Task MergeZstJsonDbFileAsync(string filePath)
                {
                    if (!File.Exists(filePath)) return;
                    try
                    {
                        using var stream = File.OpenRead(filePath);
                        using var zstd = new ZstdSharp.DecompressionStream(stream);
                        var rawDb = await JsonSerializer.DeserializeAsync<Dictionary<string, TitleDbEntry>>(zstd).ConfigureAwait(false);
                        MergeEntries(rawDb);
                    }
                    catch { }
                }

                // 1. Загружаем встроенную базу из tools/db/nutdb.json.zst с fallback на nutdb.json
                string? nutDbZstPath = StormToolResolver.ResolveNutDbZst()
                    ?? StormToolResolver.ResolveTool(Path.Combine("tools", "db", "nutdb.json.zst"));
                string? nutDbJsonPath = StormToolResolver.ResolveNutDbJson()
                    ?? StormToolResolver.ResolveTool(Path.Combine("tools", "db", "nutdb.json"));

                if (!string.IsNullOrEmpty(nutDbZstPath) && File.Exists(nutDbZstPath))
                {
                    await MergeZstJsonDbFileAsync(nutDbZstPath).ConfigureAwait(false);
                }

                if (reindexedDb.Count == 0 && !string.IsNullOrEmpty(nutDbJsonPath) && File.Exists(nutDbJsonPath))
                {
                    await MergeJsonDbFileAsync(nutDbJsonPath).ConfigureAwait(false);
                }

                // 2. Поверх накладываем свежий пользовательский кэш .switch/titledb.json (если он обновлялся из сети)
                string? activeBuiltInPath = !string.IsNullOrEmpty(nutDbZstPath) ? nutDbZstPath : nutDbJsonPath;
                if (File.Exists(_dbPath) &&
                    (string.IsNullOrEmpty(activeBuiltInPath) ||
                     !string.Equals(Path.GetFullPath(_dbPath), Path.GetFullPath(activeBuiltInPath), StringComparison.OrdinalIgnoreCase)))
                {
                    await MergeJsonDbFileAsync(_dbPath).ConfigureAwait(false);
                }

                if (reindexedDb.Count > 0)
                {
                    var normMap = new Dictionary<string, TitleDbEntry>(StringComparer.OrdinalIgnoreCase);
                    var coreMap = new Dictionary<string, TitleDbEntry>(StringComparer.OrdinalIgnoreCase);
                    foreach (var entry in reindexedDb.Values)
                    {
                        if (entry == null || string.IsNullOrWhiteSpace(entry.Name) || string.IsNullOrWhiteSpace(entry.Id))
                            continue;
                        string tidUpper = entry.Id.Trim().ToUpperInvariant();
                        if (tidUpper.Length != 16 || !tidUpper.EndsWith("000", StringComparison.Ordinal) || entry.Name.Contains("(Demo)", StringComparison.OrdinalIgnoreCase) || entry.Name.EndsWith(" Demo", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string normKey = NormalizeTitleLookupKey(entry.Name, stripEditionWords: false);
                        if (!string.IsNullOrEmpty(normKey) && !normMap.ContainsKey(normKey))
                            normMap[normKey] = entry;

                        string coreKey = NormalizeTitleLookupKey(entry.Name, stripEditionWords: true);
                        if (!string.IsNullOrEmpty(coreKey) && !coreMap.ContainsKey(coreKey))
                            coreMap[coreKey] = entry;
                    }

                    _db = reindexedDb;
                    _byNormalizedName = normMap;
                    _byCoreName = coreMap;
                    _isLoaded = true;
                    DatabaseLoaded?.Invoke();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка загрузки локальной БД TitleDB: {ex.Message}");
            }
            finally
            {
                _dbLoadLock.Release();
            }
        }

        public async Task<bool> UpdateDatabaseAsync(IProgress<int>? progress = null)
        {
            string[] mirrors = new[]
            {
                "https://raw.githubusercontent.com/blawar/titledb/master/ru.json",
                FallbackDbUrl,
                DbUrl,
                "https://ghfast.top/https://raw.githubusercontent.com/blawar/titledb/master/ru.json"
            };

            string tmpPath = _dbPath + ".tmp";
            foreach (var mirrorUrl in mirrors)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                    using var req = new HttpRequestMessage(HttpMethod.Get, mirrorUrl);
                    req.Headers.TryAddWithoutValidation("User-Agent", $"StormSwitchBox/{App.AppVersionString}");
                    using var response = await StormHttpClientFactory.Shared.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode) continue;

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
                    using (var fileStream = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var stream = await response.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false))
                    {
                        var buffer = new byte[16384];
                        long totalRead = 0;
                        int read;
                        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, read, cts.Token).ConfigureAwait(false);
                            totalRead += read;
                            if (totalBytes > 0 && progress != null)
                            {
                                progress.Report((int)((totalRead * 100) / totalBytes));
                            }
                        }
                    }

                    if (File.Exists(tmpPath) && new FileInfo(tmpPath).Length > 1024)
                    {
                        File.Move(tmpPath, _dbPath, true);
                        await LoadLocalDbAsync().ConfigureAwait(false);
                        return true;
                    }
                    else if (File.Exists(tmpPath))
                    {
                        try { File.Delete(tmpPath); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка зеркала TitleDB ({mirrorUrl}): {ex.Message}");
                    if (File.Exists(tmpPath))
                    {
                        try { File.Delete(tmpPath); } catch { }
                    }
                }
            }

            return false;
        }

        public TitleDbEntry? GetEntry(string titleId) => TryGetEntry(titleId, out var entry) ? entry : null;
        public bool TryGetEntry(string titleId, out TitleDbEntry? entry) => TryGetTitleInfo(titleId, out entry);

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

        public class DlcCompletenessReport
        {
            public string BaseTitleId { get; set; } = "";
            public int TotalAvailableInDb { get; set; }
            public int FoundInPackage { get; set; }
            public double Percentage => TotalAvailableInDb > 0 ? Math.Min(100.0, (double)FoundInPackage / TotalAvailableInDb * 100.0) : 100.0;
            public List<TitleDbEntry> MissingDlcs { get; set; } = new();
            public List<TitleDbEntry> PresentDlcs { get; set; } = new();
            public bool IsComplete => TotalAvailableInDb > 0 && MissingDlcs.Count == 0;
            public string SummaryText { get; set; } = "";
        }

        public DlcCompletenessReport CheckDlcCompleteness(string baseTitleId, IEnumerable<string> presentDlcIds)
        {
            var report = new DlcCompletenessReport { BaseTitleId = baseTitleId };
            var availableDlcs = GetDlcs(baseTitleId);
            report.TotalAvailableInDb = availableDlcs.Count;

            var presentSet = new HashSet<string>(presentDlcIds.Select(id => id.Trim().ToUpperInvariant()), StringComparer.OrdinalIgnoreCase);
            report.FoundInPackage = presentSet.Count;

            foreach (var dlc in availableDlcs)
            {
                string dlcTid = (dlc.Id ?? "").Trim().ToUpperInvariant().PadLeft(16, '0');
                if (presentSet.Contains(dlcTid))
                {
                    report.PresentDlcs.Add(dlc);
                }
                else
                {
                    report.MissingDlcs.Add(dlc);
                }
            }

            if (report.TotalAvailableInDb == 0)
            {
                report.SummaryText = "В базе TitleDB нет сведений о дополнительных DLC.";
            }
            else if (report.IsComplete)
            {
                report.SummaryText = $"Комплект DLC полон на 100%! В образе присутствуют все {report.TotalAvailableInDb} доступных DLC.";
            }
            else
            {
                report.SummaryText = $"Собрано {report.FoundInPackage} из {report.TotalAvailableInDb} DLC ({report.Percentage:F1}%). Отсутствует {report.MissingDlcs.Count} DLC.";
            }

            return report;
        }

        public void EnrichCatalogItem(CatalogItem item)
        {
            bool hasTitleDbEntry = TryGetTitleInfo(item.TitleId, out var entry) && entry != null;

            App.RunOnUI(async () =>
            {
                if (hasTitleDbEntry && entry != null)
                {
                    if (item.TitleName == "Unknown Game" || item.TitleName == "Unknown" || string.IsNullOrEmpty(item.TitleName) || HasGarbageCharacters(item.TitleName))
                        item.TitleName = entry.Name ?? item.TitleName;

                    item.Description = EnsureRussianDescription(item, entry);

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

                    if (item.Regions == "UNKNOWN")
                    {
                        if (!string.IsNullOrEmpty(entry.Regions))
                        {
                            item.Regions = entry.Regions;
                        }
                        else
                        {
                            var regMatch = System.Text.RegularExpressions.Regex.Match(item.FileName ?? "", @"\[(US|WW|EU|JP|KR|UK|AS)\]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (regMatch.Success) item.Regions = regMatch.Groups[1].Value.ToUpper();
                            else item.Regions = "WW";
                        }
                    }

                    if (!string.IsNullOrEmpty(entry.Publisher) && (item.Publisher == "Unknown" || string.IsNullOrEmpty(item.Publisher)))
                        item.Publisher = entry.Publisher;
                }

                // Включаем безопасное распределение разработчика если он не задан
                if (string.IsNullOrEmpty(item.Developer) || item.Developer == "Unknown")
                {
                    item.Developer = !string.IsNullOrEmpty(item.Publisher) && item.Publisher != "Unknown" ? item.Publisher : "Nintendo";
                }

                // Гарантируем уникальное правильное описание
                item.Description = EnsureRussianDescription(item, entry);

                // Загружаем eShop метаданные (описание на русском, скриншоты, жанр, дата)
                _ = Task.Run(async () =>
                {
                    var eshopData = await _eShopService.SearchGameInfoAsync(item.TitleName);
                    if (eshopData != null)
                    {
                        App.RunOnUI(() =>
                        {
                            // Описание - eShop RU имеет приоритет
                            if (!string.IsNullOrWhiteSpace(eshopData.Description) && (string.IsNullOrEmpty(entry?.Description) || !ContainsRussian(entry.Description)))
                            {
                                item.Description = eshopData.Description;
                            }
                            if (!string.IsNullOrWhiteSpace(eshopData.Developer))
                            {
                                item.Developer = eshopData.Developer;
                            }
                            if (!string.IsNullOrWhiteSpace(eshopData.Publisher) && (item.Publisher == "Unknown" || string.IsNullOrEmpty(item.Publisher)))
                            {
                                item.Publisher = eshopData.Publisher;
                            }
                            // Дата выхода
                            if (!string.IsNullOrWhiteSpace(eshopData.ReleaseDate) && (string.IsNullOrEmpty(item.ReleaseDate) || item.ReleaseDate == "N/A"))
                            {
                                item.ReleaseDate = eshopData.ReleaseDate;
                            }
                            // Жанр из eShop
                            if (!string.IsNullOrWhiteSpace(eshopData.Genre) && (string.IsNullOrEmpty(item.Category) || item.Category == "N/A"))
                            {
                                item.Category = eshopData.Genre;
                            }

                            // Скриншоты из eShop
                            if (eshopData.Screenshots.Count > 0)
                            {
                                item.Screenshots.Clear();
                                foreach (var shotUrl in eshopData.Screenshots)
                                {
                                    try
                                    {
                                        var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage { DecodePixelWidth = 640, DecodePixelType = Microsoft.UI.Xaml.Media.Imaging.DecodePixelType.Logical, UriSource = new Uri(shotUrl) };
                                        item.Screenshots.Add(bmp);
                                        item.HasScreenshots = true;
                                    }
                                    catch { }
                                }
                            }
                        });
                    }
                });

                await System.Threading.Tasks.Task.Run(() => 
                {
                    var dlcs = GetDlcs(item.TitleId);
                    App.RunOnUI(() =>
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
                    TitleDbEntry? updateEntry = entry;
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
                        App.RunOnUI(() => item.VersionCode = manualVer);
                    }

                    // Если отображаемая версия базовая, а код версии известен (из CNMT или файла), проверяем базу TitleDB
                    if (CatalogScannerService.IsBaseVersion(item.Version) && dictToUse != null)
                    {
                        if (dictToUse.TryGetValue(currentVersionCode, out string? dbDisplayVer) && !CatalogScannerService.IsBaseVersion(dbDisplayVer))
                        {
                            App.RunOnUI(() => item.Version = CatalogScannerService.CleanVersion(dbDisplayVer));
                        }
                        else if (uint.TryParse(currentVersionCode, out uint numC))
                        {
                            string hexC = numC.ToString("X");
                            if (dictToUse.TryGetValue(hexC, out string? hexDbVer) && !CatalogScannerService.IsBaseVersion(hexDbVer))
                            {
                                App.RunOnUI(() =>
                                {
                                    item.Version = CatalogScannerService.CleanVersion(hexDbVer);
                                    item.VersionCode = hexC;
                                });
                                currentVersionCode = hexC;
                            }
                        }
                    }

                    if (currentVersionCode == "0" && dictToUse != null && !string.IsNullOrEmpty(item.Version))
                    {
                        string localVerClean = item.Version.Trim('v', 'V', ' ').ToLowerInvariant();
                        var matchingPair = dictToUse.FirstOrDefault(kv => 
                            kv.Value != null && kv.Value.Trim('v', 'V', ' ').ToLowerInvariant() == localVerClean);
                        
                        if (matchingPair.Key != null)
                        {
                            currentVersionCode = matchingPair.Key;
                            App.RunOnUI(() => item.VersionCode = matchingPair.Key);
                        }
                    }

                    if (dictToUse != null && dictToUse.Count > 0)
                    {
                        var highest = dictToUse.MaxBy(kv => TitleDbEntry.TryParseVersionKey(kv.Key, out uint v) ? v : 0u);
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
                        App.RunOnUI(() => item.VersionCode = remoteVerStr);
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

                    if ((item.CoverBytes == null || item.CoverBytes.Length == 0) &&
                        item.CoverImage == null &&
                        !string.IsNullOrEmpty(entry?.IconUrl))
                    {
                        try
                        {
                            using var imgCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                            var imgBytes = await StormHttpClientFactory.Shared.GetByteArrayAsync(entry.IconUrl, imgCts.Token);
                            if (StormHttpClientFactory.IsValidImageBytes(imgBytes))
                            {
                                var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage { DecodePixelWidth = 256, DecodePixelType = Microsoft.UI.Xaml.Media.Imaging.DecodePixelType.Logical };
                                using var ms = new System.IO.MemoryStream(imgBytes);
                                await bmp.SetSourceAsync(ms.AsRandomAccessStream());
                                item.CoverImage = bmp;
                                item.CoverBytes = imgBytes;
                            }
                        }
                        catch { }
                    }
                });
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

        public static bool IsValidSemanticVersion(string? version)
        {
            if (string.IsNullOrWhiteSpace(version)) return false;
            string v = Regex.Replace(version.Trim(), @"^[vV]+\.?\s*", "");
            var m = Regex.Match(v, @"^(?<maj>\d{1,3})\.(?<min>\d{1,4})(?:\.(?<pat>\d{1,6}))?(?:\.(?<bld>\d{1,6}))?$");
            if (!m.Success) return false;
            if (!int.TryParse(m.Groups["maj"].Value, out int maj) || maj >= 100) return false;
            if (!m.Groups["bld"].Success && m.Groups["pat"].Success && m.Groups["pat"].Value.Length == 4 &&
                int.TryParse(m.Groups["pat"].Value, out int y) && y >= 2015 && y <= 2099)
                return false;
            return true;
        }

        public static int CompareSemanticVersions(string? a, string? b)
        {
            string va = Regex.Replace((a ?? string.Empty).Trim(), @"^[vV]+\.?\s*", "");
            string vb = Regex.Replace((b ?? string.Empty).Trim(), @"^[vV]+\.?\s*", "");
            if (string.IsNullOrEmpty(va) && string.IsNullOrEmpty(vb)) return 0;
            if (string.IsNullOrEmpty(va)) return -1;
            if (string.IsNullOrEmpty(vb)) return 1;

            var pa = va.Split('.');
            var pb = vb.Split('.');
            int maxLen = Math.Max(pa.Length, pb.Length);
            for (int i = 0; i < maxLen; i++)
            {
                int ia = (i < pa.Length && int.TryParse(Regex.Replace(pa[i], @"[^\d]", ""), out int vaNum)) ? vaNum : 0;
                int ib = (i < pb.Length && int.TryParse(Regex.Replace(pb[i], @"[^\d]", ""), out int vbNum)) ? vbNum : 0;
                if (ia != ib) return ia.CompareTo(ib);
            }
            return 0;
        }

        private void LoadObservedDisplayVersions()
        {
            try
            {
                // Purge legacy unverified cache (<= 8.0.11) so poisoned old-update versions cannot persist
                if (File.Exists(_legacyObservedVersionsCachePath))
                {
                    try { File.Delete(_legacyObservedVersionsCachePath); } catch { }
                }

                if (!File.Exists(_observedVersionsCachePath)) return;
                string json = File.ReadAllText(_observedVersionsCachePath);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, ObservedVersionEntry>>(json);
                if (loaded == null) return;
                foreach (var kvp in loaded)
                {
                    if (!string.IsNullOrWhiteSpace(kvp.Key) && kvp.Value != null && IsValidSemanticVersion(kvp.Value.Version))
                    {
                        _observedDisplayVersions[kvp.Key] = new ObservedVersionEntry
                        {
                            Version = kvp.Value.Version.Trim().TrimStart('v', 'V'),
                            Authoritative = kvp.Value.Authoritative
                        };
                    }
                }
            }
            catch { }
        }

        private void ScheduleSaveObservedDisplayVersions()
        {
            if (Interlocked.Exchange(ref _observedVersionsSaveScheduled, 1) == 0)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(1200).ConfigureAwait(false);
                        var dir = Path.GetDirectoryName(_observedVersionsCachePath);
                        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                        var snap = new Dictionary<string, ObservedVersionEntry>(_observedDisplayVersions, StringComparer.OrdinalIgnoreCase);
                        string outJson = JsonSerializer.Serialize(snap);
                        string tmp = _observedVersionsCachePath + ".tmp";
                        File.WriteAllText(tmp, outJson);
                        File.Move(tmp, _observedVersionsCachePath, true);
                    }
                    catch { }
                    finally
                    {
                        Interlocked.Exchange(ref _observedVersionsSaveScheduled, 0);
                    }
                });
            }
        }

        public void RecordObservedDisplayVersion(string? titleId, string? versionCode, string? displayVersion, bool isAuthoritative = false)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(titleId) || string.IsNullOrWhiteSpace(versionCode) || string.IsNullOrWhiteSpace(displayVersion))
                    return;
                string baseTid = RuTrackerModService.NormalizeSwitchBaseApplicationTitleId(titleId);
                if (baseTid.Length != 16) return;
                if (!uint.TryParse(Regex.Replace(versionCode.Trim(), @"[^\d]", ""), out uint patchCode) || patchCode == 0)
                    return;
                string cleanVer = Regex.Replace(displayVersion.Trim(), @"^[vV]+\.?\s*", "");
                if (!IsValidSemanticVersion(cleanVer)) return;

                string tidPrefix = baseTid + ":";
                foreach (var kvp in _observedDisplayVersions.ToArray())
                {
                    if (!kvp.Key.StartsWith(tidPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!uint.TryParse(kvp.Key.AsSpan(tidPrefix.Length), out uint otherPatch) || otherPatch == patchCode) continue;

                    if (otherPatch < patchCode)
                    {
                        if (!isAuthoritative && CompareSemanticVersions(cleanVer, kvp.Value.Version) < 0)
                        {
                            // Reject non-authoritative downgrade below an already observed smaller patch version
                            return;
                        }
                    }
                    else
                    {
                        if (!kvp.Value.Authoritative && CompareSemanticVersions(kvp.Value.Version, cleanVer) < 0)
                        {
                            _observedDisplayVersions.TryRemove(kvp.Key, out _);
                        }
                    }
                }

                string key = $"{baseTid}:{patchCode}";
                if (_observedDisplayVersions.TryGetValue(key, out var existing))
                {
                    if (existing.Authoritative && !isAuthoritative)
                        return;
                    if (!isAuthoritative && CompareSemanticVersions(cleanVer, existing.Version) <= 0)
                        return;
                    if (existing.Authoritative && isAuthoritative && string.Equals(existing.Version, cleanVer, StringComparison.OrdinalIgnoreCase))
                        return;
                }

                _observedDisplayVersions[key] = new ObservedVersionEntry
                {
                    Version = cleanVer,
                    Authoritative = isAuthoritative
                };
                ScheduleSaveObservedDisplayVersions();
            }
            catch { }
        }

        public bool TryResolveAuthoritativeDisplayVersion(string? titleId, int patchCode, out string displayVersion)
        {
            displayVersion = string.Empty;
            if (string.IsNullOrWhiteSpace(titleId) || patchCode <= 0) return false;
            string baseTid = RuTrackerModService.NormalizeSwitchBaseApplicationTitleId(titleId);
            if (baseTid.Length != 16) return false;

            string cacheKey = $"{baseTid}:{patchCode}";
            if (_observedDisplayVersions.TryGetValue(cacheKey, out var cached) &&
                cached != null && cached.Authoritative && IsValidSemanticVersion(cached.Version))
            {
                displayVersion = cached.Version;
                return true;
            }

            string patchCodeStr = patchCode.ToString(CultureInfo.InvariantCulture);
            if (TryGetTitleInfo(baseTid, out var baseEntry) && baseEntry?.VersionsDictionary != null &&
                baseEntry.VersionsDictionary.TryGetValue(patchCodeStr, out var baseSem) &&
                IsValidSemanticVersion(baseSem))
            {
                displayVersion = baseSem.Trim().TrimStart('v', 'V');
                return true;
            }

            string updateTid = TitleIdHelper.TryParseHex16(baseTid, out ulong baseTidNum) ? TitleIdHelper.ComputePatchTitleId(baseTidNum).ToString("X16") : string.Empty;
            if (!string.IsNullOrEmpty(updateTid) && TryGetTitleInfo(updateTid, out var updEntry) &&
                updEntry?.VersionsDictionary != null &&
                updEntry.VersionsDictionary.TryGetValue(patchCodeStr, out var updSem) &&
                IsValidSemanticVersion(updSem))
            {
                displayVersion = updSem.Trim().TrimStart('v', 'V');
                return true;
            }

            return false;
        }

        public bool TryResolveDisplayVersion(string? titleId, int patchCode, out string displayVersion)
        {
            if (TryResolveAuthoritativeDisplayVersion(titleId, patchCode, out displayVersion))
                return true;

            displayVersion = string.Empty;
            if (string.IsNullOrWhiteSpace(titleId) || patchCode <= 0) return false;
            string baseTid = RuTrackerModService.NormalizeSwitchBaseApplicationTitleId(titleId);
            if (baseTid.Length != 16) return false;

            string cacheKey = $"{baseTid}:{patchCode}";
            if (_observedDisplayVersions.TryGetValue(cacheKey, out var cached) &&
                cached != null && IsValidSemanticVersion(cached.Version))
            {
                displayVersion = cached.Version;
                return true;
            }

            return false;
        }

        public async Task<string?> TryResolveDisplayVersionOnlineAsync(
            string? titleId,
            int patchCode,
            string? gameTitleHint,
            CancellationToken ct = default)
        {
            if (TryResolveDisplayVersion(titleId, patchCode, out var existing) && IsValidSemanticVersion(existing))
                return existing;

            if (patchCode <= 0 || string.IsNullOrWhiteSpace(titleId))
                return null;

            string baseTid = RuTrackerModService.NormalizeSwitchBaseApplicationTitleId(titleId);
            if (baseTid.Length != 16)
                return null;

            string attemptKey = $"{baseTid}:{patchCode}";
            if (!_stormOnlineLookupAttempted.TryAdd(attemptKey, 1))
                return null;

            string rawHint = !string.IsNullOrWhiteSpace(gameTitleHint)
                ? gameTitleHint
                : (TryGetTitleInfo(baseTid, out var dbEntry) && !string.IsNullOrWhiteSpace(dbEntry?.Name) ? dbEntry.Name : string.Empty);

            if (string.IsNullOrWhiteSpace(rawHint))
                return null;

            string cleanQuery = Regex.Replace(rawHint, @"\[[^\]]*\]|\([^)]*\)", " ");
            cleanQuery = Regex.Replace(cleanQuery, @"[^\p{L}\p{Nd}\s:+&'.\-]", " ");
            cleanQuery = Regex.Replace(cleanQuery, @"\s+", " ").Trim(' ', '-', ':');
            if (cleanQuery.Length > 60)
                cleanQuery = cleanQuery.Substring(0, 60).Trim();

            if (cleanQuery.Length < 2)
                return null;

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(4.5));

                string url = $"https://stormgamesworld.ru/api/games?search={Uri.EscapeDataString(cleanQuery)}";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.TryAddWithoutValidation("Accept", "application/json");
                using var resp = await StormHttpClientFactory.Shared.SendAsync(req, cts.Token).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                    return null;

                string json = await resp.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                using var doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;
                JsonElement arrayEl = default;
                if (root.ValueKind == JsonValueKind.Array)
                {
                    arrayEl = root;
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("games", out var gEl) && gEl.ValueKind == JsonValueKind.Array)
                        arrayEl = gEl;
                    else if (root.TryGetProperty("items", out var iEl) && iEl.ValueKind == JsonValueKind.Array)
                        arrayEl = iEl;
                    else
                        return null;
                }
                else
                {
                    return null;
                }

                string? bestVer = null;
                string[] candidateFields = new[] { "finalTitle", "title", "correctTitle", "correctTitle2" };

                foreach (var item in arrayEl.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;

                    foreach (var fieldName in candidateFields)
                    {
                        if (!item.TryGetProperty(fieldName, out var fieldEl) || fieldEl.ValueKind != JsonValueKind.String)
                            continue;

                        string? titleVal = fieldEl.GetString();
                        if (string.IsNullOrWhiteSpace(titleVal)) continue;

                        foreach (Match m in StormTitleTripleRegex.Matches(titleVal))
                        {
                            string foundTid = RuTrackerModService.NormalizeSwitchBaseApplicationTitleId(m.Groups["tid"].Value);
                            if (!string.Equals(foundTid, baseTid, StringComparison.OrdinalIgnoreCase))
                                continue;

                            if (!int.TryParse(m.Groups["iv"].Value, out int foundIv) || foundIv != patchCode)
                                continue;

                            string candVer = m.Groups["dv"].Value.Trim().TrimStart('v', 'V');
                            if (!IsValidSemanticVersion(candVer))
                                continue;

                            if (string.IsNullOrEmpty(bestVer) || CompareSemanticVersions(candVer, bestVer) > 0)
                                bestVer = candVer;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(bestVer))
                {
                    RecordObservedDisplayVersion(baseTid, patchCode.ToString(CultureInfo.InvariantCulture), bestVer, isAuthoritative: false);
                    return bestVer;
                }
            }
            catch { }

            return null;
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
