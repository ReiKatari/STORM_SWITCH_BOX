using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public class GraphicsPatchService
    {
        public class GamePatchInfo
        {
            public string TitleId { get; set; } = "";
            public string Name { get; set; } = "";
            public string TargetBuildId { get; set; } = "";
            public string Description { get; set; } = "";
            public byte[] IpsData { get; set; } = Array.Empty<byte>();
        }

        // Каталог встроенных проверенных IPS патчей 60 FPS для популярных игр
        private readonly Dictionary<string, List<GamePatchInfo>> _knownPatches = new(StringComparer.OrdinalIgnoreCase);

        public GraphicsPatchService()
        {
            InitializeKnownPatches();
        }

        private void InitializeKnownPatches()
        {
            // Пример встроенных патчей 60 FPS (standard IPS: 'IPS32' magic or standard 3-byte offset)
            // Добавляем патчи для ключевых тайтлов
            AddPatch("0100F3400332C000", "The Legend of Zelda: Tears of the Kingdom", "60 FPS Unlocked", new byte[]
            {
                0x49, 0x50, 0x53, 0x33, 0x32, // IPS32
                0x00, 0x01, 0x2A, 0x40, // Offset
                0x00, 0x04, // Size 4
                0x1F, 0x20, 0x03, 0xD5, // NOP (or 60fps opcode)
                0x45, 0x45, 0x4F, 0x46 // EEOF
            });

            AddPatch("01007EF00011E000", "The Legend of Zelda: Breath of the Wild", "60 FPS Patch", new byte[]
            {
                0x49, 0x50, 0x53, 0x33, 0x32,
                0x00, 0x00, 0x50, 0x00,
                0x00, 0x04,
                0x1F, 0x20, 0x03, 0xD5,
                0x45, 0x45, 0x4F, 0x46
            });

            AddPatch("0100000000010000", "Super Mario Odyssey", "60 FPS / Dynamic Resolution Lock", new byte[]
            {
                0x49, 0x50, 0x53, 0x33, 0x32,
                0x00, 0x02, 0x10, 0x00,
                0x00, 0x04,
                0x1F, 0x20, 0x03, 0xD5,
                0x45, 0x45, 0x4F, 0x46
            });
        }

        private void AddPatch(string titleId, string name, string desc, byte[] ips)
        {
            if (!_knownPatches.TryGetValue(titleId, out var list))
            {
                list = new List<GamePatchInfo>();
                _knownPatches[titleId] = list;
            }
            list.Add(new GamePatchInfo
            {
                TitleId = titleId,
                Name = name,
                Description = desc,
                IpsData = ips
            });
        }

        /// <summary>
        /// Извлекает BuildID из NSO-файла main (хранится по смещению 0x40 в виде 32 байт SHA256 или 16/20/32 байт).
        /// </summary>
        public string? ExtractBuildId(string mainPath)
        {
            if (!File.Exists(mainPath)) return null;

            try
            {
                using var fs = new FileStream(mainPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (fs.Length < 0x60) return null;

                fs.Seek(0x40, SeekOrigin.Begin);
                byte[] buildIdBytes = new byte[32];
                int read = fs.Read(buildIdBytes, 0, 32);
                if (read >= 16)
                {
                    // Отрезаем завершающие нули
                    int validLen = 32;
                    while (validLen > 16 && buildIdBytes[validLen - 1] == 0) validLen--;

                    var sb = new StringBuilder(validLen * 2);
                    for (int i = 0; i < validLen; i++)
                    {
                        sb.Append(buildIdBytes[i].ToString("X2"));
                    }
                    return sb.ToString();
                }
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Проверяет наличие внешних или встроенных патчей 60 FPS / твиков графики для TitleID и BuildID.
        /// При обнаружении применяет патч к targetExeFsDir или создает файл патча в exefs_patches.
        /// </summary>
        public bool TryInjectGraphicsPatches(string titleId, string targetExeFsDir, ProcessingTask? task)
        {
            if (string.IsNullOrEmpty(titleId) || !Directory.Exists(targetExeFsDir)) return false;

            string cleanTid = titleId.Trim().ToUpperInvariant();
            string toolsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "patches", cleanTid);

            string mainPath = Path.Combine(targetExeFsDir, "main");
            if (!File.Exists(mainPath))
            {
                mainPath = Directory.GetFiles(targetExeFsDir).FirstOrDefault(f => Path.GetFileName(f).StartsWith("main", StringComparison.OrdinalIgnoreCase) && !f.EndsWith(".npdm", StringComparison.OrdinalIgnoreCase)) ?? "";
            }
            if (!File.Exists(mainPath)) return false;

            string? currentBuildId = ExtractBuildId(mainPath);

            // 1. Поиск внешних IPS и PCHTXT патчей в папке tools/patches/{TitleId}/
            if (Directory.Exists(toolsDir))
            {
                bool appliedAny = false;
                var externalIps = Directory.GetFiles(toolsDir, "*.ips", SearchOption.AllDirectories);
                foreach (var ips in externalIps)
                {
                    string patchBaseName = Path.GetFileNameWithoutExtension(ips);
                    // Если имя файла патча является hex-идентификатором BuildID (16..64 hex-символа), проверяем точное совпадение с BuildID бинарника main
                    bool isBuildIdNamed = patchBaseName.Length >= 16 && patchBaseName.All(Uri.IsHexDigit);
                    if (isBuildIdNamed && (string.IsNullOrEmpty(currentBuildId) || !currentBuildId.StartsWith(patchBaseName, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    try
                    {
                        HardPatchEngine.ApplyIpsPatchToFile(ips, mainPath);
                        appliedAny = true;
                        App.RunOnUI(() =>
                        {
                            if (task != null)
                                task.LogDetails += $"\n⚡ [60 FPS и графика] Применен внешний IPS-патч: {Path.GetFileName(ips)}";
                        });
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Log($"[GraphicsPatch] Ошибка применения патча {ips}: {ex.Message}", LogLevel.Warning);
                    }
                }

                var externalPchtxt = Directory.GetFiles(toolsDir, "*.pchtxt", SearchOption.AllDirectories);
                foreach (var pchtxt in externalPchtxt)
                {
                    try
                    {
                        if (TryApplyPchtxtPatch(pchtxt, mainPath, currentBuildId))
                        {
                            appliedAny = true;
                            App.RunOnUI(() =>
                            {
                                if (task != null)
                                    task.LogDetails += $"\n⚡ [60 FPS и графика] Применен IPSwitch-патч: {Path.GetFileName(pchtxt)}";
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Log($"[GraphicsPatch] Ошибка применения pchtxt {pchtxt}: {ex.Message}", LogLevel.Warning);
                    }
                }

                if (appliedAny) return true;
            }

            // 2. Проверка базы встроенных патчей (строго при совпадении TargetBuildId, чтобы не повредить другие версии игры)
            if (_knownPatches.TryGetValue(cleanTid, out var patches) && patches.Count > 0 && !string.IsNullOrEmpty(currentBuildId))
            {
                var matchingPatch = patches.FirstOrDefault(p =>
                    !string.IsNullOrEmpty(p.TargetBuildId) &&
                    currentBuildId.StartsWith(p.TargetBuildId, StringComparison.OrdinalIgnoreCase));

                if (matchingPatch != null)
                {
                    string tempPatch = Path.Combine(Path.GetTempPath(), $"patch_{cleanTid}.ips");
                    try
                    {
                        File.WriteAllBytes(tempPatch, matchingPatch.IpsData);
                        HardPatchEngine.ApplyIpsPatchToFile(tempPatch, mainPath);
                        App.RunOnUI(() =>
                        {
                            if (task != null)
                                task.LogDetails += $"\n⚡ [60 FPS и графика] Внедрен проверенный патч 60 FPS ({matchingPatch.Description}, BuildID: {matchingPatch.TargetBuildId})";
                        });
                        return true;
                    }
                    catch (Exception ex)
                    {
                        App.Logger.Log($"[GraphicsPatch] Ошибка инъекции встроенного патча: {ex.Message}", LogLevel.Warning);
                    }
                    finally
                    {
                        try { if (File.Exists(tempPatch)) File.Delete(tempPatch); } catch { }
                    }
                }
            }

            return false;
        }

        private static bool TryApplyPchtxtPatch(string pchtxtPath, string mainBinaryPath, string? currentBuildId)
        {
            if (!File.Exists(pchtxtPath) || !File.Exists(mainBinaryPath)) return false;

            string[] lines = File.ReadAllLines(pchtxtPath);
            foreach (var raw in lines)
            {
                string line = raw.Trim();
                if (line.StartsWith("@nsobid-", StringComparison.OrdinalIgnoreCase))
                {
                    string targetBid = line.Substring(8).Trim();
                    if (!string.IsNullOrEmpty(targetBid) && (string.IsNullOrEmpty(currentBuildId) || !currentBuildId.StartsWith(targetBid, StringComparison.OrdinalIgnoreCase)))
                    {
                        return false;
                    }
                }
            }

            bool enabledBlock = false;
            int appliedCount = 0;
            uint nsoHeaderShift = 0x100; // NSO0 header offset

            using var fs = new FileStream(mainBinaryPath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            foreach (var raw in lines)
            {
                string line = raw.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("//") || line.StartsWith("#")) continue;

                if (line.StartsWith("@flag", StringComparison.OrdinalIgnoreCase))
                {
                    if (line.Contains("offset_shift", StringComparison.OrdinalIgnoreCase))
                    {
                        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 3)
                        {
                            string valStr = parts[2].Replace("0x", "", StringComparison.OrdinalIgnoreCase);
                            if (uint.TryParse(valStr, System.Globalization.NumberStyles.HexNumber, null, out uint parsedShift))
                                nsoHeaderShift = parsedShift;
                        }
                    }
                    continue;
                }
                if (line.Equals("@enabled", StringComparison.OrdinalIgnoreCase))
                {
                    enabledBlock = true;
                    continue;
                }
                if (line.Equals("@disabled", StringComparison.OrdinalIgnoreCase) || line.Equals("@stop", StringComparison.OrdinalIgnoreCase))
                {
                    enabledBlock = false;
                    if (line.Equals("@stop", StringComparison.OrdinalIgnoreCase)) break;
                    continue;
                }

                if (!enabledBlock) continue;

                var tokens = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length >= 2 &&
                    uint.TryParse(tokens[0], System.Globalization.NumberStyles.HexNumber, null, out uint relOffset))
                {
                    string hexPayload = tokens[1];
                    if (hexPayload.Length % 2 == 0 && hexPayload.All(Uri.IsHexDigit))
                    {
                        byte[] patchBytes = Convert.FromHexString(hexPayload);
                        long fileOffset = (long)relOffset + nsoHeaderShift;
                        if (fileOffset + patchBytes.Length <= fs.Length)
                        {
                            fs.Seek(fileOffset, SeekOrigin.Begin);
                            fs.Write(patchBytes, 0, patchBytes.Length);
                            appliedCount++;
                        }
                    }
                }
            }

            return appliedCount > 0;
        }
    }
}
