using System;
using System.IO;
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

            string[] extensions = { ".nsp", ".nsz", ".xci", ".xcz" };
            var files = Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.AllDirectories)
                .Where(f => extensions.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            foreach (var file in files)
            {
                token.ThrowIfCancellationRequested();

                var item = new CatalogItem
                {
                    FilePath = file,
                    FileName = System.IO.Path.GetFileName(file),
                    FileSize = Models.ProcessingTask.FormatSize(new FileInfo(file).Length)
                };

                App.MainDispatcher?.TryEnqueue(() => catalog.Add(item));

                await Task.Run(async () =>
                {
                    try
                    {
                        ExtractMetadata(item, file, token);
                    }
                    catch (Exception ex)
                    {
                        App.MainDispatcher?.TryEnqueue(() => 
                        {
                            item.IsLoading = false;
                            item.HasError = true;
                            item.ErrorMessage = ex.Message;
                        });
                    }
                }, token);
            }
        }

        public async Task ScanSingleFileAsync(string filePath, ObservableCollection<CatalogItem> catalog, CancellationToken token)
        {
            if (!File.Exists(filePath)) return;

            // Check if it already exists in the catalog to prevent duplicates
            if (catalog.Any(c => c.FilePath.Equals(filePath, StringComparison.OrdinalIgnoreCase))) return;

            string[] extensions = { ".nsp", ".nsz", ".xci", ".xcz" };
            if (!extensions.Contains(System.IO.Path.GetExtension(filePath).ToLowerInvariant())) return;

            var item = new CatalogItem
            {
                FilePath = filePath,
                FileName = System.IO.Path.GetFileName(filePath),
                FileSize = Models.ProcessingTask.FormatSize(new FileInfo(filePath).Length)
            };

            App.MainDispatcher?.TryEnqueue(() => catalog.Add(item));

            await Task.Run(() =>
            {
                try
                {
                    ExtractMetadata(item, filePath, token);
                }
                catch (Exception ex)
                {
                    App.MainDispatcher?.TryEnqueue(() => 
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
                            using var stream = new MemoryStream(tikData);
                            var ticket = new LibHac.Tools.Es.Ticket(stream);
                            byte[] tKey = ticket.GetTitleKey(_keysService.CurrentKeyset);
                            string rightsIdStr = BitConverter.ToString(ticket.RightsId).Replace("-", "").ToLowerInvariant();
                            titleKeyMap[rightsIdStr] = tKey;
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

                                App.MainDispatcher?.TryEnqueue(() =>
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
                                    if (!isCurrentUpdate && item.Version != "v0" && item.Version != "0")
                                    {
                                        // База не может перезаписывать уже извлеченную версию обновления
                                        // Но TitleId и Имя можно обновить, если они еще пустые
                                        if (item.TitleId == "0000000000000000") item.TitleId = normalizedTitleId;
                                        if (item.TitleName == "Unknown Game" && !string.IsNullOrWhiteSpace(title)) item.TitleName = title;
                                        return;
                                    }

                                    item.TitleName = string.IsNullOrWhiteSpace(title) ? "Unknown" : title;
                                    item.Publisher = string.IsNullOrWhiteSpace(pub) ? "Unknown" : pub;
                                    item.Version = nacp.DisplayVersionString.ToString();
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

                                    App.MainDispatcher?.TryEnqueue(async () =>
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
                            
                            App.MainDispatcher?.TryEnqueue(() => item.IsLoading = false);
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

                                        App.MainDispatcher?.TryEnqueue(async () =>
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
                                    
                                    App.MainDispatcher?.TryEnqueue(() => 
                                    {
                                        uint currentVer = 0;
                                        uint.TryParse(item.VersionCode, out currentVer);
                                        
                                        var match = System.Text.RegularExpressions.Regex.Match(item.FileName ?? "", @"\[v(\d+)\]");
                                        if (match.Success && uint.TryParse(match.Groups[1].Value, out uint nameVer))
                                        {
                                            if (nameVer > currentVer) currentVer = nameVer;
                                        }
                                        
                                        if (version > currentVer)
                                            item.VersionCode = version.ToString();
                                        else if (currentVer > 0)
                                            item.VersionCode = currentVer.ToString();
                                    });
                                    break;
                                }
                            }
                            catch
                            {
                                App.MainDispatcher?.TryEnqueue(() => {
                                    if (item.VersionCode == "0") item.VersionCode = "ERR_META";
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Log($"[CatalogScanner] Ошибка парсинга {entryName}: {ex.Message}\n{ex.StackTrace}", LogLevel.Error);
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

            // Обогащаем данными из онлайн базы TitleDB и завершаем загрузку строго после того,
            // как все предыдущие задачи TryEnqueue (NACP и CNMT) закончат заполнение свойств item.
            App.MainDispatcher?.TryEnqueue(() => 
            {
                App.TitleDb.EnrichCatalogItem(item);
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
    }
}
