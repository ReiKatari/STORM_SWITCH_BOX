using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using StormSwitchBox.Models;
using StormSwitchBox.ViewModels;
using WinRT;
using WinRT.Interop;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace StormSwitchBox.Views;

public sealed class TasksPage : Page, IComponentConnector
{
	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private interface ITasksPage_Bindings
	{
		void Initialize();

		void Update();

		void StopTracking();

		void DisconnectUnloadedObject(int connectionId);
	}

	private interface ITasksPage_BindingsScopeConnector
	{
		WeakReference Parent { get; set; }

		bool ContainsElement(int connectionId);

		void RegisterForElementConnection(int connectionId, IComponentConnector connector);
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	[DebuggerNonUserCode]
	private static class XamlBindingSetters
	{
		public static void Set_Microsoft_UI_Xaml_Controls_ItemsControl_ItemsSource(ItemsControl obj, object value, string targetNullValue)
		{
			if (value == null && targetNullValue != null)
			{
				value = XamlBindingHelper.ConvertValue(typeof(object), targetNullValue);
			}
			obj.ItemsSource = value;
		}

		public static void Set_CommunityToolkit_WinUI_UI_Controls_DataGrid_ItemsSource(DataGrid obj, IEnumerable value, string targetNullValue)
		{
			if (value == null && targetNullValue != null)
			{
				value = (IEnumerable)XamlBindingHelper.ConvertValue(typeof(IEnumerable), targetNullValue);
			}
			obj.ItemsSource = value;
		}

		public static void Set_Microsoft_UI_Xaml_Controls_Primitives_ButtonBase_Command(ButtonBase obj, ICommand value, string targetNullValue)
		{
			if (value == null && targetNullValue != null)
			{
				value = (ICommand)XamlBindingHelper.ConvertValue(typeof(ICommand), targetNullValue);
			}
			obj.Command = value;
		}

		public static void Set_Microsoft_UI_Xaml_Controls_Control_IsEnabled(Control obj, bool value)
		{
			obj.IsEnabled = value;
		}

		public static void Set_Microsoft_UI_Xaml_Controls_Primitives_Selector_SelectedIndex(Selector obj, int value)
		{
			obj.SelectedIndex = value;
		}

		public static void Set_Microsoft_UI_Xaml_Controls_TextBlock_Text(TextBlock obj, string value, string targetNullValue)
		{
			if (value == null && targetNullValue != null)
			{
				value = targetNullValue;
			}
			obj.Text = value ?? string.Empty;
		}

		public static void Set_Microsoft_UI_Xaml_Controls_TextBlock_Foreground(TextBlock obj, Brush value, string targetNullValue)
		{
			if (value == null && targetNullValue != null)
			{
				value = (Brush)XamlBindingHelper.ConvertValue(typeof(Brush), targetNullValue);
			}
			obj.Foreground = value;
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	[DebuggerNonUserCode]
	private class TasksPage_obj18_Bindings : IDataTemplateExtension, IDataTemplateComponent, IXamlBindScopeDiagnostics, IComponentConnector, ITasksPage_Bindings
	{
		private LogMessage dataRoot;

		private bool initialized = false;

		private const int NOT_PHASED = int.MinValue;

		private const int DATA_CHANGED = 1073741824;

		private bool removedDataContextHandler = false;

		private WeakReference obj18;

		private TextBlock obj19;

		private TextBlock obj20;

		private static bool isobj19TextDisabled;

		private static bool isobj20TextDisabled;

		private static bool isobj20ForegroundDisabled;

		public void Disable(int lineNumber, int columnNumber)
		{
			if (lineNumber == 348 && columnNumber == 68)
			{
				isobj19TextDisabled = true;
			}
			else if (lineNumber == 349 && columnNumber == 68)
			{
				isobj20TextDisabled = true;
			}
			else if (lineNumber == 349 && columnNumber == 92)
			{
				isobj20ForegroundDisabled = true;
			}
		}

		public void Connect(int connectionId, object target)
		{
			switch (connectionId)
			{
			case 18:
				obj18 = new WeakReference(target.As<Grid>());
				break;
			case 19:
				obj19 = target.As<TextBlock>();
				break;
			case 20:
				obj20 = target.As<TextBlock>();
				break;
			}
		}

		[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
		[DebuggerNonUserCode]
		public IComponentConnector GetBindingConnector(int connectionId, object target)
		{
			return null;
		}

		public void DataContextChangedHandler(FrameworkElement sender, DataContextChangedEventArgs args)
		{
			if (SetDataRoot(args.NewValue))
			{
				Update();
			}
		}

		public bool ProcessBinding(uint phase)
		{
			throw new NotImplementedException();
		}

		public int ProcessBindings(ContainerContentChangingEventArgs args)
		{
			int nextPhase = -1;
			ProcessBindings(args.Item, args.ItemIndex, (int)args.Phase, out nextPhase);
			return nextPhase;
		}

		public void ResetTemplate()
		{
			Recycle();
		}

		public void ProcessBindings(object item, int itemIndex, int phase, out int nextPhase)
		{
			nextPhase = -1;
			if (phase == 0)
			{
				nextPhase = -1;
				SetDataRoot(item);
				if (!removedDataContextHandler)
				{
					removedDataContextHandler = true;
					(obj18.Target as Grid).DataContextChanged -= DataContextChangedHandler;
				}
				initialized = true;
			}
			Update_(item.As<LogMessage>(), 1 << phase);
		}

		public void Recycle()
		{
		}

		public void Initialize()
		{
			if (!initialized)
			{
				Update();
			}
		}

		public void Update()
		{
			Update_(dataRoot, int.MinValue);
			initialized = true;
		}

		public void StopTracking()
		{
		}

		public void DisconnectUnloadedObject(int connectionId)
		{
			throw new ArgumentException("No unloadable elements to disconnect.");
		}

		public bool SetDataRoot(object newDataRoot)
		{
			if (newDataRoot != null)
			{
				dataRoot = newDataRoot.As<LogMessage>();
				return true;
			}
			return false;
		}

		private void Update_(LogMessage obj, int phase)
		{
			if (obj != null && (phase & -2147483647) != 0)
			{
				Update_FormattedTime(obj.FormattedTime, phase);
				Update_Message(obj.Message, phase);
				Update_ColorBrush(obj.ColorBrush, phase);
			}
		}

		private void Update_FormattedTime(string obj, int phase)
		{
			if ((phase & -2147483647) != 0 && !isobj19TextDisabled)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_TextBlock_Text(obj19, obj, null);
			}
		}

		private void Update_Message(string obj, int phase)
		{
			if ((phase & -2147483647) != 0 && !isobj20TextDisabled)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_TextBlock_Text(obj20, obj, null);
			}
		}

		private void Update_ColorBrush(SolidColorBrush obj, int phase)
		{
			if ((phase & -2147483647) != 0 && !isobj20ForegroundDisabled)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_TextBlock_Foreground(obj20, obj, null);
			}
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	[DebuggerNonUserCode]
	private class TasksPage_obj1_Bindings : IDataTemplateComponent, IXamlBindScopeDiagnostics, IComponentConnector, ITasksPage_Bindings
	{
		[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
		[DebuggerNonUserCode]
		private class TasksPage_obj1_BindingsTracking
		{
			private WeakReference<TasksPage_obj1_Bindings> weakRefToBindingObj;

			private TasksViewModel cache_ViewModel = null;

			public TasksPage_obj1_BindingsTracking(TasksPage_obj1_Bindings obj)
			{
				weakRefToBindingObj = new WeakReference<TasksPage_obj1_Bindings>(obj);
			}

			public TasksPage_obj1_Bindings TryGetBindingObject()
			{
				TasksPage_obj1_Bindings target = null;
				if (weakRefToBindingObj != null)
				{
					weakRefToBindingObj.TryGetTarget(out target);
					if (target == null)
					{
						weakRefToBindingObj = null;
						ReleaseAllListeners();
					}
				}
				return target;
			}

			public void ReleaseAllListeners()
			{
				UpdateChildListeners_ViewModel(null);
			}

			public void PropertyChanged_ViewModel(object sender, PropertyChangedEventArgs e)
			{
				TasksPage_obj1_Bindings tasksPage_obj1_Bindings = TryGetBindingObject();
				if (tasksPage_obj1_Bindings == null)
				{
					return;
				}
				string propertyName = e.PropertyName;
				TasksViewModel tasksViewModel = sender as TasksViewModel;
				if (string.IsNullOrEmpty(propertyName))
				{
					if (tasksViewModel != null)
					{
						tasksPage_obj1_Bindings.Update_ViewModel_IsAnyTaskRunning(tasksViewModel.IsAnyTaskRunning, 1073741824);
						tasksPage_obj1_Bindings.Update_ViewModel_SelectedFormatIndex(tasksViewModel.SelectedFormatIndex, 1073741824);
					}
					return;
				}
				string text = propertyName;
				string text2 = text;
				if (!(text2 == "IsAnyTaskRunning"))
				{
					if (text2 == "SelectedFormatIndex" && tasksViewModel != null)
					{
						tasksPage_obj1_Bindings.Update_ViewModel_SelectedFormatIndex(tasksViewModel.SelectedFormatIndex, 1073741824);
					}
				}
				else if (tasksViewModel != null)
				{
					tasksPage_obj1_Bindings.Update_ViewModel_IsAnyTaskRunning(tasksViewModel.IsAnyTaskRunning, 1073741824);
				}
			}

			public void UpdateChildListeners_ViewModel(TasksViewModel obj)
			{
				if (obj != cache_ViewModel)
				{
					if (cache_ViewModel != null)
					{
						((INotifyPropertyChanged)cache_ViewModel).PropertyChanged -= PropertyChanged_ViewModel;
						cache_ViewModel = null;
					}
					if (obj != null)
					{
						cache_ViewModel = obj;
						((INotifyPropertyChanged)obj).PropertyChanged += PropertyChanged_ViewModel;
					}
				}
			}

			public void RegisterTwoWayListener_52(ComboBox sourceObject)
			{
				sourceObject.RegisterPropertyChangedCallback(Selector.SelectedIndexProperty, delegate
				{
					TryGetBindingObject()?.UpdateTwoWay_52_SelectedIndex();
				});
			}
		}

		private TasksPage dataRoot;

		private bool initialized = false;

		private const int NOT_PHASED = int.MinValue;

		private const int DATA_CHANGED = 1073741824;

		private ListView obj16;

		private DataGrid obj23;

		private AppBarButton obj37;

		private AppBarButton obj38;

		private AppBarButton obj39;

		private AppBarButton obj41;

		private ComboBox obj52;

		private static bool isobj16ItemsSourceDisabled;

		private static bool isobj23ItemsSourceDisabled;

		private static bool isobj37CommandDisabled;

		private static bool isobj38CommandDisabled;

		private static bool isobj39CommandDisabled;

		private static bool isobj39IsEnabledDisabled;

		private static bool isobj41CommandDisabled;

		private static bool isobj52SelectedIndexDisabled;

		private TasksPage_obj1_BindingsTracking bindingsTracking;

		public TasksPage_obj1_Bindings()
		{
			bindingsTracking = new TasksPage_obj1_BindingsTracking(this);
		}

		public void Disable(int lineNumber, int columnNumber)
		{
			if (lineNumber == 334 && columnNumber == 70)
			{
				isobj16ItemsSourceDisabled = true;
			}
			else if (lineNumber == 102 && columnNumber == 51)
			{
				isobj23ItemsSourceDisabled = true;
			}
			else if (lineNumber == 34 && columnNumber == 50)
			{
				isobj37CommandDisabled = true;
			}
			else if (lineNumber == 47 && columnNumber == 49)
			{
				isobj38CommandDisabled = true;
			}
			else if (lineNumber == 52 && columnNumber == 46)
			{
				isobj39CommandDisabled = true;
			}
			else if (lineNumber == 52 && columnNumber == 95)
			{
				isobj39IsEnabledDisabled = true;
			}
			else if (lineNumber == 86 && columnNumber == 44)
			{
				isobj41CommandDisabled = true;
			}
			else if (lineNumber == 40 && columnNumber == 51)
			{
				isobj52SelectedIndexDisabled = true;
			}
		}

		public void Connect(int connectionId, object target)
		{
			switch (connectionId)
			{
			case 16:
				obj16 = target.As<ListView>();
				break;
			case 23:
				obj23 = target.As<DataGrid>();
				break;
			case 37:
				obj37 = target.As<AppBarButton>();
				break;
			case 38:
				obj38 = target.As<AppBarButton>();
				break;
			case 39:
				obj39 = target.As<AppBarButton>();
				break;
			case 41:
				obj41 = target.As<AppBarButton>();
				break;
			case 52:
				obj52 = target.As<ComboBox>();
				bindingsTracking.RegisterTwoWayListener_52(obj52);
				break;
			}
		}

		[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
		[DebuggerNonUserCode]
		public IComponentConnector GetBindingConnector(int connectionId, object target)
		{
			return null;
		}

		public void ProcessBindings(object item, int itemIndex, int phase, out int nextPhase)
		{
			nextPhase = -1;
		}

		public void Recycle()
		{
		}

		public void Initialize()
		{
			if (!initialized)
			{
				Update();
			}
		}

		public void Update()
		{
			Update_(dataRoot, int.MinValue);
			initialized = true;
		}

		public void StopTracking()
		{
			bindingsTracking.ReleaseAllListeners();
			initialized = false;
		}

		public void DisconnectUnloadedObject(int connectionId)
		{
			throw new ArgumentException("No unloadable elements to disconnect.");
		}

		public bool SetDataRoot(object newDataRoot)
		{
			bindingsTracking.ReleaseAllListeners();
			if (newDataRoot != null)
			{
				dataRoot = newDataRoot.As<TasksPage>();
				return true;
			}
			return false;
		}

		public void Activated(object obj, WindowActivatedEventArgs data)
		{
			Initialize();
		}

		public void Loading(FrameworkElement src, object data)
		{
			Initialize();
		}

		private void Update_(TasksPage obj, int phase)
		{
			if (obj != null)
			{
				if ((phase & -2147483647) != 0)
				{
					Update_AppLogs(obj.AppLogs, phase);
				}
				if ((phase & -1073741823) != 0)
				{
					Update_ViewModel(obj.ViewModel, phase);
				}
			}
		}

		private void Update_AppLogs(ObservableCollection<LogMessage> obj, int phase)
		{
			if ((phase & -2147483647) != 0 && !isobj16ItemsSourceDisabled)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_ItemsControl_ItemsSource(obj16, obj, null);
			}
		}

		private void Update_ViewModel(TasksViewModel obj, int phase)
		{
			bindingsTracking.UpdateChildListeners_ViewModel(obj);
			if (obj != null)
			{
				if ((phase & -2147483647) != 0)
				{
					Update_ViewModel_Tasks(obj.Tasks, phase);
					Update_ViewModel_AddTestTaskCommand(obj.AddTestTaskCommand, phase);
					Update_ViewModel_StartAllTasksCommand(obj.StartAllTasksCommand, phase);
					Update_ViewModel_StopAllTasksCommand(obj.StopAllTasksCommand, phase);
				}
				if ((phase & -1073741823) != 0)
				{
					Update_ViewModel_IsAnyTaskRunning(obj.IsAnyTaskRunning, phase);
				}
				if ((phase & -2147483647) != 0)
				{
					Update_ViewModel_ClearTasksCommand(obj.ClearTasksCommand, phase);
				}
				if ((phase & -1073741823) != 0)
				{
					Update_ViewModel_SelectedFormatIndex(obj.SelectedFormatIndex, phase);
				}
			}
		}

		private void Update_ViewModel_Tasks(ObservableCollection<ProcessingTask> obj, int phase)
		{
			if ((phase & -2147483647) != 0 && !isobj23ItemsSourceDisabled)
			{
				XamlBindingSetters.Set_CommunityToolkit_WinUI_UI_Controls_DataGrid_ItemsSource(obj23, obj, null);
			}
		}

		private void Update_ViewModel_AddTestTaskCommand(IRelayCommand obj, int phase)
		{
			if ((phase & -2147483647) != 0 && !isobj37CommandDisabled)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_Primitives_ButtonBase_Command(obj37, obj, null);
			}
		}

		private void Update_ViewModel_StartAllTasksCommand(IRelayCommand obj, int phase)
		{
			if ((phase & -2147483647) != 0 && !isobj38CommandDisabled)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_Primitives_ButtonBase_Command(obj38, obj, null);
			}
		}

		private void Update_ViewModel_StopAllTasksCommand(IRelayCommand obj, int phase)
		{
			if ((phase & -2147483647) != 0 && !isobj39CommandDisabled)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_Primitives_ButtonBase_Command(obj39, obj, null);
			}
		}

		private void Update_ViewModel_IsAnyTaskRunning(bool obj, int phase)
		{
			if ((phase & -1073741823) != 0 && !isobj39IsEnabledDisabled)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_Control_IsEnabled(obj39, obj);
			}
		}

		private void Update_ViewModel_ClearTasksCommand(IRelayCommand obj, int phase)
		{
			if ((phase & -2147483647) != 0 && !isobj41CommandDisabled)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_Primitives_ButtonBase_Command(obj41, obj, null);
			}
		}

		private void Update_ViewModel_SelectedFormatIndex(int obj, int phase)
		{
			if ((phase & -1073741823) != 0 && !isobj52SelectedIndexDisabled)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_Primitives_Selector_SelectedIndex(obj52, obj);
			}
		}

		private void UpdateTwoWay_52_SelectedIndex()
		{
			if (initialized && dataRoot != null && dataRoot.ViewModel != null)
			{
				dataRoot.ViewModel.SelectedFormatIndex = obj52.SelectedIndex;
			}
		}
	}

	private object? _itemAtPointerPressed;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private RowDefinition LogRow;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private Thumb LogResizerThumb;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private Border DropOverlay;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private Border LoadingOverlay;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private Pivot DetailsPivot;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private TextBlock NoSelectionPlaceholder;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private Grid DetailsContainer;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private ColumnDefinition ParamsColumn;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private ScrollViewer TaskLogScrollViewer;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private ListView LogsListView;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private DataGrid TasksGrid;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private DataGrid VerifyGrid;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private ComboBox FormatComboBox;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private bool _contentLoaded;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private ITasksPage_Bindings Bindings;

	public TasksViewModel ViewModel => App.TasksVM;

	public ObservableCollection<LogMessage> AppLogs => App.Logger.Logs;

	public TasksPage()
	{
		InitializeComponent();
		base.NavigationCacheMode = NavigationCacheMode.Required;
		FormatComboBox.SelectedIndex = App.Settings.Current.SelectedFormatIndex;
		base.Loaded += delegate
		{
			if (App.Settings.Current.LogPanelHeight > 50.0)
			{
				LogRow.Height = new GridLength(App.Settings.Current.LogPanelHeight);
			}
		};
		TasksGrid.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(TasksGrid_PointerPressed), handledEventsToo: true);
	}

	private void LogResizer_DragDelta(object sender, DragDeltaEventArgs e)
	{
		double num = LogRow.Height.Value - e.VerticalChange;
		if (num >= 50.0 && num <= 500.0)
		{
			LogRow.Height = new GridLength(num);
		}
	}

	private async void LogResizer_DragCompleted(object sender, DragCompletedEventArgs e)
	{
		App.Settings.Current.LogPanelHeight = LogRow.Height.Value;
		await App.Settings.SaveAsync();
	}

	private void LogResizerThumb_PointerEntered(object sender, PointerRoutedEventArgs e)
	{
		base.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeNorthSouth);
	}

	private void LogResizerThumb_PointerExited(object sender, PointerRoutedEventArgs e)
	{
		base.ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
	}

	protected override void OnNavigatedTo(NavigationEventArgs e)
	{
		base.OnNavigatedTo(e);
		if (!(e.Parameter is string text))
		{
			return;
		}
		ViewModel.SetPageType(text);
		if (text == "Verify")
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

	private void TasksGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
	{
		DataGrid dataGrid = sender as DataGrid;
		if (dataGrid == null)
		{
			return;
		}
		FrameworkElement frameworkElement = e.OriginalSource as FrameworkElement;
		if (!(frameworkElement == null))
		{
			DataGridRow dataGridRow = FindParent<DataGridRow>(frameworkElement);
			if (dataGridRow != null && dataGridRow.DataContext != null && dataGrid.SelectedItem == dataGridRow.DataContext)
			{
				dataGrid.SelectedItem = null;
				e.Handled = true;
			}
			else
			{
				_itemAtPointerPressed = dataGrid.SelectedItem;
			}
		}
	}

	private void DataGrid_Tapped(object sender, TappedRoutedEventArgs e)
	{
	}

	private void TasksGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		DataGrid dataGrid = sender as DataGrid;
		if (dataGrid?.SelectedItem is ProcessingTask processingTask)
		{
			if (NoSelectionPlaceholder != null)
			{
				NoSelectionPlaceholder.Visibility = Visibility.Collapsed;
			}
			if (DetailsContainer != null)
			{
				DetailsContainer.Visibility = Visibility.Visible;
				DetailsContainer.DataContext = processingTask;
				if (ParamsColumn != null)
				{
					ParamsColumn.Width = ((processingTask.Operation == "Verify") ? new GridLength(0.0) : new GridLength(680.0));
				}
			}
			if (DetailsPivot != null)
			{
				DetailsPivot.SelectedIndex = 1;
			}
			if (dataGrid == TasksGrid && VerifyGrid != null)
			{
				VerifyGrid.SelectedItem = null;
			}
			if (dataGrid == VerifyGrid && TasksGrid != null)
			{
				TasksGrid.SelectedItem = null;
			}
		}
		else if ((!(TasksGrid != null) || TasksGrid.SelectedItem == null) && (!(VerifyGrid != null) || VerifyGrid.SelectedItem == null))
		{
			if (NoSelectionPlaceholder != null)
			{
				NoSelectionPlaceholder.Visibility = Visibility.Visible;
			}
			if (DetailsContainer != null)
			{
				DetailsContainer.Visibility = Visibility.Collapsed;
			}
			if (DetailsPivot != null)
			{
				DetailsPivot.SelectedIndex = 0;
			}
		}
	}

	private void TaskLogTextBlock_SizeChanged(object sender, SizeChangedEventArgs e)
	{
		if (sender is FrameworkElement child)
		{
			ScrollViewer scrollViewer = FindParent<ScrollViewer>(child);
			scrollViewer?.ChangeView(null, scrollViewer.ScrollableHeight, null, disableAnimation: false);
		}
	}

	private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
	{
		if (child == null)
		{
			return null;
		}
		DependencyObject parent = VisualTreeHelper.GetParent(child);
		if (parent == null)
		{
			return null;
		}
		if (parent is T result)
		{
			return result;
		}
		return FindParent<T>(parent);
	}

	private void ColumnVisibility_Checked(object sender, RoutedEventArgs e)
	{
		if (sender is CheckBox { Tag: var tag } && int.TryParse(tag?.ToString(), out var result) && TasksGrid != null && result >= 0 && result < TasksGrid.Columns.Count)
		{
			TasksGrid.Columns[result].Visibility = Visibility.Visible;
		}
	}

	private void ColumnVisibility_Unchecked(object sender, RoutedEventArgs e)
	{
		if (sender is CheckBox { Tag: var tag } && int.TryParse(tag?.ToString(), out var result) && TasksGrid != null && result >= 0 && result < TasksGrid.Columns.Count)
		{
			TasksGrid.Columns[result].Visibility = Visibility.Collapsed;
		}
	}

	private void CollapseAll_Click(object sender, RoutedEventArgs e)
	{
		if (TasksGrid != null)
		{
			TasksGrid.SelectedItem = null;
		}
	}

	private void FormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (sender is ComboBox { SelectedItem: ComboBoxItem { Content: var content } } comboBox)
		{
			string selectedFormat = content?.ToString() ?? "NSP";
			ViewModel.SelectedFormat = selectedFormat;
			ViewModel.SelectedFormatIndex = comboBox.SelectedIndex;
			App.Settings.Current.SelectedFormatIndex = comboBox.SelectedIndex;
			App.Settings.SaveAsync();
		}
	}

	private void DeleteTask_Click(object sender, RoutedEventArgs e)
	{
		if (sender is Button { Tag: ProcessingTask { IsRunning: false } tag })
		{
			ViewModel.DeleteTaskCommand.Execute(tag);
		}
	}

	private async void FilesCount_Click(object sender, RoutedEventArgs e)
	{
		ProcessingTask task = default(ProcessingTask);
		int num;
		if (sender is Button { Tag: var tag })
		{
			task = tag as ProcessingTask;
			num = ((task != null) ? 1 : 0);
		}
		else
		{
			num = 0;
		}
		if (num == 0 || task.FilesList == null || task.FilesList.Count == 0)
		{
			return;
		}
		StringBuilder sb = new StringBuilder();
		foreach (string file in task.FilesList)
		{
			string fileName = Path.GetFileName(file);
			sb.AppendLine(fileName);
		}
		ContentDialog dialog = new ContentDialog
		{
			Title = $"Список файлов ({task.FilesList.Count})",
			CloseButtonText = "Закрыть",
			XamlRoot = base.XamlRoot,
			MinWidth = 700.0,
			Content = new ScrollViewer
			{
				MaxHeight = 500.0,
				HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
				Content = new TextBlock
				{
					Text = sb.ToString().TrimEnd(),
					FontFamily = new FontFamily("Consolas"),
					FontSize = 12.0,
					IsTextSelectionEnabled = true,
					TextWrapping = TextWrapping.NoWrap
				}
			}
		};
		await dialog.ShowAsync();
	}

	private void OutputFolder_DragOver(object sender, DragEventArgs e)
	{
		e.AcceptedOperation = DataPackageOperation.Copy;
		e.DragUIOverride.Caption = "Установить выходную папку";
		e.Handled = true;
	}

	private async void OutputFolder_Drop(object sender, DragEventArgs e)
	{
		e.Handled = true;
		if (!e.DataView.Contains(StandardDataFormats.StorageItems))
		{
			return;
		}
		IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();
		if (items.Count <= 0)
		{
			return;
		}
		IStorageItem item = items[0];
		string path = item.Path;
		if (File.Exists(path))
		{
			path = Path.GetDirectoryName(path) ?? path;
		}
		if (sender is TextBox textBox)
		{
			textBox.Text = path;
			if (textBox.DataContext is ProcessingTask task)
			{
				task.OutputFolder = path;
			}
		}
	}

	private async void BrowseOutputFolder_Click(object sender, RoutedEventArgs e)
	{
		ProcessingTask task = null;
		if (sender is Button btn)
		{
			task = btn.Tag as ProcessingTask;
		}
		try
		{
			FolderPicker picker = new FolderPicker
			{
				SuggestedStartLocation = PickerLocationId.Desktop,
				FileTypeFilter = { "*" }
			};
			Window window = App.MainWindow;
			if (window != null)
			{
				nint hwnd = WindowNative.GetWindowHandle(window);
				InitializeWithWindow.Initialize(picker, hwnd);
			}
			StorageFolder folder = await picker.PickSingleFolderAsync();
			if (folder != null && task != null)
			{
				task.OutputFolder = folder.Path;
			}
		}
		catch (Exception)
		{
			ContentDialog dialog = new ContentDialog
			{
				Title = "Укажите выходную папку",
				CloseButtonText = "Отмена",
				PrimaryButtonText = "OK",
				XamlRoot = base.XamlRoot
			};
			TextBox textBox = (TextBox)(dialog.Content = new TextBox
			{
				PlaceholderText = "Например: E:\\OUT",
				Text = (task?.OutputFolder ?? ""),
				Width = 400.0
			});
			if (await dialog.ShowAsync() == ContentDialogResult.Primary && task != null && !string.IsNullOrWhiteSpace(textBox.Text))
			{
				task.OutputFolder = textBox.Text.Trim();
			}
		}
	}

	private void Grid_DragOver(object sender, DragEventArgs e)
	{
		e.AcceptedOperation = DataPackageOperation.Copy;
		DropOverlay.Visibility = Visibility.Visible;
		e.DragUIOverride.Caption = "Добавить файлы в Задачник";
	}

	private async void Grid_Drop(object sender, DragEventArgs e)
	{
		DragOperationDeferral deferral = e.GetDeferral();
		try
		{
			DropOverlay.Visibility = Visibility.Collapsed;
			LoadingOverlay.Visibility = Visibility.Visible;
			if (e.DataView.Contains(StandardDataFormats.StorageItems))
			{
				foreach (IStorageItem item in await e.DataView.GetStorageItemsAsync())
				{
					await ViewModel.AddDroppedFileAsync(item.Path);
				}
			}
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			App.Logger.Log("Ошибка при добавлении файлов: " + ex2.Message, LogLevel.Error);
		}
		finally
		{
			LoadingOverlay.Visibility = Visibility.Collapsed;
			deferral.Complete();
		}
		await CheckExistingFilesAsync();
	}

	private async Task CheckExistingFilesAsync()
	{
		List<ProcessingTask> tasksToRemove = new List<ProcessingTask>();
		foreach (ProcessingTask task in ViewModel.Tasks.Where((ProcessingTask t) => t.Status == "Ожидание"))
		{
			string ext = task.TargetFormat.ToLower();
			string outPath = Path.Combine(task.OutputFolder, task.OutputFileName + "." + ext);
			if (File.Exists(outPath))
			{
				ContentDialog dialog = new ContentDialog
				{
					Title = "Файл уже существует",
					Content = $"В выходной папке уже есть файл:\n{task.OutputFileName}.{ext}\n\nЧто вы хотите сделать?",
					PrimaryButtonText = "Заменить",
					SecondaryButtonText = "Отменить задачу",
					DefaultButton = ContentDialogButton.Secondary,
					XamlRoot = base.XamlRoot
				};
				if (await dialog.ShowAsync() == ContentDialogResult.Secondary)
				{
					tasksToRemove.Add(task);
				}
			}
		}
		foreach (ProcessingTask task2 in tasksToRemove)
		{
			if (!task2.IsRunning)
			{
				ViewModel.Tasks.Remove(task2);
			}
		}
	}

	private void Grid_DragLeave(object sender, DragEventArgs e)
	{
		DropOverlay.Visibility = Visibility.Collapsed;
	}

	private void OutputName_DragOver(object sender, DragEventArgs e)
	{
		e.AcceptedOperation = DataPackageOperation.Copy;
		e.DragUIOverride.Caption = "Установить выходное имя";
		e.Handled = true;
	}

	private async void OutputName_Drop(object sender, DragEventArgs e)
	{
		e.Handled = true;
		if (!e.DataView.Contains(StandardDataFormats.StorageItems))
		{
			return;
		}
		IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();
		if (items.Count <= 0)
		{
			return;
		}
		IStorageItem item = items[0];
		if (sender is TextBox textBox)
		{
			string name = (textBox.Text = Path.GetFileNameWithoutExtension(item.Path));
			if (textBox.DataContext is ProcessingTask task)
			{
				task.OutputFileName = name;
			}
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	[DebuggerNonUserCode]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("ms-appx:///Views/TasksPage.xaml");
			Application.LoadComponent(this, resourceLocator, ComponentResourceLocation.Application);
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	[DebuggerNonUserCode]
	public void Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 2:
		{
			Grid grid = target.As<Grid>();
			grid.DragOver += Grid_DragOver;
			grid.Drop += Grid_Drop;
			grid.DragLeave += Grid_DragLeave;
			break;
		}
		case 3:
			LogRow = target.As<RowDefinition>();
			break;
		case 4:
			LogResizerThumb = target.As<Thumb>();
			LogResizerThumb.DragDelta += LogResizer_DragDelta;
			LogResizerThumb.DragCompleted += LogResizer_DragCompleted;
			LogResizerThumb.PointerEntered += LogResizerThumb_PointerEntered;
			LogResizerThumb.PointerExited += LogResizerThumb_PointerExited;
			break;
		case 5:
			DropOverlay = target.As<Border>();
			break;
		case 6:
			LoadingOverlay = target.As<Border>();
			break;
		case 7:
			DetailsPivot = target.As<Pivot>();
			break;
		case 8:
			NoSelectionPlaceholder = target.As<TextBlock>();
			break;
		case 9:
			DetailsContainer = target.As<Grid>();
			break;
		case 10:
			ParamsColumn = target.As<ColumnDefinition>();
			break;
		case 11:
			TaskLogScrollViewer = target.As<ScrollViewer>();
			break;
		case 12:
		{
			TextBlock textBlock = target.As<TextBlock>();
			textBlock.SizeChanged += TaskLogTextBlock_SizeChanged;
			break;
		}
		case 13:
		{
			TextBox textBox2 = target.As<TextBox>();
			textBox2.DragOver += OutputName_DragOver;
			textBox2.Drop += OutputName_Drop;
			break;
		}
		case 14:
		{
			TextBox textBox = target.As<TextBox>();
			textBox.DragOver += OutputFolder_DragOver;
			textBox.Drop += OutputFolder_Drop;
			break;
		}
		case 15:
		{
			Button button4 = target.As<Button>();
			button4.Click += BrowseOutputFolder_Click;
			break;
		}
		case 16:
			LogsListView = target.As<ListView>();
			break;
		case 23:
			TasksGrid = target.As<DataGrid>();
			TasksGrid.Tapped += DataGrid_Tapped;
			TasksGrid.PointerPressed += TasksGrid_PointerPressed;
			TasksGrid.SelectionChanged += TasksGrid_SelectionChanged;
			break;
		case 24:
			VerifyGrid = target.As<DataGrid>();
			VerifyGrid.Tapped += DataGrid_Tapped;
			VerifyGrid.PointerPressed += TasksGrid_PointerPressed;
			VerifyGrid.SelectionChanged += TasksGrid_SelectionChanged;
			break;
		case 25:
		{
			Button button3 = target.As<Button>();
			button3.Click += DeleteTask_Click;
			break;
		}
		case 30:
		{
			Button button2 = target.As<Button>();
			button2.Click += DeleteTask_Click;
			break;
		}
		case 33:
		{
			Button button = target.As<Button>();
			button.Click += FilesCount_Click;
			break;
		}
		case 40:
		{
			AppBarButton appBarButton = target.As<AppBarButton>();
			appBarButton.Click += CollapseAll_Click;
			break;
		}
		case 42:
		{
			CheckBox checkBox10 = target.As<CheckBox>();
			checkBox10.Checked += ColumnVisibility_Checked;
			checkBox10.Unchecked += ColumnVisibility_Unchecked;
			break;
		}
		case 43:
		{
			CheckBox checkBox9 = target.As<CheckBox>();
			checkBox9.Checked += ColumnVisibility_Checked;
			checkBox9.Unchecked += ColumnVisibility_Unchecked;
			break;
		}
		case 44:
		{
			CheckBox checkBox8 = target.As<CheckBox>();
			checkBox8.Checked += ColumnVisibility_Checked;
			checkBox8.Unchecked += ColumnVisibility_Unchecked;
			break;
		}
		case 45:
		{
			CheckBox checkBox7 = target.As<CheckBox>();
			checkBox7.Checked += ColumnVisibility_Checked;
			checkBox7.Unchecked += ColumnVisibility_Unchecked;
			break;
		}
		case 46:
		{
			CheckBox checkBox6 = target.As<CheckBox>();
			checkBox6.Checked += ColumnVisibility_Checked;
			checkBox6.Unchecked += ColumnVisibility_Unchecked;
			break;
		}
		case 47:
		{
			CheckBox checkBox5 = target.As<CheckBox>();
			checkBox5.Checked += ColumnVisibility_Checked;
			checkBox5.Unchecked += ColumnVisibility_Unchecked;
			break;
		}
		case 48:
		{
			CheckBox checkBox4 = target.As<CheckBox>();
			checkBox4.Checked += ColumnVisibility_Checked;
			checkBox4.Unchecked += ColumnVisibility_Unchecked;
			break;
		}
		case 49:
		{
			CheckBox checkBox3 = target.As<CheckBox>();
			checkBox3.Checked += ColumnVisibility_Checked;
			checkBox3.Unchecked += ColumnVisibility_Unchecked;
			break;
		}
		case 50:
		{
			CheckBox checkBox2 = target.As<CheckBox>();
			checkBox2.Checked += ColumnVisibility_Checked;
			checkBox2.Unchecked += ColumnVisibility_Unchecked;
			break;
		}
		case 51:
		{
			CheckBox checkBox = target.As<CheckBox>();
			checkBox.Checked += ColumnVisibility_Checked;
			checkBox.Unchecked += ColumnVisibility_Unchecked;
			break;
		}
		case 52:
			FormatComboBox = target.As<ComboBox>();
			FormatComboBox.SelectionChanged += FormatComboBox_SelectionChanged;
			break;
		}
		_contentLoaded = true;
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	[DebuggerNonUserCode]
	public IComponentConnector GetBindingConnector(int connectionId, object target)
	{
		IComponentConnector result = null;
		switch (connectionId)
		{
		case 1:
		{
			Page page = (Page)target;
			TasksPage_obj1_Bindings tasksPage_obj1_Bindings = new TasksPage_obj1_Bindings();
			result = tasksPage_obj1_Bindings;
			tasksPage_obj1_Bindings.SetDataRoot(this);
			Bindings = tasksPage_obj1_Bindings;
			page.Loading += tasksPage_obj1_Bindings.Loading;
			XamlBindingHelper.SetDataTemplateComponent(page, tasksPage_obj1_Bindings);
			break;
		}
		case 18:
		{
			Grid grid = (Grid)target;
			TasksPage_obj18_Bindings tasksPage_obj18_Bindings = new TasksPage_obj18_Bindings();
			result = tasksPage_obj18_Bindings;
			tasksPage_obj18_Bindings.SetDataRoot(grid.DataContext);
			grid.DataContextChanged += tasksPage_obj18_Bindings.DataContextChangedHandler;
			DataTemplate.SetExtensionInstance(grid, tasksPage_obj18_Bindings);
			XamlBindingHelper.SetDataTemplateComponent(grid, tasksPage_obj18_Bindings);
			break;
		}
		}
		return result;
	}
}
