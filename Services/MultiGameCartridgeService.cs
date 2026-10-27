using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using StormSwitchBox.Models;
using Path = System.IO.Path;

namespace StormSwitchBox.Services
{
    public class MultiCartridgeItem
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileName => Path.GetFileName(FilePath);
        public string TitleName { get; set; } = string.Empty;
        public string TitleId { get; set; } = string.Empty;
        public string ContentType { get; set; } = "Application";
        public string Version { get; set; } = "v0";
        public long SizeBytes { get; set; }
        public string SizeFormatted => ProcessingTask.FormatSize(SizeBytes);
    }

    /// <summary>
    /// Пакетный конструктор кастомных XCI/XCZ мульти-картриджей.
    /// Пакетный конструктор кастомных XCI/XCZ мульти-картриджей.
    /// Пакетный конструктор кастомных XCI/XCZ мульти-картриджей.
    /// </summary>
    public static class MultiGameCartridgeService
    {
        public static MultiCartridgeItem AnalyzeItem(string filePath)
        {
            var fi = new FileInfo(filePath);
            var item = new MultiCartridgeItem
            {
                FilePath = filePath,
                TitleName = Path.GetFileNameWithoutExtension(filePath),
                SizeBytes = fi.Exists ? fi.Length : 0
            };

            try
            {
                var info = App.SwitchFormat.ParseNsp(filePath);
                if (!string.IsNullOrEmpty(info.TitleId)) item.TitleId = info.TitleId.ToUpperInvariant();
                if (!string.IsNullOrEmpty(info.ContentType)) item.ContentType = info.ContentType;
                if (!string.IsNullOrEmpty(info.Version)) item.Version = "v" + info.Version;
                if (!string.IsNullOrEmpty(item.TitleId) && App.TitleDb != null)
                {
                    var dbEntry = App.TitleDb.GetEntry(item.TitleId);
                    if (dbEntry != null && !string.IsNullOrWhiteSpace(dbEntry.Name))
                    {
                        item.TitleName = dbEntry.Name;
                    }
                }
            }
            catch { }

            return item;
        }

        public static async Task<string> BuildMultiGameCartridgeAsync(
            List<string> inputFiles,
            string outputFilePath,
            bool compressToXcz,
            ProcessingTask? task,
            IProgress<(double Percent, string Status)>? progress,
            CancellationToken ct)
        {
            if (inputFiles == null || inputFiles.Count == 0)
                throw new ArgumentException("Не выбрано ни одного файла для сборки мульти-картриджа.");

            string tempDecompDir = Path.Combine(Path.GetTempPath(), $"StormMultiCart_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDecompDir);
            TempCleanupService.RegisterActiveTempDirectory(tempDecompDir);

            string targetXciPath = compressToXcz
                ? Path.ChangeExtension(outputFilePath, ".xci")
                : (outputFilePath.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) ? outputFilePath : Path.ChangeExtension(outputFilePath, ".xci"));

            var openedStreams = new List<FileStream>();
            var openedFiles = new List<IFile>();
            var openedFileSystems = new List<IFileSystem>();
            var keepAlive = new List<object>();

            try
            {
                var workingFiles = new List<string>();
                int idx = 0;

                foreach (var srcFile in inputFiles)
                {
                    ct.ThrowIfCancellationRequested();
                    idx++;
                    if (!File.Exists(srcFile)) continue;

                    string ext = Path.GetExtension(srcFile).ToLowerInvariant();
                    if (ext == ".nsz" || ext == ".xcz")
                    {
                        progress?.Report((idx * 25.0 / inputFiles.Count, $"Извлечение игровых разделов ({idx}/{inputFiles.Count}): {Path.GetFileName(srcFile)}..."));
                        var dummyTask = task ?? new ProcessingTask { OutputFileName = Path.GetFileNameWithoutExtension(srcFile) };
                        string? decomp = await App.NszCompression.DecompressNszAsync(dummyTask, srcFile, tempDecompDir, ct);
                        if (!string.IsNullOrEmpty(decomp) && File.Exists(decomp))
                        {
                            workingFiles.Add(decomp);
                        }
                    }
                    else if (ext == ".nsp" || ext == ".xci")
                    {
                        workingFiles.Add(srcFile);
                    }
                }

                if (workingFiles.Count == 0)
                    throw new InvalidOperationException("Не найдено ключей шифрования для сборки XCI.");

                progress?.Report((30, "Формирование единого HFS0 раздела /secure и дедупликация NCA..."));

                await Task.Run(() =>
                {
                    var secureBuilder = new PartitionFileSystemBuilder();
                    var addedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    bool removeDelta = App.Settings?.Current?.RemoveDeltaNca ?? true;
                    int totalEntriesAdded = 0;
                    int deltaSkipped = 0;

                    foreach (var containerPath in workingFiles)
                    {
                        ct.ThrowIfCancellationRequested();
                        var fs = new FileStream(containerPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                        openedStreams.Add(fs);
                        IStorage storage = fs.AsStorage();
                        keepAlive.Add(storage);

                        PartitionFileSystem pfs;
                        if (containerPath.EndsWith(".xci", StringComparison.OrdinalIgnoreCase))
                        {
                            storage.GetSize(out long sz).ThrowIfFailure();
                            var rootSub = new SubStorage(storage, 0x10000, sz - 0x10000);
                            keepAlive.Add(rootSub);
                            var rootPfs = new PartitionFileSystem(rootSub);
                            openedFileSystems.Add(rootPfs);

                            using var secRef = new UniqueRef<IFile>();
                            using var secPath = new LibHac.Fs.Path();
                            secPath.Initialize(new U8Span(Encoding.UTF8.GetBytes("/secure"))).ThrowIfFailure();
                            rootPfs.OpenFile(ref secRef.Ref, in secPath, OpenMode.Read).ThrowIfFailure();
                            IFile secFile = secRef.Release();
                            openedFiles.Add(secFile);
                            IStorage secStorage = secFile.AsStorage();
                            keepAlive.Add(secStorage);
                            pfs = new PartitionFileSystem(secStorage);
                        }
                        else
                        {
                            pfs = new PartitionFileSystem(storage);
                        }
                        openedFileSystems.Add(pfs);

                        SmartShrinkService.HarvestTicketsFromFileSystem(pfs, App.Keys.CurrentKeyset);
                        var redundant = SmartShrinkService.AnalyzeRedundantEntriesInFileSystem(
                            pfs, App.Keys.CurrentKeyset, removeDelta, out int dRem, out int sRem);
                        deltaSkipped += dRem + sRem;

                        foreach (var entry in pfs.EnumerateEntries("/", "*"))
                        {
                            if (!SmartShrinkService.IsValidSwitchContainerEntry(entry.Name, allowNcz: false))
                                continue;
                            if (redundant.Contains(entry.Name))
                                continue;
                            if (!addedNames.Add(entry.Name))
                                continue;

                            using var fRef = new UniqueRef<IFile>();
                            using var pth = new LibHac.Fs.Path();
                            pth.Initialize(new U8Span(Encoding.UTF8.GetBytes(entry.FullPath))).ThrowIfFailure();
                            pfs.OpenFile(ref fRef.Ref, in pth, OpenMode.Read).ThrowIfFailure();

                            IFile entryFile = fRef.Release();
                            openedFiles.Add(entryFile);
                            IStorage entryStorage = entryFile.AsStorage();
                            keepAlive.Add(entryStorage);

                            secureBuilder.AddFile(entry.Name, new StorageFile(new SafeStorageWrapper(entryStorage), OpenMode.Read));
                            totalEntriesAdded++;
                        }
                    }

                    if (totalEntriesAdded == 0)
                        throw new InvalidOperationException("В выбранных файлах не найдено валидных NCA разделов.");

                    // Build /normal, /update (empty HFS0) and /secure HFS0 for maximum hardware & emulator compatibility
                    var emptyNormalBuilder = new PartitionFileSystemBuilder();
                    using var normalHfs0 = emptyNormalBuilder.Build(PartitionFileSystemType.Hashed);
                    var emptyUpdateBuilder = new PartitionFileSystemBuilder();
                    using var updateHfs0 = emptyUpdateBuilder.Build(PartitionFileSystemType.Hashed);
                    using var secureHfs0 = secureBuilder.Build(PartitionFileSystemType.Hashed);

                    var rootBuilder = new PartitionFileSystemBuilder();
                    rootBuilder.AddFile("update", new StorageFile(new SafeStorageWrapper(updateHfs0), OpenMode.Read));
                    rootBuilder.AddFile("normal", new StorageFile(new SafeStorageWrapper(normalHfs0), OpenMode.Read));
                    rootBuilder.AddFile("secure", new StorageFile(new SafeStorageWrapper(secureHfs0), OpenMode.Read));

                    using var rootHfs0 = rootBuilder.Build(PartitionFileSystemType.Hashed);
                    rootHfs0.GetSize(out long rootSize).ThrowIfFailure();

                    long rootOffset = 0x10000;
                    byte[] hfs0HeaderPrefix = new byte[0x10];
                    rootHfs0.Read(0, hfs0HeaderPrefix).ThrowIfFailure();
                    int hfs0EntryCount = BitConverter.ToInt32(hfs0HeaderPrefix, 4);
                    int hfs0StringTableSize = BitConverter.ToInt32(hfs0HeaderPrefix, 8);
                    int hfs0HeaderSize = 0x10 + (0x40 * hfs0EntryCount) + hfs0StringTableSize;
                    int hfs0HeaderAligned = (hfs0HeaderSize + 0x1FF) & ~0x1FF;

                    byte[] rootHeader = new byte[hfs0HeaderAligned];
                    rootHfs0.Read(0, rootHeader).ThrowIfFailure();
                    byte[] rootHeaderHash = SHA256.HashData(rootHeader);

                    long totalXciSize = rootOffset + rootSize;
                    byte cartSizeByte = GetCartridgeCapacityByte(totalXciSize);

                    string? outDir = Path.GetDirectoryName(targetXciPath);
                    if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                        Directory.CreateDirectory(outDir);

                    using var outFs = new FileStream(targetXciPath, FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024 * 1024);

                    byte[] header = new byte[rootOffset];
                    Encoding.ASCII.GetBytes("HEAD").CopyTo(header, 0x100);
                    uint securePage = (uint)(rootOffset / 0x200);
                    Array.Copy(BitConverter.GetBytes(securePage), 0, header, 0x104, 4);
                    Array.Copy(BitConverter.GetBytes(0xFFFFFFFFu), 0, header, 0x108, 4);
                    header[0x10C] = 0x00;
                    header[0x10D] = cartSizeByte;
                    header[0x10E] = 0x00;
                    header[0x10F] = 0x00;
                    ulong validDataEnd = (ulong)((totalXciSize + 0x1FF) / 0x200);
                    Array.Copy(BitConverter.GetBytes(validDataEnd), 0, header, 0x118, 8);
                    Array.Copy(BitConverter.GetBytes((ulong)rootOffset), 0, header, 0x130, 8);
                    Array.Copy(BitConverter.GetBytes((ulong)rootSize), 0, header, 0x138, 8);
                    Array.Copy(rootHeaderHash, 0, header, 0x140, 32);
                    Array.Copy(BitConverter.GetBytes(securePage), 0, header, 0x18C, 4);

                    outFs.Write(header, 0, (int)rootOffset);

                    byte[] buffer = new byte[8 * 1024 * 1024];
                    long remaining = rootSize;
                    long offset = 0;
                    while (remaining > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        int toRead = (int)Math.Min(remaining, buffer.Length);
                        rootHfs0.Read(offset, buffer.AsSpan(0, toRead)).ThrowIfFailure();
                        outFs.Write(buffer, 0, toRead);
                        offset += toRead;
                        remaining -= toRead;

                        double pct = 30.0 + (offset * 55.0 / Math.Max(1, rootSize));
                        progress?.Report((pct, $"Сборка мульти-картриджа XCI: {ProcessingTask.FormatSize(offset)} / {ProcessingTask.FormatSize(rootSize)}"));
                    }
                }, ct);

                if (compressToXcz && File.Exists(targetXciPath))
                {
                    progress?.Report((88, "Сжатие мульти-картриджа в формат XCZ (Zstandard)..."));
                    string outFolder = Path.GetDirectoryName(outputFilePath) ?? "";
                    var dummyTask = task ?? new ProcessingTask { OutputFileName = Path.GetFileNameWithoutExtension(outputFilePath) };
                    await App.NszCompression.CompressToNszAsync(dummyTask, targetXciPath, outFolder, ct);

                    string finalXcz = Path.ChangeExtension(targetXciPath, ".xcz");
                    string genNsz = Path.ChangeExtension(targetXciPath, ".nsz");
                    if (File.Exists(genNsz))
                    {
                        if (File.Exists(finalXcz)) File.Delete(finalXcz);
                        File.Move(genNsz, finalXcz);
                    }
                    try { if (File.Exists(targetXciPath)) File.Delete(targetXciPath); } catch { }
                    progress?.Report((100, $"Готово: {Path.GetFileName(finalXcz)} ({ProcessingTask.FormatSize(new FileInfo(finalXcz).Length)})"));
                    return finalXcz;
                }

                progress?.Report((100, $"Готово: {Path.GetFileName(targetXciPath)} ({ProcessingTask.FormatSize(new FileInfo(targetXciPath).Length)})"));
                return targetXciPath;
            }
            finally
            {
                foreach (var f in openedFiles) { try { f.Dispose(); } catch { } }
                foreach (var fs in openedFileSystems) { try { fs.Dispose(); } catch { } }
                foreach (var s in openedStreams) { try { s.Dispose(); } catch { } }
                TempCleanupService.ForceDeleteDirectory(tempDecompDir);
            }
        }

        private static byte GetCartridgeCapacityByte(long sizeBytes)
        {
            const long GB = 1024L * 1024L * 1024L;
            if (sizeBytes <= 1 * GB) return 0xFA; // 1 GB
            if (sizeBytes <= 2 * GB) return 0xF8; // 2 GB
            if (sizeBytes <= 4 * GB) return 0xF0; // 4 GB
            if (sizeBytes <= 8 * GB) return 0xE0; // 8 GB
            if (sizeBytes <= 16 * GB) return 0xE1; // 16 GB
            return 0xE2; // 32 GB
        }
    }
}
