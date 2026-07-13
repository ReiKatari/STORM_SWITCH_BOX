using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
    /// <summary>
    ///    NSP > NSZ / XCI > XCZ.
    ///  StormNczCompressor (ZstdSharp)   nsz.exe.
    /// </summary>
    public class NszCompressionService
    {
        private readonly SwitchFormatService _formatService;

        public NszCompressionService(SwitchFormatService formatService)
        {
            _formatService = formatService;
        }

        /// <summary>
        ///    NSP/XCI > NSZ/XCZ    Zstandard.
        ///  .nca     .ncz,    as-is.
        ///    PFS0-   .nsz.
        /// </summary>
        public async Task CompressToNszAsync(ProcessingTask task, string inputPath, string outDir, CancellationToken cancellationToken)
        {
            FileStream? fileStream = null;
            var tempStreams = new List<FileStream>();
            var openedFiles = new List<IFile>();
            string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "StormCompress_" + Guid.NewGuid().ToString("N").Substring(0, 8));

            try
            {
                App.MainDispatcher?.TryEnqueue(() =>
                {
                    task.Status = "...";
                    task.IsRunning = true;
                    task.Progress = 0;
                });

                string fileName = System.IO.Path.GetFileNameWithoutExtension(inputPath);
                bool isXci = inputPath.EndsWith(".xci", StringComparison.OrdinalIgnoreCase);
                string expectedExt = isXci ? ".xcz" : ".nsz";
                string outNszPath = System.IO.Path.Combine(outDir, fileName + expectedExt);

                App.Logger.Log($"[NSZ Engine]    NSZ: {fileName}", LogLevel.Info);

                long totalBytes = new FileInfo(inputPath).Length;

                App.MainDispatcher?.TryEnqueue(() =>
                {
                    // Update log details without overwriting initial SourceSizeBytes
                    if (task.SourceSizeBytes <= 0) task.SourceSizeBytes = totalBytes;
                    task.LogDetails = $"Загрузка: {System.IO.Path.GetFileName(inputPath)}\nРазмер: {Models.ProcessingTask.FormatSize(totalBytes)}\nЗапуск Zstd...";
                    task.Status = "Сжатие NSZ...";
                });

                System.IO.Directory.CreateDirectory(tempDir);

                fileStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                IStorage storage = fileStream.AsStorage();

                var pfs = CreatePfsFromStorage(storage, isXci);
                var pfsBuilder = new PartitionFileSystemBuilder();

                int level = App.Settings.Current.CompressionLevel;
                if (level < 1) level = 18;
                if (level > 22) level = 22;

                var entries = pfs.EnumerateEntries().ToList();
                int totalEntries = entries.Count;
                int entryIdx = 0;

                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    entryIdx++;

                    if (entry.Type == DirectoryEntryType.Directory) continue;

                    string entryName = entry.Name;
                    bool isNca = entryName.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) &&
                                 !entryName.EndsWith(".cnmt.nca", StringComparison.OrdinalIgnoreCase);

                    bool shouldCompress = false;
                    if (isNca)
                    {
                        try
                        {
                            using (var entryFile = OpenFileSafe(pfs, entry.FullPath))
                            {
                                IStorage entryStorage = entryFile.AsStorage();
                                var nca = new Nca(App.Keys.CurrentKeyset, entryStorage);
                                if (nca.Header.ContentType != NcaContentType.Control && nca.Header.ContentType != NcaContentType.Meta)
                                {
                                    shouldCompress = true;
                                }
                                else
                                {
                                    App.Logger.Log($"[NSZ Engine]    NCA  {nca.Header.ContentType}: {entryName}", LogLevel.Info);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            App.Logger.Log($"[NSZ Engine]    NCA   {entryName}: {ex.Message}. NCA    .", LogLevel.Warning);
                            shouldCompress = true;
                        }
                    }

                    if (isNca && shouldCompress)
                    {
                        string nczName = System.IO.Path.ChangeExtension(entryName, ".ncz");
                        string tempNczPath = System.IO.Path.Combine(tempDir, nczName);

                        App.MainDispatcher?.TryEnqueue(() =>
                        {
                            task.Status = $" {entryName}...";
                            task.LogDetails += $"\n[{entryIdx}/{totalEntries}]  {entryName} -> {nczName}";
                        });

                        using (var entryFile = OpenFileSafe(pfs, entry.FullPath))
                        {
                            IStorage entryStorage = entryFile.AsStorage();
                            Core.NSZ.StormNczCompressor.CompressNcaToNcz(
                                entryStorage,
                                tempNczPath,
                                level,
                                App.Keys.CurrentKeyset,
                                task,
                                cancellationToken);
                        }

                        var fs = new FileStream(tempNczPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                        tempStreams.Add(fs);
                        pfsBuilder.AddFile(nczName, new StorageFile(new SafeStorageWrapper(fs.AsStorage()), LibHac.Fs.OpenMode.Read));
                    }
                    else
                    {
                        App.MainDispatcher?.TryEnqueue(() =>
                        {
                            task.LogDetails += $"\n[{entryIdx}/{totalEntries}]  : {entryName}";
                        });

                        var entryFile = OpenFileSafe(pfs, entry.FullPath);
                        openedFiles.Add(entryFile);
                        IStorage physicalStorage = entryFile.AsStorage();
                        pfsBuilder.AddFile(entryName, new StorageFile(new SafeStorageWrapper(physicalStorage), LibHac.Fs.OpenMode.Read));
                    }
                }

                //    ,   
                if (File.Exists(outNszPath))
                {
                    try { File.Delete(outNszPath); } catch { }
                }

                App.MainDispatcher?.TryEnqueue(() =>
                {
                    task.Status = " ...";
                    task.LogDetails += "\n    ...";
                });

                using (var builtPfs = pfsBuilder.Build(PartitionFileSystemType.Standard))
                {
                    using (var destStream = new FileStream(outNszPath, FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024 * 1024))
                    {
                        builtPfs.GetSize(out long totalPfsSize).ThrowIfFailure();
                        long remaining = totalPfsSize;
                        long offset = 0;
                        byte[] buffer = new byte[128 * 1024];
                        System.Diagnostics.Stopwatch uiSw = System.Diagnostics.Stopwatch.StartNew();

                        while (remaining > 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            int toRead = (int)Math.Min(buffer.Length, remaining);
                            builtPfs.Read(offset, buffer.AsSpan(0, toRead)).ThrowIfFailure();
                            destStream.Write(buffer, 0, toRead);
                            offset += toRead;
                            remaining -= toRead;

                            if (uiSw.ElapsedMilliseconds > 200 || remaining == 0)
                            {
                                uiSw.Restart();
                                double packProgress = 99.0 + ((double)offset / totalPfsSize * 1.0);
                                App.MainDispatcher?.TryEnqueue(() => task.Progress = Math.Min(99.9, packProgress));
                            }
                        }
                    }
                }

                long finalSize = new FileInfo(outNszPath).Length;
                double ratio = (double)finalSize / totalBytes * 100.0;

                App.MainDispatcher?.TryEnqueue(() =>
                {
                    task.Progress = 100;
                    task.Status = "";
                    task.IsRunning = false;
                    task.LogDetails += $"\n  !\n : {Models.ProcessingTask.FormatSize(finalSize)} ({ratio:F1}%  )";
                    task.TargetSize = Models.ProcessingTask.FormatSize(finalSize);

                    long diff = totalBytes - finalSize;
                    double percent = (double)diff / totalBytes * 100.0;
                    task.SizeDifference = $"{(diff > 0 ? "-" : "+")}{Models.ProcessingTask.FormatSize(Math.Abs(diff))} ({Math.Abs(percent):F1}%)";

                    StormSwitchBox.Services.HistoryService.AddToHistory(task);
                });

                App.Logger.Log($"[NSZ Engine]  : {fileName}. : {100 - ratio:F1}%", LogLevel.Success);
            }
            catch (OperationCanceledException)
            {
                App.MainDispatcher?.TryEnqueue(() => { task.Status = ""; task.IsRunning = false; StormSwitchBox.Services.HistoryService.AddToHistory(task); });
            }
            catch (Exception ex)
            {
                App.MainDispatcher?.TryEnqueue(() =>
                {
                    task.Status = "";
                    task.IsRunning = false;
                    task.LogDetails += $"\n  : {ex.Message}";
                    StormSwitchBox.Services.HistoryService.AddToHistory(task);
                });
                App.Logger.Log($"[NSZ Engine]   : {ex.Message}", LogLevel.Error);
                throw;
            }
            finally
            {
                if (fileStream != null)
                {
                    try { fileStream.Dispose(); } catch { }
                }
                foreach (var fs in tempStreams)
                {
                    try { fs.Dispose(); } catch { }
                }
                foreach (var f in openedFiles)
                {
                    try { f.Dispose(); } catch { }
                }
                try
                {
                    if (Directory.Exists(tempDir))
                    {
                        Directory.Delete(tempDir, true);
                    }
                }
                catch { }
            }
        }




        /// <summary>
        ///    NSZ/XCZ > NSP/XCI   StormNczStorage.
        ///   nsz.exe -D
        /// </summary>
        public async Task<string?> DecompressNszAsync(ProcessingTask task, string inputPath, string outDir, CancellationToken cancellationToken)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(inputPath);
            string expectedExt = inputPath.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase) ? ".xci" : ".nsp";
            string outNspPath = System.IO.Path.Combine(outDir, fileName + expectedExt);

            var openedFiles = new List<IFile>();
            try
            {
                App.MainDispatcher?.TryEnqueue(() =>
                {
                    task.Status = "...";
                    task.IsRunning = true;
                    task.LogDetails += $"\n  NSZ   (Zero-Disk-IO): {System.IO.Path.GetFileName(inputPath)}";
                });

                App.Logger.Log($"[NSZ Engine]  : {fileName}", LogLevel.Info);

                using var fileStream = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                IStorage storage = fileStream.AsStorage();
                long pfsOffset = expectedExt == ".xci" ? 0x10000 : 0;
                
                IStorage pfsStorage = pfsOffset > 0 ? new SubStorage(storage, pfsOffset, storage.GetSize(out long sz).IsSuccess() ? sz - pfsOffset : 0) : storage;
                var fileSystem = new PartitionFileSystem(pfsStorage);

                var pfsBuilder = new PartitionFileSystemBuilder();
                int entryIdx = 0;

                List<string> solidFiles = new List<string>();
                List<string> physicalFiles = new List<string>();

                var sortedEntries = fileSystem.EnumerateEntries().ToList();



                bool IsNczMagic(IStorage fileStorage)
                {
                    fileStorage.GetSize(out long size).ThrowIfFailure();
                    if (size < 8) {
                        DebugLogger.Log($"[IsNczMagic] Size {size} < 8. Returning false.");
                        return false;
                    }
                    try
                    {
                        byte[] magicBuf = new byte[8];
                        fileStorage.Read(0, magicBuf);
                        string m1 = System.Text.Encoding.ASCII.GetString(magicBuf);
                        DebugLogger.Log($"[IsNczMagic] Size: {size}, m1: {m1}");
                        if (m1 == "NCZSECTN" || m1 == "NCZBLOCK") return true;

                        if (size >= 0x4008)
                        {
                            fileStorage.Read(0x4000, magicBuf);
                            string m2 = System.Text.Encoding.ASCII.GetString(magicBuf);
                            DebugLogger.Log($"[IsNczMagic] Size: {size}, m2: {m2}");
                            if (m2 == "NCZSECTN" || m2 == "NCZBLOCK") return true;
                        }
                        
                        return false;
                    }
                    catch (Exception ex) { 
                        DebugLogger.Log($"[IsNczMagic] Exception: {ex.Message}");
                        return false; 
                    }
                }

                foreach (var entry in sortedEntries)
                {
                    bool isMetadata = !entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase) && 
                                      !entry.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase);

                    if (isMetadata)
                    {
                        physicalFiles.Add(entry.Name);
                        continue;
                    }

                    using var entryFile = OpenFileSafe(fileSystem, entry.FullPath);
                    IStorage entryStorage = entryFile.AsStorage();

                    //  .nca/.ncz     
                    bool isSolidBlob = false;
                    try {
                        entryStorage.GetSize(out long sz2).ThrowIfFailure();
                        if (sz2 >= 8) {
                            byte[] mb = new byte[8];
                            entryStorage.Read(0, mb);
                            if (System.Text.Encoding.ASCII.GetString(mb) == "NCZSECTN") isSolidBlob = true;
                        }
                    } catch { }
                    if (isSolidBlob) { continue; }
                    bool isVirtualOrBlock = IsNczMagic(entryStorage) || entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase);
                    
                    if (isVirtualOrBlock)
                    {
                        solidFiles.Add(entry.Name);
                    }
                    else
                    {
                        physicalFiles.Add(entry.Name);
                    }
                }

                var titleKeyMap = new Dictionary<string, byte[]>();
                foreach (var entry in sortedEntries)
                {
                    if (entry.Name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            using var entryFile = OpenFileSafe(fileSystem, entry.FullPath);
                            IStorage tikStorage = entryFile.AsStorage();
                            tikStorage.GetSize(out long tikSize).ThrowIfFailure();
                            byte[] tikData = new byte[tikSize];
                            tikStorage.Read(0, tikData).ThrowIfFailure();
                            using var stream = new MemoryStream(tikData);
                            var ticket = new LibHac.Tools.Es.Ticket(stream);
                            byte[] tKey = ticket.GetTitleKey(App.Keys.CurrentKeyset);
                            string rightsIdStr = BitConverter.ToString(ticket.RightsId).Replace("-", "").ToLowerInvariant();
                            titleKeyMap[rightsIdStr] = tKey;
                            App.MainDispatcher?.TryEnqueue(() => task.LogDetails += $"\n  TitleKey (Zero-Disk-IO)  {rightsIdStr}");

                            try
                            {
                                string titleKeysDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch");
                                if (!Directory.Exists(titleKeysDir)) Directory.CreateDirectory(titleKeysDir);
                                string titleKeysPath = System.IO.Path.Combine(titleKeysDir, "title.keys");
                                
                                byte[] contentKey = ticket.GetTitleKey(App.Keys.CurrentKeyset);
                                int masterKeyRev = Math.Max(0, ticket.RightsId[15] - 1);
                                byte[] titleKek = App.Keys.CurrentKeyset.TitleKeks[masterKeyRev].DataRo.ToArray();
                                byte[] encryptedKey = new byte[16];
                                using (var aes = System.Security.Cryptography.Aes.Create())
                                {
                                    aes.Mode = System.Security.Cryptography.CipherMode.ECB;
                                    aes.Padding = System.Security.Cryptography.PaddingMode.None;
                                    aes.Key = titleKek;
                                    using (var encryptor = aes.CreateEncryptor())
                                    {
                                        encryptor.TransformBlock(contentKey, 0, 16, encryptedKey, 0);
                                    }
                                }
                                string titleKeyBlockHex = BitConverter.ToString(encryptedKey).Replace("-", "").ToLowerInvariant();

                                string keyLine = $"{rightsIdStr} = {titleKeyBlockHex}";
                                bool keyExists = false;
                                if (File.Exists(titleKeysPath))
                                {
                                    var lines = File.ReadAllLines(titleKeysPath);
                                    foreach (var line in lines)
                                    {
                                        if (line.Split('=')[0].Trim().ToLowerInvariant() == rightsIdStr)
                                        {
                                            keyExists = true;
                                            break;
                                        }
                                    }
                                }
                                if (!keyExists)
                                {
                                    File.AppendAllLines(titleKeysPath, new string[] { keyLine });
                                    App.MainDispatcher?.TryEnqueue(() => task.LogDetails += $"\n[INFO]  TitleKey  title.keys  .");
                                }
                            }
                            catch (Exception ex)
                            {
                                App.Logger.Log($"[NSZ Engine]   TitleKey  : {ex.Message}", LogLevel.Warning);
                            }
                        }
                        catch (Exception ex) 
                        {
                            App.MainDispatcher?.TryEnqueue(() => task.LogDetails += $"\n[]     {entry.Name}: {ex.Message}");
                        }
                    }
                }

                IStorage? globalSolidStorage = null;
                var solidEntry = sortedEntries.FirstOrDefault(e => e.Name.EndsWith(".solid", StringComparison.OrdinalIgnoreCase));
                if (solidEntry != null)
                {
                    var solidFile = OpenFileSafe(fileSystem, solidEntry.FullPath);
                    openedFiles.Add(solidFile);
                    globalSolidStorage = solidFile.AsStorage();
                }

                await Task.Run(() =>
                {
                    foreach (var entry in sortedEntries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        entryIdx++;
                        string entryName = entry.Name;

                        int currentEntry = entryIdx;
                        int totalEntries = sortedEntries.Count;
                        App.MainDispatcher?.TryEnqueue(() =>
                            task.LogDetails += $"\n[{currentEntry}/{totalEntries}]   : {entryName}");

                        if (solidFiles.Contains(entryName))
                        {
                            string ncaName = entryName.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase) 
                                ? System.IO.Path.ChangeExtension(entryName, ".nca") 
                                : entryName;
                            
                            var entryFile = OpenFileSafe(fileSystem, entry.FullPath);
                            openedFiles.Add(entryFile);
                            IStorage entryStorage = entryFile.AsStorage();
                            var decStorage = new Core.NSZ.StormNczStorage(entryStorage, titleKeyMap, globalSolidStorage, App.Keys.CurrentKeyset);
                             
                            pfsBuilder.AddFile(ncaName, new StorageFile(new SafeStorageWrapper(decStorage), LibHac.Fs.OpenMode.Read));
                        }
                        else
                        {
                            //     
                            var entryFile = OpenFileSafe(fileSystem, entry.FullPath);
                            openedFiles.Add(entryFile);
                            IStorage physicalStorage = entryFile.AsStorage();
                            pfsBuilder.AddFile(entryName, new StorageFile(new SafeStorageWrapper(physicalStorage), LibHac.Fs.OpenMode.Read));
                        }
                    }

                    App.MainDispatcher?.TryEnqueue(() => task.LogDetails += "\n   (  )...");
                    using var builtPfs = pfsBuilder.Build(PartitionFileSystemType.Standard);
                    
                    using var destStream = new FileStream(outNspPath, FileMode.Create, FileAccess.Write, FileShare.None, 16 * 1024 * 1024);
                    
                    //  : LibHac StorageStream.Read      , 
                    //    Stream.CopyTo    (EOF).
                    builtPfs.GetSize(out long totalPfsSize).ThrowIfFailure();
                    long remaining = totalPfsSize;
                    long offset = 0;
                    byte[] buffer = new byte[81920];
                    System.Diagnostics.Stopwatch uiSw = System.Diagnostics.Stopwatch.StartNew();
                    while (remaining > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        int toRead = (int)Math.Min(buffer.Length, remaining);
                        builtPfs.Read(offset, buffer.AsSpan(0, toRead)).ThrowIfFailure();
                        destStream.Write(buffer, 0, toRead);
                        offset += toRead;
                        remaining -= toRead;
                        if (uiSw.ElapsedMilliseconds > 200 || remaining == 0)
                        {
                            uiSw.Restart();
                            double packProgress = 99.0 + ((double)offset / totalPfsSize * 1.0);
                            App.MainDispatcher?.TryEnqueue(() => task.Progress = Math.Min(99.9, packProgress));
                        }
                    }
                }, cancellationToken);

                App.Logger.Log($"[NSZ Engine]   : {outNspPath}", LogLevel.Success);
                if (globalSolidStorage != null)
                {
                    try { ((IDisposable)globalSolidStorage).Dispose(); } catch { }
                }
                return outNspPath;
            }
            catch (OperationCanceledException)
            {
                try { if (System.IO.File.Exists(outNspPath)) System.IO.File.Delete(outNspPath); } catch { }
                return null;
            }
            catch (Exception ex)
            {
                App.Logger.Log($"[NSZ Engine]   :\n{ex.ToString()}", LogLevel.Error);
                try { if (System.IO.File.Exists(outNspPath)) System.IO.File.Delete(outNspPath); } catch { }
                return null;
            }
            finally
            {
                foreach (var f in openedFiles)
                {
                    try { f.Dispose(); } catch { }
                }
            }
        }

        private PartitionFileSystem CreatePfsFromStorage(IStorage storage, bool isXci)
        {
            if (isXci)
            {
                storage.GetSize(out long storageSize).ThrowIfFailure();
                var rootStorage = new SubStorage(storage, 0x10000, storageSize - 0x10000);
                var rootPfs = new PartitionFileSystem(rootStorage);
                
                
                using var secureFile = new UniqueRef<IFile>();
                using var securePath = new LibHac.Fs.Path();
                securePath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes("/secure"))).ThrowIfFailure();
                rootPfs.OpenFile(ref secureFile.Ref, in securePath, OpenMode.Read).ThrowIfFailure();

                var pfs = new PartitionFileSystem(secureFile.Release().AsStorage());
                
                return pfs;
            }
            else
            {
                var pfs = new PartitionFileSystem(storage);
                
                return pfs;
            }
        }

        private static async Task<bool> HardPatchInternalAsync(string filePath, byte[] patchData)
        {
            return await Task.FromResult(true);
        }

        private static IFile OpenFileSafe(IFileSystem fsToOpen, string pth)
        {
            using var fRef = new UniqueRef<IFile>();
            using var path = new LibHac.Fs.Path();
            path.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(pth))).ThrowIfFailure();
            fsToOpen.OpenFile(ref fRef.Ref, in path, LibHac.Fs.OpenMode.Read).ThrowIfFailure();
            return fRef.Release();
        }
    }

    /// <summary>
    ///   IStorage       .
    ///      ,   (PartitionFileSystemBuilder)      .
    /// </summary>
    public class SafeStorageWrapper : IStorage
    {
        private readonly IStorage _baseStorage;
        public SafeStorageWrapper(IStorage baseStorage)
        {
            _baseStorage = baseStorage;
        }

        public override Result Read(long offset, Span<byte> destination)
        {
            try
            {
                long size = 0;
                var res = _baseStorage.GetSize(out size);
                if (res.IsSuccess())
                {
                    if (offset >= size)
                    {
                        destination.Fill(0);
                        return Result.Success;
                    }
                    if (offset + destination.Length > size)
                    {
                        int allowed = (int)(size - offset);
                        var subDest = destination.Slice(0, allowed);
                        var readRes = _baseStorage.Read(offset, subDest);
                        if (readRes.IsFailure()) return readRes;
                        destination.Slice(allowed).Fill(0);
                        return Result.Success;
                    }
                }
                return _baseStorage.Read(offset, destination);
            }
            catch (Exception ex)
            {
                long size = 0;
                try { _baseStorage.GetSize(out size); } catch { }
                if (offset >= size)
                {
                    destination.Fill(0);
                    return Result.Success;
                }
                App.Logger.Log($"[SafeStorageWrapper]    ( {offset},  {size}): {ex.Message}", Models.LogLevel.Error);
                return LibHac.Fs.ResultFs.OutOfRange.Log();
            }
        }

        public override Result Write(long offset, ReadOnlySpan<byte> source)
        {
            return _baseStorage.Write(offset, source);
        }

        public override Result Flush()
        {
            return _baseStorage.Flush();
        }

        public override Result SetSize(long size)
        {
            return _baseStorage.SetSize(size);
        }

        public override Result GetSize(out long size)
        {
            return _baseStorage.GetSize(out size);
        }

        public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
        {
            return _baseStorage.OperateRange(outBuffer, operationId, offset, size, inBuffer);
        }
    }
}
