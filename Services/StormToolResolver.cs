using System;
using System.Collections.Generic;
using System.IO;

namespace StormSwitchBox.Services
{
    public static class StormToolResolver
    {
        private static readonly HashSet<string> ReservedWinNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        public static string ToolsRoot
        {
            get
            {
                string baseTools = Path.Combine(AppContext.BaseDirectory, "tools");
                if (Directory.Exists(baseTools)) return baseTools;
                string cwdTools = Path.Combine(Directory.GetCurrentDirectory(), "tools");
                return Directory.Exists(cwdTools) ? cwdTools : baseTools;
            }
        }

        public static string? ResolveTool(params string[] relativeCandidates)
        {
            string baseDir = AppContext.BaseDirectory;
            string cwd = Directory.GetCurrentDirectory();
            foreach (var rel in relativeCandidates)
            {
                if (string.IsNullOrWhiteSpace(rel)) continue;
                string p1 = Path.Combine(baseDir, rel);
                if (File.Exists(p1)) return p1;
                string p2 = Path.Combine(cwd, rel);
                if (File.Exists(p2)) return p2;
            }
            return null;
        }

        public static string? Resolve7Zip() => ResolveTool(Path.Combine("tools", "7z", "7za.exe"), Path.Combine("tools", "7z.exe"));
        public static string? ResolveHacpack() => ResolveTool(Path.Combine("tools", "hacpack.exe"), Path.Combine("tools", "com.github.nozwock.yanu", "hacpack.exe"));
        public static string? ResolveHactoolnet() => ResolveTool(Path.Combine("tools", "hactoolnet.exe"), Path.Combine("tools", "com.github.nozwock.yanu", "hactoolnet.exe"));
        public static string? ResolveYanuCli() => ResolveTool(Path.Combine("tools", "yanu-cli.exe"));
        public static string? ResolveNszExe() => ResolveTool(Path.Combine("tools", "nsz", "nsz.exe"), Path.Combine("tools", "nsz.exe"));
        public static string? ResolvePythonExe() => ResolveTool(Path.Combine("tools", "nsz", "python.exe"));
        public static string? ResolveLtTurboScript() => ResolveTool(Path.Combine("tools", "nsz", "storm_lt_turbo.py"));
        public static string? ResolveOxipng() => ResolveTool(Path.Combine("tools", "oxipng.exe"));
        public static string? Resolve3dsTool(string exeName) => ResolveTool(Path.Combine("tools", "3ds", exeName), Path.Combine("tools", exeName));
        public static string? ResolveForwarderMain() => ResolveTool(Path.Combine("tools", "forwarder", "main"), Path.Combine("tools", "forwarder", "cnx29", "main"));
        public static string? ResolveForwarderNpdm() => ResolveTool(Path.Combine("tools", "forwarder", "main.npdm"), Path.Combine("tools", "forwarder", "cnx29", "main.npdm"));
        public static string? ResolveNutDbZst() => ResolveTool(Path.Combine("tools", "db", "nutdb.json.zst"));
        public static string? ResolveNutDbJson() => ResolveTool(Path.Combine("tools", "db", "nutdb.json"));
        public static string? ResolveCatalogsZst() => ResolveTool(Path.Combine("tools", "db", "catalogs.zst"));

        public static string EnsureSafeSubPath(string rootDirectory, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentException("Root directory cannot be empty.", nameof(rootDirectory));

            string fullRoot = Path.GetFullPath(rootDirectory);
            if (!fullRoot.EndsWith(Path.DirectorySeparatorChar))
                fullRoot += Path.DirectorySeparatorChar;

            string cleanedRel = (relativePath ?? string.Empty)
                .Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar)
                .TrimStart(Path.DirectorySeparatorChar);

            string combined = Path.GetFullPath(Path.Combine(fullRoot, cleanedRel));
            if (!combined.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(combined.TrimEnd(Path.DirectorySeparatorChar), fullRoot.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Blocked unsafe path traversal attempt: {relativePath}");
            }
            return combined;
        }

        public static string SanitizeFileName(string? name, string fallback = "Game")
        {
            if (string.IsNullOrWhiteSpace(name)) return fallback;
            char[] invalid = Path.GetInvalidFileNameChars();
            var chars = name.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0 || char.IsControl(chars[i]))
                    chars[i] = '_';
            }
            string result = new string(chars).Trim().TrimEnd('.', ' ');
            if (string.IsNullOrWhiteSpace(result)) return fallback;
            string stem = Path.GetFileNameWithoutExtension(result);
            if (ReservedWinNames.Contains(stem))
                result = "_" + result;
            return result;
        }

        public static long CalculateDirectorySize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return 0;
            try
            {
                if (File.Exists(path)) return new FileInfo(path).Length;
                if (!Directory.Exists(path)) return 0;
                long total = 0;
                foreach (var f in Directory.EnumerateFiles(path, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }))
                {
                    try { total += new FileInfo(f).Length; } catch { }
                }
                return total;
            }
            catch
            {
                return 0;
            }
        }
    }
}
