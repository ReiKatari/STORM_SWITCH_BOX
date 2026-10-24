using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using StormSwitchBox.Models;
using StormSwitchBox.Services;

namespace StormSwitchBox.Views.Dialogs
{
    public sealed partial class ControlEditorDialog : ContentDialog
    {
        private readonly GameMetadataEditModel _model;
        private byte[]? _baseIconBytes;
        private bool _isInitializing = true;

        public ControlEditorDialog(GameMetadataEditModel model)
        {
            this.InitializeComponent();
            _model = model;
            this.XamlRoot = App.MainWindow?.Content?.XamlRoot;

            PopulateUI();
            _isInitializing = false;
            RefreshBadgePreview();
        }

        private void PopulateUI()
        {
            TitleIdText.Text = string.IsNullOrEmpty(_model.TitleId) ? "0100000000000000" : _model.TitleId;
            VersionText.Text = string.IsNullOrEmpty(_model.Version) ? "1.0.0" : _model.Version;
            TitleEnglishBox.Text = _model.TitleNameEnglish ?? "";
            TitleRussianBox.Text = _model.TitleNameRussian ?? "";
            PublisherBox.Text = _model.Publisher ?? "";

            bool is3ds = _model.Is3ds || Nintendo3dsService.Is3dsExtension(Path.GetExtension(_model.SourceFilePath ?? ""));
            _model.Is3ds = is3ds;
            PlatformBadgeText.Text = is3ds ? "NINTENDO 3DS" : "SWITCH";

            _baseIconBytes = _model.CustomIconBytes ?? _model.OriginalIconBytes;

            BadgeRusCheck.IsChecked = _model.BadgeRus;
            Badge60FpsCheck.IsChecked = _model.Badge60Fps;
            BadgeModCheck.IsChecked = _model.BadgeMod;
            BadgeAllDlcCheck.IsChecked = _model.BadgeAllDlc;
            CustomBadgeBox.Text = _model.CustomBadgeText ?? "";
            AudioWavPathBox.Text = _model.CustomAudioWavPath ?? "";

            string pos = string.IsNullOrEmpty(_model.BadgePosition) ? "BottomRight" : _model.BadgePosition;
            for (int i = 0; i < BadgePositionCombo.Items.Count; i++)
            {
                if (BadgePositionCombo.Items[i] is ComboBoxItem cbi && string.Equals(cbi.Tag?.ToString(), pos, StringComparison.OrdinalIgnoreCase))
                {
                    BadgePositionCombo.SelectedIndex = i;
                    break;
                }
            }

            ModRomFsBox.Text = _model.ModNameRomFs ?? "";
            ModExeFsBox.Text = _model.ModNameExeFs ?? "";

            if (!_model.HasRomFs && !_model.HasExeFs)
            {
                ModsSection.Visibility = Visibility.Collapsed;
            }
            else
            {
                ModsSection.Visibility = Visibility.Visible;
                RomFsNamePanel.Visibility = _model.HasRomFs ? Visibility.Visible : Visibility.Collapsed;
                ExeFsNamePanel.Visibility = _model.HasExeFs ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private string GetSelectedBadgePosition()
        {
            if (BadgePositionCombo?.SelectedItem is ComboBoxItem cbi && cbi.Tag != null)
                return cbi.Tag.ToString() ?? "BottomRight";
            return "BottomRight";
        }

        private void RefreshBadgePreview()
        {
            if (_isInitializing) return;

            try
            {
                bool bRus = BadgeRusCheck.IsChecked == true;
                bool b60 = Badge60FpsCheck.IsChecked == true;
                bool bMod = BadgeModCheck.IsChecked == true;
                bool bDlc = BadgeAllDlcCheck.IsChecked == true;
                string custom = CustomBadgeBox.Text?.Trim() ?? "";
                string pos = GetSelectedBadgePosition();

                bool anyBadge = bRus || b60 || bMod || bDlc || !string.IsNullOrWhiteSpace(custom);
                byte[]? displayBytes = _baseIconBytes;

                if (anyBadge || displayBytes != null)
                {
                    int targetRes = _model.Is3ds ? 128 : 256;
                    displayBytes = IconBadgeStudioService.RenderIconWithBadges(
                        _baseIconBytes, targetRes, bRus, b60, bMod, bDlc, custom, pos, outputPng: true);
                }

                if (displayBytes != null && displayBytes.Length > 0)
                {
                    using var ms = new MemoryStream(displayBytes);
                    var bmp = new BitmapImage();
                    bmp.SetSource(ms.AsRandomAccessStream());
                    GameIconImage.Source = bmp;
                }
                else
                {
                    GameIconImage.Source = null;
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[ControlEditorDialog] ?????? ??????? ?????? ??????: {ex.Message}", LogLevel.Warning);
            }
        }

        private void Badge_Changed(object sender, RoutedEventArgs e) => RefreshBadgePreview();
        private void CustomBadgeBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshBadgePreview();
        private void BadgePositionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e) => RefreshBadgePreview();

        private void IconDropArea_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "?????????? ????????? ??????";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsGlyphVisible = true;
        }

        private async void IconDropArea_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0 && items[0] is StorageFile file)
                {
                    await LoadCustomIconFileAsync(file.Path);
                }
            }
        }

        private async void SelectIconButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var filePath = await SystemDialogService.OpenFileDialogAsync(
                    "???????? ??????????? ?????? (PNG/JPG/WEBP)",
                    "??????????? (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|??? ????? (*.*)|*.*");

                if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                {
                    await LoadCustomIconFileAsync(filePath);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[ControlEditorDialog] ?????? ?????? ?????: {ex.Message}", LogLevel.Warning);
            }
        }

        private async Task LoadCustomIconFileAsync(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    _baseIconBytes = await File.ReadAllBytesAsync(filePath);
                    _model.CustomIconPath = filePath;
                    RefreshBadgePreview();
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[ControlEditorDialog] ?? ??????? ????????? ??????: {ex.Message}", LogLevel.Error);
            }
        }

        private void ResetIconButton_Click(object sender, RoutedEventArgs e)
        {
            _baseIconBytes = _model.OriginalIconBytes;
            _model.CustomIconBytes = null;
            _model.CustomIconPath = null;
            BadgeRusCheck.IsChecked = false;
            Badge60FpsCheck.IsChecked = false;
            BadgeModCheck.IsChecked = false;
            BadgeAllDlcCheck.IsChecked = false;
            CustomBadgeBox.Text = "";
            RefreshBadgePreview();
        }

        private async void SelectAudioWav_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var filePath = await SystemDialogService.OpenFileDialogAsync(
                    "???????? ?????-?????? ??? 3DS ??????? (.WAV / .BCWAV)",
                    "????? 3DS (*.wav;*.bcwav;*.cwav)|*.wav;*.bcwav;*.cwav|??? ????? (*.*)|*.*");
                if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                {
                    AudioWavPathBox.Text = filePath;
                }
            }
            catch { }
        }

        private void ClearAudioWav_Click(object sender, RoutedEventArgs e)
        {
            AudioWavPathBox.Text = "";
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            _model.TitleNameEnglish = TitleEnglishBox.Text.Trim();
            _model.TitleNameRussian = TitleRussianBox.Text.Trim();
            _model.Publisher = PublisherBox.Text.Trim();
            _model.ModNameRomFs = ModRomFsBox.Text.Trim();
            _model.ModNameExeFs = ModExeFsBox.Text.Trim();

            _model.BadgeRus = BadgeRusCheck.IsChecked == true;
            _model.Badge60Fps = Badge60FpsCheck.IsChecked == true;
            _model.BadgeMod = BadgeModCheck.IsChecked == true;
            _model.BadgeAllDlc = BadgeAllDlcCheck.IsChecked == true;
            _model.CustomBadgeText = CustomBadgeBox.Text?.Trim() ?? "";
            _model.BadgePosition = GetSelectedBadgePosition();
            _model.CustomAudioWavPath = AudioWavPathBox.Text?.Trim() ?? "";

            if (_model.HasBadges || _baseIconBytes != _model.OriginalIconBytes)
            {
                int targetRes = _model.Is3ds ? 48 : 256;
                _model.CustomIconBytes = IconBadgeStudioService.RenderIconWithBadges(
                    _baseIconBytes,
                    targetRes,
                    _model.BadgeRus,
                    _model.Badge60Fps,
                    _model.BadgeMod,
                    _model.BadgeAllDlc,
                    _model.CustomBadgeText,
                    _model.BadgePosition,
                    outputPng: _model.Is3ds);
            }
        }
    }
}
