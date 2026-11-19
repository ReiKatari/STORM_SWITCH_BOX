using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public class DbiInstallService
    {
        private static readonly HashSet<string> AllowedGameExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".nsp", ".nsz", ".xci", ".xcz", ".nup", ".cia", ".3ds"
        };

        private HttpListener? _httpListener;
        private CancellationTokenSource? _httpCts;
        private DirectConsoleSenderService? _fallbackSender;
        private string _servingFolder = "";
        private int _servingPort = 8080;

        public bool IsHttpServerRunning => (_httpListener?.IsListening == true) || (_fallbackSender?.IsHttpServerRunning == true);
        public string ServingFolder => _servingFolder;
        public int ServingPort => _servingPort;

        private static void ReleaseComObjectSafe(object? comObj)
        {
            if (comObj == null) return;
            try
            {
                if (Marshal.IsComObject(comObj))
                    Marshal.FinalReleaseComObject(comObj);
            }
            catch { }
        }

        /// <summary>
        /// Проверяет, подключена ли консоль Nintendo Switch с запущенным DBI MTP Responder.
        /// Возвращает динамический объект папки установки MTP ("5: MicroSD install" или "6: NAND install").
        /// </summary>
        public bool DetectDbiMtp(out dynamic? mtpInstallFolder, out string statusMessage)
        {
            mtpInstallFolder = null;
            statusMessage = "";

            object? shell = null;
            object? myComputer = null;
            object? myComputerItems = null;
            try
            {
                Type? shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null)
                {
                    statusMessage = "Компонент Windows Shell недоступен.";
                    return false;
                }

                shell = Activator.CreateInstance(shellType);
                if (shell == null)
                {
                    statusMessage = "Не удалось инициализировать Shell.Application.";
                    return false;
                }

                dynamic dynShell = shell;
                myComputer = dynShell.Namespace(17); // ssfDRIVES / This PC
                if (myComputer == null)
                {
                    statusMessage = "Не удалось открыть пространство Мой компьютер.";
                    return false;
                }

                dynamic dynMyComputer = myComputer;
                myComputerItems = dynMyComputer.Items();
                foreach (var item in (System.Collections.IEnumerable)myComputerItems)
                {
                    object? folder = null;
                    object? folderItems = null;
                    try
                    {
                        dynamic dynItem = item;
                        string itemName = (string)dynItem.Name;
                        if (itemName.Contains("Switch", StringComparison.OrdinalIgnoreCase))
                        {
                            folder = dynItem.GetFolder;
                            if (folder != null)
                            {
                                dynamic dynFolder = folder;
                                folderItems = dynFolder.Items();
                                foreach (var subItem in (System.Collections.IEnumerable)folderItems)
                                {
                                    try
                                    {
                                        dynamic dynSub = subItem;
                                        string subName = (string)dynSub.Name;
                                        if (subName.Contains("5: MicroSD install", StringComparison.OrdinalIgnoreCase) ||
                                            subName.Contains("MicroSD install", StringComparison.OrdinalIgnoreCase))
                                        {
                                            mtpInstallFolder = dynSub.GetFolder;
                                            statusMessage = $"Обнаружен DBI MTP: {itemName} -> {subName}";
                                            return true;
                                        }
                                    }
                                    finally
                                    {
                                        ReleaseComObjectSafe(subItem);
                                    }
                                }

                                // Фоллбэк на NAND install если MicroSD install не найден
                                ReleaseComObjectSafe(folderItems);
                                folderItems = dynFolder.Items();
                                foreach (var subItem in (System.Collections.IEnumerable)folderItems)
                                {
                                    try
                                    {
                                        dynamic dynSub = subItem;
                                        string subName = (string)dynSub.Name;
                                        if (subName.Contains("6: NAND install", StringComparison.OrdinalIgnoreCase) ||
                                            subName.Contains("NAND install", StringComparison.OrdinalIgnoreCase))
                                        {
                                            mtpInstallFolder = dynSub.GetFolder;
                                            statusMessage = $"Обнаружен DBI MTP: {itemName} -> {subName}";
                                            return true;
                                        }
                                    }
                                    finally
                                    {
                                        ReleaseComObjectSafe(subItem);
                                    }
                                }
                            }
                        }
                    }
                    finally
                    {
                        ReleaseComObjectSafe(folderItems);
                        ReleaseComObjectSafe(folder);
                        ReleaseComObjectSafe(item);
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
            finally
            {
                ReleaseComObjectSafe(myComputerItems);
                ReleaseComObjectSafe(myComputer);
                ReleaseComObjectSafe(shell);
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

            if (!DetectDbiMtp(out dynamic? mtpFolder, out string detectMsg) || mtpFolder == null)
            {
                ReleaseComObjectSafe((object?)mtpFolder);
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

            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        // Вызываем Folder.CopyHere с флагом 16 (Respond with "Yes to All" for any dialog box)
                        dynamic targetFolder = mtpFolder!;
                        targetFolder.CopyHere(filePath, 16);
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Ошибка копирования файла в MTP-папку Switch: {ex.Message}");
                    }
                }, ct);
            }
            finally
            {
                ReleaseComObjectSafe((object?)mtpFolder);
            }

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

            _servingFolder = Path.GetFullPath(folderToServe);
            _servingPort = port;
            _httpCts = new CancellationTokenSource();

            string[] prefixesToTry =
            {
                $"http://*:{port}/",
                $"http://+:{port}/",
                $"http://localhost:{port}/"
            };

            HttpListener? startedListener = null;
            string? boundPrefix = null;
            foreach (string prefix in prefixesToTry)
            {
                HttpListener? candidate = null;
                try
                {
                    candidate = new HttpListener();
                    candidate.Prefixes.Add(prefix);
                    candidate.Start();
                    startedListener = candidate;
                    boundPrefix = prefix;
                    break;
                }
                catch (HttpListenerException)
                {
                    try { candidate?.Close(); } catch { }
                }
                catch
                {
                    try { candidate?.Close(); } catch { }
                }
            }

            if (startedListener != null)
            {
                _httpListener = startedListener;
                Task.Run(() => HandleIncomingRequestsAsync(_httpCts.Token));
                App.Logger.Log($"[DBI Server] HTTP сервер запущен ({boundPrefix}), каталог: {_servingFolder}", LogLevel.Info);
                return;
            }

            // Fallback без прав администратора через DirectConsoleSenderService (TcpListener)
            try
            {
                var gameFiles = Directory.Exists(_servingFolder)
                    ? Directory.EnumerateFiles(_servingFolder, "*.*", SearchOption.AllDirectories)
                        .Where(f => AllowedGameExtensions.Contains(Path.GetExtension(f)))
                        .ToList()
                    : new List<string>();

                if (gameFiles.Count > 0)
                {
                    var sender = new DirectConsoleSenderService();
                    string url = sender.StartHttpInstallServer(gameFiles, port);
                    _fallbackSender = sender;
                    _servingPort = sender.ActiveHttpPort;
                    App.Logger.Log($"[DBI Server] Запущен резервный HTTP-сервер без прав администратора: {url} (файлов: {gameFiles.Count})", LogLevel.Info);
                }
                else
                {
                    App.Logger.Log($"[DBI Server] Не удалось запустить HttpListener на порту {port} без прав администратора и не найдены игровые файлы для резервного сервера.", LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[DBI Server] Ошибка запуска резервного HTTP-сервера: {ex.Message}", LogLevel.Warning);
            }
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
                _httpCts?.Dispose();
                _httpCts = null;
                App.Logger.Log("[DBI Server] HTTP сервер остановлен", LogLevel.Info);
            }

            if (_fallbackSender != null)
            {
                try { _fallbackSender.Dispose(); } catch { }
                _fallbackSender = null;
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

                // Корневой запрос: выдаем список файлов в формате JSON (DBI menu)
                if (string.IsNullOrEmpty(rawPath) || rawPath.Equals("dbimenu.json", StringComparison.OrdinalIgnoreCase))
                {
                    string normalizedRoot = _servingFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var files = Directory.Exists(_servingFolder)
                        ? Directory.EnumerateFiles(_servingFolder, "*.*", SearchOption.AllDirectories)
                            .Where(f => AllowedGameExtensions.Contains(Path.GetExtension(f)))
                            .Select(f => (object)new
                            {
                                filepath = Path.GetRelativePath(normalizedRoot, f).Replace('\\', '/'),
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

                // Защита от Path Traversal и ограничение только разрешёнными игровыми расширениями
                string localFilePath;
                try
                {
                    localFilePath = StormToolResolver.EnsureSafeSubPath(_servingFolder, rawPath);
                }
                catch
                {
                    res.StatusCode = 403;
                    byte[] forbidden = Encoding.UTF8.GetBytes("Forbidden");
                    res.ContentLength64 = forbidden.Length;
                    res.OutputStream.Write(forbidden, 0, forbidden.Length);
                    res.OutputStream.Close();
                    return;
                }

                if (!AllowedGameExtensions.Contains(Path.GetExtension(localFilePath)))
                {
                    res.StatusCode = 403;
                    byte[] forbiddenExt = Encoding.UTF8.GetBytes("Forbidden File Type");
                    res.ContentLength64 = forbiddenExt.Length;
                    res.OutputStream.Write(forbiddenExt, 0, forbiddenExt.Length);
                    res.OutputStream.Close();
                    return;
                }

                if (File.Exists(localFilePath))
                {
                    ServeFileWithRangeSupport(req, res, localFilePath);
                    return;
                }

                res.StatusCode = 404;
                byte[] notFound = Encoding.UTF8.GetBytes("File Not Found");
                res.ContentLength64 = notFound.Length;
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
            long end = totalLen > 0 ? totalLen - 1 : -1;

            string? rangeHeader = req.Headers["Range"];
            if (!string.IsNullOrWhiteSpace(rangeHeader))
            {
                var match = Regex.Match(rangeHeader, @"bytes=(\d*)-(\d*)", RegexOptions.IgnoreCase);
                string g1 = match.Success ? match.Groups[1].Value : string.Empty;
                string g2 = match.Success ? match.Groups[2].Value : string.Empty;

                bool validRange = false;
                if (match.Success && totalLen > 0 && (!string.IsNullOrEmpty(g1) || !string.IsNullOrEmpty(g2)))
                {
                    if (string.IsNullOrEmpty(g1))
                    {
                        // Суффиксный диапазон bytes=-N
                        if (long.TryParse(g2, out long suffixLen) && suffixLen > 0)
                        {
                            start = Math.Max(0, totalLen - suffixLen);
                            end = totalLen - 1;
                            validRange = true;
                        }
                    }
                    else if (long.TryParse(g1, out long parsedStart))
                    {
                        start = parsedStart;
                        if (string.IsNullOrEmpty(g2))
                        {
                            end = totalLen - 1;
                            validRange = start >= 0 && start < totalLen;
                        }
                        else if (long.TryParse(g2, out long parsedEnd))
                        {
                            end = Math.Min(totalLen - 1, parsedEnd);
                            validRange = start >= 0 && start < totalLen && end >= start;
                        }
                    }
                }

                if (!validRange)
                {
                    res.StatusCode = 416;
                    res.AddHeader("Content-Range", $"bytes */{totalLen}");
                    res.ContentLength64 = 0;
                    res.OutputStream.Close();
                    return;
                }

                res.StatusCode = 206;
                res.AddHeader("Content-Range", $"bytes {start}-{end}/{totalLen}");
            }
            else
            {
                res.StatusCode = 200;
            }

            long toSend = totalLen == 0 ? 0 : (end - start + 1);
            res.ContentLength64 = toSend;

            if (toSend <= 0)
            {
                res.OutputStream.Close();
                return;
            }

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
