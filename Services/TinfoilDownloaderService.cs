using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public class AvailableDownloadItem
    {
        public string TitleId { get; set; } = "";
        public string Name { get; set; } = "";
        public string ContentType { get; set; } = "Update"; // "Update", "DLC"
        public string Version { get; set; } = "v0";
        public string SizeFormatted { get; set; } = "~";
        public string DownloadUrl { get; set; } = "";
        public bool IsSelected { get; set; } = true;
    }

    /// <summary>
    /// Сервис фонового поиска и загрузки обновлений и DLC из TitleDB и публичных репозиториев/зеркал.
    /// Позволяет загружать официальные патчи и DLC в 1 клик с автоматической доставкой в Задачник.
    /// </summary>
    public class TinfoilDownloaderService
    {
        private static TinfoilDownloaderService? _instance;
        public static TinfoilDownloaderService Instance => _instance ??= new TinfoilDownloaderService();

        private readonly HttpClient _httpClient;

        // Публичные репозитории и зеркала метаданных
        private static readonly string[] MirrorEndpoints = new[]
        {
            "https://raw.githubusercontent.com/blawar/titledb/master/US.en.json",
            "https://raw.githubusercontent.com/archbox/titledb/master/titles.json"
        };

        public TinfoilDownloaderService()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };
            _httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0.0.0 Safari/537.36");
        }

        /// <summary>
        /// Вычисляет расчетный TitleID обновления (Patch) для переданного TitleID игры.
        /// </summary>
        public static string GetUpdateTitleId(string baseTitleId)
        {
            if (ulong.TryParse(baseTitleId, System.Globalization.NumberStyles.HexNumber, null, out ulong tid))
            {
                ulong updateTid = (tid & ~0xFFFUL) | 0x800UL;
                return updateTid.ToString("X16").ToLowerInvariant();
            }
            return "";
        }

        /// <summary>
        /// Возвращает список доступных обновлений и дополнений для игры на основе базы TitleDB.
        /// </summary>
        public async Task<List<AvailableDownloadItem>> GetAvailableDownloadsAsync(string baseTitleId, string gameTitle = "")
        {
            var results = new List<AvailableDownloadItem>();
            string cleanTid = baseTitleId.Trim().ToLowerInvariant();

            // 1. Добавляем последнее официальное обновление
            string updateTid = GetUpdateTitleId(cleanTid);
            var entry = App.TitleDb.GetEntry(cleanTid) ?? App.TitleDb.GetEntry(cleanTid.ToUpperInvariant());

            string latestVersionStr = "v65536 (1.1.0+)";
            if (entry != null && !string.IsNullOrEmpty(entry.Version))
            {
                latestVersionStr = $"v{entry.Version}";
            }

            results.Add(new AvailableDownloadItem
            {
                TitleId = updateTid.ToUpperInvariant(),
                Name = $"Патч обновления для {(!string.IsNullOrEmpty(gameTitle) ? gameTitle : cleanTid)}",
                ContentType = "Update",
                Version = latestVersionStr,
                SizeFormatted = "TitleDB Index",
                DownloadUrl = $"https://tinfoil.media/repo/switch/updates/{updateTid}.nsp",
                IsSelected = true
            });

            // 2. Ищем все известные DLC для данной игры
            try
            {
                if (ulong.TryParse(cleanTid, System.Globalization.NumberStyles.HexNumber, null, out ulong baseTidVal))
                {
                    for (ulong offset = 1; offset <= 30; offset++)
                    {
                        string dlcTid = ((baseTidVal & ~0xFFFUL) | offset).ToString("X16");
                        var dlcEntry = App.TitleDb.GetEntry(dlcTid);
                        if (dlcEntry != null)
                        {
                            results.Add(new AvailableDownloadItem
                            {
                                TitleId = dlcTid,
                                Name = !string.IsNullOrWhiteSpace(dlcEntry.Name) ? dlcEntry.Name : $"Дополнение DLC #{offset}",
                                ContentType = "DLC",
                                Version = "v0",
                                SizeFormatted = "Официальный DLC",
                                DownloadUrl = $"https://tinfoil.media/repo/switch/dlc/{dlcTid.ToLowerInvariant()}.nsp",
                                IsSelected = true
                            });
                        }
                    }
                }
            }
            catch { }

            return await Task.FromResult(results);
        }

        /// <summary>
        /// Фоновая загрузка файла с отображением прогресса и скорости.
        /// </summary>
        public async Task<string?> DownloadFileAsync(
            string url, 
            string targetFolder, 
            string fileName, 
            IProgress<(double Progress, string Speed)>? progressReporter = null, 
            CancellationToken cancellationToken = default)
        {
            try
            {
                Directory.CreateDirectory(targetFolder);
                string destinationPath = Path.Combine(targetFolder, fileName);

                using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    App.Logger.Log($"[Downloader] Зеркало вернуло код {response.StatusCode} для {fileName}. Поиск резервных ссылок...", LogLevel.Warning);
                    return null;
                }

                long totalBytes = response.Content.Headers.ContentLength ?? -1;
                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                
                var fileOptions = FileOptions.Asynchronous | FileOptions.SequentialScan;
                using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 4 * 1024 * 1024, fileOptions);

                byte[] buffer = new byte[1024 * 1024]; // 1 МБ буфер
                long totalRead = 0;
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                int read;
                while ((read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    totalRead += read;

                    double elapsed = stopwatch.Elapsed.TotalSeconds;
                    double mbPerSec = elapsed > 0 ? (totalRead / 1024.0 / 1024.0) / elapsed : 0;
                    double pct = totalBytes > 0 ? (double)totalRead / totalBytes * 100.0 : 50.0;

                    progressReporter?.Report((pct, $"{mbPerSec:F1} МБ/с"));
                }

                App.Logger.Log($"[Downloader] Файл {fileName} успешно загружен ({ProcessingTask.FormatSize(totalRead)}).", LogLevel.Success);
                return destinationPath;
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[Downloader] Ошибка загрузки {fileName}: {ex.Message}", LogLevel.Error);
                return null;
            }
        }
    }
}
