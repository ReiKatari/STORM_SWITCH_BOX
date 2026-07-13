using System.Runtime.InteropServices;

namespace Microsoft.Windows.Foundation.UndockedRegFreeWinRTCS;

internal static class NativeMethods
{
	[DllImport("Microsoft.WindowsAppRuntime.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
	internal static extern int WindowsAppRuntime_EnsureIsLoaded();
}
