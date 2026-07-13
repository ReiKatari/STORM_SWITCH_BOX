using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using StormSwitchBox;
using StormSwitchBox.Services;
using StormSwitchBox.Models;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("Initializing Keys...");
        string keysPath = @"C:\Users\ReiKatari\.switch\prod.keys";
        App.Keys.LoadKeys(keysPath);
        
        try
        {
            byte[] titleKeyUpdate = HexToBytes("959e1f519bc2298bcc3b45f8b2d4dcab");
            byte[] keyAreaKeyUpdate = HexToBytes("7fc2426b0c011a1bc5fcab13ca26d082");
            byte[] titleKeyBase = HexToBytes("5ce2833b1443d09624d893325120b7fb");
            byte[] keyAreaKeyBase = HexToBytes("715868c4fdcd0f987384105f00eb69fc");
            
            byte[] iv1 = HexToBytes("00000001000000000000000000000000");
            byte[] iv0 = HexToBytes("00000000000000000000000000000000");
            byte[] plain = System.Text.Encoding.ASCII.GetBytes("PFS0");
            
            using (var aes = System.Security.Cryptography.Aes.Create())
            {
                aes.Mode = System.Security.Cryptography.CipherMode.ECB;
                aes.Padding = System.Security.Cryptography.PaddingMode.None;
                
                void RunTest(string name, byte[] key, byte[] iv)
                {
                    using (var encryptor = aes.CreateEncryptor(key, null))
                    {
                        byte[] keystream = new byte[16];
                        encryptor.TransformBlock(iv, 0, 16, keystream, 0);
                        byte[] result = new byte[4];
                        for (int i = 0; i < 4; i++) result[i] = (byte)(plain[i] ^ keystream[i]);
                        Console.WriteLine($"[DEBUG TEST] {name}: {BitConverter.ToString(result).Replace("-", " ")}");
                    }
                }
                
                RunTest("Update TitleKey + IV=1", titleKeyUpdate, iv1);
                RunTest("Update KeyAreaKey + IV=1", keyAreaKeyUpdate, iv1);
                RunTest("Base TitleKey + IV=1", titleKeyBase, iv1);
                RunTest("Base KeyAreaKey + IV=1", keyAreaKeyBase, iv1);
                
                RunTest("Update TitleKey + IV=0", titleKeyUpdate, iv0);
                RunTest("Update KeyAreaKey + IV=0", keyAreaKeyUpdate, iv0);
                RunTest("Base TitleKey + IV=0", titleKeyBase, iv0);
                RunTest("Base KeyAreaKey + IV=0", keyAreaKeyBase, iv0);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[DEBUG TEST] AES CTR Test Error: " + ex.Message);
        }
        
        string outDir = @"P:\CONSOLES\Nintendo Switch\GAMES";
        string outFileName = "Devil Jam [WW] [RUS] (1.0.1 - 65536 - 0100C6A0235D4000) (1G+1U)";
        string outPath = Path.Combine(outDir, outFileName + ".nsz");
        
        var task = new ProcessingTask
        {
            Operation = "Multi",
            TargetFormat = "NSZ",
            OutputFolder = outDir,
            OutputFileName = outFileName,
        };
        
        var inputFiles = new List<string>
        {
            @"P:\CONSOLES\Nintendo Switch\DOWNLOADS\Devil Jam\[WW] [RUS] (1.0.1 - 65536 - 0100C6A0235D4000) (1G+1U)\Devil Jam [0100C6A0235D4000][v0] (0.45 GB).nsz",
            @"P:\CONSOLES\Nintendo Switch\DOWNLOADS\Devil Jam\[WW] [RUS] (1.0.1 - 65536 - 0100C6A0235D4000) (1G+1U)\Devil Jam [0100C6A0235D4800][v65536] (0.19 GB).nsz"
        };
        
        Console.WriteLine("Starting BuildMultiContentAsync...");
        var cts = new CancellationTokenSource();
        try
        {
            await App.MultiContent.BuildMultiContentAsync(task, inputFiles, outPath, patchFirmware: false, cts.Token);
            Console.WriteLine("Build finished.");
            Console.WriteLine("Task Status: " + task.Status);
            Console.WriteLine("Task LogDetails:\n" + task.LogDetails);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex);
        }
    }

    private static byte[] HexToBytes(string hex)
    {
        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }
}
