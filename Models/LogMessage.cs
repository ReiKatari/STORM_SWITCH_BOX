using System;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

namespace StormSwitchBox.Models
{
    public enum LogLevel
    {
        Info,
        Warning,
        Error,
        Success,
        Debug
    }

    public class LogMessage
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string Message { get; set; } = string.Empty;
        public LogLevel Level { get; set; } = LogLevel.Info;

        public SolidColorBrush ColorBrush
        {
            get
            {
                return Level switch
                {
                    LogLevel.Info => new SolidColorBrush(Colors.LightGray),
                    LogLevel.Warning => new SolidColorBrush(Colors.Yellow),
                    LogLevel.Error => new SolidColorBrush(Colors.Red),
                    LogLevel.Success => new SolidColorBrush(Colors.LightGreen),
                    LogLevel.Debug => new SolidColorBrush(Colors.DarkGray),
                    _ => new SolidColorBrush(Colors.White)
                };
            }
        }
        
        public string FormattedTime => Timestamp.ToString("HH:mm:ss.ff");
    }
}
