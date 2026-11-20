using System;
using System.Globalization;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public sealed class LogBatcher : IDisposable
    {
        private static readonly Regex PercentRegex = new(@"(\d{1,3}(?:\.\d+)?)\s*%", RegexOptions.Compiled);

        private readonly ProcessingTask _task;
        private readonly StringBuilder _buffer = new();
        private readonly object _lock = new();
        private readonly System.Timers.Timer _timer;
        private string? _latestStatus;
        private double? _latestProgress;
        private bool _completed;
        private int _uiFlushPending;

        public LogBatcher(ProcessingTask task, int intervalMs = 120)
        {
            _task = task;
            _timer = new System.Timers.Timer(intervalMs);
            _timer.Elapsed += (_, _) => Flush();
            _timer.Start();
        }

        public void AppendLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;
            lock (_lock)
            {
                if (_completed) return;
                _buffer.AppendLine(line);

                if (line.Contains("Creating Section 0", StringComparison.OrdinalIgnoreCase) ||
                    (line.Contains("RomFS", StringComparison.OrdinalIgnoreCase) && line.Contains("Creating", StringComparison.OrdinalIgnoreCase)))
                {
                    _latestStatus = "Создание секции RomFS...";
                }
                else if (line.Contains("Writing NCA body", StringComparison.OrdinalIgnoreCase) || line.Contains("Writing Section", StringComparison.OrdinalIgnoreCase))
                {
                    _latestStatus = "Запись тела NCA архива...";
                }
                else if (line.Contains("Creating Section 1", StringComparison.OrdinalIgnoreCase) ||
                         (line.Contains("ExeFS", StringComparison.OrdinalIgnoreCase) && line.Contains("Creating", StringComparison.OrdinalIgnoreCase)))
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

                var match = PercentRegex.Match(line);
                if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double p) && p >= 0 && p <= 100)
                {
                    _latestProgress = p;
                }
            }
        }

        public void Flush(bool allowWhenCompleting = false)
        {
            if (_task == null) return;
            if (!allowWhenCompleting && Interlocked.CompareExchange(ref _uiFlushPending, 1, 0) != 0)
            {
                return;
            }

            string chunk;
            string? statusToSet;
            double? progressToSet;

            lock (_lock)
            {
                if (_completed && !allowWhenCompleting)
                {
                    Volatile.Write(ref _uiFlushPending, 0);
                    return;
                }
                chunk = _buffer.ToString();
                _buffer.Clear();
                statusToSet = _latestStatus;
                _latestStatus = null;
                progressToSet = _latestProgress;
                _latestProgress = null;
            }

            if (chunk.Length == 0 && statusToSet == null && !progressToSet.HasValue)
            {
                Volatile.Write(ref _uiFlushPending, 0);
                return;
            }

            App.RunOnUI(() =>
            {
                try
                {
                    if (_task == null) return;
                    if (statusToSet != null) _task.Status = statusToSet;
                    if (progressToSet.HasValue) _task.Progress = progressToSet.Value;

                    if (chunk.Length > 0)
                    {
                        string existing = _task.LogDetails ?? string.Empty;
                        if (existing.Length > 80_000)
                            existing = "..." + existing.Substring(existing.Length - 40_000);
                        _task.LogDetails = existing + chunk;
                    }
                }
                finally
                {
                    Volatile.Write(ref _uiFlushPending, 0);
                }
            });
        }

        public void Complete()
        {
            lock (_lock)
            {
                if (_completed) return;
                _completed = true;
            }
            try { _timer.Stop(); } catch { }
            try { _timer.Dispose(); } catch { }
            Flush(allowWhenCompleting: true);
        }

        public void Dispose() => Complete();
    }
}
