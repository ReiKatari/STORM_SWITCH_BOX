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

                // Патчинг прошивки (пересборка) - принудительно запускаем при наличии модов romfs/exefs
                if (patchFirmware || hasMods)
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
                            finalInputFilesList.Remove(baseFile);
                            if (!string.IsNullOrEmpty(updateFile)) finalInputFilesList.Remove(updateFile);
                            foreach (var mod in modDirs)
                            {
                                finalInputFilesList.Remove(mod);
                            }
                            finalInputFilesList.Add(tempHardPatchedNsp);
                            App.RunOnUI(() => task.LogDetails += "\n🔵 [HardPatch] Успешно завершено.");
                        }
                        else 
                        {
                            App.RunOnUI(() => task.LogDetails += "\nℹ️ Пересборка HardPatch пропущена. Переходим к сшиванию мультиконтента...");
                        }
                    }
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

                        // Аргументы из рабочей версии 0.1.007:
                        // -kp false      — отключить KeyPatch generation
                        // --RSVcap 268435656 — ограничить Required System Version
                        // -fx files      — режим обработки файлов
                        // -ND true       — флаг NSCB (No Delete / preserve structure)
                        string args = $"-b 65536 -pv false -kp false --RSVcap 268435656 -fat exfat -fx files -ND true -roma TRUE -t {fmt} -o \"{outFolder}\" -tfile \"{mlistFile}\" -dmul \"calculate\"";
                        
                        App.Logger.Log($"[squirrel] args: {args}", Models.LogLevel.Info);


                        string squirrelBat = System.IO.Path.Combine(tempDecompDir, "run_squirrel.bat");
                        string ztoolsDir = System.IO.Path.Combine(nscbDir, "ztools");
                        
                        var batBuilder = new System.Text.StringBuilder();
                        batBuilder.AppendLine("@echo off");
                        batBuilder.AppendLine("chcp 65001 >nul 2>nul");
                        batBuilder.AppendLine("set PYTHONIOENCODING=utf-8");
                        batBuilder.AppendLine("set PYTHONUTF8=1");
                        batBuilder.AppendLine("set PYTHONUNBUFFERED=1");
                        batBuilder.AppendLine("set PYTHONLEGACYWINDOWSSTDIO=0");
                        batBuilder.AppendLine("set PYTHONCOERCECLOCALE=1");
                        if (!string.IsNullOrEmpty(isolatedUserProfile))
                            batBuilder.AppendLine($"set USERPROFILE={isolatedUserProfile}");
                        if (!string.IsNullOrEmpty(isolatedLocalAppData))
                            batBuilder.AppendLine($"set LOCALAPPDATA={isolatedLocalAppData}");
                        batBuilder.AppendLine($"cd /d \"{ztoolsDir}\"");
                        batBuilder.AppendLine($"\"{squirrelExe}\" {args}");
                        batBuilder.AppendLine("exit /b %errorlevel%");
                        
                        System.IO.File.WriteAllText(squirrelBat, batBuilder.ToString(), new System.Text.UTF8Encoding(false));

                        // ══════════════════════════════════════════════════════════════════
                        // UseShellExecute=true + WindowStyle=Hidden:
                        //
                        // squirrel.exe — frozen Python 3.7 (PyInstaller). Определяет
                        // кодировку sys.stdout через GetConsoleOutputCP(). Для этого stdout
                        // ОБЯЗАН быть реальной Windows-консолью (не pipe, не файл).
                        //
                        // ❌ CreateNoWindow=true → нет консоли → chcp бесполезен
                        // ❌ RedirectStandardOutput → pipe → locale cp1251
                        // ❌ > logfile 2>&1 → файл → locale cp1251
                        // ❌ PYTHONIOENCODING → игнорируется frozen PyInstaller
                        //
                        // ✅ UseShellExecute=true + Hidden → реальная скрытая консоль
                        //    chcp 65001 → GetConsoleOutputCP()=65001 → UTF-8 stdout
                        //    CJK-символы из NUTDB кодируются без ошибок.
                        // ══════════════════════════════════════════════════════════════════
                        var squirrelPsi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = squirrelBat,
                            UseShellExecute = true,
                            WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
                            WorkingDirectory = ztoolsDir
                        };

                        App.RunOnUI(() => task.LogDetails += "\n📦 [NSC_Builder] Запуск squirrel.exe (-dmul calculate)...");

                        int exitCode;
                        using (var squirrelProc = new System.Diagnostics.Process { StartInfo = squirrelPsi })
                        {
                            squirrelProc.Start();
                            
                            using var squirrelCts = cancellationToken.Register(() =>
                            {
                                try { if (!squirrelProc.HasExited) squirrelProc.Kill(true); } catch { }
                            });

                            await Task.Run(() => squirrelProc.WaitForExit(), cancellationToken);

                            exitCode = squirrelProc.ExitCode;
                        }
                        
                        try { if (System.IO.File.Exists(squirrelBat)) System.IO.File.Delete(squirrelBat); } catch { }

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
                                    using var checkStream = new FileStream(squirrelOut, FileMode.Open, FileAccess.Read, FileShare.Read);
                                    using var checkPfs = new PartitionFileSystem(checkStream.AsStorage());

                                    int cnmtCount = 0, controlCount = 0, totalNca = 0;
                                    long largestNca = 0;

                                    foreach (var entry in checkPfs.EnumerateEntries())
                                    {
                                        if (entry.Type == LibHac.Fs.DirectoryEntryType.Directory) continue;
                                        string name = entry.Name;
                                        if (!name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) &&
                                            !name.EndsWith(".cnmt.nca", StringComparison.OrdinalIgnoreCase)) continue;

                                        totalNca++;
                                        if (name.EndsWith(".cnmt.nca", StringComparison.OrdinalIgnoreCase))
                                        {
                                            cnmtCount++;
                                        }
                                        else
                                        {
                                            // Проверяем тип NCA по заголовку (Content Type)
                                            try
                                            {
                                                var ncaFile = OpenFileSafe(checkPfs, "/" + name);
                                                using var ncaStorage = ncaFile.AsStorage();
                                                ncaStorage.GetSize(out long ncaSize);
                                                if (ncaSize > largestNca) largestNca = ncaSize;

                                                // Control NCA обычно < 5 MB и содержит icon/title
                                                if (ncaSize > 0 && ncaSize < 10 * 1024 * 1024)
                                                    controlCount++;
                                            }
                                            catch { }
                                        }
                                    }

                                    // Ожидаем: минимум 1 CNMT, минимум 1 Control-like NCA,
                                    // и минимум N NCAs (base + DLCs = sortedList.Count * ~2)
                                    int expectedMinNca = Math.Max(3, sortedList.Count);
                                    valid = cnmtCount >= 1 && controlCount >= 1 && totalNca >= expectedMinNca;

                                    App.Logger.Log($"[squirrel] validation: cnmt={cnmtCount}, control-like={controlCount}, total={totalNca}, expected>={expectedMinNca}, valid={valid}", Models.LogLevel.Info);
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
                                App.RunOnUI(() => task.LogDetails += "\n⚠️ [NSC_Builder] Вывод squirrel.exe не прошёл валидацию (недостаточно NCA). Переход на нативную сборку C# (LibHac)...");
                            }
                        }
                        else
                        {
                            App.RunOnUI(() => task.LogDetails += $"\n⚠️ [NSC_Builder] squirrel.exe завершился с кодом {exitCode} (возможно, заблокирован политикой Device Guard). Переход на нативную сборку C# (LibHac)...");
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

            // Clean off any existing tags like (1G+1U+5D), [v196608], [0100670014482000], (1.3 - 196608...) to prevent duplicate tags
            baseGameTitle = System.Text.RegularExpressions.Regex.Replace(baseGameTitle, @"\s*\(\d+G(?:\+\d+U)?(?:\+\d+D)?\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            baseGameTitle = System.Text.RegularExpressions.Regex.Replace(baseGameTitle, @"\s*\[v\d+\]", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            baseGameTitle = System.Text.RegularExpressions.Regex.Replace(baseGameTitle, @"\s*\[[0-9A-Fa-f]{16}\]", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            baseGameTitle = System.Text.RegularExpressions.Regex.Replace(baseGameTitle, @"\s*\([^)]*\d{16}[^)]*\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (baseGameTitle.EndsWith("_Multi", StringComparison.OrdinalIgnoreCase))
                baseGameTitle = baseGameTitle.Substring(0, baseGameTitle.Length - 6);
            if (baseGameTitle.EndsWith("_Update", StringComparison.OrdinalIgnoreCase))
                baseGameTitle = baseGameTitle.Substring(0, baseGameTitle.Length - 7);

            var sb = new System.Text.StringBuilder();
            sb.Append(baseGameTitle.Trim());

            if (!string.IsNullOrEmpty(titleId))
            {
                sb.Append($" [{titleId}]");
            }

            if (!string.IsNullOrEmpty(patchVer))
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
