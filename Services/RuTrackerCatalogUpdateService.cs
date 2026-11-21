#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public sealed class CatalogRuTrackerUpdateResult
    {
        public string BaseTitleId { get; set; } = string.Empty;
        public string TopicId { get; set; } = string.Empty;
        public string TopicTitle { get; set; } = string.Empty;
        public string RuTrackerVersion { get; set; } = string.Empty;
        public string RuTrackerVersionCode { get; set; } = string.Empty;
        public int RuTrackerPatchInt { get; set; }
        public int RuTrackerDlcCount { get; set; }
        public HashSet<string> RuTrackerDlcIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public bool IsVerifiedByTitleId { get; set; }
    }

    /// <summary>
    /// Проверяет наличие обновлений версий и новых DLC на RuTracker для локальных игр в разделе «Информация».
    /// </summary>
    public sealed class RuTrackerCatalogUpdateService
    {
        private readonly RuTrackerModService _ruTracker = new();

        public async Task<int> CheckCatalogUpdatesAsync(
            IReadOnlyList<CatalogItem> items,
            Action<int, int, int>? onProgress,
            CancellationToken ct)
        {
            if (items == null || items.Count == 0)
                return 0;

            var localSwitchItems = items
                .Where(i => i != null &&
                            !i.Is3ds &&
                            !i.IsOnlineOnly &&
                            !string.IsNullOrWhiteSpace(i.TitleId) &&
                            i.TitleId.Trim().Length == 16 &&
                            !string.Equals(i.TitleId.Trim(), "0000000000000000", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (localSwitchItems.Count == 0)
            {
                onProgress?.Invoke(0, 0, 0);
                return 0;
            }

            // 1. Загружаем индекс Switch и актуальную ленту/хэши форума f=1605
            RuTrackerModService.EnsureIndexLoaded();
            try
            {
                using var feedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                feedCts.CancelAfter(TimeSpan.FromSeconds(6));
                await Task.WhenAll(
                    RuTrackerModService.RefreshAtomFeedAsync(feedCts.Token),
                    RuTrackerModService.RefreshPvcMapAsync(feedCts.Token)).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // Нефатально: работаем с уже загруженным индексом
            }

            // Группируем по базовому TitleID, чтобы проверять каждую игру на RuTracker ровно один раз
            var itemsByBaseTid = new Dictionary<string, List<CatalogItem>>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in localSwitchItems)
            {
                string baseTid = RuTrackerModService.NormalizeSwitchBaseApplicationTitleId(item.TitleId);
                if (baseTid.Length != 16) continue;
                if (!itemsByBaseTid.TryGetValue(baseTid, out var list))
                {
                    list = new List<CatalogItem>();
                    itemsByBaseTid[baseTid] = list;
                }
                list.Add(item);
            }

            int totalGames = itemsByBaseTid.Count;
            int processedGames = 0;
            int itemsWithUpdates = 0;
            object progressLock = new();

            using var gate = new SemaphoreSlim(4);
            var tasks = itemsByBaseTid.Select(async kvp =>
            {
                string baseTid = kvp.Key;
                List<CatalogItem> groupItems = kvp.Value;

                await gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    ct.ThrowIfCancellationRequested();
                    CatalogItem rep = groupItems[0];
                    var resolved = await ResolveLatestRuTrackerReleaseForGameAsync(baseTid, rep, ct).ConfigureAwait(false);

                    int newlyFlaggedInGroup = 0;
                    foreach (var item in groupItems)
                    {
                        bool anyUpdate = ApplyRuTrackerComparisonToItem(item, baseTid, resolved);
                        if (anyUpdate) newlyFlaggedInGroup++;
                    }

                    lock (progressLock)
                    {
                        processedGames++;
                        itemsWithUpdates += newlyFlaggedInGroup;
                        onProgress?.Invoke(processedGames, totalGames, itemsWithUpdates);
                    }
                }
                finally
                {
                    gate.Release();
                }
            }).ToList();

            await Task.WhenAll(tasks).ConfigureAwait(false);
            return itemsWithUpdates;
        }

        private async Task<CatalogRuTrackerUpdateResult?> ResolveLatestRuTrackerReleaseForGameAsync(
            string baseTid,
            CatalogItem sampleItem,
            CancellationToken ct)
        {
            string queryName = ResolveSearchQueryName(baseTid, sampleItem);
            if (string.IsNullOrWhiteSpace(queryName))
                return null;

            var candidates = _ruTracker.SearchTopicsInstant(queryName, "Switch (все)", 20);
            if (candidates.Count == 0 && queryName.Contains(':'))
            {
                string shortName = queryName.Split(':')[0].Trim();
                if (shortName.Length >= 3)
                    candidates = _ruTracker.SearchTopicsInstant(shortName, "Switch (все)", 20);
            }
            if (candidates.Count == 0)
                return null;

            var ranked = RankCandidates(queryName, baseTid, candidates).Take(3).ToList();
            if (ranked.Count == 0)
                return null;

            CatalogRuTrackerUpdateResult? bestResult = null;
            foreach (var cand in ranked)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    using var topicCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    topicCts.CancelAfter(TimeSpan.FromSeconds(8));

                    // Быстро получаем актуальный InfoHash + спарсенную версию из шапки темы без обхода комментариев
                    var rtClient = new RuTrackerModService();
                    RuTrackerTopicItem? detailed = null;
                    try
                    {
                        detailed = await rtClient.FetchTopicDetailsAsync(cand.TopicId, topicCts.Token, includeCommentPages: false).ConfigureAwait(false);
                    }
                    catch { }

                    var effectiveTopic = detailed ?? cand;
                    if (string.IsNullOrWhiteSpace(effectiveTopic.InfoHash))
                        effectiveTopic.InfoHash = cand.InfoHash;
                    if (string.IsNullOrWhiteSpace(effectiveTopic.Title))
                        effectiveTopic.Title = cand.Title;

                    IReadOnlyList<TorrentFileSelectionItem>? files = null;
                    try
                    {
                        files = await rtClient.InspectTorrentFilesAsync(effectiveTopic, null, topicCts.Token).ConfigureAwait(false);
                    }
                    catch { }

                    var realFiles = files?.Where(f => f.IsRealTorrentFile && f.CategoryBadge != "MAGNET").ToList()
                                    ?? new List<TorrentFileSelectionItem>();

                    if (realFiles.Count > 0)
                    {
                        var nonStandaloneTids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        bool hasMatchingTid = false;
                        foreach (var rf in realFiles)
                        {
                            string rel = rf.RelativePath ?? string.Empty;
                            if (RuTrackerModService.IsStandaloneBonusFile(rel)) continue;
                            string rawTid = !string.IsNullOrWhiteSpace(rf.DetectedTitleId)
                                ? rf.DetectedTitleId
                                : RuTrackerModService.ExtractTitleId(rel);
                            if (rawTid.Length != 16) continue;
                            string normTid = RuTrackerModService.NormalizeSwitchBaseApplicationTitleId(rawTid);
                            if (normTid.Length == 16)
                            {
                                nonStandaloneTids.Add(normTid);
                                if (string.Equals(normTid, baseTid, StringComparison.OrdinalIgnoreCase))
                                    hasMatchingTid = true;
                            }
                        }

                        // Если в раздаче явно присутствуют Switch TitleID и среди них нет нашего baseTid - это другая игра
                        if (nonStandaloneTids.Count > 0 && !hasMatchingTid)
                            continue;

                        var extracted = ExtractReleaseMetricsFromFiles(baseTid, effectiveTopic, realFiles, hasMatchingTid);
                        if (bestResult == null || IsBetterResult(extracted, bestResult))
                        {
                            bestResult = extracted;
                        }

                        if (extracted.IsVerifiedByTitleId)
                            break;
                    }
                    else if (!string.IsNullOrWhiteSpace(effectiveTopic.ParsedGameVersion) || TitleDbService.IsValidSemanticVersion(effectiveTopic.Title))
                    {
                        // Мягкий запасной разбор из заголовка/шапки темы, если .torrent сейчас недоступен
                        string htmlVer = ResolveSemanticVersionFromTopicOnly(baseTid, effectiveTopic);
                        int titleDlcCount = ExtractDlcCountHintFromTitle(effectiveTopic.Title);
                        if (!string.IsNullOrWhiteSpace(htmlVer) || titleDlcCount > 0)
                        {
                            var fallbackRes = new CatalogRuTrackerUpdateResult
                            {
                                BaseTitleId = baseTid,
                                TopicId = effectiveTopic.TopicId,
                                TopicTitle = effectiveTopic.Title,
                                RuTrackerVersion = htmlVer,
                                RuTrackerVersionCode = "0",
                                RuTrackerPatchInt = 0,
                                RuTrackerDlcCount = titleDlcCount,
                                IsVerifiedByTitleId = false
                            };
                            if (bestResult == null || IsBetterResult(fallbackRes, bestResult))
                                bestResult = fallbackRes;
                        }
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    // Переходим к следующему кандидату
                }
            }

            return bestResult;
        }

        public static CatalogRuTrackerUpdateResult ExtractReleaseMetricsFromFiles(
            string baseTid,
            RuTrackerTopicItem topic,
            IReadOnlyList<TorrentFileSelectionItem> realFiles,
            bool verifiedByTid)
        {
            int maxPatchVer = 0;
            var dlcTids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int untaggedDlcFiles = 0;
            string? explicitSemVer = null;

            bool isMultiGameTorrent = realFiles
                .Where(rf => !RuTrackerModService.IsStandaloneBonusFile(rf.RelativePath))
                .Select(rf =>
                {
                    string t = !string.IsNullOrWhiteSpace(rf.DetectedTitleId) ? rf.DetectedTitleId : RuTrackerModService.ExtractTitleId(rf.RelativePath);
                    return t.Length == 16 ? RuTrackerModService.NormalizeSwitchBaseApplicationTitleId(t) : string.Empty;
                })
                .Where(t => t.Length == 16)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() > 1;

            foreach (var rf in realFiles)
            {
                string rel = rf.RelativePath ?? string.Empty;
                // Исключаем Standalone-бонусы (артбуки, саундтреки с отдельным TitleID)
                if (RuTrackerModService.IsStandaloneBonusFile(rel))
                    continue;

                string rawTid = !string.IsNullOrWhiteSpace(rf.DetectedTitleId)
                    ? rf.DetectedTitleId
                    : RuTrackerModService.ExtractTitleId(rel);
                string normTid = rawTid.Length == 16 ? RuTrackerModService.NormalizeSwitchBaseApplicationTitleId(rawTid) : string.Empty;

                if (rawTid.Length == 16 && !string.Equals(normTid, baseTid, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (rawTid.Length == 16 && RuTrackerModService.IsDlcOfBaseTitle(baseTid, rawTid))
                {
                    dlcTids.Add(rawTid.ToUpperInvariant());
                }
                else if (string.IsNullOrEmpty(rawTid) && !isMultiGameTorrent && rf.CategoryBadge == "DLC")
                {
                    untaggedDlcFiles++;
                }

                var vMatch = Regex.Match(rel, @"\[v(?<ver>\d+)\]", RegexOptions.IgnoreCase);
                if (vMatch.Success && int.TryParse(vMatch.Groups["ver"].Value, out int vNum) && vNum > maxPatchVer)
                {
                    maxPatchVer = vNum;
                }

                foreach (Match vm in Regex.Matches(
                    rel,
                    @"(?:\[|\(|\b(?:[vV]|Update|UPD|Patch)\s*)(?<sv>\d+\.\d+(?:\.\d+){0,2})(?=\]|\)|\b)",
                    RegexOptions.IgnoreCase))
                {
                    string candVer = vm.Groups["sv"].Value.Trim();
                    if (!TitleDbService.IsValidSemanticVersion(candVer)) continue;
                    int afterIdx = vm.Index + vm.Length;
                    string tail = afterIdx < rel.Length ? rel.Substring(afterIdx, Math.Min(8, rel.Length - afterIdx)) : string.Empty;
                    if (Regex.IsMatch(tail, @"^\s*(?:GB|MB|KB|TB|ГБ|МБ|КБ|ТБ)\b", RegexOptions.IgnoreCase))
                        continue;
                    if (string.IsNullOrEmpty(explicitSemVer) || TitleDbService.CompareSemanticVersions(candVer, explicitSemVer) > 0)
                        explicitSemVer = candVer;
                }
            }

            if (string.IsNullOrEmpty(explicitSemVer) && maxPatchVer > 0 && App.TitleDb != null &&
                App.TitleDb.TryResolveDisplayVersion(baseTid, maxPatchVer, out var dbVer) &&
                TitleDbService.IsValidSemanticVersion(dbVer))
            {
                explicitSemVer = dbVer;
            }

            if (string.IsNullOrEmpty(explicitSemVer) &&
                topic.ParsedTitleVersions != null &&
                topic.ParsedTitleVersions.TryGetValue(baseTid, out var htmlTidVer) &&
                TitleDbService.IsValidSemanticVersion(htmlTidVer))
            {
                explicitSemVer = htmlTidVer;
            }

            if (string.IsNullOrEmpty(explicitSemVer) && !isMultiGameTorrent &&
                TitleDbService.IsValidSemanticVersion(topic.ParsedGameVersion))
            {
                explicitSemVer = topic.ParsedGameVersion;
            }

            if (string.IsNullOrEmpty(explicitSemVer) && !isMultiGameTorrent && !string.IsNullOrWhiteSpace(topic.Title))
            {
                foreach (Match vm in Regex.Matches(
                    topic.Title,
                    @"(?:\[|\(|\b(?:[vV]|Update|UPD|Patch)\s*)(?<sv>\d+\.\d+(?:\.\d+){0,2})(?=\]|\)|\b)",
                    RegexOptions.IgnoreCase))
                {
                    string candVer = vm.Groups["sv"].Value.Trim();
                    if (!TitleDbService.IsValidSemanticVersion(candVer)) continue;
                    int afterIdx = vm.Index + vm.Length;
                    string tail = afterIdx < topic.Title.Length ? topic.Title.Substring(afterIdx, Math.Min(8, topic.Title.Length - afterIdx)) : string.Empty;
                    if (Regex.IsMatch(tail, @"^\s*(?:GB|MB|KB|TB|ГБ|МБ|КБ|ТБ)\b", RegexOptions.IgnoreCase))
                        continue;
                    if (string.IsNullOrEmpty(explicitSemVer) || TitleDbService.CompareSemanticVersions(candVer, explicitSemVer) > 0)
                        explicitSemVer = candVer;
                }
            }

            if (maxPatchVer > 0 && !string.IsNullOrEmpty(explicitSemVer))
            {
                App.TitleDb?.RecordObservedDisplayVersion(baseTid, maxPatchVer.ToString(), explicitSemVer);
            }

            string finalVerDisplay = !string.IsNullOrEmpty(explicitSemVer)
                ? explicitSemVer
                : (maxPatchVer > 0 ? $"v{maxPatchVer}" : "1.0.0");

            return new CatalogRuTrackerUpdateResult
            {
                BaseTitleId = baseTid,
                TopicId = topic.TopicId ?? string.Empty,
                TopicTitle = topic.Title ?? string.Empty,
                RuTrackerVersion = finalVerDisplay,
                RuTrackerVersionCode = maxPatchVer > 0 ? maxPatchVer.ToString() : "0",
                RuTrackerPatchInt = maxPatchVer,
                RuTrackerDlcCount = dlcTids.Count > 0 ? dlcTids.Count : untaggedDlcFiles,
                RuTrackerDlcIds = dlcTids,
                IsVerifiedByTitleId = verifiedByTid
            };
        }

        public static bool ApplyRuTrackerComparisonToItem(CatalogItem item, string baseTid, CatalogRuTrackerUpdateResult? rt)
        {
            if (rt == null)
            {
                App.RunOnUI(() =>
                {
                    item.HasRuTrackerVersionUpdate = false;
                    item.HasRuTrackerNewDlc = false;
                    item.HasRuTrackerUpdate = false;
                    item.RuTrackerTopicId = string.Empty;
                    item.RuTrackerTopicTitle = string.Empty;
                    item.RuTrackerVersion = string.Empty;
                    item.RuTrackerVersionCode = string.Empty;
                    item.RuTrackerDlcCount = 0;
                });
                return false;
            }

            uint.TryParse(Regex.Replace(item.VersionCode ?? string.Empty, @"\D", ""), out uint localPatchVer);

            bool hasVersionUpdate = false;
            if (rt.RuTrackerPatchInt > 0 && localPatchVer > 0)
            {
                hasVersionUpdate = rt.RuTrackerPatchInt > (long)localPatchVer;
            }
            else if (rt.RuTrackerPatchInt > 0 && localPatchVer == 0)
            {
                // Локальный файл имеет патч 0 (база 1.0.0), на RuTracker уже есть апдейт > 0
                hasVersionUpdate = true;
            }
            else if (TitleDbService.IsValidSemanticVersion(rt.RuTrackerVersion) &&
                     TitleDbService.IsValidSemanticVersion(item.Version))
            {
                hasVersionUpdate = TitleDbService.CompareSemanticVersions(rt.RuTrackerVersion, item.Version) > 0;
            }

            bool hasNewDlc = false;
            if (item.LocalDlcIds != null && item.LocalDlcIds.Count > 0 && rt.RuTrackerDlcIds.Count > 0)
            {
                hasNewDlc = rt.RuTrackerDlcIds.Any(id => !item.LocalDlcIds.Contains(id));
            }
            if (!hasNewDlc && rt.RuTrackerDlcCount > item.LocalDlcCount)
            {
                hasNewDlc = true;
            }

            bool hasAnyUpdate = hasVersionUpdate || hasNewDlc;

            App.RunOnUI(() =>
            {
                item.RuTrackerTopicId = rt.TopicId;
                item.RuTrackerTopicTitle = rt.TopicTitle;
                item.RuTrackerVersion = rt.RuTrackerVersion;
                item.RuTrackerVersionCode = rt.RuTrackerVersionCode;
                item.RuTrackerDlcCount = rt.RuTrackerDlcCount;
                item.HasRuTrackerVersionUpdate = hasVersionUpdate;
                item.HasRuTrackerNewDlc = hasNewDlc;
                item.HasRuTrackerUpdate = hasAnyUpdate;
            });

            return hasAnyUpdate;
        }

        private static string ResolveSearchQueryName(string baseTid, CatalogItem sampleItem)
        {
            string? name = sampleItem.TitleName;
            if ((string.IsNullOrWhiteSpace(name) || name == "Unknown Game" || name == "Unknown") &&
                App.TitleDb != null && App.TitleDb.TryGetTitleInfo(baseTid, out var dbEntry) &&
                !string.IsNullOrWhiteSpace(dbEntry?.Name))
            {
                name = dbEntry.Name;
            }
            if (string.IsNullOrWhiteSpace(name) || name == "Unknown Game" || name == "Unknown")
            {
                name = RuTrackerModService.ExtractCleanGameTitleFromFilePath(sampleItem.FileName ?? string.Empty);
            }
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            // Удаляем технические суффиксы издания/региона для точного совпадения в индексе
            string cleaned = Regex.Replace(name, @"\s*\([^)]*\)|\s*\[[^\]]*\]", " ");
            cleaned = cleaned.Replace("™", "").Replace("®", "").Replace("©", "");
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            return cleaned;
        }

        private static List<RuTrackerTopicItem> RankCandidates(string queryName, string baseTid, List<RuTrackerTopicItem> raw)
        {
            string normQuery = NormalizeComparisonKey(queryName);
            var qTokens = normQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length >= 2).ToHashSet(StringComparer.Ordinal);

            return raw
                .Select(t =>
                {
                    string rawTitle = t.Title ?? string.Empty;
                    string lowerTitle = rawTitle.ToLowerInvariant();
                    string cleanTopic = NormalizeComparisonKey(t.CleanTitleDisplay);

                    int score = 0;
                    // Штрафуем чисто модовые/сейвовые/эмуляторные раздачи без самой игры
                    if (lowerTitle.Contains("[emulator]") || lowerTitle.Contains("[mods]") || lowerTitle.Contains("сохранени") || lowerTitle.Contains("save file"))
                        score -= 500;
                    if (lowerTitle.Contains("[dlc only]") || lowerTitle.Contains("только dlc"))
                        score -= 100;
                    if (lowerTitle.Contains("[update only]") || lowerTitle.Contains("только обновлен"))
                        score -= 80;

                    if (string.Equals(cleanTopic, normQuery, StringComparison.Ordinal))
                        score += 1000;
                    else if (!string.IsNullOrEmpty(normQuery) && cleanTopic.Contains(normQuery, StringComparison.Ordinal))
                        score += 550;
                    else if (!string.IsNullOrEmpty(cleanTopic) && normQuery.Contains(cleanTopic, StringComparison.Ordinal) && cleanTopic.Length >= 4)
                        score += 350;

                    var tTokens = cleanTopic.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    int matchedTokens = qTokens.Count > 0 ? tTokens.Count(tok => qTokens.Contains(tok)) : 0;
                    score += matchedTokens * 120;
                    score -= Math.Abs(tTokens.Length - qTokens.Count) * 40;

                    if (!string.IsNullOrEmpty(baseTid) && rawTitle.Contains(baseTid, StringComparison.OrdinalIgnoreCase))
                        score += 800;

                    if (lowerTitle.Contains("[rus") || lowerTitle.Contains("rus/"))
                        score += 35;

                    score += Math.Min(t.Seeds, 200) / 5;
                    return (Topic: t, Score: score);
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Topic.Seeds)
                .Select(x => x.Topic)
                .ToList();
        }

        private static string NormalizeComparisonKey(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            string s = input.ToLowerInvariant()
                .Replace("’", "'")
                .Replace("'", "")
                .Replace(":", " ")
                .Replace("-", " ");
            s = Regex.Replace(s, @"[^a-z0-9а-яё\s]", " ");
            return Regex.Replace(s, @"\s+", " ").Trim();
        }

        private static bool IsBetterResult(CatalogRuTrackerUpdateResult cand, CatalogRuTrackerUpdateResult current)
        {
            if (cand.IsVerifiedByTitleId != current.IsVerifiedByTitleId)
                return cand.IsVerifiedByTitleId;
            if (cand.RuTrackerPatchInt != current.RuTrackerPatchInt)
                return cand.RuTrackerPatchInt > current.RuTrackerPatchInt;
            if (cand.RuTrackerDlcCount != current.RuTrackerDlcCount)
                return cand.RuTrackerDlcCount > current.RuTrackerDlcCount;
            return TitleDbService.CompareSemanticVersions(cand.RuTrackerVersion, current.RuTrackerVersion) > 0;
        }

        private static string ResolveSemanticVersionFromTopicOnly(string baseTid, RuTrackerTopicItem topic)
        {
            if (topic.ParsedTitleVersions != null &&
                topic.ParsedTitleVersions.TryGetValue(baseTid, out var tidVer) &&
                TitleDbService.IsValidSemanticVersion(tidVer))
            {
                return tidVer;
            }
            if (TitleDbService.IsValidSemanticVersion(topic.ParsedGameVersion))
                return topic.ParsedGameVersion;
            return string.Empty;
        }

        private static int ExtractDlcCountHintFromTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title)) return 0;
            var m = Regex.Match(title, @"\+\s*(\d+)\s*DLC\b", RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups[1].Value, out int count))
                return count;
            if (Regex.IsMatch(title, @"\+\s*DLC\b", RegexOptions.IgnoreCase))
                return 1;
            return 0;
        }
    }
}
