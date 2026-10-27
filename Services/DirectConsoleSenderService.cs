using System;
using System.Buffers;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
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
    public class DiscoveredConsoleEndpoint
    {
        public string IpAddress { get; set; } = string.Empty;
        public int Port { get; set; } = 5000;
        public string ServiceName { get; set; } = string.Empty;
        public string Banner { get; set; } = string.Empty;
        public string DisplayLabel => $"{IpAddress}:{Port} — {ServiceName}" + (string.IsNullOrEmpty(Banner) ? "" : $" ({Banner})");
    }

    /// <summary>
    /// Сервис прямой отправки игр на консоли Nintendo Switch и Nintendo 3DS по локальной сети (HTTP / Tinfoil / DBI / FTP) и USB/SD:
    /// - Встроенный не требующий прав администратора HTTP/1.1 сервер (TcpListener) с поддержкой Range (206 Partial Content) и Tinfoil JSON индекса.
    /// - Встроенный генератор QR-кодов ISO/IEC 18004 для мгновенной установки .CIA на Nintendo 3DS через FBI (Remote Install -> Scan QR Code).
    /// - Прямой асинхронный FTP-клиент (PASV, бинарный поток) с автообнаружением консоли в локальной подсети (порты 5000, 21).
    /// </summary>
    public sealed class DirectConsoleSenderService : IDisposable
    {
        public static bool IsSupportedPackage(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext is ".nsp" or ".nsz" or ".xci" or ".xcz" or ".cia" or ".3ds" or ".cci" or ".cxi" or ".nro";
        }

        private TcpListener? _httpListener;
        private CancellationTokenSource? _httpCts;
        private List<string> _hostedFiles = new();
        public int ActiveHttpPort { get; private set; } = 8765;
        public bool IsHttpServerRunning => _httpListener != null;

        public event Action<string, long, long>? TransferProgressChanged;
        public event Action<string>? ServerLogAdded;

        public static string GetPrimaryLanIpAddress()
        {
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up ||
                        ni.NetworkInterfaceType == NetworkInterfaceType.Loopback ||
                        ni.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase) ||
                        ni.Description.Contains("VPN", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var props = ni.GetIPProperties();
                    if (props.GatewayAddresses.Count == 0) continue;

                    foreach (var ua in props.UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ua.Address))
                        {
                            return ua.Address.ToString();
                        }
                    }
                }

                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
                socket.Connect("8.8.8.8", 65530);
                if (socket.LocalEndPoint is IPEndPoint endPoint)
                    return endPoint.Address.ToString();
            }
            catch { }

            return "127.0.0.1";
        }

        public string StartHttpInstallServer(IEnumerable<string> filePaths, int preferredPort = 8765)
        {
            StopHttpInstallServer();

            _hostedFiles = filePaths.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (_hostedFiles.Count == 0)
                throw new InvalidOperationException("Не выбрано ни одного существующего файла для раздачи.");

            int port = preferredPort;
            for (int attempt = 0; attempt < 15; attempt++)
            {
                try
                {
                    var listener = new TcpListener(IPAddress.Any, port);
                    listener.Start();
                    _httpListener = listener;
                    ActiveHttpPort = port;
                    break;
                }
                catch
                {
                    port++;
                }
            }

            if (_httpListener == null)
                throw new InvalidOperationException("Не удалось запустить локальный HTTP-сервер на свободных портах.");

            _httpCts = new CancellationTokenSource();
            string lanIp = GetPrimaryLanIpAddress();
            _ = Task.Run(() => AcceptHttpClientsLoopAsync(_httpListener, _httpCts.Token));

            ServerLogAdded?.Invoke($"[HTTP Server] Запущен на http://{lanIp}:{ActiveHttpPort}/ (файлов: {_hostedFiles.Count})");
            return $"http://{lanIp}:{ActiveHttpPort}/";
        }

        public void StopHttpInstallServer()
        {
            try { _httpCts?.Cancel(); } catch { }
            try { _httpListener?.Stop(); } catch { }
            _httpListener = null;
            _httpCts = null;
        }

        public string GetShortFileUrl(int index = 0)
        {
            if (index < 0 || index >= _hostedFiles.Count) index = 0;
            string ext = Path.GetExtension(_hostedFiles[index]).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext)) ext = ".cia";
            string lanIp = GetPrimaryLanIpAddress();
            return $"http://{lanIp}:{ActiveHttpPort}/dl/{index}{ext}";
        }

        private async Task AcceptHttpClientsLoopAsync(TcpListener listener, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync(ct);
                    _ = Task.Run(() => HandleHttpClientAsync(client, ct), ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    if (ct.IsCancellationRequested) break;
                }
            }
        }

        private async Task HandleHttpClientAsync(TcpClient client, CancellationToken ct)
        {
            using (client)
            {
                try
                {
                    client.SendBufferSize = 4 * 1024 * 1024;
                    using var netStream = client.GetStream();
                    byte[] headerBuf = new byte[8192];
                    int read = await netStream.ReadAsync(headerBuf, 0, headerBuf.Length, ct);
                    if (read <= 0) return;

                    string requestText = Encoding.UTF8.GetString(headerBuf, 0, read);
                    string[] lines = requestText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length == 0) return;

                    string[] reqParts = lines[0].Split(' ');
                    if (reqParts.Length < 2) return;

                    string method = reqParts[0].ToUpperInvariant();
                    string rawPath = Uri.UnescapeDataString(reqParts[1]);
                    string clientIp = (client.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? "Console";

                    // Serve Tinfoil / DBI JSON index & HTML list at root
                    if (rawPath == "/" || rawPath.StartsWith("/index.tfl", StringComparison.OrdinalIgnoreCase) || rawPath.StartsWith("/tinfoil", StringComparison.OrdinalIgnoreCase))
                    {
                        string lanIp = GetPrimaryLanIpAddress();
                        var filesArray = _hostedFiles.Select((f, idx) => new
                        {
                            url = $"http://{lanIp}:{ActiveHttpPort}/files/{idx}/{Uri.EscapeDataString(Path.GetFileName(f))}",
                            size = new FileInfo(f).Length
                        }).ToArray();

                        string jsonBody = JsonSerializer.Serialize(new
                        {
                            files = filesArray,
                            success = $"STORM SWITCH BOX {App.AppVersionString} Direct Console Server"
                        }, new JsonSerializerOptions { WriteIndented = true });

                        byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
                        string respHeader =
                            "HTTP/1.1 200 OK\r\n" +
                            "Content-Type: application/json; charset=utf-8\r\n" +
                            $"Content-Length: {bodyBytes.Length}\r\n" +
                            "Connection: close\r\n\r\n";
                        byte[] hdrBytes = Encoding.ASCII.GetBytes(respHeader);
                        await netStream.WriteAsync(hdrBytes, 0, hdrBytes.Length, ct);
                        if (method != "HEAD")
                        {
                            await netStream.WriteAsync(bodyBytes, 0, bodyBytes.Length, ct);
                        }
                        ServerLogAdded?.Invoke($"[{clientIp}] Отдан список файлов Tinfoil/DBI ({_hostedFiles.Count} шт.)");
                        return;
                    }

                    // Match /dl/<idx>.ext or /files/<idx>/<filename>
                    int fileIndex = 0;
                    var mShort = Regex.Match(rawPath, @"^/(?:dl|files)/(\d+)");
                    if (mShort.Success && int.TryParse(mShort.Groups[1].Value, out int parsedIdx))
                    {
                        fileIndex = parsedIdx;
                    }

                    if (fileIndex < 0 || fileIndex >= _hostedFiles.Count)
                    {
                        byte[] notFound = Encoding.ASCII.GetBytes("HTTP/1.1 404 Not Found\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
                        await netStream.WriteAsync(notFound, 0, notFound.Length, ct);
                        return;
                    }

                    string filePath = _hostedFiles[fileIndex];
                    var fi = new FileInfo(filePath);
                    long totalLength = fi.Length;
                    long startOffset = 0;
                    long endOffset = totalLength - 1;
                    bool isRange = false;

                    foreach (var line in lines.Skip(1))
                    {
                        if (line.StartsWith("Range:", StringComparison.OrdinalIgnoreCase))
                        {
                            var rm = Regex.Match(line, @"bytes=(\d+)-(\d*)");
                            if (rm.Success)
                            {
                                isRange = true;
                                startOffset = long.Parse(rm.Groups[1].Value);
                                if (!string.IsNullOrEmpty(rm.Groups[2].Value))
                                {
                                    endOffset = Math.Min(totalLength - 1, long.Parse(rm.Groups[2].Value));
                                }
                            }
                        }
                    }

                    long contentLength = Math.Max(0, endOffset - startOffset + 1);
                    string statusLine = isRange ? "HTTP/1.1 206 Partial Content\r\n" : "HTTP/1.1 200 OK\r\n";
                    string rangeHeader = isRange ? $"Content-Range: bytes {startOffset}-{endOffset}/{totalLength}\r\n" : "";
                    string fileName = Path.GetFileName(filePath);

                    string responseHeaders =
                        statusLine +
                        "Content-Type: application/octet-stream\r\n" +
                        "Accept-Ranges: bytes\r\n" +
                        rangeHeader +
                        $"Content-Length: {contentLength}\r\n" +
                        $"Content-Disposition: attachment; filename=\"{fileName}\"\r\n" +
                        "Connection: close\r\n\r\n";

                    byte[] respHdrBytes = Encoding.ASCII.GetBytes(responseHeaders);
                    await netStream.WriteAsync(respHdrBytes, 0, respHdrBytes.Length, ct);

                    if (method == "HEAD" || contentLength <= 0)
                        return;

                    ServerLogAdded?.Invoke($"[{clientIp}] Скачивание: {fileName} ({ProcessingTask.FormatSize(contentLength)})");

                    const int BufSize = 2 * 1024 * 1024;
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(BufSize);
                    try
                    {
                        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufSize);
                        fs.Seek(startOffset, SeekOrigin.Begin);
                        long remaining = contentLength;
                        long sentTotal = startOffset;

                        while (remaining > 0 && !ct.IsCancellationRequested)
                        {
                            int toRead = (int)Math.Min(BufSize, remaining);
                            int r = await fs.ReadAsync(buffer, 0, toRead, ct);
                            if (r <= 0) break;

                            await netStream.WriteAsync(buffer, 0, r, ct);
                            remaining -= r;
                            sentTotal += r;
                            TransferProgressChanged?.Invoke(fileName, sentTotal, totalLength);
                        }

                        if (remaining == 0)
                        {
                            ServerLogAdded?.Invoke($"[{clientIp}] ✅ Передача завершена: {fileName}");
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }
                catch (Exception ex)
                {
                    if (!ct.IsCancellationRequested)
                    {
                        ServerLogAdded?.Invoke($"[HTTP] Соединение закрыто: {ex.Message}");
                    }
                }
            }
        }

        public static async Task<List<DiscoveredConsoleEndpoint>> DiscoverConsolesInLanAsync(CancellationToken ct)
        {
            var found = new List<DiscoveredConsoleEndpoint>();
            string myIp = GetPrimaryLanIpAddress();
            string[] octets = myIp.Split('.');
            if (octets.Length != 4) return found;

            string prefix = $"{octets[0]}.{octets[1]}.{octets[2]}.";
            using var sem = new SemaphoreSlim(64);
            var tasks = new List<Task>();

            int[] portsToProbe = { 5000, 21 }; // 5000 = Switch ftpd / DBI FTP, 21 = standard FTP

            for (int host = 1; host <= 254; host++)
            {
                string ip = prefix + host;
                if (ip == myIp) continue;

                foreach (int port in portsToProbe)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        await sem.WaitAsync(ct);
                        try
                        {
                            using var tcp = new TcpClient();
                            var connectTask = tcp.ConnectAsync(ip, port);
                            var completed = await Task.WhenAny(connectTask, Task.Delay(320, ct));
                            if (completed == connectTask && tcp.Connected)
                            {
                                string banner = "";
                                try
                                {
                                    tcp.ReceiveTimeout = 300;
                                    using var s = tcp.GetStream();
                                    byte[] buf = new byte[256];
                                    var readTask = s.ReadAsync(buf, 0, buf.Length, ct);
                                    if (await Task.WhenAny(readTask, Task.Delay(250, ct)) == readTask && readTask.Result > 0)
                                    {
                                        banner = Encoding.UTF8.GetString(buf, 0, readTask.Result).Trim();
                                    }
                                }
                                catch { }

                                lock (found)
                                {
                                    found.Add(new DiscoveredConsoleEndpoint
                                    {
                                        IpAddress = ip,
                                        Port = port,
                                        ServiceName = port == 5000 ? "Switch FTP (ftpd / DBI)" : "FTP Server (Switch / 3DS)",
                                        Banner = banner.Split('\r', '\n').FirstOrDefault() ?? ""
                                    });
                                }
                            }
                        }
                        catch { }
                        finally
                        {
                            sem.Release();
                        }
                    }, ct));
                }
            }

            try { await Task.WhenAll(tasks); } catch { }
            return found.OrderBy(x => x.IpAddress).ThenBy(x => x.Port).ToList();
        }

        public async Task UploadFileViaFtpAsync(
            string localFilePath,
            string hostIp,
            int port,
            string remoteDirectory,
            CancellationToken ct)
        {
            if (!File.Exists(localFilePath))
                throw new FileNotFoundException("Файл для отправки не найден.", localFilePath);

            string fileName = Path.GetFileName(localFilePath);
            long totalSize = new FileInfo(localFilePath).Length;

            using var ctrlClient = new TcpClient();
            await ctrlClient.ConnectAsync(hostIp, port, ct);
            using var ctrlStream = ctrlClient.GetStream();
            using var reader = new StreamReader(ctrlStream, Encoding.UTF8);
            using var writer = new StreamWriter(ctrlStream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\r\n" };

            await ReadFtpReplyAsync(reader);
            await SendFtpCommandAsync(writer, reader, "USER anonymous");
            await SendFtpCommandAsync(writer, reader, "PASS storm@switch");
            await SendFtpCommandAsync(writer, reader, "TYPE I");

            if (!string.IsNullOrWhiteSpace(remoteDirectory) && remoteDirectory != "/")
            {
                string cleanDir = remoteDirectory.Replace('\\', '/').TrimEnd('/');
                await SendFtpCommandAsync(writer, reader, $"MKD {cleanDir}", ignoreError: true);
                await SendFtpCommandAsync(writer, reader, $"CWD {cleanDir}", ignoreError: true);
            }

            string pasvReply = await SendFtpCommandAsync(writer, reader, "PASV");
            var m = Regex.Match(pasvReply, @"\((\d+),(\d+),(\d+),(\d+),(\d+),(\d+)\)");
            if (!m.Success)
                throw new InvalidOperationException($"Сервер консоли не вернул адрес PASV: {pasvReply}");

            string dataIp = $"{m.Groups[1].Value}.{m.Groups[2].Value}.{m.Groups[3].Value}.{m.Groups[4].Value}";
            int dataPort = (int.Parse(m.Groups[5].Value) << 8) + int.Parse(m.Groups[6].Value);
            if (dataIp == "0.0.0.0" || dataIp.StartsWith("127.")) dataIp = hostIp;

            using var dataClient = new TcpClient();
            dataClient.SendBufferSize = 2 * 1024 * 1024;
            var dataConnectTask = dataClient.ConnectAsync(dataIp, dataPort, ct).AsTask();

            await writer.WriteLineAsync($"STOR {fileName}");
            string storInit = await ReadFtpReplyAsync(reader);
            if (!storInit.StartsWith("1") && !storInit.StartsWith("2"))
                throw new IOException($"Отказ FTP-сервера при старте записи STOR: {storInit}");

            await dataConnectTask;

            const int ChunkSize = 1024 * 1024;
            byte[] buf = ArrayPool<byte>.Shared.Rent(ChunkSize);
            try
            {
                using var dataStream = dataClient.GetStream();
                using var fs = new FileStream(localFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, ChunkSize);
                long sent = 0;
                int r;
                while ((r = await fs.ReadAsync(buf, 0, ChunkSize, ct)) > 0)
                {
                    await dataStream.WriteAsync(buf, 0, r, ct);
                    sent += r;
                    TransferProgressChanged?.Invoke(fileName, sent, totalSize);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buf);
            }

            dataClient.Close();
            await ReadFtpReplyAsync(reader);
            ServerLogAdded?.Invoke($"[FTP] ✅ Файл {fileName} успешно загружен на {hostIp}:{port}{remoteDirectory}");
        }

        private static async Task<string> SendFtpCommandAsync(StreamWriter writer, StreamReader reader, string cmd, bool ignoreError = false)
        {
            await writer.WriteLineAsync(cmd);
            string reply = await ReadFtpReplyAsync(reader);
            if (!ignoreError && (reply.StartsWith("4") || reply.StartsWith("5")))
                throw new IOException($"Ошибка FTP ({cmd}): {reply}");
            return reply;
        }

        private static async Task<string> ReadFtpReplyAsync(StreamReader reader)
        {
            string? line = await reader.ReadLineAsync();
            if (line == null) return "";
            string lastLine = line;
            if (line.Length >= 4 && line[3] == '-')
            {
                string code = line.Substring(0, 3);
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    lastLine = line;
                    if (line.Length >= 4 && line.StartsWith(code) && line[3] == ' ')
                        break;
                }
            }
            return lastLine;
        }

        #region Native ISO/IEC 18004 QR Code Generator (Byte Mode, ECC Level L, Versions 1..6)

        /// <summary>
        /// Генерирует чёткий PNG QR-код по стандарту ISO/IEC 18004 (Byte Mode, ECC Level L)
        /// для сканирования камерой Nintendo 3DS в FBI (Remote Install -> Scan QR Code).
        /// </summary>
        public static byte[] GenerateQrCodePngBytes(string text, int scale = 8)
        {
            byte[] payload = Encoding.UTF8.GetBytes(text);

            // Параметры версий 1..6 (ECC Level L): (Version, Size, TotalDataBytes, EccBytesPerBlock, NumBlocks)
            (int Ver, int Size, int DataBytes, int EccPerBlock, int Blocks)[] specs =
            {
                (1, 21, 19, 7, 1),
                (2, 25, 34, 10, 1),
                (3, 29, 55, 15, 1),
                (4, 33, 80, 20, 1),
                (5, 37, 108, 26, 1),
                (6, 41, 136, 18, 2)
            };

            var spec = specs.FirstOrDefault(s => payload.Length + 2 <= s.DataBytes);
            if (spec.Ver == 0)
            {
                spec = specs[^1];
                if (payload.Length > spec.DataBytes - 2)
                    Array.Resize(ref payload, spec.DataBytes - 2);
            }

            // 1. Формируем битовый поток данных: Mode=0100 (4 бита) + Length (8 бит) + Payload + Terminator (0000)
            var bits = new List<bool>(spec.DataBytes * 8);
            AddBits(bits, 0b0100, 4);
            AddBits(bits, payload.Length, 8);
            foreach (byte b in payload) AddBits(bits, b, 8);
            for (int i = 0; i < 4 && bits.Count < spec.DataBytes * 8; i++) bits.Add(false);
            while (bits.Count % 8 != 0) bits.Add(false);

            byte[] dataCodewords = new byte[spec.DataBytes];
            for (int i = 0; i < bits.Count / 8; i++)
            {
                int val = 0;
                for (int bit = 0; bit < 8; bit++)
                    val = (val << 1) | (bits[i * 8 + bit] ? 1 : 0);
                dataCodewords[i] = (byte)val;
            }

            byte[] padBytes = { 0xEC, 0x11 };
            for (int i = bits.Count / 8, p = 0; i < spec.DataBytes; i++, p ^= 1)
                dataCodewords[i] = padBytes[p];

            // 2. Вычисляем кодовые слова коррекции ошибок Рида-Соломона в GF(256)
            byte[] finalCodewords;
            if (spec.Blocks == 1)
            {
                byte[] ecc = ComputeReedSolomonEcc(dataCodewords, spec.EccPerBlock);
                finalCodewords = new byte[dataCodewords.Length + ecc.Length];
                Array.Copy(dataCodewords, 0, finalCodewords, 0, dataCodewords.Length);
                Array.Copy(ecc, 0, finalCodewords, dataCodewords.Length, ecc.Length);
            }
            else
            {
                int blockDataLen = spec.DataBytes / 2;
                byte[] b1 = dataCodewords.Take(blockDataLen).ToArray();
                byte[] b2 = dataCodewords.Skip(blockDataLen).Take(blockDataLen).ToArray();
                byte[] e1 = ComputeReedSolomonEcc(b1, spec.EccPerBlock);
                byte[] e2 = ComputeReedSolomonEcc(b2, spec.EccPerBlock);

                var interleaved = new List<byte>(spec.DataBytes + spec.EccPerBlock * 2);
                for (int i = 0; i < blockDataLen; i++) { interleaved.Add(b1[i]); interleaved.Add(b2[i]); }
                for (int i = 0; i < spec.EccPerBlock; i++) { interleaved.Add(e1[i]); interleaved.Add(e2[i]); }
                finalCodewords = interleaved.ToArray();
            }

            // 3. Строим матрицу модулей QR-кода
            int size = spec.Size;
            bool[,] modules = new bool[size, size];
            bool[,] isFunction = new bool[size, size];

            DrawFinderPattern(modules, isFunction, 0, 0, size);
            DrawFinderPattern(modules, isFunction, size - 7, 0, size);
            DrawFinderPattern(modules, isFunction, 0, size - 7, size);

            // Timing patterns
            for (int i = 8; i < size - 8; i++)
            {
                modules[6, i] = (i % 2 == 0); isFunction[6, i] = true;
                modules[i, 6] = (i % 2 == 0); isFunction[i, 6] = true;
            }

            // Alignment pattern for Versions 2..6
            if (spec.Ver >= 2)
            {
                int alignCoord = size - 7;
                for (int dy = -2; dy <= 2; dy++)
                {
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int ax = alignCoord + dx;
                        int ay = alignCoord + dy;
                        modules[ay, ax] = Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1;
                        isFunction[ay, ax] = true;
                    }
                }
            }

            // Dark module & reserve format info areas
            ReserveFormatAreas(modules, isFunction, size);

            // 4. Заполняем зигзагом биты данных и ECC
            int bitIndex = 0;
            int totalBits = finalCodewords.Length * 8;
            for (int right = size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5;
                for (int vert = 0; vert < size; vert++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        int x = right - j;
                        bool upward = ((right + 1) & 2) == 0;
                        int y = upward ? (size - 1 - vert) : vert;
                        if (!isFunction[y, x] && bitIndex < totalBits)
                        {
                            bool bit = ((finalCodewords[bitIndex >> 3] >> (7 - (bitIndex & 7))) & 1) != 0;
                            modules[y, x] = bit;
                            bitIndex++;
                        }
                    }
                }
            }

            // 5. Применяем маску 0: (y + x) % 2 == 0 и записываем Format Information (ECC Level L = 01, Mask = 000 -> 0x77C4)
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (!isFunction[y, x] && ((y + x) % 2 == 0))
                    {
                        modules[y, x] = !modules[y, x];
                    }
                }
            }

            DrawFormatBits(modules, size, 0x77C4); // Level L (01) + Mask 0 (000) BCH-encoded with 0x5412 XOR

            // 6. Рендерим в PNG с тихой зоной (Quiet Zone = 4 модуля)
            int border = 4;
            int imgDim = (size + border * 2) * scale;
            using var bmp = new Bitmap(imgDim, imgDim, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, imgDim, imgDim);
            var bmpData = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                int[] pixels = new int[imgDim * imgDim];
                Array.Fill(pixels, unchecked((int)0xFFFFFFFF)); // Белый фон

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        if (!modules[y, x]) continue;
                        int py0 = (y + border) * scale;
                        int px0 = (x + border) * scale;
                        for (int sy = 0; sy < scale; sy++)
                        {
                            int rowOffset = (py0 + sy) * imgDim + px0;
                            for (int sx = 0; sx < scale; sx++)
                            {
                                pixels[rowOffset + sx] = unchecked((int)0xFF000000);
                            }
                        }
                    }
                }

                Marshal.Copy(pixels, 0, bmpData.Scan0, pixels.Length);
            }
            finally
            {
                bmp.UnlockBits(bmpData);
            }

            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return ms.ToArray();
        }

        private static void AddBits(List<bool> list, int val, int bitCount)
        {
            for (int i = bitCount - 1; i >= 0; i--)
                list.Add(((val >> i) & 1) != 0);
        }

        private static void DrawFinderPattern(bool[,] modules, bool[,] isFunction, int x0, int y0, int size)
        {
            for (int dy = -1; dy <= 7; dy++)
            {
                for (int dx = -1; dx <= 7; dx++)
                {
                    int x = x0 + dx;
                    int y = y0 + dy;
                    if (x < 0 || x >= size || y < 0 || y >= size) continue;
                    bool dark = (dx >= 0 && dx <= 6 && dy >= 0 && dy <= 6) &&
                                (dx == 0 || dx == 6 || dy == 0 || dy == 6 || (dx >= 2 && dx <= 4 && dy >= 2 && dy <= 4));
                    modules[y, x] = dark;
                    isFunction[y, x] = true;
                }
            }
        }

        private static void ReserveFormatAreas(bool[,] modules, bool[,] isFunction, int size)
        {
            for (int i = 0; i <= 8; i++)
            {
                if (i != 6)
                {
                    isFunction[8, i] = true;
                    isFunction[i, 8] = true;
                }
            }
            for (int i = 0; i < 8; i++)
            {
                isFunction[8, size - 1 - i] = true;
                isFunction[size - 1 - i, 8] = true;
            }
            modules[size - 8, 8] = true; // Dark module
            isFunction[size - 8, 8] = true;
        }

        private static void DrawFormatBits(bool[,] modules, int size, int formatBits)
        {
            for (int i = 0; i <= 5; i++) modules[8, i] = GetBit(formatBits, 14 - i);
            modules[8, 7] = GetBit(formatBits, 8);
            modules[8, 8] = GetBit(formatBits, 7);
            modules[7, 8] = GetBit(formatBits, 6);
            for (int i = 9; i < 15; i++) modules[14 - i, 8] = GetBit(formatBits, 14 - i);

            for (int i = 0; i < 8; i++) modules[8, size - 1 - i] = GetBit(formatBits, i);
            for (int i = 8; i < 15; i++) modules[size - 15 + i, 8] = GetBit(formatBits, i);
        }

        private static bool GetBit(int val, int bitIdx) => ((val >> bitIdx) & 1) != 0;

        private static byte[] ComputeReedSolomonEcc(byte[] data, int degree)
        {
            byte[] gen = new byte[degree];
            gen[degree - 1] = 1;
            int root = 1;
            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < degree; j++)
                {
                    gen[j] = GfMultiply(gen[j], (byte)root);
                    if (j + 1 < degree) gen[j] ^= gen[j + 1];
                }
                root = GfMultiply((byte)root, 0x02);
            }

            byte[] result = new byte[degree];
            foreach (byte b in data)
            {
                byte factor = (byte)(b ^ result[0]);
                Array.Copy(result, 1, result, 0, degree - 1);
                result[degree - 1] = 0;
                for (int j = 0; j < degree; j++)
                    result[j] ^= GfMultiply(gen[j], factor);
            }
            return result;
        }

        private static byte GfMultiply(byte x, byte y)
        {
            int z = 0;
            for (int i = 7; i >= 0; i--)
            {
                z = (z << 1) ^ ((z >> 7) * 0x11D);
                z ^= ((y >> i) & 1) * x;
            }
            return (byte)z;
        }

        #endregion

        public void Dispose()
        {
            StopHttpInstallServer();
        }
    }
}
