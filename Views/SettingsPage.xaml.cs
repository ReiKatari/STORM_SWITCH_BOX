using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace StormSwitchBox.Views
{
    public sealed partial class SettingsPage : Page
    {
        public int MaxCores => Environment.ProcessorCount;
        public Models.AppSettings Settings => App.Settings.Current;
        public Visibility KeysSelectedVisibility => string.IsNullOrEmpty(App.Settings.Current.KeysPath) ? Visibility.Collapsed : Visibility.Visible;

        public SettingsPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;

            int level = App.Settings.Current.CompressionLevel;
            if (level == 0) level = 18; // Default
            if (level <= 3) CompressionCombo.SelectedIndex = 0;
            else if (level <= 10) CompressionCombo.SelectedIndex = 1;
            else if (level <= 18) CompressionCombo.SelectedIndex = 2;
            else CompressionCombo.SelectedIndex = 3;

            // RsvCap combo initialization
            int rsvVal = App.Settings.Current.RsvCap;
            if (rsvVal == 251658240) RsvCapCombo.SelectedIndex = 1;
            else if (rsvVal == 234880824) RsvCapCombo.SelectedIndex = 2;
            else if (rsvVal == 218103408) RsvCapCombo.SelectedIndex = 3;
            else if (rsvVal == 134217728) RsvCapCombo.SelectedIndex = 4;
            else if (rsvVal == 65796) RsvCapCombo.SelectedIndex = 5;
            else RsvCapCombo.SelectedIndex = 0;

            // AccentColor combo initialization
            string color = App.Settings.Current.AccentColorTheme ?? "Default";
            if (color == "#0078D4") AccentColorCombo.SelectedIndex = 1;
            else if (color == "#107C41") AccentColorCombo.SelectedIndex = 2;
            else if (color == "#8E44AD") AccentColorCombo.SelectedIndex = 3;
            else if (color == "#E67E22") AccentColorCombo.SelectedIndex = 4;
            else if (color == "#E74C3C") AccentColorCombo.SelectedIndex = 5;
            else AccentColorCombo.SelectedIndex = 0;

            // Visual Theme combo initialization
            string th = App.Settings.Current.AppTheme ?? "STORM MIDNIGHT";
            if (th == "STORM NIGHT") ThemeCombo.SelectedIndex = 1;
            else if (th == "STORM DAY") ThemeCombo.SelectedIndex = 2;
            else if (th == "STORM CYBERPUNK") ThemeCombo.SelectedIndex = 3;
            else ThemeCombo.SelectedIndex = 0;

            // Watch Folder combos initialization
            int wfAction = App.Settings.Current.WatchFolderAction;
            if (wfAction >= 0 && wfAction <= 5) WatchFolderActionCombo.SelectedIndex = wfAction;
            else WatchFolderActionCombo.SelectedIndex = 0;

            int wfFormat = App.Settings.Current.WatchFolderFormat;
            if (wfFormat >= 0 && wfFormat <= 3) WatchFolderFormatCombo.SelectedIndex = wfFormat;
            else WatchFolderFormatCombo.SelectedIndex = 0;

            UpdateWatchFolderBadge();

            InitializeLanguages();
            PopulateKeysVersion(App.Settings.Current.KeysVersion ?? "");
        }

        private void UpdateWatchFolderBadge()
        {
            if (WatchFolderSummaryBadge == null) return;
            string actionText = "Сжатие";
            if (WatchFolderActionCombo?.SelectedItem is ComboBoxItem aItem && aItem.Content != null)
                actionText = aItem.Content.ToString()!;

            string formatText = "NSZ";
            if (WatchFolderFormatCombo?.SelectedItem is ComboBoxItem fItem && fItem.Content != null)
                formatText = fItem.Content.ToString()!;

            WatchFolderSummaryBadge.Text = $"⚡ Авто-обработка: {actionText} в {formatText}";
        }

        private async void WatchFolderActionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WatchFolderActionCombo.SelectedItem is ComboBoxItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int val))
            {
                App.Settings.Current.WatchFolderAction = val;
                await App.Settings.SaveAsync();
                UpdateWatchFolderBadge();
            }
        }

        private async void WatchFolderFormatCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WatchFolderFormatCombo.SelectedItem is ComboBoxItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int val))
            {
                App.Settings.Current.WatchFolderFormat = val;
                await App.Settings.SaveAsync();
                UpdateWatchFolderBadge();
            }
        }

        private async void RsvCapCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RsvCapCombo.SelectedItem is ComboBoxItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int val))
            {
                App.Settings.Current.RsvCap = val;
                await App.Settings.SaveAsync();
            }
        }

        private async void WatchFolderToggle_Toggled(object sender, RoutedEventArgs e)
        {
            await App.Settings.SaveAsync();
            if (App.WatchFolderService != null)
            {
                if (App.Settings.Current.EnableWatchFolder)
                {
                    App.WatchFolderService.Start();
                    // При включении — сразу сканируем существующие файлы
                    await ScanWatchFolderAsync();
                }
                else
                {
                    App.WatchFolderService.Stop();
                }
            }
        }

        private async void SelectWatchFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var folderPicker = new Windows.Storage.Pickers.FolderPicker();
                folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
                folderPicker.FileTypeFilter.Add("*");

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(folderPicker, hwnd);

                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    App.Settings.Current.WatchFolder = folder.Path;
                    WatchFolderBox.Text = folder.Path;
                    await App.Settings.SaveAsync();

                    if (App.WatchFolderService != null && App.Settings.Current.EnableWatchFolder)
                    {
                        App.WatchFolderService.Start();
                    }
                }
            }
            catch { }
        }

        private void WatchFolderBox_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Выбрать как «Умную» папку";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsContentVisible = true;
        }

        private async void WatchFolderBox_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0)
                {
                    var item = items[0];
                    string path = item.Path;
                    if (System.IO.File.Exists(path))
                    {
                        path = System.IO.Path.GetDirectoryName(path) ?? path;
                    }
                    if (System.IO.Directory.Exists(path))
                    {
                        App.Settings.Current.WatchFolder = path;
                        WatchFolderBox.Text = path;
                        await App.Settings.SaveAsync();
                        App.Logger.Log($"[WatchFolder] Папка установлена перетягиванием: {path}", Models.LogLevel.Success);

                        if (App.WatchFolderService != null && App.Settings.Current.EnableWatchFolder)
                        {
                            App.WatchFolderService.Start();
                        }
                    }
                }
            }
        }

        private async Task ScanWatchFolderAsync()
        {
            var settings = App.Settings.Current;
            string watchPath = settings.WatchFolder;

            if (string.IsNullOrWhiteSpace(watchPath) || !System.IO.Directory.Exists(watchPath))
            {
                App.Logger.Log("[WatchFolder] Папка не указана или не существует", Models.LogLevel.Warning);
                return;
            }

            try
            {
                string[] supportedExts = { ".nsp", ".nsz", ".xci", ".xcz" };
                int totalTasks = 0;

                // 1. Файлы в корне watchPath
                var rootFiles = new System.Collections.Generic.List<string>();
                foreach (var file in System.IO.Directory.EnumerateFiles(watchPath))
                {
                    string ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                    if (Array.Exists(supportedExts, x => x == ext))
                    {
                        var fi = new System.IO.FileInfo(file);
                        if (fi.Length > 0) rootFiles.Add(file);
                    }
                }

                if (rootFiles.Count > 0)
                {
                    await App.TasksVM.AddDroppedFilesBatchAsync(rootFiles);
                    totalTasks++;
                    App.Logger.Log($"[WatchFolder] Корень: {rootFiles.Count} файлов", Models.LogLevel.Info);
                }

                // 2. Каждая подпапка — передаём как директорию целиком
                // AddDroppedFileAsync сам сканирует содержимое, группирует по TitleID,
                // обнаруживает romfs/exefs и формирует комплектные задачи
                var subDirs = System.IO.Directory.GetDirectories(watchPath);
                foreach (var subDir in subDirs)
                {
                    // Проверяем, что подпапка содержит хоть что-то полезное
                    bool hasGameFiles = false;
                    try
                    {
                        foreach (var file in System.IO.Directory.EnumerateFiles(subDir, "*.*", System.IO.SearchOption.AllDirectories))
                        {
                            string ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                            if (Array.Exists(supportedExts, x => x == ext))
                            {
                                hasGameFiles = true;
                                break;
                            }
                        }
                    }
                    catch { }

                    if (hasGameFiles)
                    {
                        await App.TasksVM.AddDroppedFilesBatchAsync(new System.Collections.Generic.List<string> { subDir });
                        totalTasks++;
                        string folderName = System.IO.Path.GetFileName(subDir);
                        App.Logger.Log($"[WatchFolder] Подпапка «{folderName}» → задача добавлена", Models.LogLevel.Info);
                    }
                }

                if (totalTasks == 0)
                {
                    App.Logger.Log($"[WatchFolder] В папке «{watchPath}» не найдено файлов NSP/NSZ/XCI/XCZ", Models.LogLevel.Warning);
                }
                else
                {
                    string[] actions = { "Сжатие", "Распаковка", "Упаковка", "Конвертация", "Мульти-контент", "Проверка" };
                    string[] formats = { "NSP", "NSZ", "XCI", "XCZ" };
                    string actionStr = settings.WatchFolderAction >= 0 && settings.WatchFolderAction < actions.Length ? actions[settings.WatchFolderAction] : "Обработка";
                    string formatStr = settings.WatchFolderFormat >= 0 && settings.WatchFolderFormat < formats.Length ? formats[settings.WatchFolderFormat] : "NSP";

                    App.Logger.Log($"[WatchFolder] Добавлено {totalTasks} задач → {actionStr} в {formatStr}", Models.LogLevel.Success);

                    // Автозапуск задач
                    await App.TasksVM.StartAllTasksAsync();
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[WatchFolder] Ошибка сканирования: {ex.Message}", Models.LogLevel.Warning);
            }
        }

        private async void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThemeCombo.SelectedItem is ComboBoxItem item && item.Tag is string tagStr)
            {
                App.Settings.Current.AppTheme = tagStr;
                await App.Settings.SaveAsync();
                Services.ThemeService.ApplyTheme(tagStr);
            }
        }

        private async void AccentColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AccentColorCombo.SelectedItem is ComboBoxItem item && item.Tag is string tagStr)
            {
                App.Settings.Current.AccentColorTheme = tagStr;
                await App.Settings.SaveAsync();
                Services.ThemeService.ApplyAccentColor(tagStr);
            }
        }

        private void InitializeLanguages()
        {
            var langs = new (string Name, string[] Codes)[]
            {
                ("Русский", new[] { "ru", "ru-RU" }),
                ("Английский", new[] { "en", "en-US", "en-GB" }),
                ("Японский", new[] { "ja", "ja-JP", "Japanese" }),
                ("Испанский", new[] { "es", "es-ES", "es-MX", "Spanish" }),
                ("Французский", new[] { "fr", "fr-FR", "fr-CA", "French" }),
                ("Немецкий", new[] { "de", "de-DE", "German" }),
                ("Итальянский", new[] { "it", "it-IT", "Italian" }),
                ("Нидерландский", new[] { "nl", "nl-NL", "Dutch" }),
                ("Португальский", new[] { "pt", "pt-BR", "pt-PT", "Portuguese" }),
                ("Корейский", new[] { "ko", "ko-KR", "Korean" }),
                ("Китайский (упр.)", new[] { "zh-Hans", "zh-CN" }),
                ("Китайский (трад.)", new[] { "zh-Hant", "zh-TW" })
            };

            foreach (var lang in langs)
            {
                var cb = new CheckBox { Content = lang.Name, Margin = new Thickness(0, 0, 16, 8) };
                
                bool isChecked = false;
                if (Settings.KeepLanguages != null)
                {
                    foreach (var code in lang.Codes)
                    {
                        if (Settings.KeepLanguages.Contains(code))
                        {
                            isChecked = true;
                            break;
                        }
                    }
                }
                cb.IsChecked = isChecked;

                cb.Checked += async (s, e) =>
                {
                    if (Settings.KeepLanguages == null) Settings.KeepLanguages = new System.Collections.Generic.List<string>();
                    foreach (var code in lang.Codes)
                    {
                        if (!Settings.KeepLanguages.Contains(code)) Settings.KeepLanguages.Add(code);
                    }
                    await App.Settings.SaveAsync();
                };

                cb.Unchecked += async (s, e) =>
                {
                    if (Settings.KeepLanguages != null)
                    {
                        foreach (var code in lang.Codes)
                        {
                            Settings.KeepLanguages.Remove(code);
                        }
                        await App.Settings.SaveAsync();
                    }
                };

                LanguageItemsControl.Items.Add(cb);
            }
        }

        // ===== Выбор файла ключей =====
        private void KeysFile_DragOver(object sender, Microsoft.UI.Xaml.DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Загрузить ключи";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsGlyphVisible = true;
            // Подсветка при наведении
            if (sender is Grid grid)
            {
                grid.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen);
            }
        }

        private async void KeysFile_Drop(object sender, Microsoft.UI.Xaml.DragEventArgs e)
        {
            // Сброс подсветки
            if (sender is Grid grid)
            {
                grid.BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
            }
            
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0 && items[0] is Windows.Storage.StorageFile file)
                {
                    string ext = System.IO.Path.GetExtension(file.Path).ToLower();
                    if (ext == ".keys" || ext == ".txt" || ext == ".dat")
                    {
                        App.Settings.Current.KeysPath = file.Path;
                        await App.Settings.SaveAsync();
                        App.EnsureUserKeysAvailable();
                        App.Logger.Log($"Файл ключей применен (drag-and-drop): {file.Path}", Models.LogLevel.Success);
                        this.Bindings.Update();
                    }
                    else
                    {
                        App.Logger.Log($"Неподдерживаемый формат файла ключей: {ext}", Models.LogLevel.Warning);
                    }
                }
            }
        }

        private async void SelectKeysButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
                picker.FileTypeFilter.Add(".keys");
                picker.FileTypeFilter.Add(".txt");

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    App.Settings.Current.KeysPath = file.Path;
                    await App.Settings.SaveAsync();
                    App.EnsureUserKeysAvailable();
                    App.Logger.Log($"Файл ключей применен и расслан во все модули: {file.Path}", Models.LogLevel.Success);
                    this.Bindings.Update();
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"Ошибка выбора файла ключей: {ex.Message}", Models.LogLevel.Warning);
                
                var dialog = new ContentDialog
                {
                    Title = "Укажите путь к файлу ключей",
                    CloseButtonText = "Отмена",
                    PrimaryButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                var textBox = new TextBox
                {
                    PlaceholderText = @"Например: C:\Switch\prod.keys",
                    Text = App.Settings.Current.KeysPath ?? "",
                    Width = 400
                };
                dialog.Content = textBox;

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text) && System.IO.File.Exists(textBox.Text.Trim()))
                {
                    string filePath = textBox.Text.Trim();
                    App.Settings.Current.KeysPath = filePath;
                    await App.Settings.SaveAsync();
                    App.EnsureUserKeysAvailable();
                    App.Logger.Log($"Файл ключей применен и расслан во все модули: {filePath}", Models.LogLevel.Success);
                    this.Bindings.Update();
                }
            }
        }

        // ===== Выбор выходной папки =====
        private async void SelectOutputFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FolderPicker();
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
                picker.FileTypeFilter.Add("*");

                var window = App.MainWindow;
                if (window != null)
                {
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                }

                var folder = await picker.PickSingleFolderAsync();
                if (folder != null)
                {
                    App.Settings.Current.OutputFolder = folder.Path;
                    OutputFolderBox.Text = folder.Path;
                    await App.Settings.SaveAsync();
                    App.Logger.Log($"Выходная папка: {folder.Path}", Models.LogLevel.Info);
                }
            }
            catch (Exception)
            {
                // Fallback: ручной ввод если FolderPicker не работает
                var dialog = new ContentDialog
                {
                    Title = "Укажите выходную папку",
                    CloseButtonText = "Отмена",
                    PrimaryButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                var textBox = new TextBox
                {
                    PlaceholderText = @"Например: E:\OUT",
                    Text = App.Settings.Current.OutputFolder ?? "",
                    Width = 400
                };
                dialog.Content = textBox;

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
                {
                    App.Settings.Current.OutputFolder = textBox.Text.Trim();
                    OutputFolderBox.Text = textBox.Text.Trim();
                    await App.Settings.SaveAsync();
                    App.Logger.Log($"Выходная папка (вручную): {textBox.Text.Trim()}", Models.LogLevel.Info);
                }
            }
        }

        private async void CompressionCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item)
            {
                if (int.TryParse(item.Tag?.ToString(), out int level))
                {
                    App.Settings.Current.CompressionLevel = level;
                    await App.Settings.SaveAsync();
                }
            }
        }

        private async void Setting_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            await App.Settings.SaveAsync();
        }

        private async void Toggle_Changed(object sender, RoutedEventArgs e)
        {
            await App.Settings.SaveAsync();
        }

        private void OutputFolderBox_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Выбрать как выходную папку";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsContentVisible = true;
        }

        private async void OutputFolderBox_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0)
                {
                    var item = items[0];
                    string path = item.Path;
                    if (System.IO.File.Exists(path))
                    {
                        path = System.IO.Path.GetDirectoryName(path) ?? path;
                    }
                    if (System.IO.Directory.Exists(path))
                    {
                        App.Settings.Current.OutputFolder = path;
                        OutputFolderBox.Text = path;
                        await App.Settings.SaveAsync();
                        App.Logger.Log($"Выходная папка установлена перетягиванием: {path}", Models.LogLevel.Success);
                    }
                }
            }
        }

        private async void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
        {
            CheckUpdatesButton.IsEnabled = false;
            UpdateProgressRing.IsActive = true;
            UpdateProgressRing.Visibility = Visibility.Visible;

            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "StormSwitchBox-Updater");

                var response = await client.GetAsync("https://api.github.com/repos/ReiKatari/STORM_SWITCH_BOX/releases/latest");
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Обновления не найдены",
                        Content = new TextBlock { Text = "У вас установлена актуальная версия STORM SWITCH BOX v4.3.4." },
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                    return;
                }
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Ошибка запроса к GitHub API: {response.ReasonPhrase}");
                }

                var jsonString = await response.Content.ReadAsStringAsync();
                
                using var doc = System.Text.Json.JsonDocument.Parse(jsonString);
                var root = doc.RootElement;
                
                string tagName = root.GetProperty("tag_name").GetString() ?? "";
                string body = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "" : "";
                
                string downloadUrl = "";
                string assetName = "";
                if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var asset in assetsProp.EnumerateArray())
                    {
                        string name = asset.GetProperty("name").GetString() ?? "";
                        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                            assetName = name;
                            break; // Prefer .exe installer
                        }
                        else if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(downloadUrl))
                        {
                            downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                            assetName = name;
                        }
                    }
                }

                string cleanTag = tagName.TrimStart('v');
                var asmVer = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                var currentVer = new Version(asmVer.Major, asmVer.Minor, asmVer.Build);
                string currentVerStr = currentVer.ToString();
                
                if (Version.TryParse(cleanTag, out var latestVer) && latestVer > currentVer)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Доступно новое обновление!",
                        PrimaryButtonText = "Скачать и обновить",
                        CloseButtonText = "Отмена",
                        XamlRoot = this.XamlRoot,
                        Content = new StackPanel
                        {
                            Spacing = 12,
                            Children =
                            {
                                new TextBlock { Text = $"Доступна версия: v{cleanTag} (Текущая: v{currentVerStr})", FontSize = 16, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold },
                                new TextBlock { Text = "Список изменений:", FontSize = 12, Foreground = Application.Current.Resources.TryGetValue("TextFillColorSecondaryBrush", out var resBrush) && resBrush is Microsoft.UI.Xaml.Media.Brush b ? b : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray) },
                                new ScrollViewer
                                {
                                    MaxHeight = 150,
                                    Content = new TextBlock { Text = body, TextWrapping = TextWrapping.Wrap, FontSize = 12 }
                                }
                            }
                        }
                    };

                    var result = await dialog.ShowAsync();
                    if (result == ContentDialogResult.Primary && !string.IsNullOrEmpty(downloadUrl))
                    {
                        await StartDownloadAndUpdateAsync(downloadUrl, assetName);
                    }
                }
                else
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Обновления не найдены",
                        Content = new TextBlock { Text = $"У вас установлена актуальная версия STORM SWITCH BOX v{currentVerStr}." },
                        CloseButtonText = "OK",
                        XamlRoot = this.XamlRoot
                    };
                    await dialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "Ошибка при проверке обновлений",
                    Content = new TextBlock { Text = ex.Message, TextWrapping = TextWrapping.Wrap },
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
            }
            finally
            {
                CheckUpdatesButton.IsEnabled = true;
                UpdateProgressRing.IsActive = false;
                UpdateProgressRing.Visibility = Visibility.Collapsed;
            }
        }

        private async System.Threading.Tasks.Task StartDownloadAndUpdateAsync(string url, string assetName)
        {
            var progressRing = new ProgressRing { IsActive = true, Width = 50, Height = 50, HorizontalAlignment = HorizontalAlignment.Center };
            var progressText = new TextBlock { Text = "Подготовка к скачиванию...", HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,12,0,0) };
            var progressBar = new ProgressBar { Minimum = 0, Maximum = 100, Value = 0, Height = 10, Margin = new Thickness(0,12,0,0), Visibility = Visibility.Collapsed };

            var dialog = new ContentDialog
            {
                Title = "Загрузка обновления...",
                Content = new StackPanel { Children = { progressRing, progressText, progressBar } },
                XamlRoot = this.XamlRoot
            };

            _ = dialog.ShowAsync();

            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Add("User-Agent", "StormSwitchBox-Updater");

                using var response = await client.GetAsync(url, System.Net.Http.HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength;
                progressBar.Visibility = totalBytes.HasValue ? Visibility.Visible : Visibility.Collapsed;
                progressRing.Visibility = totalBytes.HasValue ? Visibility.Collapsed : Visibility.Visible;

                var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), assetName);
                using var fileStream = new System.IO.FileStream(tempPath, System.IO.FileMode.Create, System.IO.FileAccess.Write, System.IO.FileShare.None);
                using var contentStream = await response.Content.ReadAsStreamAsync();

                var buffer = new byte[81920];
                long totalRead = 0;
                int read;

                while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, read);
                    totalRead += read;

                    if (totalBytes.HasValue)
                    {
                        var pct = (double)totalRead / totalBytes.Value * 100;
                        progressBar.Value = pct;
                        progressText.Text = $"Скачано: {totalRead / 1024 / 1024} МБ / {totalBytes.Value / 1024 / 1024} МБ ({pct:F1}%)";
                    }
                    else
                    {
                        progressText.Text = $"Скачано: {totalRead / 1024 / 1024} МБ...";
                    }
                }

                await fileStream.FlushAsync();
                fileStream.Close();

                progressText.Text = "Скачивание завершено. Запуск обновления...";
                await System.Threading.Tasks.Task.Delay(1000);

                var appDir = System.AppDomain.CurrentDomain.BaseDirectory;
                var exePath = System.IO.Path.Combine(appDir, "StormSwitchBox.exe");
                var batchPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ssb_update.bat");

                string batchContent = "";
                if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    batchContent = $@"@echo off
chcp 65001 > nul
echo Завершение работающего приложения...
taskkill /F /IM StormSwitchBox.exe > nul 2>&1
timeout /t 2 /nobreak > nul
echo Извлечение обновления...
powershell -Command ""Expand-Archive -Path '{tempPath}' -DestinationPath '{appDir}' -Force""
echo Запуск новой версии...
start """" ""{exePath}""
del ""{tempPath}""
(goto) 2>nul & del ""%~f0""
";
                }
                else
                {
                    batchContent = $@"@echo off
chcp 65001 > nul
echo Завершение работающего приложения...
taskkill /F /IM StormSwitchBox.exe > nul 2>&1
timeout /t 2 /nobreak > nul
echo Обновление исполняемого файла...
start /wait """" ""{tempPath}"" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /FORCECLOSEAPPLICATIONS /DIR=""{appDir.TrimEnd('\\')}""
echo Запуск новой версии...
start """" ""{exePath}""
del ""{tempPath}""
(goto) 2>nul & del ""%~f0""
";
                }

                await System.IO.File.WriteAllTextAsync(batchPath, batchContent, System.Text.Encoding.UTF8);

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{batchPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                System.Diagnostics.Process.Start(psi);

                dialog.Hide();
                System.Environment.Exit(0);
            }
            catch (Exception ex)
            {
                dialog.Hide();
                var errorDialog = new ContentDialog
                {
                    Title = "Ошибка при загрузке обновления",
                    Content = new TextBlock { Text = ex.Message, TextWrapping = TextWrapping.Wrap },
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await errorDialog.ShowAsync();
            }
        }

        private void OpenLogsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string logsDir = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (System.IO.Directory.Exists(logsDir))
                {
                    System.Diagnostics.Process.Start("explorer.exe", logsDir);
                }
            }
            catch { }
        }

        private bool _isInitializingVersion = false;
        private void PopulateKeysVersion(string ver)
        {
            _isInitializingVersion = true;
            try
            {
                string digits = new string(ver.Where(char.IsDigit).ToArray());
                VerBox1.Text = digits.Length >= 1 ? digits[0].ToString() : "";
                VerBox2.Text = digits.Length >= 2 ? digits[1].ToString() : "";
                VerBox4.Text = digits.Length >= 3 ? digits[2].ToString() : "";
                VerBox6.Text = digits.Length >= 4 ? digits[3].ToString() : "";
            }
            finally
            {
                _isInitializingVersion = false;
            }
        }

        private async void VerBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializingVersion) return;

            if (sender is TextBox tb)
            {
                string val = new string(tb.Text.Where(char.IsDigit).ToArray());
                if (tb.Text != val)
                {
                    tb.Text = val;
                    tb.SelectionStart = val.Length;
                    return;
                }

                if (val.Length == 1)
                {
                    if (tb == VerBox1) VerBox2.Focus(FocusState.Programmatic);
                    else if (tb == VerBox2) VerBox4.Focus(FocusState.Programmatic);
                    else if (tb == VerBox4) VerBox6.Focus(FocusState.Programmatic);
                }
            }

            string v1 = VerBox1.Text;
            string v2 = VerBox2.Text;
            string v4 = VerBox4.Text;
            string v6 = VerBox6.Text;

            string fullVersion = $"{v1}{v2}.{v4}.{v6}";
            if (App.Settings.Current.KeysVersion != fullVersion)
            {
                App.Settings.Current.KeysVersion = fullVersion;
                await App.Settings.SaveAsync();
            }
        }

        private void VerBox_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Back)
            {
                if (sender is TextBox tb && string.IsNullOrEmpty(tb.Text))
                {
                    if (tb == VerBox6)
                    {
                        VerBox4.Focus(FocusState.Programmatic);
                        VerBox4.SelectAll();
                    }
                    else if (tb == VerBox4)
                    {
                        VerBox2.Focus(FocusState.Programmatic);
                        VerBox2.SelectAll();
                    }
                    else if (tb == VerBox2)
                    {
                        VerBox1.Focus(FocusState.Programmatic);
                        VerBox1.SelectAll();
                    }
                }
            }
        }
    }
}
