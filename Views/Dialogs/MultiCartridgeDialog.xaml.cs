using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using StormSwitchBox.Models;
using StormSwitchBox.Services;

namespace StormSwitchBox.Views.Dialogs
{
    public sealed partial class MultiCartridgeDialog : ContentDialog
    {
        public ObservableCollection<MultiCartridgeItem> Items { get; } = new();
        private CancellationTokenSource? _cts;

        public MultiCartridgeDialog(IEnumerable<string>? initialFiles = null)
        {
            this.InitializeComponent();
            this.XamlRoot = App.MainWindow?.Content?.XamlRoot;
            ItemsListView.ItemsSource = Items;
            OutputFolderBox.Text = !string.IsNullOrWhiteSpace(App.Settings.Current.OutputFolder)
                ? App.Settings.Current.OutputFolder
                : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            if (initialFiles != null)
            {
                foreach (var f in initialFiles)
                {
                    if (File.Exists(f))
                        Items.Add(MultiGameCartridgeService.AnalyzeItem(f));
                }
                UpdateSummary();
            }
        }

        private void UpdateSummary()
        {
            long totalBytes = Items.Sum(x => x.SizeBytes);
            SummaryText.Text = $"Файлов: {Items.Count} | Общий размер: {ProcessingTask.FormatSize(totalBytes)}";
        }

        private async void AddFiles_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
                picker.FileTypeFilter.Add(".nsp");
                picker.FileTypeFilter.Add(".nsz");
                picker.FileTypeFilter.Add(".xci");
                picker.FileTypeFilter.Add(".xcz");

                if (App.MainWindow != null)
                {
                    IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                    WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                }

                var files = await picker.PickMultipleFilesAsync();
                if (files != null && files.Count > 0)
                {
                    foreach (var file in files)
                    {
                        if (!Items.Any(i => string.Equals(i.FilePath, file.Path, StringComparison.OrdinalIgnoreCase)))
                        {
                            Items.Add(MultiGameCartridgeService.AnalyzeItem(file.Path));
                        }
                    }
                    UpdateSummary();
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "Ошибка добавления файлов: " + ex.Message;
            }
        }

        private void ClearFiles_Click(object sender, RoutedEventArgs e)
        {
            Items.Clear();
            UpdateSummary();
        }

        private async void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            string? folder = await SystemDialogService.OpenFolderDialogAsync("Выберите выходную папку для мульти-картриджа", OutputFolderBox.Text);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                OutputFolderBox.Text = folder;
            }
        }

        private async void BuildCartridge_Click(object sender, RoutedEventArgs e)
        {
            if (Items.Count == 0)
            {
                StatusTextBlock.Text = "⚠ Сначала добавьте хотя бы один игровой контейнер (.NSP / .NSZ / .XCI / .XCZ).";
                return;
            }

            string outFolder = OutputFolderBox.Text.Trim();
            string outName = CartridgeNameBox.Text.Trim();
            if (string.IsNullOrEmpty(outName)) outName = "STORM_MultiCartridge";

            bool compressXcz = (OutputFormatCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() == "XCZ";
            string outPath = Path.Combine(outFolder, outName + (compressXcz ? ".xcz" : ".xci"));

            BuildButton.IsEnabled = false;
            _cts = new CancellationTokenSource();

            var progress = new Progress<(double Percent, string Status)>(p =>
            {
                BuildProgressBar.Value = p.Percent;
                StatusTextBlock.Text = p.Status;
            });

            try
            {
                string finalFile = await MultiGameCartridgeService.BuildMultiGameCartridgeAsync(
                    Items.Select(i => i.FilePath).ToList(),
                    outPath,
                    compressXcz,
                    null,
                    progress,
                    _cts.Token);

                StatusTextBlock.Text = $"✅ Мульти-картридж успешно собран: {finalFile}";
                App.Logger.Log($"[Multi-Cartridge] Собран мульти-картридж ({Items.Count} файлов): {finalFile}", LogLevel.Success);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = "❌ Ошибка сборки: " + ex.Message;
                App.Logger.Log($"[Multi-Cartridge] Ошибка: {ex.Message}", LogLevel.Error);
            }
            finally
            {
                BuildButton.IsEnabled = true;
            }
        }
    }
}
