using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using Path = System.IO.Path;
using File = System.IO.File;
using Directory = System.IO.Directory;
using FileStream = System.IO.FileStream;
using FileMode = System.IO.FileMode;
using FileAccess = System.IO.FileAccess;
using FileShare = System.IO.FileShare;
using LibHac;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public class HomebrewPackageInfo
    {
        public string Name { get; set; } = "Homebrew";
        public string Author { get; set; } = "Homebrew Developer";
        public string Version { get; set; } = "1.0.0";
        public string TitleId { get; set; } = "0500000000001000";
        public byte[]? IconBytes { get; set; }
        public BitmapImage? IconImage { get; set; }
        public string PrimaryNroPath { get; set; } = string.Empty;
        public string? RomFsDir { get; set; }
        public string? ExeFsDir { get; set; }
        public string? SaveDataDir { get; set; }
        public int SaveFilesCount { get; set; }
        public List<string> InputFiles { get; set; } = new();
        public bool IsOverlay { get; set; }
        public string RelativeSdPath { get; set; } = string.Empty;
        public string? OutputFileName { get; set; }
        public string? RootDir { get; set; }
    }

    public class HomebrewService
    {
        /// <summary>
        /// Извлечение эталонного имени релиза вида "{GameFolder} {SubFolder}" если подпапка содержит тег [Homebrew]
        /// </summary>
        public static (string? OutputFileName, string? CleanGameName) ResolveReleaseNaming(string pathOrDir)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(pathOrDir)) return (null, null);
                DirectoryInfo? cur = Directory.Exists(pathOrDir) ? new DirectoryInfo(pathOrDir) : new FileInfo(pathOrDir).Directory;
                string subFolder = "";
                string gameFolder = "";

                // Если передана папка верхнего уровня игры (например, DOWNLOADS\Mario Kart 64), ищем подпапку [Homebrew]
                if (cur != null && Directory.Exists(cur.FullName))
                {
                    try
                    {
                        var hbSub = cur.GetDirectories("*[Homebrew]*", SearchOption.TopDirectoryOnly).FirstOrDefault();
                        if (hbSub != null)
                        {
                            subFolder = hbSub.Name;
                            if (!cur.Name.Equals("DOWNLOADS", StringComparison.OrdinalIgnoreCase) &&
                                !cur.Name.Equals("Nintendo Switch", StringComparison.OrdinalIgnoreCase))
                            {
                                gameFolder = cur.Name;
                            }
                        }
                    }
                    catch { }
                }

                if (string.IsNullOrEmpty(subFolder))
                {
                    while (cur != null && cur.FullName.Length >= 3)
                    {
                        if (cur.Name.Contains("[Homebrew]", StringComparison.OrdinalIgnoreCase))
                        {
                            subFolder = cur.Name;
                            if (cur.Parent != null && 
                                !cur.Parent.Name.Equals("DOWNLOADS", StringComparison.OrdinalIgnoreCase) && 
                                !cur.Parent.Name.Equals("Nintendo Switch", StringComparison.OrdinalIgnoreCase))
                            {
                                gameFolder = cur.Parent.Name;
                            }
                            break;
                        }
                        cur = cur.Parent;
                    }
                }

                if (!string.IsNullOrEmpty(subFolder))
                {
                    string outName = (!string.IsNullOrEmpty(gameFolder) && !subFolder.StartsWith(gameFolder, StringComparison.OrdinalIgnoreCase))
                        ? $"{gameFolder} {subFolder}".Trim()
                        : subFolder;

                    return (outName, !string.IsNullOrEmpty(gameFolder) ? gameFolder : null);
                }
            }
            catch { }
            return (null, null);
        }

        public static string? ResolveReleaseOutputFileName(string pathOrDir) => ResolveReleaseNaming(pathOrDir).OutputFileName;

        private readonly KeysService _keysService;
        private readonly string _toolsDir;
        private readonly string _hacpackExe;

        public HomebrewService(KeysService keysService)
        {
            _keysService = keysService;
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string[] searchDirs = 
            {
                Path.Combine(appDir, "tools"),
                Path.Combine(appDir, "..", "tools"),
                Path.Combine(appDir, "..", "..", "..", "tools"),
                Path.Combine(appDir, "..", "..", "..", "..", "tools"),
                Path.Combine(appDir, "..", "..", "..", "..", "..", "tools"),
                @"E:\STORM SWITCH BOX\tools"
            };

            string foundHacpack = "";
            string foundTools = "";
            foreach (var sDir in searchDirs)
            {
                if (Directory.Exists(sDir))
                {
                    if (string.IsNullOrEmpty(foundTools)) foundTools = sDir;
                    string c1 = Path.Combine(sDir, "com.github.nozwock.yanu", "hacpack.exe");
                    string c2 = Path.Combine(sDir, "hacpack.exe");
                    if (File.Exists(c1)) { foundHacpack = c1; break; }
                    if (File.Exists(c2)) { foundHacpack = c2; break; }
                }
            }

            _toolsDir = !string.IsNullOrEmpty(foundTools) ? foundTools : Path.Combine(appDir, "tools");
            _hacpackExe = !string.IsNullOrEmpty(foundHacpack) ? foundHacpack : Path.Combine(_toolsDir, "com.github.nozwock.yanu", "hacpack.exe");
        }

        /// <summary>
        /// Умное определение и разделение входных файлов/папок на отдельные пакеты Homebrew
        /// </summary>
        public async Task<List<HomebrewPackageInfo>> IdentifyHomebrewPackagesAsync(List<string> paths)
        {
            var results = new List<HomebrewPackageInfo>();
            if (paths == null || paths.Count == 0) return results;

            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;

                if (File.Exists(path))
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    if (ext == ".nro" || ext == ".ovl" || ext == ".elf")
                    {
                        var info = await ParseHomebrewFileAsync(path);
                        if (info != null) results.Add(info);
                    }
                    else if (ext == ".nsp" || ext == ".nsz")
                    {
                        // Проверяем, является ли перетащенный NSP/NSZ Homebrew форвардером
                        var info = await TryParseHomebrewForwarderNspAsync(path);
                        if (info != null) results.Add(info);
                    }
                }
                else if (Directory.Exists(path))
                {
                    var folderPackages = await ScanDirectoryForHomebrewAsync(path);
                    results.AddRange(folderPackages);
                }
            }

            return results;
        }

        private async Task<List<HomebrewPackageInfo>> ScanDirectoryForHomebrewAsync(string rootDir)
        {
            var packages = new List<HomebrewPackageInfo>();

            try
            {
                // Если в переданной директории содержатся поддиректории [Homebrew]...
                var hbSubDirs = Directory.GetDirectories(rootDir, "*[Homebrew]*", SearchOption.TopDirectoryOnly);
                if (hbSubDirs.Length > 0)
                {
                    foreach (var hbSub in hbSubDirs)
                    {
                        var subPackages = await ScanDirectoryForHomebrewAsync(hbSub);
                        packages.AddRange(subPackages);
                    }
                    if (packages.Count > 0) return packages;
                }

                // 1. Ищем все исполняемые файлы Homebrew (.nro, .ovl, .elf) во всей структуре папки
                var allNros = Directory.GetFiles(rootDir, "*.nro", SearchOption.AllDirectories);
                var allOvls = Directory.GetFiles(rootDir, "*.ovl", SearchOption.AllDirectories);
                var allNsps = Directory.GetFiles(rootDir, "*.nsp", SearchOption.AllDirectories);

                // Если найдена папка игры (например, Diablo I с devilutionx.nro + mpq/ini/nsp файлами)
                if (allNros.Length > 0 || allOvls.Length > 0)
                {
                    // Если несколько различных NRO в разных несвязанных подпапках switch/...
                    var nroGroups = allNros.GroupBy(n => Path.GetDirectoryName(n) ?? "").ToList();

                    // Если один NRO или все NRO принадлежат одной игре/проекту
                    if (allNros.Length == 1 || nroGroups.Count == 1)
                    {
                        string primaryNro = allNros.FirstOrDefault() ?? allOvls.First();
                        var pkg = await ParseFullHomebrewDirectoryPackageAsync(primaryNro, rootDir, allNsps);
                        if (pkg != null)
                        {
                            packages.Add(pkg);
                            return packages;
                        }
                    }
                    else
                    {
                        // Несколько независимых homebrew в подпапках
                        foreach (var group in nroGroups)
                        {
                            string primaryNro = group.First();
                            string subDir = group.Key;
                            var subNsps = Directory.GetFiles(subDir, "*.nsp", SearchOption.AllDirectories);
                            var pkg = await ParseFullHomebrewDirectoryPackageAsync(primaryNro, subDir, subNsps);
                            if (pkg != null) packages.Add(pkg);
                        }
                        return packages;
                    }
                }

                // 2. Если NRO не найдены, но есть NSP форвардеры в папке
                if (allNsps.Length > 0)
                {
                    foreach (var nsp in allNsps)
                    {
                        var info = await TryParseHomebrewForwarderNspAsync(nsp, rootDir);
                        if (info != null) packages.Add(info);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[HomebrewService] Ошибка сканирования {rootDir}: {ex.Message}", LogLevel.Warning);
            }

            return packages;
        }

        /// <summary>
        /// Парсинг одиночного Homebrew файла (.nro, .ovl, .elf)
        /// </summary>
        public async Task<HomebrewPackageInfo?> ParseHomebrewFileAsync(string filePath, string? associatedDir = null)
        {
            if (!File.Exists(filePath)) return null;
            string dir = associatedDir ?? Path.GetDirectoryName(filePath) ?? "";
            string[] companionNsps = !string.IsNullOrEmpty(dir) && Directory.Exists(dir)
                ? Directory.GetFiles(dir, "*.nsp", SearchOption.TopDirectoryOnly)
                : Array.Empty<string>();

            return await ParseFullHomebrewDirectoryPackageAsync(filePath, dir, companionNsps);
        }

        /// <summary>
        /// Комплексный парсинг Homebrew игры со всеми данными, дополнениями, переводами и конфигами
        /// </summary>
        private async Task<HomebrewPackageInfo?> ParseFullHomebrewDirectoryPackageAsync(string primaryNro, string rootDir, string[] companionNsps)
        {
            if (!File.Exists(primaryNro)) return null;

            string nroFileName = Path.GetFileName(primaryNro);
            string nroBaseName = Path.GetFileNameWithoutExtension(primaryNro);
            string nroDir = Path.GetDirectoryName(primaryNro) ?? rootDir;
            string rootDirName = Path.GetFileName(rootDir);

            var pkg = new HomebrewPackageInfo
            {
                PrimaryNroPath = primaryNro,
                IsOverlay = Path.GetExtension(primaryNro).Equals(".ovl", StringComparison.OrdinalIgnoreCase),
                Name = nroBaseName,
                RelativeSdPath = $"switch/{Path.GetFileName(nroDir)}/{nroFileName}",
                RootDir = rootDir
            };

            var (relOutputName, cleanGame) = ResolveReleaseNaming(rootDir);
            if (string.IsNullOrEmpty(relOutputName))
            {
                var rel2 = ResolveReleaseNaming(primaryNro);
                relOutputName = rel2.OutputFileName;
                if (string.IsNullOrEmpty(cleanGame)) cleanGame = rel2.CleanGameName;
            }
            if (!string.IsNullOrEmpty(relOutputName))
            {
                pkg.OutputFileName = relOutputName;
            }

            // Добавляем основной исполняемый файл
            pkg.InputFiles.Add(primaryNro);

            // Добавляем все верхнеуровневые директории данных игры (switch, assets, TheXTech, devilutionx-switch, etc.)
            try
            {
                foreach (var d in Directory.GetDirectories(rootDir))
                {
                    string dirName = Path.GetFileName(d);
                    if (dirName.Equals("romfs", StringComparison.OrdinalIgnoreCase) ||
                        dirName.Equals("exefs", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!pkg.InputFiles.Contains(d, StringComparer.OrdinalIgnoreCase))
                    {
                        pkg.InputFiles.Add(d);
                    }
                }
            }
            catch { }

            // 1. Проверяем папки romfs, exefs, save
            var allRomfs = Directory.GetDirectories(rootDir, "romfs", SearchOption.AllDirectories);
            if (allRomfs.Length > 0)
            {
                pkg.RomFsDir = allRomfs[0];
                foreach (var r in allRomfs)
                {
                    if (!pkg.InputFiles.Contains(r, StringComparer.OrdinalIgnoreCase))
                        pkg.InputFiles.Add(r);
                }
            }

            var allExefs = Directory.GetDirectories(rootDir, "exefs", SearchOption.AllDirectories);
            if (allExefs.Length > 0)
            {
                pkg.ExeFsDir = allExefs[0];
                foreach (var e in allExefs)
                {
                    if (!pkg.InputFiles.Contains(e, StringComparer.OrdinalIgnoreCase))
                        pkg.InputFiles.Add(e);
                }
            }

            string[] saveDirNames = { "save", "saves", "savedata", "save_data", "checkpoint", "jksv", "edizon" };
            foreach (var sName in saveDirNames)
            {
                var allSaves = Directory.GetDirectories(rootDir, sName, SearchOption.AllDirectories);
                if (allSaves.Length > 0)
                {
                    pkg.SaveDataDir = allSaves[0];
                    try { pkg.SaveFilesCount = Directory.GetFiles(allSaves[0], "*", SearchOption.AllDirectories).Length; } catch { }
                    foreach (var s in allSaves)
                    {
                        if (!pkg.InputFiles.Contains(s, StringComparer.OrdinalIgnoreCase))
                            pkg.InputFiles.Add(s);
                    }
                    break;
                }
            }

            // 2. Поиск и сбор ВСЕХ файлов и ресурсов игры (.rpf, .mpq, .wad, .pck, .o2r, .nes, .so, .dll, etc.)
            var excludedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".nsp", ".nsz", ".xci", ".xcz", ".tar", ".gz",
                ".tmp", ".bak", ".ds_store"
            };

            try
            {
                var allFiles = Directory.GetFiles(rootDir, "*.*", SearchOption.AllDirectories);
                foreach (var f in allFiles)
                {
                    if (f.Equals(primaryNro, StringComparison.OrdinalIgnoreCase)) continue;
                    if (pkg.RomFsDir != null && f.StartsWith(pkg.RomFsDir, StringComparison.OrdinalIgnoreCase)) continue;
                    if (pkg.ExeFsDir != null && f.StartsWith(pkg.ExeFsDir, StringComparison.OrdinalIgnoreCase)) continue;
                    if (pkg.SaveDataDir != null && f.StartsWith(pkg.SaveDataDir, StringComparison.OrdinalIgnoreCase)) continue;

                    // Не добавляем файлы, которые уже входят в любую добавленную директорию
                    if (pkg.InputFiles.Any(d => Directory.Exists(d) && f.StartsWith(d.TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))) continue;

                    string ext = Path.GetExtension(f);
                    string fileName = Path.GetFileName(f);

                    if (excludedExtensions.Contains(ext)) continue;
                    if (fileName.StartsWith("ReadMe", StringComparison.OrdinalIgnoreCase) || 
                        fileName.StartsWith("Changelog", StringComparison.OrdinalIgnoreCase) || 
                        fileName.Equals("In-Game Cheats.txt", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".odt", StringComparison.OrdinalIgnoreCase) ||
                        ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!pkg.InputFiles.Contains(f, StringComparer.OrdinalIgnoreCase))
                    {
                        pkg.InputFiles.Add(f);
                    }
                }
            }
            catch { }

            // 3. Извлечение метаданных из сопутствующего NSP форвардера (если есть)
            string? companionNsp = companionNsps?.FirstOrDefault(f => File.Exists(f));
            if (companionNsp != null)
            {
                if (!pkg.InputFiles.Contains(companionNsp, StringComparer.OrdinalIgnoreCase))
                {
                    pkg.InputFiles.Add(companionNsp);
                }
                try
                {
                    await ExtractCompanionNspMetadataAsync(pkg, companionNsp);
                }
                catch { }
            }

            // 4. Парсим ASET заголовок внутри NRO (если метаданные еще не заполнены)
            if (Path.GetExtension(primaryNro).Equals(".nro", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await Task.Run(() => ParseNroAset(pkg, primaryNro));
                }
                catch { }
            }

            // 5. Поиск кастомной обложки/иконки в папке игры (icon.png, cover.jpg, folder.jpg, etc.)
            if (pkg.IconBytes == null || pkg.IconBytes.Length == 0)
            {
                string[] iconNames = { "icon.png", "icon.jpg", "cover.png", "cover.jpg", "folder.jpg", "folder.png", "boxart.png", "logo.png" };
                foreach (var iName in iconNames)
                {
                    string candidate = Path.Combine(nroDir, iName);
                    if (!File.Exists(candidate)) candidate = Path.Combine(rootDir, iName);
                    if (File.Exists(candidate))
                    {
                        try { pkg.IconBytes = File.ReadAllBytes(candidate); break; } catch { }
                    }
                }
            }

            // 6. Формирование читаемого названия игры
            // Предустановленные профили популярных портов и Homebrew проектов
            // 1. FPS & Retro Shooters
            if (nroBaseName.Equals("devilutionx", StringComparison.OrdinalIgnoreCase))
            {
                bool hasHellfire = pkg.InputFiles.Any(f => f.Contains("hellfire", StringComparison.OrdinalIgnoreCase) || f.Contains("hf", StringComparison.OrdinalIgnoreCase));
                bool hasRus = pkg.InputFiles.Any(f => f.Contains("ru.mpq", StringComparison.OrdinalIgnoreCase) || f.Contains("Russian", StringComparison.OrdinalIgnoreCase));

                if (hasHellfire && hasRus)
                    pkg.Name = "Diablo I (DevilutionX + Hellfire + Rus)";
                else if (hasHellfire)
                    pkg.Name = "Diablo I (DevilutionX + Hellfire)";
                else if (hasRus)
                    pkg.Name = "Diablo I (DevilutionX + Rus)";
                else
                    pkg.Name = "Diablo I (DevilutionX)";
                pkg.RelativeSdPath = $"switch/devilutionx/{nroFileName}";
            }
            else if (nroBaseName.Equals("d2x", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("d2x-switch", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Diablo II (D2X Switch Port)";
                pkg.RelativeSdPath = $"switch/d2x/{nroFileName}";
            }
            else if (nroBaseName.Equals("re3", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("gta3", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Grand Theft Auto III (re3 Port)";
                pkg.RelativeSdPath = $"switch/re3/{nroFileName}";
            }
            else if (nroBaseName.Equals("reVC", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("gtavc", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Grand Theft Auto: Vice City (reVC Port)";
                pkg.RelativeSdPath = $"switch/reVC/{nroFileName}";
            }
            else if (nroBaseName.Equals("gtasa", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Grand Theft Auto: San Andreas (Switch Port)";
                pkg.RelativeSdPath = $"switch/gtasa/{nroFileName}";
            }
            else if (nroBaseName.Equals("gtav", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("gta5", StringComparison.OrdinalIgnoreCase) || rootDirName.Contains("GTA V", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Grand Theft Auto V (Homebrew Port)";
                pkg.RelativeSdPath = $"switch/gtav/{nroFileName}";
            }
            else if (nroBaseName.Equals("hl2", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("portal", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("switch-source", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("stratasource", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = nroBaseName.Equals("portal", StringComparison.OrdinalIgnoreCase) ? "Portal (Source Engine Port)" : "Half-Life 2 (Source Engine Port)";
                pkg.RelativeSdPath = $"switch/source/{nroFileName}";
            }
            else if (nroBaseName.Equals("cs16client", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("cstrike", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Counter-Strike 1.6 (CS16Client Engine)";
                pkg.RelativeSdPath = $"switch/xash3d/cstrike/{nroFileName}";
            }
            else if (nroBaseName.Equals("dod", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Day of Defeat (Xash3D Engine)";
                pkg.RelativeSdPath = $"switch/xash3d/dod/{nroFileName}";
            }
            else if (nroBaseName.Equals("tfc", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Team Fortress Classic (Xash3D Engine)";
                pkg.RelativeSdPath = $"switch/xash3d/tfc/{nroFileName}";
            }
            else if (nroBaseName.Equals("gunman", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Gunman Chronicles (Xash3D Engine)";
                pkg.RelativeSdPath = $"switch/xash3d/gunman/{nroFileName}";
            }
            else if (nroBaseName.Equals("theforceengine", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("theforce", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("darkforces", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Star Wars: Dark Forces (The Force Engine)";
                pkg.RelativeSdPath = $"switch/tfe/{nroFileName}";
            }
            else if (nroBaseName.Equals("alephone", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("marathon", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("marathon2", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("infinity", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Marathon Trilogy (Aleph One Engine)";
                pkg.RelativeSdPath = $"switch/alephone/{nroFileName}";
            }
            else if (nroBaseName.Equals("avp", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("avp-switch", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Aliens versus Predator Classic 2000";
                pkg.RelativeSdPath = $"switch/avp/{nroFileName}";
            }
            else if (nroBaseName.Equals("uhexen2", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("hammerofthyrion", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Hexen II (Hammer of Thyrion Engine)";
                pkg.RelativeSdPath = $"switch/hexen2/{nroFileName}";
            }
            else if (nroBaseName.Equals("crispy-heretic", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("crispy-hexen", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Heretic / Hexen Classic (Crispy Engine)";
                pkg.RelativeSdPath = $"switch/crispy/{nroFileName}";
            }
            else if (nroBaseName.Equals("dxx-rebirth", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("d1x", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("d2x-rebirth", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Descent 1 and 2 (DXX-Rebirth Engine)";
                pkg.RelativeSdPath = $"switch/dxx-rebirth/{nroFileName}";
            }
            else if (nroBaseName.Equals("rott", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("winrott", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Rise of the Triad (RotT Engine)";
                pkg.RelativeSdPath = $"switch/rott/{nroFileName}";
            }
            else if (nroBaseName.Equals("chocolate-strife", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("strife", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Strife: Quest for the Sigil";
                pkg.RelativeSdPath = $"switch/strife/{nroFileName}";
            }
            else if (nroBaseName.Equals("openfodder", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Cannon Fodder 1 and 2 (OpenFodder Engine)";
                pkg.RelativeSdPath = $"switch/openfodder/{nroFileName}";
            }
            else if (nroBaseName.Equals("maxpayne", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("maxpayne-switch", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Max Payne (Switch Port)";
                pkg.RelativeSdPath = $"switch/maxpayne/{nroFileName}";
            }
            else if (nroBaseName.Equals("xash3d", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("xash3d-switch", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("hl", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Half-Life (Xash3D FWGS Engine)";
                pkg.RelativeSdPath = $"switch/xash3d/{nroFileName}";
            }
            else if (nroBaseName.Equals("ioquake3", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("quake3", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Quake III Arena (ioquake3)";
                pkg.RelativeSdPath = $"switch/ioquake3/{nroFileName}";
            }
            else if (nroBaseName.Equals("dhewm3", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("doom3", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Doom 3 (dhewm3 Engine)";
                pkg.RelativeSdPath = $"switch/dhewm3/{nroFileName}";
            }
            else if (nroBaseName.Equals("serioussam", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("ssam", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Serious Sam Classic (Switch Port)";
                pkg.RelativeSdPath = $"switch/serioussam/{nroFileName}";
            }
            else if (nroBaseName.Equals("nblood", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("blood", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Blood (NBlood Port)";
                pkg.RelativeSdPath = $"switch/nblood/{nroFileName}";
            }
            else if (nroBaseName.Equals("voidsw", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("shadowwarrior", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Shadow Warrior (VoidSW Port)";
                pkg.RelativeSdPath = $"switch/voidsw/{nroFileName}";
            }
            else if (nroBaseName.Equals("pcexhumed", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("powerslave", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "PowerSlave / Exhumed (PCExhumed)";
                pkg.RelativeSdPath = $"switch/pcexhumed/{nroFileName}";
            }
            else if (nroBaseName.Equals("iortcw", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Return to Castle Wolfenstein (iortcw)";
                pkg.RelativeSdPath = $"switch/iortcw/{nroFileName}";
            }
            else if (nroBaseName.Equals("ecwolf", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Wolfenstein 3D (ECWolf)";
                pkg.RelativeSdPath = $"switch/ecwolf/{nroFileName}";
            }
            else if (nroBaseName.Equals("eduke32", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("rednukem", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Duke Nukem 3D (EDuke32/Rednukem)";
                pkg.RelativeSdPath = $"switch/{nroBaseName}/{nroFileName}";
            }
            else if (nroBaseName.Equals("gzswitch", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("gzdoom", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("prboom-plus", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Doom / GZDoom (Switch Port)";
                pkg.RelativeSdPath = $"switch/{nroBaseName}/{nroFileName}";
            }

            // 2. RPG & RTS / Strategy
            else if (nroBaseName.Equals("daggerfallunity", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("daggerfall", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "The Elder Scrolls II: Daggerfall (Unity Port)";
                pkg.RelativeSdPath = $"switch/daggerfall/{nroFileName}";
            }
            else if (nroBaseName.Equals("opentesarena", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("arena", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "The Elder Scrolls I: Arena (OpenTESArena)";
                pkg.RelativeSdPath = $"switch/opentesarena/{nroFileName}";
            }
            else if (nroBaseName.Equals("openmw", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("morrowind", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "The Elder Scrolls III: Morrowind (OpenMW Engine)";
                pkg.RelativeSdPath = $"switch/openmw/{nroFileName}";
            }
            else if (nroBaseName.Equals("opengothic", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("gothic", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Gothic (OpenGothic Engine)";
                pkg.RelativeSdPath = $"switch/opengothic/{nroFileName}";
            }
            else if (nroBaseName.Equals("gemrb", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("bg", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("iwd", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Baldur's Gate / Icewind Dale (GemRB Engine)";
                pkg.RelativeSdPath = $"switch/gemrb/{nroFileName}";
            }
            else if (nroBaseName.Equals("fallout-ce", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("fallout", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Fallout: Community Edition (Port)";
                pkg.RelativeSdPath = $"switch/fallout-ce/{nroFileName}";
            }
            else if (nroBaseName.Equals("fallout2-ce", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("fallout2", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Fallout 2: Community Edition (Port)";
                pkg.RelativeSdPath = $"switch/fallout2-ce/{nroFileName}";
            }
            else if (nroBaseName.Equals("fallouttactics", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("ftactics", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Fallout Tactics: Brotherhood of Steel";
                pkg.RelativeSdPath = $"switch/ftactics/{nroFileName}";
            }
            else if (nroBaseName.Equals("wargus", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("stratagus", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("warcraft", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("warcraft2", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Warcraft: Orcs & Humans / Warcraft II (Stratagus)";
                pkg.RelativeSdPath = $"switch/wargus/{nroFileName}";
            }
            else if (nroBaseName.Equals("stargus", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("starcraft", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "StarCraft & Brood War (Stargus Engine)";
                pkg.RelativeSdPath = $"switch/stargus/{nroFileName}";
            }
            else if (nroBaseName.Equals("openra", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("chronodivide", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("cnc", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("redalert", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Command & Conquer / Red Alert (OpenRA Engine)";
                pkg.RelativeSdPath = $"switch/openra/{nroFileName}";
            }
            else if (nroBaseName.Equals("openage", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("aoe2", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Age of Empires II (OpenAge Engine)";
                pkg.RelativeSdPath = $"switch/openage/{nroFileName}";
            }
            else if (nroBaseName.Equals("dunelegacy", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("opendune", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("dune2", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Dune II / Dune Legacy Engine";
                pkg.RelativeSdPath = $"switch/dunelegacy/{nroFileName}";
            }
            else if (nroBaseName.Equals("openxcom", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("xcom", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "X-COM: UFO Defense / Terror from the Deep (OpenXcom)";
                pkg.RelativeSdPath = $"switch/openxcom/{nroFileName}";
            }
            else if (nroBaseName.Equals("freeciv", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "FreeCiv (Civilization Strategy Engine)";
                pkg.RelativeSdPath = $"switch/freeciv/{nroFileName}";
            }
            else if (nroBaseName.Equals("openalbion", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("albion", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Albion Classic RPG (OpenAlbion Engine)";
                pkg.RelativeSdPath = $"switch/openalbion/{nroFileName}";
            }
            else if (nroBaseName.Equals("fheroes2", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Heroes of Might and Magic II (fheroes2 Engine)";
                pkg.RelativeSdPath = $"switch/fheroes2/{nroFileName}";
            }
            else if (nroBaseName.Equals("vcmi", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Heroes of Might and Magic III (VCMI Engine)";
                pkg.RelativeSdPath = $"switch/vcmi/{nroFileName}";
            }
            else if (nroBaseName.Equals("corsixth", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("themehospital", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Theme Hospital (CorsixTH Engine)";
                pkg.RelativeSdPath = $"switch/corsixth/{nroFileName}";
            }
            else if (nroBaseName.Equals("openttd", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("ttd", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Transport Tycoon Deluxe (OpenTTD)";
                pkg.RelativeSdPath = $"switch/openttd/{nroFileName}";
            }
            else if (nroBaseName.Equals("julius", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("augustus", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("caesar3", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Caesar III (Julius / Augustus Engine)";
                pkg.RelativeSdPath = $"switch/julius/{nroFileName}";
            }
            else if (nroBaseName.Equals("openrct2", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("rct2", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "RollerCoaster Tycoon 2 (OpenRCT2 Engine)";
                pkg.RelativeSdPath = $"switch/openrct2/{nroFileName}";
            }

            // 3. Action, Platformer & Adventure
            else if (nroBaseName.Equals("sonic3air", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Sonic 3 A.I.R. (Angel Island Revisited)";
                pkg.RelativeSdPath = $"switch/sonic3air/{nroFileName}";
            }
            else if (nroBaseName.Equals("srb2kart", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("ringracers", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("srb2", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Sonic Robo Blast 2 / Ring Racers";
                pkg.RelativeSdPath = $"switch/srb2/{nroFileName}";
            }
            else if (nroBaseName.Equals("rayman2", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Rayman 2: The Great Escape (Native Port)";
                pkg.RelativeSdPath = $"switch/rayman2/{nroFileName}";
            }
            else if (nroBaseName.Equals("openjazz", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("jazz2", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Jazz Jackrabbit 1 and 2 (Native Engine)";
                pkg.RelativeSdPath = $"switch/jazz2/{nroFileName}";
            }
            else if (nroBaseName.Equals("perfectdark", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Perfect Dark (Native 60FPS PC Port)";
                pkg.RelativeSdPath = $"switch/perfectdark/{nroFileName}";
            }
            else if (nroBaseName.Equals("classicube", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "ClassiCube (Minecraft Classic Native Port)";
                pkg.RelativeSdPath = $"switch/classicube/{nroFileName}";
            }
            else if (nroBaseName.Equals("minetest", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("luanti", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Minetest / Luanti (Open-Source Voxel World)";
                pkg.RelativeSdPath = $"switch/minetest/{nroFileName}";
            }
            else if (nroBaseName.Equals("supertux", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("supertuxkart", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "SuperTux / SuperTuxKart";
                pkg.RelativeSdPath = $"switch/supertux/{nroFileName}";
            }
            else if (nroBaseName.Equals("celeste64", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Celeste 64: Fragments of the Mountain";
                pkg.RelativeSdPath = $"switch/celeste64/{nroFileName}";
            }
            else if (nroBaseName.Equals("sdlpop", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("pop", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Prince of Persia Classic (SDLPoP)";
                pkg.RelativeSdPath = $"switch/sdlpop/{nroFileName}";
            }
            else if (nroBaseName.Equals("reminiscence", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Flashback (REminiscence Engine)";
                pkg.RelativeSdPath = $"switch/reminiscence/{nroFileName}";
            }
            else if (nroBaseName.Equals("raw", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("anotherworld", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Another World / Out of This World (RAW Engine)";
                pkg.RelativeSdPath = $"switch/raw/{nroFileName}";
            }
            else if (nroBaseName.Equals("hode", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Heart of Darkness (Hode Engine)";
                pkg.RelativeSdPath = $"switch/hode/{nroFileName}";
            }
            else if (nroBaseName.Equals("twine", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("lba", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Little Big Adventure 1 and 2 (Twin-E Engine)";
                pkg.RelativeSdPath = $"switch/twine/{nroFileName}";
            }
            else if (nroBaseName.Equals("malditacastilla", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Maldita Castilla (Cursed Castilla)";
                pkg.RelativeSdPath = $"switch/malditacastilla/{nroFileName}";
            }
            else if (nroBaseName.Equals("hcl", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("hydracastle", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Hydra Castle Labyrinth";
                pkg.RelativeSdPath = $"switch/hcl/{nroFileName}";
            }
            else if (nroBaseName.Equals("sm64", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("sm64ex", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("sm64nx", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Super Mario 64 (Native Port)";
                pkg.RelativeSdPath = $"switch/sm64/{nroFileName}";
            }
            else if (nroBaseName.Equals("zelda3", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "The Legend of Zelda: A Link to the Past (Native Port)";
                pkg.RelativeSdPath = $"switch/zelda3/{nroFileName}";
            }
            else if (nroBaseName.Equals("soh", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Ship of Harkinian (Zelda: Ocarina of Time)";
                pkg.RelativeSdPath = $"switch/soh/{nroFileName}";
            }
            else if (nroBaseName.Equals("2ship", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "2 Ship 2 Harkinian (Zelda: Majora's Mask)";
                pkg.RelativeSdPath = $"switch/2ship/{nroFileName}";
            }
            else if (nroBaseName.Equals("am2r", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("am2r-switch", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Another Metroid 2 Remake (AM2R Native Port)";
                pkg.RelativeSdPath = $"switch/am2r/{nroFileName}";
            }
            else if (nroBaseName.Equals("celeste", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("celestec", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Celeste Classic (C Native Port)";
                pkg.RelativeSdPath = $"switch/celeste/{nroFileName}";
            }
            else if (nroBaseName.Equals("openjk", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("jedioutcast", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("jediia", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Star Wars: Jedi Knight (OpenJK Engine)";
                pkg.RelativeSdPath = $"switch/openjk/{nroFileName}";
            }
            else if (nroBaseName.Equals("nxengine", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("cavestory", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Cave Story (NXEngine-evo)";
                pkg.RelativeSdPath = $"switch/nxengine/{nroFileName}";
            }
            else if (nroBaseName.Equals("cgenius", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("keen", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Commander Keen (Commander Genius)";
                pkg.RelativeSdPath = $"switch/cgenius/{nroFileName}";
            }
            else if (nroBaseName.Equals("opentyrian", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("tyrian", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Tyrian (OpenTyrian)";
                pkg.RelativeSdPath = $"switch/opentyrian/{nroFileName}";
            }
            else if (nroBaseName.Equals("openlara", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("openlara-switch", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Tomb Raider I (OpenLara Classic Engine)";
                pkg.RelativeSdPath = $"switch/openlara/{nroFileName}";
            }
            else if (nroBaseName.Equals("spelunky", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Spelunky Classic HD (Switch Port)";
                pkg.RelativeSdPath = $"switch/spelunky/{nroFileName}";
            }
            else if (nroBaseName.Equals("vvvvvv", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "VVVVVV (Native Port)";
                pkg.RelativeSdPath = $"switch/vvvvvv/{nroFileName}";
            }
            else if (nroBaseName.Equals("soniccd", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("sonic1", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("sonic2", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("sonicmania", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = $"Sonic ({nroBaseName.ToUpperInvariant()} Engine Port)";
                pkg.RelativeSdPath = $"switch/{nroBaseName}/{nroFileName}";
            }
            else if (nroBaseName.Equals("thextech", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Super Mario Bros. X (TheXTech Engine)";
                pkg.RelativeSdPath = $"switch/thextech/{nroFileName}";
            }
            else if (nroBaseName.Equals("drmario", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("drmariomania", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Dr. Mario Mania (Switch Port)";
                pkg.RelativeSdPath = $"switch/drmariomania_nx/{nroFileName}";
            }

            // 4. Visual Novels & Narrative Engines
            else if (nroBaseName.Equals("renpy", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("ddlc", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Ren'Py Visual Novel Engine";
                pkg.RelativeSdPath = $"switch/renpy/{nroFileName}";
            }
            else if (nroBaseName.Equals("onscripter", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("ponscripter", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("tsukihime", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("higurashi", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("umineko", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "ONScripter (Visual Novel Engine)";
                pkg.RelativeSdPath = $"switch/onscripter/{nroFileName}";
            }
            else if (nroBaseName.Equals("vnds", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "VNDS (Visual Novel Dual Screen Engine)";
                pkg.RelativeSdPath = $"switch/vnds/{nroFileName}";
            }
            else if (nroBaseName.Equals("frotz", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("glulxe", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("zork", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Frotz / Glulxe (Interactive Fiction Z-Machine)";
                pkg.RelativeSdPath = $"switch/frotz/{nroFileName}";
            }
            else if (nroBaseName.Equals("residualvm", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "ResidualVM 3D Adventures Engine";
                pkg.RelativeSdPath = $"switch/residualvm/{nroFileName}";
            }
            else if (nroBaseName.Equals("scummvm", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "ScummVM (Classic Adventure Engine)";
                pkg.RelativeSdPath = $"switch/scummvm/{nroFileName}";
            }

            // 5. Standalone Emulators
            else if (nroBaseName.Equals("dosbox", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("dosbox-staging", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("dosbox-pure", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "DOSBox-Staging (MS-DOS Emulator)";
                pkg.RelativeSdPath = $"switch/dosbox/{nroFileName}";
            }
            else if (nroBaseName.Equals("duckstation", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("pcsx-rearmed", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("psx", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "DuckStation / PCSX (PlayStation 1 Emulator)";
                pkg.RelativeSdPath = $"switch/duckstation/{nroFileName}";
            }
            else if (nroBaseName.Equals("mupen64plus", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("mupen64plus-next", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("n64", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "mupen64plus-next (Nintendo 64 Emulator)";
                pkg.RelativeSdPath = $"switch/mupen64/{nroFileName}";
            }
            else if (nroBaseName.Equals("kronos", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("yabause", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("saturn", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Kronos / Yabause (Sega Saturn Emulator)";
                pkg.RelativeSdPath = $"switch/kronos/{nroFileName}";
            }
            else if (nroBaseName.Equals("fake-08", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("pico8", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Fake-08 (PICO-8 Fantasy Console)";
                pkg.RelativeSdPath = $"switch/fake-08/{nroFileName}";
            }
            else if (nroBaseName.Equals("tic80", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "TIC-80 (Tiny Computer Engine)";
                pkg.RelativeSdPath = $"switch/tic80/{nroFileName}";
            }
            else if (nroBaseName.Equals("fuse", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("zxspectrum", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Fuse (ZX Spectrum Emulator)";
                pkg.RelativeSdPath = $"switch/fuse/{nroFileName}";
            }
            else if (nroBaseName.Equals("vice", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("puae", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("c64", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("amiga", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Vice / PUAE (Commodore 64 and Amiga Emulator)";
                pkg.RelativeSdPath = $"switch/vice/{nroFileName}";
            }
            else if (nroBaseName.Equals("snes9x", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Snes9x (Super Nintendo Emulator)";
                pkg.RelativeSdPath = $"switch/snes9x/{nroFileName}";
            }
            else if (nroBaseName.Equals("mesen", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("fceumm", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("nes", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Mesen (NES / Famicom Emulator)";
                pkg.RelativeSdPath = $"switch/mesen/{nroFileName}";
            }
            else if (nroBaseName.Equals("genesis_plus_gx", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("picodrive", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("megadrive", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Genesis Plus GX (Sega Mega Drive / 32X / CD)";
                pkg.RelativeSdPath = $"switch/genesis/{nroFileName}";
            }
            else if (nroBaseName.Equals("fbneo", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("fba", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("neogeo", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "FinalBurn Neo (Arcade and Neo-Geo Emulator)";
                pkg.RelativeSdPath = $"switch/fbneo/{nroFileName}";
            }
            else if (nroBaseName.Equals("ppsspp", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("ppsspp_standalone", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "PPSSPP (Sony PlayStation Portable Emulator)";
                pkg.RelativeSdPath = $"switch/ppsspp/{nroFileName}";
            }
            else if (nroBaseName.Equals("mgba", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "mGBA (Game Boy Advance Emulator)";
                pkg.RelativeSdPath = $"switch/mgba/{nroFileName}";
            }
            else if (nroBaseName.Equals("melonds", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "melonDS (Nintendo DS Emulator)";
                pkg.RelativeSdPath = $"switch/melonds/{nroFileName}";
            }
            else if (nroBaseName.Equals("flycast", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Flycast (Sega Dreamcast Emulator)";
                pkg.RelativeSdPath = $"switch/flycast/{nroFileName}";
            }
            else if (nroBaseName.Equals("retroarch", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("retroarch_switch", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "RetroArch (Multi-System Emulator Frontend)";
                pkg.RelativeSdPath = $"retroarch/{nroFileName}";
            }

            // 6. System Tools, Overlays & Media
            else if (nroBaseName.Equals("dbi", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "DBI (Database Installer / MTP / USB Backend)";
                pkg.RelativeSdPath = $"switch/dbi/{nroFileName}";
            }
            else if (nroBaseName.Equals("tinfoil", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("tinwoo", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("awoo", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Tinfoil / TinWoo / Awoo-Installer";
                pkg.RelativeSdPath = $"switch/tinfoil/{nroFileName}";
            }
            else if (nroBaseName.Equals("goldleaf", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Goldleaf (NSP Installer & File Manager)";
                pkg.RelativeSdPath = $"switch/goldleaf/{nroFileName}";
            }
            else if (nroBaseName.Equals("daybreak", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Daybreak (System Firmware Updater)";
                pkg.RelativeSdPath = $"switch/daybreak/{nroFileName}";
            }
            else if (nroBaseName.Equals("nx-shell", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "NX-Shell (Advanced File Explorer)";
                pkg.RelativeSdPath = $"switch/nx-shell/{nroFileName}";
            }
            else if (nroBaseName.Equals("jksv", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("checkpoint", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "JKSV и Checkpoint (Save Data Managers)";
                pkg.RelativeSdPath = $"switch/JKSV/{nroFileName}";
            }
            else if (nroBaseName.Equals("edizon", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("edizon-se", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("breeze", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "EdiZon-SE / Breeze (Memory and Cheat Engine)";
                pkg.RelativeSdPath = $"switch/EdiZon/{nroFileName}";
            }
            else if (nroBaseName.Equals("nxmp", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("pplay", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "NXMP (Hardware Accelerated Media Player)";
                pkg.RelativeSdPath = $"switch/nxmp/{nroFileName}";
            }
            else if (nroBaseName.Equals("moonlight", StringComparison.OrdinalIgnoreCase) || nroBaseName.Equals("moonlight-switch", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Moonlight (PC Game Streaming Client)";
                pkg.RelativeSdPath = $"switch/moonlight/{nroFileName}";
            }
            else if (nroBaseName.Equals("chiaki", StringComparison.OrdinalIgnoreCase))
            {
                pkg.Name = "Chiaki (PlayStation Remote Play Client)";
                pkg.RelativeSdPath = $"switch/chiaki/{nroFileName}";
            }
            else if (rootDirName.Length > 2 && !rootDirName.Equals("switch", StringComparison.OrdinalIgnoreCase) && !rootDirName.Equals("Homebrew", StringComparison.OrdinalIgnoreCase))
            {
                if (pkg.Name.Equals("app", StringComparison.OrdinalIgnoreCase) || pkg.Name.Equals("main", StringComparison.OrdinalIgnoreCase))
                {
                    pkg.Name = rootDirName;
                }
            }

            if (!string.IsNullOrEmpty(cleanGame) && 
                (pkg.Name.Equals("app", StringComparison.OrdinalIgnoreCase) || 
                 pkg.Name.Equals("main", StringComparison.OrdinalIgnoreCase) || 
                 pkg.Name.Equals("Spaghettify", StringComparison.OrdinalIgnoreCase) || 
                 pkg.Name.Equals("thextech", StringComparison.OrdinalIgnoreCase) || 
                 pkg.Name.Equals("drmario", StringComparison.OrdinalIgnoreCase) || 
                 pkg.Name.Equals("smbr", StringComparison.OrdinalIgnoreCase) || 
                 pkg.Name.Equals("gizmoduck", StringComparison.OrdinalIgnoreCase) || 
                 pkg.Name.Equals(nroBaseName, StringComparison.OrdinalIgnoreCase)))
            {
                pkg.Name = cleanGame;
            }

            // 7. Гарантируем валидный TitleID
            if (string.IsNullOrWhiteSpace(pkg.TitleId) || pkg.TitleId == "0000000000000000" || pkg.TitleId == "0500000000001000" || pkg.TitleId.Length != 16)
            {
                string allSources = string.Join(" ", pkg.InputFiles) + " " + primaryNro + " " + rootDir + " " + (companionNsp ?? "") + " " + (pkg.OutputFileName ?? "");
                var matchTid = System.Text.RegularExpressions.Regex.Match(allSources, @"(?<![0-9A-Fa-f])(0[15][0-9A-Fa-f]{14})(?![0-9A-Fa-f])");
                if (matchTid.Success)
                {
                    pkg.TitleId = matchTid.Groups[1].Value.ToUpperInvariant();
                }
                else
                {
                    pkg.TitleId = GenerateHomebrewTitleId(pkg.Name);
                }
            }

            // 8. Создаем иконку по умолчанию, если отсутствует
            if (pkg.IconBytes == null || pkg.IconBytes.Length == 0)
            {
                pkg.IconBytes = GenerateDefaultHomebrewIcon(pkg.Name);
            }

            return pkg;
        }

        private async Task<HomebrewPackageInfo?> TryParseHomebrewForwarderNspAsync(string nspPath, string? parentDir = null)
        {
            if (!File.Exists(nspPath)) return null;

            string dirToScan = parentDir ?? Path.GetDirectoryName(nspPath) ?? "";

            var pkg = new HomebrewPackageInfo
            {
                Name = Path.GetFileNameWithoutExtension(nspPath),
                PrimaryNroPath = nspPath,
                RootDir = dirToScan
            };
            pkg.InputFiles.Add(nspPath);

            var (relOutputName, cleanGame) = ResolveReleaseNaming(dirToScan);
            if (string.IsNullOrEmpty(relOutputName))
            {
                var rel2 = ResolveReleaseNaming(nspPath);
                relOutputName = rel2.OutputFileName;
                if (string.IsNullOrEmpty(cleanGame)) cleanGame = rel2.CleanGameName;
            }
            if (!string.IsNullOrEmpty(relOutputName))
            {
                pkg.OutputFileName = relOutputName;
            }
            if (!string.IsNullOrEmpty(cleanGame))
            {
                pkg.Name = cleanGame;
            }

            if (!string.IsNullOrEmpty(dirToScan) && Directory.Exists(dirToScan))
            {
                // Добавляем все верхнеуровневые директории данных игры (atmosphere, Add-ons, switch, assets, etc.)
                try
                {
                    foreach (var d in Directory.GetDirectories(dirToScan))
                    {
                        if (!pkg.InputFiles.Contains(d, StringComparer.OrdinalIgnoreCase))
                        {
                            pkg.InputFiles.Add(d);
                        }
                    }
                }
                catch { }
                // Ищем любые папки romfs (включая atmosphere/contents/.../romfs и моды)
                var romfsDirs = Directory.GetDirectories(dirToScan, "romfs", SearchOption.AllDirectories);
                if (romfsDirs.Length > 0)
                {
                    pkg.RomFsDir = romfsDirs[0];
                    foreach (var r in romfsDirs)
                    {
                        if (!pkg.InputFiles.Contains(r, StringComparer.OrdinalIgnoreCase))
                        {
                            pkg.InputFiles.Add(r);
                        }
                    }
                }

                // Ищем любые папки exefs (включая atmosphere/contents/.../exefs и моды)
                var exefsDirs = Directory.GetDirectories(dirToScan, "exefs", SearchOption.AllDirectories);
                if (exefsDirs.Length > 0)
                {
                    pkg.ExeFsDir = exefsDirs[0];
                    foreach (var e in exefsDirs)
                    {
                        if (!pkg.InputFiles.Contains(e, StringComparer.OrdinalIgnoreCase))
                        {
                            pkg.InputFiles.Add(e);
                        }
                    }
                }

                // Ищем сопутствующие файлы данных (.rpf, .mpq, .wad, .pck, .o2r, etc.)
                var excludedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    ".nsp", ".nsz", ".xci", ".xcz", ".tar", ".gz",
                    ".tmp", ".bak", ".ds_store"
                };

                try
                {
                    var allDataFiles = Directory.GetFiles(dirToScan, "*.*", SearchOption.AllDirectories);
                    foreach (var f in allDataFiles)
                    {
                        if (f.Equals(nspPath, StringComparison.OrdinalIgnoreCase)) continue;
                        if (pkg.RomFsDir != null && f.StartsWith(pkg.RomFsDir, StringComparison.OrdinalIgnoreCase)) continue;
                        if (pkg.ExeFsDir != null && f.StartsWith(pkg.ExeFsDir, StringComparison.OrdinalIgnoreCase)) continue;

                        string ext = Path.GetExtension(f);
                        string fileName = Path.GetFileName(f);

                        if (excludedExtensions.Contains(ext)) continue;
                        if (fileName.StartsWith("ReadMe", StringComparison.OrdinalIgnoreCase) || 
                            fileName.StartsWith("Changelog", StringComparison.OrdinalIgnoreCase) || 
                            fileName.Equals("In-Game Cheats.txt", StringComparison.OrdinalIgnoreCase) ||
                            ext.Equals(".odt", StringComparison.OrdinalIgnoreCase) ||
                            ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (!pkg.InputFiles.Contains(f, StringComparer.OrdinalIgnoreCase))
                        {
                            pkg.InputFiles.Add(f);
                        }
                    }
                }
                catch { }
            }

            await ExtractCompanionNspMetadataAsync(pkg, nspPath);

            if (pkg.IconBytes == null || pkg.IconBytes.Length == 0)
            {
                pkg.IconBytes = GenerateDefaultHomebrewIcon(pkg.Name);
            }

            return pkg;
        }

        /// <summary>
        /// Извлечение метаданных (TitleID, NACP, Icon, nextNroPath) из Forwarder NSP через LibHac
        /// </summary>
        private void ExtractCompanionNspMetadataSync(HomebrewPackageInfo pkg, string nspPath)
        {
            if (!File.Exists(nspPath)) return;

            try
            {
                using var fs = new FileStream(nspPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var pfs = new PartitionFileSystem(fs.AsStorage());

                foreach (var entry in pfs.EnumerateEntries("/", "*.nca"))
                {
                    try
                    {
                        using var ncaFileRef = new UniqueRef<IFile>();
                        using var entryPath = new LibHac.Fs.Path();
                        entryPath.Initialize(new U8Span(Encoding.UTF8.GetBytes(entry.FullPath))).ThrowIfFailure();
                        if (pfs.OpenFile(ref ncaFileRef.Ref, in entryPath, OpenMode.Read).IsSuccess())
                        {
                            var ncaStorage = ncaFileRef.Release().AsStorage();
                            var nca = new Nca(_keysService.CurrentKeyset, ncaStorage);

                            if (nca.Header.ContentType == NcaContentType.Control)
                            {
                                IFileSystem? romfs = null;
                                try { romfs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None); } catch { }
                                if (romfs == null)
                                    try { romfs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.IgnoreOnInvalid); } catch { }

                                if (romfs != null)
                                {
                                    // 1. NACP
                                    using var nacpFileRef = new UniqueRef<IFile>();
                                    using var nacpPath = new LibHac.Fs.Path();
                                    nacpPath.Initialize(new U8Span(Encoding.UTF8.GetBytes("/control.nacp"))).ThrowIfFailure();
                                    if (romfs.OpenFile(ref nacpFileRef.Ref, in nacpPath, OpenMode.Read).IsSuccess())
                                    {
                                        using var nacpFile = nacpFileRef.Release();
                                        nacpFile.GetSize(out long nacpSize).ThrowIfFailure();
                                        byte[] nacpData = new byte[(int)nacpSize];
                                        nacpFile.AsStream().Read(nacpData, 0, nacpData.Length);
                                        ParseNacpData(pkg, nacpData);
                                    }

                                    // 2. Icon
                                    foreach (var iconEntry in romfs.EnumerateEntries("/", "icon_*.dat"))
                                    {
                                        using var iconFileRef = new UniqueRef<IFile>();
                                        using var iconPath = new LibHac.Fs.Path();
                                        iconPath.Initialize(new U8Span(Encoding.UTF8.GetBytes(iconEntry.FullPath))).ThrowIfFailure();
                                        if (romfs.OpenFile(ref iconFileRef.Ref, in iconPath, OpenMode.Read).IsSuccess())
                                        {
                                            using var iconFile = iconFileRef.Release();
                                            iconFile.GetSize(out long iconSize).ThrowIfFailure();
                                            byte[] iconBytes = new byte[(int)iconSize];
                                            iconFile.AsStream().Read(iconBytes, 0, iconBytes.Length);
                                            if (iconBytes.Length > 0)
                                            {
                                                pkg.IconBytes = iconBytes;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            else if (nca.Header.ContentType == NcaContentType.Program)
                            {
                                if (string.IsNullOrWhiteSpace(pkg.TitleId) || pkg.TitleId == "0500000000001000")
                                {
                                    pkg.TitleId = nca.Header.TitleId.ToString("X16");
                                }

                                // Проверяем RomFS в Program NCA на наличие nextNroPath
                                if (nca.CanOpenSection(1))
                                {
                                    IFileSystem? progRomfs = null;
                                    try { progRomfs = nca.OpenFileSystem(1, IntegrityCheckLevel.None); } catch { }
                                    if (progRomfs == null)
                                        try { progRomfs = nca.OpenFileSystem(1, IntegrityCheckLevel.IgnoreOnInvalid); } catch { }

                                    if (progRomfs != null)
                                    {
                                        using var nextFileRef = new UniqueRef<IFile>();
                                        using var nextPath = new LibHac.Fs.Path();
                                        nextPath.Initialize(new U8Span(Encoding.UTF8.GetBytes("/nextNroPath"))).ThrowIfFailure();
                                        if (progRomfs.OpenFile(ref nextFileRef.Ref, in nextPath, OpenMode.Read).IsSuccess())
                                        {
                                            using var nextFile = nextFileRef.Release();
                                            using var reader = new StreamReader(nextFile.AsStream(), Encoding.UTF8);
                                            string fwdPath = reader.ReadToEnd().Trim();
                                            if (!string.IsNullOrWhiteSpace(fwdPath))
                                            {
                                                pkg.RelativeSdPath = fwdPath.Replace("sdmc:/", "").TrimStart('/');
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                // TitleID из имени файла (если NACP/Program не определили)
                if (string.IsNullOrWhiteSpace(pkg.TitleId) || pkg.TitleId == "0500000000001000")
                {
                    var matchTid = System.Text.RegularExpressions.Regex.Match(Path.GetFileName(nspPath), @"\[([0-9A-Fa-f]{16})\]");
                    if (matchTid.Success)
                    {
                        pkg.TitleId = matchTid.Groups[1].Value.ToUpperInvariant();
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[HomebrewService] Ошибка парсинга сопутствующего NSP {nspPath}: {ex.Message}", LogLevel.Warning);
            }
        }

        private async Task ExtractCompanionNspMetadataAsync(HomebrewPackageInfo pkg, string nspPath)
        {
            await Task.Run(() => ExtractCompanionNspMetadataSync(pkg, nspPath));
        }

        /// <summary>
        /// Извлечение оригинального ExeFS и RomFS из сопутствующего Forwarder NSP
        /// </summary>
        private bool TryExtractForwarderExeFsAndRomfs(string companionNsp, string exefsDir, string romfsDir, out string logMessage)
        {
            logMessage = string.Empty;
            if (!File.Exists(companionNsp)) return false;

            bool exefsExtracted = false;
            try
            {
                using var nspFs = new FileStream(companionNsp, FileMode.Open, FileAccess.Read, FileShare.Read);
                var pfs = new PartitionFileSystem(nspFs.AsStorage());

                foreach (var entry in pfs.EnumerateEntries("/", "*.nca"))
                {
                    try
                    {
                        using var ncaFileRef = new UniqueRef<IFile>();
                        using var entryPath = new LibHac.Fs.Path();
                        entryPath.Initialize(new U8Span(Encoding.UTF8.GetBytes(entry.FullPath))).ThrowIfFailure();
                        if (pfs.OpenFile(ref ncaFileRef.Ref, in entryPath, OpenMode.Read).IsSuccess())
                        {
                            var ncaStorage = ncaFileRef.Release().AsStorage();
                            var nca = new Nca(_keysService.CurrentKeyset, ncaStorage);
                            if (nca.Header.ContentType == NcaContentType.Program)
                            {
                                // 1. Извлекаем ExeFS (Section 0)
                                if (nca.CanOpenSection(0))
                                {
                                    IFileSystem? exefs = null;
                                    try { exefs = nca.OpenFileSystem(0, IntegrityCheckLevel.None); } catch { }
                                    if (exefs == null)
                                        try { exefs = nca.OpenFileSystem(0, IntegrityCheckLevel.IgnoreOnInvalid); } catch { }

                                    if (exefs != null)
                                    {
                                        Directory.CreateDirectory(exefsDir);
                                        foreach (var exefsEntry in exefs.EnumerateEntries("/", "*"))
                                        {
                                            if (exefsEntry.Type == DirectoryEntryType.File)
                                            {
                                                string destFile = Path.Combine(exefsDir, exefsEntry.Name.TrimStart('/'));
                                                using var srcFileRef = new UniqueRef<IFile>();
                                                using var srcPath = new LibHac.Fs.Path();
                                                srcPath.Initialize(new U8Span(Encoding.UTF8.GetBytes(exefsEntry.FullPath))).ThrowIfFailure();
                                                if (exefs.OpenFile(ref srcFileRef.Ref, in srcPath, OpenMode.Read).IsSuccess())
                                                {
                                                    using var srcFile = srcFileRef.Release();
                                                    using var outStream = new FileStream(destFile, FileMode.Create, FileAccess.Write);
                                                    srcFile.AsStream().CopyTo(outStream);
                                                }
                                            }
                                        }

                                        if (File.Exists(Path.Combine(exefsDir, "main")) && File.Exists(Path.Combine(exefsDir, "main.npdm")))
                                        {
                                            exefsExtracted = true;
                                            logMessage = $"[ExeFS] Успешно извлечен нативный ExeFS из форвардера: {Path.GetFileName(companionNsp)}";
                                        }
                                    }
                                }

                                // 2. Если в форвардере уже есть RomFS (например, nextNroPath, nextArgv), извлекаем их
                                if (nca.CanOpenSection(1))
                                {
                                    IFileSystem? fwdRomfs = null;
                                    try { fwdRomfs = nca.OpenFileSystem(1, IntegrityCheckLevel.None); } catch { }
                                    if (fwdRomfs == null)
                                        try { fwdRomfs = nca.OpenFileSystem(1, IntegrityCheckLevel.IgnoreOnInvalid); } catch { }

                                    if (fwdRomfs != null)
                                    {
                                        Directory.CreateDirectory(romfsDir);
                                        foreach (var rEntry in fwdRomfs.EnumerateEntries("/", "*"))
                                        {
                                            if (rEntry.Type == DirectoryEntryType.File)
                                            {
                                                string destFile = Path.Combine(romfsDir, rEntry.Name.TrimStart('/'));
                                                if (!File.Exists(destFile))
                                                {
                                                    using var srcFileRef = new UniqueRef<IFile>();
                                                    using var srcPath = new LibHac.Fs.Path();
                                                    srcPath.Initialize(new U8Span(Encoding.UTF8.GetBytes(rEntry.FullPath))).ThrowIfFailure();
                                                    if (fwdRomfs.OpenFile(ref srcFileRef.Ref, in srcPath, OpenMode.Read).IsSuccess())
                                                    {
                                                        using var srcFile = srcFileRef.Release();
                                                        using var outStream = new FileStream(destFile, FileMode.Create, FileAccess.Write);
                                                        srcFile.AsStream().CopyTo(outStream);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                break;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                logMessage = $"[ExeFS] Предупреждение при распаковке форвардера: {ex.Message}";
            }

            return exefsExtracted;
        }

        /// <summary>
        /// Извлечение метаданных ASET из NRO файла (NACP, Icon, RomFS)
        /// </summary>
        private void ParseNroAset(HomebrewPackageInfo pkg, string nroPath)
        {
            using var fs = new FileStream(nroPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);

            if (fs.Length < 0x80) return;

            // Проверяем заголовок NRO0 (смещение 0x10)
            fs.Seek(0x10, SeekOrigin.Begin);
            uint magic = br.ReadUInt32();
            if (magic != 0x304F524E) // 'NRO0'
                return;

            fs.Seek(0x18, SeekOrigin.Begin);
            uint nroSize = br.ReadUInt32();

            if (nroSize >= fs.Length || nroSize == 0) return;

            // Читаем ASET chunk в конце NRO
            fs.Seek(nroSize, SeekOrigin.Begin);
            if (fs.Length - nroSize < 0x38) return;

            uint asetMagic = br.ReadUInt32();
            if (asetMagic != 0x54455341) // 'ASET'
                return;

            uint asetVersion = br.ReadUInt32();
            long iconOffset = br.ReadInt64();
            long iconSize = br.ReadInt64();
            long nacpOffset = br.ReadInt64();
            long nacpSize = br.ReadInt64();
            long romfsOffset = br.ReadInt64();
            long romfsSize = br.ReadInt64();

            long asetBase = nroSize;

            // Чтение NACP
            if (nacpSize > 0 && nacpOffset > 0 && (asetBase + nacpOffset + nacpSize) <= fs.Length)
            {
                fs.Seek(asetBase + nacpOffset, SeekOrigin.Begin);
                byte[] nacpData = br.ReadBytes((int)nacpSize);
                ParseNacpData(pkg, nacpData);
            }

            // Чтение Иконки (JPEG / PNG)
            if (iconSize > 0 && iconOffset > 0 && (asetBase + iconOffset + iconSize) <= fs.Length)
            {
                fs.Seek(asetBase + iconOffset, SeekOrigin.Begin);
                pkg.IconBytes = br.ReadBytes((int)iconSize);
            }
        }

        private void ParseNacpData(HomebrewPackageInfo pkg, byte[] nacp)
        {
            if (nacp.Length < 0x3000) return;

            // Ищем локализованное имя: Русский (слот 11), English (слот 0), или первое непустое
            string? foundName = null;
            string? foundAuthor = null;

            int[] checkOrder = { 11, 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 12, 13, 14, 15 };
            foreach (int slot in checkOrder)
            {
                int offset = slot * 0x300;
                if (offset + 0x300 > nacp.Length) continue;

                string title = ReadNullTerminatedUtf8(nacp, offset, 0x200);
                string author = ReadNullTerminatedUtf8(nacp, offset + 0x200, 0x100);

                if (!string.IsNullOrWhiteSpace(title))
                {
                    if (foundName == null || slot == 11) foundName = title;
                    if (foundAuthor == null || slot == 11) foundAuthor = author;
                    if (slot == 11) break;
                }
            }

            if (!string.IsNullOrWhiteSpace(foundName)) pkg.Name = foundName;
            if (!string.IsNullOrWhiteSpace(foundAuthor)) pkg.Author = foundAuthor;

            // Версия из заголовка NACP (offset 0x3060)
            if (nacp.Length >= 0x3070)
            {
                string ver = ReadNullTerminatedUtf8(nacp, 0x3060, 0x10);
                if (!string.IsNullOrWhiteSpace(ver)) pkg.Version = ver;
            }

            // Title ID из заголовка NACP (offset 0x3078)
            if (nacp.Length >= 0x3080)
            {
                ulong tid = BitConverter.ToUInt64(nacp, 0x3078);
                if (tid != 0)
                {
                    pkg.TitleId = tid.ToString("X16");
                }
            }
        }

        private string ReadNullTerminatedUtf8(byte[] data, int offset, int maxLen)
        {
            int len = 0;
            while (len < maxLen && (offset + len) < data.Length && data[offset + len] != 0)
            {
                len++;
            }
            if (len == 0) return string.Empty;
            return Encoding.UTF8.GetString(data, offset, len).Trim();
        }

        /// <summary>
        /// Генерация валидного Homebrew TitleID (05xxxxxxxxxxxx00)
        /// </summary>
        public string GenerateHomebrewTitleId(string name)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(name.Trim().ToLowerInvariant()));
            
            // Формируем 16 hex символов: префикс 05 + 12 символов хэша + 00
            ulong idVal = 0x0500000000000000UL;
            ulong middle = BitConverter.ToUInt64(hash, 0) & 0x00FFFFFFFFFF0000UL;
            idVal |= middle;
            idVal &= ~0xFFUL; // Гарантируем окончание на 00

            string hex = idVal.ToString("X16").ToUpperInvariant();
            if (hex.Length != 16 || hex == "0000000000000000")
            {
                hex = "0500000000001000";
            }
            return hex;
        }

        /// <summary>
        /// Генерация красивой стилизованной Cyber Dark иконки для Homebrew
        /// </summary>
        public byte[] GenerateDefaultHomebrewIcon(string title)
        {
            try
            {
                using var bmp = new Bitmap(256, 256);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                    // Фон
                    using var bgBrush = new SolidBrush(Color.FromArgb(15, 23, 42)); // Slate 900
                    g.FillRectangle(bgBrush, 0, 0, 256, 256);

                    // Рамка
                    using var borderPen = new Pen(Color.FromArgb(14, 165, 233), 4); // Cyan 500
                    g.DrawRectangle(borderPen, 2, 2, 252, 252);

                    // Бейдж HOMEBREW
                    using var badgeBrush = new SolidBrush(Color.FromArgb(14, 165, 233));
                    g.FillRectangle(badgeBrush, 20, 20, 216, 36);
                    using var badgeFont = new Font("Arial", 12, FontStyle.Bold);
                    using var badgeTextBrush = new SolidBrush(Color.White);
                    g.DrawString("HOMEBREW APP", badgeFont, badgeTextBrush, 45, 28);

                    // Инициалы / Символ
                    string initial = title.Length > 0 ? title.Substring(0, Math.Min(3, title.Length)).ToUpperInvariant() : "HB";
                    using var titleFont = new Font("Arial", 28, FontStyle.Bold);
                    using var textBrush = new SolidBrush(Color.FromArgb(248, 250, 252));
                    
                    var sf = new StringFormat
                    {
                        Alignment = StringAlignment.Center,
                        LineAlignment = StringAlignment.Center
                    };
                    g.DrawString(initial, titleFont, textBrush, new RectangleF(10, 70, 236, 80), sf);

                    // Полное имя внизу
                    using var subFont = new Font("Arial", 11, FontStyle.Regular);
                    using var subBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
                    string displayName = title.Length > 22 ? title.Substring(0, 20) + "..." : title;
                    g.DrawString(displayName, subFont, subBrush, new RectangleF(10, 160, 236, 60), sf);
                }

                using var ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Jpeg);
                return ms.ToArray();
            }
            catch
            {
                return new byte[0];
            }
        }

        /// <summary>
        /// Полноценная сборка Homebrew NSP / Forwarder с гарантией работоспособности
        /// </summary>
        public async Task BuildHomebrewAsync(ProcessingTask task, CancellationToken ct)
        {
            string rootDrive = Path.GetPathRoot(AppDomain.CurrentDomain.BaseDirectory) ?? "C:\\";
            string tempDir = Path.Combine(rootDrive, "Temp", "StormHB", Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(tempDir);
            TempCleanupService.RegisterActiveTempDirectory(tempDir);

            try
            {
                App.RunOnUI(() =>
                {
                    task.IsRunning = true;
                    task.Status = "Подготовка...";
                    task.Progress = 5;
                    task.LogDetails = $"[Homebrew] Запуск сборки: {task.OutputFileName}\n";
                });

                string keysFile = App.Settings.Current.KeysPath;
                if (string.IsNullOrEmpty(keysFile) || !File.Exists(keysFile))
                {
                    keysFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "prod.keys");
                }

                if (!File.Exists(keysFile))
                {
                    throw new Exception("Не найден файл ключей prod.keys. Укажите его в Настройках.");
                }

                if (!File.Exists(_hacpackExe))
                {
                    throw new Exception($"Не найден инструмент сборки: {_hacpackExe}");
                }

                string titleId = (!string.IsNullOrWhiteSpace(task.GroupId) && task.GroupId.Length == 16)
                    ? task.GroupId.ToUpperInvariant()
                    : GenerateHomebrewTitleId(task.OutputFileName);

                App.RunOnUI(() =>
                {
                    task.LogDetails += $"TitleID: {titleId}\n";
                    task.LogDetails += $"Целевой формат: {task.TargetFormat}\n";
                    task.Progress = 15;
                    task.Status = "Генерация ExeFS & RomFS...";
                });

                string exefsDir = Path.Combine(tempDir, "exefs");
                string romfsDir = Path.Combine(tempDir, "romfs");
                string controlRomfsDir = Path.Combine(tempDir, "control_romfs");
                string outProgramDir = Path.Combine(tempDir, "out_prog");
                string outControlDir = Path.Combine(tempDir, "out_ctrl");
                string outMetaDir = Path.Combine(tempDir, "out_meta");
                string allNcasDir = Path.Combine(tempDir, "all_ncas");

                Directory.CreateDirectory(exefsDir);
                Directory.CreateDirectory(romfsDir);
                Directory.CreateDirectory(controlRomfsDir);
                Directory.CreateDirectory(outProgramDir);
                Directory.CreateDirectory(outControlDir);
                Directory.CreateDirectory(outMetaDir);
                Directory.CreateDirectory(allNcasDir);

                // 0. Автоматическая распаковка сопутствующих архивов (.7z, .zip, .rar)
                string archivesUnpackDir = Path.Combine(tempDir, "unpacked_archives");
                var extractedArchiveDirs = new List<string>();
                var extractedArchiveFiles = new List<string>();

                var archivesToExtract = new List<string>();
                foreach (var f in task.InputFiles)
                {
                    if (File.Exists(f))
                    {
                        string ext = Path.GetExtension(f);
                        if (ext.Equals(".7z", StringComparison.OrdinalIgnoreCase) ||
                            ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                            ext.Equals(".rar", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!archivesToExtract.Contains(f, StringComparer.OrdinalIgnoreCase))
                                archivesToExtract.Add(f);
                        }
                    }
                    else if (Directory.Exists(f))
                    {
                        try
                        {
                            foreach (var arch in Directory.GetFiles(f, "*.*", SearchOption.AllDirectories)
                                .Where(x => x.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) ||
                                            x.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                                            x.EndsWith(".rar", StringComparison.OrdinalIgnoreCase)))
                            {
                                if (!archivesToExtract.Contains(arch, StringComparer.OrdinalIgnoreCase))
                                    archivesToExtract.Add(arch);
                            }
                        }
                        catch { }
                    }
                }

                foreach (var f in task.InputFiles.ToList())
                {
                    try
                    {
                        string? scanDir = Directory.Exists(f) ? f : Path.GetDirectoryName(f);
                        if (!string.IsNullOrEmpty(scanDir) && Directory.Exists(scanDir))
                        {
                            foreach (var arch in Directory.GetFiles(scanDir, "*.*", SearchOption.TopDirectoryOnly))
                            {
                                string ext = Path.GetExtension(arch);
                                if (ext.Equals(".7z", StringComparison.OrdinalIgnoreCase) ||
                                    ext.Equals(".zip", StringComparison.OrdinalIgnoreCase) ||
                                    ext.Equals(".rar", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (!archivesToExtract.Contains(arch, StringComparer.OrdinalIgnoreCase))
                                        archivesToExtract.Add(arch);
                                }
                            }
                        }
                    }
                    catch { }
                }

                string sevenZipExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "7z.exe");
                if (!File.Exists(sevenZipExe))
                {
                    sevenZipExe = Path.Combine(Directory.GetCurrentDirectory(), "tools", "7z.exe");
                }

                if (archivesToExtract.Count > 0 && File.Exists(sevenZipExe))
                {
                    Directory.CreateDirectory(archivesUnpackDir);
                    foreach (var arch in archivesToExtract)
                    {
                        ct.ThrowIfCancellationRequested();
                        string archName = Path.GetFileName(arch);
                        string archTargetDir = Path.Combine(archivesUnpackDir, Path.GetFileNameWithoutExtension(arch));
                        Directory.CreateDirectory(archTargetDir);

                        App.RunOnUI(() =>
                        {
                            task.LogDetails += $"[Архивы] Распаковка сопутствующего архива: {archName}...\n";
                        });

                        int exitCode = await ExternalProcessRunner.RunAsync(sevenZipExe, $"x \"{arch}\" -o\"{archTargetDir}\" -y", tempDir, task, ct);
                        if (exitCode == 0 && Directory.Exists(archTargetDir))
                        {
                            extractedArchiveDirs.Add(archTargetDir);
                            foreach (var subDir in Directory.GetDirectories(archTargetDir, "*", SearchOption.AllDirectories))
                            {
                                extractedArchiveDirs.Add(subDir);
                            }
                            foreach (var subFile in Directory.GetFiles(archTargetDir, "*", SearchOption.AllDirectories))
                            {
                                extractedArchiveFiles.Add(subFile);
                            }

                            var foundNros = Directory.GetFiles(archTargetDir, "*.nro", SearchOption.AllDirectories);
                            foreach (var fn in foundNros)
                            {
                                if (!task.InputFiles.Contains(fn, StringComparer.OrdinalIgnoreCase))
                                {
                                    task.InputFiles.Add(fn);
                                    App.RunOnUI(() =>
                                    {
                                        task.LogDetails += $"[Архивы] Обнаружен исполняемый файл: {Path.GetFileName(fn)}\n";
                                    });
                                }
                            }

                            App.RunOnUI(() =>
                            {
                                task.LogDetails += $"✅ Архив {archName} успешно распакован ({extractedArchiveFiles.Count} файлов ресурсов).\n";
                            });
                        }
                        else
                        {
                            App.RunOnUI(() =>
                            {
                                task.LogDetails += $"⚠️ Предупреждение: распаковка {archName} завершилась с кодом {exitCode}.\n";
                            });
                        }
                    }
                }

                // Проверка свободного места на целевом диске
                long requiredSpace = (long)(task.SourceSizeBytes * 2.5);
                string checkPath = !string.IsNullOrEmpty(task.OutputFolder) ? task.OutputFolder : tempDir;
                if (CheckDiskSpace(checkPath, requiredSpace, out long availSpace) && availSpace > 0 && availSpace < requiredSpace)
                {
                    App.RunOnUI(() =>
                    {
                        task.LogDetails += $"[Предупреждение] На диске осталось {availSpace / (1024.0 * 1024 * 1024):F1} ГБ. Рекомендуется не менее {requiredSpace / (1024.0 * 1024 * 1024):F1} ГБ для гарантированной сборки.\n";
                    });
                }


                // 1. Формируем ExeFS
                // Если в задаче есть сопутствующий NSP форвардер, извлекаем из него оригинальный проверенный ExeFS
                string? companionNsp = task.InputFiles.FirstOrDefault(f => File.Exists(f) && (f.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase)));
                bool exefsExtracted = false;

                if (companionNsp != null)
                {
                    exefsExtracted = TryExtractForwarderExeFsAndRomfs(companionNsp, exefsDir, romfsDir, out string extractMsg);
                    if (!string.IsNullOrEmpty(extractMsg))
                    {
                        App.RunOnUI(() =>
                        {
                            task.LogDetails += extractMsg + "\n";
                        });
                    }
                }

                // Если в задаче передан пользовательский или модифицированный ExeFS (например, YNWAMAX IPS Mod), применяем поверх
                var userExeFsDirs = task.InputFiles.Where(f => Directory.Exists(f) && Path.GetFileName(f).Equals("exefs", StringComparison.OrdinalIgnoreCase)).ToList();
                foreach (var uExefs in userExeFsDirs)
                {
                    if (File.Exists(Path.Combine(uExefs, "main.npdm")) || File.Exists(Path.Combine(uExefs, "main")))
                    {
                        CopyDirectory(uExefs, exefsDir);
                        exefsExtracted = true;
                        App.RunOnUI(() =>
                        {
                            task.LogDetails += $"[ExeFS] Применены файлы ExeFS из: {uExefs}\n";
                        });
                    }
                }

                if (!exefsExtracted)
                {
                    GenerateUniversalForwarderExeFs(exefsDir, titleId);
                }

                // 2. Формируем RomFS со ВСЕМИ файлами данных игры (.rpf, .mpq, .wad, .pck, .o2r, .nes, etc.)
                var allRomfsDirs = task.InputFiles
                    .Where(f => Directory.Exists(f) && (Path.GetFileName(f).Equals("romfs", StringComparison.OrdinalIgnoreCase) || f.EndsWith(@"\romfs", StringComparison.OrdinalIgnoreCase) || f.EndsWith("/romfs", StringComparison.OrdinalIgnoreCase)))
                    .OrderBy(f => f.Contains("Add-on", StringComparison.OrdinalIgnoreCase) || f.Contains("Mod", StringComparison.OrdinalIgnoreCase) || f.Contains("Russian", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
                    .ToList();

                if (allRomfsDirs.Count == 0)
                {
                    foreach (var dir in task.InputFiles.Where(f => Directory.Exists(f)))
                    {
                        try
                        {
                            var nested = Directory.GetDirectories(dir, "romfs", SearchOption.AllDirectories);
                            foreach (var n in nested)
                            {
                                if (!allRomfsDirs.Contains(n, StringComparer.OrdinalIgnoreCase))
                                    allRomfsDirs.Add(n);
                            }
                        }
                        catch { }
                    }
                    allRomfsDirs = allRomfsDirs.OrderBy(f => f.Contains("Add-on", StringComparison.OrdinalIgnoreCase) || f.Contains("Mod", StringComparison.OrdinalIgnoreCase) || f.Contains("Russian", StringComparison.OrdinalIgnoreCase) ? 1 : 0).ToList();
                }

                var looseDataFiles = task.InputFiles.Concat(extractedArchiveFiles).Where(f => File.Exists(f) && 
                    !f.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) && 
                    !f.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) && 
                    !f.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) && 
                    !f.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase) &&
                    !f.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) &&
                    !f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                    !f.EndsWith(".rar", StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                string? nroFile = task.InputFiles.Concat(extractedArchiveFiles).FirstOrDefault(f => File.Exists(f) && Path.GetExtension(f).Equals(".nro", StringComparison.OrdinalIgnoreCase));
                bool isForwarder = (nroFile != null) || File.Exists(Path.Combine(romfsDir, "nextNroPath"));
                string effectiveRomfsDir = romfsDir;

                if (isForwarder)
                {
                    // Для HBL Forwarder NSP раздел RomFS должен быть ультра-легковесным (~1 КБ)
                    // и содержать исключительно маршруты запуска (nextNroPath, nextArgv).
                    // Все ресурсы игры, ассеты и loose-файлы деплоятся исключительно на SDMC (sdmc:/...)
                    // чтобы RomFS не переполнял память загрузчика HBL (Userspace PANIC 0x75B).

                    if (nroFile != null)
                    {
                        string nroName = Path.GetFileName(nroFile);
                        string nroBaseName = Path.GetFileNameWithoutExtension(nroFile);

                        // Определяем точный целевой путь форвардера sdmc:/...
                        string nextPath = "";
                        string fwdNextNro = Path.Combine(romfsDir, "nextNroPath");
                        if (File.Exists(fwdNextNro))
                        {
                            nextPath = File.ReadAllText(fwdNextNro).Trim();
                        }

                        if (string.IsNullOrWhiteSpace(nextPath))
                        {
                            if (task.InputFiles.Any(f => f.Contains("devilutionx", StringComparison.OrdinalIgnoreCase)))
                            {
                                nextPath = $"sdmc:/devilutionx-switch/{nroName}";
                            }
                            else if (task.InputFiles.Any(f => f.Contains("drmariomania", StringComparison.OrdinalIgnoreCase)))
                            {
                                nextPath = $"sdmc:/switch/drmariomania_nx/{nroName}";
                            }
                            else if (task.InputFiles.Any(f => f.Contains("gizmoduck", StringComparison.OrdinalIgnoreCase)))
                            {
                                nextPath = $"sdmc:/switch/gizmoduck/{nroName}";
                            }
                            else if (task.InputFiles.Any(f => f.Contains("mk64", StringComparison.OrdinalIgnoreCase) || f.Contains("spaghetti", StringComparison.OrdinalIgnoreCase)))
                            {
                                nextPath = $"sdmc:/switch/mk64_spaghetti/{nroName}";
                            }
                            else if (task.InputFiles.Any(f => f.Contains("smbr", StringComparison.OrdinalIgnoreCase)))
                            {
                                nextPath = $"sdmc:/switch/smbr_nx/{nroName}";
                            }
                            else if (task.InputFiles.Any(f => f.Contains("thextech", StringComparison.OrdinalIgnoreCase)))
                            {
                                nextPath = $"sdmc:/switch/{nroName}";
                            }
                            else
                            {
                                string appFolder = nroBaseName.Replace("-switch", "", StringComparison.OrdinalIgnoreCase).Trim();
                                nextPath = $"sdmc:/switch/{appFolder}/{nroName}";
                            }
                        }

                        string nextArgvContent = $"{nextPath}\0{nextPath}\0";
                        File.WriteAllText(Path.Combine(romfsDir, "nextNroPath"), nextPath);
                        File.WriteAllBytes(Path.Combine(romfsDir, "nextArgv"), Encoding.UTF8.GetBytes(nextArgvContent));

                        App.RunOnUI(() =>
                        {
                            task.LogDetails += $"[RomFS] HBL Forwarder: сконфигурирован целевой NRO путь {nextPath}\n";
                        });
                    }
                }
                else
                {
                    // Нативный Switch порт / монолит (например, Grand Theft Auto V)
                    // Если есть ровно один готовый каталог RomFS без модов и loose файлов - Zero-Copy
                    if (allRomfsDirs.Count == 1 && looseDataFiles.Count == 0)
                    {
                        effectiveRomfsDir = allRomfsDirs[0];
                        App.RunOnUI(() =>
                        {
                            task.LogDetails += $"[RomFS] Zero-Copy: Использование существующего RomFS каталога ({allRomfsDirs[0]})\n";
                        });
                    }
                    else
                    {
                        // Копируем базовый RomFS, а затем накатываем RomFS модов/дополнений поверх
                        foreach (var rDir in allRomfsDirs)
                        {
                            App.RunOnUI(() =>
                            {
                                task.LogDetails += $"[RomFS] Применение каталога RomFS ({Path.GetFileName(Path.GetDirectoryName(rDir)) ?? rDir})...\n";
                            });
                            CopyDirectory(rDir, romfsDir);
                        }

                        // Копируем подкаталоги данных игры (assets, save, android_root, TheXTech, devilutionx-switch, etc.)
                        var gameSubDirs = task.InputFiles.Where(f => Directory.Exists(f) && 
                            !Path.GetFileName(f).Equals("romfs", StringComparison.OrdinalIgnoreCase) && 
                            !Path.GetFileName(f).Equals("exefs", StringComparison.OrdinalIgnoreCase) && 
                            !Path.GetFileName(f).Equals("exefs_patches", StringComparison.OrdinalIgnoreCase) &&
                            !Path.GetFileName(f).Equals("atmosphere", StringComparison.OrdinalIgnoreCase) &&
                            !Path.GetFileName(f).Equals("Add-ons", StringComparison.OrdinalIgnoreCase)).ToList();

                        foreach (var gDir in gameSubDirs)
                        {
                            string dirName = Path.GetFileName(gDir);
                            string destSubDir = Path.Combine(romfsDir, dirName);
                            CopyDirectory(gDir, destSubDir);

                            // Если папка содержит прямые ресурсы (devilutionx-switch, switch/<app>, TheXTech, etc.), дублируем файлы в корень RomFS для мгновенного нахождения игрой
                            try
                            {
                                foreach (var subFile in Directory.GetFiles(gDir, "*.*", SearchOption.AllDirectories))
                                {
                                    string ext = Path.GetExtension(subFile);
                                    if (ext.Equals(".mpq", StringComparison.OrdinalIgnoreCase) ||
                                        ext.Equals(".ini", StringComparison.OrdinalIgnoreCase) ||
                                        ext.Equals(".ttf", StringComparison.OrdinalIgnoreCase) ||
                                        ext.Equals(".o2r", StringComparison.OrdinalIgnoreCase) ||
                                        ext.Equals(".pck", StringComparison.OrdinalIgnoreCase) ||
                                        ext.Equals(".so", StringComparison.OrdinalIgnoreCase))
                                    {
                                        string rootDest = Path.Combine(romfsDir, Path.GetFileName(subFile));
                                        if (!File.Exists(rootDest))
                                        {
                                            CopyFileWithRetry(subFile, rootDest, false);
                                        }
                                    }
                                }
                            }
                            catch { }

                            // Для подпапок assets, save, android_root дублируем в корень RomFS
                            try
                            {
                                foreach (var sAssets in Directory.GetDirectories(gDir, "assets", SearchOption.AllDirectories))
                                {
                                    string rootAssets = Path.Combine(romfsDir, "assets");
                                    CopyDirectory(sAssets, rootAssets);
                                }
                                foreach (var sSave in Directory.GetDirectories(gDir, "save", SearchOption.AllDirectories))
                                {
                                    string rootSave = Path.Combine(romfsDir, "save");
                                    CopyDirectory(sSave, rootSave);
                                }
                                foreach (var sAndroid in Directory.GetDirectories(gDir, "android_root", SearchOption.AllDirectories))
                                {
                                    string rootAndroid = Path.Combine(romfsDir, "android_root");
                                    CopyDirectory(sAndroid, rootAndroid);
                                }
                            }
                            catch { }
                        }

                        // Копируем все loose файлы данных в RomFS
                        int looseCopied = 0;
                        foreach (var file in looseDataFiles)
                        {
                            string fileName = Path.GetFileName(file);
                            string dest = Path.Combine(romfsDir, fileName);
                            if (!File.Exists(dest))
                            {
                                CopyFileWithRetry(file, dest, true);
                                looseCopied++;
                            }
                        }
                        if (looseCopied > 0)
                        {
                            App.RunOnUI(() =>
                            {
                                task.LogDetails += $"[RomFS] Вшито файлов ресурсов: {looseCopied}\n";
                            });
                        }
                    }
                }

                // 3. Формируем Control RomFS (control.nacp + icon)
                string appTitle = !string.IsNullOrWhiteSpace(task.GameName) ? task.GameName : task.OutputFileName;
                string appPublisher = task.CustomMetadata?.Publisher ?? "Homebrew Developer";
                string appVersion = task.CustomMetadata?.Version ?? "1.0.0";

                byte[] nacpData = GenerateNacp(appTitle, appPublisher, appVersion, titleId);
                File.WriteAllBytes(Path.Combine(controlRomfsDir, "control.nacp"), nacpData);

                byte[]? iconBytes = task.CustomMetadata?.CustomIconBytes ?? task.CustomMetadata?.OriginalIconBytes;
                if (iconBytes == null || iconBytes.Length == 0)
                {
                    try
                    {
                        string cachedIcon = Path.Combine(HistoryService.GetIconsDirectory(), $"{titleId}.png");
                        if (File.Exists(cachedIcon)) iconBytes = File.ReadAllBytes(cachedIcon);
                    }
                    catch { }
                }

                if (iconBytes == null || iconBytes.Length == 0)
                {
                    iconBytes = GenerateDefaultHomebrewIcon(appTitle);
                }

                File.WriteAllBytes(Path.Combine(controlRomfsDir, "icon_AmericanEnglish.dat"), iconBytes);
                File.WriteAllBytes(Path.Combine(controlRomfsDir, "icon_Russian.dat"), iconBytes);

                App.RunOnUI(() =>
                {
                    task.Progress = 35;
                    task.Status = "Сборка Program NCA...";
                    task.LogDetails += "[1/4] Сборка Program NCA (ExeFS + RomFS)...\n";
                });

                // 4. Сборка Program NCA через hacpack
                string progArgs = $"-k \"{keysFile}\" --type nca --ncatype program --titleid {titleId} --exefsdir \"{exefsDir}\" --romfsdir \"{effectiveRomfsDir}\" -o \"{outProgramDir}\"";
                int progCode = await ExternalProcessRunner.RunAsync(_hacpackExe, progArgs, tempDir, task, ct);
                if (progCode != 0)
                {
                    throw new Exception($"hacpack завершился с ошибкой (код {progCode}) при сборке Program NCA.");
                }

                var progNcas = Directory.GetFiles(outProgramDir, "*.nca");
                if (progNcas.Length == 0)
                {
                    throw new Exception("hacpack не создал Program NCA. Проверьте валидность prod.keys.");
                }
                string programNca = progNcas[0];

                App.RunOnUI(() =>
                {
                    task.Progress = 55;
                    task.Status = "Сборка Control NCA...";
                    task.LogDetails += "[2/4] Сборка Control NCA (Метаданные + Иконка)...\n";
                });

                // 5. Сборка Control NCA через hacpack
                string ctrlArgs = $"-k \"{keysFile}\" --type nca --ncatype control --titleid {titleId} --romfsdir \"{controlRomfsDir}\" -o \"{outControlDir}\"";
                int ctrlCode = await ExternalProcessRunner.RunAsync(_hacpackExe, ctrlArgs, tempDir, task, ct);
                if (ctrlCode != 0)
                {
                    throw new Exception($"hacpack завершился с ошибкой (код {ctrlCode}) при сборке Control NCA.");
                }

                var ctrlNcas = Directory.GetFiles(outControlDir, "*.nca");
                if (ctrlNcas.Length == 0)
                {
                    throw new Exception("hacpack не создал Control NCA.");
                }
                string controlNca = ctrlNcas[0];

                App.RunOnUI(() =>
                {
                    task.Progress = 70;
                    task.Status = "Сборка Meta NCA (CNMT)...";
                    task.LogDetails += "[3/4] Сборка Meta NCA (CNMT дескриптор)...\n";
                });

                // 6. Сборка Meta NCA (CNMT) через hacpack
                string metaArgs = $"-k \"{keysFile}\" --type nca --ncatype meta --titletype application --titleid {titleId} --titleversion 0x0 --programnca \"{programNca}\" --controlnca \"{controlNca}\" -o \"{outMetaDir}\"";
                int metaCode = await ExternalProcessRunner.RunAsync(_hacpackExe, metaArgs, tempDir, task, ct);
                if (metaCode != 0)
                {
                    throw new Exception($"hacpack завершился с ошибкой (код {metaCode}) при сборке Meta NCA.");
                }

                var metaNcas = Directory.GetFiles(outMetaDir, "*.nca");
                if (metaNcas.Length == 0)
                {
                    throw new Exception("hacpack не создал Meta NCA (CNMT).");
                }

                // 7. Сборка финального NSP
                File.Copy(programNca, Path.Combine(allNcasDir, Path.GetFileName(programNca)), true);
                File.Copy(controlNca, Path.Combine(allNcasDir, Path.GetFileName(controlNca)), true);
                foreach (var mNca in metaNcas)
                {
                    File.Copy(mNca, Path.Combine(allNcasDir, Path.GetFileName(mNca)), true);
                }

                App.RunOnUI(() =>
                {
                    task.Progress = 85;
                    task.Status = "Финальная упаковка NSP...";
                    task.LogDetails += "[4/4] Финальная упаковка PFS0 (NSP)...\n";
                });

                string tempNsp = Path.Combine(tempDir, task.OutputFileName + ".nsp");
                var pfsBuilder = new PartitionFileSystemBuilder();
                var openedStreams = new List<FileStream>();

                try
                {
                    var allNcaFiles = Directory.GetFiles(allNcasDir, "*.nca");
                    var orderedNcas = allNcaFiles.OrderBy(f =>
                    {
                        string name = Path.GetFileName(f).ToLowerInvariant();
                        if (name.EndsWith(".cnmt.nca")) return 0;
                        if (f.Equals(controlNca, StringComparison.OrdinalIgnoreCase) || name.Contains("control")) return 1;
                        if (f.Equals(programNca, StringComparison.OrdinalIgnoreCase) || name.Contains("program")) return 2;
                        return 3;
                    }).ThenBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase);

                    foreach (var ncaPath in orderedNcas)
                    {
                        string fileName = Path.GetFileName(ncaPath);
                        var fs = new FileStream(ncaPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        openedStreams.Add(fs);
                        pfsBuilder.AddFile(fileName, new StorageFile(new SafeStorageWrapper(fs.AsStorage()), LibHac.Fs.OpenMode.Read));
                    }

                    using var builtPfs = pfsBuilder.Build(PartitionFileSystemType.Standard);
                    builtPfs.GetSize(out long totalSize).ThrowIfFailure();

                    using (var destStream = new FileStream(tempNsp, FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024 * 1024))
                    {
                        long remaining = totalSize;
                        long offset = 0;
                        byte[] buffer = new byte[8 * 1024 * 1024];

                        while (remaining > 0)
                        {
                            ct.ThrowIfCancellationRequested();
                            int toRead = (int)Math.Min(buffer.Length, remaining);
                            builtPfs.Read(offset, buffer.AsSpan(0, toRead)).ThrowIfFailure();
                            destStream.Write(buffer, 0, toRead);
                            offset += toRead;
                            remaining -= toRead;

                            int prog = 85 + (int)(10.0 * (totalSize - remaining) / totalSize);
                            App.RunOnUI(() => task.Progress = prog);
                        }
                    }
                }
                finally
                {
                    foreach (var s in openedStreams) s.Dispose();
                }

                if (!File.Exists(tempNsp))
                {
                    throw new Exception("Не удалось создать выходной NSP контейнер.");
                }

                // 8. Конвертация / перемещение в целевой формат (NSP, NSZ, XCI, XCZ)
                string outFolder = task.OutputFolder;
                if (string.IsNullOrWhiteSpace(outFolder))
                {
                    outFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OUT", "Homebrew");
                }
                Directory.CreateDirectory(outFolder);

                string targetExt = (task.TargetFormat ?? "NSP").ToUpperInvariant();
                string finalPath = Path.Combine(outFolder, task.OutputFileName + "." + targetExt.ToLowerInvariant());

                if (targetExt == "NSP")
                {
                    File.Copy(tempNsp, finalPath, true);
                }
                else if (targetExt == "NSZ")
                {
                    App.RunOnUI(() =>
                    {
                        task.Status = "Сжатие в NSZ...";
                        task.LogDetails += "Сжатие NSP -> NSZ...\n";
                    });
                    await App.NszCompression.CompressToNszAsync(task, tempNsp, outFolder, ct);
                    finalPath = Path.Combine(outFolder, task.OutputFileName + ".nsz");
                }
                else if (targetExt == "XCI" || targetExt == "XCZ")
                {
                    App.RunOnUI(() =>
                    {
                        task.Status = "Конвертация в XCI...";
                        task.LogDetails += "Конвертация NSP -> XCI...\n";
                    });
                    await App.SwitchFormat.ConvertContainerAsync(task, tempNsp, outFolder, "XCI", ct);
                    string xciPath = Path.Combine(outFolder, task.OutputFileName + ".xci");
                    finalPath = xciPath;

                    if (targetExt == "XCZ" && File.Exists(xciPath))
                    {
                        App.RunOnUI(() =>
                        {
                            task.Status = "Сжатие в XCZ...";
                            task.LogDetails += "Сжатие XCI -> XCZ...\n";
                        });
                        await App.NszCompression.CompressToNszAsync(task, xciPath, outFolder, ct);
                        string generatedNsz = Path.ChangeExtension(xciPath, ".nsz");
                        string generatedXcz = Path.ChangeExtension(xciPath, ".xcz");
                        if (File.Exists(generatedNsz))
                        {
                            if (File.Exists(generatedXcz)) File.Delete(generatedXcz);
                            File.Move(generatedNsz, generatedXcz);
                        }
                        try { File.Delete(xciPath); } catch { }
                        finalPath = generatedXcz;
                    }
                }

                // Автономное формирование SDMC пакета и синхронизация с локальными эмуляторами
                try
                {
                    string? mainNro = task.InputFiles.Concat(extractedArchiveFiles).FirstOrDefault(f => File.Exists(f) && Path.GetExtension(f).Equals(".nro", StringComparison.OrdinalIgnoreCase));
                    string nextPath = "";
                    string fwdNextNro = Path.Combine(romfsDir, "nextNroPath");
                    if (File.Exists(fwdNextNro))
                    {
                        nextPath = File.ReadAllText(fwdNextNro).Trim();
                    }

                    bool hasSdmcTarget = (mainNro != null) || !string.IsNullOrWhiteSpace(nextPath);

                    if (hasSdmcTarget)
                    {
                        string nroName = mainNro != null ? Path.GetFileName(mainNro) : "main.nro";
                        string nroAppFolder = mainNro != null 
                            ? Path.GetFileNameWithoutExtension(mainNro).Replace("-switch", "", StringComparison.OrdinalIgnoreCase).Trim() 
                            : "";

                        if (string.IsNullOrWhiteSpace(nextPath))
                        {
                            if (task.InputFiles.Any(f => f.Contains("devilutionx", StringComparison.OrdinalIgnoreCase)))
                                nextPath = $"sdmc:/devilutionx-switch/{nroName}";
                            else if (task.InputFiles.Any(f => f.Contains("drmariomania", StringComparison.OrdinalIgnoreCase)))
                                nextPath = $"sdmc:/switch/drmariomania_nx/{nroName}";
                            else if (task.InputFiles.Any(f => f.Contains("gizmoduck", StringComparison.OrdinalIgnoreCase)))
                                nextPath = $"sdmc:/switch/gizmoduck/{nroName}";
                            else if (task.InputFiles.Any(f => f.Contains("mk64", StringComparison.OrdinalIgnoreCase) || f.Contains("spaghetti", StringComparison.OrdinalIgnoreCase)))
                                nextPath = $"sdmc:/switch/mk64_spaghetti/{nroName}";
                            else if (task.InputFiles.Any(f => f.Contains("smbr", StringComparison.OrdinalIgnoreCase)))
                                nextPath = $"sdmc:/switch/smbr_nx/{nroName}";
                            else if (task.InputFiles.Any(f => f.Contains("thextech", StringComparison.OrdinalIgnoreCase)))
                                nextPath = $"sdmc:/switch/{nroName}";
                            else
                            {
                                string folder = !string.IsNullOrEmpty(nroAppFolder) ? nroAppFolder : "app";
                                nextPath = $"sdmc:/switch/{folder}/{nroName}";
                            }
                        }

                        string relativeTarget = nextPath.Replace("sdmc:/", "").TrimStart('/');
                        string targetSubfolder = Path.GetDirectoryName(relativeTarget) ?? ("switch/" + nroAppFolder);
                        if (string.IsNullOrWhiteSpace(targetSubfolder))
                        {
                            targetSubfolder = "switch/" + (string.IsNullOrEmpty(nroAppFolder) ? "app" : nroAppFolder);
                        }

                        // Временная папка подготовки данных SDMC
                        string tempSdmcStaging = Path.Combine(tempDir, "sdmc_staging", targetSubfolder);
                        Directory.CreateDirectory(tempSdmcStaging);

                        // 1. Копируем все подкаталоги ресурсов (assets, save, android_root, TheXTech, etc.)
                        var allInputDirs = task.InputFiles.Concat(extractedArchiveDirs).Where(dir => Directory.Exists(dir) && 
                            !Path.GetFileName(dir).Equals("exefs", StringComparison.OrdinalIgnoreCase) && 
                            !Path.GetFileName(dir).Equals("romfs", StringComparison.OrdinalIgnoreCase) && 
                            !Path.GetFileName(dir).Equals("exefs_patches", StringComparison.OrdinalIgnoreCase) &&
                            !Path.GetFileName(dir).Equals("atmosphere", StringComparison.OrdinalIgnoreCase) &&
                            !Path.GetFileName(dir).Equals("Add-ons", StringComparison.OrdinalIgnoreCase) &&
                            !Path.GetFileName(dir).Equals("unpacked_archives", StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                        string targetSubBaseName = Path.GetFileName(targetSubfolder);

                        foreach (var d in allInputDirs)
                        {
                            string dName = Path.GetFileName(d);
                            if (dName.Equals("devilutionx-switch", StringComparison.OrdinalIgnoreCase) || 
                                (!string.IsNullOrEmpty(nroAppFolder) && dName.Equals(nroAppFolder, StringComparison.OrdinalIgnoreCase)) ||
                                dName.Equals(targetSubBaseName, StringComparison.OrdinalIgnoreCase))
                            {
                                CopyDirectory(d, tempSdmcStaging);
                            }
                            else if (dName.Equals("switch", StringComparison.OrdinalIgnoreCase))
                            {
                                string nestedApp = Path.Combine(d, targetSubBaseName);
                                if (Directory.Exists(nestedApp))
                                {
                                    CopyDirectory(nestedApp, tempSdmcStaging);
                                }
                                else
                                {
                                    CopyDirectory(d, tempSdmcStaging);
                                }
                            }
                            else if (dName.Equals("TheXTech", StringComparison.OrdinalIgnoreCase))
                            {
                                string xtechStaging = Path.Combine(tempDir, "sdmc_staging", "TheXTech");
                                Directory.CreateDirectory(xtechStaging);
                                CopyDirectory(d, xtechStaging);
                                CopyDirectory(d, Path.Combine(tempSdmcStaging, dName));
                            }
                            else if (dName.Contains("Hellfire", StringComparison.OrdinalIgnoreCase) || dName.Contains("Russian", StringComparison.OrdinalIgnoreCase))
                            {
                                foreach (var subDev in Directory.GetDirectories(d, "devilutionx-switch", SearchOption.AllDirectories))
                                {
                                    CopyDirectory(subDev, tempSdmcStaging);
                                }
                            }
                            else if (dName.Equals("assets", StringComparison.OrdinalIgnoreCase) ||
                                     dName.Equals("save", StringComparison.OrdinalIgnoreCase) ||
                                     dName.Equals("android_root", StringComparison.OrdinalIgnoreCase))
                            {
                                CopyDirectory(d, Path.Combine(tempSdmcStaging, dName));
                            }
                        }

                        // 2. Копируем все loose файлы данных (включая распакованные архивы)
                        var allLooseFiles = task.InputFiles.Concat(extractedArchiveFiles).Where(file => File.Exists(file) && 
                            !file.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) && 
                            !file.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) && 
                            !file.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) && 
                            !file.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase) && 
                            !file.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) && 
                            !file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && 
                            !file.EndsWith(".rar", StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                        foreach (var f in allLooseFiles)
                        {
                            string fExt = Path.GetExtension(f);
                            if (fExt.Equals(".pck", StringComparison.OrdinalIgnoreCase) ||
                                fExt.Equals(".so", StringComparison.OrdinalIgnoreCase) ||
                                fExt.Equals(".otf", StringComparison.OrdinalIgnoreCase) ||
                                fExt.Equals(".ttf", StringComparison.OrdinalIgnoreCase) ||
                                fExt.Equals(".nes", StringComparison.OrdinalIgnoreCase) ||
                                fExt.Equals(".mpq", StringComparison.OrdinalIgnoreCase) ||
                                fExt.Equals(".o2r", StringComparison.OrdinalIgnoreCase) ||
                                fExt.Equals(".wad", StringComparison.OrdinalIgnoreCase) ||
                                fExt.Equals(".rpf", StringComparison.OrdinalIgnoreCase) ||
                                fExt.Equals(".xml", StringComparison.OrdinalIgnoreCase) ||
                                fExt.Equals(".ini", StringComparison.OrdinalIgnoreCase) ||
                                fExt.Equals(".json", StringComparison.OrdinalIgnoreCase))
                            {
                                CopyFileWithRetry(f, Path.Combine(tempSdmcStaging, Path.GetFileName(f)), true);
                            }
                        }

                        // 3. Копируем основной NRO
                        if (mainNro != null && File.Exists(mainNro))
                        {
                            CopyFileWithRetry(mainNro, Path.Combine(tempSdmcStaging, Path.GetFileName(mainNro)), true);
                        }

                        // 4. ВСЕГДА формируем автономный пакет SDMC карты памяти рядом с собранным файлом
                        string sdmcRoot = Path.Combine(outFolder, $"{task.OutputFileName}_[SDMC]");
                        string sdmcTargetDir = Path.Combine(sdmcRoot, targetSubfolder);
                        Directory.CreateDirectory(sdmcTargetDir);
                        CopyDirectory(tempSdmcStaging, sdmcTargetDir);

                        // Для TheXTech: дублируем в sdmc/TheXTech
                        string xtechSrc = Path.Combine(tempDir, "sdmc_staging", "TheXTech");
                        if (Directory.Exists(xtechSrc))
                        {
                            string sdmcXtech = Path.Combine(sdmcRoot, "TheXTech");
                            Directory.CreateDirectory(sdmcXtech);
                            CopyDirectory(xtechSrc, sdmcXtech);
                        }

                        // Для Diablo: дублируем в devilutionx-switch и switch/devilutionx
                        if (task.InputFiles.Any(f => f.Contains("devilutionx", StringComparison.OrdinalIgnoreCase)) ||
                            targetSubfolder.Contains("devilutionx", StringComparison.OrdinalIgnoreCase))
                        {
                            string devTarget1 = Path.Combine(sdmcRoot, "devilutionx-switch");
                            string devTarget2 = Path.Combine(sdmcRoot, "switch", "devilutionx");
                            Directory.CreateDirectory(devTarget1);
                            Directory.CreateDirectory(devTarget2);
                            CopyDirectory(tempSdmcStaging, devTarget1);
                            CopyDirectory(tempSdmcStaging, devTarget2);
                        }

                        App.RunOnUI(() =>
                        {
                            task.LogDetails += $"[SDMC] Создана автономная структура карты памяти: {sdmcRoot}\n";
                        });

                        // 5. Авто-деплой в целевые папки SDMC локальных эмуляторов
                        var localEmuSdmcList = FindAllEmulatorSdmcDirectories();
                        foreach (var emuSdmc in localEmuSdmcList)
                        {
                            if (Directory.Exists(emuSdmc))
                            {
                                string singleTargetFolder = Path.Combine(emuSdmc, targetSubfolder);
                                Directory.CreateDirectory(singleTargetFolder);
                                CopyDirectory(tempSdmcStaging, singleTargetFolder);

                                if (Directory.Exists(xtechSrc))
                                {
                                    string emuXtech = Path.Combine(emuSdmc, "TheXTech");
                                    Directory.CreateDirectory(emuXtech);
                                    CopyDirectory(xtechSrc, emuXtech);
                                }

                                if (task.InputFiles.Any(f => f.Contains("devilutionx", StringComparison.OrdinalIgnoreCase)) ||
                                    targetSubfolder.Contains("devilutionx", StringComparison.OrdinalIgnoreCase))
                                {
                                    string devTarget1 = Path.Combine(emuSdmc, "devilutionx-switch");
                                    string devTarget2 = Path.Combine(emuSdmc, "switch", "devilutionx");
                                    Directory.CreateDirectory(devTarget1);
                                    Directory.CreateDirectory(devTarget2);
                                    CopyDirectory(tempSdmcStaging, devTarget1);
                                    CopyDirectory(tempSdmcStaging, devTarget2);
                                }

                                App.RunOnUI(() =>
                                {
                                    task.LogDetails += $"[SDMC] Синхронизированы ресурсы игры в эмулятор: {singleTargetFolder}\n";
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.RunOnUI(() =>
                    {
                        task.LogDetails += $"[SDMC] Предупреждение синхронизации: {ex.Message}\n";
                    });
                }

                App.RunOnUI(() =>
                {
                    if (File.Exists(finalPath))
                    {
                        long outSize = new FileInfo(finalPath).Length;
                        task.TargetSize = ProcessingTask.FormatSize(outSize);
                        if (task.SourceSizeBytes > 0)
                        {
                            long diff = task.SourceSizeBytes - outSize;
                            double percent = (double)diff / task.SourceSizeBytes * 100.0;
                            task.SizeDifference = $"{(diff > 0 ? "-" : "+")}{ProcessingTask.FormatSize(Math.Abs(diff))} ({Math.Abs(percent):F1}%)";
                        }
                    }

                    task.Progress = 100;
                    task.Status = "Успешно";
                    task.IsRunning = false;
                    task.LogDetails += $"✅ Сборка Homebrew успешно завершена: {Path.GetFileName(finalPath)} ({task.TargetSize})\n";
                });
            }
            catch (Exception ex)
            {
                App.RunOnUI(() =>
                {
                    task.IsRunning = false;
                    task.Status = "Ошибка";
                    task.LogDetails += $"\n❌ Ошибка сборки Homebrew: {ex.Message}\n";
                });
                throw;
            }
            finally
            {
                TempCleanupService.ForceDeleteDirectory(tempDir);
            }
        }

        /// <summary>
        /// Генерация стандартной универсальной структуры ExeFS (main.npdm + forwarder NSO)
        /// </summary>
        private void GenerateUniversalForwarderExeFs(string exefsDir, string titleIdStr)
        {
            ulong titleId = ulong.Parse(titleIdStr, System.Globalization.NumberStyles.HexNumber);

            // 1. Генерация main.npdm (0x1000 байт с правильным TitleID и правами доступа)
            byte[] npdm = new byte[0x1000];

            // META Header
            Encoding.ASCII.GetBytes("META").CopyTo(npdm, 0x00);
            BitConverter.GetBytes(0x00000000U).CopyTo(npdm, 0x04); // Flags
            BitConverter.GetBytes(0x00000000U).CopyTo(npdm, 0x08); // Reserved
            BitConverter.GetBytes(0x00000000U).CopyTo(npdm, 0x0C); // Main thread priority
            BitConverter.GetBytes(0x00000000U).CopyTo(npdm, 0x10); // Main thread core
            BitConverter.GetBytes(0x00100000U).CopyTo(npdm, 0x14); // Main thread stack size
            Encoding.ASCII.GetBytes("STORM-HB-FWD").CopyTo(npdm, 0x18); // Process name

            BitConverter.GetBytes(0x00000200U).CopyTo(npdm, 0x40); // ACI0 offset
            BitConverter.GetBytes(0x00000200U).CopyTo(npdm, 0x44); // ACI0 size
            BitConverter.GetBytes(0x00000400U).CopyTo(npdm, 0x48); // ACID offset
            BitConverter.GetBytes(0x00000400U).CopyTo(npdm, 0x4C); // ACID size

            // ACI0 Section (0x200)
            Encoding.ASCII.GetBytes("ACI0").CopyTo(npdm, 0x200);
            BitConverter.GetBytes(titleId).CopyTo(npdm, 0x210); // Title ID
            BitConverter.GetBytes(0x10000000UL).CopyTo(npdm, 0x218); // System resource size

            // FAC (Filesystem Access Control) - полные права
            BitConverter.GetBytes(0x00000001U).CopyTo(npdm, 0x220); // Version
            BitConverter.GetBytes(0xFFFFFFFFFFFFFFFFUL).CopyTo(npdm, 0x228); // Permissions bitmask

            // ACID Section (0x400)
            Encoding.ASCII.GetBytes("ACID").CopyTo(npdm, 0x400);
            BitConverter.GetBytes(0x00000001U).CopyTo(npdm, 0x404); // Flags (production)
            BitConverter.GetBytes(0UL).CopyTo(npdm, 0x410); // Min TitleID
            BitConverter.GetBytes(0xFFFFFFFFFFFFFFFFUL).CopyTo(npdm, 0x418); // Max TitleID
            BitConverter.GetBytes(0xFFFFFFFFFFFFFFFFUL).CopyTo(npdm, 0x420); // FAC mask

            File.WriteAllBytes(Path.Combine(exefsDir, "main.npdm"), npdm);

            // 2. Генерация NSO forwarder binary (main)
            byte[] nso = GenerateForwarderNso(titleId);
            File.WriteAllBytes(Path.Combine(exefsDir, "main"), nso);
        }

        /// <summary>
        /// Генерация стандартного NSO бинарника Forwarder'а
        /// </summary>
        private byte[] GenerateForwarderNso(ulong titleId)
        {
            // NSO0 заголовок (0x100 байт) + минимальный исполняемый код
            byte[] nso = new byte[0x1000];

            // Magic 'NSO0'
            Encoding.ASCII.GetBytes("NSO0").CopyTo(nso, 0x00);
            BitConverter.GetBytes(0U).CopyTo(nso, 0x04); // Version
            BitConverter.GetBytes(0U).CopyTo(nso, 0x08); // Flags (uncompressed)

            // Text segment
            BitConverter.GetBytes(0x100U).CopyTo(nso, 0x10); // File offset
            BitConverter.GetBytes(0x0000U).CopyTo(nso, 0x14); // Memory offset
            BitConverter.GetBytes(0x400U).CopyTo(nso, 0x18);  // Decompressed size

            // Ro segment
            BitConverter.GetBytes(0x500U).CopyTo(nso, 0x20); // File offset
            BitConverter.GetBytes(0x1000U).CopyTo(nso, 0x24); // Memory offset
            BitConverter.GetBytes(0x200U).CopyTo(nso, 0x28);  // Decompressed size

            // Data segment
            BitConverter.GetBytes(0x700U).CopyTo(nso, 0x30); // File offset
            BitConverter.GetBytes(0x2000U).CopyTo(nso, 0x34); // Memory offset
            BitConverter.GetBytes(0x100U).CopyTo(nso, 0x38);  // Decompressed size

            BitConverter.GetBytes(0x1000U).CopyTo(nso, 0x3C); // BSS size

            // Module ID / Build ID (32 bytes)
            using var sha = SHA256.Create();
            byte[] buildId = sha.ComputeHash(BitConverter.GetBytes(titleId));
            Array.Copy(buildId, 0, nso, 0x40, 32);

            // ARM64 Exit/Return stub (RET: 0xD65F03C0, SVC 0x07 ExitProcess: 0xD40000E1)
            // 0x100 Text offset
            BitConverter.GetBytes(0xD2800000U).CopyTo(nso, 0x100); // MOV X0, #0
            BitConverter.GetBytes(0xD40000E1U).CopyTo(nso, 0x104); // SVC #7 (ExitProcess)
            BitConverter.GetBytes(0xD65F03C0U).CopyTo(nso, 0x108); // RET

            // Вычисляем sha256 хеши секций и записываем в NSO заголовок
            byte[] textHash = sha.ComputeHash(nso, 0x100, 0x400);
            byte[] roHash = sha.ComputeHash(nso, 0x500, 0x200);
            byte[] dataHash = sha.ComputeHash(nso, 0x700, 0x100);

            Array.Copy(textHash, 0, nso, 0xA0, 32);
            Array.Copy(roHash, 0, nso, 0xC0, 32);
            Array.Copy(dataHash, 0, nso, 0xE0, 32);

            return nso;
        }

        private byte[] GenerateNacp(string title, string author, string version, string titleIdStr)
        {
            byte[] nacp = new byte[0x4000];
            byte[] titleBytes = Encoding.UTF8.GetBytes(title);
            byte[] authorBytes = Encoding.UTF8.GetBytes(author);

            for (int i = 0; i < 16; i++)
            {
                int offset = i * 0x300;
                Array.Copy(titleBytes, 0, nacp, offset, Math.Min(titleBytes.Length, 0x200));
                Array.Copy(authorBytes, 0, nacp, offset + 0x200, Math.Min(authorBytes.Length, 0x100));
            }

            // Version string at 0x3060
            byte[] verBytes = Encoding.UTF8.GetBytes(version);
            Array.Copy(verBytes, 0, nacp, 0x3060, Math.Min(verBytes.Length, 0x10));

            // Title ID at 0x3078
            if (ulong.TryParse(titleIdStr, System.Globalization.NumberStyles.HexNumber, null, out ulong tid))
            {
                BitConverter.GetBytes(tid).CopyTo(nacp, 0x3078);
                BitConverter.GetBytes(tid).CopyTo(nacp, 0x3038); // PresenceGroupId
                BitConverter.GetBytes(tid).CopyTo(nacp, 0x3070); // AddOnContentBaseId
            }

            return nacp;
        }

        public static void CopyFileWithRetry(string source, string destination, bool overwrite = true, int maxRetries = 3, int delayMs = 200)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    string? dir = Path.GetDirectoryName(destination);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.Copy(source, destination, overwrite);
                    return;
                }
                catch (IOException) when (i < maxRetries - 1)
                {
                    Thread.Sleep(delayMs);
                }
            }
        }

        public static bool CheckDiskSpace(string targetPath, long requiredBytes, out long availableBytes)
        {
            try
            {
                string root = Path.GetPathRoot(Path.GetFullPath(targetPath)) ?? "C:\\";
                var drive = new DriveInfo(root);
                availableBytes = drive.AvailableFreeSpace;
                return availableBytes >= requiredBytes;
            }
            catch
            {
                availableBytes = -1;
                return true;
            }
        }

        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                CopyFileWithRetry(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
            }
            foreach (var subDir in Directory.GetDirectories(sourceDir))
            {
                CopyDirectory(subDir, Path.Combine(targetDir, Path.GetFileName(subDir)));
            }
        }

        /// <summary>
        /// Выполняет всесторонний поиск папок SDMC эмуляторов на всех подключенных накопителях (C:, D:, E:, L: и др.),
        /// в запущенных процессах и системных профилях Roaming / LocalAppData.
        /// </summary>
        public static List<string> FindAllEmulatorSdmcDirectories()
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // 0. Если пользователь явно указал папки эмуляторов в Настройках — проверяем и используем ТОЛЬКО ИХ, остальные игнорируем!
            try
            {
                var customDirs = App.Settings?.Current?.EmulatorDirectories ?? SettingsService.Instance?.Current?.EmulatorDirectories;
                if (customDirs != null && customDirs.Count > 0)
                {
                    foreach (var rawDir in customDirs)
                    {
                        if (string.IsNullOrWhiteSpace(rawDir)) continue;
                        string dir = rawDir.Trim();
                        if (!Directory.Exists(dir)) continue;

                        string sdmcDirect = Path.Combine(dir, "sdmc");
                        string userSdmc = Path.Combine(dir, "user", "sdmc");
                        string assemblingUserSdmc = Path.Combine(dir, "Assembling", "user", "sdmc");

                        if (Directory.Exists(sdmcDirect))
                        {
                            result.Add(sdmcDirect);
                        }
                        else if (Directory.Exists(userSdmc))
                        {
                            result.Add(userSdmc);
                        }
                        else if (Directory.Exists(assemblingUserSdmc))
                        {
                            result.Add(assemblingUserSdmc);
                        }
                        else
                        {
                            result.Add(dir);
                        }
                    }

                    if (result.Count > 0)
                    {
                        return result.ToList();
                    }
                }
            }
            catch { }

            // 1. Стандартные системные профили AppData и LocalAppData
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            string[] standardProfileSubpaths = new[]
            {
                Path.Combine(appData, "yuzu", "sdmc"),
                Path.Combine(appData, "Ryujinx", "sdmc"),
                Path.Combine(appData, "suyu", "sdmc"),
                Path.Combine(appData, "sudachi", "sdmc"),
                Path.Combine(appData, "eden", "sdmc"),
                Path.Combine(localAppData, "yuzu", "sdmc"),
                Path.Combine(localAppData, "suyu", "sdmc"),
                Path.Combine(localAppData, "sudachi", "sdmc"),
                Path.Combine(localAppData, "Ryujinx", "sdmc")
            };

            foreach (var p in standardProfileSubpaths)
            {
                if (Directory.Exists(p)) result.Add(p);
            }

            // 2. Чтение конфигурационных файлов эмуляторов (где прописан реальный путь к SDMC)
            try
            {
                string[] configFiles = new[]
                {
                    Path.Combine(appData, "yuzu", "config", "qt-config.ini"),
                    Path.Combine(appData, "eden", "config", "qt-config.ini"),
                    Path.Combine(appData, "suyu", "config", "qt-config.ini"),
                    Path.Combine(appData, "sudachi", "config", "qt-config.ini")
                };

                foreach (var cfg in configFiles)
                {
                    if (File.Exists(cfg))
                    {
                        foreach (var line in File.ReadLines(cfg))
                        {
                            if (line.StartsWith("sdmc_directory", StringComparison.OrdinalIgnoreCase))
                            {
                                int eq = line.IndexOf('=');
                                if (eq > 0)
                                {
                                    string customSdmc = line.Substring(eq + 1).Trim().Trim('"');
                                    if (Directory.Exists(customSdmc))
                                    {
                                        result.Add(customSdmc);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // 3. Сканирование всех доступных дисков (C:, D:, E:, L: и т.д.)
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    string root = drive.RootDirectory.FullName;

                    string[] directCandidates = new[]
                    {
                        Path.Combine(root, "CONSOLES", "Nintendo Switch", "STORM SWITCH 4", "user", "sdmc"),
                        Path.Combine(root, "CONSOLES", "Nintendo Switch", "STORM SWITCH", "user", "sdmc"),
                        Path.Combine(root, "CONSOLES", "Nintendo Switch", "STORM EDEN", "user", "sdmc"),
                        Path.Combine(root, "CONSOLES", "Nintendo Switch", "Eden", "user", "sdmc"),
                        Path.Combine(root, "STORM SWITCH 4", "Assembling", "user", "sdmc"),
                        Path.Combine(root, "STORM SWITCH 4", "user", "sdmc"),
                        Path.Combine(root, "STORM SWITCH 4", "sdmc"),
                        Path.Combine(root, "STORM SWITCH", "Assembling", "user", "sdmc"),
                        Path.Combine(root, "STORM SWITCH", "user", "sdmc"),
                        Path.Combine(root, "STORM SWITCH", "sdmc"),
                        Path.Combine(root, "STORM EDEN 3", "Assembling", "user", "sdmc"),
                        Path.Combine(root, "STORM EDEN 3", "user", "sdmc"),
                        Path.Combine(root, "STORM EDEN", "user", "sdmc"),
                        Path.Combine(root, "Eden", "user", "sdmc"),
                        Path.Combine(root, "Eden", "sdmc"),
                        Path.Combine(root, "yuzu", "user", "sdmc"),
                        Path.Combine(root, "yuzu", "sdmc"),
                        Path.Combine(root, "Ryujinx", "user", "sdmc"),
                        Path.Combine(root, "Ryujinx", "sdmc"),
                        Path.Combine(root, "suyu", "user", "sdmc"),
                        Path.Combine(root, "sudachi", "user", "sdmc"),
                        Path.Combine(root, "Emulators", "STORM SWITCH 4", "user", "sdmc"),
                        Path.Combine(root, "Emulators", "STORM SWITCH", "user", "sdmc"),
                        Path.Combine(root, "Emulators", "STORM EDEN 3", "user", "sdmc"),
                        Path.Combine(root, "Emulators", "yuzu", "user", "sdmc"),
                        Path.Combine(root, "Emulators", "Ryujinx", "user", "sdmc"),
                        Path.Combine(root, "Games", "Emulators", "STORM SWITCH 4", "user", "sdmc"),
                        Path.Combine(root, "Games", "Emulators", "STORM SWITCH", "user", "sdmc"),
                        Path.Combine(root, "Games", "Emulators", "STORM EDEN 3", "user", "sdmc")
                    };

                    foreach (var cand in directCandidates)
                    {
                        if (Directory.Exists(cand))
                        {
                            result.Add(cand);
                        }
                    }

                    // Поиск в корневых папках диска (1 уровень вложенности)
                    try
                    {
                        foreach (var topDir in Directory.GetDirectories(root))
                        {
                            string topName = Path.GetFileName(topDir).ToLowerInvariant();
                            if (topName.Contains("eden") || topName.Contains("yuzu") || topName.Contains("ryujinx") ||
                                topName.Contains("suyu") || topName.Contains("sudachi") || topName.Contains("emulator") ||
                                topName.Contains("storm switch") || (topName.StartsWith("switch") && !topName.Contains("box")))
                            {
                                string userSdmc = Path.Combine(topDir, "user", "sdmc");
                                if (Directory.Exists(userSdmc)) result.Add(userSdmc);

                                string directSdmc = Path.Combine(topDir, "sdmc");
                                if (Directory.Exists(directSdmc)) result.Add(directSdmc);

                                string assemblingUserSdmc = Path.Combine(topDir, "Assembling", "user", "sdmc");
                                if (Directory.Exists(assemblingUserSdmc)) result.Add(assemblingUserSdmc);
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }

            // 4. Поиск в запущенных процессах эмуляторов
            try
            {
                string[] processNames = new[] { "eden", "stormeden", "yuzu", "ryujinx", "suyu", "sudachi", "torzu", "citron" };
                foreach (var pName in processNames)
                {
                    foreach (var proc in System.Diagnostics.Process.GetProcessesByName(pName))
                    {
                        try
                        {
                            string? procPath = proc.MainModule?.FileName;
                            if (!string.IsNullOrEmpty(procPath))
                            {
                                string? procDir = Path.GetDirectoryName(procPath);
                                if (!string.IsNullOrEmpty(procDir))
                                {
                                    string sdmc1 = Path.Combine(procDir, "user", "sdmc");
                                    if (Directory.Exists(sdmc1)) result.Add(sdmc1);

                                    string sdmc2 = Path.Combine(procDir, "sdmc");
                                    if (Directory.Exists(sdmc2)) result.Add(sdmc2);
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return result.ToList();
        }
    }
}

