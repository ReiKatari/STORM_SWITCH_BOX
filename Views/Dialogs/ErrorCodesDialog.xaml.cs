using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using StormSwitchBox.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI;

namespace StormSwitchBox.Views.Dialogs
{
    public sealed partial class ErrorCodesDialog : ContentDialog
    {
        private sealed record ErrorCodeItem(
            string Category,
            string Code,
            string Module,
            string Title,
            string Cause,
            string Solution);

        private static readonly ErrorCodeItem[] AllErrors =
        {
            new(
                "fs",
                "2002-4153 / 2002-4000..4999",
                "Модуль 2002 (FS - Файловая система)",
                "Ошибка чтения NCA или повреждённый контейнер NSP/NSZ",
                "Повреждённый сектор на microSD (особенно при использовании exFAT) или устаревшие sigpatches / sys-patch.",
                "Проверьте целостность контейнера (Verify NSP), отформатируйте карту microSD в FAT32 (кластер 32 КБ) и обновите sys-patch."
            ),
            new(
                "crash",
                "2168-0002 (0x4a8)",
                "Модуль 2168 (Userland Crash / Data Abort)",
                "Аварийное завершение игры или хоумбрю (Нехватка ОЗУ / Конфликт мода)",
                "Запуск тяжёлого хоумбрю в Applet Mode (через Альбом) или несовместимый мод LayeredFS в папке /atmosphere/contents/<TitleID>/.",
                "Запускайте хоумбрю через созданный NSP-форвардер (вкладка Homebrew) или удерживайте кнопку R при старте любой игры."
            ),
            new(
                "keys",
                "2016-0641 / 2002-0001",
                "Модуль 2016 (NS - Менеджер контента)",
                "Невозможно запустить ПО (Отсутствуют Sigpatches / Sys-Patch)",
                "Система Horizon OS блокирует запуск сшитого мульти-контента или конвертированного NSP без патчей цифровой подписи.",
                "Установите актуальный модуль sys-patch (для HOS 17.0.0 - 21.0.0+) в папку /atmosphere/contents/0100000000000BEE/ и перезагрузите консоль."
            ),
            new(
                "crash",
                "2162-0002 (0x4a2)",
                "Модуль 2162 (General System / Sysmodule)",
                "Сбой системного демона Atmosphère",
                "Устаревшая кастомная тема (0100000000001000) или фоновый плагин (sys-clk, MissionControl, ovlloader) после обновления прошивки.",
                "Удалите папку /atmosphere/contents/0100000000001000 для сброса темы и обновите системные модули Atmosphere."
            ),
            new(
                "net",
                "2155-8007 / 2124-4007 / 2124-4508",
                "Модуль 2155 / 2124 (NPNS / Сеть Nintendo)",
                "Доступ к серверам Nintendo заблокирован (Exosphere / 90DNS)",
                "Сработала защита от бана консоли при попытке игры проверить лицензию или облачные сохранения на серверах Nintendo.",
                "Это штатная работа защиты: используйте утилиту Linkalho на консоли для привязки оффлайн-аккаунта."
            ),
            new(
                "fs",
                "2002-3502 / 2002-3580",
                "Модуль 2002 (MMC / eMMC / SD Card I/O)",
                "Аппаратная ошибка ввода-вывода карты microSD или памяти eMMC",
                "Отошёл контакт считывателя microSD на плате консоли, либо деградировали ячейки памяти карты при записи больших файлов.",
                "Проверьте состояние карты в Hekate -> Console Info -> microSD (раздел Errors и Vendor ID) и при необходимости замените карту."
            ),
            new(
                "fs",
                "2005-0003 / 2160-8006",
                "Модуль 2005 (Кэш установки)",
                "Недостаточно места или зависший PlaceHolder",
                "После прерванной установки остались незавершённые временные фрагменты в папке /Nintendo/Contents/placehld/.",
                "Откройте DBI на консоли и выполните «Cleanup orphaned files» (очистка осиротевших файлов) для освобождения места."
            ),
            new(
                "keys",
                "MasterKey / KeyGeneration Error",
                "Криптография NCA3 (master_key_00 .. master_key_16)",
                "Игра требует более новую прошивку (KeyGeneration 17..20 / HOS 18.0.0 - 21.0.0+)",
                "Новые игры и патчи зашифрованы свежими master_key. Старые prod.keys или низкая версия прошивки не позволяют расшифровать NCA.",
                "Скачайте свежие ключи prod.keys во вкладке «Параметры» и при необходимости обновите прошивку Horizon OS / эмулятор."
            ),
            new(
                "fs",
                "2002-2060 / 2002-2634",
                "Модуль 2002 (Gamecard / Lotus nogc)",
                "Сбой считывания слота картриджей (включён драйвер nogc)",
                "Загрузчик Hekate/Atmosphère по умолчанию блокирует контроллер слота картриджей (nogc = 1), чтобы не обновить его прошивку.",
                "Если нужно читать физический картридж, отключите опцию nogc в настройках Hekate (Options -> Auto NoGC) и перезагрузитесь."
            ),
            new(
                "crash",
                "2144-0001 / 2001-0123",
                "Модуль 2144 / 2001 (Homebrew Menu / Applet)",
                "Вылет меню хоумбрю или несовместимость NRO с текущей версией HOS",
                "После крупного обновления системы (HOS 19.0+ / 20.0+ / 21.0+) старый hbmenu.nro или бинарник хоумбрю использует устаревший ABI.",
                "Обновите /hbmenu.nro до последней релизной сборки из состава Atmosphère и пересоберите устаревшие NRO-приложения."
            )
        };

        private string _activeCategory = "all";
        private Action? _langHandler;

        public ErrorCodesDialog()
        {
            this.InitializeComponent();
            this.XamlRoot = App.MainWindow?.Content?.XamlRoot;
            this.RequestedTheme = (App.MainWindow?.Content as FrameworkElement)?.RequestedTheme ?? ElementTheme.Dark;

            _langHandler = () => App.RunOnUI(() =>
            {
                ApplyStaticLocalization();
                RenderCards();
            });

            this.Loaded += OnDialogLoaded;
            this.Unloaded += OnDialogUnloaded;
            this.Closed += OnDialogClosed;
        }

        private void OnDialogLoaded(object sender, RoutedEventArgs e)
        {
            if (this.XamlRoot != null)
            {
                this.XamlRoot.Changed += OnXamlRootChanged;
            }
            UpdateAdaptiveSize();
            App.Localization.RegisterRoot(this);
            App.Localization.LanguageChanged -= _langHandler;
            App.Localization.LanguageChanged += _langHandler;
            ApplyStaticLocalization();
            UpdateCategoryButtons();
            RenderCards();
        }

        private void OnDialogUnloaded(object sender, RoutedEventArgs e) => CleanupSubscriptions();

        private void OnDialogClosed(ContentDialog sender, ContentDialogClosedEventArgs args) => CleanupSubscriptions();

        private void CleanupSubscriptions()
        {
            if (this.XamlRoot != null)
            {
                this.XamlRoot.Changed -= OnXamlRootChanged;
            }
            App.Localization.UnregisterRoot(this);
            if (_langHandler != null)
            {
                App.Localization.LanguageChanged -= _langHandler;
            }
        }

        private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args)
        {
            UpdateAdaptiveSize();
        }

        private void UpdateAdaptiveSize()
        {
            if (RootFrame == null || this.XamlRoot == null) return;
            double hostW = this.XamlRoot.Size.Width;
            double hostH = this.XamlRoot.Size.Height;
            if (hostW > 320 && hostH > 240)
            {
                double minW = Math.Min(820.0, Math.Max(480.0, hostW - 32.0));
                double targetW = Math.Clamp(hostW - 80.0, minW, 1120.0);
                double minH = Math.Min(560.0, Math.Max(400.0, hostH - 40.0));
                double targetH = Math.Clamp(hostH - 96.0, minH, 760.0);

                RootFrame.Width = targetW;
                RootFrame.Height = targetH;
                this.Resources["ContentDialogMinWidth"] = targetW;
            }
        }

        private void ApplyStaticLocalization()
        {
            App.Localization.TranslateRoot(HeaderBanner);
            App.Localization.TranslateRoot(ToolbarPanel);
            App.Localization.TranslateRoot(EmptyStatePanel);
            App.Localization.TranslateRoot(FooterPanel);
        }

        private void ErrorSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RenderCards();
        }

        private void CategoryFilter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string cat && !string.IsNullOrWhiteSpace(cat))
            {
                _activeCategory = cat;
                UpdateCategoryButtons();
                RenderCards();
            }
        }

        private void ResetFilter_Click(object sender, RoutedEventArgs e)
        {
            _activeCategory = "all";
            if (ErrorSearchBox != null) ErrorSearchBox.Text = string.Empty;
            UpdateCategoryButtons();
            RenderCards();
        }

        private void UpdateCategoryButtons()
        {
            var accentBg = Application.Current.Resources["AccentButtonBackground"] as Brush ?? new SolidColorBrush(Color.FromArgb(255, 0, 120, 215));
            var accentFg = Application.Current.Resources["AccentButtonForeground"] as Brush ?? new SolidColorBrush(Color.FromArgb(255, 255, 255, 255));
            var cardBg = Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] as Brush ?? new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
            var cardBorder = Application.Current.Resources["CardStrokeColorDefaultBrush"] as Brush ?? new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
            var secondaryFg = Application.Current.Resources["TextFillColorSecondaryBrush"] as Brush ?? new SolidColorBrush(Color.FromArgb(255, 160, 160, 160));
            var transparent = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));

            Button[] buttons = { FilterAllButton, FilterFsButton, FilterCrashButton, FilterKeysButton, FilterNetButton };
            foreach (var btn in buttons)
            {
                if (btn == null) continue;
                bool isActive = string.Equals(btn.Tag as string, _activeCategory, StringComparison.OrdinalIgnoreCase);
                btn.Background = isActive ? accentBg : cardBg;
                btn.Foreground = isActive ? accentFg : secondaryFg;
                btn.BorderBrush = isActive ? transparent : cardBorder;
                btn.BorderThickness = isActive ? new Thickness(0) : new Thickness(1);
            }
        }

        private void RenderCards()
        {
            if (CardsHostPanel == null) return;
            CardsHostPanel.Children.Clear();

            string query = (ErrorSearchBox?.Text ?? string.Empty).Trim();
            var loc = App.Localization;

            var matched = new List<ErrorCodeItem>();
            foreach (var err in AllErrors)
            {
                if (_activeCategory != "all" && !string.Equals(err.Category, _activeCategory, StringComparison.OrdinalIgnoreCase))
                    continue;

                string locModule = loc.TranslateText(err.Module);
                string locTitle = loc.TranslateText(err.Title);
                string locCause = loc.TranslateText(err.Cause);
                string locSolution = loc.TranslateText(err.Solution);

                if (!string.IsNullOrEmpty(query) &&
                    !err.Code.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                    !err.Module.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                    !err.Title.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                    !err.Cause.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                    !err.Solution.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                    !locModule.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                    !locTitle.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                    !locCause.Contains(query, StringComparison.OrdinalIgnoreCase) &&
                    !locSolution.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                matched.Add(new ErrorCodeItem(err.Category, err.Code, locModule, locTitle, locCause, locSolution));
            }

            if (ResultsCountText != null)
            {
                string shownLabel = loc.TranslateText("Показано:");
                ResultsCountText.Text = $"{shownLabel} {matched.Count} / {AllErrors.Length}";
            }

            if (EmptyStatePanel != null)
            {
                EmptyStatePanel.Visibility = matched.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }

            foreach (var item in matched)
            {
                CardsHostPanel.Children.Add(BuildErrorCard(item));
            }
        }

        private UIElement BuildErrorCard(ErrorCodeItem item)
        {
            Color accentColor = item.Category switch
            {
                "crash" => Color.FromArgb(255, 239, 68, 68),
                "keys" => Color.FromArgb(255, 168, 85, 247),
                "net" => Color.FromArgb(255, 6, 182, 212),
                _ => Color.FromArgb(255, 245, 158, 11)
            };

            var cardBg = Application.Current.Resources["CardBackgroundFillColorDefaultBrush"] as Brush ?? new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
            var cardSecBg = Application.Current.Resources["CardBackgroundFillColorSecondaryBrush"] as Brush ?? new SolidColorBrush(Color.FromArgb(14, 255, 255, 255));
            var cardBorder = Application.Current.Resources["CardStrokeColorDefaultBrush"] as Brush ?? new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            var primaryText = Application.Current.Resources["TextFillColorPrimaryBrush"] as Brush ?? new SolidColorBrush(Color.FromArgb(255, 245, 245, 245));
            var secondaryText = Application.Current.Resources["TextFillColorSecondaryBrush"] as Brush ?? new SolidColorBrush(Color.FromArgb(255, 180, 180, 180));

            var outerBorder = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = cardBg,
                BorderBrush = cardBorder,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(14)
            };

            var mainGrid = new Grid { ColumnSpacing = 14 };
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var stripe = new Border
            {
                Width = 4,
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(accentColor),
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Grid.SetColumn(stripe, 0);
            mainGrid.Children.Add(stripe);

            var bodyStack = new StackPanel { Spacing = 10 };
            Grid.SetColumn(bodyStack, 1);

            // Top row: Code pill + Module pill + Copy button
            var topGrid = new Grid { ColumnSpacing = 10 };
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var badgesGrid = new Grid { ColumnSpacing = 8 };
            badgesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            badgesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var codePill = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 4, 10, 4),
                Background = new SolidColorBrush(Color.FromArgb(34, accentColor.R, accentColor.G, accentColor.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(110, accentColor.R, accentColor.G, accentColor.B)),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text = item.Code,
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(accentColor),
                    IsTextSelectionEnabled = true
                }
            };
            Grid.SetColumn(codePill, 0);
            badgesGrid.Children.Add(codePill);

            var modulePill = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 4, 10, 4),
                Background = cardSecBg,
                BorderBrush = cardBorder,
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock
                {
                    Text = item.Module,
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = secondaryText,
                    TextWrapping = TextWrapping.Wrap
                }
            };
            Grid.SetColumn(modulePill, 1);
            badgesGrid.Children.Add(modulePill);

            Grid.SetColumn(badgesGrid, 0);
            topGrid.Children.Add(badgesGrid);

            var copyLabel = new TextBlock
            {
                Text = App.Localization.TranslateText("Копировать"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
            var copyIcon = new FontIcon
            {
                Glyph = "\uE8C8",
                FontSize = 12
            };
            var copyContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            copyContent.Children.Add(copyIcon);
            copyContent.Children.Add(copyLabel);

            var copyBtn = new Button
            {
                Content = copyContent,
                Height = 30,
                Padding = new Thickness(10, 0, 10, 0),
                CornerRadius = new CornerRadius(7),
                Background = cardSecBg,
                BorderBrush = cardBorder,
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center
            };
            copyBtn.Click += async (_, _) =>
            {
                try
                {
                    var pkg = new DataPackage();
                    string causeHdr = App.Localization.TranslateText("Причина:");
                    string solHdr = App.Localization.TranslateText("Решение:");
                    pkg.SetText($"{item.Code} | {item.Module}\n{item.Title}\n{causeHdr} {item.Cause}\n{solHdr} {item.Solution}");
                    Clipboard.SetContent(pkg);

                    string origText = App.Localization.TranslateText("Копировать");
                    copyLabel.Text = App.Localization.TranslateText("Скопировано");
                    copyIcon.Glyph = "\uE73E";
                    await Task.Delay(1500);
                    copyLabel.Text = origText;
                    copyIcon.Glyph = "\uE8C8";
                }
                catch { }
            };
            Grid.SetColumn(copyBtn, 1);
            topGrid.Children.Add(copyBtn);

            bodyStack.Children.Add(topGrid);

            // Title
            bodyStack.Children.Add(new TextBlock
            {
                Text = item.Title,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = primaryText,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true
            });

            // Cause box
            var causeBox = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = cardSecBg,
                BorderBrush = cardBorder,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 9, 12, 9)
            };
            var causeGrid = new Grid { ColumnSpacing = 10 };
            causeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            causeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var causeGlyph = new FontIcon
            {
                Glyph = "\uE946",
                FontSize = 15,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 56, 189, 248)),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 0, 0)
            };
            Grid.SetColumn(causeGlyph, 0);
            causeGrid.Children.Add(causeGlyph);

            var causeStack = new StackPanel { Spacing = 2 };
            causeStack.Children.Add(new TextBlock
            {
                Text = App.Localization.TranslateText("Причина:"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 56, 189, 248))
            });
            causeStack.Children.Add(new TextBlock
            {
                Text = item.Cause,
                FontSize = 13,
                Foreground = primaryText,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true
            });
            Grid.SetColumn(causeStack, 1);
            causeGrid.Children.Add(causeStack);
            causeBox.Child = causeGrid;
            bodyStack.Children.Add(causeBox);

            // Solution box
            var solColor = Color.FromArgb(255, 16, 185, 129);
            var solBox = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(22, solColor.R, solColor.G, solColor.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(72, solColor.R, solColor.G, solColor.B)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 9, 12, 9)
            };
            var solGrid = new Grid { ColumnSpacing = 10 };
            solGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            solGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var solGlyph = new FontIcon
            {
                Glyph = "\uE90F",
                FontSize = 15,
                Foreground = new SolidColorBrush(solColor),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 0, 0)
            };
            Grid.SetColumn(solGlyph, 0);
            solGrid.Children.Add(solGlyph);

            var solStack = new StackPanel { Spacing = 2 };
            solStack.Children.Add(new TextBlock
            {
                Text = App.Localization.TranslateText("Решение:"),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(solColor)
            });
            solStack.Children.Add(new TextBlock
            {
                Text = item.Solution,
                FontSize = 13,
                Foreground = primaryText,
                TextWrapping = TextWrapping.Wrap,
                IsTextSelectionEnabled = true
            });
            Grid.SetColumn(solStack, 1);
            solGrid.Children.Add(solStack);
            solBox.Child = solGrid;
            bodyStack.Children.Add(solBox);

            mainGrid.Children.Add(bodyStack);
            outerBorder.Child = mainGrid;
            return outerBorder;
        }

        private void CloseDialog_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        private void Dialog_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Escape)
            {
                e.Handled = true;
                this.Hide();
            }
        }
    }
}
