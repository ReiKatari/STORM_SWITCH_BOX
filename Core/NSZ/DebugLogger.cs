using System;
using System.IO;

public static class DebugLogger
{
    public static void Log(string msg)
    {
        try 
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug_log.txt");
            File.AppendAllText(logPath, DateTime.Now.ToString("HH:mm:ss.fff") + " " + msg + "\n");
        } 
        catch {}
    }
}
