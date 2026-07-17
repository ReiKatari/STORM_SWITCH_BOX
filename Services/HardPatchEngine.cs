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

        public async Task PatchUpdateAsync(Models.ProcessingTask task, List<string> inputFiles, string outPath, CancellationToken cancellationToken, bool isMultiContent = false)
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
                
                string isolatedUserProfile = System.IO.Path.Combine(toolsDir, "keys");
                string isolatedLocalAppData = System.IO.Path.Combine(toolsDir, "cache");
                
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

                string baseFile = string.Empty;
                string updateFile = string.Empty;

                App.RunOnUI(() => task.LogDetails += $"\nАнализ исходных файлов...");
                
                foreach (var file in inputFiles)
                {
                    if (System.IO.Directory.Exists(file)) continue;
                    var info = App.SwitchFormat.ParseNsp(file);
                    if (info.ContentType == "Application") baseFile = file;
                    else if (info.ContentType == "Patch") updateFile = file;
                }

                if (string.IsNullOrEmpty(baseFile)) baseFile = inputFiles.FirstOrDefault(f => !System.IO.Directory.Exists(f) && (f.Contains("[v0]") || f.Contains("v0"))) ?? inputFiles.FirstOrDefault(f => !System.IO.Directory.Exists(f)) ?? "";
                if (string.IsNullOrEmpty(updateFile)) updateFile = inputFiles.FirstOrDefault(f => !System.IO.Directory.Exists(f) && f != baseFile && (f.Contains("v") && !f.Contains("v0"))) ?? inputFiles.FirstOrDefault(f => !System.IO.Directory.Exists(f) && f != baseFile) ?? "";

                App.RunOnUI(() => task.LogDetails += $"\nБаза: {System.IO.Path.GetFileName(baseFile)}\nПатч: {System.IO.Path.GetFileName(updateFile)}");

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

                string yanuCliPath = FindYanuCli();
                string keysPath = App.Settings.Current.KeysPath;
                if (string.IsNullOrEmpty(keysPath) || !File.Exists(keysPath))
                    keysPath = userProfileKeys;

                string yanuOutDir = System.IO.Path.Combine(tempDir, "yanu_output");
                Directory.CreateDirectory(yanuOutDir);

                // Check if we need to apply mods for Multi-content
                bool applyMods = isMultiContent;
                
                var keepLangs = App.Settings.Current.KeepLanguages ?? new List<string> { "ru", "ru-RU", "en-US", "en-GB", "en" };
                string keepLangsStr = string.Join(",", keepLangs);
                string keepLangsArg = string.IsNullOrEmpty(keepLangsStr) ? "" : $"--keep-langs \"{keepLangsStr}\"";

                string? romfsMod = inputFiles.FirstOrDefault(d => System.IO.Directory.Exists(d) && System.IO.Path.GetFileName(d).Equals("romfs", StringComparison.OrdinalIgnoreCase));
                string? exefsMod = inputFiles.FirstOrDefault(d => System.IO.Directory.Exists(d) && System.IO.Path.GetFileName(d).Equals("exefs", StringComparison.OrdinalIgnoreCase));
                
                string titleVersionArg = "";
                if (!string.IsNullOrEmpty(updateFile))
                {
                    try {
                        var uInfo = App.SwitchFormat.ParseNsp(updateFile);
                        if (!string.IsNullOrEmpty(uInfo.Version) && uint.TryParse(uInfo.Version, out uint uv)) {
                            titleVersionArg = $"--titleversion {uv:X8}";
                        }
                    } catch { }
                }
                
                if (applyMods && (romfsMod != null || exefsMod != null))
                {
                    App.RunOnUI(() => task.LogDetails += $"\n[1/3] Распаковка файлов для применения модов (yanu-cli unpack)...");
                    
                    string tempUnpack = System.IO.Path.Combine(tempDir, "unpack_modded");
                    Directory.CreateDirectory(tempUnpack);
                    
                    string unpackArgs = $"unpack --base \"{baseFile}\"";
                    if (!string.IsNullOrEmpty(updateFile)) unpackArgs += $" --update \"{updateFile}\"";
                    unpackArgs += $" -o \"{tempUnpack}\"";
                    
                    var unpackPsi = new ProcessStartInfo
                    {
                        FileName = yanuCliPath,
                        Arguments = unpackArgs,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8,
                        StandardErrorEncoding = System.Text.Encoding.UTF8
                    };
                    unpackPsi.EnvironmentVariables["USERPROFILE"] = isolatedUserProfile;
                    unpackPsi.EnvironmentVariables["LOCALAPPDATA"] = isolatedLocalAppData;
                    using var unpackProc = Process.Start(unpackPsi);
                    if (unpackProc == null) throw new Exception("Не удалось запустить yanu-cli unpack");
                    
                    var unpackStderr = new System.Text.StringBuilder();
                    using (var logBuffer = new ProgressLogBuffer(task))
                    {
                        unpackProc.OutputDataReceived += (s, e) => {
                            if (e.Data != null) logBuffer.AppendLine(e.Data);
                        };
                        unpackProc.ErrorDataReceived += (s, e) => {
                            if (e.Data != null) unpackStderr.AppendLine(e.Data);
                        };
                        unpackProc.BeginOutputReadLine();
                        unpackProc.BeginErrorReadLine();
                        await unpackProc.WaitForExitAsync(cancellationToken);
                    }
                    if (unpackProc.ExitCode != 0) throw new Exception($"Ошибка yanu-cli unpack:\n{unpackStderr}");
                    
                    App.RunOnUI(() => task.LogDetails += $"\n[2/3] Инъекция модов (romfs/exefs)...");
                    
                    string targetRomFs = System.IO.Path.Combine(tempUnpack, "romfs");
                    string targetExeFs = System.IO.Path.Combine(tempUnpack, "exefs");
                    
                    if (!string.IsNullOrEmpty(romfsMod)) CopyDirectoryContent(romfsMod, targetRomFs);
                    if (!string.IsNullOrEmpty(exefsMod)) CopyDirectoryContent(exefsMod, targetExeFs);
                    
                    App.RunOnUI(() => task.LogDetails += $"\n[3/3] Упаковка (yanu-cli pack)...");
                    
                    string controlNca = "";
                    foreach (var ncaFile in Directory.GetFiles(tempUnpack, "*.nca", SearchOption.AllDirectories))
                    {
                        try 
                        {
                            using var fs = new FileStream(ncaFile, FileMode.Open, FileAccess.Read, FileShare.Read);
                            var nca = new LibHac.Tools.FsSystem.NcaUtils.Nca(_keysService.CurrentKeyset, fs.AsStorage());
                            if (nca.Header.ContentType == LibHac.Tools.FsSystem.NcaUtils.NcaContentType.Control) {
                                controlNca = ncaFile;
                                break;
                            }
                        } catch { }
                    }
                    if (string.IsNullOrEmpty(controlNca)) throw new Exception("Не удалось найти control.nca после распаковки для сборки модов.");

                    if (!Directory.Exists(targetRomFs)) Directory.CreateDirectory(targetRomFs);
                    if (!Directory.Exists(targetExeFs)) Directory.CreateDirectory(targetExeFs);

                    string packArgs = $"pack --titleid {titleId} --controlnca \"{controlNca}\" --romfsdir \"{targetRomFs}\" --exefsdir \"{targetExeFs}\" -o \"{yanuOutDir}\" {keepLangsArg} {titleVersionArg}".TrimEnd();
                    var packPsi = new ProcessStartInfo
                    {
                        FileName = yanuCliPath,
                        Arguments = packArgs,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8,
                        StandardErrorEncoding = System.Text.Encoding.UTF8
                    };
                    packPsi.EnvironmentVariables["USERPROFILE"] = isolatedUserProfile;
                    packPsi.EnvironmentVariables["LOCALAPPDATA"] = isolatedLocalAppData;
                    using var packProc = Process.Start(packPsi);
                    if (packProc == null) throw new Exception("Не удалось запустить yanu-cli pack");
                    
                    var packStderr = new System.Text.StringBuilder();
                    using (var logBuffer = new ProgressLogBuffer(task))
                    {
                        packProc.OutputDataReceived += (s, e) => {
                            if (e.Data != null) logBuffer.AppendLine(e.Data);
                        };
                        packProc.ErrorDataReceived += (s, e) => {
                            if (e.Data != null) packStderr.AppendLine(e.Data);
                        };
                        packProc.BeginOutputReadLine();
                        packProc.BeginErrorReadLine();
                        await packProc.WaitForExitAsync(cancellationToken);
                    }
                    if (packProc.ExitCode != 0) throw new Exception($"Ошибка yanu-cli pack:\n{packStderr}");
                }
                else
                {
                    // ═══════════════════════════════════════════════════════════════
                    // ПЕРЕСБОРКА: Стратегия 1 → yanu-cli update (один шаг)
                    //              Стратегия 2 → unpack + PFS0 combine (fallback)
                    // yanu-cli unpack НЕ создаёт romfs/exefs для BKTR-обновлений,
                    // поэтому pack без них невозможен. Используем PFS0-сборку.
                    // ═══════════════════════════════════════════════════════════════
                    
                    bool yanuUpdateSuccess = false;
                    
                    // === СТРАТЕГИЯ 1: yanu-cli update (один шаг, работает для простых обновлений) ===
                    if (!string.IsNullOrEmpty(updateFile))
                    {
                        App.RunOnUI(() => task.LogDetails += $"\n[1/2] Попытка пересборки (yanu-cli update)...");
                        
                        // yanu-cli update создаёт temp в WorkingDirectory, используем чистую папку
                        string updateWorkDir = System.IO.Path.Combine(tempDir, "update_work");
                        Directory.CreateDirectory(updateWorkDir);
                        
                        string updateArgs = $"update --base \"{baseFile}\" --update \"{updateFile}\" -o \"{yanuOutDir}\"";
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
                        
                        using var updateProc = Process.Start(updatePsi);
                        if (updateProc != null)
                        {
                            var updateStderr = new System.Text.StringBuilder();
                            using (var logBuffer = new ProgressLogBuffer(task))
                            {
                                updateProc.OutputDataReceived += (s, e) => {
                                    if (e.Data != null) logBuffer.AppendLine(e.Data);
                                };
                                updateProc.ErrorDataReceived += (s, e) => {
                                    if (e.Data != null) updateStderr.AppendLine(e.Data);
                                };
                                updateProc.BeginOutputReadLine();
                                updateProc.BeginErrorReadLine();
                                await updateProc.WaitForExitAsync(cancellationToken);
                            }
                            
                            if (updateProc.ExitCode == 0)
                            {
                                // Проверяем, что NSP файл действительно создан
                                var updateNsps = Directory.GetFiles(yanuOutDir, "*.nsp");
                                if (updateNsps.Length > 0)
                                {
                                    yanuUpdateSuccess = true;
                                    App.Logger.Log($"[yanu-cli] update OK. Создан NSP.", Models.LogLevel.Info);
                                    App.RunOnUI(() => task.LogDetails += $"\n  yanu-cli update: успешно!");
                                }
                                else
                                {
                                    App.Logger.Log("[yanu-cli] update exit=0, но NSP не найден. Переключение на fallback.", Models.LogLevel.Warning);
                                }
                            }
                            else
                            {
                                App.Logger.Log($"[yanu-cli] update failed (exit={updateProc.ExitCode}): {updateStderr.ToString().Trim()}", Models.LogLevel.Warning);
                                App.RunOnUI(() => task.LogDetails += $"\n  yanu-cli update: не удалось (BKTR). Fallback...");
                            }
                        }
                        
                        // Очистка рабочей директории update
                        try { if (Directory.Exists(updateWorkDir)) Directory.Delete(updateWorkDir, true); } catch { }
                    }
                    
                    // === СТРАТЕГИЯ 2: LibHac PFS0 Merge (надёжный fallback для BKTR) ===
                    // Читаем NSP напрямую через LibHac, объединяем все записи в один PFS0
                    // Не используем yanu-cli unpack — он создаёт .cnmt.xml артефакты
                    if (!yanuUpdateSuccess)
                    {
                        if (string.IsNullOrEmpty(updateFile))
                        {
                            // Нет апдейта — просто копируем базовый файл для конвейера
                            string outputNspPath = System.IO.Path.Combine(yanuOutDir, $"{titleId}.nsp");
                            System.IO.File.Copy(baseFile, outputNspPath, true);
                            App.RunOnUI(() => task.LogDetails += $"\n[1/1] Оригинальный файл скопирован (патч не применялся).");
                        }
                        else
                        {
                            throw new Exception("Не удалось применить Hard Patch (Update). yanu-cli вернул ошибку.");
                        }
                    }

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

                try { if (!string.IsNullOrEmpty(tempDir)) Directory.Delete(tempDir, true); } catch { }

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
                try { if (!string.IsNullOrEmpty(tempDir) && Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }

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
            
            // nsz.exe -D <input> -o <output_dir> --overwrite -t 0
            string args = $"-D \"{inputFile}\" -o \"{outputDir}\" --overwrite -t 0";
            
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
