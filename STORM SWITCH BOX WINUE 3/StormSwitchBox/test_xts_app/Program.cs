using System;

namespace TestApp
{
    public class Program
    {
        public static void Main()
        {
            uint error = 0x2F5E02;
            uint module = error & 0x1FF;
            uint desc = (error >> 9) & 0x3FFF;
            Console.WriteLine($"Module: {module} ({(module == 2 ? "FS" : "Other")})");
            Console.WriteLine($"Description: {desc}");
            Console.WriteLine($"Formatted: {2000 + module}-{desc:D4}");
        }
    }
}
