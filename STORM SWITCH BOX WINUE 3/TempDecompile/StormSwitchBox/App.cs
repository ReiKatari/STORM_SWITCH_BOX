#define DEBUG
using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Markup;
using StormSwitchBox.Models;
using StormSwitchBox.Services;
using StormSwitchBox.StormSwitchBox_XamlTypeInfo;
using StormSwitchBox.ViewModels;

namespace StormSwitchBox;

public class App : Application, IXamlMetadataProvider
{
	private static TasksViewModel? _tasksVM;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private bool _contentLoaded;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private XamlMetaDataProvider __appProvider;

	public static Window? MainWindow { get; private set; }

	public static DispatcherQueue? MainDispatcher { get; private set; }

	public static SettingsService Settings { get; } = new SettingsService();

	public static LogService Logger { get; } = new LogService();

	public static KeysService Keys { get; } = new KeysService();

	public static SwitchFormatService SwitchFormat { get; } = new SwitchFormatService(Keys);

	public static NszCompressionService NszCompression { get; } = new NszCompressionService(SwitchFormat);

	public static MultiContentService MultiContent { get; } = new MultiContentService(Keys);

	public static HardPatchEngine HardPatch { get; } = new HardPatchEngine(Keys);

	public static TitleDbService TitleDb { get; } = new TitleDbService();

	public static TicketHarvesterService TicketHarvester { get; } = new TicketHarvesterService();

	public static NativePackEngine NativePack { get; } = new NativePackEngine(Keys);

	public static TasksViewModel TasksVM => _tasksVM ?? (_tasksVM = new TasksViewModel());

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	[DebuggerNonUserCode]
	private XamlMetaDataProvider _AppProvider
	{
		get
		{
			if (__appProvider == null)
			{
				__appProvider = new XamlMetaDataProvider();
			}
			return __appProvider;
		}
	}

	public App()
	{
		InitializeComponent();
		base.UnhandledException += App_UnhandledException;
	}

	protected override async void OnLaunched(LaunchActivatedEventArgs args)
	{
		MainDispatcher = DispatcherQueue.GetForCurrentThread();
		await Settings.LoadAsync();
		Logger.Log("Приложение запущено. Настройки загружены.");
		string keysPath = Settings.Current.KeysPath;
		if (string.IsNullOrEmpty(keysPath) || !File.Exists(keysPath))
		{
			keysPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "prod.keys");
			if (!File.Exists(keysPath))
			{
				keysPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "tools", "keys.txt");
			}
		}
		if (File.Exists(keysPath))
		{
			Settings.Current.KeysPath = keysPath;
			Keys.LoadKeys(keysPath);
		}
		else
		{
			Logger.Log("Файл ключей не найден. Укажите его в параметрах.", LogLevel.Error);
		}
		MainWindow = new MainWindow();
		MainWindow.Activate();
	}

	private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
	{
		Logger.Log("CRASH: " + e.Exception.Message + "\n" + e.Exception.StackTrace, LogLevel.Error);
		File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), e.Exception.ToString());
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	[DebuggerNonUserCode]
	public void InitializeComponent()
	{
		if (_contentLoaded)
		{
			return;
		}
		_contentLoaded = true;
		Uri resourceLocator = new Uri("ms-appx:///App.xaml");
		Application.LoadComponent(this, resourceLocator);
		base.DebugSettings.BindingFailed += delegate(object sender, BindingFailedEventArgs args)
		{
			Debug.WriteLine(args.Message);
		};
		base.DebugSettings.XamlResourceReferenceFailed += delegate(DebugSettings sender, XamlResourceReferenceFailedEventArgs args)
		{
			Debug.WriteLine(args.Message);
		};
		base.UnhandledException += delegate
		{
			if (Debugger.IsAttached)
			{
				Debugger.Break();
			}
		};
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	[DebuggerNonUserCode]
	public IXamlType GetXamlType(Type type)
	{
		return _AppProvider.GetXamlType(type);
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	[DebuggerNonUserCode]
	public IXamlType GetXamlType(string fullName)
	{
		return _AppProvider.GetXamlType(fullName);
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	[DebuggerNonUserCode]
	public XmlnsDefinition[] GetXmlnsDefinitions()
	{
		return _AppProvider.GetXmlnsDefinitions();
	}
}
