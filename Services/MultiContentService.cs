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
            string intermediatePath = outPath;
            bool isCompressedFormat = task.TargetFormat.Equals("NSZ", StringComparison.OrdinalIgnoreCase) || task.TargetFormat.Equals("XCZ", StringComparison.OrdinalIgnoreCase);
            if (isCompressedFormat)
            {
                string intermediateExt = task.TargetFormat.Equals("XCZ", StringComparison.OrdinalIgnoreCase) ? ".xci" : ".nsp";
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
                string appDrive = System.IO.Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory) ?? "C:\\";
                if (targetDrive.Equals(appDrive, StringComparison.OrdinalIgnoreCase))
                {
                    string appDirTemp = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
                    tempDecompDir = System.IO.Path.Combine(appDirTemp, "StormDecomp_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                }
                else
                {
                    tempDecompDir = System.IO.Path.Combine(string.IsNullOrEmpty(targetDir) ? System.IO.Path.GetTempPath() : targetDir, "StormDecomp_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                }
                Directory.CreateDirectory(tempDecompDir);

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

                string listFile = System.IO.Path.Combine(tempDecompDir, $"list_conv_{Guid.NewGuid().ToString("N").Substring(0, 8)}.txt");
                System.IO.File.WriteAllLines(listFile, finalInputFilesList, new System.Text.UTF8Encoding(false));

                bool hasMods = finalInputFilesList.Any(d => Directory.Exists(d) && 
                    (System.IO.Path.GetFileName(d).Equals("romfs", StringComparison.OrdinalIgnoreCase) || 
                     System.IO.Path.GetFileName(d).Equals("exefs", StringComparison.OrdinalIgnoreCase)));

                // Путь к оригинальному Update NSP — нужен в LibHac fallback для Patch CNMT и тикетов
                string? savedUpdateFile = null;

                // Патчинг прошивки (пересборка base+update через yanu-cli)
                // Пропускаем для мульти-программных тайтлов (напр. AC Ezio Collection)
                bool skipHardPatch = task.IsMultiProgramTitle;
                if ((patchFirmware || hasMods) && !skipHardPatch)
                {
                    App.RunOnUI(() =>
                    {
                        task.LogDetails += hasMods 
                            ? "\n🔵 [HardPatch] Обнаружены папки модов (romfs/exefs). Запуск распаковки и пересборки..." 
                            : "\n🔵 [HardPatch] Поиск Base и Update...";
                    });
                    
                    string? baseFile = null;
                    string? updateFile = null;
                    
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
                            if (tid.EndsWith("000") && string.IsNullOrEmpty(baseFile)) baseFile = f;
                            else if (tid.EndsWith("800") && string.IsNullOrEmpty(updateFile)) updateFile = f;
                        }
                    }
                    
                    if (string.IsNullOrEmpty(baseFile)) baseFile = finalInputFilesList.FirstOrDefault(f => !Directory.Exists(f) && !f.Contains("DLC", StringComparison.OrdinalIgnoreCase) && (f.Contains("[v0]") || f.Contains("v0"))) ?? finalInputFilesList.FirstOrDefault(f => !Directory.Exists(f) && !f.Contains("DLC", StringComparison.OrdinalIgnoreCase) && !f.Contains("v")) ?? "";
                    if (string.IsNullOrEmpty(updateFile)) updateFile = finalInputFilesList.FirstOrDefault(f => !Directory.Exists(f) && f != baseFile && !f.Contains("DLC", StringComparison.OrdinalIgnoreCase) && (f.Contains("v") && !f.Contains("v0"))) ?? "";
                    
                    if (!string.IsNullOrEmpty(baseFile) && (!string.IsNullOrEmpty(updateFile) || hasMods))
                    {
                        App.RunOnUI(() => task.LogDetails += "\n🔵 [HardPatch] Физическая пересборка...");
                        string titleIdStr = "";
                        try {
                            titleIdStr = App.SwitchFormat.ParseNsp(baseFile).TitleId;
                        } catch { }
                        if (string.IsNullOrEmpty(titleIdStr)) {
                            var match = System.Text.RegularExpressions.Regex.Match(baseFile, @"\[([0-9A-Fa-f]{16})\]");
                            if (match.Success) titleIdStr = match.Groups[1].Value;
                        }
                        string suffix = string.IsNullOrEmpty(titleIdStr) ? "" : $"_[{titleIdStr}][v0]";
                        string tempHardPatchedNsp = System.IO.Path.Combine(tempDecompDir, $"patched_base{suffix}.nsp");
                        
                        var hpInput = new List<string> { baseFile };
                        if (!string.IsNullOrEmpty(updateFile)) hpInput.Add(updateFile);
                        
                        // Add mod directories (romfs/exefs) to be processed
                        var modDirs = finalInputFilesList.Where(d => Directory.Exists(d)).ToList();
                        hpInput.AddRange(modDirs);

                        await App.HardPatch.PatchUpdateAsync(task, hpInput, tempHardPatchedNsp, cancellationToken, isMultiContent: true);
                        
                        if (System.IO.File.Exists(tempHardPatchedNsp))
                        {
                            // Валидация: проверяем, что patched_base не потерял исполняемые под-программы (Program NCA > 100MB).
                            // Мульти-программные сборники (напр. AC Ezio Collection) содержат несколько Program NCA (>100MB)
                            // для разных субигр в одном NSP. yanu-cli обрабатывает только основной TitleID и теряет остальные субигры.
                            int originalProgramNcaCount = 0;
                            int patchedProgramNcaCount = 0;
                            try
                            {
                                // Считаем Program NCA (>100MB) в оригинальном base
                                using (var bs = new FileStream(baseFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                                using (var bfs = new PartitionFileSystem(bs.AsStorage()))
                                {
                                    foreach (var e in bfs.EnumerateEntries())
                                    {
                                        if (e.Type == LibHac.Fs.DirectoryEntryType.Directory) continue;
                                        if (e.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) || 
                                            e.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase))
                                        {
                                            var f = OpenFileSafe(bfs, "/" + e.Name);
                                            f.GetSize(out long size);
                                            f.Dispose();
                                            if (size > 100 * 1024 * 1024)
                                                originalProgramNcaCount++;
                                        }
                                    }
                                }
                                // Считаем Program NCA (>100MB) в patched_base
                                using (var ps = new FileStream(tempHardPatchedNsp, FileMode.Open, FileAccess.Read, FileShare.Read))
                                using (var pfs = new PartitionFileSystem(ps.AsStorage()))
                                {
                                    foreach (var e in pfs.EnumerateEntries())
                                    {
                                        if (e.Type == LibHac.Fs.DirectoryEntryType.Directory) continue;
                                        if (e.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) || 
                                            e.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase))
                                        {
                                            var f = OpenFileSafe(pfs, "/" + e.Name);
                                            f.GetSize(out long size);
                                            f.Dispose();
                                            if (size > 100 * 1024 * 1024)
                                                patchedProgramNcaCount++;
                                        }
                                    }
                                }
                            }
                            catch { }

                            App.Logger.Log($"[HardPatch] Program NCA (>100MB) validation: original={originalProgramNcaCount}, patched={patchedProgramNcaCount}", Models.LogLevel.Info);

                            if (originalProgramNcaCount > 1 && patchedProgramNcaCount < originalProgramNcaCount)
                            {
                                // patched_base потерял под-программы (мульти-программный тайтл)
                                // Отменяем HardPatch и используем оригинальные файлы
                                App.RunOnUI(() => task.LogDetails += $"\n⚠️ [HardPatch] Мульти-программный тайтл ({originalProgramNcaCount} игр → {patchedProgramNcaCount}). Используем оригинальные файлы.");
                                App.Logger.Log($"[HardPatch] Multi-program title detected: {originalProgramNcaCount} -> {patchedProgramNcaCount}. Discarding patched_base, using originals.", Models.LogLevel.Warning);
                                try { System.IO.File.Delete(tempHardPatchedNsp); } catch { }
                                
                                // Сохраняем Update для LibHac fallback (Patch CNMT + тикеты)
                                if (!string.IsNullOrEmpty(updateFile) && System.IO.File.Exists(updateFile))
                                    savedUpdateFile = updateFile;
                            }
                            else
                            {
                                // Нормальный тайтл — используем patched_base
                                if (!string.IsNullOrEmpty(updateFile) && System.IO.File.Exists(updateFile))
                                    savedUpdateFile = updateFile;

                                finalInputFilesList.Remove(baseFile);
                                if (!string.IsNullOrEmpty(updateFile)) finalInputFilesList.Remove(updateFile);
                                foreach (var mod in modDirs)
                                {
                                    finalInputFilesList.Remove(mod);
                                }
                                finalInputFilesList.Add(tempHardPatchedNsp);
                                App.RunOnUI(() => task.LogDetails += "\n🔵 [HardPatch] Успешно завершено.");
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

                // 4.5 Сшивание мультиконтента через NSC_Builder (squirrel.exe)
                App.RunOnUI(() =>
                {
                    task.LogDetails += "\n📦 [NSC_Builder] Сшивание мультиконтента...";
                    task.Status = "Сборка...";
                });

                bool isTargetXci = task.TargetFormat.Equals("XCI", StringComparison.OrdinalIgnoreCase) || task.TargetFormat.Equals("XCZ", StringComparison.OrdinalIgnoreCase);
                string actualIntermediatePath = intermediatePath;
                if (isTargetXci)
                {
                    // squirrel can generate XCI directly, so we just use intermediatePath directly
                    // no need for temp.nsp wrapping.
                }

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

                // Ищем рабочий каталог утилиты NSC_Builder
                string nscbDir = System.IO.Path.Combine(toolsDir, "nscb");
                string squirrelExe = System.IO.Path.Combine(nscbDir, "ztools", "squirrel.exe");

                if (!System.IO.File.Exists(squirrelExe))
                    throw new Exception($"NSC_Builder (squirrel.exe) не найден по пути: {squirrelExe}");

                string isolatedUserProfile = System.IO.Path.Combine(toolsDir, "keys");
                string isolatedLocalAppData = System.IO.Path.Combine(toolsDir, "cache");

                string userProfileSwitch = System.IO.Path.Combine(isolatedUserProfile, ".switch");
                string userProfileKeys = System.IO.Path.Combine(userProfileSwitch, "prod.keys");
                string userProfileKeysTxt = System.IO.Path.Combine(userProfileSwitch, "keys.txt");
                string realProfileSwitch = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch");
                string realProfileKeys = System.IO.Path.Combine(realProfileSwitch, "prod.keys");
                string realProfileKeysTxt = System.IO.Path.Combine(realProfileSwitch, "keys.txt");
                string squirrelKeys1 = System.IO.Path.Combine(nscbDir, "keys.txt");
                string squirrelKeys2 = System.IO.Path.Combine(nscbDir, "ztools", "keys.txt");

                lock (typeof(HardPatchEngine)) // Using type lock for safety as _keysLock is private
                {
                    try
                    {
                        if (!Directory.Exists(userProfileSwitch)) Directory.CreateDirectory(userProfileSwitch);
                        if (!Directory.Exists(realProfileSwitch)) Directory.CreateDirectory(realProfileSwitch);
                        if (!Directory.Exists(isolatedLocalAppData)) Directory.CreateDirectory(isolatedLocalAppData);

                        if (!string.IsNullOrEmpty(App.Settings.Current.KeysPath) && System.IO.File.Exists(App.Settings.Current.KeysPath))
                        {
                            App.SwitchFormat.CleanKeysFile(App.Settings.Current.KeysPath);
                            System.IO.File.Copy(App.Settings.Current.KeysPath, userProfileKeys, true);
                            System.IO.File.Copy(App.Settings.Current.KeysPath, userProfileKeysTxt, true);
                            System.IO.File.Copy(App.Settings.Current.KeysPath, realProfileKeys, true);
                            System.IO.File.Copy(App.Settings.Current.KeysPath, realProfileKeysTxt, true);
                            System.IO.File.Copy(App.Settings.Current.KeysPath, squirrelKeys1, true);
                            System.IO.File.Copy(App.Settings.Current.KeysPath, squirrelKeys2, true);

                            App.SwitchFormat.CleanKeysFile(userProfileKeys);
                            App.SwitchFormat.CleanKeysFile(userProfileKeysTxt);
                            App.SwitchFormat.CleanKeysFile(realProfileKeys);
                            App.SwitchFormat.CleanKeysFile(realProfileKeysTxt);
                            App.SwitchFormat.CleanKeysFile(squirrelKeys1);
                            App.SwitchFormat.CleanKeysFile(squirrelKeys2);
                        }
                    }
                    catch { }
                }

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

                string fmt = isTargetXci ? "xci" : "cnsp";
                string outFolder = System.IO.Path.Combine(tempDecompDir, "nscb_out");
                Directory.CreateDirectory(outFolder);

                bool buildDone = false;

                if (System.IO.File.Exists(squirrelExe))
                {
                    try
                    {
                        var safeSortedList = new List<string>();
                        for (int i = 0; i < sortedList.Count; i++)
                        {
                            string fPath = sortedList[i];
                            string ext = System.IO.Path.GetExtension(fPath).ToLowerInvariant();
                            string origFileName = System.IO.Path.GetFileName(fPath);

                            var tags = new List<string>();
                            var tidMatch = System.Text.RegularExpressions.Regex.Match(origFileName, @"\[([0-9a-fA-F]{16})\]");
                            if (tidMatch.Success) tags.Add(tidMatch.Value);
                            var verMatch = System.Text.RegularExpressions.Regex.Match(origFileName, @"\[v\d+\]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (verMatch.Success) tags.Add(verMatch.Value);

                            string tagSuffix = tags.Count > 0 ? "_" + string.Join("", tags) : "";

                            if (ext == ".nsz" || ext == ".xcz" || ext == ".xci")
                            {
                                string nspName = $"src_{i}{tagSuffix}.nsp";
                                string targetNspPath = System.IO.Path.Combine(tempDecompDir, nspName);
                                string itemDecompDir = System.IO.Path.Combine(tempDecompDir, $"decomp_{i}");
                                Directory.CreateDirectory(itemDecompDir);

                                App.RunOnUI(() => task.LogDetails += $"\n📦 [NSC_Builder] Распаковка {System.IO.Path.GetFileName(fPath)} -> {nspName}...");

                                string? decompResult = await App.NszCompression.DecompressNszAsync(task, fPath, itemDecompDir, cancellationToken);
                                if (decompResult != null)
                                {
                                    var producedNsp = new DirectoryInfo(itemDecompDir).GetFiles("*.nsp")
                                        .OrderByDescending(f => f.Length)
                                        .FirstOrDefault();

                                    if (producedNsp != null)
                                    {
                                        if (File.Exists(targetNspPath)) try { File.Delete(targetNspPath); } catch { }
                                        File.Move(producedNsp.FullName, targetNspPath);
                                        try { Directory.Delete(itemDecompDir, true); } catch { }
                                        safeSortedList.Add(targetNspPath);
                                        continue;
                                    }
                                }
                                try { Directory.Delete(itemDecompDir, true); } catch { }
                            }

                            string safeFileName = $"src_{i}{tagSuffix}.nsp";
                            string safePath = System.IO.Path.Combine(tempDecompDir, safeFileName);
                            if (File.Exists(safePath)) try { File.Delete(safePath); } catch { }
                            bool linked = false;
                            try { linked = CreateHardLink(safePath, fPath, IntPtr.Zero); } catch { }
                            if (!linked)
                            {
                                try { File.Copy(fPath, safePath, true); linked = true; } catch { }
                            }

                            safeSortedList.Add(linked ? safePath : fPath);
                        }

                        string mlistFile = System.IO.Path.Combine(tempDecompDir, "mlist.txt");
                        // Старая версия 0.1.007 использовала ASCII для mlist —
                        // squirrel.exe (Python 3.7) может не корректно читать UTF-8 BOM
                        System.IO.File.WriteAllLines(mlistFile, safeSortedList, System.Text.Encoding.ASCII);

                        // Аргументы из рабочей версии 0.1.007 + динамические настройки:
                        string fatMode = App.Settings.Current.SplitFat32 ? "fat32" : "exfat";
                        string ndFlag = App.Settings.Current.RemoveDeltaNca ? "true" : "false";
                        bool hasUnlocker = sortedList.Any(f => System.IO.Path.GetFileName(f).Contains("unlocker", StringComparison.OrdinalIgnoreCase));
                        bool shouldRemoveTitlerights = App.Settings.Current.RemoveTitlerights && !hasUnlocker;

                        if (App.Settings.Current.RemoveTitlerights && hasUnlocker)
                        {
                            App.Logger.Log("[NSC_Builder] Обнаружен DLC Unlocker: сохраняем билеты (.tik) для гарантированной разблокировки контента.", Models.LogLevel.Info);
                            App.RunOnUI(() => task.LogDetails += "\nℹ️ [NSC_Builder] Обнаружен Unlocker: сохраняем билеты (.tik) для разблокировки контента.");
                        }

                        string cleanFlag = shouldRemoveTitlerights ? " --C_clean_ND true" : "";
                        string romaFlag = shouldRemoveTitlerights ? "TRUE" : "FALSE";
                        int keyGen = App.Settings.Current.KeyGeneration;
                        string kpFlag = (keyGen >= 0 && keyGen <= 30) ? keyGen.ToString() : "false";
                        string pvFlag = (keyGen >= 0 && keyGen < 19) ? "true" : "false";
                        int rsvCapVal = App.Settings.Current.EnableRsvCap ? App.Settings.Current.RsvCap : 268435656;
                        string args = $"-b 65536 -pv {pvFlag} -kp {kpFlag} --RSVcap {rsvCapVal} -fat {fatMode} -fx files -ND {ndFlag} -roma {romaFlag}{cleanFlag} -t {fmt} -o \"{outFolder}\" -tfile \"{mlistFile}\" -dmul \"calculate\"";
                        
                        App.Logger.Log($"[squirrel] args: {args}", Models.LogLevel.Info);


                        string ztoolsDir = System.IO.Path.Combine(nscbDir, "ztools");
                        App.UnblockFile(squirrelExe);

                        App.RunOnUI(() => task.LogDetails += "\n📦 [NSC_Builder] Запуск squirrel.exe (-dmul calculate)...");

                        int exitCode = -1;
                        try
                        {
                            exitCode = await ExternalProcessRunner.RunAsync(
                                squirrelExe,
                                args,
                                ztoolsDir,
                                task,
                                cancellationToken,
                                isolatedUserProfile,
                                isolatedLocalAppData,
                                forceUtf8Console: true
                            );
                        }
                        catch (Exception ex)
                        {
                            App.Logger.Log($"[squirrel] launch error: {ex.Message}", Models.LogLevel.Warning);
                            App.RunOnUI(() => task.LogDetails += $"\n⚠️ [NSC_Builder] Ошибка запуска squirrel.exe ({ex.Message}).");
                        }

                        App.Logger.Log($"[squirrel] exit code: {exitCode}", Models.LogLevel.Info);

                        if (exitCode == 0)
                        {
                            // Валидация вывода: squirrel может вернуть 0 но создать
                            // некорректный NSP (без Control NCA, с битыми CNMT и т.д.)
                            string[] checkExts = { ".nsp", ".xci" };
                            string? squirrelOut = Directory.GetFiles(outFolder)
                                .Where(f => checkExts.Any(e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase)))
                                .OrderByDescending(f => new FileInfo(f).Length)
                                .FirstOrDefault();

                            bool valid = false;
                            if (!string.IsNullOrEmpty(squirrelOut) && File.Exists(squirrelOut))
                            {
                                try
                                {
                                    // Собираем все уникальные TitleID из входных файлов
                                    var expectedTitleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                    foreach (var sf in sortedList)
                                    {
                                        if (Directory.Exists(sf)) continue;
                                        var m = System.Text.RegularExpressions.Regex.Match(
                                            System.IO.Path.GetFileName(sf), @"\[([0-9a-fA-F]{16})\]");
                                        if (m.Success) expectedTitleIds.Add(m.Groups[1].Value.ToUpperInvariant());
                                        // Также пытаемся из парсинга NSP
                                        try
                                        {
                                            var pInfo = App.SwitchFormat.ParseNsp(sf);
                                            if (!string.IsNullOrEmpty(pInfo.TitleId))
                                                expectedTitleIds.Add(pInfo.TitleId.Trim().ToUpperInvariant());
                                        }
                                        catch { }
                                    }

                                    using var checkStream = new FileStream(squirrelOut, FileMode.Open, FileAccess.Read, FileShare.Read);
                                    using var checkPfs = new PartitionFileSystem(checkStream.AsStorage());

                                    int cnmtCount = 0, controlCount = 0, totalNca = 0, tikCount = 0;
                                    var foundTitleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                                    foreach (var entry in checkPfs.EnumerateEntries())
                                    {
                                        if (entry.Type == LibHac.Fs.DirectoryEntryType.Directory) continue;
                                        string name = entry.Name;

                                        if (name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase))
                                        {
                                            tikCount++;
                                            continue;
                                        }

                                        if (!name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase)) continue;

                                        totalNca++;
                                        if (name.EndsWith(".cnmt.nca", StringComparison.OrdinalIgnoreCase))
                                        {
                                            cnmtCount++;
                                            // Пытаемся извлечь TitleID из CNMT NCA
                                            try
                                            {
                                                var ncaFile = OpenFileSafe(checkPfs, "/" + name);
                                                using var ncaStorage = ncaFile.AsStorage();
                                                // Первые 16 байт заголовка NCA содержат сигнатуру,
                                                // TitleID обычно по смещению 0x210 (в зашифрованных NCA)
                                                // Используем имя файла CNMT — в правильном NSP,
                                                // squirrel включает TitleID в заголовок
                                                ncaStorage.GetSize(out long cnmtSize);
                                                if (cnmtSize > 0)
                                                {
                                                    // Каждый CNMT NCA = один Title
                                                    foundTitleIds.Add($"cnmt_{cnmtCount}");
                                                }
                                            }
                                            catch { }
                                        }
                                        else
                                        {
                                            try
                                            {
                                                var ncaFile = OpenFileSafe(checkPfs, "/" + name);
                                                using var ncaStorage = ncaFile.AsStorage();
                                                ncaStorage.GetSize(out long ncaSize);
                                                // Control NCA обычно < 10 MB
                                                if (ncaSize > 0 && ncaSize < 10 * 1024 * 1024)
                                                    controlCount++;
                                            }
                                            catch { }
                                        }
                                    }

                                    // Считаем ожидаемое количество тикетов из входных файлов
                                    int expectedTikCount = 0;
                                    foreach (var sf in sortedList)
                                    {
                                        if (Directory.Exists(sf) || !File.Exists(sf)) continue;
                                        try
                                        {
                                            using var tikCheckStream = new FileStream(sf, FileMode.Open, FileAccess.Read, FileShare.Read);
                                            using var tikCheckPfs = new PartitionFileSystem(tikCheckStream.AsStorage());
                                            foreach (var tikEntry in tikCheckPfs.EnumerateEntries())
                                            {
                                                if (tikEntry.Type == LibHac.Fs.DirectoryEntryType.Directory) continue;
                                                if (tikEntry.Name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase))
                                                    expectedTikCount++;
                                            }
                                        }
                                        catch { }
                                    }

                                    // Валидация вывода squirrel.exe:
                                    // 1. CNMT для каждого Title (base + update + каждый DLC)
                                    int expectedCnmtCount = sortedList.Count(f => !Directory.Exists(f));
                                    // 2. Минимум 1 Control NCA (для иконки/названия)
                                    // 3. Достаточно NCA в целом
                                    int expectedMinNca = Math.Max(3, sortedList.Count);
                                    
                                    // squirrel.exe с аргументом -roma TRUE очищает тикеты (.tik),
                                    // поэтому отсутствие .tik на выходе — нормальное поведение и не должно браковать сборку.
                                    valid = cnmtCount >= expectedCnmtCount && 
                                            controlCount >= 1 && 
                                            totalNca >= expectedMinNca;

                                    App.Logger.Log($"[squirrel] validation: cnmt={cnmtCount}/{expectedCnmtCount}, control-like={controlCount}, total={totalNca}, tik={tikCount}/{expectedTikCount}, expected>={expectedMinNca}, valid={valid}", Models.LogLevel.Info);
                                }
                                catch (Exception vex)
                                {
                                    App.Logger.Log($"[squirrel] validation error: {vex.Message}", Models.LogLevel.Warning);
                                }
                            }

                            if (valid)
                            {
                                buildDone = true;
                            }
                            else
                            {
                                App.Logger.Log("[squirrel] output validation failed — fallback to LibHac", Models.LogLevel.Warning);
                                // Удаляем некорректный файл squirrel'а
                                if (!string.IsNullOrEmpty(squirrelOut) && File.Exists(squirrelOut))
                                    try { File.Delete(squirrelOut); } catch { }
                                App.RunOnUI(() => task.LogDetails += "\n⚠️ [NSC_Builder] Вывод squirrel.exe не прошёл валидацию (недостаточно CNMT/NCA). Переход на нативную сборку C# (LibHac)...");
                            }
                        }
                        else
                        {
                            string reason = exitCode == 2 ? "ошибка аргументов командной строки" : exitCode == 1 ? "ошибка выполнения" : exitCode == -1 ? "не удалось запустить (возможно, заблокирован Device Guard)" : $"неизвестная ошибка";
                            App.RunOnUI(() => task.LogDetails += $"\n⚠️ [NSC_Builder] squirrel.exe завершился с кодом {exitCode} ({reason}). Переход на нативную сборку C# (LibHac)...");
                        }
                    }
                    catch (Exception ex)
                    {
                        App.RunOnUI(() => task.LogDetails += $"\n⚠️ [NSC_Builder] Запуск squirrel.exe недоступен ({ex.Message}). Переход на нативную сборку C# (LibHac)...");
                    }
                }

                // Fallback to native LibHac PFS0 assembly if squirrel.exe failed or was blocked
                if (!buildDone && !isTargetXci)
                {
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
                            // Добавляем оригинальный Update NSP только если HardPatch НЕ создавал patched_base (например при сбое/пропуске)
                            bool hasPatchedBase = scanList.Any(f => f.Contains("patched_base", StringComparison.OrdinalIgnoreCase));
                            if (!hasPatchedBase && !string.IsNullOrEmpty(savedUpdateFile) && System.IO.File.Exists(savedUpdateFile) 
                                && !scanList.Contains(savedUpdateFile, StringComparer.OrdinalIgnoreCase))
                            {
                                scanList.Add(savedUpdateFile);
                                App.Logger.Log($"[LibHac] Добавлен оригинальный Update для Patch CNMT: {System.IO.Path.GetFileName(savedUpdateFile)}", Models.LogLevel.Info);
                            }

                            foreach (string nspPath in scanList)
                            {
                                if (!System.IO.File.Exists(nspPath)) continue;
                                
                                var stream = new FileStream(nspPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                                openedStreams.Add(stream);
                                var fs = new PartitionFileSystem(stream.AsStorage());
                                openedFs.Add(fs);
                                
                                foreach (var entry in fs.EnumerateEntries())
                                {
                                    if (entry.Type == LibHac.Fs.DirectoryEntryType.Directory) continue;
                                    string name = entry.Name;
                                    
                                    if (mergedEntries.ContainsKey(name) || !IsValidNspEntry(name)) continue;

                                    var file = OpenFileSafe(fs, entry.FullPath);
                                    
                                    openedFiles.Add(file);
                                    mergedEntries[name] = file;
                                }
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

                            var orderedEntries = mergedEntries
                                .OrderBy(kvp => GetNcaPriority(kvp.Value, kvp.Key, baseTitleId))
                                .ThenBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);

                            foreach (var kvp in orderedEntries)
                            {
                                pfsBuilder.AddFile(kvp.Key, new LibHac.FsSystem.StorageFile(new StormSwitchBox.Services.SafeStorageWrapper(kvp.Value.AsStorage()), LibHac.Fs.OpenMode.Read));
                            }

                            string outputNspPath = System.IO.Path.Combine(outFolder, $"multi_out_{Guid.NewGuid().ToString("N").Substring(0, 8)}.nsp");

                            using (var builtPfs = pfsBuilder.Build(PartitionFileSystemType.Standard))
                            {
                                builtPfs.GetSize(out long totalPfsSize).ThrowIfFailure();
                                
                                using var destStream = new FileStream(outputNspPath, FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024 * 1024);
                                long remaining = totalPfsSize;
                                long offset = 0;
                                byte[] buffer = new byte[8 * 1024 * 1024];
                                var sw = System.Diagnostics.Stopwatch.StartNew();
                                
                                while (remaining > 0)
                                {
                                    cancellationToken.ThrowIfCancellationRequested();
                                    int toRead = (int)Math.Min(buffer.Length, remaining);
                                    builtPfs.Read(offset, buffer.AsSpan(0, toRead)).ThrowIfFailure();
                                    destStream.Write(buffer, 0, toRead);
                                    offset += toRead;
                                    remaining -= toRead;
                                    
                                    if (sw.ElapsedMilliseconds > 300 || remaining == 0)
                                    {
                                        sw.Restart();
                                        double pct = (double)offset / totalPfsSize * 100.0;
                                        App.RunOnUI(() => task.Progress = Math.Min(99.9, pct));
                                    }
                                }
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

            FinalizeAssembly:
                string formattedPath = FormatOutputFileName(outPath, inputFiles);
                if (!string.IsNullOrEmpty(formattedPath) && !formattedPath.Equals(outPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (intermediatePath.Equals(outPath, StringComparison.OrdinalIgnoreCase))
                        intermediatePath = formattedPath;
                    outPath = formattedPath;
                }

                if (System.IO.File.Exists(generatedFile) && !generatedFile.Equals(intermediatePath, StringComparison.OrdinalIgnoreCase))
                {
                    if (System.IO.File.Exists(intermediatePath)) System.IO.File.Delete(intermediatePath);
                    System.IO.File.Move(generatedFile, intermediatePath);
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
                    
                    string ext = task.TargetFormat.Equals("XCZ", StringComparison.OrdinalIgnoreCase) ? ".xcz" : ".nsz";
                    string expectedNsz = System.IO.Path.ChangeExtension(intermediatePath, ext);
                    string finalCompressedPath = System.IO.Path.ChangeExtension(outPath, ext);
                    
                    // Also check for NSZ/XCZ in targetDir with same filename
                    if (!System.IO.File.Exists(expectedNsz))
                    {
                        string altNsz = System.IO.Path.Combine(targetDir, System.IO.Path.GetFileNameWithoutExtension(intermediatePath) + ext);
                        if (System.IO.File.Exists(altNsz)) expectedNsz = altNsz;
                    }
                    
                    bool compressionSuccess = false;
                    if (System.IO.File.Exists(expectedNsz) && new FileInfo(expectedNsz).Length > 0)
                    {
                        if (ext == ".xcz" || ext == ".nsz")
                        {
                            if (!expectedNsz.Equals(finalCompressedPath, StringComparison.OrdinalIgnoreCase))
                            {
                                if (System.IO.File.Exists(finalCompressedPath)) System.IO.File.Delete(finalCompressedPath);
                                System.IO.File.Move(expectedNsz, finalCompressedPath);
                            }
                            compressionSuccess = true;
                        }
                    }
                    
                    if (compressionSuccess)
                    {
                        try { if (System.IO.File.Exists(intermediatePath)) System.IO.File.Delete(intermediatePath); } catch { }
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
                    task.LogDetails += $"\n✅ [Готово] Сохранен: {System.IO.Path.GetFileName(outPath)}";
                    StormSwitchBox.Services.HistoryService.AddToHistory(task);
                });

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
                if (!string.IsNullOrEmpty(tempDecompDir) && System.IO.Directory.Exists(tempDecompDir))
                {
                    for (int i = 0; i < 3; i++)
                    {
                        try 
                        { 
                            System.IO.Directory.Delete(tempDecompDir, true); 
                            break;
                        } 
                        catch 
                        { 
                            System.Threading.Thread.Sleep(500); 
                        }
                    }
                }
                
                // Ensure intermediatePath is removed if it wasn't the final output
                if (intermediatePath != outPath && !string.IsNullOrEmpty(intermediatePath) && System.IO.File.Exists(intermediatePath))
                {
                    try { System.IO.File.Delete(intermediatePath); } catch { }
                }
            }
        }
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        private string PrepareSafeFileForTemp(string sourcePath, string tempDir)
        {
            if (Directory.Exists(sourcePath)) return sourcePath;

            string origName = System.IO.Path.GetFileName(sourcePath);
            string safeName = NszCompressionService.SanitizeFileName(origName);

            string destPath = System.IO.Path.Combine(tempDir, safeName);
            if (sourcePath.Equals(destPath, StringComparison.OrdinalIgnoreCase)) return sourcePath;

            if (!File.Exists(destPath))
            {
                try
                {
                    File.Copy(sourcePath, destPath, true);
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

        private static string FormatOutputFileName(string originalOutPath, List<string> allInputFiles)
        {
            string targetDir = System.IO.Path.GetDirectoryName(originalOutPath) ?? "";
            string origFileName = System.IO.Path.GetFileNameWithoutExtension(originalOutPath);
            string ext = System.IO.Path.GetExtension(originalOutPath);

            string titleId = "";
            string patchVer = "";
            int gameCount = 0;
            int updateCount = 0;
            int dlcCount = 0;

            foreach (var f in allInputFiles)
            {
                if (System.IO.Directory.Exists(f)) continue;

                string fname = System.IO.Path.GetFileName(f);
                string tid = "";
                var matchTid = System.Text.RegularExpressions.Regex.Match(fname, @"\[([0-9A-Fa-f]{16})\]");
                if (matchTid.Success) tid = matchTid.Groups[1].Value.ToUpperInvariant();

                bool isDlc = (!string.IsNullOrEmpty(tid) && tid.Length == 16 && !tid.EndsWith("000") && !tid.EndsWith("800")) ||
                             fname.Contains("DLC", StringComparison.OrdinalIgnoreCase) ||
                             fname.Contains("AddOn", StringComparison.OrdinalIgnoreCase);

                bool isPatch = (!string.IsNullOrEmpty(tid) && tid.Length == 16 && tid.EndsWith("800")) ||
                               (fname.Contains("[v") && !fname.Contains("[v0]")) ||
                               fname.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
                               fname.Contains("Patch", StringComparison.OrdinalIgnoreCase);

                bool isBase = (!string.IsNullOrEmpty(tid) && tid.Length == 16 && tid.EndsWith("000")) ||
                              fname.Contains("[v0]") || fname.EndsWith("v0.nsp", StringComparison.OrdinalIgnoreCase) ||
                              fname.Contains("patched_base");

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

            // Удаляем только тег содержимого (1G+1U+5D) — он всегда пересчитывается
            baseGameTitle = System.Text.RegularExpressions.Regex.Replace(baseGameTitle, @"\s*\(\d+G(?:\+\d+U)?(?:\+\d+D)?\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

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

            if (parts.Count > 0)
            {
                sb.Append($" ({string.Join("+", parts)})");
            }

            sb.Append(ext);
            string newFileName = NszCompressionService.SanitizeFinalOutputFileName(sb.ToString());
            return System.IO.Path.Combine(targetDir, newFileName);
        }

        private int GetNcaPriority(LibHac.Fs.Fsa.IFile file, string fileName, ulong baseTitleId)
        {
            string lower = fileName.ToLowerInvariant();

            if (lower.EndsWith(".tik")) return 90;
            if (lower.EndsWith(".cert")) return 91;

            if (lower.EndsWith(".cnmt.xml"))
            {
                if (lower.Contains("000") || lower.Contains("base")) return 0;
                if (lower.Contains("800") || lower.Contains("update")) return 10;
                return 50;
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
                bool isUpdateTitle = tid.ToString("X16").EndsWith("800");
                bool isMainGameTitle = isBaseTitle || isUpdateTitle;

                if (type == LibHac.Tools.FsSystem.NcaUtils.NcaContentType.Meta) // CNMT
                {
                    if (isUpdateTitle) return 0; // Update CNMT (has highest version v196608) FIRST (Priority 0)
                    if (isBaseTitle) return 0; // Base Game CNMT FIRST (Priority 0)
                    return 50; // DLC CNMT (Priority 50)
                }

                if (type == LibHac.Tools.FsSystem.NcaUtils.NcaContentType.Control) // Icon artwork & Title strings
                {
                    if (isMainGameTitle) return 1; // Main Game Control NCA SECOND (Priority 1)
                    return 51;
                }

                if (type == LibHac.Tools.FsSystem.NcaUtils.NcaContentType.Program) // Executable code
                {
                    if (isMainGameTitle) return 2; // Main Game Program NCA THIRD (Priority 2)
                    return 52;
                }

                if (type == LibHac.Tools.FsSystem.NcaUtils.NcaContentType.Manual) return 3;
            }
            catch
            {
                if (lower.Contains("control")) return 1;
                if (lower.Contains("program")) return 2;
            }

            return 20;
        }
    }
}
