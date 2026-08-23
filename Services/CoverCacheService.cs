using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace StormSwitchBox.Services
{
    /// <summary>
    /// Высокопроизводительный сервис тихой загрузки и локального дискового кэширования обложек игр
    /// в структурированную папку программы 'covers/{Полное_наименование_платформы}/'
    /// с защитой от блокировок User-Agent, таймаутов и автоматическим разрешением зеркал.
    /// </summary>
    public static class CoverCacheService
    {
        private static readonly HttpClient _httpClient;
        private static readonly string _baseCoversDirectory;
        private static readonly ConcurrentDictionary<string, string> _memoryUrlMap = new(StringComparer.OrdinalIgnoreCase);

        static CoverCacheService()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
                ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true
            };

            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(12)
            };

            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/122.0.0.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept", "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");

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
                // Fallback в AppData при отсутствии прав записи
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
        }

        public static string ResolveCoverUrl(string? rawUrl, string? system = null, string? title = null, string? titleId = null)
        {
            if (string.IsNullOrWhiteSpace(rawUrl))
            {
                return GetFallbackUrl(system, title, titleId);
            }

            // Если уже локальный файл
            if (rawUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
                rawUrl.StartsWith("ms-appx://", StringComparison.OrdinalIgnoreCase) ||
                File.Exists(rawUrl))
            {
                return rawUrl;
            }

            // Проверяем кэш в памяти
            if (_memoryUrlMap.TryGetValue(rawUrl, out var cachedPath) && File.Exists(cachedPath))
            {
                return cachedPath;
            }

            string platformDirName = GetPlatformDirectoryName(system);
            string platformFolderPath = Path.Combine(_baseCoversDirectory, platformDirName);
            string safeFileName = BuildSafeFileName(title, titleId, rawUrl);
            string localFilePath = Path.Combine(platformFolderPath, safeFileName);

            if (File.Exists(localFilePath) && new FileInfo(localFilePath).Length > 200)
            {
                _memoryUrlMap[rawUrl] = localFilePath;
                return localFilePath;
            }

            string normalizedUrl = NormalizeUrl(rawUrl, system, title, titleId);

            // Фоновая тихая загрузка в кэш
            _ = Task.Run(() => CacheImageToDiskQuietlyAsync(normalizedUrl, localFilePath, platformFolderPath, rawUrl));

            return normalizedUrl;
        }

        public static async Task<string> GetOrDownloadCoverAsync(string rawUrl, string? system = null, string? title = null, string? titleId = null)
        {
            if (string.IsNullOrWhiteSpace(rawUrl))
            {
                return GetFallbackUrl(system, title, titleId);
            }

            if (rawUrl.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
                rawUrl.StartsWith("ms-appx://", StringComparison.OrdinalIgnoreCase) ||
                File.Exists(rawUrl))
            {
                return rawUrl;
            }

            string platformDirName = GetPlatformDirectoryName(system);
            string platformFolderPath = Path.Combine(_baseCoversDirectory, platformDirName);
            string safeFileName = BuildSafeFileName(title, titleId, rawUrl);
            string localFilePath = Path.Combine(platformFolderPath, safeFileName);

            if (File.Exists(localFilePath) && new FileInfo(localFilePath).Length > 200)
            {
                _memoryUrlMap[rawUrl] = localFilePath;
                return localFilePath;
            }

            string normalizedUrl = NormalizeUrl(rawUrl, system, title, titleId);
            bool success = await CacheImageToDiskQuietlyAsync(normalizedUrl, localFilePath, platformFolderPath, rawUrl).ConfigureAwait(false);

            return success && File.Exists(localFilePath) ? localFilePath : normalizedUrl;
        }

        private static async Task<bool> CacheImageToDiskQuietlyAsync(string url, string localPath, string folderPath, string originalKey)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseContentRead).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    byte[] bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    if (bytes.Length > 200)
                    {
                        await File.WriteAllBytesAsync(localPath, bytes).ConfigureAwait(false);
                        _memoryUrlMap[originalKey] = localPath;
                        return true;
                    }
                }
            }
            catch
            {
                // Тихо игнорируем сетевые ошибки, UI не блокируется
            }

            return false;
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
                if (cleanTitle.Length > 40) cleanTitle = cleanTitle.Substring(0, 40).Trim();
                return $"{cleanTitle}_{hash}{ext}";
            }
            else if (!string.IsNullOrWhiteSpace(titleId))
            {
                return $"{titleId}_{hash}{ext}";
            }

            return $"{ComputeHash(url)}{ext}";
        }

        private static string NormalizeUrl(string url, string? system, string? title, string? titleId)
        {
            if (url.Contains("nintendolife.com", StringComparison.OrdinalIgnoreCase))
            {
                return GetFallbackUrl(system, title, titleId);
            }
            return url;
        }

        public static string GetFallbackUrl(string? system, string? title, string? titleId)
        {
            if (system != null && system.Contains("3DS", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(title))
                {
                    string cleanTitle = Uri.EscapeDataString(title.Trim());
                    return $"https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_3DS/master/Named_Boxarts/{cleanTitle}%20(USA).png";
                }
                if (!string.IsNullOrWhiteSpace(titleId) && titleId.Length >= 16)
                {
                    return $"https://art.gametdb.com/3ds/coverM/US/{titleId.Substring(8, 4)}.jpg";
                }
            }

            if (system != null && system.Contains("Switch 2", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(title))
                {
                    string cleanTitle = Uri.EscapeDataString(title.Trim());
                    return $"https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Switch/master/Named_Boxarts/{cleanTitle}.png";
                }
            }

            if (system != null && system.Contains("Switch", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(titleId))
                {
                    return $"https://tinfoil.media/repo/db/icons/{titleId.ToUpperInvariant()}.jpg";
                }
                if (!string.IsNullOrWhiteSpace(title))
                {
                    string cleanTitle = Uri.EscapeDataString(title.Trim());
                    return $"https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Switch/master/Named_Boxarts/{cleanTitle}.png";
                }
            }

            if (system != null && (system.Contains("NDS", StringComparison.OrdinalIgnoreCase) || system.Contains("DS", StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.IsNullOrWhiteSpace(title))
                {
                    string cleanTitle = Uri.EscapeDataString(title.Trim());
                    return $"https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_DS/master/Named_Boxarts/{cleanTitle}%20(USA).png";
                }
            }

            if (system != null && (system.Contains("GBA", StringComparison.OrdinalIgnoreCase) || system.Contains("Advance", StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.IsNullOrWhiteSpace(title))
                {
                    string cleanTitle = Uri.EscapeDataString(title.Trim());
                    return $"https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_Boy_Advance/master/Named_Boxarts/{cleanTitle}%20(USA).png";
                }
            }

            if (system != null && (system.Contains("GBC", StringComparison.OrdinalIgnoreCase) || system.Contains("Color", StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.IsNullOrWhiteSpace(title))
                {
                    string cleanTitle = Uri.EscapeDataString(title.Trim());
                    return $"https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_Boy_Color/master/Named_Boxarts/{cleanTitle}%20(USA).png";
                }
            }

            if (system != null && (system.Contains("GB", StringComparison.OrdinalIgnoreCase) || system.Contains("Game Boy", StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.IsNullOrWhiteSpace(title))
                {
                    string cleanTitle = Uri.EscapeDataString(title.Trim());
                    return $"https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Game_Boy/master/Named_Boxarts/{cleanTitle}%20(USA).png";
                }
            }

            if (system != null && (system.Contains("N64", StringComparison.OrdinalIgnoreCase) || system.Contains("64", StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.IsNullOrWhiteSpace(title))
                {
                    string cleanTitle = Uri.EscapeDataString(title.Trim());
                    return $"https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_64/master/Named_Boxarts/{cleanTitle}%20(USA).png";
                }
            }

            if (system != null && (system.Contains("SNES", StringComparison.OrdinalIgnoreCase) || system.Contains("Super", StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.IsNullOrWhiteSpace(title))
                {
                    string cleanTitle = Uri.EscapeDataString(title.Trim());
                    return $"https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Super_Nintendo_Entertainment_System/master/Named_Boxarts/{cleanTitle}%20(USA).png";
                }
            }

            if (system != null && (system.Contains("NES", StringComparison.OrdinalIgnoreCase) || system.Contains("Famicom", StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.IsNullOrWhiteSpace(title))
                {
                    string cleanTitle = Uri.EscapeDataString(title.Trim());
                    return $"https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Nintendo_Entertainment_System/master/Named_Boxarts/{cleanTitle}%20(USA).png";
                }
            }

            if (system != null && (system.Contains("Wii U", StringComparison.OrdinalIgnoreCase) || system.Contains("WiiU", StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.IsNullOrWhiteSpace(title))
                {
                    string cleanTitle = Uri.EscapeDataString(title.Trim());
                    return $"https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Wii_U/master/Named_Boxarts/{cleanTitle}%20(USA).png";
                }
            }

            if (system != null && system.Contains("Wii", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrWhiteSpace(title))
                {
                    string cleanTitle = Uri.EscapeDataString(title.Trim());
                    return $"https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_Wii/master/Named_Boxarts/{cleanTitle}%20(USA).png";
                }
            }

            if (system != null && (system.Contains("GameCube", StringComparison.OrdinalIgnoreCase) || system.Equals("GC", StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.IsNullOrWhiteSpace(title))
                {
                    string cleanTitle = Uri.EscapeDataString(title.Trim());
                    return $"https://raw.githubusercontent.com/libretro-thumbnails/Nintendo_-_GameCube/master/Named_Boxarts/{cleanTitle}%20(USA).png";
                }
            }

            return "ms-appx:///Assets/placeholder_cover.png";
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
            return ".jpg";
        }
    }
}
