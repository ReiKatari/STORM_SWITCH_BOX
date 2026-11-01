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
        private readonly ObservableCollection<TorrentFileSelectionItem> _torrentFiles = new();
        private readonly ObservableCollection<CloudModLinkItem> _discussionLinks = new();

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
            TorrentFilesListView.ItemsSource = _torrentFiles;
            DiscussionLinksListView.ItemsSource = _discussionLinks;

            LoadSavedSettings();
            ActiveInstance = this;
            RuTrackerModService.CloudflareChallengeHandler = SolveCloudflareChallengeInDialogAsync;
            RuTrackerModService.SameOriginFetchHandler = FetchViaSameOriginInDialogAsync;
            _ = InitBackgroundWebViewAsync();

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
                    var entries = await _service.InspectTorrentFilesAsync(dummyTopic, file.Path, CancellationToken.None);
                    PopulateTorrentFiles(entries);
                    StatusText.Text = $"Загружен локальный торрент: {file.Name} ({entries.Count} файлов).";
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
                    StatusText.Text = "По запросу ничего не найдено. Попробуйте упростить запрос или вставить прямую ссылку на тему viewtopic.php?t=...";
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

                // 2. Инспектируем .torrent (если есть bb_session) или Magnet
                var entries = await _service.InspectTorrentFilesAsync(topic, null, CancellationToken.None);
                PopulateTorrentFiles(entries);

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
        }

        private void OnItemSelectionChangedForUnlocker(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TorrentFileSelectionItem.IsSelected) || e.PropertyName == nameof(CloudModLinkItem.IsSelected))
            {
                UpdateDiscussionSummaryText();
                UpdateDlcUnlockerAutoState();
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

            var checkedDiscussionLinks = _discussionLinks
                .Where(x => x.IsSelected)
                .Select(x => x.MirrorUrls != null && x.MirrorUrls.Count > 0
                    ? string.Join("|MIRROR|", new[] { x.Url }.Concat(x.MirrorUrls).Distinct(StringComparer.OrdinalIgnoreCase))
                    : x.Url)
                .ToList();
            var cloudLinks = checkedDiscussionLinks.Concat(updateLinks).Concat(dlcLinks).Concat(modLinks)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (downloadTorrent && _torrentFiles.Count > 0 && !_torrentFiles.Any(f => f.IsSelected))
            {
                StatusText.Text = "Выберите хотя бы один файл из раздачи (отметьте галочкой) или скачайте только моды.";
                return;
            }

            bool hasTorrentSource = downloadTorrent && _torrentFiles.Count > 0 && _torrentFiles.Any(f => f.IsSelected);

            if (!hasTorrentSource && cloudLinks.Count == 0)
            {
                StatusText.Text = "Выберите раздачу RuTracker / .torrent файл или вставьте ссылку на облачный мод (Яндекс.Диск / Google Drive / GitHub).";
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

            string targetReleaseDir = Path.Combine(baseDownloadDir, safeSubFolder);
            Directory.CreateDirectory(targetReleaseDir);

            string customModTitle = CustomModNameBox.Text?.Trim() ?? string.Empty;
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

            var payload = new RuTrackerTaskPayload
            {
                Topic = currentTopic,
                TorrentBytes = _service.LoadedTorrentBytes,
                MagnetLink = _service.LoadedMagnet,
                TorrentFiles = _torrentFiles.Where(f => f.IsSelected).ToList(),
                CloudLinks = cloudLinks,
                TargetReleaseDir = targetReleaseDir,
                CustomModTitle = customModTitle,
                AutoBuild = autoBuild,
                HasTorrentSource = hasTorrentSource
            };

            ProcessingTask task;
            if (_targetTask != null)
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
                    GameName = !string.IsNullOrWhiteSpace(_currentTopicTitle) ? _currentTopicTitle : "RuTracker Загрузка",
                    Operation = "Multi",
                    SourceFormat = "MULTI",
                    Status = hasTorrentSource ? "Загрузка торрента..." : "Скачивание модов...",
                    Progress = 0,
                    IsRunning = true,
                    IsRuTrackerTask = true,
                    DeleteSourceAfterVerify = deleteRtSource,
                    OutputFolder = baseDownloadDir,
                    OutputFileName = SanitizeFolderName(!string.IsNullOrWhiteSpace(_currentTopicTitle) ? _currentTopicTitle : "Game"),
                    FilesCount = $"{_torrentFiles.Count(f => f.IsSelected)} файл(ов)",
                    LogDetails = $"[RuTracker] Старт задачи: {_currentTopicTitle}\nЦелевая папка: {targetReleaseDir}",
                    RuTrackerPayload = payload
                };
                _viewModel.Tasks.Add(task);
            }

            _activeTask = task;
            _downloadCts?.Cancel();
            _downloadCts = new CancellationTokenSource();
            task.Cts = _downloadCts;

            PauseResumeDownloadBtn.Visibility = Visibility.Visible;
            PauseResumeDownloadBtn.Content = "⏸ Пауза";

            task.OnPauseRequested = () =>
            {
                App.RunOnUI(() =>
                {
                    if (ActiveInstance == this)
                    {
                        PauseResumeDownloadBtn.Content = "▶️ Продолжить";
                        StatusText.Text = "⏸ Загрузка приостановлена (Пауза)";
                        SpeedPeersText.Text = string.Empty;
                    }
                });
            };

            task.OnResumeRequested = () =>
            {
                App.RunOnUI(() =>
                {
                    if (ActiveInstance == this)
                    {
                        PauseResumeDownloadBtn.Content = "⏸ Пауза";
                        StatusText.Text = "Возобновление загрузки...";
                    }
                });
            };

            task.OnCancelRequested = () =>
            {
                App.RunOnUI(() =>
                {
                    if (ActiveInstance == this)
                    {
                        StatusText.Text = "⏹ Задача отменена пользователем.";
                        SpeedPeersText.Text = string.Empty;
                        SetUiBusy(false);
                        PauseResumeDownloadBtn.Visibility = Visibility.Collapsed;
                    }
                });
            };

            SetUiBusy(true);

            _ = Task.Run(async () =>
            {
                await ExecuteRuTrackerPipelineAsync(task, payload, _viewModel, _service,
                    p =>
                    {
                        App.RunOnUI(() =>
                        {
                            if (ActiveInstance == this)
                            {
                                DownloadProgressBar.Value = Math.Clamp(p.Percent, 0, 100);
                                StatusText.Text = p.StatusText;
                                SpeedPeersText.Text = p.SwarmTotal > p.Seeds
                                    ? $"{p.SpeedMbPerSec:F2} МБ/с | Сиды: {p.Seeds} (в рое: {p.SwarmTotal}) | Пиры: {p.Peers}"
                                    : $"{p.SpeedMbPerSec:F2} МБ/с | Сиды: {p.Seeds} | Пиры: {p.Peers}";
                            }
                        });
                    },
                    mp =>
                    {
                        App.RunOnUI(() =>
                        {
                            if (ActiveInstance == this)
                            {
                                DownloadProgressBar.Value = Math.Clamp(mp.Percent, 0, 100);
                                StatusText.Text = mp.Status;
                                if (mp.Status.Contains("МБ/с"))
                                {
                                    int sIdx = mp.Status.IndexOf('(');
                                    int eIdx = mp.Status.IndexOf(')');
                                    if (sIdx >= 0 && eIdx > sIdx)
                                        SpeedPeersText.Text = mp.Status.Substring(sIdx + 1, eIdx - sIdx - 1);
                                }
                            }
                        });
                    });

                App.RunOnUI(() =>
                {
                    if (ActiveInstance == this)
                    {
                        SetUiBusy(false);
                        PauseResumeDownloadBtn.Visibility = Visibility.Collapsed;
                        if (task.Status == "Успешно" || task.Status == "Готово")
                        {
                            DownloadProgressBar.Value = 100;
                            SpeedPeersText.Text = "Готово";
                            StatusText.Text = $"✅ Загрузка завершена! Задача добавлена в список ({targetReleaseDir}).";
                        }
                    }
                });
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
            task.IsRunning = true;
            task.IsPaused = false;

            try
            {
                // 1. Скачивание торрента / Magnet
                if (payload.HasTorrentSource)
                {
                    task.Status = "Загрузка торрента...";
                    var torrentProg = new Progress<TorrentDownloadProgress>(p =>
                    {
                        task.Progress = Math.Clamp(p.Percent, 0, 100);
                        task.Status = p.StatusText;
                        task.Speed = p.SwarmTotal > p.Seeds
                            ? $"{p.SpeedMbPerSec:F2} МБ/с | Сиды: {p.Seeds} (в рое: {p.SwarmTotal})"
                            : $"{p.SpeedMbPerSec:F2} МБ/с | Сиды: {p.Seeds}";
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
                    task.Status = "Скачивание облачных модов...";
                    var modProg = new Progress<(double Percent, string Status)>(mp =>
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

                // 3. Привязка файлов к задаче
                BindDownloadedFilesToExistingTask(task, payload.TargetReleaseDir, discoveredModPayloads, payload.CustomModTitle);

                // 4. Автоматическая сборка мульти-контента
                if (payload.AutoBuild)
                {
                    task.Status = "Сборка мульти-контента...";
                    task.Speed = string.Empty;
                    task.Progress = 0;

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
                            if (Directory.Exists(payload.TargetReleaseDir))
                            {
                                Directory.Delete(payload.TargetReleaseDir, recursive: true);
                                App.Logger?.Log($"[RuTracker] Исходный каталог очищен после успешной сборки: {payload.TargetReleaseDir}", LogLevel.Info);
                            }
                        }
                        catch { }
                    }
                }

                task.Status = "Успешно";
                task.Progress = 100;
                task.IsRunning = false;
                task.IsPaused = false;
                task.Speed = string.Empty;
                task.FinishedAt = DateTime.Now;
            }
            catch (OperationCanceledException)
            {
                task.Status = "Отменено";
                task.IsRunning = false;
                task.IsPaused = false;
                task.Speed = string.Empty;
            }
            catch (Exception ex)
            {
                task.Status = "Ошибка";
                task.IsRunning = false;
                task.IsPaused = false;
                task.Speed = string.Empty;
                task.LogDetails += $"\n❌ Ошибка: {ex.Message}";
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
                                p.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase))
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

        
        private readonly TaskCompletionSource<bool> _webViewReadyTcs = new();

        private async Task InitBackgroundWebViewAsync()
        {
            try
            {
                await CloudflareWebView.EnsureCoreWebView2Async();
                CloudflareWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                CloudflareWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;

                // Синхронизация User-Agent
                string currentUa = App.Settings.Current.RuTrackerUserAgent;
                if (!string.IsNullOrWhiteSpace(currentUa))
                {
                    CloudflareWebView.CoreWebView2.Settings.UserAgent = currentUa;
                    RuTrackerModService.SharedUserAgent = currentUa;
                }
                else
                {
                    string rawUa = await CloudflareWebView.ExecuteScriptAsync("navigator.userAgent");
                    string cleanUa = rawUa.Trim('"');
                    if (!string.IsNullOrEmpty(cleanUa) && cleanUa != "null")
                    {
                        CloudflareWebView.CoreWebView2.Settings.UserAgent = cleanUa;
                        RuTrackerModService.SharedUserAgent = cleanUa;
                        App.Settings.Current.RuTrackerUserAgent = cleanUa;
                        _ = App.Settings.SaveAsync();
                    }
                }

                // Синхронизация куки cf_clearance и bb_session в WebView2
                string mirror = RuTrackerModService.NormalizeMirrorUrl(App.Settings.Current.RuTrackerMirror);
                if (!string.IsNullOrWhiteSpace(App.Settings.Current.RuTrackerCfClearance))
                {
                    RuTrackerModService.SharedCfClearance = App.Settings.Current.RuTrackerCfClearance;
                    try
                    {
                        var cfCookie = CloudflareWebView.CoreWebView2.CookieManager.CreateCookie(
                            "cf_clearance", App.Settings.Current.RuTrackerCfClearance, ".rutracker.org", "/");
                        CloudflareWebView.CoreWebView2.CookieManager.AddOrUpdateCookie(cfCookie);
                    }
                    catch { }
                }

                if (!string.IsNullOrWhiteSpace(App.Settings.Current.RuTrackerCookie))
                {
                    try
                    {
                        var bbCookie = CloudflareWebView.CoreWebView2.CookieManager.CreateCookie(
                            "bb_session", App.Settings.Current.RuTrackerCookie, ".rutracker.org", "/");
                        CloudflareWebView.CoreWebView2.CookieManager.AddOrUpdateCookie(bbCookie);
                    }
                    catch { }
                }

                _webViewReadyTcs.TrySetResult(true);

                // Быстрая проверка ТСПУ (2 сек таймаут) перед навигацией
                string responsiveMirror = await RuTrackerModService.ProbeAndGetActiveMirrorAsync(mirror).ConfigureAwait(true);
                App.Settings.Current.RuTrackerMirror = responsiveMirror.Replace("https://", "").Replace("http://", "");
                _ = App.Settings.SaveAsync();

                // Проактивный прогрев viewforum.php?f=1605 (фоновый рендеринг Chromium без зависания)
                string warmupUrl = $"{responsiveMirror}/forum/viewforum.php?f=1605";
                CloudflareWebView.NavigationCompleted += OnWarmupNavigationCompleted;
                CloudflareWebView.Source = new Uri(warmupUrl);
            }
            catch (Exception ex)
            {
                App.Logger?.Log($"[RuTracker WebView2] Ошибка инициализации фонового обхода: {ex.Message}", LogLevel.Warning);
                _webViewReadyTcs.TrySetResult(false);
            }
        }

        private int _warmupNavCount = 0;
        private async void OnWarmupNavigationCompleted(object? sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            try
            {
                _warmupNavCount++;
                string currentUrl = CloudflareWebView.Source?.ToString() ?? "";
                var cookieList = await CloudflareWebView.CoreWebView2.CookieManager.GetCookiesAsync(currentUrl);
                var cfCookie = cookieList.FirstOrDefault(c => c.Name == "cf_clearance");
                if (cfCookie != null)
                {
                    RuTrackerModService.SharedCfClearance = cfCookie.Value;
                    App.Settings.Current.RuTrackerCfClearance = cfCookie.Value;
                    _ = App.Settings.SaveAsync();
                }

                string htmlRaw = await CloudflareWebView.ExecuteScriptAsync("document.documentElement.outerHTML");
                string html = System.Text.RegularExpressions.Regex.Unescape(htmlRaw.Trim('"'));

                bool hasForumTopics = html.Contains("viewtopic.php?t=");
                if (hasForumTopics)
                {
                    RuTrackerModService.ParseAndMergeForumTopicsHtml(html);
                }

                // Отписываемся ТОЛЬКО когда форум действительно загружен, либо получен cf_clearance и нет капчи, либо прошло более 6 навигаций
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
                CloudflareBypassPanel.Opacity = 1.0;
                CloudflareBypassPanel.IsHitTestVisible = true;
            });
        }

        private void HideCloudflareBypassUi()
        {
            App.RunOnUI(() =>
            {
                CloudflareBypassPanel.Opacity = 0.01;
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
            await _webViewReadyTcs.Task.ConfigureAwait(false);

            var tcs = new TaskCompletionSource<string?>();
            App.RunOnUI(async () =>
            {
                try
                {
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
                    string cdpResultJson = await CloudflareWebView.CoreWebView2.CallDevToolsProtocolMethodAsync("Runtime.evaluate", evalParamsJson);

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

                    // Если Same-Origin fetch вернул NEED_NAV, 403, ошибку или проверку — переходим в режим обхода страницы
                    string tid = "";
                    var tm = System.Text.RegularExpressions.Regex.Match(url, @"t=(\d+)");
                    if (tm.Success) tid = tm.Groups[1].Value;

                    var solvedHtml = await SolveCloudflareChallengeInDialogAsync(url, tid, ct);
                    tcs.TrySetResult(solvedHtml);
                }
                catch (Exception ex)
                {
                    App.Logger?.Log($"[SameOrigin Fetch CDP] Ошибка: {ex.Message}", LogLevel.Warning);
                    tcs.TrySetResult(null);
                }
            });

            using var reg = ct.Register(() => tcs.TrySetCanceled());
            return await tcs.Task.ConfigureAwait(false);
        }

        private async Task<string?> SolveCloudflareChallengeInDialogAsync(string url, string topicId, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<string?>();
            _cfTcs = tcs;

            App.RunOnUI(async () =>
            {
                try
                {
                    await CloudflareWebView.EnsureCoreWebView2Async();
                    CloudflareStatusText.Text = $"Подключение к теме #{topicId}... Проверка Cloudflare...";

                    CancellationTokenSource? timeoutCts = new CancellationTokenSource();
                    var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

                    // Внедрение stealth-скриптов обхода анти-автоматизации Turnstile
                    try
                    {
                        await CloudflareWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                            Object.defineProperty(navigator, 'webdriver', { get: () => undefined });
                            window.chrome = { runtime: {} };
                            Object.defineProperty(navigator, 'languages', { get: () => ['ru-RU', 'ru', 'en-US', 'en'] });
                            Object.defineProperty(navigator, 'plugins', { get: () => [1, 2, 3, 4, 5] });
                        ");
                    }
                    catch { }

                    // Полностью автоматический аппаратный CDP-кликер Turnstile в фоне (без показа окна пользователю)
                    _ = Task.Run(async () =>
                    {
                        for (int attempt = 0; attempt < 12 && !linkedCts.IsCancellationRequested; attempt++)
                        {
                            await Task.Delay(1500, linkedCts.Token);
                            if (tcs.Task.IsCompleted) break;

                            App.RunOnUI(async () =>
                            {
                                try
                                {
                                    string checkScript = @"(() => {
                                        try {
                                            const body = document.body ? document.body.innerHTML : '';
                                            if (body.includes('post_body') || body.includes('topic-title')) {
                                                return JSON.stringify({ passed: true });
                                            }
                                            const ifr = document.querySelector('iframe[src*=""challenges.cloudflare.com""], .cf-turnstile iframe, iframe[src*=""turnstile""], #turnstile-wrapper iframe');
                                            if (!ifr) return JSON.stringify({ passed: false, found: false });
                                            const rect = ifr.getBoundingClientRect();
                                            return JSON.stringify({
                                                passed: false,
                                                found: true,
                                                x: Math.round(rect.left + 28),
                                                y: Math.round(rect.top + (rect.height > 0 ? rect.height / 2 : 30))
                                            });
                                        } catch {
                                            return null;
                                        }
                                    })()";

                                    string resJson = await CloudflareWebView.ExecuteScriptAsync(checkScript);
                                    if (!string.IsNullOrEmpty(resJson) && resJson != "null")
                                    {
                                        string unescaped = System.Text.Json.JsonSerializer.Deserialize<string>(resJson) ?? resJson;
                                        using var doc = System.Text.Json.JsonDocument.Parse(unescaped);
                                        if (doc.RootElement.TryGetProperty("passed", out var pPassed) && pPassed.GetBoolean())
                                        {
                                            string html = await CloudflareWebView.ExecuteScriptAsync("document.documentElement.outerHTML");
                                            tcs.TrySetResult(System.Text.Json.JsonSerializer.Deserialize<string>(html) ?? html);
                                            return;
                                        }

                                        if (doc.RootElement.TryGetProperty("found", out var pFound) && pFound.GetBoolean())
                                        {
                                            double clickX = doc.RootElement.GetProperty("x").GetDouble();
                                            double clickY = doc.RootElement.GetProperty("y").GetDouble();

                                            // 1. Имитация движения мыши (mouseMoved)
                                            string moveParams = System.Text.Json.JsonSerializer.Serialize(new
                                            {
                                                type = "mouseMoved",
                                                x = clickX,
                                                y = clickY
                                            });
                                            await CloudflareWebView.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", moveParams);

                                            await Task.Delay(60);

                                            // 2. Нажатие (mousePressed)
                                            string pressParams = System.Text.Json.JsonSerializer.Serialize(new
                                            {
                                                type = "mousePressed",
                                                x = clickX,
                                                y = clickY,
                                                button = "left",
                                                clickCount = 1
                                            });
                                            await CloudflareWebView.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", pressParams);

                                            await Task.Delay(80);

                                            // 3. Отпускание (mouseReleased)
                                            string releaseParams = System.Text.Json.JsonSerializer.Serialize(new
                                            {
                                                type = "mouseReleased",
                                                x = clickX,
                                                y = clickY,
                                                button = "left",
                                                clickCount = 1
                                            });
                                            await CloudflareWebView.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", releaseParams);
                                        }
                                    }
                                }
                                catch { }
                            });
                        }

                        // Только если прошло более 18 секунд и авто-кликер не пробил защиту (например, появилась капча с выбором картинок) — показываем интерфейс для ручной помощи
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
                            var cookieList = await CloudflareWebView.CoreWebView2.CookieManager.GetCookiesAsync(url);
                            var cfCookie = cookieList.FirstOrDefault(c => c.Name == "cf_clearance");
                            var bbCookie = cookieList.FirstOrDefault(c => c.Name == "bb_session");

                            if (cfCookie != null)
                            {
                                RuTrackerModService.SharedCfClearance = cfCookie.Value;
                                RuTrackerModService.SharedUserAgent = CloudflareWebView.CoreWebView2.Settings.UserAgent;
                                _ = App.Settings.SaveAsync();
                            }
                            if (bbCookie != null && string.IsNullOrEmpty(App.Settings.Current.RuTrackerCookie))
                            {
                                App.Settings.Current.RuTrackerCookie = bbCookie.Value;
                                _ = App.Settings.SaveAsync();
                            }

                            string htmlRaw = await CloudflareWebView.ExecuteScriptAsync("document.documentElement.outerHTML");
                            string html = System.Text.RegularExpressions.Regex.Unescape(htmlRaw.Trim('"'));

                            if (html.Contains("topic-title") || html.Contains("viewtopic.php") || (cfCookie != null && !html.Contains("cf-turnstile") && !html.Contains("Just a moment...")))
                            {
                                CloudflareWebView.NavigationCompleted -= OnNavCompleted;
                                timeoutCts.Cancel();
                                HideCloudflareBypassUi();
                                tcs.TrySetResult(html);
                            }
                        }
                        catch (Exception ex)
                        {
                            App.Logger?.Log($"[Cloudflare Bypass] Ошибка чтения страницы: {ex.Message}", LogLevel.Warning);
                        }
                    }

                    CloudflareWebView.NavigationCompleted += OnNavCompleted;
                    CloudflareWebView.Source = new Uri(url);
                }
                catch (Exception ex)
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
