#define DEBUG
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using StormSwitchBox.Models;
using StormSwitchBox.Services;
using WinRT;
using WinRT.Interop;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace StormSwitchBox.Views;

public sealed class CatalogPage : Page, IComponentConnector
{
	private ObservableCollection<CatalogItem> _allCatalogItems = new ObservableCollection<CatalogItem>();

	private CatalogScannerService _scannerService;

	private CancellationTokenSource? _scanCts;

	private CatalogItem? _selectedCatalogItem;

	private CancellationTokenSource? _searchCts;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private GridView CatalogGridView;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private Grid DetailsOverlay;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private Grid FullscreenImageOverlay;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private Image FullscreenImage;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private StackPanel ScreenshotsPanel;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private GridView ScreenshotsGridView;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private TextBlock DetailIntro;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private TextBlock DetailDescription;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private TextBlock DetailLangs;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private TextBlock DetailSaveSize;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private TextBlock DetailVideoCap;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private TextBlock DetailRating;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private Button SaveCoverButton;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private TextBlock DetailSize;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private TextBlock DetailDlcCount;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private TextBlock DetailRegions;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private TextBlock DetailTitleId;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private TextBlock DetailVersionCode;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private TextBlock DetailDisplayVersion;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private TextBlock DetailReleaseDate;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private TextBlock DetailCategory;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private TextBlock DetailDeveloper;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private TextBlock DetailPublisher;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private Image DetailCover;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private TextBlock DetailTitle;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private Button UpdateDbButton;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private TextBlock ScanStatusText;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private ProgressRing ScanProgress;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private FontIcon UpdateDbIcon;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private TextBlock UpdateDbTextBlock;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private Button BrowseButton;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private TextBox SearchBox;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private ListView FoldersListView;

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	private bool _contentLoaded;

	public CatalogPage()
	{
		InitializeComponent();
		base.NavigationCacheMode = NavigationCacheMode.Required;
		_scannerService = new CatalogScannerService(App.Keys);
		CatalogGridView.ItemsSource = _allCatalogItems;
		FoldersListView.ItemsSource = App.Settings.Current.CatalogFolders;
		base.Loaded += CatalogPage_Loaded;
	}

	private void CatalogPage_Loaded(object sender, RoutedEventArgs e)
	{
		if (_allCatalogItems.Count == 0 && App.Settings.Current.CatalogFolders.Any())
		{
			ScanAllFolders_Click(this, new RoutedEventArgs());
		}
		UpdateDbStatusUI();
	}

	private void UpdateDbStatusUI()
	{
		if (App.TitleDb.IsDatabaseFresh())
		{
			UpdateDbTextBlock.Text = "Данные TitleDB обновлены";
			UpdateDbIcon.Foreground = new SolidColorBrush(Colors.LightGreen);
		}
		else
		{
			UpdateDbTextBlock.Text = "Требуется обновление TitleDB";
			UpdateDbIcon.Foreground = new SolidColorBrush(Colors.Orange);
		}
	}

	private async void AddFolder_Click(object sender, RoutedEventArgs e)
	{
		FolderPicker folderPicker = new FolderPicker
		{
			SuggestedStartLocation = PickerLocationId.Desktop,
			FileTypeFilter = { "*" }
		};
		nint hwnd = WindowNative.GetWindowHandle(App.MainWindow);
		InitializeWithWindow.Initialize(folderPicker, hwnd);
		StorageFolder folder = await folderPicker.PickSingleFolderAsync();
		if (folder != null && !App.Settings.Current.CatalogFolders.Contains(folder.Path))
		{
			App.Settings.Current.CatalogFolders.Add(folder.Path);
			FoldersListView.ItemsSource = null;
			FoldersListView.ItemsSource = App.Settings.Current.CatalogFolders;
			App.Settings.SaveAsync();
		}
	}

	private void RemoveFolder_Click(object sender, RoutedEventArgs e)
	{
		if (FoldersListView.SelectedItem is string item)
		{
			App.Settings.Current.CatalogFolders.Remove(item);
			FoldersListView.ItemsSource = null;
			FoldersListView.ItemsSource = App.Settings.Current.CatalogFolders;
			App.Settings.SaveAsync();
		}
	}

	private void ScanAllFolders_Click(object sender, RoutedEventArgs e)
	{
		StartScan();
	}

	private async void UpdateDbButton_Click(object sender, RoutedEventArgs e)
	{
		UpdateDbButton.IsEnabled = false;
		ScanProgress.IsActive = true;
		ScanStatusText.Text = "Скачивание TitleDB...";
		Progress<int> progress = new Progress<int>(delegate(int percent)
		{
			App.MainDispatcher?.TryEnqueue(delegate
			{
				ScanStatusText.Text = $"Скачивание TitleDB... {percent}%";
			});
		});
		bool success = await App.TitleDb.UpdateDatabaseAsync(progress);
		App.MainDispatcher?.TryEnqueue(delegate
		{
			ScanStatusText.Text = (success ? "База TitleDB успешно обновлена!" : "Ошибка скачивания базы!");
			ScanProgress.IsActive = false;
			UpdateDbButton.IsEnabled = true;
			UpdateDbStatusUI();
		});
	}

	private async void StartScan()
	{
		_scanCts?.Cancel();
		_scanCts = new CancellationTokenSource();
		_allCatalogItems.Clear();
		SearchBox.Text = "";
		BrowseButton.IsEnabled = false;
		ScanProgress.IsActive = true;
		List<string> folders = App.Settings.Current.CatalogFolders.ToList();
		ScanStatusText.Text = $"Подготовка к сканированию ({folders.Count} папок)...";
		try
		{
			foreach (string path in folders)
			{
				if (Directory.Exists(path))
				{
					ScanStatusText.Text = "Сканирование: " + Path.GetFileName(path) + "...";
					await _scannerService.ScanDirectoryAsync(path, _allCatalogItems, _scanCts.Token);
				}
			}
			ScanStatusText.Text = $"Найдено игр: {_allCatalogItems.Count}";
		}
		catch (OperationCanceledException)
		{
			ScanStatusText.Text = "Сканирование отменено.";
		}
		catch (Exception ex2)
		{
			Exception ex3 = ex2;
			ScanStatusText.Text = "Ошибка: " + ex3.Message;
		}
		finally
		{
			BrowseButton.IsEnabled = true;
			ScanProgress.IsActive = false;
		}
	}

	private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
	{
		string query = SearchBox.Text.Trim();
		_searchCts?.Cancel();
		_searchCts = new CancellationTokenSource();
		CancellationToken token = _searchCts.Token;
		if (string.IsNullOrWhiteSpace(query))
		{
			CatalogGridView.ItemsSource = _allCatalogItems;
			return;
		}
		try
		{
			await Task.Delay(300, token);
			if (token.IsCancellationRequested)
			{
				return;
			}
			List<CatalogItem> filtered = await Task.Run(delegate
			{
				List<CatalogItem> source = _allCatalogItems.ToList();
				List<CatalogItem> list = source.Where((CatalogItem i) => (i.TitleName != null && i.TitleName.Contains(query, StringComparison.OrdinalIgnoreCase)) || (i.TitleId != null && i.TitleId.Contains(query, StringComparison.OrdinalIgnoreCase)) || (i.Publisher != null && i.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase))).ToList();
				IEnumerable<TitleDbEntry> enumerable = App.TitleDb.SearchTitles(query);
				foreach (TitleDbEntry dbEntry in enumerable)
				{
					if (!list.Any((CatalogItem i) => i.TitleName == dbEntry.Name || i.TitleId == dbEntry.Id))
					{
						CatalogItem catalogItem = new CatalogItem
						{
							TitleName = (dbEntry.Name ?? "Unknown Game"),
							TitleId = (dbEntry.Id ?? "0000000000000000"),
							Publisher = (dbEntry.Publisher ?? "Unknown"),
							Description = (dbEntry.Description ?? "Нет описания"),
							FileSize = "TitleDB (Онлайн)",
							IsOnlineOnly = true,
							IsLoading = false
						};
						if (!string.IsNullOrEmpty(dbEntry.Version))
						{
							catalogItem.Version = dbEntry.Version;
						}
						App.TitleDb.EnrichCatalogItem(catalogItem);
						list.Add(catalogItem);
					}
				}
				return list;
			}, token);
			if (!token.IsCancellationRequested)
			{
				CatalogGridView.ItemsSource = new ObservableCollection<CatalogItem>(filtered);
			}
		}
		catch (TaskCanceledException)
		{
		}
		catch (Exception ex2)
		{
			Debug.WriteLine("Search error: " + ex2.Message);
		}
	}

	private void Grid_PointerEntered(object sender, PointerRoutedEventArgs e)
	{
		if (sender is Grid { RenderTransform: ScaleTransform renderTransform })
		{
			renderTransform.ScaleX = 1.03;
			renderTransform.ScaleY = 1.03;
		}
	}

	private void Grid_PointerExited(object sender, PointerRoutedEventArgs e)
	{
		if (sender is Grid { RenderTransform: ScaleTransform renderTransform })
		{
			renderTransform.ScaleX = 1.0;
			renderTransform.ScaleY = 1.0;
		}
	}

	private void CatalogGridView_ItemClick(object sender, ItemClickEventArgs e)
	{
		if (e.ClickedItem is CatalogItem catalogItem)
		{
			_selectedCatalogItem = catalogItem;
			DetailTitle.Text = catalogItem.TitleName ?? "Unknown";
			DetailPublisher.Text = catalogItem.Publisher ?? "Unknown";
			DetailDeveloper.Text = ((!string.IsNullOrEmpty(catalogItem.Developer)) ? catalogItem.Developer : "Unknown");
			DetailCategory.Text = ((!string.IsNullOrEmpty(catalogItem.Category)) ? catalogItem.Category : "N/A");
			DetailReleaseDate.Text = ((!string.IsNullOrEmpty(catalogItem.ReleaseDate)) ? catalogItem.ReleaseDate : "N/A");
			DetailDisplayVersion.Text = catalogItem.Version ?? "v0";
			DetailVersionCode.Text = ((!string.IsNullOrEmpty(catalogItem.VersionCode)) ? catalogItem.VersionCode : "0");
			DetailRegions.Text = catalogItem.Regions ?? "N/A";
			DetailDlcCount.Text = catalogItem.DlcCount.ToString();
			DetailTitleId.Text = catalogItem.TitleId ?? "0000000000000000";
			DetailSize.Text = catalogItem.FileSize ?? "0 MB";
			DetailCover.Source = catalogItem.CoverImage;
			DetailRating.Text = ((!string.IsNullOrEmpty(catalogItem.RatingAge)) ? catalogItem.RatingAge : "N/A");
			DetailVideoCap.Text = catalogItem.VideoCapture ?? "N/A";
			DetailSaveSize.Text = catalogItem.SaveDataSize ?? "N/A";
			DetailLangs.Text = catalogItem.SupportedLanguages ?? "N/A";
			DetailIntro.Text = catalogItem.Intro ?? "";
			DetailIntro.Visibility = (string.IsNullOrEmpty(catalogItem.Intro) ? Visibility.Collapsed : Visibility.Visible);
			DetailDescription.Text = catalogItem.Description ?? "";
			if (catalogItem.HasScreenshots)
			{
				ScreenshotsPanel.Visibility = Visibility.Visible;
				ScreenshotsGridView.ItemsSource = catalogItem.Screenshots;
			}
			else
			{
				ScreenshotsPanel.Visibility = Visibility.Collapsed;
				ScreenshotsGridView.ItemsSource = null;
			}
			DetailsOverlay.Visibility = Visibility.Visible;
		}
	}

	private void CopyDisplayVersion_Click(object sender, RoutedEventArgs e)
	{
		DataPackage dataPackage = new DataPackage();
		dataPackage.SetText(DetailDisplayVersion.Text);
		Clipboard.SetContent(dataPackage);
	}

	private void CopyVersionCode_Click(object sender, RoutedEventArgs e)
	{
		DataPackage dataPackage = new DataPackage();
		dataPackage.SetText(DetailVersionCode.Text);
		Clipboard.SetContent(dataPackage);
	}

	private void CopyTitleId_Click(object sender, RoutedEventArgs e)
	{
		DataPackage dataPackage = new DataPackage();
		dataPackage.SetText(DetailTitleId.Text);
		Clipboard.SetContent(dataPackage);
	}

	private void CardCopyVersion_Click(object sender, RoutedEventArgs e)
	{
		if ((sender as FrameworkElement)?.DataContext is CatalogItem catalogItem)
		{
			DataPackage dataPackage = new DataPackage();
			dataPackage.SetText(catalogItem.Version);
			Clipboard.SetContent(dataPackage);
		}
	}

	private void CardCopyVersionCode_Click(object sender, RoutedEventArgs e)
	{
		if ((sender as FrameworkElement)?.DataContext is CatalogItem catalogItem)
		{
			DataPackage dataPackage = new DataPackage();
			dataPackage.SetText(catalogItem.VersionCode);
			Clipboard.SetContent(dataPackage);
		}
	}

	private void CardCopyTitleId_Click(object sender, RoutedEventArgs e)
	{
		if ((sender as FrameworkElement)?.DataContext is CatalogItem catalogItem)
		{
			DataPackage dataPackage = new DataPackage();
			dataPackage.SetText(catalogItem.TitleId);
			Clipboard.SetContent(dataPackage);
		}
	}

	private void CardCopyUpdateVersion_Click(object sender, RoutedEventArgs e)
	{
		if ((sender as FrameworkElement)?.DataContext is CatalogItem catalogItem)
		{
			DataPackage dataPackage = new DataPackage();
			dataPackage.SetText((catalogItem.UpdateVersionCode != "Неизвестно") ? catalogItem.UpdateVersionCode : catalogItem.UpdateVersion);
			Clipboard.SetContent(dataPackage);
		}
	}

	private void CardCopySize_Click(object sender, RoutedEventArgs e)
	{
		if ((sender as FrameworkElement)?.DataContext is CatalogItem catalogItem)
		{
			DataPackage dataPackage = new DataPackage();
			dataPackage.SetText(catalogItem.FileSize);
			Clipboard.SetContent(dataPackage);
		}
	}

	private void CardCopyReleaseDate_Click(object sender, RoutedEventArgs e)
	{
		if ((sender as FrameworkElement)?.DataContext is CatalogItem catalogItem && !string.IsNullOrEmpty(catalogItem.ReleaseDate))
		{
			DataPackage dataPackage = new DataPackage();
			dataPackage.SetText(catalogItem.ReleaseDate);
			Clipboard.SetContent(dataPackage);
		}
	}

	private void DetailsOverlay_PointerPressed(object sender, PointerRoutedEventArgs e)
	{
		DetailsOverlay.Visibility = Visibility.Collapsed;
	}

	private void DetailsContent_PointerPressed(object sender, PointerRoutedEventArgs e)
	{
		e.Handled = true;
	}

	private void CloseDetails_Click(object sender, RoutedEventArgs e)
	{
		DetailsOverlay.Visibility = Visibility.Collapsed;
	}

	private async void SaveCoverButton_Click(object sender, RoutedEventArgs e)
	{
		if (_selectedCatalogItem == null || _selectedCatalogItem.CoverBytes == null || _selectedCatalogItem.CoverBytes.Length == 0)
		{
			ContentDialog dialog = new ContentDialog
			{
				Title = "Сохранение обложки",
				Content = "Обложка отсутствует или еще не загружена.",
				CloseButtonText = "ОК",
				XamlRoot = base.XamlRoot
			};
			await dialog.ShowAsync();
			return;
		}
		try
		{
			FileSavePicker savePicker = new FileSavePicker
			{
				SuggestedStartLocation = PickerLocationId.PicturesLibrary,
				FileTypeChoices = { 
				{
					"PNG Image",
					(IList<string>)new List<string> { ".png" }
				} }
			};
			string cleanName = _selectedCatalogItem.TitleName;
			string fileName = _selectedCatalogItem.FileName;
			if (!string.IsNullOrEmpty(fileName))
			{
				int braceIndex = fileName.IndexOf('[');
				cleanName = ((braceIndex <= 0) ? Path.GetFileNameWithoutExtension(fileName) : fileName.Substring(0, braceIndex).Trim());
			}
			string safeName = string.Join("_", cleanName.Split(Path.GetInvalidFileNameChars()));
			savePicker.SuggestedFileName = safeName;
			nint hwnd = WindowNative.GetWindowHandle(App.MainWindow);
			InitializeWithWindow.Initialize(savePicker, hwnd);
			StorageFile file = await savePicker.PickSaveFileAsync();
			if (file != null)
			{
				await FileIO.WriteBytesAsync(file, _selectedCatalogItem.CoverBytes);
				ContentDialog successDialog = new ContentDialog
				{
					Title = "Успех",
					Content = "Обложка успешно сохранена!",
					CloseButtonText = "ОК",
					XamlRoot = base.XamlRoot
				};
				await successDialog.ShowAsync();
			}
		}
		catch (Exception ex)
		{
			ContentDialog errorDialog = new ContentDialog
			{
				Title = "Ошибка сохранения",
				Content = "Не удалось сохранить файл: " + ex.Message,
				CloseButtonText = "ОК",
				XamlRoot = base.XamlRoot
			};
			await errorDialog.ShowAsync();
		}
	}

	private void ScreenshotsGridView_ItemClick(object sender, ItemClickEventArgs e)
	{
		if (e.ClickedItem is BitmapImage source)
		{
			FullscreenImage.Source = source;
			FullscreenImageOverlay.Visibility = Visibility.Visible;
		}
	}

	private void FullscreenImageOverlay_PointerPressed(object sender, PointerRoutedEventArgs e)
	{
		FullscreenImageOverlay.Visibility = Visibility.Collapsed;
	}

	private void CloseFullscreen_Click(object sender, RoutedEventArgs e)
	{
		FullscreenImageOverlay.Visibility = Visibility.Collapsed;
	}

	private void AcceptUpdate_Click(object sender, RoutedEventArgs e)
	{
		if ((sender as FrameworkElement)?.DataContext is CatalogItem catalogItem && !string.IsNullOrEmpty(catalogItem.TitleId) && !string.IsNullOrEmpty(catalogItem.UpdateVersionCode))
		{
			App.Settings.Current.VersionOverrides[catalogItem.TitleId] = catalogItem.UpdateVersionCode;
			App.Settings.SaveAsync();
			catalogItem.VersionCode = catalogItem.UpdateVersionCode;
			catalogItem.IsOutdated = false;
			catalogItem.UpdateVersion = "";
			catalogItem.UpdateVersionCode = "";
		}
	}

	[GeneratedCode("Microsoft.UI.Xaml.Markup.Compiler", " 3.0.0.2403")]
	[DebuggerNonUserCode]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("ms-appx:///Views/CatalogPage.xaml");
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
			CatalogGridView = target.As<GridView>();
			CatalogGridView.ItemClick += CatalogGridView_ItemClick;
			break;
		case 3:
			DetailsOverlay = target.As<Grid>();
			DetailsOverlay.PointerPressed += DetailsOverlay_PointerPressed;
			break;
		case 4:
			FullscreenImageOverlay = target.As<Grid>();
			FullscreenImageOverlay.PointerPressed += FullscreenImageOverlay_PointerPressed;
			break;
		case 5:
			FullscreenImage = target.As<Image>();
			break;
		case 6:
		{
			Button button15 = target.As<Button>();
			button15.Click += CloseFullscreen_Click;
			break;
		}
		case 7:
		{
			Border border = target.As<Border>();
			border.PointerPressed += DetailsContent_PointerPressed;
			break;
		}
		case 8:
			ScreenshotsPanel = target.As<StackPanel>();
			break;
		case 9:
			ScreenshotsGridView = target.As<GridView>();
			ScreenshotsGridView.ItemClick += ScreenshotsGridView_ItemClick;
			break;
		case 11:
			DetailIntro = target.As<TextBlock>();
			break;
		case 12:
			DetailDescription = target.As<TextBlock>();
			break;
		case 13:
			DetailLangs = target.As<TextBlock>();
			break;
		case 14:
			DetailSaveSize = target.As<TextBlock>();
			break;
		case 15:
			DetailVideoCap = target.As<TextBlock>();
			break;
		case 16:
			DetailRating = target.As<TextBlock>();
			break;
		case 17:
			SaveCoverButton = target.As<Button>();
			SaveCoverButton.Click += SaveCoverButton_Click;
			break;
		case 18:
			DetailSize = target.As<TextBlock>();
			break;
		case 19:
			DetailDlcCount = target.As<TextBlock>();
			break;
		case 20:
			DetailRegions = target.As<TextBlock>();
			break;
		case 21:
			DetailTitleId = target.As<TextBlock>();
			break;
		case 22:
		{
			Button button14 = target.As<Button>();
			button14.Click += CopyTitleId_Click;
			break;
		}
		case 23:
			DetailVersionCode = target.As<TextBlock>();
			break;
		case 24:
		{
			Button button13 = target.As<Button>();
			button13.Click += CopyVersionCode_Click;
			break;
		}
		case 25:
			DetailDisplayVersion = target.As<TextBlock>();
			break;
		case 26:
		{
			Button button12 = target.As<Button>();
			button12.Click += CopyDisplayVersion_Click;
			break;
		}
		case 27:
			DetailReleaseDate = target.As<TextBlock>();
			break;
		case 28:
			DetailCategory = target.As<TextBlock>();
			break;
		case 29:
			DetailDeveloper = target.As<TextBlock>();
			break;
		case 30:
			DetailPublisher = target.As<TextBlock>();
			break;
		case 32:
			DetailCover = target.As<Image>();
			break;
		case 33:
			DetailTitle = target.As<TextBlock>();
			break;
		case 34:
		{
			Button button11 = target.As<Button>();
			button11.Click += CloseDetails_Click;
			break;
		}
		case 36:
		{
			Grid grid = target.As<Grid>();
			grid.PointerEntered += Grid_PointerEntered;
			grid.PointerExited += Grid_PointerExited;
			break;
		}
		case 38:
		{
			Button button10 = target.As<Button>();
			button10.Click += CardCopyUpdateVersion_Click;
			break;
		}
		case 39:
		{
			Button button9 = target.As<Button>();
			button9.Click += AcceptUpdate_Click;
			break;
		}
		case 40:
		{
			Button button8 = target.As<Button>();
			button8.Click += CardCopySize_Click;
			break;
		}
		case 41:
		{
			Button button7 = target.As<Button>();
			button7.Click += CardCopyTitleId_Click;
			break;
		}
		case 42:
		{
			Button button6 = target.As<Button>();
			button6.Click += CardCopyReleaseDate_Click;
			break;
		}
		case 43:
		{
			Button button5 = target.As<Button>();
			button5.Click += CardCopyVersion_Click;
			break;
		}
		case 44:
		{
			Button button4 = target.As<Button>();
			button4.Click += CardCopyVersionCode_Click;
			break;
		}
		case 45:
			UpdateDbButton = target.As<Button>();
			UpdateDbButton.Click += UpdateDbButton_Click;
			break;
		case 46:
			ScanStatusText = target.As<TextBlock>();
			break;
		case 47:
			ScanProgress = target.As<ProgressRing>();
			break;
		case 48:
			UpdateDbIcon = target.As<FontIcon>();
			break;
		case 49:
			UpdateDbTextBlock = target.As<TextBlock>();
			break;
		case 50:
			BrowseButton = target.As<Button>();
			break;
		case 51:
			SearchBox = target.As<TextBox>();
			SearchBox.TextChanged += SearchBox_TextChanged;
			break;
		case 52:
			FoldersListView = target.As<ListView>();
			break;
		case 53:
		{
			Button button3 = target.As<Button>();
			button3.Click += ScanAllFolders_Click;
			break;
		}
		case 54:
		{
			Button button2 = target.As<Button>();
			button2.Click += AddFolder_Click;
			break;
		}
		case 55:
		{
			Button button = target.As<Button>();
			button.Click += RemoveFolder_Click;
			break;
		}
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
