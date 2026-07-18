using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using StormSwitchBox.Views;
using Windows.Graphics;

namespace StormSwitchBox
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "STORM SWITCH BOX v3.8.2";
            this.ExtendsContentIntoTitleBar = true; // Современный заголовок окна

            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.SetIcon(System.IO.Path.Combine(System.AppContext.BaseDirectory, "storm_switch_box.ico"));
            
            // Включаем эффект полупрозрачности Mica (как в Windows 11)
            this.SystemBackdrop = new MicaBackdrop();
            
            // Восстанавливаем размеры и позицию окна из настроек
            RestoreWindowState();
            
            // Загружаем историю обработок
            _ = StormSwitchBox.Services.HistoryService.LoadHistoryAsync();
            
            // По умолчанию загружаем Мульти-контент
            MainNavigation.SelectedItem = MainNavigation.MenuItems[4];
        }

        private void RestoreWindowState()
        {
            var settings = App.Settings.Current;
            var appWindow = this.AppWindow;
            
            if (settings.WindowWidth > 100 && settings.WindowHeight > 100)
            {
                appWindow.Resize(new SizeInt32(settings.WindowWidth, settings.WindowHeight));
            }
            
            if (settings.WindowX >= 0 && settings.WindowY >= 0)
            {
                appWindow.Move(new PointInt32(settings.WindowX, settings.WindowY));
            }
        }

        public void NavigateToAction(string action, string[] paths)
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
            ContentFrame.Navigate(typeof(Views.TasksPage), new Views.TasksStartupArgs { Action = tag, Paths = paths });
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

        private void Window_Closed(object sender, WindowEventArgs args)
        {
            // Сохраняем позицию и размер окна перед закрытием
            SaveWindowState();
            
            // Жестко завершаем процесс при закрытии окна, чтобы он не оставался висеть в памяти
            System.Environment.Exit(0);
        }
    }
}
