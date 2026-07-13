using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel.__Internals;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using WinRT;
using WinRT.StormSwitchBoxVtableClasses;
using Windows.UI;

namespace StormSwitchBox.Models;

[WinRTRuntimeClassName("Microsoft.UI.Xaml.Data.INotifyPropertyChanged")]
[WinRTExposedType(typeof(StormSwitchBox_ViewModels_TasksViewModelWinRTTypeDetails))]
public class ProcessingTask : ObservableObject
{
	[ObservableProperty]
	private string _id = string.Empty;

	[ObservableProperty]
	private BitmapImage? _gameIcon;

	[ObservableProperty]
	private string _gameName = string.Empty;

	[ObservableProperty]
	private string _operation = string.Empty;

	[ObservableProperty]
	private string _sourceFormat = string.Empty;

	[ObservableProperty]
	private string _targetFormat = string.Empty;

	[ObservableProperty]
	private string _sourceSize = string.Empty;

	[ObservableProperty]
	private string _targetSize = string.Empty;

	[ObservableProperty]
	private string _sizeDifference = string.Empty;

	[ObservableProperty]
	private string _compressionLevel = string.Empty;

	[ObservableProperty]
	private string _filesCount = string.Empty;

	[ObservableProperty]
	private string _status = string.Empty;

	[ObservableProperty]
	private double _progress;

	[ObservableProperty]
	private bool _isRunning;

	[ObservableProperty]
	private DateTime _finishedAt = DateTime.Now;

	[ObservableProperty]
	private long _sourceSizeBytes;

	[ObservableProperty]
	private string _hasRomFs = "-";

	[ObservableProperty]
	private string _hasExeFs = "-";

	[ObservableProperty]
	private string _speed = string.Empty;

	[ObservableProperty]
	private bool _isExpanded;

	[ObservableProperty]
	private string _verifyType = "-";

	[ObservableProperty]
	private string _verifyStructure = "-";

	[ObservableProperty]
	private string _verifyTitleId = "-";

	[ObservableProperty]
	private string _verifyVersion = "-";

	[ObservableProperty]
	private string _verifyMergedStatus = "-";

	[ObservableProperty]
	private string _inputFolders = string.Empty;

	[ObservableProperty]
	private string _outputFolder = string.Empty;

	[ObservableProperty]
	private string _outputFileName = string.Empty;

	[ObservableProperty]
	private string _logDetails = string.Empty;

	[JsonIgnore]
	public CancellationTokenSource? Cts { get; set; }

	[JsonIgnore]
	public string FinishedAtDisplay => FinishedAt.ToString("dd.MM.yyyy HH:mm");

	[JsonIgnore]
	public bool IsNotRunning => !IsRunning;

	public string GroupId { get; set; } = string.Empty;

	public List<string> InputFiles { get; set; } = new List<string>();

	public List<string> FilesList { get; set; } = new List<string>();

	[JsonIgnore]
	public Visibility DetailsVisibility => (!IsExpanded) ? Visibility.Collapsed : Visibility.Visible;

	[JsonIgnore]
	public string OperationDisplay
	{
		get
		{
			string operation = Operation;
			if (1 == 0)
			{
			}
			string result = operation switch
			{
				"Update" => "Обновление", 
				"Unpack" => "Распаковка", 
				"Pack" => "Упаковка", 
				"Convert" => "Конвертация", 
				"Multi" => "Мульти-контент", 
				"Verify" => "Проверка", 
				_ => Operation, 
			};
			if (1 == 0)
			{
			}
			return result;
		}
	}

	[JsonIgnore]
	public SolidColorBrush StatusColor
	{
		get
		{
			if (Status == "Успешно" || Status == "Готово" || Status == "Корректна" || Status == "Ок" || Status == "Да" || Status == "Да (Гибрид)")
			{
				return new SolidColorBrush(Color.FromArgb(byte.MaxValue, 46, 204, 113));
			}
			if (Status == "Ошибка" || Status == "Поврежден" || Status.StartsWith("Нет") || Status.Contains("ошибка") || Status.Contains("Ошибка"))
			{
				return new SolidColorBrush(Color.FromArgb(byte.MaxValue, 231, 76, 60));
			}
			if (Status == "Отменен" || Status.Contains("Предупреждение"))
			{
				return new SolidColorBrush(Color.FromArgb(byte.MaxValue, 243, 156, 18));
			}
			if (Status == "Ожидание")
			{
				return new SolidColorBrush(Color.FromArgb(byte.MaxValue, 149, 165, 166));
			}
			return new SolidColorBrush(Color.FromArgb(byte.MaxValue, 52, 152, 219));
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string Id
	{
		get
		{
			return _id;
		}
		[MemberNotNull("_id")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_id, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Id);
				_id = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Id);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public BitmapImage? GameIcon
	{
		get
		{
			return _gameIcon;
		}
		set
		{
			if (!EqualityComparer<BitmapImage>.Default.Equals(_gameIcon, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.GameIcon);
				_gameIcon = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.GameIcon);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string GameName
	{
		get
		{
			return _gameName;
		}
		[MemberNotNull("_gameName")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_gameName, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.GameName);
				_gameName = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.GameName);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string Operation
	{
		get
		{
			return _operation;
		}
		[MemberNotNull("_operation")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_operation, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Operation);
				_operation = value;
				OnOperationChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Operation);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string SourceFormat
	{
		get
		{
			return _sourceFormat;
		}
		[MemberNotNull("_sourceFormat")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_sourceFormat, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SourceFormat);
				_sourceFormat = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SourceFormat);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string TargetFormat
	{
		get
		{
			return _targetFormat;
		}
		[MemberNotNull("_targetFormat")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_targetFormat, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.TargetFormat);
				_targetFormat = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.TargetFormat);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string SourceSize
	{
		get
		{
			return _sourceSize;
		}
		[MemberNotNull("_sourceSize")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_sourceSize, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SourceSize);
				_sourceSize = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SourceSize);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string TargetSize
	{
		get
		{
			return _targetSize;
		}
		[MemberNotNull("_targetSize")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_targetSize, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.TargetSize);
				_targetSize = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.TargetSize);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string SizeDifference
	{
		get
		{
			return _sizeDifference;
		}
		[MemberNotNull("_sizeDifference")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_sizeDifference, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SizeDifference);
				_sizeDifference = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SizeDifference);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string CompressionLevel
	{
		get
		{
			return _compressionLevel;
		}
		[MemberNotNull("_compressionLevel")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_compressionLevel, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.CompressionLevel);
				_compressionLevel = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.CompressionLevel);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string FilesCount
	{
		get
		{
			return _filesCount;
		}
		[MemberNotNull("_filesCount")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_filesCount, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.FilesCount);
				_filesCount = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.FilesCount);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string Status
	{
		get
		{
			return _status;
		}
		[MemberNotNull("_status")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_status, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Status);
				_status = value;
				OnStatusChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Status);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public double Progress
	{
		get
		{
			return _progress;
		}
		set
		{
			if (!EqualityComparer<double>.Default.Equals(_progress, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Progress);
				_progress = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Progress);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsRunning
	{
		get
		{
			return _isRunning;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isRunning, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsRunning);
				_isRunning = value;
				OnIsRunningChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsRunning);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public DateTime FinishedAt
	{
		get
		{
			return _finishedAt;
		}
		set
		{
			if (!EqualityComparer<DateTime>.Default.Equals(_finishedAt, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.FinishedAt);
				_finishedAt = value;
				OnFinishedAtChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.FinishedAt);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public long SourceSizeBytes
	{
		get
		{
			return _sourceSizeBytes;
		}
		set
		{
			if (!EqualityComparer<long>.Default.Equals(_sourceSizeBytes, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.SourceSizeBytes);
				_sourceSizeBytes = value;
				OnSourceSizeBytesChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.SourceSizeBytes);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string HasRomFs
	{
		get
		{
			return _hasRomFs;
		}
		[MemberNotNull("_hasRomFs")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_hasRomFs, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HasRomFs);
				_hasRomFs = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HasRomFs);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string HasExeFs
	{
		get
		{
			return _hasExeFs;
		}
		[MemberNotNull("_hasExeFs")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_hasExeFs, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.HasExeFs);
				_hasExeFs = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.HasExeFs);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string Speed
	{
		get
		{
			return _speed;
		}
		[MemberNotNull("_speed")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_speed, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.Speed);
				_speed = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.Speed);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public bool IsExpanded
	{
		get
		{
			return _isExpanded;
		}
		set
		{
			if (!EqualityComparer<bool>.Default.Equals(_isExpanded, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.IsExpanded);
				_isExpanded = value;
				OnIsExpandedChanged(value);
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.IsExpanded);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string VerifyType
	{
		get
		{
			return _verifyType;
		}
		[MemberNotNull("_verifyType")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_verifyType, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.VerifyType);
				_verifyType = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.VerifyType);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string VerifyStructure
	{
		get
		{
			return _verifyStructure;
		}
		[MemberNotNull("_verifyStructure")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_verifyStructure, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.VerifyStructure);
				_verifyStructure = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.VerifyStructure);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string VerifyTitleId
	{
		get
		{
			return _verifyTitleId;
		}
		[MemberNotNull("_verifyTitleId")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_verifyTitleId, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.VerifyTitleId);
				_verifyTitleId = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.VerifyTitleId);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string VerifyVersion
	{
		get
		{
			return _verifyVersion;
		}
		[MemberNotNull("_verifyVersion")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_verifyVersion, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.VerifyVersion);
				_verifyVersion = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.VerifyVersion);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string VerifyMergedStatus
	{
		get
		{
			return _verifyMergedStatus;
		}
		[MemberNotNull("_verifyMergedStatus")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_verifyMergedStatus, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.VerifyMergedStatus);
				_verifyMergedStatus = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.VerifyMergedStatus);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string InputFolders
	{
		get
		{
			return _inputFolders;
		}
		[MemberNotNull("_inputFolders")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_inputFolders, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.InputFolders);
				_inputFolders = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.InputFolders);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string OutputFolder
	{
		get
		{
			return _outputFolder;
		}
		[MemberNotNull("_outputFolder")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_outputFolder, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.OutputFolder);
				_outputFolder = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.OutputFolder);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string OutputFileName
	{
		get
		{
			return _outputFileName;
		}
		[MemberNotNull("_outputFileName")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_outputFileName, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.OutputFileName);
				_outputFileName = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.OutputFileName);
			}
		}
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	[ExcludeFromCodeCoverage]
	public string LogDetails
	{
		get
		{
			return _logDetails;
		}
		[MemberNotNull("_logDetails")]
		set
		{
			if (!EqualityComparer<string>.Default.Equals(_logDetails, value))
			{
				OnPropertyChanging(__KnownINotifyPropertyChangingArgs.LogDetails);
				_logDetails = value;
				OnPropertyChanged(__KnownINotifyPropertyChangedArgs.LogDetails);
			}
		}
	}

	public static string FormatSize(long bytes)
	{
		if (bytes <= 0)
		{
			return "-";
		}
		double num;
		string text;
		if (bytes >= 1073741824)
		{
			num = (double)bytes / 1073741824.0;
			text = "GB";
		}
		else if (bytes >= 1048576)
		{
			num = (double)bytes / 1048576.0;
			text = "MB";
		}
		else
		{
			if (bytes < 1024)
			{
				return $"{bytes} B";
			}
			num = (double)bytes / 1024.0;
			text = "KB";
		}
		string text2 = num.ToString("N2", CultureInfo.GetCultureInfo("ru-RU"));
		text2 = text2.Replace('\u00a0', ' ');
		return text2 + " " + text;
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	private void OnOperationChanged(string value)
	{
		OnPropertyChanged("OperationDisplay");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	private void OnStatusChanged(string value)
	{
		OnPropertyChanged("StatusColor");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	private void OnIsRunningChanged(bool value)
	{
		OnPropertyChanged("IsNotRunning");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	private void OnFinishedAtChanged(DateTime value)
	{
		OnPropertyChanged("FinishedAtDisplay");
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	private void OnSourceSizeBytesChanged(long value)
	{
		SourceSize = FormatSize(value);
	}

	[GeneratedCode("CommunityToolkit.Mvvm.SourceGenerators.ObservablePropertyGenerator", "8.2.0.0")]
	private void OnIsExpandedChanged(bool value)
	{
		OnPropertyChanged("DetailsVisibility");
	}
}
