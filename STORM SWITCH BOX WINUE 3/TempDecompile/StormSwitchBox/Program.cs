using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using WinRT;

namespace StormSwitchBox;

public static class Program
{
	[DllImport("Microsoft.ui.xaml.dll")]
	[DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
	private static extern void XamlCheckProcessRequirements();

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	[DebuggerNonUserCode]
	[STAThread]
	private static void Main(string[] args)
	{
		XamlCheckProcessRequirements();
		ComWrappersSupport.InitializeComWrappers();
		Application.Start(delegate
		{
			DispatcherQueueSynchronizationContext synchronizationContext = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
			SynchronizationContext.SetSynchronizationContext(synchronizationContext);
			new App();
		});
	}
}
