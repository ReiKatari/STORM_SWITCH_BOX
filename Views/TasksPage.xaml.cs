using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using StormSwitchBox.ViewModels;
using System.Collections.ObjectModel;
using StormSwitchBox.Models;
using System;
using System.Text;
using Microsoft.UI.Xaml.Media;

namespace StormSwitchBox.Views
{
    public sealed partial class TasksPage : Page
    {
        public TasksViewModel ViewModel => App.TasksVM;

        public ObservableCollection<LogMessage> AppLogs => App.Logger.Logs;

        public TasksPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = Microsoft.UI.Xaml.Navigation.NavigationCacheMode.Required;
            FormatComboBox.SelectedIndex = App.Settings.Current.SelectedFormatIndex;
            
            this.Loaded += (s, e) =>
            {
                if (App.Settings.Current.LogPanelHeight > 50)
                {
                    LogRow.Height = new GridLength(App.Settings.Current.LogPanelHeight);
                }
            };

            // Перехватываем PointerPressed ДО того, как DataGrid его обработает и изменит Selection
            TasksGrid.AddHandler(Microsoft.UI.Xaml.UIElement.PointerPressedEvent, new Microsoft.UI.Xaml.Input.PointerEventHandler(TasksGrid_PointerPressed), true);
        }

        private void LogResizer_DragDelta(object sender, Microsoft.UI.Xaml.Controls.Primitives.DragDeltaEventArgs e)
        {
            double newHeight = LogRow.Height.Value - e.VerticalChange;
            if (newHeight >= 50 && newHeight <= 500)
            {
                LogRow.Height = new GridLength(newHeight);
            }
        }

        private async void LogResizer_DragCompleted(object sender, Microsoft.UI.Xaml.Controls.Primitives.DragCompletedEventArgs e)
        {
            App.Settings.Current.LogPanelHeight = LogRow.Height.Value;
            await App.Settings.SaveAsync();
        }

        private void LogResizerThumb_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            this.ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeNorthSouth);
        }

        private void LogResizerThumb_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            this.ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.Arrow);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter is TasksStartupArgs startupArgs)
            {
                // Вызов из контекстного меню: задать тип страницы, добавить файлы и запустить
                ViewModel.SetPageType(startupArgs.Action);
                TasksGrid.Visibility = Visibility.Visible;
                TasksGrid.ItemsSource = ViewModel.Tasks;
                if (VerifyGrid != null)
                {
                    VerifyGrid.Visibility = Visibility.Collapsed;
                }
                
                // Добавить файлы из аргументов командной строки
                if (startupArgs.Paths.Length > 0)
                {
                    await ViewModel.AddDroppedFilesBatchAsync(new System.Collections.Generic.List<string>(startupArgs.Paths));
                    
                    // Автоматически запустить обработку
                    if (ViewModel.Tasks.Count > 0)
                    {
                        ViewModel.StartAllTasksCommand.Execute(null);
                    }
                }
            }
            else if (e.Parameter is string pageType)
            {
                ViewModel.SetPageType(pageType);
                if (pageType == "Verify")
                {
                    TasksGrid.Visibility = Visibility.Collapsed;
                    if (VerifyGrid != null)
                    {
                        VerifyGrid.Visibility = Visibility.Visible;
                        VerifyGrid.ItemsSource = ViewModel.VerifyTasks;
                    }
                }
                else
                {
                    TasksGrid.Visibility = Visibility.Visible;
                    TasksGrid.ItemsSource = ViewModel.Tasks;
                    if (VerifyGrid != null)
                    {
                        VerifyGrid.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }

        private object? _itemAtPointerPressed;
        private ProcessingTask? _activeDetailTask;

        private void TasksGrid_PointerPressed(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var grid = sender as CommunityToolkit.WinUI.UI.Controls.DataGrid;
            if (grid == null) return;

            var originalSource = e.OriginalSource as FrameworkElement;
            if (originalSource == null) return;

            var row = FindParent<CommunityToolkit.WinUI.UI.Controls.DataGridRow>(originalSource);
            if (row != null && row.DataContext != null)
            {
                if (grid.SelectedItem == row.DataContext)
                {
                    _activeDetailTask = null;
                    grid.SelectedItem = null;
                    UpdateDetailsVisibility();
                    e.Handled = true;
                    return;
                }
            }
            _itemAtPointerPressed = grid.SelectedItem;
        }

        private void DataGrid_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            // Нажатия на строки обрабатываются в PointerPressed для более надежного переключения
        }

        private void TasksGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var grid = sender as CommunityToolkit.WinUI.UI.Controls.DataGrid;
            if (grid?.SelectedItem is ProcessingTask selectedTask)
            {
                _activeDetailTask = selectedTask;
                UpdateDetailsVisibility();

                // Снимаем выбор с другого грида, чтобы избежать рассинхронизации
                if (grid == TasksGrid && VerifyGrid != null) VerifyGrid.SelectedItem = null;
                if (grid == VerifyGrid && TasksGrid != null) TasksGrid.SelectedItem = null;
            }
            else
            {
                // Если выбор сбросился, проверяем, существует ли еще выбранная задача
                bool stillExists = _activeDetailTask != null && (ViewModel.Tasks.Contains(_activeDetailTask) || ViewModel.VerifyTasks.Contains(_activeDetailTask));
                if (!stillExists)
                {
                    _activeDetailTask = null;
                    UpdateDetailsVisibility();
                }
                else
                {
                    // Сохраняем визуальное выделение в гриде, если оно пропало из-за рендеринга виртуализации
                    if (grid != null && grid.SelectedItem == null && _activeDetailTask != null)
                    {
                        var taskToSelect = _activeDetailTask;
                        App.MainDispatcher?.TryEnqueue(() =>
                        {
                            if (grid.SelectedItem == null && (ViewModel.Tasks.Contains(taskToSelect) || ViewModel.VerifyTasks.Contains(taskToSelect)))
                            {
                                grid.SelectedItem = taskToSelect;
                            }
                        });
                    }
                }
            }
        }

        private void UpdateDetailsVisibility()
        {
            if (_activeDetailTask != null)
            {
                if (NoSelectionPlaceholder != null) NoSelectionPlaceholder.Visibility = Visibility.Collapsed;
                if (DetailsContainer != null)
                {
                    DetailsContainer.Visibility = Visibility.Visible;
                    DetailsContainer.DataContext = _activeDetailTask;
                    if (ParamsColumn != null)
                    {
                        ParamsColumn.Width = _activeDetailTask.Operation == "Verify" ? new GridLength(0) : new GridLength(680);
                    }
                }
                if (DetailsPivot != null) DetailsPivot.SelectedIndex = 1;
            }
            else
            {
                if (NoSelectionPlaceholder != null) NoSelectionPlaceholder.Visibility = Visibility.Visible;
                if (DetailsContainer != null)
                {
                    DetailsContainer.Visibility = Visibility.Collapsed;
                    if (DetailsPivot != null) DetailsPivot.SelectedIndex = 0;
                }
            }
        }

        private void CopyLog_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is Models.ProcessingTask task)
            {
                var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dp.SetText(task.LogDetails ?? string.Empty);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();

        private void InitializePicker(object picker)
        {
            IntPtr hwnd = IntPtr.Zero;
            try
            {
                if (App.MainWindow != null)
                {
                    hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
                }
            }
            catch { }

            if (hwnd == IntPtr.Zero)
            {
                hwnd = GetActiveWindow();
            }
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        }

        private async void AddFiles_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
                picker.FileTypeFilter.Add(".nsp");
                picker.FileTypeFilter.Add(".nsz");
                picker.FileTypeFilter.Add(".xci");
                picker.FileTypeFilter.Add(".xcz");
                picker.FileTypeFilter.Add(".zip");
                picker.FileTypeFilter.Add(".rar");
                picker.FileTypeFilter.Add(".7z");

                InitializePicker(picker);

                var files = await picker.PickMultipleFilesAsync();
                if (files != null && files.Count > 0)
                {
                    var paths = files.Select(file => file.Path).ToList();
                    await ViewModel.AddDroppedFilesBatchAsync(paths);
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log("Ошибка при выборе файлов: " + ex.Message, LogLevel.Error);
            }
        }

        private async void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new Windows.Storage.Pickers.FolderPicker();
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
                picker.FileTypeFilter.Add("*");

                InitializePicker(picker);

                var folder = await picker.PickSingleFolderAsync();
                if (folder != null)
                {
                    await ViewModel.AddDroppedFilesBatchAsync(new List<string> { folder.Path });
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log("Ошибка при выборе папки: " + ex.Message, LogLevel.Error);
            }
        }

        private void TaskLogTextBlock_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is FrameworkElement fe && FindParent<ScrollViewer>(fe) is ScrollViewer sv)
            {
                // Позволяет ScrollViewer плавно прокрутиться до самого низа (авто-скролл) при изменении высоты лога
                sv.ChangeView(null, sv.ScrollableHeight, null, false);
            }
        }

        private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            if (child == null) return null;
            DependencyObject? parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }

        // ===== Видимость столбцов =====
        private void ColumnVisibility_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && int.TryParse(cb.Tag?.ToString(), out int colIndex))
            {
                if (TasksGrid != null && colIndex >= 0 && colIndex < TasksGrid.Columns.Count)
                {
                    TasksGrid.Columns[colIndex].Visibility = Visibility.Visible;
                }
            }
        }

        private void ColumnVisibility_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && int.TryParse(cb.Tag?.ToString(), out int colIndex))
            {
                if (TasksGrid != null && colIndex >= 0 && colIndex < TasksGrid.Columns.Count)
                {
                    TasksGrid.Columns[colIndex].Visibility = Visibility.Collapsed;
                }
            }
        }

        // ===== Свернуть все =====
        private void CollapseAll_Click(object sender, RoutedEventArgs e)
        {
            _activeDetailTask = null;
            UpdateDetailsVisibility();
            if (TasksGrid != null) TasksGrid.SelectedItem = null;
            if (VerifyGrid != null) VerifyGrid.SelectedItem = null;
        }

        // ===== Смена формата =====
        private void FormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item)
            {
                string format = item.Content?.ToString() ?? "NSP";
                ViewModel.SelectedFormat = format;
                ViewModel.SelectedFormatIndex = cb.SelectedIndex;
                App.Settings.Current.SelectedFormatIndex = cb.SelectedIndex;
                _ = App.Settings.SaveAsync();
            }
        }

        // ===== Удаление задачи =====
        private void DeleteTask_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ProcessingTask task)
            {
                if (!task.IsRunning)
                {
                    ViewModel.DeleteTaskCommand.Execute(task);
                }
            }
        }

        // ===== Список файлов (широкий диалог) =====
        private async void FilesCount_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ProcessingTask task)
            {
                if (task.FilesList == null || task.FilesList.Count == 0) return;

                var sb = new StringBuilder();
                foreach (var file in task.FilesList)
                {
                    // Показываем только имя файла, без папок
                    string fileName = System.IO.Path.GetFileName(file);
                    sb.AppendLine(fileName);
                }

                var dialog = new ContentDialog
                {
                    Title = $"Список файлов ({task.FilesList.Count})",
                    CloseButtonText = "Закрыть",
                    XamlRoot = this.XamlRoot,
                    MinWidth = 700,
                    Content = new ScrollViewer
                    {
                        MaxHeight = 500,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        Content = new TextBlock
                        {
                            Text = sb.ToString().TrimEnd(),
                            FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                            FontSize = 12,
                            IsTextSelectionEnabled = true,
                            TextWrapping = TextWrapping.NoWrap
                        }
                    }
                };

                await dialog.ShowAsync();
            }
        }

        // ===== Выходная папка — Drag-and-Drop на TextBox =====
        private void OutputFolder_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Установить выходную папку";
            e.Handled = true;
        }

        private async void OutputFolder_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0)
                {
                    var item = items[0];
                    string path = item.Path;
                    // Если перетащили файл — берем его директорию
                    if (System.IO.File.Exists(path))
                        path = System.IO.Path.GetDirectoryName(path) ?? path;

                    if (sender is TextBox textBox)
                    {
                        textBox.Text = path;
                        var task = textBox.DataContext as ProcessingTask;
                        if (task != null) task.OutputFolder = path;
                    }
                }
            }
        }

        // ===== Кнопка "Обзор..." для выходной папки задачи =====
        private async void BrowseOutputFolder_Click(object sender, RoutedEventArgs e)
        {
            ProcessingTask? task = null;
            if (sender is Button btn) task = btn.Tag as ProcessingTask;

            try
            {
                var picker = new Windows.Storage.Pickers.FolderPicker();
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
                picker.FileTypeFilter.Add("*");

                InitializePicker(picker);

                var folder = await picker.PickSingleFolderAsync();
                if (folder != null && task != null)
                {
                    task.OutputFolder = folder.Path;
                }
            }
            catch (Exception)
            {
                // Fallback: ручной ввод
                var dialog = new ContentDialog
                {
                    Title = "Укажите выходную папку",
                    CloseButtonText = "Отмена",
                    PrimaryButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                var textBox = new TextBox
                {
                    PlaceholderText = @"Пример: E:\OUT",
                    Text = task?.OutputFolder ?? "",
                    Width = 400
                };
                dialog.Content = textBox;

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary && task != null && !string.IsNullOrWhiteSpace(textBox.Text))
                {
                    task.OutputFolder = textBox.Text.Trim();
                }
            }
        }

        // ===== Drag-and-Drop основная зона =====
        private void Grid_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            DropOverlay.Visibility = Visibility.Visible;
            e.DragUIOverride.Caption = "Добавить файлы в Задачник";
        }

        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            var deferral = e.GetDeferral();
            try
            {
                DropOverlay.Visibility = Visibility.Collapsed;
                LoadingOverlay.Visibility = Visibility.Visible;
                
                if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
                {
                    var items = await e.DataView.GetStorageItemsAsync();
                    if (items != null && items.Count > 0)
                    {
                        var paths = items.Select(item => item.Path).ToList();
                        await ViewModel.AddDroppedFilesBatchAsync(paths);
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"Ошибка при добавлении файлов: {ex.Message}", Models.LogLevel.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                deferral.Complete();
            }

            // Проверка существующих файлов в выходной папке
            await CheckExistingFilesAsync();
        }

        private async System.Threading.Tasks.Task CheckExistingFilesAsync()
        {
            var tasksToRemove = new List<ProcessingTask>();

            foreach (var task in ViewModel.Tasks.Where(t => t.Status == "Ожидание"))
            {
                string ext = task.TargetFormat.ToLower();
                string outPath = System.IO.Path.Combine(task.OutputFolder, $"{task.OutputFileName}.{ext}");

                if (System.IO.File.Exists(outPath))
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Файл уже существует",
                        Content = $"В выходной папке уже есть файл:\n{task.OutputFileName}.{ext}\n\nЧто вы хотите сделать?",
                        PrimaryButtonText = "Заменить",
                        SecondaryButtonText = "Отменить задачу",
                        DefaultButton = ContentDialogButton.Secondary,
                        XamlRoot = this.XamlRoot
                    };

                    var result = await dialog.ShowAsync();
                    if (result == ContentDialogResult.Secondary)
                    {
                        tasksToRemove.Add(task);
                    }
                }
            }

            foreach (var task in tasksToRemove)
            {
                if (!task.IsRunning)
                {
                    ViewModel.Tasks.Remove(task);
                }
            }
        }

        private void Grid_DragLeave(object sender, DragEventArgs e)
        {
            DropOverlay.Visibility = Visibility.Collapsed;
        }

        // ===== Drag выходного имени файла =====
        private void OutputName_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Установить выходное имя";
            e.Handled = true;
        }

        private async void OutputName_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0)
                {
                    var item = items[0];
                    if (sender is TextBox textBox)
                    {
                        string name = System.IO.Path.GetFileNameWithoutExtension(item.Path);
                        textBox.Text = name;
                        var task = textBox.DataContext as ProcessingTask;
                        if (task != null) task.OutputFileName = name;
                    }
                }
            }
        }

        private void CopyLogs_Click(object sender, RoutedEventArgs e)
        {
            if (AppLogs == null || AppLogs.Count == 0) return;
            var sb = new System.Text.StringBuilder();
            foreach (var log in AppLogs)
            {
                sb.AppendLine(log.Message);
            }
            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dp.SetText(sb.ToString());
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
        }
    }
}

