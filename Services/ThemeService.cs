using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using Windows.UI;

namespace StormSwitchBox.Services
{
    public static class ThemeService
    {
        public static void ApplyTheme(string themeName)
        {
            if (string.IsNullOrEmpty(themeName)) themeName = "STORM MIDNIGHT";

            ElementTheme targetElementTheme = (themeName == "STORM DAY") ? ElementTheme.Light : ElementTheme.Dark;

            // Определяем палитру цветов
            Color bg, cardBg, cardSecBg, cardBorder, textPrimary, textSecondary, textTertiary;

            switch (themeName)
            {
                case "STORM NIGHT": // Глубокая OLED Чёрная тема
                    bg = ParseHexColor("#000000");
                    cardBg = ParseHexColor("#0D0D10");
                    cardSecBg = ParseHexColor("#16161C");
                    cardBorder = ParseHexColor("#252530");
                    textPrimary = ParseHexColor("#FFFFFF");
                    textSecondary = ParseHexColor("#A5A5B5");
                    textTertiary = ParseHexColor("#757585");
                    break;

                case "STORM DAY": // Чистая светлая тема
                    bg = ParseHexColor("#F3F4F8");
                    cardBg = ParseHexColor("#FFFFFF");
                    cardSecBg = ParseHexColor("#EAEFF5");
                    cardBorder = ParseHexColor("#D1D5DB");
                    textPrimary = ParseHexColor("#111827"); // Темно-углистый текст для 100% читаемости!
                    textSecondary = ParseHexColor("#4B5563");
                    textTertiary = ParseHexColor("#6B7280");
                    break;

                case "STORM CYBERPUNK": // Киберпанк 2077
                    bg = ParseHexColor("#08090E");
                    cardBg = ParseHexColor("#10121D");
                    cardSecBg = ParseHexColor("#191C2E");
                    cardBorder = ParseHexColor("#00F0FF"); // Неоновый циан
                    textPrimary = ParseHexColor("#FCEE09"); // Неоновый жёлтый
                    textSecondary = ParseHexColor("#00F0FF"); // Неоновый циан
                    textTertiary = ParseHexColor("#8A95C0");
                    break;

                case "STORM MIDNIGHT":
                default: // Классическая темно-синеватая полночь
                    bg = ParseHexColor("#18181C");
                    cardBg = ParseHexColor("#222228");
                    cardSecBg = ParseHexColor("#2B2B34");
                    cardBorder = ParseHexColor("#363642");
                    textPrimary = ParseHexColor("#FFFFFF");
                    textSecondary = ParseHexColor("#B0B0C0");
                    textTertiary = ParseHexColor("#808090");
                    break;
            }

            // Записываем ресурсы в глобальные словари приложения
            SetResource("ApplicationPageBackgroundThemeBrush", new SolidColorBrush(bg));
            SetResource("CardBackgroundFillColorDefaultBrush", new SolidColorBrush(cardBg));
            SetResource("CardBackgroundFillColorSecondaryBrush", new SolidColorBrush(cardSecBg));
            SetResource("CardStrokeColorDefaultBrush", new SolidColorBrush(cardBorder));
            SetResource("SurfaceStrokeColorDefaultBrush", new SolidColorBrush(cardBorder));
            SetResource("TextFillColorPrimaryBrush", new SolidColorBrush(textPrimary));
            SetResource("TextFillColorSecondaryBrush", new SolidColorBrush(textSecondary));
            SetResource("TextFillColorTertiaryBrush", new SolidColorBrush(textTertiary));

            // Применяем тему к главному окну, контейнеру и навигации
            if (App.MainWindow != null)
            {
                if (App.MainWindow.Content is FrameworkElement root)
                {
                    root.RequestedTheme = targetElementTheme;

                    if (root is Grid mainGrid)
                    {
                        mainGrid.Background = new SolidColorBrush(bg);
                        foreach (var child in mainGrid.Children)
                        {
                            if (child is NavigationView nav)
                            {
                                nav.RequestedTheme = targetElementTheme;
                                nav.Background = new SolidColorBrush(bg);
                            }
                        }
                    }
                }
            }

            // Переприменяем акцентный цвет для выравнивания контраста
            ApplyAccentColor(App.Settings.Current.AccentColorTheme);
        }

        public static void ApplyAccentColor(string colorHex)
        {
            Color accentColor;
            if (string.IsNullOrEmpty(colorHex) || colorHex == "Default")
            {
                accentColor = ParseHexColor("#0078D4");
            }
            else
            {
                accentColor = ParseHexColor(colorHex);
            }

            // В теме Cyberpunk дефолтный акцент - Неоново-Желтый
            if (App.Settings.Current.AppTheme == "STORM CYBERPUNK" && (string.IsNullOrEmpty(colorHex) || colorHex == "Default"))
            {
                accentColor = ParseHexColor("#FCEE09");
            }

            Color dark1 = ChangeColorBrightness(accentColor, -0.15f);
            Color dark2 = ChangeColorBrightness(accentColor, -0.30f);
            Color light1 = ChangeColorBrightness(accentColor, 0.20f);
            Color light2 = ChangeColorBrightness(accentColor, 0.40f);

            var accentBrush = new SolidColorBrush(accentColor);
            var light1Brush = new SolidColorBrush(light1);
            var dark1Brush = new SolidColorBrush(dark1);

            SetResource("SystemAccentColor", accentColor);
            SetResource("SystemAccentColorDark1", dark1);
            SetResource("SystemAccentColorDark2", dark2);
            SetResource("SystemAccentColorLight1", light1);
            SetResource("SystemAccentColorLight2", light2);

            SetResource("SystemControlHighlightAccentBrush", accentBrush);
            SetResource("SystemControlHighlightListAccentLowBrush", new SolidColorBrush(Color.FromArgb(60, accentColor.R, accentColor.G, accentColor.B)));
            SetResource("SystemAccentColorBrush", accentBrush);

            // Элементы управления WinUI (ToggleSwitch, AccentButton, NavigationView Selection)
            SetResource("ToggleSwitchFillOn", accentBrush);
            SetResource("ToggleSwitchFillOnPointerOver", light1Brush);
            SetResource("ToggleSwitchFillOnPressed", dark1Brush);
            SetResource("AccentButtonBackground", accentBrush);
            SetResource("AccentButtonBackgroundPointerOver", light1Brush);
            SetResource("AccentButtonBackgroundPressed", dark1Brush);
            SetResource("NavigationViewItemForegroundSelected", accentBrush);
            SetResource("NavigationViewItemIconForegroundSelected", accentBrush);

            RefreshUI();
        }

        private static void SetResource(string key, object value)
        {
            Application.Current.Resources[key] = value;

            if (Application.Current.Resources.ThemeDictionaries.TryGetValue("Dark", out var darkDictObj) && darkDictObj is ResourceDictionary darkDict)
            {
                darkDict[key] = value;
            }
            if (Application.Current.Resources.ThemeDictionaries.TryGetValue("Light", out var lightDictObj) && lightDictObj is ResourceDictionary lightDict)
            {
                lightDict[key] = value;
            }
        }

        private static void RefreshUI()
        {
            if (App.MainWindow?.Content is FrameworkElement root)
            {
                var currentTheme = root.RequestedTheme;
                root.RequestedTheme = currentTheme == ElementTheme.Dark ? ElementTheme.Light : ElementTheme.Dark;
                root.RequestedTheme = currentTheme;
            }
        }

        public static Color ParseHexColor(string hex)
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
