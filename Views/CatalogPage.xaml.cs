using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using StormSwitchBox.Models;
using StormSwitchBox.Services;

namespace StormSwitchBox.Views
{
    public sealed partial class CatalogPage : Page
    {
        private readonly ObservableCollection<CatalogItem> _allCatalogItems = new();
        private readonly ObservableCollection<CatalogItem> _displayedCatalogItems = new();
        private readonly ObservableCollection<CatalogFolderEntry> _folderEntries = new();
        private readonly CatalogScannerService _scannerService;
        private readonly RuTrackerCatalogUpdateService _ruTrackerUpdateService = new();
        private CancellationTokenSource? _scanCts;
        private CancellationTokenSource? _ruTrackerCheckCts;
        private CatalogItem? _selectedCatalogItem;
        private Action? _langHandler;
        private bool _showUpdatesOnly = false;
        private bool _isScanningFolders = false;
        private bool _isCheckingRuTracker = false;

        public CatalogPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            _scannerService = new CatalogScannerService(App.Keys);
            CatalogGridView.ItemsSource = _displayedCatalogItems;
            
            // Загружаем папки
            FoldersListView.ItemsSource = _folderEntries;
            RefreshFoldersList();
            _langHandler = () => App.RunOnUI(ApplyLocalization);
            this.Loaded += CatalogPage_Loaded;
            this.Unloaded += CatalogPage_Unloaded;

            ApplyLocalization();
        }

        private void RefreshFoldersList()
        {
            _folderEntries.Clear();
            foreach (var folder in App.Settings.Current.CatalogFolders)
            {
                _folderEntries.Add(new CatalogFolderEntry
                {
                    FolderPath = folder,
                    IsRecursive = App.Settings.Current.IsCatalogFolderRecursive(folder)
                });
            }
        }

        private bool MatchesCurrentLocalFilter(CatalogItem item)
        {
            if (item == null) return false;
            if (_selectedPlatformFilter == 1 && (item.Is3ds || item.CheckIsSwitch2)) return false;
            if (_selectedPlatformFilter == 2 && !item.CheckIsSwitch2) return false;
            if (_selectedPlatformFilter == 3 && !item.Is3ds) return false;
            if (_showUpdatesOnly && !item.HasRuTrackerUpdate) return false;

            string query = SearchBox?.Text?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(query))
            {
                bool hit = (item.TitleName != null && item.TitleName.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                           (!string.IsNullOrEmpty(item.InternalTitleRussian) && item.InternalTitleRussian.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                           (!string.IsNullOrEmpty(item.InternalTitleEnglish) && item.InternalTitleEnglish.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                           (item.TitleId != null && item.TitleId.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                           (item.Publisher != null && item.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase));
                if (!hit) return false;
            }
            return true;
        }

        private void OnCatalogItemAdded(CatalogItem item)
        {
            if (MatchesCurrentLocalFilter(item) && !_displayedCatalogItems.Contains(item))
            {
                // Удаляем онлайн-заглушки TitleDB, как только появляется первая локальная игра
                for (int i = _displayedCatalogItems.Count - 1; i >= 0; i--)
                {
                    if (_displayedCatalogItems[i].IsOnlineOnly)
                        _displayedCatalogItems.RemoveAt(i);
                }
                _displayedCatalogItems.Add(item);
            }
        }

        private void CatalogPage_Unloaded(object sender, RoutedEventArgs e)
        {
            App.TaskCompleted -= App_TaskCompleted;
            if (_langHandler != null)
            {
                App.Localization.LanguageChanged -= _langHandler;
            }
        }

        private async void App_TaskCompleted(Models.ProcessingTask task)
        {
            if (string.IsNullOrEmpty(task.OutputFolder) || string.IsNullOrEmpty(task.OutputFileName)) return;
            
            // Check if the output folder is in the catalog folders list
            string normOut = task.OutputFolder.TrimEnd('\\', '/');
            if (App.Settings.Current.CatalogFolders.Any(f => f.TrimEnd('\\', '/').Equals(normOut, StringComparison.OrdinalIgnoreCase)))
            {
                string? ext = task.TargetFormat?.ToLowerInvariant() == "multi" ? "nsp" : task.TargetFormat?.ToLowerInvariant();
                if (string.IsNullOrEmpty(ext)) ext = "nsp";
                
                string finalPath = System.IO.Path.Combine(task.OutputFolder, task.OutputFileName + "." + ext);
                
                if (_scanCts == null || _scanCts.IsCancellationRequested)
                {
                    _scanCts = new CancellationTokenSource();
                }
                
                await _scannerService.ScanSingleFileAsync(finalPath, _allCatalogItems, _scanCts.Token, OnCatalogItemAdded);
                
                App.MainDispatcher?.TryEnqueue(() => 
                {
                    ApplyCurrentFilter();
                });
            }
        }

        private void CatalogPage_Loaded(object sender, RoutedEventArgs e)
        {
            App.TaskCompleted -= App_TaskCompleted;
            App.TaskCompleted += App_TaskCompleted;
            if (_langHandler != null)
            {
                App.Localization.LanguageChanged -= _langHandler;
                App.Localization.LanguageChanged += _langHandler;
            }
            ApplyLocalization();
            if (_allCatalogItems.Count == 0 && App.Settings.Current.CatalogFolders.Any())
            {
                ScanAllFolders_Click(this, new RoutedEventArgs());
            }
            UpdateDbStatusUI();
        }

        private void UpdateDbStatusUI()
        {
            if (App.TitleDb.IsDatabaseFresh())
            {
                UpdateDbTextBlock.Text = "Данные TitleDB обновлены";
                UpdateDbIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LightGreen);
            }
            else
            {
                UpdateDbTextBlock.Text = "Требуется обновление TitleDB";
                UpdateDbIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Orange);
            }
        }

        private async void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            bool recursive = AddFolderRecursiveCheckBox?.IsChecked == true;
            string? folderPath = await SystemDialogService.OpenFolderDialogAsync("Выберите папку для сканирования игр");
            if (!string.IsNullOrWhiteSpace(folderPath) && System.IO.Directory.Exists(folderPath))
            {
                string normNew = folderPath.TrimEnd('\\', '/');
                bool exists = App.Settings.Current.CatalogFolders.Any(f =>
                    f.TrimEnd('\\', '/').Equals(normNew, StringComparison.OrdinalIgnoreCase));

                if (!exists)
                {
                    App.Settings.Current.CatalogFolders.Add(folderPath);
                }
                App.Settings.Current.SetCatalogFolderRecursive(folderPath, recursive);
                RefreshFoldersList();
                if (AddFolderRecursiveCheckBox != null)
                    AddFolderRecursiveCheckBox.IsChecked = false;
                _ = App.Settings.SaveAsync();

                BrowseButton.Flyout?.Hide();
                await ScanSingleFolderIncrementalAsync(folderPath, recursive);
            }
        }

        private void RemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            string? selectedPath = (FoldersListView.SelectedItem as CatalogFolderEntry)?.FolderPath
                                   ?? FoldersListView.SelectedItem as string;
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                string normRemoved = selectedPath.TrimEnd('\\', '/');
                App.Settings.Current.CatalogFolders.RemoveAll(f =>
                    f.TrimEnd('\\', '/').Equals(normRemoved, StringComparison.OrdinalIgnoreCase));
                App.Settings.Current.SetCatalogFolderRecursive(selectedPath, false);
                RefreshFoldersList();
                _ = App.Settings.SaveAsync();

                // Динамически удаляем игры, относящиеся только к удаленной папке
                string removedPrefix = normRemoved + System.IO.Path.DirectorySeparatorChar;
                for (int i = _allCatalogItems.Count - 1; i >= 0; i--)
                {
                    var item = _allCatalogItems[i];
                    string itemPath = item.FilePath ?? string.Empty;
                    bool wasInRemoved = itemPath.StartsWith(removedPrefix, StringComparison.OrdinalIgnoreCase);
                    if (!wasInRemoved) continue;

                    bool stillCovered = App.Settings.Current.CatalogFolders.Any(rem =>
                    {
                        string normRem = rem.TrimEnd('\\', '/');
                        bool rec = App.Settings.Current.IsCatalogFolderRecursive(rem);
                        if (rec)
                            return itemPath.StartsWith(normRem + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
                        string? itemDir = System.IO.Path.GetDirectoryName(itemPath)?.TrimEnd('\\', '/');
                        return string.Equals(itemDir, normRem, StringComparison.OrdinalIgnoreCase);
                    });

                    if (!stillCovered)
                    {
                        _displayedCatalogItems.Remove(item);
                        _allCatalogItems.RemoveAt(i);
                    }
                }

                UpdateRuTrackerSummaryUi();
                ScanStatusText.Text = _allCatalogItems.Count > 0 ? $"Найдено игр: {_allCatalogItems.Count}" : "";
                ApplyCurrentFilter();
            }
        }

        private void ScanAllFolders_Click(object sender, RoutedEventArgs e)
        {
            StartScan();
        }

        private async void UpdateDbButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateDbButton.IsEnabled = false;
            ScanProgress.IsActive = true;
            ScanStatusText.Text = "Скачивание TitleDB...";

            var progress = new Progress<int>(percent =>
            {
                App.MainDispatcher?.TryEnqueue(() =>
                {
                    ScanStatusText.Text = $"Скачивание TitleDB... {percent}%";
                });
            });

            try
            {
                bool success = await App.TitleDb.UpdateDatabaseAsync(progress);
                App.MainDispatcher?.TryEnqueue(() =>
                {
                    ScanStatusText.Text = success ? "База TitleDB успешно обновлена!" : "Ошибка скачивания базы!";
                    UpdateDbStatusUI();
                });
            }
            catch (Exception ex)
            {
                App.MainDispatcher?.TryEnqueue(() =>
                {
                    ScanStatusText.Text = $"Ошибка: {ex.Message}";
                });
            }
            finally
            {
                App.MainDispatcher?.TryEnqueue(() =>
                {
                    ScanProgress.IsActive = false;
                    UpdateDbButton.IsEnabled = true;
                });
            }
        }

        private async void StartScan()
        {
            _scanCts?.Cancel();
            _ruTrackerCheckCts?.Cancel();
            _scanCts = new CancellationTokenSource();
            var ct = _scanCts.Token;

            _showUpdatesOnly = false;
            _allCatalogItems.Clear();
            _displayedCatalogItems.Clear();
            SearchBox.Text = "";
            UpdateRuTrackerSummaryUi();

            _isScanningFolders = true;
            ScanProgress.IsActive = true;
            
            var folders = App.Settings.Current.CatalogFolders.ToList();
            ScanStatusText.Text = $"Подготовка к сканированию ({folders.Count} папок)...";

            try
            {
                foreach (var path in folders)
                {
                    ct.ThrowIfCancellationRequested();
                    if (System.IO.Directory.Exists(path))
                    {
                        bool recursive = App.Settings.Current.IsCatalogFolderRecursive(path);
                        ScanStatusText.Text = $"Сканирование: {System.IO.Path.GetFileName(path)}...";
                        await _scannerService.ScanDirectoryAsync(path, _allCatalogItems, ct, recursive, OnCatalogItemAdded);
                    }
                }
                ScanStatusText.Text = $"Найдено игр: {_allCatalogItems.Count}";
                ApplyCurrentFilter();
            }
            catch (OperationCanceledException)
            {
                ScanStatusText.Text = "Сканирование отменено.";
            }
            catch (Exception ex)
            {
                ScanStatusText.Text = $"Ошибка: {ex.Message}";
            }
            finally
            {
                _isScanningFolders = false;
                ScanProgress.IsActive = false;
            }

            if (!ct.IsCancellationRequested && _allCatalogItems.Any(i => !i.Is3ds && !i.IsOnlineOnly))
            {
                _ = CheckRuTrackerUpdatesInternalAsync(isAuto: true);
            }
        }

        private async System.Threading.Tasks.Task ScanSingleFolderIncrementalAsync(string folderPath, bool recursive)
        {
            if (_isScanningFolders || _allCatalogItems.Count == 0)
            {
                StartScan();
                return;
            }

            _ruTrackerCheckCts?.Cancel();
            var ct = _scanCts?.Token ?? CancellationToken.None;
            ScanProgress.IsActive = true;
            ScanStatusText.Text = $"Сканирование: {System.IO.Path.GetFileName(folderPath)}...";

            var addedSlice = new System.Collections.Generic.List<CatalogItem>();
            try
            {
                await _scannerService.ScanDirectoryAsync(folderPath, _allCatalogItems, ct, recursive, item =>
                {
                    addedSlice.Add(item);
                    OnCatalogItemAdded(item);
                });
                ScanStatusText.Text = $"Найдено игр: {_allCatalogItems.Count}";
                ApplyCurrentFilter();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                ScanStatusText.Text = $"Ошибка: {ex.Message}";
            }
            finally
            {
                ScanProgress.IsActive = false;
            }

            if (!ct.IsCancellationRequested && addedSlice.Count > 0)
            {
                _ = CheckRuTrackerUpdatesInternalAsync(isAuto: true);
            }
        }

        private void CheckRuTrackerUpdates_Click(object sender, RoutedEventArgs e)
        {
            if (_isCheckingRuTracker)
            {
                _ruTrackerCheckCts?.Cancel();
                return;
            }
            _ = CheckRuTrackerUpdatesInternalAsync(isAuto: false);
        }

        private void UpdatesOnlyFilterButton_Click(object sender, RoutedEventArgs e)
        {
            _showUpdatesOnly = !_showUpdatesOnly;
            UpdateRuTrackerSummaryUi();
            ApplyCurrentFilter();
        }

        private void ResetUpdatesFilter_Click(object sender, RoutedEventArgs e)
        {
            _showUpdatesOnly = false;
            UpdateRuTrackerSummaryUi();
            ApplyCurrentFilter();
        }

        private async void OpenRuTrackerUpdateForItem_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not CatalogItem item)
                return;

            string query = item.HasRuTrackerTopic
                ? item.RuTrackerTopicId
                : (!string.IsNullOrWhiteSpace(item.TitleName) && item.TitleName != "Unknown Game"
                    ? item.TitleName
                    : (item.TitleId ?? string.Empty));

            try
            {
                var dlg = new Dialogs.RuTrackerModDownloaderDialog(App.TasksVM, null, query)
                {
                    XamlRoot = this.XamlRoot
                };
                await dlg.ShowAsync();
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[RuTracker] Ошибка открытия загрузчика: {ex.Message}", LogLevel.Error);
            }
        }

        private async System.Threading.Tasks.Task CheckRuTrackerUpdatesInternalAsync(bool isAuto)
        {
            var localGames = _allCatalogItems.Where(i => !i.Is3ds && !i.IsOnlineOnly).ToList();
            if (localGames.Count == 0)
            {
                if (!isAuto && RuTrackerUpdatesSummaryText != null)
                    RuTrackerUpdatesSummaryText.Text = "Добавьте папку с играми Nintendo Switch для проверки обновлений.";
                return;
            }

            _ruTrackerCheckCts?.Cancel();
            var cts = new CancellationTokenSource();
            _ruTrackerCheckCts = cts;
            var ct = cts.Token;

            _isCheckingRuTracker = true;
            if (CheckRuTrackerUpdatesTextBlock != null)
                CheckRuTrackerUpdatesTextBlock.Text = App.Localization["Остановить проверку"];
            if (RuTrackerCheckProgress != null)
            {
                RuTrackerCheckProgress.Visibility = Visibility.Visible;
                RuTrackerCheckProgress.IsActive = true;
            }
            if (RuTrackerUpdatesSummaryText != null)
                RuTrackerUpdatesSummaryText.Text = "Проверка версий и дополнений на RuTracker...";

            try
            {
                int updatedCount = await _ruTrackerUpdateService.CheckCatalogUpdatesAsync(
                    localGames,
                    (done, total, found) =>
                    {
                        App.MainDispatcher?.TryEnqueue(() =>
                        {
                            if (ct.IsCancellationRequested) return;
                            if (RuTrackerUpdatesSummaryText != null)
                                RuTrackerUpdatesSummaryText.Text = $"Проверено игр: {done} из {total} | Найдено обновлений: {found}";
                            UpdateRuTrackerSummaryUi();
                        });
                    },
                    ct);

                if (!ct.IsCancellationRequested)
                {
                    UpdateRuTrackerSummaryUi();
                    int verUpdates = _allCatalogItems.Count(i => i.HasRuTrackerVersionUpdate);
                    int dlcUpdates = _allCatalogItems.Count(i => i.HasRuTrackerNewDlc);
                    if (RuTrackerUpdatesSummaryText != null)
                    {
                        RuTrackerUpdatesSummaryText.Text = updatedCount > 0
                            ? $"Найдены обновления RuTracker для {updatedCount} игр (версий: {verUpdates}, новых DLC: {dlcUpdates})"
                            : $"Проверено игр: {localGames.Count}. У всех игр актуальные версии и дополнения.";
                    }
                    if (_showUpdatesOnly) ApplyCurrentFilter();
                }
            }
            catch (OperationCanceledException)
            {
                if (RuTrackerUpdatesSummaryText != null)
                    RuTrackerUpdatesSummaryText.Text = "Проверка обновлений RuTracker остановлена.";
            }
            catch (Exception ex)
            {
                if (RuTrackerUpdatesSummaryText != null)
                    RuTrackerUpdatesSummaryText.Text = $"Ошибка проверки RuTracker: {ex.Message}";
            }
            finally
            {
                if (ReferenceEquals(_ruTrackerCheckCts, cts))
                {
                    _isCheckingRuTracker = false;
                    if (CheckRuTrackerUpdatesTextBlock != null)
                        CheckRuTrackerUpdatesTextBlock.Text = App.Localization["Проверить обновления (RuTracker)"];
                    if (RuTrackerCheckProgress != null)
                    {
                        RuTrackerCheckProgress.IsActive = false;
                        RuTrackerCheckProgress.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        private void UpdateRuTrackerSummaryUi()
        {
            int totalUpdates = _allCatalogItems.Count(i => i.HasRuTrackerUpdate);
            if (UpdatesOnlyCountTextBlock != null)
                UpdatesOnlyCountTextBlock.Text = $"({totalUpdates})";

            if (UpdatesOnlyFilterButton != null)
            {
                UpdatesOnlyFilterButton.Visibility = (totalUpdates > 0 || _showUpdatesOnly) ? Visibility.Visible : Visibility.Collapsed;
                UpdatesOnlyFilterButton.BorderBrush = _showUpdatesOnly
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen)
                    : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.ColorHelper.FromArgb(255, 16, 185, 129));
            }
            if (ResetUpdatesFilterButton != null)
                ResetUpdatesFilterButton.Visibility = _showUpdatesOnly ? Visibility.Visible : Visibility.Collapsed;
        }

        private CancellationTokenSource? _searchCts;
        private int _selectedPlatformFilter = 0; // 0 = All, 1 = Switch, 2 = 3DS

        private void AllPlatformButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedPlatformFilter = 0;
            UpdatePlatformButtons();
            ApplyCurrentFilter();
        }

        private void SwitchPlatformButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedPlatformFilter = 1;
            UpdatePlatformButtons();
            ApplyCurrentFilter();
        }

        private void Switch2PlatformButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedPlatformFilter = 2;
            UpdatePlatformButtons();
            ApplyCurrentFilter();
        }

        private void ThreeDsPlatformButton_Click(object sender, RoutedEventArgs e)
        {
            _selectedPlatformFilter = 3;
            UpdatePlatformButtons();
            ApplyCurrentFilter();
        }

        private void UpdatePlatformButtons()
        {
            var accentBg = Application.Current.Resources["AccentButtonBackground"] as Microsoft.UI.Xaml.Media.Brush ?? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
            var accentFg = Application.Current.Resources["AccentButtonForeground"] as Microsoft.UI.Xaml.Media.Brush ?? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White);
            var transparent = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
            var secondaryFg = Application.Current.Resources["TextFillColorSecondaryBrush"] as Microsoft.UI.Xaml.Media.Brush ?? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);

            AllPlatformButton.Background = _selectedPlatformFilter == 0 ? accentBg : transparent;
            AllPlatformButton.Foreground = _selectedPlatformFilter == 0 ? accentFg : secondaryFg;

            SwitchPlatformButton.Background = _selectedPlatformFilter == 1 ? accentBg : transparent;
            SwitchPlatformButton.Foreground = _selectedPlatformFilter == 1 ? accentFg : secondaryFg;

            Switch2PlatformButton.Background = _selectedPlatformFilter == 2 ? accentBg : transparent;
            Switch2PlatformButton.Foreground = _selectedPlatformFilter == 2 ? accentFg : secondaryFg;

            ThreeDsPlatformButton.Background = _selectedPlatformFilter == 3 ? accentBg : transparent;
            ThreeDsPlatformButton.Foreground = _selectedPlatformFilter == 3 ? accentFg : secondaryFg;
        }

        private void ApplyCurrentFilter()
        {
            SearchBox_TextChanged(SearchBox, null!);
        }

        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs? e)
        {
            string query = SearchBox.Text.Trim();
            
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            try
            {
                if (!string.IsNullOrWhiteSpace(query))
                {
                    await Task.Delay(300, token); // Debounce
                    if (token.IsCancellationRequested) return;
                }

                var itemsSnapshot = _allCatalogItems.ToList();
                var filtered = await Task.Run(() => 
                {
                    var itemsCopy = itemsSnapshot;
                    
                    // Filter by platform & RuTracker updates
                    if (_selectedPlatformFilter == 1) // Switch only
                    {
                        itemsCopy = itemsCopy.Where(i => !i.Is3ds && !i.CheckIsSwitch2).ToList();
                    }
                    else if (_selectedPlatformFilter == 2) // Switch 2 only
                    {
                        itemsCopy = itemsCopy.Where(i => i.CheckIsSwitch2).ToList();
                    }
                    else if (_selectedPlatformFilter == 3) // 3DS only
                    {
                        itemsCopy = itemsCopy.Where(i => i.Is3ds).ToList();
                    }
                    if (_showUpdatesOnly)
                    {
                        itemsCopy = itemsCopy.Where(i => i.HasRuTrackerUpdate).ToList();
                    }

                    if (string.IsNullOrWhiteSpace(query))
                    {
                        if (itemsCopy.Count == 0 && _selectedPlatformFilter != 2 && !_showUpdatesOnly && App.Settings.Current.CatalogFolders.Count == 0)
                        {
                            var initialDb = App.TitleDb.GetAllEntries()
                                .Where(x => !string.IsNullOrEmpty(x.Id) && x.Id.EndsWith("000") && !string.IsNullOrEmpty(x.Name))
                                .Take(160);
                            foreach (var dbEntry in initialDb)
                            {
                                var newItem = new CatalogItem
                                {
                                    TitleName = dbEntry.Name ?? "Unknown Game",
                                    TitleId = dbEntry.Id ?? "0000000000000000",
                                    Publisher = dbEntry.Publisher ?? "Nintendo Switch",
                                    Description = dbEntry.Description ?? "Официальный релиз из базы TitleDB",
                                    FileSize = "TitleDB (База)",
                                    IsOnlineOnly = true,
                                    IsLoading = false
                                };
                                if (!string.IsNullOrEmpty(dbEntry.Version))
                                    newItem.Version = dbEntry.Version;
                                App.TitleDb.EnrichCatalogItem(newItem);
                                itemsCopy.Add(newItem);
                            }
                        }
                        return itemsCopy;
                    }

                    var local = itemsCopy.Where(i => 
                        (i.TitleName != null && i.TitleName.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(i.InternalTitleRussian) && i.InternalTitleRussian.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrEmpty(i.InternalTitleEnglish) && i.InternalTitleEnglish.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                        (i.TitleId != null && i.TitleId.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                        (i.Publisher != null && i.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();

                    // Ищем в онлайне для Switch или All
                    if (_selectedPlatformFilter != 2)
                    {
                        var onlineResults = App.TitleDb.SearchTitles(query);
                        foreach (var dbEntry in onlineResults)
                        {
                            if (local.Any(i => i.TitleName == dbEntry.Name || i.TitleId == dbEntry.Id))
                                continue;

                            var newItem = new CatalogItem
                            {
                                TitleName = dbEntry.Name ?? "Unknown Game",
                                TitleId = dbEntry.Id ?? "0000000000000000",
                                Publisher = dbEntry.Publisher ?? "Unknown",
                                Description = dbEntry.Description ?? "Нет описания",
                                FileSize = "TitleDB (Онлайн)",
                                IsOnlineOnly = true,
                                IsLoading = false
                            };
                            
                            if (!string.IsNullOrEmpty(dbEntry.Version))
                                newItem.Version = dbEntry.Version;

                            App.TitleDb.EnrichCatalogItem(newItem);
                            local.Add(newItem);
                        }
                    }

                    // Ищем в базе Nintendo 3DS (для фильтра 3DS или Все)
                    if (_selectedPlatformFilter != 1)
                    {
                        var threeDsResults = App.NintendoLibrary.Search3dsGames(query);
                        foreach (var entry in threeDsResults)
                        {
                            if (local.Any(i => i.TitleName.Equals(entry.Title, StringComparison.OrdinalIgnoreCase) || 
                                (!string.IsNullOrEmpty(entry.Id) && i.TitleId.Equals(entry.Id, StringComparison.OrdinalIgnoreCase))))
                                continue;

                            var newItem = new CatalogItem
                            {
                                TitleName = entry.Title,
                                TitleId = entry.Id,
                                Publisher = entry.Publisher,
                                Developer = entry.Developer,
                                Category = entry.Genre,
                                Description = entry.Description,
                                ReleaseDate = NintendoLibraryService.FormatDate(entry.ReleaseDate),
                                Version = NintendoLibraryService.FormatVersion(entry.Version),
                                FileSize = "3DS База",
                                Is3ds = true,
                                Platform = "3DS",
                                IsOnlineOnly = true,
                                IsLoading = false,
                                Intro = entry.CoverUrl // Сохраняем URL для безопасной загрузки на UI потоке
                            };

                            local.Add(newItem);
                        }
                    }
                    return local;
                }, token);

                if (!token.IsCancellationRequested)
                {
                    foreach (var item in filtered)
                    {
                        if (item.CoverImage == null && !string.IsNullOrEmpty(item.Intro))
                        {
                            try
                            {
                                string resolved = CoverCacheService.ResolveCoverUrl(item.Intro, item.Platform, item.TitleName, item.TitleId);
                                item.CoverImage = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage { DecodePixelWidth = 256, DecodePixelType = Microsoft.UI.Xaml.Media.Imaging.DecodePixelType.Logical, UriSource = new Uri(resolved) };
                            }
                            catch { }
                        }
                    }

                    _displayedCatalogItems.Clear();
                    foreach (var fi in filtered)
                        _displayedCatalogItems.Add(fi);
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex) 
            {
                System.Diagnostics.Debug.WriteLine($"Search error: {ex.Message}");
            }
        }

        private void Grid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Grid grid && grid.RenderTransform is Microsoft.UI.Xaml.Media.ScaleTransform scale)
            {
                // Плавное увеличение карточки при наведении
                scale.ScaleX = 1.03;
                scale.ScaleY = 1.03;
            }
        }

        private void Grid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Grid grid && grid.RenderTransform is Microsoft.UI.Xaml.Media.ScaleTransform scale)
            {
                scale.ScaleX = 1.0;
                scale.ScaleY = 1.0;
            }
        }

        private async void CatalogGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is CatalogItem item)
            {
                _selectedCatalogItem = item;
                UpdateDetailHeaderTitles(item);
                DetailPublisher.Text = item.Publisher ?? "Unknown";
                DetailDeveloper.Text = !string.IsNullOrEmpty(item.Developer) ? item.Developer : "Unknown";
                DetailCategory.Text = !string.IsNullOrEmpty(item.Category) ? item.Category : "N/A";
                DetailReleaseDate.Text = !string.IsNullOrEmpty(item.ReleaseDate) ? NintendoLibraryService.FormatDate(item.ReleaseDate) : "N/A";
                DetailDisplayVersion.Text = NintendoLibraryService.FormatVersion(item.Version);
                DetailVersionCode.Text = !string.IsNullOrEmpty(item.VersionCode) ? item.VersionCode : "0";
                
                DetailRegions.Text = item.Regions ?? "N/A";
                DetailDlcCount.Text = (!item.IsOnlineOnly && item.LocalDlcCount > 0)
                    ? (item.DlcCount > item.LocalDlcCount
                        ? $"{item.LocalDlcCount} (в файле) / {item.DlcCount} (TitleDB)"
                        : $"{item.LocalDlcCount} (в файле)")
                    : item.DlcCount.ToString();

                if (DetailRuTrackerUpdateBanner != null && DetailRuTrackerUpdateText != null)
                {
                    if (item.HasRuTrackerUpdate)
                    {
                        DetailRuTrackerUpdateBanner.Visibility = Visibility.Visible;
                        DetailRuTrackerUpdateText.Text = item.RuTrackerUpdateSummary;
                    }
                    else
                    {
                        DetailRuTrackerUpdateBanner.Visibility = Visibility.Collapsed;
                    }
                }

                DetailTitleId.Text = item.TitleId ?? "0000000000000000";
                DetailSize.Text = item.FileSize ?? "0 MB";
                if (item.CoverImage != null)
                {
                    DetailCover.Source = item.CoverImage;
                }
                else if (!string.IsNullOrEmpty(item.Intro))
                {
                    try
                    {
                        string resolved = CoverCacheService.ResolveCoverUrl(item.Intro, item.Platform, item.TitleName, item.TitleId);
                        DetailCover.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage { DecodePixelWidth = 440, DecodePixelType = Microsoft.UI.Xaml.Media.Imaging.DecodePixelType.Logical, UriSource = new Uri(resolved) };
                    }
                    catch
                    {
                        DetailCover.Source = null;
                    }
                }
                else
                {
                    DetailCover.Source = null;
                }
                
                DetailRating.Text = !string.IsNullOrEmpty(item.RatingAge) ? item.RatingAge : "N/A";
                DetailVideoCap.Text = item.VideoCapture ?? "N/A";
                DetailSaveSize.Text = item.SaveDataSize ?? "N/A";
                DetailLangs.Text = item.SupportedLanguages ?? "N/A";
                
                DetailIntro.Text = item.Intro ?? "";
                DetailIntro.Visibility = string.IsNullOrEmpty(item.Intro) ? Visibility.Collapsed : Visibility.Visible;
                
                DetailDescription.Text = TitleDbService.EnsureRussianDescription(item);
                
                if (item.HasScreenshots)
                {
                    ScreenshotsPanel.Visibility = Visibility.Visible;
                    ScreenshotsGridView.ItemsSource = item.Screenshots;
                }
                else
                {
                    ScreenshotsPanel.Visibility = Visibility.Collapsed;
                    ScreenshotsGridView.ItemsSource = null;
                }
                
                DetailsOverlay.Visibility = Visibility.Visible;

                // Если eShop данные ещё не загружены - подгружаем при открытии деталей
                if (!item.HasScreenshots || string.IsNullOrEmpty(item.Category) || item.Category == "N/A" 
                    || item.Description == "Описание не найдено." || string.IsNullOrEmpty(item.ReleaseDate) || item.ReleaseDate == "N/A")
                {
                    try
                    {
                        var eShopService = new Services.NintendoEShopService();
                        string detailQuery = !string.IsNullOrWhiteSpace(item.InternalTitleEnglish) ? item.InternalTitleEnglish : (item.TitleName ?? string.Empty);
                        var eshopData = await eShopService.SearchGameInfoAsync(detailQuery);
                        if (eshopData == null && !string.IsNullOrWhiteSpace(item.TitleName) && !string.Equals(detailQuery, item.TitleName, StringComparison.OrdinalIgnoreCase))
                        {
                            eshopData = await eShopService.SearchGameInfoAsync(item.TitleName);
                        }
                        if (eshopData != null)
                        {
                            // Описание
                            if (!string.IsNullOrWhiteSpace(eshopData.Description) && (item.Description == "Описание не найдено." || item.Description == "Нет описания"))
                            {
                                item.Description = eshopData.Description;
                                DetailDescription.Text = eshopData.Description;
                            }
                            // Жанр
                            if (!string.IsNullOrWhiteSpace(eshopData.Genre) && (string.IsNullOrEmpty(item.Category) || item.Category == "N/A"))
                            {
                                item.Category = eshopData.Genre;
                                DetailCategory.Text = eshopData.Genre;
                            }
                            // Дата выхода
                            if (!string.IsNullOrWhiteSpace(eshopData.ReleaseDate) && (string.IsNullOrEmpty(item.ReleaseDate) || item.ReleaseDate == "N/A"))
                            {
                                item.ReleaseDate = eshopData.ReleaseDate;
                                DetailReleaseDate.Text = eshopData.ReleaseDate;
                            }
                            // Разработчик
                            if (!string.IsNullOrWhiteSpace(eshopData.Developer))
                            {
                                item.Developer = eshopData.Developer;
                                DetailDeveloper.Text = eshopData.Developer;
                            }
                            // Издатель
                            if (!string.IsNullOrWhiteSpace(eshopData.Publisher) && (item.Publisher == "Unknown" || string.IsNullOrEmpty(item.Publisher)))
                            {
                                item.Publisher = eshopData.Publisher;
                                DetailPublisher.Text = eshopData.Publisher;
                            }
                            // Скриншоты
                            if (eshopData.Screenshots.Count > 0 && !item.HasScreenshots)
                            {
                                item.Screenshots.Clear();
                                foreach (var shotUrl in eshopData.Screenshots)
                                {
                                    try
                                    {
                                        var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage { DecodePixelWidth = 640, DecodePixelType = Microsoft.UI.Xaml.Media.Imaging.DecodePixelType.Logical, UriSource = new Uri(shotUrl) };
                                        item.Screenshots.Add(bmp);
                                    }
                                    catch { }
                                }
                                if (item.Screenshots.Count > 0)
                                {
                                    item.HasScreenshots = true;
                                    ScreenshotsPanel.Visibility = Visibility.Visible;
                                    ScreenshotsGridView.ItemsSource = item.Screenshots;
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        private void CopyDisplayVersion_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(DetailDisplayVersion.Text);
        }

        private void CopyVersionCode_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(DetailVersionCode.Text);
        }

        private void CopyTitleId_Click(object sender, RoutedEventArgs e)
        {
            CopyToClipboard(DetailTitleId.Text);
        }

        private async void DownloadUpdatesDlcButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCatalogItem == null) return;

            DownloadUpdatesDlcButton.IsEnabled = false;
            try
            {
                string query = _selectedCatalogItem.HasRuTrackerTopic
                    ? _selectedCatalogItem.RuTrackerTopicId
                    : (!string.IsNullOrWhiteSpace(_selectedCatalogItem.TitleName) && _selectedCatalogItem.TitleName != "Unknown Game"
                        ? _selectedCatalogItem.TitleName
                        : (_selectedCatalogItem.TitleId ?? string.Empty));

                var dlg = new Dialogs.RuTrackerModDownloaderDialog(App.TasksVM, null, query)
                {
                    XamlRoot = this.XamlRoot
                };
                await dlg.ShowAsync();
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[RuTracker] Ошибка открытия загрузчика: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                DownloadUpdatesDlcButton.IsEnabled = true;
            }
        }

        private void CardCopyVersion_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is CatalogItem item)
            {
                CopyToClipboard(item.Version);
            }
        }

        private void CardCopyVersionCode_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is CatalogItem item)
            {
                CopyToClipboard(item.VersionCode);
            }
        }

        private void CardCopyTitleId_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is CatalogItem item)
            {
                CopyToClipboard(item.TitleId);
            }
        }

        private void CardCopyUpdateVersion_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is CatalogItem item)
            {
                CopyToClipboard(item.UpdateVersionCode != "Неизвестно" ? item.UpdateVersionCode : item.UpdateVersion);
            }
        }

        private void CardCopySize_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is CatalogItem item)
            {
                CopyToClipboard(item.FileSize);
            }
        }

        private void CardCopyReleaseDate_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is CatalogItem item)
            {
                CopyToClipboard(item.ReleaseDate);
            }
        }

        private void CopyCatalogDlcName_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not TitleDbEntry dlcEntry)
                return;

            string? parentTitle = null;
            if (!string.IsNullOrWhiteSpace(dlcEntry.Id) && dlcEntry.Id.Length == 16)
            {
                string baseTid = RuTrackerModService.NormalizeSwitchBaseApplicationTitleId(dlcEntry.Id);
                parentTitle = _allCatalogItems.FirstOrDefault(i =>
                    string.Equals(RuTrackerModService.NormalizeSwitchBaseApplicationTitleId(i.TitleId), baseTid, StringComparison.OrdinalIgnoreCase))?.TitleName;
            }
            string raw = !string.IsNullOrWhiteSpace(dlcEntry.Name) ? dlcEntry.Name : (dlcEntry.Id ?? string.Empty);
            string clean = RuTrackerModService.ExtractCleanDlcName(raw, parentTitle);
            CopyToClipboard(!string.IsNullOrWhiteSpace(clean) ? clean : raw);
        }

        private void CopyAllCatalogDlcNames_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe || fe.Tag is not CatalogItem item || item.DlcList.Count == 0)
                return;

            var names = item.DlcList
                .Select(d => RuTrackerModService.ExtractCleanDlcName(d.Name, item.TitleName))
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (names.Count > 0)
                CopyToClipboard(string.Join(Environment.NewLine, names));
        }

        private void CopyToClipboard(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dataPackage.SetText(text);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
            }
            catch { }
        }

        private void DetailsOverlay_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            DetailsOverlay.Visibility = Visibility.Collapsed;
        }

        private void DetailsContent_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            // Не закрывать окно при клике на само содержимое
            e.Handled = true;
        }

        private void CloseDetails_Click(object sender, RoutedEventArgs e)
        {
            DetailsOverlay.Visibility = Visibility.Collapsed;
        }

        private async void SaveCoverButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCatalogItem == null || _selectedCatalogItem.CoverBytes == null || _selectedCatalogItem.CoverBytes.Length == 0)
            {
                var dialog = new ContentDialog
                {
                    Title = "Сохранение обложки",
                    Content = "Обложка отсутствует или еще не загружена.",
                    CloseButtonText = "ОК",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
                return;
            }

            try
            {
                var savePicker = new Windows.Storage.Pickers.FileSavePicker();
                savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
                savePicker.FileTypeChoices.Add("PNG Image", new System.Collections.Generic.List<string>() { ".png" });
                
                string cleanName = _selectedCatalogItem.TitleName;
                string fileName = _selectedCatalogItem.FileName;
                if (!string.IsNullOrEmpty(fileName))
                {
                    int braceIndex = fileName.IndexOf('[');
                    if (braceIndex > 0)
                    {
                        cleanName = fileName.Substring(0, braceIndex).Trim();
                    }
                    else
                    {
                        cleanName = System.IO.Path.GetFileNameWithoutExtension(fileName);
                    }
                }

                string safeName = string.Join("_", cleanName.Split(System.IO.Path.GetInvalidFileNameChars()));
                savePicker.SuggestedFileName = safeName;

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

                var file = await savePicker.PickSaveFileAsync();
                if (file != null)
                {
                    await Windows.Storage.FileIO.WriteBytesAsync(file, _selectedCatalogItem.CoverBytes);
                    
                    var successDialog = new ContentDialog
                    {
                        Title = "Успех",
                        Content = "Обложка успешно сохранена!",
                        CloseButtonText = "ОК",
                        XamlRoot = this.XamlRoot
                    };
                    await successDialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Ошибка сохранения",
                    Content = $"Не удалось сохранить файл: {ex.Message}",
                    CloseButtonText = "ОК",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }

        private int _currentScreenshotIndex = 0;

        private async void CopyCoverImage_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCatalogItem == null) return;
            try
            {
                byte[]? bytes = _selectedCatalogItem.CoverBytes;
                if ((bytes == null || bytes.Length == 0) && _selectedCatalogItem.CoverImage?.UriSource != null)
                {
                    var uri = _selectedCatalogItem.CoverImage.UriSource;
                    if (uri.IsFile && File.Exists(uri.LocalPath))
                    {
                        bytes = await File.ReadAllBytesAsync(uri.LocalPath);
                    }
                    else if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                    {
                        bytes = await StormHttpClientFactory.Shared.GetByteArrayAsync(uri);
                    }
                    if (bytes != null && bytes.Length > 0)
                    {
                        _selectedCatalogItem.CoverBytes = bytes;
                    }
                }

                if (bytes != null && bytes.Length > 0)
                {
                    var ras = new Windows.Storage.Streams.InMemoryRandomAccessStream();
                    using (var writer = new Windows.Storage.Streams.DataWriter(ras.GetOutputStreamAt(0)))
                    {
                        writer.WriteBytes(bytes);
                        await writer.StoreAsync();
                        await writer.FlushAsync();
                        writer.DetachStream();
                    }
                    ras.Seek(0);
                    var streamRef = Windows.Storage.Streams.RandomAccessStreamReference.CreateFromStream(ras);
                    var dataPackage = new Windows.ApplicationModel.DataTransfer.DataPackage();
                    dataPackage.SetBitmap(streamRef);
                    Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dataPackage);
                    Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
                }
            }
            catch (Exception ex)
            {
                App.Logger?.Log($"[Catalog] Ошибка копирования обложки: {ex.Message}", LogLevel.Warning);
            }
        }

        private void ScreenshotsGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Microsoft.UI.Xaml.Media.Imaging.BitmapImage bmp && _selectedCatalogItem != null)
            {
                _currentScreenshotIndex = _selectedCatalogItem.Screenshots.IndexOf(bmp);
                if (_currentScreenshotIndex < 0) _currentScreenshotIndex = 0;
                UpdateFullscreenDisplay();
                FullscreenImageOverlay.Visibility = Visibility.Visible;
            }
        }

        private void UpdateFullscreenDisplay()
        {
            if (_selectedCatalogItem == null || _selectedCatalogItem.Screenshots.Count == 0) return;

            if (_currentScreenshotIndex < 0) _currentScreenshotIndex = 0;
            if (_currentScreenshotIndex >= _selectedCatalogItem.Screenshots.Count) _currentScreenshotIndex = _selectedCatalogItem.Screenshots.Count - 1;

            FullscreenImage.Source = _selectedCatalogItem.Screenshots[_currentScreenshotIndex];
            FullscreenCounter.Text = $"Скриншот {_currentScreenshotIndex + 1} из {_selectedCatalogItem.Screenshots.Count}";

            PrevScreenshotBtn.IsEnabled = _currentScreenshotIndex > 0;
            NextScreenshotBtn.IsEnabled = _currentScreenshotIndex < _selectedCatalogItem.Screenshots.Count - 1;
        }

        private void PrevScreenshot_Click(object sender, RoutedEventArgs e)
        {
            if (_currentScreenshotIndex > 0)
            {
                _currentScreenshotIndex--;
                UpdateFullscreenDisplay();
            }
        }

        private void NextScreenshot_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCatalogItem != null && _currentScreenshotIndex < _selectedCatalogItem.Screenshots.Count - 1)
            {
                _currentScreenshotIndex++;
                UpdateFullscreenDisplay();
            }
        }

        private async void SaveCurrentScreenshot_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedCatalogItem == null || _selectedCatalogItem.Screenshots.Count == 0) return;

            try
            {
                int idx = Math.Clamp(_currentScreenshotIndex, 0, _selectedCatalogItem.Screenshots.Count - 1);
                var bmp = _selectedCatalogItem.Screenshots[idx];
                var uri = bmp?.UriSource;

                var savePicker = new Windows.Storage.Pickers.FileSavePicker();
                savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
                savePicker.FileTypeChoices.Add("PNG Image", new List<string>() { ".png" });
                savePicker.SuggestedFileName = $"{StormToolResolver.SanitizeFileName(_selectedCatalogItem.TitleName)}_Screenshot_{idx + 1}";

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

                var file = await savePicker.PickSaveFileAsync();
                if (file != null)
                {
                    byte[]? bytes = null;
                    if (uri != null)
                    {
                        if (uri.IsFile && File.Exists(uri.LocalPath))
                        {
                            bytes = await File.ReadAllBytesAsync(uri.LocalPath);
                        }
                        else if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                        {
                            bytes = await StormHttpClientFactory.Shared.GetByteArrayAsync(uri);
                        }
                    }

                    if (bytes == null || bytes.Length == 0)
                    {
                        throw new InvalidOperationException("Не удалось загрузить данные скриншота.");
                    }

                    await Windows.Storage.FileIO.WriteBytesAsync(file, bytes);

                    var dialog = new ContentDialog
                    {
                        Title = "Сохранение скриншота",
                        Content = $"Скриншот сохранен: {file.Name}",
                        CloseButtonText = "ОК",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                App.Logger?.Log($"[Catalog] Ошибка сохранения скриншота: {ex.Message}", LogLevel.Warning);
            }
        }

        private void FullscreenImageOverlay_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            FullscreenImageOverlay.Visibility = Visibility.Collapsed;
        }

        private void CloseFullscreen_Click(object sender, RoutedEventArgs e)
        {
            FullscreenImageOverlay.Visibility = Visibility.Collapsed;
        }

        private void AcceptUpdate_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is CatalogItem item)
            {
                if (!string.IsNullOrEmpty(item.TitleId) && !string.IsNullOrEmpty(item.UpdateVersionCode))
                {
                    // Сохраняем в настройки
                    App.Settings.Current.VersionOverrides[item.TitleId] = item.UpdateVersionCode;
                    _ = App.Settings.SaveAsync();

                    // Обновляем UI моментально
                    item.VersionCode = item.UpdateVersionCode;
                    item.IsOutdated = false;
                    item.UpdateVersion = "";
                    item.UpdateVersionCode = "";
                }
            }
        }

        private void UpdateDetailHeaderTitles(CatalogItem item)
        {
            DetailTitle.Text = item.TitleName ?? "Unknown";
            if (DetailAltTitle != null)
            {
                string altTitle = item.AlternateInternalTitle;
                if (!string.IsNullOrWhiteSpace(altTitle))
                {
                    bool isAltEnglish = string.Equals(altTitle, item.InternalTitleEnglish, StringComparison.OrdinalIgnoreCase);
                    string altPrefix = App.Localization.TranslateText(isAltEnglish ? "Внутреннее английское название:" : "Внутреннее русское название:");
                    DetailAltTitle.Text = $"{altPrefix} {altTitle}";
                    DetailAltTitle.Visibility = Visibility.Visible;
                }
                else
                {
                    DetailAltTitle.Text = string.Empty;
                    DetailAltTitle.Visibility = Visibility.Collapsed;
                }
            }
        }

        public void ApplyLocalization()
        {
            var loc = App.Localization;
            if (BrowseButtonTextBlock != null) BrowseButtonTextBlock.Text = loc["Catalog_ScanFolders"];
            if (FlyoutFoldersTitle != null) FlyoutFoldersTitle.Text = loc["Catalog_ScanFolders_Title"];
            if (ScanAllButton != null) ScanAllButton.Content = loc["Catalog_ScanAll"];
            if (AllPlatformButton != null) AllPlatformButton.Content = loc["Catalog_Filter_All"];
            if (SwitchPlatformButton != null) SwitchPlatformButton.Content = loc["Catalog_Filter_Switch"];
            if (ThreeDsPlatformButton != null) ThreeDsPlatformButton.Content = loc["Catalog_Filter_3ds"];
            if (SearchBox != null) SearchBox.PlaceholderText = loc["Catalog_Search_Placeholder"];
            if (UpdateDbTextBlock != null) UpdateDbTextBlock.Text = loc["Catalog_UpdateDb"];

            foreach (var item in _allCatalogItems)
            {
                if (item.HasEmbeddedInternalTitle)
                {
                    string preferred = item.ResolvePreferredInternalTitle(loc.CurrentLanguage);
                    if (!string.IsNullOrWhiteSpace(preferred) && !string.Equals(item.TitleName, preferred, StringComparison.Ordinal))
                    {
                        item.TitleName = preferred;
                    }
                }
            }

            if (_selectedCatalogItem != null && DetailsOverlay != null && DetailsOverlay.Visibility == Visibility.Visible)
            {
                UpdateDetailHeaderTitles(_selectedCatalogItem);
            }

            App.Localization.TranslateRoot(this);
        }
    
        private async void ErrorCodesLookupButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Dialogs.ErrorCodesDialog { XamlRoot = this.XamlRoot };
            await dlg.ShowAsync();
        }

    }
}
