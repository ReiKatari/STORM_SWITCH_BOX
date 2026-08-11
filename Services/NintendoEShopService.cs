using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace StormSwitchBox.Services
{
    public class EShopGameItem
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Publisher { get; set; } = "";
        public string Developer { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public string ReleaseDate { get; set; } = "";
        public string Genre { get; set; } = "";
        public List<string> Screenshots { get; set; } = new();
    }

    public class NintendoEShopService
    {
        private readonly HttpClient _httpClient;

        public NintendoEShopService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
        }

        public async Task<EShopGameItem?> SearchGameInfoAsync(string gameTitle)
        {
            if (string.IsNullOrWhiteSpace(gameTitle) || gameTitle == "Unknown Game" || gameTitle == "Unknown")
                return null;

            // Пробуем сначала RU (русское описание), затем EN (скриншоты и жанр)
            var ruResult = await SearchByLocaleAsync(gameTitle, "ru");
            var enResult = await SearchByLocaleAsync(gameTitle, "en");

            if (ruResult == null && enResult == null) return null;

            var result = ruResult ?? enResult ?? new EShopGameItem();

            // Мёрджим: русское описание + английские скриншоты/жанр
            if (enResult != null)
            {
                // Скриншоты — берём больше из EN, если RU не имеет
                if (result.Screenshots.Count == 0 && enResult.Screenshots.Count > 0)
                    result.Screenshots = enResult.Screenshots;

                // Жанр — если RU пусто, берём из EN
                if (string.IsNullOrEmpty(result.Genre) && !string.IsNullOrEmpty(enResult.Genre))
                    result.Genre = enResult.Genre;

                // Дата выхода
                if (string.IsNullOrEmpty(result.ReleaseDate) && !string.IsNullOrEmpty(enResult.ReleaseDate))
                    result.ReleaseDate = enResult.ReleaseDate;

                // Developer/Publisher fallback
                if (string.IsNullOrEmpty(result.Developer) && !string.IsNullOrEmpty(enResult.Developer))
                    result.Developer = enResult.Developer;
                if (string.IsNullOrEmpty(result.Publisher) && !string.IsNullOrEmpty(enResult.Publisher))
                    result.Publisher = enResult.Publisher;
            }

            return result;
        }

        private async Task<EShopGameItem?> SearchByLocaleAsync(string gameTitle, string locale)
        {
            try
            {
                string cleanQuery = CleanTitleForSearch(gameTitle);
                if (string.IsNullOrWhiteSpace(cleanQuery)) return null;

                string encodedQuery = Uri.EscapeDataString(cleanQuery);
                string url = $"https://search.nintendo-europe.com/{locale}/select?q={encodedQuery}&fq=type:GAME%20AND%20system_type:nintendo_switch*&rows=3&wt=json";

                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;

                string jsonStr = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(jsonStr);

                if (doc.RootElement.TryGetProperty("response", out var respProp) &&
                    respProp.TryGetProperty("docs", out var docsProp) &&
                    docsProp.ValueKind == JsonValueKind.Array &&
                    docsProp.GetArrayLength() > 0)
                {
                    var docElement = docsProp[0];
                    var item = new EShopGameItem();

                    if (docElement.TryGetProperty("title", out var titleProp))
                        item.Title = titleProp.GetString() ?? "";

                    // Описание — сначала excerpt, потом description, потом product_catalog_description_s
                    if (docElement.TryGetProperty("excerpt", out var excerptProp) && !string.IsNullOrWhiteSpace(excerptProp.GetString()))
                        item.Description = excerptProp.GetString() ?? "";
                    else if (docElement.TryGetProperty("description", out var descProp) && !string.IsNullOrWhiteSpace(descProp.GetString()))
                        item.Description = descProp.GetString() ?? "";
                    else if (docElement.TryGetProperty("product_catalog_description_s", out var catDescProp) && !string.IsNullOrWhiteSpace(catDescProp.GetString()))
                        item.Description = catDescProp.GetString() ?? "";

                    if (docElement.TryGetProperty("publisher", out var pubProp))
                        item.Publisher = pubProp.GetString() ?? "";

                    if (docElement.TryGetProperty("developer", out var devProp))
                        item.Developer = devProp.GetString() ?? "";

                    // Жанр — game_categories_txt или pretty_game_categories_txt
                    if (docElement.TryGetProperty("pretty_game_categories_txt", out var catsProp) && catsProp.ValueKind == JsonValueKind.Array)
                    {
                        var cats = new List<string>();
                        foreach (var c in catsProp.EnumerateArray())
                        {
                            var s = c.GetString();
                            if (!string.IsNullOrEmpty(s)) cats.Add(s);
                        }
                        if (cats.Count > 0) item.Genre = string.Join(", ", cats);
                    }
                    else if (docElement.TryGetProperty("game_categories_txt", out var gcProp) && gcProp.ValueKind == JsonValueKind.Array)
                    {
                        var cats = new List<string>();
                        foreach (var c in gcProp.EnumerateArray())
                        {
                            var s = c.GetString();
                            if (!string.IsNullOrEmpty(s)) cats.Add(s);
                        }
                        if (cats.Count > 0) item.Genre = string.Join(", ", cats);
                    }

                    if (docElement.TryGetProperty("image_url_sq_s", out var imgProp))
                        item.ImageUrl = imgProp.GetString() ?? "";

                    if (docElement.TryGetProperty("dates_released_dts", out var dateProp) && dateProp.ValueKind == JsonValueKind.Array && dateProp.GetArrayLength() > 0)
                    {
                        if (DateTime.TryParse(dateProp[0].GetString(), out var dt))
                            item.ReleaseDate = dt.ToString("dd.MM.yyyy");
                    }

                    // Скриншоты — собираем все доступные
                    // 1. screenshot_img_url_list (массив скриншотов)
                    if (docElement.TryGetProperty("screenshot_img_url_list", out var screenshotListProp) && screenshotListProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var screenshotUrl in screenshotListProp.EnumerateArray())
                        {
                            var s = screenshotUrl.GetString();
                            if (!string.IsNullOrEmpty(s))
                            {
                                item.Screenshots.Add(s.StartsWith("https:") ? s : "https:" + s);
                            }
                        }
                    }

                    // 2. image_url_h2x1_s (основной баннер)
                    if (docElement.TryGetProperty("image_url_h2x1_s", out var bannerProp) && !string.IsNullOrEmpty(bannerProp.GetString()))
                    {
                        string bannerUrl = bannerProp.GetString()!;
                        bannerUrl = bannerUrl.StartsWith("https:") ? bannerUrl : "https:" + bannerUrl;
                        if (!item.Screenshots.Contains(bannerUrl))
                            item.Screenshots.Add(bannerUrl);
                    }

                    // 3. image_url (дополнительное изображение)
                    if (docElement.TryGetProperty("image_url", out var imgUrlProp) && !string.IsNullOrEmpty(imgUrlProp.GetString()))
                    {
                        string imgUrl = imgUrlProp.GetString()!;
                        imgUrl = imgUrl.StartsWith("https:") ? imgUrl : "https:" + imgUrl;
                        if (!item.Screenshots.Contains(imgUrl))
                            item.Screenshots.Add(imgUrl);
                    }

                    return item;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NintendoEShopService] Search error ({locale}): {ex.Message}");
            }

            return null;
        }

        private static string CleanTitleForSearch(string rawTitle)
        {
            if (string.IsNullOrEmpty(rawTitle)) return "";
            
            // Удаляем паттерны вроде [0100...], v1.0, NSP, RUS, (1G+1U)
            string title = rawTitle;
            int bracketIdx = title.IndexOf('[');
            if (bracketIdx > 0) title = title.Substring(0, bracketIdx);

            int parenIdx = title.IndexOf('(');
            if (parenIdx > 0) title = title.Substring(0, parenIdx);

            title = title.Replace("®", "").Replace("™", "").Replace("©", "").Trim();
            return title;
        }
    }
}
