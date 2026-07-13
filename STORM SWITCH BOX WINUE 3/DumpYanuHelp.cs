using System;
using System.Diagnostics;
using System.IO;

class Program
{
    static void Main()
    {
        var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = @"e:\STORM SWITCH BOX\STORM SWITCH BOX WINUE 3\tools\yanu-cli.exe",
                Arguments = "pack --help",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        proc.Start();
        string output = proc.StandardOutput.ReadToEnd() + "\n" + proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        File.WriteAllText(@"e:\STORM SWITCH BOX\STORM SWITCH BOX WINUE 3\yanu_pack_help.txt", output);

        var proc2 = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = @"e:\STORM SWITCH BOX\STORM SWITCH BOX WINUE 3\tools\yanu-cli.exe",
                Arguments = "unpack --help",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        proc2.Start();
        string output2 = proc2.StandardOutput.ReadToEnd() + "\n" + proc2.StandardError.ReadToEnd();
        proc2.WaitForExit();
        File.WriteAllText(@"e:\STORM SWITCH BOX\STORM SWITCH BOX WINUE 3\yanu_unpack_help.txt", output2);
        
        var proc3 = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = @"e:\STORM SWITCH BOX\STORM SWITCH BOX WINUE 3\tools\yanu-cli.exe",
                Arguments = "--help",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        proc3.Start();
        string output3 = proc3.StandardOutput.ReadToEnd() + "\n" + proc3.StandardError.ReadToEnd();
        proc3.WaitForExit();
        File.WriteAllText(@"e:\STORM SWITCH BOX\STORM SWITCH BOX WINUE 3\yanu_help.txt", output3);
    }
}
