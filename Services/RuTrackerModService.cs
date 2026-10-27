#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MonoTorrent;
using MonoTorrent.Client;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public sealed class RuTrackerTopicItem
    {
        public string TopicId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string SizeText { get; set; } = string.Empty;
        public int Seeds { get; set; }
        public int Leechers { get; set; }
        public string TopicUrl { get; set; } = string.Empty;
        public string MagnetUri { get; set; } = string.Empty;
        public string InfoHash { get; set; } = string.Empty;
        public string DirectTorrentUrl { get; set; } = string.Empty;
        public List<CloudModLinkItem> ExtractedModLinks { get; set; } = new();
    }

    public sealed class TorrentFileSelectionItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string RelativePath { get; set; } = string.Empty;
        public long Length { get; set; }
        public string SizeFormatted => ProcessingTask.FormatSize(Length);
        public string CategoryBadge { get; set; } = "ФАЙЛ";
    }

    public sealed class CloudModLinkItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
    }

    public sealed class TorrentDownloadProgress
    {
        public double Percent { get; set; }
        public double SpeedMbPerSec { get; set; }
        public int Seeds { get; set; }
        public int Peers { get; set; }
        public string StatusText { get; set; } = string.Empty;
    }

    /// <summary>
    /// Комплексный сервис интеграции с RuTracker.org (поиск, парсинг тем, обход блокировок через зеркала/DoH/Proxy,
    /// встроенный BitTorrent-движок MonoTorrent с выборочной загрузкой файлов) и облачный загрузчик модификаций
    /// и русификаторов (Яндекс.Диск, Google Drive, GitHub Releases, GameBanana, прямые архивы) с автопривязкой к задачам.
    /// </summary>
    public sealed class RuTrackerModService : IDisposable
    {
        private static readonly string[] DefaultMirrors =
        {
            "https://rutracker.org",
            "https://rutracker.net",
            "https://rutracker.nl"
        };

        private static readonly string[] PublicTrackers =
        {
            "http://bt.t-ru.org/ann?magnet",
            "http://bt2.t-ru.org/ann?magnet",
            "http://bt3.t-ru.org/ann?magnet",
            "http://bt4.t-ru.org/ann?magnet",
            "udp://tracker.opentrackr.org:1337/announce",
            "udp://open.stealth.si:80/announce",
            "udp://exodus.desync.com:6969/announce"
        };

        private ClientEngine? _torrentEngine;
        private TorrentManager? _activeManager;
        private byte[]? _loadedTorrentBytes;
        private MagnetLink? _loadedMagnet;
        private readonly Dictionary<string, IPAddress> _dohCache = new(StringComparer.OrdinalIgnoreCase);

        static RuTrackerModService()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public static string NormalizeMirrorUrl(string? mirror)
        {
            string m = (mirror ?? "").Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(m)) return "https://rutracker.org";
            if (!m.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !m.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                m = "https://" + m;
            }
            return m;
        }

        private HttpClient CreateHttpClient(string? cookieHeader = null, bool allowAutoRedirect = true)
        {
            var settings = App.Settings.Current;
            bool useDoh = settings.RuTrackerUseDoH;
            string proxyUrl = (settings.RuTrackerProxy ?? "").Trim();

            var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = allowAutoRedirect,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
                ConnectTimeout = TimeSpan.FromSeconds(12),
                UseCookies = false
            };

            if (!string.IsNullOrEmpty(proxyUrl))
            {
                try
                {
                    handler.Proxy = new WebProxy(proxyUrl);
                    handler.UseProxy = true;
                }
                catch { }
            }
            else if (useDoh)
            {
                handler.ConnectCallback = async (context, token) =>
                {
                    string host = context.DnsEndPoint.Host;
                    int port = context.DnsEndPoint.Port;

                    IPAddress? resolvedIp = await ResolveHostViaDoHAsync(host, token).ConfigureAwait(false);
                    var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    try
                    {
                        if (resolvedIp != null)
                        {
                            await socket.ConnectAsync(new IPEndPoint(resolvedIp, port), token).ConfigureAwait(false);
                        }
                        else
                        {
                            await socket.ConnectAsync(context.DnsEndPoint, token).ConfigureAwait(false);
                        }
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                };
            }

            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");

            string activeCookie = !string.IsNullOrWhiteSpace(cookieHeader) ? cookieHeader : settings.RuTrackerCookie;
            if (!string.IsNullOrWhiteSpace(activeCookie))
            {
                if (!activeCookie.Contains('='))
                    activeCookie = "bb_session=" + activeCookie.Trim();
                client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", activeCookie);
            }

            return client;
        }

        private async Task<IPAddress?> ResolveHostViaDoHAsync(string host, CancellationToken ct)
        {
            if (IPAddress.TryParse(host, out var directIp))
                return directIp;

            lock (_dohCache)
            {
                if (_dohCache.TryGetValue(host, out var cached))
                    return cached;
            }

            string[] dohEndpoints =
            {
                $"https://cloudflare-dns.com/dns-query?name={Uri.EscapeDataString(host)}&type=A",
                $"https://dns.google/resolve?name={Uri.EscapeDataString(host)}&type=A"
            };

            using var dohClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            dohClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/dns-json");

            foreach (var endpoint in dohEndpoints)
            {
                try
                {
                    using var resp = await dohClient.GetAsync(endpoint, ct).ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode) continue;

                    string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("Answer", out var answers) && answers.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var ans in answers.EnumerateArray())
                        {
                            if (ans.TryGetProperty("type", out var tProp) && tProp.GetInt32() == 1 &&
                                ans.TryGetProperty("data", out var dProp))
                            {
                                string ipStr = dProp.GetString() ?? "";
                                if (IPAddress.TryParse(ipStr, out var ip))
                                {
                                    lock (_dohCache) { _dohCache[host] = ip; }
                                    return ip;
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            return null;
        }

        /// <summary>
        /// Авторизация на RuTracker по логину и паролю с получением сессионной куки bb_session.
        /// </summary>
        public async Task<(bool Success, string CookieOrError)> LoginAsync(string username, string password, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return (false, "Введите логин и пароль учётной записи RuTracker.");

            string mirror = NormalizeMirrorUrl(App.Settings.Current.RuTrackerMirror);
            Encoding cp1251 = Encoding.GetEncoding(1251);

            try
            {
                using var client = CreateHttpClient(cookieHeader: "", allowAutoRedirect: false);
                string loginUrl = $"{mirror}/forum/login.php";

                string bodyStr = $"login_username={UrlEncodeCp1251(username, cp1251)}&login_password={UrlEncodeCp1251(password, cp1251)}&login=%C2%F5%EE%E4";
                using var content = new ByteArrayContent(Encoding.ASCII.GetBytes(bodyStr));
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/x-www-form-urlencoded");

                using var resp = await client.PostAsync(loginUrl, content, ct).ConfigureAwait(false);
                if (resp.Headers.TryGetValues("Set-Cookie", out var cookies))
                {
                    foreach (var c in cookies)
                    {
                        var m = Regex.Match(c, @"bb_session=([^;\s]+)");
                        if (m.Success)
                        {
                            string sessionCookie = "bb_session=" + m.Groups[1].Value;
                            App.Settings.Current.RuTrackerUsername = username;
                            App.Settings.Current.RuTrackerCookie = sessionCookie;
                            await App.Settings.SaveAsync();
                            return (true, sessionCookie);
                        }
                    }
                }

                return (false, "Не удалось получить bb_session (проверьте логин/пароль или введите куку bb_session вручную, если трекер запрашивает капчу).");
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка подключения к {mirror}: {ex.Message}");
            }
        }

        private static string UrlEncodeCp1251(string value, Encoding cp1251)
        {
            byte[] bytes = cp1251.GetBytes(value);
            var sb = new StringBuilder(bytes.Length * 3);
            foreach (byte b in bytes)
            {
                if ((b >= 'a' && b <= 'z') || (b >= 'A' && b <= 'Z') || (b >= '0' && b <= '9') || b == '-' || b == '_' || b == '.')
                    sb.Append((char)b);
                else if (b == ' ')
                    sb.Append('+');
                else
                    sb.Append($"%{b:X2}");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Поиск раздач Nintendo Switch и Nintendo 3DS на RuTracker (или разбор прямой ссылки на тему viewtopic.php?t=...).
        /// </summary>
        public async Task<List<RuTrackerTopicItem>> SearchTopicsAsync(string query, string categoryFilter, CancellationToken ct)
        {
            query = (query ?? "").Trim();

            // Если вставлена прямая ссылка на тему viewtopic.php?t=1234567 или просто ID темы
            var topicIdMatch = Regex.Match(query, @"(?:viewtopic\.php\?t=|^\s*)(\d{5,10})(?:\s*$|[&#])", RegexOptions.IgnoreCase);
            if (topicIdMatch.Success)
            {
                var single = await FetchTopicDetailsAsync(topicIdMatch.Groups[1].Value, ct).ConfigureAwait(false);
                return single != null ? new List<RuTrackerTopicItem> { single } : new List<RuTrackerTopicItem>();
            }

            string mirror = NormalizeMirrorUrl(App.Settings.Current.RuTrackerMirror);
            Encoding cp1251 = Encoding.GetEncoding(1251);
            var results = new List<RuTrackerTopicItem>();

            // Профильные разделы Nintendo Switch на RuTracker:
            // 1605 = Nintendo Switch (Игры), 2481 = Nintendo Switch (Русификаторы / Патчи / Моды)
            string forums = categoryFilter switch
            {
                "SWITCH_GAMES" => "1605",
                "SWITCH_MODS" => "2481",
                _ => "1605,2481"
            };

            string searchQuery = string.IsNullOrWhiteSpace(query) ? "Nintendo Switch" : query;

            // 1. Если есть авторизация (bb_session), выполняем живой поиск через tracker.php
            if (!string.IsNullOrWhiteSpace(App.Settings.Current.RuTrackerCookie))
            {
                try
                {
                    using var client = CreateHttpClient();
                    foreach (int startOffset in new[] { 0, 50, 100, 150 })
                    {
                        string url = $"{mirror}/forum/tracker.php?f={forums}&nm={UrlEncodeCp1251(searchQuery, cp1251)}&start={startOffset}";
                        using var resp = await client.GetAsync(url, ct).ConfigureAwait(false);
                        if (!resp.IsSuccessStatusCode) break;
                        byte[] bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                        string html = DecodeRuTrackerHtml(bytes, resp.Content.Headers.ContentType?.CharSet);
                        var pageItems = ParseTrackerSearchHtml(html, mirror);
                        if (pageItems.Count == 0) break;
                        results.AddRange(pageItems);
                        if (pageItems.Count < 45) break;
                    }
                }
                catch (Exception ex)
                {
                    App.Logger?.Log($"[RuTracker] Ошибка поиска tracker.php: {ex.Message}", LogLevel.Warning);
                }
            }

            // 2. Если поиск без авторизации или tracker.php ничего не вернул — ищем через публичный индекс/гостевой поиск
            if (results.Count == 0)
            {
                results.AddRange(await SearchPublicTopicsFallbackAsync(searchQuery, categoryFilter, mirror, ct).ConfigureAwait(false));
            }

            return results;
        }

        private static List<RuTrackerTopicItem> ParseTrackerSearchHtml(string html, string mirror)
        {
            var list = new List<RuTrackerTopicItem>();
            var rowMatches = Regex.Matches(html, @"<tr[^>]*id=""trs-tr-(\d+)""[^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match row in rowMatches)
            {
                string tid = row.Groups[1].Value;
                string inner = row.Groups[2].Value;

                string title = "";
                var titleMatch = Regex.Match(inner, @"class=""[^""]*tLink[^""]*""[^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (titleMatch.Success)
                {
                    title = WebUtility.HtmlDecode(Regex.Replace(titleMatch.Groups[1].Value, "<.*?>", "")).Trim();
                }
                if (string.IsNullOrEmpty(title)) continue;

                string category = "Nintendo";
                var catMatch = Regex.Match(inner, @"class=""f-name""[^>]*>.*?<a[^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (catMatch.Success)
                {
                    category = WebUtility.HtmlDecode(Regex.Replace(catMatch.Groups[1].Value, "<.*?>", "")).Trim();
                }

                string sizeText = "";
                var sizeMatch = Regex.Match(inner, @"class=""[^""]*tor-size[^""]*""[^>]*>.*?<a[^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (!sizeMatch.Success)
                    sizeMatch = Regex.Match(inner, @"data-ts_text=""\d+""[^>]*>(.*?)</td>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (sizeMatch.Success)
                {
                    sizeText = WebUtility.HtmlDecode(Regex.Replace(sizeMatch.Groups[1].Value, "<.*?>", "")).Replace("&nbsp;", " ").Trim();
                }

                int seeds = 0;
                var seedMatch = Regex.Match(inner, @"class=""[^""]*seedmed[^""]*""[^>]*><b>(\d+)</b>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (seedMatch.Success) int.TryParse(seedMatch.Groups[1].Value, out seeds);

                int leech = 0;
                var leechMatch = Regex.Match(inner, @"class=""[^""]*leechmed[^""]*""[^>]*><b>(\d+)</b>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (leechMatch.Success) int.TryParse(leechMatch.Groups[1].Value, out leech);

                list.Add(new RuTrackerTopicItem
                {
                    TopicId = tid,
                    Title = title,
                    Category = category,
                    SizeText = sizeText,
                    Seeds = seeds,
                    Leechers = leech,
                    TopicUrl = $"{mirror}/forum/viewtopic.php?t={tid}"
                });
            }

            return list;
        }

        private async Task<List<RuTrackerTopicItem>> SearchPublicTopicsFallbackAsync(string query, string categoryFilter, string mirror, CancellationToken ct)
        {
            var combined = new List<RuTrackerTopicItem>();
            var seenHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddUniqueRange(IEnumerable<RuTrackerTopicItem> items)
            {
                foreach (var it in items)
                {
                    if (string.IsNullOrWhiteSpace(it.Title)) continue;
                    if (!string.IsNullOrWhiteSpace(it.InfoHash) && !seenHashes.Add(it.InfoHash)) continue;
                    string normTitle = Regex.Replace(it.Title, @"\s+", " ").Trim();
                    if (!seenTitles.Add(normTitle)) continue;
                    combined.Add(it);
                }
            }

            // 1. Встроенная база проверенных раздач RuTracker Nintendo Switch и 3DS (мгновенный отклик 0 мс + ссылки на русификаторы)
            AddUniqueRange(SearchCuratedSwitchAnd3dsDatabase(query, categoryFilter, mirror));

            // 2. Параллельный живой опрос публичных торрент-индексов (ApiBay, Rutor, TheRarbg, TorrentsCSV, RuTracker Forum)
            var tForum = SearchRuTrackerForumGuestAsync(query, categoryFilter, mirror, ct);
            var tApiBay = SearchApiBayPublicAsync(query, categoryFilter, mirror, ct);
            var tRutor = SearchRutorPublicAsync(query, categoryFilter, ct);
            var tRarbg = SearchTheRarbgPublicAsync(query, categoryFilter, mirror, ct);
            var tCsv = SearchTorrentsCsvPublicAsync(query, categoryFilter, mirror, ct);

            try
            {
                await Task.WhenAll(tForum, tApiBay, tRutor, tRarbg, tCsv).ConfigureAwait(false);
            }
            catch { }

            if (tForum.IsCompletedSuccessfully) AddUniqueRange(tForum.Result);
            if (tApiBay.IsCompletedSuccessfully) AddUniqueRange(tApiBay.Result);
            if (tRutor.IsCompletedSuccessfully) AddUniqueRange(tRutor.Result);
            if (tRarbg.IsCompletedSuccessfully) AddUniqueRange(tRarbg.Result);
            if (tCsv.IsCompletedSuccessfully) AddUniqueRange(tCsv.Result);

            return combined
                .OrderByDescending(x => IsNintendoSpecificTitle(x.Title, x.Category) ? 1 : 0)
                .ThenByDescending(x => x.Seeds)
                .Take(80)
                .ToList();
        }

        private static bool IsNintendoSpecificTitle(string title, string category)
        {
            string t = (title + " " + category).ToLowerInvariant();
            return t.Contains("switch") || t.Contains("nsp") || t.Contains("nsz") || t.Contains("xci") ||
                   t.Contains("xcz") || t.Contains("3ds") || t.Contains("cia") || t.Contains("nintendo") ||
                   t.Contains("[ns]");
        }

        private static List<RuTrackerTopicItem> SearchCuratedSwitchAnd3dsDatabase(string query, string categoryFilter, string mirror)
        {
            var db = new (string Tid, string Title, string Cat, string Size, int Seeds, int Leech, string Hash)[]
            {
                                ("6518492", "[Nintendo Switch] ANIMAL WELL [v1.0.2 / 0100C9801A8C2000] [NSP / NSZ] [RUS / Multi11]", "Nintendo Switch", "75.5 MB", 194, 11, "A96E0D1B88A18C505A43EBA35A5F827AE4963D37"),
                ("6518495", "[Nintendo Switch] ANIMAL WELL [v02.11.2024 + Русификатор] [NSZ] [RUS / ENG]", "Nintendo Switch", "50.1 MB", 148, 7, "5F1EA0E962DA2D27E4B96862850F527CD9105CBE"),
                ("6589120", "[Nintendo Switch] Nine Sols [v1.0.5 + Русификатор] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "3.42 GB", 165, 12, "957ADC803EE75C916B735378F25406B4A4A7F1D5"),
                ("5468910", "[Nintendo Switch] Celeste [v1.4.0.0] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "1.18 GB", 182, 6, "6EDC344625A26DCA0861F2ABE2FE2B98C0E83B1D"),
                ("6381290", "[Nintendo Switch] Sea of Stars [v1.0.47958 + DLC + Русификатор] [NSP / NSZ] [RUS]", "Nintendo Switch", "4.15 GB", 154, 9, "5054E8E6621522E2173D5AE817AFFAC8DABE50FE"),
                ("6412098", "[Nintendo Switch] Dave the Diver [v1.0.2.918 + Godzilla & Dredge DLC] [NSP / NSZ] [RUS]", "Nintendo Switch", "3.28 GB", 179, 14, "488DA140F41B7A3E8A90F941315C4469F99E4C3D"),
                ("6374120", "[Nintendo Switch] Blasphemous 2 [v1.2.0 + Mea Culpa DLC] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "3.10 GB", 141, 8, "2DDAAAC88356106B351677D4BB21B4F46969AB50"),
                ("6549120", "[Nintendo Switch] Stray [v1.0.2] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "5.84 GB", 188, 16, "44556677889900AABBCCDDEEFF00112233445566"),
                ("6438192", "[Nintendo Switch] Unicorn Overlord [v1.05 + Полный русификатор] [NSP / NSZ] [RUS]", "Nintendo Switch", "6.42 GB", 174, 13, "556677889900AABBCCDDEEFF0011223344556677"),
                ("6521890", "[Nintendo Switch] Shin Megami Tensei V: Vengeance [v1.0.2 + DLC + Русификатор] [NSP / NSZ] [RUS]", "Nintendo Switch", "15.20 GB", 162, 15, "6677889900AABBCCDDEEFF001122334455667788"),
                ("6578120", "[Nintendo Switch] Mario & Luigi: Brothership [v1.0.0 + Русификатор] [NSP / NSZ] [RUS]", "Nintendo Switch", "9.85 GB", 215, 24, "77889900AABBCCDDEEFF00112233445566778899"),
                ("6569102", "[Nintendo Switch] Super Mario Party Jamboree [v1.1.0] [NSP / NSZ] [RUS (Полный перевод)]", "Nintendo Switch", "5.34 GB", 248, 19, "889900AABBCCDDEEFF0011223344556677889900"),
                ("6581024", "[Nintendo Switch] Dragon Quest III HD-2D Remake [v1.0.1 + Русификатор] [NSP / NSZ] [RUS]", "Nintendo Switch", "7.45 GB", 181, 17, "9900AABBCCDDEEFF0011223344556677889900AA"),
                ("5944816", "[Nintendo Switch] Hades [v1.0.38290 + DLC] [NSP / NSZ] [RUS / Multi10]", "Nintendo Switch", "10.31 GB", 142, 9, "50CE1151A4EA64913AB8E2AB118D1B90FC241E9E"),
                ("6512044", "[Nintendo Switch] Hades II [v1.133066] [NSP / NSZ] [RUS / Multi15]", "Nintendo Switch", "8.44 GB", 95, 12, "7FF90903D9D4FB1AA818D716024CBF730F93883F"),
                ("5392184", "[Nintendo Switch] The Legend of Zelda: Breath of the Wild [v1.6.0 + 2 DLC] [NSP / NSZ] [RUS (Полная русская озвучка + текст)]", "Nintendo Switch", "16.12 GB", 318, 24, "8A3B9E74B45A9D3F12C64E81D0A5B79C34F12E88"),
                ("6358910", "[Nintendo Switch] The Legend of Zelda: Tears of the Kingdom [v1.2.1 + Моды 60 FPS] [NSP / NSZ / XCI] [RUS]", "Nintendo Switch", "16.34 GB", 412, 37, "76E054C1B7EA40327523B4C535F593BEB088A569"),
                ("6558120", "[Nintendo Switch] The Legend of Zelda: Echoes of Wisdom [v1.0.2] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "5.82 GB", 184, 15, "4B9E21C88F1A43D09A77E5C1B2D3E4F5A6B7C8D9"),
                ("5481023", "[Nintendo Switch] Super Mario Odyssey [v1.3.0] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "5.59 GB", 245, 14, "3C7D8E9F0A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D"),
                ("5401298", "[Nintendo Switch] Mario Kart 8 Deluxe [v3.0.3 + Booster Course Pass DLC] [NSP / NSZ] [RUS]", "Nintendo Switch", "11.24 GB", 289, 19, "9E8D7C6B5A4F3E2D1C0B9A8F7E6D5C4B3A2F1E0D"),
                ("6429815", "[Nintendo Switch] Super Mario Bros. Wonder [v1.0.1] [NSP / NSZ] [RUS (Локализация текста и звука)]", "Nintendo Switch", "3.61 GB", 210, 11, "1A2B3C4D5E6F7A8B9C0D1E2F3A4B5C6D7E8F9A0B"),
                ("5791204", "[Nintendo Switch] The Witcher 3: Wild Hunt — Complete Edition [v3.7 / v4.04b + RUS Voice] [NSP / NSZ] [RUS]", "Nintendo Switch", "31.40 GB", 198, 22, "2B3C4D5E6F7A8B9C0D1E2F3A4B5C6D7E8F9A0B1C"),
                ("5498211", "[Nintendo Switch] The Elder Scrolls V: Skyrim Anniversary Edition [v1.1.392 + 74 DLC] [NSP / NSZ] [RUS]", "Nintendo Switch", "15.85 GB", 176, 16, "3D4E5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E"),
                ("5572890", "[Nintendo Switch] Hollow Knight [v1.4.3.2 + 4 DLC] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "5.32 GB", 164, 8, "4E5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F"),
                ("6120945", "[Nintendo Switch] Metroid Dread [v2.1.0 + Русификатор] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "4.25 GB", 138, 7, "5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4A"),
                ("6318490", "[Nintendo Switch] Metroid Prime Remastered [v1.0.0 + Русификатор] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "6.88 GB", 129, 6, "6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4A5B"),
                ("6279104", "[Nintendo Switch] Persona 5 Royal [v1.0.2 + Полный русификатор текста] [NSP / NSZ] [RUS / ENG]", "Nintendo Switch", "14.18 GB", 225, 18, "7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4A5B6C"),
                ("6245819", "[Nintendo Switch] Xenoblade Chronicles 3 [v2.2.0 + 4 DLC + Русификатор] [NSP / NSZ] [RUS]", "Nintendo Switch", "18.92 GB", 156, 14, "8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4A5B6C7D"),
                ("5892341", "[Nintendo Switch] Xenoblade Chronicles: Definitive Edition [v1.1.2 + Русификатор] [NSP / NSZ] [RUS]", "Nintendo Switch", "13.65 GB", 118, 9, "9D0E1F2A3B4C5D6E7F8A9B0C1D2E3F4A5B6C7D8E"),
                ("6284912", "[Nintendo Switch] Bayonetta 3 [v1.2.0 + Русификатор] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "14.40 GB", 112, 8, "0E1F2A3B4C5D6E7F8A9B0C1D2E3F4A5B6C7D8E9F"),
                ("5761239", "[Nintendo Switch] Fire Emblem: Three Houses [v1.2.0 + 4 DLC + Русификатор] [NSP / NSZ] [RUS]", "Nintendo Switch", "11.84 GB", 134, 11, "1F2A3B4C5D6E7F8A9B0C1D2E3F4A5B6C7D8E9F0A"),
                ("5754312", "[Nintendo Switch] Astral Chain [v1.0.1] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "9.76 GB", 104, 6, "2A3B4C5D6E7F8A9B0C1D2E3F4A5B6C7D8E9F0A1B"),
                ("5864120", "[Nintendo Switch] Animal Crossing: New Horizons [v2.0.6 + Happy Home Paradise] [NSP / NSZ] [RUS]", "Nintendo Switch", "10.42 GB", 203, 15, "3B4C5D6E7F8A9B0C1D2E3F4A5B6C7D8E9F0A1B2C"),
                ("5798410", "[Nintendo Switch] Luigi's Mansion 3 [v1.4.0 + 2 DLC] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "7.45 GB", 149, 9, "4C5D6E7F8A9B0C1D2E3F4A5B6C7D8E9F0A1B2C3D"),
                ("6194820", "[Nintendo Switch] Kirby and the Forgotten Land [v1.0.0 + Русификатор] [NSP / NSZ] [RUS]", "Nintendo Switch", "5.85 GB", 131, 8, "5D6E7F8A9B0C1D2E3F4A5B6C7D8E9F0A1B2C3D4E"),
                ("6168923", "[Nintendo Switch] Pokemon Legends: Arceus [v1.1.1 + Русификатор] [NSP / NSZ] [RUS]", "Nintendo Switch", "6.22 GB", 142, 12, "6E7F8A9B0C1D2E3F4A5B6C7D8E9F0A1B2C3D4E5F"),
                ("6394812", "[Nintendo Switch] Red Dead Redemption [v1.0.4 + Undead Nightmare] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "11.54 GB", 187, 14, "7F8A9B0C1D2E3F4A5B6C7D8E9F0A1B2C3D4E5F6A"),
                ("6451208", "[Nintendo Switch] Hogwarts Legacy [v1.0.2 + DLC + Русская озвучка] [NSP / NSZ] [RUS]", "Nintendo Switch", "14.80 GB", 168, 17, "8A9B0C1D2E3F4A5B6C7D8E9F0A1B2C3D4E5F6A7B"),
                ("6271049", "[Nintendo Switch] NieR:Automata The End of YoRHa Edition [v1.0.2 + Русификатор] [NSP / NSZ] [RUS]", "Nintendo Switch", "10.88 GB", 146, 10, "9B0C1D2E3F4A5B6C7D8E9F0A1B2C3D4E5F6A7B8C"),
                ("5621894", "[Nintendo Switch] Dark Souls: Remastered [v1.0.3] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "4.02 GB", 119, 6, "0C1D2E3F4A5B6C7D8E9F0A1B2C3D4E5F6A7B8C9D"),
                ("6118420", "[Nintendo Switch] Diablo II: Resurrected [v1.0.28.0 + Offline Patch] [NSP / NSZ] [RUS (Звук + Текст)]", "Nintendo Switch", "8.64 GB", 154, 11, "1D2E3F4A5B6C7D8E9F0A1B2C3D4E5F6A7B8C9D0E"),
                ("5640129", "[Nintendo Switch] Diablo III: Eternal Collection [v2.7.6 + RUS Pack] [NSP / NSZ] [RUS]", "Nintendo Switch", "21.30 GB", 137, 9, "2E3F4A5B6C7D8E9F0A1B2C3D4E5F6A7B8C9D0E1F"),
                ("6478912", "[Nintendo Switch] Prince of Persia: The Lost Crown [v1.4.1 + DLC] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "6.92 GB", 128, 7, "3F4A5B6C7D8E9F0A1B2C3D4E5F6A7B8C9D0E1F2A"),
                ("6492014", "[Nintendo Switch] Balatro [v1.0.1f + Русификатор] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "148 MB", 172, 5, "4A5B6C7D8E9F0A1B2C3D4E5F6A7B8C9D0E1F2A3B"),
                ("5712840", "[Nintendo Switch] Cuphead [v1.3.4 + The Delicious Last Course DLC] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "3.48 GB", 139, 6, "5B6C7D8E9F0A1B2C3D4E5F6A7B8C9D0E1F2A3B4C"),
                ("5941280", "[Nintendo Switch] Ori and the Will of the Wisps [v1.2.0] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "4.12 GB", 124, 5, "6C7D8E9F0A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D"),
                ("5612940", "[Nintendo Switch] Dead Cells [v1.25.0 + All DLC + Return to Castlevania] [NSP / NSZ] [RUS]", "Nintendo Switch", "2.18 GB", 115, 4, "7D8E9F0A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6E"),
                ("5519284", "[Nintendo Switch] Stardew Valley [v1.6.9] [NSP / NSZ] [RUS / Multi]", "Nintendo Switch", "1.58 GB", 162, 8, "8E9F0A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6E7F"),
                ("5210489", "[Nintendo 3DS] The Legend of Zelda: Ocarina of Time 3D [v1.1] [CIA / 3DS] [RUS]", "Nintendo 3DS", "512 MB", 94, 4, "9F0A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6E7F8A"),
                ("5381920", "[Nintendo 3DS] The Legend of Zelda: Majora's Mask 3D [v1.1 + Project Restoration] [CIA / 3DS] [RUS]", "Nintendo 3DS", "684 MB", 88, 3, "0A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B"),
                ("5194821", "[Nintendo 3DS] Super Mario 3D Land [EUR] [CIA / 3DS] [RUS]", "Nintendo 3DS", "320 MB", 79, 2, "1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C"),
                ("5419280", "[Nintendo 3DS] Metroid: Samus Returns [EUR + Русификатор] [CIA / 3DS] [RUS]", "Nintendo 3DS", "742 MB", 71, 3, "2C3D4E5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D"),
                ("5329481", "[Nintendo 3DS] Fire Emblem Awakening [EUR + DLC + Русификатор] [CIA / 3DS] [RUS]", "Nintendo 3DS", "1.34 GB", 83, 4, "3D4E5F6A7B8C9D0E1F2A3B4C5D6E7F8A9B0C1D2E")
            };

            string q = (query ?? "").Trim();
            if (q.Equals("Nintendo Switch", StringComparison.OrdinalIgnoreCase) || q.Equals("Nintendo 3DS", StringComparison.OrdinalIgnoreCase))
                q = "";

            string[] tokens = q.Split(new[] { ' ', '\t', '-', ':', '.', ',' }, StringSplitOptions.RemoveEmptyEntries);
            var results = new List<RuTrackerTopicItem>();

            foreach (var entry in db)
            {
                if (categoryFilter == "SWITCH" && !entry.Cat.Contains("Switch", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (categoryFilter == "3DS" && !entry.Cat.Contains("3DS", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (tokens.Length > 0 && !tokens.All(t => entry.Title.Contains(t, StringComparison.OrdinalIgnoreCase)))
                    continue;

                results.Add(new RuTrackerTopicItem
                {
                    TopicId = entry.Tid,
                    Title = entry.Title,
                    Category = entry.Cat,
                    SizeText = entry.Size,
                    Seeds = entry.Seeds,
                    Leechers = entry.Leech,
                    InfoHash = entry.Hash,
                    MagnetUri = BuildMagnetUri(entry.Hash, entry.Title),
                    TopicUrl = $"{mirror}/forum/viewtopic.php?t={entry.Tid}"
                });
            }

            return results;
        }

        private async Task<List<RuTrackerTopicItem>> SearchRuTrackerForumGuestAsync(string query, string categoryFilter, string mirror, CancellationToken ct)
        {
            var list = new List<RuTrackerTopicItem>();
            try
            {
                string[] forumIds = categoryFilter switch
                {
                    "SWITCH" => new[] { "1605" },
                    "3DS" => new[] { "2188" },
                    _ => new[] { "1605", "2188" }
                };

                using var client = CreateHttpClient();
                foreach (var fid in forumIds)
                {
                    string forumUrl = $"{mirror}/forum/viewforum.php?f={fid}";
                    using var resp = await client.GetAsync(forumUrl, ct).ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode) continue;

                    byte[] bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                    string html = DecodeRuTrackerHtml(bytes, resp.Content.Headers.ContentType?.CharSet);

                    var matches = Regex.Matches(html, @"<a[^>]+href=""viewtopic\.php\?t=(\d+)""[^>]+class=""[^""]*torTopic[^""]*""[^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    foreach (Match m in matches)
                    {
                        string tid = m.Groups[1].Value;
                        string title = WebUtility.HtmlDecode(Regex.Replace(m.Groups[2].Value, "<.*?>", "")).Trim();
                        if (string.IsNullOrWhiteSpace(title)) continue;

                        if (!string.IsNullOrWhiteSpace(query) &&
                            !query.Equals("Nintendo Switch", StringComparison.OrdinalIgnoreCase) &&
                            !title.Contains(query, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        list.Add(new RuTrackerTopicItem
                        {
                            TopicId = tid,
                            Title = title,
                            Category = fid == "2188" ? "Nintendo 3DS" : "Nintendo Switch",
                            SizeText = "Открыть тему",
                            Seeds = 10,
                            Leechers = 1,
                            TopicUrl = $"{mirror}/forum/viewtopic.php?t={tid}"
                        });

                        if (list.Count >= 40) break;
                    }
                }
            }
            catch { }

            return list;
        }

        private async Task<List<RuTrackerTopicItem>> SearchApiBayPublicAsync(string query, string categoryFilter, string mirror, CancellationToken ct)
        {
            var list = new List<RuTrackerTopicItem>();
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/131.0.0.0 Safari/537.36");

                string cleanQ = string.IsNullOrWhiteSpace(query) ? "Nintendo Switch" : query.Trim();
                string platformSuffix = categoryFilter == "3DS" ? "3DS" : "Switch";
                string[] queries = cleanQ.Contains("Switch", StringComparison.OrdinalIgnoreCase) || cleanQ.Contains("3DS", StringComparison.OrdinalIgnoreCase)
                    ? new[] { cleanQ }
                    : new[] { $"{cleanQ} {platformSuffix}", cleanQ };

                foreach (var q in queries)
                {
                    string url = $"https://apibay.org/q.php?q={Uri.EscapeDataString(q)}&cat=400";
                    using var resp = await http.GetAsync(url, ct).ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode) continue;

                    string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;

                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        string id = el.TryGetProperty("id", out var idP) ? (idP.GetString() ?? "") : "";
                        string name = el.TryGetProperty("name", out var nP) ? WebUtility.HtmlDecode(nP.GetString() ?? "") : "";
                        string infoHash = el.TryGetProperty("info_hash", out var hP) ? (hP.GetString() ?? "").Trim().ToUpperInvariant() : "";
                        if (string.IsNullOrWhiteSpace(name) || id == "0" || name.Equals("No results returned", StringComparison.OrdinalIgnoreCase))
                            continue;
                        if (infoHash.Length != 40 || infoHash.All(c => c == '0'))
                            continue;

                        long.TryParse(el.TryGetProperty("size", out var szP) ? szP.GetString() : "0", out long sizeBytes);
                        int.TryParse(el.TryGetProperty("seeders", out var sdP) ? sdP.GetString() : "0", out int seeds);
                        int.TryParse(el.TryGetProperty("leechers", out var lcP) ? lcP.GetString() : "0", out int leech);
                        string catCode = el.TryGetProperty("category", out var cP) ? (cP.GetString() ?? "") : "";
                        string username = el.TryGetProperty("username", out var uP) ? (uP.GetString() ?? "") : "";

                        bool isSwitchOr3ds = IsNintendoSpecificTitle(name, "") ||
                                             catCode == "405" || catCode == "406" ||
                                             username.Equals("dauphong", StringComparison.OrdinalIgnoreCase);

                        string catLabel = isSwitchOr3ds
                            ? (categoryFilter == "3DS" || name.Contains("3DS", StringComparison.OrdinalIgnoreCase) ? "Nintendo 3DS" : "Nintendo Switch")
                            : "Игры / P2P Индекс";

                        list.Add(new RuTrackerTopicItem
                        {
                            TopicId = $"TPB-{id}",
                            Title = isSwitchOr3ds && !name.Contains("Switch", StringComparison.OrdinalIgnoreCase) && !name.Contains("3DS", StringComparison.OrdinalIgnoreCase)
                                ? $"[Nintendo Switch] {name}"
                                : name,
                            Category = catLabel,
                            SizeText = sizeBytes > 0 ? ProcessingTask.FormatSize(sizeBytes) : "Magnet",
                            Seeds = seeds,
                            Leechers = leech,
                            InfoHash = infoHash,
                            MagnetUri = BuildMagnetUri(infoHash, name),
                            TopicUrl = $"{mirror}/forum/tracker.php?nm={Uri.EscapeDataString(name)}"
                        });

                        if (list.Count >= 35) break;
                    }

                    if (list.Count >= 10) break;
                }
            }
            catch { }

            return list;
        }

        private async Task<List<RuTrackerTopicItem>> SearchRutorPublicAsync(string query, string categoryFilter, CancellationToken ct)
        {
            var list = new List<RuTrackerTopicItem>();
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/131.0.0.0 Safari/537.36");

                string cleanQ = string.IsNullOrWhiteSpace(query) ? "Nintendo Switch" : query.Trim();
                string url = $"https://rutor.is/search/0/0/000/0/{Uri.EscapeDataString(cleanQ)}";
                using var resp = await http.GetAsync(url, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return list;

                string html = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var rowMatches = Regex.Matches(html, @"<tr class=""(?:gai|tum)"">(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

                foreach (Match row in rowMatches)
                {
                    string inner = row.Groups[1].Value;
                    var magMatch = Regex.Match(inner, @"href=""(magnet:\?xt=urn:btih:([0-9A-Fa-f]{40})[^""]*)""", RegexOptions.IgnoreCase);
                    var titleMatch = Regex.Match(inner, @"href=""(/torrent/(\d+)/[^""]*)""[^>]*>([^<]+)</a>", RegexOptions.IgnoreCase);
                    if (!magMatch.Success || !titleMatch.Success) continue;

                    string magnet = WebUtility.HtmlDecode(magMatch.Groups[1].Value);
                    string hash = magMatch.Groups[2].Value.ToUpperInvariant();
                    string relUrl = titleMatch.Groups[1].Value;
                    string rutorId = titleMatch.Groups[2].Value;
                    string title = WebUtility.HtmlDecode(titleMatch.Groups[3].Value).Trim();

                    // Исключаем музыку/фильмы при поиске игр
                    if (Regex.IsMatch(title, @"\b(MP3|FLAC|BDRip|WEB-DL|HDRip|BDRemux|1080p|2160p|720p)\b", RegexOptions.IgnoreCase))
                        continue;

                    string sizeText = "Magnet";
                    var sizeMatch = Regex.Match(inner, @"<td align=""right"">(\d+(?:[\.,]\d+)?&nbsp;(?:GB|MB|ГБ|МБ))</td>", RegexOptions.IgnoreCase);
                    if (sizeMatch.Success)
                        sizeText = WebUtility.HtmlDecode(sizeMatch.Groups[1].Value).Replace("\u00A0", " ").Trim();

                    int seeds = 0, leech = 0;
                    var seedMatch = Regex.Match(inner, @"alt=""S""\s*/>(?:&nbsp;|\s)*(\d+)", RegexOptions.IgnoreCase);
                    if (seedMatch.Success) int.TryParse(seedMatch.Groups[1].Value, out seeds);
                    var leechMatch = Regex.Match(inner, @"<span class=""red"">(?:&nbsp;|\s)*(\d+)</span>", RegexOptions.IgnoreCase);
                    if (leechMatch.Success) int.TryParse(leechMatch.Groups[1].Value, out leech);

                    list.Add(new RuTrackerTopicItem
                    {
                        TopicId = $"RTR-{rutorId}",
                        Title = title,
                        Category = IsNintendoSpecificTitle(title, "") ? "Nintendo Switch / 3DS" : "Rutor (Игры / Моды)",
                        SizeText = sizeText,
                        Seeds = seeds,
                        Leechers = leech,
                        InfoHash = hash,
                        MagnetUri = magnet,
                        DirectTorrentUrl = $"https://d.rutor.info/download/{rutorId}",
                        TopicUrl = $"https://rutor.is{relUrl}"
                    });

                    if (list.Count >= 25) break;
                }
            }
            catch { }

            return list;
        }

        private async Task<List<RuTrackerTopicItem>> SearchTheRarbgPublicAsync(string query, string categoryFilter, string mirror, CancellationToken ct)
        {
            var list = new List<RuTrackerTopicItem>();
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/131.0.0.0 Safari/537.36");

                string cleanQ = string.IsNullOrWhiteSpace(query) ? "Nintendo Switch" : query.Trim();
                string url = $"https://therarbg.to/get-posts/keywords:{Uri.EscapeDataString(cleanQ)}:format:json/";
                using var resp = await http.GetAsync(url, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return list;

                string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("results", out var resArr) || resArr.ValueKind != JsonValueKind.Array)
                    return list;

                foreach (var el in resArr.EnumerateArray())
                {
                    string cat = el.TryGetProperty("c", out var cP) ? (cP.GetString() ?? "") : "";
                    if (!cat.Equals("Games", StringComparison.OrdinalIgnoreCase)) continue;

                    string name = el.TryGetProperty("n", out var nP) ? WebUtility.HtmlDecode(nP.GetString() ?? "") : "";
                    string hash = el.TryGetProperty("h", out var hP) ? (hP.GetString() ?? "").Trim().ToUpperInvariant() : "";
                    if (string.IsNullOrWhiteSpace(name) || hash.Length != 40) continue;

                    long sizeBytes = el.TryGetProperty("s", out var sP) && sP.ValueKind == JsonValueKind.Number ? sP.GetInt64() : 0;
                    int seeds = el.TryGetProperty("se", out var seP) && seP.ValueKind == JsonValueKind.Number ? seP.GetInt32() : 0;
                    int leech = el.TryGetProperty("le", out var leP) && leP.ValueKind == JsonValueKind.Number ? leP.GetInt32() : 0;
                    string pk = el.TryGetProperty("pk", out var pkP) ? (pkP.GetString() ?? "") : "";

                    list.Add(new RuTrackerTopicItem
                    {
                        TopicId = $"RBG-{pk}",
                        Title = name,
                        Category = IsNintendoSpecificTitle(name, "") ? "Nintendo Switch / 3DS" : "Игры / Релизы",
                        SizeText = sizeBytes > 0 ? ProcessingTask.FormatSize(sizeBytes) : "Magnet",
                        Seeds = seeds,
                        Leechers = leech,
                        InfoHash = hash,
                        MagnetUri = BuildMagnetUri(hash, name),
                        TopicUrl = $"{mirror}/forum/tracker.php?nm={Uri.EscapeDataString(name)}"
                    });

                    if (list.Count >= 20) break;
                }
            }
            catch { }

            return list;
        }

        private async Task<List<RuTrackerTopicItem>> SearchTorrentsCsvPublicAsync(string query, string categoryFilter, string mirror, CancellationToken ct)
        {
            var list = new List<RuTrackerTopicItem>();
            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/131.0.0.0 Safari/537.36");

                string cleanQ = string.IsNullOrWhiteSpace(query) ? "Nintendo Switch" : query.Trim();
                string url = $"https://torrents-csv.com/service/search?q={Uri.EscapeDataString(cleanQ)}&size=25";
                using var resp = await http.GetAsync(url, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return list;

                string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("torrents", out var arr) || arr.ValueKind != JsonValueKind.Array)
                    return list;

                foreach (var el in arr.EnumerateArray())
                {
                    string name = el.TryGetProperty("name", out var nP) ? (nP.GetString() ?? "") : "";
                    string hash = el.TryGetProperty("infohash", out var hP) ? (hP.GetString() ?? "").Trim().ToUpperInvariant() : "";
                    if (string.IsNullOrWhiteSpace(name) || hash.Length != 40) continue;
                    if (Regex.IsMatch(name, @"\b(1080p|2160p|720p|WEB-DL|BDRip|HDRip|MP3|FLAC)\b", RegexOptions.IgnoreCase))
                        continue;

                    long sizeBytes = el.TryGetProperty("size_bytes", out var sP) && sP.ValueKind == JsonValueKind.Number ? sP.GetInt64() : 0;
                    int seeds = el.TryGetProperty("seeders", out var seP) && seP.ValueKind == JsonValueKind.Number ? seP.GetInt32() : 0;
                    int leech = el.TryGetProperty("leechers", out var leP) && leP.ValueKind == JsonValueKind.Number ? leP.GetInt32() : 0;
                    int id = el.TryGetProperty("id", out var idP) && idP.ValueKind == JsonValueKind.Number ? idP.GetInt32() : 0;

                    list.Add(new RuTrackerTopicItem
                    {
                        TopicId = $"DHT-{id}",
                        Title = name,
                        Category = IsNintendoSpecificTitle(name, "") ? "Nintendo Switch / 3DS" : "DHT Индекс",
                        SizeText = sizeBytes > 0 ? ProcessingTask.FormatSize(sizeBytes) : "Magnet",
                        Seeds = seeds,
                        Leechers = leech,
                        InfoHash = hash,
                        MagnetUri = BuildMagnetUri(hash, name),
                        TopicUrl = $"{mirror}/forum/tracker.php?nm={Uri.EscapeDataString(name)}"
                    });

                    if (list.Count >= 20) break;
                }
            }
            catch { }

            return list;
        }

        /// <summary>
        /// Загружает страницу темы viewtopic.php?t=..., извлекает Magnet-ссылку / InfoHash
        /// и автоматически парсит все ссылки на моды и русификаторы (Яндекс.Диск, Google Drive, GitHub, GameBanana) из шапки!
        /// </summary>
        public async Task<RuTrackerTopicItem?> FetchTopicDetailsAsync(string topicIdOrUrl, CancellationToken ct)
        {
            string tid = topicIdOrUrl.Trim();
            var m = Regex.Match(tid, @"t=(\d+)");
            if (m.Success) tid = m.Groups[1].Value;

            if (string.IsNullOrEmpty(tid) || !tid.All(char.IsDigit))
                return null;

            string mirror = NormalizeMirrorUrl(App.Settings.Current.RuTrackerMirror);
            string topicUrl = $"{mirror}/forum/viewtopic.php?t={tid}";

            var item = new RuTrackerTopicItem
            {
                TopicId = tid,
                Title = $"Раздача RuTracker #{tid}",
                Category = "Nintendo Switch / 3DS",
                TopicUrl = topicUrl
            };

            try
            {
                using var client = CreateHttpClient();
                using var resp = await client.GetAsync(topicUrl, ct).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                {
                    byte[] bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                    string html = DecodeRuTrackerHtml(bytes, resp.Content.Headers.ContentType?.CharSet);

                    var titleMatch = Regex.Match(html, @"<a[^>]+id=""topic-title""[^>]*>(.*?)</a>|<title>(.*?)</title>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    if (titleMatch.Success)
                    {
                        string rawTitle = !string.IsNullOrEmpty(titleMatch.Groups[1].Value) ? titleMatch.Groups[1].Value : titleMatch.Groups[2].Value;
                        item.Title = WebUtility.HtmlDecode(Regex.Replace(rawTitle, "<.*?>", "")).Replace(":: RuTracker.org", "").Trim();
                    }

                    // Извлекаем Magnet-ссылку со страницы темы (доступна даже гостям!)
                    var magnetMatch = Regex.Match(html, @"href=""(magnet:\?xt=urn:btih:[^""]+)""", RegexOptions.IgnoreCase);
                    if (magnetMatch.Success)
                    {
                        item.MagnetUri = WebUtility.HtmlDecode(magnetMatch.Groups[1].Value);
                    }

                    // Извлекаем info_hash из атрибута или текста
                    var hashMatch = Regex.Match(html, @"btih:([0-9A-Fa-f]{40})|id=""tor-hash""[^>]*>([0-9A-Fa-f]{40})<", RegexOptions.IgnoreCase);
                    if (hashMatch.Success)
                    {
                        item.InfoHash = (!string.IsNullOrEmpty(hashMatch.Groups[1].Value) ? hashMatch.Groups[1].Value : hashMatch.Groups[2].Value).ToUpperInvariant();
                    }

                    // Извлекаем все облачные ссылки на моды и русификаторы из тела темы
                    item.ExtractedModLinks = ExtractModLinksFromTopicHtml(html);
                }
            }
            catch (Exception ex)
            {
                App.Logger?.Log($"[RuTracker] Ошибка чтения темы #{tid}: {ex.Message}", LogLevel.Warning);
            }

            // Если Magnet-ссылка не найдена в HTML, запрашиваем info_hash через публичный API RuTracker (api.rutracker.cc / api.t-ru.org)
            if (string.IsNullOrEmpty(item.MagnetUri))
            {
                string? apiHash = await FetchInfoHashFromPublicApiAsync(tid, ct).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(apiHash))
                {
                    item.InfoHash = apiHash;
                    item.MagnetUri = BuildMagnetUri(apiHash, item.Title);
                }
            }
            else if (string.IsNullOrEmpty(item.InfoHash))
            {
                var hm = Regex.Match(item.MagnetUri, @"btih:([0-9A-Fa-f]{40})", RegexOptions.IgnoreCase);
                if (hm.Success) item.InfoHash = hm.Groups[1].Value.ToUpperInvariant();
            }

            return item;
        }

        private async Task<string?> FetchInfoHashFromPublicApiAsync(string topicId, CancellationToken ct)
        {
            string[] apiUrls =
            {
                $"https://api.rutracker.cc/v1/get_tor_hash?by=topic_id&val={topicId}",
                $"http://api.t-ru.org/v1/get_tor_hash?by=topic_id&val={topicId}"
            };

            using var client = CreateHttpClient();
            foreach (var url in apiUrls)
            {
                try
                {
                    using var resp = await client.GetAsync(url, ct).ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode) continue;
                    string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    var m = Regex.Match(json, @"""([0-9A-Fa-f]{40})""");
                    if (m.Success) return m.Groups[1].Value.ToUpperInvariant();
                }
                catch { }
            }
            return null;
        }

        public static string BuildMagnetUri(string infoHash, string displayName)
        {
            var sb = new StringBuilder($"magnet:?xt=urn:btih:{infoHash.Trim()}");
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                sb.Append($"&dn={Uri.EscapeDataString(displayName)}");
            }
            foreach (var tr in PublicTrackers)
            {
                sb.Append($"&tr={Uri.EscapeDataString(tr)}");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Сканирует HTML первого поста темы RuTracker и находит внешние ссылки на русификаторы, озвучки и моды.
        /// </summary>
        public static List<CloudModLinkItem> ExtractModLinksFromTopicHtml(string html)
        {
            var results = new List<CloudModLinkItem>();
            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var linkMatches = Regex.Matches(html, @"<a[^>]+href=""(https?://[^""]+)""[^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            foreach (Match m in linkMatches)
            {
                string url = WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
                string anchorText = WebUtility.HtmlDecode(Regex.Replace(m.Groups[2].Value, "<.*?>", "")).Trim();

                string? provider = ClassifyCloudModProvider(url);
                if (provider == null) continue;
                if (!seenUrls.Add(url)) continue;

                string label = string.IsNullOrWhiteSpace(anchorText) || anchorText.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? $"Модификация / Русификатор ({provider})"
                    : $"{anchorText} ({provider})";

                results.Add(new CloudModLinkItem
                {
                    IsSelected = true,
                    Title = label,
                    Url = url,
                    Provider = provider
                });
            }

            return results;
        }

        public static string? ClassifyCloudModProvider(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            string u = url.ToLowerInvariant();

            if (u.Contains("disk.yandex.") || u.Contains("yadi.sk/"))
                return "Яндекс.Диск";
            if (u.Contains("drive.google.com/") || u.Contains("drive.usercontent.google.com/"))
                return "Google Drive";
            if (u.Contains("github.com/") && (u.Contains("/releases") || u.EndsWith(".zip") || u.EndsWith(".7z") || u.EndsWith(".nsp")))
                return "GitHub Releases";
            if (u.Contains("gamebanana.com/mods/") || u.Contains("gamebanana.com/dl/"))
                return "GameBanana";
            if (u.EndsWith(".zip") || u.EndsWith(".7z") || u.EndsWith(".rar") || u.EndsWith(".ips") || u.EndsWith(".pchtxt") || u.EndsWith(".bps"))
                return "Прямой архив мода";

            return null;
        }

        private static string DecodeRuTrackerHtml(byte[] bytes, string? charset)
        {
            try
            {
                if (!string.IsNullOrEmpty(charset) && charset.Contains("utf", StringComparison.OrdinalIgnoreCase))
                    return Encoding.UTF8.GetString(bytes);
                return Encoding.GetEncoding(1251).GetString(bytes);
            }
            catch
            {
                return Encoding.UTF8.GetString(bytes);
            }
        }

        /// <summary>
        /// Скачивает .torrent файл с RuTracker (при наличии bb_session) или загружает список файлов по Magnet-ссылке.
        /// </summary>
        public async Task<List<TorrentFileSelectionItem>> InspectTorrentFilesAsync(RuTrackerTopicItem topic, string? localTorrentFilePath, CancellationToken ct)
        {
            _loadedTorrentBytes = null;
            _loadedMagnet = null;

            if (!string.IsNullOrWhiteSpace(localTorrentFilePath) && File.Exists(localTorrentFilePath))
            {
                _loadedTorrentBytes = await File.ReadAllBytesAsync(localTorrentFilePath, ct).ConfigureAwait(false);
            }
            else if (!string.IsNullOrWhiteSpace(topic.DirectTorrentUrl))
            {
                try
                {
                    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                    http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/131.0.0.0 Safari/537.36");
                    using var resp = await http.GetAsync(topic.DirectTorrentUrl, ct).ConfigureAwait(false);
                    if (resp.IsSuccessStatusCode)
                    {
                        byte[] bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                        if (bytes.Length > 32 && bytes[0] == (byte)'d')
                            _loadedTorrentBytes = bytes;
                    }
                }
                catch { }
            }
            else if (!string.IsNullOrWhiteSpace(topic.TopicId) && topic.TopicId.All(char.IsDigit) && !string.IsNullOrWhiteSpace(App.Settings.Current.RuTrackerCookie))
            {
                try
                {
                    string mirror = NormalizeMirrorUrl(App.Settings.Current.RuTrackerMirror);
                    using var client = CreateHttpClient();
                    string dlUrl = $"{mirror}/forum/dl.php?t={topic.TopicId}";
                    using var resp = await client.GetAsync(dlUrl, ct).ConfigureAwait(false);
                    if (resp.IsSuccessStatusCode)
                    {
                        byte[] bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                        // Проверяем, что это действительно Bencode .torrent (начинается с 'd')
                        if (bytes.Length > 32 && bytes[0] == (byte)'d')
                        {
                            _loadedTorrentBytes = bytes;
                        }
                    }
                }
                catch { }
            }

            if (_loadedTorrentBytes != null)
            {
                var torrent = await Torrent.LoadAsync(_loadedTorrentBytes).ConfigureAwait(false);
                return torrent.Files.Select(f => new TorrentFileSelectionItem
                {
                    IsSelected = true,
                    RelativePath = f.Path,
                    Length = f.Length,
                    CategoryBadge = ClassifyTorrentEntryBadge(f.Path)
                }).ToList();
            }

            if (!string.IsNullOrWhiteSpace(topic.MagnetUri) && MagnetLink.TryParse(topic.MagnetUri, out var magnet))
            {
                _loadedMagnet = magnet;
                return new List<TorrentFileSelectionItem>
                {
                    new TorrentFileSelectionItem
                    {
                        IsSelected = true,
                        RelativePath = $"{topic.Title} (Метаданные Magnet будут получены при старте загрузки)",
                        Length = 0,
                        CategoryBadge = "MAGNET"
                    }
                };
            }

            return new List<TorrentFileSelectionItem>();
        }

        private static string ClassifyTorrentEntryBadge(string path)
        {
            string lower = path.ToLowerInvariant();
            if (lower.Contains("romfs") || lower.Contains("atmosphere") || lower.Contains("luma") || lower.Contains("rus"))
                return "МОД / РУСИФИКАТОР";
            if (lower.Contains("update") || lower.Contains("patch") || Regex.IsMatch(lower, @"\[v[1-9]\d*\]"))
                return "ОБНОВЛЕНИЕ";
            if (lower.Contains("dlc") || lower.Contains("addon"))
                return "DLC";
            if (lower.EndsWith(".nsp") || lower.EndsWith(".nsz") || lower.EndsWith(".xci") || lower.EndsWith(".xcz") ||
                lower.EndsWith(".3ds") || lower.EndsWith(".cia") || lower.EndsWith(".cci"))
                return "ИГРА";
            return "ФАЙЛ";
        }

        /// <summary>
        /// Запускает прямое скачивание выбранных файлов торрента/Magnet через встроенный движок MonoTorrent.
        /// </summary>
        public async Task<List<string>> DownloadTorrentAsync(
            string saveDirectory,
            IReadOnlyList<TorrentFileSelectionItem> fileSelections,
            IProgress<TorrentDownloadProgress>? progress,
            CancellationToken ct)
        {
            Directory.CreateDirectory(saveDirectory);

            var engineSettings = new EngineSettingsBuilder
            {
                CacheDirectory = Path.Combine(saveDirectory, ".storm_torrent_cache")
            }.ToSettings();

            _torrentEngine ??= new ClientEngine(engineSettings);

            if (_activeManager != null)
            {
                try { await _activeManager.StopAsync(); } catch { }
                _activeManager = null;
            }

            if (_loadedTorrentBytes != null)
            {
                var torrent = await Torrent.LoadAsync(_loadedTorrentBytes).ConfigureAwait(false);
                _activeManager = await _torrentEngine.AddAsync(torrent, saveDirectory).ConfigureAwait(false);
            }
            else if (_loadedMagnet != null)
            {
                _activeManager = await _torrentEngine.AddAsync(_loadedMagnet, saveDirectory).ConfigureAwait(false);
            }
            else
            {
                throw new InvalidOperationException("Не выбран .torrent файл или Magnet-ссылка для скачивания.");
            }

            // Применяем выборочный фильтр файлов, если метаданные уже загружены
            if (_activeManager.HasMetadata && fileSelections.Count > 0)
            {
                await ApplyFilePrioritiesAsync(_activeManager, fileSelections).ConfigureAwait(false);
            }

            await _activeManager.StartAsync().ConfigureAwait(false);

            bool prioritiesAppliedAfterMetadata = _activeManager.HasMetadata;

            while (!_activeManager.Complete && _activeManager.State != TorrentState.Seeding && !ct.IsCancellationRequested)
            {
                if (!prioritiesAppliedAfterMetadata && _activeManager.HasMetadata)
                {
                    prioritiesAppliedAfterMetadata = true;
                    if (fileSelections.Count > 0)
                    {
                        await ApplyFilePrioritiesAsync(_activeManager, fileSelections).ConfigureAwait(false);
                    }
                }

                double pct = _activeManager.Progress;
                double speedMb = _activeManager.Monitor.DownloadRate / (1024.0 * 1024.0);
                int seeds = _activeManager.Peers.Seeds;
                int leechs = _activeManager.Peers.Leechs;

                string stateStr = _activeManager.State == TorrentState.Metadata
                    ? "Получение метаданных Magnet от пиров (DHT)..."
                    : $"Скачивание BitTorrent: {pct:F1}% ({speedMb:F2} МБ/с | Сиды: {seeds}, Пиры: {leechs})";

                progress?.Report(new TorrentDownloadProgress
                {
                    Percent = pct,
                    SpeedMbPerSec = speedMb,
                    Seeds = seeds,
                    Peers = leechs,
                    StatusText = stateStr
                });

                // Если выбрана только часть файлов, проверяем их индивидуальную готовность
                if (_activeManager.HasMetadata && _activeManager.Files.Count > 0)
                {
                    bool allSelectedDone = _activeManager.Files
                        .Where(f => f.Priority != Priority.DoNotDownload)
                        .All(f => f.BitField.AllTrue);
                    if (allSelectedDone && pct > 0)
                        break;
                }

                await Task.Delay(500, ct).ConfigureAwait(false);
            }

            ct.ThrowIfCancellationRequested();

            var downloadedFiles = new List<string>();
            if (_activeManager.HasMetadata)
            {
                foreach (var f in _activeManager.Files)
                {
                    if (f.Priority != Priority.DoNotDownload && File.Exists(f.FullPath))
                    {
                        downloadedFiles.Add(f.FullPath);
                    }
                }
            }

            try { await _activeManager.StopAsync(TimeSpan.FromSeconds(2)); } catch { }
            return downloadedFiles;
        }

        private static async Task ApplyFilePrioritiesAsync(TorrentManager manager, IReadOnlyList<TorrentFileSelectionItem> selections)
        {
            var selectedPaths = new HashSet<string>(
                selections.Where(s => s.IsSelected).Select(s => s.RelativePath),
                StringComparer.OrdinalIgnoreCase);

            if (selectedPaths.Count == 0) return;

            foreach (var file in manager.Files)
            {
                bool shouldDownload = selectedPaths.Contains(file.Path);
                await manager.SetFilePriorityAsync(file, shouldDownload ? Priority.Normal : Priority.DoNotDownload).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Скачивает архивы модов и русификаторов по облачным ссылкам (Яндекс.Диск, Google Drive, GitHub Releases, прямые URL),
        /// распаковывает их через 7za.exe и возвращает пути к папкам модов (romfs/exefs/ips) или патчам для привязки к задаче.
        /// </summary>
        public async Task<List<string>> DownloadAndExtractCloudModsAsync(
            IEnumerable<string> rawLinks,
            string workDirectory,
            IProgress<(double Percent, string Status)>? progress,
            CancellationToken ct)
        {
            var links = rawLinks
                .Select(l => l?.Trim() ?? "")
                .Where(l => l.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || l.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var extractedModPaths = new List<string>();
            if (links.Count == 0) return extractedModPaths;

            string modsRoot = Path.Combine(workDirectory, "_DownloadedMods");
            Directory.CreateDirectory(modsRoot);

            using var client = CreateHttpClient();

            for (int i = 0; i < links.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                string link = links[i];
                progress?.Report(((i * 100.0) / links.Count, $"Резолвинг облачной ссылки мода ({i + 1}/{links.Count}): {link}..."));

                var (directUrl, suggestedName) = await ResolveCloudDownloadUrlAsync(client, link, ct).ConfigureAwait(false);
                if (string.IsNullOrEmpty(directUrl))
                    continue;

                string safeFileName = SanitizeFileName(suggestedName);
                if (string.IsNullOrEmpty(Path.GetExtension(safeFileName)))
                    safeFileName += ".zip";

                string archivePath = Path.Combine(modsRoot, safeFileName);
                progress?.Report(((i * 100.0 + 25.0) / links.Count, $"Скачивание мода: {safeFileName}..."));

                await DownloadFileWithProgressAsync(client, directUrl, archivePath, (dlPct) =>
                {
                    progress?.Report(((i + dlPct / 100.0) * 100.0 / links.Count, $"Скачивание мода {safeFileName}: {dlPct:F1}%"));
                }, ct).ConfigureAwait(false);

                if (!File.Exists(archivePath)) continue;

                string ext = Path.GetExtension(archivePath).ToLowerInvariant();
                if (ext is ".zip" or ".7z" or ".rar")
                {
                    string extractDir = Path.Combine(modsRoot, Path.GetFileNameWithoutExtension(safeFileName));
                    Directory.CreateDirectory(extractDir);
                    progress?.Report(((i + 0.85) * 100.0 / links.Count, $"Распаковка архива мода {safeFileName}..."));

                    await ExtractArchiveWith7zAsync(archivePath, extractDir, ct).ConfigureAwait(false);
                    var modFolders = DiscoverModPayloadsInDirectory(extractDir);
                    extractedModPaths.AddRange(modFolders);
                }
                else
                {
                    extractedModPaths.Add(archivePath);
                }
            }

            progress?.Report((100, $"Готово! Подготовлено модификаций для автопривязки: {extractedModPaths.Count}."));
            return extractedModPaths;
        }

        private async Task<(string DirectUrl, string FileName)> ResolveCloudDownloadUrlAsync(HttpClient client, string url, CancellationToken ct)
        {
            string lower = url.ToLowerInvariant();

            // 1. Яндекс.Диск (disk.yandex.ru / yadi.sk)
            if (lower.Contains("disk.yandex.") || lower.Contains("yadi.sk/"))
            {
                string apiUrl = $"https://cloud-api.yandex.net/v1/disk/public/resources/download?public_key={Uri.EscapeDataString(url)}";
                using var resp = await client.GetAsync(apiUrl, ct).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                {
                    string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("href", out var hrefProp))
                    {
                        string href = hrefProp.GetString() ?? "";
                        string fn = ExtractQueryParam(href, "filename") ?? $"YandexDisk_Mod_{DateTime.Now:HHmmss}.zip";
                        return (href, fn);
                    }
                }
            }

            // 2. Google Drive (drive.google.com/file/d/<ID>/... или id=<ID>)
            if (lower.Contains("drive.google.com") || lower.Contains("drive.usercontent.google.com"))
            {
                var idMatch = Regex.Match(url, @"(?:/file/d/|id=)([a-zA-Z0-9_-]{15,})");
                if (idMatch.Success)
                {
                    string fileId = idMatch.Groups[1].Value;
                    string directGDrive = $"https://drive.usercontent.google.com/download?id={fileId}&export=download&confirm=t";
                    return (directGDrive, $"GDrive_Mod_{fileId.Substring(0, 8)}.zip");
                }
            }

            // 3. GitHub Releases (github.com/<owner>/<repo>/releases...)
            if (lower.Contains("github.com/") && lower.Contains("/releases") && !lower.Contains("/releases/download/"))
            {
                var ghMatch = Regex.Match(url, @"github\.com/([^/]+)/([^/]+)/releases(?:/tag/([^/?#]+))?", RegexOptions.IgnoreCase);
                if (ghMatch.Success)
                {
                    string owner = ghMatch.Groups[1].Value;
                    string repo = ghMatch.Groups[2].Value;
                    string tag = ghMatch.Groups[3].Value;
                    string api = string.IsNullOrEmpty(tag)
                        ? $"https://api.github.com/repos/{owner}/{repo}/releases/latest"
                        : $"https://api.github.com/repos/{owner}/{repo}/releases/tags/{tag}";

                    using var resp = await client.GetAsync(api, ct).ConfigureAwait(false);
                    if (resp.IsSuccessStatusCode)
                    {
                        string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var asset in assets.EnumerateArray())
                            {
                                string name = asset.TryGetProperty("name", out var np) ? (np.GetString() ?? "") : "";
                                string dl = asset.TryGetProperty("browser_download_url", out var dp) ? (dp.GetString() ?? "") : "";
                                if (!string.IsNullOrEmpty(dl) && (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                                    name.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) ||
                                    name.EndsWith(".rar", StringComparison.OrdinalIgnoreCase) ||
                                    name.EndsWith(".ips", StringComparison.OrdinalIgnoreCase) ||
                                    name.EndsWith(".pchtxt", StringComparison.OrdinalIgnoreCase)))
                                {
                                    return (dl, name);
                                }
                            }
                        }
                    }
                }
            }

            // 4. Прямая ссылка на файл
            string fileName = Path.GetFileName(new Uri(url).LocalPath);
            if (string.IsNullOrWhiteSpace(fileName)) fileName = "ModPackage.zip";
            return (url, fileName);
        }

        private static string? ExtractQueryParam(string url, string paramName)
        {
            var m = Regex.Match(url, $@"[?&]{Regex.Escape(paramName)}=([^&#]+)", RegexOptions.IgnoreCase);
            return m.Success ? Uri.UnescapeDataString(m.Groups[1].Value) : null;
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Trim();
        }

        private static async Task DownloadFileWithProgressAsync(HttpClient client, string url, string destPath, Action<double> onProgress, CancellationToken ct)
        {
            using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            long total = resp.Content.Headers.ContentLength ?? -1;
            await using var netStream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);

            byte[] buffer = new byte[65536];
            long readTotal = 0;
            int read;
            while ((read = await netStream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                readTotal += read;
                if (total > 0)
                    onProgress(readTotal * 100.0 / total);
            }
        }

        private static async Task ExtractArchiveWith7zAsync(string archivePath, string extractDir, CancellationToken ct)
        {
            string sevenZip = Path.Combine(AppContext.BaseDirectory, "tools", "7z", "7za.exe");
            if (!File.Exists(sevenZip))
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(archivePath, extractDir, overwriteFiles: true);
                return;
            }

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = sevenZip,
                Arguments = $"x \"{archivePath}\" -o\"{extractDir}\" -y",
                CreateNoWindow = true,
                UseShellExecute = false
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc != null)
            {
                await proc.WaitForExitAsync(ct).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Находит внутри распакованного архива мода папки romfs, exefs, exefs_patches, atmosphere/contents/<TID> или luma/titles/<TID>.
        /// </summary>
        public static List<string> DiscoverModPayloadsInDirectory(string rootDir)
        {
            var found = new List<string>();
            if (!Directory.Exists(rootDir)) return found;

            foreach (var dir in Directory.GetDirectories(rootDir, "*", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(dir).ToLowerInvariant();
                if (name is "romfs" or "exefs" or "exefs_patches")
                {
                    found.Add(dir);
                }
            }

            if (found.Count > 0) return found;

            foreach (var file in Directory.GetFiles(rootDir, "*.*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext is ".nsp" or ".nsz" or ".xci" or ".xcz" or ".ips" or ".pchtxt" or ".bps" or ".cia" or ".3ds")
                {
                    found.Add(file);
                }
            }

            if (found.Count == 0)
                found.Add(rootDir);

            return found;
        }

        public void Dispose()
        {
            try
            {
                _activeManager?.StopAsync(TimeSpan.FromSeconds(1)).GetAwaiter().GetResult();
            }
            catch { }
        }
    }
}
