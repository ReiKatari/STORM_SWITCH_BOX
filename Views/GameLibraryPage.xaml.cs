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
        private string _selectedSystem = "Все системы";
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

        private void GameLibraryPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded)
            {
                GamesGridView.ItemsSource = _pagedGames;

                InitializeSystemTabsAndDropdown();
                PopulateFilterDropdowns();
                ApplyLocalization();
                
                _isLoaded = true;

                App.NintendoLibrary.LibraryUpdated += OnLibraryUpdated;
                App.TitleDb.DatabaseLoaded += OnTitleDbLoaded;

                // Асинхронная загрузка базы Switch в фоне без подвисания UI
                _ = App.NintendoLibrary.EnsureSwitchGamesLoadedAsync(App.TitleDb);

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
            if (SystemsStackPanel == null || SystemFilterComboBox == null) return;

            SystemsStackPanel.Children.Clear();
            _systemButtons.Clear();

            var comboItems = new List<string> { "Все системы" };

            // "Все системы" Tab Chip с форматированием пробела в тысячах
            int totalAll = App.NintendoLibrary.GetGameCountForSystem("Все системы");
            string formattedTotal = NintendoLibraryService.FormatNumber(totalAll);
            var allBtn = CreateSystemButton($"🌟 Все системы ({formattedTotal})", "Все системы", true);
            SystemsStackPanel.Children.Add(allBtn);
            _systemButtons.Add(allBtn);

            foreach (var platform in NintendoLibraryService.Platforms)
            {
                int count = App.NintendoLibrary.GetGameCountForSystem(platform.FullName);
                string formattedCount = NintendoLibraryService.FormatNumber(count);
                string chipLabel = $"{platform.FullName} ({formattedCount})";
                var btn = CreateSystemButton(chipLabel, platform.FullName, false);
                SystemsStackPanel.Children.Add(btn);
                _systemButtons.Add(btn);

                comboItems.Add(platform.FullName);
            }

            SystemFilterComboBox.ItemsSource = comboItems;
            SystemFilterComboBox.SelectedIndex = 0;
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
                _selectedSystem = sysKey;
                UpdateSystemButtonsUI();

                if (SystemFilterComboBox != null)
                {
                    int idx = 0;
                    for (int i = 0; i < SystemFilterComboBox.Items.Count; i++)
                    {
                        if ((SystemFilterComboBox.Items[i] as string) == _selectedSystem)
                        {
                            idx = i;
                            break;
                        }
                    }
                    if (SystemFilterComboBox.SelectedIndex != idx)
                    {
                        SystemFilterComboBox.SelectedIndex = idx;
                        return; // SelectionChanged вызовет ApplyFilters
                    }
                }

                PopulateFilterDropdowns();
                _currentPage = 1;
                ApplyFilters();
            }
        }

        private void SystemFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || SystemFilterComboBox?.SelectedItem is not string selectedSys) return;

            _selectedSystem = selectedSys;
            UpdateSystemButtonsUI();
            PopulateFilterDropdowns();
            _currentPage = 1;
            ApplyFilters();
        }

        private void UpdateSystemButtonsUI()
        {
            var accentBg = Application.Current.Resources["AccentButtonBackground"] as Brush ?? new SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
            var accentFg = Application.Current.Resources["AccentButtonForeground"] as Brush ?? new SolidColorBrush(Microsoft.UI.Colors.White);
            var secondaryFg = Application.Current.Resources["TextFillColorSecondaryBrush"] as Brush ?? new SolidColorBrush(Microsoft.UI.Colors.Gray);
            var transparent = new SolidColorBrush(Microsoft.UI.Colors.Transparent);

            foreach (var btn in _systemButtons)
            {
                bool isSel = (btn.Tag as string) == _selectedSystem;
                btn.Background = isSel ? accentBg : transparent;
                btn.Foreground = isSel ? accentFg : secondaryFg;
            }
        }

        private void PopulateFilterDropdowns()
        {
            bool previousLoaded = _isLoaded;
            _isLoaded = false;

            try
            {
                // Genres
                if (GenreComboBox != null)
                {
                    var genres = App.NintendoLibrary.GetDistinctGenres(_selectedSystem);
                    GenreComboBox.ItemsSource = genres;
                    GenreComboBox.SelectedIndex = 0;
                }

                // Developers
                if (DeveloperComboBox != null)
                {
                    var devs = App.NintendoLibrary.GetDistinctDevelopers(_selectedSystem);
                    DeveloperComboBox.ItemsSource = devs;
                    DeveloperComboBox.SelectedIndex = 0;
                }

                // Publishers
                if (PublisherComboBox != null)
                {
                    var pubs = App.NintendoLibrary.GetDistinctPublishers(_selectedSystem);
                    PublisherComboBox.ItemsSource = pubs;
                    PublisherComboBox.SelectedIndex = 0;
                }
            }
            finally
            {
                _isLoaded = previousLoaded;
            }
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
            _selectedSystem = "Все системы";
            if (SystemFilterComboBox != null) SystemFilterComboBox.SelectedIndex = 0;
            UpdateSystemButtonsUI();
            PopulateFilterDropdowns();
            if (SortComboBox != null) SortComboBox.SelectedIndex = 0;
            _currentPage = 1;
            _isLoaded = true;

            ApplyFilters();
        }

        private async void ApplyFilters()
        {
            if (!_isLoaded || GamesGridView == null || TotalGamesCountTextBlock == null) return;

            _filterCts?.Cancel();
            _filterCts = new CancellationTokenSource();
            var token = _filterCts.Token;

            string selSys = _selectedSystem;
            string? genre = GenreComboBox?.SelectedItem as string;
            string? dev = DeveloperComboBox?.SelectedItem as string;
            string? pub = PublisherComboBox?.SelectedItem as string;
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
                    return App.NintendoLibrary.QueryGames(selSys, genre, dev, pub, search, sortBy);
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
                var savePicker = new FileSavePicker();
                savePicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                savePicker.FileTypeChoices.Add("PNG Image", new List<string> { ".png" });
                savePicker.FileTypeChoices.Add("JPEG Image", new List<string> { ".jpg" });
                
                string safeName = string.Join("_", _selectedGame.Title.Split(Path.GetInvalidFileNameChars()));
                savePicker.SuggestedFileName = $"{safeName}_cover";

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

                var file = await savePicker.PickSaveFileAsync();
                if (file != null)
                {
                    string localOrDownloadUrl = await CoverCacheService.GetOrDownloadCoverAsync(
                        _selectedGame.CoverUrl, _selectedGame.System, _selectedGame.Title, _selectedGame.Id);

                    if (File.Exists(localOrDownloadUrl))
                    {
                        var bytes = await File.ReadAllBytesAsync(localOrDownloadUrl);
                        await Windows.Storage.FileIO.WriteBytesAsync(file, bytes);
                    }
                    else
                    {
                        using var httpClient = new System.Net.Http.HttpClient();
                        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                        var bytes = await httpClient.GetByteArrayAsync(localOrDownloadUrl);
                        await Windows.Storage.FileIO.WriteBytesAsync(file, bytes);
                    }
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
