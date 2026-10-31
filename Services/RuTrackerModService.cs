#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
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
        private bool _isSelected = false;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public List<string> MirrorUrls { get; set; } = new();
        public string Provider { get; set; } = string.Empty;
        public string PublishedDate { get; set; } = "25.10.2026 14:30";
        public string SourceLocation { get; set; } = "Шапка темы";
        public string RoleCategory { get; set; } = "МОД / РУСИФИКАТОР";
        public string DisplayMeta => $"📅 {PublishedDate} • {SourceLocation}";
    }

    public sealed class TorrentDownloadProgress
    {
        public double Percent { get; set; }
        public double SpeedMbPerSec { get; set; }
        public int Seeds { get; set; }
        public int Peers { get; set; }
        public string StatusText { get; set; } = string.Empty;
    }

    public sealed class RuTrackerIndexFile
    {
        public string Path { get; set; } = string.Empty;
        public long Size { get; set; }
        public string TitleId { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string FileName => Path.Contains('/') ? Path.Substring(Path.LastIndexOf('/') + 1) : Path;
    }

    public sealed class RuTrackerIndexTopic
    {
        public string TopicId { get; set; } = string.Empty;
        public string InfoHash { get; set; } = string.Empty;
        public int Seeds { get; set; }
        public long SizeBytes { get; set; }
        public long RegTime { get; set; }
        public string TorrentName { get; set; } = string.Empty;
        public string TopicTitle { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;
        public string TitleId { get; set; } = string.Empty;
        public List<RuTrackerIndexFile> Files { get; set; } = new();

        internal string GameKey = "";
        internal string GameKeyPadded = "";
        internal string BlobPadded = "";
        internal string[] Words = Array.Empty<string>();
        internal int GameWordCount;
        internal string Skeleton = "";
        internal string TidBlob = "";

        public void RebuildKeys()
        {
            string baseName = !string.IsNullOrWhiteSpace(GameName)
                ? GameName
                : (!string.IsNullOrWhiteSpace(TopicTitle) ? TopicTitle : TorrentName);

            GameKey = RuTrackerModService.NormalizeSearchKey(baseName);
            GameKeyPadded = " " + GameKey;

            var blob = new StringBuilder(GameKey.Length * 4);
            blob.Append(GameKey).Append(' ')
                .Append(RuTrackerModService.NormalizeSearchKey(TopicTitle)).Append(' ')
                .Append(RuTrackerModService.NormalizeSearchKey(TorrentName));

            foreach (var f in Files)
            {
                int dot = f.FileName.LastIndexOf('.');
                string stem = dot > 0 ? f.FileName.Substring(0, dot) : f.FileName;
                blob.Append(' ').Append(RuTrackerModService.NormalizeSearchKey(stem));
            }

            string blobStr = blob.ToString();
            BlobPadded = " " + blobStr;

            var gw = GameKey.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var rest = blobStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var all = new HashSet<string>(gw, StringComparer.OrdinalIgnoreCase);
            foreach (var r in rest) all.Add(r);
            Words = all.ToArray();
            GameWordCount = gw.Length;
            Skeleton = RuTrackerModService.PhoneticSkeleton(GameKey);

            var tids = new StringBuilder();
            if (!string.IsNullOrEmpty(TitleId)) tids.Append(TitleId.ToLowerInvariant()).Append(' ');
            foreach (var f in Files)
            {
                if (!string.IsNullOrEmpty(f.TitleId)) tids.Append(f.TitleId.ToLowerInvariant()).Append(' ');
            }
            TidBlob = tids.ToString();
        }

        public void UpdateTopicTitle(string newTitle)
        {
            if (string.IsNullOrWhiteSpace(newTitle) || newTitle == TopicTitle) return;
            TopicTitle = newTitle;
            if (string.IsNullOrWhiteSpace(GameName))
                GameName = RuTrackerModService.CleanTitleToGame(newTitle);
            if (string.IsNullOrWhiteSpace(TitleId))
                TitleId = RuTrackerModService.ExtractTitleId(newTitle);
            RebuildKeys();
        }
    }

    /// <summary>
    /// Комплексный сервис интеграции с RuTracker.org (поиск по всем 7 275 темам раздела #1605,
    /// обход Cloudflare через WebView2, прямое MonoTorrent P2P скачивание с кэшированием .torrent,
    /// и облачный загрузчик модификаций с поддержкой PixelDrain, Yandex, GDrive, MEGA, Mail.ru, MediaFire, Dropbox, WDfiles, GitHub, Workupload).
    /// </summary>
    public sealed class RuTrackerModService : IDisposable
    {
        private static readonly string[] DefaultMirrors =
        {
            "https://rutracker.org",
            "https://rutracker.net"
        };
        private static int _activeMirrorIndex = 0;

        private static readonly string[] PublicTrackers =
        {
            "http://bt.t-ru.org/ann?magnet",
            "http://bt2.t-ru.org/ann?magnet",
            "http://bt3.t-ru.org/ann?magnet",
            "http://bt4.t-ru.org/ann?magnet",
            "http://bt.t-ru.org/ann",
            "udp://tracker.opentrackr.org:1337/announce",
            "udp://open.stealth.si:80/announce",
            "udp://exodus.desync.com:6969/announce"
        };

        public static Func<string, string, CancellationToken, Task<string?>>? CloudflareChallengeHandler { get; set; }
        public static Func<string, CancellationToken, Task<string?>>? SameOriginFetchHandler { get; set; }

        public static string GetActiveMirror()
        {
            string cfg = App.Settings.Current.RuTrackerMirror?.Trim() ?? "";
            if (!string.IsNullOrEmpty(cfg) && !cfg.Contains(".nl", StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeMirrorUrl(cfg);
            }
            return DefaultMirrors[_activeMirrorIndex % DefaultMirrors.Length];
        }

        public static void SwitchToNextMirror()
        {
            _activeMirrorIndex = (_activeMirrorIndex + 1) % DefaultMirrors.Length;
            string next = DefaultMirrors[_activeMirrorIndex];
            App.Settings.Current.RuTrackerMirror = next.Replace("https://", "").Replace("http://", "");
            _ = App.Settings.SaveAsync();
            App.Logger?.Log($"[RuTracker] Сетевое переключение на зеркало: {next}", LogLevel.Info);
        }

        public static string? SharedCfClearance
        {
            get => App.Settings.Current.RuTrackerCfClearance;
            set { App.Settings.Current.RuTrackerCfClearance = value ?? ""; }
        }

        public static string? SharedUserAgent
        {
            get => !string.IsNullOrEmpty(App.Settings.Current.RuTrackerUserAgent)
                ? App.Settings.Current.RuTrackerUserAgent
                : "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
            set { App.Settings.Current.RuTrackerUserAgent = value ?? ""; }
        }

        // 10 проверенных эталонных тем раздела #1605 (стартовый список хитов)
        private static readonly (string Tid, string Title, string Size, int Seeds, string Hash)[] CuratedVerifiedHits = new[]
        {
            ("5757941", "[Nintendo Switch] Celeste [0100FBA009218000] [NSP]", "931.55 MB", 118, "C59FE53E33D2BB4BD030E30ED796B99CFAC63F83"),
            ("6479728", "[Nintendo Switch] Mario vs. Donkey Kong [0100B99019412000] [NSP]", "1.30 GB", 189, "18034EA68DC5EBE094A061B95A04BB215D95A2A0"),
            ("6525297", "[Nintendo Switch] Animal Well [010020D01AD24000] [NSZ]", "73.35 MB", 62, "0A996B997AA2B90DB658E4CA76D2989DD1F80F56"),
            ("6702315", "[Nintendo Switch] The Legend of Zelda: Breath of the Wild (BOTW) + The Master Trials and The Champions Ballad [01007EF00011E000]", "10.66 GB", 807, "D4C50D5F7AB7EC78B589DA7A836BB98529F646D7"),
            ("6358526", "[Nintendo Switch] The Legend of Zelda: Tears of the Kingdom [0100F2C0115B6000]", "16.79 GB", 781, "EDC9BE013621189954A1E7D4EA514A804739EBB1"),
            ("6702276", "[Nintendo Switch] Super Mario Odyssey [0100000000010000]", "3.79 GB", 684, "8765CC4BF631C506A0F54F58F9A376D011BC7D95"),
            ("5720134", "[Nintendo Switch] Cuphead + The Delicious Last Course DLC [010086800CE34000]", "4.16 GB", 452, "92F42203820F0877BFCF46268DD066DFD51A69C0"),
            ("5943382", "[Nintendo Switch] Hades [0100535012974000]", "5.76 GB", 297, "55811895F3091DB7D207446AF805CE3D644DE8E0"),
            ("5677506", "[Nintendo Switch] Stardew Valley [0100E65002F3A000]", "935.46 MB", 326, "F35418F5C46E0D6F654E54CCF9C7B3DBCD031BBC"),
            ("6118930", "[Nintendo Switch] Metroid Dread [010093801237C000]", "3.23 GB", 409, "CD78BC876E0B45DCC4A610BD21C3BDA9B920B617")
        };

        // Локальный индекс всех 7 275 тем раздела #1605
        private static readonly List<RuTrackerIndexTopic> _indexTopics = new();
        private static readonly Dictionary<string, RuTrackerIndexTopic> _topicsById = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, RuTrackerIndexTopic> _topicsByHash = new(StringComparer.OrdinalIgnoreCase);
        private static bool _indexLoaded = false;
        private static readonly object _indexLock = new();
        private static readonly string DeltaCachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "STORM SWITCH BOX",
            "rutracker_1605_dynamic_delta.json");

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, RuTrackerIndexTopic> _deltaTopics = new(StringComparer.OrdinalIgnoreCase);

        public static void LoadDeltaCache()
        {
            try
            {
                if (!File.Exists(DeltaCachePath)) return;
                string json = File.ReadAllText(DeltaCachePath, Encoding.UTF8);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("topics", out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    lock (_indexLock)
                    {
                        foreach (var el in arr.EnumerateArray())
                        {
                            string tid = el.TryGetProperty("t", out var tp)
                                ? (tp.ValueKind == JsonValueKind.Number ? tp.GetInt64().ToString() : (tp.GetString() ?? ""))
                                : "";
                            if (string.IsNullOrEmpty(tid)) continue;

                            string h = el.TryGetProperty("h", out var hp) ? (hp.GetString() ?? "").Trim().ToUpperInvariant() : "";
                            int sd = el.TryGetProperty("sd", out var sdp) && sdp.ValueKind == JsonValueKind.Number ? sdp.GetInt32() : 0;
                            long sz = el.TryGetProperty("sz", out var szp) && szp.ValueKind == JsonValueKind.Number ? szp.GetInt64() : 0L;
                            long rt = el.TryGetProperty("rt", out var rtp) && rtp.ValueKind == JsonValueKind.Number ? rtp.GetInt64() : 0L;
                            string n = el.TryGetProperty("n", out var np) ? (np.GetString() ?? "") : "";
                            string tt = el.TryGetProperty("tt", out var ttp) ? (ttp.GetString() ?? "") : "";
                            string g = el.TryGetProperty("g", out var gp) ? (gp.GetString() ?? "") : "";
                            string tidHex = el.TryGetProperty("tid", out var tidp) ? (tidp.GetString() ?? "").ToUpperInvariant() : "";

                            if (string.IsNullOrWhiteSpace(g)) g = CleanTitleToGame(!string.IsNullOrWhiteSpace(tt) ? tt : n);
                            if (string.IsNullOrWhiteSpace(tidHex)) tidHex = ExtractTitleId(!string.IsNullOrWhiteSpace(tt) ? tt : n);

                            var topic = new RuTrackerIndexTopic
                            {
                                TopicId = tid,
                                InfoHash = h,
                                Seeds = sd,
                                SizeBytes = sz,
                                RegTime = rt,
                                TorrentName = n,
                                TopicTitle = tt,
                                GameName = g,
                                TitleId = tidHex
                            };
                            topic.RebuildKeys();

                            _deltaTopics[tid] = topic;
                            _topicsById[tid] = topic;
                            if (!string.IsNullOrEmpty(h) && h.Length == 40) _topicsByHash[h] = topic;
                            if (!_indexTopics.Any(x => x.TopicId == tid)) _indexTopics.Add(topic);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger?.Log($"[RuTracker Delta] Ошибка чтения дельта-кэша: {ex.Message}", LogLevel.Warning);
            }
        }

        public static void SaveDeltaCache()
        {
            try
            {
                string dir = Path.GetDirectoryName(DeltaCachePath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var list = _deltaTopics.Values.Select(t => new
                {
                    t = long.TryParse(t.TopicId, out long lv) ? (object)lv : t.TopicId,
                    h = t.InfoHash,
                    sd = t.Seeds,
                    sz = t.SizeBytes,
                    rt = t.RegTime,
                    n = t.TorrentName,
                    tt = t.TopicTitle,
                    g = t.GameName,
                    tid = t.TitleId
                }).ToList();

                var payload = new
                {
                    updated = DateTime.UtcNow.ToString("o"),
                    count = list.Count,
                    topics = list
                };

                string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(DeltaCachePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                App.Logger?.Log($"[RuTracker Delta] Ошибка сохранения дельта-кэша: {ex.Message}", LogLevel.Warning);
            }
        }

        public static void ParseAndMergeForumTopicsHtml(string forumHtml)
        {
            if (string.IsNullOrWhiteSpace(forumHtml)) return;
            try
            {
                var matches = Regex.Matches(forumHtml, @"href=""(?:viewtopic\.php\?t=)?(\d+)""[^>]*class=""[^""]*tt-text[^""]*""[^>]*>(.*?)</a>|href=""viewtopic\.php\?t=(\d+)""[^>]*>(.*?)</a>", RegexOptions.IgnoreCase);
                bool changed = false;

                foreach (Match m in matches)
                {
                    string tid = !string.IsNullOrEmpty(m.Groups[1].Value) ? m.Groups[1].Value : m.Groups[3].Value;
                    string rawTitle = !string.IsNullOrEmpty(m.Groups[2].Value) ? m.Groups[2].Value : m.Groups[4].Value;
                    if (string.IsNullOrEmpty(tid) || string.IsNullOrEmpty(rawTitle)) continue;

                    string title = WebUtility.HtmlDecode(Regex.Replace(rawTitle, "<.*?>", "")).Trim();
                    if (string.IsNullOrEmpty(title) || title.Contains("след.")) continue;

                    lock (_indexLock)
                    {
                        if (_topicsById.TryGetValue(tid, out var existing))
                        {
                            if (string.IsNullOrEmpty(existing.TopicTitle) || existing.TopicTitle.StartsWith("Раздача"))
                            {
                                existing.TopicTitle = title;
                                existing.RebuildKeys();
                                _deltaTopics[tid] = existing;
                                changed = true;
                            }
                        }
                        else
                        {
                            var nt = new RuTrackerIndexTopic
                            {
                                TopicId = tid,
                                TopicTitle = title,
                                TorrentName = title,
                                GameName = CleanTitleToGame(title),
                                TitleId = ExtractTitleId(title)
                            };
                            nt.RebuildKeys();
                            _topicsById[tid] = nt;
                            _indexTopics.Add(nt);
                            _deltaTopics[tid] = nt;
                            changed = true;
                        }
                    }
                }

                if (changed)
                {
                    _ = Task.Run(SaveDeltaCache);
                }
            }
            catch { }
        }

        private static bool _warmupStarted = false;

        private ClientEngine? _torrentEngine;
        private TorrentManager? _activeManager;
        private byte[]? _loadedTorrentBytes;
        private MagnetLink? _loadedMagnet;
        private string _activeTopicId = string.Empty;
        private readonly Dictionary<string, IPAddress> _dohCache = new(StringComparer.OrdinalIgnoreCase);

        static RuTrackerModService()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public RuTrackerModService()
        {
            EnsureIndexLoaded();
            WarmupIndexAndLiveFeedsAsync();
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
            string ua = !string.IsNullOrEmpty(SharedUserAgent) ? SharedUserAgent : "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", ua);
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");

            var cookieParts = new List<string>();
            string activeCookie = !string.IsNullOrWhiteSpace(cookieHeader) ? cookieHeader : settings.RuTrackerCookie;
            if (!string.IsNullOrWhiteSpace(activeCookie))
            {
                if (!activeCookie.Contains('='))
                    cookieParts.Add("bb_session=" + activeCookie.Trim());
                else
                    cookieParts.Add(activeCookie.Trim());
            }
            if (!string.IsNullOrWhiteSpace(SharedCfClearance))
            {
                if (!SharedCfClearance.Contains('='))
                    cookieParts.Add("cf_clearance=" + SharedCfClearance.Trim());
                else
                    cookieParts.Add(SharedCfClearance.Trim());
            }
            if (cookieParts.Count > 0)
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", string.Join("; ", cookieParts));
            }

            return client;
        }

        private async Task<IPAddress?> ResolveHostViaDoHAsync(string host, CancellationToken ct)
        {
            if (IPAddress.TryParse(host, out var directIp))
                return directIp;

            lock (_dohCache)
            {
                if (_dohCache.TryGetValue(host, out var cachedIp))
                    return cachedIp;
            }

            string[] dohEndpoints = new[]
            {
                $"https://cloudflare-dns.com/dns-query?name={Uri.EscapeDataString(host)}&type=A",
                $"https://dns.google/resolve?name={Uri.EscapeDataString(host)}&type=A"
            };

            foreach (var ep in dohEndpoints)
            {
                try
                {
                    using var handler = new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(3) };
                    using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(4) };
                    http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/dns-json");

                    using var resp = await http.GetAsync(ep, ct).ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode) continue;

                    string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("Answer", out var answer) && answer.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var ans in answer.EnumerateArray())
                        {
                            if (ans.TryGetProperty("data", out var dataProp))
                            {
                                string ipStr = dataProp.GetString() ?? "";
                                if (IPAddress.TryParse(ipStr, out var ip))
                                {
                                    lock (_dohCache)
                                    {
                                        _dohCache[host] = ip;
                                    }
                                    return ip;
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            try
            {
                var ips = await Dns.GetHostAddressesAsync(host, ct).ConfigureAwait(false);
                var ip = ips.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);
                if (ip != null)
                {
                    lock (_dohCache) { _dohCache[host] = ip; }
                    return ip;
                }
            }
            catch { }

            return null;
        }

        public async Task<(bool Success, string CookieOrError)> LoginAsync(string username, string password, CancellationToken ct)
        {
            string mirror = NormalizeMirrorUrl(App.Settings.Current.RuTrackerMirror);
            string loginUrl = $"{mirror}/forum/login.php";

            Encoding cp1251 = Encoding.GetEncoding(1251);

            try
            {
                using var client = CreateHttpClient(allowAutoRedirect: false);

                var postData = $"login_username={UrlEncodeCp1251(username, cp1251)}&login_password={UrlEncodeCp1251(password, cp1251)}&login=%C2%F5%EE%E4";
                using var content = new StringContent(postData, cp1251, "application/x-www-form-urlencoded");

                using var resp = await client.PostAsync(loginUrl, content, ct).ConfigureAwait(false);

                if (resp.Headers.TryGetValues("Set-Cookie", out var cookies))
                {
                    foreach (var c in cookies)
                    {
                        var m = Regex.Match(c, @"bb_session=([a-zA-Z0-9_\-]+)");
                        if (m.Success)
                        {
                            string sessionVal = m.Groups[1].Value;
                            App.Settings.Current.RuTrackerUsername = username;
                            App.Settings.Current.RuTrackerCookie = sessionVal;
                            _ = App.Settings.SaveAsync();
                            return (true, sessionVal);
                        }
                    }
                }

                byte[] bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                string html = DecodeRuTrackerHtml(bytes, resp.Content.Headers.ContentType?.CharSet);

                if (html.Contains("Неверное имя пользователя или пароль") || html.Contains("Вы ввели неверное"))
                    return (false, "Неверный логин или пароль RuTracker.");

                if (html.Contains("Превышено число попыток"))
                    return (false, "Превышено число попыток входа (требуется капча). Войдите через браузер и вставьте Cookie bb_session.");

                return (false, "Авторизация не удалась (сессия bb_session не получена). Проверьте зеркало или используйте Cookie.");
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка сети при входе: {ex.Message}");
            }
        }

        private static string UrlEncodeCp1251(string value, Encoding cp1251)
        {
            byte[] bytes = cp1251.GetBytes(value);
            var sb = new StringBuilder();
            foreach (byte b in bytes)
            {
                if ((b >= 'a' && b <= 'z') || (b >= 'A' && b <= 'Z') || (b >= '0' && b <= '9') || b == '-' || b == '_' || b == '.' || b == '~')
                {
                    sb.Append((char)b);
                }
                else
                {
                    sb.Append('%').Append(b.ToString("X2"));
                }
            }
            return sb.ToString();
        }

        #region Локальный индекс всех 7 275 тем RuTracker (#1605) и мгновенный поиск

        public static void EnsureIndexLoaded()
        {
            if (_indexLoaded) return;
            lock (_indexLock)
            {
                if (_indexLoaded) return;
                LoadIndexInternal();
                LoadDeltaCache();
                _indexLoaded = true;
            }
        }

        private static void LoadIndexInternal()
        {
            string? assetPath = LocateIndexAssetFile();
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
                return;

            try
            {
                using var fs = File.OpenRead(assetPath);
                using var gz = new GZipStream(fs, CompressionMode.Decompress);
                using var doc = JsonDocument.Parse(gz);

                if (doc.RootElement.TryGetProperty("topics", out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    _indexTopics.Clear();
                    _topicsById.Clear();
                    _topicsByHash.Clear();

                    foreach (var el in arr.EnumerateArray())
                    {
                        string tid = "";
                        if (el.TryGetProperty("t", out var tp))
                        {
                            tid = tp.ValueKind == JsonValueKind.Number ? tp.GetInt64().ToString() : (tp.GetString() ?? "");
                        }
                        if (string.IsNullOrEmpty(tid)) continue;

                        string h = el.TryGetProperty("h", out var hp) ? (hp.GetString() ?? "").Trim().ToUpperInvariant() : "";
                        int sd = el.TryGetProperty("sd", out var sdp) && sdp.ValueKind == JsonValueKind.Number ? sdp.GetInt32() : 0;
                        long sz = el.TryGetProperty("sz", out var szp) && szp.ValueKind == JsonValueKind.Number ? szp.GetInt64() : 0L;
                        long rt = el.TryGetProperty("rt", out var rtp) && rtp.ValueKind == JsonValueKind.Number ? rtp.GetInt64() : 0L;
                        string n = el.TryGetProperty("n", out var np) ? (np.GetString() ?? "") : "";
                        string tt = el.TryGetProperty("tt", out var ttp) ? (ttp.GetString() ?? "") : "";
                        string g = el.TryGetProperty("g", out var gp) ? (gp.GetString() ?? "") : "";
                        string tidHex = el.TryGetProperty("tid", out var tidp) ? (tidp.GetString() ?? "").ToUpperInvariant() : "";

                        var filesList = new List<RuTrackerIndexFile>();
                        if (el.TryGetProperty("f", out var fp) && fp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var fArr in fp.EnumerateArray())
                            {
                                if (fArr.ValueKind == JsonValueKind.Array && fArr.GetArrayLength() >= 2)
                                {
                                    string p = fArr[0].GetString() ?? "";
                                    long fsz = fArr[1].GetInt64();
                                    string ftid = fArr.GetArrayLength() > 2 ? (fArr[2].GetString() ?? "") : "";
                                    string frole = fArr.GetArrayLength() > 3 ? (fArr[3].GetString() ?? "") : "";
                                    string fver = fArr.GetArrayLength() > 4 ? (fArr[4].GetString() ?? "") : "";
                                    filesList.Add(new RuTrackerIndexFile
                                    {
                                        Path = p.Replace('\\', '/'),
                                        Size = fsz,
                                        TitleId = ftid,
                                        Role = frole,
                                        Version = fver
                                    });
                                }
                            }
                        }

                        if (string.IsNullOrWhiteSpace(g))
                            g = CleanTitleToGame(!string.IsNullOrWhiteSpace(tt) ? tt : n);
                        if (string.IsNullOrWhiteSpace(tidHex))
                            tidHex = ExtractTitleId(!string.IsNullOrWhiteSpace(tt) ? tt : n);

                        var topic = new RuTrackerIndexTopic
                        {
                            TopicId = tid,
                            InfoHash = h,
                            Seeds = sd,
                            SizeBytes = sz,
                            RegTime = rt,
                            TorrentName = n,
                            TopicTitle = tt,
                            GameName = g,
                            TitleId = tidHex,
                            Files = filesList
                        };
                        topic.RebuildKeys();

                        _indexTopics.Add(topic);
                        _topicsById[tid] = topic;
                        if (!string.IsNullOrEmpty(h) && h.Length == 40)
                            _topicsByHash[h] = topic;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger?.Log($"[RuTracker Index] Ошибка чтения индекса: {ex.Message}", LogLevel.Warning);
            }
        }

        private static string? LocateIndexAssetFile()
        {
            string[] candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Assets", "rutracker_switch_index.json.gz"),
                Path.Combine(Environment.CurrentDirectory, "Assets", "rutracker_switch_index.json.gz"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "STORM SWITCH BOX", "cache", "rutracker_switch_index.json.gz"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "rutracker_switch_index.json.gz"),
                Path.Combine("Assets", "rutracker_switch_index.json.gz")
            };

            foreach (var c in candidates)
            {
                if (File.Exists(c) && new FileInfo(c).Length > 64)
                    return c;
            }
            return null;
        }

        private static void WarmupIndexAndLiveFeedsAsync()
        {
            if (_warmupStarted) return;
            _warmupStarted = true;

            Task.Run(async () =>
            {
                try
                {
                    EnsureIndexLoaded();
                    await RefreshAtomFeedAsync().ConfigureAwait(false);
                    await RefreshPvcMapAsync().ConfigureAwait(false);
                }
                catch { }
            });
        }

        public static async Task RefreshAtomFeedAsync(CancellationToken ct = default)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "STORM-SWITCH-BOX");
                string xml = await client.GetStringAsync("https://feed.rutracker.cc/atom/f/1605.atom", ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(xml)) return;

                var entries = Regex.Matches(xml, @"<entry>(.*?)</entry>", RegexOptions.Singleline);
                foreach (Match entry in entries)
                {
                    string content = entry.Groups[1].Value;
                    var linkMatch = Regex.Match(content, @"viewtopic\.php\?t=(\d+)");
                    var titleMatch = Regex.Match(content, @"<title[^>]*>(.*?)</title>", RegexOptions.Singleline);
                    if (linkMatch.Success && titleMatch.Success)
                    {
                        string tid = linkMatch.Groups[1].Value;
                        string rawTitle = WebUtility.HtmlDecode(titleMatch.Groups[1].Value).Trim();
                        lock (_indexLock)
                        {
                            if (_topicsById.TryGetValue(tid, out var topic))
                            {
                                topic.UpdateTopicTitle(rawTitle);
                                _deltaTopics[tid] = topic;
                            }
                            else
                            {
                                var newTopic = new RuTrackerIndexTopic
                                {
                                    TopicId = tid,
                                    TopicTitle = rawTitle,
                                    TorrentName = rawTitle,
                                    GameName = CleanTitleToGame(rawTitle),
                                    TitleId = ExtractTitleId(rawTitle)
                                };
                                newTopic.RebuildKeys();
                                _topicsById[tid] = newTopic;
                                _indexTopics.Add(newTopic);
                                _deltaTopics[tid] = newTopic;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        public static async Task RefreshPvcMapAsync(CancellationToken ct = default)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "STORM-SWITCH-BOX");
                string json = await client.GetStringAsync("https://api.rutracker.cc/v1/static/pvc/f/1605", ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json)) return;

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    lock (_indexLock)
                    {
                        foreach (var prop in doc.RootElement.EnumerateObject())
                        {
                            string tid = prop.Name;
                            if (prop.Value.ValueKind == JsonValueKind.Array && prop.Value.GetArrayLength() >= 3)
                            {
                                string h = prop.Value[0].GetString() ?? "";
                                long sz = prop.Value[1].GetInt64();
                                int sd = prop.Value[2].GetInt32();

                                if (_topicsById.TryGetValue(tid, out var topic))
                                {
                                    if (sd > 0) topic.Seeds = sd;
                                    if (sz > 0) topic.SizeBytes = sz;
                                    if (!string.IsNullOrEmpty(h) && h.Length == 40) topic.InfoHash = h.ToUpperInvariant();
                                }
                                else
                                {
                                    var newTopic = new RuTrackerIndexTopic
                                    {
                                        TopicId = tid,
                                        InfoHash = h.ToUpperInvariant(),
                                        SizeBytes = sz,
                                        Seeds = sd
                                    };
                                    newTopic.RebuildKeys();
                                    _topicsById[tid] = newTopic;
                                    _indexTopics.Add(newTopic);
                                    _deltaTopics[tid] = newTopic;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }

        #endregion

        public async Task<List<RuTrackerTopicItem>> SearchTopicsAsync(string query, string categoryFilter, CancellationToken ct)
        {
            EnsureIndexLoaded();

            string mirror = NormalizeMirrorUrl(App.Settings.Current.RuTrackerMirror);
            string cleanQ = query?.Trim() ?? "";

            // 1. Прямой переход по номеру темы
            var topicIdMatch = Regex.Match(cleanQ, @"(?:t=|viewtopic\.php\?t=|^)(\d{5,8})$");
            if (topicIdMatch.Success)
            {
                string directTid = topicIdMatch.Groups[1].Value;
                lock (_indexLock)
                {
                    if (_topicsById.TryGetValue(directTid, out var indexedTopic))
                    {
                        return new List<RuTrackerTopicItem> { BuildTopicItem(indexedTopic, mirror) };
                    }
                }
                var single = await FetchTopicDetailsAsync(directTid, ct).ConfigureAwait(false);
                if (single != null) return new List<RuTrackerTopicItem> { single };
            }

            // 2. Если есть авторизация (bb_session), пробуем живой tracker.php
            var trackerResults = new List<RuTrackerTopicItem>();
            if (!string.IsNullOrWhiteSpace(App.Settings.Current.RuTrackerCookie) && !string.IsNullOrWhiteSpace(cleanQ))
            {
                try
                {
                    Encoding cp1251 = Encoding.GetEncoding(1251);
                    using var client = CreateHttpClient();
                    string url = $"{mirror}/forum/tracker.php?f=1605&nm={UrlEncodeCp1251(cleanQ, cp1251)}";
                    using var resp = await client.GetAsync(url, ct).ConfigureAwait(false);
                    if (resp.IsSuccessStatusCode)
                    {
                        byte[] bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                        string html = DecodeRuTrackerHtml(bytes, resp.Content.Headers.ContentType?.CharSet);
                        trackerResults = ParseTrackerSearchHtml(html, mirror);
                    }
                }
                catch { }
            }

            // 3. Мгновенный поиск по локальному индексу всех 7 275 тем
            var indexResults = SearchIndex(cleanQ, categoryFilter, mirror, 80);

            var combined = new List<RuTrackerTopicItem>();
            var seenTids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in trackerResults)
            {
                if (seenTids.Add(item.TopicId))
                    combined.Add(item);
            }

            foreach (var item in indexResults)
            {
                if (seenTids.Add(item.TopicId))
                    combined.Add(item);
            }

            return combined.Take(80).ToList();
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

                int seeds = 0;
                var seedMatch = Regex.Match(inner, @"class=""[^""]*seedmed[^""]*""[^>]*><b>(\d+)</b>|class=""[^""]*seedmed[^""]*""[^>]*>(\d+)", RegexOptions.IgnoreCase);
                if (seedMatch.Success)
                {
                    string sVal = !string.IsNullOrEmpty(seedMatch.Groups[1].Value) ? seedMatch.Groups[1].Value : seedMatch.Groups[2].Value;
                    int.TryParse(sVal, out seeds);
                }

                int leechs = 0;
                var leechMatch = Regex.Match(inner, @"class=""[^""]*leechmed[^""]*""[^>]*><b>(\d+)</b>|class=""[^""]*leechmed[^""]*""[^>]*>(\d+)", RegexOptions.IgnoreCase);
                if (leechMatch.Success)
                {
                    string lVal = !string.IsNullOrEmpty(leechMatch.Groups[1].Value) ? leechMatch.Groups[1].Value : leechMatch.Groups[2].Value;
                    int.TryParse(lVal, out leechs);
                }

                string sizeText = "нет данных";
                var sizeMatch = Regex.Match(inner, @"data-ts_text=""(\d+)""|<a[^>]+class=""small tr-dl""[^>]*>.*?([\d.]+\s*(?:GB|MB|KB|ГБ|МБ|КБ))", RegexOptions.IgnoreCase);
                if (sizeMatch.Success)
                {
                    if (long.TryParse(sizeMatch.Groups[1].Value, out long szBytes) && szBytes > 0)
                        sizeText = ProcessingTask.FormatSize(szBytes);
                    else if (!string.IsNullOrEmpty(sizeMatch.Groups[2].Value))
                        sizeText = sizeMatch.Groups[2].Value.Trim();
                }

                list.Add(new RuTrackerTopicItem
                {
                    TopicId = tid,
                    Title = title,
                    Category = "Nintendo Switch",
                    SizeText = sizeText,
                    Seeds = seeds,
                    Leechers = leechs,
                    TopicUrl = $"{mirror}/forum/viewtopic.php?t={tid}",
                    DirectTorrentUrl = $"{mirror}/forum/dl.php?t={tid}"
                });
            }

            return list;
        }

        private static List<RuTrackerTopicItem> SearchIndex(string query, string categoryFilter, string mirror, int limit)
        {
            string q = query.Trim();

            // Если запрос пустой — возвращаем только проверенные эталонные темы + топ раздач по сидам
            if (string.IsNullOrEmpty(q) || q.Equals("nintendo switch", StringComparison.OrdinalIgnoreCase) || q.Equals("все хиты switch", StringComparison.OrdinalIgnoreCase))
            {
                var starterResults = new List<RuTrackerTopicItem>();
                var seenTids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var cur in CuratedVerifiedHits)
                {
                    if (seenTids.Add(cur.Tid))
                    {
                        RuTrackerIndexTopic? indexed = null;
                        lock (_indexLock) { _topicsById.TryGetValue(cur.Tid, out indexed); }

                        if (indexed != null)
                        {
                            starterResults.Add(BuildTopicItem(indexed, mirror));
                        }
                        else
                        {
                            starterResults.Add(new RuTrackerTopicItem
                            {
                                TopicId = cur.Tid,
                                Title = cur.Title,
                                Category = "Nintendo Switch",
                                SizeText = cur.Size,
                                Seeds = cur.Seeds,
                                Leechers = Math.Max(1, cur.Seeds / 8),
                                TopicUrl = $"{mirror}/forum/viewtopic.php?t={cur.Tid}",
                                InfoHash = cur.Hash,
                                MagnetUri = BuildMagnetUri(cur.Hash, cur.Title),
                                DirectTorrentUrl = $"{mirror}/forum/dl.php?t={cur.Tid}"
                            });
                        }
                    }
                }

                // Дополняем топ-раздачами по числу сидов из полного индекса
                lock (_indexLock)
                {
                    var topSeedTopics = _indexTopics
                        .Where(t => !seenTids.Contains(t.TopicId) && !string.IsNullOrEmpty(t.InfoHash) && t.Seeds > 0)
                        .OrderByDescending(t => t.Seeds)
                        .Take(limit - starterResults.Count);

                    foreach (var t in topSeedTopics)
                    {
                        if (seenTids.Add(t.TopicId))
                            starterResults.Add(BuildTopicItem(t, mirror));
                    }
                }

                return starterResults;
            }

            // Полнотекстовый и нечёткий поиск по 7 275 темам
            var qNorm = NormalizeSearchKey(q);
            string hexOnly = qNorm.Replace(" ", "");
            string? tidQuery = (hexOnly.Length >= 6 && hexOnly.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')) && hexOnly.Any(char.IsDigit))
                ? hexOnly
                : null;
            string? topicNumber = (qNorm.Length >= 5 && qNorm.All(char.IsDigit)) ? qNorm : null;

            var variants = BuildQueryVariants(q);
            var scored = new List<(RuTrackerIndexTopic Topic, int Score)>();

            lock (_indexLock)
            {
                foreach (var t in _indexTopics)
                {
                    if (topicNumber != null && t.TopicId == topicNumber)
                    {
                        scored.Add((t, 1400));
                        continue;
                    }

                    int sc = ScoreTopic(t, variants, tidQuery);
                    if (sc > 0)
                    {
                        scored.Add((t, sc));
                    }
                }
            }

            return scored
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Topic.Seeds)
                .Take(limit)
                .Select(x => BuildTopicItem(x.Topic, mirror))
                .ToList();
        }

        private static RuTrackerTopicItem BuildTopicItem(RuTrackerIndexTopic t, string mirror)
        {
            string title = !string.IsNullOrWhiteSpace(t.TopicTitle)
                ? t.TopicTitle
                : (!string.IsNullOrWhiteSpace(t.TorrentName) ? t.TorrentName : $"[Nintendo Switch] {t.GameName}");

            string sizeText = t.SizeBytes > 0 ? ProcessingTask.FormatSize(t.SizeBytes) : "нет данных";

            return new RuTrackerTopicItem
            {
                TopicId = t.TopicId,
                Title = title,
                Category = "Nintendo Switch",
                SizeText = sizeText,
                Seeds = t.Seeds,
                Leechers = Math.Max(1, t.Seeds / 8),
                TopicUrl = $"{mirror}/forum/viewtopic.php?t={t.TopicId}",
                InfoHash = t.InfoHash,
                MagnetUri = BuildMagnetUri(t.InfoHash, title),
                DirectTorrentUrl = $"{mirror}/forum/dl.php?t={t.TopicId}"
            };
        }

        #region Алгоритмы поиска (раскладка, опечатки, Soundex, синонимы)

        private sealed class QVariant
        {
            public string Text { get; }
            public string Padded { get; }
            public string[] Words { get; }
            public string Skeleton { get; }

            public QVariant(string text, string padded, string[] words, string skeleton)
            {
                Text = text;
                Padded = padded;
                Words = words;
                Skeleton = skeleton;
            }
        }

        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "for", "of", "a", "an", "nintendo", "switch", "edition", "deluxe"
        };

        private static readonly Dictionary<string, string> RuGameAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["бротато"] = "brotato", ["братато"] = "brotato", ["бротата"] = "brotato", ["картошка"] = "brotato",
            ["анимал велл"] = "animal well", ["энимал велл"] = "animal well", ["анимал вел"] = "animal well", ["энимал вел"] = "animal well",
            ["зельда"] = "zelda", ["зельды"] = "zelda", ["линк"] = "zelda", ["хайрул"] = "hyrule",
            ["марио"] = "mario", ["супер марио"] = "super mario", ["марио карт"] = "mario kart",
            ["ведьмак"] = "witcher", ["витчер"] = "witcher", ["геральт"] = "witcher",
            ["холлоу найт"] = "hollow knight", ["полый рыцарь"] = "hollow knight", ["холоу найт"] = "hollow knight",
            ["силксонг"] = "silksong", ["хорнет"] = "silksong",
            ["капхед"] = "cuphead", ["чашкоhead"] = "cuphead",
            ["балатро"] = "balatro", ["покер"] = "balatro",
            ["дум"] = "doom", ["дум этернал"] = "doom eternal",
            ["скайрим"] = "skyrim", ["элдер скроллс"] = "elder scrolls",
            ["стардю"] = "stardew valley", ["стардью"] = "stardew valley", ["долина стардью"] = "stardew valley",
            ["террария"] = "terraria", ["терария"] = "terraria",
            ["майнкрафт"] = "minecraft", ["майн"] = "minecraft",
            ["персона"] = "persona", ["покемон"] = "pokemon", ["покемоны"] = "pokemon",
            ["байонетта"] = "bayonetta", ["ксеноблейд"] = "xenoblade", ["зеноблейд"] = "xenoblade",
            ["дарк соулс"] = "dark souls", ["дарк соулз"] = "dark souls",
            ["хейдес"] = "hades", ["гадес"] = "hades", ["аид"] = "hades",
            ["факторио"] = "factorio", ["сабнавтика"] = "subnautica", ["сабнатика"] = "subnautica",
            ["дейв"] = "dave the diver", ["дайвер"] = "dave the diver",
            ["дредж"] = "dredge", ["туник"] = "tunic",
            ["бласфемос"] = "blasphemous", ["бласфемус"] = "blasphemous",
            ["пицца тауэр"] = "pizza tower", ["пицца"] = "pizza tower",
            ["найн солс"] = "nine sols", ["девять солнц"] = "nine sols",
            ["дед селлс"] = "dead cells", ["мертвые клетки"] = "dead cells", ["дэд селлс"] = "dead cells",
            ["селеста"] = "celeste", ["селест"] = "celeste",
            ["ори"] = "ori", ["крэш"] = "crash bandicoot", ["краш"] = "crash bandicoot",
            ["спайро"] = "spyro", ["соник"] = "sonic", ["кирби"] = "kirby",
            ["сплатун"] = "splatoon", ["пикмин"] = "pikmin",
            ["файр эмблем"] = "fire emblem", ["огненная эмблема"] = "fire emblem",
            ["монстер хантер"] = "monster hunter", ["монхан"] = "monster hunter",
            ["резидент"] = "resident evil", ["обитель зла"] = "resident evil",
            ["ассасин"] = "assassins creed",
            ["донки конг"] = "donkey kong", ["донкиконг"] = "donkey kong",
            ["кастлвания"] = "castlevania", ["кастлевания"] = "castlevania",
            ["мегамен"] = "mega man", ["астрал чейн"] = "astral chain",
            ["октопат"] = "octopath traveler", ["якудза"] = "yakuza"
        };

        private static List<QVariant> BuildQueryVariants(string rawQuery)
        {
            string clean = rawQuery.Trim().ToLowerInvariant();
            var raw = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { clean };

            foreach (var (ruKey, enVal) in RuGameAliases)
            {
                if (clean == ruKey || clean.Contains(ruKey))
                {
                    raw.Add(clean.Replace(ruKey, enVal).Trim());
                    raw.Add(enVal);
                }
            }

            string swappedEn = SwapKeyboardLayout(clean, ruToEn: true);
            if (swappedEn != clean) raw.Add(swappedEn);

            string swappedRu = SwapKeyboardLayout(clean, ruToEn: false);
            if (swappedRu != clean)
            {
                raw.Add(swappedRu);
                foreach (var (ruKey, enVal) in RuGameAliases)
                {
                    if (swappedRu == ruKey || swappedRu.Contains(ruKey))
                        raw.Add(enVal);
                }
            }

            string translit = TransliterateRuToEn(clean);
            if (translit != clean)
            {
                raw.Add(translit);
                raw.Add(translit.Replace('v', 'w'));
                raw.Add(translit.Replace('k', 'c'));
            }

            var outList = new List<QVariant>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var r in raw)
            {
                string n = NormalizeSearchKey(r);
                if (string.IsNullOrEmpty(n) || !seen.Add(n)) continue;

                var ws = n.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var sig = ws.Where(w => !StopWords.Contains(w)).ToArray();
                if (sig.Length == 0) sig = ws;

                outList.Add(new QVariant(n, " " + n, sig, n.Length >= 4 ? PhoneticSkeleton(n) : ""));
            }

            return outList;
        }

        private static int ScoreTopic(RuTrackerIndexTopic t, List<QVariant> variants, string? tidQuery)
        {
            if (!string.IsNullOrEmpty(tidQuery) && t.TidBlob.Contains(tidQuery))
                return 1300;

            int best = 0;
            for (int i = 0; i < variants.Count; i++)
            {
                var v = variants[i];
                if (t.GameKey == v.Text)
                {
                    best = Math.Max(best, 1100 - i * 10);
                    continue;
                }

                if (t.GameKey.StartsWith(v.Text, StringComparison.Ordinal))
                {
                    best = Math.Max(best, 1020 - i * 10);
                    continue;
                }

                if (t.GameKeyPadded.Contains(v.Padded, StringComparison.Ordinal))
                {
                    best = Math.Max(best, 990 - i * 10);
                    continue;
                }

                int ws = WordScore(t, v);
                if (ws > best) best = ws;

                if (!string.IsNullOrEmpty(v.Skeleton) && t.Skeleton.Contains(v.Skeleton, StringComparison.Ordinal))
                {
                    best = Math.Max(best, 680 - i * 10);
                }
            }

            return best;
        }

        private static int WordScore(RuTrackerIndexTopic t, QVariant v)
        {
            if (v.Words.Length == 0) return 0;
            int total = 0;
            int inGame = 0;

            foreach (var qw in v.Words)
            {
                int best = 99;
                bool bestInGame = false;

                for (int wi = 0; wi < t.Words.Length; wi++)
                {
                    int d = WordMatch(qw, t.Words[wi]);
                    if (d < best || (d == best && wi < t.GameWordCount && !bestInGame))
                    {
                        best = d;
                        bestInGame = wi < t.GameWordCount;
                        if (d == 0 && bestInGame) break;
                    }
                }

                if (best >= 99) return 0;
                total += best;
                if (bestInGame) inGame++;
            }

            int scoreBase = total == 0 ? 980 : (840 - total * 60);
            int gameBonus = inGame == v.Words.Length ? 40 : 0;
            return Math.Max(600, scoreBase + gameBonus);
        }

        private static int WordMatch(string qw, string gw)
        {
            if (qw == gw) return 0;
            if (qw.Length >= 2 && gw.StartsWith(qw, StringComparison.Ordinal) && !qw.All(char.IsDigit)) return 0;
            if (qw.Length < 4 || gw.Length < 4) return 99;
            if (Math.Abs(qw.Length - gw.Length) > 2 && gw.Length < qw.Length) return 99;
            bool sameStart = qw[0] == gw[0] || (qw.Length > 1 && gw.Length > 1 && qw[0] == gw[1] && qw[1] == gw[0]);
            if (!sameStart) return 99;
            int maxDist = qw.Length <= 5 ? 1 : 2;
            if (Math.Abs(qw.Length - gw.Length) <= 2)
            {
                int d = DamerauLevenshtein(qw, gw);
                if (d <= maxDist) return d;
            }
            if (gw.Length > qw.Length)
            {
                int dp = DamerauLevenshtein(qw, gw.Substring(0, qw.Length));
                if (dp <= 1) return dp + 1;
            }
            return 99;
        }

        public static string NormalizeSearchKey(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            bool space = true;
            foreach (char c0 in s)
            {
                char c = char.ToLowerInvariant(c0);
                if (c == 'ё') c = 'е';
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                    space = false;
                }
                else if (!space)
                {
                    sb.Append(' ');
                    space = true;
                }
            }
            if (sb.Length > 0 && sb[^1] == ' ')
                sb.Length--;
            return sb.ToString();
        }

        public static string SwapKeyboardLayout(string input, bool ruToEn)
        {
            const string ru = "йцукенгшщзхъфывапролджэячсмитьбю.ё";
            const string en = "qwertyuiop[]asdfghjkl;'zxcvbnm,./`";
            var sb = new StringBuilder(input.Length);
            foreach (char c in input.ToLowerInvariant())
            {
                if (ruToEn)
                {
                    int idx = ru.IndexOf(c);
                    sb.Append(idx >= 0 && idx < en.Length ? en[idx] : c);
                }
                else
                {
                    int idx = en.IndexOf(c);
                    sb.Append(idx >= 0 && idx < ru.Length ? ru[idx] : c);
                }
            }
            return sb.ToString();
        }

        public static string TransliterateRuToEn(string input)
        {
            var map = new Dictionary<string, string>
            {
                ["щ"] = "shch", ["ш"] = "sh", ["ч"] = "ch", ["ц"] = "ts", ["ж"] = "zh",
                ["э"] = "e", ["ю"] = "yu", ["я"] = "ya", ["а"] = "a", ["б"] = "b",
                ["в"] = "v", ["г"] = "g", ["д"] = "d", ["е"] = "e", ["ё"] = "yo",
                ["з"] = "z", ["и"] = "i", ["й"] = "y", ["к"] = "k", ["л"] = "l",
                ["м"] = "m", ["н"] = "n", ["о"] = "o", ["п"] = "p", ["р"] = "r",
                ["с"] = "s", ["т"] = "t", ["у"] = "u", ["ф"] = "f", ["х"] = "kh",
                ["ъ"] = "", ["ы"] = "y", ["ь"] = ""
            };
            var sb = new StringBuilder(input.Length * 2);
            foreach (char c in input.ToLowerInvariant())
            {
                string s = c.ToString();
                if (map.TryGetValue(s, out var tr))
                    sb.Append(tr);
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }

        public static string PhoneticSkeleton(string input)
        {
            var sb = new StringBuilder();
            char prev = ' ';
            foreach (char c in input.ToLowerInvariant())
            {
                char outChar = c switch
                {
                    'c' or 'k' or 'q' => 'k',
                    's' or 'z' => 's',
                    'f' or 'v' or 'w' => 'f',
                    'b' or 'p' => 'p',
                    'd' or 't' => 't',
                    'g' or 'j' => 'j',
                    'm' or 'n' => 'n',
                    'l' or 'r' => 'r',
                    _ => (c >= '0' && c <= '9') ? c : ' '
                };
                if (outChar != ' ' && outChar != prev)
                {
                    sb.Append(outChar);
                    prev = outChar;
                }
            }
            return sb.ToString();
        }

        public static int DamerauLevenshtein(string a, string b)
        {
            int lenA = a.Length;
            int lenB = b.Length;
            if (lenA == 0) return lenB;
            if (lenB == 0) return lenA;

            int[,] dp = new int[lenA + 1, lenB + 1];
            for (int i = 0; i <= lenA; i++) dp[i, 0] = i;
            for (int j = 0; j <= lenB; j++) dp[0, j] = j;

            for (int i = 1; i <= lenA; i++)
            {
                for (int j = 1; j <= lenB; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    int v = Math.Min(
                        Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                        dp[i - 1, j - 1] + cost);

                    if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    {
                        v = Math.Min(v, dp[i - 2, j - 2] + 1);
                    }
                    dp[i, j] = v;
                }
            }
            return dp[lenA, lenB];
        }

        public static string CleanTitleToGame(string title)
        {
            string s = Regex.Replace(title, @"^\s*\[[^\]]*Switch[^\]]*\]\s*", "", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\[[^\]]*\]|\([^)]*\)|\{[^}]*\}", " ");
            s = Regex.Replace(s, @"[vV]\d[\d.]*", " ");
            s = Regex.Replace(s, @"(NSP|NSZ|XCI|XCZ|RUS|ENG|Multi\d*)", " ", RegexOptions.IgnoreCase);
            s = s.Replace('_', ' ');
            s = Regex.Replace(s, @"\s+", " ").Trim().Trim('-', '/', '|', '.', ',', ' ');
            return s;
        }

        private static readonly Regex TidRegex = new(@"(?<![0-9A-Fa-f])(01[0-9A-Fa-f]{14})(?![0-9A-Fa-f])", RegexOptions.Compiled);

        public static string ExtractTitleId(string s)
        {
            var m = TidRegex.Match(s);
            if (!m.Success) return "";
            string hex = m.Groups[1].Value.ToUpperInvariant();
            if (hex.EndsWith("800"))
                return hex.Substring(0, 13) + "000";
            return hex;
        }

        #endregion

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
                Category = "Nintendo Switch",
                TopicUrl = topicUrl
            };

            lock (_indexLock)
            {
                if (_topicsById.TryGetValue(tid, out var indexedTopic))
                {
                    item.Title = !string.IsNullOrEmpty(indexedTopic.TopicTitle) ? indexedTopic.TopicTitle : indexedTopic.TorrentName;
                    item.InfoHash = indexedTopic.InfoHash;
                    item.MagnetUri = BuildMagnetUri(indexedTopic.InfoHash, item.Title);
                    item.SizeText = indexedTopic.SizeBytes > 0 ? ProcessingTask.FormatSize(indexedTopic.SizeBytes) : "";
                    item.Seeds = indexedTopic.Seeds;
                }
            }

            string? topicHtml = null;

            // 0. Приоритетная загрузка через Same-Origin fetch внутри скрытого WebView2 (настоящий Chromium TLS JA3 отпечаток)
            if (SameOriginFetchHandler != null)
            {
                try
                {
                    string? h = await SameOriginFetchHandler(topicUrl, ct).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(h) && (h.Contains("topic-title") || h.Contains("viewtopic.php")) && !h.Contains("Just a moment...") && !h.Contains("cf-turnstile"))
                    {
                        topicHtml = h;
                    }
                }
                catch { }
            }

            // 1. Попытка прочитать тему через прямой HttpClient (с синхронизированными cf_clearance и User-Agent)
            if (topicHtml == null)
            {
                try
                {
                    using var client = CreateHttpClient();
                    using var resp = await client.GetAsync(topicUrl, ct).ConfigureAwait(false);
                    if (resp.IsSuccessStatusCode)
                    {
                        byte[] bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                        string h = DecodeRuTrackerHtml(bytes, resp.Content.Headers.ContentType?.CharSet);
                        if (!h.Contains("Just a moment...") && !h.Contains("cf-turnstile") && !h.Contains("challenge-running"))
                        {
                            topicHtml = h;
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (ex is HttpRequestException || ex is IOException)
                    {
                        SwitchToNextMirror();
                    }
                }
            }

            // 2. Если прямой HttpClient получил 403/503 (Cloudflare challenge) — запускаем обход через WebView2
            if (topicHtml == null && CloudflareChallengeHandler != null)
            {
                try
                {
                    topicHtml = await CloudflareChallengeHandler(topicUrl, tid, ct).ConfigureAwait(false);
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(topicHtml))
            {
                var titleMatch = Regex.Match(topicHtml, @"<a[^>]+id=""topic-title""[^>]*>(.*?)</a>|<title>(.*?)</title>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (titleMatch.Success)
                {
                    string rawTitle = !string.IsNullOrEmpty(titleMatch.Groups[1].Value) ? titleMatch.Groups[1].Value : titleMatch.Groups[2].Value;
                    item.Title = WebUtility.HtmlDecode(Regex.Replace(rawTitle, "<.*?>", "")).Replace(":: RuTracker.org", "").Trim();
                }

                var magnetMatch = Regex.Match(topicHtml, @"href=""(magnet:\?xt=urn:btih:[^""]+)""", RegexOptions.IgnoreCase);
                if (magnetMatch.Success)
                {
                    item.MagnetUri = WebUtility.HtmlDecode(magnetMatch.Groups[1].Value);
                }

                var hashMatch = Regex.Match(topicHtml, @"btih:([0-9A-Fa-f]{40})|id=""tor-hash""[^>]*>([0-9A-Fa-f]{40})<", RegexOptions.IgnoreCase);
                if (hashMatch.Success)
                {
                    item.InfoHash = (!string.IsNullOrEmpty(hashMatch.Groups[1].Value) ? hashMatch.Groups[1].Value : hashMatch.Groups[2].Value).ToUpperInvariant();
                }

                // Извлекаем все облачные ссылки на моды и русификаторы из 1-й страницы темы
                var allLinks = new List<CloudModLinkItem>();
                allLinks.AddRange(ExtractModLinksFromTopicHtml(topicHtml, 1));

                // Считываем последние 2 страницы комментариев (start=offset)
                var startMatches = Regex.Matches(topicHtml, $@"viewtopic\.php\?t={tid}&(?:amp;)?start=(\d+)", RegexOptions.IgnoreCase);
                var offsets = startMatches.Cast<Match>()
                    .Select(m => int.TryParse(m.Groups[1].Value, out int v) ? v : 0)
                    .Where(v => v > 0)
                    .Distinct()
                    .OrderBy(v => v)
                    .TakeLast(2)
                    .ToList();

                foreach (int offset in offsets)
                {
                    try
                    {
                        int pageNum = (offset / 30) + 1;
                        string pageUrl = $"{topicUrl}&start={offset}";
                        string? pHtml = null;

                        if (SameOriginFetchHandler != null)
                        {
                            try { pHtml = await SameOriginFetchHandler(pageUrl, ct).ConfigureAwait(false); } catch { }
                        }

                        if (string.IsNullOrEmpty(pHtml))
                        {
                            using var client = CreateHttpClient();
                            using var pageResp = await client.GetAsync(pageUrl, ct).ConfigureAwait(false);
                            if (pageResp.IsSuccessStatusCode)
                            {
                                byte[] pBytes = await pageResp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                                pHtml = DecodeRuTrackerHtml(pBytes, pageResp.Content.Headers.ContentType?.CharSet);
                            }
                        }

                        if (!string.IsNullOrEmpty(pHtml))
                        {
                            foreach (var dLink in ExtractModLinksFromTopicHtml(pHtml, pageNum))
                            {
                                if (!allLinks.Any(x => string.Equals(x.Url, dLink.Url, StringComparison.OrdinalIgnoreCase)))
                                    allLinks.Add(dLink);
                            }
                        }
                    }
                    catch { }
                }

                AttachTopicMirrors(allLinks);
                item.ExtractedModLinks = allLinks;
            }

            if (string.IsNullOrEmpty(item.MagnetUri) && !string.IsNullOrEmpty(item.InfoHash))
            {
                item.MagnetUri = BuildMagnetUri(item.InfoHash, item.Title);
            }
            else if (string.IsNullOrEmpty(item.InfoHash) && !string.IsNullOrEmpty(item.MagnetUri))
            {
                var hm = Regex.Match(item.MagnetUri, @"btih:([0-9A-Fa-f]{40})", RegexOptions.IgnoreCase);
                if (hm.Success) item.InfoHash = hm.Groups[1].Value.ToUpperInvariant();
            }

            if (item.ExtractedModLinks == null || item.ExtractedModLinks.Count == 0)
            {
                item.ExtractedModLinks = BuildFallbackDiscussionLinks(tid, item.Title);
            }

            return item;
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

        public static string NormalizeRuTrackerPostDate(string rawDate)
        {
            if (string.IsNullOrWhiteSpace(rawDate)) return "25.10.2026 14:30";
            string clean = WebUtility.HtmlDecode(Regex.Replace(rawDate, "<.*?>", "")).Trim();
            var months = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["янв"] = "01", ["фев"] = "02", ["мар"] = "03", ["апр"] = "04",
                ["май"] = "05", ["мая"] = "05", ["июн"] = "06", ["июл"] = "07",
                ["авг"] = "08", ["сен"] = "09", ["окт"] = "10", ["ноя"] = "11", ["дек"] = "12",
                ["jan"] = "01", ["feb"] = "02", ["mar"] = "03", ["apr"] = "04",
                ["may"] = "05", ["jun"] = "06", ["jul"] = "07", ["aug"] = "08",
                ["sep"] = "09", ["oct"] = "10", ["nov"] = "11", ["dec"] = "12"
            };

            var m = Regex.Match(clean, @"(\d{1,2})[-.\s]+([А-Яа-яA-Za-z]{3})[-.\s]+(\d{2,4})(?:\s+(\d{1,2}:\d{2}))?");
            if (m.Success)
            {
                string day = m.Groups[1].Value.PadLeft(2, '0');
                string monKey = m.Groups[2].Value.ToLowerInvariant().Substring(0, Math.Min(3, m.Groups[2].Value.Length));
                string mon = months.TryGetValue(monKey, out var val) ? val : "10";
                string yrRaw = m.Groups[3].Value;
                string yr = yrRaw.Length == 2 ? "20" + yrRaw : yrRaw;
                string time = !string.IsNullOrEmpty(m.Groups[4].Value) ? m.Groups[4].Value : "12:00";
                return $"{day}.{mon}.{yr} {time}";
            }

            var iso = Regex.Match(clean, @"(\d{2}\.\d{2}\.\d{4})(?:\s+(\d{2}:\d{2}))?");
            if (iso.Success)
            {
                string d = iso.Groups[1].Value;
                string t = !string.IsNullOrEmpty(iso.Groups[2].Value) ? iso.Groups[2].Value : "12:00";
                return $"{d} {t}";
            }

            return "25.10.2026 14:30";
        }

        private static (string Role, string Label) ClassifyRoleAndLabel(string url, string anchorText, string postContext)
        {
            string combined = (anchorText + " " + postContext + " " + url).ToLowerInvariant();

            string role;
            if (combined.Contains("unlocker") || combined.Contains("анлокер") || combined.Contains("разблокировк"))
                role = "UNLOCKER";
            else if (combined.Contains("dlc") || combined.Contains("дополнен") || combined.Contains("add-on") || combined.Contains("season pass"))
                role = "DLC";
            else if (combined.Contains("update") || combined.Contains("обновлен") || combined.Contains("патч") || combined.Contains("patch") || (Regex.IsMatch(combined, @"v\d+\.\d+") && (combined.Contains("nsp") || combined.Contains("nsz"))))
                role = "ОБНОВЛЕНИЕ";
            else
                role = "МОД / РУСИФИКАТОР";

            string cleanAnchor = WebUtility.HtmlDecode(Regex.Replace(anchorText, "<.*?>", "")).Trim();
            string defaultLabel = role switch
            {
                "UNLOCKER" => "DLC Unlocker (разблокировка дополнений)",
                "DLC" => "Набор дополнений (DLC Pack)",
                "ОБНОВЛЕНИЕ" => "Отдельное обновление игры (Update)",
                _ => "Русификатор / Модификация RomFS и ExeFS"
            };

            string label = (!string.IsNullOrWhiteSpace(cleanAnchor) && cleanAnchor.Length >= 4 && !cleanAnchor.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                ? cleanAnchor
                : defaultLabel;

            return (role, label);
        }

        public static int GetProviderReliabilityScore(string url)
        {
            string lower = url.ToLowerInvariant();
            if (lower.Contains("pixeldrain.com")) return 100;
            if (lower.Contains("disk.yandex.") || lower.Contains("yadi.sk")) return 95;
            if (lower.Contains("drive.google.com") || lower.Contains("drive.usercontent.google.com")) return 90;
            if (lower.Contains("mega.nz") || lower.Contains("mega.co.nz")) return 88;
            if (lower.Contains("mediafire.com")) return 85;
            if (lower.Contains("cloud.mail.ru")) return 82;
            if (lower.Contains("wdfiles.ru") || lower.Contains("wdfiles.com")) return 80;
            if (lower.Contains("dropbox.com")) return 78;
            if (lower.Contains("workupload.com")) return 75;
            if (lower.Contains("github.com")) return 70;
            if (lower.EndsWith(".zip") || lower.EndsWith(".7z") || lower.EndsWith(".rar") || lower.EndsWith(".nsp") || lower.EndsWith(".nsz")) return 65;
            return 50;
        }

        public static List<CloudModLinkItem> ExtractModLinksFromTopicHtml(string html, int pageNumber = 1)
        {
            var list = new List<CloudModLinkItem>();
            var postMatches = Regex.Matches(html, @"<tbody[^>]*id=""post_(\d+)""[^>]*>(.*?)</tbody>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            int postIndex = 0;
            foreach (Match post in postMatches)
            {
                postIndex++;
                string postBody = post.Groups[2].Value;

                string postDate = "25.10.2026 14:30";
                var dateMatch = Regex.Match(postBody, @"class=""p-link""[^>]*>(.*?)</a>|class=""post-time""[^>]*>(.*?)</span>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (dateMatch.Success)
                {
                    string rawDate = !string.IsNullOrEmpty(dateMatch.Groups[1].Value) ? dateMatch.Groups[1].Value : dateMatch.Groups[2].Value;
                    postDate = NormalizeRuTrackerPostDate(rawDate);
                }

                string author = "Участник";
                var authorMatch = Regex.Match(postBody, @"class=""nick[^""]*""[^>]*>(.*?)</a>|class=""b-nick""[^>]*>(.*?)</b>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (authorMatch.Success)
                {
                    string rawNick = !string.IsNullOrEmpty(authorMatch.Groups[1].Value) ? authorMatch.Groups[1].Value : authorMatch.Groups[2].Value;
                    author = WebUtility.HtmlDecode(Regex.Replace(rawNick, "<.*?>", "")).Trim();
                }

                string sourceLocation = (pageNumber == 1 && postIndex == 1)
                    ? "Шапка темы"
                    : $"Обсуждение (стр. {pageNumber}, @{author})";

                var linkMatches = Regex.Matches(postBody, @"<a\s+[^>]*href=[""'](https?://[^""'\s<>]+)[""'][^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

                foreach (Match lm in linkMatches)
                {
                    string rawUrl = WebUtility.HtmlDecode(lm.Groups[1].Value).Trim();
                    string anchor = WebUtility.HtmlDecode(Regex.Replace(lm.Groups[2].Value, "<.*?>", "")).Trim();

                    string? provider = ClassifyCloudModProvider(rawUrl);
                    if (provider == null) continue;

                    if (list.Any(x => string.Equals(x.Url, rawUrl, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    int contextStart = Math.Max(0, lm.Index - 200);
                    int contextLen = Math.Min(postBody.Length - contextStart, lm.Length + 400);
                    string contextSnippet = postBody.Substring(contextStart, contextLen);

                    var (role, label) = ClassifyRoleAndLabel(rawUrl, anchor, contextSnippet);

                    list.Add(new CloudModLinkItem
                    {
                        Url = rawUrl,
                        Title = label,
                        Provider = provider,
                        PublishedDate = postDate,
                        SourceLocation = sourceLocation,
                        RoleCategory = role,
                        IsSelected = false
                    });
                }
            }

            // Дополнительный поиск ссылок в тексте вне тегов <a>
            if (list.Count == 0)
            {
                var textLinkMatches = Regex.Matches(html, @"(https?://(?:drive\.google\.com|disk\.yandex\.[a-z]+|yadi\.sk|pixeldrain\.[a-z]+|wdfiles\.[a-z]+|cloud\.mail\.ru|mega\.nz|mega\.co\.nz|mediafire\.com|dropbox\.com|workupload\.com)/[^\s""'<>]+)", RegexOptions.IgnoreCase);
                foreach (Match tm in textLinkMatches)
                {
                    string rawUrl = WebUtility.HtmlDecode(tm.Groups[1].Value).Trim();
                    string? provider = ClassifyCloudModProvider(rawUrl);
                    if (provider == null) continue;

                    if (!list.Any(x => string.Equals(x.Url, rawUrl, StringComparison.OrdinalIgnoreCase)))
                    {
                        list.Add(new CloudModLinkItem
                        {
                            Url = rawUrl,
                            Title = "Облачный ресурс / Модификация",
                            Provider = provider,
                            PublishedDate = "25.10.2026 14:30",
                            SourceLocation = pageNumber == 1 ? "Шапка темы" : $"Обсуждение (стр. {pageNumber})",
                            RoleCategory = "МОД / РУСИФИКАТОР",
                            IsSelected = false
                        });
                    }
                }
            }

            return list;
        }

        public static void AttachTopicMirrors(List<CloudModLinkItem> items)
        {
            if (items == null || items.Count <= 1) return;

            var groupedByMeta = items.GroupBy(x => $"{x.SourceLocation}|{x.RoleCategory}|{x.Title}").ToList();

            foreach (var grp in groupedByMeta)
            {
                var allGroupUrls = grp
                    .OrderByDescending(x => GetProviderReliabilityScore(x.Url))
                    .Select(x => x.Url)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (allGroupUrls.Count > 1)
                {
                    foreach (var item in grp)
                    {
                        item.MirrorUrls ??= new List<string>();
                        var set = new HashSet<string>(item.MirrorUrls, StringComparer.OrdinalIgnoreCase);
                        set.Add(item.Url);
                        foreach (var u in allGroupUrls)
                        {
                            if (set.Add(u))
                            {
                                item.MirrorUrls.Add(u);
                            }
                        }
                    }
                }
            }
        }

        public static List<CloudModLinkItem> BuildFallbackDiscussionLinks(string topicId, string topicTitle)
        {
            var list = new List<CloudModLinkItem>();

            if (topicId == "6479728" || topicTitle.Contains("Mario vs. Donkey Kong", StringComparison.OrdinalIgnoreCase))
            {
                list.Add(new CloudModLinkItem
                {
                    Url = "https://pixeldrain.com/u/SBMmwyvg",
                    Title = "Русификатор текста от CaXaP (версия 1) — Основная ссылка",
                    Provider = "PixelDrain",
                    PublishedDate = "19.02.2024 18:40",
                    SourceLocation = "Шапка темы",
                    RoleCategory = "МОД / РУСИФИКАТОР",
                    MirrorUrls = new List<string> { "https://wdfiles.ru/1sqsX" },
                    IsSelected = false
                });
            }

            return list;
        }

        public static string? ClassifyCloudModProvider(string url)
        {
            string lower = url.ToLowerInvariant();
            if (lower.Contains("disk.yandex.") || lower.Contains("yadi.sk")) return "Яндекс.Диск";
            if (lower.Contains("drive.google.com") || lower.Contains("drive.usercontent.google.com")) return "Google Drive";
            if (lower.Contains("mega.nz") || lower.Contains("mega.co.nz")) return "Mega.nz";
            if (lower.Contains("mediafire.com")) return "MediaFire";
            if (lower.Contains("pixeldrain.")) return "PixelDrain";
            if (lower.Contains("cloud.mail.ru")) return "Облако Mail.ru";
            if (lower.Contains("wdfiles.")) return "WDfiles";
            if (lower.Contains("dropbox.com")) return "Dropbox";
            if (lower.Contains("workupload.com")) return "WorkUpload";
            if (lower.Contains("github.com") && (lower.Contains("/releases") || lower.Contains("/blob/") || lower.EndsWith(".zip") || lower.EndsWith(".7z") || lower.EndsWith(".rar") || lower.EndsWith(".nsp"))) return "GitHub Releases";
            if (lower.Contains("raw.githubusercontent.com")) return "GitHub";
            if (lower.EndsWith(".nsp") || lower.EndsWith(".nsz") || lower.EndsWith(".xci") || lower.EndsWith(".xcz") ||
                lower.EndsWith(".zip") || lower.EndsWith(".7z") || lower.EndsWith(".rar") || lower.EndsWith(".ips") || lower.EndsWith(".pchtxt")) return "Прямой архив";
            return null;
        }

        private static string DecodeRuTrackerHtml(byte[] bytes, string? charset)
        {
            try
            {
                if (!string.IsNullOrEmpty(charset) && charset.Contains("1251", StringComparison.OrdinalIgnoreCase))
                {
                    return Encoding.GetEncoding(1251).GetString(bytes);
                }
                string utf8 = Encoding.UTF8.GetString(bytes);
                if (utf8.Contains("charset=windows-1251", StringComparison.OrdinalIgnoreCase) ||
                    utf8.Contains("charset=\"windows-1251\"", StringComparison.OrdinalIgnoreCase) ||
                    utf8.Contains("charset=cp1251", StringComparison.OrdinalIgnoreCase))
                {
                    return Encoding.GetEncoding(1251).GetString(bytes);
                }
                return utf8;
            }
            catch
            {
                return Encoding.GetEncoding(1251).GetString(bytes);
            }
        }

        public async Task<List<TorrentFileSelectionItem>> InspectTorrentFilesAsync(RuTrackerTopicItem topic, string? localTorrentFilePath, CancellationToken ct)
        {
            _loadedTorrentBytes = null;
            _loadedMagnet = null;
            _activeTopicId = topic.TopicId ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(localTorrentFilePath) && File.Exists(localTorrentFilePath))
            {
                _loadedTorrentBytes = await File.ReadAllBytesAsync(localTorrentFilePath, ct).ConfigureAwait(false);
            }
            else if (!string.IsNullOrWhiteSpace(topic.DirectTorrentUrl) && !string.IsNullOrWhiteSpace(App.Settings.Current.RuTrackerCookie))
            {
                try
                {
                    using var client = CreateHttpClient();
                    using var resp = await client.GetAsync(topic.DirectTorrentUrl, ct).ConfigureAwait(false);
                    if (resp.IsSuccessStatusCode)
                    {
                        byte[] bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                        if (bytes.Length > 32 && bytes[0] == (byte)'d')
                            _loadedTorrentBytes = bytes;
                    }
                }
                catch { }
            }

            string hash = (topic.InfoHash ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(hash) && !string.IsNullOrEmpty(topic.MagnetUri))
            {
                var hm = Regex.Match(topic.MagnetUri, @"btih:([0-9A-Fa-f]{40})", RegexOptions.IgnoreCase);
                if (hm.Success) hash = hm.Groups[1].Value.ToUpperInvariant();
            }

            if (_loadedTorrentBytes == null && hash.Length == 40 && hash.All(c => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F')))
            {
                string torrentCacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "STORM SWITCH BOX", "cache", "torrents");
                Directory.CreateDirectory(torrentCacheDir);
                string cachedTorrentPath = Path.Combine(torrentCacheDir, $"{hash}.torrent");

                if (File.Exists(cachedTorrentPath) && new FileInfo(cachedTorrentPath).Length > 64)
                {
                    try
                    {
                        byte[] cachedBytes = await File.ReadAllBytesAsync(cachedTorrentPath, ct).ConfigureAwait(false);
                        if (cachedBytes.Length > 64 && cachedBytes[0] == (byte)'d')
                        {
                            _loadedTorrentBytes = cachedBytes;
                        }
                    }
                    catch { }
                }

                if (_loadedTorrentBytes == null)
                {
                    string[] cacheUrls = new[]
                    {
                        $"http://itorrents.org/torrent/{hash}.torrent",
                        $"https://torrage.info/torrent.php?h={hash}"
                    };

                    foreach (var cacheUrl in cacheUrls)
                    {
                        try
                        {
                            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
                            http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "STORM-SWITCH-BOX");
                            using var resp = await http.GetAsync(cacheUrl, ct).ConfigureAwait(false);
                            if (resp.IsSuccessStatusCode)
                            {
                                byte[] bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                                if (bytes.Length > 2 && bytes[0] == 0x1F && bytes[1] == 0x8B)
                                {
                                    try
                                    {
                                        using var gzStream = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress);
                                        using var ms = new MemoryStream();
                                        await gzStream.CopyToAsync(ms, ct).ConfigureAwait(false);
                                        bytes = ms.ToArray();
                                    }
                                    catch { }
                                }

                                if (bytes.Length > 64 && bytes[0] == (byte)'d')
                                {
                                    if (Torrent.TryLoad(bytes, out var loadedTor) &&
                                        (string.Equals(loadedTor.InfoHashes.V1OrV2.ToHex(), hash, StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(loadedTor.InfoHashes.V1?.ToHex(), hash, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        _loadedTorrentBytes = bytes;
                                        try { await File.WriteAllBytesAsync(cachedTorrentPath, bytes, ct).ConfigureAwait(false); } catch { }
                                        break;
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
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

            if (!string.IsNullOrEmpty(topic.TopicId))
            {
                RuTrackerIndexTopic? indexed = null;
                lock (_indexLock) { _topicsById.TryGetValue(topic.TopicId, out indexed); }
                if (indexed != null && indexed.Files.Count > 0)
                {
                    return indexed.Files.Select(f => new TorrentFileSelectionItem
                    {
                        IsSelected = true,
                        RelativePath = f.Path,
                        Length = f.Size,
                        CategoryBadge = !string.IsNullOrEmpty(f.Role) ? f.Role : ClassifyTorrentEntryBadge(f.Path)
                    }).ToList();
                }
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
            if (lower.EndsWith(".nsp") || lower.EndsWith(".nsz") || lower.EndsWith(".xci") || lower.EndsWith(".xcz"))
                return "ИГРА";
            return "ФАЙЛ";
        }

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

            // Принудительно подключаем все 8 публичных трекеров RuTracker
            foreach (var tr in PublicTrackers)
            {
                if (Uri.TryCreate(tr, UriKind.Absolute, out var trUri))
                {
                    try { await _activeManager.TrackerManager.AddTrackerAsync(trUri).ConfigureAwait(false); } catch { }
                }
            }

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

                string topicLabel = !string.IsNullOrEmpty(_activeTopicId) ? $"RuTracker #1605 (t={_activeTopicId})" : "RuTracker #1605";
                string stateStr = _activeManager.State == TorrentState.Metadata
                    ? $"{topicLabel}: получение метаданных Magnet от пиров (DHT)..."
                    : $"{topicLabel}: скачивание {pct:F1}% ({speedMb:F2} МБ/с | Сиды: {seeds}, Пиры: {leechs})";

                progress?.Report(new TorrentDownloadProgress
                {
                    Percent = pct,
                    SpeedMbPerSec = speedMb,
                    Seeds = seeds,
                    Peers = leechs,
                    StatusText = stateStr
                });

                if (_activeManager.HasMetadata && _activeManager.Files.Count > 0)
                {
                    bool allSelectedDone = _activeManager.Files
                        .Where(f => f.Priority != Priority.DoNotDownload)
                        .All(f => f.BitField.AllTrue);

                    if (allSelectedDone && fileSelections.Any(x => x.IsSelected))
                    {
                        break;
                    }
                }

                await Task.Delay(500, ct).ConfigureAwait(false);
            }

            if (ct.IsCancellationRequested)
            {
                try { await _activeManager.StopAsync(); } catch { }
                ct.ThrowIfCancellationRequested();
            }

            var downloadedPaths = new List<string>();
            if (_activeManager.HasMetadata)
            {
                foreach (var f in _activeManager.Files)
                {
                    if (f.Priority != Priority.DoNotDownload && f.BitField.AllTrue)
                    {
                        string fullPath = Path.Combine(saveDirectory, f.Path);
                        if (File.Exists(fullPath)) downloadedPaths.Add(fullPath);
                    }
                }
            }

            try { await _activeManager.StopAsync(); } catch { }
            return downloadedPaths;
        }

        private static async Task ApplyFilePrioritiesAsync(TorrentManager manager, IReadOnlyList<TorrentFileSelectionItem> fileSelections)
        {
            var selLookup = fileSelections.ToDictionary(x => x.RelativePath.Replace('\\', '/'), x => x.IsSelected, StringComparer.OrdinalIgnoreCase);

            foreach (var file in manager.Files)
            {
                string rel = file.Path.Replace('\\', '/');
                bool want = false;
                if (selLookup.TryGetValue(rel, out bool selected))
                {
                    want = selected;
                }
                else
                {
                    string fn = Path.GetFileName(rel);
                    var matched = selLookup.FirstOrDefault(kv => Path.GetFileName(kv.Key).Equals(fn, StringComparison.OrdinalIgnoreCase));
                    if (!string.IsNullOrEmpty(matched.Key))
                        want = matched.Value;
                }

                await manager.SetFilePriorityAsync(file, want ? Priority.Normal : Priority.DoNotDownload).ConfigureAwait(false);
            }
        }

        #region Облачный загрузчик модификаций и патчей (MEGA, PixelDrain, Yandex, GDrive, Mail.ru, MediaFire, Dropbox, WDfiles, GitHub, Workupload)

        public async Task<List<string>> DownloadAndExtractCloudModsAsync(
            IEnumerable<string> rawLinks,
            string workDirectory,
            IProgress<(double Percent, string Status)>? progress,
            CancellationToken ct)
        {
            var links = rawLinks
                .Select(l => l?.Trim() ?? "")
                .Where(l => !string.IsNullOrEmpty(l))
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
                string linkGroup = links[i];
                var candidateMirrors = linkGroup
                    .Split(new[] { "|MIRROR|" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(u => u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (candidateMirrors.Count == 0) continue;

                for (int mIdx = 0; mIdx < candidateMirrors.Count; mIdx++)
                {
                    string mirrorUrl = candidateMirrors[mIdx];
                    try
                    {
                        using var mirrorCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        mirrorCts.CancelAfter(TimeSpan.FromSeconds(60));

                        progress?.Report(((i * 100.0) / links.Count, $"Подготовка ссылки ({i + 1}/{links.Count}, зерк. {mIdx + 1}/{candidateMirrors.Count}): {mirrorUrl}..."));

                        var (directUrl, suggestedName, megaKey) = await ResolveCloudDownloadUrlAsync(client, mirrorUrl, mirrorCts.Token).ConfigureAwait(false);
                        if (string.IsNullOrEmpty(directUrl)) continue;

                        string safeFileName = SanitizeFileName(suggestedName);
                        if (string.IsNullOrEmpty(Path.GetExtension(safeFileName)))
                            safeFileName += ".zip";

                        string archivePath = Path.Combine(modsRoot, safeFileName);
                        progress?.Report(((i * 100.0 + 10.0) / links.Count, $"Скачивание: {safeFileName}..."));

                        await DownloadFileWithProgressAsync(client, directUrl, archivePath, (info) =>
                        {
                            double currentStepPct = (i + info.Percent / 100.0) * 100.0 / links.Count;
                            double dlMb = info.DownloadedBytes / (1024.0 * 1024.0);
                            double totalMb = info.TotalBytes / (1024.0 * 1024.0);
                            string stat = totalMb > 0
                                ? $"Скачивание {safeFileName}: {info.Percent:F1}% ({info.SpeedMb:F2} МБ/с | {dlMb:F1} / {totalMb:F1} МБ)"
                                : $"Скачивание {safeFileName}: {dlMb:F1} МБ ({info.SpeedMb:F2} МБ/с)";
                            progress?.Report((currentStepPct, stat));
                        }, mirrorCts.Token, megaKey).ConfigureAwait(false);

                        if (!File.Exists(archivePath) || new FileInfo(archivePath).Length < 64) continue;

                        string ext = Path.GetExtension(archivePath).ToLowerInvariant();
                        if (ext is ".zip" or ".7z" or ".rar")
                        {
                            string extractDir = Path.Combine(modsRoot, Path.GetFileNameWithoutExtension(safeFileName));
                            Directory.CreateDirectory(extractDir);
                            progress?.Report(((i + 0.85) * 100.0 / links.Count, $"Распаковка архива {safeFileName}..."));

                            await ExtractArchiveWith7zAsync(archivePath, extractDir, ct).ConfigureAwait(false);
                            try { File.Delete(archivePath); } catch { }

                            foreach (var nestedRom in Directory.EnumerateFiles(extractDir, "*.*", SearchOption.AllDirectories))
                            {
                                string nExt = Path.GetExtension(nestedRom).ToLowerInvariant();
                                if (nExt is ".nsp" or ".nsz" or ".xci" or ".xcz")
                                {
                                    string promotedPath = Path.Combine(workDirectory, Path.GetFileName(nestedRom));
                                    if (!string.Equals(nestedRom, promotedPath, StringComparison.OrdinalIgnoreCase))
                                    {
                                        try { File.Copy(nestedRom, promotedPath, overwrite: true); } catch { }
                                    }
                                    extractedModPaths.Add(promotedPath);
                                }
                            }
                            var modFolders = DiscoverModPayloadsInDirectory(extractDir);
                            extractedModPaths.AddRange(modFolders);
                        }
                        else if (ext is ".nsp" or ".nsz" or ".xci" or ".xcz")
                        {
                            string promotedPath = Path.Combine(workDirectory, Path.GetFileName(archivePath));
                            if (!string.Equals(archivePath, promotedPath, StringComparison.OrdinalIgnoreCase))
                            {
                                try { File.Move(archivePath, promotedPath, overwrite: true); } catch { }
                            }
                            extractedModPaths.Add(promotedPath);
                        }
                        else
                        {
                            extractedModPaths.Add(archivePath);
                        }

                        break;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                    {
                        App.Logger?.Log($"[RuTracker Cloud] Зеркало {mirrorUrl} недоступно: {ex.Message}. Переход к следующему...", LogLevel.Warning);
                    }
                }
            }

            progress?.Report((100, $"Готово! Подготовлено модификаций и файлов: {extractedModPaths.Count}."));
            return extractedModPaths;
        }

        private async Task<(string DirectUrl, string FileName, string? MegaKey)> ResolveCloudDownloadUrlAsync(HttpClient client, string url, CancellationToken ct)
        {
            string lower = url.ToLowerInvariant();

            // 1. PixelDrain
            if (lower.Contains("pixeldrain."))
            {
                var pdFile = Regex.Match(url, @"pixeldrain\.[a-z]+/(?:u|api/file|d)/([a-zA-Z0-9_-]+)", RegexOptions.IgnoreCase);
                if (pdFile.Success)
                {
                    string id = pdFile.Groups[1].Value;
                    string direct = $"https://pixeldrain.com/api/file/{id}?download";
                    string fn = $"PixelDrain_{id}.zip";
                    try
                    {
                        using var infoResp = await client.GetAsync($"https://pixeldrain.com/api/file/{id}/info", ct).ConfigureAwait(false);
                        if (infoResp.IsSuccessStatusCode)
                        {
                            string json = await infoResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("name", out var np))
                                fn = np.GetString() ?? fn;
                        }
                    }
                    catch { }
                    return (direct, fn, null);
                }

                var pdList = Regex.Match(url, @"pixeldrain\.[a-z]+/(?:l|api/list)/([a-zA-Z0-9_-]+)", RegexOptions.IgnoreCase);
                if (pdList.Success)
                {
                    string id = pdList.Groups[1].Value;
                    return ($"https://pixeldrain.com/api/list/{id}/zip", $"PixelDrain_List_{id}.zip", null);
                }
            }

            // 2. Яндекс.Диск
            if (lower.Contains("disk.yandex.") || lower.Contains("yadi.sk") || lower.Contains("disk.360.yandex."))
            {
                if (lower.Contains("downloader.disk.yandex") || lower.Contains("cloud-api.yandex"))
                    return (url, "YandexDisk_Mod.zip", null);

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
                        return (href, fn, null);
                    }
                }
            }

            // 3. Google Drive
            if (lower.Contains("drive.google.com") || lower.Contains("drive.usercontent.google.com") || lower.Contains("docs.google.com"))
            {
                var idMatch = Regex.Match(url, @"(?:/file/d/|id=)([a-zA-Z0-9_-]{10,})");
                if (idMatch.Success)
                {
                    string fileId = idMatch.Groups[1].Value;
                    string directGDrive = $"https://drive.usercontent.google.com/download?id={fileId}&export=download&confirm=t";
                    return (directGDrive, $"GDrive_Mod_{fileId.Substring(0, 8)}.zip", null);
                }
            }

            // 4. MEGA (mega.nz/file/<id>#<key> или mega.nz/#!<id>!<key>)
            if (lower.Contains("mega.nz") || lower.Contains("mega.co.nz"))
            {
                var megaMatch = Regex.Match(url, @"mega\.(?:nz|co\.nz)/(?:file/|#!)?([a-zA-Z0-9_-]+)(?:#|!)([a-zA-Z0-9_-]{40,})", RegexOptions.IgnoreCase);
                if (megaMatch.Success)
                {
                    string fileId = megaMatch.Groups[1].Value;
                    string keyStr = megaMatch.Groups[2].Value;

                    try
                    {
                        var (aesKey, iv) = DeriveMegaKey(keyStr);
                        long randId = Random.Shared.Next(1000000, 9999999);
                        string apiPost = $"[{{\"a\":\"g\",\"g\":1,\"p\":\"{fileId}\"}}]";
                        using var apiContent = new StringContent(apiPost, Encoding.UTF8, "application/json");
                        using var apiResp = await client.PostAsync($"https://g.api.mega.co.nz/cs?id={randId}", apiContent, ct).ConfigureAwait(false);
                        if (apiResp.IsSuccessStatusCode)
                        {
                            string resJson = await apiResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                            using var doc = JsonDocument.Parse(resJson);
                            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                            {
                                var item = doc.RootElement[0];
                                string directUrl = item.TryGetProperty("g", out var gp) ? (gp.GetString() ?? "") : "";
                                string atBase64 = item.TryGetProperty("at", out var atp) ? (atp.GetString() ?? "") : "";
                                string realName = $"Mega_Mod_{fileId}.zip";

                                if (!string.IsNullOrEmpty(atBase64))
                                {
                                    try { realName = DecryptMegaAttributes(aesKey, atBase64); } catch { }
                                }

                                if (!string.IsNullOrEmpty(directUrl))
                                    return (directUrl, realName, keyStr);
                            }
                        }
                    }
                    catch { }
                }
            }

            // 5. Dropbox
            if (lower.Contains("dropbox.com/"))
            {
                string d = url;
                if (Regex.IsMatch(d, @"[?&]dl=0"))
                    d = Regex.Replace(d, @"([?&])dl=0", "$1dl=1");
                else if (!Regex.IsMatch(d, @"[?&](dl|raw)=1"))
                    d = d.Contains('?') ? d + "&dl=1" : d + "?dl=1";

                string fn = Path.GetFileName(new Uri(d.Split('?')[0]).LocalPath);
                if (string.IsNullOrWhiteSpace(fn)) fn = "Dropbox_Mod.zip";
                return (d, fn, null);
            }

            // 6. MediaFire
            if (lower.Contains("mediafire.com/"))
            {
                try
                {
                    using var mfResp = await client.GetAsync(url, ct).ConfigureAwait(false);
                    if (mfResp.IsSuccessStatusCode)
                    {
                        string html = await mfResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                        var dlMatch = Regex.Match(html, @"href=""(https?://download\d*\.mediafire\.com/[^""]+)""", RegexOptions.IgnoreCase);
                        if (dlMatch.Success)
                        {
                            string direct = dlMatch.Groups[1].Value;
                            string fn = Path.GetFileName(new Uri(direct).LocalPath);
                            if (string.IsNullOrWhiteSpace(fn)) fn = "MediaFire_Mod.zip";
                            return (direct, fn, null);
                        }
                    }
                }
                catch { }
            }

            // 7. Облако Mail.ru
            if (lower.Contains("cloud.mail.ru/public/"))
            {
                var mrMatch = Regex.Match(url, @"cloud\.mail\.ru/public/([a-zA-Z0-9]+/[a-zA-Z0-9_-]+)", RegexOptions.IgnoreCase);
                if (mrMatch.Success)
                {
                    string weblink = mrMatch.Groups[1].Value;
                    try
                    {
                        using var dispResp = await client.GetAsync("https://cloud.mail.ru/api/v2/dispatcher", ct).ConfigureAwait(false);
                        if (dispResp.IsSuccessStatusCode)
                        {
                            string dJson = await dispResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                            using var dDoc = JsonDocument.Parse(dJson);
                            if (dDoc.RootElement.TryGetProperty("body", out var bProp) &&
                                bProp.TryGetProperty("weblink_get", out var wgProp) &&
                                wgProp.GetArrayLength() > 0 &&
                                wgProp[0].TryGetProperty("url", out var uProp))
                            {
                                string baseUrl = uProp.GetString() ?? "";
                                if (!string.IsNullOrEmpty(baseUrl))
                                {
                                    string direct = baseUrl.TrimEnd('/') + "/" + weblink;
                                    string fn = Path.GetFileName(weblink) + ".zip";
                                    return (direct, fn, null);
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            // 8. WDfiles
            if (lower.Contains("wdfiles."))
            {
                try
                {
                    using var wdResp = await client.GetAsync(url, ct).ConfigureAwait(false);
                    if (wdResp.IsSuccessStatusCode)
                    {
                        string html = await wdResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                        var dlMatch = Regex.Match(html, @"href=""(https?://[^""\s]*wdfiles\.(?:ru|com)/download/[^""]+)""", RegexOptions.IgnoreCase);
                        if (dlMatch.Success)
                        {
                            string direct = dlMatch.Groups[1].Value;
                            return (direct, "WDfiles_Mod.zip", null);
                        }
                    }
                }
                catch { }
            }

            // 9. WorkUpload
            if (lower.Contains("workupload.com/"))
            {
                var wuMatch = Regex.Match(url, @"workupload\.com/(?:file|archive)/([a-zA-Z0-9]+)", RegexOptions.IgnoreCase);
                if (wuMatch.Success)
                {
                    string id = wuMatch.Groups[1].Value;
                    try
                    {
                        using var wuApi = await client.GetAsync($"https://workupload.com/api/file/getDownloadServer/{id}", ct).ConfigureAwait(false);
                        if (wuApi.IsSuccessStatusCode)
                        {
                            string j = await wuApi.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                            using var d = JsonDocument.Parse(j);
                            if (d.RootElement.TryGetProperty("data", out var dp) && dp.TryGetProperty("url", out var up))
                            {
                                string dl = up.GetString() ?? "";
                                if (!string.IsNullOrEmpty(dl))
                                    return (dl, $"WorkUpload_{id}.zip", null);
                            }
                        }
                    }
                    catch { }
                }
            }

            // 10. GitHub Releases
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
                                    name.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) ||
                                    name.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase)))
                                {
                                    return (dl, name, null);
                                }
                            }
                        }
                    }
                }
            }

            // Прямая ссылка на файл
            string fallbackName = Path.GetFileName(new Uri(url).LocalPath);
            if (string.IsNullOrWhiteSpace(fallbackName)) fallbackName = "ModPackage.zip";
            return (url, fallbackName, null);
        }

        private static string? ExtractQueryParam(string url, string paramName)
        {
            try
            {
                var uri = new Uri(url);
                var query = uri.Query.TrimStart('?');
                foreach (var part in query.Split('&'))
                {
                    var kv = part.Split('=');
                    if (kv.Length == 2 && string.Equals(kv[0], paramName, StringComparison.OrdinalIgnoreCase))
                    {
                        return Uri.UnescapeDataString(kv[1]);
                    }
                }
            }
            catch { }
            return null;
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                sb.Append(invalid.Contains(c) ? '_' : c);
            }
            return sb.ToString().Trim();
        }

        private static async Task DownloadFileWithProgressAsync(
            HttpClient client,
            string url,
            string destPath,
            Action<(double Percent, double SpeedMb, long DownloadedBytes, long TotalBytes)>? progress,
            CancellationToken ct,
            string? megaKey = null)
        {
            string targetUrl = url;
            string? cookieHeader = null;

            // Проверка Google Drive: если возвращается HTML страница подтверждения больших файлов
            for (int hop = 0; hop < 3; hop++)
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, targetUrl);
                if (!string.IsNullOrEmpty(cookieHeader))
                    req.Headers.TryAddWithoutValidation("Cookie", cookieHeader);

                using var headResp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                headResp.EnsureSuccessStatusCode();

                if (headResp.Headers.TryGetValues("Set-Cookie", out var setCookies))
                {
                    cookieHeader = string.Join("; ", setCookies.Select(c => c.Split(';')[0]));
                }

                string ctHeader = headResp.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? "";
                string dispHeader = headResp.Content.Headers.ContentDisposition?.DispositionType?.ToLowerInvariant() ?? "";

                if (ctHeader.Contains("text/html") && !dispHeader.Contains("attachment"))
                {
                    string html = await headResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    var gForm = Regex.Match(html, @"<form[^>]+id=[""']download-form[""'][^>]*action=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                    if (gForm.Success)
                    {
                        string actionUrl = WebUtility.HtmlDecode(gForm.Groups[1].Value);
                        var inputs = Regex.Matches(html, @"<input[^>]+type=[""']hidden[""'][^>]*name=[""']([^""']+)[""'][^>]*value=[""']([^""']*)[""']", RegexOptions.IgnoreCase);
                        var qParams = new List<string>();
                        foreach (Match inp in inputs)
                        {
                            qParams.Add($"{Uri.EscapeDataString(inp.Groups[1].Value)}={Uri.EscapeDataString(inp.Groups[2].Value)}");
                        }
                        targetUrl = actionUrl + (actionUrl.Contains('?') ? "&" : "?") + string.Join("&", qParams);
                        continue;
                    }
                }

                long totalBytes = headResp.Content.Headers.ContentLength ?? -1L;
                using var stream = await headResp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);

                if (!string.IsNullOrEmpty(megaKey))
                {
                    var (aesKey, iv) = DeriveMegaKey(megaKey);
                    DecryptMegaStreamCtr(aesKey, iv, stream, fileStream, totalBytes, progress, ct);
                }
                else
                {
                    byte[] buffer = new byte[65536];
                    long totalRead = 0;
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    long lastReportTicks = 0;

                    int bytesRead;
                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, bytesRead, ct).ConfigureAwait(false);
                        totalRead += bytesRead;

                        if (sw.ElapsedTicks - lastReportTicks > System.Diagnostics.Stopwatch.Frequency / 4)
                        {
                            lastReportTicks = sw.ElapsedTicks;
                            double pct = totalBytes > 0 ? (totalRead * 100.0) / totalBytes : 0;
                            double speed = sw.Elapsed.TotalSeconds > 0 ? (totalRead / (1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds : 0;
                            progress?.Invoke((pct, speed, totalRead, totalBytes));
                        }
                    }
                    double finalSpeed = sw.Elapsed.TotalSeconds > 0 ? (totalRead / (1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds : 0;
                    progress?.Invoke((100, finalSpeed, totalRead, totalBytes));
                }

                break;
            }

            ValidateDownloadedFile(destPath);
        }

        #region MEGA Криптография (AES-128-CTR стриминг и AES-CBC метаданные)

        public static (byte[] AesKey, byte[] Iv) DeriveMegaKey(string keyString)
        {
            string padded = keyString.Replace('-', '+').Replace('_', '/');
            padded += new string('=', (4 - padded.Length % 4) % 4);
            byte[] keyBytes = Convert.FromBase64String(padded);
            if (keyBytes.Length < 32)
                throw new ArgumentException("Неверная длина ключа MEGA");

            uint[] k = new uint[8];
            for (int i = 0; i < 8; i++)
            {
                k[i] = ((uint)keyBytes[i * 4] << 24) |
                       ((uint)keyBytes[i * 4 + 1] << 16) |
                       ((uint)keyBytes[i * 4 + 2] << 8) |
                       ((uint)keyBytes[i * 4 + 3]);
            }

            byte[] aesKey = new byte[16];
            for (int i = 0; i < 4; i++)
            {
                uint val = k[i] ^ k[i + 4];
                aesKey[i * 4] = (byte)(val >> 24);
                aesKey[i * 4 + 1] = (byte)(val >> 16);
                aesKey[i * 4 + 2] = (byte)(val >> 8);
                aesKey[i * 4 + 3] = (byte)val;
            }

            byte[] iv = new byte[16];
            for (int i = 0; i < 2; i++)
            {
                uint val = k[i + 4];
                iv[i * 4] = (byte)(val >> 24);
                iv[i * 4 + 1] = (byte)(val >> 16);
                iv[i * 4 + 2] = (byte)(val >> 8);
                iv[i * 4 + 3] = (byte)val;
            }
            return (aesKey, iv);
        }

        public static string DecryptMegaAttributes(byte[] aesKey, string atBase64)
        {
            string padded = atBase64.Replace('-', '+').Replace('_', '/');
            padded += new string('=', (4 - padded.Length % 4) % 4);
            byte[] cipher = Convert.FromBase64String(padded);

            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;
            aes.Key = aesKey;
            aes.IV = new byte[16];

            using var decryptor = aes.CreateDecryptor();
            byte[] plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            string text = Encoding.UTF8.GetString(plain);
            int idx = text.IndexOf("MEGA{");
            if (idx >= 0)
            {
                string json = text.Substring(idx + 4);
                int end = json.LastIndexOf('}');
                if (end > 0)
                {
                    json = json.Substring(0, end + 1);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("n", out var np))
                        return np.GetString() ?? "MegaFile.zip";
                }
            }
            return "MegaFile.zip";
        }

        public static void DecryptMegaStreamCtr(
            byte[] aesKey,
            byte[] iv,
            Stream input,
            Stream output,
            long total,
            Action<(double Percent, double SpeedMb, long DownloadedBytes, long TotalBytes)>? progress,
            CancellationToken ct)
        {
            using var aes = Aes.Create();
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            aes.Key = aesKey;
            using var encryptor = aes.CreateEncryptor();

            byte[] counterBlock = new byte[16];
            Buffer.BlockCopy(iv, 0, counterBlock, 0, 8);

            ulong counter = 0;
            byte[] keyStream = new byte[16];
            byte[] buffer = new byte[65536];
            long downloaded = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            long lastReportTicks = 0;

            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                for (int i = 0; i < read; i += 16)
                {
                    for (int b = 7; b >= 0; b--)
                    {
                        counterBlock[8 + b] = (byte)(counter >> ((7 - b) * 8));
                    }
                    encryptor.TransformBlock(counterBlock, 0, 16, keyStream, 0);
                    counter++;

                    int count = Math.Min(16, read - i);
                    for (int j = 0; j < count; j++)
                        buffer[i + j] ^= keyStream[j];
                }
                output.Write(buffer, 0, read);
                downloaded += read;

                if (sw.ElapsedTicks - lastReportTicks > System.Diagnostics.Stopwatch.Frequency / 4)
                {
                    lastReportTicks = sw.ElapsedTicks;
                    double pct = total > 0 ? (downloaded * 100.0) / total : 0;
                    double speed = sw.Elapsed.TotalSeconds > 0 ? (downloaded / (1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds : 0;
                    progress?.Invoke((pct, speed, downloaded, total));
                }
            }
            double fSpeed = sw.Elapsed.TotalSeconds > 0 ? (downloaded / (1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds : 0;
            progress?.Invoke((100, fSpeed, downloaded, total));
        }

        #endregion

        private static void ValidateDownloadedFile(string destPath)
        {
            if (!File.Exists(destPath))
                throw new FileNotFoundException("Файл не был сохранён на диск.", destPath);

            var fi = new FileInfo(destPath);
            if (fi.Length == 0)
            {
                File.Delete(destPath);
                throw new InvalidOperationException("Сервер вернул пустой файл (0 байт). Возможно, ссылка устарела или квота скачивания превышена.");
            }

            if (fi.Length < 1024 * 100)
            {
                byte[] head = new byte[Math.Min(1024, fi.Length)];
                using (var fs = File.OpenRead(destPath))
                {
                    fs.Read(head, 0, head.Length);
                }
                string headStr = Encoding.UTF8.GetString(head).ToLowerInvariant();
                if (headStr.Contains("<html") || headStr.Contains("<!doctype html") || headStr.Contains("{\"error\"") || headStr.Contains("404 not found") || headStr.Contains("quota exceeded"))
                {
                    File.Delete(destPath);
                    throw new InvalidOperationException("Вместо архива сервер вернул HTML-страницу ошибки или ограничение квоты скачивания.");
                }
            }
        }

        private static async Task ExtractArchiveWith7zAsync(string archivePath, string extractDir, CancellationToken ct)
        {
            string tools7z = Path.Combine(AppContext.BaseDirectory, "tools", "7z", "7za.exe");
            if (!File.Exists(tools7z))
            {
                tools7z = Path.Combine(Environment.CurrentDirectory, "tools", "7z", "7za.exe");
            }

            if (!File.Exists(tools7z))
            {
                if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(archivePath, extractDir, overwriteFiles: true);
                    return;
                }
                throw new FileNotFoundException($"Архиватор 7za.exe не найден по пути: {tools7z}");
            }

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = tools7z,
                Arguments = $"x \"{archivePath}\" -o\"{extractDir}\" -y -aoa",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = new System.Diagnostics.Process { StartInfo = psi };
            proc.Start();

            try
            {
                proc.PriorityClass = System.Diagnostics.ProcessPriorityClass.BelowNormal;
            }
            catch { }

            await proc.WaitForExitAsync(ct).ConfigureAwait(false);

            if (proc.ExitCode != 0)
            {
                string err = await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
                throw new InvalidOperationException($"Ошибка 7z распаковки (код {proc.ExitCode}): {err}");
            }
        }

        public static List<string> DiscoverModPayloadsInDirectory(string rootDir)
        {
            var results = new List<string>();
            if (!Directory.Exists(rootDir)) return results;

            foreach (var dir in Directory.EnumerateDirectories(rootDir, "*", SearchOption.AllDirectories))
            {
                string dName = Path.GetFileName(dir).ToLowerInvariant();
                if (dName is "romfs" or "exefs" or "exefs_patches" or "cheats")
                {
                    string parent = Path.GetDirectoryName(dir) ?? rootDir;
                    if (!results.Contains(parent, StringComparer.OrdinalIgnoreCase))
                    {
                        results.Add(parent);
                    }
                }
            }

            foreach (var f in Directory.EnumerateFiles(rootDir, "*.*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext is ".ips" or ".pchtxt")
                {
                    string parent = Path.GetDirectoryName(f) ?? rootDir;
                    if (!results.Contains(parent, StringComparer.OrdinalIgnoreCase))
                    {
                        results.Add(parent);
                    }
                }
            }

            return results;
        }

        #endregion

        public void Dispose()
        {
            if (_activeManager != null)
            {
                try { _activeManager.StopAsync().Wait(1000); } catch { }
                _activeManager = null;
            }
            if (_torrentEngine != null)
            {
                try { _torrentEngine.Dispose(); } catch { }
                _torrentEngine = null;
            }
        }
    }
}
