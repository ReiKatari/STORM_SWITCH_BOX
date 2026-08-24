using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using StormSwitchBox.ViewModels;
using System.Collections.ObjectModel;
using StormSwitchBox.Models;
using StormSwitchBox.Services;
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

            ApplyLocalization();
            App.Localization.LanguageChanged += () => App.RunOnUI(ApplyLocalization);
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

                // Применить переданный формат, если он указан
                if (!string.IsNullOrEmpty(startupArgs.Format))
                {
                    int formatIndex = startupArgs.Format.ToUpper() switch
                    {
                        "NSP" => 0,
                        "NSZ" => 1,
                        "XCI" => 2,
                        "XCZ" => 3,
                        _ => -1
                    };
                    if (formatIndex >= 0)
                    {
                        FormatComboBox.SelectedIndex = formatIndex;
                    }
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
            if (row != null && row.DataContext is ProcessingTask rowTask)
            {
                var point = e.GetCurrentPoint(grid);
                if (point.Properties.IsRightButtonPressed)
                {
                    grid.SelectedItem = rowTask;
                    _activeDetailTask = rowTask;
                    UpdateDetailsVisibility();
                    return;
                }

                if (grid.SelectedItem == rowTask)
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

        private ProcessingTask? GetTargetTask(object sender)
        {
            if (sender is FrameworkElement elem)
            {
                if (elem.Tag is ProcessingTask tagTask) return tagTask;
                if (elem.DataContext is ProcessingTask dcTask) return dcTask;
            }
            if (TasksGrid?.SelectedItem is ProcessingTask selTask) return selTask;
            if (VerifyGrid?.SelectedItem is ProcessingTask selVerTask) return selVerTask;
            if (_activeDetailTask != null) return _activeDetailTask;
            return null;
        }

        private void CopyLog_Click(object sender, RoutedEventArgs e)
        {
            var task = GetTargetTask(sender);
            if (task != null && !string.IsNullOrEmpty(task.LogDetails))
            {
                var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
                dp.SetText(task.LogDetails);
                Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
                App.Logger.Log("Журнал задачи скопирован в буфер обмена.", LogLevel.Info);
            }
        }

        private void OpenOutputFolder_Click(object sender, RoutedEventArgs e)
        {
            var task = GetTargetTask(sender);
            if (task == null) return;

            string folderToOpen = task.OutputFolder;
            if (string.IsNullOrEmpty(folderToOpen) || !System.IO.Directory.Exists(folderToOpen))
            {
                folderToOpen = App.Settings.Current.OutputFolder;
            }

            if (!string.IsNullOrEmpty(folderToOpen) && System.IO.Directory.Exists(folderToOpen))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = folderToOpen,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    App.Logger.Log($"Не удалось открыть папку: {ex.Message}", LogLevel.Warning);
                }
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
                var folderPath = await SystemDialogService.OpenFolderDialogAsync("Выберите папку для добавления файлов");
                if (!string.IsNullOrWhiteSpace(folderPath) && Directory.Exists(folderPath))
                {
                    await ViewModel.AddDroppedFilesBatchAsync(new List<string> { folderPath });
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

        // ===== Фильтрация таблицы =====
        private void TaskSearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyTaskFilters();
        private void TaskOpFilter_Changed(object sender, SelectionChangedEventArgs e) => ApplyTaskFilters();

        private void ApplyTaskFilters()
        {
            string search = TaskSearchBox?.Text?.Trim().ToLower() ?? "";
            string opFilter = (TaskOpFilter?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";

            if (string.IsNullOrEmpty(search) && string.IsNullOrEmpty(opFilter))
            {
                TasksGrid.ItemsSource = ViewModel.Tasks;
                return;
            }

            var filtered = new System.Collections.Generic.List<ProcessingTask>();
            foreach (var task in ViewModel.Tasks)
            {
                bool visible = true;
                if (!string.IsNullOrEmpty(search))
                {
                    bool matchName = (task.OutputFileName ?? "").ToLower().Contains(search);
                    bool matchOp = (task.OperationDisplay ?? "").ToLower().Contains(search);
                    visible = matchName || matchOp;
                }
                if (visible && !string.IsNullOrEmpty(opFilter))
                    visible = string.Equals(task.Operation, opFilter, System.StringComparison.OrdinalIgnoreCase);
                if (visible) filtered.Add(task);
            }
            TasksGrid.ItemsSource = filtered;
        }

        // ===== Переключение платформы =====
        private void PlatformToggle_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.TogglePlatform();
        }

        // ===== Смена формата Switch =====
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

        // ===== Смена формата 3DS =====
        private void FormatComboBox3ds_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item)
            {
                string format = item.Content?.ToString() ?? "3DS";
                ViewModel.SelectedFormatIndex3ds = cb.SelectedIndex;
                App.Settings.Current.SelectedFormatIndex3ds = cb.SelectedIndex;
                App.Settings.Current.DefaultFormat3ds = format;
                _ = App.Settings.SaveAsync();
            }
        }

        // ===== Отмена задачи =====
        private void CancelTask_Click(object sender, RoutedEventArgs e)
        {
            var task = GetTargetTask(sender);

            if (task != null && task.IsRunning)
            {
                task.Cancel();
                App.Logger.Log($"Задача отменена пользователем: {task.OutputFileName}", LogLevel.Warning);
                App.RunOnUI(() => task.LogDetails += "\n⚠️ [Пользователь] Обработка задачи была принудительно отменена.");
            }
        }

        // ===== Редактирование иконки и названия игры =====
        private async void EditMetadata_Click(object sender, RoutedEventArgs e)
        {
            var task = GetTargetTask(sender);

            if (task == null)
            {
                App.Logger.Log("Выберите задачу для редактирования метаданных.", LogLevel.Warning);
                return;
            }

            string? sourceFile = task.InputFiles?.FirstOrDefault(f => !System.IO.Directory.Exists(f) && 
                (f.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) || 
                 f.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) || 
                 f.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) ||
                 f.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase) ||
                 f.EndsWith(".3ds", StringComparison.OrdinalIgnoreCase) ||
                 f.EndsWith(".cci", StringComparison.OrdinalIgnoreCase) ||
                 f.EndsWith(".cia", StringComparison.OrdinalIgnoreCase) ||
                 f.EndsWith(".cxi", StringComparison.OrdinalIgnoreCase)));

            if (string.IsNullOrEmpty(sourceFile) || !System.IO.File.Exists(sourceFile))
            {
                App.Logger.Log("Не найден подходящий файл игры для извлечения метаданных.", LogLevel.Warning);
                return;
            }

            GameMetadataEditModel? model = task.CustomMetadata;
            if (model == null)
            {
                if (task.Is3dsTask || Nintendo3dsService.Is3dsExtension(Path.GetExtension(sourceFile)))
                {
                    var info3ds = App.Nintendo3ds.Parse3dsFile(sourceFile);
                    model = new GameMetadataEditModel
                    {
                        SourceFilePath = sourceFile,
                        TitleNameEnglish = !string.IsNullOrEmpty(info3ds?.GameName) ? info3ds.GameName : task.GameName,
                        TitleNameRussian = !string.IsNullOrEmpty(info3ds?.GameName) ? info3ds.GameName : task.GameName,
                        Publisher = !string.IsNullOrEmpty(info3ds?.Publisher) ? info3ds.Publisher : "Nintendo",
                        OriginalIconBytes = info3ds?.IconBytes
                    };
                }
                else
                {
                    model = await App.ControlEditor.ExtractMetadataAsync(sourceFile);
                }
            }

            if (model == null)
            {
                model = new GameMetadataEditModel
                {
                    SourceFilePath = sourceFile,
                    TitleNameEnglish = task.GameName,
                    TitleNameRussian = task.GameName
                };
            }

            model.HasRomFs = task.HasRomFs != "-";
            model.HasExeFs = task.HasExeFs != "-";
            model.ModNameRomFs = task.ModNameRomFs;
            model.ModNameExeFs = task.ModNameExeFs;

            var dialog = new Dialogs.ControlEditorDialog(model);
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                task.CustomMetadata = model;
                task.ModNameRomFs = model.ModNameRomFs;
                task.ModNameExeFs = model.ModNameExeFs;

                if (!string.IsNullOrEmpty(model.TitleNameRussian))
                {
                    task.GameName = model.TitleNameRussian;
                }
                else if (!string.IsNullOrEmpty(model.TitleNameEnglish))
                {
                    task.GameName = model.TitleNameEnglish;
                }

                if (model.CustomIconBytes != null && model.CustomIconBytes.Length > 0)
                {
                    using var ms = new System.IO.MemoryStream(model.CustomIconBytes);
                    var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                    bmp.SetSource(ms.AsRandomAccessStream());
                    task.GameIcon = bmp;
                }

                App.Logger.Log($"Кастомные метаданные сохранены для задачи: {task.GameName}", LogLevel.Success);
            }
        }

        // ===== Удаление задачи =====
        private void DeleteTask_Click(object sender, RoutedEventArgs e)
        {
            var task = GetTargetTask(sender);

            if (task != null && !task.IsRunning)
            {
                ViewModel.DeleteTaskCommand.Execute(task);
            }
        }

        // ===== Список файлов (широкий диалог с бэйджами) =====
        private async void FilesCount_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ProcessingTask task)
            {
                if (task.FilesList == null || task.FilesList.Count == 0) return;

                var stackPanel = new StackPanel
                {
                    Spacing = 8,
                    Margin = new Thickness(0, 4, 16, 16)
                };

                foreach (var file in task.FilesList)
                {
                    string fileName = System.IO.Path.GetFileName(file);
                    var (label, bgBrushHex, fgBrushHex) = ClassifyFileForUi(fileName, file);

                    var rowGrid = new Grid
                    {
                        ColumnDefinitions =
                        {
                            new ColumnDefinition { Width = GridLength.Auto },
                            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                        },
                        Margin = new Thickness(0, 2, 0, 2)
                    };

                    var badgeBorder = new Border
                    {
                        Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(ParseColor(bgBrushHex)),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(8, 3, 8, 3),
                        Margin = new Thickness(0, 0, 12, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var badgeText = new TextBlock
                    {
                        Text = label,
                        Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(ParseColor(fgBrushHex)),
                        FontSize = 11,
                        FontWeight = Microsoft.UI.Text.FontWeights.Bold
                    };
                    badgeBorder.Child = badgeText;
                    Grid.SetColumn(badgeBorder, 0);

                    var fileNameText = new TextBlock
                    {
                        Text = fileName,
                        FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas"),
                        FontSize = 13,
                        VerticalAlignment = VerticalAlignment.Center,
                        IsTextSelectionEnabled = true,
                        TextWrapping = TextWrapping.Wrap
                    };
                    Grid.SetColumn(fileNameText, 1);

                    rowGrid.Children.Add(badgeBorder);
                    rowGrid.Children.Add(fileNameText);

                    stackPanel.Children.Add(rowGrid);
                }

                var scrollViewer = new ScrollViewer
                {
                    MaxHeight = 680,
                    Padding = new Thickness(0, 0, 16, 20),
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content = stackPanel
                };

                var dialog = new ContentDialog
                {
                    Title = $"Список файлов ({task.FilesList.Count})",
                    CloseButtonText = "Закрыть",
                    XamlRoot = this.XamlRoot,
                    MinWidth = 1425,
                    MaxWidth = 1725,
                    Content = scrollViewer
                };

                await dialog.ShowAsync();
            }
        }

        private static (string Label, string BgHex, string FgHex) ClassifyFileForUi(string fileName, string fullPath)
        {
            long sizeBytes = 0;
            try { if (System.IO.File.Exists(fullPath)) sizeBytes = new System.IO.FileInfo(fullPath).Length; } catch { }

            string tid = "";
            var match = System.Text.RegularExpressions.Regex.Match(fileName, @"\[([0-9A-Fa-f]{16})\]");
            if (match.Success) tid = match.Groups[1].Value.ToUpperInvariant();

            bool isDlc = (!string.IsNullOrEmpty(tid) && tid.Length == 16 && !tid.EndsWith("000") && !tid.EndsWith("800")) ||
                         fileName.Contains("DLC", StringComparison.OrdinalIgnoreCase) ||
                         fileName.Contains("AddOn", StringComparison.OrdinalIgnoreCase);

            if (isDlc)
            {
                if ((sizeBytes > 0 && sizeBytes < 1024 * 1024) || fileName.Contains("Unlock", StringComparison.OrdinalIgnoreCase))
                {
                    return ("РАЗБЛОКИРОВЩИК", "#D35400", "#FFFFFF");
                }
                return ("ДОПОЛНЕНИЕ", "#8E44AD", "#FFFFFF");
            }

            bool isPatch = (!string.IsNullOrEmpty(tid) && tid.Length == 16 && tid.EndsWith("800")) ||
                           (fileName.Contains("[v") && !fileName.Contains("[v0]")) ||
                           fileName.Contains("Update", StringComparison.OrdinalIgnoreCase) ||
                           fileName.Contains("Patch", StringComparison.OrdinalIgnoreCase);

            if (isPatch)
            {
                return ("ОБНОВЛЕНИЕ", "#2980B9", "#FFFFFF");
            }

            return ("ИГРА", "#27AE60", "#FFFFFF");
        }

        private static Windows.UI.Color ParseColor(string hex)
        {
            hex = hex.TrimStart('#');
            byte a = 255;
            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
            return Windows.UI.Color.FromArgb(a, r, g, b);
        }

        private static Windows.UI.Color fgHexToColor(string hex)
        {
            return ParseColor(hex);
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
                var folder = await SystemDialogService.OpenFolderDialogAsync(
                    "Выберите выходную папку задачи",
                    !string.IsNullOrEmpty(task?.OutputFolder) ? task.OutputFolder : null);

                if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder) && task != null)
                {
                    task.OutputFolder = folder;
                }
            }
            catch (Exception ex)
            {
                App.Logger.Log($"Ошибка при выборе выходной папки: {ex.Message}", LogLevel.Warning);
            }
        }

        // ===== Drag-and-Drop основная зона =====
        private void Grid_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Добавить файлы в Задачник";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsContentVisible = true;
            e.DragUIOverride.IsGlyphVisible = true;
            DropOverlay.Visibility = Visibility.Visible;
            e.Handled = true;
        }

        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            DropOverlay.Visibility = Visibility.Collapsed;
            var deferral = e.GetDeferral();
            try
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                
                if (e.DataView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.StorageItems))
                {
                    var items = await e.DataView.GetStorageItemsAsync();
                    if (items != null && items.Count > 0)
                    {
                        var paths = items.Select(item => item.Path).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
                        if (paths.Count > 0)
                        {
                            await ViewModel.AddDroppedFilesBatchAsync(paths);
                        }
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

        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            if (AppLogs != null) AppLogs.Clear();
        }

        private void CopyLogs_Click(object sender, RoutedEventArgs e)
        {
            if (AppLogs == null || AppLogs.Count == 0) return;
            var sb = new System.Text.StringBuilder();
            foreach (var log in AppLogs)
            {
                sb.AppendLine($"[{log.FormattedTime}] [{log.LevelLabel.Trim()}] {log.Message}");
            }
            var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
            dp.SetText(sb.ToString());
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
        }

        public void ApplyLocalization()
        {
            var loc = App.Localization;
            if (AddButton != null) AddButton.Label = loc["Tasks_AddFiles"];
            if (AddFilesItem != null) AddFilesItem.Text = loc["Tasks_AddFiles"] + "...";
            if (AddFolderItem != null) AddFolderItem.Text = loc["Tasks_AddFolder"] + "...";
            if (StartAllButton != null) StartAllButton.Label = loc["Tasks_StartAll"];
            if (StopAllButton != null) StopAllButton.Label = loc["Tasks_StopAll"];
            if (ClearButton != null) ClearButton.Label = loc["Tasks_ClearList"];
            if (ColumnsFlyoutTitle != null) ColumnsFlyoutTitle.Text = loc["Tasks_Col_Actions"];
        }
    }
}

