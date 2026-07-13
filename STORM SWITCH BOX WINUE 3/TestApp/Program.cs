using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using StormSwitchBox;
using StormSwitchBox.Models;
using StormSwitchBox.Services;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;
using LibHac.Tools.FsSystem.NcaUtils;

namespace TestApp
{
    public class Program
    {
        public static async Task Main()
        {
            Console.WriteLine("=== Starting StormSwitchBox Rebuild ===");
            try
            {
                // Perform automatic replacement of UI dispatcher calls to headless-friendly RunOnUI
                try
                {
                    string[] sourceFiles = new[]
                    {
                        @"E:\STORM SWITCH BOX\STORM SWITCH BOX WINUE 3\StormSwitchBox\Services\HardPatchEngine.cs",
                        @"E:\STORM SWITCH BOX\STORM SWITCH BOX WINUE 3\StormSwitchBox\Services\NativePackEngine.cs",
                        @"E:\STORM SWITCH BOX\STORM SWITCH BOX WINUE 3\StormSwitchBox\Services\MultiContentService.cs"
                    };
                    foreach (var sf in sourceFiles)
                    {
                        if (File.Exists(sf))
                        {
                            string content = File.ReadAllText(sf);
                            string replaced = content.Replace("App.MainDispatcher?.TryEnqueue(", "App.RunOnUI(");
                            if (replaced != content)
                            {
                                File.WriteAllText(sf, replaced);
                                Console.WriteLine($"[Replacer] Updated dispatcher calls in {System.IO.Path.GetFileName(sf)}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Replacer] Error: {ex.Message}");
                }

                string keysPath = @"E:\STORM SWITCH BOX\STORM_SWITCH_BOX+(1.1.000)\tools\prod.keys";
                if (!File.Exists(keysPath))
                {
                    Console.WriteLine($"Error: Keys file not found at {keysPath}");
                    return;
                }

                // Initialize App Services
                App.Settings.Current.KeysPath = keysPath;
                App.Keys.LoadKeys(keysPath);
                Console.WriteLine("Keys loaded successfully.");

                string baseNsp = @"P:\CONSOLES\Nintendo Switch\DOWNLOADS\Devil Jam\[WW] [RUS] (1.0.1 - 65536 - 0100C6A0235D4000) (1G+1U)\Devil Jam [0100C6A0235D4000][v0] (0.45 GB).nsz";
                string updateNsp = @"P:\CONSOLES\Nintendo Switch\DOWNLOADS\Devil Jam\[WW] [RUS] (1.0.1 - 65536 - 0100C6A0235D4000) (1G+1U)\Devil Jam [0100C6A0235D4800][v65536] (0.19 GB).nsz";
                string outPath = @"P:\CONSOLES\Nintendo Switch\DOWNLOADS\Devil Jam\[WW] [RUS] (1.0.1 - 65536 - 0100C6A0235D4000) (1G+1U)\Devil Jam [0100C6A0235D4000] (Hybrid).nsp";
                var inputFiles = new List<string> { baseNsp, updateNsp };

                var task = new ProcessingTask
                {
                    Id = "hardpatch_job",
                    IsRunning = true,
                    Status = "Инициализация"
                };

                // Print task property changes
                task.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(ProcessingTask.LogDetails))
                    {
                        var logs = task.LogDetails.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        if (logs.Length > 0)
                        {
                            Console.WriteLine($"[LOG] {logs.Last()}");
                        }
                    }
                    else if (e.PropertyName == nameof(ProcessingTask.Status))
                    {
                        Console.WriteLine($"[STATUS] {task.Status}");
                    }
                    else if (e.PropertyName == nameof(ProcessingTask.Progress))
                    {
                        Console.WriteLine($"[PROGRESS] {task.Progress:F1}%");
                    }
                };

                // InspectNsp disabled - recursive reflection on KeySet causes infinite hang
                // Console.WriteLine("Inspecting Base NSP...");
                // InspectNsp(baseNsp);
                // Console.WriteLine("Inspecting Update NSP...");
                // InspectNsp(updateNsp);
                Console.WriteLine("\nRebuilding Hybrid Game...");
                await App.HardPatch.PatchUpdateAsync(task, inputFiles, outPath, CancellationToken.None);
                Console.WriteLine("Rebuild finished.");
                Console.WriteLine($"Task Status: {task.Status}");
                Console.WriteLine("=== Task Logs ===");
                Console.WriteLine(task.LogDetails);
                Console.WriteLine("=================");
                if (task.Status == "Ошибка")
                {
                    throw new Exception("Rebuild failed internally. See logs above.");
                }
                // Console.WriteLine("\nInspecting Hybrid NSZ...");
                // InspectNsp(outPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled exception in Main: {ex}");
            }
        }

        private static void RunReflectionDiagnostic(string baseNsp)
        {
            Console.WriteLine("--- LibHac Reflection Diagnostic ---");
            using var baseFs = new FileStream(baseNsp, FileMode.Open, FileAccess.Read);
            var pfs = new PartitionFileSystem(baseFs.AsStorage());
            foreach (var entry in pfs.EnumerateEntries())
            {
                if (entry.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase))
                {
                    using var fileRef = new LibHac.Common.UniqueRef<LibHac.Fs.Fsa.IFile>();
                    using var entryPath = new LibHac.Fs.Path();
                    entryPath.Initialize(System.Text.Encoding.UTF8.GetBytes(entry.FullPath)).ThrowIfFailure();
                    pfs.OpenFile(ref fileRef.Ref, in entryPath, OpenMode.Read).ThrowIfFailure();
                    var nca = new LibHac.Tools.FsSystem.NcaUtils.Nca(App.Keys.CurrentKeyset, fileRef.Get.AsStorage());
                    if (nca.Header.ContentType == LibHac.Tools.FsSystem.NcaUtils.NcaContentType.Control)
                    {
                        var secStorage = nca.OpenStorage(0, IntegrityCheckLevel.None);
                        Console.WriteLine("Successfully opened Section 0 storage.");
                        DumpStorageFields(secStorage);
                        break;
                    }
                }
            }
            Console.WriteLine("--- End of LibHac Reflection Diagnostic ---");
        }

        private static void TestTicketDecryption(string baseNspPath)
        {
            try
            {
                Console.WriteLine("\n--- Testing LibHac Ticket Decryption on Base Game ---");
                using (var baseFs = new FileStream(baseNspPath, FileMode.Open, FileAccess.Read))
                {
                    var pfs = new PartitionFileSystem(baseFs.AsStorage());
                    foreach (var entry in pfs.EnumerateEntries())
                    {
                        if (entry.Name.EndsWith(".tik", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"Found ticket: {entry.Name}");
                            using var fileRef = new LibHac.Common.UniqueRef<LibHac.Fs.Fsa.IFile>();
                            using var entryPath = new LibHac.Fs.Path();
                            entryPath.Initialize(System.Text.Encoding.UTF8.GetBytes(entry.FullPath)).ThrowIfFailure();
                            pfs.OpenFile(ref fileRef.Ref, in entryPath, OpenMode.Read).ThrowIfFailure();
                            
                            using var ticketStream = fileRef.Get.AsStream();
                            var ticket = new LibHac.Tools.Es.Ticket(ticketStream);
                            Console.WriteLine($"  RightsId: {BitConverter.ToString(ticket.RightsId).Replace("-", "")}");
                            Console.WriteLine($"  TitleKeyBlock: {BitConverter.ToString(ticket.TitleKeyBlock).Replace("-", "")}");
                            Console.WriteLine($"  TitleKeyType: {ticket.TitleKeyType}");
                            Console.WriteLine($"  CryptoType: {ticket.CryptoType}");
                            byte[] decryptedTitleKey = ticket.GetTitleKey(App.Keys.CurrentKeyset);
                            Console.WriteLine($"  Decrypted Title Key: {BitConverter.ToString(decryptedTitleKey).Replace("-", "")}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TestTicketDecryption: {ex.Message}");
            }
        }

        private static void TestTitleKeks(LibHac.Common.Keys.KeySet keySet, byte[] encTitleKey)
        {
            try
            {
                for (int idx = 0; idx < keySet.TitleKeks.Length; idx++)
                {
                    var tk = keySet.TitleKeks[idx];
                    bool allZeros = true;
                    foreach (var b in (ReadOnlySpan<byte>)tk) { if (b != 0) allZeros = false; }
                    if (allZeros) continue;

                    using var aes = System.Security.Cryptography.Aes.Create();
                    aes.Mode = System.Security.Cryptography.CipherMode.ECB;
                    aes.Padding = System.Security.Cryptography.PaddingMode.None;
                    aes.Key = ((ReadOnlySpan<byte>)tk).ToArray();
                    using var decryptor = aes.CreateDecryptor();
                    byte[] decTitleKey = decryptor.TransformFinalBlock(encTitleKey, 0, 16);
                    Console.WriteLine($"Index {idx} Decrypted Title Key: {BitConverter.ToString(decTitleKey).Replace("-", "")}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TestTitleKeks: {ex.Message}");
            }
        }

        private static void InspectNsp(string nspPath)
        {
            try
            {
                using var nspFs = new FileStream(nspPath, FileMode.Open, FileAccess.Read);
                var pfs = new PartitionFileSystem(nspFs.AsStorage());

                foreach (var entry in pfs.EnumerateEntries())
                {
                    if (entry.Name.EndsWith(".nca", StringComparison.OrdinalIgnoreCase) || entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase))
                    {
                        using var fileRef = new LibHac.Common.UniqueRef<LibHac.Fs.Fsa.IFile>();
                        using var entryPath = new LibHac.Fs.Path();
                        entryPath.Initialize(System.Text.Encoding.UTF8.GetBytes(entry.FullPath)).ThrowIfFailure();
                        pfs.OpenFile(ref fileRef.Ref, in entryPath, OpenMode.Read).ThrowIfFailure();
                        
                        var ncaStorage = fileRef.Get.AsStorage();
                        using var nczStorage = entry.Name.EndsWith(".ncz", StringComparison.OrdinalIgnoreCase)
                            ? new StormSwitchBox.Core.NSZ.StormNczStorage(ncaStorage, null, null, App.Keys.CurrentKeyset)
                            : null;
                        var nca = new Nca(App.Keys.CurrentKeyset, nczStorage ?? ncaStorage);
                        Console.WriteLine($"\n  NCA Entry: {entry.Name}");
                        Console.WriteLine($"    ContentType: {nca.Header.ContentType}");
                        Console.WriteLine($"    FormatVersion: {nca.Header.FormatVersion}");
                        
                        Console.WriteLine("    [Header Fields] (Reflection skipped for speed)");

                        
                        byte[] rawHeader = new byte[0xC00];
                        ncaStorage.Read(0, rawHeader).ThrowIfFailure();

                        // Decrypt header
                        byte[] decHeader = new byte[0xC00];
                        byte[] hKey = new byte[32];
                        ((ReadOnlySpan<byte>)App.Keys.CurrentKeyset.HeaderKey).CopyTo(hKey);
                        byte[] key1 = new byte[16];
                        byte[] key2 = new byte[16];
                        Array.Copy(hKey, 0, key1, 0, 16);
                        Array.Copy(hKey, 16, key2, 0, 16);
                        Array.Copy(rawHeader, 0, decHeader, 0, 0x200);
                        var transform = new LibHac.Tools.FsSystem.Aes128XtsTransform(key1, key2, true);
                        int encryptedLength = 0xC00 - 0x200;
                        Array.Copy(rawHeader, 0x200, decHeader, 0x200, encryptedLength);
                        for (int s = 0; s < encryptedLength / 0x200; s++)
                        {
                            transform.TransformBlock(decHeader, 0x200 + s * 0x200, 0x200, (ulong)(1 + s));
                        }
                        Console.WriteLine("    [Key Area Decryption Diagnostic]");
                        byte[] keyArea = new byte[64];
                        Array.Copy(decHeader, 0x300, keyArea, 0, 64);
                        Console.WriteLine($"      Raw Key Area: {BitConverter.ToString(keyArea).Replace("-", "")}");
                        
                        byte[] testTitleKey1 = new byte[] { 0x27, 0x72, 0x32, 0x7b, 0x46, 0xba, 0xad, 0x10, 0xb9, 0x3d, 0x27, 0x01, 0x65, 0x72, 0x02, 0xcd }; // Index 21
                        byte[] testTitleKey2 = new byte[] { 0x55, 0xe3, 0xcf, 0x2b, 0x47, 0xb8, 0xf9, 0x90, 0x58, 0x68, 0x6b, 0x44, 0x3d, 0xb7, 0x14, 0x8f }; // Index 20
                        byte[] testTitleKey3 = new byte[] { 0x46, 0x89, 0x35, 0xfa, 0xc3, 0xaa, 0x15, 0xa3, 0x91, 0x6f, 0x71, 0x8f, 0xbb, 0x2d, 0x79, 0x91 }; // Raw encrypted key
                        byte[] testTitleKey4 = new byte[] { 0x2d, 0xca, 0x22, 0x0f, 0x5e, 0xe9, 0x2f, 0x25, 0xc7, 0x68, 0xad, 0x02, 0xec, 0x94, 0x3f, 0xad }; // Decrypted content key (2DCA...)
                        
                        Action<byte[], string> TestDecryptKeyArea = (key, name) =>
                        {
                            try
                            {
                                using var aes = System.Security.Cryptography.Aes.Create();
                                aes.Mode = System.Security.Cryptography.CipherMode.ECB;
                                aes.Padding = System.Security.Cryptography.PaddingMode.None;
                                aes.Key = key;
                                using var decryptor = aes.CreateDecryptor();
                                byte[] decrypted = decryptor.TransformFinalBlock(keyArea, 0, 64);
                                Console.WriteLine($"      Decrypted with {name}: {BitConverter.ToString(decrypted).Replace("-", "")}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"      Decrypted with {name} failed: {ex.Message}");
                            }
                        };
                        
                        TestDecryptKeyArea(testTitleKey1, "TitleKey1 (Index 21)");
                        TestDecryptKeyArea(testTitleKey2, "TitleKey2 (Index 20)");
                        TestDecryptKeyArea(testTitleKey3, "TitleKey3 (Raw/Enc)");
                        TestDecryptKeyArea(testTitleKey4, "TitleKey4 (2DCA...)");

                        // Dump details for each partition section
                        for (int i = 0; i < 4; i++)
                        {
                            try
                            {
                                long startBlock = BitConverter.ToUInt32(decHeader, 0x240 + (i * 0x10));
                                long endBlock = BitConverter.ToUInt32(decHeader, 0x240 + (i * 0x10) + 8);
                                if (startBlock == 0 && endBlock == 0) continue;

                                long secOffset = startBlock * 0x200;
                                long secSize = (endBlock - startBlock) * 0x200;
                                Console.WriteLine($"    Section {i}: StartBlock={startBlock} (Offset=0x{secOffset:X}), EndBlock={endBlock} (Size=0x{secSize:X})");
                            
                            // Parse FsHeader
                            int fsHeaderOffset = 0x400 + (i * 0x200);
                            byte[] fsHeader = new byte[0x200];
                            Array.Copy(decHeader, fsHeaderOffset, fsHeader, 0, 0x200);
                            
                            int fsType = fsHeader[0x02];
                            int hashType = fsHeader[0x03];
                            int encType = fsHeader[0x04];
                            byte[] generation = new byte[8];
                            Array.Copy(fsHeader, 0x140, generation, 0, 8);

                            Console.WriteLine($"      FsHeader: FsType={fsType}, HashType={hashType}, EncryptionType={encType}");
                            Console.WriteLine($"      Generation: {BitConverter.ToString(generation).Replace("-", "")}");

                            if (hashType == 2) // HierarchicalSha256
                            {
                                int hashBlockSize = BitConverter.ToInt32(fsHeader, 0x28);
                                int hashLayerCount = BitConverter.ToInt32(fsHeader, 0x2C);
                                long level0Offset = BitConverter.ToInt64(fsHeader, 0x30);
                                long level0Size = BitConverter.ToInt64(fsHeader, 0x38);
                                long level1Offset = BitConverter.ToInt64(fsHeader, 0x40);
                                long level1Size = BitConverter.ToInt64(fsHeader, 0x48);
                                Console.WriteLine($"      HierarchicalSha256 Info:");
                                Console.WriteLine($"        BlockSize: 0x{hashBlockSize:X}");
                                Console.WriteLine($"        LayerCount: {hashLayerCount}");
                                Console.WriteLine($"        Level 0 (Hash Table): Offset=0x{level0Offset:X}, Size=0x{level0Size:X}");
                                Console.WriteLine($"        Level 1 (Data): Offset=0x{level1Offset:X}, Size=0x{level1Size:X}");
                                
                                // Read ciphertext of PFS0 start (Level 1 Offset)
                                byte[] cipherBytes = new byte[32];
                                ncaStorage.Read(secOffset + level1Offset, cipherBytes).ThrowIfFailure();
                                Console.WriteLine($"        Ciphertext of PFS0 Start: {BitConverter.ToString(cipherBytes).Replace("-", "")}");

                                // Try to read using LibHac's decrypted reader
                                try
                                {
                                    var secStorage = nca.OpenStorage(i, IntegrityCheckLevel.None);
                                    Console.WriteLine("        [Storage Fields Debug]");
                                    DumpStorageFields(secStorage);
                                    
                                    byte[] plainBytes0 = new byte[32];
                                    secStorage.Read(0, plainBytes0).ThrowIfFailure();
                                    Console.WriteLine($"        Decrypted Plaintext at offset 0: {BitConverter.ToString(plainBytes0).Replace("-", "")} (ASCII: '{System.Text.Encoding.ASCII.GetString(plainBytes0, 0, 4).Replace("\0", "\\0")}')");

                                    byte[] plainBytes1 = new byte[32];
                                    secStorage.Read(level1Offset, plainBytes1).ThrowIfFailure();
                                    Console.WriteLine($"        Decrypted Plaintext at level1Offset (0x{level1Offset:X}): {BitConverter.ToString(plainBytes1).Replace("-", "")} (ASCII: '{System.Text.Encoding.ASCII.GetString(plainBytes1, 0, 4).Replace("\0", "\\0")}')");

                                     // Print encrypted keys from Header
                                     for (int k = 0; k < 4; k++)
                                     {
                                         try
                                         {
                                             var encKey = nca.Header.GetEncryptedKey(k).ToArray();
                                             Console.WriteLine($"        Header.GetEncryptedKey({k}): {BitConverter.ToString(encKey).Replace("-", "")}");
                                         }
                                         catch (Exception ex)
                                         {
                                             Console.WriteLine($"        Header.GetEncryptedKey({k}) failed: {ex.Message}");
                                         }
                                     }

                                     // Print decrypted keys
                                     byte[][] decKeys = new byte[4][];
                                    for (int k = 0; k < 4; k++)
                                    {
                                        try
                                        {
                                            decKeys[k] = nca.GetDecryptedKey(k).ToArray();
                                            Console.WriteLine($"        Decrypted Key {k}: {BitConverter.ToString(decKeys[k]).Replace("-", "")}");
                                        }
                                        catch (Exception ex)
                                        {
                                            Console.WriteLine($"        Decrypted Key {k} failed: {ex.Message}");
                                        }
                                    }

                                    try
                                    {
                                        var decTitleKey = nca.GetDecryptedTitleKey();
                                        Console.WriteLine($"        GetDecryptedTitleKey(): {BitConverter.ToString(decTitleKey).Replace("-", "")}");
                                    }
                                    catch (Exception ex) { Console.WriteLine($"        GetDecryptedTitleKey() failed: {ex.Message}"); }

                                     var ncaKeyType = typeof(LibHac.Tools.FsSystem.NcaUtils.Nca).Assembly.GetType("LibHac.Tools.FsSystem.NcaUtils.NcaKeyType");
                                     if (ncaKeyType != null)
                                     {
                                         var getContentKeyMethod = typeof(LibHac.Tools.FsSystem.NcaUtils.Nca).GetMethod("GetContentKey", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                                         if (getContentKeyMethod != null)
                                            {
                                             foreach (var name in Enum.GetNames(ncaKeyType))
                                             {
                                                 try
                                                 {
                                                     var keyTypeVal = Enum.Parse(ncaKeyType, name);
                                                     var contentKeyVal = (byte[])getContentKeyMethod.Invoke(nca, new object[] { keyTypeVal });
                                                     Console.WriteLine($"        GetContentKey({name}): {BitConverter.ToString(contentKeyVal).Replace("-", "")}");
                                                 }
                                                 catch (Exception ex) { Console.WriteLine($"        GetContentKey({name}) failed: {ex.Message}"); }
                                             }
                                         }
                                         else
                                         {
                                             Console.WriteLine("        GetContentKey method not found via reflection.");
                                         }
                                     }
                                     else
                                     {
                                         Console.WriteLine("        NcaKeyType enum not found via reflection.");
                                     }

                                    // Compute keystream from ciphertext and plain bytes
                                    byte[] ks = new byte[16];
                                    for (int b = 0; b < 16; b++)
                                    {
                                        ks[b] = (byte)(cipherBytes[b] ^ plainBytes1[b]);
                                    }
                                    Console.WriteLine($"        Actual Keystream at level1Offset: {BitConverter.ToString(ks).Replace("-", "")}");

                                    // Decrypt keystream using each available key to find the IV used
                                    for (int k = 0; k < 4; k++)
                                    {
                                        if (decKeys[k] != null)
                                        {
                                            try
                                            {
                                                using var aes = System.Security.Cryptography.Aes.Create();
                                                aes.Mode = System.Security.Cryptography.CipherMode.ECB;
                                                aes.Padding = System.Security.Cryptography.PaddingMode.None;
                                                aes.Key = decKeys[k];
                                                using var decryptor = aes.CreateDecryptor();
                                                byte[] decryptedIv = decryptor.TransformFinalBlock(ks, 0, 16);
                                                Console.WriteLine($"        Decrypted IV with Key {k}: {BitConverter.ToString(decryptedIv).Replace("-", "")}");
                                            }
                                            catch (Exception ex)
                                            {
                                                Console.WriteLine($"        Decrypted IV with Key {k} failed: {ex.Message}");
                                            }
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    Console.WriteLine($"        Failed to read decrypted PFS0: {e.Message}");
                                }
                            }
                            else if (hashType == 3) // HierarchicalIntegrity (RomFS)
                            {
                                Console.WriteLine($"      HierarchicalIntegrity levels:");
                                for (int j = 0; j < 6; j++)
                                {
                                    int lOffset = 0x18 + j * 0x18;
                                    long lvlOff = BitConverter.ToInt64(fsHeader, lOffset);
                                    long lvlSize = BitConverter.ToInt64(fsHeader, lOffset + 8);
                                    Console.WriteLine($"        Level {j}: Offset=0x{lvlOff:X}, Size=0x{lvlSize:X}");
                                }
                            }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"    Error inspecting section {i}: {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Error inspecting NSP: {ex.Message}");
            }
        }

        private static void DumpStorageFields(object? obj, string indent = "      ")
        {
            if (obj == null) return;
            var type = obj.GetType();
            Console.WriteLine($"{indent}Type: {type.FullName}");
            
            var currentType = type;
            while (currentType != null && currentType != typeof(object))
            {
                foreach (var field in currentType.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly))
                {
                    try
                    {
                        var val = field.GetValue(obj);
                        if (val == null) continue;
                        
                        string fieldName = currentType == type ? field.Name : $"{field.Name} (base {currentType.Name})";
                        
                        if (val is byte[] bytes)
                        {
                            Console.WriteLine($"{indent}  {fieldName}: {BitConverter.ToString(bytes).Replace("-", "")}");
                        }
                        else if (val.GetType().IsPrimitive || val is string || val is decimal)
                        {
                            Console.WriteLine($"{indent}  {fieldName}: {val}");
                        }
                        else if (val is Array arr)
                        {
                            Console.WriteLine($"{indent}  {fieldName} (Array of {val.GetType().GetElementType()?.Name}):");
                            for (int k = 0; k < Math.Min(arr.Length, 4); k++)
                            {
                                var elem = arr.GetValue(k);
                                if (elem != null && !elem.GetType().IsPrimitive && !(elem is string))
                                {
                                    DumpStorageFields(elem, indent + "    ");
                                }
                            }
                        }
                        else
                        {
                            string ns = val.GetType().Namespace ?? "";
                            if (!ns.StartsWith("System") && !ns.StartsWith("Microsoft"))
                            {
                                Console.WriteLine($"{indent}  {fieldName} ->");
                                DumpStorageFields(val, indent + "    ");
                            }
                        }
                    }
                    catch {}
                }
                currentType = currentType.BaseType;
            }
        }

        private static void FindDecryptingKey(LibHac.Common.Keys.KeySet keySet, byte[] encTitleKey, byte[] targetDecKey)
        {
            Console.WriteLine("\n--- Searching for the key that decrypts TitleKey to target ---");
            var allKeys = new List<(string Name, byte[] Key)>();
            
            // Gather keys from fields and properties
            foreach (var prop in typeof(LibHac.Common.Keys.KeySet).GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
            {
                try
                {
                    var val = prop.GetValue(keySet);
                    ExtractKeysFromObject(val, prop.Name, allKeys);
                }
                catch {}
            }
            foreach (var field in typeof(LibHac.Common.Keys.KeySet).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
            {
                try
                {
                    var val = field.GetValue(keySet);
                    ExtractKeysFromObject(val, field.Name, allKeys);
                }
                catch {}
            }

            // Try decrypting with each key
            foreach (var item in allKeys)
            {
                if (item.Key.Length == 16)
                {
                    try
                    {
                        using var aes = System.Security.Cryptography.Aes.Create();
                        aes.Mode = System.Security.Cryptography.CipherMode.ECB;
                        aes.Padding = System.Security.Cryptography.PaddingMode.None;
                        aes.Key = item.Key;
                        using var decryptor = aes.CreateDecryptor();
                        byte[] decrypted = decryptor.TransformFinalBlock(encTitleKey, 0, 16);
                        
                        bool match = true;
                        for (int i = 0; i < 16; i++)
                        {
                            if (decrypted[i] != targetDecKey[i]) match = false;
                        }
                        if (match)
                        {
                            Console.WriteLine($"[MATCH FOUND!] Key name: {item.Name}, Key bytes: {BitConverter.ToString(item.Key).Replace("-", "")}");
                        }
                    }
                    catch {}
                }
            }
            Console.WriteLine("--- Search finished ---\n");
        }

        private static void ExtractKeysFromObject(object? val, string name, List<(string, byte[])> list)
        {
            if (val == null) return;
            var type = val.GetType();
            if (val is byte[] bytes)
            {
                list.Add((name, bytes));
            }
            else if (type.Name.Contains("AesKey") || type.Name.Contains("AesXtsKey") || type.Name.Contains("RsaKey"))
            {
                try
                {
                    // Try to convert to Span or read fields
                    var toArrayMethod = type.GetMethod("ToArray");
                    if (toArrayMethod != null)
                    {
                        var arr = toArrayMethod.Invoke(val, null) as byte[];
                        if (arr != null) list.Add((name, arr));
                    }
                }
                catch {}
            }
            else if (val is Array arr)
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    ExtractKeysFromObject(arr.GetValue(i), $"{name}[{i}]", list);
                }
            }
            else if (type.Namespace != null && (type.Namespace.StartsWith("LibHac") || type.Namespace.StartsWith("System")))
            {
                // If it is a Span or other container
                try
                {
                    var lengthProp = type.GetProperty("Length");
                    var getItemMethod = type.GetMethod("get_Item");
                    if (lengthProp != null && getItemMethod != null)
                    {
                        int len = (int)lengthProp.GetValue(val)!;
                        for (int i = 0; i < len; i++)
                        {
                            var elem = getItemMethod.Invoke(val, new object[] { i });
                            ExtractKeysFromObject(elem, $"{name}[{i}]", list);
                        }
                    }
                }
                catch {}
            }
        }

        public static void DumpMethodCalls(System.Reflection.MethodInfo? method)
        {
            if (method == null) return;
            var body = method.GetMethodBody();
            if (body == null) return;
            var il = body.GetILAsByteArray();
            if (il == null) return;
            var module = method.Module;
            Console.WriteLine($"\n--- Scanning calls in {method.DeclaringType?.Name}.{method.Name} ---");
            for (int i = 0; i < il.Length - 4; i++)
            {
                byte op = il[i];
                if (op == 0x28 || op == 0x6f || op == 0x73) // call, callvirt, newobj
                {
                    int token = BitConverter.ToInt32(il, i + 1);
                    try
                    {
                        var calledMethod = module.ResolveMethod(token);
                        Console.WriteLine($"  IL_{i:X4}: Calls {calledMethod.DeclaringType?.FullName}.{calledMethod.Name}");
                    }
                    catch {}
                }
            }
        }
    }
}
