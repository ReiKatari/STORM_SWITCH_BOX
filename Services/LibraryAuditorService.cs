using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public class LibraryAuditIssue
    {
        public bool IsSelected { get; set; } = true;
        public string IssueType { get; set; } = string.Empty;
        public string IssueLabel { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;
        public string TitleId { get; set; } = string.Empty;
        public string Platform { get; set; } = "Switch";
        public string Recommendation { get; set; } = string.Empty;
        public long CurrentSizeBytes { get; set; }
        public long EstimatedSavingsBytes { get; set; }
        public string CurrentSizeFormatted => ProcessingTask.FormatSize(CurrentSizeBytes);
        public string SavingsFormatted => EstimatedSavingsBytes > 0 ? ("~" + ProcessingTask.FormatSize(EstimatedSavingsBytes)) : "-";
        public List<string> RelatedFiles { get; set; } = new();
        public string PrimaryFile => RelatedFiles.FirstOrDefault() ?? string.Empty;
    }

    public class LibraryAuditReport
    {
        public int TotalFilesScanned { get; set; }
        public long TotalLibraryBytes { get; set; }
        public long TotalEstimatedSavingsBytes { get; set; }
        public List<LibraryAuditIssue> Issues { get; set; } = new();
    }

    public static class LibraryAuditorService
    {
        private static readonly HashSet<string> SupportedExts = new(StringComparer.OrdinalIgnoreCase)
        {
            ".nsp", ".nsz", ".xci", ".xcz", ".3ds", ".cci", ".cia", ".cxi"
        };

        public static async Task<LibraryAuditReport> ScanLibraryFolderAsync(
            string rootFolder,
            IProgress<(int Scanned, int Total, string CurrentFile)>? progress,
            CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                var report = new LibraryAuditReport();
                if (string.IsNullOrWhiteSpace(rootFolder) || !Directory.Exists(rootFolder))
                    return report;

                var allFiles = Directory.EnumerateFiles(rootFolder, "*.*", SearchOption.AllDirectories)
                    .Where(f => SupportedExts.Contains(Path.GetExtension(f)))
                    .ToList();

                report.TotalFilesScanned = allFiles.Count;

                var switchRecords = new List<(string Path, long Size, string TitleId, string BaseAppId, string ContentType, uint Version, string GameName)>();
                var threeDsRecords = new List<(string Path, long Size, string TitleId, string BaseAppId, string ContentType, uint Version, string GameName, bool IsEncrypted)>();

                for (int i = 0; i < allFiles.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    string f = allFiles[i];
                    long size = 0;
                    try { size = new FileInfo(f).Length; } catch { continue; }
                    report.TotalLibraryBytes += size;

                    progress?.Report((i + 1, allFiles.Count, Path.GetFileName(f)));
                    string ext = Path.GetExtension(f).ToLowerInvariant();

                    if (Nintendo3dsService.Is3dsExtension(ext))
                    {
                        var meta = App.Nintendo3ds.Parse3dsFile(f);
                        string tid = (meta.TitleId ?? "").ToUpperInvariant();
                        string baseId = tid.Length == 16 ? ("00040000" + tid.Substring(8, 6) + "00") : Path.GetFileNameWithoutExtension(f);
                        uint ver = 0;
                        if (!string.IsNullOrEmpty(meta.Version) && meta.Version.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                            uint.TryParse(meta.Version.Substring(1), out ver);
                        string gName = !string.IsNullOrWhiteSpace(meta.GameName) ? meta.GameName : Path.GetFileNameWithoutExtension(f);
                        bool enc = Nintendo3dsCryptoService.Is3dsFileEncrypted(f);

                        threeDsRecords.Add((f, size, tid, baseId, meta.ContentType ?? "Application", ver, gName, enc));

                        if (enc)
                        {
                            report.Issues.Add(new LibraryAuditIssue
                            {
                                IssueType = "Encrypted3ds",
                                IssueLabel = "?? ????????????? 3DS",
                                GameName = gName,
                                TitleId = tid,
                                Platform = "3DS",
                                CurrentSizeBytes = size,
                                EstimatedSavingsBytes = 0,
                                Recommendation = "???????????? ????? 3DS (SeedDB + NCCH) ??? ?????????? Citra/Azahar ? ?????",
                                RelatedFiles = new List<string> { f }
                            });
                        }

                        if (ext == ".3ds" || ext == ".cci")
                        {
                            long trimSavings = EstimateTrailingPaddingBytes(f);
                            if (trimSavings > 2 * 1024 * 1024)
                            {
                                report.Issues.Add(new LibraryAuditIssue
                                {
                                    IssueType = "UntrimmedCartridge",
                                    IssueLabel = "?? ?????? ????? ????????? 3DS",
                                    GameName = gName,
                                    TitleId = tid,
                                    Platform = "3DS",
                                    CurrentSizeBytes = size,
                                    EstimatedSavingsBytes = trimSavings,
                                    Recommendation = $"???????? ?????? 0xFF/0x00 ????? ? ????? ????????? 3DS (-{ProcessingTask.FormatSize(trimSavings)})",
                                    RelatedFiles = new List<string> { f }
                                });
                            }
                        }
                    }
                    else
                    {
                        var info = App.SwitchFormat.ParseNsp(f);
                        string tid = (info.TitleId ?? "").ToUpperInvariant();
                        string baseId = tid.Length == 16 ? (tid.Substring(0, 13) + "000") : Path.GetFileNameWithoutExtension(f);
                        uint.TryParse(info.Version, out uint ver);
                        string gName = Path.GetFileNameWithoutExtension(f);
                        if (!string.IsNullOrEmpty(baseId) && App.TitleDb != null)
                        {
                            var dbEntry = App.TitleDb.GetEntry(baseId) ?? App.TitleDb.GetEntry(tid);
                            if (dbEntry != null && !string.IsNullOrWhiteSpace(dbEntry.Name))
                                gName = dbEntry.Name;
                        }

                        switchRecords.Add((f, size, tid, baseId, info.ContentType ?? "Application", ver, gName));

                        if (ext == ".xci")
                        {
                            long trimSavings = EstimateTrailingPaddingBytes(f);
                            if (trimSavings > 4 * 1024 * 1024)
                            {
                                report.Issues.Add(new LibraryAuditIssue
                                {
                                    IssueType = "UntrimmedCartridge",
                                    IssueLabel = "?? ????????????? XCI",
                                    GameName = gName,
                                    TitleId = tid,
                                    Platform = "Switch",
                                    CurrentSizeBytes = size,
                                    EstimatedSavingsBytes = trimSavings,
                                    Recommendation = $"???????? ?????? ????? ????????? XCI (-{ProcessingTask.FormatSize(trimSavings)})",
                                    RelatedFiles = new List<string> { f }
                                });
                            }
                        }

                        if ((ext == ".nsp" || ext == ".xci") && size > 64 * 1024 * 1024)
                        {
                            long estSavings = (long)(size * 0.32);
                            string targetCompressed = ext == ".xci" ? "XCZ" : "NSZ";
                            report.Issues.Add(new LibraryAuditIssue
                            {
                                IssueType = "UncompressedContainer",
                                IssueLabel = $"??? ???????? {ext.TrimStart('.').ToUpperInvariant()}",
                                GameName = gName,
                                TitleId = tid,
                                Platform = "Switch",
                                CurrentSizeBytes = size,
                                EstimatedSavingsBytes = estSavings,
                                Recommendation = $"????? ? {targetCompressed} (Zstandard) ??? ???????? ~32% ????? ??? ??????",
                                RelatedFiles = new List<string> { f }
                            });
                        }
                    }
                }

                foreach (var grp in switchRecords.Where(r => r.BaseAppId.Length == 16).GroupBy(r => r.BaseAppId))
                {
                    var items = grp.ToList();
                    string gName = items.First().GameName;
                    var patches = items.Where(x => x.ContentType == "Patch" || x.TitleId.EndsWith("800")).OrderByDescending(x => x.Version).ToList();

                    if (patches.Count > 1)
                    {
                        var olderPatches = patches.Skip(1).ToList();
                        long wasted = olderPatches.Sum(p => p.Size);
                        report.Issues.Add(new LibraryAuditIssue
                        {
                            IssueType = "SupersededUpdate",
                            IssueLabel = "??? ????????? ?????? ??????",
                            GameName = gName,
                            TitleId = grp.Key,
                            Platform = "Switch",
                            CurrentSizeBytes = wasted,
                            EstimatedSavingsBytes = wasted,
                            Recommendation = $"??????? {olderPatches.Count} ?????????? ?????????? (??????????: v{patches[0].Version}). ?????? ????? ????? ????????? ???????",
                            RelatedFiles = olderPatches.Select(p => p.Path).ToList()
                        });
                    }

                    if (items.Count > 1)
                    {
                        long totalSetSize = items.Sum(x => x.Size);
                        long estSave = (long)(totalSetSize * 0.18);
                        report.Issues.Add(new LibraryAuditIssue
                        {
                            IssueType = "UnmergedSet",
                            IssueLabel = $"?? ???????? ???????? ({items.Count} ?????)",
                            GameName = gName,
                            TitleId = grp.Key,
                            Platform = "Switch",
                            CurrentSizeBytes = totalSetSize,
                            EstimatedSavingsBytes = estSave,
                            Recommendation = $"????? ????, ?????????? ? DLC ({items.Count} ??.) ? ?????? ?????????? ???? Multi-Content",
                            RelatedFiles = items.Select(x => x.Path).ToList()
                        });
                    }
                }

                foreach (var grp in threeDsRecords.Where(r => r.BaseAppId.Length == 16).GroupBy(r => r.BaseAppId))
                {
                    var items = grp.ToList();
                    string gName = items.First().GameName;
                    var patches = items.Where(x => x.ContentType == "Patch" || x.TitleId.StartsWith("0004000E")).OrderByDescending(x => x.Version).ToList();

                    if (patches.Count > 1)
                    {
                        var older = patches.Skip(1).ToList();
                        long wasted = older.Sum(p => p.Size);
                        report.Issues.Add(new LibraryAuditIssue
                        {
                            IssueType = "SupersededUpdate",
                            IssueLabel = "??? ?????? ????? 3DS",
                            GameName = gName,
                            TitleId = grp.Key,
                            Platform = "3DS",
                            CurrentSizeBytes = wasted,
                            EstimatedSavingsBytes = wasted,
                            Recommendation = $"??????? {older.Count} ?????????? CIA/3DS ?????????? (???????? v{patches[0].Version})",
                            RelatedFiles = older.Select(p => p.Path).ToList()
                        });
                    }

                    if (items.Count > 1)
                    {
                        long totalSetSize = items.Sum(x => x.Size);
                        long estSave = (long)(totalSetSize * 0.15);
                        report.Issues.Add(new LibraryAuditIssue
                        {
                            IssueType = "UnmergedSet",
                            IssueLabel = $"?? ???????? 3DS ???????? ({items.Count} ??.)",
                            GameName = gName,
                            TitleId = grp.Key,
                            Platform = "3DS",
                            CurrentSizeBytes = totalSetSize,
                            EstimatedSavingsBytes = estSave,
                            Recommendation = $"????? ??????? 3DS ????, ?????????? ? DLC ? ?????? .3DS / .CIA ?????????",
                            RelatedFiles = items.Select(x => x.Path).ToList()
                        });
                    }
                }

                report.TotalEstimatedSavingsBytes = report.Issues.Sum(i => i.EstimatedSavingsBytes);
                return report;
            }, ct);
        }

        private static long EstimateTrailingPaddingBytes(string filePath)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                long len = fs.Length;
                if (len < 1024 * 1024) return 0;

                byte[] buf = new byte[64 * 1024];
                long pos = len;
                long padding = 0;

                while (pos > 0x10000)
                {
                    int toRead = (int)Math.Min(buf.Length, pos - 0x10000);
                    pos -= toRead;
                    fs.Position = pos;
                    int read = fs.Read(buf, 0, toRead);
                    int nonPadIdx = -1;
                    for (int i = read - 1; i >= 0; i--)
                    {
                        byte b = buf[i];
                        if (b != 0xFF && b != 0x00)
                        {
                            nonPadIdx = i;
                            break;
                        }
                    }

                    if (nonPadIdx >= 0)
                    {
                        padding += (read - 1 - nonPadIdx);
                        break;
                    }
                    padding += read;
                }

                return padding;
            }
            catch
            {
                return 0;
            }
        }
    }
}
