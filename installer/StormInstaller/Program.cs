using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace StormSwitchBox.Installer;

public sealed class VolumetricButton : Button
{
    public Color PrimaryColor { get; set; } = Color.FromArgb(139, 92, 246);
    public Color SecondaryColor { get; set; } = Color.FromArgb(6, 182, 212);
    public Color SurfaceColor1 { get; set; } = Color.FromArgb(38, 28, 62);
    public Color SurfaceColor2 { get; set; } = Color.FromArgb(23, 19, 43);
    public Color SurfaceHoverColor1 { get; set; } = Color.FromArgb(46, 38, 82);
    public Color BorderColor { get; set; } = Color.FromArgb(90, 255, 255, 255);
    public Color DisabledBackColor { get; set; } = Color.FromArgb(30, 27, 48);
    public Color DisabledForeColor { get; set; } = Color.FromArgb(130, 140, 158);
    public bool IsPrimary { get; set; } = false;

    private bool _hover = false;
    private bool _pressed = false;

    public VolumetricButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseMnemonic = false;
        Cursor = Cursors.Hand;
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        if (!Enabled)
        {
            _hover = false;
            _pressed = false;
        }
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e) { if (Enabled) { _hover = true; Invalidate(); } base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs mevent) { if (Enabled) { _pressed = true; Invalidate(); } base.OnMouseDown(mevent); }
    protected override void OnMouseUp(MouseEventArgs mevent) { _pressed = false; Invalidate(); base.OnMouseUp(mevent); }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Color.FromArgb(18, 15, 29));

        int offset = (Enabled && _pressed) ? 2 : ((Enabled && _hover) ? -1 : 0);
        Rectangle rect = new Rectangle(1, 2 + Math.Max(0, offset), Width - 3, Height - 5);
        using var path = InstallerForm.GetRoundedRectPath(rect, 9);

        if (Enabled && !_pressed)
        {
            Rectangle shadowRect = new Rectangle(1, 4, Width - 3, Height - 5);
            using var shadowPath = InstallerForm.GetRoundedRectPath(shadowRect, 9);
            using var shadowBrush = new SolidBrush(Color.FromArgb(110, 0, 0, 0));
            g.FillPath(shadowBrush, shadowPath);
        }

        if (!Enabled)
        {
            using var disabledBrush = new SolidBrush(DisabledBackColor);
            g.FillPath(disabledBrush, path);
            using var disabledPen = new Pen(Color.FromArgb(55, 148, 163, 184), 1f);
            g.DrawPath(disabledPen, path);
            TextRenderer.DrawText(g, Text, Font, rect, DisabledForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            return;
        }

        Color c1 = IsPrimary ? PrimaryColor : (_hover ? SurfaceHoverColor1 : SurfaceColor1);
        Color c2 = IsPrimary ? SecondaryColor : SurfaceColor2;
        using (var grad = new LinearGradientBrush(rect, c1, c2, 135f))
        {
            g.FillPath(grad, path);
        }

        using (var topPen = new Pen(_hover ? Color.FromArgb(170, BorderColor) : BorderColor, 1.2f))
        {
            g.DrawPath(topPen, path);
        }

        TextRenderer.DrawText(g, Text, Font, rect, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}

public sealed class VolumetricCheckBox : CheckBox
{
    public Color AccentColor { get; set; } = Color.FromArgb(139, 92, 246);
    public Color BoxBackColor { get; set; } = Color.FromArgb(30, 26, 52);
    public Color BoxBorderColor { get; set; } = Color.FromArgb(95, 105, 140);

    private bool _hover = false;

    public VolumetricCheckBox()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
        UseMnemonic = false;
        Cursor = Cursors.Hand;
        Padding = new Padding(0);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        if (!Enabled) _hover = false;
        Cursor = Enabled ? Cursors.Hand : Cursors.Default;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e) { if (Enabled) { _hover = true; Invalidate(); } base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    public override Size GetPreferredSize(Size proposedSize)
    {
        Size textSize = TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPrefix);
        return new Size(26 + textSize.Width + 6, Math.Max(22, textSize.Height + 4));
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Color.FromArgb(18, 15, 29));

        const int boxSize = 16;
        const int radius = 4;
        int boxY = Math.Max(0, (Height - boxSize) / 2);
        Rectangle boxRect = new Rectangle(1, boxY, boxSize, boxSize);
        using var boxPath = InstallerForm.GetRoundedRectPath(boxRect, radius);

        if (Checked)
        {
            Color fillTop = Enabled ? AccentColor : Color.FromArgb(120, AccentColor);
            Color fillBottom = Enabled
                ? Color.FromArgb(Math.Max(0, AccentColor.R - 30), Math.Max(0, AccentColor.G - 30), Math.Max(0, AccentColor.B - 30))
                : Color.FromArgb(90, AccentColor);
            using var grad = new LinearGradientBrush(boxRect, fillTop, fillBottom, 135f);
            g.FillPath(grad, boxPath);

            using var glowPen = new Pen(Color.FromArgb(_hover ? 220 : 165, 255, 255, 255), 1.1f);
            g.DrawPath(glowPen, boxPath);
        }
        else
        {
            using var fillBrush = new SolidBrush(BoxBackColor);
            g.FillPath(fillBrush, boxPath);

            Color border = _hover && Enabled ? AccentColor : BoxBorderColor;
            using var borderPen = new Pen(border, 1.3f);
            g.DrawPath(borderPen, boxPath);
        }

        Color textColor = Enabled ? ForeColor : Color.FromArgb(140, ForeColor);
        Rectangle textRect = new Rectangle(24, 0, Math.Max(10, Width - 24), Height);
        TextRenderer.DrawText(g, Text, Font, textRect, textColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}

public sealed class InstallerForm : Form
{
    private const string AppDisplayName = "STORM SWITCH BOX";
    private static readonly string AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "8.0.0";
    private const string AppFolderName = "STORM SWITCH BOX";
    private const string ExeName = "StormSwitchBox.exe";
    private const string IcoName = "app.ico";
    private const string CertFileName = "STORM_CERTIFICATE.cer";
    private const string ManifestFileName = "install_manifest.txt";
    private const string UninstallerExeName = "uninstall.exe";
    private const string UninstallRegPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\STORM_SWITCH_BOX";
    private const string InstallerMutexName = @"Global\STORM_SWITCH_BOX_INSTALLER_SingleInstanceMutex";
    private const string InstallerWindowTitle = $"{AppDisplayName} - STORM INSTALLER";

    private Panel headerPanel = null!;
    private Panel bodyPanel = null!;
    private Panel bottomPanel = null!;
    private Label lblTitle = null!;
    private Label lblSubtitle = null!;
    private Label lblModeTitle = null!;
    private RadioButton rbStandard = null!;
    private RadioButton rbPortable = null!;
    private Label lblPathTitle = null!;
    private TextBox txtInstallPath = null!;
    private VolumetricButton btnBrowse = null!;
    private Label lblDropHint = null!;
    private Label lblOptionsTitle = null!;

    private VolumetricCheckBox chkDesktop = null!;
    private VolumetricCheckBox chkStartMenu = null!;
    private VolumetricCheckBox chkFirewall = null!;
    private VolumetricCheckBox chkInstallCert = null!;
    private VolumetricCheckBox chkContextMenu = null!;
    private VolumetricCheckBox chkRunAfter = null!;

    private ComboBox cmbLang = null!;
    private ComboBox cmbTheme = null!;
    private ComboBox cmbAccent = null!;

    private ProgressBar progressBar = null!;
    private Label lblStatus = null!;
    private VolumetricButton btnCancel = null!;
    private VolumetricButton btnInstall = null!;

    private bool isInstalling = false;
    private bool isLightTheme = false;
    private Color currentAccent = Color.FromArgb(139, 92, 246);
    private Color currentComboBack = Color.FromArgb(38, 32, 61);
    private Color currentComboFore = Color.White;
    private Color currentHeaderBorder = Color.FromArgb(61, 48, 92);
    private string currentLang = DetectDefaultLanguageCode();
    private CancellationTokenSource? _installCts;
    private int currentInstallStep = -1;
    private string lastErrorMessage = "";

    private Bitmap? _headerLogoBitmap;
    private Bitmap? _stormLogoBitmap;

    private static readonly uint WM_STORM_ACTIVATE = RegisterWindowMessage("STORM_SWITCH_BOX_INSTALLER_ACTIVATE");

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool DeleteFileW(string lpFileName);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("shell32.dll")]
    private static extern void DragAcceptFiles(IntPtr hwnd, bool fAccept);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint DragQueryFile(IntPtr hDrop, uint iFile, StringBuilder? lpszFile, int cch);

    [DllImport("shell32.dll")]
    private static extern void DragFinish(IntPtr hDrop);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint message, uint action, IntPtr pChangeFilterStruct);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ChangeWindowMessageFilter(uint message, uint action);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessage(string lpString);

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    private const int SW_RESTORE = 9;
    private const uint MSGFLT_ALLOW = 1;
    private const uint WM_DROPFILES = 0x0233;
    private const uint WM_COPYDATA = 0x004A;
    private const uint WM_COPYGLOBALDATA = 0x0049;
    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const int SHCNF_IDLIST = 0x0000;

    private static int DetectDefaultLanguageIndex()
    {
        try
        {
            string uiLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            if (string.IsNullOrEmpty(uiLang))
                uiLang = CultureInfo.InstalledUICulture.TwoLetterISOLanguageName.ToLowerInvariant();

            return uiLang switch
            {
                "ru" or "uk" or "be" or "kk" => 0,
                "en" => 1,
                "de" => 2,
                "fr" => 3,
                "zh" => 4,
                "ko" => 5,
                "ja" => 6,
                "es" => 7,
                "it" => 8,
                "pt" => 9,
                "hi" => 10,
                _ => 1
            };
        }
        catch
        {
            return 1;
        }
    }

    private static string DetectDefaultLanguageCode()
    {
        string[] langs = { "ru", "en", "de", "fr", "zh", "ko", "ja", "es", "it", "pt", "hi" };
        int idx = DetectDefaultLanguageIndex();
        return langs[Math.Clamp(idx, 0, langs.Length - 1)];
    }

    private static readonly Dictionary<string, string[]> L10n = new()
    {
        ["ru"] = new[] {
            $"Мастер установки • Версия {AppVersion}",
            "Режим установки:",
            $"Стандартная установка в C:\\Program Files\\{AppFolderName}",
            "Портативный режим (папка data/ и .portable, без записи в реестр)",
            "Папка назначения (поддерживается перетаскивание папки из Проводника):",
            "Обзор...",
            "Перетащите любую папку из Проводника Windows прямо в это окно (UAC UIPI активен)",
            "Параметры интеграции:",
            $"Создать ярлык на Рабочем столе ({AppDisplayName}.lnk)",
            "Добавить в меню «Пуск» и список приложений Windows",
            "Добавить в исключения Брандмауэра Windows (входящие и исходящие)",
            "Установить единый сертификат доверия STORM CERTIFICATE (SIGNATURE)",
            $"Запустить {AppDisplayName} сразу после завершения установки",
            "Отмена",
            "Установить",
            $"Остановка запущенных процессов {AppDisplayName}...",
            "Регистрация единого сертификата STORM CERTIFICATE (SIGNATURE)...",
            $"Распаковка файлов {AppDisplayName} {AppVersion}...",
            "Создание ярлыков и регистрация в Windows...",
            "Настройка правил Брандмауэра Windows (входящие и исходящие)...",
            $"{AppDisplayName} {AppVersion} успешно установлен!",
            "Готово",
            "Закрыть",
            "Ошибка установки: ",
            "Добавить команды в контекстное меню Проводника (.nsp, .nsz, .xci, .xcz, .nup, .3ds, .cia)",
            "Отмена установки и откат изменений...",
            "Установка отменена пользователем. Частичные файлы удалены.",
            $"Экземпляр установщика {AppDisplayName} уже запущен.",
            $"Удалить {AppDisplayName} и все зарегистрированные компоненты из системы?",
            $"{AppDisplayName} успешно удален.",
            "Ошибка удаления: ",
            "Файл манифеста установки (install_manifest.txt) не найден."
        },
        ["en"] = new[] {
            $"Setup wizard • Version {AppVersion}",
            "Installation mode:",
            $"Standard installation to C:\\Program Files\\{AppFolderName}",
            "Portable mode (creates .portable and data/ folder, zero registry writes)",
            "Destination folder (Explorer drag-and-drop supported):",
            "Browse...",
            "Drag and drop any folder from Windows Explorer directly into this window (UAC UIPI enabled)",
            "System integration options:",
            $"Create desktop shortcut ({AppDisplayName}.lnk)",
            "Add to Windows Start Menu and installed apps",
            "Add Windows Firewall inbound and outbound exceptions",
            "Install unified trust certificate STORM CERTIFICATE (SIGNATURE)",
            $"Launch {AppDisplayName} immediately after installation",
            "Cancel",
            "Install",
            $"Stopping running {AppDisplayName} processes...",
            "Registering unified certificate STORM CERTIFICATE (SIGNATURE)...",
            $"Extracting {AppDisplayName} {AppVersion} files...",
            "Creating shortcuts and registering in Windows...",
            "Configuring Windows Firewall rules (inbound and outbound)...",
            $"{AppDisplayName} {AppVersion} installed successfully!",
            "Done",
            "Close",
            "Installation error: ",
            "Add Windows Explorer context menu commands (.nsp, .nsz, .xci, .xcz, .nup, .3ds, .cia)",
            "Cancelling installation and rolling back changes...",
            "Installation cancelled by user. Partial files were removed.",
            $"An instance of the {AppDisplayName} installer is already running.",
            $"Uninstall {AppDisplayName} and all registered components from the system?",
            $"{AppDisplayName} uninstalled successfully.",
            "Uninstall error: ",
            "Installation manifest file (install_manifest.txt) was not found."
        },
        ["de"] = new[] {
            $"Installationsassistent • Version {AppVersion}",
            "Installationsmodus:",
            $"Standardinstallation in C:\\Program Files\\{AppFolderName}",
            "Portabler Modus (.portable und data/-Ordner, ohne Registry)",
            "Zielordner (Drag-and-Drop aus dem Explorer unterstützt):",
            "Durchsuchen...",
            "Ziehen Sie einen beliebigen Ordner per Drag-and-Drop in dieses Fenster",
            "Integrationsoptionen:",
            $"Desktop-Verknüpfung erstellen ({AppDisplayName}.lnk)",
            "Im Startmenü und in der App-Liste registrieren",
            "Windows-Firewall-Ausnahmen hinzufügen (Ein- und Ausgehend)",
            "Einheitliches Zertifikat STORM CERTIFICATE (SIGNATURE) installieren",
            $"{AppDisplayName} sofort nach der Installation starten",
            "Abbrechen",
            "Installieren",
            $"Laufende Prozesse von {AppDisplayName} werden beendet...",
            "Einheitliches Zertifikat STORM CERTIFICATE (SIGNATURE) wird registriert...",
            $"Dateien von {AppDisplayName} {AppVersion} werden entpackt...",
            "Verknüpfungen werden erstellt und in Windows registriert...",
            "Windows-Firewall-Regeln werden konfiguriert...",
            $"{AppDisplayName} {AppVersion} wurde erfolgreich installiert!",
            "Fertig",
            "Schließen",
            "Installationsfehler: ",
            "Kontextmenü-Befehle im Windows-Explorer hinzufügen (.nsp, .nsz, .xci, .xcz, .nup, .3ds, .cia)",
            "Installation wird abgebrochen und Änderungen werden rückgängig gemacht...",
            "Installation vom Benutzer abgebrochen. Teilweise Dateien wurden entfernt.",
            $"Eine Instanz des {AppDisplayName}-Installers wird bereits ausgeführt.",
            $"{AppDisplayName} und alle registrierten Komponenten vom System entfernen?",
            $"{AppDisplayName} wurde erfolgreich deinstalliert.",
            "Deinstallationsfehler: ",
            "Installationsmanifest (install_manifest.txt) wurde nicht gefunden."
        },
        ["fr"] = new[] {
            $"Assistant d'installation • Version {AppVersion}",
            "Mode d'installation :",
            $"Installation standard dans C:\\Program Files\\{AppFolderName}",
            "Mode portable (dossier data/ et .portable, sans registre)",
            "Dossier de destination (glisser-déposer pris en charge) :",
            "Parcourir...",
            "Glissez et déposez un dossier depuis l'Explorateur Windows dans cette fenêtre",
            "Options d'intégration :",
            $"Créer un raccourci sur le Bureau ({AppDisplayName}.lnk)",
            "Ajouter au menu Démarrer de Windows",
            "Ajouter aux exceptions du Pare-feu Windows (entrant et sortant)",
            "Installer le certificat unifié STORM CERTIFICATE (SIGNATURE)",
            $"Lancer {AppDisplayName} immédiatement après l'installation",
            "Annuler",
            "Installer",
            $"Arrêt des processus {AppDisplayName} en cours...",
            "Enregistrement du certificat unifié STORM CERTIFICATE (SIGNATURE)...",
            $"Extraction des fichiers de {AppDisplayName} {AppVersion}...",
            "Création des raccourcis et enregistrement dans Windows...",
            "Configuration des règles du Pare-feu Windows...",
            $"{AppDisplayName} {AppVersion} installé avec succès !",
            "Terminé",
            "Fermer",
            "Erreur d'installation : ",
            "Ajouter au menu contextuel de l'Explorateur Windows (.nsp, .nsz, .xci, .xcz, .nup, .3ds, .cia)",
            "Annulation de l'installation et restauration...",
            "Installation annulée par l'utilisateur. Les fichiers partiels ont été supprimés.",
            $"Une instance de l'installateur {AppDisplayName} est déjà en cours d'exécution.",
            $"Désinstaller {AppDisplayName} et tous les composants enregistrés du système ?",
            $"{AppDisplayName} désinstallé avec succès.",
            "Erreur de désinstallation : ",
            "Le fichier manifeste d'installation (install_manifest.txt) est introuvable."
        },
        ["zh"] = new[] {
            $"安装向导 • 版本 {AppVersion}",
            "安装模式：",
            $"标准安装至 C:\\Program Files\\{AppFolderName}",
            "便携模式 (创建 .portable 与 data/ 目录，不写注册表)",
            "目标文件夹（支持从资源管理器直接拖放文件夹）：",
            "浏览...",
            "支持将任意文件夹直接拖入此窗口（已启用 UAC UIPI 穿透）",
            "系统集成选项：",
            $"创建桌面快捷方式 ({AppDisplayName}.lnk)",
            "添加到 Windows“开始”菜单与应用列表",
            "添加 Windows 防火墙入站与出站例外规则",
            "安装统一信任证书 STORM CERTIFICATE (SIGNATURE)",
            $"安装完成后立即启动 {AppDisplayName}",
            "取消",
            "安装",
            $"正在停止运行中的 {AppDisplayName} 进程...",
            "正在注册统一数字证书 STORM CERTIFICATE (SIGNATURE)...",
            $"正在解压 {AppDisplayName} {AppVersion} 文件...",
            "正在创建快捷方式并注册到 Windows...",
            "正在配置 Windows 防火墙入站和出站规则...",
            $"{AppDisplayName} {AppVersion} 安装成功！",
            "完成",
            "关闭",
            "安装错误：",
            "添加资源管理器右键菜单命令 (.nsp, .nsz, .xci, .xcz, .nup, .3ds, .cia)",
            "正在取消安装并回滚更改...",
            "用户已取消安装，已清理部分写入的文件。",
            $"{AppDisplayName} 安装程序已在运行。",
            $"是否从系统中卸载 {AppDisplayName} 及其所有已注册组件？",
            $"{AppDisplayName} 已成功卸载。",
            "卸载错误：",
            "未找到安装清单文件 (install_manifest.txt)。"
        },
        ["ko"] = new[] {
            $"설치 마법사 • 버전 {AppVersion}",
            "설치 모드:",
            $"표준 설치 (C:\\Program Files\\{AppFolderName})",
            "포터블 모드 (.portable 및 data/ 폴더 생성, 레지스트리 미사용)",
            "설치 경로 (탐색기 폴더 드래그 앤 드롭 지원):",
            "찾아보기...",
            "Windows 탐색기에서 폴더를 이 창으로 직접 드래그하세요 (UAC UIPI 적용)",
            "시스템 통합 옵션:",
            $"바탕화면 바로가기 생성 ({AppDisplayName}.lnk)",
            "Windows 시작 메뉴 및 앱 목록에 등록",
            "Windows 방화벽 인바운드 및 아웃바운드 예외 추가",
            "통합 인증서 STORM CERTIFICATE (SIGNATURE) 설치",
            $"설치 완료 후 즉시 {AppDisplayName} 실행",
            "취소",
            "설치",
            $"실행 중인 {AppDisplayName} 프로세스 종료 중...",
            "통합 인증서 STORM CERTIFICATE (SIGNATURE) 등록 중...",
            $"{AppDisplayName} {AppVersion} 파일 압축 해제 중...",
            "바로가기 생성 및 Windows 레지스트리 등록 중...",
            "Windows 방화벽 규칙 설정 중...",
            $"{AppDisplayName} {AppVersion} 설치가 완료되었습니다!",
            "완료",
            "닫기",
            "설치 오류: ",
            "Windows 탐색기 컨텍스트 메뉴 명령 추가 (.nsp, .nsz, .xci, .xcz, .nup, .3ds, .cia)",
            "설치 취소 및 변경 사항 롤백 중...",
            "사용자가 설치를 취소했습니다. 임시 파일이 제거되었습니다.",
            $"{AppDisplayName} 설치 프로그램이 이미 실행 중입니다.",
            $"시스템에서 {AppDisplayName} 및 등록된 모든 구성 요소를 제거하시겠습니까?",
            $"{AppDisplayName} 제거가 완료되었습니다.",
            "제거 오류: ",
            "설치 매니페스트 파일(install_manifest.txt)을 찾을 수 없습니다."
        },
        ["ja"] = new[] {
            $"セットアップウィザード • バージョン {AppVersion}",
            "インストールモード:",
            $"標準インストール (C:\\Program Files\\{AppFolderName})",
            "ポータブルモード (.portable と data/ を作成 - レジストリ不使用)",
            "インストール先フォルダ (エクスプローラーからのドラッグ＆ドロップ対応):",
            "参照...",
            "フォルダをこのウィンドウにドロップしてパスを変更できます (UAC UIPI 対応)",
            "統合オプション:",
            $"デスクトップにショートカットを作成 ({AppDisplayName}.lnk)",
            "スタートメニューとアプリ一覧に登録",
            "Windows ファイアウォールの受信および送信ルールを追加",
            "統一証明書 STORM CERTIFICATE (SIGNATURE) をインストール",
            $"インストール完了後にすぐ {AppDisplayName} を起動",
            "キャンセル",
            "インストール",
            $"実行中の {AppDisplayName} プロセスを終了しています...",
            "統一証明書 STORM CERTIFICATE (SIGNATURE) を登録しています...",
            $"{AppDisplayName} {AppVersion} のファイルを展開しています...",
            "ショートカットを作成し Windows に登録しています...",
            "Windows ファイアウォールのルールを設定しています...",
            $"{AppDisplayName} {AppVersion} のインストールが完了しました！",
            "完了",
            "閉じる",
            "インストールエラー: ",
            "エクスプローラーの右クリックメニューを追加 (.nsp, .nsz, .xci, .xcz, .nup, .3ds, .cia)",
            "インストールを中止して変更をロールバックしています...",
            "ユーザーによりインストールが中止されました。作成中のファイルは削除されました。",
            $"{AppDisplayName} インストーラーはすでに起動しています。",
            $"{AppDisplayName} と登録されたすべてのコンポーネントをシステムから削除しますか？",
            $"{AppDisplayName} を正常にアンインストールしました。",
            "アンインストールエラー: ",
            "インストールマニフェストファイル (install_manifest.txt) が見つかりません。"
        },
        ["es"] = new[] {
            $"Asistente de instalación • Versión {AppVersion}",
            "Modo de instalación:",
            $"Instalación estándar en C:\\Program Files\\{AppFolderName}",
            "Modo portátil (crea .portable y data/, sin registro)",
            "Carpeta de destino (soporta arrastrar y soltar carpeta):",
            "Examinar...",
            "Arrastre cualquier carpeta del Explorador de Windows a esta ventana",
            "Opciones de integración:",
            $"Crear acceso directo en el Escritorio ({AppDisplayName}.lnk)",
            "Añadir al menú Inicio de Windows",
            "Añadir excepciones al Firewall de Windows (entrada y salida)",
            "Instalar certificado unificado STORM CERTIFICATE (SIGNATURE)",
            $"Iniciar {AppDisplayName} inmediatamente al finalizar",
            "Cancelar",
            "Instalar",
            $"Deteniendo procesos activos de {AppDisplayName}...",
            "Registrando certificado unificado STORM CERTIFICATE (SIGNATURE)...",
            $"Extrayendo archivos de {AppDisplayName} {AppVersion}...",
            "Creando accesos directos y registrando en Windows...",
            "Configurando reglas del Firewall de Windows...",
            $"¡{AppDisplayName} {AppVersion} instalado correctamente!",
            "Listo",
            "Cerrar",
            "Error de instalación: ",
            "Añadir comandos al menú contextual del Explorador (.nsp, .nsz, .xci, .xcz, .nup, .3ds, .cia)",
            "Cancelando la instalación y revirtiendo los cambios...",
            "Instalación cancelada por el usuario. Los archivos parciales fueron eliminados.",
            $"Ya se está ejecutando una instancia del instalador de {AppDisplayName}.",
            $"¿Desinstalar {AppDisplayName} y todos los componentes registrados del sistema?",
            $"{AppDisplayName} desinstalado correctamente.",
            "Error de desinstalación: ",
            "No se encontró el archivo de manifiesto de instalación (install_manifest.txt)."
        },
        ["it"] = new[] {
            $"Procedura d'installazione • Versione {AppVersion}",
            "Modalità d'installazione:",
            $"Installazione standard in C:\\Program Files\\{AppFolderName}",
            "Modalità portatile (crea .portable e data/, senza registro)",
            "Cartella di destinazione (Drag-and-Drop supportato):",
            "Sfoglia...",
            "Trascina qualsiasi cartella da Esplora file in questa finestra",
            "Opzioni d'integrazione:",
            $"Crea collegamento sul Desktop ({AppDisplayName}.lnk)",
            "Aggiungi al menu Start di Windows",
            "Aggiungi eccezioni a Windows Firewall (entrata e uscita)",
            "Installa certificato unificato STORM CERTIFICATE (SIGNATURE)",
            $"Avvia {AppDisplayName} subito dopo l'installazione",
            "Annulla",
            "Installa",
            $"Arresto dei processi {AppDisplayName} in esecuzione...",
            "Registrazione del certificato unificato STORM CERTIFICATE (SIGNATURE)...",
            $"Estrazione dei file di {AppDisplayName} {AppVersion}...",
            "Creazione dei collegamenti e registrazione in Windows...",
            "Configurazione delle regole di Windows Firewall...",
            $"{AppDisplayName} {AppVersion} installato correttamente!",
            "Fatto",
            "Chiudi",
            "Errore d'installazione: ",
            "Aggiungi comandi al menu contestuale di Esplora file (.nsp, .nsz, .xci, .xcz, .nup, .3ds, .cia)",
            "Annullamento dell'installazione e ripristino...",
            "Installazione annullata dall'utente. I file parziali sono stati rimossi.",
            $"Un'istanza del programma di installazione di {AppDisplayName} è già in esecuzione.",
            $"Disinstallare {AppDisplayName} e tutti i componenti registrati dal sistema?",
            $"{AppDisplayName} disinstallato correttamente.",
            "Errore di disinstallazione: ",
            "File manifesto di installazione (install_manifest.txt) non trovato."
        },
        ["pt"] = new[] {
            $"Assistente de instalação • Versão {AppVersion}",
            "Modo de instalação:",
            $"Instalação padrão em C:\\Program Files\\{AppFolderName}",
            "Modo portátil (cria .portable e pasta data/, sem registro)",
            "Pasta de destino (suporta arrastar e soltar):",
            "Procurar...",
            "Arraste qualquer pasta do Windows Explorer diretamente para esta janela",
            "Opções de integração:",
            $"Criar atalho na Área de Trabalho ({AppDisplayName}.lnk)",
            "Adicionar ao menu Iniciar do Windows",
            "Adicionar exceções no Firewall do Windows (entrada e saída)",
            "Instalar certificado unificado STORM CERTIFICATE (SIGNATURE)",
            $"Iniciar {AppDisplayName} imediatamente após a instalação",
            "Cancelar",
            "Instalar",
            $"Encerrando processos ativos do {AppDisplayName}...",
            "Registrando certificado unificado STORM CERTIFICATE (SIGNATURE)...",
            $"Extraindo arquivos do {AppDisplayName} {AppVersion}...",
            "Criando atalhos e registrando no Windows...",
            "Configurando regras do Firewall do Windows...",
            $"{AppDisplayName} {AppVersion} instalado com sucesso!",
            "Concluído",
            "Fechar",
            "Erro na instalação: ",
            "Adicionar comandos ao menu de contexto do Explorer (.nsp, .nsz, .xci, .xcz, .nup, .3ds, .cia)",
            "Cancelando a instalação e revertendo alterações...",
            "Instalação cancelada pelo usuário. Arquivos parciais foram removidos.",
            $"Uma instância do instalador do {AppDisplayName} já está em execução.",
            $"Desinstalar {AppDisplayName} e todos os componentes registrados do sistema?",
            $"{AppDisplayName} desinstalado com sucesso.",
            "Erro na desinstalación: ",
            "Arquivo de manifesto de instalação (install_manifest.txt) não encontrado."
        },
        ["hi"] = new[] {
            $"इंस्टॉलेशन विज़ार्ड • संस्करण {AppVersion}",
            "इंस्टॉलेशन मोड:",
            $"मानक इंस्टॉलेशन (C:\\Program Files\\{AppFolderName})",
            "पोर्टेबल मोड (.portable और data/ फ़ोल्डर, बिना रजिस्ट्री)",
            "गंतव्य फ़ोल्डर (फ़ोल्डर ड्रैग-एंड-ड्रॉप समर्थित):",
            "ब्राउज़ करें...",
            "विंडोज़ एक्सप्लोरर से किसी भी फ़ोल्डर को सीधे इस विंडो में ड्रॉप करें",
            "एकीकरण विकल्प:",
            $"डेस्कटॉप शॉर्टकट बनाएँ ({AppDisplayName}.lnk)",
            "स्टार्ट मेनू और ऐप सूची में जोड़ें",
            "Windows फ़ायरवॉल इनबाउंड और आउटबाउंड अपवाद जोड़ें",
            "एकीकृत प्रमाणपत्र STORM CERTIFICATE (SIGNATURE) इंस्टॉल करें",
            $"इंस्टॉलेशन के तुरंत बाद {AppDisplayName} चलाएँ",
            "रद्द करें",
            "इंस्टॉल",
            $"चल रही {AppDisplayName} प्रक्रियाएँ बंद की जा रही हैं...",
            "एकीकृत प्रमाणपत्र STORM CERTIFICATE (SIGNATURE) पंजीकृत किया जा रहा है...",
            $"{AppDisplayName} {AppVersion} फ़ाइलें निकाली जा रही हैं...",
            "शॉर्टकट बनाए जा रहे हैं और Windows में पंजीकरण हो रहा है...",
            "Windows फ़ायरवॉल नियम कॉन्फ़िगर किए जा रहे हैं...",
            $"{AppDisplayName} {AppVersion} सफलतापूर्वक इंस्टॉल हो गया!",
            "पूर्ण",
            "बंद करें",
            "इंस्टॉलेशन त्रुटि: ",
            "Windows Explorer संदर्भ मेनू कमांड जोड़ें (.nsp, .nsz, .xci, .xcz, .nup, .3ds, .cia)",
            "इंस्टॉलेशन रद्द किया जा रहा है और परिवर्तन वापस किए जा रहे हैं...",
            "उपयोगकर्ता द्वारा इंस्टॉलेशन रद्द किया गया। आंशिक फ़ाइलें हटा दी गईं।",
            $"{AppDisplayName} इंस्टॉलर का एक उदाहरण पहले से चल रहा है।",
            $"क्या आप {AppDisplayName} और सभी पंजीकृत घटकों को सिस्टम से हटाना चाहते हैं?",
            $"{AppDisplayName} सफलतापूर्वक अनइंस्टॉल हो गया।",
            "अनइंस्टॉल त्रुटि: ",
            "इंस्टॉलेशन मैनिफेस्ट फ़ाइल (install_manifest.txt) नहीं मिली।"
        }
    };

    private static string GetLocalized(string lang, int index)
    {
        if (L10n.TryGetValue(lang, out var arr) && index >= 0 && index < arr.Length)
            return arr[index];
        if (L10n.TryGetValue("en", out var enArr) && index >= 0 && index < enArr.Length)
            return enArr[index];
        if (L10n.TryGetValue("ru", out var ruArr) && index >= 0 && index < ruArr.Length)
            return ruArr[index];
        return string.Empty;
    }

    private string T(int index) => GetLocalized(currentLang, index);

    public InstallerForm()
    {
        try
        {
            var selfPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(selfPath))
                DeleteFileW(selfPath + ":Zone.Identifier");
        }
        catch { }

        this.KeyPreview = true;
        this.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Escape && !isInstalling)
                this.Close();
        };

        LoadEmbeddedAssets();
        InitializeComponent();
        DetectAndApplyDefaultLanguage();
        ApplyTheme("STORM MIDNIGHT");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _headerLogoBitmap?.Dispose();
            _headerLogoBitmap = null;
            _stormLogoBitmap?.Dispose();
            _stormLogoBitmap = null;
            _installCts?.Dispose();
            _installCts = null;
        }
        base.Dispose(disposing);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            UpdateDwmTitleBarTheme();
            DragAcceptFiles(this.Handle, true);
            ChangeWindowMessageFilterEx(this.Handle, WM_DROPFILES, MSGFLT_ALLOW, IntPtr.Zero);
            ChangeWindowMessageFilterEx(this.Handle, WM_COPYDATA, MSGFLT_ALLOW, IntPtr.Zero);
            ChangeWindowMessageFilterEx(this.Handle, WM_COPYGLOBALDATA, MSGFLT_ALLOW, IntPtr.Zero);
            ChangeWindowMessageFilter(WM_DROPFILES, MSGFLT_ALLOW);
            ChangeWindowMessageFilter(WM_COPYDATA, MSGFLT_ALLOW);
            ChangeWindowMessageFilter(WM_COPYGLOBALDATA, MSGFLT_ALLOW);
        }
        catch { }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (isInstalling && currentInstallStep != 6)
        {
            e.Cancel = true;
            RequestCancelDuringInstall();
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == (int)WM_STORM_ACTIVATE)
        {
            if (this.WindowState == FormWindowState.Minimized)
                this.WindowState = FormWindowState.Normal;
            this.Activate();
            this.BringToFront();
            ShowAlreadyRunningBanner();
            return;
        }

        if (m.Msg == (int)WM_DROPFILES)
        {
            IntPtr hDrop = m.WParam;
            uint count = DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);
            if (count > 0 && !isInstalling)
            {
                var sb = new StringBuilder(1024);
                DragQueryFile(hDrop, 0, sb, sb.Capacity);
                string dropped = sb.ToString();
                if (Directory.Exists(dropped))
                {
                    txtInstallPath.Text = NormalizeInstallPath(dropped);
                }
                else if (File.Exists(dropped))
                {
                    string? dir = Path.GetDirectoryName(dropped);
                    if (!string.IsNullOrEmpty(dir))
                        txtInstallPath.Text = NormalizeInstallPath(dir);
                }
            }
            DragFinish(hDrop);
            return;
        }

        base.WndProc(ref m);
    }

    private void ShowAlreadyRunningBanner()
    {
        lblStatus.Visible = true;
        lblStatus.ForeColor = currentAccent;
        lblStatus.Text = T(27);
    }

    private void UpdateDwmTitleBarTheme()
    {
        if (!IsHandleCreated) return;
        try
        {
            int dark = isLightTheme ? 0 : 1;
            DwmSetWindowAttribute(this.Handle, 20, ref dark, sizeof(int));
        }
        catch { }
    }

    private void LoadEmbeddedAssets()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            foreach (var name in asm.GetManifestResourceNames())
            {
                if (this.Icon == null &&
                    (name.EndsWith("app.ico", StringComparison.OrdinalIgnoreCase) ||
                     name.EndsWith("AppIcon.ico", StringComparison.OrdinalIgnoreCase) ||
                     name.EndsWith("storm_switch_box.ico", StringComparison.OrdinalIgnoreCase)))
                {
                    using var s = asm.GetManifestResourceStream(name);
                    if (s != null)
                        this.Icon = new Icon(s);
                }
                else if (_stormLogoBitmap == null &&
                         (name.EndsWith("storm_logo.png", StringComparison.OrdinalIgnoreCase) ||
                          name.EndsWith("header_badge.png", StringComparison.OrdinalIgnoreCase)))
                {
                    using var s = asm.GetManifestResourceStream(name);
                    if (s != null)
                    {
                        using var raw = Image.FromStream(s);
                        _stormLogoBitmap = new Bitmap(raw, new Size(56, 56));
                    }
                }
            }

            if (this.Icon == null && !string.IsNullOrEmpty(Application.ExecutablePath) && File.Exists(Application.ExecutablePath))
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }

            if (this.Icon != null)
            {
                using var icoBmp = this.Icon.ToBitmap();
                _headerLogoBitmap = new Bitmap(icoBmp, new Size(56, 56));
                if (_stormLogoBitmap == null)
                    _stormLogoBitmap = new Bitmap(icoBmp, new Size(56, 56));
            }
        }
        catch { }
    }

    private void InitializeComponent()
    {
        this.ClientSize = new Size(840, 580);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Text = InstallerWindowTitle;
        this.BackColor = Color.FromArgb(18, 15, 29);
        this.Font = new Font("Segoe UI", 9f, FontStyle.Regular);

        headerPanel = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(840, 92),
            BackColor = Color.FromArgb(28, 23, 46)
        };
        headerPanel.Paint += HeaderPanel_Paint;

        lblTitle = new Label
        {
            Text = AppDisplayName,
            Font = new Font("Segoe UI", 15.5f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(22, 16),
            AutoSize = true,
            UseMnemonic = false
        };
        headerPanel.Controls.Add(lblTitle);

        lblSubtitle = new Label
        {
            Text = T(0),
            Font = new Font("Segoe UI", 9.2f, FontStyle.Regular),
            ForeColor = currentAccent,
            Location = new Point(24, 48),
            AutoSize = true,
            UseMnemonic = false
        };
        headerPanel.Controls.Add(lblSubtitle);

        cmbLang = CreateThemedComboBox(new Point(422, 18), new Size(156, 24));
        cmbLang.Items.AddRange(new object[] {
            "Русский (ru)", "English (en)", "Deutsch (de)", "Français (fr)",
            "简体中文 (zh)", "한국어 (ko)", "日本語 (ja)", "Español (es)",
            "Italiano (it)", "Português (pt)", "हिन्दी (hi)"
        });
        cmbLang.SelectedIndex = 0;
        cmbLang.SelectedIndexChanged += (s, e) => ChangeLanguageByIndex(cmbLang.SelectedIndex);
        headerPanel.Controls.Add(cmbLang);

        cmbTheme = CreateThemedComboBox(new Point(586, 18), new Size(158, 24));
        cmbTheme.Items.AddRange(new object[] {
            "STORM MIDNIGHT",
            "STORM NIGHT",
            "STORM DARK",
            "STORM DAY",
            "STORM MATRIX",
            "STORM CYBERPUNK",
            "STORM FANTASY",
            "STORM WARHAMMER 40K"
        });
        cmbTheme.SelectedIndex = 0;
        cmbTheme.SelectedIndexChanged += (s, e) => ApplyTheme(cmbTheme.SelectedItem?.ToString() ?? "STORM MIDNIGHT");
        headerPanel.Controls.Add(cmbTheme);

        cmbAccent = CreateThemedComboBox(new Point(422, 48), new Size(322, 24));
        cmbAccent.Items.AddRange(new object[] {
            "Violet (#8B5CF6)", "Cyan (#06B6D4)", "Electric Blue (#3B82F6)", "Royal Indigo (#6366F1)",
            "Teal Mint (#14B8A6)", "Emerald Green (#10B981)", "Neon Lime (#84CC16)", "Amber Gold (#F59E0B)",
            "Solar Orange (#F97316)", "Crimson Red (#EF4444)", "Rose Pink (#F43F5E)", "Magenta Fuchsia (#EC4899)",
            "Platinum Silver (#E2E8F0)"
        });
        cmbAccent.SelectedIndex = 0;
        cmbAccent.SelectedIndexChanged += (s, e) => ApplyAccent(cmbAccent.SelectedIndex);
        headerPanel.Controls.Add(cmbAccent);

        this.Controls.Add(headerPanel);

        bodyPanel = new Panel
        {
            Location = new Point(22, 102),
            Size = new Size(796, 400),
            BackColor = Color.Transparent
        };
        bodyPanel.Paint += BodyPanel_Paint;

        lblModeTitle = new Label
        {
            Text = T(1),
            Font = new Font("Segoe UI", 9.8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(226, 232, 240),
            Location = new Point(0, 2),
            UseMnemonic = false,
            AutoSize = true
        };
        bodyPanel.Controls.Add(lblModeTitle);

        rbStandard = new RadioButton
        {
            Text = T(2),
            Checked = true,
            Location = new Point(8, 25),
            AutoSize = true,
            UseMnemonic = false,
            ForeColor = Color.White
        };
        rbStandard.CheckedChanged += Mode_CheckedChanged;
        bodyPanel.Controls.Add(rbStandard);

        rbPortable = new RadioButton
        {
            Text = T(3),
            Location = new Point(8, 49),
            AutoSize = true,
            UseMnemonic = false,
            ForeColor = Color.FromArgb(203, 213, 225)
        };
        rbPortable.CheckedChanged += Mode_CheckedChanged;
        bodyPanel.Controls.Add(rbPortable);

        lblPathTitle = new Label
        {
            Text = T(4),
            Font = new Font("Segoe UI", 9.8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(226, 232, 240),
            Location = new Point(0, 78),
            UseMnemonic = false,
            AutoSize = true
        };
        bodyPanel.Controls.Add(lblPathTitle);

        txtInstallPath = new TextBox
        {
            Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppFolderName),
            Location = new Point(4, 103),
            Size = new Size(656, 26),
            BackColor = Color.FromArgb(38, 32, 61),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        bodyPanel.Controls.Add(txtInstallPath);

        btnBrowse = new VolumetricButton
        {
            Text = T(5),
            Location = new Point(668, 101),
            Size = new Size(124, 30),
            ForeColor = Color.White
        };
        btnBrowse.Click += BtnBrowse_Click;
        bodyPanel.Controls.Add(btnBrowse);

        lblDropHint = new Label
        {
            Text = T(6),
            Font = new Font("Segoe UI", 8.6f, FontStyle.Italic),
            ForeColor = Color.FromArgb(56, 189, 248),
            Location = new Point(4, 134),
            UseMnemonic = false,
            AutoSize = true
        };
        bodyPanel.Controls.Add(lblDropHint);

        lblOptionsTitle = new Label
        {
            Text = T(7),
            Font = new Font("Segoe UI", 9.8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(226, 232, 240),
            Location = new Point(0, 160),
            UseMnemonic = false,
            AutoSize = true
        };
        bodyPanel.Controls.Add(lblOptionsTitle);

        chkDesktop = CreateOptionCheckBox(T(8), true, new Point(8, 186), Color.White);
        bodyPanel.Controls.Add(chkDesktop);

        chkStartMenu = CreateOptionCheckBox(T(9), true, new Point(8, 212), Color.White);
        bodyPanel.Controls.Add(chkStartMenu);

        chkFirewall = CreateOptionCheckBox(T(10), true, new Point(8, 238), Color.FromArgb(56, 189, 248));
        bodyPanel.Controls.Add(chkFirewall);

        chkInstallCert = CreateOptionCheckBox(T(11), true, new Point(8, 264), Color.FromArgb(52, 211, 153));
        bodyPanel.Controls.Add(chkInstallCert);

        chkContextMenu = CreateOptionCheckBox(T(24), true, new Point(8, 290), Color.FromArgb(196, 181, 253));
        bodyPanel.Controls.Add(chkContextMenu);

        chkRunAfter = CreateOptionCheckBox(T(12), true, new Point(8, 316), Color.FromArgb(250, 204, 21));
        bodyPanel.Controls.Add(chkRunAfter);

        progressBar = new ProgressBar
        {
            Location = new Point(4, 348),
            Size = new Size(788, 14),
            Style = ProgressBarStyle.Continuous,
            Value = 0,
            Visible = false
        };
        bodyPanel.Controls.Add(progressBar);

        lblStatus = new Label
        {
            Text = "",
            Location = new Point(4, 366),
            Size = new Size(788, 22),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(52, 211, 153),
            UseMnemonic = false,
            Visible = false
        };
        bodyPanel.Controls.Add(lblStatus);

        this.Controls.Add(bodyPanel);

        bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 64,
            BackColor = Color.FromArgb(28, 23, 46),
            Padding = new Padding(22, 12, 22, 12)
        };

        btnCancel = new VolumetricButton
        {
            Text = T(13),
            Size = new Size(130, 38),
            Location = new Point(516, 13),
            ForeColor = Color.White
        };
        btnCancel.Click += BtnCancel_Click;
        bottomPanel.Controls.Add(btnCancel);

        btnInstall = new VolumetricButton
        {
            Text = T(14),
            Size = new Size(160, 38),
            Location = new Point(658, 13),
            IsPrimary = true,
            PrimaryColor = currentAccent,
            SecondaryColor = Color.FromArgb(6, 182, 212),
            ForeColor = Color.White
        };
        btnInstall.Click += BtnInstall_Click;
        bottomPanel.Controls.Add(btnInstall);

        this.Controls.Add(bottomPanel);
    }

    private ComboBox CreateThemedComboBox(Point location, Size size)
    {
        var cmb = new ComboBox
        {
            Location = location,
            Size = size,
            DropDownStyle = ComboBoxStyle.DropDownList,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 20,
            BackColor = currentComboBack,
            ForeColor = currentComboFore,
            FlatStyle = FlatStyle.Flat
        };
        cmb.DrawItem += ComboBox_DrawItem;
        return cmb;
    }

    private void ComboBox_DrawItem(object? sender, DrawItemEventArgs e)
    {
        if (sender is not ComboBox cmb || e.Index < 0) return;

        bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        Color back = selected ? currentAccent : currentComboBack;
        Color fore = selected ? Color.White : currentComboFore;

        using var backBrush = new SolidBrush(back);
        e.Graphics.FillRectangle(backBrush, e.Bounds);

        string text = cmb.Items[e.Index]?.ToString() ?? string.Empty;
        Rectangle textRect = new Rectangle(e.Bounds.X + 4, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 8), e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics, text, cmb.Font, textRect, fore, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }

    private VolumetricCheckBox CreateOptionCheckBox(string text, bool isChecked, Point location, Color foreColor)
    {
        return new VolumetricCheckBox
        {
            Text = text,
            Checked = isChecked,
            Location = location,
            AutoSize = true,
            ForeColor = foreColor,
            AccentColor = currentAccent
        };
    }

    private void HeaderPanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // Top-right corner app icon: pure rendering without frames, borders, or background containers
        if (_headerLogoBitmap != null)
        {
            g.DrawImage(_headerLogoBitmap, new Rectangle(762, 18, 56, 56));
        }

        using var sepPen = new Pen(currentHeaderBorder, 1f);
        g.DrawLine(sepPen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
    }

    private void BodyPanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // STORM SOFT emblem strictly aligned under header app icon (X=762-22=740, same 56x56 size, no borders)
        if (_stormLogoBitmap != null)
        {
            g.DrawImage(_stormLogoBitmap, new Rectangle(740, 4, 56, 56));
        }
    }

    private void DetectAndApplyDefaultLanguage()
    {
        int idx = DetectDefaultLanguageIndex();
        if (cmbLang.SelectedIndex != idx)
            cmbLang.SelectedIndex = idx;
        else
            ChangeLanguageByIndex(idx);
    }

    private void ChangeLanguageByIndex(int index)
    {
        string[] langs = { "ru", "en", "de", "fr", "zh", "ko", "ja", "es", "it", "pt", "hi" };
        currentLang = langs[Math.Clamp(index, 0, langs.Length - 1)];

        lblSubtitle.Text = T(0);
        lblModeTitle.Text = T(1);
        rbStandard.Text = T(2);
        rbPortable.Text = T(3);
        lblPathTitle.Text = T(4);
        btnBrowse.Text = T(5);
        lblDropHint.Text = T(6);
        lblOptionsTitle.Text = T(7);
        chkDesktop.Text = T(8);
        chkStartMenu.Text = T(9);
        chkFirewall.Text = T(10);
        chkInstallCert.Text = T(11);
        chkContextMenu.Text = T(24);
        chkRunAfter.Text = T(12);
        btnCancel.Text = currentInstallStep == 6 ? T(22) : T(13);
        btnInstall.Text = currentInstallStep == 6 ? T(21) : T(14);

        if (currentInstallStep == 1) lblStatus.Text = T(15);
        else if (currentInstallStep == 2) lblStatus.Text = T(16);
        else if (currentInstallStep == 3) lblStatus.Text = T(17);
        else if (currentInstallStep == 4) lblStatus.Text = T(18);
        else if (currentInstallStep == 5) lblStatus.Text = T(19);
        else if (currentInstallStep == 6) lblStatus.Text = T(20);
        else if (currentInstallStep == 7) lblStatus.Text = T(23) + lastErrorMessage;
        else if (currentInstallStep == 8) lblStatus.Text = T(25);
        else if (currentInstallStep == 9) lblStatus.Text = T(26);
    }

    private void ApplyTheme(string theme)
    {
        isLightTheme = string.Equals(theme, "STORM DAY", StringComparison.OrdinalIgnoreCase);

        (Color bg, Color hdr, Color inputBg, Color border, Color fg, Color secBtn1, Color secBtn2, Color secHover) = theme switch
        {
            "STORM DARK" => (
                Color.FromArgb(15, 23, 42), Color.FromArgb(30, 41, 59), Color.FromArgb(51, 65, 85),
                Color.FromArgb(71, 85, 105), Color.FromArgb(248, 250, 252),
                Color.FromArgb(51, 65, 85), Color.FromArgb(30, 41, 59), Color.FromArgb(71, 85, 105)),
            "STORM NIGHT" => (
                Color.FromArgb(10, 11, 16), Color.FromArgb(18, 19, 26), Color.FromArgb(26, 28, 38),
                Color.FromArgb(37, 40, 54), Color.White,
                Color.FromArgb(34, 37, 50), Color.FromArgb(20, 22, 30), Color.FromArgb(48, 52, 70)),
            "STORM DAY" => (
                Color.FromArgb(243, 244, 248), Color.FromArgb(255, 255, 255), Color.FromArgb(255, 255, 255),
                Color.FromArgb(209, 213, 219), Color.FromArgb(17, 24, 39),
                Color.FromArgb(229, 234, 241), Color.FromArgb(209, 216, 226), Color.FromArgb(215, 222, 232)),
            "STORM MATRIX" => (
                Color.FromArgb(6, 16, 9), Color.FromArgb(11, 29, 17), Color.FromArgb(18, 43, 26),
                Color.FromArgb(30, 70, 43), Color.FromArgb(226, 252, 235),
                Color.FromArgb(20, 52, 31), Color.FromArgb(12, 32, 19), Color.FromArgb(28, 72, 43)),
            "STORM CYBERPUNK" => (
                Color.FromArgb(8, 9, 14), Color.FromArgb(16, 18, 29), Color.FromArgb(25, 28, 46),
                Color.FromArgb(255, 0, 127), Color.FromArgb(252, 238, 9),
                Color.FromArgb(32, 34, 58), Color.FromArgb(18, 20, 34), Color.FromArgb(48, 44, 82)),
            "STORM FANTASY" => (
                Color.FromArgb(18, 14, 10), Color.FromArgb(28, 22, 16), Color.FromArgb(41, 32, 23),
                Color.FromArgb(74, 55, 35), Color.FromArgb(254, 243, 199),
                Color.FromArgb(52, 40, 28), Color.FromArgb(32, 25, 18), Color.FromArgb(72, 54, 36)),
            "STORM WARHAMMER 40K" => (
                Color.FromArgb(16, 13, 13), Color.FromArgb(26, 20, 20), Color.FromArgb(38, 28, 28),
                Color.FromArgb(82, 58, 40), Color.FromArgb(245, 240, 230),
                Color.FromArgb(48, 36, 32), Color.FromArgb(28, 21, 20), Color.FromArgb(68, 50, 42)),
            _ => ( // STORM MIDNIGHT
                Color.FromArgb(18, 15, 29), Color.FromArgb(28, 23, 46), Color.FromArgb(38, 32, 61),
                Color.FromArgb(61, 48, 92), Color.White,
                Color.FromArgb(46, 36, 76), Color.FromArgb(28, 22, 48), Color.FromArgb(62, 48, 102))
        };

        this.BackColor = bg;
        headerPanel.BackColor = hdr;
        bottomPanel.BackColor = hdr;
        bodyPanel.BackColor = bg;
        currentHeaderBorder = border;
        currentComboBack = isLightTheme ? Color.FromArgb(234, 239, 245) : inputBg;
        currentComboFore = fg;

        lblTitle.ForeColor = fg;
        lblModeTitle.ForeColor = fg;
        lblPathTitle.ForeColor = fg;
        lblOptionsTitle.ForeColor = fg;
        rbStandard.ForeColor = fg;
        rbPortable.ForeColor = isLightTheme ? Color.FromArgb(55, 65, 81) : Color.FromArgb(203, 213, 225);

        txtInstallPath.BackColor = inputBg;
        txtInstallPath.ForeColor = fg;

        foreach (var cmb in new[] { cmbLang, cmbTheme, cmbAccent })
        {
            cmb.BackColor = currentComboBack;
            cmb.ForeColor = currentComboFore;
            cmb.Invalidate();
        }

        lblDropHint.ForeColor = isLightTheme ? Color.FromArgb(3, 105, 161) : Color.FromArgb(56, 189, 248);

        chkDesktop.ForeColor = fg;
        chkStartMenu.ForeColor = fg;
        chkFirewall.ForeColor = isLightTheme ? Color.FromArgb(3, 105, 161) : Color.FromArgb(56, 189, 248);
        chkInstallCert.ForeColor = isLightTheme ? Color.FromArgb(4, 120, 87) : Color.FromArgb(52, 211, 153);
        chkContextMenu.ForeColor = isLightTheme ? Color.FromArgb(109, 40, 217) : Color.FromArgb(196, 181, 253);
        chkRunAfter.ForeColor = isLightTheme ? Color.FromArgb(180, 83, 9) : Color.FromArgb(250, 204, 21);

        Color boxBg = isLightTheme ? Color.White : inputBg;
        foreach (var chk in new[] { chkDesktop, chkStartMenu, chkFirewall, chkInstallCert, chkContextMenu, chkRunAfter })
        {
            chk.BoxBackColor = boxBg;
            chk.BoxBorderColor = border;
            chk.AccentColor = currentAccent;
            chk.Invalidate();
        }

        Color disabledBg = isLightTheme ? Color.FromArgb(226, 232, 240) : Color.FromArgb(30, 27, 48);
        Color disabledFg = isLightTheme ? Color.FromArgb(100, 116, 139) : Color.FromArgb(130, 140, 158);
        Color btnBorder = isLightTheme ? Color.FromArgb(180, 190, 205) : Color.FromArgb(90, 255, 255, 255);

        foreach (var btn in new[] { btnBrowse, btnCancel })
        {
            btn.SurfaceColor1 = secBtn1;
            btn.SurfaceColor2 = secBtn2;
            btn.SurfaceHoverColor1 = secHover;
            btn.ForeColor = fg;
            btn.BorderColor = btnBorder;
            btn.DisabledBackColor = disabledBg;
            btn.DisabledForeColor = disabledFg;
            btn.Invalidate();
        }

        btnInstall.BorderColor = Color.FromArgb(110, 255, 255, 255);
        btnInstall.DisabledBackColor = disabledBg;
        btnInstall.DisabledForeColor = disabledFg;
        btnInstall.Invalidate();

        UpdateDwmTitleBarTheme();
        headerPanel.Invalidate();
        bodyPanel.Invalidate();
    }

    private void ApplyAccent(int index)
    {
        Color[] accents =
        {
            Color.FromArgb(139, 92, 246),  // Violet
            Color.FromArgb(6, 182, 212),   // Cyan
            Color.FromArgb(59, 130, 246),  // Electric Blue
            Color.FromArgb(99, 102, 241),  // Royal Indigo
            Color.FromArgb(20, 184, 166),  // Teal Mint
            Color.FromArgb(16, 185, 129),  // Emerald Green
            Color.FromArgb(132, 204, 22),  // Neon Lime
            Color.FromArgb(245, 158, 11),  // Amber Gold
            Color.FromArgb(249, 115, 22),  // Solar Orange
            Color.FromArgb(239, 68, 68),   // Crimson Red
            Color.FromArgb(244, 63, 94),   // Rose Pink
            Color.FromArgb(236, 72, 153),  // Magenta Fuchsia
            Color.FromArgb(148, 163, 184)  // Platinum Silver
        };
        currentAccent = accents[Math.Clamp(index, 0, accents.Length - 1)];
        btnInstall.PrimaryColor = currentAccent;
        lblSubtitle.ForeColor = currentAccent;

        foreach (var chk in new[] { chkDesktop, chkStartMenu, chkFirewall, chkInstallCert, chkContextMenu, chkRunAfter })
        {
            chk.AccentColor = currentAccent;
            chk.Invalidate();
        }

        headerPanel.Invalidate();
        btnInstall.Invalidate();
    }

    private void Mode_CheckedChanged(object? sender, EventArgs e)
    {
        if (isInstalling) return;
        if (rbPortable.Checked)
        {
            txtInstallPath.Text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AppFolderName);
            chkDesktop.Checked = false;
            chkStartMenu.Checked = false;
            chkContextMenu.Checked = false;
        }
        else
        {
            txtInstallPath.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppFolderName);
            chkDesktop.Checked = true;
            chkStartMenu.Checked = true;
            chkContextMenu.Checked = true;
        }
    }

    private void BtnBrowse_Click(object? sender, EventArgs e)
    {
        if (isInstalling) return;
        using var fbd = new FolderBrowserDialog
        {
            Description = $"{AppDisplayName} {AppVersion}",
            UseDescriptionForTitle = true
        };
        if (fbd.ShowDialog() == DialogResult.OK)
        {
            txtInstallPath.Text = NormalizeInstallPath(fbd.SelectedPath);
        }
    }

    private void BtnCancel_Click(object? sender, EventArgs e)
    {
        if (isInstalling)
        {
            RequestCancelDuringInstall();
            return;
        }
        this.Close();
    }

    private void RequestCancelDuringInstall()
    {
        if (_installCts != null && !_installCts.IsCancellationRequested)
        {
            btnCancel.Enabled = false;
            currentInstallStep = 8;
            lblStatus.ForeColor = isLightTheme ? Color.FromArgb(180, 83, 9) : Color.FromArgb(250, 204, 21);
            lblStatus.Text = T(25);
            _installCts.Cancel();
        }
    }

    private void SetInputsEnabled(bool enabled)
    {
        rbStandard.Enabled = enabled;
        rbPortable.Enabled = enabled;
        txtInstallPath.Enabled = enabled;
        btnBrowse.Enabled = enabled;
        chkDesktop.Enabled = enabled;
        chkStartMenu.Enabled = enabled;
        chkFirewall.Enabled = enabled;
        chkInstallCert.Enabled = enabled;
        chkContextMenu.Enabled = enabled;
        chkRunAfter.Enabled = enabled;
        cmbLang.Enabled = enabled;
    }

    private async void BtnInstall_Click(object? sender, EventArgs e)
    {
        if (isInstalling) return;
        isInstalling = true;
        btnInstall.Enabled = false;
        btnCancel.Enabled = true;
        SetInputsEnabled(false);

        progressBar.Visible = true;
        progressBar.Value = 0;
        lblStatus.Visible = true;
        lblStatus.ForeColor = isLightTheme ? Color.FromArgb(4, 120, 87) : Color.FromArgb(52, 211, 153);

        _installCts?.Dispose();
        _installCts = new CancellationTokenSource();
        CancellationToken token = _installCts.Token;

        string targetDir = NormalizeInstallPath(txtInstallPath.Text);
        txtInstallPath.Text = targetDir;

        var manifestEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rollbackFiles = new List<string>();
        var rollbackDirs = new List<string>();

        try
        {
            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
                rollbackDirs.Add(targetDir);
            }

            // Step 1: Stop running processes
            currentInstallStep = 1;
            lblStatus.Text = T(15);
            progressBar.Value = 10;
            await Task.Delay(120, token);

            StopTargetProcesses();
            token.ThrowIfCancellationRequested();

            string targetExe = Path.Combine(targetDir, ExeName);
            string targetIco = Path.Combine(targetDir, IcoName);
            string targetCer = Path.Combine(targetDir, CertFileName);

            bool icoExisted = File.Exists(targetIco);
            if (ExtractResource("app.ico", targetIco))
            {
                manifestEntries.Add(IcoName);
                if (!icoExisted) rollbackFiles.Add(targetIco);
            }

            // Step 2: Install STORM CERTIFICATE into TrustedPublisher (never Root)
            if (chkInstallCert.Checked)
            {
                currentInstallStep = 2;
                lblStatus.Text = T(16);
                progressBar.Value = 25;
                await Task.Delay(100, token);

                bool cerExisted = File.Exists(targetCer);
                if (ExtractResource(CertFileName, targetCer) && File.Exists(targetCer))
                {
                    manifestEntries.Add(CertFileName);
                    if (!cerExisted) rollbackFiles.Add(targetCer);
                    InstallCertificateSilently(targetCer);
                }
            }

            token.ThrowIfCancellationRequested();

            // Step 3: Extract payload with Zip-Slip protection
            currentInstallStep = 3;
            lblStatus.Text = T(17);
            progressBar.Value = 40;
            await Task.Delay(100, token);

            await Task.Run(() =>
            {
                ExtractPayloadWithManifest(
                    targetDir,
                    manifestEntries,
                    rollbackFiles,
                    rollbackDirs,
                    token,
                    pct =>
                    {
                        if (!IsDisposed && IsHandleCreated)
                        {
                            this.BeginInvoke(new Action(() =>
                            {
                                if (!IsDisposed) progressBar.Value = Math.Min(85, pct);
                            }));
                        }
                    });
            }, token);

            token.ThrowIfCancellationRequested();

            // Portable marker or standard uninstaller + manifest
            if (rbPortable.Checked)
            {
                string portableMarker = Path.Combine(targetDir, ".portable");
                bool markerExisted = File.Exists(portableMarker);
                File.WriteAllText(portableMarker, $"{AppDisplayName} PORTABLE MODE\n");
                manifestEntries.Add(".portable");
                if (!markerExisted) rollbackFiles.Add(portableMarker);

                string dataDir = Path.Combine(targetDir, "data");
                if (!Directory.Exists(dataDir))
                {
                    Directory.CreateDirectory(dataDir);
                    rollbackDirs.Add(dataDir);
                }
            }

            string uninstallExe = CopySelfAsUninstaller(targetDir, manifestEntries, rollbackFiles);
            WriteInstallManifest(targetDir, manifestEntries);

            // Step 4: Shortcuts, Registry, Context Menu
            currentInstallStep = 4;
            lblStatus.Text = T(18);
            progressBar.Value = 90;
            await Task.Delay(80, token);

            if (chkDesktop.Checked)
            {
                string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                CreateShortcut(Path.Combine(desktop, $"{AppDisplayName}.lnk"), targetExe, targetIco, $"{AppDisplayName} {AppVersion}");
            }

            try { DeleteFileW(targetExe + ":Zone.Identifier"); } catch { }

            if (chkStartMenu.Checked)
            {
                string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                string startDir = Path.Combine(programs, AppFolderName);
                Directory.CreateDirectory(startDir);
                CreateShortcut(Path.Combine(programs, $"{AppDisplayName}.lnk"), targetExe, targetIco, AppDisplayName);
                CreateShortcut(Path.Combine(startDir, $"{AppDisplayName}.lnk"), targetExe, targetIco, AppDisplayName);
            }

            if (!rbPortable.Checked)
            {
                RegisterUninstall(targetDir, targetExe, targetIco, uninstallExe);
            }

            if (chkContextMenu.Checked && !rbPortable.Checked)
            {
                UnregisterAllContextMenus();
                RegisterAllContextMenus(targetExe, currentLang);
            }

            // Step 5: Firewall rules
            if (chkFirewall.Checked)
            {
                currentInstallStep = 5;
                lblStatus.Text = T(19);
                progressBar.Value = 95;
                await Task.Delay(80, token);
                AddFirewallRules(targetExe, AppDisplayName);
            }

            // Step 6: Completion (lock both buttons and auto-close)
            progressBar.Value = 100;
            currentInstallStep = 6;
            lblStatus.ForeColor = isLightTheme ? Color.FromArgb(4, 120, 87) : Color.FromArgb(52, 211, 153);
            lblStatus.Text = T(20);
            btnInstall.Text = T(21);
            btnInstall.Enabled = false;
            btnCancel.Text = T(22);
            btnCancel.Enabled = false;

            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);

            if (chkRunAfter.Checked && File.Exists(targetExe))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{targetExe}\"",
                    UseShellExecute = true
                });
            }

            await Task.Delay(500);
            isInstalling = false;
            this.Close();
        }
        catch (OperationCanceledException)
        {
            await Task.Run(() => RollbackPartialInstall(targetDir, rollbackFiles, rollbackDirs));
            progressBar.Value = 0;
            currentInstallStep = 9;
            lblStatus.ForeColor = isLightTheme ? Color.FromArgb(180, 83, 9) : Color.FromArgb(250, 204, 21);
            lblStatus.Text = T(26);
            isInstalling = false;
            SetInputsEnabled(true);
            btnInstall.Enabled = true;
            btnCancel.Enabled = true;
            btnCancel.Text = T(13);
        }
        catch (Exception ex)
        {
            await Task.Run(() => RollbackPartialInstall(targetDir, rollbackFiles, rollbackDirs));
            progressBar.Value = 100;
            lblStatus.ForeColor = isLightTheme ? Color.FromArgb(185, 28, 28) : Color.FromArgb(239, 68, 68);
            currentInstallStep = 7;
            lastErrorMessage = ex.Message;
            lblStatus.Text = T(23) + ex.Message;
            isInstalling = false;
            SetInputsEnabled(true);
            btnInstall.Enabled = true;
            btnCancel.Enabled = true;
            btnCancel.Text = T(13);
        }
    }

    private static string NormalizeInstallPath(string? rawPath)
    {
        string fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppFolderName);
        if (string.IsNullOrWhiteSpace(rawPath))
            return fallback;

        string cleaned = rawPath.Trim().Trim('"').Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
            return fallback;

        if (cleaned.Length == 2 && cleaned[1] == ':')
            cleaned += Path.DirectorySeparatorChar;

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(cleaned);
        }
        catch
        {
            return fallback;
        }

        string trimmed = fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string? root = Path.GetPathRoot(fullPath)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        bool isDriveRoot = string.IsNullOrEmpty(trimmed) || string.Equals(trimmed, root, StringComparison.OrdinalIgnoreCase);

        if (isDriveRoot)
            return Path.Combine(fullPath, AppFolderName);

        string folderName = Path.GetFileName(trimmed);
        if (!string.Equals(folderName, AppFolderName, StringComparison.OrdinalIgnoreCase))
            return Path.Combine(trimmed, AppFolderName);

        return trimmed;
    }

    private static void StopTargetProcesses()
    {
        foreach (var procName in new[] { "StormSwitchBox", "STORM_SWITCH_BOX" })
        {
            foreach (var p in Process.GetProcessesByName(procName))
            {
                try { p.Kill(); p.WaitForExit(1500); } catch { }
            }
        }
    }

    private static bool ExtractResource(string resName, string targetPath)
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            foreach (var n in asm.GetManifestResourceNames())
            {
                if (n.EndsWith(resName, StringComparison.OrdinalIgnoreCase))
                {
                    using var s = asm.GetManifestResourceStream(n);
                    if (s != null)
                    {
                        using var fs = File.Create(targetPath);
                        s.CopyTo(fs);
                        try { DeleteFileW(targetPath + ":Zone.Identifier"); } catch { }
                        return true;
                    }
                    return false;
                }
            }
        }
        catch { }
        return false;
    }

    private static void ExtractPayloadWithManifest(
        string targetDir,
        ISet<string> manifestEntries,
        List<string> rollbackFiles,
        List<string> rollbackDirs,
        CancellationToken token,
        Action<int>? onProgress)
    {
        string fullTargetDir = Path.GetFullPath(targetDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                             + Path.DirectorySeparatorChar;

        var asm = Assembly.GetExecutingAssembly();
        foreach (var name in asm.GetManifestResourceNames())
        {
            if (!name.EndsWith("Payload.zip", StringComparison.OrdinalIgnoreCase))
                continue;

            using var s = asm.GetManifestResourceStream(name);
            if (s == null) continue;

            using var zip = new ZipArchive(s, ZipArchiveMode.Read);
            int total = Math.Max(1, zip.Entries.Count);
            int done = 0;
            int lastReportedPct = 40;

            foreach (var entry in zip.Entries)
            {
                token.ThrowIfCancellationRequested();

                string normalizedEntry = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                string destPath = Path.GetFullPath(Path.Combine(fullTargetDir, normalizedEntry));

                if (string.IsNullOrEmpty(entry.Name))
                {
                    string trimmedDest = destPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string trimmedRoot = fullTargetDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (!trimmedDest.Equals(trimmedRoot, StringComparison.OrdinalIgnoreCase) &&
                        !destPath.StartsWith(fullTargetDir, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException($"Unsafe directory entry blocked: {entry.FullName}");
                    }

                    if (!Directory.Exists(destPath))
                    {
                        Directory.CreateDirectory(destPath);
                        rollbackDirs.Add(destPath);
                    }
                    continue;
                }

                if (!destPath.StartsWith(fullTargetDir, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Zip-Slip attempt blocked: {entry.FullName}");
                }

                string? outDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                {
                    Directory.CreateDirectory(outDir);
                    rollbackDirs.Add(outDir);
                }

                bool existedBefore = File.Exists(destPath);
                entry.ExtractToFile(destPath, overwrite: true);
                if (!existedBefore)
                    rollbackFiles.Add(destPath);

                try { DeleteFileW(destPath + ":Zone.Identifier"); } catch { }

                string relPath = Path.GetRelativePath(fullTargetDir, destPath);
                manifestEntries.Add(relPath);

                done++;
                int pct = 40 + (int)((float)done / total * 45);
                if (pct != lastReportedPct)
                {
                    lastReportedPct = pct;
                    onProgress?.Invoke(pct);
                }
            }
        }
    }

    private static string CopySelfAsUninstaller(string targetDir, ISet<string> manifestEntries, List<string> rollbackFiles)
    {
        string uninstallExe = Path.Combine(targetDir, UninstallerExeName);
        try
        {
            string? selfPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(selfPath) && File.Exists(selfPath))
            {
                string fullSelf = Path.GetFullPath(selfPath);
                string fullUninstall = Path.GetFullPath(uninstallExe);
                if (!string.Equals(fullSelf, fullUninstall, StringComparison.OrdinalIgnoreCase))
                {
                    bool existedBefore = File.Exists(uninstallExe);
                    File.Copy(fullSelf, fullUninstall, overwrite: true);
                    if (!existedBefore) rollbackFiles.Add(fullUninstall);
                }
                try { DeleteFileW(fullUninstall + ":Zone.Identifier"); } catch { }
                manifestEntries.Add(UninstallerExeName);
            }
        }
        catch { }
        return uninstallExe;
    }

    private static void WriteInstallManifest(string targetDir, IEnumerable<string> relativeEntries)
    {
        string manifestPath = Path.Combine(targetDir, ManifestFileName);
        var ordered = relativeEntries
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(e => e, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        File.WriteAllLines(manifestPath, ordered, new UTF8Encoding(false));
    }

    private static void RollbackPartialInstall(string targetDir, IReadOnlyList<string> rollbackFiles, IReadOnlyList<string> rollbackDirs)
    {
        for (int i = rollbackFiles.Count - 1; i >= 0; i--)
        {
            try
            {
                if (File.Exists(rollbackFiles[i]))
                    File.Delete(rollbackFiles[i]);
            }
            catch { }
        }

        string manifestPath = Path.Combine(targetDir, ManifestFileName);
        try
        {
            if (File.Exists(manifestPath) && rollbackFiles.Count > 0)
                File.Delete(manifestPath);
        }
        catch { }

        foreach (string dir in rollbackDirs.Distinct(StringComparer.OrdinalIgnoreCase).OrderByDescending(d => d.Length))
        {
            try
            {
                if (Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length == 0)
                    Directory.Delete(dir, recursive: false);
            }
            catch { }
        }
    }

    private static void InstallCertificateSilently(string certPath)
    {
        try
        {
            using var cert = new X509Certificate2(certPath);
            using var store = new X509Store(StoreName.TrustedPublisher, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            store.Add(cert);
        }
        catch { }
    }

    private static void RegisterUninstall(string targetDir, string targetExe, string targetIco, string uninstallExe)
    {
        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(UninstallRegPath)
                         ?? Registry.CurrentUser.CreateSubKey(UninstallRegPath);
            if (key != null)
            {
                string uninstallCommand = File.Exists(uninstallExe)
                    ? $"\"{uninstallExe}\" --uninstall \"{targetDir}\""
                    : $"\"{Environment.ProcessPath}\" --uninstall \"{targetDir}\"";
                string quietCommand = File.Exists(uninstallExe)
                    ? $"\"{uninstallExe}\" --uninstall \"{targetDir}\" --silent"
                    : $"\"{Environment.ProcessPath}\" --uninstall \"{targetDir}\" --silent";

                key.SetValue("DisplayName", AppDisplayName);
                key.SetValue("DisplayVersion", AppVersion);
                key.SetValue("Publisher", "STORM TEAM");
                key.SetValue("DisplayIcon", File.Exists(targetIco) ? $"{targetIco},0" : $"{targetExe},0");
                key.SetValue("InstallLocation", targetDir);
                key.SetValue("UninstallString", uninstallCommand);
                key.SetValue("QuietUninstallString", quietCommand);
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }
        catch { }
    }

    private static (string Update, string Unpack, string Pack, string Convert, string Multi, string Verify) GetContextMenuVerbs(string lang)
    {
        return lang switch
        {
            "ru" => ("Обновление", "Распаковка", "Упаковка", "Конвертация", "Мульти-контент", "Проверка"),
            "de" => ("Aktualisieren", "Entpacken", "Packen", "Konvertieren", "Multi-Inhalt", "Prüfen"),
            "fr" => ("Mettre à jour", "Extraire", "Empaqueter", "Convertir", "Multi-contenu", "Vérifier"),
            "zh" => ("更新", "解包", "打包", "转换", "多内容", "校验"),
            "ko" => ("업데이트", "언팩", "패킹", "변환", "멀티 콘텐츠", "검증"),
            "ja" => ("更新", "展開", "パック", "変換", "マルチコンテンツ", "検証"),
            "es" => ("Actualizar", "Desempaquetar", "Empaquetar", "Convertir", "Multicontenido", "Verificar"),
            "it" => ("Aggiorna", "Estrai", "Impacchetta", "Converti", "Multi-contenuto", "Verifica"),
            "pt" => ("Atualizar", "Descompactar", "Empacotar", "Converter", "Multiconteúdo", "Verificar"),
            "hi" => ("अपडेट", "अनपैक", "पैक", "कनवर्ट", "मल्टी-कंटेंट", "सत्यापन"),
            _ => ("Update", "Unpack", "Pack", "Convert", "Multi-content", "Verify")
        };
    }

    private static readonly string[] ContextMenuAssociations =
    {
        @"SystemFileAssociations\.nsp",
        @"SystemFileAssociations\.nsz",
        @"SystemFileAssociations\.xci",
        @"SystemFileAssociations\.xcz",
        @"SystemFileAssociations\.nup",
        @"SystemFileAssociations\.3ds",
        @"SystemFileAssociations\.cci",
        @"SystemFileAssociations\.cia",
        @"SystemFileAssociations\.cxi",
        ".nsp",
        ".nsz",
        ".xci",
        ".xcz",
        ".nup",
        ".3ds",
        ".cci",
        ".cia",
        ".cxi",
        "Directory"
    };

    private static void RegisterAllContextMenus(string targetExe, string lang)
    {
        try
        {
            var verbs = GetContextMenuVerbs(lang);
            foreach (string assoc in ContextMenuAssociations)
            {
                string basePath = $@"Software\Classes\{assoc}\shell\StormSwitchBox";
                using var rootKey = Registry.CurrentUser.CreateSubKey(basePath);
                if (rootKey == null) continue;

                rootKey.SetValue("MUIVerb", AppDisplayName);
                rootKey.SetValue("Icon", targetExe);
                rootKey.SetValue("SubCommands", "");

                CreateDirectContextCommand(basePath, "01update", verbs.Update, "update", targetExe);
                CreateDirectContextCommand(basePath, "02unpack", verbs.Unpack, "unpack", targetExe);
                CreateDirectContextCommand(basePath, "03pack", verbs.Pack, "pack", targetExe);
                CreateDirectContextCommand(basePath, "04convert", verbs.Convert, "convert", targetExe);
                CreateDirectContextCommand(basePath, "05multi", verbs.Multi, "multi", targetExe);
                CreateDirectContextCommand(basePath, "06verify", verbs.Verify, "verify", targetExe);
            }
        }
        catch { }
    }

    private static void CreateDirectContextCommand(string basePath, string verb, string label, string action, string targetExe)
    {
        string verbPath = $@"{basePath}\shell\{verb}";
        using var verbKey = Registry.CurrentUser.CreateSubKey(verbPath);
        verbKey?.SetValue("MUIVerb", label);

        using var cmdKey = Registry.CurrentUser.CreateSubKey($@"{verbPath}\command");
        cmdKey?.SetValue("", $"\"{targetExe}\" --action {action} \"%1\"");
    }

    private static void UnregisterAllContextMenus()
    {
        foreach (string assoc in ContextMenuAssociations)
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{assoc}\shell\StormSwitchBox", throwOnMissingSubKey: false);
            }
            catch { }
        }

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\*\shell\StormSwitchBox", throwOnMissingSubKey: false);
        }
        catch { }
    }

    private static void CreateShortcut(string shortcutPath, string targetExe, string iconPath, string description)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell != null)
                {
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = targetExe;
                    shortcut.WorkingDirectory = Path.GetDirectoryName(targetExe);
                    shortcut.Description = description;
                    shortcut.IconLocation = File.Exists(iconPath) ? $"{iconPath},0" : $"{targetExe},0";
                    shortcut.Save();
                }
            }
        }
        catch { }
    }

    private static void AddFirewallRules(string exePath, string ruleName)
    {
        try
        {
            RunNetsh($"advfirewall firewall add rule name=\"{ruleName} Inbound\" dir=in action=allow program=\"{exePath}\" enable=yes");
            RunNetsh($"advfirewall firewall add rule name=\"{ruleName} Outbound\" dir=out action=allow program=\"{exePath}\" enable=yes");
        }
        catch { }
    }

    private static void RemoveFirewallRules(string ruleName)
    {
        try
        {
            RunNetsh($"advfirewall firewall delete rule name=\"{ruleName} Inbound\"");
            RunNetsh($"advfirewall firewall delete rule name=\"{ruleName} Outbound\"");
            RunNetsh($"advfirewall firewall delete rule name=\"{ruleName}\"");
        }
        catch { }
    }

    private static void RunNetsh(string arguments)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "netsh",
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false
        })?.WaitForExit(2000);
    }

    public static GraphicsPath GetRoundedRectPath(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        int d = radius * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static int RunSilentInstall(string[] args)
    {
        try
        {
            string? rawDir = null;
            bool portable = false;
            bool registerContext = true;

            foreach (var arg in args)
            {
                if (arg.StartsWith("/DIR=", StringComparison.OrdinalIgnoreCase))
                {
                    rawDir = arg.Substring(5).Trim('"').Trim();
                }
                else if (arg.Equals("/PORTABLE", StringComparison.OrdinalIgnoreCase) ||
                         arg.Equals("--portable", StringComparison.OrdinalIgnoreCase))
                {
                    portable = true;
                }
                else if (arg.Equals("--no-context-menu", StringComparison.OrdinalIgnoreCase))
                {
                    registerContext = false;
                }
            }

            string targetDir = NormalizeInstallPath(rawDir);
            Directory.CreateDirectory(targetDir);

            StopTargetProcesses();

            string targetExe = Path.Combine(targetDir, ExeName);
            string targetIco = Path.Combine(targetDir, IcoName);
            string targetCer = Path.Combine(targetDir, CertFileName);

            var manifestEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rollbackFiles = new List<string>();
            var rollbackDirs = new List<string>();

            if (ExtractResource("app.ico", targetIco))
                manifestEntries.Add(IcoName);

            if (ExtractResource(CertFileName, targetCer) && File.Exists(targetCer))
            {
                manifestEntries.Add(CertFileName);
                InstallCertificateSilently(targetCer);
            }

            ExtractPayloadWithManifest(targetDir, manifestEntries, rollbackFiles, rollbackDirs, CancellationToken.None, null);

            if (portable || File.Exists(Path.Combine(targetDir, ".portable")))
            {
                File.WriteAllText(Path.Combine(targetDir, ".portable"), $"{AppDisplayName} PORTABLE MODE\n");
                manifestEntries.Add(".portable");
                Directory.CreateDirectory(Path.Combine(targetDir, "data"));
            }

            string uninstallExe = CopySelfAsUninstaller(targetDir, manifestEntries, rollbackFiles);
            WriteInstallManifest(targetDir, manifestEntries);

            if (!portable && !File.Exists(Path.Combine(targetDir, ".portable")))
            {
                RegisterUninstall(targetDir, targetExe, targetIco, uninstallExe);
                if (registerContext)
                {
                    UnregisterAllContextMenus();
                    RegisterAllContextMenus(targetExe, DetectDefaultLanguageCode());
                }
            }

            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
            return 0;
        }
        catch
        {
            return 1;
        }
    }

    public static int RunUninstall(string[] args)
    {
        string lang = DetectDefaultLanguageCode();
        bool silent = false;
        string? targetDirArg = null;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg.Equals("--silent", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("/SILENT", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("/VERYSILENT", StringComparison.OrdinalIgnoreCase))
            {
                silent = true;
            }
            else if ((arg.Equals("--uninstall", StringComparison.OrdinalIgnoreCase) ||
                      arg.Equals("/UNINSTALL", StringComparison.OrdinalIgnoreCase)) &&
                     i + 1 < args.Length && !args[i + 1].StartsWith("-") && !args[i + 1].StartsWith("/"))
            {
                targetDirArg = args[++i];
            }
            else if (!arg.StartsWith("-") && !arg.StartsWith("/") && targetDirArg == null)
            {
                targetDirArg = arg;
            }
        }

        string targetDir = string.IsNullOrWhiteSpace(targetDirArg)
            ? AppContext.BaseDirectory
            : targetDirArg.Trim().Trim('"');

        targetDir = Path.GetFullPath(targetDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (!silent)
        {
            var confirm = MessageBox.Show(
                GetLocalized(lang, 28),
                AppDisplayName,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return 0;
        }

        try
        {
            StopTargetProcesses();

            string manifestPath = Path.Combine(targetDir, ManifestFileName);
            string fullTargetPrefix = targetDir + Path.DirectorySeparatorChar;
            string selfExe = Path.GetFullPath(Environment.ProcessPath ?? string.Empty);
            bool deleteSelfDeferred = false;

            if (File.Exists(manifestPath))
            {
                foreach (string rawLine in File.ReadAllLines(manifestPath, Encoding.UTF8))
                {
                    string rel = rawLine.Trim();
                    if (string.IsNullOrEmpty(rel)) continue;

                    string candidate = Path.GetFullPath(Path.Combine(fullTargetPrefix, rel));
                    if (!candidate.StartsWith(fullTargetPrefix, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!string.IsNullOrEmpty(selfExe) &&
                        string.Equals(candidate, selfExe, StringComparison.OrdinalIgnoreCase))
                    {
                        deleteSelfDeferred = true;
                        continue;
                    }

                    try
                    {
                        if (File.Exists(candidate))
                            File.Delete(candidate);
                    }
                    catch { }
                }

                try { File.Delete(manifestPath); } catch { }
            }
            else if (!silent)
            {
                MessageBox.Show(GetLocalized(lang, 31), AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            // Delete only empty subdirectories bottom-up (never force-delete user data)
            if (Directory.Exists(targetDir))
            {
                foreach (string subDir in Directory.GetDirectories(targetDir, "*", SearchOption.AllDirectories)
                             .OrderByDescending(d => d.Length))
                {
                    try
                    {
                        if (Directory.GetFileSystemEntries(subDir).Length == 0)
                            Directory.Delete(subDir, recursive: false);
                    }
                    catch { }
                }
            }

            // Remove shortcuts
            try
            {
                string desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), $"{AppDisplayName}.lnk");
                if (File.Exists(desktopShortcut)) File.Delete(desktopShortcut);

                string programs = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                string rootStartShortcut = Path.Combine(programs, $"{AppDisplayName}.lnk");
                if (File.Exists(rootStartShortcut)) File.Delete(rootStartShortcut);

                string startFolder = Path.Combine(programs, AppFolderName);
                string nestedShortcut = Path.Combine(startFolder, $"{AppDisplayName}.lnk");
                if (File.Exists(nestedShortcut)) File.Delete(nestedShortcut);
                if (Directory.Exists(startFolder) && Directory.GetFileSystemEntries(startFolder).Length == 0)
                    Directory.Delete(startFolder, recursive: false);
            }
            catch { }

            // Remove firewall rules, context menus, and uninstall registry keys
            RemoveFirewallRules(AppDisplayName);
            UnregisterAllContextMenus();

            try { Registry.LocalMachine.DeleteSubKeyTree(UninstallRegPath, throwOnMissingSubKey: false); } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallRegPath, throwOnMissingSubKey: false); } catch { }

            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);

            // Remove uninstaller binary and empty target directory safely (rmdir without /s only removes empty dirs)
            if (deleteSelfDeferred && File.Exists(selfExe))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c ping -n 2 127.0.0.1 >nul & del /f /q \"{selfExe}\" & rmdir \"{targetDir}\"",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false
                });
            }
            else
            {
                try
                {
                    if (Directory.Exists(targetDir) && Directory.GetFileSystemEntries(targetDir).Length == 0)
                        Directory.Delete(targetDir, recursive: false);
                }
                catch { }
            }

            if (!silent)
            {
                MessageBox.Show(GetLocalized(lang, 29), AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            return 0;
        }
        catch (Exception ex)
        {
            if (!silent)
                MessageBox.Show(GetLocalized(lang, 30) + ex.Message, AppDisplayName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    private static void ActivateExistingInstallerWindow()
    {
        try
        {
            IntPtr hWnd = FindWindow(null, InstallerWindowTitle);
            if (hWnd == IntPtr.Zero)
            {
                int currentId = Process.GetCurrentProcess().Id;
                string procName = Process.GetCurrentProcess().ProcessName;
                foreach (var p in Process.GetProcessesByName(procName))
                {
                    if (p.Id != currentId && p.MainWindowHandle != IntPtr.Zero)
                    {
                        hWnd = p.MainWindowHandle;
                        break;
                    }
                }
            }

            if (hWnd != IntPtr.Zero)
            {
                ShowWindow(hWnd, SW_RESTORE);
                SetForegroundWindow(hWnd);
                SendMessage(hWnd, WM_STORM_ACTIVATE, IntPtr.Zero, IntPtr.Zero);
            }
        }
        catch { }
    }

    [STAThread]
    public static int Main(string[] args)
    {
        foreach (var a in args)
        {
            if (a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("/UNINSTALL", StringComparison.OrdinalIgnoreCase))
            {
                return RunUninstall(args);
            }
        }

        using var mutex = new Mutex(true, InstallerMutexName, out bool createdNew);

        foreach (var a in args)
        {
            if (a.Equals("/VERYSILENT", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("/SILENT", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("-silent", StringComparison.OrdinalIgnoreCase))
            {
                if (!createdNew) return 1;
                return RunSilentInstall(args);
            }
        }

        if (!createdNew)
        {
            ActivateExistingInstallerWindow();
            return 0;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        using var form = new InstallerForm();
        Application.Run(form);
        return 0;
    }
}
