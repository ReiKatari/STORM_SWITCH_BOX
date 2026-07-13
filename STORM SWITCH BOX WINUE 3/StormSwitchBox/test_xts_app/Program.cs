using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using LibHac.Common.Keys;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Tools.FsSystem;
using StormSwitchBox;
using StormSwitchBox.Services;
using StormSwitchBox.Models;

namespace TestApp
{
    public class Program
    {
        public static async Task Main()
        {
            try
            {
                Console.WriteLine("=== StormSwitchBox Hard Patch Repack Test Started ===");
                
                string keysPath = @"E:\STORM SWITCH BOX\STORM SWITCH BOX WINUE 3\StormSwitchBox\bin\x64\Debug\net8.0-windows10.0.19041.0\temp\hardpatch_64e29d\prod.keys";
                if (!System.IO.File.Exists(keysPath))
                {
                    Console.WriteLine("ERROR: prod.keys not found at: " + keysPath);
                    return;
                }
                
                Console.WriteLine("Loading keys from: " + keysPath);
                var keysService = new KeysService();
                keysService.LoadKeys(keysPath);
                Console.WriteLine("Keys loaded and derived successfully.");

                string baseNsp = @"E:\STORM SWITCH BOX\STORM SWITCH BOX WINUE 3\StormSwitchBox\bin\x64\Debug\net8.0-windows10.0.19041.0\temp\hardpatch_64e29d\Beautiful Desolation [01006B0014590000][v0] (11.12 GB).nsp";
                string updateNsp = @"E:\STORM SWITCH BOX\STORM SWITCH BOX WINUE 3\StormSwitchBox\bin\x64\Debug\net8.0-windows10.0.19041.0\temp\hardpatch_64e29d\Beautiful Desolation [01006B0014590800][v262144] (0.87 GB).nsp";
                string outNsp = @"E:\STORM SWITCH BOX\STORM SWITCH BOX WINUE 3\StormSwitchBox\bin\x64\Debug\net8.0-windows10.0.19041.0\temp\hardpatch_64e29d\output\BEAUTIFUL DESOLATION [01006b0014590000][v1.0.5][yanu-0.10.1-packed].nsp";

                if (!File.Exists(baseNsp))
                {
                    Console.WriteLine("ERROR: Base NSP not found: " + baseNsp);
                    return;
                }
                if (!File.Exists(updateNsp))
                {
                    Console.WriteLine("ERROR: Update NSP not found: " + updateNsp);
                    return;
                }

                // Резервное копирование старого упакованного NSP (на всякий случай)
                string backupNsp = outNsp + ".bak";
                if (File.Exists(outNsp) && !File.Exists(backupNsp))
                {
                    Console.WriteLine("Backing up old packed NSP to " + backupNsp);
                    File.Copy(outNsp, backupNsp);
                }

                Console.WriteLine("Running HardPatchEngine for Beautiful Desolation...");
                var task = new ProcessingTask
                {
                    OutputFileName = System.IO.Path.GetFileName(outNsp)
                };

                // Подпишемся на изменения логов задачи, чтобы выводить их в консоль в реальном времени
                task.PropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == nameof(ProcessingTask.LogDetails))
                    {
                        Console.WriteLine("LOG UPDATE: " + task.LogDetails);
                    }
                    else if (e.PropertyName == nameof(ProcessingTask.Status))
                    {
                        Console.WriteLine("STATUS: " + task.Status);
                    }
                };

                var hardPatchEngine = new HardPatchEngine(keysService);
                await hardPatchEngine.PatchUpdateAsync(
                    task, 
                    new List<string> { baseNsp, updateNsp }, 
                    outNsp, 
                    CancellationToken.None
                );

                Console.WriteLine("HARD PATCH REPACK COMPLETED SUCCESSFUL!");
                Console.WriteLine("Output NSP File Size: " + new FileInfo(outNsp).Length + " bytes");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.ToString());
            }
        }
    }
}
