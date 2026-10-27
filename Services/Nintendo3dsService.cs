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

        private string GetSeedDbPath()
        {
            if (!string.IsNullOrEmpty(App.Settings.Current.KeysPath3ds))
            {
                if (App.Settings.Current.KeysPath3ds.EndsWith("seeddb.bin", StringComparison.OrdinalIgnoreCase) && File.Exists(App.Settings.Current.KeysPath3ds))
                    return App.Settings.Current.KeysPath3ds;
                string? dir = Path.GetDirectoryName(App.Settings.Current.KeysPath3ds);
                if (!string.IsNullOrEmpty(dir))
                {
                    string candidate = Path.Combine(dir, "seeddb.bin");
                    if (File.Exists(candidate)) return candidate;
                }
            }
            string toolSeed = Path.Combine(GetToolsDir(), "seeddb.bin");
            if (File.Exists(toolSeed)) return toolSeed;
            string userSeed = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".3ds", "seeddb.bin");
            if (File.Exists(userSeed)) return userSeed;
            return string.Empty;
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
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new BinaryReader(fs);

                if (fs.Length >= 0x200)
                {
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

                        // Read Partition 0 (Main CXI)
                        fs.Seek(0x120, SeekOrigin.Begin);
                        uint partition0Offset = reader.ReadUInt32();
                        long ncchOffset = (long)partition0Offset * 0x200;

                        if (ncchOffset > 0 && ncchOffset + 0x200 <= fs.Length)
                        {
                            TryReadNcchAndSmdh(fs, reader, ncchOffset, info);
                        }
                    }
                    else if (magicStr == "NCCH")
                    {
                        // .cxi / .cfa
                        TryReadNcchAndSmdh(fs, reader, 0, info);
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

                        if (headerSize >= 0x20 && headerSize <= 0x4000 && certSize > 0 && tmdSize > 0)
                        {
                            long tmdOffset = Align64(headerSize) + Align64(certSize) + Align64(ticketSize);
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

                                Classify3dsTitleId(info);
                            }

                            long contentOffset = tmdOffset + Align64(tmdSize);
                            if (contentOffset + 0x200 <= fs.Length)
                            {
                                TryReadNcchAndSmdh(fs, reader, contentOffset, info);
                            }

                            if (metaSize >= 0x36C0)
                            {
                                long metaOffset = contentOffset + Align64((long)contentSize);
                                if (metaOffset + 0x400 + 0x36C0 <= fs.Length)
                                {
                                    TryReadSmdhAtOffset(fs, reader, metaOffset + 0x400, info);
                                }
                            }
                        }
                    }
                }

                Classify3dsTitleId(info);

                if (string.IsNullOrEmpty(info.GameName))
                {
                    info.GameName = Path.GetFileNameWithoutExtension(filePath);
                }
            }
            catch { }

            return info;
        }

        private static long Align64(long val) => (val + 0x3F) & ~0x3FL;

        private static void Classify3dsTitleId(Nintendo3dsInfo info)
        {
            if (string.IsNullOrEmpty(info.TitleId)) return;
            if (info.TitleId.StartsWith("0004000E", StringComparison.OrdinalIgnoreCase)) info.ContentType = "Patch";
            else if (info.TitleId.StartsWith("0004008C", StringComparison.OrdinalIgnoreCase)) info.ContentType = "AddOnContent";
            else if (string.IsNullOrEmpty(info.ContentType)) info.ContentType = "Application";
        }

        private static void TryReadNcchAndSmdh(FileStream fs, BinaryReader reader, long ncchOffset, Nintendo3dsInfo info)
        {
            try
            {
                if (ncchOffset + 0x200 > fs.Length) return;
                fs.Seek(ncchOffset + 0x100, SeekOrigin.Begin);
                string magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
                if (magic != "NCCH") return;

                fs.Seek(ncchOffset + 0x108, SeekOrigin.Begin);
                ulong progId = reader.ReadUInt64();
                if (string.IsNullOrEmpty(info.TitleId) || info.TitleId == "0000000000000000")
                {
                    info.TitleId = progId.ToString("X16");
                }

                fs.Seek(ncchOffset + 0x118, SeekOrigin.Begin);
                ushort ver = reader.ReadUInt16();
                if (string.IsNullOrEmpty(info.Version) || info.Version == "v0")
                {
                    info.Version = $"v{ver}";
                }

                fs.Seek(ncchOffset + 0x150, SeekOrigin.Begin);
                string prodCode = Encoding.ASCII.GetString(reader.ReadBytes(16)).Trim('\0', ' ');
                if (!string.IsNullOrEmpty(prodCode))
                {
                    info.ProductCode = prodCode;
                }

                fs.Seek(ncchOffset + 0x1A0, SeekOrigin.Begin);
                uint exefsOffsetUnits = reader.ReadUInt32();
                uint exefsSizeUnits = reader.ReadUInt32();
                if (exefsOffsetUnits > 0 && exefsSizeUnits > 0)
                {
                    long exefsOffset = ncchOffset + (long)exefsOffsetUnits * 0x200;
                    if (exefsOffset + 0x200 <= fs.Length)
                    {
                        for (int i = 0; i < 10; i++)
                        {
                            fs.Seek(exefsOffset + i * 16, SeekOrigin.Begin);
                            string fileName = Encoding.ASCII.GetString(reader.ReadBytes(8)).Trim('\0');
                            uint fileOffset = reader.ReadUInt32();
                            uint fileSize = reader.ReadUInt32();
                            if ((fileName == "icon" || fileName == "icon.bin" || fileName == ".icon") && fileSize >= 0x36C0)
                            {
                                long smdhOffset = exefsOffset + 0x200 + fileOffset;
                                if (smdhOffset + 0x36C0 <= fs.Length)
                                {
                                    TryReadSmdhAtOffset(fs, reader, smdhOffset, info);
                                }
                                break;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private static void TryReadSmdhAtOffset(FileStream fs, BinaryReader reader, long smdhOffset, Nintendo3dsInfo info)
        {
            try
            {
                fs.Seek(smdhOffset, SeekOrigin.Begin);
                string magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
                if (magic != "SMDH") return;

                // 16 language slots (0x200 bytes each) starting at smdhOffset + 0x08
                // Index 1 = English, Index 0 = Japanese, Index 11 = Russian
                int[] preferredSlots = { 1, 11, 0, 2, 3, 4, 5 };
                foreach (int slot in preferredSlots)
                {
                    long slotOffset = smdhOffset + 0x08 + slot * 0x200;
                    fs.Seek(slotOffset, SeekOrigin.Begin);
                    string shortTitle = Encoding.Unicode.GetString(reader.ReadBytes(0x80)).Trim('\0', ' ', '\r', '\n');
                    string longTitle = Encoding.Unicode.GetString(reader.ReadBytes(0x100)).Trim('\0', ' ', '\r', '\n');
                    string publisher = Encoding.Unicode.GetString(reader.ReadBytes(0x80)).Trim('\0', ' ', '\r', '\n');

                    if (!string.IsNullOrWhiteSpace(shortTitle) || !string.IsNullOrWhiteSpace(longTitle))
                    {
                        info.GameName = !string.IsNullOrWhiteSpace(shortTitle) ? shortTitle : longTitle.Replace("\n", " ");
                        if (!string.IsNullOrWhiteSpace(publisher)) info.Publisher = publisher;
                        break;
                    }
                }

                // Read 48x48 RGB565 Morton-tiled icon at smdhOffset + 0x24C0 (size 0x1200 = 4608 bytes)
                if (info.IconBytes == null && smdhOffset + 0x24C0 + 0x1200 <= fs.Length)
                {
                    fs.Seek(smdhOffset + 0x24C0, SeekOrigin.Begin);
                    byte[] tileData = reader.ReadBytes(0x1200);
                    info.IconBytes = DecodeSmdhIconToPng(tileData);
                }
            }
            catch { }
        }

        private static byte[]? DecodeSmdhIconToPng(byte[] tileData)
        {
            try
            {
                if (tileData == null || tileData.Length < 0x1200) return null;
                using var bmp = new System.Drawing.Bitmap(48, 48, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                var rect = new System.Drawing.Rectangle(0, 0, 48, 48);
                var bmpData = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    int[] argbPixels = new int[48 * 48];
                    int i = 0;
                    for (int tileY = 0; tileY < 48; tileY += 8)
                    {
                        for (int tileX = 0; tileX < 48; tileX += 8)
                        {
                            for (int k = 0; k < 64; k++)
                            {
                                int x = (k & 0x01) | ((k & 0x04) >> 1) | ((k & 0x10) >> 2);
                                int y = ((k & 0x02) >> 1) | ((k & 0x08) >> 2) | ((k & 0x20) >> 3);
                                ushort pixel = (ushort)(tileData[i] | (tileData[i + 1] << 8));
                                i += 2;
                                int r = ((pixel >> 11) & 0x1F) * 255 / 31;
                                int g = ((pixel >> 5) & 0x3F) * 255 / 63;
                                int b = (pixel & 0x1F) * 255 / 31;
                                argbPixels[(tileY + y) * 48 + (tileX + x)] = (255 << 24) | (r << 16) | (g << 8) | b;
                            }
                        }
                    }
                    System.Runtime.InteropServices.Marshal.Copy(argbPixels, 0, bmpData.Scan0, argbPixels.Length);
                }
                finally
                {
                    bmp.UnlockBits(bmpData);
                }
                using var ms = new MemoryStream();
                bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return ms.ToArray();
            }
            catch
            {
                return null;
            }
        }

        public async Task Build3dsMultiContentAsync(
            ProcessingTask task,
            List<string> inputFiles,
            string outputFilePath,
            string targetFormat,
            CancellationToken cancellationToken,
            bool forceRebuild = false)
        {
            string makerom = GetMakeromPath();
            string ctrtool = GetCtrtoolPath();

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
                var updateFiles = new List<(string Path, int VersionNum)>();
                var dlcFiles = new List<string>();
                var modDirs = new List<string>();
                var patchFiles = new List<string>();

                foreach (var f in inputFiles)
                {
                    if (Directory.Exists(f))
                    {
                        modDirs.Add(f);
                        continue;
                    }
                    if (!File.Exists(f)) continue;

                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext == ".ips" || ext == ".bps")
                    {
                        patchFiles.Add(f);
                        continue;
                    }

                    var meta = Parse3dsFile(f);
                    int verNum = 0;
                    if (!string.IsNullOrEmpty(meta.Version) && meta.Version.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                    {
                        int.TryParse(meta.Version.Substring(1), out verNum);
                    }

                    if (meta.ContentType == "Patch" || meta.TitleId.StartsWith("0004000E", StringComparison.OrdinalIgnoreCase))
                    {
                        updateFiles.Add((f, verNum));
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
                    baseFile = inputFiles.FirstOrDefault(f => File.Exists(f) && Is3dsExtension(Path.GetExtension(f)));
                }

                if (baseFile == null)
                {
                    throw new Exception("Не найден базовый файл игры (.3ds, .cci, .cia, .cxi).");
                }

                // Sort updates ascending by version so the newest update overlays last
                var sortedUpdates = updateFiles.OrderBy(u => u.VersionNum).Select(u => u.Path).ToList();
                var baseMeta = Parse3dsFile(baseFile);

                App.RunOnUI(() =>
                {
                    task.Progress = 25;
                    task.Status = "Подготовка контента 3DS...";
                    task.LogDetails += $"\n  🎮 Базовая игра: {Path.GetFileName(baseFile)} [{baseMeta.TitleId}] ({baseMeta.ProductCode})";
                    foreach (var upd in sortedUpdates)
                        task.LogDetails += $"\n  🔄 Обновление: {Path.GetFileName(upd)}";
                    if (dlcFiles.Count > 0) task.LogDetails += $"\n  📦 Дополнения (DLC): {dlcFiles.Count} шт.";
                    if (modDirs.Count > 0) task.LogDetails += $"\n  📁 Модификации / Русификаторы: {modDirs.Count} папок";
                    if (patchFiles.Count > 0) task.LogDetails += $"\n  🩹 Бинарные патчи (IPS/BPS): {patchFiles.Count} шт.";
                });

                bool isDual3ds = string.Equals(targetFormat, "3DS + CIA", StringComparison.OrdinalIgnoreCase);
                string outExt = targetFormat.ToLowerInvariant().Replace(" (cci)", "").Trim();
                if (outExt == "3ds" || outExt == "cci" || isDual3ds) outExt = "3ds";
                else if (outExt == "cia") outExt = "cia";
                else outExt = "cxi";

                string targetOutFile = Path.ChangeExtension(outputFilePath, "." + outExt);

                // If only a single base file and no updates/mods/dlcs/patches, convert directly unless forceRebuild is requested
                bool hasCustomMeta = task.CustomMetadata != null && task.CustomMetadata.HasModifications;
                if (!forceRebuild && !hasCustomMeta && sortedUpdates.Count == 0 && modDirs.Count == 0 && dlcFiles.Count == 0 && patchFiles.Count == 0)
                {
                    await Convert3dsContainerAsync(task, baseFile, Path.GetDirectoryName(targetOutFile) ?? "", targetFormat, cancellationToken);
                    return;
                }

                // Step 1: Extract Base Game (RomFS, ExeFS, ExHeader, Logo, PlainRgn + Secondary CFAs)
                string extractedBaseDir = Path.Combine(tempDir, "base_extracted");
                Directory.CreateDirectory(extractedBaseDir);

                App.RunOnUI(() =>
                {
                    task.Progress = 40;
                    task.Status = "Распаковка базовой игры...";
                    task.LogDetails += "\n📦 [3DS] Извлечение базового RomFS, ExeFS и ExHeader...";
                });

                await Extract3dsContainerToDirAsync(baseFile, extractedBaseDir, tempDir, cancellationToken, extractSecondaryCfa: true);

                // Backup Base Game's 8-byte ProgramID from exheader.bin so update exheader doesn't overwrite TitleID to 0004000E
                byte[]? baseProgramIdBytes = null;
                string baseExHeaderPath = Path.Combine(extractedBaseDir, "exheader.bin");
                if (File.Exists(baseExHeaderPath) && new FileInfo(baseExHeaderPath).Length >= 0x400)
                {
                    byte[] exh = await File.ReadAllBytesAsync(baseExHeaderPath, cancellationToken);
                    baseProgramIdBytes = new byte[8];
                    Array.Copy(exh, 0x1C8, baseProgramIdBytes, 0, 8);
                }

                // Step 2: Extract & Overlay Updates (0004000E)
                int updIdx = 0;
                foreach (var updateFile in sortedUpdates)
                {
                    updIdx++;
                    App.RunOnUI(() =>
                    {
                        task.Progress = 55;
                        task.Status = $"Применение обновления ({updIdx}/{sortedUpdates.Count})...";
                        task.LogDetails += $"\n🔄 [3DS] Наложение обновления {Path.GetFileName(updateFile)}...";
                    });

                    string updateExtractDir = Path.Combine(tempDir, $"update_extracted_{updIdx}");
                    Directory.CreateDirectory(updateExtractDir);

                    await Extract3dsContainerToDirAsync(updateFile, updateExtractDir, tempDir, cancellationToken, extractSecondaryCfa: false);

                    NormalizeExefsFiles(Path.Combine(updateExtractDir, "exefs"));
                    NormalizeExefsFiles(Path.Combine(extractedBaseDir, "exefs"));

                    CopyDirectoryMerged(Path.Combine(updateExtractDir, "romfs"), Path.Combine(extractedBaseDir, "romfs"));
                    CopyDirectoryMerged(Path.Combine(updateExtractDir, "exefs"), Path.Combine(extractedBaseDir, "exefs"));

                    string updateExHeader = Path.Combine(updateExtractDir, "exheader.bin");
                    if (File.Exists(updateExHeader) && new FileInfo(updateExHeader).Length >= 0x400)
                    {
                        File.Copy(updateExHeader, baseExHeaderPath, true);
                    }
                }

                // Step 3: Extract & Integrate DLCs (0004008C)
                var extraCfaFiles = new List<string>();
                int dlcIdx = 0;
                foreach (var dlcFile in dlcFiles)
                {
                    dlcIdx++;
                    App.RunOnUI(() =>
                    {
                        task.Progress = 65;
                        task.Status = $"Интеграция DLC ({dlcIdx}/{dlcFiles.Count})...";
                        task.LogDetails += $"\n📦 [3DS] Интеграция дополнения {Path.GetFileName(dlcFile)}...";
                    });

                    string dlcExtractDir = Path.Combine(tempDir, $"dlc_extracted_{dlcIdx}");
                    Directory.CreateDirectory(dlcExtractDir);

                    await Extract3dsContainerToDirAsync(dlcFile, dlcExtractDir, tempDir, cancellationToken, extractSecondaryCfa: true);

                    string dlcRomfs = Path.Combine(dlcExtractDir, "romfs");
                    if (Directory.Exists(dlcRomfs) && Directory.EnumerateFileSystemEntries(dlcRomfs).Any())
                    {
                        CopyDirectoryMerged(dlcRomfs, Path.Combine(extractedBaseDir, "romfs"));
                    }

                    foreach (var cfa in Directory.GetFiles(dlcExtractDir, "cfa_*.cfa").OrderBy(f => f))
                    {
                        if (new FileInfo(cfa).Length > 0)
                        {
                            extraCfaFiles.Add(cfa);
                        }
                    }
                }

                // Step 4: Apply Mods / Russian Localizations (romfs/, exefs/, romfs.bin, luma/titles/<TID>/..., code.bin, IPS/BPS patches)
                NormalizeExefsFiles(Path.Combine(extractedBaseDir, "exefs"));
                foreach (var modDir in modDirs)
                {
                    await Apply3dsModDirectoryAsync(modDir, extractedBaseDir, tempDir, task, cancellationToken);
                }

                // Apply standalone IPS/BPS patches
                string baseCodeBin = Path.Combine(extractedBaseDir, "exefs", "code.bin");
                foreach (var patchFile in patchFiles)
                {
                    if (File.Exists(baseCodeBin))
                    {
                        ApplyBinaryPatchToCodeBin(patchFile, baseCodeBin, task);
                    }
                }

                // Step 5: Restore Base Game ProgramID in exheader.bin
                if (File.Exists(baseExHeaderPath) && baseProgramIdBytes != null)
                {
                    byte[] exh = await File.ReadAllBytesAsync(baseExHeaderPath, cancellationToken);
                    if (exh.Length >= 0x400)
                    {
                        Array.Copy(baseProgramIdBytes, 0, exh, 0x1C8, 8);
                        Array.Copy(baseProgramIdBytes, 0, exh, 0x200, 8);
                        if (exh.Length >= 0x608)
                        {
                            Array.Copy(baseProgramIdBytes, 0, exh, 0x600, 8);
                        }
                        await File.WriteAllBytesAsync(baseExHeaderPath, exh, cancellationToken);
                    }
                }

                // Step 6: Rebuild 3DS/CIA/CXI container
                await Build3dsFromExtractedDirAsync(
                    task,
                    extractedBaseDir,
                    tempDir,
                    targetOutFile,
                    outExt,
                    isDual3ds,
                    baseMeta,
                    extraCfaFiles,
                    cancellationToken);
            }
            finally
            {
                TempCleanupService.ForceDeleteDirectory(tempDir);
            }
        }

        private async Task Extract3dsContainerToDirAsync(
            string inputFile,
            string targetDir,
            string tempDir,
            CancellationToken cancellationToken,
            bool extractSecondaryCfa)
        {
            inputFile = await Nintendo3dsCryptoService.EnsureDecrypted3dsFileAsync(inputFile, tempDir, null, cancellationToken);
            string ctrtool = GetCtrtoolPath();
            string threedstool = Get3dstoolPath();
            string seedDb = GetSeedDbPath();
            string seedArg = !string.IsNullOrEmpty(seedDb) ? $"--seeddb=\"{seedDb}\" " : "";

            string inExt = Path.GetExtension(inputFile).ToLowerInvariant().TrimStart('.');
            string exHeaderPath = Path.Combine(targetDir, "exheader.bin");
            string exefsDir = Path.Combine(targetDir, "exefs");
            string romfsDir = Path.Combine(targetDir, "romfs");
            string plainRgnPath = Path.Combine(targetDir, "plainrgn.bin");
            string logoPath = Path.Combine(targetDir, "logo.bin");

            Directory.CreateDirectory(exefsDir);
            Directory.CreateDirectory(romfsDir);

            if (inExt == "cia")
            {
                string contentsDir = Path.Combine(tempDir, $"cia_contents_{Guid.NewGuid():N}");
                Directory.CreateDirectory(contentsDir);
                string prefix = Path.Combine(contentsDir, "contents");

                await RunProcessAsync(ctrtool, $"{seedArg}--contents=\"{prefix}\" \"{inputFile}\"", tempDir, cancellationToken);

                var contentFiles = Directory.GetFiles(contentsDir, "contents.*")
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (contentFiles.Count > 0)
                {
                    string mainNcch = contentFiles[0];
                    string ncchArgs = $"{seedArg}--intype=ncch " +
                                      $"--exheader=\"{exHeaderPath}\" " +
                                      $"--exefsdir=\"{exefsDir}\" " +
                                      $"--romfsdir=\"{romfsDir}\" " +
                                      $"--plainrgn=\"{plainRgnPath}\" " +
                                      $"--logo=\"{logoPath}\" " +
                                      $"--decompresscode \"{mainNcch}\"";
                    await RunProcessAsync(ctrtool, ncchArgs, tempDir, cancellationToken);

                    if (extractSecondaryCfa && contentFiles.Count > 1)
                    {
                        for (int i = 1; i < contentFiles.Count; i++)
                        {
                            string destCfa = Path.Combine(targetDir, $"cfa_{i}.cfa");
                            File.Copy(contentFiles[i], destCfa, true);
                        }
                    }
                }
            }
            else if (inExt == "3ds" || inExt == "cci")
            {
                string ctrArgs = $"{seedArg}-n 0 " +
                                 $"--exheader=\"{exHeaderPath}\" " +
                                 $"--exefsdir=\"{exefsDir}\" " +
                                 $"--romfsdir=\"{romfsDir}\" " +
                                 $"--plainrgn=\"{plainRgnPath}\" " +
                                 $"--logo=\"{logoPath}\" " +
                                 $"--decompresscode \"{inputFile}\"";
                await RunProcessAsync(ctrtool, ctrArgs, tempDir, cancellationToken);

                if (extractSecondaryCfa && File.Exists(threedstool))
                {
                    string cxi0 = Path.Combine(tempDir, $"part0_{Guid.NewGuid():N}.cxi");
                    string cfa1 = Path.Combine(targetDir, "cfa_1.cfa");
                    string cfa2 = Path.Combine(targetDir, "cfa_2.cfa");
                    await RunProcessAsync(threedstool, $"-xt012f cci \"{cxi0}\" \"{cfa1}\" \"{cfa2}\" \"{inputFile}\"", GetToolsDir(), cancellationToken);
                    try { if (File.Exists(cxi0)) File.Delete(cxi0); } catch { }
                }
            }
            else
            {
                string ctrArgs = $"{seedArg}" +
                                 $"--exheader=\"{exHeaderPath}\" " +
                                 $"--exefsdir=\"{exefsDir}\" " +
                                 $"--romfsdir=\"{romfsDir}\" " +
                                 $"--plainrgn=\"{plainRgnPath}\" " +
                                 $"--logo=\"{logoPath}\" " +
                                 $"--decompresscode \"{inputFile}\"";
                await RunProcessAsync(ctrtool, ctrArgs, tempDir, cancellationToken);
            }

            NormalizeExefsFiles(exefsDir);
        }

        private static void NormalizeExefsFiles(string exefsDir)
        {
            if (!Directory.Exists(exefsDir)) return;
            try
            {
                string dotCode = Path.Combine(exefsDir, ".code");
                string codeBin = Path.Combine(exefsDir, "code.bin");
                if (File.Exists(dotCode) && !File.Exists(codeBin)) File.Move(dotCode, codeBin);

                string bannerBnr = Path.Combine(exefsDir, "banner.bnr");
                string dotBanner = Path.Combine(exefsDir, "banner");
                string bannerBin = Path.Combine(exefsDir, "banner.bin");
                if (File.Exists(bannerBnr) && !File.Exists(bannerBin)) File.Move(bannerBnr, bannerBin);
                else if (File.Exists(dotBanner) && !File.Exists(bannerBin)) File.Move(dotBanner, bannerBin);

                string iconIcn = Path.Combine(exefsDir, "icon.icn");
                string dotIcon = Path.Combine(exefsDir, "icon");
                string iconBin = Path.Combine(exefsDir, "icon.bin");
                if (File.Exists(iconIcn) && !File.Exists(iconBin)) File.Move(iconIcn, iconBin);
                else if (File.Exists(dotIcon) && !File.Exists(iconBin)) File.Move(dotIcon, iconBin);
            }
            catch { }
        }

        private async Task Apply3dsModDirectoryAsync(
            string modDir,
            string extractedBaseDir,
            string tempDir,
            ProcessingTask task,
            CancellationToken cancellationToken)
        {
            if (!Directory.Exists(modDir)) return;

            App.RunOnUI(() => task.LogDetails += $"\n📁 [3DS Mod] Интеграция модификации / русификатора: {Path.GetFileName(modDir)}...");

            string baseRomfs = Path.Combine(extractedBaseDir, "romfs");
            string baseExefs = Path.Combine(extractedBaseDir, "exefs");
            string baseExHeader = Path.Combine(extractedBaseDir, "exheader.bin");
            Directory.CreateDirectory(baseRomfs);
            Directory.CreateDirectory(baseExefs);

            string modName = Path.GetFileName(modDir).ToLowerInvariant();
            bool appliedStructured = false;

            if (modName == "romfs")
            {
                CopyDirectoryMerged(modDir, baseRomfs);
                appliedStructured = true;
            }
            else if (modName == "exefs")
            {
                CopyDirectoryMerged(modDir, baseExefs);
                NormalizeExefsFiles(baseExefs);
                appliedStructured = true;
            }
            else
            {
                // 1. Search recursively for any 'romfs' or 'exefs' subdirectories (supports luma/titles/<TID>/romfs, Citra load/mods/<TID>/romfs, etc.)
                var allSubDirs = Directory.GetDirectories(modDir, "*", SearchOption.AllDirectories);
                foreach (var sub in allSubDirs)
                {
                    string subName = Path.GetFileName(sub).ToLowerInvariant();
                    if (subName == "romfs")
                    {
                        CopyDirectoryMerged(sub, baseRomfs);
                        appliedStructured = true;
                    }
                    else if (subName == "exefs")
                    {
                        CopyDirectoryMerged(sub, baseExefs);
                        NormalizeExefsFiles(baseExefs);
                        appliedStructured = true;
                    }
                }

                // 2. Search for pre-packed romfs.bin, code.bin, exheader.bin, banner.bin, icon.bin, and .ips/.bps patches
                var allFiles = Directory.GetFiles(modDir, "*", SearchOption.AllDirectories);
                foreach (var file in allFiles)
                {
                    // Skip files inside already-merged romfs/exefs directories
                    string rel = Path.GetRelativePath(modDir, file);
                    var segments = rel.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (segments.Any(s => s.Equals("romfs", StringComparison.OrdinalIgnoreCase) || s.Equals("exefs", StringComparison.OrdinalIgnoreCase)))
                        continue;

                    string fn = Path.GetFileName(file).ToLowerInvariant();
                    if (fn == "romfs.bin")
                    {
                        string threedstool = Get3dstoolPath();
                        if (File.Exists(threedstool))
                        {
                            string unpackModRomfs = Path.Combine(tempDir, $"mod_romfs_{Guid.NewGuid():N}");
                            Directory.CreateDirectory(unpackModRomfs);
                            await RunProcessAsync(threedstool, $"-xvf romfs \"{file}\" --romfs-dir \"{unpackModRomfs}\"", GetToolsDir(), cancellationToken);
                            CopyDirectoryMerged(unpackModRomfs, baseRomfs);
                            appliedStructured = true;
                            App.RunOnUI(() => task.LogDetails += $"\n  ✓ Распакован и наложен образ {Path.GetFileName(file)}");
                        }
                    }
                    else if (fn == "code.bin" || fn == ".code")
                    {
                        File.Copy(file, Path.Combine(baseExefs, "code.bin"), true);
                        appliedStructured = true;
                        App.RunOnUI(() => task.LogDetails += $"\n  ✓ Заменен исполняемый файл code.bin");
                    }
                    else if (fn == "exheader.bin")
                    {
                        File.Copy(file, baseExHeader, true);
                        appliedStructured = true;
                    }
                    else if (fn == "banner.bin" || fn == "banner.bnr")
                    {
                        File.Copy(file, Path.Combine(baseExefs, "banner.bin"), true);
                        appliedStructured = true;
                    }
                    else if (fn == "icon.bin" || fn == "icon.icn")
                    {
                        File.Copy(file, Path.Combine(baseExefs, "icon.bin"), true);
                        appliedStructured = true;
                    }
                    else if (fn.EndsWith(".ips") || fn.EndsWith(".bps"))
                    {
                        string targetCode = Path.Combine(baseExefs, "code.bin");
                        if (File.Exists(targetCode))
                        {
                            ApplyBinaryPatchToCodeBin(file, targetCode, task);
                            appliedStructured = true;
                        }
                    }
                }

                // 3. Fallback: if the user dropped a folder containing loose RomFS assets directly, overlay onto baseRomfs
                if (!appliedStructured)
                {
                    CopyDirectoryMerged(modDir, baseRomfs);
                    App.RunOnUI(() => task.LogDetails += $"\n  ✓ Файлы из «{Path.GetFileName(modDir)}» наложены напрямую в RomFS");
                }
            }
        }

        private static void ApplyBinaryPatchToCodeBin(string patchFile, string codeBinPath, ProcessingTask task)
        {
            try
            {
                byte[] codeBytes = File.ReadAllBytes(codeBinPath);
                byte[] patchBytes = File.ReadAllBytes(patchFile);
                string ext = Path.GetExtension(patchFile).ToLowerInvariant();
                byte[]? patched = null;

                if (ext == ".ips")
                {
                    patched = ApplyIpsPatch(codeBytes, patchBytes);
                }
                else if (ext == ".bps")
                {
                    patched = ApplyBpsPatch(codeBytes, patchBytes);
                }

                if (patched != null && patched.Length > 0)
                {
                    File.WriteAllBytes(codeBinPath, patched);
                    App.RunOnUI(() => task.LogDetails += $"\n  🩹 Применен бинарный патч {Path.GetFileName(patchFile)} к code.bin ({patched.Length} байт)");
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[3DS Patch] Ошибка применения патча {Path.GetFileName(patchFile)}: {ex.Message}", LogLevel.Warning);
            }
        }

        public static byte[] ApplyIpsPatch(byte[] source, byte[] patch)
        {
            if (patch == null || patch.Length < 8) return source;
            string magic = Encoding.ASCII.GetString(patch, 0, 5);
            bool isIps32 = magic == "IPS32";
            if (magic != "PATCH" && !isIps32) return source;

            var output = new List<byte>(source);
            int pos = 5;
            int offsetLen = isIps32 ? 4 : 3;

            while (pos + offsetLen <= patch.Length)
            {
                if (!isIps32 && pos + 3 <= patch.Length && patch[pos] == 'E' && patch[pos + 1] == 'O' && patch[pos + 2] == 'F')
                    break;
                if (isIps32 && pos + 4 <= patch.Length && patch[pos] == 'E' && patch[pos + 1] == 'E' && patch[pos + 2] == 'O' && patch[pos + 3] == 'F')
                    break;

                int offset = isIps32
                    ? (patch[pos] << 24) | (patch[pos + 1] << 16) | (patch[pos + 2] << 8) | patch[pos + 3]
                    : (patch[pos] << 16) | (patch[pos + 1] << 8) | patch[pos + 2];
                pos += offsetLen;
                if (pos + 2 > patch.Length) break;

                int size = (patch[pos] << 8) | patch[pos + 1];
                pos += 2;

                if (size > 0)
                {
                    if (pos + size > patch.Length) break;
                    while (output.Count < offset + size) output.Add(0);
                    for (int i = 0; i < size; i++)
                        output[offset + i] = patch[pos + i];
                    pos += size;
                }
                else
                {
                    if (pos + 3 > patch.Length) break;
                    int rleSize = (patch[pos] << 8) | patch[pos + 1];
                    byte rleVal = patch[pos + 2];
                    pos += 3;
                    while (output.Count < offset + rleSize) output.Add(0);
                    for (int i = 0; i < rleSize; i++)
                        output[offset + i] = rleVal;
                }
            }
            return output.ToArray();
        }

        public static byte[] ApplyBpsPatch(byte[] source, byte[] patch)
        {
            if (patch == null || patch.Length < 16) return source;
            if (Encoding.ASCII.GetString(patch, 0, 4) != "BPS1") return source;

            int pos = 4;
            ulong ReadVarInt()
            {
                ulong data = 0, shift = 1;
                while (pos < patch.Length)
                {
                    byte x = patch[pos++];
                    data += (ulong)(x & 0x7F) * shift;
                    if ((x & 0x80) != 0) break;
                    shift <<= 7;
                    data += shift;
                }
                return data;
            }

            ulong sourceSize = ReadVarInt();
            ulong targetSize = ReadVarInt();
            ulong metadataSize = ReadVarInt();
            pos += (int)metadataSize;

            byte[] target = new byte[targetSize];
            int outputOffset = 0;
            long sourceRelOffset = 0;
            long targetRelOffset = 0;

            while (pos < patch.Length - 12 && outputOffset < target.Length)
            {
                ulong action = ReadVarInt();
                int length = (int)(action >> 2) + 1;
                int mode = (int)(action & 3);

                switch (mode)
                {
                    case 0: // SourceRead
                        for (int i = 0; i < length && outputOffset < target.Length; i++, outputOffset++)
                            target[outputOffset] = outputOffset < source.Length ? source[outputOffset] : (byte)0;
                        break;
                    case 1: // TargetRead
                        for (int i = 0; i < length && outputOffset < target.Length && pos < patch.Length - 12; i++, outputOffset++, pos++)
                            target[outputOffset] = patch[pos];
                        break;
                    case 2: // SourceCopy
                    {
                        ulong d = ReadVarInt();
                        sourceRelOffset += ((d & 1) != 0 ? -(long)(d >> 1) : (long)(d >> 1));
                        for (int i = 0; i < length && outputOffset < target.Length; i++, outputOffset++, sourceRelOffset++)
                            target[outputOffset] = (sourceRelOffset >= 0 && sourceRelOffset < source.Length) ? source[sourceRelOffset] : (byte)0;
                        break;
                    }
                    case 3: // TargetCopy
                    {
                        ulong d = ReadVarInt();
                        targetRelOffset += ((d & 1) != 0 ? -(long)(d >> 1) : (long)(d >> 1));
                        for (int i = 0; i < length && outputOffset < target.Length; i++, outputOffset++, targetRelOffset++)
                            target[outputOffset] = (targetRelOffset >= 0 && targetRelOffset < target.Length) ? target[targetRelOffset] : (byte)0;
                        break;
                    }
                }
            }
            return target;
        }

        private async Task Build3dsFromExtractedDirAsync(
            ProcessingTask task,
            string extractedDir,
            string tempDir,
            string targetOutFile,
            string outExt,
            bool isDual3ds,
            Nintendo3dsInfo? baseMeta,
            List<string>? extraCfaFiles,
            CancellationToken cancellationToken)
        {
            string makerom = GetMakeromPath();
            string threedstool = Get3dstoolPath();

            App.RunOnUI(() =>
            {
                task.Progress = 80;
                task.Status = $"Сборка итогового {outExt.ToUpper()}...";
                task.LogDetails += $"\n🔨 [3DS] Сборка контейнера {Path.GetFileName(targetOutFile)}...";
            });

            string exefsDir = Path.Combine(extractedDir, "exefs");
            NormalizeExefsFiles(exefsDir);
            if (task.CustomMetadata != null && task.CustomMetadata.HasModifications)
            {
                IconBadgeStudioService.ApplyCustom3dsMetadataAndBannerToExtractedDir(task.CustomMetadata, exefsDir, task);
            }

            string romfsBin = Path.Combine(tempDir, "rebuilt_romfs.bin");
            string romfsDir = Path.Combine(extractedDir, "romfs");
            if (Directory.Exists(romfsDir) && Directory.EnumerateFileSystemEntries(romfsDir).Any())
            {
                ModOptimizerService.CleanJunkFiles(romfsDir, task);
                if (App.Settings.Current.OptimizeModAssets)
                {
                    await ModOptimizerService.OptimizeTexturesAsync(romfsDir, task, cancellationToken);
                }
                if (File.Exists(threedstool))
                {
                    await RunProcessAsync(threedstool, $"-ctf romfs \"{romfsBin}\" --romfs-dir \"{romfsDir}\"", GetToolsDir(), cancellationToken);
                }
            }
            else if (File.Exists(Path.Combine(extractedDir, "romfs.bin")))
            {
                romfsBin = Path.Combine(extractedDir, "romfs.bin");
            }

            string outDir = Path.GetDirectoryName(targetOutFile) ?? "";
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                Directory.CreateDirectory(outDir);

            string exHeaderPath = Path.Combine(extractedDir, "exheader.bin");
            string exefsCode = Path.Combine(exefsDir, "code.bin");
            string exefsBanner = Path.Combine(exefsDir, "banner.bin");
            string exefsIcon = Path.Combine(exefsDir, "icon.bin");
            string logoPath = Path.Combine(extractedDir, "logo.bin");
            string plainRgnPath = Path.Combine(extractedDir, "plainrgn.bin");

            // Ensure CompressExefsCode bit (bit 0 at offset 0x0D) is cleared in exheader.bin because code.bin is decompressed
            string uniqueIdHex = "0x00300";
            string prodCode = "CTR-P-CTAP";
            string titleName = "3DS Application";

            if (baseMeta != null)
            {
                if (!string.IsNullOrWhiteSpace(baseMeta.ProductCode)) prodCode = baseMeta.ProductCode;
                if (!string.IsNullOrWhiteSpace(baseMeta.GameName)) titleName = baseMeta.GameName.Replace("\"", "");
                if (!string.IsNullOrWhiteSpace(baseMeta.TitleId) && baseMeta.TitleId.Length == 16)
                {
                    uniqueIdHex = "0x" + baseMeta.TitleId.Substring(8, 6).TrimStart('0');
                    if (uniqueIdHex == "0x") uniqueIdHex = "0x00300";
                }
            }

            if (File.Exists(exHeaderPath) && new FileInfo(exHeaderPath).Length >= 0x400)
            {
                byte[] exh = await File.ReadAllBytesAsync(exHeaderPath, cancellationToken);
                exh[0x0D] &= 0xFE; // Clear CompressExefsCode bit since code.bin was decompressed
                ulong progId = BitConverter.ToUInt64(exh, 0x1C8);
                if (progId != 0)
                {
                    string tidStr = progId.ToString("X16");
                    string uidPart = tidStr.Substring(8, 6).TrimStart('0');
                    if (!string.IsNullOrEmpty(uidPart)) uniqueIdHex = "0x" + uidPart;
                }
                await File.WriteAllBytesAsync(exHeaderPath, exh, cancellationToken);
            }

            // Generate RSF file required by makerom when building NCCH from extracted sections
            string rsfPath = Path.Combine(tempDir, "build.rsf");
            string rsfContent =
                "BasicInfo:\n" +
                $"  Title                   : \"{titleName}\"\n" +
                $"  ProductCode             : \"{prodCode}\"\n" +
                "  Logo                    : Nintendo\n" +
                "TitleInfo:\n" +
                "  Category                : Application\n" +
                $"  UniqueId                : {uniqueIdHex}\n" +
                "Option:\n" +
                "  UseOnSD                 : true\n" +
                "  EnableCompress          : false\n" +
                "  FreeProductCode         : true\n" +
                "  EnableCrypt             : false\n";
            await File.WriteAllTextAsync(rsfPath, rsfContent, Encoding.UTF8, cancellationToken);

            // Step A: Build Main CXI (Partition 0)
            string rebuiltCxi = Path.Combine(tempDir, "rebuilt_main.cxi");
            var ncchArgs = new StringBuilder();
            ncchArgs.Append($"-f ncch -target t -rsf \"{rsfPath}\" -desc app:4 -o \"{rebuiltCxi}\" ");
            if (File.Exists(exHeaderPath)) ncchArgs.Append($"-exheader \"{exHeaderPath}\" ");
            if (File.Exists(exefsCode)) ncchArgs.Append($"-code \"{exefsCode}\" ");
            if (File.Exists(exefsIcon)) ncchArgs.Append($"-icon \"{exefsIcon}\" ");
            if (File.Exists(exefsBanner)) ncchArgs.Append($"-banner \"{exefsBanner}\" ");
            if (File.Exists(logoPath) && new FileInfo(logoPath).Length > 0) ncchArgs.Append($"-logo \"{logoPath}\" ");
            if (File.Exists(plainRgnPath) && new FileInfo(plainRgnPath).Length > 0) ncchArgs.Append($"-plainrgn \"{plainRgnPath}\" ");
            if (File.Exists(romfsBin) && new FileInfo(romfsBin).Length > 0) ncchArgs.Append($"-romfs \"{romfsBin}\" ");

            await RunProcessAsync(makerom, ncchArgs.ToString(), tempDir, cancellationToken);

            if (!File.Exists(rebuiltCxi))
            {
                throw new Exception("Не удалось собрать основной раздел CXI через makerom.");
            }

            // Step B: If target is CXI, move directly; otherwise package into CCI (.3ds) and/or CIA
            if (outExt == "cxi" && !isDual3ds)
            {
                File.Copy(rebuiltCxi, targetOutFile, true);
            }
            else
            {
                var contentBuilder = new StringBuilder();
                contentBuilder.Append($"-content \"{rebuiltCxi}:0:0\" ");

                int contentIndex = 1;
                foreach (var baseCfa in Directory.GetFiles(extractedDir, "cfa_*.cfa").OrderBy(f => f))
                {
                    if (new FileInfo(baseCfa).Length >= 0x200)
                    {
                        contentBuilder.Append($"-content \"{baseCfa}:{contentIndex}:{contentIndex}\" ");
                        contentIndex++;
                    }
                }
                if (extraCfaFiles != null)
                {
                    foreach (var extraCfa in extraCfaFiles)
                    {
                        if (File.Exists(extraCfa) && new FileInfo(extraCfa).Length >= 0x200 && contentIndex < 8)
                        {
                            contentBuilder.Append($"-content \"{extraCfa}:{contentIndex}:{contentIndex}\" ");
                            contentIndex++;
                        }
                    }
                }

                string contentArgs = contentBuilder.ToString();
                string containerFormat = outExt == "3ds" ? "cci" : "cia";
                await RunProcessAsync(makerom, $"-f {containerFormat} -target t -o \"{targetOutFile}\" {contentArgs}", tempDir, cancellationToken);
            }

            if (File.Exists(targetOutFile))
            {
                if (outExt == "3ds")
                {
                    SmartShrinkService.Trim3dsCardImage(targetOutFile, task);
                }
                string targetCiaFile = Path.ChangeExtension(targetOutFile, ".cia");
                if (isDual3ds)
                {
                    App.RunOnUI(() =>
                    {
                        task.Progress = 90;
                        task.Status = "Сборка CIA...";
                        task.LogDetails += "\n🔄 [3DS Dual] Дополнительная генерация установочного пакета CIA...";
                    });
                    await RunProcessAsync(makerom, $"-f cia -target t -o \"{targetCiaFile}\" -content \"{rebuiltCxi}:0:0\"", tempDir, cancellationToken);
                    if (!File.Exists(targetCiaFile))
                    {
                        await RunProcessAsync(makerom, $"-ccitocia \"{targetOutFile}\" \"{targetCiaFile}\"", tempDir, cancellationToken);
                    }
                }

                long finalSize = new FileInfo(targetOutFile).Length;
                long finalCiaSize = (isDual3ds && File.Exists(targetCiaFile)) ? new FileInfo(targetCiaFile).Length : 0;

                App.RunOnUI(() =>
                {
                    task.Progress = 100;
                    task.Status = "Успешно";
                    task.IsRunning = false;
                    if (isDual3ds && File.Exists(targetCiaFile))
                    {
                        task.TargetSize = $"{ProcessingTask.FormatSize(finalSize)} / {ProcessingTask.FormatSize(finalCiaSize)}";
                        task.LogDetails += $"\n📦 [Форматы 3DS] Сохранены оба файла:\n  • {Path.GetFileName(targetOutFile)} ({ProcessingTask.FormatSize(finalSize)})\n  • {Path.GetFileName(targetCiaFile)} ({ProcessingTask.FormatSize(finalCiaSize)})";
                    }
                    else
                    {
                        task.TargetSize = ProcessingTask.FormatSize(finalSize);
                        task.LogDetails += $"\n✅ [Готово] Успешно собран: {Path.GetFileName(targetOutFile)} ({task.TargetSize})";
                    }
                });
            }
            else
            {
                throw new Exception("Не удалось создать итоговый 3DS файл через makerom.");
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

            bool isDual3ds = string.Equals(targetFormat, "3DS + CIA", StringComparison.OrdinalIgnoreCase);
            string inExt = Path.GetExtension(inputPath).ToLowerInvariant().TrimStart('.');
            string outExt = targetFormat.ToLowerInvariant().Replace(" (cci)", "").Trim();
            if (outExt == "3ds" || outExt == "cci" || isDual3ds) outExt = "3ds";
            else if (outExt == "cia") outExt = "cia";
            else outExt = "cxi";

            string outFileName = Path.GetFileNameWithoutExtension(inputPath) + "." + outExt;
            string outPath = Path.Combine(outputFolder, outFileName);

            if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);
            if (task.CustomMetadata != null && !string.IsNullOrWhiteSpace(task.CustomMetadata.CustomAudioWavPath))
            {
                await Build3dsMultiContentAsync(task, new List<string> { inputPath }, outPath, targetFormat, cancellationToken, forceRebuild: true);
                return;
            }
            if (Nintendo3dsCryptoService.Is3dsFileEncrypted(inputPath))
            {
                string decTemp = Path.Combine(Path.GetTempPath(), $"Storm3DS_DecConv_{Guid.NewGuid():N}");
                Directory.CreateDirectory(decTemp);
                TempCleanupService.RegisterActiveTempDirectory(decTemp);
                inputPath = await Nintendo3dsCryptoService.EnsureDecrypted3dsFileAsync(inputPath, decTemp, task, cancellationToken);
            }

            App.RunOnUI(() =>
            {
                task.IsRunning = true;
                task.Progress = 30;
                task.Status = $"Конвертация {inExt.ToUpper()} -> {targetFormat}...";
                task.LogDetails += $"\n🔄 [3DS Convert] Конвертация {Path.GetFileName(inputPath)} -> {targetFormat}...";
            });

            if (isDual3ds)
            {
                string target3ds = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(inputPath) + ".3ds");
                string targetCia = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(inputPath) + ".cia");

                if (inExt == "3ds" || inExt == "cci")
                {
                    if (!string.Equals(inputPath, target3ds, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(inputPath, target3ds, overwrite: true);
                    }
                    await RunProcessAsync(makerom, $"-ccitocia \"{target3ds}\" -o \"{targetCia}\"", outputFolder, cancellationToken);
                    if (!File.Exists(targetCia))
                    {
                        await RunProcessAsync(makerom, $"-ccitocia \"{target3ds}\" \"{targetCia}\"", outputFolder, cancellationToken);
                    }
                }
                else if (inExt == "cia")
                {
                    if (!string.Equals(inputPath, targetCia, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(inputPath, targetCia, overwrite: true);
                    }
                    await RunProcessAsync(makerom, $"-ciatocci \"{targetCia}\" -o \"{target3ds}\"", outputFolder, cancellationToken);
                    if (!File.Exists(target3ds))
                    {
                        await RunProcessAsync(makerom, $"-ciatocci \"{targetCia}\" \"{target3ds}\"", outputFolder, cancellationToken);
                    }
                }

                if (!File.Exists(target3ds) || !File.Exists(targetCia))
                {
                    await Build3dsMultiContentAsync(task, new List<string> { inputPath }, target3ds, targetFormat, cancellationToken, forceRebuild: true);
                    return;
                }

                if (File.Exists(target3ds))
                {
                    SmartShrinkService.Trim3dsCardImage(target3ds, task);
                }

                App.RunOnUI(() =>
                {
                    task.Progress = 100;
                    task.Status = "Успешно";
                    task.IsRunning = false;
                    long s3ds = File.Exists(target3ds) ? new FileInfo(target3ds).Length : 0;
                    long scia = File.Exists(targetCia) ? new FileInfo(targetCia).Length : 0;
                    task.TargetSize = $"{ProcessingTask.FormatSize(s3ds)} / {ProcessingTask.FormatSize(scia)}";
                    task.LogDetails += $"\n📦 [Форматы 3DS] Сохранены оба файла:\n  • {Path.GetFileName(target3ds)} ({ProcessingTask.FormatSize(s3ds)})\n  • {Path.GetFileName(targetCia)} ({ProcessingTask.FormatSize(scia)})";
                });
                return;
            }

            bool isSameFormat = (inExt == outExt) || (inExt == "cci" && outExt == "3ds");
            if (isSameFormat)
            {
                if (!string.Equals(Path.GetFullPath(inputPath), Path.GetFullPath(outPath), StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(inputPath, outPath, overwrite: true);
                }
            }
            else if ((inExt == "3ds" || inExt == "cci") && outExt == "cia")
            {
                await RunProcessAsync(makerom, $"-ccitocia \"{inputPath}\" -o \"{outPath}\"", outputFolder, cancellationToken);
                if (!File.Exists(outPath))
                {
                    await RunProcessAsync(makerom, $"-ccitocia \"{inputPath}\" \"{outPath}\"", outputFolder, cancellationToken);
                }
            }
            else if (inExt == "cia" && (outExt == "3ds" || outExt == "cci"))
            {
                await RunProcessAsync(makerom, $"-ciatocci \"{inputPath}\" -o \"{outPath}\"", outputFolder, cancellationToken);
                if (!File.Exists(outPath))
                {
                    await RunProcessAsync(makerom, $"-ciatocci \"{inputPath}\" \"{outPath}\"", outputFolder, cancellationToken);
                }
            }

            if (!File.Exists(outPath))
            {
                // Fallback to full extraction & rebuild (handles CXI -> 3DS/CIA, 3DS/CIA -> CXI, and non-standard headers)
                await Build3dsMultiContentAsync(task, new List<string> { inputPath }, outPath, targetFormat, cancellationToken, forceRebuild: true);
                return;
            }

            if (File.Exists(outPath))
            {
                if (task.CustomMetadata != null && task.CustomMetadata.HasModifications)
                {
                    IconBadgeStudioService.Patch3dsSmdhInPlace(outPath, task.CustomMetadata);
                }
                if (outExt == "3ds" || outExt == "cci")
                {
                    SmartShrinkService.Trim3dsCardImage(outPath, task);
                }
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
            if (!File.Exists(ctrtool)) throw new FileNotFoundException($"ctrtool не найден: {ctrtool}");

            string gameName = Path.GetFileNameWithoutExtension(inputPath);
            string outDir = Path.Combine(outputBaseFolder, $"{gameName}_extracted");
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            string tempDir = Path.Combine(Path.GetTempPath(), $"Storm3DS_Unpack_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);
            TempCleanupService.RegisterActiveTempDirectory(tempDir);

            try
            {
                App.RunOnUI(() =>
                {
                    task.IsRunning = true;
                    task.Progress = 20;
                    task.Status = "Распаковка 3DS...";
                    task.LogDetails += $"\n📦 [3DS Unpack] Извлечение разделов в {outDir}...";
                });

                await Extract3dsContainerToDirAsync(inputPath, outDir, tempDir, cancellationToken, extractSecondaryCfa: true);

                App.RunOnUI(() =>
                {
                    task.Progress = 100;
                    task.Status = "Успешно";
                    task.IsRunning = false;
                    task.LogDetails += $"\n✅ [Готово] 3DS контейнер распакован в: {outDir}";
                });
            }
            finally
            {
                TempCleanupService.ForceDeleteDirectory(tempDir);
            }
        }

        public async Task Pack3dsContainerAsync(
            ProcessingTask task,
            string inputFolder,
            string outputBaseFolder,
            string outputFileName,
            CancellationToken cancellationToken)
        {
            string makerom = GetMakeromPath();
            if (!File.Exists(makerom)) throw new FileNotFoundException($"makerom не найден: {makerom}");

            string targetFormat = task.TargetFormat ?? "3DS";
            bool isDual3ds = string.Equals(targetFormat, "3DS + CIA", StringComparison.OrdinalIgnoreCase);
            string outExt = targetFormat.ToLowerInvariant().Replace(" (cci)", "").Trim();
            if (outExt == "3ds" || outExt == "cci" || isDual3ds) outExt = "3ds";
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

                var meta = new Nintendo3dsInfo { GameName = Path.GetFileNameWithoutExtension(outName) };
                string iconPath = Path.Combine(inputFolder, "exefs", "icon.bin");
                if (File.Exists(iconPath) && new FileInfo(iconPath).Length >= 0x36C0)
                {
                    using var fs = new FileStream(iconPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new BinaryReader(fs);
                    TryReadSmdhAtOffset(fs, reader, 0, meta);
                }

                await Build3dsFromExtractedDirAsync(
                    task,
                    inputFolder,
                    tempDir,
                    outPath,
                    outExt,
                    isDual3ds,
                    meta,
                    null,
                    cancellationToken);
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
