using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;
using StormSwitchBox.Models;
using StormSwitchBox.Services;

namespace StormSwitchBox.Views
{
    public sealed partial class HistoryPage : Page
    {
        public ObservableCollection<ProcessingTask> HistoryTasks => HistoryService.HistoryTasks;
        private object? _itemAtPointerPressed;

        public HistoryPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
        }

        private async void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ContentDialog
            {
                Title = "Очистка истории",
                Content = "Вы уверены, что хотите полностью удалить историю обработок?",
                PrimaryButtonText = "Очистить",
                CloseButtonText = "Отмена",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                HistoryTasks.Clear();
                await HistoryService.SaveHistoryAsync();
            }
        }

        private void HistoryGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var dataGrid = sender as CommunityToolkit.WinUI.UI.Controls.DataGrid;
            if (dataGrid == null) return;

            var elements = VisualTreeHelper.FindElementsInHostCoordinates(e.GetCurrentPoint(this).Position, dataGrid);
            CommunityToolkit.WinUI.UI.Controls.DataGridRow? clickedRow = null;

            foreach (var element in elements)
            {
                var row = FindParent<CommunityToolkit.WinUI.UI.Controls.DataGridRow>(element as DependencyObject);
                if (row != null)
                {
                    clickedRow = row;
                    break;
                }
            }

            if (clickedRow != null && clickedRow.DataContext != null)
            {
                _itemAtPointerPressed = clickedRow.DataContext;
            }
            else
            {
                _itemAtPointerPressed = null;
            }
        }

        private void HistoryGrid_Tapped(object sender, TappedRoutedEventArgs e)
        {
            var dataGrid = sender as CommunityToolkit.WinUI.UI.Controls.DataGrid;
            if (dataGrid == null || _itemAtPointerPressed == null) return;

            if (dataGrid.SelectedItem == _itemAtPointerPressed)
            {
                dataGrid.SelectedItem = null;
                e.Handled = true;
            }
            _itemAtPointerPressed = null;
        }

        private T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            if (child == null) return null;
            DependencyObject? parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }
    }
}
