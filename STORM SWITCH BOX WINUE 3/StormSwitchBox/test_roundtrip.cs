using System;
using System.IO;
using System.Security.Cryptography;
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
                
                byte[] headerEncOriginal = new byte[0xC00];
                using (var fs = new FileStream(ncaPath, FileMode.Open, FileAccess.Read)) {
                    fs.Read(headerEncOriginal, 0, 0xC00);
                }
                
                byte[] headerKey = new byte[32];
                ((ReadOnlySpan<byte>)keySet.HeaderKey).CopyTo(headerKey);
                
                byte[] headerDecrypted = new byte[0xC00];
                XtsDecrypt(headerEncOriginal, headerDecrypted, headerKey, 0x200, 0);
                
                string magic = System.Text.Encoding.ASCII.GetString(headerDecrypted, 0x200, 4);
                Console.WriteLine("Decrypted magic: " + magic);
                
                byte[] headerEncReencrypted = new byte[0xC00];
                XtsEncrypt(headerDecrypted, headerEncReencrypted, headerKey, 0x200, 0);
                
                // Compare original vs re-encrypted
                bool match = true;
                for (int i = 0; i < 0xC00; i++) {
                    if (headerEncOriginal[i] != headerEncReencrypted[i]) {
                        Console.WriteLine($"Mismatch at 0x{i:X3}: Original={headerEncOriginal[i]:X2}, Reencrypted={headerEncReencrypted[i]:X2}");
                        match = false;
                        if (i > 0x210) break; // Don't flood
                    }
                }
                if (match) {
                    Console.WriteLine("SUCCESS: Byte-for-byte roundtrip MATCH!");
                } else {
                    Console.WriteLine("FAILED: Mismatch found between original and re-encrypted headers!");
                }
            } catch (Exception ex) {
                Console.WriteLine("Error: " + ex);
            }
        }

        private static void XtsDecrypt(byte[] src, byte[] dst, byte[] key, int sectorSize, int startSector)
        {
            byte[] key1 = new byte[16];
            byte[] key2 = new byte[16];
            Array.Copy(key, 0, key1, 0, 16);
            Array.Copy(key, 16, key2, 0, 16);
            
            Array.Copy(src, 0, dst, 0, 0x200);
            
            var transform = new LibHac.Tools.FsSystem.Aes128XtsTransform(key1, key2, true); // true = decrypting
            int encryptedLength = src.Length - 0x200;
            Array.Copy(src, 0x200, dst, 0x200, encryptedLength);
            
            for (int i = 0; i < encryptedLength / sectorSize; i++)
            {
                transform.TransformBlock(dst, 0x200 + i * sectorSize, sectorSize, (ulong)(startSector + 1 + i));
            }
        }

        private static void XtsEncrypt(byte[] src, byte[] dst, byte[] key, int sectorSize, int startSector)
        {
            byte[] key1 = new byte[16];
            byte[] key2 = new byte[16];
            Array.Copy(key, 0, key1, 0, 16);
            Array.Copy(key, 16, key2, 0, 16);
            
            Array.Copy(src, 0, dst, 0, 0x200);
            
            var transform = new LibHac.Tools.FsSystem.Aes128XtsTransform(key1, key2, false); // false = encrypting
            int encryptedLength = src.Length - 0x200;
            Array.Copy(src, 0x200, dst, 0x200, encryptedLength);
            
            for (int i = 0; i < encryptedLength / sectorSize; i++)
            {
                transform.TransformBlock(dst, 0x200 + i * sectorSize, sectorSize, (ulong)(startSector + 1 + i));
            }
        }
    }
}
