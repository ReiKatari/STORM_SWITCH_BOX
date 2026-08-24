using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace StormSwitchBox.Services
{
    /// <summary>
    /// Надежный сервис вызова нативных диалоговых окон проводника Windows (STA Thread)
    /// с гарантией открытия Проводника на всех версиях Windows 10/11 без сбоев WinUI COM-инициализации.
    /// </summary>
    public static class SystemDialogService
    {
        private class WindowWrapper : IWin32Window
        {
            public WindowWrapper(IntPtr handle) { Handle = handle; }
            public IntPtr Handle { get; }
        }

        public static Task<string?> OpenFileDialogAsync(string title, string filter, string? initialDirectory = null)
        {
            var tcs = new TaskCompletionSource<string?>();
            var thread = new Thread(() =>
            {
                try
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
                    {
                        ofd.InitialDirectory = initialDirectory;
                    }

                    IWin32Window? owner = (App.MainWindowHandle != IntPtr.Zero) ? new WindowWrapper(App.MainWindowHandle) : null;
                    var res = owner != null ? ofd.ShowDialog(owner) : ofd.ShowDialog();
                    if (res == DialogResult.OK && !string.IsNullOrWhiteSpace(ofd.FileName))
                    {
                        tcs.SetResult(ofd.FileName);
                    }
                    else
                    {
                        tcs.SetResult(null);
                    }
                }
                catch (Exception ex)
                {
                    App.Logger?.Log($"[Dialog] Ошибка открытия OpenFileDialog: {ex.Message}", Models.LogLevel.Warning);
                    tcs.SetResult(null);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            return tcs.Task;
        }

        public static Task<string?> OpenFolderDialogAsync(string description, string? initialDirectory = null)
        {
            var tcs = new TaskCompletionSource<string?>();
            var thread = new Thread(() =>
            {
                try
                {
                    using var fbd = new FolderBrowserDialog
                    {
                        Description = description,
                        UseDescriptionForTitle = true,
                        ShowNewFolderButton = true,
                        AutoUpgradeEnabled = true
                    };

                    if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
                    {
                        fbd.SelectedPath = initialDirectory;
                    }

                    IWin32Window? owner = (App.MainWindowHandle != IntPtr.Zero) ? new WindowWrapper(App.MainWindowHandle) : null;
                    var res = owner != null ? fbd.ShowDialog(owner) : fbd.ShowDialog();
                    if (res == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                    {
                        tcs.SetResult(fbd.SelectedPath);
                    }
                    else
                    {
                        tcs.SetResult(null);
                    }
                }
                catch (Exception ex)
                {
                    App.Logger?.Log($"[Dialog] Ошибка открытия FolderBrowserDialog: {ex.Message}", Models.LogLevel.Warning);
                    tcs.SetResult(null);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            return tcs.Task;
        }

        public static Task<string?> SaveFileDialogAsync(string title, string defaultFileName, string filter, string? initialDirectory = null)
        {
            var tcs = new TaskCompletionSource<string?>();
            var thread = new Thread(() =>
            {
                try
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
                    {
                        sfd.InitialDirectory = initialDirectory;
                    }

                    IWin32Window? owner = (App.MainWindowHandle != IntPtr.Zero) ? new WindowWrapper(App.MainWindowHandle) : null;
                    var res = owner != null ? sfd.ShowDialog(owner) : sfd.ShowDialog();
                    if (res == DialogResult.OK && !string.IsNullOrWhiteSpace(sfd.FileName))
                    {
                        tcs.SetResult(sfd.FileName);
                    }
                    else
                    {
                        tcs.SetResult(null);
                    }
                }
                catch (Exception ex)
                {
                    App.Logger?.Log($"[Dialog] Ошибка открытия SaveFileDialog: {ex.Message}", Models.LogLevel.Warning);
                    tcs.SetResult(null);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            return tcs.Task;
        }
    }
}
