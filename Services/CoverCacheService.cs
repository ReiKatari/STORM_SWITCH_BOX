using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace StormSwitchBox.Services
{
    /// <summary>
    /// Высокопроизводительный сервис кэширования и загрузки обложек игр.
    /// Поддерживает все 19 поколений систем Nintendo (от Color TV-Game до Switch 2),
    /// фоновую загрузку через Unbounded Channel, параллельные воркеры и интеллектуальный подбор обложек (LibRetro, GameTDB, Tinfoil, No-Intro).
    /// </summary>
    public static class CoverCacheService
    {
        private static readonly HttpClient _httpClient;
        private static readonly string _baseCoversDirectory;
        private static readonly ConcurrentDictionary<string, string> _memoryUrlMap = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, bool> _queuedUrls = new(StringComparer.OrdinalIgnoreCase);

        private struct DownloadItem
        {
            public List<string> CandidateUrls;
            public string LocalPath;
            public string FolderPath;
            public string OriginalKey;
        }

        private static readonly Channel<DownloadItem> _downloadChannel = Channel.CreateUnbounded<DownloadItem>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

        static CoverCacheService()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(15)
            };

            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");

            string targetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "covers");
            try
            {
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }
                _baseCoversDirectory = targetDir;
            }
            catch
            {
                _baseCoversDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "StormSwitchBox",
                    "covers"
                );
                try
                {
                    if (!Directory.Exists(_baseCoversDirectory))
                    {
                        Directory.CreateDirectory(_baseCoversDirectory);
                    }
                }
                catch { }
            }

            // Запускаем 8 фоновых параллельных потоков загрузки
            for (int i = 0; i < 8; i++)
            {
                Task.Run(ProcessDownloadQueueAsync);
            }
        }

        private static async Task ProcessDownloadQueueAsync()
        {
            var reader = _downloadChannel.Reader;
            while (await reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (reader.TryRead(out var item))
                {
                    try
                    {
                        if (File.Exists(item.LocalPath) && new FileInfo(item.LocalPath).Length > 200)
                        {
                            _memoryUrlMap[item.OriginalKey] = item.LocalPath;
                            continue;
                        }

                        if (!Directory.Exists(item.FolderPath))
                        {
                            Directory.CreateDirectory(item.FolderPath);
                        }

                        bool downloaded = false;
                        foreach (var url in item.CandidateUrls)
                        {
                            if (string.IsNullOrWhiteSpace(url) || url.StartsWith("ms-appx", StringComparison.OrdinalIgnoreCase)) continue;

                            try
                            {
                                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                                using var res = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseContentRead).ConfigureAwait(false);
                                if (res.IsSuccessStatusCode)
                                {
                                    byte[] bytes = await res.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                                    if (bytes.Length > 200)
                                    {
                                        await File.WriteAllBytesAsync(item.LocalPath, bytes).ConfigureAwait(false);
                                        _memoryUrlMap[item.OriginalKey] = item.LocalPath;
                                        downloaded = true;
                                        break;
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    catch
                    {
                        // Silent catch
                    }
                    finally
                    {
                        await Task.Delay(15).ConfigureAwait(false);
                    }
                }
            }
        }

        public static string ResolveCoverUrl(string? rawUrl, string? system = null, string? title = null, string? titleId = null)
        {
            string key = !string.IsNullOrWhiteSpace(rawUrl) ? rawUrl : $"{system}_{title}_{titleId}";

            if (!string.IsNullOrWhiteSpace(rawUrl) &&
                (rawUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
                 rawUrl.StartsWith("ms-appx://", StringComparison.OrdinalIgnoreCase) ||
                 File.Exists(rawUrl)))
            {
                return rawUrl;
            }

            if (_memoryUrlMap.TryGetValue(key, out var cachedPath) && File.Exists(cachedPath))
            {
                return cachedPath;
            }

            string platformDirName = GetPlatformDirectoryName(system);
            string platformFolderPath = Path.Combine(_baseCoversDirectory, platformDirName);
            string safeFileName = BuildSafeFileName(title, titleId, key);
            string localFilePath = Path.Combine(platformFolderPath, safeFileName);

            if (File.Exists(localFilePath) && new FileInfo(localFilePath).Length > 200)
            {
                _memoryUrlMap[key] = localFilePath;
                return localFilePath;
            }

            var candidates = GetCandidateUrls(rawUrl, system, title, titleId);
            string primaryUrl = candidates.Count > 0 ? candidates[0] : "ms-appx:///Assets/placeholder_cover.png";

            if (_queuedUrls.TryAdd(key, true))
            {
                _downloadChannel.Writer.TryWrite(new DownloadItem
                {
                    CandidateUrls = candidates,
                    LocalPath = localFilePath,
                    FolderPath = platformFolderPath,
                    OriginalKey = key
                });
            }

            return primaryUrl;
        }

        public static async Task<string> GetOrDownloadCoverAsync(string? rawUrl, string? system = null, string? title = null, string? titleId = null)
        {
            string key = !string.IsNullOrWhiteSpace(rawUrl) ? rawUrl : $"{system}_{title}_{titleId}";

            if (!string.IsNullOrWhiteSpace(rawUrl) &&
                (rawUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
                 rawUrl.StartsWith("ms-appx://", StringComparison.OrdinalIgnoreCase) ||
                 File.Exists(rawUrl)))
            {
                return rawUrl;
            }

            string platformDirName = GetPlatformDirectoryName(system);
            string platformFolderPath = Path.Combine(_baseCoversDirectory, platformDirName);
            string safeFileName = BuildSafeFileName(title, titleId, key);
            string localFilePath = Path.Combine(platformFolderPath, safeFileName);

            if (File.Exists(localFilePath) && new FileInfo(localFilePath).Length > 200)
            {
                _memoryUrlMap[key] = localFilePath;
                return localFilePath;
            }

            var candidates = GetCandidateUrls(rawUrl, system, title, titleId);

            try
            {
                if (!Directory.Exists(platformFolderPath))
                {
                    Directory.CreateDirectory(platformFolderPath);
                }

                foreach (var url in candidates)
                {
                    if (string.IsNullOrWhiteSpace(url) || url.StartsWith("ms-appx", StringComparison.OrdinalIgnoreCase)) continue;

                    using var req = new HttpRequestMessage(HttpMethod.Get, url);
                    using var res = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseContentRead).ConfigureAwait(false);
                    if (res.IsSuccessStatusCode)
                    {
                        byte[] bytes = await res.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                        if (bytes.Length > 200)
                        {
                            await File.WriteAllBytesAsync(localFilePath, bytes).ConfigureAwait(false);
                            _memoryUrlMap[key] = localFilePath;
                            return localFilePath;
                        }
                    }
                }
            }
            catch { }

            return candidates.Count > 0 ? candidates[0] : "ms-appx:///Assets/placeholder_cover.png";
        }

        public static string SanitizeLibretroTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return "";
            var invalid = new[] { '&', '*', '/', ':', '`', '<', '>', '?', '\\', '|', '"' };
            var sb = new StringBuilder(title.Length);
            foreach (char c in title)
            {
                if (Array.IndexOf(invalid, c) >= 0)
                    sb.Append('_');
                else
                    sb.Append(c);
            }
            return sb.ToString().Trim();
        }

        public static List<string> GetCandidateUrls(string? rawUrl, string? system, string? title, string? titleId)
        {
            var list = new List<string>();

            if (!string.IsNullOrWhiteSpace(rawUrl) && !rawUrl.Contains("nintendolife.com", StringComparison.OrdinalIgnoreCase))
            {
                list.Add(rawUrl);
            }

            string repo = GetLibretroRepoName(system);

            if (!string.IsNullOrWhiteSpace(title))
            {
                string cleanFull = SanitizeLibretroTitle(title.Trim());
                string escapedFull = Uri.EscapeDataString(cleanFull);
                list.Add($"https://raw.githubusercontent.com/libretro-thumbnails/{repo}/master/Named_Boxarts/{escapedFull}.png");

                // Извлекаем базовое название без круглых скобок с регионами
                string baseTitle = Regex.Replace(cleanFull, @"\s*\([^)]*\)", "").Trim();
                if (!string.IsNullOrEmpty(baseTitle) && !baseTitle.Equals(cleanFull, StringComparison.OrdinalIgnoreCase))
                {
                    string escBase = Uri.EscapeDataString(baseTitle);
                    list.Add($"https://raw.githubusercontent.com/libretro-thumbnails/{repo}/master/Named_Boxarts/{escBase}%20(USA).png");
                    list.Add($"https://raw.githubusercontent.com/libretro-thumbnails/{repo}/master/Named_Boxarts/{escBase}%20(World).png");
                    list.Add($"https://raw.githubusercontent.com/libretro-thumbnails/{repo}/master/Named_Boxarts/{escBase}%20(Europe).png");
                    list.Add($"https://raw.githubusercontent.com/libretro-thumbnails/{repo}/master/Named_Boxarts/{escBase}%20(Japan).png");
                    list.Add($"https://raw.githubusercontent.com/libretro-thumbnails/{repo}/master/Named_Boxarts/{escBase}.png");
                }
            }

            // Дополнительные базы: Tinfoil и GameTDB
            if (!string.IsNullOrWhiteSpace(titleId))
            {
                string tid = titleId.Trim().ToUpperInvariant();
                if (system != null && system.Contains("Switch", StringComparison.OrdinalIgnoreCase))
                {
                    list.Add($"https://tinfoil.media/repo/db/icons/{tid}.jpg");
                    list.Add($"https://art.gametdb.com/switch/coverM/US/{tid}.jpg");
                }
                else if (system != null && system.Contains("3DS", StringComparison.OrdinalIgnoreCase) && tid.Length >= 16)
                {
                    list.Add($"https://art.gametdb.com/3ds/coverM/US/{tid.Substring(8, 4)}.jpg");
                }
            }

            return list;
        }

        public static string GetFallbackUrl(string? system, string? title, string? titleId)
        {
            var candidates = GetCandidateUrls(null, system, title, titleId);
            return candidates.Count > 0 ? candidates[0] : "ms-appx:///Assets/placeholder_cover.png";
        }

        public static string GetLibretroRepoName(string? system)
        {
            if (string.IsNullOrWhiteSpace(system)) return "Nintendo_-_Nintendo_Switch";

            string s = system.Trim();
            if (s.Contains("Color TV", StringComparison.OrdinalIgnoreCase) || s.Equals("TVG", StringComparison.OrdinalIgnoreCase))
                return "Nintendo_-_Color_TV-Game";
            if (s.Contains("Game & Watch", StringComparison.OrdinalIgnoreCase) || s.Contains("Game and Watch", StringComparison.OrdinalIgnoreCase) || s.Equals("GW", StringComparison.OrdinalIgnoreCase))
                return "Nintendo_-_Game_and_Watch";
            if (s.Contains("Famicom Disk", StringComparison.OrdinalIgnoreCase) || s.Equals("FDS", StringComparison.OrdinalIgnoreCase))
                return "Nintendo_-_Family_Computer_Disk_System";
            if (s.Contains("Entertainment System", StringComparison.OrdinalIgnoreCase) || s.Equals("NES", StringComparison.OrdinalIgnoreCase))
                return "Nintendo_-_Nintendo_Entertainment_System";
            if (s.Contains("Satellaview", StringComparison.OrdinalIgnoreCase) || s.Equals("BS-X", StringComparison.OrdinalIgnoreCase))
                return "Nintendo_-_Satellaview";
            if (s.Contains("Virtual Boy", StringComparison.OrdinalIgnoreCase) || s.Equals("VB", StringComparison.OrdinalIgnoreCase))
                return "Nintendo_-_Virtual_Boy";
            if (s.Contains("Super Nintendo", StringComparison.OrdinalIgnoreCase) || s.Equals("SNES", StringComparison.OrdinalIgnoreCase))
                return "Nintendo_-_Super_Nintendo_Entertainment_System";
            if (s.Contains("Nintendo 64", StringComparison.OrdinalIgnoreCase) || s.Equals("N64", StringComparison.OrdinalIgnoreCase))
                return "Nintendo_-_Nintendo_64";
            if (s.Contains("Pokémon Mini", StringComparison.OrdinalIgnoreCase) || s.Contains("Pokemon Mini", StringComparison.OrdinalIgnoreCase) || s.Equals("PM", StringComparison.OrdinalIgnoreCase))
                return "Nintendo_-_Pokemon_Mini";
            if (s.Contains("Game Boy Advance", StringComparison.OrdinalIgnoreCase) || s.Equals("GBA", StringComparison.OrdinalIgnoreCase))
                return "Nintendo_-_Game_Boy_Advance";
            if (s.Contains("Game Boy Color", StringComparison.OrdinalIgnoreCase) || s.Equals("GBC", StringComparison.OrdinalIgnoreCase))
                return "Nintendo_-_Game_Boy_Color";
            if (s.Contains("Game Boy", StringComparison.OrdinalIgnoreCase) || s.Equals("GB", StringComparison.OrdinalIgnoreCase))
                return "Nintendo_-_Game_Boy";
            if (s.Contains("GameCube", StringComparison.OrdinalIgnoreCase) || s.Equals("GC", StringComparison.OrdinalIgnoreCase))
                return "Nintendo_-_GameCube";
            if (s.Contains("Nintendo DS", StringComparison.OrdinalIgnoreCase) || s.Equals("NDS", StringComparison.OrdinalIgnoreCase))
                return "Nintendo_-_Nintendo_DS";
            if (s.Contains("3DS", StringComparison.OrdinalIgnoreCase))
                return "Nintendo_-_Nintendo_3DS";
            if (s.Contains("Wii U", StringComparison.OrdinalIgnoreCase) || s.Equals("WiiU", StringComparison.OrdinalIgnoreCase))
                return "Nintendo_-_Wii_U";
            if (s.Contains("Switch 2", StringComparison.OrdinalIgnoreCase))
                return "Nintendo_-_Nintendo_Switch";
            if (s.Contains("Switch", StringComparison.OrdinalIgnoreCase))
                return "Nintendo_-_Nintendo_Switch";
            if (s.Contains("Wii", StringComparison.OrdinalIgnoreCase))
                return "Nintendo_-_Wii";

            return "Nintendo_-_Nintendo_Switch";
        }

        public static string GetPlatformDirectoryName(string? system)
        {
            if (string.IsNullOrWhiteSpace(system)) return "Nintendo Switch";

            string s = system.Trim();
            if (s.Contains("Color TV", StringComparison.OrdinalIgnoreCase) || s.Equals("TVG", StringComparison.OrdinalIgnoreCase))
                return "Nintendo Color TV-Game";
            if (s.Contains("Game & Watch", StringComparison.OrdinalIgnoreCase) || s.Equals("GW", StringComparison.OrdinalIgnoreCase))
                return "Nintendo Game & Watch";
            if (s.Contains("Famicom Disk", StringComparison.OrdinalIgnoreCase) || s.Equals("FDS", StringComparison.OrdinalIgnoreCase))
                return "Nintendo Famicom Disk System";
            if (s.Contains("Entertainment System", StringComparison.OrdinalIgnoreCase) || s.Equals("NES", StringComparison.OrdinalIgnoreCase))
                return "Nintendo Entertainment System";
            if (s.Contains("Satellaview", StringComparison.OrdinalIgnoreCase) || s.Equals("BS-X", StringComparison.OrdinalIgnoreCase))
                return "Super Famicom Satellaview";
            if (s.Contains("Virtual Boy", StringComparison.OrdinalIgnoreCase) || s.Equals("VB", StringComparison.OrdinalIgnoreCase))
                return "Nintendo Virtual Boy";
            if (s.Contains("Super Nintendo", StringComparison.OrdinalIgnoreCase) || s.Equals("SNES", StringComparison.OrdinalIgnoreCase))
                return "Super Nintendo Entertainment System";
            if (s.Contains("Nintendo 64", StringComparison.OrdinalIgnoreCase) || s.Equals("N64", StringComparison.OrdinalIgnoreCase))
                return "Nintendo 64";
            if (s.Contains("Pokémon Mini", StringComparison.OrdinalIgnoreCase) || s.Contains("Pokemon Mini", StringComparison.OrdinalIgnoreCase) || s.Equals("PM", StringComparison.OrdinalIgnoreCase))
                return "Nintendo Pokémon Mini";
            if (s.Contains("Game Boy Advance", StringComparison.OrdinalIgnoreCase) || s.Equals("GBA", StringComparison.OrdinalIgnoreCase))
                return "Nintendo Game Boy Advance";
            if (s.Contains("Game Boy Color", StringComparison.OrdinalIgnoreCase) || s.Equals("GBC", StringComparison.OrdinalIgnoreCase))
                return "Nintendo Game Boy Color";
            if (s.Contains("Game Boy", StringComparison.OrdinalIgnoreCase) || s.Equals("GB", StringComparison.OrdinalIgnoreCase))
                return "Nintendo Game Boy";
            if (s.Contains("GameCube", StringComparison.OrdinalIgnoreCase) || s.Equals("GC", StringComparison.OrdinalIgnoreCase))
                return "Nintendo GameCube";
            if (s.Contains("Nintendo DS", StringComparison.OrdinalIgnoreCase) || s.Equals("NDS", StringComparison.OrdinalIgnoreCase))
                return "Nintendo DS";
            if (s.Contains("3DS", StringComparison.OrdinalIgnoreCase))
                return "Nintendo 3DS";
            if (s.Contains("Wii U", StringComparison.OrdinalIgnoreCase) || s.Equals("WiiU", StringComparison.OrdinalIgnoreCase))
                return "Nintendo Wii U";
            if (s.Contains("Switch 2", StringComparison.OrdinalIgnoreCase))
                return "Nintendo Switch 2";
            if (s.Contains("Switch", StringComparison.OrdinalIgnoreCase))
                return "Nintendo Switch";
            if (s.Contains("Wii", StringComparison.OrdinalIgnoreCase))
                return "Nintendo Wii";

            return MakeSafeDirectoryName(s);
        }

        private static string MakeSafeDirectoryName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder();
            foreach (char c in name)
            {
                if (c == '/' || c == '\\' || Array.IndexOf(invalid, c) >= 0)
                    sb.Append('_');
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }

        private static string BuildSafeFileName(string? title, string? titleId, string url)
        {
            string ext = GetExtensionFromUrl(url);
            string hash = ComputeHash(url).Substring(0, 8);

            if (!string.IsNullOrWhiteSpace(title))
            {
                string cleanTitle = MakeSafeDirectoryName(title.Trim());
                if (cleanTitle.Length > 50) cleanTitle = cleanTitle.Substring(0, 50).Trim();
                return $"{cleanTitle}_{hash}{ext}";
            }
            else if (!string.IsNullOrWhiteSpace(titleId))
            {
                return $"{titleId}_{hash}{ext}";
            }

            return $"{ComputeHash(url)}{ext}";
        }

        private static string ComputeHash(string input)
        {
            using var sha1 = SHA1.Create();
            byte[] bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                sb.Append(b.ToString("x2"));
            }
            return sb.ToString();
        }

        private static string GetExtensionFromUrl(string url)
        {
            try
            {
                int qIdx = url.IndexOf('?');
                string cleanUrl = qIdx > 0 ? url.Substring(0, qIdx) : url;
                string ext = Path.GetExtension(cleanUrl);
                if (!string.IsNullOrEmpty(ext) && ext.Length <= 5)
                {
                    return ext;
                }
            }
            catch { }
            return ".png";
        }
    }
}
