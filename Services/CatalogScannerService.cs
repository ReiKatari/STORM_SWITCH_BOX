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

        public async Task ScanDirectoryAsync(string directoryPath, ObservableCollection<CatalogItem> catalog, CancellationToken token)
        {
            if (!Directory.Exists(directoryPath)) return;

            // Автоматическое извлечение архивов (.zip, .rar, .7z) перед сканированием
            await ExtractArchivesInDirectoryAsync(directoryPath, token);

            string[] extensions = { ".nsp", ".nsz", ".xci", ".xcz", ".3ds", ".cci", ".cia", ".cxi" };
            var files = Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.AllDirectories)
                .Where(f => extensions.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            // Дедупликация — отслеживаем уже добавленные пути
            var processedPaths = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var existing in catalog)
                processedPaths.Add(existing.FilePath);

            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested();

                // Пропуск уже добавленных файлов (дедупликация по пути)
                if (processedPaths.Contains(file)) continue;

                // Проверка целостности — пропускаем недокачанные/временные файлы
                if (!IsFileComplete(file)) continue;

                processedPaths.Add(file);

                var item = new CatalogItem
                {
                    FilePath = file,
                    FileName = System.IO.Path.GetFileName(file),
                    FileSize = Models.ProcessingTask.FormatSize(new FileInfo(file).Length)
                };

                App.RunOnUI(() => catalog.Add(item));

                await Task.Run(async () =>
                {
                    try
                    {
                        ExtractMetadata(item, file, token);
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

                // Проверка блокировки файла (если файл занят — вероятно ещё качается)
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

        /// <summary>
        /// Автоматически извлекает архивы (.zip, .rar, .7z) внутри директории.
        /// </summary>
        private async Task ExtractArchivesInDirectoryAsync(string directoryPath, CancellationToken token)
        {
            string[] archiveExtensions = { ".zip", ".rar", ".7z" };
            var archives = Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.AllDirectories)
                .Where(f => archiveExtensions.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            foreach (var archive in archives)
            {
                token.ThrowIfCancellationRequested();
                if (!IsFileComplete(archive)) continue;

                string extractDir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(archive)!, System.IO.Path.GetFileNameWithoutExtension(archive));
                
                // Не извлекаем, если папка уже есть (уже извлечено ранее)
                if (Directory.Exists(extractDir)) continue;

                string ext = System.IO.Path.GetExtension(archive).ToLowerInvariant();
                
                App.RunOnUI(() => App.Logger.Log($"📦 Автоизвлечение архива: {System.IO.Path.GetFileName(archive)}..."));

                bool extracted = false;

                if (ext == ".zip")
                {
                    try
                    {
                        System.IO.Compression.ZipFile.ExtractToDirectory(archive, extractDir, overwriteFiles: true);
                        extracted = true;
                    }
                    catch (Exception ex)
                    {
                        App.RunOnUI(() => App.Logger.Log($"⚠️ Ошибка распаковки ZIP: {ex.Message}", LogLevel.Warning));
                    }
                }
                else
                {
                    string sevenZipPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "7z.exe");
                    if (File.Exists(sevenZipPath))
                    {
                        try
                        {
                            var proc = new System.Diagnostics.Process();
                            proc.StartInfo.FileName = sevenZipPath;
                            proc.StartInfo.Arguments = $"x \"{archive}\" -o\"{extractDir}\" -y";
                            proc.StartInfo.UseShellExecute = false;
                            proc.StartInfo.CreateNoWindow = true;
                            proc.Start();
                            await proc.WaitForExitAsync();
                            if (proc.ExitCode == 0) extracted = true;
                        }
                        catch (Exception ex)
                        {
                            App.RunOnUI(() => App.Logger.Log($"⚠️ Ошибка распаковки через 7z: {ex.Message}", LogLevel.Warning));
                        }
                    }
                }

                if (extracted)
                {
                    App.RunOnUI(() => App.Logger.Log($"✅ Архив {System.IO.Path.GetFileName(archive)} распакован.", LogLevel.Success));
                }
            }
        }


        public async Task ScanSingleFileAsync(string filePath, ObservableCollection<CatalogItem> catalog, CancellationToken token)
        {
            if (!File.Exists(filePath)) return;

            // Check if it already exists in the catalog to prevent duplicates
            if (catalog.Any(c => c.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase))) return;

            string[] extensions = { ".nsp", ".nsz", ".xci", ".xcz", ".3ds", ".cci", ".cia", ".cxi" };
            if (!extensions.Contains(System.IO.Path.GetExtension(filePath).ToLowerInvariant())) return;

            var item = new CatalogItem
            {
                FilePath = filePath,
                FileName = System.IO.Path.GetFileName(filePath),
                FileSize = Models.ProcessingTask.FormatSize(new FileInfo(filePath).Length)
            };

            App.RunOnUI(() => catalog.Add(item));

            await Task.Run(() =>
            {
                try
                {
                    ExtractMetadata(item, filePath, token);
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

        private void ExtractMetadata(CatalogItem item, string filePath, CancellationToken token)
        {
            string ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
            if (Nintendo3dsService.Is3dsExtension(ext))
            {
                item.Is3ds = true;
                item.Platform = "3DS";
                var info3ds = App.Nintendo3ds.Parse3dsFile(filePath);
                App.RunOnUI(() =>
                {
                    item.TitleId = !string.IsNullOrEmpty(info3ds?.TitleId) ? info3ds.TitleId : "3DS";
                    item.TitleName = !string.IsNullOrEmpty(info3ds?.GameName) ? info3ds.GameName : System.IO.Path.GetFileNameWithoutExtension(filePath);
                    item.Version = !string.IsNullOrEmpty(info3ds?.Version) ? info3ds.Version : "v0";
                    item.Publisher = !string.IsNullOrEmpty(info3ds?.Publisher) ? info3ds.Publisher : "Nintendo";
                    item.Category = "Nintendo 3DS";
                    if (info3ds?.IconBytes != null && info3ds.IconBytes.Length > 0)
                    {
                        item.CoverBytes = info3ds.IconBytes;
                        try
                        {
                            var bmp = new BitmapImage();
                            using var ms = new MemoryStream(info3ds.IconBytes);
                            bmp.SetSource(ms.AsRandomAccessStream());
                            item.CoverImage = bmp;
                        }
                        catch { }
                    }
                    item.IsLoading = false;
                });
                return;
            }

            bool isXci = filePath.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase);

            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            IStorage storage = fileStream.AsStorage();
            
            IFileSystem? fileSystem = null;

            if (isXci)
            {
                storage.GetSize(out long storageSize).ThrowIfFailure();
                var rootStorage = new SubStorage(storage, 0x10000, storageSize - 0x10000);
                var rootPfs = new PartitionFileSystem(rootStorage);
                
                
                using var secureFile = new LibHac.Common.UniqueRef<IFile>();
                using var securePath2 = new LibHac.Fs.Path();
                securePath2.Initialize(new LibHac.Common.U8Span(System.Text.Encoding.UTF8.GetBytes("/secure"))).ThrowIfFailure();
                rootPfs.OpenFile(ref secureFile.Ref, in securePath2, LibHac.Fs.OpenMode.Read).ThrowIfFailure();
                
                var pfs = new PartitionFileSystem(secureFile.Release().AsStorage());
                
                fileSystem = pfs;
            }
            else
            {
                var pfs = new PartitionFileSystem(storage);
                
                fileSystem = pfs;
            }



            var entries = fileSystem.EnumerateEntries().ToList();
            var titleKeyMap = new System.Collections.Generic.Dictionary<string, byte[]>();

            foreach (var entry in entries)
            {
                if (entry.Name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase))
                {
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

                        if (isNcz)
                        {
                            try
                            {
                                var nczStorage = new Core.NSZ.StormNczStorage(entryStorage, titleKeyMap, globalSolidStorage, App.Keys.CurrentKeyset);
                                entryStorage = nczStorage;
                                toDispose = nczStorage;
                            }
                            catch (Exception ex)
                            {
                                App.Logger.Log($"[CatalogScanner] Error opening StormNczStorage for {entryName}: {ex.Message}", LogLevel.Error);
                                continue;
                            }
                        }

                        try
                        {
                            var nca = new Nca(_keysService.CurrentKeyset, entryStorage);
                        
                        if (nca.Header.ContentType == NcaContentType.Control)
                        {
                            var romfs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);
                            
                            // Parse NACP
                            using var nacpFileRef = new LibHac.Common.UniqueRef<IFile>();
                            using var nacpPath = new LibHac.Fs.Path();
                            nacpPath.Initialize(new LibHac.Common.U8Span(System.Text.Encoding.UTF8.GetBytes("/control.nacp"))).ThrowIfFailure();
                            
                            if (romfs.OpenFile(ref nacpFileRef.Ref, in nacpPath, LibHac.Fs.OpenMode.Read).IsSuccess())
                            {
                                var nacpStream = nacpFileRef.Release().AsStream();
                                var nacp = new LibHac.Ns.ApplicationControlProperty();
                                nacpStream.Read(System.Runtime.InteropServices.MemoryMarshal.AsBytes(new Span<LibHac.Ns.ApplicationControlProperty>(ref nacp)));
                                
                                string title = nacp.Title[0].NameString.ToString();
                                string pub = nacp.Title[0].PublisherString.ToString();

                                if (HasGarbageCharacters(title)) title = "";
                                if (HasGarbageCharacters(pub)) pub = "";

                                if (string.IsNullOrWhiteSpace(title))
                                {
                                    for (int i = 0; i < 16; i++)
                                    {
                                        string t = nacp.Title[i].NameString.ToString();
                                        if (!string.IsNullOrWhiteSpace(t)) { title = t; break; }
                                    }
                                }

                                if (string.IsNullOrWhiteSpace(pub))
                                {
                                    for (int i = 0; i < 16; i++)
                                    {
                                        string p = nacp.Title[i].PublisherString.ToString();
                                        if (!string.IsNullOrWhiteSpace(p)) { pub = p; break; }
                                    }
                                }

                                string titleIdHex = nca.Header.TitleId.ToString("X16");
                                if (titleIdHex.EndsWith("8"))
                                {
                                    titleIdHex = titleIdHex.Substring(0, 15) + "0";
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

                                App.RunOnUI(() =>
                                {
                                    bool isCurrentUpdate = titleIdHex.EndsWith("800");
                                    
                                    // Нормализуем TitleId к базовой игре (000), чтобы TitleDbService работал корректно
                                    string normalizedTitleId = titleIdHex;
                                    if (isCurrentUpdate)
                                    {
                                        normalizedTitleId = titleIdHex.Substring(0, 13) + "000";
                                    }

                                    // Если у нас уже есть версия от ОБНОВЛЕНИЯ (мы поняли это по флагу или потому что уже записали высокую версию),
                                    // а сейчас читается БАЗА (000), мы не должны откатывать версию назад.
                                    // Простой хак: если текущая NACP - обновление, мы ВСЕГДА перезаписываем версию.
                                    // Если текущая NACP - база, мы перезаписываем только если версия еще дефолтная.
                                    if (!isCurrentUpdate && item.Version != "v0" && item.Version != "0" && !IsBaseVersion(item.Version))
                                    {
                                        // База не может перезаписывать уже извлеченную версию обновления
                                        // Но TitleId и Имя можно обновить, если они еще пустые
                                        if (item.TitleId == "0000000000000000") item.TitleId = normalizedTitleId;
                                        if (item.TitleName == "Unknown Game" && !string.IsNullOrWhiteSpace(title)) item.TitleName = title;
                                        return;
                                    }

                                    item.TitleName = string.IsNullOrWhiteSpace(title) ? "Unknown" : title;
                                    item.Publisher = string.IsNullOrWhiteSpace(pub) ? "Unknown" : pub;
                                    string nacpVer = nacp.DisplayVersionString.ToString();
                                    if (!IsBaseVersion(nacpVer) || IsBaseVersion(item.Version))
                                    {
                                        item.Version = CleanVersion(nacpVer);
                                    }
                                    item.TitleId = normalizedTitleId;

                                    item.SupportedLanguages = string.IsNullOrEmpty(supportedLangs) ? "Неизвестно" : supportedLangs;
                                    item.RatingAge = rating;
                                    item.VideoCapture = videoCap;
                                    item.SaveDataSize = saveSize;

                                    // Пытаемся извлечь Регион ИЗ САМОГО ФАЙЛА (на основе вшитых языков в NACP)
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

                                    if (string.IsNullOrEmpty(fileRegion))
                                    {
                                        fileRegion = "UNKNOWN";
                                    }
                                    
                                    item.Regions = fileRegion;
                                });
                            }

                            // Try to get icon
                            var iconLangs = new[] { "AmericanEnglish", "BritishEnglish", "Japanese", "French", "German", "LatinAmericanSpanish", "Spanish", "Italian", "Dutch", "CanadianFrench", "Portuguese", "Russian", "Korean", "TraditionalChinese", "SimplifiedChinese" };
                            
                            foreach (var lang in iconLangs)
                            {
                                using var iconFileRef = new LibHac.Common.UniqueRef<IFile>();
                                using var iconPath = new LibHac.Fs.Path();
                                iconPath.Initialize(new LibHac.Common.U8Span(System.Text.Encoding.UTF8.GetBytes($"/icon_{lang}.dat"))).ThrowIfFailure();
                                
                                if (romfs.OpenFile(ref iconFileRef.Ref, in iconPath, LibHac.Fs.OpenMode.Read).IsSuccess())
                                {
                                    using var memStream = new MemoryStream();
                                    iconFileRef.Release().AsStream().CopyTo(memStream);
                                    memStream.Position = 0;
                                    var buffer = memStream.ToArray();

                                    App.RunOnUI(async () =>
                                    {
                                        try
                                        {
                                            var bmp = new BitmapImage();
                                            using var ms = new MemoryStream(buffer);
                                            await bmp.SetSourceAsync(ms.AsRandomAccessStream());
                                            item.CoverImage = bmp;
                                            item.CoverBytes = buffer;
                                        }
                                        catch { }
                                    });
                                    break; // Icon found
                                }
                            }
                            
                            App.RunOnUI(() => item.IsLoading = false);
                        }
                        else if (nca.Header.ContentType == NcaContentType.Manual)
                        {
                            try
                            {
                                var romfs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.ErrorOnInvalid);
                                int count = 0;
                                foreach (var imgEntry in romfs.EnumerateEntries("/", "*.jpg").Concat(romfs.EnumerateEntries("/", "*.png")))
                                {
                                    if (count >= 5) break; // Limit to 5 screenshots
                                    
                                    using var imgFileRef = new LibHac.Common.UniqueRef<IFile>();
                                    using var imgPath = new LibHac.Fs.Path();
                                    imgPath.Initialize(new LibHac.Common.U8Span(System.Text.Encoding.UTF8.GetBytes(imgEntry.FullPath))).ThrowIfFailure();
                                    
                                    if (romfs.OpenFile(ref imgFileRef.Ref, in imgPath, LibHac.Fs.OpenMode.Read).IsSuccess())
                                    {
                                        using var memStream = new MemoryStream();
                                        imgFileRef.Release().AsStream().CopyTo(memStream);
                                        var buffer = memStream.ToArray();

                                        App.RunOnUI(async () =>
                                        {
                                            try
                                            {
                                                var bmp = new BitmapImage();
                                                using var ms = new MemoryStream(buffer);
                                                await bmp.SetSourceAsync(ms.AsRandomAccessStream());
                                                item.Screenshots.Add(bmp);
                                                item.HasScreenshots = true;
                                            }
                                            catch { }
                                        });
                                        count++;
                                    }
                                }
                            }
                            catch { }
                        }
                        else if (nca.Header.ContentType == NcaContentType.Meta)
                        {
                            try
                            {
                                var fs = nca.OpenFileSystem(0, IntegrityCheckLevel.ErrorOnInvalid);
                                foreach (var cnmtEntry in fs.EnumerateEntries())
                                {
                                    if (!cnmtEntry.Name.EndsWith(".cnmt", StringComparison.OrdinalIgnoreCase)) continue;
                                    
                                    using var cnmtFileRef = new LibHac.Common.UniqueRef<IFile>();
                                    using var cnmtPath = new LibHac.Fs.Path();
                                    cnmtPath.Initialize(new LibHac.Common.U8Span(System.Text.Encoding.UTF8.GetBytes(cnmtEntry.FullPath))).ThrowIfFailure();
                                    fs.OpenFile(ref cnmtFileRef.Ref, in cnmtPath, LibHac.Fs.OpenMode.Read).ThrowIfFailure();
                                    
                                    using var cnmtStream = new MemoryStream();
                                    cnmtFileRef.Release().AsStream().CopyTo(cnmtStream);
                                    byte[] cnmtBytes = cnmtStream.ToArray();
                                    uint version = 0;
                                    try 
                                    {
                                        if (cnmtBytes.Length >= 12)
                                            version = BitConverter.ToUInt32(cnmtBytes, 0x08);
                                    }
                                    catch { }
                                    
                                    uint fixedVersion = FixHexPackedVersionCode(version);

                                    App.RunOnUI(() => 
                                    {
                                        uint currentVer = 0;
                                        uint.TryParse(item.VersionCode, out currentVer);
                                        currentVer = FixHexPackedVersionCode(currentVer);
                                        
                                        var match = System.Text.RegularExpressions.Regex.Match(item.FileName ?? "", @"\[v(\d+)\]");
                                        if (match.Success && uint.TryParse(match.Groups[1].Value, out uint nameVer))
                                        {
                                            nameVer = FixHexPackedVersionCode(nameVer);
                                            if (nameVer > currentVer) currentVer = nameVer;
                                        }
                                        
                                        if (fixedVersion > currentVer)
                                            item.VersionCode = fixedVersion.ToString();
                                        else if (currentVer > 0)
                                            item.VersionCode = currentVer.ToString();
                                    });
                                    break;
                                }
                            }
                            catch
                            {
                                App.RunOnUI(() => {
                                    if (item.VersionCode == "0") item.VersionCode = "ERR_META";
                                });
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

            // Применяем эталонный алгоритм разрешения версий из E:\STORM EDEN 3
            string dispVer = item.Version;
            string vCode = item.VersionCode;
            ResolveVersionFromPath(filePath, ref dispVer, ref vCode);

            App.RunOnUI(() =>
            {
                item.Version = dispVer;
                item.VersionCode = vCode;
            });

            // Обогащаем данными из онлайн базы TitleDB в фоне и завершаем загрузку
            App.TitleDb.EnrichCatalogItem(item);
            App.RunOnUI(() => 
            {
                item.IsLoading = false;
            });
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
                    displayVersion = CleanVersion(fm.Groups[1].Value);
                    if (!string.IsNullOrEmpty(fm.Groups[2].Value))
                    {
                        versionCode = fm.Groups[2].Value;
                    }
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
