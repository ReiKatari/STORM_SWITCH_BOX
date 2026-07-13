using System;
using System.IO;
using System.Reflection;
using System.Linq;

class Program
{
    static void Main()
    {
        try
        {
            var asm = Assembly.LoadFrom(@"e:\STORM SWITCH BOX\STORM SWITCH BOX WINUE 3\StormSwitchBox\bin\x64\Debug\net8.0-windows10.0.19041.0\LibHac.dll");
            var type = asm.GetType("LibHac.Tools.FsSystem.NcaUtils.Nca");
            if (type == null) type = asm.GetTypes().FirstOrDefault(t => t.Name == "Nca");
            
            var sb = new System.Text.StringBuilder();
            if (type != null)
            {
                sb.AppendLine("Properties:");
                foreach (var prop in type.GetProperties()) sb.AppendLine(prop.Name + " : " + prop.PropertyType.Name);
                sb.AppendLine("Methods:");
                foreach (var method in type.GetMethods()) sb.AppendLine(method.Name);
            }
            File.WriteAllText(@"e:\STORM SWITCH BOX\STORM SWITCH BOX WINUE 3\nca_info.txt", sb.ToString());
        }
        catch (Exception ex)
        {
            File.WriteAllText(@"e:\STORM SWITCH BOX\STORM SWITCH BOX WINUE 3\nca_info.txt", ex.ToString());
        }
    }
}
