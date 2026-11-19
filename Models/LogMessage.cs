using System;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

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

        // Цвет текста сообщения
        public SolidColorBrush ColorBrush => Level switch
        {
            LogLevel.Info    => UiBrushCache.Get(Color.FromArgb(255, 180, 198, 220)),  // Серо-голубой
            LogLevel.Warning => UiBrushCache.Get(Color.FromArgb(255, 255, 193, 7)),    // Янтарный
            LogLevel.Error   => UiBrushCache.Get(Color.FromArgb(255, 255, 82, 82)),    // Красный
            LogLevel.Success => UiBrushCache.Get(Color.FromArgb(255, 76, 175, 80)),    // Зелёный
            LogLevel.Debug   => UiBrushCache.Get(Color.FromArgb(255, 108, 117, 125)),  // Тёмно-серый
            _                => UiBrushCache.Get(Colors.White)
        };

        // Цвет фона бейджа уровня
        public SolidColorBrush LevelBadgeBackground => Level switch
        {
            LogLevel.Info    => UiBrushCache.Get(Color.FromArgb(40, 100, 149, 237)),   // Синий полупрозрачный
            LogLevel.Warning => UiBrushCache.Get(Color.FromArgb(50, 255, 193, 7)),     // Янтарный полупрозрачный
            LogLevel.Error   => UiBrushCache.Get(Color.FromArgb(50, 255, 82, 82)),     // Красный полупрозрачный
            LogLevel.Success => UiBrushCache.Get(Color.FromArgb(40, 76, 175, 80)),     // Зелёный полупрозрачный
            LogLevel.Debug   => UiBrushCache.Get(Color.FromArgb(30, 108, 117, 125)),   // Серый полупрозрачный
            _                => UiBrushCache.Get(Colors.Transparent)
        };

        // Цвет текста бейджа
        public SolidColorBrush LevelBadgeForeground => Level switch
        {
            LogLevel.Info    => UiBrushCache.Get(Color.FromArgb(255, 130, 177, 255)),
            LogLevel.Warning => UiBrushCache.Get(Color.FromArgb(255, 255, 213, 79)),
            LogLevel.Error   => UiBrushCache.Get(Color.FromArgb(255, 255, 120, 120)),
            LogLevel.Success => UiBrushCache.Get(Color.FromArgb(255, 129, 199, 132)),
            LogLevel.Debug   => UiBrushCache.Get(Color.FromArgb(255, 158, 158, 158)),
            _                => UiBrushCache.Get(Colors.White)
        };

        // Текст бейджа
        public string LevelLabel => Level switch
        {
            LogLevel.Info    => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error   => "ERROR",
            LogLevel.Success => "  OK  ",
            LogLevel.Debug   => "DEBUG",
            _                => "INFO"
        };

        // Иконка уровня (FontIcon glyph)
        public string LevelIcon => Level switch
        {
            LogLevel.Info    => "\uE946",   // Info
            LogLevel.Warning => "\uE7BA",   // Warning
            LogLevel.Error   => "\uEA39",   // ErrorBadge
            LogLevel.Success => "\uE73E",   // CheckMark
            LogLevel.Debug   => "\uEBE8",   // Bug
            _                => "\uE946"
        };
        
        // Цвет фона строки для подсветки ошибок/предупреждений
        public SolidColorBrush RowBackground => Level switch
        {
            LogLevel.Error   => UiBrushCache.Get(Color.FromArgb(15, 255, 82, 82)),
            LogLevel.Warning => UiBrushCache.Get(Color.FromArgb(10, 255, 193, 7)),
            _                => UiBrushCache.Get(Colors.Transparent)
        };

        public string FormattedTime => Timestamp.ToString("HH:mm");
    }
}
