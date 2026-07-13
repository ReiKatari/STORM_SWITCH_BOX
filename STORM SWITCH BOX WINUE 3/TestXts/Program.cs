using System;
using System.IO;
using System.Linq;

namespace TestApp {
    public class Program {
        public static void Main() {
            string nspPath = @"E:\STORM SWITCH BOX\STORM SWITCH BOX WINUE 3\TestApp\bin\x64\Release\net8.0-windows10.0.19041.0\verify_temp\base_game.nsp";
            if (!File.Exists(nspPath)) {
                Console.WriteLine("NSP not found!");
                return;
            }
            
            using var fs = File.OpenRead(nspPath);
            byte[] header = new byte[0x1000];
            fs.Read(header, 0, 0x1000);
            
            uint numFiles = BitConverter.ToUInt32(header, 4);
            uint stringTableSize = BitConverter.ToUInt32(header, 8);
            uint headerSize = 0x10 + numFiles * 0x18 + stringTableSize;
            uint stringTableOffset = 0x10 + numFiles * 0x18;
            
            long ncaOffset = 0;
            for (int i = 0; i < numFiles; i++) {
                int entryOffset = 0x10 + i * 0x18;
                long offset = BitConverter.ToInt64(header, entryOffset);
                uint nameOffset = BitConverter.ToUInt32(header, entryOffset + 16);
                
                // Read name
                int idx = (int)(stringTableOffset + nameOffset);
                string name = "";
                while (header[idx] != 0) {
                    name += (char)header[idx];
                    idx++;
                }
                if (name.Contains("cnmt.nca")) {
                    ncaOffset = headerSize + offset;
                    break;
                }
            }
            
            Console.WriteLine($"NCA Offset: 0x{ncaOffset:X}");
            
            fs.Position = ncaOffset;
            byte[] ncaHeader = new byte[0xC00];
            fs.Read(ncaHeader, 0, 0xC00);
            
            byte[] ivPattern = FromHex("224DD65B38EFE6656828AA9D34EBFE6C");
            
            Console.WriteLine("Searching for IV in encrypted NCA header...");
            SearchPattern(ncaHeader, ivPattern);
            
            // Decrypt NCA header
            byte[] headerKeyBytes = FromHex("aeaab1ca08adf9bef12991f369e3c567d6881e4e4a6a47a51f6e4877062d542d");
            byte[] key1 = headerKeyBytes.Take(16).ToArray();
            byte[] key2 = headerKeyBytes.Skip(16).ToArray();
            
            byte[] decHeader = new byte[0xC00];
            Array.Copy(ncaHeader, decHeader, 0x200);
            Array.Copy(ncaHeader, 0x200, decHeader, 0x200, 0xA00);
            
            var xts = new LibHac.Tools.FsSystem.Aes128XtsTransform(key1, key2, true);
            for (int sector = 0; sector < 5; sector++) {
                xts.TransformBlock(decHeader, 0x200 + sector * 0x200, 0x200, (ulong)(sector + 1));
            }
            
            Console.WriteLine("\nSearching for IV in decrypted NCA header...");
            SearchPattern(decHeader, ivPattern);
            
            // Also search for parts of IV (first 8 bytes, last 8 bytes)
            byte[] ivUpper = ivPattern.Take(8).ToArray();
            byte[] ivLower = ivPattern.Skip(8).ToArray();
            Console.WriteLine($"\nSearching for IV upper ({ToHex(ivUpper)})...");
            SearchPattern(decHeader, ivUpper);
            Console.WriteLine($"\nSearching for IV lower ({ToHex(ivLower)})...");
            SearchPattern(decHeader, ivLower);
        }
        
        static void SearchPattern(byte[] buffer, byte[] pattern) {
            for (int i = 0; i <= buffer.Length - pattern.Length; i++) {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++) {
                    if (buffer[i + j] != pattern[j]) {
                        match = false;
                        break;
                    }
                }
                if (match) {
                    Console.WriteLine($"  Found at offset 0x{i:X}");
                }
            }
        }
        
        static byte[] FromHex(string hex) {
            hex = hex.Replace(" ", "").Replace("-", "");
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++) {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }
        
        static string ToHex(byte[] bytes) {
            return BitConverter.ToString(bytes).Replace("-", "");
        }
    }
}
