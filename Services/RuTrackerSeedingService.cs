#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using MonoTorrent;
using MonoTorrent.Client;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public sealed class RuTrackerSeedingAddRequest
    {
        public string InfoHash { get; set; } = string.Empty;
        public string DisplayTitle { get; set; } = string.Empty;
        public string TopicId { get; set; } = string.Empty;
        public string TopicTitle { get; set; } = string.Empty;
        public string ContentDir { get; set; } = string.Empty;
        public byte[]? TorrentBytes { get; set; }
        public string MagnetLink { get; set; } = string.Empty;
        public List<string> SelectedRelativePaths { get; set; } = new();
        public long TotalSelectedSizeBytes { get; set; }
    }

    public sealed class RuTrackerPersistedSeedingRecord
    {
        public string InfoHash { get; set; } = string.Empty;
        public string DisplayTitle { get; set; } = string.Empty;
        public string TopicId { get; set; } = string.Empty;
        public string TopicTitle { get; set; } = string.Empty;
        public string ContentDir { get; set; } = string.Empty;
        public string TorrentFilePath { get; set; } = string.Empty;
        public string MagnetLink { get; set; } = string.Empty;
        public List<string> SelectedRelativePaths { get; set; } = new();
        public List<int> SkipFileIndices { get; set; } = new();
        public long TotalSizeBytes { get; set; }
        public long TotalUploadedBytes { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.Now;
        public bool IsPaused { get; set; }
    }

    public sealed class RuTrackerSeedingItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        public string InfoHash { get; set; } = string.Empty;
        public string TopicId { get; set; } = string.Empty;
        public string TopicTitle { get; set; } = string.Empty;
        public string ContentDir { get; set; } = string.Empty;
        public string TorrentFilePath { get; set; } = string.Empty;
        public string MagnetLink { get; set; } = string.Empty;
        public List<string> SelectedRelativePaths { get; set; } = new();
        public List<int> SkipFileIndices { get; set; } = new();
        public long TotalSizeBytes { get; set; }
        public DateTime AddedAt { get; set; } = DateTime.Now;

        private string _displayTitle = string.Empty;
        public string DisplayTitle
        {
            get => _displayTitle;
            set
            {
                if (SetProperty(ref _displayTitle, value))
                    OnPropertyChanged(nameof(TooltipDetails));
            }
        }

        private bool _isPaused;
        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                if (SetProperty(ref _isPaused, value))
                {
                    OnPropertyChanged(nameof(PauseResumeGlyph));
                    OnPropertyChanged(nameof(PauseResumeTooltip));
                    OnPropertyChanged(nameof(SpeedAndPeersDisplay));
                    OnPropertyChanged(nameof(SpeedBrush));
                }
            }
        }

        private string _stageBadge = "ПРОВЕРКА";
        public string StageBadge
        {
            get => _stageBadge;
            set
            {
                if (SetProperty(ref _stageBadge, value))
                {
                    OnPropertyChanged(nameof(StageBadgeDisplay));
                    OnPropertyChanged(nameof(SpeedAndPeersDisplay));
                    OnPropertyChanged(nameof(SpeedBrush));
                }
            }
        }

        private SolidColorBrush? _stageBrush;
        public SolidColorBrush? StageBrush
        {
            get
            {
                if (_stageBrush == null && App.MainDispatcher?.HasThreadAccess != false)
                {
                    try { _stageBrush = new SolidColorBrush(RawStageColor); } catch { }
                }
                return _stageBrush;
            }
            set => SetProperty(ref _stageBrush, value);
        }

        private SolidColorBrush? _speedBrush;
        public SolidColorBrush? SpeedBrush
        {
            get
            {
                if (_speedBrush == null && App.MainDispatcher?.HasThreadAccess != false)
                {
                    try { _speedBrush = new SolidColorBrush(ResolveSpeedColor()); } catch { }
                }
                return _speedBrush;
            }
            set => SetProperty(ref _speedBrush, value);
        }

        private SolidColorBrush? _ratioBrush;
        public SolidColorBrush? RatioBrush
        {
            get
            {
                if (_ratioBrush == null && App.MainDispatcher?.HasThreadAccess != false)
                {
                    try { _ratioBrush = new SolidColorBrush(ResolveRatioColor()); } catch { }
                }
                return _ratioBrush;
            }
            set => SetProperty(ref _ratioBrush, value);
        }

        private double _uploadSpeedBps;
        public double UploadSpeedBps
        {
            get => _uploadSpeedBps;
            set
            {
                if (Math.Abs(_uploadSpeedBps - value) > 0.5)
                {
                    _uploadSpeedBps = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SpeedAndPeersDisplay));
                    OnPropertyChanged(nameof(SpeedBrush));
                }
            }
        }

        public long PersistedUploadedBytes;
        public bool IsLoadedInEngine;
        public long SessionUploadedBytes;
        public long LastSeenEngineSessionUp;

        private long _uiTotalUploadedBytes;
        public long TotalUploadedBytes => Math.Max(0L, PersistedUploadedBytes + SessionUploadedBytes);

        private int _connectedLeeches;
        public int ConnectedLeeches
        {
            get => _connectedLeeches;
            set
            {
                if (SetProperty(ref _connectedLeeches, value))
                    OnPropertyChanged(nameof(SpeedAndPeersDisplay));
            }
        }

        private int _connectedSeeds;
        public int ConnectedSeeds
        {
            get => _connectedSeeds;
            set
            {
                if (SetProperty(ref _connectedSeeds, value))
                    OnPropertyChanged(nameof(SpeedAndPeersDisplay));
            }
        }

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            set
            {
                if (SetProperty(ref _errorMessage, value))
                {
                    OnPropertyChanged(nameof(SpeedAndPeersDisplay));
                    OnPropertyChanged(nameof(TooltipDetails));
                }
            }
        }

        // Потокобезопасные raw-поля, обновляемые фоновым движком и сбрасываемые на UI-потоке
        public string RawStageBadge = "ПРОВЕРКА";
        public Windows.UI.Color RawStageColor = Windows.UI.Color.FromArgb(255, 14, 165, 233);
        public double RawUploadSpeedBps;
        public long RawSessionUploadedBytes;
        public int RawLeeches;
        public int RawSeeds;
        public int RawSwarm;
        public string RawErrorMessage = string.Empty;

        public string StageBadgeDisplay => App.Localization.TranslateText(StageBadge);

        public string SizeDisplay => FormatBytesStatic(TotalSizeBytes);
        public string SizeOnlyDisplay => SizeDisplay;

        public double Ratio => TotalSizeBytes > 0 ? (double)TotalUploadedBytes / TotalSizeBytes : 0.0;

        public string UploadedDisplay => $"{App.Localization.TranslateText("Отдано:")} {FormatBytesStatic(TotalUploadedBytes)}";
        public string UploadedOnlyDisplay => FormatBytesStatic(TotalUploadedBytes);

        public string RatioDisplay => $"{App.Localization.TranslateText("Рейтинг:")} {Ratio:F2}";
        public string RatioOnlyDisplay => $"{Ratio:F2}";

        public string UploadSpeedOnlyDisplay
        {
            get
            {
                if (IsPaused || StageBadge == "ПАУЗА")
                    return App.Localization.TranslateText("На паузе");
                if (StageBadge == "НЕТ ФАЙЛОВ")
                    return App.Localization.TranslateText("Нет файлов");
                if (StageBadge == "ОШИБКА")
                    return App.Localization.TranslateText("Ошибка");
                if (StageBadge == "ПРОВЕРКА")
                    return App.Localization.TranslateText("Проверка...");
                return FormatSpeedStatic(UploadSpeedBps);
            }
        }

        public string PeersSeedsDisplay => StageBadge == "РАЗДАЧА"
            ? $"{ConnectedLeeches} / {ConnectedSeeds}"
            : "-";

        public string PeersSeedsTooltip => StageBadge == "РАЗДАЧА"
            ? $"{App.Localization.TranslateText("Личи:")} {ConnectedLeeches} | {App.Localization.TranslateText("Сиды:")} {ConnectedSeeds}"
            : SpeedAndPeersDisplay;

        public string PauseResumeGlyph => IsPaused ? "\uE768" : "\uE769";

        public string PauseResumeTooltip => App.Localization.TranslateText(
            IsPaused ? "Возобновить раздачу" : "Приостановить раздачу");

        public string SpeedAndPeersDisplay
        {
            get
            {
                if (IsPaused || StageBadge == "ПАУЗА")
                    return App.Localization.TranslateText("На паузе");
                if (StageBadge == "НЕТ ФАЙЛОВ")
                    return App.Localization.TranslateText("Исходные файлы не найдены");
                if (StageBadge == "ОШИБКА")
                    return string.IsNullOrWhiteSpace(ErrorMessage)
                        ? App.Localization.TranslateText("Ошибка раздачи")
                        : ErrorMessage;
                if (StageBadge == "ПРОВЕРКА")
                    return App.Localization.TranslateText("Проверка хеша файлов...");

                string speedStr = FormatSpeedStatic(UploadSpeedBps);
                string lLbl = App.Localization.TranslateText("Личи:");
                string sLbl = App.Localization.TranslateText("Сиды:");
                return $"{speedStr} • {lLbl} {ConnectedLeeches} • {sLbl} {ConnectedSeeds}";
            }
        }

        public string TooltipDetails
        {
            get
            {
                string sb = $"{DisplayTitle}\n" +
                            $"{UploadedDisplay} ({RatioDisplay})\n" +
                            $"{ContentDir}\n" +
                            $"Hash: {InfoHash} | {AddedAt:dd.MM.yyyy HH:mm}";
                if (!string.IsNullOrWhiteSpace(ErrorMessage))
                    sb += $"\n{ErrorMessage}";
                return sb;
            }
        }

        private Windows.UI.Color ResolveSpeedColor()
        {
            if (IsPaused || StageBadge == "ПАУЗА")
                return Windows.UI.Color.FromArgb(255, 251, 191, 36); // Amber
            if (StageBadge is "ОШИБКА" or "НЕТ ФАЙЛОВ")
                return Windows.UI.Color.FromArgb(255, 248, 113, 113); // Red
            if (StageBadge == "ПРОВЕРКА")
                return Windows.UI.Color.FromArgb(255, 56, 189, 248); // Sky
            return UploadSpeedBps >= 1024
                ? Windows.UI.Color.FromArgb(255, 34, 197, 94) // Green
                : Windows.UI.Color.FromArgb(255, 148, 163, 184); // Slate
        }

        private Windows.UI.Color ResolveRatioColor()
        {
            double r = Ratio;
            if (r >= 1.0) return Windows.UI.Color.FromArgb(255, 34, 197, 94);
            if (r > 0.01) return Windows.UI.Color.FromArgb(255, 56, 189, 248);
            return Windows.UI.Color.FromArgb(255, 148, 163, 184);
        }

        public void CommitUiUpdates()
        {
            var dq = App.MainDispatcher;
            if (dq != null && !dq.HasThreadAccess)
            {
                dq.TryEnqueue(() =>
                {
                    try { CommitUiUpdates(); } catch { }
                });
                return;
            }

            if (StageBadge != RawStageBadge && !string.IsNullOrEmpty(RawStageBadge))
                StageBadge = RawStageBadge;

            if (_stageBrush == null || _stageBrush.Color != RawStageColor)
                StageBrush = new SolidColorBrush(RawStageColor);

            SessionUploadedBytes = RawSessionUploadedBytes;
            long totalUpNow = TotalUploadedBytes;
            bool uploadedChanged = _uiTotalUploadedBytes != totalUpNow;
            if (uploadedChanged)
            {
                _uiTotalUploadedBytes = totalUpNow;
                OnPropertyChanged(nameof(TotalUploadedBytes));
                OnPropertyChanged(nameof(UploadedDisplay));
                OnPropertyChanged(nameof(UploadedOnlyDisplay));
                OnPropertyChanged(nameof(Ratio));
                OnPropertyChanged(nameof(RatioDisplay));
                OnPropertyChanged(nameof(RatioOnlyDisplay));
                OnPropertyChanged(nameof(TooltipDetails));
            }

            var targetRatioColor = ResolveRatioColor();
            if (_ratioBrush == null || _ratioBrush.Color != targetRatioColor)
                RatioBrush = new SolidColorBrush(targetRatioColor);

            if (Math.Abs(UploadSpeedBps - RawUploadSpeedBps) > 0.5)
                UploadSpeedBps = RawUploadSpeedBps;

            if (ConnectedLeeches != RawLeeches)
                ConnectedLeeches = RawLeeches;
            if (ConnectedSeeds != RawSeeds)
                ConnectedSeeds = RawSeeds;
            if (ErrorMessage != RawErrorMessage)
                ErrorMessage = RawErrorMessage;

            var targetSpeedColor = ResolveSpeedColor();
            if (_speedBrush == null || _speedBrush.Color != targetSpeedColor)
                SpeedBrush = new SolidColorBrush(targetSpeedColor);

            OnPropertyChanged(nameof(SpeedAndPeersDisplay));
            OnPropertyChanged(nameof(UploadSpeedOnlyDisplay));
            OnPropertyChanged(nameof(PeersSeedsDisplay));
            OnPropertyChanged(nameof(PeersSeedsTooltip));
        }

        public void NotifyLocalizationChanged()
        {
            OnPropertyChanged(nameof(StageBadgeDisplay));
            OnPropertyChanged(nameof(SizeOnlyDisplay));
            OnPropertyChanged(nameof(UploadedDisplay));
            OnPropertyChanged(nameof(UploadedOnlyDisplay));
            OnPropertyChanged(nameof(RatioDisplay));
            OnPropertyChanged(nameof(RatioOnlyDisplay));
            OnPropertyChanged(nameof(SpeedAndPeersDisplay));
            OnPropertyChanged(nameof(UploadSpeedOnlyDisplay));
            OnPropertyChanged(nameof(PeersSeedsDisplay));
            OnPropertyChanged(nameof(PeersSeedsTooltip));
            OnPropertyChanged(nameof(PauseResumeTooltip));
            OnPropertyChanged(nameof(TooltipDetails));
        }

        protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            var dq = App.MainDispatcher;
            if (dq != null && !dq.HasThreadAccess)
            {
                dq.TryEnqueue(() =>
                {
                    try { base.OnPropertyChanged(e); } catch { }
                });
                return;
            }
            try { base.OnPropertyChanged(e); } catch { }
        }

        public static string FormatBytesStatic(long bytes)
        {
            if (bytes <= 0) return "0 МБ";
            if (bytes >= 1024L * 1024L * 1024L)
                return $"{bytes / (1024.0 * 1024.0 * 1024.0):F2} ГБ";
            if (bytes >= 1024L * 1024L)
                return $"{bytes / (1024.0 * 1024.0):F1} МБ";
            return $"{Math.Max(1.0, bytes / 1024.0):F0} КБ";
        }

        public static string FormatSpeedStatic(double bps)
        {
            if (bps >= 1024.0 * 1024.0)
                return $"{bps / (1024.0 * 1024.0):F2} МБ/с";
            if (bps >= 1024.0)
                return $"{bps / 1024.0:F0} КБ/с";
            return $"{0.0:F2} МБ/с";
        }
    }

    public readonly record struct RuTrackerSeedingSummarySnapshot(
        int TotalCount,
        int ActiveSeedingCount,
        double TotalUploadBps,
        long TotalUploadedBytes,
        int TotalLeeches,
        int TotalSeeds);

    public sealed class RuTrackerSeedingService
    {
        public static RuTrackerSeedingService Instance { get; } = new();

        public ObservableCollection<RuTrackerSeedingItem> Items { get; } = new();
        private readonly List<RuTrackerSeedingItem> _backingItems = new();
        private readonly object _syncLock = new();
        private readonly string _appDataDir;
        private readonly string _stateFilePath;
        private readonly string _resumeDir;
        private readonly string _dhtStatePath;
        private readonly string _fallbackCacheDir;
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

        private readonly SemaphoreSlim _daemonCmdLock = new(1, 1);
        private Process? _daemonProcess;
        private StreamWriter? _daemonStdin;
        private Task? _daemonReadTask;
        private bool _usingFallbackEngine;
        private ClientEngine? _mtFallbackEngine;
        private readonly Dictionary<string, TorrentManager> _mtManagers = new(StringComparer.OrdinalIgnoreCase);

        private int _loadedOnce;
        private volatile bool _isShuttingDown;
        private volatile bool _statsDirty;
        private DateTime _lastPersistAtUtc = DateTime.MinValue;
        private DateTime _lastDaemonLaunchAttemptUtc = DateTime.MinValue;

        private RuTrackerSeedingService()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _appDataDir = Path.Combine(localAppData, "StormSwitchBox");
            Directory.CreateDirectory(_appDataDir);
            _stateFilePath = Path.Combine(_appDataDir, "rutracker_seeding_state.json");
            _resumeDir = Path.Combine(_appDataDir, "seeding_resume");
            _dhtStatePath = Path.Combine(_appDataDir, "seeding_dht.state");
            _fallbackCacheDir = Path.Combine(_appDataDir, "seeding_mt_cache");

            App.Localization.LanguageChanged += () =>
            {
                RunOnUiSyncOrAsync(() =>
                {
                    RuTrackerSeedingItem[] snapshot;
                    lock (_syncLock) { snapshot = _backingItems.ToArray(); }
                    foreach (var it in snapshot)
                        it.NotifyLocalizationChanged();
                });
            };
        }

        private static void RunOnUiSyncOrAsync(Action action)
        {
            try
            {
                var dq = App.MainDispatcher;
                if (dq != null)
                {
                    if (dq.HasThreadAccess) action();
                    else dq.TryEnqueue(() => { try { action(); } catch { } });
                }
                else
                {
                    action();
                }
            }
            catch { }
        }

        public async Task LoadAndResumePersistedAsync()
        {
            if (Interlocked.Exchange(ref _loadedOnce, 1) != 0)
                return;

            try
            {
                if (!File.Exists(_stateFilePath))
                    return;

                string json = await File.ReadAllTextAsync(_stateFilePath).ConfigureAwait(false);
                var records = JsonSerializer.Deserialize<List<RuTrackerPersistedSeedingRecord>>(json);
                if (records == null || records.Count == 0)
                    return;

                var toStart = new List<RuTrackerSeedingItem>();
                foreach (var r in records)
                {
                    string hash = (r.InfoHash ?? string.Empty).Trim().ToUpperInvariant();
                    if (hash.Length != 40 || string.IsNullOrWhiteSpace(r.ContentDir))
                        continue;

                    string torrentPath = r.TorrentFilePath ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(torrentPath) || !File.Exists(torrentPath))
                    {
                        var cached = RuTrackerModService.TryResolveCachedTorrentBytes(hash);
                        if (cached != null)
                            torrentPath = RuTrackerModService.GetCachedTorrentFilePath(hash);
                    }

                    bool dirExists = Directory.Exists(r.ContentDir);
                    long computedSize = r.TotalSizeBytes > 0
                        ? r.TotalSizeBytes
                        : (dirExists ? ComputeDirectoryOrSelectedSize(r.ContentDir, r.SelectedRelativePaths) : 0);

                    var item = new RuTrackerSeedingItem
                    {
                        InfoHash = hash,
                        DisplayTitle = !string.IsNullOrWhiteSpace(r.DisplayTitle) ? r.DisplayTitle : (r.TopicTitle ?? hash),
                        TopicId = r.TopicId ?? string.Empty,
                        TopicTitle = r.TopicTitle ?? string.Empty,
                        ContentDir = r.ContentDir,
                        TorrentFilePath = torrentPath,
                        MagnetLink = r.MagnetLink ?? string.Empty,
                        SelectedRelativePaths = r.SelectedRelativePaths ?? new List<string>(),
                        SkipFileIndices = r.SkipFileIndices ?? new List<int>(),
                        TotalSizeBytes = computedSize,
                        PersistedUploadedBytes = Math.Max(0L, r.TotalUploadedBytes),
                        SessionUploadedBytes = 0,
                        AddedAt = r.AddedAt,
                        IsPaused = r.IsPaused
                    };

                    if (!dirExists)
                    {
                        item.RawStageBadge = "НЕТ ФАЙЛОВ";
                        item.RawStageColor = Windows.UI.Color.FromArgb(255, 220, 38, 38);
                        item.StageBadge = item.RawStageBadge;
                    }
                    else if (item.IsPaused)
                    {
                        item.RawStageBadge = "ПАУЗА";
                        item.RawStageColor = Windows.UI.Color.FromArgb(255, 217, 119, 6);
                        item.StageBadge = item.RawStageBadge;
                    }
                    else
                    {
                        item.RawStageBadge = "ПРОВЕРКА";
                        item.RawStageColor = Windows.UI.Color.FromArgb(255, 14, 165, 233);
                        item.StageBadge = item.RawStageBadge;
                        toStart.Add(item);
                    }

                    lock (_syncLock)
                    {
                        if (_backingItems.Any(x => string.Equals(x.InfoHash, hash, StringComparison.OrdinalIgnoreCase)))
                            continue;
                        _backingItems.Add(item);
                    }

                    RunOnUiSyncOrAsync(() =>
                    {
                        if (!Items.Contains(item))
                            Items.Add(item);
                        item.CommitUiUpdates();
                    });
                }

                if (toStart.Count > 0)
                {
                    await DispatchAddToEngineAsync(toStart).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                App.Logger?.Log($"[RuTracker Seeding] Ошибка загрузки сохранённых раздач: {ex.Message}", LogLevel.Warning);
            }
        }

        public async Task<RuTrackerSeedingItem?> AddOrUpdateSeedingAsync(RuTrackerSeedingAddRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.ContentDir) || !Directory.Exists(req.ContentDir))
                return null;

            string infoHash = (req.InfoHash ?? string.Empty).Trim().ToUpperInvariant();
            byte[]? torrentBytes = req.TorrentBytes;
            string magnet = req.MagnetLink ?? string.Empty;

            if (torrentBytes != null && Torrent.TryLoad(torrentBytes, out var tParsed) && tParsed != null)
            {
                infoHash = tParsed.InfoHashes.V1OrV2.ToHex().ToUpperInvariant();
            }

            if (infoHash.Length != 40 && !string.IsNullOrWhiteSpace(magnet) && MagnetLink.TryParse(magnet, out var magParsed))
            {
                infoHash = magParsed.InfoHashes.V1OrV2.ToHex().ToUpperInvariant();
            }

            if (infoHash.Length != 40 && !string.IsNullOrWhiteSpace(req.TopicId))
            {
                var (resolvedHash, resolvedMag) = RuTrackerModService.TryResolveTopicHashAndMagnet(req.TopicId);
                if (!string.IsNullOrWhiteSpace(resolvedHash))
                    infoHash = resolvedHash;
                if (string.IsNullOrWhiteSpace(magnet) && !string.IsNullOrWhiteSpace(resolvedMag))
                    magnet = resolvedMag;
            }

            if (infoHash.Length != 40)
            {
                App.Logger?.Log($"[RuTracker Seeding] Не удалось определить InfoHash для раздачи '{req.DisplayTitle}', сидирование пропущено.", LogLevel.Warning);
                return null;
            }

            string? torrentFilePath = null;
            if (torrentBytes != null && torrentBytes.Length > 64)
            {
                torrentFilePath = RuTrackerModService.TrySaveCachedTorrentBytes(infoHash, torrentBytes);
            }
            if (string.IsNullOrWhiteSpace(torrentFilePath) || !File.Exists(torrentFilePath))
            {
                torrentBytes ??= RuTrackerModService.TryResolveCachedTorrentBytes(infoHash);
                if (torrentBytes != null && torrentBytes.Length > 64)
                    torrentFilePath = RuTrackerModService.TrySaveCachedTorrentBytes(infoHash, torrentBytes);
            }

            if (string.IsNullOrWhiteSpace(magnet))
            {
                magnet = $"magnet:?xt=urn:btih:{infoHash.ToLowerInvariant()}&dn={Uri.EscapeDataString(req.DisplayTitle ?? infoHash)}";
            }

            var selectedRels = (req.SelectedRelativePaths ?? new List<string>())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var skipIndices = ComputeSkipFileIndices(torrentBytes, selectedRels);
            long sizeBytes = ComputeSelectedOrDiskSize(req.ContentDir, torrentBytes, selectedRels, req.TotalSelectedSizeBytes);

            RuTrackerSeedingItem target;
            lock (_syncLock)
            {
                var existing = _backingItems.FirstOrDefault(x => string.Equals(x.InfoHash, infoHash, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    // Свернём текущий счётчик сеанса перед перезапуском
                    existing.PersistedUploadedBytes += Math.Max(0L, existing.SessionUploadedBytes);
                    existing.SessionUploadedBytes = 0;
                    existing.RawSessionUploadedBytes = 0;
                    existing.LastSeenEngineSessionUp = 0;
                    existing.DisplayTitle = !string.IsNullOrWhiteSpace(req.DisplayTitle) ? req.DisplayTitle : existing.DisplayTitle;
                    existing.TopicId = !string.IsNullOrWhiteSpace(req.TopicId) ? req.TopicId : existing.TopicId;
                    existing.TopicTitle = !string.IsNullOrWhiteSpace(req.TopicTitle) ? req.TopicTitle : existing.TopicTitle;
                    existing.ContentDir = req.ContentDir;
                    existing.TorrentFilePath = torrentFilePath ?? existing.TorrentFilePath;
                    existing.MagnetLink = magnet;
                    if (selectedRels.Count > 0)
                    {
                        existing.SelectedRelativePaths = selectedRels;
                        existing.SkipFileIndices = skipIndices;
                    }
                    if (sizeBytes > 0)
                        existing.TotalSizeBytes = sizeBytes;
                    existing.IsPaused = false;
                    existing.RawStageBadge = "ПРОВЕРКА";
                    existing.RawStageColor = Windows.UI.Color.FromArgb(255, 14, 165, 233);
                    existing.RawErrorMessage = string.Empty;
                    target = existing;
                }
                else
                {
                    target = new RuTrackerSeedingItem
                    {
                        InfoHash = infoHash,
                        DisplayTitle = !string.IsNullOrWhiteSpace(req.DisplayTitle) ? req.DisplayTitle : (req.TopicTitle ?? infoHash),
                        TopicId = req.TopicId ?? string.Empty,
                        TopicTitle = req.TopicTitle ?? string.Empty,
                        ContentDir = req.ContentDir,
                        TorrentFilePath = torrentFilePath ?? string.Empty,
                        MagnetLink = magnet,
                        SelectedRelativePaths = selectedRels,
                        SkipFileIndices = skipIndices,
                        TotalSizeBytes = sizeBytes,
                        AddedAt = DateTime.Now,
                        IsPaused = false,
                        RawStageBadge = "ПРОВЕРКА",
                        RawStageColor = Windows.UI.Color.FromArgb(255, 14, 165, 233)
                    };
                    target.StageBadge = target.RawStageBadge;
                    _backingItems.Insert(0, target);
                }
            }

            RunOnUiSyncOrAsync(() =>
            {
                if (!Items.Contains(target))
                    Items.Insert(0, target);
                target.CommitUiUpdates();
            });

            SaveState();
            await DispatchAddToEngineAsync(new[] { target }).ConfigureAwait(false);
            App.Logger?.Log($"[RuTracker Seeding] Раздача добавлена на сидинг: {target.DisplayTitle} ({target.InfoHash})", LogLevel.Info);
            return target;
        }

        private static List<int> ComputeSkipFileIndices(byte[]? torrentBytes, List<string> selectedRels)
        {
            var skip = new List<int>();
            if (torrentBytes == null || selectedRels == null || selectedRels.Count == 0)
                return skip;

            try
            {
                if (!Torrent.TryLoad(torrentBytes, out var parsed) || parsed?.Files == null || parsed.Files.Count == 0)
                    return skip;

                int matchedCount = 0;
                for (int fi = 0; fi < parsed.Files.Count; fi++)
                {
                    string fNorm = parsed.Files[fi].Path.Replace('\\', '/').TrimStart('.', '/');
                    string fName = Path.GetFileName(fNorm);

                    bool isSelected = selectedRels.Any(s =>
                    {
                        string sNorm = s.Replace('\\', '/').TrimStart('.', '/');
                        if (sNorm.Equals(fNorm, StringComparison.OrdinalIgnoreCase) ||
                            sNorm.EndsWith("/" + fNorm, StringComparison.OrdinalIgnoreCase) ||
                            fNorm.EndsWith("/" + sNorm, StringComparison.OrdinalIgnoreCase))
                            return true;
                        return Path.GetFileName(sNorm).Equals(fName, StringComparison.OrdinalIgnoreCase);
                    });

                    if (isSelected)
                        matchedCount++;
                    else
                        skip.Add(fi);
                }

                // Защита: если ни один путь не сматчился, не блокируем всю раздачу целиком
                if (matchedCount == 0)
                    skip.Clear();
            }
            catch { }

            return skip;
        }

        private static long ComputeSelectedOrDiskSize(string contentDir, byte[]? torrentBytes, List<string> selectedRels, long explicitHint)
        {
            if (explicitHint > 0)
                return explicitHint;

            if (torrentBytes != null && Torrent.TryLoad(torrentBytes, out var parsed) && parsed?.Files != null && parsed.Files.Count > 0)
            {
                var skip = new HashSet<int>(ComputeSkipFileIndices(torrentBytes, selectedRels));
                long sum = 0;
                for (int i = 0; i < parsed.Files.Count; i++)
                {
                    if (!skip.Contains(i))
                        sum += parsed.Files[i].Length;
                }
                if (sum > 0) return sum;
            }

            return ComputeDirectoryOrSelectedSize(contentDir, selectedRels);
        }

        private static long ComputeDirectoryOrSelectedSize(string contentDir, List<string>? selectedRels)
        {
            try
            {
                if (!Directory.Exists(contentDir))
                    return 0;

                if (selectedRels != null && selectedRels.Count > 0)
                {
                    var selectedNames = new HashSet<string>(selectedRels.Select(Path.GetFileName).Where(n => !string.IsNullOrEmpty(n))!, StringComparer.OrdinalIgnoreCase);
                    long sum = 0;
                    foreach (var file in Directory.EnumerateFiles(contentDir, "*.*", SearchOption.AllDirectories))
                    {
                        if (selectedNames.Contains(Path.GetFileName(file)))
                        {
                            try { sum += new FileInfo(file).Length; } catch { }
                        }
                    }
                    if (sum > 0) return sum;
                }

                return Directory.EnumerateFiles(contentDir, "*.*", SearchOption.AllDirectories)
                    .Sum(f => { try { return new FileInfo(f).Length; } catch { return 0L; } });
            }
            catch
            {
                return 0;
            }
        }

        public void TogglePauseItem(RuTrackerSeedingItem item)
        {
            if (item == null) return;
            bool pauseNow = !item.IsPaused;
            item.IsPaused = pauseNow;
            if (pauseNow)
            {
                item.RawStageBadge = "ПАУЗА";
                item.RawStageColor = Windows.UI.Color.FromArgb(255, 217, 119, 6);
                item.RawUploadSpeedBps = 0;
                item.RawLeeches = 0;
                item.RawSeeds = 0;
            }
            else if (!Directory.Exists(item.ContentDir))
            {
                item.IsPaused = false;
                item.RawStageBadge = "НЕТ ФАЙЛОВ";
                item.RawStageColor = Windows.UI.Color.FromArgb(255, 220, 38, 38);
                item.CommitUiUpdates();
                return;
            }
            else
            {
                item.RawStageBadge = "ПРОВЕРКА";
                item.RawStageColor = Windows.UI.Color.FromArgb(255, 14, 165, 233);
            }

            item.CommitUiUpdates();
            SaveState();

            string hashLower = item.InfoHash.ToLowerInvariant();
            _ = Task.Run(async () =>
            {
                if (_usingFallbackEngine && _mtManagers.TryGetValue(hashLower, out var mgr))
                {
                    try
                    {
                        if (pauseNow) await mgr.PauseAsync().ConfigureAwait(false);
                        else await mgr.StartAsync().ConfigureAwait(false);
                    }
                    catch { }
                    return;
                }

                if (!pauseNow)
                {
                    if (!item.IsLoadedInEngine || _daemonProcess == null || _daemonProcess.HasExited)
                        await DispatchAddToEngineAsync(new[] { item }).ConfigureAwait(false);
                    else
                        await SendCommandAsync(new { cmd = "resume", hash = hashLower }).ConfigureAwait(false);
                }
                else
                {
                    await SendCommandAsync(new { cmd = "pause", hash = hashLower }).ConfigureAwait(false);
                }
            });
        }

        public void PauseAll()
        {
            RuTrackerSeedingItem[] snap;
            lock (_syncLock) { snap = _backingItems.ToArray(); }
            foreach (var it in snap)
            {
                if (!it.IsPaused)
                    TogglePauseItem(it);
            }
        }

        public void ResumeAll()
        {
            RuTrackerSeedingItem[] snap;
            lock (_syncLock) { snap = _backingItems.ToArray(); }
            foreach (var it in snap)
            {
                if (it.IsPaused)
                    TogglePauseItem(it);
            }
        }

        public void ReannounceItem(RuTrackerSeedingItem item)
        {
            if (item == null) return;
            if (!Directory.Exists(item.ContentDir))
            {
                item.RawStageBadge = "НЕТ ФАЙЛОВ";
                item.RawStageColor = Windows.UI.Color.FromArgb(255, 220, 38, 38);
                item.CommitUiUpdates();
                return;
            }
            string hashLower = item.InfoHash.ToLowerInvariant();
            _ = Task.Run(async () =>
            {
                if (item.IsPaused) return;
                if (_usingFallbackEngine && _mtManagers.TryGetValue(hashLower, out var mgr))
                {
                    try { await mgr.TrackerManager.AnnounceAsync(CancellationToken.None).ConfigureAwait(false); } catch { }
                    return;
                }
                if (!item.IsLoadedInEngine || _daemonProcess == null || _daemonProcess.HasExited)
                    await DispatchAddToEngineAsync(new[] { item }).ConfigureAwait(false);
                await SendCommandAsync(new { cmd = "reannounce", hash = hashLower }).ConfigureAwait(false);
            });
        }

        public static void OpenFolder(RuTrackerSeedingItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ContentDir))
                return;
            try
            {
                if (Directory.Exists(item.ContentDir))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{item.ContentDir}\"",
                        UseShellExecute = true
                    });
                }
            }
            catch { }
        }

        public void RemoveItem(RuTrackerSeedingItem item)
        {
            if (item == null) return;
            item.IsLoadedInEngine = false;
            lock (_syncLock)
            {
                _backingItems.Remove(item);
            }
            RunOnUiSyncOrAsync(() =>
            {
                Items.Remove(item);
            });
            SaveState();

            string hashLower = item.InfoHash.ToLowerInvariant();
            _ = Task.Run(async () =>
            {
                await SendCommandAsync(new { cmd = "remove", hash = hashLower }).ConfigureAwait(false);
                if (_usingFallbackEngine && _mtFallbackEngine != null && _mtManagers.TryGetValue(hashLower, out var mgr))
                {
                    _mtManagers.Remove(hashLower);
                    try { await mgr.StopAsync().ConfigureAwait(false); } catch { }
                    try { await _mtFallbackEngine.RemoveAsync(mgr).ConfigureAwait(false); } catch { }
                }
            });
        }

        public RuTrackerSeedingSummarySnapshot GetSummarySnapshot()
        {
            RuTrackerSeedingItem[] snap;
            lock (_syncLock) { snap = _backingItems.ToArray(); }

            int total = snap.Length;
            int active = 0;
            double totalUpBps = 0;
            long totalUploaded = 0;
            int leeches = 0;
            int seeds = 0;

            foreach (var it in snap)
            {
                totalUploaded += it.TotalUploadedBytes;
                if (!it.IsPaused && it.StageBadge is "РАЗДАЧА" or "ПРОВЕРКА")
                {
                    active++;
                    totalUpBps += Math.Max(0, it.RawUploadSpeedBps);
                    leeches += Math.Max(0, it.RawLeeches);
                    seeds += Math.Max(0, it.RawSeeds);
                }
            }

            return new RuTrackerSeedingSummarySnapshot(total, active, totalUpBps, totalUploaded, leeches, seeds);
        }

        public void CommitAllUiUpdates()
        {
            if (_usingFallbackEngine)
                PollFallbackEngineStats();

            RuTrackerSeedingItem[] snap;
            lock (_syncLock) { snap = _backingItems.ToArray(); }
            foreach (var it in snap)
                it.CommitUiUpdates();

            if (_statsDirty && (DateTime.UtcNow - _lastPersistAtUtc).TotalSeconds >= 30.0)
            {
                _statsDirty = false;
                SaveState();
            }
        }

        private async Task DispatchAddToEngineAsync(IEnumerable<RuTrackerSeedingItem> items)
        {
            var list = items.Where(i => i != null && !i.IsPaused && Directory.Exists(i.ContentDir)).ToList();
            if (list.Count == 0 || _isShuttingDown)
                return;

            if (!_usingFallbackEngine && await EnsureDaemonStartedAsync().ConfigureAwait(false))
            {
                foreach (var it in list)
                {
                    it.IsLoadedInEngine = true;
                    await SendCommandAsync(new
                    {
                        cmd = "add",
                        hash = it.InfoHash.ToLowerInvariant(),
                        torrent_path = it.TorrentFilePath ?? string.Empty,
                        magnet = it.MagnetLink ?? string.Empty,
                        save_dir = it.ContentDir,
                        skip_indices = it.SkipFileIndices ?? new List<int>(),
                        paused = it.IsPaused
                    }).ConfigureAwait(false);
                }
                return;
            }

            _usingFallbackEngine = true;
            await AddToFallbackEngineAsync(list).ConfigureAwait(false);
        }

        private async Task<bool> EnsureDaemonStartedAsync()
        {
            if (_isShuttingDown || _usingFallbackEngine)
                return false;

            if (_daemonProcess != null && !_daemonProcess.HasExited && _daemonStdin != null)
                return true;

            await _daemonCmdLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_daemonProcess != null && !_daemonProcess.HasExited && _daemonStdin != null)
                    return true;

                string? pyExe = StormToolResolver.ResolvePythonExe();
                string? scriptPath = StormToolResolver.ResolveLtTurboScript();
                if (string.IsNullOrEmpty(pyExe) || !File.Exists(pyExe) || string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
                {
                    _usingFallbackEngine = true;
                    return false;
                }

                Directory.CreateDirectory(_resumeDir);
                int port = RuTrackerModService.FindFreeHighListenPort(44880, 90);
                var psi = new ProcessStartInfo
                {
                    FileName = pyExe,
                    Arguments = $"\"{scriptPath}\" --seed-daemon --listen-port {port} --parent-pid {Environment.ProcessId} --resume-dir \"{_resumeDir}\" --dht-state \"{_dhtStatePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                psi.Environment["PYTHONUTF8"] = "1";
                psi.Environment["PYTHONIOENCODING"] = "utf-8";
                psi.Environment["PYTHONUNBUFFERED"] = "1";

                var proc = Process.Start(psi);
                if (proc == null)
                {
                    _usingFallbackEngine = true;
                    return false;
                }

                JobObjectManager.AddProcess(proc);
                _daemonProcess = proc;
                _daemonStdin = new StreamWriter(proc.StandardInput.BaseStream, new UTF8Encoding(false)) { AutoFlush = true };
                _daemonReadTask = Task.Run(() => ReadDaemonStdoutLoop(proc));
                _ = Task.Run(async () =>
                {
                    try { await proc.StandardError.ReadToEndAsync().ConfigureAwait(false); } catch { }
                });
                return true;
            }
            catch (Exception ex)
            {
                App.Logger?.Log($"[RuTracker Seeding] Переключение на MonoTorrent fallback: {ex.Message}", LogLevel.Warning);
                _usingFallbackEngine = true;
                return false;
            }
            finally
            {
                _daemonCmdLock.Release();
            }
        }

        private async Task ReadDaemonStdoutLoop(Process proc)
        {
            try
            {
                string? line;
                while ((line = await proc.StandardOutput.ReadLineAsync().ConfigureAwait(false)) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    try
                    {
                        using var doc = JsonDocument.Parse(line);
                        var root = doc.RootElement;
                        string ev = root.TryGetProperty("event", out var evEl) ? (evEl.GetString() ?? "") : "";
                        if (ev == "stats" && root.TryGetProperty("torrents", out var torsEl) && torsEl.ValueKind == JsonValueKind.Object)
                        {
                            RuTrackerSeedingItem[] snap;
                            lock (_syncLock) { snap = _backingItems.ToArray(); }
                            foreach (var it in snap)
                            {
                                string ihLower = it.InfoHash.ToLowerInvariant();
                                if (!torsEl.TryGetProperty(ihLower, out var tEl))
                                    continue;

                                string state = tEl.TryGetProperty("state", out var stEl) ? (stEl.GetString() ?? "") : "";
                                double upBps = tEl.TryGetProperty("up_bps", out var upEl) ? upEl.GetDouble() : 0;
                                long sessionUp = tEl.TryGetProperty("session_up", out var suEl) ? suEl.GetInt64() : 0;
                                int leechs = tEl.TryGetProperty("leechs", out var leEl) ? leEl.GetInt32() : 0;
                                int seeds = tEl.TryGetProperty("seeds", out var seEl) ? seEl.GetInt32() : 0;
                                int swarm = tEl.TryGetProperty("swarm", out var swEl) ? swEl.GetInt32() : 0;
                                string err = tEl.TryGetProperty("error", out var erEl) ? (erEl.GetString() ?? "") : "";

                                if (sessionUp < it.LastSeenEngineSessionUp)
                                {
                                    it.PersistedUploadedBytes += it.LastSeenEngineSessionUp;
                                    _statsDirty = true;
                                }
                                else if (sessionUp > it.LastSeenEngineSessionUp)
                                {
                                    _statsDirty = true;
                                }
                                it.LastSeenEngineSessionUp = sessionUp;
                                it.RawSessionUploadedBytes = sessionUp;
                                it.RawUploadSpeedBps = upBps;
                                it.RawLeeches = leechs;
                                it.RawSeeds = seeds;
                                it.RawSwarm = swarm;
                                it.RawErrorMessage = err;

                                if (state == "paused" || it.IsPaused)
                                {
                                    it.RawStageBadge = "ПАУЗА";
                                    it.RawStageColor = Windows.UI.Color.FromArgb(255, 217, 119, 6);
                                }
                                else if (state == "missing_files")
                                {
                                    it.RawStageBadge = "НЕТ ФАЙЛОВ";
                                    it.RawStageColor = Windows.UI.Color.FromArgb(255, 220, 38, 38);
                                }
                                else if (state == "error")
                                {
                                    it.RawStageBadge = "ОШИБКА";
                                    it.RawStageColor = Windows.UI.Color.FromArgb(255, 239, 68, 68);
                                }
                                else if (state is "checking" or "metadata")
                                {
                                    it.RawStageBadge = "ПРОВЕРКА";
                                    it.RawStageColor = Windows.UI.Color.FromArgb(255, 14, 165, 233);
                                }
                                else
                                {
                                    it.RawStageBadge = "РАЗДАЧА";
                                    it.RawStageColor = Windows.UI.Color.FromArgb(255, 16, 185, 129);
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            if (!_isShuttingDown)
            {
                // Неожиданный выход демона: свернём статистику и перезапустим для активных раздач (не чаще раза в 5 сек)
                RuTrackerSeedingItem[] snap;
                lock (_syncLock) { snap = _backingItems.ToArray(); }
                foreach (var it in snap)
                {
                    it.IsLoadedInEngine = false;
                    if (it.LastSeenEngineSessionUp > 0)
                    {
                        it.PersistedUploadedBytes += it.LastSeenEngineSessionUp;
                        it.LastSeenEngineSessionUp = 0;
                        it.RawSessionUploadedBytes = 0;
                    }
                }
                SaveState();

                if (snap.Any(x => !x.IsPaused && Directory.Exists(x.ContentDir)) &&
                    (DateTime.UtcNow - _lastDaemonLaunchAttemptUtc).TotalSeconds >= 5.0)
                {
                    _lastDaemonLaunchAttemptUtc = DateTime.UtcNow;
                    _daemonProcess = null;
                    _daemonStdin = null;
                    await Task.Delay(1000).ConfigureAwait(false);
                    if (!_isShuttingDown)
                        await DispatchAddToEngineAsync(snap).ConfigureAwait(false);
                }
            }
        }

        private async Task SendCommandAsync(object payload)
        {
            if (_isShuttingDown || _usingFallbackEngine)
                return;

            await _daemonCmdLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_daemonProcess != null && !_daemonProcess.HasExited && _daemonStdin != null)
                {
                    string json = JsonSerializer.Serialize(payload);
                    await _daemonStdin.WriteLineAsync(json).ConfigureAwait(false);
                    await _daemonStdin.FlushAsync().ConfigureAwait(false);
                }
            }
            catch { }
            finally
            {
                _daemonCmdLock.Release();
            }
        }

        private ClientEngine GetOrCreateFallbackEngine()
        {
            if (_mtFallbackEngine != null)
                return _mtFallbackEngine;

            Directory.CreateDirectory(_fallbackCacheDir);
            int port = RuTrackerModService.FindFreeHighListenPort(44880, 90);
            var settings = new EngineSettingsBuilder
            {
                CacheDirectory = _fallbackCacheDir,
                AllowedEncryption = new List<MonoTorrent.Connections.EncryptionType>
                {
                    MonoTorrent.Connections.EncryptionType.RC4Full,
                    MonoTorrent.Connections.EncryptionType.RC4Header,
                    MonoTorrent.Connections.EncryptionType.PlainText
                },
                ListenEndPoints = new Dictionary<string, IPEndPoint>
                {
                    ["ipv4"] = new IPEndPoint(IPAddress.Any, port)
                },
                AllowPortForwarding = true,
                AllowLocalPeerDiscovery = true,
                MaximumConnections = 2000,
                MaximumUploadRate = 0,
                MaximumDownloadRate = 0,
                AutoSaveLoadDhtCache = true,
                AutoSaveLoadFastResume = true,
                AutoSaveLoadMagnetLinkMetadata = true
            }.ToSettings();

            _mtFallbackEngine = new ClientEngine(settings);
            return _mtFallbackEngine;
        }

        private async Task AddToFallbackEngineAsync(List<RuTrackerSeedingItem> items)
        {
            var engine = GetOrCreateFallbackEngine();
            foreach (var it in items)
            {
                try
                {
                    string hashLower = it.InfoHash.ToLowerInvariant();
                    if (_mtManagers.TryGetValue(hashLower, out var existingMgr))
                    {
                        if (existingMgr.State is TorrentState.Stopped or TorrentState.Paused)
                            await existingMgr.StartAsync().ConfigureAwait(false);
                        continue;
                    }

                    Torrent? tor = null;
                    if (!string.IsNullOrWhiteSpace(it.TorrentFilePath) && File.Exists(it.TorrentFilePath))
                    {
                        byte[] raw = await File.ReadAllBytesAsync(it.TorrentFilePath).ConfigureAwait(false);
                        Torrent.TryLoad(raw, out tor);
                    }

                    bool createSub = false;
                    if (tor != null && tor.Files != null && tor.Files.Count > 1 && !string.IsNullOrWhiteSpace(tor.Name))
                    {
                        string expectedWithSub = Path.Combine(it.ContentDir, tor.Name);
                        if (Directory.Exists(expectedWithSub))
                            createSub = true;
                    }

                    var ts = new TorrentSettingsBuilder
                    {
                        AllowDht = true,
                        AllowPeerExchange = true,
                        UploadSlots = 64,
                        MaximumConnections = 1000,
                        CreateContainingDirectory = createSub
                    }.ToSettings();

                    TorrentManager mgr;
                    if (tor != null)
                        mgr = await engine.AddAsync(tor, it.ContentDir, ts).ConfigureAwait(false);
                    else if (MagnetLink.TryParse(it.MagnetLink, out var mag))
                        mgr = await engine.AddAsync(mag, it.ContentDir, ts).ConfigureAwait(false);
                    else
                        continue;

                    foreach (var tr in RuTrackerModService.PublicTrackers)
                    {
                        if (Uri.TryCreate(tr, UriKind.Absolute, out var uri))
                        {
                            try { await mgr.TrackerManager.AddTrackerAsync(uri).ConfigureAwait(false); } catch { }
                        }
                    }

                    if (mgr.Files != null && it.SkipFileIndices.Count > 0)
                    {
                        var skipSet = new HashSet<int>(it.SkipFileIndices);
                        for (int i = 0; i < mgr.Files.Count; i++)
                        {
                            if (skipSet.Contains(i))
                                await mgr.SetFilePriorityAsync(mgr.Files[i], Priority.DoNotDownload).ConfigureAwait(false);
                        }
                    }

                    _mtManagers[hashLower] = mgr;
                    if (!it.IsPaused)
                        await mgr.StartAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    it.RawStageBadge = "ОШИБКА";
                    it.RawStageColor = Windows.UI.Color.FromArgb(255, 239, 68, 68);
                    it.RawErrorMessage = ex.Message;
                }
            }
        }

        private void PollFallbackEngineStats()
        {
            RuTrackerSeedingItem[] snap;
            lock (_syncLock) { snap = _backingItems.ToArray(); }

            foreach (var it in snap)
            {
                string hashLower = it.InfoHash.ToLowerInvariant();
                if (!_mtManagers.TryGetValue(hashLower, out var mgr))
                    continue;

                if (it.IsPaused)
                {
                    it.RawStageBadge = "ПАУЗА";
                    it.RawStageColor = Windows.UI.Color.FromArgb(255, 217, 119, 6);
                    it.RawUploadSpeedBps = 0;
                    it.RawLeeches = 0;
                    it.RawSeeds = 0;
                    continue;
                }

                long sent = Math.Max(0L, mgr.Monitor.DataBytesSent);
                if (sent < it.LastSeenEngineSessionUp)
                    it.PersistedUploadedBytes += it.LastSeenEngineSessionUp;
                if (sent != it.LastSeenEngineSessionUp)
                    _statsDirty = true;
                it.LastSeenEngineSessionUp = sent;
                it.RawSessionUploadedBytes = sent;
                it.RawUploadSpeedBps = Math.Max(0L, mgr.Monitor.UploadRate);
                it.RawLeeches = Math.Max(0, mgr.Peers.Leechs);
                it.RawSeeds = Math.Max(0, mgr.Peers.Seeds);

                if (mgr.State is TorrentState.Hashing or TorrentState.Metadata)
                {
                    it.RawStageBadge = "ПРОВЕРКА";
                    it.RawStageColor = Windows.UI.Color.FromArgb(255, 14, 165, 233);
                }
                else if (mgr.State == TorrentState.Error)
                {
                    it.RawStageBadge = "ОШИБКА";
                    it.RawStageColor = Windows.UI.Color.FromArgb(255, 239, 68, 68);
                }
                else
                {
                    it.RawStageBadge = "РАЗДАЧА";
                    it.RawStageColor = Windows.UI.Color.FromArgb(255, 16, 185, 129);
                }
            }
        }

        public void SaveState()
        {
            try
            {
                List<RuTrackerPersistedSeedingRecord> records;
                lock (_syncLock)
                {
                    records = _backingItems.Select(i => new RuTrackerPersistedSeedingRecord
                    {
                        InfoHash = i.InfoHash,
                        DisplayTitle = i.DisplayTitle,
                        TopicId = i.TopicId,
                        TopicTitle = i.TopicTitle,
                        ContentDir = i.ContentDir,
                        TorrentFilePath = i.TorrentFilePath,
                        MagnetLink = i.MagnetLink,
                        SelectedRelativePaths = (i.SelectedRelativePaths ?? new List<string>()).ToList(),
                        SkipFileIndices = (i.SkipFileIndices ?? new List<int>()).ToList(),
                        TotalSizeBytes = i.TotalSizeBytes,
                        TotalUploadedBytes = Math.Max(0L, i.PersistedUploadedBytes + Math.Max(i.SessionUploadedBytes, i.LastSeenEngineSessionUp)),
                        AddedAt = i.AddedAt,
                        IsPaused = i.IsPaused
                    }).ToList();
                }

                string json = JsonSerializer.Serialize(records, _jsonOptions);
                SafeFileOperations.WriteAllTextAtomic(_stateFilePath, json, flushToDisk: true);
                _lastPersistAtUtc = DateTime.UtcNow;
            }
            catch { }
        }

        public void ShutdownGracefully()
        {
            _isShuttingDown = true;
            try
            {
                lock (_syncLock)
                {
                    foreach (var it in _backingItems)
                    {
                        long cur = Math.Max(it.SessionUploadedBytes, it.LastSeenEngineSessionUp);
                        if (cur > 0)
                        {
                            it.PersistedUploadedBytes += cur;
                            it.SessionUploadedBytes = 0;
                            it.LastSeenEngineSessionUp = 0;
                            it.RawSessionUploadedBytes = 0;
                        }
                    }
                }
                SaveState();
            }
            catch { }

            try
            {
                if (_daemonProcess != null && !_daemonProcess.HasExited && _daemonStdin != null)
                {
                    _daemonStdin.WriteLine("{\"cmd\":\"shutdown\"}");
                    _daemonStdin.Flush();
                    _daemonProcess.WaitForExit(1200);
                }
            }
            catch { }

            try
            {
                _mtFallbackEngine?.Dispose();
            }
            catch { }
        }
    }
}
