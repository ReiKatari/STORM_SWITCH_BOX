using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tools.Fs;
using LibHac.Tools.FsSystem;
using Path = System.IO.Path;

namespace StormSwitchBox.Services
{
    public class TicketHarvesterService
    {
        private readonly HashSet<string> _processedFiles = new();
        private readonly object _lockObj = new();

        public void HarvestTicketsBackground(IEnumerable<string> filePaths)
        {
            var filesToProcess = filePaths.Where(f => !Directory.Exists(f) && (f.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase))).ToList();

            if (filesToProcess.Count == 0) return;

            foreach (var file in filesToProcess)
            {
                lock (_lockObj)
                {
                    if (_processedFiles.Contains(file)) continue;
                    _processedFiles.Add(file);
                }

                try
                {
                    HarvestFromFile(file);
                }
                catch (Exception ex)
                {
                    App.Logger.Log($"[Ticket Harvester] Ошибка извлечения билетов из {Path.GetFileName(file)}: {ex.Message}", Models.LogLevel.Warning);
                }
            }
        }

        private void HarvestFromFile(string filePath)
        {
            string titleKeysPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".switch", "title.keys");

            bool isXci = filePath.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) || filePath.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase);

            using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            IStorage storage = fileStream.AsStorage();
            
            PartitionFileSystem? pfs = null;

            if (isXci)
            {
                storage.GetSize(out long storageSize).ThrowIfFailure();
                var rootStorage = new SubStorage(storage, 0x10000, storageSize - 0x10000);
                var rootPfs = new PartitionFileSystem(rootStorage);
                
                
                using var secureFile = new UniqueRef<IFile>();
                using var securePath = new LibHac.Fs.Path();
                securePath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes("/secure"))).ThrowIfFailure();
                rootPfs.OpenFile(ref secureFile.Ref, in securePath, OpenMode.Read).ThrowIfFailure();
                
                pfs = new PartitionFileSystem(secureFile.Release().AsStorage());
                
            }
            else
            {
                pfs = new PartitionFileSystem(storage);
                
            }

            var entries = pfs.EnumerateEntries().Where(e => e.Name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase)).ToList();
            
            if (entries.Count > 0)
            {
                var keysToAdd = new List<string>();

                foreach (var entry in entries)
                {
                    using var tikFileRef = new UniqueRef<IFile>();
                    using var entryPath = new LibHac.Fs.Path();
                    entryPath.Initialize(new U8Span(System.Text.Encoding.UTF8.GetBytes(entry.FullPath))).ThrowIfFailure();
                    pfs.OpenFile(ref tikFileRef.Ref, in entryPath, OpenMode.Read).ThrowIfFailure();
                    
                    IFile tikFile = tikFileRef.Release();
                    byte[] tikBytes = new byte[0x300];
                    tikFile.Read(out long bytesRead, 0, tikBytes).ThrowIfFailure();

                    if (bytesRead >= 0x2B0)
                    {
                        // Парсинг TitleKey (0x180, 16 байт) и RightsId (0x2A0, 16 байт)
                        byte[] titleKeyBytes = new byte[16];
                        Array.Copy(tikBytes, 0x180, titleKeyBytes, 0, 16);
                        
                        byte[] rightsIdBytes = new byte[16];
                        Array.Copy(tikBytes, 0x2A0, rightsIdBytes, 0, 16);

                        string titleKey = BitConverter.ToString(titleKeyBytes).Replace("-", "").ToLower();
                        string rightsId = BitConverter.ToString(rightsIdBytes).Replace("-", "").ToLower();

                        keysToAdd.Add($"{rightsId} = {titleKey}");
                    }
                }

                if (keysToAdd.Count > 0)
                {
                    lock (_lockObj)
                    {
                        var existingKeys = new HashSet<string>();
                        try 
                        {
                            if (File.Exists(titleKeysPath))
                            {
                                var lines = File.ReadAllLines(titleKeysPath);
                                foreach(var l in lines) existingKeys.Add(l.Split('=')[0].Trim().ToLower());
                            }
                            else
                            {
                                Directory.CreateDirectory(Path.GetDirectoryName(titleKeysPath)!);
                            }
                        } 
                        catch { }

                        var newKeys = keysToAdd.Where(k => !existingKeys.Contains(k.Split('=')[0].Trim().ToLower())).ToList();
                        
                        if (newKeys.Count > 0)
                        {
                            File.AppendAllLines(titleKeysPath, newKeys);
                            App.Logger.Log($"[Ticket Harvester] Найдено и добавлено {newKeys.Count} новых билетов из {Path.GetFileName(filePath)}", Models.LogLevel.Success);
                            App.MainDispatcher?.TryEnqueue(() =>
                            {
                                if (App.Keys.KeysFilePath != null && File.Exists(App.Keys.KeysFilePath))
                                {
                                    App.Keys.LoadKeys(App.Keys.KeysFilePath);
                                    App.Logger.Log("[Ticket Harvester] Ключи LibHac были автоматически обновлены.", Models.LogLevel.Info);
                                }
                            });
                        }
                    }
                }
            }
        }
    }
}
