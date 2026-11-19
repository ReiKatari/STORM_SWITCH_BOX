using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography;
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
            if (!File.Exists(_hacpackExe))
            {
                _hacpackExe = Path.Combine(_toolsDir, "hacpack.exe");
            }
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
                try
                {
                    if (!File.Exists(filePath)) return null;

                    var model = new GameMetadataEditModel
                    {
                        SourceFilePath = filePath
                    };

                    using var container = SwitchContainerHandle.Open(filePath);
                    IFileSystem pfs = container.FileSystem;

                    // Ищем Control NCA (.nca или .ncz)
                    foreach (var entry in pfs.EnumerateEntries())
                    {
                        if (entry.Type == DirectoryEntryType.Directory) continue;
                        string name = entry.Name;
                        bool isNca = name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase);
                        bool isNcz = name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase);
                        if (!isNca && !isNcz) continue;

                        try
                        {
                            using var ncaFile = OpenFileSafe(pfs, "/" + name);
                            IStorage rawStorage = ncaFile.AsStorage();
                            using var nczStorage = isNcz
                                ? new Core.NSZ.StormNczStorage(rawStorage, null, null, App.Keys.CurrentKeyset)
                                : null;
                            var nca = new Nca(App.Keys.CurrentKeyset, nczStorage ?? rawStorage);

                            if (nca.Header.ContentType == NcaContentType.Control)
                            {
                                model.TitleId = nca.Header.TitleId.ToString("X16");

                                var controlFs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);
                                using var controlFsDisp = controlFs as IDisposable;

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
            });
        }

        /// <summary>
        /// Применяет кастомные метаданные (название, издатель, иконка) и пересобирает Control NCA для целевого NSP/XCI.
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
            TempCleanupService.RegisterActiveTempDirectory(tempDir);

            try
            {
                Directory.CreateDirectory(romfsDir);
                Directory.CreateDirectory(outNcaDir);

                // 1. Подготовка control.nacp (сначала читаем актуальный NACP и иконку из уже собранного контейнера)
                byte[]? currentTargetNacp = null;
                try
                {
                    var currentMeta = await ExtractMetadataAsync(targetNspPath);
                    if (currentMeta != null)
                    {
                        if (currentMeta.RawNacpBytes != null && currentMeta.RawNacpBytes.Length >= 0x4000)
                        {
                            currentTargetNacp = currentMeta.RawNacpBytes;
                        }
                        if (string.IsNullOrWhiteSpace(model.TitleId) ||
                            model.TitleId == "0100000000000000" ||
                            model.TitleId == "0100000000010000")
                        {
                            if (!string.IsNullOrWhiteSpace(currentMeta.TitleId) && currentMeta.TitleId != "0100000000000000")
                                model.TitleId = currentMeta.TitleId;
                        }
                        if ((model.OriginalIconBytes == null || model.OriginalIconBytes.Length == 0) &&
                            currentMeta.OriginalIconBytes != null && currentMeta.OriginalIconBytes.Length > 0)
                        {
                            model.OriginalIconBytes = currentMeta.OriginalIconBytes;
                            if (string.IsNullOrEmpty(model.CustomIconPath) && model.HasBadges)
                            {
                                model.CustomIconBytes = IconBadgeStudioService.RenderIconWithBadges(
                                    currentMeta.OriginalIconBytes, 256,
                                    model.BadgeRus, model.Badge60Fps, model.BadgeMod, model.BadgeAllDlc,
                                    model.CustomBadgeText, model.BadgePosition, outputPng: false);
                            }
                        }
                    }
                }
                catch { }

                byte[] nacpData = currentTargetNacp != null
                    ? (byte[])currentTargetNacp.Clone()
                    : (model.RawNacpBytes != null && model.RawNacpBytes.Length >= 0x4000
                        ? (byte[])model.RawNacpBytes.Clone()
                        : new byte[0x4000]);

                string origEng = Encoding.UTF8.GetString(nacpData, 0, 0x200).TrimEnd('\0');
                string origRus = Encoding.UTF8.GetString(nacpData, 11 * 0x300, 0x200).TrimEnd('\0');
                string origPub = Encoding.UTF8.GetString(nacpData, 0x200, 0x100).TrimEnd('\0');
                if (string.IsNullOrEmpty(origPub))
                {
                    for (int i = 0; i < 16; i++)
                    {
                        string sp = Encoding.UTF8.GetString(nacpData, i * 0x300 + 0x200, 0x100).TrimEnd('\0');
                        if (!string.IsNullOrEmpty(sp)) { origPub = sp; break; }
                    }
                }

                bool hasExistingNacp = currentTargetNacp != null || (model.RawNacpBytes != null && model.RawNacpBytes.Length >= 0x4000);

                // Обновляем только измененные языковые слоты (не затирая остальные 16 слотов)
                if (!string.IsNullOrEmpty(model.TitleNameEnglish) && (!hasExistingNacp || !string.Equals(model.TitleNameEnglish, origEng, StringComparison.Ordinal)))
                {
                    byte[] engBytes = Encoding.UTF8.GetBytes(model.TitleNameEnglish);
                    if (!hasExistingNacp)
                    {
                        for (int i = 0; i < 16; i++)
                        {
                            if (i == 11 && !string.IsNullOrEmpty(model.TitleNameRussian)) continue;
                            Array.Clear(nacpData, i * 0x300, 0x200);
                            Array.Copy(engBytes, 0, nacpData, i * 0x300, Math.Min(engBytes.Length, 0x1FF));
                        }
                    }
                    else
                    {
                        Array.Clear(nacpData, 0, 0x200);
                        Array.Copy(engBytes, 0, nacpData, 0, Math.Min(engBytes.Length, 0x1FF));
                        string brEng = Encoding.UTF8.GetString(nacpData, 1 * 0x300, 0x200).TrimEnd('\0');
                        if (string.IsNullOrEmpty(brEng) || string.Equals(brEng, origEng, StringComparison.Ordinal))
                        {
                            Array.Clear(nacpData, 1 * 0x300, 0x200);
                            Array.Copy(engBytes, 0, nacpData, 1 * 0x300, Math.Min(engBytes.Length, 0x1FF));
                        }
                    }
                }

                if (!string.IsNullOrEmpty(model.TitleNameRussian) && (!hasExistingNacp || !string.Equals(model.TitleNameRussian, origRus, StringComparison.Ordinal)))
                {
                    byte[] rusBytes = Encoding.UTF8.GetBytes(model.TitleNameRussian);
                    Array.Clear(nacpData, 11 * 0x300, 0x200);
                    Array.Copy(rusBytes, 0, nacpData, 11 * 0x300, Math.Min(rusBytes.Length, 0x1FF));
                }

                // Издатель обновляется независимо от изменения имени игры
                string? pubToWrite = !string.IsNullOrEmpty(model.Publisher) ? model.Publisher : (!hasExistingNacp ? "Nintendo" : null);
                if (!string.IsNullOrEmpty(pubToWrite) && (!hasExistingNacp || !string.Equals(pubToWrite, origPub, StringComparison.Ordinal)))
                {
                    byte[] pubBytes = Encoding.UTF8.GetBytes(pubToWrite);
                    for (int i = 0; i < 16; i++)
                    {
                        string slotTitle = Encoding.UTF8.GetString(nacpData, i * 0x300, 0x200).TrimEnd('\0');
                        string slotPub = Encoding.UTF8.GetString(nacpData, i * 0x300 + 0x200, 0x100).TrimEnd('\0');
                        if (!hasExistingNacp || i == 0 || i == 11 || !string.IsNullOrEmpty(slotTitle) || !string.IsNullOrEmpty(slotPub))
                        {
                            Array.Clear(nacpData, i * 0x300 + 0x200, 0x100);
                            Array.Copy(pubBytes, 0, nacpData, i * 0x300 + 0x200, Math.Min(pubBytes.Length, 0xFF));
                        }
                    }
                }

                File.WriteAllBytes(Path.Combine(romfsDir, "control.nacp"), nacpData);

                // 2. Подготовка иконки (256x256 JPEG со сжатием без метаданных EXIF)
                byte[]? iconBytes = model.CustomIconBytes ?? model.OriginalIconBytes;
                if (model.HasBadges && model.CustomIconBytes == null && iconBytes != null && iconBytes.Length > 0)
                {
                    iconBytes = IconBadgeStudioService.RenderIconWithBadges(
                        iconBytes, 256, model.BadgeRus, model.Badge60Fps, model.BadgeMod, model.BadgeAllDlc, model.CustomBadgeText, model.BadgePosition, outputPng: false);
                }
                if (iconBytes != null && iconBytes.Length > 0)
                {
                    iconBytes = SmartShrinkService.OptimizeSwitchIconJpeg(iconBytes);
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
                    tempDir,
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

                // 4. Поиск оригинального CNMT и Control NCA для TitleID и безопасная пересборка Meta NCA
                string? newMetaNca = null;
                string? newMetaNcaName = null;
                string? oldMetaNcaEntryName = null;
                string? oldControlNcaEntryName = null;

                if (File.Exists(targetNspPath))
                {
                    byte[]? oldCnmtBytes = null;
                    string? progPath = null;
                    string? progNcaEntryName = null;
                    string? manualPath = null;
                    string extractedProgNca = Path.Combine(tempDir, "extracted_prog.nca");
                    string extractedManualNca = Path.Combine(tempDir, "extracted_manual.nca");
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

                    // Сканируем целевой NSP/XCI для поиска оригинального CNMT и Control NCA для данного TitleId
                    using (var srcContainer = SwitchContainerHandle.Open(targetNspPath))
                    {
                        IFileSystem srcPfs = srcContainer.FileSystem;

                        foreach (var entry in srcPfs.EnumerateEntries())
                        {
                            if (entry.Type == DirectoryEntryType.Directory) continue;
                            string name = entry.Name;
                            bool isNca = name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase);
                            bool isNcz = name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase);
                            if (!isNca && !isNcz) continue;

                            try
                            {
                                using var ef = OpenFileSafe(srcPfs, "/" + name);
                                IStorage rawEfStorage = ef.AsStorage();
                                using var nczEfStorage = isNcz
                                    ? new Core.NSZ.StormNczStorage(rawEfStorage, null, null, App.Keys.CurrentKeyset)
                                    : null;
                                var nca = new Nca(App.Keys.CurrentKeyset, nczEfStorage ?? rawEfStorage);
                                string ncaTid = nca.Header.TitleId.ToString("X16");
                                // Строгое совпадение TitleID: Base (...000) и Patch (...800) имеют одинаковые
                                // первые 13 символов, поэтому сравнение по префиксу смешивало их CNMT/Control.
                                bool tidMatch = ncaTid.Equals(titleId, StringComparison.OrdinalIgnoreCase);

                                if (nca.Header.ContentType == NcaContentType.Meta && tidMatch)
                                {
                                    oldMetaNcaEntryName = name;
                                    try
                                    {
                                        IFileSystem? romfs = null;
                                        try { romfs = nca.OpenFileSystem(0, IntegrityCheckLevel.None); } catch { }
                                        if (romfs == null)
                                        {
                                            try { romfs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None); } catch { }
                                        }

                                        if (romfs != null)
                                        {
                                            using var romfsDisp = romfs as IDisposable;
                                            foreach (var f in romfs.EnumerateEntries())
                                            {
                                                if (f.Name.EndsWith(".cnmt", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    using var cf = OpenFileSafe(romfs, f.FullPath);
                                                    using var ms = new MemoryStream();
                                                    cf.AsStream().CopyTo(ms);
                                                    oldCnmtBytes = ms.ToArray();
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                    catch { }
                                }
                                else if (nca.Header.ContentType == NcaContentType.Control && tidMatch)
                                {
                                    oldControlNcaEntryName = name;
                                }
                                else if (nca.Header.ContentType == NcaContentType.Program && progNcaEntryName == null)
                                {
                                    // Сохраняем имя записи Program NCA для ленивого извлечения только в случае необходимости
                                    progNcaEntryName = name;
                                }
                            }
                            catch { }
                        }
                    }

                    // 4.1 Быстрое и безопасное обновление CNMT с сохранением всех под-программ, DLC и разделов
                    if (oldCnmtBytes != null)
                    {
                        try
                        {
                            using var ms = new MemoryStream(oldCnmtBytes);
                            var cnmt = new LibHac.Tools.Ncm.Cnmt(ms);
                            int contentOffset = 0x20 + cnmt.TableOffset;

                            byte[] newControlSha256 = SHA256.HashData(File.ReadAllBytes(newControlNca));
                            byte[] newControlNcaId = newControlSha256.Take(16).ToArray();
                            long newControlSize = new FileInfo(newControlNca).Length;

                            string? oldControlNcaIdHex = oldControlNcaEntryName != null && oldControlNcaEntryName.Length >= 32
                                ? oldControlNcaEntryName.Substring(0, 32)
                                : null;

                            bool entryPatched = false;
                            for (int i = 0; i < cnmt.ContentEntries.Length; i++)
                            {
                                var ce = cnmt.ContentEntries[i];
                                if (ce.Type == LibHac.Ncm.ContentType.Control)
                                {
                                    string ceNcaIdHex = Convert.ToHexString(ce.NcaId);
                                    if (oldControlNcaIdHex == null || ceNcaIdHex.Equals(oldControlNcaIdHex, StringComparison.OrdinalIgnoreCase))
                                    {
                                        int entryOffset = contentOffset + i * 0x38;
                                        // Hash (32 bytes)
                                        Array.Copy(newControlSha256, 0, oldCnmtBytes, entryOffset, 32);
                                        // NcaId (16 bytes)
                                        Array.Copy(newControlNcaId, 0, oldCnmtBytes, entryOffset + 32, 16);
                                        // Size (6 bytes)
                                        byte[] sizeBytes = BitConverter.GetBytes(newControlSize);
                                        Array.Copy(sizeBytes, 0, oldCnmtBytes, entryOffset + 48, 6);
                                        entryPatched = true;
                                        break;
                                    }
                                }
                            }

                            if (entryPatched)
                            {
                                string updatedCnmtPath = Path.Combine(tempDir, "updated.cnmt");
                                File.WriteAllBytes(updatedCnmtPath, oldCnmtBytes);

                                string titleTypeStr = "application";
                                if (cnmt.Type == LibHac.Ncm.ContentMetaType.AddOnContent) titleTypeStr = "addon";
                                else if (cnmt.Type == LibHac.Ncm.ContentMetaType.SystemProgram) titleTypeStr = "systemprogram";
                                else if (cnmt.Type == LibHac.Ncm.ContentMetaType.SystemData) titleTypeStr = "systemdata";

                                string metaArgs = $"-k \"{keysFile}\" --type nca --ncatype meta --titleid {titleId} --titletype {titleTypeStr} --titleversion 0x{cnmt.TitleVersion.Version:x} --cnmt \"{updatedCnmtPath}\" -o \"{outNcaDir}\"";
                                int metaCode = await ExternalProcessRunner.RunAsync(
                                    _hacpackExe,
                                    metaArgs,
                                    tempDir,
                                    task,
                                    ct
                                );

                                if (metaCode == 0)
                                {
                                    var metaNcas = Directory.GetFiles(outNcaDir, "*.cnmt.nca");
                                    if (metaNcas.Length > 0)
                                    {
                                        bool isValid = false;
                                        try
                                        {
                                            using var checkFs = new FileStream(metaNcas[0], FileMode.Open, FileAccess.Read, FileShare.Read);
                                            var checkNca = new Nca(App.Keys.CurrentKeyset, checkFs.AsStorage());
                                            if (checkNca.CanOpenSection(0))
                                            {
                                                var checkSection0 = checkNca.OpenFileSystem(0, IntegrityCheckLevel.None);
                                                if (checkSection0.EnumerateEntries("/", "*").Any())
                                                {
                                                    isValid = true;
                                                }
                                            }
                                        }
                                        catch { }

                                        if (isValid)
                                        {
                                            newMetaNca = metaNcas[0];
                                            newMetaNcaName = Path.GetFileName(newMetaNca);
                                        }
                                        else
                                        {
                                            App.Logger.Log($"[ControlEditor] Предупреждение: сгенерированный Meta NCA пуст! Откат к оригинальному Meta NCA.", LogLevel.Warning);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            App.Logger.Log($"[ControlEditor] Ошибка прямого обновления CNMT: {ex.Message}", LogLevel.Warning);
                        }
                    }

                    // Резервный (fallback) путь через hacpack --programnca (если прямой CNMT не был найден)
                    if (newMetaNca == null && progNcaEntryName != null)
                    {
                        try
                        {
                            using var srcContainer = SwitchContainerHandle.Open(targetNspPath);
                            using var ef = OpenFileSafe(srcContainer.FileSystem, "/" + progNcaEntryName);
                            using var ps = ef.AsStream();
                            using var fs = new FileStream(extractedProgNca, FileMode.Create, FileAccess.Write);
                            ps.CopyTo(fs);
                            progPath = extractedProgNca;
                        }
                        catch { }
                    }

                    if (newMetaNca == null && progPath != null && File.Exists(progPath))
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
                            tempDir,
                            task,
                            ct
                        );

                        if (metaCode == 0)
                        {
                            var metaNcas = Directory.GetFiles(outNcaDir, "*.cnmt.nca");
                            if (metaNcas.Length > 0)
                            {
                                bool isValid = false;
                                try
                                {
                                    using var checkFs = new FileStream(metaNcas[0], FileMode.Open, FileAccess.Read, FileShare.Read);
                                    var checkNca = new Nca(App.Keys.CurrentKeyset, checkFs.AsStorage());
                                    if (checkNca.CanOpenSection(0))
                                    {
                                        var checkSection0 = checkNca.OpenFileSystem(0, IntegrityCheckLevel.None);
                                        if (checkSection0.EnumerateEntries("/", "*").Any())
                                        {
                                            isValid = true;
                                        }
                                    }
                                }
                                catch { }

                                if (isValid)
                                {
                                    newMetaNca = metaNcas[0];
                                    newMetaNcaName = Path.GetFileName(newMetaNca);
                                }
                            }
                        }
                    }

                // 5. Замена Control NCA и Meta NCA в targetNspPath (NSP или XCI) с помощью LibHac PartitionFileSystemBuilder
                await Task.Run(async () =>
                {
                    bool isTargetXci = targetNspPath.EndsWith(".xci", StringComparison.OrdinalIgnoreCase);
                    string tempPatchedNsp = Path.Combine(tempDir, "patched_target.nsp");
                    var pfsBuilder = new PartitionFileSystemBuilder();

                    using (var srcContainer = SwitchContainerHandle.Open(targetNspPath))
                    {
                        IFileSystem srcPfs = srcContainer.FileSystem;
                        var openedFiles = new List<IFile>();
                        var openedStreams = new List<FileStream>();
                        var entryDict = new Dictionary<string, IStorage>(StringComparer.OrdinalIgnoreCase);

                        try
                        {
                            foreach (var entry in srcPfs.EnumerateEntries())
                            {
                                if (entry.Type == DirectoryEntryType.Directory) continue;
                                string name = entry.Name;

                                // Исключаем СТРОГО Control NCA и Meta NCA для редактируемого TitleId.
                                // ВСЕ DLC, обновления и Control NCA других под-программ СОХРАНЯЮТСЯ в целости!
                                bool isOldControl = false;
                                bool isOldMeta = false;

                                if (oldControlNcaEntryName != null && name.Equals(oldControlNcaEntryName, StringComparison.OrdinalIgnoreCase))
                                {
                                    isOldControl = true;
                                }
                                else if (oldMetaNcaEntryName != null && name.Equals(oldMetaNcaEntryName, StringComparison.OrdinalIgnoreCase))
                                {
                                    if (newMetaNca != null) isOldMeta = true;
                                }
                                else if (name.EndsWith(".cnmt.xml", StringComparison.OrdinalIgnoreCase) && name.Contains(titleId, StringComparison.OrdinalIgnoreCase))
                                {
                                    isOldMeta = true;
                                }

                                if (isOldControl || isOldMeta) continue;

                                var oldFile = OpenFileSafe(srcPfs, "/" + name);
                                openedFiles.Add(oldFile);
                                entryDict[name] = oldFile.AsStorage();
                            }

                            if (newMetaNca != null && newMetaNcaName != null)
                            {
                                var metaStream = new FileStream(newMetaNca, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                                openedStreams.Add(metaStream);
                                entryDict[newMetaNcaName] = metaStream.AsStorage();
                            }

                            var controlStream = new FileStream(newControlNca, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
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
                                if (lower.Contains("program") || lower.EndsWith(".ncz")) return 2;
                                if (lower.Contains("manual")) return 3;
                                return 5;
                            }).ThenBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);

                            foreach (var kvp in ordered)
                            {
                                pfsBuilder.AddFile(kvp.Key, new StorageFile(new SafeStorageWrapper(kvp.Value), OpenMode.Read));
                            }

                            using (var builtPfs = pfsBuilder.Build(PartitionFileSystemType.Standard))
                            {
                                builtPfs.GetSize(out long totalSize).ThrowIfFailure();

                                using (var destStream = new FileStream(tempPatchedNsp, FileMode.Create, FileAccess.Write, FileShare.None, 32 * 1024 * 1024, FileOptions.SequentialScan))
                                {
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
                            }
                        }
                        finally
                        {
                            foreach (var f in openedFiles) try { f.Dispose(); } catch { }
                            foreach (var s in openedStreams) try { s.Dispose(); } catch { }
                        }
                    }

                    string finalPatchedFile = tempPatchedNsp;
                    if (isTargetXci)
                    {
                        string tempPatchedXci = Path.Combine(tempDir, "patched_target.xci");
                        Core.NCA.StormXciBuilder.BuildXciFromPfs0(tempPatchedNsp, tempPatchedXci, App.Keys.CurrentKeyset);
                        finalPatchedFile = tempPatchedXci;
                    }

                    await SafeFileOperations.SafeMoveOrReplaceFileAsync(finalPatchedFile, targetNspPath, task, ct);
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
                TempCleanupService.ForceDeleteDirectory(tempDir);
            }
        }
    }
}
