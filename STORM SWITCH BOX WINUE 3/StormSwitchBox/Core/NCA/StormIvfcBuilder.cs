using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using LibHac.Fs;

namespace StormSwitchBox.Core.NCA
{
    public class StormIvfcBuilder
    {
        private const int BlockSize = 0x4000;
        
        public static (byte[] MasterHash, Stream IvfcStream) BuildIvfc(Stream romFsData)
        {
            var levels = new List<byte[]>();
            
            // Level 5: RomFS data (we don't read the whole thing into memory, we hash it in blocks)
            romFsData.Position = 0;
            long dataSize = romFsData.Length;
            int numBlocksL5 = (int)((dataSize + BlockSize - 1) / BlockSize);
            byte[] l4Hashes = new byte[numBlocksL5 * 32];
            
            using (var sha = SHA256.Create())
            {
                byte[] buffer = new byte[BlockSize];
                for (int i = 0; i < numBlocksL5; i++)
                {
                    int read = romFsData.Read(buffer, 0, BlockSize);
                    if (read < BlockSize) Array.Clear(buffer, read, BlockSize - read);
                    byte[] hash = sha.ComputeHash(buffer, 0, BlockSize);
                    Array.Copy(hash, 0, l4Hashes, i * 32, 32);
                }
                
                levels.Add(l4Hashes); // Level 4
                
                // Now build lower levels until we reach Level 1
                byte[] currentLevel = l4Hashes;
                for (int level = 3; level >= 1; level--)
                {
                    int numBlocks = (currentLevel.Length + BlockSize - 1) / BlockSize;
                    byte[] nextLevel = new byte[numBlocks * 32];
                    
                    for (int i = 0; i < numBlocks; i++)
                    {
                        int offset = i * BlockSize;
                        int length = Math.Min(BlockSize, currentLevel.Length - offset);
                        Array.Clear(buffer, 0, BlockSize);
                        Array.Copy(currentLevel, offset, buffer, 0, length);
                        byte[] hash = sha.ComputeHash(buffer, 0, BlockSize);
                        Array.Copy(hash, 0, nextLevel, i * 32, 32);
                    }
                    
                    levels.Insert(0, nextLevel);
                    currentLevel = nextLevel;
                }
                
                // Master Hash
                Array.Clear(buffer, 0, BlockSize);
                Array.Copy(currentLevel, 0, buffer, 0, currentLevel.Length);
                byte[] masterHash = sha.ComputeHash(buffer, 0, BlockSize);
                
                // Combine levels into IVFC Stream
                var ivfcStream = new MemoryStream();
                foreach (var levelData in levels)
                {
                    // Align to BlockSize
                    ivfcStream.Write(levelData, 0, levelData.Length);
                    long remainder = ivfcStream.Length % BlockSize;
                    if (remainder != 0)
                    {
                        ivfcStream.Write(new byte[BlockSize - remainder], 0, (int)(BlockSize - remainder));
                    }
                }
                
                ivfcStream.Position = 0;
                return (masterHash, ivfcStream);
            }
        }
    }
}
