using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public class Switch2DumpInfo
    {
        public string TitleId { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;
        public string GameRootDir { get; set; } = string.Empty;
        public string BaseDir { get; set; } = string.Empty;
        public string? UpdateDir { get; set; }
        public string MaxUpdateVersion { get; set; } = string.Empty;
        public List<string> DlcFolders { get; set; } = new();
        public int DlcCount => DlcFolders.Count;
        public byte[]? CoverBytes { get; set; }
        public string CompositionDescription { get; set; } = string.Empty;
        public string DisplayVersion => !string.IsNullOrEmpty(MaxUpdateVersion) ? $"v{MaxUpdateVersion}" : "v0";
    }

    public static class Switch2DumpHelper
    {
                public static string GenerateCanonicalSwitch2Name(Switch2DumpInfo sw2Info)
        {
            string baseVer = "1.0.0";
            string versionPart = !string.IsNullOrEmpty(sw2Info.MaxUpdateVersion)
                ? $"{baseVer} - {sw2Info.MaxUpdateVersion} - {sw2Info.TitleId}"
                : $"{baseVer} - {sw2Info.TitleId}";
            var compParts = new List<string> { "1G" };
            if (!string.IsNullOrEmpty(sw2Info.MaxUpdateVersion)) compParts.Add("1U");
            if (sw2Info.DlcCount > 0) compParts.Add($"{sw2Info.DlcCount}D");
            return $"{sw2Info.GameName} [WW] [RUS] ({versionPart}) ({string.Join("+", compParts)})";
        }

        public static bool IsSwitch2OrExtractedGameDumpDirectory(string dirPath)
        {
            if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath))
                return false;

            try
            {
                // Strict check: component directories (base, exefs, romfs, updates, dlc) are not dump roots
                string dirName = Path.GetFileName(dirPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (dirName.Equals("base", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals("exefs", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals("romfs", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals("updates", StringComparison.OrdinalIgnoreCase) ||
                    dirName.Equals("dlc", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                // Direct dump root must contain base/exefs or exefs with main.npdm or main
                if (File.Exists(Path.Combine(dirPath, "base", "exefs", "main.npdm")) ||
                    File.Exists(Path.Combine(dirPath, "base", "exefs", "main")))
                {
                    return true;
                }

                if (File.Exists(Path.Combine(dirPath, "exefs", "main.npdm")) ||
                    File.Exists(Path.Combine(dirPath, "exefs", "main")))
                {
                    return true;
                }

                // Strict structure check: base/exefs AND base/romfs
                if (Directory.Exists(Path.Combine(dirPath, "base", "exefs")) &&
                    Directory.Exists(Path.Combine(dirPath, "base", "romfs")))
                {
                    return true;
                }
            }
            catch { }

            return false;
        }

        public static List<string> FindAllSwitch2DumpRootDirectories(string inputPath, int maxDepth = 5)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(inputPath)) return results;

            // 1. If inputPath is a file: check ancestor directories up to 4 levels
            if (File.Exists(inputPath))
            {
                var cur = Directory.GetParent(inputPath);
                int depth = 0;
                while (cur != null && depth < 4)
                {
                    if (IsSwitch2OrExtractedGameDumpDirectory(cur.FullName))
                    {
                        results.Add(cur.FullName);
                        return results;
                    }
                    cur = cur.Parent;
                    depth++;
                }
                return results;
            }

            // 2. If inputPath is a directory
            if (Directory.Exists(inputPath))
            {
                // Check if inputPath is a subfolder inside a dump root (e.g. base, exefs, romfs, updates, dlc, 0)
                string dName = Path.GetFileName(inputPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).ToLowerInvariant();
                if (dName is "base" or "exefs" or "romfs" or "dlc" or "updates" or "0")
                {
                    var cur = Directory.GetParent(inputPath);
                    int depth = 0;
                    while (cur != null && depth < 4)
                    {
                        if (IsSwitch2OrExtractedGameDumpDirectory(cur.FullName))
                        {
                            results.Add(cur.FullName);
                            return results;
                        }
                        cur = cur.Parent;
                        depth++;
                    }
                }

                // If inputPath itself is a direct dump root
                if (IsSwitch2OrExtractedGameDumpDirectory(inputPath))
                {
                    results.Add(inputPath);
                    return results;
                }

                // Recursively traverse subdirectories up to maxDepth
                void ScanSubDirectories(string currentDir, int currentDepth)
                {
                    if (currentDepth > maxDepth) return;
                    string[] subDirs;
                    try
                    {
                        subDirs = Directory.GetDirectories(currentDir);
                    }
                    catch
                    {
                        return;
                    }

                    foreach (var sub in subDirs)
                    {
                        if (IsSwitch2OrExtractedGameDumpDirectory(sub))
                        {
                            if (!results.Contains(sub, StringComparer.OrdinalIgnoreCase))
                            {
                                results.Add(sub);
                            }
                            // CRITICAL: Once a dump root directory is found, do NOT recurse inside its base/, updates/, or dlc/
                            continue;
                        }

                        string name = Path.GetFileName(sub).ToLowerInvariant();
                        if (name is ".git" or "bin" or "obj" or "node_modules" or "romfs" or "exefs")
                        {
                            continue;
                        }

                        ScanSubDirectories(sub, currentDepth + 1);
                    }
                }

                ScanSubDirectories(inputPath, 1);
            }

            return results;
        }

        public static string CleanGameTitle(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "SWITCH 2 GAME";
            string s = raw.Replace('_', ' ');
            s = Regex.Replace(s, @"([a-zA-Z])(\d)", "$1 $2");
            s = Regex.Replace(s, @"(\d)([a-zA-Z])", "$1 $2");
            s = Regex.Replace(s, @"([a-z])([A-Z])", "$1 $2");
            s = Regex.Replace(s, @"([A-Z]+)([A-Z][a-z])", "$1 $2");
            s = Regex.Replace(s, @"\s+", " ").Trim();
            return s;
        }

        public static async Task<Switch2DumpInfo?> InspectSwitch2DumpDirectoryAsync(string dirPath)
        {
            if (string.IsNullOrWhiteSpace(dirPath)) return null;

            string targetDir = dirPath;
            if (!IsSwitch2OrExtractedGameDumpDirectory(targetDir))
            {
                var roots = FindAllSwitch2DumpRootDirectories(dirPath);
                if (roots.Count > 0)
                {
                    targetDir = roots[0];
                }
            }

            if (!Directory.Exists(targetDir)) return null;

            string npdmPath = Path.Combine(targetDir, "base", "exefs", "main.npdm");
            if (!File.Exists(npdmPath)) npdmPath = Path.Combine(targetDir, "exefs", "main.npdm");

            if (!File.Exists(npdmPath)) return null;

            byte[] npdmBytes = await File.ReadAllBytesAsync(npdmPath);
            if (npdmBytes.Length < 0x200 || npdmBytes[0] != 'M' || npdmBytes[1] != 'E' || npdmBytes[2] != 'T' || npdmBytes[3] != 'A')
                return null;

            int aci0Offset = -1;
            for (int i = 0; i <= npdmBytes.Length - 0x20; i++)
            {
                if (npdmBytes[i] == 'A' && npdmBytes[i + 1] == 'C' && npdmBytes[i + 2] == 'I' && npdmBytes[i + 3] == '0')
                {
                    aci0Offset = i;
                    break;
                }
            }
            if (aci0Offset == -1 || aci0Offset + 0x18 > npdmBytes.Length) return null;

            ulong tidVal = BitConverter.ToUInt64(npdmBytes, aci0Offset + 0x10);
            string titleId = $"{tidVal:X16}";

            string baseDir = Directory.Exists(Path.Combine(targetDir, "base")) ? Path.Combine(targetDir, "base") : targetDir;

            string updDir = Path.Combine(targetDir, "updates");
            string maxVer = "";
            int maxVerInt = 0;
            string? maxUpdPath = null;
            if (Directory.Exists(updDir))
            {
                foreach (var vDir in Directory.GetDirectories(updDir))
                {
                    string vName = Path.GetFileName(vDir);
                    string vClean = vName.TrimStart('v', 'V', 'p', 'P');
                    if (int.TryParse(vClean, out int vi) && vi > maxVerInt)
                    {
                        maxVerInt = vi;
                        maxVer = vClean;
                        maxUpdPath = vDir;
                    }
                    else if (string.IsNullOrEmpty(maxVer) && !string.IsNullOrWhiteSpace(vClean))
                    {
                        maxVer = vClean;
                        maxUpdPath = vDir;
                    }
                }
            }

            string dlcDir = Path.Combine(targetDir, "dlc");
            var dlcFolders = new List<string>();
            if (Directory.Exists(dlcDir))
            {
                foreach (var d in Directory.GetDirectories(dlcDir))
                {
                    try
                    {
                        if (Directory.EnumerateFileSystemEntries(d).Any())
                        {
                            dlcFolders.Add(d);
                        }
                    }
                    catch { }
                }
            }

            string compDesc = "[БАЗА";
            if (!string.IsNullOrEmpty(maxVer))
            {
                compDesc += $" + ПАТЧ v{maxVer}";
            }
            if (dlcFolders.Count > 0)
            {
                compDesc += $" + {dlcFolders.Count} DLC";
            }
            compDesc += "]";

            string cleanName = CleanGameTitle(Path.GetFileName(targetDir));
            byte[]? cover = await FetchCoverAsync(cleanName);

            return new Switch2DumpInfo
            {
                TitleId = titleId,
                GameName = cleanName,
                GameRootDir = targetDir,
                BaseDir = baseDir,
                UpdateDir = maxUpdPath,
                MaxUpdateVersion = maxVer,
                DlcFolders = dlcFolders,
                CoverBytes = cover,
                CompositionDescription = compDesc
            };
        }

        public static Task<Switch2DumpInfo?> AnalyzeDumpDirectory(string dirPath) => InspectSwitch2DumpDirectoryAsync(dirPath);
        public static Task<Switch2DumpInfo?> AnalyzeDumpDirectoryAsync(string dirPath) => InspectSwitch2DumpDirectoryAsync(dirPath);
        public static Task CompileSwitch2DumpAsync(
            ProcessingTask task,
            string sw2Dir,
            List<string> inputFiles,
            string outPath,
            CancellationToken ct) => CompileSwitch2DumpToContainerAsync(task, sw2Dir, inputFiles, outPath, ct);

        public static async Task<byte[]?> FetchCoverAsync(string gameTitle)
        {
            if (string.IsNullOrWhiteSpace(gameTitle)) return null;
            string safeKey = Regex.Replace(gameTitle.ToLowerInvariant(), @"[^a-z0-9\u0430-\u044f\u0451]+", "_").Trim('_');
            if (safeKey.Length > 48) safeKey = safeKey.Substring(0, 48);

            string cacheDir = Path.Combine(Path.GetTempPath(), "storm_switch_covers");
            Directory.CreateDirectory(cacheDir);
            string cachedPath = Path.Combine(cacheDir, $"{safeKey}.jpg");

            if (File.Exists(cachedPath) && new FileInfo(cachedPath).Length > 1024)
            {
                try { return await File.ReadAllBytesAsync(cachedPath); } catch { }
            }

            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                string encoded = Uri.EscapeDataString(gameTitle);

                string eshopUrl = $"https://search.nintendo-europe.com/en/select?q={encoded}&fq=type:GAME&rows=5&wt=json";
                var resp = await http.GetAsync(eshopUrl);
                if (resp.IsSuccessStatusCode)
                {
                    string json = await resp.Content.ReadAsStringAsync();
                    var m = Regex.Match(json, @"""image_url_sq_s""\s*:\s*""([^""]+)""");
                    if (!m.Success) m = Regex.Match(json, @"""wishlist_email_square_image_url_s""\s*:\s*""([^""]+)""");
                    if (!m.Success) m = Regex.Match(json, @"""image_url_h2x1_s""\s*:\s*""([^""]+)""");

                    if (m.Success)
                    {
                        string imgUrl = m.Groups[1].Value.Trim();
                        if (imgUrl.StartsWith("//")) imgUrl = "https:" + imgUrl;
                        else if (!imgUrl.StartsWith("http")) imgUrl = "https://" + imgUrl;

                        var imgBytes = await http.GetByteArrayAsync(imgUrl);
                        if (imgBytes.Length > 1024)
                        {
                            try { await File.WriteAllBytesAsync(cachedPath, imgBytes); } catch { }
                            return imgBytes;
                        }
                    }
                }
            }
            catch { }

            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
                http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                string encoded = Uri.EscapeDataString(gameTitle);
                string steamUrl = $"https://store.steampowered.com/api/storesearch/?term={encoded}&l=russian&cc=RU";
                var resp = await http.GetAsync(steamUrl);
                if (resp.IsSuccessStatusCode)
                {
                    string json = await resp.Content.ReadAsStringAsync();
                    var m = Regex.Match(json, @"""id""\s*:\s*(\d+)");
                    if (m.Success)
                    {
                        string appId = m.Groups[1].Value;
                        string bannerUrl = $"https://shared.akamai.steamstatic.com/store_item_assets/steam/apps/{appId}/library_600x900.jpg";
                        var imgBytes = await http.GetByteArrayAsync(bannerUrl);
                        if (imgBytes.Length > 1024)
                        {
                            try { await File.WriteAllBytesAsync(cachedPath, imgBytes); } catch { }
                            return imgBytes;
                        }
                    }
                }
            }
            catch { }

            return null;
        }

        public static async Task CompileSwitch2DumpToContainerAsync(
            ProcessingTask task,
            string sw2Dir,
            List<string> inputFiles,
            string outPath,
            CancellationToken ct)
        {
            var sw2Info = await InspectSwitch2DumpDirectoryAsync(sw2Dir);
            if (sw2Info == null)
            {
                throw new Exception($"Не удалось проанализировать дамп Nintendo Switch 2 в {sw2Dir}");
            }

            App.RunOnUI(() =>
            {
                task.IsRunning = true;
                task.Status = "Сборка контейнера Switch 2...";
                task.Progress = 5;
                task.LogDetails = $"🚀 [Switch 2] Запуск компиляции: {sw2Info.GameName} [{sw2Info.TitleId}]\n";
                if (!string.IsNullOrEmpty(sw2Info.MaxUpdateVersion))
                    task.LogDetails += $"  • Версия обновления: v{sw2Info.MaxUpdateVersion}\n";
                if (sw2Info.DlcCount > 0)
                    task.LogDetails += $"  • Дополнений (DLC): {sw2Info.DlcCount} шт.\n";
            });

            string outDir = Path.GetDirectoryName(outPath) ?? "";
            if (!string.IsNullOrEmpty(App.Settings.Current.OutputFolderSwitch2) && Directory.Exists(App.Settings.Current.OutputFolderSwitch2))
            {
                outDir = App.Settings.Current.OutputFolderSwitch2;
            }
            if (string.IsNullOrEmpty(outDir)) outDir = Directory.GetCurrentDirectory();
            Directory.CreateDirectory(outDir);

            DateTime startTime = DateTime.Now;
            string targetFormat = !string.IsNullOrEmpty(task.TargetFormat) ? task.TargetFormat.ToUpperInvariant() : (!string.IsNullOrEmpty(App.Settings.Current.Switch2DefaultFormat) ? App.Settings.Current.Switch2DefaultFormat.ToUpperInvariant() : "NSZ");

            string canonicalName = GenerateCanonicalSwitch2Name(sw2Info);
            string baseFileName = !string.IsNullOrWhiteSpace(task.OutputFileName) && !task.OutputFileName.Contains("[БАЗА")
                ? task.OutputFileName
                : canonicalName;

            App.RunOnUI(() => { task.OutputFileName = baseFileName; });

            string tempWorkDir = Path.Combine(Path.GetTempPath(), "StormSwitch2_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(tempWorkDir);
            TempCleanupService.RegisterActiveTempDirectory(tempWorkDir);

            try
            {
                // 1. Control NCA
                App.RunOnUI(() =>
                {
                    task.Progress = 15;
                    task.Status = "Сборка Control NCA...";
                    task.LogDetails += "[1/4] Сборка Control NCA (control.nacp на 16 языках + иконка 256×256)...\n";
                });

                var customMeta = task.CustomMetadata ?? task.RuTrackerPayload?.CustomMetadata;
                byte[]? iconBytes = customMeta?.CustomIconBytes ?? customMeta?.OriginalIconBytes ?? sw2Info.CoverBytes ?? await FetchCoverAsync(sw2Info.GameName);
                if (iconBytes == null || iconBytes.Length == 0)
                {
                    iconBytes = GenerateDefaultSwitchCoverBytes(sw2Info.GameName);
                }
                iconBytes = SmartShrinkService.OptimizeSwitchIconJpeg(iconBytes);

                string engTitle = !string.IsNullOrWhiteSpace(customMeta?.TitleNameEnglish) ? customMeta.TitleNameEnglish : sw2Info.GameName;
                string rusTitle = !string.IsNullOrWhiteSpace(customMeta?.TitleNameRussian) ? customMeta.TitleNameRussian : engTitle;
                string pubName = !string.IsNullOrWhiteSpace(customMeta?.Publisher) ? customMeta.Publisher : "Nintendo";

                string ctrlNcaPath = Path.Combine(tempWorkDir, "control.nca");
                BuildControlNca(sw2Info.TitleId, engTitle, pubName, sw2Info.MaxUpdateVersion.Length > 0 ? $"v{sw2Info.MaxUpdateVersion}" : "1.0.0", iconBytes, ctrlNcaPath, rusTitle);

                byte[] ctrlSha = ComputeSha256(ctrlNcaPath);
                string ctrlHex = BitConverter.ToString(ctrlSha, 0, 16).Replace("-", "").ToLowerInvariant();
                string finalCtrlNcaPath = Path.Combine(tempWorkDir, $"{ctrlHex}.nca");
                File.Move(ctrlNcaPath, finalCtrlNcaPath, true);
                long ctrlSize = new FileInfo(finalCtrlNcaPath).Length;

                // 2. Program NCA
                App.RunOnUI(() =>
                {
                    task.Progress = 35;
                    task.Status = "Сборка Program NCA (ExeFS + RomFS)...";
                    task.LogDetails += "[2/4] Сборка Program NCA (ExeFS PFS0 + RomFS IVFC)...\n";
                });

                string progNcaPath = Path.Combine(tempWorkDir, "program.nca");
                string baseExeFs = Path.Combine(sw2Info.BaseDir, "exefs");
                string? updExeFs = sw2Info.UpdateDir != null ? Path.Combine(sw2Info.UpdateDir, "exefs") : null;
                string baseRomFs = Path.Combine(sw2Info.BaseDir, "romfs");
                string? updRomFs = sw2Info.UpdateDir != null ? Path.Combine(sw2Info.UpdateDir, "romfs") : null;

                BuildProgramNca(sw2Info.TitleId, baseExeFs, updExeFs, baseRomFs, updRomFs, progNcaPath, p =>
                {
                    App.RunOnUI(() => { task.Progress = 35 + (p * 0.35); });
                }, ct);

                byte[] progSha = ComputeSha256(progNcaPath);
                string progHex = BitConverter.ToString(progSha, 0, 16).Replace("-", "").ToLowerInvariant();
                string finalProgNcaPath = Path.Combine(tempWorkDir, $"{progHex}.nca");
                File.Move(progNcaPath, finalProgNcaPath, true);
                long progSize = new FileInfo(finalProgNcaPath).Length;

                // 3. Meta NCA (CNMT)
                App.RunOnUI(() =>
                {
                    task.Progress = 75;
                    task.Status = "Сборка Meta NCA (.cnmt.nca)...";
                    task.LogDetails += "[3/4] Сборка Meta NCA (.cnmt.nca дескриптор)...\n";
                });

                string metaNcaPath = Path.Combine(tempWorkDir, "meta.nca");
                BuildMetaCnmtNca(sw2Info.TitleId, sw2Info.MaxUpdateVersion.Length > 0 ? sw2Info.MaxUpdateVersion : "0", progSha, progSha.Take(16).ToArray(), progSize, ctrlSha, ctrlSha.Take(16).ToArray(), ctrlSize, metaNcaPath);

                byte[] metaSha = ComputeSha256(metaNcaPath);
                string metaHex = BitConverter.ToString(metaSha, 0, 16).Replace("-", "").ToLowerInvariant();
                string finalMetaNcaPath = Path.Combine(tempWorkDir, $"{metaHex}.cnmt.nca");
                File.Move(metaNcaPath, finalMetaNcaPath, true);

                // 4. Pack into final NSP
                App.RunOnUI(() =>
                {
                    task.Progress = 85;
                    task.Status = "Финальная упаковка PFS0 (.nsp)...";
                    task.LogDetails += "[4/4] Финальная упаковка контейнера...\n";
                });

                var ncaFiles = new List<string> { finalMetaNcaPath, finalCtrlNcaPath, finalProgNcaPath };
                string finalNspPath = Path.Combine(outDir, baseFileName + ".nsp");
                BuildPfs0Container(ncaFiles, finalNspPath);

                // 5. Target format conversion/compression if needed
                if (targetFormat == "NSZ" || targetFormat == "NSP + NSZ")
                {
                    App.RunOnUI(() =>
                    {
                        task.Progress = 90;
                        task.Status = "Zstandard сжатие в NSZ...";
                        task.LogDetails += "⚡ [NCZBLOCK v2] Блочное Zstandard сжатие Program NCA в .nsz...\n";
                    });
                    await App.NszCompression.CompressToNszAsync(task, finalNspPath, outDir, ct);
                    if (targetFormat == "NSZ" && File.Exists(finalNspPath))
                    {
                        try { File.Delete(finalNspPath); } catch { }
                    }
                }
                else if (targetFormat == "XCI" || targetFormat == "XCZ" || targetFormat == "XCI + XCZ")
                {
                    App.RunOnUI(() =>
                    {
                        task.Progress = 90;
                        task.Status = "Конвертация в XCI картридж...";
                        task.LogDetails += "📦 [XCI Cartridge] Упаковка в HFS0 картридж...\n";
                    });
                    await App.SwitchFormat.ConvertContainerAsync(task, finalNspPath, outDir, "XCI", ct);
                    if (targetFormat == "XCZ" || targetFormat == "XCI + XCZ")
                    {
                        string xciPath = Path.Combine(outDir, baseFileName + ".xci");
                        if (File.Exists(xciPath))
                        {
                            await App.NszCompression.CompressToNszAsync(task, xciPath, outDir, ct);
                            if (targetFormat == "XCZ")
                            {
                                try { File.Delete(xciPath); } catch { }
                            }
                        }
                    }
                    if (!targetFormat.Contains("NSP") && File.Exists(finalNspPath))
                    {
                        try { File.Delete(finalNspPath); } catch { }
                    }
                }
                else if (targetFormat == "NUP")
                {
                    string finalNupPath = Path.Combine(outDir, baseFileName + ".nup");
                    App.RunOnUI(() =>
                    {
                        task.Progress = 90;
                        task.Status = "Сборка контейнера NUP v1.0...";
                        task.LogDetails += "📦 [NUP v1.0] Упаковка в контейнер Switch 2 .nup (Zstd + SHA256 дедупликация)...\n";
                    });
                    var nupProg = new Progress<(double pct, string status)>(p => App.RunOnUI(() => { task.Progress = 90 + (p.pct * 0.10); }));
                    await NupContainerService.PackSwitchDumpToNupAsync(sw2Dir, finalNupPath, App.Settings.Current.CompressionLevel, nupProg, ct);
                    if (File.Exists(finalNspPath))
                    {
                        try { File.Delete(finalNspPath); } catch { }
                    }
                }

                string finalOutPath = finalNspPath;
                if (targetFormat == "NSZ")
                {
                    string nsz = Path.Combine(outDir, baseFileName + ".nsz");
                    if (File.Exists(nsz)) finalOutPath = nsz;
                }
                else if (targetFormat == "XCZ")
                {
                    string xcz = Path.Combine(outDir, baseFileName + ".xcz");
                    if (File.Exists(xcz)) finalOutPath = xcz;
                }
                else if (targetFormat.Contains("XCI"))
                {
                    string xci = Path.Combine(outDir, baseFileName + ".xci");
                    if (File.Exists(xci)) finalOutPath = xci;
                }
                else if (targetFormat == "NUP")
                {
                    string nup = Path.Combine(outDir, baseFileName + ".nup");
                    if (File.Exists(nup)) finalOutPath = nup;
                }
                if (!File.Exists(finalOutPath))
                {
                    var candidates = Directory.GetFiles(outDir, baseFileName + ".*")
                        .Where(f => !f.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(f => new FileInfo(f).Length)
                        .ToList();
                    if (candidates.Count > 0) finalOutPath = candidates[0];
                }

                long outSize = File.Exists(finalOutPath) ? new FileInfo(finalOutPath).Length : 0;
                string formattedTargetSize = outSize > 0 ? ProcessingTask.FormatSize(outSize) : "-";

                App.RunOnUI(() =>
                {
                    task.OutputFolder = Path.GetDirectoryName(finalOutPath) ?? outDir;
                    task.OutputFileName = Path.GetFileNameWithoutExtension(finalOutPath);
                    task.TargetSize = formattedTargetSize;
                    if (task.SourceSizeBytes > 0 && outSize > 0)
                    {
                        long diff = task.SourceSizeBytes - outSize;
                        double percent = (double)diff / task.SourceSizeBytes * 100.0;
                        task.SizeDifference = $"{(diff > 0 ? "-" : "+")}{ProcessingTask.FormatSize(Math.Abs(diff))} ({Math.Abs(percent):F1}%)";
                    }
                    TimeSpan elapsed = DateTime.Now - startTime;
                    if (elapsed.TotalSeconds > 0 && task.SourceSizeBytes > 0)
                    {
                        double mbPerSec = (task.SourceSizeBytes / (1024.0 * 1024.0)) / elapsed.TotalSeconds;
                        task.Speed = $"{mbPerSec:F1} МБ/с";
                    }
                    task.Progress = 100;
                    task.Status = "Успешно";
                    task.IsRunning = false;
                    task.FinishedAt = DateTime.Now;
                    task.LogDetails += $"► Выходной файл: {finalOutPath} ({formattedTargetSize})\n";
                    task.LogDetails += $"✅ Сборка Nintendo Switch 2 успешно завершена!\n   Выходная папка: {outDir}\n";
                    HistoryService.AddToHistory(task);
                });
                App.Logger.Log($"[Switch 2] Сборка успешно завершена: {Path.GetFileName(finalOutPath)} ({formattedTargetSize})", LogLevel.Success);
            }
            finally
            {
                try { Directory.Delete(tempWorkDir, true); } catch { }
            }
        }

        private static readonly byte[] StandardNcaHeaderKey = new byte[]
        {
            0xAE, 0xA6, 0x82, 0xD2, 0x3F, 0x20, 0x21, 0x1E, 0x8F, 0x99, 0xB8, 0xD3, 0x24, 0x9D, 0x8E, 0xFC,
            0x02, 0x45, 0xA0, 0xFB, 0x16, 0x3A, 0x39, 0x6A, 0x98, 0xAC, 0xCA, 0x6E, 0x07, 0xF3, 0xBF, 0x18
        };

        private static readonly byte[] StandardEncryptedKeyArea0 = new byte[]
        {
            0x27, 0x3B, 0x7D, 0x0D, 0x7A, 0xC6, 0x22, 0x67, 0x3E, 0xDD, 0xFB, 0xB0, 0xE3, 0xF5, 0x4E, 0x88,
            0x0F, 0xD2, 0x93, 0xD8, 0x92, 0xD6, 0x0C, 0x9E, 0xDF, 0x27, 0xD8, 0x93, 0xC1, 0x57, 0x26, 0x05,
            0x14, 0x53, 0x8D, 0x27, 0x66, 0x65, 0x10, 0xB4, 0x94, 0x5A, 0xAA, 0x15, 0xBA, 0xEE, 0xEB, 0xE7,
            0xE1, 0x14, 0x98, 0x24, 0xBC, 0x6A, 0xF5, 0xD1, 0xA2, 0xCE, 0xB3, 0x79, 0x6C, 0x55, 0xE5, 0x58
        };

        private static void FinalizeAndEncryptNcaHeaderInPlace(byte[] header)
        {
            Array.Copy(StandardEncryptedKeyArea0, 0, header, 0x300, 0x40);

            var k1 = new byte[16];
            var k2 = new byte[16];
            Array.Copy(StandardNcaHeaderKey, 0, k1, 0, 16);
            Array.Copy(StandardNcaHeaderKey, 16, k2, 0, 16);

            using var aes1 = System.Security.Cryptography.Aes.Create();
            aes1.Mode = System.Security.Cryptography.CipherMode.ECB;
            aes1.Padding = System.Security.Cryptography.PaddingMode.None;
            aes1.Key = k1;
            using var enc1 = aes1.CreateEncryptor();

            using var aes2 = System.Security.Cryptography.Aes.Create();
            aes2.Mode = System.Security.Cryptography.CipherMode.ECB;
            aes2.Padding = System.Security.Cryptography.PaddingMode.None;
            aes2.Key = k2;
            using var enc2 = aes2.CreateEncryptor();

            var tweakIn = new byte[16];
            var tweak = new byte[16];
            var tmp = new byte[16];
            var outBlk = new byte[16];

            for (int sec = 0; sec < 6; sec++)
            {
                Array.Clear(tweakIn, 0, 16);
                int s = sec;
                for (int i = 15; i >= 0; i--)
                {
                    tweakIn[i] = (byte)(s & 0xFF);
                    s >>= 8;
                }
                enc2.TransformBlock(tweakIn, 0, 16, tweak, 0);

                int secOff = sec * 0x200;
                for (int b = 0; b < 32; b++)
                {
                    int blkOff = secOff + b * 16;
                    for (int i = 0; i < 16; i++)
                        tmp[i] = (byte)(header[blkOff + i] ^ tweak[i]);
                    enc1.TransformBlock(tmp, 0, 16, outBlk, 0);
                    for (int i = 0; i < 16; i++)
                        header[blkOff + i] = (byte)(outBlk[i] ^ tweak[i]);

                    int carry = 0;
                    for (int i = 0; i < 16; i++)
                    {
                        int nc = (tweak[i] >> 7) & 1;
                        tweak[i] = (byte)(((tweak[i] << 1) | carry) & 0xFF);
                        carry = nc;
                    }
                    if (carry != 0)
                        tweak[0] ^= 0x87;
                }
            }
        }

        public static void BuildControlNca(string titleId, string gameTitle, string publisher, string displayVersion, byte[] iconJpeg, string outPath, string? russianTitle = null)
        {
            byte[] nacp = BuildNacpBytes(titleId, gameTitle, publisher, displayVersion, russianTitle);
            var romfsFiles = new Dictionary<string, byte[]>
            {
                ["control.nacp"] = nacp
            };
            string[] langs = {
                "AmericanEnglish", "BritishEnglish", "Japanese", "French", "German",
                "LatinAmericanSpanish", "Spanish", "Italian", "Dutch", "CanadianFrench",
                "Portuguese", "Russian", "Korean", "TraditionalChinese", "SimplifiedChinese",
                "BrazilianPortuguese"
            };
            foreach (var l in langs)
            {
                romfsFiles[$"icon_{l}.dat"] = iconJpeg;
            }

            byte[] romfsImage = BuildRomFsImage(romfsFiles);
            var (fsHeader0, ivfcBody) = BuildIvfcSection(romfsImage);

            long totalNcaSize = 0x4000L + ivfcBody.Length;
            byte[] header = new byte[0x4000];

            // NCA3 at 0x200
            Encoding.ASCII.GetBytes("NCA3").CopyTo(header, 0x200);
            header[0x204] = 0; // Distribution = Download
            header[0x205] = 2; // ContentType = Control
            header[0x206] = 0; // CryptoType = None
            header[0x207] = 0; // KeyAreaKeyIndex = 0
            BitConverter.GetBytes(totalNcaSize).CopyTo(header, 0x208);
            ulong tidLong = Convert.ToUInt64(titleId, 16);
            BitConverter.GetBytes(tidLong).CopyTo(header, 0x210);
            BitConverter.GetBytes(0).CopyTo(header, 0x218); // ContentIndex = 0
            BitConverter.GetBytes(0x000E0000).CopyTo(header, 0x21C); // SdkAddonVersion

            // Section 0
            BitConverter.GetBytes((int)(0x4000 / 0x200)).CopyTo(header, 0x240);
            BitConverter.GetBytes((int)(totalNcaSize / 0x200)).CopyTo(header, 0x244);
            header[0x248] = 1;

            // Section 0 FS Header at 0x400
            fsHeader0.CopyTo(header, 0x400);

            using var sha = SHA256.Create();
            byte[] fsHeaderHash = sha.ComputeHash(header, 0x400, 0x200);
            fsHeaderHash.CopyTo(header, 0x280);
            FinalizeAndEncryptNcaHeaderInPlace(header);

            using var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024);
            fs.Write(header, 0, header.Length);
            fs.Write(ivfcBody, 0, ivfcBody.Length);
        }

        public static void BuildProgramNca(
            string titleId,
            string baseExeFsDir,
            string? updExeFsDir,
            string baseRomFsDir,
            string? updRomFsDir,
            string outPath,
            Action<double> onProgress,
            CancellationToken ct)
        {
            // 1. Gather ExeFS files
            var exefsMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(baseExeFsDir))
            {
                foreach (var f in Directory.GetFiles(baseExeFsDir))
                {
                    exefsMap[Path.GetFileName(f)] = f;
                }
            }
            if (!string.IsNullOrEmpty(updExeFsDir) && Directory.Exists(updExeFsDir))
            {
                foreach (var f in Directory.GetFiles(updExeFsDir))
                {
                    exefsMap[Path.GetFileName(f)] = f;
                }
            }

            var exefsEntries = new List<Tuple<string, byte[]>>();
            foreach (var kv in exefsMap.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (kv.Key.Equals(".listing", StringComparison.OrdinalIgnoreCase)) continue;
                byte[] data = File.ReadAllBytes(kv.Value);
                exefsEntries.Add(Tuple.Create(kv.Key, data));
            }
            byte[] rawExefsPfs0 = BuildMemoryPfs0(exefsEntries);
            int exefsAlignedLen = ((rawExefsPfs0.Length + 0x1FF) / 0x200) * 0x200;
            byte[] exefsAligned = new byte[exefsAlignedLen];
            Array.Copy(rawExefsPfs0, exefsAligned, rawExefsPfs0.Length);

            using var sha = SHA256.Create();
            byte[] exefsLayer1Hash = sha.ComputeHash(rawExefsPfs0);
            byte[] exefsMasterHash = sha.ComputeHash(exefsLayer1Hash);

            byte[] hAligned = new byte[0x200];
            Array.Copy(exefsLayer1Hash, 0, hAligned, 0, 32);
            byte[] totalSec0 = new byte[hAligned.Length + exefsAligned.Length];
            Array.Copy(hAligned, 0, totalSec0, 0, hAligned.Length);
            Array.Copy(exefsAligned, 0, totalSec0, hAligned.Length, exefsAligned.Length);

            // 2. Build RomFS
            var romfsMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(baseRomFsDir))
            {
                foreach (var f in Directory.GetFiles(baseRomFsDir, "*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(baseRomFsDir, f).Replace('\\', '/');
                    if (rel.EndsWith(".listing", StringComparison.OrdinalIgnoreCase)) continue;
                    romfsMap[rel] = f;
                }
            }
            if (!string.IsNullOrEmpty(updRomFsDir) && Directory.Exists(updRomFsDir))
            {
                foreach (var f in Directory.GetFiles(updRomFsDir, "*", SearchOption.AllDirectories))
                {
                    string rel = Path.GetRelativePath(updRomFsDir, f).Replace('\\', '/');
                    if (rel.EndsWith(".listing", StringComparison.OrdinalIgnoreCase)) continue;
                    romfsMap[rel] = f;
                }
            }

            // Stream RomFS to a temp file
            string tempRomfsFile = Path.Combine(Path.GetDirectoryName(outPath) ?? Path.GetTempPath(), $"tmp_rfs_{Guid.NewGuid():N}.bin");
            long romfsLv6Len = BuildStreamedRomFs(romfsMap, tempRomfsFile, onProgress, ct);

            try
            {
                var (fsHeader1, ivfcPrefixBytes) = BuildIvfcPrefixForStreamedRomfs(tempRomfsFile, romfsLv6Len);

                long sec0Offset = 0x4000L;
                long sec0Size = totalSec0.Length;
                long sec1Offset = sec0Offset + sec0Size;
                long sec1Size = ivfcPrefixBytes.Length + romfsLv6Len;
                long totalNcaSize = sec1Offset + sec1Size;

                byte[] ncaHdr = new byte[0x4000];
                Encoding.ASCII.GetBytes("NCA3").CopyTo(ncaHdr, 0x200);
                ncaHdr[0x204] = 0; // Download
                ncaHdr[0x205] = 0; // Program
                ncaHdr[0x206] = 0; // Crypto None
                ncaHdr[0x207] = 0;
                BitConverter.GetBytes(totalNcaSize).CopyTo(ncaHdr, 0x208);
                ulong tidLong = Convert.ToUInt64(titleId, 16);
                BitConverter.GetBytes(tidLong).CopyTo(ncaHdr, 0x210);
                BitConverter.GetBytes(0).CopyTo(ncaHdr, 0x218);
                BitConverter.GetBytes(0x000E0000).CopyTo(ncaHdr, 0x21C);

                // Section 0 = ExeFS
                BitConverter.GetBytes((int)(sec0Offset / 0x200)).CopyTo(ncaHdr, 0x240);
                BitConverter.GetBytes((int)((sec0Offset + sec0Size) / 0x200)).CopyTo(ncaHdr, 0x244);
                ncaHdr[0x248] = 1;

                int fs0 = 0x400;
                BitConverter.GetBytes((short)2).CopyTo(ncaHdr, fs0);
                ncaHdr[fs0 + 2] = 1; // PartitionFs
                ncaHdr[fs0 + 3] = 2; // HierarchicalSha256
                ncaHdr[fs0 + 4] = 1; // None
                Array.Copy(exefsMasterHash, 0, ncaHdr, fs0 + 8, 32);
                int exefsBlk = 0x1000;
                while (exefsBlk < rawExefsPfs0.Length) exefsBlk <<= 1;
                BitConverter.GetBytes(exefsBlk).CopyTo(ncaHdr, fs0 + 0x28);
                BitConverter.GetBytes(2).CopyTo(ncaHdr, fs0 + 0x2C);
                BitConverter.GetBytes(0L).CopyTo(ncaHdr, fs0 + 0x30);
                BitConverter.GetBytes(32L).CopyTo(ncaHdr, fs0 + 0x38);
                BitConverter.GetBytes(0x200L).CopyTo(ncaHdr, fs0 + 0x40);
                BitConverter.GetBytes((long)rawExefsPfs0.Length).CopyTo(ncaHdr, fs0 + 0x48);
                ncaHdr[fs0 + 0x140] = 2;
                byte[] fs0Sha = sha.ComputeHash(ncaHdr, fs0, 0x200);
                Array.Copy(fs0Sha, 0, ncaHdr, 0x280, 32);

                // Section 1 = RomFS
                BitConverter.GetBytes((int)(sec1Offset / 0x200)).CopyTo(ncaHdr, 0x250);
                BitConverter.GetBytes((int)((sec1Offset + sec1Size) / 0x200)).CopyTo(ncaHdr, 0x254);
                ncaHdr[0x258] = 1;

                int fs1 = 0x600;
                Array.Copy(fsHeader1, 0, ncaHdr, fs1, fsHeader1.Length);
                byte[] fs1Sha = sha.ComputeHash(ncaHdr, fs1, 0x200);
                Array.Copy(fs1Sha, 0, ncaHdr, 0x2A0, 32);
                FinalizeAndEncryptNcaHeaderInPlace(ncaHdr);

                using var outFs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024);
                outFs.Write(ncaHdr, 0, ncaHdr.Length);
                outFs.Write(totalSec0, 0, totalSec0.Length);
                outFs.Write(ivfcPrefixBytes, 0, ivfcPrefixBytes.Length);

                using (var rfsIn = new FileStream(tempRomfsFile, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024))
                {
                    rfsIn.CopyTo(outFs);
                }
            }
            finally
            {
                try { File.Delete(tempRomfsFile); } catch { }
            }
        }

        public static void BuildMetaCnmtNca(
            string titleId,
            string versionStr,
            byte[] progSha,
            byte[] progId,
            long progSize,
            byte[] ctrlSha,
            byte[] ctrlId,
            long ctrlSize,
            string outPath)
        {
            byte[] cnmtBytes = new byte[0x20 + 0x10 + 2 * 0x38 + 0x20];
            ulong tidVal = Convert.ToUInt64(titleId, 16);
            BitConverter.GetBytes(tidVal).CopyTo(cnmtBytes, 0);

            int verInt = int.TryParse(versionStr, out int vi) ? vi : 0;
            BitConverter.GetBytes(verInt).CopyTo(cnmtBytes, 0x08);
            cnmtBytes[0x0C] = 0x80; // Application
            BitConverter.GetBytes((short)0x10).CopyTo(cnmtBytes, 0x0E); // ExtendedHeaderSize
            BitConverter.GetBytes((short)2).CopyTo(cnmtBytes, 0x10); // ContentCount
            BitConverter.GetBytes((short)0).CopyTo(cnmtBytes, 0x12); // ContentMetaCount

            ulong patchTid = (tidVal & ~0xFFFUL) | 0x800UL;
            BitConverter.GetBytes(patchTid).CopyTo(cnmtBytes, 0x20);

            // Record 0: Program (type 1)
            int r0 = 0x30;
            Array.Copy(progSha, 0, cnmtBytes, r0, 32);
            Array.Copy(progId, 0, cnmtBytes, r0 + 32, 16);
            BitConverter.GetBytes((int)(progSize & 0xFFFFFFFFL)).CopyTo(cnmtBytes, r0 + 48);
            BitConverter.GetBytes((short)((progSize >> 32) & 0xFFFFL)).CopyTo(cnmtBytes, r0 + 52);
            cnmtBytes[r0 + 54] = 1; // Program

            // Record 1: Control (type 3)
            int r1 = 0x30 + 0x38;
            Array.Copy(ctrlSha, 0, cnmtBytes, r1, 32);
            Array.Copy(ctrlId, 0, cnmtBytes, r1 + 32, 16);
            BitConverter.GetBytes((int)(ctrlSize & 0xFFFFFFFFL)).CopyTo(cnmtBytes, r1 + 48);
            BitConverter.GetBytes((short)((ctrlSize >> 32) & 0xFFFFL)).CopyTo(cnmtBytes, r1 + 52);
            cnmtBytes[r1 + 54] = 3; // Control

            string cnmtName = $"Application_{titleId.ToLowerInvariant()}.cnmt";
            byte[] rawPfs0 = BuildMemoryPfs0(new List<Tuple<string, byte[]>> { Tuple.Create(cnmtName, cnmtBytes) });
            int pfs0AlignedLen = ((rawPfs0.Length + 0x1FF) / 0x200) * 0x200;
            byte[] pfs0Aligned = new byte[pfs0AlignedLen];
            Array.Copy(rawPfs0, pfs0Aligned, rawPfs0.Length);

            using var sha = SHA256.Create();
            byte[] layer1Hash = sha.ComputeHash(rawPfs0);
            byte[] masterHash = sha.ComputeHash(layer1Hash);
            byte[] hashAligned = new byte[0x200];
            Array.Copy(layer1Hash, 0, hashAligned, 0, 32);

            byte[] secRaw = new byte[hashAligned.Length + pfs0Aligned.Length];
            Array.Copy(hashAligned, 0, secRaw, 0, hashAligned.Length);
            Array.Copy(pfs0Aligned, 0, secRaw, hashAligned.Length, pfs0Aligned.Length);
            int secAlignedLen = ((secRaw.Length + 0x1FF) / 0x200) * 0x200;
            byte[] secAligned = new byte[secAlignedLen];
            Array.Copy(secRaw, secAligned, secRaw.Length);

            long totalNcaSize = 0x4000L + secAligned.Length;
            byte[] ncaHdr = new byte[0x4000];
            Encoding.ASCII.GetBytes("NCA3").CopyTo(ncaHdr, 0x200);
            ncaHdr[0x204] = 0; // Download
            ncaHdr[0x205] = 1; // Meta
            ncaHdr[0x206] = 0; // None
            BitConverter.GetBytes(totalNcaSize).CopyTo(ncaHdr, 0x208);
            BitConverter.GetBytes(tidVal).CopyTo(ncaHdr, 0x210);
            BitConverter.GetBytes(0).CopyTo(ncaHdr, 0x218); // ContentIndex = 0
            BitConverter.GetBytes(0x000E0000).CopyTo(ncaHdr, 0x21C); // SdkAddonVersion = 14.0.0

            // Section 0
            BitConverter.GetBytes((int)(0x4000 / 0x200)).CopyTo(ncaHdr, 0x240);
            BitConverter.GetBytes((int)(totalNcaSize / 0x200)).CopyTo(ncaHdr, 0x244);
            ncaHdr[0x248] = 1;

            int fs0 = 0x400;
            BitConverter.GetBytes((short)2).CopyTo(ncaHdr, fs0);
            ncaHdr[fs0 + 2] = 1; // PartitionFs
            ncaHdr[fs0 + 3] = 2; // HierarchicalSha256
            ncaHdr[fs0 + 4] = 1; // None
            Array.Copy(masterHash, 0, ncaHdr, fs0 + 8, 32);
            int blk = 0x1000;
            while (blk < rawPfs0.Length) blk <<= 1;
            BitConverter.GetBytes(blk).CopyTo(ncaHdr, fs0 + 0x28);
            BitConverter.GetBytes(2).CopyTo(ncaHdr, fs0 + 0x2C);
            BitConverter.GetBytes(0L).CopyTo(ncaHdr, fs0 + 0x30);
            BitConverter.GetBytes(32L).CopyTo(ncaHdr, fs0 + 0x38);
            BitConverter.GetBytes(0x200L).CopyTo(ncaHdr, fs0 + 0x40);
            BitConverter.GetBytes((long)rawPfs0.Length).CopyTo(ncaHdr, fs0 + 0x48);
            ncaHdr[fs0 + 0x140] = 2;
            byte[] fs0Sha = sha.ComputeHash(ncaHdr, fs0, 0x200);
            Array.Copy(fs0Sha, 0, ncaHdr, 0x280, 32);
            FinalizeAndEncryptNcaHeaderInPlace(ncaHdr);

            using var outFs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024);
            outFs.Write(ncaHdr, 0, ncaHdr.Length);
            outFs.Write(secAligned, 0, secAligned.Length);
        }

        public static void BuildPfs0Container(List<string> ncaFiles, string outPath)
        {
            var files = ncaFiles
                .Where(File.Exists)
                .OrderBy(f =>
                {
                    string name = Path.GetFileName(f).ToLowerInvariant();
                    if (name.EndsWith(".cnmt.nca")) return 0;
                    if (name.EndsWith(".nca") && name.Length == 36) return 2;
                    return 1;
                })
                .ToList();

            var strTable = new MemoryStream();
            var nameOffsets = new int[files.Count];
            for (int i = 0; i < files.Count; i++)
            {
                nameOffsets[i] = (int)strTable.Position;
                byte[] nb = Encoding.UTF8.GetBytes(Path.GetFileName(files[i]));
                strTable.Write(nb, 0, nb.Length);
                strTable.WriteByte(0);
            }
            while (strTable.Length % 0x20 != 0) strTable.WriteByte(0);
            byte[] strTableBytes = strTable.ToArray();

            int headerSize = 0x10;
            int entrySize = 0x18;

            using var outFs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024 * 1024);
            using var writer = new BinaryWriter(outFs);

            writer.Write(Encoding.ASCII.GetBytes("PFS0"));
            writer.Write(files.Count);
            writer.Write(strTableBytes.Length);
            writer.Write(0);

            long curDataOff = 0;
            for (int i = 0; i < files.Count; i++)
            {
                long sz = new FileInfo(files[i]).Length;
                writer.Write(curDataOff);
                writer.Write(sz);
                writer.Write(nameOffsets[i]);
                writer.Write(0);
                curDataOff += sz;
            }
            writer.Write(strTableBytes);

            byte[] buf = new byte[4 * 1024 * 1024];
            foreach (var f in files)
            {
                using var inFs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.Read, 4 * 1024 * 1024);
                int r;
                while ((r = inFs.Read(buf, 0, buf.Length)) > 0)
                {
                    writer.Write(buf, 0, r);
                }
            }
        }

        private static byte[] BuildMemoryPfs0(List<Tuple<string, byte[]>> entries)
        {
            var strTable = new MemoryStream();
            var nameOffsets = new int[entries.Count];
            for (int i = 0; i < entries.Count; i++)
            {
                nameOffsets[i] = (int)strTable.Position;
                byte[] nb = Encoding.UTF8.GetBytes(entries[i].Item1);
                strTable.Write(nb, 0, nb.Length);
                strTable.WriteByte(0);
            }
            while (strTable.Length % 0x20 != 0) strTable.WriteByte(0);
            byte[] strTableBytes = strTable.ToArray();

            int totalSize = 0x10 + entries.Count * 0x18 + strTableBytes.Length + entries.Sum(e => e.Item2.Length);
            byte[] outBuf = new byte[totalSize];

            Encoding.ASCII.GetBytes("PFS0").CopyTo(outBuf, 0);
            BitConverter.GetBytes(entries.Count).CopyTo(outBuf, 4);
            BitConverter.GetBytes(strTableBytes.Length).CopyTo(outBuf, 8);
            BitConverter.GetBytes(0).CopyTo(outBuf, 12);

            long curOff = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                int baseEntry = 0x10 + i * 0x18;
                BitConverter.GetBytes(curOff).CopyTo(outBuf, baseEntry);
                BitConverter.GetBytes((long)entries[i].Item2.Length).CopyTo(outBuf, baseEntry + 8);
                BitConverter.GetBytes(nameOffsets[i]).CopyTo(outBuf, baseEntry + 16);
                BitConverter.GetBytes(0).CopyTo(outBuf, baseEntry + 20);
                curOff += entries[i].Item2.Length;
            }

            int strTableStart = 0x10 + entries.Count * 0x18;
            Array.Copy(strTableBytes, 0, outBuf, strTableStart, strTableBytes.Length);

            int dataStart = strTableStart + strTableBytes.Length;
            int curDataPos = dataStart;
            for (int i = 0; i < entries.Count; i++)
            {
                Array.Copy(entries[i].Item2, 0, outBuf, curDataPos, entries[i].Item2.Length);
                curDataPos += entries[i].Item2.Length;
            }
            return outBuf;
        }

        private static byte[] BuildNacpBytes(string titleId, string title, string publisher, string version, string? russianTitle = null)
        {
            byte[] nacp = new byte[0x4000];
            string eng = title.Length > 0 ? title : "SWITCH 2 APPLICATION";
            string rus = !string.IsNullOrWhiteSpace(russianTitle) ? russianTitle : eng;
            byte[] tEngBytes = Encoding.UTF8.GetBytes(eng);
            byte[] tRusBytes = Encoding.UTF8.GetBytes(rus);
            byte[] pBytes = Encoding.UTF8.GetBytes(publisher.Length > 0 ? publisher : "Nintendo");

            for (int i = 0; i < 16; i++)
            {
                int baseOff = i * 0x300;
                byte[] nameBytes = (i == 11) ? tRusBytes : tEngBytes;
                Array.Copy(nameBytes, 0, nacp, baseOff, Math.Min(nameBytes.Length, 0x1FF));
                Array.Copy(pBytes, 0, nacp, baseOff + 0x200, Math.Min(pBytes.Length, 0xFF));
            }

            byte[] vBytes = Encoding.UTF8.GetBytes(version.Length > 0 ? version : "1.0.0");
            Array.Copy(vBytes, 0, nacp, 0x3060, Math.Min(vBytes.Length, 0x10));

            ulong tidVal = Convert.ToUInt64(titleId, 16);
            BitConverter.GetBytes(tidVal).CopyTo(nacp, 0x3038);

            nacp[0x30F0] = 0; // LicensedByNintendo
            nacp[0x30F4] = 2; // Auto
            return nacp;
        }

        private sealed class RomFsDirNode
        {
            public string Name = "";
            public int ParentIdx;
            public int SiblingIdx = -1;
            public int ChildDirIdx = -1;
            public int ChildFileIdx = -1;
            public int NextHashIdx = -1;
            public int Offset;
            public readonly Dictionary<string, int> SubDirs = new(StringComparer.Ordinal);
            public readonly List<int> Files = new();
        }

        private sealed class RomFsFileNode
        {
            public string Name = "";
            public int ParentDirIdx;
            public int SiblingIdx = -1;
            public int NextHashIdx = -1;
            public long DataOffset;
            public long DataSize;
            public int Offset;
            public string SourceFullPath = "";
            public byte[]? InMemoryData;
        }

        private static uint CalcRomFsHashTableCount(uint numEntries)
        {
            if (numEntries < 3) return 3;
            if (numEntries < 19) return numEntries | 1;
            uint count = numEntries;
            while (count % 2 == 0 || count % 3 == 0 || count % 5 == 0 || count % 7 == 0 ||
                   count % 11 == 0 || count % 13 == 0 || count % 17 == 0)
            {
                count++;
            }
            return count;
        }

        private static uint CalcRomFsPathHash(uint parentOffset, byte[] nameBytes)
        {
            uint hash = parentOffset ^ 123456789u;
            for (int i = 0; i < nameBytes.Length; i++)
            {
                hash = (hash >> 27) | (hash << 5);
                hash ^= nameBytes[i];
            }
            return hash;
        }

        private static (byte[] MetaPrefix, List<RomFsFileNode> OrderedFiles, long TotalImageSize) BuildHierarchicalRomFsMetadata(
            List<(string RelPath, long Size, string FullPath, byte[]? Data)> items)
        {
            var sorted = items
                .Select(x => (NormPath: x.RelPath.Replace('\\', '/').Trim('/'), x.Size, x.FullPath, x.Data))
                .Where(x => !string.IsNullOrEmpty(x.NormPath))
                .OrderBy(x => x.NormPath, StringComparer.Ordinal)
                .ToList();

            var dirs = new List<RomFsDirNode>
            {
                new RomFsDirNode { Name = "", ParentIdx = 0 }
            };
            var files = new List<RomFsFileNode>(sorted.Count);

            foreach (var item in sorted)
            {
                string[] parts = item.NormPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                int curDir = 0;
                for (int p = 0; p < parts.Length - 1; p++)
                {
                    string dName = parts[p];
                    if (!dirs[curDir].SubDirs.TryGetValue(dName, out int nextDir))
                    {
                        nextDir = dirs.Count;
                        dirs.Add(new RomFsDirNode
                        {
                            Name = dName,
                            ParentIdx = curDir
                        });
                        dirs[curDir].SubDirs[dName] = nextDir;
                    }
                    curDir = nextDir;
                }

                string fName = parts[^1];
                int fIdx = files.Count;
                files.Add(new RomFsFileNode
                {
                    Name = fName,
                    ParentDirIdx = curDir,
                    DataSize = item.Size,
                    SourceFullPath = item.FullPath,
                    InMemoryData = item.Data
                });
                dirs[curDir].Files.Add(fIdx);
            }

            for (int d = 0; d < dirs.Count; d++)
            {
                var subOrder = dirs[d].SubDirs.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => k.Value).ToList();
                if (subOrder.Count > 0)
                {
                    dirs[d].ChildDirIdx = subOrder[0];
                    for (int i = 0; i < subOrder.Count - 1; i++)
                        dirs[subOrder[i]].SiblingIdx = subOrder[i + 1];
                }

                var fOrder = dirs[d].Files;
                if (fOrder.Count > 0)
                {
                    dirs[d].ChildFileIdx = fOrder[0];
                    for (int i = 0; i < fOrder.Count - 1; i++)
                        files[fOrder[i]].SiblingIdx = fOrder[i + 1];
                }
            }

            int dirTableSize = 0;
            for (int d = 0; d < dirs.Count; d++)
            {
                dirs[d].Offset = dirTableSize;
                int nameLen = Encoding.UTF8.GetByteCount(dirs[d].Name);
                dirTableSize += 24 + ((nameLen + 3) & ~3);
            }

            int fileTableSize = 0;
            long dataCursor = 0;
            for (int f = 0; f < files.Count; f++)
            {
                files[f].Offset = fileTableSize;
                int nameLen = Encoding.UTF8.GetByteCount(files[f].Name);
                fileTableSize += 32 + ((nameLen + 3) & ~3);

                dataCursor = (dataCursor + 0x0FL) & ~0x0FL;
                files[f].DataOffset = dataCursor;
                dataCursor += files[f].DataSize;
            }

            uint dirHashCount = CalcRomFsHashTableCount((uint)dirs.Count);
            uint fileHashCount = CalcRomFsHashTableCount((uint)files.Count);
            int dirHashBytes = (int)(dirHashCount * 4);
            int fileHashBytes = (int)(fileHashCount * 4);

            var dirBuckets = new int[dirHashCount];
            Array.Fill(dirBuckets, -1);
            for (int d = 0; d < dirs.Count; d++)
            {
                uint pOff = (uint)dirs[dirs[d].ParentIdx].Offset;
                byte[] nBytes = Encoding.UTF8.GetBytes(dirs[d].Name);
                uint h = CalcRomFsPathHash(pOff, nBytes) % dirHashCount;
                dirs[d].NextHashIdx = dirBuckets[h];
                dirBuckets[h] = d;
            }

            var fileBuckets = new int[fileHashCount];
            Array.Fill(fileBuckets, -1);
            for (int f = 0; f < files.Count; f++)
            {
                uint pOff = (uint)dirs[files[f].ParentDirIdx].Offset;
                byte[] nBytes = Encoding.UTF8.GetBytes(files[f].Name);
                uint h = CalcRomFsPathHash(pOff, nBytes) % fileHashCount;
                files[f].NextHashIdx = fileBuckets[h];
                fileBuckets[h] = f;
            }

            long headerSize = 0x50;
            long dirHashOff = headerSize;
            long dirTableOff = dirHashOff + dirHashBytes;
            long fileHashOff = dirTableOff + dirTableSize;
            long fileTableOff = fileHashOff + fileHashBytes;
            long filePartOff = (fileTableOff + fileTableSize + 0x1FFL) & ~0x1FFL;

            var prefix = new byte[filePartOff];
            BitConverter.GetBytes(headerSize).CopyTo(prefix, 0x00);
            BitConverter.GetBytes(dirHashOff).CopyTo(prefix, 0x08);
            BitConverter.GetBytes((long)dirHashBytes).CopyTo(prefix, 0x10);
            BitConverter.GetBytes(dirTableOff).CopyTo(prefix, 0x18);
            BitConverter.GetBytes((long)dirTableSize).CopyTo(prefix, 0x20);
            BitConverter.GetBytes(fileHashOff).CopyTo(prefix, 0x28);
            BitConverter.GetBytes((long)fileHashBytes).CopyTo(prefix, 0x30);
            BitConverter.GetBytes(fileTableOff).CopyTo(prefix, 0x38);
            BitConverter.GetBytes((long)fileTableSize).CopyTo(prefix, 0x40);
            BitConverter.GetBytes(filePartOff).CopyTo(prefix, 0x48);

            for (int i = 0; i < dirBuckets.Length; i++)
            {
                uint val = dirBuckets[i] < 0 ? 0xFFFFFFFFu : (uint)dirs[dirBuckets[i]].Offset;
                BitConverter.GetBytes(val).CopyTo(prefix, (int)dirHashOff + i * 4);
            }

            for (int d = 0; d < dirs.Count; d++)
            {
                var node = dirs[d];
                int pos = (int)dirTableOff + node.Offset;
                uint parentOff = (uint)dirs[node.ParentIdx].Offset;
                uint siblingOff = node.SiblingIdx < 0 ? 0xFFFFFFFFu : (uint)dirs[node.SiblingIdx].Offset;
                uint childDirOff = node.ChildDirIdx < 0 ? 0xFFFFFFFFu : (uint)dirs[node.ChildDirIdx].Offset;
                uint childFileOff = node.ChildFileIdx < 0 ? 0xFFFFFFFFu : (uint)files[node.ChildFileIdx].Offset;
                uint nextHashOff = node.NextHashIdx < 0 ? 0xFFFFFFFFu : (uint)dirs[node.NextHashIdx].Offset;
                byte[] nBytes = Encoding.UTF8.GetBytes(node.Name);

                BitConverter.GetBytes(parentOff).CopyTo(prefix, pos + 0x00);
                BitConverter.GetBytes(siblingOff).CopyTo(prefix, pos + 0x04);
                BitConverter.GetBytes(childDirOff).CopyTo(prefix, pos + 0x08);
                BitConverter.GetBytes(childFileOff).CopyTo(prefix, pos + 0x0C);
                BitConverter.GetBytes(nextHashOff).CopyTo(prefix, pos + 0x10);
                BitConverter.GetBytes((uint)nBytes.Length).CopyTo(prefix, pos + 0x14);
                if (nBytes.Length > 0)
                    Array.Copy(nBytes, 0, prefix, pos + 0x18, nBytes.Length);
            }

            for (int i = 0; i < fileBuckets.Length; i++)
            {
                uint val = fileBuckets[i] < 0 ? 0xFFFFFFFFu : (uint)files[fileBuckets[i]].Offset;
                BitConverter.GetBytes(val).CopyTo(prefix, (int)fileHashOff + i * 4);
            }

            for (int f = 0; f < files.Count; f++)
            {
                var node = files[f];
                int pos = (int)fileTableOff + node.Offset;
                uint parentOff = (uint)dirs[node.ParentDirIdx].Offset;
                uint siblingOff = node.SiblingIdx < 0 ? 0xFFFFFFFFu : (uint)files[node.SiblingIdx].Offset;
                uint nextHashOff = node.NextHashIdx < 0 ? 0xFFFFFFFFu : (uint)files[node.NextHashIdx].Offset;
                byte[] nBytes = Encoding.UTF8.GetBytes(node.Name);

                BitConverter.GetBytes(parentOff).CopyTo(prefix, pos + 0x00);
                BitConverter.GetBytes(siblingOff).CopyTo(prefix, pos + 0x04);
                BitConverter.GetBytes(node.DataOffset).CopyTo(prefix, pos + 0x08);
                BitConverter.GetBytes(node.DataSize).CopyTo(prefix, pos + 0x10);
                BitConverter.GetBytes(nextHashOff).CopyTo(prefix, pos + 0x18);
                BitConverter.GetBytes((uint)nBytes.Length).CopyTo(prefix, pos + 0x1C);
                if (nBytes.Length > 0)
                    Array.Copy(nBytes, 0, prefix, pos + 0x20, nBytes.Length);
            }

            long totalSize = (filePartOff + dataCursor + 0x3FFFL) & ~0x3FFFL;
            return (prefix, files, totalSize);
        }

        private static byte[] BuildRomFsImage(Dictionary<string, byte[]> files)
        {
            var items = files
                .Select(kv => (RelPath: kv.Key, Size: (long)kv.Value.Length, FullPath: "", Data: (byte[]?)kv.Value))
                .ToList();
            var (prefix, orderedFiles, totalSize) = BuildHierarchicalRomFsMetadata(items);
            byte[] outBuf = new byte[totalSize];
            Array.Copy(prefix, 0, outBuf, 0, prefix.Length);
            int filePartOff = prefix.Length;
            foreach (var f in orderedFiles)
            {
                if (f.InMemoryData != null && f.InMemoryData.Length > 0)
                    Array.Copy(f.InMemoryData, 0, outBuf, filePartOff + (int)f.DataOffset, f.InMemoryData.Length);
            }
            return outBuf;
        }

        private static Tuple<byte[], byte[]> BuildIvfcSection(byte[] romfsBytes)
        {
            int blockSize = 0x4000;
            int alignedLen = ((romfsBytes.Length + blockSize - 1) / blockSize) * blockSize;
            byte[] lv6 = new byte[alignedLen];
            Array.Copy(romfsBytes, lv6, romfsBytes.Length);

            using var sha = SHA256.Create();
            byte[] lv5Raw = HashBlocks(lv6, blockSize, sha);
            byte[] lv5 = AlignBlock(lv5Raw, blockSize);
            byte[] lv4Raw = HashBlocks(lv5, blockSize, sha);
            byte[] lv4 = AlignBlock(lv4Raw, blockSize);
            byte[] lv3Raw = HashBlocks(lv4, blockSize, sha);
            byte[] lv3 = AlignBlock(lv3Raw, blockSize);
            byte[] lv2Raw = HashBlocks(lv3, blockSize, sha);
            byte[] lv2 = AlignBlock(lv2Raw, blockSize);
            byte[] lv1Raw = HashBlocks(lv2, blockSize, sha);
            byte[] lv1 = AlignBlock(lv1Raw, blockSize);
            byte[] masterHash = HashBlocks(lv1, blockSize, sha);

            byte[] ivfcPrefix = new byte[lv1.Length + lv2.Length + lv3.Length + lv4.Length + lv5.Length];
            int p = 0;
            Array.Copy(lv1, 0, ivfcPrefix, p, lv1.Length); p += lv1.Length;
            Array.Copy(lv2, 0, ivfcPrefix, p, lv2.Length); p += lv2.Length;
            Array.Copy(lv3, 0, ivfcPrefix, p, lv3.Length); p += lv3.Length;
            Array.Copy(lv4, 0, ivfcPrefix, p, lv4.Length); p += lv4.Length;
            Array.Copy(lv5, 0, ivfcPrefix, p, lv5.Length);

            byte[] fullBody = new byte[ivfcPrefix.Length + lv6.Length];
            Array.Copy(ivfcPrefix, 0, fullBody, 0, ivfcPrefix.Length);
            Array.Copy(lv6, 0, fullBody, ivfcPrefix.Length, lv6.Length);

            byte[] fsHdr = new byte[0x200];
            BitConverter.GetBytes((short)2).CopyTo(fsHdr, 0); // Version = 2
            fsHdr[2] = 0; // FsType::RomFs = 0
            fsHdr[3] = 3; // HashType::HierarchicalIntegrity (IVFC) = 3
            fsHdr[4] = 1; // EncryptionType::None = 1

            Encoding.ASCII.GetBytes("IVFC").CopyTo(fsHdr, 8);
            BitConverter.GetBytes(0x00020000).CopyTo(fsHdr, 0x0C);
            BitConverter.GetBytes(0x20).CopyTo(fsHdr, 0x10); // master_hash_size = 0x20
            BitConverter.GetBytes(7).CopyTo(fsHdr, 0x14);    // max_layers = 7 (6 levels + master)

            long[] offsets = { 0, lv1.Length, lv1.Length + lv2.Length, lv1.Length + lv2.Length + lv3.Length, lv1.Length + lv2.Length + lv3.Length + lv4.Length, (long)ivfcPrefix.Length };
            long[] sizes = { lv1.Length, lv2.Length, lv3.Length, lv4.Length, lv5.Length, lv6.Length };

            for (int i = 0; i < 6; i++)
            {
                int lOff = 0x18 + i * 0x18;
                BitConverter.GetBytes(offsets[i]).CopyTo(fsHdr, lOff);
                BitConverter.GetBytes(sizes[i]).CopyTo(fsHdr, lOff + 8);
                BitConverter.GetBytes(14).CopyTo(fsHdr, lOff + 16);
                BitConverter.GetBytes(0).CopyTo(fsHdr, lOff + 20);
            }
            Array.Copy(masterHash, 0, fsHdr, 0xC8, Math.Min(masterHash.Length, 32));
            fsHdr[0x140] = 2;

            return Tuple.Create(fsHdr, fullBody);
        }

        private static long BuildStreamedRomFs(Dictionary<string, string> files, string outTempFile, Action<double> onProgress, CancellationToken ct)
        {
            var items = new List<(string RelPath, long Size, string FullPath, byte[]? Data)>(files.Count);
            foreach (var kv in files)
            {
                long sz = new FileInfo(kv.Value).Length;
                items.Add((kv.Key, sz, kv.Value, null));
            }

            var (metaHeader, orderedFiles, totalRomFsSize) = BuildHierarchicalRomFsMetadata(items);
            long filePartitionOffset = metaHeader.Length;

            using var outFs = new FileStream(outTempFile, FileMode.Create, FileAccess.Write, FileShare.None, 2 * 1024 * 1024);
            outFs.Write(metaHeader, 0, metaHeader.Length);

            byte[] buf = new byte[1024 * 1024];
            long written = filePartitionOffset;

            for (int i = 0; i < orderedFiles.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var f = orderedFiles[i];
                long targetOff = filePartitionOffset + f.DataOffset;
                while (written < targetOff)
                {
                    int pad = (int)Math.Min(targetOff - written, buf.Length);
                    Array.Clear(buf, 0, pad);
                    outFs.Write(buf, 0, pad);
                    written += pad;
                }

                using var inFs = new FileStream(f.SourceFullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024);
                int r;
                while ((r = inFs.Read(buf, 0, buf.Length)) > 0)
                {
                    outFs.Write(buf, 0, r);
                    written += r;
                }
                if (i % 20 == 0)
                {
                    onProgress((double)i / Math.Max(1, orderedFiles.Count));
                }
            }

            while (written < totalRomFsSize)
            {
                int pad = (int)Math.Min(totalRomFsSize - written, buf.Length);
                Array.Clear(buf, 0, pad);
                outFs.Write(buf, 0, pad);
                written += pad;
            }

            return totalRomFsSize;
        }

        private static Tuple<byte[], byte[]> BuildIvfcPrefixForStreamedRomfs(string romfsFile, long romfsLen)
        {
            int blockSize = 0x4000;
            using var sha = SHA256.Create();

            var lv5List = new List<byte>();
            byte[] block = new byte[blockSize];
            using (var fs = new FileStream(romfsFile, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024))
            {
                int r;
                while ((r = fs.Read(block, 0, blockSize)) > 0)
                {
                    if (r < blockSize) Array.Clear(block, r, blockSize - r);
                    byte[] hash = sha.ComputeHash(block);
                    lv5List.AddRange(hash);
                }
            }
            byte[] lv5Raw = lv5List.ToArray();
            byte[] lv5 = AlignBlock(lv5Raw, blockSize);
            byte[] lv4Raw = HashBlocks(lv5, blockSize, sha);
            byte[] lv4 = AlignBlock(lv4Raw, blockSize);
            byte[] lv3Raw = HashBlocks(lv4, blockSize, sha);
            byte[] lv3 = AlignBlock(lv3Raw, blockSize);
            byte[] lv2Raw = HashBlocks(lv3, blockSize, sha);
            byte[] lv2 = AlignBlock(lv2Raw, blockSize);
            byte[] lv1Raw = HashBlocks(lv2, blockSize, sha);
            byte[] lv1 = AlignBlock(lv1Raw, blockSize);
            byte[] masterHash = HashBlocks(lv1, blockSize, sha);

            byte[] ivfcPrefix = new byte[lv1.Length + lv2.Length + lv3.Length + lv4.Length + lv5.Length];
            int p = 0;
            Array.Copy(lv1, 0, ivfcPrefix, p, lv1.Length); p += lv1.Length;
            Array.Copy(lv2, 0, ivfcPrefix, p, lv2.Length); p += lv2.Length;
            Array.Copy(lv3, 0, ivfcPrefix, p, lv3.Length); p += lv3.Length;
            Array.Copy(lv4, 0, ivfcPrefix, p, lv4.Length); p += lv4.Length;
            Array.Copy(lv5, 0, ivfcPrefix, p, lv5.Length);

            byte[] fsHdr = new byte[0x200];
            BitConverter.GetBytes((short)2).CopyTo(fsHdr, 0); // Version = 2
            fsHdr[2] = 0; // FsType::RomFs = 0
            fsHdr[3] = 3; // HashType::HierarchicalIntegrity (IVFC) = 3
            fsHdr[4] = 1; // EncryptionType::None = 1

            Encoding.ASCII.GetBytes("IVFC").CopyTo(fsHdr, 8);
            BitConverter.GetBytes(0x00020000).CopyTo(fsHdr, 0x0C);
            BitConverter.GetBytes(0x20).CopyTo(fsHdr, 0x10); // master_hash_size = 0x20
            BitConverter.GetBytes(7).CopyTo(fsHdr, 0x14);    // max_layers = 7 (6 levels + master)

            long[] offsets = { 0, lv1.Length, lv1.Length + lv2.Length, lv1.Length + lv2.Length + lv3.Length, lv1.Length + lv2.Length + lv3.Length + lv4.Length, (long)ivfcPrefix.Length };
            long[] sizes = { lv1.Length, lv2.Length, lv3.Length, lv4.Length, lv5.Length, romfsLen };

            for (int i = 0; i < 6; i++)
            {
                int lOff = 0x18 + i * 0x18;
                BitConverter.GetBytes(offsets[i]).CopyTo(fsHdr, lOff);
                BitConverter.GetBytes(sizes[i]).CopyTo(fsHdr, lOff + 8);
                BitConverter.GetBytes(14).CopyTo(fsHdr, lOff + 16);
                BitConverter.GetBytes(0).CopyTo(fsHdr, lOff + 20);
            }
            Array.Copy(masterHash, 0, fsHdr, 0xC8, Math.Min(masterHash.Length, 32));
            fsHdr[0x140] = 2;

            return Tuple.Create(fsHdr, ivfcPrefix);
        }

        private static byte[] HashBlocks(byte[] data, int blockSize, SHA256 sha)
        {
            int n = data.Length / blockSize;
            byte[] outBuf = new byte[n * 32];
            for (int i = 0; i < n; i++)
            {
                byte[] h = sha.ComputeHash(data, i * blockSize, blockSize);
                Array.Copy(h, 0, outBuf, i * 32, 32);
            }
            return outBuf;
        }

        private static byte[] AlignBlock(byte[] data, int blockSize)
        {
            int rem = data.Length % blockSize;
            if (rem == 0) return data;
            byte[] aligned = new byte[data.Length + (blockSize - rem)];
            Array.Copy(data, aligned, data.Length);
            return aligned;
        }

        private static byte[] ComputeSha256(string path)
        {
            using var sha = SHA256.Create();
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024);
            return sha.ComputeHash(fs);
        }

        private static byte[] GenerateDefaultSwitchCoverBytes(string title)
        {
            // Simple 256x256 fallback bitmap or JPEG placeholder
            byte[] placeholder = new byte[2048];
            placeholder[0] = 0xFF;
            placeholder[1] = 0xD8;
            placeholder[2] = 0xFF;
            placeholder[3] = 0xE0;
            return placeholder;
        }
    }

    public static class Switch2DumpCompilerService
    {
        public static bool IsSwitch2OrExtractedGameDumpDirectory(string dirPath)
            => Switch2DumpHelper.IsSwitch2OrExtractedGameDumpDirectory(dirPath);

        public static List<string> FindAllSwitch2DumpRootDirectories(string inputPath, int maxDepth = 5)
            => Switch2DumpHelper.FindAllSwitch2DumpRootDirectories(inputPath, maxDepth);

        public static Task<Switch2DumpInfo?> InspectSwitch2DumpDirectoryAsync(string dirPath)
            => Switch2DumpHelper.InspectSwitch2DumpDirectoryAsync(dirPath);

        public static Task<Switch2DumpInfo?> AnalyzeDumpDirectory(string dirPath)
            => Switch2DumpHelper.InspectSwitch2DumpDirectoryAsync(dirPath);

        public static Task<Switch2DumpInfo?> AnalyzeDumpDirectoryAsync(string dirPath)
            => Switch2DumpHelper.InspectSwitch2DumpDirectoryAsync(dirPath);

        public static Task CompileSwitch2DumpToContainerAsync(
            ProcessingTask task,
            string sw2Dir,
            List<string> inputFiles,
            string outPath,
            CancellationToken ct)
            => Switch2DumpHelper.CompileSwitch2DumpToContainerAsync(task, sw2Dir, inputFiles, outPath, ct);

        public static Task CompileSwitch2DumpAsync(
            ProcessingTask task,
            string sw2Dir,
            List<string> inputFiles,
            string outPath,
            CancellationToken ct)
            => Switch2DumpHelper.CompileSwitch2DumpToContainerAsync(task, sw2Dir, inputFiles, outPath, ct);
    }
}