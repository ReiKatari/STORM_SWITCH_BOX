using System;
using System.IO;

public static class DebugLogger
{
	public static void Log(string msg)
	{
		try
		{
			File.AppendAllText("e:\\STORM SWITCH BOX\\STORM SWITCH BOX WINUE 3\\StormSwitchBox\\debug_log.txt", DateTime.Now.ToString("HH:mm:ss.fff") + " " + msg + "\n");
		}
		catch
		{
		}
	}
}
