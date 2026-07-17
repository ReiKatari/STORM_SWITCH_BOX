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
            this.Title = "STORM SWITCH BOX v3.5";
            this.ExtendsContentIntoTitleBar = true; // Современный заголовок окна
            
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
