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
        private ObservableCollection<CatalogItem> _allCatalogItems = new();
        private CatalogScannerService _scannerService;
        private CancellationTokenSource? _scanCts;
        private CatalogItem? _selectedCatalogItem;

        public CatalogPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            _scannerService = new CatalogScannerService(App.Keys);
            CatalogGridView.ItemsSource = _allCatalogItems;
            
            // Загружаем папки
            FoldersListView.ItemsSource = App.Settings.Current.CatalogFolders;
            this.Loaded += CatalogPage_Loaded;
            App.TaskCompleted += App_TaskCompleted;
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
                
                await _scannerService.ScanSingleFileAsync(finalPath, _allCatalogItems, _scanCts.Token);
                
                // Refresh UI binding so the newly added item shows up immediately even if a search filter is active
                App.MainDispatcher?.TryEnqueue(() => 
                {
                    CatalogGridView.ItemsSource = null;
                    CatalogGridView.ItemsSource = _allCatalogItems;
                    SearchBox_TextChanged(null!, null!);
                });
            }
        }

        private void CatalogPage_Loaded(object sender, RoutedEventArgs e)
        {
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
            var folderPicker = new Windows.Storage.Pickers.FolderPicker();
            folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
            folderPicker.FileTypeFilter.Add("*");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                if (!App.Settings.Current.CatalogFolders.Contains(folder.Path))
                {
                    App.Settings.Current.CatalogFolders.Add(folder.Path);
                    FoldersListView.ItemsSource = null;
                    FoldersListView.ItemsSource = App.Settings.Current.CatalogFolders;
                    _ = App.Settings.SaveAsync();
                }
            }
        }

        private void RemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            if (FoldersListView.SelectedItem is string selectedPath)
            {
                App.Settings.Current.CatalogFolders.Remove(selectedPath);
                FoldersListView.ItemsSource = null;
                FoldersListView.ItemsSource = App.Settings.Current.CatalogFolders;
                _ = App.Settings.SaveAsync();
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

            bool success = await App.TitleDb.UpdateDatabaseAsync(progress);
            
            App.MainDispatcher?.TryEnqueue(() =>
            {
                ScanStatusText.Text = success ? "База TitleDB успешно обновлена!" : "Ошибка скачивания базы!";
                ScanProgress.IsActive = false;
                UpdateDbButton.IsEnabled = true;
                UpdateDbStatusUI();
            });
        }

        private async void StartScan()
        {
            _scanCts?.Cancel();
            _scanCts = new CancellationTokenSource();
            _allCatalogItems.Clear();
            SearchBox.Text = "";

            BrowseButton.IsEnabled = false;
            ScanProgress.IsActive = true;
            
            var folders = App.Settings.Current.CatalogFolders.ToList();
            ScanStatusText.Text = $"Подготовка к сканированию ({folders.Count} папок)...";

            try
            {
                foreach (var path in folders)
                {
                    if (System.IO.Directory.Exists(path))
                    {
                        ScanStatusText.Text = $"Сканирование: {System.IO.Path.GetFileName(path)}...";
                        await _scannerService.ScanDirectoryAsync(path, _allCatalogItems, _scanCts.Token);
                    }
                }
                ScanStatusText.Text = $"Найдено игр: {_allCatalogItems.Count}";
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
                BrowseButton.IsEnabled = true;
                ScanProgress.IsActive = false;
            }
        }

        private CancellationTokenSource? _searchCts;

        private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox.Text.Trim();
            
            _searchCts?.Cancel();
            _searchCts = new CancellationTokenSource();
            var token = _searchCts.Token;

            if (string.IsNullOrWhiteSpace(query))
            {
                CatalogGridView.ItemsSource = _allCatalogItems;
                return;
            }

            try
            {
                await Task.Delay(300, token); // Debounce
                if (token.IsCancellationRequested) return;

                var filtered = await Task.Run(() => 
                {
                    var itemsCopy = _allCatalogItems.ToList();
                    var local = itemsCopy.Where(i => 
                        (i.TitleName != null && i.TitleName.Contains(query, StringComparison.OrdinalIgnoreCase)) || 
                        (i.TitleId != null && i.TitleId.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                        (i.Publisher != null && i.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();

                    // Ищем в онлайне
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
                    return local;
                }, token);

                if (!token.IsCancellationRequested)
                {
                    CatalogGridView.ItemsSource = new ObservableCollection<CatalogItem>(filtered);
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

        private void CatalogGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is CatalogItem item)
            {
                _selectedCatalogItem = item;
                DetailTitle.Text = item.TitleName ?? "Unknown";
                DetailPublisher.Text = item.Publisher ?? "Unknown";
                DetailDeveloper.Text = !string.IsNullOrEmpty(item.Developer) ? item.Developer : "Unknown";
                DetailCategory.Text = !string.IsNullOrEmpty(item.Category) ? item.Category : "N/A";
                DetailReleaseDate.Text = !string.IsNullOrEmpty(item.ReleaseDate) ? item.ReleaseDate : "N/A";
                
                DetailDisplayVersion.Text = item.Version ?? "v0";
                DetailVersionCode.Text = !string.IsNullOrEmpty(item.VersionCode) ? item.VersionCode : "0";
                
                DetailRegions.Text = item.Regions ?? "N/A";
                DetailDlcCount.Text = item.DlcCount.ToString();

                DetailTitleId.Text = item.TitleId ?? "0000000000000000";
                DetailSize.Text = item.FileSize ?? "0 MB";
                DetailCover.Source = item.CoverImage;
                
                DetailRating.Text = !string.IsNullOrEmpty(item.RatingAge) ? item.RatingAge : "N/A";
                DetailVideoCap.Text = item.VideoCapture ?? "N/A";
                DetailSaveSize.Text = item.SaveDataSize ?? "N/A";
                DetailLangs.Text = item.SupportedLanguages ?? "N/A";
                
                DetailIntro.Text = item.Intro ?? "";
                DetailIntro.Visibility = string.IsNullOrEmpty(item.Intro) ? Visibility.Collapsed : Visibility.Visible;
                
                DetailDescription.Text = item.Description ?? "";
                
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

        private void ScreenshotsGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is Microsoft.UI.Xaml.Media.Imaging.BitmapImage bmp)
            {
                FullscreenImage.Source = bmp;
                FullscreenImageOverlay.Visibility = Visibility.Visible;
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
    }
}
