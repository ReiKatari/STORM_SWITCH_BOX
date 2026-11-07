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
    public sealed class RuTrackerActiveDownloadItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
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

        /// <summary>
        /// Возвращает true только если закачка занимает один из 5 основных скоростных слотов.
        /// Активные закачки со скоростью ниже 200 КБ/с (после стартового прогрева 8 сек) продолжают качаться и НЕ учитываются в лимите 5!
        /// </summary>
        public bool CountsTowardConcurrencyLimit(DateTime nowUtc)
        {
            if (!IsActivelyDownloading)
                return false;

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

        public void NotifyLocalizationChanged()
        {
            OnPropertyChanged(nameof(StageBadgeDisplay));
            OnPropertyChanged(nameof(ProgressDetailsDisplay));
            OnPropertyChanged(nameof(SpeedTextDisplay));
            OnPropertyChanged(nameof(SpeedAndPeersTextDisplay));
            OnPropertyChanged(nameof(PauseResumeToolTip));
            OnPropertyChanged(nameof(PauseResumeTooltip));
        }

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
            if (StageBadge != RawStageBadge && !string.IsNullOrEmpty(RawStageBadge))
            {
                StageBadge = RawStageBadge;
                OnPropertyChanged(nameof(StageBadgeDisplay));
                OnPropertyChanged(nameof(PauseResumeGlyph));
                OnPropertyChanged(nameof(PauseResumeToolTip));
                OnPropertyChanged(nameof(PauseResumeTooltip));
            }
            if (_stageBrush == null || _stageBrush.Color != RawStageColor)
                StageBrush = new SolidColorBrush(RawStageColor);
            var targetSpeedColor = ResolveSpeedColor(EffectiveStage);
            if (_speedBrush == null || _speedBrush.Color != targetSpeedColor)
                SpeedBrush = new SolidColorBrush(targetSpeedColor);
            CurrentSpeedBps = RawSpeedBps;
            CurrentSeeds = RawSeeds;
            CurrentPeers = RawPeers;
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
        public string OutputFormat { get; set; } = "NSP";
        public bool AutoBuild { get; set; } = true;
        public bool IncludeTechTags { get; set; }
        public bool InjectDlcUnlocker { get; set; }
        public bool DeleteSourceAfterVerify { get; set; } = true;
        public string TopicId { get; set; } = string.Empty;
        public string TopicTitle { get; set; } = string.Empty;
        public int Seeds { get; set; }
        public string MagnetLink { get; set; } = string.Empty;
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

        public const int MaxConcurrentDownloads = 5;
        public const double LowSpeedExemptThresholdBps = 200.0 * 1024.0; // < 200 КБ/с не занимают слот в 5 одновременных закачках
        public const double StartupWarmupSeconds = 8.0;
        public const int MaxTotalConcurrentDownloads = 30;
        public ObservableCollection<RuTrackerActiveDownloadItem> Items { get; } = new();
        private readonly List<RuTrackerActiveDownloadItem> _backingItems = new();

        private readonly object _syncLock = new();
        private readonly System.Threading.Timer _uiTimer;
        private readonly string _queueStatePath;

        private RuTrackerDownloadQueueService()
        {
            string localAppDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StormSwitchBox");
            Directory.CreateDirectory(localAppDataDir);
            _queueStatePath = Path.Combine(localAppDataDir, "rutracker_download_queue.json");

            // UI-таймер 250 мс через System.Threading.Timer + App.RunOnUI
            _uiTimer = new System.Threading.Timer(OnTimerCallback, null, TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250));

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

        private void OnTimerCallback(object? state)
        {
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

            if (snapshot.Length == 0) return;

            double totalSpeedBps = 0;
            int maxSeeds = 0;
            int maxPeers = 0;
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
                    SaveQueueState();
                    EvaluateQueuePriorities();
                }

                item.UpdateSmoothedSpeed();
                item.CommitUiUpdates();

                if (item.IsActive && item.EffectiveStage is "СКАЧИВАНИЕ" or "МОДЫ" or "МЕТАДАННЫЕ")
                {
                    runningCount++;
                    totalSpeedBps += item.RawSpeedBps;
                    if (item.RawSeeds > maxSeeds) maxSeeds = item.RawSeeds;
                    if (item.RawPeers > maxPeers) maxPeers = item.RawPeers;
                }

                // Синхронизируем состояние задач в TasksViewModel.Tasks
                foreach (var (t, _, _) in item.CreatedTasks)
                {
                    if (item.StageBadge is "СКАЧИВАНИЕ" or "МОДЫ" or "МЕТАДАННЫЕ")
                    {
                        t.Progress = Math.Clamp(item.RawPercent, 0, 100);
                        if (!string.IsNullOrEmpty(item.RawSpeedText))
                            t.Speed = item.RawSpeedText;
                    }
                }
            }

            DateTime nowUtc = DateTime.UtcNow;
            int countedFastRunning = snapshot.Count(i => i.CountsTowardConcurrencyLimit(nowUtc));

            // Самовосстанавливающийся планировщик:
            // Закачки со скоростью < 200 КБ/с качаются параллельно и НЕ занимают слот в MaxConcurrentDownloads (5)!
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

            int inList = snapshot.Length;
            int downloading = runningCount;
            int inQueue = snapshot.Count(i => i.StageBadge is "В ОЧЕРЕДИ");
            int finished = snapshot.Count(i => i.StageBadge is "ГОТОВО");

            // Если открыто диалоговое окно RuTrackerModDownloaderDialog, обновляем его заголовочную панель
            var activeDlg = RuTrackerModDownloaderDialog.ActiveInstance;
            if (activeDlg != null)
            {
                activeDlg.UpdateHeaderSummary(inList, downloading, inQueue, finished, totalSpeedBps, maxSeeds, maxPeers);
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

                // Если пользователь вручную поднял ожидающую задачу стрелкой вверх в топ-5 при заполненных слотах —
                // освобождаем место за счёт самой нижней быстрой задачи (медленные < 200 КБ/с никогда не останавливаем).
                if (allowPreemptionForReorder)
                {
                    var topRequested = candidates.Take(MaxConcurrentDownloads)
                        .Where(i => !i.IsPipelineStarted || i.IsPreemptedByQueue)
                        .ToList();

                    int currentCounted = candidates.Count(i => i.CountsTowardConcurrencyLimit(nowUtc));
                    if (topRequested.Count > 0 && currentCounted >= MaxConcurrentDownloads)
                    {
                        int needToFree = Math.Min(topRequested.Count, currentCounted - MaxConcurrentDownloads + topRequested.Count);
                        var demotableFast = candidates
                            .Skip(MaxConcurrentDownloads)
                            .Where(i => i.CountsTowardConcurrencyLimit(nowUtc))
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
                        if (item.IsPreemptedByQueue)
                        {
                            item.IsPreemptedByQueue = false;
                            item.LastActivatedAtUtc = nowUtc;
                            item.SmoothedSpeedBps = 0;
                            item.PauseEvent.Set();
                            if (item.ActiveTorrentManager != null &&
                                (item.ActiveTorrentManager.State == TorrentState.Paused || item.ActiveTorrentManager.State == TorrentState.Stopped))
                            {
                                try { item.ActiveTorrentManager.StartAsync(); } catch { }
                            }
                            item.RawStageBadge = item.HasTorrentSource ? "СКАЧИВАНИЕ" : "МОДЫ";
                            item.RawStageColor = Windows.UI.Color.FromArgb(255, 2, 132, 199);
                        }
                        else if (!item.IsPipelineStarted)
                        {
                            item.LastActivatedAtUtc = nowUtc;
                            item.SmoothedSpeedBps = 0;
                            StartItemPipeline(item);
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
                    try
                    {
                        if (item.RawStageBadge != "ОТМЕНЕНО" && item.RawStageBadge != "ОШИБКА")
                        {
                            item.RawStageBadge = "ГОТОВО";
                            item.RawStageColor = Windows.UI.Color.FromArgb(255, 34, 197, 94);
                            item.RawPercent = 100.0;
                            item.RawSpeedText = "100% Завершено";
                            item.RawDetails = "Сборка завершена";
                        }
                        item.IsActive = false;
                        RunOnUiSyncOrAsync(() =>
                        {
                            item.CommitUiUpdates();
                            SaveQueueState();
                        });
                    }
                    catch { }
                    EvaluateQueuePriorities();
                }
            });
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
                    }).ConfigureAwait(false);
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
                ? payload.TorrentFiles.Where(f => f.IsSelected && f.CategoryBadge == "МОД / РУСИФИКАТОР").ToList()
                : new List<TorrentFileSelectionItem>();

            bool hasAllowedModsInRelease = selectedModEntries.Count > 0 || payload.CloudLinks.Count > 0;
            if (hasAllowedModsInRelease)
            {
                var allowedModFolderNames = selectedModEntries
                    .SelectMany(f => f.GetEffectiveRelativePaths().Concat(new[] { f.RelativePath }))
                    .Select(p => p.Replace('/', '\\').Trim('\\'))
                    .Select(p => p.Split('\\').FirstOrDefault(s => !string.IsNullOrEmpty(s)))
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var p in RuTrackerModService.DiscoverModPayloadsInDirectory(payload.TargetReleaseDir))
                {
                    bool matchesSelected = allowedModFolderNames.Count == 0 ||
                                           allowedModFolderNames.Any(m => p.Contains(m, StringComparison.OrdinalIgnoreCase)) ||
                                           p.Contains("Extracted_TorrentMod_", StringComparison.OrdinalIgnoreCase);
                    if (matchesSelected && !discoveredModPayloads.Contains(p, StringComparer.OrdinalIgnoreCase))
                        discoveredModPayloads.Add(p);
                }
            }

            // Выбираем игровые контейнеры
            var matchedContainers = RuTrackerModDownloaderDialog.FilterDownloadedContainersForSlot(
                Directory.Exists(payload.TargetReleaseDir)
                    ? Directory.EnumerateFiles(payload.TargetReleaseDir, "*.*", SearchOption.AllDirectories)
                        .Where(p => p.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) ||
                                    p.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) ||
                                    p.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) ||
                                    p.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase) ||
                                    p.EndsWith(".nro", StringComparison.OrdinalIgnoreCase))
                        .ToList()
                    : new List<string>(),
                payload.TorrentFiles,
                payload.SlotNumber,
                payload.SlotGameTitle,
                new List<string> { payload.SlotGameTitle });

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

            // 5. Автоматическая сборка мульти-контента
            if (payload.AutoBuild && task.InputFiles.Count > 0)
            {
                item.RawStageBadge = "СБОРКА";
                item.RawStageColor = Windows.UI.Color.FromArgb(255, 168, 85, 247); // #A855F7 Purple
                item.RawDetails = "Сборка мульти-контента...";
                item.RawSpeedText = "Сборка";
                RunOnUiSyncOrAsync(() => item.CommitUiUpdates());
                EvaluateQueuePriorities(); // Немедленно освобождаем сетевой слот для следующей закачки в очереди!

                App.RunOnUI(() =>
                {
                    task.Status = "Сборка мульти-контента...";
                    task.Speed = string.Empty;
                    task.Progress = 0;
                });

                string outExt = task.GetPrimaryExtension();
                string outDir = string.IsNullOrWhiteSpace(task.OutputFolder)
                    ? (string.IsNullOrWhiteSpace(App.Settings.Current.OutputFolder) ? payload.TargetReleaseDir : App.Settings.Current.OutputFolder)
                    : task.OutputFolder;
                Directory.CreateDirectory(outDir);
                string outPath = Path.Combine(outDir, task.OutputFileName + outExt);

                await App.MultiContent.BuildMultiContentAsync(
                    task,
                    task.InputFiles,
                    outPath,
                    patchFirmware: App.Settings.Current.ForceMultiRebuild,
                    ct).ConfigureAwait(false);

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
                        if (!string.IsNullOrWhiteSpace(payload.SharedReleaseDir) &&
                            !string.Equals(payload.SharedReleaseDir, payload.TargetReleaseDir, StringComparison.OrdinalIgnoreCase))
                        {
                            bool anySiblingStillPending = false;
                            RunOnUiSyncOrAsync(() =>
                            {
                                anySiblingStillPending = item.ViewModel != null && item.ViewModel.Tasks.Any(other =>
                                    other != task &&
                                    other.RuTrackerPayload != null &&
                                    string.Equals(other.RuTrackerPayload.SharedReleaseDir, payload.SharedReleaseDir, StringComparison.OrdinalIgnoreCase) &&
                                    (other.IsRunning || other.Status == "Ожидание" || other.Status.Contains("Подключение") || other.Status.Contains("Загрузка") || other.Status.Contains("Сборка")));
                            });
                            if (!anySiblingStillPending && payload.SlotNumber >= payload.TotalSlots - 1)
                            {
                                RuTrackerModDownloaderDialog.CleanupRuTrackerReleaseDirectory(payload.SharedReleaseDir, outDir);
                            }
                        }
                    }
                    catch { }
                }

                App.RunOnUI(() =>
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
                });

                if (buildSucceeded && payload.TotalSlots <= 1)
                {
                    item.RawStageBadge = "ГОТОВО";
                    item.RawStageColor = Windows.UI.Color.FromArgb(255, 34, 197, 94);
                    item.RawPercent = 100.0;
                    item.RawSpeedText = "100% Завершено";
                    item.RawDetails = "Сборка завершена";
                    item.IsActive = false;
                    RunOnUiSyncOrAsync(() =>
                    {
                        item.CommitUiUpdates();
                        SaveQueueState();
                    });
                    try { EvaluateQueuePriorities(); } catch { }
                }
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
                    .Where(f => f.IsSelected)
                    .GroupBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
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

                await service.DownloadTorrentAsync(
                    sharedReleaseDir,
                    unionFiles,
                    torrentProg,
                    ct,
                    service.LoadedTorrentBytes,
                    service.LoadedMagnet,
                    currentTopic?.TopicId,
                    currentTopic?.Seeds ?? 0,
                    item.PauseEvent,
                    mgr =>
                    {
                        item.ActiveTorrentManager = mgr;
                        foreach (var (t, _, _) in createdTasks)
                            t.ActiveTorrentManager = mgr;
                    }).ConfigureAwait(false);
            }

            // Распаковываем ТОЛЬКО ВЫБРАННЫЕ архивы модов (.7z / .zip / .rar)
            var unionSelectedRelPaths = unionFiles
                .Where(f => f.IsSelected)
                .SelectMany(f => f.GetEffectiveRelativePaths())
                .ToList();
            await RuTrackerModDownloaderDialog.ExtractLocalTorrentModArchivesAsync(sharedReleaseDir, ct, unionSelectedRelPaths).ConfigureAwait(false);

            // Собираем контейнеры
            var allDownloadedContainers = Directory.Exists(sharedReleaseDir)
                ? Directory.EnumerateFiles(sharedReleaseDir, "*.*", SearchOption.AllDirectories)
                    .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}Part_", StringComparison.OrdinalIgnoreCase) &&
                                (p.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) ||
                                 p.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) ||
                                 p.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) ||
                                 p.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase) ||
                                 p.EndsWith(".nro", StringComparison.OrdinalIgnoreCase)))
                    .ToList()
                : new List<string>();

            var allSlotTitles = createdTasks.Select(c => c.Payload.SlotGameTitle).ToList();

            // 2. Для каждой части/игры выполняем её облачные моды и сборку
            for (int i = 0; i < createdTasks.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var (task, payload, slot) = createdTasks[i];
                int slotNum = slot.SlotNumber;

                var matchedContainers = RuTrackerModDownloaderDialog.FilterDownloadedContainersForSlot(
                    allDownloadedContainers,
                    payload.TorrentFiles,
                    slotNum,
                    payload.SlotGameTitle,
                    allSlotTitles);

                foreach (var srcContainer in matchedContainers)
                {
                    string dstFile = Path.Combine(payload.TargetReleaseDir, Path.GetFileName(srcContainer));
                    if (!string.Equals(Path.GetFullPath(srcContainer), Path.GetFullPath(dstFile), StringComparison.OrdinalIgnoreCase) && !File.Exists(dstFile))
                    {
                        try { File.Move(srcContainer, dstFile); }
                        catch { try { File.Copy(srcContainer, dstFile, true); } catch { } }
                    }
                }

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
                    CustomMetadata = payload.CustomMetadata
                };

                await ExecuteSingleTaskPipelineAsync(item, task, slotExecPayload, ct).ConfigureAwait(false);
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
                SaveQueueState();
            });
            try { EvaluateQueuePriorities(); } catch { }
        }

        public static void UpdateItemTorrentProgress(RuTrackerActiveDownloadItem item, TorrentDownloadProgress p)
        {
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

            item.RawSpeedText = $"{p.SpeedMbPerSec:F2} МБ/с • Сиды: {p.Seeds}";

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
                if (item.IsPausedByUser || item.StageBadge == "ПАУЗА")
                {
                    item.IsPausedByUser = false;
                    item.LastActivatedAtUtc = DateTime.UtcNow;
                    item.SmoothedSpeedBps = 0;
                    item.PauseEvent.Set();
                    if (item.ActiveTorrentManager != null &&
                        (item.ActiveTorrentManager.State == TorrentState.Paused || item.ActiveTorrentManager.State == TorrentState.Stopped))
                    {
                        try { item.ActiveTorrentManager.StartAsync(); } catch { }
                    }
                    EvaluateQueuePriorities();
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
                        SaveQueueState();
                    });
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
                foreach (var item in _backingItems.Where(i => i.IsPausedByUser || i.StageBadge == "ПАУЗА"))
                {
                    item.IsPausedByUser = false;
                    item.LastActivatedAtUtc = nowUtc;
                    item.SmoothedSpeedBps = 0;
                    item.PauseEvent.Set();
                }
                EvaluateQueuePriorities();
                SaveQueueState();
            }
        }

        public void ClearFinished()
        {
            lock (_syncLock)
            {
                _backingItems.RemoveAll(i => i.StageBadge is "ГОТОВО" or "ОТМЕНЕНО" or "ОШИБКА");
                RunOnUiSyncOrAsync(() =>
                {
                    var finished = Items.Where(i => i.StageBadge is "ГОТОВО" or "ОТМЕНЕНО" or "ОШИБКА").ToList();
                    foreach (var item in finished)
                    {
                        Items.Remove(item);
                    }
                    SaveQueueState();
                });
            }
        }

        public void BindRestoredTasksFromViewModel(ViewModels.TasksViewModel? vm)
        {
            if (vm == null) return;
            lock (_syncLock)
            {
                foreach (var item in _backingItems)
                {
                    item.ViewModel = vm;
                    if (item.CreatedTasks == null) continue;

                    for (int i = 0; i < item.CreatedTasks.Count; i++)
                    {
                        var (oldTask, payload, slot) = item.CreatedTasks[i];
                        var matchedVmTask = vm.Tasks.FirstOrDefault(t =>
                            t == oldTask ||
                            (!string.IsNullOrEmpty(oldTask.Id) && t.Id == oldTask.Id) ||
                            (!string.IsNullOrEmpty(payload.SlotGameTitle) && t.GameName == payload.SlotGameTitle) ||
                            (!string.IsNullOrEmpty(oldTask.GameName) && t.GameName == oldTask.GameName) ||
                            (!string.IsNullOrEmpty(oldTask.OutputFileName) && !string.IsNullOrEmpty(oldTask.OutputFolder) &&
                             t.OutputFileName == oldTask.OutputFileName && t.OutputFolder == oldTask.OutputFolder));

                        if (matchedVmTask != null)
                        {
                            if (matchedVmTask.RuTrackerPayload == null)
                            {
                                matchedVmTask.RuTrackerPayload = payload;
                            }
                            if (matchedVmTask.Cts == null && oldTask.Cts != null)
                            {
                                matchedVmTask.Cts = oldTask.Cts;
                            }
                            item.CreatedTasks[i] = (matchedVmTask, payload, slot);

                            bool outputExists = false;
                            string outDir = string.IsNullOrWhiteSpace(matchedVmTask.OutputFolder)
                                ? (string.IsNullOrWhiteSpace(App.Settings.Current.OutputFolder) ? payload.TargetReleaseDir : App.Settings.Current.OutputFolder)
                                : matchedVmTask.OutputFolder;
                            if (Directory.Exists(outDir))
                            {
                                string displayTid = RuTrackerModService.NormalizeSwitchBaseApplicationTitleId(matchedVmTask.GameName);
                                string san = ProcessingTask.SanitizeFileName(matchedVmTask.GameName);
                                var files = Directory.GetFiles(outDir, "*.*").Where(f =>
                                    (f.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) ||
                                     f.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) ||
                                     f.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) ||
                                     f.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase)) &&
                                    new FileInfo(f).Length > 0);
                                outputExists = files.Any(f =>
                                    (!string.IsNullOrWhiteSpace(matchedVmTask.GameName) && Path.GetFileNameWithoutExtension(f).Contains(matchedVmTask.GameName, StringComparison.OrdinalIgnoreCase)) ||
                                    (!string.IsNullOrWhiteSpace(san) && Path.GetFileNameWithoutExtension(f).Contains(san, StringComparison.OrdinalIgnoreCase)) ||
                                    (!string.IsNullOrWhiteSpace(displayTid) && f.Contains(displayTid, StringComparison.OrdinalIgnoreCase)));
                            }

                            if (item.IsCompleted && outputExists && matchedVmTask.Status == "⏸ Сохранено на паузе")
                            {
                                matchedVmTask.Status = "Успешно";
                                matchedVmTask.Progress = 100;
                                matchedVmTask.IsRunning = false;
                                matchedVmTask.IsPaused = false;
                                matchedVmTask.EnsureFinalMetricsPopulated();
                            }
                            else
                            {
                                matchedVmTask.EnsureSourceMetricsPopulated();
                            }

                            if (matchedVmTask.GameIcon == null)
                            {
                                HistoryService.TryLoadIconForTask(matchedVmTask);
                            }
                        }
                    }
                }
            }
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
            {
                return false;
            }

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
                targetItem.LastActivatedAtUtc = DateTime.UtcNow;
                targetItem.SmoothedSpeedBps = 0;
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
                {
                    HistoryService.TryLoadIconForTask(task);
                }

                targetItem.CommitUiUpdates();
                EvaluateQueuePriorities();
                SaveQueueState();
            }

            return true;
        }


        private void SaveQueueState()
        {
            try
            {
                List<RuTrackerPersistedQueueRecord> records;
                lock (_syncLock)
                {
                    records = _backingItems.Select(i =>
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
                            foreach (var (t, payload, slot) in i.CreatedTasks)
                            {
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
                                    DeleteSourceAfterVerify = t.DeleteSourceAfterVerify,
                                    TopicId = payload.Topic?.TopicId ?? string.Empty,
                                    TopicTitle = payload.Topic?.Title ?? string.Empty,
                                    Seeds = payload.Topic?.Seeds ?? 0,
                                    MagnetLink = payload.MagnetLink?.ToV1String() ?? payload.MagnetLink?.ToString() ?? payload.Topic?.MagnetUri ?? string.Empty,
                                    TorrentBytesBase64 = payload.TorrentBytes != null ? Convert.ToBase64String(payload.TorrentBytes) : string.Empty,
                                    TorrentFiles = payload.TorrentFiles.Select(f => new PersistedTorrentFileItem
                                    {
                                        RelativePath = f.RelativePath,
                                        Length = f.Length,
                                        IsSelected = f.IsSelected,
                                        CategoryBadge = f.CategoryBadge,
                                        IsRealTorrentFile = f.IsRealTorrentFile
                                    }).ToList(),
                                    CloudLinks = payload.CloudLinks.ToList(),
                                    TaskTitle = t.GameName
                                });
                            }
                        }

                        return rec;
                    }).ToList();
                }

                string json = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_queueStatePath, json);
            }
            catch { }
        }

        private void LoadPersistedState()
        {
            try
            {
                if (File.Exists(_queueStatePath))
                {
                    string json = File.ReadAllText(_queueStatePath);
                    var records = JsonSerializer.Deserialize<List<RuTrackerPersistedQueueRecord>>(json);
                    if (records != null && records.Count > 0)
                    {
                        foreach (var r in records)
                        {
                            bool isCompleted = r.StageBadge is "ГОТОВО" or "ОТМЕНЕНО" or "ОШИБКА";
                            bool mayNeedPostBuildHeal = r.StageBadge == "СБОРКА" ||
                                (r.StageBadge == "ОШИБКА" && (string.IsNullOrWhiteSpace(r.ProgressDetails) || r.ProgressDetails == "Сборка мульти-контента..."));
                            if (mayNeedPostBuildHeal)
                            {
                                bool outputExists = false;
                                var tidMatch = System.Text.RegularExpressions.Regex.Match(r.DisplayTitle ?? string.Empty, @"\b(0100[0-9A-Fa-f]{12})\b");
                                string displayTid = tidMatch.Success ? tidMatch.Groups[1].Value : string.Empty;

                                if (r.Tasks != null)
                                {
                                    foreach (var tp in r.Tasks)
                                    {
                                        string outDir = string.IsNullOrWhiteSpace(tp.OutputFolder)
                                            ? (string.IsNullOrWhiteSpace(App.Settings.Current.OutputFolder) ? tp.TargetReleaseDir : App.Settings.Current.OutputFolder)
                                            : tp.OutputFolder;
                                        string gameName = !string.IsNullOrWhiteSpace(tp.TaskTitle) ? tp.TaskTitle : (!string.IsNullOrWhiteSpace(tp.SlotGameTitle) ? tp.SlotGameTitle : r.DisplayTitle);
                                        string sanGameName = ProcessingTask.SanitizeFileName(gameName);
                                        if (Directory.Exists(outDir))
                                        {
                                            var files = Directory.GetFiles(outDir, "*.*").Where(f =>
                                                (f.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) ||
                                                 f.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) ||
                                                 f.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) ||
                                                 f.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase)) &&
                                                new FileInfo(f).Length > 0);
                                            if (files.Any(f =>
                                                (!string.IsNullOrWhiteSpace(gameName) && Path.GetFileNameWithoutExtension(f).Contains(gameName, StringComparison.OrdinalIgnoreCase)) ||
                                                (!string.IsNullOrWhiteSpace(sanGameName) && Path.GetFileNameWithoutExtension(f).Contains(sanGameName, StringComparison.OrdinalIgnoreCase)) ||
                                                (!string.IsNullOrWhiteSpace(displayTid) && f.Contains(displayTid, StringComparison.OrdinalIgnoreCase)) ||
                                                (!string.IsNullOrWhiteSpace(tp.TopicId) && f.Contains(tp.TopicId, StringComparison.OrdinalIgnoreCase))))
                                            {
                                                outputExists = true;
                                                break;
                                            }
                                        }
                                    }
                                }
                                if (outputExists)
                                {
                                    isCompleted = true;
                                    r.StageBadge = "ГОТОВО";
                                    r.ProgressPercent = 100.0;
                                    r.ProgressDetails = "Сборка завершена";
                                }
                            }

                            var item = new RuTrackerActiveDownloadItem
                            {
                                BatchId = r.BatchId,
                                DisplayTitle = r.DisplayTitle,
                                StageBadge = isCompleted ? r.StageBadge : "ПАУЗА",
                                ProgressPercent = r.ProgressPercent,
                                ProgressDetails = r.ProgressDetails,
                                SpeedAndPeersText = r.StageBadge == "ГОТОВО" ? "100% Завершено" : string.Empty,
                                IsPausedByUser = isCompleted ? r.IsPausedByUser : true,
                                IsActive = false,
                                IsBuildCompletedOnDisk = r.StageBadge == "ГОТОВО",
                                SharedReleaseDir = r.SharedReleaseDir,
                                HasTorrentSource = r.HasTorrentSource
                            };
                            item.RawPercent = r.ProgressPercent;
                            item.RawDetails = r.ProgressDetails;
                            item.RawSpeedText = r.StageBadge == "ГОТОВО" ? "100% Завершено" : string.Empty;
                            item.RawStageBadge = item.StageBadge;
                            item.RawStageColor = item.StageBadge switch
                            {
                                "ГОТОВО" => Windows.UI.Color.FromArgb(255, 34, 197, 94),
                                "ОТМЕНЕНО" or "ОШИБКА" => Windows.UI.Color.FromArgb(255, 220, 38, 38),
                                "ПАУЗА" => Windows.UI.Color.FromArgb(255, 217, 119, 6),
                                _ => Windows.UI.Color.FromArgb(255, 100, 116, 139)
                            };

                            if (r.Tasks != null && r.Tasks.Count > 0)
                            {
                                foreach (var tp in r.Tasks)
                                {
                                    ProcessingTask? task = null;
                                    if (App.TasksVM != null)
                                    {
                                        task = App.TasksVM.Tasks.FirstOrDefault(vmTask =>
                                            (!string.IsNullOrEmpty(tp.TaskId) && vmTask.Id == tp.TaskId) ||
                                            (!string.IsNullOrEmpty(tp.TaskTitle) && vmTask.GameName == tp.TaskTitle) ||
                                            (!string.IsNullOrEmpty(tp.SlotGameTitle) && vmTask.GameName == tp.SlotGameTitle) ||
                                            (!string.IsNullOrEmpty(tp.OutputFolder) && !string.IsNullOrEmpty(tp.TaskTitle) &&
                                             vmTask.OutputFolder == tp.OutputFolder && vmTask.OutputFileName == ProcessingTask.SanitizeFileName(tp.TaskTitle)));
                                    }

                                    if (task == null)
                                    {
                                        task = new ProcessingTask
                                        {
                                            Id = !string.IsNullOrEmpty(tp.TaskId) ? tp.TaskId : Guid.NewGuid().ToString("N").Substring(0, 8),
                                            GameName = !string.IsNullOrWhiteSpace(tp.TaskTitle) ? tp.TaskTitle : tp.SlotGameTitle,
                                            TargetFormat = tp.OutputFormat,
                                            OutputFolder = tp.OutputFolder,
                                            DeleteSourceAfterVerify = tp.DeleteSourceAfterVerify,
                                            Progress = r.ProgressPercent,
                                            Status = isCompleted ? (r.StageBadge == "ГОТОВО" ? "Успешно" : r.StageBadge) : "⏸ Сохранено на паузе",
                                            IsRuTrackerTask = true,
                                            IsRunning = false,
                                            IsPaused = !isCompleted
                                        };
                                    }

                                    MonoTorrent.MagnetLink? mag = null;
                                    if (!string.IsNullOrEmpty(tp.MagnetLink))
                                    {
                                        try { MonoTorrent.MagnetLink.TryParse(tp.MagnetLink, out mag); } catch { }
                                    }

                                    byte[]? tBytes = null;
                                    if (!string.IsNullOrEmpty(tp.TorrentBytesBase64))
                                    {
                                        try { tBytes = Convert.FromBase64String(tp.TorrentBytesBase64); } catch { }
                                    }

                                    var payload = new RuTrackerTaskPayload
                                    {
                                        SlotNumber = tp.SlotNumber,
                                        PosterUrl = tp.PosterUrl,
                                        SlotGameTitle = tp.SlotGameTitle,
                                        CustomModTitle = tp.CustomModTitle,
                                        TargetReleaseDir = tp.TargetReleaseDir,
                                        AutoBuild = tp.AutoBuild,
                                        AppendAutoTags = tp.IncludeTechTags,
                                        SharedReleaseDir = r.SharedReleaseDir,
                                        HasTorrentSource = r.HasTorrentSource,
                                        CloudLinks = tp.CloudLinks.ToList(),
                                        TorrentBytes = tBytes,
                                        MagnetLink = mag,
                                        Topic = new RuTrackerTopicItem
                                        {
                                            TopicId = tp.TopicId,
                                            Title = tp.TopicTitle,
                                            Seeds = tp.Seeds,
                                            MagnetUri = tp.MagnetLink
                                        },
                                        TorrentFiles = tp.TorrentFiles.Select(tf => new TorrentFileSelectionItem
                                        {
                                            RelativePath = tf.RelativePath,
                                            Length = tf.Length,
                                            IsSelected = tf.IsSelected,
                                            CategoryBadge = tf.CategoryBadge,
                                            IsRealTorrentFile = tf.IsRealTorrentFile
                                        }).ToList()
                                    };

                                    task.RuTrackerPayload = payload;
                                    item.CreatedTasks.Add((task, payload, new RuTrackerGameSlotItem { SlotNumber = tp.SlotNumber }));
                                }
                            }
                            else if (!isCompleted)
                            {
                                // Устаревшие записи без данных задач: помечаем "ОТМЕНЕНО", чтобы не блокировать очередь
                                item.StageBadge = "ОТМЕНЕНО";
                                item.RawStageBadge = "ОТМЕНЕНО";
                                item.RawStageColor = Windows.UI.Color.FromArgb(255, 220, 38, 38);
                                item.RawDetails = "Сессия сброшена";
                            }

                            item.CommitUiUpdates();

                            lock (_syncLock)
                            {
                                _backingItems.Add(item);
                            }

                            RunOnUiSyncOrAsync(() =>
                            {
                                Items.Add(item);
                            });
                        }
                    }
                }
            }
            catch { }
        }
    }
}
