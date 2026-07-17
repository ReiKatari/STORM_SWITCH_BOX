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

                // ══════════════════════════════════════════════════════════════════
                // СОРТИРОВКА ДЛЯ ЭМУЛЯТОРОВ: Base игра всегда должна быть первой!
                // Иначе эмулятор (Yuzu/Ryujinx/STORM EDEN) не найдет main executable
                // ══════════════════════════════════════════════════════════════════
                var sortedList = new List<string>();
                string? mainApp = null;
                string? patchApp = null;
                var dlcs = new List<string>();

                foreach (var f in finalInputFilesList)
                {
                    if (Directory.Exists(f)) continue; // Пропускаем папки

                    bool isBase = false;
                    bool isPatch = false;

                    try
                    {
                        var info = App.SwitchFormat.ParseNsp(f);
                        if (info.ContentType == "Application") isBase = true;
                        else if (info.ContentType == "Patch") isPatch = true;
                    } 
                    catch { }

                    if (!isBase && !isPatch)
                    {
                        // Fallback по имени
                        if (f.Contains("[v0]") || f.EndsWith("v0.nsp", StringComparison.OrdinalIgnoreCase) || f.Contains("patched_base")) isBase = true;
                        else if (f.Contains("v") && !f.Contains("v0")) isPatch = true;
                    }

                    if (isBase && mainApp == null) mainApp = f;
                    else if (isPatch && patchApp == null) patchApp = f;
                    else dlcs.Add(f);
                }

                if (!string.IsNullOrEmpty(mainApp)) sortedList.Add(mainApp);
                if (!string.IsNullOrEmpty(patchApp)) sortedList.Add(patchApp);
                sortedList.AddRange(dlcs);

                if (sortedList.Count == 0) sortedList = finalInputFilesList.Where(f => !Directory.Exists(f)).ToList();

                string fmt = isTargetXci ? "xci" : "nsp";
                string outFolder = System.IO.Path.Combine(tempDecompDir, "nscb_out");
                Directory.CreateDirectory(outFolder);

                string mlistFile = System.IO.Path.Combine(tempDecompDir, "mlist.txt");
                System.IO.File.WriteAllLines(mlistFile, sortedList);

                if (!isTargetXci)
                {
                    App.RunOnUI(() =>
                    {
                        task.LogDetails += "\n📦 [LibHac] Быстрая сборка Multi-NSP (PFS0)...";
                    });

                    var pfsBuilder = new PartitionFileSystemBuilder();
                    var mergedEntries = new Dictionary<string, LibHac.Fs.Fsa.IFile>(StringComparer.OrdinalIgnoreCase);
                    var openedFs = new List<PartitionFileSystem>();
                    var openedStreams = new List<FileStream>();
                    var openedFiles = new List<LibHac.Fs.Fsa.IFile>();

                    try
                    {
                        foreach (string nspPath in sortedList)
                        {
                            if (!System.IO.File.Exists(nspPath)) continue;
                            
                            var stream = new FileStream(nspPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                            openedStreams.Add(stream);
                            var fs = new PartitionFileSystem(stream.AsStorage());
                            openedFs.Add(fs);
                            
                            foreach (var entry in fs.EnumerateEntries())
                            {
                                if (entry.Type == DirectoryEntryType.Directory) continue;
                                string name = entry.Name;
                                
                                // Пропускаем дубликаты и невалидные метаданные
                                if (mergedEntries.ContainsKey(name) || !IsValidNspEntry(name)) continue;
                                
                                var file = OpenFileSafe(fs, entry.FullPath);
                                
                                openedFiles.Add(file);
                                mergedEntries[name] = file;
                                
                                pfsBuilder.AddFile(name, new StorageFile(new StormSwitchBox.Services.SafeStorageWrapper(file.AsStorage()), LibHac.Fs.OpenMode.Read));
                            }
                        }

                        string outputNspPath = System.IO.Path.Combine(outFolder, $"multi_out_{Guid.NewGuid().ToString("N").Substring(0, 8)}.nsp");

                        using (var builtPfs = pfsBuilder.Build(PartitionFileSystemType.Standard))
                        {
                            builtPfs.GetSize(out long totalPfsSize).ThrowIfFailure();
                            
                            using var destStream = new FileStream(outputNspPath, FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024 * 1024);
                            long remaining = totalPfsSize;
                            long offset = 0;
                            byte[] buffer = new byte[8 * 1024 * 1024]; // 8MB буфер
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
                    }
                    finally
                    {
                        foreach (var f in openedFiles) { try { f.Dispose(); } catch { } }
                        foreach (var s in openedStreams) { try { s.Dispose(); } catch { } }
                    }
                }
                else
                {
                    string args = $"-t {fmt} -o \"{outFolder}\" -tfile \"{mlistFile}\" -dmul \"calculate\"";
                    
                    // Log the file list being passed to squirrel for diagnostics
                    App.Logger.Log($"[squirrel] args: {args}", Models.LogLevel.Info);
                    try
                    {
                        var mlistContents = System.IO.File.ReadAllLines(mlistFile);
                        for (int i = 0; i < mlistContents.Length; i++)
                        {
                            var mf = mlistContents[i];
                            long mfSize = System.IO.File.Exists(mf) ? new System.IO.FileInfo(mf).Length : -1;
                            App.Logger.Log($"[squirrel] mlist[{i}]: {mf} ({mfSize} bytes)", Models.LogLevel.Info);
                        }
                    }
                    catch { }

                    var psi = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = $"/c chcp 65001 >nul & \"{squirrelExe}\" {args}",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    psi.EnvironmentVariables["USERPROFILE"] = isolatedUserProfile;
                    psi.EnvironmentVariables["LOCALAPPDATA"] = isolatedLocalAppData;

                    using var proc = System.Diagnostics.Process.Start(psi);
                    if (proc == null) throw new Exception("Не удалось запустить squirrel.exe");

                    await proc.WaitForExitAsync(cancellationToken);

                    App.Logger.Log($"[squirrel] exit code: {proc.ExitCode}", Models.LogLevel.Info);

                    if (proc.ExitCode != 0)
                    {
                        throw new Exception($"NSC_Builder squirrel failed with exit code {proc.ExitCode}.");
                    }
                }

                // Search for the actual content file (.nsp/.xci), skipping metadata like .cnmt.xml
                string[] contentExtensions = new[] { ".nsp", ".xci", ".nsz", ".xcz" };
                string? generatedFile = Directory.GetFiles(outFolder)
                    .Where(f => contentExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(f => new System.IO.FileInfo(f).Length)
                    .FirstOrDefault();

                // If no content file found, list everything in the output folder for diagnostics
                if (string.IsNullOrEmpty(generatedFile))
                {
                    var allFiles = Directory.GetFiles(outFolder);
                    string listing = allFiles.Length == 0
                        ? "(empty)"
                        : string.Join("\n", allFiles.Select(f => $"  {System.IO.Path.GetFileName(f)} ({new System.IO.FileInfo(f).Length} bytes)"));
                    throw new Exception($"NSC_Builder squirrel didn't produce any .nsp/.xci files.\nOutput folder contents:\n{listing}");
                }
                
                var fileInfo = new System.IO.FileInfo(generatedFile);
                if (fileInfo.Length < 100 * 1024)
                {
                    throw new Exception($"NSC_Builder output file '{fileInfo.Name}' is suspiciously small ({fileInfo.Length} bytes). Process failed silently. This usually means NSC_Builder rejected the patched base due to missing TitleID/v0 tags or invalid signatures.");
                }

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
    }
}
