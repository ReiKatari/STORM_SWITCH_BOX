using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    /// <summary>
    /// Универсальный сервисный движок выполнения внешних консольных утилит (yanu-cli, squirrel, nsz, hacpack).
    /// Гарантирует Юникод UTF-8, безопасную работу в фоновом режиме и сжатие логов через LogBatcher.
    /// </summary>
    public static class ExternalProcessRunner
    {
        public static async Task<int> RunAsync(
            string fileName,
            string arguments,
            string workingDirectory,
            ProcessingTask task,
            CancellationToken cancellationToken,
            string? isolatedUserProfile = null,
            string? isolatedLocalAppData = null)
        {
            var batcher = new LogBatcher(task);

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            // Гарантия Юникода для Python и утилит (включая PyInstaller-замороженные .exe)
            psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8:surrogateescape";
            psi.EnvironmentVariables["PYTHONUTF8"] = "1";
            psi.EnvironmentVariables["PYTHONLEGACYWINDOWSSTDIO"] = "0";
            psi.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
            psi.EnvironmentVariables["PYTHONCOERCECLOCALE"] = "1";

            if (!string.IsNullOrEmpty(isolatedUserProfile))
                psi.EnvironmentVariables["USERPROFILE"] = isolatedUserProfile;
            if (!string.IsNullOrEmpty(isolatedLocalAppData))
                psi.EnvironmentVariables["LOCALAPPDATA"] = isolatedLocalAppData;

            using var process = new Process { StartInfo = psi };

            DataReceivedEventHandler handler = (s, e) =>
            {
                if (e.Data != null)
                {
                    string line = e.Data.TrimEnd('\r', '\n');
                    // Подавляем спам прогресс-баров (tqdm)
                    if (line.Contains("[tqdm]") || line.Contains("100%|██████████")) return;
                    batcher.AppendLine(line);
                }
            };

            process.OutputDataReceived += handler;
            process.ErrorDataReceived += handler;

            process.Start();
            process.StandardInput.Close(); // Закрываем stdin чтобы процесс не ждал ввода
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited) process.Kill(true);
                }
                catch { }
            });

            await process.WaitForExitAsync(cancellationToken);
            batcher.Complete();

            return process.ExitCode;
        }
    }
}
