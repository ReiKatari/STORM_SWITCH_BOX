using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using StormSwitchBox.Models;
using StormSwitchBox.Services;
using WinRT;

namespace StormSwitchBox.Views;

public sealed class HistoryPage : Page, IComponentConnector
{
	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private interface IHistoryPage_Bindings
	{
		void Initialize();

		void Update();

		void StopTracking();

		void DisconnectUnloadedObject(int connectionId);
	}

	private interface IHistoryPage_BindingsScopeConnector
	{
		WeakReference Parent { get; set; }

		bool ContainsElement(int connectionId);

		void RegisterForElementConnection(int connectionId, IComponentConnector connector);
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	[DebuggerNonUserCode]
	private static class XamlBindingSetters
	{
		public static void Set_CommunityToolkit_WinUI_UI_Controls_DataGrid_ItemsSource(DataGrid obj, IEnumerable value, string targetNullValue)
		{
			if (value == null && targetNullValue != null)
			{
				value = (IEnumerable)XamlBindingHelper.ConvertValue(typeof(IEnumerable), targetNullValue);
			}
			obj.ItemsSource = value;
		}

		public static void Set_Microsoft_UI_Xaml_Controls_ContentControl_Content(ContentControl obj, object value, string targetNullValue)
		{
			if (value == null && targetNullValue != null)
			{
				value = XamlBindingHelper.ConvertValue(typeof(object), targetNullValue);
			}
			obj.Content = value;
		}

		public static void Set_Microsoft_UI_Xaml_Controls_TextBlock_Text(TextBlock obj, string value, string targetNullValue)
		{
			if (value == null && targetNullValue != null)
			{
				value = targetNullValue;
			}
			obj.Text = value ?? string.Empty;
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	[DebuggerNonUserCode]
	private class HistoryPage_obj8_Bindings : IDataTemplateExtension, IDataTemplateComponent, IXamlBindScopeDiagnostics, IComponentConnector, IHistoryPage_Bindings
	{
		private ProcessingTask dataRoot;

		private bool initialized = false;

		private const int NOT_PHASED = int.MinValue;

		private const int DATA_CHANGED = 1073741824;

		private bool removedDataContextHandler = false;

		private WeakReference obj8;

		private static bool isobj8ContentDisabled;

		public void Disable(int lineNumber, int columnNumber)
		{
			if (lineNumber == 99 && columnNumber == 37)
			{
				isobj8ContentDisabled = true;
			}
		}

		public void Connect(int connectionId, object target)
		{
			if (connectionId == 8)
			{
				obj8 = new WeakReference(target.As<Button>());
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
					(obj8.Target as Button).DataContextChanged -= DataContextChangedHandler;
				}
				initialized = true;
			}
			Update_(item.As<ProcessingTask>(), 1 << phase);
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
				dataRoot = newDataRoot.As<ProcessingTask>();
				return true;
			}
			return false;
		}

		private void Update_(ProcessingTask obj, int phase)
		{
			if (obj != null && (phase & -2147483647) != 0)
			{
				Update_FilesCount(obj.FilesCount, phase);
			}
		}

		private void Update_FilesCount(string obj, int phase)
		{
			if ((phase & -2147483647) != 0 && !isobj8ContentDisabled && obj8.Target as Button != null)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_ContentControl_Content(obj8.Target as Button, obj, null);
			}
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	[DebuggerNonUserCode]
	private class HistoryPage_obj10_Bindings : IDataTemplateExtension, IDataTemplateComponent, IXamlBindScopeDiagnostics, IComponentConnector, IHistoryPage_Bindings
	{
		private ProcessingTask dataRoot;

		private bool initialized = false;

		private const int NOT_PHASED = int.MinValue;

		private const int DATA_CHANGED = 1073741824;

		private bool removedDataContextHandler = false;

		private WeakReference obj10;

		private static bool isobj10TextDisabled;

		public void Disable(int lineNumber, int columnNumber)
		{
			if (lineNumber == 88 && columnNumber == 40)
			{
				isobj10TextDisabled = true;
			}
		}

		public void Connect(int connectionId, object target)
		{
			if (connectionId == 10)
			{
				obj10 = new WeakReference(target.As<TextBlock>());
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
					(obj10.Target as TextBlock).DataContextChanged -= DataContextChangedHandler;
				}
				initialized = true;
			}
			Update_(item.As<ProcessingTask>(), 1 << phase);
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
				dataRoot = newDataRoot.As<ProcessingTask>();
				return true;
			}
			return false;
		}

		private void Update_(ProcessingTask obj, int phase)
		{
			if (obj != null && (phase & -2147483647) != 0)
			{
				Update_TargetFormat(obj.TargetFormat, phase);
			}
		}

		private void Update_TargetFormat(string obj, int phase)
		{
			if ((phase & -2147483647) != 0 && !isobj10TextDisabled && obj10.Target as TextBlock != null)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_TextBlock_Text(obj10.Target as TextBlock, obj, null);
			}
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	[DebuggerNonUserCode]
	private class HistoryPage_obj14_Bindings : IDataTemplateExtension, IDataTemplateComponent, IXamlBindScopeDiagnostics, IComponentConnector, IHistoryPage_Bindings
	{
		[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
		[DebuggerNonUserCode]
		private class HistoryPage_obj14_BindingsTracking
		{
			private WeakReference<HistoryPage_obj14_Bindings> weakRefToBindingObj;

			public HistoryPage_obj14_BindingsTracking(HistoryPage_obj14_Bindings obj)
			{
				weakRefToBindingObj = new WeakReference<HistoryPage_obj14_Bindings>(obj);
			}

			public HistoryPage_obj14_Bindings TryGetBindingObject()
			{
				HistoryPage_obj14_Bindings target = null;
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
				UpdateChildListeners_(null);
			}

			public void PropertyChanged_(object sender, PropertyChangedEventArgs e)
			{
				HistoryPage_obj14_Bindings historyPage_obj14_Bindings = TryGetBindingObject();
				if (historyPage_obj14_Bindings == null)
				{
					return;
				}
				string propertyName = e.PropertyName;
				ProcessingTask processingTask = sender as ProcessingTask;
				if (string.IsNullOrEmpty(propertyName))
				{
					if (processingTask != null)
					{
						historyPage_obj14_Bindings.Update_LogDetails(processingTask.LogDetails, 1073741824);
					}
					return;
				}
				string text = propertyName;
				string text2 = text;
				if (text2 == "LogDetails" && processingTask != null)
				{
					historyPage_obj14_Bindings.Update_LogDetails(processingTask.LogDetails, 1073741824);
				}
			}

			public void UpdateChildListeners_(ProcessingTask obj)
			{
				HistoryPage_obj14_Bindings historyPage_obj14_Bindings = TryGetBindingObject();
				if (historyPage_obj14_Bindings != null)
				{
					if (historyPage_obj14_Bindings.dataRoot != null)
					{
						((INotifyPropertyChanged)historyPage_obj14_Bindings.dataRoot).PropertyChanged -= PropertyChanged_;
					}
					if (obj != null)
					{
						historyPage_obj14_Bindings.dataRoot = obj;
						((INotifyPropertyChanged)obj).PropertyChanged += PropertyChanged_;
					}
				}
			}
		}

		private ProcessingTask dataRoot;

		private bool initialized = false;

		private const int NOT_PHASED = int.MinValue;

		private const int DATA_CHANGED = 1073741824;

		private bool removedDataContextHandler = false;

		private WeakReference obj14;

		private TextBlock obj15;

		private TextBlock obj16;

		private TextBlock obj17;

		private TextBlock obj18;

		private static bool isobj15TextDisabled;

		private static bool isobj16TextDisabled;

		private static bool isobj17TextDisabled;

		private static bool isobj18TextDisabled;

		private HistoryPage_obj14_BindingsTracking bindingsTracking;

		public HistoryPage_obj14_Bindings()
		{
			bindingsTracking = new HistoryPage_obj14_BindingsTracking(this);
		}

		public void Disable(int lineNumber, int columnNumber)
		{
			if (lineNumber == 172 && columnNumber == 48)
			{
				isobj15TextDisabled = true;
			}
			else if (lineNumber == 158 && columnNumber == 77)
			{
				isobj16TextDisabled = true;
			}
			else if (lineNumber == 161 && columnNumber == 77)
			{
				isobj17TextDisabled = true;
			}
			else if (lineNumber == 164 && columnNumber == 77)
			{
				isobj18TextDisabled = true;
			}
		}

		public void Connect(int connectionId, object target)
		{
			switch (connectionId)
			{
			case 14:
				obj14 = new WeakReference(target.As<Border>());
				break;
			case 15:
				obj15 = target.As<TextBlock>();
				break;
			case 16:
				obj16 = target.As<TextBlock>();
				break;
			case 17:
				obj17 = target.As<TextBlock>();
				break;
			case 18:
				obj18 = target.As<TextBlock>();
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
					(obj14.Target as Border).DataContextChanged -= DataContextChangedHandler;
				}
				initialized = true;
			}
			Update_(item.As<ProcessingTask>(), 1 << phase);
		}

		public void Recycle()
		{
			bindingsTracking.ReleaseAllListeners();
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
				dataRoot = newDataRoot.As<ProcessingTask>();
				return true;
			}
			return false;
		}

		private void Update_(ProcessingTask obj, int phase)
		{
			bindingsTracking.UpdateChildListeners_(obj);
			if (obj != null)
			{
				if ((phase & -1073741823) != 0)
				{
					Update_LogDetails(obj.LogDetails, phase);
				}
				if ((phase & -2147483647) != 0)
				{
					Update_InputFolders(obj.InputFolders, phase);
					Update_OutputFolder(obj.OutputFolder, phase);
					Update_OutputFileName(obj.OutputFileName, phase);
				}
			}
		}

		private void Update_LogDetails(string obj, int phase)
		{
			if ((phase & -1073741823) != 0 && !isobj15TextDisabled)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_TextBlock_Text(obj15, obj, null);
			}
		}

		private void Update_InputFolders(string obj, int phase)
		{
			if ((phase & -2147483647) != 0 && !isobj16TextDisabled)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_TextBlock_Text(obj16, obj, null);
			}
		}

		private void Update_OutputFolder(string obj, int phase)
		{
			if ((phase & -2147483647) != 0 && !isobj17TextDisabled)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_TextBlock_Text(obj17, obj, null);
			}
		}

		private void Update_OutputFileName(string obj, int phase)
		{
			if ((phase & -2147483647) != 0 && !isobj18TextDisabled)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_TextBlock_Text(obj18, obj, null);
			}
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	[DebuggerNonUserCode]
	private class HistoryPage_obj1_Bindings : IDataTemplateComponent, IXamlBindScopeDiagnostics, IComponentConnector, IHistoryPage_Bindings
	{
		[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
		[DebuggerNonUserCode]
		private class HistoryPage_obj1_BindingsTracking
		{
			private WeakReference<HistoryPage_obj1_Bindings> weakRefToBindingObj;

			private ObservableCollection<ProcessingTask> cache_HistoryTasks = null;

			public HistoryPage_obj1_BindingsTracking(HistoryPage_obj1_Bindings obj)
			{
				weakRefToBindingObj = new WeakReference<HistoryPage_obj1_Bindings>(obj);
			}

			public HistoryPage_obj1_Bindings TryGetBindingObject()
			{
				HistoryPage_obj1_Bindings target = null;
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
				UpdateChildListeners_HistoryTasks(null);
			}

			public void PropertyChanged_HistoryTasks(object sender, PropertyChangedEventArgs e)
			{
				HistoryPage_obj1_Bindings historyPage_obj1_Bindings = TryGetBindingObject();
				if (historyPage_obj1_Bindings != null)
				{
					string propertyName = e.PropertyName;
					ObservableCollection<ProcessingTask> observableCollection = sender as ObservableCollection<ProcessingTask>;
					if (!string.IsNullOrEmpty(propertyName))
					{
						string text = propertyName;
						string text2 = text;
					}
				}
			}

			public void CollectionChanged_HistoryTasks(object sender, NotifyCollectionChangedEventArgs e)
			{
				HistoryPage_obj1_Bindings historyPage_obj1_Bindings = TryGetBindingObject();
				if (historyPage_obj1_Bindings != null)
				{
					ObservableCollection<ProcessingTask> observableCollection = sender as ObservableCollection<ProcessingTask>;
				}
			}

			public void UpdateChildListeners_HistoryTasks(ObservableCollection<ProcessingTask> obj)
			{
				if (obj != cache_HistoryTasks)
				{
					if (cache_HistoryTasks != null)
					{
						((INotifyPropertyChanged)cache_HistoryTasks).PropertyChanged -= PropertyChanged_HistoryTasks;
						((INotifyCollectionChanged)cache_HistoryTasks).CollectionChanged -= CollectionChanged_HistoryTasks;
						cache_HistoryTasks = null;
					}
					if (obj != null)
					{
						cache_HistoryTasks = obj;
						((INotifyPropertyChanged)obj).PropertyChanged += PropertyChanged_HistoryTasks;
						((INotifyCollectionChanged)obj).CollectionChanged += CollectionChanged_HistoryTasks;
					}
				}
			}
		}

		private HistoryPage dataRoot;

		private bool initialized = false;

		private const int NOT_PHASED = int.MinValue;

		private const int DATA_CHANGED = 1073741824;

		private DataGrid obj2;

		private static bool isobj2ItemsSourceDisabled;

		private HistoryPage_obj1_BindingsTracking bindingsTracking;

		public HistoryPage_obj1_Bindings()
		{
			bindingsTracking = new HistoryPage_obj1_BindingsTracking(this);
		}

		public void Disable(int lineNumber, int columnNumber)
		{
			if (lineNumber == 38 && columnNumber == 62)
			{
				isobj2ItemsSourceDisabled = true;
			}
		}

		public void Connect(int connectionId, object target)
		{
			if (connectionId == 2)
			{
				obj2 = target.As<DataGrid>();
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
				dataRoot = newDataRoot.As<HistoryPage>();
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

		private void Update_(HistoryPage obj, int phase)
		{
			if (obj != null && (phase & -1073741823) != 0)
			{
				Update_HistoryTasks(obj.HistoryTasks, phase);
			}
		}

		private void Update_HistoryTasks(ObservableCollection<ProcessingTask> obj, int phase)
		{
			bindingsTracking.UpdateChildListeners_HistoryTasks(obj);
			if ((phase & -1073741823) != 0 && !isobj2ItemsSourceDisabled)
			{
				XamlBindingSetters.Set_CommunityToolkit_WinUI_UI_Controls_DataGrid_ItemsSource(obj2, obj, null);
			}
		}
	}

	private object? _itemAtPointerPressed;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private DataGrid HistoryGrid;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private bool _contentLoaded;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private IHistoryPage_Bindings Bindings;

	public ObservableCollection<ProcessingTask> HistoryTasks => HistoryService.HistoryTasks;

	public HistoryPage()
	{
		InitializeComponent();
		base.NavigationCacheMode = NavigationCacheMode.Required;
	}

	private async void ClearHistory_Click(object sender, RoutedEventArgs e)
	{
		ContentDialog dialog = new ContentDialog
		{
			Title = "Очистка истории",
			Content = "Вы уверены, что хотите полностью удалить историю обработок?",
			PrimaryButtonText = "Очистить",
			CloseButtonText = "Отмена",
			DefaultButton = ContentDialogButton.Close,
			XamlRoot = base.XamlRoot
		};
		if (await dialog.ShowAsync() == ContentDialogResult.Primary)
		{
			HistoryTasks.Clear();
			await HistoryService.SaveHistoryAsync();
		}
	}

	private void HistoryGrid_PointerPressed(object sender, PointerRoutedEventArgs e)
	{
		DataGrid dataGrid = sender as DataGrid;
		if (dataGrid == null)
		{
			return;
		}
		IEnumerable<UIElement> enumerable = VisualTreeHelper.FindElementsInHostCoordinates(e.GetCurrentPoint(this).Position, dataGrid);
		DataGridRow dataGridRow = null;
		foreach (UIElement item in enumerable)
		{
			DataGridRow dataGridRow2 = FindParent<DataGridRow>(item);
			if (dataGridRow2 != null)
			{
				dataGridRow = dataGridRow2;
				break;
			}
		}
		if (dataGridRow != null && dataGridRow.DataContext != null)
		{
			_itemAtPointerPressed = dataGridRow.DataContext;
		}
		else
		{
			_itemAtPointerPressed = null;
		}
	}

	private void HistoryGrid_Tapped(object sender, TappedRoutedEventArgs e)
	{
		DataGrid dataGrid = sender as DataGrid;
		if (!(dataGrid == null) && _itemAtPointerPressed != null)
		{
			if (dataGrid.SelectedItem == _itemAtPointerPressed)
			{
				dataGrid.SelectedItem = null;
				e.Handled = true;
			}
			_itemAtPointerPressed = null;
		}
	}

	private T? FindParent<T>(DependencyObject? child) where T : DependencyObject
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

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	[DebuggerNonUserCode]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("ms-appx:///Views/HistoryPage.xaml");
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
			HistoryGrid = target.As<DataGrid>();
			HistoryGrid.Tapped += HistoryGrid_Tapped;
			HistoryGrid.PointerPressed += HistoryGrid_PointerPressed;
			break;
		case 19:
		{
			Button button = target.As<Button>();
			button.Click += ClearHistory_Click;
			break;
		}
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
			HistoryPage_obj1_Bindings historyPage_obj1_Bindings = new HistoryPage_obj1_Bindings();
			result = historyPage_obj1_Bindings;
			historyPage_obj1_Bindings.SetDataRoot(this);
			Bindings = historyPage_obj1_Bindings;
			page.Loading += historyPage_obj1_Bindings.Loading;
			XamlBindingHelper.SetDataTemplateComponent(page, historyPage_obj1_Bindings);
			break;
		}
		case 8:
		{
			Button button = (Button)target;
			HistoryPage_obj8_Bindings historyPage_obj8_Bindings = new HistoryPage_obj8_Bindings();
			result = historyPage_obj8_Bindings;
			historyPage_obj8_Bindings.SetDataRoot(button.DataContext);
			button.DataContextChanged += historyPage_obj8_Bindings.DataContextChangedHandler;
			DataTemplate.SetExtensionInstance(button, historyPage_obj8_Bindings);
			XamlBindingHelper.SetDataTemplateComponent(button, historyPage_obj8_Bindings);
			break;
		}
		case 10:
		{
			TextBlock textBlock = (TextBlock)target;
			HistoryPage_obj10_Bindings historyPage_obj10_Bindings = new HistoryPage_obj10_Bindings();
			result = historyPage_obj10_Bindings;
			historyPage_obj10_Bindings.SetDataRoot(textBlock.DataContext);
			textBlock.DataContextChanged += historyPage_obj10_Bindings.DataContextChangedHandler;
			DataTemplate.SetExtensionInstance(textBlock, historyPage_obj10_Bindings);
			XamlBindingHelper.SetDataTemplateComponent(textBlock, historyPage_obj10_Bindings);
			break;
		}
		case 14:
		{
			Border border = (Border)target;
			HistoryPage_obj14_Bindings historyPage_obj14_Bindings = new HistoryPage_obj14_Bindings();
			result = historyPage_obj14_Bindings;
			historyPage_obj14_Bindings.SetDataRoot(border.DataContext);
			border.DataContextChanged += historyPage_obj14_Bindings.DataContextChangedHandler;
			DataTemplate.SetExtensionInstance(border, historyPage_obj14_Bindings);
			XamlBindingHelper.SetDataTemplateComponent(border, historyPage_obj14_Bindings);
			break;
		}
		}
		return result;
	}
}
