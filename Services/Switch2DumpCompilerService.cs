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
    }

    public static class Switch2DumpHelper
    {
        public static bool IsSwitch2OrExtractedGameDumpDirectory(string dirPath)
        {
            if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath))
                return false;

            if (File.Exists(Path.Combine(dirPath, "base", "exefs", "main.npdm")) ||
                File.Exists(Path.Combine(dirPath, "exefs", "main.npdm")))
            {
                return true;
            }

            try
            {
                var subDirs = Directory.GetDirectories(dirPath);
                foreach (var sub in subDirs)
                {
                    if (File.Exists(Path.Combine(sub, "base", "exefs", "main.npdm")) ||
                        File.Exists(Path.Combine(sub, "exefs", "main.npdm")))
                    {
                        return true;
                    }
                }
            }
            catch { }

            return false;
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
            if (!Directory.Exists(dirPath)) return null;

            string targetDir = dirPath;
            string npdmPath = Path.Combine(targetDir, "base", "exefs", "main.npdm");
            if (!File.Exists(npdmPath)) npdmPath = Path.Combine(targetDir, "exefs", "main.npdm");

            if (!File.Exists(npdmPath))
            {
                try
                {
                    foreach (var sub in Directory.GetDirectories(dirPath))
                    {
                        string p1 = Path.Combine(sub, "base", "exefs", "main.npdm");
                        string p2 = Path.Combine(sub, "exefs", "main.npdm");
                        if (File.Exists(p1) || File.Exists(p2))
                        {
                            targetDir = sub;
                            npdmPath = File.Exists(p1) ? p1 : p2;
                            break;
                        }
                    }
                }
                catch { }
            }

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
                    if (int.TryParse(vName, out int vi) && vi > maxVerInt)
                    {
                        maxVerInt = vi;
                        maxVer = vName;
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
                    if (Directory.GetFileSystemEntries(d).Length > 0)
                    {
                        dlcFolders.Add(d);
                    }
                }
            }

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
                CoverBytes = cover
            };
        }

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

            string targetFormat = task.TargetFormat?.ToUpperInvariant() ?? "NSP";

            string baseFileName = sw2Info.GameName;
            if (!string.IsNullOrEmpty(sw2Info.MaxUpdateVersion))
                baseFileName += $" [v{sw2Info.MaxUpdateVersion}]";
            baseFileName += $" [{sw2Info.TitleId}] (1G+1U";
            if (sw2Info.DlcCount > 0) baseFileName += $"+{sw2Info.DlcCount}D";
            baseFileName += ")";

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

                byte[]? iconBytes = sw2Info.CoverBytes ?? await FetchCoverAsync(sw2Info.GameName);
                if (iconBytes == null || iconBytes.Length == 0)
                {
                    iconBytes = GenerateDefaultSwitchCoverBytes(sw2Info.GameName);
                }
                iconBytes = SmartShrinkService.OptimizeSwitchIconJpeg(iconBytes);

                string ctrlNcaPath = Path.Combine(tempWorkDir, "control.nca");
                BuildControlNca(sw2Info.TitleId, sw2Info.GameName, "Nintendo", sw2Info.MaxUpdateVersion.Length > 0 ? $"v{sw2Info.MaxUpdateVersion}" : "1.0.0", iconBytes, ctrlNcaPath);

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

                App.RunOnUI(() =>
                {
                    task.Progress = 100;
                    task.Status = "Успешно";
                    task.IsRunning = false;
                    task.FinishedAt = DateTime.Now;
                    task.LogDetails += $"✅ Сборка Nintendo Switch 2 успешно завершена!\n   Выходная папка: {outDir}\n";
                });
            }
            finally
            {
                try { Directory.Delete(tempWorkDir, true); } catch { }
            }
        }

        public static void BuildControlNca(string titleId, string gameTitle, string publisher, string displayVersion, byte[] iconJpeg, string outPath)
        {
            byte[] nacp = BuildNacpBytes(titleId, gameTitle, publisher, displayVersion);
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
                ncaHdr[0x205] = 1; // Program
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
            ncaHdr[0x205] = 0; // Meta
            ncaHdr[0x206] = 0; // None
            BitConverter.GetBytes(totalNcaSize).CopyTo(ncaHdr, 0x208);
            BitConverter.GetBytes(tidVal).CopyTo(ncaHdr, 0x210);

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

        private static byte[] BuildNacpBytes(string titleId, string title, string publisher, string version)
        {
            byte[] nacp = new byte[0x4000];
            byte[] tBytes = Encoding.UTF8.GetBytes(title.Length > 0 ? title : "SWITCH 2 APPLICATION");
            byte[] pBytes = Encoding.UTF8.GetBytes(publisher.Length > 0 ? publisher : "Nintendo");

            for (int i = 0; i < 16; i++)
            {
                int baseOff = i * 0x300;
                Array.Copy(tBytes, 0, nacp, baseOff, Math.Min(tBytes.Length, 0x200));
                Array.Copy(pBytes, 0, nacp, baseOff + 0x200, Math.Min(pBytes.Length, 0x100));
            }

            byte[] vBytes = Encoding.UTF8.GetBytes(version.Length > 0 ? version : "1.0.0");
            Array.Copy(vBytes, 0, nacp, 0x3060, Math.Min(vBytes.Length, 0x10));

            ulong tidVal = Convert.ToUInt64(titleId, 16);
            BitConverter.GetBytes(tidVal).CopyTo(nacp, 0x3038);

            nacp[0x30F0] = 0; // LicensedByNintendo
            nacp[0x30F4] = 2; // Auto
            return nacp;
        }

        private static byte[] BuildRomFsImage(Dictionary<string, byte[]> files)
        {
            var entries = files.ToList();
            int bucketCount = 3;
            int dirHashTableSize = 4;
            int dirMetaTableSize = 24;
            int fileHashTableSize = bucketCount * 4;

            int[] fileMetaOffsets = new int[entries.Count];
            List<byte[]> fileNameBytesList = entries.Select(e => Encoding.UTF8.GetBytes(e.Key)).ToList();
            int curMetaOff = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                fileMetaOffsets[i] = curMetaOff;
                int aligned = (fileNameBytesList[i].Length + 3) & ~3;
                curMetaOff += 32 + aligned;
            }
            int fileMetaTableSize = curMetaOff;

            int headerAndMetaEnd = 0x50 + dirHashTableSize + dirMetaTableSize + fileHashTableSize + fileMetaTableSize;
            int filePartitionOffset = (headerAndMetaEnd + 0x1FF) & ~0x1FF;

            long[] fileDataOffsets = new long[entries.Count];
            long curDataOff = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                curDataOff = (curDataOff + 0x0FL) & ~0x0FL;
                fileDataOffsets[i] = curDataOff;
                curDataOff += entries[i].Value.Length;
            }

            int totalRomFsSize = (int)((filePartitionOffset + curDataOff + 0x3FFFL) & ~0x3FFFL);
            byte[] outBuf = new byte[totalRomFsSize];

            int dirHashOff = 0x50;
            int dirMetaOff = dirHashOff + dirHashTableSize;
            int fileHashOff = dirMetaOff + dirMetaTableSize;
            int fileMetaOff = fileHashOff + fileHashTableSize;

            BitConverter.GetBytes(0x50L).CopyTo(outBuf, 0x00);
            BitConverter.GetBytes((long)dirHashOff).CopyTo(outBuf, 0x08);
            BitConverter.GetBytes((long)dirHashTableSize).CopyTo(outBuf, 0x10);
            BitConverter.GetBytes((long)dirMetaOff).CopyTo(outBuf, 0x18);
            BitConverter.GetBytes((long)dirMetaTableSize).CopyTo(outBuf, 0x20);
            BitConverter.GetBytes((long)fileHashOff).CopyTo(outBuf, 0x28);
            BitConverter.GetBytes((long)fileHashTableSize).CopyTo(outBuf, 0x30);
            BitConverter.GetBytes((long)fileMetaOff).CopyTo(outBuf, 0x38);
            BitConverter.GetBytes((long)fileMetaTableSize).CopyTo(outBuf, 0x40);
            BitConverter.GetBytes((long)filePartitionOffset).CopyTo(outBuf, 0x48);

            // root dir
            BitConverter.GetBytes(0).CopyTo(outBuf, dirMetaOff);
            BitConverter.GetBytes(-1).CopyTo(outBuf, dirMetaOff + 4);
            BitConverter.GetBytes(-1).CopyTo(outBuf, dirMetaOff + 8);
            BitConverter.GetBytes(entries.Count > 0 ? 0 : -1).CopyTo(outBuf, dirMetaOff + 12);
            BitConverter.GetBytes(-1).CopyTo(outBuf, dirMetaOff + 16);
            BitConverter.GetBytes(0).CopyTo(outBuf, dirMetaOff + 20);

            int[] bucketChains = Enumerable.Repeat(-1, bucketCount).ToArray();
            int[] fileHashNext = Enumerable.Repeat(-1, entries.Count).ToArray();
            for (int i = 0; i < entries.Count; i++)
            {
                uint h = 123456789u;
                foreach (byte b in fileNameBytesList[i])
                {
                    h = (h >> 5) | (h << 27);
                    h ^= b;
                }
                int bIdx = (int)(h % (uint)bucketCount);
                fileHashNext[i] = bucketChains[bIdx];
                bucketChains[bIdx] = fileMetaOffsets[i];
            }
            for (int b = 0; b < bucketCount; b++)
            {
                BitConverter.GetBytes(bucketChains[b]).CopyTo(outBuf, fileHashOff + b * 4);
            }

            for (int i = 0; i < entries.Count; i++)
            {
                int mOff = fileMetaOff + fileMetaOffsets[i];
                byte[] nb = fileNameBytesList[i];
                int next = (i + 1 < entries.Count) ? fileMetaOffsets[i + 1] : -1;
                BitConverter.GetBytes(0).CopyTo(outBuf, mOff);
                BitConverter.GetBytes(next).CopyTo(outBuf, mOff + 4);
                BitConverter.GetBytes(fileDataOffsets[i]).CopyTo(outBuf, mOff + 8);
                BitConverter.GetBytes((long)entries[i].Value.Length).CopyTo(outBuf, mOff + 16);
                BitConverter.GetBytes(fileHashNext[i]).CopyTo(outBuf, mOff + 24);
                BitConverter.GetBytes(nb.Length).CopyTo(outBuf, mOff + 28);
                Array.Copy(nb, 0, outBuf, mOff + 32, nb.Length);
            }

            for (int i = 0; i < entries.Count; i++)
            {
                int dest = filePartitionOffset + (int)fileDataOffsets[i];
                Array.Copy(entries[i].Value, 0, outBuf, dest, entries[i].Value.Length);
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
            BitConverter.GetBytes((short)2).CopyTo(fsHdr, 0); // Version
            fsHdr[2] = 2; // RomFS
            fsHdr[3] = 3; // IVFC
            fsHdr[4] = 1; // None

            Encoding.ASCII.GetBytes("IVFC").CopyTo(fsHdr, 8);
            BitConverter.GetBytes(0x00020000).CopyTo(fsHdr, 0x0C);
            BitConverter.GetBytes(7).CopyTo(fsHdr, 0x10);

            long[] offsets = { 0, lv1.Length, lv1.Length + lv2.Length, lv1.Length + lv2.Length + lv3.Length, lv1.Length + lv2.Length + lv3.Length + lv4.Length, (long)ivfcPrefix.Length };
            long[] sizes = { lv1.Length, lv2.Length, lv3.Length, lv4.Length, lv5.Length, lv6.Length };

            for (int i = 0; i < 6; i++)
            {
                int lOff = 0x14 + i * 0x18;
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
            var entries = files.ToList();
            int bucketCount = 3;
            int dirHashTableSize = 4;
            int dirMetaTableSize = 24;
            int fileHashTableSize = bucketCount * 4;

            int[] fileMetaOffsets = new int[entries.Count];
            List<byte[]> fileNameBytesList = entries.Select(e => Encoding.UTF8.GetBytes(e.Key)).ToList();
            int curMetaOff = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                fileMetaOffsets[i] = curMetaOff;
                int aligned = (fileNameBytesList[i].Length + 3) & ~3;
                curMetaOff += 32 + aligned;
            }
            int fileMetaTableSize = curMetaOff;

            int headerAndMetaEnd = 0x50 + dirHashTableSize + dirMetaTableSize + fileHashTableSize + fileMetaTableSize;
            int filePartitionOffset = (headerAndMetaEnd + 0x1FF) & ~0x1FF;

            long[] fileDataOffsets = new long[entries.Count];
            long curDataOff = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                curDataOff = (curDataOff + 0x0FL) & ~0x0FL;
                fileDataOffsets[i] = curDataOff;
                curDataOff += new FileInfo(entries[i].Value).Length;
            }

            long totalRomFsSize = (filePartitionOffset + curDataOff + 0x3FFFL) & ~0x3FFFL;
            byte[] metaHeader = new byte[filePartitionOffset];

            int dirHashOff = 0x50;
            int dirMetaOff = dirHashOff + dirHashTableSize;
            int fileHashOff = dirMetaOff + dirMetaTableSize;
            int fileMetaOff = fileHashOff + fileHashTableSize;

            BitConverter.GetBytes(0x50L).CopyTo(metaHeader, 0x00);
            BitConverter.GetBytes((long)dirHashOff).CopyTo(metaHeader, 0x08);
            BitConverter.GetBytes((long)dirHashTableSize).CopyTo(metaHeader, 0x10);
            BitConverter.GetBytes((long)dirMetaOff).CopyTo(metaHeader, 0x18);
            BitConverter.GetBytes((long)dirMetaTableSize).CopyTo(metaHeader, 0x20);
            BitConverter.GetBytes((long)fileHashOff).CopyTo(metaHeader, 0x28);
            BitConverter.GetBytes((long)fileHashTableSize).CopyTo(metaHeader, 0x30);
            BitConverter.GetBytes((long)fileMetaOff).CopyTo(metaHeader, 0x38);
            BitConverter.GetBytes((long)fileMetaTableSize).CopyTo(metaHeader, 0x40);
            BitConverter.GetBytes((long)filePartitionOffset).CopyTo(metaHeader, 0x48);

            BitConverter.GetBytes(0).CopyTo(metaHeader, dirMetaOff);
            BitConverter.GetBytes(-1).CopyTo(metaHeader, dirMetaOff + 4);
            BitConverter.GetBytes(-1).CopyTo(metaHeader, dirMetaOff + 8);
            BitConverter.GetBytes(entries.Count > 0 ? 0 : -1).CopyTo(metaHeader, dirMetaOff + 12);
            BitConverter.GetBytes(-1).CopyTo(metaHeader, dirMetaOff + 16);
            BitConverter.GetBytes(0).CopyTo(metaHeader, dirMetaOff + 20);

            int[] bucketChains = Enumerable.Repeat(-1, bucketCount).ToArray();
            int[] fileHashNext = Enumerable.Repeat(-1, entries.Count).ToArray();
            for (int i = 0; i < entries.Count; i++)
            {
                uint h = 123456789u;
                foreach (byte b in fileNameBytesList[i])
                {
                    h = (h >> 5) | (h << 27);
                    h ^= b;
                }
                int bIdx = (int)(h % (uint)bucketCount);
                fileHashNext[i] = bucketChains[bIdx];
                bucketChains[bIdx] = fileMetaOffsets[i];
            }
            for (int b = 0; b < bucketCount; b++)
            {
                BitConverter.GetBytes(bucketChains[b]).CopyTo(metaHeader, fileHashOff + b * 4);
            }

            for (int i = 0; i < entries.Count; i++)
            {
                int mOff = fileMetaOff + fileMetaOffsets[i];
                byte[] nb = fileNameBytesList[i];
                int next = (i + 1 < entries.Count) ? fileMetaOffsets[i + 1] : -1;
                BitConverter.GetBytes(0).CopyTo(metaHeader, mOff);
                BitConverter.GetBytes(next).CopyTo(metaHeader, mOff + 4);
                BitConverter.GetBytes(fileDataOffsets[i]).CopyTo(metaHeader, mOff + 8);
                long fLen = new FileInfo(entries[i].Value).Length;
                BitConverter.GetBytes(fLen).CopyTo(metaHeader, mOff + 16);
                BitConverter.GetBytes(fileHashNext[i]).CopyTo(metaHeader, mOff + 24);
                BitConverter.GetBytes(nb.Length).CopyTo(metaHeader, mOff + 28);
                Array.Copy(nb, 0, metaHeader, mOff + 32, nb.Length);
            }

            using var outFs = new FileStream(outTempFile, FileMode.Create, FileAccess.Write, FileShare.None, 2 * 1024 * 1024);
            outFs.Write(metaHeader, 0, metaHeader.Length);

            byte[] buf = new byte[1024 * 1024];
            long written = filePartitionOffset;

            for (int i = 0; i < entries.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                long targetOff = filePartitionOffset + fileDataOffsets[i];
                while (written < targetOff)
                {
                    int pad = (int)Math.Min(targetOff - written, buf.Length);
                    Array.Clear(buf, 0, pad);
                    outFs.Write(buf, 0, pad);
                    written += pad;
                }

                using var inFs = new FileStream(entries[i].Value, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024);
                int r;
                while ((r = inFs.Read(buf, 0, buf.Length)) > 0)
                {
                    outFs.Write(buf, 0, r);
                    written += r;
                }
                if (i % 20 == 0)
                {
                    onProgress((double)i / Math.Max(1, entries.Count));
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
            BitConverter.GetBytes((short)2).CopyTo(fsHdr, 0);
            fsHdr[2] = 2; // RomFS
            fsHdr[3] = 3; // IVFC
            fsHdr[4] = 1; // None

            Encoding.ASCII.GetBytes("IVFC").CopyTo(fsHdr, 8);
            BitConverter.GetBytes(0x00020000).CopyTo(fsHdr, 0x0C);
            BitConverter.GetBytes(7).CopyTo(fsHdr, 0x10);

            long[] offsets = { 0, lv1.Length, lv1.Length + lv2.Length, lv1.Length + lv2.Length + lv3.Length, lv1.Length + lv2.Length + lv3.Length + lv4.Length, (long)ivfcPrefix.Length };
            long[] sizes = { lv1.Length, lv2.Length, lv3.Length, lv4.Length, lv5.Length, romfsLen };

            for (int i = 0; i < 6; i++)
            {
                int lOff = 0x14 + i * 0x18;
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
}
