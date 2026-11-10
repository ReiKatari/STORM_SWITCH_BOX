using StormSwitchBox.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;

namespace StormSwitchBox.Models
{
    public partial class ProcessingTask : ObservableObject
    {
        [ObservableProperty] private string _id = string.Empty;
        [ObservableProperty]
        [property: System.Text.Json.Serialization.JsonIgnore]
        private Microsoft.UI.Xaml.Media.Imaging.BitmapImage? _gameIcon;

        [System.Text.Json.Serialization.JsonIgnore]
        public byte[]? IconBytes { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public Microsoft.UI.Xaml.Media.Imaging.BitmapImage? IconBitmap
        {
            get => GameIcon;
            set => GameIcon = value;
        }
        [ObservableProperty] private string _gameName = string.Empty;
        [ObservableProperty] private string _operation = string.Empty;
        [ObservableProperty] private string _sourceFormat = string.Empty;
        private string _targetFormat = "NSP";
        public string TargetFormat
        {
            get => string.IsNullOrWhiteSpace(_targetFormat) ? (Is3dsTask ? "3DS" : "NSP") : _targetFormat;
            set
            {
                // CRITICAL: Prevent ComboBox virtualization recycling from resetting TargetFormat to null/empty!
                if (string.IsNullOrWhiteSpace(value))
                {
                    return;
                }
                if (SetProperty(ref _targetFormat, value))
                {
                    OnPropertyChanged(nameof(TargetFormatColor));
                    OnPropertyChanged(nameof(TargetFormatWeight));
                    OverwriteConfirmed = false;
                    if (IsCompleted && !IsRunning)
                    {
                        Status = "Ожидание";
                        Progress = 0;
                    }
                }
            }
        }

        [ObservableProperty] private bool _overwriteConfirmed;

        [ObservableProperty] private string _sourceSize = string.Empty;
        [ObservableProperty] private string _targetSize = string.Empty;
        [ObservableProperty] private string _sizeDifference = string.Empty;
        [ObservableProperty] private string _compressionLevel = string.Empty;
        [ObservableProperty] private string _filesCount = string.Empty;
        [ObservableProperty] private string _status = string.Empty;

        [System.Text.Json.Serialization.JsonIgnore]
        public string StatusDisplay => App.Localization.TranslateText(Status);

        [ObservableProperty] private double _progress;
        [ObservableProperty] private bool _isRunning;
        [ObservableProperty] private bool _isPaused;
        [ObservableProperty] private DateTime _finishedAt = DateTime.Now;

        [System.Text.Json.Serialization.JsonIgnore]
        public System.Threading.CancellationTokenSource? Cts { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public Action? OnPauseRequested { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public Action? OnResumeRequested { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public Action? OnCancelRequested { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public System.Threading.ManualResetEventSlim PauseEvent { get; } = new(true);

        [System.Text.Json.Serialization.JsonIgnore]
        public MonoTorrent.Client.TorrentManager? ActiveTorrentManager { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public RuTrackerTaskPayload? RuTrackerPayload { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool HasActiveWorker { get; set; }
        
        [System.Text.Json.Serialization.JsonIgnore]
        public string FinishedAtDisplay => FinishedAt.ToString("dd.MM.yyyy HH:mm");
        partial void OnFinishedAtChanged(DateTime value) => OnPropertyChanged(nameof(FinishedAtDisplay));
        
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsNotRunning => !IsRunning;
        partial void OnIsRunningChanged(bool value)
        {
            OnPropertyChanged(nameof(IsNotRunning));
            OnPropertyChanged(nameof(CanChangeFormat));
            OnPropertyChanged(nameof(PauseButtonVisibility));
            OnPropertyChanged(nameof(ResumeButtonVisibility));
        }

        partial void OnIsPausedChanged(bool value)
        {
            OnPropertyChanged(nameof(PauseButtonVisibility));
            OnPropertyChanged(nameof(ResumeButtonVisibility));
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public Visibility PauseButtonVisibility => IsRunning && !IsPaused ? Visibility.Visible : Visibility.Collapsed;

        [System.Text.Json.Serialization.JsonIgnore]
        public Visibility ResumeButtonVisibility => IsRunning && IsPaused ? Visibility.Visible : Visibility.Collapsed;

        [ObservableProperty] private int _buildMode = 0;
        [ObservableProperty] private bool _isSwitch2Task;
        [ObservableProperty] private string _titleId = string.Empty;
        [ObservableProperty] private bool _is3dsTask;
        partial void OnIs3dsTaskChanged(bool value)
        {
            AvailableTargetFormats = value
                ? new List<string> { "3DS", "CIA", "CXI", "3DS + CIA" }
                : new List<string> { "NSP", "NSZ", "XCI", "XCZ", "NUP", "NSP + NSZ", "XCI + XCZ" };

            if (_targetFormat == "3DS" || _targetFormat == "CIA" || _targetFormat == "CXI" || _targetFormat == "3DS + CIA")
            {
                if (!value) TargetFormat = "NSP";
            }
            else if (_targetFormat == "NSP" || _targetFormat == "NSZ" || _targetFormat == "XCI" || _targetFormat == "XCZ" || _targetFormat == "NSP + NSZ" || _targetFormat == "XCI + XCZ")
            {
                if (value) TargetFormat = "3DS";
            }
        }

        [ObservableProperty] private List<string> _availableTargetFormats = new() { "NSP", "NSZ", "XCI", "XCZ", "NUP", "NSP + NSZ", "XCI + XCZ" };

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsDualFormat =>
            string.Equals(TargetFormat, "NSP + NSZ", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(TargetFormat, "XCI + XCZ", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(TargetFormat, "3DS + CIA", StringComparison.OrdinalIgnoreCase);

        public string GetPrimaryExtension()
        {
            return TargetFormat switch
            {
                "NSP + NSZ" => ".nsp",
                "XCI + XCZ" => ".xci",
                "3DS + CIA" => ".3ds",
                "NSP" => ".nsp",
                "NSZ" => ".nsz",
                "XCI" => ".xci",
                "XCZ" => ".xcz",
                "NUP" => ".nup",
                "3DS" => ".3ds",
                "CIA" => ".cia",
                "CXI" => ".cxi",
                _ => "." + (string.IsNullOrWhiteSpace(TargetFormat) ? "nsp" : TargetFormat.Trim().ToLowerInvariant())
            };
        }

        public string GetSecondaryExtension()
        {
            return TargetFormat switch
            {
                "NSP + NSZ" => ".nsz",
                "XCI + XCZ" => ".xcz",
                "3DS + CIA" => ".cia",
                _ => string.Empty
            };
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsCompleted => Status == "Успешно" || Status == "Готово" || Status == "Корректна" || Status == "Ок" || Status == "Да" || Status == "Да (Гибрид)";

        [System.Text.Json.Serialization.JsonIgnore]
        public bool CanChangeFormat => !IsRunning;

        [System.Text.Json.Serialization.JsonIgnore]
        public Visibility FormatCompletedVisibility => Visibility.Collapsed;

        [System.Text.Json.Serialization.JsonIgnore]
        public Visibility FormatSelectableVisibility => Visibility.Visible;

        private static readonly SolidColorBrush FormatGreenBrush = new(Windows.UI.Color.FromArgb(255, 46, 204, 113));
        private static readonly SolidColorBrush FormatCyanBrush = new(Windows.UI.Color.FromArgb(255, 0, 229, 255));

        [System.Text.Json.Serialization.JsonIgnore]
        public SolidColorBrush TargetFormatColor => IsCompleted ? FormatGreenBrush : FormatCyanBrush;

        [System.Text.Json.Serialization.JsonIgnore]
        public Windows.UI.Text.FontWeight TargetFormatWeight => IsCompleted 
            ? Microsoft.UI.Text.FontWeights.Bold 
            : Microsoft.UI.Text.FontWeights.Normal;

        [ObservableProperty] private long _sourceSizeBytes;
        [ObservableProperty] private string _hasRomFs = "-";
        [ObservableProperty] private string _hasExeFs = "-";
        [ObservableProperty] private string _hasSaveData = "-";
        [ObservableProperty] private string _saveDataFolder = string.Empty;
        [ObservableProperty] private int _saveFilesCount;
        [ObservableProperty] private string _modNameRomFs = string.Empty;
        [ObservableProperty] private string _modNameExeFs = string.Empty;
        [ObservableProperty] private string _speed = string.Empty;
        [ObservableProperty] private bool _isExpanded;

        // Свойства верификации (раздел Проверка)
        [ObservableProperty] private string _verifyType = "-";
        [ObservableProperty] private string _verifyStructure = "-";
        [ObservableProperty] private string _verifyTitleId = "-";
        [ObservableProperty] private string _verifyVersion = "-";
        [ObservableProperty] private string _verifyMergedStatus = "-";

        // Детальная информация
        [ObservableProperty] private string _inputFolders = string.Empty;
        [ObservableProperty] private string _outputFolder = string.Empty;
        [ObservableProperty] private string _outputFileName = string.Empty;
        [ObservableProperty] private string _logDetails = string.Empty;

        // Идентификатор группы (первые 12 символов Title ID) для объединения файлов
        public string GroupId { get; set; } = string.Empty;

        // Список абсолютных путей к входным файлам (нужен для движка обработки)
        public List<string> InputFiles { get; set; } = new();

        // Список файлов (для отображения по клику - только имена)
        public List<string> FilesList { get; set; } = new();
        
        // Предварительный анализ
        /// <summary>Мульти-программный тайтл (несколько TitleID в одном NSP) — yanu-cli не может обработать</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsMultiProgramTitle { get; set; }

        /// <summary>Флаг задачи, созданной через RuTracker</summary>
        public bool IsRuTrackerTask { get; set; }

        /// <summary>Сохранять точное пользовательское имя выходного файла без принудительного добавления тегов [TitleID][vXXX](1G+1U)</summary>
        public bool KeepExactOutputFileName { get; set; }

        /// <summary>Удалять исходные файлы после успешной проверки выходного контейнера (для задач RuTracker)</summary>
        public bool DeleteSourceAfterVerify { get; set; } = true;
        
        /// <summary>Результат предварительного анализа входных файлов</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string PreAnalysisLog { get; set; } = string.Empty;

        /// <summary>Видимость панели деталей (привязка к IsExpanded)</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public Visibility DetailsVisibility => IsExpanded ? Visibility.Visible : Visibility.Collapsed;

        partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(DetailsVisibility));

        /// <summary>Локализованное название обработки для отображения в таблице</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string OperationDisplay => Operation switch
        {
            "Update" => LocalizationService.Instance["Nav_Update"],
            "Unpack" => LocalizationService.Instance["Nav_Unpack"],
            "Pack" => LocalizationService.Instance["Nav_Pack"],
            "Convert" => LocalizationService.Instance["Nav_Convert"],
            "Multi" => LocalizationService.Instance["Nav_Multi"],
            "Homebrew" => LocalizationService.Instance["Nav_Homebrew"],
            "Verify" => LocalizationService.Instance["Nav_Verify"],
            _ => LocalizationService.Instance.TranslateText(Operation)
        };

        public void NotifyLocalizationChanged()
        {
            OnPropertyChanged(nameof(OperationDisplay));
            OnPropertyChanged(nameof(StatusDisplay));
            if (SourceSizeBytes > 0)
            {
                SourceSize = FormatSize(SourceSizeBytes);
            }
            OnPropertyChanged(nameof(SourceSize));
            OnPropertyChanged(nameof(TargetSize));
            OnPropertyChanged(nameof(FinishedAtDisplay));
        }

        partial void OnOperationChanged(string value) => OnPropertyChanged(nameof(OperationDisplay));


        /// <summary>Форматирование размера: 2 320,21 МБ</summary>
        public static string FormatSize(long bytes)
        {
            return App.Localization.FormatSize(bytes);
        }

        partial void OnSourceSizeBytesChanged(long value)
        {
            SourceSize = FormatSize(value);
        }

        // Кастомные метаданные (иконка, название игры, издатель)
        [System.Text.Json.Serialization.JsonIgnore]
        public string PosterUrl { get; set; } = string.Empty;
        public GameMetadataEditModel? CustomMetadata { get; set; }

        public void Pause()
        {
            if (!IsRunning || IsPaused) return;
            IsPaused = true;
            PauseEvent.Reset();
            Status = "⏸ Сохранено на паузе";
            Speed = string.Empty;
            try
            {
                OnPauseRequested?.Invoke();
                if (ActiveTorrentManager != null && ActiveTorrentManager.State == MonoTorrent.Client.TorrentState.Downloading)
                {
                    _ = ActiveTorrentManager.PauseAsync();
                }
            }
            catch { }
        }

        public void Resume()
        {
            if (!IsRunning || !IsPaused) return;
            IsPaused = false;
            PauseEvent.Set();
            Status = "Возобновление...";
            try
            {
                OnResumeRequested?.Invoke();
                if (ActiveTorrentManager != null && ActiveTorrentManager.State == MonoTorrent.Client.TorrentState.Paused)
                {
                    _ = ActiveTorrentManager.StartAsync();
                }
            }
            catch { }
        }

        public void Cancel()
        {
            try
            {
                OnCancelRequested?.Invoke();
                PauseEvent.Set();
                if (ActiveTorrentManager != null)
                {
                    _ = ActiveTorrentManager.StopAsync();
                }
                Cts?.Cancel();
            }
            catch { }
            Status = "Отменено";
            Speed = string.Empty;
            IsRunning = false;
            IsPaused = false;
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public SolidColorBrush StatusColor
        {
            get
            {
                if (Status == "Успешно" || Status == "Готово" || Status == "Корректна" || Status == "Ок" || Status == "Да" || Status == "Да (Гибрид)") 
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 46, 204, 113)); // Emerald Green
                if (Status == "Ошибка" || Status == "Поврежден" || Status.StartsWith("Нет") || Status.Contains("ошибка") || Status.Contains("Ошибка")) 
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 231, 76, 60)); // Alizarin Red
                if (Status == "Пауза") return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 245, 158, 11)); // Amber/Orange for Pause
                if (Status == "Отменен" || Status == "Отменено" || Status.Contains("Предупреждение")) 
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 243, 156, 18)); // Orange
                if (Status == "Ожидание") 
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 149, 165, 166)); // Asbestos Gray
                return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 52, 152, 219)); // Peter River Blue
            }
        }

        partial void OnStatusChanged(string value)
        {
            OnPropertyChanged(nameof(StatusDisplay));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(IsCompleted));
            OnPropertyChanged(nameof(CanChangeFormat));
            OnPropertyChanged(nameof(TargetFormatColor));
            OnPropertyChanged(nameof(TargetFormatWeight));
            OnPropertyChanged(nameof(FormatCompletedVisibility));
            OnPropertyChanged(nameof(FormatSelectableVisibility));
        }

        protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
        {
            var dq = App.MainDispatcher;
            if (dq != null && !dq.HasThreadAccess)
            {
                dq.TryEnqueue(() =>
                {
                    try { base.OnPropertyChanged(e); } catch { }
                });
                return;
            }
            try { base.OnPropertyChanged(e); } catch { }
        }
    
        public static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            var invalid = System.IO.Path.GetInvalidFileNameChars();
            var sb = new System.Text.StringBuilder(name.Length);
            foreach (char c in name)
            {
                sb.Append(invalid.Contains(c) ? '_' : c);
            }
            return sb.ToString().Trim();
        }

        public static long TryParseFormattedSizeToBytes(string? formatted)
        {
            if (string.IsNullOrWhiteSpace(formatted)) return 0;
            string clean = formatted.Trim().Replace('\u00A0', ' ');
            if (clean == "-" || clean == "0" || clean == "0 Б" || clean == "0 B") return 0;

            long multiplier = 1;
            string numPart = clean;

            if (clean.EndsWith("ТБ", StringComparison.OrdinalIgnoreCase) || clean.EndsWith("TB", StringComparison.OrdinalIgnoreCase))
            {
                multiplier = 1024L * 1024 * 1024 * 1024;
                numPart = clean.Substring(0, clean.Length - 2).Trim();
            }
            else if (clean.EndsWith("ГБ", StringComparison.OrdinalIgnoreCase) || clean.EndsWith("GB", StringComparison.OrdinalIgnoreCase))
            {
                multiplier = 1024L * 1024 * 1024;
                numPart = clean.Substring(0, clean.Length - 2).Trim();
            }
            else if (clean.EndsWith("МБ", StringComparison.OrdinalIgnoreCase) || clean.EndsWith("MB", StringComparison.OrdinalIgnoreCase))
            {
                multiplier = 1024L * 1024;
                numPart = clean.Substring(0, clean.Length - 2).Trim();
            }
            else if (clean.EndsWith("КБ", StringComparison.OrdinalIgnoreCase) || clean.EndsWith("KB", StringComparison.OrdinalIgnoreCase))
            {
                multiplier = 1024L;
                numPart = clean.Substring(0, clean.Length - 2).Trim();
            }
            else if (clean.EndsWith("Bytes", StringComparison.OrdinalIgnoreCase))
            {
                multiplier = 1;
                numPart = clean.Substring(0, clean.Length - 5).Trim();
            }
            else if (clean.EndsWith("Б", StringComparison.OrdinalIgnoreCase) || clean.EndsWith("B", StringComparison.OrdinalIgnoreCase))
            {
                multiplier = 1;
                numPart = clean.Substring(0, clean.Length - 1).Trim();
            }

            numPart = numPart.Replace(" ", "").Replace(",", ".");
            if (double.TryParse(numPart, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double val))
            {
                return (long)(val * multiplier);
            }
            return 0;
        }

        public static long CalculatePathsTotalBytes(IEnumerable<string>? paths)
        {
            if (paths == null) return 0;
            long total = 0;
            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                try
                {
                    if (System.IO.File.Exists(path))
                    {
                        total += new System.IO.FileInfo(path).Length;
                    }
                    else if (System.IO.Directory.Exists(path))
                    {
                        foreach (var f in System.IO.Directory.EnumerateFiles(path, "*.*", System.IO.SearchOption.AllDirectories))
                        {
                            try
                            {
                                total += new System.IO.FileInfo(f).Length;
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
            return total;
        }

        public static string ComputeDefaultCompressionLevel(string? targetFormat, bool isSwitch2)
        {
            if (isSwitch2 || string.Equals(targetFormat, "NUP", StringComparison.OrdinalIgnoreCase))
            {
                int lvl = App.Settings?.Current?.Switch2CompressionLevel ?? 18;
                return lvl > 0 ? lvl.ToString() : "18";
            }

            if (string.IsNullOrWhiteSpace(targetFormat))
            {
                int lvl = App.Settings?.Current?.CompressionLevel ?? 22;
                return lvl > 0 ? lvl.ToString() : "22";
            }

            string fmt = targetFormat.Trim().ToUpperInvariant();
            if (fmt.Contains("NSZ") || fmt.Contains("XCZ"))
            {
                int lvl = App.Settings?.Current?.CompressionLevel ?? 22;
                return lvl > 0 ? lvl.ToString() : "22";
            }

            if (fmt == "NSP" || fmt == "XCI" || fmt == "3DS" || fmt == "CIA" || fmt == "CXI")
            {
                return "-";
            }

            int defaultLvl = App.Settings?.Current?.CompressionLevel ?? 22;
            return defaultLvl > 0 ? defaultLvl.ToString() : "22";
        }

        public void EnsureSourceMetricsPopulated()
        {
            // 1. SourceSizeBytes
            if (SourceSizeBytes <= 0)
            {
                if (InputFiles != null && InputFiles.Count > 0)
                {
                    SourceSizeBytes = CalculatePathsTotalBytes(InputFiles);
                }

                if (SourceSizeBytes <= 0 && RuTrackerPayload != null)
                {
                    if (!string.IsNullOrWhiteSpace(RuTrackerPayload.TargetReleaseDir) && System.IO.Directory.Exists(RuTrackerPayload.TargetReleaseDir))
                    {
                        var matchingFiles = new List<string>();
                        if (RuTrackerPayload.TorrentFiles != null && RuTrackerPayload.TorrentFiles.Count > 0)
                        {
                            var selected = RuTrackerPayload.TorrentFiles.Where(f => f.IsSelected).ToList();
                            var listToMatch = selected.Count > 0 ? selected : RuTrackerPayload.TorrentFiles;
                            foreach (var tf in listToMatch)
                            {
                                string candidate = System.IO.Path.Combine(RuTrackerPayload.TargetReleaseDir, tf.RelativePath);
                                if (System.IO.File.Exists(candidate)) matchingFiles.Add(candidate);
                            }
                        }
                        if (matchingFiles.Count > 0)
                        {
                            SourceSizeBytes = CalculatePathsTotalBytes(matchingFiles);
                        }
                        else
                        {
                            SourceSizeBytes = CalculatePathsTotalBytes(new[] { RuTrackerPayload.TargetReleaseDir });
                        }
                    }

                    if (SourceSizeBytes <= 0 && RuTrackerPayload.TorrentFiles != null && RuTrackerPayload.TorrentFiles.Count > 0)
                    {
                        var selected = RuTrackerPayload.TorrentFiles.Where(f => f.IsSelected && f.Length > 0).ToList();
                        SourceSizeBytes = selected.Count > 0
                            ? selected.Sum(f => f.Length)
                            : RuTrackerPayload.TorrentFiles.Where(f => f.Length > 0).Sum(f => f.Length);
                    }
                }

                if (SourceSizeBytes <= 0 && !string.IsNullOrWhiteSpace(SourceSize))
                {
                    SourceSizeBytes = TryParseFormattedSizeToBytes(SourceSize);
                }
            }

            // 2. SourceSize
            if ((string.IsNullOrWhiteSpace(SourceSize) || SourceSize == "-") && SourceSizeBytes > 0)
            {
                SourceSize = FormatSize(SourceSizeBytes);
            }

            // 3. InputFolders
            if (string.IsNullOrWhiteSpace(InputFolders))
            {
                if (InputFiles != null && InputFiles.Count > 0)
                {
                    var dirs = InputFiles
                        .Select(p => System.IO.Directory.Exists(p) ? p : (System.IO.Path.GetDirectoryName(p) ?? ""))
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .Distinct(StringComparer.OrdinalIgnoreCase);
                    InputFolders = string.Join("; ", dirs);
                }
                else if (RuTrackerPayload != null)
                {
                    if (!string.IsNullOrWhiteSpace(RuTrackerPayload.TargetReleaseDir))
                        InputFolders = RuTrackerPayload.TargetReleaseDir;
                    else if (!string.IsNullOrWhiteSpace(RuTrackerPayload.SharedReleaseDir))
                        InputFolders = RuTrackerPayload.SharedReleaseDir;
                }
            }

            // 4. FilesCount
            if (string.IsNullOrWhiteSpace(FilesCount) || FilesCount == "0" || FilesCount == "0 файл(ов)")
            {
                if (InputFiles != null && InputFiles.Count > 0)
                {
                    FilesCount = $"{InputFiles.Count} файл(ов)";
                }
                else if (RuTrackerPayload?.TorrentFiles != null && RuTrackerPayload.TorrentFiles.Count > 0)
                {
                    int selCount = RuTrackerPayload.TorrentFiles.Count(f => f.IsSelected);
                    int count = selCount > 0 ? selCount : RuTrackerPayload.TorrentFiles.Count;
                    int clouds = RuTrackerPayload.CloudLinks?.Count ?? 0;
                    FilesCount = $"{count} файл(ов)" + (clouds > 0 ? $" + {clouds} мод." : "");
                }
            }

            // 5. CompressionLevel
            if (string.IsNullOrWhiteSpace(CompressionLevel) || CompressionLevel == "-")
            {
                CompressionLevel = ComputeDefaultCompressionLevel(TargetFormat, IsSwitch2Task);
            }
        }

        public void EnsureFinalMetricsPopulated(string? explicitOutputPath = null)
        {
            EnsureSourceMetricsPopulated();

            string outDir = !string.IsNullOrWhiteSpace(OutputFolder)
                ? OutputFolder
                : (!string.IsNullOrWhiteSpace(App.Settings?.Current?.OutputFolder)
                    ? App.Settings.Current.OutputFolder
                    : (RuTrackerPayload?.TargetReleaseDir ?? ""));

            bool isDual = IsDualFormat || (TargetFormat != null && TargetFormat.Contains("+"));

            string? primaryOut = null;
            string? secondaryOut = null;

            if (!string.IsNullOrWhiteSpace(explicitOutputPath) && System.IO.File.Exists(explicitOutputPath))
            {
                primaryOut = explicitOutputPath;
            }

            if (isDual)
            {
                string pExt = GetPrimaryExtension();
                string sExt = GetSecondaryExtension();
                if (!string.IsNullOrEmpty(outDir) && System.IO.Directory.Exists(outDir) && !string.IsNullOrEmpty(OutputFileName))
                {
                    string pCandidate = System.IO.Path.Combine(outDir, OutputFileName + pExt);
                    string sCandidate = System.IO.Path.Combine(outDir, OutputFileName + sExt);
                    if (System.IO.File.Exists(pCandidate)) primaryOut ??= pCandidate;
                    if (System.IO.File.Exists(sCandidate)) secondaryOut = sCandidate;
                }
            }

            if (primaryOut == null && !string.IsNullOrEmpty(outDir) && System.IO.Directory.Exists(outDir))
            {
                string pExt = GetPrimaryExtension();
                if (!string.IsNullOrEmpty(OutputFileName))
                {
                    string candidate = System.IO.Path.Combine(outDir, OutputFileName + pExt);
                    if (System.IO.File.Exists(candidate)) primaryOut = candidate;

                    if (primaryOut == null)
                    {
                        string san = SanitizeFileName(OutputFileName);
                        string sanCandidate = System.IO.Path.Combine(outDir, san + pExt);
                        if (System.IO.File.Exists(sanCandidate)) primaryOut = sanCandidate;
                    }
                }

                if (primaryOut == null)
                {
                    string tid = !string.IsNullOrEmpty(TitleId) ? TitleId : (GroupId ?? "");
                    var candidates = System.IO.Directory.GetFiles(outDir, "*.*").Where(f =>
                        f.EndsWith(".nsp", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".nsz", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".xci", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".xcz", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".3ds", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".cia", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".nup", StringComparison.OrdinalIgnoreCase)).ToList();

                    primaryOut = candidates.FirstOrDefault(f =>
                        (!string.IsNullOrWhiteSpace(OutputFileName) && System.IO.Path.GetFileNameWithoutExtension(f).Contains(OutputFileName, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(GameName) && System.IO.Path.GetFileNameWithoutExtension(f).Contains(GameName, StringComparison.OrdinalIgnoreCase)) ||
                        (!string.IsNullOrWhiteSpace(tid) && f.Contains(tid, StringComparison.OrdinalIgnoreCase)));
                }
            }

            if (primaryOut != null && System.IO.File.Exists(primaryOut))
            {
                long pSize = new System.IO.FileInfo(primaryOut).Length;
                if (isDual && secondaryOut != null && System.IO.File.Exists(secondaryOut))
                {
                    long sSize = new System.IO.FileInfo(secondaryOut).Length;
                    TargetSize = $"{FormatSize(pSize)} + {FormatSize(sSize)}";

                    long compSize = Math.Min(pSize, sSize);
                    if (SourceSizeBytes > 0 && compSize > 0)
                    {
                        long diff = SourceSizeBytes - compSize;
                        double percent = (double)diff / SourceSizeBytes * 100.0;
                        SizeDifference = $"{(diff > 0 ? "-" : "+")}{FormatSize(Math.Abs(diff))} ({Math.Abs(percent):F1}%)";
                    }
                }
                else
                {
                    TargetSize = FormatSize(pSize);
                    if (SourceSizeBytes > 0 && pSize > 0)
                    {
                        long diff = SourceSizeBytes - pSize;
                        double percent = (double)diff / SourceSizeBytes * 100.0;
                        SizeDifference = $"{(diff > 0 ? "-" : "+")}{FormatSize(Math.Abs(diff))} ({Math.Abs(percent):F1}%)";
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(CompressionLevel) || CompressionLevel == "-")
            {
                CompressionLevel = ComputeDefaultCompressionLevel(TargetFormat, IsSwitch2Task);
            }
        }

    }
    public sealed class RuTrackerTaskPayload
    {
        public string PosterUrl { get; set; } = string.Empty;
        public RuTrackerTopicItem? Topic { get; set; }
        public byte[]? TorrentBytes { get; set; }
        public MonoTorrent.MagnetLink? MagnetLink { get; set; }
        public List<TorrentFileSelectionItem> TorrentFiles { get; set; } = new();
        public List<string> CloudLinks { get; set; } = new();
        public string TargetReleaseDir { get; set; } = string.Empty;
        public string CustomModTitle { get; set; } = string.Empty;
        public bool AutoBuild { get; set; } = true;
        public bool HasTorrentSource { get; set; } = true;
        public int SlotNumber { get; set; } = 0;
        public int TotalSlots { get; set; } = 1;
        public string SlotGameTitle { get; set; } = string.Empty;
        public string SharedReleaseDir { get; set; } = string.Empty;
        public bool AppendAutoTags { get; set; } = false;
        public GameMetadataEditModel? CustomMetadata { get; set; }
    }

    public sealed class PersistedTaskSnapshot
    {
        public string Id { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public string SourceFormat { get; set; } = string.Empty;
        public string TargetFormat { get; set; } = "NSP";
        public string Status { get; set; } = "⏸ Сохранено на паузе";
        public double Progress { get; set; }
        public string SourceSize { get; set; } = string.Empty;
        public string TargetSize { get; set; } = string.Empty;
        public string OutputFolder { get; set; } = string.Empty;
        public string OutputFileName { get; set; } = string.Empty;
        public string LogDetails { get; set; } = string.Empty;
        public string GroupId { get; set; } = string.Empty;
        public List<string> InputFiles { get; set; } = new();
        public List<string> FilesList { get; set; } = new();
        public bool IsRuTrackerTask { get; set; }
        public bool KeepExactOutputFileName { get; set; }
        public bool DeleteSourceAfterVerify { get; set; }
        public RuTrackerTaskSnapshot? RuTrackerSnapshot { get; set; }

        public long SourceSizeBytes { get; set; }
        public string SizeDifference { get; set; } = string.Empty;
        public string CompressionLevel { get; set; } = string.Empty;
        public string FilesCount { get; set; } = string.Empty;
        public string InputFolders { get; set; } = string.Empty;
        public string TitleId { get; set; } = string.Empty;
        public bool Is3dsTask { get; set; }
        public bool IsSwitch2Task { get; set; }
        public int BuildMode { get; set; }
        public string HasRomFs { get; set; } = "-";
        public string HasExeFs { get; set; } = "-";
        public string HasSaveData { get; set; } = "-";
        public string SaveDataFolder { get; set; } = string.Empty;
        public int SaveFilesCount { get; set; }
        public string ModNameRomFs { get; set; } = string.Empty;
        public string ModNameExeFs { get; set; } = string.Empty;
        public byte[]? IconBytes { get; set; }
        public GameMetadataEditModel? CustomMetadata { get; set; }
    }

    public sealed class RuTrackerTaskSnapshot
    {
        public string TopicId { get; set; } = string.Empty;
        public string TopicTitle { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int Seeds { get; set; }
        public string MagnetUri { get; set; } = string.Empty;
        public string InfoHash { get; set; } = string.Empty;
        public string DirectTorrentUrl { get; set; } = string.Empty;
        public byte[]? TorrentBytes { get; set; }
        public List<PersistedTorrentFileItem> TorrentFiles { get; set; } = new();
        public List<string> CloudLinks { get; set; } = new();
        public string TargetReleaseDir { get; set; } = string.Empty;
        public string CustomModTitle { get; set; } = string.Empty;
        public bool AutoBuild { get; set; } = true;
        public bool HasTorrentSource { get; set; } = true;
        public string SharedReleaseDir { get; set; } = string.Empty;
        public bool AppendAutoTags { get; set; } = false;
        public int SlotNumber { get; set; } = 0;
        public int TotalSlots { get; set; } = 1;
        public string SlotGameTitle { get; set; } = string.Empty;
        public string PosterUrl { get; set; } = string.Empty;
    }

    public sealed class PersistedTorrentFileItem
    {
        public string RelativePath { get; set; } = string.Empty;
        public long Length { get; set; }
        public bool IsSelected { get; set; }
        public string CategoryBadge { get; set; } = "ФАЙЛ";
        public bool IsRealTorrentFile { get; set; }
    }

}
