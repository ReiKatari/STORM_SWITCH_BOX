using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using StormSwitchBox.Models;
using StormSwitchBox.Services;
using StormSwitchBox.Views;
using WinRT;
using Windows.Graphics;

namespace StormSwitchBox;

public sealed class MainWindow : Window, IComponentConnector
{
	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private NavigationView MainNavigation;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private Frame ContentFrame;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private bool _contentLoaded;

	public MainWindow()
	{
		InitializeComponent();
		base.Title = "STORM SWITCH BOX 2.5";
		base.ExtendsContentIntoTitleBar = true;
		base.SystemBackdrop = new MicaBackdrop();
		RestoreWindowState();
		HistoryService.LoadHistoryAsync();
		MainNavigation.SelectedItem = MainNavigation.MenuItems[4];
	}

	private void RestoreWindowState()
	{
		AppSettings current = App.Settings.Current;
		AppWindow appWindow = base.AppWindow;
		if (current.WindowWidth > 100 && current.WindowHeight > 100)
		{
			appWindow.Resize(new SizeInt32(current.WindowWidth, current.WindowHeight));
		}
		if (current.WindowX >= 0 && current.WindowY >= 0)
		{
			appWindow.Move(new PointInt32(current.WindowX, current.WindowY));
		}
	}

	private void SaveWindowState()
	{
		AppSettings current = App.Settings.Current;
		AppWindow appWindow = base.AppWindow;
		current.WindowWidth = appWindow.Size.Width;
		current.WindowHeight = appWindow.Size.Height;
		current.WindowX = appWindow.Position.X;
		current.WindowY = appWindow.Position.Y;
		App.Settings.SaveAsync();
	}

	private void MainNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
	{
		if (args.IsSettingsSelected)
		{
			ContentFrame.Navigate(typeof(SettingsPage));
		}
		else
		{
			if (!(args.SelectedItemContainer != null))
			{
				return;
			}
			string text = args.SelectedItemContainer.Tag?.ToString();
			if (!string.IsNullOrEmpty(text))
			{
				if (text == "History")
				{
					ContentFrame.Navigate(typeof(HistoryPage));
				}
				else if (text == "Catalog")
				{
					ContentFrame.Navigate(typeof(CatalogPage));
				}
				else
				{
					ContentFrame.Navigate(typeof(TasksPage), text);
				}
			}
		}
	}

	private void Window_Closed(object sender, WindowEventArgs args)
	{
		SaveWindowState();
		Environment.Exit(0);
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	[DebuggerNonUserCode]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("ms-appx:///MainWindow.xaml");
			Application.LoadComponent(this, resourceLocator, ComponentResourceLocation.Application);
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	[DebuggerNonUserCode]
	public void Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
		{
			Window window = target.As<Window>();
			window.Closed += Window_Closed;
			break;
		}
		case 2:
			MainNavigation = target.As<NavigationView>();
			MainNavigation.SelectionChanged += MainNavigation_SelectionChanged;
			break;
		case 3:
			ContentFrame = target.As<Frame>();
			break;
		}
		_contentLoaded = true;
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	[DebuggerNonUserCode]
	public IComponentConnector GetBindingConnector(int connectionId, object target)
	{
		return null;
	}
}
