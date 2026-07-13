using System;
using System.IO;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;

namespace TestApp {
    public class Program {
        public static void Main() {
            try {
                string keysPath = @"E:\STORM SWITCH BOX\STORM_SWITCH_BOX+(1.1.000)\tools\prod.keys";
                if (!File.Exists(keysPath)) {
                    Console.WriteLine("prod.keys not found at: " + keysPath);
                    return;
                }
                var keySet = ExternalKeyReader.ReadKeyFile(keysPath);
                keySet.DeriveKeys();
                
                string nspPath = @"E:\STORM SWITCH BOX\STORM SWITCH BOX WINUE 3\StormSwitchBox\bin\x64\Debug\net8.0-windows10.0.19041.0\temp\temp_hardpatch_949f58\Underling Uprising [010057901E9E6000][v0] (1.06 GB).nsp";
                if (!File.Exists(nspPath)) {
                    Console.WriteLine("nsp file not found: " + nspPath);
                    return;
                }
                
                Console.WriteLine("Opening NSP: " + nspPath);
                using var fs = new FileStream(nspPath, FileMode.Open, FileAccess.Read);
                var pfs = new PartitionFileSystem(fs.AsStorage());
                
                foreach (var entry in pfs.EnumerateEntries()) {
                    Console.WriteLine($"Entry: {entry.Name}, Size: {entry.Size}");
                    if (entry.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase)) {
                        Console.WriteLine("Found NCA entry. Trying to parse...");
                        
                        using var fileRef = new LibHac.Common.UniqueRef<IFile>();
                        using var entryPath = new LibHac.Fs.Path();
                        entryPath.Initialize(System.Text.Encoding.UTF8.GetBytes(entry.FullPath)).ThrowIfFailure();
                        pfs.OpenFile(ref fileRef.Ref, in entryPath, OpenMode.Read).ThrowIfFailure();
                        
                        IStorage ncaStorage = fileRef.Get.AsStorage();
                        try {
                            var nca = new Nca(keySet, ncaStorage);
                            Console.WriteLine($"NCA TitleId: {nca.Header.TitleId:X16}, ContentType: {nca.Header.ContentType}");
                            
                            if (nca.Header.ContentType == NcaContentType.Program) {
                                Console.WriteLine("Attempting to open Code section (ExeFS)...");
                                try {
                                    var exefs = nca.OpenFileSystem(NcaSectionType.Code, IntegrityCheckLevel.None);
                                    if (exefs != null) {
                                        Console.WriteLine("SUCCESS: Opened ExeFS!");
                                        foreach (var subEntry in exefs.EnumerateEntries("/", "*")) {
                                            Console.WriteLine($"  File in ExeFS: {subEntry.FullPath}, Size: {subEntry.Size}");
                                        }
                                    } else {
                                        Console.WriteLine("Failed: OpenFileSystem returned null");
                                    }
                                } catch (Exception ex) {
                                    Console.WriteLine("Failed to open Code section: " + ex);
                                }
                                
                                Console.WriteLine("Attempting to open Data section (RomFS)...");
                                try {
                                    var romfs = nca.OpenFileSystem(NcaSectionType.Data, IntegrityCheckLevel.None);
                                    if (romfs != null) {
                                        Console.WriteLine("SUCCESS: Opened RomFS!");
                                    } else {
                                        Console.WriteLine("Failed: OpenFileSystem returned null");
                                    }
                                } catch (Exception ex) {
                                    Console.WriteLine("Failed to open Data section: " + ex);
                                }
                            }
                        } catch (Exception ex) {
                            Console.WriteLine("Error parsing NCA: " + ex.Message);
                        }
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine("Error: " + ex);
            }
        }
    }
}
