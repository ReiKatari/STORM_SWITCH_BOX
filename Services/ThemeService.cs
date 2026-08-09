using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace StormSwitchBox.Services
{
    public static class ThemeService
    {
        public static void ApplyAccentColor(string colorHex)
        {
            if (string.IsNullOrEmpty(colorHex) || colorHex == "Default")
            {
                Application.Current.Resources.Remove("SystemAccentColor");
                Application.Current.Resources.Remove("SystemAccentColorDark1");
                Application.Current.Resources.Remove("SystemAccentColorLight1");
                return;
            }

            try
            {
                Color accentColor = ParseHexColor(colorHex);

                Application.Current.Resources["SystemAccentColor"] = accentColor;
                Application.Current.Resources["SystemAccentColorDark1"] = ChangeColorBrightness(accentColor, -0.15f);
                Application.Current.Resources["SystemAccentColorDark2"] = ChangeColorBrightness(accentColor, -0.30f);
                Application.Current.Resources["SystemAccentColorLight1"] = ChangeColorBrightness(accentColor, 0.15f);
                Application.Current.Resources["SystemAccentColorLight2"] = ChangeColorBrightness(accentColor, 0.30f);

                var accentBrush = new SolidColorBrush(accentColor);
                Application.Current.Resources["SystemControlHighlightAccentBrush"] = accentBrush;
                Application.Current.Resources["SystemControlHighlightListAccentLowBrush"] = new SolidColorBrush(ChangeColorBrightness(accentColor, -0.4f));

                // Принудительное мгновенное переключение темы для перерисовки WinUI 3
                if (App.MainWindow?.Content is FrameworkElement root)
                {
                    var currentTheme = root.RequestedTheme;
                    root.RequestedTheme = currentTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
                    root.RequestedTheme = currentTheme;
                }
            }
            catch { }
        }

        public static void ApplyTheme(string themeName)
        {
            if (App.MainWindow?.Content is FrameworkElement root)
            {
                switch (themeName)
                {
                    case "STORM DAY":
                        root.RequestedTheme = ElementTheme.Light;
                        ApplyCustomThemeResources("STORM DAY");
                        break;
                    case "STORM NIGHT":
                        root.RequestedTheme = ElementTheme.Dark;
                        ApplyCustomThemeResources("STORM NIGHT");
                        break;
                    case "STORM CYBERPUNK":
                        root.RequestedTheme = ElementTheme.Dark;
                        ApplyCustomThemeResources("STORM CYBERPUNK");
                        break;
                    case "STORM MIDNIGHT":
                    default:
                        root.RequestedTheme = ElementTheme.Dark;
                        ApplyCustomThemeResources("STORM MIDNIGHT");
                        break;
                }
            }
        }

        private static void ApplyCustomThemeResources(string themeName)
        {
            var res = Application.Current.Resources;
            switch (themeName)
            {
                case "STORM DAY":
                    res["ApplicationPageBackgroundThemeBrush"] = new SolidColorBrush(ParseHexColor("#F5F6F8"));
                    res["CardBackgroundFillColorDefaultBrush"] = new SolidColorBrush(ParseHexColor("#FFFFFF"));
                    res["CardBackgroundFillColorSecondaryBrush"] = new SolidColorBrush(ParseHexColor("#EFEFEF"));
                    res["TextFillColorPrimaryBrush"] = new SolidColorBrush(ParseHexColor("#1A1D20"));
                    res["TextFillColorSecondaryBrush"] = new SolidColorBrush(ParseHexColor("#5A626A"));
                    break;

                case "STORM NIGHT": // OLED Deep Black
                    res["ApplicationPageBackgroundThemeBrush"] = new SolidColorBrush(ParseHexColor("#000000"));
                    res["CardBackgroundFillColorDefaultBrush"] = new SolidColorBrush(ParseHexColor("#0B0C0E"));
                    res["CardBackgroundFillColorSecondaryBrush"] = new SolidColorBrush(ParseHexColor("#141518"));
                    res["TextFillColorPrimaryBrush"] = new SolidColorBrush(ParseHexColor("#FFFFFF"));
                    res["TextFillColorSecondaryBrush"] = new SolidColorBrush(ParseHexColor("#A0A5B0"));
                    break;

                case "STORM CYBERPUNK": // Cyberpunk 2077 Neon Yellow & Cyan & Dark Charcoal
                    res["ApplicationPageBackgroundThemeBrush"] = new SolidColorBrush(ParseHexColor("#0A0B10"));
                    res["CardBackgroundFillColorDefaultBrush"] = new SolidColorBrush(ParseHexColor("#12141F"));
                    res["CardBackgroundFillColorSecondaryBrush"] = new SolidColorBrush(ParseHexColor("#1A1D2E"));
                    res["TextFillColorPrimaryBrush"] = new SolidColorBrush(ParseHexColor("#FCEE09")); // Neon Yellow
                    res["TextFillColorSecondaryBrush"] = new SolidColorBrush(ParseHexColor("#00F0FF")); // Neon Cyan
                    break;

                case "STORM MIDNIGHT":
                default:
                    res["ApplicationPageBackgroundThemeBrush"] = new SolidColorBrush(ParseHexColor("#1C1C1E"));
                    res["CardBackgroundFillColorDefaultBrush"] = new SolidColorBrush(ParseHexColor("#2C2C2E"));
                    res["CardBackgroundFillColorSecondaryBrush"] = new SolidColorBrush(ParseHexColor("#3A3A3C"));
                    res["TextFillColorPrimaryBrush"] = new SolidColorBrush(ParseHexColor("#FFFFFF"));
                    res["TextFillColorSecondaryBrush"] = new SolidColorBrush(ParseHexColor("#8E8E93"));
                    break;
            }
        }

        private static Color ParseHexColor(string hex)
        {
            hex = hex.Replace("#", "").Trim();
            if (hex.Length == 6)
            {
                byte r = Convert.ToByte(hex.Substring(0, 2), 16);
                byte g = Convert.ToByte(hex.Substring(2, 2), 16);
                byte b = Convert.ToByte(hex.Substring(4, 2), 16);
                return Color.FromArgb(255, r, g, b);
            }
            if (hex.Length == 8)
            {
                byte a = Convert.ToByte(hex.Substring(0, 2), 16);
                byte r = Convert.ToByte(hex.Substring(2, 2), 16);
                byte g = Convert.ToByte(hex.Substring(4, 2), 16);
                byte b = Convert.ToByte(hex.Substring(6, 2), 16);
                return Color.FromArgb(a, r, g, b);
            }
            return Microsoft.UI.Colors.RoyalBlue;
        }

        private static Color ChangeColorBrightness(Color color, float factor)
        {
            float r = color.R;
            float g = color.G;
            float b = color.B;

            if (factor < 0)
            {
                factor = 1 + factor;
                r *= factor;
                g *= factor;
                b *= factor;
            }
            else
            {
                r = (255 - r) * factor + r;
                g = (255 - g) * factor + g;
                b = (255 - b) * factor + b;
            }

            return Color.FromArgb(color.A, (byte)Math.Clamp(r, 0, 255), (byte)Math.Clamp(g, 0, 255), (byte)Math.Clamp(b, 0, 255));
        }
    }
}
