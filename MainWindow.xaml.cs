using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using StormSwitchBox.Views;
using Windows.Graphics;
using WinForms = System.Windows.Forms;

namespace StormSwitchBox
{
    public sealed partial class MainWindow : Window
    {
        private WinForms.NotifyIcon? _notifyIcon;

        private static class Win32
        {
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool ShowWindow(System.IntPtr hWnd, int nCmdShow);
            
            [System.Runtime.InteropServices.DllImport("user32.dll")]
            public static extern bool SetForegroundWindow(System.IntPtr hWnd);
            
            public const int SW_RESTORE = 9;
        }

        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "STORM SWITCH BOX v4.3.2";
            this.ExtendsContentIntoTitleBar = true; // Современный заголовок окна

            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.SetIcon(System.IO.Path.Combine(System.AppContext.BaseDirectory, "assets", "storm_switch_box.ico"));
            
            // Безопасное включение фонового эффекта в зависимости от ОС (Mica на Win11, Acrylic на Win10)
            if (Microsoft.UI.Composition.SystemBackdrops.MicaController.IsSupported())
            {
                this.SystemBackdrop = new MicaBackdrop();
            }
            else if (Microsoft.UI.Composition.SystemBackdrops.DesktopAcrylicController.IsSupported())
            {
                this.SystemBackdrop = new DesktopAcrylicBackdrop();
            }
            
            // Восстанавливаем размеры и позицию окна из настроек
            RestoreWindowState();
            
            // Инициализируем системный трей
            InitializeTrayIcon();
            this.AppWindow.Closing += AppWindow_Closing;

            // Применяем сохраненную тему и акцентный цвет
            Services.ThemeService.ApplyTheme(App.Settings.Current.AppTheme);
            Services.ThemeService.ApplyAccentColor(App.Settings.Current.AccentColorTheme);

            // Загружаем историю обработок
            _ = StormSwitchBox.Services.HistoryService.LoadHistoryAsync();
            
            // По умолчанию загружаем Мульти-контент
            MainNavigation.SelectedItem = MainNavigation.MenuItems[4];
        }

        private void RestoreWindowState()
        {
            var settings = App.Settings.Current;
            var appWindow = this.AppWindow;
            
            int width = settings.WindowWidth >= 600 ? settings.WindowWidth : 1200;
            int height = settings.WindowHeight >= 400 ? settings.WindowHeight : 800;
            appWindow.Resize(new SizeInt32(width, height));
            
            var point = new PointInt32(settings.WindowX, settings.WindowY);
            var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromPoint(point, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
            
            if (displayArea != null && settings.WindowX >= 0 && settings.WindowY >= 0 &&
                settings.WindowX < (displayArea.WorkArea.X + displayArea.WorkArea.Width - 100) &&
                settings.WindowY < (displayArea.WorkArea.Y + displayArea.WorkArea.Height - 100))
            {
                appWindow.Move(point);
            }
            else
            {
                var primaryArea = Microsoft.UI.Windowing.DisplayArea.Primary;
                if (primaryArea != null)
                {
                    int centerX = primaryArea.WorkArea.X + (primaryArea.WorkArea.Width - width) / 2;
                    int centerY = primaryArea.WorkArea.Y + (primaryArea.WorkArea.Height - height) / 2;
                    appWindow.Move(new PointInt32(Math.Max(0, centerX), Math.Max(0, centerY)));
                }
            }
        }

        public void NavigateToAction(string action, string[] paths, string? format = null)
        {
            // Map CLI action to XAML navigation Tag and nav item index
            var (tag, index) = action switch
            {
                "update"  => ("Update",  0),
                "unpack"  => ("Unpack",  1),
                "pack"    => ("Pack",    2),
                "convert" => ("Convert", 3),
                "multi"   => ("Multi",   4),
                _         => ("Multi",   4)
            };
            
            // Select the appropriate nav item
            MainNavigation.SelectedItem = MainNavigation.MenuItems[index];
            ContentFrame.Navigate(typeof(Views.TasksPage), new Views.TasksStartupArgs { Action = tag, Paths = paths, Format = format });
        }

        private void SaveWindowState()
        {
            var settings = App.Settings.Current;
            var appWindow = this.AppWindow;
            
            settings.WindowWidth = appWindow.Size.Width;
            settings.WindowHeight = appWindow.Size.Height;
            settings.WindowX = appWindow.Position.X;
            settings.WindowY = appWindow.Position.Y;
            
            _ = App.Settings.SaveAsync();
        }

        private void MainNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                ContentFrame.Navigate(typeof(SettingsPage));
            }
            else if (args.SelectedItemContainer != null)
            {
                var tag = args.SelectedItemContainer.Tag?.ToString();
                if (!string.IsNullOrEmpty(tag))
                {
                    if (tag == "History")
                    {
                        ContentFrame.Navigate(typeof(HistoryPage));
                    }
                    else if (tag == "Catalog")
                    {
                        ContentFrame.Navigate(typeof(CatalogPage));
                    }
                    else if (tag == "Instruction")
                    {
                        ContentFrame.Navigate(typeof(InstructionPage));
                    }
                    else
                    {
                        ContentFrame.Navigate(typeof(TasksPage), tag);
                    }
                }
            }
        }

        private void InitializeTrayIcon()
        {
            _notifyIcon = new WinForms.NotifyIcon();
            var iconPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "assets", "storm_switch_box.ico");
            if (System.IO.File.Exists(iconPath))
            {
                _notifyIcon.Icon = new System.Drawing.Icon(iconPath);
            }
            _notifyIcon.Text = "STORM_SWITCH_BOX";
            _notifyIcon.Visible = false;
            
            _notifyIcon.DoubleClick += (s, e) => RestoreWindow();
            
            var contextMenu = new WinForms.ContextMenuStrip();
            var restoreItem = new WinForms.ToolStripMenuItem("Развернуть", null, (s, e) => RestoreWindow());
            var exitItem = new WinForms.ToolStripMenuItem("Выход", null, (s, e) =>
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                SaveWindowState();
                Services.JobObjectManager.KillAllToolProcesses();
                System.Environment.Exit(0);
            });
            
            contextMenu.Items.Add(restoreItem);
            contextMenu.Items.Add(exitItem);
            _notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            args.Cancel = true;
            SaveWindowState();
            MinimizeToTray();
        }

        private void MinimizeToTray()
        {
            this.AppWindow.Hide();
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = true;
            }
        }

        public void RestoreWindow()
        {
            this.AppWindow.Show();
            if (this.AppWindow.Presenter is OverlappedPresenter presenter)
            {
                if (presenter.State == OverlappedPresenterState.Minimized)
                {
                    presenter.Restore();
                }
            }
            this.AppWindow.MoveInZOrderAtTop();
            
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Win32.ShowWindow(hWnd, Win32.SW_RESTORE);
            Win32.SetForegroundWindow(hWnd);
            
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
            }
        }

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            // Сохраняем позицию и размер окна перед закрытием
            SaveWindowState();
            
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            
            // Жестко завершаем процесс и все утилиты при закрытии
            Services.JobObjectManager.KillAllToolProcesses();
            System.Environment.Exit(0);
        }
    }
}
