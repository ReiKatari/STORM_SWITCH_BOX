using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.WinUI.UI.Controls;
using CommunityToolkit.WinUI.UI.Controls.CommunityToolkit_WinUI_UI_Controls_DataGrid_XamlTypeInfo;
using CommunityToolkit.WinUI.UI.Controls.Primitives;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.XamlTypeInfo;
using StormSwitchBox.Models;
using StormSwitchBox.ViewModels;
using StormSwitchBox.Views;
using Windows.Globalization.NumberFormatting;
using Windows.UI.Text;

namespace StormSwitchBox.StormSwitchBox_XamlTypeInfo;

[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
[DebuggerNonUserCode]
internal class XamlTypeInfoProvider
{
	private Dictionary<string, IXamlType> _xamlTypeCacheByName = new Dictionary<string, IXamlType>();

	private Dictionary<Type, IXamlType> _xamlTypeCacheByType = new Dictionary<Type, IXamlType>();

	private Dictionary<string, IXamlMember> _xamlMembers = new Dictionary<string, IXamlMember>();

	private string[] _typeNameTable = null;

	private Type[] _typeTable = null;

	private List<IXamlMetadataProvider> _otherProviders;

	private List<IXamlMetadataProvider> OtherProviders
	{
		get
		{
			if (_otherProviders == null)
			{
				List<IXamlMetadataProvider> list = new List<IXamlMetadataProvider>();
				IXamlMetadataProvider item = new CommunityToolkit.WinUI.UI.Controls.CommunityToolkit_WinUI_UI_Controls_DataGrid_XamlTypeInfo.XamlMetaDataProvider();
				list.Add(item);
				item = new XamlControlsXamlMetaDataProvider();
				list.Add(item);
				_otherProviders = list;
			}
			return _otherProviders;
		}
	}

	public IXamlType GetXamlTypeByType(Type type)
	{
		IXamlType value;
		lock (_xamlTypeCacheByType)
		{
			if (_xamlTypeCacheByType.TryGetValue(type, out value))
			{
				return value;
			}
			int num = LookupTypeIndexByType(type);
			if (num != -1)
			{
				value = CreateXamlType(num);
			}
			XamlUserType xamlUserType = value as XamlUserType;
			if (value == null || (xamlUserType != null && xamlUserType.IsReturnTypeStub && !xamlUserType.IsLocalType))
			{
				IXamlType xamlType = CheckOtherMetadataProvidersForType(type);
				if (xamlType != null && (xamlType.IsConstructible || value == null))
				{
					value = xamlType;
				}
			}
			if (value != null)
			{
				_xamlTypeCacheByName.Add(value.FullName, value);
				_xamlTypeCacheByType.Add(value.UnderlyingType, value);
			}
		}
		return value;
	}

	public IXamlType GetXamlTypeByName(string typeName)
	{
		if (string.IsNullOrEmpty(typeName))
		{
			return null;
		}
		IXamlType value;
		lock (_xamlTypeCacheByType)
		{
			if (_xamlTypeCacheByName.TryGetValue(typeName, out value))
			{
				return value;
			}
			int num = LookupTypeIndexByName(typeName);
			if (num != -1)
			{
				value = CreateXamlType(num);
			}
			XamlUserType xamlUserType = value as XamlUserType;
			if (value == null || (xamlUserType != null && xamlUserType.IsReturnTypeStub && !xamlUserType.IsLocalType))
			{
				IXamlType xamlType = CheckOtherMetadataProvidersForName(typeName);
				if (xamlType != null && (xamlType.IsConstructible || value == null))
				{
					value = xamlType;
				}
			}
			if (value != null)
			{
				_xamlTypeCacheByName.Add(value.FullName, value);
				_xamlTypeCacheByType.Add(value.UnderlyingType, value);
			}
		}
		return value;
	}

	public IXamlMember GetMemberByLongName(string longMemberName)
	{
		if (string.IsNullOrEmpty(longMemberName))
		{
			return null;
		}
		IXamlMember value;
		lock (_xamlMembers)
		{
			if (_xamlMembers.TryGetValue(longMemberName, out value))
			{
				return value;
			}
			value = CreateXamlMember(longMemberName);
			if (value != null)
			{
				_xamlMembers.Add(longMemberName, value);
			}
		}
		return value;
	}

	private void InitTypeTables()
	{
		_typeNameTable = new string[99];
		_typeNameTable[0] = "Microsoft.UI.Xaml.Controls.XamlControlsResources";
		_typeNameTable[1] = "Microsoft.UI.Xaml.ResourceDictionary";
		_typeNameTable[2] = "Object";
		_typeNameTable[3] = "Boolean";
		_typeNameTable[4] = "Microsoft.UI.Xaml.Controls.NavigationView";
		_typeNameTable[5] = "Microsoft.UI.Xaml.Controls.ContentControl";
		_typeNameTable[6] = "Microsoft.UI.Xaml.Controls.NavigationViewPaneDisplayMode";
		_typeNameTable[7] = "System.Enum";
		_typeNameTable[8] = "System.ValueType";
		_typeNameTable[9] = "Double";
		_typeNameTable[10] = "System.Collections.Generic.IList`1<Object>";
		_typeNameTable[11] = "Microsoft.UI.Xaml.Controls.AutoSuggestBox";
		_typeNameTable[12] = "Microsoft.UI.Xaml.UIElement";
		_typeNameTable[13] = "Microsoft.UI.Xaml.Controls.NavigationViewDisplayMode";
		_typeNameTable[14] = "Microsoft.UI.Xaml.DataTemplate";
		_typeNameTable[15] = "Microsoft.UI.Xaml.Controls.NavigationViewBackButtonVisible";
		_typeNameTable[16] = "Microsoft.UI.Xaml.Style";
		_typeNameTable[17] = "Microsoft.UI.Xaml.Controls.StyleSelector";
		_typeNameTable[18] = "Microsoft.UI.Xaml.Controls.DataTemplateSelector";
		_typeNameTable[19] = "Microsoft.UI.Xaml.Controls.NavigationViewOverflowLabelMode";
		_typeNameTable[20] = "String";
		_typeNameTable[21] = "Microsoft.UI.Xaml.Controls.NavigationViewSelectionFollowsFocus";
		_typeNameTable[22] = "Microsoft.UI.Xaml.Controls.NavigationViewShoulderNavigationEnabled";
		_typeNameTable[23] = "Microsoft.UI.Xaml.Controls.NavigationViewTemplateSettings";
		_typeNameTable[24] = "Microsoft.UI.Xaml.DependencyObject";
		_typeNameTable[25] = "Microsoft.UI.Xaml.Controls.NavigationViewItem";
		_typeNameTable[26] = "Microsoft.UI.Xaml.Controls.NavigationViewItemBase";
		_typeNameTable[27] = "Microsoft.UI.Xaml.Controls.IconElement";
		_typeNameTable[28] = "Microsoft.UI.Xaml.Controls.InfoBadge";
		_typeNameTable[29] = "Microsoft.UI.Xaml.Controls.Control";
		_typeNameTable[30] = "Microsoft.UI.Xaml.Controls.NavigationViewItemSeparator";
		_typeNameTable[31] = "StormSwitchBox.MainWindow";
		_typeNameTable[32] = "Microsoft.UI.Xaml.Window";
		_typeNameTable[33] = "Microsoft.UI.Xaml.Controls.ProgressRing";
		_typeNameTable[34] = "Microsoft.UI.Xaml.Controls.ProgressRingTemplateSettings";
		_typeNameTable[35] = "StormSwitchBox.Views.CatalogPage";
		_typeNameTable[36] = "Microsoft.UI.Xaml.Controls.Page";
		_typeNameTable[37] = "Microsoft.UI.Xaml.Controls.UserControl";
		_typeNameTable[38] = "CommunityToolkit.WinUI.UI.Controls.DataGrid";
		_typeNameTable[39] = "System.Collections.IEnumerable";
		_typeNameTable[40] = "CommunityToolkit.WinUI.UI.Controls.DataGridGridLinesVisibility";
		_typeNameTable[41] = "CommunityToolkit.WinUI.UI.Controls.DataGridHeadersVisibility";
		_typeNameTable[42] = "CommunityToolkit.WinUI.UI.Controls.DataGridRowDetailsVisibilityMode";
		_typeNameTable[43] = "CommunityToolkit.WinUI.UI.Controls.DataGridSelectionMode";
		_typeNameTable[44] = "System.Collections.ObjectModel.ObservableCollection`1<CommunityToolkit.WinUI.UI.Controls.DataGridColumn>";
		_typeNameTable[45] = "System.Collections.ObjectModel.Collection`1<CommunityToolkit.WinUI.UI.Controls.DataGridColumn>";
		_typeNameTable[46] = "CommunityToolkit.WinUI.UI.Controls.DataGridColumn";
		_typeNameTable[47] = "Microsoft.UI.Xaml.Data.Binding";
		_typeNameTable[48] = "Int32";
		_typeNameTable[49] = "System.Nullable`1<CommunityToolkit.WinUI.UI.Controls.DataGridSortDirection>";
		_typeNameTable[50] = "CommunityToolkit.WinUI.UI.Controls.DataGridSortDirection";
		_typeNameTable[51] = "Microsoft.UI.Xaml.Visibility";
		_typeNameTable[52] = "CommunityToolkit.WinUI.UI.Controls.DataGridLength";
		_typeNameTable[53] = "Microsoft.UI.Xaml.Media.Brush";
		_typeNameTable[54] = "CommunityToolkit.WinUI.UI.Controls.DataGridClipboardCopyMode";
		_typeNameTable[55] = "Microsoft.UI.Xaml.Controls.ScrollBarVisibility";
		_typeNameTable[56] = "Microsoft.UI.Xaml.Controls.IncrementalLoadingTrigger";
		_typeNameTable[57] = "System.Collections.ObjectModel.ObservableCollection`1<Microsoft.UI.Xaml.Style>";
		_typeNameTable[58] = "System.Collections.ObjectModel.Collection`1<Microsoft.UI.Xaml.Style>";
		_typeNameTable[59] = "System.Collections.IList";
		_typeNameTable[60] = "CommunityToolkit.WinUI.UI.Controls.Primitives.DataGridColumnHeader";
		_typeNameTable[61] = "CommunityToolkit.WinUI.UI.Controls.DataGridRow";
		_typeNameTable[62] = "CommunityToolkit.WinUI.UI.Controls.DataGridTemplateColumn";
		_typeNameTable[63] = "CommunityToolkit.WinUI.UI.Controls.DataGridTextColumn";
		_typeNameTable[64] = "CommunityToolkit.WinUI.UI.Controls.DataGridBoundColumn";
		_typeNameTable[65] = "Microsoft.UI.Xaml.Media.FontFamily";
		_typeNameTable[66] = "Windows.UI.Text.FontStyle";
		_typeNameTable[67] = "Windows.UI.Text.FontWeight";
		_typeNameTable[68] = "Microsoft.UI.Xaml.Controls.ProgressBar";
		_typeNameTable[69] = "Microsoft.UI.Xaml.Controls.Primitives.RangeBase";
		_typeNameTable[70] = "Microsoft.UI.Xaml.Controls.ProgressBarTemplateSettings";
		_typeNameTable[71] = "StormSwitchBox.Views.HistoryPage";
		_typeNameTable[72] = "System.Collections.ObjectModel.ObservableCollection`1<StormSwitchBox.Models.ProcessingTask>";
		_typeNameTable[73] = "System.Collections.ObjectModel.Collection`1<StormSwitchBox.Models.ProcessingTask>";
		_typeNameTable[74] = "StormSwitchBox.Models.ProcessingTask";
		_typeNameTable[75] = "CommunityToolkit.Mvvm.ComponentModel.ObservableObject";
		_typeNameTable[76] = "System.Threading.CancellationTokenSource";
		_typeNameTable[77] = "System.Collections.Generic.List`1<String>";
		_typeNameTable[78] = "Microsoft.UI.Xaml.Media.SolidColorBrush";
		_typeNameTable[79] = "Microsoft.UI.Xaml.Media.Imaging.BitmapImage";
		_typeNameTable[80] = "System.DateTime";
		_typeNameTable[81] = "Int64";
		_typeNameTable[82] = "Microsoft.UI.Xaml.Controls.NumberBox";
		_typeNameTable[83] = "Microsoft.UI.Xaml.Controls.NumberBoxSpinButtonPlacementMode";
		_typeNameTable[84] = "Windows.Globalization.NumberFormatting.INumberFormatter2";
		_typeNameTable[85] = "Microsoft.UI.Xaml.Controls.Primitives.FlyoutBase";
		_typeNameTable[86] = "Microsoft.UI.Xaml.TextReadingOrder";
		_typeNameTable[87] = "Microsoft.UI.Xaml.Controls.NumberBoxValidationMode";
		_typeNameTable[88] = "StormSwitchBox.Views.SettingsPage";
		_typeNameTable[89] = "StormSwitchBox.Models.AppSettings";
		_typeNameTable[90] = "Microsoft.UI.Xaml.Controls.Primitives.Thumb";
		_typeNameTable[91] = "StormSwitchBox.Views.TasksPage";
		_typeNameTable[92] = "StormSwitchBox.ViewModels.TasksViewModel";
		_typeNameTable[93] = "System.Collections.ObjectModel.ObservableCollection`1<StormSwitchBox.Models.LogMessage>";
		_typeNameTable[94] = "System.Collections.ObjectModel.Collection`1<StormSwitchBox.Models.LogMessage>";
		_typeNameTable[95] = "StormSwitchBox.Models.LogMessage";
		_typeNameTable[96] = "StormSwitchBox.Models.LogLevel";
		_typeNameTable[97] = "Microsoft.UI.Xaml.Controls.TreeViewNode";
		_typeNameTable[98] = "System.Collections.Generic.IList`1<Microsoft.UI.Xaml.Controls.TreeViewNode>";
		_typeTable = new Type[99];
		_typeTable[0] = typeof(XamlControlsResources);
		_typeTable[1] = typeof(ResourceDictionary);
		_typeTable[2] = typeof(object);
		_typeTable[3] = typeof(bool);
		_typeTable[4] = typeof(NavigationView);
		_typeTable[5] = typeof(ContentControl);
		_typeTable[6] = typeof(NavigationViewPaneDisplayMode);
		_typeTable[7] = typeof(Enum);
		_typeTable[8] = typeof(ValueType);
		_typeTable[9] = typeof(double);
		_typeTable[10] = typeof(IList<object>);
		_typeTable[11] = typeof(AutoSuggestBox);
		_typeTable[12] = typeof(UIElement);
		_typeTable[13] = typeof(NavigationViewDisplayMode);
		_typeTable[14] = typeof(DataTemplate);
		_typeTable[15] = typeof(NavigationViewBackButtonVisible);
		_typeTable[16] = typeof(Style);
		_typeTable[17] = typeof(StyleSelector);
		_typeTable[18] = typeof(DataTemplateSelector);
		_typeTable[19] = typeof(NavigationViewOverflowLabelMode);
		_typeTable[20] = typeof(string);
		_typeTable[21] = typeof(NavigationViewSelectionFollowsFocus);
		_typeTable[22] = typeof(NavigationViewShoulderNavigationEnabled);
		_typeTable[23] = typeof(NavigationViewTemplateSettings);
		_typeTable[24] = typeof(DependencyObject);
		_typeTable[25] = typeof(NavigationViewItem);
		_typeTable[26] = typeof(NavigationViewItemBase);
		_typeTable[27] = typeof(IconElement);
		_typeTable[28] = typeof(InfoBadge);
		_typeTable[29] = typeof(Control);
		_typeTable[30] = typeof(NavigationViewItemSeparator);
		_typeTable[31] = typeof(MainWindow);
		_typeTable[32] = typeof(Window);
		_typeTable[33] = typeof(ProgressRing);
		_typeTable[34] = typeof(ProgressRingTemplateSettings);
		_typeTable[35] = typeof(CatalogPage);
		_typeTable[36] = typeof(Page);
		_typeTable[37] = typeof(UserControl);
		_typeTable[38] = typeof(DataGrid);
		_typeTable[39] = typeof(IEnumerable);
		_typeTable[40] = typeof(DataGridGridLinesVisibility);
		_typeTable[41] = typeof(DataGridHeadersVisibility);
		_typeTable[42] = typeof(DataGridRowDetailsVisibilityMode);
		_typeTable[43] = typeof(DataGridSelectionMode);
		_typeTable[44] = typeof(ObservableCollection<DataGridColumn>);
		_typeTable[45] = typeof(Collection<DataGridColumn>);
		_typeTable[46] = typeof(DataGridColumn);
		_typeTable[47] = typeof(Binding);
		_typeTable[48] = typeof(int);
		_typeTable[49] = typeof(DataGridSortDirection?);
		_typeTable[50] = typeof(DataGridSortDirection);
		_typeTable[51] = typeof(Visibility);
		_typeTable[52] = typeof(DataGridLength);
		_typeTable[53] = typeof(Brush);
		_typeTable[54] = typeof(DataGridClipboardCopyMode);
		_typeTable[55] = typeof(ScrollBarVisibility);
		_typeTable[56] = typeof(IncrementalLoadingTrigger);
		_typeTable[57] = typeof(ObservableCollection<Style>);
		_typeTable[58] = typeof(Collection<Style>);
		_typeTable[59] = typeof(IList);
		_typeTable[60] = typeof(DataGridColumnHeader);
		_typeTable[61] = typeof(DataGridRow);
		_typeTable[62] = typeof(DataGridTemplateColumn);
		_typeTable[63] = typeof(DataGridTextColumn);
		_typeTable[64] = typeof(DataGridBoundColumn);
		_typeTable[65] = typeof(FontFamily);
		_typeTable[66] = typeof(FontStyle);
		_typeTable[67] = typeof(FontWeight);
		_typeTable[68] = typeof(ProgressBar);
		_typeTable[69] = typeof(RangeBase);
		_typeTable[70] = typeof(ProgressBarTemplateSettings);
		_typeTable[71] = typeof(HistoryPage);
		_typeTable[72] = typeof(ObservableCollection<ProcessingTask>);
		_typeTable[73] = typeof(Collection<ProcessingTask>);
		_typeTable[74] = typeof(ProcessingTask);
		_typeTable[75] = typeof(ObservableObject);
		_typeTable[76] = typeof(CancellationTokenSource);
		_typeTable[77] = typeof(List<string>);
		_typeTable[78] = typeof(SolidColorBrush);
		_typeTable[79] = typeof(BitmapImage);
		_typeTable[80] = typeof(DateTime);
		_typeTable[81] = typeof(long);
		_typeTable[82] = typeof(NumberBox);
		_typeTable[83] = typeof(NumberBoxSpinButtonPlacementMode);
		_typeTable[84] = typeof(INumberFormatter2);
		_typeTable[85] = typeof(FlyoutBase);
		_typeTable[86] = typeof(TextReadingOrder);
		_typeTable[87] = typeof(NumberBoxValidationMode);
		_typeTable[88] = typeof(SettingsPage);
		_typeTable[89] = typeof(AppSettings);
		_typeTable[90] = typeof(Thumb);
		_typeTable[91] = typeof(TasksPage);
		_typeTable[92] = typeof(TasksViewModel);
		_typeTable[93] = typeof(ObservableCollection<LogMessage>);
		_typeTable[94] = typeof(Collection<LogMessage>);
		_typeTable[95] = typeof(LogMessage);
		_typeTable[96] = typeof(LogLevel);
		_typeTable[97] = typeof(TreeViewNode);
		_typeTable[98] = typeof(IList<TreeViewNode>);
	}

	private int LookupTypeIndexByName(string typeName)
	{
		if (_typeNameTable == null)
		{
			InitTypeTables();
		}
		for (int i = 0; i < _typeNameTable.Length; i++)
		{
			if (string.CompareOrdinal(_typeNameTable[i], typeName) == 0)
			{
				return i;
			}
		}
		return -1;
	}

	private int LookupTypeIndexByType(Type type)
	{
		if (_typeTable == null)
		{
			InitTypeTables();
		}
		for (int i = 0; i < _typeTable.Length; i++)
		{
			if (type == _typeTable[i])
			{
				return i;
			}
		}
		return -1;
	}

	private object Activate_0_XamlControlsResources()
	{
		return new XamlControlsResources();
	}

	private object Activate_4_NavigationView()
	{
		return new NavigationView();
	}

	private object Activate_23_NavigationViewTemplateSettings()
	{
		return new NavigationViewTemplateSettings();
	}

	private object Activate_25_NavigationViewItem()
	{
		return new NavigationViewItem();
	}

	private object Activate_28_InfoBadge()
	{
		return new InfoBadge();
	}

	private object Activate_30_NavigationViewItemSeparator()
	{
		return new NavigationViewItemSeparator();
	}

	private object Activate_31_MainWindow()
	{
		return new MainWindow();
	}

	private object Activate_33_ProgressRing()
	{
		return new ProgressRing();
	}

	private object Activate_35_CatalogPage()
	{
		return new CatalogPage();
	}

	private object Activate_38_DataGrid()
	{
		return new DataGrid();
	}

	private object Activate_44_ObservableCollection()
	{
		return new ObservableCollection<DataGridColumn>();
	}

	private object Activate_45_Collection()
	{
		return new Collection<DataGridColumn>();
	}

	private object Activate_57_ObservableCollection()
	{
		return new ObservableCollection<Style>();
	}

	private object Activate_58_Collection()
	{
		return new Collection<Style>();
	}

	private object Activate_60_DataGridColumnHeader()
	{
		return new DataGridColumnHeader();
	}

	private object Activate_61_DataGridRow()
	{
		return new DataGridRow();
	}

	private object Activate_62_DataGridTemplateColumn()
	{
		return new DataGridTemplateColumn();
	}

	private object Activate_63_DataGridTextColumn()
	{
		return new DataGridTextColumn();
	}

	private object Activate_68_ProgressBar()
	{
		return new ProgressBar();
	}

	private object Activate_71_HistoryPage()
	{
		return new HistoryPage();
	}

	private object Activate_72_ObservableCollection()
	{
		return new ObservableCollection<ProcessingTask>();
	}

	private object Activate_73_Collection()
	{
		return new Collection<ProcessingTask>();
	}

	private object Activate_74_ProcessingTask()
	{
		return new ProcessingTask();
	}

	private object Activate_76_CancellationTokenSource()
	{
		return new CancellationTokenSource();
	}

	private object Activate_77_List()
	{
		return new List<string>();
	}

	private object Activate_82_NumberBox()
	{
		return new NumberBox();
	}

	private object Activate_88_SettingsPage()
	{
		return new SettingsPage();
	}

	private object Activate_89_AppSettings()
	{
		return new AppSettings();
	}

	private object Activate_91_TasksPage()
	{
		return new TasksPage();
	}

	private object Activate_92_TasksViewModel()
	{
		return new TasksViewModel();
	}

	private object Activate_93_ObservableCollection()
	{
		return new ObservableCollection<LogMessage>();
	}

	private object Activate_94_Collection()
	{
		return new Collection<LogMessage>();
	}

	private object Activate_95_LogMessage()
	{
		return new LogMessage();
	}

	private object Activate_97_TreeViewNode()
	{
		return new TreeViewNode();
	}

	private void MapAdd_0_XamlControlsResources(object instance, object key, object item)
	{
		IDictionary<object, object> dictionary = (IDictionary<object, object>)instance;
		dictionary.Add(key, item);
	}

	private void VectorAdd_10_IList(object instance, object item)
	{
		ICollection<object> collection = (ICollection<object>)instance;
		collection.Add(item);
	}

	private void VectorAdd_44_ObservableCollection(object instance, object item)
	{
		ICollection<DataGridColumn> collection = (ICollection<DataGridColumn>)instance;
		DataGridColumn item2 = (DataGridColumn)item;
		collection.Add(item2);
	}

	private void VectorAdd_45_Collection(object instance, object item)
	{
		ICollection<DataGridColumn> collection = (ICollection<DataGridColumn>)instance;
		DataGridColumn item2 = (DataGridColumn)item;
		collection.Add(item2);
	}

	private void VectorAdd_57_ObservableCollection(object instance, object item)
	{
		ICollection<Style> collection = (ICollection<Style>)instance;
		Style item2 = (Style)item;
		collection.Add(item2);
	}

	private void VectorAdd_58_Collection(object instance, object item)
	{
		ICollection<Style> collection = (ICollection<Style>)instance;
		Style item2 = (Style)item;
		collection.Add(item2);
	}

	private void VectorAdd_72_ObservableCollection(object instance, object item)
	{
		ICollection<ProcessingTask> collection = (ICollection<ProcessingTask>)instance;
		ProcessingTask item2 = (ProcessingTask)item;
		collection.Add(item2);
	}

	private void VectorAdd_73_Collection(object instance, object item)
	{
		ICollection<ProcessingTask> collection = (ICollection<ProcessingTask>)instance;
		ProcessingTask item2 = (ProcessingTask)item;
		collection.Add(item2);
	}

	private void VectorAdd_77_List(object instance, object item)
	{
		ICollection<string> collection = (ICollection<string>)instance;
		string item2 = (string)item;
		collection.Add(item2);
	}

	private void VectorAdd_93_ObservableCollection(object instance, object item)
	{
		ICollection<LogMessage> collection = (ICollection<LogMessage>)instance;
		LogMessage item2 = (LogMessage)item;
		collection.Add(item2);
	}

	private void VectorAdd_94_Collection(object instance, object item)
	{
		ICollection<LogMessage> collection = (ICollection<LogMessage>)instance;
		LogMessage item2 = (LogMessage)item;
		collection.Add(item2);
	}

	private void VectorAdd_98_IList(object instance, object item)
	{
		ICollection<TreeViewNode> collection = (ICollection<TreeViewNode>)instance;
		TreeViewNode item2 = (TreeViewNode)item;
		collection.Add(item2);
	}

	private IXamlType CreateXamlType(int typeIndex)
	{
		XamlSystemBaseType result = null;
		string fullName = _typeNameTable[typeIndex];
		Type type = _typeTable[typeIndex];
		switch (typeIndex)
		{
		case 0:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.ResourceDictionary"));
			xamlUserType.Activator = Activate_0_XamlControlsResources;
			xamlUserType.DictionaryAdd = MapAdd_0_XamlControlsResources;
			xamlUserType.AddMemberName("UseCompactResources");
			result = xamlUserType;
			break;
		}
		case 1:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 2:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 3:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 4:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ContentControl"));
			xamlUserType.Activator = Activate_4_NavigationView;
			xamlUserType.AddMemberName("PaneDisplayMode");
			xamlUserType.AddMemberName("OpenPaneLength");
			xamlUserType.AddMemberName("MenuItems");
			xamlUserType.AddMemberName("FooterMenuItems");
			xamlUserType.AddMemberName("AlwaysShowHeader");
			xamlUserType.AddMemberName("AutoSuggestBox");
			xamlUserType.AddMemberName("CompactModeThresholdWidth");
			xamlUserType.AddMemberName("CompactPaneLength");
			xamlUserType.AddMemberName("ContentOverlay");
			xamlUserType.AddMemberName("DisplayMode");
			xamlUserType.AddMemberName("ExpandedModeThresholdWidth");
			xamlUserType.AddMemberName("FooterMenuItemsSource");
			xamlUserType.AddMemberName("Header");
			xamlUserType.AddMemberName("HeaderTemplate");
			xamlUserType.AddMemberName("IsBackButtonVisible");
			xamlUserType.AddMemberName("IsBackEnabled");
			xamlUserType.AddMemberName("IsPaneOpen");
			xamlUserType.AddMemberName("IsPaneToggleButtonVisible");
			xamlUserType.AddMemberName("IsPaneVisible");
			xamlUserType.AddMemberName("IsSettingsVisible");
			xamlUserType.AddMemberName("IsTitleBarAutoPaddingEnabled");
			xamlUserType.AddMemberName("MenuItemContainerStyle");
			xamlUserType.AddMemberName("MenuItemContainerStyleSelector");
			xamlUserType.AddMemberName("MenuItemTemplate");
			xamlUserType.AddMemberName("MenuItemTemplateSelector");
			xamlUserType.AddMemberName("MenuItemsSource");
			xamlUserType.AddMemberName("OverflowLabelMode");
			xamlUserType.AddMemberName("PaneCustomContent");
			xamlUserType.AddMemberName("PaneFooter");
			xamlUserType.AddMemberName("PaneHeader");
			xamlUserType.AddMemberName("PaneTitle");
			xamlUserType.AddMemberName("PaneToggleButtonStyle");
			xamlUserType.AddMemberName("SelectedItem");
			xamlUserType.AddMemberName("SelectionFollowsFocus");
			xamlUserType.AddMemberName("SettingsItem");
			xamlUserType.AddMemberName("ShoulderNavigationEnabled");
			xamlUserType.AddMemberName("TemplateSettings");
			result = xamlUserType;
			break;
		}
		case 5:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 6:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Enum"));
			xamlUserType.AddEnumValue("Auto", NavigationViewPaneDisplayMode.Auto);
			xamlUserType.AddEnumValue("Left", NavigationViewPaneDisplayMode.Left);
			xamlUserType.AddEnumValue("Top", NavigationViewPaneDisplayMode.Top);
			xamlUserType.AddEnumValue("LeftCompact", NavigationViewPaneDisplayMode.LeftCompact);
			xamlUserType.AddEnumValue("LeftMinimal", NavigationViewPaneDisplayMode.LeftMinimal);
			result = xamlUserType;
			break;
		}
		case 7:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.ValueType"));
			result = xamlUserType;
			break;
		}
		case 8:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Object"));
			result = xamlUserType;
			break;
		}
		case 9:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 10:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, null);
			xamlUserType.CollectionAdd = VectorAdd_10_IList;
			xamlUserType.SetIsReturnTypeStub();
			result = xamlUserType;
			break;
		}
		case 11:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 12:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 13:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Enum"));
			xamlUserType.AddEnumValue("Minimal", NavigationViewDisplayMode.Minimal);
			xamlUserType.AddEnumValue("Compact", NavigationViewDisplayMode.Compact);
			xamlUserType.AddEnumValue("Expanded", NavigationViewDisplayMode.Expanded);
			result = xamlUserType;
			break;
		}
		case 14:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 15:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Enum"));
			xamlUserType.AddEnumValue("Collapsed", NavigationViewBackButtonVisible.Collapsed);
			xamlUserType.AddEnumValue("Visible", NavigationViewBackButtonVisible.Visible);
			xamlUserType.AddEnumValue("Auto", NavigationViewBackButtonVisible.Auto);
			result = xamlUserType;
			break;
		}
		case 16:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 17:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 18:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 19:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Enum"));
			xamlUserType.AddEnumValue("MoreLabel", NavigationViewOverflowLabelMode.MoreLabel);
			xamlUserType.AddEnumValue("NoLabel", NavigationViewOverflowLabelMode.NoLabel);
			result = xamlUserType;
			break;
		}
		case 20:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 21:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Enum"));
			xamlUserType.AddEnumValue("Disabled", NavigationViewSelectionFollowsFocus.Disabled);
			xamlUserType.AddEnumValue("Enabled", NavigationViewSelectionFollowsFocus.Enabled);
			result = xamlUserType;
			break;
		}
		case 22:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Enum"));
			xamlUserType.AddEnumValue("WhenSelectionFollowsFocus", NavigationViewShoulderNavigationEnabled.WhenSelectionFollowsFocus);
			xamlUserType.AddEnumValue("Always", NavigationViewShoulderNavigationEnabled.Always);
			xamlUserType.AddEnumValue("Never", NavigationViewShoulderNavigationEnabled.Never);
			result = xamlUserType;
			break;
		}
		case 23:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.DependencyObject"));
			xamlUserType.SetIsReturnTypeStub();
			result = xamlUserType;
			break;
		}
		case 24:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 25:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationViewItemBase"));
			xamlUserType.Activator = Activate_25_NavigationViewItem;
			xamlUserType.AddMemberName("Icon");
			xamlUserType.AddMemberName("CompactPaneLength");
			xamlUserType.AddMemberName("HasUnrealizedChildren");
			xamlUserType.AddMemberName("InfoBadge");
			xamlUserType.AddMemberName("IsChildSelected");
			xamlUserType.AddMemberName("IsExpanded");
			xamlUserType.AddMemberName("MenuItems");
			xamlUserType.AddMemberName("MenuItemsSource");
			xamlUserType.AddMemberName("SelectsOnInvoked");
			result = xamlUserType;
			break;
		}
		case 26:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ContentControl"));
			xamlUserType.AddMemberName("IsSelected");
			result = xamlUserType;
			break;
		}
		case 27:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 28:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.Control"));
			xamlUserType.SetIsReturnTypeStub();
			result = xamlUserType;
			break;
		}
		case 29:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 30:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationViewItemBase"));
			xamlUserType.Activator = Activate_30_NavigationViewItemSeparator;
			result = xamlUserType;
			break;
		}
		case 31:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Window"));
			xamlUserType.Activator = Activate_31_MainWindow;
			xamlUserType.SetIsLocalType();
			result = xamlUserType;
			break;
		}
		case 32:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 33:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.Control"));
			xamlUserType.Activator = Activate_33_ProgressRing;
			xamlUserType.AddMemberName("IsActive");
			xamlUserType.AddMemberName("IsIndeterminate");
			xamlUserType.AddMemberName("Maximum");
			xamlUserType.AddMemberName("Minimum");
			xamlUserType.AddMemberName("TemplateSettings");
			xamlUserType.AddMemberName("Value");
			result = xamlUserType;
			break;
		}
		case 34:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.DependencyObject"));
			xamlUserType.SetIsReturnTypeStub();
			result = xamlUserType;
			break;
		}
		case 35:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.Page"));
			xamlUserType.Activator = Activate_35_CatalogPage;
			xamlUserType.SetIsLocalType();
			result = xamlUserType;
			break;
		}
		case 36:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 37:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 38:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.Control"));
			xamlUserType.Activator = Activate_38_DataGrid;
			xamlUserType.AddMemberName("ItemsSource");
			xamlUserType.AddMemberName("AutoGenerateColumns");
			xamlUserType.AddMemberName("GridLinesVisibility");
			xamlUserType.AddMemberName("HeadersVisibility");
			xamlUserType.AddMemberName("RowDetailsVisibilityMode");
			xamlUserType.AddMemberName("SelectionMode");
			xamlUserType.AddMemberName("CanUserSortColumns");
			xamlUserType.AddMemberName("CanUserReorderColumns");
			xamlUserType.AddMemberName("RowHeight");
			xamlUserType.AddMemberName("ColumnHeaderStyle");
			xamlUserType.AddMemberName("RowStyle");
			xamlUserType.AddMemberName("Columns");
			xamlUserType.AddMemberName("RowDetailsTemplate");
			xamlUserType.AddMemberName("AlternatingRowBackground");
			xamlUserType.AddMemberName("AlternatingRowForeground");
			xamlUserType.AddMemberName("AreRowDetailsFrozen");
			xamlUserType.AddMemberName("AreRowGroupHeadersFrozen");
			xamlUserType.AddMemberName("CanUserResizeColumns");
			xamlUserType.AddMemberName("CellStyle");
			xamlUserType.AddMemberName("ClipboardCopyMode");
			xamlUserType.AddMemberName("ColumnHeaderHeight");
			xamlUserType.AddMemberName("ColumnWidth");
			xamlUserType.AddMemberName("DataFetchSize");
			xamlUserType.AddMemberName("DragIndicatorStyle");
			xamlUserType.AddMemberName("DropLocationIndicatorStyle");
			xamlUserType.AddMemberName("FrozenColumnCount");
			xamlUserType.AddMemberName("HorizontalGridLinesBrush");
			xamlUserType.AddMemberName("HorizontalScrollBarVisibility");
			xamlUserType.AddMemberName("IsReadOnly");
			xamlUserType.AddMemberName("IsValid");
			xamlUserType.AddMemberName("IncrementalLoadingThreshold");
			xamlUserType.AddMemberName("IncrementalLoadingTrigger");
			xamlUserType.AddMemberName("MaxColumnWidth");
			xamlUserType.AddMemberName("MinColumnWidth");
			xamlUserType.AddMemberName("RowBackground");
			xamlUserType.AddMemberName("RowForeground");
			xamlUserType.AddMemberName("RowHeaderWidth");
			xamlUserType.AddMemberName("RowHeaderStyle");
			xamlUserType.AddMemberName("SelectedIndex");
			xamlUserType.AddMemberName("SelectedItem");
			xamlUserType.AddMemberName("VerticalGridLinesBrush");
			xamlUserType.AddMemberName("VerticalScrollBarVisibility");
			xamlUserType.AddMemberName("CurrentColumn");
			xamlUserType.AddMemberName("RowGroupHeaderPropertyNameAlternative");
			xamlUserType.AddMemberName("RowGroupHeaderStyles");
			xamlUserType.AddMemberName("SelectedItems");
			result = xamlUserType;
			break;
		}
		case 39:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, null);
			xamlUserType.SetIsReturnTypeStub();
			result = xamlUserType;
			break;
		}
		case 40:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Enum"));
			xamlUserType.AddEnumValue("None", DataGridGridLinesVisibility.None);
			xamlUserType.AddEnumValue("Horizontal", DataGridGridLinesVisibility.Horizontal);
			xamlUserType.AddEnumValue("Vertical", DataGridGridLinesVisibility.Vertical);
			xamlUserType.AddEnumValue("All", DataGridGridLinesVisibility.All);
			result = xamlUserType;
			break;
		}
		case 41:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Enum"));
			xamlUserType.AddEnumValue("None", DataGridHeadersVisibility.None);
			xamlUserType.AddEnumValue("Column", DataGridHeadersVisibility.Column);
			xamlUserType.AddEnumValue("Row", DataGridHeadersVisibility.Row);
			xamlUserType.AddEnumValue("All", DataGridHeadersVisibility.All);
			result = xamlUserType;
			break;
		}
		case 42:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Enum"));
			xamlUserType.AddEnumValue("VisibleWhenSelected", DataGridRowDetailsVisibilityMode.VisibleWhenSelected);
			xamlUserType.AddEnumValue("Visible", DataGridRowDetailsVisibilityMode.Visible);
			xamlUserType.AddEnumValue("Collapsed", DataGridRowDetailsVisibilityMode.Collapsed);
			result = xamlUserType;
			break;
		}
		case 43:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Enum"));
			xamlUserType.AddEnumValue("Extended", DataGridSelectionMode.Extended);
			xamlUserType.AddEnumValue("Single", DataGridSelectionMode.Single);
			result = xamlUserType;
			break;
		}
		case 44:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Collections.ObjectModel.Collection`1<CommunityToolkit.WinUI.UI.Controls.DataGridColumn>"));
			xamlUserType.CollectionAdd = VectorAdd_44_ObservableCollection;
			xamlUserType.SetIsReturnTypeStub();
			result = xamlUserType;
			break;
		}
		case 45:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Object"));
			xamlUserType.Activator = Activate_45_Collection;
			xamlUserType.CollectionAdd = VectorAdd_45_Collection;
			result = xamlUserType;
			break;
		}
		case 46:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.DependencyObject"));
			xamlUserType.AddMemberName("ActualWidth");
			xamlUserType.AddMemberName("CanUserReorder");
			xamlUserType.AddMemberName("CanUserResize");
			xamlUserType.AddMemberName("CanUserSort");
			xamlUserType.AddMemberName("CellStyle");
			xamlUserType.AddMemberName("ClipboardContentBinding");
			xamlUserType.AddMemberName("DisplayIndex");
			xamlUserType.AddMemberName("DragIndicatorStyle");
			xamlUserType.AddMemberName("HeaderStyle");
			xamlUserType.AddMemberName("Header");
			xamlUserType.AddMemberName("IsAutoGenerated");
			xamlUserType.AddMemberName("IsFrozen");
			xamlUserType.AddMemberName("IsReadOnly");
			xamlUserType.AddMemberName("MaxWidth");
			xamlUserType.AddMemberName("MinWidth");
			xamlUserType.AddMemberName("SortDirection");
			xamlUserType.AddMemberName("Tag");
			xamlUserType.AddMemberName("Visibility");
			xamlUserType.AddMemberName("Width");
			result = xamlUserType;
			break;
		}
		case 47:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 48:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 49:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.ValueType"));
			xamlUserType.SetBoxedType(GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridSortDirection"));
			xamlUserType.BoxInstance = xamlUserType.BoxType<DataGridSortDirection>;
			xamlUserType.SetIsReturnTypeStub();
			result = xamlUserType;
			break;
		}
		case 50:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Enum"));
			xamlUserType.AddEnumValue("Ascending", DataGridSortDirection.Ascending);
			xamlUserType.AddEnumValue("Descending", DataGridSortDirection.Descending);
			result = xamlUserType;
			break;
		}
		case 51:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 52:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.ValueType"));
			xamlUserType.CreateFromStringMethod = (string x) => DataGridLength.ConvertFromString(x);
			xamlUserType.SetIsReturnTypeStub();
			result = xamlUserType;
			break;
		}
		case 53:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 54:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Enum"));
			xamlUserType.AddEnumValue("None", DataGridClipboardCopyMode.None);
			xamlUserType.AddEnumValue("ExcludeHeader", DataGridClipboardCopyMode.ExcludeHeader);
			xamlUserType.AddEnumValue("IncludeHeader", DataGridClipboardCopyMode.IncludeHeader);
			result = xamlUserType;
			break;
		}
		case 55:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 56:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 57:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Collections.ObjectModel.Collection`1<Microsoft.UI.Xaml.Style>"));
			xamlUserType.CollectionAdd = VectorAdd_57_ObservableCollection;
			xamlUserType.SetIsReturnTypeStub();
			result = xamlUserType;
			break;
		}
		case 58:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Object"));
			xamlUserType.Activator = Activate_58_Collection;
			xamlUserType.CollectionAdd = VectorAdd_58_Collection;
			result = xamlUserType;
			break;
		}
		case 59:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, null);
			xamlUserType.SetIsReturnTypeStub();
			result = xamlUserType;
			break;
		}
		case 60:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ContentControl"));
			xamlUserType.Activator = Activate_60_DataGridColumnHeader;
			xamlUserType.AddMemberName("SeparatorBrush");
			xamlUserType.AddMemberName("SeparatorVisibility");
			result = xamlUserType;
			break;
		}
		case 61:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.Control"));
			xamlUserType.Activator = Activate_61_DataGridRow;
			xamlUserType.AddMemberName("DetailsTemplate");
			xamlUserType.AddMemberName("DetailsVisibility");
			xamlUserType.AddMemberName("Header");
			xamlUserType.AddMemberName("HeaderStyle");
			xamlUserType.AddMemberName("IsValid");
			result = xamlUserType;
			break;
		}
		case 62:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridColumn"));
			xamlUserType.Activator = Activate_62_DataGridTemplateColumn;
			xamlUserType.AddMemberName("CellTemplate");
			xamlUserType.AddMemberName("CellEditingTemplate");
			result = xamlUserType;
			break;
		}
		case 63:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridBoundColumn"));
			xamlUserType.Activator = Activate_63_DataGridTextColumn;
			xamlUserType.AddMemberName("FontFamily");
			xamlUserType.AddMemberName("FontSize");
			xamlUserType.AddMemberName("FontStyle");
			xamlUserType.AddMemberName("FontWeight");
			xamlUserType.AddMemberName("Foreground");
			result = xamlUserType;
			break;
		}
		case 64:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridColumn"));
			xamlUserType.AddMemberName("Binding");
			xamlUserType.AddMemberName("ElementStyle");
			xamlUserType.AddMemberName("ClipboardContentBinding");
			xamlUserType.AddMemberName("EditingElementStyle");
			result = xamlUserType;
			break;
		}
		case 65:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 66:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Enum"));
			xamlUserType.AddEnumValue("Normal", FontStyle.Normal);
			xamlUserType.AddEnumValue("Oblique", FontStyle.Oblique);
			xamlUserType.AddEnumValue("Italic", FontStyle.Italic);
			result = xamlUserType;
			break;
		}
		case 67:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.ValueType"));
			xamlUserType.SetIsReturnTypeStub();
			result = xamlUserType;
			break;
		}
		case 68:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.Primitives.RangeBase"));
			xamlUserType.Activator = Activate_68_ProgressBar;
			xamlUserType.AddMemberName("IsIndeterminate");
			xamlUserType.AddMemberName("ShowError");
			xamlUserType.AddMemberName("ShowPaused");
			xamlUserType.AddMemberName("TemplateSettings");
			result = xamlUserType;
			break;
		}
		case 69:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 70:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.DependencyObject"));
			xamlUserType.SetIsReturnTypeStub();
			result = xamlUserType;
			break;
		}
		case 71:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.Page"));
			xamlUserType.Activator = Activate_71_HistoryPage;
			xamlUserType.AddMemberName("HistoryTasks");
			xamlUserType.SetIsLocalType();
			result = xamlUserType;
			break;
		}
		case 72:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Collections.ObjectModel.Collection`1<StormSwitchBox.Models.ProcessingTask>"));
			xamlUserType.CollectionAdd = VectorAdd_72_ObservableCollection;
			xamlUserType.SetIsReturnTypeStub();
			result = xamlUserType;
			break;
		}
		case 73:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Object"));
			xamlUserType.Activator = Activate_73_Collection;
			xamlUserType.CollectionAdd = VectorAdd_73_Collection;
			result = xamlUserType;
			break;
		}
		case 74:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("CommunityToolkit.Mvvm.ComponentModel.ObservableObject"));
			xamlUserType.Activator = Activate_74_ProcessingTask;
			xamlUserType.AddMemberName("Cts");
			xamlUserType.AddMemberName("FinishedAtDisplay");
			xamlUserType.AddMemberName("IsNotRunning");
			xamlUserType.AddMemberName("GroupId");
			xamlUserType.AddMemberName("InputFiles");
			xamlUserType.AddMemberName("FilesList");
			xamlUserType.AddMemberName("DetailsVisibility");
			xamlUserType.AddMemberName("OperationDisplay");
			xamlUserType.AddMemberName("StatusColor");
			xamlUserType.AddMemberName("Id");
			xamlUserType.AddMemberName("GameIcon");
			xamlUserType.AddMemberName("GameName");
			xamlUserType.AddMemberName("Operation");
			xamlUserType.AddMemberName("SourceFormat");
			xamlUserType.AddMemberName("TargetFormat");
			xamlUserType.AddMemberName("SourceSize");
			xamlUserType.AddMemberName("TargetSize");
			xamlUserType.AddMemberName("SizeDifference");
			xamlUserType.AddMemberName("CompressionLevel");
			xamlUserType.AddMemberName("FilesCount");
			xamlUserType.AddMemberName("Status");
			xamlUserType.AddMemberName("Progress");
			xamlUserType.AddMemberName("IsRunning");
			xamlUserType.AddMemberName("FinishedAt");
			xamlUserType.AddMemberName("SourceSizeBytes");
			xamlUserType.AddMemberName("HasRomFs");
			xamlUserType.AddMemberName("HasExeFs");
			xamlUserType.AddMemberName("Speed");
			xamlUserType.AddMemberName("IsExpanded");
			xamlUserType.AddMemberName("VerifyType");
			xamlUserType.AddMemberName("VerifyStructure");
			xamlUserType.AddMemberName("VerifyTitleId");
			xamlUserType.AddMemberName("VerifyVersion");
			xamlUserType.AddMemberName("VerifyMergedStatus");
			xamlUserType.AddMemberName("InputFolders");
			xamlUserType.AddMemberName("OutputFolder");
			xamlUserType.AddMemberName("OutputFileName");
			xamlUserType.AddMemberName("LogDetails");
			xamlUserType.SetIsLocalType();
			result = xamlUserType;
			break;
		}
		case 75:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Object"));
			result = xamlUserType;
			break;
		}
		case 76:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Object"));
			xamlUserType.SetIsReturnTypeStub();
			result = xamlUserType;
			break;
		}
		case 77:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Object"));
			xamlUserType.CollectionAdd = VectorAdd_77_List;
			xamlUserType.SetIsReturnTypeStub();
			result = xamlUserType;
			break;
		}
		case 78:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 79:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 80:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.ValueType"));
			xamlUserType.SetIsReturnTypeStub();
			result = xamlUserType;
			break;
		}
		case 81:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 82:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.Control"));
			xamlUserType.Activator = Activate_82_NumberBox;
			xamlUserType.AddMemberName("Value");
			xamlUserType.AddMemberName("Minimum");
			xamlUserType.AddMemberName("Maximum");
			xamlUserType.AddMemberName("SpinButtonPlacementMode");
			xamlUserType.AddMemberName("AcceptsExpression");
			xamlUserType.AddMemberName("Description");
			xamlUserType.AddMemberName("Header");
			xamlUserType.AddMemberName("HeaderTemplate");
			xamlUserType.AddMemberName("IsWrapEnabled");
			xamlUserType.AddMemberName("LargeChange");
			xamlUserType.AddMemberName("NumberFormatter");
			xamlUserType.AddMemberName("PlaceholderText");
			xamlUserType.AddMemberName("PreventKeyboardDisplayOnProgrammaticFocus");
			xamlUserType.AddMemberName("SelectionFlyout");
			xamlUserType.AddMemberName("SelectionHighlightColor");
			xamlUserType.AddMemberName("SmallChange");
			xamlUserType.AddMemberName("Text");
			xamlUserType.AddMemberName("TextReadingOrder");
			xamlUserType.AddMemberName("ValidationMode");
			result = xamlUserType;
			break;
		}
		case 83:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Enum"));
			xamlUserType.AddEnumValue("Hidden", NumberBoxSpinButtonPlacementMode.Hidden);
			xamlUserType.AddEnumValue("Compact", NumberBoxSpinButtonPlacementMode.Compact);
			xamlUserType.AddEnumValue("Inline", NumberBoxSpinButtonPlacementMode.Inline);
			result = xamlUserType;
			break;
		}
		case 84:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, null);
			xamlUserType.SetIsReturnTypeStub();
			result = xamlUserType;
			break;
		}
		case 85:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 86:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 87:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Enum"));
			xamlUserType.AddEnumValue("InvalidInputOverwritten", NumberBoxValidationMode.InvalidInputOverwritten);
			xamlUserType.AddEnumValue("Disabled", NumberBoxValidationMode.Disabled);
			result = xamlUserType;
			break;
		}
		case 88:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.Page"));
			xamlUserType.Activator = Activate_88_SettingsPage;
			xamlUserType.AddMemberName("MaxCores");
			xamlUserType.AddMemberName("Settings");
			xamlUserType.AddMemberName("KeysSelectedVisibility");
			xamlUserType.SetIsLocalType();
			result = xamlUserType;
			break;
		}
		case 89:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Object"));
			xamlUserType.SetIsReturnTypeStub();
			xamlUserType.SetIsLocalType();
			result = xamlUserType;
			break;
		}
		case 90:
			result = new XamlSystemBaseType(fullName, type);
			break;
		case 91:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.Controls.Page"));
			xamlUserType.Activator = Activate_91_TasksPage;
			xamlUserType.AddMemberName("ViewModel");
			xamlUserType.AddMemberName("AppLogs");
			xamlUserType.SetIsLocalType();
			result = xamlUserType;
			break;
		}
		case 92:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("CommunityToolkit.Mvvm.ComponentModel.ObservableObject"));
			xamlUserType.SetIsReturnTypeStub();
			xamlUserType.SetIsLocalType();
			result = xamlUserType;
			break;
		}
		case 93:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Collections.ObjectModel.Collection`1<StormSwitchBox.Models.LogMessage>"));
			xamlUserType.CollectionAdd = VectorAdd_93_ObservableCollection;
			xamlUserType.SetIsReturnTypeStub();
			result = xamlUserType;
			break;
		}
		case 94:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Object"));
			xamlUserType.Activator = Activate_94_Collection;
			xamlUserType.CollectionAdd = VectorAdd_94_Collection;
			result = xamlUserType;
			break;
		}
		case 95:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Object"));
			xamlUserType.Activator = Activate_95_LogMessage;
			xamlUserType.AddMemberName("Timestamp");
			xamlUserType.AddMemberName("Message");
			xamlUserType.AddMemberName("Level");
			xamlUserType.AddMemberName("ColorBrush");
			xamlUserType.AddMemberName("FormattedTime");
			xamlUserType.SetIsLocalType();
			result = xamlUserType;
			break;
		}
		case 96:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("System.Enum"));
			xamlUserType.AddEnumValue("Info", LogLevel.Info);
			xamlUserType.AddEnumValue("Warning", LogLevel.Warning);
			xamlUserType.AddEnumValue("Error", LogLevel.Error);
			xamlUserType.AddEnumValue("Success", LogLevel.Success);
			xamlUserType.AddEnumValue("Debug", LogLevel.Debug);
			xamlUserType.SetIsLocalType();
			result = xamlUserType;
			break;
		}
		case 97:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, GetXamlTypeByName("Microsoft.UI.Xaml.DependencyObject"));
			xamlUserType.Activator = Activate_97_TreeViewNode;
			xamlUserType.AddMemberName("Children");
			xamlUserType.AddMemberName("Content");
			xamlUserType.AddMemberName("Depth");
			xamlUserType.AddMemberName("HasChildren");
			xamlUserType.AddMemberName("HasUnrealizedChildren");
			xamlUserType.AddMemberName("IsExpanded");
			xamlUserType.AddMemberName("Parent");
			xamlUserType.SetIsBindable();
			result = xamlUserType;
			break;
		}
		case 98:
		{
			XamlUserType xamlUserType = new XamlUserType(this, fullName, type, null);
			xamlUserType.CollectionAdd = VectorAdd_98_IList;
			xamlUserType.SetIsReturnTypeStub();
			result = xamlUserType;
			break;
		}
		}
		return result;
	}

	private IXamlType CheckOtherMetadataProvidersForName(string typeName)
	{
		IXamlType xamlType = null;
		IXamlType result = null;
		foreach (IXamlMetadataProvider otherProvider in OtherProviders)
		{
			xamlType = otherProvider.GetXamlType(typeName);
			if (xamlType != null)
			{
				if (xamlType.IsConstructible)
				{
					return xamlType;
				}
				result = xamlType;
			}
		}
		return result;
	}

	private IXamlType CheckOtherMetadataProvidersForType(Type type)
	{
		IXamlType xamlType = null;
		IXamlType result = null;
		foreach (IXamlMetadataProvider otherProvider in OtherProviders)
		{
			xamlType = otherProvider.GetXamlType(type);
			if (xamlType != null)
			{
				if (xamlType.IsConstructible)
				{
					return xamlType;
				}
				result = xamlType;
			}
		}
		return result;
	}

	private object get_0_XamlControlsResources_UseCompactResources(object instance)
	{
		XamlControlsResources xamlControlsResources = (XamlControlsResources)instance;
		return xamlControlsResources.UseCompactResources;
	}

	private void set_0_XamlControlsResources_UseCompactResources(object instance, object Value)
	{
		XamlControlsResources xamlControlsResources = (XamlControlsResources)instance;
		xamlControlsResources.UseCompactResources = (bool)Value;
	}

	private object get_1_NavigationView_PaneDisplayMode(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.PaneDisplayMode;
	}

	private void set_1_NavigationView_PaneDisplayMode(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.PaneDisplayMode = (NavigationViewPaneDisplayMode)Value;
	}

	private object get_2_NavigationView_OpenPaneLength(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.OpenPaneLength;
	}

	private void set_2_NavigationView_OpenPaneLength(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.OpenPaneLength = (double)Value;
	}

	private object get_3_NavigationView_MenuItems(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.MenuItems;
	}

	private object get_4_NavigationView_FooterMenuItems(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.FooterMenuItems;
	}

	private object get_5_NavigationView_AlwaysShowHeader(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.AlwaysShowHeader;
	}

	private void set_5_NavigationView_AlwaysShowHeader(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.AlwaysShowHeader = (bool)Value;
	}

	private object get_6_NavigationView_AutoSuggestBox(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.AutoSuggestBox;
	}

	private void set_6_NavigationView_AutoSuggestBox(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.AutoSuggestBox = (AutoSuggestBox)Value;
	}

	private object get_7_NavigationView_CompactModeThresholdWidth(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.CompactModeThresholdWidth;
	}

	private void set_7_NavigationView_CompactModeThresholdWidth(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.CompactModeThresholdWidth = (double)Value;
	}

	private object get_8_NavigationView_CompactPaneLength(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.CompactPaneLength;
	}

	private void set_8_NavigationView_CompactPaneLength(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.CompactPaneLength = (double)Value;
	}

	private object get_9_NavigationView_ContentOverlay(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.ContentOverlay;
	}

	private void set_9_NavigationView_ContentOverlay(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.ContentOverlay = (UIElement)Value;
	}

	private object get_10_NavigationView_DisplayMode(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.DisplayMode;
	}

	private object get_11_NavigationView_ExpandedModeThresholdWidth(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.ExpandedModeThresholdWidth;
	}

	private void set_11_NavigationView_ExpandedModeThresholdWidth(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.ExpandedModeThresholdWidth = (double)Value;
	}

	private object get_12_NavigationView_FooterMenuItemsSource(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.FooterMenuItemsSource;
	}

	private void set_12_NavigationView_FooterMenuItemsSource(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.FooterMenuItemsSource = Value;
	}

	private object get_13_NavigationView_Header(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.Header;
	}

	private void set_13_NavigationView_Header(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.Header = Value;
	}

	private object get_14_NavigationView_HeaderTemplate(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.HeaderTemplate;
	}

	private void set_14_NavigationView_HeaderTemplate(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.HeaderTemplate = (DataTemplate)Value;
	}

	private object get_15_NavigationView_IsBackButtonVisible(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.IsBackButtonVisible;
	}

	private void set_15_NavigationView_IsBackButtonVisible(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.IsBackButtonVisible = (NavigationViewBackButtonVisible)Value;
	}

	private object get_16_NavigationView_IsBackEnabled(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.IsBackEnabled;
	}

	private void set_16_NavigationView_IsBackEnabled(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.IsBackEnabled = (bool)Value;
	}

	private object get_17_NavigationView_IsPaneOpen(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.IsPaneOpen;
	}

	private void set_17_NavigationView_IsPaneOpen(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.IsPaneOpen = (bool)Value;
	}

	private object get_18_NavigationView_IsPaneToggleButtonVisible(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.IsPaneToggleButtonVisible;
	}

	private void set_18_NavigationView_IsPaneToggleButtonVisible(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.IsPaneToggleButtonVisible = (bool)Value;
	}

	private object get_19_NavigationView_IsPaneVisible(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.IsPaneVisible;
	}

	private void set_19_NavigationView_IsPaneVisible(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.IsPaneVisible = (bool)Value;
	}

	private object get_20_NavigationView_IsSettingsVisible(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.IsSettingsVisible;
	}

	private void set_20_NavigationView_IsSettingsVisible(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.IsSettingsVisible = (bool)Value;
	}

	private object get_21_NavigationView_IsTitleBarAutoPaddingEnabled(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.IsTitleBarAutoPaddingEnabled;
	}

	private void set_21_NavigationView_IsTitleBarAutoPaddingEnabled(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.IsTitleBarAutoPaddingEnabled = (bool)Value;
	}

	private object get_22_NavigationView_MenuItemContainerStyle(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.MenuItemContainerStyle;
	}

	private void set_22_NavigationView_MenuItemContainerStyle(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.MenuItemContainerStyle = (Style)Value;
	}

	private object get_23_NavigationView_MenuItemContainerStyleSelector(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.MenuItemContainerStyleSelector;
	}

	private void set_23_NavigationView_MenuItemContainerStyleSelector(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.MenuItemContainerStyleSelector = (StyleSelector)Value;
	}

	private object get_24_NavigationView_MenuItemTemplate(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.MenuItemTemplate;
	}

	private void set_24_NavigationView_MenuItemTemplate(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.MenuItemTemplate = (DataTemplate)Value;
	}

	private object get_25_NavigationView_MenuItemTemplateSelector(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.MenuItemTemplateSelector;
	}

	private void set_25_NavigationView_MenuItemTemplateSelector(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.MenuItemTemplateSelector = (DataTemplateSelector)Value;
	}

	private object get_26_NavigationView_MenuItemsSource(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.MenuItemsSource;
	}

	private void set_26_NavigationView_MenuItemsSource(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.MenuItemsSource = Value;
	}

	private object get_27_NavigationView_OverflowLabelMode(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.OverflowLabelMode;
	}

	private void set_27_NavigationView_OverflowLabelMode(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.OverflowLabelMode = (NavigationViewOverflowLabelMode)Value;
	}

	private object get_28_NavigationView_PaneCustomContent(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.PaneCustomContent;
	}

	private void set_28_NavigationView_PaneCustomContent(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.PaneCustomContent = (UIElement)Value;
	}

	private object get_29_NavigationView_PaneFooter(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.PaneFooter;
	}

	private void set_29_NavigationView_PaneFooter(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.PaneFooter = (UIElement)Value;
	}

	private object get_30_NavigationView_PaneHeader(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.PaneHeader;
	}

	private void set_30_NavigationView_PaneHeader(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.PaneHeader = (UIElement)Value;
	}

	private object get_31_NavigationView_PaneTitle(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.PaneTitle;
	}

	private void set_31_NavigationView_PaneTitle(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.PaneTitle = (string)Value;
	}

	private object get_32_NavigationView_PaneToggleButtonStyle(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.PaneToggleButtonStyle;
	}

	private void set_32_NavigationView_PaneToggleButtonStyle(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.PaneToggleButtonStyle = (Style)Value;
	}

	private object get_33_NavigationView_SelectedItem(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.SelectedItem;
	}

	private void set_33_NavigationView_SelectedItem(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.SelectedItem = Value;
	}

	private object get_34_NavigationView_SelectionFollowsFocus(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.SelectionFollowsFocus;
	}

	private void set_34_NavigationView_SelectionFollowsFocus(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.SelectionFollowsFocus = (NavigationViewSelectionFollowsFocus)Value;
	}

	private object get_35_NavigationView_SettingsItem(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.SettingsItem;
	}

	private object get_36_NavigationView_ShoulderNavigationEnabled(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.ShoulderNavigationEnabled;
	}

	private void set_36_NavigationView_ShoulderNavigationEnabled(object instance, object Value)
	{
		NavigationView navigationView = (NavigationView)instance;
		navigationView.ShoulderNavigationEnabled = (NavigationViewShoulderNavigationEnabled)Value;
	}

	private object get_37_NavigationView_TemplateSettings(object instance)
	{
		NavigationView navigationView = (NavigationView)instance;
		return navigationView.TemplateSettings;
	}

	private object get_38_NavigationViewItem_Icon(object instance)
	{
		NavigationViewItem navigationViewItem = (NavigationViewItem)instance;
		return navigationViewItem.Icon;
	}

	private void set_38_NavigationViewItem_Icon(object instance, object Value)
	{
		NavigationViewItem navigationViewItem = (NavigationViewItem)instance;
		navigationViewItem.Icon = (IconElement)Value;
	}

	private object get_39_NavigationViewItem_CompactPaneLength(object instance)
	{
		NavigationViewItem navigationViewItem = (NavigationViewItem)instance;
		return navigationViewItem.CompactPaneLength;
	}

	private object get_40_NavigationViewItem_HasUnrealizedChildren(object instance)
	{
		NavigationViewItem navigationViewItem = (NavigationViewItem)instance;
		return navigationViewItem.HasUnrealizedChildren;
	}

	private void set_40_NavigationViewItem_HasUnrealizedChildren(object instance, object Value)
	{
		NavigationViewItem navigationViewItem = (NavigationViewItem)instance;
		navigationViewItem.HasUnrealizedChildren = (bool)Value;
	}

	private object get_41_NavigationViewItem_InfoBadge(object instance)
	{
		NavigationViewItem navigationViewItem = (NavigationViewItem)instance;
		return navigationViewItem.InfoBadge;
	}

	private void set_41_NavigationViewItem_InfoBadge(object instance, object Value)
	{
		NavigationViewItem navigationViewItem = (NavigationViewItem)instance;
		navigationViewItem.InfoBadge = (InfoBadge)Value;
	}

	private object get_42_NavigationViewItem_IsChildSelected(object instance)
	{
		NavigationViewItem navigationViewItem = (NavigationViewItem)instance;
		return navigationViewItem.IsChildSelected;
	}

	private void set_42_NavigationViewItem_IsChildSelected(object instance, object Value)
	{
		NavigationViewItem navigationViewItem = (NavigationViewItem)instance;
		navigationViewItem.IsChildSelected = (bool)Value;
	}

	private object get_43_NavigationViewItem_IsExpanded(object instance)
	{
		NavigationViewItem navigationViewItem = (NavigationViewItem)instance;
		return navigationViewItem.IsExpanded;
	}

	private void set_43_NavigationViewItem_IsExpanded(object instance, object Value)
	{
		NavigationViewItem navigationViewItem = (NavigationViewItem)instance;
		navigationViewItem.IsExpanded = (bool)Value;
	}

	private object get_44_NavigationViewItem_MenuItems(object instance)
	{
		NavigationViewItem navigationViewItem = (NavigationViewItem)instance;
		return navigationViewItem.MenuItems;
	}

	private object get_45_NavigationViewItem_MenuItemsSource(object instance)
	{
		NavigationViewItem navigationViewItem = (NavigationViewItem)instance;
		return navigationViewItem.MenuItemsSource;
	}

	private void set_45_NavigationViewItem_MenuItemsSource(object instance, object Value)
	{
		NavigationViewItem navigationViewItem = (NavigationViewItem)instance;
		navigationViewItem.MenuItemsSource = Value;
	}

	private object get_46_NavigationViewItem_SelectsOnInvoked(object instance)
	{
		NavigationViewItem navigationViewItem = (NavigationViewItem)instance;
		return navigationViewItem.SelectsOnInvoked;
	}

	private void set_46_NavigationViewItem_SelectsOnInvoked(object instance, object Value)
	{
		NavigationViewItem navigationViewItem = (NavigationViewItem)instance;
		navigationViewItem.SelectsOnInvoked = (bool)Value;
	}

	private object get_47_NavigationViewItemBase_IsSelected(object instance)
	{
		NavigationViewItemBase navigationViewItemBase = (NavigationViewItemBase)instance;
		return navigationViewItemBase.IsSelected;
	}

	private void set_47_NavigationViewItemBase_IsSelected(object instance, object Value)
	{
		NavigationViewItemBase navigationViewItemBase = (NavigationViewItemBase)instance;
		navigationViewItemBase.IsSelected = (bool)Value;
	}

	private object get_48_ProgressRing_IsActive(object instance)
	{
		ProgressRing progressRing = (ProgressRing)instance;
		return progressRing.IsActive;
	}

	private void set_48_ProgressRing_IsActive(object instance, object Value)
	{
		ProgressRing progressRing = (ProgressRing)instance;
		progressRing.IsActive = (bool)Value;
	}

	private object get_49_ProgressRing_IsIndeterminate(object instance)
	{
		ProgressRing progressRing = (ProgressRing)instance;
		return progressRing.IsIndeterminate;
	}

	private void set_49_ProgressRing_IsIndeterminate(object instance, object Value)
	{
		ProgressRing progressRing = (ProgressRing)instance;
		progressRing.IsIndeterminate = (bool)Value;
	}

	private object get_50_ProgressRing_Maximum(object instance)
	{
		ProgressRing progressRing = (ProgressRing)instance;
		return progressRing.Maximum;
	}

	private void set_50_ProgressRing_Maximum(object instance, object Value)
	{
		ProgressRing progressRing = (ProgressRing)instance;
		progressRing.Maximum = (double)Value;
	}

	private object get_51_ProgressRing_Minimum(object instance)
	{
		ProgressRing progressRing = (ProgressRing)instance;
		return progressRing.Minimum;
	}

	private void set_51_ProgressRing_Minimum(object instance, object Value)
	{
		ProgressRing progressRing = (ProgressRing)instance;
		progressRing.Minimum = (double)Value;
	}

	private object get_52_ProgressRing_TemplateSettings(object instance)
	{
		ProgressRing progressRing = (ProgressRing)instance;
		return progressRing.TemplateSettings;
	}

	private object get_53_ProgressRing_Value(object instance)
	{
		ProgressRing progressRing = (ProgressRing)instance;
		return progressRing.Value;
	}

	private void set_53_ProgressRing_Value(object instance, object Value)
	{
		ProgressRing progressRing = (ProgressRing)instance;
		progressRing.Value = (double)Value;
	}

	private object get_54_DataGrid_ItemsSource(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.ItemsSource;
	}

	private void set_54_DataGrid_ItemsSource(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.ItemsSource = (IEnumerable)Value;
	}

	private object get_55_DataGrid_AutoGenerateColumns(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.AutoGenerateColumns;
	}

	private void set_55_DataGrid_AutoGenerateColumns(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.AutoGenerateColumns = (bool)Value;
	}

	private object get_56_DataGrid_GridLinesVisibility(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.GridLinesVisibility;
	}

	private void set_56_DataGrid_GridLinesVisibility(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.GridLinesVisibility = (DataGridGridLinesVisibility)Value;
	}

	private object get_57_DataGrid_HeadersVisibility(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.HeadersVisibility;
	}

	private void set_57_DataGrid_HeadersVisibility(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.HeadersVisibility = (DataGridHeadersVisibility)Value;
	}

	private object get_58_DataGrid_RowDetailsVisibilityMode(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.RowDetailsVisibilityMode;
	}

	private void set_58_DataGrid_RowDetailsVisibilityMode(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.RowDetailsVisibilityMode = (DataGridRowDetailsVisibilityMode)Value;
	}

	private object get_59_DataGrid_SelectionMode(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.SelectionMode;
	}

	private void set_59_DataGrid_SelectionMode(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.SelectionMode = (DataGridSelectionMode)Value;
	}

	private object get_60_DataGrid_CanUserSortColumns(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.CanUserSortColumns;
	}

	private void set_60_DataGrid_CanUserSortColumns(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.CanUserSortColumns = (bool)Value;
	}

	private object get_61_DataGrid_CanUserReorderColumns(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.CanUserReorderColumns;
	}

	private void set_61_DataGrid_CanUserReorderColumns(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.CanUserReorderColumns = (bool)Value;
	}

	private object get_62_DataGrid_RowHeight(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.RowHeight;
	}

	private void set_62_DataGrid_RowHeight(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.RowHeight = (double)Value;
	}

	private object get_63_DataGrid_ColumnHeaderStyle(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.ColumnHeaderStyle;
	}

	private void set_63_DataGrid_ColumnHeaderStyle(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.ColumnHeaderStyle = (Style)Value;
	}

	private object get_64_DataGrid_RowStyle(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.RowStyle;
	}

	private void set_64_DataGrid_RowStyle(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.RowStyle = (Style)Value;
	}

	private object get_65_DataGrid_Columns(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.Columns;
	}

	private object get_66_DataGridColumn_ActualWidth(object instance)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		return dataGridColumn.ActualWidth;
	}

	private object get_67_DataGridColumn_CanUserReorder(object instance)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		return dataGridColumn.CanUserReorder;
	}

	private void set_67_DataGridColumn_CanUserReorder(object instance, object Value)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		dataGridColumn.CanUserReorder = (bool)Value;
	}

	private object get_68_DataGridColumn_CanUserResize(object instance)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		return dataGridColumn.CanUserResize;
	}

	private void set_68_DataGridColumn_CanUserResize(object instance, object Value)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		dataGridColumn.CanUserResize = (bool)Value;
	}

	private object get_69_DataGridColumn_CanUserSort(object instance)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		return dataGridColumn.CanUserSort;
	}

	private void set_69_DataGridColumn_CanUserSort(object instance, object Value)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		dataGridColumn.CanUserSort = (bool)Value;
	}

	private object get_70_DataGridColumn_CellStyle(object instance)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		return dataGridColumn.CellStyle;
	}

	private void set_70_DataGridColumn_CellStyle(object instance, object Value)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		dataGridColumn.CellStyle = (Style)Value;
	}

	private object get_71_DataGridColumn_ClipboardContentBinding(object instance)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		return dataGridColumn.ClipboardContentBinding;
	}

	private void set_71_DataGridColumn_ClipboardContentBinding(object instance, object Value)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		dataGridColumn.ClipboardContentBinding = (Binding)Value;
	}

	private object get_72_DataGridColumn_DisplayIndex(object instance)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		return dataGridColumn.DisplayIndex;
	}

	private void set_72_DataGridColumn_DisplayIndex(object instance, object Value)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		dataGridColumn.DisplayIndex = (int)Value;
	}

	private object get_73_DataGridColumn_DragIndicatorStyle(object instance)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		return dataGridColumn.DragIndicatorStyle;
	}

	private void set_73_DataGridColumn_DragIndicatorStyle(object instance, object Value)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		dataGridColumn.DragIndicatorStyle = (Style)Value;
	}

	private object get_74_DataGridColumn_HeaderStyle(object instance)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		return dataGridColumn.HeaderStyle;
	}

	private void set_74_DataGridColumn_HeaderStyle(object instance, object Value)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		dataGridColumn.HeaderStyle = (Style)Value;
	}

	private object get_75_DataGridColumn_Header(object instance)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		return dataGridColumn.Header;
	}

	private void set_75_DataGridColumn_Header(object instance, object Value)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		dataGridColumn.Header = Value;
	}

	private object get_76_DataGridColumn_IsAutoGenerated(object instance)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		return dataGridColumn.IsAutoGenerated;
	}

	private object get_77_DataGridColumn_IsFrozen(object instance)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		return dataGridColumn.IsFrozen;
	}

	private object get_78_DataGridColumn_IsReadOnly(object instance)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		return dataGridColumn.IsReadOnly;
	}

	private void set_78_DataGridColumn_IsReadOnly(object instance, object Value)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		dataGridColumn.IsReadOnly = (bool)Value;
	}

	private object get_79_DataGridColumn_MaxWidth(object instance)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		return dataGridColumn.MaxWidth;
	}

	private void set_79_DataGridColumn_MaxWidth(object instance, object Value)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		dataGridColumn.MaxWidth = (double)Value;
	}

	private object get_80_DataGridColumn_MinWidth(object instance)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		return dataGridColumn.MinWidth;
	}

	private void set_80_DataGridColumn_MinWidth(object instance, object Value)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		dataGridColumn.MinWidth = (double)Value;
	}

	private object get_81_DataGridColumn_SortDirection(object instance)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		return dataGridColumn.SortDirection;
	}

	private void set_81_DataGridColumn_SortDirection(object instance, object Value)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		dataGridColumn.SortDirection = (DataGridSortDirection?)Value;
	}

	private object get_82_DataGridColumn_Tag(object instance)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		return dataGridColumn.Tag;
	}

	private void set_82_DataGridColumn_Tag(object instance, object Value)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		dataGridColumn.Tag = Value;
	}

	private object get_83_DataGridColumn_Visibility(object instance)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		return dataGridColumn.Visibility;
	}

	private void set_83_DataGridColumn_Visibility(object instance, object Value)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		dataGridColumn.Visibility = (Visibility)Value;
	}

	private object get_84_DataGridColumn_Width(object instance)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		return dataGridColumn.Width;
	}

	private void set_84_DataGridColumn_Width(object instance, object Value)
	{
		DataGridColumn dataGridColumn = (DataGridColumn)instance;
		dataGridColumn.Width = (DataGridLength)Value;
	}

	private object get_85_DataGrid_RowDetailsTemplate(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.RowDetailsTemplate;
	}

	private void set_85_DataGrid_RowDetailsTemplate(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.RowDetailsTemplate = (DataTemplate)Value;
	}

	private object get_86_DataGrid_AlternatingRowBackground(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.AlternatingRowBackground;
	}

	private void set_86_DataGrid_AlternatingRowBackground(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.AlternatingRowBackground = (Brush)Value;
	}

	private object get_87_DataGrid_AlternatingRowForeground(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.AlternatingRowForeground;
	}

	private void set_87_DataGrid_AlternatingRowForeground(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.AlternatingRowForeground = (Brush)Value;
	}

	private object get_88_DataGrid_AreRowDetailsFrozen(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.AreRowDetailsFrozen;
	}

	private void set_88_DataGrid_AreRowDetailsFrozen(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.AreRowDetailsFrozen = (bool)Value;
	}

	private object get_89_DataGrid_AreRowGroupHeadersFrozen(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.AreRowGroupHeadersFrozen;
	}

	private void set_89_DataGrid_AreRowGroupHeadersFrozen(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.AreRowGroupHeadersFrozen = (bool)Value;
	}

	private object get_90_DataGrid_CanUserResizeColumns(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.CanUserResizeColumns;
	}

	private void set_90_DataGrid_CanUserResizeColumns(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.CanUserResizeColumns = (bool)Value;
	}

	private object get_91_DataGrid_CellStyle(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.CellStyle;
	}

	private void set_91_DataGrid_CellStyle(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.CellStyle = (Style)Value;
	}

	private object get_92_DataGrid_ClipboardCopyMode(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.ClipboardCopyMode;
	}

	private void set_92_DataGrid_ClipboardCopyMode(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.ClipboardCopyMode = (DataGridClipboardCopyMode)Value;
	}

	private object get_93_DataGrid_ColumnHeaderHeight(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.ColumnHeaderHeight;
	}

	private void set_93_DataGrid_ColumnHeaderHeight(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.ColumnHeaderHeight = (double)Value;
	}

	private object get_94_DataGrid_ColumnWidth(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.ColumnWidth;
	}

	private void set_94_DataGrid_ColumnWidth(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.ColumnWidth = (DataGridLength)Value;
	}

	private object get_95_DataGrid_DataFetchSize(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.DataFetchSize;
	}

	private void set_95_DataGrid_DataFetchSize(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.DataFetchSize = (double)Value;
	}

	private object get_96_DataGrid_DragIndicatorStyle(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.DragIndicatorStyle;
	}

	private void set_96_DataGrid_DragIndicatorStyle(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.DragIndicatorStyle = (Style)Value;
	}

	private object get_97_DataGrid_DropLocationIndicatorStyle(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.DropLocationIndicatorStyle;
	}

	private void set_97_DataGrid_DropLocationIndicatorStyle(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.DropLocationIndicatorStyle = (Style)Value;
	}

	private object get_98_DataGrid_FrozenColumnCount(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.FrozenColumnCount;
	}

	private void set_98_DataGrid_FrozenColumnCount(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.FrozenColumnCount = (int)Value;
	}

	private object get_99_DataGrid_HorizontalGridLinesBrush(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.HorizontalGridLinesBrush;
	}

	private void set_99_DataGrid_HorizontalGridLinesBrush(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.HorizontalGridLinesBrush = (Brush)Value;
	}

	private object get_100_DataGrid_HorizontalScrollBarVisibility(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.HorizontalScrollBarVisibility;
	}

	private void set_100_DataGrid_HorizontalScrollBarVisibility(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.HorizontalScrollBarVisibility = (ScrollBarVisibility)Value;
	}

	private object get_101_DataGrid_IsReadOnly(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.IsReadOnly;
	}

	private void set_101_DataGrid_IsReadOnly(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.IsReadOnly = (bool)Value;
	}

	private object get_102_DataGrid_IsValid(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.IsValid;
	}

	private object get_103_DataGrid_IncrementalLoadingThreshold(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.IncrementalLoadingThreshold;
	}

	private void set_103_DataGrid_IncrementalLoadingThreshold(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.IncrementalLoadingThreshold = (double)Value;
	}

	private object get_104_DataGrid_IncrementalLoadingTrigger(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.IncrementalLoadingTrigger;
	}

	private void set_104_DataGrid_IncrementalLoadingTrigger(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.IncrementalLoadingTrigger = (IncrementalLoadingTrigger)Value;
	}

	private object get_105_DataGrid_MaxColumnWidth(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.MaxColumnWidth;
	}

	private void set_105_DataGrid_MaxColumnWidth(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.MaxColumnWidth = (double)Value;
	}

	private object get_106_DataGrid_MinColumnWidth(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.MinColumnWidth;
	}

	private void set_106_DataGrid_MinColumnWidth(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.MinColumnWidth = (double)Value;
	}

	private object get_107_DataGrid_RowBackground(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.RowBackground;
	}

	private void set_107_DataGrid_RowBackground(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.RowBackground = (Brush)Value;
	}

	private object get_108_DataGrid_RowForeground(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.RowForeground;
	}

	private void set_108_DataGrid_RowForeground(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.RowForeground = (Brush)Value;
	}

	private object get_109_DataGrid_RowHeaderWidth(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.RowHeaderWidth;
	}

	private void set_109_DataGrid_RowHeaderWidth(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.RowHeaderWidth = (double)Value;
	}

	private object get_110_DataGrid_RowHeaderStyle(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.RowHeaderStyle;
	}

	private void set_110_DataGrid_RowHeaderStyle(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.RowHeaderStyle = (Style)Value;
	}

	private object get_111_DataGrid_SelectedIndex(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.SelectedIndex;
	}

	private void set_111_DataGrid_SelectedIndex(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.SelectedIndex = (int)Value;
	}

	private object get_112_DataGrid_SelectedItem(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.SelectedItem;
	}

	private void set_112_DataGrid_SelectedItem(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.SelectedItem = Value;
	}

	private object get_113_DataGrid_VerticalGridLinesBrush(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.VerticalGridLinesBrush;
	}

	private void set_113_DataGrid_VerticalGridLinesBrush(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.VerticalGridLinesBrush = (Brush)Value;
	}

	private object get_114_DataGrid_VerticalScrollBarVisibility(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.VerticalScrollBarVisibility;
	}

	private void set_114_DataGrid_VerticalScrollBarVisibility(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.VerticalScrollBarVisibility = (ScrollBarVisibility)Value;
	}

	private object get_115_DataGrid_CurrentColumn(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.CurrentColumn;
	}

	private void set_115_DataGrid_CurrentColumn(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.CurrentColumn = (DataGridColumn)Value;
	}

	private object get_116_DataGrid_RowGroupHeaderPropertyNameAlternative(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.RowGroupHeaderPropertyNameAlternative;
	}

	private void set_116_DataGrid_RowGroupHeaderPropertyNameAlternative(object instance, object Value)
	{
		DataGrid dataGrid = (DataGrid)instance;
		dataGrid.RowGroupHeaderPropertyNameAlternative = (string)Value;
	}

	private object get_117_DataGrid_RowGroupHeaderStyles(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.RowGroupHeaderStyles;
	}

	private object get_118_DataGrid_SelectedItems(object instance)
	{
		DataGrid dataGrid = (DataGrid)instance;
		return dataGrid.SelectedItems;
	}

	private object get_119_DataGridColumnHeader_SeparatorBrush(object instance)
	{
		DataGridColumnHeader dataGridColumnHeader = (DataGridColumnHeader)instance;
		return dataGridColumnHeader.SeparatorBrush;
	}

	private void set_119_DataGridColumnHeader_SeparatorBrush(object instance, object Value)
	{
		DataGridColumnHeader dataGridColumnHeader = (DataGridColumnHeader)instance;
		dataGridColumnHeader.SeparatorBrush = (Brush)Value;
	}

	private object get_120_DataGridColumnHeader_SeparatorVisibility(object instance)
	{
		DataGridColumnHeader dataGridColumnHeader = (DataGridColumnHeader)instance;
		return dataGridColumnHeader.SeparatorVisibility;
	}

	private void set_120_DataGridColumnHeader_SeparatorVisibility(object instance, object Value)
	{
		DataGridColumnHeader dataGridColumnHeader = (DataGridColumnHeader)instance;
		dataGridColumnHeader.SeparatorVisibility = (Visibility)Value;
	}

	private object get_121_DataGridRow_DetailsTemplate(object instance)
	{
		DataGridRow dataGridRow = (DataGridRow)instance;
		return dataGridRow.DetailsTemplate;
	}

	private void set_121_DataGridRow_DetailsTemplate(object instance, object Value)
	{
		DataGridRow dataGridRow = (DataGridRow)instance;
		dataGridRow.DetailsTemplate = (DataTemplate)Value;
	}

	private object get_122_DataGridRow_DetailsVisibility(object instance)
	{
		DataGridRow dataGridRow = (DataGridRow)instance;
		return dataGridRow.DetailsVisibility;
	}

	private void set_122_DataGridRow_DetailsVisibility(object instance, object Value)
	{
		DataGridRow dataGridRow = (DataGridRow)instance;
		dataGridRow.DetailsVisibility = (Visibility)Value;
	}

	private object get_123_DataGridRow_Header(object instance)
	{
		DataGridRow dataGridRow = (DataGridRow)instance;
		return dataGridRow.Header;
	}

	private void set_123_DataGridRow_Header(object instance, object Value)
	{
		DataGridRow dataGridRow = (DataGridRow)instance;
		dataGridRow.Header = Value;
	}

	private object get_124_DataGridRow_HeaderStyle(object instance)
	{
		DataGridRow dataGridRow = (DataGridRow)instance;
		return dataGridRow.HeaderStyle;
	}

	private void set_124_DataGridRow_HeaderStyle(object instance, object Value)
	{
		DataGridRow dataGridRow = (DataGridRow)instance;
		dataGridRow.HeaderStyle = (Style)Value;
	}

	private object get_125_DataGridRow_IsValid(object instance)
	{
		DataGridRow dataGridRow = (DataGridRow)instance;
		return dataGridRow.IsValid;
	}

	private object get_126_DataGridTemplateColumn_CellTemplate(object instance)
	{
		DataGridTemplateColumn dataGridTemplateColumn = (DataGridTemplateColumn)instance;
		return dataGridTemplateColumn.CellTemplate;
	}

	private void set_126_DataGridTemplateColumn_CellTemplate(object instance, object Value)
	{
		DataGridTemplateColumn dataGridTemplateColumn = (DataGridTemplateColumn)instance;
		dataGridTemplateColumn.CellTemplate = (DataTemplate)Value;
	}

	private object get_127_DataGridTemplateColumn_CellEditingTemplate(object instance)
	{
		DataGridTemplateColumn dataGridTemplateColumn = (DataGridTemplateColumn)instance;
		return dataGridTemplateColumn.CellEditingTemplate;
	}

	private void set_127_DataGridTemplateColumn_CellEditingTemplate(object instance, object Value)
	{
		DataGridTemplateColumn dataGridTemplateColumn = (DataGridTemplateColumn)instance;
		dataGridTemplateColumn.CellEditingTemplate = (DataTemplate)Value;
	}

	private object get_128_DataGridBoundColumn_Binding(object instance)
	{
		DataGridBoundColumn dataGridBoundColumn = (DataGridBoundColumn)instance;
		return dataGridBoundColumn.Binding;
	}

	private void set_128_DataGridBoundColumn_Binding(object instance, object Value)
	{
		DataGridBoundColumn dataGridBoundColumn = (DataGridBoundColumn)instance;
		dataGridBoundColumn.Binding = (Binding)Value;
	}

	private object get_129_DataGridBoundColumn_ElementStyle(object instance)
	{
		DataGridBoundColumn dataGridBoundColumn = (DataGridBoundColumn)instance;
		return dataGridBoundColumn.ElementStyle;
	}

	private void set_129_DataGridBoundColumn_ElementStyle(object instance, object Value)
	{
		DataGridBoundColumn dataGridBoundColumn = (DataGridBoundColumn)instance;
		dataGridBoundColumn.ElementStyle = (Style)Value;
	}

	private object get_130_DataGridTextColumn_FontFamily(object instance)
	{
		DataGridTextColumn dataGridTextColumn = (DataGridTextColumn)instance;
		return dataGridTextColumn.FontFamily;
	}

	private void set_130_DataGridTextColumn_FontFamily(object instance, object Value)
	{
		DataGridTextColumn dataGridTextColumn = (DataGridTextColumn)instance;
		dataGridTextColumn.FontFamily = (FontFamily)Value;
	}

	private object get_131_DataGridTextColumn_FontSize(object instance)
	{
		DataGridTextColumn dataGridTextColumn = (DataGridTextColumn)instance;
		return dataGridTextColumn.FontSize;
	}

	private void set_131_DataGridTextColumn_FontSize(object instance, object Value)
	{
		DataGridTextColumn dataGridTextColumn = (DataGridTextColumn)instance;
		dataGridTextColumn.FontSize = (double)Value;
	}

	private object get_132_DataGridTextColumn_FontStyle(object instance)
	{
		DataGridTextColumn dataGridTextColumn = (DataGridTextColumn)instance;
		return dataGridTextColumn.FontStyle;
	}

	private void set_132_DataGridTextColumn_FontStyle(object instance, object Value)
	{
		DataGridTextColumn dataGridTextColumn = (DataGridTextColumn)instance;
		dataGridTextColumn.FontStyle = (FontStyle)Value;
	}

	private object get_133_DataGridTextColumn_FontWeight(object instance)
	{
		DataGridTextColumn dataGridTextColumn = (DataGridTextColumn)instance;
		return dataGridTextColumn.FontWeight;
	}

	private void set_133_DataGridTextColumn_FontWeight(object instance, object Value)
	{
		DataGridTextColumn dataGridTextColumn = (DataGridTextColumn)instance;
		dataGridTextColumn.FontWeight = (FontWeight)Value;
	}

	private object get_134_DataGridTextColumn_Foreground(object instance)
	{
		DataGridTextColumn dataGridTextColumn = (DataGridTextColumn)instance;
		return dataGridTextColumn.Foreground;
	}

	private void set_134_DataGridTextColumn_Foreground(object instance, object Value)
	{
		DataGridTextColumn dataGridTextColumn = (DataGridTextColumn)instance;
		dataGridTextColumn.Foreground = (Brush)Value;
	}

	private object get_135_DataGridBoundColumn_ClipboardContentBinding(object instance)
	{
		DataGridBoundColumn dataGridBoundColumn = (DataGridBoundColumn)instance;
		return dataGridBoundColumn.ClipboardContentBinding;
	}

	private void set_135_DataGridBoundColumn_ClipboardContentBinding(object instance, object Value)
	{
		DataGridBoundColumn dataGridBoundColumn = (DataGridBoundColumn)instance;
		dataGridBoundColumn.ClipboardContentBinding = (Binding)Value;
	}

	private object get_136_DataGridBoundColumn_EditingElementStyle(object instance)
	{
		DataGridBoundColumn dataGridBoundColumn = (DataGridBoundColumn)instance;
		return dataGridBoundColumn.EditingElementStyle;
	}

	private void set_136_DataGridBoundColumn_EditingElementStyle(object instance, object Value)
	{
		DataGridBoundColumn dataGridBoundColumn = (DataGridBoundColumn)instance;
		dataGridBoundColumn.EditingElementStyle = (Style)Value;
	}

	private object get_137_ProgressBar_IsIndeterminate(object instance)
	{
		ProgressBar progressBar = (ProgressBar)instance;
		return progressBar.IsIndeterminate;
	}

	private void set_137_ProgressBar_IsIndeterminate(object instance, object Value)
	{
		ProgressBar progressBar = (ProgressBar)instance;
		progressBar.IsIndeterminate = (bool)Value;
	}

	private object get_138_ProgressBar_ShowError(object instance)
	{
		ProgressBar progressBar = (ProgressBar)instance;
		return progressBar.ShowError;
	}

	private void set_138_ProgressBar_ShowError(object instance, object Value)
	{
		ProgressBar progressBar = (ProgressBar)instance;
		progressBar.ShowError = (bool)Value;
	}

	private object get_139_ProgressBar_ShowPaused(object instance)
	{
		ProgressBar progressBar = (ProgressBar)instance;
		return progressBar.ShowPaused;
	}

	private void set_139_ProgressBar_ShowPaused(object instance, object Value)
	{
		ProgressBar progressBar = (ProgressBar)instance;
		progressBar.ShowPaused = (bool)Value;
	}

	private object get_140_ProgressBar_TemplateSettings(object instance)
	{
		ProgressBar progressBar = (ProgressBar)instance;
		return progressBar.TemplateSettings;
	}

	private object get_141_HistoryPage_HistoryTasks(object instance)
	{
		HistoryPage historyPage = (HistoryPage)instance;
		return historyPage.HistoryTasks;
	}

	private object get_142_ProcessingTask_Cts(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.Cts;
	}

	private void set_142_ProcessingTask_Cts(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.Cts = (CancellationTokenSource)Value;
	}

	private object get_143_ProcessingTask_FinishedAtDisplay(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.FinishedAtDisplay;
	}

	private object get_144_ProcessingTask_IsNotRunning(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.IsNotRunning;
	}

	private object get_145_ProcessingTask_GroupId(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.GroupId;
	}

	private void set_145_ProcessingTask_GroupId(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.GroupId = (string)Value;
	}

	private object get_146_ProcessingTask_InputFiles(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.InputFiles;
	}

	private void set_146_ProcessingTask_InputFiles(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.InputFiles = (List<string>)Value;
	}

	private object get_147_ProcessingTask_FilesList(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.FilesList;
	}

	private void set_147_ProcessingTask_FilesList(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.FilesList = (List<string>)Value;
	}

	private object get_148_ProcessingTask_DetailsVisibility(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.DetailsVisibility;
	}

	private object get_149_ProcessingTask_OperationDisplay(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.OperationDisplay;
	}

	private object get_150_ProcessingTask_StatusColor(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.StatusColor;
	}

	private object get_151_ProcessingTask_Id(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.Id;
	}

	private void set_151_ProcessingTask_Id(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.Id = (string)Value;
	}

	private object get_152_ProcessingTask_GameIcon(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.GameIcon;
	}

	private void set_152_ProcessingTask_GameIcon(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.GameIcon = (BitmapImage)Value;
	}

	private object get_153_ProcessingTask_GameName(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.GameName;
	}

	private void set_153_ProcessingTask_GameName(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.GameName = (string)Value;
	}

	private object get_154_ProcessingTask_Operation(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.Operation;
	}

	private void set_154_ProcessingTask_Operation(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.Operation = (string)Value;
	}

	private object get_155_ProcessingTask_SourceFormat(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.SourceFormat;
	}

	private void set_155_ProcessingTask_SourceFormat(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.SourceFormat = (string)Value;
	}

	private object get_156_ProcessingTask_TargetFormat(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.TargetFormat;
	}

	private void set_156_ProcessingTask_TargetFormat(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.TargetFormat = (string)Value;
	}

	private object get_157_ProcessingTask_SourceSize(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.SourceSize;
	}

	private void set_157_ProcessingTask_SourceSize(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.SourceSize = (string)Value;
	}

	private object get_158_ProcessingTask_TargetSize(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.TargetSize;
	}

	private void set_158_ProcessingTask_TargetSize(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.TargetSize = (string)Value;
	}

	private object get_159_ProcessingTask_SizeDifference(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.SizeDifference;
	}

	private void set_159_ProcessingTask_SizeDifference(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.SizeDifference = (string)Value;
	}

	private object get_160_ProcessingTask_CompressionLevel(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.CompressionLevel;
	}

	private void set_160_ProcessingTask_CompressionLevel(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.CompressionLevel = (string)Value;
	}

	private object get_161_ProcessingTask_FilesCount(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.FilesCount;
	}

	private void set_161_ProcessingTask_FilesCount(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.FilesCount = (string)Value;
	}

	private object get_162_ProcessingTask_Status(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.Status;
	}

	private void set_162_ProcessingTask_Status(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.Status = (string)Value;
	}

	private object get_163_ProcessingTask_Progress(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.Progress;
	}

	private void set_163_ProcessingTask_Progress(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.Progress = (double)Value;
	}

	private object get_164_ProcessingTask_IsRunning(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.IsRunning;
	}

	private void set_164_ProcessingTask_IsRunning(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.IsRunning = (bool)Value;
	}

	private object get_165_ProcessingTask_FinishedAt(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.FinishedAt;
	}

	private void set_165_ProcessingTask_FinishedAt(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.FinishedAt = (DateTime)Value;
	}

	private object get_166_ProcessingTask_SourceSizeBytes(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.SourceSizeBytes;
	}

	private void set_166_ProcessingTask_SourceSizeBytes(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.SourceSizeBytes = (long)Value;
	}

	private object get_167_ProcessingTask_HasRomFs(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.HasRomFs;
	}

	private void set_167_ProcessingTask_HasRomFs(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.HasRomFs = (string)Value;
	}

	private object get_168_ProcessingTask_HasExeFs(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.HasExeFs;
	}

	private void set_168_ProcessingTask_HasExeFs(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.HasExeFs = (string)Value;
	}

	private object get_169_ProcessingTask_Speed(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.Speed;
	}

	private void set_169_ProcessingTask_Speed(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.Speed = (string)Value;
	}

	private object get_170_ProcessingTask_IsExpanded(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.IsExpanded;
	}

	private void set_170_ProcessingTask_IsExpanded(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.IsExpanded = (bool)Value;
	}

	private object get_171_ProcessingTask_VerifyType(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.VerifyType;
	}

	private void set_171_ProcessingTask_VerifyType(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.VerifyType = (string)Value;
	}

	private object get_172_ProcessingTask_VerifyStructure(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.VerifyStructure;
	}

	private void set_172_ProcessingTask_VerifyStructure(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.VerifyStructure = (string)Value;
	}

	private object get_173_ProcessingTask_VerifyTitleId(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.VerifyTitleId;
	}

	private void set_173_ProcessingTask_VerifyTitleId(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.VerifyTitleId = (string)Value;
	}

	private object get_174_ProcessingTask_VerifyVersion(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.VerifyVersion;
	}

	private void set_174_ProcessingTask_VerifyVersion(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.VerifyVersion = (string)Value;
	}

	private object get_175_ProcessingTask_VerifyMergedStatus(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.VerifyMergedStatus;
	}

	private void set_175_ProcessingTask_VerifyMergedStatus(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.VerifyMergedStatus = (string)Value;
	}

	private object get_176_ProcessingTask_InputFolders(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.InputFolders;
	}

	private void set_176_ProcessingTask_InputFolders(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.InputFolders = (string)Value;
	}

	private object get_177_ProcessingTask_OutputFolder(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.OutputFolder;
	}

	private void set_177_ProcessingTask_OutputFolder(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.OutputFolder = (string)Value;
	}

	private object get_178_ProcessingTask_OutputFileName(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.OutputFileName;
	}

	private void set_178_ProcessingTask_OutputFileName(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.OutputFileName = (string)Value;
	}

	private object get_179_ProcessingTask_LogDetails(object instance)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		return processingTask.LogDetails;
	}

	private void set_179_ProcessingTask_LogDetails(object instance, object Value)
	{
		ProcessingTask processingTask = (ProcessingTask)instance;
		processingTask.LogDetails = (string)Value;
	}

	private object get_180_NumberBox_Value(object instance)
	{
		NumberBox numberBox = (NumberBox)instance;
		return numberBox.Value;
	}

	private void set_180_NumberBox_Value(object instance, object Value)
	{
		NumberBox numberBox = (NumberBox)instance;
		numberBox.Value = (double)Value;
	}

	private object get_181_NumberBox_Minimum(object instance)
	{
		NumberBox numberBox = (NumberBox)instance;
		return numberBox.Minimum;
	}

	private void set_181_NumberBox_Minimum(object instance, object Value)
	{
		NumberBox numberBox = (NumberBox)instance;
		numberBox.Minimum = (double)Value;
	}

	private object get_182_NumberBox_Maximum(object instance)
	{
		NumberBox numberBox = (NumberBox)instance;
		return numberBox.Maximum;
	}

	private void set_182_NumberBox_Maximum(object instance, object Value)
	{
		NumberBox numberBox = (NumberBox)instance;
		numberBox.Maximum = (double)Value;
	}

	private object get_183_NumberBox_SpinButtonPlacementMode(object instance)
	{
		NumberBox numberBox = (NumberBox)instance;
		return numberBox.SpinButtonPlacementMode;
	}

	private void set_183_NumberBox_SpinButtonPlacementMode(object instance, object Value)
	{
		NumberBox numberBox = (NumberBox)instance;
		numberBox.SpinButtonPlacementMode = (NumberBoxSpinButtonPlacementMode)Value;
	}

	private object get_184_NumberBox_AcceptsExpression(object instance)
	{
		NumberBox numberBox = (NumberBox)instance;
		return numberBox.AcceptsExpression;
	}

	private void set_184_NumberBox_AcceptsExpression(object instance, object Value)
	{
		NumberBox numberBox = (NumberBox)instance;
		numberBox.AcceptsExpression = (bool)Value;
	}

	private object get_185_NumberBox_Description(object instance)
	{
		NumberBox numberBox = (NumberBox)instance;
		return numberBox.Description;
	}

	private void set_185_NumberBox_Description(object instance, object Value)
	{
		NumberBox numberBox = (NumberBox)instance;
		numberBox.Description = Value;
	}

	private object get_186_NumberBox_Header(object instance)
	{
		NumberBox numberBox = (NumberBox)instance;
		return numberBox.Header;
	}

	private void set_186_NumberBox_Header(object instance, object Value)
	{
		NumberBox numberBox = (NumberBox)instance;
		numberBox.Header = Value;
	}

	private object get_187_NumberBox_HeaderTemplate(object instance)
	{
		NumberBox numberBox = (NumberBox)instance;
		return numberBox.HeaderTemplate;
	}

	private void set_187_NumberBox_HeaderTemplate(object instance, object Value)
	{
		NumberBox numberBox = (NumberBox)instance;
		numberBox.HeaderTemplate = (DataTemplate)Value;
	}

	private object get_188_NumberBox_IsWrapEnabled(object instance)
	{
		NumberBox numberBox = (NumberBox)instance;
		return numberBox.IsWrapEnabled;
	}

	private void set_188_NumberBox_IsWrapEnabled(object instance, object Value)
	{
		NumberBox numberBox = (NumberBox)instance;
		numberBox.IsWrapEnabled = (bool)Value;
	}

	private object get_189_NumberBox_LargeChange(object instance)
	{
		NumberBox numberBox = (NumberBox)instance;
		return numberBox.LargeChange;
	}

	private void set_189_NumberBox_LargeChange(object instance, object Value)
	{
		NumberBox numberBox = (NumberBox)instance;
		numberBox.LargeChange = (double)Value;
	}

	private object get_190_NumberBox_NumberFormatter(object instance)
	{
		NumberBox numberBox = (NumberBox)instance;
		return numberBox.NumberFormatter;
	}

	private void set_190_NumberBox_NumberFormatter(object instance, object Value)
	{
		NumberBox numberBox = (NumberBox)instance;
		numberBox.NumberFormatter = (INumberFormatter2)Value;
	}

	private object get_191_NumberBox_PlaceholderText(object instance)
	{
		NumberBox numberBox = (NumberBox)instance;
		return numberBox.PlaceholderText;
	}

	private void set_191_NumberBox_PlaceholderText(object instance, object Value)
	{
		NumberBox numberBox = (NumberBox)instance;
		numberBox.PlaceholderText = (string)Value;
	}

	private object get_192_NumberBox_PreventKeyboardDisplayOnProgrammaticFocus(object instance)
	{
		NumberBox numberBox = (NumberBox)instance;
		return numberBox.PreventKeyboardDisplayOnProgrammaticFocus;
	}

	private void set_192_NumberBox_PreventKeyboardDisplayOnProgrammaticFocus(object instance, object Value)
	{
		NumberBox numberBox = (NumberBox)instance;
		numberBox.PreventKeyboardDisplayOnProgrammaticFocus = (bool)Value;
	}

	private object get_193_NumberBox_SelectionFlyout(object instance)
	{
		NumberBox numberBox = (NumberBox)instance;
		return numberBox.SelectionFlyout;
	}

	private void set_193_NumberBox_SelectionFlyout(object instance, object Value)
	{
		NumberBox numberBox = (NumberBox)instance;
		numberBox.SelectionFlyout = (FlyoutBase)Value;
	}

	private object get_194_NumberBox_SelectionHighlightColor(object instance)
	{
		NumberBox numberBox = (NumberBox)instance;
		return numberBox.SelectionHighlightColor;
	}

	private void set_194_NumberBox_SelectionHighlightColor(object instance, object Value)
	{
		NumberBox numberBox = (NumberBox)instance;
		numberBox.SelectionHighlightColor = (SolidColorBrush)Value;
	}

	private object get_195_NumberBox_SmallChange(object instance)
	{
		NumberBox numberBox = (NumberBox)instance;
		return numberBox.SmallChange;
	}

	private void set_195_NumberBox_SmallChange(object instance, object Value)
	{
		NumberBox numberBox = (NumberBox)instance;
		numberBox.SmallChange = (double)Value;
	}

	private object get_196_NumberBox_Text(object instance)
	{
		NumberBox numberBox = (NumberBox)instance;
		return numberBox.Text;
	}

	private void set_196_NumberBox_Text(object instance, object Value)
	{
		NumberBox numberBox = (NumberBox)instance;
		numberBox.Text = (string)Value;
	}

	private object get_197_NumberBox_TextReadingOrder(object instance)
	{
		NumberBox numberBox = (NumberBox)instance;
		return numberBox.TextReadingOrder;
	}

	private void set_197_NumberBox_TextReadingOrder(object instance, object Value)
	{
		NumberBox numberBox = (NumberBox)instance;
		numberBox.TextReadingOrder = (TextReadingOrder)Value;
	}

	private object get_198_NumberBox_ValidationMode(object instance)
	{
		NumberBox numberBox = (NumberBox)instance;
		return numberBox.ValidationMode;
	}

	private void set_198_NumberBox_ValidationMode(object instance, object Value)
	{
		NumberBox numberBox = (NumberBox)instance;
		numberBox.ValidationMode = (NumberBoxValidationMode)Value;
	}

	private object get_199_SettingsPage_MaxCores(object instance)
	{
		SettingsPage settingsPage = (SettingsPage)instance;
		return settingsPage.MaxCores;
	}

	private object get_200_SettingsPage_Settings(object instance)
	{
		SettingsPage settingsPage = (SettingsPage)instance;
		return settingsPage.Settings;
	}

	private object get_201_SettingsPage_KeysSelectedVisibility(object instance)
	{
		SettingsPage settingsPage = (SettingsPage)instance;
		return settingsPage.KeysSelectedVisibility;
	}

	private object get_202_TasksPage_ViewModel(object instance)
	{
		TasksPage tasksPage = (TasksPage)instance;
		return tasksPage.ViewModel;
	}

	private object get_203_TasksPage_AppLogs(object instance)
	{
		TasksPage tasksPage = (TasksPage)instance;
		return tasksPage.AppLogs;
	}

	private object get_204_LogMessage_Timestamp(object instance)
	{
		LogMessage logMessage = (LogMessage)instance;
		return logMessage.Timestamp;
	}

	private void set_204_LogMessage_Timestamp(object instance, object Value)
	{
		LogMessage logMessage = (LogMessage)instance;
		logMessage.Timestamp = (DateTime)Value;
	}

	private object get_205_LogMessage_Message(object instance)
	{
		LogMessage logMessage = (LogMessage)instance;
		return logMessage.Message;
	}

	private void set_205_LogMessage_Message(object instance, object Value)
	{
		LogMessage logMessage = (LogMessage)instance;
		logMessage.Message = (string)Value;
	}

	private object get_206_LogMessage_Level(object instance)
	{
		LogMessage logMessage = (LogMessage)instance;
		return logMessage.Level;
	}

	private void set_206_LogMessage_Level(object instance, object Value)
	{
		LogMessage logMessage = (LogMessage)instance;
		logMessage.Level = (LogLevel)Value;
	}

	private object get_207_LogMessage_ColorBrush(object instance)
	{
		LogMessage logMessage = (LogMessage)instance;
		return logMessage.ColorBrush;
	}

	private object get_208_LogMessage_FormattedTime(object instance)
	{
		LogMessage logMessage = (LogMessage)instance;
		return logMessage.FormattedTime;
	}

	private object get_209_TreeViewNode_Children(object instance)
	{
		TreeViewNode treeViewNode = (TreeViewNode)instance;
		return treeViewNode.Children;
	}

	private object get_210_TreeViewNode_Content(object instance)
	{
		TreeViewNode treeViewNode = (TreeViewNode)instance;
		return treeViewNode.Content;
	}

	private void set_210_TreeViewNode_Content(object instance, object Value)
	{
		TreeViewNode treeViewNode = (TreeViewNode)instance;
		treeViewNode.Content = Value;
	}

	private object get_211_TreeViewNode_Depth(object instance)
	{
		TreeViewNode treeViewNode = (TreeViewNode)instance;
		return treeViewNode.Depth;
	}

	private object get_212_TreeViewNode_HasChildren(object instance)
	{
		TreeViewNode treeViewNode = (TreeViewNode)instance;
		return treeViewNode.HasChildren;
	}

	private object get_213_TreeViewNode_HasUnrealizedChildren(object instance)
	{
		TreeViewNode treeViewNode = (TreeViewNode)instance;
		return treeViewNode.HasUnrealizedChildren;
	}

	private void set_213_TreeViewNode_HasUnrealizedChildren(object instance, object Value)
	{
		TreeViewNode treeViewNode = (TreeViewNode)instance;
		treeViewNode.HasUnrealizedChildren = (bool)Value;
	}

	private object get_214_TreeViewNode_IsExpanded(object instance)
	{
		TreeViewNode treeViewNode = (TreeViewNode)instance;
		return treeViewNode.IsExpanded;
	}

	private void set_214_TreeViewNode_IsExpanded(object instance, object Value)
	{
		TreeViewNode treeViewNode = (TreeViewNode)instance;
		treeViewNode.IsExpanded = (bool)Value;
	}

	private object get_215_TreeViewNode_Parent(object instance)
	{
		TreeViewNode treeViewNode = (TreeViewNode)instance;
		return treeViewNode.Parent;
	}

	private IXamlMember CreateXamlMember(string longMemberName)
	{
		XamlMember xamlMember = null;
		switch (longMemberName)
		{
		case "Microsoft.UI.Xaml.Controls.XamlControlsResources.UseCompactResources":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.XamlControlsResources");
			xamlMember = new XamlMember(this, "UseCompactResources", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_0_XamlControlsResources_UseCompactResources;
			xamlMember.Setter = set_0_XamlControlsResources_UseCompactResources;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.PaneDisplayMode":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "PaneDisplayMode", "Microsoft.UI.Xaml.Controls.NavigationViewPaneDisplayMode");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_1_NavigationView_PaneDisplayMode;
			xamlMember.Setter = set_1_NavigationView_PaneDisplayMode;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.OpenPaneLength":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "OpenPaneLength", "Double");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_2_NavigationView_OpenPaneLength;
			xamlMember.Setter = set_2_NavigationView_OpenPaneLength;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.MenuItems":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "MenuItems", "System.Collections.Generic.IList`1<Object>");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_3_NavigationView_MenuItems;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.FooterMenuItems":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "FooterMenuItems", "System.Collections.Generic.IList`1<Object>");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_4_NavigationView_FooterMenuItems;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.AlwaysShowHeader":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "AlwaysShowHeader", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_5_NavigationView_AlwaysShowHeader;
			xamlMember.Setter = set_5_NavigationView_AlwaysShowHeader;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.AutoSuggestBox":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "AutoSuggestBox", "Microsoft.UI.Xaml.Controls.AutoSuggestBox");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_6_NavigationView_AutoSuggestBox;
			xamlMember.Setter = set_6_NavigationView_AutoSuggestBox;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.CompactModeThresholdWidth":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "CompactModeThresholdWidth", "Double");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_7_NavigationView_CompactModeThresholdWidth;
			xamlMember.Setter = set_7_NavigationView_CompactModeThresholdWidth;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.CompactPaneLength":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "CompactPaneLength", "Double");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_8_NavigationView_CompactPaneLength;
			xamlMember.Setter = set_8_NavigationView_CompactPaneLength;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.ContentOverlay":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "ContentOverlay", "Microsoft.UI.Xaml.UIElement");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_9_NavigationView_ContentOverlay;
			xamlMember.Setter = set_9_NavigationView_ContentOverlay;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.DisplayMode":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "DisplayMode", "Microsoft.UI.Xaml.Controls.NavigationViewDisplayMode");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_10_NavigationView_DisplayMode;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.ExpandedModeThresholdWidth":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "ExpandedModeThresholdWidth", "Double");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_11_NavigationView_ExpandedModeThresholdWidth;
			xamlMember.Setter = set_11_NavigationView_ExpandedModeThresholdWidth;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.FooterMenuItemsSource":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "FooterMenuItemsSource", "Object");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_12_NavigationView_FooterMenuItemsSource;
			xamlMember.Setter = set_12_NavigationView_FooterMenuItemsSource;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.Header":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "Header", "Object");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_13_NavigationView_Header;
			xamlMember.Setter = set_13_NavigationView_Header;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.HeaderTemplate":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "HeaderTemplate", "Microsoft.UI.Xaml.DataTemplate");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_14_NavigationView_HeaderTemplate;
			xamlMember.Setter = set_14_NavigationView_HeaderTemplate;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.IsBackButtonVisible":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "IsBackButtonVisible", "Microsoft.UI.Xaml.Controls.NavigationViewBackButtonVisible");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_15_NavigationView_IsBackButtonVisible;
			xamlMember.Setter = set_15_NavigationView_IsBackButtonVisible;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.IsBackEnabled":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "IsBackEnabled", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_16_NavigationView_IsBackEnabled;
			xamlMember.Setter = set_16_NavigationView_IsBackEnabled;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.IsPaneOpen":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "IsPaneOpen", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_17_NavigationView_IsPaneOpen;
			xamlMember.Setter = set_17_NavigationView_IsPaneOpen;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.IsPaneToggleButtonVisible":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "IsPaneToggleButtonVisible", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_18_NavigationView_IsPaneToggleButtonVisible;
			xamlMember.Setter = set_18_NavigationView_IsPaneToggleButtonVisible;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.IsPaneVisible":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "IsPaneVisible", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_19_NavigationView_IsPaneVisible;
			xamlMember.Setter = set_19_NavigationView_IsPaneVisible;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.IsSettingsVisible":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "IsSettingsVisible", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_20_NavigationView_IsSettingsVisible;
			xamlMember.Setter = set_20_NavigationView_IsSettingsVisible;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.IsTitleBarAutoPaddingEnabled":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "IsTitleBarAutoPaddingEnabled", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_21_NavigationView_IsTitleBarAutoPaddingEnabled;
			xamlMember.Setter = set_21_NavigationView_IsTitleBarAutoPaddingEnabled;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.MenuItemContainerStyle":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "MenuItemContainerStyle", "Microsoft.UI.Xaml.Style");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_22_NavigationView_MenuItemContainerStyle;
			xamlMember.Setter = set_22_NavigationView_MenuItemContainerStyle;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.MenuItemContainerStyleSelector":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "MenuItemContainerStyleSelector", "Microsoft.UI.Xaml.Controls.StyleSelector");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_23_NavigationView_MenuItemContainerStyleSelector;
			xamlMember.Setter = set_23_NavigationView_MenuItemContainerStyleSelector;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.MenuItemTemplate":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "MenuItemTemplate", "Microsoft.UI.Xaml.DataTemplate");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_24_NavigationView_MenuItemTemplate;
			xamlMember.Setter = set_24_NavigationView_MenuItemTemplate;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.MenuItemTemplateSelector":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "MenuItemTemplateSelector", "Microsoft.UI.Xaml.Controls.DataTemplateSelector");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_25_NavigationView_MenuItemTemplateSelector;
			xamlMember.Setter = set_25_NavigationView_MenuItemTemplateSelector;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.MenuItemsSource":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "MenuItemsSource", "Object");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_26_NavigationView_MenuItemsSource;
			xamlMember.Setter = set_26_NavigationView_MenuItemsSource;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.OverflowLabelMode":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "OverflowLabelMode", "Microsoft.UI.Xaml.Controls.NavigationViewOverflowLabelMode");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_27_NavigationView_OverflowLabelMode;
			xamlMember.Setter = set_27_NavigationView_OverflowLabelMode;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.PaneCustomContent":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "PaneCustomContent", "Microsoft.UI.Xaml.UIElement");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_28_NavigationView_PaneCustomContent;
			xamlMember.Setter = set_28_NavigationView_PaneCustomContent;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.PaneFooter":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "PaneFooter", "Microsoft.UI.Xaml.UIElement");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_29_NavigationView_PaneFooter;
			xamlMember.Setter = set_29_NavigationView_PaneFooter;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.PaneHeader":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "PaneHeader", "Microsoft.UI.Xaml.UIElement");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_30_NavigationView_PaneHeader;
			xamlMember.Setter = set_30_NavigationView_PaneHeader;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.PaneTitle":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "PaneTitle", "String");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_31_NavigationView_PaneTitle;
			xamlMember.Setter = set_31_NavigationView_PaneTitle;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.PaneToggleButtonStyle":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "PaneToggleButtonStyle", "Microsoft.UI.Xaml.Style");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_32_NavigationView_PaneToggleButtonStyle;
			xamlMember.Setter = set_32_NavigationView_PaneToggleButtonStyle;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.SelectedItem":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "SelectedItem", "Object");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_33_NavigationView_SelectedItem;
			xamlMember.Setter = set_33_NavigationView_SelectedItem;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.SelectionFollowsFocus":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "SelectionFollowsFocus", "Microsoft.UI.Xaml.Controls.NavigationViewSelectionFollowsFocus");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_34_NavigationView_SelectionFollowsFocus;
			xamlMember.Setter = set_34_NavigationView_SelectionFollowsFocus;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.SettingsItem":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "SettingsItem", "Object");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_35_NavigationView_SettingsItem;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.ShoulderNavigationEnabled":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "ShoulderNavigationEnabled", "Microsoft.UI.Xaml.Controls.NavigationViewShoulderNavigationEnabled");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_36_NavigationView_ShoulderNavigationEnabled;
			xamlMember.Setter = set_36_NavigationView_ShoulderNavigationEnabled;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationView.TemplateSettings":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationView");
			xamlMember = new XamlMember(this, "TemplateSettings", "Microsoft.UI.Xaml.Controls.NavigationViewTemplateSettings");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_37_NavigationView_TemplateSettings;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationViewItem.Icon":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationViewItem");
			xamlMember = new XamlMember(this, "Icon", "Microsoft.UI.Xaml.Controls.IconElement");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_38_NavigationViewItem_Icon;
			xamlMember.Setter = set_38_NavigationViewItem_Icon;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationViewItem.CompactPaneLength":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationViewItem");
			xamlMember = new XamlMember(this, "CompactPaneLength", "Double");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_39_NavigationViewItem_CompactPaneLength;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationViewItem.HasUnrealizedChildren":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationViewItem");
			xamlMember = new XamlMember(this, "HasUnrealizedChildren", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_40_NavigationViewItem_HasUnrealizedChildren;
			xamlMember.Setter = set_40_NavigationViewItem_HasUnrealizedChildren;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationViewItem.InfoBadge":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationViewItem");
			xamlMember = new XamlMember(this, "InfoBadge", "Microsoft.UI.Xaml.Controls.InfoBadge");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_41_NavigationViewItem_InfoBadge;
			xamlMember.Setter = set_41_NavigationViewItem_InfoBadge;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationViewItem.IsChildSelected":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationViewItem");
			xamlMember = new XamlMember(this, "IsChildSelected", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_42_NavigationViewItem_IsChildSelected;
			xamlMember.Setter = set_42_NavigationViewItem_IsChildSelected;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationViewItem.IsExpanded":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationViewItem");
			xamlMember = new XamlMember(this, "IsExpanded", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_43_NavigationViewItem_IsExpanded;
			xamlMember.Setter = set_43_NavigationViewItem_IsExpanded;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationViewItem.MenuItems":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationViewItem");
			xamlMember = new XamlMember(this, "MenuItems", "System.Collections.Generic.IList`1<Object>");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_44_NavigationViewItem_MenuItems;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationViewItem.MenuItemsSource":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationViewItem");
			xamlMember = new XamlMember(this, "MenuItemsSource", "Object");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_45_NavigationViewItem_MenuItemsSource;
			xamlMember.Setter = set_45_NavigationViewItem_MenuItemsSource;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationViewItem.SelectsOnInvoked":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationViewItem");
			xamlMember = new XamlMember(this, "SelectsOnInvoked", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_46_NavigationViewItem_SelectsOnInvoked;
			xamlMember.Setter = set_46_NavigationViewItem_SelectsOnInvoked;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NavigationViewItemBase.IsSelected":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NavigationViewItemBase");
			xamlMember = new XamlMember(this, "IsSelected", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_47_NavigationViewItemBase_IsSelected;
			xamlMember.Setter = set_47_NavigationViewItemBase_IsSelected;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.ProgressRing.IsActive":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ProgressRing");
			xamlMember = new XamlMember(this, "IsActive", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_48_ProgressRing_IsActive;
			xamlMember.Setter = set_48_ProgressRing_IsActive;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.ProgressRing.IsIndeterminate":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ProgressRing");
			xamlMember = new XamlMember(this, "IsIndeterminate", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_49_ProgressRing_IsIndeterminate;
			xamlMember.Setter = set_49_ProgressRing_IsIndeterminate;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.ProgressRing.Maximum":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ProgressRing");
			xamlMember = new XamlMember(this, "Maximum", "Double");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_50_ProgressRing_Maximum;
			xamlMember.Setter = set_50_ProgressRing_Maximum;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.ProgressRing.Minimum":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ProgressRing");
			xamlMember = new XamlMember(this, "Minimum", "Double");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_51_ProgressRing_Minimum;
			xamlMember.Setter = set_51_ProgressRing_Minimum;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.ProgressRing.TemplateSettings":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ProgressRing");
			xamlMember = new XamlMember(this, "TemplateSettings", "Microsoft.UI.Xaml.Controls.ProgressRingTemplateSettings");
			xamlMember.Getter = get_52_ProgressRing_TemplateSettings;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "Microsoft.UI.Xaml.Controls.ProgressRing.Value":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ProgressRing");
			xamlMember = new XamlMember(this, "Value", "Double");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_53_ProgressRing_Value;
			xamlMember.Setter = set_53_ProgressRing_Value;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.ItemsSource":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "ItemsSource", "System.Collections.IEnumerable");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_54_DataGrid_ItemsSource;
			xamlMember.Setter = set_54_DataGrid_ItemsSource;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.AutoGenerateColumns":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "AutoGenerateColumns", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_55_DataGrid_AutoGenerateColumns;
			xamlMember.Setter = set_55_DataGrid_AutoGenerateColumns;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.GridLinesVisibility":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "GridLinesVisibility", "CommunityToolkit.WinUI.UI.Controls.DataGridGridLinesVisibility");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_56_DataGrid_GridLinesVisibility;
			xamlMember.Setter = set_56_DataGrid_GridLinesVisibility;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.HeadersVisibility":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "HeadersVisibility", "CommunityToolkit.WinUI.UI.Controls.DataGridHeadersVisibility");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_57_DataGrid_HeadersVisibility;
			xamlMember.Setter = set_57_DataGrid_HeadersVisibility;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.RowDetailsVisibilityMode":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "RowDetailsVisibilityMode", "CommunityToolkit.WinUI.UI.Controls.DataGridRowDetailsVisibilityMode");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_58_DataGrid_RowDetailsVisibilityMode;
			xamlMember.Setter = set_58_DataGrid_RowDetailsVisibilityMode;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.SelectionMode":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "SelectionMode", "CommunityToolkit.WinUI.UI.Controls.DataGridSelectionMode");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_59_DataGrid_SelectionMode;
			xamlMember.Setter = set_59_DataGrid_SelectionMode;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.CanUserSortColumns":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "CanUserSortColumns", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_60_DataGrid_CanUserSortColumns;
			xamlMember.Setter = set_60_DataGrid_CanUserSortColumns;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.CanUserReorderColumns":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "CanUserReorderColumns", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_61_DataGrid_CanUserReorderColumns;
			xamlMember.Setter = set_61_DataGrid_CanUserReorderColumns;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.RowHeight":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "RowHeight", "Double");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_62_DataGrid_RowHeight;
			xamlMember.Setter = set_62_DataGrid_RowHeight;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.ColumnHeaderStyle":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "ColumnHeaderStyle", "Microsoft.UI.Xaml.Style");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_63_DataGrid_ColumnHeaderStyle;
			xamlMember.Setter = set_63_DataGrid_ColumnHeaderStyle;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.RowStyle":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "RowStyle", "Microsoft.UI.Xaml.Style");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_64_DataGrid_RowStyle;
			xamlMember.Setter = set_64_DataGrid_RowStyle;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.Columns":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "Columns", "System.Collections.ObjectModel.ObservableCollection`1<CommunityToolkit.WinUI.UI.Controls.DataGridColumn>");
			xamlMember.Getter = get_65_DataGrid_Columns;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridColumn.ActualWidth":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridColumn");
			xamlMember = new XamlMember(this, "ActualWidth", "Double");
			xamlMember.Getter = get_66_DataGridColumn_ActualWidth;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridColumn.CanUserReorder":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridColumn");
			xamlMember = new XamlMember(this, "CanUserReorder", "Boolean");
			xamlMember.Getter = get_67_DataGridColumn_CanUserReorder;
			xamlMember.Setter = set_67_DataGridColumn_CanUserReorder;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridColumn.CanUserResize":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridColumn");
			xamlMember = new XamlMember(this, "CanUserResize", "Boolean");
			xamlMember.Getter = get_68_DataGridColumn_CanUserResize;
			xamlMember.Setter = set_68_DataGridColumn_CanUserResize;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridColumn.CanUserSort":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridColumn");
			xamlMember = new XamlMember(this, "CanUserSort", "Boolean");
			xamlMember.Getter = get_69_DataGridColumn_CanUserSort;
			xamlMember.Setter = set_69_DataGridColumn_CanUserSort;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridColumn.CellStyle":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridColumn");
			xamlMember = new XamlMember(this, "CellStyle", "Microsoft.UI.Xaml.Style");
			xamlMember.Getter = get_70_DataGridColumn_CellStyle;
			xamlMember.Setter = set_70_DataGridColumn_CellStyle;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridColumn.ClipboardContentBinding":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridColumn");
			xamlMember = new XamlMember(this, "ClipboardContentBinding", "Microsoft.UI.Xaml.Data.Binding");
			xamlMember.Getter = get_71_DataGridColumn_ClipboardContentBinding;
			xamlMember.Setter = set_71_DataGridColumn_ClipboardContentBinding;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridColumn.DisplayIndex":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridColumn");
			xamlMember = new XamlMember(this, "DisplayIndex", "Int32");
			xamlMember.Getter = get_72_DataGridColumn_DisplayIndex;
			xamlMember.Setter = set_72_DataGridColumn_DisplayIndex;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridColumn.DragIndicatorStyle":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridColumn");
			xamlMember = new XamlMember(this, "DragIndicatorStyle", "Microsoft.UI.Xaml.Style");
			xamlMember.Getter = get_73_DataGridColumn_DragIndicatorStyle;
			xamlMember.Setter = set_73_DataGridColumn_DragIndicatorStyle;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridColumn.HeaderStyle":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridColumn");
			xamlMember = new XamlMember(this, "HeaderStyle", "Microsoft.UI.Xaml.Style");
			xamlMember.Getter = get_74_DataGridColumn_HeaderStyle;
			xamlMember.Setter = set_74_DataGridColumn_HeaderStyle;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridColumn.Header":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridColumn");
			xamlMember = new XamlMember(this, "Header", "Object");
			xamlMember.Getter = get_75_DataGridColumn_Header;
			xamlMember.Setter = set_75_DataGridColumn_Header;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridColumn.IsAutoGenerated":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridColumn");
			xamlMember = new XamlMember(this, "IsAutoGenerated", "Boolean");
			xamlMember.Getter = get_76_DataGridColumn_IsAutoGenerated;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridColumn.IsFrozen":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridColumn");
			xamlMember = new XamlMember(this, "IsFrozen", "Boolean");
			xamlMember.Getter = get_77_DataGridColumn_IsFrozen;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridColumn.IsReadOnly":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridColumn");
			xamlMember = new XamlMember(this, "IsReadOnly", "Boolean");
			xamlMember.Getter = get_78_DataGridColumn_IsReadOnly;
			xamlMember.Setter = set_78_DataGridColumn_IsReadOnly;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridColumn.MaxWidth":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridColumn");
			xamlMember = new XamlMember(this, "MaxWidth", "Double");
			xamlMember.Getter = get_79_DataGridColumn_MaxWidth;
			xamlMember.Setter = set_79_DataGridColumn_MaxWidth;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridColumn.MinWidth":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridColumn");
			xamlMember = new XamlMember(this, "MinWidth", "Double");
			xamlMember.Getter = get_80_DataGridColumn_MinWidth;
			xamlMember.Setter = set_80_DataGridColumn_MinWidth;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridColumn.SortDirection":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridColumn");
			xamlMember = new XamlMember(this, "SortDirection", "System.Nullable`1<CommunityToolkit.WinUI.UI.Controls.DataGridSortDirection>");
			xamlMember.Getter = get_81_DataGridColumn_SortDirection;
			xamlMember.Setter = set_81_DataGridColumn_SortDirection;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridColumn.Tag":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridColumn");
			xamlMember = new XamlMember(this, "Tag", "Object");
			xamlMember.Getter = get_82_DataGridColumn_Tag;
			xamlMember.Setter = set_82_DataGridColumn_Tag;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridColumn.Visibility":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridColumn");
			xamlMember = new XamlMember(this, "Visibility", "Microsoft.UI.Xaml.Visibility");
			xamlMember.Getter = get_83_DataGridColumn_Visibility;
			xamlMember.Setter = set_83_DataGridColumn_Visibility;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridColumn.Width":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridColumn");
			xamlMember = new XamlMember(this, "Width", "CommunityToolkit.WinUI.UI.Controls.DataGridLength");
			xamlMember.Getter = get_84_DataGridColumn_Width;
			xamlMember.Setter = set_84_DataGridColumn_Width;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.RowDetailsTemplate":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "RowDetailsTemplate", "Microsoft.UI.Xaml.DataTemplate");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_85_DataGrid_RowDetailsTemplate;
			xamlMember.Setter = set_85_DataGrid_RowDetailsTemplate;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.AlternatingRowBackground":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "AlternatingRowBackground", "Microsoft.UI.Xaml.Media.Brush");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_86_DataGrid_AlternatingRowBackground;
			xamlMember.Setter = set_86_DataGrid_AlternatingRowBackground;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.AlternatingRowForeground":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "AlternatingRowForeground", "Microsoft.UI.Xaml.Media.Brush");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_87_DataGrid_AlternatingRowForeground;
			xamlMember.Setter = set_87_DataGrid_AlternatingRowForeground;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.AreRowDetailsFrozen":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "AreRowDetailsFrozen", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_88_DataGrid_AreRowDetailsFrozen;
			xamlMember.Setter = set_88_DataGrid_AreRowDetailsFrozen;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.AreRowGroupHeadersFrozen":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "AreRowGroupHeadersFrozen", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_89_DataGrid_AreRowGroupHeadersFrozen;
			xamlMember.Setter = set_89_DataGrid_AreRowGroupHeadersFrozen;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.CanUserResizeColumns":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "CanUserResizeColumns", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_90_DataGrid_CanUserResizeColumns;
			xamlMember.Setter = set_90_DataGrid_CanUserResizeColumns;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.CellStyle":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "CellStyle", "Microsoft.UI.Xaml.Style");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_91_DataGrid_CellStyle;
			xamlMember.Setter = set_91_DataGrid_CellStyle;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.ClipboardCopyMode":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "ClipboardCopyMode", "CommunityToolkit.WinUI.UI.Controls.DataGridClipboardCopyMode");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_92_DataGrid_ClipboardCopyMode;
			xamlMember.Setter = set_92_DataGrid_ClipboardCopyMode;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.ColumnHeaderHeight":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "ColumnHeaderHeight", "Double");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_93_DataGrid_ColumnHeaderHeight;
			xamlMember.Setter = set_93_DataGrid_ColumnHeaderHeight;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.ColumnWidth":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "ColumnWidth", "CommunityToolkit.WinUI.UI.Controls.DataGridLength");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_94_DataGrid_ColumnWidth;
			xamlMember.Setter = set_94_DataGrid_ColumnWidth;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.DataFetchSize":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "DataFetchSize", "Double");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_95_DataGrid_DataFetchSize;
			xamlMember.Setter = set_95_DataGrid_DataFetchSize;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.DragIndicatorStyle":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "DragIndicatorStyle", "Microsoft.UI.Xaml.Style");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_96_DataGrid_DragIndicatorStyle;
			xamlMember.Setter = set_96_DataGrid_DragIndicatorStyle;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.DropLocationIndicatorStyle":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "DropLocationIndicatorStyle", "Microsoft.UI.Xaml.Style");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_97_DataGrid_DropLocationIndicatorStyle;
			xamlMember.Setter = set_97_DataGrid_DropLocationIndicatorStyle;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.FrozenColumnCount":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "FrozenColumnCount", "Int32");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_98_DataGrid_FrozenColumnCount;
			xamlMember.Setter = set_98_DataGrid_FrozenColumnCount;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.HorizontalGridLinesBrush":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "HorizontalGridLinesBrush", "Microsoft.UI.Xaml.Media.Brush");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_99_DataGrid_HorizontalGridLinesBrush;
			xamlMember.Setter = set_99_DataGrid_HorizontalGridLinesBrush;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.HorizontalScrollBarVisibility":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "HorizontalScrollBarVisibility", "Microsoft.UI.Xaml.Controls.ScrollBarVisibility");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_100_DataGrid_HorizontalScrollBarVisibility;
			xamlMember.Setter = set_100_DataGrid_HorizontalScrollBarVisibility;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.IsReadOnly":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "IsReadOnly", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_101_DataGrid_IsReadOnly;
			xamlMember.Setter = set_101_DataGrid_IsReadOnly;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.IsValid":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "IsValid", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_102_DataGrid_IsValid;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.IncrementalLoadingThreshold":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "IncrementalLoadingThreshold", "Double");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_103_DataGrid_IncrementalLoadingThreshold;
			xamlMember.Setter = set_103_DataGrid_IncrementalLoadingThreshold;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.IncrementalLoadingTrigger":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "IncrementalLoadingTrigger", "Microsoft.UI.Xaml.Controls.IncrementalLoadingTrigger");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_104_DataGrid_IncrementalLoadingTrigger;
			xamlMember.Setter = set_104_DataGrid_IncrementalLoadingTrigger;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.MaxColumnWidth":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "MaxColumnWidth", "Double");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_105_DataGrid_MaxColumnWidth;
			xamlMember.Setter = set_105_DataGrid_MaxColumnWidth;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.MinColumnWidth":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "MinColumnWidth", "Double");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_106_DataGrid_MinColumnWidth;
			xamlMember.Setter = set_106_DataGrid_MinColumnWidth;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.RowBackground":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "RowBackground", "Microsoft.UI.Xaml.Media.Brush");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_107_DataGrid_RowBackground;
			xamlMember.Setter = set_107_DataGrid_RowBackground;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.RowForeground":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "RowForeground", "Microsoft.UI.Xaml.Media.Brush");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_108_DataGrid_RowForeground;
			xamlMember.Setter = set_108_DataGrid_RowForeground;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.RowHeaderWidth":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "RowHeaderWidth", "Double");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_109_DataGrid_RowHeaderWidth;
			xamlMember.Setter = set_109_DataGrid_RowHeaderWidth;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.RowHeaderStyle":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "RowHeaderStyle", "Microsoft.UI.Xaml.Style");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_110_DataGrid_RowHeaderStyle;
			xamlMember.Setter = set_110_DataGrid_RowHeaderStyle;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.SelectedIndex":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "SelectedIndex", "Int32");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_111_DataGrid_SelectedIndex;
			xamlMember.Setter = set_111_DataGrid_SelectedIndex;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.SelectedItem":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "SelectedItem", "Object");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_112_DataGrid_SelectedItem;
			xamlMember.Setter = set_112_DataGrid_SelectedItem;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.VerticalGridLinesBrush":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "VerticalGridLinesBrush", "Microsoft.UI.Xaml.Media.Brush");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_113_DataGrid_VerticalGridLinesBrush;
			xamlMember.Setter = set_113_DataGrid_VerticalGridLinesBrush;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.VerticalScrollBarVisibility":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "VerticalScrollBarVisibility", "Microsoft.UI.Xaml.Controls.ScrollBarVisibility");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_114_DataGrid_VerticalScrollBarVisibility;
			xamlMember.Setter = set_114_DataGrid_VerticalScrollBarVisibility;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.CurrentColumn":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "CurrentColumn", "CommunityToolkit.WinUI.UI.Controls.DataGridColumn");
			xamlMember.Getter = get_115_DataGrid_CurrentColumn;
			xamlMember.Setter = set_115_DataGrid_CurrentColumn;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.RowGroupHeaderPropertyNameAlternative":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "RowGroupHeaderPropertyNameAlternative", "String");
			xamlMember.Getter = get_116_DataGrid_RowGroupHeaderPropertyNameAlternative;
			xamlMember.Setter = set_116_DataGrid_RowGroupHeaderPropertyNameAlternative;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.RowGroupHeaderStyles":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "RowGroupHeaderStyles", "System.Collections.ObjectModel.ObservableCollection`1<Microsoft.UI.Xaml.Style>");
			xamlMember.Getter = get_117_DataGrid_RowGroupHeaderStyles;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGrid.SelectedItems":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGrid");
			xamlMember = new XamlMember(this, "SelectedItems", "System.Collections.IList");
			xamlMember.Getter = get_118_DataGrid_SelectedItems;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.Primitives.DataGridColumnHeader.SeparatorBrush":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.Primitives.DataGridColumnHeader");
			xamlMember = new XamlMember(this, "SeparatorBrush", "Microsoft.UI.Xaml.Media.Brush");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_119_DataGridColumnHeader_SeparatorBrush;
			xamlMember.Setter = set_119_DataGridColumnHeader_SeparatorBrush;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.Primitives.DataGridColumnHeader.SeparatorVisibility":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.Primitives.DataGridColumnHeader");
			xamlMember = new XamlMember(this, "SeparatorVisibility", "Microsoft.UI.Xaml.Visibility");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_120_DataGridColumnHeader_SeparatorVisibility;
			xamlMember.Setter = set_120_DataGridColumnHeader_SeparatorVisibility;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridRow.DetailsTemplate":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridRow");
			xamlMember = new XamlMember(this, "DetailsTemplate", "Microsoft.UI.Xaml.DataTemplate");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_121_DataGridRow_DetailsTemplate;
			xamlMember.Setter = set_121_DataGridRow_DetailsTemplate;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridRow.DetailsVisibility":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridRow");
			xamlMember = new XamlMember(this, "DetailsVisibility", "Microsoft.UI.Xaml.Visibility");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_122_DataGridRow_DetailsVisibility;
			xamlMember.Setter = set_122_DataGridRow_DetailsVisibility;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridRow.Header":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridRow");
			xamlMember = new XamlMember(this, "Header", "Object");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_123_DataGridRow_Header;
			xamlMember.Setter = set_123_DataGridRow_Header;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridRow.HeaderStyle":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridRow");
			xamlMember = new XamlMember(this, "HeaderStyle", "Microsoft.UI.Xaml.Style");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_124_DataGridRow_HeaderStyle;
			xamlMember.Setter = set_124_DataGridRow_HeaderStyle;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridRow.IsValid":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridRow");
			xamlMember = new XamlMember(this, "IsValid", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_125_DataGridRow_IsValid;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridTemplateColumn.CellTemplate":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridTemplateColumn");
			xamlMember = new XamlMember(this, "CellTemplate", "Microsoft.UI.Xaml.DataTemplate");
			xamlMember.Getter = get_126_DataGridTemplateColumn_CellTemplate;
			xamlMember.Setter = set_126_DataGridTemplateColumn_CellTemplate;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridTemplateColumn.CellEditingTemplate":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridTemplateColumn");
			xamlMember = new XamlMember(this, "CellEditingTemplate", "Microsoft.UI.Xaml.DataTemplate");
			xamlMember.Getter = get_127_DataGridTemplateColumn_CellEditingTemplate;
			xamlMember.Setter = set_127_DataGridTemplateColumn_CellEditingTemplate;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridBoundColumn.Binding":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridBoundColumn");
			xamlMember = new XamlMember(this, "Binding", "Microsoft.UI.Xaml.Data.Binding");
			xamlMember.Getter = get_128_DataGridBoundColumn_Binding;
			xamlMember.Setter = set_128_DataGridBoundColumn_Binding;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridBoundColumn.ElementStyle":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridBoundColumn");
			xamlMember = new XamlMember(this, "ElementStyle", "Microsoft.UI.Xaml.Style");
			xamlMember.Getter = get_129_DataGridBoundColumn_ElementStyle;
			xamlMember.Setter = set_129_DataGridBoundColumn_ElementStyle;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridTextColumn.FontFamily":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridTextColumn");
			xamlMember = new XamlMember(this, "FontFamily", "Microsoft.UI.Xaml.Media.FontFamily");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_130_DataGridTextColumn_FontFamily;
			xamlMember.Setter = set_130_DataGridTextColumn_FontFamily;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridTextColumn.FontSize":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridTextColumn");
			xamlMember = new XamlMember(this, "FontSize", "Double");
			xamlMember.Getter = get_131_DataGridTextColumn_FontSize;
			xamlMember.Setter = set_131_DataGridTextColumn_FontSize;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridTextColumn.FontStyle":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridTextColumn");
			xamlMember = new XamlMember(this, "FontStyle", "Windows.UI.Text.FontStyle");
			xamlMember.Getter = get_132_DataGridTextColumn_FontStyle;
			xamlMember.Setter = set_132_DataGridTextColumn_FontStyle;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridTextColumn.FontWeight":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridTextColumn");
			xamlMember = new XamlMember(this, "FontWeight", "Windows.UI.Text.FontWeight");
			xamlMember.Getter = get_133_DataGridTextColumn_FontWeight;
			xamlMember.Setter = set_133_DataGridTextColumn_FontWeight;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridTextColumn.Foreground":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridTextColumn");
			xamlMember = new XamlMember(this, "Foreground", "Microsoft.UI.Xaml.Media.Brush");
			xamlMember.Getter = get_134_DataGridTextColumn_Foreground;
			xamlMember.Setter = set_134_DataGridTextColumn_Foreground;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridBoundColumn.ClipboardContentBinding":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridBoundColumn");
			xamlMember = new XamlMember(this, "ClipboardContentBinding", "Microsoft.UI.Xaml.Data.Binding");
			xamlMember.Getter = get_135_DataGridBoundColumn_ClipboardContentBinding;
			xamlMember.Setter = set_135_DataGridBoundColumn_ClipboardContentBinding;
			break;
		}
		case "CommunityToolkit.WinUI.UI.Controls.DataGridBoundColumn.EditingElementStyle":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("CommunityToolkit.WinUI.UI.Controls.DataGridBoundColumn");
			xamlMember = new XamlMember(this, "EditingElementStyle", "Microsoft.UI.Xaml.Style");
			xamlMember.Getter = get_136_DataGridBoundColumn_EditingElementStyle;
			xamlMember.Setter = set_136_DataGridBoundColumn_EditingElementStyle;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.ProgressBar.IsIndeterminate":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ProgressBar");
			xamlMember = new XamlMember(this, "IsIndeterminate", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_137_ProgressBar_IsIndeterminate;
			xamlMember.Setter = set_137_ProgressBar_IsIndeterminate;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.ProgressBar.ShowError":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ProgressBar");
			xamlMember = new XamlMember(this, "ShowError", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_138_ProgressBar_ShowError;
			xamlMember.Setter = set_138_ProgressBar_ShowError;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.ProgressBar.ShowPaused":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ProgressBar");
			xamlMember = new XamlMember(this, "ShowPaused", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_139_ProgressBar_ShowPaused;
			xamlMember.Setter = set_139_ProgressBar_ShowPaused;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.ProgressBar.TemplateSettings":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.ProgressBar");
			xamlMember = new XamlMember(this, "TemplateSettings", "Microsoft.UI.Xaml.Controls.ProgressBarTemplateSettings");
			xamlMember.Getter = get_140_ProgressBar_TemplateSettings;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "StormSwitchBox.Views.HistoryPage.HistoryTasks":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Views.HistoryPage");
			xamlMember = new XamlMember(this, "HistoryTasks", "System.Collections.ObjectModel.ObservableCollection`1<StormSwitchBox.Models.ProcessingTask>");
			xamlMember.Getter = get_141_HistoryPage_HistoryTasks;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.Cts":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "Cts", "System.Threading.CancellationTokenSource");
			xamlMember.Getter = get_142_ProcessingTask_Cts;
			xamlMember.Setter = set_142_ProcessingTask_Cts;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.FinishedAtDisplay":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "FinishedAtDisplay", "String");
			xamlMember.Getter = get_143_ProcessingTask_FinishedAtDisplay;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.IsNotRunning":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "IsNotRunning", "Boolean");
			xamlMember.Getter = get_144_ProcessingTask_IsNotRunning;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.GroupId":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "GroupId", "String");
			xamlMember.Getter = get_145_ProcessingTask_GroupId;
			xamlMember.Setter = set_145_ProcessingTask_GroupId;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.InputFiles":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "InputFiles", "System.Collections.Generic.List`1<String>");
			xamlMember.Getter = get_146_ProcessingTask_InputFiles;
			xamlMember.Setter = set_146_ProcessingTask_InputFiles;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.FilesList":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "FilesList", "System.Collections.Generic.List`1<String>");
			xamlMember.Getter = get_147_ProcessingTask_FilesList;
			xamlMember.Setter = set_147_ProcessingTask_FilesList;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.DetailsVisibility":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "DetailsVisibility", "Microsoft.UI.Xaml.Visibility");
			xamlMember.Getter = get_148_ProcessingTask_DetailsVisibility;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.OperationDisplay":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "OperationDisplay", "String");
			xamlMember.Getter = get_149_ProcessingTask_OperationDisplay;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.StatusColor":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "StatusColor", "Microsoft.UI.Xaml.Media.SolidColorBrush");
			xamlMember.Getter = get_150_ProcessingTask_StatusColor;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.Id":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "Id", "String");
			xamlMember.Getter = get_151_ProcessingTask_Id;
			xamlMember.Setter = set_151_ProcessingTask_Id;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.GameIcon":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "GameIcon", "Microsoft.UI.Xaml.Media.Imaging.BitmapImage");
			xamlMember.Getter = get_152_ProcessingTask_GameIcon;
			xamlMember.Setter = set_152_ProcessingTask_GameIcon;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.GameName":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "GameName", "String");
			xamlMember.Getter = get_153_ProcessingTask_GameName;
			xamlMember.Setter = set_153_ProcessingTask_GameName;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.Operation":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "Operation", "String");
			xamlMember.Getter = get_154_ProcessingTask_Operation;
			xamlMember.Setter = set_154_ProcessingTask_Operation;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.SourceFormat":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "SourceFormat", "String");
			xamlMember.Getter = get_155_ProcessingTask_SourceFormat;
			xamlMember.Setter = set_155_ProcessingTask_SourceFormat;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.TargetFormat":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "TargetFormat", "String");
			xamlMember.Getter = get_156_ProcessingTask_TargetFormat;
			xamlMember.Setter = set_156_ProcessingTask_TargetFormat;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.SourceSize":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "SourceSize", "String");
			xamlMember.Getter = get_157_ProcessingTask_SourceSize;
			xamlMember.Setter = set_157_ProcessingTask_SourceSize;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.TargetSize":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "TargetSize", "String");
			xamlMember.Getter = get_158_ProcessingTask_TargetSize;
			xamlMember.Setter = set_158_ProcessingTask_TargetSize;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.SizeDifference":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "SizeDifference", "String");
			xamlMember.Getter = get_159_ProcessingTask_SizeDifference;
			xamlMember.Setter = set_159_ProcessingTask_SizeDifference;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.CompressionLevel":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "CompressionLevel", "String");
			xamlMember.Getter = get_160_ProcessingTask_CompressionLevel;
			xamlMember.Setter = set_160_ProcessingTask_CompressionLevel;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.FilesCount":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "FilesCount", "String");
			xamlMember.Getter = get_161_ProcessingTask_FilesCount;
			xamlMember.Setter = set_161_ProcessingTask_FilesCount;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.Status":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "Status", "String");
			xamlMember.Getter = get_162_ProcessingTask_Status;
			xamlMember.Setter = set_162_ProcessingTask_Status;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.Progress":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "Progress", "Double");
			xamlMember.Getter = get_163_ProcessingTask_Progress;
			xamlMember.Setter = set_163_ProcessingTask_Progress;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.IsRunning":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "IsRunning", "Boolean");
			xamlMember.Getter = get_164_ProcessingTask_IsRunning;
			xamlMember.Setter = set_164_ProcessingTask_IsRunning;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.FinishedAt":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "FinishedAt", "System.DateTime");
			xamlMember.Getter = get_165_ProcessingTask_FinishedAt;
			xamlMember.Setter = set_165_ProcessingTask_FinishedAt;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.SourceSizeBytes":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "SourceSizeBytes", "Int64");
			xamlMember.Getter = get_166_ProcessingTask_SourceSizeBytes;
			xamlMember.Setter = set_166_ProcessingTask_SourceSizeBytes;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.HasRomFs":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "HasRomFs", "String");
			xamlMember.Getter = get_167_ProcessingTask_HasRomFs;
			xamlMember.Setter = set_167_ProcessingTask_HasRomFs;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.HasExeFs":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "HasExeFs", "String");
			xamlMember.Getter = get_168_ProcessingTask_HasExeFs;
			xamlMember.Setter = set_168_ProcessingTask_HasExeFs;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.Speed":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "Speed", "String");
			xamlMember.Getter = get_169_ProcessingTask_Speed;
			xamlMember.Setter = set_169_ProcessingTask_Speed;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.IsExpanded":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "IsExpanded", "Boolean");
			xamlMember.Getter = get_170_ProcessingTask_IsExpanded;
			xamlMember.Setter = set_170_ProcessingTask_IsExpanded;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.VerifyType":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "VerifyType", "String");
			xamlMember.Getter = get_171_ProcessingTask_VerifyType;
			xamlMember.Setter = set_171_ProcessingTask_VerifyType;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.VerifyStructure":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "VerifyStructure", "String");
			xamlMember.Getter = get_172_ProcessingTask_VerifyStructure;
			xamlMember.Setter = set_172_ProcessingTask_VerifyStructure;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.VerifyTitleId":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "VerifyTitleId", "String");
			xamlMember.Getter = get_173_ProcessingTask_VerifyTitleId;
			xamlMember.Setter = set_173_ProcessingTask_VerifyTitleId;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.VerifyVersion":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "VerifyVersion", "String");
			xamlMember.Getter = get_174_ProcessingTask_VerifyVersion;
			xamlMember.Setter = set_174_ProcessingTask_VerifyVersion;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.VerifyMergedStatus":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "VerifyMergedStatus", "String");
			xamlMember.Getter = get_175_ProcessingTask_VerifyMergedStatus;
			xamlMember.Setter = set_175_ProcessingTask_VerifyMergedStatus;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.InputFolders":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "InputFolders", "String");
			xamlMember.Getter = get_176_ProcessingTask_InputFolders;
			xamlMember.Setter = set_176_ProcessingTask_InputFolders;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.OutputFolder":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "OutputFolder", "String");
			xamlMember.Getter = get_177_ProcessingTask_OutputFolder;
			xamlMember.Setter = set_177_ProcessingTask_OutputFolder;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.OutputFileName":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "OutputFileName", "String");
			xamlMember.Getter = get_178_ProcessingTask_OutputFileName;
			xamlMember.Setter = set_178_ProcessingTask_OutputFileName;
			break;
		}
		case "StormSwitchBox.Models.ProcessingTask.LogDetails":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.ProcessingTask");
			xamlMember = new XamlMember(this, "LogDetails", "String");
			xamlMember.Getter = get_179_ProcessingTask_LogDetails;
			xamlMember.Setter = set_179_ProcessingTask_LogDetails;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NumberBox.Value":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
			xamlMember = new XamlMember(this, "Value", "Double");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_180_NumberBox_Value;
			xamlMember.Setter = set_180_NumberBox_Value;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NumberBox.Minimum":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
			xamlMember = new XamlMember(this, "Minimum", "Double");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_181_NumberBox_Minimum;
			xamlMember.Setter = set_181_NumberBox_Minimum;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NumberBox.Maximum":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
			xamlMember = new XamlMember(this, "Maximum", "Double");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_182_NumberBox_Maximum;
			xamlMember.Setter = set_182_NumberBox_Maximum;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NumberBox.SpinButtonPlacementMode":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
			xamlMember = new XamlMember(this, "SpinButtonPlacementMode", "Microsoft.UI.Xaml.Controls.NumberBoxSpinButtonPlacementMode");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_183_NumberBox_SpinButtonPlacementMode;
			xamlMember.Setter = set_183_NumberBox_SpinButtonPlacementMode;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NumberBox.AcceptsExpression":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
			xamlMember = new XamlMember(this, "AcceptsExpression", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_184_NumberBox_AcceptsExpression;
			xamlMember.Setter = set_184_NumberBox_AcceptsExpression;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NumberBox.Description":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
			xamlMember = new XamlMember(this, "Description", "Object");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_185_NumberBox_Description;
			xamlMember.Setter = set_185_NumberBox_Description;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NumberBox.Header":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
			xamlMember = new XamlMember(this, "Header", "Object");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_186_NumberBox_Header;
			xamlMember.Setter = set_186_NumberBox_Header;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NumberBox.HeaderTemplate":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
			xamlMember = new XamlMember(this, "HeaderTemplate", "Microsoft.UI.Xaml.DataTemplate");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_187_NumberBox_HeaderTemplate;
			xamlMember.Setter = set_187_NumberBox_HeaderTemplate;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NumberBox.IsWrapEnabled":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
			xamlMember = new XamlMember(this, "IsWrapEnabled", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_188_NumberBox_IsWrapEnabled;
			xamlMember.Setter = set_188_NumberBox_IsWrapEnabled;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NumberBox.LargeChange":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
			xamlMember = new XamlMember(this, "LargeChange", "Double");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_189_NumberBox_LargeChange;
			xamlMember.Setter = set_189_NumberBox_LargeChange;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NumberBox.NumberFormatter":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
			xamlMember = new XamlMember(this, "NumberFormatter", "Windows.Globalization.NumberFormatting.INumberFormatter2");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_190_NumberBox_NumberFormatter;
			xamlMember.Setter = set_190_NumberBox_NumberFormatter;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NumberBox.PlaceholderText":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
			xamlMember = new XamlMember(this, "PlaceholderText", "String");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_191_NumberBox_PlaceholderText;
			xamlMember.Setter = set_191_NumberBox_PlaceholderText;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NumberBox.PreventKeyboardDisplayOnProgrammaticFocus":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
			xamlMember = new XamlMember(this, "PreventKeyboardDisplayOnProgrammaticFocus", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_192_NumberBox_PreventKeyboardDisplayOnProgrammaticFocus;
			xamlMember.Setter = set_192_NumberBox_PreventKeyboardDisplayOnProgrammaticFocus;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NumberBox.SelectionFlyout":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
			xamlMember = new XamlMember(this, "SelectionFlyout", "Microsoft.UI.Xaml.Controls.Primitives.FlyoutBase");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_193_NumberBox_SelectionFlyout;
			xamlMember.Setter = set_193_NumberBox_SelectionFlyout;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NumberBox.SelectionHighlightColor":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
			xamlMember = new XamlMember(this, "SelectionHighlightColor", "Microsoft.UI.Xaml.Media.SolidColorBrush");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_194_NumberBox_SelectionHighlightColor;
			xamlMember.Setter = set_194_NumberBox_SelectionHighlightColor;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NumberBox.SmallChange":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
			xamlMember = new XamlMember(this, "SmallChange", "Double");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_195_NumberBox_SmallChange;
			xamlMember.Setter = set_195_NumberBox_SmallChange;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NumberBox.Text":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
			xamlMember = new XamlMember(this, "Text", "String");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_196_NumberBox_Text;
			xamlMember.Setter = set_196_NumberBox_Text;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NumberBox.TextReadingOrder":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
			xamlMember = new XamlMember(this, "TextReadingOrder", "Microsoft.UI.Xaml.TextReadingOrder");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_197_NumberBox_TextReadingOrder;
			xamlMember.Setter = set_197_NumberBox_TextReadingOrder;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.NumberBox.ValidationMode":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.NumberBox");
			xamlMember = new XamlMember(this, "ValidationMode", "Microsoft.UI.Xaml.Controls.NumberBoxValidationMode");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_198_NumberBox_ValidationMode;
			xamlMember.Setter = set_198_NumberBox_ValidationMode;
			break;
		}
		case "StormSwitchBox.Views.SettingsPage.MaxCores":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Views.SettingsPage");
			xamlMember = new XamlMember(this, "MaxCores", "Int32");
			xamlMember.Getter = get_199_SettingsPage_MaxCores;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "StormSwitchBox.Views.SettingsPage.Settings":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Views.SettingsPage");
			xamlMember = new XamlMember(this, "Settings", "StormSwitchBox.Models.AppSettings");
			xamlMember.Getter = get_200_SettingsPage_Settings;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "StormSwitchBox.Views.SettingsPage.KeysSelectedVisibility":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Views.SettingsPage");
			xamlMember = new XamlMember(this, "KeysSelectedVisibility", "Microsoft.UI.Xaml.Visibility");
			xamlMember.Getter = get_201_SettingsPage_KeysSelectedVisibility;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "StormSwitchBox.Views.TasksPage.ViewModel":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Views.TasksPage");
			xamlMember = new XamlMember(this, "ViewModel", "StormSwitchBox.ViewModels.TasksViewModel");
			xamlMember.Getter = get_202_TasksPage_ViewModel;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "StormSwitchBox.Views.TasksPage.AppLogs":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Views.TasksPage");
			xamlMember = new XamlMember(this, "AppLogs", "System.Collections.ObjectModel.ObservableCollection`1<StormSwitchBox.Models.LogMessage>");
			xamlMember.Getter = get_203_TasksPage_AppLogs;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "StormSwitchBox.Models.LogMessage.Timestamp":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.LogMessage");
			xamlMember = new XamlMember(this, "Timestamp", "System.DateTime");
			xamlMember.Getter = get_204_LogMessage_Timestamp;
			xamlMember.Setter = set_204_LogMessage_Timestamp;
			break;
		}
		case "StormSwitchBox.Models.LogMessage.Message":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.LogMessage");
			xamlMember = new XamlMember(this, "Message", "String");
			xamlMember.Getter = get_205_LogMessage_Message;
			xamlMember.Setter = set_205_LogMessage_Message;
			break;
		}
		case "StormSwitchBox.Models.LogMessage.Level":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.LogMessage");
			xamlMember = new XamlMember(this, "Level", "StormSwitchBox.Models.LogLevel");
			xamlMember.Getter = get_206_LogMessage_Level;
			xamlMember.Setter = set_206_LogMessage_Level;
			break;
		}
		case "StormSwitchBox.Models.LogMessage.ColorBrush":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.LogMessage");
			xamlMember = new XamlMember(this, "ColorBrush", "Microsoft.UI.Xaml.Media.SolidColorBrush");
			xamlMember.Getter = get_207_LogMessage_ColorBrush;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "StormSwitchBox.Models.LogMessage.FormattedTime":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("StormSwitchBox.Models.LogMessage");
			xamlMember = new XamlMember(this, "FormattedTime", "String");
			xamlMember.Getter = get_208_LogMessage_FormattedTime;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "Microsoft.UI.Xaml.Controls.TreeViewNode.Children":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.TreeViewNode");
			xamlMember = new XamlMember(this, "Children", "System.Collections.Generic.IList`1<Microsoft.UI.Xaml.Controls.TreeViewNode>");
			xamlMember.Getter = get_209_TreeViewNode_Children;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "Microsoft.UI.Xaml.Controls.TreeViewNode.Content":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.TreeViewNode");
			xamlMember = new XamlMember(this, "Content", "Object");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_210_TreeViewNode_Content;
			xamlMember.Setter = set_210_TreeViewNode_Content;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.TreeViewNode.Depth":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.TreeViewNode");
			xamlMember = new XamlMember(this, "Depth", "Int32");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_211_TreeViewNode_Depth;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "Microsoft.UI.Xaml.Controls.TreeViewNode.HasChildren":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.TreeViewNode");
			xamlMember = new XamlMember(this, "HasChildren", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_212_TreeViewNode_HasChildren;
			xamlMember.SetIsReadOnly();
			break;
		}
		case "Microsoft.UI.Xaml.Controls.TreeViewNode.HasUnrealizedChildren":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.TreeViewNode");
			xamlMember = new XamlMember(this, "HasUnrealizedChildren", "Boolean");
			xamlMember.Getter = get_213_TreeViewNode_HasUnrealizedChildren;
			xamlMember.Setter = set_213_TreeViewNode_HasUnrealizedChildren;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.TreeViewNode.IsExpanded":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.TreeViewNode");
			xamlMember = new XamlMember(this, "IsExpanded", "Boolean");
			xamlMember.SetIsDependencyProperty();
			xamlMember.Getter = get_214_TreeViewNode_IsExpanded;
			xamlMember.Setter = set_214_TreeViewNode_IsExpanded;
			break;
		}
		case "Microsoft.UI.Xaml.Controls.TreeViewNode.Parent":
		{
			XamlUserType xamlUserType = (XamlUserType)GetXamlTypeByName("Microsoft.UI.Xaml.Controls.TreeViewNode");
			xamlMember = new XamlMember(this, "Parent", "Microsoft.UI.Xaml.Controls.TreeViewNode");
			xamlMember.Getter = get_215_TreeViewNode_Parent;
			xamlMember.SetIsReadOnly();
			break;
		}
		}
		return xamlMember;
	}
}
