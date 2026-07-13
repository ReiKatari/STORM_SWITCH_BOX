using System;
using System.Reflection;
using LibHac.Tools.FsSystem.NcaUtils;

namespace TestApp {
    public class Program {
        public static void Main() {
            try {
                // Let's search all types in the LibHac assembly that represent FS headers
                var asm = typeof(Nca).Assembly;
                foreach (var type in asm.GetTypes()) {
                    if (type.Name.Contains("FsHeader") && !type.Name.Contains("Nca")) {
                        Console.WriteLine("---------------------------------------------");
                        Console.WriteLine("Type: " + type.FullName);
                        foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
                            Console.WriteLine($"  Field: {f.FieldType.Name} {f.Name}");
                        }
                        foreach (var prop in type.GetProperties()) {
                            Console.WriteLine($"  Property: {prop.PropertyType.Name} {prop.Name}");
                        }
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine("Error: " + ex);
            }
        }
    }
}
