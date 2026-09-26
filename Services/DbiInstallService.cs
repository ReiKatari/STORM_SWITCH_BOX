using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public class DbiInstallService
    {
        private HttpListener? _httpListener;
        private CancellationTokenSource? _httpCts;
        private string _servingFolder = "";
        private int _servingPort = 8080;

        public bool IsHttpServerRunning => _httpListener?.IsListening == true;
        public string ServingFolder => _servingFolder;
        public int ServingPort => _servingPort;

        /// <summary>
        /// Проверяет, подключена ли консоль Nintendo Switch с запущенным DBI MTP Responder.
        /// Возвращает динамический объект папки установки MTP ("5: MicroSD install" или "6: NAND install").
        /// </summary>
        public bool DetectDbiMtp(out dynamic? mtpInstallFolder, out string statusMessage)
        {
            mtpInstallFolder = null;
            statusMessage = "";

            try
            {
                Type? shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null)
                {
                    statusMessage = "Компонент Windows Shell недоступен.";
                    return false;
                }

                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell == null)
                {
                    statusMessage = "Не удалось инициализировать Shell.Application.";
                    return false;
                }

                dynamic? myComputer = shell.Namespace(17); // ssfDRIVES / This PC
                if (myComputer == null)
                {
                    statusMessage = "Не удалось открыть пространство Мой компьютер.";
                    return false;
                }

                foreach (var item in myComputer.Items())
                {
                    string itemName = (string)item.Name;
                    if (itemName.Contains("Switch", StringComparison.OrdinalIgnoreCase))
                    {
                        dynamic? folder = item.GetFolder;
                        if (folder != null)
                        {
                            foreach (var subItem in folder.Items())
                            {
                                string subName = (string)subItem.Name;
                                if (subName.Contains("5: MicroSD install", StringComparison.OrdinalIgnoreCase) ||
                                    subName.Contains("MicroSD install", StringComparison.OrdinalIgnoreCase))
                                {
                                    mtpInstallFolder = subItem.GetFolder;
                                    statusMessage = $"Обнаружен DBI MTP: {itemName} -> {subName}";
                                    return true;
                                }
                            }

                            // Фоллбэк на NAND install если MicroSD install не найден
                            foreach (var subItem in folder.Items())
                            {
                                string subName = (string)subItem.Name;
                                if (subName.Contains("6: NAND install", StringComparison.OrdinalIgnoreCase) ||
                                    subName.Contains("NAND install", StringComparison.OrdinalIgnoreCase))
                                {
                                    mtpInstallFolder = subItem.GetFolder;
                                    statusMessage = $"Обнаружен DBI MTP: {itemName} -> {subName}";
                                    return true;
                                }
                            }
                        }
                    }
                }

                statusMessage = "Консоль Nintendo Switch не обнаружена в режиме DBI MTP Responder.";
                return false;
            }
            catch (Exception ex)
            {
                statusMessage = $"Ошибка поиска DBI MTP: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Выполняет быструю установку файла (.nsp / .nsz / .xci / .xcz) на консоль через DBI MTP.
        /// </summary>
        public async Task<bool> InstallViaMtpAsync(string filePath, ProcessingTask? task, CancellationToken ct)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Файл для установки не найден на диске", filePath);
            }

            if (!DetectDbiMtp(out dynamic? mtpFolder, out string detectMsg))
            {
                throw new InvalidOperationException(detectMsg);
            }

            string fileName = Path.GetFileName(filePath);
            App.RunOnUI(() =>
            {
                if (task != null)
                {
                    task.Status = "Установка по USB (DBI)...";
                    task.Progress = 5;
                    task.LogDetails += $"\n📲 [DBI MTP] Найдена консоль. Начинается передача файла: {fileName}";
                }
            });

            await Task.Run(() =>
            {
                try
                {
                    // Вызываем Folder.CopyHere с флагом 16 (Respond with "Yes to All" for any dialog box)
                    mtpFolder.CopyHere(filePath, 16);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Ошибка копирования файла в MTP-папку Switch: {ex.Message}");
                }
            }, ct);

            App.RunOnUI(() =>
            {
                if (task != null)
                {
                    task.Progress = 100;
                    task.Status = "Установлено на Switch";
                    task.LogDetails += $"\n✓ [DBI MTP] Файл успешно передан на консоль Nintendo Switch: {fileName}";
                }
            });

            return true;
        }

        /// <summary>
        /// Получает локальный IP-адрес ПК в сети Wi-Fi/LAN для DBI.
        /// </summary>
        public string GetLocalIpAddress()
        {
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                                n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .OrderByDescending(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                                            n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211);

                foreach (var ni in interfaces)
                {
                    var ipProps = ni.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !IPAddress.IsLoopback(addr.Address))
                        {
                            return addr.Address.ToString();
                        }
                    }
                }
            }
            catch { }

            return "127.0.0.1";
        }

        /// <summary>
        /// Запускает встроенный HTTP-сервер для установки игр через сетевой клиент DBI (Install title from Web/HTTP).
        /// </summary>
        public void StartHttpServer(string folderToServe, int port = 8080)
        {
            StopHttpServer();

            _servingFolder = folderToServe;
            _servingPort = port;
            _httpCts = new CancellationTokenSource();

            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add($"http://*:{port}/");
                _httpListener.Start();
            }
            catch
            {
                // Если привязка к http://*:port требует прав администратора, привязываемся к localhost / 0.0.0.0
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add($"http://+:{port}/");
                _httpListener.Start();
            }

            Task.Run(() => HandleIncomingRequestsAsync(_httpCts.Token));

            App.Logger.Log($"[DBI Server] HTTP сервер запущен на порту {port}, каталог: {folderToServe}", LogLevel.Info);
        }

        public void StopHttpServer()
        {
            if (_httpListener != null)
            {
                try
                {
                    _httpCts?.Cancel();
                    _httpListener.Stop();
                    _httpListener.Close();
                }
                catch { }
                _httpListener = null;
                _httpCts = null;
                App.Logger.Log("[DBI Server] HTTP сервер остановлен", LogLevel.Info);
            }
        }

        private async Task HandleIncomingRequestsAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _httpListener?.IsListening == true)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();
                    _ = Task.Run(() => ProcessHttpRequest(context), ct);
                }
                catch
                {
                    if (ct.IsCancellationRequested) break;
                }
            }
        }

        private void ProcessHttpRequest(HttpListenerContext context)
        {
            var req = context.Request;
            var res = context.Response;

            try
            {
                string rawPath = WebUtility.UrlDecode(req.Url?.AbsolutePath ?? "/").TrimStart('/');

                // Корневой запрос: выдаем список файлов в формате JSON (DBI menu) и HTML (для браузера)
                if (string.IsNullOrEmpty(rawPath) || rawPath.Equals("dbimenu.json", StringComparison.OrdinalIgnoreCase))
                {
                    var allowedExts = new[] { ".nsp", ".nsz", ".xci", ".xcz" };
                    var files = Directory.Exists(_servingFolder)
                        ? Directory.GetFiles(_servingFolder, "*.*", SearchOption.AllDirectories)
                            .Where(f => allowedExts.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                            .Select(f => (object)new
                            {
                                filepath = f.Substring(_servingFolder.Length).TrimStart('\\', '/').Replace('\\', '/'),
                                filename = Path.GetFileName(f),
                                size = new FileInfo(f).Length
                            })
                            .ToList()
                        : new List<object>();

                    byte[] jsonBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(files, new JsonSerializerOptions { WriteIndented = true }));
                    res.ContentType = "application/json; charset=utf-8";
                    res.ContentLength64 = jsonBytes.Length;
                    res.StatusCode = 200;
                    res.OutputStream.Write(jsonBytes, 0, jsonBytes.Length);
                    res.OutputStream.Close();
                    return;
                }

                // Запрос файла игры
                string localFilePath = Path.Combine(_servingFolder, rawPath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(localFilePath))
                {
                    ServeFileWithRangeSupport(req, res, localFilePath);
                    return;
                }

                res.StatusCode = 404;
                byte[] notFound = Encoding.UTF8.GetBytes("File Not Found");
                res.OutputStream.Write(notFound, 0, notFound.Length);
                res.OutputStream.Close();
            }
            catch
            {
                try { res.StatusCode = 500; res.Close(); } catch { }
            }
        }

        private void ServeFileWithRangeSupport(HttpListenerRequest req, HttpListenerResponse res, string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            long totalLen = fileInfo.Length;

            res.ContentType = "application/octet-stream";
            res.AddHeader("Accept-Ranges", "bytes");

            long start = 0;
            long end = totalLen - 1;

            if (req.Headers["Range"] != null)
            {
                string rangeHeader = req.Headers["Range"]!;
                var match = System.Text.RegularExpressions.Regex.Match(rangeHeader, @"bytes=(\d*)-(\d*)");
                if (match.Success)
                {
                    if (!string.IsNullOrEmpty(match.Groups[1].Value))
                        start = long.Parse(match.Groups[1].Value);
                    if (!string.IsNullOrEmpty(match.Groups[2].Value))
                        end = long.Parse(match.Groups[2].Value);

                    res.StatusCode = 206;
                    res.AddHeader("Content-Range", $"bytes {start}-{end}/{totalLen}");
                }
            }
            else
            {
                res.StatusCode = 200;
            }

            long toSend = end - start + 1;
            res.ContentLength64 = toSend;

            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            fs.Seek(start, SeekOrigin.Begin);

            byte[] buffer = new byte[64 * 1024];
            long bytesRemaining = toSend;

            while (bytesRemaining > 0)
            {
                int read = fs.Read(buffer, 0, (int)Math.Min(buffer.Length, bytesRemaining));
                if (read <= 0) break;
                res.OutputStream.Write(buffer, 0, read);
                bytesRemaining -= read;
            }

            res.OutputStream.Close();
        }
    }
}
