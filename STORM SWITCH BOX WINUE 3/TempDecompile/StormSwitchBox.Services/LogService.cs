using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services;

public class LogService
{
	private readonly DispatcherQueue _dispatcherQueue;

	private readonly string _logFilePath;

	private readonly Channel<LogMessage> _logChannel;

	public ObservableCollection<LogMessage> Logs { get; } = new ObservableCollection<LogMessage>();

	public LogService()
	{
		_dispatcherQueue = DispatcherQueue.GetForCurrentThread();
		string text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
		if (!Directory.Exists(text))
		{
			Directory.CreateDirectory(text);
		}
		_logFilePath = Path.Combine(text, $"session_{DateTime.Now:yyyyMMdd_HHmmss}.log");
		_logChannel = Channel.CreateUnbounded<LogMessage>(new UnboundedChannelOptions
		{
			SingleReader = true
		});
		ProcessLogQueueAsync();
	}

	private async Task ProcessLogQueueAsync()
	{
		await foreach (LogMessage log in _logChannel.Reader.ReadAllAsync())
		{
			try
			{
				await File.AppendAllTextAsync(_logFilePath, $"[{log.FormattedTime}] [{log.Level}] {log.Message}\n");
			}
			catch
			{
			}
		}
	}

	public void Log(string message, LogLevel level = LogLevel.Info)
	{
		LogMessage log = new LogMessage
		{
			Message = message,
			Level = level
		};
		_logChannel.Writer.TryWrite(log);
		_dispatcherQueue?.TryEnqueue(delegate
		{
			Logs.Add(log);
			if (Logs.Count > 1000)
			{
				Logs.RemoveAt(0);
			}
		});
	}
}
