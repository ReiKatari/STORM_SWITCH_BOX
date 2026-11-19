using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
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
        public async Task<EShopGameItem?> SearchGameInfoAsync(string gameTitle)
        {
            if (string.IsNullOrWhiteSpace(gameTitle) || gameTitle == "Unknown Game" || gameTitle == "Unknown")
                return null;

            // RU локаль мертва (возвращает 0 результатов), используем только EN
            return await SearchByLocaleAsync(gameTitle, "en");
        }

        private async Task<EShopGameItem?> SearchByLocaleAsync(string gameTitle, string locale)
        {
            try
            {
                string cleanQuery = CleanTitleForSearch(gameTitle);
                if (string.IsNullOrWhiteSpace(cleanQuery)) return null;

                string encodedQuery = Uri.EscapeDataString(cleanQuery);
                string url = $"https://search.nintendo-europe.com/{locale}/select?q={encodedQuery}&fq=type:GAME&rows=5&wt=json";

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var response = await StormHttpClientFactory.Shared.GetAsync(url, cts.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) return null;

                string jsonStr = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(jsonStr);

                if (doc.RootElement.TryGetProperty("response", out var respProp) &&
                    respProp.TryGetProperty("docs", out var docsProp) &&
                    docsProp.ValueKind == JsonValueKind.Array &&
                    docsProp.GetArrayLength() > 0)
                {
                    // Ищем наиболее релевантный результат с проверкой схожести названия (не берём случайный docsProp[0])
                    JsonElement? bestDoc = null;
                    double bestScore = 0.0;
                    string normQuery = NormalizeTitleForComparison(cleanQuery);
                    var queryTokens = normQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    foreach (var candidate in docsProp.EnumerateArray())
                    {
                        if (candidate.TryGetProperty("title", out var tProp))
                        {
                            string candidateRaw = tProp.GetString() ?? "";
                            string normCand = NormalizeTitleForComparison(candidateRaw);
                            if (string.IsNullOrEmpty(normCand)) continue;

                            if (string.Equals(normCand, normQuery, StringComparison.Ordinal))
                            {
                                bestDoc = candidate;
                                bestScore = 1.0;
                                break;
                            }

                            double score = 0.0;
                            if (normCand.Contains(normQuery, StringComparison.Ordinal) ||
                                normQuery.Contains(normCand, StringComparison.Ordinal))
                            {
                                int minLen = Math.Min(normCand.Length, normQuery.Length);
                                int maxLen = Math.Max(normCand.Length, normQuery.Length);
                                score = maxLen > 0 ? (double)minLen / maxLen : 0.0;
                                if (score < 0.55 && minLen >= 5) score = 0.65;
                            }
                            else if (queryTokens.Length > 0)
                            {
                                var candTokens = normCand.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
                                int matched = queryTokens.Count(t => candTokens.Contains(t));
                                score = (double)matched / Math.Max(queryTokens.Length, candTokens.Count);
                            }

                            if (score > bestScore && score >= 0.55)
                            {
                                bestScore = score;
                                bestDoc = candidate;
                            }
                        }
                    }

                    if (!bestDoc.HasValue)
                        return null;

                    var docElement = bestDoc.Value;
                    var item = new EShopGameItem();

                    if (docElement.TryGetProperty("title", out var titleProp))
                        item.Title = titleProp.GetString() ?? "";

                    // Описание — excerpt -> product_catalog_description_s
                    if (docElement.TryGetProperty("excerpt", out var excerptProp) && !string.IsNullOrWhiteSpace(excerptProp.GetString()))
                        item.Description = excerptProp.GetString() ?? "";
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

                    // Скриншоты — собираем все доступные изображения
                    // 1. screenshot_img_url_list (массив скриншотов)
                    if (docElement.TryGetProperty("screenshot_img_url_list", out var screenshotListProp) && screenshotListProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var screenshotUrl in screenshotListProp.EnumerateArray())
                        {
                            var s = screenshotUrl.GetString();
                            if (!string.IsNullOrEmpty(s))
                            {
                                item.Screenshots.Add(s.StartsWith("https:") || s.StartsWith("http:") ? s : "https:" + s);
                            }
                        }
                    }

                    // 2. image_url_h16x9_s (широкоформатный баннер — лучше для скриншотов)
                    AddImageIfAvailable(docElement, "image_url_h16x9_s", item.Screenshots);

                    // 3. image_url_h2x1_s (2:1 баннер)
                    AddImageIfAvailable(docElement, "image_url_h2x1_s", item.Screenshots);

                    // 4. wishlist_email_banner640w_image_url_s
                    AddImageIfAvailable(docElement, "wishlist_email_banner640w_image_url_s", item.Screenshots);

                    return item;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[NintendoEShopService] Search error ({locale}): {ex.Message}");
            }

            return null;
        }

        private static void AddImageIfAvailable(JsonElement docElement, string propName, List<string> screenshots)
        {
            if (docElement.TryGetProperty(propName, out var prop) && !string.IsNullOrEmpty(prop.GetString()))
            {
                string url = prop.GetString()!;
                url = (url.StartsWith("https:") || url.StartsWith("http:")) ? url : "https:" + url;
                if (!screenshots.Contains(url))
                    screenshots.Add(url);
            }
        }

        private static string NormalizeTitleForComparison(string rawTitle)
        {
            if (string.IsNullOrWhiteSpace(rawTitle)) return "";
            string s = rawTitle.Replace("®", "").Replace("™", "").Replace("©", "").ToLowerInvariant();
            s = Regex.Replace(s, @"\[[^\]]*\]|\([^\)]*\)", " ");
            s = Regex.Replace(s, @"[^a-z0-9а-яё]+", " ");
            return Regex.Replace(s, @"\s+", " ").Trim();
        }

        private static string CleanTitleForSearch(string rawTitle)
        {
            if (string.IsNullOrEmpty(rawTitle)) return "";

            // Удаляем паттерны в скобках вроде [0100...], (1G+1U), (USA) регулярным выражением
            string title = Regex.Replace(rawTitle, @"\[[^\]]*\]|\([^\)]*\)", " ");
            title = title.Replace("®", "").Replace("™", "").Replace("©", "");
            title = Regex.Replace(title, @"\s+", " ").Trim();
            return title;
        }
    }
}
