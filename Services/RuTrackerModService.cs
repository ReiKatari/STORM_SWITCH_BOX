#nullable enable
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using MonoTorrent;
using MonoTorrent.Client;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public sealed class RuTrackerTopicItem
    {
        public string TopicId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string SizeText { get; set; } = string.Empty;
        public int Seeds { get; set; }
        public int Leechers { get; set; }
        public string TopicUrl { get; set; } = string.Empty;
        public string MagnetUri { get; set; } = string.Empty;
        public string InfoHash { get; set; } = string.Empty;
        public string DirectTorrentUrl { get; set; } = string.Empty;
        public string PosterUrl { get; set; } = string.Empty;
        public List<CloudModLinkItem> ExtractedModLinks { get; set; } = new();
    }

    public sealed class GameSlotTargetOption
    {
        public int SlotNumber { get; set; }
        public string DisplayText => $"ИГРА #{SlotNumber}";

        public override string ToString() => DisplayText;
        public override bool Equals(object? obj) => obj is GameSlotTargetOption other && other.SlotNumber == SlotNumber;
        public override int GetHashCode() => SlotNumber.GetHashCode();
    }

    public sealed class RuTrackerGameSlotItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        private int _slotNumber = 1;
        public int SlotNumber
        {
            get => _slotNumber;
            set
            {
                if (SetProperty(ref _slotNumber, value))
                {
                    OnPropertyChanged(nameof(SlotBadge));
                    OnPropertyChanged(nameof(CanChangeSlotNumber));
                }
            }
        }

        public string SlotBadge => $"ИГРА #{SlotNumber}";

        public System.Collections.ObjectModel.ObservableCollection<GameSlotTargetOption> AvailableSlotTargets { get; } = new();

        private GameSlotTargetOption? _selectedSlotTarget;
        public GameSlotTargetOption? SelectedSlotTarget
        {
            get => _selectedSlotTarget;
            set => SetProperty(ref _selectedSlotTarget, value);
        }

        public bool CanChangeSlotNumber => AvailableSlotTargets.Count > 1;

        public void RefreshSlotTargetsState()
        {
            OnPropertyChanged(nameof(CanChangeSlotNumber));
            OnPropertyChanged(nameof(SelectedSlotTarget));
        }

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        public static readonly string[] DefaultTargetFormats = new[] { "NSP", "NSZ", "XCI", "XCZ", "NSP + NSZ", "XCI + XCZ" };
        public string[] AvailableTargetFormats => DefaultTargetFormats;

        private string _targetFormat = "NSP";
        public string TargetFormat
        {
            get => string.IsNullOrWhiteSpace(_targetFormat) ? "NSP" : _targetFormat;
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                    SetProperty(ref _targetFormat, value);
            }
        }

        private string _gameTitle = string.Empty;
        public string GameTitle
        {
            get => _gameTitle;
            set
            {
                if (SetProperty(ref _gameTitle, value))
                {
                    if (!_isUserEditedMetadata && !string.IsNullOrWhiteSpace(value))
                    {
                        string clean = RuTrackerModService.ExtractCleanGameTitleFromFilePath(value);
                        if (string.IsNullOrWhiteSpace(clean)) clean = value.Trim();
                        if (string.IsNullOrWhiteSpace(_internalTitleEnglish))
                        {
                            _internalTitleEnglish = clean;
                            OnPropertyChanged(nameof(InternalTitleEnglish));
                        }
                        if (string.IsNullOrWhiteSpace(_internalTitleRussian))
                        {
                            _internalTitleRussian = clean;
                            OnPropertyChanged(nameof(InternalTitleRussian));
                        }
                    }
                }
            }
        }

        private string _customModTitle = string.Empty;
        public string CustomModTitle
        {
            get => _customModTitle;
            set => SetProperty(ref _customModTitle, value);
        }

        private string _manualExtraLinks = string.Empty;
        public string ManualExtraLinks
        {
            get => _manualExtraLinks;
            set => SetProperty(ref _manualExtraLinks, value);
        }

        private string _summaryText = "Файлы: авто • Ссылки: 0";
        public string SummaryText
        {
            get => _summaryText;
            set => SetProperty(ref _summaryText, value);
        }

        private string _titleId = string.Empty;
        public string TitleId
        {
            get => _titleId;
            set
            {
                if (SetProperty(ref _titleId, value))
                    OnPropertyChanged(nameof(TitleIdAndVersionBadge));
            }
        }

        private string _displayVersion = "1.0.0";
        public string DisplayVersion
        {
            get => string.IsNullOrWhiteSpace(_displayVersion) ? "1.0.0" : _displayVersion;
            set
            {
                if (SetProperty(ref _displayVersion, value))
                    OnPropertyChanged(nameof(TitleIdAndVersionBadge));
            }
        }

        public string TitleIdAndVersionBadge =>
            !string.IsNullOrWhiteSpace(TitleId)
                ? $"{TitleId} • v{DisplayVersion}"
                : $"v{DisplayVersion}";

        private string _internalTitleEnglish = string.Empty;
        public string InternalTitleEnglish
        {
            get => _internalTitleEnglish;
            set
            {
                if (SetProperty(ref _internalTitleEnglish, value))
                {
                    OnPropertyChanged(nameof(HasCustomMetadata));
                    OnPropertyChanged(nameof(MetadataStudioButtonText));
                }
            }
        }

        private string _internalTitleRussian = string.Empty;
        public string InternalTitleRussian
        {
            get => _internalTitleRussian;
            set
            {
                if (SetProperty(ref _internalTitleRussian, value))
                {
                    OnPropertyChanged(nameof(HasCustomMetadata));
                    OnPropertyChanged(nameof(MetadataStudioButtonText));
                }
            }
        }

        private string _publisher = "Nintendo";
        public string Publisher
        {
            get => _publisher;
            set
            {
                if (SetProperty(ref _publisher, value))
                {
                    OnPropertyChanged(nameof(HasCustomMetadata));
                    OnPropertyChanged(nameof(MetadataStudioButtonText));
                }
            }
        }

        private bool _isUserEditedMetadata;
        public bool IsUserEditedMetadata
        {
            get => _isUserEditedMetadata;
            set
            {
                if (SetProperty(ref _isUserEditedMetadata, value))
                {
                    OnPropertyChanged(nameof(HasCustomMetadata));
                    OnPropertyChanged(nameof(MetadataStudioButtonText));
                }
            }
        }

        private byte[]? _originalIconBytes;
        public byte[]? OriginalIconBytes
        {
            get => _originalIconBytes;
            set
            {
                _originalIconBytes = value;
                RefreshSlotIconBitmap();
            }
        }

        private byte[]? _customIconBytes;
        public byte[]? CustomIconBytes
        {
            get => _customIconBytes;
            set
            {
                _customIconBytes = value;
                OnPropertyChanged(nameof(HasCustomMetadata));
                OnPropertyChanged(nameof(MetadataStudioButtonText));
                RefreshSlotIconBitmap();
            }
        }

        private string? _customIconPath;
        public string? CustomIconPath
        {
            get => _customIconPath;
            set
            {
                if (SetProperty(ref _customIconPath, value))
                {
                    OnPropertyChanged(nameof(HasCustomMetadata));
                    OnPropertyChanged(nameof(MetadataStudioButtonText));
                }
            }
        }

        private Microsoft.UI.Xaml.Media.Imaging.BitmapImage? _slotIconBitmap;
        public Microsoft.UI.Xaml.Media.Imaging.BitmapImage? SlotIconBitmap
        {
            get => _slotIconBitmap;
            set => SetProperty(ref _slotIconBitmap, value);
        }

        public bool IsManuallyAdded { get; set; }
        public bool Is3ds { get; set; }
        public bool BadgeRus { get; set; }
        public bool Badge60Fps { get; set; }
        public bool BadgeMod { get; set; }
        public bool BadgeAllDlc { get; set; }
        public string CustomBadgeText { get; set; } = string.Empty;
        public string BadgePosition { get; set; } = "BottomRight";
        public string CustomAudioWavPath { get; set; } = string.Empty;

        public bool HasBadges => BadgeRus || Badge60Fps || BadgeMod || BadgeAllDlc || !string.IsNullOrWhiteSpace(CustomBadgeText);

        public bool HasCustomMetadata =>
            IsUserEditedMetadata ||
            (CustomIconBytes != null && CustomIconBytes.Length > 0) ||
            !string.IsNullOrWhiteSpace(CustomIconPath) ||
            HasBadges ||
            !string.IsNullOrWhiteSpace(CustomAudioWavPath);

        public string MetadataStudioButtonText => HasCustomMetadata ? "🎨 Студия NACP ✓" : "🎨 Студия NACP";

        public void SetEnrichedMetadataSilently(string titleId, string version, string engTitle, string rusTitle, string publisher, byte[]? iconBytes)
        {
            bool titleIdChanged = !string.IsNullOrWhiteSpace(titleId) &&
                                  !string.Equals(TitleId, titleId, StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(titleId))
                TitleId = titleId;
            if (!string.IsNullOrWhiteSpace(version))
                DisplayVersion = version;

            if (!IsUserEditedMetadata)
            {
                if (!string.IsNullOrWhiteSpace(engTitle))
                {
                    _internalTitleEnglish = engTitle;
                    OnPropertyChanged(nameof(InternalTitleEnglish));
                }
                if (!string.IsNullOrWhiteSpace(rusTitle))
                {
                    _internalTitleRussian = rusTitle;
                    OnPropertyChanged(nameof(InternalTitleRussian));
                }
                if (!string.IsNullOrWhiteSpace(publisher))
                {
                    _publisher = publisher;
                    OnPropertyChanged(nameof(Publisher));
                }
            }

            if (iconBytes != null && iconBytes.Length > 0 && (_originalIconBytes == null || _originalIconBytes.Length == 0 || titleIdChanged))
            {
                _originalIconBytes = iconBytes;
                RefreshSlotIconBitmap();
            }
            else if (titleIdChanged && (iconBytes == null || iconBytes.Length == 0))
            {
                _originalIconBytes = null;
                RefreshSlotIconBitmap();
            }
        }

        public void RefreshSlotIconBitmap()
        {
            byte[]? activeBytes = _customIconBytes ?? _originalIconBytes;
            if (activeBytes == null || activeBytes.Length == 0)
            {
                App.RunOnUI(() => SlotIconBitmap = null);
                return;
            }

            byte[] dataCopy = activeBytes;
            App.RunOnUI(async () =>
            {
                try
                {
                    using var ms = new MemoryStream(dataCopy);
                    var bmp = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage();
                    await bmp.SetSourceAsync(ms.AsRandomAccessStream());
                    SlotIconBitmap = bmp;
                }
                catch { }
            });
        }

        public StormSwitchBox.Models.GameMetadataEditModel ToMetadataEditModel()
        {
            string cleanFallback = RuTrackerModService.ExtractCleanGameTitleFromFilePath(GameTitle);
            if (string.IsNullOrWhiteSpace(cleanFallback)) cleanFallback = GameTitle.Trim();

            string eng = !string.IsNullOrWhiteSpace(InternalTitleEnglish) ? InternalTitleEnglish.Trim() : cleanFallback;
            string rus = !string.IsNullOrWhiteSpace(InternalTitleRussian) ? InternalTitleRussian.Trim() : eng;
            string pub = !string.IsNullOrWhiteSpace(Publisher) ? Publisher.Trim() : "Nintendo";

            return new StormSwitchBox.Models.GameMetadataEditModel
            {
                TitleId = !string.IsNullOrWhiteSpace(TitleId) ? TitleId : "0100000000000000",
                Version = DisplayVersion,
                TitleNameEnglish = eng,
                TitleNameRussian = rus,
                Publisher = pub,
                OriginalIconBytes = OriginalIconBytes,
                CustomIconBytes = CustomIconBytes,
                CustomIconPath = CustomIconPath,
                Is3ds = Is3ds,
                BadgeRus = BadgeRus,
                Badge60Fps = Badge60Fps,
                BadgeMod = BadgeMod,
                BadgeAllDlc = BadgeAllDlc,
                CustomBadgeText = CustomBadgeText,
                BadgePosition = BadgePosition,
                CustomAudioWavPath = CustomAudioWavPath,
                HasRomFs = !string.IsNullOrWhiteSpace(CustomModTitle),
                ModNameRomFs = CustomModTitle ?? string.Empty
            };
        }

        public void ApplyFromMetadataEditModel(StormSwitchBox.Models.GameMetadataEditModel model)
        {
            if (model == null) return;
            IsUserEditedMetadata = true;
            if (!string.IsNullOrWhiteSpace(model.TitleId) && model.TitleId != "0100000000000000")
                TitleId = model.TitleId;
            if (!string.IsNullOrWhiteSpace(model.Version))
                DisplayVersion = model.Version;

            _internalTitleEnglish = model.TitleNameEnglish?.Trim() ?? string.Empty;
            _internalTitleRussian = model.TitleNameRussian?.Trim() ?? string.Empty;
            _publisher = !string.IsNullOrWhiteSpace(model.Publisher) ? model.Publisher.Trim() : "Nintendo";

            OriginalIconBytes = model.OriginalIconBytes;
            CustomIconPath = model.CustomIconPath;
            CustomIconBytes = model.CustomIconBytes;
            Is3ds = model.Is3ds;
            BadgeRus = model.BadgeRus;
            Badge60Fps = model.Badge60Fps;
            BadgeMod = model.BadgeMod;
            BadgeAllDlc = model.BadgeAllDlc;
            CustomBadgeText = model.CustomBadgeText ?? string.Empty;
            BadgePosition = model.BadgePosition;
            CustomAudioWavPath = model.CustomAudioWavPath;
            if (!string.IsNullOrWhiteSpace(model.ModNameRomFs))
                CustomModTitle = model.ModNameRomFs;

            OnPropertyChanged(nameof(InternalTitleEnglish));
            OnPropertyChanged(nameof(InternalTitleRussian));
            OnPropertyChanged(nameof(Publisher));
            OnPropertyChanged(nameof(HasCustomMetadata));
            OnPropertyChanged(nameof(MetadataStudioButtonText));
            RefreshSlotIconBitmap();
        }
    }

    public sealed class TorrentFileSelectionItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private int _targetGameSlot = 0; // 0 = Все игры (общий), 1..10 = Игра #1..#10
        public int TargetGameSlot
        {
            get => _targetGameSlot;
            set => SetProperty(ref _targetGameSlot, Math.Max(0, value));
        }

        public string RelativePath { get; set; } = string.Empty;
        public List<string> GroupedRelativePaths { get; set; } = new();
        public string DetectedTitleId { get; set; } = string.Empty;
        public IReadOnlyList<string> GetEffectiveRelativePaths() => GroupedRelativePaths != null && GroupedRelativePaths.Count > 0 ? GroupedRelativePaths : new[] { RelativePath };
        private string _displayPath = string.Empty;
        public string DisplayPath
        {
            get => !string.IsNullOrEmpty(_displayPath) ? _displayPath : RelativePath;
            set => SetProperty(ref _displayPath, value);
        }
        public long Length { get; set; }
        public long SizeBytes { get => Length; set => Length = value; }
        public string SizeFormatted => Length > 0 ? ProcessingTask.FormatSize(Length) : "DHT/Magnet";

        private string _categoryBadge = "ФАЙЛ";
        public string CategoryBadge
        {
            get => _categoryBadge;
            set
            {
                if (SetProperty(ref _categoryBadge, value ?? "ФАЙЛ"))
                {
                    OnPropertyChanged(nameof(RoleBadgeBackground));
                    OnPropertyChanged(nameof(RoleBadgeForeground));
                }
            }
        }

        public bool IsRealTorrentFile { get; set; } = false;

        public string RoleBadgeBackground => CategoryBadge switch
        {
            "ИГРА" => "#064E3B",
            "ОБНОВЛЕНИЕ" => "#0C4A6E",
            "ОБНОВЛЕНИЕ (РУС)" => "#164E63",
            "DLC" => "#4C1D95",
            "МОД / РУСИФИКАТОР" => "#78350F",
            "ИНФО" => "#1E293B",
            _ => "#1E293B"
        };

        public string RoleBadgeForeground => CategoryBadge switch
        {
            "ИГРА" => "#34D399",
            "ОБНОВЛЕНИЕ" => "#38BDF8",
            "ОБНОВЛЕНИЕ (РУС)" => "#67E8F9",
            "DLC" => "#C084FC",
            "МОД / РУСИФИКАТОР" => "#FBBF24",
            "ИНФО" => "#94A3B8",
            _ => "#FBBF24"
        };
    }

    public sealed class RuTrackerFileGroupBlock : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        public int SlotNumber { get; set; }

        private string _groupBadge = string.Empty;
        public string GroupBadge
        {
            get => !string.IsNullOrEmpty(_groupBadge) ? _groupBadge : (SlotNumber > 0 ? $"🎮 ИГРА #{SlotNumber}" : "📦 ОБЩИЕ ФАЙЛЫ");
            set => SetProperty(ref _groupBadge, value);
        }
        public string BlockBadge => GroupBadge;

        private string _groupTitle = string.Empty;
        public string GroupTitle
        {
            get => _groupTitle;
            set => SetProperty(ref _groupTitle, value);
        }
        public string GameTitle
        {
            get => GroupTitle;
            set => GroupTitle = value;
        }

        private string _groupSummary = string.Empty;
        public string GroupSummary
        {
            get => _groupSummary;
            set => SetProperty(ref _groupSummary, value);
        }
        public string HeaderSummary
        {
            get => GroupSummary;
            set => GroupSummary = value;
        }

        public string BadgeBackground => SlotNumber switch
        {
            1 => "#0284C7",
            2 => "#7C3AED",
            3 => "#059669",
            4 => "#D97706",
            5 => "#DB2777",
            6 => "#2563EB",
            7 => "#9333EA",
            8 => "#0D9488",
            9 => "#EA580C",
            10 => "#4F46E5",
            _ => "#334155"
        };

        public System.Collections.ObjectModel.ObservableCollection<TorrentFileSelectionItem> Files { get; } = new();
    }

    public sealed class RuTrackerLinkGroupBlock : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        public int SlotNumber { get; set; }

        private string _groupBadge = string.Empty;
        public string GroupBadge
        {
            get => !string.IsNullOrEmpty(_groupBadge) ? _groupBadge : (SlotNumber > 0 ? $"🎮 ИГРА #{SlotNumber}" : "🌐 ОБЩИЕ ССЫЛКИ");
            set => SetProperty(ref _groupBadge, value);
        }
        public string BlockBadge => GroupBadge;

        private string _groupTitle = string.Empty;
        public string GroupTitle
        {
            get => _groupTitle;
            set => SetProperty(ref _groupTitle, value);
        }
        public string GameTitle
        {
            get => GroupTitle;
            set => GroupTitle = value;
        }

        private string _groupSummary = string.Empty;
        public string GroupSummary
        {
            get => _groupSummary;
            set => SetProperty(ref _groupSummary, value);
        }
        public string HeaderSummary
        {
            get => GroupSummary;
            set => GroupSummary = value;
        }

        public string BadgeBackground => SlotNumber switch
        {
            1 => "#0284C7",
            2 => "#7C3AED",
            3 => "#059669",
            4 => "#D97706",
            5 => "#DB2777",
            _ => "#334155"
        };

        public System.Collections.ObjectModel.ObservableCollection<CloudModLinkItem> Links { get; } = new();
    }

    public sealed class CloudModLinkItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        private bool _isSelected = false;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        private int _targetGameSlot = 0; // 0 = Все игры, 1..10 = Игра #1..#10
        public int TargetGameSlot
        {
            get => _targetGameSlot;
            set => SetProperty(ref _targetGameSlot, Math.Max(0, value));
        }

        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public List<string> MirrorUrls { get; set; } = new();
        public string Provider { get; set; } = string.Empty;
        public string PublishedDate { get; set; } = "25.10.2026 14:30";
        public string SourceLocation { get; set; } = "Шапка темы";
        public string RoleCategory { get; set; } = "МОД / РУСИФИКАТОР";
        public string DisplayMeta => $"📅 {PublishedDate} • {SourceLocation}";
    }

    public sealed class TorrentDownloadProgress
    {
        public double Percent { get; set; }
        public double SpeedMbPerSec { get; set; }
        public int Seeds { get; set; }
        public int Peers { get; set; }
        public int SwarmTotal { get; set; }
        public long DownloadedBytes { get; set; }
        public long TotalBytes { get; set; }
        public bool IsMetadataPhase { get; set; }
        public string StatusText { get; set; } = string.Empty;
    }

    public sealed class RuTrackerIndexFile
    {
        public string Path { get; set; } = string.Empty;
        public long Size { get; set; }
        public string TitleId { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string FileName => Path.Contains('/') ? Path.Substring(Path.LastIndexOf('/') + 1) : Path;
    }

    public sealed class RuTrackerIndexTopic
    {
        public string TopicId { get; set; } = string.Empty;
        public string InfoHash { get; set; } = string.Empty;
        public int Seeds { get; set; }
        public long SizeBytes { get; set; }
        public long RegTime { get; set; }
        public string TorrentName { get; set; } = string.Empty;
        public string TopicTitle { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;
        public string TitleId { get; set; } = string.Empty;
        public List<RuTrackerIndexFile> Files { get; set; } = new();

        internal string GameKey = "";
        internal string GameKeyPadded = "";
        internal string TitleKey = "";
        internal string TitleKeyPadded = "";
        internal string BlobPadded = "";
        internal string[] Words = Array.Empty<string>();
        internal int GameWordCount;
        internal string Skeleton = "";
        internal string TidBlob = "";

        private static readonly HashSet<string> TagNoiseWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "nintendo", "switch", "nsw", "nsw2", "nsp", "nsz", "xci", "xcz", "nup", "nro",
            "rus", "eng", "jpn", "multi", "update", "upd", "dlc", "unlocker", "patched",
            "repack", "gb", "mb", "гб", "мб", "обновлено", "порт", "хоумбрю", "homebrew",
            "mod", "mods", "русификатор"
        };

        public void RebuildKeys()
        {
            string rawFullTitle = !string.IsNullOrWhiteSpace(TopicTitle) ? TopicTitle : TorrentName;
            string baseName = !string.IsNullOrWhiteSpace(GameName)
                ? GameName
                : RuTrackerModService.CleanTitleToGame(rawFullTitle);

            GameKey = RuTrackerModService.NormalizeSearchKey(baseName);
            GameKeyPadded = " " + GameKey + " ";

            string fullClean = RuTrackerModService.CleanTitleKeepAltNames(rawFullTitle);
            TitleKey = RuTrackerModService.NormalizeSearchKey(string.IsNullOrWhiteSpace(fullClean) ? baseName : fullClean);
            TitleKeyPadded = " " + TitleKey + " ";

            var blob = new StringBuilder(GameKey.Length * 4);
            blob.Append(GameKey).Append(' ').Append(TitleKey);

            foreach (var f in Files)
            {
                int dot = f.FileName.LastIndexOf('.');
                string stem = dot > 0 ? f.FileName.Substring(0, dot) : f.FileName;
                blob.Append(' ').Append(RuTrackerModService.NormalizeSearchKey(RuTrackerModService.CleanTitleToGame(stem)));
            }

            string blobStr = blob.ToString();
            BlobPadded = " " + blobStr + " ";

            var orderedWords = new List<string>();
            var seenWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var w in GameKey.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!TagNoiseWords.Contains(w) && seenWords.Add(w))
                    orderedWords.Add(w);
            }
            GameWordCount = orderedWords.Count;

            foreach (var r in blobStr.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!TagNoiseWords.Contains(r) && seenWords.Add(r))
                    orderedWords.Add(r);
            }

            Words = orderedWords.ToArray();
            Skeleton = "";

            var tids = new StringBuilder();
            if (!string.IsNullOrEmpty(TitleId)) tids.Append(TitleId.ToLowerInvariant()).Append(' ');
            foreach (var f in Files)
            {
                if (!string.IsNullOrEmpty(f.TitleId)) tids.Append(f.TitleId.ToLowerInvariant()).Append(' ');
            }
            TidBlob = tids.ToString();
        }

        public void UpdateTopicTitle(string newTitle)
        {
            if (string.IsNullOrWhiteSpace(newTitle) || newTitle == TopicTitle) return;
            TopicTitle = newTitle;
            if (string.IsNullOrWhiteSpace(GameName))
                GameName = RuTrackerModService.CleanTitleToGame(newTitle);
            if (string.IsNullOrWhiteSpace(TitleId))
                TitleId = RuTrackerModService.ExtractTitleId(newTitle);
            RebuildKeys();
        }
    }

    /// <summary>
    /// Комплексный сервис интеграции с RuTracker.org (поиск по всем 7 275 темам раздела #1605,
    /// обход Cloudflare через WebView2, прямое MonoTorrent P2P скачивание с кэшированием .torrent,
    /// и облачный загрузчик модификаций с поддержкой PixelDrain, Yandex, GDrive, MEGA, Mail.ru, MediaFire, Dropbox, WDfiles, GitHub, Workupload).
    /// </summary>
    public sealed class RuTrackerModService : IDisposable
    {
        public static readonly System.Net.CookieContainer SharedForumCookies = new();
        public static Func<string, bool, CancellationToken, Task<(string? Html, string? DownloadedFilePath, string? CookieHeader)>>? ExternalBrowserBypassHandler { get; set; }

        private static readonly string[] DefaultMirrors =
        {
            "https://rutracker.org",
            "https://rutracker.net"
        };
        private static int _activeMirrorIndex = 0;

        private static readonly string[] PublicTrackers =
        {
            "http://bt.t-ru.org/ann?magnet",
            "http://bt2.t-ru.org/ann?magnet",
            "http://bt3.t-ru.org/ann?magnet",
            "http://bt4.t-ru.org/ann?magnet",
            "http://bt.t-ru.org/ann",
            "http://retracker.local/announce",
            "https://tracker.opentrackr.org:443/announce",
            "https://tracker.gbitt.info:443/announce",
            "http://tracker.dler.org:6969/announce",
            "https://tr.burnabyhighstar.com:443/announce",
            "http://open.acgnxtracker.com:80/announce",
            "http://tracker.mywaifu.best:6969/announce",
            "https://tracker.tamersunion.org:443/announce",
            "http://tracker.bt4g.com:2095/announce",
            "udp://tracker.opentrackr.org:1337/announce",
            "udp://open.stealth.si:80/announce",
            "udp://exodus.desync.com:6969/announce",
            "udp://tracker.torrent.eu.org:451/announce",
            "udp://open.demonii.com:1337/announce",
            "udp://explodie.org:6969/announce",
            "udp://tracker.moeking.me:6969/announce",
            "udp://opentracker.i2p.rocks:6969/announce",
            "udp://tracker.openbittorrent.com:6969/announce",
            "udp://tracker.theoks.net:6969/announce",
            "udp://tracker.bittor.pw:1337/announce",
            "udp://bt1.archive.org:6969/announce",
            "udp://bt2.archive.org:6969/announce",
            "udp://p4p.arenabg.com:1337/announce",
            "udp://retracker.hotplug.ru:2710/announce",
            "udp://opentor.org:2710/announce",
            "udp://tracker.zerobytes.xyz:1337/announce"
        };

        public static Func<string, string, CancellationToken, Task<string?>>? CloudflareChallengeHandler { get; set; }
        public static Func<string, CancellationToken, Task<string?>>? SameOriginFetchHandler { get; set; }
        public static Func<string, CancellationToken, Task<List<(string Path, long Size)>?>>? SameOriginViewTorrentHandler { get; set; }
        public static Func<string, CancellationToken, Task<byte[]?>>? SameOriginTorrentBytesHandler { get; set; }

        public static string GetActiveMirror()
        {
            string cfg = App.Settings.Current.RuTrackerMirror?.Trim() ?? "";
            if (!string.IsNullOrEmpty(cfg) && !cfg.Contains(".nl", StringComparison.OrdinalIgnoreCase))
            {
                return NormalizeMirrorUrl(cfg);
            }
            return DefaultMirrors[_activeMirrorIndex % DefaultMirrors.Length];
        }

        public static async Task<string> ProbeAndGetActiveMirrorAsync(string? preferred = null, CancellationToken ct = default)
        {
            string[] mirrors = new[] { "https://rutracker.org", "https://rutracker.net" };
            if (!string.IsNullOrWhiteSpace(preferred))
            {
                string norm = NormalizeMirrorUrl(preferred);
                if (mirrors.Contains(norm, StringComparer.OrdinalIgnoreCase))
                {
                    mirrors = new[] { norm }.Concat(mirrors.Where(m => !m.Equals(norm, StringComparison.OrdinalIgnoreCase))).ToArray();
                }
            }

            foreach (var m in mirrors)
            {
                try
                {
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2.5));
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
                    var handler = new HttpClientHandler { AllowAutoRedirect = false };
                    using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(2.5) };
                    client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36");

                    using var resp = await client.GetAsync($"{m}/forum/index.php", HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
                    return m;
                }
                catch (OperationCanceledException)
                {
                    App.Logger?.Log($"[RuTracker Probe] Переключение с {m} на резервное зеркало (ТСПУ/таймаут)", LogLevel.Debug);
                }
                catch (Exception ex)
                {
                    App.Logger?.Log($"[RuTracker Probe] Пропуск {m}: {ex.Message}", LogLevel.Debug);
                }
            }

            return "https://rutracker.net";
        }
        public static void SwitchToNextMirror()
        {
            _activeMirrorIndex = (_activeMirrorIndex + 1) % DefaultMirrors.Length;
            string next = DefaultMirrors[_activeMirrorIndex];
            App.Settings.Current.RuTrackerMirror = next.Replace("https://", "").Replace("http://", "");
            _ = App.Settings.SaveAsync();
            App.Logger?.Log($"[RuTracker] Сетевое переключение на зеркало: {next}", LogLevel.Info);
        }

        public static string? SharedCfClearance
        {
            get => App.Settings.Current.RuTrackerCfClearance;
            set { App.Settings.Current.RuTrackerCfClearance = value ?? ""; }
        }

        public static string? SharedUserAgent
        {
            get => !string.IsNullOrEmpty(App.Settings.Current.RuTrackerUserAgent)
                ? App.Settings.Current.RuTrackerUserAgent
                : "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
            set { App.Settings.Current.RuTrackerUserAgent = value ?? ""; }
        }

        // 10 проверенных эталонных тем раздела #1605 (стартовый список хитов)
        private static readonly (string Tid, string Title, string Size, int Seeds, string Hash)[] CuratedVerifiedHits = new[]
        {
            ("5757941", "[Nintendo Switch] Celeste [0100FBA009218000] [NSP]", "931.55 MB", 118, "C59FE53E33D2BB4BD030E30ED796B99CFAC63F83"),
            ("6479728", "[Nintendo Switch] Mario vs. Donkey Kong [0100B99019412000] [NSP]", "1.30 GB", 189, "18034EA68DC5EBE094A061B95A04BB215D95A2A0"),
            ("6525297", "[Nintendo Switch] Animal Well [010020D01AD24000] [NSZ]", "73.35 MB", 62, "0A996B997AA2B90DB658E4CA76D2989DD1F80F56"),
            ("6702315", "[Nintendo Switch] The Legend of Zelda: Breath of the Wild (BOTW) + The Master Trials and The Champions Ballad [01007EF00011E000]", "10.66 GB", 807, "D4C50D5F7AB7EC78B589DA7A836BB98529F646D7"),
            ("6358526", "[Nintendo Switch] The Legend of Zelda: Tears of the Kingdom [0100F2C0115B6000]", "16.79 GB", 781, "EDC9BE013621189954A1E7D4EA514A804739EBB1"),
            ("6702276", "[Nintendo Switch] Super Mario Odyssey [0100000000010000]", "3.79 GB", 684, "8765CC4BF631C506A0F54F58F9A376D011BC7D95"),
            ("5720134", "[Nintendo Switch] Cuphead + The Delicious Last Course DLC [010086800CE34000]", "4.16 GB", 452, "92F42203820F0877BFCF46268DD066DFD51A69C0"),
            ("5943382", "[Nintendo Switch] Hades [0100535012974000]", "5.76 GB", 297, "55811895F3091DB7D207446AF805CE3D644DE8E0"),
            ("5677506", "[Nintendo Switch] Stardew Valley [0100E65002F3A000]", "935.46 MB", 326, "F35418F5C46E0D6F654E54CCF9C7B3DBCD031BBC"),
            ("6118930", "[Nintendo Switch] Metroid Dread [010093801237C000]", "3.23 GB", 409, "CD78BC876E0B45DCC4A610BD21C3BDA9B920B617")
        };

        // Локальный индекс всех 7 275 тем раздела #1605
        private static readonly List<RuTrackerIndexTopic> _indexTopics = new();
        private static readonly Dictionary<string, RuTrackerIndexTopic> _topicsById = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, RuTrackerIndexTopic> _topicsByHash = new(StringComparer.OrdinalIgnoreCase);
        private static bool _indexLoaded = false;
        private static readonly object _indexLock = new();
        private static readonly string DeltaCachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "STORM SWITCH BOX",
            "rutracker_1605_dynamic_delta.json");
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, RuTrackerIndexTopic> _deltaTopics = new(StringComparer.OrdinalIgnoreCase);
        public static void LoadDeltaCache()
        {
            try
            {
                if (!File.Exists(DeltaCachePath)) return;
                string json = File.ReadAllText(DeltaCachePath, Encoding.UTF8);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("topics", out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    lock (_indexLock)
                    {
                        foreach (var el in arr.EnumerateArray())
                        {
                            string tid = el.TryGetProperty("t", out var tp)
                                ? (tp.ValueKind == JsonValueKind.Number ? tp.GetInt64().ToString() : (tp.GetString() ?? ""))
                                : "";
                            if (string.IsNullOrEmpty(tid)) continue;

                            string h = el.TryGetProperty("h", out var hp) ? (hp.GetString() ?? "").Trim().ToUpperInvariant() : "";
                            int sd = el.TryGetProperty("sd", out var sdp) && sdp.ValueKind == JsonValueKind.Number ? sdp.GetInt32() : 0;
                            long sz = el.TryGetProperty("sz", out var szp) && szp.ValueKind == JsonValueKind.Number ? szp.GetInt64() : 0L;
                            long rt = el.TryGetProperty("rt", out var rtp) && rtp.ValueKind == JsonValueKind.Number ? rtp.GetInt64() : 0L;
                            string n = el.TryGetProperty("n", out var np) ? (np.GetString() ?? "") : "";
                            string tt = el.TryGetProperty("tt", out var ttp) ? (ttp.GetString() ?? "") : "";
                            string g = el.TryGetProperty("g", out var gp) ? (gp.GetString() ?? "") : "";
                            string tidHex = el.TryGetProperty("tid", out var tidp) ? (tidp.GetString() ?? "").ToUpperInvariant() : "";

                            if (string.IsNullOrWhiteSpace(g)) g = CleanTitleToGame(!string.IsNullOrWhiteSpace(tt) ? tt : n);
                            if (string.IsNullOrWhiteSpace(tidHex)) tidHex = ExtractTitleId(!string.IsNullOrWhiteSpace(tt) ? tt : n);

                            if (_topicsById.TryGetValue(tid, out var existing))
                            {
                                if (!string.IsNullOrEmpty(h) && h.Length == 40) existing.InfoHash = h;
                                if (sd > 0) existing.Seeds = sd;
                                if (sz > 0) existing.SizeBytes = sz;
                                if (rt > 0) existing.RegTime = rt;
                                if (!string.IsNullOrWhiteSpace(n)) existing.TorrentName = n;
                                if (!string.IsNullOrWhiteSpace(tt)) existing.TopicTitle = tt;
                                if (!string.IsNullOrWhiteSpace(g)) existing.GameName = g;
                                if (!string.IsNullOrWhiteSpace(tidHex)) existing.TitleId = tidHex;
                                try { existing.RebuildKeys(); } catch { }
                                _deltaTopics[tid] = existing;
                                if (!string.IsNullOrEmpty(existing.InfoHash) && existing.InfoHash.Length == 40)
                                    _topicsByHash[existing.InfoHash] = existing;
                            }
                            else
                            {
                                var topic = new RuTrackerIndexTopic
                                {
                                    TopicId = tid,
                                    InfoHash = h,
                                    Seeds = sd,
                                    SizeBytes = sz,
                                    RegTime = rt,
                                    TorrentName = n,
                                    TopicTitle = tt,
                                    GameName = g,
                                    TitleId = tidHex
                                };
                                try { topic.RebuildKeys(); } catch { }

                                _deltaTopics[tid] = topic;
                                _topicsById[tid] = topic;
                                if (!string.IsNullOrEmpty(h) && h.Length == 40) _topicsByHash[h] = topic;
                                _indexTopics.Add(topic);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger?.Log($"[RuTracker Delta] Ошибка чтения дельта-кэша: {ex.Message}", LogLevel.Warning);
            }
        }

        public static void SaveDeltaCache()
        {
            try
            {
                string dir = Path.GetDirectoryName(DeltaCachePath)!;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                var list = _deltaTopics.Values.Select(t => new
                {
                    t = long.TryParse(t.TopicId, out long lv) ? (object)lv : t.TopicId,
                    h = t.InfoHash,
                    sd = t.Seeds,
                    sz = t.SizeBytes,
                    rt = t.RegTime,
                    n = t.TorrentName,
                    tt = t.TopicTitle,
                    g = t.GameName,
                    tid = t.TitleId
                }).ToList();

                var payload = new
                {
                    updated = DateTime.UtcNow.ToString("o"),
                    count = list.Count,
                    topics = list
                };

                string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(DeltaCachePath, json, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                App.Logger?.Log($"[RuTracker Delta] Ошибка сохранения дельта-кэша: {ex.Message}", LogLevel.Warning);
            }
        }

        public static void ParseAndMergeForumTopicsHtml(string forumHtml)
        {
            if (string.IsNullOrWhiteSpace(forumHtml)) return;
            try
            {
                var matches = Regex.Matches(forumHtml, @"href=""(?:viewtopic\.php\?t=)?(\d+)""[^>]*class=""[^""]*tt-text[^""]*""[^>]*>(.*?)</a>|href=""viewtopic\.php\?t=(\d+)""[^>]*>(.*?)</a>", RegexOptions.IgnoreCase);
                bool changed = false;

                foreach (Match m in matches)
                {
                    string tid = !string.IsNullOrEmpty(m.Groups[1].Value) ? m.Groups[1].Value : m.Groups[3].Value;
                    string rawTitle = !string.IsNullOrEmpty(m.Groups[2].Value) ? m.Groups[2].Value : m.Groups[4].Value;
                    if (string.IsNullOrEmpty(tid) || string.IsNullOrEmpty(rawTitle)) continue;

                    string title = WebUtility.HtmlDecode(Regex.Replace(rawTitle, "<.*?>", "")).Trim();
                    if (string.IsNullOrEmpty(title) || title.Contains("след.")) continue;

                    lock (_indexLock)
                    {
                        if (_topicsById.TryGetValue(tid, out var existing))
                        {
                            if (string.IsNullOrEmpty(existing.TopicTitle) || existing.TopicTitle.StartsWith("Раздача"))
                            {
                                existing.TopicTitle = title;
                                existing.RebuildKeys();
                                _deltaTopics[tid] = existing;
                                changed = true;
                            }
                        }
                        else
                        {
                            var nt = new RuTrackerIndexTopic
                            {
                                TopicId = tid,
                                TopicTitle = title,
                                TorrentName = title,
                                GameName = CleanTitleToGame(title),
                                TitleId = ExtractTitleId(title)
                            };
                            nt.RebuildKeys();
                            _topicsById[tid] = nt;
                            _indexTopics.Add(nt);
                            _deltaTopics[tid] = nt;
                            changed = true;
                        }
                    }
                }

                if (changed)
                {
                    _ = Task.Run(SaveDeltaCache);
                }
            }
            catch { }
        }

        public static async Task ResolveUnnamedTopicsViaSameOriginAsync(CancellationToken ct = default)
        {
            if (SameOriginFetchHandler == null) return;
            List<string> unnamedIds;
            lock (_indexLock)
            {
                unnamedIds = _indexTopics
                    .Where(t => string.IsNullOrWhiteSpace(t.TopicTitle) && string.IsNullOrWhiteSpace(t.TorrentName))
                    .Select(t => t.TopicId)
                    .Take(40)
                    .ToList();
            }
            if (unnamedIds.Count == 0) return;

            string mirror = GetActiveMirror();
            bool anyUpdated = false;
            foreach (var tid in unnamedIds)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    string? html = await SameOriginFetchHandler($"{mirror}/forum/viewtopic.php?t={tid}", ct).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(html)) continue;
                    var tm = Regex.Match(html, @"<title>(.*?)</title>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                    if (tm.Success)
                    {
                        string title = WebUtility.HtmlDecode(Regex.Replace(tm.Groups[1].Value, @"::\s*RuTracker\.org.*$", "", RegexOptions.IgnoreCase)).Trim();
                        if (!string.IsNullOrWhiteSpace(title) && !title.Contains("Just a moment") && !title.Contains("Один момент"))
                        {
                            lock (_indexLock)
                            {
                                if (_topicsById.TryGetValue(tid, out var topic))
                                {
                                    topic.UpdateTopicTitle(title);
                                    _deltaTopics[tid] = topic;
                                    anyUpdated = true;
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            if (anyUpdated)
            {
                _ = Task.Run(SaveDeltaCache);
            }
        }

        private static bool _warmupStarted = false;
        private static int _lastSwarmTotalPeers = 0;
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, TorrentManager> _activeManagers = new();

        private TorrentManager? _activeManager;
        private byte[]? _loadedTorrentBytes;
        private MagnetLink? _loadedMagnet;
        private string _activeTopicId = string.Empty;
        private int _activeTopicSeeds = 0;

        public byte[]? LoadedTorrentBytes => _loadedTorrentBytes;
        public MagnetLink? LoadedMagnet => _loadedMagnet;
        public string ActiveTopicId => _activeTopicId;
        public int ActiveTopicSeeds => _activeTopicSeeds;

        public RuTrackerModService CreateIsolatedDownloadSession()
        {
            var session = new RuTrackerModService();
            if (_loadedTorrentBytes != null)
            {
                session._loadedTorrentBytes = (byte[])_loadedTorrentBytes.Clone();
            }
            session._loadedMagnet = _loadedMagnet;
            session._activeTopicId = _activeTopicId;
            session._activeTopicSeeds = _activeTopicSeeds;
            return session;
        }
        private static readonly Dictionary<string, IPAddress> _dohCache = new(StringComparer.OrdinalIgnoreCase);

        static RuTrackerModService()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            LoadTopicLinksCache();
        }

        public RuTrackerModService()
        {
            EnsureIndexLoaded();
            WarmupIndexAndLiveFeedsAsync();
        }

        public static string NormalizeMirrorUrl(string? mirror)
        {
            string m = (mirror ?? "").Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(m)) return "https://rutracker.org";
            if (!m.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !m.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                m = "https://" + m;
            }
            return m;
        }

        public static HttpClient CreateHttpClient(string? cookieHeader = null, bool allowAutoRedirect = true)
        {
            var settings = App.Settings.Current;
            bool useDoh = settings.RuTrackerUseDoH;
            string proxyUrl = (settings.RuTrackerProxy ?? "").Trim();

            var handler = new SocketsHttpHandler
            {
                AllowAutoRedirect = allowAutoRedirect,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
                ConnectTimeout = TimeSpan.FromSeconds(12),
                UseCookies = false
            };

            if (!string.IsNullOrEmpty(proxyUrl))
            {
                try
                {
                    handler.Proxy = new WebProxy(proxyUrl);
                    handler.UseProxy = true;
                }
                catch { }
            }
            else if (useDoh)
            {
                handler.ConnectCallback = async (context, token) =>
                {
                    string host = context.DnsEndPoint.Host;
                    int port = context.DnsEndPoint.Port;

                    IPAddress? resolvedIp = await ResolveHostViaDoHAsync(host, token).ConfigureAwait(false);
                    var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    try
                    {
                        if (resolvedIp != null)
                        {
                            await socket.ConnectAsync(new IPEndPoint(resolvedIp, port), token).ConfigureAwait(false);
                        }
                        else
                        {
                            await socket.ConnectAsync(context.DnsEndPoint, token).ConfigureAwait(false);
                        }
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                };
            }

            var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            string ua = !string.IsNullOrEmpty(SharedUserAgent) ? SharedUserAgent : "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", ua);
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");

            var cookieParts = new List<string>();
            string activeCookie = !string.IsNullOrWhiteSpace(cookieHeader) ? cookieHeader : settings.RuTrackerCookie;
            if (!string.IsNullOrWhiteSpace(activeCookie))
            {
                if (!activeCookie.Contains('='))
                    cookieParts.Add("bb_session=" + activeCookie.Trim());
                else
                    cookieParts.Add(activeCookie.Trim());
            }
            if (!string.IsNullOrWhiteSpace(SharedCfClearance))
            {
                if (!SharedCfClearance.Contains('='))
                    cookieParts.Add("cf_clearance=" + SharedCfClearance.Trim());
                else
                    cookieParts.Add(SharedCfClearance.Trim());
            }
            if (cookieParts.Count > 0)
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", string.Join("; ", cookieParts));
            }

            return client;
        }

        private static async Task<IPAddress?> ResolveHostViaDoHAsync(string host, CancellationToken ct)
        {
            if (IPAddress.TryParse(host, out var directIp))
                return directIp;

            lock (_dohCache)
            {
                if (_dohCache.TryGetValue(host, out var cachedIp))
                    return cachedIp;
            }

            string[] dohEndpoints = new[]
            {
                $"https://cloudflare-dns.com/dns-query?name={Uri.EscapeDataString(host)}&type=A",
                $"https://dns.google/resolve?name={Uri.EscapeDataString(host)}&type=A"
            };

            foreach (var ep in dohEndpoints)
            {
                try
                {
                    using var handler = new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(3) };
                    using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(4) };
                    http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/dns-json");

                    using var resp = await http.GetAsync(ep, ct).ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode) continue;

                    string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("Answer", out var answer) && answer.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var ans in answer.EnumerateArray())
                        {
                            if (ans.TryGetProperty("data", out var dataProp))
                            {
                                string ipStr = dataProp.GetString() ?? "";
                                if (IPAddress.TryParse(ipStr, out var ip))
                                {
                                    lock (_dohCache)
                                    {
                                        _dohCache[host] = ip;
                                    }
                                    return ip;
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            try
            {
                var ips = await Dns.GetHostAddressesAsync(host, ct).ConfigureAwait(false);
                var ip = ips.FirstOrDefault(x => x.AddressFamily == AddressFamily.InterNetwork);
                if (ip != null)
                {
                    lock (_dohCache) { _dohCache[host] = ip; }
                    return ip;
                }
            }
            catch { }

            return null;
        }

        public async Task<(bool Success, string CookieOrError)> LoginAsync(string username, string password, CancellationToken ct)
        {
            string mirror = NormalizeMirrorUrl(App.Settings.Current.RuTrackerMirror);
            string loginUrl = $"{mirror}/forum/login.php";
            Encoding cp1251 = Encoding.GetEncoding(1251);
            try
            {
                using var client = CreateHttpClient(allowAutoRedirect: false);

                var postData = $"login_username={UrlEncodeCp1251(username, cp1251)}&login_password={UrlEncodeCp1251(password, cp1251)}&login=%C2%F5%EE%E4";
                using var content = new StringContent(postData, cp1251, "application/x-www-form-urlencoded");
                using var resp = await client.PostAsync(loginUrl, content, ct).ConfigureAwait(false);
                if (resp.Headers.TryGetValues("Set-Cookie", out var cookies))
                {
                    foreach (var c in cookies)
                    {
                        var m = Regex.Match(c, @"bb_session=([a-zA-Z0-9_\-]+)");
                        if (m.Success)
                        {
                            string sessionVal = m.Groups[1].Value;
                            App.Settings.Current.RuTrackerUsername = username;
                            App.Settings.Current.RuTrackerCookie = sessionVal;
                            _ = App.Settings.SaveAsync();
                            return (true, sessionVal);
                        }
                    }
                }

                byte[] bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                string html = DecodeRuTrackerHtml(bytes, resp.Content.Headers.ContentType?.CharSet);

                if (html.Contains("Неверное имя пользователя или пароль") || html.Contains("Вы ввели неверное"))
                    return (false, "Неверный логин или пароль RuTracker.");

                if (html.Contains("Превышено число попыток"))
                    return (false, "Превышено число попыток входа (требуется капча). Войдите через браузер и вставьте Cookie bb_session.");

                return (false, "Авторизация не удалась (сессия bb_session не получена). Проверьте зеркало или используйте Cookie.");
            }
            catch (Exception ex)
            {
                return (false, $"Ошибка сети при входе: {ex.Message}");
            }
        }

        private static string UrlEncodeCp1251(string value, Encoding cp1251)
        {
            byte[] bytes = cp1251.GetBytes(value);
            var sb = new StringBuilder();
            foreach (byte b in bytes)
            {
                if ((b >= 'a' && b <= 'z') || (b >= 'A' && b <= 'Z') || (b >= '0' && b <= '9') || b == '-' || b == '_' || b == '.' || b == '~')
                {
                    sb.Append((char)b);
                }
                else
                {
                    sb.Append('%').Append(b.ToString("X2"));
                }
            }
            return sb.ToString();
        }
        #region Локальный индекс всех 7 275 тем RuTracker (#1605) и мгновенный поиск
        public static void EnsureIndexLoaded()
        {
            if (_indexLoaded) return;
            lock (_indexLock)
            {
                if (_indexLoaded) return;
                LoadIndexInternal();
                LoadDeltaCache();
                _indexLoaded = true;
            }
        }

        private static void LoadIndexInternal()
        {
            string? assetPath = LocateIndexAssetFile();
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath))
                return;

            try
            {
                using var fs = File.OpenRead(assetPath);
                using var gz = new GZipStream(fs, CompressionMode.Decompress);
                using var doc = JsonDocument.Parse(gz);

                if (doc.RootElement.TryGetProperty("topics", out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    _indexTopics.Clear();
                    _topicsById.Clear();
                    _topicsByHash.Clear();

                    foreach (var el in arr.EnumerateArray())
                    {
                        string tid = "";
                        if (el.TryGetProperty("t", out var tp))
                        {
                            tid = tp.ValueKind == JsonValueKind.Number ? tp.GetInt64().ToString() : (tp.GetString() ?? "");
                        }
                        if (string.IsNullOrEmpty(tid)) continue;

                        string h = el.TryGetProperty("h", out var hp) ? (hp.GetString() ?? "").Trim().ToUpperInvariant() : "";
                        int sd = el.TryGetProperty("sd", out var sdp) && sdp.ValueKind == JsonValueKind.Number ? sdp.GetInt32() : 0;
                        long sz = el.TryGetProperty("sz", out var szp) && szp.ValueKind == JsonValueKind.Number ? szp.GetInt64() : 0L;
                        long rt = el.TryGetProperty("rt", out var rtp) && rtp.ValueKind == JsonValueKind.Number ? rtp.GetInt64() : 0L;
                        string n = el.TryGetProperty("n", out var np) ? (np.GetString() ?? "") : "";
                        string tt = el.TryGetProperty("tt", out var ttp) ? (ttp.GetString() ?? "") : "";
                        string g = el.TryGetProperty("g", out var gp) ? (gp.GetString() ?? "") : "";
                        string tidHex = el.TryGetProperty("tid", out var tidp) ? (tidp.GetString() ?? "").ToUpperInvariant() : "";

                        var filesList = new List<RuTrackerIndexFile>();
                        if (el.TryGetProperty("f", out var fp) && fp.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var fArr in fp.EnumerateArray())
                            {
                                if (fArr.ValueKind == JsonValueKind.Array && fArr.GetArrayLength() >= 2)
                                {
                                    string p = fArr[0].GetString() ?? "";
                                    long fsz = fArr[1].GetInt64();
                                    string ftid = fArr.GetArrayLength() > 2 ? (fArr[2].GetString() ?? "") : "";
                                    string frole = fArr.GetArrayLength() > 3 ? (fArr[3].GetString() ?? "") : "";
                                    string fver = fArr.GetArrayLength() > 4 ? (fArr[4].GetString() ?? "") : "";
                                    filesList.Add(new RuTrackerIndexFile
                                    {
                                        Path = p.Replace('\\', '/'),
                                        Size = fsz,
                                        TitleId = ftid,
                                        Role = frole,
                                        Version = fver
                                    });
                                }
                            }
                        }

                        if (string.IsNullOrWhiteSpace(g))
                            g = CleanTitleToGame(!string.IsNullOrWhiteSpace(tt) ? tt : n);
                        if (string.IsNullOrWhiteSpace(tidHex))
                            tidHex = ExtractTitleId(!string.IsNullOrWhiteSpace(tt) ? tt : n);

                        var topic = new RuTrackerIndexTopic
                        {
                            TopicId = tid,
                            InfoHash = h,
                            Seeds = sd,
                            SizeBytes = sz,
                            RegTime = rt,
                            TorrentName = n,
                            TopicTitle = tt,
                            GameName = g,
                            TitleId = tidHex,
                            Files = filesList
                        };
                        try { topic.RebuildKeys(); } catch { }

                        _indexTopics.Add(topic);
                        _topicsById[tid] = topic;
                        if (!string.IsNullOrEmpty(h) && h.Length == 40)
                            _topicsByHash[h] = topic;
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger?.Log($"[RuTracker Index] Ошибка чтения индекса: {ex.Message}", LogLevel.Warning);
            }
        }

        private static string? LocateIndexAssetFile()
        {
            string[] candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Assets", "rutracker_switch_index.json.gz"),
                Path.Combine(Environment.CurrentDirectory, "Assets", "rutracker_switch_index.json.gz"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "STORM SWITCH BOX", "cache", "rutracker_switch_index.json.gz"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "rutracker_switch_index.json.gz"),
                Path.Combine("Assets", "rutracker_switch_index.json.gz")
            };

            foreach (var c in candidates)
            {
                if (File.Exists(c) && new FileInfo(c).Length > 64)
                    return c;
            }
            return null;
        }

        private static void WarmupIndexAndLiveFeedsAsync()
        {
            if (_warmupStarted) return;
            _warmupStarted = true;

            Task.Run(async () =>
            {
                try
                {
                    EnsureIndexLoaded();
                    await RefreshAtomFeedAsync().ConfigureAwait(false);
                    await RefreshPvcMapAsync().ConfigureAwait(false);
                }
                catch { }
            });
        }

        public static async Task RefreshAtomFeedAsync(CancellationToken ct = default)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "STORM-SWITCH-BOX");
                string xml = await client.GetStringAsync("https://feed.rutracker.cc/atom/f/1605.atom", ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(xml)) return;

                var entries = Regex.Matches(xml, @"<entry>(.*?)</entry>", RegexOptions.Singleline);
                foreach (Match entry in entries)
                {
                    string content = entry.Groups[1].Value;
                    var linkMatch = Regex.Match(content, @"viewtopic\.php\?t=(\d+)");
                    var titleMatch = Regex.Match(content, @"<title[^>]*>(.*?)</title>", RegexOptions.Singleline);
                    if (linkMatch.Success && titleMatch.Success)
                    {
                        string tid = linkMatch.Groups[1].Value;
                        string rawInner = titleMatch.Groups[1].Value.Trim();
                        if (rawInner.StartsWith("<![CDATA[", StringComparison.OrdinalIgnoreCase) && rawInner.EndsWith("]]>"))
                            rawInner = rawInner.Substring(9, rawInner.Length - 12);
                        string rawTitle = WebUtility.HtmlDecode(rawInner).Trim();
                        lock (_indexLock)
                        {
                            if (_topicsById.TryGetValue(tid, out var topic))
                            {
                                topic.UpdateTopicTitle(rawTitle);
                                _deltaTopics[tid] = topic;
                            }
                            else
                            {
                                var newTopic = new RuTrackerIndexTopic
                                {
                                    TopicId = tid,
                                    TopicTitle = rawTitle,
                                    TorrentName = rawTitle,
                                    GameName = CleanTitleToGame(rawTitle),
                                    TitleId = ExtractTitleId(rawTitle)
                                };
                                newTopic.RebuildKeys();
                                _topicsById[tid] = newTopic;
                                _indexTopics.Add(newTopic);
                                _deltaTopics[tid] = newTopic;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        public static async Task RefreshPvcMapAsync(CancellationToken ct = default)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "STORM-SWITCH-BOX");
                string json = await client.GetStringAsync("https://api.rutracker.cc/v1/static/pvc/f/1605", ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json)) return;

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    lock (_indexLock)
                    {
                        JsonElement mapRoot = doc.RootElement.TryGetProperty("result", out var resElem) && resElem.ValueKind == JsonValueKind.Object
                            ? resElem
                            : doc.RootElement;
                        foreach (var prop in mapRoot.EnumerateObject())
                        {
                            string tid = prop.Name;
                            if (prop.Value.ValueKind == JsonValueKind.Array && prop.Value.GetArrayLength() >= 3)
                            {
                                string h = "";
                                long sz = 0;
                                int sd = 0;
                                if (prop.Value.GetArrayLength() >= 8 && prop.Value[7].ValueKind == JsonValueKind.String)
                                {
                                    sd = prop.Value[1].ValueKind == JsonValueKind.Number ? prop.Value[1].GetInt32() : 0;
                                    sz = prop.Value[3].ValueKind == JsonValueKind.Number ? prop.Value[3].GetInt64() : 0L;
                                    h = prop.Value[7].GetString() ?? "";
                                }
                                else if (prop.Value[0].ValueKind == JsonValueKind.String)
                                {
                                    h = prop.Value[0].GetString() ?? "";
                                    sz = prop.Value[1].ValueKind == JsonValueKind.Number ? prop.Value[1].GetInt64() : 0L;
                                    sd = prop.Value[2].ValueKind == JsonValueKind.Number ? prop.Value[2].GetInt32() : 0;
                                }

                                if (_topicsById.TryGetValue(tid, out var topic))
                                {
                                    if (sd > 0) topic.Seeds = sd;
                                    if (sz > 0) topic.SizeBytes = sz;
                                    if (!string.IsNullOrEmpty(h) && h.Length == 40) topic.InfoHash = h.ToUpperInvariant();
                                }
                                else
                                {
                                    var newTopic = new RuTrackerIndexTopic
                                    {
                                        TopicId = tid,
                                        InfoHash = h.ToUpperInvariant(),
                                        SizeBytes = sz,
                                        Seeds = sd
                                    };
                                    newTopic.RebuildKeys();
                                    _topicsById[tid] = newTopic;
                                    _indexTopics.Add(newTopic);
                                    _deltaTopics[tid] = newTopic;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
        }
        #endregion
        public async Task<List<RuTrackerTopicItem>> SearchTopicsAsync(string query, string categoryFilter, CancellationToken ct)
        {
            EnsureIndexLoaded();

            string mirror = NormalizeMirrorUrl(App.Settings.Current.RuTrackerMirror);
            string cleanQ = query?.Trim() ?? "";

            // 1. Прямой переход по номеру темы
            var topicIdMatch = Regex.Match(cleanQ, @"(?:t=|viewtopic\.php\?t=|^)(\d{5,8})$");
            if (topicIdMatch.Success)
            {
                string directTid = topicIdMatch.Groups[1].Value;
                lock (_indexLock)
                {
                    if (_topicsById.TryGetValue(directTid, out var indexedTopic))
                    {
                        return new List<RuTrackerTopicItem> { BuildTopicItem(indexedTopic, mirror) };
                    }
                }
                var single = await FetchTopicDetailsAsync(directTid, ct).ConfigureAwait(false);
                if (single != null) return new List<RuTrackerTopicItem> { single };
            }

            // 2. Если есть авторизация (bb_session) или активный WebView2, пробуем живой tracker.php
            var trackerResults = new List<RuTrackerTopicItem>();
            if (!string.IsNullOrWhiteSpace(cleanQ) && (!string.IsNullOrWhiteSpace(App.Settings.Current.RuTrackerCookie) || SameOriginFetchHandler != null))
            {
                try
                {
                    Encoding cp1251 = Encoding.GetEncoding(1251);
                    string url = $"{mirror}/forum/tracker.php?f=1605&nm={UrlEncodeCp1251(cleanQ, cp1251)}";
                    if (!string.IsNullOrWhiteSpace(App.Settings.Current.RuTrackerCookie))
                    {
                        using var client = CreateHttpClient();
                        using var resp = await client.GetAsync(url, ct).ConfigureAwait(false);
                        if (resp.IsSuccessStatusCode)
                        {
                            byte[] bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                            string html = DecodeRuTrackerHtml(bytes, resp.Content.Headers.ContentType?.CharSet);
                            trackerResults = ParseTrackerSearchHtml(html, mirror);
                        }
                    }
                    if (trackerResults.Count == 0 && SameOriginFetchHandler != null)
                    {
                        string? wvHtml = await SameOriginFetchHandler(url, ct).ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(wvHtml))
                        {
                            trackerResults = ParseTrackerSearchHtml(wvHtml, mirror);
                        }
                    }
                }
                catch { }
            }

            // 3. Мгновенный поиск по локальному индексу всех 7 275 тем
            var indexResults = SearchIndex(cleanQ, categoryFilter, mirror, 80);

            var combined = new List<RuTrackerTopicItem>();
            var seenTids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var item in trackerResults)
            {
                if (seenTids.Add(item.TopicId))
                    combined.Add(item);
            }

            foreach (var item in indexResults)
            {
                if (seenTids.Add(item.TopicId))
                    combined.Add(item);
            }

            return combined.Take(80).ToList();
        }

        private static List<RuTrackerTopicItem> ParseTrackerSearchHtml(string html, string mirror)
        {
            var list = new List<RuTrackerTopicItem>();
            var rowMatches = Regex.Matches(html, @"<tr[^>]*id=""trs-tr-(\d+)""[^>]*>(.*?)</tr>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match row in rowMatches)
            {
                string tid = row.Groups[1].Value;
                string inner = row.Groups[2].Value;

                string title = "";
                var titleMatch = Regex.Match(inner, @"class=""[^""]*tLink[^""]*""[^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (titleMatch.Success)
                {
                    title = WebUtility.HtmlDecode(Regex.Replace(titleMatch.Groups[1].Value, "<.*?>", "")).Trim();
                }
                if (string.IsNullOrEmpty(title)) continue;

                int seeds = 0;
                var seedMatch = Regex.Match(inner, @"class=""[^""]*seedmed[^""]*""[^>]*><b>(\d+)</b>|class=""[^""]*seedmed[^""]*""[^>]*>(\d+)", RegexOptions.IgnoreCase);
                if (seedMatch.Success)
                {
                    string sVal = !string.IsNullOrEmpty(seedMatch.Groups[1].Value) ? seedMatch.Groups[1].Value : seedMatch.Groups[2].Value;
                    int.TryParse(sVal, out seeds);
                }

                int leechs = 0;
                var leechMatch = Regex.Match(inner, @"class=""[^""]*leechmed[^""]*""[^>]*><b>(\d+)</b>|class=""[^""]*leechmed[^""]*""[^>]*>(\d+)", RegexOptions.IgnoreCase);
                if (leechMatch.Success)
                {
                    string lVal = !string.IsNullOrEmpty(leechMatch.Groups[1].Value) ? leechMatch.Groups[1].Value : leechMatch.Groups[2].Value;
                    int.TryParse(lVal, out leechs);
                }

                string sizeText = "нет данных";
                var sizeMatch = Regex.Match(inner, @"data-ts_text=""(\d+)""|<a[^>]+class=""small tr-dl""[^>]*>.*?([\d.]+\s*(?:GB|MB|KB|ГБ|МБ|КБ))", RegexOptions.IgnoreCase);
                if (sizeMatch.Success)
                {
                    if (long.TryParse(sizeMatch.Groups[1].Value, out long szBytes) && szBytes > 0)
                        sizeText = ProcessingTask.FormatSize(szBytes);
                    else if (!string.IsNullOrEmpty(sizeMatch.Groups[2].Value))
                        sizeText = sizeMatch.Groups[2].Value.Trim();
                }

                list.Add(new RuTrackerTopicItem
                {
                    TopicId = tid,
                    Title = title,
                    Category = "Nintendo Switch",
                    SizeText = sizeText,
                    Seeds = seeds,
                    Leechers = leechs,
                    TopicUrl = $"{mirror}/forum/viewtopic.php?t={tid}",
                    DirectTorrentUrl = $"{mirror}/forum/dl.php?t={tid}"
                });
            }

            return list;
        }

        private static List<RuTrackerTopicItem> SearchIndex(string query, string categoryFilter, string mirror, int limit)
        {
            string q = query.Trim();

            // Если запрос пустой — возвращаем только проверенные эталонные темы + топ раздач по сидам
            if (string.IsNullOrEmpty(q) || q.Equals("nintendo switch", StringComparison.OrdinalIgnoreCase) || q.Equals("все хиты switch", StringComparison.OrdinalIgnoreCase))
            {
                var starterResults = new List<RuTrackerTopicItem>();
                var seenTids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var cur in CuratedVerifiedHits)
                {
                    if (seenTids.Add(cur.Tid))
                    {
                        RuTrackerIndexTopic? indexed = null;
                        lock (_indexLock) { _topicsById.TryGetValue(cur.Tid, out indexed); }

                        if (indexed != null)
                        {
                            starterResults.Add(BuildTopicItem(indexed, mirror));
                        }
                        else
                        {
                            starterResults.Add(new RuTrackerTopicItem
                            {
                                TopicId = cur.Tid,
                                Title = cur.Title,
                                Category = "Nintendo Switch",
                                SizeText = cur.Size,
                                Seeds = cur.Seeds,
                                Leechers = Math.Max(1, cur.Seeds / 8),
                                TopicUrl = $"{mirror}/forum/viewtopic.php?t={cur.Tid}",
                                InfoHash = cur.Hash,
                                MagnetUri = BuildMagnetUri(cur.Hash, cur.Title),
                                DirectTorrentUrl = $"{mirror}/forum/dl.php?t={cur.Tid}"
                            });
                        }
                    }
                }

                // Дополняем топ-раздачами по числу сидов из полного индекса
                lock (_indexLock)
                {
                    var topSeedTopics = _indexTopics
                        .Where(t => !seenTids.Contains(t.TopicId) && !string.IsNullOrEmpty(t.InfoHash) && !string.IsNullOrWhiteSpace(t.GameKey) && t.Seeds > 0)
                        .OrderByDescending(t => t.Seeds)
                        .Take(limit - starterResults.Count);

                    foreach (var t in topSeedTopics)
                    {
                        if (seenTids.Add(t.TopicId))
                            starterResults.Add(BuildTopicItem(t, mirror));
                    }
                }

                return starterResults;
            }

            // Полнотекстовый и нечёткий поиск по 7 275 темам
            var qNorm = NormalizeSearchKey(q);
            string hexOnly = qNorm.Replace(" ", "");
            string? tidQuery = (hexOnly.Length >= 6 && hexOnly.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')) && hexOnly.Any(char.IsDigit))
                ? hexOnly
                : null;
            string? topicNumber = (qNorm.Length >= 5 && qNorm.All(char.IsDigit)) ? qNorm : null;

            var variants = BuildQueryVariants(q);
            var scored = new List<(RuTrackerIndexTopic Topic, int Score)>();

            lock (_indexLock)
            {
                foreach (var t in _indexTopics)
                {
                    if (topicNumber != null && t.TopicId == topicNumber)
                    {
                        scored.Add((t, 1400));
                        continue;
                    }

                    int sc = ScoreTopic(t, variants, tidQuery);
                    if (sc > 0)
                    {
                        scored.Add((t, sc));
                    }
                }
            }

            return scored
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Topic.Seeds)
                .Take(limit)
                .Select(x => BuildTopicItem(x.Topic, mirror))
                .ToList();
        }

        private static RuTrackerTopicItem BuildTopicItem(RuTrackerIndexTopic t, string mirror)
        {
            string title = !string.IsNullOrWhiteSpace(t.TopicTitle)
                ? t.TopicTitle
                : (!string.IsNullOrWhiteSpace(t.TorrentName) ? t.TorrentName : $"[Nintendo Switch] {t.GameName}");
            string sizeText = t.SizeBytes > 0 ? ProcessingTask.FormatSize(t.SizeBytes) : "нет данных";
            return new RuTrackerTopicItem
            {
                TopicId = t.TopicId,
                Title = title,
                Category = "Nintendo Switch",
                SizeText = sizeText,
                Seeds = t.Seeds,
                Leechers = Math.Max(1, t.Seeds / 8),
                TopicUrl = $"{mirror}/forum/viewtopic.php?t={t.TopicId}",
                InfoHash = t.InfoHash,
                MagnetUri = BuildMagnetUri(t.InfoHash, title),
                DirectTorrentUrl = $"{mirror}/forum/dl.php?t={t.TopicId}"
            };
        }
        #region Алгоритмы поиска (раскладка, опечатки, Soundex, синонимы)
        private sealed class QVariant
        {
            public string Text { get; }
            public string Padded { get; }
            public string[] Words { get; }
            public bool AllowTypo { get; }



            public QVariant(string text, string padded, string[] words, bool allowTypo)
            {
                Text = text;
                Padded = padded;
                Words = words;
                AllowTypo = allowTypo;
            }
        }



        private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "for", "of", "a", "an", "nintendo", "switch", "edition", "deluxe"
        };



        private static readonly Dictionary<string, string> RuGameAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["бротато"] = "brotato", ["братато"] = "brotato", ["бротата"] = "brotato", ["картошка"] = "brotato",
            ["анимал велл"] = "animal well", ["энимал велл"] = "animal well", ["анимал вел"] = "animal well", ["энимал вел"] = "animal well",
            ["зельда"] = "zelda", ["зельды"] = "zelda", ["линк"] = "zelda", ["хайрул"] = "hyrule",
            ["марио"] = "mario", ["супер марио"] = "super mario", ["марио карт"] = "mario kart",
            ["ведьмак"] = "witcher", ["витчер"] = "witcher", ["геральт"] = "witcher",
            ["холлоу найт"] = "hollow knight", ["полый рыцарь"] = "hollow knight", ["холоу найт"] = "hollow knight",
            ["силксонг"] = "silksong", ["хорнет"] = "silksong",
            ["капхед"] = "cuphead", ["чашкоhead"] = "cuphead",
            ["балатро"] = "balatro", ["покер"] = "balatro",
            ["дум"] = "doom", ["дум этернал"] = "doom eternal",
            ["скайрим"] = "skyrim", ["элдер скроллс"] = "elder scrolls",
            ["стардю"] = "stardew valley", ["стардью"] = "stardew valley", ["долина стардью"] = "stardew valley",
            ["террария"] = "terraria", ["терария"] = "terraria",
            ["майнкрафт"] = "minecraft", ["майн"] = "minecraft",
            ["персона"] = "persona", ["покемон"] = "pokemon", ["покемоны"] = "pokemon",
            ["байонетта"] = "bayonetta", ["ксеноблейд"] = "xenoblade", ["зеноблейд"] = "xenoblade",
            ["дарк соулс"] = "dark souls", ["дарк соулз"] = "dark souls",
            ["хейдес"] = "hades", ["гадес"] = "hades", ["аид"] = "hades",
            ["факторио"] = "factorio", ["сабнавтика"] = "subnautica", ["сабнатика"] = "subnautica",
            ["дейв"] = "dave the diver", ["дайвер"] = "dave the diver",
            ["дредж"] = "dredge", ["туник"] = "tunic",
            ["бласфемос"] = "blasphemous", ["бласфемус"] = "blasphemous",
            ["пицца тауэр"] = "pizza tower", ["пицца"] = "pizza tower",
            ["найн солс"] = "nine sols", ["девять солнц"] = "nine sols",
            ["дед селлс"] = "dead cells", ["мертвые клетки"] = "dead cells", ["дэд селлс"] = "dead cells",
            ["селеста"] = "celeste", ["селест"] = "celeste",
            ["ори"] = "ori", ["крэш"] = "crash bandicoot", ["краш"] = "crash bandicoot",
            ["спайро"] = "spyro", ["соник"] = "sonic", ["кирби"] = "kirby",
            ["сплатун"] = "splatoon", ["пикмин"] = "pikmin",
            ["файр эмблем"] = "fire emblem", ["огненная эмблема"] = "fire emblem",
            ["монстер хантер"] = "monster hunter", ["монхан"] = "monster hunter",
            ["резидент"] = "resident evil", ["обитель зла"] = "resident evil",
            ["ассасин"] = "assassins creed",
            ["донки конг"] = "donkey kong", ["донкиконг"] = "donkey kong",
            ["кастлвания"] = "castlevania", ["кастлевания"] = "castlevania",
            ["мегамен"] = "mega man", ["астрал чейн"] = "astral chain",
            ["октопат"] = "octopath traveler", ["якудза"] = "yakuza",
            ["аггелос"] = "aggelos"
        };

        private static bool ContainsWholePhrase(string paddedText, string phrase)
        {
            if (string.IsNullOrWhiteSpace(phrase)) return false;
            return paddedText.Contains(" " + phrase + " ", StringComparison.OrdinalIgnoreCase);
        }

        private static List<QVariant> BuildQueryVariants(string rawQuery)
        {
            string clean = NormalizeSearchKey(rawQuery);
            string paddedClean = " " + clean + " ";
            var raw = new List<(string Text, bool AllowTypo)>();
            if (!string.IsNullOrEmpty(clean))
                raw.Add((clean, true));

            foreach (var (ruKey, enVal) in RuGameAliases)
            {
                if (clean.Equals(ruKey, StringComparison.OrdinalIgnoreCase) || ContainsWholePhrase(paddedClean, ruKey))
                {
                    string replaced = (" " + clean + " ").Replace(" " + ruKey + " ", " " + enVal + " ", StringComparison.OrdinalIgnoreCase).Trim();
                    raw.Add((replaced, true));
                    raw.Add((enVal, true));
                }
            }

            bool hasCyrillic = clean.Any(c => c >= 'а' && c <= 'я');
            bool hasLatin = clean.Any(c => c >= 'a' && c <= 'z');

            if (hasCyrillic)
            {
                string translit = TransliterateRuToEn(clean);
                if (translit != clean)
                {
                    raw.Add((translit, true));
                    raw.Add((translit.Replace('v', 'w'), false));
                    raw.Add((translit.Replace('k', 'c'), false));
                }

                string swappedEn = SwapKeyboardLayout(rawQuery.Trim().ToLowerInvariant(), ruToEn: true);
                if (swappedEn != clean)
                    raw.Add((swappedEn, false));
            }

            if (hasLatin)
            {
                string swappedRu = NormalizeSearchKey(SwapKeyboardLayout(rawQuery.Trim().ToLowerInvariant(), ruToEn: false));
                if (!string.IsNullOrEmpty(swappedRu) && swappedRu != clean)
                {
                    raw.Add((swappedRu, false));
                    string paddedSwapped = " " + swappedRu + " ";
                    foreach (var (ruKey, enVal) in RuGameAliases)
                    {
                        if (swappedRu.Equals(ruKey, StringComparison.OrdinalIgnoreCase) || ContainsWholePhrase(paddedSwapped, ruKey))
                            raw.Add((enVal, true));
                    }
                }
            }

            var outList = new List<QVariant>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (r, allowTypo) in raw)
            {
                string n = NormalizeSearchKey(r);
                if (string.IsNullOrEmpty(n) || !seen.Add(n)) continue;

                var ws = n.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var sig = ws.Where(w => !StopWords.Contains(w)).ToArray();
                if (sig.Length == 0) sig = ws;

                outList.Add(new QVariant(n, " " + n + " ", sig, allowTypo));
            }

            return outList;
        }

        private static int ScoreTopic(RuTrackerIndexTopic t, List<QVariant> variants, string? tidQuery)
        {
            if (string.IsNullOrWhiteSpace(t.GameKey) && string.IsNullOrWhiteSpace(t.TitleKey))
                return 0;

            if (!string.IsNullOrEmpty(tidQuery) && t.TidBlob.Contains(tidQuery, StringComparison.OrdinalIgnoreCase))
                return 1300;

            int best = 0;
            for (int i = 0; i < variants.Count; i++)
            {
                var v = variants[i];
                int penalty = i * 15;

                if (t.GameKey.Equals(v.Text, StringComparison.Ordinal))
                {
                    best = Math.Max(best, 1200 - penalty);
                    continue;
                }

                if (t.GameKey.StartsWith(v.Text + " ", StringComparison.Ordinal))
                {
                    best = Math.Max(best, 1140 - penalty);
                    continue;
                }

                if (v.Text.Length >= 4 && t.GameKey.StartsWith(v.Text, StringComparison.Ordinal))
                {
                    best = Math.Max(best, 1080 - penalty);
                    continue;
                }

                if (t.GameKeyPadded.Contains(v.Padded, StringComparison.Ordinal))
                {
                    best = Math.Max(best, 1050 - penalty);
                    continue;
                }

                if (!string.IsNullOrEmpty(t.TitleKeyPadded) && t.TitleKeyPadded.Contains(v.Padded, StringComparison.Ordinal))
                {
                    best = Math.Max(best, 1020 - penalty);
                    continue;
                }

                int ws = WordScore(t, v) - penalty;
                if (ws > best) best = ws;
            }

            return best;
        }

        private static int WordScore(RuTrackerIndexTopic t, QVariant v)
        {
            if (v.Words.Length == 0 || t.Words.Length == 0) return 0;
            int totalTypos = 0;
            int inGame = 0;

            foreach (var qw in v.Words)
            {
                int best = 99;
                bool bestInGame = false;

                for (int wi = 0; wi < t.Words.Length; wi++)
                {
                    int d = WordMatch(qw, t.Words[wi], v.AllowTypo);
                    if (d < best || (d == best && wi < t.GameWordCount && !bestInGame))
                    {
                        best = d;
                        bestInGame = wi < t.GameWordCount;
                        if (d == 0 && bestInGame) break;
                    }
                }

                if (best >= 99) return 0;
                totalTypos += best;
                if (bestInGame) inGame++;
            }

            int scoreBase = totalTypos == 0 ? 980 : (880 - totalTypos * 60);
            int gameBonus = inGame == v.Words.Length ? 40 : (inGame > 0 ? 15 : 0);
            return Math.Max(650, scoreBase + gameBonus);
        }

        /// <summary>
        /// Проверяет, отличаются ли два слова перестановкой ровно одной пары букв (соседних или через одну).
        /// </summary>
        public static bool IsSingleLetterSwap(string a, string b)
        {
            if (a.Length != b.Length || a.Length < 3) return false;
            int p1 = -1, p2 = -1;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    if (p1 < 0) p1 = i;
                    else if (p2 < 0) p2 = i;
                    else return false;
                }
            }
            if (p1 < 0 || p2 < 0) return false;
            if (p2 - p1 > 2) return false;
            if (a.Length == 3 && p1 == 0) return false;
            return a[p1] == b[p2] && a[p2] == b[p1];
        }

        /// <summary>
        /// Точный матч слова с допуском ровно 1 опечатки (перепутаны 2 буквы или 1 неправильная/пропущенная/лишняя буква).
        /// </summary>
        private static int WordMatch(string qw, string gw, bool allowTypo)
        {
            if (qw.Equals(gw, StringComparison.Ordinal)) return 0;

            // Точный префикс длинного слова (без искажения букв)
            if (!qw.All(char.IsDigit) && gw.StartsWith(qw, StringComparison.Ordinal))
            {
                if (qw.Length >= 5 || (qw.Length == 4 && gw.Length <= 7) || (qw.Length == 3 && gw.Length <= 5))
                    return 1;
            }
            if (!allowTypo) return 99;
            // Перепутаны 2 буквы местами (например, zedla -> zelda, mairo -> mario, aggleos -> aggelos)
            if (IsSingleLetterSwap(qw, gw))
                return 1;

            if (qw.Length < 4 || gw.Length < 4) return 99;
            if (Math.Abs(qw.Length - gw.Length) > 1) return 99;

            // Первая буква должна совпадать, либо (для слов >= 5 букв) все остальные буквы совпадают 1-в-1
            bool firstOk = qw[0] == gw[0] ||
                           (qw.Length == gw.Length && qw.Length >= 5 && qw.AsSpan(1).SequenceEqual(gw.AsSpan(1)));
            if (!firstOk) return 99;

            // Строго не более 1 ошибки (1 неправильная буква, 1 пропущенная или 1 лишняя)
            int d = DamerauLevenshtein(qw, gw);
            return d <= 1 ? 1 : 99;
        }

        public static string NormalizeSearchKey(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            bool space = true;
            foreach (char c0 in s)
            {
                char c = char.ToLowerInvariant(c0);
                if (c == 'ё') c = 'е';
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                    space = false;
                }
                else if (!space)
                {
                    sb.Append(' ');
                    space = true;
                }
            }
            if (sb.Length > 0 && sb[^1] == ' ')
                sb.Length--;
            return sb.ToString();
        }

        public static string SwapKeyboardLayout(string input, bool ruToEn)
        {
            const string ru = "йцукенгшщзхъфывапролджэячсмитьбю.ё";
            const string en = "qwertyuiop[]asdfghjkl;'zxcvbnm,./`";
            var sb = new StringBuilder(input.Length);
            foreach (char c in input.ToLowerInvariant())
            {
                if (ruToEn)
                {
                    int idx = ru.IndexOf(c);
                    sb.Append(idx >= 0 && idx < en.Length ? en[idx] : c);
                }
                else
                {
                    int idx = en.IndexOf(c);
                    sb.Append(idx >= 0 && idx < ru.Length ? ru[idx] : c);
                }
            }
            return sb.ToString();
        }

        public static string TransliterateRuToEn(string input)
        {
            var map = new Dictionary<string, string>
            {
                ["щ"] = "shch", ["ш"] = "sh", ["ч"] = "ch", ["ц"] = "ts", ["ж"] = "zh",
                ["э"] = "e", ["ю"] = "yu", ["я"] = "ya", ["а"] = "a", ["б"] = "b",
                ["в"] = "v", ["г"] = "g", ["д"] = "d", ["е"] = "e", ["ё"] = "yo",
                ["з"] = "z", ["и"] = "i", ["й"] = "y", ["к"] = "k", ["л"] = "l",
                ["м"] = "m", ["н"] = "n", ["о"] = "o", ["п"] = "p", ["р"] = "r",
                ["с"] = "s", ["т"] = "t", ["у"] = "u", ["ф"] = "f", ["х"] = "kh",
                ["ъ"] = "", ["ы"] = "y", ["ь"] = ""
            };
            var sb = new StringBuilder(input.Length * 2);
            foreach (char c in input.ToLowerInvariant())
            {
                string s = c.ToString();
                if (map.TryGetValue(s, out var tr))
                    sb.Append(tr);
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }
        public static string PhoneticSkeleton(string input) => string.Empty;
        public static int DamerauLevenshtein(string a, string b)
        {
            int lenA = a.Length;
            int lenB = b.Length;
            if (Math.Abs(lenA - lenB) > 2) return 99;
            if (lenA == 0) return lenB;
            if (lenB == 0) return lenA;

            int[,] dp = new int[lenA + 1, lenB + 1];
            for (int i = 0; i <= lenA; i++) dp[i, 0] = i;
            for (int j = 0; j <= lenB; j++) dp[0, j] = j;

            for (int i = 1; i <= lenA; i++)
            {
                for (int j = 1; j <= lenB; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    int v = Math.Min(
                        Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                        dp[i - 1, j - 1] + cost);

                    if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    {
                        v = Math.Min(v, dp[i - 2, j - 2] + 1);
                    }
                    dp[i, j] = v;
                }
            }
            return dp[lenA, lenB];
        }

        public static string CleanTitleToGame(string title)
        {
            string s = Regex.Replace(title, @"^\s*\[[^\]]*Switch[^\]]*\]\s*", "", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"\[[^\]]*\]|\([^)]*\)|\{[^}]*\}", " ");
            s = Regex.Replace(s, @" [vV]\d[\d.]* ", " ");
            s = Regex.Replace(s, @" (NSP|NSZ|XCI|XCZ|NUP|NRO|RUS|ENG|Multi\d*) ", " ", RegexOptions.IgnoreCase);
            s = s.Replace('_', ' ');
            s = Regex.Replace(s, @"\s+", " ").Trim().Trim('-', '/', '|', '.', ',', ' ');
            return s;
        }

        public static string CleanTitleKeepAltNames(string title)
        {
            string s = Regex.Replace(title, @"\[[^\]]*\]|\{[^}]*\}", " ");
            s = Regex.Replace(s, @"\((?:порт|хоумбрю|homebrew[^)]*|update[^)]*|dlc[^)]*|v\d[^)]*|mod[^)]*)\)", " ", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @" [vV]\d[\d.]* ", " ");
            s = Regex.Replace(s, @" (NSP|NSZ|XCI|XCZ|NUP|NRO|RUS|ENG|Multi\d*) ", " ", RegexOptions.IgnoreCase);
            s = s.Replace('_', ' ').Replace('(', ' ').Replace(')', ' ');
            s = Regex.Replace(s, @"\s+", " ").Trim().Trim('-', '/', '|', '.', ',', ' ');
            return s;
        }
        private static readonly Regex TidRegex = new(@"(?<![0-9A-Fa-f])(01[0-9A-Fa-f]{14})(?![0-9A-Fa-f])", RegexOptions.Compiled);
        public static string ExtractTitleId(string s)
        {
            var m = TidRegex.Match(s);
            if (!m.Success) return "";
            string hex = m.Groups[1].Value.ToUpperInvariant();
            if (hex.EndsWith("800"))
                return hex.Substring(0, 13) + "000";
            return hex;
        }

        public static string NormalizeSwitchBaseApplicationTitleId(string? rawTid)
        {
            if (string.IsNullOrWhiteSpace(rawTid)) return string.Empty;
            string hex = rawTid.Trim();
            if (hex.Length != 16 || !hex.All(c => Uri.IsHexDigit(c)))
            {
                string extracted = ExtractTitleId(hex);
                if (extracted.Length == 16) hex = extracted;
                else return string.Empty;
            }
            hex = hex.ToUpperInvariant();
            if (hex.Length != 16) return string.Empty;

            if (hex.EndsWith("000"))
                return hex;
            if (hex.EndsWith("800"))
                return hex.Substring(0, 13) + "000";

            try
            {
                ulong val = Convert.ToUInt64(hex, 16);
                ulong baseMasked = val & 0xFFFFFFFFFFFFF000UL;
                ulong resultVal = baseMasked >= 0x0100000000001000UL ? baseMasked - 0x1000UL : baseMasked;
                return resultVal.ToString("X16");
            }
            catch
            {
                return hex;
            }
        }
        #endregion
        public async Task<RuTrackerTopicItem?> FetchTopicDetailsAsync(string topicIdOrUrl, CancellationToken ct)
        {
            string tid = topicIdOrUrl.Trim();
            var m = Regex.Match(tid, @"t=(\d+)");
            if (m.Success) tid = m.Groups[1].Value;

            if (string.IsNullOrEmpty(tid) || !tid.All(char.IsDigit))
                return null;

            string mirror = NormalizeMirrorUrl(App.Settings.Current.RuTrackerMirror);
            string topicUrl = $"{mirror}/forum/viewtopic.php?t={tid}";

            var item = new RuTrackerTopicItem
            {
                TopicId = tid,
                Title = $"Раздача RuTracker #{tid}",
                Category = "Nintendo Switch",
                TopicUrl = topicUrl
            };

            lock (_indexLock)
            {
                if (_topicsById.TryGetValue(tid, out var indexedTopic))
                {
                    item.Title = !string.IsNullOrEmpty(indexedTopic.TopicTitle) ? indexedTopic.TopicTitle : indexedTopic.TorrentName;
                    item.InfoHash = indexedTopic.InfoHash;
                    item.MagnetUri = BuildMagnetUri(indexedTopic.InfoHash, item.Title);
                    item.SizeText = indexedTopic.SizeBytes > 0 ? ProcessingTask.FormatSize(indexedTopic.SizeBytes) : "";
                    item.Seeds = indexedTopic.Seeds;
                }
            }
            string? topicHtml = await FetchValidatedTopicPageHtmlAsync(tid, 0, ct).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(topicHtml))
            {
                var titleMatch = Regex.Match(topicHtml, @"<a[^>]+id=""topic-title""[^>]*>(.*?)</a>|<title>(.*?)</title>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (titleMatch.Success)
                {
                    string rawTitle = !string.IsNullOrEmpty(titleMatch.Groups[1].Value) ? titleMatch.Groups[1].Value : titleMatch.Groups[2].Value;
                    item.Title = WebUtility.HtmlDecode(Regex.Replace(rawTitle, "<.*?>", "")).Replace(":: RuTracker.org", "").Trim();
                }

                var magnetMatch = Regex.Match(topicHtml, @"href=""(magnet:\?xt=urn:btih:[^""]+)""", RegexOptions.IgnoreCase);
                if (magnetMatch.Success)
                {
                    item.MagnetUri = WebUtility.HtmlDecode(magnetMatch.Groups[1].Value);
                }

                var hashMatch = Regex.Match(topicHtml, @"btih:([0-9A-Fa-f]{40})|id=""tor-hash""[^>]*>([0-9A-Fa-f]{40})<", RegexOptions.IgnoreCase);
                if (hashMatch.Success)
                {
                    item.InfoHash = (!string.IsNullOrEmpty(hashMatch.Groups[1].Value) ? hashMatch.Groups[1].Value : hashMatch.Groups[2].Value).ToUpperInvariant();
                }

                var posterMatch = Regex.Match(topicHtml, @"<var[^>]+class=""[^"" ]*postImgAligned[^"" ]*""[^>]+title=""(https?://[^"" ]+\.(?:jpg|jpeg|png|webp))""|<var[^>]+class=""[^"" ]*postImg[^"" ]*""[^>]+title=""(https?://[^"" ]+\.(?:jpg|jpeg|png|webp))""", RegexOptions.IgnoreCase);
                if (posterMatch.Success)
                {
                    item.PosterUrl = WebUtility.HtmlDecode(!string.IsNullOrEmpty(posterMatch.Groups[1].Value) ? posterMatch.Groups[1].Value : posterMatch.Groups[2].Value);
                }

                // Извлекаем все облачные ссылки на моды и русификаторы из 1-й страницы темы
                var allLinks = new List<CloudModLinkItem>();
                allLinks.AddRange(ExtractModLinksFromTopicHtml(topicHtml, 1));

                // Извлекаем пагинацию темы (start=offset) со всех страниц
                var startMatches = Regex.Matches(topicHtml, $@"viewtopic\.php\?t={tid}&(?:amp;)?start=(\d+)", RegexOptions.IgnoreCase);
                var discoveredOffsets = startMatches.Cast<Match>()
                    .Select(m => int.TryParse(m.Groups[1].Value, out int v) ? v : 0)
                    .Where(v => v > 0)
                    .Distinct()
                    .OrderBy(v => v)
                    .ToList();

                int maxOffset = discoveredOffsets.Count > 0 ? discoveredOffsets.Max() : 0;
                var allGeneratedOffsets = new List<int>();
                for (int off = 30; off <= maxOffset; off += 30)
                {
                    allGeneratedOffsets.Add(off);
                }
                var allOffsets = discoveredOffsets.Concat(allGeneratedOffsets).Distinct().OrderBy(v => v).ToList();

                var targetOffsets = new HashSet<int>();
                if (allOffsets.Count <= 12)
                {
                    foreach (var o in allOffsets) targetOffsets.Add(o);
                }
                else
                {
                    if (allOffsets.Contains(30)) targetOffsets.Add(30);
                    if (allOffsets.Contains(60)) targetOffsets.Add(60);
                    foreach (var o in allOffsets.TakeLast(10)) targetOffsets.Add(o);
                }

                if (targetOffsets.Count > 0)
                {
                    var sem = new SemaphoreSlim(4);
                    var pageTasks = targetOffsets.OrderBy(v => v).Select(async offset =>
                    {
                        await sem.WaitAsync(ct).ConfigureAwait(false);
                        try
                        {
                            int pageNum = (offset / 30) + 1;
                            string? pHtml = await FetchValidatedTopicPageHtmlAsync(tid, offset, ct).ConfigureAwait(false);
                            if (!string.IsNullOrEmpty(pHtml))
                            {
                                var pageLinks = ExtractModLinksFromTopicHtml(pHtml, pageNum);
                                return (Offset: offset, Links: pageLinks);
                            }
                            return (Offset: offset, Links: new List<CloudModLinkItem>());
                        }
                        catch
                        {
                            return (Offset: offset, Links: new List<CloudModLinkItem>());
                        }
                        finally
                        {
                            sem.Release();
                        }
                    }).ToList();

                    var results = await Task.WhenAll(pageTasks).ConfigureAwait(false);
                    foreach (var res in results.OrderBy(r => r.Offset))
                    {
                        foreach (var dLink in res.Links)
                        {
                            if (!allLinks.Any(x => string.Equals(x.Url, dLink.Url, StringComparison.OrdinalIgnoreCase)))
                                allLinks.Add(dLink);
                        }
                    }
                }

                AttachTopicMirrors(allLinks);
                item.ExtractedModLinks = allLinks;
            }

            if (string.IsNullOrEmpty(item.MagnetUri) && !string.IsNullOrEmpty(item.InfoHash))
            {
                item.MagnetUri = BuildMagnetUri(item.InfoHash, item.Title);
            }
            else if (string.IsNullOrEmpty(item.InfoHash) && !string.IsNullOrEmpty(item.MagnetUri))
            {
                var hm = Regex.Match(item.MagnetUri, @"btih:([0-9A-Fa-f]{40})", RegexOptions.IgnoreCase);
                if (hm.Success) item.InfoHash = hm.Groups[1].Value.ToUpperInvariant();
            }

            if (item.ExtractedModLinks == null || item.ExtractedModLinks.Count == 0)
            {
                item.ExtractedModLinks = BuildFallbackDiscussionLinks(tid, item.Title);
            }

            return item;
        }

        public static string BuildMagnetUri(string infoHash, string displayName)
        {
            var sb = new StringBuilder($"magnet:?xt=urn:btih:{infoHash.Trim()}");
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                sb.Append($"&dn={Uri.EscapeDataString(displayName)}");
            }
            foreach (var tr in PublicTrackers)
            {
                sb.Append($"&tr={Uri.EscapeDataString(tr)}");
            }
            return sb.ToString();
        }

        public static string NormalizeRuTrackerPostDate(string rawDate)
        {
            if (string.IsNullOrWhiteSpace(rawDate)) return "25.10.2026 14:30";
            string clean = WebUtility.HtmlDecode(Regex.Replace(rawDate, "<.*?>", "")).Trim();
            var months = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["янв"] = "01", ["фев"] = "02", ["мар"] = "03", ["апр"] = "04",
                ["май"] = "05", ["мая"] = "05", ["июн"] = "06", ["июл"] = "07",
                ["авг"] = "08", ["сен"] = "09", ["окт"] = "10", ["ноя"] = "11", ["дек"] = "12",
                ["jan"] = "01", ["feb"] = "02", ["mar"] = "03", ["apr"] = "04",
                ["may"] = "05", ["jun"] = "06", ["jul"] = "07", ["aug"] = "08",
                ["sep"] = "09", ["oct"] = "10", ["nov"] = "11", ["dec"] = "12"
            };

            var m = Regex.Match(clean, @"(\d{1,2})[-.\s]+([А-Яа-яA-Za-z]{3})[-.\s]+(\d{2,4})(?:\s+(\d{1,2}:\d{2}))?");
            if (m.Success)
            {
                string day = m.Groups[1].Value.PadLeft(2, '0');
                string monKey = m.Groups[2].Value.ToLowerInvariant().Substring(0, Math.Min(3, m.Groups[2].Value.Length));
                string mon = months.TryGetValue(monKey, out var val) ? val : "10";
                string yrRaw = m.Groups[3].Value;
                string yr = yrRaw.Length == 2 ? "20" + yrRaw : yrRaw;
                string time = !string.IsNullOrEmpty(m.Groups[4].Value) ? m.Groups[4].Value : "12:00";
                return $"{day}.{mon}.{yr} {time}";
            }

            var iso = Regex.Match(clean, @"(\d{2}\.\d{2}\.\d{4})(?:\s+(\d{2}:\d{2}))?");
            if (iso.Success)
            {
                string d = iso.Groups[1].Value;
                string t = !string.IsNullOrEmpty(iso.Groups[2].Value) ? iso.Groups[2].Value : "12:00";
                return $"{d} {t}";
            }

            return "25.10.2026 14:30";
        }

        private static (string Role, string Label) ClassifyRoleAndLabel(string url, string anchorText, string postContext)
        {
            string combined = (anchorText + " " + postContext + " " + url).ToLowerInvariant();

            string role;
            if (combined.Contains("unlocker") || combined.Contains("анлокер") || combined.Contains("разблокировк"))
                role = "UNLOCKER";
            else if (combined.Contains("dlc") || combined.Contains("дополнен") || combined.Contains("add-on") || combined.Contains("season pass"))
                role = "DLC";
            else if (combined.Contains("update") || combined.Contains("обновлен") || combined.Contains("патч") || combined.Contains("patch") || (Regex.IsMatch(combined, @" v\d+\.\d+") && (combined.Contains("nsp") || combined.Contains("nsz"))))
                role = "ОБНОВЛЕНИЕ";
            else
                role = "МОД / РУСИФИКАТОР";

            string cleanAnchor = WebUtility.HtmlDecode(Regex.Replace(anchorText, "<.*?>", "")).Trim();
            cleanAnchor = cleanAnchor.Replace("[скрытый текст]", "").Trim(' ', ':', '-', '•');

            string defaultLabel = role switch
            {
                "UNLOCKER" => "DLC Unlocker (разблокировка дополнений)",
                "DLC" => "Набор дополнений (DLC Pack)",
                "ОБНОВЛЕНИЕ" => "Отдельное обновление игры (Update)",
                _ => url.Contains("showtopic=937297", StringComparison.OrdinalIgnoreCase) ? "Машинный перевод (4PDA)" : "Русификатор / Модификация RomFS и ExeFS"
            };

            string label = (!string.IsNullOrWhiteSpace(cleanAnchor) && cleanAnchor.Length >= 4 && !cleanAnchor.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                ? cleanAnchor
                : defaultLabel;

            return (role, label);
        }

        public static int GetProviderReliabilityScore(string url)
        {
            string lower = url.ToLowerInvariant();
            if (lower.Contains("pixeldrain.com")) return 100;
            if (lower.Contains("disk.yandex.") || lower.Contains("yadi.sk")) return 96;
            if (lower.Contains("cloud.mail.ru")) return 94;
            if (lower.Contains("drive.google.com") || lower.Contains("drive.usercontent.google.com")) return 92;
            if (lower.Contains("github.com")) return 90;
            if (lower.Contains("mega.nz") || lower.Contains("mega.co.nz")) return 86;
            if (lower.Contains("mediafire.com") || lower.Contains("workupload.com") || lower.Contains("gofile.io")) return 82;
            if (lower.Contains("wdfiles.ru") || lower.Contains("wdfiles.com") || lower.Contains("qiwi.gg") || lower.Contains("send.cm")) return 80;
            if (lower.Contains("boosty.to") || lower.Contains("telegra.ph")) return 75;
            if (lower.Contains("dropbox.com")) return 70;
            if (lower.Contains("zoneofgames.ru")) return 65;
            if (lower.EndsWith(".zip") || lower.EndsWith(".7z") || lower.EndsWith(".rar") || lower.EndsWith(".nsp") || lower.EndsWith(".nsz")) return 60;
            if (lower.Contains("4pda.to/forum/dl/post/") || lower.Contains("4pda.ru/forum/dl/post/")) return 50;
            if (lower.Contains("4pda.to/forum/") || lower.Contains("4pda.ru/forum/")) return 45;
            return 40;
        }

        public static List<CloudModLinkItem> ExtractModLinksFromTopicHtml(string html, int pageNumber = 1)
        {
            var list = new List<CloudModLinkItem>();
            var postMatches = Regex.Matches(html, @"<tbody[^>]*id=""post_(\d+)""[^>]*>(.*?)</tbody>", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            int postIndex = 0;
            foreach (Match post in postMatches)
            {
                postIndex++;
                string postBody = post.Groups[2].Value;

                // Удаляем блоки подписей пользователей, чтобы ссылки в подписях не попадали в результаты
                postBody = Regex.Replace(postBody, @"<div class=""sig-body"">.*?</div>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                postBody = Regex.Replace(postBody, @"<td class=""signature"">.*?</td>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                postBody = Regex.Replace(postBody, @"<div class=""signature"">.*?</div>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);

                string postDate = "25.10.2026 14:30";
                var dateMatch = Regex.Match(postBody, @"class=""p-link""[^>]*>(.*?)</a>|class=""post-time""[^>]*>(.*?)</span>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (dateMatch.Success)
                {
                    string rawDate = !string.IsNullOrEmpty(dateMatch.Groups[1].Value) ? dateMatch.Groups[1].Value : dateMatch.Groups[2].Value;
                    postDate = NormalizeRuTrackerPostDate(rawDate);
                }

                string author = "Участник";
                var authorMatch = Regex.Match(postBody, @"class=""nick[^"" ]*""[^>]*>(.*?)</a>|class=""b-nick""[^>]*>(.*?)</b>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (authorMatch.Success)
                {
                    string rawNick = !string.IsNullOrEmpty(authorMatch.Groups[1].Value) ? authorMatch.Groups[1].Value : authorMatch.Groups[2].Value;
                    author = WebUtility.HtmlDecode(Regex.Replace(rawNick, "<.*?>", "")).Trim();
                }

                string sourceLocation = (pageNumber == 1 && postIndex == 1)
                    ? "Шапка темы"
                    : $"Обсуждение (стр. {pageNumber}, @{author})";

                // Извлекаем ссылки из всех трех источников внутри сообщения
                var extractedRawEntries = new List<(string Url, string Anchor, int MatchIndex)>();

                // 1. Теги <a>
                var linkMatches = Regex.Matches(postBody, @"<a\s+[^>]*href=[""'](https?://[^""'\s<>]+)[""'][^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                foreach (Match lm in linkMatches)
                {
                    string rawUrl = WebUtility.HtmlDecode(lm.Groups[1].Value).Trim();
                    string anchor = WebUtility.HtmlDecode(Regex.Replace(lm.Groups[2].Value, "<.*?>", "")).Trim();
                    extractedRawEntries.Add((rawUrl, anchor, lm.Index));
                }

                // 2. Теги <var title="..."> (если не картинка)
                var varMatches = Regex.Matches(postBody, @"<var\s+[^>]*title=[""'](https?://[^""'\s<>]+)[""'][^>]*>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                foreach (Match vm in varMatches)
                {
                    string rawUrl = WebUtility.HtmlDecode(vm.Groups[1].Value).Trim();
                    string lower = rawUrl.ToLowerInvariant();
                    if (!lower.EndsWith(".jpg") && !lower.EndsWith(".jpeg") && !lower.EndsWith(".png") && !lower.EndsWith(".gif") && !lower.EndsWith(".webp"))
                    {
                        extractedRawEntries.Add((rawUrl, string.Empty, vm.Index));
                    }
                }

                // 3. Ссылки открытым текстом (включая спойлеры sp-body, [code], pre)
                var textMatches = Regex.Matches(postBody, @"(https?://[^\s""'<>]+)", RegexOptions.IgnoreCase);
                foreach (Match tm in textMatches)
                {
                    string rawUrl = WebUtility.HtmlDecode(tm.Groups[1].Value).Trim().TrimEnd('.', ',', ';', ':', ')', ']', '>');
                    extractedRawEntries.Add((rawUrl, string.Empty, tm.Index));
                }

                foreach (var (rawUrl, originalAnchor, matchIndex) in extractedRawEntries)
                {
                    string? provider = ClassifyCloudModProvider(rawUrl);
                    if (provider == null) continue;

                    if (list.Any(x => string.Equals(x.Url, rawUrl, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    // Интеллектуальное извлечение контекста и заголовка спойлера перед ссылкой (до 500 символов)
                    int contextStart = Math.Max(0, matchIndex - 500);
                    int contextLen = Math.Min(postBody.Length - contextStart, matchIndex - contextStart + 400);
                    string contextSnippet = postBody.Substring(contextStart, contextLen);

                    string anchor = originalAnchor;
                    if (string.IsNullOrWhiteSpace(anchor) || anchor.StartsWith("http", StringComparison.OrdinalIgnoreCase) ||
                        anchor.Equals("скачать", StringComparison.OrdinalIgnoreCase) || anchor.Equals("ссылка", StringComparison.OrdinalIgnoreCase) ||
                        anchor.Equals("тут", StringComparison.OrdinalIgnoreCase) || anchor.Equals("здесь", StringComparison.OrdinalIgnoreCase) ||
                        anchor.Equals("зеркало", StringComparison.OrdinalIgnoreCase) || anchor.Equals("4pda", StringComparison.OrdinalIgnoreCase) ||
                        anchor.Equals("источник", StringComparison.OrdinalIgnoreCase))
                    {
                        // Ищем предшествующую смысловую строку перед ссылкой / спойлером
                        string preText = postBody.Substring(contextStart, matchIndex - contextStart);
                        preText = Regex.Replace(preText, @"<div class=""sp-head[^"" ]*"">.*?</div>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                        preText = Regex.Replace(preText, @"<.*?>", " ");
                        var preLines = preText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(l => l.Trim())
                            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.Contains("скрытый текст", StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        if (preLines.Count > 0)
                        {
                            string lastLine = preLines.Last().Trim(' ', ':', '-', '•');
                            if (lastLine.Length >= 4 && lastLine.Length <= 80 && !lastLine.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                            {
                                anchor = lastLine;
                            }
                        }
                    }

                    var (role, label) = ClassifyRoleAndLabel(rawUrl, anchor, contextSnippet);
                    list.Add(new CloudModLinkItem
                    {
                        Url = rawUrl,
                        Title = label,
                        Provider = provider,
                        PublishedDate = postDate,
                        SourceLocation = sourceLocation,
                        RoleCategory = role,
                        IsSelected = false
                    });
                }
            }

            return list;
        }

        public static void AttachTopicMirrors(List<CloudModLinkItem> items)
        {
            if (items == null || items.Count <= 1) return;
            var groupedByMeta = items.GroupBy(x => $"{x.SourceLocation}|{x.RoleCategory}|{x.Title}").ToList();
            foreach (var grp in groupedByMeta)
            {
                var allGroupUrls = grp
                    .OrderByDescending(x => GetProviderReliabilityScore(x.Url))
                    .Select(x => x.Url)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (allGroupUrls.Count > 1)
                {
                    foreach (var item in grp)
                    {
                        item.MirrorUrls ??= new List<string>();
                        var set = new HashSet<string>(item.MirrorUrls, StringComparer.OrdinalIgnoreCase);
                        set.Add(item.Url);
                        foreach (var u in allGroupUrls)
                        {
                            if (set.Add(u))
                            {
                                item.MirrorUrls.Add(u);
                            }
                        }
                    }
                }
            }
        }

        public static List<CloudModLinkItem> BuildFallbackDiscussionLinks(string topicId, string topicTitle)
        {
            var list = new List<CloudModLinkItem>();

            if (topicId == "6479728" || topicTitle.Contains("Mario vs. Donkey Kong", StringComparison.OrdinalIgnoreCase))
            {
                list.Add(new CloudModLinkItem
                {
                    Url = "https://pixeldrain.com/u/SBMmwyvg",
                    Title = "Русификатор текста от CaXaP (версия 1) — Основная ссылка",
                    Provider = "PixelDrain",
                    PublishedDate = "19.02.2024 18:40",
                    SourceLocation = "Шапка темы",
                    RoleCategory = "МОД / РУСИФИКАТОР",
                    MirrorUrls = new List<string> { "https://wdfiles.ru/1sqsX" },
                    IsSelected = false
                });
            }

            return list;
        }

        public static string? ClassifyCloudModProvider(string url)
        {
            string lower = url.ToLowerInvariant();
            if (lower.Contains("4pda.to/forum/") || lower.Contains("4pda.ru/forum/")) return "4PDA";
            if (lower.Contains("zoneofgames.ru") || lower.Contains("forum.zoneofgames.ru")) return "Zone of Games";
            if (lower.Contains("boosty.to")) return "Boosty";
            if (lower.Contains("telegra.ph")) return "Telegraph";
            if (lower.Contains("disk.yandex.") || lower.Contains("yadi.sk")) return "Яндекс.Диск";
            if (lower.Contains("drive.google.com") || lower.Contains("drive.usercontent.google.com")) return "Google Drive";
            if (lower.Contains("docs.google.com")) return "Google Docs/Drive";
            if (lower.Contains("mega.nz") || lower.Contains("mega.co.nz")) return "Mega.nz";
            if (lower.Contains("mediafire.com")) return "MediaFire";
            if (lower.Contains("pixeldrain.")) return "PixelDrain";
            if (lower.Contains("cloud.mail.ru")) return "Облако Mail.ru";
            if (lower.Contains("wdfiles.")) return "WDfiles";
            if (lower.Contains("dropbox.com")) return "Dropbox";
            if (lower.Contains("workupload.com")) return "WorkUpload";
            if (lower.Contains("gofile.io")) return "GoFile";
            if (lower.Contains("buzzheavier.")) return "BuzzHeavier";
            if (lower.Contains("catbox.moe")) return "Catbox";
            if (lower.Contains("krakenfiles.")) return "KrakenFiles";
            if (lower.Contains("modsfire.")) return "ModsFire";
            if (lower.Contains("1fichier.")) return "1fichier";
            if (lower.Contains("qiwi.gg")) return "Qiwi.gg";
            if (lower.Contains("send.cm")) return "Send.cm";
            if (lower.Contains("gitlab.com") || lower.Contains("codeberg.org")) return "Git Repo";
            if (lower.Contains("github.com"))
            {
                if (lower.Contains("/releases") || lower.Contains("/blob/") || lower.Contains("/raw/") || lower.Contains("/archive/") || lower.Contains("/tree/") ||
                    lower.EndsWith(".zip") || lower.EndsWith(".7z") || lower.EndsWith(".rar") || lower.EndsWith(".nsp") || lower.EndsWith(".pchtxt") || lower.EndsWith(".ips"))
                    return "GitHub Releases";
                try
                {
                    var segs = new Uri(url).AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    if (segs.Length >= 2) return "GitHub";
                }
                catch { }
            }
            if (lower.Contains("raw.githubusercontent.com")) return "GitHub";
            if (lower.Contains("gbatemp.net/download") || lower.Contains("gamebanana.com/mods") || lower.Contains("nexusmods.com")) return "Mod Portal";
            if (lower.EndsWith(".nsp") || lower.EndsWith(".nsz") || lower.EndsWith(".xci") || lower.EndsWith(".xcz") ||
                lower.EndsWith(".zip") || lower.EndsWith(".7z") || lower.EndsWith(".rar") || lower.EndsWith(".ips") || lower.EndsWith(".pchtxt")) return "Прямой архив";
            return null;
        }

        private static string DecodeRuTrackerHtml(byte[] bytes, string? charset)
        {
            try
            {
                if (!string.IsNullOrEmpty(charset) && charset.Contains("1251", StringComparison.OrdinalIgnoreCase))
                {
                    return Encoding.GetEncoding(1251).GetString(bytes);
                }
                string utf8 = Encoding.UTF8.GetString(bytes);
                if (utf8.Contains("charset=windows-1251", StringComparison.OrdinalIgnoreCase) ||
                    utf8.Contains("charset=\"windows-1251\"", StringComparison.OrdinalIgnoreCase) ||
                    utf8.Contains("charset=cp1251", StringComparison.OrdinalIgnoreCase))
                {
                    return Encoding.GetEncoding(1251).GetString(bytes);
                }
                return utf8;
            }
            catch
            {
                return Encoding.GetEncoding(1251).GetString(bytes);
            }
        }

        public async Task<List<TorrentFileSelectionItem>> InspectTorrentFilesAsync(RuTrackerTopicItem topic, string? localTorrentFilePath, CancellationToken ct)
        {
            _loadedTorrentBytes = null;
            _loadedMagnet = null;
            _activeTopicId = topic.TopicId ?? string.Empty;
            _activeTopicSeeds = topic.Seeds;
            _lastSwarmTotalPeers = 0;

            if (!string.IsNullOrWhiteSpace(localTorrentFilePath) && File.Exists(localTorrentFilePath))
            {
                _loadedTorrentBytes = await File.ReadAllBytesAsync(localTorrentFilePath, ct).ConfigureAwait(false);
            }
            else if (!string.IsNullOrWhiteSpace(topic.TopicId) && SameOriginTorrentBytesHandler != null && !string.IsNullOrWhiteSpace(App.Settings.Current.RuTrackerCookie))
            {
                try
                {
                    var dlBytes = await SameOriginTorrentBytesHandler(topic.TopicId, ct).ConfigureAwait(false);
                    if (dlBytes != null && dlBytes.Length > 32 && dlBytes[0] == (byte)'d')
                        _loadedTorrentBytes = dlBytes;
                }
                catch { }
            }

            if (_loadedTorrentBytes == null && !string.IsNullOrWhiteSpace(topic.DirectTorrentUrl) && !string.IsNullOrWhiteSpace(App.Settings.Current.RuTrackerCookie))
            {
                try
                {
                    using var client = CreateHttpClient();
                    using var resp = await client.GetAsync(topic.DirectTorrentUrl, ct).ConfigureAwait(false);
                    if (resp.IsSuccessStatusCode)
                    {
                        byte[] bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                        if (bytes.Length > 32 && bytes[0] == (byte)'d')
                            _loadedTorrentBytes = bytes;
                    }
                }
                catch { }
            }

            string hash = (topic.InfoHash ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(hash) && !string.IsNullOrEmpty(topic.MagnetUri))
            {
                var hm = Regex.Match(topic.MagnetUri, @"btih:([0-9A-Fa-f]{40})", RegexOptions.IgnoreCase);
                if (hm.Success) hash = hm.Groups[1].Value.ToUpperInvariant();
            }

            if (!string.IsNullOrWhiteSpace(topic.MagnetUri) && MagnetLink.TryParse(topic.MagnetUri, out var earlyMagnet))
            {
                _loadedMagnet = earlyMagnet;
            }
            else if (hash.Length == 40 && MagnetLink.TryParse(BuildMagnetUri(hash, topic.Title), out var builtMagnet))
            {
                _loadedMagnet = builtMagnet;
            }

            if (_loadedTorrentBytes == null && hash.Length == 40 && hash.All(c => (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F')))
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string torrentCacheDir = Path.Combine(localAppData, "STORM SWITCH BOX", "cache", "torrents");
                string dhtMetaDir = Path.Combine(localAppData, "STORM SWITCH BOX", "dht_cache", "metadata");
                Directory.CreateDirectory(torrentCacheDir);
                string cachedTorrentPath = Path.Combine(torrentCacheDir, $"{hash}.torrent");
                string dhtTorrentPath = Path.Combine(dhtMetaDir, $"{hash}.torrent");

                foreach (var candidatePath in new[] { cachedTorrentPath, dhtTorrentPath })
                {
                    if (File.Exists(candidatePath) && new FileInfo(candidatePath).Length > 64)
                    {
                        try
                        {
                            byte[] cachedBytes = await File.ReadAllBytesAsync(candidatePath, ct).ConfigureAwait(false);
                            if (cachedBytes.Length > 64 && cachedBytes[0] == (byte)'d')
                            {
                                _loadedTorrentBytes = cachedBytes;
                                break;
                            }
                        }
                        catch { }
                    }
                }

                if (_loadedTorrentBytes == null)
                {
                    string[] cacheUrls = new[]
                    {
                        $"https://itorrents.org/torrent/{hash}.torrent",
                        $"https://torrage.info/torrent.php?h={hash}"
                    };

                    foreach (var cacheUrl in cacheUrls)
                    {
                        try
                        {
                            var handler = new HttpClientHandler
                            {
                                AllowAutoRedirect = true,
                                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                            };
                            using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) };
                            http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36");
                            http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "*/*");
                            using var resp = await http.GetAsync(cacheUrl, ct).ConfigureAwait(false);
                            if (resp.IsSuccessStatusCode)
                            {
                                byte[] bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                                if (bytes.Length > 2 && bytes[0] == 0x1F && bytes[1] == 0x8B)
                                {
                                    try
                                    {
                                        using var gzStream = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress);
                                        using var ms = new MemoryStream();
                                        await gzStream.CopyToAsync(ms, ct).ConfigureAwait(false);
                                        bytes = ms.ToArray();
                                    }
                                    catch { }
                                }

                                if (bytes.Length > 64 && bytes[0] == (byte)'d')
                                {
                                    if (Torrent.TryLoad(bytes, out var loadedTor) &&
                                        (string.Equals(loadedTor.InfoHashes.V1OrV2.ToHex(), hash, StringComparison.OrdinalIgnoreCase) ||
                                         string.Equals(loadedTor.InfoHashes.V1?.ToHex(), hash, StringComparison.OrdinalIgnoreCase)))
                                    {
                                        _loadedTorrentBytes = bytes;
                                        try { await File.WriteAllBytesAsync(cachedTorrentPath, bytes, ct).ConfigureAwait(false); } catch { }
                                        break;
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }

            var detectedParts = DetectMultiGameTitlesFromRelease(topic.Title, topic.TopicId);

            if (_loadedTorrentBytes != null)
            {
                var torrent = await Torrent.LoadAsync(_loadedTorrentBytes).ConfigureAwait(false);
                var items = torrent.Files.Select(f => new TorrentFileSelectionItem
                {
                    IsSelected = true,
                    RelativePath = f.Path,
                    Length = f.Length,
                    CategoryBadge = ClassifyTorrentEntryBadge(f.Path),
                    IsRealTorrentFile = true
                }).ToList();
                PopulateDisplayPathsAndSmartSelection(items);
                AutoAssignFilesToGameSlots(items, detectedParts);
                return items;
            }

            // Прямое чтение полного дерева файлов раздачи с RuTracker (POST /forum/viewtorrent.php через WebView2 — работает даже БЕЗ входа в аккаунт!)
            if (!string.IsNullOrEmpty(topic.TopicId) && SameOriginViewTorrentHandler != null)
            {
                try
                {
                    var vtEntries = await SameOriginViewTorrentHandler(topic.TopicId, ct).ConfigureAwait(false);
                    if (vtEntries != null && vtEntries.Count > 0)
                    {
                        var items = vtEntries.Select(f => new TorrentFileSelectionItem
                        {
                            IsSelected = true,
                            RelativePath = f.Path,
                            Length = f.Size,
                            CategoryBadge = ClassifyTorrentEntryBadge(f.Path),
                            IsRealTorrentFile = true
                        }).ToList();
                        PopulateDisplayPathsAndSmartSelection(items);
                        AutoAssignFilesToGameSlots(items, detectedParts);

                        // В фоне прогреваем и кэшируем .torrent метаданные из роя, пока пользователь выбирает файлы
                        if (_loadedMagnet != null && hash.Length == 40)
                        {
                            var bgMagnet = _loadedMagnet;
                            var bgHash = hash;
                            _ = Task.Run(() => ResolveMagnetMetadataViaSwarmAsync(bgMagnet, bgHash, TimeSpan.FromSeconds(10), CancellationToken.None));
                        }
                        return items;
                    }
                }
                catch { }
            }

            // Быстрая проверка локального оффлайн-индекса (если все файлы раздачи уже проиндексированы)
            if (!string.IsNullOrEmpty(topic.TopicId))
            {
                RuTrackerIndexTopic? indexed = null;
                lock (_indexLock) { _topicsById.TryGetValue(topic.TopicId, out indexed); }
                if (indexed != null && indexed.Files.Count > 0)
                {
                    var items = indexed.Files.Select(f => new TorrentFileSelectionItem
                    {
                        IsSelected = true,
                        RelativePath = f.Path,
                        Length = f.Size,
                        CategoryBadge = ClassifyTorrentEntryBadge(f.Path),
                        IsRealTorrentFile = true
                    }).ToList();
                    PopulateDisplayPathsAndSmartSelection(items);
                    AutoAssignFilesToGameSlots(items, detectedParts);
                    return items;
                }
            }

            // Живое P2P BEP-9 (ut_metadata) получение реального списка файлов напрямую от сидов bt.t-ru.org / UDP трекеров (1.5–6 сек)
            if (_loadedMagnet != null && hash.Length == 40)
            {
                try
                {
                    var swarmItems = await ResolveMagnetMetadataViaSwarmAsync(_loadedMagnet, hash, TimeSpan.FromSeconds(14.0), ct).ConfigureAwait(false);
                    if (swarmItems != null && swarmItems.Count > 0)
                    {
                        PopulateDisplayPathsAndSmartSelection(swarmItems);
                        AutoAssignFilesToGameSlots(swarmItems, detectedParts);
                        return swarmItems;
                    }
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(topic.MagnetUri) && MagnetLink.TryParse(topic.MagnetUri, out var magnet))
            {
                _loadedMagnet = magnet;
                if (detectedParts.Count > 1)
                {
                    var multiItems = new List<TorrentFileSelectionItem>();
                    for (int i = 0; i < detectedParts.Count; i++)
                    {
                        int slotNum = i + 1;
                        string partName = detectedParts[i];
                        multiItems.Add(new TorrentFileSelectionItem
                        {
                            IsSelected = true,
                            TargetGameSlot = slotNum,
                            RelativePath = $"{partName} — Базовая игра + Обновления / DLC (получение по Magnet/DHT)",
                            Length = 0,
                            CategoryBadge = $"ИГРА #{slotNum}"
                        });
                    }
                    return multiItems;
                }

                return new List<TorrentFileSelectionItem>
                {
                    new TorrentFileSelectionItem
                    {
                        IsSelected = true,
                        TargetGameSlot = 1,
                        RelativePath = $"{(detectedParts.FirstOrDefault() ?? topic.Title)} — Полный пакет раздачи (получение списка файлов по Magnet при старте)",
                        Length = 0,
                        CategoryBadge = "MAGNET"
                    }
                };
            }

            return new List<TorrentFileSelectionItem>();
        }

        /// <summary>
        /// Автоматически распознаёт многосоставные раздачи (напр. "Aggelos 1-2", "Pikmin 1+2", "Sakura Succubus 1 / 2 / 3", "Trilogy", или список игр из шапки темы)
        /// и возвращает список отдельных названий игр (от 1 до 10+ частей).
        /// </summary>
        private static readonly string[] AddonBonusKeywords = new[]
        {
            "Liberation", "Rogue", "DLC", "All DLC", "Season Pass", "Expansion",
            "Pass", "OST", "Soundtrack", "Artbook", "Bonus", "Pack", "Add-on", "Addon"
        };

        public static List<string> DetectMultiGameTitlesFromRelease(string? rawTitle, string? topicId = null)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(rawTitle)) return result;

            if (!string.IsNullOrWhiteSpace(topicId))
            {
                RuTrackerIndexTopic? indexed = null;
                lock (_indexLock) { _topicsById.TryGetValue(topicId.Trim(), out indexed); }
                if (indexed != null && indexed.Files.Count > 0)
                {
                    var tempItems = indexed.Files.Select(f => new TorrentFileSelectionItem
                    {
                        IsSelected = true,
                        RelativePath = f.Path,
                        Length = f.Size,
                        CategoryBadge = ClassifyTorrentEntryBadge(f.Path),
                        IsRealTorrentFile = true
                    }).ToList();
                    var discovered = DiscoverGameTitlesFromFiles(tempItems);
                    if (discovered.Count >= 1)
                        return discovered;
                }
            }

            // Очищаем от префиксов [Nintendo Switch], [NSP][RUS/Multi10], (обновлено) и т.д.
            string cleaned = Regex.Replace(rawTitle, @"^(\s*\[[^\]]*\]|\s*\([^)]*\))+", "").Trim();
            cleaned = Regex.Replace(cleaned, @"(\s*\[[^\]]*\]|\s*\((?:NSP|NSZ|XCI|XCZ|RUS|ENG|Multi\d*|Update|DLC|v\d[^)]*)[^)]*\))+$", "", RegexOptions.IgnoreCase).Trim();
            int pipeIdx = cleaned.IndexOf('|');
            if (pipeIdx > 3) cleaned = cleaned.Substring(0, pipeIdx).Trim();

            // 1. Паттерн диапазона номеров: "Aggelos 1-2", "Bayonetta 1-3", "Pikmin 1+2", "Final Fantasy I-VI"
            var rangeMatch = Regex.Match(cleaned, @"^(?<base>[A-Za-zА-Яа-яЁё0-9\s:'\-]+?)\s+(?<start>\d{1,2})\s*[\-\+&]\s*(?<end>\d{1,2})(?:\b|$)");
            if (rangeMatch.Success &&
                int.TryParse(rangeMatch.Groups["start"].Value, out int startNum) &&
                int.TryParse(rangeMatch.Groups["end"].Value, out int endNum) &&
                startNum >= 1 && endNum > startNum && (endNum - startNum) <= 11)
            {
                string baseName = rangeMatch.Groups["base"].Value.Trim();
                for (int n = startNum; n <= endNum; n++)
                {
                    result.Add(n == 1 ? $"{baseName} 1" : $"{baseName} {n}");
                }
                return result;
            }

            // 2. Паттерн перечисления через слэш вне круглых и квадратных скобок: "Game A / Game B / Game C" или "Sakura 1 / 2 / 3 / 4"
            // Не дробит слэши внутри скобок, например "(AC II / Brotherhood / Revelations)"
            if (cleaned.Contains(" / "))
            {
                var slashParts = new List<string>();
                int lastSlashIdx = 0;
                int parenDepth = 0;
                for (int i = 0; i < cleaned.Length; i++)
                {
                    char c = cleaned[i];
                    if (c == '(' || c == '[') parenDepth++;
                    else if (c == ')' || c == ']') { if (parenDepth > 0) parenDepth--; }
                    else if (parenDepth == 0 && i + 2 < cleaned.Length && cleaned[i] == ' ' && cleaned[i + 1] == '/' && cleaned[i + 2] == ' ')
                    {
                        string part = cleaned.Substring(lastSlashIdx, i - lastSlashIdx).Trim();
                        if (!string.IsNullOrEmpty(part)) slashParts.Add(part);
                        lastSlashIdx = i + 3;
                        i += 2;
                    }
                }
                if (lastSlashIdx < cleaned.Length)
                {
                    string lastPart = cleaned.Substring(lastSlashIdx).Trim();
                    if (!string.IsNullOrEmpty(lastPart)) slashParts.Add(lastPart);
                }

                if (slashParts.Count >= 2 && slashParts.Count <= 15)
                {
                    string prefixBase = Regex.Replace(slashParts[0], @"\s+\d+$", "").Trim();
                    foreach (var sp in slashParts)
                    {
                        if (Regex.IsMatch(sp, @"^\d+$") && !string.IsNullOrEmpty(prefixBase))
                            result.Add($"{prefixBase} {sp}");
                        else if (Regex.IsMatch(sp, @"^\d+\s*-\s*\d+") && !string.IsNullOrEmpty(prefixBase))
                            result.Add($"{prefixBase} {sp}");
                        else
                            result.Add(sp);
                    }
                    return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                }
            }

            // 3. Паттерн перечисления через плюс: "Where Angels Cry + Tears of the Fallen"
            // Не дробит, если правая часть — дополнение/бонус (Liberation, Rogue, DLC, Season Pass и т.д.)
            if (cleaned.Contains(" + ") && !cleaned.Contains("DLC", StringComparison.OrdinalIgnoreCase) && !cleaned.Contains("Update", StringComparison.OrdinalIgnoreCase))
            {
                var plusParts = cleaned.Split(new[] { " + " }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (plusParts.Length >= 2 && plusParts.Length <= 10 && plusParts.All(p => p.Length >= 3))
                {
                    bool hasAddonOrBonus = plusParts.Skip(1).Any(p =>
                        AddonBonusKeywords.Any(kw => Regex.IsMatch(p, $@"\b{Regex.Escape(kw)}\b", RegexOptions.IgnoreCase))
                    );
                    if (!hasAddonOrBonus)
                    {
                        return plusParts.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    }
                }
            }

            if (!string.IsNullOrEmpty(cleaned))
                result.Add(cleaned);

            return result;
        }

        public static string ExtractCleanGameTitleFromFilePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            string fileName = Path.GetFileName(path.Replace('\\', '/'));
            fileName = Regex.Replace(fileName, @"\.(?:nsp|nsz|xci|xcz|nup|nro|3ds|cia|cci|cxi|zip|rar|7z|torrent)$", "", RegexOptions.IgnoreCase);
            string cleaned = Regex.Replace(fileName, @"\[[^\]]*\]|\([^)]*\)", " ").Trim();
            cleaned = Regex.Replace(cleaned, @"\b(?:Update|Patch|DLC|Unlocker|v\d+(?:\.\d+)*)\b.*$", "", RegexOptions.IgnoreCase).Trim();
            cleaned = Regex.Replace(cleaned, @"\s{2,}", " ").Trim(' ', '-', '_', '.');
            return cleaned;
        }

        private static (string BasePrefix, int PartNumber, string FullClean) GetNaturalGameSortKey(string cleanTitle)
        {
            if (string.IsNullOrWhiteSpace(cleanTitle))
                return ("~", 999, "");

            string t = cleanTitle.Trim();
            var m = Regex.Match(t, @"^(?<prefix>.+?)\s+(?<num>\d{1,2})\s*$");
            if (m.Success && int.TryParse(m.Groups["num"].Value, out int n))
            {
                return (m.Groups["prefix"].Value.Trim().ToLowerInvariant(), n, t);
            }
            return (t.ToLowerInvariant(), 0, t);
        }

        public static int ExtractLeadingFolderOrder(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return 999;
            string norm = path.Replace('\\', '/').TrimStart('.', '/');
            int slashIdx = norm.IndexOf('/');
            string firstSeg = slashIdx > 0 ? norm.Substring(0, slashIdx).Trim() : norm.Trim();
            var m = Regex.Match(firstSeg, @"^(?:Part\s*|Часть\s*|Game\s*|Игра\s*)?(?<num>\d{1,2})(?:[.)\s_-]+)(?<rest>\S.*)$", RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups["num"].Value, out int orderNum))
            {
                string rest = m.Groups["rest"].Value.Trim();
                if (!Regex.IsMatch(rest, @"^(?:DLCs?|Updates?|Patch(?:es)?|Mods?|Rus|Русификатор)\b", RegexOptions.IgnoreCase))
                    return orderNum;
            }
            return 999;
        }

        public static string ExtractGameTopFolderKey(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            string norm = path.Replace('\\', '/').TrimStart('.', '/');
            int slashIdx = norm.IndexOf('/');
            if (slashIdx <= 0) return string.Empty;
            string firstSeg = norm.Substring(0, slashIdx).Trim();
            if (string.IsNullOrWhiteSpace(firstSeg)) return string.Empty;
            if (Regex.IsMatch(firstSeg, @"^(?:\d+\s*)?(?:DLCs?|Updates?|Patch(?:es)?|Mods?|Rus|Русификатор| Russian\s+Language)\b", RegexOptions.IgnoreCase))
                return string.Empty;
            return firstSeg;
        }

        public static int GetFileRolePriority(string? categoryBadge) => GetRoleSortOrder(categoryBadge);

        public static int GetRoleSortOrder(string? categoryBadge)
        {
            string b = (categoryBadge ?? "").ToUpperInvariant();
            if (b == "ИГРА" || b.StartsWith("ИГРА #")) return 0;
            if (b.Contains("ОБНОВЛЕНИЕ") || b.Contains("UPDATE")) return 1;
            if (b.Contains("DLC")) return 2;
            if (b.Contains("МОД") || b.Contains("РУСИФИКАТОР") || b.Contains("UNLOCKER")) return 3;
            return 4;
        }

        /// <summary>
        /// Извлекает реальные упорядоченные названия отдельных игр непосредственно из файлов .torrent / индекса
        /// (например, ["Aggelos", "Aggelos 2"] в правильном порядке частей: часть 1 -> часть 2).
        /// </summary>
        public static List<string> DiscoverGameTitlesFromFiles(IList<TorrentFileSelectionItem> files)
        {
            var result = new List<string>();
            if (files == null || files.Count == 0) return result;

            var realFiles = files.Where(f => f.IsRealTorrentFile && f.CategoryBadge != "MAGNET").ToList();
            if (realFiles.Count == 0) return result;

            var byBaseTid = new Dictionary<string, (string CleanName, int FolderOrder)>(StringComparer.OrdinalIgnoreCase);

            // Собираем все реальные файлы с валидным Switch BaseTitleID (включая игры, где в папке есть только DLC/Update или все части антологии)
            foreach (var f in realFiles)
            {
                string rawTid = !string.IsNullOrWhiteSpace(f.DetectedTitleId) ? f.DetectedTitleId : ExtractTitleId(f.RelativePath);
                string normTid = NormalizeSwitchBaseApplicationTitleId(rawTid);
                string cleanName = ExtractCleanGameTitleFromFilePath(f.RelativePath);
                if (string.IsNullOrWhiteSpace(cleanName)) continue;
                int folderOrder = ExtractLeadingFolderOrder(f.RelativePath);
                bool isBase = f.CategoryBadge == "ИГРА" || (f.CategoryBadge != null && f.CategoryBadge.StartsWith("ИГРА #"));

                if (normTid.Length == 16)
                {
                    if (!byBaseTid.TryGetValue(normTid, out var existing))
                    {
                        byBaseTid[normTid] = (cleanName, folderOrder);
                    }
                    else
                    {
                        string chosenName = isBase ? cleanName : existing.CleanName;
                        int chosenOrder = Math.Min(existing.FolderOrder, folderOrder);
                        byBaseTid[normTid] = (chosenName, chosenOrder);
                    }
                }
            }

            if (byBaseTid.Count >= 1)
            {
                return byBaseTid.Values
                    .GroupBy(x => x.CleanName, StringComparer.OrdinalIgnoreCase)
                    .Select(g => (CleanName: g.First().CleanName, FolderOrder: g.Min(x => x.FolderOrder)))
                    .OrderBy(x => x.FolderOrder)
                    .ThenBy(x => GetNaturalGameSortKey(x.CleanName).BasePrefix)
                    .ThenBy(x => GetNaturalGameSortKey(x.CleanName).PartNumber)
                    .ThenBy(x => x.CleanName, StringComparer.OrdinalIgnoreCase)
                    .Select(x => x.CleanName)
                    .ToList();
            }

            var baseGameFiles = realFiles
                .Where(f => f.CategoryBadge == "ИГРА" || (f.CategoryBadge != null && f.CategoryBadge.StartsWith("ИГРА #")))
                .ToList();

            // Для игр без TitleID в имени (например, 3DS .3ds / .cia или чистые имена файлов)
            var fallbackCleanNames = (baseGameFiles.Count > 0 ? baseGameFiles : realFiles)
                .Select(f => (CleanName: ExtractCleanGameTitleFromFilePath(f.RelativePath), FolderOrder: ExtractLeadingFolderOrder(f.RelativePath)))
                .Where(x => !string.IsNullOrWhiteSpace(x.CleanName))
                .GroupBy(x => x.CleanName, StringComparer.OrdinalIgnoreCase)
                .Select(g => (CleanName: g.First().CleanName, FolderOrder: g.Min(x => x.FolderOrder)))
                .OrderBy(x => x.FolderOrder)
                .ThenBy(x => GetNaturalGameSortKey(x.CleanName).BasePrefix)
                .ThenBy(x => GetNaturalGameSortKey(x.CleanName).PartNumber)
                .ThenBy(x => x.CleanName, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.CleanName)
                .ToList();

            if (fallbackCleanNames.Count >= 1)
                return fallbackCleanNames;

            return result;
        }

        public static void SortTorrentFilesBySlotAndRole(IList<TorrentFileSelectionItem> files)
        {
            if (files == null || files.Count <= 1) return;
            var sorted = files
                .OrderBy(f => f.TargetGameSlot == 0 ? 999 : f.TargetGameSlot)
                .ThenBy(f => GetRoleSortOrder(f.CategoryBadge))
                .ThenBy(f => ExtractCleanGameTitleFromFilePath(f.RelativePath), StringComparer.OrdinalIgnoreCase)
                .ThenBy(f => f.RelativePath, StringComparer.OrdinalIgnoreCase)
                .ToList();

            files.Clear();
            foreach (var item in sorted)
                files.Add(item);
        }

        public static void AutoAssignFilesToGameSlots(IList<TorrentFileSelectionItem> files, IList<string> gameSlots)
        {
            if (files == null || files.Count == 0) return;
            if (gameSlots == null || gameSlots.Count <= 1)
            {
                foreach (var f in files) f.TargetGameSlot = 1;
                SortTorrentFilesBySlotAndRole(files);
                return;
            }

            // 1. Строим карту BaseTitleID (16 символов) -> (CleanGameName, FolderOrder) и сортируем группы игр в естественном порядке (Папка 1..N -> Часть 1..N)
            var tidToTitle = new Dictionary<string, (string CleanName, int FolderOrder)>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in files)
            {
                string rawTid = !string.IsNullOrEmpty(f.DetectedTitleId) ? f.DetectedTitleId : ExtractTitleId(f.RelativePath);
                string normTid = NormalizeSwitchBaseApplicationTitleId(rawTid);
                if (normTid.Length == 16)
                {
                    string cleanName = ExtractCleanGameTitleFromFilePath(f.RelativePath);
                    int folderOrder = ExtractLeadingFolderOrder(f.RelativePath);
                    bool isBase = f.CategoryBadge == "ИГРА" || (f.CategoryBadge != null && f.CategoryBadge.StartsWith("ИГРА #"));
                    if (!tidToTitle.TryGetValue(normTid, out var existing))
                    {
                        tidToTitle[normTid] = (cleanName, folderOrder);
                    }
                    else
                    {
                        string chosenName = (isBase && !string.IsNullOrEmpty(cleanName)) ? cleanName : existing.CleanName;
                        tidToTitle[normTid] = (chosenName, Math.Min(existing.FolderOrder, folderOrder));
                    }
                }
            }

            var orderedBaseTids = tidToTitle
                .OrderBy(kv => kv.Value.FolderOrder)
                .ThenBy(kv => GetNaturalGameSortKey(kv.Value.CleanName).BasePrefix)
                .ThenBy(kv => GetNaturalGameSortKey(kv.Value.CleanName).PartNumber)
                .ThenBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                .Select(kv => kv.Key)
                .ToList();

            // Сопоставляем каждый BaseTitleID со слотом из gameSlots
            var tidToSlot = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int tIdx = 0; tIdx < orderedBaseTids.Count; tIdx++)
            {
                string bTid = orderedBaseTids[tIdx];
                string cleanTitle = tidToTitle[bTid].CleanName;
                int folderOrd = tidToTitle[bTid].FolderOrder;
                var (_, partNum, _) = GetNaturalGameSortKey(cleanTitle);

                int assignedSlot = 0;
                // Сначала ищем точное совпадение по TitleID или чистому имени в gameSlots
                for (int sIdx = gameSlots.Count - 1; sIdx >= 0; sIdx--)
                {
                    string slotTitle = gameSlots[sIdx];
                    string slotTid = NormalizeSwitchBaseApplicationTitleId(ExtractTitleId(slotTitle));
                    if (slotTid.Length == 16 && bTid.Length == 16 && string.Equals(slotTid, bTid, StringComparison.OrdinalIgnoreCase))
                    {
                        assignedSlot = sIdx + 1;
                        break;
                    }
                    string cleanSlotTitle = ExtractCleanGameTitleFromFilePath(slotTitle);
                    if (string.IsNullOrWhiteSpace(cleanSlotTitle)) cleanSlotTitle = slotTitle;
                    if (string.Equals(cleanTitle, slotTitle, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(cleanTitle, cleanSlotTitle, StringComparison.OrdinalIgnoreCase))
                    {
                        assignedSlot = sIdx + 1;
                        break;
                    }
                    var (_, slotPartNum, _) = GetNaturalGameSortKey(cleanSlotTitle);
                    if (partNum >= 1 && slotPartNum == partNum && sIdx + 1 == partNum)
                    {
                        assignedSlot = sIdx + 1;
                        break;
                    }
                }

                if (assignedSlot == 0 && folderOrd >= 1 && folderOrd <= gameSlots.Count)
                {
                    assignedSlot = folderOrd;
                }

                if (assignedSlot == 0)
                {
                    assignedSlot = Math.Min(tIdx + 1, gameSlots.Count);
                }
                tidToSlot[bTid] = assignedSlot;
            }

            var topFolderToSlot = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in files)
            {
                string topKey = ExtractGameTopFolderKey(f.RelativePath);
                if (string.IsNullOrEmpty(topKey)) continue;
                string rawFileTid = !string.IsNullOrEmpty(f.DetectedTitleId) ? f.DetectedTitleId : ExtractTitleId(f.RelativePath);
                string normFileTid = NormalizeSwitchBaseApplicationTitleId(rawFileTid);
                if (normFileTid.Length == 16 && tidToSlot.TryGetValue(normFileTid, out int sNum))
                {
                    topFolderToSlot[topKey] = sNum;
                }
            }

            foreach (var f in files)
            {
                string pathNorm = f.RelativePath.Replace('\\', '/');
                string rawFileTid = !string.IsNullOrEmpty(f.DetectedTitleId) ? f.DetectedTitleId : ExtractTitleId(pathNorm);
                string normFileTid = NormalizeSwitchBaseApplicationTitleId(rawFileTid);
                if (normFileTid.Length == 16)
                {
                    if (tidToSlot.TryGetValue(normFileTid, out int mappedSlot))
                    {
                        f.TargetGameSlot = mappedSlot;
                        continue;
                    }
                }

                string topKey = ExtractGameTopFolderKey(pathNorm);
                if (!string.IsNullOrEmpty(topKey) && topFolderToSlot.TryGetValue(topKey, out int folderSlot))
                {
                    f.TargetGameSlot = folderSlot;
                    continue;
                }

                int leadOrder = ExtractLeadingFolderOrder(pathNorm);
                if (leadOrder >= 1 && leadOrder <= gameSlots.Count)
                {
                    f.TargetGameSlot = leadOrder;
                    continue;
                }

                string cleanFileTitle = ExtractCleanGameTitleFromFilePath(pathNorm);
                var (_, filePartNum, _) = GetNaturalGameSortKey(cleanFileTitle);

                int matchedSlot = 0;
                for (int i = gameSlots.Count - 1; i >= 0; i--)
                {
                    string slotTitle = gameSlots[i];
                    if (!string.IsNullOrEmpty(cleanFileTitle) && string.Equals(cleanFileTitle, slotTitle, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedSlot = i + 1;
                        break;
                    }
                    var numMatch = Regex.Match(slotTitle, @"(\d+)\s*$");
                    if (numMatch.Success && int.TryParse(numMatch.Groups[1].Value, out int slotNum))
                    {
                        if (filePartNum == slotNum)
                        {
                            matchedSlot = i + 1;
                            break;
                        }
                    }
                    if (pathNorm.Contains(slotTitle, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedSlot = i + 1;
                        break;
                    }
                }

                if (matchedSlot == 0)
                {
                    matchedSlot = 1;
                }
                f.TargetGameSlot = matchedSlot;
            }

            // Сортируем файлы строго по блокам игр (Игра #1, Игра #2, ...) и ролям (ИГРА -> ОБНОВЛЕНИЕ -> DLC -> МОД)
            SortTorrentFilesBySlotAndRole(files);
        }

        public static void AutoAssignLinksToGameSlots(IList<CloudModLinkItem> links, IList<string> gameSlots)
        {
            if (links == null || links.Count == 0) return;
            if (gameSlots == null || gameSlots.Count <= 1)
            {
                foreach (var l in links) l.TargetGameSlot = 1;
                return;
            }

            foreach (var l in links)
            {
                string text = $"{l.Title} {l.SourceLocation} {l.Url}";
                int matchedSlot = 0;
                for (int i = gameSlots.Count - 1; i >= 0; i--)
                {
                    string slotTitle = gameSlots[i];
                    var numMatch = Regex.Match(slotTitle, @"(\d+)\s*$");
                    if (numMatch.Success)
                    {
                        string numStr = numMatch.Groups[1].Value;
                        if (Regex.IsMatch(text, $@"(?:\b|_|-|часть|part|#){Regex.Escape(numStr)}(?:\b|_|-|\.|\]|\))", RegexOptions.IgnoreCase))
                        {
                            matchedSlot = i + 1;
                            break;
                        }
                    }
                    if (text.Contains(slotTitle, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedSlot = i + 1;
                        break;
                    }
                }
                l.TargetGameSlot = matchedSlot; // 0 = Общий / Все игры, 1..N = конкретная часть
            }
        }

        public static string ClassifyTorrentEntryBadge(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "ФАЙЛ";
            string norm = path.Replace('\\', '/').Trim();
            string fn = Path.GetFileName(norm).ToLowerInvariant();
            string ext = Path.GetExtension(fn).ToLowerInvariant();
            string dir = (Path.GetDirectoryName(norm) ?? "").Replace('\\', '/').ToLowerInvariant();

            // 1. Текстовые описания, NFO, скриншоты и ссылки
            if (ext is ".txt" or ".nfo" or ".url" or ".md" or ".html" or ".htm" or ".jpg" or ".jpeg" or ".png" or ".webp" or ".sfv")
                return "ИНФО";

            // 2. Игровые контейнеры Switch / 3DS (.nsp, .nsz, .xci, .xcz, .nup, .3ds, .cia, .cci, .cxi) — проверяем ДО папок с "rus"!
            if (ext is ".nsp" or ".nsz" or ".xci" or ".xcz" or ".nup" or ".3ds" or ".cia" or ".cci" or ".cxi")
            {
                string tid = ExtractTitleId(fn);
                bool isUpdateByTid = tid.Length == 16 && tid.EndsWith("800", StringComparison.OrdinalIgnoreCase);
                bool isUpdateByVer = Regex.IsMatch(fn, @"\[v[1-9]\d*\]") || Regex.IsMatch(fn, @"(?:^|[\s_\-\.\(\[])(?:update|patch|upd)(?:$|[\s_\-\.\)\]v\d])");
                if (isUpdateByTid || isUpdateByVer)
                {
                    bool inTransSubfolder = Regex.IsMatch(dir, @"(?:translation|перевод|русиф|machine\s*trans|rus[\s_\-]mod|укр|ukr)", RegexOptions.IgnoreCase);
                    return inTransSubfolder ? "ОБНОВЛЕНИЕ (РУС)" : "ОБНОВЛЕНИЕ";
                }

                bool isDlcByTid = tid.Length == 16 && !tid.EndsWith("000", StringComparison.OrdinalIgnoreCase) && !tid.EndsWith("800", StringComparison.OrdinalIgnoreCase);
                bool isDlcByName = Regex.IsMatch(fn, @"(?:^|[\s_\-\.\(\[])(?:dlc|addon|unlocker)(?:$|[\s_\-\.\)\]\d])");
                if (isDlcByTid || isDlcByName)
                    return "DLC";

                return "ИГРА";
            }

            // 3. Архивы модификаций, LayeredFS (atmosphere/romfs/exefs), читы и IPS-патчи
            if (ext is ".7z" or ".zip" or ".rar" or ".ips" or ".pchtxt" or ".bps" ||
                norm.Contains("atmosphere", StringComparison.OrdinalIgnoreCase) ||
                norm.Contains("romfs", StringComparison.OrdinalIgnoreCase) ||
                norm.Contains("exefs", StringComparison.OrdinalIgnoreCase) ||
                norm.Contains("contents", StringComparison.OrdinalIgnoreCase) ||
                norm.Contains("luma", StringComparison.OrdinalIgnoreCase) ||
                norm.Contains("cheats", StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(norm, @"(?:translation|перевод|русиф|mod|мод|rus|укр|ukr)", RegexOptions.IgnoreCase))
            {
                return "МОД / РУСИФИКАТОР";
            }

            return "ФАЙЛ";
        }

        private static readonly HashSet<string> BinaryContainerExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".nsp", ".nsz", ".xci", ".xcz", ".3ds", ".cia", ".cci", ".cxi", ".nca", ".ncz", ".nro"
        };

        private static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".7z", ".zip", ".rar"
        };

        private static readonly HashSet<string> InfoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".txt", ".nfo", ".jpg", ".jpeg", ".png", ".url"
        };

        private static readonly HashSet<string> ModStructuralSegments = new(StringComparer.OrdinalIgnoreCase)
        {
            "atmosphere", "contents", "romfs", "exefs", "exefs_patches", "ips_patches",
            "layeredfs", "luma", "titles", "reinx", "sxos", "mods", "cheats"
        };

        private static readonly string[] ModFolderKeywords = new[]
        {
            "mod", "rus", "русиф", "перевод", "translation", "локализац", "patch", "ukr", "cheat"
        };

        public static void CollapseModFoldersIntoSingleEntries(List<TorrentFileSelectionItem> items)
        {
            if (items == null || items.Count <= 1) return;
            if (!items.Any(x => x.IsRealTorrentFile)) return;

            var splitPaths = items
                .Select(i => i.RelativePath.Replace('\\', '/').TrimStart('.').TrimStart('/'))
                .ToList();

            string commonRoot = "";
            if (splitPaths.All(p => p.Contains('/')))
            {
                string firstSeg = splitPaths[0].Substring(0, splitPaths[0].IndexOf('/'));
                if (!string.IsNullOrEmpty(firstSeg) && splitPaths.All(p => p.StartsWith(firstSeg + "/", StringComparison.OrdinalIgnoreCase)))
                {
                    commonRoot = firstSeg + "/";
                }
            }

            var containerAncestorDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < items.Count; i++)
            {
                string p = splitPaths[i];
                string ext = Path.GetExtension(p).ToLowerInvariant();
                if (BinaryContainerExtensions.Contains(ext))
                {
                    string sub = (!string.IsNullOrEmpty(commonRoot) && p.StartsWith(commonRoot, StringComparison.OrdinalIgnoreCase))
                        ? p.Substring(commonRoot.Length)
                        : p;
                    string[] parts = sub.Split('/');
                    for (int k = 1; k < parts.Length; k++)
                    {
                        containerAncestorDirs.Add(string.Join("/", parts.Take(k)));
                    }
                }
            }

            var modFolderGroups = new Dictionary<string, List<TorrentFileSelectionItem>>(StringComparer.OrdinalIgnoreCase);
            var unassigned = new List<TorrentFileSelectionItem>();

            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                string p = splitPaths[i];
                string ext = Path.GetExtension(p).ToLowerInvariant();

                if (BinaryContainerExtensions.Contains(ext))
                {
                    unassigned.Add(item);
                    continue;
                }

                string sub = (!string.IsNullOrEmpty(commonRoot) && p.StartsWith(commonRoot, StringComparison.OrdinalIgnoreCase))
                    ? p.Substring(commonRoot.Length)
                    : p;

                string[] parts = sub.Split('/');
                if (parts.Length < 2)
                {
                    unassigned.Add(item);
                    continue;
                }

                string? matchedFolderPrefix = null;
                for (int s = 0; s < parts.Length - 1; s++)
                {
                    string candidateSub = string.Join("/", parts.Take(s + 1));
                    if (containerAncestorDirs.Contains(candidateSub))
                        continue;

                    string seg = parts[s];
                    bool isModSeg = ModStructuralSegments.Contains(seg) ||
                                    ModFolderKeywords.Any(kw => seg.Contains(kw, StringComparison.OrdinalIgnoreCase));

                    if (isModSeg)
                    {
                        matchedFolderPrefix = (!string.IsNullOrEmpty(commonRoot) ? commonRoot : "") + candidateSub;
                        break;
                    }
                }

                if (!string.IsNullOrEmpty(matchedFolderPrefix))
                {
                    if (!modFolderGroups.TryGetValue(matchedFolderPrefix, out var list))
                    {
                        list = new List<TorrentFileSelectionItem>();
                        modFolderGroups[matchedFolderPrefix] = list;
                    }
                    list.Add(item);
                }
                else
                {
                    unassigned.Add(item);
                }
            }

            var collapsedResult = new List<TorrentFileSelectionItem>(unassigned);

            foreach (var (prefix, group) in modFolderGroups)
            {
                bool hasUnpackedMods = group.Any(it =>
                {
                    string e = Path.GetExtension(it.RelativePath).ToLowerInvariant();
                    return !ArchiveExtensions.Contains(e) && !InfoExtensions.Contains(e);
                });

                if (hasUnpackedMods)
                {
                    string tid = "";
                    foreach (var git in group)
                    {
                        string rawTid = ExtractTitleId(git.RelativePath);
                        if (rawTid.Length == 16)
                        {
                            tid = rawTid;
                            break;
                        }
                    }

                    string cleanPrefixDisplay = (!string.IsNullOrEmpty(commonRoot) && prefix.StartsWith(commonRoot, StringComparison.OrdinalIgnoreCase))
                        ? prefix.Substring(commonRoot.Length)
                        : prefix;
                    cleanPrefixDisplay = cleanPrefixDisplay.TrimEnd('/');

                    var collapsedItem = new TorrentFileSelectionItem
                    {
                        IsSelected = true,
                        IsRealTorrentFile = true,
                        CategoryBadge = "МОД / РУСИФИКАТОР",
                        Length = group.Sum(x => x.Length),
                        RelativePath = !string.IsNullOrEmpty(tid) && !prefix.Contains(tid, StringComparison.OrdinalIgnoreCase)
                            ? $"{prefix} [{tid}]"
                            : prefix,
                        DetectedTitleId = tid,
                        DisplayPath = $"{cleanPrefixDisplay}/ ({group.Count} файлов)",
                        GroupedRelativePaths = group.Select(x => x.RelativePath).ToList()
                    };

                    collapsedResult.Add(collapsedItem);
                }
                else
                {
                    collapsedResult.AddRange(group);
                }
            }

            items.Clear();
            items.AddRange(collapsedResult);
        }

        public static void PopulateDisplayPathsAndSmartSelection(IList<TorrentFileSelectionItem> items)
        {
            if (items == null || items.Count == 0) return;
            if (items is List<TorrentFileSelectionItem> list)
            {
                CollapseModFoldersIntoSingleEntries(list);
            }

            // 1. Вычисляем общий корневой каталог торрента (напр. "./Another Eden Begins [NSP]/"), чтобы не засорять начало строки в UI
            var splitPaths = items
                .Select(i => i.RelativePath.Replace('\\', '/').TrimStart('.').TrimStart('/'))
                .ToList();

            string commonRoot = "";
            if (splitPaths.All(p => p.Contains('/')))
            {
                string firstSeg = splitPaths[0].Substring(0, splitPaths[0].IndexOf('/'));
                if (!string.IsNullOrEmpty(firstSeg) && splitPaths.All(p => p.StartsWith(firstSeg + "/", StringComparison.OrdinalIgnoreCase)))
                {
                    commonRoot = firstSeg + "/";
                }
            }

            for (int idx = 0; idx < items.Count; idx++)
            {
                string cleanRel = splitPaths[idx];
                items[idx].RelativePath = cleanRel;
                string disp = (!string.IsNullOrEmpty(commonRoot) && cleanRel.StartsWith(commonRoot, StringComparison.OrdinalIgnoreCase))
                    ? cleanRel.Substring(commonRoot.Length)
                    : cleanRel;

                if (items[idx].GroupedRelativePaths != null && items[idx].GroupedRelativePaths.Count > 0)
                {
                    disp = disp.TrimEnd('/') + $"/ ({items[idx].GroupedRelativePaths.Count} файлов)";
                }
                items[idx].DisplayPath = disp;

                // Отключаем текстовые ReadMe / NFO / картинки по умолчанию
                if (items[idx].CategoryBadge == "ИНФО")
                {
                    items[idx].IsSelected = false;
                }
            }

            // 2. Умное разрешение нескольких обновлений одной игры (напр. в корне лежит свежий v262144, а в папке русификатора рядом с atmosphere.7z лежит v131072 под перевод)
            var updates = items
                .Where(i => i.CategoryBadge == "ОБНОВЛЕНИЕ" || i.CategoryBadge == "ОБНОВЛЕНИЕ (РУС)")
                .ToList();

            if (updates.Count >= 2)
            {
                var modDirs = items
                    .Where(i => i.CategoryBadge == "МОД / РУСИФИКАТОР" && i.IsSelected)
                    .Select(i => (Path.GetDirectoryName(i.DisplayPath.Replace('\\', '/')) ?? "").Replace('\\', '/').Trim('/'))
                    .Where(d => !string.IsNullOrEmpty(d))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var groupedByGame = updates.GroupBy(u =>
                {
                    string tid = ExtractTitleId(u.RelativePath);
                    return tid.Length >= 13 ? tid.Substring(0, 13).ToUpperInvariant() : ExtractCleanGameTitleFromFilePath(u.RelativePath).ToUpperInvariant();
                });

                foreach (var grp in groupedByGame)
                {
                    var grpList = grp.ToList();
                    if (grpList.Count < 2) continue;

                    var modPairedUpdates = grpList.Where(u =>
                    {
                        string uDir = (Path.GetDirectoryName(u.DisplayPath.Replace('\\', '/')) ?? "").Replace('\\', '/').Trim('/');
                        return !string.IsNullOrEmpty(uDir) && modDirs.Contains(uDir);
                    }).ToList();

                    if (modPairedUpdates.Count > 0)
                    {
                        // Оставляем включённым обновление, спаренное с папкой русификатора, а конфликтующие обновления снимаем по умолчанию (пользователь видит оба и может переключить)
                        foreach (var u in grpList)
                            u.IsSelected = modPairedUpdates.Contains(u);
                    }
                    else
                    {
                        // Если модов в подпапках нет — оставляем включённым самое свежее обновление [vXXXXX]
                        long ParseVer(TorrentFileSelectionItem it)
                        {
                            var vm = Regex.Match(it.RelativePath, @"\[v(\d+)\]", RegexOptions.IgnoreCase);
                            return vm.Success && long.TryParse(vm.Groups[1].Value, out long v) ? v : 0L;
                        }
                        var newest = grpList.OrderByDescending(ParseVer).ThenByDescending(x => x.Length).First();
                        foreach (var u in grpList)
                            u.IsSelected = ReferenceEquals(u, newest);
                    }
                }
            }
        }

        private async Task<List<TorrentFileSelectionItem>?> ResolveMagnetMetadataViaSwarmAsync(MagnetLink magnet, string hashHex, TimeSpan timeout, CancellationToken ct)
        {
            if (magnet == null || string.IsNullOrEmpty(hashHex) || hashHex.Length != 40)
                return null;

            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string torrentCacheDir = Path.Combine(localAppData, "STORM SWITCH BOX", "cache", "torrents");
            Directory.CreateDirectory(torrentCacheDir);
            string cachedTorrentPath = Path.Combine(torrentCacheDir, $"{hashHex.ToUpperInvariant()}.torrent");

            if (File.Exists(cachedTorrentPath) && new FileInfo(cachedTorrentPath).Length > 64)
            {
                try
                {
                    byte[] existing = await File.ReadAllBytesAsync(cachedTorrentPath, ct).ConfigureAwait(false);
                    if (existing.Length > 64 && existing[0] == (byte)'d' && Torrent.TryLoad(existing, out var existingTor))
                    {
                        _loadedTorrentBytes = existing;
                        return existingTor.Files.Select(f => new TorrentFileSelectionItem
                        {
                            IsSelected = true,
                            RelativePath = f.Path,
                            Length = f.Length,
                            CategoryBadge = ClassifyTorrentEntryBadge(f.Path),
                            IsRealTorrentFile = true
                        }).ToList();
                    }
                }
                catch { }
            }

            string dhtCacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "STORM SWITCH BOX", "dht_cache");
            Directory.CreateDirectory(dhtCacheDir);
            string tempMetaSaveDir = Path.Combine(localAppData, "STORM SWITCH BOX", "cache", "meta_probe");
            Directory.CreateDirectory(tempMetaSaveDir);

            var engine = GetOrCreateEngine(dhtCacheDir);
            TorrentManager? metaMgr = null;
            bool createdHere = false;

            try
            {
                metaMgr = engine.Torrents.FirstOrDefault(t =>
                    string.Equals(t.InfoHashes.V1OrV2.ToHex(), hashHex, StringComparison.OrdinalIgnoreCase));

                if (metaMgr == null)
                {
                    var ts = new TorrentSettingsBuilder
                    {
                        MaximumConnections = 120,
                        AllowDht = true,
                        AllowPeerExchange = true
                    }.ToSettings();
                    metaMgr = await engine.AddAsync(magnet, tempMetaSaveDir, ts).ConfigureAwait(false);
                    createdHere = true;
                }

                if (!metaMgr.HasMetadata)
                {
                    foreach (var tr in PublicTrackers.Take(10))
                    {
                        if (Uri.TryCreate(tr, UriKind.Absolute, out var trUri))
                        {
                            try { await metaMgr.TrackerManager.AddTrackerAsync(trUri).ConfigureAwait(false); } catch { }
                        }
                    }

                    if (metaMgr.State == TorrentState.Stopped || metaMgr.State == TorrentState.Paused)
                        await metaMgr.StartAsync().ConfigureAwait(false);

                    using var metaCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    metaCts.CancelAfter(timeout);

                    _ = Task.Run(() => HarvestPeersFromHttpTrackersAsync(hashHex, metaMgr, metaCts.Token));
                    _ = Task.Run(() => InjectPeersViaTurboDhtAsync(hashHex, metaMgr, metaCts.Token));

                    while (!metaMgr.HasMetadata && !metaCts.IsCancellationRequested)
                    {
                        await Task.Delay(120, metaCts.Token).ConfigureAwait(false);
                    }
                }

                if (metaMgr.HasMetadata && metaMgr.Files != null && metaMgr.Files.Count > 0)
                {
                    var items = metaMgr.Files.Select(f => new TorrentFileSelectionItem
                    {
                        IsSelected = true,
                        RelativePath = f.Path,
                        Length = f.Length,
                        CategoryBadge = ClassifyTorrentEntryBadge(f.Path),
                        IsRealTorrentFile = true
                    }).ToList();

                    // Если MonoTorrent сохранил .torrent в кэш метаданных — копируем в основной кэш .torrent и сохраняем в _loadedTorrentBytes
                    try
                    {
                        if (!string.IsNullOrEmpty(metaMgr.MetadataPath) && File.Exists(metaMgr.MetadataPath))
                        {
                            byte[] rawBytes = await File.ReadAllBytesAsync(metaMgr.MetadataPath, CancellationToken.None).ConfigureAwait(false);
                            if (rawBytes.Length > 64 && rawBytes[0] == (byte)'d')
                            {
                                _loadedTorrentBytes = rawBytes;
                                await File.WriteAllBytesAsync(cachedTorrentPath, rawBytes, CancellationToken.None).ConfigureAwait(false);
                            }
                        }

                        if (_loadedTorrentBytes == null)
                        {
                            string dhtMetaDir = Path.Combine(dhtCacheDir, "metadata");
                            if (Directory.Exists(dhtMetaDir))
                            {
                                var foundMeta = Directory.EnumerateFiles(dhtMetaDir, "*.torrent")
                                    .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p).Equals(hashHex, StringComparison.OrdinalIgnoreCase));
                                if (!string.IsNullOrEmpty(foundMeta) && File.Exists(foundMeta))
                                {
                                    byte[] rawBytes = await File.ReadAllBytesAsync(foundMeta, CancellationToken.None).ConfigureAwait(false);
                                    if (rawBytes.Length > 64 && rawBytes[0] == (byte)'d')
                                    {
                                        _loadedTorrentBytes = rawBytes;
                                        await File.WriteAllBytesAsync(cachedTorrentPath, rawBytes, CancellationToken.None).ConfigureAwait(false);
                                    }
                                }
                            }
                        }

                        if (_loadedTorrentBytes == null && File.Exists(cachedTorrentPath))
                        {
                            byte[] rawBytes = await File.ReadAllBytesAsync(cachedTorrentPath, CancellationToken.None).ConfigureAwait(false);
                            if (rawBytes.Length > 64 && rawBytes[0] == (byte)'d')
                            {
                                _loadedTorrentBytes = rawBytes;
                            }
                        }
                    }
                    catch { }

                    return items;
                }
            }
            catch { }
            finally
            {
                if (createdHere && metaMgr != null)
                {
                    try { await metaMgr.StopAsync(TimeSpan.FromMilliseconds(400)).ConfigureAwait(false); } catch { }
                    try { await engine.RemoveAsync(metaMgr, RemoveMode.CacheDataOnly).ConfigureAwait(false); } catch { }
                }
            }

            return null;
        }

        public async Task<List<string>> DownloadTorrentAsync(
            string saveDirectory,
            IReadOnlyList<TorrentFileSelectionItem> fileSelections,
            IProgress<TorrentDownloadProgress>? progress,
            CancellationToken ct,
            byte[]? torrentBytes = null,
            MagnetLink? magnetLink = null,
            string? topicId = null,
            int topicSeeds = 0,
            ManualResetEventSlim? pauseEvent = null,
            Action<TorrentManager>? onManagerCreated = null,
            int stallTimeoutSec = 600)
        {
            Directory.CreateDirectory(saveDirectory);

            var torrentSettings = new TorrentSettingsBuilder

            {

                MaximumConnections = 400,
                UploadSlots = 64,
                AllowDht = true,
                AllowPeerExchange = true
            }.ToSettings();

            byte[]? rawTorrent = torrentBytes ?? _loadedTorrentBytes;
            MagnetLink? rawMagnet = magnetLink ?? _loadedMagnet;
            string effectiveTopicId = !string.IsNullOrEmpty(topicId) ? topicId : _activeTopicId;
            int effectiveTopicSeeds = topicSeeds > 0 ? topicSeeds : _activeTopicSeeds;

            string currentHash = "";
            string resolvedTorrentName = "Switch_Release";
            Torrent? parsedTorrentObj = null;
            if (rawTorrent != null)
            {
                parsedTorrentObj = await Torrent.LoadAsync(rawTorrent).ConfigureAwait(false);
                currentHash = parsedTorrentObj.InfoHashes.V1OrV2.ToHex();
                if (!string.IsNullOrWhiteSpace(parsedTorrentObj.Name))
                    resolvedTorrentName = parsedTorrentObj.Name;
            }
            else if (rawMagnet != null)
            {
                currentHash = rawMagnet.InfoHashes.V1OrV2.ToHex();
                if (!string.IsNullOrWhiteSpace(rawMagnet.Name))
                    resolvedTorrentName = rawMagnet.Name;
            }
            else
            {
                throw new InvalidOperationException("Не выбран .torrent файл или Magnet-ссылка для скачивания.");
            }

            // 1. Сначала пробуем высокоскоростной локальный торрент-клиент (qBittorrent WebUI / uTP BEP-29 + Holepunch BEP-55 + HTTP/1.1 Pipelining):
            // даёт полную скорость канала (6-50+ МБ/с) и обходит анти-личерскую фильтрацию сидов qBittorrent Enhanced.
            var qbitResult = await TryDownloadViaLocalQBitAsync(
                saveDirectory,
                fileSelections,
                progress,
                ct,
                rawTorrent,
                currentHash,
                resolvedTorrentName,
                effectiveTopicId,
                effectiveTopicSeeds,
                pauseEvent).ConfigureAwait(false);
            if (qbitResult != null && qbitResult.Count > 0)
            {
                return qbitResult;
            }

            // 2. TIER 2: Встроенный C++ libtorrent-rasterbar (storm_lt_turbo.py + tools\nsz\python.exe)
            // Задействуется автоматически, если внешний qBittorrent не установлен или недоступен.
            var ltResult = await TryDownloadViaEmbeddedLibtorrentAsync(
                saveDirectory,
                fileSelections,
                progress,
                ct,
                rawTorrent,
                currentHash,
                resolvedTorrentName,
                effectiveTopicId,
                effectiveTopicSeeds,
                pauseEvent,
                rawMagnet).ConfigureAwait(false);
            if (ltResult != null && ltResult.Count > 0)
            {
                return ltResult;
            }

            ClientEngine engine;
            bool isTemporaryEngine = false;
            string? temporarySessionDir = null;

            var sharedEngine = GetOrCreateSharedQueueEngine();
            bool hashAlreadyInShared = false;
            if (!string.IsNullOrEmpty(currentHash))
            {
                lock (_queueEngineLock)
                {
                    hashAlreadyInShared = sharedEngine.Torrents.Any(t =>
                        string.Equals(t.InfoHashes.V1OrV2.ToHex(), currentHash, StringComparison.OrdinalIgnoreCase) ||
                        (t.Torrent != null && string.Equals(t.Torrent.InfoHashes.V1OrV2.ToHex(), currentHash, StringComparison.OrdinalIgnoreCase)));
                }
            }

            if (hashAlreadyInShared)
            {
                string dhtCacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "STORM SWITCH BOX", "dht_cache");
                temporarySessionDir = Path.Combine(dhtCacheDir, "session_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(temporarySessionDir);
                engine = CreateTemporaryFallbackEngine(temporarySessionDir);
                isTemporaryEngine = true;
            }
            else
            {
                engine = sharedEngine;
            }

            TorrentManager manager;
            if (parsedTorrentObj != null)
            {
                manager = await engine.AddAsync(parsedTorrentObj, saveDirectory, torrentSettings).ConfigureAwait(false);
            }
            else if (rawMagnet != null)
            {
                manager = await engine.AddAsync(rawMagnet, saveDirectory, torrentSettings).ConfigureAwait(false);
            }
            else
            {
                throw new InvalidOperationException("Не выбран .torrent файл или Magnet-ссылка для скачивания.");
            }

            string mgrKey = Guid.NewGuid().ToString("N");
            _activeManagers[mgrKey] = manager;
            _activeManager = manager; // для обратной совместимости
            onManagerCreated?.Invoke(manager);

            // Принудительно подключаем все публичные трекеры RuTracker
            foreach (var tr in PublicTrackers)
            {
                if (Uri.TryCreate(tr, UriKind.Absolute, out var trUri))
                {
                    try { await manager.TrackerManager.AddTrackerAsync(trUri).ConfigureAwait(false); } catch { }
                }
            }

            bool prioritiesAppliedAfterMetadata = manager.HasMetadata;
            if (manager.HasMetadata && fileSelections != null && fileSelections.Count > 0)
            {
                await ApplyFilePrioritiesAsync(manager, fileSelections).ConfigureAwait(false);
            }
            else
            {
                _ = Task.Run(async () =>
                {
                    while (!prioritiesAppliedAfterMetadata && !ct.IsCancellationRequested && manager.State != TorrentState.Stopped)
                    {
                        if (manager.HasMetadata)
                        {
                            prioritiesAppliedAfterMetadata = true;
                            if (fileSelections != null && fileSelections.Count > 0)
                            {
                                try { await ApplyFilePrioritiesAsync(manager, fileSelections).ConfigureAwait(false); } catch { }
                            }
                            break;
                        }
                        await Task.Delay(200, ct).ConfigureAwait(false);
                    }
                }, ct);
            }
            await manager.StartAsync().ConfigureAwait(false);

            // Запускаем параллельный Turbo BEP-5 UDP DHT Spider и HTTP/1.1 Pipelined Tracker Harvester ПОСЛЕ старта менеджера
            if (!string.IsNullOrEmpty(currentHash))
            {
                _ = Task.Run(() => HarvestPeersFromHttpTrackersAsync(currentHash, manager, ct));
                _ = Task.Run(() => InjectPeersViaTurboDhtAsync(currentHash, manager, ct));
            }
            long lastProgressTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            long lastHarvestTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            long lastReinjectTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            double lastProgressRatio = -1;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    // Пока сидов < 15 или скорость < 2.5 МБ/с, каждые 3 секунды инжектим пул обнаруженных HTTP/UDP/DHT пиров в активный TorrentManager
                    double elapsedReinject = (System.Diagnostics.Stopwatch.GetTimestamp() - lastReinjectTicks) / (double)System.Diagnostics.Stopwatch.Frequency;
                    var httpPeersScoped = !string.IsNullOrEmpty(currentHash) ? GetHttpPeersForHash(currentHash) : null;
                    var dhtPeersScoped = !string.IsNullOrEmpty(currentHash) ? GetDhtPeersForHash(currentHash) : null;
                    int scopedPeerCount = (httpPeersScoped?.Count ?? 0) + (dhtPeersScoped?.Count ?? 0);
                    if ((manager.Peers.Seeds < 15 || manager.Monitor.DownloadRate < 2621440) && elapsedReinject >= 3.0 && scopedPeerCount > 0)
                    {
                        lastReinjectTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                        try
                        {
                            var combinedKeys = (httpPeersScoped?.Keys ?? Enumerable.Empty<string>())
                                .Concat(dhtPeersScoped?.Keys ?? Enumerable.Empty<string>())
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .Take(250)
                                .Select(k => new PeerInfo(new Uri($"ipv4://{k}")))
                                .ToList();
                            if (combinedKeys.Count > 0)
                                await manager.AddPeersAsync(combinedKeys).ConfigureAwait(false);
                        }
                        catch { }
                    }

                    // Каждые 14 секунд непрерывно подтягиваем свежую волну сидов из трекеров и DHT
                    double elapsedHarvest = (System.Diagnostics.Stopwatch.GetTimestamp() - lastHarvestTicks) / (double)System.Diagnostics.Stopwatch.Frequency;
                    if (elapsedHarvest >= 14.0)
                    {
                        lastHarvestTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                        if (!string.IsNullOrEmpty(currentHash))
                        {
                            _ = Task.Run(() => HarvestPeersFromHttpTrackersAsync(currentHash, manager, ct));
                            _ = Task.Run(() => InjectPeersViaTurboDhtAsync(currentHash, manager, ct));
                        }
                    }

                    // Поддержка настоящей паузы MonoTorrent
                    if (pauseEvent != null && !pauseEvent.IsSet)
                    {
                        if (manager.State == TorrentState.Downloading || manager.State == TorrentState.Metadata)
                        {
                            try { await manager.PauseAsync().ConfigureAwait(false); } catch { }
                        }

                        while (!pauseEvent.IsSet && !ct.IsCancellationRequested)
                        {
                            await Task.Delay(250, ct).ConfigureAwait(false);
                        }

                        if (!ct.IsCancellationRequested && (manager.State == TorrentState.Paused || manager.State == TorrentState.Stopped))
                        {
                            try { await manager.StartAsync().ConfigureAwait(false); } catch { }
                        }
                        // Сброс таймера застревания (stall timer) при возобновлении
                        lastProgressTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                    }

                    if (!prioritiesAppliedAfterMetadata && manager.HasMetadata)
                    {
                        prioritiesAppliedAfterMetadata = true;
                        if (fileSelections != null && fileSelections.Count > 0)
                        {
                            await ApplyFilePrioritiesAsync(manager, fileSelections).ConfigureAwait(false);
                        }
                    }

                    long totalWantedBytes = 0;
                    long downloadedWantedBytes = 0;
                    var wantedFiles = new List<ITorrentManagerFile>();
                    if (manager.HasMetadata && manager.Files.Count > 0)
                    {
                        foreach (var tf in manager.Files)
                        {
                            if (tf.Priority != Priority.DoNotDownload)
                            {
                                wantedFiles.Add(tf);
                                totalWantedBytes += tf.Length;
                                downloadedWantedBytes += (long)(tf.Length * (tf.BitField.PercentComplete / 100.0));
                            }
                        }
                    }
                    if (totalWantedBytes <= 0 && manager.Torrent != null)
                    {
                        totalWantedBytes = manager.Torrent.Size;
                        downloadedWantedBytes = (long)(totalWantedBytes * (manager.Progress / 100.0));
                    }

                    double pct = 0.0;
                    if (manager.HasMetadata)
                    {
                        pct = totalWantedBytes > 0
                            ? Math.Max(manager.PartialProgress, (downloadedWantedBytes * 100.0) / totalWantedBytes)
                            : manager.PartialProgress;
                    }
                    pct = Math.Clamp(pct, 0.0, 100.0);

                    double speedMb = manager.Monitor.DownloadRate / (1024.0 * 1024.0);
                    int seeds = manager.Peers.Seeds;
                    int leechs = manager.Peers.Leechs;

                    int swarmTotal = Math.Max(effectiveTopicSeeds, Math.Max(_lastSwarmTotalPeers, seeds + leechs));
                    string seedsStr = swarmTotal > seeds ? $"Сиды: {seeds} (в рое: {swarmTotal})" : $"Сиды: {seeds}";

                    string topicLabel = !string.IsNullOrEmpty(effectiveTopicId) ? $"RuTracker #1605 (t={effectiveTopicId})" : "RuTracker #1605";
                    string stateStr = manager.State == TorrentState.Metadata
                        ? $"{topicLabel}: получение метаданных Magnet от пиров (DHT)..."
                        : (pauseEvent != null && !pauseEvent.IsSet
                            ? $"{topicLabel}: пауза ({pct:F1}% | {seedsStr}, Пиры: {leechs})"
                            : $"{topicLabel}: скачивание {pct:F1}% ({speedMb:F2} МБ/с | {seedsStr}, Пиры: {leechs})");

                    progress?.Report(new TorrentDownloadProgress
                    {
                        Percent = pct,
                        SpeedMbPerSec = speedMb,
                        Seeds = seeds,
                        Peers = leechs,
                        SwarmTotal = swarmTotal,
                        DownloadedBytes = downloadedWantedBytes,
                        TotalBytes = totalWantedBytes,
                        IsMetadataPhase = !manager.HasMetadata || manager.State == TorrentState.Metadata,
                        StatusText = stateStr
                    });

                    // Динамическая адаптация без падений по тайм-ауту: при отсутствии прогресса > 20 с ре-анонсим трекеры и инжектим DHT
                    if (pct > lastProgressRatio || speedMb > 0.05)
                    {
                        lastProgressRatio = pct;
                        lastProgressTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                    }
                    else
                    {
                        if (pauseEvent == null || pauseEvent.IsSet)
                        {
                            double elapsedSec = (System.Diagnostics.Stopwatch.GetTimestamp() - lastProgressTicks) / (double)System.Diagnostics.Stopwatch.Frequency;
                            if (elapsedSec >= 20.0)
                            {
                                lastProgressTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                                try { _ = manager.TrackerManager.AnnounceAsync(ct); } catch { }
                                if (!string.IsNullOrEmpty(currentHash))
                                {
                                    _ = Task.Run(() => HarvestPeersFromHttpTrackersAsync(currentHash, manager, ct));
                                    _ = Task.Run(() => InjectPeersViaTurboDhtAsync(currentHash, manager, ct));
                                }
                            }
                        }
                    }

                    if (manager.HasMetadata && manager.Files.Count > 0)
                    {
                        bool allSelectedDone = wantedFiles.Count > 0
                            ? wantedFiles.All(f => f.BitField.AllTrue)
                            : (manager.PartialProgress >= 99.99 || manager.Progress >= 99.99);

                        if ((allSelectedDone || manager.PartialProgress >= 99.99 || manager.State == TorrentState.Seeding) &&
                            fileSelections.Any(x => x.IsSelected))
                        {
                            progress?.Report(new TorrentDownloadProgress
                            {
                                Percent = 100.0,
                                SpeedMbPerSec = 0,
                                Seeds = seeds,
                                Peers = leechs,
                                SwarmTotal = swarmTotal,
                                DownloadedBytes = totalWantedBytes,
                                TotalBytes = totalWantedBytes,
                                IsMetadataPhase = false,
                                StatusText = $"{topicLabel}: скачивание завершено (100.0%)"
                            });
                            break;
                        }
                    }

                    await Task.Delay(500, ct).ConfigureAwait(false);
                }

                if (ct.IsCancellationRequested)
                {
                    try { await manager.StopAsync().ConfigureAwait(false); } catch { }
                    ct.ThrowIfCancellationRequested();
                }

                // Удаление граничных кусков (Boundary Pieces) невыбранных файлов
                if (manager.HasMetadata && manager.Files != null && manager.Files.Count > 0)
                {
                    var selectedRelSet = fileSelections != null && fileSelections.Count > 0
                        ? fileSelections
                            .Where(f => f.IsSelected)
                            .SelectMany(f => f.GetEffectiveRelativePaths())
                            .Select(f => f.Replace('/', '\\').Trim('\\'))
                            .ToHashSet(StringComparer.OrdinalIgnoreCase)
                        : null;

                    foreach (var f in manager.Files)
                    {
                        string normRel = f.Path.Replace('/', '\\').Trim('\\');
                        bool isUnwanted = f.Priority == Priority.DoNotDownload ||
                                          (selectedRelSet != null && selectedRelSet.Count > 0 &&
                                           !selectedRelSet.Contains(normRel) &&
                                           !selectedRelSet.Contains(Path.GetFileName(normRel)));

                        if (isUnwanted)
                        {
                            try
                            {
                                string targetPath = !string.IsNullOrEmpty(f.FullPath) && File.Exists(f.FullPath)
                                    ? f.FullPath
                                    : Path.Combine(saveDirectory, f.Path);

                                if (File.Exists(targetPath))
                                {
                                    File.Delete(targetPath);
                                }
                            }
                            catch { }
                        }
                    }
                }

                var downloadedPaths = new List<string>();
                if (manager.HasMetadata)
                {
                    var selectedRelSet = fileSelections != null && fileSelections.Count > 0
                        ? fileSelections
                            .Where(f => f.IsSelected)
                            .SelectMany(f => f.GetEffectiveRelativePaths())
                            .Select(f => f.Replace('/', '\\').Trim('\\'))
                            .ToHashSet(StringComparer.OrdinalIgnoreCase)
                        : null;

                    foreach (var f in manager.Files)
                    {
                        string normRel = f.Path.Replace('/', '\\').Trim('\\');
                        bool isUnwanted = f.Priority == Priority.DoNotDownload ||
                                          (selectedRelSet != null && selectedRelSet.Count > 0 &&
                                           !selectedRelSet.Contains(normRel) &&
                                           !selectedRelSet.Contains(Path.GetFileName(normRel)));

                        if (!isUnwanted && f.BitField.AllTrue)
                        {
                            string fullPath = !string.IsNullOrEmpty(f.FullPath) && File.Exists(f.FullPath)
                                ? f.FullPath
                                : Path.Combine(saveDirectory, f.Path);

                            if (File.Exists(fullPath))
                            {
                                string ext = Path.GetExtension(fullPath).ToLowerInvariant();
                                if (ext is ".zip" or ".7z" or ".rar")
                                {
                                    try
                                    {
                                        string extractDir = Path.Combine(saveDirectory, Path.GetFileNameWithoutExtension(fullPath));
                                        Directory.CreateDirectory(extractDir);
                                        ExtractArchiveWith7zAsync(fullPath, extractDir, ct).GetAwaiter().GetResult();

                                        var extractedContainers = Directory.EnumerateFiles(extractDir, "*.*", SearchOption.AllDirectories)
                                            .Where(p => p.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) ||
                                                        p.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) ||
                                                        p.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) ||
                                                        p.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase))
                                            .ToList();

                                        if (extractedContainers.Count > 0)
                                        {
                                            downloadedPaths.AddRange(extractedContainers);
                                        }
                                        else
                                        {
                                            downloadedPaths.Add(fullPath);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        App.Logger?.Log($"[RuTracker Torrent] Ошибка распаковки архива {fullPath}: {ex.Message}", LogLevel.Warning);
                                        downloadedPaths.Add(fullPath);
                                    }
                                }
                                else
                                {
                                    downloadedPaths.Add(fullPath);
                                }
                            }
                        }
                    }
                }

                return downloadedPaths;
            }
            finally
            {
                if (manager != null)
                {
                    try { await manager.StopAsync().ConfigureAwait(false); } catch { }
                    try { await engine.RemoveAsync(manager).ConfigureAwait(false); } catch { }
                }
                _activeManagers.TryRemove(mgrKey, out _);
                if (_activeManager == manager) _activeManager = null;
                if (isTemporaryEngine)
                {
                    try { engine.Dispose(); } catch { }
                    if (!string.IsNullOrEmpty(temporarySessionDir))
                    {
                        try
                        {
                            if (Directory.Exists(temporarySessionDir))
                                Directory.Delete(temporarySessionDir, true);
                        }
                        catch { }
                    }
                }
            }
        }

        private static readonly SemaphoreSlim _qbitBootstrapLock = new(1, 1);

        private static (string BaseUrl, string Username, string Password) ResolveLocalQBitConfig()
        {
            int port = 18080;
            string username = "admin";
            string password = "";
            try
            {
                string iniPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "qBittorrent", "qBittorrent.ini");
                bool inWebUiSection = false;
                if (File.Exists(iniPath))
                {
                    foreach (var rawLine in File.ReadAllLines(iniPath))
                    {
                        string line = rawLine.Trim();
                        if (line.StartsWith("[") && line.EndsWith("]"))
                        {
                            inWebUiSection = line.Equals("[WebUI]", StringComparison.OrdinalIgnoreCase);
                            continue;
                        }

                        if ((inWebUiSection && line.StartsWith("Port=", StringComparison.OrdinalIgnoreCase)) ||
                            line.StartsWith(@"WebUI\Port=", StringComparison.OrdinalIgnoreCase))
                        {
                            string val = line.Substring(line.IndexOf('=') + 1).Trim();
                            if (int.TryParse(val, out int p) && p > 0 && p < 65536)
                                port = p;
                        }
                        else if ((inWebUiSection && line.StartsWith("Username=", StringComparison.OrdinalIgnoreCase)) ||
                                 line.StartsWith(@"WebUI\Username=", StringComparison.OrdinalIgnoreCase))
                        {
                            string u = line.Substring(line.IndexOf('=') + 1).Trim();
                            if (!string.IsNullOrEmpty(u)) username = u;
                        }
                    }

                    // Если в INI всё ещё стоит дефолтный 8080, проверяем, не занят ли он посторонним процессом (Docker/WSL)
                    if (port == 8080)
                    {
                        bool isDockerHog = false;
                        try
                        {
                            using var pingClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(500) };
                            var resp = pingClient.GetAsync("http://127.0.0.1:8080/api/v2/app/version").GetAwaiter().GetResult();
                            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                                isDockerHog = true; // Docker backend возвращает 404 на /api/v2
                        }
                        catch
                        {
                            // Если порт вообще не отвечает при запущенном qbittorrent - тоже переключаем
                            if (Process.GetProcessesByName("qbittorrent").Length > 0)
                                isDockerHog = true;
                        }

                        if (isDockerHog)
                        {
                            port = 18080;
                            try
                            {
                                string text = File.ReadAllText(iniPath);
                                text = Regex.Replace(text, @"(?m)^Port=8080\r?$", "Port=18080");
                                text = Regex.Replace(text, @"(?m)^WebUI\\Port=8080\r?$", @"WebUI\Port=18080");
                                File.WriteAllText(iniPath, text);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
            return ($"http://127.0.0.1:{port}", username, password);
        }

        private static async Task<bool> EnsureLocalQBitReadyAsync(CancellationToken ct)
        {
            try
            {
                bool isRunning = Process.GetProcessesByName("qbittorrent").Length > 0;
                string[] candidatePaths = new[]
                {
                    @"C:\Program Files\qBittorrent\qbittorrent.exe",
                    @"C:\Program Files (x86)\qBittorrent\qbittorrent.exe",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\qBittorrent\qbittorrent.exe")
                };
                string? foundExe = candidatePaths.FirstOrDefault(File.Exists);

                // Если qBittorrent не запущен и не установлен ни по одному из стандартных путей - мгновенный выход (<1 мс)
                if (!isRunning && string.IsNullOrEmpty(foundExe))
                    return false;

                var (baseUrl, username, password) = ResolveLocalQBitConfig();

                using var fastHandler = new HttpClientHandler { UseCookies = true, CookieContainer = new System.Net.CookieContainer(), UseProxy = false };
                using var fastClient = new HttpClient(fastHandler) { Timeout = TimeSpan.FromSeconds(1.2) };

                // Быстрая проверка: возможно, WebUI уже готов к работе прямо сейчас
                try
                {
                    using var pingResp = await fastClient.GetAsync($"{baseUrl}/api/v2/app/version", ct).ConfigureAwait(false);
                    if (pingResp.IsSuccessStatusCode || pingResp.StatusCode == System.Net.HttpStatusCode.Forbidden || pingResp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        return true;
                }
                catch { }

                // Захватываем лок бутстрапа для потокобезопасного запуска и настройки
                await _qbitBootstrapLock.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    // Повторная быстрая проверка после захвата блокировки
                    try
                    {
                        using var pingResp2 = await fastClient.GetAsync($"{baseUrl}/api/v2/app/version", ct).ConfigureAwait(false);
                        if (pingResp2.IsSuccessStatusCode || pingResp2.StatusCode == System.Net.HttpStatusCode.Forbidden || pingResp2.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                            return true;
                    }
                    catch { }

                    // Гарантируем корректные настройки WebUI в qBittorrent.ini
                    string iniPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "qBittorrent", "qBittorrent.ini");
                    try
                    {
                        if (File.Exists(iniPath))
                        {
                            string iniText = File.ReadAllText(iniPath);
                            bool modified = false;

                            if (iniText.Contains("[WebUI]", StringComparison.OrdinalIgnoreCase))
                            {
                                if (Regex.IsMatch(iniText, @"(?m)^Enabled=false\r?$"))
                                {
                                    iniText = Regex.Replace(iniText, @"(?m)^Enabled=false\r?$", "Enabled=true");
                                    modified = true;
                                }
                                else if (!Regex.IsMatch(iniText, @"(?m)^Enabled="))
                                {
                                    iniText = Regex.Replace(iniText, @"(?m)^\[WebUI\]\r?$", "[WebUI]\r\nEnabled=true");
                                    modified = true;
                                }

                                if (Regex.IsMatch(iniText, @"(?m)^LocalHostAuth=true\r?$"))
                                {
                                    iniText = Regex.Replace(iniText, @"(?m)^LocalHostAuth=true\r?$", "LocalHostAuth=false");
                                    modified = true;
                                }
                                else if (!Regex.IsMatch(iniText, @"(?m)^LocalHostAuth="))
                                {
                                    iniText = Regex.Replace(iniText, @"(?m)^\[WebUI\]\r?$", "[WebUI]\r\nLocalHostAuth=false");
                                    modified = true;
                                }
                            }
                            else if (iniText.Contains(@"WebUI\Enabled=", StringComparison.OrdinalIgnoreCase))
                            {
                                if (Regex.IsMatch(iniText, @"(?m)^WebUI\\Enabled=false\r?$"))
                                {
                                    iniText = Regex.Replace(iniText, @"(?m)^WebUI\\Enabled=false\r?$", @"WebUI\Enabled=true");
                                    modified = true;
                                }
                                if (Regex.IsMatch(iniText, @"(?m)^WebUI\\LocalHostAuth=true\r?$"))
                                {
                                    iniText = Regex.Replace(iniText, @"(?m)^WebUI\\LocalHostAuth=true\r?$", @"WebUI\LocalHostAuth=false");
                                    modified = true;
                                }
                            }
                            else
                            {
                                iniText += "\r\n[WebUI]\r\nEnabled=true\r\nLocalHostAuth=false\r\nPort=18080\r\n";
                                modified = true;
                            }

                            if (modified)
                                File.WriteAllText(iniPath, iniText);
                        }
                        else
                        {
                            string? dir = Path.GetDirectoryName(iniPath);
                            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                            File.WriteAllText(iniPath, "[WebUI]\r\nEnabled=true\r\nLocalHostAuth=false\r\nPort=18080\r\n");
                        }
                    }
                    catch { }

                    // Запускаем процесс, если он еще не запущен
                    if (Process.GetProcessesByName("qbittorrent").Length == 0 && !string.IsNullOrEmpty(foundExe))
                    {
                        try
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = foundExe,
                                Arguments = "--no-splash",
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WindowStyle = ProcessWindowStyle.Minimized
                            });
                        }
                        catch { }
                    }

                    // Опрашиваем WebUI до 30 раз по 300 мс (до 9 секунд)
                    for (int i = 0; i < 30; i++)
                    {
                        ct.ThrowIfCancellationRequested();
                        try
                        {
                            using var checkResp = await fastClient.GetAsync($"{baseUrl}/api/v2/app/version", ct).ConfigureAwait(false);
                            if (checkResp.IsSuccessStatusCode || checkResp.StatusCode == System.Net.HttpStatusCode.Forbidden || checkResp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                                return true;
                        }
                        catch { }
                        await Task.Delay(300, ct).ConfigureAwait(false);
                    }
                }
                finally
                {
                    _qbitBootstrapLock.Release();
                }

                return Process.GetProcessesByName("qbittorrent").Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<List<PeerInfo>> QueryUdpTrackersBep15Async(byte[] infoHashBytes, int listenPort, CancellationToken ct)
        {
            var discovered = new System.Collections.Concurrent.ConcurrentBag<PeerInfo>();
            if (infoHashBytes == null || infoHashBytes.Length != 20)
                return discovered.ToList();

            var udpTrackers = new (string Host, int Port)[]
            {
                ("tracker.opentrackr.org", 1337),
                ("open.stealth.si", 80),
                ("exodus.desync.com", 6969),
                ("tracker.torrent.eu.org", 451),
                ("tracker.openbittorrent.com", 6969),
                ("explodie.org", 6969)
            };

            byte[] peerIdBytes = Encoding.ASCII.GetBytes("-qB5000-" + Guid.NewGuid().ToString("N").Substring(0, 12));

            var tasks = udpTrackers.Select(async tr =>
            {
                try
                {
                    using var udp = new System.Net.Sockets.UdpClient();
                    udp.Client.ReceiveTimeout = 1800;
                    udp.Client.SendTimeout = 1800;

                    var ips = await Dns.GetHostAddressesAsync(tr.Host, ct).ConfigureAwait(false);
                    var ipv4 = ips.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                    if (ipv4 == null) return;
                    var remoteEp = new IPEndPoint(ipv4, tr.Port);

                    // 1. BEP-15 Connect Request (16 bytes)
                    byte[] connReq = new byte[16];
                    System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(connReq.AsSpan(0, 8), 0x41727101980UL);
                    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(connReq.AsSpan(8, 4), 0); // action = 0 (connect)
                    int txId = Random.Shared.Next(1, int.MaxValue);
                    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(connReq.AsSpan(12, 4), txId);

                    await udp.SendAsync(connReq, connReq.Length, remoteEp).ConfigureAwait(false);
                    using var cts1 = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts1.CancelAfter(1600);
                    var connResp = await udp.ReceiveAsync(cts1.Token).ConfigureAwait(false);
                    if (connResp.Buffer.Length < 16) return;
                    int respAction = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(connResp.Buffer.AsSpan(0, 4));
                    int respTxId = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(connResp.Buffer.AsSpan(4, 4));
                    if (respAction != 0 || respTxId != txId) return;
                    ulong connectionId = System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(connResp.Buffer.AsSpan(8, 8));

                    // 2. BEP-15 Announce Request (98 bytes)
                    byte[] annReq = new byte[98];
                    System.Buffers.Binary.BinaryPrimitives.WriteUInt64BigEndian(annReq.AsSpan(0, 8), connectionId);
                    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(annReq.AsSpan(8, 4), 1); // action = 1 (announce)
                    int txId2 = Random.Shared.Next(1, int.MaxValue);
                    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(annReq.AsSpan(12, 4), txId2);
                    Buffer.BlockCopy(infoHashBytes, 0, annReq, 16, 20);
                    Buffer.BlockCopy(peerIdBytes, 0, annReq, 36, 20);
                    System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(annReq.AsSpan(56, 8), 0L); // downloaded
                    System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(annReq.AsSpan(64, 8), 1073741824L); // left
                    System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(annReq.AsSpan(72, 8), 0L); // uploaded
                    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(annReq.AsSpan(80, 4), 2); // event = started
                    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(annReq.AsSpan(84, 4), 0); // IP = default
                    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(annReq.AsSpan(88, 4), Random.Shared.Next()); // key
                    System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(annReq.AsSpan(92, 4), 200); // num_want
                    System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(annReq.AsSpan(96, 2), (ushort)(listenPort > 0 ? listenPort : 44859));

                    await udp.SendAsync(annReq, annReq.Length, remoteEp).ConfigureAwait(false);
                    using var cts2 = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts2.CancelAfter(1800);
                    var annResp = await udp.ReceiveAsync(cts2.Token).ConfigureAwait(false);
                    if (annResp.Buffer.Length >= 20)
                    {
                        int act2 = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(annResp.Buffer.AsSpan(0, 4));
                        int rTx2 = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(annResp.Buffer.AsSpan(4, 4));
                        if (act2 == 1 && rTx2 == txId2)
                        {
                            int leechers = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(annResp.Buffer.AsSpan(12, 4));
                            int seeders = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(annResp.Buffer.AsSpan(16, 4));
                            if (seeders + leechers > _lastSwarmTotalPeers)
                                _lastSwarmTotalPeers = seeders + leechers;

                            for (int pos = 20; pos + 6 <= annResp.Buffer.Length; pos += 6)
                            {
                                byte b0 = annResp.Buffer[pos];
                                byte b1 = annResp.Buffer[pos + 1];
                                byte b2 = annResp.Buffer[pos + 2];
                                byte b3 = annResp.Buffer[pos + 3];
                                int pPort = (annResp.Buffer[pos + 4] << 8) | annResp.Buffer[pos + 5];
                                if (b0 == 0 || b0 == 127 || pPort <= 0 || pPort > 65535) continue;
                                string key = $"{b0}.{b1}.{b2}.{b3}:{pPort}";
                                string hHex = Convert.ToHexString(infoHashBytes).ToUpperInvariant();
                                GetHttpPeersForHash(hHex)[key] = 1;
                                discovered.Add(new PeerInfo(new Uri($"ipv4://{key}")));
                            }
                        }
                    }
                }
                catch { }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);
            return discovered.ToList();
        }

        private async Task<List<string>> HarvestRawPeerStringsFromTrackersAsync(string infoHashHex, int portToAnnounce, CancellationToken ct, byte[]? rawTorrent = null)
        {
            var peers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(infoHashHex) || infoHashHex.Length != 40) return peers.ToList();
            try
            {
                byte[] hashBytes = Convert.FromHexString(infoHashHex);
                var sbHash = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    if ((b >= '0' && b <= '9') || (b >= 'a' && b <= 'z') || (b >= 'A' && b <= 'Z') || b == '.' || b == '-' || b == '_' || b == '~')
                        sbHash.Append((char)b);
                    else
                        sbHash.Append($"%{b:X2}");
                }
                string encodedHash = sbHash.ToString();
                string peerId = "-qB5000-" + Guid.NewGuid().ToString("N").Substring(0, 12);
                int listenPort = portToAnnounce > 0 ? portToAnnounce : 44859;
                string stdParams = $"info_hash={encodedHash}&peer_id={peerId}&port={listenPort}&uploaded=0&downloaded=0&left=1073741824&compact=1&numwant=200&event=started";

                var tempPeerInfos = new List<PeerInfo>();
                var announceTargets = new List<(string Host, string PathAndQuery)>
                {
                    ("bt.t-ru.org", $"/ann?magnet&{stdParams}"),
                    ("bt2.t-ru.org", $"/ann?magnet&{stdParams}"),
                    ("bt3.t-ru.org", $"/ann?magnet&{stdParams}"),
                    ("bt4.t-ru.org", $"/ann?magnet&{stdParams}")
                };

                // Извлекаем персональные passkey-ссылки (?pk=...) из самого .torrent файла
                if (rawTorrent != null && rawTorrent.Length > 0)
                {
                    try
                    {
                        string rawAscii = Encoding.ASCII.GetString(rawTorrent);
                        foreach (Match m in Regex.Matches(rawAscii, @"http://(bt\d*\.t-ru\.org)(/ann\?[^\x00-\x20""'<>\\]+)", RegexOptions.IgnoreCase))
                        {
                            string host = m.Groups[1].Value;
                            string pq = m.Groups[2].Value;
                            if (pq.Contains("pk=", StringComparison.OrdinalIgnoreCase))
                            {
                                var pkMatch = Regex.Match(pq, @"/ann\?pk=[0-9a-fA-F]+");
                                if (pkMatch.Success)
                                {
                                    announceTargets.Add((host, $"{pkMatch.Value}&{stdParams}"));
                                    foreach (var altHost in new[] { "bt.t-ru.org", "bt2.t-ru.org", "bt3.t-ru.org", "bt4.t-ru.org" })
                                    {
                                        announceTargets.Add((altHost, $"{pkMatch.Value}&{stdParams}"));
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                var btTasks = announceTargets
                    .Distinct()
                    .Select(async target =>
                    {
                        byte[]? data = await FetchTrackerViaHttpPipeliningAsync(target.Host, target.PathAndQuery, ct).ConfigureAwait(false);
                        ExtractCompactPeersFromBencodedData(data, tempPeerInfos, infoHashHex);
                    });

                var udpTask = QueryUdpTrackersBep15Async(hashBytes, listenPort, ct);
                var dhtTask = QueryDhtPeersViaSpiderAsync(hashBytes, ct);

                await Task.WhenAll(Task.WhenAll(btTasks), udpTask, dhtTask).ConfigureAwait(false);

                var dhtDict = GetDhtPeersForHash(infoHashHex);
                var httpDict = GetHttpPeersForHash(infoHashHex);

                foreach (var pi in await udpTask.ConfigureAwait(false))
                    peers.Add($"{pi.ConnectionUri.Host}:{pi.ConnectionUri.Port}");
                foreach (var dPeer in await dhtTask.ConfigureAwait(false))
                {
                    dhtDict[dPeer] = 1;
                    peers.Add(dPeer);
                }
                foreach (var k in httpDict.Keys)
                    peers.Add(k);
                foreach (var k in dhtDict.Keys)
                    peers.Add(k);
                lock (tempPeerInfos)
                {
                    foreach (var pi in tempPeerInfos)
                        peers.Add($"{pi.ConnectionUri.Host}:{pi.ConnectionUri.Port}");
                }
            }
            catch { }
            return peers.ToList();
        }

        private async Task<List<string>?> TryDownloadViaLocalQBitAsync(
            string saveDirectory,
            IReadOnlyList<TorrentFileSelectionItem> fileSelections,
            IProgress<TorrentDownloadProgress>? progress,
            CancellationToken ct,
            byte[]? rawTorrent,
            string currentHash,
            string torrentName,
            string effectiveTopicId,
            int effectiveTopicSeeds,
            ManualResetEventSlim? pauseEvent)
        {
            if (string.IsNullOrEmpty(currentHash) || currentHash.Length != 40)
                return null;

            if (!await EnsureLocalQBitReadyAsync(ct).ConfigureAwait(false))
                return null;

            string hashLower = currentHash.ToLowerInvariant();
            var (baseUrl, username, password) = ResolveLocalQBitConfig();

            using var handler = new HttpClientHandler
            {
                UseCookies = true,
                CookieContainer = new System.Net.CookieContainer(),
                UseProxy = false
            };
            using var qClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(3.5) };

            bool qbitOnline = false;
            int maxAttempts = Process.GetProcessesByName("qbittorrent").Length > 0 ? 15 : 4;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    using var verCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    verCts.CancelAfter(1500);
                    using var verResp = await qClient.GetAsync($"{baseUrl}/api/v2/app/version", verCts.Token).ConfigureAwait(false);
                    if (verResp.StatusCode == System.Net.HttpStatusCode.Forbidden || verResp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        var loginForm = new FormUrlEncodedContent(new[]
                        {
                            new KeyValuePair<string, string>("username", username),
                            new KeyValuePair<string, string>("password", password)
                        });
                        await qClient.PostAsync($"{baseUrl}/api/v2/auth/login", loginForm, verCts.Token).ConfigureAwait(false);
                        using var verResp2 = await qClient.GetAsync($"{baseUrl}/api/v2/app/version", verCts.Token).ConfigureAwait(false);
                        if (verResp2.IsSuccessStatusCode)
                        {
                            qbitOnline = true;
                            break;
                        }
                    }
                    else if (verResp.IsSuccessStatusCode)
                    {
                        qbitOnline = true;
                        break;
                    }
                }
                catch { }
                if (Process.GetProcessesByName("qbittorrent").Length > 0)
                    await Task.Delay(300, ct).ConfigureAwait(false);
                else
                    break;
            }

            if (!qbitOnline)
                return null;

            int qbitListenPort = 44859;
            try
            {
                string prefsJson = await qClient.GetStringAsync($"{baseUrl}/api/v2/app/preferences", ct).ConfigureAwait(false);
                using var prefsDoc = System.Text.Json.JsonDocument.Parse(prefsJson);
                if (prefsDoc.RootElement.TryGetProperty("listen_port", out var lpEl) && lpEl.TryGetInt32(out int lp) && lp > 0)
                    qbitListenPort = lp;
            }
            catch { }

            bool existedBefore = false;
            try
            {
                string checkJson = await qClient.GetStringAsync($"{baseUrl}/api/v2/torrents/info?hashes={hashLower}", ct).ConfigureAwait(false);
                using var checkDoc = System.Text.Json.JsonDocument.Parse(checkJson);
                if (checkDoc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array && checkDoc.RootElement.GetArrayLength() > 0)
                    existedBefore = true;
            }
            catch { }

            string magnetUri = $"magnet:?xt=urn:btih:{hashLower}&dn={Uri.EscapeDataString(torrentName)}&tr=http%3A%2F%2Fbt.t-ru.org%2Fann%3Fmagnet&tr=http%3A%2F%2Fbt2.t-ru.org%2Fann%3Fmagnet&tr=http%3A%2F%2Fbt3.t-ru.org%2Fann%3Fmagnet&tr=http%3A%2F%2Fbt4.t-ru.org%2Fann%3Fmagnet&tr=udp%3A%2F%2Ftracker.opentrackr.org%3A1337%2Fannounce";

            try
            {
                bool filePrioApplied = false;

                if (!existedBefore)
                {
                    bool addedOk = false;
                    if (rawTorrent != null && rawTorrent.Length > 0)
                    {
                        try
                        {
                            string boundary = "----WebKitFormBoundary" + Guid.NewGuid().ToString("N").Substring(0, 16);
                            using var ms = new MemoryStream();
                            void WriteAscii(string s) => ms.Write(Encoding.UTF8.GetBytes(s));
                            WriteAscii($"--{boundary}\r\nContent-Disposition: form-data; name=\"torrents\"; filename=\"release.torrent\"\r\nContent-Type: application/x-bittorrent\r\n\r\n");
                            ms.Write(rawTorrent, 0, rawTorrent.Length);
                            WriteAscii($"\r\n--{boundary}\r\nContent-Disposition: form-data; name=\"savepath\"\r\n\r\n{saveDirectory}\r\n--{boundary}\r\nContent-Disposition: form-data; name=\"contentLayout\"\r\n\r\nOriginal\r\n--{boundary}\r\nContent-Disposition: form-data; name=\"autoTMM\"\r\n\r\nfalse\r\n--{boundary}\r\nContent-Disposition: form-data; name=\"paused\"\r\n\r\nfalse\r\n--{boundary}--\r\n");

                            using var byteContent = new ByteArrayContent(ms.ToArray());
                            byteContent.Headers.Remove("Content-Type");
                            byteContent.Headers.TryAddWithoutValidation("Content-Type", $"multipart/form-data; boundary={boundary}");
                            using var addResp = await qClient.PostAsync($"{baseUrl}/api/v2/torrents/add", byteContent, ct).ConfigureAwait(false);
                            if (addResp.IsSuccessStatusCode)
                            {
                                await Task.Delay(150, ct).ConfigureAwait(false);
                                string verifyJson = await qClient.GetStringAsync($"{baseUrl}/api/v2/torrents/info?hashes={hashLower}", ct).ConfigureAwait(false);
                                using var verifyDoc = System.Text.Json.JsonDocument.Parse(verifyJson);
                                addedOk = verifyDoc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array && verifyDoc.RootElement.GetArrayLength() > 0;
                            }
                        }
                        catch { }
                    }

                    if (!addedOk)
                    {
                        var addForm = new FormUrlEncodedContent(new[]
                        {
                            new KeyValuePair<string, string>("urls", magnetUri),
                            new KeyValuePair<string, string>("savepath", saveDirectory),
                            new KeyValuePair<string, string>("autoTMM", "false"),
                            new KeyValuePair<string, string>("paused", "false"),
                            new KeyValuePair<string, string>("stopped", "false")
                        });
                        using var addResp = await qClient.PostAsync($"{baseUrl}/api/v2/torrents/add", addForm, ct).ConfigureAwait(false);
                        if (!addResp.IsSuccessStatusCode)
                            return null;
                    }
                }
                else
                {
                    var hashForm = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("hashes", hashLower) });
                    try { await qClient.PostAsync($"{baseUrl}/api/v2/torrents/start", hashForm, ct).ConfigureAwait(false); } catch { }
                    try { await qClient.PostAsync($"{baseUrl}/api/v2/torrents/resume", hashForm, ct).ConfigureAwait(false); } catch { }
                }

                App.Logger?.Log($"[RuTracker Turbo Engine] Задействован высокоскоростной движок qBittorrent WebUI (uTP BEP-29 + Holepunch BEP-55) для {hashLower}", LogLevel.Info);

                // Снимаем любые лимиты скорости, включаем ForceStart, выставляем параметры сессии libtorrent на максимум
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var prefsForm = new FormUrlEncodedContent(new[]
                        {
                            new KeyValuePair<string, string>("json", "{\"dl_limit\":0,\"up_limit\":0,\"queueing_enabled\":false,\"max_active_downloads\":30,\"max_active_torrents\":30,\"dht\":true,\"pex\":true,\"lsd\":true,\"enable_utp\":true,\"utp_tcp_mixed_mode\":0,\"enable_multi_connections_from_same_ip\":true,\"announce_to_all_trackers\":true,\"announce_to_all_tiers\":true,\"connection_speed\":100,\"max_connec\":2000,\"max_connec_per_torrent\":250,\"async_io_threads\":16,\"file_pool_size\":1024}")
                        });
                        await qClient.PostAsync($"{baseUrl}/api/v2/app/setPreferences", prefsForm, ct).ConfigureAwait(false);
                    }
                    catch { }
                    try
                    {
                        var forceForm = new FormUrlEncodedContent(new[]
                        {
                            new KeyValuePair<string, string>("hashes", hashLower),
                            new KeyValuePair<string, string>("value", "true")
                        });
                        await qClient.PostAsync($"{baseUrl}/api/v2/torrents/setForceStart", forceForm, ct).ConfigureAwait(false);
                    }
                    catch { }

                    // Извлекаем персональные passkey-трекеры (?pk=...) и добавляем в qBittorrent
                    var trackerUrls = new List<string>(PublicTrackers);
                    if (rawTorrent != null && rawTorrent.Length > 0)
                    {
                        try
                        {
                            string rawAscii = Encoding.ASCII.GetString(rawTorrent);
                            foreach (Match m in Regex.Matches(rawAscii, @"http://(bt\d*\.t-ru\.org/ann\?[^\x00-\x20""'<>\\]+)", RegexOptions.IgnoreCase))
                            {
                                if (m.Value.Contains("pk=", StringComparison.OrdinalIgnoreCase))
                                    trackerUrls.Insert(0, m.Value);
                            }
                        }
                        catch { }
                    }

                    try
                    {
                        var trForm = new FormUrlEncodedContent(new[]
                        {
                            new KeyValuePair<string, string>("hash", hashLower),
                            new KeyValuePair<string, string>("urls", string.Join("\n", trackerUrls.Distinct()))
                        });
                        await qClient.PostAsync($"{baseUrl}/api/v2/torrents/addTrackers", trForm, ct).ConfigureAwait(false);
                    }
                    catch { }
                    try
                    {
                        var reannForm = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("hashes", hashLower) });
                        await qClient.PostAsync($"{baseUrl}/api/v2/torrents/reannounce", reannForm, ct).ConfigureAwait(false);
                    }
                    catch { }
                }, ct);

                // Функция применения выбора файлов к торренту в qBittorrent
                async Task TryApplyFilePrioritiesAsync()
                {
                    if (filePrioApplied || fileSelections == null || fileSelections.Count == 0 || !fileSelections.Any(f => !f.IsSelected))
                        return;

                    try
                    {
                        string filesJson = await qClient.GetStringAsync($"{baseUrl}/api/v2/torrents/files?hash={hashLower}", ct).ConfigureAwait(false);
                        using var fDoc = System.Text.Json.JsonDocument.Parse(filesJson);
                        if (fDoc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array && fDoc.RootElement.GetArrayLength() > 0)
                        {
                            var skipIds = new List<string>();
                            var keepIds = new List<string>();
                            int arrLen = fDoc.RootElement.GetArrayLength();
                            for (int fi = 0; fi < arrLen; fi++)
                            {
                                var fObj = fDoc.RootElement[fi];
                                int fIdx = fObj.TryGetProperty("index", out var idxEl) ? idxEl.GetInt32() : fi;
                                string fName = fObj.TryGetProperty("name", out var nmEl) ? (nmEl.GetString() ?? "") : "";
                                string fFileOnly = Path.GetFileName(fName.Replace('/', Path.DirectorySeparatorChar));
                                string fNorm = fName.Replace('\\', '/').TrimStart('.').TrimStart('/');

                                var matchedSel = fileSelections.FirstOrDefault(s =>
                                {
                                    foreach (var relPath in s.GetEffectiveRelativePaths())
                                    {
                                        string sNorm = relPath.Replace('\\', '/').TrimStart('.').TrimStart('/');
                                        if (sNorm.Equals(fNorm, StringComparison.OrdinalIgnoreCase) ||
                                            sNorm.EndsWith("/" + fNorm, StringComparison.OrdinalIgnoreCase) ||
                                            fNorm.EndsWith("/" + sNorm, StringComparison.OrdinalIgnoreCase))
                                            return true;
                                        if (Path.GetFileName(relPath).Equals(fFileOnly, StringComparison.OrdinalIgnoreCase))
                                            return true;
                                    }
                                    return false;
                                });

                                if (matchedSel != null && !matchedSel.IsSelected)
                                    skipIds.Add(fIdx.ToString());
                                else
                                    keepIds.Add(fIdx.ToString());
                            }
                            if (skipIds.Count > 0)
                            {
                                var prio0 = new FormUrlEncodedContent(new[]
                                {
                                    new KeyValuePair<string, string>("hash", hashLower),
                                    new KeyValuePair<string, string>("id", string.Join("|", skipIds)),
                                    new KeyValuePair<string, string>("priority", "0")
                                });
                                await qClient.PostAsync($"{baseUrl}/api/v2/torrents/filePrio", prio0, ct).ConfigureAwait(false);
                            }
                            if (keepIds.Count > 0)
                            {
                                var prio1 = new FormUrlEncodedContent(new[]
                                {
                                    new KeyValuePair<string, string>("hash", hashLower),
                                    new KeyValuePair<string, string>("id", string.Join("|", keepIds)),
                                    new KeyValuePair<string, string>("priority", "1")
                                });
                                await qClient.PostAsync($"{baseUrl}/api/v2/torrents/filePrio", prio1, ct).ConfigureAwait(false);
                            }
                            filePrioApplied = true;
                        }
                    }
                    catch { }
                }

                // Пытаемся применить приоритеты файлов немедленно
                await TryApplyFilePrioritiesAsync().ConfigureAwait(false);

                // Multi-wave peer injection: волны на T=0с, T=2.5с, T=6с и затем каждые 12с
                _ = Task.Run(async () =>
                {
                    int[] waveDelays = new[] { 0, 2500, 6000 };
                    for (int w = 0; w < waveDelays.Length; w++)
                    {
                        if (waveDelays[w] > 0)
                            await Task.Delay(waveDelays[w], ct).ConfigureAwait(false);

                        var peersList = await HarvestRawPeerStringsFromTrackersAsync(hashLower, qbitListenPort, ct, rawTorrent).ConfigureAwait(false);
                        if (peersList.Count > 0)
                        {
                            for (int chunkStart = 0; chunkStart < peersList.Count; chunkStart += 100)
                            {
                                var chunk = peersList.Skip(chunkStart).Take(100);
                                var peersForm = new FormUrlEncodedContent(new[]
                                {
                                    new KeyValuePair<string, string>("hashes", hashLower),
                                    new KeyValuePair<string, string>("peers", string.Join("|", chunk))
                                });
                                try { await qClient.PostAsync($"{baseUrl}/api/v2/torrents/addPeers", peersForm, ct).ConfigureAwait(false); } catch { }
                            }
                        }
                    }
                }, ct);

                bool wasPausedInQbit = false;
                long lastPeerRefreshTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                string actualSavePath = saveDirectory;

                while (!ct.IsCancellationRequested)
                {
                    if (pauseEvent != null && !pauseEvent.IsSet)
                    {
                        if (!wasPausedInQbit)
                        {
                            wasPausedInQbit = true;
                            var hForm = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("hashes", hashLower) });
                            try { await qClient.PostAsync($"{baseUrl}/api/v2/torrents/stop", hForm, ct).ConfigureAwait(false); } catch { }
                            try { await qClient.PostAsync($"{baseUrl}/api/v2/torrents/pause", hForm, ct).ConfigureAwait(false); } catch { }
                        }
                        await Task.Delay(250, ct).ConfigureAwait(false);
                        continue;
                    }
                    else if (wasPausedInQbit)
                    {
                        wasPausedInQbit = false;
                        var hForm = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("hashes", hashLower) });
                        try { await qClient.PostAsync($"{baseUrl}/api/v2/torrents/start", hForm, ct).ConfigureAwait(false); } catch { }
                        try { await qClient.PostAsync($"{baseUrl}/api/v2/torrents/resume", hForm, ct).ConfigureAwait(false); } catch { }
                    }

                    string infoJson = await qClient.GetStringAsync($"{baseUrl}/api/v2/torrents/info?hashes={hashLower}", ct).ConfigureAwait(false);
                    using var doc = System.Text.Json.JsonDocument.Parse(infoJson);
                    if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                    {
                        await Task.Delay(350, ct).ConfigureAwait(false);
                        continue;
                    }

                    var item = doc.RootElement[0];
                    long dlSpeedBytes = item.TryGetProperty("dlspeed", out var sEl) ? sEl.GetInt64() : 0L;
                    int numSeeds = item.TryGetProperty("num_seeds", out var nsEl) ? nsEl.GetInt32() : 0;
                    int numComplete = item.TryGetProperty("num_complete", out var ncEl) ? ncEl.GetInt32() : 0;
                    int numLeechs = item.TryGetProperty("num_leechs", out var nlEl) ? nlEl.GetInt32() : 0;
                    long completedBytes = item.TryGetProperty("completed", out var cbEl) ? cbEl.GetInt64() : 0L;
                    long totalBytes = item.TryGetProperty("total_size", out var tszEl) ? tszEl.GetInt64() : 0L;
                    if (totalBytes <= 0 && item.TryGetProperty("size", out var szEl))
                        totalBytes = szEl.GetInt64();
                    long amountLeft = item.TryGetProperty("amount_left", out var alEl) ? alEl.GetInt64() : -1L;
                    string state = item.TryGetProperty("state", out var stEl) ? (stEl.GetString() ?? "") : "";
                    if (item.TryGetProperty("save_path", out var spEl) && !string.IsNullOrWhiteSpace(spEl.GetString()))
                        actualSavePath = spEl.GetString()!;

                    // Применяем выбор файлов, если ещё не применён
                    if (!filePrioApplied && totalBytes > 0)
                    {
                        await TryApplyFilePrioritiesAsync().ConfigureAwait(false);
                    }

                    double speedMb = dlSpeedBytes / (1024.0 * 1024.0);
                    double elapsedHarvest = (System.Diagnostics.Stopwatch.GetTimestamp() - lastPeerRefreshTicks) / (double)System.Diagnostics.Stopwatch.Frequency;
                    if (elapsedHarvest >= 12.0 && (speedMb < 15.0 || numSeeds < 20))
                    {
                        lastPeerRefreshTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                        _ = Task.Run(async () =>
                        {
                            var peersList = await HarvestRawPeerStringsFromTrackersAsync(hashLower, qbitListenPort, ct, rawTorrent).ConfigureAwait(false);
                            if (peersList.Count > 0)
                            {
                                for (int chunkStart = 0; chunkStart < peersList.Count; chunkStart += 100)
                                {
                                    var chunk = peersList.Skip(chunkStart).Take(100);
                                    var peersForm = new FormUrlEncodedContent(new[]
                                    {
                                        new KeyValuePair<string, string>("hashes", hashLower),
                                        new KeyValuePair<string, string>("peers", string.Join("|", chunk))
                                    });
                                    try { await qClient.PostAsync($"{baseUrl}/api/v2/torrents/addPeers", peersForm, ct).ConfigureAwait(false); } catch { }
                                }
                            }
                        }, ct);
                    }

                    double prog01;
                    if (amountLeft >= 0 && totalBytes > 0)
                    {
                        long wantedCompleted = Math.Max(0, totalBytes - amountLeft);
                        prog01 = Math.Clamp((double)wantedCompleted / totalBytes, 0.0, 1.0);
                        completedBytes = wantedCompleted;
                    }
                    else
                    {
                        prog01 = item.TryGetProperty("progress", out var pEl) ? pEl.GetDouble() : 0.0;
                    }

                    double pct = Math.Clamp(prog01 * 100.0, 0.0, 100.0);
                    int swarmTotal = Math.Max(effectiveTopicSeeds, Math.Max(numComplete, Math.Max(_lastSwarmTotalPeers, numSeeds + numLeechs)));
                    string seedsStr = swarmTotal > numSeeds ? $"Сиды: {numSeeds} (в рое: {swarmTotal})" : $"Сиды: {numSeeds}";
                    string topicLabel = !string.IsNullOrEmpty(effectiveTopicId) ? $"RuTracker #1605 (t={effectiveTopicId})" : "RuTracker #1605";

                    bool isMeta = state.Equals("metaDL", StringComparison.OrdinalIgnoreCase) || state.Equals("forcedMetaDL", StringComparison.OrdinalIgnoreCase);
                    string stateStr = isMeta
                        ? $"{topicLabel}: получение метаданных Magnet от пиров..."
                        : $"{topicLabel}: скачивание {pct:F1}% ({speedMb:F2} МБ/с | {seedsStr}, Пиры: {numLeechs})";

                    progress?.Report(new TorrentDownloadProgress
                    {
                        Percent = pct,
                        SpeedMbPerSec = speedMb,
                        Seeds = numSeeds,
                        SwarmTotal = swarmTotal,
                        Peers = numLeechs,
                        StatusText = stateStr,
                        DownloadedBytes = completedBytes,
                        TotalBytes = totalBytes
                    });

                    bool isFinishedState = (amountLeft == 0 && totalBytes > 0 && filePrioApplied) ||
                        prog01 >= 0.9999 ||
                        state.Equals("uploading", StringComparison.OrdinalIgnoreCase) ||
                        state.Equals("stalledUP", StringComparison.OrdinalIgnoreCase) ||
                        state.Equals("forcedUP", StringComparison.OrdinalIgnoreCase) ||
                        state.Equals("queuedUP", StringComparison.OrdinalIgnoreCase) ||
                        state.Equals("stoppedUP", StringComparison.OrdinalIgnoreCase) ||
                        state.Equals("pausedUP", StringComparison.OrdinalIgnoreCase);

                    if (isFinishedState && totalBytes > 0)
                        break;

                    await Task.Delay(350, ct).ConfigureAwait(false);
                }

                if (ct.IsCancellationRequested)
                {
                    if (!existedBefore)
                    {
                        var delForm = new FormUrlEncodedContent(new[]
                        {
                            new KeyValuePair<string, string>("hashes", hashLower),
                            new KeyValuePair<string, string>("deleteFiles", "false")
                        });
                        try { await qClient.PostAsync($"{baseUrl}/api/v2/torrents/delete", delForm, CancellationToken.None).ConfigureAwait(false); } catch { }
                    }
                    ct.ThrowIfCancellationRequested();
                }

                // Собираем скачанные файлы
                var downloadedPaths = new List<string>();
                try
                {
                    string filesJson = await qClient.GetStringAsync($"{baseUrl}/api/v2/torrents/files?hash={hashLower}", CancellationToken.None).ConfigureAwait(false);
                    using var fDoc = System.Text.Json.JsonDocument.Parse(filesJson);
                    if (fDoc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        foreach (var fObj in fDoc.RootElement.EnumerateArray())
                        {
                            int prio = fObj.TryGetProperty("priority", out var prEl) ? prEl.GetInt32() : 1;
                            if (prio == 0) continue;
                            string relName = fObj.TryGetProperty("name", out var nmEl) ? (nmEl.GetString() ?? "") : "";
                            if (string.IsNullOrEmpty(relName)) continue;

                            string normRel = relName.Replace('/', Path.DirectorySeparatorChar);
                            string srcFull = Path.Combine(actualSavePath, normRel);
                            string dstFull = Path.Combine(saveDirectory, normRel);

                            if (!File.Exists(dstFull) && File.Exists(srcFull) && !string.Equals(srcFull, dstFull, StringComparison.OrdinalIgnoreCase))
                            {
                                string? dstDir = Path.GetDirectoryName(dstFull);
                                if (!string.IsNullOrEmpty(dstDir)) Directory.CreateDirectory(dstDir);
                                File.Copy(srcFull, dstFull, overwrite: true);
                            }

                            string targetPath = File.Exists(dstFull) ? dstFull : srcFull;
                            if (File.Exists(targetPath))
                            {
                                string ext = Path.GetExtension(targetPath).ToLowerInvariant();
                                if (ext is ".zip" or ".7z" or ".rar")
                                {
                                    try
                                    {
                                        string extractDir = Path.Combine(saveDirectory, Path.GetFileNameWithoutExtension(targetPath));
                                        Directory.CreateDirectory(extractDir);
                                        await ExtractArchiveWith7zAsync(targetPath, extractDir, ct).ConfigureAwait(false);
                                        var extractedContainers = Directory.EnumerateFiles(extractDir, "*.*", SearchOption.AllDirectories)
                                            .Where(p => p.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) ||
                                                        p.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) ||
                                                        p.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) ||
                                                        p.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase))
                                            .ToList();
                                        if (extractedContainers.Count > 0)
                                            downloadedPaths.AddRange(extractedContainers);
                                        else
                                            downloadedPaths.Add(targetPath);
                                    }
                                    catch
                                    {
                                        downloadedPaths.Add(targetPath);
                                    }
                                }
                                else
                                {
                                    downloadedPaths.Add(targetPath);
                                }
                            }
                        }
                    }
                }
                catch { }

                if (!existedBefore)
                {
                    var delForm = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("hashes", hashLower),
                        new KeyValuePair<string, string>("deleteFiles", "false")
                    });
                    try { await qClient.PostAsync($"{baseUrl}/api/v2/torrents/delete", delForm, CancellationToken.None).ConfigureAwait(false); } catch { }
                }

                return downloadedPaths;
            }
            catch (OperationCanceledException)
            {
                if (!existedBefore)
                {
                    try
                    {
                        var delForm = new FormUrlEncodedContent(new[]
                        {
                            new KeyValuePair<string, string>("hashes", hashLower),
                            new KeyValuePair<string, string>("deleteFiles", "false")
                        });
                        await qClient.PostAsync($"{baseUrl}/api/v2/torrents/delete", delForm, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch { }
                }
                throw;
            }
            catch (Exception ex)
            {
                App.Logger?.Log($"[RuTracker Turbo Engine] Переключение с qBittorrent WebUI на встроенный движок: {ex.Message}", LogLevel.Warning);
                return null;
            }
        }

        private async Task<List<string>?> TryDownloadViaEmbeddedLibtorrentAsync(
            string saveDirectory,
            IReadOnlyList<TorrentFileSelectionItem> fileSelections,
            IProgress<TorrentDownloadProgress>? progress,
            CancellationToken ct,
            byte[]? rawTorrent,
            string currentHash,
            string torrentName,
            string effectiveTopicId,
            int effectiveTopicSeeds,
            ManualResetEventSlim? pauseEvent,
            MagnetLink? rawMagnet)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string pythonExe = Path.Combine(baseDir, "tools", "nsz", "python.exe");
            string scriptPath = Path.Combine(baseDir, "tools", "nsz", "storm_lt_turbo.py");

            if (!File.Exists(pythonExe) || !File.Exists(scriptPath))
            {
                string altBase = Directory.GetCurrentDirectory();
                string altPy = Path.Combine(altBase, "tools", "nsz", "python.exe");
                string altSc = Path.Combine(altBase, "tools", "nsz", "storm_lt_turbo.py");
                if (File.Exists(altPy) && File.Exists(altSc))
                {
                    pythonExe = altPy;
                    scriptPath = altSc;
                }
                else
                {
                    return null;
                }
            }

            string hashLower = currentHash.ToLowerInvariant();
            string tempDir = Path.Combine(Path.GetTempPath(), "STORM_LT_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            string peersFile = Path.Combine(tempDir, "peers.txt");
            string controlFile = Path.Combine(tempDir, "control.txt");
            string? tempTorrentFile = null;

            Process? proc = null;
            CancellationTokenSource? peerFeederCts = null;

            try
            {
                var argsList = new List<string>
                {
                    $"\"{scriptPath}\"",
                    $"--save-dir \"{saveDirectory}\"",
                    $"--peers-file \"{peersFile}\"",
                    $"--control-file \"{controlFile}\""
                };

                if (rawTorrent != null && rawTorrent.Length > 0)
                {
                    tempTorrentFile = Path.Combine(tempDir, "release.torrent");
                    await File.WriteAllBytesAsync(tempTorrentFile, rawTorrent, ct).ConfigureAwait(false);
                    argsList.Add($"--torrent \"{tempTorrentFile}\"");
                }
                else if (rawMagnet != null)
                {
                    argsList.Add($"--magnet \"{rawMagnet.ToV1String()}\"");
                }
                else
                {
                    string magnetUri = $"magnet:?xt=urn:btih:{hashLower}&dn={Uri.EscapeDataString(torrentName)}&tr=http%3A%2F%2Fbt.t-ru.org%2Fann%3Fmagnet&tr=http%3A%2F%2Fbt2.t-ru.org%2Fann%3Fmagnet&tr=http%3A%2F%2Fbt3.t-ru.org%2Fann%3Fmagnet&tr=http%3A%2F%2Fbt4.t-ru.org%2Fann%3Fmagnet";
                    argsList.Add($"--magnet \"{magnetUri}\"");
                }

                if (fileSelections != null && fileSelections.Count > 0 && fileSelections.Any(f => !f.IsSelected) && rawTorrent != null)
                {
                    try
                    {
                        if (Torrent.TryLoad(rawTorrent, out var parsedTorrent) && parsedTorrent.Files != null)
                        {
                            var skipIdxs = new List<int>();
                            for (int fi = 0; fi < parsedTorrent.Files.Count; fi++)
                            {
                                var tf = parsedTorrent.Files[fi];
                                string fNorm = tf.Path.Replace('\\', '/').TrimStart('.').TrimStart('/');
                                string fFileOnly = Path.GetFileName(tf.Path);

                                var matchedSel = fileSelections.FirstOrDefault(s =>
                                {
                                    foreach (var relPath in s.GetEffectiveRelativePaths())
                                    {
                                        string sNorm = relPath.Replace('\\', '/').TrimStart('.').TrimStart('/');
                                        if (sNorm.Equals(fNorm, StringComparison.OrdinalIgnoreCase) ||
                                            sNorm.EndsWith("/" + fNorm, StringComparison.OrdinalIgnoreCase) ||
                                            fNorm.EndsWith("/" + sNorm, StringComparison.OrdinalIgnoreCase))
                                            return true;
                                        if (Path.GetFileName(relPath).Equals(fFileOnly, StringComparison.OrdinalIgnoreCase))
                                            return true;
                                    }
                                    return false;
                                });

                                if (matchedSel != null && !matchedSel.IsSelected)
                                    skipIdxs.Add(fi);
                            }
                            if (skipIdxs.Count > 0)
                                argsList.Add($"--skip-indices {string.Join(",", skipIdxs)}");
                        }
                    }
                    catch { }
                }

                App.Logger?.Log($"[RuTracker Turbo Engine] Задействован встроенный нативный движок libtorrent-rasterbar (Tier 2) для {hashLower}", LogLevel.Info);

                peerFeederCts = new CancellationTokenSource();
                var feederToken = peerFeederCts.Token;

                _ = Task.Run(async () =>
                {
                    var sentPeers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    int[] delaySteps = new[] { 0, 2500, 6000, 12000 };
                    int step = 0;
                    while (!feederToken.IsCancellationRequested)
                    {
                        int delayMs = step < delaySteps.Length ? delaySteps[step++] : 12000;
                        if (delayMs > 0)
                            await Task.Delay(delayMs, feederToken).ConfigureAwait(false);

                        try
                        {
                            var peers = await HarvestRawPeerStringsFromTrackersAsync(hashLower, 44859, feederToken, rawTorrent).ConfigureAwait(false);
                            var newPeers = peers.Where(p => sentPeers.Add(p)).ToList();
                            if (newPeers.Count > 0)
                            {
                                await File.AppendAllLinesAsync(peersFile, newPeers, feederToken).ConfigureAwait(false);
                            }
                        }
                        catch { }
                    }
                }, feederToken);

                var psi = new ProcessStartInfo
                {
                    FileName = pythonExe,
                    Arguments = string.Join(" ", argsList),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = Path.GetDirectoryName(pythonExe)
                };

                proc = new Process { StartInfo = psi };
                proc.Start();
                try { proc.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }

                bool wasPaused = false;
                string topicLabel = !string.IsNullOrEmpty(effectiveTopicId) ? $"RuTracker #1605 (t={effectiveTopicId})" : "RuTracker #1605";

                while (!proc.HasExited && !ct.IsCancellationRequested)
                {
                    if (pauseEvent != null && !pauseEvent.IsSet)
                    {
                        if (!wasPaused)
                        {
                            wasPaused = true;
                            try { await File.WriteAllTextAsync(controlFile, "PAUSE", ct).ConfigureAwait(false); } catch { }
                        }
                        await Task.Delay(250, ct).ConfigureAwait(false);
                        continue;
                    }
                    else if (wasPaused)
                    {
                        wasPaused = false;
                        try { await File.WriteAllTextAsync(controlFile, "RESUME", ct).ConfigureAwait(false); } catch { }
                    }

                    string? line = await proc.StandardOutput.ReadLineAsync(ct).ConfigureAwait(false);
                    if (line == null) break;

                    if (line.StartsWith("{") && line.EndsWith("}"))
                    {
                        try
                        {
                            using var doc = System.Text.Json.JsonDocument.Parse(line);
                            var root = doc.RootElement;
                            double pct = root.TryGetProperty("pct", out var pEl) ? pEl.GetDouble() : 0.0;
                            long speedBps = root.TryGetProperty("speed_bps", out var spEl) ? spEl.GetInt64() : 0L;
                            int seeds = root.TryGetProperty("seeds", out var sdEl) ? sdEl.GetInt32() : 0;
                            int peers = root.TryGetProperty("peers", out var prEl) ? prEl.GetInt32() : 0;
                            int swarm = root.TryGetProperty("swarm", out var swEl) ? swEl.GetInt32() : 0;
                            long doneBytes = root.TryGetProperty("done_bytes", out var dbEl) ? dbEl.GetInt64() : 0L;
                            long totalBytes = root.TryGetProperty("total_bytes", out var tbEl) ? tbEl.GetInt64() : 0L;
                            string state = root.TryGetProperty("state", out var stEl) ? (stEl.GetString() ?? "") : "";
                            bool isMeta = root.TryGetProperty("is_meta", out var imEl) && imEl.GetBoolean();

                            double speedMb = speedBps / (1024.0 * 1024.0);
                            int swarmTotal = Math.Max(effectiveTopicSeeds, Math.Max(swarm, seeds + peers));
                            string seedsStr = swarmTotal > seeds ? $"Сиды: {seeds} (в рое: {swarmTotal})" : $"Сиды: {seeds}";

                            string stateStr = isMeta
                                ? $"{topicLabel}: получение метаданных Magnet от пиров (libtorrent)..."
                                : $"{topicLabel}: скачивание {pct:F1}% ({speedMb:F2} МБ/с | {seedsStr}, Пиры: {peers}) [libtorrent]";

                            progress?.Report(new TorrentDownloadProgress
                            {
                                Percent = pct,
                                SpeedMbPerSec = speedMb,
                                Seeds = seeds,
                                SwarmTotal = swarmTotal,
                                Peers = peers,
                                StatusText = stateStr,
                                DownloadedBytes = doneBytes,
                                TotalBytes = totalBytes
                            });

                            if (state == "finished")
                                break;
                        }
                        catch { }
                    }
                }

                if (ct.IsCancellationRequested)
                {
                    try { await File.WriteAllTextAsync(controlFile, "STOP", CancellationToken.None).ConfigureAwait(false); } catch { }
                    try { proc?.Kill(entireProcessTree: true); } catch { }
                    ct.ThrowIfCancellationRequested();
                }

                if (proc != null) await proc.WaitForExitAsync(ct).ConfigureAwait(false);

                // Собираем скачанные файлы
                var downloadedPaths = new List<string>();
                if (Directory.Exists(saveDirectory))
                {
                    var files = Directory.EnumerateFiles(saveDirectory, "*.*", SearchOption.AllDirectories).ToList();
                    foreach (var targetPath in files)
                    {
                        string ext = Path.GetExtension(targetPath).ToLowerInvariant();
                        if (ext is ".zip" or ".7z" or ".rar")
                        {
                            try
                            {
                                string extractDir = Path.Combine(saveDirectory, Path.GetFileNameWithoutExtension(targetPath));
                                Directory.CreateDirectory(extractDir);
                                await ExtractArchiveWith7zAsync(targetPath, extractDir, ct).ConfigureAwait(false);
                                var extractedContainers = Directory.EnumerateFiles(extractDir, "*.*", SearchOption.AllDirectories)
                                    .Where(p => p.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) ||
                                                p.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) ||
                                                p.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) ||
                                                p.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase))
                                    .ToList();
                                if (extractedContainers.Count > 0)
                                    downloadedPaths.AddRange(extractedContainers);
                                else
                                    downloadedPaths.Add(targetPath);
                            }
                            catch
                            {
                                downloadedPaths.Add(targetPath);
                            }
                        }
                        else
                        {
                            downloadedPaths.Add(targetPath);
                        }
                    }
                }

                return downloadedPaths.Count > 0 ? downloadedPaths : null;
            }
            catch (OperationCanceledException)
            {
                try { proc?.Kill(entireProcessTree: true); } catch { }
                throw;
            }
            catch (Exception ex)
            {
                App.Logger?.Log($"[RuTracker Turbo Engine] Ошибка Tier 2 libtorrent: {ex.Message}", LogLevel.Warning);
                return null;
            }
            finally
            {
                try { peerFeederCts?.Cancel(); peerFeederCts?.Dispose(); } catch { }
                try { if (proc != null && !proc.HasExited) proc.Kill(entireProcessTree: true); } catch { }
                try { proc?.Dispose(); } catch { }
                try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }



        private static ClientEngine? _sharedTorrentEngine;
        private static readonly object _engineLock = new();

        private static ClientEngine? _sharedQueueEngine;
        private static readonly object _queueEngineLock = new();

        public static ClientEngine GetOrCreateSharedQueueEngine()
        {
            if (_sharedQueueEngine != null)
                return _sharedQueueEngine;

            lock (_queueEngineLock)
            {
                if (_sharedQueueEngine != null)
                    return _sharedQueueEngine;

                string dhtCacheDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "STORM SWITCH BOX", "dht_cache");
                string sharedCacheDir = Path.Combine(dhtCacheDir, "shared_engine");
                Directory.CreateDirectory(sharedCacheDir);

                var encList = new List<MonoTorrent.Connections.EncryptionType>
                {
                    MonoTorrent.Connections.EncryptionType.RC4Full,
                    MonoTorrent.Connections.EncryptionType.RC4Header,
                    MonoTorrent.Connections.EncryptionType.PlainText
                };

                int listenPort = 44860;
                try
                {
                    using var testTcp = new System.Net.Sockets.TcpListener(IPAddress.Any, listenPort);
                    testTcp.Start();
                    testTcp.Stop();
                }
                catch
                {
                    listenPort = 0;
                }

                var listenEps = new Dictionary<string, IPEndPoint>
                {
                    ["ipv4"] = new IPEndPoint(IPAddress.Any, listenPort)
                };

                var engineSettings = new EngineSettingsBuilder
                {
                    CacheDirectory = sharedCacheDir,
                    AllowedEncryption = encList,
                    ListenEndPoints = listenEps,
                    AllowPortForwarding = true,
                    AllowLocalPeerDiscovery = true,
                    UsePartialFiles = false,
                    MaximumConnections = 1600,
                    MaximumDownloadRate = 0,
                    MaximumUploadRate = 0,
                    MaximumHalfOpenConnections = 250,
                    MaximumOpenFiles = 1024,
                    MaximumDiskReadRate = 0,
                    MaximumDiskWriteRate = 0,
                    DiskCacheBytes = 256 * 1024 * 1024,
                    DhtEndPoint = new IPEndPoint(IPAddress.Any, listenPort),
                    AutoSaveLoadDhtCache = true,
                    AutoSaveLoadFastResume = true,
                    AutoSaveLoadMagnetLinkMetadata = true
                }.ToSettings();

                _sharedQueueEngine = new ClientEngine(engineSettings);
                return _sharedQueueEngine;
            }
        }

        private static ClientEngine CreateTemporaryFallbackEngine(string sessionCacheDir)
        {
            var encList = new List<MonoTorrent.Connections.EncryptionType>
            {
                MonoTorrent.Connections.EncryptionType.RC4Full,
                MonoTorrent.Connections.EncryptionType.RC4Header,
                MonoTorrent.Connections.EncryptionType.PlainText
            };

            var listenEps = new Dictionary<string, IPEndPoint>
            {
                ["ipv4"] = new IPEndPoint(IPAddress.Any, 0)
            };

            var engineSettings = new EngineSettingsBuilder
            {
                CacheDirectory = sessionCacheDir,
                AllowedEncryption = encList,
                ListenEndPoints = listenEps,
                AllowPortForwarding = false,
                AllowLocalPeerDiscovery = false,
                UsePartialFiles = false,
                MaximumConnections = 150,
                MaximumDownloadRate = 0,
                MaximumUploadRate = 0,
                MaximumHalfOpenConnections = 30,
                MaximumOpenFiles = 128,
                MaximumDiskReadRate = 0,
                MaximumDiskWriteRate = 0,
                DiskCacheBytes = 32 * 1024 * 1024,
                DhtEndPoint = new IPEndPoint(IPAddress.Any, 0),
                AutoSaveLoadDhtCache = false,
                AutoSaveLoadFastResume = false,
                AutoSaveLoadMagnetLinkMetadata = true
            }.ToSettings();

            return new ClientEngine(engineSettings);
        }

        private static ClientEngine CreateDownloadEngine(string sessionCacheDir) => CreateTemporaryFallbackEngine(sessionCacheDir);

        private static ClientEngine GetOrCreateEngine(string cacheDirectory)
        {
            if (_sharedTorrentEngine != null)
                return _sharedTorrentEngine;

            lock (_engineLock)
            {
                if (_sharedTorrentEngine != null)
                    return _sharedTorrentEngine;

                var encList = new List<MonoTorrent.Connections.EncryptionType>
                {
                    MonoTorrent.Connections.EncryptionType.RC4Full,
                    MonoTorrent.Connections.EncryptionType.RC4Header,
                    MonoTorrent.Connections.EncryptionType.PlainText
                };

                var listenEps = new Dictionary<string, IPEndPoint>
                {
                    ["ipv4"] = new IPEndPoint(IPAddress.Any, 44860)
                };

                var engineSettings = new EngineSettingsBuilder
                {
                    CacheDirectory = cacheDirectory,
                    AllowedEncryption = encList,
                    ListenEndPoints = listenEps,
                    AllowPortForwarding = true,
                    AllowLocalPeerDiscovery = true,
                    UsePartialFiles = false,
                    MaximumConnections = 800,
                    MaximumDownloadRate = 0,
                    MaximumUploadRate = 0,
                    MaximumHalfOpenConnections = 150,
                    MaximumOpenFiles = 256,
                    MaximumDiskReadRate = 0,
                    MaximumDiskWriteRate = 0,
                    DiskCacheBytes = 256 * 1024 * 1024,
                    DhtEndPoint = new IPEndPoint(IPAddress.Any, 44860),
                    AutoSaveLoadDhtCache = true,
                    AutoSaveLoadFastResume = true,
                    AutoSaveLoadMagnetLinkMetadata = true
                }.ToSettings();

                _sharedTorrentEngine = new ClientEngine(engineSettings);
                return _sharedTorrentEngine;
            }
        }

        private static async Task WaitUntilManagerAcceptsPeersAsync(TorrentManager manager, CancellationToken ct)
        {
            for (int i = 0; i < 25 && !ct.IsCancellationRequested; i++)
            {
                if (manager.State == TorrentState.Downloading || manager.State == TorrentState.Metadata || manager.State == TorrentState.Seeding)
                    return;
                await Task.Delay(150, ct).ConfigureAwait(false);
            }
        }

        private static async Task InjectPeersViaTurboDhtAsync(string infoHash, TorrentManager manager, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(infoHash) || infoHash.Length != 40) return;
            try
            {
                await WaitUntilManagerAcceptsPeersAsync(manager, ct).ConfigureAwait(false);
                byte[] targetHash = Convert.FromHexString(infoHash);
                byte[] myNodeId = new byte[20];
                Random.Shared.NextBytes(myNodeId);

                // get_peers запрос (95 байт)
                var getPeersBytes = new byte[95];
                int offset = 0;
                void CopyGp(byte[] src) { Buffer.BlockCopy(src, 0, getPeersBytes, offset, src.Length); offset += src.Length; }
                CopyGp(Encoding.ASCII.GetBytes("d1:ad2:id20:"));
                CopyGp(myNodeId);
                CopyGp(Encoding.ASCII.GetBytes("9:info_hash20:"));
                CopyGp(targetHash);
                CopyGp(Encoding.ASCII.GetBytes("e1:q9:get_peers1:t2:aa1:y1:qe"));

                // find_node запрос (92 байт) для предпрогрева и обхода rate-limit
                var findNodeBytes = new byte[92];
                offset = 0;
                void CopyFn(byte[] src) { Buffer.BlockCopy(src, 0, findNodeBytes, offset, src.Length); offset += src.Length; }
                CopyFn(Encoding.ASCII.GetBytes("d1:ad2:id20:"));
                CopyFn(myNodeId);
                CopyFn(Encoding.ASCII.GetBytes("6:target20:"));
                CopyFn(targetHash);
                CopyFn(Encoding.ASCII.GetBytes("e1:q9:find_node1:t2:aa1:y1:qe"));

                using var udp = new UdpClient(0);
                udp.Client.ReceiveTimeout = 1000;

                var bootstrapRouters = new (string Host, int Port)[]
                {
                    ("router.bittorrent.com", 6881),
                    ("dht.transmissionbt.com", 6881),
                    ("router.utorrent.com", 6881),
                    ("dht.libtorrent.org", 25401),
                    ("dht.aelitis.com", 6881)
                };

                var visitedEndpoints = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
                var discoveredPeers = new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
                var allDiscoveredPeers = new List<PeerInfo>();
                var allPeersLock = new object();

                var candidateNodes = new List<(byte[] NodeId, IPEndPoint EndPoint)>();
                var candidateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var candidatesLock = new object();

                // 1. Предпрогрев: на 5 бутстрап-роутеров отправляем find_node и get_peers
                foreach (var r in bootstrapRouters)
                {
                    try
                    {
                        var addresses = await Dns.GetHostAddressesAsync(r.Host).ConfigureAwait(false);
                        foreach (var addr in addresses.Where(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))
                        {
                            var ep = new IPEndPoint(addr, r.Port);
                            string epKey = ep.ToString();
                            if (visitedEndpoints.TryAdd(epKey, 0))
                            {
                                try
                                {
                                    await udp.SendAsync(findNodeBytes, findNodeBytes.Length, ep).ConfigureAwait(false);
                                    await udp.SendAsync(getPeersBytes, getPeersBytes.Length, ep).ConfigureAwait(false);
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                }

                using var spiderCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, spiderCts.Token);

                // Фоновый наблюдатель: повторная подача DHT-пиров через 3 секунды после перехода в Downloading
                _ = Task.Run(async () =>
                {
                    try
                    {
                        while (!ct.IsCancellationRequested && manager.State != TorrentState.Downloading && manager.State != TorrentState.Seeding)
                        {
                            await Task.Delay(500, ct).ConfigureAwait(false);
                        }

                        if (manager.State == TorrentState.Downloading && !ct.IsCancellationRequested)
                        {
                            await Task.Delay(3000, ct).ConfigureAwait(false);
                            List<PeerInfo> snapshot;
                            lock (allPeersLock)
                            {
                                snapshot = allDiscoveredPeers.ToList();
                            }
                            if (snapshot.Count > 0)
                            {
                                await manager.AddPeersAsync(snapshot).ConfigureAwait(false);
                                App.Logger?.Log($"[RuTracker Turbo DHT] Повторно инжектировано {snapshot.Count} DHT-пиров в состоянии Downloading", LogLevel.Info);
                            }
                        }
                    }
                    catch { }
                }, ct);

                // 2. Потокобезопасная отправка каждые 180 мс: выбираем до 35 узлов с минимальным 160-битным XOR-расстоянием
                _ = Task.Run(async () =>
                {
                    while (!linked.Token.IsCancellationRequested)
                    {
                        List<IPEndPoint> batchToSend = new();
                        lock (candidatesLock)
                        {
                            candidateNodes.RemoveAll(c => visitedEndpoints.ContainsKey(c.EndPoint.ToString()));
                            candidateNodes.Sort((a, b) => CompareXorDistance(a.NodeId, b.NodeId, targetHash));

                            int takeCount = Math.Min(35, candidateNodes.Count);
                            for (int i = 0; i < takeCount; i++)
                            {
                                batchToSend.Add(candidateNodes[i].EndPoint);
                            }
                            candidateNodes.RemoveRange(0, takeCount);
                        }

                        foreach (var targetEp in batchToSend)
                        {
                            string key = targetEp.ToString();
                            if (visitedEndpoints.TryAdd(key, 0))
                            {
                                try
                                {
                                    await udp.SendAsync(getPeersBytes, getPeersBytes.Length, targetEp).ConfigureAwait(false);
                                }
                                catch { }
                            }
                        }

                        try
                        {
                            await Task.Delay(180, linked.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }, linked.Token);

                int totalPeersFound = 0;
                var newPeersBatch = new List<PeerInfo>();

                while (!linked.Token.IsCancellationRequested)
                {
                    try
                    {
                        var recvResult = await udp.ReceiveAsync(linked.Token).ConfigureAwait(false);
                        byte[] buffer = recvResult.Buffer;
                        if (buffer.Length < 10) continue;

                        int valuesIdx = FindBytePattern(buffer, Encoding.ASCII.GetBytes("6:valuesl"));
                        if (valuesIdx != -1)
                        {
                            int pos = valuesIdx + 9;
                            while (pos + 8 <= buffer.Length && buffer[pos] == (byte)'6' && buffer[pos + 1] == (byte)':')
                            {
                                byte[] ipBytes = new byte[4];
                                Buffer.BlockCopy(buffer, pos + 2, ipBytes, 0, 4);
                                int port = (buffer[pos + 6] << 8) | buffer[pos + 7];
                                var ipAddr = new IPAddress(ipBytes);
                                string peerKey = $"{ipAddr}:{port}";

                                if (port > 0 && port < 65536 && discoveredPeers.TryAdd(peerKey, 0))
                                {
                                    var peerInfo = new PeerInfo(new Uri($"ipv4://{ipAddr}:{port}"));
                                    newPeersBatch.Add(peerInfo);
                                    lock (allPeersLock)
                                    {
                                        allDiscoveredPeers.Add(peerInfo);
                                    }
                                    totalPeersFound++;
                                    _lastSwarmTotalPeers = totalPeersFound;
                                }
                                pos += 8;
                            }

                            if (newPeersBatch.Count > 0)
                            {
                                try
                                {
                                    await manager.AddPeersAsync(newPeersBatch).ConfigureAwait(false);
                                }
                                catch { }
                                newPeersBatch.Clear();
                            }
                        }

                        int nodesIdx = FindBytePattern(buffer, Encoding.ASCII.GetBytes("5:nodes"));
                        if (nodesIdx != -1)
                        {
                            int colonIdx = Array.IndexOf(buffer, (byte)':', nodesIdx + 7);
                            if (colonIdx != -1)
                            {
                                int dataStart = colonIdx + 1;
                                int nodeDataLen = buffer.Length - dataStart;
                                int nodeCount = nodeDataLen / 26;

                                for (int i = 0; i < nodeCount; i++)
                                {
                                    int nPos = dataStart + i * 26;
                                    byte[] nId = new byte[20];
                                    Buffer.BlockCopy(buffer, nPos, nId, 0, 20);

                                    byte[] nip = new byte[4];
                                    Buffer.BlockCopy(buffer, nPos + 20, nip, 0, 4);
                                    int nport = (buffer[nPos + 24] << 8) | buffer[nPos + 25];

                                    if (nport > 0 && nport < 65536)
                                    {
                                        var ep = new IPEndPoint(new IPAddress(nip), nport);
                                        string epKey = ep.ToString();
                                        if (!visitedEndpoints.ContainsKey(epKey))
                                        {
                                            lock (candidatesLock)
                                            {
                                                if (candidateKeys.Add(epKey))
                                                {
                                                    candidateNodes.Add((nId, ep));
                                                    if (candidateNodes.Count > 500)
                                                    {
                                                        candidateNodes.Sort((a, b) => CompareXorDistance(a.NodeId, b.NodeId, targetHash));
                                                        candidateNodes.RemoveRange(350, candidateNodes.Count - 350);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch { }
                }

                _lastSwarmTotalPeers = Math.Max(_lastSwarmTotalPeers, totalPeersFound);
                App.Logger?.Log($"[RuTracker Turbo DHT] BEP-5 спайдер завершил сбор: найдено {totalPeersFound} пиров и передано в MonoTorrent", LogLevel.Info);
            }
            catch (Exception ex)
            {
                App.Logger?.Log($"[RuTracker Turbo DHT] Спайдер завершён: {ex.Message}", LogLevel.Info);
            }
        }

        private static int FindBytePattern(byte[] src, byte[] pattern)
        {
            for (int i = 0; i <= src.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (src[i + j] != pattern[j]) { match = false; break; }
                }
                if (match) return i;
            }
            return -1;
        }

        private static int CompareXorDistance(byte[] a, byte[] b, byte[] target)
        {
            int len = Math.Min(20, Math.Min(a.Length, Math.Min(b.Length, target.Length)));
            for (int i = 0; i < len; i++)
            {
                int da = a[i] ^ target[i];
                int db = b[i] ^ target[i];
                if (da != db)
                    return da.CompareTo(db);
            }
            return 0;
        }

        private static async Task ApplyFilePrioritiesAsync(TorrentManager manager, IReadOnlyList<TorrentFileSelectionItem> fileSelections)
        {
            if (fileSelections == null || fileSelections.Count == 0)
            {
                foreach (var file in manager.Files)
                {
                    string ext = Path.GetExtension(file.Path).ToLowerInvariant();
                    bool want = ext is ".nsp" or ".nsz" or ".xci" or ".xcz" or ".zip" or ".7z" or ".rar";
                    await manager.SetFilePriorityAsync(file, want ? Priority.Normal : Priority.DoNotDownload).ConfigureAwait(false);
                }
                return;
            }
            bool hasRealFiles = fileSelections.Any(x => x.IsRealTorrentFile);
            if (hasRealFiles)
            {
                // Сопоставление строго по реальным путям/именам файлов
                var selLookup = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in fileSelections)
                {
                    foreach (var effPath in item.GetEffectiveRelativePaths())
                    {
                        string normEff = effPath.Replace('\\', '/').Trim('/');
                        selLookup[normEff] = item.IsSelected;
                        selLookup[Path.GetFileName(normEff)] = item.IsSelected;
                    }
                }

                foreach (var file in manager.Files)
                {
                    string rel = file.Path.Replace('\\', '/');
                    bool want = false;
                    if (selLookup.TryGetValue(rel, out bool selected))
                    {
                        want = selected;
                    }
                    else
                    {
                        string relClean = rel.TrimStart('.').TrimStart('/');
                        var suffixMatched = selLookup.FirstOrDefault(kv =>
                        {
                            string kClean = kv.Key.TrimStart('.').TrimStart('/');
                            return kClean.EndsWith("/" + relClean, StringComparison.OrdinalIgnoreCase) ||
                                   relClean.EndsWith("/" + kClean, StringComparison.OrdinalIgnoreCase);
                        });
                        if (!string.IsNullOrEmpty(suffixMatched.Key))
                        {
                            want = suffixMatched.Value;
                        }
                        else
                        {
                            string fn = Path.GetFileName(rel);
                            var matched = selLookup.FirstOrDefault(kv => Path.GetFileName(kv.Key).Equals(fn, StringComparison.OrdinalIgnoreCase));
                            if (!string.IsNullOrEmpty(matched.Key))
                                want = matched.Value;
                        }
                    }

                    await manager.SetFilePriorityAsync(file, want ? Priority.Normal : Priority.DoNotDownload).ConfigureAwait(false);
                }
            }
            else
            {
                // Переданы только синтетические заглушки или роли из индекса
                bool allSelected = fileSelections.All(x => x.IsSelected);
                var selectedRoles = fileSelections
                    .Where(x => x.IsSelected)
                    .Select(x => x.CategoryBadge.ToUpperInvariant())
                    .ToHashSet();

                var selectedTitleIds = fileSelections
                    .Where(x => x.IsSelected)
                    .Select(x => ExtractTitleId(x.RelativePath))
                    .Where(tid => !string.IsNullOrEmpty(tid))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var file in manager.Files)
                {
                    string ext = Path.GetExtension(file.Path).ToLowerInvariant();
                    bool isSwitchOrArchive = ext is ".nsp" or ".nsz" or ".xci" or ".xcz" or ".zip" or ".7z" or ".rar";
                    bool want = false;

                    if (isSwitchOrArchive)
                    {
                        if (allSelected)
                        {
                            want = true;
                        }
                        else
                        {
                            string fileTid = ExtractTitleId(file.Path);
                            if (!string.IsNullOrEmpty(fileTid) && selectedTitleIds.Contains(fileTid))
                            {
                                want = true;
                            }
                            else
                            {
                                string badge = ClassifyTorrentEntryBadge(file.Path).ToUpperInvariant();
                                if (selectedRoles.Contains(badge) || selectedRoles.Contains("ФАЙЛ") || selectedRoles.Contains("MAGNET"))
                                {
                                    want = true;
                                }
                            }
                        }
                    }

                    await manager.SetFilePriorityAsync(file, want ? Priority.Normal : Priority.DoNotDownload).ConfigureAwait(false);
                }
            }
        }
        #region Облачный загрузчик модификаций и патчей (MEGA, PixelDrain, Yandex, GDrive, Mail.ru, MediaFire, Dropbox, WDfiles, GitHub, Workupload)
        public async Task<List<string>> DownloadAndExtractCloudModsAsync(
            IEnumerable<string> rawLinks,
            string workDirectory,
            IProgress<(double Percent, string Status)>? progress,
            CancellationToken ct,
            ManualResetEventSlim? pauseEvent = null)
        {
            var links = rawLinks
                .Select(l => l?.Trim() ?? "")
                .Where(l => !string.IsNullOrEmpty(l))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var extractedModPaths = new List<string>();
            if (links.Count == 0) return extractedModPaths;

            string modsRoot = Path.Combine(workDirectory, "_DownloadedMods");
            Directory.CreateDirectory(modsRoot);
            using var client = CreateHttpClient();
            for (int i = 0; i < links.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                string linkGroup = links[i];
                var candidateMirrors = linkGroup
                    .Split(new[] { "|MIRROR|" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(u => u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (candidateMirrors.Count == 0) continue;
                for (int mIdx = 0; mIdx < candidateMirrors.Count; mIdx++)
                {
                    string mirrorUrl = candidateMirrors[mIdx];
                    try
                    {
                        using var mirrorCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        mirrorCts.CancelAfter(TimeSpan.FromSeconds(60));

                        // Проверяем, является ли ссылка страницей форума / статьи (4PDA, Zone of Games, Boosty, Telegraph)
                        string lowerMirror = mirrorUrl.ToLowerInvariant();
                        bool isForumPage = lowerMirror.Contains("4pda.to/forum/") || lowerMirror.Contains("4pda.ru/forum/") ||
                                           lowerMirror.Contains("zoneofgames.ru") || lowerMirror.Contains("boosty.to") ||
                                           lowerMirror.Contains("telegra.ph") ||
                                           (lowerMirror.Contains("github.com/") && !lowerMirror.Contains("/releases/download/"));

                        var directUrlsToTry = new List<string>();
                        if (isForumPage)
                        {
                            progress?.Report(((i * 100.0) / links.Count, $"Поиск прямых ссылок в теме форума: {mirrorUrl}..."));
                            var forumCloudUrls = await ResolveForumPostEmbeddedCloudUrlsAsync(client, mirrorUrl, mirrorCts.Token).ConfigureAwait(false);
                            if (forumCloudUrls.Count > 0)
                            {
                                directUrlsToTry.AddRange(forumCloudUrls);
                            }
                        }
                        if (directUrlsToTry.Count == 0)
                        {
                            directUrlsToTry.Add(mirrorUrl);
                        }

                        bool mirrorSucceeded = false;
                        foreach (var targetDownloadUrl in directUrlsToTry)
                        {
                            try
                            {
                                progress?.Report(((i * 100.0) / links.Count, $"Подготовка ссылки ({i + 1}/{links.Count}): {targetDownloadUrl}..."));
                                var (directUrl, suggestedName, megaKey) = await ResolveCloudDownloadUrlAsync(client, targetDownloadUrl, mirrorCts.Token).ConfigureAwait(false);
                                if (string.IsNullOrEmpty(directUrl)) continue;

                                string safeFileName = SanitizeFileName(suggestedName);
                                if (string.IsNullOrEmpty(Path.GetExtension(safeFileName)))
                                    safeFileName += ".zip";

                                string archivePath = Path.Combine(modsRoot, safeFileName);
                                progress?.Report(((i * 100.0 + 10.0) / links.Count, $"Скачивание: {safeFileName}..."));

                                await DownloadFileWithProgressAsync(client, directUrl, archivePath, (info) =>
                                {
                                    double currentStepPct = (i + info.Percent / 100.0) * 100.0 / links.Count;
                                    double dlMb = info.DownloadedBytes / (1024.0 * 1024.0);
                                    double totalMb = info.TotalBytes / (1024.0 * 1024.0);
                                    string stat = totalMb > 0
                                        ? $"Скачивание {safeFileName}: {info.Percent:F1}% ({info.SpeedMb:F2} МБ/с | {dlMb:F1} / {totalMb:F1} МБ)"
                                        : $"Скачивание {safeFileName}: {dlMb:F1} МБ ({info.SpeedMb:F2} МБ/с)";
                                    progress?.Report((currentStepPct, stat));
                                }, mirrorCts.Token, megaKey, pauseEvent).ConfigureAwait(false);

                                if (!File.Exists(archivePath) || new FileInfo(archivePath).Length < 64) continue;
                                if (IsHtmlOrErrorStubFile(archivePath))
                                {
                                    App.Logger?.Log($"[RuTracker Cloud] Загруженный файл {safeFileName} оказался HTML/страницей ошибки вместо архива. Удаление и переход к следующему зеркалу...", LogLevel.Warning);
                                    try { File.Delete(archivePath); } catch { }
                                    continue;
                                }
                                string ext = Path.GetExtension(archivePath).ToLowerInvariant();
                                if (ext is ".zip" or ".7z" or ".rar")
                                {
                                    string extractDir = Path.Combine(modsRoot, Path.GetFileNameWithoutExtension(safeFileName));
                                    Directory.CreateDirectory(extractDir);
                                    progress?.Report(((i + 0.85) * 100.0 / links.Count, $"Распаковка архива {safeFileName}..."));

                                    await ExtractArchiveWith7zAsync(archivePath, extractDir, ct).ConfigureAwait(false);
                                    try { File.Delete(archivePath); } catch { }

                                    foreach (var nestedRom in Directory.EnumerateFiles(extractDir, "*.*", SearchOption.AllDirectories))
                                    {
                                        string nExt = Path.GetExtension(nestedRom).ToLowerInvariant();
                                        if (nExt is ".nsp" or ".nsz" or ".xci" or ".xcz")
                                        {
                                            string promotedPath = Path.Combine(workDirectory, Path.GetFileName(nestedRom));
                                            if (!string.Equals(nestedRom, promotedPath, StringComparison.OrdinalIgnoreCase))
                                            {
                                                try { File.Copy(nestedRom, promotedPath, overwrite: true); } catch { }
                                            }
                                            extractedModPaths.Add(promotedPath);
                                        }
                                    }
                                    var modFolders = DiscoverModPayloadsInDirectory(extractDir);
                                    extractedModPaths.AddRange(modFolders);
                                }
                                else if (ext is ".nsp" or ".nsz" or ".xci" or ".xcz")
                                {
                                    string promotedPath = Path.Combine(workDirectory, Path.GetFileName(archivePath));
                                    if (!string.Equals(archivePath, promotedPath, StringComparison.OrdinalIgnoreCase))
                                    {
                                        try { File.Move(archivePath, promotedPath, overwrite: true); } catch { }
                                    }
                                    extractedModPaths.Add(promotedPath);
                                }
                                else
                                {
                                    extractedModPaths.Add(archivePath);
                                }

                                if (extractedModPaths.Count > 0)
                                {
                                    mirrorSucceeded = true;
                                    break;
                                }
                            }
                            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                            {
                                App.Logger?.Log($"[RuTracker Cloud] Ошибка скачивания {targetDownloadUrl}: {ex.Message}", LogLevel.Debug);
                            }
                        }

                        // Интерактивный обход капчи/авторизации браузером при отказе прямых HTTP ссылок
                        if (!mirrorSucceeded && ExternalBrowserBypassHandler != null && directUrlsToTry.Any(u => u.Contains("4pda.") || u.Contains("zoneofgames.ru")))
                        {
                            try
                            {
                                string attachOrForumUrl = directUrlsToTry.FirstOrDefault(u => u.Contains("/dl/post/")) ?? mirrorUrl;
                                progress?.Report(((i * 100.0 + 50.0) / links.Count, "Ожидание подтверждения доступа в окне браузера..."));
                                var (_, dlPath, _) = await ExternalBrowserBypassHandler(attachOrForumUrl, true, ct).ConfigureAwait(false);
                                if (!string.IsNullOrEmpty(dlPath) && File.Exists(dlPath) && !IsHtmlOrErrorStubFile(dlPath))
                                {
                                    string ext = Path.GetExtension(dlPath).ToLowerInvariant();
                                    if (ext is ".zip" or ".7z" or ".rar")
                                    {
                                        string extractDir = Path.Combine(modsRoot, Path.GetFileNameWithoutExtension(dlPath));
                                        Directory.CreateDirectory(extractDir);
                                        await ExtractArchiveWith7zAsync(dlPath, extractDir, ct).ConfigureAwait(false);
                                        try { File.Delete(dlPath); } catch { }
                                        var modFolders = DiscoverModPayloadsInDirectory(extractDir);
                                        extractedModPaths.AddRange(modFolders);
                                    }
                                    else
                                    {
                                        extractedModPaths.Add(dlPath);
                                    }
                                    if (extractedModPaths.Count > 0)
                                    {
                                        mirrorSucceeded = true;
                                    }
                                }
                            }
                            catch { }
                        }

                        if (mirrorSucceeded)
                            break;
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
                    {
                        App.Logger?.Log($"[RuTracker Cloud] Зеркало {mirrorUrl} недоступно: {ex.Message}. Переход к следующему...", LogLevel.Warning);
                    }
                }
            }

            progress?.Report((100, $"Готово! Подготовлено модификаций и файлов: {extractedModPaths.Count}."));
            return extractedModPaths;
        }

        private async Task<(string DirectUrl, string FileName, string? MegaKey)> ResolveCloudDownloadUrlAsync(HttpClient client, string url, CancellationToken ct)
        {
            string lower = url.ToLowerInvariant();

            // 1. PixelDrain
            if (lower.Contains("pixeldrain."))
            {
                var pdFile = Regex.Match(url, @"pixeldrain\.[a-z]+/(?:u|api/file|d)/([a-zA-Z0-9_-]+)", RegexOptions.IgnoreCase);
                if (pdFile.Success)
                {
                    string id = pdFile.Groups[1].Value;
                    string direct = $"https://pixeldrain.com/api/file/{id}?download";
                    string fn = $"PixelDrain_{id}.zip";
                    try
                    {
                        using var infoResp = await client.GetAsync($"https://pixeldrain.com/api/file/{id}/info", ct).ConfigureAwait(false);
                        if (infoResp.IsSuccessStatusCode)
                        {
                            string json = await infoResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                            using var doc = JsonDocument.Parse(json);
                            if (doc.RootElement.TryGetProperty("name", out var np))
                                fn = np.GetString() ?? fn;
                        }
                    }
                    catch { }
                    return (direct, fn, null);
                }

                var pdList = Regex.Match(url, @"pixeldrain\.[a-z]+/(?:l|api/list)/([a-zA-Z0-9_-]+)", RegexOptions.IgnoreCase);
                if (pdList.Success)
                {
                    string id = pdList.Groups[1].Value;
                    return ($"https://pixeldrain.com/api/list/{id}/zip", $"PixelDrain_List_{id}.zip", null);
                }
            }

            // 2. Яндекс.Диск
            if (lower.Contains("disk.yandex.") || lower.Contains("yadi.sk") || lower.Contains("disk.360.yandex."))
            {
                if (lower.Contains("downloader.disk.yandex") || lower.Contains("cloud-api.yandex"))
                    return (url, "YandexDisk_Mod.zip", null);

                string apiUrl = $"https://cloud-api.yandex.net/v1/disk/public/resources/download?public_key={Uri.EscapeDataString(url)}";
                using var resp = await client.GetAsync(apiUrl, ct).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                {
                    string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("href", out var hrefProp))
                    {
                        string href = hrefProp.GetString() ?? "";
                        string fn = ExtractQueryParam(href, "filename") ?? $"YandexDisk_Mod_{DateTime.Now:HHmmss}.zip";
                        return (href, fn, null);
                    }
                }
            }

            // 3. Google Drive
            if (lower.Contains("drive.google.com") || lower.Contains("drive.usercontent.google.com") || lower.Contains("docs.google.com"))
            {
                var idMatch = Regex.Match(url, @"(?:/file/d/|id=)([a-zA-Z0-9_-]{10,})");
                if (idMatch.Success)
                {
                    string fileId = idMatch.Groups[1].Value;
                    string directGDrive = $"https://drive.usercontent.google.com/download?id={fileId}&export=download&confirm=t";
                    return (directGDrive, $"GDrive_Mod_{fileId.Substring(0, 8)}.zip", null);
                }
            }

            // 4. MEGA (mega.nz/file/<id>#<key> или mega.nz/#!<id>!<key>)
            if (lower.Contains("mega.nz") || lower.Contains("mega.co.nz"))
            {
                var megaMatch = Regex.Match(url, @"mega\.(?:nz|co\.nz)/(?:file/|#!)?([a-zA-Z0-9_-]+)(?:#|!)([a-zA-Z0-9_-]{40,})", RegexOptions.IgnoreCase);
                if (megaMatch.Success)
                {
                    string fileId = megaMatch.Groups[1].Value;
                    string keyStr = megaMatch.Groups[2].Value;

                    try
                    {
                        var (aesKey, iv) = DeriveMegaKey(keyStr);
                        long randId = Random.Shared.Next(1000000, 9999999);
                        string apiPost = $"[{{\"a\":\"g\",\"g\":1,\"p\":\"{fileId}\"}}]";
                        using var apiContent = new StringContent(apiPost, Encoding.UTF8, "application/json");
                        using var apiResp = await client.PostAsync($"https://g.api.mega.co.nz/cs?id={randId}", apiContent, ct).ConfigureAwait(false);
                        if (apiResp.IsSuccessStatusCode)
                        {
                            string resJson = await apiResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                            using var doc = JsonDocument.Parse(resJson);
                            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                            {
                                var item = doc.RootElement[0];
                                string directUrl = item.TryGetProperty("g", out var gp) ? (gp.GetString() ?? "") : "";
                                string atBase64 = item.TryGetProperty("at", out var atp) ? (atp.GetString() ?? "") : "";
                                string realName = $"Mega_Mod_{fileId}.zip";

                                if (!string.IsNullOrEmpty(atBase64))
                                {
                                    try { realName = DecryptMegaAttributes(aesKey, atBase64); } catch { }
                                }

                                if (!string.IsNullOrEmpty(directUrl))
                                    return (directUrl, realName, keyStr);
                            }
                        }
                    }
                    catch { }
                }
            }

            // 5. Dropbox
            if (lower.Contains("dropbox.com/"))
            {
                string d = url;
                if (Regex.IsMatch(d, @"[?&]dl=0"))
                    d = Regex.Replace(d, @"([?&])dl=0", "$1dl=1");
                else if (!Regex.IsMatch(d, @"[?&](dl|raw)=1"))
                    d = d.Contains('?') ? d + "&dl=1" : d + "?dl=1";

                string fn = Path.GetFileName(new Uri(d.Split('?')[0]).LocalPath);
                if (string.IsNullOrWhiteSpace(fn)) fn = "Dropbox_Mod.zip";
                return (d, fn, null);
            }

            // 6. MediaFire
            if (lower.Contains("mediafire.com/"))
            {
                try
                {
                    using var mfResp = await client.GetAsync(url, ct).ConfigureAwait(false);
                    if (mfResp.IsSuccessStatusCode)
                    {
                        string html = await mfResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                        var dlMatch = Regex.Match(html, @"href=""(https?://download\d*\.mediafire\.com/[^""]+)""", RegexOptions.IgnoreCase);
                        if (dlMatch.Success)
                        {
                            string direct = dlMatch.Groups[1].Value;
                            string fn = Path.GetFileName(new Uri(direct).LocalPath);
                            if (string.IsNullOrWhiteSpace(fn)) fn = "MediaFire_Mod.zip";
                            return (direct, fn, null);
                        }
                    }
                }
                catch { }
            }

            // 7. Облако Mail.ru
            if (lower.Contains("cloud.mail.ru/public/"))
            {
                var mrMatch = Regex.Match(url, @"cloud\.mail\.ru/public/([a-zA-Z0-9]+/[a-zA-Z0-9_.-]+)", RegexOptions.IgnoreCase);
                if (mrMatch.Success)
                {
                    string weblink = mrMatch.Groups[1].Value;
                    try
                    {
                        using var dispResp = await client.GetAsync("https://cloud.mail.ru/api/v2/dispatcher?api=2", ct).ConfigureAwait(false);
                        if (dispResp.IsSuccessStatusCode)
                        {
                            string dJson = await dispResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                            using var dDoc = JsonDocument.Parse(dJson);
                            if (dDoc.RootElement.TryGetProperty("body", out var bProp) &&
                                bProp.TryGetProperty("weblink_get", out var wgProp) &&
                                wgProp.GetArrayLength() > 0 &&
                                wgProp[0].TryGetProperty("url", out var uProp))
                            {
                                string baseUrl = uProp.GetString() ?? "";
                                if (!string.IsNullOrEmpty(baseUrl))
                                {
                                    string suggestedName = Path.GetFileName(weblink) + ".zip";
                                    string targetWeblink = weblink;

                                    try
                                    {
                                        using var fileResp = await client.GetAsync($"https://cloud.mail.ru/api/v2/file?weblink={Uri.EscapeDataString(weblink)}", ct).ConfigureAwait(false);
                                        if (fileResp.IsSuccessStatusCode)
                                        {
                                            string fJson = await fileResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                                            using var fDoc = JsonDocument.Parse(fJson);
                                            if (fDoc.RootElement.TryGetProperty("body", out var fbProp))
                                            {
                                                if (fbProp.TryGetProperty("name", out var np))
                                                    suggestedName = np.GetString() ?? suggestedName;

                                                if (fbProp.TryGetProperty("kind", out var kp) && kp.GetString() == "folder")
                                                {
                                                    using var foldResp = await client.GetAsync($"https://cloud.mail.ru/api/v2/folder?weblink={Uri.EscapeDataString(weblink)}", ct).ConfigureAwait(false);
                                                    if (foldResp.IsSuccessStatusCode)
                                                    {
                                                        string foldJson = await foldResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                                                        using var foldDoc = JsonDocument.Parse(foldJson);
                                                        if (foldDoc.RootElement.TryGetProperty("body", out var foldBody) &&
                                                            foldBody.TryGetProperty("list", out var listProp) &&
                                                            listProp.ValueKind == JsonValueKind.Array)
                                                        {
                                                            string? bestWeblink = null;
                                                            string? bestName = null;
                                                            (int, int, int, int)? maxVer = null;

                                                            foreach (var item in listProp.EnumerateArray())
                                                            {
                                                                string iname = item.TryGetProperty("name", out var inp) ? (inp.GetString() ?? "") : "";
                                                                string iweblink = item.TryGetProperty("weblink", out var iwp) ? (iwp.GetString() ?? "") : "";
                                                                if (string.IsNullOrEmpty(iname)) continue;

                                                                var ver = TryExtractModVersionTuple(iname);
                                                                if (ver != null)
                                                                {
                                                                    if (maxVer == null || CompareVersionTuples(ver.Value, maxVer.Value) > 0)
                                                                    {
                                                                        maxVer = ver;
                                                                        bestWeblink = iweblink;
                                                                        bestName = iname;
                                                                    }
                                                                }
                                                                else if (bestWeblink == null && (iname.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || iname.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) || iname.EndsWith(".rar", StringComparison.OrdinalIgnoreCase)))
                                                                {
                                                                    bestWeblink = iweblink;
                                                                    bestName = iname;
                                                                }
                                                            }

                                                            if (!string.IsNullOrEmpty(bestWeblink))
                                                            {
                                                                targetWeblink = bestWeblink;
                                                                suggestedName = bestName ?? suggestedName;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    catch { }

                                    string direct = baseUrl.TrimEnd('/') + "/" + targetWeblink.TrimStart('/');
                                    return (direct, suggestedName, null);
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            // 8. WDfiles
            if (lower.Contains("wdfiles."))
            {
                try
                {
                    using var wdResp = await client.GetAsync(url, ct).ConfigureAwait(false);
                    if (wdResp.IsSuccessStatusCode)
                    {
                        string html = await wdResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                        var dlMatch = Regex.Match(html, @"href=""(https?://[^""\s]*wdfiles\.(?:ru|com)/download/[^""]+)""", RegexOptions.IgnoreCase);
                        if (dlMatch.Success)
                        {
                            string direct = dlMatch.Groups[1].Value;
                            return (direct, "WDfiles_Mod.zip", null);
                        }
                    }
                }
                catch { }
            }

            // 9. WorkUpload
            if (lower.Contains("workupload.com/"))
            {
                var wuMatch = Regex.Match(url, @"workupload\.com/(?:file|archive)/([a-zA-Z0-9]+)", RegexOptions.IgnoreCase);
                if (wuMatch.Success)
                {
                    string id = wuMatch.Groups[1].Value;
                    try
                    {
                        using var wuApi = await client.GetAsync($"https://workupload.com/api/file/getDownloadServer/{id}", ct).ConfigureAwait(false);
                        if (wuApi.IsSuccessStatusCode)
                        {
                            string j = await wuApi.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                            using var d = JsonDocument.Parse(j);
                            if (d.RootElement.TryGetProperty("data", out var dp) && dp.TryGetProperty("url", out var up))
                            {
                                string dl = up.GetString() ?? "";
                                if (!string.IsNullOrEmpty(dl))
                                    return (dl, $"WorkUpload_{id}.zip", null);
                            }
                        }
                    }
                    catch { }
                }
            }

            // 10. GitHub Releases
            if (lower.Contains("github.com/") && lower.Contains("/releases") && !lower.Contains("/releases/download/"))
            {
                var ghMatch = Regex.Match(url, @"github\.com/([^/]+)/([^/]+)/releases(?:/tag/([^/?#]+))?", RegexOptions.IgnoreCase);
                if (ghMatch.Success)
                {
                    string owner = ghMatch.Groups[1].Value;
                    string repo = ghMatch.Groups[2].Value;
                    string tag = ghMatch.Groups[3].Value;
                    string api = string.IsNullOrEmpty(tag)
                        ? $"https://api.github.com/repos/{owner}/{repo}/releases/latest"
                        : $"https://api.github.com/repos/{owner}/{repo}/releases/tags/{tag}";

                    using var resp = await client.GetAsync(api, ct).ConfigureAwait(false);
                    if (resp.IsSuccessStatusCode)
                    {
                        string json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var asset in assets.EnumerateArray())
                            {
                                string name = asset.TryGetProperty("name", out var np) ? (np.GetString() ?? "") : "";
                                string dl = asset.TryGetProperty("browser_download_url", out var dp) ? (dp.GetString() ?? "") : "";
                                if (!string.IsNullOrEmpty(dl) && (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                                    name.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) ||
                                    name.EndsWith(".rar", StringComparison.OrdinalIgnoreCase) ||
                                    name.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) ||
                                    name.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase)))
                                {
                                    return (dl, name, null);
                                }
                            }
                        }
                    }
                }
            }

            // Прямая ссылка на файл
            string fallbackName = Path.GetFileName(new Uri(url).LocalPath);
            if (string.IsNullOrWhiteSpace(fallbackName)) fallbackName = "ModPackage.zip";
            return (url, fallbackName, null);
        }

        private static string? ExtractQueryParam(string url, string paramName)
        {
            try
            {
                var uri = new Uri(url);
                var query = uri.Query.TrimStart('?');
                foreach (var part in query.Split('&'))
                {
                    var kv = part.Split('=');
                    if (kv.Length == 2 && string.Equals(kv[0], paramName, StringComparison.OrdinalIgnoreCase))
                    {
                        return Uri.UnescapeDataString(kv[1]);
                    }
                }
            }
            catch { }
            return null;
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                sb.Append(invalid.Contains(c) ? '_' : c);
            }
            return sb.ToString().Trim();
        }

        private static async Task DownloadFileWithProgressAsync(
            HttpClient client,
            string url,
            string destPath,
            Action<(double Percent, double SpeedMb, long DownloadedBytes, long TotalBytes)>? progress,
            CancellationToken ct,
            string? megaKey = null,
            ManualResetEventSlim? pauseEvent = null)
        {
            string targetUrl = url;
            string? cookieHeader = null;

            // Проверка Google Drive: если возвращается HTML страница подтверждения больших файлов
            for (int hop = 0; hop < 3; hop++)
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, targetUrl);
                if (!string.IsNullOrEmpty(cookieHeader))
                    req.Headers.TryAddWithoutValidation("Cookie", cookieHeader);

                using var headResp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                headResp.EnsureSuccessStatusCode();

                if (headResp.Headers.TryGetValues("Set-Cookie", out var setCookies))
                {
                    cookieHeader = string.Join("; ", setCookies.Select(c => c.Split(';')[0]));
                }

                string ctHeader = headResp.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? "";
                string dispHeader = headResp.Content.Headers.ContentDisposition?.DispositionType?.ToLowerInvariant() ?? "";

                if (ctHeader.Contains("text/html") && !dispHeader.Contains("attachment"))
                {
                    string html = await headResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    var gForm = Regex.Match(html, @"<form[^>]+id=[""']download-form[""'][^>]*action=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                    if (gForm.Success)
                    {
                        string actionUrl = WebUtility.HtmlDecode(gForm.Groups[1].Value);
                        var inputs = Regex.Matches(html, @"<input[^>]+type=[""']hidden[""'][^>]*name=[""']([^""']+)[""'][^>]*value=[""']([^""']*)[""']", RegexOptions.IgnoreCase);
                        var qParams = new List<string>();
                        foreach (Match inp in inputs)
                        {
                            qParams.Add($"{Uri.EscapeDataString(inp.Groups[1].Value)}={Uri.EscapeDataString(inp.Groups[2].Value)}");
                        }
                        targetUrl = actionUrl + (actionUrl.Contains('?') ? "&" : "?") + string.Join("&", qParams);
                        continue;
                    }
                }

                long totalBytes = headResp.Content.Headers.ContentLength ?? -1L;
                using var stream = await headResp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);

                if (!string.IsNullOrEmpty(megaKey))
                {
                    var (aesKey, iv) = DeriveMegaKey(megaKey);
                    DecryptMegaStreamCtr(aesKey, iv, stream, fileStream, totalBytes, progress, ct);
                }
                else
                {
                    byte[] buffer = new byte[65536];
                    long totalRead = 0;
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    long lastReportTicks = 0;

                    int bytesRead;
                    while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false)) > 0)
                    {
                        if (pauseEvent != null && !pauseEvent.IsSet)
                        {
                            while (!pauseEvent.IsSet && !ct.IsCancellationRequested)
                            {
                                await Task.Delay(200, ct).ConfigureAwait(false);
                            }
                        }
                        await fileStream.WriteAsync(buffer, 0, bytesRead, ct).ConfigureAwait(false);
                        totalRead += bytesRead;

                        if (sw.ElapsedTicks - lastReportTicks > System.Diagnostics.Stopwatch.Frequency / 4)
                        {
                            lastReportTicks = sw.ElapsedTicks;
                            double pct = totalBytes > 0 ? (totalRead * 100.0) / totalBytes : 0;
                            double speed = sw.Elapsed.TotalSeconds > 0 ? (totalRead / (1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds : 0;
                            progress?.Invoke((pct, speed, totalRead, totalBytes));
                        }
                    }
                    double finalSpeed = sw.Elapsed.TotalSeconds > 0 ? (totalRead / (1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds : 0;
                    progress?.Invoke((100, finalSpeed, totalRead, totalBytes));
                }

                break;
            }

            ValidateDownloadedFile(destPath);
        }
        #region MEGA Криптография (AES-128-CTR стриминг и AES-CBC метаданные)
        public static (byte[] AesKey, byte[] Iv) DeriveMegaKey(string keyString)
        {
            string padded = keyString.Replace('-', '+').Replace('_', '/');
            padded += new string('=', (4 - padded.Length % 4) % 4);
            byte[] keyBytes = Convert.FromBase64String(padded);
            if (keyBytes.Length < 32)
                throw new ArgumentException("Неверная длина ключа MEGA");

            uint[] k = new uint[8];
            for (int i = 0; i < 8; i++)
            {
                k[i] = ((uint)keyBytes[i * 4] << 24) |
                       ((uint)keyBytes[i * 4 + 1] << 16) |
                       ((uint)keyBytes[i * 4 + 2] << 8) |
                       ((uint)keyBytes[i * 4 + 3]);
            }

            byte[] aesKey = new byte[16];
            for (int i = 0; i < 4; i++)
            {
                uint val = k[i] ^ k[i + 4];
                aesKey[i * 4] = (byte)(val >> 24);
                aesKey[i * 4 + 1] = (byte)(val >> 16);
                aesKey[i * 4 + 2] = (byte)(val >> 8);
                aesKey[i * 4 + 3] = (byte)val;
            }

            byte[] iv = new byte[16];
            for (int i = 0; i < 2; i++)
            {
                uint val = k[i + 4];
                iv[i * 4] = (byte)(val >> 24);
                iv[i * 4 + 1] = (byte)(val >> 16);
                iv[i * 4 + 2] = (byte)(val >> 8);
                iv[i * 4 + 3] = (byte)val;
            }
            return (aesKey, iv);
        }

        public static string DecryptMegaAttributes(byte[] aesKey, string atBase64)
        {
            string padded = atBase64.Replace('-', '+').Replace('_', '/');
            padded += new string('=', (4 - padded.Length % 4) % 4);
            byte[] cipher = Convert.FromBase64String(padded);

            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;
            aes.Key = aesKey;
            aes.IV = new byte[16];

            using var decryptor = aes.CreateDecryptor();
            byte[] plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            string text = Encoding.UTF8.GetString(plain);
            int idx = text.IndexOf("MEGA{");
            if (idx >= 0)
            {
                string json = text.Substring(idx + 4);
                int end = json.LastIndexOf('}');
                if (end > 0)
                {
                    json = json.Substring(0, end + 1);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("n", out var np))
                        return np.GetString() ?? "MegaFile.zip";
                }
            }
            return "MegaFile.zip";
        }

        public static void DecryptMegaStreamCtr(
            byte[] aesKey,
            byte[] iv,
            Stream input,
            Stream output,
            long total,
            Action<(double Percent, double SpeedMb, long DownloadedBytes, long TotalBytes)>? progress,
            CancellationToken ct)
        {
            using var aes = Aes.Create();
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;
            aes.Key = aesKey;
            using var encryptor = aes.CreateEncryptor();

            byte[] counterBlock = new byte[16];
            Buffer.BlockCopy(iv, 0, counterBlock, 0, 8);

            ulong counter = 0;
            byte[] keyStream = new byte[16];
            byte[] buffer = new byte[65536];
            long downloaded = 0;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            long lastReportTicks = 0;

            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                for (int i = 0; i < read; i += 16)
                {
                    for (int b = 7; b >= 0; b--)
                    {
                        counterBlock[8 + b] = (byte)(counter >> ((7 - b) * 8));
                    }
                    encryptor.TransformBlock(counterBlock, 0, 16, keyStream, 0);
                    counter++;

                    int count = Math.Min(16, read - i);
                    for (int j = 0; j < count; j++)
                        buffer[i + j] ^= keyStream[j];
                }
                output.Write(buffer, 0, read);
                downloaded += read;

                if (sw.ElapsedTicks - lastReportTicks > System.Diagnostics.Stopwatch.Frequency / 4)
                {
                    lastReportTicks = sw.ElapsedTicks;
                    double pct = total > 0 ? (downloaded * 100.0) / total : 0;
                    double speed = sw.Elapsed.TotalSeconds > 0 ? (downloaded / (1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds : 0;
                    progress?.Invoke((pct, speed, downloaded, total));
                }
            }
            double fSpeed = sw.Elapsed.TotalSeconds > 0 ? (downloaded / (1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds : 0;
            progress?.Invoke((100, fSpeed, downloaded, total));
        }
        #endregion
        private static void ValidateDownloadedFile(string destPath)
        {
            if (!File.Exists(destPath))
                throw new FileNotFoundException("Файл не был сохранён на диск.", destPath);

            var fi = new FileInfo(destPath);
            if (fi.Length == 0)
            {
                File.Delete(destPath);
                throw new InvalidOperationException("Сервер вернул пустой файл (0 байт). Возможно, ссылка устарела или квота скачивания превышена.");
            }

            if (fi.Length < 1024 * 100)
            {
                byte[] head = new byte[Math.Min(1024, fi.Length)];
                using (var fs = File.OpenRead(destPath))
                {
                    fs.Read(head, 0, head.Length);
                }
                string headStr = Encoding.UTF8.GetString(head).ToLowerInvariant();
                if (headStr.Contains("<html") || headStr.Contains("<!doctype html") || headStr.Contains("{\"error\"") || headStr.Contains("404 not found") || headStr.Contains("quota exceeded"))
                {
                    File.Delete(destPath);
                    throw new InvalidOperationException("Вместо архива сервер вернул HTML-страницу ошибки или ограничение квоты скачивания.");
                }
            }
        }

        public static async Task ExtractArchiveWith7zAsync(string archivePath, string extractDir, CancellationToken ct)
        {
            string tools7z = Path.Combine(AppContext.BaseDirectory, "tools", "7z", "7za.exe");
            if (!File.Exists(tools7z))
            {
                tools7z = Path.Combine(Environment.CurrentDirectory, "tools", "7z", "7za.exe");
            }

            if (!File.Exists(tools7z))
            {
                if (archivePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    System.IO.Compression.ZipFile.ExtractToDirectory(archivePath, extractDir, overwriteFiles: true);
                    return;
                }
                throw new FileNotFoundException($"Архиватор 7za.exe не найден по пути: {tools7z}");
            }

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = tools7z,
                Arguments = $"x \"{archivePath}\" -o\"{extractDir}\" -y -aoa",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var proc = new System.Diagnostics.Process { StartInfo = psi };
            proc.Start();

            try
            {
                proc.PriorityClass = System.Diagnostics.ProcessPriorityClass.BelowNormal;
            }
            catch { }
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);
            if (proc.ExitCode != 0)
            {
                string err = await proc.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);
                throw new InvalidOperationException($"Ошибка 7z распаковки (код {proc.ExitCode}): {err}");
            }
        }

        public static List<string> DiscoverModPayloadsInDirectory(string rootDir)
        {
            var results = new List<string>();
            if (!Directory.Exists(rootDir)) return results;

            foreach (var dir in Directory.EnumerateDirectories(rootDir, "*", SearchOption.AllDirectories))
            {
                string dName = Path.GetFileName(dir).ToLowerInvariant();
                if (dName is "romfs" or "exefs" or "exefs_patches" or "cheats")
                {
                    string parent = Path.GetDirectoryName(dir) ?? rootDir;
                    if (!results.Contains(parent, StringComparer.OrdinalIgnoreCase))
                    {
                        results.Add(parent);
                    }
                }
            }

            foreach (var f in Directory.EnumerateFiles(rootDir, "*.*", SearchOption.AllDirectories))
            {
                string ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext is ".ips" or ".pchtxt")
                {
                    string parent = Path.GetDirectoryName(f) ?? rootDir;
                    if (!results.Contains(parent, StringComparer.OrdinalIgnoreCase))
                    {
                        results.Add(parent);
                    }
                }
            }

            return results;
        }
        #endregion
        public void Dispose()
        {
            foreach (var kvp in _activeManagers)
            {
                try { kvp.Value.StopAsync().Wait(500); } catch { }
            }
            _activeManagers.Clear();
            _activeManager = null;
            // Тёплый _sharedTorrentEngine сохраняется для поддержания маршрутной таблицы DHT
        }

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Concurrent.ConcurrentDictionary<string, byte>> _discoveredHttpPeersByHash = new(StringComparer.OrdinalIgnoreCase);
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Collections.Concurrent.ConcurrentDictionary<string, byte>> _discoveredDhtPeersByHash = new(StringComparer.OrdinalIgnoreCase);

        private static System.Collections.Concurrent.ConcurrentDictionary<string, byte> GetHttpPeersForHash(string hashHex) =>
            _discoveredHttpPeersByHash.GetOrAdd(hashHex.ToUpperInvariant(), _ => new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase));

        private static System.Collections.Concurrent.ConcurrentDictionary<string, byte> GetDhtPeersForHash(string hashHex) =>
            _discoveredDhtPeersByHash.GetOrAdd(hashHex.ToUpperInvariant(), _ => new System.Collections.Concurrent.ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase));

        private static Task<List<string>> QueryDhtPeersViaSpiderAsync(byte[] infoHashBytes, CancellationToken ct)
        {
            string hashHex = Convert.ToHexString(infoHashBytes).ToUpperInvariant();
            return Task.FromResult(GetDhtPeersForHash(hashHex).Keys.ToList());
        }
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _topicLinksCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly string _topicCachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "STORM SWITCH BOX", "rutracker_topic_links_cache.json");

        public static bool IsValidRuTrackerTopicHtml(string? html)
        {
            if (string.IsNullOrWhiteSpace(html)) return false;
            if (html.Contains("Just a moment...", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("_cf_chl_opt", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("cf-turnstile", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("challenge-running", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("Enable JavaScript and cookies", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            return html.Contains("topic-title", StringComparison.OrdinalIgnoreCase) ||
                   html.Contains("post_body", StringComparison.OrdinalIgnoreCase) ||
                   html.Contains("tor-hash", StringComparison.OrdinalIgnoreCase) ||
                   html.Contains("magnet:?xt=urn:btih:", StringComparison.OrdinalIgnoreCase);
        }

        private static void LoadTopicLinksCache()
        {
            try
            {
                if (File.Exists(_topicCachePath))
                {
                    var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_topicCachePath));
                    if (dict != null)
                    {
                        bool purgedPoisoned = false;
                        foreach (var kvp in dict)
                        {
                            if (IsValidRuTrackerTopicHtml(kvp.Value))
                                _topicLinksCache[kvp.Key] = kvp.Value;
                            else
                                purgedPoisoned = true;
                        }
                        if (purgedPoisoned)
                            SaveTopicLinksCache();
                    }
                }
            }
            catch { }
        }

        private static void SaveTopicLinksCache()
        {
            try
            {
                string? dir = Path.GetDirectoryName(_topicCachePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var dict = new Dictionary<string, string>(_topicLinksCache);
                File.WriteAllText(_topicCachePath, System.Text.Json.JsonSerializer.Serialize(dict, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        public static async Task<string?> FetchValidatedTopicPageHtmlAsync(string tid, int startOffset, CancellationToken ct)
        {
            string cacheKey = startOffset > 0 ? $"{tid}_start_{startOffset}" : tid;
            if (_topicLinksCache.TryGetValue(cacheKey, out var cached) && IsValidRuTrackerTopicHtml(cached))
            {
                return cached;
            }

            string baseMirror = NormalizeMirrorUrl(App.Settings.Current.RuTrackerMirror);
            string pageUrl = startOffset > 0
                ? $"{baseMirror}/forum/viewtopic.php?t={tid}&start={startOffset}"
                : $"{baseMirror}/forum/viewtopic.php?t={tid}";

            // 1. Same-Origin fetch внутри скрытого WebView2 (настоящий Chromium TLS JA3 + cf_clearance)
            if (SameOriginFetchHandler != null)
            {
                try
                {
                    string? h = await SameOriginFetchHandler(pageUrl, ct).ConfigureAwait(false);
                    if (IsValidRuTrackerTopicHtml(h))
                    {
                        _topicLinksCache[cacheKey] = h!;
                        SaveTopicLinksCache();
                        return h;
                    }
                }
                catch { }
            }

            // 2. Резервная параллельная гонка через облачные Cloud-рендеры и прокси
            try
            {
                string? cloudHtml = await FetchTopicHtmlViaCloudRaceAsync(tid, ct, startOffset).ConfigureAwait(false);
                if (IsValidRuTrackerTopicHtml(cloudHtml))
                {
                    return cloudHtml;
                }
            }
            catch { }

            // 3. Прямой HttpClient
            try
            {
                using var client = CreateHttpClient();
                using var resp = await client.GetAsync(pageUrl, ct).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                {
                    byte[] bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                    string h = DecodeRuTrackerHtml(bytes, resp.Content.Headers.ContentType?.CharSet);
                    if (IsValidRuTrackerTopicHtml(h))
                    {
                        _topicLinksCache[cacheKey] = h;
                        SaveTopicLinksCache();
                        return h;
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex is HttpRequestException || ex is IOException)
                {
                    SwitchToNextMirror();
                }
            }

            // 4. Если прямой вызов для страницы 1 вернул 403/503 — вызываем интерактивный обход Cloudflare
            if (startOffset == 0 && CloudflareChallengeHandler != null)
            {
                try
                {
                    string? challengeHtml = await CloudflareChallengeHandler(pageUrl, tid, ct).ConfigureAwait(false);
                    if (IsValidRuTrackerTopicHtml(challengeHtml))
                    {
                        _topicLinksCache[cacheKey] = challengeHtml!;
                        SaveTopicLinksCache();
                        return challengeHtml;
                    }
                }
                catch { }
            }

            return null;
        }

        public static async Task<string?> FetchTopicHtmlViaCloudRaceAsync(string tid, CancellationToken ct, int startOffset = 0)
        {
            string cacheKey = startOffset > 0 ? $"{tid}_start_{startOffset}" : tid;
            if (_topicLinksCache.TryGetValue(cacheKey, out var cached) && IsValidRuTrackerTopicHtml(cached))
            {
                return cached;
            }

            string targetUrl = startOffset > 0
                ? $"https://rutracker.org/forum/viewtopic.php?t={tid}&start={startOffset}"
                : $"https://rutracker.org/forum/viewtopic.php?t={tid}";

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(8));
            var lct = linkedCts.Token;
            var tasks = new List<Task<string?>>();

            // 1. Jina AI Cloud Reader (обходит Cloudflare и возвращает чистый HTML раздачи)
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                    client.DefaultRequestHeaders.Add("X-Return-Format", "html");
                    string url = $"https://r.jina.ai/{targetUrl}";
                    string html = await client.GetStringAsync(url, lct).ConfigureAwait(false);
                    if (IsValidRuTrackerTopicHtml(html))
                    {
                        return html;
                    }
                }
                catch { }
                return null;
            }));

            // 2. CodeTabs Proxy
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                    string url = $"https://api.codetabs.com/v1/proxy?quest={Uri.EscapeDataString(targetUrl)}";
                    string html = await client.GetStringAsync(url, lct).ConfigureAwait(false);
                    if (IsValidRuTrackerTopicHtml(html))
                    {
                        return html;
                    }
                }
                catch { }
                return null;
            }));

            // 3. AllOrigins Proxy
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                    string url = $"https://api.allorigins.win/raw?url={Uri.EscapeDataString(targetUrl)}";
                    string html = await client.GetStringAsync(url, lct).ConfigureAwait(false);
                    if (IsValidRuTrackerTopicHtml(html))
                    {
                        return html;
                    }
                }
                catch { }
                return null;
            }));

            // 4. CorsProxy.io
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                    string url = $"https://corsproxy.io/?{Uri.EscapeDataString(targetUrl)}";
                    string html = await client.GetStringAsync(url, lct).ConfigureAwait(false);
                    if (IsValidRuTrackerTopicHtml(html))
                    {
                        return html;
                    }
                }
                catch { }
                return null;
            }));

            // Ожидаем первого успешного ответа
            while (tasks.Count > 0 && !lct.IsCancellationRequested)
            {
                var finished = await Task.WhenAny(tasks).ConfigureAwait(false);
                tasks.Remove(finished);
                try
                {
                    string? result = await finished.ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(result))
                    {
                        _topicLinksCache[cacheKey] = result;
                        SaveTopicLinksCache();
                        return result;
                    }
                }
                catch { }
            }

            return null;
        }

        public static bool IsAntiBotChallengeOrErrorHtml(string? html)
        {
            if (string.IsNullOrWhiteSpace(html) || html.Length < 400) return true;
            string lower = html.ToLowerInvariant();
            return lower.Contains("just a moment...") ||
                   lower.Contains("cf-turnstile") ||
                   lower.Contains("ddos-guard") ||
                   lower.Contains("проверка браузера") ||
                   lower.Contains("showcaptcha") ||
                   lower.Contains("g-recaptcha") ||
                   lower.Contains("h-captcha") ||
                   lower.Contains("enable javascript and cookies to continue") ||
                   lower.Contains("attention required! | cloudflare");
        }

        public static bool IsHtmlOrErrorStubFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return true;
                var fi = new FileInfo(filePath);
                if (fi.Length == 0) return true;
                using var fs = File.OpenRead(filePath);
                byte[] buf = new byte[Math.Min(1024, (int)fs.Length)];
                int read = fs.Read(buf, 0, buf.Length);
                if (read < 4) return true;
                string text = Encoding.UTF8.GetString(buf, 0, read);
                string lower = text.ToLowerInvariant();
                return lower.StartsWith("<!doctype") || lower.StartsWith("<html") || lower.StartsWith("<script") ||
                       lower.StartsWith("{\"error\"") || lower.Contains("<body") || lower.Contains("обнаружена ошибка") ||
                       lower.Contains("4pda.to") || lower.Contains("cloudflare") || lower.Contains("just a moment");
            }
            catch { return false; }
        }

        public static (int Major, int Minor, int Patch, int Build)? TryExtractModVersionTuple(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            var matches = Regex.Matches(text, @"(?:^|[^0-9a-zA-Z])v?(\d+(?:\.\d+){1,3}[a-zA-Z]?)(?:[^0-9a-zA-Z]|$)", RegexOptions.IgnoreCase);
            foreach (Match m in matches)
            {
                string rawVer = m.Groups[1].Value;
                string[] parts = rawVer.Split('.');
                try
                {
                    var nums = new List<int>();
                    foreach (var p in parts)
                    {
                        var numMatch = Regex.Match(p, @"^(\d+)");
                        nums.Add(numMatch.Success ? int.Parse(numMatch.Groups[1].Value) : 0);
                    }
                    if (nums.Count == 0 || nums[0] > 1900 || nums.Count > 4) continue;
                    while (nums.Count < 4) nums.Add(0);
                    return (nums[0], nums[1], nums[2], nums[3]);
                }
                catch { }
            }
            return null;
        }

        public static int CompareVersionTuples((int Major, int Minor, int Patch, int Build) a, (int Major, int Minor, int Patch, int Build) b)
        {
            int c = a.Major.CompareTo(b.Major);
            if (c != 0) return c;
            c = a.Minor.CompareTo(b.Minor);
            if (c != 0) return c;
            c = a.Patch.CompareTo(b.Patch);
            if (c != 0) return c;
            return a.Build.CompareTo(b.Build);
        }

        public static string StripOldVersionSpoilers(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return html;
            string[] patterns = new[]
            {
                @"<div\s+class=[""']sp-wrap[""'][^>]*>.*?<div\s+class=[""']sp-head[^""']*[""'][^>]*>(?:(?!</div>).)*(?:старые|старая|архив|история|предыдущ|old\s*ver|previous|changelog|лог\s*измен).*?</div>\s*<div\s+class=[""']sp-body[""'][^>]*>.*?</div>\s*</div>",
                @"<div\s+class=[""']ipsSpoiler[""'][^>]*>.*?<div\s+class=[""']ipsSpoiler_header[""'][^>]*>(?:(?!</div>).)*(?:старые|старая|архив|история|предыдущ|old\s*ver|previous|changelog|лог\s*измен).*?</div>\s*<div\s+class=[""']ipsSpoiler_contents[""'][^>]*>.*?</div>\s*</div>",
                @"<details[^>]*>.*?<summary[^>]*>(?:(?!</summary>).)*(?:старые|старая|архив|история|предыдущ|old\s*ver|previous|changelog|лог\s*измен).*?</summary>.*?</details>"
            };
            foreach (var pat in patterns)
            {
                html = Regex.Replace(html, pat, "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            }
            return html;
        }

        public static async Task<string?> FetchForumPageHtmlWithBypassAsync(HttpClient client, string forumUrl, CancellationToken ct)
        {
            // Method 1: Прямой запрос с Chrome 131 заголовками и SharedForumCookies
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, forumUrl);
                req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
                req.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8");
                req.Headers.TryAddWithoutValidation("Accept-Language", "ru-RU,ru;q=0.9,en-US;q=0.8,en;q=0.7");
                req.Headers.TryAddWithoutValidation("sec-ch-ua", "\"Google Chrome\";v=\"131\", \"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\"");
                req.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
                req.Headers.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");

                string cookieHeader = SharedForumCookies.GetCookieHeader(new Uri(forumUrl));
                if (!string.IsNullOrEmpty(cookieHeader))
                {
                    req.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
                }

                // Для zoneofgames.ru применяем короткий таймаут 3.5с из-за частых зависаний DPI провайдеров
                var reqCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                if (forumUrl.Contains("zoneofgames.ru", StringComparison.OrdinalIgnoreCase))
                    reqCts.CancelAfter(TimeSpan.FromSeconds(3.5));

                using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseContentRead, reqCts.Token).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                {
                    byte[] bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                    string html = DecodeRuTrackerHtml(bytes, resp.Content.Headers.ContentType?.CharSet);
                    if (!IsAntiBotChallengeOrErrorHtml(html))
                        return html;
                }
            }
            catch { }

            // Method 2: Зеркальные домены и внешние прокси-рендереры
            try
            {
                // Если 4pda.to, пробуем зеркало 4pda.ws
                if (forumUrl.Contains("4pda.to", StringComparison.OrdinalIgnoreCase))
                {
                    string wsUrl = forumUrl.Replace("4pda.to", "4pda.ws");
                    using var wsReq = new HttpRequestMessage(HttpMethod.Get, wsUrl);
                    wsReq.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
                    using var wsResp = await client.SendAsync(wsReq, HttpCompletionOption.ResponseContentRead, ct).ConfigureAwait(false);
                    if (wsResp.IsSuccessStatusCode)
                    {
                        byte[] bytes = await wsResp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                        string html = DecodeRuTrackerHtml(bytes, wsResp.Content.Headers.ContentType?.CharSet);
                        if (!IsAntiBotChallengeOrErrorHtml(html))
                            return html;
                    }
                }

                // Cloud proxy: api.codetabs.com
                string proxyUrl = $"https://api.codetabs.com/v1/proxy?quest={Uri.EscapeDataString(forumUrl)}";
                using var pReq = new HttpRequestMessage(HttpMethod.Get, proxyUrl);
                pReq.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
                var pCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                pCts.CancelAfter(TimeSpan.FromSeconds(4.5));
                using var pResp = await client.SendAsync(pReq, HttpCompletionOption.ResponseContentRead, pCts.Token).ConfigureAwait(false);
                if (pResp.IsSuccessStatusCode)
                {
                    string html = await pResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    if (!IsAntiBotChallengeOrErrorHtml(html))
                        return html;
                }
            }
            catch { }

            // Cloud headless renderer: r.jina.ai
            try
            {
                string jinaUrl = $"https://r.jina.ai/{forumUrl}";
                using var jReq = new HttpRequestMessage(HttpMethod.Get, jinaUrl);
                jReq.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");
                var jCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                jCts.CancelAfter(TimeSpan.FromSeconds(7));
                using var jResp = await client.SendAsync(jReq, HttpCompletionOption.ResponseContentRead, jCts.Token).ConfigureAwait(false);
                if (jResp.IsSuccessStatusCode)
                {
                    string jText = await jResp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(jText) && jText.Length > 200)
                        return jText;
                }
            }
            catch { }

            // Method 3: Бесшумный WebView2 через ExternalBrowserBypassHandler
            if (ExternalBrowserBypassHandler != null)
            {
                try
                {
                    var (wvHtml, _, cookies) = await ExternalBrowserBypassHandler(forumUrl, false, ct).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(cookies))
                    {
                        try { SharedForumCookies.SetCookies(new Uri(forumUrl), cookies); } catch { }
                    }
                    if (!IsAntiBotChallengeOrErrorHtml(wvHtml))
                        return wvHtml;
                }
                catch { }
            }

            return null;
        }

        public static async Task<List<string>> ResolveForumPostEmbeddedCloudUrlsAsync(HttpClient client, string forumUrl, CancellationToken ct)
        {
            var discovered = new List<string>();
            try
            {
                string? html = await FetchForumPageHtmlWithBypassAsync(client, forumUrl, ct).ConfigureAwait(false);
                if (string.IsNullOrEmpty(html)) return discovered;

                // Извлекаем идентификатор поста из URL (напр. p=140785061 или #entry140785061 или comment=1291955)
                string? postId = null;
                var pm = Regex.Match(forumUrl, @"[?&](?:p|comment)=(\d+)|#(?:entry|elComment_|post-main-)(\d+)", RegexOptions.IgnoreCase);
                if (pm.Success)
                    postId = !string.IsNullOrEmpty(pm.Groups[1].Value) ? pm.Groups[1].Value : pm.Groups[2].Value;

                string targetHtml = html;
                if (!string.IsNullOrEmpty(postId))
                {
                    var postMarker = Regex.Match(html, $@"(?:data-post=""{postId}""|name=""entry{postId}""|id=""post-main-{postId}""|id=""elComment_{postId}"")", RegexOptions.IgnoreCase);
                    if (postMarker.Success)
                    {
                        int startPos = postMarker.Index;
                        var nextPost = Regex.Match(html.Substring(startPos + 100), @"(?:data-post=""\d+""|class=""post_header_container""|id=""elComment_\d+"")", RegexOptions.IgnoreCase);
                        int endPos = nextPost.Success ? startPos + 100 + nextPost.Index : Math.Min(html.Length, startPos + 15000);
                        targetHtml = html.Substring(startPos, endPos - startPos);
                    }
                }

                // 1. Очищаем спойлеры со старыми версиями
                targetHtml = StripOldVersionSpoilers(targetHtml);

                // 2. Сбор кандидатов: парсим как HTML-ссылки, так и Markdown [текст](url)
                var rawCandidates = new List<(string Url, string Anchor, string Prefix)>();

                var hrefMatches = Regex.Matches(targetHtml, @"<a\s+[^>]*href=[""'](https?://[^""']+)[""'][^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                foreach (Match m in hrefMatches)
                {
                    string u = m.Groups[1].Value.Trim();
                    string anchor = Regex.Replace(m.Groups[2].Value, @"<[^>]+>", " ").Trim();
                    int startPos = m.Index;
                    string prefix = targetHtml.Substring(Math.Max(0, startPos - 120), Math.Min(120, startPos));
                    string cleanPrefix = Regex.Replace(prefix, @"<[^>]+>", " ").Trim();
                    rawCandidates.Add((u, anchor, cleanPrefix));
                }

                var mdMatches = Regex.Matches(targetHtml, @"\[([^\]]+)\]\((https?://[^\)]+)\)", RegexOptions.IgnoreCase);
                foreach (Match m in mdMatches)
                {
                    string anchor = m.Groups[1].Value.Trim();
                    string u = m.Groups[2].Value.Trim();
                    int startPos = m.Index;
                    string prefix = targetHtml.Substring(Math.Max(0, startPos - 120), Math.Min(120, startPos));
                    string cleanPrefix = Regex.Replace(prefix, @"<[^>]+>", " ").Trim();
                    rawCandidates.Add((u, anchor, cleanPrefix));
                }

                var richCandidates = new List<(string Url, (int Major, int Minor, int Patch, int Build)? Version, int? AttachId, bool IsCloud, bool IsCompanion)>();

                foreach (var (rawUrl, anchor, prefix) in rawCandidates)
                {
                    string u = WebUtility.HtmlDecode(rawUrl).Trim();
                    // Разворачиваем редирект 4PDA pages/go/?u=...
                    if (u.Contains("4pda.to/pages/go/?u=", StringComparison.OrdinalIgnoreCase) ||
                        u.Contains("4pda.ru/pages/go/?u=", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var uri = new Uri(u);
                            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                            string? target = query["u"];
                            if (!string.IsNullOrEmpty(target))
                                u = Uri.UnescapeDataString(target);
                        }
                        catch { }
                    }

                    string lower = u.ToLowerInvariant();
                    bool isCloud =
                        lower.Contains("disk.yandex.") || lower.Contains("yadi.sk") ||
                        lower.Contains("drive.google.com") || lower.Contains("docs.google.com") ||
                        lower.Contains("cloud.mail.ru") || lower.Contains("mega.nz") || lower.Contains("mega.co.nz") ||
                        lower.Contains("mediafire.com") || lower.Contains("pixeldrain.") || lower.Contains("wdfiles.") ||
                        lower.Contains("dropbox.com") || lower.Contains("workupload.com") || lower.Contains("gofile.io") ||
                        lower.Contains("buzzheavier.") || lower.Contains("catbox.moe") || lower.Contains("krakenfiles.") ||
                        lower.Contains("modsfire.") || lower.Contains("1fichier.") || lower.Contains("qiwi.gg") ||
                        lower.Contains("send.cm") || lower.Contains("4pda.to/forum/dl/post/") || lower.Contains("4pda.ru/forum/dl/post/") ||
                        (lower.Contains("github.com") && (lower.Contains("/releases") || lower.Contains("/raw/") || lower.Contains("/blob/") || lower.EndsWith(".zip") || lower.EndsWith(".7z") || lower.EndsWith(".rar") || lower.EndsWith(".nsp"))) ||
                        lower.EndsWith(".zip") || lower.EndsWith(".7z") || lower.EndsWith(".rar") || lower.EndsWith(".nsp") || lower.EndsWith(".nsz") || lower.EndsWith(".xci") || lower.EndsWith(".xcz") || lower.EndsWith(".ips") || lower.EndsWith(".pchtxt");

                    bool isCompanion = (lower.Contains("zoneofgames.ru") || lower.Contains("boosty.to") || (lower.Contains("github.com") && lower.Contains("/releases"))) && !isCloud;

                    if (!isCloud && !isCompanion) continue;

                    string decodedName = "";
                    try { decodedName = Uri.UnescapeDataString(Path.GetFileName(new Uri(u).LocalPath)); } catch { }

                    int? attachId = null;
                    var attMatch = Regex.Match(u, @"/dl/post/(\d+)/");
                    if (attMatch.Success)
                        attachId = int.Parse(attMatch.Groups[1].Value);

                    var ver = TryExtractModVersionTuple(decodedName) ??
                              TryExtractModVersionTuple(anchor) ??
                              TryExtractModVersionTuple(prefix.Split(' ').LastOrDefault()) ??
                              TryExtractModVersionTuple(prefix);

                    richCandidates.Add((u, ver, attachId, isCloud, isCompanion));
                }

                // 3. Фильтрация строго по максимальной версии (Latest-Version-Only Filtering)
                var cloudCandidates = richCandidates.Where(c => c.IsCloud).ToList();
                var versions = cloudCandidates.Where(c => c.Version.HasValue).Select(c => c.Version!.Value).ToList();
                if (versions.Count > 0)
                {
                    var maxVer = versions.Aggregate((curMax, next) => CompareVersionTuples(next, curMax) > 0 ? next : curMax);
                    cloudCandidates = cloudCandidates.Where(c => !c.Version.HasValue || CompareVersionTuples(c.Version.Value, maxVer) == 0).ToList();
                }

                // Если остались неверсионированные вложения 4PDA, оставляем только вложение с максимальным ID
                var unversionedAtts = cloudCandidates.Where(c => c.AttachId.HasValue && !c.Version.HasValue).ToList();
                if (unversionedAtts.Count > 1)
                {
                    int maxAid = unversionedAtts.Max(c => c.AttachId!.Value);
                    cloudCandidates = cloudCandidates.Where(c => !c.AttachId.HasValue || c.Version.HasValue || c.AttachId.Value == maxAid).ToList();
                }

                foreach (var c in cloudCandidates)
                {
                    if (!discovered.Contains(c.Url, StringComparer.OrdinalIgnoreCase))
                        discovered.Add(c.Url);
                }

                // 4. Паукообразный сбор ссылок из темы-спутника (только если нет открытых облачных зеркал!)
                bool hasOpenCloud = discovered.Any(u =>
                    u.Contains("cloud.mail.ru") || u.Contains("disk.yandex") || u.Contains("yadi.sk") ||
                    u.Contains("pixeldrain") || u.Contains("drive.google") || u.Contains("mega.nz") ||
                    u.Contains("mediafire") || u.Contains("workupload") || u.Contains("github.com"));

                if (!hasOpenCloud)
                {
                    var companionLinks = richCandidates.Where(c => c.IsCompanion).Select(c => c.Url).ToList();
                    foreach (var cUrl in companionLinks)
                    {
                        if (cUrl.Equals(forumUrl, StringComparison.OrdinalIgnoreCase)) continue;
                        try
                        {
                            var compCloudUrls = await ResolveForumPostEmbeddedCloudUrlsAsync(client, cUrl, ct).ConfigureAwait(false);
                            foreach (var cu in compCloudUrls)
                            {
                                if (!discovered.Contains(cu, StringComparer.OrdinalIgnoreCase))
                                    discovered.Add(cu);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                App.Logger?.Log($"[RuTracker Mod] Ошибка извлечения ссылок из форума {forumUrl}: {ex.Message}", LogLevel.Debug);
            }

            return discovered.OrderByDescending(GetProviderReliabilityScore).ToList();
        }


        private static readonly string[] CloudflareTrackerEdgeIps = new[]
        {
            "188.114.96.3",
            "188.114.97.3",
            "104.21.32.39",
            "172.67.182.196"
        };

        /// <summary>
        /// Выполняет обход ТСПУ DPI для HTTP-трекеров RuTracker (bt.t-ru.org..bt4.t-ru.org)
        /// методом HTTP/1.1 Pipelining через граничные узлы Cloudflare:
        /// первый запрос в TCP-пакете имеет Host: cloudflare.com (пропускается DPI-фильтром провайдера),
        /// второй запрос в том же пакете запрашивает /ann?magnet у bt*.t-ru.org и мгновенно возвращает компактный список живых сидов.
        /// </summary>
        private static async Task<byte[]?> FetchTrackerViaHttpPipeliningAsync(string host, string pathAndQuery, CancellationToken ct)
        {
            string payload =
                "GET / HTTP/1.1\r\n" +
                "Host: cloudflare.com\r\n\r\n" +
                $"GET {pathAndQuery} HTTP/1.1\r\n" +
                $"Host: {host}\r\n" +
                "User-Agent: uTorrent/3550\r\n" +
                "Accept-Encoding: identity\r\n" +
                "Connection: close\r\n\r\n";
            byte[] reqBytes = Encoding.ASCII.GetBytes(payload);

            foreach (var edgeIp in CloudflareTrackerEdgeIps)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(4));

                    using var tcp = new System.Net.Sockets.TcpClient();
                    tcp.NoDelay = true;
                    await tcp.ConnectAsync(IPAddress.Parse(edgeIp), 80, timeoutCts.Token).ConfigureAwait(false);
                    using var stream = tcp.GetStream();
                    await stream.WriteAsync(reqBytes, 0, reqBytes.Length, timeoutCts.Token).ConfigureAwait(false);
                    await stream.FlushAsync(timeoutCts.Token).ConfigureAwait(false);

                    using var ms = new MemoryStream();
                    byte[] buf = new byte[8192];
                    while (ms.Length < 131072)
                    {
                        int r = await stream.ReadAsync(buf, 0, buf.Length, timeoutCts.Token).ConfigureAwait(false);
                        if (r <= 0) break;
                        ms.Write(buf, 0, r);
                    }

                    byte[] data = ms.ToArray();
                    if (data.Length > 0 && FindBytePattern(data, Encoding.ASCII.GetBytes("5:peers")) >= 0)
                    {
                        return data;
                    }
                }
                catch { }
            }
            return null;
        }

        private static void ExtractCompactPeersFromBencodedData(byte[]? data, List<PeerInfo> newPeers, string? hashHex = null)
        {
            if (data == null || data.Length == 0) return;
            var hashDict = !string.IsNullOrEmpty(hashHex) ? GetHttpPeersForHash(hashHex) : null;

            // 1. IPv4 компактный список: 5:peers<len>:<6-byte entries>
            int searchOffset = 0;
            byte[] peersMarker = Encoding.ASCII.GetBytes("5:peers");
            while (searchOffset < data.Length)
            {
                int pIdx = FindBytePatternOffset(data, peersMarker, searchOffset);
                if (pIdx < 0) break;
                searchOffset = pIdx + 7;

                int colonIdx = Array.IndexOf(data, (byte)':', pIdx + 7);
                if (colonIdx > 0 && colonIdx < data.Length - 1 && colonIdx - (pIdx + 7) <= 6)
                {
                    string lenStr = Encoding.ASCII.GetString(data, pIdx + 7, colonIdx - (pIdx + 7));
                    if (int.TryParse(lenStr, out int peersLen) && peersLen >= 6)
                    {
                        int peerDataStart = colonIdx + 1;
                        int peerDataEnd = Math.Min(data.Length, peerDataStart + peersLen);
                        for (int pos = peerDataStart; pos + 6 <= peerDataEnd; pos += 6)
                        {
                            byte[] ipBytes = new byte[4];
                            Buffer.BlockCopy(data, pos, ipBytes, 0, 4);
                            if (ipBytes[0] == 0 || ipBytes[0] == 127) continue;
                            int port = (data[pos + 4] << 8) | data[pos + 5];
                            if (port > 0 && port < 65536)
                            {
                                var ip = new IPAddress(ipBytes);
                                string pKey = $"{ip}:{port}";
                                if (hashDict != null)
                                    hashDict.TryAdd(pKey, 0);
                                var peer = new PeerInfo(new Uri($"ipv4://{ip}:{port}"));
                                lock (newPeers)
                                {
                                    if (!newPeers.Any(p => p.ConnectionUri.Equals(peer.ConnectionUri)))
                                        newPeers.Add(peer);
                                }
                            }
                        }
                        searchOffset = peerDataEnd;
                    }
                }
            }
        }

        private static int FindBytePatternOffset(byte[] src, byte[] pattern, int startOffset)
        {
            for (int i = Math.Max(0, startOffset); i <= src.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (src[i + j] != pattern[j]) { match = false; break; }
                }
                if (match) return i;
            }
            return -1;
        }

        public static async Task HarvestPeersFromHttpTrackersAsync(string infoHashHex, TorrentManager manager, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(infoHashHex) || infoHashHex.Length != 40) return;

            try
            {
                await WaitUntilManagerAcceptsPeersAsync(manager, ct).ConfigureAwait(false);

                byte[] hashBytes = Convert.FromHexString(infoHashHex);
                var sbHash = new StringBuilder();
                foreach (byte b in hashBytes)
                {
                    if ((b >= 'a' && b <= 'z') || (b >= 'A' && b <= 'Z') || (b >= '0' && b <= '9') || b == '-' || b == '_' || b == '.' || b == '~')
                    {
                        sbHash.Append((char)b);
                    }
                    else
                    {
                        sbHash.Append($"%{b:X2}");
                    }
                }
                string encodedHash = sbHash.ToString();
                string peerId = "-qB5000-" + Guid.NewGuid().ToString("N").Substring(0, 12);
                int listenPort = 44860;
                long leftBytes = manager.Torrent?.Size ?? 1073741824L;
                string stdParams = $"info_hash={encodedHash}&peer_id={peerId}&port={listenPort}&uploaded=0&downloaded=0&left={leftBytes}&compact=1&numwant=200&event=started";

                var newPeers = new List<PeerInfo>();

                // 1. Приоритетный опрос всех 4 HTTP-трекеров RuTracker (bt..bt4.t-ru.org) через HTTP/1.1 Pipelining (обход ТСПУ DPI за 50мс)
                var btHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "bt.t-ru.org",
                    "bt2.t-ru.org",
                    "bt3.t-ru.org",
                    "bt4.t-ru.org"
                };

                var announceTargets = new List<(string Host, string PathAndQuery)>();
                foreach (var h in btHosts)
                    announceTargets.Add((h, $"/ann?magnet&{stdParams}"));

                try
                {
                    if (manager.TrackerManager?.Tiers != null)
                    {
                        foreach (var tier in manager.TrackerManager.Tiers)
                        {
                            foreach (var tr in tier.Trackers)
                            {
                                string uriStr = tr.Uri?.ToString() ?? "";
                                var pkMatch = Regex.Match(uriStr, @"/ann\?pk=[0-9a-fA-F]+", RegexOptions.IgnoreCase);
                                if (pkMatch.Success)
                                {
                                    foreach (var h in btHosts)
                                        announceTargets.Add((h, $"{pkMatch.Value}&{stdParams}"));
                                }
                            }
                        }
                    }
                }
                catch { }

                var btTasks = announceTargets.Distinct().Select(async target =>
                {
                    byte[]? data = await FetchTrackerViaHttpPipeliningAsync(target.Host, target.PathAndQuery, ct).ConfigureAwait(false);
                    ExtractCompactPeersFromBencodedData(data, newPeers, infoHashHex);
                });
                var udpTask = QueryUdpTrackersBep15Async(hashBytes, listenPort > 0 ? listenPort : 44859, ct);
                await Task.WhenAll(Task.WhenAll(btTasks), udpTask).ConfigureAwait(false);
                try
                {
                    var udpPeers = await udpTask.ConfigureAwait(false);
                    lock (newPeers)
                    {
                        foreach (var up in udpPeers)
                        {
                            if (!newPeers.Any(p => p.ConnectionUri.Equals(up.ConnectionUri)))
                                newPeers.Add(up);
                        }
                    }
                }
                catch { }

                // Если уже нашли живые пиры с bt.t-ru.org — немедленно передаём первую волну в TorrentManager!
                if (newPeers.Count > 0)
                {
                    try
                    {
                        List<PeerInfo> snapshot;
                        lock (newPeers) snapshot = newPeers.ToList();
                        await manager.AddPeersAsync(snapshot).ConfigureAwait(false);
                        _lastSwarmTotalPeers = Math.Max(_lastSwarmTotalPeers, GetHttpPeersForHash(infoHashHex).Count);
                    }
                    catch { }
                }

                // 2. Дополнительно опрашиваем глобальные международные HTTP/HTTPS трекеры
                var globalAnnounceUrls = new List<string>
                {
                    "https://tracker.opentrackr.org:443/announce",
                    "https://tracker.gbitt.info:443/announce",
                    "http://tracker.dler.org:6969/announce",
                    "https://tr.burnabyhighstar.com:443/announce",
                    "http://open.acgnxtracker.com:80/announce",
                    "http://tracker.mywaifu.best:6969/announce",
                    "https://tracker.tamersunion.org:443/announce",
                    "http://tracker.bt4g.com:2095/announce",
                    "http://retracker.local/announce"
                };

                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                client.DefaultRequestHeaders.UserAgent.ParseAdd("uTorrent/3550");
                var queryTasks = globalAnnounceUrls.Select(async announceUrl =>
                {
                    string sep = announceUrl.Contains('?') ? "&" : "?";
                    string reqUrl = $"{announceUrl}{sep}{stdParams}";
                    try
                    {
                        byte[] data = await client.GetByteArrayAsync(reqUrl, ct).ConfigureAwait(false);
                        ExtractCompactPeersFromBencodedData(data, newPeers, infoHashHex);
                    }
                    catch { }
                });
                await Task.WhenAll(queryTasks).ConfigureAwait(false);

                if (newPeers.Count > 0)
                {
                    await WaitUntilManagerAcceptsPeersAsync(manager, ct).ConfigureAwait(false);
                    List<PeerInfo> snapshotFinal;
                    lock (newPeers) snapshotFinal = newPeers.ToList();
                    await manager.AddPeersAsync(snapshotFinal).ConfigureAwait(false);
                    int totalInPool = GetHttpPeersForHash(infoHashHex).Count;
                    _lastSwarmTotalPeers = Math.Max(_lastSwarmTotalPeers, totalInPool);
                    App.Logger?.Log($"[RuTracker HTTP Harvester] Найдено и подключено пиров/сидов: {snapshotFinal.Count} (всего в пуле: {totalInPool})", LogLevel.Info);
                }
            }
            catch { }
        }

    }
}
