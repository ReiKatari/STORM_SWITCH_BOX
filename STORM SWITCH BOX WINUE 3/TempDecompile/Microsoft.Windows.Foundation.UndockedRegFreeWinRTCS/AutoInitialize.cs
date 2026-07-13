using System;
using System.Runtime.CompilerServices;

namespace Microsoft.Windows.Foundation.UndockedRegFreeWinRTCS;

internal class AutoInitialize
{
	[ModuleInitializer]
	internal static void AccessWindowsAppSDK()
	{
		Environment.SetEnvironmentVariable("MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY", AppContext.BaseDirectory);
		NativeMethods.WindowsAppRuntime_EnsureIsLoaded();
	}
}
