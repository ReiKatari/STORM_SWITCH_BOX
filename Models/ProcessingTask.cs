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
        [ObservableProperty] private string _gameName = string.Empty;
        [ObservableProperty] private string _operation = string.Empty;
        [ObservableProperty] private string _sourceFormat = string.Empty;
        [ObservableProperty] private string _targetFormat = string.Empty;
        [ObservableProperty] private string _sourceSize = string.Empty;
        [ObservableProperty] private string _targetSize = string.Empty;
        [ObservableProperty] private string _sizeDifference = string.Empty;
        [ObservableProperty] private string _compressionLevel = string.Empty;
        [ObservableProperty] private string _filesCount = string.Empty;
        [ObservableProperty] private string _status = string.Empty;
        [ObservableProperty] private double _progress;
        [ObservableProperty] private bool _isRunning;
        [ObservableProperty] private DateTime _finishedAt = DateTime.Now;

        [System.Text.Json.Serialization.JsonIgnore]
        public System.Threading.CancellationTokenSource? Cts { get; set; }
        
        [System.Text.Json.Serialization.JsonIgnore]
        public string FinishedAtDisplay => FinishedAt.ToString("dd.MM.yyyy HH:mm");
        partial void OnFinishedAtChanged(DateTime value) => OnPropertyChanged(nameof(FinishedAtDisplay));
        
        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsNotRunning => !IsRunning;
        partial void OnIsRunningChanged(bool value) => OnPropertyChanged(nameof(IsNotRunning));

        [ObservableProperty] private long _sourceSizeBytes;
        [ObservableProperty] private string _hasRomFs = "-";
        [ObservableProperty] private string _hasExeFs = "-";
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

        /// <summary>Видимость панели деталей (привязка к IsExpanded)</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public Visibility DetailsVisibility => IsExpanded ? Visibility.Visible : Visibility.Collapsed;

        partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(DetailsVisibility));

        /// <summary>Локализованное название обработки для отображения в таблице</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string OperationDisplay => Operation switch
        {
            "Update" => "Обновление",
            "Unpack" => "Распаковка",
            "Pack" => "Упаковка",
            "Convert" => "Конвертация",
            "Multi" => "Мульти-контент",
            "Verify" => "Проверка",
            _ => Operation
        };

        partial void OnOperationChanged(string value) => OnPropertyChanged(nameof(OperationDisplay));


        /// <summary>Форматирование размера: 2 320,21 МБ</summary>
        public static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "-";

            double value;
            string unit;

            if (bytes >= 1_073_741_824L)
            {
                value = bytes / 1_073_741_824.0;
                unit = "ГБ";
            }
            else if (bytes >= 1_048_576L)
            {
                value = bytes / 1_048_576.0;
                unit = "МБ";
            }
            else if (bytes >= 1024L)
            {
                value = bytes / 1024.0;
                unit = "КБ";
            }
            else
            {
                return $"{bytes} Б";
            }

            string formatted = value.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("ru-RU"));
            formatted = formatted.Replace('\u00A0', ' ');
            return $"{formatted} {unit}";
        }

        partial void OnSourceSizeBytesChanged(long value)
        {
            SourceSize = FormatSize(value);
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
                if (Status == "Отменен" || Status.Contains("Предупреждение")) 
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 243, 156, 18)); // Orange
                if (Status == "Ожидание") 
                    return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 149, 165, 166)); // Asbestos Gray
                return new SolidColorBrush(Windows.UI.Color.FromArgb(255, 52, 152, 219)); // Peter River Blue
            }
        }

        partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(StatusColor));
    }
}
