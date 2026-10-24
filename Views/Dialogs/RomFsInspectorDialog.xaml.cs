using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using StormSwitchBox.Models;
using StormSwitchBox.Services;

namespace StormSwitchBox.Views.Dialogs
{
    public sealed partial class RomFsInspectorDialog : ContentDialog
    {
        private List<VirtualArchiveEntry> _allEntries = new();
        public ObservableCollection<VirtualArchiveEntry> FilteredEntries { get; } = new();
        private string _currentContainerPath = string.Empty;

        public RomFsInspectorDialog(string? initialFilePath = null)
        {
            this.InitializeComponent();
            this.XamlRoot = App.MainWindow?.Content?.XamlRoot;
            EntriesListView.ItemsSource = FilteredEntries;

            if (!string.IsNullOrWhiteSpace(initialFilePath) && File.Exists(initialFilePath))
            {
                _ = LoadContainerAsync(initialFilePath);
            }
        }

        private async Task LoadContainerAsync(string path)
        {
            _currentContainerPath = path;
            ContainerPathBox.Text = path;
            bool is3dsEnc = Nintendo3dsService.Is3dsExtension(Path.GetExtension(path)) && Nintendo3dsCryptoService.Is3dsFileEncrypted(path);
            Decrypt3dsBtn.Visibility = is3dsEnc ? Visibility.Visible : Visibility.Collapsed;

            StatusText.Text = $"? ??????????? ???????????? {Path.GetFileName(path)}...";
            try
            {
                _allEntries = await VirtualRomFsInspectorService.InspectContainerAsync(path, CancellationToken.None);
                ApplyFilters();
                StatusText.Text = $"? ??????? ??????????? ????????: {_allEntries.Count} (??????????: {FilteredEntries.Count})";
            }
            catch (Exception ex)
            {
                StatusText.Text = "? ?????? ?????? ??????????: " + ex.Message;
            }
        }

        private void ApplyFilters()
        {
            string query = SearchFilterBox?.Text?.Trim() ?? "";
            string secTag = (SectionFilterCombo?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "ALL";

            var queryList = _allEntries.AsEnumerable();
            if (secTag != "ALL")
            {
                queryList = queryList.Where(e => e.Section.Contains(secTag, StringComparison.OrdinalIgnoreCase));
            }
            if (!string.IsNullOrEmpty(query))
            {
                queryList = queryList.Where(e => e.FullPath.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                                 e.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
            }

            FilteredEntries.Clear();
            foreach (var item in queryList.Take(2500))
            {
                FilteredEntries.Add(item);
            }
        }

        private void SearchFilterBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();
        private void SectionFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilters();

        private async void BrowseContainer_Click(object sender, RoutedEventArgs e)
        {
            string? path = await SystemDialogService.OpenFileDialogAsync(
                "???????? ??????? ????? Switch / 3DS",
                "??????? ?????????? (*.nsp;*.nsz;*.xci;*.xcz;*.nca;*.3ds;*.cci;*.cia;*.cxi)|*.nsp;*.nsz;*.xci;*.xcz;*.nca;*.3ds;*.cci;*.cia;*.cxi|??? ????? (*.*)|*.*");
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                await LoadContainerAsync(path);
            }
        }

        private async void Decrypt3ds_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentContainerPath) || !File.Exists(_currentContainerPath)) return;
            Decrypt3dsBtn.IsEnabled = false;
            try
            {
                string ext = Path.GetExtension(_currentContainerPath);
                string decPath = Path.Combine(
                    Path.GetDirectoryName(_currentContainerPath) ?? "",
                    Path.GetFileNameWithoutExtension(_currentContainerPath) + " [Decrypted]" + ext);

                var prog = new Progress<(double Percent, string Status)>(p => StatusText.Text = p.Status);
                bool ok = await Nintendo3dsCryptoService.Decrypt3dsFileAsync(_currentContainerPath, decPath, null, prog, CancellationToken.None);
                if (ok && File.Exists(decPath))
                {
                    await LoadContainerAsync(decPath);
                }
            }
            finally
            {
                Decrypt3dsBtn.IsEnabled = true;
            }
        }

        private async void EntriesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EntriesListView.SelectedItem is not VirtualArchiveEntry entry) return;

            PreviewHeaderText.Text = $"{entry.Section}: {entry.FullPath} ({entry.SizeFormatted})";
            try
            {
                int maxPreview = entry.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                 entry.Name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                 entry.Name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                 entry.Name.EndsWith(".dat", StringComparison.OrdinalIgnoreCase)
                    ? 2 * 1024 * 1024
                    : 4096;

                byte[] bytes = await VirtualRomFsInspectorService.ReadEntryBytesAsync(_currentContainerPath, entry, maxPreview);
                if (bytes.Length == 0)
                {
                    PreviewImage.Visibility = Visibility.Collapsed;
                    TextPreviewScroll.Visibility = Visibility.Visible;
                    PreviewTextBlock.Text = "(???? ???? ??? ?????????? ??? ??????? ??????)";
                    return;
                }

                // Check if PNG or JPEG magic
                bool isPng = bytes.Length > 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
                bool isJpg = bytes.Length > 4 && bytes[0] == 0xFF && bytes[1] == 0xD8;
                if (isPng || isJpg)
                {
                    using var ms = new MemoryStream(bytes);
                    var bmp = new BitmapImage();
                    await bmp.SetSourceAsync(ms.AsRandomAccessStream());
                    PreviewImage.Source = bmp;
                    PreviewImage.Visibility = Visibility.Visible;
                    TextPreviewScroll.Visibility = Visibility.Collapsed;
                    return;
                }

                PreviewImage.Visibility = Visibility.Collapsed;
                TextPreviewScroll.Visibility = Visibility.Visible;
                PreviewTextBlock.Text = FormatHexAndTextPreview(bytes);
            }
            catch (Exception ex)
            {
                PreviewImage.Visibility = Visibility.Collapsed;
                TextPreviewScroll.Visibility = Visibility.Visible;
                PreviewTextBlock.Text = "?????? ?????????????: " + ex.Message;
            }
        }

        private static string FormatHexAndTextPreview(byte[] data)
        {
            int limit = Math.Min(data.Length, 1024);
            var sb = new StringBuilder();
            for (int i = 0; i < limit; i += 16)
            {
                sb.Append($"{i:X4}  ");
                for (int j = 0; j < 16; j++)
                {
                    if (i + j < limit) sb.Append($"{data[i + j]:X2} ");
                    else sb.Append("   ");
                }
                sb.Append(" | ");
                for (int j = 0; j < 16 && i + j < limit; j++)
                {
                    byte b = data[i + j];
                    sb.Append(b >= 32 && b < 127 ? (char)b : '.');
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private async void ExtractSelected_Click(object sender, RoutedEventArgs e)
        {
            if (EntriesListView.SelectedItem is not VirtualArchiveEntry entry)
            {
                StatusText.Text = "?? ???????? ???? ? ?????? ??? ??????????? ??????????.";
                return;
            }

            string? folder = await SystemDialogService.OpenFolderDialogAsync("???????? ????? ??? ?????????? ?????");
            if (string.IsNullOrWhiteSpace(folder)) return;

            try
            {
                int count = await VirtualRomFsInspectorService.ExtractEntriesAsync(
                    _currentContainerPath, new[] { entry }, folder, null, CancellationToken.None);
                StatusText.Text = $"? ??????? ???????? ????: {entry.FullPath} -> {folder}";
            }
            catch (Exception ex)
            {
                StatusText.Text = "? ?????? ??????????: " + ex.Message;
            }
        }

        private async void ExtractFiltered_Click(object sender, RoutedEventArgs e)
        {
            if (FilteredEntries.Count == 0) return;

            string? folder = await SystemDialogService.OpenFolderDialogAsync($"???????? ????? ??? ?????????? {FilteredEntries.Count} ??????");
            if (string.IsNullOrWhiteSpace(folder)) return;

            var prog = new Progress<(int Done, int Total, string CurrentFile)>(p =>
                StatusText.Text = $"? ?????????? ({p.Done}/{p.Total}): {p.CurrentFile}");

            try
            {
                int count = await VirtualRomFsInspectorService.ExtractEntriesAsync(
                    _currentContainerPath, FilteredEntries.ToList(), folder, prog, CancellationToken.None);
                StatusText.Text = $"? ??????? ????????? ??????: {count} ? ????? {folder}";
            }
            catch (Exception ex)
            {
                StatusText.Text = "? ?????? ????????? ??????????: " + ex.Message;
            }
        }
    }
}
