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
                        RestoreDirectory = true
                    };

                    if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
                    {
                        ofd.InitialDirectory = initialDirectory;
                    }

                    var res = ofd.ShowDialog();
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
                        ShowNewFolderButton = true
                    };

                    if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
                    {
                        fbd.SelectedPath = initialDirectory;
                    }

                    var res = fbd.ShowDialog();
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
    }
}
