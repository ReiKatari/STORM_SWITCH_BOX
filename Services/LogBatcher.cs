using System;
using System.Text;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    /// <summary>
    /// Высокопроизводительный сервисный буферизатор логов.
    /// Накапливает консольный вывод и отправляет его в UI-поток пакетами, исключая фризы интерфейса.
    /// </summary>
    public class LogBatcher
    {
        private readonly ProcessingTask _task;
        private readonly StringBuilder _buffer = new();
        private readonly object _lock = new();
        private readonly System.Timers.Timer _timer;
        private string? _latestStatus;
        private double? _latestProgress;

        public LogBatcher(ProcessingTask task, int intervalMs = 150)
        {
            _task = task;
            _timer = new System.Timers.Timer(intervalMs);
            _timer.Elapsed += (s, e) => Flush();
            _timer.Start();
        }

        public void AppendLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            lock (_lock)
            {
                _buffer.AppendLine(line);

                // Интеллектуальный анализ прогресса и статуса утилит (hacpack, yanu, nsz, 3dstool, makerom)
                if (line.Contains("Creating Section 0", StringComparison.OrdinalIgnoreCase) || line.Contains("RomFS", StringComparison.OrdinalIgnoreCase) && line.Contains("Creating", StringComparison.OrdinalIgnoreCase))
                {
                    _latestStatus = "Создание секции RomFS...";
                }
                else if (line.Contains("Writing NCA body", StringComparison.OrdinalIgnoreCase) || line.Contains("Writing Section", StringComparison.OrdinalIgnoreCase))
                {
                    _latestStatus = "Запись тела NCA архива...";
                }
                else if (line.Contains("Creating Section 1", StringComparison.OrdinalIgnoreCase) || line.Contains("ExeFS", StringComparison.OrdinalIgnoreCase) && line.Contains("Creating", StringComparison.OrdinalIgnoreCase))
                {
                    _latestStatus = "Создание секции ExeFS...";
                }
                else if (line.Contains("Creating PFS0", StringComparison.OrdinalIgnoreCase) || line.Contains("Packing NSP", StringComparison.OrdinalIgnoreCase))
                {
                    _latestStatus = "Сборка контейнера PFS0 (NSP)...";
                }
                else if (line.Contains("Compressing", StringComparison.OrdinalIgnoreCase))
                {
                    _latestStatus = "Сжатие архива (Zstandard)...";
                }
                else if (line.Contains("Decompressing", StringComparison.OrdinalIgnoreCase))
                {
                    _latestStatus = "Распаковка контейнера...";
                }
                else if (line.Contains("Extracting", StringComparison.OrdinalIgnoreCase))
                {
                    _latestStatus = "Извлечение ресурсов...";
                }

                // Поиск процента выполнения в выводе (напр. "45%" или "78.5%")
                var match = System.Text.RegularExpressions.Regex.Match(line, @"\b(\d{1,3}(?:\.\d+)?)\s*%\b");
                if (match.Success && double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double p))
                {
                    if (p >= 0 && p <= 100)
                    {
                        _latestProgress = p;
                    }
                }
            }
        }

        public void Flush()
        {
            if (_task == null) return;

            string chunk;
            string? statusToSet = null;
            double? progressToSet = null;

            lock (_lock)
            {
                chunk = _buffer.ToString();
                _buffer.Clear();
                statusToSet = _latestStatus;
                _latestStatus = null;
                progressToSet = _latestProgress;
                _latestProgress = null;
            }

            if (chunk.Length == 0 && statusToSet == null && !progressToSet.HasValue) return;

            App.RunOnUI(() =>
            {
                if (_task == null) return;
                
                if (statusToSet != null)
                {
                    _task.Status = statusToSet;
                }

                if (progressToSet.HasValue)
                {
                    _task.Progress = progressToSet.Value;
                }

                if (chunk.Length > 0)
                {
                    if (_task.LogDetails != null && _task.LogDetails.Length > 150_000)
                    {
                        _task.LogDetails = "..." + _task.LogDetails.Substring(_task.LogDetails.Length - 80_000);
                    }
                    _task.LogDetails = (_task.LogDetails ?? "") + chunk;
                }
            });
        }

        public void Complete()
        {
            _timer.Stop();
            _timer.Dispose();
            Flush();
        }
    }
}
