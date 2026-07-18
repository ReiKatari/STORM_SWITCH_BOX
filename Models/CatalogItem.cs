using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Text.Json.Serialization;
using StormSwitchBox.Services;

namespace StormSwitchBox.Models
{
    public partial class CatalogItem : ObservableObject
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
        [NotifyPropertyChangedFor(nameof(RegionsVisibility))]
        private string _regions = "WW";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DlcCountDisplay))]
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
        [NotifyPropertyChangedFor(nameof(AcceptButtonVisibility))]
        private bool _isOnlineOnly = false;
        
        public Microsoft.UI.Xaml.Visibility AcceptButtonVisibility => IsOnlineOnly ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
        [ObservableProperty]
        private string _errorMessage = "";

        [ObservableProperty] private string _supportedLanguages = "";
        [ObservableProperty] private string _ratingAge = "";
        [ObservableProperty] private string _videoCapture = "";
        [ObservableProperty] private string _saveDataSize = "";
        [ObservableProperty] private string _systemVersion = "";
        
        [ObservableProperty] private string _description = "Нет описания";
        [ObservableProperty] private string _intro = "";
        [ObservableProperty] private string _category = "";
        [ObservableProperty] private string _developer = "";
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ReleaseDateVisibility))]
        private string _releaseDate = "";

        [ObservableProperty] 
        [NotifyPropertyChangedFor(nameof(OutdatedVisibility))]
        private bool _isOutdated = false;
        
        public Microsoft.UI.Xaml.Visibility OutdatedVisibility => IsOutdated ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        public Microsoft.UI.Xaml.Visibility RegionsVisibility => string.IsNullOrEmpty(Regions) || Regions == "N/A" ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
        public Microsoft.UI.Xaml.Visibility ReleaseDateVisibility => string.IsNullOrEmpty(ReleaseDate) || ReleaseDate == "N/A" ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;
        public string DlcCountDisplay => $"Дополнения: {DlcCount}";
        
        [ObservableProperty]
        private System.Collections.ObjectModel.ObservableCollection<BitmapImage> _screenshots = new();

        [ObservableProperty]
        private bool _hasScreenshots = false;

        public System.Collections.ObjectModel.ObservableCollection<TitleDbEntry> DlcList { get; } = new();
    }
}
