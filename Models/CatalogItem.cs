using System;
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
        [NotifyPropertyChangedFor(nameof(AlternateInternalTitle))]
        [NotifyPropertyChangedFor(nameof(TitleTooltip))]
        private string _titleName = "Unknown Game";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AlternateInternalTitle))]
        [NotifyPropertyChangedFor(nameof(TitleTooltip))]
        private string _internalTitleRussian = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(AlternateInternalTitle))]
        [NotifyPropertyChangedFor(nameof(TitleTooltip))]
        private string _internalTitleEnglish = string.Empty;

        [ObservableProperty]
        private string _internalTitleFallback = string.Empty;

        [ObservableProperty]
        private bool _hasEmbeddedInternalTitle = false;

        public string ResolvePreferredInternalTitle(string? languageCode)
        {
            bool preferRu = string.IsNullOrEmpty(languageCode) || languageCode.StartsWith("ru", StringComparison.OrdinalIgnoreCase);
            if (preferRu)
            {
                if (!string.IsNullOrWhiteSpace(InternalTitleRussian)) return InternalTitleRussian;
                if (!string.IsNullOrWhiteSpace(InternalTitleEnglish)) return InternalTitleEnglish;
                if (!string.IsNullOrWhiteSpace(InternalTitleFallback)) return InternalTitleFallback;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(InternalTitleEnglish)) return InternalTitleEnglish;
                if (!string.IsNullOrWhiteSpace(InternalTitleRussian)) return InternalTitleRussian;
                if (!string.IsNullOrWhiteSpace(InternalTitleFallback)) return InternalTitleFallback;
            }
            return string.Empty;
        }

        [JsonIgnore]
        public string AlternateInternalTitle
        {
            get
            {
                if (string.IsNullOrWhiteSpace(InternalTitleRussian) || string.IsNullOrWhiteSpace(InternalTitleEnglish))
                    return string.Empty;
                if (string.Equals(InternalTitleRussian.Trim(), InternalTitleEnglish.Trim(), StringComparison.OrdinalIgnoreCase))
                    return string.Empty;
                return string.Equals(TitleName?.Trim(), InternalTitleRussian.Trim(), StringComparison.OrdinalIgnoreCase)
                    ? InternalTitleEnglish
                    : InternalTitleRussian;
            }
        }

        [JsonIgnore]
        public string TitleTooltip
        {
            get
            {
                string alt = AlternateInternalTitle;
                return !string.IsNullOrWhiteSpace(alt) ? $"{TitleName}\n{alt}" : TitleName;
            }
        }

        [ObservableProperty]
        private string _titleId = "0000000000000000";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlatformBadgeText))]
        [NotifyPropertyChangedFor(nameof(PlatformBadgeGlyph))]
        [NotifyPropertyChangedFor(nameof(PlatformBadgeBackground))]
        [NotifyPropertyChangedFor(nameof(FileFormat))]
        private bool _is3ds = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlatformBadgeText))]
        [NotifyPropertyChangedFor(nameof(PlatformBadgeGlyph))]
        [NotifyPropertyChangedFor(nameof(PlatformBadgeBackground))]
        [NotifyPropertyChangedFor(nameof(FileFormat))]
        private string _platform = "Switch";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PlatformBadgeText))]
        [NotifyPropertyChangedFor(nameof(PlatformBadgeGlyph))]
        [NotifyPropertyChangedFor(nameof(PlatformBadgeBackground))]
        [NotifyPropertyChangedFor(nameof(FileFormat))]
        private bool _isSwitch2 = false;

        public bool CheckIsSwitch2 => IsSwitch2 || Platform == "Switch 2" || Platform == "Nintendo Switch 2" || (TitleId != null && TitleId.StartsWith("04", StringComparison.OrdinalIgnoreCase));

        public string PlatformBadgeText => (Is3ds || Platform == "3DS" || Platform == "Nintendo 3DS") 
            ? "Nintendo 3DS" 
            : (CheckIsSwitch2 ? "Nintendo Switch 2" : "Nintendo Switch");
        public string PlatformBadgeGlyph => (Is3ds || Platform == "3DS" || Platform == "Nintendo 3DS") ? "\uE8EA" : "\uE7FC";
        public Microsoft.UI.Xaml.Media.SolidColorBrush PlatformBadgeBackground => (Is3ds || Platform == "3DS" || Platform == "Nintendo 3DS") 
            ? UiBrushCache.Get(Windows.UI.Color.FromArgb(230, 211, 47, 47))
            : (CheckIsSwitch2 
                ? UiBrushCache.Get(Windows.UI.Color.FromArgb(230, 142, 68, 173))
                : UiBrushCache.Get(Windows.UI.Color.FromArgb(230, 0, 120, 215)));

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RegionsVisibility))]
        private string _regions = "WW";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DlcCountDisplay))]
        private int _dlcCount = 0;

        [ObservableProperty]
        private string _version = "0";

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

        public string FileFormat
        {
            get
            {
                string path = !string.IsNullOrEmpty(FilePath) ? FilePath : FileName;
                if (!string.IsNullOrEmpty(path))
                {
                    string ext = System.IO.Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
                    if (!string.IsNullOrEmpty(ext)) return ext;
                }
                if (Is3ds || Platform == "3DS" || Platform == "Nintendo 3DS") return "3DS";
                return "NSP";
            }
        }

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
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DlcCountDisplay))]
        [NotifyPropertyChangedFor(nameof(RuTrackerUpdateSummary))]
        private int _localDlcCount = 0;

        [JsonIgnore]
        public System.Collections.Generic.HashSet<string> LocalDlcIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RuTrackerUpdateBadgeVisibility))]
        [NotifyPropertyChangedFor(nameof(RuTrackerUpdateSummary))]
        private bool _hasRuTrackerUpdate = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RuTrackerUpdateSummary))]
        private bool _hasRuTrackerVersionUpdate = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RuTrackerUpdateSummary))]
        private bool _hasRuTrackerNewDlc = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RuTrackerUpdateSummary))]
        private string _ruTrackerVersion = string.Empty;

        [ObservableProperty]
        private string _ruTrackerVersionCode = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(RuTrackerUpdateSummary))]
        private int _ruTrackerDlcCount = 0;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasRuTrackerTopic))]
        private string _ruTrackerTopicId = string.Empty;

        [ObservableProperty]
        private string _ruTrackerTopicTitle = string.Empty;

        public Microsoft.UI.Xaml.Visibility RuTrackerUpdateBadgeVisibility => HasRuTrackerUpdate ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
        public bool HasRuTrackerTopic => !string.IsNullOrWhiteSpace(RuTrackerTopicId);

        public string RuTrackerUpdateSummary
        {
            get
            {
                if (!HasRuTrackerUpdate) return string.Empty;
                var parts = new System.Collections.Generic.List<string>();
                if (HasRuTrackerVersionUpdate && !string.IsNullOrWhiteSpace(RuTrackerVersion))
                {
                    string localVer = !string.IsNullOrWhiteSpace(Version) ? Version : "0";
                    parts.Add($"Версия: {localVer} -> {RuTrackerVersion}");
                }
                if (HasRuTrackerNewDlc && RuTrackerDlcCount > LocalDlcCount)
                {
                    int delta = Math.Max(1, RuTrackerDlcCount - LocalDlcCount);
                    parts.Add($"DLC: {LocalDlcCount} -> {RuTrackerDlcCount} (+{delta})");
                }
                if (parts.Count == 0)
                    parts.Add($"RuTracker: {RuTrackerVersion}");
                return "RuTracker: " + string.Join(" | ", parts);
            }
        }

        public string DlcCountDisplay
        {
            get
            {
                if (!IsOnlineOnly && LocalDlcCount > 0)
                {
                    return DlcCount > LocalDlcCount
                        ? $"DLC: {LocalDlcCount}/{DlcCount}"
                        : $"DLC: {LocalDlcCount}";
                }
                return $"Дополнения: {DlcCount}";
            }
        }
        
        [ObservableProperty]
        private System.Collections.ObjectModel.ObservableCollection<BitmapImage> _screenshots = new();

        [ObservableProperty]
        private bool _hasScreenshots = false;

        public System.Collections.ObjectModel.ObservableCollection<TitleDbEntry> DlcList { get; } = new();
    }

    public sealed class CatalogFolderEntry
    {
        public string FolderPath { get; set; } = string.Empty;
        public bool IsRecursive { get; set; }
        public string DisplayName => IsRecursive ? $"{FolderPath}  [подпапки]" : FolderPath;
        public override string ToString() => DisplayName;
    }
}
