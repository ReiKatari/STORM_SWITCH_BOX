using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using StormSwitchBox.Models;

namespace StormSwitchBox.Services
{
    public static class ExternalProcessRunner
    {
        public static Task<int> RunAsync(
            string fileName,
            IReadOnlyList<string> argumentList,
            string workingDirectory,
            ProcessingTask? task,
            CancellationToken cancellationToken,
            string? isolatedUserProfile = null,
            string? isolatedLocalAppData = null)
        {
            return RunCoreAsync(fileName, null, argumentList, workingDirectory, task, cancellationToken, isolatedUserProfile, isolatedLocalAppData);
        }

        public static Task<int> RunAsync(
            string fileName,
            string arguments,
            string workingDirectory,
            ProcessingTask? task,
            CancellationToken cancellationToken,
            string? isolatedUserProfile = null,
            string? isolatedLocalAppData = null,
            bool forceUtf8Console = false)
        {
            return RunCoreAsync(fileName, arguments, null, workingDirectory, task, cancellationToken, isolatedUserProfile, isolatedLocalAppData);
        }

        public static async Task<(int ExitCode, string StdOut, string StdErr)> RunCaptureAsync(
            string fileName,
            IReadOnlyList<string>? argumentList,
            string? rawArguments,
            string workingDirectory,
            CancellationToken cancellationToken)
        {
            var psi = CreateBaseStartInfo(fileName, rawArguments, argumentList, workingDirectory, null, null);
            var stdOut = new StringBuilder();
            var stdErr = new StringBuilder();

            using var process = new Process { StartInfo = psi };
            process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (stdOut) stdOut.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (stdErr) stdErr.AppendLine(e.Data); };

            process.Start();
            int pid = process.Id;
            TryLowerPriority(process);
            JobObjectManager.AddProcess(process);
            try { process.StandardInput.Close(); } catch { }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                return (process.ExitCode, stdOut.ToString(), stdErr.ToString());
            }
            catch (OperationCanceledException)
            {
                TryKillTree(process);
                throw;
            }
            finally
            {
                TryKillIfStillRunningOnCancel(process, cancellationToken);
                JobObjectManager.UnregisterProcess(pid);
            }
        }

        private static async Task<int> RunCoreAsync(
            string fileName,
            string? arguments,
            IReadOnlyList<string>? argumentList,
            string workingDirectory,
            ProcessingTask? task,
            CancellationToken cancellationToken,
            string? isolatedUserProfile,
            string? isolatedLocalAppData)
        {
            using var batcher = task != null ? new LogBatcher(task) : null;
            var psi = CreateBaseStartInfo(fileName, arguments, argumentList, workingDirectory, isolatedUserProfile, isolatedLocalAppData);

            using var process = new Process { StartInfo = psi };
            DataReceivedEventHandler handler = (_, e) =>
            {
                if (e.Data != null && batcher != null)
                {
                    string line = e.Data.TrimEnd('\r', '\n');
                    if (line.Contains("[tqdm]", StringComparison.Ordinal) ||
                        line.Contains("100%|", StringComparison.Ordinal) ||
                        line.StartsWith("[WARN]: Failed to match key", StringComparison.Ordinal))
                        return;
                    batcher.AppendLine(line);
                }
            };

            process.OutputDataReceived += handler;
            process.ErrorDataReceived += handler;

            process.Start();
            int pid = process.Id;
            TryLowerPriority(process);
            JobObjectManager.AddProcess(process);
            try { process.StandardInput.Close(); } catch { }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                return process.ExitCode;
            }
            catch (OperationCanceledException)
            {
                TryKillTree(process);
                throw;
            }
            finally
            {
                TryKillIfStillRunningOnCancel(process, cancellationToken);
                JobObjectManager.UnregisterProcess(pid);
                batcher?.Complete();
            }
        }

        private static ProcessStartInfo CreateBaseStartInfo(
            string fileName,
            string? arguments,
            IReadOnlyList<string>? argumentList,
            string workingDirectory,
            string? isolatedUserProfile,
            string? isolatedLocalAppData)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = string.IsNullOrWhiteSpace(workingDirectory) ? AppContext.BaseDirectory : workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            if (argumentList != null)
            {
                foreach (var arg in argumentList)
                    psi.ArgumentList.Add(arg);
            }
            else if (!string.IsNullOrEmpty(arguments))
            {
                psi.Arguments = arguments;
            }

            psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8:replace";
            psi.EnvironmentVariables["PYTHONUTF8"] = "1";
            psi.EnvironmentVariables["PYTHONLEGACYWINDOWSSTDIO"] = "0";
            psi.EnvironmentVariables["PYTHONUNBUFFERED"] = "1";
            psi.EnvironmentVariables["PYTHONCOERCECLOCALE"] = "1";

            if (!string.IsNullOrEmpty(isolatedUserProfile))
                psi.EnvironmentVariables["USERPROFILE"] = isolatedUserProfile;
            if (!string.IsNullOrEmpty(isolatedLocalAppData))
                psi.EnvironmentVariables["LOCALAPPDATA"] = isolatedLocalAppData;

            return psi;
        }

        private static void TryLowerPriority(Process process)
        {
            try
            {
                if (!process.HasExited)
                    process.PriorityClass = ProcessPriorityClass.BelowNormal;
            }
            catch { }
        }

        private static void TryKillIfStillRunningOnCancel(Process process, CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
                TryKillTree(process);
        }

        private static void TryKillTree(Process process)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch { }
        }
    }
}
