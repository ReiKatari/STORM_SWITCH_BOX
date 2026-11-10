using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Buffers.Binary;
using System.Threading;
using System.Threading.Tasks;
using StormSwitchBox.Models;
using ZstdSharp;

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
        public string DisplayVersion => Switch2DumpHelper.DetectDisplayVersion(BaseDir, UpdateDir, MaxUpdateVersion);
    }

    public static class Switch2DumpHelper
    {
                public static string DetectDisplayVersion(string baseDir, string? updateDir, string maxUpdateVersion)
        {
            // 1) Check <updateDir>/control/control.nacp then <baseDir>/control/control.nacp
            var nacpPaths = new List<string>();
            if (!string.IsNullOrEmpty(updateDir))
            {
                nacpPaths.Add(Path.Combine(updateDir, "control", "control.nacp"));
            }
            if (!string.IsNullOrEmpty(baseDir))
            {
                nacpPaths.Add(Path.Combine(baseDir, "control", "control.nacp"));
            }

            foreach (var path in nacpPaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        if (fs.Length >= 0x3070)
                        {
                            fs.Seek(0x3060, SeekOrigin.Begin);
                            byte[] buf = new byte[16];
                            int read = fs.Read(buf, 0, 16);
                            if (read == 16)
                            {
                                string ver = Encoding.UTF8.GetString(buf).TrimEnd('\0').Trim();
                                if (!string.IsNullOrEmpty(ver)) return ver;
                            }
                        }
                    }
                    catch { }
                }
            }

            // 2) Check <updateDir>/romfs/System/AddressTable and <updateDir>/romfs/GameData (or <baseDir>/romfs/...)
            var regex = new Regex(@"Product\.(\d{3,4})\.", RegexOptions.IgnoreCase);
            var dirs = new List<string>();
            if (!string.IsNullOrEmpty(updateDir))
            {
                dirs.Add(Path.Combine(updateDir, "romfs", "System", "AddressTable"));
                dirs.Add(Path.Combine(updateDir, "romfs", "GameData"));
            }
            if (!string.IsNullOrEmpty(baseDir))
            {
                dirs.Add(Path.Combine(baseDir, "romfs", "System", "AddressTable"));
                dirs.Add(Path.Combine(baseDir, "romfs", "GameData"));
            }

            foreach (var dir in dirs)
            {
                if (Directory.Exists(dir))
                {
                    try
                    {
                        foreach (var filePath in Directory.EnumerateFiles(dir))
                        {
                            string fileName = Path.GetFileName(filePath);
                            var m = regex.Match(fileName);
                            if (m.Success)
                            {
                                string digits = m.Groups[1].Value;
                                if (digits.Length == 3)
                                {
                                    return $"{digits[0]}.{digits[1]}.{digits[2]}";
                                }
                                if (digits.Length == 4)
                                {
                                    return $"{digits[0]}.{digits[1]}.{digits.Substring(2)}";
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            // 3) Fallback
            if (string.IsNullOrEmpty(maxUpdateVersion)) return "1.0.0";
            if (int.TryParse(maxUpdateVersion, out int vNum) && vNum >= 65536)
            {
                return $"1.0.{vNum / 65536}";
            }
            return "1.0.0";
        }

        public static string GenerateCanonicalSwitch2Name(Switch2DumpInfo sw2Info)
        {
            string baseVer = DetectDisplayVersion(sw2Info.BaseDir, sw2Info.UpdateDir, sw2Info.MaxUpdateVersion);
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

                // Guard: never treat a LayeredFS mod/cheat/translation folder or a subfolder next to .nsp/.nsz/.xci/.xcz as a standalone dump
                string lowerDirName = dirName.ToLowerInvariant();
                if (lowerDirName.Contains("mod") || lowerDirName.Contains("мод") ||
                    lowerDirName.Contains("rus") || lowerDirName.Contains("рус") ||
                    lowerDirName.Contains("ukr") || lowerDirName.Contains("укр") ||
                    lowerDirName.Contains("cheat") || lowerDirName.Contains("чит") ||
                    lowerDirName.Contains("patch") || lowerDirName.Contains("патч") ||
                    lowerDirName.Contains("translation") || lowerDirName.Contains("перевод") ||
                    lowerDirName.Equals("atmosphere") || lowerDirName.Equals("contents") ||
                    lowerDirName.Equals("load") || lowerDirName.Equals("luma") || lowerDirName.Equals("titles"))
                {
                    return false;
                }

                var parentDir = Directory.GetParent(dirPath);
                if (parentDir != null)
                {
                    try
                    {
                        bool siblingContainersExist = Directory.EnumerateFiles(parentDir.FullName, "*.*", SearchOption.TopDirectoryOnly)
                            .Any(f => f.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) ||
                                      f.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) ||
                                      f.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) ||
                                      f.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase) ||
                                      f.EndsWith(".nup", StringComparison.OrdinalIgnoreCase));
                        if (siblingContainersExist)
                        {
                            return false;
                        }
                    }
                    catch { }
                }

                try
                {
                    bool dirContainersExist = Directory.EnumerateFiles(dirPath, "*.*", SearchOption.TopDirectoryOnly)
                        .Any(f => f.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) ||
                                  f.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) ||
                                  f.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) ||
                                  f.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase) ||
                                  f.EndsWith(".nup", StringComparison.OrdinalIgnoreCase));
                    if (dirContainersExist)
                    {
                        return false;
                    }
                }
                catch { }

                // Direct dump root with base/exefs/main.npdm + base/romfs
                if (File.Exists(Path.Combine(dirPath, "base", "exefs", "main.npdm")) &&
                    Directory.Exists(Path.Combine(dirPath, "base", "romfs")))
                {
                    return true;
                }

                // Full extracted dump with exefs/main.npdm + exefs/rtld + romfs (mods never have rtld alongside full exefs+romfs without containers)
                if (File.Exists(Path.Combine(dirPath, "exefs", "main.npdm")) &&
                    File.Exists(Path.Combine(dirPath, "exefs", "rtld")) &&
                    Directory.Exists(Path.Combine(dirPath, "romfs")))
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
                foreach (var d in Directory.GetDirectories(dlcDir).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                {
                    try
                    {
                        bool hasRealFiles = Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories)
                            .Any(f => !Path.GetFileName(f).Equals(".listing", StringComparison.OrdinalIgnoreCase));
                        if (hasRealFiles)
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
            byte[]? cover = null;
            var iconDirs = new[]
            {
                Path.Combine(targetDir, "control"),
                Path.Combine(baseDir, "control"),
                Path.Combine(targetDir, "basedata", "control")
            };
            foreach (var cDir in iconDirs)
            {
                if (cover != null) break;
                if (Directory.Exists(cDir))
                {
                    var iconFile = Directory.GetFiles(cDir, "icon_*.dat").FirstOrDefault();
                    if (iconFile != null && File.Exists(iconFile) && new FileInfo(iconFile).Length > 64)
                    {
                        try { cover = File.ReadAllBytes(iconFile); } catch { }
                    }
                }
            }
            if (cover == null)
            {
                var imgCandidates = new[]
                {
                    Path.Combine(targetDir, "icon.jpg"), Path.Combine(targetDir, "icon.png"),
                    Path.Combine(targetDir, "cover.jpg"), Path.Combine(targetDir, "cover.png"),
                    Path.Combine(baseDir, "icon.jpg"), Path.Combine(baseDir, "icon.png")
                };
                foreach (var img in imgCandidates)
                {
                    if (File.Exists(img) && new FileInfo(img).Length > 64)
                    {
                        try { cover = File.ReadAllBytes(img); break; } catch { }
                    }
                }
            }
            if (cover == null)
            {
                cover = await FetchCoverAsync(cleanName);
            }

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
            if (string.IsNullOrEmpty(outDir) && !string.IsNullOrEmpty(task.OutputFolder) && Directory.Exists(task.OutputFolder))
            {
                outDir = task.OutputFolder;
            }
            if (string.IsNullOrEmpty(outDir) && !string.IsNullOrEmpty(App.Settings.Current.OutputFolderSwitch2) && Directory.Exists(App.Settings.Current.OutputFolderSwitch2))
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

            string tempRoot = Path.Combine(Path.GetPathRoot(Path.GetFullPath(outDir)) ?? Path.GetTempPath(), "STORM_TMP");
            Directory.CreateDirectory(tempRoot);
            string tempWorkDir = Path.Combine(tempRoot, "StormSwitch2_" + Guid.NewGuid().ToString("N").Substring(0, 8));
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

                bool isSwitch2 = sw2Info.TitleId.StartsWith("0400", StringComparison.OrdinalIgnoreCase);
                string baseExeFs = Path.Combine(sw2Info.BaseDir, "exefs");
                string? updExeFs = sw2Info.UpdateDir != null ? Path.Combine(sw2Info.UpdateDir, "exefs") : null;
                uint sdkAddonVersion = DetectSdkAddonVersion(!string.IsNullOrEmpty(updExeFs) && Directory.Exists(updExeFs) ? updExeFs : baseExeFs, isSwitch2);

                string ctrlNcaPath = Path.Combine(tempWorkDir, "control.nca");
                string displayVer = DetectDisplayVersion(sw2Info.BaseDir, sw2Info.UpdateDir, sw2Info.MaxUpdateVersion);
                BuildControlNca(sw2Info.TitleId, engTitle, pubName, displayVer, iconBytes, ctrlNcaPath, rusTitle, sdkAddonVersion);

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
                string baseRomFs = Path.Combine(sw2Info.BaseDir, "romfs");
                string? updRomFs = sw2Info.UpdateDir != null ? Path.Combine(sw2Info.UpdateDir, "romfs") : null;

                BuildProgramNca(sw2Info.TitleId, baseExeFs, updExeFs, baseRomFs, updRomFs, progNcaPath, p =>
                {
                    App.RunOnUI(() => { task.Progress = 35 + (p * 0.35); });
                }, ct, sdkAddonVersion, normalizeAcidForLegacySwitch1: false);

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
                BuildMetaCnmtNca(sw2Info.TitleId, sw2Info.MaxUpdateVersion.Length > 0 ? sw2Info.MaxUpdateVersion : "0", progSha, progSha.Take(16).ToArray(), progSize, ctrlSha, ctrlSha.Take(16).ToArray(), ctrlSize, metaNcaPath, sdkAddonVersion);

                byte[] metaSha = ComputeSha256(metaNcaPath);
                string metaHex = BitConverter.ToString(metaSha, 0, 16).Replace("-", "").ToLowerInvariant();
                string finalMetaNcaPath = Path.Combine(tempWorkDir, $"{metaHex}.cnmt.nca");
                File.Move(metaNcaPath, finalMetaNcaPath, true);

                var ncaFiles = new List<string> { finalMetaNcaPath, finalCtrlNcaPath, finalProgNcaPath };

                // 3b. DLC Packaging
                if (sw2Info.DlcFolders.Count > 0)
                {
                    ulong baseTidVal = Convert.ToUInt64(sw2Info.TitleId, 16);
                    int dlcIdx = 0;
                    foreach (var dlcDir in sw2Info.DlcFolders.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    {
                        dlcIdx++;
                        App.RunOnUI(() =>
                        {
                            task.Progress = 75 + (int)((double)dlcIdx / sw2Info.DlcFolders.Count * 9);
                            task.Status = $"Сборка DLC {dlcIdx}/{sw2Info.DlcFolders.Count}...";
                            task.LogDetails += $"[DLC {dlcIdx}/{sw2Info.DlcFolders.Count}] Сборка Data NCA и AddOnContent CNMT...\n";
                        });

                        // 1. Resolve dlcTitleId
                        string dirName = Path.GetFileName(dlcDir);
                        string dlcTitleId = "";
                        if (dirName.Length == 16 && ulong.TryParse(dirName, System.Globalization.NumberStyles.HexNumber, null, out _))
                        {
                            dlcTitleId = dirName.ToUpperInvariant();
                        }
                        else
                        {
                            string npdm = Path.Combine(dlcDir, "exefs", "main.npdm");
                            if (!File.Exists(npdm))
                            {
                                var npdmCandidates = Directory.GetFiles(dlcDir, "main.npdm", SearchOption.AllDirectories);
                                if (npdmCandidates.Length > 0) npdm = npdmCandidates[0];
                            }
                            if (File.Exists(npdm))
                            {
                                byte[] npdmBytes = File.ReadAllBytes(npdm);
                                int aci0 = -1;
                                for (int i = 0; i <= npdmBytes.Length - 0x20; i++)
                                {
                                    if (npdmBytes[i] == 'A' && npdmBytes[i+1] == 'C' && npdmBytes[i+2] == 'I' && npdmBytes[i+3] == '0')
                                    {
                                        aci0 = i; break;
                                    }
                                }
                                if (aci0 != -1 && aci0 + 0x18 <= npdmBytes.Length)
                                {
                                    ulong t = BitConverter.ToUInt64(npdmBytes, aci0 + 0x10);
                                    if (t != 0) dlcTitleId = $"{t:X16}";
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(dlcTitleId))
                        {
                            ulong calculatedDlcTid = (baseTidVal & ~0xFFFUL) + 0x1000UL + (ulong)dlcIdx;
                            dlcTitleId = $"{calculatedDlcTid:X16}";
                        }

                        // 2. Resolve dlcRomFsRoot
                        string dlcRomFsRoot = dlcDir;
                        string directRomfs = Path.Combine(dlcDir, "romfs");
                        if (Directory.Exists(directRomfs))
                        {
                            dlcRomFsRoot = directRomfs;
                        }
                        else
                        {
                            var subDirs = Directory.GetDirectories(dlcDir);
                            string? foundSubRomfs = null;
                            foreach (var sub in subDirs)
                            {
                                string cand = Path.Combine(sub, "romfs");
                                if (Directory.Exists(cand))
                                {
                                    foundSubRomfs = cand;
                                    break;
                                }
                            }
                            if (foundSubRomfs != null)
                            {
                                dlcRomFsRoot = foundSubRomfs;
                            }
                        }

                        // 3. Build DLC Data NCA
                        string dlcDataNcaPath = Path.Combine(tempWorkDir, $"dlc_data_{dlcIdx}.nca");
                        BuildDlcDataNca(dlcTitleId, dlcRomFsRoot, dlcDataNcaPath, p => { }, ct, sdkAddonVersion);

                        byte[] dlcDataSha = ComputeSha256(dlcDataNcaPath);
                        string dlcDataHex = BitConverter.ToString(dlcDataSha, 0, 16).Replace("-", "").ToLowerInvariant();
                        string finalDlcDataNcaPath = Path.Combine(tempWorkDir, $"{dlcDataHex}.nca");
                        File.Move(dlcDataNcaPath, finalDlcDataNcaPath, true);
                        long dlcDataSize = new FileInfo(finalDlcDataNcaPath).Length;

                        // 4. Build DLC Meta CNMT NCA
                        string dlcMetaNcaPath = Path.Combine(tempWorkDir, $"dlc_meta_{dlcIdx}.nca");
                        BuildDlcMetaCnmtNca(dlcTitleId, sw2Info.TitleId, dlcDataSha, dlcDataSha.Take(16).ToArray(), dlcDataSize, dlcMetaNcaPath, sdkAddonVersion);

                        byte[] dlcMetaSha = ComputeSha256(dlcMetaNcaPath);
                        string dlcMetaHex = BitConverter.ToString(dlcMetaSha, 0, 16).Replace("-", "").ToLowerInvariant();
                        string finalDlcMetaNcaPath = Path.Combine(tempWorkDir, $"{dlcMetaHex}.cnmt.nca");
                        File.Move(dlcMetaNcaPath, finalDlcMetaNcaPath, true);

                        ncaFiles.Add(finalDlcDataNcaPath);
                        ncaFiles.Add(finalDlcMetaNcaPath);
                    }
                }

                // 4. Pack into final NSP
                App.RunOnUI(() =>
                {
                    task.Progress = 85;
                    task.Status = "Финальная упаковка PFS0 (.nsp)...";
                    task.LogDetails += "[4/4] Финальная упаковка контейнера...\n";
                });

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

        internal static readonly byte[] StandardNcaHeaderKey = new byte[]
        {
            0xAE, 0xAA, 0xB1, 0xCA, 0x08, 0xAD, 0xF9, 0xBE, 0xF1, 0x29, 0x91, 0xF3, 0x69, 0xE3, 0xC5, 0x67,
            0xD6, 0x88, 0x1E, 0x4E, 0x4A, 0x6A, 0x47, 0xA5, 0x1F, 0x6E, 0x48, 0x77, 0x06, 0x2D, 0x54, 0x2D
        };

        internal static readonly byte[] LegacyStormNcaHeaderKey = new byte[]
        {
            0xAE, 0xA6, 0x82, 0xD2, 0x3F, 0x20, 0x21, 0x1E, 0x8F, 0x99, 0xB8, 0xD3, 0x24, 0x9D, 0x8E, 0xFC,
            0x02, 0x45, 0xA0, 0xFB, 0x16, 0x3A, 0x39, 0x6A, 0x98, 0xAC, 0xCA, 0x6E, 0x07, 0xF3, 0xBF, 0x18
        };

        private static readonly byte[] StandardEncryptedKeyArea0 = new byte[]
        {
            0x8C, 0xF2, 0x50, 0x9F, 0x0E, 0x2B, 0xCC, 0x5F, 0xF7, 0x72, 0xCF, 0xDB, 0x77, 0xC8, 0xEA, 0x17,
            0x8C, 0xF2, 0x50, 0x9F, 0x0E, 0x2B, 0xCC, 0x5F, 0xF7, 0x72, 0xCF, 0xDB, 0x77, 0xC8, 0xEA, 0x17,
            0xCA, 0xF8, 0xF5, 0x17, 0x94, 0xF0, 0xDB, 0x98, 0xBB, 0xC7, 0xC6, 0xDC, 0xAB, 0x88, 0x73, 0x84,
            0x8C, 0xF2, 0x50, 0x9F, 0x0E, 0x2B, 0xCC, 0x5F, 0xF7, 0x72, 0xCF, 0xDB, 0x77, 0xC8, 0xEA, 0x17
        };

        /// <summary>
        /// Нормализует секцию ACID в main.npdm дампов Switch 2 (RSA-3072 смещение +0x300)
        /// под стандарт NCA3 / LibHac / Eden / Yuzu / Citron / Ryujinx (RSA-2048 смещение +0x200),
        /// устраняя ошибку «ACID Stream doesn't contain ACID section!» / «ROM зашифрован».
        /// </summary>
        public static uint DetectSdkAddonVersion(string? exefsDir, bool isSwitch2)
        {
            uint defaultVer = isSwitch2 ? 0x00140000u : 0x000E0000u;
            if (string.IsNullOrEmpty(exefsDir) || !Directory.Exists(exefsDir))
                return defaultVer;

            try
            {
                var files = Directory.GetFiles(exefsDir)
                    .Where(f => {
                        string n = Path.GetFileName(f).ToLowerInvariant();
                        return n.StartsWith("sdk") || n.StartsWith("main");
                    });

                foreach (var file in files)
                {
                    long len = new FileInfo(file).Length;
                    byte[] buf;
                    if (len > 8 * 1024 * 1024)
                    {
                        using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                        buf = new byte[Math.Min(2 * 1024 * 1024, (int)len)];
                        fs.Read(buf, 0, buf.Length);
                    }
                    else
                    {
                        buf = File.ReadAllBytes(file);
                    }

                    string text = Encoding.ASCII.GetString(buf);
                    var m = Regex.Match(text, @"(?:NintendoSDK|NintendoSdk_lib_|NintendoSdk_)(\d+)\.(\d+)\.(\d+)");
                    if (m.Success)
                    {
                        if (uint.TryParse(m.Groups[1].Value, out uint major) &&
                            uint.TryParse(m.Groups[2].Value, out uint minor) &&
                            uint.TryParse(m.Groups[3].Value, out uint micro))
                        {
                            return (major << 16) | (minor << 8) | micro;
                        }
                    }
                }
            }
            catch { }

            return defaultVer;
        }

        public static byte[] NormalizeSwitch2NpdmForNca3(byte[] npdmBytes, bool normalizeAcidForLegacySwitch1 = false)
        {
            if (!normalizeAcidForLegacySwitch1)
            {
                return npdmBytes;
            }

            if (npdmBytes == null || npdmBytes.Length < 0x100)
                return npdmBytes ?? Array.Empty<byte>();

            try
            {
                if (npdmBytes[0] != (byte)'M' || npdmBytes[1] != (byte)'E' || npdmBytes[2] != (byte)'T' || npdmBytes[3] != (byte)'A')
                    return npdmBytes;

                int aci0Off = BitConverter.ToInt32(npdmBytes, 0x70);
                int aci0Size = BitConverter.ToInt32(npdmBytes, 0x74);
                int acidOff = BitConverter.ToInt32(npdmBytes, 0x78);
                int acidSize = BitConverter.ToInt32(npdmBytes, 0x7C);

                if (acidOff <= 0 || acidSize <= 0x320 || acidOff + 0x320 > npdmBytes.Length)
                    return npdmBytes;

                // Если на +0x200 уже находится стандартный "ACID", ничего менять не нужно
                if (npdmBytes[acidOff + 0x200] == (byte)'A' &&
                    npdmBytes[acidOff + 0x201] == (byte)'C' &&
                    npdmBytes[acidOff + 0x202] == (byte)'I' &&
                    npdmBytes[acidOff + 0x203] == (byte)'D')
                {
                    return npdmBytes;
                }

                // В дампах Switch 2 (Ounce) перед "ACID" идёт RSA-3072 (0x180 подпись + 0x180 модуль = 0x300 байт)
                if (npdmBytes[acidOff + 0x300] == (byte)'A' &&
                    npdmBytes[acidOff + 0x301] == (byte)'C' &&
                    npdmBytes[acidOff + 0x302] == (byte)'I' &&
                    npdmBytes[acidOff + 0x303] == (byte)'D')
                {
                    int acidBodyStart = acidOff + 0x300;
                    int acidBodyLen = (aci0Off > acidBodyStart && aci0Off <= npdmBytes.Length)
                        ? (aci0Off - acidBodyStart)
                        : (npdmBytes.Length - acidBodyStart);

                    byte[] acidBody = new byte[acidBodyLen];
                    Array.Copy(npdmBytes, acidBodyStart, acidBody, 0, acidBodyLen);

                    int oldBodySize = BitConverter.ToInt32(acidBody, 0x04);
                    if (oldBodySize > 0x80)
                        BitConverter.GetBytes(oldBodySize - 0x80).CopyTo(acidBody, 0x04);

                    // Сдвигаем внутренние смещения FAC, SAC, KAC внутри ACID с базы 0x300 на базу 0x200 (-0x100)
                    for (int internalOff = 0x20; internalOff <= 0x30; internalOff += 8)
                    {
                        int subOff = BitConverter.ToInt32(acidBody, internalOff);
                        if (subOff >= 0x300)
                            BitConverter.GetBytes(subOff - 0x100).CopyTo(acidBody, internalOff);
                    }

                    using var ms = new MemoryStream();
                    ms.Write(npdmBytes, 0, acidOff);
                    // 0x100 байт подписи + 0x100 байт модуля (2048-бит)
                    ms.Write(npdmBytes, acidOff, 0x100);
                    ms.Write(npdmBytes, acidOff + 0x180, 0x100);
                    ms.Write(acidBody, 0, acidBody.Length);

                    int newAci0Off = (int)ms.Position;
                    if (aci0Off > 0 && aci0Off < npdmBytes.Length)
                    {
                        ms.Write(npdmBytes, aci0Off, npdmBytes.Length - aci0Off);
                    }

                    byte[] normalized = ms.ToArray();
                    BitConverter.GetBytes(newAci0Off).CopyTo(normalized, 0x70);
                    BitConverter.GetBytes(0x200 + acidBody.Length).CopyTo(normalized, 0x7C);
                    return normalized;
                }
            }
            catch { }

            return npdmBytes;
        }

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

        public static void BuildControlNca(string titleId, string gameTitle, string publisher, string displayVersion, byte[] iconJpeg, string outPath, string? russianTitle = null, uint sdkAddonVersion = 0x00140000u)
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
            BitConverter.GetBytes(sdkAddonVersion).CopyTo(header, 0x21C); // SdkAddonVersion

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
            CancellationToken ct,
            uint sdkAddonVersion = 0x00140000u,
            bool normalizeAcidForLegacySwitch1 = false,
            Stream? nupStream = null,
            Dictionary<string, string>? prebuiltRomfsMap = null)
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
                if (kv.Key.Equals("main.npdm", StringComparison.OrdinalIgnoreCase))
                {
                    if (normalizeAcidForLegacySwitch1)
                    {
                        data = NormalizeSwitch2NpdmForNca3(data, true);
                    }
                }
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
            var romfsMap = prebuiltRomfsMap != null ? prebuiltRomfsMap : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (prebuiltRomfsMap == null)
            {
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
            }

            // Pre-check free disk space before streaming RomFS
            long estimatedRomfsBytes = 0;
            foreach (var f in romfsMap.Values)
            {
                if (f.StartsWith("__NUP__|", StringComparison.Ordinal))
                {
                    string[] parts = f.Split('|');
                    if (parts.Length >= 5 && long.TryParse(parts[4], out long sz))
                        estimatedRomfsBytes += sz;
                }
                else
                {
                    try { if (File.Exists(f)) estimatedRomfsBytes += new FileInfo(f).Length; } catch { }
                }
            }
            string tempRomfsFile = Path.Combine(Path.GetDirectoryName(outPath) ?? Path.GetTempPath(), $"tmp_rfs_{Guid.NewGuid():N}.bin");
            string tempDir = Path.GetDirectoryName(tempRomfsFile) ?? Path.GetTempPath();
            var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(tempDir)) ?? tempDir);
            long requiredSpace = estimatedRomfsBytes + (512L << 20);
            if (drive.AvailableFreeSpace < requiredSpace)
            {
                throw new IOException($"Недостаточно свободного места на диске {drive.Name} для создания Program RomFS: доступно {drive.AvailableFreeSpace / (1024 * 1024)} МБ, требуется {requiredSpace / (1024 * 1024)} МБ.");
            }

            long romfsLv6Len = BuildStreamedRomFs(romfsMap, tempRomfsFile, onProgress, ct, nupStream);

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
                BitConverter.GetBytes(sdkAddonVersion).CopyTo(ncaHdr, 0x21C);

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
            string outPath,
            uint sdkAddonVersion = 0x00140000u)
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
            BitConverter.GetBytes(sdkAddonVersion).CopyTo(ncaHdr, 0x21C); // SdkAddonVersion

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

        public static void BuildDlcDataNca(
            string dlcTitleId,
            string dlcRomFsRoot,
            string outPath,
            Action<double> onProgress,
            CancellationToken ct,
            uint sdkAddonVersion = 0x00140000u,
            Stream? nupStream = null,
            Dictionary<string, string>? prebuiltRomfsMap = null)
        {
            var romfsMap = prebuiltRomfsMap != null ? prebuiltRomfsMap : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (prebuiltRomfsMap == null)
            {
                if (Directory.Exists(dlcRomFsRoot))
                {
                    foreach (var f in Directory.GetFiles(dlcRomFsRoot, "*", SearchOption.AllDirectories))
                    {
                        string rel = Path.GetRelativePath(dlcRomFsRoot, f).Replace('\\', '/');
                        if (rel.Equals(".listing", StringComparison.OrdinalIgnoreCase) ||
                            rel.EndsWith("/.listing", StringComparison.OrdinalIgnoreCase)) continue;
                        romfsMap[rel] = f;
                    }
                }
            }

            long estimatedRomfsBytes = 0;
            foreach (var f in romfsMap.Values)
            {
                if (f.StartsWith("__NUP__|", StringComparison.Ordinal))
                {
                    string[] parts = f.Split('|');
                    if (parts.Length >= 5 && long.TryParse(parts[4], out long sz))
                        estimatedRomfsBytes += sz;
                }
                else
                {
                    try { if (File.Exists(f)) estimatedRomfsBytes += new FileInfo(f).Length; } catch { }
                }
            }

            string tempRomfsFile = Path.Combine(Path.GetDirectoryName(outPath) ?? Path.GetTempPath(), $"tmp_rfs_{Guid.NewGuid():N}.bin");
            string tempDir = Path.GetDirectoryName(tempRomfsFile) ?? Path.GetTempPath();
            var drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(tempDir)) ?? tempDir);
            long requiredSpace = estimatedRomfsBytes + (512L << 20);
            if (drive.AvailableFreeSpace < requiredSpace)
            {
                throw new IOException($"Недостаточно свободного места на диске {drive.Name} для создания DLC RomFS: доступно {drive.AvailableFreeSpace / (1024 * 1024)} МБ, требуется {requiredSpace / (1024 * 1024)} МБ.");
            }

            long romfsLv6Len = BuildStreamedRomFs(romfsMap, tempRomfsFile, onProgress, ct, nupStream);

            try
            {
                var (fsHeader0, ivfcPrefixBytes) = BuildIvfcPrefixForStreamedRomfs(tempRomfsFile, romfsLv6Len);

                long sec0Offset = 0x4000L;
                long sec0Size = ivfcPrefixBytes.Length + romfsLv6Len;
                long totalNcaSize = sec0Offset + sec0Size;

                byte[] ncaHdr = new byte[0x4000];
                Encoding.ASCII.GetBytes("NCA3").CopyTo(ncaHdr, 0x200);
                ncaHdr[0x204] = 0; // Download
                ncaHdr[0x205] = 5; // PublicData (5) - standard for AOC RomFS DLC
                ncaHdr[0x206] = 0; // Crypto None
                ncaHdr[0x207] = 0;
                BitConverter.GetBytes(totalNcaSize).CopyTo(ncaHdr, 0x208);
                ulong tidLong = Convert.ToUInt64(dlcTitleId, 16);
                BitConverter.GetBytes(tidLong).CopyTo(ncaHdr, 0x210);
                BitConverter.GetBytes(0).CopyTo(ncaHdr, 0x218);
                BitConverter.GetBytes(sdkAddonVersion).CopyTo(ncaHdr, 0x21C);

                // Section 0 = RomFS (IVFC)
                BitConverter.GetBytes((int)(sec0Offset / 0x200)).CopyTo(ncaHdr, 0x240);
                BitConverter.GetBytes((int)((sec0Offset + sec0Size) / 0x200)).CopyTo(ncaHdr, 0x244);
                ncaHdr[0x248] = 1;

                // FS Header 0 at 0x400
                Array.Copy(fsHeader0, 0, ncaHdr, 0x400, fsHeader0.Length);
                using var sha = SHA256.Create();
                byte[] fs0Sha = sha.ComputeHash(ncaHdr, 0x400, 0x200);
                Array.Copy(fs0Sha, 0, ncaHdr, 0x280, 32);

                FinalizeAndEncryptNcaHeaderInPlace(ncaHdr);

                using var outFs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024);
                outFs.Write(ncaHdr, 0, ncaHdr.Length);
                outFs.Write(ivfcPrefixBytes, 0, ivfcPrefixBytes.Length);

                using (var rfsIn = new FileStream(tempRomfsFile, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024))
                {
                    rfsIn.CopyTo(outFs);
                }
            }
            finally
            {
                try { if (File.Exists(tempRomfsFile)) File.Delete(tempRomfsFile); } catch { }
            }
        }

        public static void BuildDlcMetaCnmtNca(
            string dlcTitleId,
            string baseAppTitleId,
            byte[] dlcDataSha,
            byte[] dlcDataId,
            long dlcDataSize,
            string outPath,
            uint sdkAddonVersion = 0x00140000u)
        {
            byte[] cnmtBytes = new byte[0x20 + 0x10 + 1 * 0x38 + 0x20];
            ulong dlcTidVal = Convert.ToUInt64(dlcTitleId, 16);
            ulong baseTidVal = Convert.ToUInt64(baseAppTitleId, 16) & ~0xFFFUL;

            // CNMTHeader
            BitConverter.GetBytes(dlcTidVal).CopyTo(cnmtBytes, 0);       // title_id
            BitConverter.GetBytes(0).CopyTo(cnmtBytes, 8);               // title_version = 0
            cnmtBytes[0x0C] = 0x82;                                      // TitleType = AOC (0x82)
            cnmtBytes[0x0D] = 0;                                         // reserved
            BitConverter.GetBytes((short)0x10).CopyTo(cnmtBytes, 0x0E);  // table_offset = 0x10
            BitConverter.GetBytes((short)1).CopyTo(cnmtBytes, 0x10);     // number_content_entries = 1
            BitConverter.GetBytes((short)0).CopyTo(cnmtBytes, 0x12);     // number_meta_entries = 0
            cnmtBytes[0x14] = 0;                                         // attributes
            cnmtBytes[0x17] = 1;                                         // is_committed

            // OptionalHeader at 0x20
            BitConverter.GetBytes(baseTidVal).CopyTo(cnmtBytes, 0x20);   // base application title ID
            BitConverter.GetBytes(0L).CopyTo(cnmtBytes, 0x28);           // minimum_version = 0

            // ContentRecord 0 at 0x30
            int r0 = 0x30;
            Array.Copy(dlcDataSha, 0, cnmtBytes, r0, 32);                // SHA-256
            Array.Copy(dlcDataId, 0, cnmtBytes, r0 + 32, 16);           // NCA ID
            BitConverter.GetBytes((int)(dlcDataSize & 0xFFFFFFFFL)).CopyTo(cnmtBytes, r0 + 48);
            BitConverter.GetBytes((short)((dlcDataSize >> 32) & 0xFFFFL)).CopyTo(cnmtBytes, r0 + 52);
            cnmtBytes[r0 + 54] = 2;                                      // ContentRecordType::Data (2)
            cnmtBytes[r0 + 55] = 0;                                      // padding

            string cnmtName = $"AddOnContent_{dlcTitleId.ToLowerInvariant()}.cnmt";
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
            BitConverter.GetBytes(dlcTidVal).CopyTo(ncaHdr, 0x210);
            BitConverter.GetBytes(0).CopyTo(ncaHdr, 0x218); // ContentIndex = 0
            BitConverter.GetBytes(sdkAddonVersion).CopyTo(ncaHdr, 0x21C);

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
            ulong aocBaseId = (tidVal & ~0xFFFUL) + 0x1000UL;
            BitConverter.GetBytes(aocBaseId).CopyTo(nacp, 0x3070);

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
                hash = (hash >> 5) | (hash << 27);
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

        private class BoundedReadStream : Stream
        {
            private readonly Stream _baseStream;
            private readonly long _length;
            private long _position;

            public BoundedReadStream(Stream baseStream, long length)
            {
                _baseStream = baseStream;
                _length = length;
                _position = 0;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _length;
            public override long Position { get => _position; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count)
            {
                long remaining = _length - _position;
                if (remaining <= 0) return 0;
                int toRead = (int)Math.Min(count, remaining);
                int read = _baseStream.Read(buffer, offset, toRead);
                _position += read;
                return read;
            }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        private static long BuildStreamedRomFs(Dictionary<string, string> files, string outTempFile, Action<double> onProgress, CancellationToken ct, Stream? nupStream = null)
        {
            var items = new List<(string RelPath, long Size, string FullPath, byte[]? Data)>(files.Count);
            foreach (var kv in files)
            {
                long sz = 0;
                if (kv.Value.StartsWith("__NUP__|", StringComparison.Ordinal))
                {
                    string[] parts = kv.Value.Split('|');
                    if (parts.Length >= 5 && long.TryParse(parts[4], out long parsedSz))
                    {
                        sz = parsedSz;
                    }
                }
                else
                {
                    try { sz = new FileInfo(kv.Value).Length; } catch { }
                }
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

                if (f.SourceFullPath.StartsWith("__NUP__|", StringComparison.Ordinal) && nupStream != null)
                {
                    string[] parts = f.SourceFullPath.Split('|');
                    long readOffset = long.Parse(parts[1]);
                    long extentSize = long.Parse(parts[2]);
                    byte codec = byte.Parse(parts[3]);
                    long rawSize = long.Parse(parts[4]);

                    if (rawSize <= 0 || extentSize == 0)
                    {
                        if (rawSize > 0)
                        {
                            long remZ = rawSize;
                            while (remZ > 0)
                            {
                                int z = (int)Math.Min(remZ, buf.Length);
                                Array.Clear(buf, 0, z);
                                outFs.Write(buf, 0, z);
                                written += z;
                                remZ -= z;
                            }
                        }
                    }
                    else
                    {
                        nupStream.Seek(readOffset, SeekOrigin.Begin);
                        if (codec == 1) // Zstd
                        {
                            using var bound = new BoundedReadStream(nupStream, extentSize);
                            using var zstdStream = new ZstdSharp.DecompressionStream(bound);
                            int r;
                            while ((r = zstdStream.Read(buf, 0, buf.Length)) > 0)
                            {
                                outFs.Write(buf, 0, r);
                                written += r;
                            }
                        }
                        else // uncompressed
                        {
                            long remaining = f.DataSize;
                            while (remaining > 0)
                            {
                                int toRead = (int)Math.Min(remaining, buf.Length);
                                int r = nupStream.Read(buf, 0, toRead);
                                if (r <= 0) break;
                                outFs.Write(buf, 0, r);
                                written += r;
                                remaining -= r;
                            }
                        }
                    }
                }
                else
                {
                    using var inFs = new FileStream(f.SourceFullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024);
                    int r;
                    while ((r = inFs.Read(buf, 0, buf.Length)) > 0)
                    {
                        outFs.Write(buf, 0, r);
                        written += r;
                    }
                }

                if (i % 200 == 0 || i == orderedFiles.Count - 1)
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

        private static async Task ExtractNupExeFsRecursiveAsync(
            Stream nupStream,
            ulong blobOffset,
            uint dirIdx,
            string currentOutDir,
            List<(uint NameOff, uint NameLen, uint FirstChild, uint ChildCount)> dirs,
            List<NupContainerService.NupFileEntry> files,
            uint[] children,
            Func<uint, uint, string> getString,
            CancellationToken ct)
        {
            Directory.CreateDirectory(currentOutDir);
            var d = dirs[(int)dirIdx];
            for (uint i = 0; i < d.ChildCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                uint childVal = children[d.FirstChild + i];
                if (childVal < dirs.Count)
                {
                    string subName = getString(dirs[(int)childVal].NameOff, dirs[(int)childVal].NameLen);
                    if (string.IsNullOrEmpty(subName)) subName = $"dir_{childVal}";
                    string subPath = Path.Combine(currentOutDir, subName);
                    await ExtractNupExeFsRecursiveAsync(nupStream, blobOffset, childVal, subPath, dirs, files, children, getString, ct);
                }
                else
                {
                    int fileIdx = (int)(childVal - (uint)dirs.Count);
                    if (fileIdx >= 0 && fileIdx < files.Count)
                    {
                        var fe = files[fileIdx];
                        string fileName = getString(fe.NameOff, fe.NameLen);
                        if (string.IsNullOrEmpty(fileName)) fileName = $"file_{fileIdx}.bin";
                        string filePath = Path.Combine(currentOutDir, fileName);

                        ulong readOffset = fe.ExtentOffset < blobOffset ? (blobOffset + fe.ExtentOffset) : fe.ExtentOffset;
                        nupStream.Seek((long)readOffset, SeekOrigin.Begin);

                        byte[] extentBytes = new byte[fe.ExtentSize];
                        await nupStream.ReadExactlyAsync(extentBytes, 0, (int)fe.ExtentSize, ct);

                        byte[] uncompressedData;
                        if (fe.Codec == 1) // Zstandard
                        {
                            using var decompressor = new Decompressor();
                            uncompressedData = decompressor.Unwrap(extentBytes).ToArray();
                        }
                        else
                        {
                            uncompressedData = extentBytes;
                        }

                        await File.WriteAllBytesAsync(filePath, uncompressedData, ct);
                    }
                }
            }
        }

        private static void CollectNupRomFsRecursive(
            uint dirIdx,
            string currentRelPath,
            List<(uint NameOff, uint NameLen, uint FirstChild, uint ChildCount)> dirs,
            List<NupContainerService.NupFileEntry> files,
            uint[] children,
            Func<uint, uint, string> getString,
            ulong blobOffset,
            Dictionary<string, string> targetMap)
        {
            var d = dirs[(int)dirIdx];
            for (uint i = 0; i < d.ChildCount; i++)
            {
                uint childVal = children[d.FirstChild + i];
                if (childVal < dirs.Count)
                {
                    string subName = getString(dirs[(int)childVal].NameOff, dirs[(int)childVal].NameLen);
                    if (string.IsNullOrEmpty(subName)) subName = $"dir_{childVal}";
                    string subRel = string.IsNullOrEmpty(currentRelPath) ? subName : (currentRelPath + "/" + subName);
                    CollectNupRomFsRecursive(childVal, subRel, dirs, files, children, getString, blobOffset, targetMap);
                }
                else
                {
                    int fileIdx = (int)(childVal - (uint)dirs.Count);
                    if (fileIdx >= 0 && fileIdx < files.Count)
                    {
                        var fe = files[fileIdx];
                        string fileName = getString(fe.NameOff, fe.NameLen);
                        if (string.IsNullOrEmpty(fileName)) fileName = $"file_{fileIdx}.bin";
                        if (fileName.Equals(".listing", StringComparison.OrdinalIgnoreCase)) continue;
                        string rel = string.IsNullOrEmpty(currentRelPath) ? fileName : (currentRelPath + "/" + fileName);
                        ulong readOffset = fe.ExtentOffset < blobOffset ? (blobOffset + fe.ExtentOffset) : fe.ExtentOffset;
                        targetMap[rel] = $"__NUP__|{readOffset}|{fe.ExtentSize}|{fe.Codec}|{fe.Size}";
                    }
                }
            }
        }

        public static async Task CompileNupToSwitch2ContainerAsync(
            ProcessingTask task,
            string nupPath,
            string outPath,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(nupPath) || !File.Exists(nupPath))
            {
                throw new FileNotFoundException($"Файл .nup не найден: {nupPath}");
            }

            App.RunOnUI(() =>
            {
                task.EnsureSourceMetricsPopulated();
                task.Status = "Анализ .nup контейнера...";
                task.IsRunning = true;
                task.Progress = 5;
                task.LogDetails += $"\n[NUP Stream] Запуск прямого потокового компилятора Switch 2 без распаковки на диск ({Path.GetFileName(nupPath)})...\n";
            });

            await using var fs = new FileStream(nupPath, FileMode.Open, FileAccess.Read, FileShare.Read, 2 * 1024 * 1024, useAsync: true);
            if (fs.Length < 0x80) throw new InvalidDataException("Файл слишком мал для заголовка NUP.");

            byte[] superblock = new byte[0x80];
            await fs.ReadExactlyAsync(superblock, 0, 0x80, ct);
            if (!superblock.AsSpan(0, 4).SequenceEqual(NupContainerService.NupMagic))
            {
                throw new InvalidDataException("Файл не является валидным NUP v1.0 контейнером!");
            }

            ulong tocOffset = BinaryPrimitives.ReadUInt64LittleEndian(superblock.AsSpan(0x08, 8));
            ulong tocSize = BinaryPrimitives.ReadUInt64LittleEndian(superblock.AsSpan(0x10, 8));
            ulong blobOffset = BinaryPrimitives.ReadUInt64LittleEndian(superblock.AsSpan(0x38, 8));
            ulong fileSize = BinaryPrimitives.ReadUInt64LittleEndian(superblock.AsSpan(0x40, 8));

            fs.Seek((long)tocOffset, SeekOrigin.Begin);
            byte[] tocBytes = new byte[tocSize];
            await fs.ReadExactlyAsync(tocBytes, 0, (int)tocSize, ct);

            if (tocBytes.Length < 0x28 || !tocBytes.AsSpan(0, 4).SequenceEqual(NupContainerService.TocMagic))
            {
                throw new InvalidDataException("Заголовок TOC NUP поврежден или отсутствует!");
            }

            uint packageCount = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(0x04, 4));
            uint c08 = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(0x08, 4));
            uint c0C = tocBytes.Length >= 0x10 ? BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(0x0C, 4)) : 0;
            uint c10 = tocBytes.Length >= 0x14 ? BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(0x10, 4)) : 0;
            ulong strPoolOffset = BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(0x18, 8));
            ulong strPoolSize = BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(0x20, 8));

            bool isLegacy28 = c0C > 0 &&
                (0x28UL + (ulong)packageCount * 0x40UL + (ulong)c08 * 0x18UL + (ulong)c0C * 0x50UL + (ulong)c10 * 4UL) == strPoolOffset;

            int pkgStart = isLegacy28 ? 0x28 : 0x30;
            ulong dirCount = isLegacy28 ? c08 : BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(0x08, 8));
            ulong fileCount = isLegacy28 ? c0C : BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(0x10, 8));

            string GetString(uint off, uint len)
            {
                int start = (int)(strPoolOffset + off);
                if (start >= tocBytes.Length) return string.Empty;
                int actualLen = (int)len;
                if (start + actualLen > tocBytes.Length) actualLen = tocBytes.Length - start;
                return Encoding.UTF8.GetString(tocBytes, start, actualLen).TrimEnd('\0');
            }

            // Dirs
            int dirsTableOffset = pkgStart + (int)packageCount * 0x40;
            var dirs = new List<(uint NameOff, uint NameLen, uint FirstChild, uint ChildCount)>((int)dirCount);
            for (int i = 0; i < (int)dirCount; i++)
            {
                int off = dirsTableOffset + i * 0x18;
                dirs.Add((
                    BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(off, 4)),
                    BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(off + 4, 4)),
                    BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(off + 8, 4)),
                    BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(off + 12, 4))
                ));
            }

            // Files
            int filesTableOffset = dirsTableOffset + (int)dirCount * 0x18;
            var files = new List<NupContainerService.NupFileEntry>((int)fileCount);
            for (int i = 0; i < (int)fileCount; i++)
            {
                int off = filesTableOffset + i * 0x50;
                files.Add(new NupContainerService.NupFileEntry
                {
                    NameOff = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(off, 4)),
                    NameLen = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(off + 4, 4)),
                    Size = BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(off + 8, 8)),
                    ExtentOffset = BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(off + 16, 8)),
                    ExtentSize = BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(off + 24, 8)),
                    Sha256 = tocBytes.AsSpan(off + 32, 32).ToArray(),
                    Codec = tocBytes[off + 64]
                });
            }

            // children[]
            int childrenStart = filesTableOffset + (int)fileCount * 0x50;
            int childrenCount = (int)((strPoolOffset > (ulong)childrenStart) ? (strPoolOffset - (ulong)childrenStart) / 4 : 0);
            var children = new uint[childrenCount];
            for (int i = 0; i < childrenCount; i++)
            {
                children[i] = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(childrenStart + i * 4, 4));
            }

            // Packages & TLV
            var packages = new List<NupPackageInfo>();
            for (int p = 0; p < packageCount; p++)
            {
                int pOff = pkgStart + p * 0x40;
                ulong tid = BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(pOff, 8));
                uint rootDirIdx = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(pOff + 8, 4));
                uint exefsDirIdx = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(pOff + 12, 4));
                uint romfsDirIdx = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(pOff + 16, 4));
                uint version = BinaryPrimitives.ReadUInt32LittleEndian(tocBytes.AsSpan(pOff + 20, 4));
                byte kind = tocBytes[pOff + 24];
                byte flags = tocBytes[pOff + 25];
                ulong metaOffset = BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(pOff + 0x20, 8));
                ulong metaSize = BinaryPrimitives.ReadUInt64LittleEndian(tocBytes.AsSpan(pOff + 0x28, 8));

                var pkgInfo = new NupPackageInfo
                {
                    TitleId = tid,
                    Version = version,
                    Kind = kind,
                    Flags = flags,
                    RootDirIndex = rootDirIdx,
                    ExeFsDirIndex = exefsDirIdx,
                    RomFsDirIndex = romfsDirIdx
                };

                if (metaOffset > 0 && metaSize > 0 && metaSize < 32 * 1024 * 1024)
                {
                    byte[]? metaData = null;
                    if (metaOffset >= (ulong)tocBytes.Length && metaOffset + metaSize <= (ulong)fs.Length)
                    {
                        try
                        {
                            long curPos = fs.Position;
                            fs.Seek((long)metaOffset, SeekOrigin.Begin);
                            byte[] buf = new byte[metaSize];
                            await fs.ReadExactlyAsync(buf, 0, (int)metaSize, ct);
                            fs.Seek(curPos, SeekOrigin.Begin);
                            metaData = buf;
                        }
                        catch { }
                    }
                    else if (metaOffset + metaSize <= (ulong)tocBytes.Length)
                    {
                        metaData = tocBytes.AsSpan((int)metaOffset, (int)metaSize).ToArray();
                    }

                    if (metaData != null)
                    {
                        int mCur = 0;
                        int mEnd = metaData.Length;
                        while (mCur + 8 <= mEnd)
                        {
                            uint tag = BinaryPrimitives.ReadUInt32LittleEndian(metaData.AsSpan(mCur, 4));
                            uint len = BinaryPrimitives.ReadUInt32LittleEndian(metaData.AsSpan(mCur + 4, 4));
                            mCur += 8;
                            if (mCur + (int)len > mEnd) break;
                            byte[] val = metaData.AsSpan(mCur, (int)len).ToArray();
                            mCur += (int)len;

                            switch (tag)
                            {
                                case 0x0001: pkgInfo.NpdmBytes = val; break;
                                case 0x0002: pkgInfo.NacpBytes = val; break;
                                case 0x0003: if (val.Length > 64) pkgInfo.IconBytes = val; break;
                                case 0x0004: pkgInfo.DisplayVersion = Encoding.UTF8.GetString(val).TrimEnd('\0').Trim(); break;
                                case 0x0005:
                                case 0x0008:
                                    string name = Encoding.UTF8.GetString(val).TrimEnd('\0').Trim();
                                    if (!string.IsNullOrEmpty(name))
                                    {
                                        if (string.IsNullOrEmpty(pkgInfo.TitleName) || tag == 0x0005)
                                            pkgInfo.TitleName = name;
                                    }
                                    break;
                                case 0x0006: pkgInfo.Publisher = Encoding.UTF8.GetString(val).TrimEnd('\0').Trim(); break;
                                case 0x0007: pkgInfo.BuildId = Convert.ToHexString(val); break;
                            }
                        }
                    }
                }

                packages.Add(pkgInfo);
            }

            var basePkg = packages.FirstOrDefault(p => p.Kind == 0) ?? packages.FirstOrDefault();
            if (basePkg == null) throw new InvalidDataException("Контейнер .nup не содержит базового пакета!");
            var updPkg = packages.FirstOrDefault(p => p.Kind == 1);
            var dlcPackages = packages.Where(p => p.Kind == 2).ToList();

            string titleId = basePkg.TitleIdHex.ToUpperInvariant();
            bool isSwitch2 = titleId.StartsWith("0400", StringComparison.OrdinalIgnoreCase);

            string outDir = Path.GetDirectoryName(outPath) ?? "";
            if (string.IsNullOrEmpty(outDir) && !string.IsNullOrEmpty(task.OutputFolder) && Directory.Exists(task.OutputFolder))
            {
                outDir = task.OutputFolder;
            }
            if (string.IsNullOrEmpty(outDir) && !string.IsNullOrEmpty(App.Settings.Current.OutputFolderSwitch2) && Directory.Exists(App.Settings.Current.OutputFolderSwitch2))
            {
                outDir = App.Settings.Current.OutputFolderSwitch2;
            }
            if (string.IsNullOrEmpty(outDir)) outDir = Directory.GetCurrentDirectory();
            Directory.CreateDirectory(outDir);

            DateTime startTime = DateTime.Now;
            string targetFormat = !string.IsNullOrEmpty(task.TargetFormat) ? task.TargetFormat.ToUpperInvariant() : (!string.IsNullOrEmpty(App.Settings.Current.Switch2DefaultFormat) ? App.Settings.Current.Switch2DefaultFormat.ToUpperInvariant() : "NSZ");

            string canonicalName = CleanGameTitle(!string.IsNullOrEmpty(basePkg.TitleName) ? basePkg.TitleName : Path.GetFileNameWithoutExtension(nupPath));
            string baseFileName = !string.IsNullOrWhiteSpace(task.OutputFileName) && !task.OutputFileName.Contains("[БАЗА")
                ? task.OutputFileName
                : canonicalName;

            App.RunOnUI(() => { task.OutputFileName = baseFileName; });

            string tempRoot = Path.Combine(Path.GetPathRoot(Path.GetFullPath(outDir)) ?? Path.GetTempPath(), "STORM_TMP");
            Directory.CreateDirectory(tempRoot);
            string tempWorkDir = Path.Combine(tempRoot, "StormSwitch2_" + Guid.NewGuid().ToString("N").Substring(0, 8));
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
                byte[]? iconBytes = customMeta?.CustomIconBytes ?? customMeta?.OriginalIconBytes ?? basePkg.IconBytes ?? await FetchCoverAsync(basePkg.TitleName);
                if (iconBytes == null || iconBytes.Length == 0)
                {
                    iconBytes = GenerateDefaultSwitchCoverBytes(basePkg.TitleName);
                }
                iconBytes = SmartShrinkService.OptimizeSwitchIconJpeg(iconBytes);

                string engTitle = !string.IsNullOrWhiteSpace(customMeta?.TitleNameEnglish) ? customMeta.TitleNameEnglish : (!string.IsNullOrEmpty(basePkg.TitleName) ? basePkg.TitleName : "Nintendo Switch Game");
                string rusTitle = !string.IsNullOrWhiteSpace(customMeta?.TitleNameRussian) ? customMeta.TitleNameRussian : engTitle;
                string pubName = !string.IsNullOrWhiteSpace(customMeta?.Publisher) ? customMeta.Publisher : (!string.IsNullOrEmpty(basePkg.Publisher) ? basePkg.Publisher : "Nintendo");

                // Extract ExeFS files (~6 files) into tempWorkDir\\base_exefs and tempWorkDir\\upd_exefs
                string baseExeFsDir = Path.Combine(tempWorkDir, "base_exefs");
                Directory.CreateDirectory(baseExeFsDir);
                if (basePkg.ExeFsDirIndex < dirs.Count)
                {
                    await ExtractNupExeFsRecursiveAsync(fs, blobOffset, basePkg.ExeFsDirIndex, baseExeFsDir, dirs, files, children, GetString, ct);
                }
                if (basePkg.NpdmBytes != null && basePkg.NpdmBytes.Length > 0 && !File.Exists(Path.Combine(baseExeFsDir, "main.npdm")))
                {
                    await File.WriteAllBytesAsync(Path.Combine(baseExeFsDir, "main.npdm"), basePkg.NpdmBytes, ct);
                }

                string? updExeFsDir = null;
                if (updPkg != null && updPkg.ExeFsDirIndex < dirs.Count)
                {
                    updExeFsDir = Path.Combine(tempWorkDir, "upd_exefs");
                    Directory.CreateDirectory(updExeFsDir);
                    await ExtractNupExeFsRecursiveAsync(fs, blobOffset, updPkg.ExeFsDirIndex, updExeFsDir, dirs, files, children, GetString, ct);
                    if (updPkg.NpdmBytes != null && updPkg.NpdmBytes.Length > 0 && !File.Exists(Path.Combine(updExeFsDir, "main.npdm")))
                    {
                        await File.WriteAllBytesAsync(Path.Combine(updExeFsDir, "main.npdm"), updPkg.NpdmBytes, ct);
                    }
                }

                uint sdkAddonVersion = DetectSdkAddonVersion(!string.IsNullOrEmpty(updExeFsDir) && Directory.Exists(updExeFsDir) ? updExeFsDir : baseExeFsDir, isSwitch2);

                string ctrlNcaPath = Path.Combine(tempWorkDir, "control.nca");
                string displayVer = updPkg != null && !string.IsNullOrEmpty(updPkg.DisplayVersion)
                    ? updPkg.DisplayVersion
                    : (!string.IsNullOrEmpty(basePkg.DisplayVersion) ? basePkg.DisplayVersion : (updPkg != null ? $"v{updPkg.Version}" : "1.0.0"));

                BuildControlNca(titleId, engTitle, pubName, displayVer, iconBytes, ctrlNcaPath, rusTitle, sdkAddonVersion);

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

                var romfsMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (basePkg.RomFsDirIndex < dirs.Count)
                {
                    CollectNupRomFsRecursive(basePkg.RomFsDirIndex, "", dirs, files, children, GetString, blobOffset, romfsMap);
                }
                if (updPkg != null && updPkg.RomFsDirIndex < dirs.Count)
                {
                    CollectNupRomFsRecursive(updPkg.RomFsDirIndex, "", dirs, files, children, GetString, blobOffset, romfsMap);
                }

                string progNcaPath = Path.Combine(tempWorkDir, "program.nca");
                BuildProgramNca(titleId, baseExeFsDir, updExeFsDir, "", null, progNcaPath, p =>
                {
                    App.RunOnUI(() => { task.Progress = 35 + (p * 0.35); });
                }, ct, sdkAddonVersion, normalizeAcidForLegacySwitch1: false, nupStream: fs, prebuiltRomfsMap: romfsMap);

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
                string metaVer = updPkg != null ? updPkg.Version.ToString() : "0";
                BuildMetaCnmtNca(titleId, metaVer, progSha, progSha.Take(16).ToArray(), progSize, ctrlSha, ctrlSha.Take(16).ToArray(), ctrlSize, metaNcaPath, sdkAddonVersion);

                byte[] metaSha = ComputeSha256(metaNcaPath);
                string metaHex = BitConverter.ToString(metaSha, 0, 16).Replace("-", "").ToLowerInvariant();
                string finalMetaNcaPath = Path.Combine(tempWorkDir, $"{metaHex}.cnmt.nca");
                File.Move(metaNcaPath, finalMetaNcaPath, true);

                var ncaFiles = new List<string> { finalMetaNcaPath, finalCtrlNcaPath, finalProgNcaPath };

                // 3b. DLC Packaging
                if (dlcPackages.Count > 0)
                {
                    ulong baseTidVal = Convert.ToUInt64(titleId, 16);
                    int dlcIdx = 0;
                    foreach (var dlcPkg in dlcPackages)
                    {
                        dlcIdx++;
                        App.RunOnUI(() =>
                        {
                            task.Progress = 75 + (int)((double)dlcIdx / dlcPackages.Count * 9);
                            task.Status = $"Сборка DLC {dlcIdx}/{dlcPackages.Count}...";
                            task.LogDetails += $"[DLC {dlcIdx}/{dlcPackages.Count}] Сборка Data NCA и AddOnContent CNMT...\n";
                        });

                        string dlcTitleId = dlcPkg.TitleId != 0 ? dlcPkg.TitleIdHex.ToUpperInvariant() : $"{(baseTidVal & ~0xFFFUL) + 0x1000UL + (ulong)dlcIdx:X16}";

                        var dlcRomfsMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        if (dlcPkg.RomFsDirIndex < dirs.Count)
                        {
                            CollectNupRomFsRecursive(dlcPkg.RomFsDirIndex, "", dirs, files, children, GetString, blobOffset, dlcRomfsMap);
                        }

                        string dlcDataNcaPath = Path.Combine(tempWorkDir, $"dlc_data_{dlcIdx}.nca");
                        BuildDlcDataNca(dlcTitleId, "", dlcDataNcaPath, p => { }, ct, sdkAddonVersion, nupStream: fs, prebuiltRomfsMap: dlcRomfsMap);

                        byte[] dlcDataSha = ComputeSha256(dlcDataNcaPath);
                        string dlcDataHex = BitConverter.ToString(dlcDataSha, 0, 16).Replace("-", "").ToLowerInvariant();
                        string finalDlcDataNcaPath = Path.Combine(tempWorkDir, $"{dlcDataHex}.nca");
                        File.Move(dlcDataNcaPath, finalDlcDataNcaPath, true);
                        long dlcDataSize = new FileInfo(finalDlcDataNcaPath).Length;

                        string dlcMetaNcaPath = Path.Combine(tempWorkDir, $"dlc_meta_{dlcIdx}.nca");
                        BuildDlcMetaCnmtNca(dlcTitleId, titleId, dlcDataSha, dlcDataSha.Take(16).ToArray(), dlcDataSize, dlcMetaNcaPath, sdkAddonVersion);

                        byte[] dlcMetaSha = ComputeSha256(dlcMetaNcaPath);
                        string dlcMetaHex = BitConverter.ToString(dlcMetaSha, 0, 16).Replace("-", "").ToLowerInvariant();
                        string finalDlcMetaNcaPath = Path.Combine(tempWorkDir, $"{dlcMetaHex}.cnmt.nca");
                        File.Move(dlcMetaNcaPath, finalDlcMetaNcaPath, true);

                        ncaFiles.Add(finalDlcDataNcaPath);
                        ncaFiles.Add(finalDlcMetaNcaPath);
                    }
                }

                // 4. Pack into final NSP
                App.RunOnUI(() =>
                {
                    task.Progress = 85;
                    task.Status = "Финальная упаковка PFS0 (.nsp)...";
                    task.LogDetails += "[4/4] Финальная упаковка контейнера...\n";
                });

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
                if (!File.Exists(finalOutPath))
                {
                    var candidates = Directory.GetFiles(outDir, baseFileName + ".*")
                        .Where(f => !f.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                        .OrderByDescending(f => new FileInfo(f).Length)
                        .ToList();
                    if (candidates.Count > 0) finalOutPath = candidates[0];
                }

                task.EnsureFinalMetricsPopulated(finalOutPath);

                App.RunOnUI(() =>
                {
                    task.Progress = 100;
                    task.Status = "Готово";
                    task.IsRunning = false;
                    task.FinishedAt = DateTime.Now;
                    task.LogDetails += $"► Выходной файл: {finalOutPath} ({task.TargetSize})\n";
                    task.LogDetails += $"✅ Сборка Nintendo Switch 2 из .nup успешно завершена!\n   Выходная папка: {outDir}\n";
                    HistoryService.AddToHistory(task);
                });
                App.Logger.Log($"[Switch 2 NUP] Сборка успешно завершена: {Path.GetFileName(finalOutPath)} ({task.TargetSize})", LogLevel.Success);
            }
            finally
            {
                try { Directory.Delete(tempWorkDir, true); } catch { }
            }
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
        public static string DetectDisplayVersion(string baseDir, string? updateDir, string maxUpdateVersion) => Switch2DumpHelper.DetectDisplayVersion(baseDir, updateDir, maxUpdateVersion);
        public static string GenerateCanonicalSwitch2Name(Switch2DumpInfo sw2Info) => Switch2DumpHelper.GenerateCanonicalSwitch2Name(sw2Info);
        public static byte[] StandardNcaHeaderKey => Switch2DumpHelper.StandardNcaHeaderKey;
        public static byte[] LegacyStormNcaHeaderKey => Switch2DumpHelper.LegacyStormNcaHeaderKey;
        public static uint DetectSdkAddonVersion(string? exefsDir, bool isSwitch2) => Switch2DumpHelper.DetectSdkAddonVersion(exefsDir, isSwitch2);
        public static byte[] NormalizeSwitch2NpdmForNca3(byte[] npdmBytes, bool normalizeAcidForLegacySwitch1 = false) => Switch2DumpHelper.NormalizeSwitch2NpdmForNca3(npdmBytes, normalizeAcidForLegacySwitch1);
        public static void BuildControlNca(string titleId, string gameTitle, string publisher, string displayVersion, byte[] iconJpeg, string outPath, string? russianTitle = null, uint sdkAddonVersion = 0x00140000u) => Switch2DumpHelper.BuildControlNca(titleId, gameTitle, publisher, displayVersion, iconJpeg, outPath, russianTitle, sdkAddonVersion);
        public static void BuildProgramNca(string titleId, string baseExeFsDir, string? updExeFsDir, string baseRomFsDir, string? updRomFsDir, string outPath, Action<double> onProgress, CancellationToken ct, uint sdkAddonVersion = 0x00140000u, bool normalizeAcidForLegacySwitch1 = false, Stream? nupStream = null, Dictionary<string, string>? prebuiltRomfsMap = null) => Switch2DumpHelper.BuildProgramNca(titleId, baseExeFsDir, updExeFsDir, baseRomFsDir, updRomFsDir, outPath, onProgress, ct, sdkAddonVersion, normalizeAcidForLegacySwitch1, nupStream, prebuiltRomfsMap);
        public static void BuildMetaCnmtNca(string titleId, string versionStr, byte[] progSha, byte[] progId, long progSize, byte[] ctrlSha, byte[] ctrlId, long ctrlSize, string outPath, uint sdkAddonVersion = 0x00140000u) => Switch2DumpHelper.BuildMetaCnmtNca(titleId, versionStr, progSha, progId, progSize, ctrlSha, ctrlId, ctrlSize, outPath, sdkAddonVersion);
        public static void BuildDlcDataNca(string dlcTitleId, string dlcRomFsRoot, string outPath, Action<double> onProgress, CancellationToken ct, uint sdkAddonVersion = 0x00140000u, Stream? nupStream = null, Dictionary<string, string>? prebuiltRomfsMap = null) => Switch2DumpHelper.BuildDlcDataNca(dlcTitleId, dlcRomFsRoot, outPath, onProgress, ct, sdkAddonVersion, nupStream, prebuiltRomfsMap);
        public static void BuildDlcMetaCnmtNca(string dlcTitleId, string baseAppTitleId, byte[] dlcDataSha, byte[] dlcDataId, long dlcDataSize, string outPath, uint sdkAddonVersion = 0x00140000u) => Switch2DumpHelper.BuildDlcMetaCnmtNca(dlcTitleId, baseAppTitleId, dlcDataSha, dlcDataId, dlcDataSize, outPath, sdkAddonVersion);
        public static void BuildPfs0Container(List<string> ncaFiles, string outPath) => Switch2DumpHelper.BuildPfs0Container(ncaFiles, outPath);
        public static Task CompileNupToSwitch2ContainerAsync(ProcessingTask task, string nupPath, string outPath, CancellationToken ct) => Switch2DumpHelper.CompileNupToSwitch2ContainerAsync(task, nupPath, outPath, ct);

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