using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public sealed class LogService : IDisposable
    {
        public ObservableCollection<LogMessage> Logs { get; } = new();
        public string LogsDirectory { get; }

        private readonly string _logFilePath;
        private readonly Channel<LogMessage> _logChannel;
        private bool _disposed;

        public LogService()
        {
            LogsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StormSwitchBox", "logs");
            try { Directory.CreateDirectory(LogsDirectory); } catch { }
            PruneOldSessionLogs();

            _logFilePath = Path.Combine(LogsDirectory, $"session_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log");
            _logChannel = Channel.CreateBounded<LogMessage>(new BoundedChannelOptions(4096)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true
            });

            _ = Task.Run(ProcessLogQueueAsync);
        }

        private void PruneOldSessionLogs()
        {
            try
            {
                var cutoff = DateTime.UtcNow.AddDays(-14);
                foreach (var file in Directory.EnumerateFiles(LogsDirectory, "session_*.log"))
                {
                    try
                    {
                        if (File.GetLastWriteTimeUtc(file) < cutoff)
                            File.Delete(file);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private async Task ProcessLogQueueAsync()
        {
            StreamWriter? writer = null;
            try
            {
                var fs = new FileStream(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 4096, useAsync: true);
                writer = new StreamWriter(fs, Encoding.UTF8) { AutoFlush = false };

                while (await _logChannel.Reader.WaitToReadAsync().ConfigureAwait(false))
                {
                    while (_logChannel.Reader.TryRead(out var log))
                    {
                        await writer.WriteLineAsync($"[{log.FormattedTime}] [{log.Level}] {log.Message}").ConfigureAwait(false);
                    }
                    await writer.FlushAsync().ConfigureAwait(false);
                }
            }
            catch { }
            finally
            {
                try { writer?.Dispose(); } catch { }
            }
        }

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            if (_disposed) return;
            var log = new LogMessage { Message = message, Level = level };
            _logChannel.Writer.TryWrite(log);

            if (App.MainDispatcher != null)
            {
                App.RunOnUI(() =>
                {
                    Logs.Add(log);
                    if (Logs.Count > 1000)
                        Logs.RemoveAt(0);
                });
            }
        }

        public void Clear()
        {
            App.RunOnUI(() => Logs.Clear());
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _logChannel.Writer.TryComplete();
        }
    }
}
