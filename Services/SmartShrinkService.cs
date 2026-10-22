using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Spl;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;
using LibHac.Tools.Ncm;

namespace StormSwitchBox.Services
{
    /// <summary>
    /// Lossless Shrink Engine (Zero-Data-Loss Smart Size Optimization):
    /// Гарантирует минимальный размер выходных файлов (NSP, NSZ, XCI, XCZ, 3DS, CIA, Homebrew)
    /// без малейшей потери игровых данных, DLC, локализаций или совместимости.
    /// </summary>
    public static class SmartShrinkService
    {
        /// <summary>
        /// Проверяет, является ли файл валидным объектом внутри контейнера Switch (PFS0 / HFS0).
        /// Исключает дамперский мусор (.cnmt.xml, .nacp.xml, .jpg, .txt, .nfo, .sfv), который раздувает контейнер
        /// и может вызывать ошибки установки в DBI / Tinfoil.
        /// </summary>
        public static bool IsValidSwitchContainerEntry(string fileName, bool allowNcz = true)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return false;
            string lower = System.IO.Path.GetFileName(fileName).ToLowerInvariant();
            if (lower.EndsWith(".nca")) return true;
            if (allowNcz && lower.EndsWith(".ncz")) return true;
            if (lower.EndsWith(".tik")) return true;
            if (lower.EndsWith(".cert")) return true;
            return false;
        }

        /// <summary>
        /// Извлекает TitleKey из всех .tik билетов внутри PFS0/HFS0 и регистрирует их в KeySet.ExternalKeys.
        /// Это критически важно перед сжатием в NSZ/XCZ: без TitleKey шифрованные (Titlerights) NCA
        /// имеют максимальную энтропию AES-CTR и не сжимаются алгоритмом Zstandard.
        /// </summary>
        public static int HarvestTicketsFromFileSystem(IFileSystem pfs, KeySet? keySet)
        {
            if (keySet == null) return 0;
            int harvested = 0;

            foreach (var entry in pfs.EnumerateEntries("/", "*.tik"))
            {
                try
                {
                    using var tikFile = new UniqueRef<IFile>();
                    using var path = new LibHac.Fs.Path();
                    path.Initialize(new U8Span(Encoding.UTF8.GetBytes(entry.FullPath))).ThrowIfFailure();
                    if (pfs.OpenFile(ref tikFile.Ref, in path, OpenMode.Read).IsSuccess())
                    {
                        using var stream = tikFile.Release().AsStream();
                        byte[] tikBytes = new byte[stream.Length];
                        int read = stream.Read(tikBytes, 0, tikBytes.Length);
                        if (read >= 0x2B0 && TryRegisterTicketBytes(tikBytes, entry.Name, keySet))
                        {
                            harvested++;
                        }
                    }
                }
                catch { }
            }

            return harvested;
        }

        /// <summary>
        /// Извлекает TitleKey из всех .tik билетов в папке на диске и регистрирует их в KeySet.ExternalKeys.
        /// </summary>
        public static int HarvestTicketsFromDirectory(string directoryPath, KeySet? keySet)
        {
            if (keySet == null || !Directory.Exists(directoryPath)) return 0;
            int harvested = 0;

            foreach (var tikPath in Directory.GetFiles(directoryPath, "*.tik", SearchOption.AllDirectories))
            {
                try
                {
                    byte[] tikBytes = File.ReadAllBytes(tikPath);
                    if (tikBytes.Length >= 0x2B0 && TryRegisterTicketBytes(tikBytes, System.IO.Path.GetFileName(tikPath), keySet))
                    {
                        harvested++;
                    }
                }
                catch { }
            }

            return harvested;
        }

        private static bool TryRegisterTicketBytes(byte[] tikBytes, string fileName, KeySet keySet)
        {
            try
            {
                // Стандартный RSA-2048 Tik (сигнатура 0x00010004): TitleKey блок на 0x180, RightsId на 0x2A0
                int bodyOffset = 0x140;
                uint sigType = BitConverter.ToUInt32(tikBytes, 0);
                if (sigType == 0x00010003 || sigType == 0x00010000) bodyOffset = 0x240;
                else if (sigType == 0x00010004 || sigType == 0x00010001) bodyOffset = 0x140;
                else if (sigType == 0x00010005 || sigType == 0x00010002) bodyOffset = 0x80;

                int titleKeyOffset = bodyOffset + 0x40;
                int rightsIdOffset = bodyOffset + 0x160;
                if (tikBytes.Length < rightsIdOffset + 16) return false;

                byte[] ridBytes = tikBytes.AsSpan(rightsIdOffset, 16).ToArray();
                byte[] tkeyBytes = tikBytes.AsSpan(titleKeyOffset, 16).ToArray();

                // Если RightsId пустой в теле билета, пробуем взять из имени файла (32 hex символа)
                string cleanName = System.IO.Path.GetFileNameWithoutExtension(fileName);
                if (ridBytes.All(b => b == 0) && cleanName.Length == 32)
                {
                    ridBytes = Convert.FromHexString(cleanName);
                }

                if (!ridBytes.All(b => b == 0) && !tkeyBytes.All(b => b == 0))
                {
                    var rightsId = new RightsId(ridBytes);
                    var accessKey = new AccessKey(tkeyBytes);
                    keySet.ExternalKeySet.Add(rightsId, accessKey);

                    var decInfo = TicketHarvesterService.ExtractDecryptedTicket(tikBytes, tikBytes.Length, keySet);
                    if (decInfo.HasValue && !string.IsNullOrEmpty(decInfo.Value.RightsId) && decInfo.Value.TitleKey != null)
                    {
                        lock (Core.NSZ.StormNczCompressor.TitleKeysCache)
                        {
                            Core.NSZ.StormNczCompressor.TitleKeysCache[decInfo.Value.RightsId] = decInfo.Value.TitleKey;
                        }
                    }
                    return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Анализирует файловую систему контейнера (PFS0 / HFS0) и возвращает список избыточных файлов NCA/NCZ:
        /// 1. Все DeltaFragment (.nca и .ncz), если включен removeDelta.
        /// 2. Устаревшие (перекрытые более новым патчем) Update NCA, если в одном контейнере несколько Patch CNMT для одного TitleId.
        /// </summary>
        public static HashSet<string> AnalyzeRedundantEntriesInFileSystem(
            IFileSystem pfs,
            KeySet? keySet,
            bool removeDelta,
            out int deltaRemovedCount,
            out int supersededRemovedCount)
        {
            deltaRemovedCount = 0;
            supersededRemovedCount = 0;
            var toExclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (keySet == null) return toExclude;

            // Сначала подгружаем все билеты из этого же контейнера
            HarvestTicketsFromFileSystem(pfs, keySet);

            var patchCnmtByTitleId = new Dictionary<ulong, List<(uint Version, string CnmtFileName, List<string> ContentFiles)>>();

            foreach (var entry in pfs.EnumerateEntries("/", "*.cnmt.nca"))
            {
                try
                {
                    using var ncaFile = new UniqueRef<IFile>();
                    using var path = new LibHac.Fs.Path();
                    path.Initialize(new U8Span(Encoding.UTF8.GetBytes(entry.FullPath))).ThrowIfFailure();
                    if (!pfs.OpenFile(ref ncaFile.Ref, in path, OpenMode.Read).IsSuccess()) continue;

                    var nca = new Nca(keySet, ncaFile.Release().AsStorage());
                    if (nca.Header.ContentType != NcaContentType.Meta || !nca.CanOpenSection(0)) continue;

                    IFileSystem? cnmtFs = null;
                    try { cnmtFs = nca.OpenFileSystem(0, IntegrityCheckLevel.None); } catch { }
                    if (cnmtFs == null)
                    {
                        try { cnmtFs = nca.OpenFileSystem(0, IntegrityCheckLevel.IgnoreOnInvalid); } catch { }
                    }
                    if (cnmtFs == null) continue;

                    foreach (var cnmtEntry in cnmtFs.EnumerateEntries("/", "*.cnmt"))
                    {
                        using var cnmtFile = new UniqueRef<IFile>();
                        using var cPath = new LibHac.Fs.Path();
                        cPath.Initialize(new U8Span(Encoding.UTF8.GetBytes(cnmtEntry.FullPath))).ThrowIfFailure();
                        if (!cnmtFs.OpenFile(ref cnmtFile.Ref, in cPath, OpenMode.Read).IsSuccess()) continue;

                        var cnmt = new Cnmt(cnmtFile.Release().AsStream());
                        var contentFiles = new List<string>();

                        foreach (var contentEntry in cnmt.ContentEntries)
                        {
                            string hexId = BitConverter.ToString(contentEntry.NcaId).Replace("-", "").ToLowerInvariant();
                            if (contentEntry.Type == LibHac.Ncm.ContentType.DeltaFragment)
                            {
                                if (removeDelta)
                                {
                                    if (toExclude.Add(hexId + ".nca")) deltaRemovedCount++;
                                    toExclude.Add(hexId + ".ncz");
                                }
                            }
                            else
                            {
                                contentFiles.Add(hexId + ".nca");
                                contentFiles.Add(hexId + ".ncz");
                            }
                        }

                        if (cnmt.Type == ContentMetaType.Patch)
                        {
                            uint ver = cnmt.TitleVersion?.Version ?? 0;
                            if (!patchCnmtByTitleId.TryGetValue(cnmt.TitleId, out var list))
                            {
                                list = new List<(uint, string, List<string>)>();
                                patchCnmtByTitleId[cnmt.TitleId] = list;
                            }
                            list.Add((ver, entry.Name, contentFiles));
                        }
                    }
                }
                catch { }
            }

            // Если для одного TitleId найдено несколько Patch CNMT разных версий — оставляем только самую новую версию!
            foreach (var kvp in patchCnmtByTitleId)
            {
                if (kvp.Value.Count <= 1) continue;
                var ordered = kvp.Value.OrderByDescending(x => x.Version).ToList();
                var newest = ordered[0];
                var newestKeepSet = new HashSet<string>(newest.ContentFiles, StringComparer.OrdinalIgnoreCase)
                {
                    newest.CnmtFileName
                };

                for (int i = 1; i < ordered.Count; i++)
                {
                    var older = ordered[i];
                    if (!newestKeepSet.Contains(older.CnmtFileName) && toExclude.Add(older.CnmtFileName))
                    {
                        supersededRemovedCount++;
                    }
                    foreach (var cf in older.ContentFiles)
                    {
                        if (!newestKeepSet.Contains(cf) && toExclude.Add(cf))
                        {
                            if (cf.EndsWith(".nca", StringComparison.OrdinalIgnoreCase))
                                supersededRemovedCount++;
                        }
                    }
                }
            }

            return toExclude;
        }

        /// <summary>
        /// Анализирует папку с распакованными файлами NCA/NCZ перед упаковкой в NSP/XCI и возвращает список избыточных файлов.
        /// </summary>
        public static HashSet<string> AnalyzeRedundantEntriesInDirectory(
            string directoryPath,
            KeySet? keySet,
            bool removeDelta,
            out int deltaRemovedCount,
            out int supersededRemovedCount)
        {
            deltaRemovedCount = 0;
            supersededRemovedCount = 0;
            var toExclude = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!Directory.Exists(directoryPath) || keySet == null) return toExclude;

            HarvestTicketsFromDirectory(directoryPath, keySet);

            var patchCnmtByTitleId = new Dictionary<ulong, List<(uint Version, string CnmtFileName, List<string> ContentFiles)>>();

            foreach (var cnmtNcaPath in Directory.GetFiles(directoryPath, "*.cnmt.nca", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    using var fs = new FileStream(cnmtNcaPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    var nca = new Nca(keySet, fs.AsStorage());
                    if (nca.Header.ContentType != NcaContentType.Meta || !nca.CanOpenSection(0)) continue;

                    IFileSystem? cnmtFs = null;
                    try { cnmtFs = nca.OpenFileSystem(0, IntegrityCheckLevel.None); } catch { }
                    if (cnmtFs == null)
                    {
                        try { cnmtFs = nca.OpenFileSystem(0, IntegrityCheckLevel.IgnoreOnInvalid); } catch { }
                    }
                    if (cnmtFs == null) continue;

                    string cnmtFileName = System.IO.Path.GetFileName(cnmtNcaPath);

                    foreach (var cnmtEntry in cnmtFs.EnumerateEntries("/", "*.cnmt"))
                    {
                        using var cnmtFile = new UniqueRef<IFile>();
                        using var cPath = new LibHac.Fs.Path();
                        cPath.Initialize(new U8Span(Encoding.UTF8.GetBytes(cnmtEntry.FullPath))).ThrowIfFailure();
                        if (!cnmtFs.OpenFile(ref cnmtFile.Ref, in cPath, OpenMode.Read).IsSuccess()) continue;

                        var cnmt = new Cnmt(cnmtFile.Release().AsStream());
                        var contentFiles = new List<string>();

                        foreach (var contentEntry in cnmt.ContentEntries)
                        {
                            string hexId = BitConverter.ToString(contentEntry.NcaId).Replace("-", "").ToLowerInvariant();
                            if (contentEntry.Type == LibHac.Ncm.ContentType.DeltaFragment)
                            {
                                if (removeDelta)
                                {
                                    if (toExclude.Add(hexId + ".nca")) deltaRemovedCount++;
                                    toExclude.Add(hexId + ".ncz");
                                }
                            }
                            else
                            {
                                contentFiles.Add(hexId + ".nca");
                                contentFiles.Add(hexId + ".ncz");
                            }
                        }

                        if (cnmt.Type == ContentMetaType.Patch)
                        {
                            uint ver = cnmt.TitleVersion?.Version ?? 0;
                            if (!patchCnmtByTitleId.TryGetValue(cnmt.TitleId, out var list))
                            {
                                list = new List<(uint, string, List<string>)>();
                                patchCnmtByTitleId[cnmt.TitleId] = list;
                            }
                            list.Add((ver, cnmtFileName, contentFiles));
                        }
                    }
                }
                catch { }
            }

            foreach (var kvp in patchCnmtByTitleId)
            {
                if (kvp.Value.Count <= 1) continue;
                var ordered = kvp.Value.OrderByDescending(x => x.Version).ToList();
                var newest = ordered[0];
                var newestKeepSet = new HashSet<string>(newest.ContentFiles, StringComparer.OrdinalIgnoreCase)
                {
                    newest.CnmtFileName
                };

                for (int i = 1; i < ordered.Count; i++)
                {
                    var older = ordered[i];
                    if (!newestKeepSet.Contains(older.CnmtFileName) && toExclude.Add(older.CnmtFileName))
                    {
                        supersededRemovedCount++;
                    }
                    foreach (var cf in older.ContentFiles)
                    {
                        if (!newestKeepSet.Contains(cf) && toExclude.Add(cf))
                        {
                            if (cf.EndsWith(".nca", StringComparison.OrdinalIgnoreCase))
                                supersededRemovedCount++;
                        }
                    }
                }
            }

            return toExclude;
        }

        /// <summary>
        /// Оптимизирует иконку Switch (256x256 Baseline JPEG без EXIF/ICC-профилей, качество 92)
        /// для минимального размера Control NCA при 100% совместимости с меню Horizon OS.
        /// </summary>
        public static byte[] OptimizeSwitchIconJpeg(byte[] rawIconBytes)
        {
            if (rawIconBytes == null || rawIconBytes.Length == 0) return Array.Empty<byte>();

            try
            {
                using var inMs = new MemoryStream(rawIconBytes);
                using var srcImg = Image.FromStream(inMs);
                using var targetBmp = new Bitmap(256, 256, PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(targetBmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.Clear(Color.Black);
                    g.DrawImage(srcImg, new Rectangle(0, 0, 256, 256));
                }

                var jpegCodec = ImageCodecInfo.GetImageEncoders().FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);
                if (jpegCodec != null)
                {
                    using var encParams = new EncoderParameters(1);
                    encParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 92L);
                    using var outMs = new MemoryStream();
                    targetBmp.Save(outMs, jpegCodec, encParams);
                    byte[] optimized = outMs.ToArray();
                    // Если исходник уже был валидным JPEG 256x256 меньшего размера без EXIF — оставляем меньший
                    if (srcImg.Width == 256 && srcImg.Height == 256 &&
                        rawIconBytes.Length >= 2 && rawIconBytes[0] == 0xFF && rawIconBytes[1] == 0xD8 &&
                        rawIconBytes.Length < optimized.Length)
                    {
                        return rawIconBytes;
                    }
                    return optimized;
                }
            }
            catch { }

            return rawIconBytes;
        }

        /// <summary>
        /// Безопасно обрезает пустой хвост картриджа Nintendo 3DS (.3ds / .cci, заголовок NCSD)
        /// строго по границе последнего активного раздела NCCH, сохраняя 100% данных и экономя до 1.5 ГБ на роме.
        /// </summary>
        public static long Trim3dsCardImage(string filePath, Models.ProcessingTask? task = null)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath)) return 0;

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                if (fs.Length <= 0x4000) return 0;

                using var br = new BinaryReader(fs, Encoding.ASCII, leaveOpen: true);
                fs.Seek(0x100, SeekOrigin.Begin);
                byte[] magic = br.ReadBytes(4);
                if (Encoding.ASCII.GetString(magic) != "NCSD") return 0;

                // Считываем таблицу из 8 разделов NCSD по смещению 0x120 (каждый: uint offsetInMediaUnits, uint lengthInMediaUnits)
                // 1 Media Unit = 0x200 (512) байт
                fs.Seek(0x120, SeekOrigin.Begin);
                long maxValidEnd = 0x4000;
                for (int i = 0; i < 8; i++)
                {
                    uint partOffsetMu = br.ReadUInt32();
                    uint partLengthMu = br.ReadUInt32();
                    if (partOffsetMu > 0 && partLengthMu > 0)
                    {
                        long partEnd = (long)(partOffsetMu + partLengthMu) * 0x200L;
                        if (partEnd > maxValidEnd)
                        {
                            maxValidEnd = partEnd;
                        }
                    }
                }

                // Выравниваем по границе 0x200
                maxValidEnd = (maxValidEnd + 0x1FFL) & ~0x1FFL;

                if (maxValidEnd > 0x4000 && maxValidEnd < fs.Length)
                {
                    // Проверяем выборочно хвост файла, чтобы убедиться, что там только пустой паддинг картриджа (0xFF или 0x00)
                    long tailBytes = fs.Length - maxValidEnd;
                    if (tailBytes >= 0x200)
                    {
                        fs.Seek(maxValidEnd, SeekOrigin.Begin);
                        int checkLen = (int)Math.Min(4096, tailBytes);
                        byte[] sample = br.ReadBytes(checkLen);
                        bool isPadding = sample.All(b => b == 0xFF || b == 0x00);
                        if (isPadding)
                        {
                            fs.SetLength(maxValidEnd);
                            if (task != null)
                            {
                                App.RunOnUI(() => task.LogDetails += $"\n[SmartShrink 3DS] Обрезан пустой хвост картриджа: -{Models.ProcessingTask.FormatSize(tailBytes)}");
                            }
                            return tailBytes;
                        }
                    }
                }
            }
            catch { }

            return 0;
        }
    }
}
