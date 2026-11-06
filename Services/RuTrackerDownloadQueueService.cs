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

        private string _displayTitle = string.Empty;
        public string DisplayTitle
        {
            get => _displayTitle;
            set => SetProperty(ref _displayTitle, value);
        }

        private string _stageBadge = "В ОЧЕРЕДИ";
        public string StageBadge
        {
            get => _stageBadge;
            set
            {
                if (SetProperty(ref _stageBadge, value))
                {
                    OnPropertyChanged(nameof(PauseResumeGlyph));
                    OnPropertyChanged(nameof(PauseResumeToolTip));
                }
            }
        }

        private SolidColorBrush _stageBrush = new(Windows.UI.Color.FromArgb(255, 100, 116, 139)); // Slate #64748B
        public SolidColorBrush StageBrush
        {
            get => _stageBrush;
            set => SetProperty(ref _stageBrush, value);
        }

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
            set => SetProperty(ref _progressDetails, value);
        }

        private string _speedAndPeersText = string.Empty;
        public string SpeedAndPeersText
        {
            get => _speedAndPeersText;
            set => SetProperty(ref _speedAndPeersText, value);
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

        public string PauseResumeGlyph => (IsPausedByUser || StageBadge == "ПАУЗА") ? "\uE768" : "\uE769"; // E768 = Play, E769 = Pause
        public string PauseResumeToolTip => (IsPausedByUser || StageBadge == "ПАУЗА") ? "Продолжить скачивание" : "Приостановить скачивание";

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
        public bool DeleteSourceAfterVerify { get; set; } = true;
        public Task? ExecutionTask { get; set; }

        public void CommitUiUpdates()
        {
            if (Math.Abs(ProgressPercent - RawPercent) > 0.05)
                ProgressPercent = RawPercent;
            if (ProgressDetails != RawDetails && !string.IsNullOrEmpty(RawDetails))
                ProgressDetails = RawDetails;
            if (SpeedAndPeersText != RawSpeedText && !string.IsNullOrEmpty(RawSpeedText))
                SpeedAndPeersText = RawSpeedText;
            if (StageBadge != RawStageBadge && !string.IsNullOrEmpty(RawStageBadge))
            {
                StageBadge = RawStageBadge;
                OnPropertyChanged(nameof(PauseResumeGlyph));
                OnPropertyChanged(nameof(PauseResumeToolTip));
            }
            if (StageBrush.Color != RawStageColor)
                StageBrush = new SolidColorBrush(RawStageColor);
            CurrentSpeedBps = RawSpeedBps;
            CurrentSeeds = RawSeeds;
            CurrentPeers = RawPeers;
        }
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
    }

    public sealed class RuTrackerDownloadQueueService
    {
        public static RuTrackerDownloadQueueService Instance { get; } = new();

        public const int MaxConcurrentDownloads = 2;
        public ObservableCollection<RuTrackerActiveDownloadItem> Items { get; } = new();

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
        }

        private void OnTimerCallback(object? state)
        {
            App.RunOnUI(() =>
            {
                OnUiTimerTick();
            });
        }

        private void OnUiTimerTick()
        {
            if (Items.Count == 0) return;

            double totalSpeedBps = 0;
            int maxSeeds = 0;
            int maxPeers = 0;

            foreach (var item in Items)
            {
                item.CommitUiUpdates();

                if (item.IsActive && item.StageBadge is "СКАЧИВАНИЕ" or "МОДЫ" or "МЕТАДАННЫЕ")
                {
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

            int inList = Items.Count;
            int downloading = Items.Count(i => i.StageBadge is "СКАЧИВАНИЕ" or "МОДЫ" or "МЕТАДАННЫЕ");
            int inQueue = Items.Count(i => i.StageBadge is "В ОЧЕРЕДИ");
            int finished = Items.Count(i => i.StageBadge is "ГОТОВО");

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
                App.RunOnUI(() =>
                {
                    Items.Add(item);
                });
            }
            EvaluateQueuePriorities();
            SaveQueueState();
        }

        public void EvaluateQueuePriorities()
        {
            lock (_syncLock)
            {
                // Находим все кандидаты на загрузку: не завершены, не отменены, не ошибочны и не на паузе пользователем
                var candidates = Items
                    .Where(i => i.StageBadge is not ("ГОТОВО" or "ОТМЕНЕНО" or "ОШИБКА") && !i.IsPausedByUser)
                    .ToList();

                var shouldBeActive = candidates.Take(MaxConcurrentDownloads).ToHashSet();

                // 1. Вытесняем элементы за пределами топ-2 в статус "В ОЧЕРЕДИ"
                foreach (var item in Items)
                {
                    if (item.StageBadge is "ГОТОВО" or "ОТМЕНЕНО" or "ОШИБКА" || item.IsPausedByUser)
                        continue;

                    if (!shouldBeActive.Contains(item))
                    {
                        if (item.IsPipelineStarted && !item.IsPreemptedByQueue)
                        {
                            item.IsPreemptedByQueue = true;
                            item.PauseEvent.Reset();
                            if (item.ActiveTorrentManager != null &&
                                (item.ActiveTorrentManager.State == TorrentState.Downloading || item.ActiveTorrentManager.State == TorrentState.Metadata))
                            {
                                try { item.ActiveTorrentManager.PauseAsync(); } catch { }
                            }
                        }

                        item.RawStageBadge = "В ОЧЕРЕДИ";
                        item.RawStageColor = Windows.UI.Color.FromArgb(255, 100, 116, 139);
                        item.RawDetails = "В очереди закачек...";
                        item.RawSpeedText = "В очереди";
                        item.RawSpeedBps = 0;

                        foreach (var (t, _, _) in item.CreatedTasks)
                        {
                            App.RunOnUI(() =>
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

                // 2. Активируем или возобновляем элементы из топ-2
                foreach (var item in shouldBeActive)
                {
                    if (item.IsPreemptedByQueue)
                    {
                        item.IsPreemptedByQueue = false;
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
                        StartItemPipeline(item);
                    }
                }
            }
        }

        private void StartItemPipeline(RuTrackerActiveDownloadItem item)
        {
            item.IsPipelineStarted = true;
            item.IsActive = true;
            item.IsPreemptedByQueue = false;
            item.PauseEvent.Set();
            item.RawStageBadge = item.HasTorrentSource ? "СКАЧИВАНИЕ" : "МОДЫ";
            item.RawStageColor = Windows.UI.Color.FromArgb(255, 2, 132, 199);
            item.RawDetails = "Подключение к рою...";
            item.RawSpeedText = "0,00 МБ/с • Сиды: 0";

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
                        App.RunOnUI(() =>
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
                    item.IsActive = false;
                    item.RawSpeedBps = 0;
                    item.RawStageBadge = "ОШИБКА";
                    item.RawStageColor = Windows.UI.Color.FromArgb(255, 239, 68, 68);
                    item.RawSpeedText = "Ошибка";
                    item.RawDetails = ex.Message;
                    App.Logger?.Log($"[RuTracker Queue] Ошибка выполнения пакета {item.BatchId}: {ex.Message}", LogLevel.Error);
                    foreach (var (t, _, _) in item.CreatedTasks)
                    {
                        App.RunOnUI(() =>
                        {
                            t.Status = $"Ошибка: {ex.Message}";
                            t.IsRunning = false;
                            t.IsPaused = false;
                            t.Speed = string.Empty;
                        });
                    }
                }
                finally
                {
                    item.IsActive = false;
                    item.RawSpeedBps = 0;
                    if (item.RawStageBadge != "ОТМЕНЕНО" && item.RawStageBadge != "ОШИБКА")
                    {
                        item.RawStageBadge = "ГОТОВО";
                        item.RawStageColor = Windows.UI.Color.FromArgb(255, 34, 197, 94);
                        item.RawPercent = 100;
                        item.RawSpeedText = "100% Завершено";
                        item.RawDetails = "Задачи созданы";
                    }

                    App.RunOnUI(() =>
                    {
                        item.CommitUiUpdates();
                    });

                    SaveQueueState();
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

                if (task.GameIcon == null)
                {
                    string builtContainer = Path.Combine(outDir, task.OutputFileName + outExt);
                    if (File.Exists(builtContainer))
                    {
                        RuTrackerModDownloaderDialog.TryExtractAndSetTaskIcon(task, new[] { builtContainer });
                    }
                }

                bool buildSucceeded = task.Status != "Ошибка" && task.Status != "Отменено";
                if (task.DeleteSourceAfterVerify && buildSucceeded)
                {
                    RuTrackerModDownloaderDialog.CleanupRuTrackerReleaseDirectory(payload.TargetReleaseDir, outDir);
                    if (!string.IsNullOrWhiteSpace(payload.SharedReleaseDir) &&
                        !string.Equals(payload.SharedReleaseDir, payload.TargetReleaseDir, StringComparison.OrdinalIgnoreCase))
                    {
                        bool anySiblingStillPending = item.ViewModel != null && item.ViewModel.Tasks.Any(other =>
                            other != task &&
                            other.RuTrackerPayload != null &&
                            string.Equals(other.RuTrackerPayload.SharedReleaseDir, payload.SharedReleaseDir, StringComparison.OrdinalIgnoreCase) &&
                            (other.IsRunning || other.Status == "Ожидание" || other.Status.Contains("Подключение") || other.Status.Contains("Загрузка") || other.Status.Contains("Сборка")));
                        if (!anySiblingStillPending)
                        {
                            RuTrackerModDownloaderDialog.CleanupRuTrackerReleaseDirectory(payload.SharedReleaseDir, outDir);
                        }
                    }
                }

                App.RunOnUI(() =>
                {
                    if (task.Status != "Ошибка" && task.Status != "Отменено")
                    {
                        task.Status = "Успешно";
                        task.Progress = 100;
                    }
                    task.IsRunning = false;
                    task.IsPaused = false;
                    task.Speed = string.Empty;
                    task.FinishedAt = DateTime.Now;
                });
            }
            else
            {
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
            item.ViewModel?.SavePersistedTasks();
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
            if (item.IsPausedByUser || item.StageBadge == "ПАУЗА")
            {
                item.IsPausedByUser = false;
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
                    App.RunOnUI(() =>
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

        public void CancelOrRemoveItem(RuTrackerActiveDownloadItem item)
        {
            if (item.StageBadge is "ГОТОВО" or "ОТМЕНЕНО" or "ОШИБКА")
            {
                App.RunOnUI(() =>
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
                App.RunOnUI(() =>
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

        public void MoveItemUp(RuTrackerActiveDownloadItem item)
        {
            App.RunOnUI(() =>
            {
                int idx = Items.IndexOf(item);
                if (idx > 0)
                {
                    Items.Move(idx, idx - 1);
                    EvaluateQueuePriorities();
                    SaveQueueState();
                }
            });
        }

        public void MoveItemDown(RuTrackerActiveDownloadItem item)
        {
            App.RunOnUI(() =>
            {
                int idx = Items.IndexOf(item);
                if (idx >= 0 && idx < Items.Count - 1)
                {
                    Items.Move(idx, idx + 1);
                    EvaluateQueuePriorities();
                    SaveQueueState();
                }
            });
        }

        public void PauseAll()
        {
            foreach (var item in Items.Where(i => i.StageBadge is not ("ГОТОВО" or "ОТМЕНЕНО" or "ОШИБКА")))
            {
                if (!item.IsPausedByUser)
                {
                    TogglePauseItem(item);
                }
            }
        }

        public void ResumeAll()
        {
            foreach (var item in Items.Where(i => i.IsPausedByUser || i.StageBadge == "ПАУЗА"))
            {
                item.IsPausedByUser = false;
                item.PauseEvent.Set();
            }
            EvaluateQueuePriorities();
            SaveQueueState();
        }

        public void ClearFinished()
        {
            App.RunOnUI(() =>
            {
                var finished = Items.Where(i => i.StageBadge is "ГОТОВО" or "ОТМЕНЕНО" or "ОШИБКА").ToList();
                foreach (var item in finished)
                {
                    Items.Remove(item);
                }
                SaveQueueState();
            });
        }

        private void SaveQueueState()
        {
            try
            {
                var records = Items.Select(i => new RuTrackerPersistedQueueRecord
                {
                    BatchId = i.BatchId,
                    DisplayTitle = i.DisplayTitle,
                    StageBadge = i.StageBadge,
                    ProgressPercent = i.ProgressPercent,
                    ProgressDetails = i.ProgressDetails,
                    IsPausedByUser = i.IsPausedByUser,
                    SavedAt = DateTime.Now
                }).ToList();

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
                        App.RunOnUI(() =>
                        {
                            foreach (var r in records)
                            {
                                var item = new RuTrackerActiveDownloadItem
                                {
                                    BatchId = r.BatchId,
                                    DisplayTitle = r.DisplayTitle,
                                    StageBadge = r.StageBadge is "СКАЧИВАНИЕ" or "МОДЫ" or "МЕТАДАННЫЕ" ? "ПАУЗА" : r.StageBadge,
                                    ProgressPercent = r.ProgressPercent,
                                    ProgressDetails = r.ProgressDetails,
                                    IsPausedByUser = r.IsPausedByUser || (r.StageBadge is "СКАЧИВАНИЕ" or "МОДЫ" or "МЕТАДАННЫЕ"),
                                    IsActive = false
                                };
                                item.RawPercent = r.ProgressPercent;
                                item.RawDetails = r.ProgressDetails;
                                item.RawStageBadge = item.StageBadge;
                                item.RawStageColor = item.StageBadge switch
                                {
                                    "ГОТОВО" => Windows.UI.Color.FromArgb(255, 34, 197, 94),
                                    "ОТМЕНЕНО" or "ОШИБКА" => Windows.UI.Color.FromArgb(255, 220, 38, 38),
                                    "ПАУЗА" => Windows.UI.Color.FromArgb(255, 217, 119, 6),
                                    _ => Windows.UI.Color.FromArgb(255, 100, 116, 139)
                                };
                                item.CommitUiUpdates();
                                Items.Add(item);
                            }
                        });
                    }
                }
            }
            catch { }
        }
    }
}
