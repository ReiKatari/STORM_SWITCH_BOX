using System.Text.RegularExpressions;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using LibHac;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;

namespace StormSwitchBox.Services
{
    public class HardPatchEngine
    {
        private readonly KeysService _keysService;
        private static readonly object _keysLock = new object();

        public HardPatchEngine(KeysService keysService)
        {
            _keysService = keysService;
        }

        public async Task PatchUpdateAsync(Models.ProcessingTask task, List<string> inputFiles, string outPath, CancellationToken cancellationToken, bool isMultiContent = false, string explicitBaseFile = "", string explicitUpdateFile = "")
        {
            App.RunOnUI(() =>
            {
                task.Status = "Подготовка...";
                task.LogDetails += "\nНачинаем пересборку (Hard Patch) через yanu-cli...";
            });

            string tempDir = "";
            try
            {
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
                
                string localAppDataDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StormSwitchBox");
                string isolatedUserProfile = System.IO.Path.Combine(localAppDataDir, "user_profile");
                string isolatedLocalAppData = System.IO.Path.Combine(localAppDataDir, "cache");
                
                string userProfileSwitch = System.IO.Path.Combine(isolatedUserProfile, ".switch");
                string userProfileKeys = System.IO.Path.Combine(userProfileSwitch, "prod.keys");
                string userProfileTitleKeys = System.IO.Path.Combine(userProfileSwitch, "title.keys");
                
                lock (_keysLock)
                {
                    try
                    {
                        if (!Directory.Exists(userProfileSwitch)) Directory.CreateDirectory(userProfileSwitch);
                        if (!Directory.Exists(isolatedLocalAppData)) Directory.CreateDirectory(isolatedLocalAppData);

                        if (!string.IsNullOrEmpty(App.Settings.Current.KeysPath) && File.Exists(App.Settings.Current.KeysPath))
                        {
                            App.SwitchFormat.CleanKeysFile(App.Settings.Current.KeysPath);
                            File.Copy(App.Settings.Current.KeysPath, userProfileKeys, true);
                        }
                        App.SwitchFormat.CleanKeysFile(userProfileKeys);

                        // Гарантированная синхронизация title.keys в изолированный профиль
                        string mainTitleKeys = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "title.keys");
                        if (!File.Exists(mainTitleKeys) && !string.IsNullOrEmpty(App.Settings.Current.KeysPath))
                        {
                            string candidate = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(App.Settings.Current.KeysPath) ?? "", "title.keys");
                            if (File.Exists(candidate)) mainTitleKeys = candidate;
                        }

                        if (File.Exists(mainTitleKeys))
                        {
                            File.Copy(mainTitleKeys, userProfileTitleKeys, true);
                            App.Logger.Log($"[HardPatchEngine] title.keys успешно синхронизирован в изолированный профиль ({mainTitleKeys})", Models.LogLevel.Info);
                        }
                        else
                        {
                            App.Logger.Log($"[HardPatchEngine] Внимание: файл title.keys не найден по основному пути ({mainTitleKeys})", Models.LogLevel.Warning);
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Log($"[HardPatchEngine] Ошибка синхронизации ключей в изолированный профиль: {ex.Message}", Models.LogLevel.Warning);
                    }
                }

                if (inputFiles.Count < 1)
                {
                    throw new Exception("Ошибка - нет входных файлов (база + патч).");
                }

                string baseFile = explicitBaseFile;
                string updateFile = explicitUpdateFile;

                if (string.IsNullOrEmpty(baseFile) || string.IsNullOrEmpty(updateFile))
                {
                    App.RunOnUI(() => task.LogDetails += $"\nАнализ исходных файлов...");
                    
                    var dlcFiles = new List<string>();

                    foreach (var file in inputFiles)
                    {
                        if (System.IO.Directory.Exists(file)) continue;
                        
                        var info = App.SwitchFormat.ParseNsp(file);
                        string tid = (info.TitleId ?? "").Trim().ToUpperInvariant();
                        
                        bool isDlc = info.ContentType == "AddOnContent" || 
                                     (!string.IsNullOrEmpty(tid) && tid.Length == 16 && !tid.EndsWith("000") && !tid.EndsWith("800")) ||
                                     file.Contains("DLC", StringComparison.OrdinalIgnoreCase);

                        bool isBase = info.ContentType == "Application" || 
                                      (!string.IsNullOrEmpty(tid) && tid.Length == 16 && tid.EndsWith("000"));

                        bool isPatch = info.ContentType == "Patch" || 
                                       (!string.IsNullOrEmpty(tid) && tid.Length == 16 && tid.EndsWith("800"));

                        if (isBase && string.IsNullOrEmpty(baseFile))
                        {
                            baseFile = file;
                        }
                        else if (isPatch && string.IsNullOrEmpty(updateFile))
                        {
                            updateFile = file;
                        }
                        else if (isDlc)
                        {
                            dlcFiles.Add(file);
                        }
                    }

                    if (string.IsNullOrEmpty(baseFile))
                    {
                        baseFile = inputFiles.FirstOrDefault(f => !System.IO.Directory.Exists(f) && 
                            !f.Contains("DLC", StringComparison.OrdinalIgnoreCase) && 
                            (f.Contains("[v0]") || f.Contains("v0"))) ?? 
                            inputFiles.FirstOrDefault(f => !System.IO.Directory.Exists(f) && !f.Contains("DLC", StringComparison.OrdinalIgnoreCase)) ?? "";
                    }

                    if (string.IsNullOrEmpty(updateFile))
                    {
                        updateFile = inputFiles.FirstOrDefault(f => !System.IO.Directory.Exists(f) && 
                            f != baseFile && 
                            !f.Contains("DLC", StringComparison.OrdinalIgnoreCase) && 
                            f.Contains("v") && !f.Contains("v0")) ?? "";
                    }

                    if (dlcFiles.Count > 0)
                    {
                        App.RunOnUI(() => task.LogDetails += $"\nℹ️ Найдено DLC файлов: {dlcFiles.Count} (пропущены для Hard Patch обновления).");
                    }
                }

                bool hasModFolders = inputFiles.Any(d => System.IO.Directory.Exists(d) && 
                    (System.IO.Path.GetFileName(d).Equals("romfs", StringComparison.OrdinalIgnoreCase) || 
                     System.IO.Path.GetFileName(d).Equals("exefs", StringComparison.OrdinalIgnoreCase) ||
                     System.IO.Path.GetFileName(d).Equals("exefs_patches", StringComparison.OrdinalIgnoreCase)));

                if (string.IsNullOrEmpty(updateFile) && !hasModFolders)
                {
                    if (isMultiContent)
                    {
                        App.RunOnUI(() => task.LogDetails += "\nℹ️ Файл обновления и папки модов (romfs/exefs/exefs_patches) отсутствуют. Пропускаем HardPatch...");
                        return;
                    }
                    else
                    {
                        throw new Exception("Ошибка: не найден файл обновления или папки модов romfs/exefs/exefs_patches.");
                    }
                }

                App.RunOnUI(() =>
                {
                    string infoStr = $"\nБаза: {System.IO.Path.GetFileName(baseFile)}";
                    if (!string.IsNullOrEmpty(updateFile)) infoStr += $"\nПатч: {System.IO.Path.GetFileName(updateFile)}";
                    if (hasModFolders) infoStr += "\nОбнаружены моды (romfs/exefs/exefs_patches)";
                    task.LogDetails += infoStr;
                });

                string titleId = string.Empty;

                string? targetDir = System.IO.Path.GetDirectoryName(outPath);
                if (string.IsNullOrEmpty(targetDir)) targetDir = AppDomain.CurrentDomain.BaseDirectory;
                
                string targetDrive = System.IO.Path.GetPathRoot(targetDir) ?? "C:\\";
                string appDrive = System.IO.Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory) ?? "C:\\";
                
                tempDir = System.IO.Path.Combine(targetDrive, $"STORM_TMP_{Guid.NewGuid().ToString("N").Substring(0, 6)}");
                Directory.CreateDirectory(tempDir);
                TempCleanupService.RegisterActiveTempDirectory(tempDir);

                // Декомпрессия NSZ через nsz.exe (проверенный инструмент, корректно регенерирует IVFC хеш-деревья)
                // StormNczStorage НЕ используется здесь — он создаёт NCA с невалидными хеш-деревьями целостности
                if (baseFile.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) || baseFile.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase))
                {
                    App.RunOnUI(() => task.LogDetails += $"\nРаспаковка {System.IO.Path.GetExtension(baseFile)} -> .nsp (nsz.exe)...");

                    string? decompResult = await DecompressWithNszExeAsync(task, baseFile, tempDir, isolatedUserProfile, cancellationToken);
                    if (!string.IsNullOrEmpty(decompResult) && System.IO.File.Exists(decompResult))
                    {
                        baseFile = decompResult;
                        App.RunOnUI(() => task.LogDetails += $"\n  OK: {System.IO.Path.GetFileName(decompResult)}");
                    }
                    else
                    {
                        throw new Exception($"Не удалось декомпрессировать базовый файл {System.IO.Path.GetFileName(baseFile)}.");
                    }
                }
                
                if (updateFile.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) || updateFile.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase))
                {
                    App.RunOnUI(() => task.LogDetails += $"\nРаспаковка {System.IO.Path.GetExtension(updateFile)} -> .nsp (nsz.exe)...");
                    string? decompResult = await DecompressWithNszExeAsync(task, updateFile, tempDir, isolatedUserProfile, cancellationToken);
                    if (!string.IsNullOrEmpty(decompResult) && System.IO.File.Exists(decompResult))
                    {
                        updateFile = decompResult;
                        App.RunOnUI(() => task.LogDetails += $"\n  OK: {System.IO.Path.GetFileName(decompResult)}");
                    }
                    else
                    {
                        throw new Exception($"Не удалось декомпрессировать файл обновления {System.IO.Path.GetFileName(updateFile)}.");
                    }
                }

                try
                {
                    App.TicketHarvester.HarvestTicketsBackground(new[] { baseFile, updateFile }.Where(f => !string.IsNullOrEmpty(f) && File.Exists(f)));
                    string mainTitleKeys = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "title.keys");
                    if (File.Exists(mainTitleKeys))
                    {
                        File.Copy(mainTitleKeys, userProfileTitleKeys, true);
                    }
                }
                catch { }

                try
                {
                    App.RunOnUI(() => task.LogDetails += $"\nЧтение метаданных (TitleID) из базы...");
                    var info = App.SwitchFormat.ParseNsp(baseFile);
                    if (!string.IsNullOrEmpty(info.TitleId))
                    {
                        titleId = info.TitleId;
                        App.RunOnUI(() => task.LogDetails += $"\nОпределен TitleID: {titleId}");
                    }
                }
                catch { }

                if (string.IsNullOrEmpty(titleId))
                {
                    throw new Exception("Не удалось определить TitleID базовой игры. Проверьте исходный файл.");
                }
                
                string outDir = System.IO.Path.GetDirectoryName(outPath) ?? string.Empty;
                Directory.CreateDirectory(outDir);

                App.EnsureUserKeysAvailable();

                string yanuCliPath = FindYanuCli();
                string keysPath = App.Settings.Current.KeysPath;

                string defaultKeysPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "prod.keys");
                bool isDefaultLocation = false;
                try
                {
                    if (!string.IsNullOrEmpty(keysPath) && File.Exists(keysPath) && File.Exists(defaultKeysPath))
                    {
                        isDefaultLocation = System.IO.Path.GetFullPath(keysPath).Equals(System.IO.Path.GetFullPath(defaultKeysPath), StringComparison.OrdinalIgnoreCase);
                    }
                }
                catch { }

                string keyfileFlag = (!string.IsNullOrEmpty(keysPath) && File.Exists(keysPath) && !isDefaultLocation) ? $"-k \"{keysPath}\" " : "";

                string yanuOutDir = System.IO.Path.Combine(tempDir, "yanu_output");
                Directory.CreateDirectory(yanuOutDir);

                var romfsMods = new List<string>();
                var exefsMods = new List<string>();
                string? exefsPatchesMod = null;
                var standaloneModFiles = new List<string>();

                // Auto-extract .zip/.7z/.rar mod archives in inputFiles or input directories
                var archivesToExtract = new List<string>();
                foreach (var f in inputFiles.Where(File.Exists))
                {
                    string ext = System.IO.Path.GetExtension(f).ToLowerInvariant();
                    if (ext is ".zip" or ".7z" or ".rar") archivesToExtract.Add(f);
                }
                foreach (var d in inputFiles.Where(Directory.Exists))
                {
                    try
                    {
                        var subArch = Directory.EnumerateFiles(d, "*.*", SearchOption.AllDirectories)
                            .Where(f => {
                                string ext = System.IO.Path.GetExtension(f).ToLowerInvariant();
                                return ext is ".zip" or ".7z" or ".rar";
                            });
                        archivesToExtract.AddRange(subArch);
                    }
                    catch { }
                }

                int archIdx = 0;
                string? sevenZipPath = ViewModels.TasksViewModel.GetSevenZipPath();
                foreach (var arch in archivesToExtract.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        string extractDir = System.IO.Path.Combine(tempDir, "extracted_mod_" + archIdx++);
                        Directory.CreateDirectory(extractDir);
                        bool extracted = false;
                        if (File.Exists(sevenZipPath))
                        {
                            var psi = new ProcessStartInfo
                            {
                                FileName = sevenZipPath,
                                Arguments = $"x -y \"{arch}\" -o\"{extractDir}\"",
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };
                            using var proc = Process.Start(psi);
                            if (proc != null)
                            {
                                proc.WaitForExit(60000);
                                extracted = (proc.ExitCode == 0);
                            }
                        }
                        if (!extracted && (System.IO.Path.GetExtension(arch).Equals(".zip", StringComparison.OrdinalIgnoreCase) || System.IO.Path.GetExtension(arch).Equals(".bin", StringComparison.OrdinalIgnoreCase)))
                        {
                            foreach (var encCandidate in new System.Text.Encoding?[] { null, GetCp866EncodingSafe(), GetWin1251EncodingSafe(), System.Text.Encoding.Latin1 })
                            {
                                try
                                {
                                    if (encCandidate == null)
                                        System.IO.Compression.ZipFile.ExtractToDirectory(arch, extractDir, overwriteFiles: true);
                                    else
                                        System.IO.Compression.ZipFile.ExtractToDirectory(arch, extractDir, encCandidate, overwriteFiles: true);
                                    extracted = true;
                                    break;
                                }
                                catch { }
                            }
                        }

                        if (extracted)
                        {
                            var nRomfs = Directory.GetDirectories(extractDir, "*romfs*", SearchOption.AllDirectories)
                                .Where(sub => System.IO.Path.GetFileName(sub).Equals("romfs", StringComparison.OrdinalIgnoreCase) || System.IO.Path.GetFileName(sub).StartsWith("unlocker_romfs", StringComparison.OrdinalIgnoreCase));
                            romfsMods.AddRange(nRomfs);

                            var nExefs = Directory.GetDirectories(extractDir, "*exefs*", SearchOption.AllDirectories)
                                .Where(sub => System.IO.Path.GetFileName(sub).Equals("exefs", StringComparison.OrdinalIgnoreCase));
                            exefsMods.AddRange(nExefs);

                            var nPatches = Directory.GetDirectories(extractDir, "exefs_patches", SearchOption.AllDirectories).FirstOrDefault();
                            if (nPatches != null) exefsPatchesMod ??= nPatches;

                            var nStandalone = Directory.GetFiles(extractDir, "*.*", SearchOption.AllDirectories)
                                .Where(f => {
                                    string fn = System.IO.Path.GetFileName(f).ToLowerInvariant();
                                    string ext = System.IO.Path.GetExtension(f).ToLowerInvariant();
                                    return (ext == ".pak" || ext == ".zs" || ext == ".sarc" || fn.EndsWith(".sarc.zs")) &&
                                           !f.Contains("romfs", StringComparison.OrdinalIgnoreCase) &&
                                           !f.Contains("exefs", StringComparison.OrdinalIgnoreCase);
                                });
                            standaloneModFiles.AddRange(nStandalone);
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Log($"[HardPatchEngine] Ошибка распаковки архива мода {arch}: {ex.Message}", Models.LogLevel.Warning);
                    }
                }

                foreach (var f in inputFiles.Where(File.Exists))
                {
                    string fn = System.IO.Path.GetFileName(f).ToLowerInvariant();
                    string ext = System.IO.Path.GetExtension(f).ToLowerInvariant();
                    if (ext == ".pak" || ext == ".zs" || ext == ".sarc" || fn.EndsWith(".sarc.zs"))
                    {
                        standaloneModFiles.Add(f);
                    }
                    else if (ext == ".ips" || ext == ".pchtxt" || ext == ".bps")
                    {
                        string directPatchesDir = System.IO.Path.Combine(tempDir, "direct_exefs_patches");
                        Directory.CreateDirectory(directPatchesDir);
                        string destPath = System.IO.Path.Combine(directPatchesDir, System.IO.Path.GetFileName(f));
                        try { File.Copy(f, destPath, true); } catch { }
                        exefsPatchesMod = directPatchesDir;
                    }
                }

                foreach (var d in inputFiles.Where(Directory.Exists))
                {
                    string dName = System.IO.Path.GetFileName(d);
                    if (dName.Equals("romfs", StringComparison.OrdinalIgnoreCase) || dName.StartsWith("unlocker_romfs", StringComparison.OrdinalIgnoreCase))
                    {
                        romfsMods.Add(d);
                    }
                    else if (dName.Equals("exefs", StringComparison.OrdinalIgnoreCase))
                    {
                        exefsMods.Add(d);
                    }
                    else if (dName.Equals("exefs_patches", StringComparison.OrdinalIgnoreCase))
                    {
                        exefsPatchesMod ??= d;
                    }
                    else
                    {
                        try
                        {
                            // Рекурсивный поиск romfs и exefs с любой глубины вложенности
                            var nestedRomfs = Directory.GetDirectories(d, "*romfs*", SearchOption.AllDirectories)
                                .Where(sub => System.IO.Path.GetFileName(sub).Equals("romfs", StringComparison.OrdinalIgnoreCase) || System.IO.Path.GetFileName(sub).StartsWith("unlocker_romfs", StringComparison.OrdinalIgnoreCase));
                            romfsMods.AddRange(nestedRomfs);

                            var nestedExefs = Directory.GetDirectories(d, "*exefs*", SearchOption.AllDirectories)
                                .Where(sub => System.IO.Path.GetFileName(sub).Equals("exefs", StringComparison.OrdinalIgnoreCase));
                            exefsMods.AddRange(nestedExefs);

                            var nestedPatches = Directory.GetDirectories(d, "exefs_patches", SearchOption.AllDirectories).FirstOrDefault();
                            if (nestedPatches != null) exefsPatchesMod ??= nestedPatches;

                            // Поиск автономных файлов модов (.pak, .sarc.zs), если нет корневой папки romfs
                            var standalone = Directory.GetFiles(d, "*.*", SearchOption.AllDirectories)
                                .Where(f => {
                                    string fn = System.IO.Path.GetFileName(f).ToLowerInvariant();
                                    string ext = System.IO.Path.GetExtension(f).ToLowerInvariant();
                                    return (ext == ".pak" || ext == ".zs" || ext == ".sarc" || fn.EndsWith(".sarc.zs")) &&
                                           !f.Contains("romfs", StringComparison.OrdinalIgnoreCase) &&
                                           !f.Contains("exefs", StringComparison.OrdinalIgnoreCase);
                                });
                            standaloneModFiles.AddRange(standalone);
                        }
                        catch { }
                    }
                }
                string? romfsMod = romfsMods.FirstOrDefault();
                string? exefsMod = exefsMods.FirstOrDefault();
                bool applyMods = (romfsMods.Count > 0 || exefsMods.Count > 0 || exefsPatchesMod != null || standaloneModFiles.Count > 0);
                
                string keepLangsArg = "";
                if (App.Settings.Current.TrimXci && App.Settings.Current.KeepLanguages != null && App.Settings.Current.KeepLanguages.Count > 0)
                {
                    string keepLangsStr = string.Join(",", App.Settings.Current.KeepLanguages);
                    keepLangsArg = string.IsNullOrEmpty(keepLangsStr) ? "" : $"--keep-langs \"{keepLangsStr}\"";
                }
                
                string titleVersionArg = "";
                uint? targetTitleVersion = null;
                if (!string.IsNullOrEmpty(updateFile))
                {
                    try {
                        var uInfo = App.SwitchFormat.ParseNsp(updateFile);
                        if (!string.IsNullOrEmpty(uInfo.Version) && uint.TryParse(uInfo.Version, out uint uv)) {
                            targetTitleVersion = uv;
                            // hacpack ожидает --titleversion в 4-байтном HEX (например 0x00010000 для 65536)
                            titleVersionArg = $"--titleversion 0x{uv:x8}";
                        }
                    } catch { }
                }
                else if (!string.IsNullOrEmpty(baseFile))
                {
                    try {
                        var bInfo = App.SwitchFormat.ParseNsp(baseFile);
                        if (!string.IsNullOrEmpty(bInfo.Version) && uint.TryParse(bInfo.Version, out uint bv)) {
                            targetTitleVersion = bv;
                            titleVersionArg = $"--titleversion 0x{bv:x8}";
                        }
                    } catch { }
                }
                
                bool hasModsToApply = (romfsMod != null || exefsMod != null || exefsPatchesMod != null || applyMods);
                bool yanuUpdateSuccess = false;
                long baseSize = (!string.IsNullOrEmpty(baseFile) && File.Exists(baseFile)) ? new FileInfo(baseFile).Length : 0;

                // СЦЕНАРИЙ 1: Если модов нет и передан файл обновления — сначала пробуем прямой и быстрый yanu-cli update
                if (!hasModsToApply && !string.IsNullOrEmpty(updateFile))
                {
                    App.RunOnUI(() => task.LogDetails += $"\n[1/2] Интеграция обновления (yanu-cli update)...");

                    string updateWorkDir = System.IO.Path.Combine(tempDir, "update_work");
                    Directory.CreateDirectory(updateWorkDir);

                    string updateArgs = $"{keyfileFlag}update --base \"{baseFile}\" --update \"{updateFile}\" -o \"{yanuOutDir}\"";
                    if (!string.IsNullOrEmpty(keepLangsArg)) updateArgs += $" {keepLangsArg}";
                    if (!string.IsNullOrEmpty(titleVersionArg)) updateArgs += $" {titleVersionArg}";

                    App.Logger.Log($"[yanu-cli] update: {updateArgs}", Models.LogLevel.Info);

                    var updatePsi = new ProcessStartInfo
                    {
                        FileName = yanuCliPath,
                        Arguments = updateArgs,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        WorkingDirectory = updateWorkDir,
                        StandardOutputEncoding = System.Text.Encoding.UTF8,
                        StandardErrorEncoding = System.Text.Encoding.UTF8
                    };
                    updatePsi.EnvironmentVariables["USERPROFILE"] = isolatedUserProfile;
                    updatePsi.EnvironmentVariables["LOCALAPPDATA"] = isolatedLocalAppData;
                    updatePsi.EnvironmentVariables["APPDATA"] = isolatedLocalAppData;
                    updatePsi.EnvironmentVariables["TEMP"] = tempDir;
                    updatePsi.EnvironmentVariables["TMP"] = tempDir;

                    using var updateProc = Process.Start(updatePsi);
                    if (updateProc != null)
                    {
                        var updateStderr = new System.Text.StringBuilder();
                        using (var logBuffer = new ProgressLogBuffer(task))
                        {
                            updateProc.OutputDataReceived += (s, e) => { if (e.Data != null) logBuffer.AppendLine(e.Data); };
                            updateProc.ErrorDataReceived += (s, e) => { if (e.Data != null) updateStderr.AppendLine(e.Data); };
                            updateProc.BeginOutputReadLine();
                            updateProc.BeginErrorReadLine();
                            await updateProc.WaitForExitAsync(cancellationToken);
                        }

                        if (updateProc.ExitCode == 0)
                        {
                            var updateNsps = Directory.GetFiles(yanuOutDir, "*.nsp");
                            long minAcceptableSize = Math.Min(25L * 1024 * 1024, (long)(baseSize * 0.2));
                            if (updateNsps.Length > 0 && (baseSize <= 50 * 1024 * 1024 || new FileInfo(updateNsps[0]).Length >= minAcceptableSize))
                            {
                                yanuUpdateSuccess = true;
                                App.Logger.Log($"[yanu-cli] update OK. Создан NSP ({new FileInfo(updateNsps[0]).Length} байт).", Models.LogLevel.Info);
                                App.RunOnUI(() => task.LogDetails += $"\n  yanu-cli update: успешно!");
                            }
                            else
                            {
                                if (updateNsps.Length > 0)
                                {
                                    App.Logger.Log($"[yanu-cli] update exit=0, но размер NSP ({new FileInfo(updateNsps[0]).Length} байт) меньше порога ({minAcceptableSize} байт). Удаляем поврежденный файл и переключаемся на unpack/pack.", Models.LogLevel.Warning);
                                    foreach (var f in updateNsps) try { File.Delete(f); } catch { }
                                }
                                else
                                {
                                    App.Logger.Log("[yanu-cli] update exit=0, но NSP не найден. Переключение на unpack/pack.", Models.LogLevel.Warning);
                                }
                            }
                        }
                        else
                        {
                            App.Logger.Log($"[yanu-cli] update failed (exit={updateProc.ExitCode}): {updateStderr.ToString().Trim()}", Models.LogLevel.Warning);
                            App.RunOnUI(() => task.LogDetails += $"\n  yanu-cli update: прямой патч не удался. Запуск универсального unpack/pack...");
                        }
                    }

                    try { if (Directory.Exists(updateWorkDir)) Directory.Delete(updateWorkDir, true); } catch { }
                }

                // СЦЕНАРИЙ 2: Если есть моды или прямой update не удался — полный конвейер unpack + dynamic resolve + mod inject + pack
                if (!yanuUpdateSuccess)
                {

                    App.RunOnUI(() => task.LogDetails += hasModsToApply
                        ? $"\n[1/3] Распаковка файлов для применения обновления и модов (yanu-cli unpack)..."
                        : $"\n[1/2] Распаковка для слияния без дубликатов (yanu-cli unpack)...");

                    string tempUnpack = System.IO.Path.Combine(tempDir, "unpack_modded");
                    Directory.CreateDirectory(tempUnpack);

                    string unpackArgs = $"{keyfileFlag}unpack --base \"{baseFile}\"";
                    if (!string.IsNullOrEmpty(updateFile)) unpackArgs += $" --update \"{updateFile}\"";
                    unpackArgs += $" -o \"{tempUnpack}\"";

                    var unpackPsi = new ProcessStartInfo
                    {
                        FileName = yanuCliPath,
                        Arguments = unpackArgs,
                        WorkingDirectory = tempUnpack,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8,
                        StandardErrorEncoding = System.Text.Encoding.UTF8
                    };
                    unpackPsi.EnvironmentVariables["USERPROFILE"] = isolatedUserProfile;
                    unpackPsi.EnvironmentVariables["LOCALAPPDATA"] = isolatedLocalAppData;
                    unpackPsi.EnvironmentVariables["APPDATA"] = isolatedLocalAppData;
                    unpackPsi.EnvironmentVariables["TEMP"] = tempDir;
                    unpackPsi.EnvironmentVariables["TMP"] = tempDir;
                    using var unpackProc = Process.Start(unpackPsi);
                    if (unpackProc == null) throw new Exception("Не удалось запустить yanu-cli unpack");

                    var unpackStderr = new System.Text.StringBuilder();
                    using (var logBuffer = new ProgressLogBuffer(task))
                    {
                        unpackProc.OutputDataReceived += (s, e) => { if (e.Data != null) logBuffer.AppendLine(e.Data); };
                        unpackProc.ErrorDataReceived += (s, e) => { if (e.Data != null) unpackStderr.AppendLine(e.Data); };
                        unpackProc.BeginOutputReadLine();
                        unpackProc.BeginErrorReadLine();
                        await unpackProc.WaitForExitAsync(cancellationToken);
                    }
                    if (unpackProc.ExitCode != 0) throw new Exception($"Ошибка yanu-cli unpack:\n{unpackStderr}");

                    // Динамический поиск реальных путей ExeFS (с подлинным main.npdm), RomFS и Control NCA внутри tempUnpack
                    var (targetExeFs, targetRomFs, controlNca, resolvedTitleId) = ResolveUnpackedStructure(
                        tempUnpack,
                        baseFile,
                        updateFile,
                        keysPath,
                        _keysService,
                        titleId,
                        yanuCliPath,
                        tempDir,
                        isolatedUserProfile,
                        isolatedLocalAppData);

                    if (hasModsToApply)
                    {
                        if (App.Settings.Current.OptimizeModAssets)
                        {
                            App.RunOnUI(() => task.LogDetails += "\n🧹 [Mod Optimizer] Очистка мусора и сжатие текстур модов без потерь (Oxipng)...");
                            foreach (var rMod in romfsMods)
                            {
                                await ModOptimizerService.OptimizeModDirectoryAsync(rMod, task, cancellationToken);
                            }
                            if (!string.IsNullOrEmpty(exefsMod))
                            {
                                await ModOptimizerService.OptimizeModDirectoryAsync(exefsMod, task, cancellationToken);
                            }
                        }

                        App.RunOnUI(() => task.LogDetails += $"\n[2/3] Инъекция модов (romfs/exefs/exefs_patches)...");
                        foreach (var rMod in romfsMods)
                        {
                            CopyDirectoryContentFiltered(rMod, targetRomFs);
                        }
                        foreach (var eMod in exefsMods)
                        {
                            CopyDirectoryContentFiltered(eMod, targetExeFs);
                        }
                        if (!string.IsNullOrEmpty(exefsPatchesMod)) ApplyExeFsPatches(exefsPatchesMod, targetExeFs, task);
                        foreach (var sMod in standaloneModFiles)
                        {
                            InjectStandaloneModFile(sMod, targetRomFs, task);
                        }
                        SynchronizeCrossRegionLocalization(targetRomFs, romfsMods, standaloneModFiles, task);
                    }

                    if (App.Settings.Current.AutoInject60FpsPatches)
                    {
                        App.GraphicsPatch.TryInjectGraphicsPatches(titleId, targetExeFs, task);
                    }

                    // Всегда очищаем мусорные артефакты ОС и бэкапы из RomFS и ExeFS перед упаковкой для минимизации размера
                    ModOptimizerService.CleanJunkFiles(targetRomFs, task);
                    ModOptimizerService.CleanJunkFiles(targetExeFs, task);

                    App.RunOnUI(() => task.LogDetails += hasModsToApply
                        ? $"\n[3/3] Монолитная сборка (yanu-cli pack)..."
                        : $"\n[2/2] Монолитная сборка без дубликатов (yanu-cli pack)...");

                    string titleIdStr = !string.IsNullOrEmpty(resolvedTitleId) ? resolvedTitleId : titleId;
                    string packArgs = $"{keyfileFlag}pack --titleid {titleIdStr} --controlnca \"{controlNca}\" --romfsdir \"{targetRomFs}\" --exefsdir \"{targetExeFs}\" -o \"{yanuOutDir}\" {keepLangsArg} {titleVersionArg}".TrimEnd();
                    var packPsi = new ProcessStartInfo
                    {
                        FileName = yanuCliPath,
                        Arguments = packArgs,
                        WorkingDirectory = tempUnpack,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8,
                        StandardErrorEncoding = System.Text.Encoding.UTF8
                    };
                    packPsi.EnvironmentVariables["USERPROFILE"] = isolatedUserProfile;
                    packPsi.EnvironmentVariables["LOCALAPPDATA"] = isolatedLocalAppData;
                    packPsi.EnvironmentVariables["APPDATA"] = isolatedLocalAppData;
                    packPsi.EnvironmentVariables["TEMP"] = tempDir;
                    packPsi.EnvironmentVariables["TMP"] = tempDir;
                    using var packProc = Process.Start(packPsi);
                    if (packProc == null) throw new Exception("Не удалось запустить yanu-cli pack");

                    var packStderr = new System.Text.StringBuilder();
                    using (var logBuffer = new ProgressLogBuffer(task))
                    {
                        packProc.OutputDataReceived += (s, e) => { if (e.Data != null) logBuffer.AppendLine(e.Data); };
                        packProc.ErrorDataReceived += (s, e) => { if (e.Data != null) packStderr.AppendLine(e.Data); };
                        packProc.BeginOutputReadLine();
                        packProc.BeginErrorReadLine();
                        await packProc.WaitForExitAsync(cancellationToken);
                    }
                    if (packProc.ExitCode != 0) throw new Exception($"Ошибка yanu-cli pack:\n{packStderr}");
                }

                // Поиск сгенерированного NSP
                var generatedFiles = Directory.GetFiles(yanuOutDir, "*.nsp");
                if (generatedFiles.Length == 0)
                {
                    generatedFiles = Directory.GetFiles(tempDir, "*.nsp");
                }

                if (File.Exists(outPath)) File.Delete(outPath);

                if (generatedFiles.Length > 0)
                {
                    string genFile = generatedFiles.OrderByDescending(f => new FileInfo(f).CreationTime).First();
                    long genFileSize = new FileInfo(genFile).Length;
                    long minGenSize = Math.Min(25L * 1024 * 1024, (long)(baseSize * 0.2));
                    if (baseSize > 50 * 1024 * 1024 && genFileSize < minGenSize)
                    {
                        throw new InvalidOperationException($"Размер сгенерированного файла ({genFileSize} байт) аномально мал относительно базы ({baseSize} байт). RomFS отсутствует.");
                    }
                    
                    // Нормализация Program NCA (Section[0] 0x0C00 -> 0x4000 +26 блоков) и точной десятичной версии CNMT
                    if (File.Exists(genFile))
                    {
                        NormalizeMergedNspSectionOffsetsTo0x4000(genFile, _keysService, targetTitleVersion, task);
                    }

                    // Применяем кастомные метаданные / иконку при необходимости
                    if (task.CustomMetadata != null && File.Exists(genFile))
                    {
                        await App.ControlEditor.ApplyCustomMetadataAsync(task.CustomMetadata, genFile, task, cancellationToken);
                    }

                    string targetExt = System.IO.Path.GetExtension(outPath).ToLower();
                    if (targetExt == ".nsp")
                    {
                        await SafeFileOperations.SafeMoveOrReplaceFileAsync(genFile, outPath, task, cancellationToken);
                    }
                    else if (targetExt == ".xci" || targetExt == ".xcz")
                    {
                        string outDirF = System.IO.Path.GetDirectoryName(outPath) ?? string.Empty;
                        App.RunOnUI(() => task.LogDetails += "\nШаг финализации - конвертация в XCI...");
                        await App.SwitchFormat.ConvertContainerAsync(task, genFile, outDirF, "XCI", cancellationToken);
                        
                        string expectedXci = System.IO.Path.ChangeExtension(System.IO.Path.Combine(outDirF, System.IO.Path.GetFileName(genFile)), ".xci");
                        
                        if (targetExt == ".xcz" && File.Exists(expectedXci))
                        {
                            App.RunOnUI(() => task.LogDetails += "\nСжатие в XCZ...");
                            await App.NszCompression.CompressToNszAsync(task, expectedXci, outDirF, cancellationToken);
                            string expectedNsz = System.IO.Path.ChangeExtension(expectedXci, ".nsz");
                            if (File.Exists(expectedNsz))
                            {
                                await SafeFileOperations.SafeMoveOrReplaceFileAsync(expectedNsz, outPath, task, cancellationToken);
                            }
                            // Сохраняем исходный XCI тоже рядом с XCZ
                            string finalXci = System.IO.Path.ChangeExtension(outPath, ".xci");
                            if (!expectedXci.Equals(finalXci, StringComparison.OrdinalIgnoreCase) && File.Exists(expectedXci))
                            {
                                try { File.Move(expectedXci, finalXci, true); } catch { }
                            }
                        }
                        else if (File.Exists(expectedXci))
                        {
                            await SafeFileOperations.SafeMoveOrReplaceFileAsync(expectedXci, outPath, task, cancellationToken);
                        }
                    }
                    else if (targetExt == ".nsz")
                    {
                        string outDirF = System.IO.Path.GetDirectoryName(outPath) ?? string.Empty;
                        App.RunOnUI(() => task.LogDetails += "\nШаг финализации - сжатие в NSZ...");
                        await App.NszCompression.CompressToNszAsync(task, genFile, outDirF, cancellationToken);
                        
                        string expectedNsz = System.IO.Path.ChangeExtension(System.IO.Path.Combine(outDirF, System.IO.Path.GetFileName(genFile)), ".nsz");
                        if (File.Exists(expectedNsz))
                        {
                            await SafeFileOperations.SafeMoveOrReplaceFileAsync(expectedNsz, outPath, task, cancellationToken);
                            // Если это отдельная задача (не мультиконтент), также сохраняем NSP рядом
                            if (!isMultiContent)
                            {
                                string nspFinal = System.IO.Path.ChangeExtension(outPath, ".nsp");
                                if (!File.Exists(nspFinal) && File.Exists(genFile))
                                {
                                    try { File.Copy(genFile, nspFinal, true); } catch { }
                                }
                            }
                        }
                    }
                }
                else
                {
                    throw new Exception("Критическая ошибка: Результирующий NSP файл не найден после работы yanu-cli.");
                }

                TempCleanupService.ForceDeleteDirectory(tempDir);

                App.RunOnUI(() =>
                {
                    if (!isMultiContent && System.IO.File.Exists(outPath))
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
                    task.Status = "Готово...";
                    task.LogDetails += $"\n- Итог: {System.IO.Path.GetFileName(outPath)}";
                });
            }
            catch (Exception ex)
            {
                TempCleanupService.ForceDeleteDirectory(tempDir);

                if (isMultiContent)
                {
                    App.Logger.Log($"[HardPatch] Ошибка при пересборке (isMultiContent=true): {ex.Message}", Models.LogLevel.Warning);
                    throw;
                }

                App.RunOnUI(() =>
                {
                    task.Status = "Ошибка";
                    task.IsRunning = false;
                    task.LogDetails += $"\n[ОШИБКА]: {ex.Message}";
                    StormSwitchBox.Services.HistoryService.AddToHistory(task);
                });
                
                App.Logger.Log($"Ошибка хардпатча: {ex.ToString()}", Models.LogLevel.Error);
            }
        }

        private static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {dir.FullName}");

            DirectoryInfo[] dirs = dir.GetDirectories();
            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                string targetFilePath = System.IO.Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            if (recursive)
            {
                foreach (DirectoryInfo subDir in dirs)
                {
                    string newDestinationDir = System.IO.Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }

        private static async Task RunProcessAsync(string fileName, string arguments, Action<string>? onProgress, CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };
            using var proc = Process.Start(psi);
            if (proc == null) return;
            var err = new System.Text.StringBuilder();
            proc.OutputDataReceived += (s, e) => {
                if (e.Data != null) onProgress?.Invoke(e.Data);
            };
            proc.ErrorDataReceived += (s, e) => {
                if (e.Data != null) err.AppendLine(e.Data);
            };
            proc.BeginOutputReadLine();
            proc.BeginErrorReadLine();
            await proc.WaitForExitAsync(ct);
            if (proc.ExitCode != 0)
                App.Logger.Log($"[RunProcess] {System.IO.Path.GetFileName(fileName)} exit={proc.ExitCode}: {err.ToString().Trim()}", Models.LogLevel.Warning);
        }

        private static string FindYanuCli()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] searchPaths = new[]
            {
                System.IO.Path.Combine(appDir, "tools", "yanu-cli.exe"),
                System.IO.Path.Combine(appDir, "tools", "com.github.nozwock.yanu", "yanu-cli.exe"),
                System.IO.Path.Combine(appDir, "..", "..", "..", "tools", "yanu-cli.exe"),
                System.IO.Path.Combine(appDir, "..", "..", "..", "tools", "com.github.nozwock.yanu", "yanu-cli.exe"),
                System.IO.Path.Combine(appDir, "..", "..", "..", "..", "tools", "yanu-cli.exe"),
                System.IO.Path.Combine(appDir, "..", "..", "..", "..", "..", "tools", "yanu-cli.exe"),
            };
            
            foreach (var p in searchPaths)
            {
                string full = System.IO.Path.GetFullPath(p);
                if (File.Exists(full)) return full;
            }
            
            string? projectRoot = appDir;
            for (int i = 0; i < 8 && projectRoot != null; i++)
            {
                string candidate = System.IO.Path.Combine(projectRoot, "tools", "yanu-cli.exe");
                if (File.Exists(candidate)) return candidate;
                string candidateSub = System.IO.Path.Combine(projectRoot, "tools", "com.github.nozwock.yanu", "yanu-cli.exe");
                if (File.Exists(candidateSub)) return candidateSub;
                projectRoot = System.IO.Path.GetDirectoryName(projectRoot);
            }
            
            throw new Exception("yanu-cli.exe не найден. Убедитесь, что утилита находится в tools/.");
        }

        /// <summary>
        /// Finds nsz.exe in the tools directory for NSZ/XCZ decompression.
        /// </summary>
        private static string FindNszExe()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] searchPaths = new[]
            {
                System.IO.Path.Combine(appDir, "tools", "nsz", "nsz.exe"),
                System.IO.Path.Combine(appDir, "..", "..", "..", "tools", "nsz", "nsz.exe"),
                System.IO.Path.Combine(appDir, "..", "..", "..", "..", "tools", "nsz", "nsz.exe"),
                System.IO.Path.Combine(appDir, "..", "..", "..", "..", "..", "tools", "nsz", "nsz.exe"),
            };
            
            foreach (var p in searchPaths)
            {
                string full = System.IO.Path.GetFullPath(p);
                if (File.Exists(full)) return full;
            }
            
            string? projectRoot = appDir;
            for (int i = 0; i < 8 && projectRoot != null; i++)
            {
                string candidate = System.IO.Path.Combine(projectRoot, "tools", "nsz", "nsz.exe");
                if (File.Exists(candidate)) return candidate;
                projectRoot = System.IO.Path.GetDirectoryName(projectRoot);
            }
            
            throw new Exception("nsz.exe не найден. Убедитесь, что утилита находится в tools/nsz/.");
        }

        /// <summary>
        /// Decompresses NSZ/XCZ file to NSP/XCI using nsz.exe.
        /// This produces valid NCA files with correct IVFC hash trees,
        /// unlike the in-process StormNczStorage which leaves stale hash trees.
        /// </summary>
        private static async Task<string?> DecompressWithNszExeAsync(
            Models.ProcessingTask task,
            string inputFile,
            string outputDir,
            string isolatedUserProfile,
            CancellationToken cancellationToken)
        {
            string nszExe = FindNszExe();
            App.Logger.Log($"[nsz.exe] Decompressing: {System.IO.Path.GetFileName(inputFile)}", Models.LogLevel.Info);
            
            string keysFile = System.IO.Path.Combine(isolatedUserProfile, ".switch", "prod.keys");
            string keysParam = "";
            if (File.Exists(keysFile))
            {
                keysParam = $"--keys \"{keysFile}\"";
            }
            else if (!string.IsNullOrEmpty(App.Settings.Current.KeysPath) && File.Exists(App.Settings.Current.KeysPath))
            {
                keysParam = $"--keys \"{App.Settings.Current.KeysPath}\"";
            }

            // nsz.exe -D <input> -o <output_dir> --overwrite --minimal-output --keys <keys> -t 0
            string args = $"-D \"{inputFile}\" -o \"{outputDir}\" --overwrite --minimal-output {keysParam} -t 0".Trim();
            
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c chcp 65001 >nul & \"{nszExe}\" {args}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };
            psi.EnvironmentVariables["USERPROFILE"] = isolatedUserProfile;
            psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            psi.EnvironmentVariables["PYTHONUTF8"] = "1";

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                App.Logger.Log("[nsz.exe] Не удалось запустить процесс", Models.LogLevel.Error);
                return null;
            }

            var stderr = new System.Text.StringBuilder();
            using (var logBuffer = new ProgressLogBuffer(task))
            {
                proc.OutputDataReceived += (s, e) => {
                    if (e.Data != null) logBuffer.AppendLine(e.Data);
                };
                proc.ErrorDataReceived += (s, e) => {
                    if (e.Data != null) stderr.AppendLine(e.Data);
                };
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                await proc.WaitForExitAsync(cancellationToken);
            }

            App.Logger.Log($"[nsz.exe] Exit={proc.ExitCode}", Models.LogLevel.Info);
            if (stderr.Length > 0)
                App.Logger.Log($"[nsz.exe] stderr={stderr.ToString().Trim()}", Models.LogLevel.Warning);

            if (proc.ExitCode != 0)
            {
                App.Logger.Log($"[nsz.exe] Decompression failed (exit={proc.ExitCode})", Models.LogLevel.Error);
                return null;
            }

            // Find the output file — nsz.exe creates .nsp from .nsz, .xci from .xcz
            string expectedExt = inputFile.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) ? ".nsp" : ".xci";
            string baseName = System.IO.Path.GetFileNameWithoutExtension(inputFile);
            
            // Try exact filename match first
            string expectedPath = System.IO.Path.Combine(outputDir, baseName + expectedExt);
            if (File.Exists(expectedPath)) return expectedPath;

            // Fallback: search for any new file with the expected extension
            var candidates = Directory.GetFiles(outputDir, $"*{expectedExt}")
                .OrderByDescending(f => new FileInfo(f).CreationTime)
                .ToArray();
            
            if (candidates.Length > 0)
            {
                App.Logger.Log($"[nsz.exe] Found output: {System.IO.Path.GetFileName(candidates[0])}", Models.LogLevel.Info);
                return candidates[0];
            }

            App.Logger.Log($"[nsz.exe] Output file not found in {outputDir}", Models.LogLevel.Error);
            return null;
        }

        private static void CopyDirectoryContent(string sourceDir, string destinationDir)
        {
            if (!Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                if (ModOptimizerService.IsJunkFile(file)) continue;
                string dest = System.IO.Path.Combine(destinationDir, System.IO.Path.GetFileName(file));
                File.Copy(file, dest, true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string dirName = System.IO.Path.GetFileName(dir);
                if (dirName.Equals(".git", StringComparison.OrdinalIgnoreCase) || 
                    dirName.Equals(".svn", StringComparison.OrdinalIgnoreCase)) continue;
                string dest = System.IO.Path.Combine(destinationDir, dirName);
                CopyDirectoryContent(dir, dest);
            }
        }
        /// <summary>
        /// Manually builds a PFS0 (NSP) archive from all files in sourceDir.
        /// PFS0 format: "PFS0" magic, file count, string table size, reserved,
        /// then per-file entries (offset, size, name offset, reserved), string table, file data.
        /// </summary>
        private static void BuildPfs0Nsp(string sourceDir, string outputPath)
        {
            var files = Directory.GetFiles(sourceDir)
                .Where(f => IsValidNspEntry(System.IO.Path.GetFileName(f)))
                .OrderBy(f => System.IO.Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (files.Length == 0) return;

            // Build string table
            var stringTable = new System.IO.MemoryStream();
            var nameOffsets = new int[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                nameOffsets[i] = (int)stringTable.Position;
                byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(System.IO.Path.GetFileName(files[i]));
                stringTable.Write(nameBytes, 0, nameBytes.Length);
                stringTable.WriteByte(0); // null terminator
            }
            // Pad string table to 0x20 alignment
            while (stringTable.Length % 0x20 != 0)
                stringTable.WriteByte(0);
            byte[] stringTableData = stringTable.ToArray();

            int headerSize = 0x10; // magic(4) + fileCount(4) + strTableSize(4) + reserved(4)
            int entrySize = 0x18;  // offset(8) + size(8) + nameOffset(4) + reserved(4)
            long dataOffset = headerSize + (entrySize * files.Length) + stringTableData.Length;

            using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new BinaryWriter(output);

            // PFS0 header
            writer.Write(System.Text.Encoding.ASCII.GetBytes("PFS0")); // magic
            writer.Write((int)files.Length);                            // file count
            writer.Write((int)stringTableData.Length);                 // string table size
            writer.Write((int)0);                                      // reserved

            // File entries
            long currentOffset = 0;
            for (int i = 0; i < files.Length; i++)
            {
                long fileSize = new FileInfo(files[i]).Length;
                writer.Write(currentOffset);       // offset relative to data start
                writer.Write(fileSize);            // size
                writer.Write(nameOffsets[i]);       // string table offset
                writer.Write((int)0);              // reserved
                currentOffset += fileSize;
            }

            // String table
            writer.Write(stringTableData);

            // File data
            byte[] buffer = new byte[8 * 1024 * 1024]; // 8MB buffer
            for (int i = 0; i < files.Length; i++)
            {
                using var fileStream = new FileStream(files[i], FileMode.Open, FileAccess.Read, FileShare.Read);
                int bytesRead;
                while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    writer.Write(buffer, 0, bytesRead);
                }
            }
        }

        /// <summary>
        /// Checks if a file entry name is valid for inclusion in an NSP (PFS0) container.
        /// Filters out metadata artifacts (.xml, .json) that tools like yanu-cli create
        /// but which don't belong in the final NSP.
        /// </summary>
        private static bool IsValidNspEntry(string name)
        {
            // Valid NSP entries: .nca, .ncz, .tik, .cert
            // Invalid: .xml, .json, .cnmt.xml (yanu-cli artifacts)
            string ext = System.IO.Path.GetExtension(name).ToLowerInvariant();
            return ext == ".nca" || ext == ".ncz" || ext == ".tik" || ext == ".cert";
        }

        /// <summary>
        /// Safely opens a file from a LibHac IFileSystem by its path.
        /// </summary>
        private static IFile OpenFileSafe(IFileSystem fsToOpen, string pth)
        {
            using var fRef = new UniqueRef<IFile>();
            using var path = new LibHac.Fs.Path();
            path.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(pth))).ThrowIfFailure();
            fsToOpen.OpenFile(ref fRef.Ref, in path, LibHac.Fs.OpenMode.Read).ThrowIfFailure();
            return fRef.Release();
        }

        /// <summary>
        /// Применяет модификации из папки exefs_patches (IPS патчи и бинарные правки)
        /// </summary>
        private static void ApplyExeFsPatches(string exefsPatchesDir, string targetExeFsDir, Models.ProcessingTask task)
        {
            if (!Directory.Exists(exefsPatchesDir) || !Directory.Exists(targetExeFsDir)) return;

            var ipsFiles = Directory.GetFiles(exefsPatchesDir, "*.ips", SearchOption.AllDirectories);
            if (ipsFiles.Length > 0)
            {
                string mainBin = System.IO.Path.Combine(targetExeFsDir, "main");
                if (!File.Exists(mainBin))
                {
                    mainBin = Directory.GetFiles(targetExeFsDir).FirstOrDefault(f => System.IO.Path.GetFileName(f).StartsWith("main", StringComparison.OrdinalIgnoreCase)) ?? "";
                }

                foreach (var ipsFile in ipsFiles)
                {
                    try
                    {
                        if (File.Exists(mainBin))
                        {
                            ApplyIpsPatchToFile(ipsFile, mainBin);
                            App.RunOnUI(() => task.LogDetails += $"\n  ✓ Применен IPS патч: {System.IO.Path.GetFileName(ipsFile)} -> {System.IO.Path.GetFileName(mainBin)}");
                        }
                    }
                    catch (Exception ex)
                    {
                        App.RunOnUI(() => task.LogDetails += $"\n  ⚠️ Ошибка применения IPS патча {System.IO.Path.GetFileName(ipsFile)}: {ex.Message}");
                    }
                }
            }

            // Копируем любые другие файлы из exefs_patches
            try
            {
                foreach (var file in Directory.GetFiles(exefsPatchesDir, "*.*", SearchOption.AllDirectories))
                {
                    if (file.EndsWith(".ips", StringComparison.OrdinalIgnoreCase)) continue;
                    string rel = System.IO.Path.GetRelativePath(exefsPatchesDir, file);
                    string dest = System.IO.Path.Combine(targetExeFsDir, rel);
                    string? dDir = System.IO.Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(dDir)) Directory.CreateDirectory(dDir);
                    File.Copy(file, dest, true);
                }
            }
            catch { }
        }

        /// <summary>
        /// Применяет стандартный IPS/IPSwitch патч к бинарному исполняемому файлу
        /// </summary>
        public static void ApplyIpsPatchToFile(string ipsFilePath, string targetBinaryPath)
        {
            if (!File.Exists(ipsFilePath) || !File.Exists(targetBinaryPath)) return;

            byte[] ips = File.ReadAllBytes(ipsFilePath);
            if (ips.Length < 8) return;

            // Проверка сигнатуры "PATCH" (0x50, 0x41, 0x54, 0x43, 0x48)
            if (ips[0] != (byte)'P' || ips[1] != (byte)'A' || ips[2] != (byte)'T' || ips[3] != (byte)'C' || ips[4] != (byte)'H')
                return;

            using var fs = new FileStream(targetBinaryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            int pos = 5;

            while (pos + 3 <= ips.Length)
            {
                // Проверка EOF ("EOF" = 0x45, 0x4F, 0x46)
                if (ips[pos] == (byte)'E' && ips[pos + 1] == (byte)'O' && ips[pos + 2] == (byte)'F')
                    break;

                int offset = (ips[pos] << 16) | (ips[pos + 1] << 8) | ips[pos + 2];
                pos += 3;

                if (pos + 2 > ips.Length) break;
                int length = (ips[pos] << 8) | ips[pos + 1];
                pos += 2;

                if (length == 0) // RLE запись
                {
                    if (pos + 3 > ips.Length) break;
                    int rleLen = (ips[pos] << 8) | ips[pos + 1];
                    pos += 2;
                    byte val = ips[pos++];

                    if (offset + rleLen > fs.Length)
                        fs.SetLength(offset + rleLen);

                    fs.Seek(offset, SeekOrigin.Begin);
                    byte[] fill = new byte[rleLen];
                    Array.Fill(fill, val);
                    fs.Write(fill, 0, rleLen);
                }
                else // Обычная запись
                {
                    if (pos + length > ips.Length) break;

                    if (offset + length > fs.Length)
                        fs.SetLength(offset + length);

                    fs.Seek(offset, SeekOrigin.Begin);
                    fs.Write(ips, pos, length);
                    pos += length;
                }
            }
        }
    

        /// <summary>
        /// Динамически разрешает пути распакованной структуры (ExeFS с подлинным main.npdm, RomFS, Control NCA и TitleID)
        /// с гарантированным извлечением аутентичного main.npdm и всех бинарников из базовой игры.
        /// </summary>
        private static (string targetExeFs, string targetRomFs, string controlNca, string resolvedTitleId) ResolveUnpackedStructure(
            string tempUnpack,
            string baseFile,
            string? updateFile,
            string keysPath,
            KeysService keysService,
            string fallbackTitleId,
            string yanuCliPath,
            string tempDir,
            string isolatedUserProfile,
            string isolatedLocalAppData)
        {
            // 1. Поиск и сборка единой директории ExeFS (с подлинным main.npdm, библиотеками и обновленным main)
            string targetExeFs = System.IO.Path.Combine(tempUnpack, "final_exefs");
            Directory.CreateDirectory(targetExeFs);

            // 1.1 Гарантированно извлекаем полный ExeFS базовой игры (rtld, main, main.npdm, sdk, subsdk*) ПЕРВЫМ
            if (!string.IsNullOrEmpty(baseFile) && File.Exists(baseFile))
            {
                ExtractExeFsSingleFile(baseFile, targetExeFs, keysService, keysPath, overwriteExisting: true);
            }

            // 1.2 Копируем файлы из basedata/*exefs* в tempUnpack с перезаписью
            var baseExeDirs = Directory.GetDirectories(tempUnpack, "*exefs*", SearchOption.AllDirectories)
                .Where(d => !d.Equals(targetExeFs, StringComparison.OrdinalIgnoreCase) && !d.Contains("patchdata", StringComparison.OrdinalIgnoreCase));
            foreach (var exefsDir in baseExeDirs)
            {
                foreach (var file in Directory.GetFiles(exefsDir))
                {
                    string dest = System.IO.Path.Combine(targetExeFs, System.IO.Path.GetFileName(file));
                    File.Copy(file, dest, true);
                }
            }

            // 1.3 Если есть файл обновления, извлекаем и перезаписываем исполняемые файлы свежими версиями из обновления (overwrite: true)
            if (!string.IsNullOrEmpty(updateFile) && File.Exists(updateFile))
            {
                ExtractExeFsSingleFile(updateFile, targetExeFs, keysService, keysPath, overwriteExisting: true);
                var patchExeDirs = Directory.GetDirectories(tempUnpack, "*exefs*", SearchOption.AllDirectories)
                    .Where(d => !d.Equals(targetExeFs, StringComparison.OrdinalIgnoreCase) && d.Contains("patchdata", StringComparison.OrdinalIgnoreCase));
                foreach (var exefsDir in patchExeDirs)
                {
                    foreach (var file in Directory.GetFiles(exefsDir))
                    {
                        string dest = System.IO.Path.Combine(targetExeFs, System.IO.Path.GetFileName(file));
                        File.Copy(file, dest, true);
                    }
                }
            }

            // 1.4 Если main.npdm все еще отсутствует в targetExeFs — гарантированно извлекаем через fallback
            if (!File.Exists(System.IO.Path.Combine(targetExeFs, "main.npdm")))
            {
                ExtractExeFsFromNspWithLibHac(
                    baseFile,
                    updateFile,
                    targetExeFs,
                    keysService,
                    keysPath,
                    yanuCliPath,
                    tempDir,
                    isolatedUserProfile,
                    isolatedLocalAppData);
            }

            // Если main.npdm найден в любой другой подпапке tempUnpack — копируем его в targetExeFs
            if (!File.Exists(System.IO.Path.Combine(targetExeFs, "main.npdm")))
            {
                var anyNpdm = Directory.GetFiles(tempUnpack, "*.npdm", SearchOption.AllDirectories).FirstOrDefault();
                if (!string.IsNullOrEmpty(anyNpdm) && File.Exists(anyNpdm))
                {
                    try { File.Copy(anyNpdm, System.IO.Path.Combine(targetExeFs, "main.npdm"), true); } catch { }
                }
            }

            if (!File.Exists(System.IO.Path.Combine(targetExeFs, "main.npdm")))
            {
                throw new Exception("Не удалось извлечь оригинальный дескриптор main.npdm из базовой игры. Убедитесь, что ключи prod.keys и title.keys настроены корректно.");
            }

            // 2. Поиск RomFS папки
            string targetRomFs = "";
            var romfsDirs = Directory.GetDirectories(tempUnpack, "romfs", SearchOption.AllDirectories);
            if (romfsDirs.Length > 0)
            {
                targetRomFs = romfsDirs[0];
            }
            else
            {
                targetRomFs = System.IO.Path.Combine(tempUnpack, "romfs");
            }

            // Валидация наличия RomFS: если игра имела размер > 50 МБ, пустой RomFS означает сбой распаковки!
            long baseFileSize = (!string.IsNullOrEmpty(baseFile) && File.Exists(baseFile)) ? new FileInfo(baseFile).Length : 0;
            bool isRomFsExpected = baseFileSize > 50 * 1024 * 1024;
            bool hasRomFsEntries = Directory.Exists(targetRomFs) && Directory.EnumerateFileSystemEntries(targetRomFs).Any();

            if (isRomFsExpected && !hasRomFsEntries)
            {
                try
                {
                    App.Logger.Log("[HardPatchEngine] RomFS пуст после yanu-cli unpack. Попытка резервного извлечения через hactoolnet...", Models.LogLevel.Warning);
                    ExtractRomFsWithHactoolnet(baseFile, updateFile, targetRomFs, keysPath, isolatedUserProfile);
                    hasRomFsEntries = Directory.Exists(targetRomFs) && Directory.EnumerateFileSystemEntries(targetRomFs).Any();
                }
                catch (Exception ex)
                {
                    App.Logger.Log($"[HardPatchEngine] Ошибка резервного извлечения RomFS: {ex.Message}", Models.LogLevel.Warning);
                }
            }

            if (isRomFsExpected && !hasRomFsEntries)
            {
                throw new InvalidOperationException($"Не удалось извлечь RomFS из базовой игры/обновления. Каталог ресурсов пуст. Монолитная сборка невозможна для данного набора файлов.");
            }

            if (!Directory.Exists(targetRomFs)) Directory.CreateDirectory(targetRomFs);

            // 3. Поиск Control NCA и TitleID
            string controlNca = "";
            ulong maxTitleId = 0;

            var controlCandidates = Directory.GetFiles(tempUnpack, "control.nca", SearchOption.AllDirectories)
                .OrderByDescending(f => f.Contains("patchdata", StringComparison.OrdinalIgnoreCase) ? 2 : (f.Contains("basedata", StringComparison.OrdinalIgnoreCase) ? 1 : 0))
                .ToArray();

            if (controlCandidates.Length > 0)
            {
                controlNca = controlCandidates[0];
            }

            var ncaFiles = Directory.GetFiles(tempUnpack, "*.nca", SearchOption.AllDirectories)
                .OrderByDescending(f => f.Contains("patchdata", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ToList();

            foreach (var ncaFile in ncaFiles)
            {
                try
                {
                    if (keysService.IsLoaded)
                    {
                        using var fs = new FileStream(ncaFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                        var nca = new LibHac.Tools.FsSystem.NcaUtils.Nca(keysService.CurrentKeyset, fs.AsStorage());
                        if (nca.Header.ContentType == LibHac.Tools.FsSystem.NcaUtils.NcaContentType.Control)
                        {
                            if (nca.Header.TitleId >= maxTitleId)
                            {
                                maxTitleId = nca.Header.TitleId;
                                if (string.IsNullOrEmpty(controlNca) || ncaFile.Contains("patchdata", StringComparison.OrdinalIgnoreCase))
                                {
                                    controlNca = ncaFile;
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            if (string.IsNullOrEmpty(controlNca))
            {
                controlNca = ncaFiles.FirstOrDefault(f => f.EndsWith(".nca", StringComparison.OrdinalIgnoreCase)) ?? "";
            }

            // Fallback: извлечение Control NCA напрямую из файлов, если yanu unpack его не извлек
            if (string.IsNullOrEmpty(controlNca) || !File.Exists(controlNca))
            {
                controlNca = ExtractControlNcaIfMissing(baseFile, updateFile, tempUnpack, keysService);
            }

            ulong baseTidUlong = maxTitleId > 0 ? (maxTitleId & ~0xFFFUL) : 0;
            string resolvedTitleId = baseTidUlong > 0 ? baseTidUlong.ToString("X16") : fallbackTitleId;
            if (resolvedTitleId.Length == 16 && !resolvedTitleId.EndsWith("000", StringComparison.OrdinalIgnoreCase))
            {
                resolvedTitleId = resolvedTitleId.Substring(0, 13) + "000";
            }

            ulong npdmBaseTid = 0;
            if (baseTidUlong > 0)
            {
                npdmBaseTid = baseTidUlong;
            }
            else if (!string.IsNullOrEmpty(resolvedTitleId) && ulong.TryParse(resolvedTitleId, System.Globalization.NumberStyles.HexNumber, null, out ulong parsedTid))
            {
                npdmBaseTid = parsedTid;
            }

            if (npdmBaseTid > 0)
            {
                string npdmFile = System.IO.Path.Combine(targetExeFs, "main.npdm");
                PatchNpdmTitleId(npdmFile, npdmBaseTid);
            }

            return (targetExeFs, targetRomFs, controlNca, resolvedTitleId);
        }

        /// <summary>
        /// Извлекает файлы ExeFS (включая оригинальный main.npdm) напрямую из базового и обновленного NSP/XCI через yanu-cli / LibHac / hactoolnet
        /// </summary>
        private static void ExtractExeFsFromNspWithLibHac(
            string baseFile,
            string? updateFile,
            string targetExeFs,
            KeysService keysService,
            string keysPath,
            string yanuCliPath,
            string tempDir,
            string isolatedUserProfile,
            string isolatedLocalAppData)
        {
            Directory.CreateDirectory(targetExeFs);

            // 1. Метод 1: Прямое извлечение оригинального ExeFS / main.npdm из базовой игры через yanu-cli unpack --base
            if (!File.Exists(System.IO.Path.Combine(targetExeFs, "main.npdm")) && !string.IsNullOrEmpty(baseFile) && File.Exists(baseFile))
            {
                ExtractExeFsWithYanuBaseUnpack(
                    baseFile,
                    targetExeFs,
                    keysPath,
                    yanuCliPath,
                    tempDir,
                    isolatedUserProfile,
                    isolatedLocalAppData);
            }

            // 2. Метод 2: Извлечение ExeFS через LibHac (NSP/XCI)
            if (!File.Exists(System.IO.Path.Combine(targetExeFs, "main.npdm")))
            {
                if (!string.IsNullOrEmpty(baseFile) && File.Exists(baseFile))
                {
                    ExtractExeFsSingleFile(baseFile, targetExeFs, keysService, keysPath);
                }
                if (!string.IsNullOrEmpty(updateFile) && File.Exists(updateFile))
                {
                    ExtractExeFsSingleFile(updateFile, targetExeFs, keysService, keysPath);
                }
            }

            // 3. Метод 3: Fallback-извлечение через hactoolnet
            if (!File.Exists(System.IO.Path.Combine(targetExeFs, "main.npdm")))
            {
                ExtractExeFsWithHactoolnet(baseFile, updateFile, targetExeFs, keysPath);
            }
        }

        /// <summary>
        /// Извлекает файлы ExeFS (включая оригинальный main.npdm) напрямую через базовую распаковку yanu-cli
        /// </summary>
        private static void ExtractExeFsWithYanuBaseUnpack(
            string baseFile,
            string targetExeFs,
            string keysPath,
            string yanuCliPath,
            string tempDir,
            string isolatedUserProfile,
            string isolatedLocalAppData)
        {
            if (string.IsNullOrEmpty(yanuCliPath) || !File.Exists(yanuCliPath) || string.IsNullOrEmpty(baseFile) || !File.Exists(baseFile)) return;

            string tempBaseUnpack = System.IO.Path.Combine(tempDir, "base_npdm_unpack");
            try
            {
                Directory.CreateDirectory(tempBaseUnpack);

                string defaultKeysPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "prod.keys");
                bool isDefaultLocation = false;
                try
                {
                    if (!string.IsNullOrEmpty(keysPath) && File.Exists(keysPath) && File.Exists(defaultKeysPath))
                    {
                        isDefaultLocation = System.IO.Path.GetFullPath(keysPath).Equals(System.IO.Path.GetFullPath(defaultKeysPath), StringComparison.OrdinalIgnoreCase);
                    }
                }
                catch { }

                string keyfileFlag = (!string.IsNullOrEmpty(keysPath) && File.Exists(keysPath) && !isDefaultLocation) ? $"-k \"{keysPath}\" " : "";
                string unpackArgs = $"{keyfileFlag}unpack --base \"{baseFile}\" -o \"{tempBaseUnpack}\"";

                var psi = new ProcessStartInfo
                {
                    FileName = yanuCliPath,
                    Arguments = unpackArgs,
                    WorkingDirectory = tempBaseUnpack,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };
                psi.EnvironmentVariables["USERPROFILE"] = isolatedUserProfile;
                psi.EnvironmentVariables["LOCALAPPDATA"] = isolatedLocalAppData;
                psi.EnvironmentVariables["APPDATA"] = isolatedLocalAppData;
                psi.EnvironmentVariables["TEMP"] = tempDir;
                psi.EnvironmentVariables["TMP"] = tempDir;

                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();
                    proc.WaitForExit(60000);
                    var npdmFiles = Directory.GetFiles(tempBaseUnpack, "main.npdm", SearchOption.AllDirectories);
                    if (npdmFiles.Length > 0)
                    {
                        string baseExeFsDir = System.IO.Path.GetDirectoryName(npdmFiles[0])!;
                        foreach (var file in Directory.GetFiles(baseExeFsDir))
                        {
                            string fileName = System.IO.Path.GetFileName(file);
                            string dest = System.IO.Path.Combine(targetExeFs, fileName);
                            // main.npdm, sdk, rtld и subsdk копируем всегда; main не перезаписываем, если в targetExeFs уже есть обновленный main
                            if (fileName.Equals("main.npdm", StringComparison.OrdinalIgnoreCase) || !File.Exists(dest))
                            {
                                File.Copy(file, dest, true);
                            }
                        }
                        App.Logger.Log($"[HardPatchEngine] Успешно извлечен оригинальный main.npdm через yanu-cli unpack.", Models.LogLevel.Info);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[HardPatchEngine] ExtractExeFsWithYanuBaseUnpack warning: {ex.Message}", Models.LogLevel.Warning);
            }
            finally
            {
                try { if (Directory.Exists(tempBaseUnpack)) Directory.Delete(tempBaseUnpack, true); } catch { }
            }
        }

        /// <summary>
        /// Извлекает ExeFS из отдельного файла контейнера (.nsp/.xci) с предварительным сбором тикетов TitleKey
        /// </summary>
        private static void ExtractExeFsSingleFile(string filePath, string targetExeFs, KeysService keysService, string keysPath, bool overwriteExisting = false)
        {
            try
            {
                if (!File.Exists(filePath)) return;

                bool isXci = filePath.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase);

                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                IStorage storage = fileStream.AsStorage();

                IFileSystem? fileSystem = null;

                if (isXci)
                {
                    storage.GetSize(out long storageSize).ThrowIfFailure();
                    if (storageSize > 0x10000)
                    {
                        var rootStorage = new SubStorage(storage, 0x10000, storageSize - 0x10000);
                        var rootPfs = new PartitionFileSystem(rootStorage);
                        using var secureFile = new UniqueRef<IFile>();
                        using var securePath = new LibHac.Fs.Path();
                        securePath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes("/secure"))).ThrowIfFailure();
                        if (rootPfs.OpenFile(ref secureFile.Ref, in securePath, OpenMode.Read).IsSuccess())
                        {
                            fileSystem = new PartitionFileSystem(secureFile.Release().AsStorage());
                        }
                    }
                }
                else
                {
                    fileSystem = new PartitionFileSystem(storage);
                }

                if (fileSystem == null) return;

                // Харвестинг тикетов (.tik) и регистрация TitleKeys
                var keysToAdd = new List<string>();
                foreach (var entry in fileSystem.EnumerateEntries().Where(e => e.Name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase)))
                {
                    try
                    {
                        using var tikFileRef = new UniqueRef<IFile>();
                        using var tikPath = new LibHac.Fs.Path();
                        tikPath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(entry.FullPath))).ThrowIfFailure();
                        if (fileSystem.OpenFile(ref tikFileRef.Ref, in tikPath, OpenMode.Read).IsSuccess())
                        {
                            using var tikFile = tikFileRef.Release();
                            byte[] tikBytes = new byte[0x400];
                            tikFile.Read(out long bytesRead, 0, tikBytes).ThrowIfFailure();
                            if (bytesRead >= 0x100)
                            {
                                var ticketInfo = TicketHarvesterService.ExtractTicketInfo(tikBytes, (int)bytesRead);
                                if (ticketInfo.HasValue && !string.IsNullOrEmpty(ticketInfo.Value.RightsId) && !string.IsNullOrEmpty(ticketInfo.Value.TitleKey))
                                {
                                    byte[] tKey = Convert.FromHexString(ticketInfo.Value.TitleKey);
                                    lock (Core.NSZ.StormNczCompressor.TitleKeysCache)
                                    {
                                        Core.NSZ.StormNczCompressor.TitleKeysCache[ticketInfo.Value.RightsId] = tKey;
                                    }
                                    keysToAdd.Add($"{ticketInfo.Value.RightsId} = {ticketInfo.Value.TitleKey}");
                                }
                            }
                        }
                    }
                    catch { }
                }

                if (keysToAdd.Count > 0)
                {
                    try
                    {
                        string titleKeysPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "title.keys");
                        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(titleKeysPath)!);
                        var existingLines = File.Exists(titleKeysPath) ? File.ReadAllLines(titleKeysPath).ToList() : new List<string>();
                        foreach (var k in keysToAdd)
                        {
                            string rId = k.Split('=')[0].Trim();
                            if (!existingLines.Any(l => l.StartsWith(rId, StringComparison.OrdinalIgnoreCase)))
                            {
                                existingLines.Add(k);
                            }
                        }
                        File.WriteAllLines(titleKeysPath, existingLines);
                        try
                        {
                            string isoTitleKeys = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StormSwitchBox", "user_profile", ".switch", "title.keys");
                            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(isoTitleKeys)!);
                            File.WriteAllLines(isoTitleKeys, existingLines);
                        }
                        catch { }
                        keysService.LoadKeys(keysService.KeysFilePath ?? keysPath);
                    }
                    catch { }
                }

                // Поиск Program NCA и распаковка ExeFS
                foreach (var entry in fileSystem.EnumerateEntries().Where(e => e.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase)))
                {
                    using var ncaFileRef = new UniqueRef<IFile>();
                    using var ncaPath = new LibHac.Fs.Path();
                    ncaPath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(entry.FullPath))).ThrowIfFailure();
                    if (fileSystem.OpenFile(ref ncaFileRef.Ref, in ncaPath, OpenMode.Read).IsSuccess())
                    {
                        try
                        {
                            IFile ncaFile = ncaFileRef.Release();
                            var nca = new LibHac.Tools.FsSystem.NcaUtils.Nca(keysService.CurrentKeyset, ncaFile.AsStorage());
                            if (nca.Header.ContentType == LibHac.Tools.FsSystem.NcaUtils.NcaContentType.Program || (byte)nca.Header.ContentType == 3 || (byte)nca.Header.ContentType == 0)
                            {
                                for (int section = 0; section < 4; section++)
                                {
                                    if (nca.CanOpenSection(section))
                                    {
                                        try
                                        {
                                            IFileSystem? exefs = null;
                                            try { exefs = nca.OpenFileSystem(section, IntegrityCheckLevel.None); } catch { }
                                            if (exefs == null)
                                            {
                                                try { exefs = nca.OpenFileSystem(section, IntegrityCheckLevel.IgnoreOnInvalid); } catch { }
                                            }

                                            if (exefs != null)
                                            {
                                                var exefsEntries = exefs.EnumerateEntries("/", "*").ToList();
                                                bool isExeFs = exefsEntries.Any(e => e.Name.Equals("main.npdm", StringComparison.OrdinalIgnoreCase) || e.Name.Equals("main", StringComparison.OrdinalIgnoreCase));
                                                if (isExeFs)
                                                {
                                                    Directory.CreateDirectory(targetExeFs);
                                                    foreach (var exefsEntry in exefsEntries)
                                                    {
                                                        string destFile = System.IO.Path.Combine(targetExeFs, exefsEntry.Name.TrimStart('/'));
                                                        // main.npdm, sdk, rtld копируем всегда; main не перезаписываем, если уже есть обновленный main
                                                        if (overwriteExisting || exefsEntry.Name.TrimStart('/').Equals("main.npdm", StringComparison.OrdinalIgnoreCase) || !File.Exists(destFile))
                                                        {
                                                            using var srcFileRef = new UniqueRef<IFile>();
                                                            using var srcPath = new LibHac.Fs.Path();
                                                            srcPath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(exefsEntry.FullPath))).ThrowIfFailure();
                                                            if (exefs.OpenFile(ref srcFileRef.Ref, in srcPath, OpenMode.Read).IsSuccess())
                                                            {
                                                                using var srcFile = srcFileRef.Release();
                                                                using var outStream = new FileStream(destFile, FileMode.Create, FileAccess.Write);
                                                                srcFile.AsStream().CopyTo(outStream);
                                                            }
                                                        }
                                                    }

                                                    if (File.Exists(System.IO.Path.Combine(targetExeFs, "main.npdm")))
                                                    {
                                                        App.Logger.Log($"[HardPatchEngine] Успешно извлечен ExeFS (main.npdm) из {System.IO.Path.GetFileName(filePath)} section {section} через LibHac.", Models.LogLevel.Info);
                                                    }
                                                }
                                            }
                                        }
                                        catch { }
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[HardPatchEngine] ExtractExeFsSingleFile ({System.IO.Path.GetFileName(filePath)}): {ex.Message}", Models.LogLevel.Warning);
            }
        }

        /// <summary>
        /// Резервное извлечение ExeFS через утилиту hactoolnet
        /// </summary>
        private static void ExtractExeFsWithHactoolnet(string baseFile, string? updateFile, string targetExeFs, string keysPath)
        {
            string? hactoolnetPath = FindHactoolnet();
            if (string.IsNullOrEmpty(hactoolnetPath) || !File.Exists(hactoolnetPath)) return;

            try
            {
                string titleKeysPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "title.keys");
                string tkFlag = File.Exists(titleKeysPath) ? $"--titlekeys \"{titleKeysPath}\" " : "";
                string kFlag = (!string.IsNullOrEmpty(keysPath) && File.Exists(keysPath)) ? $"-k \"{keysPath}\" " : "";
                Directory.CreateDirectory(targetExeFs);

                if (!string.IsNullOrEmpty(baseFile) && File.Exists(baseFile))
                {
                    string tFlag = (baseFile.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) || baseFile.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase)) ? "-t xci" : "-t pfs0";
                    var psi = new ProcessStartInfo
                    {
                        FileName = hactoolnetPath,
                        Arguments = $"{kFlag}{tkFlag}{tFlag} --exefsdir \"{targetExeFs}\" \"{baseFile}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    proc?.WaitForExit(30000);
                }

                if (!string.IsNullOrEmpty(updateFile) && File.Exists(updateFile))
                {
                    string tFlag = (updateFile.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) || updateFile.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase)) ? "-t xci" : "-t pfs0";
                    var psi = new ProcessStartInfo
                    {
                        FileName = hactoolnetPath,
                        Arguments = $"{kFlag}{tkFlag}{tFlag} --exefsdir \"{targetExeFs}\" \"{updateFile}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    proc?.WaitForExit(30000);
                }

                App.Logger.Log($"[HardPatchEngine] hactoolnet ExeFS extraction check completed.", Models.LogLevel.Info);
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[HardPatchEngine] hactoolnet fallback error: {ex.Message}", Models.LogLevel.Warning);
            }
        }

        /// <summary>
        /// Резервное извлечение RomFS через утилиту hactoolnet
        /// </summary>
        private static void ExtractRomFsWithHactoolnet(string baseFile, string? updateFile, string targetRomFs, string keysPath, string isolatedUserProfile)
        {
            string? hactoolnetPath = FindHactoolnet();
            if (string.IsNullOrEmpty(hactoolnetPath) || !File.Exists(hactoolnetPath)) return;

            try
            {
                string titleKeysPath = System.IO.Path.Combine(isolatedUserProfile, ".switch", "title.keys");
                if (!File.Exists(titleKeysPath))
                    titleKeysPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "title.keys");

                string tkFlag = File.Exists(titleKeysPath) ? $"--titlekeys \"{titleKeysPath}\" " : "";
                string kFlag = (!string.IsNullOrEmpty(keysPath) && File.Exists(keysPath)) ? $"-k \"{keysPath}\" " : "";
                Directory.CreateDirectory(targetRomFs);

                if (!string.IsNullOrEmpty(baseFile) && File.Exists(baseFile))
                {
                    string tFlag = (baseFile.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) || baseFile.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase)) ? "-t xci" : "-t pfs0";
                    var psi = new ProcessStartInfo
                    {
                        FileName = hactoolnetPath,
                        Arguments = $"{kFlag}{tkFlag}{tFlag} --romfsdir \"{targetRomFs}\" \"{baseFile}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    psi.EnvironmentVariables["USERPROFILE"] = isolatedUserProfile;
                    using var proc = Process.Start(psi);
                    proc?.WaitForExit(120000);
                }

                if (!string.IsNullOrEmpty(updateFile) && File.Exists(updateFile))
                {
                    string tFlag = (updateFile.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) || updateFile.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase)) ? "-t xci" : "-t pfs0";
                    var psi = new ProcessStartInfo
                    {
                        FileName = hactoolnetPath,
                        Arguments = $"{kFlag}{tkFlag}{tFlag} --romfsdir \"{targetRomFs}\" \"{updateFile}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };
                    psi.EnvironmentVariables["USERPROFILE"] = isolatedUserProfile;
                    using var proc = Process.Start(psi);
                    proc?.WaitForExit(120000);
                }

                App.Logger.Log($"[HardPatchEngine] hactoolnet RomFS extraction check completed.", Models.LogLevel.Info);
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[HardPatchEngine] hactoolnet RomFS fallback error: {ex.Message}", Models.LogLevel.Warning);
            }
        }

        /// <summary>
        /// Извлекает файл Control NCA из базового файла или обновления, если yanu unpack его не создал
        /// </summary>
        private static string ExtractControlNcaIfMissing(string baseFile, string? updateFile, string tempUnpack, KeysService keysService)
        {
            string controlPath = System.IO.Path.Combine(tempUnpack, "control.nca");
            string[] sources = string.IsNullOrEmpty(updateFile) ? new[] { baseFile } : new[] { updateFile, baseFile };

            foreach (var file in sources)
            {
                try
                {
                    if (!File.Exists(file)) continue;
                    bool isXci = file.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase);

                    using var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                    IStorage storage = fileStream.AsStorage();
                    IFileSystem? fileSystem = null;

                    if (isXci)
                    {
                        storage.GetSize(out long storageSize).ThrowIfFailure();
                        if (storageSize > 0x10000)
                        {
                            var rootStorage = new SubStorage(storage, 0x10000, storageSize - 0x10000);
                            var rootPfs = new PartitionFileSystem(rootStorage);
                            using var secureFile = new UniqueRef<IFile>();
                            using var securePath = new LibHac.Fs.Path();
                            securePath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes("/secure"))).ThrowIfFailure();
                            if (rootPfs.OpenFile(ref secureFile.Ref, in securePath, OpenMode.Read).IsSuccess())
                            {
                                fileSystem = new PartitionFileSystem(secureFile.Release().AsStorage());
                            }
                        }
                    }
                    else
                    {
                        fileSystem = new PartitionFileSystem(storage);
                    }

                    if (fileSystem == null) continue;

                    foreach (var entry in fileSystem.EnumerateEntries().Where(e => e.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase)))
                    {
                        using var ncaFileRef = new UniqueRef<IFile>();
                        using var ncaPath = new LibHac.Fs.Path();
                        ncaPath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(entry.FullPath))).ThrowIfFailure();
                        if (fileSystem.OpenFile(ref ncaFileRef.Ref, in ncaPath, OpenMode.Read).IsSuccess())
                        {
                            try
                            {
                                IFile ncaFile = ncaFileRef.Release();
                                var nca = new LibHac.Tools.FsSystem.NcaUtils.Nca(keysService.CurrentKeyset, ncaFile.AsStorage());
                                if (nca.Header.ContentType == LibHac.Tools.FsSystem.NcaUtils.NcaContentType.Control || (byte)nca.Header.ContentType == 2)
                                {
                                    using var outStream = new FileStream(controlPath, FileMode.Create, FileAccess.Write);
                                    ncaFile.AsStream().CopyTo(outStream);
                                    if (File.Exists(controlPath) && new FileInfo(controlPath).Length > 0)
                                    {
                                        App.Logger.Log($"[HardPatchEngine] Извлечен Control NCA из {System.IO.Path.GetFileName(file)} -> {controlPath}", Models.LogLevel.Info);
                                        return controlPath;
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }

            return "";
        }

        private static string? FindHactoolnet()
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] searchPaths = new[]
            {
                System.IO.Path.Combine(appDir, "tools", "com.github.nozwock.yanu", "hactoolnet.exe"),
                System.IO.Path.Combine(appDir, "tools", "hactoolnet.exe"),
                System.IO.Path.Combine(appDir, "..", "..", "..", "tools", "com.github.nozwock.yanu", "hactoolnet.exe"),
                System.IO.Path.Combine(appDir, "..", "..", "..", "..", "tools", "com.github.nozwock.yanu", "hactoolnet.exe"),
                System.IO.Path.Combine(appDir, "..", "..", "..", "..", "..", "tools", "com.github.nozwock.yanu", "hactoolnet.exe"),
            };

            foreach (var p in searchPaths)
            {
                string full = System.IO.Path.GetFullPath(p);
                if (File.Exists(full)) return full;
            }

            return null;
        }
/// <summary>
        /// Патчит TitleID в дескрипторе main.npdm (секции ACI0 и ACID) на Base TitleID (...000) для предотвращения черного экрана.
        /// </summary>
        private static void PatchNpdmTitleId(string npdmPath, ulong baseTitleId)
        {
            if (!File.Exists(npdmPath) || baseTitleId == 0) return;
            try
            {
                byte[] npdm = File.ReadAllBytes(npdmPath);
                if (npdm.Length < 0x80) return;
                if (npdm[0] != (byte)'M' || npdm[1] != (byte)'E' || npdm[2] != (byte)'T' || npdm[3] != (byte)'A') return;

                // ACI0 offset pointer at 0x70
                uint aci0Off = BitConverter.ToUInt32(npdm, 0x70);
                if (aci0Off >= 0x80 && aci0Off + 0x18 <= npdm.Length &&
                    npdm[aci0Off] == (byte)'A' && npdm[aci0Off + 1] == (byte)'C' && npdm[aci0Off + 2] == (byte)'I' && npdm[aci0Off + 3] == (byte)'0')
                {
                    BitConverter.GetBytes(baseTitleId).CopyTo(npdm, (int)aci0Off + 0x10);
                }

                // ACID offset pointer at 0x78
                uint acidOff = BitConverter.ToUInt32(npdm, 0x78);
                if (acidOff >= 0x80 && acidOff + 0x220 <= npdm.Length &&
                    npdm[acidOff + 0x200] == (byte)'A' && npdm[acidOff + 0x201] == (byte)'C' && npdm[acidOff + 0x202] == (byte)'I' && npdm[acidOff + 0x203] == (byte)'D')
                {
                    BitConverter.GetBytes(baseTitleId).CopyTo(npdm, (int)acidOff + 0x210);
                    BitConverter.GetBytes(baseTitleId).CopyTo(npdm, (int)acidOff + 0x218);
                }

                File.WriteAllBytes(npdmPath, npdm);
                App.Logger.Log($"[HardPatchEngine] main.npdm успешно пропатчен на Base TitleID: {baseTitleId:X16}", Models.LogLevel.Info);
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[HardPatchEngine] PatchNpdmTitleId warning: {ex.Message}", Models.LogLevel.Warning);
            }
        }

        /// <summary>
        /// Копирует содержимое папки с перезаписью и фильтрацией мусорных файлов и инструкций/ReadMe.
        /// </summary>
        private static void CopyDirectoryContentFiltered(string sourceDir, string destinationDir)
        {
            if (!Directory.Exists(destinationDir))
            {
                Directory.CreateDirectory(destinationDir);
            }

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                if (ModOptimizerService.IsJunkFile(file)) continue;
                string fn = System.IO.Path.GetFileName(file).ToLowerInvariant();
                if (fn.EndsWith(".txt") || fn.EndsWith(".md") || fn.EndsWith(".url") || fn.EndsWith(".nfo") ||
                    fn.EndsWith(".bat") || fn.EndsWith(".cmd") || fn.StartsWith("readme") ||
                    fn.Contains("установ") || fn.Contains("автор") || fn.Contains("changelog"))
                {
                    continue; // Пропускаем инструкции, текстовые файлы и ридми из модов
                }
                string dest = System.IO.Path.Combine(destinationDir, System.IO.Path.GetFileName(file));
                File.Copy(file, dest, true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string dirName = System.IO.Path.GetFileName(dir);
                if (dirName.Equals(".git", StringComparison.OrdinalIgnoreCase) || 
                    dirName.Equals(".svn", StringComparison.OrdinalIgnoreCase)) continue;
                string dest = System.IO.Path.Combine(destinationDir, dirName);
                CopyDirectoryContentFiltered(dir, dest);
            }
        }

        /// <summary>
        /// Автоматически внедряет одиночный файл мода (.pak, .sarc.zs) в подходящий подкаталог внутри RomFS.
        /// </summary>
        private static void InjectStandaloneModFile(string modFilePath, string targetRomFs, Models.ProcessingTask task)
        {
            if (!File.Exists(modFilePath) || !Directory.Exists(targetRomFs)) return;
            try
            {
                string fileName = System.IO.Path.GetFileName(modFilePath);
                string ext = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
                string targetDir = targetRomFs;

                if (ext == ".pak")
                {
                    // Ищем существующую папку Paks внутри targetRomFs
                    var pakDirs = Directory.GetDirectories(targetRomFs, "*pak*", SearchOption.AllDirectories);
                    if (pakDirs.Length > 0)
                    {
                        targetDir = pakDirs[0];
                    }
                    else
                    {
                        var pakFiles = Directory.GetFiles(targetRomFs, "*.pak", SearchOption.AllDirectories);
                        if (pakFiles.Length > 0)
                        {
                            targetDir = System.IO.Path.GetDirectoryName(pakFiles[0])!;
                        }
                    }
                }
                else if (fileName.EndsWith(".sarc.zs", StringComparison.OrdinalIgnoreCase) || ext == ".sarc" || ext == ".zs")
                {
                    // Ищем папку Mals или расположение .sarc.zs в RomFS
                    var malsDirs = Directory.GetDirectories(targetRomFs, "*mals*", SearchOption.AllDirectories);
                    if (malsDirs.Length > 0)
                    {
                        targetDir = malsDirs[0];
                    }
                    else
                    {
                        var sarcFiles = Directory.GetFiles(targetRomFs, "*.sarc.zs", SearchOption.AllDirectories);
                        if (sarcFiles.Length > 0)
                        {
                            targetDir = System.IO.Path.GetDirectoryName(sarcFiles[0])!;
                        }
                    }
                }

                Directory.CreateDirectory(targetDir);
                string destPath = System.IO.Path.Combine(targetDir, fileName);
                File.Copy(modFilePath, destPath, true);
                App.Logger.Log($"[HardPatchEngine] Внедрен автономный мод {fileName} -> {destPath}", Models.LogLevel.Info);
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[HardPatchEngine] Ошибка внедрения автономного мода {modFilePath}: {ex.Message}", Models.LogLevel.Warning);
            }
        }


        private static System.Text.Encoding? GetCp866EncodingSafe()
        {
            try
            {
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                return System.Text.Encoding.GetEncoding(866);
            }
            catch { return null; }
        }

        private static System.Text.Encoding? GetWin1251EncodingSafe()
        {
            try
            {
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                return System.Text.Encoding.GetEncoding(1251);
            }
            catch { return null; }
        }

        private static byte[] NcaXtsCryptHeader0xC00(byte[] hdr0xC00, byte[] headerKey32, bool decrypt)
        {
            byte[] k1 = new byte[16];
            byte[] k2 = new byte[16];
            Buffer.BlockCopy(headerKey32, 0, k1, 0, 16);
            Buffer.BlockCopy(headerKey32, 16, k2, 0, 16);

            using var aes1 = System.Security.Cryptography.Aes.Create();
            aes1.Mode = System.Security.Cryptography.CipherMode.ECB;
            aes1.Padding = System.Security.Cryptography.PaddingMode.None;
            aes1.Key = k1;
            using var mainTransform = decrypt ? aes1.CreateDecryptor() : aes1.CreateEncryptor();

            using var aes2 = System.Security.Cryptography.Aes.Create();
            aes2.Mode = System.Security.Cryptography.CipherMode.ECB;
            aes2.Padding = System.Security.Cryptography.PaddingMode.None;
            aes2.Key = k2;
            using var tweakEncryptor = aes2.CreateEncryptor();

            byte[] output = new byte[0xC00];
            byte[] tweakIn = new byte[16];
            byte[] tw = new byte[16];
            byte[] xBuf = new byte[16];
            byte[] pBuf = new byte[16];

            for (int sec = 0; sec < 0xC00 / 0x200; sec++)
            {
                Array.Clear(tweakIn, 0, 16);
                ulong n = (ulong)sec;
                for (int b = 15; b >= 0; b--)
                {
                    tweakIn[b] = (byte)(n & 0xFF);
                    n >>= 8;
                }
                tweakEncryptor.TransformBlock(tweakIn, 0, 16, tw, 0);

                for (int blk = 0; blk < 0x200 / 16; blk++)
                {
                    int off = sec * 0x200 + blk * 16;
                    for (int j = 0; j < 16; j++) xBuf[j] = (byte)(hdr0xC00[off + j] ^ tw[j]);
                    mainTransform.TransformBlock(xBuf, 0, 16, pBuf, 0);
                    for (int j = 0; j < 16; j++) output[off + j] = (byte)(pBuf[j] ^ tw[j]);

                    int carry = (tw[15] >> 7) & 1;
                    for (int j = 15; j > 0; j--)
                    {
                        tw[j] = (byte)(((tw[j] << 1) | ((tw[j - 1] >> 7) & 1)) & 0xFF);
                    }
                    tw[0] = (byte)(((tw[0] << 1) ^ (carry != 0 ? 0x87 : 0)) & 0xFF);
                }
            }
            return output;
        }

        private static byte[] AesEcbDecryptBlock16(byte[] block16, byte[] key16)
        {
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Mode = System.Security.Cryptography.CipherMode.ECB;
            aes.Padding = System.Security.Cryptography.PaddingMode.None;
            aes.Key = key16;
            using var dec = aes.CreateDecryptor();
            byte[] outBuf = new byte[16];
            dec.TransformBlock(block16, 0, 16, outBuf, 0);
            return outBuf;
        }

        private static void AesCtrCryptInPlace(byte[] buf, int offset, int length, byte[] key16, ReadOnlySpan<byte> genSec8, long ncaByteOffset)
        {
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Mode = System.Security.Cryptography.CipherMode.ECB;
            aes.Padding = System.Security.Cryptography.PaddingMode.None;
            aes.Key = key16;
            using var enc = aes.CreateEncryptor();

            byte[] ctr = new byte[16];
            for (int j = 0; j < 8; j++) ctr[j] = genSec8[7 - j];
            byte[] ks = new byte[16];

            int pos = 0;
            while (pos < length)
            {
                long absPos = ncaByteOffset + pos;
                long blkIdx = absPos >> 4;
                int inBlk = (int)(absPos & 15);
                System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(ctr.AsSpan(8, 8), (ulong)blkIdx);
                enc.TransformBlock(ctr, 0, 16, ks, 0);
                int chunk = Math.Min(length - pos, 16 - inBlk);
                for (int k = 0; k < chunk; k++)
                {
                    buf[offset + pos + k] ^= ks[inBlk + k];
                }
                pos += chunk;
            }
        }

        private static byte[]? ResolveNcaCtrKey(byte[] decHdr, LibHac.Common.Keys.KeySet keyset)
        {
            int c1 = decHdr[0x206];
            int c2 = decHdr[0x220];
            int keygen = Math.Max(c1, c2);
            int primarySlot = Math.Max(0, keygen - 1);
            int kaekIdx = decHdr[0x207];
            byte[] encSlot2 = new byte[16];
            Buffer.BlockCopy(decHdr, 0x320, encSlot2, 0, 16);

            var slots = new List<int> { primarySlot, keygen };
            for (int g = 0; g < keyset.KeyAreaKeys.Length; g++)
                if (!slots.Contains(g)) slots.Add(g);

            foreach (int slot in slots)
            {
                if (slot < 0 || slot >= keyset.KeyAreaKeys.Length) continue;
                int kIdx = (kaekIdx >= 0 && kaekIdx < 3) ? kaekIdx : 0;
                byte[] areaKey = new byte[16];
                ((ReadOnlySpan<byte>)keyset.KeyAreaKeys[slot][kIdx]).CopyTo(areaKey);
                bool zero = true;
                for (int b = 0; b < 16; b++) if (areaKey[b] != 0) { zero = false; break; }
                if (zero) continue;

                byte[] dec = AesEcbDecryptBlock16(encSlot2, areaKey);
                bool isHacpack = true;
                for (int b = 0; b < 16; b++) if (dec[b] != 0x04) { isHacpack = false; break; }
                if (isHacpack || slot == primarySlot) return dec;
            }
            return null;
        }

        public static void NormalizeMergedNspSectionOffsetsTo0x4000(string nspPath, KeysService keysService, uint? expectedTitleVersion = null, Models.ProcessingTask? task = null)
        {
            if (string.IsNullOrEmpty(nspPath) || !File.Exists(nspPath) || keysService?.CurrentKeyset == null)
                return;

            byte[] headerKey = new byte[32];
            ((ReadOnlySpan<byte>)keysService.CurrentKeyset.HeaderKey).CopyTo(headerKey);
            bool hasHeaderKey = false;
            for (int i = 0; i < 32; i++) if (headerKey[i] != 0) { hasHeaderKey = true; break; }
            if (!hasHeaderKey) return;

            string workDir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(nspPath) ?? System.IO.Path.GetTempPath(), "_norm0x4000_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            try
            {
                var pfsEntries = new List<(string Name, long AbsOffset, long Size)>();
                using (var fsIn = new FileStream(nspPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (fsIn.Length < 16) return;
                    byte[] pfsHdr = new byte[16];
                    if (fsIn.Read(pfsHdr, 0, 16) != 16) return;
                    if (pfsHdr[0] != 'P' || pfsHdr[1] != 'F' || pfsHdr[2] != 'S' || pfsHdr[3] != '0') return;

                    int numFiles = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(pfsHdr.AsSpan(4, 4));
                    int strTabSize = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(pfsHdr.AsSpan(8, 4));
                    if (numFiles <= 0 || numFiles > 256 || strTabSize <= 0 || strTabSize > 1024 * 1024) return;

                    byte[] entBuf = new byte[numFiles * 24];
                    if (fsIn.Read(entBuf, 0, entBuf.Length) != entBuf.Length) return;
                    byte[] strTab = new byte[strTabSize];
                    if (fsIn.Read(strTab, 0, strTabSize) != strTabSize) return;
                    long dataBase = 16L + entBuf.Length + strTabSize;

                    for (int i = 0; i < numFiles; i++)
                    {
                        long relOff = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(entBuf.AsSpan(i * 24, 8));
                        long sz = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(entBuf.AsSpan(i * 24 + 8, 8));
                        int sOff = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(entBuf.AsSpan(i * 24 + 16, 4));
                        int sEnd = sOff;
                        while (sEnd < strTabSize && strTab[sEnd] != 0) sEnd++;
                        string name = System.Text.Encoding.UTF8.GetString(strTab, sOff, sEnd - sOff);
                        pfsEntries.Add((name, dataBase + relOff, sz));
                    }

                    // Первый проход: проверяем, нужно ли выравнивать Program NCA (< 0x4000 -> 0x4000) или исправлять версию в CNMT
                    var progReplacements = new Dictionary<string, (byte[] NewHash, long NewSize, string NewName, string TempPath)>(StringComparer.OrdinalIgnoreCase);
                    var entryOverrides = new Dictionary<int, (string NewName, string TempPath, long NewSize)>();

                    for (int idx = 0; idx < pfsEntries.Count; idx++)
                    {
                        var ent = pfsEntries[idx];
                        if (!ent.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) ||
                            ent.Name.EndsWith(".cnmt.nca", StringComparison.OrdinalIgnoreCase) ||
                            ent.Size < 0xC00)
                            continue;

                        fsIn.Seek(ent.AbsOffset, SeekOrigin.Begin);
                        byte[] rawHdr = new byte[0xC00];
                        if (fsIn.Read(rawHdr, 0, 0xC00) != 0xC00) continue;
                        byte[] decHdr = (rawHdr[0x200] == 'N' && rawHdr[0x201] == 'C' && rawHdr[0x202] == 'A' && rawHdr[0x203] == '3')
                            ? (byte[])rawHdr.Clone()
                            : NcaXtsCryptHeader0xC00(rawHdr, headerKey, decrypt: true);

                        if (decHdr[0x200] != 'N' || decHdr[0x201] != 'C' || decHdr[0x202] != 'A' || decHdr[0x203] != '3')
                            continue;

                        byte contentType = decHdr[0x205];
                        if (contentType != 0) continue; // Только Program NCA

                        uint minStartBlk = uint.MaxValue;
                        for (int s = 0; s < 4; s++)
                        {
                            uint sb = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(decHdr.AsSpan(0x240 + s * 16, 4));
                            uint eb = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(decHdr.AsSpan(0x240 + s * 16 + 4, 4));
                            if (eb > sb && sb < minStartBlk) minStartBlk = sb;
                        }

                        if (minStartBlk == uint.MaxValue || minStartBlk >= 0x20 || minStartBlk < 0x06)
                            continue;

                        uint padBlocks = 0x20 - minStartBlk;
                        long padBytes = (long)padBlocks * 0x200L;
                        long firstSecByteOff = (long)minStartBlk * 0x200L;

                        ulong oldContentSize = System.Buffers.Binary.BinaryPrimitives.ReadUInt64LittleEndian(decHdr.AsSpan(0x208, 8));
                        ulong newContentSize = oldContentSize + (ulong)padBytes;
                        System.Buffers.Binary.BinaryPrimitives.WriteUInt64LittleEndian(decHdr.AsSpan(0x208, 8), newContentSize);

                        var activeSecs = new List<(int SecIdx, long OldStart, long OldEnd, byte EncType, byte[] GenSec)>();
                        for (int s = 0; s < 4; s++)
                        {
                            uint sb = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(decHdr.AsSpan(0x240 + s * 16, 4));
                            uint eb = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(decHdr.AsSpan(0x240 + s * 16 + 4, 4));
                            if (eb > sb)
                            {
                                byte encType = decHdr[0x400 + s * 0x200 + 4];
                                byte[] gensec = new byte[8];
                                Buffer.BlockCopy(decHdr, 0x400 + s * 0x200 + 0x140, gensec, 0, 8);
                                activeSecs.Add((s, (long)sb * 0x200L, (long)eb * 0x200L, encType, gensec));
                                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(decHdr.AsSpan(0x240 + s * 16, 4), sb + padBlocks);
                                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(decHdr.AsSpan(0x240 + s * 16 + 4, 4), eb + padBlocks);
                            }
                        }
                        activeSecs.Sort((a, b) => a.OldStart.CompareTo(b.OldStart));

                        byte[] newEncHdr = NcaXtsCryptHeader0xC00(decHdr, headerKey, decrypt: false);
                        Directory.CreateDirectory(workDir);
                        string normProgPath = System.IO.Path.Combine(workDir, $"norm_prog_{idx}.nca");

                        byte[]? ctrKey = activeSecs.Any(x => x.EncType == 3) ? ResolveNcaCtrKey(decHdr, keysService.CurrentKeyset) : null;

                        using var sha = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
                        using (var fsOutProg = new FileStream(normProgPath, FileMode.Create, FileAccess.Write, FileShare.None, 4 * 1024 * 1024))
                        {
                            // Заголовок 0x000..0xC00 + оригинальные байты до firstSecByteOff (если > 0xC00) + padBytes нулей до 0x4000
                            fsOutProg.Write(newEncHdr, 0, 0xC00);
                            sha.AppendData(newEncHdr, 0, 0xC00);

                            if (firstSecByteOff > 0xC00)
                            {
                                byte[] preGap = new byte[(int)(firstSecByteOff - 0xC00)];
                                fsIn.Seek(ent.AbsOffset + 0xC00, SeekOrigin.Begin);
                                int r = fsIn.Read(preGap, 0, preGap.Length);
                                fsOutProg.Write(preGap, 0, r);
                                sha.AppendData(preGap, 0, r);
                            }

                            byte[] zeroPad = new byte[(int)padBytes];
                            fsOutProg.Write(zeroPad, 0, zeroPad.Length);
                            sha.AppendData(zeroPad, 0, zeroPad.Length);

                            byte[] copyBuf = new byte[4 * 1024 * 1024];
                            if (ctrKey == null || activeSecs.All(x => x.EncType == 1))
                            {
                                fsIn.Seek(ent.AbsOffset + firstSecByteOff, SeekOrigin.Begin);
                                long rem = ent.Size - firstSecByteOff;
                                while (rem > 0)
                                {
                                    int toRead = (int)Math.Min(copyBuf.Length, rem);
                                    int got = fsIn.Read(copyBuf, 0, toRead);
                                    if (got <= 0) break;
                                    fsOutProg.Write(copyBuf, 0, got);
                                    sha.AppendData(copyBuf, 0, got);
                                    rem -= got;
                                }
                            }
                            else
                            {
                                long curOldOff = firstSecByteOff;
                                foreach (var sec in activeSecs)
                                {
                                    if (sec.OldStart > curOldOff)
                                    {
                                        fsIn.Seek(ent.AbsOffset + curOldOff, SeekOrigin.Begin);
                                        long gapRem = sec.OldStart - curOldOff;
                                        while (gapRem > 0)
                                        {
                                            int got = fsIn.Read(copyBuf, 0, (int)Math.Min(copyBuf.Length, gapRem));
                                            if (got <= 0) break;
                                            fsOutProg.Write(copyBuf, 0, got);
                                            sha.AppendData(copyBuf, 0, got);
                                            gapRem -= got;
                                        }
                                    }
                                    fsIn.Seek(ent.AbsOffset + sec.OldStart, SeekOrigin.Begin);
                                    long secRem = sec.OldEnd - sec.OldStart;
                                    long secRel = 0;
                                    while (secRem > 0)
                                    {
                                        int got = fsIn.Read(copyBuf, 0, (int)Math.Min(copyBuf.Length, secRem));
                                        if (got <= 0) break;
                                        if (sec.EncType == 3 && ctrKey != null)
                                        {
                                            AesCtrCryptInPlace(copyBuf, 0, got, ctrKey, sec.GenSec, sec.OldStart + secRel);
                                            AesCtrCryptInPlace(copyBuf, 0, got, ctrKey, sec.GenSec, sec.OldStart + padBytes + secRel);
                                        }
                                        fsOutProg.Write(copyBuf, 0, got);
                                        sha.AppendData(copyBuf, 0, got);
                                        secRel += got;
                                        secRem -= got;
                                    }
                                    curOldOff = sec.OldEnd;
                                }
                                if (curOldOff < ent.Size)
                                {
                                    fsIn.Seek(ent.AbsOffset + curOldOff, SeekOrigin.Begin);
                                    long tailRem = ent.Size - curOldOff;
                                    while (tailRem > 0)
                                    {
                                        int got = fsIn.Read(copyBuf, 0, (int)Math.Min(copyBuf.Length, tailRem));
                                        if (got <= 0) break;
                                        fsOutProg.Write(copyBuf, 0, got);
                                        sha.AppendData(copyBuf, 0, got);
                                        tailRem -= got;
                                    }
                                }
                            }
                        }

                        byte[] newProgHash = sha.GetHashAndReset();
                        long newProgSize = new FileInfo(normProgPath).Length;
                        string newProgName = Convert.ToHexString(newProgHash, 0, 16).ToLowerInvariant() + ".nca";
                        string oldNcaId = ent.Name.Split('.')[0].ToLowerInvariant();
                        progReplacements[oldNcaId] = (newProgHash, newProgSize, newProgName, normProgPath);
                        entryOverrides[idx] = (newProgName, normProgPath, newProgSize);
                        App.Logger?.Log($"[HardPatchEngine] Normalized Program NCA {ent.Name} -> {newProgName} (Section[0] 0x{firstSecByteOff:X} -> 0x4000, +{padBytes} B)", Models.LogLevel.Info);
                    }

                    // Второй проход: обновляем .cnmt.nca (хеш/размер Program NCA и десятичную версию TitleVersion)
                    for (int idx = 0; idx < pfsEntries.Count; idx++)
                    {
                        var ent = pfsEntries[idx];
                        if (!ent.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) || ent.Size < 0x1000 || ent.Size > 1024 * 1024)
                            continue;

                        fsIn.Seek(ent.AbsOffset, SeekOrigin.Begin);
                        byte[] rawNca = new byte[(int)ent.Size];
                        if (fsIn.Read(rawNca, 0, rawNca.Length) != rawNca.Length) continue;

                        byte[] rawHdr = new byte[0xC00];
                        Buffer.BlockCopy(rawNca, 0, rawHdr, 0, 0xC00);
                        byte[] decHdr = (rawHdr[0x200] == 'N' && rawHdr[0x201] == 'C' && rawHdr[0x202] == 'A' && rawHdr[0x203] == '3')
                            ? (byte[])rawHdr.Clone()
                            : NcaXtsCryptHeader0xC00(rawHdr, headerKey, decrypt: true);

                        if (decHdr[0x200] != 'N' || decHdr[0x201] != 'C' || decHdr[0x202] != 'A' || decHdr[0x203] != '3')
                            continue;
                        if (decHdr[0x205] != 1) continue; // ContentType == 1 (Meta)

                        uint sb = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(decHdr.AsSpan(0x240, 4));
                        uint eb = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(decHdr.AsSpan(0x244, 4));
                        if (eb <= sb) continue;

                        int secStart = (int)(sb * 0x200);
                        int secLen = (int)((eb - sb) * 0x200);
                        if (secStart + secLen > rawNca.Length) continue;

                        byte encType = decHdr[0x404];
                        byte[] gensec = new byte[8];
                        Buffer.BlockCopy(decHdr, 0x540, gensec, 0, 8);
                        byte[]? ctrKey = (encType == 3) ? ResolveNcaCtrKey(decHdr, keysService.CurrentKeyset) : null;
                        if (encType == 3 && ctrKey == null) continue;

                        byte[] secPlain = new byte[secLen];
                        Buffer.BlockCopy(rawNca, secStart, secPlain, 0, secLen);
                        if (encType == 3 && ctrKey != null)
                        {
                            AesCtrCryptInPlace(secPlain, 0, secLen, ctrKey, gensec, secStart);
                        }

                        uint blkSize = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(decHdr.AsSpan(0x428, 4));
                        long htOff = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(decHdr.AsSpan(0x430, 8));
                        long htSize = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(decHdr.AsSpan(0x438, 8));
                        long pfsOff = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(decHdr.AsSpan(0x440, 8));
                        long pfsSize = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(decHdr.AsSpan(0x448, 8));

                        if (pfsOff < 0 || pfsOff + pfsSize > secLen || secPlain[pfsOff] != 'P' || secPlain[pfsOff + 1] != 'F' || secPlain[pfsOff + 2] != 'S' || secPlain[pfsOff + 3] != '0')
                            continue;

                        int mNum = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(secPlain.AsSpan((int)pfsOff + 4, 4));
                        int mStrSz = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(secPlain.AsSpan((int)pfsOff + 8, 4));
                        int mDataBase = (int)pfsOff + 16 + mNum * 24 + mStrSz;

                        bool metaChanged = false;
                        for (int m = 0; m < mNum; m++)
                        {
                            long mRelOff = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(secPlain.AsSpan((int)pfsOff + 16 + m * 24, 8));
                            long mSz = System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(secPlain.AsSpan((int)pfsOff + 16 + m * 24 + 8, 8));
                            int mSOff = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(secPlain.AsSpan((int)pfsOff + 16 + m * 24 + 16, 4));
                            int mSEnd = mSOff;
                            while (mSEnd < mStrSz && secPlain[(int)pfsOff + 16 + mNum * 24 + mSEnd] != 0) mSEnd++;
                            string mName = System.Text.Encoding.UTF8.GetString(secPlain, (int)pfsOff + 16 + mNum * 24 + mSOff, mSEnd - mSOff);
                            if (!mName.EndsWith(".cnmt", StringComparison.OrdinalIgnoreCase)) continue;

                            int cnmtPos = mDataBase + (int)mRelOff;
                            if (cnmtPos + 0x20 > secLen) continue;

                            uint curVer = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(secPlain.AsSpan(cnmtPos + 0x08, 4));
                            uint desiredVer = curVer;
                            if (expectedTitleVersion.HasValue)
                            {
                                desiredVer = expectedTitleVersion.Value;
                            }
                            else if ((curVer & 0xFFFF) != 0 && uint.TryParse(curVer.ToString("X"), out uint hexFixed) && (hexFixed % 65536 == 0))
                            {
                                desiredVer = hexFixed;
                            }

                            if (desiredVer != curVer)
                            {
                                System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(secPlain.AsSpan(cnmtPos + 0x08, 4), desiredVer);
                                metaChanged = true;
                            }

                            ushort tableOffset = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(secPlain.AsSpan(cnmtPos + 0x0E, 2));
                            ushort contentCount = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(secPlain.AsSpan(cnmtPos + 0x10, 2));
                            int recBase = cnmtPos + 0x20 + tableOffset;

                            for (int r = 0; r < contentCount; r++)
                            {
                                int rPos = recBase + r * 56;
                                if (rPos + 56 > cnmtPos + mSz) break;
                                string recNcaId = Convert.ToHexString(secPlain.AsSpan(rPos + 0x20, 16)).ToLowerInvariant();
                                byte recType = secPlain[rPos + 0x36];
                                if (progReplacements.TryGetValue(recNcaId, out var rep) || (recType == 1 && progReplacements.Count == 1 && (rep = progReplacements.Values.First()).NewHash != null))
                                {
                                    rep.NewHash.CopyTo(secPlain.AsSpan(rPos, 32));
                                    rep.NewHash.AsSpan(0, 16).CopyTo(secPlain.AsSpan(rPos + 0x20, 16));
                                    ulong sz48 = (ulong)rep.NewSize;
                                    System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(secPlain.AsSpan(rPos + 0x30, 4), (uint)(sz48 & 0xFFFFFFFFUL));
                                    System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(secPlain.AsSpan(rPos + 0x34, 2), (ushort)((sz48 >> 32) & 0xFFFFUL));
                                    metaChanged = true;
                                }
                            }
                        }

                        if (!metaChanged) continue;

                        // Пересчёт PFS0 HashTable, MasterHash и SHA-256 заголовка секции 0
                        int effBlk = blkSize > 0 ? (int)blkSize : (int)pfsSize;
                        int blkIdx = 0;
                        for (long off = 0; off < pfsSize; off += effBlk)
                        {
                            int chunkLen = (int)Math.Min(effBlk, pfsSize - off);
                            byte[] bh = System.Security.Cryptography.SHA256.HashData(secPlain.AsSpan((int)(pfsOff + off), chunkLen));
                            if (htOff + (blkIdx + 1) * 32 <= secLen)
                            {
                                bh.CopyTo(secPlain.AsSpan((int)htOff + blkIdx * 32, 32));
                            }
                            blkIdx++;
                        }

                        byte[] masterHash = System.Security.Cryptography.SHA256.HashData(secPlain.AsSpan((int)htOff, (int)htSize));
                        masterHash.CopyTo(decHdr.AsSpan(0x408, 32));

                        byte[] fshHash = System.Security.Cryptography.SHA256.HashData(decHdr.AsSpan(0x400, 0x200));
                        fshHash.CopyTo(decHdr.AsSpan(0x280, 32));

                        byte[] secEnc = (byte[])secPlain.Clone();
                        if (encType == 3 && ctrKey != null)
                        {
                            AesCtrCryptInPlace(secEnc, 0, secLen, ctrKey, gensec, secStart);
                        }

                        byte[] newMetaHdr = NcaXtsCryptHeader0xC00(decHdr, headerKey, decrypt: false);
                        byte[] newMetaNca = (byte[])rawNca.Clone();
                        Buffer.BlockCopy(newMetaHdr, 0, newMetaNca, 0, 0xC00);
                        Buffer.BlockCopy(secEnc, 0, newMetaNca, secStart, secLen);

                        byte[] newMetaSha = System.Security.Cryptography.SHA256.HashData(newMetaNca);
                        string newMetaName = Convert.ToHexString(newMetaSha, 0, 16).ToLowerInvariant() + ".cnmt.nca";
                        Directory.CreateDirectory(workDir);
                        string normMetaPath = System.IO.Path.Combine(workDir, newMetaName);
                        File.WriteAllBytes(normMetaPath, newMetaNca);
                        entryOverrides[idx] = (newMetaName, normMetaPath, newMetaNca.Length);
                        App.Logger?.Log($"[HardPatchEngine] Updated Meta NCA {ent.Name} -> {newMetaName}", Models.LogLevel.Info);
                    }

                    if (entryOverrides.Count == 0) return;

                    // Пересборка внешнего PFS0 (.nsp)
                    string rebuiltNsp = System.IO.Path.Combine(workDir, "normalized_rebuilt.nsp");
                    var finalItems = new List<(string Name, long Size, int OrigIdx, string? OverridePath)>();
                    for (int idx = 0; idx < pfsEntries.Count; idx++)
                    {
                        if (entryOverrides.TryGetValue(idx, out var ov))
                            finalItems.Add((ov.NewName, ov.NewSize, idx, ov.TempPath));
                        else
                            finalItems.Add((pfsEntries[idx].Name, pfsEntries[idx].Size, idx, null));
                    }

                    using var strMs = new MemoryStream();
                    var strOffsets = new int[finalItems.Count];
                    for (int i = 0; i < finalItems.Count; i++)
                    {
                        strOffsets[i] = (int)strMs.Position;
                        byte[] nb = System.Text.Encoding.UTF8.GetBytes(finalItems[i].Name);
                        strMs.Write(nb, 0, nb.Length);
                        strMs.WriteByte(0);
                    }
                    int rawHeaderBeforePad = 16 + finalItems.Count * 24 + (int)strMs.Position;
                    int alignedHeader = (rawHeaderBeforePad + 0x1F) & ~0x1F;
                    int padZeros = alignedHeader - rawHeaderBeforePad;
                    for (int p = 0; p < padZeros; p++) strMs.WriteByte(0);
                    byte[] newStrTab = strMs.ToArray();

                    using (var fsOutNsp = new FileStream(rebuiltNsp, FileMode.Create, FileAccess.Write, FileShare.None, 4 * 1024 * 1024))
                    {
                        byte[] outHdr = new byte[16 + finalItems.Count * 24 + newStrTab.Length];
                        outHdr[0] = (byte)'P'; outHdr[1] = (byte)'F'; outHdr[2] = (byte)'S'; outHdr[3] = (byte)'0';
                        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(outHdr.AsSpan(4, 4), finalItems.Count);
                        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(outHdr.AsSpan(8, 4), newStrTab.Length);
                        long curRel = 0;
                        for (int i = 0; i < finalItems.Count; i++)
                        {
                            System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(outHdr.AsSpan(16 + i * 24, 8), curRel);
                            System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian(outHdr.AsSpan(16 + i * 24 + 8, 8), finalItems[i].Size);
                            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(outHdr.AsSpan(16 + i * 24 + 16, 4), strOffsets[i]);
                            curRel += finalItems[i].Size;
                        }
                        Buffer.BlockCopy(newStrTab, 0, outHdr, 16 + finalItems.Count * 24, newStrTab.Length);
                        fsOutNsp.Write(outHdr, 0, outHdr.Length);

                        byte[] copyBuf = new byte[4 * 1024 * 1024];
                        foreach (var item in finalItems)
                        {
                            if (item.OverridePath != null)
                            {
                                using var ovFs = new FileStream(item.OverridePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4 * 1024 * 1024);
                                ovFs.CopyTo(fsOutNsp, 4 * 1024 * 1024);
                            }
                            else
                            {
                                var orig = pfsEntries[item.OrigIdx];
                                fsIn.Seek(orig.AbsOffset, SeekOrigin.Begin);
                                long rem = orig.Size;
                                while (rem > 0)
                                {
                                    int got = fsIn.Read(copyBuf, 0, (int)Math.Min(copyBuf.Length, rem));
                                    if (got <= 0) break;
                                    fsOutNsp.Write(copyBuf, 0, got);
                                    rem -= got;
                                }
                            }
                        }
                    }
                }

                string rebuiltCheck = System.IO.Path.Combine(workDir, "normalized_rebuilt.nsp");
                if (File.Exists(rebuiltCheck) && new FileInfo(rebuiltCheck).Length > 0x4000)
                {
                    File.Move(rebuiltCheck, nspPath, overwrite: true);
                    if (task != null)
                    {
                        App.RunOnUI(() => task.LogDetails += "\n✅ [NCA Normalizer] Секции Program NCA выровнены по 0x4000 (+26 блоков), CNMT синхронизирован.");
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger?.Log($"[HardPatchEngine] Ошибка нормализации секций 0x4000: {ex.Message}", Models.LogLevel.Warning);
            }
            finally
            {
                try { if (Directory.Exists(workDir)) Directory.Delete(workDir, recursive: true); } catch { }
            }
        }

        private static void SynchronizeCrossRegionLocalization(string targetRomFs, List<string> romfsMods, List<string> standaloneModFiles, Models.ProcessingTask task)
        {
            if (!Directory.Exists(targetRomFs)) return;
            try
            {
                bool syncedAny = false;

                var injectedRelativeFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var injectedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var rMod in romfsMods)
                {
                    if (!Directory.Exists(rMod)) continue;
                    foreach (var f in Directory.EnumerateFiles(rMod, "*", SearchOption.AllDirectories))
                    {
                        string rel = System.IO.Path.GetRelativePath(rMod, f);
                        injectedRelativeFiles.Add(rel);
                        injectedFileNames.Add(System.IO.Path.GetFileName(f));
                    }
                }
                foreach (var sMod in standaloneModFiles)
                {
                    if (File.Exists(sMod))
                    {
                        injectedFileNames.Add(System.IO.Path.GetFileName(sMod));
                    }
                }

                var malsDirs = Directory.GetDirectories(targetRomFs, "*", SearchOption.AllDirectories)
                    .Where(d => System.IO.Path.GetFileName(d).Equals("Mals", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var malsDir in malsDirs)
                {
                    var sarcFiles = Directory.GetFiles(malsDir, "*.sarc.zs", SearchOption.TopDirectoryOnly);
                    var moddedMalsFiles = sarcFiles
                        .Where(f => injectedFileNames.Contains(System.IO.Path.GetFileName(f)))
                        .ToList();

                    if (moddedMalsFiles.Count > 0)
                    {
                        string moddedMalsFile = moddedMalsFiles.First();
                        string moddedFn = System.IO.Path.GetFileName(moddedMalsFile);

                        foreach (var targetFile in sarcFiles)
                        {
                            string tFn = System.IO.Path.GetFileName(targetFile);
                            if (tFn.StartsWith("RUru.Product.", StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(targetFile, moddedMalsFile, StringComparison.OrdinalIgnoreCase))
                            {
                                try { File.Delete(targetFile); } catch { }
                                continue;
                            }
                            if (tFn.StartsWith("EUen.Product.", StringComparison.OrdinalIgnoreCase) ||
                                tFn.StartsWith("USen.Product.", StringComparison.OrdinalIgnoreCase) ||
                                tFn.StartsWith("EUru.Product.", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!string.Equals(targetFile, moddedMalsFile, StringComparison.OrdinalIgnoreCase))
                                {
                                    File.Copy(moddedMalsFile, targetFile, overwrite: true);
                                    syncedAny = true;
                                }
                            }
                        }

                        var mMatch = Regex.Match(moddedFn, @"^[A-Za-z]{2,4}\.(Product\.[^.]+\.sarc\.zs)$", RegexOptions.IgnoreCase);
                        if (mMatch.Success)
                        {
                            string rest = mMatch.Groups[1].Value;
                            foreach (var regionPrefix in new[] { "EUen.", "USen.", "EUru." })
                            {
                                string regTarget = System.IO.Path.Combine(malsDir, regionPrefix + rest);
                                if (!string.Equals(regTarget, moddedMalsFile, StringComparison.OrdinalIgnoreCase))
                                {
                                    File.Copy(moddedMalsFile, regTarget, overwrite: true);
                                    syncedAny = true;
                                }
                            }
                        }
                    }
                }

                string[][] regionalPairs = new[]
                {
                    new[] { "EU_English", "US_English" },
                    new[] { "EuEnglish", "UsEnglish" },
                    new[] { "en-GB", "en-US" },
                    new[] { "EUen", "USen" }
                };

                foreach (var relPath in injectedRelativeFiles)
                {
                    foreach (var pair in regionalPairs)
                    {
                        string rA = pair[0];
                        string rB = pair[1];

                        string? srcRegion = null;
                        string? dstRegion = null;

                        if (relPath.Contains(rA, StringComparison.OrdinalIgnoreCase))
                        {
                            srcRegion = rA;
                            dstRegion = rB;
                        }
                        else if (relPath.Contains(rB, StringComparison.OrdinalIgnoreCase))
                        {
                            srcRegion = rB;
                            dstRegion = rA;
                        }

                        if (srcRegion != null && dstRegion != null)
                        {
                            string counterpartRel = Regex.Replace(relPath, Regex.Escape(srcRegion), dstRegion, RegexOptions.IgnoreCase);
                            if (!injectedRelativeFiles.Contains(counterpartRel))
                            {
                                string srcFull = System.IO.Path.Combine(targetRomFs, relPath);
                                string dstFull = System.IO.Path.Combine(targetRomFs, counterpartRel);

                                if (File.Exists(srcFull))
                                {
                                    string? dstDir = System.IO.Path.GetDirectoryName(dstFull);
                                    if (dstDir != null && (Directory.Exists(dstDir) || File.Exists(dstFull)))
                                    {
                                        Directory.CreateDirectory(dstDir);
                                        File.Copy(srcFull, dstFull, overwrite: true);
                                        syncedAny = true;
                                    }
                                }
                            }
                        }
                    }
                }

                if (syncedAny)
                {
                    App.RunOnUI(() => task.LogDetails += "\n🌐 [Localization Sync] Выполнена кросс-региональная синхронизация локализации (EU ↔ US)...");
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[Localization Sync] Предупреждение: {ex.Message}", Models.LogLevel.Warning);
            }
        }
    }

    public class ProgressLogBuffer : IDisposable
    {
        private readonly Models.ProcessingTask _task;
        private readonly List<string> _buffer = new List<string>();
        private readonly System.Timers.Timer _timer;
        private readonly object _lock = new object();
        private string? _lastProgressLine = null;

        public ProgressLogBuffer(Models.ProcessingTask task)
        {
            _task = task;
            _timer = new System.Timers.Timer(150); // Update UI every 150 ms
            _timer.Elapsed += (s, e) => Flush();
            _timer.AutoReset = true;
            _timer.Start();
        }

        public void AppendLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return;

            lock (_lock)
            {
                if (IsProgressLine(line))
                {
                    _lastProgressLine = line;
                }
                else
                {
                    _buffer.Add(line);
                }
            }
        }

        private static bool IsProgressLine(string line)
        {
            string trimmed = line.Trim();
            return trimmed.StartsWith("[") && (trimmed.Contains("%") || trimmed.Contains("/") || trimmed.Contains("]"));
        }

        public void Flush()
        {
            List<string> linesToAppend;
            string? progressLine;

            lock (_lock)
            {
                if (_buffer.Count == 0 && _lastProgressLine == null) return;
                
                linesToAppend = new List<string>(_buffer);
                _buffer.Clear();
                
                progressLine = _lastProgressLine;
                _lastProgressLine = null;
            }

            App.RunOnUI(() =>
            {
                string current = _task.LogDetails ?? "";
                var sb = new System.Text.StringBuilder(current);

                // If the last line of current log is a progress line, remove it
                int lastNewLine = current.LastIndexOf('\n');
                string lastLine = lastNewLine >= 0 ? current.Substring(lastNewLine + 1) : current;
                if (IsProgressLine(lastLine))
                {
                    sb.Length = Math.Max(0, lastNewLine);
                }

                // Append new non-progress lines
                foreach (var line in linesToAppend)
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != '\n') sb.Append('\n');
                    sb.Append("    ").Append(line);
                }

                // Append or update the progress line
                if (progressLine != null)
                {
                    if (sb.Length > 0 && sb[sb.Length - 1] != '\n') sb.Append('\n');
                    sb.Append("    ").Append(progressLine);
                }

                _task.LogDetails = LimitLines(sb.ToString(), 250);
            });
        }

        private static string LimitLines(string text, int maxLines)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            int lineCount = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n') lineCount++;
            }
            
            if (lineCount <= maxLines) return text;
            
            int linesToSkip = lineCount - maxLines;
            int currentSkip = 0;
            int cutIndex = -1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    currentSkip++;
                    if (currentSkip == linesToSkip)
                    {
                        cutIndex = i + 1;
                        break;
                    }
                }
            }
            
            if (cutIndex > 0 && cutIndex < text.Length)
            {
                return "[...] " + text.Substring(cutIndex);
            }
            return text;
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
            Flush();
        }
            }
}
