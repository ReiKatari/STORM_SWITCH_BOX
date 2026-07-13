using System;
using System.IO;
using System.Reflection;
using LibHac.Common.Keys;
using LibHac.Tools.FsSystem;

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
                
                string ncaPath = @"E:\STORM SWITCH BOX\STORM SWITCH BOX WINUE 3\StormSwitchBox\bin\x64\Debug\net8.0-windows10.0.19041.0\temp\hardpatch_64e29d\basedata\4f29895cb549bbea7b0d3ff3e941b907.nca";
                if (!File.Exists(ncaPath)) {
                    Console.WriteLine("nca file not found: " + ncaPath);
                    return;
                }
                
                byte[] headerEnc = new byte[0xC00];
                using (var fs = new FileStream(ncaPath, FileMode.Open, FileAccess.Read)) {
                    fs.Read(headerEnc, 0, 0xC00);
                }
                
                Console.WriteLine("Encrypted bytes [0x200..0x210]: " + BitConverter.ToString(headerEnc, 0x200, 16));
                
                byte[] headerKey = new byte[32];
                ((ReadOnlySpan<byte>)keySet.HeaderKey).CopyTo(headerKey);
                
                byte[] key1 = new byte[16];
                byte[] key2 = new byte[16];
                Array.Copy(headerKey, 0, key1, 0, 16);
                Array.Copy(headerKey, 16, key2, 0, 16);
                
                // Let's test various sector parameters for XTS
                for (int startSector = 0; startSector <= 2; startSector++) {
                    for (int useOffset = 0; useOffset <= 1; useOffset++) {
                        // Let's try decryption
                        byte[] dst = new byte[0xC00];
                        Array.Copy(headerEnc, dst, 0xC00);
                        
                        var transform = new LibHac.Tools.FsSystem.Aes128XtsTransform(key1, key2, true);
                        int encryptedLength = 0xC00 - 0x200;
                        int sectorSize = 0x200;
                        
                        for (int i = 0; i < encryptedLength / sectorSize; i++) {
                            // Sector index formula:
                            ulong sectorIndex = 0;
                            if (useOffset == 0) {
                                sectorIndex = (ulong)(startSector + i);
                            } else {
                                sectorIndex = (ulong)(startSector + 1 + i);
                            }
                            
                            transform.TransformBlock(dst, 0x200 + i * sectorSize, sectorSize, sectorIndex);
                        }
                        
                        string magic = System.Text.Encoding.ASCII.GetString(dst, 0x200, 4);
                        string magicHex = BitConverter.ToString(dst, 0x200, 4);
                        Console.WriteLine($"startSector={startSector}, useOffset={useOffset} => Magic: '{magic}' ({magicHex})");
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine("Error: " + ex);
            }
        }
    }
}
