using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace StormSwitchBox.Services
{
    public static class JobObjectManager
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(IntPtr hJob, int JobObjectInfoType, IntPtr lpJobObjectInfo, int cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        private const int JobObjectExtendedLimitInformation = 9;
        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        private static IntPtr _jobHandle = IntPtr.Zero;
        private static readonly object _lock = new();
        private static readonly ConcurrentDictionary<int, long> _trackedChildPids = new();

        public static void InitializeJobForCurrentProcess()
        {
            lock (_lock)
            {
                if (_jobHandle != IntPtr.Zero) return;

                try
                {
                    _jobHandle = CreateJobObject(IntPtr.Zero, "StormSwitchBox_ChildJob_" + Environment.ProcessId);
                    if (_jobHandle != IntPtr.Zero)
                    {
                        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
                        info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

                        int length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
                        IntPtr ptr = Marshal.AllocHGlobal(length);
                        try
                        {
                            Marshal.StructureToPtr(info, ptr, false);
                            if (SetInformationJobObject(_jobHandle, JobObjectExtendedLimitInformation, ptr, length))
                            {
                                AssignProcessToJobObject(_jobHandle, Process.GetCurrentProcess().Handle);
                            }
                        }
                        finally
                        {
                            Marshal.FreeHGlobal(ptr);
                        }
                    }
                }
                catch { }
            }
        }

        public static void AddProcess(Process? process)
        {
            if (process == null) return;
            InitializeJobForCurrentProcess();

            try
            {
                if (!process.HasExited)
                {
                    if (_jobHandle != IntPtr.Zero)
                        AssignProcessToJobObject(_jobHandle, process.Handle);

                    long startTicks = 0;
                    try { startTicks = process.StartTime.Ticks; } catch { }
                    _trackedChildPids[process.Id] = startTicks;
                }
            }
            catch { }
        }

        public static void UnregisterProcess(int pid)
        {
            _trackedChildPids.TryRemove(pid, out _);
        }

        /// <summary>
        /// Безопасно завершает строго дочерние процессы, запущенные самим приложением (без убийства сторонних процессов ОС).
        /// </summary>
        public static void KillAllToolProcesses()
        {
            int currentPid = Environment.ProcessId;
            string toolsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "tools"));

            foreach (var kv in _trackedChildPids.ToArray())
            {
                int pid = kv.Key;
                long expectedTicks = kv.Value;
                if (pid == currentPid) continue;

                try
                {
                    using var proc = Process.GetProcessById(pid);
                    if (!proc.HasExited)
                    {
                        bool matchesStart = expectedTicks == 0;
                        if (!matchesStart)
                        {
                            try { matchesStart = proc.StartTime.Ticks == expectedTicks; } catch { matchesStart = false; }
                        }

                        bool isInsideTools = false;
                        if (!matchesStart)
                        {
                            try
                            {
                                string? exePath = proc.MainModule?.FileName;
                                if (!string.IsNullOrEmpty(exePath))
                                    isInsideTools = Path.GetFullPath(exePath).StartsWith(toolsDir, StringComparison.OrdinalIgnoreCase);
                            }
                            catch { }
                        }

                        if (matchesStart || isInsideTools)
                            proc.Kill(entireProcessTree: true);
                    }
                }
                catch { }
                finally
                {
                    _trackedChildPids.TryRemove(pid, out _);
                }
            }
        }
    }
}
