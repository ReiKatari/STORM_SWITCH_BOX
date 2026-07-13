using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

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

            InitializeLanguages();
        }

        private void InitializeLanguages()
        {
            var langs = new (string Name, string[] Codes)[]
            {
                ("Русский", new[] { "ru", "ru-RU" }),
                ("English", new[] { "en", "en-US", "en-GB" }),
                ("Japanese", new[] { "ja", "ja-JP", "Japanese" }),
                ("Spanish", new[] { "es", "es-ES", "es-MX", "Spanish" }),
                ("French", new[] { "fr", "fr-FR", "fr-CA", "French" }),
                ("German", new[] { "de", "de-DE", "German" }),
                ("Italian", new[] { "it", "it-IT", "Italian" }),
                ("Dutch", new[] { "nl", "nl-NL", "Dutch" }),
                ("Portuguese", new[] { "pt", "pt-BR", "pt-PT", "Portuguese" }),
                ("Korean", new[] { "ko", "ko-KR", "Korean" }),
                ("Chinese (Simp)", new[] { "zh-Hans", "zh-CN" }),
                ("Chinese (Trad)", new[] { "zh-Hant", "zh-TW" })
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
        private async void SelectKeysButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
                picker.FileTypeFilter.Add(".keys");
                picker.FileTypeFilter.Add(".txt");
                picker.FileTypeFilter.Add("*");

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    string toolsDir = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "tools");
                    if (!System.IO.Directory.Exists(toolsDir))
                    {
                        string devToolsDir = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "..", "..", "tools");
                        if (System.IO.Directory.Exists(devToolsDir))
                            toolsDir = devToolsDir;
                        else
                            System.IO.Directory.CreateDirectory(toolsDir);
                    }

                    string targetTxt = System.IO.Path.Combine(toolsDir, "keys.txt");
                    string targetProd = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "prod.keys");

                    try { System.IO.File.Copy(file.Path, targetTxt, true); } catch { }
                    try { System.IO.File.Copy(file.Path, targetProd, true); } catch { }

                    App.Settings.Current.KeysPath = targetTxt;
                    await App.Settings.SaveAsync();
                    App.Keys.LoadKeys(targetTxt);
                    App.Logger.Log($"Файл ключей скопирован и применен: {targetTxt}", Models.LogLevel.Success);
                    this.Bindings.Update();
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"Ошибка выбора файла ключей: {ex.Message}", Models.LogLevel.Error);
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
    }
}
