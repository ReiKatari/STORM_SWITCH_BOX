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
                    }
                    catch { }
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
                
                if (targetDrive.Equals(appDrive, StringComparison.OrdinalIgnoreCase))
                {
                    string appDirTemp = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
                    tempDir = System.IO.Path.Combine(appDirTemp, $"STORM_TMP_{Guid.NewGuid().ToString("N").Substring(0, 6)}");
                }
                else
                {
                    tempDir = System.IO.Path.Combine(targetDrive, $"STORM_TMP_{Guid.NewGuid().ToString("N").Substring(0, 6)}");
                }
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

                bool applyMods = isMultiContent;
                
                string keepLangsArg = "";
                if (App.Settings.Current.TrimXci && App.Settings.Current.KeepLanguages != null && App.Settings.Current.KeepLanguages.Count > 0)
                {
                    string keepLangsStr = string.Join(",", App.Settings.Current.KeepLanguages);
                    keepLangsArg = string.IsNullOrEmpty(keepLangsStr) ? "" : $"--keep-langs \"{keepLangsStr}\"";
                }

                string? romfsMod = inputFiles.FirstOrDefault(d => System.IO.Directory.Exists(d) && System.IO.Path.GetFileName(d).Equals("romfs", StringComparison.OrdinalIgnoreCase));
                string? exefsMod = inputFiles.FirstOrDefault(d => System.IO.Directory.Exists(d) && System.IO.Path.GetFileName(d).Equals("exefs", StringComparison.OrdinalIgnoreCase));
                string? exefsPatchesMod = inputFiles.FirstOrDefault(d => System.IO.Directory.Exists(d) && System.IO.Path.GetFileName(d).Equals("exefs_patches", StringComparison.OrdinalIgnoreCase));
                
                string titleVersionArg = "";
                if (!string.IsNullOrEmpty(updateFile))
                {
                    try {
                        var uInfo = App.SwitchFormat.ParseNsp(updateFile);
                        if (!string.IsNullOrEmpty(uInfo.Version) && uint.TryParse(uInfo.Version, out uint uv)) {
                            titleVersionArg = $"--titleversion {uv}";
                        }
                    } catch { }
                }
                else if (!string.IsNullOrEmpty(baseFile))
                {
                    try {
                        var bInfo = App.SwitchFormat.ParseNsp(baseFile);
                        if (!string.IsNullOrEmpty(bInfo.Version) && uint.TryParse(bInfo.Version, out uint bv)) {
                            titleVersionArg = $"--titleversion {bv}";
                        }
                    } catch { }
                }
                
                bool hasModsToApply = (romfsMod != null || exefsMod != null || exefsPatchesMod != null || applyMods);
                bool yanuUpdateSuccess = false;

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
                            if (updateNsps.Length > 0)
                            {
                                yanuUpdateSuccess = true;
                                App.Logger.Log($"[yanu-cli] update OK. Создан NSP.", Models.LogLevel.Info);
                                App.RunOnUI(() => task.LogDetails += $"\n  yanu-cli update: успешно!");
                            }
                            else
                            {
                                App.Logger.Log("[yanu-cli] update exit=0, но NSP не найден. Переключение на unpack/pack.", Models.LogLevel.Warning);
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
                        App.RunOnUI(() => task.LogDetails += $"\n[2/3] Инъекция модов (romfs/exefs/exefs_patches)...");
                        if (!string.IsNullOrEmpty(romfsMod)) CopyDirectoryContent(romfsMod, targetRomFs);
                        if (!string.IsNullOrEmpty(exefsMod)) CopyDirectoryContent(exefsMod, targetExeFs);
                        if (!string.IsNullOrEmpty(exefsPatchesMod)) ApplyExeFsPatches(exefsPatchesMod, targetExeFs, task);
                    }

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
                    
                    // Применяем кастомные метаданные / иконку при необходимости
                    if (task.CustomMetadata != null && File.Exists(genFile))
                    {
                        await App.ControlEditor.ApplyCustomMetadataAsync(task.CustomMetadata, genFile, task, cancellationToken);
                    }

                    string targetExt = System.IO.Path.GetExtension(outPath).ToLower();
                    if (targetExt == ".nsp")
                    {
                        File.Move(genFile, outPath);
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
                                File.Move(expectedNsz, outPath);
                            }
                            try { File.Delete(expectedXci); } catch { }
                        }
                        else if (File.Exists(expectedXci))
                        {
                            File.Move(expectedXci, outPath);
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
                            File.Move(expectedNsz, outPath);
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
                System.IO.Path.Combine(appDir, "..", "..", "..", "tools", "yanu-cli.exe"),
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
                string dest = System.IO.Path.Combine(destinationDir, System.IO.Path.GetFileName(file));
                File.Copy(file, dest, true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string dest = System.IO.Path.Combine(destinationDir, System.IO.Path.GetFileName(dir));
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
        private static void ApplyIpsPatchToFile(string ipsFilePath, string targetBinaryPath)
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

            // 1.1 Копируем файлы из всех найденных папок exefs / basedata / patchdata
            // Сначала basedata (чтобы получить base main.npdm, sdk, rtld, subsdk*), затем patchdata (чтобы перезаписать main свежей версией из патча)
            var allExeDirs = Directory.GetDirectories(tempUnpack, "*exefs*", SearchOption.AllDirectories)
                .Where(d => !d.Equals(targetExeFs, StringComparison.OrdinalIgnoreCase))
                .OrderBy(d => d.Contains("patchdata", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                .ToArray();

            foreach (var exefsDir in allExeDirs)
            {
                foreach (var file in Directory.GetFiles(exefsDir))
                {
                    string dest = System.IO.Path.Combine(targetExeFs, System.IO.Path.GetFileName(file));
                    File.Copy(file, dest, true);
                }
            }

            // 1.2 Если main.npdm все еще отсутствует в targetExeFs — гарантированно извлекаем оригинальный ExeFS и main.npdm
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

            string resolvedTitleId = maxTitleId > 0 ? maxTitleId.ToString("X16") : fallbackTitleId;

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
        private static void ExtractExeFsSingleFile(string filePath, string targetExeFs, KeysService keysService, string keysPath)
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
                                                        if (exefsEntry.Name.TrimStart('/').Equals("main.npdm", StringComparison.OrdinalIgnoreCase) || !File.Exists(destFile))
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
