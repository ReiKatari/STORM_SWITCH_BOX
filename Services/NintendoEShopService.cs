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
        public List<string> Screenshots { get; set; } = new();
    }

    public class NintendoEShopService
    {
        private readonly HttpClient _httpClient;

        public NintendoEShopService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            _httpClient.Timeout = TimeSpan.FromSeconds(8);
        }

        public async Task<EShopGameItem?> SearchGameInfoAsync(string gameTitle)
        {
            if (string.IsNullOrWhiteSpace(gameTitle) || gameTitle == "Unknown Game" || gameTitle == "Unknown")
                return null;

            try
            {
                // Очищаем название от расширений и мусора
                string cleanQuery = CleanTitleForSearch(gameTitle);
                if (string.IsNullOrWhiteSpace(cleanQuery)) return null;

                string encodedQuery = Uri.EscapeDataString(cleanQuery);
                string url = $"https://search.nintendo-europe.com/ru/select?q={encodedQuery}&fq=type:GAME%20AND%20system_type:nintendo_switch*&rows=3&wt=json";

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

                    if (docElement.TryGetProperty("excerpt", out var excerptProp) && !string.IsNullOrEmpty(excerptProp.GetString()))
                        item.Description = excerptProp.GetString() ?? "";

                    if (docElement.TryGetProperty("publisher", out var pubProp))
                        item.Publisher = pubProp.GetString() ?? "";

                    if (docElement.TryGetProperty("developer", out var devProp))
                        item.Developer = devProp.GetString() ?? "";

                    if (docElement.TryGetProperty("image_url_sq_s", out var imgProp))
                        item.ImageUrl = imgProp.GetString() ?? "";

                    if (docElement.TryGetProperty("dates_released_dts", out var dateProp) && dateProp.ValueKind == JsonValueKind.Array && dateProp.GetArrayLength() > 0)
                    {
                        if (DateTime.TryParse(dateProp[0].GetString(), out var dt))
                            item.ReleaseDate = dt.ToString("dd.MM.yyyy");
                    }

                    // Скриншоты
                    if (docElement.TryGetProperty("image_url_h2x1_s", out var bannerProp) && !string.IsNullOrEmpty(bannerProp.GetString()))
                    {
                        item.Screenshots.Add(bannerProp.GetString()!);
                    }

                    return item;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NintendoEShopService] Search error: {ex.Message}");
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
