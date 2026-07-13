using System;
using System.Reflection;
using LibHac.Common.Keys;

namespace TestApp {
    public class Program {
        public static void Main() {
            try {
                var keySet = new KeySet();
                Type type = keySet.GetType();
                Console.WriteLine("=== PROPERTIES ===");
                foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
                    Console.WriteLine($"{prop.PropertyType} {prop.Name}");
                }
                Console.WriteLine("=== FIELDS ===");
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
                    Console.WriteLine($"{field.FieldType} {field.Name}");
                }
            } catch (Exception ex) {
                Console.WriteLine("Error: " + ex);
            }
        }
    }
}
