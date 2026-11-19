using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace StormSwitchBox.Services
{
    /// <summary>
    /// Безопасный сервис вызова диалоговых окон выбора файлов и папок:
    /// на UI-потоке использует WinUI StoragePickers с InitializeWithWindow, а при fallback на STA-поток не передаёт чужой WinUI HWND (исключает Win32 deadlock).
    /// </summary>
    public static class SystemDialogService
    {
        private static Task<T> RunOnUiAsync<T>(Func<Task<T>> uiFunc)
        {
            var dq = App.MainDispatcher;
            if (dq == null)
                return Task.FromException<T>(new InvalidOperationException("MainDispatcher unavailable"));

            if (dq.HasThreadAccess)
                return uiFunc();

            var tcs = new TaskCompletionSource<T>();
            if (!dq.TryEnqueue(async () =>
            {
                try
                {
                    T res = await uiFunc();
                    tcs.TrySetResult(res);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            }))
            {
                tcs.TrySetException(new InvalidOperationException("Failed to enqueue UI dialog"));
            }
            return tcs.Task;
        }

        private static List<string> ParseFileExtensions(string? filter)
        {
            var exts = new List<string>();
            if (!string.IsNullOrWhiteSpace(filter))
            {
                string[] parts = filter.Split('|');
                for (int i = 1; i < parts.Length; i += 2)
                {
                    foreach (string token in parts[i].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (token == "*.*" || token == "*")
                        {
                            if (!exts.Contains("*")) exts.Add("*");
                        }
                        else if (token.StartsWith("*.", StringComparison.Ordinal))
                        {
                            string ext = token.Substring(1);
                            if (!exts.Contains(ext, StringComparer.OrdinalIgnoreCase))
                                exts.Add(ext);
                        }
                    }
                }
            }
            if (exts.Count == 0)
                exts.Add("*");
            return exts;
        }

        public static async Task<List<string>?> OpenMultipleFilesDialogAsync(string title, string filter, string? initialDirectory = null)
        {
            if (App.MainWindowHandle != IntPtr.Zero && App.MainDispatcher != null)
            {
                try
                {
                    var picked = await RunOnUiAsync(async () =>
                    {
                        var picker = new FileOpenPicker();
                        InitializeWithWindow.Initialize(picker, App.MainWindowHandle);
                        picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
                        foreach (string ext in ParseFileExtensions(filter))
                            picker.FileTypeFilter.Add(ext);
                        var files = await picker.PickMultipleFilesAsync();
                        return files?.Select(f => f.Path).Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
                    });
                    if (picked != null && picked.Count > 0)
                        return picked;
                    return null;
                }
                catch { }
            }

            return await RunStaDialogAsync<List<string>?>(() =>
            {
                using var ofd = new OpenFileDialog
                {
                    Title = title,
                    Filter = filter,
                    CheckFileExists = true,
                    Multiselect = true,
                    RestoreDirectory = true,
                    AutoUpgradeEnabled = true
                };
                if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
                    ofd.InitialDirectory = initialDirectory;

                var res = ofd.ShowDialog();
                return (res == DialogResult.OK && ofd.FileNames != null && ofd.FileNames.Length > 0)
                    ? ofd.FileNames.ToList()
                    : null;
            }, "OpenMultipleFilesDialog");
        }

        public static async Task<string?> OpenFileDialogAsync(string title, string filter, string? initialDirectory = null)
        {
            if (App.MainWindowHandle != IntPtr.Zero && App.MainDispatcher != null)
            {
                try
                {
                    var picked = await RunOnUiAsync(async () =>
                    {
                        var picker = new FileOpenPicker();
                        InitializeWithWindow.Initialize(picker, App.MainWindowHandle);
                        picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
                        foreach (string ext in ParseFileExtensions(filter))
                            picker.FileTypeFilter.Add(ext);
                        var file = await picker.PickSingleFileAsync();
                        return file?.Path;
                    });
                    return string.IsNullOrWhiteSpace(picked) ? null : picked;
                }
                catch { }
            }

            return await RunStaDialogAsync<string?>(() =>
            {
                using var ofd = new OpenFileDialog
                {
                    Title = title,
                    Filter = filter,
                    CheckFileExists = true,
                    Multiselect = false,
                    RestoreDirectory = true,
                    AutoUpgradeEnabled = true
                };
                if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
                    ofd.InitialDirectory = initialDirectory;

                var res = ofd.ShowDialog();
                return (res == DialogResult.OK && !string.IsNullOrWhiteSpace(ofd.FileName)) ? ofd.FileName : null;
            }, "OpenFileDialog");
        }

        public static async Task<string?> OpenFolderDialogAsync(string description, string? initialDirectory = null)
        {
            if (App.MainWindowHandle != IntPtr.Zero && App.MainDispatcher != null)
            {
                try
                {
                    var picked = await RunOnUiAsync(async () =>
                    {
                        var picker = new FolderPicker();
                        InitializeWithWindow.Initialize(picker, App.MainWindowHandle);
                        picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
                        picker.FileTypeFilter.Add("*");
                        var folder = await picker.PickSingleFolderAsync();
                        return folder?.Path;
                    });
                    return string.IsNullOrWhiteSpace(picked) ? null : picked;
                }
                catch { }
            }

            return await RunStaDialogAsync<string?>(() =>
            {
                using var fbd = new FolderBrowserDialog
                {
                    Description = description,
                    UseDescriptionForTitle = true,
                    ShowNewFolderButton = true,
                    AutoUpgradeEnabled = true
                };
                if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
                    fbd.SelectedPath = initialDirectory;

                var res = fbd.ShowDialog();
                return (res == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath)) ? fbd.SelectedPath : null;
            }, "FolderBrowserDialog");
        }

        public static async Task<string?> SaveFileDialogAsync(string title, string defaultFileName, string filter, string? initialDirectory = null)
        {
            if (App.MainWindowHandle != IntPtr.Zero && App.MainDispatcher != null)
            {
                try
                {
                    var picked = await RunOnUiAsync(async () =>
                    {
                        var picker = new FileSavePicker();
                        InitializeWithWindow.Initialize(picker, App.MainWindowHandle);
                        picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
                        picker.SuggestedFileName = defaultFileName;

                        string[] parts = (filter ?? string.Empty).Split('|');
                        for (int i = 0; i + 1 < parts.Length; i += 2)
                        {
                            string label = string.IsNullOrWhiteSpace(parts[i]) ? "Files" : parts[i].Trim();
                            var exts = parts[i + 1]
                                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                .Where(t => t.StartsWith("*.", StringComparison.Ordinal) && t != "*.*")
                                .Select(t => t.Substring(1))
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .ToList();
                            if (exts.Count > 0 && !picker.FileTypeChoices.ContainsKey(label))
                                picker.FileTypeChoices.Add(label, exts);
                        }
                        if (picker.FileTypeChoices.Count == 0)
                        {
                            string fallbackExt = Path.GetExtension(defaultFileName);
                            picker.FileTypeChoices.Add("File", new List<string> { string.IsNullOrEmpty(fallbackExt) ? ".dat" : fallbackExt });
                        }

                        var file = await picker.PickSaveFileAsync();
                        return file?.Path;
                    });
                    return string.IsNullOrWhiteSpace(picked) ? null : picked;
                }
                catch { }
            }

            return await RunStaDialogAsync<string?>(() =>
            {
                using var sfd = new SaveFileDialog
                {
                    Title = title,
                    FileName = defaultFileName,
                    Filter = filter,
                    OverwritePrompt = true,
                    RestoreDirectory = true,
                    AutoUpgradeEnabled = true
                };
                if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
                    sfd.InitialDirectory = initialDirectory;

                var res = sfd.ShowDialog();
                return (res == DialogResult.OK && !string.IsNullOrWhiteSpace(sfd.FileName)) ? sfd.FileName : null;
            }, "SaveFileDialog");
        }

        private static Task<T> RunStaDialogAsync<T>(Func<T> dialogAction, string dialogName)
        {
            var tcs = new TaskCompletionSource<T>();
            var thread = new Thread(() =>
            {
                try
                {
                    tcs.SetResult(dialogAction());
                }
                catch (Exception ex)
                {
                    App.Logger?.Log($"[Dialog] Ошибка открытия {dialogName}: {ex.Message}", Models.LogLevel.Warning);
                    tcs.SetResult(default!);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            return tcs.Task;
        }
    }
}
