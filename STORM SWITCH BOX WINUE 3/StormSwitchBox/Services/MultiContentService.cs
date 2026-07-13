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
                tempDecompDir = System.IO.Path.Combine(string.IsNullOrEmpty(targetDir) ? System.IO.Path.GetTempPath() : targetDir, "StormDecomp_" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(tempDecompDir);

                var decompTasks = inputFiles.Select(async f =>
                {
                    if (f.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase))
                    {
                        string? decompResult = await App.NszCompression.DecompressNszAsync(task, f, tempDecompDir, cancellationToken);
                        
                        if (!string.IsNullOrEmpty(decompResult) && File.Exists(decompResult))
                        {
                            finalInputFiles.Add(CreateHardLinkWithTags(decompResult, tempDecompDir));
                        }
                        else
                        {
                            throw new Exception($"Нативная декомпрессия файла {System.IO.Path.GetFileName(f)} завершилась с ошибкой.");
                        }
                    }
                    else
                    {
                        finalInputFiles.Add(CreateHardLinkWithTags(f, tempDecompDir));
                    }
                });

                await Task.WhenAll(decompTasks);
                var finalInputFilesList = finalInputFiles.ToList();

                string listFile = System.IO.Path.Combine(tempDecompDir, $"list_conv_{Guid.NewGuid().ToString("N").Substring(0, 8)}.txt");
                System.IO.File.WriteAllLines(listFile, finalInputFilesList, new System.Text.UTF8Encoding(false));

                // Патчинг прошивки (пересборка)
                if (patchFirmware)
                {
                    App.RunOnUI(() =>
                    {
                        task.LogDetails += "\n🔵 [HardPatch] Поиск Base и Update...";
                    });
                    
                    string? baseFile = null;
                    string? updateFile = null;
                    
                    foreach (var f in finalInputFilesList)
                    {
                        try 
                        {
                            var info = App.SwitchFormat.ParseNsp(f);
                            if (info.ContentType == "Application") baseFile = f;
                            else if (info.ContentType == "Patch") updateFile = f;
                        }
                        catch { }
                    }
                    
                    if (string.IsNullOrEmpty(baseFile)) baseFile = finalInputFilesList.FirstOrDefault(f => f.Contains("[v0]") || f.Contains("v0")) ?? finalInputFilesList.FirstOrDefault(f => !f.Contains("v")) ?? "";
                    if (string.IsNullOrEmpty(updateFile)) updateFile = finalInputFilesList.FirstOrDefault(f => f != baseFile && (f.Contains("v") && !f.Contains("v0"))) ?? finalInputFilesList.FirstOrDefault(f => f != baseFile) ?? "";
                    
                    if (!string.IsNullOrEmpty(baseFile) && !string.IsNullOrEmpty(updateFile))
                    {
                        App.RunOnUI(() => task.LogDetails += "\n🔵 [HardPatch] Физическая пересборка...");
                        string tempHardPatchedNsp = System.IO.Path.Combine(tempDecompDir, "patched_base_temp.nsp");
                        
                        var hpInput = new List<string> { baseFile, updateFile };
                        
                        // Add mod directories (romfs/exefs) to be processed
                        var modDirs = finalInputFilesList.Where(d => Directory.Exists(d)).ToList();
                        hpInput.AddRange(modDirs);

                        await App.HardPatch.PatchUpdateAsync(task, hpInput, tempHardPatchedNsp, cancellationToken, isMultiContent: true);
                        
                        if (System.IO.File.Exists(tempHardPatchedNsp))
                        {
                            finalInputFilesList.Remove(baseFile);
                            finalInputFilesList.Remove(updateFile);
                            foreach (var mod in modDirs)
                            {
                                finalInputFilesList.Remove(mod);
                            }
                            finalInputFilesList.Add(tempHardPatchedNsp);
                            App.RunOnUI(() => task.LogDetails += "\n🔵 [HardPatch] Успешно завершено.");
                        }
                        else 
                        {
                            throw new Exception("Не удалось создать пересобранный файл базы.");
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
                string squirrelExe = System.IO.Path.Combine(toolsDir, "nscb", "ztools", "squirrel.exe");

                if (!System.IO.File.Exists(squirrelExe))
                    throw new Exception($"NSC_Builder (squirrel.exe) не найден по пути: {squirrelExe}");

                string isolatedUserProfile = System.IO.Path.Combine(toolsDir, "keys");
                string isolatedLocalAppData = System.IO.Path.Combine(toolsDir, "cache");

                string userProfileSwitch = System.IO.Path.Combine(isolatedUserProfile, ".switch");
                string userProfileKeys = System.IO.Path.Combine(userProfileSwitch, "prod.keys");
                string squirrelKeys = System.IO.Path.Combine(toolsDir, "nscb", "ztools", "keys.txt");

                lock (typeof(HardPatchEngine)) // Using type lock for safety as _keysLock is private
                {
                    try
                    {
                        if (!Directory.Exists(userProfileSwitch)) Directory.CreateDirectory(userProfileSwitch);
                        if (!Directory.Exists(isolatedLocalAppData)) Directory.CreateDirectory(isolatedLocalAppData);

                        if (!string.IsNullOrEmpty(App.Settings.Current.KeysPath) && System.IO.File.Exists(App.Settings.Current.KeysPath))
                        {
                            App.SwitchFormat.CleanKeysFile(App.Settings.Current.KeysPath);
                            System.IO.File.Copy(App.Settings.Current.KeysPath, userProfileKeys, true);
                            System.IO.File.Copy(App.Settings.Current.KeysPath, squirrelKeys, true);
                        }
                    }
                    catch { }
                }

                string mlistFile = System.IO.Path.Combine(tempDecompDir, "mlist.txt");
                System.IO.File.WriteAllLines(mlistFile, finalInputFilesList);

                string outFolder = System.IO.Path.Combine(tempDecompDir, "nscb_out");
                Directory.CreateDirectory(outFolder);

                string fmt = isTargetXci ? "xci" : "nsp";
                string args = $"-t {fmt} -o \"{outFolder}\" -tfile \"{mlistFile}\" -dmul \"calculate\"";
                
                App.Logger.Log($"[squirrel] {args}", Models.LogLevel.Info);

                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = squirrelExe,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };
                psi.EnvironmentVariables["USERPROFILE"] = isolatedUserProfile;
                psi.EnvironmentVariables["LOCALAPPDATA"] = isolatedLocalAppData;
                psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
                psi.EnvironmentVariables["PYTHONUTF8"] = "1";

                using var proc = System.Diagnostics.Process.Start(psi);
                if (proc == null) throw new Exception("Не удалось запустить squirrel.exe");

                string stdErr = await proc.StandardError.ReadToEndAsync();
                await proc.WaitForExitAsync(cancellationToken);

                if (proc.ExitCode != 0)
                    throw new Exception($"NSC_Builder squirrel failed:\n{stdErr}");

                string generatedFile = Directory.GetFiles(outFolder).FirstOrDefault();
                if (string.IsNullOrEmpty(generatedFile))
                    throw new Exception("NSC_Builder squirrel didn't produce any output files. Check keys and inputs.");

                if (System.IO.File.Exists(actualIntermediatePath)) System.IO.File.Delete(actualIntermediatePath);
                System.IO.File.Move(generatedFile, actualIntermediatePath);

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

        private string CreateHardLinkWithTags(string sourcePath, string tempDir)
        {
            // Поскольку мы перешли на нативную сборку, нам больше не нужно
            // складывать все файлы в одну папку (HardLink) для squirrel.exe.
            // Мы можем просто возвращать исходный путь.
            return sourcePath;
        }



        private static IFile OpenFileSafe(IFileSystem fsToOpen, string pth)
        {
            using var fRef = new UniqueRef<IFile>();
            using var path = new LibHac.Fs.Path();
            path.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(pth))).ThrowIfFailure();
            fsToOpen.OpenFile(ref fRef.Ref, in path, OpenMode.Read).ThrowIfFailure();
            return fRef.Release();
        }
    }
}
