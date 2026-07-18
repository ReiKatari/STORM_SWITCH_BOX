using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public class LogService
    {
        public ObservableCollection<LogMessage> Logs { get; } = new ObservableCollection<LogMessage>();
        private readonly DispatcherQueue? _dispatcherQueue;
        private readonly string _logFilePath;
        private readonly Channel<LogMessage> _logChannel;
        public LogService()
        {
            try
            {
                _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            }
            catch
            {
                _dispatcherQueue = null;
            }
            
            var logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!Directory.Exists(logsDir))
            {
                Directory.CreateDirectory(logsDir);
            }
            
            _logFilePath = Path.Combine(logsDir, $"session_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            
            _logChannel = Channel.CreateUnbounded<LogMessage>(new UnboundedChannelOptions { SingleReader = true });
            _ = ProcessLogQueueAsync();
        }

        private async Task ProcessLogQueueAsync()
        {
            await foreach (var log in _logChannel.Reader.ReadAllAsync())
            {
                try
                {
                    await File.AppendAllTextAsync(_logFilePath, $"[{log.FormattedTime}] [{log.Level}] {log.Message}\n");
                }
                catch { }
            }
        }

        public void Log(string message, LogLevel level = LogLevel.Info)
        {
            var log = new LogMessage { Message = message, Level = level };
            
            _logChannel.Writer.TryWrite(log);
            
            // Output to UI
            _dispatcherQueue?.TryEnqueue(() =>
            {
                Logs.Add(log);
                
                if (Logs.Count > 1000)
                {
                    Logs.RemoveAt(0);
                }
            });
        }
    }
}
