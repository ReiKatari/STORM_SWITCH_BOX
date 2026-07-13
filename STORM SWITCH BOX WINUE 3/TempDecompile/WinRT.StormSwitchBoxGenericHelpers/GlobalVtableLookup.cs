using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ABI.System;
using ABI.System.Collections;

namespace WinRT.StormSwitchBoxGenericHelpers;

internal static class GlobalVtableLookup
{
	[ModuleInitializer]
	internal static void InitializeGlobalVtableLookup()
	{
		ComWrappersSupport.RegisterTypeComInterfaceEntriesLookup(LookupVtableEntries);
		ComWrappersSupport.RegisterTypeRuntimeClassNameLookup(LookupRuntimeClassName);
	}

	private static ComWrappers.ComInterfaceEntry[] LookupVtableEntries(System.Type type)
	{
		string text = type.ToString();
		if (text == "System.Collections.Specialized.ReadOnlyList" || text == "System.Collections.Specialized.SingleItemReadOnlyList")
		{
			return new ComWrappers.ComInterfaceEntry[2]
			{
				new ComWrappers.ComInterfaceEntry
				{
					IID = IListMethods.IID,
					Vtable = IListMethods.AbiToProjectionVftablePtr
				},
				new ComWrappers.ComInterfaceEntry
				{
					IID = IEnumerableMethods.IID,
					Vtable = IEnumerableMethods.AbiToProjectionVftablePtr
				}
			};
		}
		switch (text)
		{
		default:
			if (!(text == "LibHac.FsSystem.PartitionFileSystem"))
			{
				return null;
			}
			goto case "System.Threading.CancellationTokenSource";
		case "System.Threading.CancellationTokenSource":
		case "System.IO.FileStream":
		case "LibHac.Fs.MemoryStorage":
			return new ComWrappers.ComInterfaceEntry[1]
			{
				new ComWrappers.ComInterfaceEntry
				{
					IID = IDisposableMethods.IID,
					Vtable = IDisposableMethods.AbiToProjectionVftablePtr
				}
			};
		}
	}

	private static string LookupRuntimeClassName(System.Type type)
	{
		string text = type.ToString();
		if (text == "System.Collections.Specialized.ReadOnlyList" || text == "System.Collections.Specialized.SingleItemReadOnlyList")
		{
			return "Microsoft.UI.Xaml.Interop.IBindableVector";
		}
		switch (text)
		{
		default:
			if (!(text == "LibHac.FsSystem.PartitionFileSystem"))
			{
				return null;
			}
			goto case "System.Threading.CancellationTokenSource";
		case "System.Threading.CancellationTokenSource":
		case "System.IO.FileStream":
		case "LibHac.Fs.MemoryStorage":
			return "Windows.Foundation.IClosable";
		}
	}
}
