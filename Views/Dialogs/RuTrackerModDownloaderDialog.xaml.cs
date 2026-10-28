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
        private readonly TasksViewModel _viewModel;
        private readonly ProcessingTask? _targetTask;
        private readonly RuTrackerModService _service = new();

        private readonly ObservableCollection<RuTrackerTopicItem> _topics = new();
        private readonly ObservableCollection<TorrentFileSelectionItem> _torrentFiles = new();

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

            LoadSavedSettings();

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
                    AppendCloudModLinks(topic.ExtractedModLinks.Select(m => m.Url));
                    ModLinksHintText.Text = $"Найдено ссылок в теме: {topic.ExtractedModLinks.Count}";
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
                        AppendCloudModLinks(detailed.ExtractedModLinks.Select(m => m.Url));
                        ModLinksHintText.Text = $"Найдено ссылок в теме: {detailed.ExtractedModLinks.Count}";
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
            _torrentFiles.Clear();
            foreach (var entry in entries)
                _torrentFiles.Add(entry);
            TorrentFilesHeader.Text = $"📦 Файлы в раздаче ({entries.Count} шт.)";
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

        private void CancelDownload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _downloadCts?.Cancel();
                StatusText.Text = "Остановка загрузки...";
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

            var cloudLinks = (CloudModLinksBox.Text ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                            s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            bool hasTorrentSource = downloadTorrent && _torrentFiles.Count > 0;

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

            _downloadCts?.Cancel();
            _downloadCts = new CancellationTokenSource();
            var ct = _downloadCts.Token;

            SetUiBusy(true);

            try
            {
                // 1. Скачивание торрента / Magnet (если запрошено)
                if (hasTorrentSource)
                {
                    var torrentProgress = new Progress<TorrentDownloadProgress>(p =>
                    {
                        App.RunOnUI(() =>
                        {
                            DownloadProgressBar.Value = Math.Clamp(p.Percent, 0, 100);
                            StatusText.Text = p.StatusText;
                            SpeedPeersText.Text = $"{p.SpeedMbPerSec:F2} МБ/с | Сиды: {p.Seeds} | Пиры: {p.Peers}";
                        });
                    });

                    await _service.DownloadTorrentAsync(
                        targetReleaseDir,
                        _torrentFiles.ToList(),
                        torrentProgress,
                        ct);
                }

                // 2. Скачивание и автораспаковка всех облачных модов / русификаторов
                var discoveredModPayloads = new List<string>();
                if (cloudLinks.Count > 0)
                {
                    var modProg = new Progress<(double Percent, string Status)>(mp =>
                    {
                        App.RunOnUI(() =>
                        {
                            DownloadProgressBar.Value = Math.Clamp(mp.Percent, 0, 100);
                            StatusText.Text = mp.Status;
                        });
                    });

                    var extractedPaths = await _service.DownloadAndExtractCloudModsAsync(cloudLinks, targetReleaseDir, modProg, ct);
                    foreach (var ep in extractedPaths)
                    {
                        if (!discoveredModPayloads.Contains(ep, StringComparer.OrdinalIgnoreCase))
                            discoveredModPayloads.Add(ep);
                    }
                }

                // Также проверяем сам каталог релиза на наличие распакованных папок romfs / exefs / exefs_patches
                foreach (var p in RuTrackerModService.DiscoverModPayloadsInDirectory(targetReleaseDir))
                {
                    if (!discoveredModPayloads.Contains(p, StringComparer.OrdinalIgnoreCase))
                        discoveredModPayloads.Add(p);
                }

                // 3. Интеграция в задачу (привязка к существующей задаче или создание новой комплектной задачи)
                string customModTitle = CustomModNameBox.Text?.Trim() ?? string.Empty;

                SaveCurrentSettings();
                bool deleteRtSource = DeleteRuTrackerSourceCheckBox.IsChecked == true;

                if (_targetTask != null)
                {
                    _targetTask.IsRuTrackerTask = true;
                    _targetTask.DeleteSourceAfterVerify = deleteRtSource;
                    BindDownloadedFilesToExistingTask(_targetTask, targetReleaseDir, discoveredModPayloads, customModTitle);
                    StatusText.Text = $"✅ Модификации и файлы успешно привязаны к задаче «{_targetTask.GameName}»!";
                }
                else
                {
                    StatusText.Text = "Формирование задачи в STORM SWITCH BOX...";
                    await _viewModel.AddDroppedFilesBatchAsync(new List<string> { targetReleaseDir });

                    var newestTask = _viewModel.Tasks.LastOrDefault();
                    if (newestTask != null)
                    {
                        newestTask.IsRuTrackerTask = true;
                        newestTask.DeleteSourceAfterVerify = deleteRtSource;
                        if (!string.IsNullOrWhiteSpace(customModTitle))
                        {
                            if (newestTask.HasRomFs == "Да") newestTask.ModNameRomFs = customModTitle;
                            if (newestTask.HasExeFs == "Да") newestTask.ModNameExeFs = customModTitle;
                        }
                    }

                    StatusText.Text = $"✅ Загрузка завершена! Задача добавлена в список ({targetReleaseDir}).";

                    if (AutoBuildCheckBox.IsChecked == true && _viewModel.StartAllTasksCommand.CanExecute(null))
                    {
                        _viewModel.StartAllTasksCommand.Execute(null);
                    }
                }

                DownloadProgressBar.Value = 100;
                SpeedPeersText.Text = "Готово";
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = "⏹ Загрузка остановлена пользователем.";
                SpeedPeersText.Text = string.Empty;
            }
            catch (Exception ex)
            {
                StatusText.Text = $"❌ Ошибка загрузки: {ex.Message}";
                App.Logger.Log($"[RuTracker/CloudMod] Ошибка: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                SetUiBusy(false);
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
                if (name == "romfs" || name.EndsWith(".romfs") || name == "romfs.bin")
                    addedRomFs = true;
                else if (name == "exefs" || name == "exefs_patches" || name.EndsWith(".ips") || name.EndsWith(".pchtxt") || name.EndsWith(".bps"))
                    addedExeFs = true;
            }

            if (Directory.Exists(downloadDir))
            {
                var gameFiles = Directory.GetFiles(downloadDir, "*.*", SearchOption.AllDirectories)
                    .Where(f =>
                    {
                        string ext = Path.GetExtension(f).ToLowerInvariant();
                        return ext == ".nsp" || ext == ".nsz" || ext == ".xci" || ext == ".xcz" || ext == ".cia" || ext == ".3ds";
                    });

                foreach (var gf in gameFiles)
                {
                    if (!task.InputFiles.Contains(gf, StringComparer.OrdinalIgnoreCase))
                    {
                        task.InputFiles.Add(gf);
                        task.FilesList.Add(Path.GetFileName(gf));
                    }
                }
            }

            if (addedRomFs)
            {
                task.HasRomFs = "Да";
                if (!string.IsNullOrWhiteSpace(customModTitle))
                    task.ModNameRomFs = customModTitle;
                else if (string.IsNullOrWhiteSpace(task.ModNameRomFs))
                    task.ModNameRomFs = "Cloud Mod (RomFS)";
            }

            if (addedExeFs)
            {
                task.HasExeFs = "Да";
                if (!string.IsNullOrWhiteSpace(customModTitle))
                    task.ModNameExeFs = customModTitle;
                else if (string.IsNullOrWhiteSpace(task.ModNameExeFs))
                    task.ModNameExeFs = "Cloud Patch (ExeFS/IPS)";
            }

            task.FilesCount = task.InputFiles.Count.ToString();
            if (task.InputFiles.Count > 1 && task.Operation != "Homebrew")
            {
                task.Operation = "Multi";
            }

            task.LogDetails += $"\n[CloudMod] Привязано элементов: {modPayloads.Count} из {downloadDir}";
            App.Logger.Log($"[CloudMod] К задаче «{task.GameName}» привязано {modPayloads.Count} модификаций (RomFS: {task.HasRomFs}, ExeFS: {task.HasExeFs}).", LogLevel.Success);
        }

        private void SetUiBusy(bool busy)
        {
            StartFullDownloadBtn.IsEnabled = !busy;
            DownloadOnlyModsBtn.IsEnabled = !busy;
            SearchBtn.IsEnabled = !busy;
            CancelDownloadBtn.IsEnabled = busy;
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

        private void Dialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            try { _downloadCts?.Cancel(); } catch { }
            try { _service.Dispose(); } catch { }
        }
    }
}
