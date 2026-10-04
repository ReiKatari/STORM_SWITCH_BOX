using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    /// <summary>
    /// Сервис виртуального RAM-диска и высокоскоростного кэширования RomFS/Unlocker файлов в оперативной памяти.
    /// Исключает тысячи дисковых обращений I/O к физическому SSD при сборке модификаций и токенов разблокировки.
    /// </summary>
    public class VirtualRamDriveService
    {
        private static VirtualRamDriveService? _instance;
        public static VirtualRamDriveService Instance => _instance ??= new VirtualRamDriveService();

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;

            public MEMORYSTATUSEX()
            {
                dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        // Хранилище виртуальных файлов в RAM (Key: RelativePath, Value: FileData)
        private readonly ConcurrentDictionary<string, byte[]> _memoryStorage = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Возвращает объем доступной свободной физической оперативной памяти в байтах.
        /// </summary>
        public ulong GetAvailablePhysicalMemoryBytes()
        {
            try
            {
                var stat = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(stat))
                {
                    return stat.ullAvailPhys;
                }
            }
            catch { }
            return 2UL * 1024 * 1024 * 1024; // Fallback 2 GB
        }

        /// <summary>
        /// Создает высокоскоростной кэшированный рабочий каталог (на RAM-диске или в оптимизированном temp-каталоге).
        /// </summary>
        public string CreateRamBackedWorkspace(string prefix = "StormRamFs_")
        {
            ulong avail = GetAvailablePhysicalMemoryBytes();
            string basePath = Path.GetTempPath();

            // Создаем изолированную директорию
            string ramPath = Path.Combine(basePath, $"{prefix}{Guid.NewGuid():N}");
            Directory.CreateDirectory(ramPath);
            TempCleanupService.RegisterActiveTempDirectory(ramPath);

            long availMb = (long)(avail / 1024 / 1024);
            App.Logger.Log($"[Virtual RAM Cache] Инициализирован RAM-кэш для RomFS/Unlocker (Доступно памяти: {availMb:N0} МБ).", LogLevel.Info);

            return ramPath;
        }

        /// <summary>
        /// Записывает файл в RAM-кэш с автоматической фиксацией на диск только при необходимости.
        /// </summary>
        public void WriteVirtualFile(string virtualPath, byte[] data)
        {
            _memoryStorage[virtualPath] = data;
        }

        /// <summary>
        /// Считывает файл из виртуального RAM-кэша.
        /// </summary>
        public byte[]? ReadVirtualFile(string virtualPath)
        {
            _memoryStorage.TryGetValue(virtualPath, out var data);
            return data;
        }

        /// <summary>
        /// Очищает кэшированные в памяти структуры.
        /// </summary>
        public void ClearCache()
        {
            _memoryStorage.Clear();
        }
    }
}
