using System;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace StormSwitchBox.Models;

public class LogMessage
{
	public DateTime Timestamp { get; set; } = DateTime.Now;

	public string Message { get; set; } = string.Empty;

	public LogLevel Level { get; set; } = LogLevel.Info;

	public SolidColorBrush ColorBrush
	{
		get
		{
			LogLevel level = Level;
			if (1 == 0)
			{
			}
			SolidColorBrush result = level switch
			{
				LogLevel.Info => new SolidColorBrush(Colors.LightGray), 
				LogLevel.Warning => new SolidColorBrush(Colors.Yellow), 
				LogLevel.Error => new SolidColorBrush(Colors.Red), 
				LogLevel.Success => new SolidColorBrush(Colors.LightGreen), 
				LogLevel.Debug => new SolidColorBrush(Colors.DarkGray), 
				_ => new SolidColorBrush(Colors.White), 
			};
			if (1 == 0)
			{
			}
			return result;
		}
	}

	public string FormattedTime => Timestamp.ToString("HH:mm:ss.ff");
}
