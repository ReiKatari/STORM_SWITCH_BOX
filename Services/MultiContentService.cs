using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LibHac;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using StormSwitchBox.Models;
using Path = System.IO.Path;

namespace StormSwitchBox.Services
{
    public class MultiContentService
    {
        private readonly KeysService _keysService;

        public MultiContentService(KeysService keysService)
        {
            _keysService = keysService;
        }

        public async Task BuildMultiContentAsync(ProcessingTask task, List<string> inputFiles, string outPath, bool patchFirmware, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(task.TargetFormat))
            {
                task.TargetFormat = task.Is3dsTask ? "3DS" : "NSP";
            }
            string intermediatePath = outPath;
            bool compressionSuccess = false;
            bool isCompressedFormat = string.Equals(task.TargetFormat, "NSZ", StringComparison.OrdinalIgnoreCase) || string.Equals(task.TargetFormat, "XCZ", StringComparison.OrdinalIgnoreCase);
            if (isCompressedFormat)
            {
                string intermediateExt = string.Equals(task.TargetFormat, "XCZ", StringComparison.OrdinalIgnoreCase) ? ".xci" : ".nsp";
                intermediatePath = System.IO.Path.ChangeExtension(outPath, intermediateExt);
                if (intermediatePath.Equals(outPath, StringComparison.OrdinalIgnoreCase))
                {
                    intermediatePath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(outPath) ?? string.Empty, System.IO.Path.GetFileNameWithoutExtension(outPath) + "_temp" + intermediateExt);
                }
            }
            
            string tempDecompDir = string.Empty;

            try
            {
                App.RunOnUI(() =>
                {
                    task.Status = "Анализ файлов...";
                    task.IsRunning = true;
                    task.Progress = 0;
                    task.LogDetails += $"\n📋 [Настройки] Файлов: {inputFiles.Count} | HardPatch: {(patchFirmware ? "Да" : "Нет")}";
                });

                if (!_keysService.IsLoaded) throw new Exception("Отсутствуют криптографические ключи (prod.keys). Пожалуйста, выберите их в параметрах.");


                // Анализ файлов
                foreach (var f in inputFiles)
                {
                    // var info = App.SwitchFormat.ParseNsp(f);
                }

                // Если это сборка 1G+1U или 1G+1U+1M, мы делегируем сборку напрямую в NSC_Builder.
                // Для 1G+1U+1M сюда приходит только один файл: prepatch.nsp (который уже содержит слитые данные базы, патча и мода).
                // Это избегает создания кривого "сырого" PFS0 и позволяет squirrel.exe правильно слить CNMT или пропатчить версию.
                string targetDir = System.IO.Path.GetDirectoryName(outPath) ?? string.Empty;
                if (!string.IsNullOrEmpty(targetDir) && !System.IO.Directory.Exists(targetDir))
                    System.IO.Directory.CreateDirectory(targetDir);

                // Параллельная декомпрессия NSZ/XCZ (Pipeline Parallelism)
                App.RunOnUI(() => task.LogDetails += "\n🟣 [Декомпрессия] Распаковка NSZ/XCZ...");
                
                var finalInputFiles = new System.Collections.Concurrent.ConcurrentBag<string>();
                string targetDrive = System.IO.Path.GetPathRoot(targetDir) ?? "C:\\";
                // Всегда размещаем временный каталог близко к корню целевого диска для гарантированной защиты от лимита MAX_PATH (260 символов)
                tempDecompDir = System.IO.Path.Combine(targetDrive, "STORM_TMP", "SD_" + Guid.NewGuid().ToString("N").Substring(0, 6));
                Directory.CreateDirectory(tempDecompDir);
                TempCleanupService.RegisterActiveTempDirectory(tempDecompDir);

                var decompTasks = inputFiles.Select(async f =>
                {
                    if (f.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase))
                    {
                        string? decompResult = await App.NszCompression.DecompressNszAsync(task, f, tempDecompDir, cancellationToken);
                        
                        if (!string.IsNullOrEmpty(decompResult) && File.Exists(decompResult))
                        {
                            finalInputFiles.Add(PrepareSafeFileForTemp(decompResult, tempDecompDir));
                        }
                        else
                        {
                            throw new Exception($"Нативная декомпрессия файла {System.IO.Path.GetFileName(f)} завершилась с ошибкой.");
                        }
                    }
                    else
                    {
                        finalInputFiles.Add(PrepareSafeFileForTemp(f, tempDecompDir));
                    }
                });

                await Task.WhenAll(decompTasks);
                var finalInputFilesList = finalInputFiles.ToList();

                string formattedOut = FormatOutputFileName(outPath, inputFiles);
                if (!string.IsNullOrEmpty(formattedOut))
                {
                    outPath = formattedOut;
                    task.OutputFileName = System.IO.Path.GetFileNameWithoutExtension(outPath);
                }

                string listFile = System.IO.Path.Combine(tempDecompDir, $"list_conv_{Guid.NewGuid().ToString("N").Substring(0, 8)}.txt");
                System.IO.File.WriteAllLines(listFile, finalInputFilesList, new System.Text.UTF8Encoding(false));

                bool hasMods = finalInputFilesList.Any(d => Directory.Exists(d) && 
                    (System.IO.Path.GetFileName(d).Equals("romfs", StringComparison.OrdinalIgnoreCase) || 
                     System.IO.Path.GetFileName(d).Equals("exefs", StringComparison.OrdinalIgnoreCase) ||
                     System.IO.Path.GetFileName(d).Equals("exefs_patches", StringComparison.OrdinalIgnoreCase) ||
                     System.IO.Path.GetFileName(d).Equals("cheats", StringComparison.OrdinalIgnoreCase) ||
                     d.Contains("romfs", StringComparison.OrdinalIgnoreCase) || 
                     d.Contains("exefs", StringComparison.OrdinalIgnoreCase) ||
                     d.Contains("cheat", StringComparison.OrdinalIgnoreCase) ||
                     d.Contains("чит", StringComparison.OrdinalIgnoreCase) ||
                     d.Contains("mod", StringComparison.OrdinalIgnoreCase) ||
                     d.Contains("мод", StringComparison.OrdinalIgnoreCase)));

                string? savedBaseFile = null;
                string? savedUpdateFile = null;
                bool hasPatchedBase = false;

                // 4. Поиск Base и Update и умный анализ метода сборки (Smart Processing)
                string? baseFile = null;
                string? updateFile = null;
                string titleIdStr = "";
                
                foreach (var f in finalInputFilesList)
                {
                    if (Directory.Exists(f)) continue;
                    string tid = "";
                    try 
                    {
                        var info = App.SwitchFormat.ParseNsp(f);
                        if (info.ContentType == "Application") baseFile = f;
                        else if (info.ContentType == "Patch") updateFile = f;
                        tid = (info.TitleId ?? "").Trim().ToUpperInvariant();
                    }
                    catch { }

                    if (string.IsNullOrEmpty(tid))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(f, @"\[([0-9A-Fa-f]{16})\]");
                        if (match.Success) tid = match.Groups[1].Value.ToUpperInvariant();
                    }

                    if (!string.IsNullOrEmpty(tid) && tid.Length == 16)
                    {
                        if (tid.EndsWith("000"))
                        {
                            if (string.IsNullOrEmpty(baseFile)) baseFile = f;
                            if (string.IsNullOrEmpty(titleIdStr)) titleIdStr = tid;
                        }
                        else if (tid.EndsWith("800"))
                        {
                            if (string.IsNullOrEmpty(updateFile)) updateFile = f;
                            if (string.IsNullOrEmpty(titleIdStr)) titleIdStr = tid.Substring(0, 13) + "000";
                        }
                    }
                }
                
                if (string.IsNullOrEmpty(baseFile)) baseFile = finalInputFilesList.FirstOrDefault(f => !Directory.Exists(f) && !f.Contains("DLC", StringComparison.OrdinalIgnoreCase) && (f.Contains("[v0]") || f.Contains("v0"))) ?? finalInputFilesList.FirstOrDefault(f => !Directory.Exists(f) && !f.Contains("DLC", StringComparison.OrdinalIgnoreCase) && !f.Contains("v")) ?? "";
                if (string.IsNullOrEmpty(updateFile)) updateFile = finalInputFilesList.FirstOrDefault(f => !Directory.Exists(f) && f != baseFile && !f.Contains("DLC", StringComparison.OrdinalIgnoreCase) && (f.Contains("v") && !f.Contains("v0"))) ?? "";

                long baseSize = (!string.IsNullOrEmpty(baseFile) && File.Exists(baseFile)) ? new FileInfo(baseFile).Length : 0;
                long updateSize = (!string.IsNullOrEmpty(updateFile) && File.Exists(updateFile)) ? new FileInfo(updateFile).Length : 0;

                int effectiveBuildMode = task.BuildMode != 0 ? task.BuildMode : App.Settings.Current.MultiContentBuildMode;
                bool forceHardPatch = (effectiveBuildMode == 1);
                bool skipHardPatch = (effectiveBuildMode == 2) || task.IsMultiProgramTitle;

                if (!skipHardPatch)
                {
                    if (!string.IsNullOrEmpty(baseFile) && (!string.IsNullOrEmpty(updateFile) || hasMods))
                    {
                        savedBaseFile = baseFile;
                        savedUpdateFile = updateFile;

                        App.RunOnUI(() => task.LogDetails += "\n🔵 [HardPatch] Умная монолитная пересборка RomFS (объединение базы, обновлений и модов)...");
                        if (string.IsNullOrEmpty(titleIdStr))
                        {
                            try {
                                titleIdStr = App.SwitchFormat.ParseNsp(baseFile).TitleId;
                            } catch { }
                            if (string.IsNullOrEmpty(titleIdStr)) {
                                var match = System.Text.RegularExpressions.Regex.Match(baseFile, @"\[([0-9A-Fa-f]{16})\]");
                                if (match.Success) titleIdStr = match.Groups[1].Value;
                            }
                        }

                        // Извлечение токенов разблокировки из Unlocker DLC для прямой интеграции в RomFS игры
                        var unlockerRomfsDirs = ExtractUnlockerRomFsDirectories(finalInputFilesList, tempDecompDir, titleIdStr, task, cancellationToken);
                        if (unlockerRomfsDirs.Count > 0)
                        {
                            hasMods = true;
                        }

                        string suffix = string.IsNullOrEmpty(titleIdStr) ? "" : $"_[{titleIdStr}][v0]";
                        string tempHardPatchedNsp = System.IO.Path.Combine(tempDecompDir, $"patched_base{suffix}.nsp");
                        
                        var hpInput = new List<string> { baseFile };
                        if (!string.IsNullOrEmpty(updateFile)) hpInput.Add(updateFile);
                        
                        // Add mod directories (romfs/exefs) and extracted unlocker directories to be processed
                        var modDirs = finalInputFilesList.Where(d => Directory.Exists(d)).ToList();
                        hpInput.AddRange(modDirs);
                        hpInput.AddRange(unlockerRomfsDirs);

                        bool hardPatchSuccess = false;
                        try
                        {
                            await App.HardPatch.PatchUpdateAsync(task, hpInput, tempHardPatchedNsp, cancellationToken, isMultiContent: true);
                            
                            if (System.IO.File.Exists(tempHardPatchedNsp))
                            {
                                long patchedSize = new FileInfo(tempHardPatchedNsp).Length;
                                long minValidSize = Math.Min(25L * 1024 * 1024, (long)(baseSize * 0.2));
                                // Валидация: если исходная база > 50 МБ, а результат хардпатча меньше минимального порога (20% базы или < 25 МБ) — это поврежденный огрызок без RomFS!
                                if (baseSize > 50 * 1024 * 1024 && patchedSize < minValidSize)
                                {
                                    App.Logger.Log($"[HardPatch] Размер пересобранного файла ({patchedSize} байт) аномально мал относительно базы ({baseSize} байт). Откат к нативной сборке.", Models.LogLevel.Warning);
                                    App.RunOnUI(() => task.LogDetails += $"\n⚠️ [HardPatch] Размер пересобранного файла ({Models.ProcessingTask.FormatSize(patchedSize)}) аномально мал относительно базы ({Models.ProcessingTask.FormatSize(baseSize)}). Откат к нативному сшиванию мультиконтента...");
                                    try { System.IO.File.Delete(tempHardPatchedNsp); } catch { }
                                }
                                else if (patchedSize > 0)
                                {
                                    hardPatchSuccess = true;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            App.Logger.Log($"[HardPatch] Исключение монолитного слияния: {ex.Message}. Откат к нативному сшиванию мультиконтента.", Models.LogLevel.Warning);
                            App.RunOnUI(() => task.LogDetails += $"\n⚠️ [HardPatch] Монолитное слияние невозможно ({ex.Message}). Выполняется откат к прямому сшиванию оригинальных разделов...");
                            try { if (System.IO.File.Exists(tempHardPatchedNsp)) System.IO.File.Delete(tempHardPatchedNsp); } catch { }
                        }

                        if (hardPatchSuccess && System.IO.File.Exists(tempHardPatchedNsp))
                        {
                            if (task.IsMultiProgramTitle)
                            {
                                // Мульти-программный сборник (напр. AC Ezio Collection с несколькими независимыми Application TitleID)
                                App.RunOnUI(() => task.LogDetails += $"\n⚠️ [HardPatch] Мульти-программный сборник. Используем оригинальные разделы для сохранения всех под-игр.");
                                App.Logger.Log($"[HardPatch] Multi-program title detected. Discarding patched_base, using originals.", Models.LogLevel.Warning);
                                try { System.IO.File.Delete(tempHardPatchedNsp); } catch { }
                            }
                            else
                            {
                                // Обычная игра — пересобранная база полностью заменяет базу и обновление, исключая дубликаты
                                hasPatchedBase = true;
                                finalInputFilesList.Remove(baseFile);
                                if (!string.IsNullOrEmpty(updateFile)) finalInputFilesList.Remove(updateFile);
                                foreach (var mod in modDirs)
                                {
                                    finalInputFilesList.Remove(mod);
                                }
                                finalInputFilesList.Add(tempHardPatchedNsp);
                                App.RunOnUI(() => task.LogDetails += "\n🔵 [HardPatch] Физическая пересборка успешно завершена. Ресурсы обновлены, дублирование исключено.");

                                bool isTargetXciLocal = string.Equals(task.TargetFormat, "XCI", StringComparison.OrdinalIgnoreCase) || string.Equals(task.TargetFormat, "XCZ", StringComparison.OrdinalIgnoreCase);
                                if (finalInputFilesList.Count == 1 && !isTargetXciLocal && !isCompressedFormat)
                                {
                                    outPath = await SafeFileOperations.SafeMoveOrReplaceFileAsync(tempHardPatchedNsp, outPath, task, cancellationToken);
                                    App.RunOnUI(() =>
                                    {
                                        if (System.IO.File.Exists(outPath))
                                        {
                                            long outSize = new System.IO.FileInfo(outPath).Length;
                                            task.TargetSize = Models.ProcessingTask.FormatSize(outSize);
                                            if (task.SourceSizeBytes > 0)
                                            {
                                                long diff = task.SourceSizeBytes - outSize;
                                                double percent = (double)diff / task.SourceSizeBytes * 100.0;
                                                task.SizeDifference = $"{(diff > 0 ? "-" : "+")}{Models.ProcessingTask.FormatSize(Math.Abs(diff))} ({Math.Abs(percent):F1}%)";
                                            }
                                        }
                                        task.Progress = 100;
                                        task.Status = "Успешно";
                                        task.IsRunning = false;
                                        task.LogDetails += "\n✅ [Успех] Монолитный образ игры (Base + Update + ExeFS) успешно собран и готов к запуску!";
                                        StormSwitchBox.Services.HistoryService.AddToHistory(task);
                                    });
                                    DeployCheatsIfPresent(titleIdStr, inputFiles, outPath);
                                    App.Logger.Log($"Мульти-контент успешно создан: {System.IO.Path.GetFileName(outPath)}", LogLevel.Success);
                                    return;
                                }
                            }
                        }
                        else 
                        {
                            App.RunOnUI(() => task.LogDetails += "\nℹ️ Пересборка HardPatch пропущена. Переходим к сшиванию мультиконтента...");
                        }
                    }
                }
                else if (skipHardPatch)
                {
                    App.RunOnUI(() => task.LogDetails += "\n⚠️ [HardPatch] Мульти-программный тайтл — пропуск yanu-cli, используем оригинальные файлы.");
                    App.Logger.Log("[HardPatch] Skipped: multi-program title detected by pre-analysis", LogLevel.Info);
                }

                // 4.5 Сшивание мультиконтента через нативный движок LibHac PFS0
                App.RunOnUI(() =>
                {
                    task.LogDetails += "\n📦 [NSC_Builder] Сшивание мультиконтента...";
                    task.Status = "Сборка...";
                });

                bool isTargetXci = string.Equals(task.TargetFormat, "XCI", StringComparison.OrdinalIgnoreCase) || string.Equals(task.TargetFormat, "XCZ", StringComparison.OrdinalIgnoreCase);
                
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string toolsDir = System.IO.Path.Combine(appDir, "tools");
                if (!System.IO.Directory.Exists(toolsDir))
                {
                    string parentTools = System.IO.Path.Combine(appDir, "..", "tools");
                    if (System.IO.Directory.Exists(parentTools))
                    {
                        toolsDir = parentTools;
                    }
                }

                App.EnsureUserKeysAvailable();

                var sortedList = new List<string>();
                string? mainApp = null;
                string? patchApp = null;
                var dlcs = new List<string>();

                foreach (var f in finalInputFilesList)
                {
                    if (Directory.Exists(f)) continue;

                    bool isBase = false;
                    bool isPatch = false;
                    bool isDlc = false;

                    try
                    {
                        var info = App.SwitchFormat.ParseNsp(f);
                        if (info.ContentType == "Application") isBase = true;
                        else if (info.ContentType == "Patch") isPatch = true;
                        else if (info.ContentType == "AddOnContent") isDlc = true;
                    } 
                    catch { }

                    if (!isBase && !isPatch && !isDlc)
                    {
                        string tid = "";
                        var match = System.Text.RegularExpressions.Regex.Match(f, @"\[([0-9A-Fa-f]{16})\]");
                        if (match.Success) tid = match.Groups[1].Value.ToUpperInvariant();

                        if (!string.IsNullOrEmpty(tid) && tid.Length == 16)
                        {
                            if (tid.EndsWith("000")) isBase = true;
                            else if (tid.EndsWith("800")) isPatch = true;
                            else isDlc = true;
                        }
                        else
                        {
                            if (f.Contains("DLC", StringComparison.OrdinalIgnoreCase) || f.Contains("AddOn", StringComparison.OrdinalIgnoreCase)) isDlc = true;
                            else if (f.Contains("[v0]") || f.EndsWith("v0.nsp", StringComparison.OrdinalIgnoreCase) || f.Contains("patched_base")) isBase = true;
                            else if (f.Contains("v") && !f.Contains("v0")) isPatch = true;
                        }
                    }

                    if (isDlc) dlcs.Add(f);
                    else if (isBase && mainApp == null) mainApp = f;
                    else if (isPatch && patchApp == null) patchApp = f;
                    else dlcs.Add(f);
                }

                if (!string.IsNullOrEmpty(mainApp)) sortedList.Add(mainApp);
                if (!string.IsNullOrEmpty(patchApp)) sortedList.Add(patchApp);
                sortedList.AddRange(dlcs);

                if (sortedList.Count == 0) sortedList = finalInputFilesList.Where(f => !Directory.Exists(f)).ToList();

                string outFolder = System.IO.Path.Combine(tempDecompDir, "libhac_out");
                Directory.CreateDirectory(outFolder);

                bool buildDone = false;
                
                App.RunOnUI(() => task.LogDetails += "\n📦 [LibHac] Нативная сборка Multi-NSP (PFS0)...");

                try
                {
                    var pfsBuilder = new PartitionFileSystemBuilder();
                    var mergedEntries = new Dictionary<string, LibHac.Fs.Fsa.IFile>(StringComparer.OrdinalIgnoreCase);
                    var openedFs = new List<PartitionFileSystem>();
                    var openedStreams = new List<FileStream>();
                    var openedFiles = new List<LibHac.Fs.Fsa.IFile>();

                    try
                    {
                        var scanList = new List<string>();
                        if (!string.IsNullOrEmpty(mainApp) && System.IO.File.Exists(mainApp)) scanList.Add(mainApp);
                        foreach (var f in sortedList)
                        {
                            if (!scanList.Contains(f, StringComparer.OrdinalIgnoreCase) && System.IO.File.Exists(f))
                                scanList.Add(f);
                        }
                        foreach (var f in finalInputFilesList)
                        {
                            if (!System.IO.Directory.Exists(f) && !scanList.Contains(f, StringComparer.OrdinalIgnoreCase) && System.IO.File.Exists(f))
                                scanList.Add(f);
                        }

                        // Если целевой формат несжатый NSP/XCI, а часть файлов — NSZ/XCZ/XCI, предварительно распаковываем их
                        var processedScanList = new List<string>();
                        for (int i = 0; i < scanList.Count; i++)
                        {
                            string fPath = scanList[i];
                            string ext = System.IO.Path.GetExtension(fPath).ToLowerInvariant();

                            if (!isCompressedFormat && (ext == ".nsz" || ext == ".xcz" || ext == ".xci"))
                            {
                                string nspName = $"src_{i}_{System.IO.Path.GetFileNameWithoutExtension(fPath)}.nsp";
                                string targetNspPath = System.IO.Path.Combine(tempDecompDir, nspName);
                                string itemDecompDir = System.IO.Path.Combine(tempDecompDir, $"decomp_{i}");
                                Directory.CreateDirectory(itemDecompDir);

                                App.RunOnUI(() => task.LogDetails += $"\n📦 [LibHac] Распаковка {System.IO.Path.GetFileName(fPath)} -> {nspName}...");

                                string? decompResult = await App.NszCompression.DecompressNszAsync(task, fPath, itemDecompDir, cancellationToken);
                                if (decompResult != null)
                                {
                                    var producedNsp = new DirectoryInfo(itemDecompDir).GetFiles("*.nsp")
                                        .OrderByDescending(f => f.Length)
                                        .FirstOrDefault();

                                    if (producedNsp != null)
                                    {
                                        targetNspPath = await SafeFileOperations.SafeMoveOrReplaceFileAsync(producedNsp.FullName, targetNspPath, task, cancellationToken);
                                        try { Directory.Delete(itemDecompDir, true); } catch { }
                                        processedScanList.Add(targetNspPath);
                                        continue;
                                    }
                                }
                                try { Directory.Delete(itemDecompDir, true); } catch { }
                            }

                            processedScanList.Add(fPath);
                        }

                        var baseEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        var deltaNcaNamesToExclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        if (App.Settings.Current.RemoveDeltaNca)
                        {
                            var allScanFiles = processedScanList.Concat(inputFiles).Distinct().ToList();
                            deltaNcaNamesToExclude = DetectDeltaNcasToExclude(allScanFiles);
                        }

                        // 1. Сканируем основные файлы сборки (Base/Patched Base + DLCs + Unlockers)
                        for (int scanIdx = 0; scanIdx < processedScanList.Count; scanIdx++)
                        {
                            string nspPath = processedScanList[scanIdx];
                            if (!System.IO.File.Exists(nspPath)) continue;
                            
                            var stream = new FileStream(nspPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                            openedStreams.Add(stream);
                            var fs = new PartitionFileSystem(stream.AsStorage());
                            openedFs.Add(fs);

                            bool isMainGame = (scanIdx == 0);
                            
                            foreach (var entry in fs.EnumerateEntries())
                            {
                                if (entry.Type == LibHac.Fs.DirectoryEntryType.Directory) continue;
                                string name = entry.Name;
                                
                                if (isMainGame)
                                {
                                    baseEntries.Add(name);
                                }

                                if (App.Settings.Current.RemoveDeltaNca && deltaNcaNamesToExclude.Contains(name))
                                {
                                    App.Logger.Log($"[Delta Cleaner] Пропущен мусорный Delta NCA: {name} (экономия места)", Models.LogLevel.Info);
                                    App.RunOnUI(() => task.LogDetails += $"\n🗑️ [Delta Cleaner] Удален мусорный Delta NCA: {name}");
                                    continue;
                                }

                                if (mergedEntries.ContainsKey(name) || !IsValidNspEntry(name)) continue;

                                var file = OpenFileSafe(fs, entry.FullPath);
                                openedFiles.Add(file);
                                mergedEntries[name] = file;
                            }
                        }

                        // 2. Сохраняем тикеты (.tik) и сертификаты (.cert) из оригинальных файлов.
                        // Если была выполнена пересборка HardPatch (hasPatchedBase == true), то Patch CNMT НЕ внедряется,
                        // так как обновление уже физически вшито в единый Program NCA пересобранной базы.
                        var extraSources = new List<string>();
                        if (!string.IsNullOrEmpty(savedUpdateFile) && File.Exists(savedUpdateFile)) extraSources.Add(savedUpdateFile);
                        if (!string.IsNullOrEmpty(savedBaseFile) && File.Exists(savedBaseFile)) extraSources.Add(savedBaseFile);

                        foreach (var extraPath in extraSources)
                        {
                            try
                            {
                                var stream = new FileStream(extraPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                                openedStreams.Add(stream);
                                var fs = new PartitionFileSystem(stream.AsStorage());
                                openedFs.Add(fs);

                                foreach (var entry in fs.EnumerateEntries())
                                {
                                    if (entry.Type == LibHac.Fs.DirectoryEntryType.Directory) continue;
                                    string name = entry.Name;
                                    string lower = name.ToLowerInvariant();

                                    // Извлекаем тикеты (.tik), сертификаты (.cert) и Patch CNMT (.cnmt.nca)
                                    bool isTicketOrCert = lower.EndsWith(".tik") || lower.EndsWith(".cert");
                                    bool isPatchCnmt = !hasPatchedBase && (lower.EndsWith(".cnmt.nca") || lower.EndsWith(".cnmt.xml"));

                                    if (isTicketOrCert || isPatchCnmt)
                                    {
                                        if (!mergedEntries.ContainsKey(name))
                                        {
                                            var file = OpenFileSafe(fs, entry.FullPath);
                                            openedFiles.Add(file);
                                            mergedEntries[name] = file;
                                            if (isTicketOrCert)
                                            {
                                                App.Logger.Log($"[LibHac] Вшит тикет/сертификат Unlocker: {name}", Models.LogLevel.Info);
                                            }
                                            else if (isPatchCnmt)
                                            {
                                                App.Logger.Log($"[LibHac] Вшит Update Patch CNMT: {name}", Models.LogLevel.Info);
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }
                        }

                        // Strictly order entries so Base CNMT (0), Control/Icon NCA (1), Program NCA (2) are FIRST
                        ulong baseTitleId = 0;
                        if (!string.IsNullOrEmpty(mainApp))
                        {
                            try
                            {
                                var info = App.SwitchFormat.ParseNsp(mainApp);
                                if (!string.IsNullOrEmpty(info.TitleId) && ulong.TryParse(info.TitleId, System.Globalization.NumberStyles.HexNumber, null, out ulong parsedTid))
                                {
                                    baseTitleId = parsedTid;
                                }
                            }
                            catch { }
                        }
                        if (baseTitleId == 0 && !string.IsNullOrEmpty(savedBaseFile))
                        {
                            try
                            {
                                var info = App.SwitchFormat.ParseNsp(savedBaseFile);
                                if (!string.IsNullOrEmpty(info.TitleId) && ulong.TryParse(info.TitleId, System.Globalization.NumberStyles.HexNumber, null, out ulong parsedTid))
                                {
                                    baseTitleId = parsedTid;
                                }
                            }
                            catch { }
                        }
                        if (baseTitleId == 0)
                        {
                            string combinedHint = $"{task.OutputFileName} {mainApp} {savedBaseFile} {task.GroupId} " + string.Join(" ", inputFiles);
                            var m = System.Text.RegularExpressions.Regex.Match(combinedHint, @"0100[0-9A-Fa-f]{12}");
                            if (m.Success && ulong.TryParse(m.Value, System.Globalization.NumberStyles.HexNumber, null, out ulong regexTid))
                            {
                                baseTitleId = regexTid;
                            }
                        }



                        if (App.Settings.Current.EnableDlcCompletenessCheck && baseTitleId != 0)
                        {
                            var presentDlcIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var f in inputFiles)
                            {
                                try
                                {
                                    var fInfo = App.SwitchFormat.ParseNsp(f);
                                    if (!string.IsNullOrEmpty(fInfo.TitleId) && fInfo.ContentType == "AddOnContent")
                                    {
                                        presentDlcIds.Add(fInfo.TitleId);
                                    }
                                }
                                catch { }
                            }

                            if (presentDlcIds.Count > 0)
                            {
                                var dlcReport = App.TitleDb.CheckDlcCompleteness(baseTitleId.ToString("X16"), presentDlcIds);
                                App.RunOnUI(() =>
                                {
                                    task.LogDetails += $"\n📦 [Инспектор DLC] {dlcReport.SummaryText}";
                                    if (dlcReport.MissingDlcs.Count > 0 && dlcReport.MissingDlcs.Count <= 5)
                                    {
                                        foreach (var missing in dlcReport.MissingDlcs)
                                        {
                                            task.LogDetails += $"\n  • [0x{missing.Id:X16}] {missing.Name ?? "Неизвестное дополнение"}";
                                        }
                                    }
                                });
                            }
                        }

                        if (App.Settings.Current.EnableRsvCap && App.Settings.Current.RsvCap > 0)
                        {
                            App.RunOnUI(() => task.LogDetails += $"\n🛡️ [RSV Cap] Применен лимит минимальной версии системы (RSV Cap: {App.Settings.Current.RsvCap})");
                        }

                        var orderedEntries = mergedEntries
                            .OrderBy(kvp => GetNcaPriority(kvp.Value, kvp.Key, baseTitleId, baseEntries))
                            .ThenBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);

                        foreach (var kvp in orderedEntries)
                        {
                            pfsBuilder.AddFile(kvp.Key, new LibHac.FsSystem.StorageFile(new StormSwitchBox.Services.SafeStorageWrapper(kvp.Value.AsStorage()), LibHac.Fs.OpenMode.Read));
                        }

                        string outputNspPath = System.IO.Path.Combine(outFolder, $"multi_out_{Guid.NewGuid().ToString("N").Substring(0, 8)}.nsp");

                        using (var builtPfs = pfsBuilder.Build(PartitionFileSystemType.Standard))
                        {
                            builtPfs.GetSize(out long totalPfsSize).ThrowIfFailure();
                            
                            FileStream destStream;
                            try
                            {
                                var fsOptions = new FileStreamOptions
                                {
                                    Mode = FileMode.Create,
                                    Access = FileAccess.Write,
                                    Share = FileShare.None,
                                    BufferSize = 8 * 1024 * 1024,
                                    Options = FileOptions.SequentialScan
                                };
                                destStream = new FileStream(outputNspPath, fsOptions);
                            }
                            catch
                            {
                                destStream = new FileStream(outputNspPath, FileMode.Create, FileAccess.Write, FileShare.None, 4 * 1024 * 1024);
                            }

                            using (destStream)
                            {
                                long remaining = totalPfsSize;
                                long offset = 0;
                                int chunkSize = 8 * 1024 * 1024;
                                byte[] rentedBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(chunkSize);
                                var sw = System.Diagnostics.Stopwatch.StartNew();

                                try
                                {
                                    while (remaining > 0)
                                    {
                                        cancellationToken.ThrowIfCancellationRequested();
                                        int toRead = (int)Math.Min(chunkSize, remaining);
                                        builtPfs.Read(offset, rentedBuffer.AsSpan(0, toRead)).ThrowIfFailure();
                                        destStream.Write(rentedBuffer, 0, toRead);
                                        offset += toRead;
                                        remaining -= toRead;

                                        if (sw.ElapsedMilliseconds > 350 || remaining == 0)
                                        {
                                            sw.Restart();
                                            double pct = (double)offset / totalPfsSize * 100.0;
                                            App.RunOnUI(() => task.Progress = Math.Min(99.9, pct));
                                        }
                                    }
                                }
                                finally
                                {
                                    System.Buffers.ArrayPool<byte>.Shared.Return(rentedBuffer);
                                }
                            }
                        }

                        if (isTargetXci)
                        {
                            App.RunOnUI(() => task.LogDetails += "\n🔄 [Конвертация] Сборка XCI из Multi-NSP (4nxci)...");
                            await App.SwitchFormat.ConvertContainerAsync(task, outputNspPath, outFolder, "XCI", cancellationToken);
                            try { if (File.Exists(outputNspPath)) File.Delete(outputNspPath); } catch { }
                        }

                        buildDone = true;
                    }
                    finally
                    {
                        foreach (var f in openedFiles) { try { f.Dispose(); } catch { } }
                        foreach (var s in openedStreams) { try { s.Dispose(); } catch { } }
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Не удалось собрать мультиконтент: {ex.Message}");
                }

                if (!buildDone)
                {
                    throw new Exception("Не удалось создать мультиконтент — сшивание завершилось без результата.");
                }

                // Search for the actual content file (.nsp/.xci), skipping metadata like .cnmt.xml
                string[] contentExtensions = new[] { ".nsp", ".xci", ".nsz", ".xcz" };
                string? generatedFile = Directory.GetFiles(outFolder)
                    .Where(f => contentExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(f => new System.IO.FileInfo(f).Length)
                    .FirstOrDefault();

                string formattedPath = FormatOutputFileName(outPath, inputFiles);
                if (!string.IsNullOrEmpty(formattedPath) && !formattedPath.Equals(outPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (intermediatePath.Equals(outPath, StringComparison.OrdinalIgnoreCase))
                        intermediatePath = formattedPath;
                    outPath = formattedPath;
                }

                if (System.IO.File.Exists(generatedFile) && !generatedFile.Equals(intermediatePath, StringComparison.OrdinalIgnoreCase))
                {
                    intermediatePath = await SafeFileOperations.SafeMoveOrReplaceFileAsync(generatedFile, intermediatePath, task, cancellationToken);
                    if (!isCompressedFormat)
                    {
                        outPath = intermediatePath;
                    }
                }

                // Применяем кастомные метаданные / иконку, если они заданы пользователем (и еще не применены в HardPatch)
                if (task.CustomMetadata != null && !hasPatchedBase && System.IO.File.Exists(intermediatePath) && intermediatePath.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase))
                {
                    await App.ControlEditor.ApplyCustomMetadataAsync(task.CustomMetadata, intermediatePath, task, cancellationToken);
                }

                // 5. Zstandard Сжатие (NSZ/XCZ), если необходимо
                if (isCompressedFormat)
                {
                    App.RunOnUI(() =>
                    {
                        task.LogDetails += $"\n🟡 [Сжатие] Zstandard в формат {task.TargetFormat}...";
                        task.Status = "Сжатие...";
                    });
                    
                    await App.NszCompression.CompressToNszAsync(task, intermediatePath, targetDir, cancellationToken);
                    
                    string ext = string.Equals(task.TargetFormat, "XCZ", StringComparison.OrdinalIgnoreCase) ? ".xcz" : ".nsz";
                    string expectedNsz = System.IO.Path.ChangeExtension(intermediatePath, ext);
                    string finalCompressedPath = System.IO.Path.ChangeExtension(outPath, ext);
                    
                    // Also check for NSZ/XCZ in targetDir with same filename
                    if (!System.IO.File.Exists(expectedNsz))
                    {
                        string altNsz = System.IO.Path.Combine(targetDir, System.IO.Path.GetFileNameWithoutExtension(intermediatePath) + ext);
                        if (System.IO.File.Exists(altNsz)) expectedNsz = altNsz;
                    }
                    
                    compressionSuccess = false;
                    if (System.IO.File.Exists(expectedNsz) && new FileInfo(expectedNsz).Length > 0)
                    {
                        if (ext == ".xcz" || ext == ".nsz")
                        {
                            if (!expectedNsz.Equals(finalCompressedPath, StringComparison.OrdinalIgnoreCase))
                            {
                                finalCompressedPath = await SafeFileOperations.SafeMoveOrReplaceFileAsync(expectedNsz, finalCompressedPath, task, cancellationToken);
                            }
                            compressionSuccess = true;
                        }
                    }
                    
                    if (compressionSuccess)
                    {
                        // Сохраняем оба файла: и несжатый образ (NSP/XCI), и сжатый (NSZ/XCZ)
                        outPath = finalCompressedPath;
                    }
                    else
                    {
                        // Compression failed — keep intermediate NSP/XCI as output
                        App.RunOnUI(() => task.LogDetails += "\n⚠️ [Внимание] Сжатие не удалось. Сохранен NSP.");
                        outPath = intermediatePath;
                    }
                }


                App.RunOnUI(() =>
                {
                    if (System.IO.File.Exists(outPath))
                    {
                        long outSize = new System.IO.FileInfo(outPath).Length;
                        task.TargetSize = Models.ProcessingTask.FormatSize(outSize);
                        if (task.SourceSizeBytes > 0)
                        {
                            long diff = task.SourceSizeBytes - outSize;
                            double percent = (double)diff / task.SourceSizeBytes * 100.0;
                            task.SizeDifference = $"{(diff > 0 ? "-" : "+")}{Models.ProcessingTask.FormatSize(Math.Abs(diff))} ({Math.Abs(percent):F1}%)";
                        }
                    }

                    task.Progress = 100;
                    task.Status = "Успешно";
                    task.IsRunning = false;
                    if (isCompressedFormat && compressionSuccess && System.IO.File.Exists(intermediatePath))
                    {
                        task.LogDetails += $"\n📦 [Форматы] Сохранены оба файла:\n  • {System.IO.Path.GetFileName(intermediatePath)}\n  • {System.IO.Path.GetFileName(outPath)}";
                    }
                    else
                    {
                        task.LogDetails += $"\n✅ [Готово] Сохранен: {System.IO.Path.GetFileName(outPath)}";
                    }
                    StormSwitchBox.Services.HistoryService.AddToHistory(task);
                });

                DeployCheatsIfPresent(titleIdStr, inputFiles, outPath);
                App.Logger.Log($"Мульти-контент успешно создан: {System.IO.Path.GetFileName(outPath)}", LogLevel.Success);
            }
            catch (OperationCanceledException)
            {
                App.RunOnUI(() => { task.Status = "Отменен"; task.IsRunning = false; StormSwitchBox.Services.HistoryService.AddToHistory(task); });
            }
            catch (Exception ex)
            {
                App.RunOnUI(() => { task.Status = "Ошибка"; task.IsRunning = false; task.LogDetails += $"\n🔴 [Ошибка] {ex.Message}"; StormSwitchBox.Services.HistoryService.AddToHistory(task); });
                string operationName = task.Operation == "Update" ? "обновления" : "сборки мульти-контента";
                App.Logger.Log($"Ошибка {operationName}: {ex.ToString()}", LogLevel.Error);
            }
            finally
            {
                TempCleanupService.ForceDeleteDirectory(tempDecompDir);
                
                // Не удаляем intermediatePath, если сжатие прошло успешно (сохраняем оба файла: и NSP/XCI, и NSZ/XCZ)
                if (!(isCompressedFormat && compressionSuccess) && intermediatePath != outPath && !string.IsNullOrEmpty(intermediatePath) && System.IO.File.Exists(intermediatePath))
                {
                    TempCleanupService.ForceDeleteFile(intermediatePath);
                }
            }
        }
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        private string PrepareSafeFileForTemp(string sourcePath, string tempDir)
        {
            if (Directory.Exists(sourcePath)) return sourcePath;

            // Если это уже NSP файл и его путь безопасный (< 240 символов) — используем его напрямую без копирования
            if (sourcePath.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) && sourcePath.Length < 240 && File.Exists(sourcePath))
            {
                return sourcePath;
            }

            string origName = System.IO.Path.GetFileName(sourcePath);
            string safeName = NszCompressionService.SanitizeFileName(origName);

            // Защита от слишком длинных имен файлов (> 80 символов)
            if (safeName.Length > 80)
            {
                string ext = System.IO.Path.GetExtension(safeName);
                var matchTid = System.Text.RegularExpressions.Regex.Match(safeName, @"\[[0-9A-Fa-f]{16}\]");
                string tidPart = matchTid.Success ? "_" + matchTid.Value : "";
                string prefix = safeName.Substring(0, Math.Min(30, safeName.Length));
                safeName = $"{prefix}{tidPart}_{Guid.NewGuid().ToString("N").Substring(0, 4)}{ext}";
            }

            string destPath = System.IO.Path.Combine(tempDir, safeName);
            if (sourcePath.Equals(destPath, StringComparison.OrdinalIgnoreCase)) return sourcePath;

            if (!File.Exists(destPath))
            {
                try
                {
                    // Пробуем создать быстрый хардлинк
                    if (CreateHardLink(destPath, sourcePath, IntPtr.Zero))
                    {
                        return destPath;
                    }

                    // Если файл небольшой (< 300 МБ) — копируем в быстрый STORM_TMP
                    var fi = new FileInfo(sourcePath);
                    if (fi.Length < 300L * 1024 * 1024)
                    {
                        File.Copy(sourcePath, destPath, true);
                    }
                    else
                    {
                        return sourcePath;
                    }
                }
                catch
                {
                    return sourcePath;
                }
            }
            return destPath;
        }



        private static IFile OpenFileSafe(IFileSystem fsToOpen, string pth)
        {
            using var fRef = new UniqueRef<IFile>();
            using var path = new LibHac.Fs.Path();
            path.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(pth))).ThrowIfFailure();
            fsToOpen.OpenFile(ref fRef.Ref, in path, OpenMode.Read).ThrowIfFailure();
            return fRef.Release();
        }
        private static LibHac.Fs.Fsa.IFile OpenFileSafe(PartitionFileSystem fs, string fullPath)
        {
            var path = new LibHac.Fs.Path();
            path.Initialize(new LibHac.Common.U8Span(System.Text.Encoding.UTF8.GetBytes(fullPath))).ThrowIfFailure();
            using var fileRef = new LibHac.Common.UniqueRef<LibHac.Fs.Fsa.IFile>();
            fs.OpenFile(ref fileRef.Ref, in path, LibHac.Fs.OpenMode.Read).ThrowIfFailure();
            return fileRef.Release();
        }

        private static bool IsValidNspEntry(string name)
        {
            // Valid NSP entries: .nca, .ncz, .tik, .cert
            string ext = System.IO.Path.GetExtension(name).ToLowerInvariant();
            return ext == ".nca" || ext == ".ncz" || ext == ".tik" || ext == ".cert";
        }

        public static string FormatOutputFileName(string originalOutPath, List<string> allInputFiles)
        {
            string targetDir = System.IO.Path.GetDirectoryName(originalOutPath) ?? "";
            string origFileName = System.IO.Path.GetFileNameWithoutExtension(originalOutPath);
            string ext = System.IO.Path.GetExtension(originalOutPath);

            string titleId = "";
            string patchVer = "";
            int gameCount = 0;
            int updateCount = 0;
            int dlcCount = 0;
            int modCount = 0;

            foreach (var f in allInputFiles)
            {
                if (System.IO.Directory.Exists(f))
                {
                    string dirName = System.IO.Path.GetFileName(f).ToLowerInvariant();
                    if (dirName == "romfs" || dirName == "exefs" || dirName == "exefs_patches" || dirName == "cheats" || dirName == "atmosphere" ||
                        f.Contains("romfs", StringComparison.OrdinalIgnoreCase) || 
                        f.Contains("exefs", StringComparison.OrdinalIgnoreCase) ||
                        f.Contains("exefs_patches", StringComparison.OrdinalIgnoreCase) ||
                        f.Contains("cheat", StringComparison.OrdinalIgnoreCase) ||
                        f.Contains("чит", StringComparison.OrdinalIgnoreCase) ||
                        f.Contains("mod", StringComparison.OrdinalIgnoreCase) ||
                        f.Contains("мод", StringComparison.OrdinalIgnoreCase))
                    {
                        modCount = 1;
                    }
                    continue;
                }

                string fname = System.IO.Path.GetFileName(f);
                string tid = "";
                var matchTid = System.Text.RegularExpressions.Regex.Match(fname, @"\[([0-9A-Fa-f]{16})\]");
                if (matchTid.Success) tid = matchTid.Groups[1].Value.ToUpperInvariant();

                bool isModFile = fname.Contains("MOD", StringComparison.OrdinalIgnoreCase) ||
                                 fname.Contains("РУС", StringComparison.OrdinalIgnoreCase) ||
                                 fname.Contains("RUS", StringComparison.OrdinalIgnoreCase) ||
                                 fname.Contains("cheat", StringComparison.OrdinalIgnoreCase) ||
                                 fname.Contains("чит", StringComparison.OrdinalIgnoreCase) ||
                                 fname.Contains("romfs", StringComparison.OrdinalIgnoreCase) ||
                                 fname.Contains("exefs", StringComparison.OrdinalIgnoreCase);

                bool isDlc = (!string.IsNullOrEmpty(tid) && tid.Length == 16 && !tid.EndsWith("000") && !tid.EndsWith("800")) ||
                             fname.Contains("DLC", StringComparison.OrdinalIgnoreCase) ||
                             fname.Contains("AddOn", StringComparison.OrdinalIgnoreCase);

                bool isPatch = !isModFile && ((!string.IsNullOrEmpty(tid) && tid.Length == 16 && tid.EndsWith("800")) ||
                               fname.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
                               fname.Contains("Patch", StringComparison.OrdinalIgnoreCase) ||
                               (fname.Contains("[v") && !fname.Contains("[v0]")));

                bool isBase = (!string.IsNullOrEmpty(tid) && tid.Length == 16 && tid.EndsWith("000")) ||
                              fname.Contains("[v0]") || fname.EndsWith("v0.nsp", StringComparison.OrdinalIgnoreCase) ||
                              fname.Contains("patched_base");

                if (isModFile)
                {
                    modCount = 1;
                }

                if (isDlc)
                {
                    dlcCount++;
                }
                else if (isPatch)
                {
                    updateCount++;
                    var matchVer = System.Text.RegularExpressions.Regex.Match(fname, @"\[v(\d+)\]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (matchVer.Success && string.IsNullOrEmpty(patchVer))
                    {
                        patchVer = matchVer.Groups[1].Value;
                    }
                }
                else if (isBase)
                {
                    gameCount++;
                    if (string.IsNullOrEmpty(titleId) && !string.IsNullOrEmpty(tid)) titleId = tid;
                }
            }

            if (string.IsNullOrEmpty(titleId) || string.IsNullOrEmpty(patchVer))
            {
                try
                {
                    foreach (var f in allInputFiles)
                    {
                        if (System.IO.Directory.Exists(f)) continue;
                        var info = App.SwitchFormat.ParseNsp(f);
                        if (string.IsNullOrEmpty(titleId) && info.ContentType == "Application" && !string.IsNullOrEmpty(info.TitleId))
                            titleId = info.TitleId.Trim().ToUpperInvariant();
                        if (string.IsNullOrEmpty(patchVer) && info.ContentType == "Patch" && !string.IsNullOrEmpty(info.Version))
                            patchVer = info.Version.Trim();
                    }
                }
                catch { }
            }

            if (gameCount == 0) gameCount = 1;

            string baseGameTitle = origFileName;

            // Проверяем, содержит ли оригинальное имя файла уже TitleID и/или версию
            // Если пользователь указал их в своём формате (например, в круглых скобках),
            // не нужно удалять и добавлять заново в квадратных скобках
            bool origHasTitleId = !string.IsNullOrEmpty(titleId) &&
                origFileName.Contains(titleId, StringComparison.OrdinalIgnoreCase);
            bool origHasPatchVer = !string.IsNullOrEmpty(patchVer) &&
                (origFileName.Contains($"v{patchVer}", StringComparison.OrdinalIgnoreCase) ||
                 origFileName.Contains(patchVer, StringComparison.OrdinalIgnoreCase));

            // Удаляем любой существующий тег содержимого (1G+1U+4D), (1G+1U+4D+1M), (1G+1U+1M), (1G+5D) и т.д. — он всегда пересчитывается заново
            baseGameTitle = System.Text.RegularExpressions.Regex.Replace(baseGameTitle, @"\s*\(\d+[A-Za-z](?:\+\d+[A-Za-z])*\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // Удаляем квадратные теги [TitleID] и [vXXX] ТОЛЬКО если их нет в оригинале
            // (т.е. они были добавлены автоматически ранее, а не пользователем)
            if (!origHasTitleId)
            {
                baseGameTitle = System.Text.RegularExpressions.Regex.Replace(baseGameTitle, @"\s*\[[0-9A-Fa-f]{16}\]", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            if (!origHasPatchVer)
            {
                baseGameTitle = System.Text.RegularExpressions.Regex.Replace(baseGameTitle, @"\s*\[v\d+\]", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Не удаляем пользовательские круглые скобки с информацией о версии (1.0.9 - 458752 - TitleID)
            // Удаляем только если TitleID НЕ был в оригинале (значит это автоматический тег)
            if (!origHasTitleId)
            {
                baseGameTitle = System.Text.RegularExpressions.Regex.Replace(baseGameTitle, @"\s*\([^)]*\d{16}[^)]*\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            if (baseGameTitle.EndsWith("_Multi", StringComparison.OrdinalIgnoreCase))
                baseGameTitle = baseGameTitle.Substring(0, baseGameTitle.Length - 6);
            if (baseGameTitle.EndsWith("_Update", StringComparison.OrdinalIgnoreCase))
                baseGameTitle = baseGameTitle.Substring(0, baseGameTitle.Length - 7);

            var sb = new System.Text.StringBuilder();
            sb.Append(baseGameTitle.Trim());

            // Добавляем TitleID и версию ТОЛЬКО если их нет в оригинальном имени
            if (!origHasTitleId && !string.IsNullOrEmpty(titleId))
            {
                sb.Append($" [{titleId}]");
            }

            if (!origHasPatchVer && !string.IsNullOrEmpty(patchVer))
            {
                sb.Append(patchVer.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? $" [{patchVer}]" : $" [v{patchVer}]");
            }

            var parts = new List<string>();
            if (gameCount > 0) parts.Add($"{gameCount}G");
            if (updateCount > 0) parts.Add($"{updateCount}U");
            if (dlcCount > 0) parts.Add($"{dlcCount}D");
            if (modCount > 0) parts.Add($"{modCount}M");

            if (parts.Count > 0)
            {
                sb.Append($" ({string.Join("+", parts)})");
            }

            sb.Append(ext);
            string newFileName = NszCompressionService.SanitizeFinalOutputFileName(sb.ToString());
            return System.IO.Path.Combine(targetDir, newFileName);
        }

        private int GetNcaPriority(LibHac.Fs.Fsa.IFile file, string fileName, ulong baseTitleId, HashSet<string>? baseEntries = null)
        {
            string lower = fileName.ToLowerInvariant();

            if (lower.EndsWith(".tik")) return 0;
            if (lower.EndsWith(".cert")) return 1;

            bool isFromBase = baseEntries != null && baseEntries.Contains(fileName);

            if (lower.EndsWith(".cnmt.xml"))
            {
                if (isFromBase || lower.Contains("000") || lower.Contains("base")) return 2;
                if (lower.Contains("800") || lower.Contains("update") || lower.Contains("patch")) return 10;
                return 20;
            }

            if (lower.EndsWith(".cnmt.nca"))
            {
                if (isFromBase) return 3; // Base/Patched Game CNMT is top priority among NCAs
                if (lower.Contains("000") || lower.Contains("base")) return 3;
                if (lower.Contains("800") || lower.Contains("update") || lower.Contains("patch")) return 11; // Update Patch CNMT
                if (baseTitleId != 0 && lower.Contains(baseTitleId.ToString("x16"))) return 3;
                if (baseTitleId != 0 && lower.Contains((baseTitleId + 0x800).ToString("x16"))) return 11;
                return 21; // DLC / Mod CNMT
            }

            try
            {
                LibHac.Fs.IStorage storage = file.AsStorage();
                if (lower.EndsWith(".ncz"))
                {
                    try
                    {
                        storage = new Core.NSZ.StormNczStorage(storage, null, null, _keysService.CurrentKeyset);
                    }
                    catch { }
                }

                var nca = new LibHac.Tools.FsSystem.NcaUtils.Nca(_keysService.CurrentKeyset, storage);
                var type = nca.Header.ContentType;
                ulong tid = nca.Header.TitleId;

                bool isBaseTitle = (baseTitleId != 0 && tid == baseTitleId) || tid.ToString("X16").EndsWith("000");
                bool isUpdateTitle = (baseTitleId != 0 && tid == (baseTitleId | 0x800)) || tid.ToString("X16").EndsWith("800");

                if (type == LibHac.Tools.FsSystem.NcaUtils.NcaContentType.Meta) // CNMT
                {
                    if (isBaseTitle) return 3;
                    if (isUpdateTitle) return 11;
                    return 21;
                }

                if (type == LibHac.Tools.FsSystem.NcaUtils.NcaContentType.Control) // Icon artwork & Title strings
                {
                    if (isBaseTitle) return 4;
                    if (isUpdateTitle) return 12;
                    return 22;
                }

                if (type == LibHac.Tools.FsSystem.NcaUtils.NcaContentType.Program) // Executable code
                {
                    if (isBaseTitle) return 5;
                    if (isUpdateTitle) return 13;
                    return 23;
                }

                if (type == LibHac.Tools.FsSystem.NcaUtils.NcaContentType.Manual || type == LibHac.Tools.FsSystem.NcaUtils.NcaContentType.PublicData || type == LibHac.Tools.FsSystem.NcaUtils.NcaContentType.Data)
                {
                    if (isBaseTitle) return 6;
                    if (isUpdateTitle) return 14;
                    return 24;
                }
            }
            catch
            {
                if (isFromBase)
                {
                    if (lower.EndsWith(".cnmt.nca")) return 3;
                    if (lower.Contains("control")) return 4;
                    if (lower.Contains("program")) return 5;
                    
                    try
                    {
                        file.GetSize(out long fSize);
                        if (fSize > 0 && fSize < 5 * 1024 * 1024) return 4; // Control-sized NCA
                    }
                    catch { }
                    return 5;
                }

                if (lower.EndsWith(".cnmt.nca"))
                {
                    if (lower.Contains("800") || lower.Contains("update") || lower.Contains("patch")) return 11;
                    return 21;
                }
                if (lower.Contains("control")) return 22;
                if (lower.Contains("program")) return 23;
            }

            return isFromBase ? 7 : 25;
        }

        private List<string> ExtractUnlockerRomFsDirectories(List<string> inputFiles, string tempDir, string? baseTitleIdStr, Models.ProcessingTask task, CancellationToken ct)
        {
            var extractedDirs = new List<string>();
            int unlockerIndex = 0;

            foreach (var file in inputFiles)
            {
                if (Directory.Exists(file)) continue;
                string fname = System.IO.Path.GetFileName(file);
                
                // Проверяем, является ли файл Unlocker-патчем/DLC (с учетом русских и английских названий)
                bool isUnlockerName = fname.Contains("Unlocker", StringComparison.OrdinalIgnoreCase) ||
                                     fname.Contains("Unlock", StringComparison.OrdinalIgnoreCase) ||
                                     fname.Contains("Custom Unlock", StringComparison.OrdinalIgnoreCase) ||
                                     fname.Contains("Анлокер", StringComparison.OrdinalIgnoreCase) ||
                                     fname.Contains("Разблокировщик", StringComparison.OrdinalIgnoreCase) ||
                                     fname.Contains("Разблокировка", StringComparison.OrdinalIgnoreCase) ||
                                     fname.Contains("Анлок", StringComparison.OrdinalIgnoreCase) ||
                                     fname.Contains("Все персонажи", StringComparison.OrdinalIgnoreCase) ||
                                     fname.Contains("Персонажи", StringComparison.OrdinalIgnoreCase);

                bool isSmallDlc = false;
                try
                {
                    var info = App.SwitchFormat.ParseNsp(file);
                    if (info.ContentType == "AddOnContent" && new FileInfo(file).Length < 250 * 1024 * 1024)
                    {
                        isSmallDlc = true;
                    }
                }
                catch { }

                if (!isUnlockerName && !isSmallDlc) continue;

                string targetRomfs = System.IO.Path.Combine(tempDir, $"unlocker_romfs_{unlockerIndex}");
                Directory.CreateDirectory(targetRomfs);

                bool extracted = false;
                try
                {
                    using var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var pfs = new PartitionFileSystem(fileStream.AsStorage());
                    foreach (var entry in pfs.EnumerateEntries())
                    {
                        if (entry.Type == LibHac.Fs.DirectoryEntryType.Directory) continue;
                        string ename = entry.Name.ToLowerInvariant();
                        if (ename.EndsWith(".nca") && !ename.EndsWith(".cnmt.nca"))
                        {
                            using var ncaFile = new UniqueRef<IFile>();
                            using var entryPath = new LibHac.Fs.Path();
                            entryPath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(entry.FullPath))).ThrowIfFailure();
                            pfs.OpenFile(ref ncaFile.Ref, in entryPath, OpenMode.Read).ThrowIfFailure();
                            
                            var nca = new LibHac.Tools.FsSystem.NcaUtils.Nca(_keysService.CurrentKeyset, ncaFile.Release().AsStorage());
                            
                            for (int sec = 0; sec < 2; sec++)
                            {
                                if (nca.SectionExists(sec) && nca.CanOpenSection(sec))
                                {
                                    try
                                    {
                                        var romfsFs = nca.OpenFileSystem(sec, IntegrityCheckLevel.None);
                                        ExtractAllEntriesFromRomFs(romfsFs, targetRomfs, ct);
                                        if (Directory.GetFiles(targetRomfs, "*", SearchOption.AllDirectories).Length > 0)
                                        {
                                            extracted = true;
                                            break;
                                        }
                                    }
                                    catch (Exception exLib)
                                    {
                                        App.Logger.Log($"[Unlocker] LibHac section {sec} extract note: {exLib.Message}", Models.LogLevel.Info);
                                    }
                                }
                            }
                            if (extracted) break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.Log($"[Unlocker] LibHac extraction warning: {ex.Message}", Models.LogLevel.Warning);
                }

                // Fallback через hactoolnet если LibHac не извлек
                if (!extracted)
                {
                    try
                    {
                        string appDir = AppDomain.CurrentDomain.BaseDirectory;
                        string hactoolPath = System.IO.Path.Combine(appDir, "tools", "com.github.nozwock.yanu", "hactoolnet.exe");
                        if (!File.Exists(hactoolPath))
                            hactoolPath = System.IO.Path.Combine(appDir, "..", "tools", "com.github.nozwock.yanu", "hactoolnet.exe");
                        if (!File.Exists(hactoolPath))
                            hactoolPath = System.IO.Path.Combine(appDir, "tools", "hactoolnet.exe");

                        if (File.Exists(hactoolPath))
                        {
                            string keysPath = App.Settings.Current.KeysPath;
                            if (string.IsNullOrEmpty(keysPath) || !File.Exists(keysPath))
                                keysPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "prod.keys");

                            string keyArg = File.Exists(keysPath) ? $"-k \"{keysPath}\"" : "";
                            string tempExtractDir = System.IO.Path.Combine(tempDir, $"hactool_extract_{unlockerIndex}");
                            Directory.CreateDirectory(tempExtractDir);

                            var psi = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = hactoolPath,
                                Arguments = $"{keyArg} -t pfs0 --outdir \"{tempExtractDir}\" \"{file}\"",
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };
                            using (var proc = System.Diagnostics.Process.Start(psi))
                            {
                                proc?.WaitForExit(15000);
                            }

                            var extractedNcas = Directory.GetFiles(tempExtractDir, "*.nca");
                            foreach (var ncaPath in extractedNcas)
                            {
                                if (ncaPath.EndsWith(".cnmt.nca", StringComparison.OrdinalIgnoreCase)) continue;
                                var romfsPsi = new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = hactoolPath,
                                    Arguments = $"{keyArg} --romfsdir \"{targetRomfs}\" \"{ncaPath}\"",
                                    UseShellExecute = false,
                                    CreateNoWindow = true
                                };
                                using (var rproc = System.Diagnostics.Process.Start(romfsPsi))
                                {
                                    rproc?.WaitForExit(15000);
                                }
                            }
                            try { Directory.Delete(tempExtractDir, true); } catch { }
                            extracted = Directory.GetFiles(targetRomfs, "*", SearchOption.AllDirectories).Length > 0;
                        }
                    }
                    catch { }
                }

                if (extracted)
                {
                    var allExtracted = Directory.GetFiles(targetRomfs, "*", SearchOption.AllDirectories);
                    int fileCount = allExtracted.Length;
                    App.Logger.Log($"[Unlocker] Успешно извлечено {fileCount} файлов разблокировки из {fname} для RomFS-инъекции.", Models.LogLevel.Success);
                    string sampleNames = string.Join(", ", allExtracted.Take(4).Select(System.IO.Path.GetFileName));
                    if (fileCount > 4) sampleNames += "...";
                    App.RunOnUI(() => task.LogDetails += $"\n🔓 [Unlocker] Извлечено {fileCount} токенов разблокировки из {fname} ({sampleNames})");
                    extractedDirs.Add(targetRomfs);

                    // Синхронизация с эмулятором (LayeredFS)
                    SyncUnlockerToEmulators(targetRomfs, baseTitleIdStr, task);
                    unlockerIndex++;
                }
                else
                {
                    try { Directory.Delete(targetRomfs, true); } catch { }
                }
            }

            return extractedDirs;
        }

        private static void ExtractAllEntriesFromRomFs(LibHac.Fs.Fsa.IFileSystem fs, string targetDir, CancellationToken ct)
        {
            foreach (var rentry in fs.EnumerateEntries("/", "*"))
            {
                ct.ThrowIfCancellationRequested();
                if (rentry.Type == LibHac.Fs.DirectoryEntryType.Directory) continue;

                string relPath = rentry.FullPath.TrimStart('/');
                string localPath = System.IO.Path.Combine(targetDir, relPath);
                string? localDir = System.IO.Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(localDir) && !Directory.Exists(localDir))
                {
                    Directory.CreateDirectory(localDir);
                }

                using var fileRef = new UniqueRef<IFile>();
                using var filePath = new LibHac.Fs.Path();
                filePath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(rentry.FullPath))).ThrowIfFailure();
                fs.OpenFile(ref fileRef.Ref, in filePath, OpenMode.Read).ThrowIfFailure();
                var f = fileRef.Release();

                f.GetSize(out long fSize).ThrowIfFailure();
                using var outFs = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
                byte[] buf = new byte[64 * 1024];
                long off = 0;
                while (off < fSize)
                {
                    int toRead = (int)Math.Min(buf.Length, fSize - off);
                    f.Read(out long r, off, buf.AsSpan(0, toRead)).ThrowIfFailure();
                    outFs.Write(buf, 0, (int)r);
                    off += r;
                }
            }
        }

        private void SyncUnlockerToEmulators(string unlockerRomfsDir, string? baseTitleIdStr, Models.ProcessingTask task)
        {
            if (string.IsNullOrEmpty(baseTitleIdStr)) return;
            string cleanTid = baseTitleIdStr.Trim().ToUpperInvariant();
            if (cleanTid.Length != 16) return;

            try
            {
                var emulatorPaths = HomebrewService.FindAllEmulatorSdmcDirectories();
                foreach (var sdmcPath in emulatorPaths)
                {
                    string userDir = System.IO.Path.GetDirectoryName(sdmcPath) ?? "";
                    if (!Directory.Exists(userDir)) continue;

                    // 1. user/load/<TitleID>/romfs/
                    string loadRomfs = System.IO.Path.Combine(userDir, "load", cleanTid, "romfs");
                    Directory.CreateDirectory(loadRomfs);
                    CopyDirectoryContentSafe(unlockerRomfsDir, loadRomfs);

                    // 2. user/sdmc/atmosphere/contents/<TitleID>/romfs/
                    string atmoRomfs = System.IO.Path.Combine(sdmcPath, "atmosphere", "contents", cleanTid, "romfs");
                    Directory.CreateDirectory(atmoRomfs);
                    CopyDirectoryContentSafe(unlockerRomfsDir, atmoRomfs);

                    App.Logger.Log($"[Unlocker] Синхронизированы LayeredFS файлы разблокировки для {cleanTid} в эмулятор: {userDir}", Models.LogLevel.Success);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[Unlocker] Ошибка синхронизации с эмуляторами: {ex.Message}", Models.LogLevel.Warning);
            }
        }

        private static void CopyDirectoryContentSafe(string sourceDir, string targetDir)
        {
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string rel = System.IO.Path.GetRelativePath(sourceDir, file);
                string dest = System.IO.Path.Combine(targetDir, rel);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, true);
            }
        }

        private static void DeployCheatsIfPresent(string titleId, List<string> inputFiles, string outPath)
        {
            if (string.IsNullOrEmpty(titleId)) return;
            string cleanTid = titleId.Trim().ToUpperInvariant();

            var cheatFiles = new List<string>();
            foreach (var input in inputFiles)
            {
                if (Directory.Exists(input))
                {
                    try
                    {
                        var txts = Directory.GetFiles(input, "*.txt", SearchOption.AllDirectories)
                            .Where(f => f.Contains("cheat", StringComparison.OrdinalIgnoreCase) || 
                                        f.Contains("contents", StringComparison.OrdinalIgnoreCase) ||
                                        System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileNameWithoutExtension(f), @"^[0-9A-Fa-f]{16}$"))
                            .ToList();
                        cheatFiles.AddRange(txts);
                    }
                    catch { }
                }
                else if (File.Exists(input))
                {
                    if (input.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) && 
                        (input.Contains("cheat", StringComparison.OrdinalIgnoreCase) || 
                         System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileNameWithoutExtension(input), @"^[0-9A-Fa-f]{16}$")))
                    {
                        cheatFiles.Add(input);
                    }
                }
            }

            if (cheatFiles.Count == 0)
            {
                var scannedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var input in inputFiles)
                {
                    string parentDir = Directory.Exists(input) ? input : (Path.GetDirectoryName(input) ?? "");
                    if (!string.IsNullOrEmpty(parentDir) && scannedDirs.Add(parentDir) && Directory.Exists(parentDir))
                    {
                        try
                        {
                            var txts = Directory.GetFiles(parentDir, "*.txt", SearchOption.AllDirectories)
                                .Where(f => f.Contains("cheat", StringComparison.OrdinalIgnoreCase) || 
                                            (f.Contains("contents", StringComparison.OrdinalIgnoreCase) && f.Contains(cleanTid, StringComparison.OrdinalIgnoreCase)) ||
                                            System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileNameWithoutExtension(f), @"^[0-9A-Fa-f]{16}$"))
                                .ToList();
                            cheatFiles.AddRange(txts);
                        }
                        catch { }
                    }
                }
            }

            if (cheatFiles.Count == 0) return;

            // 1. Синхронизация читов с эмуляторами
            try
            {
                var emulatorPaths = HomebrewService.FindAllEmulatorSdmcDirectories();
                foreach (var sdmcPath in emulatorPaths)
                {
                    string userDir = System.IO.Path.GetDirectoryName(sdmcPath) ?? "";
                    if (!Directory.Exists(userDir)) continue;

                    string atmoCheats = System.IO.Path.Combine(sdmcPath, "atmosphere", "contents", cleanTid, "cheats");
                    string loadCheats = System.IO.Path.Combine(userDir, "load", cleanTid, "cheats");
                    Directory.CreateDirectory(atmoCheats);
                    Directory.CreateDirectory(loadCheats);

                    foreach (var cheatFile in cheatFiles)
                    {
                        string targetFileAtmo = System.IO.Path.Combine(atmoCheats, Path.GetFileName(cheatFile));
                        string targetFileLoad = System.IO.Path.Combine(loadCheats, Path.GetFileName(cheatFile));
                        File.Copy(cheatFile, targetFileAtmo, true);
                        File.Copy(cheatFile, targetFileLoad, true);
                    }

                    App.Logger.Log($"[Cheats] Чит-коды синхронизированы для {cleanTid} в эмулятор: {userDir}", Models.LogLevel.Success);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[Cheats] Ошибка синхронизации чит-кодов с эмуляторами: {ex.Message}", Models.LogLevel.Warning);
            }

            // 2. Копирование читов в каталог с собранным файлом
            try
            {
                string outDir = Path.GetDirectoryName(outPath) ?? "";
                if (Directory.Exists(outDir))
                {
                    string outCheatsDir = Path.Combine(outDir, "cheats", cleanTid);
                    Directory.CreateDirectory(outCheatsDir);
                    foreach (var cheatFile in cheatFiles)
                    {
                        string targetFile = Path.Combine(outCheatsDir, Path.GetFileName(cheatFile));
                        File.Copy(cheatFile, targetFile, true);
                    }
                    App.Logger.Log($"[Cheats] Чит-коды сохранены в выходной каталог: {outCheatsDir}", Models.LogLevel.Success);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[Cheats] Ошибка сохранения чит-кодов в выходной каталог: {ex.Message}", Models.LogLevel.Warning);
            }
        }

        private async Task<List<string>> GenerateModAddonEntriesAsync(
            ulong baseTitleId, 
            string tempDir, 
            bool hasRomFs, 
            bool hasExeFs, 
            int existingDlcCount,
            Models.ProcessingTask task, 
            CancellationToken ct)
        {
            var generatedNcas = new List<string>();
            try
            {
                string toolsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
                if (!Directory.Exists(toolsDir))
                {
                    string parentTools = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "tools");
                    if (Directory.Exists(parentTools)) toolsDir = parentTools;
                }

                string hacpackExe = Path.Combine(toolsDir, "com.github.nozwock.yanu", "hacpack.exe");
                if (!File.Exists(hacpackExe)) return generatedNcas;

                string? keysFile = App.Settings.Current.KeysPath;
                if (string.IsNullOrEmpty(keysFile) || !File.Exists(keysFile))
                {
                    string userKeys = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "prod.keys");
                    if (File.Exists(userKeys)) keysFile = userKeys;
                }
                if (string.IsNullOrEmpty(keysFile) || !File.Exists(keysFile))
                {
                    string toolsKeys = Path.Combine(toolsDir, "keys", ".switch", "prod.keys");
                    if (File.Exists(toolsKeys)) keysFile = toolsKeys;
                }
                if (string.IsNullOrEmpty(keysFile) || !File.Exists(keysFile))
                {
                    string nscbKeys = Path.Combine(toolsDir, "nscb", "ztools", "keys.txt");
                    if (File.Exists(nscbKeys)) keysFile = nscbKeys;
                }

                if (string.IsNullOrEmpty(keysFile) || !File.Exists(keysFile))
                {
                    App.Logger.Log("[ModAddon] Файл ключей не найден. Пропуск создания метаданных дополнений.", LogLevel.Warning);
                    return generatedNcas;
                }

                string modTempDir = Path.Combine(tempDir, "mod_addon_gen");
                Directory.CreateDirectory(modTempDir);

                // Извлекаем или генерируем иконку для Control NCA (256x256 JPEG)
                byte[]? iconBytes = task.CustomMetadata?.CustomIconBytes ?? task.CustomMetadata?.OriginalIconBytes;
                if (iconBytes == null || iconBytes.Length == 0)
                {
                    try
                    {
                        using var bmp = new System.Drawing.Bitmap(256, 256);
                        using (var g = System.Drawing.Graphics.FromImage(bmp))
                        {
                            g.Clear(System.Drawing.Color.FromArgb(30, 130, 230));
                            using var font = new System.Drawing.Font("Arial", 28, System.Drawing.FontStyle.Bold);
                            using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
                            g.DrawString("MOD", font, brush, 75, 105);
                        }
                        using var ms = new MemoryStream();
                        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                        iconBytes = ms.ToArray();
                    }
                    catch { }
                }

                ulong baseTitleIdClean = baseTitleId & ~0xFFFUL;
                int currentDlcIdx = existingDlcCount + 1;

                if (hasRomFs)
                {
                    ulong modTid = baseTitleIdClean | 0x1000 | (ulong)currentDlcIdx;
                    string modTidHex = modTid.ToString("X16");

                    string romfsControlDir = Path.Combine(modTempDir, "romfs_mod_control");
                    Directory.CreateDirectory(romfsControlDir);

                    string romFsTitle = !string.IsNullOrWhiteSpace(task.ModNameRomFs) ? task.ModNameRomFs : "Модификации: RomFS";

                    byte[] nacp = new byte[0x4000];
                    byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(romFsTitle);
                    byte[] devBytes = System.Text.Encoding.UTF8.GetBytes("STORM MODS");

                    // Записываем имя и разработчика во все языковые слоты NACP (16 языков по 0x300 байт)
                    for (int l = 0; l < 16; l++)
                    {
                        int titleOffset = l * 0x300;
                        int devOffset = titleOffset + 0x200;
                        Array.Copy(nameBytes, 0, nacp, titleOffset, Math.Min(nameBytes.Length, 0x200));
                        Array.Copy(devBytes, 0, nacp, devOffset, Math.Min(devBytes.Length, 0x100));
                    }
                    File.WriteAllBytes(Path.Combine(romfsControlDir, "control.nacp"), nacp);

                    if (iconBytes != null && iconBytes.Length > 0)
                    {
                        File.WriteAllBytes(Path.Combine(romfsControlDir, "icon_AmericanEnglish.dat"), iconBytes);
                        File.WriteAllBytes(Path.Combine(romfsControlDir, "icon_Russian.dat"), iconBytes);
                    }

                    string dataRomfsDir = Path.Combine(modTempDir, "romfs_mod_data");
                    Directory.CreateDirectory(dataRomfsDir);
                    File.WriteAllText(Path.Combine(dataRomfsDir, "mod.txt"), "Storm Switch Box Integrated RomFS Mod");

                    string outControlDir = Path.Combine(modTempDir, "out_romfs_control");
                    string outDataDir = Path.Combine(modTempDir, "out_romfs_data");
                    string outMetaDir = Path.Combine(modTempDir, "out_romfs_meta");
                    Directory.CreateDirectory(outControlDir);
                    Directory.CreateDirectory(outDataDir);
                    Directory.CreateDirectory(outMetaDir);

                    // 1. Control NCA
                    await ExternalProcessRunner.RunAsync(hacpackExe, $"-k \"{keysFile}\" --type nca --ncatype control --titleid {modTidHex} --romfsdir \"{romfsControlDir}\" -o \"{outControlDir}\"", modTempDir, task, ct);
                    // 2. PublicData NCA
                    await ExternalProcessRunner.RunAsync(hacpackExe, $"-k \"{keysFile}\" --type nca --ncatype publicdata --titleid {modTidHex} --romfsdir \"{dataRomfsDir}\" -o \"{outDataDir}\"", modTempDir, task, ct);

                    var controlNcas = Directory.GetFiles(outControlDir, "*.nca");
                    var dataNcas = Directory.GetFiles(outDataDir, "*.nca");

                    if (controlNcas.Length > 0 && dataNcas.Length > 0)
                    {
                        string controlNca = controlNcas[0];
                        string publicDataNca = dataNcas[0];

                        // 3. Addon CNMT NCA
                        await ExternalProcessRunner.RunAsync(hacpackExe, $"-k \"{keysFile}\" --type nca --ncatype meta --titletype addon --titleid {modTidHex} --titleversion 0x0 --publicdatanca \"{publicDataNca}\" --controlnca \"{controlNca}\" -o \"{outMetaDir}\"", modTempDir, task, ct);
                        
                        generatedNcas.Add(controlNca);
                        generatedNcas.Add(publicDataNca);
                        generatedNcas.AddRange(Directory.GetFiles(outMetaDir, "*.nca"));
                    }

                    currentDlcIdx++;
                }

                if (hasExeFs)
                {
                    ulong modTid = baseTitleIdClean | 0x1000 | (ulong)currentDlcIdx;
                    string modTidHex = modTid.ToString("X16");

                    string exefsControlDir = Path.Combine(modTempDir, "exefs_mod_control");
                    Directory.CreateDirectory(exefsControlDir);

                    string exeFsTitle = !string.IsNullOrWhiteSpace(task.ModNameExeFs) ? task.ModNameExeFs : "Модификации: ExeFS";

                    byte[] nacp = new byte[0x4000];
                    byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(exeFsTitle);
                    byte[] devBytes = System.Text.Encoding.UTF8.GetBytes("STORM MODS");

                    for (int l = 0; l < 16; l++)
                    {
                        int titleOffset = l * 0x300;
                        int devOffset = titleOffset + 0x200;
                        Array.Copy(nameBytes, 0, nacp, titleOffset, Math.Min(nameBytes.Length, 0x200));
                        Array.Copy(devBytes, 0, nacp, devOffset, Math.Min(devBytes.Length, 0x100));
                    }
                    File.WriteAllBytes(Path.Combine(exefsControlDir, "control.nacp"), nacp);

                    if (iconBytes != null && iconBytes.Length > 0)
                    {
                        File.WriteAllBytes(Path.Combine(exefsControlDir, "icon_AmericanEnglish.dat"), iconBytes);
                        File.WriteAllBytes(Path.Combine(exefsControlDir, "icon_Russian.dat"), iconBytes);
                    }

                    string dataRomfsDir = Path.Combine(modTempDir, "exefs_mod_data");
                    Directory.CreateDirectory(dataRomfsDir);
                    File.WriteAllText(Path.Combine(dataRomfsDir, "mod.txt"), "Storm Switch Box Integrated ExeFS Mod");

                    string outControlDir = Path.Combine(modTempDir, "out_exefs_control");
                    string outDataDir = Path.Combine(modTempDir, "out_exefs_data");
                    string outMetaDir = Path.Combine(modTempDir, "out_exefs_meta");
                    Directory.CreateDirectory(outControlDir);
                    Directory.CreateDirectory(outDataDir);
                    Directory.CreateDirectory(outMetaDir);

                    // 1. Control NCA
                    await ExternalProcessRunner.RunAsync(hacpackExe, $"-k \"{keysFile}\" --type nca --ncatype control --titleid {modTidHex} --romfsdir \"{exefsControlDir}\" -o \"{outControlDir}\"", modTempDir, task, ct);
                    // 2. PublicData NCA
                    await ExternalProcessRunner.RunAsync(hacpackExe, $"-k \"{keysFile}\" --type nca --ncatype publicdata --titleid {modTidHex} --romfsdir \"{dataRomfsDir}\" -o \"{outDataDir}\"", modTempDir, task, ct);

                    var controlNcas = Directory.GetFiles(outControlDir, "*.nca");
                    var dataNcas = Directory.GetFiles(outDataDir, "*.nca");

                    if (controlNcas.Length > 0 && dataNcas.Length > 0)
                    {
                        string controlNca = controlNcas[0];
                        string publicDataNca = dataNcas[0];

                        // 3. Addon CNMT NCA
                        await ExternalProcessRunner.RunAsync(hacpackExe, $"-k \"{keysFile}\" --type nca --ncatype meta --titletype addon --titleid {modTidHex} --titleversion 0x0 --publicdatanca \"{publicDataNca}\" --controlnca \"{controlNca}\" -o \"{outMetaDir}\"", modTempDir, task, ct);
                        
                        generatedNcas.Add(controlNca);
                        generatedNcas.Add(publicDataNca);
                        generatedNcas.AddRange(Directory.GetFiles(outMetaDir, "*.nca"));
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[ModAddon] Ошибка создания метаданных модификаций: {ex.Message}", Models.LogLevel.Warning);
            }
            return generatedNcas;
        }

        public static HashSet<string> DetectDeltaNcasToExclude(List<string> nspPaths)
        {
            var deltaNcas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var nspPath in nspPaths)
            {
                if (!System.IO.File.Exists(nspPath)) continue;
                try
                {
                    using var chkStream = new FileStream(nspPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var chkFs = new PartitionFileSystem(chkStream.AsStorage());
                    foreach (var ent in chkFs.EnumerateEntries())
                    {
                        // 1. Проверка .cnmt.xml
                        if (ent.Name.EndsWith(".cnmt.xml", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                using var fRef = new UniqueRef<IFile>();
                                using var p = new LibHac.Fs.Path();
                                p.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(ent.FullPath))).ThrowIfFailure();
                                chkFs.OpenFile(ref fRef.Ref, in p, OpenMode.Read).ThrowIfFailure();
                                using var sr = new StreamReader(fRef.Release().AsStream());
                                string xml = sr.ReadToEnd();
                                var matches = System.Text.RegularExpressions.Regex.Matches(xml, @"(?s)<Content>.*?<Type>DeltaFragment</Type>.*?<Id>([a-fA-F0-9]{32})</Id>.*?</Content>");
                                foreach (System.Text.RegularExpressions.Match m in matches)
                                {
                                    if (m.Groups.Count > 1) deltaNcas.Add(m.Groups[1].Value.ToLowerInvariant() + ".nca");
                                }
                                var matches2 = System.Text.RegularExpressions.Regex.Matches(xml, @"(?s)<Content>.*?<Id>([a-fA-F0-9]{32})</Id>.*?<Type>DeltaFragment</Type>.*?</Content>");
                                foreach (System.Text.RegularExpressions.Match m in matches2)
                                {
                                    if (m.Groups.Count > 1) deltaNcas.Add(m.Groups[1].Value.ToLowerInvariant() + ".nca");
                                }
                            }
                            catch { }
                        }
                        // 2. Проверка бинарного CNMT внутри .cnmt.nca
                        else if (ent.Name.EndsWith(".cnmt.nca", StringComparison.OrdinalIgnoreCase) || 
                                 (ent.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) && ent.Name.Length >= 36))
                        {
                            try
                            {
                                using var fRef = new UniqueRef<IFile>();
                                using var p = new LibHac.Fs.Path();
                                p.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(ent.FullPath))).ThrowIfFailure();
                                if (chkFs.OpenFile(ref fRef.Ref, in p, OpenMode.Read).IsSuccess())
                                {
                                    var ncaStorage = fRef.Release().AsStorage();
                                    var nca = new Nca(App.Keys.CurrentKeyset, ncaStorage);
                                    if (nca.Header.ContentType == NcaContentType.Meta)
                                    {
                                        IFileSystem? romfs = null;
                                        try { romfs = nca.OpenFileSystem(0, IntegrityCheckLevel.None); } catch { }
                                        if (romfs == null)
                                        {
                                            try { romfs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None); } catch { }
                                        }

                                        if (romfs != null)
                                        {
                                            foreach (var f in romfs.EnumerateEntries())
                                            {
                                                if (f.Name.EndsWith(".cnmt", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    using var cfRef = new UniqueRef<IFile>();
                                                    using var cp = new LibHac.Fs.Path();
                                                    cp.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(f.FullPath))).ThrowIfFailure();
                                                    if (romfs.OpenFile(ref cfRef.Ref, in cp, OpenMode.Read).IsSuccess())
                                                    {
                                                        using var ms = new MemoryStream();
                                                        cfRef.Release().AsStream().CopyTo(ms);
                                                        ms.Position = 0;
                                                        var cnmt = new LibHac.Tools.Ncm.Cnmt(ms);
                                                        foreach (var cEntry in cnmt.ContentEntries)
                                                        {
                                                            if (cEntry.Type == LibHac.Ncm.ContentType.DeltaFragment && cEntry.NcaId != null)
                                                            {
                                                                string hex = Convert.ToHexString(cEntry.NcaId).ToLowerInvariant();
                                                                deltaNcas.Add(hex + ".nca");
                                                            }
                                                        }
                                                    }
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
            return deltaNcas;
        }
    }
}
