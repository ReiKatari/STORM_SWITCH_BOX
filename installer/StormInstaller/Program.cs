using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace StormSwitchBox.Installer;

public sealed class VolumetricButton : Button
{
    public Color PrimaryColor { get; set; } = Color.FromArgb(139, 92, 246);
    public Color SecondaryColor { get; set; } = Color.FromArgb(6, 182, 212);
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

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; _pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs mevent) { _pressed = true; Invalidate(); base.OnMouseDown(mevent); }
    protected override void OnMouseUp(MouseEventArgs mevent) { _pressed = false; Invalidate(); base.OnMouseUp(mevent); }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        var g = pevent.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Color.FromArgb(13, 11, 24));

        int offset = _pressed ? 2 : (_hover ? -1 : 0);
        Rectangle rect = new Rectangle(1, 2 + Math.Max(0, offset), Width - 3, Height - 5);
        using var path = InstallerForm.GetRoundedRectPath(rect, 9);

        // 3D Bottom Bevel Shadow
        if (!_pressed)
        {
            Rectangle shadowRect = new Rectangle(1, 4, Width - 3, Height - 5);
            using var shadowPath = InstallerForm.GetRoundedRectPath(shadowRect, 9);
            using var shadowBrush = new SolidBrush(Color.FromArgb(140, 0, 0, 0));
            g.FillPath(shadowBrush, shadowPath);
        }

        // 3D Surface Gradient
        Color c1 = IsPrimary ? PrimaryColor : Color.FromArgb(_hover ? 46 : 38, _hover ? 38 : 28, _hover ? 82 : 62);
        Color c2 = IsPrimary ? SecondaryColor : Color.FromArgb(23, 19, 43);
        using (var grad = new LinearGradientBrush(rect, c1, c2, 135f))
        {
            g.FillPath(grad, path);
        }

        // Top Specular Highlight
        using (var topPen = new Pen(Color.FromArgb(_hover ? 140 : 90, 255, 255, 255), 1.2f))
        {
            g.DrawPath(topPen, path);
        }

        TextRenderer.DrawText(g, Text, Font, rect, ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
}

public sealed class InstallerForm : Form
{
    private const string AppDisplayName = "STORM SWITCH BOX";
    private static readonly string AppVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "6.6.0";
    private const string AppFolderName = "STORM SWITCH BOX";
    private const string ExeName = "StormSwitchBox.exe";
    private const string IcoName = "app.ico";

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

    private CheckBox chkDesktop = null!;
    private CheckBox chkStartMenu = null!;
    private CheckBox chkFirewall = null!;
    private CheckBox chkInstallCert = null!;
    private CheckBox chkRunAfter = null!;

    private ComboBox cmbLang = null!;
    private ComboBox cmbTheme = null!;
    private ComboBox cmbAccent = null!;

    private ProgressBar progressBar = null!;
    private Label lblStatus = null!;
    private VolumetricButton btnCancel = null!;
    private VolumetricButton btnInstall = null!;

    private bool isInstalling = false;
    private Color currentAccent = Color.FromArgb(139, 92, 246);
    private string currentLang = "ru";

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool DeleteFileW(string lpFileName);

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [DllImport("shell32.dll")]
    private static extern void DragAcceptFiles(IntPtr hwnd, bool fAccept);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint DragQueryFile(IntPtr hDrop, uint iFile, System.Text.StringBuilder? lpszFile, int cch);

    [DllImport("shell32.dll")]
    private static extern void DragFinish(IntPtr hDrop);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint message, uint action, IntPtr pChangeFilterStruct);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool ChangeWindowMessageFilter(uint message, uint action);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int wEventId, int uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const uint MSGFLT_ALLOW = 1;
    private const uint WM_DROPFILES = 0x0233;
    private const uint WM_COPYDATA = 0x004A;
    private const uint WM_COPYGLOBALDATA = 0x0049;
    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const int SHCNF_IDLIST = 0x0000;

    private static readonly Dictionary<string, string[]> L10n = new()
    {
        ["ru"] = new[] {
            $"Мастер установки • Версия {AppVersion}",
            "Режим установки:",
            $"📦 Стандартная установка в C:\\Program Files\\{AppFolderName}",
            "⚡ Портативный режим (Portable Mode - папка data/ и .portable, без реестра)",
            "Папка назначения (поддерживается Drag-and-Drop папки из Проводника):",
            "📂 Обзор...",
            "✨ Перетащите любую папку из Проводника Windows прямо в это окно (UAC UIPI активен)",
            "Параметры интеграции (уникальные семантические иконки):",
            $"🖥️  Создать иконку на Рабочем столе ({AppDisplayName}.lnk)",
            "🚀  Установить в меню «Пуск» Windows",
            "🛡️  Добавить в исключения Брандмауэра Windows (Inbound & Outbound)",
            "🔏  Зарегистрировать единый сертификат STORM SERTIFICATE (SIGNATURE)",
            $"⚡  Запустить {AppDisplayName} {AppVersion} после завершения установки",
            "✖ Отмена",
            "⚡ Установить"
        },
        ["en"] = new[] {
            $"Setup Wizard • Version {AppVersion}",
            "Installation Mode:",
            $"📦 Standard Installation to C:\\Program Files\\{AppFolderName}",
            "⚡ Portable Mode (Creates .portable and data/ folder, zero registry writes)",
            "Destination Folder (Drag-and-Drop folder from Explorer supported):",
            "📂 Browse...",
            "✨ Drag & drop any folder from Windows Explorer directly into this window (UAC UIPI enabled)",
            "System Integration Options:",
            $"🖥️  Create Desktop Shortcut ({AppDisplayName}.lnk)",
            "🚀  Install to Windows Start Menu",
            "🛡️  Add Windows Firewall Inbound & Outbound Exceptions",
            "🔏  Register Unified Certificate STORM SERTIFICATE (SIGNATURE)",
            $"⚡  Launch {AppDisplayName} {AppVersion} after installation",
            "✖ Cancel",
            "⚡ Install"
        },
        ["de"] = new[] {
            $"Installationsassistent • Version {AppVersion}",
            "Installationsmodus:",
            $"📦 Standardinstallation in C:\\Program Files\\{AppFolderName}",
            "⚡ Portabler Modus (.portable und data/-Ordner, ohne Registry)",
            "Zielordner (Drag-and-Drop aus dem Explorer unterstützt):",
            "📂 Durchsuchen...",
            "✨ Ziehen Sie einen beliebigen Ordner per Drag-and-Drop in dieses Fenster",
            "Integrationsoptionen:",
            $"🖥️  Desktop-Verknüpfung erstellen ({AppDisplayName}.lnk)",
            "🚀  Im Startmenü installieren",
            "🛡️  Windows-Firewall-Ausnahmen hinzufügen (Inbound & Outbound)",
            "🔏  Einheitliches Zertifikat STORM SERTIFICATE (SIGNATURE) registrieren",
            $"⚡  {AppDisplayName} {AppVersion} nach der Installation starten",
            "✖ Abbrechen",
            "⚡ Installieren"
        },
        ["fr"] = new[] {
            $"Assistant d'installation • Version {AppVersion}",
            "Mode d'installation :",
            $"📦 Installation standard dans C:\\Program Files\\{AppFolderName}",
            "⚡ Mode Portable (Dossier data/ et .portable, sans registre)",
            "Dossier de destination (Glisser-déposer pris en charge) :",
            "📂 Parcourir...",
            "✨ Glissez-déposez un dossier depuis l'Explorateur Windows dans cette fenêtre",
            "Options d'intégration :",
            $"🖥️  Créer un raccourci sur le Bureau ({AppDisplayName}.lnk)",
            "🚀  Installer dans le menu Démarrer",
            "🛡️  Ajouter aux exceptions du Pare-feu Windows (Entrant/Sortant)",
            "🔏  Enregistrer le certificat unifié STORM SERTIFICATE (SIGNATURE)",
            $"⚡  Lancer {AppDisplayName} {AppVersion} après l'installation",
            "✖ Annuler",
            "⚡ Installer"
        },
        ["zh"] = new[] {
            $"安装向导 • 版本 {AppVersion}",
            "安装模式：",
            $"📦 标准安装至 C:\\Program Files\\{AppFolderName}",
            "⚡ 便携模式 (Portable Mode - 创建 .portable 与 data/ 目录，不写注册表)",
            "目标文件夹（支持从资源管理器直接拖放文件夹）：",
            "📂 浏览...",
            "✨ 支持将任意文件夹直接拖入此窗口（已启用 UAC UIPI 穿透）",
            "系统集成选项：",
            $"🖥️  创建桌面快捷方式 ({AppDisplayName}.lnk)",
            "🚀  安装到“开始”菜单",
            "🛡️  添加 Windows 防火墙入站与出站例外规则",
            "🔏  注册统一数字证书 STORM SERTIFICATE (SIGNATURE)",
            $"⚡  安装完成后立即启动 {AppDisplayName} {AppVersion}",
            "✖ 取消",
            "⚡ 安装"
        },
        ["ko"] = new[] {
            $"설치 마법사 • 버전 {AppVersion}",
            "설치 모드:",
            $"📦 표준 설치 (C:\\Program Files\\{AppFolderName})",
            "⚡ 포터블 모드 (.portable 및 data/ 폴더 생성, 레지스트리 미사용)",
            "설치 경로 (탐색기 폴더 드래그 앤 드롭 지원):",
            "📂 찾아보기...",
            "✨ Windows 탐색기에서 폴더를 이 창으로 직접 드래그하세요 (UAC UIPI 우회 적용)",
            "시스템 통합 옵션:",
            $"🖥️  바탕화면 바로가기 생성 ({AppDisplayName}.lnk)",
            "🚀  시작 메뉴에 등록",
            "🛡️  Windows 방화벽 인바운드 및 아웃바운드 예외 추가",
            "🔏  통합 인증서 STORM SERTIFICATE (SIGNATURE) 등록",
            $"⚡  설치 후 {AppDisplayName} {AppVersion} 바로 실행",
            "✖ 취소",
            "⚡ 설치"
        },
        ["ja"] = new[] {
            $"セットアップウィザード • バージョン {AppVersion}",
            "インストールモード:",
            $"📦 標準インストール (C:\\Program Files\\{AppFolderName})",
            "⚡ ポータブルモード (.portable と data/ を作成 - レジストリ不使用)",
            "インストール先フォルダ (エクスプローラーからのドラッグ＆ドロップ対応):",
            "📂 参照...",
            "✨ フォルダをこのウィンドウにドロップしてパスを変更できます (UAC UIPI 対応)",
            "統合オプション:",
            $"🖥️  デスクトップにショートカットを作成 ({AppDisplayName}.lnk)",
            "🚀  スタートメニューに登録",
            "🛡️  Windows ファイアウォールの受信/送信ルールを追加",
            "🔏  統一証明書 STORM SERTIFICATE (SIGNATURE) を登録",
            $"⚡  完了後に {AppDisplayName} {AppVersion} を起動",
            "✖ キャンセル",
            "⚡ インストール"
        },
        ["es"] = new[] {
            $"Asistente de instalación • Versión {AppVersion}",
            "Modo de instalación:",
            $"📦 Instalación estándar en C:\\Program Files\\{AppFolderName}",
            "⚡ Modo Portátil (Crea .portable y data/, sin registro)",
            "Carpeta de destino (Soporta arrastrar y soltar carpeta):",
            "📂 Examinar...",
            "✨ Arrastre cualquier carpeta del Explorador de Windows a esta ventana",
            "Opciones de integración:",
            $"🖥️  Crear icono en el Escritorio ({AppDisplayName}.lnk)",
            "🚀  Instalar en el menú Inicio",
            "🛡️  Añadir excepciones al Firewall de Windows (Entrada/Salida)",
            "🔏  Registrar certificado unificado STORM SERTIFICATE (SIGNATURE)",
            $"⚡  Iniciar {AppDisplayName} {AppVersion} al finalizar",
            "✖ Cancelar",
            "⚡ Instalar"
        },
        ["it"] = new[] {
            $"Procedura d'installazione • Versione {AppVersion}",
            "Modalità d'installazione:",
            $"📦 Installazione standard in C:\\Program Files\\{AppFolderName}",
            "⚡ Modalità Portatile (Crea .portable e data/, senza registro)",
            "Cartella di destinazione (Drag-and-Drop supportato):",
            "📂 Sfoglia...",
            "✨ Trascina qualsiasi cartella da Esplora file in questa finestra",
            "Opzioni di integrazione:",
            $"🖥️  Crea icona sul Desktop ({AppDisplayName}.lnk)",
            "🚀  Installa nel menu Start",
            "🛡️  Aggiungi eccezioni a Windows Firewall (Inbound/Outbound)",
            "🔏  Registra certificato unificato STORM SERTIFICATE (SIGNATURE)",
            $"⚡  Avvia {AppDisplayName} {AppVersion} dopo l'installazione",
            "✖ Annulla",
            "⚡ Installa"
        },
        ["pt"] = new[] {
            $"Assistente de Instalação • Versão {AppVersion}",
            "Modo de instalação:",
            $"📦 Instalação padrão em C:\\Program Files\\{AppFolderName}",
            "⚡ Modo Portátil (Cria .portable e pasta data/, sem registro)",
            "Pasta de destino (Suporta Arrastar e Soltar):",
            "📂 Procurar...",
            "✨ Arraste qualquer pasta do Windows Explorer diretamente para esta janela",
            "Opções de integração:",
            $"🖥️  Criar ícone na Área de Trabalho ({AppDisplayName}.lnk)",
            "🚀  Instalar no menu Iniciar",
            "🛡️  Adicionar exceções no Firewall do Windows (Entrada/Saída)",
            "🔏  Registrar certificado unificado STORM SERTIFICATE (SIGNATURE)",
            $"⚡  Iniciar {AppDisplayName} {AppVersion} após a instalação",
            "✖ Cancelar",
            "⚡ Instalar"
        },
        ["hi"] = new[] {
            $"इंस्टॉलेशन विज़ार्ड • संस्करण {AppVersion}",
            "इंस्टॉलेशन मोड:",
            $"📦 मानक इंस्टॉलेशन (C:\\Program Files\\{AppFolderName})",
            "⚡ पोर्टेबल मोड (.portable और data/ फ़ोल्डर, बिना रजिस्ट्री)",
            "गंतव्य फ़ोल्डर (फ़ोल्डर ड्रैग-एंड-ड्रॉप समर्थित):",
            "📂 ब्राउज़ करें...",
            "✨ विंडोज़ एक्सप्लोरर से किसी भी फ़ोल्डर को सीधे इस विंडो में ड्रॉप करें",
            "एकीकरण विकल्प:",
            $"🖥️  डेस्कटॉप आइकन बनाएँ ({AppDisplayName}.lnk)",
            "🚀  स्टार्ट मेनू में इंस्टॉल करें",
            "🛡️  Windows फ़ायरवॉल इनबाउंड/आउटबाउंड अपवाद जोड़ें",
            "🔏  एकीकृत प्रमाणपत्र STORM SERTIFICATE (SIGNATURE) पंजीकृत करें",
            $"⚡  इंस्टॉलेशन के बाद {AppDisplayName} {AppVersion} चलाएँ",
            "✖ रद्द करें",
            "⚡ इंस्टॉल"
        }
    };

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
        LoadEmbeddedIcon();
        InitializeComponent();
        DetectAndApplyDefaultLanguage();
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            int dark = 1;
            DwmSetWindowAttribute(this.Handle, 20, ref dark, sizeof(int));

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

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == (int)WM_DROPFILES)
        {
            IntPtr hDrop = m.WParam;
            uint count = DragQueryFile(hDrop, 0xFFFFFFFF, null, 0);
            if (count > 0)
            {
                var sb = new System.Text.StringBuilder(1024);
                DragQueryFile(hDrop, 0, sb, sb.Capacity);
                string dropped = sb.ToString();
                if (Directory.Exists(dropped))
                {
                    txtInstallPath.Text = Path.Combine(dropped, AppFolderName);
                }
                else if (File.Exists(dropped))
                {
                    string? dir = Path.GetDirectoryName(dropped);
                    if (!string.IsNullOrEmpty(dir))
                        txtInstallPath.Text = Path.Combine(dir, AppFolderName);
                }
            }
            DragFinish(hDrop);
            return;
        }
        base.WndProc(ref m);
    }

    private void LoadEmbeddedIcon()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            foreach (var name in asm.GetManifestResourceNames())
            {
                if (name.EndsWith("app.ico", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("AppIcon.ico", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith("storm_switch_box.ico", StringComparison.OrdinalIgnoreCase))
                {
                    using var s = asm.GetManifestResourceStream(name);
                    if (s != null)
                    {
                        this.Icon = new Icon(s);
                        return;
                    }
                }
            }
            if (this.Icon == null && !string.IsNullOrEmpty(Application.ExecutablePath) && File.Exists(Application.ExecutablePath))
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
        }
        catch { }
    }

    private void InitializeComponent()
    {
        // 1. FORM SETUP
        this.ClientSize = new Size(840, 580);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.StartPosition = FormStartPosition.CenterScreen;
        this.Text = $"{AppDisplayName} - STORM INSTALLER";
        this.BackColor = Color.FromArgb(13, 11, 24);
        this.Font = new Font("Segoe UI", 9f, FontStyle.Regular);

        // 2. HEADER PANEL (Location = 0, 0, Size = 840, 92)
        headerPanel = new Panel
        {
            Location = new Point(0, 0),
            Size = new Size(840, 92),
            BackColor = Color.FromArgb(20, 16, 38)
        };
        headerPanel.Paint += HeaderPanel_Paint;

        lblTitle = new Label
        {
            Text = AppDisplayName,
            Font = new Font("Segoe UI", 15.5f, FontStyle.Bold),
            ForeColor = Color.White,
            Location = new Point(88, 18),
            AutoSize = true,
            UseMnemonic = false
        };
        headerPanel.Controls.Add(lblTitle);

        lblSubtitle = new Label
        {
            Text = $"Мастер установки • Версия {AppVersion}",
            Font = new Font("Segoe UI", 9.2f, FontStyle.Regular),
            ForeColor = currentAccent,
            Location = new Point(90, 48),
            AutoSize = true,
            UseMnemonic = false
        };
        headerPanel.Controls.Add(lblSubtitle);

        // Dropdowns shifted right to X=504 per standard (>170px clearance)
        cmbLang = new ComboBox
        {
            Location = new Point(504, 16),
            Size = new Size(150, 24),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(28, 24, 48),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        cmbLang.Items.AddRange(new object[] {
            "Русский (ru)", "English (en)", "Deutsch (de)", "Français (fr)",
            "简体中文 (zh)", "한국어 (ko)", "日本語 (ja)", "Español (es)",
            "Italiano (it)", "Português (pt)", "हिन्दी (hi)"
        });
        cmbLang.SelectedIndex = 0;
        cmbLang.SelectedIndexChanged += (s, e) => ChangeLanguageByIndex(cmbLang.SelectedIndex);
        headerPanel.Controls.Add(cmbLang);

        cmbTheme = new ComboBox
        {
            Location = new Point(662, 16),
            Size = new Size(156, 24),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(28, 24, 48),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        cmbTheme.Items.AddRange(new object[] {
            "MIDNIGHT (Фиолет)", "NIGHT (OLED)", "DARK (Тёмная)", "DAY (Светлая)",
            "FANTASY (Индиго)", "CYBERPUNK (2077)", "WARHAMMER 40K", "ELEMENTAL", "STORM (Шторм)"
        });
        cmbTheme.SelectedIndex = 0;
        cmbTheme.SelectedIndexChanged += (s, e) => ApplyTheme(cmbTheme.SelectedItem?.ToString()?.Split(' ')[0] ?? "MIDNIGHT");
        headerPanel.Controls.Add(cmbTheme);

        cmbAccent = new ComboBox
        {
            Location = new Point(504, 46),
            Size = new Size(314, 24),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.FromArgb(28, 24, 48),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
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

        // 3. BODY PANEL (Location = 22, 102, Size = 796, 400)
        bodyPanel = new Panel
        {
            Location = new Point(22, 102),
            Size = new Size(796, 400),
            BackColor = Color.Transparent
        };

        lblModeTitle = new Label
        {
            Text = "Режим установки:",
            Font = new Font("Segoe UI", 9.8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(226, 232, 240),
            Location = new Point(0, 2),
            UseMnemonic = false,
            AutoSize = true
        };
        bodyPanel.Controls.Add(lblModeTitle);

        rbStandard = new RadioButton
        {
            Text = $"📦 Стандартная установка в C:\\Program Files\\{AppFolderName}",
            Checked = true,
            Location = new Point(8, 26),
            AutoSize = true,
            UseMnemonic = false,
            ForeColor = Color.White
        };
        rbStandard.CheckedChanged += Mode_CheckedChanged;
        bodyPanel.Controls.Add(rbStandard);

        rbPortable = new RadioButton
        {
            Text = "⚡ Портативный режим (Portable Mode - папка data/ и .portable, без реестра)",
            Location = new Point(8, 50),
            AutoSize = true,
            UseMnemonic = false,
            ForeColor = Color.FromArgb(203, 213, 225)
        };
        rbPortable.CheckedChanged += Mode_CheckedChanged;
        bodyPanel.Controls.Add(rbPortable);

        lblPathTitle = new Label
        {
            Text = "Папка назначения (поддерживается Drag-and-Drop папки из Проводника):",
            Font = new Font("Segoe UI", 9.8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(226, 232, 240),
            Location = new Point(0, 80),
            UseMnemonic = false,
            AutoSize = true
        };
        bodyPanel.Controls.Add(lblPathTitle);

        txtInstallPath = new TextBox
        {
            Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppFolderName),
            Location = new Point(4, 105),
            Size = new Size(656, 26),
            BackColor = Color.FromArgb(24, 20, 44),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        bodyPanel.Controls.Add(txtInstallPath);

        btnBrowse = new VolumetricButton
        {
            Text = "📂 Обзор...",
            Location = new Point(668, 103),
            Size = new Size(124, 30),
            ForeColor = Color.White
        };
        btnBrowse.Click += BtnBrowse_Click;
        bodyPanel.Controls.Add(btnBrowse);

        lblDropHint = new Label
        {
            Text = "✨ Перетащите любую папку из Проводника Windows прямо в это окно (UAC UIPI активен)",
            Font = new Font("Segoe UI", 8.6f, FontStyle.Italic),
            ForeColor = Color.FromArgb(56, 189, 248),
            Location = new Point(4, 136),
            UseMnemonic = false,
            AutoSize = true
        };
        bodyPanel.Controls.Add(lblDropHint);

        lblOptionsTitle = new Label
        {
            Text = "Параметры интеграции (уникальные семантические иконки):",
            Font = new Font("Segoe UI", 9.8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(226, 232, 240),
            Location = new Point(0, 164),
            UseMnemonic = false,
            AutoSize = true
        };
        bodyPanel.Controls.Add(lblOptionsTitle);

        chkDesktop = new CheckBox
        {
            Text = $"🖥️  Создать ярлык на Рабочем столе ({AppDisplayName}.lnk)",
            Checked = true,
            Location = new Point(8, 190),
            UseMnemonic = false,
            AutoSize = true,
            ForeColor = Color.White
        };
        bodyPanel.Controls.Add(chkDesktop);

        chkStartMenu = new CheckBox
        {
            Text = "📌  Добавить в меню «Пуск» и список приложений Windows",
            Checked = true,
            Location = new Point(8, 217),
            UseMnemonic = false,
            AutoSize = true,
            ForeColor = Color.White
        };
        bodyPanel.Controls.Add(chkStartMenu);

        chkFirewall = new CheckBox
        {
            Text = "🛡️  Добавить в исключения Брандмауэра Windows (Inbound & Outbound)",
            Checked = true,
            Location = new Point(8, 244),
            UseMnemonic = false,
            AutoSize = true,
            ForeColor = Color.FromArgb(56, 189, 248)
        };
        bodyPanel.Controls.Add(chkFirewall);

        chkInstallCert = new CheckBox
        {
            Text = "🔐  Установить единый сертификат доверия STORM SERTIFICATE (SIGNATURE)",
            Checked = true,
            Location = new Point(8, 271),
            UseMnemonic = false,
            AutoSize = true,
            ForeColor = Color.FromArgb(52, 211, 153)
        };
        bodyPanel.Controls.Add(chkInstallCert);

        chkRunAfter = new CheckBox
        {
            Text = $"⚡  Запустить {AppDisplayName} сразу после завершения установки",
            Checked = true,
            Location = new Point(8, 298),
            UseMnemonic = false,
            AutoSize = true,
            ForeColor = Color.FromArgb(250, 204, 21)
        };
        bodyPanel.Controls.Add(chkRunAfter);

        // Индикатор установки внутри bodyPanel per standard
        progressBar = new ProgressBar
        {
            Location = new Point(4, 336),
            Size = new Size(788, 14),
            Style = ProgressBarStyle.Continuous,
            Value = 0,
            Visible = false
        };
        bodyPanel.Controls.Add(progressBar);

        lblStatus = new Label
        {
            Text = "",
            Location = new Point(4, 355),
            Size = new Size(788, 22),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(52, 211, 153),
            UseMnemonic = false,
            Visible = false
        };
        bodyPanel.Controls.Add(lblStatus);

        this.Controls.Add(bodyPanel);

        // 4. BOTTOM PANEL (Dock = Bottom, Height = 64)
        bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 64,
            BackColor = Color.FromArgb(20, 16, 38),
            Padding = new Padding(22, 12, 22, 12)
        };

        btnCancel = new VolumetricButton
        {
            Text = "✖ Отмена",
            Size = new Size(130, 38),
            Location = new Point(516, 13),
            ForeColor = Color.White
        };
        btnCancel.Click += (s, e) => this.Close();
        bottomPanel.Controls.Add(btnCancel);

        btnInstall = new VolumetricButton
        {
            Text = "⚡ Установить",
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

    private Bitmap? _headerLogoBitmap;

    private void HeaderPanel_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // Draw authentic 3D logo icon
        Rectangle iconRect = new Rectangle(24, 16, 56, 56);
        if (_headerLogoBitmap == null && this.Icon != null)
        {
            try
            {
                using var bmp = this.Icon.ToBitmap();
                _headerLogoBitmap = new Bitmap(bmp, new Size(56, 56));
            }
            catch { }
        }

        if (_headerLogoBitmap != null)
        {
            // Soft neon glow under logo
            using var shadowPath = GetRoundedRectPath(new Rectangle(22, 14, 60, 60), 16);
            using var glowBrush = new SolidBrush(Color.FromArgb(45, currentAccent.R, currentAccent.G, currentAccent.B));
            g.FillPath(glowBrush, shadowPath);

            g.DrawImage(_headerLogoBitmap, iconRect);
        }
        else
        {
            using var iconPath = GetRoundedRectPath(iconRect, 14);
            using var iconGrad = new LinearGradientBrush(iconRect, currentAccent, Color.FromArgb(6, 182, 212), 135f);
            g.FillPath(iconGrad, iconPath);
        }

        // Bottom separator
        using var sepPen = new Pen(Color.FromArgb(40, 32, 64), 1f);
        g.DrawLine(sepPen, 0, headerPanel.Height - 1, headerPanel.Width, headerPanel.Height - 1);
    }

    private void DetectAndApplyDefaultLanguage()
    {
        try
        {
            string uiLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToLowerInvariant();
            int idx = uiLang switch
            {
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
                _ => 0 // ru default
            };
            cmbLang.SelectedIndex = idx;
        }
        catch
        {
            cmbLang.SelectedIndex = 0;
        }
    }

    private void ChangeLanguageByIndex(int index)
    {
        string[] langs = { "ru", "en", "de", "fr", "zh", "ko", "ja", "es", "it", "pt", "hi" };
        currentLang = langs[Math.Clamp(index, 0, langs.Length - 1)];

        if (L10n.TryGetValue(currentLang, out var t))
        {
            lblSubtitle.Text = t[0];
            lblModeTitle.Text = t[1];
            rbStandard.Text = t[2];
            rbPortable.Text = t[3];
            lblPathTitle.Text = t[4];
            btnBrowse.Text = t[5];
            lblDropHint.Text = t[6];
            lblOptionsTitle.Text = t[7];
            chkDesktop.Text = t[8];
            chkStartMenu.Text = t[9];
            chkFirewall.Text = t[10];
            chkInstallCert.Text = t[11];
            chkRunAfter.Text = t[12];
            btnCancel.Text = t[13];
            btnInstall.Text = t[14];
        }
    }

    private void ApplyTheme(string theme)
    {
        (Color bg, Color hdr, Color fg) = theme switch
        {
            "NIGHT" => (Color.FromArgb(0, 0, 0), Color.FromArgb(6, 6, 9), Color.White),
            "DARK" => (Color.FromArgb(17, 19, 24), Color.FromArgb(24, 28, 36), Color.White),
            "DAY" => (Color.FromArgb(241, 245, 249), Color.FromArgb(226, 232, 240), Color.FromArgb(15, 23, 42)),
            "FANTASY" => (Color.FromArgb(14, 19, 36), Color.FromArgb(22, 30, 56), Color.FromArgb(254, 249, 195)),
            "CYBERPUNK" => (Color.FromArgb(11, 10, 16), Color.FromArgb(22, 16, 34), Color.FromArgb(252, 238, 9)),
            "WARHAMMER 40K" => (Color.FromArgb(12, 10, 8), Color.FromArgb(26, 19, 15), Color.FromArgb(254, 240, 138)),
            "ELEMENTAL" => (Color.FromArgb(9, 19, 24), Color.FromArgb(14, 30, 38), Color.White),
            "STORM" => (Color.FromArgb(7, 5, 16), Color.FromArgb(16, 11, 36), Color.White),
            _ => (Color.FromArgb(13, 11, 24), Color.FromArgb(20, 16, 38), Color.White)
        };

        this.BackColor = bg;
        headerPanel.BackColor = hdr;
        bottomPanel.BackColor = hdr;
        lblTitle.ForeColor = fg;
        lblModeTitle.ForeColor = fg;
        lblPathTitle.ForeColor = fg;
        lblOptionsTitle.ForeColor = fg;
        rbStandard.ForeColor = fg;
        rbPortable.ForeColor = fg;
        chkDesktop.ForeColor = fg;
        chkStartMenu.ForeColor = fg;
        headerPanel.Invalidate();
        btnBrowse.Invalidate();
        btnCancel.Invalidate();
        btnInstall.Invalidate();
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
        headerPanel.Invalidate();
        btnInstall.Invalidate();
    }

    private void Mode_CheckedChanged(object? sender, EventArgs e)
    {
        if (isInstalling) return;
        if (rbPortable.Checked)
        {
            txtInstallPath.Text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{AppFolderName} PORTABLE");
            chkDesktop.Checked = false;
            chkStartMenu.Checked = false;
        }
        else
        {
            txtInstallPath.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppFolderName);
            chkDesktop.Checked = true;
            chkStartMenu.Checked = true;
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
            txtInstallPath.Text = Path.Combine(fbd.SelectedPath, AppFolderName);
        }
    }

    private async void BtnInstall_Click(object? sender, EventArgs e)
    {
        if (isInstalling) return;
        isInstalling = true;
        btnInstall.Enabled = false;
        progressBar.Visible = true;
        lblStatus.Visible = true;

        try
        {
            string targetDir = txtInstallPath.Text.Trim();
            if (string.IsNullOrEmpty(targetDir))
            {
                targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppFolderName);
            }
            Directory.CreateDirectory(targetDir);

            // Шаг 1: Остановка запущенных процессов
            lblStatus.Text = $"Остановка запущенных процессов {AppDisplayName}...";
            progressBar.Value = 10;
            await Task.Delay(120);

            foreach (var p in Process.GetProcessesByName("StormSwitchBox"))
            {
                try { p.Kill(); p.WaitForExit(1500); } catch { }
            }

            string targetExe = Path.Combine(targetDir, ExeName);
            string targetIco = Path.Combine(targetDir, IcoName);
            string targetCer = Path.Combine(targetDir, "STORM_SERTIFICATE.cer");

            // Извлекаем иконку app.ico в каталог установки для ярлыков и панели управления
            ExtractResource("app.ico", targetIco);

            // Шаг 2: Установка сертификата STORM
            if (chkInstallCert.Checked)
            {
                lblStatus.Text = "Регистрация единого сертификата STORM SERTIFICATE (SIGNATURE)...";
                progressBar.Value = 25;
                await Task.Delay(100);
                ExtractResource("STORM_SERTIFICATE.cer", targetCer);
                if (File.Exists(targetCer))
                {
                    InstallCertificateSilently(targetCer);
                }
            }

            // Шаг 3: Распаковка дистрибутива
            lblStatus.Text = $"Распаковка файлов {AppDisplayName} {AppVersion}...";
            progressBar.Value = 40;
            await Task.Delay(100);

            await Task.Run(() =>
            {
                var asm = Assembly.GetExecutingAssembly();
                foreach (var name in asm.GetManifestResourceNames())
                {
                    if (name.EndsWith("Payload.zip", StringComparison.OrdinalIgnoreCase))
                    {
                        using var s = asm.GetManifestResourceStream(name);
                        if (s != null)
                        {
                            using var zip = new ZipArchive(s, ZipArchiveMode.Read);
                            int total = Math.Max(1, zip.Entries.Count);
                            int done = 0;
                            int lastReportedPct = 40;
                            foreach (var entry in zip.Entries)
                            {
                                if (string.IsNullOrEmpty(entry.Name))
                                {
                                    Directory.CreateDirectory(Path.Combine(targetDir, entry.FullName));
                                    continue;
                                }

                                string outPath = Path.Combine(targetDir, entry.FullName);
                                string? outDir = Path.GetDirectoryName(outPath);
                                if (!string.IsNullOrEmpty(outDir))
                                    Directory.CreateDirectory(outDir);

                                entry.ExtractToFile(outPath, overwrite: true);
                                try { DeleteFileW(outPath + ":Zone.Identifier"); } catch { }
                                done++;
                                int pct = 40 + (int)((float)done / total * 45);
                                if (pct != lastReportedPct)
                                {
                                    lastReportedPct = pct;
                                    this.Invoke(new Action(() => progressBar.Value = Math.Min(85, pct)));
                                }
                            }
                        }
                    }
                }
            });

            // Портативный режим
            if (rbPortable.Checked)
            {
                File.WriteAllText(Path.Combine(targetDir, ".portable"), $"{AppDisplayName} PORTABLE MODE\n");
                Directory.CreateDirectory(Path.Combine(targetDir, "data"));
            }

            // Шаг 4: Создание ярлыков
            lblStatus.Text = "Создание ярлыков и регистрация в Windows...";
            progressBar.Value = 90;
            await Task.Delay(80);

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

            // Регистрация в реестре Windows (Установленные приложения / DisplayIcon)
            if (!rbPortable.Checked)
            {
                RegisterUninstall(targetDir, targetExe, targetIco);
            }

            // Шаг 5: Настройка Брандмауэра
            if (chkFirewall.Checked)
            {
                lblStatus.Text = "Настройка правил Брандмауэра Windows (Inbound & Outbound)...";
                progressBar.Value = 95;
                await Task.Delay(80);
                AddFirewallRules(targetExe, AppDisplayName);
            }

            // Шаг 6: Финализация
            progressBar.Value = 100;
            lblStatus.Text = $"✨ {AppDisplayName} {AppVersion} успешно установлен!";
            btnInstall.Text = "✓ Готово";
            btnInstall.Enabled = false;
            btnCancel.Text = "Закрыть";
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
            this.Close();
        }
        catch (Exception ex)
        {
            progressBar.Value = 100;
            lblStatus.ForeColor = Color.FromArgb(239, 68, 68);
            lblStatus.Text = $"✗ Ошибка установки: {ex.Message}";
            btnInstall.Enabled = true;
            isInstalling = false;
        }
    }

    private void ExtractResource(string resName, string targetPath)
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
                    }
                    return;
                }
            }
        }
        catch { }
    }

    private void InstallCertificateSilently(string certPath)
    {
        try
        {
            using var cert = new X509Certificate2(certPath);
            using var store = new X509Store(StoreName.Root, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            store.Add(cert);
        }
        catch { }
        try
        {
            using var cert = new X509Certificate2(certPath);
            using var store = new X509Store(StoreName.TrustedPublisher, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadWrite);
            store.Add(cert);
        }
        catch { }
    }

    private void RegisterUninstall(string targetDir, string targetExe, string targetIco)
    {
        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\STORM_SWITCH_BOX")
                         ?? Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\STORM_SWITCH_BOX");
            if (key != null)
            {
                key.SetValue("DisplayName", $"{AppDisplayName} {AppVersion}");
                key.SetValue("DisplayVersion", AppVersion);
                key.SetValue("Publisher", "STORM TEAM");
                key.SetValue("DisplayIcon", File.Exists(targetIco) ? $"{targetIco},0" : $"{targetExe},0");
                key.SetValue("InstallLocation", targetDir);
                key.SetValue("UninstallString", $"cmd.exe /c rmdir /s /q \"{targetDir}\"");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }
        catch { }
    }

    private void CreateShortcut(string shortcutPath, string targetExe, string iconPath, string description)
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
                    if (File.Exists(iconPath))
                        shortcut.IconLocation = $"{iconPath},0";
                    else
                        shortcut.IconLocation = $"{targetExe},0";
                    shortcut.Save();
                }
            }
        }
        catch { }
    }

    private void AddFirewallRules(string exePath, string ruleName)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall add rule name=\"{ruleName} Inbound\" dir=in action=allow program=\"{exePath}\" enable=yes",
                CreateNoWindow = true,
                UseShellExecute = false
            })?.WaitForExit(2000);

            Process.Start(new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall add rule name=\"{ruleName} Outbound\" dir=out action=allow program=\"{exePath}\" enable=yes",
                CreateNoWindow = true,
                UseShellExecute = false
            })?.WaitForExit(2000);
        }
        catch { }
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
            string targetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppFolderName);
            foreach (var arg in args)
            {
                if (arg.StartsWith("/DIR=", StringComparison.OrdinalIgnoreCase))
                {
                    string custom = arg.Substring(5).Trim('"').Trim();
                    if (!string.IsNullOrWhiteSpace(custom))
                        targetDir = custom;
                }
            }
            Directory.CreateDirectory(targetDir);

            foreach (var p in Process.GetProcessesByName("StormSwitchBox"))
            {
                try { p.Kill(); p.WaitForExit(1500); } catch { }
            }

            string targetExe = Path.Combine(targetDir, ExeName);
            string targetIco = Path.Combine(targetDir, IcoName);
            string targetCer = Path.Combine(targetDir, "STORM_SERTIFICATE.cer");

            var form = new InstallerForm();
            form.ExtractResource("app.ico", targetIco);
            form.ExtractResource("STORM_SERTIFICATE.cer", targetCer);
            if (File.Exists(targetCer))
                form.InstallCertificateSilently(targetCer);

            var asm = Assembly.GetExecutingAssembly();
            foreach (var name in asm.GetManifestResourceNames())
            {
                if (name.EndsWith("Payload.zip", StringComparison.OrdinalIgnoreCase))
                {
                    using var s = asm.GetManifestResourceStream(name);
                    if (s != null)
                    {
                        using var zip = new ZipArchive(s, ZipArchiveMode.Read);
                        foreach (var entry in zip.Entries)
                        {
                            if (string.IsNullOrEmpty(entry.Name))
                            {
                                Directory.CreateDirectory(Path.Combine(targetDir, entry.FullName));
                                continue;
                            }
                            string outPath = Path.Combine(targetDir, entry.FullName);
                            string? outDir = Path.GetDirectoryName(outPath);
                            if (!string.IsNullOrEmpty(outDir))
                                Directory.CreateDirectory(outDir);
                            entry.ExtractToFile(outPath, overwrite: true);
                            try { DeleteFileW(outPath + ":Zone.Identifier"); } catch { }
                        }
                    }
                }
            }

            if (!File.Exists(Path.Combine(targetDir, ".portable")))
            {
                form.RegisterUninstall(targetDir, targetExe, targetIco);
            }
            return 0;
        }
        catch
        {
            return 1;
        }
    }

    [STAThread]
    public static int Main(string[] args)
    {
        foreach (var a in args)
        {
            if (a.Equals("/VERYSILENT", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("/SILENT", StringComparison.OrdinalIgnoreCase) ||
                a.Equals("-silent", StringComparison.OrdinalIgnoreCase))
            {
                return RunSilentInstall(args);
            }
        }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new InstallerForm());
        return 0;
    }
}
