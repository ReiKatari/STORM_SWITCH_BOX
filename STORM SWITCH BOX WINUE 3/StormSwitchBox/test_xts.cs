using System;
using System.Reflection;

namespace TestApp {
    public class Program {
        public static void Main() {
            var asm = typeof(LibHac.Tools.FsSystem.Nca).Assembly;
            Console.WriteLine("Assembly Loaded: " + asm.FullName);
            var type = typeof(LibHac.Tools.FsSystem.Nca);
            Console.WriteLine("\n--- CONSTRUCTORS ---");
            foreach (var ctor in type.GetConstructors()) {
                Console.WriteLine(ctor.ToString());
            }
            Console.WriteLine("\n--- METHODS ---");
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static)) {
                if (method.Name.Contains("Base") || method.Name.Contains("Set")) {
                    Console.WriteLine(method.ToString());
                }
            }
        }
    }
}
