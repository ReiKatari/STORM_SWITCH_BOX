using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using StormSwitchBox.Models;
using StormSwitchBox.Services;

namespace StormSwitchBox.Views
{
    public sealed partial class GameLibraryPage : Page
    {
        private readonly HashSet<string> _selectedSystems = new(StringComparer.OrdinalIgnoreCase);
        private CancellationTokenSource? _searchCts;
        private NintendoGameEntry? _selectedGame;
        private readonly List<Button> _systemButtons = new();
        private bool _isLoaded = false;

        // Pagination state
        private List<NintendoGameEntry> _allFilteredGames = new();
        private readonly ObservableCollection<NintendoGameEntry> _pagedGames = new();
        private int _currentPage = 1;
        private int _pageSize = 48;
        private int _totalPages = 1;

        public GameLibraryPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            this.Loaded += GameLibraryPage_Loaded;
        }

        private CancellationTokenSource? _filterCts;

        private async void GameLibraryPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded)
            {
                GamesGridView.ItemsSource = _pagedGames;
                _isLoaded = true;

                ApplyLocalization();
                InitializeSystemTabsAndDropdown();

                App.NintendoLibrary.LibraryUpdated += OnLibraryUpdated;
                App.TitleDb.DatabaseLoaded += OnTitleDbLoaded;

                // Асинхронная загрузка базы Switch в фоне без подвисания UI
                _ = App.NintendoLibrary.EnsureSwitchGamesLoadedAsync(App.TitleDb);

                await Task.Run(() =>
                {
                    // Pre-warm distinct caches off-UI thread
                    App.NintendoLibrary.GetDistinctGenres(null);
                    App.NintendoLibrary.GetDistinctDevelopers(null);
                    App.NintendoLibrary.GetDistinctPublishers(null);
                    App.NintendoLibrary.GetDistinctLanguages(null);
                });

                PopulateFilterDropdowns();
                ApplyFilters();
            }
            else
            {
                ApplyLocalization();
            }
        }

        private void OnTitleDbLoaded()
        {
            _ = App.NintendoLibrary.EnsureSwitchGamesLoadedAsync(App.TitleDb);
        }

        private void OnLibraryUpdated()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                InitializeSystemTabsAndDropdown();
                PopulateFilterDropdowns();
                ApplyFilters();
            });
        }

        public void ApplyLocalization()
        {
            var loc = App.Localization;
            if (loc == null) return;

            if (SearchBox != null) SearchBox.PlaceholderText = loc["Library_Search_Placeholder"];
            if (ResetButtonTextBlock != null) ResetButtonTextBlock.Text = loc["Library_Reset"];
            if (PerPageLabelTextBlock != null) PerPageLabelTextBlock.Text = loc["Library_PerPage"];
            if (DialogSearchWebTextBlock != null) DialogSearchWebTextBlock.Text = loc["Library_SearchWeb"];

            if (GameDetailsDialog != null)
            {
                GameDetailsDialog.PrimaryButtonText = loc["Library_SaveCover"];
                GameDetailsDialog.SecondaryButtonText = loc["Library_CopyDetails"];
                GameDetailsDialog.CloseButtonText = loc["Common_Close"];
            }

            UpdatePageSummaryText();
        }

        private void InitializeSystemTabsAndDropdown()
        {
            if (SystemsStackPanel == null) return;

            SystemsStackPanel.Children.Clear();
            _systemButtons.Clear();

            // "Все системы" Tab Chip с форматированием пробела в тысячах
            int totalAll = App.NintendoLibrary.GetGameCountForSystem("Все системы");
            string formattedTotal = NintendoLibraryService.FormatNumber(totalAll);
            var allBtn = CreateSystemButton($"🌟 Все системы ({formattedTotal})", "Все системы", _selectedSystems.Count == 0);
            SystemsStackPanel.Children.Add(allBtn);
            _systemButtons.Add(allBtn);

            foreach (var platform in NintendoLibraryService.Platforms)
            {
                int count = App.NintendoLibrary.GetGameCountForSystem(platform.FullName);
                string formattedCount = NintendoLibraryService.FormatNumber(count);
                string chipLabel = $"{platform.FullName} ({formattedCount})";
                bool isSel = _selectedSystems.Contains(platform.FullName);
                var btn = CreateSystemButton(chipLabel, platform.FullName, isSel);
                SystemsStackPanel.Children.Add(btn);
                _systemButtons.Add(btn);
            }
        }

        private Button CreateSystemButton(string label, string systemKey, bool isSelected)
        {
            var accentBg = Application.Current.Resources["AccentButtonBackground"] as Brush ?? new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
            var accentFg = Application.Current.Resources["AccentButtonForeground"] as Brush ?? new SolidColorBrush(Microsoft.UI.Colors.White);
            var secondaryFg = Application.Current.Resources["TextFillColorSecondaryBrush"] as Brush ?? new SolidColorBrush(Microsoft.UI.Colors.Gray);
            var transparent = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

            var btn = new Button
            {
                Content = label,
                Tag = systemKey,
                Height = 34,
                CornerRadius = new CornerRadius(6),
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Padding = new Thickness(12, 0, 12, 0),
                Background = isSelected ? accentBg : transparent,
                Foreground = isSelected ? accentFg : secondaryFg
            };

            btn.Click += SystemButton_Click;
            return btn;
        }

        private void SystemButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button clickedBtn && clickedBtn.Tag is string sysKey)
            {
                if (sysKey == "Все системы")
                {
                    _selectedSystems.Clear();
                }
                else
                {
                    if (_selectedSystems.Contains(sysKey))
                    {
                        _selectedSystems.Remove(sysKey);
                    }
                    else
                    {
                        _selectedSystems.Add(sysKey);
                    }
                }

                UpdateSystemButtonsUI();
                PopulateFilterDropdowns();
                _currentPage = 1;
                ApplyFilters();
            }
        }

        private void UpdateSystemButtonsUI()
        {
            var accentBg = Application.Current.Resources["AccentButtonBackground"] as Brush ?? new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
            var accentFg = Application.Current.Resources["AccentButtonForeground"] as Brush ?? new SolidColorBrush(Microsoft.UI.Colors.White);
            var secondaryFg = Application.Current.Resources["TextFillColorSecondaryBrush"] as Brush ?? new SolidColorBrush(Microsoft.UI.Colors.Gray);
            var transparent = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

            foreach (var btn in _systemButtons)
            {
                string tag = (btn.Tag as string) ?? "";
                bool isSel = (tag == "Все системы" && _selectedSystems.Count == 0) ||
                             (_selectedSystems.Contains(tag));

                btn.Background = isSel ? accentBg : transparent;
                btn.Foreground = isSel ? accentFg : secondaryFg;
            }
        }

        public class FilterItemViewModel : System.ComponentModel.INotifyPropertyChanged
        {
            private bool _isChecked;
            public string Name { get; set; } = "";
            public bool IsChecked
            {
                get => _isChecked;
                set
                {
                    if (_isChecked != value)
                    {
                        _isChecked = value;
                        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsChecked)));
                    }
                }
            }
            public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        }

        private readonly List<FilterItemViewModel> _allGenreItems = new();
        private readonly ObservableCollection<FilterItemViewModel> _visibleGenreItems = new();

        private readonly List<FilterItemViewModel> _allDevItems = new();
        private readonly ObservableCollection<FilterItemViewModel> _visibleDevItems = new();

        private readonly List<FilterItemViewModel> _allPubItems = new();
        private readonly ObservableCollection<FilterItemViewModel> _visiblePubItems = new();

        private readonly List<FilterItemViewModel> _allLangItems = new();
        private readonly ObservableCollection<FilterItemViewModel> _visibleLangItems = new();

        private void PopulateFilterDropdowns()
        {
            bool previousLoaded = _isLoaded;
            _isLoaded = false;

            try
            {
                string? singleSys = _selectedSystems.Count == 1 ? _selectedSystems.First() : null;

                // 1. Genres
                var rawGenres = App.NintendoLibrary.GetDistinctGenres(singleSys).Where(g => g != "Все жанры").ToList();
                _allGenreItems.Clear();
                _visibleGenreItems.Clear();
                foreach (var g in rawGenres)
                {
                    var item = new FilterItemViewModel { Name = g, IsChecked = false };
                    _allGenreItems.Add(item);
                    _visibleGenreItems.Add(item);
                }
                if (GenreListView != null) GenreListView.ItemsSource = _visibleGenreItems;
                UpdateGenreButtonLabel();

                // 2. Developers
                var rawDevs = App.NintendoLibrary.GetDistinctDevelopers(singleSys).Where(d => d != "Все разработчики").ToList();
                _allDevItems.Clear();
                _visibleDevItems.Clear();
                foreach (var d in rawDevs)
                {
                    var item = new FilterItemViewModel { Name = d, IsChecked = false };
                    _allDevItems.Add(item);
                    _visibleDevItems.Add(item);
                }
                if (DevListView != null) DevListView.ItemsSource = _visibleDevItems;
                UpdateDevButtonLabel();

                // 3. Publishers
                var rawPubs = App.NintendoLibrary.GetDistinctPublishers(singleSys).Where(p => p != "Все издатели").ToList();
                _allPubItems.Clear();
                _visiblePubItems.Clear();
                foreach (var p in rawPubs)
                {
                    var item = new FilterItemViewModel { Name = p, IsChecked = false };
                    _allPubItems.Add(item);
                    _visiblePubItems.Add(item);
                }
                if (PubListView != null) PubListView.ItemsSource = _visiblePubItems;
                UpdatePubButtonLabel();

                // 4. Languages
                var rawLangs = App.NintendoLibrary.GetDistinctLanguages(singleSys).Where(l => l != "Все языки").ToList();
                _allLangItems.Clear();
                _visibleLangItems.Clear();
                foreach (var l in rawLangs)
                {
                    var item = new FilterItemViewModel { Name = l, IsChecked = false };
                    _allLangItems.Add(item);
                    _visibleLangItems.Add(item);
                }
                if (LangListView != null) LangListView.ItemsSource = _visibleLangItems;
                UpdateLangButtonLabel();
            }
            finally
            {
                _isLoaded = previousLoaded;
            }
        }

        private void UpdateGenreButtonLabel()
        {
            int count = _allGenreItems.Count(i => i.IsChecked);
            if (GenreButtonText != null)
                GenreButtonText.Text = count > 0 ? $"Жанры ({count})" : "Все жанры";
        }

        private void UpdateDevButtonLabel()
        {
            int count = _allDevItems.Count(i => i.IsChecked);
            if (DevButtonText != null)
                DevButtonText.Text = count > 0 ? $"Разработчики ({count})" : "Все разработчики";
        }

        private void UpdatePubButtonLabel()
        {
            int count = _allPubItems.Count(i => i.IsChecked);
            if (PubButtonText != null)
                PubButtonText.Text = count > 0 ? $"Издатели ({count})" : "Все издатели";
        }

        private void UpdateLangButtonLabel()
        {
            int count = _allLangItems.Count(i => i.IsChecked);
            if (LangButtonText != null)
                LangButtonText.Text = count > 0 ? $"Языки ({count})" : "Все языки";
        }

        private void FilterItem_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            UpdateGenreButtonLabel();
            UpdateDevButtonLabel();
            UpdatePubButtonLabel();
            UpdateLangButtonLabel();
            _currentPage = 1;
            ApplyFilters();
        }

        private void SelectAllGenres_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _allGenreItems) item.IsChecked = true;
            UpdateGenreButtonLabel();
            _currentPage = 1;
            ApplyFilters();
        }

        private void ClearGenres_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _allGenreItems) item.IsChecked = false;
            UpdateGenreButtonLabel();
            _currentPage = 1;
            ApplyFilters();
        }

        private void GenreSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = GenreSearchBox?.Text?.Trim() ?? "";
            _visibleGenreItems.Clear();
            var matches = string.IsNullOrEmpty(filter)
                ? _allGenreItems
                : _allGenreItems.Where(i => i.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));
            foreach (var m in matches) _visibleGenreItems.Add(m);
        }

        private void SelectAllDevs_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _allDevItems) item.IsChecked = true;
            UpdateDevButtonLabel();
            _currentPage = 1;
            ApplyFilters();
        }

        private void ClearDevs_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _allDevItems) item.IsChecked = false;
            UpdateDevButtonLabel();
            _currentPage = 1;
            ApplyFilters();
        }

        private void DevSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = DevSearchBox?.Text?.Trim() ?? "";
            _visibleDevItems.Clear();
            var matches = string.IsNullOrEmpty(filter)
                ? _allDevItems
                : _allDevItems.Where(i => i.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));
            foreach (var m in matches) _visibleDevItems.Add(m);
        }

        private void SelectAllPubs_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _allPubItems) item.IsChecked = true;
            UpdatePubButtonLabel();
            _currentPage = 1;
            ApplyFilters();
        }

        private void ClearPubs_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _allPubItems) item.IsChecked = false;
            UpdatePubButtonLabel();
            _currentPage = 1;
            ApplyFilters();
        }

        private void PubSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = PubSearchBox?.Text?.Trim() ?? "";
            _visiblePubItems.Clear();
            var matches = string.IsNullOrEmpty(filter)
                ? _allPubItems
                : _allPubItems.Where(i => i.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));
            foreach (var m in matches) _visiblePubItems.Add(m);
        }

        private void SelectAllLangs_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _allLangItems) item.IsChecked = true;
            UpdateLangButtonLabel();
            _currentPage = 1;
            ApplyFilters();
        }

        private void ClearLangs_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _allLangItems) item.IsChecked = false;
            UpdateLangButtonLabel();
            _currentPage = 1;
            ApplyFilters();
        }

        private void LangSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string filter = LangSearchBox?.Text?.Trim() ?? "";
            _visibleLangItems.Clear();
            var matches = string.IsNullOrEmpty(filter)
                ? _allLangItems
                : _allLangItems.Where(i => i.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));
            foreach (var m in matches) _visibleLangItems.Add(m);
        }

        private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            _currentPage = 1;
            ApplyFilters();
        }

        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded) return;

            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            try
            {
                await Task.Delay(250, token);
                if (!token.IsCancellationRequested)
                {
                    _currentPage = 1;
                    ApplyFilters();
                }
            }
            catch (TaskCanceledException) { }
        }

        private void ResetFilters_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            _isLoaded = false;
            if (SearchBox != null) SearchBox.Text = "";
            _selectedSystems.Clear();

            foreach (var item in _allGenreItems) item.IsChecked = false;
            foreach (var item in _allDevItems) item.IsChecked = false;
            foreach (var item in _allPubItems) item.IsChecked = false;
            foreach (var item in _allLangItems) item.IsChecked = false;

            UpdateGenreButtonLabel();
            UpdateDevButtonLabel();
            UpdatePubButtonLabel();
            UpdateLangButtonLabel();

            UpdateSystemButtonsUI();
            if (SortComboBox != null) SortComboBox.SelectedIndex = 0;
            _currentPage = 1;
            _isLoaded = true;

            ApplyFilters();
        }

        private async void RefreshDb_Click(object sender, RoutedEventArgs e)
        {
            if (RefreshDbButton != null) RefreshDbButton.IsEnabled = false;
            try
            {
                if (RefreshDbTextBlock != null) RefreshDbTextBlock.Text = "Обновление...";
                await Task.Run(async () =>
                {
                    await App.NintendoLibrary.ReloadAllDatabasesAsync(App.TitleDb);
                });

                InitializeSystemTabsAndDropdown();
                PopulateFilterDropdowns();
                ApplyFilters();
            }
            catch (Exception ex)
            {
                App.Logger.Log($"Ошибка обновления базы данных: {ex.Message}", Models.LogLevel.Error);
            }
            finally
            {
                if (RefreshDbTextBlock != null) RefreshDbTextBlock.Text = "Обновить базу";
                if (RefreshDbButton != null) RefreshDbButton.IsEnabled = true;
            }
        }

        private async void ApplyFilters()
        {
            if (!_isLoaded || GamesGridView == null || TotalGamesCountTextBlock == null) return;

            _filterCts?.Cancel();
            _filterCts = new CancellationTokenSource();
            var token = _filterCts.Token;

            var systems = _selectedSystems.Count > 0 ? _selectedSystems.ToList() : null;
            var genres = _allGenreItems.Where(i => i.IsChecked).Select(i => i.Name).ToList();
            var devs = _allDevItems.Where(i => i.IsChecked).Select(i => i.Name).ToList();
            var pubs = _allPubItems.Where(i => i.IsChecked).Select(i => i.Name).ToList();
            var langs = _allLangItems.Where(i => i.IsChecked).Select(i => i.Name).ToList();
            string? search = SearchBox?.Text?.Trim();

            string sortBy = "Title";
            if (SortComboBox?.SelectedItem is ComboBoxItem cbi && cbi.Tag is string tag)
            {
                sortBy = tag;
            }

            try
            {
                var filtered = await Task.Run(() =>
                {
                    return App.NintendoLibrary.QueryGames(
                        systems, 
                        genres.Count > 0 ? genres : null, 
                        devs.Count > 0 ? devs : null, 
                        pubs.Count > 0 ? pubs : null, 
                        langs.Count > 0 ? langs : null, 
                        search, 
                        sortBy);
                }, token);

                if (token.IsCancellationRequested) return;

                _allFilteredGames = filtered;
                string formattedCount = NintendoLibraryService.FormatNumber(_allFilteredGames.Count);
                TotalGamesCountTextBlock.Text = $"Игр: {formattedCount}";

                UpdatePagination();
            }
            catch (TaskCanceledException) { }
        }

        #region Pagination Logic

        private void UpdatePagination()
        {
            if (_pageSize > 0)
            {
                _totalPages = Math.Max(1, (int)Math.Ceiling(_allFilteredGames.Count / (double)_pageSize));
            }
            else
            {
                _totalPages = 1;
            }

            _currentPage = Math.Clamp(_currentPage, 1, _totalPages);

            _pagedGames.Clear();
            var pageSlice = (_pageSize > 0)
                ? _allFilteredGames.Skip((_currentPage - 1) * _pageSize).Take(_pageSize)
                : _allFilteredGames;

            foreach (var item in pageSlice)
            {
                _pagedGames.Add(item);
            }

            UpdatePaginationControls();
        }

        private void UpdatePaginationControls()
        {
            if (FirstPageButton != null) FirstPageButton.IsEnabled = _currentPage > 1;
            if (PrevPageButton != null) PrevPageButton.IsEnabled = _currentPage > 1;
            if (NextPageButton != null) NextPageButton.IsEnabled = _currentPage < _totalPages;
            if (LastPageButton != null) LastPageButton.IsEnabled = _currentPage < _totalPages;

            RenderPageNumberButtons();
            UpdatePageSummaryText();
        }

        private void RenderPageNumberButtons()
        {
            if (PageNumbersStackPanel == null) return;

            PageNumbersStackPanel.Children.Clear();

            if (_totalPages <= 1) return;

            int startPage = Math.Max(1, _currentPage - 2);
            int endPage = Math.Min(_totalPages, _currentPage + 2);

            if (startPage > 1)
            {
                PageNumbersStackPanel.Children.Add(CreatePageButton(1));
                if (startPage > 2)
                {
                    PageNumbersStackPanel.Children.Add(new TextBlock 
                    { 
                        Text = "...", 
                        VerticalAlignment = VerticalAlignment.Center, 
                        Margin = new Thickness(4, 0, 4, 0),
                        Foreground = Application.Current.Resources["TextFillColorSecondaryBrush"] as Brush 
                    });
                }
            }

            for (int p = startPage; p <= endPage; p++)
            {
                PageNumbersStackPanel.Children.Add(CreatePageButton(p));
            }

            if (endPage < _totalPages)
            {
                if (endPage < _totalPages - 1)
                {
                    PageNumbersStackPanel.Children.Add(new TextBlock 
                    { 
                        Text = "...", 
                        VerticalAlignment = VerticalAlignment.Center, 
                        Margin = new Thickness(4, 0, 4, 0),
                        Foreground = Application.Current.Resources["TextFillColorSecondaryBrush"] as Brush 
                    });
                }
                PageNumbersStackPanel.Children.Add(CreatePageButton(_totalPages));
            }
        }

        private Button CreatePageButton(int pageNumber)
        {
            bool isCurrent = pageNumber == _currentPage;
            var accentBg = Application.Current.Resources["AccentButtonBackground"] as Brush ?? new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
            var accentFg = Application.Current.Resources["AccentButtonForeground"] as Brush ?? new SolidColorBrush(Microsoft.UI.Colors.White);
            var defaultBg = Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] as Brush ?? new SolidColorBrush(Microsoft.UI.Colors.Transparent);
            var defaultFg = Application.Current.Resources["TextFillColorPrimaryBrush"] as Brush ?? new SolidColorBrush(Microsoft.UI.Colors.White);

            var btn = new Button
            {
                Content = NintendoLibraryService.FormatNumber(pageNumber),
                Tag = pageNumber,
                Width = 36,
                Height = 32,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(6),
                FontWeight = isCurrent ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal,
                Background = isCurrent ? accentBg : defaultBg,
                Foreground = isCurrent ? accentFg : defaultFg
            };

            btn.Click += (s, e) =>
            {
                if (s is Button b && b.Tag is int targetPage && targetPage != _currentPage)
                {
                    _currentPage = targetPage;
                    UpdatePagination();
                }
            };

            return btn;
        }

        private void UpdatePageSummaryText()
        {
            if (PageInfoTextBlock == null) return;

            if (_allFilteredGames.Count == 0)
            {
                PageInfoTextBlock.Text = "Игр не найдено";
                return;
            }

            int from = (_pageSize > 0) ? ((_currentPage - 1) * _pageSize + 1) : 1;
            int to = (_pageSize > 0) ? Math.Min(_allFilteredGames.Count, _currentPage * _pageSize) : _allFilteredGames.Count;

            string fCur = NintendoLibraryService.FormatNumber(_currentPage);
            string fTot = NintendoLibraryService.FormatNumber(_totalPages);
            string fFrom = NintendoLibraryService.FormatNumber(from);
            string fTo = NintendoLibraryService.FormatNumber(to);
            string fAll = NintendoLibraryService.FormatNumber(_allFilteredGames.Count);

            PageInfoTextBlock.Text = $"Страница {fCur} из {fTot} (Показано {fFrom}–{fTo} из {fAll})";
        }

        private void PageSizeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;

            if (PageSizeComboBox?.SelectedItem is ComboBoxItem cbi && cbi.Tag is string tagStr && int.TryParse(tagStr, out int size))
            {
                _pageSize = size;
                _currentPage = 1;
                UpdatePagination();
            }
        }

        private void FirstPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage = 1;
                UpdatePagination();
            }
        }

        private void PrevPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                UpdatePagination();
            }
        }

        private void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                UpdatePagination();
            }
        }

        private void LastPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage = _totalPages;
                UpdatePagination();
            }
        }

        private void GoToPageTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                JumpToEnteredPage();
                e.Handled = true;
            }
        }

        private void GoToPageButton_Click(object sender, RoutedEventArgs e)
        {
            JumpToEnteredPage();
        }

        private void JumpToEnteredPage()
        {
            if (GoToPageTextBox == null) return;
            if (int.TryParse(GoToPageTextBox.Text.Trim(), out int targetPage))
            {
                if (targetPage >= 1 && targetPage <= _totalPages)
                {
                    _currentPage = targetPage;
                    UpdatePagination();
                }
                else if (targetPage < 1)
                {
                    _currentPage = 1;
                    UpdatePagination();
                }
                else if (targetPage > _totalPages)
                {
                    _currentPage = _totalPages;
                    UpdatePagination();
                }
            }
        }

        #endregion

        private void Grid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Grid grid && grid.RenderTransform is ScaleTransform scale)
            {
                scale.ScaleX = 1.03;
                scale.ScaleY = 1.03;
            }
        }

        private void Grid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            if (sender is Grid grid && grid.RenderTransform is ScaleTransform scale)
            {
                scale.ScaleX = 1.0;
                scale.ScaleY = 1.0;
            }
        }

        private async void GamesGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is NintendoGameEntry game)
            {
                _selectedGame = game;

                DialogTitleTextBlock.Text = game.Title;
                DialogSystemTextBlock.Text = game.System;
                DialogGenreTextBlock.Text = game.Genre;
                DialogReleaseDateTextBlock.Text = NintendoLibraryService.FormatDate(game.ReleaseDate);
                DialogDeveloperTextBlock.Text = game.Developer;
                DialogPublisherTextBlock.Text = game.Publisher;
                
                string cleanVer = NintendoLibraryService.FormatVersion(game.Version);
                DialogEditionTextBlock.Text = $"{game.Edition} ({cleanVer})";
                
                DialogRegionPlayersTextBlock.Text = $"{game.Region} | {game.Players}";
                DialogRatingIdTextBlock.Text = $"{game.Rating} | ID: {game.Id}";
                DialogDescriptionTextBlock.Text = game.Description;

                if (!string.IsNullOrEmpty(game.CoverUrl))
                {
                    try
                    {
                        string coverUrl = CoverCacheService.ResolveCoverUrl(game.CoverUrl, game.System, game.Title, game.Id);
                        DialogCoverImage.Source = new BitmapImage(new Uri(coverUrl));
                    }
                    catch
                    {
                        DialogCoverImage.Source = null;
                    }
                }
                else
                {
                    DialogCoverImage.Source = null;
                }

                GameDetailsDialog.XamlRoot = this.XamlRoot;
                await GameDetailsDialog.ShowAsync();
            }
        }

        private async void GameDetailsDialog_SaveCoverClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (_selectedGame == null || string.IsNullOrEmpty(_selectedGame.CoverUrl)) return;

            var deferral = args.GetDeferral();
            try
            {
                string safeName = string.Join("_", _selectedGame.Title.Split(Path.GetInvalidFileNameChars()));
                string? targetFile = await SystemDialogService.SaveFileDialogAsync(
                    "Сохранить обложку игры",
                    $"{safeName}_cover.jpg",
                    "JPEG Image (*.jpg)|*.jpg|PNG Image (*.png)|*.png|All Files (*.*)|*.*");

                if (!string.IsNullOrWhiteSpace(targetFile))
                {
                    string localOrDownloadUrl = await CoverCacheService.GetOrDownloadCoverAsync(
                        _selectedGame.CoverUrl, _selectedGame.System, _selectedGame.Title, _selectedGame.Id);

                    if (File.Exists(localOrDownloadUrl))
                    {
                        var bytes = await File.ReadAllBytesAsync(localOrDownloadUrl);
                        await File.WriteAllBytesAsync(targetFile, bytes);
                    }
                    else
                    {
                        using var httpClient = new System.Net.Http.HttpClient();
                        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                        var bytes = await httpClient.GetByteArrayAsync(localOrDownloadUrl);
                        await File.WriteAllBytesAsync(targetFile, bytes);
                    }

                    App.Logger?.Log($"[GameLibrary] Обложка сохранена: {targetFile}", LogLevel.Success);
                }
            }
            catch (Exception ex)
            {
                App.Logger?.Log($"[GameLibrary] Ошибка сохранения обложки: {ex.Message}", LogLevel.Warning);
            }
            finally
            {
                deferral.Complete();
            }
        }

        private void GameDetailsDialog_CopyDetailsClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (_selectedGame == null) return;

            try
            {
                string cleanVer = NintendoLibraryService.FormatVersion(_selectedGame.Version);
                string cleanDate = NintendoLibraryService.FormatDate(_selectedGame.ReleaseDate);

                var dp = new DataPackage();
                string text = $"Игра: {_selectedGame.Title}\n" +
                              $"Система: {_selectedGame.System}\n" +
                              $"Жанр: {_selectedGame.Genre}\n" +
                              $"Дата выхода: {cleanDate}\n" +
                              $"Разработчик: {_selectedGame.Developer}\n" +
                              $"Издатель: {_selectedGame.Publisher}\n" +
                              $"Издание / Версия: {_selectedGame.Edition} ({cleanVer})\n" +
                              $"Регион: {_selectedGame.Region}\n" +
                              $"ID: {_selectedGame.Id}\n\n" +
                              $"Описание:\n{_selectedGame.Description}";

                dp.SetText(text);
                Clipboard.SetContent(dp);
            }
            catch { }
        }

        private void DialogSearchWeb_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedGame == null) return;

            try
            {
                string query = Uri.EscapeDataString($"Nintendo {_selectedGame.SystemShort} {_selectedGame.Title}");
                var uri = new Uri($"https://www.google.com/search?q={query}");
                _ = Windows.System.Launcher.LaunchUriAsync(uri);
            }
            catch { }
        }
    }
}
