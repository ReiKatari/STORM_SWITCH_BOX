using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using Microsoft.UI.Xaml.Media.Imaging;
using StormSwitchBox.Models;
using System.Runtime.InteropServices.WindowsRuntime;

namespace StormSwitchBox.Services
{
    public class CatalogScannerService
    {
        private readonly KeysService _keysService;

        public CatalogScannerService(KeysService keysService)
        {
            _keysService = keysService;
        }

        public async Task ScanDirectoryAsync(
            string directoryPath,
            ObservableCollection<CatalogItem> catalog,
            CancellationToken token,
            bool recursive = false,
            Action<CatalogItem>? onItemAdded = null)
        {
            if (!Directory.Exists(directoryPath)) return;

            string[] extensions = { ".nsp", ".nsz", ".xci", ".xcz", ".3ds", ".cci", ".cia", ".cxi", ".cfa", ".app", ".3dsx", ".elf", ".axf", ".artic", ".zcci", ".zcxi", ".zcia", ".z3dsx", ".z3ds", ".bcci", ".bcxi", ".bcia" };
            var enumOpts = new EnumerationOptions
            {
                RecurseSubdirectories = recursive,
                IgnoreInaccessible = true
            };
            var files = Directory.EnumerateFiles(directoryPath, "*.*", enumOpts)
                .Where(f => extensions.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            // Дедупликация - отслеживаем уже добавленные пути
            var processedPaths = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var existing in catalog)
                processedPaths.Add(existing.FilePath);

            var addedInScan = new System.Collections.Generic.List<CatalogItem>();

            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested();

                // Пропуск уже добавленных файлов (дедупликация по пути)
                if (processedPaths.Contains(file)) continue;

                // Проверка целостности - пропускаем недокачанные/временные файлы
                if (!IsFileComplete(file)) continue;

                processedPaths.Add(file);

                var item = new CatalogItem
                {
                    FilePath = file,
                    FileName = System.IO.Path.GetFileName(file),
                    FileSize = Models.ProcessingTask.FormatSize(new FileInfo(file).Length)
                };
                addedInScan.Add(item);

                App.RunOnUI(() =>
                {
                    catalog.Add(item);
                    onItemAdded?.Invoke(item);
                });

                await Task.Run(async () =>
                {
                    try
                    {
                        await ExtractMetadataAsync(item, file, token).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        App.RunOnUI(() =>
                        {
                            item.IsLoading = false;
                            item.HasError = true;
                            item.ErrorMessage = ex.Message;
                        });
                    }
                }, token);
            }

            // Агрегируем отдельные [DLC]-файлы из той же папки в счетчик локальных DLC родительской игры
            AggregateSiblingDlcIds(catalog, addedInScan);
        }

        private static void AggregateSiblingDlcIds(
            ObservableCollection<CatalogItem> catalog,
            System.Collections.Generic.List<CatalogItem> scannedItems)
        {
            try
            {
                var baseItems = catalog
                    .Where(c => c != null && !c.Is3ds && !string.IsNullOrWhiteSpace(c.TitleId) && c.TitleId.Length == 16 && c.TitleId != "0000000000000000")
                    .ToList();
                if (baseItems.Count == 0) return;

                foreach (var src in scannedItems)
                {
                    string rawFnTid = RuTrackerModService.ExtractTitleId(src.FileName ?? string.Empty);
                    if (rawFnTid.Length != 16) continue;
                    string baseTid = RuTrackerModService.NormalizeSwitchBaseApplicationTitleId(rawFnTid);
                    if (!RuTrackerModService.IsDlcOfBaseTitle(baseTid, rawFnTid)) continue;

                    var parent = baseItems.FirstOrDefault(p =>
                        string.Equals(RuTrackerModService.NormalizeSwitchBaseApplicationTitleId(p.TitleId), baseTid, StringComparison.OrdinalIgnoreCase) &&
                        !ReferenceEquals(p, src));
                    if (parent != null)
                    {
                        App.RunOnUI(() =>
                        {
                            if (parent.LocalDlcIds.Add(rawFnTid.ToUpperInvariant()))
                            {
                                if (parent.LocalDlcIds.Count > parent.LocalDlcCount)
                                    parent.LocalDlcCount = parent.LocalDlcIds.Count;
                            }
                        });
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Проверяет, что файл полностью скачан и готов к обработке.
        /// </summary>
        private static bool IsFileComplete(string filePath)
        {
            try
            {
                var fi = new FileInfo(filePath);
                
                // Пропуск файлов нулевого размера
                if (fi.Length == 0) return false;

                // Пропуск временных/недокачанных файлов
                string ext = fi.Extension.ToLowerInvariant();
                string[] incompleteExtensions = { ".part", ".crdownload", ".tmp", ".downloading", ".partial", ".!ut", ".bc!" };
                if (incompleteExtensions.Contains(ext)) return false;

                // Проверка наличия рядом файла-спутника недокачки (.part и т.д.)
                string dir = fi.DirectoryName ?? "";
                string nameNoExt = System.IO.Path.GetFileNameWithoutExtension(filePath);
                string fullName = fi.Name;
                foreach (var marker in incompleteExtensions)
                {
                    if (File.Exists(System.IO.Path.Combine(dir, fullName + marker))) return false;
                    if (File.Exists(System.IO.Path.Combine(dir, nameNoExt + marker))) return false;
                }

                // Проверка блокировки файла (если файл занят - вероятно ещё качается)
                using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    // Файл доступен для чтения
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task ScanSingleFileAsync(string filePath, ObservableCollection<CatalogItem> catalog, CancellationToken token, Action<CatalogItem>? onItemAdded = null)
        {
            if (!File.Exists(filePath)) return;

            // Check if it already exists in the catalog to prevent duplicates
            if (catalog.Any(c => c.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase))) return;

            string[] extensions = { ".nsp", ".nsz", ".xci", ".xcz", ".3ds", ".cci", ".cia", ".cxi", ".cfa", ".app", ".3dsx", ".elf", ".axf", ".artic", ".zcci", ".zcxi", ".zcia", ".z3dsx", ".z3ds", ".bcci", ".bcxi", ".bcia" };
            if (!extensions.Contains(System.IO.Path.GetExtension(filePath).ToLowerInvariant())) return;

            var item = new CatalogItem
            {
                FilePath = filePath,
                FileName = System.IO.Path.GetFileName(filePath),
                FileSize = Models.ProcessingTask.FormatSize(new FileInfo(filePath).Length)
            };

            App.RunOnUI(() =>
            {
                catalog.Add(item);
                onItemAdded?.Invoke(item);
            });

            await Task.Run(async () =>
            {
                try
                {
                    await ExtractMetadataAsync(item, filePath, token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    App.RunOnUI(() => 
                    {
                        item.IsLoading = false;
                        item.HasError = true;
                        item.ErrorMessage = ex.Message;
                    });
                }
            }, token);
        }

        private async Task ExtractMetadataAsync(CatalogItem item, string filePath, CancellationToken token)
        {
            string ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
            if (Nintendo3dsService.Is3dsExtension(ext))
            {
                var info3ds = App.Nintendo3ds.Parse3dsFile(filePath);
                var tcs3ds = new TaskCompletionSource<bool>();
                App.RunOnUI(() =>
                {
                    try
                    {
                        item.Is3ds = true;
                        item.Platform = "3DS";
                        item.TitleId = !string.IsNullOrEmpty(info3ds?.TitleId) ? info3ds.TitleId : "3DS";
                        item.InternalTitleRussian = info3ds?.GameNameRussian ?? string.Empty;
                        item.InternalTitleEnglish = info3ds?.GameNameEnglish ?? string.Empty;
                        item.InternalTitleFallback = info3ds?.GameName ?? string.Empty;
                        bool has3dsTitle = !string.IsNullOrWhiteSpace(item.InternalTitleRussian) ||
                                           !string.IsNullOrWhiteSpace(item.InternalTitleEnglish) ||
                                           !string.IsNullOrWhiteSpace(item.InternalTitleFallback);
                        item.HasEmbeddedInternalTitle = has3dsTitle;
                        item.TitleName = has3dsTitle
                            ? item.ResolvePreferredInternalTitle(App.Localization.CurrentLanguage)
                            : System.IO.Path.GetFileNameWithoutExtension(filePath);
                        item.Version = !string.IsNullOrEmpty(info3ds?.Version) ? info3ds.Version : "v0";
                        item.Publisher = !string.IsNullOrEmpty(info3ds?.Publisher) ? info3ds.Publisher : "Nintendo";
                        item.Category = "Nintendo 3DS";
                        if (info3ds?.IconBytes != null && info3ds.IconBytes.Length > 0)
                        {
                            item.CoverBytes = info3ds.IconBytes;
                            try
                            {
                                var bmp = new BitmapImage { DecodePixelWidth = 256, DecodePixelType = DecodePixelType.Logical };
                                using var ms = new MemoryStream(info3ds.IconBytes);
                                bmp.SetSource(ms.AsRandomAccessStream());
                                item.CoverImage = bmp;
                            }
                            catch { }
                        }
                        item.IsLoading = false;
                    }
                    finally
                    {
                        tcs3ds.TrySetResult(true);
                    }
                });
                if (App.MainDispatcher != null) await tcs3ds.Task.ConfigureAwait(false);
                return;
            }

            using var container = SwitchContainerHandle.Open(filePath);
            IFileSystem fileSystem = container.FileSystem;

            var entries = fileSystem.EnumerateEntries().ToList();
            var titleKeyMap = new System.Collections.Generic.Dictionary<string, byte[]>();
            var localContainedTids = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var localExplicitAocTids = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in entries)
            {
                if (entry.Name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase))
                {
                    if (entry.Name.Length >= 16)
                    {
                        string tikTid = entry.Name.Substring(0, 16);
                        if (tikTid.All(Uri.IsHexDigit))
                            localContainedTids.Add(tikTid.ToUpperInvariant());
                    }
                    try
                    {
                        using var tikFileRefOut = new LibHac.Common.UniqueRef<IFile>();
                        using var tikPath = new LibHac.Fs.Path();
                        tikPath.Initialize(new LibHac.Common.U8Span(System.Text.Encoding.UTF8.GetBytes(entry.FullPath))).ThrowIfFailure();
                        if (fileSystem.OpenFile(ref tikFileRefOut.Ref, in tikPath, LibHac.Fs.OpenMode.Read).IsSuccess())
                        {
                            using var tikFile = tikFileRefOut.Release();
                            IStorage tikStorage = tikFile.AsStorage();
                            tikStorage.GetSize(out long tikSize).ThrowIfFailure();
                            byte[] tikData = new byte[tikSize];
                            tikStorage.Read(0, tikData).ThrowIfFailure();
                            var ticketInfo = TicketHarvesterService.ExtractDecryptedTicket(tikData, (int)tikSize, _keysService.CurrentKeyset);
                            if (ticketInfo.HasValue && !string.IsNullOrEmpty(ticketInfo.Value.RightsId) && ticketInfo.Value.TitleKey != null && ticketInfo.Value.TitleKey.Length == 16)
                            {
                                string rightsIdStr = ticketInfo.Value.RightsId;
                                byte[] tKey = ticketInfo.Value.TitleKey;
                                titleKeyMap[rightsIdStr] = tKey;
                                lock (Core.NSZ.StormNczCompressor.TitleKeysCache)
                                {
                                    Core.NSZ.StormNczCompressor.TitleKeysCache[rightsIdStr] = tKey;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Log($"[CatalogScanner] [WARNING] Error reading key from {entry.Name}: {ex.Message}", LogLevel.Warning);
                    }
                }
            }

            IStorage? globalSolidStorage = null;
            IFile? solidFile = null;
            var solidEntry = entries.FirstOrDefault(e => e.Name.EndsWith(".solid", StringComparison.OrdinalIgnoreCase));
            if (solidEntry != null)
            {
                try
                {
                    using var solidFileRefOut = new LibHac.Common.UniqueRef<IFile>();
                    using var solidPath = new LibHac.Fs.Path();
                    solidPath.Initialize(new LibHac.Common.U8Span(System.Text.Encoding.UTF8.GetBytes(solidEntry.FullPath))).ThrowIfFailure();
                    if (fileSystem.OpenFile(ref solidFileRefOut.Ref, in solidPath, LibHac.Fs.OpenMode.Read).IsSuccess())
                    {
                        solidFile = solidFileRefOut.Release();
                        globalSolidStorage = solidFile.AsStorage();
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.Log($"[CatalogScanner] [WARNING] Error opening solid storage: {ex.Message}", LogLevel.Warning);
                }
            }

            // Локальные переменные вместо гонки потоков с App.RunOnUI
            string localTitleId = item.TitleId;
            string localTitleName = item.TitleName;
            string localPublisher = item.Publisher;
            string localDispVer = item.Version;
            string localVersionCode = item.VersionCode;
            string localSupportedLangs = item.SupportedLanguages;
            string localRatingAge = item.RatingAge;
            string localVideoCapture = item.VideoCapture;
            string localSaveDataSize = item.SaveDataSize;
            string localRegions = item.Regions;
            string baseRuTitle = string.Empty, baseEnTitle = string.Empty, baseAnyTitle = string.Empty;
            string baseRuPub = string.Empty, baseEnPub = string.Empty, baseAnyPub = string.Empty;
            string updRuTitle = string.Empty, updEnTitle = string.Empty, updAnyTitle = string.Empty;
            string updRuPub = string.Empty, updEnPub = string.Empty, updAnyPub = string.Empty;
            byte[]? localCoverBytes = null;
            var localScreenshots = new System.Collections.Generic.List<byte[]>();
            bool hasControlMeta = false;
            bool metaError = false;
            string fileNameOnly = System.IO.Path.GetFileName(filePath);

            try
            {
                foreach (var entry in entries)
                {
                    token.ThrowIfCancellationRequested();
                    string entryName = entry.Name;

                    bool isNca = entryName.EndsWith(".nca", StringComparison.OrdinalIgnoreCase);
                    bool isNcz = entryName.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase);

                    if (isNca || isNcz)
                    {
                        using var fileRefOut = new LibHac.Common.UniqueRef<IFile>();
                        using var entryPath = new LibHac.Fs.Path();
                        entryPath.Initialize(new LibHac.Common.U8Span(System.Text.Encoding.UTF8.GetBytes(entry.FullPath))).ThrowIfFailure();
                        fileSystem.OpenFile(ref fileRefOut.Ref, in entryPath, LibHac.Fs.OpenMode.Read).ThrowIfFailure();
                        using IFile fileRef = fileRefOut.Release();
                        IStorage entryStorage = fileRef.AsStorage();
                        IDisposable? toDispose = null;

                        Nca nca;
                        try
                        {
                            // Быстрая проверка заголовка NCA (первые 0x4000 байт не сжаты даже в NCZ)
                            nca = new Nca(_keysService.CurrentKeyset, entryStorage);
                            KeysService.RegisterRightsIdFallback(_keysService.CurrentKeyset, nca);
                        }
                        catch
                        {
                            continue;
                        }

                        if (nca.Header.TitleId != 0)
                        {
                            localContainedTids.Add(nca.Header.TitleId.ToString("X16"));
                        }

                        // Оптимизация: сканеру каталога нужны ТОЛЬКО Control, Manual и Meta.
                        if (nca.Header.ContentType != NcaContentType.Control && 
                            nca.Header.ContentType != NcaContentType.Manual && 
                            nca.Header.ContentType != NcaContentType.Meta)
                        {
                            continue;
                        }

                        if (isNcz)
                        {
                            try
                            {
                                var nczStorage = new Core.NSZ.StormNczStorage(entryStorage, titleKeyMap, globalSolidStorage, App.Keys.CurrentKeyset);
                                entryStorage = nczStorage;
                                toDispose = nczStorage;
                                nca = new Nca(_keysService.CurrentKeyset, entryStorage);
                                KeysService.RegisterRightsIdFallback(_keysService.CurrentKeyset, nca);
                            }
                            catch (Exception ex)
                            {
                                App.Logger.Log($"[CatalogScanner] [INFO] Пропущен NCZ {entryName}: {ex.Message}", LogLevel.Info);
                                continue;
                            }
                        }

                        try
                        {
                            if (nca.Header.ContentType == NcaContentType.Control)
                            {
                                var romfs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);
                                try
                                {
                                    // Parse NACP
                                    using var nacpFileRef = new LibHac.Common.UniqueRef<IFile>();
                                    using var nacpPath = new LibHac.Fs.Path();
                                    nacpPath.Initialize(new LibHac.Common.U8Span(System.Text.Encoding.UTF8.GetBytes("/control.nacp"))).ThrowIfFailure();

                                    if (romfs.OpenFile(ref nacpFileRef.Ref, in nacpPath, LibHac.Fs.OpenMode.Read).IsSuccess())
                                    {
                                        using var nacpFile = nacpFileRef.Release();
                                        using var nacpStream = nacpFile.AsStream();
                                        byte[] nacpBytes = new byte[0x4000];
                                        int bytesRead = ReadStreamFull(nacpStream, nacpBytes);
                                        var nacp = new LibHac.Ns.ApplicationControlProperty();
                                        var nacpSpan = System.Runtime.InteropServices.MemoryMarshal.AsBytes(new Span<LibHac.Ns.ApplicationControlProperty>(ref nacp));
                                        nacpBytes.AsSpan(0, Math.Min(bytesRead, nacpSpan.Length)).CopyTo(nacpSpan);

                                        string ruTitle = CleanNacpString(nacp.Title[11].NameString.ToString());
                                        string enTitle = CleanNacpString(nacp.Title[0].NameString.ToString());
                                        if (string.IsNullOrEmpty(enTitle))
                                            enTitle = CleanNacpString(nacp.Title[1].NameString.ToString());

                                        string anyTitle = string.Empty;
                                        int[] slotOrder = { 11, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 13, 14, 15 };
                                        foreach (int slotIdx in slotOrder)
                                        {
                                            string candidate = CleanNacpString(nacp.Title[slotIdx].NameString.ToString());
                                            if (!string.IsNullOrEmpty(candidate)) { anyTitle = candidate; break; }
                                        }

                                        string ruPub = CleanNacpString(nacp.Title[11].PublisherString.ToString());
                                        string enPub = CleanNacpString(nacp.Title[0].PublisherString.ToString());
                                        if (string.IsNullOrEmpty(enPub))
                                            enPub = CleanNacpString(nacp.Title[1].PublisherString.ToString());

                                        string anyPub = string.Empty;
                                        foreach (int slotIdx in slotOrder)
                                        {
                                            string candidate = CleanNacpString(nacp.Title[slotIdx].PublisherString.ToString());
                                            if (!string.IsNullOrEmpty(candidate)) { anyPub = candidate; break; }
                                        }

                                        string title = !string.IsNullOrEmpty(ruTitle) ? ruTitle : (!string.IsNullOrEmpty(enTitle) ? enTitle : anyTitle);
                                        string pub = !string.IsNullOrEmpty(ruPub) ? ruPub : (!string.IsNullOrEmpty(enPub) ? enPub : anyPub);

                                        string rawTitleIdHex = nca.Header.TitleId.ToString("X16");
                                        bool isCurrentUpdate = rawTitleIdHex.EndsWith("800", StringComparison.OrdinalIgnoreCase);
                                        string normalizedTitleId = TitleIdHelper.NormalizeBaseTitleIdHex(rawTitleIdHex);

                                        if (isCurrentUpdate)
                                        {
                                            if (!string.IsNullOrEmpty(ruTitle)) updRuTitle = ruTitle;
                                            if (!string.IsNullOrEmpty(enTitle)) updEnTitle = enTitle;
                                            if (!string.IsNullOrEmpty(anyTitle)) updAnyTitle = anyTitle;
                                            if (!string.IsNullOrEmpty(ruPub)) updRuPub = ruPub;
                                            if (!string.IsNullOrEmpty(enPub)) updEnPub = enPub;
                                            if (!string.IsNullOrEmpty(anyPub)) updAnyPub = anyPub;
                                        }
                                        else
                                        {
                                            if (!string.IsNullOrEmpty(ruTitle)) baseRuTitle = ruTitle;
                                            if (!string.IsNullOrEmpty(enTitle)) baseEnTitle = enTitle;
                                            if (!string.IsNullOrEmpty(anyTitle)) baseAnyTitle = anyTitle;
                                            if (!string.IsNullOrEmpty(ruPub)) baseRuPub = ruPub;
                                            if (!string.IsNullOrEmpty(enPub)) baseEnPub = enPub;
                                            if (!string.IsNullOrEmpty(anyPub)) baseAnyPub = anyPub;
                                        }

                                        string supportedLangs = "";
                                        try
                                        {
                                            var langs = new System.Collections.Generic.List<string>();
                                            uint langBits = (uint)nacp.SupportedLanguageFlag;
                                            var langNames = new[] { "AmericanEnglish", "BritishEnglish", "Japanese", "French", "German", "LatinAmericanSpanish", "Spanish", "Italian", "Dutch", "CanadianFrench", "Portuguese", "Russian", "Korean", "TraditionalChinese", "SimplifiedChinese", "BrazilianPortuguese" };
                                            for (int i = 0; i < langNames.Length; i++)
                                            {
                                                if ((langBits & (1u << i)) != 0) langs.Add(langNames[i].Replace("American", "").Replace("British", "").Replace("LatinAmerican", ""));
                                            }
                                            supportedLangs = string.Join(", ", langs.Distinct());
                                        }
                                        catch { }

                                        string rating = "Нет данных";
                                        try
                                        {
                                            if (nacp.RatingAge[2] >= 0 && nacp.RatingAge[2] < 31)
                                                rating = $"PEGI {nacp.RatingAge[2]}+";
                                            else if (nacp.RatingAge[1] >= 0 && nacp.RatingAge[1] < 31)
                                                rating = $"ESRB {nacp.RatingAge[1]}+";
                                        }
                                        catch { }

                                        string videoCap = "Неизвестно";
                                        try { videoCap = ((int)nacp.VideoCapture == 2) ? "Да" : "Нет"; } catch { }

                                        string saveSize = "Неизвестно";
                                        try { saveSize = Models.ProcessingTask.FormatSize(nacp.UserAccountSaveDataSize); } catch { }

                                        if (!isCurrentUpdate && localDispVer != "v0" && localDispVer != "0" && !IsBaseVersion(localDispVer))
                                        {
                                            if (localTitleId == "0000000000000000") localTitleId = normalizedTitleId;
                                            if (localTitleName == "Unknown Game" && !string.IsNullOrWhiteSpace(title)) localTitleName = title;
                                        }
                                        else
                                        {
                                            localTitleName = string.IsNullOrWhiteSpace(title) ? "Unknown" : title;
                                            localPublisher = string.IsNullOrWhiteSpace(pub) ? "Unknown" : pub;
                                            string nacpVer = nacp.DisplayVersionString.ToString();
                                            if (!IsBaseVersion(nacpVer) || IsBaseVersion(localDispVer))
                                            {
                                                localDispVer = CleanVersion(nacpVer);
                                            }
                                            localTitleId = normalizedTitleId;

                                            localSupportedLangs = string.IsNullOrEmpty(supportedLangs) ? "Неизвестно" : supportedLangs;
                                            localRatingAge = rating;
                                            localVideoCapture = videoCap;
                                            localSaveDataSize = saveSize;

                                            string fileRegion = "";
                                            if (!string.IsNullOrEmpty(supportedLangs))
                                            {
                                                bool hasUs = supportedLangs.Contains("English");
                                                bool hasEu = supportedLangs.Contains("French") || supportedLangs.Contains("German") || supportedLangs.Contains("Italian") || supportedLangs.Contains("Spanish") || supportedLangs.Contains("Dutch");
                                                bool hasJp = supportedLangs.Contains("Japanese");
                                                bool hasKr = supportedLangs.Contains("Korean");
                                                bool hasCn = supportedLangs.Contains("Chinese");

                                                if (hasUs && hasEu && hasJp) fileRegion = "WW";
                                                else if (hasUs && hasEu) fileRegion = "US/EU";
                                                else if (hasUs && !hasEu && !hasJp) fileRegion = "US";
                                                else if (!hasUs && hasEu) fileRegion = "EU";
                                                else if (hasJp && !hasUs && !hasEu) fileRegion = "JP";
                                                else if (hasKr && !hasUs && !hasEu && !hasJp) fileRegion = "KR";
                                                else if (hasCn && !hasUs && !hasEu && !hasJp) fileRegion = "AS";
                                            }

                                            localRegions = string.IsNullOrEmpty(fileRegion) ? "UNKNOWN" : fileRegion;
                                            hasControlMeta = true;
                                        }
                                    }

                                    // Try to get icon (только если ещё не извлекли локальную иконку)
                                    if (localCoverBytes == null)
                                    {
                                        bool preferRuIcon = App.Localization.CurrentLanguage.StartsWith("ru", StringComparison.OrdinalIgnoreCase);
                                        var iconLangs = preferRuIcon
                                            ? new[] { "Russian", "AmericanEnglish", "BritishEnglish", "Japanese", "French", "German", "LatinAmericanSpanish", "Spanish", "Italian", "Dutch", "CanadianFrench", "Portuguese", "Korean", "TraditionalChinese", "SimplifiedChinese" }
                                            : new[] { "AmericanEnglish", "BritishEnglish", "Russian", "Japanese", "French", "German", "LatinAmericanSpanish", "Spanish", "Italian", "Dutch", "CanadianFrench", "Portuguese", "Korean", "TraditionalChinese", "SimplifiedChinese" };

                                        foreach (var lang in iconLangs)
                                        {
                                            using var iconFileRef = new LibHac.Common.UniqueRef<IFile>();
                                            using var iconPath = new LibHac.Fs.Path();
                                            iconPath.Initialize(new LibHac.Common.U8Span(System.Text.Encoding.UTF8.GetBytes($"/icon_{lang}.dat"))).ThrowIfFailure();

                                            if (romfs.OpenFile(ref iconFileRef.Ref, in iconPath, LibHac.Fs.OpenMode.Read).IsSuccess())
                                            {
                                                using var iconFile = iconFileRef.Release();
                                                using var memStream = new MemoryStream();
                                                iconFile.AsStream().CopyTo(memStream);
                                                localCoverBytes = memStream.ToArray();
                                                break;
                                            }
                                        }
                                    }
                                }
                                finally
                                {
                                    (romfs as IDisposable)?.Dispose();
                                }
                            }
                            else if (nca.Header.ContentType == NcaContentType.Manual)
                            {
                                try
                                {
                                    var romfs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);
                                    try
                                    {
                                        foreach (var imgEntry in romfs.EnumerateEntries("/", "*.jpg").Concat(romfs.EnumerateEntries("/", "*.png")))
                                        {
                                            if (localScreenshots.Count >= 5) break;

                                            using var imgFileRef = new LibHac.Common.UniqueRef<IFile>();
                                            using var imgPath = new LibHac.Fs.Path();
                                            imgPath.Initialize(new LibHac.Common.U8Span(System.Text.Encoding.UTF8.GetBytes(imgEntry.FullPath))).ThrowIfFailure();

                                            if (romfs.OpenFile(ref imgFileRef.Ref, in imgPath, LibHac.Fs.OpenMode.Read).IsSuccess())
                                            {
                                                using var imgFile = imgFileRef.Release();
                                                using var memStream = new MemoryStream();
                                                imgFile.AsStream().CopyTo(memStream);
                                                localScreenshots.Add(memStream.ToArray());
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        (romfs as IDisposable)?.Dispose();
                                    }
                                }
                                catch { }
                            }
                            else if (nca.Header.ContentType == NcaContentType.Meta)
                            {
                                try
                                {
                                    var fs = nca.OpenFileSystem(0, IntegrityCheckLevel.ErrorOnInvalid);
                                    try
                                    {
                                        foreach (var cnmtEntry in fs.EnumerateEntries())
                                        {
                                            if (!cnmtEntry.Name.EndsWith(".cnmt", StringComparison.OrdinalIgnoreCase)) continue;

                                            using var cnmtFileRef = new LibHac.Common.UniqueRef<IFile>();
                                            using var cnmtPath = new LibHac.Fs.Path();
                                            cnmtPath.Initialize(new LibHac.Common.U8Span(System.Text.Encoding.UTF8.GetBytes(cnmtEntry.FullPath))).ThrowIfFailure();
                                            fs.OpenFile(ref cnmtFileRef.Ref, in cnmtPath, LibHac.Fs.OpenMode.Read).ThrowIfFailure();

                                            using var cnmtFile = cnmtFileRef.Release();
                                            using var cnmtStream = new MemoryStream();
                                            cnmtFile.AsStream().CopyTo(cnmtStream);
                                            byte[] cnmtBytes = cnmtStream.ToArray();
                                            uint version = 0;
                                            ulong cnmtTid = 0;
                                            byte cnmtType = 0;
                                            try
                                            {
                                                if (cnmtBytes.Length >= 13)
                                                {
                                                    cnmtTid = BitConverter.ToUInt64(cnmtBytes, 0x00);
                                                    version = BitConverter.ToUInt32(cnmtBytes, 0x08);
                                                    cnmtType = cnmtBytes[0x0C];
                                                }
                                            }
                                            catch { }

                                            if (cnmtTid != 0)
                                            {
                                                string cnmtTidHex = cnmtTid.ToString("X16");
                                                localContainedTids.Add(cnmtTidHex);
                                                if (cnmtType == 0x82)
                                                    localExplicitAocTids.Add(cnmtTidHex);
                                            }

                                            // Для версии игры учитываем только Application (0x80) и Patch (0x81), а не номер версии отдельного DLC (0x82)
                                            if (cnmtType != 0x82)
                                            {
                                                uint fixedVersion = FixHexPackedVersionCode(version);
                                                uint currentVer = 0;
                                                uint.TryParse(localVersionCode, out currentVer);
                                                currentVer = FixHexPackedVersionCode(currentVer);

                                                var match = System.Text.RegularExpressions.Regex.Match(fileNameOnly, @"\[v(\d+)\]");
                                                if (match.Success && uint.TryParse(match.Groups[1].Value, out uint nameVer))
                                                {
                                                    nameVer = FixHexPackedVersionCode(nameVer);
                                                    if (nameVer > currentVer) currentVer = nameVer;
                                                }

                                                if (fixedVersion > currentVer)
                                                    localVersionCode = fixedVersion.ToString();
                                                else if (currentVer > 0)
                                                    localVersionCode = currentVer.ToString();
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        (fs as IDisposable)?.Dispose();
                                    }
                                }
                                catch
                                {
                                    metaError = true;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message.Contains("Unable to decrypt NCA header", StringComparison.OrdinalIgnoreCase))
                            {
                                App.Logger.Log($"[CatalogScanner] Заголовок {entryName} зашифрован (пропущен)", LogLevel.Warning);
                            }
                            else
                            {
                                App.Logger.Log($"[CatalogScanner] Ошибка парсинга {entryName}: {ex.Message}", LogLevel.Warning);
                            }
                        }
                        finally
                        {
                            if (toDispose != null)
                            {
                                try { toDispose.Dispose(); } catch { }
                            }
                        }
                    }
                }
            }
            finally
            {
                if (solidFile != null)
                {
                    try { solidFile.Dispose(); } catch { }
                }
            }

            if (metaError && localVersionCode == "0")
                localVersionCode = "ERR_META";

            bool hasAuthoritativeNacpVer = hasControlMeta && !IsBaseVersion(localDispVer);
            // Вычисляем итоговую версию в локальных переменных ДО единственного вызова App.RunOnUI
            ResolveVersionFromPath(filePath, ref localDispVer, ref localVersionCode);

            var matchedDlcIds = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(localTitleId) && localTitleId != "0000000000000000")
            {
                foreach (var tid in localContainedTids)
                {
                    if (RuTrackerModService.IsDlcOfBaseTitle(localTitleId, tid))
                        matchedDlcIds.Add(tid.ToUpperInvariant());
                }
                foreach (var aocTid in localExplicitAocTids)
                {
                    if (RuTrackerModService.IsDlcOfBaseTitle(localTitleId, aocTid))
                        matchedDlcIds.Add(aocTid.ToUpperInvariant());
                }
            }
            int localDlcCount = matchedDlcIds.Count;
            var fnDlcMatch = System.Text.RegularExpressions.Regex.Match(fileNameOnly, @"\+(\d+)D(?=[)\+\s]|$)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (fnDlcMatch.Success && int.TryParse(fnDlcMatch.Groups[1].Value, out int fnDlc) && fnDlc > localDlcCount)
                localDlcCount = fnDlc;

            string finalRuTitle = (!string.IsNullOrEmpty(updRuTitle) && ContainsCyrillic(updRuTitle)) ? updRuTitle
                                : (!string.IsNullOrEmpty(baseRuTitle) && ContainsCyrillic(baseRuTitle)) ? baseRuTitle
                                : (!string.IsNullOrEmpty(updRuTitle) ? updRuTitle : baseRuTitle);
            string finalEnTitle = !string.IsNullOrEmpty(updEnTitle) ? updEnTitle : baseEnTitle;
            string finalAnyTitle = !string.IsNullOrEmpty(updAnyTitle) ? updAnyTitle : baseAnyTitle;
            bool hasEmbeddedTitle = !string.IsNullOrEmpty(finalRuTitle) || !string.IsNullOrEmpty(finalEnTitle) || !string.IsNullOrEmpty(finalAnyTitle);
            bool preferRu = App.Localization.CurrentLanguage.StartsWith("ru", StringComparison.OrdinalIgnoreCase);

            if (hasEmbeddedTitle)
            {
                localTitleName = preferRu
                    ? (!string.IsNullOrEmpty(finalRuTitle) ? finalRuTitle : (!string.IsNullOrEmpty(finalEnTitle) ? finalEnTitle : finalAnyTitle))
                    : (!string.IsNullOrEmpty(finalEnTitle) ? finalEnTitle : (!string.IsNullOrEmpty(finalRuTitle) ? finalRuTitle : finalAnyTitle));
            }

            string resolvedPublisher = preferRu
                ? FirstNonEmpty(updRuPub, baseRuPub, updEnPub, baseEnPub, updAnyPub, baseAnyPub)
                : FirstNonEmpty(updEnPub, baseEnPub, updRuPub, baseRuPub, updAnyPub, baseAnyPub);
            if (!string.IsNullOrEmpty(resolvedPublisher))
            {
                localPublisher = resolvedPublisher;
            }

            if (hasControlMeta && !string.IsNullOrWhiteSpace(localTitleId) &&
                localTitleId != "0000000000000000" && !IsBaseVersion(localDispVer))
            {
                App.TitleDb?.RecordObservedDisplayVersion(localTitleId, localVersionCode, localDispVer, isAuthoritative: hasAuthoritativeNacpVer);
            }

            var uiApplyTcs = new TaskCompletionSource<bool>();
            App.RunOnUI(async () =>
            {
                try
                {
                    item.TitleId = localTitleId;
                    item.InternalTitleRussian = finalRuTitle;
                    item.InternalTitleEnglish = finalEnTitle;
                    item.InternalTitleFallback = finalAnyTitle;
                    item.HasEmbeddedInternalTitle = hasEmbeddedTitle;
                    item.TitleName = localTitleName;
                    item.Publisher = localPublisher;
                    item.Version = localDispVer;
                    item.VersionCode = localVersionCode;
                    item.SupportedLanguages = localSupportedLangs;
                    item.RatingAge = localRatingAge;
                    item.VideoCapture = localVideoCapture;
                    item.SaveDataSize = localSaveDataSize;
                    item.Regions = localRegions;
                    item.LocalDlcIds = matchedDlcIds;
                    item.LocalDlcCount = localDlcCount;

                    if (localCoverBytes != null && localCoverBytes.Length > 0)
                    {
                        item.CoverBytes = localCoverBytes;
                        try
                        {
                            var bmp = new BitmapImage { DecodePixelWidth = 256, DecodePixelType = DecodePixelType.Logical };
                            using var ms = new MemoryStream(localCoverBytes);
                            await bmp.SetSourceAsync(ms.AsRandomAccessStream());
                            item.CoverImage = bmp;
                        }
                        catch { }
                    }

                    if (localScreenshots.Count > 0)
                    {
                        foreach (var shotBytes in localScreenshots)
                        {
                            try
                            {
                                var bmp = new BitmapImage { DecodePixelWidth = 256, DecodePixelType = DecodePixelType.Logical };
                                using var ms = new MemoryStream(shotBytes);
                                await bmp.SetSourceAsync(ms.AsRandomAccessStream());
                                item.Screenshots.Add(bmp);
                                item.HasScreenshots = true;
                            }
                            catch { }
                        }
                    }

                    if (hasControlMeta)
                        item.IsLoading = false;
                }
                finally
                {
                    uiApplyTcs.TrySetResult(true);
                }
            });

            if (App.MainDispatcher != null)
                await uiApplyTcs.Task.ConfigureAwait(false);

            // Обогащаем данными из онлайн базы TitleDB в фоне и завершаем загрузку
            App.TitleDb?.EnrichCatalogItem(item);
            App.RunOnUI(() =>
            {
                item.IsLoading = false;
            });
        }

        private static int ReadStreamFull(Stream stream, byte[] buffer)
        {
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int n = stream.Read(buffer, totalRead, buffer.Length - totalRead);
                if (n <= 0) break;
                totalRead += n;
            }
            return totalRead;
        }

        private static string CleanNacpString(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            string trimmed = raw.Trim('\0', ' ', '\r', '\n', '\t');
            if (string.IsNullOrWhiteSpace(trimmed) || HasGarbageCharacters(trimmed))
                return string.Empty;
            return trimmed;
        }

        private static bool ContainsCyrillic(string? s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
            {
                if ((c >= 'А' && c <= 'я') || c == 'Ё' || c == 'ё')
                    return true;
            }
            return false;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var v in values)
            {
                if (!string.IsNullOrWhiteSpace(v)) return v;
            }
            return string.Empty;
        }

        private static bool HasGarbageCharacters(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            foreach (char c in s)
            {
                if (c == '\uFFFD' || c == '✦' || char.IsControl(c))
                    return true;
            }
            return false;
        }

        #region Version Resolution (Ported from E:\STORM EDEN 3)

        private static readonly System.Text.RegularExpressions.Regex FnPairVerRegex =
            new System.Text.RegularExpressions.Regex(@"\(([0-9]+\.[0-9]+(?:\.[0-9]+)*)\s*-\s*([0-9]+)", System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex FnVerRegex =
            new System.Text.RegularExpressions.Regex(@"(?:[\(\[\s_]v?|\b)([0-9]+\.[0-9]+(?:\.[0-9]+)*)(?!\s*(?:GB|MB|KB|TB|ГБ|МБ|КБ|Б|B)\b)", System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex FnVnumRegex =
            new System.Text.RegularExpressions.Regex(@"\[v([0-9]+)\]", System.Text.RegularExpressions.RegexOptions.Compiled);

        private static readonly System.Text.RegularExpressions.Regex VerRegex =
            new System.Text.RegularExpressions.Regex(@"[\[\(_]v(\d+)[\]\)]|[-_\s](\d{5,8})[-_\s\)]", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

        public static bool IsBaseVersion(string? ver)
        {
            if (string.IsNullOrWhiteSpace(ver)) return true;
            string v = ver.Trim();
            while (v.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                v = v.Substring(1);
            }
            v = v.Trim();
            return string.IsNullOrEmpty(v) || v == "0" || v == "1.0" || v == "1.0.0" || v == "1.0.0.0" || v.Equals("PACKED", StringComparison.OrdinalIgnoreCase);
        }

        public static string CleanVersion(string? ver)
        {
            if (string.IsNullOrWhiteSpace(ver)) return "1.0.0";
            string v = ver.Trim();
            while (v.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            {
                v = v.Substring(1);
            }
            v = v.Trim();
            if (string.IsNullOrEmpty(v) || v == "0" || v.Equals("PACKED", StringComparison.OrdinalIgnoreCase))
            {
                return "1.0.0";
            }
            return v;
        }

        public static uint FixHexPackedVersionCode(uint rawCode)
        {
            if (rawCode == 0) return 0;
            if (rawCode % 65536 == 0) return rawCode;

            // Если версия упакована как шестнадцатеричный BCD код (например, 26215748 = 0x01900544 => "1900544")
            string hex = rawCode.ToString("X");
            if (hex.Length >= 5 && hex.Length <= 8 && uint.TryParse(hex, out uint parsedDec))
            {
                if (parsedDec % 65536 == 0 || (parsedDec < rawCode && parsedDec > 0))
                {
                    return parsedDec;
                }
            }
            return rawCode;
        }

        public static void ResolveVersionFromPath(string filePath, ref string displayVersion, ref string versionCode)
        {
            string fileName = System.IO.Path.GetFileName(filePath);
            string parentDirName = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(filePath) ?? "");
            string[] candidates = { fileName, parentDirName, filePath };

            // 1. Поиск парной версии из имени файла (например: "(1.29.0 - 1900544 - ...)")
            // Точно как в E:\STORM EDEN 3 (main_window.cpp lines 8810-8815): при наличии пары в имени файла,
            // она имеет высший приоритет над NACP базы и артефактами CNMT.
            foreach (var text in candidates)
            {
                if (string.IsNullOrWhiteSpace(text)) continue;

                var fm = FnPairVerRegex.Match(text);
                if (fm.Success && !string.IsNullOrEmpty(fm.Groups[1].Value))
                {
                    string fileDispVer = CleanVersion(fm.Groups[1].Value);
                    if (!IsBaseVersion(fileDispVer) || IsBaseVersion(displayVersion))
                    {
                        displayVersion = fileDispVer;
                    }
                    if (!string.IsNullOrEmpty(fm.Groups[2].Value))
                    {
                        versionCode = fm.Groups[2].Value;
                    }
                    // Никогда не подменяем реальную отображаемую версию эвристикой 1.0.{vcNum/65536}
                    return;
                }
            }

            // 2. Если парной версии в имени файла нет, проверяем одиночные regex
            string? foundDisplayVer = null;
            string? foundVersionCode = null;

            foreach (var text in candidates)
            {
                if (string.IsNullOrWhiteSpace(text)) continue;

                // Поиск общей версии (например: "(1.29.0)" или "[v1.29.0]")
                if (string.IsNullOrEmpty(foundDisplayVer))
                {
                    var m = FnVerRegex.Match(text);
                    if (m.Success && !string.IsNullOrEmpty(m.Groups[1].Value))
                    {
                        string candidate = m.Groups[1].Value;
                        if (!IsBaseVersion(candidate))
                        {
                            foundDisplayVer = candidate;
                        }
                    }
                }

                // Поиск кода версии ([v12345] или 5-8 цифр)
                if (string.IsNullOrEmpty(foundVersionCode))
                {
                    var vm = FnVnumRegex.Match(text);
                    if (vm.Success && !string.IsNullOrEmpty(vm.Groups[1].Value))
                    {
                        foundVersionCode = vm.Groups[1].Value;
                    }
                    else
                    {
                        var match = VerRegex.Match(text);
                        if (match.Success)
                        {
                            for (int i = 1; i < match.Groups.Count; i++)
                            {
                                var cap = match.Groups[i].Value;
                                if (!string.IsNullOrEmpty(cap) && uint.TryParse(cap, out uint parsed) && parsed > 0)
                                {
                                    foundVersionCode = cap;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            // Применяем найденную версию, если текущая является базовой
            if (IsBaseVersion(displayVersion))
            {
                if (!string.IsNullOrEmpty(foundDisplayVer) && !IsBaseVersion(foundDisplayVer))
                {
                    displayVersion = foundDisplayVer;
                }
            }

            displayVersion = CleanVersion(displayVersion);

            // Коррекция и назначение кода версии
            uint currentCode = 0;
            uint.TryParse(versionCode, out currentCode);
            currentCode = FixHexPackedVersionCode(currentCode);

            if (!string.IsNullOrEmpty(foundVersionCode) && uint.TryParse(foundVersionCode, out uint foundCodeVal))
            {
                foundCodeVal = FixHexPackedVersionCode(foundCodeVal);
                if (foundCodeVal > currentCode || currentCode == 0)
                {
                    currentCode = foundCodeVal;
                }
            }

            // Запасной расчет кода версии из display version (по формуле Nintendo Switch из E:\STORM EDEN 3)
            if (currentCode == 0 && !IsBaseVersion(displayVersion))
            {
                var parts = displayVersion.Split('.');
                if (parts.Length >= 2 && int.TryParse(parts[0], out int major) && int.TryParse(parts[1], out int minor))
                {
                    int patch = 0;
                    if (parts.Length >= 3) int.TryParse(parts[2], out patch);
                    if (major >= 1)
                    {
                        currentCode = (uint)((major - 1) * 655360 + minor * 65536 + (patch * 65536) / 10);
                    }
                }
            }

            versionCode = currentCode.ToString();
        }

        #endregion
    }
}
