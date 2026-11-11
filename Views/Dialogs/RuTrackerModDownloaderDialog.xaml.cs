using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using StormSwitchBox.Models;
using StormSwitchBox.Services;
using StormSwitchBox.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace StormSwitchBox.Views.Dialogs
{
    public sealed partial class RuTrackerModDownloaderDialog : ContentDialog
    {
        public static RuTrackerModDownloaderDialog? ActiveInstance { get; private set; }
        private TaskCompletionSource<string?>? _cfTcs;
        private readonly TasksViewModel _viewModel;
        private readonly ProcessingTask? _targetTask;
        private readonly RuTrackerModService _service = new();
        private ProcessingTask? _activeTask;

        private readonly List<RuTrackerTopicItem> _allFetchedTopics = new();
        private readonly ObservableCollection<RuTrackerTopicItem> _topics = new();
        private readonly ObservableCollection<RuTrackerGameSlotItem> _gameSlots = new();
        private readonly ObservableCollection<TorrentFileSelectionItem> _torrentFiles = new();
        private readonly ObservableCollection<CloudModLinkItem> _discussionLinks = new();
        private readonly ObservableCollection<RuTrackerFileGroupBlock> _fileGroups = new();
        private readonly ObservableCollection<RuTrackerLinkGroupBlock> _linkGroups = new();
        private readonly ObservableCollection<RuTrackerActiveDownloadItem> _activeDownloadRows = new();
        private DispatcherTimer? _uiAggregationTimer;
        private DispatcherTimer? _instantSearchDebounceTimer;
        private bool _isUiInitialized;
        private bool _suppressInstantSearchWatcher;
        private bool _suppressTopicSelectionInspect;
        private bool _suppressGroupRebuild;
        private bool _suppressSlotMergeSelection;

        private string _workspaceMode = "Overview";
        private bool _isTopicsPanelCollapsed;
        private bool _isCloudPanelCollapsed;
        private bool _isQueuePanelExpanded = true;
        private int _lastObservedQueueCount;

        private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush BrushStageActive =
            new(Windows.UI.Color.FromArgb(255, 2, 132, 199));
        private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush BrushStageQueue =
            new(Windows.UI.Color.FromArgb(255, 100, 116, 139));
        private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush BrushStageDone =
            new(Windows.UI.Color.FromArgb(255, 34, 197, 94));
        private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush BrushModeActiveBg =
            new(Windows.UI.Color.FromArgb(255, 109, 40, 217));
        private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush BrushModeInactiveBg =
            new(Windows.UI.Color.FromArgb(0, 0, 0, 0));
        private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush BrushModeActiveFg =
            new(Windows.UI.Color.FromArgb(255, 248, 250, 252));
        private static readonly Microsoft.UI.Xaml.Media.SolidColorBrush BrushModeInactiveFg =
            new(Windows.UI.Color.FromArgb(255, 203, 213, 225));

        private string? _localTorrentPath;
        private string _currentTopicTitle = string.Empty;
        private string _currentTopicUrl = string.Empty;
        private string _currentTopicPosterUrl = string.Empty;
        private CancellationTokenSource? _downloadCts;
        private readonly List<CancellationTokenSource> _activeBatchCtsList = new();
        private readonly object _batchLock = new();
        private int _activeBatchesCount = 0;

        private Action? _langHandler;

        public RuTrackerModDownloaderDialog(TasksViewModel viewModel, ProcessingTask? targetTask = null)
        {
            this.InitializeComponent();

            _langHandler = () =>
            {
                App.RunOnUI(() =>
                {
                    ApplyLocalization();
                    App.Localization.TranslateRoot(this);
                });
            };

            this.Loaded += (_, _) =>
            {
                App.Localization.RegisterRoot(this);
                ApplyLocalization();
                App.Localization.TranslateRoot(this);
                App.Localization.LanguageChanged += _langHandler;
            };

            this.Unloaded += (_, _) =>
            {
                App.Localization.UnregisterRoot(this);
                if (_langHandler != null) App.Localization.LanguageChanged -= _langHandler;
            };
            this.XamlRoot = App.MainWindow.Content.XamlRoot;
            this.RequestedTheme = (App.MainWindow.Content as FrameworkElement)?.RequestedTheme ?? ElementTheme.Dark;

            _viewModel = viewModel;
            _targetTask = targetTask;

            TopicsListView.ItemsSource = _topics;
            GameSlotsListView.ItemsSource = _gameSlots;
            TorrentFilesListView.ItemsSource = _fileGroups;
            DiscussionLinksListView.ItemsSource = _linkGroups;
            ActiveDownloadsItemsControl.ItemsSource = RuTrackerDownloadQueueService.Instance.Items;
            _uiAggregationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _uiAggregationTimer.Tick += UiAggregationTimer_Tick;
            _uiAggregationTimer.Start();

            _instantSearchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(95) };
            _instantSearchDebounceTimer.Tick += InstantSearchDebounceTimer_Tick;

            string initialFormat = !string.IsNullOrWhiteSpace(_targetTask?.TargetFormat)
                ? _targetTask.TargetFormat
                : (!string.IsNullOrWhiteSpace(_viewModel?.SelectedFormat) ? _viewModel.SelectedFormat : "NSP");
            for (int i = 0; i < GlobalSlotFormatCombo.Items.Count; i++)
            {
                if (GlobalSlotFormatCombo.Items[i] is ComboBoxItem cbi &&
                    string.Equals(cbi.Content?.ToString(), initialFormat, StringComparison.OrdinalIgnoreCase))
                {
                    GlobalSlotFormatCombo.SelectedIndex = i;
                    break;
                }
            }

            InitializeDefaultGameSlot("Игра 1");

            LoadSavedSettings();
            ActiveInstance = this;
            RuTrackerModService.CloudflareChallengeHandler = SolveCloudflareChallengeInDialogAsync;
            RuTrackerModService.SameOriginFetchHandler = FetchViaSameOriginInDialogAsync;
            RuTrackerModService.SameOriginViewTorrentHandler = FetchViewTorrentTreeInDialogAsync;
            RuTrackerModService.SameOriginTorrentBytesHandler = FetchTorrentBytesViaSameOriginInDialogAsync;
            this.Opened += Dialog_Opened;
            if (this.XamlRoot != null)
            {
                this.XamlRoot.Changed += XamlRoot_Changed;
            }
            CloudflareWebView.Loaded += CloudflareWebView_Loaded;

            if (_targetTask != null)
            {
                TargetTaskBanner.Visibility = Visibility.Visible;
                TargetTaskBannerText.Text = $"Целевая задача: {_targetTask.GameName} ({_targetTask.SourceFormat} -> {_targetTask.TargetFormat})";
                if (StartFullDownloadBtnText != null)
                    StartFullDownloadBtnText.Text = "Скачать выбранное и привязать к задаче";
                if (!string.IsNullOrWhiteSpace(_targetTask.GameName))
                {
                    string cleanQuery = System.Text.RegularExpressions.Regex.Replace(_targetTask.GameName, @"\[.*?\]|\(.*?\)", "").Trim();
                    _suppressInstantSearchWatcher = true;
                    SearchOrUrlBox.Text = cleanQuery;
                    _suppressInstantSearchWatcher = false;
                    ClearSearchBoxBtn.Visibility = string.IsNullOrEmpty(cleanQuery) ? Visibility.Collapsed : Visibility.Visible;
                }
            }

            _isUiInitialized = true;
            UpdateDialogAdaptiveSize();
            ApplyWorkspaceLayout();
        }

        private void LoadSavedSettings()
        {
            var cfg = App.Settings.Current;

            string mirror = string.IsNullOrWhiteSpace(cfg.RuTrackerMirror) ? "rutracker.org" : cfg.RuTrackerMirror;
            for (int i = 0; i < MirrorCombo.Items.Count; i++)
            {
                if (MirrorCombo.Items[i] is ComboBoxItem item && string.Equals(item.Tag?.ToString(), mirror, StringComparison.OrdinalIgnoreCase))
                {
                    MirrorCombo.SelectedIndex = i;
                    break;
                }
            }

            UseDoHCheckBox.IsChecked = cfg.RuTrackerUseDoH;
            ProxyBox.Text = cfg.RuTrackerProxy ?? string.Empty;
            LoginUserBox.Text = cfg.RuTrackerUsername ?? string.Empty;
            CookieBox.Text = cfg.RuTrackerCookie ?? string.Empty;
            AutoBuildCheckBox.IsChecked = cfg.RuTrackerAutoBuildTask;
            DeleteRuTrackerSourceCheckBox.IsChecked = cfg.RuTrackerDeleteSourceOnSuccess;

            string defaultDlFolder = !string.IsNullOrWhiteSpace(cfg.RuTrackerDownloadFolder)
                ? cfg.RuTrackerDownloadFolder
                : (!string.IsNullOrWhiteSpace(cfg.OutputFolder)
                    ? Path.Combine(cfg.OutputFolder, "RuTracker_Downloads")
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "STORM_SWITCH_BOX_Downloads"));
            DownloadFolderBox.Text = defaultDlFolder;
            if (!string.IsNullOrWhiteSpace(CookieBox.Text))
            {
                AuthStatusText.Text = "Статус: Активна сессия по Cookie bb_session (доступен полный поиск и .torrent файлы)";
            }
        }

        private void SaveCurrentSettings()
        {
            var cfg = App.Settings.Current;
            if (MirrorCombo.SelectedItem is ComboBoxItem mirrorItem && mirrorItem.Tag != null)
                cfg.RuTrackerMirror = mirrorItem.Tag.ToString() ?? "rutracker.org";

            cfg.RuTrackerUseDoH = UseDoHCheckBox.IsChecked == true;
            cfg.RuTrackerProxy = ProxyBox.Text?.Trim() ?? string.Empty;
            cfg.RuTrackerUsername = LoginUserBox.Text?.Trim() ?? string.Empty;
            cfg.RuTrackerCookie = CookieBox.Text?.Trim() ?? string.Empty;
            cfg.RuTrackerDownloadFolder = DownloadFolderBox.Text?.Trim() ?? string.Empty;
            cfg.RuTrackerAutoBuildTask = AutoBuildCheckBox.IsChecked == true;
            cfg.RuTrackerDeleteSourceOnSuccess = DeleteRuTrackerSourceCheckBox.IsChecked == true;
            _ = App.Settings.SaveAsync();
        }

        private void Dialog_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                if (SlotMetadataStudioOverlay.Visibility == Visibility.Visible)
                {
                    e.Handled = true;
                    SlotMetadataStudioOverlay.Visibility = Visibility.Collapsed;
                    return;
                }
                e.Handled = true;
                this.Hide();
            }
        }

        private void CloseDialogTopBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        private void XamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args)
        {
            UpdateDialogAdaptiveSize();
        }

        private void UpdateDialogAdaptiveSize()
        {
            if (RootWorkspaceGrid == null || this.XamlRoot == null) return;
            double hostW = this.XamlRoot.Size.Width;
            double hostH = this.XamlRoot.Size.Height;
            if (hostW > 400 && hostH > 300)
            {
                RootWorkspaceGrid.Width = Math.Clamp(hostW - 32.0, 960.0, 1860.0);
                RootWorkspaceGrid.Height = Math.Clamp(hostH - 32.0, 610.0, 1020.0);
            }
        }

        private void WorkspaceMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is string mode && !string.IsNullOrWhiteSpace(mode))
            {
                _workspaceMode = mode;
                if (mode == "CloudMods" && ManualCloudLinksExpander != null)
                {
                    ManualCloudLinksExpander.IsExpanded = true;
                }
                else if (mode == "Downloads")
                {
                    _isQueuePanelExpanded = true;
                }
                ApplyWorkspaceLayout();
            }
        }

        private void ToggleTopicsPanel_Click(object sender, RoutedEventArgs e)
        {
            if (_workspaceMode != "Overview")
            {
                _workspaceMode = "Overview";
                _isTopicsPanelCollapsed = false;
            }
            else
            {
                _isTopicsPanelCollapsed = !_isTopicsPanelCollapsed;
            }
            ApplyWorkspaceLayout();
        }

        private void ToggleCloudPanel_Click(object sender, RoutedEventArgs e)
        {
            if (_workspaceMode != "Overview")
            {
                _workspaceMode = "Overview";
                _isCloudPanelCollapsed = false;
            }
            else
            {
                _isCloudPanelCollapsed = !_isCloudPanelCollapsed;
            }
            ApplyWorkspaceLayout();
        }

        private void ApplyWorkspaceLayout()
        {
            if (!_isUiInitialized || MainBentoGrid == null) return;

            void StyleModeBtn(Button? btn, bool active)
            {
                if (btn == null) return;
                btn.Background = active ? BrushModeActiveBg : BrushModeInactiveBg;
                btn.Foreground = active ? BrushModeActiveFg : BrushModeInactiveFg;
            }

            StyleModeBtn(ModeOverviewBtn, _workspaceMode == "Overview");
            StyleModeBtn(ModeGamesFilesBtn, _workspaceMode == "GamesFiles");
            StyleModeBtn(ModeCloudModsBtn, _workspaceMode == "CloudMods");
            StyleModeBtn(ModeDownloadsBtn, _workspaceMode == "Downloads");

            if (_workspaceMode == "Downloads")
            {
                MainBentoGrid.Visibility = Visibility.Collapsed;
                WorkspaceBentoRow.Height = new GridLength(0);
                QueuePanelRow.Height = new GridLength(1, GridUnitType.Star);
                QueueListRowDef.Height = new GridLength(1, GridUnitType.Star);
                ActiveDownloadsScrollViewer.Visibility = Visibility.Visible;
                ActiveDownloadsItemsControl.MaxHeight = 2000;
                ToggleQueueExpandIcon.Glyph = "\uE70E";
                RestoreTopicsBtn.Visibility = Visibility.Collapsed;
                RestoreCloudBtn.Visibility = Visibility.Collapsed;
                return;
            }

            MainBentoGrid.Visibility = Visibility.Visible;
            WorkspaceBentoRow.Height = new GridLength(1, GridUnitType.Star);
            QueuePanelRow.Height = GridLength.Auto;
            QueueListRowDef.Height = GridLength.Auto;
            ActiveDownloadsItemsControl.MaxHeight = 132;
            SyncQueueVisibilityState();

            if (_workspaceMode == "GamesFiles")
            {
                // 50% / 50% на всю высоту: Игры и части слева, Файлы в раздаче справа
                TopicsCardBorder.Visibility = Visibility.Collapsed;
                CloudCardBorder.Visibility = Visibility.Collapsed;
                GameSlotsCardBorder.Visibility = Visibility.Visible;
                TorrentFilesCardBorder.Visibility = Visibility.Visible;

                BentoTopicsCol.Width = new GridLength(0);
                BentoCenterCol.Width = new GridLength(1.0, GridUnitType.Star);
                BentoCloudCol.Width = new GridLength(1.08, GridUnitType.Star);

                BentoUpperRow.Height = new GridLength(1.0, GridUnitType.Star);
                BentoLowerRow.Height = new GridLength(0);

                Grid.SetRow(GameSlotsCardBorder, 0);
                Grid.SetRowSpan(GameSlotsCardBorder, 2);
                Grid.SetColumn(GameSlotsCardBorder, 1);

                Grid.SetRow(TorrentFilesCardBorder, 0);
                Grid.SetRowSpan(TorrentFilesCardBorder, 2);
                Grid.SetColumn(TorrentFilesCardBorder, 2);

                RestoreTopicsBtn.Visibility = Visibility.Collapsed;
                RestoreCloudBtn.Visibility = Visibility.Collapsed;
            }
            else if (_workspaceMode == "CloudMods")
            {
                // Фокус на слотах игр слева и облачных ссылках / параметрах сборки справа
                TopicsCardBorder.Visibility = Visibility.Collapsed;
                TorrentFilesCardBorder.Visibility = Visibility.Collapsed;
                GameSlotsCardBorder.Visibility = Visibility.Visible;
                CloudCardBorder.Visibility = Visibility.Visible;

                BentoTopicsCol.Width = new GridLength(0);
                BentoCenterCol.Width = new GridLength(1.05, GridUnitType.Star);
                BentoCloudCol.Width = new GridLength(1.05, GridUnitType.Star);

                BentoUpperRow.Height = new GridLength(1.0, GridUnitType.Star);
                BentoLowerRow.Height = new GridLength(0);

                Grid.SetRow(GameSlotsCardBorder, 0);
                Grid.SetRowSpan(GameSlotsCardBorder, 2);
                Grid.SetColumn(GameSlotsCardBorder, 1);

                Grid.SetRow(CloudCardBorder, 0);
                Grid.SetRowSpan(CloudCardBorder, 2);
                Grid.SetColumn(CloudCardBorder, 2);

                RestoreTopicsBtn.Visibility = Visibility.Collapsed;
                RestoreCloudBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                // Overview: Асимметричная Bento-сетка с полноразмерным списком раздач слева
                TopicsCardBorder.Visibility = _isTopicsPanelCollapsed ? Visibility.Collapsed : Visibility.Visible;
                CloudCardBorder.Visibility = _isCloudPanelCollapsed ? Visibility.Collapsed : Visibility.Visible;
                GameSlotsCardBorder.Visibility = Visibility.Visible;
                TorrentFilesCardBorder.Visibility = Visibility.Visible;

                BentoTopicsCol.Width = _isTopicsPanelCollapsed ? new GridLength(0) : new GridLength(0.82, GridUnitType.Star);
                BentoCenterCol.Width = new GridLength(1.48, GridUnitType.Star);
                BentoCloudCol.Width = _isCloudPanelCollapsed ? new GridLength(0) : new GridLength(0.92, GridUnitType.Star);

                BentoUpperRow.Height = new GridLength(0.96, GridUnitType.Star);
                BentoLowerRow.Height = new GridLength(1.14, GridUnitType.Star);

                Grid.SetRow(TopicsCardBorder, 0);
                Grid.SetRowSpan(TopicsCardBorder, 2);
                Grid.SetColumn(TopicsCardBorder, 0);

                Grid.SetRow(GameSlotsCardBorder, 0);
                Grid.SetRowSpan(GameSlotsCardBorder, 1);
                Grid.SetColumn(GameSlotsCardBorder, 1);

                Grid.SetRow(TorrentFilesCardBorder, 1);
                Grid.SetRowSpan(TorrentFilesCardBorder, 1);
                Grid.SetColumn(TorrentFilesCardBorder, 1);

                Grid.SetRow(CloudCardBorder, 0);
                Grid.SetRowSpan(CloudCardBorder, 2);
                Grid.SetColumn(CloudCardBorder, 2);

                RestoreTopicsBtn.Visibility = _isTopicsPanelCollapsed ? Visibility.Visible : Visibility.Collapsed;
                RestoreCloudBtn.Visibility = _isCloudPanelCollapsed ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void SyncQueueVisibilityState()
        {
            if (ActiveDownloadsScrollViewer == null) return;
            int queueCount = RuTrackerDownloadQueueService.Instance.Items.Count;
            if (_workspaceMode == "Downloads")
            {
                ActiveDownloadsScrollViewer.Visibility = Visibility.Visible;
                ToggleQueueExpandIcon.Glyph = "\uE70E";
                return;
            }

            bool showList = queueCount > 0 && _isQueuePanelExpanded;
            ActiveDownloadsScrollViewer.Visibility = showList ? Visibility.Visible : Visibility.Collapsed;
            ToggleQueueExpandIcon.Glyph = showList ? "\uE70E" : "\uE70D";
            ToggleQueueExpandText.Text = queueCount > 0
                ? (showList ? $"Свернуть ({queueCount})" : $"Очередь ({queueCount})")
                : "Очередь";
        }

        private void ToggleQueueExpand_Click(object sender, RoutedEventArgs e)
        {
            if (_workspaceMode == "Downloads")
            {
                _workspaceMode = "Overview";
                _isQueuePanelExpanded = false;
                ApplyWorkspaceLayout();
                return;
            }

            if (RuTrackerDownloadQueueService.Instance.Items.Count == 0)
            {
                _workspaceMode = "Downloads";
                ApplyWorkspaceLayout();
                return;
            }

            _isQueuePanelExpanded = !_isQueuePanelExpanded;
            SyncQueueVisibilityState();
        }

        private void ForumSectionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isUiInitialized) return;
            RefreshInstantTopics(SearchOrUrlBox?.Text ?? string.Empty, autoSelectFirst: false);
        }

        private void SearchOrUrlBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isUiInitialized || _suppressInstantSearchWatcher) return;
            string text = SearchOrUrlBox.Text ?? string.Empty;
            ClearSearchBoxBtn.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;

            if (text.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _instantSearchDebounceTimer?.Stop();
            _instantSearchDebounceTimer?.Start();
        }

        private void InstantSearchDebounceTimer_Tick(object? sender, object e)
        {
            _instantSearchDebounceTimer?.Stop();
            RefreshInstantTopics(SearchOrUrlBox?.Text ?? string.Empty, autoSelectFirst: false);
        }

        private void ClearSearchBoxBtn_Click(object sender, RoutedEventArgs e)
        {
            _suppressInstantSearchWatcher = true;
            SearchOrUrlBox.Text = string.Empty;
            _suppressInstantSearchWatcher = false;
            ClearSearchBoxBtn.Visibility = Visibility.Collapsed;
            RefreshInstantTopics(string.Empty, autoSelectFirst: false);
            SearchOrUrlBox.Focus(FocusState.Programmatic);
        }

        private void QuickPresetQuery_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe) return;
            string preset = fe.Tag?.ToString() ?? string.Empty;
            _suppressInstantSearchWatcher = true;
            SearchOrUrlBox.Text = preset;
            _suppressInstantSearchWatcher = false;
            ClearSearchBoxBtn.Visibility = string.IsNullOrEmpty(preset) ? Visibility.Collapsed : Visibility.Visible;
            if (_workspaceMode != "Overview" || _isTopicsPanelCollapsed)
            {
                _workspaceMode = "Overview";
                _isTopicsPanelCollapsed = false;
                ApplyWorkspaceLayout();
            }
            RefreshInstantTopics(preset, autoSelectFirst: false);
        }

        private void TopicLocalFilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isUiInitialized) return;
            ApplyTopicFilterAndSort(autoSelectFirst: false);
        }

        private void TopicSortCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isUiInitialized) return;
            ApplyTopicFilterAndSort(autoSelectFirst: false);
        }

        private void RefreshInstantTopics(string query, bool autoSelectFirst)
        {
            try
            {
                string catFilter = ForumSectionCombo?.SelectedIndex switch
                {
                    1 => "SWITCH",
                    2 => "3DS",
                    _ => "ALL"
                };

                var instant = _service.SearchTopicsInstant(query, catFilter, 80);
                _allFetchedTopics.Clear();
                _allFetchedTopics.AddRange(instant);
                ApplyTopicFilterAndSort(autoSelectFirst);

                if (string.IsNullOrWhiteSpace(query))
                {
                    StatusText.Text = $"Популярные раздачи каталога Switch ({_topics.Count}). Начните вводить название игры для мгновенного поиска.";
                }
                else
                {
                    StatusText.Text = _topics.Count > 0
                        ? $"Мгновенный поиск «{query.Trim()}»: найдено {_topics.Count} раздач. Выберите тему слева или нажмите Enter для сетевого обновления."
                        : $"В локальном индексе «{query.Trim()}» не найдено - нажмите «Найти и открыть» для живого запроса к RuTracker.";
                }
            }
            catch { }
        }

        private void ApplyTopicFilterAndSort(bool autoSelectFirst)
        {
            if (TopicsListView == null) return;
            string? prevSelectedTid = (TopicsListView.SelectedItem as RuTrackerTopicItem)?.TopicId;

            string localFilter = TopicLocalFilterBox?.Text?.Trim() ?? string.Empty;
            IEnumerable<RuTrackerTopicItem> query = _allFetchedTopics;

            if (!string.IsNullOrEmpty(localFilter))
            {
                var tokens = localFilter.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                query = query.Where(t => tokens.All(tok =>
                    (t.Title?.Contains(tok, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (t.TopicId?.Contains(tok, StringComparison.OrdinalIgnoreCase) ?? false)));
            }

            int sortMode = TopicSortCombo?.SelectedIndex ?? 0;
            var finalItems = sortMode switch
            {
                1 => query.OrderByDescending(t => t.EffectiveSizeBytes).ThenByDescending(t => t.Seeds).ToList(),
                2 => query.OrderBy(t => t.CleanTitleDisplay, StringComparer.OrdinalIgnoreCase).ThenByDescending(t => t.Seeds).ToList(),
                _ => string.IsNullOrWhiteSpace(SearchOrUrlBox?.Text)
                    ? query.OrderByDescending(t => t.Seeds).ToList()
                    : query.ToList()
            };

            _suppressTopicSelectionInspect = !autoSelectFirst;
            try
            {
                _topics.Clear();
                foreach (var item in finalItems)
                    _topics.Add(item);

                if (TopicsCountText != null)
                    TopicsCountText.Text = $"{_topics.Count} тем";

                if (autoSelectFirst && _topics.Count > 0)
                {
                    TopicsListView.SelectedIndex = 0;
                }
                else if (!string.IsNullOrEmpty(prevSelectedTid))
                {
                    var keep = _topics.FirstOrDefault(x => x.TopicId == prevSelectedTid);
                    if (keep != null)
                        TopicsListView.SelectedItem = keep;
                }
            }
            finally
            {
                _suppressTopicSelectionInspect = false;
            }
        }

        private void SearchOrUrlBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                SearchOrInspect_Click(sender, new RoutedEventArgs());
            }
        }

        private void SettingsToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            SettingsPanel.Visibility = SettingsToggleBtn.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ApplyNetworkSettings_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentSettings();
            StatusText.Text = "Сетевые настройки (Зеркало / DoH / Прокси) успешно применены.";
        }

        private async void LoginRuTracker_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentSettings();

            if (!string.IsNullOrWhiteSpace(CookieBox.Text) && string.IsNullOrWhiteSpace(LoginPassBox.Password))
            {
                AuthStatusText.Text = "✅ Сессия bb_session установлена вручную.";
                StatusText.Text = "Сессия RuTracker активирована через Cookie.";
                return;
            }

            if (string.IsNullOrWhiteSpace(LoginUserBox.Text) || string.IsNullOrWhiteSpace(LoginPassBox.Password))
            {
                StatusText.Text = "Введите логин и пароль RuTracker или вставьте Cookie bb_session.";
                return;
            }

            LoginBtn.IsEnabled = false;
            StatusText.Text = "Выполняется вход на RuTracker...";
            var (ok, cookieOrError) = await _service.LoginAsync(LoginUserBox.Text.Trim(), LoginPassBox.Password, CancellationToken.None);
            LoginBtn.IsEnabled = true;

            if (ok)
            {
                CookieBox.Text = cookieOrError;
                AuthStatusText.Text = "✅ Вход выполнен успешно (сессия bb_session сохранена).";
                StatusText.Text = "Авторизация на RuTracker прошла успешно!";
            }
            else
            {
                AuthStatusText.Text = $"⚠ {cookieOrError}";
                StatusText.Text = cookieOrError;
            }
        }

        private async void BrowseDownloadFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FolderPicker();
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
                picker.FileTypeFilter.Add("*");
                var folder = await picker.PickSingleFolderAsync();
                if (folder != null)
                {
                    DownloadFolderBox.Text = folder.Path;
                    SaveCurrentSettings();
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Ошибка выбора папки: {ex.Message}";
            }
        }

        private async void BrowseTorrentFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
                picker.FileTypeFilter.Add(".torrent");
                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    _localTorrentPath = file.Path;
                    _currentTopicTitle = Path.GetFileNameWithoutExtension(file.Name);

                    var dummyTopic = new RuTrackerTopicItem { Title = _currentTopicTitle };
                    RebuildGameSlotsForTopic(_currentTopicTitle, null);
                    var entries = await _service.InspectTorrentFilesAsync(dummyTopic, file.Path, CancellationToken.None);
                    SyncGameSlotsWithDiscoveredFiles(entries);
                    PopulateTorrentFiles(entries);
                    var slotTitles = _gameSlots.Select(s => s.GameTitle).ToList();
                    _suppressGroupRebuild = true;
                    RuTrackerModService.AutoAssignFilesToGameSlots(_torrentFiles, slotTitles);
                    _suppressGroupRebuild = false;
                    EnrichGameSlotsFromFilesAndTitleDb();
                    UpdateGameSlotsSummary();
                    StatusText.Text = $"Загружен локальный торрент: {file.Name} ({entries.Count} файлов, игр/частей: {_gameSlots.Count}).";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Ошибка открытия .torrent: {ex.Message}";
            }
        }

        private async void SearchOrInspect_Click(object sender, RoutedEventArgs e)
        {
            SaveCurrentSettings();
            string input = SearchOrUrlBox.Text?.Trim() ?? string.Empty;
            // Если введена прямая облачная ссылка (Яндекс.Диск / Google Drive / GitHub), сразу добавляем её в поле ссылок на моды
            if (input.Contains("disk.yandex.", StringComparison.OrdinalIgnoreCase) ||
                input.Contains("yadi.sk", StringComparison.OrdinalIgnoreCase) ||
                input.Contains("drive.google.com", StringComparison.OrdinalIgnoreCase) ||
                input.Contains("github.com", StringComparison.OrdinalIgnoreCase) ||
                input.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                input.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) ||
                input.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))
            {
                AppendCloudModLinks(new[] { input });
                StatusText.Text = "Облачная ссылка добавлена в список модов. Нажмите «Скачать только облачные моды и привязать».";
                return;
            }

            // Если это Magnet-ссылка
            if (input.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
            {
                _localTorrentPath = null;
                _currentTopicTitle = "Magnet_Release";
                var magnetTopic = new RuTrackerTopicItem
                {
                    Title = "Magnet Release",
                    MagnetUri = input,
                    Category = "Magnet"
                };
                var entries = await _service.InspectTorrentFilesAsync(magnetTopic, null, CancellationToken.None);
                PopulateTorrentFiles(entries);
                StatusText.Text = "Magnet-ссылка распознана. Нажмите кнопку скачивания для старта.";
                return;
            }

            SearchBtn.IsEnabled = false;
            try
            {
                string catFilter = ForumSectionCombo.SelectedIndex switch
                {
                    1 => "SWITCH",
                    2 => "3DS",
                    _ => "ALL"
                };

                StatusText.Text = "Выполняется сетевой поиск раздач на RuTracker...";
                var instantHits = _service.SearchTopicsInstant(input, catFilter, 80);
                if (instantHits.Count > 0)
                {
                    _allFetchedTopics.Clear();
                    _allFetchedTopics.AddRange(instantHits);
                    ApplyTopicFilterAndSort(autoSelectFirst: false);
                }

                var results = await _service.SearchTopicsAsync(input, catFilter, CancellationToken.None);

                _allFetchedTopics.Clear();
                _allFetchedTopics.AddRange(results);
                ApplyTopicFilterAndSort(autoSelectFirst: true);

                if (_topics.Count > 0)
                {
                    if (_workspaceMode != "Overview" || _isTopicsPanelCollapsed)
                    {
                        _workspaceMode = "Overview";
                        _isTopicsPanelCollapsed = false;
                        ApplyWorkspaceLayout();
                    }
                    StatusText.Text = $"Найдено раздач: {_topics.Count}. Выберите раздачу слева для просмотра файлов и модов.";
                }
                else
                {
                    StatusText.Text = $"По запросу «{input}» раздач на RuTracker не найдено. Проверьте название или откройте внешний .torrent / magnet.";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Ошибка запроса: {ex.Message}";
            }
            finally
            {
                SearchBtn.IsEnabled = true;
            }
        }

        private CancellationTokenSource? _topicSelectionCts;
        private long _topicSelectionSeq;

        private async void TopicsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressTopicSelectionInspect)
                return;
            if (TopicsListView.SelectedItem is not RuTrackerTopicItem topic)
                return;

            var prevCts = System.Threading.Interlocked.Exchange(ref _topicSelectionCts, null);
            if (prevCts != null)
            {
                try { prevCts.Cancel(); prevCts.Dispose(); } catch { }
            }

            var selCts = new CancellationTokenSource();
            _topicSelectionCts = selCts;
            var ct = selCts.Token;
            long mySeq = System.Threading.Interlocked.Increment(ref _topicSelectionSeq);

            _localTorrentPath = null;
            _currentTopicTitle = topic.Title;
            _currentTopicUrl = topic.TopicUrl;
            _currentTopicPosterUrl = topic.PosterUrl ?? string.Empty;

            // МГНОВЕННАЯ ФАЗА (< 3 мс): сразу наполняем слоты, TitleID/версию/иконку из TitleDB, Magnet и кэшированные файлы!
            _service.PrimeQuickTopicState(topic);

            _discussionLinks.Clear();
            if (topic.ExtractedModLinks != null && topic.ExtractedModLinks.Count > 0)
            {
                PopulateDiscussionLinks(topic.ExtractedModLinks);
            }
            else
            {
                ModLinksHintText.Text = "Поиск внешних ссылок...";
            }

            RebuildGameSlotsForTopic(topic.Title, topic.TopicId);

            var instantCachedFiles = _service.TryGetInstantCachedTorrentFiles(topic);
            if (instantCachedFiles != null && instantCachedFiles.Count > 0)
            {
                SyncGameSlotsWithDiscoveredFiles(instantCachedFiles);
                PopulateTorrentFiles(instantCachedFiles);
            }
            else if (!string.IsNullOrWhiteSpace(topic.MagnetUri))
            {
                var quickMagnetEntry = new List<TorrentFileSelectionItem>
                {
                    new TorrentFileSelectionItem
                    {
                        IsSelected = true,
                        TargetGameSlot = 1,
                        RelativePath = $"{topic.CleanTitleDisplay} - загрузка списка файлов раздачи...",
                        Length = topic.EffectiveSizeBytes,
                        CategoryBadge = "MAGNET"
                    }
                };
                PopulateTorrentFiles(quickMagnetEntry);
                TorrentFilesHeader.Text = "Чтение списка файлов раздачи...";
            }
            else
            {
                _torrentFiles.Clear();
                TorrentFilesHeader.Text = "Чтение списка файлов раздачи...";
                RebuildFileAndLinkGroups();
            }

            var initialSlotTitles = _gameSlots.Select(s => s.GameTitle).ToList();
            _suppressGroupRebuild = true;
            if (_torrentFiles.Count > 0)
                RuTrackerModService.AutoAssignFilesToGameSlots(_torrentFiles, initialSlotTitles);
            if (_discussionLinks.Count > 0)
                RuTrackerModService.AutoAssignLinksToGameSlots(_discussionLinks, initialSlotTitles);
            _suppressGroupRebuild = false;
            EnrichGameSlotsFromFilesAndTitleDb();
            UpdateGameSlotsSummary();
            RebuildFileAndLinkGroups();

            if (instantCachedFiles != null && instantCachedFiles.Count > 0)
                StatusText.Text = $"Раздача готова мгновенно из кэша: {instantCachedFiles.Count} файлов ({_gameSlots.Count} игр/частей).";
            else
                StatusText.Text = $"Быстрая подгрузка файлов и ссылок темы [{topic.TopicId}]...";

            try
            {
                // ПАРАЛЛЕЛЬНАЯ ФАЗА (~0.3-0.6 сек): список файлов .torrent и 1-я страница темы грузятся ОДНОВРЕМЕННО!
                Task<List<TorrentFileSelectionItem>>? filesTask = (instantCachedFiles == null || instantCachedFiles.Count == 0)
                    ? _service.InspectTorrentFilesAsync(topic, null, ct)
                    : null;

                Task<RuTrackerTopicItem?> detailsTask = _service.FetchTopicDetailsAsync(topic.TopicId, ct, includeCommentPages: false);

                if (filesTask != null)
                {
                    _ = filesTask.ContinueWith(t =>
                    {
                        if (t.IsCanceled || t.IsFaulted || ct.IsCancellationRequested) return;
                        var entries = t.Result;
                        App.RunOnUI(() =>
                        {
                            if (ct.IsCancellationRequested || mySeq != System.Threading.Interlocked.Read(ref _topicSelectionSeq))
                                return;

                            SyncGameSlotsWithDiscoveredFiles(entries);
                            PopulateTorrentFiles(entries);
                            var slotTitles = _gameSlots.Select(s => s.GameTitle).ToList();
                            _suppressGroupRebuild = true;
                            RuTrackerModService.AutoAssignFilesToGameSlots(_torrentFiles, slotTitles);
                            RuTrackerModService.AutoAssignLinksToGameSlots(_discussionLinks, slotTitles);
                            _suppressGroupRebuild = false;
                            EnrichGameSlotsFromFilesAndTitleDb();
                            UpdateGameSlotsSummary();
                            RebuildFileAndLinkGroups();

                            if (entries.Count > 1 || (entries.Count == 1 && entries[0].CategoryBadge != "MAGNET"))
                            {
                                StatusText.Text = $"Раздача готова: {entries.Count} файлов в .torrent ({_gameSlots.Count} игр/частей). Отметьте нужные файлы или скачайте раздачу.";
                            }
                            else if (!string.IsNullOrEmpty(topic.MagnetUri))
                            {
                                TorrentFilesHeader.Text = $"Magnet-разрешение активно ({topic.SizeText}) - файлы будут получены через P2P";
                                StatusText.Text = "Раздача готова к высокоскоростной загрузке.";
                            }
                        });
                    }, TaskScheduler.Default);
                }

                var detailed = await detailsTask;
                if (ct.IsCancellationRequested || mySeq != System.Threading.Interlocked.Read(ref _topicSelectionSeq))
                    return;

                if (detailed != null)
                {
                    if (!string.IsNullOrEmpty(detailed.MagnetUri))
                        topic.MagnetUri = detailed.MagnetUri;
                    if (!string.IsNullOrEmpty(detailed.InfoHash))
                        topic.InfoHash = detailed.InfoHash;
                    if (!string.IsNullOrEmpty(detailed.PosterUrl))
                    {
                        topic.PosterUrl = detailed.PosterUrl;
                        _currentTopicPosterUrl = detailed.PosterUrl;
                    }
                    _service.PrimeQuickTopicState(topic);

                    if (detailed.ExtractedModLinks.Count > 0)
                    {
                        topic.ExtractedModLinks = detailed.ExtractedModLinks;
                        PopulateDiscussionLinks(detailed.ExtractedModLinks);
                        var slotTitles = _gameSlots.Select(s => s.GameTitle).ToList();
                        _suppressGroupRebuild = true;
                        RuTrackerModService.AutoAssignLinksToGameSlots(_discussionLinks, slotTitles);
                        _suppressGroupRebuild = false;
                        RebuildFileAndLinkGroups();
                        UpdateGameSlotsSummary();
                    }
                    else if (_discussionLinks.Count == 0)
                    {
                        ModLinksHintText.Text = "Внешних ссылок в шапке не найдено (проверка комментариев...)";
                    }

                    EnrichGameSlotsFromFilesAndTitleDb();
                }

                // ФОНОВАЯ ФАЗА: тихо догружаем ссылки на русификаторы/моды из комментариев без блокировки интерфейса
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var fullLinks = await _service.FetchTopicCommentLinksAsync(topic.TopicId, ct).ConfigureAwait(false);
                        if (ct.IsCancellationRequested) return;
                        App.RunOnUI(() =>
                        {
                            if (ct.IsCancellationRequested || mySeq != System.Threading.Interlocked.Read(ref _topicSelectionSeq))
                                return;

                            if (fullLinks != null && fullLinks.Count > _discussionLinks.Count)
                            {
                                var previouslySelectedUrls = new HashSet<string>(
                                    _discussionLinks.Where(x => x.IsSelected).Select(x => x.Url),
                                    StringComparer.OrdinalIgnoreCase);

                                topic.ExtractedModLinks = fullLinks;
                                PopulateDiscussionLinks(fullLinks);
                                foreach (var lnk in _discussionLinks)
                                {
                                    if (previouslySelectedUrls.Contains(lnk.Url))
                                        lnk.IsSelected = true;
                                }
                                var slotTitles = _gameSlots.Select(s => s.GameTitle).ToList();
                                _suppressGroupRebuild = true;
                                RuTrackerModService.AutoAssignLinksToGameSlots(_discussionLinks, slotTitles);
                                _suppressGroupRebuild = false;
                                RebuildFileAndLinkGroups();
                                UpdateGameSlotsSummary();
                            }
                            else if (_discussionLinks.Count == 0)
                            {
                                ModLinksHintText.Text = "Внешних ссылок в теме не найдено";
                            }
                        });
                    }
                    catch { }
                }, ct);

                if (filesTask != null)
                    await filesTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                    StatusText.Text = $"Ошибка чтения темы: {ex.Message}";
            }
        }

        private void PopulateTorrentFiles(List<TorrentFileSelectionItem> entries)
        {
            foreach (var oldItem in _torrentFiles)
                oldItem.PropertyChanged -= OnItemSelectionChangedForUnlocker;
            _torrentFiles.Clear();
            foreach (var entry in entries)
            {
                entry.PropertyChanged += OnItemSelectionChangedForUnlocker;
                _torrentFiles.Add(entry);
            }
            TorrentFilesHeader.Text = $"Файлы в раздаче ({entries.Count} шт.)";
            UpdateDlcUnlockerAutoState();
            RebuildFileAndLinkGroups();
        }

        private void PopulateDiscussionLinks(IEnumerable<CloudModLinkItem> links)
        {
            foreach (var oldLink in _discussionLinks)
                oldLink.PropertyChanged -= OnItemSelectionChangedForUnlocker;
            _discussionLinks.Clear();
            foreach (var link in links)
            {
                link.IsSelected = false;
                link.PropertyChanged += OnItemSelectionChangedForUnlocker;
                _discussionLinks.Add(link);
            }
            UpdateDiscussionSummaryText();
            UpdateDlcUnlockerAutoState();
            RebuildFileAndLinkGroups();
        }

        private string GetDefaultTargetFormat()
        {
            if (GlobalSlotFormatCombo?.SelectedItem is ComboBoxItem cbi &&
                cbi.Content is string fmt &&
                !string.IsNullOrWhiteSpace(fmt))
            {
                return fmt;
            }
            if (!string.IsNullOrWhiteSpace(_targetTask?.TargetFormat))
                return _targetTask.TargetFormat;
            if (!string.IsNullOrWhiteSpace(_viewModel?.SelectedFormat))
                return _viewModel.SelectedFormat;
            return "NSP";
        }

        private void GlobalSlotFormatCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isUiInitialized) return;
            string fmt = GetDefaultTargetFormat();
            foreach (var slot in _gameSlots)
            {
                slot.TargetFormat = fmt;
            }
        }

        private void ToggleSingleSlotDetails_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is RuTrackerGameSlotItem slot)
            {
                slot.IsDetailsExpanded = !slot.IsDetailsExpanded;
                SyncSlotCompactHeaderButton();
            }
        }

        private void ToggleAllSlotsCompact_Click(object sender, RoutedEventArgs e)
        {
            bool anyExpanded = _gameSlots.Any(s => s.IsDetailsExpanded);
            bool nextState = !anyExpanded;
            foreach (var s in _gameSlots)
                s.IsDetailsExpanded = nextState;
            SyncSlotCompactHeaderButton();
        }

        private void ApplyAdaptiveSlotExpansion()
        {
            bool expandByDefault = _gameSlots.Count <= 2;
            foreach (var s in _gameSlots)
                s.IsDetailsExpanded = expandByDefault;
            SyncSlotCompactHeaderButton();
        }

        private void SyncSlotCompactHeaderButton()
        {
            if (ToggleAllSlotsCompactIcon == null || ToggleAllSlotsCompactText == null) return;
            bool anyExpanded = _gameSlots.Any(s => s.IsDetailsExpanded);
            ToggleAllSlotsCompactIcon.Glyph = anyExpanded ? "\uE70E" : "\uE70D";
            ToggleAllSlotsCompactText.Text = anyExpanded ? "Компактно" : "Поля NACP";
        }

        private void InitializeDefaultGameSlot(string title)
        {
            _gameSlots.Clear();
            var slot = new RuTrackerGameSlotItem
            {
                SlotNumber = 1,
                IsEnabled = true,
                GameTitle = title,
                TargetFormat = GetDefaultTargetFormat()
            };
            slot.PropertyChanged += (_, _) => UpdateGameSlotsSummary();
            _gameSlots.Add(slot);
            RefreshGameSlotTargetOptions();
            UpdateGameSlotsSummary();
        }

        private void RebuildGameSlotsForTopic(string? topicTitle, string? topicId = null)
        {
            var detected = RuTrackerModService.DetectMultiGameTitlesFromRelease(topicTitle, topicId);
            if (detected.Count == 0)
            {
                detected.Add(!string.IsNullOrWhiteSpace(topicTitle) ? topicTitle : "Игра 1");
            }

            string defaultFmt = GetDefaultTargetFormat();
            _gameSlots.Clear();
            for (int i = 0; i < detected.Count; i++)
            {
                var slot = new RuTrackerGameSlotItem
                {
                    SlotNumber = i + 1,
                    IsEnabled = true,
                    GameTitle = detected[i],
                    TargetFormat = defaultFmt
                };
                slot.PropertyChanged += (_, _) => UpdateGameSlotsSummary();
                _gameSlots.Add(slot);
            }
            ApplyAdaptiveSlotExpansion();
            RefreshGameSlotTargetOptions();
            UpdateGameSlotsSummary();
        }

        private void CenteredInput_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not DependencyObject root) return;
            ApplyVerticalCenterToInputVisualTree(root);
            if (sender is FrameworkElement fe)
            {
                void OnFirstLayout(object? s, object? args)
                {
                    fe.LayoutUpdated -= OnFirstLayout;
                    ApplyVerticalCenterToInputVisualTree(fe);
                }
                fe.LayoutUpdated += OnFirstLayout;
            }
        }

        private static void ApplyVerticalCenterToInputVisualTree(DependencyObject parent)
        {
            int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement fe)
                {
                    if (fe.Name == "ContentElement" || fe.Name == "PlaceholderTextContentPresenter")
                    {
                        fe.VerticalAlignment = VerticalAlignment.Center;
                        fe.Margin = new Thickness(0);
                        if (fe is Control ctrl)
                            ctrl.Padding = new Thickness(ctrl.Padding.Left, 0, ctrl.Padding.Right, 0);
                        if (fe is ContentPresenter cp)
                            cp.Padding = new Thickness(cp.Padding.Left, 0, cp.Padding.Right, 0);
                    }
                }
                ApplyVerticalCenterToInputVisualTree(child);
            }
        }

        private void SyncGameSlotsWithDiscoveredFiles(IList<TorrentFileSelectionItem> entries)
        {
            if (entries == null || !entries.Any(e => e.IsRealTorrentFile))
                return;

            var discoveredFromFiles = RuTrackerModService.DiscoverGameTitlesFromFiles(entries);
            if (discoveredFromFiles.Count == 0)
                return;

            string defaultFmt = GetDefaultTargetFormat();
            var prevFormats = _gameSlots.ToDictionary(s => s.SlotNumber, s => s.TargetFormat);

            _gameSlots.Clear();
            for (int i = 0; i < discoveredFromFiles.Count; i++)
            {
                int slotNum = i + 1;
                string slotFmt = prevFormats.TryGetValue(slotNum, out var existingFmt) && !string.IsNullOrWhiteSpace(existingFmt)
                    ? existingFmt
                    : defaultFmt;
                var slot = new RuTrackerGameSlotItem
                {
                    SlotNumber = slotNum,
                    IsEnabled = true,
                    GameTitle = discoveredFromFiles[i],
                    TargetFormat = slotFmt
                };
                slot.PropertyChanged += (_, _) => UpdateGameSlotsSummary();
                _gameSlots.Add(slot);
            }

            // Если после синхронизации слотов стало меньше, все файлы и ссылки с превышающим TargetGameSlot переназначаются в слот 1
            _suppressGroupRebuild = true;
            foreach (var f in _torrentFiles.Where(x => x.TargetGameSlot > _gameSlots.Count))
                f.TargetGameSlot = 1;
            foreach (var l in _discussionLinks.Where(x => x.TargetGameSlot > _gameSlots.Count))
                l.TargetGameSlot = 1;
            _suppressGroupRebuild = false;

            ApplyAdaptiveSlotExpansion();
            RefreshGameSlotTargetOptions();
            UpdateGameSlotsSummary();
        }

        private void RefreshGameSlotTargetOptions()
        {
            _suppressSlotMergeSelection = true;
            try
            {
                var slotNums = _gameSlots
                    .Select(s => s.SlotNumber)
                    .Where(n => n > 0)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();
                if (slotNums.Count == 0)
                    slotNums.Add(1);

                var allOptions = slotNums
                    .Select(n => new GameSlotTargetOption { SlotNumber = n })
                    .ToList();

                foreach (var slot in _gameSlots)
                {
                    slot.AvailableSlotTargets.Clear();
                    foreach (var opt in allOptions)
                    {
                        slot.AvailableSlotTargets.Add(opt);
                    }
                    slot.SelectedSlotTarget = slot.AvailableSlotTargets.FirstOrDefault(o => o.SlotNumber == slot.SlotNumber);
                    slot.RefreshSlotTargetsState();
                }
            }
            finally
            {
                _suppressSlotMergeSelection = false;
            }
        }

        private void GameSlotNumberCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSlotMergeSelection) return;
            if (sender is not ComboBox cb || cb.Tag is not RuTrackerGameSlotItem sourceSlot) return;
            if (cb.SelectedItem is not GameSlotTargetOption selectedOption) return;

            int fromSlot = sourceSlot.SlotNumber;
            int toSlot = selectedOption.SlotNumber;

            if (fromSlot == toSlot || fromSlot <= 0 || toSlot <= 0) return;

            MergeGameSlotIntoTarget(fromSlot, toSlot);
        }

        private void MergeGameSlotIntoTarget(int fromSlot, int toSlot)
        {
            var sourceSlot = _gameSlots.FirstOrDefault(s => s.SlotNumber == fromSlot);
            var targetSlot = _gameSlots.FirstOrDefault(s => s.SlotNumber == toSlot);
            if (sourceSlot == null || targetSlot == null || fromSlot == toSlot) return;

            string sourceTitle = sourceSlot.GameTitle;
            string targetTitle = targetSlot.GameTitle;

            _suppressGroupRebuild = true;
            try
            {
                // 1. Все файлы в _torrentFiles с TargetGameSlot == fromSlot получают TargetGameSlot = toSlot и IsSelected = true
                var matchingFiles = _torrentFiles.Where(f => f.TargetGameSlot == fromSlot).ToList();
                if (matchingFiles.Count > 0)
                {
                    foreach (var f in matchingFiles)
                    {
                        f.TargetGameSlot = toSlot;
                        f.IsSelected = true;
                        if (f.CategoryBadge == $"ИГРА #{fromSlot}" || f.CategoryBadge == "ИГРА")
                        {
                            f.CategoryBadge = "DLC";
                        }
                    }
                }
                else
                {
                    // Если у sourceSlot вообще не было отдельной строки в _torrentFiles только потому,
                    // что раздача в чистом Magnet-режиме из 1 строки, создаём/обновляем запись для sourceSlot.GameTitle в toSlot
                    string slotTitle = !string.IsNullOrWhiteSpace(sourceSlot.GameTitle) ? sourceSlot.GameTitle : $"Игра #{fromSlot}";
                    var existingSynth = _torrentFiles.FirstOrDefault(f => f.RelativePath == slotTitle);
                    if (existingSynth != null)
                    {
                        existingSynth.TargetGameSlot = toSlot;
                        existingSynth.IsSelected = true;
                        existingSynth.CategoryBadge = "DLC";
                    }
                    else
                    {
                        var newSynth = new TorrentFileSelectionItem
                        {
                            IsSelected = true,
                            TargetGameSlot = toSlot,
                            RelativePath = slotTitle,
                            DisplayPath = slotTitle,
                            CategoryBadge = "DLC",
                            Length = 0,
                            IsRealTorrentFile = false
                        };
                        newSynth.PropertyChanged += OnItemSelectionChangedForUnlocker;
                        _torrentFiles.Add(newSynth);
                    }
                }

                // 2. Все ссылки в _discussionLinks с TargetGameSlot == fromSlot переносятся в toSlot
                foreach (var l in _discussionLinks.Where(x => x.TargetGameSlot == fromSlot))
                {
                    l.TargetGameSlot = toSlot;
                    l.IsSelected = true;
                }

                // Объединяем ручные дополнительные ссылки
                if (!string.IsNullOrWhiteSpace(sourceSlot.ManualExtraLinks))
                {
                    string extra = sourceSlot.ManualExtraLinks.Trim();
                    if (string.IsNullOrWhiteSpace(targetSlot.ManualExtraLinks))
                    {
                        targetSlot.ManualExtraLinks = extra;
                    }
                    else if (!targetSlot.ManualExtraLinks.Contains(extra, StringComparison.OrdinalIgnoreCase))
                    {
                        targetSlot.ManualExtraLinks = (targetSlot.ManualExtraLinks.Trim() + "\n" + extra).Trim();
                    }
                }

                // 3. Слот fromSlot удаляется из _gameSlots
                _gameSlots.Remove(sourceSlot);

                // 4. Все слоты/файлы/ссылки с номером > fromSlot сдвигаются на -1 без дыр в нумерации
                foreach (var s in _gameSlots.Where(x => x.SlotNumber > fromSlot))
                {
                    s.SlotNumber -= 1;
                }
                foreach (var f in _torrentFiles.Where(x => x.TargetGameSlot > fromSlot))
                {
                    f.TargetGameSlot -= 1;
                }
                foreach (var l in _discussionLinks.Where(x => x.TargetGameSlot > fromSlot))
                {
                    l.TargetGameSlot -= 1;
                }
            }
            finally
            {
                _suppressGroupRebuild = false;
            }

            RuTrackerModService.SortTorrentFilesBySlotAndRole(_torrentFiles);
            RefreshGameSlotTargetOptions();
            EnrichGameSlotsFromFilesAndTitleDb();
            UpdateGameSlotsSummary();

            LiveTelemetryText.Text = $"🔗 Слот #{fromSlot} ({sourceTitle}) объединён со слотом #{toSlot} ({targetTitle}). Все файлы перенесены в слот #{toSlot}.";
        }

        private void AutoSplitGames_Click(object sender, RoutedEventArgs e)
        {
            RebuildGameSlotsForTopic(_currentTopicTitle, _service.ActiveTopicId);
            SyncGameSlotsWithDiscoveredFiles(_torrentFiles);
            var slotTitles = _gameSlots.Select(s => s.GameTitle).ToList();
            _suppressGroupRebuild = true;
            RuTrackerModService.AutoAssignFilesToGameSlots(_torrentFiles, slotTitles);
            RuTrackerModService.AutoAssignLinksToGameSlots(_discussionLinks, slotTitles);
            _suppressGroupRebuild = false;
            EnrichGameSlotsFromFilesAndTitleDb();
            UpdateGameSlotsSummary();
            LiveTelemetryText.Text = $"🪄 Авто-разбивка выполнена: распознано частей/игр - {_gameSlots.Count}. Файлы сгруппированы по блокам игр.";
        }

        private bool _pendingManualSlotSync;

        private void AddGameSlot_Click(object sender, RoutedEventArgs e)
        {
            if (_gameSlots.Count >= 10)
            {
                LiveTelemetryText.Text = "⚠️ Достигнут максимум 10 отдельных слотов в списке быстрой привязки.";
                return;
            }
            int nextNum = Enumerable.Range(1, 10).FirstOrDefault(n => _gameSlots.All(s => s.SlotNumber != n));
            if (nextNum <= 0) nextNum = _gameSlots.Count + 1;

            string inferred = InferGameTitleForSlot(nextNum);
            string baseName = _gameSlots.Count > 0
                ? System.Text.RegularExpressions.Regex.Replace(_gameSlots[0].GameTitle, @"\s+\d+$", "").Trim()
                : (!string.IsNullOrWhiteSpace(_currentTopicTitle) ? _currentTopicTitle : "Игра");
            string slotTitle = !string.IsNullOrWhiteSpace(inferred) && !inferred.StartsWith("Игра #", StringComparison.OrdinalIgnoreCase)
                ? inferred
                : $"{baseName} {nextNum}";

            var slot = new RuTrackerGameSlotItem
            {
                SlotNumber = nextNum,
                IsEnabled = true,
                IsManuallyAdded = true,
                GameTitle = slotTitle,
                TargetFormat = GetDefaultTargetFormat()
            };
            slot.PropertyChanged += (_, _) => UpdateGameSlotsSummary();
            InsertSlotSorted(slot);
            RefreshGameSlotTargetOptions();
            EnrichGameSlotsFromFilesAndTitleDb();
            UpdateGameSlotsSummary();
            try { GameSlotsListView?.ScrollIntoView(slot); } catch { }
        }

        private void InsertSlotSorted(RuTrackerGameSlotItem slot)
        {
            int insertIdx = 0;
            while (insertIdx < _gameSlots.Count && _gameSlots[insertIdx].SlotNumber < slot.SlotNumber)
                insertIdx++;
            _gameSlots.Insert(insertIdx, slot);
        }

        private void RemoveGameSlot_Click(object sender, RoutedEventArgs e)
        {
            if (_gameSlots.Count <= 1) return;
            int removedSlot = _gameSlots[^1].SlotNumber;
            _gameSlots.RemoveAt(_gameSlots.Count - 1);
            int fallbackSlot = _gameSlots.Count > 0 ? _gameSlots[0].SlotNumber : 1;
            _suppressGroupRebuild = true;
            foreach (var f in _torrentFiles.Where(x => x.TargetGameSlot == removedSlot))
                f.TargetGameSlot = fallbackSlot;
            foreach (var l in _discussionLinks.Where(x => x.TargetGameSlot == removedSlot))
                l.TargetGameSlot = fallbackSlot;
            _suppressGroupRebuild = false;
            RefreshGameSlotTargetOptions();
            EnrichGameSlotsFromFilesAndTitleDb();
            UpdateGameSlotsSummary();
        }

        private void SlotAssignmentCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressGroupRebuild) return;
            // Игнорируем первичную инициализацию шаблона WinUI ComboBox при рендеринге списка
            if (e.RemovedItems.Count == 0 || e.AddedItems.Count == 0) return;

            if (sender is ComboBox cb && cb.SelectedIndex >= 0)
            {
                int targetSlot = cb.SelectedIndex;
                if (cb.DataContext is TorrentFileSelectionItem tf && tf.TargetGameSlot != targetSlot)
                {
                    tf.TargetGameSlot = targetSlot;
                }
                else if (cb.DataContext is CloudModLinkItem cl && cl.TargetGameSlot != targetSlot)
                {
                    cl.TargetGameSlot = targetSlot;
                }
            }

            if (_pendingManualSlotSync) return;
            _pendingManualSlotSync = true;

            if (DispatcherQueue != null)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    _pendingManualSlotSync = false;
                    SyncGameSlotsFromManualAssignments();
                });
            }
            else
            {
                _pendingManualSlotSync = false;
                SyncGameSlotsFromManualAssignments();
            }
        }

        private string InferGameTitleForSlot(int slotNumber)
        {
            var slotFiles = _torrentFiles.Where(f => f.TargetGameSlot == slotNumber).ToList();
            if (slotFiles.Count > 0)
            {
                // 1. Пробуем найти точное имя по Base TitleID в базе TitleDB
                foreach (var f in slotFiles)
                {
                    string rawTid = !string.IsNullOrWhiteSpace(f.DetectedTitleId)
                        ? f.DetectedTitleId
                        : RuTrackerModService.ExtractTitleId(f.RelativePath ?? string.Empty);
                    string baseTid = RuTrackerModService.NormalizeSwitchBaseApplicationTitleId(rawTid);
                    if (baseTid.Length == 16 && App.TitleDb != null && App.TitleDb.TryGetTitleInfo(baseTid, out var dbEntry) &&
                        dbEntry != null && !string.IsNullOrWhiteSpace(dbEntry.Name))
                    {
                        return dbEntry.Name.Trim();
                    }
                }

                // 2. Пробуем извлечь чистое название из файла базовой игры или любого файла слота
                var preferred = slotFiles
                    .OrderBy(f => RuTrackerModService.ClassifyTorrentEntryBadge(f.RelativePath ?? string.Empty) == "ИГРА" ? 0 : 1)
                    .ThenBy(f => f.Length > 0 ? -f.Length : 0)
                    .ToList();

                foreach (var f in preferred)
                {
                    string clean = RuTrackerModService.ExtractCleanGameTitleFromFilePath(f.RelativePath ?? string.Empty);
                    if (!string.IsNullOrWhiteSpace(clean))
                        return clean;
                }

                // 3. Fallback: имя родительской папки без ведущего номера
                foreach (var f in preferred)
                {
                    string topKey = RuTrackerModService.ExtractGameTopFolderKey(f.RelativePath);
                    if (!string.IsNullOrWhiteSpace(topKey))
                    {
                        string stripped = System.Text.RegularExpressions.Regex.Replace(topKey, @"^\d{1,2}[.)\s_-]+", "").Trim();
                        if (!string.IsNullOrWhiteSpace(stripped))
                            return stripped;
                    }
                }
            }

            var slotLinks = _discussionLinks.Where(l => l.TargetGameSlot == slotNumber).ToList();
            foreach (var l in slotLinks)
            {
                string clean = RuTrackerModService.ExtractCleanGameTitleFromFilePath(l.Title ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(clean))
                    return clean;
            }

            return $"Игра #{slotNumber}";
        }

        private static bool IsGenericPlaceholderSlotTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title)) return true;
            return System.Text.RegularExpressions.Regex.IsMatch(title.Trim(), @"^(?:Игра|Game)\s*#?\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private void SyncGameSlotsFromManualAssignments()
        {
            if (_suppressGroupRebuild) return;

            var newlyCreatedSlots = new List<RuTrackerGameSlotItem>();

            _suppressGroupRebuild = true;
            try
            {
                // 1. Восстанавливаем бейдж "ИГРА" у базовых образов ([v0]), которые ранее при слиянии слотов получили бейдж "DLC",
                //    если пользователь выделил их обратно в отдельный игровой слот.
                foreach (var grp in _torrentFiles.Where(f => f.TargetGameSlot > 0).GroupBy(f => f.TargetGameSlot))
                {
                    var existingBaseTids = grp
                        .Where(f => f.CategoryBadge == "ИГРА")
                        .Select(f => RuTrackerModService.NormalizeSwitchBaseApplicationTitleId(
                            !string.IsNullOrWhiteSpace(f.DetectedTitleId) ? f.DetectedTitleId : RuTrackerModService.ExtractTitleId(f.RelativePath ?? string.Empty)))
                        .Where(t => t.Length == 16)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    foreach (var f in grp)
                    {
                        if (f.CategoryBadge == "DLC" &&
                            f.IsRealTorrentFile &&
                            RuTrackerModService.ClassifyTorrentEntryBadge(f.RelativePath ?? string.Empty) == "ИГРА")
                        {
                            string fBaseTid = RuTrackerModService.NormalizeSwitchBaseApplicationTitleId(
                                !string.IsNullOrWhiteSpace(f.DetectedTitleId) ? f.DetectedTitleId : RuTrackerModService.ExtractTitleId(f.RelativePath ?? string.Empty));
                            if (existingBaseTids.Count == 0 || (fBaseTid.Length == 16 && existingBaseTids.Contains(fBaseTid)))
                            {
                                f.CategoryBadge = "ИГРА";
                                if (fBaseTid.Length == 16)
                                    existingBaseTids.Add(fBaseTid);
                            }
                        }
                    }
                }

                // 2. Находим все номера слотов (> 0), которые назначены хотя бы одному файлу или ссылке
                var referencedSlotNums = _torrentFiles.Select(f => f.TargetGameSlot)
                    .Concat(_discussionLinks.Select(l => l.TargetGameSlot))
                    .Where(n => n > 0)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();

                string defaultFmt = GetDefaultTargetFormat();

                foreach (int slotNum in referencedSlotNums)
                {
                    if (_gameSlots.All(s => s.SlotNumber != slotNum))
                    {
                        var newSlot = new RuTrackerGameSlotItem
                        {
                            SlotNumber = slotNum,
                            IsEnabled = true,
                            GameTitle = InferGameTitleForSlot(slotNum),
                            TargetFormat = defaultFmt
                        };
                        newSlot.PropertyChanged += (_, _) => UpdateGameSlotsSummary();
                        InsertSlotSorted(newSlot);
                        newlyCreatedSlots.Add(newSlot);
                    }
                }

                // 3. Актуализируем названия существующих слотов, если у них стояла заглушка "Игра #N"
                //    или если файлы прежней игры были полностью перенесены в другой слот
                foreach (var slot in _gameSlots)
                {
                    if (slot.IsUserEditedMetadata) continue;
                    var slotFiles = _torrentFiles.Where(f => f.TargetGameSlot == slot.SlotNumber).ToList();
                    if (slotFiles.Count == 0) continue;

                    var currentBaseTids = slotFiles
                        .Select(f => RuTrackerModService.NormalizeSwitchBaseApplicationTitleId(
                            !string.IsNullOrWhiteSpace(f.DetectedTitleId) ? f.DetectedTitleId : RuTrackerModService.ExtractTitleId(f.RelativePath ?? string.Empty)))
                        .Where(t => t.Length == 16)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    bool oldTidGone = slot.TitleId.Length == 16 &&
                                      slot.TitleId != "0100000000000000" &&
                                      currentBaseTids.Count > 0 &&
                                      !currentBaseTids.Contains(RuTrackerModService.NormalizeSwitchBaseApplicationTitleId(slot.TitleId));

                    if (IsGenericPlaceholderSlotTitle(slot.GameTitle) || oldTidGone)
                    {
                        string inferred = InferGameTitleForSlot(slot.SlotNumber);
                        if (!string.IsNullOrWhiteSpace(inferred) && !IsGenericPlaceholderSlotTitle(inferred))
                        {
                            slot.GameTitle = inferred;
                        }
                    }
                }

                // 4. Удаляем опустевшие авто-слоты (без файлов, ссылок и ручных ссылок), оставляя минимум 1 слот
                if (_gameSlots.Count > 1)
                {
                    var emptyAutoSlots = _gameSlots
                        .Where(s => !s.IsManuallyAdded &&
                                    string.IsNullOrWhiteSpace(s.ManualExtraLinks) &&
                                    _torrentFiles.All(f => f.TargetGameSlot != s.SlotNumber) &&
                                    _discussionLinks.All(l => l.TargetGameSlot != s.SlotNumber))
                        .ToList();

                    foreach (var emptySlot in emptyAutoSlots)
                    {
                        if (_gameSlots.Count <= 1) break;
                        _gameSlots.Remove(emptySlot);
                    }
                }

                RuTrackerModService.SortTorrentFilesBySlotAndRole(_torrentFiles);
            }
            finally
            {
                _suppressGroupRebuild = false;
            }

            RefreshGameSlotTargetOptions();
            EnrichGameSlotsFromFilesAndTitleDb();
            UpdateGameSlotsSummary();

            if (newlyCreatedSlots.Count > 0)
            {
                try { GameSlotsListView?.ScrollIntoView(newlyCreatedSlots[^1]); } catch { }
                string addedDesc = string.Join(", ", newlyCreatedSlots.Select(s => $"Игра #{s.SlotNumber} ({s.GameTitle})"));
                LiveTelemetryText.Text = $"➕ Автоматически добавлена задача в список игр: {addedDesc}. Всего игр/задач: {_gameSlots.Count}.";
            }
        }

        private void UpdateGameSlotsSummary()
        {
            if (_suppressGroupRebuild) return;

            if (GameSlotsCountText != null)
            {
                int enabledCount = _gameSlots.Count(s => s.IsEnabled);
                GameSlotsCountText.Text = $"{enabledCount} из {_gameSlots.Count} задач";
            }

            foreach (var slot in _gameSlots)
            {
                int filesForSlot = _torrentFiles.Count(f => f.IsSelected && (f.TargetGameSlot == 0 || f.TargetGameSlot == slot.SlotNumber));
                int linksForSlot = _discussionLinks.Count(l => l.IsSelected && (l.TargetGameSlot == 0 || l.TargetGameSlot == slot.SlotNumber));
                int manualLinksCount = (slot.ManualExtraLinks ?? string.Empty)
                    .Split(new[] { ' ', ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Count(s => s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
                slot.SummaryText = $"Файлы: {filesForSlot} • Ссылки: {linksForSlot + manualLinksCount}";
            }

            RebuildFileAndLinkGroups();
        }

        private RuTrackerGameSlotItem? _activeStudioSlot;
        private GameMetadataEditModel? _activeStudioModel;
        private bool _isInitializingStudioOverlay;

        private void EnrichGameSlotsFromFilesAndTitleDb()
        {
            foreach (var slot in _gameSlots)
            {
                var selectedSlotFiles = _torrentFiles
                    .Where(f => f.IsSelected && (f.TargetGameSlot == slot.SlotNumber || (_gameSlots.Count == 1 && f.TargetGameSlot <= 1)))
                    .ToList();

                var slotFiles = selectedSlotFiles.Count > 0
                    ? selectedSlotFiles
                    : _torrentFiles.Where(f => f.TargetGameSlot == slot.SlotNumber || (_gameSlots.Count == 1 && f.TargetGameSlot <= 1)).ToList();

                string bestTid = string.Empty;
                int maxPatchVer = 0;
                string cleanTitleFromFile = string.Empty;
                bool is3ds = false;

                foreach (var f in slotFiles)
                {
                    string rel = f.RelativePath ?? string.Empty;
                    if (rel.EndsWith(".3ds", StringComparison.OrdinalIgnoreCase) ||
                        rel.EndsWith(".cia", StringComparison.OrdinalIgnoreCase) ||
                        rel.EndsWith(".cci", StringComparison.OrdinalIgnoreCase) ||
                        rel.EndsWith(".cxi", StringComparison.OrdinalIgnoreCase))
                    {
                        is3ds = true;
                    }

                    string tid = !string.IsNullOrWhiteSpace(f.DetectedTitleId)
                        ? f.DetectedTitleId
                        : RuTrackerModService.ExtractTitleId(rel);
                    if (tid.Length == 16)
                    {
                        string baseTid = RuTrackerModService.NormalizeSwitchBaseApplicationTitleId(tid);
                        if (baseTid.Length == 16 && (string.IsNullOrEmpty(bestTid) || tid.EndsWith("000", StringComparison.OrdinalIgnoreCase)))
                            bestTid = baseTid;
                    }

                    var vMatch = System.Text.RegularExpressions.Regex.Match(rel, @"\[v(?<ver>\d+)\]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (vMatch.Success && int.TryParse(vMatch.Groups["ver"].Value, out int vNum) && vNum > maxPatchVer)
                    {
                        maxPatchVer = vNum;
                    }

                    if (string.IsNullOrEmpty(cleanTitleFromFile) && f.CategoryBadge == "ИГРА")
                    {
                        cleanTitleFromFile = RuTrackerModService.ExtractCleanGameTitleFromFilePath(rel);
                    }
                }

                if (string.IsNullOrEmpty(cleanTitleFromFile))
                {
                    foreach (var f in slotFiles)
                    {
                        string fallbackFromAnyFile = RuTrackerModService.ExtractCleanGameTitleFromFilePath(f.RelativePath ?? string.Empty);
                        if (!string.IsNullOrWhiteSpace(fallbackFromAnyFile))
                        {
                            cleanTitleFromFile = fallbackFromAnyFile;
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(bestTid))
                {
                    string fromSlot = RuTrackerModService.ExtractTitleId(slot.GameTitle);
                    if (fromSlot.Length == 16)
                        bestTid = RuTrackerModService.NormalizeSwitchBaseApplicationTitleId(fromSlot);
                }

                if (string.IsNullOrEmpty(bestTid) && _gameSlots.Count == 1 && !string.IsNullOrWhiteSpace(_currentTopicTitle))
                {
                    string fromTopic = RuTrackerModService.ExtractTitleId(_currentTopicTitle);
                    if (fromTopic.Length == 16)
                        bestTid = RuTrackerModService.NormalizeSwitchBaseApplicationTitleId(fromTopic);
                }

                if (string.IsNullOrEmpty(cleanTitleFromFile))
                {
                    cleanTitleFromFile = RuTrackerModService.ExtractCleanTopicTitle(slot.GameTitle);
                    if (string.IsNullOrWhiteSpace(cleanTitleFromFile) && _gameSlots.Count == 1)
                        cleanTitleFromFile = RuTrackerModService.ExtractCleanTopicTitle(_currentTopicTitle);
                    if (string.IsNullOrWhiteSpace(cleanTitleFromFile))
                        cleanTitleFromFile = slot.GameTitle.Trim();
                }

                TitleDbEntry? dbEntry = null;
                if (!string.IsNullOrEmpty(bestTid) && App.TitleDb != null && App.TitleDb.TryGetTitleInfo(bestTid, out var tidEntry) && tidEntry != null)
                {
                    dbEntry = tidEntry;
                }
                else if (App.TitleDb != null && App.TitleDb.TryFindTitleByName(cleanTitleFromFile, out var nameEntry) && nameEntry != null)
                {
                    dbEntry = nameEntry;
                    if (string.IsNullOrEmpty(bestTid) && !string.IsNullOrWhiteSpace(nameEntry.Id))
                        bestTid = RuTrackerModService.NormalizeSwitchBaseApplicationTitleId(nameEntry.Id.Trim().ToUpperInvariant());
                }

                // Извлекаем семантическую версию (строго без 'v', например 1.15.0) и внутреннюю числовую версию (например 983040)
                string? explicitSemanticVer = null;
                var candidateVerSources = new List<string>();
                foreach (var sf in slotFiles)
                {
                    if (!string.IsNullOrWhiteSpace(sf.RelativePath))
                        candidateVerSources.Add(sf.RelativePath);
                }
                if (!string.IsNullOrWhiteSpace(slot.GameTitle))
                    candidateVerSources.Add(slot.GameTitle);
                if (_gameSlots.Count == 1 && !string.IsNullOrWhiteSpace(_currentTopicTitle))
                    candidateVerSources.Add(_currentTopicTitle);

                foreach (var srcText in candidateVerSources)
                {
                    var comboMatch = System.Text.RegularExpressions.Regex.Match(srcText, @"\((?<dv>\d+\.\d+(?:\.\d+)*)\s*-\s*(?<iv>\d+)\)");
                    if (comboMatch.Success)
                    {
                        if (string.IsNullOrEmpty(explicitSemanticVer))
                            explicitSemanticVer = comboMatch.Groups["dv"].Value;
                        if (int.TryParse(comboMatch.Groups["iv"].Value, out int ivCombo) && ivCombo > maxPatchVer)
                            maxPatchVer = ivCombo;
                    }

                    foreach (System.Text.RegularExpressions.Match vm in System.Text.RegularExpressions.Regex.Matches(
                        srcText,
                        @"(?:\[|\(|\b(?:[vV]|Update|UPD|Patch)\s*)(?<sv>\d+\.\d+(?:\.\d+){0,2})(?=\]|\)|\b)",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        string cand = vm.Groups["sv"].Value.Trim();
                        // Отсекаем даты вида 12.05.2024 и размеры
                        var parts = cand.Split('.');
                        if (parts.Length == 3 && parts[2].Length == 4) continue;
                        int afterIdx = vm.Index + vm.Length;
                        string tail = afterIdx < srcText.Length ? srcText.Substring(afterIdx, Math.Min(8, srcText.Length - afterIdx)) : string.Empty;
                        if (System.Text.RegularExpressions.Regex.IsMatch(tail, @"^\s*(?:GB|MB|KB|TB|ГБ|МБ|КБ|ТБ)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                            continue;
                        explicitSemanticVer = cand;
                    }

                    foreach (System.Text.RegularExpressions.Match ivm in System.Text.RegularExpressions.Regex.Matches(
                        srcText,
                        @"\[v?(?<iv>\d{5,10})\]",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    {
                        if (int.TryParse(ivm.Groups["iv"].Value, out int ivVal) && ivVal > maxPatchVer)
                            maxPatchVer = ivVal;
                    }
                }

                if (dbEntry?.VersionsDictionary != null && dbEntry.VersionsDictionary.Count > 0)
                {
                    if (maxPatchVer > 0 && string.IsNullOrEmpty(explicitSemanticVer) &&
                        dbEntry.VersionsDictionary.TryGetValue(maxPatchVer.ToString(), out var mappedSem) &&
                        !string.IsNullOrWhiteSpace(mappedSem))
                    {
                        explicitSemanticVer = mappedSem.Trim().TrimStart('v', 'V');
                    }
                    else if (maxPatchVer == 0 && !string.IsNullOrEmpty(explicitSemanticVer))
                    {
                        var rev = dbEntry.VersionsDictionary.FirstOrDefault(kv =>
                            string.Equals(kv.Value?.Trim().TrimStart('v', 'V'), explicitSemanticVer, StringComparison.OrdinalIgnoreCase));
                        if (!string.IsNullOrEmpty(rev.Key) && int.TryParse(rev.Key, out int mappedIv) && mappedIv > 0)
                            maxPatchVer = mappedIv;
                    }
                }

                string displayVer;
                if (!string.IsNullOrWhiteSpace(explicitSemanticVer))
                {
                    displayVer = explicitSemanticVer.Trim().TrimStart('v', 'V');
                }
                else if (maxPatchVer >= 65536)
                {
                    int major = maxPatchVer / 65536;
                    displayVer = $"1.{major}.0";
                }
                else
                {
                    displayVer = "1.0.0";
                }

                string internalVerStr = maxPatchVer > 0 ? maxPatchVer.ToString() : "0";

                string engTitle = cleanTitleFromFile;
                string rusTitle = cleanTitleFromFile;
                string publisher = "Nintendo";
                string? iconUrl = null;

                if (dbEntry != null)
                {
                    if (!string.IsNullOrWhiteSpace(dbEntry.Name))
                    {
                        engTitle = dbEntry.Name.Trim();
                        rusTitle = dbEntry.Name.Trim();
                    }
                    if (!string.IsNullOrWhiteSpace(dbEntry.Publisher) && !string.IsNullOrWhiteSpace(dbEntry.Developer) &&
                        !string.Equals(dbEntry.Publisher.Trim(), dbEntry.Developer.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        publisher = $"{dbEntry.Publisher.Trim()} / {dbEntry.Developer.Trim()}";
                    }
                    else if (!string.IsNullOrWhiteSpace(dbEntry.Publisher))
                    {
                        publisher = dbEntry.Publisher.Trim();
                    }
                    else if (!string.IsNullOrWhiteSpace(dbEntry.Developer))
                    {
                        publisher = dbEntry.Developer.Trim();
                    }
                    iconUrl = dbEntry.IconUrl;
                }

                if (IsGenericPlaceholderSlotTitle(slot.GameTitle) && !string.IsNullOrWhiteSpace(engTitle) && !IsGenericPlaceholderSlotTitle(engTitle))
                {
                    slot.GameTitle = engTitle;
                }

                if (string.IsNullOrWhiteSpace(iconUrl) && _gameSlots.Count == 1 && !string.IsNullOrWhiteSpace(_currentTopicPosterUrl))
                    iconUrl = _currentTopicPosterUrl;

                byte[]? cachedIcon = TryLoadCachedIconBytes(bestTid, cleanTitleFromFile);
                slot.Is3ds = is3ds;
                slot.SetEnrichedMetadataSilently(bestTid, displayVer, internalVerStr, engTitle, rusTitle, publisher, cachedIcon);

                if ((cachedIcon == null || cachedIcon.Length == 0) && !string.IsNullOrWhiteSpace(iconUrl))
                {
                    _ = FetchAndCacheSlotIconFromUrlAsync(slot, bestTid, iconUrl);
                }
            }
        }

        private static byte[]? TryLoadCachedIconBytes(string? titleId, string? cleanGameName)
        {
            try
            {
                string iconsDir = HistoryService.GetIconsDirectory();
                if (!Directory.Exists(iconsDir)) return null;

                var candidates = new List<string>();
                if (!string.IsNullOrWhiteSpace(titleId))
                {
                    candidates.Add(Path.Combine(iconsDir, $"{titleId}.png"));
                    if (titleId.Length >= 13)
                    {
                        candidates.Add(Path.Combine(iconsDir, $"{titleId.Substring(0, 13)}000.png"));
                        candidates.Add(Path.Combine(iconsDir, $"{TasksViewModel.GetTitleGroupPrefix(titleId)}.png"));
                    }
                }
                if (!string.IsNullOrWhiteSpace(cleanGameName))
                {
                    string safe = string.Join("_", cleanGameName.Split(Path.GetInvalidFileNameChars()));
                    candidates.Add(Path.Combine(iconsDir, $"{safe}.png"));
                }

                foreach (var c in candidates)
                {
                    if (File.Exists(c))
                    {
                        byte[] bytes = File.ReadAllBytes(c);
                        if (bytes.Length > 0) return bytes;
                    }
                }
            }
            catch { }
            return null;
        }

        private static async Task FetchAndCacheSlotIconFromUrlAsync(RuTrackerGameSlotItem slot, string titleId, string iconUrl)
        {
            try
            {
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                byte[] bytes = await http.GetByteArrayAsync(iconUrl).ConfigureAwait(false);
                if (bytes != null && bytes.Length > 256)
                {
                    try
                    {
                        string iconsDir = HistoryService.GetIconsDirectory();
                        if (!string.IsNullOrWhiteSpace(titleId))
                            File.WriteAllBytes(Path.Combine(iconsDir, $"{titleId}.png"), bytes);
                    }
                    catch { }

                    if (slot.OriginalIconBytes == null || slot.OriginalIconBytes.Length == 0)
                    {
                        slot.OriginalIconBytes = bytes;
                    }
                }
            }
            catch { }
        }

        private void SlotInlineMetadata_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is RuTrackerGameSlotItem slot)
            {
                if (fe is TextBox tb && tb.FocusState != FocusState.Unfocused)
                {
                    slot.IsUserEditedMetadata = true;
                }
            }
        }

        private async void SelectSlotIconQuick_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not RuTrackerGameSlotItem slot)
                return;

            try
            {
                var picker = new FileOpenPicker();
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");
                picker.FileTypeFilter.Add(".bmp");
                picker.FileTypeFilter.Add(".webp");

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    ApplyCustomIconFileToSlot(slot, file.Path);
                }
            }
            catch (Exception ex)
            {
                LiveTelemetryText.Text = $"⚠️ Ошибка выбора иконки: {ex.Message}";
            }
        }

        private void ApplyCustomIconFileToSlot(RuTrackerGameSlotItem slot, string filePath)
        {
            try
            {
                int targetSize = slot.Is3ds ? 48 : 256;
                slot.CustomIconPath = filePath;
                slot.CustomIconBytes = IconBadgeStudioService.RenderIconWithBadges(
                    File.ReadAllBytes(filePath), targetSize, slot.BadgeRus, slot.Badge60Fps, slot.BadgeMod, slot.BadgeAllDlc,
                    slot.CustomBadgeText, slot.BadgePosition, outputPng: true);
                slot.IsUserEditedMetadata = true;
                LiveTelemetryText.Text = $"🖼 Иконка для «{slot.SlotBadge}» успешно обновлена: {Path.GetFileName(filePath)}";
            }
            catch (Exception ex)
            {
                LiveTelemetryText.Text = $"⚠️ Не удалось обработать изображение иконки: {ex.Message}";
            }
        }

        private void SlotIconPreview_DragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
                e.DragUIOverride.Caption = "Заменить иконку (256x256)";
            }
        }

        private async void SlotIconPreview_Drop(object sender, DragEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not RuTrackerGameSlotItem slot)
                return;

            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                var file = items.OfType<Windows.Storage.StorageFile>().FirstOrDefault();
                if (file != null)
                {
                    string ext = Path.GetExtension(file.Path).ToLowerInvariant();
                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".webp")
                    {
                        ApplyCustomIconFileToSlot(slot, file.Path);
                    }
                }
            }
        }


        private void CopyTextToClipboardWithFeedback(string? textToCopy, string label)
        {
            string clean = (textToCopy ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(clean))
                return;

            try
            {
                var pkg = new Windows.ApplicationModel.DataTransfer.DataPackage();
                pkg.SetText(clean);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(pkg);
                Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
                StatusText.Text = $"Скопировано ({label}): {clean}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Не удалось скопировать {label}: {ex.Message}";
            }
        }

        private void CopySlotTitleId_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is RuTrackerGameSlotItem slot && !string.IsNullOrWhiteSpace(slot.TitleId))
            {
                CopyTextToClipboardWithFeedback(slot.TitleId, "TitleID");
            }
        }

        private void CopySlotVersion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is RuTrackerGameSlotItem slot)
            {
                CopyTextToClipboardWithFeedback(slot.CleanVersionDisplay.TrimStart('v', 'V'), "Версия");
            }
        }

        private void CopySlotInternalVersion_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.Tag is RuTrackerGameSlotItem slot)
            {
                CopyTextToClipboardWithFeedback(slot.InternalVersionDisplay, "Внутренняя версия");
            }
        }

        private void CopyStudioTitleId_Click(object sender, RoutedEventArgs e)
        {
            CopyTextToClipboardWithFeedback(StudioTitleIdText?.Text, "TitleID");
        }

        private void CopyStudioVersion_Click(object sender, RoutedEventArgs e)
        {
            CopyTextToClipboardWithFeedback(StudioVersionText?.Text?.TrimStart('v', 'V'), "Версия");
        }

        private void CopyStudioInternalVersion_Click(object sender, RoutedEventArgs e)
        {
            CopyTextToClipboardWithFeedback(StudioInternalVersionText?.Text, "Внутренняя версия");
        }

        private void OpenSlotMetadataStudio_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not RuTrackerGameSlotItem slot)
                return;

            _activeStudioSlot = slot;
            _activeStudioModel = slot.ToMetadataEditModel();

            _isInitializingStudioOverlay = true;
            StudioSlotBadgeText.Text = slot.SlotBadge;
            StudioTitleIdText.Text = !string.IsNullOrWhiteSpace(_activeStudioModel.TitleId) ? _activeStudioModel.TitleId : "0100000000000000";
            StudioVersionText.Text = (!string.IsNullOrWhiteSpace(_activeStudioModel.Version) ? _activeStudioModel.Version : "1.0.0").TrimStart('v', 'V');
            if (StudioInternalVersionText != null)
                StudioInternalVersionText.Text = slot.InternalVersionDisplay;
            StudioPlatformBadgeText.Text = _activeStudioModel.Is3ds ? "3DS SMDH" : "SWITCH";

            StudioTitleEnglishBox.Text = _activeStudioModel.TitleNameEnglish;
            StudioTitleRussianBox.Text = _activeStudioModel.TitleNameRussian;
            StudioPublisherBox.Text = _activeStudioModel.Publisher;

            StudioBadgeRusCheck.IsChecked = _activeStudioModel.BadgeRus;
            StudioBadge60FpsCheck.IsChecked = _activeStudioModel.Badge60Fps;
            StudioBadgeModCheck.IsChecked = _activeStudioModel.BadgeMod;
            StudioBadgeAllDlcCheck.IsChecked = _activeStudioModel.BadgeAllDlc;
            StudioCustomBadgeTextBox.Text = _activeStudioModel.CustomBadgeText ?? string.Empty;
            StudioBadgeCornerCombo.SelectedIndex = _activeStudioModel.BadgePosition switch { "BottomLeft" => 1, "TopRight" => 2, "TopLeft" => 3, _ => 0 };
            StudioAudioJinglePathBox.Text = _activeStudioModel.CustomAudioWavPath ?? string.Empty;
            _isInitializingStudioOverlay = false;

            RefreshStudioOverlayIconPreview();
            SlotMetadataStudioOverlay.Visibility = Visibility.Visible;
        }

        private void RefreshStudioOverlayIconPreview()
        {
            if (_activeStudioModel == null) return;
            try
            {
                int targetSize = _activeStudioModel.Is3ds ? 48 : 256;
                if (!string.IsNullOrEmpty(_activeStudioModel.CustomIconPath) && File.Exists(_activeStudioModel.CustomIconPath))
                {
                    _activeStudioModel.CustomIconBytes = IconBadgeStudioService.RenderIconWithBadges(
                        File.ReadAllBytes(_activeStudioModel.CustomIconPath),
                        targetSize,
                        _activeStudioModel.BadgeRus,
                        _activeStudioModel.Badge60Fps,
                        _activeStudioModel.BadgeMod,
                        _activeStudioModel.BadgeAllDlc,
                        _activeStudioModel.CustomBadgeText,
                        _activeStudioModel.BadgePosition,
                        outputPng: true);
                }
                else if (_activeStudioModel.HasBadges && _activeStudioModel.OriginalIconBytes != null && _activeStudioModel.OriginalIconBytes.Length > 0)
                {
                    _activeStudioModel.CustomIconBytes = IconBadgeStudioService.RenderIconWithBadges(
                        _activeStudioModel.OriginalIconBytes,
                        targetSize,
                        _activeStudioModel.BadgeRus,
                        _activeStudioModel.Badge60Fps,
                        _activeStudioModel.BadgeMod,
                        _activeStudioModel.BadgeAllDlc,
                        _activeStudioModel.CustomBadgeText,
                        _activeStudioModel.BadgePosition,
                        outputPng: true);
                }
                else if (string.IsNullOrEmpty(_activeStudioModel.CustomIconPath) && !_activeStudioModel.HasBadges)
                {
                    _activeStudioModel.CustomIconBytes = null;
                }

                byte[]? previewBytes = _activeStudioModel.CustomIconBytes ?? _activeStudioModel.OriginalIconBytes;
                if (previewBytes != null && previewBytes.Length > 0)
                {
                    using var ms = new MemoryStream(previewBytes);
                    var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                    bmp.SetSource(ms.AsRandomAccessStream());
                    StudioIconPreviewImage.Source = bmp;
                }
                else
                {
                    StudioIconPreviewImage.Source = null;
                }

                StudioResetIconBtn.IsEnabled = !string.IsNullOrEmpty(_activeStudioModel.CustomIconPath) || _activeStudioModel.HasBadges || _activeStudioModel.CustomIconBytes != null;
            }
            catch { }
        }

        private async void StudioSelectIcon_Click(object sender, RoutedEventArgs e)
        {
            if (_activeStudioModel == null) return;
            try
            {
                var picker = new FileOpenPicker();
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
                picker.ViewMode = PickerViewMode.Thumbnail;
                picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                picker.FileTypeFilter.Add(".png");
                picker.FileTypeFilter.Add(".jpg");
                picker.FileTypeFilter.Add(".jpeg");
                picker.FileTypeFilter.Add(".bmp");
                picker.FileTypeFilter.Add(".webp");

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    _activeStudioModel.CustomIconPath = file.Path;
                    RefreshStudioOverlayIconPreview();
                }
            }
            catch { }
        }

        private void StudioResetIcon_Click(object sender, RoutedEventArgs e)
        {
            if (_activeStudioModel == null) return;
            _activeStudioModel.CustomIconPath = null;
            _activeStudioModel.CustomIconBytes = null;
            _isInitializingStudioOverlay = true;
            StudioBadgeRusCheck.IsChecked = false;
            StudioBadge60FpsCheck.IsChecked = false;
            StudioBadgeModCheck.IsChecked = false;
            StudioBadgeAllDlcCheck.IsChecked = false;
            StudioCustomBadgeTextBox.Text = string.Empty;
            _isInitializingStudioOverlay = false;

            _activeStudioModel.BadgeRus = false;
            _activeStudioModel.Badge60Fps = false;
            _activeStudioModel.BadgeMod = false;
            _activeStudioModel.BadgeAllDlc = false;
            _activeStudioModel.CustomBadgeText = string.Empty;
            RefreshStudioOverlayIconPreview();
        }

        private void StudioBadgeOption_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializingStudioOverlay || _activeStudioModel == null) return;
            _activeStudioModel.BadgeRus = StudioBadgeRusCheck.IsChecked == true;
            _activeStudioModel.Badge60Fps = StudioBadge60FpsCheck.IsChecked == true;
            _activeStudioModel.BadgeMod = StudioBadgeModCheck.IsChecked == true;
            _activeStudioModel.BadgeAllDlc = StudioBadgeAllDlcCheck.IsChecked == true;
            RefreshStudioOverlayIconPreview();
        }

        private void StudioCustomBadgeTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializingStudioOverlay || _activeStudioModel == null) return;
            _activeStudioModel.CustomBadgeText = StudioCustomBadgeTextBox.Text?.Trim() ?? string.Empty;
            RefreshStudioOverlayIconPreview();
        }

        private void StudioBadgeCornerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializingStudioOverlay || _activeStudioModel == null || StudioBadgeCornerCombo == null) return;
            _activeStudioModel.BadgePosition = StudioBadgeCornerCombo.SelectedIndex switch { 1 => "BottomLeft", 2 => "TopRight", 3 => "TopLeft", _ => "BottomRight" };
            RefreshStudioOverlayIconPreview();
        }

        private async void StudioSelectAudioWav_Click(object sender, RoutedEventArgs e)
        {
            if (_activeStudioModel == null) return;
            try
            {
                var picker = new FileOpenPicker();
                InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
                picker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
                picker.FileTypeFilter.Add(".wav");
                picker.FileTypeFilter.Add(".bcwav");
                picker.FileTypeFilter.Add(".cwav");

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    _activeStudioModel.CustomAudioWavPath = file.Path;
                    StudioAudioJinglePathBox.Text = file.Path;
                }
            }
            catch { }
        }

        private void StudioClearAudioWav_Click(object sender, RoutedEventArgs e)
        {
            if (_activeStudioModel == null) return;
            _activeStudioModel.CustomAudioWavPath = string.Empty;
            StudioAudioJinglePathBox.Text = string.Empty;
        }

        private void StudioIconBorder_DragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
                e.DragUIOverride.Caption = "Заменить иконку (256x256)";
            }
        }

        private async void StudioIconBorder_Drop(object sender, DragEventArgs e)
        {
            if (_activeStudioModel == null) return;
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                var file = items.OfType<Windows.Storage.StorageFile>().FirstOrDefault();
                if (file != null)
                {
                    string ext = Path.GetExtension(file.Path).ToLowerInvariant();
                    if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp" || ext == ".webp")
                    {
                        _activeStudioModel.CustomIconPath = file.Path;
                        RefreshStudioOverlayIconPreview();
                    }
                }
            }
        }

        private void StudioSave_Click(object sender, RoutedEventArgs e)
        {
            if (_activeStudioSlot != null && _activeStudioModel != null)
            {
                _activeStudioModel.TitleNameEnglish = StudioTitleEnglishBox.Text?.Trim() ?? string.Empty;
                _activeStudioModel.TitleNameRussian = StudioTitleRussianBox.Text?.Trim() ?? string.Empty;
                _activeStudioModel.Publisher = StudioPublisherBox.Text?.Trim() ?? "Nintendo";
                _activeStudioSlot.ApplyFromMetadataEditModel(_activeStudioModel);
                LiveTelemetryText.Text = $"🎨 Метаданные NACP сохранены для «{_activeStudioSlot.SlotBadge}»: EN=«{_activeStudioSlot.InternalTitleEnglish}», RU=«{_activeStudioSlot.InternalTitleRussian}», Издатель=«{_activeStudioSlot.Publisher}».";
            }
            SlotMetadataStudioOverlay.Visibility = Visibility.Collapsed;
        }

        private void StudioCancel_Click(object sender, RoutedEventArgs e)
        {
            SlotMetadataStudioOverlay.Visibility = Visibility.Collapsed;
        }

        private void RebuildFileAndLinkGroups()
        {
            if (_suppressGroupRebuild) return;

            // 1. Формируем блоки файлов строго по играм (ИГРА #1, ИГРА #2, ..., и Общие файлы, если есть)
            var sortedFiles = _torrentFiles
                .OrderBy(f => f.TargetGameSlot == 0 ? 999 : f.TargetGameSlot)
                .ThenBy(f => RuTrackerModService.GetFileRolePriority(f.CategoryBadge))
                .ThenBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var newFileGroups = new List<RuTrackerFileGroupBlock>();
            foreach (var grp in sortedFiles.GroupBy(f => f.TargetGameSlot))
            {
                int slotNum = grp.Key;
                var slotObj = _gameSlots.FirstOrDefault(s => s.SlotNumber == slotNum);
                string badge = slotNum <= 0 ? "ОБЩИЕ ФАЙЛЫ" : $"ИГРА #{slotNum}";
                string title = slotNum <= 0
                    ? "Для всех частей раздачи"
                    : (!string.IsNullOrWhiteSpace(slotObj?.GameTitle) ? slotObj!.GameTitle : $"Игра #{slotNum}");

                string fileFilter = TorrentFileFilterBox?.Text?.Trim() ?? string.Empty;
                var visibleGrpFiles = string.IsNullOrEmpty(fileFilter)
                    ? grp.ToList()
                    : grp.Where(x =>
                        (x.DisplayPath?.Contains(fileFilter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (x.RelativePath?.Contains(fileFilter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (x.CategoryBadge?.Contains(fileFilter, StringComparison.OrdinalIgnoreCase) ?? false)).ToList();

                if (visibleGrpFiles.Count == 0 && !string.IsNullOrEmpty(fileFilter))
                    continue;

                long totalBytes = grp.Sum(x => Math.Max(0L, x.SizeBytes));
                long selBytes = grp.Where(x => x.IsSelected).Sum(x => Math.Max(0L, x.SizeBytes));
                int count = grp.Count();
                int selCount = grp.Count(x => x.IsSelected);
                string countWord = count == 1 ? "1 файл" : (count >= 2 && count <= 4 ? $"{count} файла" : $"{count} файлов");
                string summary = selCount == count
                    ? $"{countWord} • {ProcessingTask.FormatSize(selBytes)}"
                    : $"{selCount} из {count} файлов • {ProcessingTask.FormatSize(selBytes)}";

                var block = new RuTrackerFileGroupBlock
                {
                    SlotNumber = slotNum,
                    GroupBadge = badge,
                    GroupTitle = title,
                    GroupSummary = summary
                };
                foreach (var fileItem in visibleGrpFiles)
                    block.Files.Add(fileItem);
                newFileGroups.Add(block);
            }

            // Проверяем, изменилась ли структура групп, чтобы не сбрасывать скролл без необходимости
            bool sameFileStructure = _fileGroups.Count == newFileGroups.Count &&
                _fileGroups.Zip(newFileGroups, (a, b) =>
                    a.SlotNumber == b.SlotNumber &&
                    a.Files.SequenceEqual(b.Files)).All(x => x);

            if (sameFileStructure)
            {
                for (int i = 0; i < _fileGroups.Count; i++)
                {
                    _fileGroups[i].GroupBadge = newFileGroups[i].GroupBadge;
                    _fileGroups[i].GroupTitle = newFileGroups[i].GroupTitle;
                    _fileGroups[i].GroupSummary = newFileGroups[i].GroupSummary;
                }
            }
            else
            {
                _fileGroups.Clear();
                foreach (var g in newFileGroups)
                    _fileGroups.Add(g);
            }

            if (TorrentFilesHeader != null && _torrentFiles.Count > 0 && (_torrentFiles.Count > 1 || _torrentFiles[0].CategoryBadge != "MAGNET"))
            {
                TorrentFilesHeader.Text = $"Файлы в раздаче ({_torrentFiles.Count} шт. • игр: {_fileGroups.Count})";
            }

            // 2. Формируем блоки ссылок из темы и обсуждений по играм
            var sortedLinks = _discussionLinks
                .OrderBy(l => l.TargetGameSlot == 0 ? 0 : l.TargetGameSlot)
                .ThenBy(l => l.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var newLinkGroups = new List<RuTrackerLinkGroupBlock>();
            foreach (var grp in sortedLinks.GroupBy(l => l.TargetGameSlot))
            {
                int slotNum = grp.Key;
                var slotObj = _gameSlots.FirstOrDefault(s => s.SlotNumber == slotNum);
                string badge = slotNum <= 0 ? "ОБЩИЕ ССЫЛКИ" : $"ИГРА #{slotNum}";
                string title = slotNum <= 0
                    ? "Применяются ко всем выбранным играм"
                    : (!string.IsNullOrWhiteSpace(slotObj?.GameTitle) ? slotObj!.GameTitle : $"Игра #{slotNum}");

                int count = grp.Count();
                int selCount = grp.Count(x => x.IsSelected);
                var block = new RuTrackerLinkGroupBlock
                {
                    SlotNumber = slotNum,
                    GroupBadge = badge,
                    GroupTitle = title,
                    GroupSummary = $"выбрано {selCount} из {count}"
                };
                foreach (var linkItem in grp)
                    block.Links.Add(linkItem);
                newLinkGroups.Add(block);
            }

            bool sameLinkStructure = _linkGroups.Count == newLinkGroups.Count &&
                _linkGroups.Zip(newLinkGroups, (a, b) =>
                    a.SlotNumber == b.SlotNumber &&
                    a.Links.SequenceEqual(b.Links)).All(x => x);

            if (sameLinkStructure)
            {
                for (int i = 0; i < _linkGroups.Count; i++)
                {
                    _linkGroups[i].GroupBadge = newLinkGroups[i].GroupBadge;
                    _linkGroups[i].GroupTitle = newLinkGroups[i].GroupTitle;
                    _linkGroups[i].GroupSummary = newLinkGroups[i].GroupSummary;
                }
            }
            else
            {
                _linkGroups.Clear();
                foreach (var g in newLinkGroups)
                    _linkGroups.Add(g);
            }

            if (EmptyLinksHintBorder != null)
                EmptyLinksHintBorder.Visibility = _linkGroups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (RestoreCloudBtnText != null)
                RestoreCloudBtnText.Text = _discussionLinks.Count > 0 ? $"Ссылки ({_discussionLinks.Count})" : "Ссылки и сборка";
        }

        private void OnItemSelectionChangedForUnlocker(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TorrentFileSelectionItem.IsSelected) ||
                e.PropertyName == nameof(CloudModLinkItem.IsSelected) ||
                e.PropertyName == nameof(TorrentFileSelectionItem.TargetGameSlot) ||
                e.PropertyName == nameof(CloudModLinkItem.TargetGameSlot))
            {
                UpdateDiscussionSummaryText();
                UpdateDlcUnlockerAutoState();
                EnrichGameSlotsFromFilesAndTitleDb();
                UpdateGameSlotsSummary();
            }
        }

        private void CloudDlcLinksBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateDlcUnlockerAutoState();
        }

        private void UpdateDiscussionSummaryText()
        {
            int total = _discussionLinks.Count;
            int selected = _discussionLinks.Count(x => x.IsSelected);
            ModLinksHintText.Text = total > 0 ? $"Ссылок: {total} (выбр. {selected})" : "Ссылок: 0";
        }

        private void UpdateDlcUnlockerAutoState()
        {
            bool hasInTorrent = _torrentFiles.Any(f => f.IsSelected &&
                (f.CategoryBadge.Contains("UNLOCKER", StringComparison.OrdinalIgnoreCase) ||
                 f.RelativePath.Contains("unlocker", StringComparison.OrdinalIgnoreCase) ||
                 f.RelativePath.Contains("анлокер", StringComparison.OrdinalIgnoreCase)));

            bool hasInDiscussion = _discussionLinks.Any(d => d.IsSelected &&
                (string.Equals(d.RoleCategory, "UNLOCKER", StringComparison.OrdinalIgnoreCase) ||
                 d.Title.Contains("unlocker", StringComparison.OrdinalIgnoreCase) ||
                 d.Title.Contains("анлокер", StringComparison.OrdinalIgnoreCase) ||
                 d.Url.Contains("unlocker", StringComparison.OrdinalIgnoreCase)));

            string manualDlcText = CloudDlcLinksBox?.Text ?? string.Empty;
            bool hasInManualDlc = manualDlcText.Contains("unlocker", StringComparison.OrdinalIgnoreCase) ||
                                  manualDlcText.Contains("анлокер", StringComparison.OrdinalIgnoreCase);

            if (StitchDlcUnlockerCheckBox != null)
            {
                StitchDlcUnlockerCheckBox.IsChecked = hasInTorrent || hasInDiscussion || hasInManualDlc;
            }
        }

        private void AppendCloudModLinks(IEnumerable<string> links)
        {
            var existing = (CloudModLinksBox.Text ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();

            foreach (var link in links)
            {
                if (!existing.Contains(link, StringComparer.OrdinalIgnoreCase))
                    existing.Add(link);
            }

            CloudModLinksBox.Text = string.Join(Environment.NewLine, existing);
        }

        private void TorrentFileFilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isUiInitialized) return;
            RebuildFileAndLinkGroups();
        }

        private void SelectAllFiles_Click(object sender, RoutedEventArgs e)
        {
            _suppressGroupRebuild = true;
            foreach (var f in _torrentFiles)
                f.IsSelected = true;
            _suppressGroupRebuild = false;
            UpdateDlcUnlockerAutoState();
            EnrichGameSlotsFromFilesAndTitleDb();
            UpdateGameSlotsSummary();
        }

        private void DeselectAllFiles_Click(object sender, RoutedEventArgs e)
        {
            _suppressGroupRebuild = true;
            foreach (var f in _torrentFiles)
                f.IsSelected = false;
            _suppressGroupRebuild = false;
            UpdateDlcUnlockerAutoState();
            UpdateGameSlotsSummary();
        }

        private void ToggleFileGroupSelection_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not RuTrackerFileGroupBlock groupBlock)
                return;

            var targetFiles = _torrentFiles.Where(f => f.TargetGameSlot == groupBlock.SlotNumber).ToList();
            if (targetFiles.Count == 0) return;

            bool selectNext = targetFiles.Any(f => !f.IsSelected);
            _suppressGroupRebuild = true;
            foreach (var f in targetFiles)
                f.IsSelected = selectNext;
            _suppressGroupRebuild = false;
            UpdateDlcUnlockerAutoState();
            EnrichGameSlotsFromFilesAndTitleDb();
            UpdateGameSlotsSummary();
        }

        private void SelectOnlyUpdatesAndMods_Click(object sender, RoutedEventArgs e)
        {
            _suppressGroupRebuild = true;
            foreach (var f in _torrentFiles)
            {
                f.IsSelected = f.CategoryBadge.Contains("ОБНОВЛЕНИЕ", StringComparison.OrdinalIgnoreCase) ||
                               f.CategoryBadge.Contains("DLC", StringComparison.OrdinalIgnoreCase) ||
                               f.CategoryBadge.Contains("МОД", StringComparison.OrdinalIgnoreCase);
            }
            _suppressGroupRebuild = false;
            UpdateDlcUnlockerAutoState();
            EnrichGameSlotsFromFilesAndTitleDb();
            UpdateGameSlotsSummary();
        }

        private void OpenTopicInBrowser_Click(object sender, RoutedEventArgs e)
        {
            string url = !string.IsNullOrWhiteSpace(_currentTopicUrl)
                ? _currentTopicUrl
                : $"https://{((MirrorCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "rutracker.org")}/forum/index.php";

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        }

        private void PauseResumeDownload_Click(object sender, RoutedEventArgs e)
        {
            if (_activeTask == null) return;
            if (_activeTask.IsPaused)
            {
                _activeTask.Resume();
                if (PauseResumeDownloadBtnIcon != null) PauseResumeDownloadBtnIcon.Glyph = "\uE769";
                if (PauseResumeDownloadBtnText != null) PauseResumeDownloadBtnText.Text = "Пауза";
                StatusText.Text = "Возобновление загрузки...";
            }
            else
            {
                _activeTask.Pause();
                if (PauseResumeDownloadBtnIcon != null) PauseResumeDownloadBtnIcon.Glyph = "\uE768";
                if (PauseResumeDownloadBtnText != null) PauseResumeDownloadBtnText.Text = "Продолжить";
                StatusText.Text = "Загрузка приостановлена (пауза).";
                SpeedPeersText.Text = string.Empty;
            }
        }

        private void CancelDownload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                lock (_batchLock)
                {
                    foreach (var cts in _activeBatchCtsList.ToList())
                    {
                        try { cts.Cancel(); } catch { }
                    }
                    _activeBatchCtsList.Clear();
                }
                _activeTask?.Cancel();
                _downloadCts?.Cancel();
                StatusText.Text = "⏹ Все активные загрузки отменены пользователем.";
                SpeedPeersText.Text = string.Empty;
                DownloadProgressBar.Value = 0;
                DownloadProgressBar.IsIndeterminate = false;
                PauseResumeDownloadBtn.Visibility = Visibility.Collapsed;
            }
            catch { }
        }

        private async void DownloadOnlyCloudMods_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteDownloadPipelineAsync(downloadTorrent: false);
        }

        private async void StartFullDownload_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteDownloadPipelineAsync(downloadTorrent: true);
        }

        private async Task ExecuteDownloadPipelineAsync(bool downloadTorrent)
        {
            SaveCurrentSettings();

            var updateLinks = (CloudUpdateLinksBox.Text ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                .Take(3)
                .ToList();

            var dlcLinks = (CloudDlcLinksBox.Text ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                .Take(3)
                .ToList();

            var modLinks = (CloudModLinksBox.Text ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                .Take(5)
                .ToList();

            var globalManualLinks = updateLinks.Concat(dlcLinks).Concat(modLinks)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var activeSlots = _gameSlots.Where(s => s.IsEnabled).ToList();
            if (activeSlots.Count == 0)
            {
                activeSlots.Add(new RuTrackerGameSlotItem
                {
                    SlotNumber = 1,
                    IsEnabled = true,
                    GameTitle = !string.IsNullOrWhiteSpace(_currentTopicTitle) ? _currentTopicTitle : "Игра 1"
                });
            }

            bool anyLinksSelected = _discussionLinks.Any(x => x.IsSelected) ||
                                    globalManualLinks.Count > 0 ||
                                    activeSlots.Any(s => !string.IsNullOrWhiteSpace(s.ManualExtraLinks));

            var selectedUiTopic = TopicsListView.SelectedItem as RuTrackerTopicItem;
            if (downloadTorrent && _torrentFiles.Count == 0 && selectedUiTopic != null)
            {
                _service.PrimeQuickTopicState(selectedUiTopic);
                if (_service.LoadedTorrentBytes != null || _service.LoadedMagnet != null || !string.IsNullOrWhiteSpace(selectedUiTopic.InfoHash))
                {
                    _torrentFiles.Add(new TorrentFileSelectionItem
                    {
                        IsSelected = true,
                        TargetGameSlot = 1,
                        RelativePath = $"{selectedUiTopic.CleanTitleDisplay} - Полный пакет раздачи",
                        Length = selectedUiTopic.EffectiveSizeBytes,
                        CategoryBadge = "MAGNET"
                    });
                }
            }

            if (downloadTorrent && _torrentFiles.Count > 0 && !_torrentFiles.Any(f => f.IsSelected))
            {
                StatusText.Text = "Выберите хотя бы один файл из раздачи (отметьте галочкой) или скачайте только моды.";
                return;
            }
            bool hasTorrentSource = downloadTorrent && (
                (_torrentFiles.Count > 0 && _torrentFiles.Any(f => f.IsSelected)) ||
                _service.LoadedTorrentBytes != null ||
                _service.LoadedMagnet != null);
            if (!hasTorrentSource && !anyLinksSelected)
            {
                StatusText.Text = "Выберите раздачу RuTracker / .torrent файл или отметьте ссылки на русификатор / облачный мод.";
                return;
            }

            string baseDownloadDir = DownloadFolderBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(baseDownloadDir))
            {
                baseDownloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "STORM_SWITCH_BOX_Downloads");
            }

            string batchShortId = Guid.NewGuid().ToString("N").Substring(0, 6);
            string safeSubFolder = SanitizeFolderName(!string.IsNullOrWhiteSpace(_currentTopicTitle)
                ? _currentTopicTitle
                : (_targetTask?.GameName ?? $"Release_{DateTime.Now:yyyyMMdd_HHmmss}"));

            string sharedReleaseDir = Path.Combine(baseDownloadDir, $"{safeSubFolder}_{batchShortId}");
            Directory.CreateDirectory(sharedReleaseDir);

            string globalCustomModTitle = CustomModNameBox.Text?.Trim() ?? string.Empty;
            bool deleteRtSource = DeleteRuTrackerSourceCheckBox.IsChecked == true;
            bool autoBuild = AutoBuildCheckBox.IsChecked == true;
            bool appendAutoTags = AppendAutoTagsCheckBox.IsChecked == true;

            RuTrackerTopicItem? currentTopic = null;
            if (!string.IsNullOrEmpty(_service.ActiveTopicId))
            {
                currentTopic = _topics.FirstOrDefault(t => t.TopicId == _service.ActiveTopicId) ?? new RuTrackerTopicItem
                {
                    TopicId = _service.ActiveTopicId,
                    Title = _currentTopicTitle,
                    Seeds = _service.ActiveTopicSeeds
                };
            }

            var isolatedService = _service.CreateIsolatedDownloadSession();
            var batchCts = new CancellationTokenSource();
            _downloadCts = batchCts;

            lock (_batchLock)
            {
                _activeBatchCtsList.Add(batchCts);
                _activeBatchesCount++;
            }

            var createdTasks = new List<(ProcessingTask Task, RuTrackerTaskPayload Payload, RuTrackerGameSlotItem Slot)>();

            for (int idx = 0; idx < activeSlots.Count; idx++)
            {
                var slot = activeSlots[idx];
                string slotTitle = !string.IsNullOrWhiteSpace(slot.GameTitle)
                    ? slot.GameTitle.Trim()
                    : (!string.IsNullOrWhiteSpace(_currentTopicTitle) ? _currentTopicTitle : $"Игра {slot.SlotNumber}");

                var slotFiles = _torrentFiles
                    .Where(f => f.IsSelected && (_gameSlots.Count <= 1 || f.TargetGameSlot == 0 || f.TargetGameSlot == slot.SlotNumber))
                    .Select(f => new TorrentFileSelectionItem
                    {
                        IsSelected = true,
                        RelativePath = f.RelativePath,
                        Length = f.Length,
                        CategoryBadge = f.CategoryBadge,
                        IsRealTorrentFile = f.IsRealTorrentFile,
                        TargetGameSlot = f.TargetGameSlot,
                        GroupedRelativePaths = new List<string>(f.GroupedRelativePaths),
                        DetectedTitleId = f.DetectedTitleId,
                        DisplayPath = f.DisplayPath
                    })
                    .ToList();

                if (slotFiles.Count == 0 && hasTorrentSource && _gameSlots.Count <= 1)
                {
                    slotFiles = _torrentFiles
                        .Where(f => f.IsSelected)
                        .Select(f => new TorrentFileSelectionItem
                        {
                            IsSelected = true,
                            RelativePath = f.RelativePath,
                            Length = f.Length,
                            CategoryBadge = f.CategoryBadge,
                            IsRealTorrentFile = f.IsRealTorrentFile,
                            TargetGameSlot = f.TargetGameSlot,
                            GroupedRelativePaths = new List<string>(f.GroupedRelativePaths),
                            DetectedTitleId = f.DetectedTitleId,
                            DisplayPath = f.DisplayPath
                        })
                        .ToList();
                }

                var slotDiscussionLinks = _discussionLinks
                    .Where(x => x.IsSelected && (_gameSlots.Count <= 1 || x.TargetGameSlot == 0 || x.TargetGameSlot == slot.SlotNumber))
                    .Select(x => x.MirrorUrls != null && x.MirrorUrls.Count > 0
                        ? string.Join("|MIRROR|", new[] { x.Url }.Concat(x.MirrorUrls).Distinct(StringComparer.OrdinalIgnoreCase))
                        : x.Url)
                    .ToList();

                var slotManualLinks = (slot.ManualExtraLinks ?? string.Empty)
                    .Split(new[] { ' ', ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var slotCloudLinks = slotDiscussionLinks
                    .Concat(slotManualLinks)
                    .Concat(globalManualLinks)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (_gameSlots.Count > 1 && slotFiles.Count == 0 && slotCloudLinks.Count == 0)
                    continue;

                string slotTargetDir = activeSlots.Count > 1
                    ? Path.Combine(sharedReleaseDir, $"Part_{slot.SlotNumber}_{SanitizeFolderName(slotTitle)}")
                    : sharedReleaseDir;
                Directory.CreateDirectory(slotTargetDir);

                string effectiveModTitle = !string.IsNullOrWhiteSpace(slot.CustomModTitle)
                    ? slot.CustomModTitle.Trim()
                    : globalCustomModTitle;

                string chosenFormat = !string.IsNullOrWhiteSpace(slot.TargetFormat)
                    ? slot.TargetFormat.Trim()
                    : GetDefaultTargetFormat();

                var slotMetadataModel = slot.ToMetadataEditModel();
                var payload = new RuTrackerTaskPayload
                {
                    Topic = currentTopic,
                    TorrentBytes = isolatedService.LoadedTorrentBytes,
                    MagnetLink = isolatedService.LoadedMagnet,
                    TorrentFiles = slotFiles,
                    CloudLinks = slotCloudLinks,
                    TargetReleaseDir = slotTargetDir,
                    CustomModTitle = effectiveModTitle,
                    AutoBuild = autoBuild,
                    HasTorrentSource = hasTorrentSource,
                    SlotNumber = slot.SlotNumber,
                    TotalSlots = activeSlots.Count,
                    SlotGameTitle = slotTitle,
                    SharedReleaseDir = sharedReleaseDir,
                    AppendAutoTags = appendAutoTags,
                    CustomMetadata = slotMetadataModel
                };

                ProcessingTask task;
                string effectiveTid = slot.TitleId ?? string.Empty;
                payload.PosterUrl = !string.IsNullOrWhiteSpace(_currentTopicPosterUrl) ? _currentTopicPosterUrl : (payload.Topic?.PosterUrl ?? string.Empty);
                var slotIconBytes = slot.CustomIconBytes ?? slot.OriginalIconBytes;

                long initBytes = slotFiles.Where(f => f.IsSelected && f.Length > 0).Sum(f => f.Length);
                if (initBytes <= 0 && slotFiles.Count > 0)
                    initBytes = slotFiles.Where(f => f.Length > 0).Sum(f => f.Length);

                if (_targetTask != null && _activeTask == null && idx == 0 && activeSlots.Count == 1)
                {
                    task = _targetTask;
                    if (!string.IsNullOrWhiteSpace(slotTitle))
                    {
                        task.GameName = slotTitle;
                        task.OutputFileName = SanitizeFileName(slotTitle);
                    }
                    task.TargetFormat = chosenFormat;
                    task.KeepExactOutputFileName = !appendAutoTags;
                    task.IsRuTrackerTask = true;
                    task.DeleteSourceAfterVerify = deleteRtSource;
                    task.RuTrackerPayload = payload;
                    task.CustomMetadata = slotMetadataModel;
                    if (!string.IsNullOrWhiteSpace(effectiveTid))
                    {
                        task.TitleId = effectiveTid;
                        task.GroupId = TasksViewModel.GetTitleGroupPrefix(effectiveTid);
                    }
                    if (slot.SlotIconBitmap != null)
                        task.GameIcon = slot.SlotIconBitmap;
                    if (slotIconBytes != null && slotIconBytes.Length > 64)
                    {
                        HistoryService.SaveTaskIconToCache(task, slotIconBytes);
                    }
                    else if (task.GameIcon == null)
                    {
                        HistoryService.TryLoadIconForTask(task);
                    }

                    if (initBytes > 0)
                    {
                        task.SourceSizeBytes = initBytes;
                        task.SourceSize = ProcessingTask.FormatSize(initBytes);
                    }
                    task.InputFolders = slotTargetDir;
                    task.CompressionLevel = ProcessingTask.ComputeDefaultCompressionLevel(chosenFormat, task.IsSwitch2Task);
                    task.FilesCount = $"{slotFiles.Count} файл(ов)" + (slotCloudLinks.Count > 0 ? $" + {slotCloudLinks.Count} мод." : "");
                    task.Cts = batchCts;
                }
                else
                {
                    task = new ProcessingTask
                    {
                        Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                        GameName = slotTitle,
                        TitleId = effectiveTid,
                        GroupId = !string.IsNullOrWhiteSpace(effectiveTid) ? TasksViewModel.GetTitleGroupPrefix(effectiveTid) : string.Empty,
                        GameIcon = slot.SlotIconBitmap,
                        CustomMetadata = slotMetadataModel,
                        Operation = "Multi",
                        SourceFormat = "MULTI",
                        TargetFormat = chosenFormat,
                        KeepExactOutputFileName = !appendAutoTags,
                        Status = hasTorrentSource ? "Подключение к рою DHT/P2P..." : "Скачивание модов...",
                        Progress = 0,
                        IsRunning = true,
                        IsRuTrackerTask = true,
                        DeleteSourceAfterVerify = deleteRtSource,
                        OutputFolder = baseDownloadDir,
                        OutputFileName = SanitizeFileName(slotTitle),
                        SourceSizeBytes = initBytes,
                        SourceSize = initBytes > 0 ? ProcessingTask.FormatSize(initBytes) : "-",
                        InputFolders = slotTargetDir,
                        CompressionLevel = ProcessingTask.ComputeDefaultCompressionLevel(chosenFormat, false),
                        FilesCount = $"{slotFiles.Count} файл(ов)" + (slotCloudLinks.Count > 0 ? $" + {slotCloudLinks.Count} мод." : ""),
                        LogDetails = $"[RuTracker Пакет #{batchShortId}] Старт задачи #{slot.SlotNumber}: {slotTitle} (Формат: {chosenFormat})\nЦелевая папка: {slotTargetDir}",
                        RuTrackerPayload = payload,
                        Cts = batchCts
                    };
                    if (slotIconBytes != null && slotIconBytes.Length > 64)
                    {
                        HistoryService.SaveTaskIconToCache(task, slotIconBytes);
                    }
                    else if (task.GameIcon == null)
                    {
                        HistoryService.TryLoadIconForTask(task);
                    }
                    _viewModel.Tasks.Add(task);
                }

                createdTasks.Add((task, payload, slot));
            }

            _viewModel.SavePersistedTasks();

            var primaryTask = createdTasks[0].Task;
            _activeTask = primaryTask;

            string displayBatchTitle = createdTasks.Count == 1
                ? createdTasks[0].Payload.SlotGameTitle
                : $"{_currentTopicTitle} ({createdTasks.Count} игр)";

            var rowItem = new RuTrackerActiveDownloadItem
            {
                BatchId = batchShortId,
                DisplayTitle = displayBatchTitle,
                StageBadge = "В ОЧЕРЕДИ",
                StageBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 116, 139)),
                ProgressPercent = 0,
                ProgressDetails = "В очереди закачек...",
                SpeedAndPeersText = "В очереди",
                IsActive = true,
                Cts = batchCts,
                CreatedTasks = createdTasks,
                SharedReleaseDir = sharedReleaseDir,
                HasTorrentSource = hasTorrentSource,
                CurrentTopic = currentTopic,
                Service = isolatedService,
                ViewModel = _viewModel,
                DeleteSourceAfterVerify = deleteRtSource
            };

            foreach (var (t, _, _) in createdTasks)
            {
                t.OnPauseRequested = () => RuTrackerDownloadQueueService.Instance.TogglePauseItem(rowItem);
                t.OnResumeRequested = () => RuTrackerDownloadQueueService.Instance.TogglePauseItem(rowItem);
                t.OnCancelRequested = () => RuTrackerDownloadQueueService.Instance.CancelOrRemoveItem(rowItem);
            }

            RuTrackerDownloadQueueService.Instance.EnqueueBatch(rowItem);

            ActiveDownloadsScrollViewer.Visibility = Visibility.Visible;
            DownloadProgressBar.Visibility = Visibility.Collapsed;
            PauseResumeDownloadBtn.Visibility = Visibility.Visible;
            if (PauseResumeDownloadBtnIcon != null) PauseResumeDownloadBtnIcon.Glyph = "";
            if (PauseResumeDownloadBtnText != null) PauseResumeDownloadBtnText.Text = App.Localization.TranslateText("Пауза");
            CancelDownloadBtn.IsEnabled = true;

            await Task.CompletedTask;
        }

        public static async Task ExecuteRuTrackerPipelineAsync(
            ProcessingTask task,
            RuTrackerTaskPayload payload,
            TasksViewModel viewModel,
            RuTrackerModService service,
            Action<TorrentDownloadProgress>? onTorrentProgress = null,
            Action<(double Percent, string Status)>? onModProgress = null)
        {
            var ct = task.Cts?.Token ?? CancellationToken.None;
            task.HasActiveWorker = true;
            App.RunOnUI(() =>
            {
                task.IsRunning = true;
                task.IsPaused = false;
            });

            try
            {
                // 1. Скачивание торрента / Magnet
                List<string>? downloadedTorrentFiles = null;
                if (payload.HasTorrentSource)
                {
                    App.RunOnUI(() => { task.Status = "Загрузка торрента..."; });
                    var torrentProg = new Progress<TorrentDownloadProgress>(p =>
                    {
                        App.RunOnUI(() =>
                        {
                            task.Progress = Math.Clamp(p.Percent, 0, 100);
                            task.Status = p.StatusText;
                            task.Speed = p.SwarmTotal > p.Seeds
                                ? $"{p.SpeedMbPerSec:F2} МБ/с | Сиды: {p.Seeds} (в рое: {p.SwarmTotal})"
                                : $"{p.SpeedMbPerSec:F2} МБ/с | Сиды: {p.Seeds}";
                        });
                        onTorrentProgress?.Invoke(p);
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
                        task.PauseEvent,
                        mgr => task.ActiveTorrentManager = mgr).ConfigureAwait(false);
                }

                // 2. Скачивание и автораспаковка всех облачных модов / русификаторов
                var discoveredModPayloads = new List<string>();
                if (payload.CloudLinks.Count > 0)
                {
                    App.RunOnUI(() => { task.Status = "Скачивание облачных модов..."; });
                    var modProg = new Progress<(double Percent, string Status)>(mp =>
                    {
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
                        onModProgress?.Invoke(mp);
                    });

                    var extractedPaths = await service.DownloadAndExtractCloudModsAsync(
                        payload.CloudLinks,
                        payload.TargetReleaseDir,
                        modProg,
                        ct,
                        task.PauseEvent).ConfigureAwait(false);

                    foreach (var ep in extractedPaths)
                    {
                        if (!discoveredModPayloads.Contains(ep, StringComparer.OrdinalIgnoreCase))
                            discoveredModPayloads.Add(ep);
                    }
                }

                // Автоматически распаковываем ТОЛЬКО ВЫБРАННЫЕ архивы модов/русификаторов (.7z / .zip / .rar), скачанные внутри самой раздачи (напр. atmosphere.7z)
                var allowedSelectedRelPaths = payload.TorrentFiles != null
                    ? payload.TorrentFiles.Where(f => f.IsSelected).SelectMany(f => f.GetEffectiveRelativePaths()).ToList()
                    : new List<string>();

                await ExtractLocalTorrentModArchivesAsync(payload.TargetReleaseDir, ct, allowedSelectedRelPaths).ConfigureAwait(false);

                // Сканируем каталог релиза на наличие распакованных romfs / exefs ТОЛЬКО если выбрана соответствующая запись мода
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

                // Собираем контейнеры и строго фильтруем по выбранным файлам этого слота
                List<string> candidateContainers;
                if (downloadedTorrentFiles != null && downloadedTorrentFiles.Count > 0)
                {
                    candidateContainers = downloadedTorrentFiles;
                }
                else if (Directory.Exists(payload.TargetReleaseDir))
                {
                    candidateContainers = Directory.EnumerateFiles(payload.TargetReleaseDir, "*.*", SearchOption.AllDirectories)
                        .Where(p => p.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) ||
                                    p.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) ||
                                    p.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) ||
                                    p.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase) ||
                                    p.EndsWith(".nro", StringComparison.OrdinalIgnoreCase))
                        .ToList();
                }
                else
                {
                    candidateContainers = new List<string>();
                }

                var matchedContainers = FilterDownloadedContainersForSlot(
                    candidateContainers,
                    payload.TorrentFiles,
                    payload.SlotNumber,
                    payload.SlotGameTitle,
                    new List<string> { payload.SlotGameTitle });

                // 3. Привязка файлов к задаче (на UI-потоке во избежание RPC_E_WRONG_THREAD)
                var bindTcs = new TaskCompletionSource<bool>();
                App.RunOnUI(() =>
                {
                    try
                    {
                        BindDownloadedFilesToExistingTask(task, payload.TargetReleaseDir, discoveredModPayloads, payload.CustomModTitle, matchedContainers);
                    }
                    finally
                    {
                        bindTcs.TrySetResult(true);
                    }
                });
                await bindTcs.Task.ConfigureAwait(false);

                // 4. Автоматическая сборка мульти-контента (как в Android-версии)
                if (payload.AutoBuild && task.InputFiles.Count > 0)
                {
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

                    // Если иконка ещё не была извлечена (например, из сжатого .nsz до распаковки), извлекаем её из собранного контейнера
                    if (task.GameIcon == null)
                    {
                        string builtContainer = Path.Combine(outDir, task.OutputFileName + outExt);
                        if (File.Exists(builtContainer))
                        {
                            TryExtractAndSetTaskIcon(task, new[] { builtContainer });
                        }
                    }

                    bool buildSucceeded = task.Status != "Ошибка" && task.Status != "Отменено";
                    task.EnsureFinalMetricsPopulated(outPath);

                    if (task.DeleteSourceAfterVerify && buildSucceeded)
                    {
                        CleanupRuTrackerReleaseDirectory(payload.TargetReleaseDir, outDir);
                        if (!string.IsNullOrWhiteSpace(payload.SharedReleaseDir) &&
                            !string.Equals(payload.SharedReleaseDir, payload.TargetReleaseDir, StringComparison.OrdinalIgnoreCase))
                        {
                            bool anySiblingStillPending = viewModel.Tasks.Any(other =>
                                other != task &&
                                other.RuTrackerPayload != null &&
                                string.Equals(other.RuTrackerPayload.SharedReleaseDir, payload.SharedReleaseDir, StringComparison.OrdinalIgnoreCase) &&
                                (other.IsRunning || other.Status == "Ожидание" || other.Status.Contains("Подключение") || other.Status.Contains("Загрузка") || other.Status.Contains("Сборка")));
                            if (!anySiblingStillPending)
                            {
                                CleanupRuTrackerReleaseDirectory(payload.SharedReleaseDir, outDir);
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
                        viewModel.SavePersistedTasks();
                    });
                }
                else
                {
                    // Если автосборка отключена пользователем - переводим задачу в статус «Ожидание», чтобы её можно было запустить из очереди задач
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
            }
            catch (OperationCanceledException)
            {
                App.RunOnUI(() =>
                {
                    task.Status = "Отменено";
                    task.IsRunning = false;
                    task.IsPaused = false;
                    task.Speed = string.Empty;
                });
            }
            catch (Exception ex)
            {
                App.RunOnUI(() =>
                {
                    task.Status = "Ошибка";
                    task.IsRunning = false;
                    task.IsPaused = false;
                    task.Speed = string.Empty;
                    task.LogDetails += $"\n❌ Ошибка: {ex.Message}";
                });
                App.Logger?.Log($"[RuTracker Task Pipeline] Ошибка: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                task.HasActiveWorker = false;
                viewModel.SavePersistedTasks();
            }
        }

        public static void BindDownloadedFilesToExistingTask(
            ProcessingTask task,
            string downloadDir,
            List<string> modPayloads,
            string customModTitle,
            IReadOnlyCollection<string>? allowedContainerPaths = null)
        {
            bool addedRomFs = false;
            bool addedExeFs = false;

            foreach (var payload in modPayloads)
            {
                if (!task.InputFiles.Contains(payload, StringComparer.OrdinalIgnoreCase))
                {
                    task.InputFiles.Add(payload);
                    task.FilesList.Add(Path.GetFileName(payload));
                }

                string name = Path.GetFileName(payload).ToLowerInvariant();
                if (name == "romfs" || name.EndsWith(".romfs") || name == "romfs.bin" || name.EndsWith(".pak") || name.EndsWith(".sarc.zs") || name.EndsWith(".sarc") || name.EndsWith(".zs") ||
                    (Directory.Exists(payload) && Directory.Exists(Path.Combine(payload, "romfs"))))
                    addedRomFs = true;
                if (name == "exefs" || name == "exefs_patches" || name.EndsWith(".ips") || name.EndsWith(".pchtxt") || name.EndsWith(".bps") ||
                    (Directory.Exists(payload) && (Directory.Exists(Path.Combine(payload, "exefs")) || Directory.Exists(Path.Combine(payload, "exefs_patches")))))
                    addedExeFs = true;
            }

            if (Directory.Exists(downloadDir))
            {
                var discoveredFiles = Directory.EnumerateFiles(downloadDir, "*.*", SearchOption.AllDirectories)
                    .Where(p => p.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) ||
                                p.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) ||
                                p.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) ||
                                p.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase) ||
                                p.EndsWith(".nup", StringComparison.OrdinalIgnoreCase) ||
                                p.EndsWith(".nro", StringComparison.OrdinalIgnoreCase) ||
                                p.EndsWith(".3ds", StringComparison.OrdinalIgnoreCase) ||
                                p.EndsWith(".cia", StringComparison.OrdinalIgnoreCase) ||
                                p.EndsWith(".cci", StringComparison.OrdinalIgnoreCase) ||
                                p.EndsWith(".cxi", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                HashSet<string>? allowedNames = null;
                HashSet<string>? allowedFull = null;
                if (allowedContainerPaths != null && allowedContainerPaths.Count > 0)
                {
                    allowedFull = new HashSet<string>(allowedContainerPaths.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
                    allowedNames = new HashSet<string>(allowedContainerPaths.Select(Path.GetFileName), StringComparer.OrdinalIgnoreCase);
                }

                foreach (var f in discoveredFiles)
                {
                    if (allowedFull != null && allowedNames != null)
                    {
                        string fullF = Path.GetFullPath(f);
                        string nameF = Path.GetFileName(f);
                        if (!allowedFull.Contains(fullF) && !allowedNames.Contains(nameF))
                        {
                            continue; // не выбран пользователем для этой задачи!
                        }
                    }

                    if (!task.InputFiles.Contains(f, StringComparer.OrdinalIgnoreCase))
                    {
                        task.InputFiles.Add(f);
                        task.FilesList.Add(Path.GetFileName(f));
                    }
                }
            }
            task.FilesCount = $"{task.InputFiles.Count} файл(ов)";
            long totalBytes = 0;
            foreach (var f in task.InputFiles)
            {
                try
                {
                    if (File.Exists(f)) totalBytes += new FileInfo(f).Length;
                    else if (Directory.Exists(f))
                    {
                        totalBytes += Directory.EnumerateFiles(f, "*.*", SearchOption.AllDirectories)
                            .Sum(fi => new FileInfo(fi).Length);
                    }
                }
                catch { }
            }
            task.SourceSizeBytes = totalBytes;
            if (totalBytes > 0)
                task.SourceSize = ProcessingTask.FormatSize(totalBytes);

            if (task.InputFiles.Count > 0)
            {
                var distinctDirs = task.InputFiles
                    .Select(p => Directory.Exists(p) ? p : (Path.GetDirectoryName(p) ?? ""))
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase);
                task.InputFolders = string.Join("; ", distinctDirs);
            }
            else if (!string.IsNullOrWhiteSpace(downloadDir))
            {
                task.InputFolders = downloadDir;
            }
            task.CompressionLevel = ProcessingTask.ComputeDefaultCompressionLevel(task.TargetFormat, task.IsSwitch2Task);

            if (addedRomFs) task.HasRomFs = "Да";
            if (addedExeFs) task.HasExeFs = "Да";

            if (!string.IsNullOrWhiteSpace(customModTitle))
            {
                if (task.HasRomFs == "Да") task.ModNameRomFs = customModTitle;
                if (task.HasExeFs == "Да") task.ModNameExeFs = customModTitle;
            }

            if (modPayloads.Count > 0 || task.InputFiles.Count > 1 || addedRomFs || addedExeFs)
            {
                if (task.Operation != "Homebrew" && task.Operation != "Verify" && task.Operation != "Unpack")
                {
                    task.Operation = "Multi";
                    task.SourceFormat = "MULTI";
                }
                if (task.RuTrackerPayload != null)
                {
                    if (task.CustomMetadata == null && task.RuTrackerPayload.CustomMetadata != null)
                        task.CustomMetadata = task.RuTrackerPayload.CustomMetadata;

                    if (!string.IsNullOrWhiteSpace(task.RuTrackerPayload.SlotGameTitle))
                    {
                        task.GameName = task.RuTrackerPayload.SlotGameTitle.Trim();
                        if (string.IsNullOrWhiteSpace(task.OutputFileName) || task.KeepExactOutputFileName)
                            task.OutputFileName = SanitizeFileName(task.GameName);
                    }
                }
                if (!task.KeepExactOutputFileName && (task.RuTrackerPayload == null || task.RuTrackerPayload.AppendAutoTags))
                {
                    string formattedName = MultiContentService.FormatOutputFileName(
                        Path.Combine(task.OutputFolder, task.OutputFileName + task.GetPrimaryExtension()), 
                        task.InputFiles);
                    task.OutputFileName = Path.GetFileNameWithoutExtension(formattedName);
                }
            }

            // Извлекаем обложку (GameIcon) и GroupId из скачанных игровых файлов для отображения в колонке «Иконка»
            TryExtractAndSetTaskIcon(task, task.InputFiles);
            if (task.GameIcon == null)
            {
                HistoryService.TryLoadIconForTask(task);
            }
            App.TasksVM?.SavePersistedTasks();
        }

        public static void TryExtractAndSetTaskIcon(ProcessingTask task, IEnumerable<string> candidateFiles)
        {
            try
            {
                byte[]? bestIconBytes = null;
                string bestTitleId = task.TitleId;

                var orderedFiles = candidateFiles
                    .Where(File.Exists)
                    .OrderByDescending(f => f.Contains("[v0]", StringComparison.OrdinalIgnoreCase) || f.Contains("000]", StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(f => new FileInfo(f).Length)
                    .ToList();

                foreach (var file in orderedFiles)
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    try
                    {
                        if (Nintendo3dsService.Is3dsExtension(ext))
                        {
                            var info3ds = App.Nintendo3ds.Parse3dsFile(file);
                            if (string.IsNullOrEmpty(bestTitleId) && !string.IsNullOrEmpty(info3ds.TitleId))
                                bestTitleId = info3ds.TitleId.ToUpperInvariant();
                            if (info3ds.IconBytes != null && info3ds.IconBytes.Length > 0)
                            {
                                bestIconBytes = info3ds.IconBytes;
                                break;
                            }
                        }
                        else if (ext == ".nsp" || ext == ".nsz" || ext == ".xci" || ext == ".xcz")
                        {
                            var info = App.SwitchFormat.ParseNsp(file);
                            if (string.IsNullOrEmpty(bestTitleId) && !string.IsNullOrEmpty(info.TitleId))
                                bestTitleId = info.TitleId.ToUpperInvariant();
                            if (info.IconBytes != null && info.IconBytes.Length > 0)
                            {
                                bestIconBytes = info.IconBytes;
                                break;
                            }
                        }
                    }
                    catch { }

                    if (string.IsNullOrEmpty(bestTitleId))
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(Path.GetFileName(file), "(?i)([0-9a-f]{16})");
                        if (m.Success) bestTitleId = m.Groups[1].Value.ToUpperInvariant();
                    }
                }

                if (!string.IsNullOrEmpty(bestTitleId))
                {
                    task.TitleId = bestTitleId;
                    if (string.IsNullOrEmpty(task.GroupId))
                        task.GroupId = TasksViewModel.GetTitleGroupPrefix(bestTitleId);
                }

                string groupId = !string.IsNullOrEmpty(task.GroupId)
                    ? task.GroupId
                    : TasksViewModel.GetTitleGroupPrefix(bestTitleId);

                if (bestIconBytes != null && bestIconBytes.Length > 64)
                {
                    task.IconBytes = bestIconBytes;
                    HistoryService.SaveTaskIconToCache(task, bestIconBytes);
                }
                if (bestIconBytes != null && bestIconBytes.Length > 0 && !string.IsNullOrEmpty(groupId))
                {
                    try
                    {
                        string safeGroupId = string.Join("_", groupId.Split(Path.GetInvalidFileNameChars()));
                        if (!string.IsNullOrEmpty(safeGroupId))
                        {
                            string iconCachePath = Path.Combine(HistoryService.GetIconsDirectory(), $"{safeGroupId}.png");
                            File.WriteAllBytes(iconCachePath, bestIconBytes);
                        }
                    }
                    catch { }
                }
                else if ((bestIconBytes == null || bestIconBytes.Length == 0) && !string.IsNullOrEmpty(groupId))
                {
                    try
                    {
                        string safeGroupId = string.Join("_", groupId.Split(Path.GetInvalidFileNameChars()));
                        string iconCachePath = Path.Combine(HistoryService.GetIconsDirectory(), $"{safeGroupId}.png");
                        if (File.Exists(iconCachePath))
                            bestIconBytes = File.ReadAllBytes(iconCachePath);
                    }
                    catch { }
                }

                if (task.CustomMetadata != null)
                {
                    if (string.IsNullOrEmpty(task.CustomMetadata.TitleId) || task.CustomMetadata.TitleId == "0100000000000000")
                    {
                        if (!string.IsNullOrEmpty(bestTitleId))
                            task.CustomMetadata.TitleId = bestTitleId;
                    }
                    if ((task.CustomMetadata.OriginalIconBytes == null || task.CustomMetadata.OriginalIconBytes.Length == 0) &&
                        bestIconBytes != null && bestIconBytes.Length > 0)
                    {
                        task.CustomMetadata.OriginalIconBytes = bestIconBytes;
                        if (string.IsNullOrEmpty(task.CustomMetadata.CustomIconPath) && task.CustomMetadata.HasBadges)
                        {
                            int targetSize = task.CustomMetadata.Is3ds ? 48 : 256;
                            task.CustomMetadata.CustomIconBytes = IconBadgeStudioService.RenderIconWithBadges(
                                bestIconBytes, targetSize,
                                task.CustomMetadata.BadgeRus, task.CustomMetadata.Badge60Fps,
                                task.CustomMetadata.BadgeMod, task.CustomMetadata.BadgeAllDlc,
                                task.CustomMetadata.CustomBadgeText, task.CustomMetadata.BadgePosition, outputPng: true);
                        }
                    }
                    if (task.CustomMetadata.CustomIconBytes != null && task.CustomMetadata.CustomIconBytes.Length > 0)
                    {
                        bestIconBytes = task.CustomMetadata.CustomIconBytes;
                    }
                }

                if (bestIconBytes != null && bestIconBytes.Length > 0)
                {
                    byte[] iconData = bestIconBytes;
                    App.RunOnUI(async () =>
                    {
                        try
                        {
                            using var ms = new MemoryStream(iconData);
                            var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                            await bitmap.SetSourceAsync(ms.AsRandomAccessStream());
                            task.GameIcon = bitmap;
                        }
                        catch { }
                    });
                }
            }
            catch { }
        }

        public static void CleanupRuTrackerReleaseDirectory(string? dirToDelete, string? outDir)
        {
            if (string.IsNullOrWhiteSpace(dirToDelete) || !Directory.Exists(dirToDelete))
                return;
            try
            {
                string fullTarget = Path.GetFullPath(dirToDelete).TrimEnd('\\', '/');
                string fullOut = !string.IsNullOrWhiteSpace(outDir)
                    ? Path.GetFullPath(outDir).TrimEnd('\\', '/')
                    : string.Empty;

                if (string.Equals(fullTarget, fullOut, StringComparison.OrdinalIgnoreCase))
                    return;

                Directory.Delete(dirToDelete, recursive: true);
                App.Logger?.Log($"[RuTracker] Временный исходный каталог раздачи удалён: {dirToDelete}", LogLevel.Info);

                try
                {
                    string? parentDir = Path.GetDirectoryName(fullTarget);
                    if (!string.IsNullOrEmpty(parentDir) && Directory.Exists(parentDir) &&
                        !string.Equals(parentDir, fullOut, StringComparison.OrdinalIgnoreCase))
                    {
                        string pName = Path.GetFileName(parentDir);
                        if (pName.Equals("_Staging_", StringComparison.OrdinalIgnoreCase) ||
                            pName.Equals("RuTracker_Staging", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!Directory.EnumerateFileSystemEntries(parentDir).Any())
                            {
                                Directory.Delete(parentDir, false);
                            }
                        }
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                App.Logger?.Log($"[RuTracker] Не удалось удалить временный каталог {dirToDelete}: {ex.Message}", LogLevel.Debug);
            }
        }

        private void UiAggregationTimer_Tick(object? sender, object e)
        {
            if (_activeDownloadRows.Count == 0) return;

            foreach (var r in _activeDownloadRows)
            {
                r.CommitUiUpdates();
            }

            int total = _activeDownloadRows.Count;
            int active = _activeDownloadRows.Count(r => r.IsActive);
            int completed = _activeDownloadRows.Count(r => r.StageBadge == "ГОТОВО");

            StatusText.Text = $"Добавлено на закачку: {total} (активно: {active}, завершено: {completed})";

            if (active > 0)
            {
                double totalSpeedBps = _activeDownloadRows.Where(r => r.IsActive).Sum(r => r.CurrentSpeedBps);
                int totalSeeds = _activeDownloadRows.Where(r => r.IsActive).Sum(r => r.CurrentSeeds);
                int totalPeers = _activeDownloadRows.Where(r => r.IsActive).Sum(r => r.CurrentPeers);
                double totalSpeedMb = totalSpeedBps / (1024.0 * 1024.0);

                SpeedPeersText.Text = $"Скорость скачивания: {totalSpeedMb:F2} МБ/с  |  Сиды: {totalSeeds} • Пиры: {totalPeers}";
                StageBadgeText.Text = $"АКТИВНО ({active})";
                StageBadgeBorder.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 2, 132, 199));
            }
            else
            {
                SpeedPeersText.Text = completed > 0 ? "Все загрузки завершены" : "";
                StageBadgeText.Text = completed == total ? "ГОТОВО" : "ЗАВЕРШЕНО";
                StageBadgeBorder.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 34, 197, 94));
            }
        }

        private void CancelSingleBatch_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is RuTrackerActiveDownloadItem item)
            {
                RuTrackerDownloadQueueService.Instance.CancelOrRemoveItem(item);
                LiveTelemetryText.Text = $"⏹ Закачка «{item.DisplayTitle}» отменена.";
            }
        }

        public static void UpdateActiveDownloadTorrentProgress(RuTrackerActiveDownloadItem item, TorrentDownloadProgress p)
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

        public static void UpdateActiveDownloadCloudProgress(RuTrackerActiveDownloadItem item, (double Percent, string Status) mp)
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

        private void UpdateDialogTorrentProgress(TorrentDownloadProgress p, int slotIndex, int totalSlots, string gameTitle)
        {
            App.RunOnUI(() =>
            {
                if (ActiveInstance != this) return;
                bool indeterminate = p.IsMetadataPhase || (p.Percent <= 0.05 && p.DownloadedBytes <= 0);
                DownloadProgressBar.IsIndeterminate = indeterminate;
                if (!indeterminate)
                {
                    DownloadProgressBar.Value = Math.Clamp(p.Percent, 0, 100);
                }

                StageBadgeText.Text = p.IsMetadataPhase
                    ? "ЭТАП 1/4 • МЕТАДАННЫЕ DHT"
                    : $"ЭТАП 2/4 • ТОРРЕНТ ({p.Percent:F1}%)";

                StatusText.Text = p.StatusText;

                if (p.TotalBytes > 0)
                {
                    double dlMb = p.DownloadedBytes / (1024.0 * 1024.0);
                    double totMb = p.TotalBytes / (1024.0 * 1024.0);
                    VolumeProgressText.Text = $"{dlMb:F1} / {totMb:F1} МБ ({p.Percent:F1}%)";
                }
                else
                {
                    VolumeProgressText.Text = p.IsMetadataPhase ? "Поиск пиров..." : $"{p.Percent:F1}%";
                }

                SpeedPeersText.Text = p.SwarmTotal > p.Seeds
                    ? $"{p.SpeedMbPerSec:F2} МБ/с | Сиды: {p.Seeds} (в рое: {p.SwarmTotal}) | Пиры: {p.Peers}"
                    : $"{p.SpeedMbPerSec:F2} МБ/с | Сиды: {p.Seeds} | Пиры: {p.Peers}";

                LiveTelemetryText.Text = p.IsMetadataPhase
                    ? $"🔍 [DHT + HTTP Trackers] Рукопожатие с пирами раздачи «{gameTitle}» (Сидов в рое: {p.SwarmTotal}, подключено: {p.Seeds} сидов / {p.Peers} пиров)..."
                    : $"⬇️ [P2P Stream] Загрузка «{gameTitle}»: {VolumeProgressText.Text} на скорости {p.SpeedMbPerSec:F2} МБ/с (активных пиров: {p.Seeds + p.Peers})";
            });
        }

        private void UpdateDialogCloudProgress((double Percent, string Status) mp, int slotIndex, int totalSlots, string gameTitle)
        {
            App.RunOnUI(() =>
            {
                if (ActiveInstance != this) return;
                DownloadProgressBar.IsIndeterminate = mp.Percent <= 0.5;
                DownloadProgressBar.Value = Math.Clamp(mp.Percent, 0, 100);
                StageBadgeText.Text = totalSlots > 1
                    ? $"ЭТАП 3/4 • МОДЫ ({slotIndex}/{totalSlots})"
                    : $"ЭТАП 3/4 • ОБЛАКО ({mp.Percent:F0}%)";
                StatusText.Text = $"[{gameTitle}] {mp.Status}";
                VolumeProgressText.Text = $"{mp.Percent:F1}%";
                if (mp.Status.Contains("МБ/с"))
                {
                    int sIdx = mp.Status.IndexOf('(');
                    int eIdx = mp.Status.IndexOf(')');
                    if (sIdx >= 0 && eIdx > sIdx)
                        SpeedPeersText.Text = mp.Status.Substring(sIdx + 1, eIdx - sIdx - 1);
                }
                LiveTelemetryText.Text = $"☁️ [Cloud Mod Loader] Часть #{slotIndex} «{gameTitle}»: {mp.Status}";
            });
        }

        private async Task ExecuteMultiGameRuTrackerPipelineAsync(
            List<(ProcessingTask Task, RuTrackerTaskPayload Payload, RuTrackerGameSlotItem Slot)> createdTasks,
            string sharedReleaseDir,
            bool hasTorrentSource,
            RuTrackerTopicItem? currentTopic,
            RuTrackerModService service,
            CancellationToken ct,
            RuTrackerActiveDownloadItem? activeRowItem = null)
        {
            var firstTask = createdTasks[0].Task;
            var unionFiles = new List<TorrentFileSelectionItem>();

            // 1. Скачиваем объединенный набор выбранных файлов торрента один раз в общую папку раздачи
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
                    if (activeRowItem != null)
                        UpdateActiveDownloadTorrentProgress(activeRowItem, p);
                    else
                        UpdateDialogTorrentProgress(p, 1, createdTasks.Count, _currentTopicTitle);
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
                    firstTask.PauseEvent,
                    mgr =>
                    {
                        foreach (var (t, _, _) in createdTasks)
                            t.ActiveTorrentManager = mgr;
                    }).ConfigureAwait(false);
            }

            // Распаковываем ТОЛЬКО ВЫБРАННЫЕ архивы модов/русификаторов (.7z / .zip / .rar), скачанные внутри раздачи (напр. atmosphere.7z)
            var unionSelectedRelPaths = unionFiles
                .Where(f => f.IsSelected)
                .SelectMany(f => f.GetEffectiveRelativePaths())
                .ToList();
            await ExtractLocalTorrentModArchivesAsync(sharedReleaseDir, ct, unionSelectedRelPaths).ConfigureAwait(false);

            // Собираем все скачанные игровые контейнеры в общей папке и раскладываем их по слотам игр (1..N)
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

            // 2. Для каждой отдельной части/игры качаем её собственные ссылки (русификаторы/патчи/DLC) и собираем её задачу
            for (int i = 0; i < createdTasks.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var (task, payload, slot) = createdTasks[i];
                int slotNum = slot.SlotNumber;

                // Определяем файлы контейнеров, относящиеся именно к этой части игры
                var matchedContainers = FilterDownloadedContainersForSlot(
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
                        try
                        {
                            File.Move(srcContainer, dstFile);
                        }
                        catch
                        {
                            try { File.Copy(srcContainer, dstFile, true); } catch { }
                        }
                    }
                }

                // Запускаем стадию облачных модов и сборки для конкретной задачи (HasTorrentSource = false, т.к. торрент уже скачан)
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

                int capturedIdx = i + 1;
                await ExecuteRuTrackerPipelineAsync(
                    task,
                    slotExecPayload,
                    _viewModel,
                    service,
                    null,
                    mp =>
                    {
                        if (activeRowItem != null)
                            UpdateActiveDownloadCloudProgress(activeRowItem, mp);
                        else
                            UpdateDialogCloudProgress(mp, capturedIdx, createdTasks.Count, payload.SlotGameTitle);
                    }).ConfigureAwait(false);
            }

            // По завершении всех задач многосоставной раздачи гарантированно удаляем общую папку раздачи (напр. [Nintendo Switch] Aggelos 1-2 [NSP][RUS_Multi10])
            if (createdTasks.Any(c => c.Task.DeleteSourceAfterVerify) &&
                createdTasks.All(c => c.Task.Status == "Успешно" || c.Task.Status == "Готово"))
            {
                string primaryOutDir = createdTasks[0].Task.OutputFolder;
                CleanupRuTrackerReleaseDirectory(sharedReleaseDir, primaryOutDir);
            }
        }

        public static List<string> FilterDownloadedContainersForSlot(
            List<string> allContainers,
            List<TorrentFileSelectionItem> slotFileSelections,
            int slotNumber,
            string slotGameTitle,
            List<string> allSlotTitles)
        {
            if (allContainers == null || allContainers.Count == 0)
                return new List<string>();

            var selectedItems = (slotFileSelections ?? new List<TorrentFileSelectionItem>())
                .Where(f => f.IsSelected && (f.TargetGameSlot == slotNumber || (allSlotTitles.Count <= 1 && f.TargetGameSlot <= 1)))
                .ToList();

            bool hasRealTorrentFiles = selectedItems.Any(f => f.IsRealTorrentFile);

            if (hasRealTorrentFiles)
            {
                var allowedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var allowedRelPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var f in selectedItems)
                {
                    foreach (var effPath in f.GetEffectiveRelativePaths())
                    {
                        if (!string.IsNullOrEmpty(effPath))
                        {
                            string normRel = effPath.Replace('/', '\\').Trim('\\');
                            allowedRelPaths.Add(normRel);
                            string fn = Path.GetFileName(normRel);
                            if (!string.IsNullOrEmpty(fn))
                                allowedFileNames.Add(fn);
                        }
                    }
                }

                var matched = new List<string>();
                foreach (var c in allContainers)
                {
                    string normContainer = c.Replace('/', '\\').Trim('\\');
                    string containerName = Path.GetFileName(normContainer);

                    bool isMatch = allowedRelPaths.Any(rel => normContainer.EndsWith(rel, StringComparison.OrdinalIgnoreCase)) ||
                                   allowedFileNames.Contains(containerName);

                    if (isMatch)
                        matched.Add(c);
                }

                return matched;
            }
            else
            {
                var tempItems = allContainers.Select(c => new TorrentFileSelectionItem
                {
                    IsSelected = true,
                    RelativePath = Path.GetFileName(c),
                    IsRealTorrentFile = true
                }).ToList();

                RuTrackerModService.AutoAssignFilesToGameSlots(tempItems, allSlotTitles);
                var matched = new List<string>();
                for (int i = 0; i < allContainers.Count; i++)
                {
                    if (tempItems[i].TargetGameSlot == slotNumber || (allSlotTitles.Count <= 1 && tempItems[i].TargetGameSlot <= 1))
                        matched.Add(allContainers[i]);
                }

                return matched;
            }
        }

        private void SetUiBusy(bool busy)
        {
            StartFullDownloadBtn.IsEnabled = true;
            DownloadOnlyModsBtn.IsEnabled = true;
            SearchBtn.IsEnabled = true;
            CancelDownloadBtn.IsEnabled = true;
        }

        private static string SanitizeFolderName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            name = name.Trim().Trim('.');
            if (name.Length > 75)
                name = name.Substring(0, 75).Trim();
            return string.IsNullOrWhiteSpace(name) ? "Release" : name;
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            name = name.Trim().Trim('.');
            if (name.Length > 200)
                name = name.Substring(0, 200).Trim();
            return string.IsNullOrWhiteSpace(name) ? "Game" : name;
        }

        private readonly TaskCompletionSource<bool> _visualTreeReadyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly SemaphoreSlim _webViewInitGate = new(1, 1);
        private bool _webViewConfigured = false;
        private bool _warmupStartedInDialog = false;
        private static Microsoft.Web.WebView2.Core.CoreWebView2Environment? _sharedWebViewEnv;

        private void Dialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            _visualTreeReadyTcs.TrySetResult(true);
            UpdateDialogAdaptiveSize();

            int initialQueue = RuTrackerDownloadQueueService.Instance.Items.Count;
            _lastObservedQueueCount = initialQueue;
            if (initialQueue > 0)
            {
                DownloadProgressBar.Visibility = Visibility.Collapsed;
            }
            SyncQueueVisibilityState();

            if (_topics.Count == 0)
            {
                RefreshInstantTopics(SearchOrUrlBox?.Text ?? string.Empty, autoSelectFirst: _targetTask != null);
            }

            if (!_warmupStartedInDialog)
            {
                _warmupStartedInDialog = true;
                _ = InitBackgroundWebViewAsync();
            }
        }

        private void CloudflareWebView_Loaded(object sender, RoutedEventArgs e)
        {
            _visualTreeReadyTcs.TrySetResult(true);
            if (!_warmupStartedInDialog)
            {
                _warmupStartedInDialog = true;
                _ = InitBackgroundWebViewAsync();
            }
        }

        private async Task<bool> EnsureWebViewInitializedAsync(CancellationToken ct = default)
        {
            if (CloudflareWebView?.CoreWebView2 != null && _webViewConfigured)
                return true;

            // Ждём загрузки диалога в визуальное дерево WinUI 3 (до 3 секунд)
            await Task.WhenAny(_visualTreeReadyTcs.Task, Task.Delay(3000, ct)).ConfigureAwait(false);

            await _webViewInitGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                App.RunOnUI(async () =>
                {
                    try
                    {
                        if (CloudflareWebView == null)
                        {
                            tcs.TrySetResult(false);
                            return;
                        }

                        if (CloudflareWebView.CoreWebView2 == null)
                        {
                            try
                            {
                                if (_sharedWebViewEnv == null)
                                {
                                    string userDataDir = Path.Combine(
                                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                        "STORM SWITCH BOX",
                                        "WebView2");
                                    Directory.CreateDirectory(userDataDir);
                                    _sharedWebViewEnv = await Microsoft.Web.WebView2.Core.CoreWebView2Environment.CreateWithOptionsAsync(
                                        null, userDataDir, null);
                                }
                                await CloudflareWebView.EnsureCoreWebView2Async(_sharedWebViewEnv);
                            }
                            catch
                            {
                                try { await CloudflareWebView.EnsureCoreWebView2Async(); } catch { }
                            }
                        }

                        if (CloudflareWebView.CoreWebView2 == null)
                        {
                            await Task.Delay(250);
                            try { await CloudflareWebView.EnsureCoreWebView2Async(); } catch { }
                        }

                        var coreWv = CloudflareWebView.CoreWebView2;
                        if (coreWv == null)
                        {
                            tcs.TrySetResult(false);
                            return;
                        }

                        if (!_webViewConfigured)
                        {
                            coreWv.Settings.IsStatusBarEnabled = false;
                            coreWv.Settings.AreDevToolsEnabled = false;

                            string currentUa = App.Settings.Current.RuTrackerUserAgent;
                            if (!string.IsNullOrWhiteSpace(currentUa))
                            {
                                coreWv.Settings.UserAgent = currentUa;
                                RuTrackerModService.SharedUserAgent = currentUa;
                            }
                            else
                            {
                                string rawUa = await CloudflareWebView.ExecuteScriptAsync("navigator.userAgent");
                                string cleanUa = (rawUa ?? "").Trim('"');
                                if (!string.IsNullOrEmpty(cleanUa) && cleanUa != "null")
                                {
                                    coreWv.Settings.UserAgent = cleanUa;
                                    RuTrackerModService.SharedUserAgent = cleanUa;
                                    App.Settings.Current.RuTrackerUserAgent = cleanUa;
                                    _ = App.Settings.SaveAsync();
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(App.Settings.Current.RuTrackerCfClearance))
                            {
                                RuTrackerModService.SharedCfClearance = App.Settings.Current.RuTrackerCfClearance;
                                try
                                {
                                    var cfCookie = coreWv.CookieManager.CreateCookie(
                                        "cf_clearance", App.Settings.Current.RuTrackerCfClearance, ".rutracker.org", "/");
                                    coreWv.CookieManager.AddOrUpdateCookie(cfCookie);
                                }
                                catch { }
                            }

                            if (!string.IsNullOrWhiteSpace(App.Settings.Current.RuTrackerCookie))
                            {
                                try
                                {
                                    var bbCookie = coreWv.CookieManager.CreateCookie(
                                        "bb_session", App.Settings.Current.RuTrackerCookie, ".rutracker.org", "/");
                                    coreWv.CookieManager.AddOrUpdateCookie(bbCookie);
                                }
                                catch { }
                            }

                            _webViewConfigured = true;
                        }

                        tcs.TrySetResult(true);
                    }
                    catch
                    {
                        tcs.TrySetResult(false);
                    }
                });

                using var reg = ct.Register(() => tcs.TrySetResult(false));
                return await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                _webViewInitGate.Release();
            }
        }

        private async Task InitBackgroundWebViewAsync()
        {
            try
            {
                bool ready = await EnsureWebViewInitializedAsync().ConfigureAwait(false);
                if (!ready) return;

                string mirror = RuTrackerModService.NormalizeMirrorUrl(App.Settings.Current.RuTrackerMirror);
                string responsiveMirror = await RuTrackerModService.ProbeAndGetActiveMirrorAsync(mirror).ConfigureAwait(false);
                App.Settings.Current.RuTrackerMirror = responsiveMirror.Replace("https://", "").Replace("http://", "");
                _ = App.Settings.SaveAsync();

                App.RunOnUI(() =>
                {
                    try
                    {
                        if (CloudflareWebView?.CoreWebView2 == null) return;
                        string warmupUrl = $"{responsiveMirror}/forum/viewforum.php?f=1605";
                        CloudflareWebView.NavigationCompleted -= OnWarmupNavigationCompleted;
                        CloudflareWebView.NavigationCompleted += OnWarmupNavigationCompleted;
                        CloudflareWebView.Source = new Uri(warmupUrl);
                    }
                    catch { }
                });
            }
            catch { }
        }

        private int _warmupNavCount = 0;
        private async void OnWarmupNavigationCompleted(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            try
            {
                var coreWv = CloudflareWebView?.CoreWebView2;
                if (coreWv == null) return;

                _warmupNavCount++;
                string currentUrl = CloudflareWebView?.Source?.ToString() ?? "";
                if (string.IsNullOrEmpty(currentUrl)) return;

                var cookieList = await coreWv.CookieManager.GetCookiesAsync(currentUrl);
                var cfCookie = cookieList.FirstOrDefault(c => c.Name == "cf_clearance");
                if (cfCookie != null)
                {
                    RuTrackerModService.SharedCfClearance = cfCookie.Value;
                    App.Settings.Current.RuTrackerCfClearance = cfCookie.Value;
                    _ = App.Settings.SaveAsync();
                }

                string htmlRaw = await CloudflareWebView!.ExecuteScriptAsync("document.documentElement.outerHTML");
                string html = System.Text.RegularExpressions.Regex.Unescape((htmlRaw ?? "").Trim('"'));

                bool hasForumTopics = html.Contains("viewtopic.php?t=");
                if (hasForumTopics)
                {
                    RuTrackerModService.ParseAndMergeForumTopicsHtml(html);
                    _ = Task.Run(() => RuTrackerModService.ResolveUnnamedTopicsViaSameOriginAsync());
                }

                if (hasForumTopics || (cfCookie != null && !html.Contains("Just a moment...") && !html.Contains("cf-turnstile")) || _warmupNavCount >= 6)
                {
                    CloudflareWebView.NavigationCompleted -= OnWarmupNavigationCompleted;
                }
            }
            catch { }
        }

        private void ShowCloudflareBypassUi(string message)
        {
            App.RunOnUI(() =>
            {
                CloudflareStatusText.Text = message;
                CloudflareBypassPanel.Width = 360;
                CloudflareBypassPanel.Height = 260;
                CloudflareBypassPanel.Margin = new Thickness(0, 0, 16, 16);
                CloudflareBypassPanel.Padding = new Thickness(10);
                CloudflareBypassPanel.BorderThickness = new Thickness(1.5);
                CloudflareBypassPanel.Opacity = 1.0;
                CloudflareBypassPanel.IsHitTestVisible = true;
            });
        }

        private void HideCloudflareBypassUi()
        {
            App.RunOnUI(() =>
            {
                CloudflareBypassPanel.Width = 1;
                CloudflareBypassPanel.Height = 1;
                CloudflareBypassPanel.Margin = new Thickness(0);
                CloudflareBypassPanel.Padding = new Thickness(0);
                CloudflareBypassPanel.BorderThickness = new Thickness(0);
                CloudflareBypassPanel.Opacity = 0.0;
                CloudflareBypassPanel.IsHitTestVisible = false;
            });
        }

        private void CloseCloudflareBypass_Click(object sender, RoutedEventArgs e)
        {
            HideCloudflareBypassUi();
            try { _cfTcs?.TrySetResult(null); } catch { }
        }

        private async Task<string?> FetchViaSameOriginInDialogAsync(string url, CancellationToken ct)
        {
            bool ready = await EnsureWebViewInitializedAsync(ct).ConfigureAwait(false);
            if (!ready) return null;

            var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            App.RunOnUI(async () =>
            {
                try
                {
                    var coreWv = CloudflareWebView?.CoreWebView2;
                    if (coreWv == null)
                    {
                        tcs.TrySetResult(null);
                        return;
                    }

                    string escapedUrl = url.Replace("'", "\\'");
                    string jsCode = "(async () => {" +
                        " try {" +
                        "   if (!location.hostname.includes('rutracker.')) return 'NEED_NAV';" +
                        $"   const parsed = new URL('{escapedUrl}', location.origin);" +
                        "   const sameUrl = location.origin + parsed.pathname + parsed.search;" +
                        "   const resp = await fetch(sameUrl, { credentials: 'include' });" +
                        "   if (!resp.ok) return 'HTTP_STATUS:' + resp.status;" +
                        "   const buf = await resp.arrayBuffer();" +
                        "   const decoder = new TextDecoder('windows-1251');" +
                        "   return decoder.decode(buf);" +
                        " } catch (e) {" +
                        "   return 'FETCH_ERR:' + e.message;" +
                        " }" +
                        "})()";

                    var evalParams = new
                    {
                        expression = jsCode,
                        awaitPromise = true,
                        returnByValue = true
                    };
                    string evalParamsJson = System.Text.Json.JsonSerializer.Serialize(evalParams);
                    string cdpResultJson = await coreWv.CallDevToolsProtocolMethodAsync("Runtime.evaluate", evalParamsJson);

                    string fetchedText = "";
                    if (!string.IsNullOrEmpty(cdpResultJson))
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(cdpResultJson);
                        if (doc.RootElement.TryGetProperty("result", out var resObj) &&
                            resObj.TryGetProperty("value", out var valProp) &&
                            valProp.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            fetchedText = valProp.GetString() ?? "";
                        }
                    }

                    if (!string.IsNullOrEmpty(fetchedText) &&
                        !fetchedText.Equals("NEED_NAV", StringComparison.OrdinalIgnoreCase) &&
                        !fetchedText.StartsWith("HTTP_STATUS:") &&
                        !fetchedText.StartsWith("FETCH_ERR:") &&
                        !fetchedText.Contains("Just a moment...") &&
                        !fetchedText.Contains("cf-turnstile"))
                    {
                        tcs.TrySetResult(fetchedText);
                        return;
                    }

                    string tid = "";
                    var tm = System.Text.RegularExpressions.Regex.Match(url, @"t=(\d+)");
                    if (tm.Success) tid = tm.Groups[1].Value;

                    var solvedHtml = await SolveCloudflareChallengeInDialogAsync(url, tid, ct);
                    tcs.TrySetResult(solvedHtml);
                }
                catch
                {
                    tcs.TrySetResult(null);
                }
            });

            using var reg = ct.Register(() => tcs.TrySetCanceled());
            return await tcs.Task.ConfigureAwait(false);
        }

        private async Task<string?> SolveCloudflareChallengeInDialogAsync(string url, string topicId, CancellationToken ct)
        {
            bool ready = await EnsureWebViewInitializedAsync(ct).ConfigureAwait(false);
            if (!ready) return null;

            var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _cfTcs = tcs;

            App.RunOnUI(async () =>
            {
                try
                {
                    var coreWv = CloudflareWebView?.CoreWebView2;
                    if (coreWv == null)
                    {
                        tcs.TrySetResult(null);
                        return;
                    }
                    CloudflareStatusText.Text = $"Подключение к теме #{topicId}... Проверка Cloudflare...";
                    var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                    var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
                    linkedCts.Token.Register(() =>
                    {
                        HideCloudflareBypassUi();
                        tcs.TrySetResult(null);
                    });

                    _ = Task.Run(async () =>
                    {
                        for (int attempt = 0; attempt < 9 && !tcs.Task.IsCompleted && !linkedCts.Token.IsCancellationRequested; attempt++)
                        {
                            await Task.Delay(2000, linkedCts.Token);
                            if (tcs.Task.IsCompleted) return;

                            App.RunOnUI(async () =>
                            {
                                try
                                {
                                    var wv = CloudflareWebView?.CoreWebView2;
                                    if (wv == null) return;

                                    string checkJs = @"
                                        (function() {
                                            if (document.getElementById('topic-title') || document.querySelector('a[href*=""magnet:?""]')) return 'SOLVED';
                                            const cfIframe = document.querySelector('iframe[src*=""challenges.cloudflare.com""], iframe[src*=""turnstile""], iframe[id*=""cf-chl-widget""]');
                                            if (cfIframe) {
                                                const r = cfIframe.getBoundingClientRect();
                                                if (r.width > 0 && r.height > 0) return 'CLICK:' + Math.round(r.left + 28) + ',' + Math.round(r.top + r.height / 2);
                                            }
                                            const cfDiv = document.querySelector('.cf-turnstile, #challenge-stage, #turnstile-wrapper');
                                            if (cfDiv) {
                                                const r = cfDiv.getBoundingClientRect();
                                                if (r.width > 0 && r.height > 0) return 'CLICK:' + Math.round(r.left + 28) + ',' + Math.round(r.top + r.height / 2);
                                            }
                                            return 'WAIT';
                                        })()";

                                    string resRaw = await CloudflareWebView!.ExecuteScriptAsync(checkJs);
                                    string res = (resRaw ?? "").Trim('"');
                                    if (res == "SOLVED")
                                    {
                                        string htmlRaw = await CloudflareWebView.ExecuteScriptAsync("document.documentElement.outerHTML");
                                        string html = System.Text.RegularExpressions.Regex.Unescape((htmlRaw ?? "").Trim('"'));
                                        HideCloudflareBypassUi();
                                        tcs.TrySetResult(html);
                                        return;
                                    }

                                    if (res.StartsWith("CLICK:"))
                                    {
                                        var parts = res.Substring(6).Split(',');
                                        if (parts.Length == 2 && int.TryParse(parts[0], out int clickX) && int.TryParse(parts[1], out int clickY))
                                        {
                                            string pressParams = System.Text.Json.JsonSerializer.Serialize(new
                                            {
                                                type = "mousePressed",
                                                x = clickX,
                                                y = clickY,
                                                button = "left",
                                                clickCount = 1
                                            });
                                            await wv.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", pressParams);
                                            await Task.Delay(65);
                                            string releaseParams = System.Text.Json.JsonSerializer.Serialize(new
                                            {
                                                type = "mouseReleased",
                                                x = clickX,
                                                y = clickY,
                                                button = "left",
                                                clickCount = 1
                                            });
                                            await wv.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", releaseParams);
                                        }
                                    }
                                }
                                catch { }
                            });
                        }

                        await Task.Delay(2000, linkedCts.Token);
                        if (!tcs.Task.IsCompleted)
                        {
                            App.RunOnUI(() =>
                            {
                                ShowCloudflareBypassUi($"🛡️ Требуется подтверждение капчи Cloudflare для раздачи #{topicId}...");
                            });
                        }
                    }, linkedCts.Token);

                    async void OnNavCompleted(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
                    {
                        try
                        {
                            var wv = CloudflareWebView?.CoreWebView2;
                            if (wv == null) return;

                            var cookieList = await wv.CookieManager.GetCookiesAsync(url);
                            var cfCookie = cookieList.FirstOrDefault(c => c.Name == "cf_clearance");
                            var bbCookie = cookieList.FirstOrDefault(c => c.Name == "bb_session");

                            if (cfCookie != null)
                            {
                                RuTrackerModService.SharedCfClearance = cfCookie.Value;
                                RuTrackerModService.SharedUserAgent = wv.Settings.UserAgent;
                                _ = App.Settings.SaveAsync();
                            }
                            if (bbCookie != null && string.IsNullOrEmpty(App.Settings.Current.RuTrackerCookie))
                            {
                                App.Settings.Current.RuTrackerCookie = bbCookie.Value;
                                _ = App.Settings.SaveAsync();
                            }

                            string htmlRaw = await CloudflareWebView!.ExecuteScriptAsync("document.documentElement.outerHTML");
                            string html = System.Text.RegularExpressions.Regex.Unescape((htmlRaw ?? "").Trim('"'));

                            if (html.Contains("topic-title") || html.Contains("viewtopic.php") || (cfCookie != null && !html.Contains("cf-turnstile") && !html.Contains("Just a moment...")))
                            {
                                CloudflareWebView.NavigationCompleted -= OnNavCompleted;
                                timeoutCts.Cancel();
                                HideCloudflareBypassUi();
                                tcs.TrySetResult(html);
                            }
                        }
                        catch { }
                    }

                    CloudflareWebView!.NavigationCompleted += OnNavCompleted;
                    CloudflareWebView.Source = new Uri(url);
                }
                catch
                {
                    HideCloudflareBypassUi();
                    tcs.TrySetResult(null);
                }
            });

            using var reg = ct.Register(() => tcs.TrySetCanceled());
            return await tcs.Task.ConfigureAwait(false);
        }

        public static async Task ExtractLocalTorrentModArchivesAsync(
            string releaseDir,
            CancellationToken ct,
            IReadOnlyCollection<string>? selectedRelativePaths = null)
        {
            if (string.IsNullOrWhiteSpace(releaseDir) || !Directory.Exists(releaseDir))
                return;

            try
            {
                var archiveFiles = Directory.EnumerateFiles(releaseDir, "*.*", SearchOption.AllDirectories)
                    .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}Extracted_TorrentMod_", StringComparison.OrdinalIgnoreCase) &&
                                (f.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) ||
                                 f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                                 f.EndsWith(".rar", StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                HashSet<string>? allowedArchNames = null;
                if (selectedRelativePaths != null && selectedRelativePaths.Count > 0)
                {
                    allowedArchNames = new HashSet<string>(
                        selectedRelativePaths.Select(p => Path.GetFileName(p.Replace('/', '\\'))),
                        StringComparer.OrdinalIgnoreCase);
                }

                foreach (var arch in archiveFiles)
                {
                    ct.ThrowIfCancellationRequested();
                    string archFileName = Path.GetFileName(arch);
                    if (allowedArchNames != null && !allowedArchNames.Contains(archFileName))
                    {
                        continue; // архив не был выбран пользователем!
                    }

                    string baseName = Path.GetFileNameWithoutExtension(arch);
                    string parentDir = Path.GetDirectoryName(arch) ?? releaseDir;
                    string extractDir = Path.Combine(parentDir, $"Extracted_TorrentMod_{SanitizeFolderName(baseName)}");
                    if (Directory.Exists(extractDir) && Directory.EnumerateFileSystemEntries(extractDir).Any())
                        continue;

                    Directory.CreateDirectory(extractDir);
                    try
                    {
                        await RuTrackerModService.ExtractArchiveWith7zAsync(arch, extractDir, ct).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        App.Logger?.Log($"[RuTracker Mod Unpack] Не удалось распаковать архив из раздачи {Path.GetFileName(arch)}: {ex.Message}", LogLevel.Warning);
                    }
                }
            }
            catch { }
        }

        private async Task<byte[]?> FetchTorrentBytesViaSameOriginInDialogAsync(string topicId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(topicId)) return null;
            bool ready = await EnsureWebViewInitializedAsync(ct).ConfigureAwait(false);
            if (!ready) return null;

            var tcs = new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
            App.RunOnUI(async () =>
            {
                try
                {
                    var coreWv = CloudflareWebView?.CoreWebView2;
                    if (coreWv == null) { tcs.TrySetResult(null); return; }

                    string js = "(async () => {" +
                        " try {" +
                        "   if (!location.hostname.includes('rutracker.')) return '';" +
                        $"   const resp = await fetch(location.origin + '/forum/dl.php?t={topicId}', {{ method: 'POST', credentials: 'include' }});" +
                        "   if (!resp.ok) return '';" +
                        "   const buf = new Uint8Array(await resp.arrayBuffer());" +
                        "   if (buf.length < 64 || buf[0] !== 100) return '';" +
                        "   let bin = '';" +
                        "   for (let i = 0; i < buf.length; i++) bin += String.fromCharCode(buf[i]);" +
                        "   return btoa(bin);" +
                        " } catch (e) { return ''; }" +
                        "})()";

                    string evalJson = System.Text.Json.JsonSerializer.Serialize(new { expression = js, awaitPromise = true, returnByValue = true });
                    string cdpRes = await coreWv.CallDevToolsProtocolMethodAsync("Runtime.evaluate", evalJson);
                    if (!string.IsNullOrEmpty(cdpRes))
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(cdpRes);
                        if (doc.RootElement.TryGetProperty("result", out var rObj) &&
                            rObj.TryGetProperty("value", out var vProp) &&
                            vProp.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            string b64 = vProp.GetString() ?? "";
                            if (!string.IsNullOrEmpty(b64))
                            {
                                tcs.TrySetResult(Convert.FromBase64String(b64));
                                return;
                            }
                        }
                    }
                    tcs.TrySetResult(null);
                }
                catch { tcs.TrySetResult(null); }
            });

            using var reg = ct.Register(() => tcs.TrySetCanceled());
            return await tcs.Task.ConfigureAwait(false);
        }

        private async Task<List<(string Path, long Size)>?> FetchViewTorrentTreeInDialogAsync(string topicId, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(topicId)) return null;
            bool ready = await EnsureWebViewInitializedAsync(ct).ConfigureAwait(false);
            if (!ready) return null;

            // Если WebView2 ещё не находится на домене rutracker.*, сначала открываем страницу темы (проходя Cloudflare при необходимости)
            string currentHost = "";
            var hostTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            App.RunOnUI(() =>
            {
                try { hostTcs.TrySetResult(CloudflareWebView?.Source?.Host ?? ""); }
                catch { hostTcs.TrySetResult(""); }
            });
            currentHost = await hostTcs.Task.ConfigureAwait(false);

            if (!currentHost.Contains("rutracker.", StringComparison.OrdinalIgnoreCase))
            {
                string mirror = RuTrackerModService.GetActiveMirror();
                await SolveCloudflareChallengeInDialogAsync($"{mirror}/forum/viewtopic.php?t={topicId}", topicId, ct).ConfigureAwait(false);
            }

            var tcs = new TaskCompletionSource<List<(string Path, long Size)>?>(TaskCreationOptions.RunContinuationsAsynchronously);
            App.RunOnUI(async () =>
            {
                try
                {
                    var coreWv = CloudflareWebView?.CoreWebView2;
                    if (coreWv == null) { tcs.TrySetResult(null); return; }

                    string js = @"
                    (async () => {
                      try {
                        if (!location.hostname.includes('rutracker.')) return 'NEED_NAV';
                        const resp = await fetch(location.origin + '/forum/viewtorrent.php', {
                          method: 'POST',
                          credentials: 'include',
                          headers: {
                            'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
                            'X-Requested-With': 'XMLHttpRequest'
                          },
                          body: 't=" + topicId + @"' + ((window.BB && window.BB.form_token) ? '&form_token=' + encodeURIComponent(window.BB.form_token) : '')
                        });
                        if (!resp.ok) return 'HTTP:' + resp.status;
                        const buf = await resp.arrayBuffer();
                        let html = new TextDecoder('windows-1251').decode(buf);
                        if (html.includes('charset=utf-8') || html.includes('charset=""utf-8""')) {
                          html = new TextDecoder('utf-8').decode(buf);
                        }
                        if (html.includes('Just a moment...') || html.includes('_cf_chl_opt')) return 'CF_BLOCK';

                        const doc = new DOMParser().parseFromString(html, 'text/html');
                        const rootUl = doc.querySelector('ul.ftree, ul.tree-root, div.tor-filelist-wrap ul, ul');
                        if (!rootUl) return 'NO_UL';

                        const files = [];
                        function parseHumanSize(txt) {
                          if (!txt) return 0;
                          const m = txt.replace(/,/g, '.').match(/([\d.]+)\s*(B|KB|MB|GB|TB|Б|КБ|МБ|ГБ|ТБ)/i);
                          if (!m) return 0;
                          const v = parseFloat(m[1]);
                          const u = m[2].toUpperCase();
                          if (u === 'TB' || u === 'ТБ') return Math.round(v * 1099511627776);
                          if (u === 'GB' || u === 'ГБ') return Math.round(v * 1073741824);
                          if (u === 'MB' || u === 'МБ') return Math.round(v * 1048576);
                          if (u === 'KB' || u === 'КБ') return Math.round(v * 1024);
                          return Math.round(v);
                        }

                        function walkUl(ul, stack) {
                          const items = ul.children;
                          for (let i = 0; i < items.length; i++) {
                            const li = items[i];
                            if (!li || li.tagName !== 'LI') continue;
                            let childUl = null;
                            for (let c = 0; c < li.children.length; c++) {
                              if (li.children[c].tagName === 'UL') { childUl = li.children[c]; break; }
                            }
                            if (childUl) {
                              let dirName = '';
                              const bEl = li.querySelector(':scope > div > b, :scope > b');
                              if (bEl) {
                                dirName = bEl.textContent.trim();
                              } else {
                                const clone = li.cloneNode(true);
                                clone.querySelectorAll('ul, s, i').forEach(n => n.remove());
                                dirName = clone.textContent.trim();
                              }
                              dirName = dirName.replace(/^\.\/+/, '').trim();
                              const nextStack = dirName ? stack.concat([dirName]) : stack;
                              walkUl(childUl, nextStack);
                            } else {
                              let byteSize = 0;
                              const iEl = li.querySelector('i');
                              if (iEl) {
                                const digits = iEl.textContent.replace(/[^\d]/g, '');
                                if (digits) byteSize = parseInt(digits, 10) || 0;
                              }
                              const sEl = li.querySelector('s');
                              if (!byteSize && sEl) {
                                byteSize = parseHumanSize(sEl.textContent);
                              }
                              let fileName = '';
                              const spEl = li.querySelector(':scope > div > span, :scope > span, :scope > div > b, :scope > b');
                              if (spEl) {
                                fileName = spEl.textContent.trim();
                              } else {
                                const clone = li.cloneNode(true);
                                clone.querySelectorAll('s, i, ul').forEach(n => n.remove());
                                fileName = clone.textContent.trim();
                              }
                              fileName = fileName.replace(/^\.\/+/, '').replace(/\s*·\s*$/, '').trim();
                              if (fileName) {
                                const relPath = stack.length > 0 ? stack.concat([fileName]).join('/') : fileName;
                                files.push({ path: relPath, size: byteSize });
                              }
                            }
                          }
                        }

                        walkUl(rootUl, []);
                        return JSON.stringify(files);
                      } catch (e) {
                        return 'ERR:' + e.message;
                      }
                    })()";

                    string evalJson = System.Text.Json.JsonSerializer.Serialize(new { expression = js, awaitPromise = true, returnByValue = true });
                    string cdpRes = await coreWv.CallDevToolsProtocolMethodAsync("Runtime.evaluate", evalJson);
                    if (!string.IsNullOrEmpty(cdpRes))
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(cdpRes);
                        if (doc.RootElement.TryGetProperty("result", out var rObj) &&
                            rObj.TryGetProperty("value", out var vProp) &&
                            vProp.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            string raw = vProp.GetString() ?? "";
                            if (raw.StartsWith("["))
                            {
                                var parsedList = new List<(string Path, long Size)>();
                                using var arrDoc = System.Text.Json.JsonDocument.Parse(raw);
                                foreach (var el in arrDoc.RootElement.EnumerateArray())
                                {
                                    string p = el.TryGetProperty("path", out var pEl) ? (pEl.GetString() ?? "") : "";
                                    long sz = el.TryGetProperty("size", out var sEl) && sEl.TryGetInt64(out long v) ? v : 0L;
                                    if (!string.IsNullOrWhiteSpace(p))
                                        parsedList.Add((p, sz));
                                }
                                if (parsedList.Count > 0)
                                {
                                    tcs.TrySetResult(parsedList);
                                    return;
                                }
                            }
                        }
                    }
                    tcs.TrySetResult(null);
                }
                catch
                {
                    tcs.TrySetResult(null);
                }
            });

            using var reg = ct.Register(() => tcs.TrySetCanceled());
            return await tcs.Task.ConfigureAwait(false);
        }

        private void ApplyLocalization()
        {
            if (DialogHeaderTitleText != null)
                DialogHeaderTitleText.Text = App.Localization.TranslateText("RuTracker и облачный загрузчик игр, патчей и модов");
            if (StartFullDownloadBtnText != null)
            {
                StartFullDownloadBtnText.Text = App.Localization.TranslateText(
                    _targetTask != null
                        ? "Скачать выбранное и привязать к задаче"
                        : "Скачать раздачу с модами и создать задачи");
            }
        }

        private void Dialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            try { _topicSelectionCts?.Cancel(); _topicSelectionCts?.Dispose(); _topicSelectionCts = null; } catch { }
            _instantSearchDebounceTimer?.Stop();
            _uiAggregationTimer?.Stop();
            if (this.XamlRoot != null)
            {
                this.XamlRoot.Changed -= XamlRoot_Changed;
            }
            App.Localization.UnregisterRoot(this);
            if (_langHandler != null) App.Localization.LanguageChanged -= _langHandler;
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
                RuTrackerModService.CloudflareChallengeHandler = null;
                RuTrackerModService.SameOriginFetchHandler = null;
                RuTrackerModService.SameOriginViewTorrentHandler = null;
                RuTrackerModService.SameOriginTorrentBytesHandler = null;
            }
            try { _cfTcs?.TrySetResult(null); } catch { }
            // ВАЖНО: Не отменяем _downloadCts и не глушим сервис!
            // Задача продолжает выполняться в главном задачнике TasksViewModel.Tasks!
        }

        public void UpdateHeaderSummary(int inList, int downloading, int inQueue, int finished, double totalSpeedBps, int maxSeeds, int maxPeers)
        {
            if (_lastObservedQueueCount == 0 && inList > 0)
            {
                _isQueuePanelExpanded = true;
            }
            _lastObservedQueueCount = inList;

            DownloadProgressBar.Visibility = Visibility.Collapsed;
            SyncQueueVisibilityState();

            if (ModeDownloadsBtnText != null)
            {
                ModeDownloadsBtnText.Text = inList > 0 ? $"Закачки ({inList})" : "Закачки";
            }

            if (downloading > 0)
            {
                StageBadgeText.Text = App.Localization.TranslateText($"АКТИВНО ({downloading})");
                StageBadgeBorder.Background = BrushStageActive;
            }
            else if (inQueue > 0)
            {
                StageBadgeText.Text = App.Localization.TranslateText($"В ОЧЕРЕДИ ({inQueue})");
                StageBadgeBorder.Background = BrushStageQueue;
            }
            else if (finished > 0)
            {
                StageBadgeText.Text = App.Localization.TranslateText("ГОТОВО");
                StageBadgeBorder.Background = BrushStageDone;
            }
            else
            {
                StageBadgeText.Text = App.Localization.TranslateText("ГОТОВ К РАБОТЕ");
                StageBadgeBorder.Background = BrushStageActive;
            }

            if (QueueSummaryText != null)
            {
                QueueSummaryText.Text = inList > 0
                    ? App.Localization.TranslateText($"В списке: {inList} • Качается: {downloading} • В очереди: {inQueue} • Готово: {finished}")
                    : App.Localization.TranslateText("Активных закачек нет");
            }

            if (totalSpeedBps > 1024)
            {
                SpeedPeersText.Text = App.Localization.TranslateText($"Скорость: {totalSpeedBps / (1024.0 * 1024.0):F2} МБ/с | Сиды: {maxSeeds} • Пиры: {maxPeers}");
            }
            else if (downloading > 0)
            {
                SpeedPeersText.Text = App.Localization.TranslateText($"Поиск пиров DHT/P2P... | Сиды: {maxSeeds}");
            }
            else
            {
                SpeedPeersText.Text = string.Empty;
            }
        }

        private void TogglePauseQueueItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is RuTrackerActiveDownloadItem item)
            {
                RuTrackerDownloadQueueService.Instance.TogglePauseItem(item);
            }
        }

        private void MoveQueueItemUp_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is RuTrackerActiveDownloadItem item)
            {
                RuTrackerDownloadQueueService.Instance.MoveItemUp(item);
            }
        }

        private void MoveQueueItemDown_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is RuTrackerActiveDownloadItem item)
            {
                RuTrackerDownloadQueueService.Instance.MoveItemDown(item);
            }
        }

        private void PauseAllDownloads_Click(object sender, RoutedEventArgs e)
        {
            RuTrackerDownloadQueueService.Instance.PauseAll();
        }

        private void ResumeAllDownloads_Click(object sender, RoutedEventArgs e)
        {
            RuTrackerDownloadQueueService.Instance.ResumeAll();
        }

        private void ClearFinishedDownloads_Click(object sender, RoutedEventArgs e)
        {
            RuTrackerDownloadQueueService.Instance.ClearFinished();
        }

    }
}
