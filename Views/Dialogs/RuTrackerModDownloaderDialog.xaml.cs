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

        private readonly ObservableCollection<RuTrackerTopicItem> _topics = new();
        private readonly ObservableCollection<RuTrackerGameSlotItem> _gameSlots = new();
        private readonly ObservableCollection<TorrentFileSelectionItem> _torrentFiles = new();
        private readonly ObservableCollection<CloudModLinkItem> _discussionLinks = new();
        private readonly ObservableCollection<RuTrackerFileGroupBlock> _fileGroups = new();
        private readonly ObservableCollection<RuTrackerLinkGroupBlock> _linkGroups = new();
        private bool _suppressGroupRebuild;

        private string? _localTorrentPath;
        private string _currentTopicTitle = string.Empty;
        private string _currentTopicUrl = string.Empty;
        private CancellationTokenSource? _downloadCts;

        public RuTrackerModDownloaderDialog(TasksViewModel viewModel, ProcessingTask? targetTask = null)
        {
            this.InitializeComponent();
            this.XamlRoot = App.MainWindow.Content.XamlRoot;
            this.RequestedTheme = (App.MainWindow.Content as FrameworkElement)?.RequestedTheme ?? ElementTheme.Dark;

            _viewModel = viewModel;
            _targetTask = targetTask;

            TopicsListView.ItemsSource = _topics;
            GameSlotsListView.ItemsSource = _gameSlots;
            TorrentFilesListView.ItemsSource = _fileGroups;
            DiscussionLinksListView.ItemsSource = _linkGroups;
            InitializeDefaultGameSlot("Игра 1");

            LoadSavedSettings();
            ActiveInstance = this;
            RuTrackerModService.CloudflareChallengeHandler = SolveCloudflareChallengeInDialogAsync;
            RuTrackerModService.SameOriginFetchHandler = FetchViaSameOriginInDialogAsync;
            this.Opened += Dialog_Opened;
            CloudflareWebView.Loaded += CloudflareWebView_Loaded;

            if (_targetTask != null)
            {
                TargetTaskBanner.Visibility = Visibility.Visible;
                TargetTaskBannerText.Text = $"Целевая задача: {_targetTask.GameName} ({_targetTask.SourceFormat} → {_targetTask.TargetFormat})";
                StartFullDownloadBtn.Content = "🚀 Скачать выбранное и привязать к задаче";
                if (!string.IsNullOrWhiteSpace(_targetTask.GameName))
                {
                    string cleanQuery = System.Text.RegularExpressions.Regex.Replace(_targetTask.GameName, @"\[.*?\]|\(.*?\)", "").Trim();
                    SearchOrUrlBox.Text = cleanQuery;
                }
            }
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
                e.Handled = true;
                this.Hide();
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

                StatusText.Text = "Выполняется поиск раздач Nintendo Switch / 3DS на RuTracker...";
                var results = await _service.SearchTopicsAsync(input, catFilter, CancellationToken.None);

                _topics.Clear();
                foreach (var r in results)
                    _topics.Add(r);

                TopicsCountText.Text = $"{_topics.Count} тем";
                if (_topics.Count > 0)
                {
                    TopicsListView.SelectedIndex = 0;
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

        private async void TopicsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TopicsListView.SelectedItem is not RuTrackerTopicItem topic)
                return;

            _localTorrentPath = null;
            _currentTopicTitle = topic.Title;
            _currentTopicUrl = topic.TopicUrl;
            StatusText.Text = $"Анализ темы [{topic.TopicId}]: парсинг шапки на русификаторы/моды и чтение списка файлов...";
            try
            {
                if (topic.ExtractedModLinks != null && topic.ExtractedModLinks.Count > 0)
                {
                    PopulateDiscussionLinks(topic.ExtractedModLinks);
                }

                // 1. Детальный парсинг темы (извлекает Magnet, InfoHash и ссылки на Яндекс.Диск / Google Drive / GitHub из шапки)
                var detailed = await _service.FetchTopicDetailsAsync(topic.TopicId, CancellationToken.None);
                if (detailed != null)
                {
                    if (!string.IsNullOrEmpty(detailed.MagnetUri))
                        topic.MagnetUri = detailed.MagnetUri;
                    if (!string.IsNullOrEmpty(detailed.InfoHash))
                        topic.InfoHash = detailed.InfoHash;

                    if (detailed.ExtractedModLinks.Count > 0)
                    {
                        PopulateDiscussionLinks(detailed.ExtractedModLinks);
                    }
                    else if (topic.ExtractedModLinks == null || topic.ExtractedModLinks.Count == 0)
                    {
                        ModLinksHintText.Text = "Внешних ссылок в теме не найдено";
                    }
                }
                else if (topic.ExtractedModLinks == null || topic.ExtractedModLinks.Count == 0)
                {
                    ModLinksHintText.Text = "Готов к вставке облачных ссылок";
                }

                // 2. Инспектируем .torrent (если есть bb_session) или Magnet + авто-разбивка на отдельные игры/части
                RebuildGameSlotsForTopic(topic.Title, topic.TopicId);
                var entries = await _service.InspectTorrentFilesAsync(topic, null, CancellationToken.None);
                SyncGameSlotsWithDiscoveredFiles(entries);
                PopulateTorrentFiles(entries);
                var slotTitles = _gameSlots.Select(s => s.GameTitle).ToList();
                _suppressGroupRebuild = true;
                RuTrackerModService.AutoAssignFilesToGameSlots(_torrentFiles, slotTitles);
                RuTrackerModService.AutoAssignLinksToGameSlots(_discussionLinks, slotTitles);
                _suppressGroupRebuild = false;
                UpdateGameSlotsSummary();

                if (entries.Count > 1 || (entries.Count == 1 && entries[0].CategoryBadge != "MAGNET"))
                {
                    StatusText.Text = $"Раздача готова: {entries.Count} файлов в .torrent. Отметьте нужные файлы или скачайте всю раздачу.";
                }
                else if (!string.IsNullOrEmpty(topic.MagnetUri))
                {
                    TorrentFilesHeader.Text = $"🧲 Magnet-разрешение активно ({topic.SizeText}) — файлы будут получены через DHT/P2P";
                    StatusText.Text = "Раздача готова к загрузке по Magnet (без авторизации через публичный API хешей RuTracker).";
                }
                else
                {
                    TorrentFilesHeader.Text = "Для просмотра отдельных файлов войдите в аккаунт или используйте Magnet";
                    StatusText.Text = "Тема прочитана. Вы можете скачать прикреплённые облачные моды или войти в аккаунт для .torrent.";
                }
            }
            catch (Exception ex)
            {
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
            TorrentFilesHeader.Text = $"📦 Файлы в раздаче ({entries.Count} шт.)";
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

        private void InitializeDefaultGameSlot(string title)
        {
            _gameSlots.Clear();
            var slot = new RuTrackerGameSlotItem
            {
                SlotNumber = 1,
                IsEnabled = true,
                GameTitle = title
            };
            slot.PropertyChanged += (_, _) => UpdateGameSlotsSummary();
            _gameSlots.Add(slot);
            UpdateGameSlotsSummary();
        }

        private void RebuildGameSlotsForTopic(string? topicTitle, string? topicId = null)
        {
            var detected = RuTrackerModService.DetectMultiGameTitlesFromRelease(topicTitle, topicId);
            if (detected.Count == 0)
            {
                detected.Add(!string.IsNullOrWhiteSpace(topicTitle) ? topicTitle : "Игра 1");
            }

            _gameSlots.Clear();
            for (int i = 0; i < detected.Count; i++)
            {
                var slot = new RuTrackerGameSlotItem
                {
                    SlotNumber = i + 1,
                    IsEnabled = true,
                    GameTitle = detected[i]
                };
                slot.PropertyChanged += (_, _) => UpdateGameSlotsSummary();
                _gameSlots.Add(slot);
            }
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
            var discoveredFromFiles = RuTrackerModService.DiscoverGameTitlesFromFiles(entries);
            if (discoveredFromFiles.Count == 0)
                return;

            // Если в файлах раздачи обнаружено больше игр или более точные названия (например, "Aggelos" и "Aggelos 2"),
            // синхронизируем слоты в естественном порядке (Часть 1 -> ИГРА #1, Часть 2 -> ИГРА #2)
            if (discoveredFromFiles.Count >= _gameSlots.Count || _gameSlots.Count <= 1)
            {
                _gameSlots.Clear();
                for (int i = 0; i < discoveredFromFiles.Count; i++)
                {
                    var slot = new RuTrackerGameSlotItem
                    {
                        SlotNumber = i + 1,
                        IsEnabled = true,
                        GameTitle = discoveredFromFiles[i]
                    };
                    slot.PropertyChanged += (_, _) => UpdateGameSlotsSummary();
                    _gameSlots.Add(slot);
                }
            }
            else
            {
                for (int i = 0; i < Math.Min(_gameSlots.Count, discoveredFromFiles.Count); i++)
                {
                    if (!string.IsNullOrWhiteSpace(discoveredFromFiles[i]))
                        _gameSlots[i].GameTitle = discoveredFromFiles[i];
                }
            }
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
            UpdateGameSlotsSummary();
            LiveTelemetryText.Text = $"🪄 Авто-разбивка выполнена: распознано частей/игр — {_gameSlots.Count}. Файлы сгруппированы по блокам игр.";
        }

        private void AddGameSlot_Click(object sender, RoutedEventArgs e)
        {
            if (_gameSlots.Count >= 10)
            {
                LiveTelemetryText.Text = "⚠️ Достигнут максимум 10 отдельных слотов в списке быстрой привязки.";
                return;
            }
            int nextNum = _gameSlots.Count + 1;
            string baseName = _gameSlots.Count > 0
                ? System.Text.RegularExpressions.Regex.Replace(_gameSlots[0].GameTitle, @"\s+\d+$", "").Trim()
                : (!string.IsNullOrWhiteSpace(_currentTopicTitle) ? _currentTopicTitle : "Игра");
            var slot = new RuTrackerGameSlotItem
            {
                SlotNumber = nextNum,
                IsEnabled = true,
                GameTitle = $"{baseName} {nextNum}"
            };
            slot.PropertyChanged += (_, _) => UpdateGameSlotsSummary();
            _gameSlots.Add(slot);
            UpdateGameSlotsSummary();
        }

        private void RemoveGameSlot_Click(object sender, RoutedEventArgs e)
        {
            if (_gameSlots.Count <= 1) return;
            int removedSlot = _gameSlots.Count;
            _gameSlots.RemoveAt(_gameSlots.Count - 1);
            _suppressGroupRebuild = true;
            foreach (var f in _torrentFiles.Where(x => x.TargetGameSlot == removedSlot))
                f.TargetGameSlot = 1;
            foreach (var l in _discussionLinks.Where(x => x.TargetGameSlot == removedSlot))
                l.TargetGameSlot = 1;
            _suppressGroupRebuild = false;
            UpdateGameSlotsSummary();
        }

        private void SlotAssignmentCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressGroupRebuild) return;
            UpdateGameSlotsSummary();
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
                string badge = slotNum <= 0 ? "📦 ОБЩИЕ ФАЙЛЫ" : $"🎮 ИГРА #{slotNum}";
                string title = slotNum <= 0
                    ? "Для всех частей раздачи"
                    : (!string.IsNullOrWhiteSpace(slotObj?.GameTitle) ? slotObj!.GameTitle : $"Игра #{slotNum}");

                long totalBytes = grp.Sum(x => Math.Max(0L, x.SizeBytes));
                string sizeStr = totalBytes > 0 ? $"{totalBytes / 1024.0 / 1024.0:F2} МБ" : "Magnet / P2P";
                int count = grp.Count();
                string countWord = count == 1 ? "1 файл" : (count >= 2 && count <= 4 ? $"{count} файла" : $"{count} файлов");

                var block = new RuTrackerFileGroupBlock
                {
                    SlotNumber = slotNum,
                    GroupBadge = badge,
                    GroupTitle = title,
                    GroupSummary = $"{countWord} • {sizeStr}"
                };
                foreach (var fileItem in grp)
                    block.Files.Add(fileItem);
                newFileGroups.Add(block);
            }

            // Проверяем, изменилась ли структура групп, чтобы не сбрасывать скролл без необходимости
            bool sameFileStructure = _fileGroups.Count == newFileGroups.Count &&
                _fileGroups.Zip(newFileGroups, (a, b) =>
                    a.SlotNumber == b.SlotNumber &&
                    a.GroupTitle == b.GroupTitle &&
                    a.GroupSummary == b.GroupSummary &&
                    a.Files.SequenceEqual(b.Files)).All(x => x);

            if (!sameFileStructure)
            {
                _fileGroups.Clear();
                foreach (var g in newFileGroups)
                    _fileGroups.Add(g);
            }

            if (TorrentFilesHeader != null && _torrentFiles.Count > 0 && (_torrentFiles.Count > 1 || _torrentFiles[0].CategoryBadge != "MAGNET"))
            {
                TorrentFilesHeader.Text = $"📦 Файлы в раздаче ({_torrentFiles.Count} шт. • блоками по играм: {_fileGroups.Count})";
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
                string badge = slotNum <= 0 ? "🌐 ОБЩИЕ ССЫЛКИ" : $"🎮 ИГРА #{slotNum}";
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
                    a.GroupTitle == b.GroupTitle &&
                    a.GroupSummary == b.GroupSummary &&
                    a.Links.SequenceEqual(b.Links)).All(x => x);

            if (!sameLinkStructure)
            {
                _linkGroups.Clear();
                foreach (var g in newLinkGroups)
                    _linkGroups.Add(g);
            }
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
            ModLinksHintText.Text = $"Ссылок в теме и обсуждениях: {total} (выбрано: {selected})";
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

        private void SelectAllFiles_Click(object sender, RoutedEventArgs e)
        {
            foreach (var f in _torrentFiles)
                f.IsSelected = true;
        }

        private void SelectOnlyUpdatesAndMods_Click(object sender, RoutedEventArgs e)
        {
            foreach (var f in _torrentFiles)
            {
                f.IsSelected = f.CategoryBadge.Contains("ОБНОВЛЕНИЕ", StringComparison.OrdinalIgnoreCase) ||
                               f.CategoryBadge.Contains("DLC", StringComparison.OrdinalIgnoreCase) ||
                               f.CategoryBadge.Contains("МОД", StringComparison.OrdinalIgnoreCase);
            }
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
                PauseResumeDownloadBtn.Content = "⏸ Пауза";
                StatusText.Text = "Возобновление загрузки...";
            }
            else
            {
                _activeTask.Pause();
                PauseResumeDownloadBtn.Content = "▶️ Продолжить";
                StatusText.Text = "⏸ Загрузка приостановлена (Пауза)";
                SpeedPeersText.Text = string.Empty;
            }
        }

        private void CancelDownload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _activeTask?.Cancel();
                _downloadCts?.Cancel();
                StatusText.Text = "⏹ Задача отменена пользователем.";
                SpeedPeersText.Text = "";
                DownloadProgressBar.Value = 0;
                SetUiBusy(false);
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

            if (downloadTorrent && _torrentFiles.Count > 0 && !_torrentFiles.Any(f => f.IsSelected))
            {
                StatusText.Text = "Выберите хотя бы один файл из раздачи (отметьте галочкой) или скачайте только моды.";
                return;
            }
            bool hasTorrentSource = downloadTorrent && _torrentFiles.Count > 0 && _torrentFiles.Any(f => f.IsSelected);
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

            string safeSubFolder = SanitizeFolderName(!string.IsNullOrWhiteSpace(_currentTopicTitle)
                ? _currentTopicTitle
                : (_targetTask?.GameName ?? $"Release_{DateTime.Now:yyyyMMdd_HHmmss}"));

            string sharedReleaseDir = Path.Combine(baseDownloadDir, safeSubFolder);
            Directory.CreateDirectory(sharedReleaseDir);

            string globalCustomModTitle = CustomModNameBox.Text?.Trim() ?? string.Empty;
            bool deleteRtSource = DeleteRuTrackerSourceCheckBox.IsChecked == true;
            bool autoBuild = AutoBuildCheckBox.IsChecked == true;

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

            _downloadCts?.Cancel();
            _downloadCts = new CancellationTokenSource();

            var createdTasks = new List<(ProcessingTask Task, RuTrackerTaskPayload Payload, RuTrackerGameSlotItem Slot)>();

            for (int idx = 0; idx < activeSlots.Count; idx++)
            {
                var slot = activeSlots[idx];
                string slotTitle = !string.IsNullOrWhiteSpace(slot.GameTitle)
                    ? slot.GameTitle.Trim()
                    : (!string.IsNullOrWhiteSpace(_currentTopicTitle) ? _currentTopicTitle : $"Игра {slot.SlotNumber}");

                var slotFiles = _torrentFiles
                    .Where(f => f.IsSelected && (activeSlots.Count == 1 || f.TargetGameSlot == 0 || f.TargetGameSlot == slot.SlotNumber))
                    .ToList();
                if (slotFiles.Count == 0 && hasTorrentSource)
                {
                    slotFiles = _torrentFiles.Where(f => f.IsSelected).ToList();
                }

                var slotDiscussionLinks = _discussionLinks
                    .Where(x => x.IsSelected && (activeSlots.Count == 1 || x.TargetGameSlot == 0 || x.TargetGameSlot == slot.SlotNumber))
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

                string slotTargetDir = activeSlots.Count > 1
                    ? Path.Combine(sharedReleaseDir, $"Part_{slot.SlotNumber}_{SanitizeFolderName(slotTitle)}")
                    : sharedReleaseDir;
                Directory.CreateDirectory(slotTargetDir);

                string effectiveModTitle = !string.IsNullOrWhiteSpace(slot.CustomModTitle)
                    ? slot.CustomModTitle.Trim()
                    : globalCustomModTitle;

                var payload = new RuTrackerTaskPayload
                {
                    Topic = currentTopic,
                    TorrentBytes = _service.LoadedTorrentBytes,
                    MagnetLink = _service.LoadedMagnet,
                    TorrentFiles = slotFiles,
                    CloudLinks = slotCloudLinks,
                    TargetReleaseDir = slotTargetDir,
                    CustomModTitle = effectiveModTitle,
                    AutoBuild = autoBuild,
                    HasTorrentSource = hasTorrentSource,
                    SlotNumber = slot.SlotNumber,
                    TotalSlots = activeSlots.Count,
                    SlotGameTitle = slotTitle
                };

                ProcessingTask task;
                if (_targetTask != null && idx == 0 && activeSlots.Count == 1)
                {
                    task = _targetTask;
                    task.IsRuTrackerTask = true;
                    task.DeleteSourceAfterVerify = deleteRtSource;
                    task.RuTrackerPayload = payload;
                }
                else
                {
                    task = new ProcessingTask
                    {
                        Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                        GameName = slotTitle,
                        Operation = "Multi",
                        SourceFormat = "MULTI",
                        Status = hasTorrentSource ? "Подключение к рою DHT/P2P..." : "Скачивание модов...",
                        Progress = 0,
                        IsRunning = true,
                        IsRuTrackerTask = true,
                        DeleteSourceAfterVerify = deleteRtSource && (idx == activeSlots.Count - 1),
                        OutputFolder = baseDownloadDir,
                        OutputFileName = SanitizeFolderName(slotTitle),
                        FilesCount = $"{slotFiles.Count} файл(ов)" + (slotCloudLinks.Count > 0 ? $" + {slotCloudLinks.Count} мод." : ""),
                        LogDetails = $"[RuTracker] Старт задачи #{slot.SlotNumber}: {slotTitle}\nЦелевая папка: {slotTargetDir}",
                        RuTrackerPayload = payload,
                        Cts = _downloadCts
                    };
                    _viewModel.Tasks.Add(task);
                }

                createdTasks.Add((task, payload, slot));
            }

            var primaryTask = createdTasks[0].Task;
            _activeTask = primaryTask;

            PauseResumeDownloadBtn.Visibility = Visibility.Visible;
            PauseResumeDownloadBtn.Content = "⏸ Пауза";
            StageBadgeText.Text = hasTorrentSource ? "ЭТАП 1/4 • ПОИСК ПИРОВ DHT" : "ЭТАП 1/2 • ОБЛАЧНЫЕ МОДЫ";
            DownloadProgressBar.IsIndeterminate = hasTorrentSource;
            DownloadProgressBar.Value = 0;
            VolumeProgressText.Text = "Инициализация...";
            SpeedPeersText.Text = "Подключение к трекерам и DHT...";
            LiveTelemetryText.Text = $"🚀 Создано отдельных задач: {createdTasks.Count} ({string.Join(", ", createdTasks.Select(c => c.Payload.SlotGameTitle))}). Запуск загрузки...";

            foreach (var (t, _, _) in createdTasks)
            {
                t.OnPauseRequested = () =>
                {
                    App.RunOnUI(() =>
                    {
                        if (ActiveInstance == this)
                        {
                            PauseResumeDownloadBtn.Content = "▶️ Продолжить";
                            StageBadgeText.Text = "ПАУЗА";
                            DownloadProgressBar.IsIndeterminate = false;
                            StatusText.Text = "⏸ Загрузка приостановлена (Пауза)";
                            SpeedPeersText.Text = string.Empty;
                        }
                    });
                };

                t.OnResumeRequested = () =>
                {
                    App.RunOnUI(() =>
                    {
                        if (ActiveInstance == this)
                        {
                            PauseResumeDownloadBtn.Content = "⏸ Пауза";
                            StageBadgeText.Text = "АКТИВНО";
                            StatusText.Text = "Возобновление загрузки...";
                        }
                    });
                };

                t.OnCancelRequested = () =>
                {
                    App.RunOnUI(() =>
                    {
                        if (ActiveInstance == this)
                        {
                            StageBadgeText.Text = "ОТМЕНЕНО";
                            DownloadProgressBar.IsIndeterminate = false;
                            StatusText.Text = "⏹ Задача отменена пользователем.";
                            SpeedPeersText.Text = string.Empty;
                            VolumeProgressText.Text = string.Empty;
                            SetUiBusy(false);
                            PauseResumeDownloadBtn.Visibility = Visibility.Collapsed;
                        }
                    });
                };
            }

            SetUiBusy(true);
            _ = Task.Run(async () =>
            {
                try
                {
                    if (createdTasks.Count == 1)
                    {
                        var (singleTask, singlePayload, _) = createdTasks[0];
                        await ExecuteRuTrackerPipelineAsync(singleTask, singlePayload, _viewModel, _service,
                            p => UpdateDialogTorrentProgress(p, 1, 1, singlePayload.SlotGameTitle),
                            mp => UpdateDialogCloudProgress(mp, 1, 1, singlePayload.SlotGameTitle));
                    }
                    else
                    {
                        // Многосоставная раздача (2..10+ игр): скачиваем выбранные файлы торрента ОДИН РАЗ в общий буфер,
                        // а затем распределяем файлы и применяем индивидуальные русификаторы/ссылки к каждой задаче отдельно!
                        await ExecuteMultiGameRuTrackerPipelineAsync(
                            createdTasks,
                            sharedReleaseDir,
                            hasTorrentSource,
                            currentTopic,
                            _downloadCts.Token);
                    }
                }
                finally
                {
                    App.RunOnUI(() =>
                    {
                        if (ActiveInstance == this)
                        {
                            DownloadProgressBar.IsIndeterminate = false;
                            SetUiBusy(false);
                            PauseResumeDownloadBtn.Visibility = Visibility.Collapsed;
                            if (createdTasks.All(c => c.Task.Status == "Успешно" || c.Task.Status == "Готово" || c.Task.Status == "Ожидание"))
                            {
                                StageBadgeText.Text = "ГОТОВО";
                                DownloadProgressBar.Value = 100;
                                SpeedPeersText.Text = "100% Завершено";
                                StatusText.Text = $"✅ Обработано задач: {createdTasks.Count} ({sharedReleaseDir}).";
                                LiveTelemetryText.Text = $"✅ Все задачи ({string.Join(", ", createdTasks.Select(c => c.Payload.SlotGameTitle))}) успешно сформированы в очереди задач!";
                            }
                        }
                    });
                }
            });

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

                    await service.DownloadTorrentAsync(
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

                // Сканируем каталог релиза на наличие распакованных romfs / exefs
                foreach (var p in RuTrackerModService.DiscoverModPayloadsInDirectory(payload.TargetReleaseDir))
                {
                    if (!discoveredModPayloads.Contains(p, StringComparer.OrdinalIgnoreCase))
                        discoveredModPayloads.Add(p);
                }

                // 3. Привязка файлов к задаче (на UI-потоке во избежание RPC_E_WRONG_THREAD)
                var bindTcs = new TaskCompletionSource<bool>();
                App.RunOnUI(() =>
                {
                    try
                    {
                        BindDownloadedFilesToExistingTask(task, payload.TargetReleaseDir, discoveredModPayloads, payload.CustomModTitle);
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

                    if (task.DeleteSourceAfterVerify && (task.Status == "Успешно" || task.Status == "Готово"))
                    {
                        try
                        {
                            if (Directory.Exists(payload.TargetReleaseDir) &&
                                !string.Equals(Path.GetFullPath(payload.TargetReleaseDir).TrimEnd('\\', '/'),
                                               Path.GetFullPath(outDir).TrimEnd('\\', '/'),
                                               StringComparison.OrdinalIgnoreCase))
                            {
                                Directory.Delete(payload.TargetReleaseDir, recursive: true);
                                App.Logger?.Log($"[RuTracker] Исходный каталог очищен после успешной сборки: {payload.TargetReleaseDir}", LogLevel.Info);
                            }
                        }
                        catch { }
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
                    // Если автосборка отключена пользователем — переводим задачу в статус «Ожидание», чтобы её можно было запустить из очереди задач
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

        private static void BindDownloadedFilesToExistingTask(
            ProcessingTask task,
            string downloadDir,
            List<string> modPayloads,
            string customModTitle)
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
                if (name == "romfs" || name.EndsWith(".romfs") || name == "romfs.bin" || name.EndsWith(".pak") || name.EndsWith(".sarc.zs") || name.EndsWith(".sarc") || name.EndsWith(".zs"))
                    addedRomFs = true;
                if (name == "exefs" || name == "exefs_patches" || name.EndsWith(".ips") || name.EndsWith(".pchtxt") || name.EndsWith(".bps"))
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

                foreach (var f in discoveredFiles)
                {
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
                string formattedName = MultiContentService.FormatOutputFileName(
                    Path.Combine(task.OutputFolder, task.OutputFileName + task.GetPrimaryExtension()), 
                    task.InputFiles);
                task.OutputFileName = Path.GetFileNameWithoutExtension(formattedName);
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
            CancellationToken ct)
        {
            var firstTask = createdTasks[0].Task;

            // 1. Скачиваем объединенный набор выбранных файлов торрента один раз в общую папку раздачи
            if (hasTorrentSource)
            {
                var unionFiles = _torrentFiles.Where(f => f.IsSelected).ToList();
                var torrentProg = new Progress<TorrentDownloadProgress>(p =>
                {
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

                await _service.DownloadTorrentAsync(
                    sharedReleaseDir,
                    unionFiles,
                    torrentProg,
                    ct,
                    _service.LoadedTorrentBytes,
                    _service.LoadedMagnet,
                    currentTopic?.TopicId,
                    currentTopic?.Seeds ?? 0,
                    firstTask.PauseEvent,
                    mgr =>
                    {
                        foreach (var (t, _, _) in createdTasks)
                            t.ActiveTorrentManager = mgr;
                    }).ConfigureAwait(false);
            }

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
                    SlotGameTitle = payload.SlotGameTitle
                };

                int capturedIdx = i + 1;
                await ExecuteRuTrackerPipelineAsync(
                    task,
                    slotExecPayload,
                    _viewModel,
                    _service,
                    null,
                    mp => UpdateDialogCloudProgress(mp, capturedIdx, createdTasks.Count, payload.SlotGameTitle)).ConfigureAwait(false);
            }
        }

        private static List<string> FilterDownloadedContainersForSlot(
            List<string> allContainers,
            List<TorrentFileSelectionItem> slotFileSelections,
            int slotNumber,
            string slotGameTitle,
            List<string> allSlotTitles)
        {
            if (allContainers.Count <= 1 || allSlotTitles.Count <= 1)
                return allContainers.ToList();

            // 1. Если в slotFileSelections были реальные имена файлов из .torrent и они назначены на конкретный слот
            var explicitRealNames = slotFileSelections
                .Where(f => f.IsRealTorrentFile && f.TargetGameSlot == slotNumber)
                .Select(f => Path.GetFileName(f.RelativePath))
                .Where(n => !string.IsNullOrEmpty(n))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (explicitRealNames.Count > 0)
            {
                var byExplicit = allContainers.Where(c => explicitRealNames.Contains(Path.GetFileName(c))).ToList();
                if (byExplicit.Count > 0) return byExplicit;
            }

            // 2. Авто-группировка скачанных файлов по базовому TitleID (первые 13 символов + 000) или номеру части
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
                if (tempItems[i].TargetGameSlot == slotNumber || tempItems[i].TargetGameSlot == 0)
                    matched.Add(allContainers[i]);
            }

            return matched.Count > 0 ? matched : allContainers.ToList();
        }

        private void SetUiBusy(bool busy)
        {
            StartFullDownloadBtn.IsEnabled = !busy;
            DownloadOnlyModsBtn.IsEnabled = !busy;
            SearchBtn.IsEnabled = !busy;
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

        private readonly TaskCompletionSource<bool> _visualTreeReadyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly SemaphoreSlim _webViewInitGate = new(1, 1);
        private bool _webViewConfigured = false;
        private bool _warmupStartedInDialog = false;
        private static Microsoft.Web.WebView2.Core.CoreWebView2Environment? _sharedWebViewEnv;

        private void Dialog_Opened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            _visualTreeReadyTcs.TrySetResult(true);
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

        private void Dialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
                RuTrackerModService.CloudflareChallengeHandler = null;
                RuTrackerModService.SameOriginFetchHandler = null;
            }
            try { _cfTcs?.TrySetResult(null); } catch { }
            // ВАЖНО: Не отменяем _downloadCts и не глушим сервис!
            // Задача продолжает выполняться в главном задачнике TasksViewModel.Tasks!
        }
    }
}
