using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using StormSwitchBox.Models;
using StormSwitchBox.Services;
using WinRT.Interop;

namespace StormSwitchBox.Views.Dialogs
{
    public sealed partial class ControlEditorDialog : ContentDialog
    {
        private GameMetadataEditModel _model;

        public ControlEditorDialog(GameMetadataEditModel model)
        {
            this.InitializeComponent();
            _model = model;
            this.XamlRoot = App.MainWindow?.Content?.XamlRoot;

            PopulateUI();
        }

        private void PopulateUI()
        {
            TitleIdText.Text = _model.TitleId;
            VersionText.Text = string.IsNullOrEmpty(_model.Version) ? "1.0.0" : _model.Version;
            TitleEnglishBox.Text = _model.TitleNameEnglish ?? "";
            TitleRussianBox.Text = _model.TitleNameRussian ?? "";
            PublisherBox.Text = _model.Publisher ?? "";

            ModRomFsBox.Text = string.IsNullOrEmpty(_model.ModNameRomFs) ? "Модификации: RomFS" : _model.ModNameRomFs;
            ModExeFsBox.Text = string.IsNullOrEmpty(_model.ModNameExeFs) ? "Модификации: ExeFS" : _model.ModNameExeFs;

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

            UpdateIconPreview();
        }

        private void UpdateIconPreview()
        {
            try
            {
                byte[]? bytesToUse = _model.CustomIconBytes ?? _model.OriginalIconBytes;
                if (bytesToUse != null && bytesToUse.Length > 0)
                {
                    using var ms = new MemoryStream(bytesToUse);
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
                App.Logger.Log($"[ControlEditorDialog] Ошибка отображения иконки: {ex.Message}", LogLevel.Warning);
            }
        }

        private void IconDropArea_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Установить кастомную иконку";
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
                    "Выберите изображение иконки (PNG/JPG/WEBP)",
                    "Изображения (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp|Все файлы (*.*)|*.*");

                if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                {
                    await LoadCustomIconFileAsync(filePath);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[ControlEditorDialog] Ошибка выбора файла: {ex.Message}", LogLevel.Warning);
            }
        }

        private async Task LoadCustomIconFileAsync(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    byte[] bytes = await File.ReadAllBytesAsync(filePath);
                    _model.CustomIconBytes = bytes;
                    _model.CustomIconPath = filePath;
                    UpdateIconPreview();
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[ControlEditorDialog] Не удалось загрузить иконку: {ex.Message}", LogLevel.Error);
            }
        }

        private void ResetIconButton_Click(object sender, RoutedEventArgs e)
        {
            _model.CustomIconBytes = null;
            _model.CustomIconPath = null;
            UpdateIconPreview();
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            _model.TitleNameEnglish = TitleEnglishBox.Text.Trim();
            _model.TitleNameRussian = TitleRussianBox.Text.Trim();
            _model.Publisher = PublisherBox.Text.Trim();
            _model.ModNameRomFs = string.IsNullOrWhiteSpace(ModRomFsBox.Text) ? "Модификации: RomFS" : ModRomFsBox.Text.Trim();
            _model.ModNameExeFs = string.IsNullOrWhiteSpace(ModExeFsBox.Text) ? "Модификации: ExeFS" : ModExeFsBox.Text.Trim();
        }
    }
}
