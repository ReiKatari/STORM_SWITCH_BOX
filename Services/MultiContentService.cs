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
using Path = System.IO.Path;

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
            if (string.IsNullOrWhiteSpace(task.TargetFormat))
            {
                App.RunOnUI(() => { task.TargetFormat = task.Is3dsTask ? "3DS" : "NSP"; });
            }

            bool isTargetXci = string.Equals(task.TargetFormat, "XCI", StringComparison.OrdinalIgnoreCase) || 
                               string.Equals(task.TargetFormat, "XCZ", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(task.TargetFormat, "XCI + XCZ", StringComparison.OrdinalIgnoreCase);

            bool isDualFormat = string.Equals(task.TargetFormat, "NSP + NSZ", StringComparison.OrdinalIgnoreCase) || 
                                string.Equals(task.TargetFormat, "XCI + XCZ", StringComparison.OrdinalIgnoreCase);

            bool isCompressedFormat = string.Equals(task.TargetFormat, "NSZ", StringComparison.OrdinalIgnoreCase) || 
                                      string.Equals(task.TargetFormat, "XCZ", StringComparison.OrdinalIgnoreCase) || 
                                      isDualFormat;

            bool keepUncompressed = isDualFormat || (!string.Equals(task.TargetFormat, "NSZ", StringComparison.OrdinalIgnoreCase) && 
                                                     !string.Equals(task.TargetFormat, "XCZ", StringComparison.OrdinalIgnoreCase));

            string uncompressedExt = isTargetXci ? ".xci" : ".nsp";
            string compressedExt = isTargetXci ? ".xcz" : ".nsz";
            string intermediatePath = outPath;
            string finalUncompressedPath = string.Empty;
            string finalCompressedPath = string.Empty;
            bool compressionSuccess = false;
            
            string tempDecompDir = string.Empty;

            try
            {
                App.RunOnUI(() =>
                {
                    App.RunOnUI(() => { task.Status = "РђРЅР°Р»РёР· С„Р°Р№Р»РѕРІ..."; });
                    App.RunOnUI(() => { task.IsRunning = true; });
                    App.RunOnUI(() => { task.Progress = 0; });
                    task.LogDetails += $"\nрџ“‹ [РќР°СЃС‚СЂРѕР№РєРё] Р¤Р°Р№Р»РѕРІ: {inputFiles.Count} | HardPatch: {(patchFirmware ? "Р”Р°" : "РќРµС‚")}";
                });

                if (!_keysService.IsLoaded) throw new Exception("РћС‚СЃСѓС‚СЃС‚РІСѓСЋС‚ РєСЂРёРїС‚РѕРіСЂР°С„РёС‡РµСЃРєРёРµ РєР»СЋС‡Рё (prod.keys). РџРѕР¶Р°Р»СѓР№СЃС‚Р°, РІС‹Р±РµСЂРёС‚Рµ РёС… РІ РїР°СЂР°РјРµС‚СЂР°С….");


                // РђРЅР°Р»РёР· С„Р°Р№Р»РѕРІ
                foreach (var f in inputFiles)
                {
                    // var info = App.SwitchFormat.ParseNsp(f);
                }

                // Р•СЃР»Рё СЌС‚Рѕ СЃР±РѕСЂРєР° 1G+1U РёР»Рё 1G+1U+1M, РјС‹ РґРµР»РµРіРёСЂСѓРµРј СЃР±РѕСЂРєСѓ РЅР°РїСЂСЏРјСѓСЋ РІ NSC_Builder.
                // Р”Р»СЏ 1G+1U+1M СЃСЋРґР° РїСЂРёС…РѕРґРёС‚ С‚РѕР»СЊРєРѕ РѕРґРёРЅ С„Р°Р№Р»: prepatch.nsp (РєРѕС‚РѕСЂС‹Р№ СѓР¶Рµ СЃРѕРґРµСЂР¶РёС‚ СЃР»РёС‚С‹Рµ РґР°РЅРЅС‹Рµ Р±Р°Р·С‹, РїР°С‚С‡Р° Рё РјРѕРґР°).
                // Р­С‚Рѕ РёР·Р±РµРіР°РµС‚ СЃРѕР·РґР°РЅРёСЏ РєСЂРёРІРѕРіРѕ "СЃС‹СЂРѕРіРѕ" PFS0 Рё РїРѕР·РІРѕР»СЏРµС‚ squirrel.exe РїСЂР°РІРёР»СЊРЅРѕ СЃР»РёС‚СЊ CNMT РёР»Рё РїСЂРѕРїР°С‚С‡РёС‚СЊ РІРµСЂСЃРёСЋ.
                string targetDir = System.IO.Path.GetDirectoryName(outPath) ?? string.Empty;
                if (!string.IsNullOrEmpty(targetDir) && !System.IO.Directory.Exists(targetDir))
                    System.IO.Directory.CreateDirectory(targetDir);

                // РџР°СЂР°Р»Р»РµР»СЊРЅР°СЏ РґРµРєРѕРјРїСЂРµСЃСЃРёСЏ NSZ/XCZ (Pipeline Parallelism)
                App.RunOnUI(() => task.LogDetails += "\nрџџЈ [Р”РµРєРѕРјРїСЂРµСЃСЃРёСЏ] Р Р°СЃРїР°РєРѕРІРєР° NSZ/XCZ...");
                
                var finalInputFiles = new System.Collections.Concurrent.ConcurrentBag<string>();
                string targetDrive = System.IO.Path.GetPathRoot(targetDir) ?? "C:\\";
                // Р’СЃРµРіРґР° СЂР°Р·РјРµС‰Р°РµРј РІСЂРµРјРµРЅРЅС‹Р№ РєР°С‚Р°Р»РѕРі Р±Р»РёР·РєРѕ Рє РєРѕСЂРЅСЋ С†РµР»РµРІРѕРіРѕ РґРёСЃРєР° РґР»СЏ РіР°СЂР°РЅС‚РёСЂРѕРІР°РЅРЅРѕР№ Р·Р°С‰РёС‚С‹ РѕС‚ Р»РёРјРёС‚Р° MAX_PATH (260 СЃРёРјРІРѕР»РѕРІ)
                tempDecompDir = System.IO.Path.Combine(targetDrive, "STORM_TMP", "SD_" + Guid.NewGuid().ToString("N").Substring(0, 6));
                Directory.CreateDirectory(tempDecompDir);
                TempCleanupService.RegisterActiveTempDirectory(tempDecompDir);

                var decompTasks = inputFiles.Select(async f =>
                {
                    if (f.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase))
                    {
                        string? decompResult = await App.NszCompression.DecompressNszAsync(task, f, tempDecompDir, cancellationToken);
                        
                        if (!string.IsNullOrEmpty(decompResult) && File.Exists(decompResult))
                        {
                            finalInputFiles.Add(PrepareSafeFileForTemp(decompResult, tempDecompDir));
                        }
                        else
                        {
                            throw new Exception($"РќР°С‚РёРІРЅР°СЏ РґРµРєРѕРјРїСЂРµСЃСЃРёСЏ С„Р°Р№Р»Р° {System.IO.Path.GetFileName(f)} Р·Р°РІРµСЂС€РёР»Р°СЃСЊ СЃ РѕС€РёР±РєРѕР№.");
                        }
                    }
                    else
                    {
                        finalInputFiles.Add(PrepareSafeFileForTemp(f, tempDecompDir));
                    }
                });

                await Task.WhenAll(decompTasks);
                var finalInputFilesList = finalInputFiles.ToList();

                string formattedOut = FormatOutputFileName(outPath, inputFiles);
                if (!string.IsNullOrEmpty(formattedOut))
                {
                    outPath = formattedOut;
                    App.RunOnUI(() => { task.OutputFileName = System.IO.Path.GetFileNameWithoutExtension(outPath); });
                }

                string baseNameClean = System.IO.Path.GetFileNameWithoutExtension(outPath);
                finalUncompressedPath = System.IO.Path.Combine(targetDir, baseNameClean + uncompressedExt);
                finalCompressedPath = System.IO.Path.Combine(targetDir, baseNameClean + compressedExt);

                if (keepUncompressed)
                {
                    intermediatePath = finalUncompressedPath;
                }
                else
                {
                    // Р”Р»СЏ С‡РёСЃС‚РѕРіРѕ СЃР¶Р°С‚РѕРіРѕ С„РѕСЂРјР°С‚Р° (NSZ/XCZ) РЅРµСЃР¶Р°С‚С‹Р№ С„Р°Р№Р» С„РѕСЂРјРёСЂСѓРµС‚СЃСЏ РёСЃРєР»СЋС‡РёС‚РµР»СЊРЅРѕ РІРѕ РІСЂРµРјРµРЅРЅРѕРј РєР°С‚Р°Р»РѕРіРµ
                    intermediatePath = System.IO.Path.Combine(tempDecompDir, "inter_" + Guid.NewGuid().ToString("N").Substring(0, 6) + uncompressedExt);
                }

                string listFile = System.IO.Path.Combine(tempDecompDir, $"list_conv_{Guid.NewGuid().ToString("N").Substring(0, 8)}.txt");
                System.IO.File.WriteAllLines(listFile, finalInputFilesList, new System.Text.UTF8Encoding(false));

                bool hasMods = finalInputFilesList.Any(d => Directory.Exists(d) && 
                    (System.IO.Path.GetFileName(d).Equals("romfs", StringComparison.OrdinalIgnoreCase) || 
                     System.IO.Path.GetFileName(d).Equals("exefs", StringComparison.OrdinalIgnoreCase) ||
                     System.IO.Path.GetFileName(d).Equals("exefs_patches", StringComparison.OrdinalIgnoreCase) ||
                     System.IO.Path.GetFileName(d).Equals("cheats", StringComparison.OrdinalIgnoreCase) ||
                     d.Contains("romfs", StringComparison.OrdinalIgnoreCase) || 
                     d.Contains("exefs", StringComparison.OrdinalIgnoreCase) ||
                     d.Contains("cheat", StringComparison.OrdinalIgnoreCase) ||
                     d.Contains("С‡РёС‚", StringComparison.OrdinalIgnoreCase) ||
                     d.Contains("mod", StringComparison.OrdinalIgnoreCase) ||
                     d.Contains("РјРѕРґ", StringComparison.OrdinalIgnoreCase)));

                string? savedBaseFile = null;
                string? savedUpdateFile = null;
                bool hasPatchedBase = false;

                // 4. РџРѕРёСЃРє Base Рё Update Рё СѓРјРЅС‹Р№ Р°РЅР°Р»РёР· РјРµС‚РѕРґР° СЃР±РѕСЂРєРё (Smart Processing)
                string? baseFile = null;
                string? updateFile = null;
                string titleIdStr = "";
                
                foreach (var f in finalInputFilesList)
                {
                    if (Directory.Exists(f)) continue;
                    string tid = "";
                    try 
                    {
                        var info = App.SwitchFormat.ParseNsp(f);
                        if (info.ContentType == "Application") baseFile = f;
                        else if (info.ContentType == "Patch") updateFile = f;
                        tid = (info.TitleId ?? "").Trim().ToUpperInvariant();
                    }
                    catch { }

                    if (string.IsNullOrEmpty(tid))
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(f, @"\[([0-9A-Fa-f]{16})\]");
                        if (match.Success) tid = match.Groups[1].Value.ToUpperInvariant();
                    }

                    if (!string.IsNullOrEmpty(tid) && tid.Length == 16)
                    {
                        if (tid.EndsWith("000"))
                        {
                            if (string.IsNullOrEmpty(baseFile)) baseFile = f;
                            if (string.IsNullOrEmpty(titleIdStr)) titleIdStr = tid;
                        }
                        else if (tid.EndsWith("800"))
                        {
                            if (string.IsNullOrEmpty(updateFile)) updateFile = f;
                            if (string.IsNullOrEmpty(titleIdStr)) titleIdStr = tid.Substring(0, 13) + "000";
                        }
                    }
                }
                
                if (string.IsNullOrEmpty(baseFile)) baseFile = finalInputFilesList.FirstOrDefault(f => !Directory.Exists(f) && !f.Contains("DLC", StringComparison.OrdinalIgnoreCase) && (f.Contains("[v0]") || f.Contains("v0"))) ?? finalInputFilesList.FirstOrDefault(f => !Directory.Exists(f) && !f.Contains("DLC", StringComparison.OrdinalIgnoreCase) && !f.Contains("v")) ?? "";
                if (string.IsNullOrEmpty(updateFile)) updateFile = finalInputFilesList.FirstOrDefault(f => !Directory.Exists(f) && f != baseFile && !f.Contains("DLC", StringComparison.OrdinalIgnoreCase) && (f.Contains("v") && !f.Contains("v0"))) ?? "";

                long baseSize = (!string.IsNullOrEmpty(baseFile) && File.Exists(baseFile)) ? new FileInfo(baseFile).Length : 0;
                long updateSize = (!string.IsNullOrEmpty(updateFile) && File.Exists(updateFile)) ? new FileInfo(updateFile).Length : 0;

                int effectiveBuildMode = task.BuildMode != 0 ? task.BuildMode : App.Settings.Current.MultiContentBuildMode;
                bool forceHardPatch = (effectiveBuildMode == 1);
                bool skipHardPatch = (effectiveBuildMode == 2) || task.IsMultiProgramTitle;

                if (!skipHardPatch)
                {
                    if (!string.IsNullOrEmpty(baseFile) && (!string.IsNullOrEmpty(updateFile) || hasMods))
                    {
                        savedBaseFile = baseFile;
                        savedUpdateFile = updateFile;

                        App.RunOnUI(() => task.LogDetails += "\nрџ”µ [HardPatch] РЈРјРЅР°СЏ РјРѕРЅРѕР»РёС‚РЅР°СЏ РїРµСЂРµСЃР±РѕСЂРєР° RomFS (РѕР±СЉРµРґРёРЅРµРЅРёРµ Р±Р°Р·С‹, РѕР±РЅРѕРІР»РµРЅРёР№ Рё РјРѕРґРѕРІ)...");
                        if (string.IsNullOrEmpty(titleIdStr))
                        {
                            try {
                                titleIdStr = App.SwitchFormat.ParseNsp(baseFile).TitleId;
                            } catch { }
                            if (string.IsNullOrEmpty(titleIdStr)) {
                                var match = System.Text.RegularExpressions.Regex.Match(baseFile, @"\[([0-9A-Fa-f]{16})\]");
                                if (match.Success) titleIdStr = match.Groups[1].Value;
                            }
                        }

                        // РР·РІР»РµС‡РµРЅРёРµ С‚РѕРєРµРЅРѕРІ СЂР°Р·Р±Р»РѕРєРёСЂРѕРІРєРё РёР· Unlocker DLC РґР»СЏ РїСЂСЏРјРѕР№ РёРЅС‚РµРіСЂР°С†РёРё РІ RomFS РёРіСЂС‹
                        var unlockerRomfsDirs = ExtractUnlockerRomFsDirectories(finalInputFilesList, tempDecompDir, titleIdStr, task, cancellationToken);
                        if (unlockerRomfsDirs.Count > 0)
                        {
                            hasMods = true;
                        }

                        string suffix = string.IsNullOrEmpty(titleIdStr) ? "" : $"_[{titleIdStr}][v0]";
                        string tempHardPatchedNsp = System.IO.Path.Combine(tempDecompDir, $"patched_base{suffix}.nsp");
                        
                        var hpInput = new List<string> { baseFile };
                        if (!string.IsNullOrEmpty(updateFile)) hpInput.Add(updateFile);
                        
                        // Add mod directories (romfs/exefs) and extracted unlocker directories to be processed
                        var modDirs = finalInputFilesList.Where(d => Directory.Exists(d)).ToList();
                        hpInput.AddRange(modDirs);
                        hpInput.AddRange(unlockerRomfsDirs);

                        bool hardPatchSuccess = false;
                        try
                        {
                            await App.HardPatch.PatchUpdateAsync(task, hpInput, tempHardPatchedNsp, cancellationToken, isMultiContent: true);
                            
                            if (System.IO.File.Exists(tempHardPatchedNsp))
                            {
                                long patchedSize = new FileInfo(tempHardPatchedNsp).Length;
                                long minValidSize = Math.Min(25L * 1024 * 1024, (long)(baseSize * 0.2));
                                // Р’Р°Р»РёРґР°С†РёСЏ: РµСЃР»Рё РёСЃС…РѕРґРЅР°СЏ Р±Р°Р·Р° > 50 РњР‘, Р° СЂРµР·СѓР»СЊС‚Р°С‚ С…Р°СЂРґРїР°С‚С‡Р° РјРµРЅСЊС€Рµ РјРёРЅРёРјР°Р»СЊРЅРѕРіРѕ РїРѕСЂРѕРіР° (20% Р±Р°Р·С‹ РёР»Рё < 25 РњР‘) вЂ” СЌС‚Рѕ РїРѕРІСЂРµР¶РґРµРЅРЅС‹Р№ РѕРіСЂС‹Р·РѕРє Р±РµР· RomFS!
                                if (baseSize > 50 * 1024 * 1024 && patchedSize < minValidSize)
                                {
                                    App.Logger.Log($"[HardPatch] Р Р°Р·РјРµСЂ РїРµСЂРµСЃРѕР±СЂР°РЅРЅРѕРіРѕ С„Р°Р№Р»Р° ({patchedSize} Р±Р°Р№С‚) Р°РЅРѕРјР°Р»СЊРЅРѕ РјР°Р» РѕС‚РЅРѕСЃРёС‚РµР»СЊРЅРѕ Р±Р°Р·С‹ ({baseSize} Р±Р°Р№С‚). РћС‚РєР°С‚ Рє РЅР°С‚РёРІРЅРѕР№ СЃР±РѕСЂРєРµ.", Models.LogLevel.Warning);
                                    App.RunOnUI(() => task.LogDetails += $"\nвљ пёЏ [HardPatch] Р Р°Р·РјРµСЂ РїРµСЂРµСЃРѕР±СЂР°РЅРЅРѕРіРѕ С„Р°Р№Р»Р° ({Models.ProcessingTask.FormatSize(patchedSize)}) Р°РЅРѕРјР°Р»СЊРЅРѕ РјР°Р» РѕС‚РЅРѕСЃРёС‚РµР»СЊРЅРѕ Р±Р°Р·С‹ ({Models.ProcessingTask.FormatSize(baseSize)}). РћС‚РєР°С‚ Рє РЅР°С‚РёРІРЅРѕРјСѓ СЃС€РёРІР°РЅРёСЋ РјСѓР»СЊС‚РёРєРѕРЅС‚РµРЅС‚Р°...");
                                    try { System.IO.File.Delete(tempHardPatchedNsp); } catch { }
                                }
                                else if (patchedSize > 0)
                                {
                                    hardPatchSuccess = true;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            App.Logger.Log($"[HardPatch] РСЃРєР»СЋС‡РµРЅРёРµ РјРѕРЅРѕР»РёС‚РЅРѕРіРѕ СЃР»РёСЏРЅРёСЏ: {ex.Message}. РћС‚РєР°С‚ Рє РЅР°С‚РёРІРЅРѕРјСѓ СЃС€РёРІР°РЅРёСЋ РјСѓР»СЊС‚РёРєРѕРЅС‚РµРЅС‚Р°.", Models.LogLevel.Warning);
                            App.RunOnUI(() => task.LogDetails += $"\nвљ пёЏ [HardPatch] РњРѕРЅРѕР»РёС‚РЅРѕРµ СЃР»РёСЏРЅРёРµ РЅРµРІРѕР·РјРѕР¶РЅРѕ ({ex.Message}). Р’С‹РїРѕР»РЅСЏРµС‚СЃСЏ РѕС‚РєР°С‚ Рє РїСЂСЏРјРѕРјСѓ СЃС€РёРІР°РЅРёСЋ РѕСЂРёРіРёРЅР°Р»СЊРЅС‹С… СЂР°Р·РґРµР»РѕРІ...");
                            try { if (System.IO.File.Exists(tempHardPatchedNsp)) System.IO.File.Delete(tempHardPatchedNsp); } catch { }
                        }

                        if (hardPatchSuccess && System.IO.File.Exists(tempHardPatchedNsp))
                        {
                            if (task.IsMultiProgramTitle)
                            {
                                // РњСѓР»СЊС‚Рё-РїСЂРѕРіСЂР°РјРјРЅС‹Р№ СЃР±РѕСЂРЅРёРє (РЅР°РїСЂ. AC Ezio Collection СЃ РЅРµСЃРєРѕР»СЊРєРёРјРё РЅРµР·Р°РІРёСЃРёРјС‹РјРё Application TitleID)
                                App.RunOnUI(() => task.LogDetails += $"\nвљ пёЏ [HardPatch] РњСѓР»СЊС‚Рё-РїСЂРѕРіСЂР°РјРјРЅС‹Р№ СЃР±РѕСЂРЅРёРє. РСЃРїРѕР»СЊР·СѓРµРј РѕСЂРёРіРёРЅР°Р»СЊРЅС‹Рµ СЂР°Р·РґРµР»С‹ РґР»СЏ СЃРѕС…СЂР°РЅРµРЅРёСЏ РІСЃРµС… РїРѕРґ-РёРіСЂ.");
                                App.Logger.Log($"[HardPatch] Multi-program title detected. Discarding patched_base, using originals.", Models.LogLevel.Warning);
                                try { System.IO.File.Delete(tempHardPatchedNsp); } catch { }
                            }
                            else
                            {
                                // РћР±С‹С‡РЅР°СЏ РёРіСЂР° вЂ” РїРµСЂРµСЃРѕР±СЂР°РЅРЅР°СЏ Р±Р°Р·Р° РїРѕР»РЅРѕСЃС‚СЊСЋ Р·Р°РјРµРЅСЏРµС‚ Р±Р°Р·Сѓ Рё РѕР±РЅРѕРІР»РµРЅРёРµ, РёСЃРєР»СЋС‡Р°СЏ РґСѓР±Р»РёРєР°С‚С‹
                                hasPatchedBase = true;
                                finalInputFilesList.RemoveAll(f =>
                                    (!string.IsNullOrEmpty(baseFile) && string.Equals(f, baseFile, StringComparison.OrdinalIgnoreCase)) ||
                                    (!string.IsNullOrEmpty(updateFile) && string.Equals(f, updateFile, StringComparison.OrdinalIgnoreCase)) ||
                                    modDirs.Any(m => string.Equals(f, m, StringComparison.OrdinalIgnoreCase)) ||
                                    unlockerRomfsDirs.Any(u => string.Equals(f, u, StringComparison.OrdinalIgnoreCase)));
                                finalInputFilesList.Add(tempHardPatchedNsp);
                                App.RunOnUI(() => task.LogDetails += "\nрџ”µ [HardPatch] Р¤РёР·РёС‡РµСЃРєР°СЏ РїРµСЂРµСЃР±РѕСЂРєР° СѓСЃРїРµС€РЅРѕ Р·Р°РІРµСЂС€РµРЅР°. Р РµСЃСѓСЂСЃС‹ РѕР±РЅРѕРІР»РµРЅС‹, РґСѓР±Р»РёСЂРѕРІР°РЅРёРµ РёСЃРєР»СЋС‡РµРЅРѕ.");

                                if (finalInputFilesList.Count == 1 && !isTargetXci)
                                {
                                    intermediatePath = await SafeFileOperations.SafeMoveOrReplaceFileAsync(tempHardPatchedNsp, intermediatePath, task, cancellationToken);

                                    if (task.CustomMetadata != null && !hasPatchedBase && System.IO.File.Exists(intermediatePath))
                                    {
                                        await App.ControlEditor.ApplyCustomMetadataAsync(task.CustomMetadata, intermediatePath, task, cancellationToken);
                                    }

                                    if (isCompressedFormat)
                                    {
                                        App.RunOnUI(() =>
                                        {
                                            task.LogDetails += $"\nрџџЎ [РЎР¶Р°С‚РёРµ] Zstandard РІ С„РѕСЂРјР°С‚ {(isDualFormat ? "NSZ" : task.TargetFormat)}...";
                                            App.RunOnUI(() => { task.Status = "РЎР¶Р°С‚РёРµ..."; });
                                        });

                                        await App.NszCompression.CompressToNszAsync(task, intermediatePath, targetDir, cancellationToken);

                                        string expectedNsz = System.IO.Path.ChangeExtension(intermediatePath, compressedExt);
                                        if (!System.IO.File.Exists(expectedNsz))
                                        {
                                            string altNsz = System.IO.Path.Combine(targetDir, System.IO.Path.GetFileNameWithoutExtension(intermediatePath) + compressedExt);
                                            if (System.IO.File.Exists(altNsz)) expectedNsz = altNsz;
                                        }

                                        if (System.IO.File.Exists(expectedNsz))
                                        {
                                            if (!expectedNsz.Equals(finalCompressedPath, StringComparison.OrdinalIgnoreCase))
                                            {
                                                finalCompressedPath = await SafeFileOperations.SafeMoveOrReplaceFileAsync(expectedNsz, finalCompressedPath, task, cancellationToken);
                                            }
                                            compressionSuccess = true;
                                        }

                                        if (!keepUncompressed)
                                        {
                                            SafeFileOperations.SafeDeleteFile(intermediatePath);
                                        }
                                    }

                                    string mainResultPath = keepUncompressed ? intermediatePath : finalCompressedPath;
                                    App.RunOnUI(() =>
                                    {
                                        if (isDualFormat && compressionSuccess && System.IO.File.Exists(intermediatePath) && System.IO.File.Exists(finalCompressedPath))
                                        {
                                            long nspSize = new FileInfo(intermediatePath).Length;
                                            long nszSize = new FileInfo(finalCompressedPath).Length;
                                            task.TargetSize = $"{Models.ProcessingTask.FormatSize(nspSize)} / {Models.ProcessingTask.FormatSize(nszSize)}";
                                            task.LogDetails += $"\nрџ“¦ [Р¤РѕСЂРјР°С‚С‹] РЈСЃРїРµС€РЅРѕ СЃРѕР·РґР°РЅС‹ РѕР±Р° С„Р°Р№Р»Р°:\n  вЂў {System.IO.Path.GetFileName(intermediatePath)} ({Models.ProcessingTask.FormatSize(nspSize)})\n  вЂў {System.IO.Path.GetFileName(finalCompressedPath)} ({Models.ProcessingTask.FormatSize(nszSize)})";
                                        }
                                        else if (System.IO.File.Exists(mainResultPath))
                                        {
                                            long outSize = new System.IO.FileInfo(mainResultPath).Length;
                                            task.TargetSize = Models.ProcessingTask.FormatSize(outSize);
                                            if (task.SourceSizeBytes > 0)
                                            {
                                                long diff = task.SourceSizeBytes - outSize;
                                                double percent = (double)diff / task.SourceSizeBytes * 100.0;
                                                App.RunOnUI(() => { task.SizeDifference = $"{(diff > 0 ? "-" : "+")}{Models.ProcessingTask.FormatSize(Math.Abs(diff))} ({Math.Abs(percent):F1}%)"; });
                                            }
                                        }
                                        App.RunOnUI(() => { task.Progress = 100; });
                                        App.RunOnUI(() => { task.Status = "РЈСЃРїРµС€РЅРѕ"; });
                                        App.RunOnUI(() => { task.IsRunning = false; });
                                        task.LogDetails += "\nвњ… [РЈСЃРїРµС…] РњРѕРЅРѕР»РёС‚РЅС‹Р№ РѕР±СЂР°Р· РёРіСЂС‹ (Base + Update + ExeFS) СѓСЃРїРµС€РЅРѕ СЃРѕР±СЂР°РЅ Рё РіРѕС‚РѕРІ Рє Р·Р°РїСѓСЃРєСѓ!";
                                        StormSwitchBox.Services.HistoryService.AddToHistory(task);
                                    });
                                    DeployCheatsIfPresent(titleIdStr, inputFiles, mainResultPath);
                                    App.Logger.Log($"РњСѓР»СЊС‚Рё-РєРѕРЅС‚РµРЅС‚ СѓСЃРїРµС€РЅРѕ СЃРѕР·РґР°РЅ: {System.IO.Path.GetFileName(mainResultPath)}", LogLevel.Success);
                                    return;
                                }
                            }
                        }
                        else 
                        {
                            App.RunOnUI(() => task.LogDetails += "\nв„№пёЏ РџРµСЂРµСЃР±РѕСЂРєР° HardPatch РїСЂРѕРїСѓС‰РµРЅР°. РџРµСЂРµС…РѕРґРёРј Рє СЃС€РёРІР°РЅРёСЋ РјСѓР»СЊС‚РёРєРѕРЅС‚РµРЅС‚Р°...");
                        }
                    }
                }
                else if (skipHardPatch)
                {
                    App.RunOnUI(() => task.LogDetails += "\nвљ пёЏ [HardPatch] РњСѓР»СЊС‚Рё-РїСЂРѕРіСЂР°РјРјРЅС‹Р№ С‚Р°Р№С‚Р» вЂ” РїСЂРѕРїСѓСЃРє yanu-cli, РёСЃРїРѕР»СЊР·СѓРµРј РѕСЂРёРіРёРЅР°Р»СЊРЅС‹Рµ С„Р°Р№Р»С‹.");
                    App.Logger.Log("[HardPatch] Skipped: multi-program title detected by pre-analysis", LogLevel.Info);
                }

                // 4.3 Р‘С‹СЃС‚СЂС‹Р№ РїСѓС‚СЊ РґР»СЏ РѕРґРёРЅРѕС‡РЅРѕРіРѕ С„Р°Р№Р»Р° (Single-File Fast Path):
                // Р•СЃР»Рё РІС…РѕРґРЅРѕР№ С„Р°Р№Р» СЂРѕРІРЅРѕ РѕРґРёРЅ (Р±Р°Р·РѕРІР°СЏ РёРіСЂР°) Рё РЅРµС‚ РѕР±РЅРѕРІР»РµРЅРёР№/DLC/РјРѕРґРѕРІ, РЅР°РїСЂСЏРјСѓСЋ РїРµСЂРµРјРµС‰Р°РµРј/СЃР¶РёРјР°РµРј Р±РµР· LibHac
                if (finalInputFilesList.Count == 1 && !hasPatchedBase && !isTargetXci)
                {
                    string singleFile = finalInputFilesList[0];
                    bool isDecompressedTemp = !string.IsNullOrEmpty(tempDecompDir) && singleFile.StartsWith(tempDecompDir, StringComparison.OrdinalIgnoreCase);
                    bool isSamePath = string.Equals(System.IO.Path.GetFullPath(singleFile), System.IO.Path.GetFullPath(intermediatePath), StringComparison.OrdinalIgnoreCase);

                    if (!isSamePath)
                    {
                        if (isDecompressedTemp)
                        {
                            intermediatePath = await SafeFileOperations.SafeMoveOrReplaceFileAsync(singleFile, intermediatePath, task, cancellationToken);
                        }
                        else
                        {
                            SafeFileOperations.ResetFileAttributesSafe(singleFile);
                            if (System.IO.File.Exists(intermediatePath))
                            {
                                SafeFileOperations.ResetFileAttributesSafe(intermediatePath);
                            }
                            System.IO.File.Copy(singleFile, intermediatePath, overwrite: true);
                        }
                    }

                    if (task.CustomMetadata != null && System.IO.File.Exists(intermediatePath))
                    {
                        await App.ControlEditor.ApplyCustomMetadataAsync(task.CustomMetadata, intermediatePath, task, cancellationToken);
                    }

                    if (isCompressedFormat)
                    {
                        App.RunOnUI(() =>
                        {
                            task.LogDetails += $"\nрџџЎ [РЎР¶Р°С‚РёРµ] Zstandard РІ С„РѕСЂРјР°С‚ {(isDualFormat ? "NSZ" : task.TargetFormat)}...";
                            App.RunOnUI(() => { task.Status = "РЎР¶Р°С‚РёРµ..."; });
                        });

                        await App.NszCompression.CompressToNszAsync(task, intermediatePath, targetDir, cancellationToken);

                        string expectedNsz = System.IO.Path.ChangeExtension(intermediatePath, compressedExt);
                        if (!System.IO.File.Exists(expectedNsz))
                        {
                            string altNsz = System.IO.Path.Combine(targetDir, System.IO.Path.GetFileNameWithoutExtension(intermediatePath) + compressedExt);
                            if (System.IO.File.Exists(altNsz)) expectedNsz = altNsz;
                        }

                        if (System.IO.File.Exists(expectedNsz))
                        {
                            if (!expectedNsz.Equals(finalCompressedPath, StringComparison.OrdinalIgnoreCase))
                            {
                                finalCompressedPath = await SafeFileOperations.SafeMoveOrReplaceFileAsync(expectedNsz, finalCompressedPath, task, cancellationToken);
                            }
                            compressionSuccess = true;
                        }

                        if (!keepUncompressed)
                        {
                            if (!isSamePath || isDecompressedTemp)
                            {
                                SafeFileOperations.SafeDeleteFile(intermediatePath);
                            }
                        }
                    }

                    string mainResultPath = keepUncompressed ? intermediatePath : finalCompressedPath;
                    App.RunOnUI(() =>
                    {
                        if (isDualFormat && compressionSuccess && System.IO.File.Exists(intermediatePath) && System.IO.File.Exists(finalCompressedPath))
                        {
                            long nspSize = new FileInfo(intermediatePath).Length;
                            long nszSize = new FileInfo(finalCompressedPath).Length;
                            task.TargetSize = $"{Models.ProcessingTask.FormatSize(nspSize)} / {Models.ProcessingTask.FormatSize(nszSize)}";
                            task.LogDetails += $"\nрџ“¦ [Р¤РѕСЂРјР°С‚С‹] РЈСЃРїРµС€РЅРѕ СЃРѕР·РґР°РЅС‹ РѕР±Р° С„Р°Р№Р»Р°:\n  вЂў {System.IO.Path.GetFileName(intermediatePath)} ({Models.ProcessingTask.FormatSize(nspSize)})\n  вЂў {System.IO.Path.GetFileName(finalCompressedPath)} ({Models.ProcessingTask.FormatSize(nszSize)})";
                        }
                        else if (System.IO.File.Exists(mainResultPath))
                        {
                            long outSize = new System.IO.FileInfo(mainResultPath).Length;
                            task.TargetSize = Models.ProcessingTask.FormatSize(outSize);
                            if (task.SourceSizeBytes > 0)
                            {
                                long diff = task.SourceSizeBytes - outSize;
                                double percent = (double)diff / task.SourceSizeBytes * 100.0;
                                App.RunOnUI(() => { task.SizeDifference = $"{(diff > 0 ? "-" : "+")}{Models.ProcessingTask.FormatSize(Math.Abs(diff))} ({Math.Abs(percent):F1}%)"; });
                            }
                        }
                        App.RunOnUI(() => { task.Progress = 100; });
                        App.RunOnUI(() => { task.Status = "РЈСЃРїРµС€РЅРѕ"; });
                        App.RunOnUI(() => { task.IsRunning = false; });
                        task.LogDetails += "\nвњ… [РЈСЃРїРµС…] РћР±СЂР°Р±РѕС‚РєР° С„Р°Р№Р»Р° СѓСЃРїРµС€РЅРѕ Р·Р°РІРµСЂС€РµРЅР°!";
                        StormSwitchBox.Services.HistoryService.AddToHistory(task);
                    });
                    DeployCheatsIfPresent(titleIdStr, inputFiles, mainResultPath);
                    App.Logger.Log($"Р¤Р°Р№Р» СѓСЃРїРµС€РЅРѕ РѕР±СЂР°Р±РѕС‚Р°РЅ: {System.IO.Path.GetFileName(mainResultPath)}", LogLevel.Success);
                    return;
                }

                // 4.5 РЎС€РёРІР°РЅРёРµ РјСѓР»СЊС‚РёРєРѕРЅС‚РµРЅС‚Р° С‡РµСЂРµР· РЅР°С‚РёРІРЅС‹Р№ РґРІРёР¶РѕРє LibHac PFS0
                App.RunOnUI(() =>
                {
                    task.LogDetails += "\nрџ“¦ [NSC_Builder] РЎС€РёРІР°РЅРёРµ РјСѓР»СЊС‚РёРєРѕРЅС‚РµРЅС‚Р°...";
                    App.RunOnUI(() => { task.Status = "РЎР±РѕСЂРєР°..."; });
                });

                // isTargetXci declared at method start
                
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

                App.EnsureUserKeysAvailable();

                var sortedList = new List<string>();
                string? mainApp = null;
                string? patchApp = null;
                var dlcs = new List<string>();

                foreach (var f in finalInputFilesList)
                {
                    if (Directory.Exists(f)) continue;

                    bool isBase = false;
                    bool isPatch = false;
                    bool isDlc = false;

                    try
                    {
                        var info = App.SwitchFormat.ParseNsp(f);
                        if (info.ContentType == "Application") isBase = true;
                        else if (info.ContentType == "Patch") isPatch = true;
                        else if (info.ContentType == "AddOnContent") isDlc = true;
                    } 
                    catch { }

                    if (!isBase && !isPatch && !isDlc)
                    {
                        string tid = "";
                        var match = System.Text.RegularExpressions.Regex.Match(f, @"\[([0-9A-Fa-f]{16})\]");
                        if (match.Success) tid = match.Groups[1].Value.ToUpperInvariant();

                        if (!string.IsNullOrEmpty(tid) && tid.Length == 16)
                        {
                            if (tid.EndsWith("000")) isBase = true;
                            else if (tid.EndsWith("800")) isPatch = true;
                            else isDlc = true;
                        }
                        else
                        {
                            if (f.Contains("DLC", StringComparison.OrdinalIgnoreCase) || f.Contains("AddOn", StringComparison.OrdinalIgnoreCase)) isDlc = true;
                            else if (f.Contains("[v0]") || f.EndsWith("v0.nsp", StringComparison.OrdinalIgnoreCase) || f.Contains("patched_base")) isBase = true;
                            else if (f.Contains("v") && !f.Contains("v0")) isPatch = true;
                        }
                    }

                    if (isDlc) dlcs.Add(f);
                    else if (isBase && mainApp == null) mainApp = f;
                    else if (isPatch && patchApp == null) patchApp = f;
                    else dlcs.Add(f);
                }

                if (!string.IsNullOrEmpty(mainApp)) sortedList.Add(mainApp);
                if (!string.IsNullOrEmpty(patchApp)) sortedList.Add(patchApp);
                sortedList.AddRange(dlcs);

                if (sortedList.Count == 0) sortedList = finalInputFilesList.Where(f => !Directory.Exists(f)).ToList();

                string outFolder = System.IO.Path.Combine(tempDecompDir, "libhac_out");
                Directory.CreateDirectory(outFolder);

                bool buildDone = false;
                
                App.RunOnUI(() => task.LogDetails += "\nрџ“¦ [LibHac] РќР°С‚РёРІРЅР°СЏ СЃР±РѕСЂРєР° Multi-NSP (PFS0)...");

                try
                {
                    var pfsBuilder = new PartitionFileSystemBuilder();
                    var mergedEntries = new Dictionary<string, LibHac.Fs.Fsa.IFile>(StringComparer.OrdinalIgnoreCase);
                    var openedFs = new List<PartitionFileSystem>();
                    var openedStreams = new List<FileStream>();
                    var openedFiles = new List<LibHac.Fs.Fsa.IFile>();

                    try
                    {
                        var scanList = new List<string>();
                        if (!string.IsNullOrEmpty(mainApp) && System.IO.File.Exists(mainApp)) scanList.Add(mainApp);
                        foreach (var f in sortedList)
                        {
                            if (!scanList.Contains(f, StringComparer.OrdinalIgnoreCase) && System.IO.File.Exists(f))
                                scanList.Add(f);
                        }
                        foreach (var f in finalInputFilesList)
                        {
                            if (!System.IO.Directory.Exists(f) && !scanList.Contains(f, StringComparer.OrdinalIgnoreCase) && System.IO.File.Exists(f))
                                scanList.Add(f);
                        }

                        // Р•СЃР»Рё С†РµР»РµРІРѕР№ С„РѕСЂРјР°С‚ РЅРµСЃР¶Р°С‚С‹Р№ NSP/XCI, Р° С‡Р°СЃС‚СЊ С„Р°Р№Р»РѕРІ вЂ” NSZ/XCZ/XCI, РїСЂРµРґРІР°СЂРёС‚РµР»СЊРЅРѕ СЂР°СЃРїР°РєРѕРІС‹РІР°РµРј РёС…
                        var processedScanList = new List<string>();
                        for (int i = 0; i < scanList.Count; i++)
                        {
                            string fPath = scanList[i];
                            string ext = System.IO.Path.GetExtension(fPath).ToLowerInvariant();

                            if (!isCompressedFormat && (ext == ".nsz" || ext == ".xcz" || ext == ".xci"))
                            {
                                string nspName = $"src_{i}_{System.IO.Path.GetFileNameWithoutExtension(fPath)}.nsp";
                                string targetNspPath = System.IO.Path.Combine(tempDecompDir, nspName);
                                string itemDecompDir = System.IO.Path.Combine(tempDecompDir, $"decomp_{i}");
                                Directory.CreateDirectory(itemDecompDir);

                                App.RunOnUI(() => task.LogDetails += $"\nрџ“¦ [LibHac] Р Р°СЃРїР°РєРѕРІРєР° {System.IO.Path.GetFileName(fPath)} -> {nspName}...");

                                string? decompResult = await App.NszCompression.DecompressNszAsync(task, fPath, itemDecompDir, cancellationToken);
                                if (decompResult != null)
                                {
                                    var producedNsp = new DirectoryInfo(itemDecompDir).GetFiles("*.nsp")
                                        .OrderByDescending(f => f.Length)
                                        .FirstOrDefault();

                                    if (producedNsp != null)
                                    {
                                        targetNspPath = await SafeFileOperations.SafeMoveOrReplaceFileAsync(producedNsp.FullName, targetNspPath, task, cancellationToken);
                                        try { Directory.Delete(itemDecompDir, true); } catch { }
                                        processedScanList.Add(targetNspPath);
                                        continue;
                                    }
                                }
                                try { Directory.Delete(itemDecompDir, true); } catch { }
                            }

                            processedScanList.Add(fPath);
                        }

                        var baseEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        var deltaNcaNamesToExclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                        if (App.Settings.Current.RemoveDeltaNca)
                        {
                            var allScanFiles = processedScanList.Concat(inputFiles).Distinct().ToList();
                            deltaNcaNamesToExclude = DetectDeltaNcasToExclude(allScanFiles);
                        }

                        // 1. РЎРєР°РЅРёСЂСѓРµРј РѕСЃРЅРѕРІРЅС‹Рµ С„Р°Р№Р»С‹ СЃР±РѕСЂРєРё (Base/Patched Base + DLCs + Unlockers)
                        for (int scanIdx = 0; scanIdx < processedScanList.Count; scanIdx++)
                        {
                            string nspPath = processedScanList[scanIdx];
                            if (!System.IO.File.Exists(nspPath)) continue;
                            
                            var stream = new FileStream(nspPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                            openedStreams.Add(stream);
                            var fs = new PartitionFileSystem(stream.AsStorage());
                            openedFs.Add(fs);

                            bool isMainGame = (scanIdx == 0);
                            
                            foreach (var entry in fs.EnumerateEntries())
                            {
                                if (entry.Type == LibHac.Fs.DirectoryEntryType.Directory) continue;
                                string name = entry.Name;
                                
                                if (isMainGame)
                                {
                                    baseEntries.Add(name);
                                }

                                if (App.Settings.Current.RemoveDeltaNca && deltaNcaNamesToExclude.Contains(name))
                                {
                                    App.Logger.Log($"[Delta Cleaner] РџСЂРѕРїСѓС‰РµРЅ РјСѓСЃРѕСЂРЅС‹Р№ Delta NCA: {name} (СЌРєРѕРЅРѕРјРёСЏ РјРµСЃС‚Р°)", Models.LogLevel.Info);
                                    App.RunOnUI(() => task.LogDetails += $"\nрџ—‘пёЏ [Delta Cleaner] РЈРґР°Р»РµРЅ РјСѓСЃРѕСЂРЅС‹Р№ Delta NCA: {name}");
                                    continue;
                                }

                                if (mergedEntries.ContainsKey(name) || !IsValidNspEntry(name)) continue;

                                var file = OpenFileSafe(fs, entry.FullPath);
                                openedFiles.Add(file);
                                mergedEntries[name] = file;
                            }
                        }

                        // 2. РЎРѕС…СЂР°РЅСЏРµРј С‚РёРєРµС‚С‹ (.tik) Рё СЃРµСЂС‚РёС„РёРєР°С‚С‹ (.cert) РёР· РѕСЂРёРіРёРЅР°Р»СЊРЅС‹С… С„Р°Р№Р»РѕРІ.
                        // Р•СЃР»Рё Р±С‹Р»Р° РІС‹РїРѕР»РЅРµРЅР° РїРµСЂРµСЃР±РѕСЂРєР° HardPatch (hasPatchedBase == true), С‚Рѕ Patch CNMT РќР• РІРЅРµРґСЂСЏРµС‚СЃСЏ,
                        // С‚Р°Рє РєР°Рє РѕР±РЅРѕРІР»РµРЅРёРµ СѓР¶Рµ С„РёР·РёС‡РµСЃРєРё РІС€РёС‚Рѕ РІ РµРґРёРЅС‹Р№ Program NCA РїРµСЂРµСЃРѕР±СЂР°РЅРЅРѕР№ Р±Р°Р·С‹.
                        var extraSources = new List<string>();
                        if (!string.IsNullOrEmpty(savedUpdateFile) && File.Exists(savedUpdateFile)) extraSources.Add(savedUpdateFile);
                        if (!string.IsNullOrEmpty(savedBaseFile) && File.Exists(savedBaseFile)) extraSources.Add(savedBaseFile);

                        foreach (var extraPath in extraSources)
                        {
                            try
                            {
                                var stream = new FileStream(extraPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                                openedStreams.Add(stream);
                                var fs = new PartitionFileSystem(stream.AsStorage());
                                openedFs.Add(fs);

                                foreach (var entry in fs.EnumerateEntries())
                                {
                                    if (entry.Type == LibHac.Fs.DirectoryEntryType.Directory) continue;
                                    string name = entry.Name;
                                    string lower = name.ToLowerInvariant();

                                    // РР·РІР»РµРєР°РµРј С‚РёРєРµС‚С‹ (.tik), СЃРµСЂС‚РёС„РёРєР°С‚С‹ (.cert) Рё Patch CNMT (.cnmt.nca)
                                    bool isTicketOrCert = lower.EndsWith(".tik") || lower.EndsWith(".cert");
                                    bool isPatchCnmt = !hasPatchedBase && (lower.EndsWith(".cnmt.nca") || lower.EndsWith(".cnmt.xml"));

                                    if (isTicketOrCert || isPatchCnmt)
                                    {
                                        if (!mergedEntries.ContainsKey(name))
                                        {
                                            var file = OpenFileSafe(fs, entry.FullPath);
                                            openedFiles.Add(file);
                                            mergedEntries[name] = file;
                                            if (isTicketOrCert)
                                            {
                                                App.Logger.Log($"[LibHac] Р’С€РёС‚ С‚РёРєРµС‚/СЃРµСЂС‚РёС„РёРєР°С‚ Unlocker: {name}", Models.LogLevel.Info);
                                            }
                                            else if (isPatchCnmt)
                                            {
                                                App.Logger.Log($"[LibHac] Р’С€РёС‚ Update Patch CNMT: {name}", Models.LogLevel.Info);
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }
                        }

                        // Strictly order entries so Base CNMT (0), Control/Icon NCA (1), Program NCA (2) are FIRST
                        ulong baseTitleId = 0;
                        if (!string.IsNullOrEmpty(mainApp))
                        {
                            try
                            {
                                var info = App.SwitchFormat.ParseNsp(mainApp);
                                if (!string.IsNullOrEmpty(info.TitleId) && ulong.TryParse(info.TitleId, System.Globalization.NumberStyles.HexNumber, null, out ulong parsedTid))
                                {
                                    baseTitleId = parsedTid;
                                }
                            }
                            catch { }
                        }
                        if (baseTitleId == 0 && !string.IsNullOrEmpty(savedBaseFile))
                        {
                            try
                            {
                                var info = App.SwitchFormat.ParseNsp(savedBaseFile);
                                if (!string.IsNullOrEmpty(info.TitleId) && ulong.TryParse(info.TitleId, System.Globalization.NumberStyles.HexNumber, null, out ulong parsedTid))
                                {
                                    baseTitleId = parsedTid;
                                }
                            }
                            catch { }
                        }
                        if (baseTitleId == 0)
                        {
                            string combinedHint = $"{task.OutputFileName} {mainApp} {savedBaseFile} {task.GroupId} " + string.Join(" ", inputFiles);
                            var m = System.Text.RegularExpressions.Regex.Match(combinedHint, @"0100[0-9A-Fa-f]{12}");
                            if (m.Success && ulong.TryParse(m.Value, System.Globalization.NumberStyles.HexNumber, null, out ulong regexTid))
                            {
                                baseTitleId = regexTid;
                            }
                        }



                        if (App.Settings.Current.EnableDlcCompletenessCheck && baseTitleId != 0)
                        {
                            var presentDlcIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var f in inputFiles)
                            {
                                try
                                {
                                    var fInfo = App.SwitchFormat.ParseNsp(f);
                                    if (!string.IsNullOrEmpty(fInfo.TitleId) && fInfo.ContentType == "AddOnContent")
                                    {
                                        presentDlcIds.Add(fInfo.TitleId);
                                    }
                                }
                                catch { }
                            }

                            if (presentDlcIds.Count > 0)
                            {
                                var dlcReport = App.TitleDb.CheckDlcCompleteness(baseTitleId.ToString("X16"), presentDlcIds);
                                App.RunOnUI(() =>
                                {
                                    task.LogDetails += $"\nрџ“¦ [РРЅСЃРїРµРєС‚РѕСЂ DLC] {dlcReport.SummaryText}";
                                    if (dlcReport.MissingDlcs.Count > 0 && dlcReport.MissingDlcs.Count <= 5)
                                    {
                                        foreach (var missing in dlcReport.MissingDlcs)
                                        {
                                            task.LogDetails += $"\n  вЂў [0x{missing.Id:X16}] {missing.Name ?? "РќРµРёР·РІРµСЃС‚РЅРѕРµ РґРѕРїРѕР»РЅРµРЅРёРµ"}";
                                        }
                                    }
                                });
                            }
                        }

                        if (App.Settings.Current.EnableRsvCap && App.Settings.Current.RsvCap > 0)
                        {
                            App.RunOnUI(() => task.LogDetails += $"\nрџ›ЎпёЏ [RSV Cap] РџСЂРёРјРµРЅРµРЅ Р»РёРјРёС‚ РјРёРЅРёРјР°Р»СЊРЅРѕР№ РІРµСЂСЃРёРё СЃРёСЃС‚РµРјС‹ (RSV Cap: {App.Settings.Current.RsvCap})");
                        }

                        var orderedEntries = mergedEntries
                            .OrderBy(kvp => GetNcaPriority(kvp.Value, kvp.Key, baseTitleId, baseEntries))
                            .ThenBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);

                        foreach (var kvp in orderedEntries)
                        {
                            pfsBuilder.AddFile(kvp.Key, new LibHac.FsSystem.StorageFile(new StormSwitchBox.Services.SafeStorageWrapper(kvp.Value.AsStorage()), LibHac.Fs.OpenMode.Read));
                        }

                        string outputNspPath = System.IO.Path.Combine(outFolder, $"multi_out_{Guid.NewGuid().ToString("N").Substring(0, 8)}.nsp");

                        using (var builtPfs = pfsBuilder.Build(PartitionFileSystemType.Standard))
                        {
                            builtPfs.GetSize(out long totalPfsSize).ThrowIfFailure();
                            
                            FileStream destStream;
                            try
                            {
                                var fsOptions = new FileStreamOptions
                                {
                                    Mode = FileMode.Create,
                                    Access = FileAccess.Write,
                                    Share = FileShare.None,
                                    BufferSize = 8 * 1024 * 1024,
                                    Options = FileOptions.SequentialScan
                                };
                                destStream = new FileStream(outputNspPath, fsOptions);
                            }
                            catch
                            {
                                destStream = new FileStream(outputNspPath, FileMode.Create, FileAccess.Write, FileShare.None, 4 * 1024 * 1024);
                            }

                            using (destStream)
                            {
                                long remaining = totalPfsSize;
                                long offset = 0;
                                int chunkSize = 8 * 1024 * 1024;
                                byte[] rentedBuffer = System.Buffers.ArrayPool<byte>.Shared.Rent(chunkSize);
                                var sw = System.Diagnostics.Stopwatch.StartNew();

                                try
                                {
                                    while (remaining > 0)
                                    {
                                        cancellationToken.ThrowIfCancellationRequested();
                                        int toRead = (int)Math.Min(chunkSize, remaining);
                                        builtPfs.Read(offset, rentedBuffer.AsSpan(0, toRead)).ThrowIfFailure();
                                        destStream.Write(rentedBuffer, 0, toRead);
                                        offset += toRead;
                                        remaining -= toRead;

                                        if (sw.ElapsedMilliseconds > 350 || remaining == 0)
                                        {
                                            sw.Restart();
                                            double pct = (double)offset / totalPfsSize * 100.0;
                                            App.RunOnUI(() => task.Progress = Math.Min(99.9, pct));
                                        }
                                    }
                                }
                                finally
                                {
                                    System.Buffers.ArrayPool<byte>.Shared.Return(rentedBuffer);
                                }
                            }
                        }

                        if (isTargetXci)
                        {
                            App.RunOnUI(() => task.LogDetails += "\nрџ”„ [РљРѕРЅРІРµСЂС‚Р°С†РёСЏ] РЎР±РѕСЂРєР° XCI РёР· Multi-NSP (4nxci)...");
                            await App.SwitchFormat.ConvertContainerAsync(task, outputNspPath, outFolder, "XCI", cancellationToken);
                            try { if (File.Exists(outputNspPath)) File.Delete(outputNspPath); } catch { }
                        }

                        buildDone = true;
                    }
                    finally
                    {
                        foreach (var f in openedFiles) { try { f.Dispose(); } catch { } }
                        foreach (var s in openedStreams) { try { s.Dispose(); } catch { } }
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"РќРµ СѓРґР°Р»РѕСЃСЊ СЃРѕР±СЂР°С‚СЊ РјСѓР»СЊС‚РёРєРѕРЅС‚РµРЅС‚: {ex.Message}");
                }

                if (!buildDone)
                {
                    throw new Exception("РќРµ СѓРґР°Р»РѕСЃСЊ СЃРѕР·РґР°С‚СЊ РјСѓР»СЊС‚РёРєРѕРЅС‚РµРЅС‚ вЂ” СЃС€РёРІР°РЅРёРµ Р·Р°РІРµСЂС€РёР»РѕСЃСЊ Р±РµР· СЂРµР·СѓР»СЊС‚Р°С‚Р°.");
                }

                // Search for the actual content file (.nsp/.xci), skipping metadata like .cnmt.xml
                string[] contentExtensions = new[] { ".nsp", ".xci", ".nsz", ".xcz" };
                string? generatedFile = Directory.GetFiles(outFolder)
                    .Where(f => contentExtensions.Any(ext => f.EndsWith(ext, StringComparison.OrdinalIgnoreCase)))
                    .OrderByDescending(f => new System.IO.FileInfo(f).Length)
                    .FirstOrDefault();

                string formattedPath = FormatOutputFileName(outPath, inputFiles);
                if (!string.IsNullOrEmpty(formattedPath) && !formattedPath.Equals(outPath, StringComparison.OrdinalIgnoreCase))
                {
                    if (intermediatePath.Equals(outPath, StringComparison.OrdinalIgnoreCase))
                        intermediatePath = formattedPath;
                    outPath = formattedPath;
                }

                if (System.IO.File.Exists(generatedFile) && !generatedFile.Equals(intermediatePath, StringComparison.OrdinalIgnoreCase))
                {
                    intermediatePath = await SafeFileOperations.SafeMoveOrReplaceFileAsync(generatedFile, intermediatePath, task, cancellationToken);
                    if (!isCompressedFormat)
                    {
                        outPath = intermediatePath;
                    }
                }

                // РџСЂРёРјРµРЅСЏРµРј РєР°СЃС‚РѕРјРЅС‹Рµ РјРµС‚Р°РґР°РЅРЅС‹Рµ / РёРєРѕРЅРєСѓ, РµСЃР»Рё РѕРЅРё Р·Р°РґР°РЅС‹ РїРѕР»СЊР·РѕРІР°С‚РµР»РµРј (Рё РµС‰Рµ РЅРµ РїСЂРёРјРµРЅРµРЅС‹ РІ HardPatch)
                if (task.CustomMetadata != null && !hasPatchedBase && System.IO.File.Exists(intermediatePath) && intermediatePath.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase))
                {
                    await App.ControlEditor.ApplyCustomMetadataAsync(task.CustomMetadata, intermediatePath, task, cancellationToken);
                }

                // 5. Zstandard РЎР¶Р°С‚РёРµ (NSZ/XCZ), РµСЃР»Рё РЅРµРѕР±С…РѕРґРёРјРѕ
                if (isCompressedFormat)
                {
                    App.RunOnUI(() =>
                    {
                        task.LogDetails += $"\nрџџЎ [РЎР¶Р°С‚РёРµ] Zstandard РІ С„РѕСЂРјР°С‚ {(isDualFormat ? (isTargetXci ? "XCZ" : "NSZ") : task.TargetFormat)}...";
                        App.RunOnUI(() => { task.Status = "РЎР¶Р°С‚РёРµ..."; });
                    });
                    
                    await App.NszCompression.CompressToNszAsync(task, intermediatePath, targetDir, cancellationToken);
                    
                    string expectedNsz = System.IO.Path.ChangeExtension(intermediatePath, compressedExt);
                    if (!System.IO.File.Exists(expectedNsz))
                    {
                        string altNsz = System.IO.Path.Combine(targetDir, System.IO.Path.GetFileNameWithoutExtension(intermediatePath) + compressedExt);
                        if (System.IO.File.Exists(altNsz)) expectedNsz = altNsz;
                    }
                    
                    compressionSuccess = false;
                    if (System.IO.File.Exists(expectedNsz) && new FileInfo(expectedNsz).Length > 0)
                    {
                        if (!expectedNsz.Equals(finalCompressedPath, StringComparison.OrdinalIgnoreCase))
                        {
                            finalCompressedPath = await SafeFileOperations.SafeMoveOrReplaceFileAsync(expectedNsz, finalCompressedPath, task, cancellationToken);
                        }
                        compressionSuccess = true;
                    }
                    
                    if (compressionSuccess)
                    {
                        if (!keepUncompressed)
                        {
                            SafeFileOperations.SafeDeleteFile(intermediatePath);
                        }
                    }
                    else
                    {
                        App.RunOnUI(() => task.LogDetails += "\nвљ пёЏ [Р’РЅРёРјР°РЅРёРµ] РЎР¶Р°С‚РёРµ РЅРµ СѓРґР°Р»РѕСЃСЊ. РЎРѕС…СЂР°РЅРµРЅ РёСЃС…РѕРґРЅС‹Р№ РѕР±СЂР°Р·.");
                    }
                }

                string finalMainPath = keepUncompressed ? intermediatePath : (compressionSuccess ? finalCompressedPath : intermediatePath);

                App.RunOnUI(() =>
                {
                    if (isDualFormat && compressionSuccess && System.IO.File.Exists(intermediatePath) && System.IO.File.Exists(finalCompressedPath))
                    {
                        long uncompSize = new FileInfo(intermediatePath).Length;
                        long compSize = new FileInfo(finalCompressedPath).Length;
                        task.TargetSize = $"{Models.ProcessingTask.FormatSize(uncompSize)} / {Models.ProcessingTask.FormatSize(compSize)}";
                        task.LogDetails += $"\nрџ“¦ [Р¤РѕСЂРјР°С‚С‹] РЈСЃРїРµС€РЅРѕ СЃРѕР·РґР°РЅС‹ РѕР±Р° С„Р°Р№Р»Р°:\n  вЂў {System.IO.Path.GetFileName(intermediatePath)} ({Models.ProcessingTask.FormatSize(uncompSize)})\n  вЂў {System.IO.Path.GetFileName(finalCompressedPath)} ({Models.ProcessingTask.FormatSize(compSize)})";
                    }
                    else if (System.IO.File.Exists(finalMainPath))
                    {
                        long outSize = new System.IO.FileInfo(finalMainPath).Length;
                        task.TargetSize = Models.ProcessingTask.FormatSize(outSize);
                        if (task.SourceSizeBytes > 0)
                        {
                            long diff = task.SourceSizeBytes - outSize;
                            double percent = (double)diff / task.SourceSizeBytes * 100.0;
                            App.RunOnUI(() => { task.SizeDifference = $"{(diff > 0 ? "-" : "+")}{Models.ProcessingTask.FormatSize(Math.Abs(diff))} ({Math.Abs(percent):F1}%)"; });
                        }
                        task.LogDetails += $"\nвњ… [Р“РѕС‚РѕРІРѕ] РЎРѕС…СЂР°РЅРµРЅ: {System.IO.Path.GetFileName(finalMainPath)}";
                    }

                    App.RunOnUI(() => { task.Progress = 100; });
                    App.RunOnUI(() => { task.Status = "РЈСЃРїРµС€РЅРѕ"; });
                    App.RunOnUI(() => { task.IsRunning = false; });
                    StormSwitchBox.Services.HistoryService.AddToHistory(task);
                });

                DeployCheatsIfPresent(titleIdStr, inputFiles, finalMainPath);
                App.Logger.Log($"РњСѓР»СЊС‚Рё-РєРѕРЅС‚РµРЅС‚ СѓСЃРїРµС€РЅРѕ СЃРѕР·РґР°РЅ: {System.IO.Path.GetFileName(finalMainPath)}", LogLevel.Success);
            }
            catch (OperationCanceledException)
            {
                App.RunOnUI(() => { task.Status = "РћС‚РјРµРЅРµРЅ"; task.IsRunning = false; StormSwitchBox.Services.HistoryService.AddToHistory(task); });
            }
            catch (Exception ex)
            {
                string errText = !string.IsNullOrWhiteSpace(ex.Message) ? ex.Message : ex.GetType().Name;
                if (ex.InnerException != null && !string.IsNullOrWhiteSpace(ex.InnerException.Message))
                {
                    errText += $" ({ex.InnerException.Message})";
                }
                App.RunOnUI(() => { task.Status = "РћС€РёР±РєР°"; task.IsRunning = false; task.LogDetails += $"\nрџ”ґ [РћС€РёР±РєР°] {errText}"; StormSwitchBox.Services.HistoryService.AddToHistory(task); });
                string operationName = task.Operation == "Update" ? "РѕР±РЅРѕРІР»РµРЅРёСЏ" : "СЃР±РѕСЂРєРё РјСѓР»СЊС‚Рё-РєРѕРЅС‚РµРЅС‚Р°";
                App.Logger.Log($"РћС€РёР±РєР° {operationName}: {ex.ToString()}", LogLevel.Error);
            }
            finally
            {
                TempCleanupService.ForceDeleteDirectory(tempDecompDir);
                
                // РќРµ СѓРґР°Р»СЏРµРј intermediatePath, РµСЃР»Рё СЃР¶Р°С‚РёРµ РїСЂРѕС€Р»Рѕ СѓСЃРїРµС€РЅРѕ (СЃРѕС…СЂР°РЅСЏРµРј РѕР±Р° С„Р°Р№Р»Р°: Рё NSP/XCI, Рё NSZ/XCZ)
                if (!(isCompressedFormat && compressionSuccess) && intermediatePath != outPath && !string.IsNullOrEmpty(intermediatePath) && System.IO.File.Exists(intermediatePath))
                {
                    TempCleanupService.ForceDeleteFile(intermediatePath);
                }
            }
        }
        [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

        private string PrepareSafeFileForTemp(string sourcePath, string tempDir)
        {
            if (Directory.Exists(sourcePath)) return sourcePath;

            // Р•СЃР»Рё СЌС‚Рѕ СѓР¶Рµ NSP С„Р°Р№Р» Рё РµРіРѕ РїСѓС‚СЊ Р±РµР·РѕРїР°СЃРЅС‹Р№ (< 240 СЃРёРјРІРѕР»РѕРІ) вЂ” РёСЃРїРѕР»СЊР·СѓРµРј РµРіРѕ РЅР°РїСЂСЏРјСѓСЋ Р±РµР· РєРѕРїРёСЂРѕРІР°РЅРёСЏ
            if (sourcePath.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) && sourcePath.Length < 240 && File.Exists(sourcePath))
            {
                return sourcePath;
            }

            string origName = System.IO.Path.GetFileName(sourcePath);
            string safeName = NszCompressionService.SanitizeFileName(origName);

            // Р—Р°С‰РёС‚Р° РѕС‚ СЃР»РёС€РєРѕРј РґР»РёРЅРЅС‹С… РёРјРµРЅ С„Р°Р№Р»РѕРІ (> 80 СЃРёРјРІРѕР»РѕРІ)
            if (safeName.Length > 80)
            {
                string ext = System.IO.Path.GetExtension(safeName);
                var matchTid = System.Text.RegularExpressions.Regex.Match(safeName, @"\[[0-9A-Fa-f]{16}\]");
                string tidPart = matchTid.Success ? "_" + matchTid.Value : "";
                string prefix = safeName.Substring(0, Math.Min(30, safeName.Length));
                safeName = $"{prefix}{tidPart}_{Guid.NewGuid().ToString("N").Substring(0, 4)}{ext}";
            }

            string destPath = System.IO.Path.Combine(tempDir, safeName);
            if (sourcePath.Equals(destPath, StringComparison.OrdinalIgnoreCase)) return sourcePath;

            if (!File.Exists(destPath))
            {
                try
                {
                    // РџСЂРѕР±СѓРµРј СЃРѕР·РґР°С‚СЊ Р±С‹СЃС‚СЂС‹Р№ С…Р°СЂРґР»РёРЅРє
                    if (CreateHardLink(destPath, sourcePath, IntPtr.Zero))
                    {
                        return destPath;
                    }

                    // Р•СЃР»Рё С„Р°Р№Р» РЅРµР±РѕР»СЊС€РѕР№ (< 300 РњР‘) вЂ” РєРѕРїРёСЂСѓРµРј РІ Р±С‹СЃС‚СЂС‹Р№ STORM_TMP
                    var fi = new FileInfo(sourcePath);
                    if (fi.Length < 300L * 1024 * 1024)
                    {
                        File.Copy(sourcePath, destPath, true);
                    }
                    else
                    {
                        return sourcePath;
                    }
                }
                catch
                {
                    return sourcePath;
                }
            }
            return destPath;
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

        public static string FormatOutputFileName(string originalOutPath, List<string> allInputFiles)
        {
            string targetDir = System.IO.Path.GetDirectoryName(originalOutPath) ?? "";
            string origFileName = System.IO.Path.GetFileNameWithoutExtension(originalOutPath);
            string rawExt = System.IO.Path.GetExtension(originalOutPath).ToLowerInvariant().Trim();
            string ext = ".nsp";
            if (rawExt.Contains("xcz")) ext = ".xcz";
            else if (rawExt.Contains("xci")) ext = ".xci";
            else if (rawExt.Contains("nsz")) ext = ".nsz";
            else if (rawExt.Contains("cia")) ext = ".cia";
            else if (rawExt.Contains("3ds")) ext = ".3ds";
            else if (rawExt.Contains("nsp")) ext = ".nsp";
            else if (!string.IsNullOrEmpty(rawExt)) ext = rawExt;

            origFileName = System.Text.RegularExpressions.Regex.Replace(origFileName, @"\s*\+\s*(?:nsz|nsp|xcz|xci|cia|3ds)$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();

            string titleId = "";
            string patchVer = "";
            int gameCount = 0;
            int updateCount = 0;
            int dlcCount = 0;
            int modCount = 0;

            foreach (var f in allInputFiles)
            {
                if (System.IO.Directory.Exists(f))
                {
                    string dirName = System.IO.Path.GetFileName(f).ToLowerInvariant();
                    if (dirName == "romfs" || dirName == "exefs" || dirName == "exefs_patches" || dirName == "cheats" || dirName == "atmosphere" ||
                        f.Contains("romfs", StringComparison.OrdinalIgnoreCase) || 
                        f.Contains("exefs", StringComparison.OrdinalIgnoreCase) ||
                        f.Contains("exefs_patches", StringComparison.OrdinalIgnoreCase) ||
                        f.Contains("cheat", StringComparison.OrdinalIgnoreCase) ||
                        f.Contains("С‡РёС‚", StringComparison.OrdinalIgnoreCase) ||
                        f.Contains("mod", StringComparison.OrdinalIgnoreCase) ||
                        f.Contains("РјРѕРґ", StringComparison.OrdinalIgnoreCase))
                    {
                        modCount = 1;
                    }
                    continue;
                }

                string fname = System.IO.Path.GetFileName(f);
                string tid = "";
                var matchTid = System.Text.RegularExpressions.Regex.Match(fname, @"\[([0-9A-Fa-f]{16})\]");
                if (matchTid.Success) tid = matchTid.Groups[1].Value.ToUpperInvariant();

                bool isModFile = fname.Contains("MOD", StringComparison.OrdinalIgnoreCase) ||
                                 fname.Contains("Р РЈРЎ", StringComparison.OrdinalIgnoreCase) ||
                                 fname.Contains("RUS", StringComparison.OrdinalIgnoreCase) ||
                                 fname.Contains("cheat", StringComparison.OrdinalIgnoreCase) ||
                                 fname.Contains("С‡РёС‚", StringComparison.OrdinalIgnoreCase) ||
                                 fname.Contains("romfs", StringComparison.OrdinalIgnoreCase) ||
                                 fname.Contains("exefs", StringComparison.OrdinalIgnoreCase);

                bool isDlc = (!string.IsNullOrEmpty(tid) && tid.Length == 16 && !tid.EndsWith("000") && !tid.EndsWith("800")) ||
                             fname.Contains("DLC", StringComparison.OrdinalIgnoreCase) ||
                             fname.Contains("AddOn", StringComparison.OrdinalIgnoreCase);

                bool isPatch = !isModFile && ((!string.IsNullOrEmpty(tid) && tid.Length == 16 && tid.EndsWith("800")) ||
                               fname.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
                               fname.Contains("Patch", StringComparison.OrdinalIgnoreCase) ||
                               (fname.Contains("[v") && !fname.Contains("[v0]")));

                bool isBase = (!string.IsNullOrEmpty(tid) && tid.Length == 16 && tid.EndsWith("000")) ||
                              fname.Contains("[v0]") || fname.EndsWith("v0.nsp", StringComparison.OrdinalIgnoreCase) ||
                              fname.Contains("patched_base");

                if (isModFile)
                {
                    modCount = 1;
                }

                if (isDlc)
                {
                    dlcCount++;
                }
                else if (isPatch)
                {
                    updateCount++;
                    var matchVer = System.Text.RegularExpressions.Regex.Match(fname, @"\[v(\d+)\]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (matchVer.Success && string.IsNullOrEmpty(patchVer))
                    {
                        patchVer = matchVer.Groups[1].Value;
                    }
                }
                else if (isBase)
                {
                    gameCount++;
                    if (string.IsNullOrEmpty(titleId) && !string.IsNullOrEmpty(tid)) titleId = tid;
                }
            }

            if (string.IsNullOrEmpty(titleId) || string.IsNullOrEmpty(patchVer))
            {
                try
                {
                    foreach (var f in allInputFiles)
                    {
                        if (System.IO.Directory.Exists(f)) continue;
                        var info = App.SwitchFormat.ParseNsp(f);
                        if (string.IsNullOrEmpty(titleId) && info.ContentType == "Application" && !string.IsNullOrEmpty(info.TitleId))
                            titleId = info.TitleId.Trim().ToUpperInvariant();
                        if (string.IsNullOrEmpty(patchVer) && info.ContentType == "Patch" && !string.IsNullOrEmpty(info.Version))
                            patchVer = info.Version.Trim();
                    }
                }
                catch { }
            }

            if (gameCount == 0) gameCount = 1;

            string baseGameTitle = origFileName;

            // РџСЂРѕРІРµСЂСЏРµРј, СЃРѕРґРµСЂР¶РёС‚ Р»Рё РѕСЂРёРіРёРЅР°Р»СЊРЅРѕРµ РёРјСЏ С„Р°Р№Р»Р° СѓР¶Рµ TitleID Рё/РёР»Рё РІРµСЂСЃРёСЋ
            // Р•СЃР»Рё РїРѕР»СЊР·РѕРІР°С‚РµР»СЊ СѓРєР°Р·Р°Р» РёС… РІ СЃРІРѕС‘Рј С„РѕСЂРјР°С‚Рµ (РЅР°РїСЂРёРјРµСЂ, РІ РєСЂСѓРіР»С‹С… СЃРєРѕР±РєР°С…),
            // РЅРµ РЅСѓР¶РЅРѕ СѓРґР°Р»СЏС‚СЊ Рё РґРѕР±Р°РІР»СЏС‚СЊ Р·Р°РЅРѕРІРѕ РІ РєРІР°РґСЂР°С‚РЅС‹С… СЃРєРѕР±РєР°С…
            bool origHasTitleId = !string.IsNullOrEmpty(titleId) &&
                origFileName.Contains(titleId, StringComparison.OrdinalIgnoreCase);
            bool origHasPatchVer = !string.IsNullOrEmpty(patchVer) &&
                (origFileName.Contains($"v{patchVer}", StringComparison.OrdinalIgnoreCase) ||
                 origFileName.Contains(patchVer, StringComparison.OrdinalIgnoreCase));

            // РЈРґР°Р»СЏРµРј Р»СЋР±РѕР№ СЃСѓС‰РµСЃС‚РІСѓСЋС‰РёР№ С‚РµРі СЃРѕРґРµСЂР¶РёРјРѕРіРѕ (1G+1U+4D), (1G+1U+4D+1M), (1G+1U+1M), (1G+5D) Рё С‚.Рґ. вЂ” РѕРЅ РІСЃРµРіРґР° РїРµСЂРµСЃС‡РёС‚С‹РІР°РµС‚СЃСЏ Р·Р°РЅРѕРІРѕ
            baseGameTitle = System.Text.RegularExpressions.Regex.Replace(baseGameTitle, @"\s*\(\d+[A-Za-z](?:\+\d+[A-Za-z])*\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // РЈРґР°Р»СЏРµРј РєРІР°РґСЂР°С‚РЅС‹Рµ С‚РµРіРё [TitleID] Рё [vXXX] РўРћР›Р¬РљРћ РµСЃР»Рё РёС… РЅРµС‚ РІ РѕСЂРёРіРёРЅР°Р»Рµ
            // (С‚.Рµ. РѕРЅРё Р±С‹Р»Рё РґРѕР±Р°РІР»РµРЅС‹ Р°РІС‚РѕРјР°С‚РёС‡РµСЃРєРё СЂР°РЅРµРµ, Р° РЅРµ РїРѕР»СЊР·РѕРІР°С‚РµР»РµРј)
            if (!origHasTitleId)
            {
                baseGameTitle = System.Text.RegularExpressions.Regex.Replace(baseGameTitle, @"\s*\[[0-9A-Fa-f]{16}\]", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            if (!origHasPatchVer)
            {
                baseGameTitle = System.Text.RegularExpressions.Regex.Replace(baseGameTitle, @"\s*\[v\d+\]", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // РќРµ СѓРґР°Р»СЏРµРј РїРѕР»СЊР·РѕРІР°С‚РµР»СЊСЃРєРёРµ РєСЂСѓРіР»С‹Рµ СЃРєРѕР±РєРё СЃ РёРЅС„РѕСЂРјР°С†РёРµР№ Рѕ РІРµСЂСЃРёРё (1.0.9 - 458752 - TitleID)
            // РЈРґР°Р»СЏРµРј С‚РѕР»СЊРєРѕ РµСЃР»Рё TitleID РќР• Р±С‹Р» РІ РѕСЂРёРіРёРЅР°Р»Рµ (Р·РЅР°С‡РёС‚ СЌС‚Рѕ Р°РІС‚РѕРјР°С‚РёС‡РµСЃРєРёР№ С‚РµРі)
            if (!origHasTitleId)
            {
                baseGameTitle = System.Text.RegularExpressions.Regex.Replace(baseGameTitle, @"\s*\([^)]*\d{16}[^)]*\)", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            if (baseGameTitle.EndsWith("_Multi", StringComparison.OrdinalIgnoreCase))
                baseGameTitle = baseGameTitle.Substring(0, baseGameTitle.Length - 6);
            if (baseGameTitle.EndsWith("_Update", StringComparison.OrdinalIgnoreCase))
                baseGameTitle = baseGameTitle.Substring(0, baseGameTitle.Length - 7);

            var sb = new System.Text.StringBuilder();
            sb.Append(baseGameTitle.Trim());

            // Р”РѕР±Р°РІР»СЏРµРј TitleID Рё РІРµСЂСЃРёСЋ РўРћР›Р¬РљРћ РµСЃР»Рё РёС… РЅРµС‚ РІ РѕСЂРёРіРёРЅР°Р»СЊРЅРѕРј РёРјРµРЅРё
            if (!origHasTitleId && !string.IsNullOrEmpty(titleId))
            {
                sb.Append($" [{titleId}]");
            }

            if (!origHasPatchVer && !string.IsNullOrEmpty(patchVer))
            {
                sb.Append(patchVer.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? $" [{patchVer}]" : $" [v{patchVer}]");
            }

            var parts = new List<string>();
            if (gameCount > 0) parts.Add($"{gameCount}G");
            if (updateCount > 0) parts.Add($"{updateCount}U");
            if (dlcCount > 0) parts.Add($"{dlcCount}D");
            if (modCount > 0) parts.Add($"{modCount}M");

            if (parts.Count > 0)
            {
                sb.Append($" ({string.Join("+", parts)})");
            }

            sb.Append(ext);
            string newFileName = NszCompressionService.SanitizeFinalOutputFileName(sb.ToString());
            return System.IO.Path.Combine(targetDir, newFileName);
        }

        private int GetNcaPriority(LibHac.Fs.Fsa.IFile file, string fileName, ulong baseTitleId, HashSet<string>? baseEntries = null)
        {
            string lower = fileName.ToLowerInvariant();

            if (lower.EndsWith(".tik")) return 0;
            if (lower.EndsWith(".cert")) return 1;

            bool isFromBase = baseEntries != null && baseEntries.Contains(fileName);

            if (lower.EndsWith(".cnmt.xml"))
            {
                if (isFromBase || lower.Contains("000") || lower.Contains("base")) return 2;
                if (lower.Contains("800") || lower.Contains("update") || lower.Contains("patch")) return 10;
                return 20;
            }

            if (lower.EndsWith(".cnmt.nca"))
            {
                if (isFromBase) return 3; // Base/Patched Game CNMT is top priority among NCAs
                if (lower.Contains("000") || lower.Contains("base")) return 3;
                if (lower.Contains("800") || lower.Contains("update") || lower.Contains("patch")) return 11; // Update Patch CNMT
                if (baseTitleId != 0 && lower.Contains(baseTitleId.ToString("x16"))) return 3;
                if (baseTitleId != 0 && lower.Contains((baseTitleId + 0x800).ToString("x16"))) return 11;
                return 21; // DLC / Mod CNMT
            }

            try
            {
                LibHac.Fs.IStorage storage = file.AsStorage();
                if (lower.EndsWith(".ncz"))
                {
                    try
                    {
                        storage = new Core.NSZ.StormNczStorage(storage, null, null, _keysService.CurrentKeyset);
                    }
                    catch { }
                }

                var nca = new LibHac.Tools.FsSystem.NcaUtils.Nca(_keysService.CurrentKeyset, storage);
                var type = nca.Header.ContentType;
                ulong tid = nca.Header.TitleId;

                bool isBaseTitle = (baseTitleId != 0 && tid == baseTitleId) || tid.ToString("X16").EndsWith("000");
                bool isUpdateTitle = (baseTitleId != 0 && tid == (baseTitleId | 0x800)) || tid.ToString("X16").EndsWith("800");

                if (type == LibHac.Tools.FsSystem.NcaUtils.NcaContentType.Meta) // CNMT
                {
                    if (isBaseTitle) return 3;
                    if (isUpdateTitle) return 11;
                    return 21;
                }

                if (type == LibHac.Tools.FsSystem.NcaUtils.NcaContentType.Control) // Icon artwork & Title strings
                {
                    if (isBaseTitle) return 4;
                    if (isUpdateTitle) return 12;
                    return 22;
                }

                if (type == LibHac.Tools.FsSystem.NcaUtils.NcaContentType.Program) // Executable code
                {
                    if (isBaseTitle) return 5;
                    if (isUpdateTitle) return 13;
                    return 23;
                }

                if (type == LibHac.Tools.FsSystem.NcaUtils.NcaContentType.Manual || type == LibHac.Tools.FsSystem.NcaUtils.NcaContentType.PublicData || type == LibHac.Tools.FsSystem.NcaUtils.NcaContentType.Data)
                {
                    if (isBaseTitle) return 6;
                    if (isUpdateTitle) return 14;
                    return 24;
                }
            }
            catch
            {
                if (isFromBase)
                {
                    if (lower.EndsWith(".cnmt.nca")) return 3;
                    if (lower.Contains("control")) return 4;
                    if (lower.Contains("program")) return 5;
                    
                    try
                    {
                        file.GetSize(out long fSize);
                        if (fSize > 0 && fSize < 5 * 1024 * 1024) return 4; // Control-sized NCA
                    }
                    catch { }
                    return 5;
                }

                if (lower.EndsWith(".cnmt.nca"))
                {
                    if (lower.Contains("800") || lower.Contains("update") || lower.Contains("patch")) return 11;
                    return 21;
                }
                if (lower.Contains("control")) return 22;
                if (lower.Contains("program")) return 23;
            }

            return isFromBase ? 7 : 25;
        }

        private List<string> ExtractUnlockerRomFsDirectories(List<string> inputFiles, string tempDir, string? baseTitleIdStr, Models.ProcessingTask task, CancellationToken ct)
        {
            var extractedDirs = new List<string>();
            int unlockerIndex = 0;

            foreach (var file in inputFiles)
            {
                if (Directory.Exists(file)) continue;
                string fname = System.IO.Path.GetFileName(file);
                
                // РџСЂРѕРІРµСЂСЏРµРј, СЏРІР»СЏРµС‚СЃСЏ Р»Рё С„Р°Р№Р» Unlocker-РїР°С‚С‡РµРј/DLC (СЃ СѓС‡РµС‚РѕРј СЂСѓСЃСЃРєРёС… Рё Р°РЅРіР»РёР№СЃРєРёС… РЅР°Р·РІР°РЅРёР№)
                bool isUnlockerName = fname.Contains("Unlocker", StringComparison.OrdinalIgnoreCase) ||
                                     fname.Contains("Unlock", StringComparison.OrdinalIgnoreCase) ||
                                     fname.Contains("Custom Unlock", StringComparison.OrdinalIgnoreCase) ||
                                     fname.Contains("РђРЅР»РѕРєРµСЂ", StringComparison.OrdinalIgnoreCase) ||
                                     fname.Contains("Р Р°Р·Р±Р»РѕРєРёСЂРѕРІС‰РёРє", StringComparison.OrdinalIgnoreCase) ||
                                     fname.Contains("Р Р°Р·Р±Р»РѕРєРёСЂРѕРІРєР°", StringComparison.OrdinalIgnoreCase) ||
                                     fname.Contains("РђРЅР»РѕРє", StringComparison.OrdinalIgnoreCase) ||
                                     fname.Contains("Р’СЃРµ РїРµСЂСЃРѕРЅР°Р¶Рё", StringComparison.OrdinalIgnoreCase) ||
                                     fname.Contains("РџРµСЂСЃРѕРЅР°Р¶Рё", StringComparison.OrdinalIgnoreCase);

                bool isSmallDlc = false;
                try
                {
                    var info = App.SwitchFormat.ParseNsp(file);
                    if (info.ContentType == "AddOnContent" && new FileInfo(file).Length < 250 * 1024 * 1024)
                    {
                        isSmallDlc = true;
                    }
                }
                catch { }

                if (!isUnlockerName && !isSmallDlc) continue;

                string targetRomfs = System.IO.Path.Combine(tempDir, $"unlocker_romfs_{unlockerIndex}");
                Directory.CreateDirectory(targetRomfs);

                bool extracted = false;
                try
                {
                    using var fileStream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var pfs = new PartitionFileSystem(fileStream.AsStorage());
                    foreach (var entry in pfs.EnumerateEntries())
                    {
                        if (entry.Type == LibHac.Fs.DirectoryEntryType.Directory) continue;
                        string ename = entry.Name.ToLowerInvariant();
                        if (ename.EndsWith(".nca") && !ename.EndsWith(".cnmt.nca"))
                        {
                            using var ncaFile = new UniqueRef<IFile>();
                            using var entryPath = new LibHac.Fs.Path();
                            entryPath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(entry.FullPath))).ThrowIfFailure();
                            pfs.OpenFile(ref ncaFile.Ref, in entryPath, OpenMode.Read).ThrowIfFailure();
                            
                            var nca = new LibHac.Tools.FsSystem.NcaUtils.Nca(_keysService.CurrentKeyset, ncaFile.Release().AsStorage());
                            
                            for (int sec = 0; sec < 2; sec++)
                            {
                                if (nca.SectionExists(sec) && nca.CanOpenSection(sec))
                                {
                                    try
                                    {
                                        var romfsFs = nca.OpenFileSystem(sec, IntegrityCheckLevel.None);
                                        ExtractAllEntriesFromRomFs(romfsFs, targetRomfs, ct);
                                        if (Directory.GetFiles(targetRomfs, "*", SearchOption.AllDirectories).Length > 0)
                                        {
                                            extracted = true;
                                            break;
                                        }
                                    }
                                    catch (Exception exLib)
                                    {
                                        App.Logger.Log($"[Unlocker] LibHac section {sec} extract note: {exLib.Message}", Models.LogLevel.Info);
                                    }
                                }
                            }
                            if (extracted) break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.Log($"[Unlocker] LibHac extraction warning: {ex.Message}", Models.LogLevel.Warning);
                }

                // Fallback С‡РµСЂРµР· hactoolnet РµСЃР»Рё LibHac РЅРµ РёР·РІР»РµРє
                if (!extracted)
                {
                    try
                    {
                        string appDir = AppDomain.CurrentDomain.BaseDirectory;
                        string hactoolPath = System.IO.Path.Combine(appDir, "tools", "com.github.nozwock.yanu", "hactoolnet.exe");
                        if (!File.Exists(hactoolPath))
                            hactoolPath = System.IO.Path.Combine(appDir, "..", "tools", "com.github.nozwock.yanu", "hactoolnet.exe");
                        if (!File.Exists(hactoolPath))
                            hactoolPath = System.IO.Path.Combine(appDir, "tools", "hactoolnet.exe");

                        if (File.Exists(hactoolPath))
                        {
                            string keysPath = App.Settings.Current.KeysPath;
                            if (string.IsNullOrEmpty(keysPath) || !File.Exists(keysPath))
                                keysPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "prod.keys");

                            string keyArg = File.Exists(keysPath) ? $"-k \"{keysPath}\"" : "";
                            string tempExtractDir = System.IO.Path.Combine(tempDir, $"hactool_extract_{unlockerIndex}");
                            Directory.CreateDirectory(tempExtractDir);

                            var psi = new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = hactoolPath,
                                Arguments = $"{keyArg} -t pfs0 --outdir \"{tempExtractDir}\" \"{file}\"",
                                UseShellExecute = false,
                                CreateNoWindow = true
                            };
                            using (var proc = System.Diagnostics.Process.Start(psi))
                            {
                                proc?.WaitForExit(15000);
                            }

                            var extractedNcas = Directory.GetFiles(tempExtractDir, "*.nca");
                            foreach (var ncaPath in extractedNcas)
                            {
                                if (ncaPath.EndsWith(".cnmt.nca", StringComparison.OrdinalIgnoreCase)) continue;
                                var romfsPsi = new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = hactoolPath,
                                    Arguments = $"{keyArg} --romfsdir \"{targetRomfs}\" \"{ncaPath}\"",
                                    UseShellExecute = false,
                                    CreateNoWindow = true
                                };
                                using (var rproc = System.Diagnostics.Process.Start(romfsPsi))
                                {
                                    rproc?.WaitForExit(15000);
                                }
                            }
                            try { Directory.Delete(tempExtractDir, true); } catch { }
                            extracted = Directory.GetFiles(targetRomfs, "*", SearchOption.AllDirectories).Length > 0;
                        }
                    }
                    catch { }
                }

                if (extracted)
                {
                    var allExtracted = Directory.GetFiles(targetRomfs, "*", SearchOption.AllDirectories);
                    int fileCount = allExtracted.Length;
                    App.Logger.Log($"[Unlocker] РЈСЃРїРµС€РЅРѕ РёР·РІР»РµС‡РµРЅРѕ {fileCount} С„Р°Р№Р»РѕРІ СЂР°Р·Р±Р»РѕРєРёСЂРѕРІРєРё РёР· {fname} РґР»СЏ RomFS-РёРЅСЉРµРєС†РёРё.", Models.LogLevel.Success);
                    string sampleNames = string.Join(", ", allExtracted.Take(4).Select(System.IO.Path.GetFileName));
                    if (fileCount > 4) sampleNames += "...";
                    App.RunOnUI(() => task.LogDetails += $"\nрџ”“ [Unlocker] РР·РІР»РµС‡РµРЅРѕ {fileCount} С‚РѕРєРµРЅРѕРІ СЂР°Р·Р±Р»РѕРєРёСЂРѕРІРєРё РёР· {fname} ({sampleNames})");
                    extractedDirs.Add(targetRomfs);

                    // РЎРёРЅС…СЂРѕРЅРёР·Р°С†РёСЏ СЃ СЌРјСѓР»СЏС‚РѕСЂРѕРј (LayeredFS)
                    SyncUnlockerToEmulators(targetRomfs, baseTitleIdStr, task);
                    unlockerIndex++;
                }
                else
                {
                    try { Directory.Delete(targetRomfs, true); } catch { }
                }
            }

            return extractedDirs;
        }

        private static void ExtractAllEntriesFromRomFs(LibHac.Fs.Fsa.IFileSystem fs, string targetDir, CancellationToken ct)
        {
            foreach (var rentry in fs.EnumerateEntries("/", "*"))
            {
                ct.ThrowIfCancellationRequested();
                if (rentry.Type == LibHac.Fs.DirectoryEntryType.Directory) continue;

                string relPath = rentry.FullPath.TrimStart('/');
                string localPath = System.IO.Path.Combine(targetDir, relPath);
                string? localDir = System.IO.Path.GetDirectoryName(localPath);
                if (!string.IsNullOrEmpty(localDir) && !Directory.Exists(localDir))
                {
                    Directory.CreateDirectory(localDir);
                }

                using var fileRef = new UniqueRef<IFile>();
                using var filePath = new LibHac.Fs.Path();
                filePath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(rentry.FullPath))).ThrowIfFailure();
                fs.OpenFile(ref fileRef.Ref, in filePath, OpenMode.Read).ThrowIfFailure();
                var f = fileRef.Release();

                f.GetSize(out long fSize).ThrowIfFailure();
                using var outFs = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None);
                byte[] buf = new byte[64 * 1024];
                long off = 0;
                while (off < fSize)
                {
                    int toRead = (int)Math.Min(buf.Length, fSize - off);
                    f.Read(out long r, off, buf.AsSpan(0, toRead)).ThrowIfFailure();
                    outFs.Write(buf, 0, (int)r);
                    off += r;
                }
            }
        }

        private void SyncUnlockerToEmulators(string unlockerRomfsDir, string? baseTitleIdStr, Models.ProcessingTask task)
        {
            if (string.IsNullOrEmpty(baseTitleIdStr)) return;
            string cleanTid = baseTitleIdStr.Trim().ToUpperInvariant();
            if (cleanTid.Length != 16) return;

            try
            {
                var emulatorPaths = HomebrewService.FindAllEmulatorSdmcDirectories();
                foreach (var sdmcPath in emulatorPaths)
                {
                    string userDir = System.IO.Path.GetDirectoryName(sdmcPath) ?? "";
                    if (!Directory.Exists(userDir)) continue;

                    // 1. user/load/<TitleID>/romfs/
                    string loadRomfs = System.IO.Path.Combine(userDir, "load", cleanTid, "romfs");
                    Directory.CreateDirectory(loadRomfs);
                    CopyDirectoryContentSafe(unlockerRomfsDir, loadRomfs);

                    // 2. user/sdmc/atmosphere/contents/<TitleID>/romfs/
                    string atmoRomfs = System.IO.Path.Combine(sdmcPath, "atmosphere", "contents", cleanTid, "romfs");
                    Directory.CreateDirectory(atmoRomfs);
                    CopyDirectoryContentSafe(unlockerRomfsDir, atmoRomfs);

                    App.Logger.Log($"[Unlocker] РЎРёРЅС…СЂРѕРЅРёР·РёСЂРѕРІР°РЅС‹ LayeredFS С„Р°Р№Р»С‹ СЂР°Р·Р±Р»РѕРєРёСЂРѕРІРєРё РґР»СЏ {cleanTid} РІ СЌРјСѓР»СЏС‚РѕСЂ: {userDir}", Models.LogLevel.Success);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[Unlocker] РћС€РёР±РєР° СЃРёРЅС…СЂРѕРЅРёР·Р°С†РёРё СЃ СЌРјСѓР»СЏС‚РѕСЂР°РјРё: {ex.Message}", Models.LogLevel.Warning);
            }
        }

        private static void CopyDirectoryContentSafe(string sourceDir, string targetDir)
        {
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string rel = System.IO.Path.GetRelativePath(sourceDir, file);
                string dest = System.IO.Path.Combine(targetDir, rel);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dest)!);
                File.Copy(file, dest, true);
            }
        }

        private static void DeployCheatsIfPresent(string titleId, List<string> inputFiles, string outPath)
        {
            if (string.IsNullOrEmpty(titleId)) return;
            string cleanTid = titleId.Trim().ToUpperInvariant();

            var cheatFiles = new List<string>();
            foreach (var input in inputFiles)
            {
                if (Directory.Exists(input))
                {
                    try
                    {
                        var txts = Directory.GetFiles(input, "*.txt", SearchOption.AllDirectories)
                            .Where(f => f.Contains("cheat", StringComparison.OrdinalIgnoreCase) || 
                                        f.Contains("contents", StringComparison.OrdinalIgnoreCase) ||
                                        System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileNameWithoutExtension(f), @"^[0-9A-Fa-f]{16}$"))
                            .ToList();
                        cheatFiles.AddRange(txts);
                    }
                    catch { }
                }
                else if (File.Exists(input))
                {
                    if (input.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) && 
                        (input.Contains("cheat", StringComparison.OrdinalIgnoreCase) || 
                         System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileNameWithoutExtension(input), @"^[0-9A-Fa-f]{16}$")))
                    {
                        cheatFiles.Add(input);
                    }
                }
            }

            if (cheatFiles.Count == 0)
            {
                var scannedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var input in inputFiles)
                {
                    string parentDir = Directory.Exists(input) ? input : (Path.GetDirectoryName(input) ?? "");
                    if (!string.IsNullOrEmpty(parentDir) && scannedDirs.Add(parentDir) && Directory.Exists(parentDir))
                    {
                        try
                        {
                            var txts = Directory.GetFiles(parentDir, "*.txt", SearchOption.AllDirectories)
                                .Where(f => f.Contains("cheat", StringComparison.OrdinalIgnoreCase) || 
                                            (f.Contains("contents", StringComparison.OrdinalIgnoreCase) && f.Contains(cleanTid, StringComparison.OrdinalIgnoreCase)) ||
                                            System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileNameWithoutExtension(f), @"^[0-9A-Fa-f]{16}$"))
                                .ToList();
                            cheatFiles.AddRange(txts);
                        }
                        catch { }
                    }
                }
            }

            if (cheatFiles.Count == 0) return;

            // 1. РЎРёРЅС…СЂРѕРЅРёР·Р°С†РёСЏ С‡РёС‚РѕРІ СЃ СЌРјСѓР»СЏС‚РѕСЂР°РјРё
            try
            {
                var emulatorPaths = HomebrewService.FindAllEmulatorSdmcDirectories();
                foreach (var sdmcPath in emulatorPaths)
                {
                    string userDir = System.IO.Path.GetDirectoryName(sdmcPath) ?? "";
                    if (!Directory.Exists(userDir)) continue;

                    string atmoCheats = System.IO.Path.Combine(sdmcPath, "atmosphere", "contents", cleanTid, "cheats");
                    string loadCheats = System.IO.Path.Combine(userDir, "load", cleanTid, "cheats");
                    Directory.CreateDirectory(atmoCheats);
                    Directory.CreateDirectory(loadCheats);

                    foreach (var cheatFile in cheatFiles)
                    {
                        string targetFileAtmo = System.IO.Path.Combine(atmoCheats, Path.GetFileName(cheatFile));
                        string targetFileLoad = System.IO.Path.Combine(loadCheats, Path.GetFileName(cheatFile));
                        File.Copy(cheatFile, targetFileAtmo, true);
                        File.Copy(cheatFile, targetFileLoad, true);
                    }

                    App.Logger.Log($"[Cheats] Р§РёС‚-РєРѕРґС‹ СЃРёРЅС…СЂРѕРЅРёР·РёСЂРѕРІР°РЅС‹ РґР»СЏ {cleanTid} РІ СЌРјСѓР»СЏС‚РѕСЂ: {userDir}", Models.LogLevel.Success);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[Cheats] РћС€РёР±РєР° СЃРёРЅС…СЂРѕРЅРёР·Р°С†РёРё С‡РёС‚-РєРѕРґРѕРІ СЃ СЌРјСѓР»СЏС‚РѕСЂР°РјРё: {ex.Message}", Models.LogLevel.Warning);
            }

            // 2. РљРѕРїРёСЂРѕРІР°РЅРёРµ С‡РёС‚РѕРІ РІ РєР°С‚Р°Р»РѕРі СЃ СЃРѕР±СЂР°РЅРЅС‹Рј С„Р°Р№Р»РѕРј
            try
            {
                string outDir = Path.GetDirectoryName(outPath) ?? "";
                if (Directory.Exists(outDir))
                {
                    string outCheatsDir = Path.Combine(outDir, "cheats", cleanTid);
                    Directory.CreateDirectory(outCheatsDir);
                    foreach (var cheatFile in cheatFiles)
                    {
                        string targetFile = Path.Combine(outCheatsDir, Path.GetFileName(cheatFile));
                        File.Copy(cheatFile, targetFile, true);
                    }
                    App.Logger.Log($"[Cheats] Р§РёС‚-РєРѕРґС‹ СЃРѕС…СЂР°РЅРµРЅС‹ РІ РІС‹С…РѕРґРЅРѕР№ РєР°С‚Р°Р»РѕРі: {outCheatsDir}", Models.LogLevel.Success);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[Cheats] РћС€РёР±РєР° СЃРѕС…СЂР°РЅРµРЅРёСЏ С‡РёС‚-РєРѕРґРѕРІ РІ РІС‹С…РѕРґРЅРѕР№ РєР°С‚Р°Р»РѕРі: {ex.Message}", Models.LogLevel.Warning);
            }
        }

        private async Task<List<string>> GenerateModAddonEntriesAsync(
            ulong baseTitleId, 
            string tempDir, 
            bool hasRomFs, 
            bool hasExeFs, 
            int existingDlcCount,
            Models.ProcessingTask task, 
            CancellationToken ct)
        {
            var generatedNcas = new List<string>();
            try
            {
                string toolsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
                if (!Directory.Exists(toolsDir))
                {
                    string parentTools = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "tools");
                    if (Directory.Exists(parentTools)) toolsDir = parentTools;
                }

                string hacpackExe = Path.Combine(toolsDir, "com.github.nozwock.yanu", "hacpack.exe");
                if (!File.Exists(hacpackExe)) return generatedNcas;

                string? keysFile = App.Settings.Current.KeysPath;
                if (string.IsNullOrEmpty(keysFile) || !File.Exists(keysFile))
                {
                    string userKeys = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "prod.keys");
                    if (File.Exists(userKeys)) keysFile = userKeys;
                }
                if (string.IsNullOrEmpty(keysFile) || !File.Exists(keysFile))
                {
                    string toolsKeys = Path.Combine(toolsDir, "keys", ".switch", "prod.keys");
                    if (File.Exists(toolsKeys)) keysFile = toolsKeys;
                }
                if (string.IsNullOrEmpty(keysFile) || !File.Exists(keysFile))
                {
                    string nscbKeys = Path.Combine(toolsDir, "nscb", "ztools", "keys.txt");
                    if (File.Exists(nscbKeys)) keysFile = nscbKeys;
                }

                if (string.IsNullOrEmpty(keysFile) || !File.Exists(keysFile))
                {
                    App.Logger.Log("[ModAddon] Р¤Р°Р№Р» РєР»СЋС‡РµР№ РЅРµ РЅР°Р№РґРµРЅ. РџСЂРѕРїСѓСЃРє СЃРѕР·РґР°РЅРёСЏ РјРµС‚Р°РґР°РЅРЅС‹С… РґРѕРїРѕР»РЅРµРЅРёР№.", LogLevel.Warning);
                    return generatedNcas;
                }

                string modTempDir = Path.Combine(tempDir, "mod_addon_gen");
                Directory.CreateDirectory(modTempDir);

                // РР·РІР»РµРєР°РµРј РёР»Рё РіРµРЅРµСЂРёСЂСѓРµРј РёРєРѕРЅРєСѓ РґР»СЏ Control NCA (256x256 JPEG)
                byte[]? iconBytes = task.CustomMetadata?.CustomIconBytes ?? task.CustomMetadata?.OriginalIconBytes;
                if (iconBytes == null || iconBytes.Length == 0)
                {
                    try
                    {
                        using var bmp = new System.Drawing.Bitmap(256, 256);
                        using (var g = System.Drawing.Graphics.FromImage(bmp))
                        {
                            g.Clear(System.Drawing.Color.FromArgb(30, 130, 230));
                            using var font = new System.Drawing.Font("Arial", 28, System.Drawing.FontStyle.Bold);
                            using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
                            g.DrawString("MOD", font, brush, 75, 105);
                        }
                        using var ms = new MemoryStream();
                        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Jpeg);
                        iconBytes = ms.ToArray();
                    }
                    catch { }
                }

                ulong baseTitleIdClean = baseTitleId & ~0xFFFUL;
                int currentDlcIdx = existingDlcCount + 1;

                if (hasRomFs)
                {
                    ulong modTid = baseTitleIdClean | 0x1000 | (ulong)currentDlcIdx;
                    string modTidHex = modTid.ToString("X16");

                    string romfsControlDir = Path.Combine(modTempDir, "romfs_mod_control");
                    Directory.CreateDirectory(romfsControlDir);

                    string romFsTitle = !string.IsNullOrWhiteSpace(task.ModNameRomFs) ? task.ModNameRomFs : "РњРѕРґРёС„РёРєР°С†РёРё: RomFS";

                    byte[] nacp = new byte[0x4000];
                    byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(romFsTitle);
                    byte[] devBytes = System.Text.Encoding.UTF8.GetBytes("STORM MODS");

                    // Р—Р°РїРёСЃС‹РІР°РµРј РёРјСЏ Рё СЂР°Р·СЂР°Р±РѕС‚С‡РёРєР° РІРѕ РІСЃРµ СЏР·С‹РєРѕРІС‹Рµ СЃР»РѕС‚С‹ NACP (16 СЏР·С‹РєРѕРІ РїРѕ 0x300 Р±Р°Р№С‚)
                    for (int l = 0; l < 16; l++)
                    {
                        int titleOffset = l * 0x300;
                        int devOffset = titleOffset + 0x200;
                        Array.Copy(nameBytes, 0, nacp, titleOffset, Math.Min(nameBytes.Length, 0x200));
                        Array.Copy(devBytes, 0, nacp, devOffset, Math.Min(devBytes.Length, 0x100));
                    }
                    File.WriteAllBytes(Path.Combine(romfsControlDir, "control.nacp"), nacp);

                    if (iconBytes != null && iconBytes.Length > 0)
                    {
                        File.WriteAllBytes(Path.Combine(romfsControlDir, "icon_AmericanEnglish.dat"), iconBytes);
                        File.WriteAllBytes(Path.Combine(romfsControlDir, "icon_Russian.dat"), iconBytes);
                    }

                    string dataRomfsDir = Path.Combine(modTempDir, "romfs_mod_data");
                    Directory.CreateDirectory(dataRomfsDir);
                    File.WriteAllText(Path.Combine(dataRomfsDir, "mod.txt"), "Storm Switch Box Integrated RomFS Mod");

                    string outControlDir = Path.Combine(modTempDir, "out_romfs_control");
                    string outDataDir = Path.Combine(modTempDir, "out_romfs_data");
                    string outMetaDir = Path.Combine(modTempDir, "out_romfs_meta");
                    Directory.CreateDirectory(outControlDir);
                    Directory.CreateDirectory(outDataDir);
                    Directory.CreateDirectory(outMetaDir);

                    // 1. Control NCA
                    await ExternalProcessRunner.RunAsync(hacpackExe, $"-k \"{keysFile}\" --type nca --ncatype control --titleid {modTidHex} --romfsdir \"{romfsControlDir}\" -o \"{outControlDir}\"", modTempDir, task, ct);
                    // 2. PublicData NCA
                    await ExternalProcessRunner.RunAsync(hacpackExe, $"-k \"{keysFile}\" --type nca --ncatype publicdata --titleid {modTidHex} --romfsdir \"{dataRomfsDir}\" -o \"{outDataDir}\"", modTempDir, task, ct);

                    var controlNcas = Directory.GetFiles(outControlDir, "*.nca");
                    var dataNcas = Directory.GetFiles(outDataDir, "*.nca");

                    if (controlNcas.Length > 0 && dataNcas.Length > 0)
                    {
                        string controlNca = controlNcas[0];
                        string publicDataNca = dataNcas[0];

                        // 3. Addon CNMT NCA
                        await ExternalProcessRunner.RunAsync(hacpackExe, $"-k \"{keysFile}\" --type nca --ncatype meta --titletype addon --titleid {modTidHex} --titleversion 0x0 --publicdatanca \"{publicDataNca}\" --controlnca \"{controlNca}\" -o \"{outMetaDir}\"", modTempDir, task, ct);
                        
                        generatedNcas.Add(controlNca);
                        generatedNcas.Add(publicDataNca);
                        generatedNcas.AddRange(Directory.GetFiles(outMetaDir, "*.nca"));
                    }

                    currentDlcIdx++;
                }

                if (hasExeFs)
                {
                    ulong modTid = baseTitleIdClean | 0x1000 | (ulong)currentDlcIdx;
                    string modTidHex = modTid.ToString("X16");

                    string exefsControlDir = Path.Combine(modTempDir, "exefs_mod_control");
                    Directory.CreateDirectory(exefsControlDir);

                    string exeFsTitle = !string.IsNullOrWhiteSpace(task.ModNameExeFs) ? task.ModNameExeFs : "РњРѕРґРёС„РёРєР°С†РёРё: ExeFS";

                    byte[] nacp = new byte[0x4000];
                    byte[] nameBytes = System.Text.Encoding.UTF8.GetBytes(exeFsTitle);
                    byte[] devBytes = System.Text.Encoding.UTF8.GetBytes("STORM MODS");

                    for (int l = 0; l < 16; l++)
                    {
                        int titleOffset = l * 0x300;
                        int devOffset = titleOffset + 0x200;
                        Array.Copy(nameBytes, 0, nacp, titleOffset, Math.Min(nameBytes.Length, 0x200));
                        Array.Copy(devBytes, 0, nacp, devOffset, Math.Min(devBytes.Length, 0x100));
                    }
                    File.WriteAllBytes(Path.Combine(exefsControlDir, "control.nacp"), nacp);

                    if (iconBytes != null && iconBytes.Length > 0)
                    {
                        File.WriteAllBytes(Path.Combine(exefsControlDir, "icon_AmericanEnglish.dat"), iconBytes);
                        File.WriteAllBytes(Path.Combine(exefsControlDir, "icon_Russian.dat"), iconBytes);
                    }

                    string dataRomfsDir = Path.Combine(modTempDir, "exefs_mod_data");
                    Directory.CreateDirectory(dataRomfsDir);
                    File.WriteAllText(Path.Combine(dataRomfsDir, "mod.txt"), "Storm Switch Box Integrated ExeFS Mod");

                    string outControlDir = Path.Combine(modTempDir, "out_exefs_control");
                    string outDataDir = Path.Combine(modTempDir, "out_exefs_data");
                    string outMetaDir = Path.Combine(modTempDir, "out_exefs_meta");
                    Directory.CreateDirectory(outControlDir);
                    Directory.CreateDirectory(outDataDir);
                    Directory.CreateDirectory(outMetaDir);

                    // 1. Control NCA
                    await ExternalProcessRunner.RunAsync(hacpackExe, $"-k \"{keysFile}\" --type nca --ncatype control --titleid {modTidHex} --romfsdir \"{exefsControlDir}\" -o \"{outControlDir}\"", modTempDir, task, ct);
                    // 2. PublicData NCA
                    await ExternalProcessRunner.RunAsync(hacpackExe, $"-k \"{keysFile}\" --type nca --ncatype publicdata --titleid {modTidHex} --romfsdir \"{dataRomfsDir}\" -o \"{outDataDir}\"", modTempDir, task, ct);

                    var controlNcas = Directory.GetFiles(outControlDir, "*.nca");
                    var dataNcas = Directory.GetFiles(outDataDir, "*.nca");

                    if (controlNcas.Length > 0 && dataNcas.Length > 0)
                    {
                        string controlNca = controlNcas[0];
                        string publicDataNca = dataNcas[0];

                        // 3. Addon CNMT NCA
                        await ExternalProcessRunner.RunAsync(hacpackExe, $"-k \"{keysFile}\" --type nca --ncatype meta --titletype addon --titleid {modTidHex} --titleversion 0x0 --publicdatanca \"{publicDataNca}\" --controlnca \"{controlNca}\" -o \"{outMetaDir}\"", modTempDir, task, ct);
                        
                        generatedNcas.Add(controlNca);
                        generatedNcas.Add(publicDataNca);
                        generatedNcas.AddRange(Directory.GetFiles(outMetaDir, "*.nca"));
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[ModAddon] РћС€РёР±РєР° СЃРѕР·РґР°РЅРёСЏ РјРµС‚Р°РґР°РЅРЅС‹С… РјРѕРґРёС„РёРєР°С†РёР№: {ex.Message}", Models.LogLevel.Warning);
            }
            return generatedNcas;
        }

        public static HashSet<string> DetectDeltaNcasToExclude(List<string> nspPaths)
        {
            var deltaNcas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var nspPath in nspPaths)
            {
                if (!System.IO.File.Exists(nspPath)) continue;
                try
                {
                    using var chkStream = new FileStream(nspPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var chkFs = new PartitionFileSystem(chkStream.AsStorage());
                    foreach (var ent in chkFs.EnumerateEntries())
                    {
                        // 1. РџСЂРѕРІРµСЂРєР° .cnmt.xml
                        if (ent.Name.EndsWith(".cnmt.xml", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                using var fRef = new UniqueRef<IFile>();
                                using var p = new LibHac.Fs.Path();
                                p.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(ent.FullPath))).ThrowIfFailure();
                                chkFs.OpenFile(ref fRef.Ref, in p, OpenMode.Read).ThrowIfFailure();
                                using var sr = new StreamReader(fRef.Release().AsStream());
                                string xml = sr.ReadToEnd();
                                var matches = System.Text.RegularExpressions.Regex.Matches(xml, @"(?s)<Content>.*?<Type>DeltaFragment</Type>.*?<Id>([a-fA-F0-9]{32})</Id>.*?</Content>");
                                foreach (System.Text.RegularExpressions.Match m in matches)
                                {
                                    if (m.Groups.Count > 1) deltaNcas.Add(m.Groups[1].Value.ToLowerInvariant() + ".nca");
                                }
                                var matches2 = System.Text.RegularExpressions.Regex.Matches(xml, @"(?s)<Content>.*?<Id>([a-fA-F0-9]{32})</Id>.*?<Type>DeltaFragment</Type>.*?</Content>");
                                foreach (System.Text.RegularExpressions.Match m in matches2)
                                {
                                    if (m.Groups.Count > 1) deltaNcas.Add(m.Groups[1].Value.ToLowerInvariant() + ".nca");
                                }
                            }
                            catch { }
                        }
                        // 2. РџСЂРѕРІРµСЂРєР° Р±РёРЅР°СЂРЅРѕРіРѕ CNMT РІРЅСѓС‚СЂРё .cnmt.nca
                        else if (ent.Name.EndsWith(".cnmt.nca", StringComparison.OrdinalIgnoreCase) || 
                                 (ent.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) && ent.Name.Length >= 36))
                        {
                            try
                            {
                                using var fRef = new UniqueRef<IFile>();
                                using var p = new LibHac.Fs.Path();
                                p.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(ent.FullPath))).ThrowIfFailure();
                                if (chkFs.OpenFile(ref fRef.Ref, in p, OpenMode.Read).IsSuccess())
                                {
                                    var ncaStorage = fRef.Release().AsStorage();
                                    var nca = new Nca(App.Keys.CurrentKeyset, ncaStorage);
                                    if (nca.Header.ContentType == NcaContentType.Meta)
                                    {
                                        IFileSystem? romfs = null;
                                        try { romfs = nca.OpenFileSystem(0, IntegrityCheckLevel.None); } catch { }
                                        if (romfs == null)
                                        {
                                            try { romfs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None); } catch { }
                                        }

                                        if (romfs != null)
                                        {
                                            foreach (var f in romfs.EnumerateEntries())
                                            {
                                                if (f.Name.EndsWith(".cnmt", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    using var cfRef = new UniqueRef<IFile>();
                                                    using var cp = new LibHac.Fs.Path();
                                                    cp.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(f.FullPath))).ThrowIfFailure();
                                                    if (romfs.OpenFile(ref cfRef.Ref, in cp, OpenMode.Read).IsSuccess())
                                                    {
                                                        using var ms = new MemoryStream();
                                                        cfRef.Release().AsStream().CopyTo(ms);
                                                        ms.Position = 0;
                                                        var cnmt = new LibHac.Tools.Ncm.Cnmt(ms);
                                                        foreach (var cEntry in cnmt.ContentEntries)
                                                        {
                                                            if (cEntry.Type == LibHac.Ncm.ContentType.DeltaFragment && cEntry.NcaId != null)
                                                            {
                                                                string hex = Convert.ToHexString(cEntry.NcaId).ToLowerInvariant();
                                                                deltaNcas.Add(hex + ".nca");
                                                            }
                                                        }
                                                    }
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
            return deltaNcas;
        }
    }
}

