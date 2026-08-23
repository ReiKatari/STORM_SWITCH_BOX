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
            }
        }

        public void Flush()
        {
            string chunk;
            lock (_lock)
            {
                if (_buffer.Length == 0) return;
                chunk = _buffer.ToString();
                _buffer.Clear();
            }

            App.RunOnUI(() =>
            {
                if (_task.LogDetails != null && _task.LogDetails.Length > 150_000)
                {
                    _task.LogDetails = "..." + _task.LogDetails.Substring(_task.LogDetails.Length - 80_000);
                }
                _task.LogDetails += chunk;
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
