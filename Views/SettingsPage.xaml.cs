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
        public Visibility Keys3dsSelectedVisibility => string.IsNullOrEmpty(App.Settings.Current.KeysPath3ds) ? Visibility.Collapsed : Visibility.Visible;

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

            // 3DS Format combo initialization
            string f3ds = App.Settings.Current.DefaultFormat3ds ?? "3DS";
            if (f3ds == "CIA") Format3dsCombo.SelectedIndex = 1;
            else if (f3ds == "CXI") Format3dsCombo.SelectedIndex = 2;
            else Format3dsCombo.SelectedIndex = 0;

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

            // Switch Watch Folder combos initialization
            int wfActionSwitch = App.Settings.Current.WatchFolderActionSwitch;
            if (wfActionSwitch >= 0 && wfActionSwitch <= 5) WatchFolderActionComboSwitch.SelectedIndex = wfActionSwitch;
            else WatchFolderActionComboSwitch.SelectedIndex = 0;

            int wfFormatSwitch = App.Settings.Current.WatchFolderFormatSwitch;
            if (wfFormatSwitch >= 0 && wfFormatSwitch <= 3) WatchFolderFormatComboSwitch.SelectedIndex = wfFormatSwitch;
            else WatchFolderFormatComboSwitch.SelectedIndex = 0;

            // 3DS Watch Folder combos initialization
            int wfAction3ds = App.Settings.Current.WatchFolderAction3ds;
            if (wfAction3ds >= 0 && wfAction3ds <= 4) WatchFolderActionCombo3ds.SelectedIndex = wfAction3ds;
            else WatchFolderActionCombo3ds.SelectedIndex = 0;

            int wfFormat3ds = App.Settings.Current.WatchFolderFormat3ds;
            if (wfFormat3ds >= 0 && wfFormat3ds <= 2) WatchFolderFormatCombo3ds.SelectedIndex = wfFormat3ds;
            else WatchFolderFormatCombo3ds.SelectedIndex = 0;

            // Language combo initialization
            string lang = App.Settings.Current.Language ?? "ru";
            LanguageCombo.SelectedIndex = lang switch
            {
                "en" => 1,
                "de" => 2,
                "zh" => 3,
                "ja" => 4,
                _ => 0
            };

            UpdateWatchFolderUiState();
            if (App.WatchFolderService != null)
            {
                App.WatchFolderService.StateChanged += () => App.RunOnUI(UpdateWatchFolderUiState);
            }

            InitializeLanguages();
            PopulateKeysVersion(App.Settings.Current.KeysVersion ?? "");

            SelectTab(App.Settings.Current.SelectedSettingsTab);

            ApplyLocalization();
            App.Localization.LanguageChanged += () => App.RunOnUI(ApplyLocalization);
        }

        private void UpdateWatchFolderUiState()
        {
            var loc = App.Localization;

            // 1. Switch UI
            if (WatchFolderSwitchSummaryBadge != null)
            {
                string actionText = "Сжатие";
                if (WatchFolderActionComboSwitch?.SelectedItem is ComboBoxItem aItem && aItem.Content != null)
                    actionText = aItem.Content.ToString()!;

                string formatText = "NSP";
                if (WatchFolderFormatComboSwitch?.SelectedItem is ComboBoxItem fItem && fItem.Content != null)
                    formatText = fItem.Content.ToString()!;

                WatchFolderSwitchSummaryBadge.Text = $"{loc["Settings_Switch_WatchFolder_Badge"]} {actionText} в {formatText}";
            }

            bool isSwitchRunning = App.WatchFolderService?.IsSwitchRunning == true;
            if (ToggleWatchFolderSwitchBtn != null)
            {
                ToggleWatchFolderSwitchBtn.Content = isSwitchRunning ? loc["Settings_Switch_WatchFolder_Stop"] : loc["Settings_Switch_WatchFolder_Start"];
            }
            if (WatchFolderSwitchStatusTxt != null)
            {
                WatchFolderSwitchStatusTxt.Text = isSwitchRunning ? loc["Settings_Switch_WatchFolder_Active"] : loc["Settings_Switch_WatchFolder_Stopped"];
                WatchFolderSwitchStatusTxt.Foreground = isSwitchRunning 
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen)
                    : (Application.Current.Resources["TextFillColorSecondaryBrush"] as Microsoft.UI.Xaml.Media.Brush ?? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray));
            }

            // 2. 3DS UI
            if (WatchFolder3dsSummaryBadge != null)
            {
                string actionText = "Мульти-контент";
                if (WatchFolderActionCombo3ds?.SelectedItem is ComboBoxItem aItem && aItem.Content != null)
                    actionText = aItem.Content.ToString()!;

                string formatText = "3DS";
                if (WatchFolderFormatCombo3ds?.SelectedItem is ComboBoxItem fItem && fItem.Content != null)
                    formatText = fItem.Content.ToString()!;

                WatchFolder3dsSummaryBadge.Text = $"{loc["Settings_3ds_WatchFolder_Badge"]} {actionText} в {formatText}";
            }

            bool is3dsRunning = App.WatchFolderService?.Is3dsRunning == true;
            if (ToggleWatchFolder3dsBtn != null)
            {
                ToggleWatchFolder3dsBtn.Content = is3dsRunning ? loc["Settings_3ds_WatchFolder_Stop"] : loc["Settings_3ds_WatchFolder_Start"];
            }
            if (WatchFolder3dsStatusTxt != null)
            {
                WatchFolder3dsStatusTxt.Text = is3dsRunning ? loc["Settings_3ds_WatchFolder_Active"] : loc["Settings_3ds_WatchFolder_Stopped"];
                WatchFolder3dsStatusTxt.Foreground = is3dsRunning 
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen)
                    : (Application.Current.Resources["TextFillColorSecondaryBrush"] as Microsoft.UI.Xaml.Media.Brush ?? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray));
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

        #region Switch Watch Folder Handlers

        private async void WatchFolderActionComboSwitch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WatchFolderActionComboSwitch.SelectedItem is ComboBoxItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int val))
            {
                App.Settings.Current.WatchFolderActionSwitch = val;
                await App.Settings.SaveAsync();
                UpdateWatchFolderUiState();
            }
        }

        private async void WatchFolderFormatComboSwitch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WatchFolderFormatComboSwitch.SelectedItem is ComboBoxItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int val))
            {
                App.Settings.Current.WatchFolderFormatSwitch = val;
                await App.Settings.SaveAsync();
                UpdateWatchFolderUiState();
            }
        }

        private async void WatchFolderSwitchToggle_Toggled(object sender, RoutedEventArgs e)
        {
            await App.Settings.SaveAsync();
            if (!App.Settings.Current.EnableWatchFolderSwitch && App.WatchFolderService != null && App.WatchFolderService.IsSwitchRunning)
            {
                App.WatchFolderService.StopSwitch();
            }
            UpdateWatchFolderUiState();
        }

        private async void SelectWatchFolderSwitch_Click(object sender, RoutedEventArgs e)
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
                    App.Settings.Current.WatchFolderSwitch = folder.Path;
                    WatchFolderBoxSwitch.Text = folder.Path;
                    await App.Settings.SaveAsync();
                }
            }
            catch { }
        }

        private void WatchFolderBoxSwitch_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Выбрать как «Умную» папку Switch";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsContentVisible = true;
        }

        private async void WatchFolderBoxSwitch_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0)
                {
                    var item = items[0];
                    string path = item.Path;
                    if (System.IO.File.Exists(path)) path = System.IO.Path.GetDirectoryName(path) ?? path;
                    if (System.IO.Directory.Exists(path))
                    {
                        App.Settings.Current.WatchFolderSwitch = path;
                        WatchFolderBoxSwitch.Text = path;
                        await App.Settings.SaveAsync();
                        App.Logger.Log($"[WatchFolder Switch] Папка установлена: {path}", Models.LogLevel.Success);
                    }
                }
            }
        }

        private async void ToggleWatchFolderSwitchBtn_Click(object sender, RoutedEventArgs e)
        {
            if (App.WatchFolderService == null) return;

            if (App.WatchFolderService.IsSwitchRunning)
            {
                App.WatchFolderService.StopSwitch();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(App.Settings.Current.WatchFolderSwitch) || !System.IO.Directory.Exists(App.Settings.Current.WatchFolderSwitch))
                {
                    App.Logger.Log("[WatchFolder Switch] Укажите корректную существующую папку для отслеживания", Models.LogLevel.Warning);
                    return;
                }
                App.WatchFolderService.StartSwitch();
                await ScanWatchFolderSwitchAsync();
            }
            UpdateWatchFolderUiState();
        }

        private async Task ScanWatchFolderSwitchAsync()
        {
            var settings = App.Settings.Current;
            string watchPath = settings.WatchFolderSwitch;

            if (string.IsNullOrWhiteSpace(watchPath) || !System.IO.Directory.Exists(watchPath))
                return;

            try
            {
                string[] supportedExts = { ".nsp", ".nsz", ".xci", ".xcz" };
                int totalTasks = 0;

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
                    App.TasksVM.SelectedPlatform = 0;
                    await App.TasksVM.AddDroppedFilesBatchAsync(rootFiles);
                    totalTasks++;
                }

                var subDirs = System.IO.Directory.GetDirectories(watchPath);
                foreach (var subDir in subDirs)
                {
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
                        App.TasksVM.SelectedPlatform = 0;
                        await App.TasksVM.AddDroppedFilesBatchAsync(new System.Collections.Generic.List<string> { subDir });
                        totalTasks++;
                    }
                }

                if (totalTasks > 0)
                {
                    await App.TasksVM.StartAllTasksAsync();
                }
            }
            catch (Exception ex)
            {
                App.Logger?.Log($"[WatchFolder Switch] Ошибка сканирования: {ex.Message}", Models.LogLevel.Warning);
            }
        }

        #endregion

        #region 3DS Watch Folder Handlers

        private async void WatchFolderActionCombo3ds_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WatchFolderActionCombo3ds.SelectedItem is ComboBoxItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int val))
            {
                App.Settings.Current.WatchFolderAction3ds = val;
                await App.Settings.SaveAsync();
                UpdateWatchFolderUiState();
            }
        }

        private async void WatchFolderFormatCombo3ds_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WatchFolderFormatCombo3ds.SelectedItem is ComboBoxItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int val))
            {
                App.Settings.Current.WatchFolderFormat3ds = val;
                await App.Settings.SaveAsync();
                UpdateWatchFolderUiState();
            }
        }

        private async void WatchFolder3dsToggle_Toggled(object sender, RoutedEventArgs e)
        {
            await App.Settings.SaveAsync();
            if (!App.Settings.Current.EnableWatchFolder3ds && App.WatchFolderService != null && App.WatchFolderService.Is3dsRunning)
            {
                App.WatchFolderService.Stop3ds();
            }
            UpdateWatchFolderUiState();
        }

        private async void SelectWatchFolder3ds_Click(object sender, RoutedEventArgs e)
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
                    App.Settings.Current.WatchFolder3ds = folder.Path;
                    WatchFolderBox3ds.Text = folder.Path;
                    await App.Settings.SaveAsync();
                }
            }
            catch { }
        }

        private void WatchFolderBox3ds_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Выбрать как «Умную» папку 3DS";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsContentVisible = true;
        }

        private async void WatchFolderBox3ds_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0)
                {
                    var item = items[0];
                    string path = item.Path;
                    if (System.IO.File.Exists(path)) path = System.IO.Path.GetDirectoryName(path) ?? path;
                    if (System.IO.Directory.Exists(path))
                    {
                        App.Settings.Current.WatchFolder3ds = path;
                        WatchFolderBox3ds.Text = path;
                        await App.Settings.SaveAsync();
                        App.Logger.Log($"[WatchFolder 3DS] Папка установлена: {path}", Models.LogLevel.Success);
                    }
                }
            }
        }

        private async void ToggleWatchFolder3dsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (App.WatchFolderService == null) return;

            if (App.WatchFolderService.Is3dsRunning)
            {
                App.WatchFolderService.Stop3ds();
            }
            else
            {
                if (string.IsNullOrWhiteSpace(App.Settings.Current.WatchFolder3ds) || !System.IO.Directory.Exists(App.Settings.Current.WatchFolder3ds))
                {
                    App.Logger.Log("[WatchFolder 3DS] Укажите корректную существующую папку для отслеживания", Models.LogLevel.Warning);
                    return;
                }
                App.WatchFolderService.Start3ds();
                await ScanWatchFolder3dsAsync();
            }
            UpdateWatchFolderUiState();
        }

        private async Task ScanWatchFolder3dsAsync()
        {
            var settings = App.Settings.Current;
            string watchPath = settings.WatchFolder3ds;

            if (string.IsNullOrWhiteSpace(watchPath) || !System.IO.Directory.Exists(watchPath))
                return;

            try
            {
                string[] supportedExts = { ".3ds", ".cci", ".cia", ".cxi", ".cfa" };
                int totalTasks = 0;

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
                    App.TasksVM.SelectedPlatform = 1;
                    await App.TasksVM.AddDroppedFilesBatchAsync(rootFiles);
                    totalTasks++;
                }

                var subDirs = System.IO.Directory.GetDirectories(watchPath);
                foreach (var subDir in subDirs)
                {
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
                        App.TasksVM.SelectedPlatform = 1;
                        await App.TasksVM.AddDroppedFilesBatchAsync(new System.Collections.Generic.List<string> { subDir });
                        totalTasks++;
                    }
                }

                if (totalTasks > 0)
                {
                    await App.TasksVM.StartAllTasksAsync();
                }
            }
            catch (Exception ex)
            {
                App.Logger?.Log($"[WatchFolder 3DS] Ошибка сканирования: {ex.Message}", Models.LogLevel.Warning);
            }
        }

        #endregion

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
            if (LanguageItemsControl == null) return;
            LanguageItemsControl.Items.Clear();

            var loc = App.Localization;
            var langs = new (string LocKey, string[] Codes)[]
            {
                ("Lang_Russian", new[] { "ru", "ru-RU" }),
                ("Lang_English", new[] { "en", "en-US", "en-GB" }),
                ("Lang_Japanese", new[] { "ja", "ja-JP", "Japanese" }),
                ("Lang_Spanish", new[] { "es", "es-ES", "es-MX", "Spanish" }),
                ("Lang_French", new[] { "fr", "fr-FR", "fr-CA", "French" }),
                ("Lang_German", new[] { "de", "de-DE", "German" }),
                ("Lang_Italian", new[] { "it", "it-IT", "Italian" }),
                ("Lang_Dutch", new[] { "nl", "nl-NL", "Dutch" }),
                ("Lang_Portuguese", new[] { "pt", "pt-BR", "pt-PT", "Portuguese" }),
                ("Lang_Korean", new[] { "ko", "ko-KR", "Korean" }),
                ("Lang_Chinese_Simplified", new[] { "zh-Hans", "zh-CN" }),
                ("Lang_Chinese_Traditional", new[] { "zh-Hant", "zh-TW" })
            };

            foreach (var lang in langs)
            {
                string displayName = loc.GetString(lang.LocKey);
                if (string.IsNullOrEmpty(displayName)) displayName = lang.LocKey;

                var cb = new CheckBox { Content = displayName, Margin = new Thickness(0, 0, 16, 8) };
                
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

        // ===== Переключение вкладок параметров (Общие / Switch / 3DS) =====
        private void GeneralTabButton_Click(object sender, RoutedEventArgs e)
        {
            SelectTab(0);
        }

        private void SwitchTabButton_Click(object sender, RoutedEventArgs e)
        {
            SelectTab(1);
        }

        private void ThreeDsTabButton_Click(object sender, RoutedEventArgs e)
        {
            SelectTab(2);
        }

        private void SelectTab(int index)
        {
            App.Settings.Current.SelectedSettingsTab = index;

            GeneralSettingsPanel.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
            SwitchSettingsPanel.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
            ThreeDsSettingsPanel.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;

            var accentBrush = Application.Current.Resources["SystemControlHighlightAccentBrush"] as Microsoft.UI.Xaml.Media.Brush 
                ?? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.DodgerBlue);
            var defaultBrush = Application.Current.Resources["CardStrokeColorDefaultBrush"] as Microsoft.UI.Xaml.Media.Brush 
                ?? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);
            var secondaryTextBrush = Application.Current.Resources["TextFillColorSecondaryBrush"] as Microsoft.UI.Xaml.Media.Brush 
                ?? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray);

            GeneralTabButton.BorderBrush = index == 0 ? accentBrush : defaultBrush;
            GeneralTabButton.BorderThickness = new Thickness(index == 0 ? 2 : 1);
            GeneralTabIcon.Foreground = index == 0 ? accentBrush : secondaryTextBrush;

            SwitchTabButton.BorderBrush = index == 1 ? accentBrush : defaultBrush;
            SwitchTabButton.BorderThickness = new Thickness(index == 1 ? 2 : 1);
            SwitchTabIcon.Foreground = index == 1 ? accentBrush : secondaryTextBrush;

            ThreeDsTabButton.BorderBrush = index == 2 ? accentBrush : defaultBrush;
            ThreeDsTabButton.BorderThickness = new Thickness(index == 2 ? 2 : 1);
            ThreeDsTabIcon.Foreground = index == 2 ? accentBrush : secondaryTextBrush;
        }

        private async void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageCombo.SelectedItem is ComboBoxItem item && item.Tag is string langCode)
            {
                if (App.Settings.Current.Language != langCode)
                {
                    App.Settings.Current.Language = langCode;
                    await App.Settings.SaveAsync();
                    App.Localization.CurrentLanguage = langCode;
                }
            }
        }

        public void ApplyLocalization()
        {
            var loc = App.Localization;

            if (PageTitleText != null) PageTitleText.Text = loc["Settings_Title"];
            if (GeneralTabText != null) GeneralTabText.Text = loc["Settings_Tab_General"];
            if (SwitchTabText != null) SwitchTabText.Text = loc["Settings_Tab_Switch"];
            if (ThreeDsTabText != null) ThreeDsTabText.Text = loc["Settings_Tab_3ds"];

            // General Panel
            if (LangHeaderTxt != null) LangHeaderTxt.Text = loc["Settings_General_Language_Header"];
            if (LangTitleTxt != null) LangTitleTxt.Text = loc["Settings_General_Language_Title"];
            if (LangDescTxt != null) LangDescTxt.Text = loc["Settings_General_Language_Desc"];

            if (AppearHeaderTxt != null) AppearHeaderTxt.Text = loc["Settings_General_Appearance_Header"];
            if (ThemeTitleTxt != null) ThemeTitleTxt.Text = loc["Settings_General_Theme_Title"];
            if (ThemeDescTxt != null) ThemeDescTxt.Text = loc["Settings_General_Theme_Desc"];
            if (AccentTitleTxt != null) AccentTitleTxt.Text = loc["Settings_General_Accent_Title"];
            if (AccentDescTxt != null) AccentDescTxt.Text = loc["Settings_General_Accent_Desc"];
            if (SoundTitleTxt != null) SoundTitleTxt.Text = loc["Settings_General_Sound_Title"];
            if (SoundDescTxt != null) SoundDescTxt.Text = loc["Settings_General_Sound_Desc"];

            if (FilesHeaderTxt != null) FilesHeaderTxt.Text = loc["Settings_General_Files_Header"];
            if (ComplexTitleTxt != null) ComplexTitleTxt.Text = loc["Settings_General_Complex_Title"];
            if (ComplexDescTxt != null) ComplexDescTxt.Text = loc["Settings_General_Complex_Desc"];
            if (DeleteSrcTitleTxt != null) DeleteSrcTitleTxt.Text = loc["Settings_General_DeleteSource_Title"];
            if (DeleteSrcDescTxt != null) DeleteSrcDescTxt.Text = loc["Settings_General_DeleteSource_Desc"];

            if (PerfHeaderTxt != null) PerfHeaderTxt.Text = loc["Settings_General_Perf_Header"];
            if (CompTitleTxt != null) CompTitleTxt.Text = loc["Settings_General_Compression_Title"];
            if (CompDescTxt != null) CompDescTxt.Text = loc["Settings_General_Compression_Desc"];
            if (TasksTitleTxt != null) TasksTitleTxt.Text = loc["Settings_General_Tasks_Title"];
            if (TasksDescTxt != null) TasksDescTxt.Text = loc["Settings_General_Tasks_Desc"];
            if (ThreadsTitleTxt != null) ThreadsTitleTxt.Text = loc["Settings_General_Threads_Title"];
            if (ThreadsDescTxt != null) ThreadsDescTxt.Text = loc["Settings_General_Threads_Desc"];

            if (AboutHeaderTxt != null) AboutHeaderTxt.Text = loc["Settings_General_About_Header"];
            if (AppTitleVersionTxt != null) AppTitleVersionTxt.Text = $"STORM SWITCH BOX {App.Settings.Current.AppVersion}";
            if (AboutDescriptionTxt != null) AboutDescriptionTxt.Text = loc["Settings_General_About_Desc"];
            if (CheckUpdatesButton != null) CheckUpdatesButton.Content = loc["Settings_General_CheckUpdates"];
            if (OpenLogsButton != null) OpenLogsButton.Content = loc["Settings_General_OpenLogs"];

            // Switch Panel
            if (SwitchKeysHeaderTxt != null) SwitchKeysHeaderTxt.Text = loc["Settings_Switch_Keys_Header"];
            if (SwitchKeysTitleTxt != null) SwitchKeysTitleTxt.Text = loc["Settings_Switch_Keys_Title"];
            if (SwitchKeysDescTxt != null) SwitchKeysDescTxt.Text = loc["Settings_Switch_Keys_Desc"];
            if (SwitchKeysLoadedTxt != null) SwitchKeysLoadedTxt.Text = loc["Settings_Switch_Keys_Loaded"];
            if (SelectKeysButton != null) SelectKeysButton.Content = loc["Settings_Switch_Keys_Pick"];

            if (SwitchAlgoHeaderTxt != null) SwitchAlgoHeaderTxt.Text = loc["Settings_Switch_Algorithms_Header"];
            if (KeyGenTitleTxt != null) KeyGenTitleTxt.Text = loc["Settings_Switch_KeyGen_Title"];
            if (KeyGenDescTxt != null) KeyGenDescTxt.Text = loc["Settings_Switch_KeyGen_Desc"];
            if (RsvTitleTxt != null) RsvTitleTxt.Text = loc["Settings_Switch_Rsv_Title"];
            if (RsvDescTxt != null) RsvDescTxt.Text = loc["Settings_Switch_Rsv_Desc"];
            if (TitlerightsTitleTxt != null) TitlerightsTitleTxt.Text = loc["Settings_Switch_Titlerights_Title"];
            if (TitlerightsDescTxt != null) TitlerightsDescTxt.Text = loc["Settings_Switch_Titlerights_Desc"];
            if (DeltaTitleTxt != null) DeltaTitleTxt.Text = loc["Settings_Switch_Delta_Title"];
            if (DeltaDescTxt != null) DeltaDescTxt.Text = loc["Settings_Switch_Delta_Desc"];
            if (Fat32TitleTxt != null) Fat32TitleTxt.Text = loc["Settings_Switch_Fat32_Title"];
            if (Fat32DescTxt != null) Fat32DescTxt.Text = loc["Settings_Switch_Fat32_Desc"];
            if (ForceMultiTitleTxt != null) ForceMultiTitleTxt.Text = loc["Settings_Switch_ForceMulti_Title"];
            if (ForceMultiDescTxt != null) ForceMultiDescTxt.Text = loc["Settings_Switch_ForceMulti_Desc"];
            if (LangTrimTitleTxt != null) LangTrimTitleTxt.Text = loc["Settings_Switch_LangTrim_Title"];
            if (LangTrimDescTxt != null) LangTrimDescTxt.Text = loc["Settings_Switch_LangTrim_Desc"];
            if (LangTrimSubTxt != null) LangTrimSubTxt.Text = loc["Settings_Switch_LangTrim_Sub"];

            if (SwitchWatchTitleTxt != null) SwitchWatchTitleTxt.Text = loc["Settings_Switch_WatchFolder_Title"];
            if (SwitchWatchDescTxt != null) SwitchWatchDescTxt.Text = loc["Settings_Switch_WatchFolder_Desc"];
            if (SelectWatchFolderSwitchButton != null) SelectWatchFolderSwitchButton.Content = loc["Settings_Switch_WatchFolder_Pick"];
            if (WatchFolderBoxSwitch != null) WatchFolderBoxSwitch.PlaceholderText = loc["Settings_Switch_OutFolder_Placeholder"];

            if (SwitchOutFolderTitleTxt != null) SwitchOutFolderTitleTxt.Text = loc["Settings_Switch_OutFolder_Title"];
            if (SwitchOutFolderDescTxt != null) SwitchOutFolderDescTxt.Text = loc["Settings_Switch_OutFolder_Desc"];
            if (OutputFolderBox != null) OutputFolderBox.PlaceholderText = loc["Settings_Switch_OutFolder_Placeholder"];
            if (SelectOutputFolderButton != null) SelectOutputFolderButton.Content = loc["Settings_Switch_OutFolder_Browse"];

            // 3DS Panel
            if (ThreeDsKeysHeaderTxt != null) ThreeDsKeysHeaderTxt.Text = loc["Settings_3ds_Keys_Header"];
            if (ThreeDsKeysTitleTxt != null) ThreeDsKeysTitleTxt.Text = loc["Settings_3ds_Keys_Title"];
            if (ThreeDsKeysDescTxt != null) ThreeDsKeysDescTxt.Text = loc["Settings_3ds_Keys_Desc"];
            if (ThreeDsKeysLoadedTxt != null) ThreeDsKeysLoadedTxt.Text = loc["Settings_3ds_Keys_Loaded"];
            if (SelectKeys3dsButton != null) SelectKeys3dsButton.Content = loc["Settings_3ds_Keys_Pick"];

            if (ThreeDsAlgoHeaderTxt != null) ThreeDsAlgoHeaderTxt.Text = loc["Settings_3ds_Algorithms_Header"];
            if (ThreeDsFormatTitleTxt != null) ThreeDsFormatTitleTxt.Text = loc["Settings_3ds_Format_Title"];
            if (ThreeDsFormatDescTxt != null) ThreeDsFormatDescTxt.Text = loc["Settings_3ds_Format_Desc"];
            if (ThreeDsHardPatchTitleTxt != null) ThreeDsHardPatchTitleTxt.Text = loc["Settings_3ds_HardPatch_Title"];
            if (ThreeDsHardPatchDescTxt != null) ThreeDsHardPatchDescTxt.Text = loc["Settings_3ds_HardPatch_Desc"];

            if (ThreeDsWatchTitleTxt != null) ThreeDsWatchTitleTxt.Text = loc["Settings_3ds_WatchFolder_Title"];
            if (ThreeDsWatchDescTxt != null) ThreeDsWatchDescTxt.Text = loc["Settings_3ds_WatchFolder_Desc"];
            if (SelectWatchFolder3dsButton != null) SelectWatchFolder3dsButton.Content = loc["Settings_3ds_WatchFolder_Pick"];
            if (WatchFolderBox3ds != null) WatchFolderBox3ds.PlaceholderText = loc["Settings_3ds_OutFolder_Placeholder"];

            if (ThreeDsOutFolderTitleTxt != null) ThreeDsOutFolderTitleTxt.Text = loc["Settings_3ds_OutFolder_Title"];
            if (ThreeDsOutFolderDescTxt != null) ThreeDsOutFolderDescTxt.Text = loc["Settings_3ds_OutFolder_Desc"];
            if (OutputFolderBox3ds != null) OutputFolderBox3ds.PlaceholderText = loc["Settings_3ds_OutFolder_Placeholder"];
            if (SelectOutputFolder3dsButton != null) SelectOutputFolder3dsButton.Content = loc["Settings_3ds_OutFolder_Browse"];

            // Localize Toggle Switches
            string onTxt = loc.GetString("Common_On");
            if (string.IsNullOrEmpty(onTxt)) onTxt = "Вкл.";
            string offTxt = loc.GetString("Common_Off");
            if (string.IsNullOrEmpty(offTxt)) offTxt = "Откл.";

            if (SoundToggle != null) { SoundToggle.OnContent = onTxt; SoundToggle.OffContent = offTxt; }
            if (ComplexFoldersToggle != null) { ComplexFoldersToggle.OnContent = onTxt; ComplexFoldersToggle.OffContent = offTxt; }
            if (DeleteSourceToggle != null) { DeleteSourceToggle.OnContent = onTxt; DeleteSourceToggle.OffContent = offTxt; }
            if (RemoveTitlerightsToggle != null) { RemoveTitlerightsToggle.OnContent = onTxt; RemoveTitlerightsToggle.OffContent = offTxt; }
            if (RemoveDeltaNcaToggle != null) { RemoveDeltaNcaToggle.OnContent = onTxt; RemoveDeltaNcaToggle.OffContent = offTxt; }
            if (SplitFat32Toggle != null) { SplitFat32Toggle.OnContent = onTxt; SplitFat32Toggle.OffContent = offTxt; }
            if (ForceMultiRebuildToggle != null) { ForceMultiRebuildToggle.OnContent = onTxt; ForceMultiRebuildToggle.OffContent = offTxt; }
            if (TrimXciToggle != null) { TrimXciToggle.OnContent = onTxt; TrimXciToggle.OffContent = offTxt; }
            if (WatchFolderSwitchToggle != null) { WatchFolderSwitchToggle.OnContent = onTxt; WatchFolderSwitchToggle.OffContent = offTxt; }
            if (HardPatch3dsToggle != null) { HardPatch3dsToggle.OnContent = onTxt; HardPatch3dsToggle.OffContent = offTxt; }
            if (WatchFolder3dsToggle != null) { WatchFolder3dsToggle.OnContent = onTxt; WatchFolder3dsToggle.OffContent = offTxt; }

            // Re-render RomFS trim languages with current language
            InitializeLanguages();

            UpdateWatchFolderUiState();
        }

        // ===== 3DS Ключи и настройки =====
        private async void SelectKeys3dsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var filePicker = new Windows.Storage.Pickers.FileOpenPicker();
                filePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
                filePicker.FileTypeFilter.Add(".txt");
                filePicker.FileTypeFilter.Add(".bin");
                filePicker.FileTypeFilter.Add("*");

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(filePicker, hwnd);

                var file = await filePicker.PickSingleFileAsync();
                if (file != null)
                {
                    App.Settings.Current.KeysPath3ds = file.Path;
                    await App.Settings.SaveAsync();
                    App.Logger.Log($"[3DS Keys] Подключен файл ключей: {file.Path}", Models.LogLevel.Success);
                    this.Bindings.Update();
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[3DS Keys] Ошибка выбора ключей: {ex.Message}", Models.LogLevel.Error);
            }
        }

        private void KeysFile3ds_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Установить файл ключей 3DS";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsContentVisible = true;
        }

        private async void KeysFile3ds_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0)
                {
                    var file = items[0];
                    if (System.IO.File.Exists(file.Path))
                    {
                        App.Settings.Current.KeysPath3ds = file.Path;
                        await App.Settings.SaveAsync();
                        App.Logger.Log($"[3DS Keys] Установлен файл ключей: {file.Path}", Models.LogLevel.Success);
                        this.Bindings.Update();
                    }
                }
            }
        }

        private async void Format3dsCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Format3dsCombo?.SelectedItem is ComboBoxItem item && item.Tag is string tagStr)
            {
                App.Settings.Current.DefaultFormat3ds = tagStr;
                App.Settings.Current.SelectedFormatIndex3ds = Format3dsCombo.SelectedIndex;
                await App.Settings.SaveAsync();
            }
        }

        private async void Toggle3ds_Changed(object sender, RoutedEventArgs e)
        {
            await App.Settings.SaveAsync();
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
                        App.Logger.Log($"Выходная папка Switch установлена перетягиванием: {path}", Models.LogLevel.Success);
                    }
                }
            }
        }

        // ===== Выбор выходной папки 3DS =====
        private async void SelectOutputFolder3ds_Click(object sender, RoutedEventArgs e)
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
                    App.Settings.Current.OutputFolder3ds = folder.Path;
                    OutputFolderBox3ds.Text = folder.Path;
                    await App.Settings.SaveAsync();
                    App.Logger.Log($"Выходная папка 3DS: {folder.Path}", Models.LogLevel.Info);
                }
            }
            catch (Exception)
            {
                var dialog = new ContentDialog
                {
                    Title = "Укажите выходную папку 3DS",
                    CloseButtonText = "Отмена",
                    PrimaryButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                var textBox = new TextBox
                {
                    PlaceholderText = @"Например: E:\3DS_OUT",
                    Text = App.Settings.Current.OutputFolder3ds ?? "",
                    Width = 400
                };
                dialog.Content = textBox;

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
                {
                    App.Settings.Current.OutputFolder3ds = textBox.Text.Trim();
                    OutputFolderBox3ds.Text = textBox.Text.Trim();
                    await App.Settings.SaveAsync();
                    App.Logger.Log($"Выходная папка 3DS (вручную): {textBox.Text.Trim()}", Models.LogLevel.Info);
                }
            }
        }

        private void OutputFolderBox3ds_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Выбрать как выходную папку 3DS";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsContentVisible = true;
        }

        private async void OutputFolderBox3ds_Drop(object sender, DragEventArgs e)
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
                        App.Settings.Current.OutputFolder3ds = path;
                        OutputFolderBox3ds.Text = path;
                        await App.Settings.SaveAsync();
                        App.Logger.Log($"Выходная папка 3DS установлена перетягиванием: {path}", Models.LogLevel.Success);
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
                        Content = new TextBlock { Text = $"У вас установлена актуальная версия STORM SWITCH BOX v{App.Settings.Current.AppVersion}." },
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

                var appDir = System.AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
                var exePath = System.IO.Path.Combine(appDir, "StormSwitchBox.exe");
                var batchPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ssb_update.bat");
                var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ssb_update_log.txt");
                var pid = System.Diagnostics.Process.GetCurrentProcess().Id;

                string batchContent;
                if (assetName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    batchContent =
$@"@echo off
chcp 65001 > nul
echo [SSB Updater] Начало обновления... > ""{logPath}""
echo Завершение STORM SWITCH BOX (PID {pid})...
taskkill /F /PID {pid} > nul 2>&1
:WAIT_LOOP
timeout /t 1 /nobreak > nul
tasklist /FI ""PID eq {pid}"" 2>nul | find /i ""StormSwitchBox"" > nul
if not errorlevel 1 (
    echo Ожидание завершения процесса... >> ""{logPath}""
    goto WAIT_LOOP
)
echo Процесс завершён. >> ""{logPath}""
echo Извлечение обновления из ZIP...
echo Извлечение в: {appDir} >> ""{logPath}""
powershell -NoProfile -ExecutionPolicy Bypass -Command ""Expand-Archive -LiteralPath '{tempPath}' -DestinationPath '{appDir}' -Force"" >> ""{logPath}"" 2>&1
if errorlevel 1 (
    echo ОШИБКА: Не удалось извлечь архив! >> ""{logPath}""
    echo Ошибка извлечения архива. Файл сохранён: {tempPath}
    pause
    exit /b 1
)
echo Извлечение завершено успешно. >> ""{logPath}""
echo Запуск новой версии...
start """" ""{exePath}""
del ""{tempPath}"" > nul 2>&1
echo Обновление завершено. >> ""{logPath}""
(goto) 2>nul & del ""%~f0""
";
                }
                else
                {
                    // Inno Setup installer
                    batchContent =
$@"@echo off
chcp 65001 > nul
echo [SSB Updater] Начало обновления (installer)... > ""{logPath}""
echo Завершение STORM SWITCH BOX (PID {pid})...
taskkill /F /PID {pid} > nul 2>&1
:WAIT_LOOP
timeout /t 1 /nobreak > nul
tasklist /FI ""PID eq {pid}"" 2>nul | find /i ""StormSwitchBox"" > nul
if not errorlevel 1 (
    echo Ожидание завершения процесса... >> ""{logPath}""
    goto WAIT_LOOP
)
echo Процесс завершён. >> ""{logPath}""
echo Запуск установщика...
echo Запуск: {tempPath} /VERYSILENT /DIR={appDir} >> ""{logPath}""
""{tempPath}"" /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /FORCECLOSEAPPLICATIONS /DIR=""{appDir}""
if errorlevel 1 (
    echo ОШИБКА: Установщик завершился с ошибкой! >> ""{logPath}""
    echo Ошибка установки. Файл сохранён: {tempPath}
    pause
    exit /b 1
)
echo Установка завершена. >> ""{logPath}""
echo Запуск новой версии...
start """" ""{exePath}""
del ""{tempPath}"" > nul 2>&1
echo Обновление завершено. >> ""{logPath}""
(goto) 2>nul & del ""%~f0""
";
                }

                // Записываем bat без BOM (cmd.exe не любит UTF-8 BOM)
                await System.IO.File.WriteAllTextAsync(batchPath, batchContent, new System.Text.UTF8Encoding(false));

                // КРИТИЧЕСКИ ВАЖНО: UseShellExecute = true запускает bat как НЕЗАВИСИМЫЙ процесс,
                // который НЕ будет убит при завершении текущего приложения (без Job Object наследования)
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = batchPath,
                    UseShellExecute = true,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Normal
                };
                System.Diagnostics.Process.Start(psi);

                dialog.Hide();

                // Даём bat-скрипту время стартовать перед закрытием
                await System.Threading.Tasks.Task.Delay(500);
                Application.Current.Exit();
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
