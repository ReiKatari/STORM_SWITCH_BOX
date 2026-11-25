#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using MonoTorrent.Client;
using StormSwitchBox.Models;
using StormSwitchBox.ViewModels;
using StormSwitchBox.Views.Dialogs;

namespace StormSwitchBox.Services
{
    public sealed class RuTrackerActiveDownloadItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject, IDisposable
    {
        private int _disposed = 0;
        public string BatchId { get; set; } = string.Empty;
        public bool IsCompleted => StageBadge == "ГОТОВО" || RawStageBadge == "ГОТОВО" || IsBuildCompletedOnDisk;

        private string _displayTitle = string.Empty;
        public string DisplayTitle
        {
            get => _displayTitle;
            set => SetProperty(ref _displayTitle, value);
        }

        private string _stageBadge = "В ОЧЕРЕДИ";
        public string EffectiveStage => !string.IsNullOrEmpty(RawStageBadge) ? RawStageBadge : StageBadge;

        public string StageBadge
        {
            get => _stageBadge;
            set
            {
                if (SetProperty(ref _stageBadge, value))
                {
                    OnPropertyChanged(nameof(StageBadgeDisplay));
                    OnPropertyChanged(nameof(PauseResumeGlyph));
                    OnPropertyChanged(nameof(PauseResumeToolTip));
                    OnPropertyChanged(nameof(PauseResumeTooltip));
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
                    try { _speedBrush = new SolidColorBrush(ResolveSpeedColor(EffectiveStage)); } catch { }
                }
                return _speedBrush;
            }
            set => SetProperty(ref _speedBrush, value);
        }

        private static Windows.UI.Color ResolveSpeedColor(string stage) => stage switch
        {
            "ОШИБКА" or "ОТМЕНЕНО" => Windows.UI.Color.FromArgb(255, 248, 113, 113), // #F87171 Red
            "ПАУЗА" => Windows.UI.Color.FromArgb(255, 251, 191, 36),                 // #FBBF24 Amber
            "В ОЧЕРЕДИ" => Windows.UI.Color.FromArgb(255, 148, 163, 184),            // #94A3B8 Slate
            "СБОРКА" or "МОДЫ" => Windows.UI.Color.FromArgb(255, 192, 132, 252),     // #C084FC Violet
            _ => Windows.UI.Color.FromArgb(255, 34, 197, 94)                         // #22C55E Emerald
        };

        private double _progressPercent;
        public double ProgressPercent
        {
            get => _progressPercent;
            set => SetProperty(ref _progressPercent, value);
        }

        private string _progressDetails = string.Empty;
        public string ProgressDetails
        {
            get => _progressDetails;
            set
            {
                if (SetProperty(ref _progressDetails, value))
                {
                    OnPropertyChanged(nameof(ProgressDetailsDisplay));
                }
            }
        }

        private string _speedAndPeersText = string.Empty;
        public string SpeedAndPeersText
        {
            get => _speedAndPeersText;
            set
            {
                if (SetProperty(ref _speedAndPeersText, value))
                {
                    OnPropertyChanged(nameof(SpeedTextDisplay));
                    OnPropertyChanged(nameof(SpeedAndPeersTextDisplay));
                }
            }
        }

        public double CurrentSpeedBps { get; set; }
        public int CurrentSeeds { get; set; }
        public int CurrentPeers { get; set; }

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        private bool _isPausedByUser = false;
        public bool IsPausedByUser
        {
            get => _isPausedByUser;
            set
            {
                if (SetProperty(ref _isPausedByUser, value))
                {
                    OnPropertyChanged(nameof(PauseResumeGlyph));
                    OnPropertyChanged(nameof(PauseResumeToolTip));
                }
            }
        }

        public bool IsPreemptedByQueue { get; set; }
        public DateTime LastActivatedAtUtc { get; set; } = DateTime.MinValue;
        public double SmoothedSpeedBps { get; set; }

        public bool IsActivelyDownloading =>
            IsPipelineStarted &&
            !IsPreemptedByQueue &&
            !IsPausedByUser &&
            EffectiveStage is not ("ГОТОВО" or "ОТМЕНЕНО" or "ОШИБКА" or "СБОРКА" or "ПАУЗА" or "В ОЧЕРЕДИ");

        public void UpdateSmoothedSpeed()
        {
            if (IsActivelyDownloading && EffectiveStage is "СКАЧИВАНИЕ" or "МОДЫ" or "МЕТАДАННЫЕ")
            {
                double raw = Math.Max(0.0, RawSpeedBps);
                SmoothedSpeedBps = SmoothedSpeedBps <= 0.0
                    ? raw
                    : (SmoothedSpeedBps * 0.65) + (raw * 0.35);
            }
            else
            {
                SmoothedSpeedBps = 0.0;
            }
        }

        public bool CountsTowardConcurrencyLimit(DateTime nowUtc)
        {
            if (!IsActivelyDownloading)
                return false;

            if (RawSeeds > 0 || RawPercent >= 95.0)
                return true;

            double elapsedSec = LastActivatedAtUtc == DateTime.MinValue
                ? 0.0
                : (nowUtc - LastActivatedAtUtc).TotalSeconds;

            if (elapsedSec < RuTrackerDownloadQueueService.StartupWarmupSeconds)
                return true;

            double effectiveBps = Math.Max(RawSpeedBps, SmoothedSpeedBps);
            return effectiveBps >= RuTrackerDownloadQueueService.LowSpeedExemptThresholdBps;
        }

        public string PauseResumeGlyph => (IsPausedByUser || StageBadge == "ПАУЗА") ? "\uE768" : "\uE769"; // E768 = Play, E769 = Pause
        public string PauseResumeToolTip => App.Localization.TranslateText((IsPausedByUser || StageBadge == "ПАУЗА") ? "Продолжить скачивание" : "Приостановить скачивание");
        public string PauseResumeTooltip => PauseResumeToolTip;

        public string StageBadgeDisplay => App.Localization.TranslateText(StageBadge);
        public string ProgressDetailsDisplay => App.Localization.TranslateText(ProgressDetails);
        public string SpeedTextDisplay => App.Localization.TranslateText(SpeedAndPeersText);
        public string SpeedAndPeersTextDisplay => App.Localization.TranslateText(SpeedAndPeersText);

        public string SpeedOnlyDisplay
        {
            get
            {
                string st = EffectiveStage;
                if (st is "СКАЧИВАНИЕ" or "МЕТАДАННЫЕ")
                    return $"{(CurrentSpeedBps / (1024.0 * 1024.0)):F2} МБ/с";
                if (st == "МОДЫ")
                {
                    if (!string.IsNullOrWhiteSpace(SpeedAndPeersText) && SpeedAndPeersText.Contains("МБ/с", StringComparison.OrdinalIgnoreCase))
                        return SpeedAndPeersText;
                    return App.Localization.TranslateText("Моды");
                }
                if (st == "СБОРКА") return App.Localization.TranslateText("Сборка");
                if (st == "ПАУЗА" || IsPausedByUser) return App.Localization.TranslateText("Пауза");
                if (st == "ГОТОВО") return App.Localization.TranslateText("Готово");
                if (st == "ОТМЕНЕНО") return App.Localization.TranslateText("Отменено");
                if (st == "ОШИБКА") return App.Localization.TranslateText("Ошибка");
                return App.Localization.TranslateText("В очереди");
            }
        }

        public string SeedsPeersDisplay => EffectiveStage is "СКАЧИВАНИЕ" or "МЕТАДАННЫЕ"
            ? $"{CurrentSeeds} / {CurrentPeers}"
            : "-";

        public string SeedsPeersTooltip => EffectiveStage is "СКАЧИВАНИЕ" or "МЕТАДАННЫЕ"
            ? $"{App.Localization.TranslateText("Сиды:")} {CurrentSeeds} | {App.Localization.TranslateText("Пиры:")} {CurrentPeers}"
            : ProgressDetailsDisplay;

        public void NotifyLocalizationChanged()
        {
            OnPropertyChanged(nameof(StageBadgeDisplay));
            OnPropertyChanged(nameof(ProgressDetailsDisplay));
            OnPropertyChanged(nameof(SpeedTextDisplay));
            OnPropertyChanged(nameof(SpeedAndPeersTextDisplay));
            OnPropertyChanged(nameof(SpeedOnlyDisplay));
            OnPropertyChanged(nameof(SeedsPeersDisplay));
            OnPropertyChanged(nameof(SeedsPeersTooltip));
            OnPropertyChanged(nameof(PauseResumeToolTip));
            OnPropertyChanged(nameof(PauseResumeTooltip));
        }

        public string? ActiveLimitFilePath { get; set; }
        internal long LastWrittenLimitBps { get; set; } = -1;

        public CancellationTokenSource? Cts { get; set; }
        public ManualResetEventSlim PauseEvent { get; } = new(true);
        public TorrentManager? ActiveTorrentManager { get; set; }

        // Потокобезопасные поля для агрегации UI
        public double RawPercent;
        public double RawSpeedBps;
        public int RawSeeds;
        public int RawPeers;
        public string RawDetails = string.Empty;
        public string RawSpeedText = string.Empty;
        public string RawStageBadge = "В ОЧЕРЕДИ";
        public Windows.UI.Color RawStageColor = Windows.UI.Color.FromArgb(255, 100, 116, 139);

        // Данные выполнения задачи
        public List<(ProcessingTask Task, RuTrackerTaskPayload Payload, RuTrackerGameSlotItem Slot)> CreatedTasks { get; set; } = new();
        public string SharedReleaseDir { get; set; } = string.Empty;
        public bool HasTorrentSource { get; set; } = true;
        public RuTrackerTopicItem? CurrentTopic { get; set; }
        public RuTrackerModService? Service { get; set; }
        public TasksViewModel? ViewModel { get; set; }
        public bool IsPipelineStarted { get; set; }
        public bool IsBuildCompletedOnDisk { get; set; }
        public bool DeleteSourceAfterVerify { get; set; } = true;
        public Task? ExecutionTask { get; set; }

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

            if (Math.Abs(ProgressPercent - RawPercent) > 0.05)
                ProgressPercent = RawPercent;
            if (ProgressDetails != RawDetails && !string.IsNullOrEmpty(RawDetails))
                ProgressDetails = RawDetails;
            if (SpeedAndPeersText != RawSpeedText)
                SpeedAndPeersText = RawSpeedText;
            bool stageChanged = StageBadge != RawStageBadge && !string.IsNullOrEmpty(RawStageBadge);
            if (stageChanged)
            {
                StageBadge = RawStageBadge;
                OnPropertyChanged(nameof(StageBadgeDisplay));
                OnPropertyChanged(nameof(PauseResumeGlyph));
                OnPropertyChanged(nameof(PauseResumeToolTip));
                OnPropertyChanged(nameof(PauseResumeTooltip));
            }
            if (dq != null)
            {
                if (_stageBrush == null || _stageBrush.Color != RawStageColor)
                    StageBrush = new SolidColorBrush(RawStageColor);
                var targetSpeedColor = ResolveSpeedColor(EffectiveStage);
                if (_speedBrush == null || _speedBrush.Color != targetSpeedColor)
                    SpeedBrush = new SolidColorBrush(targetSpeedColor);
            }
            bool metricsChanged = Math.Abs(CurrentSpeedBps - RawSpeedBps) > 256.0 ||
                                  CurrentSeeds != RawSeeds ||
                                  CurrentPeers != RawPeers ||
                                  stageChanged;
            CurrentSpeedBps = RawSpeedBps;
            CurrentSeeds = RawSeeds;
            CurrentPeers = RawPeers;
            if (metricsChanged)
            {
                OnPropertyChanged(nameof(SpeedOnlyDisplay));
                OnPropertyChanged(nameof(SeedsPeersDisplay));
                OnPropertyChanged(nameof(SeedsPeersTooltip));
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            try { Cts?.Cancel(); } catch { }
            try { Cts?.Dispose(); } catch { }
            Cts = null;
            try { PauseEvent.Dispose(); } catch { }
        }
    }

        public sealed class RuTrackerPersistedTaskPayload
    {
        public string TaskId { get; set; } = string.Empty;
        public string PosterUrl { get; set; } = string.Empty;
        public int SlotNumber { get; set; }
        public string SlotGameTitle { get; set; } = string.Empty;
        public string CustomModTitle { get; set; } = string.Empty;
        public string TargetReleaseDir { get; set; } = string.Empty;
        public string OutputFolder { get; set; } = string.Empty;
        public string OutputFormat { get; set; } = "NSZ";
        public bool AutoBuild { get; set; } = true;
        public bool IncludeTechTags { get; set; }
        public bool InjectDlcUnlocker { get; set; }
        public bool DeleteSourceAfterVerify { get; set; } = true;
        public string TopicId { get; set; } = string.Empty;
        public string TopicTitle { get; set; } = string.Empty;
        public int Seeds { get; set; }
        public string InfoHash { get; set; } = string.Empty;
        public string MagnetLink { get; set; } = string.Empty;
        public string TorrentCachePath { get; set; } = string.Empty;
        public string TorrentBytesBase64 { get; set; } = string.Empty;
        public List<PersistedTorrentFileItem> TorrentFiles { get; set; } = new();
        public List<string> CloudLinks { get; set; } = new();
        public string TaskTitle { get; set; } = string.Empty;
    }

    public sealed class RuTrackerPersistedQueueRecord
    {
        public string BatchId { get; set; } = string.Empty;
        public string DisplayTitle { get; set; } = string.Empty;
        public string StageBadge { get; set; } = string.Empty;
        public double ProgressPercent { get; set; }
        public string ProgressDetails { get; set; } = string.Empty;
        public bool IsPausedByUser { get; set; }
        public DateTime SavedAt { get; set; } = DateTime.Now;
        public string SharedReleaseDir { get; set; } = string.Empty;
        public bool HasTorrentSource { get; set; } = true;
        public List<RuTrackerPersistedTaskPayload> Tasks { get; set; } = new();
    }

    public sealed class RuTrackerDownloadQueueService
    {
        public static RuTrackerDownloadQueueService Instance { get; } = new();

        public static int MaxConcurrentDownloads => Math.Clamp(App.Settings?.Current?.RuTrackerMaxConcurrentDownloads ?? 4, 1, 10);
        public const double LowSpeedExemptThresholdBps = 64.0 * 1024.0; // Только 0-сид < 64 КБ/с вне основных слотов (макс. N+1 всего)
        public const double StartupWarmupSeconds = 20.0;
        public static int MaxTotalConcurrentDownloads => MaxConcurrentDownloads + 1;
        public static int MaxConcurrentBuilds => Math.Clamp(App.Settings?.Current?.ConcurrentTasks ?? 3, 1, 8);
        public ObservableCollection<RuTrackerActiveDownloadItem> Items { get; } = new();
        public ObservableCollection<RuTrackerActiveDownloadItem> BottomDockItems { get; } = new();
        public ObservableCollection<RuTrackerSeedingItem> SeedingItems => RuTrackerSeedingService.Instance.Items;
        public RuTrackerSeedingService Seeding => RuTrackerSeedingService.Instance;
        private readonly List<RuTrackerActiveDownloadItem> _backingItems = new();
        private readonly HashSet<string> _completedIdentityKeys = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _buildGateLock = new();
        private int _activeBuildCount = 0;
        private readonly LinkedList<TaskCompletionSource<bool>> _buildWaiters = new();

        private readonly object _syncLock = new();
        private readonly System.Threading.Timer _uiTimer;
        private static readonly JsonSerializerOptions _queueJsonOptions = new JsonSerializerOptions { WriteIndented = false };
        private readonly string _queueStatePath;

        private int _queueSaveRequestVersion = 0;
        private int _queueSaveLoopActive = 0;
        private readonly SemaphoreSlim _queueSaveSemaphore = new(1, 1);

        private double _observedPeakBps = 0.0;
        private DateTime _probeStartedUtc = DateTime.MinValue;
        private DateTime _pinnedHighSinceUtc = DateTime.MinValue;
        private DateTime _lastReprobeUtc = DateTime.MinValue;
        private DateTime _lastPeakSaveUtc = DateTime.MinValue;
        private DateTime _lastLimitWriteUtc = DateTime.MinValue;
        private DateTime _activeTransferStartedUtc = DateTime.MinValue;
        private int _limitTickCounter = 0;
        private readonly object _limitCalcLock = new();

        public void EnsureInitialized() { }

        private Task AcquireBuildSlotAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            LinkedListNode<TaskCompletionSource<bool>> node;
            TaskCompletionSource<bool> tcs;
            lock (_buildGateLock)
            {
                if (_activeBuildCount < MaxConcurrentBuilds)
                {
                    _activeBuildCount++;
                    return Task.CompletedTask;
                }
                tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                node = _buildWaiters.AddLast(tcs);
            }

            if (ct.CanBeCanceled)
            {
                CancellationTokenRegistration reg = ct.Register(() =>
                {
                    bool removed = false;
                    lock (_buildGateLock)
                    {
                        if (node.List == _buildWaiters)
                        {
                            _buildWaiters.Remove(node);
                            removed = true;
                        }
                    }
                    if (removed)
                        tcs.TrySetCanceled(ct);
                });

                return tcs.Task.ContinueWith(
                    t =>
                    {
                        reg.Dispose();
                        return t;
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default).Unwrap();
            }

            return tcs.Task;
        }

        private void ReleaseBuildSlot()
        {
            List<TaskCompletionSource<bool>>? toWake = null;
            lock (_buildGateLock)
            {
                if (_activeBuildCount > 0)
                    _activeBuildCount--;
                int limit = MaxConcurrentBuilds;
                while (_activeBuildCount < limit && _buildWaiters.First != null)
                {
                    var node = _buildWaiters.First;
                    _buildWaiters.RemoveFirst();
                    _activeBuildCount++;
                    toWake ??= new List<TaskCompletionSource<bool>>();
                    toWake.Add(node.Value);
                }
            }
            if (toWake != null)
            {
                foreach (var waiter in toWake)
                    waiter.TrySetResult(true);
            }
        }

        private void NotifyBuildConcurrencyChanged()
        {
            List<TaskCompletionSource<bool>>? toWake = null;
            lock (_buildGateLock)
            {
                int limit = MaxConcurrentBuilds;
                while (_activeBuildCount < limit && _buildWaiters.First != null)
                {
                    var node = _buildWaiters.First;
                    _buildWaiters.RemoveFirst();
                    _activeBuildCount++;
                    toWake ??= new List<TaskCompletionSource<bool>>();
                    toWake.Add(node.Value);
                }
            }
            if (toWake != null)
            {
                foreach (var waiter in toWake)
                    waiter.TrySetResult(true);
            }
        }

        public void NotifyConcurrencySettingsChanged()
        {
            NotifyBuildConcurrencyChanged();
            EvaluateQueuePriorities(allowPreemptionForReorder: true);
        }

        private RuTrackerDownloadQueueService()
        {
            string localAppDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StormSwitchBox");
            Directory.CreateDirectory(localAppDataDir);
            _queueStatePath = Path.Combine(localAppDataDir, "rutracker_download_queue.json");

            // UI-таймер 350 мс через System.Threading.Timer + ленивый пропуск при полном простое
            _uiTimer = new System.Threading.Timer(OnTimerCallback, null, TimeSpan.FromMilliseconds(350), TimeSpan.FromMilliseconds(350));

            LoadPersistedState();

            App.Localization.LanguageChanged += () =>
            {
                RunOnUiSyncOrAsync(() =>
                {
                    RuTrackerActiveDownloadItem[] snapshot;
                    lock (_syncLock)
                    {
                        snapshot = _backingItems.ToArray();
                    }
                    foreach (var item in snapshot)
                    {
                        item.NotifyLocalizationChanged();
                    }
                });
            };
        }

        private void RunOnUiSyncOrAsync(Action action)
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

        private bool _lastTimerWasIdle;

        private void OnTimerCallback(object? state)
        {
            bool anyWork;
            lock (_syncLock)
            {
                anyWork = _backingItems.Any(i =>
                    i.IsActive ||
                    !i.IsPipelineStarted && !i.IsPausedByUser && i.EffectiveStage is not ("ГОТОВО" or "ОТМЕНЕНО" or "ОШИБКА") ||
                    i.ExecutionTask is { IsCompleted: true } && i.EffectiveStage is not ("ГОТОВО" or "ОТМЕНЕНО" or "ОШИБКА" or "ПАУЗА") ||
                    i.StageBadge != i.RawStageBadge);
            }
            if (!anyWork && RuTrackerModDownloaderDialog.ActiveInstance == null && RuTrackerSeedingService.Instance.GetSummarySnapshot().ActiveSeedingCount == 0)
            {
                if (_lastTimerWasIdle) return;
                _lastTimerWasIdle = true;
            }
            else
            {
                _lastTimerWasIdle = false;
            }

            RunOnUiSyncOrAsync(() =>
            {
                OnUiTimerTick();
            });
        }

                private void OnUiTimerTick()
        {
            RuTrackerActiveDownloadItem[] snapshot;
            lock (_syncLock)
            {
                snapshot = _backingItems.ToArray();
            }

            double totalSpeedBps = 0;
            int totalSeeds = 0;
            int totalPeers = 0;
            int runningCount = 0;

            foreach (var item in snapshot)
            {
                // Watchdog: автоматическое исцеление завершенных задач со старым бейджем
                if (item.ExecutionTask is { IsCompleted: true } &&
                    item.EffectiveStage is not ("ГОТОВО" or "ОТМЕНЕНО" or "ОШИБКА" or "ПАУЗА"))
                {
                    item.IsActive = false;
                    item.RawStageBadge = item.ExecutionTask.IsFaulted ? "ОШИБКА" : (item.ExecutionTask.IsCanceled ? "ОТМЕНЕНО" : "ГОТОВО");
                    item.RawStageColor = item.RawStageBadge == "ГОТОВО"
                        ? Windows.UI.Color.FromArgb(255, 34, 197, 94)
                        : Windows.UI.Color.FromArgb(255, 220, 38, 38);
                    if (item.RawStageBadge == "ГОТОВО")
                    {
                        item.RawPercent = 100.0;
                        item.RawSpeedText = "100% Завершено";
                        item.RawDetails = "Сборка завершена";
                    }
                    item.CommitUiUpdates();
                    if (item.RawStageBadge == "ГОТОВО")
                    {
                        item.IsBuildCompletedOnDisk = true;
                        RecordCompletedBatchIdentities(item);
                    }
                    SyncBottomDockOrderOnUi();
                    SaveQueueState();
                    EvaluateQueuePriorities();
                }

                item.UpdateSmoothedSpeed();
                item.CommitUiUpdates();

                if (item.IsActive && item.EffectiveStage is "СКАЧИВАНИЕ" or "МОДЫ" or "МЕТАДАННЫЕ")
                {
                    runningCount++;
                    totalSpeedBps += item.RawSpeedBps;
                    totalSeeds += Math.Max(0, item.RawSeeds);
                    totalPeers += Math.Max(0, item.RawPeers);
                }

                // Синхронизируем состояние задач в TasksViewModel.Tasks (только при реальном изменении)
                foreach (var (t, _, _) in item.CreatedTasks)
                {
                    if (item.StageBadge is "СКАЧИВАНИЕ" or "МОДЫ" or "МЕТАДАННЫЕ")
                    {
                        double clampedPct = Math.Clamp(item.RawPercent, 0, 100);
                        if (Math.Abs(t.Progress - clampedPct) > 0.05)
                            t.Progress = clampedPct;
                        if (!string.IsNullOrEmpty(item.RawSpeedText) && !string.Equals(t.Speed, item.RawSpeedText, StringComparison.Ordinal))
                            t.Speed = item.RawSpeedText;
                    }
                }
            }

            DateTime nowUtc = DateTime.UtcNow;
            int countedFastRunning = snapshot.Count(i => i.CountsTowardConcurrencyLimit(nowUtc));

            // Самовосстанавливающийся планировщик:
            if (countedFastRunning < MaxConcurrentDownloads && runningCount < MaxTotalConcurrentDownloads)
            {
                bool hasPending = snapshot.Any(i =>
                    i.EffectiveStage is not ("ГОТОВО" or "ОТМЕНЕНО" or "ОШИБКА" or "СБОРКА") &&
                    !i.IsPausedByUser &&
                    (!i.IsPipelineStarted || i.IsPreemptedByQueue) &&
                    i.CreatedTasks.Count > 0);

                if (hasPending)
                {
                    EvaluateQueuePriorities();
                }
            }

            if ((++_limitTickCounter % 4) == 0)
            {
                ApplyGlobalSpeedLimitDistribution(snapshot, totalSpeedBps);
            }

            int inList = snapshot.Length;
            int dockCount = BottomDockItems.Count;
            int downloading = runningCount;
            int inQueue = snapshot.Count(i => i.StageBadge is "В ОЧЕРЕДИ");
            int finished = snapshot.Count(i => i.StageBadge is "ГОТОВО");

            RuTrackerSeedingService.Instance.CommitAllUiUpdates();
            var seedSummary = RuTrackerSeedingService.Instance.GetSummarySnapshot();

            // Если открыто диалоговое окно RuTrackerModDownloaderDialog, обновляем его заголовочную панель
            var activeDlg = RuTrackerModDownloaderDialog.ActiveInstance;
            if (activeDlg != null)
            {
                activeDlg.UpdateHeaderSummary(
                    activeDlg.IsDownloadsTabActive ? inList : dockCount,
                    downloading,
                    inQueue,
                    finished,
                    totalSpeedBps,
                    totalSeeds,
                    totalPeers,
                    seedSummary.TotalCount,
                    seedSummary.ActiveSeedingCount,
                    seedSummary.TotalUploadBps,
                    seedSummary.TotalUploadedBytes,
                    seedSummary.TotalLeeches,
                    seedSummary.TotalSeeds,
                    totalInQueueAndFinished: inList);
            }
        }

        public void EnqueueBatch(RuTrackerActiveDownloadItem item)
        {
            lock (_syncLock)
            {
                if (!_backingItems.Contains(item))
                    _backingItems.Add(item);

                RunOnUiSyncOrAsync(() =>
                {
                    if (!Items.Contains(item))
                        Items.Add(item);
                    SyncBottomDockOrderOnUi();
                });
            }
            EvaluateQueuePriorities();
            SaveQueueState();
        }

        public void EvaluateQueuePriorities(bool allowPreemptionForReorder = false)
        {
            lock (_syncLock)
            {
                DateTime nowUtc = DateTime.UtcNow;

                // Находим все кандидаты на загрузку: не завершены, не отменены, не ошибочны, не в сборке, не на паузе пользователем и с непустыми задачами
                var candidates = _backingItems
                    .Where(i => i.EffectiveStage is not ("ГОТОВО" or "ОТМЕНЕНО" or "ОШИБКА" or "СБОРКА") && !i.IsPausedByUser && i.CreatedTasks.Count > 0)
                    .ToList();

                // Если пользователь вручную поднял ожидающую задачу стрелкой вверх в топ-5 при заполненных слотах -
                // освобождаем место за счёт самой нижней быстрой задачи (медленные < 200 КБ/с никогда не останавливаем).
                if (allowPreemptionForReorder)
                {
                    int maxDl = MaxConcurrentDownloads;
                    var topRequested = candidates.Take(maxDl)
                        .Where(i => !i.IsPipelineStarted || i.IsPreemptedByQueue)
                        .ToList();

                    int currentCounted = candidates.Count(i => i.CountsTowardConcurrencyLimit(nowUtc));
                    int excessCount = Math.Max(0, currentCounted - maxDl);
                    int needToFree = (topRequested.Count > 0 && currentCounted >= maxDl)
                        ? Math.Min(topRequested.Count + excessCount, currentCounted)
                        : excessCount;

                    if (needToFree > 0)
                    {
                        var demotableFast = candidates
                            .Where(i => i.CountsTowardConcurrencyLimit(nowUtc))
                            .Skip(Math.Max(0, maxDl - topRequested.Count))
                            .Reverse()
                            .Take(needToFree)
                            .ToList();

                        foreach (var demote in demotableFast)
                        {
                            demote.IsPreemptedByQueue = true;
                            demote.PauseEvent.Reset();
                            demote.SmoothedSpeedBps = 0;
                            if (demote.ActiveTorrentManager != null &&
                                (demote.ActiveTorrentManager.State == TorrentState.Downloading || demote.ActiveTorrentManager.State == TorrentState.Metadata))
                            {
                                try { demote.ActiveTorrentManager.PauseAsync(); } catch { }
                            }
                            demote.RawStageBadge = "В ОЧЕРЕДИ";
                            demote.RawStageColor = Windows.UI.Color.FromArgb(255, 100, 116, 139);
                            demote.RawDetails = "В очереди закачек...";
                            demote.RawSpeedText = "В очереди";
                            demote.RawSpeedBps = 0;
                        }
                    }
                }

                int totalActive = candidates.Count(i => i.IsActivelyDownloading);
                int fastSlotsUsed = candidates.Count(i => i.CountsTowardConcurrencyLimit(nowUtc));

                // Запускаем или возобновляем очередные задачи по порядку, пока занято меньше 5 быстрых слотов (>= 200 КБ/с).
                // Все уже запущенные закачки (включая < 200 КБ/с) продолжают качаться без прерываний!
                foreach (var item in candidates)
                {
                    if (item.IsActivelyDownloading)
                        continue;

                    if (fastSlotsUsed < MaxConcurrentDownloads && totalActive < MaxTotalConcurrentDownloads)
                    {
                        bool pipelineNotRunning = !item.IsPipelineStarted ||
                                                  item.ExecutionTask == null ||
                                                  item.ExecutionTask.IsCompleted;

                        if (pipelineNotRunning)
                        {
                            if (item.Cts == null || item.Cts.IsCancellationRequested)
                                item.Cts = new CancellationTokenSource();
                            item.IsPreemptedByQueue = false;
                            item.IsPausedByUser = false;
                            item.LastActivatedAtUtc = nowUtc;
                            item.SmoothedSpeedBps = 0;
                            StartItemPipeline(item);
                        }
                        else
                        {
                            item.IsPreemptedByQueue = false;
                            item.IsPausedByUser = false;
                            item.LastActivatedAtUtc = nowUtc;
                            item.SmoothedSpeedBps = 0;
                            item.PauseEvent.Set();
                            if (item.ActiveTorrentManager != null &&
                                (item.ActiveTorrentManager.State == TorrentState.Paused || item.ActiveTorrentManager.State == TorrentState.Stopped))
                            {
                                try { item.ActiveTorrentManager.StartAsync(); } catch { }
                            }
                            item.RawStageBadge = item.HasTorrentSource ? "СКАЧИВАНИЕ" : "МОДЫ";
                            item.StageBadge = item.RawStageBadge;
                            item.RawStageColor = Windows.UI.Color.FromArgb(255, 2, 132, 199);
                            if (string.IsNullOrWhiteSpace(item.RawDetails) || item.RawDetails.Contains("Приостановлено") || item.RawDetails.Contains("очереди"))
                                item.RawDetails = "Возобновление скачивания...";

                            foreach (var (t, _, _) in item.CreatedTasks)
                            {
                                RunOnUiSyncOrAsync(() =>
                                {
                                    t.IsPaused = false;
                                    t.IsRunning = true;
                                    t.PauseEvent.Set();
                                    if (t.Status == "Пауза" || t.Status.Contains("Сохранено на паузе") || t.Status.Contains("очереди"))
                                        t.Status = "Возобновление скачивания...";
                                });
                            }
                        }

                        fastSlotsUsed++;
                        totalActive++;
                    }
                    else
                    {
                        // Ещё не запущенный или ранее вытесненный элемент ожидает свободного слота в очереди
                        item.RawStageBadge = "В ОЧЕРЕДИ";
                        item.RawStageColor = Windows.UI.Color.FromArgb(255, 100, 116, 139);
                        item.RawDetails = "В очереди закачек...";
                        item.RawSpeedText = "В очереди";
                        item.RawSpeedBps = 0;

                        foreach (var (t, _, _) in item.CreatedTasks)
                        {
                            RunOnUiSyncOrAsync(() =>
                            {
                                if (t.IsRunning)
                                {
                                    t.Status = "В очереди RuTracker...";
                                    t.Speed = "В очереди";
                                }
                            });
                        }
                    }
                }
            }
        }

        private void StartItemPipeline(RuTrackerActiveDownloadItem item)
        {
            if (item.CreatedTasks.Count == 0)
            {
                item.IsActive = false;
                item.RawStageBadge = "ОТМЕНЕНО";
                item.RawStageColor = Windows.UI.Color.FromArgb(255, 220, 38, 38);
                item.RawSpeedText = "Отменено";
                item.RawDetails = "Отсутствуют задачи для выполнения";
                item.CommitUiUpdates();
                return;
            }

            item.IsPipelineStarted = true;
            item.IsActive = true;
            item.IsPreemptedByQueue = false;
            if (item.LastActivatedAtUtc == DateTime.MinValue)
                item.LastActivatedAtUtc = DateTime.UtcNow;
            item.PauseEvent.Set();
            item.RawStageBadge = item.HasTorrentSource ? "СКАЧИВАНИЕ" : "МОДЫ";
            item.RawStageColor = Windows.UI.Color.FromArgb(255, 2, 132, 199);
            item.RawDetails = "Подключение к рою...";
            item.RawSpeedText = "0,00 МБ/с • Сиды: 0";
            item.CommitUiUpdates();

            item.ExecutionTask = Task.Run(async () =>
            {
                var batchCts = item.Cts ?? new CancellationTokenSource();
                var ct = batchCts.Token;
                bool handledByFinalizer = false;

                try
                {
                    if (item.CreatedTasks.Count == 1)
                    {
                        var (singleTask, singlePayload, _) = item.CreatedTasks[0];
                        await ExecuteSingleTaskPipelineAsync(item, singleTask, singlePayload, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        await ExecuteMultiTaskPipelineAsync(item, ct).ConfigureAwait(false);
                    }

                    handledByFinalizer = await HandleSuccessfulBatchCompletionAsync(item).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    item.IsActive = false;
                    item.RawSpeedBps = 0;
                    item.RawStageBadge = "ОТМЕНЕНО";
                    item.RawStageColor = Windows.UI.Color.FromArgb(255, 220, 38, 38);
                    item.RawSpeedText = "Отменено";
                    item.RawDetails = "Отменено пользователем";
                    foreach (var (t, _, _) in item.CreatedTasks)
                    {
                        RunOnUiSyncOrAsync(() =>
                        {
                            t.Status = "Отменено";
                            t.IsRunning = false;
                            t.IsPaused = false;
                            t.Speed = string.Empty;
                        });
                    }
                }
                catch (Exception ex)
                {
                    if (item.IsBuildCompletedOnDisk || item.RawStageBadge == "ГОТОВО")
                    {
                        App.Logger?.Log($"[RuTracker Queue] Пост-сборочное исключение проигнорировано (образ уже собран) [{item.BatchId}]: {ex.GetType().Name} {ex.Message}", LogLevel.Warning);
                        handledByFinalizer = await HandleSuccessfulBatchCompletionAsync(item).ConfigureAwait(false);
                    }
                    else
                    {
                        string errMsg = !string.IsNullOrWhiteSpace(ex.Message)
                            ? ex.Message
                            : $"{ex.GetType().Name} (0x{ex.HResult:X8})";
                        item.IsActive = false;
                        item.RawSpeedBps = 0;
                        item.RawStageBadge = "ОШИБКА";
                        item.RawStageColor = Windows.UI.Color.FromArgb(255, 239, 68, 68);
                        item.RawSpeedText = "Ошибка";
                        item.RawDetails = errMsg;
                        App.Logger?.Log($"[RuTracker Queue] Ошибка выполнения пакета {item.BatchId}: {errMsg}", LogLevel.Error);
                        foreach (var (t, _, _) in item.CreatedTasks)
                        {
                            RunOnUiSyncOrAsync(() =>
                            {
                                if (t.Status != "Успешно" && t.Status != "Готово")
                                {
                                    t.Status = $"Ошибка: {errMsg}";
                                }
                                t.IsRunning = false;
                                t.IsPaused = false;
                                t.Speed = string.Empty;
                            });
                        }
                    }
                }
                finally
                {
                    item.IsActive = false;
                    item.RawSpeedBps = 0;
                    if (!handledByFinalizer)
                    {
                        try
                        {
                            if (item.RawStageBadge != "ОТМЕНЕНО" && item.RawStageBadge != "ОШИБКА")
                            {
                                item.RawStageBadge = "ГОТОВО";
                                item.RawStageColor = Windows.UI.Color.FromArgb(255, 34, 197, 94);
                                item.RawPercent = 100.0;
                                item.RawSpeedText = "100% Завершено";
                                item.RawDetails = "Сборка завершена";
                                item.IsBuildCompletedOnDisk = true;
                                RecordCompletedBatchIdentities(item);
                            }
                            item.IsActive = false;
                            RunOnUiSyncOrAsync(() =>
                            {
                                item.CommitUiUpdates();
                                SyncBottomDockOrderOnUi();
                                SaveQueueState();
                            });
                        }
                        catch { }
                    }
                    EvaluateQueuePriorities();
                }
            });
        }

        private async Task<bool> HandleSuccessfulBatchCompletionAsync(RuTrackerActiveDownloadItem item)
        {
            if (item == null || item.RawStageBadge is "ОШИБКА" or "ОТМЕНЕНО")
                return false;

            if (item.CreatedTasks == null || item.CreatedTasks.Count == 0)
                return false;

            bool allTasksSucceeded = item.IsBuildCompletedOnDisk || item.CreatedTasks.All(c =>
                c.Task.Status == "Успешно" ||
                c.Task.Status == "Готово" ||
                (!c.Payload.AutoBuild && !c.Task.Status.StartsWith("Ошибка", StringComparison.OrdinalIgnoreCase)));

            if (!allTasksSucceeded)
                return false;

            RecordCompletedBatchIdentities(item);

            bool hadAutoBuild = item.CreatedTasks.Any(c => c.Payload.AutoBuild);
            bool deleteSource = item.DeleteSourceAfterVerify && item.CreatedTasks.All(c => c.Task.DeleteSourceAfterVerify);

            if (deleteSource && hadAutoBuild)
            {
                RemoveCompletedBatchFromQueue(item, disposeItem: true);
                return true;
            }

            if (!deleteSource && item.HasTorrentSource)
            {
                string contentDir = item.CreatedTasks.Count > 1
                    ? item.SharedReleaseDir
                    : (!string.IsNullOrWhiteSpace(item.CreatedTasks[0].Payload.TargetReleaseDir)
                        ? item.CreatedTasks[0].Payload.TargetReleaseDir
                        : item.SharedReleaseDir);

                byte[]? tBytes = item.Service?.LoadedTorrentBytes
                    ?? item.CreatedTasks.Select(c => c.Payload.TorrentBytes).FirstOrDefault(b => b != null && b.Length > 0);
                string magnetUri = item.Service?.LoadedMagnet?.ToV1String()
                    ?? item.CreatedTasks.Select(c => c.Payload.MagnetLink?.ToV1String()).FirstOrDefault(m => !string.IsNullOrEmpty(m))
                    ?? item.CurrentTopic?.MagnetUri
                    ?? string.Empty;
                string topicId = item.CurrentTopic?.TopicId
                    ?? item.CreatedTasks.Select(c => c.Payload.Topic?.TopicId).FirstOrDefault(id => !string.IsNullOrEmpty(id))
                    ?? string.Empty;
                string topicTitle = !string.IsNullOrWhiteSpace(item.CurrentTopic?.Title)
                    ? item.CurrentTopic!.Title
                    : item.DisplayTitle;

                var selectedRels = item.CreatedTasks
                    .SelectMany(c => c.Payload.TorrentFiles ?? new List<TorrentFileSelectionItem>())
                    .Where(f => f.IsSelected)
                    .SelectMany(f => f.GetEffectiveRelativePaths())
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                long totalSelectedSize = item.CreatedTasks
                    .SelectMany(c => c.Payload.TorrentFiles ?? new List<TorrentFileSelectionItem>())
                    .Where(f => f.IsSelected)
                    .GroupBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
                    .Sum(g => Math.Max(0L, g.First().Length));

                var req = new RuTrackerSeedingAddRequest
                {
                    DisplayTitle = item.DisplayTitle,
                    TopicId = topicId,
                    TopicTitle = topicTitle,
                    ContentDir = contentDir,
                    TorrentBytes = tBytes,
                    MagnetLink = magnetUri,
                    SelectedRelativePaths = selectedRels,
                    TotalSelectedSizeBytes = totalSelectedSize
                };

                RemoveCompletedBatchFromQueue(item, disposeItem: false);
                try
                {
                    await RuTrackerSeedingService.Instance.AddOrUpdateSeedingAsync(req).ConfigureAwait(false);
                }
                finally
                {
                    try { item.Dispose(); } catch { }
                }
                return true;
            }

            item.IsBuildCompletedOnDisk = true;
            item.RawStageBadge = "ГОТОВО";
            item.RawStageColor = Windows.UI.Color.FromArgb(255, 34, 197, 94);
            item.RawPercent = 100.0;
            item.RawSpeedText = "100% Завершено";
            item.RawDetails = "Сборка завершена";
            item.IsActive = false;
            RunOnUiSyncOrAsync(() =>
            {
                item.CommitUiUpdates();
                BottomDockItems.Remove(item);
                SaveQueueState();
                TriggerDialogHeaderRefresh();
            });
            return true;
        }

        private void RemoveCompletedBatchFromQueue(RuTrackerActiveDownloadItem item, bool disposeItem)
        {
            lock (_syncLock)
            {
                _backingItems.Remove(item);
            }
            RunOnUiSyncOrAsync(() =>
            {
                Items.Remove(item);
                BottomDockItems.Remove(item);
                SaveQueueState();
                TriggerDialogHeaderRefresh();
            });
            if (disposeItem)
            {
                try { item.Dispose(); } catch { }
            }
        }

        private void TriggerDialogHeaderRefresh()
        {
            RuTrackerActiveDownloadItem[] snapshot;
            lock (_syncLock) { snapshot = _backingItems.ToArray(); }
            int inList = snapshot.Length;
            int dockCount = BottomDockItems.Count;
            int downloading = snapshot.Count(i => i.IsActive && i.EffectiveStage is "СКАЧИВАНИЕ" or "МОДЫ" or "МЕТАДАННЫЕ");
            int inQueue = snapshot.Count(i => i.StageBadge is "В ОЧЕРЕДИ");
            int finished = snapshot.Count(i => i.StageBadge is "ГОТОВО");
            var seedSummary = RuTrackerSeedingService.Instance.GetSummarySnapshot();
            var activeDlg = RuTrackerModDownloaderDialog.ActiveInstance;
            activeDlg?.UpdateHeaderSummary(
                activeDlg.IsDownloadsTabActive ? inList : dockCount,
                downloading, inQueue, finished, 0, 0, 0,
                seedSummary.TotalCount, seedSummary.ActiveSeedingCount, seedSummary.TotalUploadBps,
                seedSummary.TotalUploadedBytes, seedSummary.TotalLeeches, seedSummary.TotalSeeds,
                totalInQueueAndFinished: inList);
        }

        private async Task ExecuteSingleTaskPipelineAsync(
            RuTrackerActiveDownloadItem item,
            ProcessingTask task,
            RuTrackerTaskPayload payload,
            CancellationToken ct)
        {
            var service = item.Service ?? new RuTrackerModService();
            task.HasActiveWorker = true;
            App.RunOnUI(() =>
            {
                task.IsRunning = true;
                task.IsPaused = false;
            });

            // 1. Скачивание торрента / Magnet
            List<string>? downloadedTorrentFiles = null;
            if (payload.HasTorrentSource)
            {
                App.RunOnUI(() => { task.Status = "Загрузка торрента..."; });
                var torrentProg = new Progress<TorrentDownloadProgress>(p =>
                {
                    UpdateItemTorrentProgress(item, p);
                    App.RunOnUI(() =>
                    {
                        task.Progress = Math.Clamp(p.Percent, 0, 100);
                        task.Status = p.StatusText;
                        task.Speed = p.SwarmTotal > p.Seeds
                            ? $"{p.SpeedMbPerSec:F2} МБ/с | Сиды: {p.Seeds} (в рое: {p.SwarmTotal})"
                            : $"{p.SpeedMbPerSec:F2} МБ/с | Сиды: {p.Seeds}";
                    });
                });

                downloadedTorrentFiles = await service.DownloadTorrentAsync(
                    payload.TargetReleaseDir,
                    payload.TorrentFiles,
                    torrentProg,
                    ct,
                    payload.TorrentBytes,
                    payload.MagnetLink,
                    payload.Topic?.TopicId,
                    payload.Topic?.Seeds ?? 0,
                    item.PauseEvent,
                    mgr =>
                    {
                        item.ActiveTorrentManager = mgr;
                        task.ActiveTorrentManager = mgr;
                    },
                    onLimitFileReady: lf =>
                    {
                        item.ActiveLimitFilePath = lf;
                        item.LastWrittenLimitBps = -1;
                    },
                    initialDownloadLimitBps: GetInitialDownloadLimitBpsForNewItem()).ConfigureAwait(false);
            }

            // 2. Скачивание и автораспаковка всех облачных модов / русификаторов
            var discoveredModPayloads = new List<string>();
            if (payload.CloudLinks.Count > 0)
            {
                item.RawStageBadge = "МОДЫ";
                item.RawStageColor = Windows.UI.Color.FromArgb(255, 124, 58, 237);
                App.RunOnUI(() => { task.Status = "Скачивание облачных модов..."; });

                var modProg = new Progress<(double Percent, string Status)>(mp =>
                {
                    UpdateItemCloudProgress(item, mp);
                    App.RunOnUI(() =>
                    {
                        task.Progress = Math.Clamp(mp.Percent, 0, 100);
                        task.Status = mp.Status;
                        if (mp.Status.Contains("МБ/с"))
                        {
                            int sIdx = mp.Status.IndexOf('(');
                            int eIdx = mp.Status.IndexOf(')');
                            if (sIdx >= 0 && eIdx > sIdx)
                                task.Speed = mp.Status.Substring(sIdx + 1, eIdx - sIdx - 1);
                        }
                    });
                });

                var extractedPaths = await service.DownloadAndExtractCloudModsAsync(
                    payload.CloudLinks,
                    payload.TargetReleaseDir,
                    modProg,
                    ct,
                    item.PauseEvent).ConfigureAwait(false);

                foreach (var ep in extractedPaths)
                {
                    if (!discoveredModPayloads.Contains(ep, StringComparer.OrdinalIgnoreCase))
                        discoveredModPayloads.Add(ep);
                }
            }

            // 3. Автоматически распаковываем ТОЛЬКО ВЫБРАННЫЕ архивы модов (.7z / .zip / .rar)
            var allowedSelectedRelPaths = payload.TorrentFiles != null
                ? payload.TorrentFiles.Where(f => f.IsSelected).SelectMany(f => f.GetEffectiveRelativePaths()).ToList()
                : new List<string>();

            await RuTrackerModDownloaderDialog.ExtractLocalTorrentModArchivesAsync(payload.TargetReleaseDir, ct, allowedSelectedRelPaths).ConfigureAwait(false);

            // Сканируем каталог релиза на наличие распакованных romfs / exefs
            var selectedModEntries = payload.TorrentFiles != null
                ? payload.TorrentFiles.Where(f => f.IsSelected && (f.CategoryBadge == "МОДЫ" || f.CategoryBadge == "МОД / РУСИФИКАТОР")).ToList()
                : new List<TorrentFileSelectionItem>();

            bool hasAllowedModsInRelease = selectedModEntries.Count > 0 || payload.CloudLinks.Count > 0;
            if (hasAllowedModsInRelease)
            {
                var allowedModFolderNames = selectedModEntries
                    .SelectMany(f => f.GetEffectiveRelativePaths().Concat(new[] { f.RelativePath }))
                    .Select(p => p.Replace('/', '\\').Trim('\\'))
                    .Select(p => p.Split('\\').FirstOrDefault(s => !string.IsNullOrEmpty(s)))
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Select(s => s!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var modScanDirs = new List<string>();
                if (!string.IsNullOrWhiteSpace(payload.TargetReleaseDir) && Directory.Exists(payload.TargetReleaseDir))
                    modScanDirs.Add(payload.TargetReleaseDir);
                if (!string.IsNullOrWhiteSpace(payload.SharedReleaseDir) &&
                    !string.Equals(payload.SharedReleaseDir, payload.TargetReleaseDir, StringComparison.OrdinalIgnoreCase) &&
                    Directory.Exists(payload.SharedReleaseDir))
                {
                    modScanDirs.Add(payload.SharedReleaseDir);
                }

                foreach (var scanDir in modScanDirs)
                {
                    foreach (var p in RuTrackerModService.DiscoverModPayloadsInDirectory(scanDir))
                    {
                        if (scanDir.Equals(payload.SharedReleaseDir, StringComparison.OrdinalIgnoreCase) &&
                            p.Contains($"{Path.DirectorySeparatorChar}Part_", StringComparison.OrdinalIgnoreCase))
                            continue;
                        bool matchesSelected = allowedModFolderNames.Count == 0 ||
                                               allowedModFolderNames.Any(m => p.Contains(m, StringComparison.OrdinalIgnoreCase)) ||
                                               p.Contains("Extracted_TorrentMod_", StringComparison.OrdinalIgnoreCase);
                        if (matchesSelected && !discoveredModPayloads.Contains(p, StringComparer.OrdinalIgnoreCase))
                            discoveredModPayloads.Add(p);
                    }
                }
            }

            // Выбираем игровые контейнеры
            var candidateContainers = Directory.Exists(payload.TargetReleaseDir)
                ? Directory.EnumerateFiles(payload.TargetReleaseDir, "*.*", SearchOption.AllDirectories)
                    .Where(RuTrackerModDownloaderDialog.IsSupportedGameContainerPath)
                    .ToList()
                : new List<string>();

            List<string> matchedContainers;
            if (payload.PreMatchedContainerPaths != null && payload.PreMatchedContainerPaths.Count > 0)
            {
                matchedContainers = payload.PreMatchedContainerPaths
                    .Where(File.Exists)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            else
            {
                matchedContainers = RuTrackerModDownloaderDialog.FilterDownloadedContainersForSlot(
                    candidateContainers,
                    payload.TorrentFiles ?? new List<TorrentFileSelectionItem>(),
                    payload.SlotNumber,
                    payload.SlotGameTitle,
                    new List<string> { payload.SlotGameTitle });
                if (matchedContainers.Count == 0 && candidateContainers.Count > 0 && RuTrackerModDownloaderDialog.IsDedicatedPartDir(payload.TargetReleaseDir))
                {
                    matchedContainers = candidateContainers;
                }
            }

            // 4. Привязка файлов к задаче на UI-потоке
            var bindTcs = new TaskCompletionSource<bool>();
            App.RunOnUI(() =>
            {
                try
                {
                    RuTrackerModDownloaderDialog.BindDownloadedFilesToExistingTask(task, payload.TargetReleaseDir, discoveredModPayloads, payload.CustomModTitle, matchedContainers);
                }
                finally
                {
                    bindTcs.TrySetResult(true);
                }
            });
            await bindTcs.Task.ConfigureAwait(false);

            // 5. Автоматическая сборка мульти-контента (параллельно до MaxConcurrentBuilds)
            if (payload.AutoBuild && task.InputFiles.Count > 0)
            {
                item.RawStageBadge = "СБОРКА";
                item.RawStageColor = Windows.UI.Color.FromArgb(255, 168, 85, 247); // #A855F7 Purple
                item.RawDetails = "В очереди сборки...";
                item.RawSpeedText = "Сборка";
                RunOnUiSyncOrAsync(() => item.CommitUiUpdates());
                EvaluateQueuePriorities(); // Немедленно освобождаем сетевой слот для следующей закачки в очереди!

                string outExt = task.GetPrimaryExtension();
                string outDir = RuTrackerModDownloaderDialog.ResolveRuTrackerFinalOutputFolder(task.TargetFormat, task.TitleId, task.OutputFolder);
                Directory.CreateDirectory(outDir);
                string outPath = Path.Combine(outDir, task.OutputFileName + outExt);

                App.RunOnUI(() =>
                {
                    task.OutputFolder = outDir;
                    task.Status = "В очереди сборки...";
                    task.Speed = string.Empty;
                });

                await AcquireBuildSlotAsync(ct).ConfigureAwait(false);
                try
                {
                    item.RawDetails = payload.TotalSlots > 1
                        ? $"Сборка ({payload.SlotNumber}/{payload.TotalSlots}): {payload.SlotGameTitle}..."
                        : "Сборка мульти-контента...";
                    RunOnUiSyncOrAsync(() => item.CommitUiUpdates());

                    App.RunOnUI(() =>
                    {
                        task.Status = payload.TotalSlots > 1
                            ? $"Сборка ({payload.SlotNumber}/{payload.TotalSlots}): {payload.SlotGameTitle}..."
                            : "Сборка мульти-контента...";
                        task.Speed = string.Empty;
                        task.Progress = 0;
                    });

                    await App.MultiContent.BuildMultiContentAsync(
                        task,
                        task.InputFiles,
                        outPath,
                        patchFirmware: App.Settings.Current.ForceMultiRebuild,
                        ct).ConfigureAwait(false);
                }
                finally
                {
                    ReleaseBuildSlot();
                }

                if (App.MainDispatcher != null && !App.MainDispatcher.HasThreadAccess)
                {
                    var statusFlushTcs = new TaskCompletionSource<bool>();
                    if (!App.MainDispatcher.TryEnqueue(() => statusFlushTcs.TrySetResult(true)))
                        statusFlushTcs.TrySetResult(true);
                    await Task.WhenAny(statusFlushTcs.Task, Task.Delay(500)).ConfigureAwait(false);
                }

                string builtContainer = Path.Combine(outDir, task.OutputFileName + outExt);
                if (!File.Exists(builtContainer))
                {
                    string fallbackExt = outExt.Equals(".nsz", StringComparison.OrdinalIgnoreCase)
                        ? ".nsp"
                        : (outExt.Equals(".xcz", StringComparison.OrdinalIgnoreCase) ? ".xci" : outExt);
                    string altContainer = Path.Combine(outDir, task.OutputFileName + fallbackExt);
                    if (File.Exists(altContainer))
                        builtContainer = altContainer;
                }

                bool fileExistsOnDisk = File.Exists(builtContainer) && new FileInfo(builtContainer).Length > 0;
                bool buildSucceeded = fileExistsOnDisk &&
                                      !task.Status.StartsWith("Ошибка", StringComparison.OrdinalIgnoreCase) &&
                                      !task.Status.StartsWith("Отмен", StringComparison.OrdinalIgnoreCase);

                if (!buildSucceeded && task.Status.StartsWith("Ошибка", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(task.Status);
                }

                if (buildSucceeded && payload.TotalSlots <= 1)
                {
                    item.IsBuildCompletedOnDisk = true;
                }

                try
                {
                    if (fileExistsOnDisk)
                    {
                        RuTrackerModDownloaderDialog.TryExtractAndSetTaskIcon(task, new[] { builtContainer });
                    }
                }
                catch { }

                task.EnsureFinalMetricsPopulated(builtContainer);

                if (task.DeleteSourceAfterVerify && buildSucceeded)
                {
                    try
                    {
                        RuTrackerModDownloaderDialog.CleanupRuTrackerReleaseDirectory(payload.TargetReleaseDir, outDir);
                        // Multi-slot SharedReleaseDir is owned by ExecuteMultiTaskPipelineAsync and deleted ONLY after ALL slots complete
                        if (payload.TotalSlots <= 1 &&
                            !string.IsNullOrWhiteSpace(payload.SharedReleaseDir) &&
                            !string.Equals(payload.SharedReleaseDir, payload.TargetReleaseDir, StringComparison.OrdinalIgnoreCase))
                        {
                            RuTrackerModDownloaderDialog.CleanupRuTrackerReleaseDirectory(payload.SharedReleaseDir, outDir);
                        }
                    }
                    catch { }
                }

                var finFlushTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                if (App.MainDispatcher != null && !App.MainDispatcher.HasThreadAccess)
                {
                    if (!App.MainDispatcher.TryEnqueue(() =>
                    {
                        try
                        {
                            if (buildSucceeded || (!task.Status.StartsWith("Ошибка", StringComparison.OrdinalIgnoreCase) && !task.Status.StartsWith("Отмен", StringComparison.OrdinalIgnoreCase)))
                            {
                                task.Status = "Успешно";
                                task.Progress = 100;
                            }
                            task.IsRunning = false;
                            task.IsPaused = false;
                            task.Speed = string.Empty;
                            task.FinishedAt = DateTime.Now;
                            item.ViewModel?.SavePersistedTasks();
                        }
                        finally { finFlushTcs.TrySetResult(true); }
                    }))
                    {
                        finFlushTcs.TrySetResult(true);
                    }
                    await Task.WhenAny(finFlushTcs.Task, Task.Delay(500)).ConfigureAwait(false);
                }
                else
                {
                    if (buildSucceeded || (!task.Status.StartsWith("Ошибка", StringComparison.OrdinalIgnoreCase) && !task.Status.StartsWith("Отмен", StringComparison.OrdinalIgnoreCase)))
                    {
                        task.Status = "Успешно";
                        task.Progress = 100;
                    }
                    task.IsRunning = false;
                    task.IsPaused = false;
                    task.Speed = string.Empty;
                    task.FinishedAt = DateTime.Now;
                    item.ViewModel?.SavePersistedTasks();
                }

                if (buildSucceeded && payload.TotalSlots <= 1)
                {
                    RecordCompletedBatchIdentities(item);
                    item.RawStageBadge = "ГОТОВО";
                    item.RawStageColor = Windows.UI.Color.FromArgb(255, 34, 197, 94);
                    item.RawPercent = 100.0;
                    item.RawSpeedText = "100% Завершено";
                    item.RawDetails = "Сборка завершена";
                    item.IsActive = false;
                    RunOnUiSyncOrAsync(() =>
                    {
                        item.CommitUiUpdates();
                        SyncBottomDockOrderOnUi();
                        SaveQueueState();
                    });
                    try { EvaluateQueuePriorities(); } catch { }
                }
            }
            else if (payload.AutoBuild)
            {
                App.RunOnUI(() =>
                {
                    task.Status = "Ошибка";
                    task.Progress = 0;
                    task.IsRunning = false;
                    task.IsPaused = false;
                    task.Speed = string.Empty;
                    task.FinishedAt = DateTime.Now;
                    task.LogDetails += $"\n❌ Ошибка: Не найдены игровые файлы для сборки в каталоге {payload.TargetReleaseDir}.";
                });
                throw new InvalidOperationException($"Не найдены игровые файлы для сборки части #{payload.SlotNumber} ({payload.SlotGameTitle}).");
            }
            else
            {
                item.IsBuildCompletedOnDisk = true;
                App.RunOnUI(() =>
                {
                    task.IsRuTrackerTask = false;
                    task.Status = task.InputFiles.Count > 0 ? "Ожидание" : "Успешно";
                    task.Progress = task.InputFiles.Count > 0 ? 0 : 100;
                    task.IsRunning = false;
                    task.IsPaused = false;
                    task.Speed = string.Empty;
                    task.FinishedAt = DateTime.Now;
                });
            }

            task.HasActiveWorker = false;
            RunOnUiSyncOrAsync(() =>
            {
                try { item.ViewModel?.SavePersistedTasks(); } catch { }
            });
        }

        private async Task ExecuteMultiTaskPipelineAsync(
            RuTrackerActiveDownloadItem item,
            CancellationToken ct)
        {
            var createdTasks = item.CreatedTasks;
            var sharedReleaseDir = item.SharedReleaseDir;
            var hasTorrentSource = item.HasTorrentSource;
            var currentTopic = item.CurrentTopic;
            var service = item.Service ?? new RuTrackerModService();

            var firstTask = createdTasks[0].Task;
            var unionFiles = new List<TorrentFileSelectionItem>();

            // 1. Скачиваем объединенный набор файлов торрента в общую папку
            if (hasTorrentSource)
            {
                unionFiles = createdTasks
                    .SelectMany(c => c.Payload.TorrentFiles)
                    .GroupBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.FirstOrDefault(x => x.IsSelected) ?? g.First())
                    .ToList();

                var torrentProg = new Progress<TorrentDownloadProgress>(p =>
                {
                    UpdateItemTorrentProgress(item, p);
                    App.RunOnUI(() =>
                    {
                        foreach (var (t, _, _) in createdTasks)
                        {
                            t.Progress = Math.Clamp(p.Percent, 0, 100);
                            t.Status = p.StatusText;
                            t.Speed = p.SwarmTotal > p.Seeds
                                ? $"{p.SpeedMbPerSec:F2} МБ/с | Сиды: {p.Seeds} (в рое: {p.SwarmTotal})"
                                : $"{p.SpeedMbPerSec:F2} МБ/с | Сиды: {p.Seeds}";
                        }
                    });
                });

                byte[]? effectiveTorrentBytes = service.LoadedTorrentBytes
                    ?? createdTasks.Select(c => c.Payload.TorrentBytes).FirstOrDefault(b => b != null && b.Length > 0);
                var effectiveMagnet = service.LoadedMagnet
                    ?? createdTasks.Select(c => c.Payload.MagnetLink).FirstOrDefault(m => m != null);
                var fallbackTopic = currentTopic
                    ?? createdTasks.Select(c => c.Payload.Topic).FirstOrDefault(tp => tp != null);

                await service.DownloadTorrentAsync(
                    sharedReleaseDir,
                    unionFiles,
                    torrentProg,
                    ct,
                    effectiveTorrentBytes,
                    effectiveMagnet,
                    fallbackTopic?.TopicId,
                    fallbackTopic?.Seeds ?? 0,
                    item.PauseEvent,
                    mgr =>
                    {
                        item.ActiveTorrentManager = mgr;
                        foreach (var (t, _, _) in createdTasks)
                            t.ActiveTorrentManager = mgr;
                    },
                    onLimitFileReady: lf =>
                    {
                        item.ActiveLimitFilePath = lf;
                        item.LastWrittenLimitBps = -1;
                    },
                    initialDownloadLimitBps: GetInitialDownloadLimitBpsForNewItem()).ConfigureAwait(false);
            }

            // Распаковываем ТОЛЬКО ВЫБРАННЫЕ архивы модов (.7z / .zip / .rar)
            var unionSelectedRelPaths = unionFiles
                .Where(f => f.IsSelected)
                .SelectMany(f => f.GetEffectiveRelativePaths())
                .ToList();
            await RuTrackerModDownloaderDialog.ExtractLocalTorrentModArchivesAsync(sharedReleaseDir, ct, unionSelectedRelPaths).ConfigureAwait(false);

            // Собираем все игровые контейнеры в общей папке раздачи и распределяем по всем слотам ДО сборки первого слота
            var allDownloadedContainers = Directory.Exists(sharedReleaseDir)
                ? Directory.EnumerateFiles(sharedReleaseDir, "*.*", SearchOption.AllDirectories)
                    .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}Part_", StringComparison.OrdinalIgnoreCase) &&
                                RuTrackerModDownloaderDialog.IsSupportedGameContainerPath(p))
                    .ToList()
                : new List<string>();

            var allSlotTitles = createdTasks.Select(c => c.Payload.SlotGameTitle).ToList();

            // Phase 1: Pre-stage all slots' containers before building slot 0
            var perSlotMatches = new List<List<string>>(createdTasks.Count);
            var containerRefCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var (_, payload, slot) in createdTasks)
            {
                var matched = RuTrackerModDownloaderDialog.FilterDownloadedContainersForSlot(
                    allDownloadedContainers,
                    payload.TorrentFiles,
                    slot.SlotNumber,
                    payload.SlotGameTitle,
                    allSlotTitles);
                perSlotMatches.Add(matched);
                foreach (var srcPath in matched)
                {
                    containerRefCounts[srcPath] = containerRefCounts.TryGetValue(srcPath, out int c) ? c + 1 : 1;
                }
            }

            bool preserveForSeeding = hasTorrentSource && !createdTasks.Any(c => c.Task.DeleteSourceAfterVerify);
            if (!preserveForSeeding)
            {
                var remainingMoveCounts = new Dictionary<string, int>(containerRefCounts, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < createdTasks.Count; i++)
                {
                    var (_, payload, _) = createdTasks[i];
                    Directory.CreateDirectory(payload.TargetReleaseDir);
                    foreach (var srcContainer in perSlotMatches[i])
                    {
                        int rem = remainingMoveCounts.TryGetValue(srcContainer, out int rCount) ? rCount - 1 : 0;
                        remainingMoveCounts[srcContainer] = rem;

                        string dstFile = Path.Combine(payload.TargetReleaseDir, Path.GetFileName(srcContainer));
                        if (string.Equals(Path.GetFullPath(srcContainer), Path.GetFullPath(dstFile), StringComparison.OrdinalIgnoreCase) || File.Exists(dstFile))
                            continue;
                        if (!File.Exists(srcContainer))
                            continue;

                        try
                        {
                            if (rem > 0)
                                File.Copy(srcContainer, dstFile, true);
                            else
                                File.Move(srcContainer, dstFile);
                        }
                        catch (Exception moveEx)
                        {
                            App.Logger?.Log($"[RuTracker Queue] Ошибка подготовки файла '{Path.GetFileName(srcContainer)}' для части #{payload.SlotNumber}: {moveEx.Message}", LogLevel.Warning);
                            try
                            {
                                if (File.Exists(srcContainer)) File.Copy(srcContainer, dstFile, true);
                            }
                            catch (Exception copyEx)
                            {
                                App.Logger?.Log($"[RuTracker Queue] Повторное копирование '{Path.GetFileName(srcContainer)}' не удалось: {copyEx.Message}", LogLevel.Warning);
                            }
                        }
                    }
                }
            }

            // Phase 2: Для каждой части/игры выполняем её облачные моды и сборку с изоляцией ошибок по слотам
            for (int i = 0; i < createdTasks.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var (task, payload, _) = createdTasks[i];

                item.RawStageBadge = "СБОРКА";
                item.RawStageColor = Windows.UI.Color.FromArgb(255, 168, 85, 247);
                item.RawDetails = $"Сборка ({i + 1}/{createdTasks.Count}): {payload.SlotGameTitle}...";
                item.RawSpeedText = $"Часть {i + 1}/{createdTasks.Count}";
                RunOnUiSyncOrAsync(() => item.CommitUiUpdates());

                var slotExecPayload = new RuTrackerTaskPayload
                {
                    Topic = payload.Topic,
                    TorrentBytes = payload.TorrentBytes,
                    MagnetLink = payload.MagnetLink,
                    TorrentFiles = payload.TorrentFiles,
                    CloudLinks = payload.CloudLinks,
                    TargetReleaseDir = payload.TargetReleaseDir,
                    CustomModTitle = payload.CustomModTitle,
                    AutoBuild = payload.AutoBuild,
                    HasTorrentSource = false,
                    SlotNumber = payload.SlotNumber,
                    TotalSlots = payload.TotalSlots,
                    SlotGameTitle = payload.SlotGameTitle,
                    SharedReleaseDir = sharedReleaseDir,
                    AppendAutoTags = payload.AppendAutoTags,
                    CustomMetadata = payload.CustomMetadata,
                    PreMatchedContainerPaths = preserveForSeeding ? perSlotMatches[i] : null
                };

                try
                {
                    await ExecuteSingleTaskPipelineAsync(item, task, slotExecPayload, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception slotEx)
                {
                    App.Logger?.Log($"[RuTracker Queue Multi] Ошибка обработки части #{payload.SlotNumber} ({payload.SlotGameTitle}): {slotEx.Message}", LogLevel.Error);
                    RunOnUiSyncOrAsync(() =>
                    {
                        if (task.Status != "Ошибка" && task.Status != "Отменено")
                            task.Status = "Ошибка";
                        task.IsRunning = false;
                        task.IsPaused = false;
                        task.Speed = string.Empty;
                        task.LogDetails += $"\n❌ Ошибка части #{payload.SlotNumber}: {slotEx.Message}";
                        item.ViewModel?.SavePersistedTasks();
                    });
                }
            }

            // Phase 3: Итоговая очистка общей временной папки и точный статус многосоставной раздачи
            int okCount = createdTasks.Count(c => c.Task.Status == "Успешно" || c.Task.Status == "Готово");
            bool allSucceeded = okCount == createdTasks.Count;

            if (allSucceeded && createdTasks.Any(c => c.Task.DeleteSourceAfterVerify))
            {
                try
                {
                    var primaryTask = createdTasks[0].Task;
                    string primaryOutDir = RuTrackerModDownloaderDialog.ResolveRuTrackerFinalOutputFolder(
                        primaryTask.TargetFormat,
                        primaryTask.TitleId,
                        primaryTask.OutputFolder);
                    RuTrackerModDownloaderDialog.CleanupRuTrackerReleaseDirectory(sharedReleaseDir, primaryOutDir);
                }
                catch { }
            }

            if (allSucceeded)
            {
                item.IsBuildCompletedOnDisk = true;
                RecordCompletedBatchIdentities(item);
                item.RawStageBadge = "ГОТОВО";
                item.RawStageColor = Windows.UI.Color.FromArgb(255, 34, 197, 94);
                item.RawPercent = 100.0;
                item.RawSpeedText = "100% Завершено";
                item.RawDetails = createdTasks.Count > 1 ? $"Сборка завершена ({createdTasks.Count} игр)" : "Сборка завершена";
            }
            else
            {
                item.IsBuildCompletedOnDisk = okCount > 0;
                item.RawStageBadge = "ОШИБКА";
                item.RawStageColor = Windows.UI.Color.FromArgb(255, 239, 68, 68);
                item.RawSpeedText = "Ошибка";
                item.RawDetails = $"Собрано {okCount} из {createdTasks.Count} игр (см. лог задач)";
            }

            item.IsActive = false;
            RunOnUiSyncOrAsync(() =>
            {
                item.CommitUiUpdates();
                SyncBottomDockOrderOnUi();
                SaveQueueState();
            });
            try { EvaluateQueuePriorities(); } catch { }
        }

        public static void UpdateItemTorrentProgress(RuTrackerActiveDownloadItem item, TorrentDownloadProgress p)
        {
            if (item.IsPausedByUser || item.IsPreemptedByQueue)
                return;
            item.RawPercent = Math.Clamp(p.Percent, 0, 100);
            item.RawSpeedBps = p.SpeedMbPerSec * 1024.0 * 1024.0;
            item.RawSeeds = p.Seeds;
            item.RawPeers = p.Peers;

            if (p.TotalBytes > 0)
            {
                double dlMb = p.DownloadedBytes / (1024.0 * 1024.0);
                double totMb = p.TotalBytes / (1024.0 * 1024.0);
                item.RawDetails = $"{dlMb:F1} / {totMb:F1} МБ ({p.Percent:F1}%)";
            }
            else
            {
                item.RawDetails = p.IsMetadataPhase ? "Поиск пиров..." : $"{p.Percent:F1}%";
            }

            item.RawSpeedText = p.Peers > 0 ? $"{p.SpeedMbPerSec:F2} МБ/с • Сиды: {p.Seeds} • Пиры: {p.Peers}" : $"{p.SpeedMbPerSec:F2} МБ/с • Сиды: {p.Seeds}";

            if (p.IsMetadataPhase)
            {
                item.RawStageBadge = "МЕТАДАННЫЕ";
                item.RawStageColor = Windows.UI.Color.FromArgb(255, 14, 165, 233);
            }
            else
            {
                item.RawStageBadge = "СКАЧИВАНИЕ";
                item.RawStageColor = Windows.UI.Color.FromArgb(255, 2, 132, 199);
            }
        }

        public static void UpdateItemCloudProgress(RuTrackerActiveDownloadItem item, (double Percent, string Status) mp)
        {
            if (item.IsPausedByUser || item.IsPreemptedByQueue)
                return;
            item.RawPercent = Math.Clamp(mp.Percent, 0, 100);
            item.RawDetails = $"{mp.Percent:F1}%";
            item.RawStageBadge = "МОДЫ";
            item.RawStageColor = Windows.UI.Color.FromArgb(255, 124, 58, 237);
            if (mp.Status.Contains("МБ/с"))
            {
                int sIdx = mp.Status.IndexOf('(');
                int eIdx = mp.Status.IndexOf(')');
                if (sIdx >= 0 && eIdx > sIdx)
                    item.RawSpeedText = mp.Status.Substring(sIdx + 1, eIdx - sIdx - 1);
            }
        }

                public void TogglePauseItem(RuTrackerActiveDownloadItem item)
        {
            lock (_syncLock)
            {
                if (item.IsPausedByUser || item.StageBadge == "ПАУЗА" || item.RawStageBadge == "ПАУЗА")
                {
                    item.IsPausedByUser = false;
                    item.IsPreemptedByQueue = false;
                    item.LastActivatedAtUtc = DateTime.UtcNow;
                    item.SmoothedSpeedBps = 0;
                    item.RawStageBadge = "В ОЧЕРЕДИ";
                    item.StageBadge = "В ОЧЕРЕДИ";
                    item.PauseEvent.Set();
                    if (item.ActiveTorrentManager != null &&
                        (item.ActiveTorrentManager.State == TorrentState.Paused || item.ActiveTorrentManager.State == TorrentState.Stopped))
                    {
                        try { item.ActiveTorrentManager.StartAsync(); } catch { }
                    }
                    EvaluateQueuePriorities();
                    RunOnUiSyncOrAsync(() => item.CommitUiUpdates());
                    SaveQueueState();
                }
                else
                {
                    item.IsPausedByUser = true;
                    item.PauseEvent.Reset();
                    if (item.ActiveTorrentManager != null &&
                        (item.ActiveTorrentManager.State == TorrentState.Downloading || item.ActiveTorrentManager.State == TorrentState.Metadata))
                    {
                        try { item.ActiveTorrentManager.PauseAsync(); } catch { }
                    }
                    item.RawStageBadge = "ПАУЗА";
                    item.RawStageColor = Windows.UI.Color.FromArgb(255, 217, 119, 6); // Amber #D97706
                    item.RawSpeedBps = 0;
                    item.RawSpeedText = "Пауза";
                    item.RawDetails = "Приостановлено пользователем";

                    foreach (var (t, _, _) in item.CreatedTasks)
                    {
                        RunOnUiSyncOrAsync(() =>
                        {
                            t.Status = "Пауза";
                            t.Speed = string.Empty;
                            t.IsPaused = true;
                        });
                    }
                    EvaluateQueuePriorities();
                }

                item.CommitUiUpdates();
                SaveQueueState();
            }
        }

        public void CancelOrRemoveItem(RuTrackerActiveDownloadItem item)
        {
            lock (_syncLock)
            {
                if (item.StageBadge is "ГОТОВО" or "ОТМЕНЕНО" or "ОШИБКА")
                {
                    _backingItems.Remove(item);
                    RunOnUiSyncOrAsync(() =>
                    {
                        Items.Remove(item);
                        BottomDockItems.Remove(item);
                        SaveQueueState();
                        TriggerDialogHeaderRefresh();
                    });
                    item.Dispose();
                    return;
                }

                item.Cts?.Cancel();
                if (item.ActiveTorrentManager != null)
                {
                    try { item.ActiveTorrentManager.StopAsync(); } catch { }
                }
                item.IsActive = false;
                item.RawStageBadge = "ОТМЕНЕНО";
                item.RawStageColor = Windows.UI.Color.FromArgb(255, 220, 38, 38);
                item.RawSpeedBps = 0;
                item.RawSpeedText = "Отменено";
                item.RawDetails = "Отменено пользователем";

                foreach (var (t, _, _) in item.CreatedTasks)
                {
                    RunOnUiSyncOrAsync(() =>
                    {
                        t.Status = "Отменено";
                        t.IsRunning = false;
                        t.IsPaused = false;
                        t.Speed = string.Empty;
                    });
                }

                item.CommitUiUpdates();
                EvaluateQueuePriorities();
                SaveQueueState();
            }
        }

        public void MoveItemUp(RuTrackerActiveDownloadItem item)
        {
            lock (_syncLock)
            {
                int idx = _backingItems.IndexOf(item);
                if (idx > 0)
                {
                    var temp = _backingItems[idx];
                    _backingItems[idx] = _backingItems[idx - 1];
                    _backingItems[idx - 1] = temp;

                    RunOnUiSyncOrAsync(() =>
                    {
                        int uiIdx = Items.IndexOf(item);
                        if (uiIdx > 0)
                        {
                            Items.Move(uiIdx, uiIdx - 1);
                        }
                        SyncBottomDockOrderOnUi();
                    });

                    EvaluateQueuePriorities(allowPreemptionForReorder: true);
                    SaveQueueState();
                }
            }
        }

        public void MoveItemDown(RuTrackerActiveDownloadItem item)
        {
            lock (_syncLock)
            {
                int idx = _backingItems.IndexOf(item);
                if (idx >= 0 && idx < _backingItems.Count - 1)
                {
                    var temp = _backingItems[idx];
                    _backingItems[idx] = _backingItems[idx + 1];
                    _backingItems[idx + 1] = temp;

                    RunOnUiSyncOrAsync(() =>
                    {
                        int uiIdx = Items.IndexOf(item);
                        if (uiIdx >= 0 && uiIdx < Items.Count - 1)
                        {
                            Items.Move(uiIdx, uiIdx + 1);
                        }
                        SyncBottomDockOrderOnUi();
                    });

                    EvaluateQueuePriorities(allowPreemptionForReorder: true);
                    SaveQueueState();
                }
            }
        }

        public void PauseAll()
        {
            lock (_syncLock)
            {
                foreach (var item in _backingItems.Where(i => i.StageBadge is not ("ГОТОВО" or "ОТМЕНЕНО" or "ОШИБКА")))
                {
                    if (!item.IsPausedByUser)
                    {
                        TogglePauseItem(item);
                    }
                }
            }
        }

        public void ResumeAll()
        {
            lock (_syncLock)
            {
                DateTime nowUtc = DateTime.UtcNow;
                foreach (var item in _backingItems.Where(i => i.IsPausedByUser || i.StageBadge == "ПАУЗА" || i.RawStageBadge == "ПАУЗА"))
                {
                    item.IsPausedByUser = false;
                    item.IsPreemptedByQueue = false;
                    item.LastActivatedAtUtc = nowUtc;
                    item.SmoothedSpeedBps = 0;
                    item.RawStageBadge = "В ОЧЕРЕДИ";
                    item.StageBadge = "В ОЧЕРЕДИ";
                    item.PauseEvent.Set();
                }
                EvaluateQueuePriorities();
                RunOnUiSyncOrAsync(() =>
                {
                    foreach (var item in _backingItems)
                        item.CommitUiUpdates();
                });
                SaveQueueState();
            }
        }

        public void ClearFinished()
        {
            lock (_syncLock)
            {
                var toDispose = _backingItems.Where(i => i.StageBadge is "ГОТОВО" or "ОТМЕНЕНО" or "ОШИБКА").ToList();
                _backingItems.RemoveAll(i => i.StageBadge is "ГОТОВО" or "ОТМЕНЕНО" or "ОШИБКА");
                RunOnUiSyncOrAsync(() =>
                {
                    var finished = Items.Where(i => i.StageBadge is "ГОТОВО" or "ОТМЕНЕНО" or "ОШИБКА").ToList();
                    foreach (var item in finished)
                    {
                        Items.Remove(item);
                        BottomDockItems.Remove(item);
                    }
                    SyncBottomDockOrderOnUi();
                    SaveQueueState();
                    TriggerDialogHeaderRefresh();
                });
                foreach (var item in toDispose)
                {
                    item.Dispose();
                }
            }
        }

        public long GetInitialDownloadLimitBpsForNewItem()
        {
            var s = App.Settings.Current;
            string mode = s.RuTrackerSpeedLimitMode ?? "AutoHeadroom";
            if (string.Equals(mode, "Custom", StringComparison.OrdinalIgnoreCase))
            {
                double mb = s.RuTrackerGlobalSpeedLimitMbPerSec;
                return mb > 0.05 ? (long)Math.Max(256L * 1024L, mb * 1024.0 * 1024.0) : 0L;
            }
            // Both Unlimited and AutoHeadroom always start uncapped; AutoHeadroom only steps in near measured saturation
            return 0L;
        }

        public void NotifySpeedLimitSettingsChanged()
        {
            lock (_limitCalcLock)
            {
                _probeStartedUtc = DateTime.MinValue;
                _pinnedHighSinceUtc = DateTime.MinValue;
                _lastReprobeUtc = DateTime.MinValue;
                _lastLimitWriteUtc = DateTime.MinValue;
                _activeTransferStartedUtc = DateTime.MinValue;
                _limitTickCounter = 0;
            }
            RuTrackerActiveDownloadItem[] snapshot;
            lock (_syncLock) { snapshot = _backingItems.ToArray(); }
            double totalBps = snapshot
                .Where(i => i.IsActive && i.EffectiveStage is "СКАЧИВАНИЕ" or "МЕТАДАННЫЕ")
                .Sum(i => Math.Max(0.0, i.RawSpeedBps));
            foreach (var it in snapshot)
                it.LastWrittenLimitBps = -1;
            ApplyGlobalSpeedLimitDistribution(snapshot, totalBps, forceWrite: true);
        }

        private void ApplyGlobalSpeedLimitDistribution(RuTrackerActiveDownloadItem[] snapshot, double totalSpeedBps, bool forceWrite = false)
        {
            var activeDownloaders = snapshot
                .Where(i => i.IsActivelyDownloading && i.EffectiveStage is "СКАЧИВАНИЕ" or "МЕТАДАННЫЕ")
                .ToList();
            if (activeDownloaders.Count == 0)
            {
                lock (_limitCalcLock)
                {
                    _activeTransferStartedUtc = DateTime.MinValue;
                }
                return;
            }

            var s = App.Settings.Current;
            string mode = s.RuTrackerSpeedLimitMode ?? "AutoHeadroom";
            long globalBudgetBps = 0L;
            DateTime nowUtc = DateTime.UtcNow;

            if (string.Equals(mode, "Unlimited", StringComparison.OrdinalIgnoreCase))
            {
                globalBudgetBps = 0L;
            }
            else if (string.Equals(mode, "Custom", StringComparison.OrdinalIgnoreCase))
            {
                double mb = s.RuTrackerGlobalSpeedLimitMbPerSec;
                globalBudgetBps = mb > 0.05 ? (long)Math.Max(256L * 1024L, mb * 1024.0 * 1024.0) : 0L;
            }
            else
            {
                lock (_limitCalcLock)
                {
                    if (_observedPeakBps <= 0.0)
                    {
                        if (s.RuTrackerObservedPeakBandwidthMbPerSec >= 6.0)
                            _observedPeakBps = s.RuTrackerObservedPeakBandwidthMbPerSec * 1024.0 * 1024.0;
                        else if (s.RuTrackerObservedPeakBandwidthMbPerSec > 0.0 && s.RuTrackerObservedPeakBandwidthMbPerSec < 6.0)
                        {
                            // Discard poisoned low peak estimates saved during short warmup
                            s.RuTrackerObservedPeakBandwidthMbPerSec = 0.0;
                        }
                    }

                    if (totalSpeedBps >= 512.0 * 1024.0 && _activeTransferStartedUtc == DateTime.MinValue)
                        _activeTransferStartedUtc = nowUtc;

                    if (totalSpeedBps > _observedPeakBps && totalSpeedBps >= 2.0 * 1024.0 * 1024.0)
                    {
                        _observedPeakBps = totalSpeedBps;
                        if ((nowUtc - _lastPeakSaveUtc).TotalSeconds >= 15.0)
                        {
                            _lastPeakSaveUtc = nowUtc;
                            double peakMb = Math.Round(_observedPeakBps / (1024.0 * 1024.0), 2);
                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    s.RuTrackerObservedPeakBandwidthMbPerSec = peakMb;
                                    await App.Settings.SaveAsync().ConfigureAwait(false);
                                }
                                catch { }
                            });
                        }
                    }

                    bool warmupDone = _observedPeakBps >= 8.0 * 1024.0 * 1024.0 &&
                                      _activeTransferStartedUtc != DateTime.MinValue &&
                                      (nowUtc - _activeTransferStartedUtc).TotalSeconds >= 20.0;

                    if (!warmupDone)
                    {
                        globalBudgetBps = 0L;
                    }
                    else
                    {
                        double reserveMb = Math.Max(0.5, s.RuTrackerAutoHeadroomReserveMbPerSec);
                        long reserveBps = (long)(reserveMb * 1024.0 * 1024.0);
                        long targetBudget = Math.Max(4L * 1024L * 1024L, (long)_observedPeakBps - reserveBps);

                        bool inReprobeWindow = _probeStartedUtc != DateTime.MinValue && (nowUtc - _probeStartedUtc).TotalSeconds < 8.0;
                        if (inReprobeWindow)
                        {
                            globalBudgetBps = 0L;
                        }
                        else
                        {
                            if (_probeStartedUtc != DateTime.MinValue && (nowUtc - _probeStartedUtc).TotalSeconds >= 8.0)
                                _probeStartedUtc = DateTime.MinValue;

                            // Never throttle individual torrents when aggregate speed is below 88% of target budget
                            if (totalSpeedBps < targetBudget * 0.88)
                            {
                                _pinnedHighSinceUtc = DateTime.MinValue;
                                globalBudgetBps = 0L;
                            }
                            else
                            {
                                if (totalSpeedBps >= targetBudget * 0.92)
                                {
                                    if (_pinnedHighSinceUtc == DateTime.MinValue)
                                        _pinnedHighSinceUtc = nowUtc;
                                    else if ((nowUtc - _pinnedHighSinceUtc).TotalSeconds >= 20.0 &&
                                             (_lastReprobeUtc == DateTime.MinValue || (nowUtc - _lastReprobeUtc).TotalSeconds >= 60.0))
                                    {
                                        _pinnedHighSinceUtc = DateTime.MinValue;
                                        _lastReprobeUtc = nowUtc;
                                        _probeStartedUtc = nowUtc;
                                        globalBudgetBps = 0L;
                                    }
                                    else
                                    {
                                        globalBudgetBps = targetBudget;
                                    }
                                }
                                else
                                {
                                    _pinnedHighSinceUtc = DateTime.MinValue;
                                    globalBudgetBps = targetBudget;
                                }
                            }
                        }
                    }
                }
            }

            bool isCustomMode = string.Equals(mode, "Custom", StringComparison.OrdinalIgnoreCase);
            long minDeltaBps = isCustomMode ? 512L * 1024L : 1024L * 1024L;
            double minWriteIntervalSec = isCustomMode ? 2.0 : 3.0;
            bool cooldownOk = forceWrite || _lastLimitWriteUtc == DateTime.MinValue || (nowUtc - _lastLimitWriteUtc).TotalSeconds >= minWriteIntervalSec;

            var transferring = activeDownloaders.Where(i => i.RawSpeedBps >= 128.0 * 1024.0).ToList();
            var desiredLimits = new Dictionary<RuTrackerActiveDownloadItem, long>(activeDownloaders.Count);

            if (globalBudgetBps <= 0L)
            {
                foreach (var it in activeDownloaders)
                    desiredLimits[it] = 0L;
            }
            else if (isCustomMode && (totalSpeedBps < globalBudgetBps * 0.88 || transferring.Count <= 1))
            {
                foreach (var it in activeDownloaders)
                    desiredLimits[it] = globalBudgetBps;
            }
            else if (transferring.Count <= 1)
            {
                foreach (var it in activeDownloaders)
                    desiredLimits[it] = transferring.Count == 1 && ReferenceEquals(it, transferring[0]) ? globalBudgetBps : 0L;
            }
            else
            {
                double[] weights = new double[transferring.Count];
                double weightSum = 0.0;
                for (int k = 0; k < transferring.Count; k++)
                {
                    double w = Math.Max(512.0 * 1024.0, transferring[k].RawSpeedBps * 1.25 + 512.0 * 1024.0);
                    weights[k] = w;
                    weightSum += w;
                }
                for (int k = 0; k < transferring.Count; k++)
                {
                    long alloc = Math.Max(512L * 1024L, (long)(globalBudgetBps * (weights[k] / Math.Max(1.0, weightSum))));
                    desiredLimits[transferring[k]] = alloc;
                }
                foreach (var it in activeDownloaders)
                {
                    if (!desiredLimits.ContainsKey(it))
                        desiredLimits[it] = 0L; // Keep stalled 0-seed torrents uncapped so they don't consume budget
                }
            }

            var updates = new List<(RuTrackerActiveDownloadItem Item, string? LimitFile, long AllocBps)>(activeDownloaders.Count);
            foreach (var it in activeDownloaders)
            {
                long alloc = desiredLimits[it];
                if (alloc == 0L)
                {
                    if (it.LastWrittenLimitBps != 0L)
                        updates.Add((it, it.ActiveLimitFilePath, 0L));
                }
                else if (cooldownOk && (it.LastWrittenLimitBps <= 0L || Math.Abs(alloc - it.LastWrittenLimitBps) >= minDeltaBps))
                {
                    updates.Add((it, it.ActiveLimitFilePath, alloc));
                }
            }

            if (updates.Count == 0)
                return;

            _lastLimitWriteUtc = nowUtc;

            foreach (var (it, _, alloc) in updates)
            {
                it.LastWrittenLimitBps = alloc;
                var engine = it.ActiveTorrentManager?.Engine;
                if (engine != null)
                {
                    int rate = alloc <= 0 ? 0 : (int)Math.Min(int.MaxValue, alloc);
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            var sb = new MonoTorrent.Client.EngineSettingsBuilder(engine.Settings)
                            {
                                MaximumDownloadRate = rate
                            };
                            await engine.UpdateSettingsAsync(sb.ToSettings()).ConfigureAwait(false);
                        }
                        catch { }
                    });
                }
            }

            _ = Task.Run(() =>
            {
                foreach (var (_, lfPath, alloc) in updates)
                {
                    if (string.IsNullOrEmpty(lfPath)) continue;
                    try
                    {
                        string tmp = lfPath + ".tmp";
                        File.WriteAllText(tmp, alloc.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        File.Move(tmp, lfPath, overwrite: true);
                    }
                    catch
                    {
                        try { File.WriteAllText(lfPath, alloc.ToString(System.Globalization.CultureInfo.InvariantCulture)); } catch { }
                    }
                }
            });
        }

        private static string ResolveInfoHashFromPayload(RuTrackerTaskPayload payload)
        {
            if (!string.IsNullOrWhiteSpace(payload.Topic?.InfoHash) && payload.Topic.InfoHash.Trim().Length == 40)
                return payload.Topic.InfoHash.Trim().ToUpperInvariant();
            if (payload.MagnetLink != null)
                return payload.MagnetLink.InfoHashes.V1OrV2.ToHex().ToUpperInvariant();
            if (payload.TorrentBytes != null && payload.TorrentBytes.Length > 64 &&
                MonoTorrent.Torrent.TryLoad(payload.TorrentBytes, out var parsed) && parsed != null)
                return parsed.InfoHashes.V1OrV2.ToHex().ToUpperInvariant();
            return string.Empty;
        }

        private static void CacheTorrentBytesQuiet(string infoHash, byte[]? torrentBytes)
        {
            if (torrentBytes == null || torrentBytes.Length <= 64 || string.IsNullOrWhiteSpace(infoHash))
                return;
            string cleanHash = infoHash.Trim().ToUpperInvariant();
            if (cleanHash.Length != 40) return;
            _ = Task.Run(() =>
            {
                try { RuTrackerModService.TrySaveCachedTorrentBytes(cleanHash, torrentBytes); } catch { }
            });
        }

        private static byte[]? LoadTorrentBytesFromCacheOrLegacy(string? infoHash, string? torrentCachePath, string? legacyBase64)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(torrentCachePath) && File.Exists(torrentCachePath) && new FileInfo(torrentCachePath).Length > 64)
                {
                    byte[] b = File.ReadAllBytes(torrentCachePath);
                    if (b.Length > 64 && b[0] == (byte)'d') return b;
                }
            }
            catch { }

            if (!string.IsNullOrWhiteSpace(infoHash) && infoHash.Trim().Length == 40)
            {
                try
                {
                    string cp = RuTrackerModService.GetCachedTorrentFilePath(infoHash.Trim().ToUpperInvariant());
                    if (File.Exists(cp) && new FileInfo(cp).Length > 64)
                    {
                        byte[] b = File.ReadAllBytes(cp);
                        if (b.Length > 64 && b[0] == (byte)'d') return b;
                    }
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(legacyBase64))
            {
                try
                {
                    byte[] b = Convert.FromBase64String(legacyBase64);
                    if (b.Length > 64)
                    {
                        if (!string.IsNullOrWhiteSpace(infoHash))
                            CacheTorrentBytesQuiet(infoHash, b);
                        return b;
                    }
                }
                catch { }
            }

            return null;
        }

        private void SyncBottomDockOrderOnUi()
        {
            RunOnUiSyncOrAsync(() =>
            {
                var desired = Items.Where(x => !x.IsCompleted && x.EffectiveStage != "ГОТОВО").ToList();
                for (int idx = 0; idx < desired.Count; idx++)
                {
                    var target = desired[idx];
                    int curIdx = BottomDockItems.IndexOf(target);
                    if (curIdx < 0)
                        BottomDockItems.Insert(idx, target);
                    else if (curIdx != idx)
                        BottomDockItems.Move(curIdx, idx);
                }
                for (int i = BottomDockItems.Count - 1; i >= 0; i--)
                {
                    if (!desired.Contains(BottomDockItems[i]))
                        BottomDockItems.RemoveAt(i);
                }
            });
        }

        private static string NormalizeIdentityTitleKey(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            string s = raw.Trim();
            if (string.Equals(s, "Игра 1", StringComparison.OrdinalIgnoreCase)) return string.Empty;
            return s.ToLowerInvariant();
        }

        private void RecordCompletedIdentity(string? infoHash, string? topicId, string? titleId, string? title, bool allowTopicHashMatch = true)
        {
            lock (_syncLock)
            {
                if (!string.IsNullOrWhiteSpace(titleId))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(titleId, @"(0[14]00[0-9A-Fa-f]{12})");
                    if (m.Success) _completedIdentityKeys.Add("tid:" + m.Groups[1].Value.ToUpperInvariant());
                }
                if (!string.IsNullOrWhiteSpace(title))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(title, @"(0[14]00[0-9A-Fa-f]{12})");
                    if (m.Success) _completedIdentityKeys.Add("tid:" + m.Groups[1].Value.ToUpperInvariant());
                    string nt = NormalizeIdentityTitleKey(title);
                    if (!string.IsNullOrEmpty(nt)) _completedIdentityKeys.Add("title:" + nt);
                }
                if (allowTopicHashMatch)
                {
                    string h = (infoHash ?? string.Empty).Trim().ToUpperInvariant();
                    if (h.Length == 40) _completedIdentityKeys.Add("hash:" + h);
                    if (!string.IsNullOrWhiteSpace(topicId)) _completedIdentityKeys.Add("topic:" + topicId.Trim());
                }
            }
        }

        private void RecordCompletedBatchIdentities(RuTrackerActiveDownloadItem item)
        {
            if (item == null) return;
            RecordCompletedIdentity(
                item.CurrentTopic?.InfoHash,
                item.CurrentTopic?.TopicId,
                null,
                item.DisplayTitle,
                allowTopicHashMatch: true);
            if (item.CreatedTasks != null)
            {
                foreach (var (t, payload, _) in item.CreatedTasks)
                {
                    RecordCompletedIdentity(
                        ResolveInfoHashFromPayload(payload),
                        payload.Topic?.TopicId ?? item.CurrentTopic?.TopicId,
                        t.TitleId,
                        t.GameName,
                        allowTopicHashMatch: true);
                    if (!string.IsNullOrWhiteSpace(payload.SlotGameTitle))
                        RecordCompletedIdentity(null, null, null, payload.SlotGameTitle, allowTopicHashMatch: false);
                }
            }
        }

        public static bool HasBuiltOutputOnDisk(string? outputFormat, string? outputFolder, string? titleId, string? topicId, string? gameTitle)
        {
            try
            {
                string outDir = RuTrackerModDownloaderDialog.ResolveRuTrackerFinalOutputFolder(outputFormat ?? "NSZ", titleId ?? string.Empty, outputFolder ?? string.Empty);
                if (string.IsNullOrWhiteSpace(outDir) || !Directory.Exists(outDir))
                    return false;

                string cleanName = (gameTitle ?? string.Empty).Trim();
                string sanName = ProcessingTask.SanitizeFileName(cleanName);
                string tid = (titleId ?? string.Empty).Trim();
                if (string.IsNullOrEmpty(tid) && !string.IsNullOrEmpty(cleanName))
                {
                    var m = System.Text.RegularExpressions.Regex.Match(cleanName, @"\b(0[14]00[0-9A-Fa-f]{12})\b");
                    if (m.Success) tid = m.Groups[1].Value;
                }

                var files = Directory.EnumerateFiles(outDir, "*.*").Where(f =>
                    (f.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) ||
                     f.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) ||
                     f.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) ||
                     f.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase)) &&
                    new FileInfo(f).Length > 0);

                return files.Any(f =>
                {
                    string fn = Path.GetFileNameWithoutExtension(f);
                    return (!string.IsNullOrWhiteSpace(cleanName) && fn.Contains(cleanName, StringComparison.OrdinalIgnoreCase)) ||
                           (!string.IsNullOrWhiteSpace(sanName) && fn.Contains(sanName, StringComparison.OrdinalIgnoreCase)) ||
                           (!string.IsNullOrWhiteSpace(tid) && f.Contains(tid, StringComparison.OrdinalIgnoreCase)) ||
                           (!string.IsNullOrWhiteSpace(topicId) && f.Contains(topicId, StringComparison.OrdinalIgnoreCase));
                });
            }
            catch
            {
                return false;
            }
        }

        public bool IsAlreadyCompletedOrSeeding(string? infoHash, string? topicId, string? titleId, string? title, string? outputFormat = null, string? outputFolder = null)
        {
            string h = (infoHash ?? string.Empty).Trim().ToUpperInvariant();
            string t = (topicId ?? string.Empty).Trim();
            string nt = NormalizeIdentityTitleKey(title);
            string tid = string.Empty;
            if (!string.IsNullOrWhiteSpace(titleId))
            {
                var m = System.Text.RegularExpressions.Regex.Match(titleId, @"(0[14]00[0-9A-Fa-f]{12})");
                if (m.Success) tid = m.Groups[1].Value.ToUpperInvariant();
            }
            if (string.IsNullOrEmpty(tid) && !string.IsNullOrWhiteSpace(title))
            {
                var m = System.Text.RegularExpressions.Regex.Match(title, @"(0[14]00[0-9A-Fa-f]{12})");
                if (m.Success) tid = m.Groups[1].Value.ToUpperInvariant();
            }

            lock (_syncLock)
            {
                if ((!string.IsNullOrEmpty(tid) && _completedIdentityKeys.Contains("tid:" + tid)) ||
                    (!string.IsNullOrEmpty(nt) && _completedIdentityKeys.Contains("title:" + nt)) ||
                    (h.Length == 40 && _completedIdentityKeys.Contains("hash:" + h)) ||
                    (!string.IsNullOrEmpty(t) && _completedIdentityKeys.Contains("topic:" + t)))
                {
                    return true;
                }

                if (_backingItems.Any(b =>
                    b.IsCompleted &&
                    ((!string.IsNullOrEmpty(nt) && string.Equals(NormalizeIdentityTitleKey(b.DisplayTitle), nt, StringComparison.Ordinal)) ||
                     (!string.IsNullOrEmpty(t) && string.Equals(b.CurrentTopic?.TopicId?.Trim(), t, StringComparison.OrdinalIgnoreCase)) ||
                     b.CreatedTasks.Any(ct =>
                         (!string.IsNullOrEmpty(tid) && string.Equals(ct.Task.TitleId?.Trim(), tid, StringComparison.OrdinalIgnoreCase)) ||
                         (!string.IsNullOrEmpty(nt) && string.Equals(NormalizeIdentityTitleKey(ct.Task.GameName), nt, StringComparison.Ordinal))))))
                {
                    return true;
                }
            }

            if (RuTrackerSeedingService.Instance.HasSeedingEntryMatching(h, t, title))
                return true;

            if (HasBuiltOutputOnDisk(outputFormat, outputFolder, tid, t, title))
                return true;

            return false;
        }

        public void BindRestoredTasksFromViewModel(
            ViewModels.TasksViewModel? vm,
            List<PersistedTaskSnapshot>? orphanedRtSnapshots = null)
        {
            if (vm == null) return;
            bool shouldAutoStart = false;

            lock (_syncLock)
            {
                // 0. Очищаем любые устаревшие дубликаты в TasksViewModel, которые уже ГОТОВО или на сидинге
                var staleCompletedInVm = vm.Tasks.Where(vmTask =>
                    vmTask.IsRuTrackerTask &&
                    vmTask.Status != "Успешно" && vmTask.Status != "Готово" &&
                    IsAlreadyCompletedOrSeeding(
                        vmTask.RuTrackerPayload?.Topic?.InfoHash,
                        vmTask.RuTrackerPayload?.Topic?.TopicId,
                        vmTask.TitleId,
                        vmTask.GameName,
                        vmTask.TargetFormat,
                        vmTask.OutputFolder)).ToList();
                foreach (var stale in staleCompletedInVm)
                {
                    RunOnUiSyncOrAsync(() => vm.Tasks.Remove(stale));
                }

                // 1. Синхронизируем незавершённые свежие задачи из рутрекер-очереди в TasksViewModel без дубликатов
                foreach (var item in _backingItems)
                {
                    item.ViewModel = vm;
                    if (item.CreatedTasks == null) continue;

                    for (int i = 0; i < item.CreatedTasks.Count; i++)
                    {
                        var (itemTask, payload, slot) = item.CreatedTasks[i];
                        var dupes = vm.Tasks.Where(vmTask =>
                            !ReferenceEquals(vmTask, itemTask) &&
                            ((!string.IsNullOrEmpty(itemTask.Id) && vmTask.Id == itemTask.Id) ||
                             (vmTask.IsRuTrackerTask && !string.IsNullOrEmpty(itemTask.GameName) && vmTask.GameName == itemTask.GameName &&
                              vmTask.OutputFolder == itemTask.OutputFolder))).ToList();
                        foreach (var dup in dupes)
                            RunOnUiSyncOrAsync(() => vm.Tasks.Remove(dup));

                        if (!item.IsCompleted && !vm.Tasks.Contains(itemTask))
                        {
                            RunOnUiSyncOrAsync(() =>
                            {
                                if (!vm.Tasks.Contains(itemTask))
                                    vm.Tasks.Add(itemTask);
                                if (itemTask.GameIcon == null)
                                    HistoryService.TryLoadIconForTask(itemTask);
                            });
                            shouldAutoStart = true;
                        }
                        else if (!item.IsCompleted)
                        {
                            shouldAutoStart = true;
                        }
                    }
                }

                // 2. Восстанавливаем свежие задачи для снимков из persisted_tasks.json, которых ещё нет в _backingItems и которые НЕ завершены
                if (orphanedRtSnapshots != null && orphanedRtSnapshots.Count > 0)
                {
                    foreach (var snap in orphanedRtSnapshots)
                    {
                        var rtSnap = snap.RuTrackerSnapshot;
                        if (rtSnap == null) continue;

                        if (snap.Status == "Успешно" || snap.Status == "Готово" || snap.Status == "Отменено" || snap.Status == "Отменен")
                            continue;

                        if (IsAlreadyCompletedOrSeeding(rtSnap.InfoHash, rtSnap.TopicId, snap.TitleId, snap.GameName, snap.TargetFormat, snap.OutputFolder))
                            continue;

                        bool alreadyTracked = _backingItems.Any(b =>
                            b.CreatedTasks.Any(ct =>
                                (!string.IsNullOrEmpty(snap.Id) && ct.Task.Id == snap.Id) ||
                                (!string.IsNullOrEmpty(rtSnap.TopicId) && ct.Payload.Topic?.TopicId == rtSnap.TopicId &&
                                 ct.Payload.SlotNumber == rtSnap.SlotNumber && ct.Payload.SlotGameTitle == rtSnap.SlotGameTitle) ||
                                (!string.IsNullOrEmpty(snap.GameName) && string.Equals(ct.Task.GameName, snap.GameName, StringComparison.OrdinalIgnoreCase))));
                        if (alreadyTracked) continue;

                        byte[]? tBytes = rtSnap.TorrentBytes ?? LoadTorrentBytesFromCacheOrLegacy(rtSnap.InfoHash, rtSnap.TorrentCachePath, null);
                        MonoTorrent.MagnetLink? mag = null;
                        if (!string.IsNullOrEmpty(rtSnap.MagnetUri))
                        {
                            try { MonoTorrent.MagnetLink.TryParse(rtSnap.MagnetUri, out mag); } catch { }
                        }

                        var cleanMeta = CloneMetadataWithoutBinaryBlobs(snap.CustomMetadata);
                        var payload = new RuTrackerTaskPayload
                        {
                            Topic = new RuTrackerTopicItem
                            {
                                TopicId = rtSnap.TopicId,
                                Title = rtSnap.TopicTitle,
                                Category = rtSnap.Category,
                                Seeds = rtSnap.Seeds,
                                MagnetUri = rtSnap.MagnetUri,
                                InfoHash = rtSnap.InfoHash,
                                DirectTorrentUrl = rtSnap.DirectTorrentUrl
                            },
                            TorrentBytes = tBytes,
                            MagnetLink = mag,
                            TorrentFiles = (rtSnap.TorrentFiles ?? new List<PersistedTorrentFileItem>()).Select(tf => new TorrentFileSelectionItem
                            {
                                RelativePath = tf.RelativePath,
                                Length = tf.Length,
                                IsSelected = tf.IsSelected,
                                CategoryBadge = tf.CategoryBadge,
                                IsRealTorrentFile = tf.IsRealTorrentFile
                            }).ToList(),
                            CloudLinks = new List<string>(rtSnap.CloudLinks ?? new List<string>()),
                            TargetReleaseDir = rtSnap.TargetReleaseDir,
                            CustomModTitle = rtSnap.CustomModTitle,
                            AutoBuild = rtSnap.AutoBuild,
                            HasTorrentSource = rtSnap.HasTorrentSource,
                            SharedReleaseDir = rtSnap.SharedReleaseDir,
                            AppendAutoTags = rtSnap.AppendAutoTags,
                            SlotNumber = rtSnap.SlotNumber,
                            TotalSlots = rtSnap.TotalSlots,
                            SlotGameTitle = rtSnap.SlotGameTitle,
                            PosterUrl = rtSnap.PosterUrl,
                            CustomMetadata = cleanMeta
                        };

                        var freshTask = new ProcessingTask
                        {
                            Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                            GameName = snap.GameName,
                            Operation = string.IsNullOrWhiteSpace(snap.Operation) ? "Multi" : snap.Operation,
                            SourceFormat = string.IsNullOrWhiteSpace(snap.SourceFormat) ? "MULTI" : snap.SourceFormat,
                            TargetFormat = string.IsNullOrWhiteSpace(snap.TargetFormat) ? "NSZ" : snap.TargetFormat,
                            Status = "В очереди",
                            Progress = 0,
                            SourceSize = snap.SourceSize,
                            SourceSizeBytes = snap.SourceSizeBytes,
                            OutputFolder = RuTrackerModDownloaderDialog.ResolveRuTrackerFinalOutputFolder(snap.TargetFormat, snap.TitleId, snap.OutputFolder),
                            OutputFileName = snap.OutputFileName,
                            LogDetails = "[Автоперезапуск] Незавершённая задача очищена и добавлена заново после запуска программы.",
                            GroupId = snap.GroupId,
                            TitleId = snap.TitleId,
                            Is3dsTask = snap.Is3dsTask,
                            IsSwitch2Task = snap.IsSwitch2Task,
                            BuildMode = snap.BuildMode,
                            IsRuTrackerTask = true,
                            KeepExactOutputFileName = snap.KeepExactOutputFileName,
                            DeleteSourceAfterVerify = snap.DeleteSourceAfterVerify,
                            IsRunning = false,
                            IsPaused = false,
                            HasActiveWorker = false,
                            CompressionLevel = snap.CompressionLevel,
                            FilesCount = snap.FilesCount,
                            InputFolders = snap.InputFolders,
                            CustomMetadata = cleanMeta,
                            RuTrackerPayload = payload
                        };

                        var freshItem = new RuTrackerActiveDownloadItem
                        {
                            BatchId = Guid.NewGuid().ToString("N").Substring(0, 6),
                            DisplayTitle = snap.GameName,
                            StageBadge = "В ОЧЕРЕДИ",
                            ProgressPercent = 0,
                            ProgressDetails = "Автоперезапуск...",
                            SpeedAndPeersText = "В очереди",
                            IsPausedByUser = false,
                            IsActive = true,
                            SharedReleaseDir = rtSnap.SharedReleaseDir ?? rtSnap.TargetReleaseDir ?? string.Empty,
                            HasTorrentSource = rtSnap.HasTorrentSource,
                            CurrentTopic = payload.Topic,
                            Service = App.RuTrackerMod.CreateIsolatedDownloadSession(),
                            ViewModel = vm,
                            DeleteSourceAfterVerify = snap.DeleteSourceAfterVerify,
                            Cts = new CancellationTokenSource()
                        };
                        freshItem.RawStageBadge = "В ОЧЕРЕДИ";
                        freshItem.RawStageColor = Windows.UI.Color.FromArgb(255, 100, 116, 139);
                        freshItem.RawDetails = "Автоперезапуск...";
                        freshItem.RawSpeedText = "В очереди";
                        freshItem.PauseEvent.Set();

                        freshTask.OnPauseRequested = () => TogglePauseItem(freshItem);
                        freshTask.OnResumeRequested = () => TogglePauseItem(freshItem);
                        freshTask.OnCancelRequested = () => CancelOrRemoveItem(freshItem);
                        freshItem.CreatedTasks.Add((freshTask, payload, new RuTrackerGameSlotItem { SlotNumber = rtSnap.SlotNumber, GameTitle = rtSnap.SlotGameTitle }));

                        _backingItems.Add(freshItem);
                        RecordCompletedIdentity(null, null, null, null, allowTopicHashMatch: false);
                        RunOnUiSyncOrAsync(() =>
                        {
                            if (!Items.Contains(freshItem))
                                Items.Add(freshItem);
                            if (!BottomDockItems.Contains(freshItem))
                                BottomDockItems.Add(freshItem);
                            if (!vm.Tasks.Contains(freshTask))
                                vm.Tasks.Add(freshTask);
                            HistoryService.TryLoadIconForTask(freshTask);
                            freshItem.CommitUiUpdates();
                        });
                        shouldAutoStart = true;
                    }
                }
            }

            SyncBottomDockOrderOnUi();

            if (shouldAutoStart)
            {
                SaveQueueState();
                _ = Task.Run(async () =>
                {
                    await Task.Delay(1000).ConfigureAwait(false);
                    try { EvaluateQueuePriorities(); } catch { }
                    SaveQueueState();
                });
            }
        }

        private static GameMetadataEditModel? CloneMetadataWithoutBinaryBlobs(GameMetadataEditModel? src)
        {
            if (src == null) return null;
            return new GameMetadataEditModel
            {
                TitleId = src.TitleId,
                Version = src.Version,
                TitleNameRussian = src.TitleNameRussian,
                TitleNameEnglish = src.TitleNameEnglish,
                Publisher = src.Publisher,
                SourceFilePath = src.SourceFilePath,
                CustomIconPath = src.CustomIconPath,
                OriginalIconBytes = null,
                CustomIconBytes = null,
                RawNacpBytes = null,
                TempExtractDir = src.TempExtractDir,
                ModNameRomFs = src.ModNameRomFs,
                ModNameExeFs = src.ModNameExeFs,
                HasRomFs = src.HasRomFs,
                HasExeFs = src.HasExeFs,
                Is3ds = src.Is3ds,
                BadgeRus = src.BadgeRus,
                Badge60Fps = src.Badge60Fps,
                BadgeMod = src.BadgeMod,
                BadgeAllDlc = src.BadgeAllDlc,
                CustomBadgeText = src.CustomBadgeText,
                BadgePosition = src.BadgePosition,
                CustomAudioWavPath = src.CustomAudioWavPath
            };
        }

        public bool TryResumeTaskFromViewModel(ProcessingTask task, ViewModels.TasksViewModel vm)
        {
            RuTrackerActiveDownloadItem? targetItem = null;
            lock (_syncLock)
            {
                targetItem = _backingItems.FirstOrDefault(item =>
                    item.CreatedTasks.Any(ct => ct.Task == task ||
                        (!string.IsNullOrEmpty(ct.Task.Id) && ct.Task.Id == task.Id) ||
                        (!string.IsNullOrEmpty(ct.Task.GameName) && ct.Task.GameName == task.GameName) ||
                        (!string.IsNullOrEmpty(ct.Task.OutputFileName) && ct.Task.OutputFileName == task.OutputFileName)));
            }

            if (targetItem == null)
                return false;

            targetItem.ViewModel = vm;
            lock (_syncLock)
            {
                for (int i = 0; i < targetItem.CreatedTasks.Count; i++)
                {
                    if (targetItem.CreatedTasks[i].Task != task &&
                        (targetItem.CreatedTasks[i].Task.Id == task.Id || targetItem.CreatedTasks[i].Task.GameName == task.GameName))
                    {
                        targetItem.CreatedTasks[i] = (task, targetItem.CreatedTasks[i].Payload, targetItem.CreatedTasks[i].Slot);
                    }
                }

                targetItem.IsPausedByUser = false;
                targetItem.IsPreemptedByQueue = false;
                targetItem.LastActivatedAtUtc = DateTime.UtcNow;
                targetItem.SmoothedSpeedBps = 0;
                if (targetItem.RawStageBadge == "ПАУЗА" || targetItem.StageBadge == "ПАУЗА")
                {
                    targetItem.RawStageBadge = "В ОЧЕРЕДИ";
                    targetItem.StageBadge = "В ОЧЕРЕДИ";
                }
                targetItem.PauseEvent.Set();

                if (targetItem.ActiveTorrentManager != null &&
                    (targetItem.ActiveTorrentManager.State == TorrentState.Paused || targetItem.ActiveTorrentManager.State == TorrentState.Stopped))
                {
                    try { targetItem.ActiveTorrentManager.StartAsync(); } catch { }
                }

                task.IsPaused = false;
                task.PauseEvent.Set();
                task.EnsureSourceMetricsPopulated();
                if (task.GameIcon == null)
                    HistoryService.TryLoadIconForTask(task);

                targetItem.CommitUiUpdates();
                EvaluateQueuePriorities();
                SaveQueueState();
            }

            return true;
        }

        private static bool GetInjectDlcUnlockerFlag(RuTrackerTaskPayload payload)
        {
            try
            {
                var prop = typeof(RuTrackerTaskPayload).GetProperty("InjectDlcUnlocker");
                if (prop != null && prop.PropertyType == typeof(bool) && prop.GetValue(payload) is bool val)
                    return val;
            }
            catch { }

            return payload.TorrentFiles != null && payload.TorrentFiles.Any(f =>
                f.IsSelected &&
                (f.CategoryBadge.Contains("UNLOCKER", StringComparison.OrdinalIgnoreCase) ||
                 f.RelativePath.Contains("unlocker", StringComparison.OrdinalIgnoreCase)));
        }

        private static void SetInjectDlcUnlockerFlag(RuTrackerTaskPayload payload, bool value)
        {
            try
            {
                var prop = typeof(RuTrackerTaskPayload).GetProperty("InjectDlcUnlocker");
                if (prop != null && prop.CanWrite && prop.PropertyType == typeof(bool))
                    prop.SetValue(payload, value);
            }
            catch { }
        }

        private List<RuTrackerPersistedQueueRecord> BuildQueueRecordsSnapshot()
        {
            lock (_syncLock)
            {
                return _backingItems.Select(i =>
                {
                    var rec = new RuTrackerPersistedQueueRecord
                    {
                        BatchId = i.BatchId,
                        DisplayTitle = i.DisplayTitle,
                        StageBadge = i.EffectiveStage,
                        ProgressPercent = i.RawPercent > 0 || i.EffectiveStage == "ГОТОВО" ? i.RawPercent : i.ProgressPercent,
                        ProgressDetails = !string.IsNullOrEmpty(i.RawDetails) ? i.RawDetails : i.ProgressDetails,
                        IsPausedByUser = i.IsPausedByUser,
                        SavedAt = DateTime.Now,
                        SharedReleaseDir = i.SharedReleaseDir,
                        HasTorrentSource = i.HasTorrentSource
                    };

                    if (i.CreatedTasks != null)
                    {
                        foreach (var (t, payload, _) in i.CreatedTasks)
                        {
                            string infoHash = ResolveInfoHashFromPayload(payload);
                            string torCachePath = infoHash.Length == 40 ? RuTrackerModService.GetCachedTorrentFilePath(infoHash) : string.Empty;
                            if (payload.TorrentBytes != null && payload.TorrentBytes.Length > 64 && infoHash.Length == 40)
                                CacheTorrentBytesQuiet(infoHash, payload.TorrentBytes);

                            rec.Tasks.Add(new RuTrackerPersistedTaskPayload
                            {
                                TaskId = t.Id,
                                PosterUrl = payload.PosterUrl ?? payload.Topic?.PosterUrl ?? string.Empty,
                                SlotNumber = payload.SlotNumber,
                                SlotGameTitle = payload.SlotGameTitle,
                                CustomModTitle = payload.CustomModTitle,
                                TargetReleaseDir = payload.TargetReleaseDir,
                                OutputFolder = t.OutputFolder,
                                OutputFormat = t.TargetFormat,
                                AutoBuild = payload.AutoBuild,
                                IncludeTechTags = payload.AppendAutoTags,
                                InjectDlcUnlocker = GetInjectDlcUnlockerFlag(payload),
                                DeleteSourceAfterVerify = t.DeleteSourceAfterVerify,
                                TopicId = payload.Topic?.TopicId ?? string.Empty,
                                TopicTitle = payload.Topic?.Title ?? string.Empty,
                                Seeds = payload.Topic?.Seeds ?? 0,
                                InfoHash = infoHash,
                                TorrentCachePath = torCachePath,
                                MagnetLink = payload.MagnetLink?.ToV1String() ?? payload.MagnetLink?.ToString() ?? payload.Topic?.MagnetUri ?? string.Empty,
                                TorrentBytesBase64 = string.Empty,
                                TorrentFiles = (payload.TorrentFiles ?? new List<TorrentFileSelectionItem>()).Select(f => new PersistedTorrentFileItem
                                {
                                    RelativePath = f.RelativePath,
                                    Length = f.Length,
                                    IsSelected = f.IsSelected,
                                    CategoryBadge = f.CategoryBadge,
                                    IsRealTorrentFile = f.IsRealTorrentFile
                                }).ToList(),
                                CloudLinks = (payload.CloudLinks ?? new List<string>()).ToList(),
                                TaskTitle = t.GameName
                            });
                        }
                    }

                    return rec;
                }).ToList();
            }
        }

        private void SaveQueueState()
        {
            Interlocked.Increment(ref _queueSaveRequestVersion);
            if (Interlocked.CompareExchange(ref _queueSaveLoopActive, 1, 0) == 0)
            {
                _ = RunCoalescedQueueSaveLoopAsync();
            }
        }

        private async Task RunCoalescedQueueSaveLoopAsync()
        {
            try
            {
                while (true)
                {
                    await Task.Delay(250).ConfigureAwait(false);
                    int targetVer = Volatile.Read(ref _queueSaveRequestVersion);
                    var records = BuildQueueRecordsSnapshot();
                    string json = JsonSerializer.Serialize(records, _queueJsonOptions);
                    await _queueSaveSemaphore.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        await SafeFileOperations.WriteAllTextAtomicAsync(_queueStatePath, json, flushToDisk: false).ConfigureAwait(false);
                    }
                    finally
                    {
                        _queueSaveSemaphore.Release();
                    }

                    if (Volatile.Read(ref _queueSaveRequestVersion) == targetVer)
                        break;
                }
            }
            catch { }
            finally
            {
                int lastSeen = Volatile.Read(ref _queueSaveRequestVersion);
                Volatile.Write(ref _queueSaveLoopActive, 0);
                if (Volatile.Read(ref _queueSaveRequestVersion) != lastSeen &&
                    Interlocked.CompareExchange(ref _queueSaveLoopActive, 1, 0) == 0)
                {
                    _ = RunCoalescedQueueSaveLoopAsync();
                }
            }
        }

        public void FlushQueueStateSync()
        {
            try
            {
                Interlocked.Increment(ref _queueSaveRequestVersion);
                var records = BuildQueueRecordsSnapshot();
                string json = JsonSerializer.Serialize(records, _queueJsonOptions);
                bool acquired = _queueSaveSemaphore.Wait(2000);
                try
                {
                    SafeFileOperations.WriteAllTextAtomic(_queueStatePath, json, flushToDisk: false);
                }
                finally
                {
                    if (acquired) _queueSaveSemaphore.Release();
                }
            }
            catch { }
        }

        private void LoadPersistedState()
        {
            bool rewrittenQueue = false;
            try
            {
                if (!File.Exists(_queueStatePath)) return;
                string json = File.ReadAllText(_queueStatePath);
                var records = JsonSerializer.Deserialize<List<RuTrackerPersistedQueueRecord>>(json);
                if (records == null || records.Count == 0) return;

                static bool CheckRecordBuiltOnDisk(RuTrackerPersistedQueueRecord r)
                {
                    var tidMatch = System.Text.RegularExpressions.Regex.Match(r.DisplayTitle ?? string.Empty, @"\b(0[14]00[0-9A-Fa-f]{12})\b");
                    string displayTid = tidMatch.Success ? tidMatch.Groups[1].Value : string.Empty;
                    if (r.Tasks != null && r.Tasks.Count > 0)
                    {
                        foreach (var tp in r.Tasks)
                        {
                            string gameName = !string.IsNullOrWhiteSpace(tp.TaskTitle)
                                ? tp.TaskTitle
                                : (!string.IsNullOrWhiteSpace(tp.SlotGameTitle) ? tp.SlotGameTitle : (r.DisplayTitle ?? string.Empty));
                            if (HasBuiltOutputOnDisk(tp.OutputFormat, tp.OutputFolder, displayTid, tp.TopicId, gameName))
                                return true;
                        }
                    }
                    else
                    {
                        if (HasBuiltOutputOnDisk("NSZ", string.Empty, displayTid, string.Empty, r.DisplayTitle))
                            return true;
                    }
                    return false;
                }

                var pendingRecords = new List<RuTrackerPersistedQueueRecord>();
                var seenUnfinishedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Pass A: Handle completed records first so unfinished duplicates get recorded & filtered cleanly
                foreach (var r in records)
                {
                    if (r.StageBadge == "ОТМЕНЕНО")
                    {
                        rewrittenQueue = true;
                        continue;
                    }

                    bool isCompleted = r.StageBadge == "ГОТОВО" || CheckRecordBuiltOnDisk(r);
                    if (!isCompleted && r.Tasks != null)
                    {
                        isCompleted = r.Tasks.Any(tp =>
                            IsAlreadyCompletedOrSeeding(
                                tp.InfoHash,
                                tp.TopicId,
                                null,
                                !string.IsNullOrWhiteSpace(tp.TaskTitle) ? tp.TaskTitle : r.DisplayTitle,
                                tp.OutputFormat,
                                tp.OutputFolder));
                    }

                    if (!isCompleted)
                    {
                        pendingRecords.Add(r);
                        continue;
                    }

                    r.StageBadge = "ГОТОВО";
                    r.ProgressPercent = 100.0;
                    r.ProgressDetails = "Сборка завершена";

                    RecordCompletedIdentity(null, null, null, r.DisplayTitle, allowTopicHashMatch: true);
                    if (r.Tasks != null)
                    {
                        foreach (var tp in r.Tasks)
                        {
                            string tTitle = !string.IsNullOrWhiteSpace(tp.TaskTitle)
                                ? tp.TaskTitle
                                : (tp.SlotGameTitle ?? r.DisplayTitle ?? string.Empty);
                            RecordCompletedIdentity(tp.InfoHash, tp.TopicId, null, tTitle, allowTopicHashMatch: true);
                        }
                    }

                    bool deleteSource = r.Tasks == null || r.Tasks.Count == 0 || r.Tasks.All(tp => tp.DeleteSourceAfterVerify);
                    if (deleteSource)
                    {
                        rewrittenQueue = true;
                        continue;
                    }

                    if (r.HasTorrentSource)
                    {
                        // Keep completed seeding batches strictly in "Раздачи" ("Закачки" tab), never in queue or bottom dock
                        rewrittenQueue = true;
                        string firstTopicId = r.Tasks?.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.TopicId))?.TopicId ?? string.Empty;
                        string firstHash = r.Tasks?.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t.InfoHash))?.InfoHash ?? string.Empty;
                        if (!RuTrackerSeedingService.Instance.HasSeedingEntryMatching(firstHash, firstTopicId, r.DisplayTitle))
                        {
                            string contentDir = r.Tasks != null && r.Tasks.Count == 1 && !string.IsNullOrWhiteSpace(r.Tasks[0].TargetReleaseDir)
                                ? r.Tasks[0].TargetReleaseDir
                                : (r.SharedReleaseDir ?? r.Tasks?.FirstOrDefault()?.TargetReleaseDir ?? string.Empty);

                            if (!string.IsNullOrWhiteSpace(contentDir) && Directory.Exists(contentDir))
                            {
                                var tp0 = r.Tasks?.FirstOrDefault();
                                byte[]? tBytes = tp0 != null ? LoadTorrentBytesFromCacheOrLegacy(tp0.InfoHash, tp0.TorrentCachePath, tp0.TorrentBytesBase64) : null;
                                var selRels = (r.Tasks ?? new List<RuTrackerPersistedTaskPayload>())
                                    .SelectMany(tp => tp.TorrentFiles ?? new List<PersistedTorrentFileItem>())
                                    .Where(tf => tf.IsSelected && !string.IsNullOrWhiteSpace(tf.RelativePath))
                                    .Select(tf => tf.RelativePath)
                                    .Distinct(StringComparer.OrdinalIgnoreCase)
                                    .ToList();
                                long selSize = (r.Tasks ?? new List<RuTrackerPersistedTaskPayload>())
                                    .SelectMany(tp => tp.TorrentFiles ?? new List<PersistedTorrentFileItem>())
                                    .Where(tf => tf.IsSelected)
                                    .GroupBy(tf => tf.RelativePath, StringComparer.OrdinalIgnoreCase)
                                    .Sum(g => Math.Max(0L, g.First().Length));

                                var req = new RuTrackerSeedingAddRequest
                                {
                                    InfoHash = firstHash,
                                    DisplayTitle = r.DisplayTitle ?? string.Empty,
                                    TopicId = firstTopicId,
                                    TopicTitle = tp0?.TopicTitle ?? (r.DisplayTitle ?? string.Empty),
                                    ContentDir = contentDir,
                                    TorrentBytes = tBytes,
                                    MagnetLink = tp0?.MagnetLink ?? string.Empty,
                                    SelectedRelativePaths = selRels,
                                    TotalSelectedSizeBytes = selSize
                                };

                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        await RuTrackerSeedingService.Instance.LoadAndResumePersistedAsync().ConfigureAwait(false);
                                        await RuTrackerSeedingService.Instance.AddOrUpdateSeedingAsync(req).ConfigureAwait(false);
                                    }
                                    catch { }
                                });
                            }
                        }
                        continue;
                    }

                    // Completed mod-only (!HasTorrentSource) item: keep in Items ("Закачки" tab) ONLY, never in BottomDockItems
                    var doneItem = BuildRestoredQueueItemFromRecord(r, isCompleted: true);
                    lock (_syncLock) { _backingItems.Add(doneItem); }
                    RunOnUiSyncOrAsync(() =>
                    {
                        if (!Items.Contains(doneItem))
                            Items.Add(doneItem);
                        doneItem.CommitUiUpdates();
                    });
                }

                // Pass B: Deduplicate & recreate unfinished batches at 0% for clean auto-restart
                foreach (var r in pendingRecords)
                {
                    if (r.Tasks == null || r.Tasks.Count == 0)
                    {
                        rewrittenQueue = true;
                        continue;
                    }

                    var tp0 = r.Tasks[0];
                    string checkTitle = !string.IsNullOrWhiteSpace(tp0.TaskTitle) ? tp0.TaskTitle : (r.DisplayTitle ?? string.Empty);
                    if (IsAlreadyCompletedOrSeeding(tp0.InfoHash, tp0.TopicId, null, checkTitle, tp0.OutputFormat, tp0.OutputFolder))
                    {
                        rewrittenQueue = true;
                        continue;
                    }

                    string dedupKey = !string.IsNullOrWhiteSpace(tp0.TopicId)
                        ? ("topic:" + tp0.TopicId + "|" + NormalizeIdentityTitleKey(r.DisplayTitle))
                        : ("title:" + NormalizeIdentityTitleKey(r.DisplayTitle));
                    if (!seenUnfinishedKeys.Add(dedupKey))
                    {
                        rewrittenQueue = true;
                        continue;
                    }

                    rewrittenQueue = true;
                    var freshItem = BuildRestoredQueueItemFromRecord(r, isCompleted: false);
                    lock (_syncLock) { _backingItems.Add(freshItem); }
                    RunOnUiSyncOrAsync(() =>
                    {
                        if (!Items.Contains(freshItem))
                            Items.Add(freshItem);
                        if (!BottomDockItems.Contains(freshItem))
                            BottomDockItems.Add(freshItem);
                        freshItem.CommitUiUpdates();
                    });
                }

                SyncBottomDockOrderOnUi();

                if (rewrittenQueue)
                    SaveQueueState();
            }
            catch { }
        }

        private RuTrackerActiveDownloadItem BuildRestoredQueueItemFromRecord(RuTrackerPersistedQueueRecord r, bool isCompleted)
        {
            var item = new RuTrackerActiveDownloadItem
            {
                BatchId = isCompleted ? (r.BatchId ?? string.Empty) : Guid.NewGuid().ToString("N").Substring(0, 6),
                DisplayTitle = r.DisplayTitle ?? string.Empty,
                StageBadge = isCompleted ? "ГОТОВО" : "В ОЧЕРЕДИ",
                ProgressPercent = isCompleted ? 100.0 : 0.0,
                ProgressDetails = isCompleted ? (r.ProgressDetails ?? "Сборка завершена") : "Автоперезапуск...",
                SpeedAndPeersText = isCompleted ? "100% Завершено" : "В очереди",
                IsPausedByUser = false,
                IsActive = !isCompleted,
                IsPipelineStarted = false,
                IsBuildCompletedOnDisk = isCompleted,
                SharedReleaseDir = r.SharedReleaseDir ?? string.Empty,
                HasTorrentSource = r.HasTorrentSource,
                DeleteSourceAfterVerify = r.Tasks == null || r.Tasks.Count == 0 || r.Tasks.All(tp => tp.DeleteSourceAfterVerify),
                Service = !isCompleted ? App.RuTrackerMod.CreateIsolatedDownloadSession() : null,
                Cts = new CancellationTokenSource()
            };
            item.RawPercent = item.ProgressPercent;
            item.RawDetails = item.ProgressDetails;
            item.RawSpeedText = item.SpeedAndPeersText;
            item.RawStageBadge = item.StageBadge;
            item.RawStageColor = isCompleted
                ? Windows.UI.Color.FromArgb(255, 34, 197, 94)
                : Windows.UI.Color.FromArgb(255, 100, 116, 139);
            item.PauseEvent.Set();

            if (r.Tasks != null && r.Tasks.Count > 0)
            {
                foreach (var tp in r.Tasks)
                {
                    string gameTitle = !string.IsNullOrWhiteSpace(tp.TaskTitle) ? tp.TaskTitle : (tp.SlotGameTitle ?? string.Empty);
                    var task = new ProcessingTask
                    {
                        Id = isCompleted
                            ? (!string.IsNullOrEmpty(tp.TaskId) ? tp.TaskId : Guid.NewGuid().ToString("N").Substring(0, 8))
                            : Guid.NewGuid().ToString("N").Substring(0, 8),
                        GameName = gameTitle,
                        Operation = "Multi",
                        SourceFormat = "MULTI",
                        TargetFormat = string.IsNullOrWhiteSpace(tp.OutputFormat) ? "NSZ" : tp.OutputFormat,
                        OutputFolder = RuTrackerModDownloaderDialog.ResolveRuTrackerFinalOutputFolder(tp.OutputFormat, string.Empty, tp.OutputFolder),
                        OutputFileName = ProcessingTask.SanitizeFileName(gameTitle),
                        DeleteSourceAfterVerify = tp.DeleteSourceAfterVerify,
                        KeepExactOutputFileName = !tp.IncludeTechTags,
                        Progress = isCompleted ? 100.0 : 0.0,
                        Status = isCompleted ? "Успешно" : "В очереди",
                        LogDetails = isCompleted
                            ? string.Empty
                            : "[Автоперезапуск] Незавершённая задача очищена и добавлена заново после запуска программы.",
                        IsRuTrackerTask = true,
                        IsRunning = false,
                        IsPaused = false,
                        HasActiveWorker = false,
                        InputFolders = tp.TargetReleaseDir ?? string.Empty
                    };

                    MonoTorrent.MagnetLink? mag = null;
                    if (!string.IsNullOrEmpty(tp.MagnetLink))
                    {
                        try { MonoTorrent.MagnetLink.TryParse(tp.MagnetLink, out mag); } catch { }
                    }

                    byte[]? tBytes = LoadTorrentBytesFromCacheOrLegacy(tp.InfoHash, tp.TorrentCachePath, tp.TorrentBytesBase64);

                    var payload = new RuTrackerTaskPayload
                    {
                        SlotNumber = tp.SlotNumber,
                        PosterUrl = tp.PosterUrl ?? string.Empty,
                        SlotGameTitle = tp.SlotGameTitle ?? string.Empty,
                        CustomModTitle = tp.CustomModTitle ?? string.Empty,
                        TargetReleaseDir = tp.TargetReleaseDir ?? string.Empty,
                        AutoBuild = tp.AutoBuild,
                        AppendAutoTags = tp.IncludeTechTags,
                        SharedReleaseDir = r.SharedReleaseDir ?? string.Empty,
                        HasTorrentSource = r.HasTorrentSource,
                        CloudLinks = (tp.CloudLinks ?? new List<string>()).ToList(),
                        TorrentBytes = tBytes,
                        MagnetLink = mag,
                        Topic = new RuTrackerTopicItem
                        {
                            TopicId = tp.TopicId ?? string.Empty,
                            Title = tp.TopicTitle ?? string.Empty,
                            Seeds = tp.Seeds,
                            InfoHash = tp.InfoHash ?? string.Empty,
                            MagnetUri = tp.MagnetLink ?? string.Empty
                        },
                        TorrentFiles = (tp.TorrentFiles ?? new List<PersistedTorrentFileItem>()).Select(tf => new TorrentFileSelectionItem
                        {
                            RelativePath = tf.RelativePath ?? string.Empty,
                            Length = tf.Length,
                            IsSelected = tf.IsSelected,
                            CategoryBadge = tf.CategoryBadge ?? string.Empty,
                            IsRealTorrentFile = tf.IsRealTorrentFile
                        }).ToList()
                    };
                    SetInjectDlcUnlockerFlag(payload, tp.InjectDlcUnlocker);

                    task.RuTrackerPayload = payload;
                    task.OnPauseRequested = () => TogglePauseItem(item);
                    task.OnResumeRequested = () => TogglePauseItem(item);
                    task.OnCancelRequested = () => CancelOrRemoveItem(item);
                    item.CreatedTasks.Add((task, payload, new RuTrackerGameSlotItem { SlotNumber = tp.SlotNumber, GameTitle = tp.SlotGameTitle ?? gameTitle }));
                }
            }

            return item;
        }
    }
}
