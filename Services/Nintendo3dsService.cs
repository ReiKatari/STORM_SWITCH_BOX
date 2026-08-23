using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public class Nintendo3dsService
    {
        private string GetToolsDir()
        {
            string baseTools = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "3ds");
            if (Directory.Exists(baseTools)) return baseTools;
            string devTools = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "tools", "3ds");
            if (Directory.Exists(devTools)) return Path.GetFullPath(devTools);
            return baseTools;
        }

        private string GetMakeromPath() => Path.Combine(GetToolsDir(), "makerom.exe");
        private string GetCtrtoolPath() => Path.Combine(GetToolsDir(), "ctrtool.exe");
        private string Get3dstoolPath() => Path.Combine(GetToolsDir(), "3dstool.exe");
        private string GetAesKeysPath()
        {
            if (!string.IsNullOrEmpty(App.Settings.Current.KeysPath3ds) && File.Exists(App.Settings.Current.KeysPath3ds))
                return App.Settings.Current.KeysPath3ds;
            string defaultKeys = Path.Combine(GetToolsDir(), "aes_keys.txt");
            if (File.Exists(defaultKeys)) return defaultKeys;
            string userKeys = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".3ds", "aes_keys.txt");
            if (File.Exists(userKeys)) return userKeys;
            return defaultKeys;
        }

        public static bool Is3dsExtension(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return false;
            string lower = ext.ToLowerInvariant().TrimStart('.');
            return lower == "3ds" || lower == "cci" || lower == "cia" || lower == "cxi" || lower == "cfa";
        }

        public async Task<Nintendo3dsInfo> Parse3dsFileAsync(string filePath)
        {
            return await Task.Run(() => Parse3dsFile(filePath));
        }

        public Nintendo3dsInfo Parse3dsFile(string filePath)
        {
            var info = new Nintendo3dsInfo
            {
                SizeBytes = File.Exists(filePath) ? new FileInfo(filePath).Length : 0,
                FileFormat = Path.GetExtension(filePath).ToUpperInvariant().TrimStart('.')
            };

            if (!File.Exists(filePath)) return info;

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = new BinaryReader(fs);

                byte[] magic = new byte[4];
                fs.Seek(0x100, SeekOrigin.Begin);
                fs.Read(magic, 0, 4);
                string magicStr = Encoding.ASCII.GetString(magic);

                if (magicStr == "NCSD")
                {
                    // .3ds / .cci (Card Image)
                    fs.Seek(0x108, SeekOrigin.Begin);
                    ulong mediaId = reader.ReadUInt64();
                    info.TitleId = mediaId.ToString("X16");
                    info.ContentType = "Application";

                    // Read Product code from NCCH0 header (Partition 0)
                    fs.Seek(0x120, SeekOrigin.Begin);
                    uint partition0Offset = reader.ReadUInt32();
                    long ncchOffset = (long)partition0Offset * 0x200;

                    if (ncchOffset > 0 && ncchOffset < fs.Length)
                    {
                        fs.Seek(ncchOffset + 0x150, SeekOrigin.Begin);
                        byte[] prodCode = reader.ReadBytes(16);
                        info.ProductCode = Encoding.ASCII.GetString(prodCode).Trim('\0', ' ');

                        fs.Seek(ncchOffset + 0x118, SeekOrigin.Begin);
                        ushort ver = reader.ReadUInt16();
                        info.Version = $"v{ver}";
                    }
                }
                else if (magicStr == "NCCH")
                {
                    // .cxi / .cfa
                    fs.Seek(0x108, SeekOrigin.Begin);
                    ulong progId = reader.ReadUInt64();
                    info.TitleId = progId.ToString("X16");

                    fs.Seek(0x150, SeekOrigin.Begin);
                    byte[] prodCode = reader.ReadBytes(16);
                    info.ProductCode = Encoding.ASCII.GetString(prodCode).Trim('\0', ' ');

                    fs.Seek(0x118, SeekOrigin.Begin);
                    ushort ver = reader.ReadUInt16();
                    info.Version = $"v{ver}";

                    if (info.TitleId.StartsWith("0004000E", StringComparison.OrdinalIgnoreCase)) info.ContentType = "Patch";
                    else if (info.TitleId.StartsWith("0004008C", StringComparison.OrdinalIgnoreCase)) info.ContentType = "AddOnContent";
                    else info.ContentType = "Application";
                }
                else
                {
                    // Check if CIA
                    fs.Seek(0, SeekOrigin.Begin);
                    uint headerSize = reader.ReadUInt32();
                    ushort type = reader.ReadUInt16();
                    ushort version = reader.ReadUInt16();
                    uint certSize = reader.ReadUInt32();
                    uint ticketSize = reader.ReadUInt32();
                    uint tmdSize = reader.ReadUInt32();
                    uint metaSize = reader.ReadUInt32();
                    ulong contentSize = reader.ReadUInt64();

                    if (headerSize >= 0x20 && certSize > 0 && tmdSize > 0)
                    {
                        long tmdOffset = ((headerSize + 0x3F) & ~0x3F) + ((certSize + 0x3F) & ~0x3F) + ((ticketSize + 0x3F) & ~0x3F);
                        if (tmdOffset + 0x1A0 <= fs.Length)
                        {
                            fs.Seek(tmdOffset + 0x18C, SeekOrigin.Begin);
                            ulong titleId = 0;
                            for (int i = 0; i < 8; i++)
                            {
                                titleId = (titleId << 8) | reader.ReadByte();
                            }
                            info.TitleId = titleId.ToString("X16");

                            fs.Seek(tmdOffset + 0x19C, SeekOrigin.Begin);
                            ushort ver = (ushort)((reader.ReadByte() << 8) | reader.ReadByte());
                            info.Version = $"v{ver}";

                            if (info.TitleId.StartsWith("0004000E", StringComparison.OrdinalIgnoreCase)) info.ContentType = "Patch";
                            else if (info.TitleId.StartsWith("0004008C", StringComparison.OrdinalIgnoreCase)) info.ContentType = "AddOnContent";
                            else info.ContentType = "Application";
                        }
                    }
                }

                // If Title Name is still empty, format from filename
                if (string.IsNullOrEmpty(info.GameName))
                {
                    info.GameName = Path.GetFileNameWithoutExtension(filePath);
                }
            }
            catch { }

            return info;
        }

        public async Task Build3dsMultiContentAsync(
            ProcessingTask task,
            List<string> inputFiles,
            string outputFilePath,
            string targetFormat,
            CancellationToken cancellationToken)
        {
            string makerom = GetMakeromPath();
            string ctrtool = GetCtrtoolPath();
            string keysFile = GetAesKeysPath();

            if (!File.Exists(makerom))
            {
                throw new FileNotFoundException($"Утилита makerom не найдена: {makerom}");
            }

            string tempDir = Path.Combine(Path.GetTempPath(), $"Storm3DS_Multi_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            TempCleanupService.RegisterActiveTempDirectory(tempDir);

            try
            {
                App.RunOnUI(() =>
                {
                    task.IsRunning = true;
                    task.Progress = 10;
                    task.Status = "Анализ файлов 3DS...";
                    task.LogDetails += $"\n🕹️ [3DS Engine] Начало сборки Multi-Content в формат {targetFormat}...";
                });

                // Classify files
                string? baseFile = null;
                string? updateFile = null;
                var dlcFiles = new List<string>();
                var modDirs = new List<string>();

                foreach (var f in inputFiles)
                {
                    if (Directory.Exists(f))
                    {
                        modDirs.Add(f);
                        continue;
                    }

                    var meta = Parse3dsFile(f);
                    if (meta.ContentType == "Patch" || meta.TitleId.StartsWith("0004000E", StringComparison.OrdinalIgnoreCase))
                    {
                        if (updateFile == null) updateFile = f;
                    }
                    else if (meta.ContentType == "AddOnContent" || meta.TitleId.StartsWith("0004008C", StringComparison.OrdinalIgnoreCase))
                    {
                        dlcFiles.Add(f);
                    }
                    else
                    {
                        if (baseFile == null) baseFile = f;
                        else dlcFiles.Add(f);
                    }
                }

                if (baseFile == null && inputFiles.Count > 0)
                {
                    baseFile = inputFiles.FirstOrDefault(f => !Directory.Exists(f));
                }

                if (baseFile == null)
                {
                    throw new Exception("Не найден базовый файл игры (.3ds, .cci, .cia).");
                }

                App.RunOnUI(() =>
                {
                    task.Progress = 25;
                    task.Status = "Подготовка контента 3DS...";
                    task.LogDetails += $"\n  🎮 Базовая игра: {Path.GetFileName(baseFile)}";
                    if (!string.IsNullOrEmpty(updateFile)) task.LogDetails += $"\n  🔄 Обновление: {Path.GetFileName(updateFile)}";
                    if (dlcFiles.Count > 0) task.LogDetails += $"\n  📦 Дополнения: {dlcFiles.Count} шт.";
                    if (modDirs.Count > 0) task.LogDetails += $"\n  📁 Модификации: {modDirs.Count} папок";
                });

                string outExt = targetFormat.ToLowerInvariant().Replace(" (cci)", "").Trim();
                if (outExt == "3ds" || outExt == "cci") outExt = "3ds";
                else if (outExt == "cia") outExt = "cia";
                else outExt = "cxi";

                string targetOutFile = Path.ChangeExtension(outputFilePath, "." + outExt);

                // If base file has same format and no updates/mods, convert or copy directly
                if (string.IsNullOrEmpty(updateFile) && modDirs.Count == 0 && dlcFiles.Count == 0)
                {
                    await Convert3dsContainerAsync(task, baseFile, Path.GetDirectoryName(targetOutFile) ?? "", targetFormat, cancellationToken);
                    return;
                }

                // HardPatch: Extract Base Game
                string extractedBaseDir = Path.Combine(tempDir, "base_extracted");
                Directory.CreateDirectory(extractedBaseDir);

                App.RunOnUI(() =>
                {
                    task.Progress = 40;
                    task.Status = "Распаковка базовой игры...";
                    task.LogDetails += "\n📦 [3DS] Извлечение RomFS и ExeFS...";
                });

                string ctrArgs = $"--exheader=\"{Path.Combine(extractedBaseDir, "exheader.bin")}\" " +
                                 $"--exefsdir=\"{Path.Combine(extractedBaseDir, "exefs")}\" " +
                                 $"--romfsdir=\"{Path.Combine(extractedBaseDir, "romfs")}\" " +
                                 $"--plainrgn=\"{Path.Combine(extractedBaseDir, "plainrgn.bin")}\" " +
                                 $"--logo=\"{Path.Combine(extractedBaseDir, "logo.bin")}\" ";

                if (File.Exists(keysFile))
                {
                    ctrArgs += $" -k \"{keysFile}\" ";
                }

                ctrArgs += $"\"{baseFile}\"";

                await RunProcessAsync(ctrtool, ctrArgs, tempDir, cancellationToken);

                // If Update file exists, extract update and overlay
                if (!string.IsNullOrEmpty(updateFile))
                {
                    App.RunOnUI(() =>
                    {
                        task.Progress = 60;
                        task.Status = "Применение обновления (HardPatch)...";
                        task.LogDetails += $"\n🔄 [3DS] Наложение обновления {Path.GetFileName(updateFile)}...";
                    });

                    string updateExtractDir = Path.Combine(tempDir, "update_extracted");
                    Directory.CreateDirectory(updateExtractDir);

                    string updateCtrArgs = $"--exefsdir=\"{Path.Combine(updateExtractDir, "exefs")}\" " +
                                           $"--romfsdir=\"{Path.Combine(updateExtractDir, "romfs")}\" " +
                                           $"--exheader=\"{Path.Combine(updateExtractDir, "exheader.bin")}\" ";
                    if (File.Exists(keysFile)) updateCtrArgs += $" -k \"{keysFile}\" ";
                    updateCtrArgs += $"\"{updateFile}\"";

                    await RunProcessAsync(ctrtool, updateCtrArgs, tempDir, cancellationToken);

                    // Overlay update files
                    CopyDirectoryMerged(Path.Combine(updateExtractDir, "romfs"), Path.Combine(extractedBaseDir, "romfs"));
                    CopyDirectoryMerged(Path.Combine(updateExtractDir, "exefs"), Path.Combine(extractedBaseDir, "exefs"));

                    string updateExHeader = Path.Combine(updateExtractDir, "exheader.bin");
                    if (File.Exists(updateExHeader))
                    {
                        File.Copy(updateExHeader, Path.Combine(extractedBaseDir, "exheader.bin"), true);
                    }
                }

                // Overlay Mods
                foreach (var modDir in modDirs)
                {
                    App.RunOnUI(() => task.LogDetails += $"\n📁 [3DS] Применение мода: {Path.GetFileName(modDir)}...");
                    string modName = Path.GetFileName(modDir).ToLowerInvariant();
                    if (modName == "romfs" || modName.Contains("romfs"))
                    {
                        CopyDirectoryMerged(modDir, Path.Combine(extractedBaseDir, "romfs"));
                    }
                    else if (modName == "exefs" || modName.Contains("exefs"))
                    {
                        CopyDirectoryMerged(modDir, Path.Combine(extractedBaseDir, "exefs"));
                    }
                    else
                    {
                        // Check subdirs
                        string subRomfs = Path.Combine(modDir, "romfs");
                        if (Directory.Exists(subRomfs)) CopyDirectoryMerged(subRomfs, Path.Combine(extractedBaseDir, "romfs"));
                        string subExefs = Path.Combine(modDir, "exefs");
                        if (Directory.Exists(subExefs)) CopyDirectoryMerged(subExefs, Path.Combine(extractedBaseDir, "exefs"));
                    }
                }

                // Rebuild using 3dstool & makerom
                App.RunOnUI(() =>
                {
                    task.Progress = 80;
                    task.Status = $"Сборка итогового {outExt.ToUpper()}...";
                    task.LogDetails += $"\n🔨 [3DS] Сборка контейнера {targetOutFile}...";
                });

                string romfsBin = Path.Combine(tempDir, "rebuilt_romfs.bin");
                string romfsDir = Path.Combine(extractedBaseDir, "romfs");
                if (Directory.Exists(romfsDir) && Directory.EnumerateFileSystemEntries(romfsDir).Any())
                {
                    string threedstool = Get3dstoolPath();
                    if (File.Exists(threedstool))
                    {
                        await RunProcessAsync(threedstool, $"-ctf romfs \"{romfsBin}\" --romfs-dir \"{romfsDir}\"", tempDir, cancellationToken);
                    }
                }

                string outDir = Path.GetDirectoryName(targetOutFile) ?? "";
                if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

                // Build with makerom
                string formatFlag = outExt == "3ds" ? "cci" : (outExt == "cia" ? "cia" : "cxi");
                string targetType = outExt == "3ds" ? "p" : (outExt == "cia" ? "t" : "d");

                string exHeaderPath = Path.Combine(extractedBaseDir, "exheader.bin");
                string exefsCode = Path.Combine(extractedBaseDir, "exefs", "code.bin");
                string exefsBanner = Path.Combine(extractedBaseDir, "exefs", "banner.bin");
                string exefsIcon = Path.Combine(extractedBaseDir, "exefs", "icon.bin");
                string logoPath = Path.Combine(extractedBaseDir, "logo.bin");
                string plainRgnPath = Path.Combine(extractedBaseDir, "plainrgn.bin");

                string makeromArgs = $"-f {formatFlag} -target {targetType} -o \"{targetOutFile}\" ";
                if (File.Exists(exHeaderPath)) makeromArgs += $"-exheader \"{exHeaderPath}\" ";
                if (File.Exists(exefsCode)) makeromArgs += $"-code \"{exefsCode}\" ";
                if (File.Exists(exefsIcon)) makeromArgs += $"-icon \"{exefsIcon}\" ";
                if (File.Exists(exefsBanner)) makeromArgs += $"-banner \"{exefsBanner}\" ";
                if (File.Exists(logoPath)) makeromArgs += $"-logo \"{logoPath}\" ";
                if (File.Exists(plainRgnPath)) makeromArgs += $"-plainrgn \"{plainRgnPath}\" ";
                if (File.Exists(romfsBin)) makeromArgs += $"-romfs \"{romfsBin}\" ";

                await RunProcessAsync(makerom, makeromArgs, tempDir, cancellationToken);

                if (!File.Exists(targetOutFile))
                {
                    // Fallback to direct ciatocci if conversion
                    if (outExt == "3ds" && baseFile.EndsWith(".cia", StringComparison.OrdinalIgnoreCase))
                    {
                        await RunProcessAsync(makerom, $"-ciatocci \"{baseFile}\" \"{targetOutFile}\"", tempDir, cancellationToken);
                    }
                }

                if (File.Exists(targetOutFile))
                {
                    long finalSize = new FileInfo(targetOutFile).Length;
                    App.RunOnUI(() =>
                    {
                        task.Progress = 100;
                        task.Status = "Успешно";
                        task.IsRunning = false;
                        task.TargetSize = ProcessingTask.FormatSize(finalSize);
                        task.LogDetails += $"\n✅ [Готово] Успешно собран: {Path.GetFileName(targetOutFile)} ({task.TargetSize})";
                    });
                }
                else
                {
                    throw new Exception("Не удалось создать итоговый 3DS файл через makerom.");
                }
            }
            finally
            {
                TempCleanupService.ForceDeleteDirectory(tempDir);
            }
        }

        public async Task Convert3dsContainerAsync(
            ProcessingTask task,
            string inputPath,
            string outputFolder,
            string targetFormat,
            CancellationToken cancellationToken)
        {
            string makerom = GetMakeromPath();
            if (!File.Exists(makerom)) throw new FileNotFoundException($"makerom не найден: {makerom}");

            string inExt = Path.GetExtension(inputPath).ToLowerInvariant().TrimStart('.');
            string outExt = targetFormat.ToLowerInvariant().Replace(" (cci)", "").Trim();
            if (outExt == "3ds" || outExt == "cci") outExt = "3ds";
            else if (outExt == "cia") outExt = "cia";
            else outExt = "cxi";

            string outFileName = Path.GetFileNameWithoutExtension(inputPath) + "." + outExt;
            string outPath = Path.Combine(outputFolder, outFileName);

            if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);

            App.RunOnUI(() =>
            {
                task.IsRunning = true;
                task.Progress = 30;
                task.Status = $"Конвертация {inExt.ToUpper()} -> {outExt.ToUpper()}...";
                task.LogDetails += $"\n🔄 [3DS Convert] Конвертация {Path.GetFileName(inputPath)} -> {outFileName}...";
            });

            if ((inExt == "3ds" || inExt == "cci") && outExt == "cia")
            {
                await RunProcessAsync(makerom, $"-ccitocia \"{inputPath}\" \"{outPath}\"", outputFolder, cancellationToken);
            }
            else if (inExt == "cia" && (outExt == "3ds" || outExt == "cci"))
            {
                await RunProcessAsync(makerom, $"-ciatocci \"{inputPath}\" \"{outPath}\"", outputFolder, cancellationToken);
            }
            else
            {
                // CXI or generic via extraction and pack
                await Build3dsMultiContentAsync(task, new List<string> { inputPath }, outPath, targetFormat, cancellationToken);
                return;
            }

            if (File.Exists(outPath))
            {
                long finalSize = new FileInfo(outPath).Length;
                App.RunOnUI(() =>
                {
                    task.Progress = 100;
                    task.Status = "Успешно";
                    task.IsRunning = false;
                    task.TargetSize = ProcessingTask.FormatSize(finalSize);
                    task.LogDetails += $"\n✅ [Готово] Сохранен: {outFileName} ({task.TargetSize})";
                });
            }
            else
            {
                throw new Exception($"Конвертация 3DS завершилась с ошибкой. Выходной файл не создан.");
            }
        }

        public async Task Unpack3dsContainerAsync(
            ProcessingTask task,
            string inputPath,
            string outputBaseFolder,
            CancellationToken cancellationToken)
        {
            string ctrtool = GetCtrtoolPath();
            string keysFile = GetAesKeysPath();

            if (!File.Exists(ctrtool)) throw new FileNotFoundException($"ctrtool не найден: {ctrtool}");

            string gameName = Path.GetFileNameWithoutExtension(inputPath);
            string outDir = Path.Combine(outputBaseFolder, $"{gameName}_extracted");
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            App.RunOnUI(() =>
            {
                task.IsRunning = true;
                task.Progress = 20;
                task.Status = "Распаковка 3DS...";
                task.LogDetails += $"\n📦 [3DS Unpack] Извлечение разделов в {outDir}...";
            });

            string ctrArgs = $"--exheader=\"{Path.Combine(outDir, "exheader.bin")}\" " +
                             $"--exefsdir=\"{Path.Combine(outDir, "exefs")}\" " +
                             $"--romfsdir=\"{Path.Combine(outDir, "romfs")}\" " +
                             $"--plainrgn=\"{Path.Combine(outDir, "plainrgn.bin")}\" " +
                             $"--logo=\"{Path.Combine(outDir, "logo.bin")}\" ";

            if (File.Exists(keysFile)) ctrArgs += $" -k \"{keysFile}\" ";
            ctrArgs += $"\"{inputPath}\"";

            await RunProcessAsync(ctrtool, ctrArgs, outDir, cancellationToken);

            App.RunOnUI(() =>
            {
                task.Progress = 100;
                task.Status = "Успешно";
                task.IsRunning = false;
                task.LogDetails += $"\n✅ [Готово] 3DS контейнер распакован в: {outDir}";
            });
        }

        public async Task Pack3dsContainerAsync(
            ProcessingTask task,
            string inputFolder,
            string outputBaseFolder,
            string outputFileName,
            CancellationToken cancellationToken)
        {
            string makerom = GetMakeromPath();
            string threedstool = Get3dstoolPath();

            if (!File.Exists(makerom)) throw new FileNotFoundException($"makerom не найден: {makerom}");

            string targetFormat = task.TargetFormat ?? "3DS";
            string outExt = targetFormat.ToLowerInvariant().Replace(" (cci)", "").Trim();
            if (outExt == "3ds" || outExt == "cci") outExt = "3ds";
            else if (outExt == "cia") outExt = "cia";
            else outExt = "cxi";

            string outName = string.IsNullOrEmpty(outputFileName) ? Path.GetFileName(inputFolder) : outputFileName;
            if (!outName.EndsWith("." + outExt, StringComparison.OrdinalIgnoreCase))
            {
                outName = Path.ChangeExtension(outName, "." + outExt);
            }

            string outPath = Path.Combine(outputBaseFolder, outName);
            if (!Directory.Exists(outputBaseFolder)) Directory.CreateDirectory(outputBaseFolder);

            string tempDir = Path.Combine(Path.GetTempPath(), $"Storm3DS_Pack_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            TempCleanupService.RegisterActiveTempDirectory(tempDir);

            try
            {
                App.RunOnUI(() =>
                {
                    task.IsRunning = true;
                    task.Progress = 20;
                    task.Status = $"Сборка {outExt.ToUpper()}...";
                    task.LogDetails += $"\n🔨 [3DS Pack] Упаковка из папки {inputFolder} в {outName}...";
                });

                string romfsBin = Path.Combine(tempDir, "rebuilt_romfs.bin");
                string romfsDir = Path.Combine(inputFolder, "romfs");
                if (Directory.Exists(romfsDir) && Directory.EnumerateFileSystemEntries(romfsDir).Any() && File.Exists(threedstool))
                {
                    await RunProcessAsync(threedstool, $"-ctf romfs \"{romfsBin}\" --romfs-dir \"{romfsDir}\"", tempDir, cancellationToken);
                }

                string formatFlag = outExt == "3ds" ? "cci" : (outExt == "cia" ? "cia" : "cxi");
                string targetType = outExt == "3ds" ? "p" : (outExt == "cia" ? "t" : "d");

                string exHeaderPath = Path.Combine(inputFolder, "exheader.bin");
                string exefsCode = Path.Combine(inputFolder, "exefs", "code.bin");
                string exefsBanner = Path.Combine(inputFolder, "exefs", "banner.bin");
                string exefsIcon = Path.Combine(inputFolder, "exefs", "icon.bin");
                string logoPath = Path.Combine(inputFolder, "logo.bin");
                string plainRgnPath = Path.Combine(inputFolder, "plainrgn.bin");

                string makeromArgs = $"-f {formatFlag} -target {targetType} -o \"{outPath}\" ";
                if (File.Exists(exHeaderPath)) makeromArgs += $"-exheader \"{exHeaderPath}\" ";
                if (File.Exists(exefsCode)) makeromArgs += $"-code \"{exefsCode}\" ";
                if (File.Exists(exefsIcon)) makeromArgs += $"-icon \"{exefsIcon}\" ";
                if (File.Exists(exefsBanner)) makeromArgs += $"-banner \"{exefsBanner}\" ";
                if (File.Exists(logoPath)) makeromArgs += $"-logo \"{logoPath}\" ";
                if (File.Exists(plainRgnPath)) makeromArgs += $"-plainrgn \"{plainRgnPath}\" ";
                if (File.Exists(romfsBin)) makeromArgs += $"-romfs \"{romfsBin}\" ";

                await RunProcessAsync(makerom, makeromArgs, tempDir, cancellationToken);

                if (File.Exists(outPath))
                {
                    long finalSize = new FileInfo(outPath).Length;
                    App.RunOnUI(() =>
                    {
                        task.Progress = 100;
                        task.Status = "Успешно";
                        task.IsRunning = false;
                        task.TargetSize = ProcessingTask.FormatSize(finalSize);
                        task.LogDetails += $"\n✅ [Готово] Сохранен: {outName} ({task.TargetSize})";
                    });
                }
                else
                {
                    throw new Exception("Не удалось создать 3DS файл через makerom.");
                }
            }
            finally
            {
                TempCleanupService.ForceDeleteDirectory(tempDir);
            }
        }

        private static void CopyDirectoryMerged(string sourceDir, string targetDir)
        {
            if (!Directory.Exists(sourceDir)) return;
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string dest = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, dest, true);
            }

            foreach (string dir in Directory.GetDirectories(sourceDir))
            {
                string dest = Path.Combine(targetDir, Path.GetFileName(dir));
                CopyDirectoryMerged(dir, dest);
            }
        }

        private async Task RunProcessAsync(string exePath, string args, string workingDir, CancellationToken cancellationToken)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = args,
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = psi };
            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (s, e) => { if (e.Data != null) output.AppendLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) error.AppendLine(e.Data); };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                App.Logger.Log($"[3DS Tool Error ({Path.GetFileName(exePath)}) Exit={process.ExitCode}]: {error}\n{output}", LogLevel.Warning);
            }
        }
    }
}
