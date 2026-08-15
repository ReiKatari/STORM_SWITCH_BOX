using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using LibHac.Ns;
using StormSwitchBox.Models;
using Path = System.IO.Path;

namespace StormSwitchBox.Services
{
    public class ControlEditorService
    {
        private readonly string _toolsDir;
        private readonly string _hacpackExe;
        private readonly string _keysPath;

        public ControlEditorService()
        {
            _toolsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
            _hacpackExe = Path.Combine(_toolsDir, "com.github.nozwock.yanu", "hacpack.exe");
            _keysPath = Path.Combine(_toolsDir, "prod.keys");
            if (!File.Exists(_keysPath))
            {
                _keysPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "prod.keys");
            }
        }

        private static IFile OpenFileSafe(IFileSystem fsToOpen, string pth)
        {
            using var fRef = new UniqueRef<IFile>();
            using var path = new LibHac.Fs.Path();
            path.Initialize(new U8Span(Encoding.UTF8.GetBytes(pth))).ThrowIfFailure();
            fsToOpen.OpenFile(ref fRef.Ref, in path, OpenMode.Read).ThrowIfFailure();
            return fRef.Release();
        }

        /// <summary>
        /// Извлекает метаданные и иконку из NSP/XCI файла для редактирования.
        /// </summary>
        public async Task<GameMetadataEditModel?> ExtractMetadataAsync(string filePath)
        {
            return await Task.Run(() =>
            {
                FileStream? stream = null;
                try
                {
                    if (!File.Exists(filePath)) return null;

                    var model = new GameMetadataEditModel
                    {
                        SourceFilePath = filePath
                    };

                    stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    IStorage storage = stream.AsStorage();

                    IFileSystem pfs;
                    if (filePath.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase))
                    {
                        storage.GetSize(out long storageSize).ThrowIfFailure();
                        var rootStorage = new SubStorage(storage, 0x10000, storageSize - 0x10000);
                        var rootPfs = new PartitionFileSystem(rootStorage);

                        using var secureFile = OpenFileSafe(rootPfs, "/secure");
                        pfs = new PartitionFileSystem(secureFile.AsStorage());
                    }
                    else
                    {
                        pfs = new PartitionFileSystem(storage);
                    }

                    // Ищем Control NCA
                    foreach (var entry in pfs.EnumerateEntries())
                    {
                        if (entry.Type == DirectoryEntryType.Directory) continue;
                        string name = entry.Name;
                        if (!name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase)) continue;

                        try
                        {
                            using var ncaFile = OpenFileSafe(pfs, "/" + name);
                            var ncaStorage = ncaFile.AsStorage();
                            var nca = new Nca(App.Keys.CurrentKeyset, ncaStorage);

                            if (nca.Header.ContentType == NcaContentType.Control)
                            {
                                model.TitleId = nca.Header.TitleId.ToString("X16");

                                var controlFs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);

                                // Чтение control.nacp
                                try
                                {
                                    using var nacpFile = OpenFileSafe(controlFs, "/control.nacp");
                                    using var nacpStream = nacpFile.AsStream();
                                    byte[] nacpBytes = new byte[0x4000];
                                    nacpStream.Read(nacpBytes, 0, nacpBytes.Length);
                                    model.RawNacpBytes = nacpBytes;

                                    var nacp = new ApplicationControlProperty();
                                    var span = System.Runtime.InteropServices.MemoryMarshal.AsBytes(new Span<ApplicationControlProperty>(ref nacp));
                                    nacpBytes.AsSpan(0, Math.Min(nacpBytes.Length, span.Length)).CopyTo(span);

                                    model.Version = nacp.DisplayVersionString.ToString();
                                    model.TitleNameEnglish = nacp.Title[0].NameString.ToString(); // AmericanEnglish
                                    model.TitleNameRussian = nacp.Title[11].NameString.ToString(); // Russian
                                    model.Publisher = nacp.Title[0].PublisherString.ToString();

                                    if (string.IsNullOrEmpty(model.TitleNameEnglish))
                                    {
                                        for (int i = 0; i < 16; i++)
                                        {
                                            string t = nacp.Title[i].NameString.ToString();
                                            if (!string.IsNullOrEmpty(t)) { model.TitleNameEnglish = t; break; }
                                        }
                                    }

                                    if (string.IsNullOrEmpty(model.Publisher))
                                    {
                                        for (int i = 0; i < 16; i++)
                                        {
                                            string p = nacp.Title[i].PublisherString.ToString();
                                            if (!string.IsNullOrEmpty(p)) { model.Publisher = p; break; }
                                        }
                                    }
                                }
                                catch { }

                                // Чтение иконки
                                string[] iconNames = { "/icon_AmericanEnglish.dat", "/icon_Russian.dat", "/icon_Default.dat" };
                                foreach (var iconName in iconNames)
                                {
                                    try
                                    {
                                        using var iconFile = OpenFileSafe(controlFs, iconName);
                                        using var iconStream = iconFile.AsStream();
                                        using var ms = new MemoryStream();
                                        iconStream.CopyTo(ms);
                                        model.OriginalIconBytes = ms.ToArray();
                                        break;
                                    }
                                    catch { }
                                }

                                return model;
                            }
                        }
                        catch { }
                    }

                    return model;
                }
                catch (Exception ex)
                {
                    App.Logger.Log($"[ControlEditor] Ошибка чтения метаданных из {filePath}: {ex.Message}", LogLevel.Warning);
                    return null;
                }
                finally
                {
                    stream?.Dispose();
                }
            });
        }

        /// <summary>
        /// Применяет кастомные метаданные (название, издатель, иконка) и пересобирает Control NCA для целевого NSP.
        /// </summary>
        public async Task<bool> ApplyCustomMetadataAsync(
            GameMetadataEditModel model, 
            string targetNspPath, 
            ProcessingTask task, 
            CancellationToken ct)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "StormControl_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            string romfsDir = Path.Combine(tempDir, "control_romfs");
            string outNcaDir = Path.Combine(tempDir, "out_nca");

            try
            {
                Directory.CreateDirectory(romfsDir);
                Directory.CreateDirectory(outNcaDir);

                // 1. Подготовка control.nacp
                byte[] nacpData = model.RawNacpBytes != null && model.RawNacpBytes.Length >= 0x4000 
                    ? (byte[])model.RawNacpBytes.Clone() 
                    : new byte[0x4000];

                string finalEngName = !string.IsNullOrEmpty(model.TitleNameEnglish) ? model.TitleNameEnglish : model.TitleNameRussian;
                string finalRusName = !string.IsNullOrEmpty(model.TitleNameRussian) ? model.TitleNameRussian : model.TitleNameEnglish;
                string finalPub = !string.IsNullOrEmpty(model.Publisher) ? model.Publisher : "Nintendo";

                // Заполняем названия для языков
                for (int i = 0; i < 16; i++)
                {
                    string nameToUse = (i == 11) ? finalRusName : finalEngName;
                    if (!string.IsNullOrEmpty(nameToUse))
                    {
                        byte[] nameBytes = Encoding.UTF8.GetBytes(nameToUse);
                        byte[] pubBytes = Encoding.UTF8.GetBytes(finalPub);

                        Array.Clear(nacpData, i * 0x300, 0x200);
                        Array.Copy(nameBytes, 0, nacpData, i * 0x300, Math.Min(nameBytes.Length, 0x1FF));

                        Array.Clear(nacpData, i * 0x300 + 0x200, 0x100);
                        Array.Copy(pubBytes, 0, nacpData, i * 0x300 + 0x200, Math.Min(pubBytes.Length, 0xFF));
                    }
                }

                File.WriteAllBytes(Path.Combine(romfsDir, "control.nacp"), nacpData);

                // 2. Подготовка иконки (512x512 JPEG)
                byte[]? iconBytes = model.CustomIconBytes ?? model.OriginalIconBytes;
                if (iconBytes != null && iconBytes.Length > 0)
                {
                    // Пишем для всех распространенных языков
                    string[] iconNames = { 
                        "icon_AmericanEnglish.dat", "icon_Russian.dat", "icon_BritishEnglish.dat",
                        "icon_Japanese.dat", "icon_French.dat", "icon_German.dat", "icon_Spanish.dat",
                        "icon_Italian.dat", "icon_Default.dat"
                    };

                    foreach (var iname in iconNames)
                    {
                        File.WriteAllBytes(Path.Combine(romfsDir, iname), iconBytes);
                    }
                }

                // 3. Сборка нового Control NCA через hacpack.exe
                string titleId = !string.IsNullOrEmpty(model.TitleId) ? model.TitleId : "0100000000010000";
                string keysFile = App.Settings.Current.KeysPath;
                if (string.IsNullOrEmpty(keysFile) || !File.Exists(keysFile)) keysFile = _keysPath;

                string hacpackArgs = $"-k \"{keysFile}\" --type nca --ncatype control --titleid {titleId} --romfsdir \"{romfsDir}\" -o \"{outNcaDir}\"";

                App.Logger.Log($"[ControlEditor] Сборка Control NCA: hacpack {hacpackArgs}", LogLevel.Info);
                App.RunOnUI(() => task.LogDetails += "\n🎨 [ControlEditor] Сборка кастомного Control NCA (иконка + название)...");

                int exitCode = await ExternalProcessRunner.RunAsync(
                    _hacpackExe,
                    hacpackArgs,
                    _toolsDir,
                    task,
                    ct
                );

                if (exitCode != 0)
                {
                    App.Logger.Log($"[ControlEditor] Ошибка hacpack: exit code {exitCode}", LogLevel.Warning);
                    return false;
                }

                var producedNcas = Directory.GetFiles(outNcaDir, "*.nca");
                if (producedNcas.Length == 0)
                {
                    App.Logger.Log("[ControlEditor] hacpack не создал NCA файл", LogLevel.Warning);
                    return false;
                }

                string newControlNca = producedNcas[0];
                string newControlNcaName = Path.GetFileName(newControlNca);

                // 4. Поиск Program NCA и пересборка Meta NCA (CNMT) чтобы метаданные ссылались на новый Control NCA
                string? newMetaNca = null;
                string? newMetaNcaName = null;

                if (File.Exists(targetNspPath))
                {
                    string extractedProgNca = Path.Combine(tempDir, "extracted_prog.nca");
                    string extractedLegalNca = Path.Combine(tempDir, "extracted_legal.nca");
                    string extractedManualNca = Path.Combine(tempDir, "extracted_manual.nca");
                    string? progPath = null;
                    string? manualPath = null;
                    string titleVersionHex = "0x0";
                    try
                    {
                        var pInfo = App.SwitchFormat.ParseNsp(targetNspPath);
                        if (!string.IsNullOrEmpty(pInfo.Version) && uint.TryParse(pInfo.Version, out uint parsedV))
                        {
                            titleVersionHex = $"0x{parsedV:X}";
                        }
                    }
                    catch { }

                    // Извлекаем Program NCA и другие мета-зависимости из целевого NSP
                    using (var srcStream = new FileStream(targetNspPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        IStorage srcStorage = srcStream.AsStorage();
                        var srcPfs = new PartitionFileSystem(srcStorage);

                        foreach (var entry in srcPfs.EnumerateEntries())
                        {
                            if (entry.Type == DirectoryEntryType.Directory) continue;
                            string name = entry.Name;
                            if (!name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase)) continue;

                            try
                            {
                                using var ef = OpenFileSafe(srcPfs, "/" + name);
                                var nca = new Nca(App.Keys.CurrentKeyset, ef.AsStorage());
                                
                                if (nca.Header.ContentType == NcaContentType.Program && progPath == null)
                                {
                                    using var ps = ef.AsStream();
                                    using var fs = new FileStream(extractedProgNca, FileMode.Create, FileAccess.Write);
                                    ps.CopyTo(fs);
                                    progPath = extractedProgNca;
                                }
                                else if (nca.Header.ContentType == NcaContentType.Manual && manualPath == null)
                                {
                                    using var ms = ef.AsStream();
                                    using var fs = new FileStream(extractedManualNca, FileMode.Create, FileAccess.Write);
                                    ms.CopyTo(fs);
                                    manualPath = extractedManualNca;
                                }
                            }
                            catch { }
                        }
                    }

                    // Если Program NCA найден, собираем обновленный Meta NCA через hacpack
                    if (progPath != null && File.Exists(progPath))
                    {
                        string metaArgs = $"-k \"{keysFile}\" --type nca --ncatype meta --titleid {titleId} --titletype application --titleversion {titleVersionHex} --programnca \"{progPath}\" --controlnca \"{newControlNca}\"";
                        if (manualPath != null && File.Exists(manualPath))
                        {
                            metaArgs += $" --htmldocnca \"{manualPath}\"";
                        }
                        metaArgs += $" -o \"{outNcaDir}\"";

                        int metaCode = await ExternalProcessRunner.RunAsync(
                            _hacpackExe,
                            metaArgs,
                            _toolsDir,
                            task,
                            ct
                        );

                        if (metaCode == 0)
                        {
                            var metaNcas = Directory.GetFiles(outNcaDir, "*.cnmt.nca");
                            if (metaNcas.Length > 0)
                            {
                                newMetaNca = metaNcas[0];
                                newMetaNcaName = Path.GetFileName(newMetaNca);
                            }
                        }
                    }

                    // 5. Замена Control NCA и Meta NCA в targetNspPath с помощью LibHac PartitionFileSystemBuilder
                    await Task.Run(() =>
                    {
                        string tempPatchedNsp = Path.Combine(tempDir, "patched_target.nsp");
                        var pfsBuilder = new PartitionFileSystemBuilder();

                        using (var srcStream = new FileStream(targetNspPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            IStorage srcStorage = srcStream.AsStorage();
                            var srcPfs = new PartitionFileSystem(srcStorage);
                            var openedFiles = new List<IFile>();
                            var openedStreams = new List<FileStream>();
                            var entryDict = new Dictionary<string, IStorage>(StringComparer.OrdinalIgnoreCase);

                            try
                            {
                                foreach (var entry in srcPfs.EnumerateEntries())
                                {
                                    if (entry.Type == DirectoryEntryType.Directory) continue;
                                    string name = entry.Name;

                                    // Пропускаем старый Control NCA
                                    bool isOldControl = false;
                                    bool isOldMeta = false;

                                    if (name.EndsWith(".cnmt.nca", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".cnmt.xml", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (newMetaNca != null) isOldMeta = true; // Заменим новым Meta NCA
                                    }
                                    else if (name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase))
                                    {
                                        try
                                        {
                                            using var ef = OpenFileSafe(srcPfs, "/" + name);
                                            var nca = new Nca(App.Keys.CurrentKeyset, ef.AsStorage());
                                            if (nca.Header.ContentType == NcaContentType.Control)
                                            {
                                                isOldControl = true;
                                            }
                                            else if (nca.Header.ContentType == NcaContentType.Meta && newMetaNca != null)
                                            {
                                                isOldMeta = true;
                                            }
                                        }
                                        catch { }
                                    }

                                    if (isOldControl || isOldMeta) continue;

                                    var oldFile = OpenFileSafe(srcPfs, "/" + name);
                                    openedFiles.Add(oldFile);
                                    entryDict[name] = oldFile.AsStorage();
                                }

                                if (newMetaNca != null && newMetaNcaName != null)
                                {
                                    var metaStream = new FileStream(newMetaNca, FileMode.Open, FileAccess.Read, FileShare.Read);
                                    openedStreams.Add(metaStream);
                                    entryDict[newMetaNcaName] = metaStream.AsStorage();
                                }

                                var controlStream = new FileStream(newControlNca, FileMode.Open, FileAccess.Read, FileShare.Read);
                                openedStreams.Add(controlStream);
                                entryDict[newControlNcaName] = controlStream.AsStorage();

                                // Сортируем файлы в строгом порядке Nintendo Switch PFS0:
                                // 0: Meta (CNMT) -> 1: Control -> 2: Program -> 3: Manual -> 50: DLC -> 90: Tickets/Certs
                                var ordered = entryDict.OrderBy(kvp =>
                                {
                                    string lower = kvp.Key.ToLowerInvariant();
                                    if (lower.EndsWith(".tik")) return 90;
                                    if (lower.EndsWith(".cert")) return 91;
                                    if (lower.EndsWith(".cnmt.nca") || lower.EndsWith(".cnmt.xml")) return 0;
                                    if (lower.Equals(newControlNcaName, StringComparison.OrdinalIgnoreCase) || lower.Contains("control")) return 1;
                                    if (lower.Contains("program")) return 2;
                                    if (lower.Contains("manual")) return 3;
                                    return 5;
                                }).ThenBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);

                                foreach (var kvp in ordered)
                                {
                                    pfsBuilder.AddFile(kvp.Key, new StorageFile(new SafeStorageWrapper(kvp.Value), OpenMode.Read));
                                }

                                using var builtPfs = pfsBuilder.Build(PartitionFileSystemType.Standard);
                                builtPfs.GetSize(out long totalSize).ThrowIfFailure();

                                using var destStream = new FileStream(tempPatchedNsp, FileMode.Create, FileAccess.Write, FileShare.None, 32 * 1024 * 1024, FileOptions.SequentialScan);
                                long remaining = totalSize;
                                long offset = 0;
                                byte[] buffer = new byte[32 * 1024 * 1024];

                                while (remaining > 0)
                                {
                                    ct.ThrowIfCancellationRequested();
                                    int toRead = (int)Math.Min(buffer.Length, remaining);
                                    builtPfs.Read(offset, buffer.AsSpan(0, toRead)).ThrowIfFailure();
                                    destStream.Write(buffer, 0, toRead);
                                    offset += toRead;
                                    remaining -= toRead;
                                }
                            }
                            finally
                            {
                                foreach (var f in openedFiles) try { f.Dispose(); } catch { }
                                foreach (var s in openedStreams) try { s.Dispose(); } catch { }
                            }
                        }

                        File.Delete(targetNspPath);
                        File.Move(tempPatchedNsp, targetNspPath);
                    }, ct);
                }

                App.RunOnUI(() => task.LogDetails += "\n✅ [ControlEditor] Кастомные метаданные и иконка успешно интегрированы!");
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[ControlEditor] Ошибка применения метаданных: {ex.Message}", LogLevel.Error);
                App.RunOnUI(() => task.LogDetails += $"\n⚠️ [ControlEditor] Ошибка интеграции метаданных: {ex.Message}");
                return false;
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }
}
