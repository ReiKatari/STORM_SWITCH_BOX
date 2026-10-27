using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using StormSwitchBox.Models;
using StormSwitchBox.Services;
using Windows.ApplicationModel.DataTransfer;

namespace StormSwitchBox.Views.Dialogs
{
    public sealed partial class DirectConsoleSenderDialog : ContentDialog
    {
        private readonly DirectConsoleSenderService _senderService = new();
        private readonly List<string> _files = new();
        private List<DiscoveredConsoleEndpoint> _discoveredConsoles = new();
        private CancellationTokenSource? _activeOpCts;

        public DirectConsoleSenderDialog(IEnumerable<string>? initialFiles = null)
        {
            this.InitializeComponent();

            _senderService.ServerLogAdded += OnServerLogAdded;
            _senderService.TransferProgressChanged += OnTransferProgressChanged;

            string lanIp = DirectConsoleSenderService.GetPrimaryLanIpAddress();
            string[] parts = lanIp.Split('.');
            if (parts.Length == 4)
            {
                FtpHostBox.Text = $"{parts[0]}.{parts[1]}.{parts[2]}.50";
            }

            if (initialFiles != null)
            {
                foreach (var f in initialFiles.Where(File.Exists))
                {
                    if (!_files.Contains(f, StringComparer.OrdinalIgnoreCase))
                        _files.Add(f);
                }
            }

            RefreshFilesAndRestartServer();
        }

        private void RefreshFilesAndRestartServer()
        {
            FilesComboBox.Items.Clear();
            foreach (var f in _files)
            {
                long sz = 0;
                try { sz = new FileInfo(f).Length; } catch { }
                FilesComboBox.Items.Add($"{Path.GetFileName(f)} ({ProcessingTask.FormatSize(sz)})");
            }

            if (_files.Count > 0)
            {
                FilesComboBox.SelectedIndex = 0;
                try
                {
                    _senderService.StartHttpInstallServer(_files);
                    UpdateQrAndUrlForSelection(0);
                }
                catch (Exception ex)
                {
                    StatusText.Text = "⚠️ Ошибка запуска HTTP-сервера: " + ex.Message;
                }
            }
            else
            {
                DirectUrlBox.Text = $"http://{DirectConsoleSenderService.GetPrimaryLanIpAddress()}:8765/";
                AppendLog("[Система] Выберите файл (.NSP/.NSZ/.XCI/.CIA/.3DS) для активации HTTP/QR раздачи или отправки по FTP.");
            }
        }

        private async void UpdateQrAndUrlForSelection(int index)
        {
            if (index < 0 || index >= _files.Count) return;

            string file = _files[index];
            string ext = Path.GetExtension(file).ToLowerInvariant();
            if (ext == ".cia" || ext == ".3ds" || ext == ".cci" || ext == ".cxi")
            {
                FtpRemoteDirBox.Text = "/cias";
            }
            else
            {
                FtpRemoteDirBox.Text = "/switch";
            }

            string shortUrl = _senderService.GetShortFileUrl(index);
            DirectUrlBox.Text = shortUrl;

            try
            {
                byte[] qrPng = DirectConsoleSenderService.GenerateQrCodePngBytes(shortUrl, scale: 8);
                using var ms = new MemoryStream(qrPng);
                var bmp = new BitmapImage();
                await bmp.SetSourceAsync(ms.AsRandomAccessStream());
                QrCodeImage.Source = bmp;
            }
            catch (Exception ex)
            {
                AppendLog("[QR] Ошибка генерации QR: " + ex.Message);
            }
        }

        private void FilesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FilesComboBox.SelectedIndex >= 0)
            {
                UpdateQrAndUrlForSelection(FilesComboBox.SelectedIndex);
            }
        }

        private async void BrowseFiles_Click(object sender, RoutedEventArgs e)
        {
            var picked = await SystemDialogService.OpenMultipleFilesDialogAsync(
                "Выберите игровые пакеты для отправки на консоль Switch / 3DS",
                "Пакеты Switch и 3DS (*.nsp;*.nsz;*.xci;*.xcz;*.cia;*.3ds;*.cci;*.nro)|*.nsp;*.nsz;*.xci;*.xcz;*.cia;*.3ds;*.cci;*.nro|Все файлы (*.*)|*.*");

            if (picked != null && picked.Count > 0)
            {
                _files.Clear();
                _files.AddRange(picked.Where(File.Exists));
                RefreshFilesAndRestartServer();
            }
        }

        private void CopyUrl_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(DirectUrlBox.Text)) return;
            var pkg = new DataPackage();
            pkg.SetText(DirectUrlBox.Text);
            Clipboard.SetContent(pkg);
            StatusText.Text = "✅ Ссылка скопирована в буфер обмена: " + DirectUrlBox.Text;
        }

        private async void ScanLan_Click(object sender, RoutedEventArgs e)
        {
            ScanLanBtn.IsEnabled = false;
            StatusText.Text = "🔍 Поиск консолей Switch / 3DS (порты 5000, 21) в локальной сети...";
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                _discoveredConsoles = await DirectConsoleSenderService.DiscoverConsolesInLanAsync(cts.Token);
                DiscoveredConsolesCombo.Items.Clear();

                foreach (var c in _discoveredConsoles)
                {
                    DiscoveredConsolesCombo.Items.Add(c.DisplayLabel);
                }

                if (_discoveredConsoles.Count > 0)
                {
                    DiscoveredConsolesCombo.SelectedIndex = 0;
                    StatusText.Text = $"✅ Найдено активных консолей в сети: {_discoveredConsoles.Count}";
                    AppendLog($"[LAN Scan] Обнаружено устройств: {_discoveredConsoles.Count}");
                }
                else
                {
                    StatusText.Text = "ℹ️ Открытых FTP-серверов (порты 5000/21) в подсети не найдено. Убедитесь, что на консоли запущен DBI FTP / ftpd.";
                }
            }
            catch (Exception ex)
            {
                StatusText.Text = "⚠️ Ошибка сканирования сети: " + ex.Message;
            }
            finally
            {
                ScanLanBtn.IsEnabled = true;
            }
        }

        private void DiscoveredConsolesCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int idx = DiscoveredConsolesCombo.SelectedIndex;
            if (idx >= 0 && idx < _discoveredConsoles.Count)
            {
                var ep = _discoveredConsoles[idx];
                FtpHostBox.Text = ep.IpAddress;
                FtpPortBox.Text = ep.Port.ToString();
            }
        }

        private async void SendFtp_Click(object sender, RoutedEventArgs e)
        {
            int idx = Math.Max(0, FilesComboBox.SelectedIndex);
            if (_files.Count == 0 || idx >= _files.Count)
            {
                StatusText.Text = "⚠️ Сначала выберите файл для отправки.";
                return;
            }

            string host = FtpHostBox.Text.Trim();
            if (string.IsNullOrEmpty(host))
            {
                StatusText.Text = "⚠️ Укажите IP-адрес консоли.";
                return;
            }

            if (!int.TryParse(FtpPortBox.Text.Trim(), out int port) || port <= 0 || port > 65535)
                port = 5000;

            string remoteDir = string.IsNullOrWhiteSpace(FtpRemoteDirBox.Text) ? "/" : FtpRemoteDirBox.Text.Trim();
            string file = _files[idx];

            SendFtpBtn.IsEnabled = false;
            _activeOpCts?.Cancel();
            _activeOpCts = new CancellationTokenSource();

            try
            {
                StatusText.Text = $"📡 Подключение по FTP к {host}:{port} и передача {Path.GetFileName(file)}...";
                await _senderService.UploadFileViaFtpAsync(file, host, port, remoteDir, _activeOpCts.Token);
                TransferProgressBar.Value = 100;
                StatusText.Text = $"✅ Файл {Path.GetFileName(file)} успешно отправлен на консоль ({host}:{port}{remoteDir})!";
            }
            catch (Exception ex)
            {
                StatusText.Text = "❌ Ошибка FTP-передачи: " + ex.Message;
                AppendLog("[FTP Ошибка] " + ex.Message);
            }
            finally
            {
                SendFtpBtn.IsEnabled = true;
            }
        }

        private async void CopyUsbDrive_Click(object sender, RoutedEventArgs e)
        {
            int idx = Math.Max(0, FilesComboBox.SelectedIndex);
            if (_files.Count == 0 || idx >= _files.Count)
            {
                StatusText.Text = "⚠️ Сначала выберите файл для копирования.";
                return;
            }

            string file = _files[idx];
            string? targetFolder = await SystemDialogService.OpenFolderDialogAsync("Выберите папку на SD-карте или USB-накопителе консоли");
            if (string.IsNullOrWhiteSpace(targetFolder) || !Directory.Exists(targetFolder)) return;

            CopyUsbDriveBtn.IsEnabled = false;
            try
            {
                string destPath = Path.Combine(targetFolder, Path.GetFileName(file));
                long total = new FileInfo(file).Length;
                StatusText.Text = $"💾 Копирование {Path.GetFileName(file)} на {targetFolder}...";

                await Task.Run(async () =>
                {
                    byte[] buf = new byte[4 * 1024 * 1024];
                    using var src = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, buf.Length);
                    using var dst = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, buf.Length);
                    long copied = 0;
                    int r;
                    while ((r = await src.ReadAsync(buf, 0, buf.Length)) > 0)
                    {
                        await dst.WriteAsync(buf, 0, r);
                        copied += r;
                        OnTransferProgressChanged(Path.GetFileName(file), copied, total);
                    }
                });

                TransferProgressBar.Value = 100;
                StatusText.Text = $"✅ Скопировано на накопитель: {destPath}";
                AppendLog($"[USB/SD] Файл скопирован: {destPath}");
            }
            catch (Exception ex)
            {
                StatusText.Text = "❌ Ошибка копирования: " + ex.Message;
            }
            finally
            {
                CopyUsbDriveBtn.IsEnabled = true;
            }
        }

        private void OnTransferProgressChanged(string fileName, long sent, long total)
        {
            double pct = total > 0 ? (sent * 100.0 / total) : 0;
            App.RunOnUI(() =>
            {
                TransferProgressBar.Value = pct;
                StatusText.Text = $"⚡ Передача {fileName}: {ProcessingTask.FormatSize(sent)} / {ProcessingTask.FormatSize(total)} ({pct:F1}%)";
            });
        }

        private void OnServerLogAdded(string msg)
        {
            AppendLog(msg);
        }

        private void AppendLog(string msg)
        {
            App.RunOnUI(() =>
            {
                string line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
                ServerLogText.Text = string.IsNullOrEmpty(ServerLogText.Text)
                    ? line
                    : (ServerLogText.Text + "\n" + line);
                LogScrollViewer.ChangeView(null, LogScrollViewer.ScrollableHeight, null);
            });
        }

        private void Dialog_Closed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            try { _activeOpCts?.Cancel(); } catch { }
            _senderService.Dispose();
        }
    }
}
