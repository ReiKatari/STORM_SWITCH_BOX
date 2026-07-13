using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Collections.Generic;

class Program
{
    static void Main()
    {
        string path = @""E:\STORM SWITCH BOX\STORM SWITCH BOX WINUE 3\StormSwitchBox\bin\Debug\net8.0-windows10.0.19041.0\win-x64\StormSwitchBox.dll"";
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var peReader = new PEReader(fs);
        var metadataReader = peReader.GetMetadataReader();
        
        var strings = new HashSet<string>();
        foreach (var handle in metadataReader.UserStrings)
        {
            string s = metadataReader.GetUserString(handle);
            if (!string.IsNullOrEmpty(s)) strings.Add(s);
        }
        
        foreach (var s in strings)
        {
            if (System.Text.Encoding.UTF8.GetByteCount(s) > s.Length)
                Console.WriteLine(s);
        }
    }
}