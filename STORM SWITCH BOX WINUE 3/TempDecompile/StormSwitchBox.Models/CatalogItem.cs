using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using WinRT;
using WinRT.StormSwitchBoxVtableClasses;

namespace StormSwitchBox.Models;

[WinRTRuntimeClassName("Microsoft.UI.Xaml.Data.INotifyPropertyChanged")]
[WinRTExposedType(typeof(StormSwitchBox_ViewModels_TasksViewModelWinRTTypeDetails))]
public class CatalogItem : ObservableObject
{
	[ObservableProperty]
	private string _filePath = "";

	[ObservableProperty]
	private string _fileName = "";

	[ObservableProperty]
	private string _titleName = "Unknown Game";

	[ObservableProperty]
	private string _titleId = "0000000000000000";

	[ObservableProperty]
	[NotifyPropertyChangedFor("RegionsVisibility")]
	private string _regions = "WW";

	[ObservableProperty]
	[NotifyPropertyChangedFor("DlcCountDisplay")]
	private int _dlcCount = 0;

	[ObservableProperty]
	private string _version = "v0";

	[ObservableProperty]
	private string _versionCode = "0";

	[ObservableProperty]
	private string _updateVersion = string.Empty;

	[ObservableProperty]
	private string _updateVersionCode = string.Empty;

	[ObservableProperty]
	private string _publisher = "Unknown";

	[ObservableProperty]
	private string _fileSize = "0 MB";

	[ObservableProperty]
	[JsonIgnore]
	private BitmapImage? _coverImage;

	[ObservableProperty]
	[JsonIgnore]
	private byte[]? _coverBytes;

	[ObservableProperty]
	private bool _isLoading = true;

	[ObservableProperty]
	private bool _hasError = false;

	[ObservableProperty]
	[NotifyPropertyChangedFor("AcceptButtonVisibility")]
	private bool _isOnlineOnly = false;

	[ObservableProperty]
	private string _errorMessage = "";

	[ObservableProperty]
	private string _supportedLanguages = "";

	[ObservableProperty]
	private string _ratingAge = "";

	[ObservableProperty]
	private string _videoCapture = "";

	[ObservableProperty]
	private string _saveDataSize = "";

	[ObservableProperty]
	private string _systemVersion = "";

	[ObservableProperty]
	private string _description = "Нет описания";

	[ObservableProperty]
	private string _intro = "";

	[ObservableProperty]
	private string _category = "";

	[ObservableProperty]
	private string _developer = "";

	[ObservableProperty]
	[NotifyPropertyChangedFor("ReleaseDateVisibility")]
	private string _releaseDate = "";

	[ObservableProperty]
	[NotifyPropertyChangedFor("OutdatedVisibility")]
	private bool _isOutdated = false;

	[ObservableProperty]
	private ObservableCollection<BitmapImage> _screenshots = new ObservableCollection<BitmapImage>();

	[ObservableProperty]
	private bool _hasScreenshots = false;

	public Visibility AcceptButtonVisibility => IsOnlineOnly ? Visibility.Collapsed : Visibility.Visible;

	public Visibility OutdatedVisibility => (!IsOutdated) ? Visibility.Collapsed : Visibility.Visible;

	public Visibility RegionsVisibility => (string.IsNullOrEmpty(Regions) || Regions == "N/A") ? Visibility.Collapsed : Visibility.Visible;

	public Visibility ReleaseDateVisibility => (string.IsNullOrEmpty(ReleaseDate) || ReleaseDate == "N/A") ? Visibility.Collapsed : Visibility.Visible;

	public string DlcCountDisplay => $"DLC: {DlcCount}";

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string FilePath
	{
		get
		{
			return _filePath;
		}
		[MemberNotNull("_filePath")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_filePath, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.FilePath);
				_filePath = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.FilePath);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string FileName
	{
		get
		{
			return _fileName;
		}
		[MemberNotNull("_fileName")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_fileName, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.FileName);
				_fileName = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.FileName);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string TitleName
	{
		get
		{
			return _titleName;
		}
		[MemberNotNull("_titleName")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_titleName, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.TitleName);
				_titleName = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.TitleName);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string TitleId
	{
		get
		{
			return _titleId;
		}
		[MemberNotNull("_titleId")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_titleId, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.TitleId);
				_titleId = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.TitleId);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string Regions
	{
		get
		{
			return _regions;
		}
		[MemberNotNull("_regions")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_regions, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Regions);
				_regions = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Regions);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.RegionsVisibility);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public int DlcCount
	{
		get
		{
			return _dlcCount;
		}
		set
		{
			if (!EqualityComparer<int>.Default.Equals(_dlcCount, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.DlcCount);
				_dlcCount = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DlcCount);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.DlcCountDisplay);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string Version
	{
		get
		{
			return _version;
		}
		[MemberNotNull("_version")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_version, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Version);
				_version = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Version);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string VersionCode
	{
		get
		{
			return _versionCode;
		}
		[MemberNotNull("_versionCode")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_versionCode, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.VersionCode);
				_versionCode = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.VersionCode);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string UpdateVersion
	{
		get
		{
			return _updateVersion;
		}
		[MemberNotNull("_updateVersion")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_updateVersion, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.UpdateVersion);
				_updateVersion = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.UpdateVersion);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string UpdateVersionCode
	{
		get
		{
			return _updateVersionCode;
		}
		[MemberNotNull("_updateVersionCode")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_updateVersionCode, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.UpdateVersionCode);
				_updateVersionCode = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.UpdateVersionCode);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string Publisher
	{
		get
		{
			return _publisher;
		}
		[MemberNotNull("_publisher")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_publisher, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Publisher);
				_publisher = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Publisher);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string FileSize
	{
		get
		{
			return _fileSize;
		}
		[MemberNotNull("_fileSize")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_fileSize, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.FileSize);
				_fileSize = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.FileSize);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public BitmapImage? CoverImage
	{
		get
		{
			return _coverImage;
		}
		set
		{
			if (!EqualityComparer<BitmapImage>.Default.Equals(_coverImage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CoverImage);
				_coverImage = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CoverImage);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public byte[]? CoverBytes
	{
		get
		{
			return _coverBytes;
		}
		set
		{
			if (!EqualityComparer<byte[]>.Default.Equals(_coverBytes, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CoverBytes);
				_coverBytes = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CoverBytes);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsLoading
	{
		get
		{
			return _isLoading;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isLoading, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsLoading);
				_isLoading = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsLoading);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public bool HasError
	{
		get
		{
			return _hasError;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_hasError, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HasError);
				_hasError = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HasError);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsOnlineOnly
	{
		get
		{
			return _isOnlineOnly;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isOnlineOnly, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsOnlineOnly);
				_isOnlineOnly = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsOnlineOnly);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.AcceptButtonVisibility);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string ErrorMessage
	{
		get
		{
			return _errorMessage;
		}
		[MemberNotNull("_errorMessage")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_errorMessage, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ErrorMessage);
				_errorMessage = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ErrorMessage);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string SupportedLanguages
	{
		get
		{
			return _supportedLanguages;
		}
		[MemberNotNull("_supportedLanguages")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_supportedLanguages, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SupportedLanguages);
				_supportedLanguages = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SupportedLanguages);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string RatingAge
	{
		get
		{
			return _ratingAge;
		}
		[MemberNotNull("_ratingAge")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_ratingAge, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.RatingAge);
				_ratingAge = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.RatingAge);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string VideoCapture
	{
		get
		{
			return _videoCapture;
		}
		[MemberNotNull("_videoCapture")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_videoCapture, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.VideoCapture);
				_videoCapture = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.VideoCapture);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string SaveDataSize
	{
		get
		{
			return _saveDataSize;
		}
		[MemberNotNull("_saveDataSize")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_saveDataSize, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SaveDataSize);
				_saveDataSize = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SaveDataSize);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string SystemVersion
	{
		get
		{
			return _systemVersion;
		}
		[MemberNotNull("_systemVersion")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_systemVersion, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SystemVersion);
				_systemVersion = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SystemVersion);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string Description
	{
		get
		{
			return _description;
		}
		[MemberNotNull("_description")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_description, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Description);
				_description = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Description);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string Intro
	{
		get
		{
			return _intro;
		}
		[MemberNotNull("_intro")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_intro, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Intro);
				_intro = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Intro);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string Category
	{
		get
		{
			return _category;
		}
		[MemberNotNull("_category")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_category, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Category);
				_category = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Category);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string Developer
	{
		get
		{
			return _developer;
		}
		[MemberNotNull("_developer")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_developer, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Developer);
				_developer = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Developer);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string ReleaseDate
	{
		get
		{
			return _releaseDate;
		}
		[MemberNotNull("_releaseDate")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_releaseDate, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.ReleaseDate);
				_releaseDate = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ReleaseDate);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.ReleaseDateVisibility);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsOutdated
	{
		get
		{
			return _isOutdated;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isOutdated, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsOutdated);
				_isOutdated = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsOutdated);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.OutdatedVisibility);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public ObservableCollection<BitmapImage> Screenshots
	{
		get
		{
			return _screenshots;
		}
		[MemberNotNull("_screenshots")]
		set
		{
			if (!EqualityComparer<ObservableCollection<BitmapImage>>.Default.Equals(_screenshots, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Screenshots);
				_screenshots = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Screenshots);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public bool HasScreenshots
	{
		get
		{
			return _hasScreenshots;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_hasScreenshots, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HasScreenshots);
				_hasScreenshots = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HasScreenshots);
			}
		}
	}
}
