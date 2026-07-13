using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Navigation;
using StormSwitchBox.Models;
using WinRT;
using WinRT.Interop;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace StormSwitchBox.Views;

public sealed class SettingsPage : Page, IComponentConnector
{
	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private interface ISettingsPage_Bindings
	{
		void Initialize();

		void Update();

		void StopTracking();

		void DisconnectUnloadedObject(int connectionId);
	}

	private interface ISettingsPage_BindingsScopeConnector
	{
		WeakReference Parent { get; set; }

		bool ContainsElement(int connectionId);

		void RegisterForElementConnection(int connectionId, IComponentConnector connector);
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	[DebuggerNonUserCode]
	private static class XamlBindingSetters
	{
		public static void Set_Microsoft_UI_Xaml_Controls_ToggleSwitch_IsOn(ToggleSwitch obj, bool value)
		{
			obj.IsOn = value;
		}

		public static void Set_Microsoft_UI_Xaml_UIElement_Visibility(UIElement obj, Visibility value)
		{
			obj.Visibility = value;
		}

		public static void Set_Microsoft_UI_Xaml_Controls_NumberBox_Value(NumberBox obj, double value)
		{
			obj.Value = value;
		}

		public static void Set_Microsoft_UI_Xaml_Controls_NumberBox_Maximum(NumberBox obj, double value)
		{
			obj.Maximum = value;
		}

		public static void Set_Microsoft_UI_Xaml_Controls_TextBox_Text(TextBox obj, string value, string targetNullValue)
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
	private class SettingsPage_obj1_Bindings : IDataTemplateComponent, IXamlBindScopeDiagnostics, IComponentConnector, ISettingsPage_Bindings
	{
		[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
		[DebuggerNonUserCode]
		private class SettingsPage_obj1_BindingsTracking
		{
			private WeakReference<SettingsPage_obj1_Bindings> weakRefToBindingObj;

			private ToggleSwitch cache_TrimXciToggle = null;

			private long tokenDPC_TrimXciToggle_IsOn = 0L;

			public SettingsPage_obj1_BindingsTracking(SettingsPage_obj1_Bindings obj)
			{
				weakRefToBindingObj = new WeakReference<SettingsPage_obj1_Bindings>(obj);
			}

			public SettingsPage_obj1_Bindings TryGetBindingObject()
			{
				SettingsPage_obj1_Bindings target = null;
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
				UpdateChildListeners_TrimXciToggle(null);
			}

			public void DependencyPropertyChanged_TrimXciToggle_IsOn(DependencyObject sender, DependencyProperty prop)
			{
				SettingsPage_obj1_Bindings settingsPage_obj1_Bindings = TryGetBindingObject();
				if (settingsPage_obj1_Bindings != null)
				{
					ToggleSwitch toggleSwitch = sender as ToggleSwitch;
					if (toggleSwitch != null)
					{
						settingsPage_obj1_Bindings.Update_TrimXciToggle_IsOn(toggleSwitch.IsOn, 1073741824);
					}
				}
			}

			public void UpdateChildListeners_TrimXciToggle(ToggleSwitch obj)
			{
				if (obj != cache_TrimXciToggle)
				{
					if (cache_TrimXciToggle != null)
					{
						cache_TrimXciToggle.UnregisterPropertyChangedCallback(ToggleSwitch.IsOnProperty, tokenDPC_TrimXciToggle_IsOn);
						cache_TrimXciToggle = null;
					}
					if (obj != null)
					{
						cache_TrimXciToggle = obj;
						tokenDPC_TrimXciToggle_IsOn = obj.RegisterPropertyChangedCallback(ToggleSwitch.IsOnProperty, DependencyPropertyChanged_TrimXciToggle_IsOn);
					}
				}
			}

			public void RegisterTwoWayListener_2(ToggleSwitch sourceObject)
			{
				sourceObject.RegisterPropertyChangedCallback(ToggleSwitch.IsOnProperty, delegate
				{
					TryGetBindingObject()?.UpdateTwoWay_2_IsOn();
				});
			}

			public void RegisterTwoWayListener_3(ToggleSwitch sourceObject)
			{
				sourceObject.RegisterPropertyChangedCallback(ToggleSwitch.IsOnProperty, delegate
				{
					TryGetBindingObject()?.UpdateTwoWay_3_IsOn();
				});
			}

			public void RegisterTwoWayListener_6(ToggleSwitch sourceObject)
			{
				sourceObject.RegisterPropertyChangedCallback(ToggleSwitch.IsOnProperty, delegate
				{
					TryGetBindingObject()?.UpdateTwoWay_6_IsOn();
				});
			}

			public void RegisterTwoWayListener_7(ToggleSwitch sourceObject)
			{
				sourceObject.RegisterPropertyChangedCallback(ToggleSwitch.IsOnProperty, delegate
				{
					TryGetBindingObject()?.UpdateTwoWay_7_IsOn();
				});
			}

			public void RegisterTwoWayListener_8(NumberBox sourceObject)
			{
				sourceObject.RegisterPropertyChangedCallback(NumberBox.ValueProperty, delegate
				{
					TryGetBindingObject()?.UpdateTwoWay_8_Value();
				});
			}

			public void RegisterTwoWayListener_9(NumberBox sourceObject)
			{
				sourceObject.RegisterPropertyChangedCallback(NumberBox.ValueProperty, delegate
				{
					TryGetBindingObject()?.UpdateTwoWay_9_Value();
				});
			}

			public void RegisterTwoWayListener_10(NumberBox sourceObject)
			{
				sourceObject.RegisterPropertyChangedCallback(NumberBox.ValueProperty, delegate
				{
					TryGetBindingObject()?.UpdateTwoWay_10_Value();
				});
			}

			public void RegisterTwoWayListener_12(TextBox sourceObject)
			{
				sourceObject.LostFocus += delegate
				{
					TryGetBindingObject()?.UpdateTwoWay_12_Text();
				};
			}

			public void RegisterTwoWayListener_15(TextBox sourceObject)
			{
				sourceObject.LostFocus += delegate
				{
					TryGetBindingObject()?.UpdateTwoWay_15_Text();
				};
			}
		}

		private SettingsPage dataRoot;

		private bool initialized = false;

		private const int NOT_PHASED = int.MinValue;

		private const int DATA_CHANGED = 1073741824;

		private ToggleSwitch obj2;

		private ToggleSwitch obj3;

		private Border obj4;

		private ToggleSwitch obj6;

		private ToggleSwitch obj7;

		private NumberBox obj8;

		private NumberBox obj9;

		private NumberBox obj10;

		private TextBox obj12;

		private StackPanel obj14;

		private TextBox obj15;

		private static bool isobj2IsOnDisabled;

		private static bool isobj3IsOnDisabled;

		private static bool isobj4VisibilityDisabled;

		private static bool isobj6IsOnDisabled;

		private static bool isobj7IsOnDisabled;

		private static bool isobj8ValueDisabled;

		private static bool isobj8MaximumDisabled;

		private static bool isobj9ValueDisabled;

		private static bool isobj10ValueDisabled;

		private static bool isobj12TextDisabled;

		private static bool isobj14VisibilityDisabled;

		private static bool isobj15TextDisabled;

		private SettingsPage_obj1_BindingsTracking bindingsTracking;

		public SettingsPage_obj1_Bindings()
		{
			bindingsTracking = new SettingsPage_obj1_BindingsTracking(this);
		}

		public void Disable(int lineNumber, int columnNumber)
		{
			if (lineNumber == 130 && columnNumber == 71)
			{
				isobj2IsOnDisabled = true;
			}
			else if (lineNumber == 125 && columnNumber == 71)
			{
				isobj3IsOnDisabled = true;
			}
			else if (lineNumber == 109 && columnNumber == 37)
			{
				isobj4VisibilityDisabled = true;
			}
			else if (lineNumber == 104 && columnNumber == 98)
			{
				isobj6IsOnDisabled = true;
			}
			else if (lineNumber == 98 && columnNumber == 71)
			{
				isobj7IsOnDisabled = true;
			}
			else if (lineNumber == 93 && columnNumber == 80)
			{
				isobj8ValueDisabled = true;
			}
			else if (lineNumber == 93 && columnNumber == 141)
			{
				isobj8MaximumDisabled = true;
			}
			else if (lineNumber == 88 && columnNumber == 80)
			{
				isobj9ValueDisabled = true;
			}
			else if (lineNumber == 83 && columnNumber == 80)
			{
				isobj10ValueDisabled = true;
			}
			else if (lineNumber == 58 && columnNumber == 38)
			{
				isobj12TextDisabled = true;
			}
			else if (lineNumber == 27 && columnNumber == 109)
			{
				isobj14VisibilityDisabled = true;
			}
			else if (lineNumber == 31 && columnNumber == 79)
			{
				isobj15TextDisabled = true;
			}
		}

		public void Connect(int connectionId, object target)
		{
			switch (connectionId)
			{
			case 2:
				obj2 = target.As<ToggleSwitch>();
				bindingsTracking.RegisterTwoWayListener_2(obj2);
				break;
			case 3:
				obj3 = target.As<ToggleSwitch>();
				bindingsTracking.RegisterTwoWayListener_3(obj3);
				break;
			case 4:
				obj4 = target.As<Border>();
				break;
			case 6:
				obj6 = target.As<ToggleSwitch>();
				bindingsTracking.RegisterTwoWayListener_6(obj6);
				break;
			case 7:
				obj7 = target.As<ToggleSwitch>();
				bindingsTracking.RegisterTwoWayListener_7(obj7);
				break;
			case 8:
				obj8 = target.As<NumberBox>();
				bindingsTracking.RegisterTwoWayListener_8(obj8);
				break;
			case 9:
				obj9 = target.As<NumberBox>();
				bindingsTracking.RegisterTwoWayListener_9(obj9);
				break;
			case 10:
				obj10 = target.As<NumberBox>();
				bindingsTracking.RegisterTwoWayListener_10(obj10);
				break;
			case 12:
				obj12 = target.As<TextBox>();
				bindingsTracking.RegisterTwoWayListener_12(obj12);
				break;
			case 14:
				obj14 = target.As<StackPanel>();
				break;
			case 15:
				obj15 = target.As<TextBox>();
				bindingsTracking.RegisterTwoWayListener_15(obj15);
				break;
			case 5:
			case 11:
			case 13:
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
				dataRoot = newDataRoot.As<SettingsPage>();
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

		private void Update_(SettingsPage obj, int phase)
		{
			if (obj != null)
			{
				if ((phase & -1073741823) != 0)
				{
					Update_Settings(obj.Settings, phase);
					Update_TrimXciToggle(obj.TrimXciToggle, phase);
				}
				if ((phase & -2147483647) != 0)
				{
					Update_MaxCores(obj.MaxCores, phase);
					Update_KeysSelectedVisibility(obj.KeysSelectedVisibility, phase);
				}
			}
		}

		private void Update_Settings(AppSettings obj, int phase)
		{
			if (obj != null && (phase & -1073741823) != 0)
			{
				Update_Settings_ForceMultiRebuild(obj.ForceMultiRebuild, phase);
				Update_Settings_ComplexFolders(obj.ComplexFolders, phase);
				Update_Settings_TrimXci(obj.TrimXci, phase);
				Update_Settings_DeleteSourceOnSuccess(obj.DeleteSourceOnSuccess, phase);
				Update_Settings_UsedCores(obj.UsedCores, phase);
				Update_Settings_ConcurrentTasks(obj.ConcurrentTasks, phase);
				Update_Settings_KeyGeneration(obj.KeyGeneration, phase);
				Update_Settings_OutputFolder(obj.OutputFolder, phase);
				Update_Settings_KeysVersion(obj.KeysVersion, phase);
			}
		}

		private void Update_Settings_ForceMultiRebuild(bool obj, int phase)
		{
			if ((phase & -1073741823) != 0 && !isobj2IsOnDisabled)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_ToggleSwitch_IsOn(obj2, obj);
			}
		}

		private void Update_Settings_ComplexFolders(bool obj, int phase)
		{
			if ((phase & -1073741823) != 0 && !isobj3IsOnDisabled)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_ToggleSwitch_IsOn(obj3, obj);
			}
		}

		private void Update_TrimXciToggle(ToggleSwitch obj, int phase)
		{
			bindingsTracking.UpdateChildListeners_TrimXciToggle(obj);
			if (obj != null && (phase & -1073741823) != 0)
			{
				Update_TrimXciToggle_IsOn(obj.IsOn, phase);
			}
		}

		private void Update_TrimXciToggle_IsOn(bool obj, int phase)
		{
			if ((phase & -1073741823) != 0)
			{
				Update_TrimXciToggle_IsOn_Cast_IsOn_To_Visibility((!obj) ? Visibility.Collapsed : Visibility.Visible, phase);
			}
		}

		private void Update_TrimXciToggle_IsOn_Cast_IsOn_To_Visibility(Visibility obj, int phase)
		{
			if ((phase & -1073741823) != 0 && !isobj4VisibilityDisabled)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_UIElement_Visibility(obj4, obj);
			}
		}

		private void Update_Settings_TrimXci(bool obj, int phase)
		{
			if ((phase & -1073741823) != 0 && !isobj6IsOnDisabled)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_ToggleSwitch_IsOn(obj6, obj);
			}
		}

		private void Update_Settings_DeleteSourceOnSuccess(bool obj, int phase)
		{
			if ((phase & -1073741823) != 0 && !isobj7IsOnDisabled)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_ToggleSwitch_IsOn(obj7, obj);
			}
		}

		private void Update_Settings_UsedCores(int obj, int phase)
		{
			if ((phase & -1073741823) != 0 && !isobj8ValueDisabled)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_NumberBox_Value(obj8, obj);
			}
		}

		private void Update_MaxCores(int obj, int phase)
		{
			if ((phase & -2147483647) != 0 && !isobj8MaximumDisabled)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_NumberBox_Maximum(obj8, obj);
			}
		}

		private void Update_Settings_ConcurrentTasks(int obj, int phase)
		{
			if ((phase & -1073741823) != 0 && !isobj9ValueDisabled)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_NumberBox_Value(obj9, obj);
			}
		}

		private void Update_Settings_KeyGeneration(int obj, int phase)
		{
			if ((phase & -1073741823) != 0 && !isobj10ValueDisabled)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_NumberBox_Value(obj10, obj);
			}
		}

		private void Update_Settings_OutputFolder(string obj, int phase)
		{
			if ((phase & -1073741823) != 0 && !isobj12TextDisabled)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_TextBox_Text(obj12, obj, null);
			}
		}

		private void Update_KeysSelectedVisibility(Visibility obj, int phase)
		{
			if ((phase & -2147483647) != 0 && !isobj14VisibilityDisabled)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_UIElement_Visibility(obj14, obj);
			}
		}

		private void Update_Settings_KeysVersion(string obj, int phase)
		{
			if ((phase & -1073741823) != 0 && !isobj15TextDisabled)
			{
				XamlBindingSetters.Set_Microsoft_UI_Xaml_Controls_TextBox_Text(obj15, obj, null);
			}
		}

		private void UpdateTwoWay_2_IsOn()
		{
			if (initialized && dataRoot != null && dataRoot.Settings != null)
			{
				dataRoot.Settings.ForceMultiRebuild = obj2.IsOn;
			}
		}

		private void UpdateTwoWay_3_IsOn()
		{
			if (initialized && dataRoot != null && dataRoot.Settings != null)
			{
				dataRoot.Settings.ComplexFolders = obj3.IsOn;
			}
		}

		private void UpdateTwoWay_6_IsOn()
		{
			if (initialized && dataRoot != null && dataRoot.Settings != null)
			{
				dataRoot.Settings.TrimXci = obj6.IsOn;
			}
		}

		private void UpdateTwoWay_7_IsOn()
		{
			if (initialized && dataRoot != null && dataRoot.Settings != null)
			{
				dataRoot.Settings.DeleteSourceOnSuccess = obj7.IsOn;
			}
		}

		private void UpdateTwoWay_8_Value()
		{
			if (initialized && dataRoot != null && dataRoot.Settings != null)
			{
				dataRoot.Settings.UsedCores = (int)obj8.Value;
			}
		}

		private void UpdateTwoWay_9_Value()
		{
			if (initialized && dataRoot != null && dataRoot.Settings != null)
			{
				dataRoot.Settings.ConcurrentTasks = (int)obj9.Value;
			}
		}

		private void UpdateTwoWay_10_Value()
		{
			if (initialized && dataRoot != null && dataRoot.Settings != null)
			{
				dataRoot.Settings.KeyGeneration = (int)obj10.Value;
			}
		}

		private void UpdateTwoWay_12_Text()
		{
			if (initialized && dataRoot != null && dataRoot.Settings != null)
			{
				dataRoot.Settings.OutputFolder = obj12.Text;
			}
		}

		private void UpdateTwoWay_15_Text()
		{
			if (initialized && dataRoot != null && dataRoot.Settings != null)
			{
				dataRoot.Settings.KeysVersion = obj15.Text;
			}
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private Border LanguageSelectionPanel;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private ItemsControl LanguageItemsControl;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private ToggleSwitch TrimXciToggle;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private ComboBox CompressionCombo;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private TextBox OutputFolderBox;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private bool _contentLoaded;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private ISettingsPage_Bindings Bindings;

	public int MaxCores => Environment.ProcessorCount;

	public AppSettings Settings => App.Settings.Current;

	public Visibility KeysSelectedVisibility => string.IsNullOrEmpty(App.Settings.Current.KeysPath) ? Visibility.Collapsed : Visibility.Visible;

	public SettingsPage()
	{
		InitializeComponent();
		base.NavigationCacheMode = NavigationCacheMode.Required;
		int num = App.Settings.Current.CompressionLevel;
		if (num == 0)
		{
			num = 18;
		}
		if (num <= 3)
		{
			CompressionCombo.SelectedIndex = 0;
		}
		else if (num <= 10)
		{
			CompressionCombo.SelectedIndex = 1;
		}
		else if (num <= 18)
		{
			CompressionCombo.SelectedIndex = 2;
		}
		else
		{
			CompressionCombo.SelectedIndex = 3;
		}
		InitializeLanguages();
	}

	private void InitializeLanguages()
	{
		(string, string[])[] array = new(string, string[])[12]
		{
			("Русский", new string[2] { "ru", "ru-RU" }),
			("English", new string[3] { "en", "en-US", "en-GB" }),
			("Japanese", new string[3] { "ja", "ja-JP", "Japanese" }),
			("Spanish", new string[4] { "es", "es-ES", "es-MX", "Spanish" }),
			("French", new string[4] { "fr", "fr-FR", "fr-CA", "French" }),
			("German", new string[3] { "de", "de-DE", "German" }),
			("Italian", new string[3] { "it", "it-IT", "Italian" }),
			("Dutch", new string[3] { "nl", "nl-NL", "Dutch" }),
			("Portuguese", new string[4] { "pt", "pt-BR", "pt-PT", "Portuguese" }),
			("Korean", new string[3] { "ko", "ko-KR", "Korean" }),
			("Chinese (Simp)", new string[2] { "zh-Hans", "zh-CN" }),
			("Chinese (Trad)", new string[2] { "zh-Hant", "zh-TW" })
		};
		(string, string[])[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			(string Name, string[] Codes) lang = array2[i];
			CheckBox checkBox = new CheckBox
			{
				Content = lang.Name,
				Margin = new Thickness(0.0, 0.0, 16.0, 8.0)
			};
			bool value = false;
			if (Settings.KeepLanguages != null)
			{
				string[] item = lang.Codes;
				foreach (string item2 in item)
				{
					if (Settings.KeepLanguages.Contains(item2))
					{
						value = true;
						break;
					}
				}
			}
			checkBox.IsChecked = value;
			checkBox.Checked += async delegate
			{
				if (Settings.KeepLanguages == null)
				{
					Settings.KeepLanguages = new List<string>();
				}
				string[] item3 = lang.Codes;
				foreach (string code in item3)
				{
					if (!Settings.KeepLanguages.Contains(code))
					{
						Settings.KeepLanguages.Add(code);
					}
				}
				await App.Settings.SaveAsync();
			};
			checkBox.Unchecked += async delegate
			{
				if (Settings.KeepLanguages != null)
				{
					string[] item3 = lang.Codes;
					foreach (string code in item3)
					{
						Settings.KeepLanguages.Remove(code);
					}
					await App.Settings.SaveAsync();
				}
			};
			LanguageItemsControl.Items.Add(checkBox);
		}
	}

	private async void SelectKeysButton_Click(object sender, RoutedEventArgs e)
	{
		try
		{
			FileOpenPicker picker = new FileOpenPicker
			{
				ViewMode = PickerViewMode.List,
				SuggestedStartLocation = PickerLocationId.ComputerFolder,
				FileTypeFilter = { ".keys", ".txt", "*" }
			};
			nint hwnd = WindowNative.GetWindowHandle(App.MainWindow);
			InitializeWithWindow.Initialize(picker, hwnd);
			StorageFile file = await picker.PickSingleFileAsync();
			if (!(file != null))
			{
				return;
			}
			string toolsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");
			if (!Directory.Exists(toolsDir))
			{
				string devToolsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "tools");
				if (Directory.Exists(devToolsDir))
				{
					toolsDir = devToolsDir;
				}
				else
				{
					Directory.CreateDirectory(toolsDir);
				}
			}
			string targetTxt = Path.Combine(toolsDir, "keys.txt");
			string targetProd = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "prod.keys");
			try
			{
				File.Copy(file.Path, targetTxt, overwrite: true);
			}
			catch
			{
			}
			try
			{
				File.Copy(file.Path, targetProd, overwrite: true);
			}
			catch
			{
			}
			App.Settings.Current.KeysPath = targetTxt;
			await App.Settings.SaveAsync();
			App.Keys.LoadKeys(targetTxt);
			App.Logger.Log("Файл ключей скопирован и применен: " + targetTxt, LogLevel.Success);
			Bindings.Update();
		}
		catch (Exception ex)
		{
			Exception ex2 = ex;
			App.Logger.Log("Ошибка выбора файла ключей: " + ex2.Message, LogLevel.Error);
		}
	}

	private async void SelectOutputFolder_Click(object sender, RoutedEventArgs e)
	{
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
			if (folder != null)
			{
				App.Settings.Current.OutputFolder = folder.Path;
				OutputFolderBox.Text = folder.Path;
				await App.Settings.SaveAsync();
				App.Logger.Log("Выходная папка: " + folder.Path);
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
				Text = (App.Settings.Current.OutputFolder ?? ""),
				Width = 400.0
			});
			if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(textBox.Text))
			{
				App.Settings.Current.OutputFolder = textBox.Text.Trim();
				OutputFolderBox.Text = textBox.Text.Trim();
				await App.Settings.SaveAsync();
				App.Logger.Log("Выходная папка (вручную): " + textBox.Text.Trim());
			}
		}
	}

	private async void CompressionCombo_Changed(object sender, SelectionChangedEventArgs e)
	{
		ComboBoxItem item = default(ComboBoxItem);
		int num;
		if (sender is ComboBox { SelectedItem: var selectedItem })
		{
			item = selectedItem as ComboBoxItem;
			num = (((object)item != null) ? 1 : 0);
		}
		else
		{
			num = 0;
		}
		if (num != 0 && int.TryParse(item.Tag?.ToString(), out var level))
		{
			App.Settings.Current.CompressionLevel = level;
			await App.Settings.SaveAsync();
		}
	}

	private async void Setting_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
	{
		await App.Settings.SaveAsync();
	}

	private async void Toggle_Changed(object sender, RoutedEventArgs e)
	{
		await App.Settings.SaveAsync();
	}

	private void OutputFolderBox_DragOver(object sender, DragEventArgs e)
	{
		e.AcceptedOperation = DataPackageOperation.Copy;
		e.DragUIOverride.Caption = "Выбрать как выходную папку";
		e.DragUIOverride.IsCaptionVisible = true;
		e.DragUIOverride.IsContentVisible = true;
	}

	private async void OutputFolderBox_Drop(object sender, DragEventArgs e)
	{
		if (!e.DataView.Contains(StandardDataFormats.StorageItems))
		{
			return;
		}
		IReadOnlyList<IStorageItem> items = await e.DataView.GetStorageItemsAsync();
		if (items.Count > 0)
		{
			IStorageItem item = items[0];
			string path = item.Path;
			if (File.Exists(path))
			{
				path = Path.GetDirectoryName(path) ?? path;
			}
			if (Directory.Exists(path))
			{
				App.Settings.Current.OutputFolder = path;
				OutputFolderBox.Text = path;
				await App.Settings.SaveAsync();
				App.Logger.Log("Выходная папка установлена перетягиванием: " + path, LogLevel.Success);
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
			Uri resourceLocator = new Uri("ms-appx:///Views/SettingsPage.xaml");
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
			ToggleSwitch toggleSwitch3 = target.As<ToggleSwitch>();
			toggleSwitch3.Toggled += Toggle_Changed;
			break;
		}
		case 3:
		{
			ToggleSwitch toggleSwitch2 = target.As<ToggleSwitch>();
			toggleSwitch2.Toggled += Toggle_Changed;
			break;
		}
		case 4:
			LanguageSelectionPanel = target.As<Border>();
			break;
		case 5:
			LanguageItemsControl = target.As<ItemsControl>();
			break;
		case 6:
			TrimXciToggle = target.As<ToggleSwitch>();
			TrimXciToggle.Toggled += Toggle_Changed;
			break;
		case 7:
		{
			ToggleSwitch toggleSwitch = target.As<ToggleSwitch>();
			toggleSwitch.Toggled += Toggle_Changed;
			break;
		}
		case 8:
		{
			NumberBox numberBox3 = target.As<NumberBox>();
			numberBox3.ValueChanged += Setting_ValueChanged;
			break;
		}
		case 9:
		{
			NumberBox numberBox2 = target.As<NumberBox>();
			numberBox2.ValueChanged += Setting_ValueChanged;
			break;
		}
		case 10:
		{
			NumberBox numberBox = target.As<NumberBox>();
			numberBox.ValueChanged += Setting_ValueChanged;
			break;
		}
		case 11:
			CompressionCombo = target.As<ComboBox>();
			CompressionCombo.SelectionChanged += CompressionCombo_Changed;
			break;
		case 12:
			OutputFolderBox = target.As<TextBox>();
			OutputFolderBox.DragOver += OutputFolderBox_DragOver;
			OutputFolderBox.Drop += OutputFolderBox_Drop;
			break;
		case 13:
		{
			Button button2 = target.As<Button>();
			button2.Click += SelectOutputFolder_Click;
			break;
		}
		case 16:
		{
			Button button = target.As<Button>();
			button.Click += SelectKeysButton_Click;
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
		if (connectionId == 1)
		{
			Page page = (Page)target;
			SettingsPage_obj1_Bindings settingsPage_obj1_Bindings = new SettingsPage_obj1_Bindings();
			result = settingsPage_obj1_Bindings;
			settingsPage_obj1_Bindings.SetDataRoot(this);
			Bindings = settingsPage_obj1_Bindings;
			page.Loading += settingsPage_obj1_Bindings.Loading;
			XamlBindingHelper.SetDataTemplateComponent(page, settingsPage_obj1_Bindings);
		}
		return result;
	}
}
