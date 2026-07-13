using System.Runtime.InteropServices;
using ABI.System.ComponentModel;

namespace WinRT.StormSwitchBoxVtableClasses;

internal sealed class StormSwitchBox_ViewModels_TasksViewModelWinRTTypeDetails : IWinRTExposedTypeDetails
{
	public ComWrappers.ComInterfaceEntry[] GetExposedInterfaces()
	{
		return new ComWrappers.ComInterfaceEntry[1]
		{
			new ComWrappers.ComInterfaceEntry
			{
				IID = INotifyPropertyChangedMethods.IID,
				Vtable = INotifyPropertyChangedMethods.AbiToProjectionVftablePtr
			}
		};
	}
}
