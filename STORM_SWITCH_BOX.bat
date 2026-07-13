<# ::1.4.000
@echo off&pushd "%~dp0"&set "arg1=%~1"&set "arg2=%~2"&powershell -WindowStyle Minimized -ExecutionPolicy Bypass -c "iex ((Get-Content '%~f0' -Encoding utf8) -join [Environment]::Newline);YE"&exit
#>

# Упрощение имен типов для улучшения читаемости
using namespace System.Windows.Forms
using namespace System.Drawing
using namespace System.Drawing.Drawing2D
using namespace System.IO

# Проверка всех необходимых зависимостей перед запуском
function Check-Dependencies {
    $missing = @()
    $downloadUrls = @{}
    
    if ($PSVersionTable.PSVersion.Major -lt 5) {
        $missing += "Windows PowerShell 5.1+"
    }
    
    $netRelease = 0
    try { $netRelease = (Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" -Name "Release" -ErrorAction SilentlyContinue).Release } catch {}
    if (-not $netRelease -or $netRelease -lt 460798) {
        $missing += ".NET Framework 4.7 или новее"
        $downloadUrls[".NET Framework 4.8"] = "https://dotnet.microsoft.com/en-us/download/dotnet-framework/thank-you/net48-web-installer"
    }
    
    $vcFound = (Test-Path "$env:windir\System32\vcruntime140.dll") -or (Test-Path "$env:windir\System32\msvcp140.dll")
    if (-not $vcFound) {
        $missing += "Visual C++ Redistributable (2015-2022)"
        $downloadUrls["Visual C++ Redistributable"] = "https://aka.ms/vs/17/release/vc_redist.x64.exe"
    }
    
    foreach ($dll in @("user32.dll", "dwmapi.dll", "uxtheme.dll")) {
        if (-not (Test-Path "$env:windir\System32\$dll")) {
            $missing += "Системная библиотека $dll"
        }
    }
    
    if ($missing.Count -gt 0) {
        $msg = "ВНИМАНИЕ! Для корректной работы STORM SWITCH BOX отсутствуют следующие зависимости:`n"
        foreach ($m in $missing) { $msg += " - $m`n" }
        
        if ($downloadUrls.Count -gt 0) {
            $msg += "`nЖелаете скачать и установить недостающие компоненты прямо сейчас?"
            $wshell = New-Object -ComObject Wscript.Shell
            $res = $wshell.Popup($msg, 0, "STORM SWITCH BOX - Отсутствуют компоненты", 4 + 48) # 4=Yes/No, 48=Warning
            if ($res -eq 6) {
                foreach ($url in $downloadUrls.Values) {
                    Start-Process $url
                }
            }
        } else {
            $msg += "`nПожалуйста, установите их для продолжения работы."
            $wshell = New-Object -ComObject Wscript.Shell
            $wshell.Popup($msg, 0, "STORM SWITCH BOX - Ошибка", 16) # 16=Error
        }
        exit
    }
}
Check-Dependencies

# Определение кастомных контролов для стилизации меню
$csharpCode = @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;
namespace YanuExt.CustomControls
{
    public class CustomColorTable : ProfessionalColorTable
    {
        public override Color MenuBorder { get { return Color.FromArgb(255, 60, 60, 60); } }
        public override Color MenuItemBorder { get { return Color.FromArgb(255, 85, 85, 85); } }
        public override Color MenuItemSelected { get { return Color.FromArgb(255, 85, 85, 85); } }
        public override Color MenuItemPressedGradientBegin { get { return Color.FromArgb(255, 45, 45, 48); } }
        public override Color MenuItemPressedGradientEnd { get { return Color.FromArgb(255, 45, 45, 48); } }
        public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(255, 85, 85, 85); } }
        public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(255, 85, 85, 85); } }
        public override Color ImageMarginGradientBegin { get { return Color.FromArgb(255, 45, 45, 48); } }
        public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(255, 45, 45, 48); } }
        public override Color ImageMarginGradientEnd { get { return Color.FromArgb(255, 45, 45, 48); } }
        public override Color ToolStripDropDownBackground { get { return Color.FromArgb(255, 45, 45, 48); } }
        public override Color SeparatorDark { get { return Color.FromArgb(255, 90, 90, 90); } }
    }
    public class CustomMenuRenderer : ToolStripProfessionalRenderer
    {
        public CustomMenuRenderer() : base(new CustomColorTable()) {}
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Color.White;
            base.OnRenderItemText(e);
        }
    }
    // Классы для создания индикатора выполнения в таблице
    public class DataGridViewProgressColumn : DataGridViewColumn
    {
        public DataGridViewProgressColumn() : base(new DataGridViewProgressCell()) { }
        public override DataGridViewCell CellTemplate
        {
            get { return base.CellTemplate; }
            set
            {
                if (value != null && !value.GetType().IsAssignableFrom(typeof(DataGridViewProgressCell)))
                {
                    throw new InvalidCastException("Must be a DataGridViewProgressCell");
                }
                base.CellTemplate = value;
            }
        }
    }
    
    // Класс для новой колонки с процентами
    public class DataGridViewPercentageColumn : DataGridViewColumn
    {
        public DataGridViewPercentageColumn() : base(new DataGridViewPercentageCell()) { }
        public override DataGridViewCell CellTemplate
        {
            get { return base.CellTemplate; }
            set
            {
                if (value != null && !value.GetType().IsAssignableFrom(typeof(DataGridViewPercentageCell)))
                {
                    throw new InvalidCastException("Must be a DataGridViewPercentageCell");
                }
                base.CellTemplate = value;
            }
        }
    }
    
    public class DataGridViewPercentageCell : DataGridViewTextBoxCell
    {
        protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex, DataGridViewElementStates cellState, object value, object formattedValue, string errorText, DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
        {
            // Рисуем фон и рамку
            base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText, cellStyle, advancedBorderStyle, paintParts & ~DataGridViewPaintParts.ContentForeground);
            int progressVal = 0;
            bool isInt = value is int;
            if (isInt || (value != null && int.TryParse(value.ToString(), out progressVal)))
            {
                if (isInt) 
                {
                    progressVal = Convert.ToInt32(value);
                }
                
                if (progressVal < 0) progressVal = 0;
                if (progressVal > 100) progressVal = 100;
                
                float percentage = (float)progressVal / 100.0f;
                Brush foreColorBrush = new SolidBrush(Color.FromArgb(255, 76, 175, 80)); // Зеленый
                Brush backColorBrush = new SolidBrush(cellStyle.BackColor);
                // Рисуем индикатор выполнения
                if ((paintParts & DataGridViewPaintParts.ContentForeground) != 0)
                {
                    RectangleF progressBarBounds = new RectangleF(cellBounds.X + 2, cellBounds.Y + 2, cellBounds.Width - 4, cellBounds.Height - 4);
                    graphics.FillRectangle(backColorBrush, progressBarBounds);
                    graphics.FillRectangle(foreColorBrush, progressBarBounds.X, progressBarBounds.Y, progressBarBounds.Width * percentage, progressBarBounds.Height);
                    // Рисуем текст с процентами
                    string text = progressVal.ToString() + "%";
                    TextRenderer.DrawText(graphics, text, cellStyle.Font, Rectangle.Round(progressBarBounds), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                
                foreColorBrush.Dispose();
                backColorBrush.Dispose();
            }
        }
    }
    public class DataGridViewProgressCell : DataGridViewTextBoxCell
    {
        protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex, DataGridViewElementStates cellState, object value, object formattedValue, string errorText, DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle, DataGridViewPaintParts paintParts)
        {
            // Сначала рисуем фон и рамку ячейки
            base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText, cellStyle, advancedBorderStyle, paintParts & ~DataGridViewPaintParts.ContentForeground);
            string text = formattedValue as string;
            // Если значение - это текст (статус), просто рисуем текст
            if (!string.IsNullOrEmpty(text) && !(value is int))
            {
                if ((paintParts & DataGridViewPaintParts.ContentForeground) != 0)
                {
                    TextRenderer.DrawText(graphics, text, cellStyle.Font, cellBounds, cellStyle.ForeColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                return;
            }
            // Если это число, рисуем ProgressBar (старый вариант, для обратной совместимости)
            if (value is int)
            {
                Rectangle progressBarBounds = new Rectangle(cellBounds.X + 2, cellBounds.Y + 2, cellBounds.Width - 4, cellBounds.Height - 4);
                ProgressBarRenderer.DrawHorizontalBar(graphics, progressBarBounds);
                
                Rectangle marqueeBounds = new Rectangle(progressBarBounds.X, progressBarBounds.Y, (int)(progressBarBounds.Width), progressBarBounds.Height);
                ProgressBarRenderer.DrawHorizontalChunks(graphics, marqueeBounds);
            }
        }
    }
}
public class User32 {
    [DllImport("user32.dll")]
    public static extern bool ChangeWindowMessageFilterEx(IntPtr hWnd, uint msg, uint action, IntPtr pChangeFilterStruct);
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
    public const uint WM_DROPFILES = 0x0233;
    public const uint WM_COPYDATA = 0x004A;
    public const uint WM_COPYGLOBALDATA = 0x0049;
    public const uint MSGFLT_ALLOW = 1;
}
public class DwmApi {
    [DllImport("dwmapi.dll", PreserveSig = true)]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
}
public class UxTheme {
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    public static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string pszSubIdList);
}
"@
Add-Type -TypeDefinition $csharpCode -ReferencedAssemblies System.Windows.Forms, System.Drawing, System.IO.Compression.FileSystem
Add-Type -AssemblyName Microsoft.VisualBasic

function YE {
$script:DebugMode = $false

#====================================================================================
#  БЛОК КОДА ДЛЯ ФОНОВЫХ ПРОЦЕССОВ (ВОРКЕРОВ)
#====================================================================================
# (Req 1) Блок функций воркера (ПОЛНАЯ ВЕРСИЯ С ОБНОВЛЕННЫМ ЗАГОЛОВКОМ)
$script:WorkerFunctionsBlock = @'
# ОПТИМИЗАЦИЯ: Создаем кэш для информации о файлах
$script:nspInfoCache = [System.Collections.Concurrent.ConcurrentDictionary[string, object]]::new()
$script:currentStage = ""
$script:stageStartTime = Get-Date

# Включаем режим UTF-8 для Python (решает проблему с [Errno 22] при парсинге списков squirrel)
$env:PYTHONUTF8="1"

function Write-WorkerLog {
    param([string]$Message, [string]$Type='INFO')
    
    # Ультра-современные иконки и цвета
    $icon = switch ($Type.ToUpper()) { 
        'SUCCESS'    { '✅'; $color = 'Green'; $bgSymbol = '█' }
        'ERROR'      { '❌'; $color = 'Red'; $bgSymbol = '▓' }
        'TOOL_ERROR' { '⚠️'; $color = 'DarkRed'; $bgSymbol = '▓' }
        'WARN'       { '⚡'; $color = 'Yellow'; $bgSymbol = '░' }
        'DEBUG'      { '🔍'; $color = 'DarkGray'; $bgSymbol = '·' }
        'STAGE'      { '🚀'; $color = 'Cyan'; $bgSymbol = '▶' }
        'PROGRESS'   { '📊'; $color = 'Blue'; $bgSymbol = '█' }
        'FILE'       { '📁'; $color = 'DarkCyan'; $bgSymbol = '▸' }
        default      { '●'; $color = 'Gray'; $bgSymbol = '·' }
    }
    
    $timestamp = Get-Date -Format 'HH:mm:ss.ff'
    
    # Если это новый этап - выводим красивый разделитель
    if ($Type.ToUpper() -eq 'STAGE') {
        if ($script:currentStage) {
            $elapsed = ((Get-Date) - $script:stageStartTime).TotalSeconds
            Write-Host ""
            Write-Host "  └──" -NoNewline -ForegroundColor DarkGray
            Write-Host " ✓ Завершено за " -NoNewline -ForegroundColor Gray
            Write-Host ("{0:F2}с" -f $elapsed) -ForegroundColor Green
            Write-Host ""
            Start-Sleep -Milliseconds 800
        }
        
        # Очистка экрана и отрисовка шапки заново (для скрытия логов утилит и коррекции ширины)
        if ($taskData) {
            Write-Header -Title "СТАРТ ЗАДАЧИ" -Subtitle $taskData.TaskType -AppVersion $taskData.AppVersion -OutputInfo $taskData.DisplayOutputName
        }
        
        # Красивая рамка этапа
        $headerLen = 72
        $msgLen = $Message.Length
        $paddingLen = $headerLen - $msgLen - 12
        if ($paddingLen -lt 0) { $paddingLen = 0 }
        
        Write-Host "  ╔" -NoNewline -ForegroundColor Magenta
        Write-Host ("═" * 4) -NoNewline -ForegroundColor DarkMagenta
        Write-Host "═══ " -NoNewline -ForegroundColor Magenta
        Write-Host $icon -NoNewline
        Write-Host " " -NoNewline
        Write-Host $Message.ToUpper() -NoNewline -ForegroundColor White
        Write-Host " " -NoNewline
        Write-Host ("═" * $paddingLen) -NoNewline -ForegroundColor DarkMagenta
        Write-Host "═══╗" -ForegroundColor Magenta
        
        $script:currentStage = $Message
        $script:stageStartTime = Get-Date
    }
    elseif ($Type.ToUpper() -eq 'PROGRESS') {
        # Прогресс с индикатором
        Write-Host "  ║ " -NoNewline -ForegroundColor DarkMagenta
        Write-Host $icon -NoNewline
        Write-Host " " -NoNewline
        Write-Host $Message -ForegroundColor White
    }
    else {
        # Обычное сообщение с улучшенным форматированием
        Write-Host "  ║ " -NoNewline -ForegroundColor DarkGray
        Write-Host "[$timestamp]" -NoNewline -ForegroundColor DarkGray
        Write-Host " " -NoNewline
        Write-Host $icon -NoNewline
        Write-Host " " -NoNewline
        Write-Host $Message -ForegroundColor $color
    }
    
    $logEntry = @{TaskID=$taskData.TaskID; Type=$Type; Message="$Message"}
    $logEntry | ConvertTo-Json -Compress | Add-Content -Path $logFile
}

function Write-Header {
    param($Title, $Subtitle, $AppVersion, $OutputInfo)
    Clear-Host
    
    # Устанавливаем заголовок окна
    $Host.UI.RawUI.WindowTitle = "STORM SWITCH BOX - WORKER"
    $script:headerStartTime = Get-Date
    
    # Получаем ширину консоли (минус 2 для отступов)
    $consoleWidth = $Host.UI.RawUI.WindowSize.Width
    if ($consoleWidth -lt 60) { $consoleWidth = 80 }
    $innerWidth = $consoleWidth - 4
    
    # Функция для центрирования текста
    function Center-Text {
        param($text, $width)
        $textLen = $text.Length
        if ($textLen -ge $width) { return $text.Substring(0, $width) }
        $leftPad = [math]::Floor(($width - $textLen) / 2)
        $rightPad = $width - $textLen - $leftPad
        return (" " * $leftPad) + $text + (" " * $rightPad)
    }
    
    # Функция для рисования градиентной линии
    function Draw-GradientLine {
        param($leftChar, $fillChar, $rightChar)
        $segment = [math]::Floor($innerWidth / 4)
        $remainder = $innerWidth - ($segment * 4)
        
        Write-Host $leftChar -NoNewline -ForegroundColor DarkBlue
        Write-Host ($fillChar * $segment) -NoNewline -ForegroundColor DarkBlue
        Write-Host ($fillChar * $segment) -NoNewline -ForegroundColor Blue
        Write-Host ($fillChar * $segment) -NoNewline -ForegroundColor Cyan
        Write-Host ($fillChar * ($segment + $remainder)) -NoNewline -ForegroundColor DarkCyan
        Write-Host $rightChar -ForegroundColor DarkBlue
    }
    
    # Верхняя рамка
    Write-Host ""
    Draw-GradientLine "╔" "═" "╗"
    
    # Пустая строка
    Write-Host "║" -NoNewline -ForegroundColor DarkBlue
    Write-Host (" " * $innerWidth) -NoNewline
    Write-Host "║" -ForegroundColor DarkBlue
    
    # ASCII Art логотип STORM
    $logoLines = @(
        " ███████╗████████╗ ██████╗ ██████╗ ███╗   ███╗",
        " ██╔════╝╚══██╔══╝██╔═══██╗██╔══██╗████╗ ████║",
        " ███████╗   ██║   ██║   ██║██████╔╝██╔████╔██║",
        " ╚════██║   ██║   ██║   ██║██╔══██╗██║╚██╔╝██║",
        " ███████║   ██║   ╚██████╔╝██║  ██║██║ ╚═╝ ██║",
        " ╚══════╝   ╚═╝    ╚═════╝ ╚═╝  ╚═╝╚═╝     ╚═╝"
    )
    $colors = @('DarkCyan', 'Cyan', 'White', 'Cyan', 'DarkCyan', 'DarkBlue')
    
    foreach ($i in 0..($logoLines.Count - 1)) {
        Write-Host "║" -NoNewline -ForegroundColor DarkBlue
        $centered = Center-Text $logoLines[$i] $innerWidth
        Write-Host $centered -NoNewline -ForegroundColor $colors[$i]
        Write-Host "║" -ForegroundColor DarkBlue
    }
    
    # Название SWITCH BOX
    Write-Host "║" -NoNewline -ForegroundColor DarkBlue
    $titleText = "S W I T C H   B O X"
    Write-Host (Center-Text $titleText $innerWidth) -NoNewline -ForegroundColor Yellow
    Write-Host "║" -ForegroundColor DarkBlue
    
    # Версия
    if ($AppVersion) { 
        $cleanVer = ($AppVersion -replace 'STORM SWITCH BOX', '' -replace '\(', '' -replace '\)', '').Trim()
        Write-Host "║" -NoNewline -ForegroundColor DarkBlue
        Write-Host (Center-Text "v$cleanVer" $innerWidth) -NoNewline -ForegroundColor DarkGray
        Write-Host "║" -ForegroundColor DarkBlue
    }
    
    # Разделитель
    Draw-GradientLine "╠" "─" "╣"
    
    # Информация о задаче (фиксированные метки без эмодзи для правильного выравнивания)
    $labelWidth = 12  # " ЗАДАЧА:    " = 12 символов
    $valueWidth = $innerWidth - $labelWidth
    
    # ЗАДАЧА
    Write-Host "║" -NoNewline -ForegroundColor DarkBlue
    Write-Host " ЗАДАЧА:    " -NoNewline -ForegroundColor Yellow
    $displayTitle = if ($Title.Length -gt $valueWidth) { $Title.Substring(0, $valueWidth - 3) + "..." } else { $Title }
    Write-Host $displayTitle -NoNewline -ForegroundColor White
    $padding = $valueWidth - $displayTitle.Length
    if ($padding -gt 0) { Write-Host (" " * $padding) -NoNewline }
    Write-Host "║" -ForegroundColor DarkBlue
    
    # ВЫВОД
    if (-not [string]::IsNullOrWhiteSpace($OutputInfo)) {
        Write-Host "║" -NoNewline -ForegroundColor DarkBlue
        Write-Host " ВЫВОД:     " -NoNewline -ForegroundColor Yellow
        $displayOutput = if ($OutputInfo.Length -gt $valueWidth) { $OutputInfo.Substring(0, $valueWidth - 3) + "..." } else { $OutputInfo }
        Write-Host $displayOutput -NoNewline -ForegroundColor Green
        $padding = $valueWidth - $displayOutput.Length
        if ($padding -gt 0) { Write-Host (" " * $padding) -NoNewline }
        Write-Host "║" -ForegroundColor DarkBlue
    }
    
    # ЭТАП
    if ($Subtitle) { 
        Write-Host "║" -NoNewline -ForegroundColor DarkBlue
        Write-Host " ЭТАП:      " -NoNewline -ForegroundColor Yellow
        $displaySub = if ($Subtitle.Length -gt $valueWidth) { $Subtitle.Substring(0, $valueWidth - 3) + "..." } else { $Subtitle }
        Write-Host $displaySub -NoNewline -ForegroundColor Magenta
        $padding = $valueWidth - $displaySub.Length
        if ($padding -gt 0) { Write-Host (" " * $padding) -NoNewline }
        Write-Host "║" -ForegroundColor DarkBlue
    }
    
    # СТАРТ
    $startTimeStr = (Get-Date).ToString("HH:mm:ss")
    Write-Host "║" -NoNewline -ForegroundColor DarkBlue
    Write-Host " СТАРТ:     " -NoNewline -ForegroundColor Yellow
    Write-Host $startTimeStr -NoNewline -ForegroundColor Cyan
    $padding = $valueWidth - $startTimeStr.Length
    if ($padding -gt 0) { Write-Host (" " * $padding) -NoNewline }
    Write-Host "║" -ForegroundColor DarkBlue
    
    # Нижняя рамка
    Draw-GradientLine "╚" "═" "╝"
    Write-Host ""
}

# Ультра-современный заголовок консоли воркера (на всю ширину)

function Repair-KeysFile {
    param($path)
    if (-not (Test-Path -LiteralPath $path)) { return }
    try {
        $bytes = [System.IO.File]::ReadAllBytes($path)
        $hasBom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
        $lines = Get-Content $path -Encoding UTF8 -ErrorAction Stop
        $fixed = @()
        $needsFix = $hasBom
        foreach ($line in $lines) {
            if ($line -match '^([^=]+=\s*)([0-9a-fA-F]{32})00\s*$') {
                $fixed += ($matches[1] + $matches[2])
                $needsFix = $true
            } else {
                $fixed += $line
            }
        }
        if ($needsFix) {
            $utf8NoBom = New-Object System.Text.UTF8Encoding $false
            [System.IO.File]::WriteAllLines($path, $fixed, $utf8NoBom)
            Write-WorkerLog "Ключи очищены от BOM/хвостов: $([System.IO.Path]::GetFileName($path))" -Type 'DEBUG'
        }
    } catch { }
}

function Wait-ForFileAccess {
    param($filePath, $timeoutSeconds = 5)
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($stopwatch.Elapsed.TotalSeconds -lt $timeoutSeconds) {
        try {
            $stream = [System.IO.File]::Open($filePath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::None)
            $stream.Close()
            $stream.Dispose()
            return $true
        } catch {
            Start-Sleep -Milliseconds 200
        }
    }
    return $false
}

function Get-ItemSize {
    param($itemInfo)
    $totalSize = [long]0
    try {
        if ($itemInfo -is [System.IO.DirectoryInfo] -and $itemInfo.PSIsContainer) {
            $measure = Get-ChildItem -LiteralPath $itemInfo.FullName -Recurse -File -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum
            if ($measure -and $measure.Sum) {
                $totalSize = $measure.Sum
            }
        } elseif ($itemInfo -is [System.IO.FileInfo]) {
            $totalSize = $itemInfo.Length
        }
        elseif ($itemInfo -is [string] -and (Test-Path -LiteralPath $itemInfo)) {
            $itemObj = Get-Item -LiteralPath $itemInfo
            if ($itemObj.PSIsContainer) {
                $measure = Get-ChildItem -LiteralPath $itemObj.FullName -Recurse -File -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum
                if ($measure -and $measure.Sum) { $totalSize = $measure.Sum }
            } else {
                $totalSize = $itemObj.Length
            }
        }
    } catch {
        Write-Log "Не удалось определить размер для '$($itemInfo.FullName)'. Ошибка: $($_.Exception.Message)" -Type 'WARN'
    }
    return [long]$totalSize
}

function Get-NspInfo {
    param($filePath, $toolPaths)
    if ($script:nspInfoCache.ContainsKey($filePath)) { return $script:nspInfoCache[$filePath] }
    
    $info = @{ Title = ''; TitleID = ''; Version = 0; DisplayVersion = '0' }
    if (-not (Test-Path -LiteralPath $filePath)) { return $info }
    
    try {
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $startInfo.FileName = $toolPaths.nsz_exe
        $startInfo.Arguments = "--info -p `"$filePath`""
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $startInfo.StandardOutputEncoding = [System.Text.Encoding]::UTF8
        
        $process = [System.Diagnostics.Process]::Start($startInfo)
        $output = $process.StandardOutput.ReadToEnd()
        $process.WaitForExit()
        $process.Close()
        
        if ($process.ExitCode -ne 0) {
            $startInfo.Arguments = "--info -k `"$($toolPaths.key)`" -p `"$filePath`""
            $process = [System.Diagnostics.Process]::Start($startInfo)
            $output = $process.StandardOutput.ReadToEnd()
            $process.WaitForExit()
            $process.Close()
        }
        
        $titleMatch = $output | Select-String -Pattern 'Display Title:\s+(.+)'
        if ($titleMatch) { $info.Title = $titleMatch.Matches[0].Groups[1].Value.Trim() }
        
        $titleIdMatch = $output | Select-String -Pattern 'Title ID:\s+([0-9a-fA-F]{16})'
        if ($titleIdMatch) { $info.TitleID = $titleIdMatch.Matches[0].Groups[1].Value.Trim() }
        
        $versionMatch = $output | Select-String -Pattern 'Version:\s+([0-9]+)'
        if ($versionMatch) { $info.Version = [int64]$versionMatch.Matches[0].Groups[1].Value }
        
        $dVerMatch = $output | Select-String -Pattern 'Display Version:\s+(.+)'
        if ($dVerMatch) { $info.DisplayVersion = $dVerMatch.Matches[0].Groups[1].Value.Trim() }
    } catch {
        Write-Log "Ошибка чтения метаданных NSP '$([System.IO.Path]::GetFileName($filePath))': $($_.Exception.Message)" -Type 'WARN'
    }
    
    $res = [pscustomobject]$info
    $script:nspInfoCache[$filePath] = $res
    return $res
}

function Generate-CustomFileName {
    param($taskData, $originalFileName, $toolPaths, $InputFilePath=$null)
    try {
        $finalName = $originalFileName
        $infoSource = if ($InputFilePath) { $InputFilePath } else { $taskData.OriginalBase }
        
        if (-not $infoSource -or -not (Test-Path -LiteralPath $infoSource)) { return $originalFileName }

        $info = Get-NspInfo -filePath $infoSource -toolPaths $toolPaths
        
        if (-not $info.TitleID -and $taskData.OriginalUpdates) {
             foreach($upd in $taskData.OriginalUpdates) {
                 if (Test-Path -LiteralPath $upd) {
                     $uInfo = Get-NspInfo -filePath $upd -toolPaths $toolPaths
                     if($uInfo.Version -gt $info.Version) { $info = $uInfo }
                 }
             }
        }

        if ($info.TitleID) {
            # Извлекаем fallback-имя из оригинального файла (до первой скобки)
            $fallbackTitle = $null
            if ($taskData.OriginalBase) {
                $origName = [System.IO.Path]::GetFileNameWithoutExtension((Split-Path $taskData.OriginalBase -Leaf))
                $fallbackTitle = ($origName -split '\[')[0].Trim()
                # Если fallback пустой или слишком короткий, пробуем из переданного имени
                if ([string]::IsNullOrWhiteSpace($fallbackTitle) -or $fallbackTitle.Length -lt 2) { $fallbackTitle = $null }
            }
            if (-not $fallbackTitle) {
                $fName = [System.IO.Path]::GetFileNameWithoutExtension($originalFileName)
                $fallbackTitle = ($fName -split '\[')[0].Trim()
            }
            
            $title = $fallbackTitle
            if ($info.Title) {
                # Проверяем: если title из NACP содержит нечитаемые символы (CJK, '?'), используем fallback
                $hasGarbled = $info.Title -match '[\?]{3,}'
                $hasCJK = $info.Title -match '[\p{IsCJKUnifiedIdeographs}\p{IsHiragana}\p{IsKatakana}\p{IsHangulSyllables}]'
                if (-not $hasGarbled -and -not $hasCJK) {
                    $title = $info.Title
                } else {
                    Write-WorkerLog "NACP содержит CJK-символы ('$($info.Title)'), используется имя из файла: '$title'" -Type 'DEBUG'
                }
            }
            
            $tid = $info.TitleID.ToUpper()
            $ver = if ($info.Version -gt 0) { "[$($info.DisplayVersion)]" } else { "" }
            
            $counts = [System.Collections.Generic.List[string]]::new()
            if ($taskData.Base) { $counts.Add("1G") }
            if ($taskData.Updates) { $counts.Add("$($([array]$taskData.Updates).Count)U") }
            if ($taskData.DLCs) { $counts.Add("$($taskData.DLCs.Count)D") }
            
            $modCount = 0
            if ($taskData.RomfsPaths) { $modCount += $taskData.RomfsPaths.Count }
            if ($taskData.ExefsPath) { $modCount++ }
            if ($modCount -gt 0) { $counts.Add("${modCount}M") }
            
            $countsStr = if ($counts.Count -gt 0) { " ($($counts -join '+'))" } else { "" }
            
            $ext = [System.IO.Path]::GetExtension($originalFileName)
            $finalName = "$title [$tid]$ver$countsStr$ext"
        }
        
        $invalidChars = [System.IO.Path]::GetInvalidFileNameChars() -join ''
        $regex = "[{0}]" -f [regex]::Escape($invalidChars)
        return ($finalName -replace $regex, '').Trim()
    } catch { return $originalFileName }
}

function Invoke-Tool {
    param($procInfo, $title)
    $fullCommand = "`"$($procInfo.Exe)`" $($procInfo.Args)"
    Write-WorkerLog -Message ">> $title" -Type 'INFO'
    
    try {
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $startInfo.FileName = $procInfo.Exe
        $startInfo.Arguments = $procInfo.Args
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $false
        
        if ($procInfo.WorkingDir) {
            if (-not (Test-Path -LiteralPath $procInfo.WorkingDir)) { 
                New-Item -ItemType Directory -Path $procInfo.WorkingDir -Force -EA 0 | Out-Null 
            }
            $startInfo.WorkingDirectory = $procInfo.WorkingDir
        }
        
        $process = New-Object System.Diagnostics.Process
        $process.StartInfo = $startInfo
        $process.Start() | Out-Null
        $process.WaitForExit()
        
        $exitCode = $process.ExitCode
        
        # Фикс: добавляем пустую строку, чтобы сбросить последствия \r от прогресс-баров (например, nsz)
        Write-Host ""
        
        if ($exitCode -ne 0) { 
            throw "Утилита '$([System.IO.Path]::GetFileName($procInfo.Exe))' завершилась с кодом $exitCode."
        }
        return $exitCode
    } catch { 
        Write-WorkerLog "ОШИБКА '$title': $($_.Exception.Message)" -Type 'ERROR'
        throw 
    }
}

function Invoke-YanuPack {
    param($packArgs, $tempDir, $toolPaths, $taskID, $useCores=$null)
    $runDir = Join-Path $tempDir "run_pack"
    [void](New-Item -ItemType Directory -Force $runDir)
    
    $yanuExeInRunDir = Join-Path $runDir (Split-Path $toolPaths.yanu_cli_path -Leaf)
    Copy-Item -Path $toolPaths.yanu_cli_path -Destination $yanuExeInRunDir -Force
    
    $finalPackArgs = "--keyfile `"$($toolPaths.key)`" $packArgs"
    $packProc = @{ Exe = $yanuExeInRunDir; Args = $finalPackArgs; WorkingDir = $runDir }
    
    try { 
        Invoke-Tool $packProc "Упаковка (yanu-cli)" 
    } finally { 
        if (Test-Path $runDir) { Remove-Item $runDir -Recurse -Force -ErrorAction SilentlyContinue } 
    }
}

function Prepare-IsolatedTool {
    param($toolName, $taskTempDir, $originalToolPaths)
    $isolatedDir = Join-Path $taskTempDir "isolated_$toolName"
    $sourceDir = if ($toolName -eq 'nsz') { $originalToolPaths.ndir } else { $originalToolPaths.nbdir }
    
    Write-WorkerLog "Изоляция $toolName в $isolatedDir..."
    if (-not (Test-Path $isolatedDir)) { New-Item -ItemType Directory -Path $isolatedDir -Force -EA 0 }
    
    # ИСПРАВЛЕНО: правильное экранирование путей для robocopy
    $roboCopyArgs = @($sourceDir, $isolatedDir, '/E', '/NFL', '/NDL', '/NJH', '/NJS', '/nc', '/ns', '/np', '/R:3', '/W:2')
    & robocopy.exe @roboCopyArgs | Out-Null
    
    $newToolPaths = @{}
    if ($toolName -eq 'nsz') {
        $newToolPaths.Exe = Join-Path $isolatedDir "nsz.exe"
        $newToolPaths.WorkingDir = $isolatedDir
        $newToolPaths.KeyFile = Join-Path $isolatedDir "keys.txt"
        $newToolPaths.RootDir = $isolatedDir
    } elseif ($toolName -eq 'nscb') {
        $ztoolsDir = Join-Path $isolatedDir "ztools"
        if (Test-Path (Join-Path $ztoolsDir "squirrel.exe")) {
            $newToolPaths.Exe = Join-Path $ztoolsDir "squirrel.exe"
            $newToolPaths.WorkingDir = $ztoolsDir
            $newToolPaths.KeyFile = Join-Path $ztoolsDir "keys.txt"
        } else {
            $newToolPaths.Exe = Join-Path $isolatedDir "squirrel.exe"
            $newToolPaths.WorkingDir = $isolatedDir
            $newToolPaths.KeyFile = Join-Path $isolatedDir "keys.txt"
        }
        $newToolPaths.RootDir = $isolatedDir
    }
    return $newToolPaths
}

function Setup-IsolatedToolWithKeys {
    param($toolName, $tempDir, $toolPaths)
    
    # Проверяем NeedIsolation: если файлы НЕ разделяются между задачами — пропускаем robocopy
    $needIsolation = if ($taskData -and $taskData.NeedIsolation) { $true } else { $false }
    
    if ($needIsolation) {
        # ПОЛНАЯ ИЗОЛЯЦИЯ: robocopy всей папки инструмента (для параллельных задач с общими файлами)
        $resultPaths = Prepare-IsolatedTool -toolName $toolName -taskTempDir $tempDir -originalToolPaths $toolPaths
    } else {
        # ПРЯМОЙ РЕЖИМ: используем оригинальные пути (без копирования ~100-200 МБ)
        $resultPaths = @{}
        if ($toolName -eq 'nsz') {
            $resultPaths.Exe = $toolPaths.nsz_exe
            $resultPaths.WorkingDir = $toolPaths.ndir
            $resultPaths.KeyFile = Join-Path $toolPaths.ndir "keys.txt"
            $resultPaths.RootDir = $toolPaths.ndir
        } elseif ($toolName -eq 'nscb') {
            $ztoolsDir = Join-Path $toolPaths.nbdir "ztools"
            if (Test-Path (Join-Path $ztoolsDir "squirrel.exe")) {
                $resultPaths.Exe = Join-Path $ztoolsDir "squirrel.exe"
                $resultPaths.WorkingDir = $ztoolsDir
                $resultPaths.KeyFile = Join-Path $ztoolsDir "keys.txt"
            } else {
                $resultPaths.Exe = Join-Path $toolPaths.nbdir "squirrel.exe"
                $resultPaths.WorkingDir = $toolPaths.nbdir
                $resultPaths.KeyFile = Join-Path $toolPaths.nbdir "keys.txt"
            }
            $resultPaths.RootDir = $toolPaths.nbdir
        }
        Write-WorkerLog "Изоляция пропущена (файлы не разделяются)" -Type 'DEBUG'
    }
    
    # Ключи копируем ВСЕГДА (лёгкая операция, гарантирует актуальность)
    if (Test-Path -LiteralPath $toolPaths.key) {
        try {
            $keyDir = Split-Path $resultPaths.KeyFile -Parent
            if (-not (Test-Path $keyDir)) { New-Item -ItemType Directory -Path $keyDir -Force }
            Copy-Item -LiteralPath $toolPaths.key -Destination $resultPaths.KeyFile -Force
            if ($toolName -eq 'nscb' -and $resultPaths.RootDir) { 
                Copy-Item -LiteralPath $toolPaths.key -Destination (Join-Path $resultPaths.RootDir "keys.txt") -Force -EA SilentlyContinue 
            }
        } catch { 
            Write-WorkerLog "Ошибка копирования ключей: $($_.Exception.Message)" -Type 'WARN' 
        }
    }
    return $resultPaths
}

function Convert-To-NspIfNeeded {
    param($filePath, $convertDir, $isolatedNsz, $useCores=$null, $needIsolation=$false, $forceTags=$null)
    
    # [FIX] Умная проверка пути
    $realPath = $filePath
    if (-not (Test-Path -LiteralPath $realPath)) {
        $cleanPath = $filePath -replace '^\\\\\\?\\', ''
        if (Test-Path -LiteralPath $cleanPath) { $realPath = $cleanPath } 
        else { throw "Файл не найден: $filePath" }
    }
    
    $fileInfo = Get-Item -LiteralPath $realPath
    $ext = $fileInfo.Extension.ToLower()
    
    # Имя для временных операций. СОХРАНЯЕМ теги TitleID и Version, так как NSC_Builder зависит от них!
    $origName = [System.IO.Path]::GetFileNameWithoutExtension($fileInfo.Name)
    $safeName = "src_" + [guid]::NewGuid().ToString("N").Substring(0, 6)
    if ($origName -match '(?i)Unlocker') { $safeName += "_Unlocker" }
    $validTags = [System.Collections.Generic.List[string]]::new()
    [regex]::Matches($origName, '\[([0-9a-fA-F]{16})\]') | ForEach-Object { $validTags.Add($_.Value) }
    [regex]::Matches($origName, '\[v\d+\]', 'IgnoreCase') | ForEach-Object { $validTags.Add($_.Value) }
    
    if ($validTags.Count -gt 0) { 
        $safeName += "_" + ($validTags -join '') 
    } elseif ($forceTags) {
        $safeName += "_" + $forceTags
    }
    
    $targetNspPath = Join-Path $convertDir ($safeName + ".nsp")
    
    # Путь к файлу для обработки (оригинал или копия)
    $workingFilePath = $realPath
    $isolatedSourcePath = $null
    
    # ПРЯМАЯ ОБРАБОТКА: отключено предварительное копирование для ускорения распаковки
    # Копируем ТОЛЬКО если файл используется несколькими задачами ($needIsolation)
    $isCompressed = $ext -in '.nsz', '.xcz', '.xci'
    if ($needIsolation) {
        $isolatedSourceDir = Join-Path $convertDir "sources"
        if (-not (Test-Path $isolatedSourceDir)) { 
            New-Item -ItemType Directory -Path $isolatedSourceDir -Force | Out-Null 
        }
        
        $isolatedSourcePath = Join-Path $isolatedSourceDir ($safeName + $ext)
        
        $copySuccess = $false
        for ($attempt = 1; $attempt -le 5; $attempt++) {
            try {
                Write-WorkerLog "Копирование исходника (изоляция для параллельных задач, попытка $attempt)..." -Type 'INFO'
                
                # Копирование с отображением процентов
                $sourceStream = [System.IO.File]::OpenRead($realPath)
                $destStream = [System.IO.File]::Create($isolatedSourcePath)
                $totalBytes = $sourceStream.Length
                $bufferSize = 4MB
                $buffer = New-Object byte[] $bufferSize
                $copiedBytes = 0
                $lastPrintedPercent = -1
                
                while (($read = $sourceStream.Read($buffer, 0, $buffer.Length)) -gt 0) {
                    $destStream.Write($buffer, 0, $read)
                    $copiedBytes += $read
                    $percent = [math]::Floor(($copiedBytes / $totalBytes) * 100)
                    
                    if ($percent -ne $lastPrintedPercent -and ($percent % 5 -eq 0 -or $percent -eq 100)) {
                        Write-Host "`r  ║ " -NoNewline -ForegroundColor DarkMagenta
                        Write-Host "📊" -NoNewline
                        Write-Host " " -NoNewline
                        Write-Host "Прогресс копирования: $percent%       " -NoNewline -ForegroundColor White
                        $lastPrintedPercent = $percent
                    }
                }
                
                $sourceStream.Close()
                $destStream.Close()
                $sourceStream.Dispose()
                $destStream.Dispose()
                
                Write-Host "" # Перевод строки после завершения прогресса
                $copySuccess = $true
                break
            } catch {
                if ($sourceStream) { $sourceStream.Dispose() }
                if ($destStream) { $destStream.Dispose() }
                
                if ($attempt -lt 5) {
                    $delay = $attempt * 1000
                    Write-WorkerLog "Файл занят, ожидание ${delay}мс..." -Type 'WARN'
                    Start-Sleep -Milliseconds $delay
                } else {
                    throw "Не удалось скопировать файл после 5 попыток: $($_.Exception.Message)"
                }
            }
        }
        if (-not $copySuccess) { throw "Не удалось скопировать исходный файл" }
        $workingFilePath = $isolatedSourcePath
    }

    if ($ext -in '.nsz', '.xcz', '.xci') {
        Write-WorkerLog "Подготовка: $ext -> NSP (в $safeName.nsp)" -Type 'INFO'
        $threadsArg = if ($useCores) { "-t $useCores" } else { "" }
        $convProc = @{ Exe = $isolatedNsz.Exe; Args = "-D `"$workingFilePath`" -o `"$convertDir`" $threadsArg"; WorkingDir = $isolatedNsz.WorkingDir }
        if ((Invoke-Tool $convProc "Распаковка исходника") -ne 0) { throw "Ошибка конвертации исходного файла." }
        
        $convertedFile = Get-ChildItem -Path $convertDir -Filter "*.nsp" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if (-not $convertedFile) { throw "Не найден .nsp файл после конвертации." }
        
        # Удаляем изолированную копию исходника для экономии места
        if ($isolatedSourcePath -and (Test-Path $isolatedSourcePath)) {
            Remove-Item -LiteralPath $isolatedSourcePath -Force -ErrorAction SilentlyContinue
        }
        
        if ($convertedFile.Name -ne ($safeName + ".nsp")) { 
            Rename-Item -LiteralPath $convertedFile.FullName -NewName ($safeName + ".nsp") -Force 
        }
        return $targetNspPath
        
    } elseif ($ext -eq '.nsp') {
        # ПРЯМАЯ РАБОТА: никаких копий, работаем прямо с источника (по просьбе пользователя)
        return $realPath
    }
    
    return $workingFilePath
}


# Функция для проверки, является ли файл "сшитым" (содержит base + update/DLC)
function Test-IsStitchedFile {
    param($filePath, $isolatedNsz)
    
    try {
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $startInfo.FileName = $isolatedNsz.Exe
        $startInfo.Arguments = "--info -p `"$filePath`""
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $startInfo.StandardOutputEncoding = [System.Text.Encoding]::UTF8
        $startInfo.WorkingDirectory = $isolatedNsz.WorkingDir
        
        $process = [System.Diagnostics.Process]::Start($startInfo)
        $output = $process.StandardOutput.ReadToEnd()
        $process.WaitForExit()
        
        # ИСПРАВЛЕНИЕ: Анализируем Title ID по суффиксам
        # Формат Title ID: XXXXXXXXXXXXX + YYY (13 символов база + 3 символа тип)
        # YYY = 000 - базовая игра
        # YYY = 800 - обновление
        # YYY = 001-7FF - DLC
        
        $titleIdRegex = '([0-9A-Fa-f]{16})'
        $allMatches = [regex]::Matches($output, $titleIdRegex)
        $allTitleIds = @($allMatches | ForEach-Object { $_.Groups[1].Value.ToUpper() } | Sort-Object -Unique)
        
        # Проверяем наличие РЕАЛЬНОГО контента обновления или DLC по суффиксам
        $hasUpdate = $false
        $hasDLC = $false
        
        foreach ($tid in $allTitleIds) {
            $suffix = $tid.Substring(13, 3)  # Последние 3 символа
            
            if ($suffix -eq '800') {
                $hasUpdate = $true
            }
            elseif ($suffix -match '^[0-7][0-9A-Fa-f]{2}$' -and $suffix -ne '000') {
                # DLC суффиксы: 001-7FF (но не 000 и не 800)
                $hasDLC = $true
            }
        }
        
        # Файл считается сшитым ТОЛЬКО если есть реальный update (800) или DLC контент
        $isStitched = $hasUpdate -or $hasDLC
        
        if ($isStitched) {
            Write-WorkerLog "Файл содержит: $(if ($hasUpdate) {'Update '})$(if ($hasDLC) {'DLC'})" -Type 'INFO'
        }
        
        return @{
            IsStitched = $isStitched
            TitleIds = $allTitleIds
            HasUpdate = $hasUpdate
            HasDLC = $hasDLC
            BaseTitleId = if ($allTitleIds.Count -gt 0) { 
                # Базовый Title ID заканчивается на 000
                $allTitleIds | Where-Object { $_.Substring(13, 3) -eq '000' } | Select-Object -First 1
            } else { $null }
        }
    } catch {
        Write-WorkerLog "Ошибка проверки файла на сшитость: $($_.Exception.Message)" -Type 'WARN'
        return @{ IsStitched = $false; TitleIds = @(); HasUpdate = $false; HasDLC = $false; BaseTitleId = $null }
    }
}

# Функция для извлечения только базовой игры из сшитого файла
function Extract-BaseFromStitched {
    param($stitchedFilePath, $outputDir, $isolatedNsz, $toolPaths, $useCores)
    
    Write-WorkerLog "Обнаружен сшитый файл. Извлечение базовой игры..." -Type 'INFO'
    
    # 1. Распаковываем сшитый файл во временную папку
    $extractDir = Join-Path $outputDir "stitched_extract"
    [void](New-Item -ItemType Directory -Force $extractDir)
    
    $coresArg = if ($useCores) { "-t $useCores" } else { "" }
    $extractArgs = "$coresArg -D `"$stitchedFilePath`" -o `"$extractDir`""
    
    $extractProc = @{ Exe = $isolatedNsz.Exe; Args = $extractArgs; WorkingDir = $isolatedNsz.WorkingDir }
    $exitCode = Invoke-Tool $extractProc "Извлечение из сшитого файла"
    
    if ($exitCode -ne 0) {
        Write-WorkerLog "Не удалось разделить сшитый файл, используем стандартный метод через распаковку..." -Type 'WARN'
        
        # Fallback: распаковываем и упаковываем заново только base
        $unpackTemp = Join-Path $outputDir "unpack_stitched"
        [void](New-Item -ItemType Directory -Force $unpackTemp)
        
        # Изоляция yanu-cli
        $isolatedYanuDir = Join-Path $outputDir 'isolated_yanu_extract'
        [void](New-Item -ItemType Directory -Force $isolatedYanuDir)
        $isolatedYanuPath = Join-Path $isolatedYanuDir 'yanu-cli.exe'
        Copy-Item -LiteralPath $toolPaths.yanu_cli_path -Destination $isolatedYanuPath -Force
        
        # Распаковка только base (без --update)
        $unpackArgs = "unpack --base `"$stitchedFilePath`" -o `"$unpackTemp`""
        $unpackProc = @{ Exe = $isolatedYanuPath; Args = "--keyfile `"$($toolPaths.key)`" $unpackArgs"; WorkingDir = $outputDir }
        
        if ((Invoke-Tool $unpackProc "Распаковка базовой игры") -ne 0) {
            Write-WorkerLog "Не удалось извлечь базовую игру из сшитого файла" -Type 'ERROR'
            return $null
        }
        
        # Находим control.nca и упаковываем обратно
        $pdata = if (Test-Path -LiteralPath (Join-Path $unpackTemp "basedata")) { Join-Path $unpackTemp "basedata" } else { $unpackTemp }
        $oldErrorPref = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        
        $controlNcaFile = (Get-ChildItem -LiteralPath $pdata -Filter *.nca -Recurse | ForEach-Object { 
            if (& $toolPaths.hactoolnet "-k" $toolPaths.key $_.FullName 2>$null | Select-String 'Control' -Quiet) { $_; return } 
        } | Select-Object -First 1)
        
        if (-not $controlNcaFile) {
            $ErrorActionPreference = $oldErrorPref
            Write-WorkerLog "Не найден control.nca в распакованном файле" -Type 'ERROR'
            return $null
        }
        
        $titleId = (& $toolPaths.hactoolnet "-k" $toolPaths.key $controlNcaFile.FullName 2>$null | Select-String 'TitleID').ToString().Split(':')[-1].Trim()
        $ErrorActionPreference = $oldErrorPref
        
        # Упаковка
        $packArgsBuilder = [System.Text.StringBuilder]::new("pack ")
        [void]$packArgsBuilder.Append("--controlnca `"$($controlNcaFile.FullName)`" ")
        [void]$packArgsBuilder.Append("--titleid `"$titleId`" ")
        
        $romfsPath = Get-ChildItem -Path $unpackTemp -Filter "romfs" -Recurse -Directory | Select-Object -First 1
        $exefsPath = Get-ChildItem -Path $unpackTemp -Filter "exefs" -Recurse -Directory | Select-Object -First 1
        
        if ($romfsPath) { [void]$packArgsBuilder.Append("--romfsdir `"$($romfsPath.FullName)`" ") }
        if ($exefsPath) { [void]$packArgsBuilder.Append("--exefsdir `"$($exefsPath.FullName)`" ") }
        [void]$packArgsBuilder.Append("-o `"$extractDir`"")
        
        Invoke-YanuPack -packArgs $packArgsBuilder.ToString() -tempDir $outputDir -toolPaths $toolPaths -taskID "extract_base" -useCores $useCores
    }
    
    # Ищем только базовую игру (Title ID заканчивается на 000)
    $extractedFiles = Get-ChildItem -LiteralPath $extractDir -Filter "*.nsp" -File
    # Поддержка квадратных скобок [0100...000] и круглых скобок или просто ID в имени
    $baseFile = $extractedFiles | Where-Object { 
        $_.Name -match '\[0100[0-9A-Fa-f]{12}000\]' -or 
        $_.Name -match '[\(\s\-]0100[0-9A-Fa-f]{12}000[\)\s]' -or
        $_.Name -match '0100[0-9A-Fa-f]{12}000'
    } | Select-Object -First 1
    
    if (-not $baseFile) {
        # Берём любой первый файл
        $baseFile = $extractedFiles | Select-Object -First 1
    }
    
    if ($baseFile) {
        Write-WorkerLog "Извлечена базовая игра: $($baseFile.Name)" -Type 'SUCCESS'
        return $baseFile.FullName
    }
    
    return $null
}

# ═══════════════════════════════════════════════════════════════════
# УНИВЕРСАЛЬНАЯ ФУНКЦИЯ POST-PACK КОНВЕРТАЦИИ (XCI/NSZ/XCZ)
# Устраняет дублирование блоков конвертации в UpdateRepack/Pack/Convert
# ═══════════════════════════════════════════════════════════════════
function Invoke-PostPackConversion {
    param(
        [string]$InputFile,
        [string]$TargetFormat,
        [string]$OutputDir,
        [hashtable]$ToolPaths,
        [string]$KpVal,
        [string]$TrimVal,
        [int]$CompressionLevel,
        $UseCores,
        $IsolatedNsz = $null,
        [string]$ListFileName = 'list_conv.txt'
    )
    
    $currentFile = $InputFile
    $targetFormatLower = $TargetFormat.ToLower()
    
    # Шаг 1: Конвертация NSP → XCI (если целевой формат XCI или XCZ)
    if ($targetFormatLower -in 'xci', 'xcz') {
        Write-WorkerProgress -Status "Конвертация в XCI" -Percent 92
        $isolatedNscb = Setup-IsolatedToolWithKeys -toolName 'nscb' -tempDir $tempDir -toolPaths $ToolPaths
        $nscbListPath = Join-Path $tempDir $ListFileName
        $utf8NoBom = New-Object System.Text.UTF8Encoding $false
        [System.IO.File]::WriteAllLines($nscbListPath, @($currentFile), $utf8NoBom)
        $squirrelArgsArray = @('-b', '65536', '-pv', 'false', '-kp', $KpVal, '-tm', $TrimVal, '--RSVcap', '268435656', '-fat', 'exfat', '-fx', 'files', '-ND', 'true', '-t', 'xci', '-o', "`"$OutputDir`"", '-tfile', "`"$nscbListPath`"", '-roma', 'TRUE', '-dmul', '"calculate"')
        $squirrelProc = @{ Exe = $isolatedNscb.Exe; Args = ($squirrelArgsArray -join ' '); WorkingDir = $isolatedNscb.WorkingDir }
        if ((Invoke-Tool $squirrelProc "Конвертация в XCI") -ne 0) { throw "Ошибка конвертации в XCI." }
        if ($KpVal -ne "false") { Write-WorkerLog "Понижение версии ключей до Generation $KpVal успешно применено." -Type 'SUCCESS' }
        Remove-Item -LiteralPath $currentFile -Force
        $xciFile = (Get-ChildItem -LiteralPath $OutputDir -Filter "*.xci" | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
        if (-not $xciFile) { throw "Не найден .xci файл после конвертации." }
        Log-Xci-Status -filePath $xciFile -isTrimmed $TrimVal
        $currentFile = $xciFile
    }
    
    # Шаг 2: Сжатие в NSZ/XCZ (если целевой формат NSZ или XCZ)
    if ($targetFormatLower -in 'nsz', 'xcz') {
        Write-WorkerProgress -Status "Сжатие ($($targetFormatLower.ToUpper()))" -Percent 95
        if (-not $IsolatedNsz) { $IsolatedNsz = Setup-IsolatedToolWithKeys -toolName 'nsz' -tempDir $tempDir -toolPaths $ToolPaths }
        $coresArg = if ($UseCores) { "-t $UseCores" } else { "" }
        $nszArgs = "$coresArg -C -l $CompressionLevel `"$currentFile`" -o `"$OutputDir`""
        $procConv = @{ Exe = $IsolatedNsz.Exe; Args = $nszArgs; WorkingDir = $IsolatedNsz.WorkingDir }
        if ((Invoke-Tool $procConv "Сжатие в $($targetFormatLower.ToUpper())") -ne 0) { throw "Ошибка сжатия в $($targetFormatLower.ToUpper())." }
        Remove-Item -LiteralPath $currentFile -Force
        $currentFile = (Get-ChildItem -LiteralPath $OutputDir -Filter "*.$targetFormatLower" | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
        if (-not $currentFile) { throw "Не найден .$targetFormatLower файл после сжатия." }
    }
    
    return $currentFile
}
'@
#====================================================================================
#  БЛОК ИНИЦИАлизации И ПРОВЕРОК
#====================================================================================
function YEStart {
    $args_ = @($env:arg1, $env:arg2)
    $script:cd = $pwd
    
    # --- ИСПРАВЛЕНИЕ: УМНЫЙ ВЫБОР TEMP ПАПКИ ---
    # По умолчанию используем папку temp внутри программы.
    $localTemp = "$cd\temp"
    
    # Если путь слишком длинный, переключаемся на корень диска (SB_T), 
    # чтобы избежать вылетов утилит.
    if ($localTemp.Length -gt 70) { 
        $driveRoot = [System.IO.Path]::GetPathRoot($script:cd)
        $script:wdir = Join-Path $driveRoot "SB_T"
    } else {
        $script:wdir = $localTemp
    }
    # -------------------------------------------

    $script:odir = "$cd\out"
    $script:tdir = "$cd\tools"
    $script:ndir = "$tdir\nsz"
    $script:nbdir = "$tdir\nscb"
    $script:key = "$tdir\prod.keys"
    
    $script:settingsFile = "$cd\ssb.settings"
    $script:outNamesFile = "$cd\ssb.outnames"
    $script:outNamesFileLock = New-Object System.Object
    
    # (Req 1) Обновление версии
    $script:title = 'STORM SWITCH BOX (1.4.000)'
    $yanu_rec = '0.10.1'

    # (Req 2) Определение количества ядер CPU
    $script:MaxCores = [System.Environment]::ProcessorCount
    if ($script:MaxCores -lt 1) { $script:MaxCores = 1 }
    $script:DefaultCores = $script:MaxCores

    $script:boldFont = New-Object Font('Segoe UI', 10, [FontStyle]::Bold)
    $script:regularFont = New-Object Font('Segoe UI', 10)
    $script:smallFont = New-Object Font('Segoe UI', 9)
    $script:buttons = 'Обновление', 'Распаковка', 'Упаковка', 'Конвертация', 'Создание мульти-контента', '', 'Системные файлы', 'Настройки', '', 'История обработок'
    $script:button_names = 'Update', 'Unpack', 'Pack', 'Convert', 'Multi', '', 'System', 'Settings', '', 'History'
    $script:logStorage = [System.Collections.Concurrent.ConcurrentDictionary[string, object]]::new()
    $script:generalLog = [System.Collections.Generic.List[string]]::new()
    $script:activeDownloads = @{}
    $script:runningTasks = [System.Collections.Concurrent.ConcurrentDictionary[int, object]]::new()

    # ПЕРСИСТЕНТНОЕ ЛОГИРОВАНИЕ: Инициализация файлового лога
    $script:logFileWriteCount = 0
    try {
        $logDir = Join-Path $script:cd 'logs'
        if (-not (Test-Path -LiteralPath $logDir)) { [void](New-Item -ItemType Directory -Force $logDir) }
        $logFileName = "ssb_log_$(Get-Date -Format 'yyyy-MM-dd').txt"
        $logFilePath = Join-Path $logDir $logFileName
        $script:logFileWriter = [System.IO.StreamWriter]::new($logFilePath, $true, [System.Text.Encoding]::UTF8)
        $script:logFileWriter.AutoFlush = $false
        $script:logFileWriter.WriteLine("")
        $script:logFileWriter.WriteLine("═══════════════════════════════════════════════════════")
        $script:logFileWriter.WriteLine("  STORM SWITCH BOX — Сессия $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
        $script:logFileWriter.WriteLine("═══════════════════════════════════════════════════════")
        $script:logFileWriter.Flush()
        # Ротация: удаляем логи старше 7 дней
        Get-ChildItem -Path $logDir -Filter 'ssb_log_*.txt' -ErrorAction SilentlyContinue |
            Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-7) } |
            Remove-Item -Force -ErrorAction SilentlyContinue
    } catch {
        $script:logFileWriter = $null
    }

    $script:defaultOutPaths = @{
        Update = Join-Path $script:odir "UPDATE"
        Unpack = Join-Path $script:odir "UNPACKING"
        Pack = Join-Path $script:odir "PACKAGING"
        Convert = Join-Path $script:odir "CONVERSION"
        Multi = Join-Path $script:odir "CREATION OF MULTI-CONTENT"
    }

    if (Test-Path $script:settingsFile) {
        $script:settings = Import-Csv $script:settingsFile -ErrorAction SilentlyContinue | Select-Object -First 1
    } else {
        $script:settings = $null
    }
    Write-Log "Запуск $script:title..."
    Write-Log "Обнаружено ядер CPU: $script:MaxCores."
    if ($script:wdir -like "*SB_T*") {
        Write-Log "ВНИМАНИЕ: Программа находится в глубокой папке. Для надежности используется TEMP в корне диска: $script:wdir"
    } else {
        Write-Log "Используется локальная TEMP папка: $script:wdir"
    }

    if (@($tdir, $ndir, $nbdir, $wdir, $odir) | Where-Object { -not (Test-Path $_) }) {
        [void](New-Item -ItemType Directory -Force $ndir, $nbdir, $wdir, $odir)
        Write-Log "Созданы необходимые директории."
    }

    Write-Log "Проверка/создание папок вывода по умолчанию..."
    $script:defaultOutPaths.Values | ForEach-Object { New-Item -ItemType Directory -Path $_ -Force -ErrorAction SilentlyContinue }

    $yanu_item = Get-Item "$tdir\*yanu*cli*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq ($script:yanu_cli = $yanu_item.FullName)) {
        $script:yanu_cli = Join-Path $script:tdir "yanu-cli.exe"
    } else {
        Write-Log "Найден yanu-cli: $($script:yanu_cli)."
    }
    $hactoolnet_item = Get-ChildItem -Path $script:tdir -Filter "hactoolnet.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    $script:hactoolnet = $hactoolnet_item.FullName
    if (-not $script:hactoolnet) {
        $appDataPath = Join-Path $env:LOCALAPPDATA "com.github.nozwock.yanu\hactoolnet.exe"
        if (Test-Path -LiteralPath $appDataPath) {
           $script:hactoolnet = $appDataPath
           Write-Log "Найден hactoolnet.exe в AppData: $script:hactoolnet"
        }
    }
    if (-not $script:hactoolnet) {
        YEmsg "ВНИМАНИЕ: hactoolnet.exe не найден ни в папке 'tools', ни в AppData.`nУпаковка, Обновление и Распаковка отдельных NCA/BIN файлов будут недоступны.`nОн является частью NSC_Builder."
    }
    if (Test-Path $key) {
        Write-Log "Найден файл prod.keys."
    }
    if (Test-Path $script:yanu_cli) {
        try {
            if (($yanu_v = ((& $script:yanu_cli -V).Trim().Split(' ')[-1])) -ne $yanu_rec) {
                Write-Log "ВНИМАНИЕ: Рекомендуемая версия yanu: $yanu_rec (Текущая: $yanu_v)" -Type 'ERROR'
                YEmsg "Рекомендуемая версия yanu: $yanu_rec`nТекущая: $yanu_v"
            }
        } catch {
            Write-Log "Не удалось определить версию yanu-cli. Возможно, файл поврежден или заблокирован." -Type 'ERROR'
        }
    }
    if(-not $script:DebugMode) {
        Get-Item -Path "$wdir\worker-*.ps1", "$wdir\task-*.xml", "$wdir\status-*.log", "$wdir\result-*.xml", "$tdir\yanu.log.*", "$cd\yanu.log.*", "$cd\CRASH_REPORT_*.log", "$wdir\*.progress", "$wdir\*.livelog", "$wdir\temp_dl_*.ps1", "$wdir\*.pid", "$wdir\crash-*.log", "$tdir\base*", "$tdir\NSCB.log", "$cd\base*", "$tdir\*.tmp" -ErrorAction SilentlyContinue | Remove-Item -Force -Recurse
    }
    $script:nsz_exe = Join-Path $script:ndir "nsz.exe"
    $script:nscb_bat = Join-Path $script:nbdir "NSCB.bat"
    $script:squirrel_exe = Join-Path $script:nbdir "ztools\squirrel.exe"

    Write-Log "Инициализация GUI..."
    YEGUI
}
#====================================================================================
# БЛОК ВСПОМОГАТЕЛЬНЫХ ФУНКЦИЙ
#====================================================================================
function Send-ToastNotification {
    param(
        [string]$Title,
        [string]$Message,
        [string]$Type = 'Info'  # 'Success', 'Error', 'Info'
    )
    # Показываем toast только если окно не в фокусе или свёрнуто
    if ($script:f -and $script:f.WindowState -ne [FormWindowState]::Minimized) {
        try {
            $foregroundHwnd = [User32]::GetForegroundWindow()
            if ($foregroundHwnd -eq $script:f.Handle) { return }
        } catch { }
    }
    
    try {
        # Используем нативный Windows Toast API (не требует BurntToast)
        [void][Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime]
        [void][Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime]
        
        $template = @"
<toast>
    <visual>
        <binding template="ToastGeneric">
            <text>$([System.Security.SecurityElement]::Escape($Title))</text>
            <text>$([System.Security.SecurityElement]::Escape($Message))</text>
        </binding>
    </visual>
    <audio silent="false"/>
</toast>
"@
        $xml = [Windows.Data.Xml.Dom.XmlDocument]::new()
        $xml.LoadXml($template)
        $toast = [Windows.UI.Notifications.ToastNotification]::new($xml)
        $appId = '{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}\WindowsPowerShell\v1.0\powershell.exe'
        [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier($appId).Show($toast)
    } catch {
        # Toast API недоступен — тихо пропускаем
    }
}

function Stop-ProcessTree {
    param([int]$ProcessId)
    Write-Log "Запрос на остановку дерева процессов для PID: $ProcessId" -Type 'WARN'
    $childQuery = "ParentProcessId = $ProcessId"
    try {
        $childProcesses = Get-CimInstance -ClassName Win32_Process -Filter $childQuery -ErrorAction Stop
        foreach ($child in $childProcesses) {
            Write-Log "Найден дочерний процесс (PID: $($child.ProcessId), Name: $($child.Name)). Рекурсивная остановка..." -Type 'WARN'
            Stop-ProcessTree -ProcessId $child.ProcessId
        }
    } catch {
        Write-Log "Ошибка при поиске дочерних процессов для PID ${ProcessId}: $($_.Exception.Message)" -Type 'WARN'
    }
    try {
        $procToStop = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
        if ($procToStop) {
            Write-Log "Принудительная остановка процесса (PID: $($procToStop.Id), Name: $($procToStop.Name))." -Type 'WARN'
            $procToStop | Stop-Process -Force -ErrorAction SilentlyContinue
        }
    } catch {
        Write-Log "Ошибка при попытке остановить процесс с PID ${ProcessId}: $($_.Exception.Message)" -Type 'ERROR'
    }
}
function Request-SaveSettings {
    if ($null -eq $script:saveSettingsTimer) {
        $script:saveSettingsTimer = New-Object System.Windows.Forms.Timer
        $script:saveSettingsTimer.Interval = 500
        $script:saveSettingsTimer.Add_Tick({ $this.Stop(); Save-Settings })
    }
    $script:saveSettingsTimer.Stop()
    $script:saveSettingsTimer.Start()
}
function Save-Settings {
    if (-not $script:f -or -not $script:f.IsHandleCreated) { return }

    $previousX = if ($script:settings) { $script:settings.WindowX } else { $script:f.Location.X }
    $previousY = if ($script:settings) { $script:settings.WindowY } else { $script:f.Location.Y }
    $previousW = if ($script:settings) { $script:settings.WindowWidth } else { $script:f.Size.Width }
    $previousH = if ($script:settings) { $script:settings.WindowHeight } else { $script:f.Size.Height }
    $preservedKeysVersion = if ($script:settings) { $script:settings.KeysVersion } else { $null }

    $settingsToSave = [ordered]@{
        WindowX = if ($script:f.WindowState -eq 'Normal') { $script:f.Location.X } else { $previousX }
        WindowY = if ($script:f.WindowState -eq 'Normal') { $script:f.Location.Y } else { $previousY }
        WindowWidth = if ($script:f.WindowState -eq 'Normal') { $script:f.Size.Width } else { $previousW }
        WindowHeight = if ($script:f.WindowState -eq 'Normal') { $script:f.Size.Height } else { $previousH }
        WindowState = $script:f.WindowState
        CompressionLevel = $script:tbCompression.Value
        
        KeyGeneration = $script:numKeyGen.Value
        UnpackStitched = if ($script:cbUnpackStitched) { $script:cbUnpackStitched.Checked } else { $false }
        Fat32Split = if ($script:cbFat32Split) { $script:cbFat32Split.Checked } else { $false }
        ComplexFolders = if ($script:cbComplexFolders) { $script:cbComplexFolders.Checked } else { $false }
        ForceMultiRebuild = if ($script:cbForceMultiRebuild) { $script:cbForceMultiRebuild.Checked } else { $false }
        
        UsedCores = $script:numUsedCores.Value
        ConcurrentTasks = $script:numConcurrentTasks.Value
        TaskPanelVisible = $script:isTaskPanelVisible
        KeysVersion = $preservedKeysVersion
    }
    
    foreach ($key in $script:outputControls.Keys) {
        $currentPath = $script:outputControls[$key].TxtOutFolder.Text
        $defaultPath = $script:defaultOutPaths[$key]
        if ($currentPath -eq $defaultPath) {
            $settingsToSave["LastOutPath_$key"] = "" 
        } else {
            $settingsToSave["LastOutPath_$key"] = $currentPath
        }
    }
    if ($script:txtOutFolder_unpack) {
         $currentPath = $script:txtOutFolder_unpack.Text
         $defaultPath = $script:defaultOutPaths['Unpack']
         if ($currentPath -eq $defaultPath) {
             $settingsToSave["LastOutPath_Unpack"] = ""
         } else {
             $settingsToSave["LastOutPath_Unpack"] = $script:txtOutFolder_unpack.Text
         }
    }

    $columnKeyMap = @{
        'Задача'='Task'; 'Обработка'='Processing'; 'Нач. формат'='StartFormat'; 'Кон. формат'='EndFormat';
        'Нач. размер'='StartSize'; 'Кон. размер'='EndSize'; 'Разница'='Difference';
        'Уровень сжатия'='CompressionLevelGrid'; 'Кол-во файлов'='FileCount'; 'Статус'='Status'; 'Выполнение'='Execution'
    }
    foreach($col in $script:taskGrid.Columns) {
        if ($columnKeyMap.ContainsKey($col.HeaderText)) {
            $englishKey = $columnKeyMap[$col.HeaderText]
            $settingsToSave["TaskGrid_Col_${englishKey}_Width"] = $col.Width
            $settingsToSave["TaskGrid_Col_${englishKey}_Visible"] = $col.Visible
        }
    }
    $newSettings = [pscustomobject]$settingsToSave

    $tempSettingsFile = "$script:settingsFile.tmp"
    $backupSettingsFile = "$script:settingsFile.backup"
    try {
        $newSettings | Export-Csv -Force -Path $tempSettingsFile -NoTypeInformation -Encoding UTF8
        if ((Test-Path -LiteralPath $tempSettingsFile) -and (Get-Item $tempSettingsFile).Length -gt 0) {
            if (Test-Path -LiteralPath $backupSettingsFile) {
                Remove-Item -LiteralPath $backupSettingsFile -Force -ErrorAction SilentlyContinue
            }
            if (Test-Path -LiteralPath $script:settingsFile) {
                Rename-Item -LiteralPath $script:settingsFile -NewName (Split-Path $backupSettingsFile -Leaf) -Force -ErrorAction SilentlyContinue
            }
            Rename-Item -LiteralPath $tempSettingsFile -NewName (Split-Path $script:settingsFile -Leaf) -Force
            $script:settings = $newSettings
        } else {
            throw "Не удалось создать корректный временный файл настроек."
        }
    } catch {
        Write-Log "Ошибка при надежном сохранении настроек: $($_.Exception.Message)" -Type 'ERROR'
        if (Test-Path -LiteralPath $tempSettingsFile) {
            Remove-Item -LiteralPath $tempSettingsFile -Force -ErrorAction SilentlyContinue
        }
    }
}
function Add-CtrlA-Handler {
    param($control)
    $control.Add_KeyDown({
        if ($_.Control -and $_.KeyCode -eq [System.Windows.Forms.Keys]::A) {
            $this.SelectAll()
            $_.SuppressKeyPress = $true
        }
    })
}
# =====================================================
# NACP EDITOR FUNCTIONS (GUI SCOPE)
# =====================================================

# Показывает стилизованное сообщение
function Show-StyledMessage {
    param([string]$Title, [string]$Message, [string]$Type='Info')
    
    $msgForm = New-Object System.Windows.Forms.Form
    $msgForm.Size = [System.Drawing.Size]::new(400, 150)
    $msgForm.StartPosition = "CenterParent"
    $msgForm.FormBorderStyle = "None"
    $msgForm.BackColor = [System.Drawing.ColorTranslator]::FromHtml('#252526')
    
    # Шапка
    $pnlHeader = New-Object System.Windows.Forms.Panel
    $pnlHeader.Size = [System.Drawing.Size]::new(400, 30); $pnlHeader.Location = [System.Drawing.Point]::new(0, 0)
    $pnlHeader.BackColor = [System.Drawing.ColorTranslator]::FromHtml('#2D2D30')
    $pnlHeader.Add_MouseDown({ 
        if ($_.Button -eq [System.Windows.Forms.MouseButtons]::Left) {
            $msgForm.Capture = $false
            $msg = [System.Windows.Forms.Message]::Create($msgForm.Handle, 0xA1, [IntPtr]2, [IntPtr]0)
            $msgForm.DefWndProc([ref]$msg)
        }
    })
    
    $lblTitle = New-Object System.Windows.Forms.Label
    $lblTitle.Text = $Title; $lblTitle.ForeColor = 'White'; $lblTitle.Font = [System.Drawing.Font]::new("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)
    $lblTitle.Location = [System.Drawing.Point]::new(10, 7); $lblTitle.AutoSize = $true
    $pnlHeader.Controls.Add($lblTitle)
    
    $btnClose = New-Object System.Windows.Forms.Label
    $btnClose.Text = "✕"; $btnClose.ForeColor = 'Gray'; $btnClose.Font = [System.Drawing.Font]::new("Segoe UI", 9)
    $btnClose.Location = [System.Drawing.Point]::new(375, 7); $btnClose.Cursor = [System.Windows.Forms.Cursors]::Hand
    $btnClose.Add_Click({ $msgForm.Close() })
    $btnClose.Add_MouseEnter({ $this.ForeColor = 'White' }); $btnClose.Add_MouseLeave({ $this.ForeColor = 'Gray' })
    $pnlHeader.Controls.Add($btnClose)
    $msgForm.Controls.Add($pnlHeader)
    
    # Иконка
    $lblIcon = New-Object System.Windows.Forms.Label
    $lblIcon.Font = [System.Drawing.Font]::new("Segoe UI Emoji", 24)
    $lblIcon.Location = [System.Drawing.Point]::new(20, 50); $lblIcon.Size = [System.Drawing.Size]::new(50, 50)
    $lblIcon.Text = if ($Type -eq 'Error') { "❌" } elseif ($Type -eq 'Warning') { "⚠️" } else { "✅" }
    $msgForm.Controls.Add($lblIcon)
    
    # Текст
    $lblMsg = New-Object System.Windows.Forms.Label
    $lblMsg.Text = $Message; $lblMsg.ForeColor = 'White'; $lblMsg.Font = [System.Drawing.Font]::new("Segoe UI", 10)
    $lblMsg.Location = [System.Drawing.Point]::new(80, 50); $lblMsg.Size = [System.Drawing.Size]::new(300, 60)
    $msgForm.Controls.Add($lblMsg)
    
    # Кнопка ОК
    $btnOk = New-Object System.Windows.Forms.Button
    $btnOk.Text = "OK"; $btnOk.Size = [System.Drawing.Size]::new(80, 25); $btnOk.Location = [System.Drawing.Point]::new(160, 115)
    $btnOk.FlatStyle = "Flat"; $btnOk.ForeColor = "White"; $btnOk.BackColor = [System.Drawing.ColorTranslator]::FromHtml('#007ACC')
    $btnOk.FlatAppearance.BorderSize = 0
    $btnOk.Add_Click({ $msgForm.Close() })
    $msgForm.Controls.Add($btnOk)
    

    
    [void]$msgForm.ShowDialog()
}

function Show-FolderSelectionDialog {
    param([string]$mainFolderName, [array]$subFolders)
    
    $dialog = New-Object System.Windows.Forms.Form
    $dialog.Size = [System.Drawing.Size]::new(500, 300)
    $dialog.StartPosition = "CenterParent"
    $dialog.FormBorderStyle = "None"
    $dialog.BackColor = [System.Drawing.ColorTranslator]::FromHtml('#252526')
    
    # Шапка
    $pnlHeader = New-Object System.Windows.Forms.Panel
    $pnlHeader.Size = [System.Drawing.Size]::new(500, 30); $pnlHeader.Location = [System.Drawing.Point]::new(0, 0)
    $pnlHeader.BackColor = [System.Drawing.ColorTranslator]::FromHtml('#2D2D30')
    $pnlHeader.Add_MouseDown({ 
        if ($_.Button -eq [System.Windows.Forms.MouseButtons]::Left) {
            $dialog.Capture = $false
            $msg = [System.Windows.Forms.Message]::Create($dialog.Handle, 0xA1, [IntPtr]2, [IntPtr]0)
            $dialog.DefWndProc([ref]$msg)
        }
    })
    
    $lblTitle = New-Object System.Windows.Forms.Label
    $lblTitle.Text = "Выберите подпапку для '$mainFolderName'"; $lblTitle.ForeColor = 'White'; $lblTitle.Font = [System.Drawing.Font]::new("Segoe UI", 9, [System.Drawing.FontStyle]::Bold)
    $lblTitle.Location = [System.Drawing.Point]::new(10, 7); $lblTitle.AutoSize = $true
    $pnlHeader.Controls.Add($lblTitle)
    
    $btnClose = New-Object System.Windows.Forms.Label
    $btnClose.Text = "✕"; $btnClose.ForeColor = 'Gray'; $btnClose.Font = [System.Drawing.Font]::new("Segoe UI", 9)
    $btnClose.Location = [System.Drawing.Point]::new(475, 7); $btnClose.Cursor = [System.Windows.Forms.Cursors]::Hand
    $btnClose.Add_Click({ $dialog.DialogResult = "Cancel"; $dialog.Close() })
    $btnClose.Add_MouseEnter({ $this.ForeColor = 'White' }); $btnClose.Add_MouseLeave({ $this.ForeColor = 'Gray' })
    $pnlHeader.Controls.Add($btnClose)
    $dialog.Controls.Add($pnlHeader)
    
    # Список
    $listBox = New-Object System.Windows.Forms.ListBox
    $listBox.Location = [System.Drawing.Point]::new(10, 40)
    $listBox.Size = [System.Drawing.Size]::new(480, 200)
    $listBox.BackColor = [System.Drawing.ColorTranslator]::FromHtml('#1E1E1E')
    $listBox.ForeColor = 'White'
    $listBox.BorderStyle = 'FixedSingle'
    $listBox.Font = [System.Drawing.Font]::new("Segoe UI", 10)
    foreach ($f in $subFolders) { $listBox.Items.Add($f.Name) | Out-Null }
    if ($listBox.Items.Count -gt 0) { $listBox.SelectedIndex = 0 }
    $dialog.Controls.Add($listBox)
    
    # Кнопки
    $btnOk = New-Object System.Windows.Forms.Button
    $btnOk.Text = "Выбрать"; $btnOk.Size = [System.Drawing.Size]::new(100, 30); $btnOk.Location = [System.Drawing.Point]::new(280, 255)
    $btnOk.FlatStyle = "Flat"; $btnOk.ForeColor = "White"; $btnOk.BackColor = [System.Drawing.ColorTranslator]::FromHtml('#007ACC')
    $btnOk.FlatAppearance.BorderSize = 0
    $btnOk.Add_Click({ $dialog.DialogResult = "OK"; $dialog.Close() })
    $dialog.Controls.Add($btnOk)
    
    $btnCancel = New-Object System.Windows.Forms.Button
    $btnCancel.Text = "Отмена"; $btnCancel.Size = [System.Drawing.Size]::new(100, 30); $btnCancel.Location = [System.Drawing.Point]::new(390, 255)
    $btnCancel.FlatStyle = "Flat"; $btnCancel.ForeColor = "White"; $btnCancel.BackColor = [System.Drawing.ColorTranslator]::FromHtml('#3E3E42')
    $btnCancel.FlatAppearance.BorderSize = 0
    $btnCancel.Add_Click({ $dialog.DialogResult = "Cancel"; $dialog.Close() })
    $dialog.Controls.Add($btnCancel)
    
    $script:SelectedSubFolder = $null
    if ($dialog.ShowDialog($script:f) -eq "OK") {
        $script:SelectedSubFolder = $listBox.SelectedItem
    }
    return $script:SelectedSubFolder
}

function Read-NacpGameName {
    param([string]$NacpPath)
    if (-not (Test-Path -LiteralPath $NacpPath)) { return $null }
    try {
        $bytes = [System.IO.File]::ReadAllBytes($NacpPath)
        if ($bytes.Length -lt 0x300) { return $null }
        $nameBytes = $bytes[0..0x1FF]
        $nullIndex = [Array]::IndexOf($nameBytes, [byte]0)
        if ($nullIndex -gt 0) { $nameBytes = $nameBytes[0..($nullIndex-1)] }
        $gameName = [System.Text.Encoding]::UTF8.GetString($nameBytes)
        return $gameName.Trim()
    } catch { return $null }
}

function Write-NacpGameName {
    param([string]$NacpPath, [string]$NewName)
    if (-not (Test-Path -LiteralPath $NacpPath)) { return @{ Success = $false; Error = "Файл не найден: $NacpPath" } }
    try {
        $bytes = [System.IO.File]::ReadAllBytes($NacpPath)
        if ($bytes.Length -lt 0x4000) { return @{ Success = $false; Error = "Некорректный размер NACP файла" } }
        $nameBytes = [System.Text.Encoding]::UTF8.GetBytes($NewName)
        if ($nameBytes.Length -gt 0x1FF) { $nameBytes = $nameBytes[0..0x1FE] }
        $nameBuffer = New-Object byte[] 0x200
        [Array]::Copy($nameBytes, $nameBuffer, $nameBytes.Length)
        for ($i = 0; $i -lt 16; $i++) {
            $offset = $i * 0x300
            [Array]::Copy($nameBuffer, 0, $bytes, $offset, 0x200)
        }
        [System.IO.File]::WriteAllBytes($NacpPath, $bytes)
        return @{ Success = $true; Error = $null }
    } catch { return @{ Success = $false; Error = $_.Exception.Message } }
}

# Показывает диалог редактирования NACP
function Show-NacpEditorDialog {
    # Глобальные переменные для временного хранения состояния диалога
    $script:nacpEditContext = @{
        TempDir = $null
        NacpPath = $null
        IsNca = $false
        OriginalFile = $null
    }

    $dialog = New-Object System.Windows.Forms.Form
    $dialog.Size = [System.Drawing.Size]::new(500, 230)
    $dialog.StartPosition = "CenterParent"
    $dialog.FormBorderStyle = "None"
    $dialog.BackColor = [System.Drawing.ColorTranslator]::FromHtml('#252526')
    $dialog.AllowDrop = $true
    
    # == КАСТОМНАЯ ШАПКА ==
    $pnlHeader = New-Object System.Windows.Forms.Panel
    $pnlHeader.Size = [System.Drawing.Size]::new(500, 32); $pnlHeader.Location = [System.Drawing.Point]::new(0, 0)
    $pnlHeader.BackColor = [System.Drawing.ColorTranslator]::FromHtml('#2D2D30')
    
    $lblTitle = New-Object System.Windows.Forms.Label
    $lblTitle.Text = "Редактор NACP"; $lblTitle.ForeColor = '#AAAAAA'; $lblTitle.Font = [System.Drawing.Font]::new("Segoe UI", 9)
    $lblTitle.Location = [System.Drawing.Point]::new(10, 8); $lblTitle.AutoSize = $true
    
    $btnClose = New-Object System.Windows.Forms.Label
    $btnClose.Text = "✕"; $btnClose.ForeColor = '#AAAAAA'; $btnClose.Font = [System.Drawing.Font]::new("Arial", 10)
    $btnClose.Location = [System.Drawing.Point]::new(475, 5); $btnClose.Size = [System.Drawing.Size]::new(20, 20)
    $btnClose.Cursor = [System.Windows.Forms.Cursors]::Hand
    $btnClose.Add_MouseEnter({ $this.ForeColor = 'White'; $this.BackColor = '#C42B1C' })
    $btnClose.Add_MouseLeave({ $this.ForeColor = '#AAAAAA'; $this.BackColor = 'Transparent' })
    $btnClose.Add_Click({ $dialog.Close() })
    
    $pnlHeader.Controls.AddRange(@($lblTitle, $btnClose))
    
    # Перетаскивание окна за шапку
    $pnlHeader.Add_MouseDown({ 
        if ($_.Button -eq [System.Windows.Forms.MouseButtons]::Left) {
            $dialog.Capture = $false
            $msg = [System.Windows.Forms.Message]::Create($dialog.Handle, 0xA1, [IntPtr]2, [IntPtr]0)
            $dialog.DefWndProc([ref]$msg)
        }
    })
    $dialog.Controls.Add($pnlHeader)
    
    # == КОНТЕНТ ==
    
    $lblFile = New-Object System.Windows.Forms.Label
    $lblFile.Text = "Файл (NACP/NCA):"; $lblFile.Location = [System.Drawing.Point]::new(10, 50); $lblFile.Size = [System.Drawing.Size]::new(110, 20); $lblFile.ForeColor = 'White'
    
    $txtFile = New-Object System.Windows.Forms.TextBox
    $txtFile.Location = [System.Drawing.Point]::new(120, 47); $txtFile.Size = [System.Drawing.Size]::new(280, 22)
    $txtFile.BackColor = [System.Drawing.ColorTranslator]::FromHtml('#333333'); $txtFile.ForeColor = 'White'; $txtFile.BorderStyle = 'FixedSingle'
    
    $btnBrowse = New-Object System.Windows.Forms.Button
    $btnBrowse.Text = "..."; $btnBrowse.Location = [System.Drawing.Point]::new(405, 46); $btnBrowse.Size = [System.Drawing.Size]::new(30, 24)
    $btnBrowse.BackColor = [System.Drawing.ColorTranslator]::FromHtml('#3E3E42'); $btnBrowse.ForeColor = 'White'; $btnBrowse.FlatStyle = 'Flat'
    $btnBrowse.FlatAppearance.BorderSize = 0
    
    $lblHelper = New-Object System.Windows.Forms.Label
    $lblHelper.Text = "(Перетащите файл сюда)"; $lblHelper.Location = [System.Drawing.Point]::new(120, 70); $lblHelper.Size = [System.Drawing.Size]::new(200, 15); $lblHelper.ForeColor = 'Gray'; $lblHelper.Font = [System.Drawing.Font]::new("Segoe UI", 8)
    
    $lblCurrent = New-Object System.Windows.Forms.Label
    $lblCurrent.Text = "Текущее имя:"; $lblCurrent.Location = [System.Drawing.Point]::new(10, 95); $lblCurrent.Size = [System.Drawing.Size]::new(110, 20); $lblCurrent.ForeColor = 'White'
    
    $txtCurrent = New-Object System.Windows.Forms.TextBox
    $txtCurrent.Location = [System.Drawing.Point]::new(120, 92); $txtCurrent.Size = [System.Drawing.Size]::new(315, 22); $txtCurrent.ReadOnly = $true
    $txtCurrent.BackColor = [System.Drawing.ColorTranslator]::FromHtml('#1E1E1E'); $txtCurrent.ForeColor = 'Gray'; $txtCurrent.BorderStyle = 'FixedSingle'
    
    $lblNew = New-Object System.Windows.Forms.Label
    $lblNew.Text = "Новое имя:"; $lblNew.Location = [System.Drawing.Point]::new(10, 130); $lblNew.Size = [System.Drawing.Size]::new(110, 20); $lblNew.ForeColor = 'White'
    
    $txtNew = New-Object System.Windows.Forms.TextBox
    $txtNew.Location = [System.Drawing.Point]::new(120, 127); $txtNew.Size = [System.Drawing.Size]::new(315, 22)
    $txtNew.BackColor = [System.Drawing.ColorTranslator]::FromHtml('#333333'); $txtNew.ForeColor = 'White'; $txtNew.BorderStyle = 'FixedSingle'
    
    $btnSave = New-Object System.Windows.Forms.Button
    $btnSave.Text = "Сохранить"; $btnSave.Location = [System.Drawing.Point]::new(280, 170); $btnSave.Size = [System.Drawing.Size]::new(90, 28)
    $btnSave.BackColor = [System.Drawing.ColorTranslator]::FromHtml('#007ACC'); $btnSave.ForeColor = 'White'; $btnSave.FlatStyle = 'Flat'
    $btnSave.FlatAppearance.BorderSize = 0
    
    $btnCancel = New-Object System.Windows.Forms.Button
    $btnCancel.Text = "Отмена"; $btnCancel.Location = [System.Drawing.Point]::new(380, 170); $btnCancel.Size = [System.Drawing.Size]::new(70, 28)
    $btnCancel.BackColor = [System.Drawing.ColorTranslator]::FromHtml('#3E3E42'); $btnCancel.ForeColor = 'White'; $btnCancel.FlatStyle = 'Flat'
    $btnCancel.FlatAppearance.BorderSize = 0
    $btnCancel.Add_Click({ $dialog.Close() })
    
    $lblStatus = New-Object System.Windows.Forms.Label
    $lblStatus.Text = ""; $lblStatus.Location = [System.Drawing.Point]::new(10, 205); $lblStatus.Size = [System.Drawing.Size]::new(460, 20); $lblStatus.ForeColor = 'Yellow'

    # Логика загрузки файла (вынесена для использования в DragDrop)
    $script:LoadNacpFile = {
        param($filePath)
        
        # Очистка предыдущего контекста
        if ($script:nacpEditContext.TempDir -and (Test-Path $script:nacpEditContext.TempDir)) {
            Remove-Item -LiteralPath $script:nacpEditContext.TempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
        $script:nacpEditContext = @{ TempDir = $null; NacpPath = $null; IsNca = $false; OriginalFile = $filePath }
        
        $txtFile.Text = $filePath
        $ext = [System.IO.Path]::GetExtension($filePath).ToLower()
        $lblStatus.Text = "Чтение файла..."
        [System.Windows.Forms.Application]::DoEvents()
        
        if ($ext -eq '.nca') {
            if (-not (Test-Path $script:hactoolnet)) {
                Show-StyledMessage -Title "Ошибка" -Message "hactoolnet.exe не найден!" -Type 'Error'
                return
            }
            
            $temp = Join-Path $env:TEMP ("sb_nacp_" + [guid]::NewGuid().ToString())
            [void](New-Item -ItemType Directory -Force $temp)
            
            $proc = New-Object System.Diagnostics.ProcessStartInfo
            $proc.FileName = $script:hactoolnet
            $proc.Arguments = "-k `"$script:key`" `"$filePath`" --romfsdir `"$temp`""
            $proc.UseShellExecute = $false; $proc.CreateNoWindow = $true
            
            try {
                $p = [System.Diagnostics.Process]::Start($proc)
                $p.WaitForExit()
                
                if ($p.ExitCode -ne 0) { throw "Ошибка hactoolnet code $($p.ExitCode)" }
                
                $nacp = Get-ChildItem $temp -Filter "control.nacp" -Recurse | Select-Object -First 1
                if ($nacp) {
                    $script:nacpEditContext.IsNca = $true
                    $script:nacpEditContext.TempDir = $temp
                    $script:nacpEditContext.NacpPath = $nacp.FullName
                    $name = Read-NacpGameName -NacpPath $nacp.FullName
                    $lblStatus.Text = "NCA распакован."
                } else {
                    $lblStatus.Text = "control.nacp не найден!"
                    Remove-Item -LiteralPath $temp -Recurse -Force
                    $name = $null
                }
            } catch {
                 Show-StyledMessage -Title "Ошибка" -Message "Ошибка открытия NCA: $_" -Type 'Error'
                 $lblStatus.Text = "Ошибка открытия."
                 $name = $null
            }
        } else {
            $script:nacpEditContext.NacpPath = $filePath
            $name = Read-NacpGameName -NacpPath $filePath
            $lblStatus.Text = "NACP файл открыт."
        }
        
        $txtCurrent.Text = if ($name) { $name } else { "..." }
        $txtNew.Text = $txtCurrent.Text
    }
    
    $btnBrowse.Add_Click({
        $ofd = New-Object System.Windows.Forms.OpenFileDialog
        $ofd.Filter = "Supported files (*.nacp;*.nca)|*.nacp;*.nca|NACP files (*.nacp)|*.nacp|NCA files (*.nca)|*.nca"
        if ($ofd.ShowDialog() -eq 'OK') {
            & $script:LoadNacpFile -filePath $ofd.FileName
        }
    })
    
    # Drag & Drop Handlers
    $dialog.Add_DragEnter({
        if ($_.Data.GetDataPresent([System.Windows.Forms.DataFormats]::FileDrop)) {
            $_.Effect = [System.Windows.Forms.DragDropEffects]::Copy
        }
    })
    
    $dialog.Add_DragDrop({
        $files = $_.Data.GetData([System.Windows.Forms.DataFormats]::FileDrop)
        if ($files.Count -gt 0) {
            $file = $files[0]
            $ext = [System.IO.Path]::GetExtension($file).ToLower()
            if ($ext -in @('.nacp', '.nca')) {
                & $script:LoadNacpFile -filePath $file
            }
        }
    })
    
    $btnSave.Add_Click({
        if (-not $script:nacpEditContext.NacpPath) { return }
        if ([string]::IsNullOrWhiteSpace($txtNew.Text)) { return }
        
        $lblStatus.Text = "Сохранение..."
        [System.Windows.Forms.Application]::DoEvents()
        
        # 1. Запись имени
        $result = Write-NacpGameName -NacpPath $script:nacpEditContext.NacpPath -NewName $txtNew.Text
        if (-not $result.Success) {
            Show-StyledMessage -Title "Ошибка" -Message "Ошибка записи NACP: $($result.Error)" -Type 'Error'
            return
        }
        
        # 2. Если NCA - перепаковка
        if ($script:nacpEditContext.IsNca) {
            $lblStatus.Text = "Перепаковка NCA..."
            [System.Windows.Forms.Application]::DoEvents()
            
            $hacPackInfo = Get-Item "$script:tdir\com.github.nozwock.yanu\hacPack.exe" -ErrorAction SilentlyContinue 
            if (-not $hacPackInfo) { 
                $hacPackInfo = Get-ChildItem "$script:tdir" -Filter "hacPack.exe" -Recurse | Select -First 1 
            }
            
            if ($hacPackInfo) {
                $repackOut = Join-Path $script:nacpEditContext.TempDir "out_repack"
                [void](New-Item -ItemType Directory -Force $repackOut)
                
                # Извлекаем TitleID из пути к файлу (папка может содержать [TitleID])
                # NCA файлы называются по хешу, но папка обычно содержит TitleID в квадратных скобках
                $fullPath = $script:nacpEditContext.OriginalFile
                $tidMatch = [regex]::Match($fullPath, '\[([0-9a-fA-F]{16})\]')
                if ($tidMatch.Success) {
                    $repackTitleId = $tidMatch.Groups[1].Value.ToLower()
                } else {
                    # Fallback: читаем TitleID из оригинального NCA через hactoolnet
                    try {
                        $htOutput = & $script:hactoolnet "-k" $script:key $script:nacpEditContext.OriginalFile 2>&1 | Out-String
                        $htMatch = [regex]::Match($htOutput, 'TitleID[:\s]+([0-9a-fA-F]{16})')
                        if ($htMatch.Success) {
                            $repackTitleId = $htMatch.Groups[1].Value.ToLower()
                        } else {
                            $repackTitleId = "0100000000000000"
                            $lblStatus.Text = "ВНИМАНИЕ: TitleID не найден!"
                        }
                    } catch {
                        $repackTitleId = "0100000000000000"
                        $lblStatus.Text = "ВНИМАНИЕ: Ошибка чтения TitleID!"
                    }
                }
                
                $hacPackArgs = "-k `"$script:key`" --type nca --ncatype control --romfsdir `"$($script:nacpEditContext.TempDir)`" --titleid $repackTitleId -o `"$repackOut`""
                
                # DEBUG: Show command being executed
                $lblStatus.Text = "Перепаковка... TitleID: $repackTitleId"
                [System.Windows.Forms.Application]::DoEvents()
                
                $proc = New-Object System.Diagnostics.ProcessStartInfo
                $proc.FileName = $hacPackInfo.FullName
                $proc.Arguments = $hacPackArgs
                $proc.UseShellExecute = $false
                $proc.CreateNoWindow = $true
                # НЕ перенаправляем stdout/stderr чтобы избежать deadlock
                
                $p = [System.Diagnostics.Process]::Start($proc)
                $finished = $p.WaitForExit(30000)  # 30 секунд таймаут
                if (-not $finished) {
                    $p.Kill()
                    Show-StyledMessage -Title "Ошибка" -Message "hacPack завис (таймаут 30 сек)" -Type 'Error'
                    return
                }
                
                $newNca = Get-ChildItem $repackOut -Filter "*.nca" -ErrorAction SilentlyContinue | Select -First 1
                if ($p.ExitCode -eq 0 -and $newNca) {
                     try {
                        Copy-Item -LiteralPath $newNca.FullName -Destination $script:nacpEditContext.OriginalFile -Force
                        $lblStatus.Text = "NCA успешно обновлен!"
                        Show-StyledMessage -Title "Успех" -Message "NCA файл успешно пересобран! TitleID: $repackTitleId" -Type 'Success'
                     } catch {
                        Show-StyledMessage -Title "Ошибка" -Message "Ошибка замены NCA файла: $_" -Type 'Error'
                     }
                } else {
                    Show-StyledMessage -Title "Ошибка hacPack" -Message "hacPack завершился с кодом $($p.ExitCode)`nTitleID: $repackTitleId" -Type 'Error'
                }
            } else {
                 Show-StyledMessage -Title "Внимание" -Message "hacPack.exe не найден! Изменения применены только во временный файл." -Type 'Warning'
            }
        } else {
             Show-StyledMessage -Title "Успех" -Message "Имя игры успешно изменено в NACP файле!" -Type 'Success'
        }
        
        $dialog.DialogResult = [System.Windows.Forms.DialogResult]::OK
        $dialog.Close()
    })
    
    $dialog.Add_FormClosing({
        if ($script:nacpEditContext.TempDir -and (Test-Path $script:nacpEditContext.TempDir)) {
             Remove-Item -LiteralPath $script:nacpEditContext.TempDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    })
    
    $dialog.Controls.AddRange(@($lblFile, $txtFile, $btnBrowse, $lblHelper, $lblCurrent, $txtCurrent, $lblNew, $txtNew, $btnSave, $btnCancel, $lblStatus))
    [void]$dialog.ShowDialog()
}


function Write-Log {
    param(
        [string]$Message,
        [string]$Type = 'INFO',
        [string]$TaskID = $null
    )
    $timestamp = Get-Date -Format 'HH:mm:ss.fff'
    $sanitizedMessage = $Message -replace '[\x00-\x08\x0B\x0C\x0E-\x1F]'
    $logEntry = "[{0}] [{1}] {2}" -f $timestamp, $Type.ToUpper(), $sanitizedMessage
    $syncObject = $script:logStorage
    [System.Threading.Monitor]::Enter($syncObject)
    try {
        if ($TaskID) {
            if (-not $script:logStorage.ContainsKey($TaskID)) {
                $script:logStorage[$TaskID] = [System.Collections.Generic.List[string]]::new()
            }
            $script:logStorage[$TaskID].Add($logEntry)
        } else {
            $script:generalLog.Add($logEntry)
        }
    } finally {
        [System.Threading.Monitor]::Exit($syncObject)
    }
    
    # ПЕРСИСТЕНТНОЕ ЛОГИРОВАНИЕ В ФАЙЛ (для диагностики после закрытия)
    if ($script:logFileWriter) {
        try {
            $fileLogLine = if ($TaskID) { "[{0}] [{1}] [Task:{2}] {3}" -f $timestamp, $Type.ToUpper(), $TaskID, $sanitizedMessage } else { $logEntry }
            $script:logFileWriter.WriteLine($fileLogLine)
            # Flush каждые 10 записей для баланса между надёжностью и производительностью
            $script:logFileWriteCount++
            if ($script:logFileWriteCount -ge 10) {
                $script:logFileWriter.Flush()
                $script:logFileWriteCount = 0
            }
        } catch {
            # Не блокируем работу при ошибке записи в файл
        }
    }
    
    # МГНОВЕННОЕ ОБНОВЛЕНИЕ: Добавляем только новую строку (инкрементально)
    if ($script:logBox -and $script:logBox.IsHandleCreated -and -not $script:logBox.IsDisposed) {
        try {
            $appendAction = [Action[string]]{ 
                param($entry)
                Append-LogEntry $entry
            }
            if ($script:logBox.InvokeRequired) {
                $script:logBox.Invoke($appendAction, @($logEntry))
            } else {
                Append-LogEntry $logEntry
            }
        } catch {
            # Игнорируем ошибки при обновлении UI (например, если форма закрывается)
        }
    }
}

# Инкрементальное добавление одной строки в лог (БЫСТРО)
function Append-LogEntry {
    param([string]$line)
    if (-not $script:logBox -or $script:logBox.IsDisposed) { return }
    $logBox = $script:logBox
    
    # Если выбраны конкретные задачи — не добавляем общие логи в реальном времени
    if ($script:taskGrid.SelectedRows.Count -gt 0 -and $script:taskGrid.Visible) { return }
    
    $isAtBottom = ($logBox.GetPositionFromCharIndex($logBox.TextLength).Y - $logBox.ClientSize.Height - $logBox.AutoScrollOffset.Y) -lt 50
    
    $match = [regex]::Match($line, "^(\[.*?\])\s(\[.*?\])\s(.*)")
    if ($match.Success) {
        $logBox.SelectionColor = [Color]::Gray
        $logBox.AppendText($match.Groups[1].Value + " ") 
        $type = $match.Groups[2].Value.ToUpper()
        $typeColor = switch -regex ($type) { 
            '\[INFO\]' { [Color]::DodgerBlue } 
            '\[DEBUG\]' { [Color]::Gray } 
            '\[SUCCESS\]' { [Color]::LightGreen } 
            '\[ERROR\]|\[TOOL_ERROR\]' { [Color]::LightCoral } 
            '\[WARN\]' { [Color]::Yellow } 
            default { [Color]::LightGray } 
        }
        $logBox.SelectionColor = $typeColor
        $logBox.AppendText($type + " ")
        $logBox.SelectionColor = [Color]::White
        $logBox.AppendText($match.Groups[3].Value + [Environment]::NewLine) 
    } else {
        $logBox.SelectionColor = [Color]::White
        $logBox.AppendText($line + [Environment]::NewLine)
    }
    
    if ($isAtBottom) { $logBox.ScrollToCaret() }
}

# (Req 6) Полная перерисовка лога — вызывается ТОЛЬКО при смене выбранной задачи
function Update-LogView {
    if (-not $script:logBox -or $script:logBox.IsDisposed) { return }
    $logBox = $script:logBox
    
    $logBox.SuspendLayout()
    
    # 1. Собираем данные для отображения
    $logSource = [System.Collections.Generic.List[string]]::new()
    $syncObject = $script:logStorage
    
    [System.Threading.Monitor]::Enter($syncObject)
    try {
        if ($script:taskGrid.SelectedRows.Count -gt 0 -and $script:taskGrid.Visible) {
            # РЕЖИМ: ВЫБРАНЫ ЗАДАЧИ (Только логи конкретных задач)
            $selectedTaskIDs = $script:taskGrid.SelectedRows | ForEach-Object { $_.Tag.TaskID }
            foreach ($id in $selectedTaskIDs) {
                if ($script:logStorage.ContainsKey($id)) {
                    $logSource.AddRange($script:logStorage[$id])
                }
            }
        } else {
            # РЕЖИМ: ОБЩИЙ (Общий лог + Логи ВСЕХ задач)
            $logSource.AddRange($script:generalLog)
            foreach ($key in $script:logStorage.Keys) {
                $logSource.AddRange($script:logStorage[$key])
            }
        }
        
        # 2. Сортировка по времени (формат [HH:mm:ss.fff])
        $logSource.Sort({
            param($a, $b)
            if ($a.Length -gt 12 -and $b.Length -gt 12) {
                return [string]::Compare($a.Substring(0, 12), $b.Substring(0, 12))
            }
            return 0
        })
    } finally {
        [System.Threading.Monitor]::Exit($syncObject)
    }

    # 3. Отрисовка
    $logBox.Clear()
    
    # Оптимизация: Если строк очень много, показываем только последние 1000
    $startIndex = 0
    if ($logSource.Count -gt 1000) {
        $startIndex = $logSource.Count - 1000
        $logBox.SelectionColor = [Color]::Gray
        $logBox.AppendText("... (старые записи скрыты для производительности, показана последняя 1000 строк) ...`n")
    }

    for ($i = $startIndex; $i -lt $logSource.Count; $i++) {
        Append-LogEntry $logSource[$i]
    }

    $logBox.ResumeLayout()
    $logBox.ScrollToCaret()
}
function Format-FileSize {
    param([double]$bytes)
    $suffixes = 'B', 'KB', 'MB', 'GB', 'TB', 'PB'
    if ($bytes -eq 0) { return "0 B" }
    try {
        $power = [math]::Floor([math]::Log($bytes, 1024))
        $value = $bytes / ([math]::Pow(1024, $power))
        return "{0:N2} {1}" -f $value, $suffixes[$power]
    } catch {
        return "N/A"
    }
}
function Get-ItemSize {
    param($itemInfo)
    $totalSize = [long]0
    try {
        if ($itemInfo -is [System.IO.DirectoryInfo] -and $itemInfo.PSIsContainer) {
            $measure = Get-ChildItem -LiteralPath $itemInfo.FullName -Recurse -File -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum
            if ($measure.Sum) { $totalSize = $measure.Sum }
        } elseif ($itemInfo -is [System.IO.FileInfo]) {
            $totalSize = $itemInfo.Length
        } elseif ($itemInfo -is [string]) {
            if (Test-Path -LiteralPath $itemInfo -PathType Container) {
                $measure = Get-ChildItem -LiteralPath $itemInfo -Recurse -File -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum
                if ($measure.Sum) { $totalSize = $measure.Sum }
            } elseif (Test-Path -LiteralPath $itemInfo -PathType Leaf) {
                $totalSize = (Get-Item -LiteralPath $itemInfo -Force -ErrorAction SilentlyContinue).Length
            }
        }
    } catch {
        Write-Log "Ошибка подсчёта размера: $($_.Exception.Message)" -Type 'DEBUG'
    }
    return $totalSize
}
function YEOpenFile {
    param($title = "Выберите файл", $filter = "Все файлы (*.*)|*.*")
    $fileDialog = New-Object OpenFileDialog
    $fileDialog.Title = $title
    $fileDialog.Filter = $filter
    if ($fileDialog.ShowDialog() -eq [DialogResult]::OK) {
        return $fileDialog.FileName
    }
    return $null
}
function YEFolder {
    # Использование OpenFileDialog для имитации современного диалога выбора папки
    $dialog = New-Object OpenFileDialog
    $dialog.Title = "Выберите выходную папку"
    $dialog.Filter = "Папки|*.this.is.a.folder"
    $dialog.FileName = "Выбор папки"
    # Эти три свойства - ключ к работе
    $dialog.ValidateNames = $false
    $dialog.CheckFileExists = $false
    $dialog.CheckPathExists = $true
    if ($dialog.ShowDialog($script:f) -eq 'OK') {
        # Возвращаем путь к директории, в которой находится "выбранный" элемент
        return [Path]::GetDirectoryName($dialog.FileName)
    }
    return $null
}
function Show-CustomInputBox {
    param($prompt, $title, $defaultValue)
    $inputBox = New-Object Form
    $inputBox.BackColor = [ColorTranslator]::FromHtml('#2D2D30')
    $inputBox.ForeColor = [Color]::White
    $inputBox.Font = $script:regularFont
    $inputBox.FormBorderStyle = [FormBorderStyle]::None
    $inputBox.StartPosition = [FormStartPosition]::CenterParent
    $inputBox.Size = New-Object Size(360, 180)
    
    # Кастомная шапка
    $titleBar = New-Object Panel
    $titleBar.Size = New-Object Size($inputBox.Width, 32)
    $titleBar.Location = '0, 0'
    $titleBar.BackColor = [ColorTranslator]::FromHtml('#1e1e1e')
    
    $titleLabel = New-Object Label
    $titleLabel.Text = $title
    $titleLabel.ForeColor = [ColorTranslator]::FromHtml('#AAAAAA')
    $titleLabel.Font = $script:boldFont
    $titleLabel.Location = '10, 8'
    $titleLabel.AutoSize = $true
    
    $closeBtn = New-Object Button
    $closeBtn.Text = '×'
    $closeBtn.Size = '32, 32'
    $closeBtn.Location = "$($inputBox.Width - 32), 0"
    $closeBtn.FlatStyle = [FlatStyle]::Flat
    $closeBtn.FlatAppearance.BorderSize = 0
    $closeBtn.BackColor = [ColorTranslator]::FromHtml('#1e1e1e')
    $closeBtn.ForeColor = [ColorTranslator]::FromHtml('#AAAAAA')
    $closeBtn.Font = New-Object System.Drawing.Font("Arial", 12, [System.Drawing.FontStyle]::Bold)
    $closeBtn.Cursor = [System.Windows.Forms.Cursors]::Hand
    $closeBtn.Add_MouseEnter({ $this.BackColor = [ColorTranslator]::FromHtml('#C53030'); $this.ForeColor = [Color]::White })
    $closeBtn.Add_MouseLeave({ $this.BackColor = [ColorTranslator]::FromHtml('#1e1e1e'); $this.ForeColor = [ColorTranslator]::FromHtml('#AAAAAA') })
    $closeBtn.Add_Click({ $inputBox.DialogResult = [DialogResult]::Cancel; $inputBox.Close() })
    
    $titleBar.Controls.Add($titleLabel)
    $titleBar.Controls.Add($closeBtn)
    $inputBox.Controls.Add($titleBar)
    
    # Перемещение окна за шапку
    $script:drag = $false; $script:startPoint = New-Object System.Drawing.Point(0,0)
    $titleBar.Add_MouseDown({ param($s, $e); if ($e.Button -eq 'Left') { $script:drag = $true; $script:startPoint = $e.Location } })
    $titleBar.Add_MouseMove({ param($s, $e); if ($script:drag) { $inputBox.Location = New-Object System.Drawing.Point([int]($inputBox.Location.X + $e.X - $script:startPoint.X), [int]($inputBox.Location.Y + $e.Y - $script:startPoint.Y)) } })
    $titleBar.Add_MouseUp({ param($s, $e); $script:drag = $false })
    $titleLabel.Add_MouseDown({ param($s, $e); if ($e.Button -eq 'Left') { $script:drag = $true; $script:startPoint = $e.Location } })
    $titleLabel.Add_MouseMove({ param($s, $e); if ($script:drag) { $inputBox.Location = New-Object System.Drawing.Point([int]($inputBox.Location.X + $e.X - $script:startPoint.X), [int]($inputBox.Location.Y + $e.Y - $script:startPoint.Y)) } })
    $titleLabel.Add_MouseUp({ param($s, $e); $script:drag = $false })

    # Текст запроса
    $promptLabel = New-Object Label
    $promptLabel.Text = $prompt
    $promptLabel.Font = $script:regularFont
    $promptLabel.Location = '15, 45'
    $promptLabel.AutoSize = $true
    $inputBox.Controls.Add($promptLabel)
    
    $textBox = $null
    $keyBoxes = @()
    
    if ($title -eq "Версия ключей") {
        # Стилизованные 4 окна для версии ключей (XX.X.X)
        $startX = 100
        for ($i = 0; $i -lt 4; $i++) {
            $panel = New-Object Panel
            $panel.BackColor = [ColorTranslator]::FromHtml('#007ACC')
            $panel.Padding = New-Object System.Windows.Forms.Padding(1)
            $panel.Size = New-Object Size(32, 40)
            
            $tb = New-Object TextBox
            $tb.BorderStyle = [BorderStyle]::None
            $tb.BackColor = [ColorTranslator]::FromHtml('#1e1e1e')
            $tb.ForeColor = [ColorTranslator]::FromHtml('#4ec9b0')
            $tb.Font = New-Object System.Drawing.Font("Arial", 16, [System.Drawing.FontStyle]::Bold)
            $tb.Dock = [DockStyle]::Fill
            $tb.TextAlign = [System.Windows.Forms.HorizontalAlignment]::Center
            $tb.MaxLength = 1
            $tb.Text = ""
            $tb.Name = "kBox$i"
            $tb.Tag = $i
            
            $panel.Controls.Add($tb)
            $panel.Location = New-Object System.Drawing.Point($startX, 75)
            $inputBox.Controls.Add($panel)
            $keyBoxes += $tb
            
            $startX += 36
            if ($i -eq 1 -or $i -eq 2) {
                $dot = New-Object Label
                $dot.Text = "."
                $dot.ForeColor = [ColorTranslator]::FromHtml('#4ec9b0')
                $dot.Font = New-Object System.Drawing.Font("Arial", 18, [System.Drawing.FontStyle]::Bold)
                $dot.AutoSize = $true
                $dot.Location = New-Object System.Drawing.Point(($startX - 3), 75)
                $inputBox.Controls.Add($dot)
                $startX += 17
            }
        }
        
        $def = $defaultValue -replace '\D',''
        for ($i=0; $i -lt 4 -and $i -lt $def.Length; $i++) {
            $keyBoxes[$i].Text = $def[$i].ToString()
        }
        
        foreach ($tb in $keyBoxes) {
            $tb.Add_TextChanged({
                param($s, $e)
                if ($s.Text.Length -eq 1) {
                    if ($s.Text -notmatch '\d') { $s.Text = '' }
                    else {
                        $idx = [int]$s.Tag
                        if ($idx -lt 3) {
                            $nextTb = $s.FindForm().Controls.Find("kBox$($idx+1)", $true)[0]
                            if ($nextTb) { $nextTb.Focus(); $nextTb.SelectAll() }
                        }
                    }
                }
            })
            $tb.Add_KeyDown({
                param($s, $e)
                if ($e.KeyCode -eq 'Back' -and $s.Text.Length -eq 0) {
                    $idx = [int]$s.Tag
                    if ($idx -gt 0) { 
                        $prevTb = $s.FindForm().Controls.Find("kBox$($idx-1)", $true)[0]
                        if ($prevTb) { $prevTb.Focus(); $prevTb.SelectionStart = $prevTb.Text.Length }
                        $e.Handled = $true
                    }
                }
            })
        }
        
        $inputBox.Add_Shown({
            $target = $keyBoxes[0]; $dLen = ($defaultValue -replace '\D','').Length
            if ($dLen -gt 0) { $targetIndex = [Math]::Min($dLen, 3); $target = $keyBoxes[$targetIndex] }
            $target.Focus(); $target.SelectAll()
        })
    } else {
        # Стандартное поле ввода (для всех остальных случаев)
        $inputPanel = New-Object Panel
        $inputPanel.BackColor = [ColorTranslator]::FromHtml('#007ACC')
        $inputPanel.Padding = New-Object System.Windows.Forms.Padding(1)
        $inputPanel.Location = '15, 75'
        $inputPanel.Size = '330, 26'
        
        $textBox = New-Object TextBox
        $textBox.Font = $script:boldFont
        $textBox.BackColor = [ColorTranslator]::FromHtml('#1e1e1e')
        $textBox.ForeColor = [Color]::White
        $textBox.BorderStyle = [BorderStyle]::None
        $textBox.Dock = [DockStyle]::Fill
        $textBox.Text = $defaultValue
        
        $inputPanel.Controls.Add($textBox)
        $inputBox.Controls.Add($inputPanel)
        $inputBox.Tag = $textBox
        
        $inputBox.Add_Shown({ $this.Tag.Focus() })
    }
    
    # Кнопки
    $okButton = New-Object Button
    $cancelButton = New-Object Button
    $inputBox.AcceptButton = $okButton
    $inputBox.CancelButton = $cancelButton
    
    $okButton.Text = 'OK'
    $okButton.DialogResult = [DialogResult]::OK
    $okButton.Font = $script:boldFont
    $okButton.BackColor = [ColorTranslator]::FromHtml('#007ACC')
    $okButton.ForeColor = [Color]::White
    $okButton.FlatStyle = [FlatStyle]::Flat
    $okButton.FlatAppearance.BorderSize = 0
    $okButton.Size = '100, 30'
    $okButton.Location = '70, 130'
    $okButton.Cursor = [System.Windows.Forms.Cursors]::Hand
    $inputBox.Controls.Add($okButton)
    
    $cancelButton.Text = 'Отмена'
    $cancelButton.DialogResult = [DialogResult]::Cancel
    $cancelButton.Font = $script:boldFont
    $cancelButton.BackColor = [ColorTranslator]::FromHtml('#3E3E42')
    $cancelButton.ForeColor = [Color]::White
    $cancelButton.FlatStyle = [FlatStyle]::Flat
    $cancelButton.FlatAppearance.BorderSize = 0
    $cancelButton.Size = '100, 30'
    $cancelButton.Location = '190, 130'
    $cancelButton.Cursor = [System.Windows.Forms.Cursors]::Hand
    $inputBox.Controls.Add($cancelButton)
    
    # Отрисовка внешней рамки (чтобы форма без BorderStyle не сливалась)
    $inputBox.Add_Paint({
        param($s, $e)
        $pen = New-Object System.Drawing.Pen([ColorTranslator]::FromHtml('#3E3E42'), 1)
        $rect = New-Object System.Drawing.Rectangle(0, 0, ($inputBox.Width - 1), ($inputBox.Height - 1))
        $e.Graphics.DrawRectangle($pen, $rect)
        $pen.Dispose()
    })
    
    if ($inputBox.ShowDialog($script:f) -eq 'OK') {
        if ($title -eq "Версия ключей") {
            return "$($keyBoxes[0].Text)$($keyBoxes[1].Text).$($keyBoxes[2].Text).$($keyBoxes[3].Text)"
        } else {
            return $textBox.Text
        }
    } else {
        return $null
    }
}
function YECustomMsg {
    param($msg, $parent)
    $msgBox = New-Object Form
    $msgBox.BackColor = [ColorTranslator]::FromHtml('#2D2D30')
    $msgBox.ForeColor = [Color]::White
    $msgBox.Font = $script:regularFont
    $msgBox.FormBorderStyle = [FormBorderStyle]::FixedDialog
    $msgBox.MaximizeBox = $false
    $msgBox.MinimizeBox = $false
    $msgBox.StartPosition = [FormStartPosition]::CenterParent
    $msgBox.Text = "INFO: $($script:title)"
    $msgBox.AutoSize = $false
    $msgLabel = New-Object Label
    $msgLabel.Text = $msg
    $msgLabel.Font = $script:regularFont
    $msgLabel.Location = '15, 15'
    $msgLabel.MaximumSize = '860, 0'
    $msgLabel.AutoSize = $true
    $okButton = New-Object Button
    $okButton.Text = 'OK'
    $okButton.DialogResult = [DialogResult]::OK
    $okButton.Font = $script:boldFont
    $okButton.BackColor = [ColorTranslator]::FromHtml('#3E3E42')
    $okButton.ForeColor = [Color]::White
    $okButton.Cursor = [Cursors]::Hand
    $okButton.FlatStyle = [FlatStyle]::Flat
    $okButton.FlatAppearance.BorderSize = 0
    $okButton.Size = '100, 30'
    $msgBox.Controls.AddRange(($msgLabel, $okButton))
    $msgBox.Add_Shown({
        $newWidth = [math]::Max(300, $msgLabel.Right + 30)
        $newHeight = $msgLabel.Bottom + $okButton.Height + 30
        $msgBox.ClientSize = New-Object Size([int]$newWidth, [int]$newHeight)
        $okButton.Location = New-Object Point([int](($msgBox.ClientSize.Width - $okButton.Width) / 2), [int]($msgLabel.Bottom + 10))
    })
    
    $parentForm = if ($parent) { $parent } else { $script:f }
    if ($parentForm) {
        [void]$msgBox.ShowDialog($parentForm)
    } else {
        [void]$msgBox.ShowDialog()
    }
}
function YECustomConfirmMsg {
    param($msg, $title, $parent, $btnYesText="Да", $btnNoText="Нет")
    $msgBox = New-Object Form
    $msgBox.BackColor = [ColorTranslator]::FromHtml('#2D2D30')
    $msgBox.ForeColor = [Color]::White
    $msgBox.Font = $script:regularFont
    $msgBox.FormBorderStyle = [FormBorderStyle]::None
    $msgBox.StartPosition = [FormStartPosition]::CenterParent
    $msgBox.Text = $title
    $msgBox.AutoSize = $false
    
    $msgBox.Add_Paint({
        $pen = New-Object System.Drawing.Pen([ColorTranslator]::FromHtml('#007ACC'), 1)
        $_.Graphics.DrawRectangle($pen, 0, 0, ($msgBox.Width - 1), ($msgBox.Height - 1))
        $pen.Dispose()
    })
    
    $titleBar = New-Object Panel
    $titleBar.BackColor = [ColorTranslator]::FromHtml('#252526')
    $titleBar.Height = 30
    $titleBar.Dock = [DockStyle]::Top
    $titleBar.Add_Paint({
        param($s, $e)
        $rect = $s.ClientRectangle
        $colorStart = [Color]::FromArgb(255, 0, 35, 60)
        $colorEnd = [ColorTranslator]::FromHtml('#252526')
        $brush = New-Object LinearGradientBrush($rect, $colorStart, $colorEnd, [LinearGradientMode]::Horizontal)
        $e.Graphics.FillRectangle($brush, $rect)
        $brush.Dispose()
    })
    
    $closeBtn = New-Object Button
    $closeBtn.Text = "X"
    $closeBtn.FlatStyle = [FlatStyle]::Flat
    $closeBtn.FlatAppearance.BorderSize = 0
    $closeBtn.ForeColor = [Color]::White
    $closeBtn.BackColor = [Color]::Transparent
    $closeBtn.Size = '40, 30'
    $closeBtn.Dock = [DockStyle]::Right
    $closeBtn.Add_Click({ $msgBox.DialogResult = [DialogResult]::Cancel; $msgBox.Close() })
    $closeBtn.Add_MouseEnter({ $closeBtn.BackColor = [ColorTranslator]::FromHtml('#E81123') })
    $closeBtn.Add_MouseLeave({ $closeBtn.BackColor = [Color]::Transparent })
    
    $titleLbl = New-Object Label
    $titleLbl.Text = $title
    $titleLbl.ForeColor = [Color]::White
    $titleLbl.Font = $script:boldFont
    $titleLbl.AutoSize = $false
    $titleLbl.Dock = [DockStyle]::Fill
    $titleLbl.TextAlign = [ContentAlignment]::MiddleCenter
    
    $titleBar.Controls.Add($closeBtn)
    $titleBar.Controls.Add($titleLbl)
    
    $script:dragging = $false
    $script:dragCursorPoint = [Point]::Empty
    $script:dragFormPoint = [Point]::Empty
    
    $dragDown = {
        if ($_.Button -eq [MouseButtons]::Left) {
            $script:dragging = $true
            $script:dragCursorPoint = [System.Windows.Forms.Cursor]::Position
            $script:dragFormPoint = $msgBox.Location
        }
    }
    $dragMove = {
        if ($script:dragging) {
            $diff = [Point]::new([System.Windows.Forms.Cursor]::Position.X - $script:dragCursorPoint.X, [System.Windows.Forms.Cursor]::Position.Y - $script:dragCursorPoint.Y)
            $msgBox.Location = [Point]::new($script:dragFormPoint.X + $diff.X, $script:dragFormPoint.Y + $diff.Y)
        }
    }
    $dragUp = { $script:dragging = $false }
    
    $titleBar.Add_MouseDown($dragDown)
    $titleBar.Add_MouseMove($dragMove)
    $titleBar.Add_MouseUp($dragUp)
    
    $titleLbl.Add_MouseDown($dragDown)
    $titleLbl.Add_MouseMove($dragMove)
    $titleLbl.Add_MouseUp($dragUp)
    
    $msgLabel = New-Object Label
    $msgLabel.Text = $msg
    $msgLabel.Font = $script:regularFont
    $msgLabel.Location = '15, 45'
    $msgLabel.MaximumSize = '860, 0'
    $msgLabel.AutoSize = $true
    
    $btnYes = New-Object Button
    $btnYes.Text = $btnYesText
    $btnYes.DialogResult = [DialogResult]::Yes
    $btnYes.Font = $script:boldFont
    $btnYes.BackColor = [ColorTranslator]::FromHtml('#3E3E42')
    $btnYes.ForeColor = [Color]::White
    $btnYes.Cursor = [Cursors]::Hand
    $btnYes.FlatStyle = [FlatStyle]::Flat
    $btnYes.FlatAppearance.BorderSize = 0
    $btnYes.Size = '140, 30'
    
    $btnNo = New-Object Button
    $btnNo.Text = $btnNoText
    $btnNo.DialogResult = [DialogResult]::No
    $btnNo.Font = $script:boldFont
    $btnNo.BackColor = [ColorTranslator]::FromHtml('#3E3E42')
    $btnNo.ForeColor = [Color]::White
    $btnNo.Cursor = [Cursors]::Hand
    $btnNo.FlatStyle = [FlatStyle]::Flat
    $btnNo.FlatAppearance.BorderSize = 0
    $btnNo.Size = '140, 30'
    
    $msgBox.Controls.AddRange(($titleBar, $msgLabel, $btnYes, $btnNo))
    
    $msgBox.Add_Shown({
        $newWidth = [math]::Max(360, $msgLabel.Right + 30)
        $newHeight = $msgLabel.Bottom + $btnYes.Height + 40
        $msgBox.ClientSize = New-Object Size([int]$newWidth, [int]$newHeight)
        
        $totalBtnWidth = $btnYes.Width + $btnNo.Width + 20
        $startX = [int](($msgBox.ClientSize.Width - $totalBtnWidth) / 2)
        $btnY = [int]($msgLabel.Bottom + 20)
        
        $btnYes.Location = New-Object Point($startX, $btnY)
        $btnNo.Location = New-Object Point([int]($startX + $btnYes.Width + 20), $btnY)
    })
    
    $parentForm = if ($parent) { $parent } else { $script:f }
    if ($parentForm) {
        $result = $msgBox.ShowDialog($parentForm)
    } else {
        $result = $msgBox.ShowDialog()
    }
    return $result.ToString()
}
function YEmsg {
    param($msg, $btns = 'OK', $parent = $null)
    if ($btns -ne 'OK') {
        return [MessageBox]::Show($msg, $title, $btns, 'Info')
    } else {
        YECustomMsg $msg $parent
    }
}
function Update-MultiGameUIState {
    param($listBox)
    $tabName = ($listBox.Name -split '_')[1]
    if ($tabName -notin 'Update', 'Multi') {
        return
    }
    $outputConf = $script:outputControls[$tabName]
    if (-not $outputConf) { return }
    
    # 1. Если список пуст
    if ($listBox.Items.Count -eq 0) {
        # Если поле было заблокировано (после мульти-режима), разблокируем и возвращаем цвет темы
        if ($outputConf.TxtOutFile.ReadOnly) {
            $outputConf.TxtOutFile.ReadOnly = $false
            $outputConf.TxtOutFile.BackColor = [ColorTranslator]::FromHtml('#333333')
            $outputConf.TxtOutFile.Text = ""
        }
        return
    }
    
    # Проверка: есть ли в списке элементы, указывающие на мульти-режим (индекс #...)
    $isMultiGame = $false
    foreach ($item in $listBox.Items) {
        if ($item.PSObject.Properties['DisplayString'] -and $item.DisplayString -like '*#*') {
            $isMultiGame = $true
            break
        }
    }
    
    if ($isMultiGame) {
        # 2. Режим МУЛЬТИ: Блокируем поле и ставим светло-серый цвет индикации
        if (-not $outputConf.TxtOutFile.ReadOnly) {
            $outputConf.TxtOutFile.Text = "(Автоматическое имя для каждой игры)"
            $outputConf.TxtOutFile.ReadOnly = $true
            $outputConf.TxtOutFile.BackColor = [ColorTranslator]::FromHtml('#555555')
            Write-Log "Обнаружено несколько игр. Поле имени выходного файла заблокировано для автоматического именования."
        }
    } else {
        # 3. Режим ОДИНОЧНЫЙ: Разблокируем и возвращаем стандартный тёмный фон (#333333)
        if ($outputConf.TxtOutFile.ReadOnly) {
            $outputConf.TxtOutFile.ReadOnly = $false
            $outputConf.TxtOutFile.BackColor = [ColorTranslator]::FromHtml('#333333')
            if ($outputConf.TxtOutFile.Text -eq "(Автоматическое имя для каждой игры)") {
                $outputConf.TxtOutFile.Text = ""
            }
        }
    }
}
# ═══════════════════════════════════════════════════════════════════
# ЕДИНАЯ функция получения TitleID через nsz --info
# (Используется в YEDDHandler_Advanced и YEDDHandler_Standard)
# ═══════════════════════════════════════════════════════════════════
function Get-TitleIdFromNsz {
    param([string]$FilePath)
    if (-not (Test-Path -LiteralPath $FilePath)) { return $null }
    if (-not (Test-Path -LiteralPath $script:nsz_exe)) { return $null }
    try {
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $startInfo.FileName = $script:nsz_exe
        $startInfo.Arguments = "--info -p `"$FilePath`""
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $startInfo.StandardOutputEncoding = [System.Text.Encoding]::UTF8
        $process = [System.Diagnostics.Process]::Start($startInfo)
        $output = $process.StandardOutput.ReadToEnd()
        $err = $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        
        # Объединяем вывод (иногда инфо падает в stderr в старых версиях nsz)
        $fullOutput = $output + "`n" + $err

        $idRegex = '(?i)(?:Title\s*ID:|titleId\s*=?)\s*([0-9A-Fa-f]{16})'
        $allMatches = [regex]::Matches($fullOutput, $idRegex)
        
        $titleIds = @()
        foreach ($m in $allMatches) { 
            $val = $m.Groups[1].Value.ToUpper()
            if ($val -notin $titleIds) { $titleIds += $val }
        }
        
        if ($titleIds.Count -eq 0) { return $null }
        if ($titleIds.Count -eq 1) { return $titleIds[0] }
        
        # Приоритет: Update (800) → DLC (not 000/800) → Base (000)
        $updateId = $titleIds | Where-Object { $_.EndsWith('800') } | Select-Object -First 1
        if ($updateId) { return $updateId }
        
        $dlcId = $titleIds | Where-Object { -not $_.EndsWith('000') -and -not $_.EndsWith('800') } | Sort-Object -Descending | Select-Object -First 1
        if ($dlcId) { return $dlcId }

        $baseId = $titleIds | Where-Object { $_.EndsWith('000') } | Select-Object -First 1
        if ($baseId) { return $baseId }
        
        return $titleIds[0]
    } catch {
        Write-Log "Ошибка получения TitleID из '$([System.IO.Path]::GetFileName($FilePath))': $($_.Exception.Message)" -Type 'WARN'
        return $null
    }
}

function YEDDHandler_Advanced {
    param($listBox, $unpackMode = $false)
    $existingItems = @($listBox.Items)
    $newFoundItems = [System.Collections.Generic.List[FileSystemInfo]]::new()

    try {
        $allDroppedPaths = $_.Data.GetData('FileDrop')
        if (-not $allDroppedPaths) { return }

        $tabName = ($listBox.Name -split '_')[1]
        $isRestrictedMode = ($unpackMode -or $tabName -eq 'Update')

        foreach ($rawPathInput in $allDroppedPaths) {
            try {
                $rawPath = $rawPathInput.Trim()
                $safePath = $rawPath.Replace('/', '\')
                if ($safePath.Length -ge 240 -or $safePath -match '^[a-zA-Z]:' -or $safePath.StartsWith("\\")) {
                    if (-not $safePath.StartsWith("\\?\")) {
                        if ($safePath.StartsWith("\\")) { $safePath = "\\?\UNC\" + $safePath.Substring(2) } 
                        else { $safePath = "\\?\" + $safePath }
                    }
                }

                $item = $null
                $isFolder = $false
                
                try {
                    $item = Get-Item -LiteralPath $safePath -ErrorAction Stop
                    $isFolder = $item.PSIsContainer
                } catch {
                    Write-Log "Не удалось получить элемент: '$safePath'. $($_.Exception.Message)" -Type 'WARN'
                }

                if (-not $item) { continue }

                # --- Внутренняя функция: распаковка архива ---
                $Local:ExtractArchive = {
                    param($archiveItem, [ref]$foundItems)
                    $cleanPath = $archiveItem.FullName -replace '^\\\\\?\\', ''
                    $parentDir = Split-Path $cleanPath -Parent
                    $extractDir = Join-Path $parentDir $archiveItem.BaseName
                    Write-Log "Обнаружен архив: $($archiveItem.Name). Автоматическая распаковка..." -Type 'INFO'
                    try {
                        # Ищем 7-Zip (работает со всеми форматами и путями)
                        $sevenZipPath = Join-Path $env:ProgramFiles '7-Zip\7z.exe'
                        if (-not (Test-Path -LiteralPath $sevenZipPath)) {
                            $sevenZipPath = Join-Path ${env:ProgramFiles(x86)} '7-Zip\7z.exe'
                        }
                        
                        $winRarPath = Join-Path $env:ProgramFiles 'WinRAR\WinRAR.exe'
                        if (-not (Test-Path -LiteralPath $winRarPath)) {
                            $winRarPath = Join-Path ${env:ProgramFiles(x86)} 'WinRAR\WinRAR.exe'
                        }

                        if (Test-Path -LiteralPath $sevenZipPath) {
                            $psi = New-Object System.Diagnostics.ProcessStartInfo
                            $psi.FileName = $sevenZipPath
                            $psi.Arguments = "x `"$cleanPath`" -o`"$extractDir`" -aoa -y"
                            $psi.UseShellExecute = $false; $psi.CreateNoWindow = $true
                            $proc = [System.Diagnostics.Process]::Start($psi)
                            $proc.WaitForExit()
                            if ($proc.ExitCode -gt 1) { throw "7-Zip завершился с кодом $($proc.ExitCode)" }
                        } elseif (Test-Path -LiteralPath $winRarPath) {
                            $psi = New-Object System.Diagnostics.ProcessStartInfo
                            $psi.FileName = $winRarPath
                            $psi.Arguments = "x -y -ibck -inul `"$cleanPath`" `"$extractDir\\`""
                            $psi.UseShellExecute = $false; $psi.CreateNoWindow = $true
                            $proc = [System.Diagnostics.Process]::Start($psi)
                            $proc.WaitForExit()
                            if ($proc.ExitCode -gt 1) { throw "WinRAR завершился с кодом $($proc.ExitCode)" }
                        } elseif ($archiveItem.Extension.ToLower() -eq '.zip') {
                            # Фоллбэк: .NET ZipFile для .zip (без зависимости от 7-Zip)
                            Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue
                            $zip = [System.IO.Compression.ZipFile]::OpenRead($cleanPath)
                            try {
                                foreach ($entry in $zip.Entries) {
                                    if ($entry.FullName.EndsWith('/')) { continue }
                                    $destFile = [System.IO.Path]::Combine($extractDir, $entry.FullName)
                                    $destFileDir = [System.IO.Path]::GetDirectoryName($destFile)
                                    if (-not [System.IO.Directory]::Exists($destFileDir)) {
                                        [void][System.IO.Directory]::CreateDirectory($destFileDir)
                                    }
                                    [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destFile, $true)
                                }
                            } finally { $zip.Dispose() }
                        } else {
                            Write-Log "7-Zip или WinRAR не найдены. Установите 7-Zip или WinRAR для поддержки $($archiveItem.Extension) архивов." -Type 'ERROR'
                            return
                        }
                        # Не добавляем ничего в список — последующее сканирование romfs/exefs найдёт всё
                        Write-Log "Архив '$($archiveItem.Name)' успешно распакован в '$extractDir'" -Type 'SUCCESS'
                    } catch {
                        Write-Log "Ошибка распаковки архива '$($archiveItem.Name)': $($_.Exception.Message)" -Type 'ERROR'
                    }
                }

                # --- Внутренняя функция: распаковка romfs.bin ---
                $Local:ExtractRomfsBin = {
                    param($romfsItem, [ref]$foundItems)
                    Write-Log "Обнаружен romfs.bin: $($romfsItem.FullName). Автоматическая распаковка..." -Type 'INFO'
                    if (-not (Test-Path -LiteralPath $script:hactoolnet)) {
                        Write-Log "hactoolnet.exe не найден. Невозможно распаковать romfs.bin." -Type 'ERROR'
                        return
                    }
                    $cleanPath = $romfsItem.FullName -replace '^\\\\\?\\', ''
                    $parentDir = Split-Path $cleanPath -Parent
                    $extractDir = Join-Path $parentDir $romfsItem.BaseName
                    try {
                        if ([System.IO.Directory]::Exists($extractDir)) {
                            [System.IO.Directory]::Delete($extractDir, $true)
                        }
                        [void][System.IO.Directory]::CreateDirectory($extractDir)
                        $hactoolArgs = "-k `"$script:key`" -t romfs `"$($romfsItem.FullName)`" --romfsdir `"$extractDir`""
                        $psi = New-Object System.Diagnostics.ProcessStartInfo
                        $psi.FileName = $script:hactoolnet
                        $psi.Arguments = $hactoolArgs
                        $psi.UseShellExecute = $false; $psi.CreateNoWindow = $true
                        $proc = [System.Diagnostics.Process]::Start($psi)
                        $proc.WaitForExit()
                        if ($proc.ExitCode -ne 0) { throw "hactoolnet завершился с кодом $($proc.ExitCode)" }
                        # Не добавляем в список — последующее сканирование romfs/exefs найдёт папку
                        Write-Log "romfs.bin успешно распакован в '$extractDir'" -Type 'SUCCESS'
                    } catch {
                        Write-Log "Ошибка распаковки romfs.bin: $($_.Exception.Message)" -Type 'ERROR'
                    }
                }

                if ($isFolder) {
                    # Сначала распаковываем архивы и romfs.bin, чтобы их содержимое было найдено при последующем сканировании
                    if (-not $isRestrictedMode) {
                        # ОПТИМИЗАЦИЯ: Один проход вместо 3-4 отдельных рекурсивных сканирований
                        $allChildren = Get-ChildItem -LiteralPath $item.FullName -Recurse -Force -ErrorAction SilentlyContinue
                        # Фаза 1: Распаковка архивов и romfs.bin
                        foreach ($child in $allChildren) {
                            if ($child -is [System.IO.FileInfo]) {
                                if ($child.Extension -match '(?i)^\.(zip|7z|rar)$') {
                                    & $Local:ExtractArchive $child ([ref]$newFoundItems)
                                } elseif ($child.Name -eq 'romfs.bin') {
                                    & $Local:ExtractRomfsBin $child ([ref]$newFoundItems)
                                }
                            }
                        }
                        # Фаза 2: Повторное сканирование (архивы могли создать новые файлы/папки)
                        Get-ChildItem -LiteralPath $item.FullName -Recurse -Force -ErrorAction SilentlyContinue | ForEach-Object {
                            if ($_ -is [System.IO.FileInfo] -and $_.Name -match '\.(nsp|nsz|xci|xcz)$') { $newFoundItems.Add($_) }
                            elseif ($_ -is [System.IO.DirectoryInfo] -and $_.Name -in 'romfs', 'exefs') { $newFoundItems.Add($_) }
                        }
                    } else {
                        # Restricted: один проход, только игровые файлы
                        Get-ChildItem -LiteralPath $item.FullName -Recurse -File -Force -ErrorAction SilentlyContinue |
                            Where-Object { $_.Name -match '\.(nsp|nsz|xci|xcz)$' } | ForEach-Object { $newFoundItems.Add($_) }
                    }
                } else {
                    if ($item.Name -match '\.(nsp|nsz|xci|xcz)$') {
                        $newFoundItems.Add($item)
                    }
                    elseif (-not $isRestrictedMode -and $item.Extension -match '(?i)^\.(zip|7z|rar)$') {
                        & $Local:ExtractArchive $item ([ref]$newFoundItems)
                    }
                    elseif (-not $isRestrictedMode -and $item.Name -eq 'romfs.bin') {
                        & $Local:ExtractRomfsBin $item ([ref]$newFoundItems)
                    }
                }
            } catch {
                Write-Log "Ошибка обработки '$rawPathInput': $($_.Exception.Message)" -Type 'WARN'
            }
        }
        
        # --- ФИЛЬТРАЦИЯ И ОПРЕДЕЛЕНИЕ ID ---
        $finalItemsToAdd = [System.Collections.Generic.List[object]]::new()
        $existingPaths = $existingItems | ForEach-Object { if($_.PSObject.Properties['Item']){$_.Item.FullName}else{$_.FullName} }

        foreach ($f in $newFoundItems) {
            if ($f.FullName -in $existingPaths) { continue }
            
            # Для папок romfs/exefs (только если не restricted)
            if ($f -is [System.IO.DirectoryInfo]) {
                if (-not $isRestrictedMode) { $finalItemsToAdd.Add($f); Write-Log "Добавлена папка мода: $($f.Name)" -Type 'SUCCESS' }
                continue
            }

            # Для файлов определяем ID
            $tId = $null
            # 1. Читаем ID напрямую из файла, чтобы избежать ошибок с неверными тегами в именах
            Write-Log "Анализ ID файла: $($f.Name)..." -Type 'DEBUG'
            $tId = Get-TitleIdFromNsz -FilePath $f.FullName

            # 2. Быстрый поиск в имени (только если nsz не смог определить)
            if (-not $tId -and $f.Name -match '\[([0-9a-fA-F]{16})\]') {
                $tId = $matches[1].ToUpper()
                Write-Log "ID получен из имени файла (fallback): $tId" -Type 'DEBUG'
            }

            if ($tId) {
                # Для группировки конвертируем Update ID (800) в Base ID (000)
                $groupingTid = if ($tId.EndsWith("800")) { 
                    $tId.Substring(0, 13) + "000" 
                } else { 
                    $tId 
                }
                $f | Add-Member -MemberType NoteProperty -Name 'CachedTitleID' -Value $groupingTid -Force
                
                # Определяем тип по ОРИГИНАЛЬНОМУ ID
                $type = "DLC/OTHER"
                if ($tId.EndsWith("000")) { $type = "ИГРА" }
                elseif ($tId.EndsWith("800")) { $type = "ОБНОВЛЕНИЕ" }
                if ($f.Name -match 'Unlocker') { $type = "UNLOCKER" }

                if ($isRestrictedMode) {
                    if ($type -in "ИГРА", "ОБНОВЛЕНИЕ") {
                        $finalItemsToAdd.Add($f)
                        Write-Log "Добавлен: $($f.Name) [ID: $tId -> $type]" -Type 'SUCCESS'
                    } else {
                        Write-Log "ПРОПУЩЕНО: $($f.Name) [ID: $tId]. Это $type. В данном режиме разрешены только ИГРА и ОБНОВЛЕНИЕ." -Type 'WARN'
                    }
                } else {
                    $finalItemsToAdd.Add($f)
                    Write-Log "Добавлен в мульти-контент: $($f.Name) [ID: $tId -> $type]" -Type 'SUCCESS'
                }
            } else {
                Write-Log "ОШИБКА: Не удалось определить ID для $($f.Name). Файл пропущен." -Type 'ERROR'
            }
        }

        $allItemsToProcess = @($existingItems.ForEach({if($_.PSObject.Properties['Item']){$_.Item}else{$_}})) + @($finalItemsToAdd)
        Rebuild-ListBoxItems -ListBox $listBox -AllItems $allItemsToProcess -UnpackMode $unpackMode

    } catch {
        Write-Log "Критическая ошибка Drag&Drop Handler: $($_.Exception.Message)" -Type 'ERROR'
    }
}

function YEDDHandler_Standard {
    $listBox = $this
    $tempItems = [System.Collections.Generic.List[object]]::new()

    # Используем единую функцию Get-TitleIdFromNsz (определена на уровне скрипта)

    $listBox.BeginUpdate()
    try {
        $paths = $_.Data.GetData('FileDrop')
        if (-not $paths) { return }

        # --- ИСПРАВЛЕНИЕ ОПРЕДЕЛЕНИЯ ВКЛАДОК ---
        # Ранее "*Pack*" совпадало с "Unpack", из-за чего Распаковка работала как Упаковка.
        # Теперь используем точные имена или исключения.
        
        $lbName = $listBox.Name
        # Упаковка: только конкретный ListBox
        $isPackTab = ($lbName -eq "listBox_Pack_0") 
        # Распаковка: содержит Unpack
        $isUnpackTab = ($lbName -like "*Unpack*")
        # Обновление: содержит Update
        $isUpdateTab = ($lbName -like "*Update*")

        foreach ($rawPath in $paths) {
            $path = $rawPath.Trim().Replace('/', '\')
            # Обработка длинных путей
            if ($path.Length -ge 240 -and -not $path.StartsWith("\\?\")) {
                $path = if ($path.StartsWith("\\")) { "\\?\UNC\" + $path.Substring(2) } else { "\\?\" + $path }
            }

            $item = Get-Item -LiteralPath $path -ErrorAction SilentlyContinue
            if (-not $item) { continue }

            # =========================================================================
            # ЛОГИКА ДЛЯ УПАКОВКИ (PACK) - СТРОГО ПАПКИ С basedata/romfs
            # =========================================================================
            if ($isPackTab) {
                if ($item.PSIsContainer) {
                    $hasBasedata = Test-Path -LiteralPath (Join-Path $item.FullName "basedata")
                    $hasRomfs = Test-Path -LiteralPath (Join-Path $item.FullName "romfs")

                    if ($hasBasedata -and $hasRomfs) {
                        $isDup = $false
                        foreach ($ex in $listBox.Items) { if ($ex.FullName -eq $item.FullName) { $isDup = $true; break } }
                        
                        if (-not $isDup) {
                            $listBox.Items.Add($item)
                            Write-Log "Добавлена папка для упаковки: $($item.Name)" -Type 'SUCCESS'
                        } else {
                            Write-Log "Папка уже в списке: $($item.Name)" -Type 'INFO'
                        }
                    } else {
                        Write-Log "ОТКЛОНЕНО: '$($item.Name)'. Для Упаковки требуются папки 'basedata' и 'romfs' внутри." -Type 'ERROR'
                    }
                } else {
                    Write-Log "ПРОПУЩЕНО: '$($item.Name)'. В Упаковку можно добавлять только ПАПКИ." -Type 'WARN'
                }
                continue 
            }

            # =========================================================================
            # ЛОГИКА ДЛЯ ОБНОВЛЕНИЯ / РАСПАКОВКИ - ФАЙЛЫ + ID
            # =========================================================================
            
            $filesFound = @()
            if ($item.PSIsContainer) {
                $filesFound = Get-ChildItem -LiteralPath $item.FullName -Recurse -File -Force -ErrorAction SilentlyContinue |
                              Where-Object { $_.Extension.ToLower() -in '.nsp', '.nsz', '.xci', '.xcz' }
            } elseif ($item.Extension.ToLower() -in '.nsp', '.nsz', '.xci', '.xcz') {
                $filesFound = @($item)
            }

            foreach ($file in $filesFound) {
                $isDuplicate = $false
                foreach ($existing in $listBox.Items) {
                    if ($existing -is [System.IO.FileInfo] -and $existing.FullName -eq $file.FullName) { $isDuplicate = $true; break }
                    if ($existing.PSObject.Properties['Item'] -and $existing.Item.FullName -eq $file.FullName) { $isDuplicate = $true; break }
                }
                if ($isDuplicate) { continue }

                # Определение ID - сначала из имени файла для типа, потом из nsz для группировки
                $originalTid = $null
                $groupingTid = $null
                
                # 1. Пытаемся извлечь оригинальный ID из имени файла
                if ($file.Name -match '\[([0-9a-fA-F]{16})\]') {
                    $originalTid = $matches[1].ToUpper()
                }
                
                # 2. Если не нашли в имени, используем nsz
                if (-not $originalTid) {
                    $originalTid = Get-TitleIdFromNsz -FilePath $file.FullName
                }
                
                if ($originalTid) {
                    # Определяем тип по ОРИГИНАЛЬНОМУ ID
                    $fileType = "ДРУГОЕ"
                    $isAccepted = $false

                    if ($originalTid.EndsWith("000")) { 
                        $fileType = "ИГРА"
                        $isAccepted = $true
                    } elseif ($originalTid.EndsWith("800")) { 
                        $fileType = "ОБНОВЛЕНИЕ"
                        $isAccepted = $true
                    } else {
                        $fileType = "DLC/SYS"
                        # Для Unpack/Update разрешаем только Игры и Обновления
                        $isAccepted = $false
                    }

                    if ($isAccepted) {
                        # Для группировки используем базовый ID (xxx000)
                        $groupingTid = if ($originalTid.EndsWith("800")) { 
                            $originalTid.Substring(0, 13) + "000" 
                        } else { 
                            $originalTid 
                        }
                        $file | Add-Member -MemberType NoteProperty -Name 'CachedTitleID' -Value $groupingTid -Force
                        
                        $listBox.Items.Add($file)
                        $tempItems.Add($file)
                        Write-Log "Добавлен: $($file.Name) [Title ID: $originalTid → $fileType]" -Type 'SUCCESS'
                    } else {
                        Write-Log "ОТКЛОНЕНО: $($file.Name) [Title ID: $originalTid → $fileType]. Разрешены только ИГРА и ОБНОВЛЕНИЕ." -Type 'WARN'
                    }
                } else {
                    Write-Log "ОШИБКА: Не удалось получить Title ID из $($file.Name). Файл может быть поврежден." -Type 'ERROR'
                }
            }
        }

        if ($tempItems.Count -gt 0 -and (-not $isPackTab)) {
            $allItems = [System.Collections.Generic.List[object]]::new()
            foreach ($i in $listBox.Items) {
                if ($i -is [System.IO.FileInfo]) { $allItems.Add($i) }
                elseif ($i.PSObject.Properties['Item']) { $allItems.Add($i.Item) }
            }
            Rebuild-ListBoxItems -ListBox $listBox -AllItems $allItems -UnpackMode $isUnpackTab
        }
    } catch {
        Write-Log "Ошибка Handler: $($_.Exception.Message)" -Type 'ERROR'
    } finally {
        $listBox.EndUpdate()
        $this.BackColor = $script:listBoxOriginalBackColor
    }
}

function Rebuild-ListBoxItems {
    param($ListBox, $AllItems, $UnpackMode = $false)
    if ($null -eq $ListBox -or $ListBox.IsDisposed) { return }
    $listBox.BeginUpdate()
    try {
        $tabName = ($ListBox.Name -split '_')[1]
        $isUpdateTab = ($tabName -eq 'Update')
        $isRestrictedMode = ($UnpackMode -or $isUpdateTab) 

        $uniquePaths = New-Object 'System.Collections.Generic.HashSet[string]'
        $allGameFiles = [System.Collections.Generic.List[System.IO.FileInfo]]::new()
        $allModFolders = [System.Collections.Generic.List[System.IO.DirectoryInfo]]::new()

        foreach ($item in $AllItems) {
            if (-not $item) { continue }
            try {
                if ($item -is [string] -or $item.Type -eq 'SEPARATOR') { continue }
                if ($uniquePaths.Add($item.FullName)) {
                    if ($item -is [System.IO.FileInfo]) { $allGameFiles.Add($item) }
                    elseif ($item -is [System.IO.DirectoryInfo]) { $allModFolders.Add($item) }
                }
            } catch {
                Write-Log "Ошибка обработки элемента списка: $($_.Exception.Message)" -Type 'WARN'
            }
        }
        
        $idRegex = '\[([0-9a-fA-F]{16})\]'
        
        # Группировка
        $grouped = @{}
        foreach ($file in $allGameFiles) {
            $tid = $null
            if ($file.PSObject.Properties['CachedTitleID']) { $tid = $file.CachedTitleID }
            elseif ($file.Name -match $idRegex) { $tid = $matches[1].ToUpper() }
            
            # Для сшитых файлов: если TID заканчивается на 800 (обновление), конвертируем в базовый (000)
            if ($tid -and $tid.EndsWith('800')) {
                $tid = $tid.Substring(0, 13) + '000'
            }
            
            $groupId = if ($tid) { $tid.Substring(0, 12) } else { [guid]::NewGuid().ToString() }
            
            if (-not $grouped.ContainsKey($groupId)) { $grouped[$groupId] = [System.Collections.Generic.List[object]]::new() }
            $grouped[$groupId].Add($file)
        }

        if ($grouped.Count -eq 0) { $listBox.Items.Clear(); Update-ListBoxLabel $ListBox; return }
        
        $listBox.Items.Clear()
        $gameIndex = 1
        $multipleGames = $grouped.Count -gt 1

        foreach ($key in $grouped.Keys) {
            $groupFiles = $grouped[$key]
            $base = $null; $update = $null; $dlcs = @(); $unlockers = @()
            
            foreach ($file in $groupFiles) {
                $tid = if ($file.PSObject.Properties['CachedTitleID']) { $file.CachedTitleID } 
                       elseif ($file.Name -match $idRegex) { $matches[1].ToUpper() } else { $null }
                
                $isBase = $false; $isUpd = $false
                
                # ПРОВЕРКА СШИТОГО ФАЙЛА: если в имени есть (1G+...) - это базовая игра со вшитым контентом
                $isStitchedWithBase = $file.Name -match '\(1G\+'
                
                if ($isStitchedWithBase) {
                    # Сшитый файл с базовой игрой - всегда считаем как GAME
                    $isBase = $true
                }
                elseif ($tid) {
                    if ($tid.EndsWith('000')) { $isBase = $true }
                    elseif ($tid.EndsWith('800')) { $isUpd = $true }
                }
                
                # Check ver 0 override - но НЕ для сшитых файлов!
                if ($isBase -and -not $isStitchedWithBase) {
                    if ($file.Name -match '\[v(\d+)\]') {
                        if ([int64]$matches[1] -gt 0) { $isBase = $false; $isUpd = $true }
                    }
                }

                if ($isBase) { 
                    if (-not $base -or $file.Length -gt $base.Length) { $base = $file } 
                    else { if (-not $isRestrictedMode) { $dlcs += $file } }
                }
                elseif ($isUpd) { 
                    if (-not $update -or $file.Name -gt $update.Name) { $update = $file }
                    else { Write-Log "Пропущено устаревшее обновление: $($file.Name) (используется более новое)" -Type 'DEBUG' }
                }
                else {
                    if ($file.Name -match 'Unlocker') { $unlockers += $file } else { $dlcs += $file }
                }
            }

            if ($isRestrictedMode -and -not $base -and -not $update) { continue }
            
            # Fallback logic: if no base found but update exists, try to treat update as base if name is ambiguous (rare)
            # Actually for Unpack/Update logic we strictly need GAME and UPDATE
            
            if ($gameIndex -gt 1) {
                 $listBox.Items.Add([PSCustomObject]@{ Item = '---'; Type = 'SEPARATOR'; DisplayString = ('─' * 70); GameGroupKey = [guid]::NewGuid().ToString() })
            }
            
            $suffix = if ($multipleGames) { " #$gameIndex" } else { "" }
            $gameGroupKey = $key

            if ($base) {
                $listBox.Items.Add([PSCustomObject]@{ Item = $base; Type = 'GAME'; DisplayString = "[ИГРА$suffix] $($base.Name)"; GameGroupKey = $gameGroupKey })
            }
            if ($update) {
                $listBox.Items.Add([PSCustomObject]@{ Item = $update; Type = 'UPDATE'; DisplayString = "[ОБНОВЛЕНИЕ$suffix] $($update.Name)"; GameGroupKey = $gameGroupKey })
            }
            
            if (-not $isRestrictedMode) {
                foreach ($u in $unlockers) {
                    $listBox.Items.Add([PSCustomObject]@{ Item = $u; Type = 'UNLOCKER'; DisplayString = "[UNLOCKER$suffix] $($u.Name)"; GameGroupKey = $gameGroupKey })
                }
                foreach ($d in $dlcs) {
                    $listBox.Items.Add([PSCustomObject]@{ Item = $d; Type = 'DLC'; DisplayString = "[DLC$suffix] $($d.Name)"; GameGroupKey = $gameGroupKey })
                }
                # Mods — привязка по пути ИЛИ к первой группе (поддержка разных дисков)
                if ($base) {
                    $cleanBaseDir = ($base.DirectoryName -replace '^\\\\\?\\', '').TrimEnd('\') + '\'
                    $mods = $allModFolders | Where-Object { ($_.FullName -replace '^\\\\\?\\', '').StartsWith($cleanBaseDir, [System.StringComparison]::OrdinalIgnoreCase) }
                    foreach ($m in $mods) {
                         $listBox.Items.Add([PSCustomObject]@{ Item = $m; Type = $m.Name.ToUpper(); DisplayString = "[$($m.Name.ToUpper())$suffix] $($m.FullName)"; GameGroupKey = $gameGroupKey })
                    }
                    # Привязка «сирот»: моды с других дисков, не привязанные ни к одной группе
                    if ($gameIndex -eq 1) {
                        $assignedPaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
                        foreach ($m in $mods) { [void]$assignedPaths.Add($m.FullName) }
                        $orphanMods = $allModFolders | Where-Object { -not $assignedPaths.Contains($_.FullName) }
                        foreach ($m in $orphanMods) {
                            $listBox.Items.Add([PSCustomObject]@{ Item = $m; Type = $m.Name.ToUpper(); DisplayString = "[$($m.Name.ToUpper())$suffix] $($m.FullName) (внеш.)"; GameGroupKey = $gameGroupKey })
                            Write-Log "Мод '$($m.Name)' привязан к игре '$($base.Name)' (с другого диска)" -Type 'SUCCESS'
                        }
                    }
                }
            }
            $gameIndex++
        }
        Update-MultiGameUIState $listBox
        Update-ListBoxLabel $ListBox
    } catch {
         Write-Log "Ошибка в Rebuild-ListBoxItems: $($_.Exception.Message)" -Type 'ERROR'
    } finally {
        $listBox.EndUpdate()
    }
}

function Update-ListBoxLabel {
    param($ListBox)
    $labelToUpdate = $null
    try {
        $tabName = ($ListBox.Name -split '_')[1]
        if ($tabName -eq "Multi") {$tabName = "Multi_Combined"}
        
        if ($ListBox.Parent) {
            $found = $ListBox.Parent.Controls.Find("label_$tabName", $true)
            if ($found.Count -gt 0) {
                $labelToUpdate = $found[0]
                $baseTitle = ($labelToUpdate.Tag -as [string])
                if(-not $baseTitle) {
                    $baseTitle = $labelToUpdate.Text
                    $labelToUpdate.Tag = $baseTitle
                }
                $labelToUpdate.Text = $baseTitle
            }
        }
    } catch {
        Write-Log "Ошибка обновления метки списка: $($_.Exception.Message)" -Type 'WARN'
    }
}

function YEDDHandler_Convert {
    $listBox = $this
    $existingItems = @($listBox.Items)
    $newFoundItems = [System.Collections.Generic.List[System.IO.FileInfo]]::new()
    
    try {
        $allDroppedPaths = $_.Data.GetData('FileDrop')
        if (-not $allDroppedPaths) { return }

        $outputConf = $script:outputControls['Convert']
        if (-not $outputConf) { return }
        $targetFormat = $outputConf.SelectedFormat.ToLower()
        $acceptedExtensions = @('.nsp', '.nsz', '.xci', '.xcz') | Where-Object { $_ -ne ".$targetFormat" }
        
        Write-Log "Обработка Drag&Drop (Конвертация -> $targetFormat)..."

        foreach ($rawPathInput in $allDroppedPaths) {
            try {
                $rawPath = $rawPathInput.Trim()
                $safePath = $rawPath.Replace('/', '\')
                if ($safePath.Length -ge 240 -or $safePath -match '^[a-zA-Z]:' -or $safePath.StartsWith("\\")) {
                    if (-not $safePath.StartsWith("\\?\")) {
                        if ($safePath.StartsWith("\\")) { $safePath = "\\?\UNC\" + $safePath.Substring(2) } 
                        else { $safePath = "\\?\" + $safePath }
                    }
                }

                $item = $null
                $isFolder = $false
                $accessFailed = $false

                # Прямой доступ через Get-Item
                try {
                    $item = Get-Item -LiteralPath $safePath -ErrorAction Stop
                    $isFolder = $item.PSIsContainer
                } catch {
                    $accessFailed = $true
                }

                # Fallback: Поиск через родителя
                if ($accessFailed) {
                     $lastSlash = $safePath.LastIndexOf('\')
                     if ($lastSlash -gt 0) {
                         $parentDir = $safePath.Substring(0, $lastSlash)
                         $fileName = $safePath.Substring($lastSlash + 1)
                        
                         $found = Get-ChildItem -LiteralPath $parentDir -Force -ErrorAction SilentlyContinue | Where-Object { $_.Name -eq $fileName } | Select-Object -First 1
                         
                         if ($found) {
                             $item = $found
                             $isFolder = ($found -is [System.IO.DirectoryInfo])
                         }
                     }
                }

                if (-not $item) { continue }

                if ($isFolder) {
                    Get-ChildItem -LiteralPath $item.FullName -Recurse -File -Force -ErrorAction SilentlyContinue | Where-Object { 
                        $currentExt = if ($_.Extension) { $_.Extension.ToLower() } else { "" }
                        $currentExt -in $acceptedExtensions 
                    } | ForEach-Object { $newFoundItems.Add($_) }
                } else {
                    $currentExt = if ($item.Extension) { $item.Extension.ToLower() } else { "" }
                    if ($currentExt -in $acceptedExtensions) {
                        $newFoundItems.Add($item)
                    }
                }
            } catch {
                Write-Log "Ошибка при обработке пути '$rawPathInput': $($_.Exception.Message)" -Type 'WARN'
            }
        }

        if ($newFoundItems.Count -eq 0) { return }

        $existingPaths = $listBox.Items | ForEach-Object { $_.FullName }
        foreach ($file in $newFoundItems) {
            if ($file.FullName -notin $existingPaths) {
                $listBox.Items.Add($file)
                Write-Log "Добавлен для конвертации: $($file.Name)"
            }
        }

    } catch {
        Write-Log "Критическая ошибка Convert Handler: $($_.Exception.Message)" -Type 'ERROR'
        if (Get-Command 'YEmsg' -ErrorAction SilentlyContinue) {
            YEmsg "Ошибка. См. лог." "OK" $script:f
        }
    }
}
function YELBCHandler {
    if ($_.Button -eq 'Right') {
        $selectedItems = @($this.SelectedItems)
        if ($selectedItems.Count -eq 0) { return }

        # 1. Удаляем выбранные элементы визуально и пишем в лог
        foreach ($item in $selectedItems) {
            # Пытаемся получить красивое имя для лога
            $displayName = $item.Name
            if ($item.PSObject.Properties['DisplayString']) { $displayName = $item.DisplayString }
            elseif ($item.PSObject.Properties['DisplayText']) { $displayName = $item.DisplayText }
            
            Write-Log "Удален элемент: $displayName"
            $this.Items.Remove($item)
        }

        # 2. Перестраиваем список (Rebuild) ТОЛЬКО для Update и Unpack (кроме Loose файлов)
        # Вкладки Pack, Convert и Multi не требуют перегруппировки через Rebuild-ListBoxItems
        if ($this.Name -like "*Update*" -or ($this.Name -like "*Unpack*" -and $this.Name -notlike "*Loose*")) {
            
            # Собираем оставшиеся элементы правильно (учитывая, что они могут быть разного типа)
            $remainingItems = [System.Collections.Generic.List[object]]::new()
            
            foreach ($rowItem in $this.Items) {
                # Если элемент уже сгруппирован (обернут в объект с полем Item), достаем оригинал
                if ($rowItem.PSObject.Properties['Item']) {
                    $remainingItems.Add($rowItem.Item)
                } 
                # Если это сырой файл/папка
                elseif ($rowItem -is [System.IO.FileInfo] -or $rowItem -is [System.IO.DirectoryInfo]) {
                    $remainingItems.Add($rowItem)
                }
            }

            # Запускаем перестроение для обновления нумерации (#1, #2...)
            $isUnpack = ($this.Name -like "*Unpack*")
            Rebuild-ListBoxItems -ListBox $this -AllItems $remainingItems -UnpackMode $isUnpack
        }
    } 
    elseif ($_.Button -eq 'Middle') {
        # Логика средней кнопки (смена отображения имени/пути), если требуется
        if ($this.Items.Count -gt 0 -and $this.Items[0] -is [System.IO.FileInfo]) {
            $this.DisplayMember = if ($this.DisplayMember -eq 'Name') { 'FullName' } else { 'Name' }
        }
    }
}
#====================================================================================
#  БЛОК ГРАФИЧЕСКОГО ИНТЕРФЕЙСА (GUI)
#====================================================================================
function Create-OutputControls {
    param($parent, $lbWidth, $nsz_f, $yOffset = 0)
    $controls = [PSCustomObject]@{ SelectedFormat = 'NSP'; TxtOutFolder = $null; TxtOutFile = $null; FormatButtons = @() }
    
    $lblOutFormat = New-Object Label
    $lblOutFormat.Location = '9, ' + (320 + $yOffset)
    $lblOutFormat.Text = 'Формат вывода'
    $lblOutFormat.Font = $script:boldFont
    $lblOutFormat.AutoSize = 1
    
    $formatButtonWidth = ($lbWidth - 15) / 4
    'NSP,NSZ,XCI,XCZ'.Split(',') | ForEach-Object {
        $fmtIndex = $controls.FormatButtons.Count
        $fmtBtn = New-Object Button
        $fmtBtn.Text = $_
        $fmtBtn.Size = [Size]::new([int]$formatButtonWidth, 25)
        $fmtBtn.Location = [Point]::new((9 + ($fmtIndex * ([int]$formatButtonWidth + 5))), (346 + $yOffset))
        $fmtBtn.Font = $script:boldFont
        $fmtBtn.Name = $_
        $fmtBtn.BackColor = [ColorTranslator]::FromHtml('#3E3E42')
        $fmtBtn.ForeColor = [Color]::White
        $fmtBtn.FlatStyle = [FlatStyle]::Flat
        $fmtBtn.FlatAppearance.BorderSize = 1
        $fmtBtn.FlatAppearance.BorderColor = [ColorTranslator]::FromHtml('#6A6A70')
        $fmtBtn.FlatAppearance.MouseOverBackColor = [ColorTranslator]::FromHtml('#4E4E52')
        $fmtBtn.FlatAppearance.MouseDownBackColor = [ColorTranslator]::FromHtml('#28282C')
        
        $isEnabled = $true
        if (($fmtBtn.Name -in 'NSZ', 'XCZ') -and !$nsz_f) { 
            $isEnabled = $false
        }
        if (($fmtBtn.Name -in 'XCI', 'XCZ') -and -not (Test-Path $script:squirrel_exe)) {
            $isEnabled = $false
        }

        if (-not $isEnabled) {
            $fmtBtn.Enabled = $false
            $fmtBtn.BackColor = [ColorTranslator]::FromHtml('#333333')
        }
        
        $fmtBtn.Add_Click({
            $tabName = $this.Parent.Name.Split('_')[1]
            $currentControls = $script:outputControls[$tabName]
            if ($currentControls) {
                $oldFormat = $currentControls.SelectedFormat
                $currentControls.SelectedFormat = $this.Name
                Write-Log "Формат вывода изменен с '$oldFormat' на '$($this.Name)'"
                
                $activeColor = [ColorTranslator]::FromHtml('#007ACC')
                $inactiveColor = [ColorTranslator]::FromHtml('#3E3E42')
                
                $currentControls.FormatButtons | ForEach-Object { 
                    if ($_.Enabled) {
                        if ($_.Name -eq $currentControls.SelectedFormat) { $_.BackColor = $activeColor; $_.FlatAppearance.MouseOverBackColor = [ColorTranslator]::FromHtml('#1A8CD8') } else { $_.BackColor = $inactiveColor; $_.FlatAppearance.MouseOverBackColor = [ColorTranslator]::FromHtml('#4E4E52') }
                    }
                }
                
                # --- ЛОГИКА АКТИВАЦИИ TRIM ---
                if ($script:cbXciTrim) {
                    if ($this.Name -eq 'XCI') {
                        $script:cbXciTrim.Enabled = $true
                        # АВТОВКЛЮЧЕНИЕ: При выборе XCI ставим галочку
                        $script:cbXciTrim.Checked = $true
                        $script:cbXciTrim.ForeColor = [Color]::White
                    } else {
                        $script:cbXciTrim.Enabled = $false
                        # АВТОВЫКЛЮЧЕНИЕ: При смене формата снимаем галочку
                        $script:cbXciTrim.Checked = $false
                        $script:cbXciTrim.ForeColor = [Color]::Gray
                    }
                }
            }
        })
        $controls.FormatButtons += $fmtBtn
    }
    ($controls.FormatButtons | Where-Object { $_.Name -eq 'NSP' }).BackColor = [ColorTranslator]::FromHtml('#007ACC')
    ($controls.FormatButtons | Where-Object { $_.Name -eq 'NSP' }).FlatAppearance.MouseOverBackColor = [ColorTranslator]::FromHtml('#1A8CD8')
    
    $lblOutFolder = New-Object Label
    $lblOutFolder.Location = '9,' + (375 + $yOffset)
    $lblOutFolder.Text = 'Выходная папка'
    $lblOutFolder.Font = $script:boldFont
    $lblOutFolder.AutoSize = 1
    
    $txtOutFolder = New-Object TextBox
    $txtOutFolder.Location = '9,' + (401 + $yOffset)
    $txtOutFolder.Size = [Size]::new([int]($lbWidth - 90), 20)
    $txtOutFolder.Font = $script:regularFont
    $txtOutFolder.BackColor = [ColorTranslator]::FromHtml('#333333')
    $txtOutFolder.ForeColor = [Color]::White
    $txtOutFolder.BorderStyle = [BorderStyle]::FixedSingle
    $txtOutFolder.AllowDrop = $true
    
    $txtOutFolder.Add_DragEnter({ if ($_.Data.GetData("FileDrop") | % { (Get-Item -LiteralPath $_).PSIsContainer }) { $_.Effect = 'Copy' } else { $_.Effect = 'None' } })
    $txtOutFolder.Add_DragDrop({ if (($path=$_.Data.GetData("FileDrop")[0]) -and (Get-Item -LiteralPath $path).PSIsContainer) { $this.Text = $path } })
    $txtOutFolder.Add_TextChanged({ Request-SaveSettings })
    Add-CtrlA-Handler $txtOutFolder
    $controls.TxtOutFolder = $txtOutFolder
    
    $btnBrowseFolder = New-Object Button
    $btnBrowseFolder.Text = 'Обзор'
    $btnBrowseFolder.Size = '85, 25'
    $btnBrowseFolder.Location = [Point]::new([int]($txtOutFolder.Right + 5), (401 + $yOffset))
    $btnBrowseFolder.Font = $script:boldFont
    $btnBrowseFolder.BackColor = [ColorTranslator]::FromHtml('#3E3E42')
    $btnBrowseFolder.ForeColor = [Color]::White
    $btnBrowseFolder.FlatStyle = [FlatStyle]::Flat
    $btnBrowseFolder.FlatAppearance.BorderSize = 1
    $btnBrowseFolder.FlatAppearance.BorderColor = [ColorTranslator]::FromHtml('#6A6A70')
    $btnBrowseFolder.FlatAppearance.MouseOverBackColor = [ColorTranslator]::FromHtml('#4E4E52')
    $btnBrowseFolder.FlatAppearance.MouseDownBackColor = [ColorTranslator]::FromHtml('#28282C')
    $btnBrowseFolder.Add_Click({ if ($selectedFolder = YEFolder) { $txtOutFolder.Text = $selectedFolder } })
    
    $lblOutFile = New-Object Label
    $lblOutFile.Location = '9,' + (433 + $yOffset)
    $lblOutFile.Text = 'Выходное имя файла (пусто = авто)'
    $lblOutFile.Font = $script:boldFont
    $lblOutFile.AutoSize = 1
    
    $txtOutFile = New-Object TextBox
    $txtOutFile.Location = '9,' + (459 + $yOffset)
    $txtOutFile.Size = [Size]::new([int]$lbWidth, 20)
    $txtOutFile.Font = $script:regularFont
    $txtOutFile.BackColor = [ColorTranslator]::FromHtml('#333333')
    $txtOutFile.ForeColor = [Color]::White
    $txtOutFile.BorderStyle = [BorderStyle]::FixedSingle
    
    $txtOutFile.AllowDrop = $true
    $txtOutFile.Add_DragEnter({ 
        if ($_.Data.GetDataPresent("FileDrop")) { $_.Effect = 'Copy' } else { $_.Effect = 'None' } 
    })
    $txtOutFile.Add_DragDrop({
        $paths = $_.Data.GetData("FileDrop")
        if ($paths) {
            try {
                $p = $paths[0]
                if ([System.IO.Directory]::Exists($p)) {
                    $dirInfo = New-Object System.IO.DirectoryInfo($p)
                    $mainName = $dirInfo.Name
                    if ($script:cbComplexFolders -and $script:cbComplexFolders.Checked) {
                        $subDirs = Get-ChildItem -LiteralPath $p -Directory
                        if ($subDirs.Count -eq 1) {
                            $this.Text = "$mainName $($subDirs[0].Name)"
                        } elseif ($subDirs.Count -gt 1) {
                            $selected = Show-FolderSelectionDialog -mainFolderName $mainName -subFolders $subDirs
                            if ($selected) {
                                $this.Text = "$mainName $selected"
                            } else {
                                $this.Text = $mainName
                            }
                        } else {
                            $parentDir = $dirInfo.Parent
                            if ($parentDir) {
                                $this.Text = "$($parentDir.Name) $mainName"
                            } else {
                                $this.Text = $mainName
                            }
                        }
                    } else {
                        $this.Text = $mainName
                    }
                } elseif ([System.IO.File]::Exists($p)) {
                     $this.Text = (New-Object System.IO.FileInfo($p)).Name
                }
            } catch {
                Write-Log "Ошибка Drag&Drop в поле имени файла: $($_.Exception.Message)" -Type 'WARN'
            }
        }
    })
    
    Add-CtrlA-Handler $txtOutFile
    $controls.TxtOutFile = $txtOutFile
    
    $parent.Controls.AddRange(@($lblOutFolder, $txtOutFolder, $lblOutFile, $txtOutFile, $btnBrowseFolder, $lblOutFormat) + $controls.FormatButtons)
    return $controls
}

function Check-DuplicateTask {
    param($OutDir, $OutName, $OutFormat, $BaseFile)
    
    $finalName = $OutName
    if ([string]::IsNullOrWhiteSpace($finalName)) {
        if ($BaseFile) {
            $rawName = [System.IO.Path]::GetFileNameWithoutExtension($BaseFile.Name)
            $cleanName = ($rawName -split '\[')[0].Trim()
            $cleanName = ($cleanName -split '\(')[0].Trim()
            $finalName = $cleanName -replace '["\[\](){}]', ''
        } else {
            return $false
        }
    }
    
    $ext = ""
    if (-not [string]::IsNullOrWhiteSpace($OutFormat)) {
        $ext = if ($OutFormat -eq 'FOLDER') { "" } elseif ($OutFormat -eq 'NCA') { ".nca" } else { "." + $OutFormat.ToLower() }
    } else {
        $ext = ".nsp" # Резервное значение для избежания ошибок
    }
    
    if ($ext -ne "" -and $finalName.ToLower().EndsWith($ext.ToLower())) {
        $finalName = $finalName.Substring(0, $finalName.Length - $ext.Length)
    }
    
    $exists = $false
    $matchName = ""
    
    if (-not [string]::IsNullOrWhiteSpace($OutName)) {
        $fullPath = Join-Path $OutDir ($finalName + $ext)
        $exists = Test-Path -LiteralPath $fullPath
        $matchName = $finalName + $ext
    } else {
        # Если имя формируется автоматически, оно содержит TitleID и прочее. Ищем по маске.
        $safeName = [regex]::Escape($finalName)
        $foundFiles = Get-ChildItem -LiteralPath $OutDir -Filter "$finalName*$ext" -File -ErrorAction SilentlyContinue | Where-Object { $_.Name -match "^$safeName" }
        $exists = ($foundFiles.Count -gt 0)
        $matchName = if ($exists) { $foundFiles[0].Name } else { "$finalName...$ext" }
        
        # Если по базовому имени не нашли, но это Multi, проверяем также ModdedBase_ (на случай сбоев метаданных)
        if (-not $exists -and $BaseFile -and $BaseFile.Name -match '\[([0-9a-fA-F]{16})\]') {
            $tid = $matches[1].ToUpper()
            $modFound = Get-ChildItem -LiteralPath $OutDir -Filter "ModdedBase_[$tid]*$ext" -File -ErrorAction SilentlyContinue
            if ($modFound.Count -gt 0) {
                $exists = $true
                $matchName = $modFound[0].Name
            }
        }
    }
    
    if ($exists) {
        $msg = "Внимание: В выходной папке '$OutDir' уже существует файл (или папка) '$matchName'.`n`nВероятно, эта задача уже выполнялась. Вы уверены, что хотите добавить её снова? Существующий файл может быть перезаписан или задублирован."
        $ans = YECustomConfirmMsg -msg $msg -title "Проверка истории" -btnYesText "ОБРАБОТАТЬ" -btnNoText "ОТМЕНИТЬ"
        if ($ans -eq 'No') { return $true }
    }
    return $false
}

function Refresh-HistoryTab {
    if (-not $script:histGrid) { return }
    $script:histGrid.Rows.Clear()
    $historyFile = Join-Path $script:cd "ssb.history.json"
    if (Test-Path $historyFile) {
        try {
            $content = Get-Content -LiteralPath $historyFile -Raw -Encoding UTF8
            if ($content) {
                $history = ConvertFrom-Json $content
                if ($history -isnot [array]) { $history = @($history) }
                $search = $script:histSearch.Text.ToLower()
                foreach ($h in $history) {
                    if (-not [string]::IsNullOrWhiteSpace($search)) {
                        $match = ($h.Date.ToLower().Contains($search) -or $h.TaskType.ToLower().Contains($search) -or $h.OriginalName.ToLower().Contains($search) -or $h.FinalPath.ToLower().Contains($search))
                        if (-not $match) { continue }
                    }
                    $dateStr = $h.Date
                    try {
                        $dt = [datetime]::Parse($h.Date)
                        $dateStr = $dt.ToString("dd.MM.yyyy HH:mm")
                    } catch {}
                    
                    $outFileName = "-"
                    $outDirName = "-"
                    if ($h.FinalPath -and $h.FinalPath -ne "-") {
                        try {
                            $outFileName = Split-Path $h.FinalPath -Leaf
                            $outDirName = Split-Path $h.FinalPath -Parent
                        } catch {
                            $outFileName = $h.FinalPath
                            $outDirName = $h.FinalPath
                        }
                    } else {
                        $outFileName = $h.OriginalName
                    }
                    
                    [void]$script:histGrid.Rows.Add($dateStr, $h.TaskType, $outFileName, $outDirName)
                    $lastRow = $script:histGrid.Rows[$script:histGrid.Rows.Count - 1]
                    $lastRow.Cells['Type'].ToolTipText = $h.TaskType
                    $lastRow.Cells['Game'].ToolTipText = $outFileName
                    $lastRow.Cells['OutDir'].ToolTipText = $outDirName
                }
            }
        } catch {
            Write-Log "Ошибка чтения истории: $($_.Exception.Message)" -Type 'ERROR'
        }
    }
}

function Create-HistoryTab {
    param($parentControl)
    $histX = 15; $histY = 30;
    
    $lblHistTitle = New-Object Label
    $lblHistTitle.Text = "История обработок"
    $lblHistTitle.Font = New-Object Font('Segoe UI Semibold', 14, [FontStyle]::Bold)
    $lblHistTitle.ForeColor = [Color]::FromArgb(255, 255, 200, 100)
    $lblHistTitle.AutoSize = $true
    $lblHistTitle.Location = "$histX, $histY"
    $parentControl.Controls.Add($lblHistTitle)
    
    $btnHistClear = New-Object Button
    $btnHistClear.Text = "Очистить историю"
    $btnHistClear.Font = $script:regularFont
    $btnHistClear.BackColor = [ColorTranslator]::FromHtml('#C53030')
    $btnHistClear.ForeColor = [Color]::White
    $btnHistClear.FlatStyle = [FlatStyle]::Flat
    $btnHistClear.FlatAppearance.BorderSize = 0
    $btnHistClear.Size = "150, 30"
    $btnHistClear.Location = "$($parentControl.Width - 170), $histY"
    $btnHistClear.Cursor = [Cursors]::Hand
    $parentControl.Controls.Add($btnHistClear)
    
    $histY += 40
    
    $lblSearch = New-Object Label
    $lblSearch.Text = "Поиск:"
    $lblSearch.Font = $script:regularFont
    $lblSearch.ForeColor = [Color]::White
    $lblSearch.AutoSize = $true
    $lblSearch.Location = "$histX, $($histY + 4)"
    $parentControl.Controls.Add($lblSearch)
    
    $txtSearch = New-Object TextBox
    $txtSearch.Font = $script:regularFont
    $txtSearch.BackColor = [ColorTranslator]::FromHtml('#333333')
    $txtSearch.ForeColor = [Color]::White
    $txtSearch.BorderStyle = [BorderStyle]::FixedSingle
    $txtSearch.Size = "300, 25"
    $txtSearch.Location = "$($histX + 60), $histY"
    $parentControl.Controls.Add($txtSearch)
    
    $histY += 40
    
    $histGrid = New-Object DataGridView
    $histGrid.Name = "histGrid"
    $histGrid.BackgroundColor = [ColorTranslator]::FromHtml('#1e1e1e')
    $histGrid.ForeColor = [Color]::White
    $histGrid.GridColor = [ColorTranslator]::FromHtml('#6A6A70')
    $histGrid.BorderStyle = [BorderStyle]::None
    $histGrid.AllowUserToAddRows = $false
    $histGrid.AllowUserToDeleteRows = $false
    $histGrid.ReadOnly = $true
    $histGrid.SelectionMode = [DataGridViewSelectionMode]::FullRowSelect
    $histGrid.MultiSelect = $false
    $histGrid.RowHeadersVisible = $false
    $histGrid.AutoSizeColumnsMode = [DataGridViewAutoSizeColumnsMode]::Fill
    $histGrid.AllowUserToResizeRows = $false
    $histGrid.Font = $script:smallFont
    $histGrid.ShowCellToolTips = $true
    $histGrid.RowTemplate.Height = 28
    
    $histGrid.EnableHeadersVisualStyles = $false
    $headerStyle = New-Object DataGridViewCellStyle
    $headerStyle.BackColor = [ColorTranslator]::FromHtml('#3E3E42')
    $headerStyle.ForeColor = [Color]::White
    $headerStyle.Font = New-Object Font('Segoe UI', 9, [FontStyle]::Bold)
    $headerStyle.Alignment = 'MiddleCenter'
    $histGrid.ColumnHeadersDefaultCellStyle = $headerStyle
    $histGrid.ColumnHeadersHeight = 30
    $histGrid.ColumnHeadersHeightSizeMode = 'DisableResizing'
    $histGrid.ColumnHeadersBorderStyle = [DataGridViewHeaderBorderStyle]::Single
    $histGrid.CellBorderStyle = [DataGridViewCellBorderStyle]::Single
    
    $cellStyle = New-Object DataGridViewCellStyle
    $cellStyle.BackColor = [ColorTranslator]::FromHtml('#252528')
    $cellStyle.ForeColor = [Color]::White
    $cellStyle.SelectionBackColor = [ColorTranslator]::FromHtml('#005A9E')
    $cellStyle.SelectionForeColor = [Color]::White
    $cellStyle.Padding = New-Object Padding(4, 0, 4, 0)
    $histGrid.DefaultCellStyle = $cellStyle
    
    $altCellStyle = New-Object DataGridViewCellStyle
    $altCellStyle.BackColor = [ColorTranslator]::FromHtml('#2D2D30')
    $altCellStyle.Padding = New-Object Padding(4, 0, 4, 0)
    $histGrid.AlternatingRowsDefaultCellStyle = $altCellStyle
    $histGrid.Size = New-Object Size(($parentControl.Width - 30), ($parentControl.Height - $histY - 20))
    $histGrid.Location = New-Object Point($histX, $histY)
    
    [void]$histGrid.Columns.Add('Date', 'Дата')
    [void]$histGrid.Columns.Add('Type', 'Задача')
    [void]$histGrid.Columns.Add('Game', 'Выходное имя файла')
    [void]$histGrid.Columns.Add('OutDir', 'Выходная папка')
    
    $histGrid.Columns['Date'].Width = 140
    $histGrid.Columns['Type'].Width = 120
    $histGrid.Columns['Game'].FillWeight = 200
    
    $parentControl.Controls.Add($histGrid)
    
    $script:histGrid = $histGrid
    $script:histSearch = $txtSearch
    
    $btnHistClear.Add_Click({
        $ans = YECustomConfirmMsg -msg "Вы уверены, что хотите удалить всю историю?" -title "Очистка истории" -btnYesText "ОЧИСТИТЬ" -btnNoText "ОТМЕНИТЬ"
        if ($ans -eq 'Yes') {
            $historyFile = Join-Path $script:cd "ssb.history.json"
            if (Test-Path $historyFile) { Remove-Item $historyFile -Force -ErrorAction SilentlyContinue }
            Refresh-HistoryTab
        }
    })
    
    $txtSearch.Add_TextChanged({ Refresh-HistoryTab })
    Refresh-HistoryTab
}

function YEGUI {
    $script:suppressSave = $true

    # Сбрасываем счетчик логов при пересоздании окна
    $script:lastGeneralLogCount = 0
    $script:f = New-Object Form
    $script:f.Size = '1724, 932'
    $script:f.BackColor = [ColorTranslator]::FromHtml('#2D2D30')
    $script:f.ForeColor = [Color]::White
    $script:f.Font = $script:regularFont
    $script:f.FormBorderStyle = [FormBorderStyle]::None
    $script:f.Text = ''
    $script:f.MaximizeBox = $false
    $script:f.StartPosition = [FormStartPosition]::WindowsDefaultLocation

    $script:tt = New-Object System.Windows.Forms.ToolTip
    $script:tt.AutoPopDelay = 15000
    $script:tt.InitialDelay = 500
    $script:tt.ReshowDelay = 200
    $script:tt.ShowAlways = $true
    $script:tt.ToolTipIcon = [System.Windows.Forms.ToolTipIcon]::Info
    $script:tt.ToolTipTitle = "Подсказка"
    $script:tt.BackColor = [ColorTranslator]::FromHtml('#2D2D30')
    $script:tt.ForeColor = [Color]::White
    $script:tt.UseAnimation = $true
    $script:tt.UseFading = $true

    $titleBarHeight = 30
    $titleBar = New-Object Panel
    $titleBar.Height = $titleBarHeight
    $titleBar.Dock = [DockStyle]::Top
    $titleBar.BackColor = [ColorTranslator]::FromHtml('#252526')
    $titleBar.Add_Paint({
        param($s, $e)
        $rect = $s.ClientRectangle
        $colorStart = [Color]::FromArgb(255, 0, 35, 60)
        $colorEnd = [ColorTranslator]::FromHtml('#252526')
        $brush = New-Object LinearGradientBrush($rect, $colorStart, $colorEnd, [LinearGradientMode]::Horizontal)
        $e.Graphics.FillRectangle($brush, $rect)
        $brush.Dispose()
    })
    $titleLabel = New-Object Label
    $titleLabel.Text = $script:title
    $titleLabel.Font = $script:boldFont
    $titleLabel.ForeColor = [Color]::White
    $titleLabel.BackColor = [Color]::Transparent
    $titleLabel.Location = '10, 7'
    $titleLabel.AutoSize = $true

    $btnClose = New-Object Button
    $btnClose.Size = [Size]::new(46, $titleBarHeight)
    $btnClose.Dock = [DockStyle]::Right
    $btnClose.Text = '✕'
    $btnClose.Font = New-Object Font('Segoe UI', 10)
    $btnClose.ForeColor = [Color]::White
    $btnClose.BackColor = [Color]::Transparent
    $btnClose.FlatStyle = [FlatStyle]::Flat
    $btnClose.FlatAppearance.BorderSize = 0
    $btnClose.FlatAppearance.MouseOverBackColor = [Color]::FromArgb(255, 232, 17, 35)
    $btnClose.FlatAppearance.MouseDownBackColor = [Color]::FromArgb(255, 153, 10, 20)
    $btnClose.Add_Click({ $script:f.Close() })

    $btnMinimize = New-Object Button
    $btnMinimize.Size = [Size]::new(46, $titleBarHeight)
    $btnMinimize.Dock = [DockStyle]::Right
    $btnMinimize.Text = '—'
    $btnMinimize.Font = New-Object Font('Segoe UI', 9)
    $btnMinimize.ForeColor = [Color]::White
    $btnMinimize.BackColor = [Color]::Transparent
    $btnMinimize.FlatStyle = [FlatStyle]::Flat
    $btnMinimize.FlatAppearance.BorderSize = 0
    $btnMinimize.FlatAppearance.MouseOverBackColor = [Color]::FromArgb(255, 63, 63, 65)
    $btnMinimize.FlatAppearance.MouseDownBackColor = [Color]::FromArgb(255, 0, 122, 204)
    $btnMinimize.Add_Click({ $script:f.WindowState = [FormWindowState]::Minimized })
    
    $script:dragging = $false
    $script:dragCursorPoint = $null
    $script:dragFormPoint = $null
    $dragHandlerMouseUp = { param($s, $e); if ($e.Button -eq [MouseButtons]::Left) { $script:dragging = $false } }
    $dragHandlerMouseDown = { param($s, $e); if ($e.Button -eq [MouseButtons]::Left) { $script:dragging = $true; $script:dragCursorPoint = [Cursor]::Position; $script:dragFormPoint = $script:f.Location } }
    $dragHandlerMouseMove = { param($s, $e); if ($script:dragging) { $currentScreenPos = [Cursor]::Position; $diff = [Point]::Subtract($currentScreenPos, [Size]$script:dragCursorPoint); $script:f.Location = [Point]::Add($script:dragFormPoint, [Size]$diff) } }
    
    $titleBar.Add_MouseDown($dragHandlerMouseDown); $titleBar.Add_MouseMove($dragHandlerMouseMove); $titleBar.Add_MouseUp($dragHandlerMouseUp)
    $titleLabel.Add_MouseDown($dragHandlerMouseDown); $titleLabel.Add_MouseMove($dragHandlerMouseMove); $titleLabel.Add_MouseUp($dragHandlerMouseUp)
    $titleBar.Controls.Add($titleLabel)
    $titleBar.Controls.Add($btnMinimize)
    $titleBar.Controls.Add($btnClose)
    $script:f.Controls.Add($titleBar)

    $script:f.Add_LocationChanged({ if((-not $script:suppressSave) -and ($script:f.WindowState -eq 'Normal')) { Request-SaveSettings } })
    $script:f.Add_SizeChanged({ if((-not $script:suppressSave) -and ($script:f.WindowState -eq 'Normal')) { Request-SaveSettings } })
    $script:f.Add_HandleCreated({
        try {
            $darkMode = 1
            [DwmApi]::DwmSetWindowAttribute($script:f.Handle, 20, [ref]$darkMode, 4)
            [DwmApi]::DwmSetWindowAttribute($script:f.Handle, 19, [ref]$darkMode, 4)
            foreach ($ctrl in $script:taskGrid.Controls) {
                if ($ctrl -is [System.Windows.Forms.ScrollBar]) {
                    [UxTheme]::SetWindowTheme($ctrl.Handle, "DarkMode_Explorer", $null)
                }
            }
        } catch {
            Write-Log "Не удалось применить DWM тёмную тему: $($_.Exception.Message)" -Type 'DEBUG'
        }
    })

    $script:f.Add_Click({ if ($script:taskGrid.SelectedRows.Count -gt 0) { $script:taskGrid.ClearSelection() } })
    
    $script:f.Add_FormClosing({
        $script:f.WindowState = [FormWindowState]::Normal
        Write-Log "Закрытие приложения. Завершение всех активных задач и их дочерних процессов..."
        $pidsToKill = @($script:runningTasks.Keys)
        foreach ($pid in $pidsToKill) {
            Write-Log "Принудительное завершение дерева процессов для задачи с PID: $pid"
            Stop-ProcessTree -ProcessId $pid
        }
        $script:taskUpdateTimer.Stop()
        $script:taskUpdateTimer.Dispose()
        foreach ($key in $script:outputControls.Keys) { $script:outputControls[$key].TxtOutFile.Text = "" }
        Save-Settings
        if(-not $script:DebugMode) {
            Get-Item -Path "$wdir\*", "$cd\CRASH_REPORT*", "$cd\yanu.log*" -ErrorAction SilentlyContinue | Remove-Item -Force -Recurse
            Get-Item -Path "$tdir\yanu.log.*", "$tdir\base*", "$tdir\NSCB.log", "$tdir\*.tmp" -ErrorAction SilentlyContinue | Remove-Item -Force -Recurse
        }
        # Закрытие файлового лога
        if ($script:logFileWriter) {
            try {
                $script:logFileWriter.WriteLine("[$(Get-Date -Format 'HH:mm:ss.fff')] [INFO] Приложение закрыто.")
                $script:logFileWriter.Flush()
                $script:logFileWriter.Close()
                $script:logFileWriter.Dispose()
                $script:logFileWriter = $null
            } catch { }
        }
    })
    
    $script:f.Add_Load({
        Update-LogView
        try {
            [User32]::ChangeWindowMessageFilterEx($script:f.Handle, [User32]::WM_DROPFILES, [User32]::MSGFLT_ALLOW, [System.IntPtr]::Zero) | Out-Null
            [User32]::ChangeWindowMessageFilterEx($script:f.Handle, [User32]::WM_COPYDATA, [User32]::MSGFLT_ALLOW, [System.IntPtr]::Zero) | Out-Null
            [User32]::ChangeWindowMessageFilterEx($script:f.Handle, [User32]::WM_COPYGLOBALDATA, [User32]::MSGFLT_ALLOW, [System.IntPtr]::Zero) | Out-Null
        } catch {
            Write-Log "Не удалось применить фильтр сообщений окна для Drag&Drop."
        }
    })
    
    # Таймер обновления задач - оптимизирован для мгновенного вывода логов
    $script:taskUpdateTimer = New-Object Timer
    $script:taskUpdateTimer.Interval = 250  # Уменьшено с 500мс для более быстрого обновления логов
    $script:taskUpdateTimer.Add_Tick({ try { Check-RunningTasks } catch { Write-Log "Ошибка в таймере обновления задач: $($_.Exception.Message)" -Type 'ERROR' } })
    $script:taskUpdateTimer.Start()
    
    $mainYOffset = 42
    $navGroupBox = New-Object GroupBox
    $navGroupBox.Location = "8, $mainYOffset"
    $navGroupBox.Name = 'navGroupBox'
    $navGroupBox.Size = '216, 620'
    $navGroupBox.Text = "Управление"
    $navGroupBox.ForeColor = [Color]::White
    $navGroupBox.Font = $script:boldFont
    $navGroupBox.Add_Click({ $script:taskGrid.ClearSelection() })
    $script:f.Controls.Add($navGroupBox)
    
    $taskGroupBox = New-Object GroupBox
    $taskGroupBox.Location = "824, $mainYOffset"
    $taskGroupBox.Size = '892, 620'
    $taskGroupBox.Text = "Задачи"
    $taskGroupBox.ForeColor = [Color]::White
    $taskGroupBox.Font = New-Object Font('Segoe UI', 9, [FontStyle]::Bold)
    $taskGroupBox.Add_Click({ $script:taskGrid.ClearSelection() })
    $script:f.Controls.Add($taskGroupBox)

    $logGroupBox = New-Object GroupBox
    $logGroupBox.Location = "$($navGroupBox.Left), $($navGroupBox.Bottom + 8)"
    $logGroupBox.Size = [Size]::new(($taskGroupBox.Right - $navGroupBox.Left), 194)
    $logGroupBox.Text = "Блок логов"
    $logGroupBox.ForeColor = [Color]::White
    $logGroupBox.Font = $script:boldFont
    $logGroupBox.Add_Click({ $script:taskGrid.ClearSelection() })
    $script:f.Controls.Add($logGroupBox)
    
    # --- БЛОК: Глобальный прогресс ---
    $script:globalProgressVal = 0.0
    $script:globalProgressText = "Ожидание..."
    $script:globalProgressColor = '#2E7D32'  # Зелёный по умолчанию

    $globalGroupBox = New-Object GroupBox
    $globalGroupBox.Location = "$($navGroupBox.Left), $($logGroupBox.Bottom + 6)"
    $globalGroupBox.Size = [Size]::new(($taskGroupBox.Right - $navGroupBox.Left), 54)
    $globalGroupBox.Text = "Общий прогресс"
    $globalGroupBox.ForeColor = [Color]::White
    $globalGroupBox.Font = $script:boldFont
    $script:f.Controls.Add($globalGroupBox)
    $script:globalGroupBox = $globalGroupBox
    $script:globalProgressPanel = New-Object Panel
    $script:globalProgressPanel.Location = "6, 20"
    $script:globalProgressPanel.Size = [Size]::new(($globalGroupBox.Width - 12), 26)
    $script:globalProgressPanel.Anchor = 'Top, Left, Right'
    $script:globalProgressPanel.BackColor = [ColorTranslator]::FromHtml('#333333')
    $script:globalProgressPanel.Add_Paint({
        param($s, $e)
        $g = $e.Graphics; $rec = $s.ClientRectangle
        $bgBrush = New-Object SolidBrush([ColorTranslator]::FromHtml('#333333'))
        $g.FillRectangle($bgBrush, $rec)
        if ($script:globalProgressVal -gt 0) {
            $width = [int](($rec.Width * $script:globalProgressVal) / 100)
            $progRec = New-Object Rectangle(0, 0, $width, $rec.Height)
            $progBrush = New-Object SolidBrush([ColorTranslator]::FromHtml($script:globalProgressColor))
            $g.FillRectangle($progBrush, $progRec)
            $progBrush.Dispose()
        }
        $text = $script:globalProgressText
        $font = $script:boldFont
        $textBrush = New-Object SolidBrush([Color]::White)
        $stringFormat = New-Object StringFormat
        $stringFormat.Alignment = 'Center'
        $stringFormat.LineAlignment = 'Center'
        $rectF = New-Object System.Drawing.RectangleF($rec.X, $rec.Y, $rec.Width, $rec.Height)
        $g.DrawString($text, $font, $textBrush, $rectF, $stringFormat)
        $bgBrush.Dispose(); $textBrush.Dispose(); $stringFormat.Dispose()
    })
    $globalGroupBox.Controls.Add($script:globalProgressPanel)
    
    $YELBKeyDownHandler = {
        param($s, $e)
        if ($e.Control -and $e.KeyCode -eq 'A') {
            $lb = $s
            if ($lb.SelectionMode -eq 'MultiExtended' -or $lb.SelectionMode -eq 'MultiSimple') {
                for ($i = 0; $i -lt $lb.Items.Count; $i++) { $lb.SetSelected($i, $true) }
                $e.Handled = $true
            }
        }
        elseif ($e.KeyCode -eq 'Delete') {
            $listBox = $s
            if ($listBox.SelectedItems.Count -gt 0) {
                $itemsToRemove = @($listBox.SelectedItems)
                foreach ($item in $itemsToRemove) {
                    $displayName = if ($item.PSObject.Properties.Name -contains 'DisplayString') { $item.DisplayString } else { $item.Name }
                    Write-Log "Удалён элемент: $displayName"
                    $listBox.Items.Remove($item)
                }
                $e.Handled = $true
            }
        }
    }
    
    $script:listBoxOriginalBackColor = [ColorTranslator]::FromHtml('#333333')
    $script:listBoxDragOverBackColor = [ColorTranslator]::FromHtml('#007ACC')
    $dragEnterHandler = { $_.Effect = [DragDropEffects]::Copy; $this.BackColor = $script:listBoxDragOverBackColor }
    $dragLeaveHandler = { $this.BackColor = $script:listBoxOriginalBackColor }
    
    $script:outputControls = @{}
    for($i = 0; $i -lt $script:buttons.Count; $i++) {
        $currentButtonText = $script:buttons[$i]
        $currentButtonName = $script:button_names[$i]
        # Пропускаем пустые элементы (служат как отступ)
        if ([string]::IsNullOrEmpty($currentButtonName)) { continue }
        $yPos = 20 + ($i * 38)
        $b = New-Object Button
        $b.Location = '8,' + $yPos
        $b.Size = '200, 30'
        $b.Name = "NavButton_$currentButtonName"
        $b.Tag = $currentButtonName
        $b.Text = $currentButtonText
        $b.TextAlign = 'MiddleLeft'
        $b.Font = $script:boldFont
        $b.ForeColor = [Color]::White
        $b.FlatStyle = [FlatStyle]::Flat
        $b.FlatAppearance.BorderSize = 1
        $b.FlatAppearance.BorderColor = [ColorTranslator]::FromHtml('#6A6A70')
        switch ($currentButtonName) {
            'System' { $b.BackColor = [Color]::FromArgb(255, 75, 0, 130); $b.FlatAppearance.MouseOverBackColor = [ColorTranslator]::FromHtml('#5E1299') }
            'Settings' { $b.BackColor = [ColorTranslator]::FromHtml('#1ABC9C'); $b.FlatAppearance.MouseOverBackColor = [ColorTranslator]::FromHtml('#2DCEAE') }
            default { $b.BackColor = [ColorTranslator]::FromHtml('#3E3E42'); $b.FlatAppearance.MouseOverBackColor = [ColorTranslator]::FromHtml('#4E4E52') }
        }
        $b.Add_Click({
            $clickedButtonTag = $this.Tag
            $navGB = $script:f.Controls.Find("navGroupBox", $true)[0]
            foreach ($navBtn in $navGB.Controls) {
                if ($navBtn -is [Button] -and $navBtn.Name -like "NavButton_*") {
                    if ($navBtn.Enabled) {
                        switch ($navBtn.Tag) {
                            'System' { $navBtn.BackColor = [Color]::FromArgb(255, 75, 0, 130); $navBtn.FlatAppearance.MouseOverBackColor = [ColorTranslator]::FromHtml('#5E1299') }
                            'Settings' { $navBtn.BackColor = [ColorTranslator]::FromHtml('#1ABC9C'); $navBtn.FlatAppearance.MouseOverBackColor = [ColorTranslator]::FromHtml('#2DCEAE') }
                            default { $navBtn.BackColor = [ColorTranslator]::FromHtml('#3E3E42'); $navBtn.FlatAppearance.MouseOverBackColor = [ColorTranslator]::FromHtml('#4E4E52') }
                        }
                    }
                }
            }
            $this.BackColor = [ColorTranslator]::FromHtml('#007ACC')
            $this.FlatAppearance.MouseOverBackColor = [ColorTranslator]::FromHtml('#1A8CD8')
            $script:f.Controls | ForEach-Object {
                if ($_.GetType().Name -eq 'GroupBox' -and $_.Name -match 'TabPanel_') {
                    $_.Visible = ($_.Name -eq "TabPanel_$clickedButtonTag")
                }
            }
        })
        $navGroupBox.Controls.Add($b)
        $gb = New-Object GroupBox
        $gb.Name = "TabPanel_$currentButtonName"
        $gb.Text = $currentButtonText
        $gb.Font = $script:boldFont
        $gb.Location = "232, $mainYOffset"
        $gb.Size = '584, 620'
        $gb.Visible = if ($currentButtonName -eq 'Multi') { 1 } else { 0 }
        $gb.ForeColor = [Color]::White
        $gb.Add_Click({ $script:taskGrid.ClearSelection() })
        $script:f.Controls.Add($gb)
        $lbWidth = $gb.ClientSize.Width - 18

        if ($currentButtonName -eq 'Update') {
            $outputControlsHeight = 165; $outputControlsTopY = $gb.ClientSize.Height - $outputControlsHeight - 10
            $listTopMargin = 40; $labelTopMargin = 21; $listHeight = $outputControlsTopY - $listTopMargin - 10
            $lbl = New-Object Label; $lbl.Location = "9, $labelTopMargin"; $lbl.Name = "label_Update"; $lbl.Text = "Файлы (Базовая игра и Файл обновления)"; $lbl.Font = $script:boldFont; $lbl.AutoSize = $true
            $ltbx = New-Object ListBox; $ltbx.Name = "listBox_Update_0"; $ltbx.Location = "9, $listTopMargin"; $ltbx.Size = [Size]::new($lbWidth, [int]$listHeight); $ltbx.AllowDrop = $true; $ltbx.BackColor = $script:listBoxOriginalBackColor; $ltbx.ForeColor = [Color]::White; $ltbx.BorderStyle = [BorderStyle]::FixedSingle; $ltbx.Font = $script:regularFont; $ltbx.DisplayMember = 'DisplayString'; $ltbx.HorizontalScrollbar = $true; $ltbx.IntegralHeight = $false; $ltbx.SelectionMode = 'MultiExtended'
            $ltbx.Add_DragEnter($dragEnterHandler); $ltbx.Add_DragLeave($dragLeaveHandler)
            
            # --- ИСПОЛЬЗОВАНИЕ НОВОГО HANDLER ---
            $ltbx.Add_DragDrop({ $this.BackColor = $script:listBoxOriginalBackColor; YEDDHandler_Standard })
            
            $ltbx.Add_MouseUP({ YELBCHandler }); $ltbx.Add_KeyDown($YELBKeyDownHandler)
            $gb.Controls.AddRange(@($lbl, $ltbx))
            $outputControlsYOffset = $outputControlsTopY - 320
            $script:outputControls[$currentButtonName] = Create-OutputControls $gb $lbWidth $script:nsz_f $outputControlsYOffset
            $script:outputControls[$currentButtonName].TxtOutFolder.Text = $script:defaultOutPaths['Update']
        }
        elseif ($currentButtonName -eq 'Unpack') {
            $currentY = 21; $labelToListboxSpacing = 19; $groupSpacing = 8
            $outputSectionTopY = $gb.ClientSize.Height - 75
            $availableHeight = $outputSectionTopY - $currentY - 30
            $listHeight = [int](($availableHeight - ($labelToListboxSpacing * 2) - $groupSpacing) / 2)
            
            # --- Поле 1: Файлы (Базовая игра и Файл обновления) ---
            $lblCombined = New-Object Label; $lblCombined.Location = "9, $currentY"; $lblCombined.Name = "label_Unpack"; $lblCombined.Text = 'Файлы (Базовая игра и Файл обновления)'; $lblCombined.Font = $script:boldFont; $lblCombined.AutoSize = 1
            $currentY += $labelToListboxSpacing
            $ltbxCombined = New-Object ListBox; $ltbxCombined.Name = "listBox_Unpack_Combined"; $ltbxCombined.Location = "9, $currentY"; $ltbxCombined.Size = [Size]::new($lbWidth, $listHeight)
            $ltbxCombined.AllowDrop=1; $ltbxCombined.BackColor=$script:listBoxOriginalBackColor; $ltbxCombined.ForeColor='White'; $ltbxCombined.BorderStyle=[BorderStyle]::FixedSingle; $ltbxCombined.Font=$script:regularFont; $ltbxCombined.DisplayMember='DisplayString'; $ltbxCombined.HorizontalScrollbar=1; $ltbxCombined.IntegralHeight=0; $ltbxCombined.SelectionMode='MultiExtended'
            $ltbxCombined.Add_DragEnter($dragEnterHandler); $ltbxCombined.Add_DragLeave($dragLeaveHandler)
            $ltbxCombined.Add_DragDrop({ $this.BackColor = $script:listBoxOriginalBackColor; YEDDHandler_Standard })
            $ltbxCombined.Add_MouseUP({YELBCHandler}); $ltbxCombined.Add_KeyDown($YELBKeyDownHandler)
            $currentY = $ltbxCombined.Bottom + $groupSpacing

            # --- Поле 2: Отдельный файл (NCA, romfs.bin) ---
            $lblLoose = New-Object Label; $lblLoose.Location = "9, $currentY"; $lblLoose.Name = "label_Unpack_Loose"; $lblLoose.Text = 'Отдельный файл (.nca, romfs.bin)'; $lblLoose.Font = $script:boldFont; $lblLoose.AutoSize = 1
            $currentY += $labelToListboxSpacing
            $ltbxLoose = New-Object ListBox; $ltbxLoose.Name = "listBox_Unpack_Loose"; $ltbxLoose.Location = "9, $currentY"; $ltbxLoose.Size = [Size]::new($lbWidth, $listHeight)
            $ltbxLoose.AllowDrop=1; $ltbxLoose.BackColor=$script:listBoxOriginalBackColor; $ltbxLoose.ForeColor='White'; $ltbxLoose.BorderStyle=[BorderStyle]::FixedSingle; $ltbxLoose.Font=$script:regularFont; $ltbxLoose.DisplayMember='Name'; $ltbxLoose.HorizontalScrollbar=1; $ltbxLoose.IntegralHeight=0; $ltbxLoose.SelectionMode=3
            $ltbxLoose.Add_DragEnter($dragEnterHandler); $ltbxLoose.Add_DragLeave($dragLeaveHandler); $ltbxLoose.Add_DragDrop({
                param($s, $e)
                $this.BackColor = $script:listBoxOriginalBackColor
                $allDroppedPaths = $e.Data.GetData('FileDrop')
                $allDroppedPaths | ForEach-Object {
                    $item = Get-Item -LiteralPath $_; if($item.Extension -in '.nca', '.bin') { $s.Items.Add($item)}
                }
            }); $ltbxLoose.Add_MouseUP({YELBCHandler}); $ltbxLoose.Add_KeyDown($YELBKeyDownHandler)

            # --- Выходная папка ---
            $lblOutFolder_unpack = New-Object Label; $lblOutFolder_unpack.Location = "9, $outputSectionTopY"; $lblOutFolder_unpack.Text = 'Выходная папка'; $lblOutFolder_unpack.Font = $script:boldFont; $lblOutFolder_unpack.AutoSize = $true

            $script:txtOutFolder_unpack = New-Object TextBox; $script:txtOutFolder_unpack.Location = "9, $($outputSectionTopY + 26)";
            $script:txtOutFolder_unpack.Size = [Size]::new([int]($lbWidth - 90), 20);
            $script:txtOutFolder_unpack.Font = $script:regularFont; $script:txtOutFolder_unpack.BackColor = [ColorTranslator]::FromHtml('#333333')
            $script:txtOutFolder_unpack.ForeColor = [Color]::White
            $script:txtOutFolder_unpack.BorderStyle = [BorderStyle]::FixedSingle
            $script:txtOutFolder_unpack.AllowDrop = $true
            $script:txtOutFolder_unpack.Text = $script:defaultOutPaths['Unpack']
            $script:txtOutFolder_unpack.Add_TextChanged({ Request-SaveSettings }); Add-CtrlA-Handler $script:txtOutFolder_unpack
            $script:txtOutFolder_unpack.Add_DragEnter({ if ($_.Data.GetData("FileDrop") | % { (Get-Item -LiteralPath $_).PSIsContainer }) { $_.Effect = 'Copy' } else { $_.Effect = 'None' } })
            $script:txtOutFolder_unpack.Add_DragDrop({ if (($path=$_.Data.GetData("FileDrop")[0]) -and (Get-Item -LiteralPath $path).PSIsContainer) { $this.Text = $path } })
            $btnBrowseFolder_unpack = New-Object Button; $btnBrowseFolder_unpack.Text = 'Обзор'; $btnBrowseFolder_unpack.Size = "85, 25"; $btnBrowseFolder_unpack.Location = [Point]::new([int]($script:txtOutFolder_unpack.Right + 5), $outputSectionTopY + 26)
            $btnBrowseFolder_unpack.Font = $script:boldFont; $btnBrowseFolder_unpack.BackColor = [ColorTranslator]::FromHtml('#3E3E42'); $btnBrowseFolder_unpack.ForeColor = 'White'; $btnBrowseFolder_unpack.FlatStyle = [FlatStyle]::Flat; $btnBrowseFolder_unpack.FlatAppearance.BorderSize = 1; $btnBrowseFolder_unpack.FlatAppearance.BorderColor = [ColorTranslator]::FromHtml('#6A6A70'); $btnBrowseFolder_unpack.FlatAppearance.MouseOverBackColor = [ColorTranslator]::FromHtml('#4E4E52'); $btnBrowseFolder_unpack.FlatAppearance.MouseDownBackColor = [ColorTranslator]::FromHtml('#28282C')
            $btnBrowseFolder_unpack.Add_Click({ if ($selectedFolder = YEFolder) { $script:txtOutFolder_unpack.Text = $selectedFolder } })
            $gb.Controls.AddRange(@($lblCombined, $ltbxCombined, $lblLoose, $ltbxLoose, $lblOutFolder_unpack, $script:txtOutFolder_unpack, $btnBrowseFolder_unpack))
        }
        elseif ($currentButtonName -eq 'Pack') {
            $outputControlsHeight = 165; $outputControlsTopY = $gb.ClientSize.Height - $outputControlsHeight - 10
            $currentY = 21; $labelToListboxSpacing = 19; $groupSpacing = 8
            $availableHeight = $outputControlsTopY - $currentY - 30
            $listHeight = [int](($availableHeight - ($labelToListboxSpacing * 2) - $groupSpacing) / 2)
            
            # --- Поле 1: Папка игры (упаковка в NSP) ---
            $lblPackGame = New-Object Label; $lblPackGame.Location = "9, $currentY"; $lblPackGame.Name = "label_Pack"; $lblPackGame.Text = 'Папка в игру'; $lblPackGame.Font = $script:boldFont; $lblPackGame.AutoSize = 1
            $currentY += $labelToListboxSpacing
            $ltbxPackGame = New-Object ListBox; $ltbxPackGame.Name = "listBox_Pack_0"; $ltbxPackGame.Location = "9, $currentY"
            $ltbxPackGame.Size = [Size]::new($lbWidth, $listHeight); $ltbxPackGame.AllowDrop = 1; $ltbxPackGame.BackColor = $script:listBoxOriginalBackColor
            $ltbxPackGame.ForeColor = 'White'; $ltbxPackGame.BorderStyle = [BorderStyle]::FixedSingle; $ltbxPackGame.Font = $script:regularFont
            $ltbxPackGame.DisplayMember = 'Name'; $ltbxPackGame.HorizontalScrollbar = 1; $ltbxPackGame.IntegralHeight = 0; $ltbxPackGame.SelectionMode = 3
            $ltbxPackGame.Add_DragEnter($dragEnterHandler); $ltbxPackGame.Add_DragLeave($dragLeaveHandler)
            $ltbxPackGame.Add_DragDrop({ $this.BackColor = $script:listBoxOriginalBackColor; YEDDHandler_Standard })
            $ltbxPackGame.Add_MouseUP({YELBCHandler}); $ltbxPackGame.Add_KeyDown($YELBKeyDownHandler)
            $currentY = $ltbxPackGame.Bottom + $groupSpacing

            # --- Поле 2: NCA (папка для упаковки в .nca) ---
            $lblPackNca = New-Object Label; $lblPackNca.Location = "9, $currentY"; $lblPackNca.Name = "label_Pack_Nca"; $lblPackNca.Text = 'Папка в .nca'; $lblPackNca.Font = $script:boldFont; $lblPackNca.AutoSize = 1
            $currentY += $labelToListboxSpacing
            $script:ltbxPackNca = New-Object ListBox; $script:ltbxPackNca.Name = "listBox_Pack_Nca"; $script:ltbxPackNca.Location = "9, $currentY"
            $script:ltbxPackNca.Size = [Size]::new($lbWidth, $listHeight); $script:ltbxPackNca.AllowDrop = 1; $script:ltbxPackNca.BackColor = $script:listBoxOriginalBackColor
            $script:ltbxPackNca.ForeColor = 'White'; $script:ltbxPackNca.BorderStyle = [BorderStyle]::FixedSingle; $script:ltbxPackNca.Font = $script:regularFont
            $script:ltbxPackNca.DisplayMember = 'Name'; $script:ltbxPackNca.HorizontalScrollbar = 1; $script:ltbxPackNca.IntegralHeight = 0; $script:ltbxPackNca.SelectionMode = 3
            $script:ltbxPackNca.Add_DragEnter($dragEnterHandler); $script:ltbxPackNca.Add_DragLeave($dragLeaveHandler)
            $script:ltbxPackNca.Add_DragDrop({
                param($s, $e)
                $this.BackColor = $script:listBoxOriginalBackColor
                $allDroppedPaths = $e.Data.GetData('FileDrop')
                $allDroppedPaths | ForEach-Object {
                    $item = Get-Item -LiteralPath $_
                    if ($item.PSIsContainer) { $s.Items.Add($item) }
                }
            }); $script:ltbxPackNca.Add_MouseUP({YELBCHandler}); $script:ltbxPackNca.Add_KeyDown($YELBKeyDownHandler)

            $gb.Controls.AddRange(@($lblPackGame, $ltbxPackGame, $lblPackNca, $script:ltbxPackNca))
            $outputControlsYOffset = $outputControlsTopY - 320
            $script:outputControls[$currentButtonName] = Create-OutputControls $gb $lbWidth $script:nsz_f $outputControlsYOffset
            $script:outputControls[$currentButtonName].TxtOutFolder.Text = $script:defaultOutPaths['Pack']
        }
        elseif ($currentButtonName -eq 'Convert') {
            $outputControlsHeight = 165; $outputControlsTopY = $gb.ClientSize.Height - $outputControlsHeight - 10; $outputControlsYOffset = $outputControlsTopY - 320
            $listTopMargin = 40; $labelTopMargin = 21; $listHeight = $outputControlsTopY - $listTopMargin - 10
            $lbl = New-Object Label; $lbl.Location = '9,' + $labelTopMargin; $lbl.Name = "label_Convert"; $lbl.Text = 'NSP, XCI, NSZ, XCZ'; $lbl.Font = $script:boldFont; $lbl.AutoSize = 1
            $ltbx = New-Object ListBox; $ltbx.Name = "listBox_Convert_0"; $ltbx.Location = '9,' + $listTopMargin; $ltbx.Size = [Size]::new($lbWidth, [int]$listHeight); $ltbx.AllowDrop = 1; $ltbx.BackColor = $script:listBoxOriginalBackColor;
            $ltbx.ForeColor = 'White'; $ltbx.BorderStyle = [BorderStyle]::FixedSingle; $ltbx.Font = $script:regularFont;
            $ltbx.DisplayMember = 'Name'; $ltbx.HorizontalScrollbar = 1; $ltbx.IntegralHeight = 0; $ltbx.SelectionMode = 3
            $ltbx.Add_DragEnter($dragEnterHandler); $ltbx.Add_DragLeave($dragLeaveHandler); $ltbx.Add_DragDrop({$this.BackColor = $script:listBoxOriginalBackColor; YEDDHandler_Convert});
            $ltbx.Add_MouseUP({YELBCHandler}); $ltbx.Add_KeyDown($YELBKeyDownHandler)
            $gb.Controls.AddRange(@($lbl, $ltbx))
            $script:outputControls[$currentButtonName] = Create-OutputControls $gb $lbWidth $script:nsz_f $outputControlsYOffset
            $script:outputControls[$currentButtonName].TxtOutFolder.Text = $script:defaultOutPaths['Convert']
        }
        elseif ($currentButtonName -eq 'Multi') {
            $outputControlsHeight = 165; $outputControlsTopY = $gb.ClientSize.Height - $outputControlsHeight - 10; $outputControlsYOffset = $outputControlsTopY - 320
            $listTopMargin = 40; $labelTopMargin = 21; $listHeight = $outputControlsTopY - $listTopMargin - 10
            $lbl = New-Object Label; $lbl.Location = '9,' + $labelTopMargin; $lbl.Name = "label_Multi_Combined"; $lbl.Text = 'Файлы и папки (Базовая игра, Файл обновления, Дополнения (DLC), RomFS, ExeFS)';
            $lbl.Font = $script:boldFont; $lbl.AutoSize = 1
            $ltbx = New-Object ListBox; $ltbx.Name = "listBox_Multi_Combined"; $ltbx.Location = '9,' + $listTopMargin; $ltbx.Size = [Size]::new($lbWidth, [int]$listHeight); $ltbx.AllowDrop = 1; $ltbx.BackColor = $script:listBoxOriginalBackColor;
            $ltbx.ForeColor = 'White';
            $ltbx.BorderStyle = [BorderStyle]::FixedSingle; $ltbx.Font = $script:regularFont; $ltbx.DisplayMember = 'DisplayString'; $ltbx.HorizontalScrollbar = 1; $ltbx.IntegralHeight = 0; $ltbx.SelectionMode = 3
            $ltbx.Add_DragEnter($dragEnterHandler); $ltbx.Add_DragLeave($dragLeaveHandler);
            $ltbx.Add_DragDrop({$this.BackColor = $script:listBoxOriginalBackColor; YEDDHandler_Advanced -listBox $this});
            $ltbx.Add_MouseUP({YELBCHandler});
            $ltbx.Add_KeyDown($YELBKeyDownHandler)

            # Удаление по правой кнопке мыши — только выделенные элементы
            $ltbx.Add_MouseClick({
                param($sender, $e)
                if ($e.Button -eq [System.Windows.Forms.MouseButtons]::Right) {
                    $lb = $sender
                    if ($lb.SelectedItems.Count -eq 0) { return }
                    $itemsToRemove = @($lb.SelectedItems)
                    foreach ($item in $itemsToRemove) {
                        Write-Log "Удалён элемент: $($item.DisplayString)"
                        $lb.Items.Remove($item)
                    }
                }
            })

            $gb.Controls.AddRange(@($lbl, $ltbx))
            $script:outputControls[$currentButtonName] = Create-OutputControls $gb $lbWidth $script:nsz_f $outputControlsYOffset
            $script:outputControls[$currentButtonName].TxtOutFolder.Text = $script:defaultOutPaths['Multi']
        }
        elseif ($currentButtonName -eq 'System') {
            Create-SystemFilesTab $gb
        }
        elseif ($currentButtonName -eq 'Settings') {
            # === ВКЛАДКА НАСТРОЕК ===
            $settingsY = 30
            $settingsLabelWidth = 200
            $settingsControlX = 295
            
            # --- Заголовок ---
            $lblSettingsTitle = New-Object Label
            $lblSettingsTitle.Text = "Параметры обработки"
            $lblSettingsTitle.Font = New-Object Font('Segoe UI Semibold', 14, [FontStyle]::Bold)
            $lblSettingsTitle.ForeColor = [Color]::FromArgb(255, 255, 200, 100)
            $lblSettingsTitle.AutoSize = $true
            $lblSettingsTitle.Location = "15, $settingsY"
            $settingsY += 45
            
            # --- Уменьшение размера XCI ---
            $script:cbXciTrim = New-Object CheckBox
            $script:cbXciTrim.Text = "Уменьшение размера XCI (Trim)"
            $script:cbXciTrim.Font = $script:boldFont
            $script:cbXciTrim.AutoSize = $true
            $script:cbXciTrim.Location = "15, $settingsY"
            $script:cbXciTrim.Enabled = $false
            $script:cbXciTrim.ForeColor = [Color]::Gray
            $script:cbXciTrim.Add_CheckedChanged({ Request-SaveSettings })
            $settingsY += 40
            
            # --- Комплексное выходное имя по папкам ---
            $script:cbComplexFolders = New-Object CheckBox
            $script:cbComplexFolders.Text = "Комплексное выходное имя файла по папкам"
            $script:cbComplexFolders.Font = $script:boldFont
            $script:cbComplexFolders.AutoSize = $true
            $script:cbComplexFolders.Location = "15, $settingsY"
            $script:cbComplexFolders.ForeColor = [Color]::White
            $script:cbComplexFolders.Add_CheckedChanged({ Request-SaveSettings })
            $settingsY += 40
            
            # --- Разделение для FAT32 ---
            $script:cbFat32Split = New-Object CheckBox
            $script:cbFat32Split.Text = "Разделять файлы >4GB (для FAT32)"
            $script:cbFat32Split.Font = $script:boldFont
            $script:cbFat32Split.AutoSize = $true
            $script:cbFat32Split.Location = "15, $settingsY"
            $script:cbFat32Split.ForeColor = [Color]::White
            $script:cbFat32Split.Add_CheckedChanged({ Request-SaveSettings })
            $settingsY += 40
            
            # --- Пересборка файлов в Создании мульти-контента ---
            $script:cbForceMultiRebuild = New-Object CheckBox
            $script:cbForceMultiRebuild.Text = "Пересборка файлов в Создании мульти-контента"
            $script:cbForceMultiRebuild.Font = $script:boldFont
            $script:cbForceMultiRebuild.AutoSize = $true
            $script:cbForceMultiRebuild.Location = "15, $settingsY"
            $script:cbForceMultiRebuild.ForeColor = [Color]::White
            $script:cbForceMultiRebuild.Add_CheckedChanged({ Request-SaveSettings })
            $settingsY += 40
            
            # --- Понижение версии (KG) ---
            $script:lblKeyGen = New-Object Label
            $script:lblKeyGen.Text = "Понижение версии (KeyGeneration):"
            $script:lblKeyGen.Font = $script:boldFont
            $script:lblKeyGen.AutoSize = $true
            $script:lblKeyGen.Location = "15, $settingsY"
            
            $script:numKeyGen = New-Object NumericUpDown
            $script:numKeyGen.Minimum = 17
            $script:numKeyGen.Maximum = 22
            $script:numKeyGen.Value = 19
            $script:numKeyGen.Location = "$settingsControlX, $($settingsY - 2)"
            $script:numKeyGen.Size = '60, 24'
            $script:numKeyGen.Font = $script:regularFont
            $script:numKeyGen.BackColor = [ColorTranslator]::FromHtml('#3E3E42')
            $script:numKeyGen.ForeColor = [Color]::White
            $script:numKeyGen.BorderStyle = 'None'
            $script:numKeyGen.TextAlign = 'Center'
            $script:numKeyGen.Add_ValueChanged({ Request-SaveSettings })
            $settingsY += 40
            
            # --- Используемые ядра ---
            $lblCores = New-Object Label
            $lblCores.Text = "Используемые ядра CPU:"
            $lblCores.Font = $script:boldFont
            $lblCores.AutoSize = $true
            $lblCores.Location = "15, $settingsY"
            
            $script:numUsedCores = New-Object NumericUpDown
            $script:numUsedCores.Minimum = 1
            $script:numUsedCores.Maximum = $script:MaxCores
            $script:numUsedCores.Value = $script:DefaultCores
            $script:numUsedCores.Location = "$settingsControlX, $($settingsY - 2)"
            $script:numUsedCores.Size = '60, 24'
            $script:numUsedCores.Font = $script:regularFont
            $script:numUsedCores.BackColor = [ColorTranslator]::FromHtml('#3E3E42')
            $script:numUsedCores.ForeColor = [Color]::White
            $script:numUsedCores.BorderStyle = 'None'
            $script:numUsedCores.TextAlign = 'Center'
            $script:numUsedCores.Add_ValueChanged({ Request-SaveSettings })
            $settingsY += 40
            
            # --- Одновременные задачи ---
            $lblConcurrent = New-Object Label
            $lblConcurrent.Text = "Одновременные задачи:"
            $lblConcurrent.Font = $script:boldFont
            $lblConcurrent.AutoSize = $true
            $lblConcurrent.Location = "15, $settingsY"
            
            $script:numConcurrentTasks = New-Object NumericUpDown
            $script:numConcurrentTasks.Minimum = 1
            $script:numConcurrentTasks.Maximum = [Math]::Min(16, $script:MaxCores)
            $script:numConcurrentTasks.Value = 3
            $script:numConcurrentTasks.Location = "$settingsControlX, $($settingsY - 2)"
            $script:numConcurrentTasks.Size = '60, 24'
            $script:numConcurrentTasks.Font = $script:regularFont
            $script:numConcurrentTasks.BackColor = [ColorTranslator]::FromHtml('#3E3E42')
            $script:numConcurrentTasks.ForeColor = [Color]::White
            $script:numConcurrentTasks.BorderStyle = 'None'
            $script:numConcurrentTasks.TextAlign = 'Center'
            $script:numConcurrentTasks.Add_ValueChanged({ Request-SaveSettings })
            $settingsY += 40
            
            # --- Уровень сжатия ---
            $lblCompression = New-Object Label
            $lblCompression.Text = "Уровень сжатия (NSZ):"
            $lblCompression.Font = $script:boldFont
            $lblCompression.AutoSize = $true
            $lblCompression.Location = "15, $settingsY"
            
            $script:tbCompression = New-Object NumericUpDown
            $script:tbCompression.Minimum = 1
            $script:tbCompression.Maximum = 22
            $script:tbCompression.Value = 22
            $script:tbCompression.Location = "$settingsControlX, $($settingsY - 2)"
            $script:tbCompression.Size = '60, 24'
            $script:tbCompression.Font = $script:regularFont
            $script:tbCompression.BackColor = [ColorTranslator]::FromHtml('#3E3E42')
            $script:tbCompression.ForeColor = [Color]::White
            $script:tbCompression.BorderStyle = 'None'
            $script:tbCompression.TextAlign = 'Center'
            $script:tbCompression.Add_ValueChanged({ Request-SaveSettings })
            $settingsY += 50
            
            # --- Информационный блок ---
            $lblInfo = New-Object Label
            $lblInfo.Text = "Настройки сохраняются автоматически"
            $lblInfo.Font = $script:smallFont
            $lblInfo.ForeColor = [Color]::Gray
            $lblInfo.AutoSize = $true
            $lblInfo.Location = "15, $settingsY"
            
            $gb.Controls.AddRange(@(
                $lblSettingsTitle,
                $script:cbXciTrim,
                $script:cbFat32Split,
                $script:cbComplexFolders,
                $script:cbForceMultiRebuild,
                $script:lblKeyGen, $script:numKeyGen,
                $lblCores, $script:numUsedCores,
                $lblConcurrent, $script:numConcurrentTasks,
                $lblCompression, $script:tbCompression,
                $lblInfo
            ))
        }
        elseif ($currentButtonName -eq 'History') {
            Create-HistoryTab $gb
        }
    }

    ($script:f.Controls.Find("NavButton_Multi", $true)[0]).BackColor = [ColorTranslator]::FromHtml('#007ACC')
    
    # === КНОПКИ ДЕЙСТВИЙ В НАВИГАЦИИ (настройки перенесены в вкладку "Настройки") ===
    # Кнопки расположены над ссылками внизу панели
    $currentNavY = $navGroupBox.ClientSize.Height - 160

    $btnToTasks = New-Object Button; $btnToTasks.Location = "8, $currentNavY"; $btnToTasks.Size = '200, 30'; $btnToTasks.Text = 'В задачи'; $btnToTasks.Font = $script:boldFont;
    $btnToTasks.BackColor = [Color]::FromArgb(255, 25, 25, 112); $btnToTasks.ForeColor = [Color]::White; $btnToTasks.FlatStyle = [FlatStyle]::Flat;
    $btnToTasks.FlatAppearance.BorderSize = 1; $btnToTasks.FlatAppearance.BorderColor = [ColorTranslator]::FromHtml('#6A6A70');
    $btnToTasks.Add_MouseEnter({ $this.BackColor = [Color]::FromArgb(255, 70, 70, 180) });
    $btnToTasks.Add_MouseLeave({ $this.BackColor = [Color]::FromArgb(255, 25, 25, 112) })
    $btnToTasks.Add_Click({ Add-ActiveTabTasksToQueue | Out-Null; if (-not $script:isTaskPanelVisible) { $script:taskGrid.ClearSelection() }; if ($script:taskGrid.SelectedRows.Count -gt 0) { $script:taskGrid.ClearSelection() } })
    $currentNavY += 35
    $script:startButtonState = 'Normal'
    $bs = New-Object Button; $bs.Location = "8, $currentNavY"; $bs.Size = '200, 30'; $bs.Text = 'Начать всё'; $bs.Font = $script:boldFont; $bs.Visible = 1;
    $bs.FlatStyle = [FlatStyle]::Flat; $bs.FlatAppearance.BorderSize = 0
    $bs.Add_Paint({ param($s,$e)
        $c1,$c2=switch($script:startButtonState){'Hover'{[Color]::FromArgb(255,140,234,164);[Color]::FromArgb(255,96,195,100)}'Click'{[Color]::FromArgb(255,100,204,124);[Color]::FromArgb(255,56,155,60)}default{[Color]::FromArgb(255,120,224,144);[Color]::FromArgb(255,76,175,80)}}
        $brush=New-Object LinearGradientBrush($s.ClientRectangle,$c1,$c2,90); $e.Graphics.FillRectangle($brush,$s.ClientRectangle)
        $pen=New-Object Pen([ColorTranslator]::FromHtml('#6A6A70'), 1); $e.Graphics.DrawRectangle($pen, 0, 0, ($s.ClientRectangle.Width-1), ($s.ClientRectangle.Height-1)); $pen.Dispose()
        $txtFmt=New-Object StringFormat; $txtFmt.Alignment='Center'; $txtFmt.LineAlignment='Center'
        $txtBrush=New-Object SolidBrush($s.ForeColor); $rectF=New-Object RectangleF($s.ClientRectangle.X,$s.ClientRectangle.Y,$s.ClientRectangle.Width,$s.ClientRectangle.Height)
        $e.Graphics.DrawString($s.Text,$s.Font,$txtBrush,$rectF,$txtFmt); $brush.Dispose(); $txtBrush.Dispose(); $txtFmt.Dispose()
    })
    $bs.Add_MouseEnter({ $script:startButtonState = 'Hover'; $bs.Invalidate() }); $bs.Add_MouseLeave({ $script:startButtonState = 'Normal'; $bs.Invalidate() }); $bs.Add_MouseDown({ $script:startButtonState = 'Click'; $bs.Invalidate() });
    $bs.Add_MouseUp({ $script:startButtonState = 'Hover'; $bs.Invalidate() })
    $bs.Add_Click({
        try {
            $script:globalProgressVal = 0.0
            $script:globalProgressText = "Ожидание..."
            if ($script:globalProgressPanel -and $script:globalProgressPanel.IsHandleCreated) {
                $script:globalProgressPanel.Invalidate()
            }
            Add-ActiveTabTasksToQueue | Out-Null; if (-not $script:isTaskPanelVisible) { $script:taskGrid.ClearSelection() }; if ($script:taskGrid.SelectedRows.Count -gt 0) { $script:taskGrid.ClearSelection() }
            Write-Log "Запуск обработки очереди задач..."; foreach ($row in $script:taskGrid.Rows) { if ($row.Cells['Статус'].Value -eq 'Ожидает...') { $row.Cells['Статус'].Value = 'Ожидание' } }; ProcessTaskQueue
        } catch { $errDetails = $_ | Format-List * -Force | Out-String; Write-Log "Критическая ошибка при запуске задач: $($_.Exception.Message) `n$errDetails" -Type 'ERROR';
            YEmsg "Критическая ошибка при запуске задач:`n$($_.Exception.Message)" "OK" $script:f }
    })
    $currentNavY += 35
    $btnToggleTasks = New-Object Button; $btnToggleTasks.Size = '200, 30';
    $btnToggleTasks.Location = "8, $currentNavY"; $btnToggleTasks.Font = $script:boldFont; $btnToggleTasks.Text = '↔ Свернуть';
    $btnToggleTasks.BackColor = [Color]::DarkOrange; $btnToggleTasks.ForeColor = [Color]::White;
    $btnToggleTasks.FlatStyle = [FlatStyle]::Flat;
    $btnToggleTasks.FlatAppearance.BorderSize = 1; $btnToggleTasks.FlatAppearance.BorderColor = [ColorTranslator]::FromHtml('#6A6A70')
    $originalLinkLabel = New-Object LinkLabel; $originalFullText = "Оригинал YanuExt: andrey4556"; $originalLink1Text = "YanuExt";
    $originalLink2Text = "andrey4556";
    $originalLinkLabel.Text = $originalFullText; $originalLinkLabel.Font = $script:boldFont; $originalLinkLabel.ForeColor = [Color]::White;
    $originalLinkLabel.LinkColor = [Color]::DodgerBlue; $originalLinkLabel.ActiveLinkColor = [Color]::Tomato;
    $originalLinkLabel.AutoSize = $true;
    $originalLinkLabel.Location = [Point]::new(2, $navGroupBox.ClientSize.Height - 45)
    $originalLinkLabel.Links.Add($originalFullText.IndexOf($originalLink1Text), $originalLink1Text.Length, "https://github.com/vvvooopy/yanuext"); $originalLinkLabel.Links.Add($originalFullText.IndexOf($originalLink2Text), $originalLink2Text.Length, "https://4pda.to/forum/index.php?showuser=9246779");
    $originalLinkLabel.Add_LinkClicked({param($s, $e) Start-Process $e.Link.LinkData })
    $modLabel = New-Object LinkLabel; $fullText = 'Мод YanuExt: ReiKatari'; $linkText = 'ReiKatari';
    $startIndex = $fullText.IndexOf($linkText);
    $modLabel.Text = $fullText; $modLabel.Font = $script:boldFont; $modLabel.LinkArea = [LinkArea]::new($startIndex, $linkText.Length); $modLabel.LinkColor = [Color]::Red; $modLabel.ForeColor = [Color]::White;
    $modLabel.Location = [Point]::new(2, $navGroupBox.ClientSize.Height - 25); $modLabel.AutoSize = $true
    $modLabel.Add_LinkClicked({ Start-Process "https://4pda.to/forum/index.php?showuser=7365134" })

    $navGroupBox.Controls.AddRange(@($bs, $modLabel, $btnToTasks, $originalLinkLabel, $btnToggleTasks))

    $script:taskGrid = New-Object DataGridView;
    $script:taskGrid.Location = '6, 30'; $script:taskGrid.Size = [Size]::new(($taskGroupBox.Width - 12), ($taskGroupBox.Height - 36)); $script:taskGrid.Anchor = 'Top, Bottom, Left, Right';
    $script:taskGrid.Name = 'taskGrid'; $script:taskGrid.AllowUserToAddRows = $false; $script:taskGrid.AllowUserToDeleteRows = $false; $script:taskGrid.AllowUserToResizeRows = $false; $script:taskGrid.AllowUserToOrderColumns = $true; $script:taskGrid.RowHeadersVisible = $false;
    $script:taskGrid.MultiSelect = $true; $script:taskGrid.SelectionMode = [DataGridViewSelectionMode]::FullRowSelect; $script:taskGrid.EnableHeadersVisualStyles = $false; $script:taskGrid.BorderStyle = [BorderStyle]::None; $script:taskGrid.ColumnHeadersBorderStyle = [DataGridViewHeaderBorderStyle]::Single; $script:taskGrid.CellBorderStyle = [DataGridViewCellBorderStyle]::Single; $script:taskGrid.GridColor = [ColorTranslator]::FromHtml('#6A6A70');
    $script:taskGrid.BackgroundColor = [ColorTranslator]::FromHtml('#1E1E1E'); $script:taskGrid.Font = $script:smallFont; $script:taskGrid.ShowCellToolTips = $true; $script:taskGrid.RowTemplate.Height = 28
    $script:taskGrid.Add_SelectionChanged({ Update-LogView });
    $script:taskGrid.Add_ColumnWidthChanged({ Request-SaveSettings })
    $headerStyle = New-Object DataGridViewCellStyle; $headerStyle.BackColor = [ColorTranslator]::FromHtml('#3E3E42'); $headerStyle.ForeColor = [Color]::White; $headerStyle.Font = New-Object Font('Segoe UI', 9, [FontStyle]::Bold);
    $headerStyle.Alignment = 'MiddleCenter'
    $script:taskGrid.ColumnHeadersDefaultCellStyle = $headerStyle; $script:taskGrid.ColumnHeadersHeight = 30;
    $script:taskGrid.ColumnHeadersHeightSizeMode = 'DisableResizing'
    $cellStyle = New-Object DataGridViewCellStyle; $cellStyle.BackColor = [ColorTranslator]::FromHtml('#252528'); $cellStyle.ForeColor = [Color]::White; $cellStyle.SelectionBackColor = [ColorTranslator]::FromHtml('#005A9E');
    $cellStyle.SelectionForeColor = [Color]::White; $cellStyle.Padding = New-Object Padding(4, 0, 4, 0)
    $script:taskGrid.DefaultCellStyle = $cellStyle
    $altCellStyle = New-Object DataGridViewCellStyle; $altCellStyle.BackColor = [ColorTranslator]::fromhtml('#2D2D30'); $altCellStyle.Padding = New-Object Padding(4, 0, 4, 0)
    $script:taskGrid.AlternatingRowsDefaultCellStyle = $altCellStyle
    $colHeaders = [ordered]@{ 'Задача'=100; 'Обработка'=200; 'Нач. формат'=100; 'Кон. формат'=100; 'Нач. размер'=110; 'Кон. размер'=110; 'Разница'=80;
    'Уровень сжатия'=135; 'Кол-во файлов'=120; 'Статус'=200; 'Выполнение'=120 }
    foreach ($h in $colHeaders.GetEnumerator()) {
        if ($h.Name -eq 'Выполнение') {
            $c = New-Object YanuExt.CustomControls.DataGridViewPercentageColumn
        } else {
            $c = New-Object DataGridViewTextBoxColumn
        }
        $c.Name = $h.Name;
        $c.HeaderText = $h.Name; $c.Width = $h.Value; $c.ReadOnly = $true; $c.SortMode = 'NotSortable'
        if ($h.Name -in 'Нач. размер', 'Кон. размер', 'Разница', 'Уровень сжатия', 'Кол-во файлов', 'Нач. формат', 'Кон. формат', 'Выполнение') { $c.DefaultCellStyle.Alignment = 'MiddleCenter' } else { $c.DefaultCellStyle.Alignment = 'MiddleLeft' }
        if($h.Name -eq 'Выполнение') { $c.Resizable = [DataGridViewTriState]::False }
        $script:taskGrid.Columns.Add($c) |
        Out-Null
    }
    
    $taskGridHeaderMenu = New-Object ContextMenuStrip;
    $taskGridHeaderMenu.Renderer = New-Object YanuExt.CustomControls.CustomMenuRenderer
    $taskGridHeaderMenu.Add_Opening({ param($s, $e) ; foreach ($item in $s.Items) { if ($item.Tag -is [DataGridViewColumn]) { $item.Checked = $item.Tag.Visible } } })
    $script:taskGrid.Columns |
    ForEach-Object { $column = $_; $item = $taskGridHeaderMenu.Items.Add($column.HeaderText); $item.Name = $column.HeaderText; $item.CheckOnClick = $true; $item.Checked = $column.Visible; $item.Tag = $column;
    $item.Add_Click({ $this.Tag.Visible = $this.Checked; Save-Settings }) }
    $script:taskGrid.Add_CellMouseClick({ param($s, $e) ; if ($e.RowIndex -eq -1 -and $e.Button -eq 'Right') { $taskGridHeaderMenu.Show([Cursor]::Position) } })
    $taskGridRowMenu = New-Object ContextMenuStrip;
    $taskGridRowMenu.Renderer = New-Object YanuExt.CustomControls.CustomMenuRenderer
    $startItem = $taskGridRowMenu.Items.Add('Начать'); $cancelItem = $taskGridRowMenu.Items.Add('Отменить'); $openFolderItem = $taskGridRowMenu.Items.Add('Открыть папку'); $taskGridRowMenu.Items.Add((New-Object ToolStripSeparator)) |
    Out-Null; $deleteItem = $taskGridRowMenu.Items.Add('Удалить')
    $startItem.Add_Click({ if ($script:taskGrid.SelectedRows.Count -gt 0) { $row = $script:taskGrid.SelectedRows[0]; if ($row.Cells['Статус'].Value -is [string] -and $row.Cells['Статус'].Value -eq 'Ожидает...') { $row.Cells['Статус'].Value = 'Ожидание'; Write-Log "Задача '$($row.Cells['Задача'].Value)' для '$($row.Cells['Обработка'].Value)' вручную поставлена в очередь." -TaskID $row.Tag.TaskID; ProcessTaskQueue } } })
    $cancelItem.Add_Click({ if ($script:taskGrid.SelectedRows.Count -gt 0) { $row = $script:taskGrid.SelectedRows[0]; $procId = $row.Tag.ProcessID; if ($procId -and $script:runningTasks.ContainsKey($procId)) { Write-Log "Отмена задачи '$($row.Cells['Задача'].Value)'..." -TaskID $row.Tag.TaskID -Type 'WARN'; Stop-ProcessTree -ProcessId $procId } } })
    $openFolderItem.Add_Click({ if ($script:taskGrid.SelectedRows.Count -gt 0) { $row = $script:taskGrid.SelectedRows[0]; $path = $row.Tag.FinalPath; if ([string]::IsNullOrWhiteSpace($path) -or -not (Test-Path -LiteralPath $path)) {
    Write-Log "Не удалось открыть папку: путь не существует. '$path'" -Type 'ERROR'; return }; if((Get-Item -LiteralPath $path).PSIsContainer) { Invoke-Item -LiteralPath $path } else { Invoke-Item -LiteralPath (Split-Path -LiteralPath $path) }; Write-Log "Открытие: '$path'" } })
    
    $deleteItem.Add_Click({
        if ($script:taskGrid.SelectedRows.Count -gt 0) {
            $rowsToRemove = @($script:taskGrid.SelectedRows)
            foreach($row in $rowsToRemove){
                if ($row.Tag.ProcessID -and $script:runningTasks.ContainsKey($row.Tag.ProcessID)) {
                    Stop-ProcessTree -ProcessId $row.Tag.ProcessID
                    $taskInfo = $null
                    $script:runningTasks.TryRemove($row.Tag.ProcessID, [ref]$taskInfo) | Out-Null
                }
                Write-Log "Удаление задачи '$($row.Cells['Задача'].Value)' для '$($row.Cells['Обработка'].Value)'." -TaskID $row.Tag.TaskID -Type 'WARN'
                $script:taskGrid.Rows.Remove($row)
            }
        }
    })
    
    $script:taskGrid.Add_MouseDown({ param($s, $e);
    if ($e.Button -eq 'Left') { $hitTest = $s.HitTest($e.X, $e.Y); if ($hitTest.Type -eq [DataGridViewHitTestType]::Nowhere) { if ($s.SelectedRows.Count -gt 0) { $s.ClearSelection() } } } elseif ($e.Button -eq 'Right') { $hitTest = $s.HitTest($e.X, $e.Y);
    if ($hitTest.RowIndex -ge 0) { if (-not $s.Rows[$hitTest.RowIndex].Selected) { $s.ClearSelection(); $s.Rows[$hitTest.RowIndex].Selected = $true };
    if ($s.SelectedRows.Count -gt 0) { $firstSelectedRow = $s.SelectedRows[0]; $status = $firstSelectedRow.Cells['Статус'].Value; $startItem.Enabled = ($status -is [string] -and $status -eq 'Ожидает...');
    $cancelItem.Enabled = ($firstSelectedRow.Tag.ProcessID -and $script:runningTasks.ContainsKey($firstSelectedRow.Tag.ProcessID)); $openFolderItem.Enabled = ($status -is [string] -and $status -eq 'Готово' -and -not [string]::IsNullOrWhiteSpace($firstSelectedRow.Tag.FinalPath) -and (Test-Path -LiteralPath $firstSelectedRow.Tag.FinalPath));
    $deleteItem.Enabled = $true; $taskGridRowMenu.Show($s, $e.Location) } } } })
    
    $script:taskGrid.Add_KeyDown({ param($s, $e);
        if ($e.KeyCode -eq [Keys]::Delete) {
            if ($s.SelectedRows.Count -gt 0) {
                $rowsToRemove = @($s.SelectedRows)
                foreach($row in $rowsToRemove){
                    if ($row.Tag.ProcessID -and $script:runningTasks.ContainsKey($row.Tag.ProcessID)) {
                        Stop-ProcessTree -ProcessId $row.Tag.ProcessID
                        $taskInfo = $null
                        $script:runningTasks.TryRemove($row.Tag.ProcessID, [ref]$taskInfo) | Out-Null
                    }
                    Write-Log "Удаление задачи '$($row.Cells['Задача'].Value)' для '$($row.Cells['Обработка'].Value)'." -TaskID $row.Tag.TaskID -Type 'WARN'
                    $s.Rows.Remove($row)
                }
            }
        }
    })
    
    $taskGroupBox.Controls.Add($script:taskGrid)
    $logBox =
    New-Object RichTextBox; $logBox.BackColor = [ColorTranslator]::FromHtml('#1E1E1E'); $logBox.ForeColor = [Color]::White; $logBox.Font = $script:regularFont; $logBox.ReadOnly = $true; $logBox.Location = '6, 22';
    $logBox.Size = [Size]::new(($logGroupBox.Width - 12), ($logGroupBox.Height - 28)); $logBox.Anchor = 'Top, Bottom, Left, Right';
    $logBox.WordWrap = $true
    Add-CtrlA-Handler $logBox; $script:logBox = $logBox;
    $logGroupBox.Controls.Add($logBox)

    $btnToggleTasks.Add_Click({
        $script:isTaskPanelVisible = -not $script:isTaskPanelVisible

        $activeTab = $script:f.Controls | Where-Object { $_.Name -like 'TabPanel_*' -and $_.Visible } | Select-Object -First 1
        $filesPanelRight = if ($activeTab) { $activeTab.Right } else { 816 }
        if ($script:isTaskPanelVisible) {
            # РАЗВЕРНУТЫЙ ВИД
            $taskGroupBox.Visible = $true
            $logGroupBox.Visible = $true

            $logGroupBox.Location = [Point]::new($navGroupBox.Left, $navGroupBox.Bottom + 8)
            $logGroupBox.Size = [Size]::new(($taskGroupBox.Right - $navGroupBox.Left), 194)

            $script:globalGroupBox.Location = [Point]::new($navGroupBox.Left, $logGroupBox.Bottom + 6)
            $script:globalGroupBox.Size = [Size]::new(($taskGroupBox.Right - $navGroupBox.Left), 54)
            $script:globalProgressPanel.Width = $script:globalGroupBox.Width - 12

            $script:f.Width = 1724
            $script:f.Height = 932
            $this.Text = "↔ Свернуть"
        } else {
            # СВЕРНУТЫЙ ВИД - Прогресс-бар поднимается к блокам меню/файлов
            $taskGroupBox.Visible = $false
            $logGroupBox.Visible = $false

            # Размещаем прогресс-бар с небольшим отступом под блоком файлов
            $activeTabBottom = if ($activeTab) { $activeTab.Bottom } else { $navGroupBox.Bottom }
            $script:globalGroupBox.Location = [Point]::new($navGroupBox.Left, $activeTabBottom + 8)
            $newProgressWidth = ($filesPanelRight - $navGroupBox.Left)
            $script:globalGroupBox.Size = [Size]::new($newProgressWidth, 54)
            $script:globalProgressPanel.Width = $script:globalGroupBox.Width - 12

            # Ширина окна = правая граница панели файлов + отступ (8px)
            $newWidth = $filesPanelRight + 8
            # Высота = Нижняя граница прогресс-бара + отступ снизу (8px)
            $newHeight = $script:globalGroupBox.Bottom + 8

            $script:f.Size = [Size]::new($newWidth, $newHeight)
            $this.Text = "↔ Развернуть"
        }
        if (-not $script:suppressSave) { Request-SaveSettings }
    })

    # 1. Determine Initial State
    $isExpanded = $true
    if ($script:settings -and ($script:settings.PSObject.Properties.Name -contains 'TaskPanelVisible')) {
        try {
            $isExpanded = [System.Convert]::ToBoolean($script:settings.TaskPanelVisible)
        } catch {
            $isExpanded = $true
        }
    } else {
        $isExpanded = $false
    }
    $script:isTaskPanelVisible = $isExpanded
    # 2. Restore Standard Settings
    
    if ($script:settings) {
        if ($script:settings.WindowX -and $script:settings.WindowY) {
            try { $x=[int]$script:settings.WindowX;
            $y=[int]$script:settings.WindowY; $script:f.StartPosition='Manual'; $script:f.Location=[Point]::new([int]$x, [int]$y) } catch { Write-Log "Не удалось восстановить положение окна."
            -Type 'ERROR' }
        }
    }
    # 3. Apply State Logic
    if ($isExpanded) {
        # EXPANDED STATE
        $taskGroupBox.Visible = $true
        $logGroupBox.Visible = $true
        $btnToggleTasks.Text = "↔ Свернуть"
        $logGroupBox.Location = [Point]::new($navGroupBox.Left, $navGroupBox.Bottom + 8)
        $logGroupBox.Size = [Size]::new(($taskGroupBox.Right - $navGroupBox.Left), 194)

        $script:globalGroupBox.Location = [Point]::new($navGroupBox.Left, $logGroupBox.Bottom + 6)
        $script:globalGroupBox.Size = [Size]::new(($taskGroupBox.Right - $navGroupBox.Left), 54)
        $script:globalProgressPanel.Width = $script:globalGroupBox.Width - 12
        if ($script:settings.WindowWidth -and $script:settings.WindowHeight) {
             try { $script:f.Size = [Size]::new([int]$script:settings.WindowWidth, [int]$script:settings.WindowHeight) } catch { $script:f.Size = '1724, 932' }
        } else {
            $script:f.Size = '1724, 932'
        }
    } else {
        # COLLAPSED STATE - Прогресс-бар поднимается к блокам меню/файлов
        $taskGroupBox.Visible = $false
        $logGroupBox.Visible = $false
        $btnToggleTasks.Text = "↔ Развернуть"
        $activeTab = $script:f.Controls |
        Where-Object { $_.Name -like 'TabPanel_*' -and $_.Visible } | Select-Object -First 1
        $filesPanelRight = if ($activeTab) { $activeTab.Right } else { 816 }
        # Размещаем прогресс-бар с небольшим отступом под блоком файлов
        $activeTabBottom = if ($activeTab) { $activeTab.Bottom } else { $navGroupBox.Bottom }
        $script:globalGroupBox.Location = [Point]::new($navGroupBox.Left, $activeTabBottom + 8)
        $newProgressWidth = ($filesPanelRight - $navGroupBox.Left)
        $script:globalGroupBox.Size = [Size]::new($newProgressWidth, 54)
        $script:globalProgressPanel.Width = $script:globalGroupBox.Width - 12
        $newWidth = $filesPanelRight + 8
        $newHeight = $script:globalGroupBox.Bottom
        + 8

        $script:f.Size = [Size]::new($newWidth, $newHeight)
    }
    if ($script:settings) {
        if ($script:settings.WindowState) { try { if ($script:settings.WindowState -ne 'Minimized') { $script:f.WindowState = $script:settings.WindowState } } catch { Write-Log "Не удалось восстановить состояние окна."
        -Type 'ERROR' } }
        foreach ($key in $script:outputControls.Keys) { $settingName = "LastOutPath_$key";
        $savedPath = $script:settings.$settingName; if (-not [string]::IsNullOrWhiteSpace($savedPath)) { $isOldDefault = ($savedPath.Contains("\out\")) -and -not ($savedPath.StartsWith($script:cd));
        if (([Directory]::Exists($savedPath)) -and (-not $isOldDefault)) { $script:outputControls[$key].TxtOutFolder.Text = $savedPath } } }
        if ($script:txtOutFolder_unpack) { $savedPath = $script:settings.LastOutPath_Unpack;
        if (-not [string]::IsNullOrWhiteSpace($savedPath)) { $isOldDefault = ($savedPath.Contains("\out\")) -and -not ($savedPath.StartsWith($script:cd));
        if (([Directory]::Exists($savedPath)) -and (-not $isOldDefault)) { $script:txtOutFolder_unpack.Text = $savedPath } } }
        if ($script:settings.CompressionLevel) { try { $level = [int]$script:settings.CompressionLevel;
        if ($level -ge $script:tbCompression.Minimum -and $level -le $script:tbCompression.Maximum) { $script:tbCompression.Value = $level } } catch { Write-Log "Не удалось восстановить уровень сжатия."
        -Type 'WARN' } }
        if ($script:settings.UsedCores) { try { $cores = [int]$script:settings.UsedCores;
        if ($cores -ge $script:numUsedCores.Minimum -and $cores -le $script:numUsedCores.Maximum) { $script:numUsedCores.Value = $cores } } catch { Write-Log "Не удалось восстановить количество используемых ядер."
        -Type 'WARN' } }
        if ($script:settings.ConcurrentTasks) { try { $tasks = [int]$script:settings.ConcurrentTasks;
        if ($tasks -ge $script:numConcurrentTasks.Minimum -and $tasks -le $script:numConcurrentTasks.Maximum) { $script:numConcurrentTasks.Value = $tasks } } catch { Write-Log "Не удалось восстановить количество одновременных задач."
        -Type 'WARN' } }
        if ($script:settings.KeyGeneration) { try { $kg = [int]$script:settings.KeyGeneration;
        if ($kg -ge $script:numKeyGen.Minimum -and $kg -le $script:numKeyGen.Maximum) { $script:numKeyGen.Value = $kg } } catch { Write-Log "Не удалось восстановить KeyGeneration." -Type 'WARN' } }
        if ($script:settings.UnpackStitched) { try { if ($script:cbUnpackStitched) { $script:cbUnpackStitched.Checked = [System.Convert]::ToBoolean($script:settings.UnpackStitched) } } catch { Write-Log "Не удалось восстановить UnpackStitched." -Type 'WARN' } }
        if ($script:settings.Fat32Split) { try { if ($script:cbFat32Split) { $script:cbFat32Split.Checked = [System.Convert]::ToBoolean($script:settings.Fat32Split) } } catch { Write-Log "Не удалось восстановить Fat32Split." -Type 'WARN' } }
        if ($script:settings.ComplexFolders) { try { if ($script:cbComplexFolders) { $script:cbComplexFolders.Checked = [System.Convert]::ToBoolean($script:settings.ComplexFolders) } } catch { Write-Log "Не удалось восстановить ComplexFolders." -Type 'WARN' } }
        if ($script:settings.ForceMultiRebuild) { try { if ($script:cbForceMultiRebuild) { $script:cbForceMultiRebuild.Checked = [System.Convert]::ToBoolean($script:settings.ForceMultiRebuild) } } catch { Write-Log "Не удалось восстановить ForceMultiRebuild." -Type 'WARN' } }
        $columnKeyMap = @{ 'Задача'='Task';'Обработка'='Processing';'Нач.
        формат'='StartFormat';'Кон. формат'='EndFormat'; 'Нач. размер'='StartSize';'Кон. размер'='EndSize';'Разница'='Difference'; 'Уровень сжатия'='CompressionLevelGrid';'Кол-во файлов'='FileCount';'Статус'='Status';'Выполнение'='Execution' }
        foreach($col in $script:taskGrid.Columns) { if ($columnKeyMap.ContainsKey($col.HeaderText)) { $englishKey = $columnKeyMap[$col.HeaderText];
        $widthProp = "TaskGrid_Col_${englishKey}_Width"; $visibleProp = "TaskGrid_Col_${englishKey}_Visible"; if ($script:settings.PSObject.Properties[$widthProp]) { try { $col.Width = [int]$script:settings.$widthProp } catch { Write-Log "Не удалось восстановить ширину столбца '$($col.HeaderText)'." -Type 'DEBUG' } };
        if ($script:settings.PSObject.Properties[$visibleProp]) { try { $col.Visible = [System.Convert]::ToBoolean($script:settings.$visibleProp); $hdrItem = $taskGridHeaderMenu.Items[$col.HeaderText]; if ($hdrItem) { $hdrItem.Checked = $col.Visible } } catch { Write-Log "Не удалось восстановить видимость столбца '$($col.HeaderText)'." -Type 'DEBUG' } } } }
    }

    # Назначаем подсказки
    if ($script:tt) {
        $script:tt.SetToolTip($script:cbXciTrim, "Удаляет пустое неиспользуемое пространство из XCI-образов для уменьшения размера.`n(Не рекомендуется, так как ломает проверку целостности оригинального картриджа)")
        
        $script:tt.SetToolTip($script:cbComplexFolders, "Автоматически формирует имя выходного файла из названия папки игры и перетянутой подпапки.`nУдобно для массовой обработки модов и DLC, разложенных по версиям.")
        $script:tt.SetToolTip($script:cbForceMultiRebuild, "Принудительно пересобирает базовую игру (полный цикл распаковки-упаковки), даже если моды не добавлены.`nПолезно для чистой реструктуризации файла, но занимает значительно больше времени.")
        
        $keyGenWarning = "Опция полезна для обхода требований новых прошивок (позволяет запускать игры на старом системном ПО).`nВНИМАНИЕ: Не поддерживается в режиме 'Обновление' (UpdateRepack) во избежание повреждения патчей.`nРаботает при 'Сборке Мульти-контента' и 'Конвертации'."
        $script:tt.SetToolTip($script:lblKeyGen, $keyGenWarning)
        $script:tt.SetToolTip($script:numKeyGen, $keyGenWarning)
        
        $script:tt.SetToolTip($lblCores, "Позволяет ускорить процессы сжатия и конвертации, задействуя несколько ядер процессора одновременно.")
        $script:tt.SetToolTip($script:numUsedCores, "Максимальное количество физических потоков CPU, выделяемых под одну задачу.`nОптимально - значение по умолчанию.")
        
        $script:tt.SetToolTip($lblConcurrent, "Количество игр, обрабатываемых одновременно.`nВНИМАНИЕ: Потребляет огромное количество оперативной памяти (ОЗУ)!")
        $script:tt.SetToolTip($script:numConcurrentTasks, "Число параллельных задач. Увеличивайте только при наличии 32+ ГБ ОЗУ и обработке мелких файлов.")
        
        $script:tt.SetToolTip($lblCompression, "Уровень алгоритма сжатия файлов в формат NSZ/XCZ.`nЧем больше значение, тем меньше размер файла, но процесс займёт гораздо больше времени.")
        $script:tt.SetToolTip($script:tbCompression, "Значение по умолчанию: 18. Диапазон: 1 (быстрое) - 22 (максимально плотное сжатие).")
        
        if ($lblInfo) { $script:tt.SetToolTip($lblInfo, "Любые изменения в этом окне применяются мгновенно и сохраняются.") }
        if ($btnToTasks) { $script:tt.SetToolTip($btnToTasks, "Отправить все выбранные файлы из текущей вкладки в общую 'Очередь задач' (справа).") }
        if ($bs) { $script:tt.SetToolTip($bs, "Запустить поочередное выполнение всех задач из Очереди.") }
        if ($btnCancel) { $script:tt.SetToolTip($btnCancel, "Прервать выполнение текущей задачи и остановить очередь.") }
        
        # Подсказки на навигационные кнопки (если они созданы)
        foreach ($navBtn in $script:f.Controls.Find("navGroupBox", $true)[0].Controls) {
            if ($navBtn -is [System.Windows.Forms.Button]) {
                switch ($navBtn.Tag) {
                    'Multi' { $script:tt.SetToolTip($navBtn, "Основной режим: позволяет собрать Базу, Обновление, DLC и Моды в один файл.") }
                    'Update' { $script:tt.SetToolTip($navBtn, "Позволяет вшить Обновление/DLC в базовую игру без пересоздания основной структуры.") }
                    'Unpack' { $script:tt.SetToolTip($navBtn, "Извлечение файлов из .nsp/.xci/.nsz. Извлеченный romfs можно редактировать.") }
                    'Pack' { $script:tt.SetToolTip($navBtn, "Упаковка распакованной папки обратно в контейнер .nsp или .nca.") }
                    'Convert' { $script:tt.SetToolTip($navBtn, "Быстрое конвертирование файлов между форматами и их сжатие (NSZ/XCZ).") }
                    'System' { $script:tt.SetToolTip($navBtn, "Встроенные утилиты: патчинг NACP и другие системные инструменты.") }
                    'Settings' { $script:tt.SetToolTip($navBtn, "Глобальные настройки обработки для всех режимов программы.") }
                    'History' { $script:tt.SetToolTip($navBtn, "История выполненных задач и проверка обработанных файлов.") }
                }
            }
        }
    }

    $script:suppressSave = $false
    [void]$script:f.ShowDialog()
}
#====================================================================================
#  БЛОК ОСНОВНЫХ ФУНКЦИЙ
#====================================================================================
function Add-ActiveTabTasksToQueue {
    try {
        $activeTab = $script:f.Controls | Where-Object { ($_.GetType().Name -eq 'GroupBox') -and ($_.Visible) -and ($_.Name -like 'TabPanel_*') } | Select-Object -First 1
        if (-not $activeTab) { return }
        $tabName = $activeTab.Name.Split('_')[1]

        $outputConf = $script:outputControls[$tabName]
        $capturedOutName = ""
        if ($outputConf -and $outputConf.TxtOutFile) {
            $capturedOutName = $outputConf.TxtOutFile.Text
            $outputConf.TxtOutFile.Text = ""
        }
       
        $capturedCores = $script:numUsedCores.Value

        if ($tabName -in 'Update', 'Unpack', 'Multi') {
            $isUnpack = $tabName -eq 'Unpack'
            $listBoxName = switch($tabName) {
                'Update' { "listBox_Update_0" }
                'Unpack' { "listBox_Unpack_Combined" }
                'Multi'  { "listBox_Multi_Combined" }
            }
            $foundControls = $activeTab.Controls.Find($listBoxName, $true)
            if ($foundControls.Count -eq 0) { return }
            $ltbx = $foundControls[0]

            if ($ltbx.Items.Count -eq 0 -and (-not $isUnpack -or ($activeTab.Controls.Find('listBox_Unpack_Loose', $true)[0].Items.Count -eq 0))) { return }

            # Группировка элементов. Для Multi используется BaseID, для остальных GameGroupKey.
            # Если GameGroupKey нет (Multi), используем BaseID или группируем все вместе, если это плоский список.
            $groupedTasks = $ltbx.Items | Where-Object { 
                # Исключаем разделители
                $type = if ($_.PSObject.Properties['Type']) { $_.Type } else { $_.FileType }
                $type -ne 'SEPARATOR' 
            } | Group-Object -Property { 
                if ($_.PSObject.Properties['GameGroupKey']) { $_.GameGroupKey } 
                elseif ($_.PSObject.Properties['BaseID']) { $_.BaseID }
                else { 'LegacyGroup' }
            }
            
            foreach ($taskGroup in $groupedTasks) {
                $baseFile = $null; $updateFile = $null;
                $dlcList = [System.Collections.Generic.List[string]]::new(); 
                $unlockerList = [System.Collections.Generic.List[string]]::new(); 
                $romfsList = [System.Collections.Generic.List[string]]::new(); 
                $exefsPath = $null; $allFilesForStitching = [System.Collections.Generic.List[string]]::new()
                
                foreach ($item in $taskGroup.Group) {
                    # --- УНИФИКАЦИЯ ДАННЫХ ---
                    # Все вкладки (Update, Unpack, Multi) используют одинаковую структуру:
                    # объект хранится в .Item, тип в .Type (устанавливается в Rebuild-ListBoxItems)
                    
                    $fileObj = $item.Item
                    $typeStr = $item.Type
                    
                    if (-not $fileObj) { continue }

                    switch ($typeStr) {
                        # Базовая игра
                        'GAME' { $baseFile = $fileObj; $allFilesForStitching.Add($fileObj.FullName) }
                        'ИГРА' { $baseFile = $fileObj; $allFilesForStitching.Add($fileObj.FullName) }
                        
                        # Обновление
                        'UPDATE'     { $updateFile = $fileObj; $allFilesForStitching.Add($fileObj.FullName) }
                        'ОБНОВЛЕНИЕ' { $updateFile = $fileObj; $allFilesForStitching.Add($fileObj.FullName) }
                        
                        # Unlocker
                        'UNLOCKER' { $unlockerList.Add($fileObj.FullName); $allFilesForStitching.Add($fileObj.FullName) }
                        
                        # DLC
                        'DLC'   { $dlcList.Add($fileObj.FullName); $allFilesForStitching.Add($fileObj.FullName) }
                        '[DLC]' { $dlcList.Add($fileObj.FullName); $allFilesForStitching.Add($fileObj.FullName) }
                        
                        # Mods (RomFS / ExeFS)
                        'ROMFS' { $romfsList.Add($fileObj.FullName) }
                        'EXEFS' { $exefsPath = $fileObj.FullName }
                        'ROMFS/EXEFS' {
                            # В режиме Multi тип общий, определяем по имени папки
                            if ($fileObj.Name -eq 'romfs') { $romfsList.Add($fileObj.FullName) }
                            elseif ($fileObj.Name -eq 'exefs') { $exefsPath = $fileObj.FullName }
                        }
                    }
                }

                if (-not $baseFile) { 
                    # ИСПРАВЛЕНИЕ: Если база не найдена по типу, ищем среди всех файлов группы
                    # Сначала проверяем allFilesForStitching
                    if ($allFilesForStitching.Count -gt 0) {
                        # Берём самый большой файл как базу
                        $largestFile = $allFilesForStitching | ForEach-Object { 
                            if (Test-Path -LiteralPath $_) { Get-Item -LiteralPath $_ } 
                        } | Sort-Object Length -Descending | Select-Object -First 1
                        
                        if ($largestFile) {
                            $baseFile = $largestFile
                            Write-Log "Базовый файл определён автоматически: $($baseFile.Name)" -Type 'INFO'
                        }
                    }
                    
                    # Если всё ещё нет базы - ищем любой NSP/XCI файл в группе
                    if (-not $baseFile) {
                        foreach ($item in $taskGroup.Group) {
                            $fObj = if ($item.PSObject.Properties['Item']) { $item.Item } else { $item }
                            if ($fObj -and $fObj -is [System.IO.FileInfo]) {
                                $ext = $fObj.Extension.ToLower()
                                if ($ext -in '.nsp', '.xci', '.nsz', '.xcz') {
                                    $baseFile = $fObj
                                    $allFilesForStitching.Add($fObj.FullName)
                                    Write-Log "Базовый файл найден в группе: $($baseFile.Name)" -Type 'INFO'
                                    break
                                }
                            }
                        }
                    }
                    
                    if (-not $baseFile) {
                        Write-Log "Пропуск группы $($taskGroup.Name): не найден базовый файл (ИГРА/GAME)." -Type "WARN"
                        continue 
                    }
                }
                
                $tmpOutDir = if($isUnpack) { $script:txtOutFolder_unpack.Text } else { $outputConf.TxtOutFolder.Text }
                $tmpOutFormat = if($isUnpack) { 'FOLDER' } else { $outputConf.SelectedFormat }
                if (Check-DuplicateTask -OutDir $tmpOutDir -OutName $capturedOutName -OutFormat $tmpOutFormat -BaseFile $baseFile) {
                    continue
                }
                
                $shortID = [guid]::NewGuid().ToString("N").Substring(0, 8)
                $taskData = [ordered]@{ TaskID = $shortID; Cores = $capturedCores }
                
                if (-not [string]::IsNullOrWhiteSpace($capturedOutName)) {
                    [System.Threading.Monitor]::Enter($script:outNamesFileLock)
                    try {
                        $nameRecord = [pscustomobject]@{ TaskID = $taskData.TaskID; OutName = $capturedOutName }
                        $nameRecord | Export-Csv -Path $script:outNamesFile -Append -NoTypeInformation -Encoding UTF8
                    } finally { [System.Threading.Monitor]::Exit($script:outNamesFileLock) }
                }

                $areModsPresent = ($romfsList.Count -gt 0) -or (-not [string]::IsNullOrEmpty($exefsPath))
                $isUnlockerPresent = $unlockerList.Count -gt 0

                if ($isUnpack) { $taskData.TaskType = 'Unpack' }
                elseif ($isUnlockerPresent) { $taskData.TaskType = 'DirectStitch' } 
                elseif ($areModsPresent) { $taskData.TaskType = 'BuildMulti' }
                elseif ($tabName -eq 'Update') { $taskData.TaskType = 'UpdateRepack' }
                elseif ($tabName -eq 'Multi') { $taskData.TaskType = 'BuildMulti' }

                $taskData.Base = $baseFile.FullName
                $taskData.Updates = if($updateFile) { @($updateFile.FullName) } else { $null }
                $taskData.RomfsPaths = if($romfsList.Count -gt 0) {$romfsList} else {$null}
                $taskData.ExefsPath = $exefsPath
                
                if ($isUnlockerPresent) {
                    Write-Log "В задачу включены файлы UNLOCKER ($($unlockerList.Count) шт.). Они будут вшиты." -TaskID $taskData.TaskID -Type 'SUCCESS'
                }

                $taskData.DLCs = if($dlcList.Count -gt 0) {$dlcList} else {$null}
                $taskData.Unlockers = if($unlockerList.Count -gt 0) {$unlockerList} else {$null}

                $taskData.FilesForStitching = $allFilesForStitching
                
                $taskData.OutDir = if($isUnpack) { $script:txtOutFolder_unpack.Text } else { $outputConf.TxtOutFolder.Text };
                $taskData.OutFormat = if(-not $isUnpack) { $outputConf.SelectedFormat } else { $null };
                $taskData.OutName = $capturedOutName
                $taskData.CompressionLevel = $script:tbCompression.Value
                $taskData.KeyGeneration = $script:numKeyGen.Value
                $taskData.XciTrim = $script:cbXciTrim.Checked
                $taskData.Fat32Split = $script:cbFat32Split.Checked
                $taskData.UnpackStitched = if($script:cbUnpackStitched){$script:cbUnpackStitched.Checked}else{$false}
                $taskData.ForceMultiRebuild = if($script:cbForceMultiRebuild){$script:cbForceMultiRebuild.Checked}else{$false}
                
                # Доп. поле для внутреннего имени (если нужно для мульти)
                if ($tabName -eq 'Multi') {
                    # Пытаемся сформировать красивое имя, если выходное не задано явно
                    if ([string]::IsNullOrWhiteSpace($capturedOutName)) {
                        # Извлекаем имя базового файла и санитизируем для squirrel.exe
                        $rawName = [System.IO.Path]::GetFileNameWithoutExtension($baseFile.Name)
                        # Извлекаем только название игры (до первой квадратной скобки или круглой скобки)
                        $cleanName = ($rawName -split '\[')[0].Trim()
                        $cleanName = ($cleanName -split '\(')[0].Trim()
                        # Удаляем оставшиеся спецсимволы, которые могут сломать аргументы
                        $cleanName = $cleanName -replace '["\[\](){}]', ''
                        $taskData.InternalName = if ([string]::IsNullOrWhiteSpace($cleanName)) { $null } else { $cleanName }
                    }
                }

                $initialSize = [long]0;
                $initialSize += Get-ItemSize $baseFile;
                if($updateFile) { $initialSize += Get-ItemSize $updateFile };
                $dlcList.ForEach({ if($_) { $initialSize += Get-ItemSize -itemInfo $_ } });
                $unlockerList.ForEach({ if($_) { $initialSize += Get-ItemSize -itemInfo $_ } });
                $romfsList.ForEach({ if($_) { $initialSize += Get-ItemSize -itemInfo $_ } });
                if($exefsPath) { $initialSize += Get-ItemSize -itemInfo $exefsPath };
                $taskData.InitialSize = $initialSize
                
                $fileCount = 1 + $(if($updateFile){1}else{0}) + $dlcList.Count + $unlockerList.Count + $romfsList.Count + $(if($exefsPath){1}else{0});
                $startFormatString = '';
                
                # ИСПРАВЛЕНО: Правильное определение начального формата
                # Собираем все расширения файлов из группы
                $allExtensions = [System.Collections.Generic.List[string]]::new()
                
                foreach ($groupItem in $taskGroup.Group) {
                    $fObj = if ($groupItem.PSObject.Properties['Item']) { $groupItem.Item } else { $groupItem.FileInfo }
                    if ($fObj -and $fObj -is [System.IO.FileInfo] -and $fObj.Extension) {
                        $ext = $fObj.Extension.TrimStart('.').ToUpper()
                        if ($ext -in @('NSP', 'NSZ', 'XCI', 'XCZ')) {
                            $allExtensions.Add($ext)
                        }
                    }
                }
                
                # Определяем формат на основе уникальных расширений
                if ($allExtensions.Count -gt 0) {
                    $uniqueExts = @($allExtensions | Select-Object -Unique)
                    if ($uniqueExts.Count -eq 1) {
                        # Все файлы одного формата
                        $startFormatString = $uniqueExts[0]
                    } else {
                        # Разные форматы
                        $startFormatString = 'MULTI'
                    }
                }
                
                # Логируем определенный формат
                Write-Log "Определен начальный формат: $startFormatString (файлов: $($allExtensions.Count))" -TaskID $taskData.TaskID
                
                $taskDisplayName = switch($taskData.TaskType){
                    'UpdateRepack' { 'Обновление' }
                    'BuildMulti' { 'Сборка мульти' }
                    'DirectStitch' { 'Сборка с Unlocker' }
                    'Unpack' { "Распаковка" }
                    default { $taskData.TaskType }
                }
                $outFormatDisplay = if($isUnpack) { 'FOLDER' } else { $taskData.OutFormat }

                $rowIndex = $script:taskGrid.Rows.Add($taskDisplayName, "$($baseFile.Name) +...", $startFormatString, $outFormatDisplay, (Format-FileSize $initialSize), '-', '-', $script:tbCompression.Value, $fileCount, 'Ожидает...', $null)
                $script:taskGrid.Rows[$rowIndex].Tag = $taskData;
                $script:taskGrid.Rows[$rowIndex].Cells['Обработка'].ToolTipText = $taskData.Base; 
                Write-Log "Задача '$($taskData.TaskType)' добавлена." -TaskID $taskData.TaskID
            }

            if ($isUnpack) {
                $ltbxLoose = $activeTab.Controls.Find('listBox_Unpack_Loose', $true)[0]
                if ($ltbxLoose.Items.Count -gt 0) {
                    $looseFiles = @($ltbxLoose.Items | ForEach-Object { $_.FullName });
                    $totalSize = 0; $looseFiles.ForEach({ $totalSize += Get-ItemSize -itemInfo $_ })
                    
                    $shortID = [guid]::NewGuid().ToString("N").Substring(0, 8)
                    $taskData = [ordered]@{ TaskType = 'Unpack'; LooseFiles = $looseFiles; OutDir = $script:txtOutFolder_unpack.Text; TaskID = $shortID; InitialSize = $totalSize; Cores = $capturedCores }
                    
                    $startFormatString = '';
                    $uniqueExtensions = @($ltbxLoose.Items.Extension.TrimStart('.').ToUpper() | Select-Object -Unique)
                    if ($uniqueExtensions.Count -gt 1) { $startFormatString = 'MULTI' } elseif ($uniqueExtensions.Count -eq 1) { $startFormatString = $uniqueExtensions[0] }

                    $baseItemForNaming = Get-Item -LiteralPath $looseFiles[0]
                    $rowIndex = $script:taskGrid.Rows.Add("Распаковка", "$($baseItemForNaming.Name) + ...", $startFormatString, 'FOLDER', (Format-FileSize $taskData.InitialSize), '-', '-', '-', $looseFiles.Count, 'Ожидает...', $null)
                    $script:taskGrid.Rows[$rowIndex].Tag = $taskData;
                    $script:taskGrid.Rows[$rowIndex].Cells['Обработка'].ToolTipText = ($looseFiles -join "`n")
                    Write-Log "Задача 'Распаковка' (отдельные файлы) добавлена." -TaskID $taskData.TaskID
                }
                
                $ltbx.Items.Clear(); $ltbxLoose.Items.Clear()
            } else {
                $ltbx.Items.Clear()
            }
            Update-MultiGameUIState $ltbx
            Update-ListBoxLabel $ltbx
        } 
        elseif ($tabName -eq 'Pack') {
            $foundControls = $activeTab.Controls.Find("listBox_Pack_0", $true)
            if ($foundControls.Count -eq 0) { return }
            $lb = $foundControls[0]

            if ($lb.Items.Count -gt 0) {
                $useNumbering = $lb.Items.Count -gt 1 -and -not [string]::IsNullOrWhiteSpace($capturedOutName)
                $baseOutName = if($useNumbering){[System.IO.Path]::GetFileNameWithoutExtension($capturedOutName)}else{$null}
                $outExt = if($useNumbering){[System.IO.Path]::GetExtension($capturedOutName)}else{$null}

                for ($i = 0; $i -lt $lb.Items.Count; $i++) {
                    $item = $lb.Items[$i] 
                    $currentOutName = if ($useNumbering) { "${baseOutName}_$($i + 1)${outExt}" } else { $capturedOutName }
                    
                    if (Check-DuplicateTask -OutDir $outputConf.TxtOutFolder.Text -OutName $currentOutName -OutFormat $outputConf.SelectedFormat -BaseFile $item) {
                        continue
                    }
                    
                    $initialSize = Get-ItemSize $item
                    
                    $shortID = [guid]::NewGuid().ToString("N").Substring(0, 8)
                    $taskData = [ordered]@{ 
                        TaskID = $shortID;
                        TaskType = 'Pack';
                        Folder = $item.FullName; 
                        Base = $item.FullName;
                        InitialSize = $initialSize;
                        CompressionLevel = $script:tbCompression.Value; 
                        OutDir = $outputConf.TxtOutFolder.Text;
                        OutFormat = $outputConf.SelectedFormat;
                        Cores = $capturedCores
                        KeyGeneration = $script:numKeyGen.Value;
                        XciTrim = $script:cbXciTrim.Checked
                    }
                    if (-not [string]::IsNullOrWhiteSpace($currentOutName)) {
                        [System.Threading.Monitor]::Enter($script:outNamesFileLock)
                        try {
                            $nameRecord = [pscustomobject]@{ TaskID = $taskData.TaskID; OutName = $currentOutName }
                            $nameRecord | Export-Csv -Path $script:outNamesFile -Append -NoTypeInformation -Encoding UTF8
                        } finally { [System.Threading.Monitor]::Exit($script:outNamesFileLock) }
                    }
                    $rowIndex = $script:taskGrid.Rows.Add('Упаковка', $item.Name, 'FOLDER', $taskData.OutFormat, (Format-FileSize $initialSize), '-', '-', $script:tbCompression.Value, '1', 'Ожидает...', $null)
                    $script:taskGrid.Rows[$rowIndex].Tag = $taskData;
                    $script:taskGrid.Rows[$rowIndex].Cells['Обработка'].ToolTipText = $item.FullName
                    Write-Log "Задача 'Упаковка' для '$($item.Name)' добавлена." -TaskID $taskData.TaskID
                }
                $lb.Items.Clear()
                Update-ListBoxLabel $lb
            }
            
            # === Обработка NCA папок ===
            $lbPackNca = $activeTab.Controls.Find('listBox_Pack_Nca', $true)
            if ($lbPackNca.Count -gt 0 -and $lbPackNca[0].Items.Count -gt 0) {
                $lbNca = $lbPackNca[0]
                for ($i = 0; $i -lt $lbNca.Items.Count; $i++) {
                    $item = $lbNca.Items[$i]
                    
                    if (Check-DuplicateTask -OutDir $outputConf.TxtOutFolder.Text -OutName "" -OutFormat "NCA" -BaseFile $item) {
                        continue
                    }
                    
                    $initialSize = Get-ItemSize $item
                    
                    $shortID = [guid]::NewGuid().ToString("N").Substring(0, 8)
                    $taskData = [ordered]@{ 
                        TaskID = $shortID;
                        TaskType = 'PackNCA';
                        Folder = $item.FullName; 
                        InitialSize = $initialSize;
                        OutDir = $outputConf.TxtOutFolder.Text;
                        Cores = $capturedCores;
                        KeyGeneration = $script:numKeyGen.Value
                    }
                    $rowIndex = $script:taskGrid.Rows.Add('Упаковка NCA', $item.Name, 'FOLDER', 'NCA', (Format-FileSize $initialSize), '-', '-', '-', '1', 'Ожидает...', $null)
                    $script:taskGrid.Rows[$rowIndex].Tag = $taskData;
                    $script:taskGrid.Rows[$rowIndex].Cells['Обработка'].ToolTipText = $item.FullName
                    Write-Log "Задача 'Упаковка NCA' для '$($item.Name)' добавлена." -TaskID $taskData.TaskID
                }
                $lbNca.Items.Clear()
            }
        }
        elseif ($tabName -eq 'Convert') {
            $foundControls = $activeTab.Controls.Find("listBox_Convert_0", $true)
            if ($foundControls.Count -eq 0) { return }
            $lb = $foundControls[0]
            if ($lb.Items.Count -gt 0) {
                $useNumbering = $lb.Items.Count -gt 1 -and -not [string]::IsNullOrWhiteSpace($capturedOutName)
                $baseOutName = if($useNumbering){[System.IO.Path]::GetFileNameWithoutExtension($capturedOutName)}else{$null}
                $outExt = if($useNumbering){[System.IO.Path]::GetExtension($capturedOutName)}else{$null}

                for ($i = 0; $i -lt $lb.Items.Count; $i++) {
                    $item = $lb.Items[$i]
                    $currentOutName = if ($useNumbering) { "${baseOutName}_$($i + 1)${outExt}" } else { $capturedOutName }
                    
                    if (Check-DuplicateTask -OutDir $outputConf.TxtOutFolder.Text -OutName $currentOutName -OutFormat $outputConf.SelectedFormat -BaseFile $item) {
                        continue
                    }
                    
                    $initialSize = Get-ItemSize $item
                    
                    $shortID = [guid]::NewGuid().ToString("N").Substring(0, 8)
                    $taskData = [ordered]@{ 
                        TaskID = $shortID;
                        TaskType = 'Convert';
                        File = $item.FullName; 
                        Base = $item.FullName; 
                        InitialSize = $initialSize; 
                        CompressionLevel = $script:tbCompression.Value; 
                        OutDir = $outputConf.TxtOutFolder.Text;
                        OutFormat = $outputConf.SelectedFormat;
                        Cores = $capturedCores
                        KeyGeneration = $script:numKeyGen.Value;
                        XciTrim = $script:cbXciTrim.Checked
                    }
                    if (-not [string]::IsNullOrWhiteSpace($currentOutName)) {
                        [System.Threading.Monitor]::Enter($script:outNamesFileLock)
                        try {
                            $nameRecord = [pscustomobject]@{ TaskID = $taskData.TaskID; OutName = $currentOutName }
                            $nameRecord | Export-Csv -Path $script:outNamesFile -Append -NoTypeInformation -Encoding UTF8
                        } finally { [System.Threading.Monitor]::Exit($script:outNamesFileLock) }
                    }
                    $sourceFormat = ($item.Extension -replace '\.','').ToUpper()
                    $rowIndex = $script:taskGrid.Rows.Add('Конвертация', $item.Name, $sourceFormat, $taskData.OutFormat, (Format-FileSize $initialSize), '-', '-', $script:tbCompression.Value, '1', 'Ожидает...', $null)
                    $script:taskGrid.Rows[$rowIndex].Tag = $taskData;
                    $script:taskGrid.Rows[$rowIndex].Cells['Обработка'].ToolTipText = $item.FullName
                    Write-Log "Задача 'Конвертация' для '$($item.Name)' добавлена." -TaskID $taskData.TaskID
                }
                $lb.Items.Clear()
                Update-ListBoxLabel $lb
            }
        }
    } catch {
        Write-Log "Критическая ошибка при добавлении задач в очередь: $($_.Exception.Message)`n$($_.ScriptStackTrace)" -Type 'ERROR'
        YEmsg "Произошла критическая ошибка при добавлении задач. Подробности в блоке логов." "OK" $script:f
    }
}

function ProcessTaskQueue {
    $lockObject = $script:taskGrid
    if (-not [System.Threading.Monitor]::TryEnter($lockObject, 100)) {
        return
    }
    
    try {
        $maxConcurrentTasks = $script:numConcurrentTasks.Value
        # Создаем копию массива строк, чтобы не блокировать итератор при модификации
        $rowsArray = @($script:taskGrid.Rows)
        
        # УМНОЕ КОПИРОВАНИЕ: Определяем какие исходные файлы используются несколькими задачами
        $sourceFileCount = @{}
        foreach ($row in $rowsArray) {
            if ($null -eq $row -or $row.IsNewRow) { continue }
            $td = $row.Tag
            if ($td -and $td.Base) {
                $key = $td.Base.ToLower()
                if ($sourceFileCount.ContainsKey($key)) { $sourceFileCount[$key]++ }
                else { $sourceFileCount[$key] = 1 }
            }
        }
        # Устанавливаем флаг NeedIsolation для задач с разделяемыми файлами
        foreach ($row in $rowsArray) {
            if ($null -eq $row -or $row.IsNewRow) { continue }
            $td = $row.Tag
            if ($td -and $td.Base) {
                $key = $td.Base.ToLower()
                $td.NeedIsolation = ($sourceFileCount[$key] -ge 2)
            }
        }
        
        foreach ($taskRow in $rowsArray) {
            # Проверка лимита задач
            if ($script:runningTasks.Count -ge $maxConcurrentTasks) { break }
            
            # Проверка валидности строки
            if ($null -eq $taskRow -or $taskRow.IsNewRow) { continue }
            if ($taskRow.Cells['Статус'].Value -notin 'Ожидание', 'Ожидает...') { continue }
            
            $taskData = $taskRow.Tag
            
            # ВАЛИДАЦИЯ: Проверка свободного места на диске перед запуском
            try {
                $outDir = if (-not [string]::IsNullOrWhiteSpace($taskData.OutDir)) { $taskData.OutDir } else { $script:odir }
                $outDrive = [System.IO.Path]::GetPathRoot($outDir)
                $tempDrive = [System.IO.Path]::GetPathRoot($script:wdir)
                
                # Оцениваем необходимый размер (исходный файл * 2 для запаса)
                $estimatedSize = [long]0
                if ($taskData.Base -and (Test-Path -LiteralPath $taskData.Base -ErrorAction SilentlyContinue)) {
                    $estimatedSize = (Get-Item -LiteralPath $taskData.Base -ErrorAction SilentlyContinue).Length * 2
                } elseif ($taskData.File -and (Test-Path -LiteralPath $taskData.File -ErrorAction SilentlyContinue)) {
                    $estimatedSize = (Get-Item -LiteralPath $taskData.File -ErrorAction SilentlyContinue).Length * 2
                }
                
                if ($estimatedSize -gt 0) {
                    # Проверяем диск вывода
                    $outDriveInfo = [System.IO.DriveInfo]::new($outDrive)
                    if ($outDriveInfo.AvailableFreeSpace -lt $estimatedSize) {
                        $neededGB = [math]::Round($estimatedSize / 1GB, 2)
                        $freeGB = [math]::Round($outDriveInfo.AvailableFreeSpace / 1GB, 2)
                        Write-Log "ОШИБКА: Недостаточно места на диске $outDrive (нужно ~${neededGB} GB, доступно ${freeGB} GB)." -TaskID $taskData.TaskID -Type 'ERROR'
                        $taskRow.Cells['Статус'].Value = "Ошибка (мало места)"
                        $taskRow.Cells['Выполнение'].Value = $null
                        continue
                    }
                    # Проверяем диск temp (если отличается)
                    if ($tempDrive.ToLower() -ne $outDrive.ToLower()) {
                        $tempDriveInfo = [System.IO.DriveInfo]::new($tempDrive)
                        if ($tempDriveInfo.AvailableFreeSpace -lt $estimatedSize) {
                            $neededGB = [math]::Round($estimatedSize / 1GB, 2)
                            $freeGB = [math]::Round($tempDriveInfo.AvailableFreeSpace / 1GB, 2)
                            Write-Log "ОШИБКА: Недостаточно места на temp-диске $tempDrive (нужно ~${neededGB} GB, доступно ${freeGB} GB)." -TaskID $taskData.TaskID -Type 'ERROR'
                            $taskRow.Cells['Статус'].Value = "Ошибка (мало места)"
                            $taskRow.Cells['Выполнение'].Value = $null
                            continue
                        }
                    }
                }
            } catch {
                Write-Log "Не удалось проверить свободное место: $($_.Exception.Message)" -Type 'WARN'
            }
            
            $taskRow.Cells['Статус'].Value = 'Запуск...'
            $taskRow.Cells['Выполнение'].Value = 0
            
            # СТАБИЛЬНОСТЬ: Задержка между запусками для предотвращения конфликтов
            # Используем флаг для Timer-based планирования, не блокируя UI
            if ($script:runningTasks.Count -gt 0) {
                $delayMs = if ($taskData.TaskType -in 'BuildMulti', 'DirectStitch') { 3000 } else { 1500 }
                $script:_pendingTaskDelay = $true
                $delayTimer = New-Object System.Windows.Forms.Timer
                $delayTimer.Interval = $delayMs
                $delayTimer.Add_Tick({
                    $this.Stop(); $this.Dispose()
                    $script:_pendingTaskDelay = $false
                    ProcessTaskQueue
                })
                $delayTimer.Start()
                return
            }
            
            # Сохраняем оригинальные пути для именования
            if ($taskData.Base) { $taskData.OriginalBase = $taskData.Base } 
            elseif ($taskData.File) { $taskData.OriginalBase = $taskData.File } 
            elseif ($taskData.Folder) { $taskData.OriginalBase = $taskData.Folder } 
            elseif ($taskData.Files) { $taskData.OriginalBase = $taskData.Files[0] } 
            elseif ($taskData.LooseFiles) { $taskData.OriginalBase = $taskData.LooseFiles[0] }
            else { $taskData.OriginalBase = $null }
            
            if ($taskData.Updates) { $taskData.OriginalUpdates = $taskData.Updates } else { $taskData.OriginalUpdates = $null }
            
            $taskData.AppVersion = $script:title

            # Получение красивого имени выхода для отображения в консоли
            $displayOutputName = "Автоматически"
            if (Test-Path -LiteralPath $script:outNamesFile) {
                try {
                    $allNames = Import-Csv $script:outNamesFile -ErrorAction SilentlyContinue
                    $nameRecord = $allNames | Where-Object { $_.TaskID -eq $taskData.TaskID } | Select-Object -First 1
                    if ($nameRecord) { $displayOutputName = $nameRecord.OutName }
                } catch { Write-WorkerLog "Не удалось прочитать файл имён: $($_.Exception.Message)" -Type 'WARN' }
            }
            if ($displayOutputName -eq "Автоматически" -and $taskData.OriginalBase) {
                try { $displayOutputName = [System.IO.Path]::GetFileName($taskData.OriginalBase) } catch { Write-WorkerLog "Не удалось извлечь имя файла из пути" -Type 'WARN' }
            }
            $taskData.DisplayOutputName = $displayOutputName

            # Пути к инструментам
            $taskData.ToolPaths = @{ 
                cd = $script:cd;
                tdir = $script:tdir; ndir = $script:ndir; nbdir = $script:nbdir; 
                wdir = $script:wdir; odir = $script:odir; key = $script:key;
                hactoolnet = "$script:hactoolnet"; yanu_cli_path = "$script:yanu_cli"; 
                nsz_exe = "$script:nsz_exe"; nscb_path = "$script:nscb_bat";
                squirrel_exe = "$script:squirrel_exe"
            }
            # Абсолютный путь к ключам
            if ($taskData.ToolPaths.key -and (Test-Path -LiteralPath $taskData.ToolPaths.key)) { 
                $taskData.ToolPaths.key = [System.IO.Path]::GetFullPath($taskData.ToolPaths.key) 
            }
            
            # Экспорт данных задачи в XML
            $taskDataPath = Join-Path $script:wdir "task-$($taskData.TaskID).xml"
            $taskData | Export-Clixml -Path $taskDataPath
            $workerScriptPath = Join-Path $script:wdir "worker-$($taskData.TaskID).ps1"
            
            # =================================================================================
            # ГЕНЕРАЦИЯ СКРИПТА ВОРКЕРА
            # =================================================================================
            $workerScriptContentTemplate = @'
param( $TaskDataPath )
  ##WORKER_FUNCTIONS_BLOCK##

# ДОБАВЬТЕ ЭТУ СТРОКУ:
Add-Type -AssemblyName System.Windows.Forms | Out-Null

# --- Настройка окна консоли ---
try {
    $taskData = Import-Clixml -Path $TaskDataPath
    $layoutFile = Join-Path $taskData.ToolPaths.cd "ssb.worker.layout"
    $w = 85; $h = 15; 
    if (Test-Path -LiteralPath $layoutFile) { 
        try { 
            $saved = Import-Clixml $layoutFile;
            if ($saved.Width -ge 20 -and $saved.Height -ge 5) { $w = $saved.Width; $h = $saved.Height } 
        } catch {
    Write-WorkerLog "Не удалось восстановить макет окна воркера: $($_.Exception.Message)" -Type 'WARN'
} 
    }
    $Host.UI.RawUI.BackgroundColor = 'Black'; 
    $Host.UI.RawUI.ForegroundColor = 'Gray';
    $bufW = [math]::Max($w + 20, 120); 
    $Host.UI.RawUI.BufferSize = New-Object System.Management.Automation.Host.Size($bufW, 3000); 
    $Host.UI.RawUI.WindowSize = New-Object System.Management.Automation.Host.Size($w, $h);
    Clear-Host
} catch {
    Write-WorkerLog "Не удалось настроить окно консоли воркера: $($_.Exception.Message)" -Type 'WARN'
}

try {
    $ErrorActionPreference = 'Stop'; 
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    
    $toolPaths = $taskData.ToolPaths;
    $logFile = Join-Path $toolPaths.wdir "log-$($taskData.TaskID).log"; 
    $statusFile = Join-Path $toolPaths.wdir "status-$($taskData.TaskID).log";
    $resultFile = Join-Path $toolPaths.wdir "result-$($taskData.TaskID).xml"
    
    $useCores = $taskData.Cores;
    $keyGen = if ($taskData.KeyGeneration) { $taskData.KeyGeneration } else { 21 };
    $kpVal = if ([int]$keyGen -lt 21) { [string]$keyGen } else { "false" }; 
    $isXciTarget = $taskData.OutFormat -and ($taskData.OutFormat.ToString().ToUpper() -eq "XCI");
    $trimVal = if ($taskData.XciTrim -and $isXciTarget) { "true" } else { "false" }

    # Локальные функции логирования
    function Write-WorkerProgress { 
        param($Status, [int]$Percent = -1); 
        [System.IO.File]::WriteAllText($statusFile, "$Percent|$Status")
    }
    function Log-Xci-Status { 
        param($filePath, $isTrimmed) 
        if ($isTrimmed -eq "true" -and (Test-Path -LiteralPath $filePath)) { 
            try { 
                $size = (Get-Item -LiteralPath $filePath).Length;
                $sizeStr = if ($size -gt 1073741824) { "{0:N2} GB" -f ($size / 1GB) } else { "{0:N2} MB" -f ($size / 1MB) };
                Write-WorkerLog "Файл XCI обработан (Trim активен). Итоговый размер: $sizeStr" -Type 'SUCCESS' 
            } catch {
                Write-WorkerLog "Ошибка определения размера XCI: $($_.Exception.Message)" -Type 'WARN'
            } 
        } 
    }
    function Format-DetailedError { 
        param ($errorRecord, $taskDetails);
        $errorMessage = $errorRecord.Exception.Message.Trim(); 
        $suggestion = "Общая ошибка скрипта."; 
        if($errorMessage -like "*yanu-cli*"){$suggestion = "Ошибка утилиты yanu-cli."} 
        elseif($errorMessage -like "*nsz*"){$suggestion = "Ошибка утилиты nsz."} 
        elseif($errorMessage -like "*NSCB*"){$suggestion = "Ошибка утилиты NSCB."};
        $report = @("====== ERROR ======", "MSG: $errorMessage", "SUGGESTION: $suggestion", "TRACE: " + $errorRecord.ToString().Trim());
        return $report -join [System.Environment]::NewLine 
    }
    
    
    
    $result = @{ TaskID = $taskData.TaskID; Status = 'Ошибка'; FinalPath = '-'; InitialSize = 0; FinalSize = $null };
    $tempDir = Join-Path $toolPaths.wdir $taskData.TaskID
    
    try {
        Write-Header -Title "СТАРТ ЗАДАЧИ" -Subtitle $taskData.TaskType -AppVersion $taskData.AppVersion -OutputInfo $taskData.DisplayOutputName
        [void](New-Item -ItemType Directory -Force $tempDir -ErrorAction SilentlyContinue)
        
        # Проверка ключей
        if (-not $toolPaths.key -or -not (Test-Path -LiteralPath $toolPaths.key)) { $toolPaths.key = Join-Path $toolPaths.tdir 'prod.keys' }
        Repair-KeysFile $toolPaths.key
        Repair-KeysFile (Join-Path $toolPaths.ndir 'keys.txt')
        Repair-KeysFile (Join-Path $toolPaths.nbdir 'ztools\keys.txt')
        
        if ([int]$keyGen -lt 21) { Write-WorkerLog "Включено понижение версии (KeyGeneration): $keyGen" -Type "WARN" }
        if ($trimVal -eq "true") { Write-WorkerLog "Включено уменьшение размера XCI (Trimming)" -Type "WARN" }
        
        $result.InitialSize = $taskData.InitialSize;
        $finalFileInTemp = $null
        
        switch ($taskData.TaskType) {
            'UpdateRepack' {
                Write-WorkerLog "Подготовка инструментов" -Type 'STAGE'
                Write-WorkerProgress -Status "Подготовка (NSZ)" -Percent 2;
                $isolatedNsz = Setup-IsolatedToolWithKeys -toolName 'nsz' -tempDir $tempDir -toolPaths $toolPaths
                $convertDir = Join-Path $tempDir 'converted_nsp';
                [void](New-Item -ItemType Directory -Force $convertDir)
                
                $needIso = if ($taskData.NeedIsolation) { $taskData.NeedIsolation } else { $false }
                $baseFileNsp = Convert-To-NspIfNeeded -filePath $taskData.Base -convertDir $convertDir -isolatedNsz $isolatedNsz -useCores $useCores -needIsolation $needIso;
                
                # === ПРОВЕРКА НА СШИТЫЙ ФАЙЛ (UpdateRepack) ===
                $updatesArray = @($taskData.Updates); 
                $hasNewUpdate = $updatesArray.Count -gt 0
                
                if ($hasNewUpdate) {
                    $stitchedInfo = Test-IsStitchedFile -filePath $baseFileNsp -isolatedNsz $isolatedNsz
                    
                    if ($stitchedInfo.IsStitched -and $stitchedInfo.HasUpdate) {
                        Write-WorkerLog "База содержит старое обновление. Извлечение базовой игры для применения нового обновления..." -Type 'WARN'
                        
                        $extractedBase = Extract-BaseFromStitched -stitchedFilePath $baseFileNsp -outputDir $convertDir -isolatedNsz $isolatedNsz -toolPaths $toolPaths -useCores $useCores
                        
                        if ($extractedBase -and (Test-Path -LiteralPath $extractedBase)) {
                            Write-WorkerLog "Старое обновление будет заменено новым" -Type 'SUCCESS'
                            $baseFileNsp = $extractedBase
                        } else {
                            Write-WorkerLog "Не удалось извлечь базу. Продолжаем с оригинальным файлом" -Type 'WARN'
                        }
                    }
                }
                
                $updateFileNsp = $null; 
                if ($updatesArray.Count -gt 0) { 
                    $updateFileNsp = Convert-To-NspIfNeeded -filePath $updatesArray[0] -convertDir $convertDir -isolatedNsz $isolatedNsz -useCores $useCores -needIsolation $needIso
                }
                
                Write-WorkerProgress -Status "Подготовка (yanu-cli)" -Percent 5;
                $unpackDir = Join-Path $tempDir 'unpacked'; 
                [void](New-Item -ItemType Directory -Force $unpackDir); 
                
                # ИЗОЛЯЦИЯ yanu-cli: копируем в temp задачи для избежания конфликтов
                $isolatedYanuDir = Join-Path $tempDir 'isolated_yanu'
                [void](New-Item -ItemType Directory -Force $isolatedYanuDir)
                $isolatedYanuPath = Join-Path $isolatedYanuDir 'yanu-cli.exe'
                Copy-Item -LiteralPath $toolPaths.yanu_cli_path -Destination $isolatedYanuPath -Force
                
                Write-WorkerLog "Распаковка файлов" -Type 'STAGE'
                Write-WorkerProgress -Status "Распаковка" -Percent 10;
                $updateArgUnpack = if ($updateFileNsp) { " --update `"$updateFileNsp`"" } else { "" }; 
                $unpackProcArgs = "unpack --base `"$baseFileNsp`"$updateArgUnpack -o `"$unpackDir`"";
                $unpackProc = @{ Exe = $isolatedYanuPath; Args = "--keyfile `"$($toolPaths.key)`" $unpackProcArgs"; WorkingDir = $tempDir };
                if ((Invoke-Tool $unpackProc "Распаковка") -ne 0) { throw "Ошибка распаковки" }
                
                Write-WorkerLog "Анализ структуры" -Type 'STAGE'
                Write-WorkerProgress -Status "Анализ" -Percent 60;
                if (-not (Test-Path -LiteralPath $toolPaths.hactoolnet)) { throw "hactoolnet.exe не найдена." };
                
                # ИСПРАВЛЕННЫЙ ПОИСК control.nca
                $controlNcaFile = $null
                # 1. Сначала ищем в стандартных местах
                $stdPaths = @(Join-Path $unpackDir "updatedata"; Join-Path $unpackDir "basedata")
                foreach ($p in $stdPaths) {
                    if (Test-Path $p) {
                        $controlNcaFile = (Get-ChildItem -LiteralPath $p -Filter *.nca | ForEach-Object { if (& $toolPaths.hactoolnet "-k" $toolPaths.key $_.FullName | Select-String 'Control' -Quiet) { $_; return } } | Select-Object -First 1)
                        if ($controlNcaFile) { break }
                    }
                }
                # 2. Если не нашли, ищем рекурсивно во всей папке распаковки
                if (-not $controlNcaFile) {
                    Write-WorkerLog "Control.nca не найден в стандартных путях, поиск по всей папке..." -Type 'WARN'
                    $controlNcaFile = (Get-ChildItem -LiteralPath $unpackDir -Filter *.nca -Recurse | ForEach-Object { if (& $toolPaths.hactoolnet "-k" $toolPaths.key $_.FullName | Select-String 'Control' -Quiet) { $_; return } } | Select-Object -First 1)
                }
                
                if (-not $controlNcaFile) { throw "Не найден control.nca. Невозможно продолжить упаковку." }; 
                
                # ИЗМЕНЕНИЕ: Читаем TitleID из NPDM (если есть), так как hacPack требует точного совпадения для Program NCA
                $titleId = $null
                $mainNpdm = $null
                
                # Ищем main.npdm в exefs (рекурсивно)
                $mainNpdmFile = Get-ChildItem -LiteralPath $unpackDir -Filter "main.npdm" -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 1
                $mainNpdm = if ($mainNpdmFile) { $mainNpdmFile.FullName } else { $null }
                
                if ($mainNpdm) {
                     # WORKAROUND: hactoolnet может не переваривать пути с квадратными скобками.
                     # Копируем NPDM во временный файл с простым именем.
                     $msgNpdmTemp = Join-Path $tempDir "temp_read_u.npdm"
                     Copy-Item -LiteralPath $mainNpdm -Destination $msgNpdmTemp -Force

                     try {
                         # Пробуем прочитать NPDM с ключами
                         $npdmOutput = & $toolPaths.hactoolnet "-k" $toolPaths.key "-t" "npdm" "$msgNpdmTemp" 2>&1
                         $npdmOutputStr = $npdmOutput | Out-String
                         
                         # Regex flexible for spaces and case
                         $match = [regex]::Match($npdmOutputStr, '(?i)Program\s*I[Dd]:\s*([0-9a-fA-F]{16})')
                         
                         if ($match.Success) {
                             $titleId = $match.Groups[1].Value.Trim().ToLower()
                             Write-WorkerLog "Использован TitleID из NPDM: $titleId" -Type 'INFO'
                         } else {
                             Write-WorkerLog "Не удалось распарсить Program ID из NPDM." -Type 'WARN'
                         }
                     } catch {
                         Write-WorkerLog "Ошибка при чтении NPDM: $($_.Exception.Message)" -Type 'WARN'
                     }
                     
                     if (Test-Path -LiteralPath $msgNpdmTemp) { Remove-Item -LiteralPath $msgNpdmTemp -Force -ErrorAction SilentlyContinue }
                }
                
                # Если не нашли в NPDM - извлекаем из имени базового файла
                if (-not $titleId -or $titleId -eq '0100000000000000') {
                    # Fallback: Извлекаем TitleID из имени базового файла
                    # Поддерживаемые форматы: [0100...], (0100...), или просто 0100... (16 hex символов)
                    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($taskData.OriginalBase)
                    
                    # Попытка 1: ID в квадратных скобках [0100...]
                    $folderMatch = [regex]::Match($baseName, '\[([0-9a-fA-F]{16})\]')
                    if (-not $folderMatch.Success) {
                        # Попытка 2: ID в круглых скобках (... - 0100...)
                        $folderMatch = [regex]::Match($baseName, '[\(\s\-]([0-9a-fA-F]{16})[\)\s]')
                    }
                    if (-not $folderMatch.Success) {
                        # Попытка 3: Просто 16-символьный hex код начинающийся с 01
                        $folderMatch = [regex]::Match($baseName, '(01[0-9a-fA-F]{14})')
                    }
                    
                    if ($folderMatch.Success) {
                        $titleId = $folderMatch.Groups[1].Value.Trim().ToLower()
                        Write-WorkerLog "Использован TitleID из имени файла: $titleId" -Type 'INFO'
                    } else {
                        throw "Не удалось определить TitleID! Проверьте имя файла или содержимое."
                    }
                }
                
                Write-WorkerLog "Упаковка результата" -Type 'STAGE'
                Write-WorkerProgress -Status "Упаковка" -Percent 70;
                
                # Ищем папки romfs/exefs (они могут быть в basedata или updatedata или корне)
                $romfsPath = $null; $exefsPath = $null
                
                $potentialRomfs = @((Get-ChildItem -Path $unpackDir -Filter "romfs" -Recurse -Directory)) | Sort-Object LastWriteTime -Descending | Select-Object -First 1
                if ($potentialRomfs) { $romfsPath = $potentialRomfs.FullName }
                
                $potentialExefs = @((Get-ChildItem -Path $unpackDir -Filter "exefs" -Recurse -Directory)) | Sort-Object LastWriteTime -Descending | Select-Object -First 1
                if ($potentialExefs) { $exefsPath = $potentialExefs.FullName }

                $packArgsBuilder = [System.Text.StringBuilder]::new("pack "); 
                [void]$packArgsBuilder.Append("--controlnca `"$($controlNcaFile.FullName)`" "); 
                # yanu-cli ТРЕБУЕТ titleid - всегда передаем
                [void]$packArgsBuilder.Append("--titleid `"$titleId`" ");
                
                if ($romfsPath) { [void]$packArgsBuilder.Append("--romfsdir `"$romfsPath`" ") }; 
                if ($exefsPath) { [void]$packArgsBuilder.Append("--exefsdir `"$exefsPath`" ") }; 
                [void]$packArgsBuilder.Append("-o `"$tempDir`"");
                
                $packArgs = $packArgsBuilder.ToString(); 
                Invoke-YanuPack -packArgs $packArgs -tempDir $tempDir -toolPaths $toolPaths -taskID $taskData.TaskID
                
                $packedNsp = (Get-ChildItem -LiteralPath $tempDir -Filter "*.nsp" -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName;
                if (-not $packedNsp) { throw "Ошибка: .nsp файл не был создан после упаковки." };
                $finalFileInTemp = $packedNsp
                
                # Конвертация в XCI или сжатие (через единую функцию)
                if ($taskData.OutFormat.ToLower() -in 'nsz', 'xcz', 'xci') {
                    $finalFileInTemp = Invoke-PostPackConversion -InputFile $packedNsp -TargetFormat $taskData.OutFormat -OutputDir $tempDir -ToolPaths $toolPaths -KpVal $kpVal -TrimVal $trimVal -CompressionLevel $taskData.CompressionLevel -UseCores $useCores -IsolatedNsz $isolatedNsz -ListFileName 'list_upd.txt'
                }
                
                # ОЧИСТКА: Удаляем распакованные папки перед завершением (для экономии места)
                if (Test-Path $unpackDir) { Remove-Item -LiteralPath $unpackDir -Recurse -Force -ErrorAction SilentlyContinue }
                if (Test-Path $isolatedYanuDir) { Remove-Item -LiteralPath $isolatedYanuDir -Recurse -Force -ErrorAction SilentlyContinue }
                
            }
            'Unpack' { 
                $outDir = if ([string]::IsNullOrWhiteSpace($taskData.OutDir)) { $toolPaths.odir } else { $taskData.OutDir };
                $mainFileForNaming = if ($taskData.Base) { $taskData.Base } else { $taskData.LooseFiles[0] };
                $baseFolderName = [System.IO.Path]::GetFileNameWithoutExtension($mainFileForNaming); 
                $finalFolderName = $baseFolderName; 
                [array]$updates = $taskData.Updates;
                
                if($updates.Count -gt 0) { 
                    $updateFile = $updates[0]; 
                    $updateVersionMatch = [regex]::Match($updateFile, '(\[v[0-9]+\])');
                    if($updateVersionMatch.Success){ $finalFolderName = $baseFolderName -replace '\[v[0-9]+\]', $updateVersionMatch.Groups[1].Value } 
                };
                $finalFolderName = $finalFolderName -replace '\s\([0-9\.]+\s[A-Z]+\)$', '';
                $finalUnpackPath = Join-Path -Path $outDir -ChildPath $finalFolderName; 
                [void](New-Item -ItemType Directory -Force $finalUnpackPath -ErrorAction SilentlyContinue);
                
                if ($taskData.Base) { 
                    $isolatedNsz = Setup-IsolatedToolWithKeys -toolName 'nsz' -tempDir $tempDir -toolPaths $toolPaths
                    $convertDir = Join-Path $tempDir 'converted_nsp';
                    [void](New-Item -ItemType Directory -Force $convertDir);
                    $needIso = if ($taskData.NeedIsolation) { $taskData.NeedIsolation } else { $false }
                    $baseFile = Convert-To-NspIfNeeded -filePath $taskData.Base -convertDir $convertDir -isolatedNsz $isolatedNsz -useCores $useCores -needIsolation $needIso;
                    $updateFileForUnpack = if($updates.Count -gt 0) { Convert-To-NspIfNeeded -filePath $updates[0] -convertDir $convertDir -isolatedNsz $isolatedNsz -useCores $useCores -needIsolation $needIso } else { $null };
                    $updateArg = if($updateFileForUnpack) { " --update `"$updateFileForUnpack`"" } else { "" }; 
                    
                    # ПРОВЕРКА ФЛАГА UnpackStitched: если нужно разделить base/udp/dlc - нужно использовать --extract-titles
                    # Но пока старая логика
                    
                    $procArgs = "unpack --base `"$baseFile`"$updateArg -o `"$finalUnpackPath`"";
                    
                    # ИЗОЛЯЦИЯ yanu-cli для Unpack
                    $isolatedYanuDir = Join-Path $tempDir 'isolated_yanu'
                    [void](New-Item -ItemType Directory -Force $isolatedYanuDir)
                    $isolatedYanuPath = Join-Path $isolatedYanuDir 'yanu-cli.exe'
                    Copy-Item -LiteralPath $toolPaths.yanu_cli_path -Destination $isolatedYanuPath -Force
                    
                    $proc = @{ Exe = $isolatedYanuPath; Args = "--keyfile `"$($toolPaths.key)`" $procArgs"; WorkingDir = $tempDir };
                    if ((Invoke-Tool $proc "Распаковка NSP") -ne 0) { throw "Ошибка распаковки." } 
                }
                
                [array]$looseFiles = $taskData.LooseFiles;
                if($looseFiles.Count -gt 0) { 
                    foreach($looseFile in $looseFiles) { 
                        $ext = [System.IO.Path]::GetExtension($looseFile).ToLower();
                        $fileName = [System.IO.Path]::GetFileNameWithoutExtension($looseFile);
                        # Каждый файл извлекается в свою отдельную папку
                        $fileOutPath = Join-Path $outDir $fileName
                        [void](New-Item -ItemType Directory -Force $fileOutPath -ErrorAction SilentlyContinue)
                        
                        if($ext -eq '.nca') { 
                            $hactoolArgs = "-k `"$($toolPaths.key)`" `"$looseFile`" --romfsdir `"$fileOutPath\romfs`" --exefsdir `"$fileOutPath\exefs`""; 
                            $hactoolProc = @{ Exe = $toolPaths.hactoolnet; Args = $hactoolArgs; WorkingDir = $toolPaths.tdir }; 
                            Invoke-Tool $hactoolProc "Распаковка NCA: $fileName" | Out-Null 
                        } 
                        elseif($ext -eq '.bin') { 
                            $hactoolArgs = "-k `"$($toolPaths.key)`" -t romfs `"$looseFile`" --romfsdir `"$fileOutPath\romfs`"";
                            $hactoolProc = @{ Exe = $toolPaths.hactoolnet; Args = $hactoolArgs; WorkingDir = $toolPaths.tdir }; 
                            Invoke-Tool $hactoolProc "Распаковка BIN: $fileName" | Out-Null 
                        } 
                        Write-WorkerLog "Извлечено в: $fileOutPath" -Type 'SUCCESS'
                    } 
                }; 
                $result.FinalPath = $finalUnpackPath
            }
            'Pack' { 
                Write-WorkerProgress -Status "Анализ" -Percent 10;
                if (-not (Test-Path -LiteralPath $toolPaths.hactoolnet)) { throw "hactoolnet.exe не найдена." }
                $searchRoot = $taskData.Folder;
                $controlNcaFile = $null; 
                $standardPaths = @(Join-Path $searchRoot "updatedata"; Join-Path $searchRoot "basedata")
                foreach ($path in $standardPaths) { 
                    if (Test-Path -LiteralPath $path) { 
                        $controlNcaFile = (Get-ChildItem -LiteralPath $path -Filter *.nca | ForEach-Object { if (& $toolPaths.hactoolnet "-k" $toolPaths.key $_.FullName | Select-String 'Control' -Quiet) { $_; return } } | Select-Object -First 1);
                        if ($controlNcaFile) { break } 
                    } 
                }
                if (-not $controlNcaFile) { 
                    Write-WorkerLog "Поиск control.nca (рекурсивно)..." -Type 'WARN';
                    $controlNcaFile = (Get-ChildItem -LiteralPath $searchRoot -Filter *.nca -Recurse | ForEach-Object { if (& $toolPaths.hactoolnet "-k" $toolPaths.key $_.FullName | Select-String 'Control' -Quiet) { $_; return } } | Select-Object -First 1) 
                }
                if (-not $controlNcaFile) { throw "Не найден control.nca." }; 
                                # [FIX] Читаем TitleID из NPDM (если есть) и приводим к нижнему регистру для совеместимости с hacPack
                $titleId = $null
                
                # Ищем main.npdm (для Program NCA) рекурсивно
                $mainNpdmFile = Get-ChildItem -LiteralPath $searchRoot -Filter "main.npdm" -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 1
                $mainNpdm = if ($mainNpdmFile) { $mainNpdmFile.FullName } else { $null }
                
                if ($mainNpdm) {
                     # WORKAROUND: hactoolnet может не переваривать пути с квадратными скобками.
                     # Копируем NPDM во временный файл с простым именем.
                     $msgNpdmTemp = Join-Path $tempDir "temp_read.npdm"
                     Copy-Item -LiteralPath $mainNpdm -Destination $msgNpdmTemp -Force
                     
                     # DEBUG: Check file exist/size
                     if (Test-Path -LiteralPath $msgNpdmTemp) {
                         $fSize = (Get-Item -LiteralPath $msgNpdmTemp).Length
                         Write-WorkerLog "Temp NPDM size: $fSize bytes" -Type 'INFO'
                     } else {
                         Write-WorkerLog "Temp NPDM not found!" -Type 'ERROR'
                     }

                     try {
                         # Пробуем прочитать NPDM с ключами
                         $npdmOutput = & $toolPaths.hactoolnet "-k" $toolPaths.key "-t" "npdm" "$msgNpdmTemp" 2>&1
                         $npdmOutputStr = $npdmOutput | Out-String
                         
                         Write-WorkerLog "Hactool out len: $($npdmOutputStr.Length)" -Type 'INFO'

                         # Regex flexible for spaces and case
                         $match = [regex]::Match($npdmOutputStr, '(?i)Program\s*I[Dd]:\s*([0-9a-fA-F]{16})')
                         
                         if ($match.Success) {
                             $titleId = $match.Groups[1].Value.Trim().ToLower()
                             Write-WorkerLog "Использован TitleID из NPDM: $titleId" -Type 'INFO'
                         } else {
                             Write-WorkerLog "Не удалось распарсить Program ID из NPDM." -Type 'WARN'
                         }
                     } catch {
                         Write-WorkerLog "Ошибка при чтении NPDM: $($_.Exception.Message)" -Type 'WARN'
                     }
                     
                     if (Test-Path -LiteralPath $msgNpdmTemp) { Remove-Item -LiteralPath $msgNpdmTemp -Force -ErrorAction SilentlyContinue }
                } else {
                     Write-WorkerLog "main.npdm не найден (рекурсивно) в $searchRoot" -Type 'WARN'
                }
                
                if (-not $titleId -or $titleId -eq '0100000000000000') {
                    # Fallback: Извлекаем TitleID из имени папки (напр. "Game [01002EF01A316000][v0]")
                    $folderName = [System.IO.Path]::GetFileName($searchRoot)
                    $folderMatch = [regex]::Match($folderName, '\[([0-9a-fA-F]{16})\]')
                    if ($folderMatch.Success) {
                        $titleId = $folderMatch.Groups[1].Value.Trim().ToLower()
                        Write-WorkerLog "Использован TitleID из имени папки: $titleId" -Type 'INFO'
                    } else {
                        throw "Не удалось определить TitleID! Проверьте имя папки или содержимое."
                    }
                }
                
                Write-WorkerProgress -Status "Упаковка" -Percent 50;
                $romfsPath = Join-Path $searchRoot "romfs"; 
                $exefsPath = Join-Path $searchRoot "exefs"
                
                if (-not (Test-Path -LiteralPath $romfsPath)) { 
                    $possibleRomfs = Get-ChildItem -LiteralPath $searchRoot -Filter "romfs" -Directory -Recurse | Select-Object -First 1; 
                    if ($possibleRomfs) { $romfsPath = $possibleRomfs.FullName } 
                }
                if (-not (Test-Path -LiteralPath $exefsPath)) { 
                    $possibleExefs = Get-ChildItem -LiteralPath $searchRoot -Filter "exefs" -Directory -Recurse | Select-Object -First 1; 
                    if ($possibleExefs) { $exefsPath = $possibleExefs.FullName } 
                }
                
                $packArgsBuilder = [System.Text.StringBuilder]::new("pack ");
                [void]$packArgsBuilder.Append("--controlnca `"$($controlNcaFile.FullName)`" "); 
                # yanu-cli ТРЕБУЕТ titleid - всегда передаем
                [void]$packArgsBuilder.Append("--titleid `"$titleId`" ");
                
                if (Test-Path -LiteralPath $romfsPath) { [void]$packArgsBuilder.Append("--romfsdir `"$romfsPath`" ") } 
                else { Write-WorkerLog "romfs не найдена." -Type 'WARN' }; 
                
                if (Test-Path -LiteralPath $exefsPath) { [void]$packArgsBuilder.Append("--exefsdir `"$exefsPath`" ") } 
                else { Write-WorkerLog "exefs не найдена." -Type 'WARN' }
                
                [void]$packArgsBuilder.Append("-o `"$tempDir`"");
                Invoke-YanuPack -packArgs $packArgsBuilder.ToString() -tempDir $tempDir -toolPaths $toolPaths -taskID $taskData.TaskID -useCores $useCores
                
                Write-WorkerProgress -Status "Поиск файла" -Percent 90;
                $packedNsp = $null; $attempts = 10
                while ($attempts -gt 0) { 
                    $packedNspItem = Get-ChildItem -LiteralPath $tempDir -Filter "*.nsp" -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1; 
                    if ($packedNspItem) { 
                        $packedNsp = $packedNspItem.FullName; 
                        Write-WorkerLog "Найден файл: $packedNsp" -Type 'INFO'; 
                        # Ждём освобождения файла (yanu-cli может ещё держать handle)
                        Start-Sleep -Seconds 2
                        break 
                    }; 
                    $attempts--; if ($attempts -gt 0) { Start-Sleep -Milliseconds 500 } 
                }
                if (-not $packedNsp) { throw "Не найден .nsp файл." }; $finalFileInTemp = $packedNsp
                
                # Конвертация в XCI или сжатие (через единую функцию)
                if ($taskData.OutFormat.ToLower() -in 'nsz', 'xcz', 'xci') { 
                    $finalFileInTemp = Invoke-PostPackConversion -InputFile $packedNsp -TargetFormat $taskData.OutFormat -OutputDir $tempDir -ToolPaths $toolPaths -KpVal $kpVal -TrimVal $trimVal -CompressionLevel $taskData.CompressionLevel -UseCores $useCores -ListFileName 'list_pack.txt'
                }
            }
            'DirectStitch' {
                Write-WorkerProgress -Status "Подготовка" -Percent 2; 
                $isolatedNsz = Setup-IsolatedToolWithKeys -toolName 'nsz' -tempDir $tempDir -toolPaths $toolPaths
                $convertDir = Join-Path $tempDir 'converted_nsp'; [void](New-Item -ItemType Directory -Force $convertDir)
                $outDir = if (-not [string]::IsNullOrWhiteSpace($taskData.OutDir)) { $taskData.OutDir } else { $toolPaths.odir };
                if (-not (Test-Path -LiteralPath $outDir)) { [void](New-Item -ItemType Directory -Force $outDir) }
                
                Write-WorkerLog "Конвертация/Сбор файлов" -Type 'STAGE';
                Write-WorkerLog "Подготовка/Изоляция файлов" -Type 'STAGE';
                $needIso = if ($taskData.NeedIsolation) { $taskData.NeedIsolation } else { $false }
                
                # ИСПРАВЛЕНИЕ: Собираем ВСЕ файлы (base, update, DLC, unlocker) в один общий список
                # для сшивания в ОДИН проход через squirrel.exe (как в рабочей версии 0.0.227)
                $sourcesToStitch = [System.Collections.Generic.List[string]]::new()
                
                $validBaseTags = [System.Collections.Generic.List[string]]::new()
                [regex]::Matches($taskData.Base, '\[([0-9a-fA-F]{16})\]') | ForEach-Object { $validBaseTags.Add($_.Value) }
                [regex]::Matches($taskData.Base, '\[v\d+\]', 'IgnoreCase') | ForEach-Object { $validBaseTags.Add($_.Value) }
                $forceTagsStr = if ($validBaseTags.Count -gt 0) { $validBaseTags -join '' } else { "" }
                
                if ($taskData.Base) { $sourcesToStitch.Add((Convert-To-NspIfNeeded -filePath $taskData.Base -convertDir $convertDir -isolatedNsz $isolatedNsz -useCores $useCores -needIsolation $needIso -forceTags $forceTagsStr)) }
                if ($taskData.Updates) { foreach ($file in [array]$taskData.Updates) { $sourcesToStitch.Add((Convert-To-NspIfNeeded -filePath $file -convertDir $convertDir -isolatedNsz $isolatedNsz -useCores $useCores -needIsolation $needIso -forceTags $forceTagsStr)) } }
                if ($taskData.DLCs) { foreach ($file in [array]$taskData.DLCs) { $sourcesToStitch.Add((Convert-To-NspIfNeeded -filePath $file -convertDir $convertDir -isolatedNsz $isolatedNsz -useCores $useCores -needIsolation $needIso -forceTags $forceTagsStr)) } }
                if ($taskData.Unlockers) { foreach ($file in [array]$taskData.Unlockers) { $sourcesToStitch.Add((Convert-To-NspIfNeeded -filePath $file -convertDir $convertDir -isolatedNsz $isolatedNsz -useCores $useCores -needIsolation $needIso -forceTags $forceTagsStr)) } }
  
                Write-WorkerProgress -Status "Подготовка к сшиванию" -Percent 20;
                $localFilesForStitching = [System.Collections.Generic.List[string]]::new()
                foreach ($sourceFile in $sourcesToStitch) { 
                    if (-not (Test-Path -LiteralPath $sourceFile)) { continue }
                    $fName = Split-Path $sourceFile -Leaf
                    if ($fName -match "Unlocker") { Write-WorkerLog "Вшивание UNLOCKER: '$fName'" -Type 'SUCCESS' } else { Write-WorkerLog "Подготовка '$fName' (Прямой доступ)..." }
                    $localFilesForStitching.Add($sourceFile)
                }
                
                Write-WorkerProgress -Status "Сшивание (NSCB)" -Percent 25;
                $intermediateNSP = $null; $nscbListPath = Join-Path $tempDir "list.txt"; 
                $utf8NoBom = New-Object System.Text.UTF8Encoding $false
                $longLocalFiles = $localFilesForStitching | ForEach-Object { if (-not $_.StartsWith("\\?\") -and $_ -match "^[A-Za-z]:\\") { "\\?\$_" } else { $_ } }
                [System.IO.File]::WriteAllLines($nscbListPath, $longLocalFiles, $utf8NoBom)
                $isolatedNscb = Setup-IsolatedToolWithKeys -toolName 'nscb' -tempDir $tempDir -toolPaths $toolPaths
                
                $targetForNSCB = if ($taskData.OutFormat.ToLower() -in 'nsz', 'xcz') { $tempDir } else { $outDir };
                $longTargetForNSCB = if (-not $targetForNSCB.StartsWith("\\?\") -and $targetForNSCB -match "^[A-Za-z]:\\") { "\\?\$targetForNSCB" } else { $targetForNSCB }
                $filesBefore = @(Get-ChildItem -LiteralPath $targetForNSCB -Filter "*.nsp" -File -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)
                $nscbTargetType = if ($taskData.OutFormat.ToLower() -in 'xci', 'xcz') { 'xci' } else { 'cnsp' };
                $nscbTargetExt = if ($nscbTargetType -eq 'xci') { '.xci' } else { '.nsp' }
                
                $fatArg = if ($taskData.Fat32Split) { 'fat32' } else { 'exfat' }
                $squirrelArgsArray = @('-b', '65536', '-pv', 'false', '-kp', $kpVal);
                if ($nscbTargetType -eq 'xci') { $squirrelArgsArray += ('-tm', $trimVal) }
                $squirrelArgsArray += ('--RSVcap', '268435656', '-fat', $fatArg, '-fx', 'files', '-ND', 'true', '-t', $nscbTargetType, '-o', "`"$longTargetForNSCB`"", '-tfile', "`"$nscbListPath`"", '-roma', 'TRUE', '-dmul', '"calculate"')
                $squirrelProc = @{ Exe = $isolatedNscb.Exe; Args = ($squirrelArgsArray -join ' '); WorkingDir = $isolatedNscb.WorkingDir }
                
                if ((Invoke-Tool $squirrelProc "Сшивание") -ne 0) { throw "Ошибка во время сшивания файлов (squirrel.exe)" }
                if ($kpVal -ne "false") { Write-WorkerLog "Понижение версии ключей до Generation $kpVal успешно применено." -Type 'SUCCESS' }
                
                $filesAfter = @(Get-ChildItem -LiteralPath $targetForNSCB -Filter "*$nscbTargetExt" -File -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)
                $intermediateNSP = $filesAfter | Where-Object { $_ -notin $filesBefore } | Select-Object -First 1
                if (-not $intermediateNSP) { throw "Не найден файл $nscbTargetExt после сшивания." }

                Log-Xci-Status -filePath $intermediateNSP -isTrimmed $trimVal;
                $finalFileInTemp = $intermediateNSP
                if ($taskData.OutFormat.ToLower() -in 'nsz', 'xcz') {
                    Write-WorkerProgress -Status "Сжатие (NSZ)" -Percent 95;
                    $targetDirForCompression = $outDir;
                    $coresArg = if ($useCores) { "-t $useCores" } else { "" };
                    $nszArgs = "$coresArg -C -l $($taskData.CompressionLevel) `"$finalFileInTemp`" -o `"$targetDirForCompression`""
                    $procConv = @{ Exe = $isolatedNsz.Exe; Args = $nszArgs; WorkingDir = $isolatedNsz.WorkingDir }; 
                    if ((Invoke-Tool $procConv "Сжатие") -ne 0) { throw "Ошибка сжатия." }
                    if (Test-Path -LiteralPath $finalFileInTemp) { Remove-Item -LiteralPath $finalFileInTemp -Force }
                    $finalFileInTemp = (Get-ChildItem -LiteralPath $targetDirForCompression -Filter "*.$($taskData.OutFormat.ToLower())" | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
                }
            }
            'BuildMulti' {
                Write-WorkerProgress -Status "Подготовка" -Percent 2;
                $isolatedNsz = Setup-IsolatedToolWithKeys -toolName 'nsz' -tempDir $tempDir -toolPaths $toolPaths;
                $convertDir = Join-Path $tempDir 'converted_nsp'; [void](New-Item -ItemType Directory -Force $convertDir)
                Write-WorkerLog "Конвертация исходных файлов" -Type 'STAGE';
                $needIso = if ($taskData.NeedIsolation) { $taskData.NeedIsolation } else { $false }
                
                $validBaseTags = [System.Collections.Generic.List[string]]::new()
                [regex]::Matches($taskData.Base, '\[([0-9a-fA-F]{16})\]') | ForEach-Object { $validBaseTags.Add($_.Value) }
                [regex]::Matches($taskData.Base, '\[v\d+\]', 'IgnoreCase') | ForEach-Object { $validBaseTags.Add($_.Value) }
                $forceTagsStr = if ($validBaseTags.Count -gt 0) { $validBaseTags -join '' } else { "" }
                
                $convertedBase = Convert-To-NspIfNeeded -filePath $taskData.Base -convertDir $convertDir -isolatedNsz $isolatedNsz -useCores $useCores -needIsolation $needIso -forceTags $forceTagsStr; 
                
                # ОТКЛЮЧЕНО: Проверка на сшитый файл вызывала ложные срабатывания
                # Если нужно заменить контент в существующем сшитом файле, 
                # пользователь должен вручную распаковать базу

                $convertedUpdates = [System.Collections.Generic.List[string]]::new();
                if ($taskData.Updates) { foreach ($file in [array]$taskData.Updates) { $convertedUpdates.Add((Convert-To-NspIfNeeded -filePath $file -convertDir $convertDir -isolatedNsz $isolatedNsz -useCores $useCores -needIsolation $needIso -forceTags $forceTagsStr)) } };
                $convertedDLCs = [System.Collections.Generic.List[string]]::new(); 
                if ($taskData.DLCs) { foreach ($file in [array]$taskData.DLCs) { $convertedDLCs.Add((Convert-To-NspIfNeeded -filePath $file -convertDir $convertDir -isolatedNsz $isolatedNsz -useCores $useCores -needIsolation $needIso -forceTags $forceTagsStr)) } }
                  $convertedUnlockers = [System.Collections.Generic.List[string]]::new(); 
                  if ($taskData.Unlockers) { foreach ($file in [array]$taskData.Unlockers) { $convertedUnlockers.Add((Convert-To-NspIfNeeded -filePath $file -convertDir $convertDir -isolatedNsz $isolatedNsz -useCores $useCores -needIsolation $needIso -forceTags $forceTagsStr)) } }
                
                $unpackDir = Join-Path $tempDir 'unpacked'; [void](New-Item -ItemType Directory -Force $unpackDir); 
                
                $outDir = if (-not [string]::IsNullOrWhiteSpace($taskData.OutDir)) { $taskData.OutDir } else { $toolPaths.odir };
                if (-not (Test-Path -LiteralPath $outDir)) { [void](New-Item -ItemType Directory -Force $outDir) };
                
                # Возвращаем packDir в $tempDir (SSD), чтобы операции сшивания/упаковки не тормозили на медленном HDD
                $packDir = Join-Path $tempDir 'packed'; 
                [void](New-Item -ItemType Directory -Force $packDir -ErrorAction SilentlyContinue);
                
                $hasMods = ($null -ne $taskData.RomfsPaths -and $taskData.RomfsPaths.Count -gt 0) -or (-not [string]::IsNullOrEmpty($taskData.ExefsPath)) -or $taskData.ForceMultiRebuild;
                $intermediateNSP = $null;
                $nscbTargetType = if ($taskData.OutFormat.ToLower() -in 'xci', 'xcz') { 'xci' } else { 'cnsp' };
                $nscbTargetExt = if ($nscbTargetType -eq 'xci') { '.xci' } else { '.nsp' }

                if ($hasMods) {
                    Write-WorkerLog "Обнаружены моды. Полный цикл (yanu-cli).";
                    Write-WorkerProgress -Status "Подготовка" -Percent 5; 
                    $baseFileForUnpack = $convertedBase;
                    $updateFileForUnpack = if ($convertedUpdates.Count -gt 0) { $convertedUpdates[0] } else { $null };
                    
                    Write-WorkerProgress -Status "Распаковка" -Percent 30; 
                    $updateArgUnpack = if ($updateFileForUnpack) { " --update `"$updateFileForUnpack`"" } else { "" };
                    $unpackProcArgs = "unpack --base `"$baseFileForUnpack`"$updateArgUnpack -o `"$unpackDir`"";
                    
                    # ИЗОЛЯЦИЯ yanu-cli для BuildMulti
                    $isolatedYanuDir = Join-Path $tempDir 'isolated_yanu'
                    [void](New-Item -ItemType Directory -Force $isolatedYanuDir -ErrorAction SilentlyContinue)
                    $isolatedYanuPath = Join-Path $isolatedYanuDir 'yanu-cli.exe'
                    Copy-Item -LiteralPath $toolPaths.yanu_cli_path -Destination $isolatedYanuPath -Force
                    
                    $unpackProc = @{ Exe = $isolatedYanuPath; Args = "--keyfile `"$($toolPaths.key)`" $unpackProcArgs"; WorkingDir = $tempDir }; 
                    if ((Invoke-Tool $unpackProc "Распаковка") -ne 0) { throw "Ошибка распаковки." };
                    
                    Write-WorkerProgress -Status "Применение модов" -Percent 50;
                    if ($taskData.RomfsPaths) { 
                        $targetRomfs = Join-Path $unpackDir "romfs";
                        [void](New-Item -ItemType Directory -Path $targetRomfs -Force -EA 0); 
                        foreach ($romfsItem in [array]$taskData.RomfsPaths) { 
                            if (Test-Path -LiteralPath $romfsItem) { 
                                $roboSrc = $romfsItem -replace '^\\\\\?\\', '';
                                & robocopy.exe "$roboSrc" "$targetRomfs" /E /IS /IT /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null 
                            } 
                        } 
                    };
                    if ($taskData.ExefsPath) { 
                        $targetExefs = Join-Path $unpackDir "exefs";
                        [void](New-Item -ItemType Directory -Path $targetExefs -Force -EA 0); 
                        $roboSrc = $taskData.ExefsPath -replace '^\\\\\?\\', '';
                        & robocopy.exe "$roboSrc" "$targetExefs" /E /IS /IT /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null 
                    };
                    
                    Write-WorkerProgress -Status "Упаковка" -Percent 70;
                    $pdata = if (Test-Path -LiteralPath (Join-Path $unpackDir "updatedata")) { Join-Path $unpackDir "updatedata" } else { Join-Path $unpackDir "basedata" };
                    
                    $oldErrorPref = $ErrorActionPreference
                    $ErrorActionPreference = 'Continue'
                    
                    $controlNcaFile = $null
                    foreach ($ncaFile in (Get-ChildItem -LiteralPath $pdata -Filter *.nca)) {
                        $hactoolOutput = & $toolPaths.hactoolnet -k "$($toolPaths.key)" "$($ncaFile.FullName)" 2>$null | Out-String
                        if ($hactoolOutput -match 'Control') {
                            $controlNcaFile = $ncaFile
                            break
                        }
                    }
                    
                    if (-not $controlNcaFile) {
                        Write-WorkerLog "Стандартный поиск control.nca не дал результата. Ищем рекурсивно..." -Type 'WARN'
                        foreach ($ncaFile in (Get-ChildItem -LiteralPath $unpackDir -Filter *.nca -Recurse)) {
                            $hactoolOutput = & $toolPaths.hactoolnet -k "$($toolPaths.key)" "$($ncaFile.FullName)" 2>$null | Out-String
                            if ($hactoolOutput -match 'Control') {
                                $controlNcaFile = $ncaFile
                                break
                            }
                        }
                    }
                    
                    if (-not $controlNcaFile) { 
                        $ErrorActionPreference = $oldErrorPref
                        throw "Не удалось найти control.nca." 
                    }
                    
                    $titleIdOutput = & $toolPaths.hactoolnet -k "$($toolPaths.key)" "$($controlNcaFile.FullName)" 2>$null | Out-String
                    $ErrorActionPreference = $oldErrorPref
                    
                    $titleIdMatch = [regex]::Match($titleIdOutput, 'TitleID:\s*([0-9A-Fa-f]{16})')
                    if ($titleIdMatch.Success) {
                        $titleId = $titleIdMatch.Groups[1].Value.Trim()
                    } else {
                        throw "Не удалось извлечь TitleID из control.nca"
                    }
                    
                    $packArgsBuilder = [System.Text.StringBuilder]::new("pack ");
                    [void]$packArgsBuilder.Append("--controlnca `"$($controlNcaFile.FullName)`" ");
                    [void]$packArgsBuilder.Append("--titleid `"$titleId`" ");
                    $finalRomfs = Join-Path $unpackDir "romfs";
                    $finalExefs = Join-Path $unpackDir "exefs"
                    if (Test-Path -LiteralPath $finalRomfs) { [void]$packArgsBuilder.Append("--romfsdir `"$finalRomfs`" ") }
                    if (Test-Path -LiteralPath $finalExefs) { [void]$packArgsBuilder.Append("--exefsdir `"$finalExefs`" ") }
                    [void]$packArgsBuilder.Append("-o `"$packDir`"");
                    Invoke-YanuPack -packArgs $packArgsBuilder.ToString() -tempDir $tempDir -toolPaths $toolPaths -taskID $taskData.TaskID -useCores $useCores;
                    
                    $packedModdedNsp = (Get-ChildItem -LiteralPath $packDir -Filter "*.nsp" -File | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName;
                    if (-not $packedModdedNsp) { throw "Не удалось найти упакованный .nsp файл." };
                    
                    # ПЕРЕИМЕНОВЫВАЕМ упакованную базу, чтобы NSC_Builder распознал её. Ему КРИТИЧЕСКИ важны теги [TitleID] и [v0] для привязки Unlocker/DLC!
                    $renamedModdedNsp = Join-Path $packDir "ModdedBase_[$titleId]_[v0].nsp"
                    if ($packedModdedNsp.ToLower() -ne $renamedModdedNsp.ToLower()) {
                        Rename-Item -LiteralPath $packedModdedNsp -NewName (Split-Path $renamedModdedNsp -Leaf) -Force
                        $packedModdedNsp = $renamedModdedNsp
                    }
                    
                    # Подменяем базу на модовую и очищаем апдейты, чтобы их не сшивать дважды
                    $convertedBase = $packedModdedNsp
                    $convertedUpdates.Clear()
                }
                
                if ($true) {
                    Write-WorkerProgress -Status "Сшивание" -Percent 25; 
                    $filesForStitching = [System.Collections.Generic.List[string]]::new(); 
                    $filesForStitching.Add($convertedBase); 
                    $filesForStitching.AddRange($convertedUpdates); 
                    $filesForStitching.AddRange($convertedDLCs);
                    # ИСПРАВЛЕНИЕ: Unlocker-файлы добавляются в ОБЩИЙ список сшивания (один проход)
                    # как в рабочей версии 0.0.227, вместо отдельного этапа 2
                    $filesForStitching.AddRange($convertedUnlockers);
                    
                    # Логирование UNLOCKER
                    foreach ($uf in $convertedUnlockers) { 
                        Write-WorkerLog "Вшивание UNLOCKER: '$(Split-Path $uf -Leaf)'" -Type 'SUCCESS' 
                    }
                    
                    if ($filesForStitching.Count -gt 1 -or (-not [string]::IsNullOrWhiteSpace($taskData.InternalName))) { 
                        $nscbListPath = Join-Path $tempDir "list.txt"; 
                        $utf8NoBom = New-Object System.Text.UTF8Encoding $false
                        $longFilesForStitching = $filesForStitching | ForEach-Object { if (-not $_.StartsWith("\\?\") -and $_ -match "^[A-Za-z]:\\") { "\\?\$_" } else { $_ } }
                        [System.IO.File]::WriteAllLines($nscbListPath, $longFilesForStitching, $utf8NoBom);
                        $isolatedNscb = Setup-IsolatedToolWithKeys -toolName 'nscb' -tempDir $tempDir -toolPaths $toolPaths;
                        
                        $longPackDir = if (-not $packDir.StartsWith("\\?\") -and $packDir -match "^[A-Za-z]:\\") { "\\?\$packDir" } else { $packDir }
                        $fatArg = if ($taskData.Fat32Split) { 'fat32' } else { 'exfat' }
                        $squirrelArgsArray = @('-b', '65536', '-pv', 'false', '-kp', $kpVal, '--RSVcap', '268435656', '-fat', $fatArg, '-fx', 'files', '-ND', 'true', '-t', $nscbTargetType, '-o', "`"$longPackDir`"", '-tfile', "`"$nscbListPath`"", '-roma', 'TRUE', '-dmul', '"calculate"');
                        # ПРИМЕЧАНИЕ: squirrel.exe не поддерживает аргумент -nm для установки внутреннего имени
                        $squirrelProc = @{ Exe = $isolatedNscb.Exe; Args = ($squirrelArgsArray -join ' '); WorkingDir = $isolatedNscb.WorkingDir };
                        if ((Invoke-Tool $squirrelProc "Сшивание") -ne 0) { throw "Ошибка сшивания." }; Start-Sleep -Seconds 1;
                        if ($kpVal -ne "false") { Write-WorkerLog "Понижение версии ключей до Generation $kpVal успешно применено." -Type 'SUCCESS' }
                        $intermediateNSP = Get-ChildItem -LiteralPath $packDir -Filter "*$nscbTargetExt" -File -ErrorAction SilentlyContinue | Sort-Object -Property LastWriteTime -Descending | Select-Object -First 1 | Select-Object -ExpandProperty FullName;
                        if (-not $intermediateNSP) { throw "Не найден файл $nscbTargetExt." } 
                    } else { 
                        $tempDest = Join-Path $packDir ([System.IO.Path]::GetFileName($filesForStitching[0]));
                        if ($filesForStitching[0].ToLower() -ne $tempDest.ToLower()) {
                            Copy-Item -LiteralPath $filesForStitching[0] -Destination $tempDest -Force; 
                        }
                        $intermediateNSP = $tempDest 
                    }
                    
                    Log-Xci-Status -filePath $intermediateNSP -isTrimmed $trimVal
                }
                
                if (-not $intermediateNSP) { throw "Не удалось получить промежуточный файл." }; $finalFileInTemp = $intermediateNSP; 
                
                if ($taskData.OutFormat.ToLower() -in 'nsz', 'xcz') { 
                    Write-WorkerProgress -Status "Сжатие (NSZ)" -Percent 95;
                    $coresArg = if ($useCores) { "-t $useCores" } else { "" }
                    $nszArgs = "$coresArg -C -l $($taskData.CompressionLevel) `"$finalFileInTemp`" -o `"$packDir`"";
                    $procConv = @{ Exe = $isolatedNsz.Exe; Args = $nszArgs; WorkingDir = $isolatedNsz.WorkingDir };
                    if ((Invoke-Tool $procConv "Конвертация в NSZ") -ne 0) { throw "Ошибка сжатия." };
                    Remove-Item -LiteralPath $finalFileInTemp -Force;
                    $finalFileInTemp = (Get-ChildItem -LiteralPath $packDir -Filter "*.$($taskData.OutFormat.ToLower())" | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName 
                }
            }
            'Convert' { 
                Write-WorkerProgress -Status "Подготовка" -Percent 5;
                $inputFile = $taskData.File; 
                $sourceExt = ([System.IO.Path]::GetExtension($inputFile)).ToLower(); 
                $targetFormat = $taskData.OutFormat.ToLower();
                
                # РАСПАКОВКА (если исходник сжат) или СЖАТИЕ В NSZ (если это конечная цель)
                if (($sourceExt -in '.nsz', '.xcz') -or ($targetFormat -eq 'nsz')) { 
                    $isolatedNsz = Setup-IsolatedToolWithKeys -toolName 'nsz' -tempDir $tempDir -toolPaths $toolPaths;
                    $nszArgs = ""; $opTitle = ""; $expectedExt = ""; 
                    if ($sourceExt -in '.nsz', '.xcz') { 
                        $opTitle = "Распаковка из $sourceExt";
                        $nszArgs = "-D `"$inputFile`" -o `"$tempDir`""; 
                        $expectedExt = if ($sourceExt -eq '.xcz') { '.xci' } else { '.nsp' } 
                    } else { 
                        $opTitle = "Сжатие в nsz";
                        $coresArg = if ($useCores) { "-t $useCores" } else { "" };
                        $nszArgs = "$coresArg -C -l $($taskData.CompressionLevel) `"$inputFile`" -o `"$tempDir`""; 
                        $expectedExt = ".nsz"
                    }
                    Write-WorkerProgress -Status "$opTitle (NSZ)" -Percent 50;
                    $proc = @{ Exe = $isolatedNsz.Exe; Args = $nszArgs; WorkingDir = $isolatedNsz.WorkingDir };
                    if ((Invoke-Tool $proc $opTitle) -ne 0) { throw "Ошибка: $opTitle" };
                    $finalFileInTemp = (Get-ChildItem -LiteralPath $tempDir -Filter "*$($expectedExt)" | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName 
                } else { 
                    Write-WorkerLog "Копирование файла" -Type 'STAGE';
                    $tempCopyPath = Join-Path $tempDir (Split-Path $inputFile -Leaf);
                    Copy-Item -LiteralPath $inputFile -Destination $tempCopyPath -Force;
                    $finalFileInTemp = $tempCopyPath
                }

                if (-not $finalFileInTemp -or -not (Test-Path -LiteralPath $finalFileInTemp)) { throw "Не найден рабочий файл." } 
                
                # КОНВЕРТАЦИЯ В XCI и/или СЖАТИЕ (через единую функцию)
                if ($targetFormat -in 'xci', 'xcz') {
                    $finalFileInTemp = Invoke-PostPackConversion -InputFile $finalFileInTemp -TargetFormat $targetFormat -OutputDir $tempDir -ToolPaths $toolPaths -KpVal $kpVal -TrimVal $trimVal -CompressionLevel $taskData.CompressionLevel -UseCores $useCores -IsolatedNsz $isolatedNsz -ListFileName 'list_convert.txt'
                }
            }
            'PackNCA' {
                Write-WorkerLog "Упаковка папки в NCA" -Type 'STAGE'
                Write-WorkerProgress -Status "Упаковка NCA" -Percent 10
                
                $folderPath = $taskData.Folder
                $outDir = if (-not [string]::IsNullOrWhiteSpace($taskData.OutDir)) { $taskData.OutDir } else { $toolPaths.odir }
                if (-not (Test-Path -LiteralPath $outDir)) { [void](New-Item -ItemType Directory -Force $outDir) }
                
                $folderName = Split-Path $folderPath -Leaf
                $outputNca = Join-Path $outDir "$folderName.nca"
                
                Write-WorkerProgress -Status "Создание NCA" -Percent 50
                
                # hacPack находится в той же папке что и hactoolnet
                $hacPackPath = Join-Path (Split-Path $toolPaths.hactoolnet -Parent) "hacPack.exe"
                
                if (Test-Path -LiteralPath $hacPackPath) {
                    # Создаём временную выходную папку для hacPack
                    $hacPackOutDir = Join-Path $tempDir "hacpack_out"
                    [void](New-Item -ItemType Directory -Force $hacPackOutDir)
                    
                    # Проверяем структуру папки: если внутри есть romfs, используем его напрямую
                    $romfsSubfolder = Join-Path $folderPath "romfs"
                    $actualRomfsDir = if (Test-Path -LiteralPath $romfsSubfolder) { $romfsSubfolder } else { $folderPath }
                    
                    Write-WorkerLog "Путь romfs для hacPack: $actualRomfsDir" -Type 'INFO'
                    
                    # hacPack параметры: --type nca --ncatype data --romfsdir <folder> --titleid <id> -o <outdir>
                    $hacPackArgs = "-k `"$($toolPaths.key)`" --type nca --ncatype data --romfsdir `"$actualRomfsDir`" --titleid 0100000000000000 -o `"$hacPackOutDir`""
                    $hacPackProc = @{ Exe = $hacPackPath; Args = $hacPackArgs; WorkingDir = $tempDir }
                    $exitCode = Invoke-Tool $hacPackProc "Создание NCA через hacPack"
                    
                    if ($exitCode -eq 0) {
                        # hacPack создаёт NCA с именем-хешем, найдём его и переименуем
                        $createdNca = Get-ChildItem -LiteralPath $hacPackOutDir -Filter "*.nca" | Select-Object -First 1
                        if ($createdNca) {
                            Move-Item -LiteralPath $createdNca.FullName -Destination $outputNca -Force
                            Write-WorkerLog "NCA создан через hacPack: $outputNca" -Type 'SUCCESS'
                        } else {
                            throw "hacPack не создал NCA файл"
                        }
                    } else {
                        throw "hacPack завершился с ошибкой (код: $exitCode)"
                    }
                } else {
                    throw "hacPack.exe не найден по пути: $hacPackPath"
                }
                
                if (Test-Path -LiteralPath $outputNca) {
                    $finalFileInTemp = $outputNca
                } else {
                    throw "Не удалось создать NCA файл"
                }
            }
        }
        
        # ═══════════════════════════════════════════════════════════════
        if ($taskData.TaskType -ne 'Unpack' -and $null -ne $finalFileInTemp -and (Test-Path -LiteralPath $finalFileInTemp)) {
            $finalizationTimer = [System.Diagnostics.Stopwatch]::StartNew()
            Write-WorkerLog "Финализация" -Type 'STAGE'
            Write-Header -Title "ФИНАЛИЗАЦИЯ" -Subtitle "Сохранение" -AppVersion $taskData.AppVersion

            # ── Шаг 1/5: Определение размера результата ──
            Write-WorkerProgress -Status "Финализация: размер файла" -Percent 96
            $finalFileSizeBytes = 0
            try {
                $finalFileSizeBytes = (Get-Item -LiteralPath $finalFileInTemp).Length
                $sizeDisplay = if ($finalFileSizeBytes -gt 1073741824) { "{0:N2} GB" -f ($finalFileSizeBytes / 1GB) } elseif ($finalFileSizeBytes -gt 1048576) { "{0:N2} MB" -f ($finalFileSizeBytes / 1MB) } else { "{0:N2} KB" -f ($finalFileSizeBytes / 1KB) }
                Write-WorkerLog "Размер результата: $sizeDisplay" -Type 'FILE'
            } catch { Write-WorkerLog "Не удалось определить размер файла" -Type 'WARN' }
            
            if ($finalFileSizeBytes -gt 0 -and $finalFileSizeBytes -le 1024 -and $taskData.TaskType -in 'DirectStitch', 'BuildMulti') {
                if (Test-Path -LiteralPath $finalFileInTemp) { Remove-Item -LiteralPath $finalFileInTemp -Force -ErrorAction SilentlyContinue }
                throw "ОШИБКА: NSC_Builder не смог обработать игру (сгенерирован пустой файл $finalFileSizeBytes байт). Причина: Игра слишком новая (требует FW 18+) и NSCB физически не может ее собрать из-за устаревшего ядра или ключей. Пожалуйста, используйте обычную папку с обновлениями на консоли/эмуляторе, склейка для этой игры не поддерживается!"
            }
            
            # ── Шаг 2/5: Определение выходной папки ──
            Write-WorkerProgress -Status "Финализация: выходная папка" -Percent 97
            $finalFileDir = if (-not [string]::IsNullOrWhiteSpace($taskData.OutDir)) { $taskData.OutDir } else { $toolPaths.odir }
            if (-not (Test-Path -LiteralPath $finalFileDir)) { [void](New-Item -ItemType Directory -Force $finalFileDir) }
            Write-WorkerLog "Выходная папка: $finalFileDir" -Type 'FILE'
            
            # ── Шаг 3/5: Формирование имени файла ──
            Write-WorkerProgress -Status "Финализация: имя файла" -Percent 97
            Write-WorkerLog "Анализ метаданных для формирования имени..." -Type 'INFO'
            $currentFileName = Split-Path $finalFileInTemp -Leaf
            
            if (-not [string]::IsNullOrWhiteSpace($taskData.OutName)) {
                $extFinal = [System.IO.Path]::GetExtension($currentFileName)
                $userBaseName = $taskData.OutName
                if ($extFinal -ne "" -and $userBaseName.ToLower().EndsWith($extFinal.ToLower())) {
                    $userBaseName = $userBaseName.Substring(0, $userBaseName.Length - $extFinal.Length)
                }
                $finalFileName = $userBaseName + $extFinal
            } else {
                $finalFileName = Generate-CustomFileName -taskData $taskData -originalFileName $currentFileName -toolPaths $toolPaths -InputFilePath $finalFileInTemp
            }
            
            $destinationPath = Join-Path -Path $finalFileDir -ChildPath $finalFileName
            
            # ЗАЩИТА ОТ ПЕРЕЗАПИСИ: Если файл с таким именем уже существует (например, от другой параллельной задачи), добавляем уникальный суффикс
            $counter = 1
            $baseFinalName = [System.IO.Path]::GetFileNameWithoutExtension($finalFileName)
            $extFinalName = [System.IO.Path]::GetExtension($finalFileName)
            while (Test-Path -LiteralPath $destinationPath) {
                $finalFileName = "${baseFinalName}_(${counter})${extFinalName}"
                $destinationPath = Join-Path -Path $finalFileDir -ChildPath $finalFileName
                $counter++
            }
            
            Write-WorkerLog "Имя файла: $finalFileName" -Type 'SUCCESS'
            
            # ── Шаг 4/5: Перемещение / Переименование файла ──
            if ($finalFileInTemp -ne $destinationPath) {
                $srcDir = Split-Path -Path $finalFileInTemp -Parent
                $srcRoot = [System.IO.Path]::GetPathRoot($finalFileInTemp)
                $dstRoot = [System.IO.Path]::GetPathRoot($finalFileDir)
                $isCrossVolume = ($srcRoot -and $dstRoot -and ($srcRoot.ToLower() -ne $dstRoot.ToLower()))
                
                if ($srcDir -eq $finalFileDir) {
                    # Просто переименование — мгновенно
                    Write-WorkerProgress -Status "Финализация: переименование" -Percent 98
                    Write-WorkerLog "Переименование файла..." -Type 'INFO'
                    Rename-Item -LiteralPath $finalFileInTemp -NewName $finalFileName -Force
                    Write-WorkerLog "Переименовано успешно" -Type 'SUCCESS'
                } elseif ($isCrossVolume) {
                    # Копирование между томами — ДОЛГАЯ операция с прогрессом
                    Write-WorkerProgress -Status "Финализация: копирование ($sizeDisplay)" -Percent 98
                    Write-WorkerLog "Копирование между томами: $srcRoot → $dstRoot ($sizeDisplay)..." -Type 'PROGRESS'
                    
                    $copyTimer = [System.Diagnostics.Stopwatch]::StartNew()
                    
                    # Используем robocopy с ключом /J (unbuffered I/O) для предотвращения забивания кэша и падения скорости
                    $srcFileName = Split-Path $finalFileInTemp -Leaf
                    $roboCopyArgs = @($srcDir, $finalFileDir, $srcFileName, '/MOV', '/J', '/R:5', '/W:3', '/NJH', '/NJS', '/NDL', '/nc', '/ns')
                    & robocopy.exe @roboCopyArgs | Out-Null
                    
                    # Если robocopy переместил файл, переименуем в финальное имя
                    $movedPath = Join-Path $finalFileDir $srcFileName
                    if ((Test-Path -LiteralPath $movedPath) -and $srcFileName -ne $finalFileName) {
                        Rename-Item -LiteralPath $movedPath -NewName $finalFileName -Force
                    }
                    
                    $copyTimer.Stop()
                    $copyElapsed = $copyTimer.Elapsed
                    $speed = if ($copyElapsed.TotalSeconds -gt 0 -and $finalFileSizeBytes -gt 0) { "{0:N1} MB/s" -f ($finalFileSizeBytes / 1MB / $copyElapsed.TotalSeconds) } else { "мгновенно" }
                    Write-WorkerLog "Копирование завершено за $("{0:mm\:ss}" -f $copyElapsed) ($speed)" -Type 'SUCCESS'
                } else {
                    # Перемещение на том же томе — быстро
                    Write-WorkerProgress -Status "Финализация: перемещение" -Percent 98
                    Write-WorkerLog "Перемещение файла..." -Type 'INFO'
                    Move-Item -LiteralPath $finalFileInTemp -Destination $destinationPath -Force
                    Write-WorkerLog "Перемещено успешно" -Type 'SUCCESS'
                }
            }
            $result.FinalPath = $destinationPath
            
            # ── Шаг 5/5: Подтверждение и итог ──
            Write-WorkerProgress -Status "Финализация: проверка" -Percent 99
            $finalizationTimer.Stop()
            $totalElapsed = $finalizationTimer.Elapsed
            Write-WorkerLog "Файл сохранён: $finalFileName (за $("{0:mm\:ss}" -f $totalElapsed))" -Type 'SUCCESS'
        } elseif ($taskData.TaskType -eq 'Unpack') { $result.FinalPath = $finalUnpackPath }
        
        # Подсчёт итогового размера
        Write-WorkerProgress -Status "Завершение: подсчёт" -Percent 99
        try { if ($result.FinalPath -and (Test-Path -LiteralPath $result.FinalPath -ErrorAction Stop)) { $finalItem = (Get-Item -LiteralPath $result.FinalPath -ErrorAction SilentlyContinue | Select-Object -First 1);
            if ($finalItem) { $result.FinalSize = Get-ItemSize $finalItem } } } catch { Write-WorkerLog "Файл создан, но не удалось получить размер." -Type 'WARN' }; $result.Status = 'Готово'
    } catch {
        if ($_.Exception.Message -ne "Задача пропущена") {
            $result.Status = 'Ошибка';
            $formattedError = Format-DetailedError $_ $taskData; $reportPath = Join-Path $toolPaths.cd "CRASH_REPORT_$($taskData.TaskID).log"; $formattedError | Out-File -FilePath $reportPath -Encoding utf8;
            $formattedError -split [System.Environment]::NewLine | ForEach-Object { Write-WorkerLog -Message $_ -Type "ERROR" }
        }
    }
    finally { 
        try { 
            if ($layoutFile) { $curr = $Host.UI.RawUI.WindowSize; [pscustomobject]@{Width=$curr.Width; Height=$curr.Height} | Export-Clixml $layoutFile -Force }
            if (Test-Path -LiteralPath $tempDir) { Remove-Item -LiteralPath $tempDir -Recurse -Force -ErrorAction SilentlyContinue };
            $finalOutDir = if(-not [string]::IsNullOrWhiteSpace($taskData.OutDir)) { $taskData.OutDir } else { $toolPaths.odir }; 
            $tempPackDir = Join-Path $finalOutDir ".storm_temp_pack_$($taskData.TaskID)";
            if (Test-Path -LiteralPath $tempPackDir) { Remove-Item -LiteralPath $tempPackDir -Recurse -Force -ErrorAction SilentlyContinue };
            Get-Item -Path (Join-Path $finalOutDir "*.cnmt.xml") -ErrorAction SilentlyContinue | Remove-Item -Force; 
            Remove-Item -Path "$($toolPaths.tdir)\yanu.log.*", "$($toolPaths.tdir)\base*", "$($toolPaths.nbdir)\NSCB.log", "$($toolPaths.tdir)\*.tmp" -Recurse -Force -ErrorAction SilentlyContinue 
        } catch { Write-WorkerLog "Ошибка очистки: $($_.Exception.Message)" -Type 'WARN' };
        $result | Export-Clixml -Path $resultFile 
    }
} catch { $crashLogPath = Join-Path (Split-Path $TaskDataPath -Parent) "crash-$((Get-Date -Format 'yyyyMMddHHmmss').ToString())-$($PID).log";
    $errorContent = @("FATAL WORKER SCRIPT ERROR", "Time: $(Get-Date)", "TaskDataPath: $TaskDataPath", "Error: $_.ToString()", "StackTrace: $($_.ScriptStackTrace)") -join [System.Environment]::NewLine; $errorContent | Out-File -FilePath $crashLogPath -Encoding utf8 }
'@
            
            # Вставляем блок функций воркера в шаблон
            $workerScriptContent = $workerScriptContentTemplate.Replace('##WORKER_FUNCTIONS_BLOCK##', $script:WorkerFunctionsBlock)
            
            $workerScriptContent | Set-Content -Path $workerScriptPath -Encoding UTF8
            $startProcArgs = @{ FilePath = 'powershell.exe'; PassThru = $true; WorkingDirectory = $script:wdir }
            $startProcArgs.WindowStyle = 'Normal'
            
            if ($script:DebugMode) { $argList = "-NoExit -NoProfile -ExecutionPolicy Bypass -File `"$workerScriptPath`" -TaskDataPath `"$taskDataPath`"" } 
            else { $argList = "-NoProfile -ExecutionPolicy Bypass -File `"$workerScriptPath`" -TaskDataPath `"$taskDataPath`"" }
            
            $startProcArgs.ArgumentList = $argList
            $process = Start-Process @startProcArgs
            
            $taskRow.Tag.ProcessID = $process.Id
            $taskInfo = @{ Row = $taskRow; Process = $process; StatusFile = (Join-Path $script:wdir "status-$($taskData.TaskID).log"); LogFile = (Join-Path $script:wdir "log-$($taskData.TaskID).log") }
            $script:runningTasks[$process.Id] = $taskInfo
        }
    } finally {
        [System.Threading.Monitor]::Exit($lockObject)
    }
}

function Check-RunningTasks {
    # Обработка активных процессов
    if (-not $script:runningTasks.IsEmpty) {
        $procIds = @($script:runningTasks.Keys)
        foreach ($procId in $procIds) {
            if (-not $script:runningTasks.ContainsKey($procId)) { continue }
            $taskInfo = $script:runningTasks[$procId]
            $process = Get-Process -Id $procId -ErrorAction SilentlyContinue
            
            # Чтение логов из файла
            if ($taskInfo.LogFile -and (Test-Path -LiteralPath $taskInfo.LogFile)) {
                try {
                    $logContent = $null
                    try {
                        $fs = [System.IO.FileStream]::new($taskInfo.LogFile, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
                        $lastPos = if ($taskInfo.LastLogPos) { $taskInfo.LastLogPos } else { 0 }
                        if ($fs.Length -gt $lastPos) {
                            $fs.Position = $lastPos
                            $sr = [System.IO.StreamReader]::new($fs, [System.Text.Encoding]::UTF8)
                            $logContent = $sr.ReadToEnd()
                            $taskInfo.LastLogPos = $fs.Position
                            $sr.Dispose()
                        }
                        $fs.Dispose()
                    } catch { }
                    if ($logContent) {
                        $logContent -split "`n" | Where-Object { $_.Trim() } | ForEach-Object {
                            try {
                                $entry = ConvertFrom-Json $_
                                Write-Log -Message $entry.Message -Type $entry.Type -TaskID $entry.TaskID
                            } catch { }
                        }
                    }
                } catch {
                    Write-Log "Ошибка чтения логов воркера (PID: $procId): $($_.Exception.Message)" -Type 'DEBUG'
                }
            }

            if ($process) {
                if (Test-Path -LiteralPath $taskInfo.StatusFile) {
                    try {
                        $statusLine = [System.IO.File]::ReadAllText($taskInfo.StatusFile).Trim()
                        if ($statusLine -match "^(-?\d+)\|(.*)$" -and $taskInfo.Row.DataGridView) {
                            $taskInfo.Row.Cells['Статус'].Value = $matches[2]
                            $pct = [int]$matches[1]
                            if ($pct -ge 0) { $taskInfo.Row.Cells['Выполнение'].Value = $pct }
                        }
                    } catch {
                        Write-Log "Ошибка чтения статуса воркера (PID: $procId): $($_.Exception.Message)" -Type 'DEBUG'
                    }
                }
            } else {
                # Процесс завершен
                $taskRow = $taskInfo.Row; $taskData = $taskRow.Tag;
                $resultFile = Join-Path $script:wdir "result-$($taskData.TaskID).xml"
                $crashLog = Get-Item -Path (Join-Path $script:wdir "crash-*.log") -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "*-$($procId).log" }
                
                if ($crashLog) {
                    $crashReason = Get-Content -LiteralPath $crashLog.FullName | Out-String; Write-Log "КРАХ ВОРКЕРА! Детали: $crashReason" -TaskID $taskData.TaskID -Type 'ERROR'; $taskRow.Cells['Статус'].Value = "Ошибка (КРАХ ВОРКЕРА)"; $taskRow.Cells['Выполнение'].Value = $null
                    Send-ToastNotification -Title '❌ Ошибка задачи' -Message "Задача '$($taskRow.Cells['Задача'].Value)' завершилась крахом воркера." -Type 'Error'
                } elseif (Test-Path -LiteralPath $resultFile) {
                    $finalResult = Import-Clixml -Path $resultFile
                    if ($finalResult.Status -eq 'Готово' -and (Test-Path -LiteralPath $finalResult.FinalPath)) {
                        $userDefinedName = $null
                        [System.Threading.Monitor]::Enter($script:outNamesFileLock)
                        try {
                            if (Test-Path -LiteralPath $script:outNamesFile) {
                                $allNames = Import-Csv $script:outNamesFile -ErrorAction SilentlyContinue
                                $nameRecord = $allNames | Where-Object { $_.TaskID -eq $finalResult.TaskID } | Select-Object -First 1
                                if ($nameRecord) {
                                    $userDefinedName = $nameRecord.OutName
                                    $remainingNames = $allNames | Where-Object { $_.TaskID -ne $finalResult.TaskID }
                                    if ($remainingNames.Count -gt 0) { $remainingNames | Export-Csv -Path $script:outNamesFile -NoTypeInformation -Encoding UTF8 } else { Remove-Item -LiteralPath $script:outNamesFile -Force -ErrorAction SilentlyContinue }
                                }
                            }
                        } finally { [System.Threading.Monitor]::Exit($script:outNamesFileLock) }

                        if (-not [string]::IsNullOrWhiteSpace($userDefinedName)) {
                            try {
                                $originalPath = $finalResult.FinalPath
                                $correctExtension = ""
                                if (-not [string]::IsNullOrWhiteSpace($taskData.OutFormat)) {
                                    $correctExtension = "." + $taskData.OutFormat.ToLower()
                                } else {
                                    $correctExtension = ".nsp"
                                }
                                $finalName = $userDefinedName
                                $validGameExtensions = '.nsp', '.nsz', '.xci', '.xcz'
                            
                                $currentExt = ''; if ([System.IO.Path]::HasExtension($finalName)) { $currentExt = [System.IO.Path]::GetExtension($finalName).ToLower() }
                                if ($currentExt -notin $validGameExtensions) { $finalName += $correctExtension }
                                $invalidChars = [System.IO.Path]::GetInvalidFileNameChars() -join ''; $regex = "[{0}]" -f [regex]::Escape($invalidChars); $finalName = ($finalName -replace $regex, '').Trim()
                                
                                Write-Log "Переименование готового файла в '$finalName'..." -TaskID $finalResult.TaskID
                                $newPath = Join-Path (Split-Path $originalPath -Parent) $finalName
                                # ИСПРАВЛЕНИЕ: Использование -LiteralPath для переименования
                                Rename-Item -LiteralPath $originalPath -NewName $finalName -Force
                                $finalResult.FinalPath = $newPath
                                Write-Log "Файл успешно сохранен: $finalName" -TaskID $finalResult.TaskID -Type 'SUCCESS'
                                
                            } catch { Write-Log "Не удалось переименовать файл в '$userDefinedName'. Ошибка: $($_.Exception.Message)" -TaskID $finalResult.TaskID -Type 'ERROR' }
                        }
                    }
                    $taskRow.Cells['Статус'].Value = $finalResult.Status; $taskRow.Tag.FinalPath = $finalResult.FinalPath
                    if ($finalResult.Status -eq 'Готово') { $taskRow.Cells['Выполнение'].Value = 100 } else { $taskRow.Cells['Выполнение'].Value = $null }
                    if ($finalResult.FinalSize -ne $null) { $taskRow.Cells['Кон. размер'].Value = Format-FileSize $finalResult.FinalSize }
                    if ($finalResult.InitialSize -gt 0 -and $finalResult.FinalSize -ne $null -and $finalResult.FinalSize -gt 0) { $diff = [math]::Round(($finalResult.FinalSize / $finalResult.InitialSize) * 100, 2); $taskRow.Cells['Разница'].Value = "{0:N2} %" -f $diff }
                    if ($finalResult.Status -eq 'Готово') { 
                        Write-Log "Задача '$($taskRow.Cells['Задача'].Value)' для '$($taskRow.Cells['Обработка'].Value)' успешно завершена." -TaskID $taskRow.Tag.TaskID -Type 'SUCCESS'; 
                        Send-ToastNotification -Title '✅ Задача завершена' -Message "'$($taskRow.Cells['Обработка'].Value)' успешно обработано." -Type 'Success' 
                        
                        try {
                            $historyFile = Join-Path $script:cd "ssb.history.json"
                            $record = [ordered]@{
                                Date = (Get-Date -Format "yyyy-MM-dd HH:mm:ss")
                                TaskType = $taskRow.Cells['Задача'].Value
                                OriginalName = $taskRow.Cells['Обработка'].Value
                                FinalPath = if ($finalResult.FinalPath) { $finalResult.FinalPath } else { $taskData.OutDir }
                            }
                            $historyArray = @()
                            if (Test-Path $historyFile) {
                                $content = Get-Content -LiteralPath $historyFile -Raw -Encoding UTF8
                                if ($content) { $historyArray = @(ConvertFrom-Json $content) }
                            }
                            $historyArray = @($record) + $historyArray
                            $historyArray | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $historyFile -Encoding UTF8
                            
                            if ($script:f.InvokeRequired) { $script:f.Invoke({ Refresh-HistoryTab }) } else { Refresh-HistoryTab }
                        } catch { Write-Log "Ошибка сохранения истории: $($_.Exception.Message)" -Type 'WARN' }
                    } else { 
                        Write-Log "Задача '$($taskRow.Cells['Задача'].Value)' для '$($taskRow.Cells['Обработка'].Value)' завершилась со статусом: $($finalResult.Status)." -TaskID $taskRow.Tag.TaskID -Type 'ERROR'; 
                        Send-ToastNotification -Title '❌ Ошибка задачи' -Message "'$($taskRow.Cells['Обработка'].Value)' завершилась с ошибкой." -Type 'Error' 
                    }
                } else {
                    $taskRow.Cells['Статус'].Value = "Ошибка (Крах)"; $taskRow.Cells['Выполнение'].Value = $null; Write-Log "Процесс задачи завершился аварийно. Файл результата не был создан." -TaskID $taskData.TaskID -Type 'ERROR'
                    Send-ToastNotification -Title '❌ Крах задачи' -Message "Задача '$($taskRow.Cells['Задача'].Value)' завершилась аварийно." -Type 'Error'
                }
                $temp = $null; $script:runningTasks.TryRemove($procId, [ref]$temp)
                if (-not $script:DebugMode) { if ($crashLog) { Remove-Item -LiteralPath $crashLog.FullName -Force -ErrorAction SilentlyContinue }; Remove-Item -Path (Join-Path $script:wdir "*-$($taskData.TaskID).*"), (Join-Path $script:wdir "$($taskData.TaskID)") -Recurse -Force -ErrorAction SilentlyContinue }
                ProcessTaskQueue
            }
        }
    }

    foreach ($toolName in @($script:activeDownloads.Keys)) {
        $process = $script:activeDownloads[$toolName]
        $statusFile = Join-Path $script:wdir "dl_status_$toolName.txt"
        
        $systemTab = $script:f.Controls.Find("TabPanel_System", $true)
        $grid = $null; if ($systemTab.Count -gt 0) { $grid = $systemTab[0].Controls.Find("systemFilesGrid", $true)[0] }

        if (-not $process.HasExited) {
            if ($grid -and (Test-Path -LiteralPath $statusFile)) {
                try {
                    $statusText = [System.IO.File]::ReadAllText($statusFile).Trim()
                    foreach ($row in $grid.Rows) { if ($row.Cells['Программа'].Value -eq $toolName) { $row.Cells['Статус'].Value = $statusText; break } }
                } catch {
                    Write-Log "Ошибка чтения статуса загрузки '$toolName': $($_.Exception.Message)" -Type 'DEBUG'
                }
            }
        } else {
            Write-Log "Процесс загрузки для '$toolName' завершен."; [void]$script:activeDownloads.Remove($toolName)
            $tempScriptPath = Join-Path $script:wdir "temp_dl_$toolName.ps1"
            if (Test-Path -LiteralPath $tempScriptPath) { Remove-Item -LiteralPath $tempScriptPath -Force -ErrorAction SilentlyContinue }
            if (Test-Path -LiteralPath $statusFile) { Remove-Item -LiteralPath $statusFile -Force -ErrorAction SilentlyContinue }
            if ($grid) { Update-SystemFilesStatus -GridView $grid }
            # После скачивания Complete Pack устанавливаем версию ключей по умолчанию
            if ($toolName -eq 'Complete Pack' -and (Test-Path (Join-Path $script:tdir 'complete_pack.marker'))) {
                try {
                    if (-not $script:settings) { $script:settings = [pscustomobject]@{} }
                    if (-not $script:settings.KeysVersion) {
                        $script:settings = $script:settings | Add-Member -MemberType NoteProperty -Name 'KeysVersion' -Value '22.1.0' -PassThru -Force
                        Save-Settings
                        Write-Log "Версия ключей установлена по умолчанию: 22.1.0" -Type 'SUCCESS'
                        # Копируем ключи во все нужные места
                        $packKeys = Join-Path $script:tdir 'prod.keys'
                        if (Test-Path -LiteralPath $packKeys) {
                            Copy-Item -LiteralPath $packKeys -Destination (Join-Path $script:ndir 'prod.keys') -Force -ErrorAction SilentlyContinue
                            Copy-Item -LiteralPath $packKeys -Destination (Join-Path $script:ndir 'keys.txt') -Force -ErrorAction SilentlyContinue
                            $nscbKeyDir = Join-Path $script:nbdir 'ztools'
                            if (-not (Test-Path -LiteralPath $nscbKeyDir)) { [void](New-Item -ItemType Directory -Force $nscbKeyDir) }
                            Copy-Item -LiteralPath $packKeys -Destination (Join-Path $nscbKeyDir 'keys.txt') -Force -ErrorAction SilentlyContinue
                            Write-Log "Ключи из Complete Pack скопированы во все утилиты." -Type 'SUCCESS'
                        }
                        if ($grid) { Update-SystemFilesStatus -GridView $grid }
                    }
                } catch { Write-Log "Ошибка установки версии ключей: $($_.Exception.Message)" -Type 'WARN' }
            }
        }
    }
    
    if ($script:taskGrid -and $script:taskGrid.Rows.Count -gt 0) {
        $totalTasks = $script:taskGrid.Rows.Count
        $completedTasks = 0
        $errorTasks = 0
        foreach ($row in $script:taskGrid.Rows) {
            $status = $row.Cells['Статус'].Value
            if ($status -eq 'Готово') {
                $completedTasks++
            } elseif ($status -match 'Ошибка') {
                $errorTasks++
            }
        }
        
        $finishedTasks = $completedTasks + $errorTasks
        $percent = 0.0
        if ($totalTasks -gt 0) {
            $percent = ($finishedTasks / $totalTasks) * 100
        }
        
        # Определяем цвет прогресс-бара
        if ($finishedTasks -gt 0) {
            if ($errorTasks -gt 0 -and $completedTasks -eq 0) {
                $script:globalProgressColor = '#C62828'  # Красный - только ошибки
            } elseif ($errorTasks -gt 0 -and $completedTasks -gt 0) {
                $script:globalProgressColor = '#EF6C00'  # Оранжевый - смешанные результаты
            } else {
                $script:globalProgressColor = '#2E7D32'  # Зелёный - только успешные
            }
        } else {
            $script:globalProgressColor = '#2E7D32'  # Зелёный по умолчанию
        }
        
        # Формируем текст
        if ($errorTasks -gt 0 -and $completedTasks -eq 0) {
            $newText = "Ошибки: $errorTasks из $totalTasks ($([math]::Round($percent))%)"
        } elseif ($errorTasks -gt 0) {
            $completedPercent = [math]::Round(($completedTasks / $totalTasks) * 100)
            $errorPercent = [math]::Round(($errorTasks / $totalTasks) * 100)
            $newText = "Выполнено: $completedTasks ($completedPercent%) | Ошибки: $errorTasks ($errorPercent%)"
        } else {
            $newText = "Выполнено: $finishedTasks из $totalTasks ($([math]::Round($percent))%)"
        }
        
        if ($script:globalProgressVal -ne $percent -or $script:globalProgressText -ne $newText) {
            $script:globalProgressVal = $percent
            $script:globalProgressText = $newText
            if ($script:globalProgressPanel -and $script:globalProgressPanel.IsHandleCreated) {
                $script:globalProgressPanel.Invalidate()
            }
        }

    } elseif ($script:globalProgressVal -ne 0) {
        $script:globalProgressVal = 0.0
        $script:globalProgressText = "Ожидание..."
        if ($script:globalProgressPanel -and $script:globalProgressPanel.IsHandleCreated) {
            $script:globalProgressPanel.Invalidate()
        }
    }
}
#====================================================================================
#  БЛОК: СИСТЕМНЫЕ ФАЙЛЫ
#====================================================================================
function Create-SystemFilesTab {
    param($parent)
    $grid = New-Object DataGridView; $grid.Name = 'systemFilesGrid'; $grid.Location = '6, 30';
    $grid.Size = [Size]::new(($parent.ClientSize.Width - 12), ($parent.ClientSize.Height - 36));
    $grid.Anchor = 'Top, Bottom, Left, Right'; $grid.AllowUserToAddRows = $false; $grid.AllowUserToDeleteRows = $false;
    $grid.AllowUserToResizeRows = $false; $grid.RowHeadersVisible = $false;
    $grid.MultiSelect = $false; $grid.SelectionMode = 'FullRowSelect'; $grid.EnableHeadersVisualStyles = $false; $grid.BorderStyle = [BorderStyle]::None;
    $grid.ColumnHeadersBorderStyle = [DataGridViewHeaderBorderStyle]::Single; $grid.GridColor = [ColorTranslator]::FromHtml('#6A6A70');
    $grid.BackgroundColor = [ColorTranslator]::FromHtml('#2D2D30'); $grid.Font = $script:regularFont; $grid.RowTemplate.Height = 24
    $headerStyle = New-Object DataGridViewCellStyle; $headerStyle.BackColor = [ColorTranslator]::FromHtml('#3E3E42');
    $headerStyle.ForeColor = [Color]::White; $headerStyle.Font = New-Object Font('Segoe UI', 9, [FontStyle]::Bold); $headerStyle.Alignment = 'MiddleCenter'
    $grid.ColumnHeadersDefaultCellStyle = $headerStyle; $grid.ColumnHeadersHeight = 30;
    $grid.ColumnHeadersHeightSizeMode = 'DisableResizing'
    $cellStyle = New-Object DataGridViewCellStyle; $cellStyle.BackColor = [ColorTranslator]::FromHtml('#333333'); $cellStyle.ForeColor = [Color]::White; $cellStyle.SelectionBackColor = [ColorTranslator]::FromHtml('#007ACC');
    $cellStyle.SelectionForeColor = [Color]::White
    $grid.DefaultCellStyle = $cellStyle
    $altCellStyle = New-Object DataGridViewCellStyle; $altCellStyle.BackColor = [ColorTranslator]::FromHtml('#3C3C3C');
    $grid.AlternatingRowsDefaultCellStyle = $altCellStyle
    $colHeaders = @{ 'Программа' = 110; 'Версия' = 90; 'Статус' = 170; 'Действие' = 150 }
    foreach($h in $colHeaders.GetEnumerator()){ $c=New-Object DataGridViewTextBoxColumn; $c.Name=$h.Name; $c.HeaderText=$h.Name; $c.Width=$h.Value; $c.ReadOnly=$true; $c.SortMode='NotSortable'; $c.DefaultCellStyle.Alignment='MiddleCenter'; $grid.Columns.Add($c)|Out-Null }
    $grid.Columns['Действие'].AutoSizeMode = 'Fill'
    $parent.Controls.Add($grid)
    $grid.add_CellClick({
        param($s, $e); if($e.RowIndex -lt 0){ return }; $cell = $s.Rows[$e.RowIndex].Cells[$e.ColumnIndex]; if ($cell.OwningColumn.Name -ne 'Действие') { return }; $action = $cell.Value.ToString(); $toolName = $s.Rows[$e.RowIndex].Cells['Программа'].Value
        if ($action -eq 'СКАЧАТЬ') {
            $urls = @{ 'Complete Pack'='https://github.com/ReiKatari/STORM_SWITCH_BOX_TOOLS/releases/download/0.0.3/tools.zip'; 'NSZ'='https://github.com/nicoboss/nsz/releases/download/4.6.1/nsz_v4.6.1_win64_portable.zip';'NSC_Builder'='https://github.com/julesontheroad/NSC_BUILDER/releases/download/1.01b/NSCB_101bx64.zip';'yanu-cli'='https://github.com/nozwock/yanu/releases/download/0.10.1/yanu-cli-x86_64-pc-windows-msvc.exe' }; $url = $urls[$ToolName]; if(-not $url) { return }
            Write-Log "Инициализация загрузки '$toolName'..."
            $scriptBlockContent = @"
param(`$ToolName, `$Url, `$WDirPath, `$NDirPath, `$NBDirPath, `$TDirPath)
function Write-HostLog { param(`$Message, `$Type='INFO', `$Color='White'); Write-Host "[$((Get-Date).ToString('HH:mm:ss'))] [`$Type] `$Message" -ForegroundColor `$Color }
`$statusFile = Join-Path `$WDirPath "dl_status_`$ToolName.txt"; `$tempPath = Join-Path `$WDirPath "dl_`$(Get-Random)"; try { Add-Type -AssemblyName System.IO.Compression.FileSystem; [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13; if (`$ToolName -eq 'Complete Pack') { Write-HostLog "Очистка папки /tools/ мимо корзины..." -Color 'Cyan'; if(Test-Path `$TDirPath){ Get-ChildItem -Path `$TDirPath | Remove-Item -Force -Recurse -ErrorAction SilentlyContinue }; [void](New-Item -ItemType Directory -Force `$TDirPath); `$dlFile = Join-Path `$TDirPath "CompletePack.zip"; } else { [void](New-Item -ItemType Directory -Force `$tempPath); `$dlFile = Join-Path `$tempPath (Split-Path `$Url -Leaf); } Write-HostLog "Начинаю загрузку '`$ToolName'..." -Color 'Yellow'; [System.IO.File]::WriteAllText(`$statusFile, "Скачивание: 0%"); `$req = [System.Net.HttpWebRequest]::Create(`$Url); `$req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) STORMSWITCHBOX/1.0"; `$resp = `$req.GetResponse(); `$total = `$resp.ContentLength; `$stream = `$resp.GetResponseStream(); `$fs = [System.IO.File]::Create(`$dlFile); `$buf = New-Object byte[] 8192; `$read = 0; `$totalRead = 0; `$lastUpdate = [DateTime]::MinValue; while ((`$read = `$stream.Read(`$buf, 0, `$buf.Length)) -gt 0) { `$fs.Write(`$buf, 0, `$read); `$totalRead += `$read; `$now = [DateTime]::Now; if ((`$now - `$lastUpdate).TotalMilliseconds -gt 500) { `$lastUpdate = `$now; if (`$total -gt 0) { `$pct = [math]::Round((`$totalRead / `$total) * 100); [System.IO.File]::WriteAllText(`$statusFile, "Скачивание: `$pct%") } else { `$mb = [math]::Round(`$totalRead / 1MB, 2); [System.IO.File]::WriteAllText(`$statusFile, "Скачивание: `$mb MB") } } }; `$fs.Close(); `$stream.Close(); if(-not(Test-Path -LiteralPath `$dlFile)){throw "Не удалось скачать файл."}; Write-HostLog "Файл скачан." -Color 'Green'; [System.IO.File]::WriteAllText(`$statusFile, "Распаковка..."); switch(`$ToolName){ 'Complete Pack' { Write-HostLog "Распаковка архива в текущую папку..." -Color 'Cyan'; [System.IO.Compression.ZipFile]::ExtractToDirectory(`$dlFile, `$TDirPath); `$innerTools = Join-Path `$TDirPath "tools"; if (Test-Path -LiteralPath `$innerTools) { Move-Item -Path (`$innerTools + "\*") -Destination `$TDirPath -Force; Remove-Item -Path `$innerTools -Force -Recurse }; Remove-Item -LiteralPath `$dlFile -Force; New-Item -ItemType File -Path (Join-Path `$TDirPath 'complete_pack.marker') -Force | Out-Null } 'NSZ' { `$targetDir = `$NDirPath; `$extractPath = Join-Path `$tempPath "extract"; Write-HostLog "Распаковка NSZ..." -Color 'Cyan'; [System.IO.Compression.ZipFile]::ExtractToDirectory(`$dlFile, `$extractPath); `$nszExe = Get-ChildItem -Path `$extractPath -Filter "nsz.exe" -Recurse | Select-Object -First 1; if(`$nszExe){ Move-Item -Path (`$nszExe.Directory.FullName + "\*") -Destination `$targetDir -Force }else{throw "nsz.exe не найден."} } 'NSC_Builder' { `$targetDir = `$NBDirPath; `$extractPath = Join-Path `$tempPath "extract"; Write-HostLog "Распаковка NSC_Builder..." -Color 'Cyan'; [System.IO.Compression.ZipFile]::ExtractToDirectory(`$dlFile, `$extractPath); `$nscbBat = Get-ChildItem -Path `$extractPath -Filter "NSCB.bat" -Recurse | Select-Object -First 1; if(`$nscbBat){ Move-Item -Path (`$nscbBat.Directory.FullName + "\*") -Destination `$targetDir -Force }else{throw "NSCB.bat не найден."} } 'yanu-cli' { Move-Item -LiteralPath `$dlFile -Destination (Join-Path `$TDirPath "yanu-cli.exe") -Force } } } catch { Write-HostLog "ОШИБКА: `$(`$_.Exception.Message)" -Type 'ERROR' -Color 'Red'; [System.IO.File]::WriteAllText(`$statusFile, "Ошибка!"); Start-Sleep -Seconds 10 } finally { if(Test-Path `$tempPath){Remove-Item `$tempPath -Recurse -Force -ErrorAction SilentlyContinue} }
"@
            $tempScriptPath = Join-Path $script:wdir "temp_dl_$($toolName).ps1"; $scriptBlockContent | Out-File -FilePath $tempScriptPath -Encoding utf8
            $psArgs = "-NoProfile -ExecutionPolicy Bypass -File `"$tempScriptPath`" -ToolName `"$toolName`" -Url `"$Url`" -WDirPath `"$($script:wdir)`" -NDirPath `"$($script:ndir)`" -NBDirPath `"$($script:nbdir)`" -TDirPath `"$($script:tdir)`""
            try { $process = Start-Process powershell.exe -ArgumentList $psArgs -PassThru; if ($process) { $script:activeDownloads[$toolName] = $process; $s.Rows[$e.RowIndex].Cells['Действие'].Value = "Выполняется..."; $s.Rows[$e.RowIndex].Cells['Статус'].Value = "Смотрите консоль..." } } catch { Write-Log "Не удалось запустить процесс загрузки для '$toolName': $($_.Exception.Message)" -Type 'ERROR'; Remove-Item -LiteralPath $tempScriptPath -EA SilentlyContinue }
        } elseif (($action -eq 'ОБЗОР' -or $action -eq 'ОБНОВИТЬ') -and $toolName -eq 'Keys') {
            $selectedKeyFile = YEOpenFile -title "Выберите файл prod.keys" -filter "Файл ключей (prod.keys)|prod.keys"
            if ($selectedKeyFile) {
                Write-Log "Выбран файл ключей: $selectedKeyFile"
                try {
                    $version = Show-CustomInputBox -prompt "Введите версию файла ключей:" -title "Версия ключей" -defaultValue ""
                    if(-not [string]::IsNullOrWhiteSpace($version)) {
                        if(-not $script:settings) { $script:settings = [pscustomobject]@{} }
                        Write-Log "Копирование ключей для всех утилит..."; Copy-Item -LiteralPath $selectedKeyFile -Destination (Join-Path $script:tdir "prod.keys") -Force;
                        Copy-Item -LiteralPath $selectedKeyFile -Destination (Join-Path $script:ndir "prod.keys") -Force; Copy-Item -LiteralPath $selectedKeyFile -Destination (Join-Path $script:ndir "keys.txt") -Force; $nscbKeyDir = Join-Path $script:nbdir "ztools";
                        if(-not(Test-Path -LiteralPath $nscbKeyDir)){[void](New-Item -ItemType Directory -Force $nscbKeyDir)}; Copy-Item -LiteralPath $selectedKeyFile -Destination (Join-Path $nscbKeyDir "keys.txt") -Force
                        if ($script:settings -isnot [System.Management.Automation.PSCustomObject]) { $script:settings = [pscustomobject]@{} }
                        $script:settings = $script:settings | Add-Member -MemberType NoteProperty -Name 'KeysVersion' -Value $version -PassThru -Force; Save-Settings
                        Write-Log "Файлы ключей версии '$version' успешно скопированы." -Type 'SUCCESS'; Update-SystemFilesStatus -GridView $s
                    } else { Write-Log "Установка ключей отменена пользователем." }
                } catch { Write-Log "Ошибка при копировании файлов ключей: $($_.Exception.Message)" -Type 'ERROR'; YEmsg "Ошибка при копировании файлов ключей:`n$($_.Exception.Message)" "OK" $script:f }
            }
        }
    })
    Update-SystemFilesStatus -GridView $grid
}
function Update-SystemFilesStatus {
    param($GridView)
    if ($GridView.InvokeRequired) { $GridView.Invoke([Action[object]]{ param($gv) Update-SystemFilesStatus -GridView $gv }, @($GridView)); return }
    $systemFiles = @( @{Name='Complete Pack'; Path=(Join-Path $script:tdir 'complete_pack.marker')}, @{Name='Keys'; Path=$script:key} )
    $GridView.Rows.Clear()
    $completePackInstalled = Test-Path -LiteralPath (Join-Path $script:tdir 'complete_pack.marker')
    foreach ($file in $systemFiles) {
        $isInstalled = $false;
        if($file.Name -eq 'Keys'){
            $isInstalled = Test-Path -LiteralPath $file.Path
        } else {
            $isInstalled = Test-Path -LiteralPath $file.Path
            if ($file.SecondaryPath -and -not (Test-Path -LiteralPath $file.SecondaryPath)) { $isInstalled = $false }
        }
        $status=if($isInstalled){'Установлено'}else{'Отсутствует'}; $actionText = ""
        if($file.Name -eq 'Keys') { if ($isInstalled) { $actionText = 'ОБНОВИТЬ' } else { $actionText = 'ОБЗОР' } } else { if ($isInstalled) { $actionText = '✔' } else { $actionText = 'СКАЧАТЬ' } }
        if($script:activeDownloads.ContainsKey($file.Name)){ $status='Смотрите консоль...'; $actionText='Выполняется...' }
        $version='-';
        if($isInstalled){ try{ $version=switch($file.Name){ 'Complete Pack'{ if($script:settings -and $script:settings.KeysVersion) {$script:settings.KeysVersion} else {'22.1.0'} } 'Keys'{ if($script:settings -and $script:settings.KeysVersion) {$script:settings.KeysVersion} else {'-'} } } }catch{ Write-Log "Ошибка определения версии '$($file.Name)': $($_.Exception.Message)" -Type 'DEBUG' } }
        $rowIndex=$GridView.Rows.Add($file.Name,$version,$status,$actionText)
        $GridView.Rows[$rowIndex].Cells['Статус'].Style.ForeColor=if($status-eq'Установлено'){[Color]::LightGreen}elseif($status-eq'Отсутствует' -or $status-eq'Ошибка установки' -or $status -eq 'Сначала установите утилиты'){[Color]::LightCoral}else{[Color]::White}
        if($actionText -in @('СКАЧАТЬ', 'ОБЗОР', 'ОБНОВИТЬ')){ $linkStyle=New-Object DataGridViewCellStyle; $linkStyle.ForeColor=[Color]::DodgerBlue; $linkStyle.Font=New-Object Font($script:regularFont,'Underline'); $GridView.Rows[$rowIndex].Cells['Действие'].Style=$linkStyle }
    }
    Update-NavButtonState
}
function Update-NavButtonState {
    if ($script:f.InvokeRequired) { $script:f.Invoke([Action]{ Update-NavButtonState }); return }
    $script:nsz_f = Test-Path -LiteralPath $script:nsz_exe; $script:nscb_f = (Test-Path -LiteralPath $script:nscb_bat) -and (Test-Path -LiteralPath $script:squirrel_exe); $script:yanu_f = Test-Path -LiteralPath $script:yanu_cli;
    $script:htn_f = -not [string]::IsNullOrEmpty($script:hactoolnet); $keys_f = Test-Path -LiteralPath $script:key
    $allYanuToolsFound = $script:yanu_f -and $keys_f -and $script:htn_f
    $navGroupBox = $script:f.Controls.Find("navGroupBox", $true)[0]
    foreach ($button in $navGroupBox.Controls) {
        if ($button -is [Button] -and $button.Name -like "NavButton_*") {
            $buttonName = $button.Tag; $shouldBeEnabled = $false
            switch($buttonName) { 'Update' { $shouldBeEnabled = $allYanuToolsFound } 'Unpack' { $shouldBeEnabled = $allYanuToolsFound } 'Pack' { $shouldBeEnabled = $allYanuToolsFound } 'Convert' { $shouldBeEnabled = $script:nsz_f } 'Multi' { $shouldBeEnabled = $script:nscb_f -and $allYanuToolsFound } default { $shouldBeEnabled = $true } }
            $button.Enabled = $shouldBeEnabled
            if (-not $button.Enabled) { 
                $button.BackColor = [ColorTranslator]::FromHtml('#333333'); 
                $button.FlatAppearance.MouseOverBackColor = [ColorTranslator]::FromHtml('#333333') 
            } else {
                $tabPanels = $script:f.Controls | Where-Object { $_.Name -eq "TabPanel_$buttonName" }
                $isActive = ($tabPanels.Count -gt 0 -and $tabPanels[0].Visible)
                if ($isActive) {
                    $button.BackColor = [ColorTranslator]::FromHtml('#007ACC')
                    $button.FlatAppearance.MouseOverBackColor = [ColorTranslator]::FromHtml('#1A8CD8')
                } else {
                    switch ($buttonName) {
                        'System' { $button.BackColor = [Color]::FromArgb(255, 75, 0, 130); $button.FlatAppearance.MouseOverBackColor = [ColorTranslator]::FromHtml('#5E1299') }
                        'Settings' { $button.BackColor = [ColorTranslator]::FromHtml('#1ABC9C'); $button.FlatAppearance.MouseOverBackColor = [ColorTranslator]::FromHtml('#2DCEAE') }
                        default { $button.BackColor = [ColorTranslator]::FromHtml('#3E3E42'); $button.FlatAppearance.MouseOverBackColor = [ColorTranslator]::FromHtml('#4E4E52') }
                    }
                }
            }
        }
    }
    foreach ($outputControl in $script:outputControls.Values) {
        foreach ($fmtButton in $outputControl.FormatButtons) {
            $isEnabled = $true
            if ($fmtButton.Name -in 'NSZ', 'XCZ') { if (-not $script:nsz_f) { $isEnabled = $false } }
            if ($fmtButton.Name -in 'XCI', 'XCZ') { if (-not $script:nscb_f) { $isEnabled = $false } }
            $fmtButton.Enabled = $isEnabled
            if (-not $isEnabled) { $fmtButton.BackColor = [ColorTranslator]::FromHtml('#333333'); $fmtButton.FlatAppearance.MouseOverBackColor = [ColorTranslator]::FromHtml('#333333') } elseif ($outputControl.SelectedFormat -ne $fmtButton.Name) { $fmtButton.BackColor = [ColorTranslator]::FromHtml('#3E3E42'); $fmtButton.FlatAppearance.MouseOverBackColor = [ColorTranslator]::FromHtml('#4E4E52') }
        }
    }
}
YEStart
}

