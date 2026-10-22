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

            Color bg, cardBg, cardSecBg, cardBorder, textPrimary, textSecondary, textTertiary;

            switch (themeName)
            {
                case "STORM DARK": // Тёмный кибер (сланцевые тона, акцент Cyan #00D2FF)
                    bg = ParseHexColor("#0F172A");
                    cardBg = ParseHexColor("#1E293B");
                    cardSecBg = ParseHexColor("#334155");
                    cardBorder = ParseHexColor("#475569");
                    textPrimary = ParseHexColor("#F8FAFC");
                    textSecondary = ParseHexColor("#94A3B8");
                    textTertiary = ParseHexColor("#64748B");
                    break;

                case "STORM NIGHT": // Чистая OLED Чёрная тема
                    bg = ParseHexColor("#0A0B10");
                    cardBg = ParseHexColor("#12131A");
                    cardSecBg = ParseHexColor("#1A1C26");
                    cardBorder = ParseHexColor("#252836");
                    textPrimary = ParseHexColor("#FFFFFF");
                    textSecondary = ParseHexColor("#A5A5B5");
                    textTertiary = ParseHexColor("#757585");
                    break;

                case "STORM DAY": // Чистая Светлая тема
                    bg = ParseHexColor("#F3F4F8");
                    cardBg = ParseHexColor("#FFFFFF");
                    cardSecBg = ParseHexColor("#EAEFF5");
                    cardBorder = ParseHexColor("#D1D5DB");
                    textPrimary = ParseHexColor("#111827"); // Темно-углистый текст 100% читаемости!
                    textSecondary = ParseHexColor("#4B5563");
                    textTertiary = ParseHexColor("#6B7280");
                    break;

                case "STORM MATRIX": // Матричный зелёный (#00FF66 на тёмно-изумрудном фоне)
                    bg = ParseHexColor("#061009");
                    cardBg = ParseHexColor("#0B1D11");
                    cardSecBg = ParseHexColor("#122B1A");
                    cardBorder = ParseHexColor("#1E462B");
                    textPrimary = ParseHexColor("#E2FCEB");
                    textSecondary = ParseHexColor("#4ADE80");
                    textTertiary = ParseHexColor("#22C55E");
                    break;

                case "STORM CYBERPUNK": // Неон Найт-Сити
                    bg = ParseHexColor("#08090E");
                    cardBg = ParseHexColor("#10121D");
                    cardSecBg = ParseHexColor("#191C2E");
                    cardBorder = ParseHexColor("#FF007F");
                    textPrimary = ParseHexColor("#FCEE09");
                    textSecondary = ParseHexColor("#00F0FF");
                    textTertiary = ParseHexColor("#8A95C0");
                    break;

                case "STORM FANTASY": // Королевское золото (#F59E0B на глубоком ночном фоне)
                    bg = ParseHexColor("#120E0A");
                    cardBg = ParseHexColor("#1C1610");
                    cardSecBg = ParseHexColor("#292017");
                    cardBorder = ParseHexColor("#4A3723");
                    textPrimary = ParseHexColor("#FEF3C7");
                    textSecondary = ParseHexColor("#FBBF24");
                    textTertiary = ParseHexColor("#B45309");
                    break;

                case "STORM WARHAMMER 40K": // Имперская готика (#D4AF37 на тёмно-титановом фоне)
                    bg = ParseHexColor("#100D0D");
                    cardBg = ParseHexColor("#1A1414");
                    cardSecBg = ParseHexColor("#261C1C");
                    cardBorder = ParseHexColor("#523A28");
                    textPrimary = ParseHexColor("#F5F0E6");
                    textSecondary = ParseHexColor("#D4AF37");
                    textTertiary = ParseHexColor("#9A7B3E");
                    break;

                case "STORM MIDNIGHT":
                default: // Аметистовая ночь (тёмно-фиолетовый фон, фиолетовый акцент)
                    bg = ParseHexColor("#120F1D");
                    cardBg = ParseHexColor("#1C172E");
                    cardSecBg = ParseHexColor("#26203D");
                    cardBorder = ParseHexColor("#3D305C");
                    textPrimary = ParseHexColor("#FFFFFF");
                    textSecondary = ParseHexColor("#C4B5FD");
                    textTertiary = ParseHexColor("#8B7AA8");
                    break;
            }

            // Записываем ресурсы в глобальные словари
            SetResourceBrush("ApplicationPageBackgroundThemeBrush", bg);
            SetResourceBrush("CardBackgroundFillColorDefaultBrush", cardBg);
            SetResourceBrush("CardBackgroundFillColorSecondaryBrush", cardSecBg);
            SetResourceBrush("CardStrokeColorDefaultBrush", cardBorder);
            SetResourceBrush("SurfaceStrokeColorDefaultBrush", cardBorder);
            SetResourceBrush("TextFillColorPrimaryBrush", textPrimary);
            SetResourceBrush("TextFillColorSecondaryBrush", textSecondary);
            SetResourceBrush("TextFillColorTertiaryBrush", textTertiary);

            // Применяем тему к окну и навигации
            if (App.MainWindow != null)
            {
                if (App.MainWindow.Content is FrameworkElement root)
                {
                    root.RequestedTheme = targetElementTheme;

                    if (root is Grid mainGrid)
                    {
                        mainGrid.Background = GetSolidBrush(bg);
                        foreach (var child in mainGrid.Children)
                        {
                            if (child is NavigationView nav)
                            {
                                nav.RequestedTheme = targetElementTheme;
                                nav.Background = GetSolidBrush(bg);
                            }
                        }
                    }
                }
            }

            // Переприменяем акцентный цвет
            ApplyAccentColor(App.Settings.Current.AccentColorTheme);
        }

        public static void ApplyAccentColor(string colorHex)
        {
            Color accentColor;
            if (string.IsNullOrEmpty(colorHex) || colorHex == "Default")
            {
                accentColor = App.Settings.Current.AppTheme switch
                {
                    "STORM DARK" => ParseHexColor("#00D2FF"),
                    "STORM NIGHT" => ParseHexColor("#00F0FF"),
                    "STORM DAY" => ParseHexColor("#0284C7"),
                    "STORM MATRIX" => ParseHexColor("#00FF66"),
                    "STORM CYBERPUNK" => ParseHexColor("#FF007F"),
                    "STORM FANTASY" => ParseHexColor("#F59E0B"),
                    "STORM WARHAMMER 40K" => ParseHexColor("#D4AF37"),
                    _ => ParseHexColor("#8E44AD") // STORM MIDNIGHT
                };
            }
            else
            {
                accentColor = ParseHexColor(colorHex);
            }

            Color dark1 = ChangeColorBrightness(accentColor, -0.15f);
            Color dark2 = ChangeColorBrightness(accentColor, -0.30f);
            Color light1 = ChangeColorBrightness(accentColor, 0.20f);
            Color light2 = ChangeColorBrightness(accentColor, 0.40f);

            SetResourceValue("SystemAccentColor", accentColor);
            SetResourceValue("SystemAccentColorDark1", dark1);
            SetResourceValue("SystemAccentColorDark2", dark2);
            SetResourceValue("SystemAccentColorLight1", light1);
            SetResourceValue("SystemAccentColorLight2", light2);

            SetResourceBrush("SystemControlHighlightAccentBrush", accentColor);
            SetResourceBrush("SystemControlHighlightListAccentLowBrush", Color.FromArgb(60, accentColor.R, accentColor.G, accentColor.B));
            SetResourceBrush("SystemAccentColorBrush", accentColor);

            // Кнопки, переключатели, фокусы и навигация
            SetResourceBrush("ToggleSwitchFillOn", accentColor);
            SetResourceBrush("ToggleSwitchFillOnPointerOver", light1);
            SetResourceBrush("ToggleSwitchFillOnPressed", dark1);
            SetResourceBrush("AccentButtonBackground", accentColor);
            SetResourceBrush("AccentButtonBackgroundPointerOver", light1);
            SetResourceBrush("AccentButtonBackgroundPressed", dark1);
            SetResourceBrush("NavigationViewItemForegroundSelected", accentColor);
            SetResourceBrush("NavigationViewItemIconForegroundSelected", accentColor);

            RefreshUI();
        }

        private static void SetResourceValue(string key, object value)
        {
            Application.Current.Resources[key] = value;

            if (Application.Current.Resources.ThemeDictionaries.TryGetValue("Dark", out var darkObj) && darkObj is ResourceDictionary darkDict)
                darkDict[key] = value;

            if (Application.Current.Resources.ThemeDictionaries.TryGetValue("Light", out var lightObj) && lightObj is ResourceDictionary lightDict)
                lightDict[key] = value;
        }

        private static void SetResourceBrush(string key, Color color)
        {
            SetBrushInDictionary(Application.Current.Resources, key, color);

            if (Application.Current.Resources.ThemeDictionaries.TryGetValue("Dark", out var darkObj) && darkObj is ResourceDictionary darkDict)
                SetBrushInDictionary(darkDict, key, color);

            if (Application.Current.Resources.ThemeDictionaries.TryGetValue("Light", out var lightObj) && lightObj is ResourceDictionary lightDict)
                SetBrushInDictionary(lightDict, key, color);
        }

        private static void SetBrushInDictionary(ResourceDictionary dict, string key, Color color)
        {
            // Всегда создаём новый объект кисти вместо мутации существующего,
            // т.к. системные WinUI ресурсные кисти являются frozen/sealed объектами
            // и попытка изменить .Color вызывает UnauthorizedAccessException.
            dict[key] = new SolidColorBrush(color);
        }

        private static SolidColorBrush GetSolidBrush(Color color)
        {
            return new SolidColorBrush(color);
        }

        private static void RefreshUI()
        {
            if (App.MainWindow?.Content is FrameworkElement root)
            {
                var targetTheme = (App.Settings.Current.AppTheme == "STORM DAY") ? ElementTheme.Light : ElementTheme.Dark;
                var oppositeTheme = (targetTheme == ElementTheme.Dark) ? ElementTheme.Light : ElementTheme.Dark;

                // Переключаем тему корневого элемента на противоположную и обратно,
                // чтобы WinUI 3 немедленно перевычислил все ThemeResource подписи во всём визуальном дереве
                root.RequestedTheme = oppositeTheme;
                root.RequestedTheme = targetTheme;

                if (root is Grid mainGrid)
                {
                    foreach (var child in mainGrid.Children)
                    {
                        if (child is NavigationView nav)
                        {
                            nav.RequestedTheme = oppositeTheme;
                            nav.RequestedTheme = targetTheme;
                            
                            if (nav.Content is Frame frame && frame.Content is Page activePage)
                            {
                                activePage.RequestedTheme = oppositeTheme;
                                activePage.RequestedTheme = targetTheme;
                            }
                        }
                    }
                }

                // Асинхронно подтверждаем обновление через 1 кадр на DispatcherQueue для 100% плавного и моментального изменения
                App.RunOnUI(async () =>
                {
                    await Task.Delay(16);
                    root.RequestedTheme = targetTheme;
                });
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
