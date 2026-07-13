<# ::0.1.000
@echo off&pushd "%~dp0"&set "arg1=%~1"&set "arg2=%~2"&powershell -WindowStyle Minimized -ExecutionPolicy Bypass -c "iex ((Get-Content '%~f0' -Encoding utf8) -join [Environment]::Newline);YE"&exit
#>

# Упрощение имен типов для улучшения читаемости
using namespace System.Windows.Forms
using namespace System.Drawing
using namespace System.Drawing.Drawing2D
using namespace System.IO

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
    public const uint WM_DROPFILES = 0x0233;
    public const uint WM_COPYDATA = 0x004A;
    public const uint WM_COPYGLOBALDATA = 0x0049;
    public const uint MSGFLT_ALLOW = 1;
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

# Ультра-современный заголовок консоли воркера (на всю ширину)
function Write-Header {
    param($Title, $Subtitle, $AppVersion, $OutputInfo)
    Clear-Host
    
    # Устанавливаем заголовок окна
    $Host.UI.RawUI.WindowTitle = "STORM SWITCH BOX - WORKER"
    $script:headerStartTime = Get-Date
    
    # Получаем ширину консоли (минус 2 для отступов)
    $consoleWidth = $Host.UI.RawUI.WindowSize.Width - 2
    if ($consoleWidth -lt 60) { $consoleWidth = 80 }
    $innerWidth = $consoleWidth - 2  # Минус рамка слева и справа
    
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
    } catch {}
    
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
            $title = if ($info.Title) { $info.Title } else { 
                $fName = [System.IO.Path]::GetFileNameWithoutExtension($originalFileName)
                ($fName -split '\[')[0].Trim() 
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
    param($procInfo, $title, $maxRetries = 3)
    $fullCommand = "`"$($procInfo.Exe)`" $($procInfo.Args)"
    Write-WorkerLog -Message ">> $title" -Type 'INFO'
    
    $lastError = $null
    for ($attempt = 1; $attempt -le $maxRetries; $attempt++) {
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
            if ($exitCode -ne 0) { 
                throw "Утилита '$([System.IO.Path]::GetFileName($procInfo.Exe))' завершилась с кодом $exitCode."
            }
            return $exitCode
        } catch { 
            $lastError = $_
            if ($attempt -lt $maxRetries) {
                $delay = $attempt * 2  # 2s, 4s, 6s
                Write-WorkerLog "Попытка $attempt/$maxRetries не удалась. Повтор через ${delay}с..." -Type 'WARN'
                Start-Sleep -Seconds $delay
            }
        }
    }
    
    Write-WorkerLog "ОШИБКА '$title': $($lastError.Exception.Message)" -Type 'ERROR'
    throw $lastError
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
    $isolatedPaths = Prepare-IsolatedTool -toolName $toolName -taskTempDir $tempDir -originalToolPaths $toolPaths
    
    if (Test-Path -LiteralPath $toolPaths.key) {
        try {
            $keyDir = Split-Path $isolatedPaths.KeyFile -Parent
            if (-not (Test-Path $keyDir)) { New-Item -ItemType Directory -Path $keyDir -Force }
            
            Copy-Item -LiteralPath $toolPaths.key -Destination $isolatedPaths.KeyFile -Force
            
            if ($toolName -eq 'nscb' -and $isolatedPaths.RootDir) { 
                Copy-Item -LiteralPath $toolPaths.key -Destination (Join-Path $isolatedPaths.RootDir "keys.txt") -Force -EA SilentlyContinue 
            }
        } catch { 
            Write-WorkerLog "Ошибка копирования ключей: $($_.Exception.Message)" -Type 'WARN' 
        }
    }
    return $isolatedPaths
}

function Convert-To-NspIfNeeded {
    param($filePath, $convertDir, $isolatedNsz, $useCores=$null, $needIsolation=$false)
    
    # [FIX] Умная проверка пути
    $realPath = $filePath
    if (-not (Test-Path -LiteralPath $realPath)) {
        $cleanPath = $filePath -replace '^\\\\\\?\\', ''
        if (Test-Path -LiteralPath $cleanPath) { $realPath = $cleanPath } 
        else { throw "Файл не найден: $filePath" }
    }
    
    $fileInfo = Get-Item -LiteralPath $realPath
    $ext = $fileInfo.Extension.ToLower()
    $safeName = "src_" + [guid]::NewGuid().ToString("N").Substring(0, 8)
    $targetNspPath = Join-Path $convertDir ($safeName + ".nsp")
    
    # Путь к файлу для обработки (оригинал или копия)
    $workingFilePath = $realPath
    $isolatedSourcePath = $null
    
    # УМНОЕ КОПИРОВАНИЕ: копируем ТОЛЬКО если файл используется несколькими задачами
    if ($needIsolation) {
        $isolatedSourceDir = Join-Path $convertDir "sources"
        if (-not (Test-Path $isolatedSourceDir)) { 
            New-Item -ItemType Directory -Path $isolatedSourceDir -Force | Out-Null 
        }
        
        $isolatedSourcePath = Join-Path $isolatedSourceDir ($safeName + $ext)
        
        # Копируем с повторными попытками
        $copySuccess = $false
        for ($attempt = 1; $attempt -le 5; $attempt++) {
            try {
                Write-WorkerLog "Копирование исходника (файл разделяемый, попытка $attempt)..." -Type 'INFO'
                Copy-Item -LiteralPath $realPath -Destination $isolatedSourcePath -Force -ErrorAction Stop
                $copySuccess = $true
                break
            } catch {
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
        $coresArg = if ($useCores) { "-t $useCores" } else { "" }
        $convProc = @{ Exe = $isolatedNsz.Exe; Args = "$coresArg -D `"$workingFilePath`" -o `"$convertDir`""; WorkingDir = $isolatedNsz.WorkingDir }
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
        if ($needIsolation -and $isolatedSourcePath) {
            Rename-Item -LiteralPath $isolatedSourcePath -NewName ($safeName + ".nsp") -Force
            return $targetNspPath
        } else {
            # Без изоляции - стандартная логика
            $sourceRoot = [System.IO.Path]::GetPathRoot($realPath)
            $destRoot = [System.IO.Path]::GetPathRoot($convertDir)
            $isDifferentVolume = ($sourceRoot -and $destRoot -and ($sourceRoot.ToLower() -ne $destRoot.ToLower()))
            $hasBadChars = $realPath -match '\[.*\]'; $isTooLong = $realPath.Length -gt 240
            if (-not $hasBadChars -and -not $isTooLong -and -not $isDifferentVolume) { return $realPath }
            Write-WorkerLog "Подготовка: Создание безопасной копии..." -Type 'INFO'
            if ($isDifferentVolume) { Copy-Item -LiteralPath $realPath -Destination $targetNspPath -Force } 
            else { try { New-Item -ItemType HardLink -Path $targetNspPath -Value $realPath -Force -ErrorAction Stop | Out-Null } catch { Copy-Item -LiteralPath $realPath -Destination $targetNspPath -Force } }
            return $targetNspPath
        }
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
        
        # Ищем все уникальные Title ID в выводе
        $titleIdRegex = '([0-9A-Fa-f]{16})'
        $allMatches = [regex]::Matches($output, $titleIdRegex)
        $uniqueTitleIds = @($allMatches | ForEach-Object { $_.Groups[1].Value.ToUpper() } | Sort-Object -Unique)
        
        # Если найдено более 1 уникального Title ID - файл сшитый
        # Также проверяем наличие слов "Update" или "DLC" в выводе
        $hasUpdate = $output -match '(?i)\bUpdate\b'
        $hasDLC = $output -match '(?i)\bDLC\b'
        
        $isStitched = ($uniqueTitleIds.Count -gt 1) -or $hasUpdate -or $hasDLC
        
        return @{
            IsStitched = $isStitched
            TitleIds = $uniqueTitleIds
            HasUpdate = $hasUpdate
            HasDLC = $hasDLC
            BaseTitleId = if ($uniqueTitleIds.Count -gt 0) { 
                # Базовый Title ID обычно заканчивается на 000
                $uniqueTitleIds | Where-Object { $_ -match '000$' } | Select-Object -First 1
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
        $controlNcaFile = (Get-ChildItem -LiteralPath $pdata -Filter *.nca -Recurse | ForEach-Object { 
            if (& $toolPaths.hactoolnet "-k" $toolPaths.key $_.FullName 2>$null | Select-String 'Control' -Quiet) { $_; return } 
        } | Select-Object -First 1)
        
        if (-not $controlNcaFile) {
            Write-WorkerLog "Не найден control.nca в распакованном файле" -Type 'ERROR'
            return $null
        }
        
        $titleId = (& $toolPaths.hactoolnet "-k" $toolPaths.key $controlNcaFile.FullName | Select-String 'TitleID').ToString().Split(':')[-1].Trim()
        
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
    $script:title = 'STORM SWITCH BOX (0.1.007)'
    $yanu_rec = '0.10.1'

    # (Req 2) Определение количества ядер CPU
    $script:MaxCores = [System.Environment]::ProcessorCount
    if ($script:MaxCores -lt 1) { $script:MaxCores = 1 }
    $script:DefaultCores = $script:MaxCores

    $script:boldFont = New-Object Font('Century Gothic', 9, [FontStyle]::Bold)
    $script:regularFont = New-Object Font('Century Gothic', 9)
    $script:smallFont = New-Object Font('Century Gothic', 8)
    $script:buttons = 'Обновление', 'Распаковка', 'Упаковка', 'Конвертация', 'Создание мульти-контента', '', 'Системные файлы', 'Настройки'
    $script:button_names = 'Update', 'Unpack', 'Pack', 'Convert', 'Multi', '', 'System', 'Settings'
    $script:logStorage = [System.Collections.Concurrent.ConcurrentDictionary[string, object]]::new()
    $script:generalLog = [System.Collections.Generic.List[string]]::new()
    $script:activeDownloads = @{}
    $script:runningTasks = [System.Collections.Concurrent.ConcurrentDictionary[int, object]]::new()

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

    if ((0 -in (Test-Path $tdir, $ndir, $nbdir, $wdir, $odir))) {
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
    } catch {}
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
    
    # Обводка
    $pnlBorder = New-Object System.Windows.Forms.Panel
    $pnlBorder.Size = [System.Drawing.Size]::new(400, 150); $pnlBorder.Location = [System.Drawing.Point]::new(0, 0)
    $pnlBorder.BorderStyle = [System.Windows.Forms.BorderStyle]::FixedSingle
    # $msgForm.Controls.Add($pnlBorder) 
    
    [void]$msgForm.ShowDialog()
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
    
    # МГНОВЕННОЕ ОБНОВЛЕНИЕ: Обновляем блок логов сразу после добавления записи
    if ($script:logBox -and $script:logBox.IsHandleCreated -and -not $script:logBox.IsDisposed) {
        try {
            if ($script:logBox.InvokeRequired) {
                $script:logBox.Invoke([Action]{ Update-LogView })
            } else {
                Update-LogView
            }
        } catch {
            # Игнорируем ошибки при обновлении UI (например, если форма закрывается)
        }
    }
}

# (Req 6) Переработанная функция Update-LogView для реализации селективного логирования
function Update-LogView {
    if (-not $script:logBox -or $script:logBox.IsDisposed) { return }
    $logBox = $script:logBox
    
    # Запоминаем, был ли скролл внизу
    $isAtBottom = ($logBox.GetPositionFromCharIndex($logBox.TextLength).Y - $logBox.ClientSize.Height - $logBox.AutoScrollOffset.Y) -lt 50
    
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
            # Добавляем общий лог (системные сообщения)
            $logSource.AddRange($script:generalLog)
            
            # Добавляем логи всех существующих задач
            foreach ($key in $script:logStorage.Keys) {
                $logSource.AddRange($script:logStorage[$key])
            }
        }
        
        # 2. Сортировка по времени (формат [HH:mm:ss.fff])
        $logSource.Sort({
            param($a, $b)
            if ($a.Length -gt 12 -and $b.Length -gt 12) {
                # Сравниваем только временную метку в начале строки
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
        $line = $logSource[$i]
        $match = [regex]::Match($line, "^(\[.*?\])\s(\[.*?\])\s(.*)")
        if ($match.Success) {
            # Время
            $logBox.SelectionColor = [Color]::Gray
            $logBox.AppendText($match.Groups[1].Value + " ") 
            
            # Тип (INFO, ERROR и т.д.) с раскраской
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
            
            # Текст сообщения
            $logBox.SelectionColor = [Color]::White
            $logBox.AppendText($match.Groups[3].Value + [Environment]::NewLine) 
        } else {
            # Если строка не стандартного формата
            $logBox.SelectionColor = [Color]::White
            $logBox.AppendText($line + [Environment]::NewLine)
        }
    }

    $logBox.ResumeLayout()
    
    # Автопрокрутка вниз, если были внизу
    if ($isAtBottom) {
        $logBox.ScrollToCaret()
    }
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
            # ИСПРАВЛЕНИЕ: Используем стандартный рекурсивный подсчет для гарантии получения размера
            $measure = Get-ChildItem -LiteralPath $itemInfo.FullName -Recurse -File -Force -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum
            if ($measure -and $measure.Sum) {
                $totalSize = $measure.Sum
            }
        } elseif ($itemInfo -is [System.IO.FileInfo]) {
            $totalSize = $itemInfo.Length
        }
    } catch {
        Write-Log "Не удалось определить размер для '$($itemInfo.FullName)'. Ошибка: $($_.Exception.Message)" -Type 'WARN'
    }
    return [long]$totalSize
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
    $inputBox.FormBorderStyle = [FormBorderStyle]::FixedDialog
    $inputBox.MaximizeBox = $false
    $inputBox.MinimizeBox = $false
    $inputBox.StartPosition = [FormStartPosition]::CenterParent
    $inputBox.Text = $title
    $inputBox.Size = New-Object Size(400, 180)
    
    $okButton = New-Object Button
    $cancelButton = New-Object Button
    $inputBox.AcceptButton = $okButton
    $inputBox.CancelButton = $cancelButton
    
    $promptLabel = New-Object Label
    $promptLabel.Text = $prompt
    $promptLabel.Font = $script:regularFont
    $promptLabel.Location = '15, 15'
    $promptLabel.AutoSize = $true
    $inputBox.Controls.Add($promptLabel)
    
    $textBox = New-Object TextBox
    $textBox.Font = $script:regularFont
    $textBox.Location = '15, 40'
    $textBox.Size = '350, 25'
    $textBox.Text = $defaultValue
    $inputBox.Controls.Add($textBox)
    
    $okButton.Text = 'OK'
    $okButton.DialogResult = [DialogResult]::OK
    $okButton.Font = $script:boldFont
    $okButton.BackColor = [ColorTranslator]::FromHtml('#3E3E42')
    $okButton.ForeColor = [Color]::White
    $okButton.FlatStyle = [FlatStyle]::Flat
    $okButton.FlatAppearance.BorderSize = 0
    $okButton.Size = '100, 30'
    $okButton.Location = '80, 90'
    $inputBox.Controls.Add($okButton)
    
    $cancelButton.Text = 'Отмена'
    $cancelButton.DialogResult = [DialogResult]::Cancel
    $cancelButton.Font = $script:boldFont
    $cancelButton.BackColor = [ColorTranslator]::FromHtml('#3E3E42')
    $cancelButton.ForeColor = [Color]::White
    $cancelButton.FlatStyle = [FlatStyle]::Flat
    $cancelButton.FlatAppearance.BorderSize = 0
    $cancelButton.Size = '100, 30'
    $cancelButton.Location = '200, 90'
    $inputBox.Controls.Add($cancelButton)
    
    if($title -eq "Версия ключей"){
        $textBox.Add_TextChanged({
            param($s, $e)
            $tb = $s
            $startText = $tb.Text
            $startCursor = $tb.SelectionStart
            $currentText = $startText -replace '\D',''
            if ($currentText.Length -gt 6) { $currentText = $currentText.Substring(0, 6) }
            $formattedText = $currentText
            if ($currentText.Length -gt 3) {
                $formattedText = "{0}.{1}.{2}" -f $currentText.Substring(0,2), $currentText.Substring(2,1), $currentText.Substring(3)
            } elseif ($currentText.Length -gt 2) {
                $formattedText = "{0}.{1}" -f $currentText.Substring(0,2), $currentText.Substring(2,1)
            }
            if ($startText -ne $formattedText) {
                $tb.Text = $formattedText
                try { $tb.SelectionStart = $startCursor + ($formattedText.Length - $startText.Length) } catch {}
            }
        })
    }
    
    if ($inputBox.ShowDialog($script:f) -eq 'OK') {
        return $textBox.Text
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
function YEDDHandler_Advanced {
    param($listBox, $unpackMode = $false)
    $existingItems = @($listBox.Items)
    $newFoundItems = [System.Collections.Generic.List[FileSystemInfo]]::new()
    
    # --- Внутренняя функция получения ID через nsz (Fallback) ---
    function Local-GetTitleIdFromNsz {
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
            $process.WaitForExit()
            
            $idRegex = '(?i)(?:Title\s*ID:|titleId\s*=?)\s*([0-9A-Fa-f]{16})'
            $allMatches = [regex]::Matches($output, $idRegex)
            $titleIds = @()
            foreach ($m in $allMatches) { $titleIds += $m.Groups[1].Value.ToUpper() }
            
            if ($titleIds.Count -eq 0) { return $null }
            # Возвращаем ID. Ищем Base (000) для группировки, иначе Update (800), иначе первый.
            $baseId = $titleIds | Where-Object { $_.EndsWith('000') } | Select-Object -First 1
            if ($baseId) { return $baseId }
            $updateId = $titleIds | Where-Object { $_.EndsWith('800') } | Select-Object -First 1
            if ($updateId) { return $updateId }
            return $titleIds[0]
        } catch { return $null }
    }

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
                } catch {}

                if (-not $item) { continue }

                if ($isFolder) {
                    Get-ChildItem -LiteralPath $item.FullName -Recurse -File -Force -ErrorAction SilentlyContinue |
                        Where-Object { $_.Name -match '\.(nsp|nsz|xci|xcz)$' } | ForEach-Object { $newFoundItems.Add($_) }
                        
                    if (-not $isRestrictedMode) {
                        Get-ChildItem -LiteralPath $item.FullName -Recurse -Directory -Force -ErrorAction SilentlyContinue |
                            Where-Object { $_.Name -in 'romfs', 'exefs' } | ForEach-Object { $newFoundItems.Add($_) }
                    }
                } else {
                    if ($item.Name -match '\.(nsp|nsz|xci|xcz)$') {
                        $newFoundItems.Add($item)
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
            # 1. Быстрый поиск в имени
            if ($f.Name -match '\[([0-9a-fA-F]{16})\]') {
                $tId = $matches[1].ToUpper()
            } else {
                # 2. Медленный поиск через nsz (только если нет в имени)
                Write-Log "Анализ ID файла: $($f.Name)..." -Type 'DEBUG'
                $tId = Local-GetTitleIdFromNsz -FilePath $f.FullName
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

    # --- Внутренняя функция получения ID через nsz ---
    function Local-GetTitleIdFromNsz {
        param([string]$FilePath)
        if (-not (Test-Path -LiteralPath $FilePath)) { return $null }
        if (-not (Test-Path -LiteralPath $script:nsz_exe)) { return $null }
        try {
            $startInfo = New-Object System.Diagnostics.ProcessStartInfo
            $startInfo.FileName = $script:nsz_exe
            # Используем --info для получения данных
            $startInfo.Arguments = "--info -p `"$FilePath`""
            $startInfo.RedirectStandardOutput = $true
            $startInfo.RedirectStandardError = $true
            $startInfo.UseShellExecute = $false
            $startInfo.CreateNoWindow = $true
            $startInfo.StandardOutputEncoding = [System.Text.Encoding]::UTF8
            $process = [System.Diagnostics.Process]::Start($startInfo)
            $output = $process.StandardOutput.ReadToEnd()
            $err = $process.StandardError.ReadToEnd() # Читаем ошибки на всякий случай
            $process.WaitForExit()
            
            # Объединяем вывод, иногда инфо падает в stderr в старых версиях
            $fullOutput = $output + "`n" + $err

            # 1. Поиск Title ID (универсальный паттерн)
            # Ищет "Title ID: 01..." ИЛИ "titleId = 01..." ИЛИ "titleId=01..."
            $idRegex = '(?i)(?:Title\s*ID:|titleId\s*=?)\s*([0-9A-Fa-f]{16})'
            $allMatches = [regex]::Matches($fullOutput, $idRegex)
            
            $titleIds = @()
            foreach ($m in $allMatches) { 
                $val = $m.Groups[1].Value.ToUpper()
                if ($val -notin $titleIds) { $titleIds += $val }
            }
            
            if ($titleIds.Count -eq 0) { return $null }
            if ($titleIds.Count -eq 1) { return $titleIds[0] }
            
            # 2. Логика выбора: если найдено несколько ID
            # Приоритет: сначала ищем Base (000) для правильной группировки сшитых файлов с обновлениями,
            # затем Update (800), иначе берем первый попавшийся (DLC)
            
            # Сначала ищем Base (кончается на 000) - для группировки с обновлениями
            $baseId = $titleIds | Where-Object { $_.EndsWith('000') } | Select-Object -First 1
            if ($baseId) { return $baseId }

            # Если нет Base, ищем Update (кончается на 800)
            $updateId = $titleIds | Where-Object { $_.EndsWith('800') } | Select-Object -First 1
            if ($updateId) { return $updateId }

            # Если ничего специфичного, возвращаем первый найденный (обычно для DLC)
            return $titleIds[0]

        } catch { return $null }
    }

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
                    $originalTid = Local-GetTitleIdFromNsz -FilePath $file.FullName
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
            } catch {}
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
                    else { if (-not $isRestrictedMode) { $dlcs += $file } } # Keep redundant updates as DLCs in multi mode? better safely ignore or add
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
                # Mods
                if ($base) {
                    $baseDir = $base.DirectoryName
                    $checkDir = $baseDir.TrimEnd('\') + '\'
                    $mods = $allModFolders | Where-Object { $_.FullName.StartsWith($checkDir, [System.StringComparison]::OrdinalIgnoreCase) }
                    foreach ($m in $mods) {
                         $listBox.Items.Add([PSCustomObject]@{ Item = $m; Type = $m.Name.ToUpper(); DisplayString = "[$($m.Name.ToUpper())$suffix] $($m.FullName)"; GameGroupKey = $gameGroupKey })
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
    } catch {}
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
        $fmtBtn.Location = [Point]::new((9 + ($fmtIndex * ([int]$formatButtonWidth + 5))), (340 + $yOffset))
        $fmtBtn.Font = $script:boldFont
        $fmtBtn.Name = $_
        $fmtBtn.BackColor = [ColorTranslator]::FromHtml('#3E3E42')
        $fmtBtn.ForeColor = [Color]::White
        $fmtBtn.FlatStyle = [FlatStyle]::Flat
        $fmtBtn.FlatAppearance.BorderSize = 0
        
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
                        $_.BackColor = if ($_.Name -eq $currentControls.SelectedFormat) { $activeColor } else { $inactiveColor }
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
    
    $lblOutFolder = New-Object Label
    $lblOutFolder.Location = '9,' + (370 + $yOffset)
    $lblOutFolder.Text = 'Выходная папка'
    $lblOutFolder.Font = $script:boldFont
    $lblOutFolder.AutoSize = 1
    
    $txtOutFolder = New-Object TextBox
    $txtOutFolder.Location = '9,' + (390 + $yOffset)
    $txtOutFolder.Size = [Size]::new([int]($lbWidth - 90), 20)
    $txtOutFolder.Font = $script:regularFont
    $txtOutFolder.BackColor = [ColorTranslator]::FromHtml('#333333')
    $txtOutFolder.ForeColor = [Color]::White
    $txtOutFolder.BorderStyle = [BorderStyle]::FixedSingle
    $txtOutFolder.AllowDrop = $true
    
    $txtOutFolder.Add_DragEnter({ if ($_.Data.GetData("FileDrop") | % { (Get-Item -LiteralPath $_).PSIsContainer }) { $_.Effect = 'Copy' } else { $_.Effect = 'None' } })
    $txtOutFolder.Add_DragDrop({ if (($path=$_.Data.GetData("FileDrop")[0]) -and (Get-Item -LiteralPath $path).PSIsContainer) { $this.Text = $path } })
    $txtOutFolder.Add_TextChanged({ Save-Settings })
    Add-CtrlA-Handler $txtOutFolder
    $controls.TxtOutFolder = $txtOutFolder
    
    $btnBrowseFolder = New-Object Button
    $btnBrowseFolder.Text = 'Обзор'
    $btnBrowseFolder.Size = '85, 24'
    $btnBrowseFolder.Location = [Point]::new([int]($txtOutFolder.Right + 5), (389 + $yOffset))
    $btnBrowseFolder.Font = $script:boldFont
    $btnBrowseFolder.BackColor = [ColorTranslator]::FromHtml('#3E3E42')
    $btnBrowseFolder.ForeColor = [Color]::White
    $btnBrowseFolder.FlatStyle = [FlatStyle]::Flat
    $btnBrowseFolder.FlatAppearance.BorderSize = 0
    $btnBrowseFolder.Add_Click({ if ($selectedFolder = YEFolder) { $txtOutFolder.Text = $selectedFolder } })
    
    $lblOutFile = New-Object Label
    $lblOutFile.Location = '9,' + (415 + $yOffset)
    $lblOutFile.Text = 'Выходное имя файла (пусто = авто)'
    $lblOutFile.Font = $script:boldFont
    $lblOutFile.AutoSize = 1
    
    $txtOutFile = New-Object TextBox
    $txtOutFile.Location = '9,' + (435 + $yOffset)
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
                     $this.Text = (New-Object System.IO.DirectoryInfo($p)).Name
                } elseif ([System.IO.File]::Exists($p)) {
                     $this.Text = (New-Object System.IO.FileInfo($p)).Name
                }
            } catch {}
        }
    })
    
    Add-CtrlA-Handler $txtOutFile
    $controls.TxtOutFile = $txtOutFile
    
    $parent.Controls.AddRange(@($lblOutFolder, $txtOutFolder, $lblOutFile, $txtOutFile, $btnBrowseFolder, $lblOutFormat) + $controls.FormatButtons)
    return $controls
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

    $script:f.Add_LocationChanged({ if((-not $script:suppressSave) -and ($script:f.WindowState -eq 'Normal')) { Save-Settings } })
    $script:f.Add_SizeChanged({ if((-not $script:suppressSave) -and ($script:f.WindowState -eq 'Normal')) { Save-Settings } })

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
    $taskGroupBox.Font = New-Object Font('Century Gothic', 8.5, [FontStyle]::Bold)
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
        $b.FlatAppearance.BorderSize = 0
        switch ($currentButtonName) {
            'System' { $b.BackColor = [Color]::FromArgb(255, 75, 0, 130) }
            'Settings' { $b.BackColor = [ColorTranslator]::FromHtml('#1ABC9C') }
            default { $b.BackColor = [ColorTranslator]::FromHtml('#3E3E42') }
        }
        $b.Add_Click({
            $clickedButtonTag = $this.Tag
            $navGB = $script:f.Controls.Find("navGroupBox", $true)[0]
            foreach ($navBtn in $navGB.Controls) {
                if ($navBtn -is [Button] -and $navBtn.Name -like "NavButton_*") {
                    if ($navBtn.Enabled) {
                        switch ($navBtn.Tag) {
                            'System' { $navBtn.BackColor = [Color]::FromArgb(255, 75, 0, 130) }
                            'Settings' { $navBtn.BackColor = [ColorTranslator]::FromHtml('#1ABC9C') }
                            default { $navBtn.BackColor = [ColorTranslator]::FromHtml('#3E3E42') }
                        }
                    }
                }
            }
            $this.BackColor = [ColorTranslator]::FromHtml('#007ACC')
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
        $gb.Visible = if ($i -eq 0) { 1 } else { 0 }
        $gb.ForeColor = [Color]::White
        $gb.Add_Click({ $script:taskGrid.ClearSelection() })
        $script:f.Controls.Add($gb)
        $lbWidth = $gb.ClientSize.Width - 18

        if ($currentButtonName -eq 'Update') {
            $outputControlsHeight = 145; $outputControlsTopY = $gb.ClientSize.Height - $outputControlsHeight - 10
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
            $outputSectionTopY = $gb.ClientSize.Height - 65
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

            $script:txtOutFolder_unpack = New-Object TextBox; $script:txtOutFolder_unpack.Location = "9, $($outputSectionTopY + 22)";
            $script:txtOutFolder_unpack.Size = [Size]::new([int]($lbWidth - 90), 20);
            $script:txtOutFolder_unpack.Font = $script:regularFont; $script:txtOutFolder_unpack.BackColor = [ColorTranslator]::FromHtml('#333333')
            $script:txtOutFolder_unpack.ForeColor = [Color]::White
            $script:txtOutFolder_unpack.BorderStyle = [BorderStyle]::FixedSingle
            $script:txtOutFolder_unpack.AllowDrop = $true
            $script:txtOutFolder_unpack.Text = $script:defaultOutPaths['Unpack']
            $script:txtOutFolder_unpack.Add_TextChanged({ Save-Settings }); Add-CtrlA-Handler $script:txtOutFolder_unpack
            $script:txtOutFolder_unpack.Add_DragEnter({ if ($_.Data.GetData("FileDrop") | % { (Get-Item -LiteralPath $_).PSIsContainer }) { $_.Effect = 'Copy' } else { $_.Effect = 'None' } })
            $script:txtOutFolder_unpack.Add_DragDrop({ if (($path=$_.Data.GetData("FileDrop")[0]) -and (Get-Item -LiteralPath $path).PSIsContainer) { $this.Text = $path } })
            $btnBrowseFolder_unpack = New-Object Button; $btnBrowseFolder_unpack.Text = 'Обзор'; $btnBrowseFolder_unpack.Size = "85, 24"; $btnBrowseFolder_unpack.Location = [Point]::new([int]($script:txtOutFolder_unpack.Right + 5), $outputSectionTopY + 21)
            $btnBrowseFolder_unpack.Font = $script:boldFont; $btnBrowseFolder_unpack.BackColor = [ColorTranslator]::FromHtml('#3E3E42'); $btnBrowseFolder_unpack.ForeColor = 'White'; $btnBrowseFolder_unpack.FlatStyle = [FlatStyle]::Flat; $btnBrowseFolder_unpack.FlatAppearance.BorderSize = 0
            $btnBrowseFolder_unpack.Add_Click({ if ($selectedFolder = YEFolder) { $script:txtOutFolder_unpack.Text = $selectedFolder } })
            $gb.Controls.AddRange(@($lblCombined, $ltbxCombined, $lblLoose, $ltbxLoose, $lblOutFolder_unpack, $script:txtOutFolder_unpack, $btnBrowseFolder_unpack))
        }
        elseif ($currentButtonName -eq 'Pack') {
            $outputControlsHeight = 145; $outputControlsTopY = $gb.ClientSize.Height - $outputControlsHeight - 10
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
            $outputControlsHeight = 145; $outputControlsTopY = $gb.ClientSize.Height - $outputControlsHeight - 10; $outputControlsYOffset = $outputControlsTopY - 320
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
            $outputControlsHeight = 145; $outputControlsTopY = $gb.ClientSize.Height - $outputControlsHeight - 10; $outputControlsYOffset = $outputControlsTopY - 320
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
                        Write-Log "Удалён элемент: $($item.DisplayText)"
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
            $lblSettingsTitle.Font = New-Object Font('Century Gothic', 12, [FontStyle]::Bold)
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
            $script:cbXciTrim.Add_CheckedChanged({ Save-Settings })
            $settingsY += 40
            
            # --- Понижение версии (KG) ---
            $script:lblKeyGen = New-Object Label
            $script:lblKeyGen.Text = "Понижение версии (KeyGeneration):"
            $script:lblKeyGen.Font = $script:boldFont
            $script:lblKeyGen.AutoSize = $true
            $script:lblKeyGen.Location = "15, $settingsY"
            
            $script:numKeyGen = New-Object NumericUpDown
            $script:numKeyGen.Minimum = 17
            $script:numKeyGen.Maximum = 21
            $script:numKeyGen.Value = 21
            $script:numKeyGen.Location = "$settingsControlX, $($settingsY - 2)"
            $script:numKeyGen.Size = '60, 24'
            $script:numKeyGen.Font = $script:regularFont
            $script:numKeyGen.BackColor = [ColorTranslator]::FromHtml('#3E3E42')
            $script:numKeyGen.ForeColor = [Color]::White
            $script:numKeyGen.BorderStyle = 'None'
            $script:numKeyGen.TextAlign = 'Center'
            $script:numKeyGen.Add_ValueChanged({ Save-Settings })
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
            $script:numUsedCores.Add_ValueChanged({ Save-Settings })
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
            $script:numConcurrentTasks.Value = 1
            $script:numConcurrentTasks.Location = "$settingsControlX, $($settingsY - 2)"
            $script:numConcurrentTasks.Size = '60, 24'
            $script:numConcurrentTasks.Font = $script:regularFont
            $script:numConcurrentTasks.BackColor = [ColorTranslator]::FromHtml('#3E3E42')
            $script:numConcurrentTasks.ForeColor = [Color]::White
            $script:numConcurrentTasks.BorderStyle = 'None'
            $script:numConcurrentTasks.TextAlign = 'Center'
            $script:numConcurrentTasks.Add_ValueChanged({ Save-Settings })
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
            $script:tbCompression.Value = 18
            $script:tbCompression.Location = "$settingsControlX, $($settingsY - 2)"
            $script:tbCompression.Size = '60, 24'
            $script:tbCompression.Font = $script:regularFont
            $script:tbCompression.BackColor = [ColorTranslator]::FromHtml('#3E3E42')
            $script:tbCompression.ForeColor = [Color]::White
            $script:tbCompression.BorderStyle = 'None'
            $script:tbCompression.TextAlign = 'Center'
            $script:tbCompression.Add_ValueChanged({ Save-Settings })
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
                $script:lblKeyGen, $script:numKeyGen,
                $lblCores, $script:numUsedCores,
                $lblConcurrent, $script:numConcurrentTasks,
                $lblCompression, $script:tbCompression,
                $lblInfo
            ))
        }
    }

    ($script:f.Controls.Find("NavButton_Update", $true)[0]).BackColor = [ColorTranslator]::FromHtml('#007ACC')
    
    # === КНОПКИ ДЕЙСТВИЙ В НАВИГАЦИИ (настройки перенесены в вкладку "Настройки") ===
    # Кнопки расположены над ссылками внизу панели
    $currentNavY = $navGroupBox.ClientSize.Height - 160

    $btnToTasks = New-Object Button; $btnToTasks.Location = "8, $currentNavY"; $btnToTasks.Size = '200, 30'; $btnToTasks.Text = 'В задачи'; $btnToTasks.Font = $script:boldFont;
    $btnToTasks.BackColor = [Color]::FromArgb(255, 25, 25, 112); $btnToTasks.ForeColor = [Color]::White; $btnToTasks.FlatStyle = [FlatStyle]::Flat;
    $btnToTasks.FlatAppearance.BorderSize = 0
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
    $btnToggleTasks.FlatAppearance.BorderSize = 0
    $originalLinkLabel = New-Object LinkLabel; $originalFullText = "Оригинал YanuExt: andrey4556"; $originalLink1Text = "YanuExt";
    $originalLink2Text = "andrey4556";
    $originalLinkLabel.Text = $originalFullText; $originalLinkLabel.Font = $script:boldFont; $originalLinkLabel.ForeColor = [Color]::White;
    $originalLinkLabel.LinkColor = [Color]::DodgerBlue; $originalLinkLabel.ActiveLinkColor = [Color]::Tomato;
    $originalLinkLabel.AutoSize = $true;
    $originalLinkLabel.Location = [Point]::new(8, $navGroupBox.ClientSize.Height - 45)
    $originalLinkLabel.Links.Add($originalFullText.IndexOf($originalLink1Text), $originalLink1Text.Length, "https://github.com/vvvooopy/yanuext"); $originalLinkLabel.Links.Add($originalFullText.IndexOf($originalLink2Text), $originalLink2Text.Length, "https://4pda.to/forum/index.php?showuser=9246779");
    $originalLinkLabel.Add_LinkClicked({param($s, $e) Start-Process $e.Link.LinkData })
    $modLabel = New-Object LinkLabel; $fullText = 'Мод YanuExt: ReiKatari'; $linkText = 'ReiKatari';
    $startIndex = $fullText.IndexOf($linkText);
    $modLabel.Text = $fullText; $modLabel.Font = $script:boldFont; $modLabel.LinkArea = [LinkArea]::new($startIndex, $linkText.Length); $modLabel.LinkColor = [Color]::Red; $modLabel.ForeColor = [Color]::White;
    $modLabel.Location = [Point]::new(38, $navGroupBox.ClientSize.Height - 25); $modLabel.AutoSize = $true
    $modLabel.Add_LinkClicked({ Start-Process "https://4pda.to/forum/index.php?showuser=7365134" })

    $navGroupBox.Controls.AddRange(@($bs, $modLabel, $btnToTasks, $originalLinkLabel, $btnToggleTasks))

    $script:taskGrid = New-Object DataGridView;
    $script:taskGrid.Location = '6, 30'; $script:taskGrid.Size = [Size]::new(($taskGroupBox.Width - 12), ($taskGroupBox.Height - 36)); $script:taskGrid.Anchor = 'Top, Bottom, Left, Right';
    $script:taskGrid.Name = 'taskGrid'; $script:taskGrid.AllowUserToAddRows = $false; $script:taskGrid.AllowUserToDeleteRows = $false; $script:taskGrid.AllowUserToResizeRows = $false; $script:taskGrid.AllowUserToOrderColumns = $true; $script:taskGrid.RowHeadersVisible = $false;
    $script:taskGrid.MultiSelect = $true; $script:taskGrid.SelectionMode = [DataGridViewSelectionMode]::FullRowSelect; $script:taskGrid.EnableHeadersVisualStyles = $false; $script:taskGrid.BorderStyle = [BorderStyle]::None; $script:taskGrid.ColumnHeadersBorderStyle = [DataGridViewHeaderBorderStyle]::Single; $script:taskGrid.GridColor = [ColorTranslator]::FromHtml('#505050');
    $script:taskGrid.BackgroundColor = [ColorTranslator]::FromHtml('#2D2D30'); $script:taskGrid.Font = $script:regularFont; $script:taskGrid.ShowCellToolTips = $true; $script:taskGrid.RowTemplate.Height = 24
    $script:taskGrid.Add_SelectionChanged({ Update-LogView });
    $script:taskGrid.Add_ColumnWidthChanged({ Save-Settings })
    $headerStyle = New-Object DataGridViewCellStyle; $headerStyle.BackColor = [ColorTranslator]::FromHtml('#3E3E42'); $headerStyle.ForeColor = [Color]::White; $headerStyle.Font = $script:boldFont;
    $headerStyle.Alignment = 'MiddleCenter'
    $script:taskGrid.ColumnHeadersDefaultCellStyle = $headerStyle; $script:taskGrid.ColumnHeadersHeight = 30;
    $script:taskGrid.ColumnHeadersHeightSizeMode = 'DisableResizing'
    $cellStyle = New-Object DataGridViewCellStyle; $cellStyle.BackColor = [ColorTranslator]::FromHtml('#333333'); $cellStyle.ForeColor = [Color]::White; $cellStyle.SelectionBackColor = [ColorTranslator]::FromHtml('#007ACC');
    $cellStyle.SelectionForeColor = [Color]::White
    $script:taskGrid.DefaultCellStyle = $cellStyle
    $altCellStyle = New-Object DataGridViewCellStyle; $altCellStyle.BackColor = [ColorTranslator]::fromhtml('#3C3C3C');
    $script:taskGrid.AlternatingRowsDefaultCellStyle = $altCellStyle
    $colHeaders = [ordered]@{ 'Задача'=100; 'Обработка'=200; 'Нач. формат'=70; 'Кон. формат'=70; 'Нач. размер'=85; 'Кон. размер'=85; 'Разница'=65;
    'Уровень сжатия'=70; 'Кол-во файлов'=60; 'Статус'=200; 'Выполнение'=120 }
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
    try { $script:taskGrid.Columns['Обработка'].DefaultCellStyle.GetType().GetProperty("Trimming").SetValue($script:taskGrid.Columns['Обработка'].DefaultCellStyle, [StringTrimming]::EllipsisPath) } catch {}
    $taskGridHeaderMenu = New-Object ContextMenuStrip;
    $taskGridHeaderMenu.Renderer = New-Object YanuExt.CustomControls.CustomMenuRenderer
    $taskGridHeaderMenu.Add_Opening({ param($s, $e) ; foreach ($item in $s.Items) { if ($item.Tag -is [DataGridViewColumn]) { $item.Checked = $item.Tag.Visible } } })
    $script:taskGrid.Columns |
    ForEach-Object { $column = $_; $item = $taskGridHeaderMenu.Items.Add($column.HeaderText); $item.CheckOnClick = $true; $item.Checked = $column.Visible; $item.Tag = $column;
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
        if (-not $script:suppressSave) { Save-Settings }
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
        if ($kg -ge $script:numKeyGen.Minimum -and $kg -le $script:numKeyGen.Maximum) { $script:numKeyGen.Value = $kg } } catch {} }
        if ($script:settings.UnpackStitched) { try { if ($script:cbUnpackStitched) { $script:cbUnpackStitched.Checked = [System.Convert]::ToBoolean($script:settings.UnpackStitched) } } catch {} }
        $columnKeyMap = @{ 'Задача'='Task';'Обработка'='Processing';'Нач.
        формат'='StartFormat';'Кон. формат'='EndFormat'; 'Нач. размер'='StartSize';'Кон. размер'='EndSize';'Разница'='Difference'; 'Уровень сжатия'='CompressionLevelGrid';'Кол-во файлов'='FileCount';'Статус'='Status';'Выполнение'='Execution' }
        foreach($col in $script:taskGrid.Columns) { if ($columnKeyMap.ContainsKey($col.HeaderText)) { $englishKey = $columnKeyMap[$col.HeaderText];
        $widthProp = "TaskGrid_Col_${englishKey}_Width"; $visibleProp = "TaskGrid_Col_${englishKey}_Visible"; if ($script:settings.PSObject.Properties[$widthProp]) { try { $col.Width = [int]$script:settings.$widthProp } catch {} };
        if ($script:settings.PSObject.Properties[$visibleProp]) { try { $col.Visible = [System.Convert]::ToBoolean($script:settings.$visibleProp); $taskGridHeaderMenu.Items[$col.HeaderText].Checked = $col.Visible } catch {} } } }
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
                    # Если база не найдена, пробуем найти самый большой файл среди stitching, если это распаковка
                    if ($isUnpack -and $allFilesForStitching.Count -gt 0) {
                         $baseFile = Get-Item $allFilesForStitching[0]
                    } else {
                        Write-Log "Пропуск группы $($taskGroup.Name): не найден базовый файл (ИГРА/GAME)." -Type "WARN"
                        continue 
                    }
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
                elseif ($tabName -eq 'Update') { $taskData.TaskType = 'UpdateRepack' }
                elseif ($tabName -eq 'Multi') { $taskData.TaskType = 'BuildMulti' }

                $taskData.Base = $baseFile.FullName
                $taskData.Updates = if($updateFile) { @($updateFile.FullName) } else { $null }
                $taskData.RomfsPaths = if($romfsList.Count -gt 0) {$romfsList} else {$null}
                $taskData.ExefsPath = $exefsPath
                
                if ($unlockerPresent) {
                    Write-Log "В задачу включены файлы UNLOCKER ($($unlockerList.Count) шт.). Они будут вшиты." -TaskID $taskData.TaskID -Type 'SUCCESS'
                }

                $allExtras = [System.Collections.Generic.List[string]]::new()
                $allExtras.AddRange($dlcList)
                $allExtras.AddRange($unlockerList)
                $taskData.DLCs = if($allExtras.Count -gt 0) {$allExtras} else {$null}

                $taskData.FilesForStitching = $allFilesForStitching
                
                $taskData.OutDir = if($isUnpack) { $script:txtOutFolder_unpack.Text } else { $outputConf.TxtOutFolder.Text };
                $taskData.OutFormat = if(-not $isUnpack) { $outputConf.SelectedFormat } else { $null };
                $taskData.CompressionLevel = $script:tbCompression.Value
                $taskData.KeyGeneration = $script:numKeyGen.Value
                $taskData.XciTrim = $script:cbXciTrim.Checked
                $taskData.UnpackStitched = if($script:cbUnpackStitched){$script:cbUnpackStitched.Checked}else{$false}
                
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
                $allExtras.ForEach({ if($_) { $initialSize += Get-ItemSize -itemInfo $_ } });
                $romfsList.ForEach({ if($_) { $initialSize += Get-ItemSize -itemInfo $_ } });
                if($exefsPath) { $initialSize += Get-ItemSize -itemInfo $exefsPath };
                $taskData.InitialSize = $initialSize
                
                $fileCount = 1 + $(if($updateFile){1}else{0}) + $allExtras.Count + $romfsList.Count + $(if($exefsPath){1}else{0});
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
            if ($taskRow.Cells['Статус'].Value -ne 'Ожидание') { continue }
            
            $taskData = $taskRow.Tag
            
            $taskRow.Cells['Статус'].Value = 'Запуск...'
            $taskRow.Cells['Выполнение'].Value = 0
            
            # СТАБИЛЬНОСТЬ: Добавляем задержку между запусками задач
            # чтобы избежать конфликтов при одновременном старте
            if ($script:runningTasks.Count -gt 0) {
                # Увеличенная задержка для задач с NSCB/squirrel (они более склонны к конфликтам)
                $delayMs = if ($taskData.TaskType -in 'BuildMulti', 'DirectStitch') { 3000 } else { 1500 }
                Start-Sleep -Milliseconds $delayMs
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
                } catch {}
            }
            if ($displayOutputName -eq "Автоматически" -and $taskData.OriginalBase) {
                try { $displayOutputName = [System.IO.Path]::GetFileName($taskData.OriginalBase) } catch {}
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
        } catch {} 
    }
    $Host.UI.RawUI.BackgroundColor = 'Black'; 
    $Host.UI.RawUI.ForegroundColor = 'Gray';
    $bufW = [math]::Max($w + 20, 120); 
    $Host.UI.RawUI.BufferSize = New-Object System.Management.Automation.Host.Size($bufW, 3000); 
    $Host.UI.RawUI.WindowSize = New-Object System.Management.Automation.Host.Size($w, $h);
    Clear-Host
} catch {}

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
    function Write-WorkerLog { 
        param($Message, $Type='INFO');
        $logEntry = @{TaskID=$taskData.TaskID; Type=$Type; Message="$Message"}; 
        $logEntry | ConvertTo-Json -Compress | Add-Content -LiteralPath $logFile 
    }
    function Write-WorkerProgress { 
        param($Status, [int]$Percent = -1); 
        $progress = @{ StatusDescription = $Status; PercentComplete = $Percent }; 
        $progress | Export-Clixml -Path $statusFile 
    }
    function Log-Xci-Status { 
        param($filePath, $isTrimmed) 
        if ($isTrimmed -eq "true" -and (Test-Path -LiteralPath $filePath)) { 
            try { 
                $size = (Get-Item -LiteralPath $filePath).Length;
                $sizeStr = if ($size -gt 1073741824) { "{0:N2} GB" -f ($size / 1GB) } else { "{0:N2} MB" -f ($size / 1MB) };
                Write-WorkerLog "Файл XCI обработан (Trim активен). Итоговый размер: $sizeStr" -Type 'SUCCESS' 
            } catch {} 
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
    
    ##WORKER_FUNCTIONS_BLOCK##
    
    $result = @{ TaskID = $taskData.TaskID; Status = 'Ошибка'; FinalPath = '-'; InitialSize = 0; FinalSize = $null };
    $tempDir = Join-Path $toolPaths.wdir $taskData.TaskID
    
    try {
        Write-Header -Title "СТАРТ ЗАДАЧИ" -Subtitle $taskData.TaskType -AppVersion $taskData.AppVersion -OutputInfo $taskData.DisplayOutputName
        [void](New-Item -ItemType Directory -Force $tempDir -ErrorAction SilentlyContinue)
        
        # Проверка ключей
        if (-not $toolPaths.key -or -not (Test-Path -LiteralPath $toolPaths.key)) { $toolPaths.key = Join-Path $toolPaths.tdir 'prod.keys' }
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
                
                # Конвертация в XCI или сжатие
                if ($taskData.OutFormat.ToLower() -in 'nsz', 'xcz', 'xci') {
                    if ($taskData.OutFormat.ToLower() -in 'xci', 'xcz') {
                        Write-WorkerProgress -Status "Конвертация в XCI" -Percent 92;
                        $isolatedNscb = Setup-IsolatedToolWithKeys -toolName 'nscb' -tempDir $tempDir -toolPaths $toolPaths
                        $nscbListPath = Join-Path $tempDir "list_upd.txt";
                        $packedNsp | Out-File -FilePath $nscbListPath -Encoding ASCII
                        $squirrelArgsArray = @('-b', '65536', '-pv', 'false', '-kp', $kpVal, '-tm', $trimVal, '--RSVcap', '268435656', '-fat', 'exfat', '-fx', 'files', '-ND', 'true', '-t', 'xci', '-o', "`"$tempDir`"", '-tfile', "`"$nscbListPath`"", '-roma', 'TRUE', '-dmul', '"calculate"')
                        $squirrelProc = @{ Exe = $isolatedNscb.Exe; Args = ($squirrelArgsArray -join ' '); WorkingDir = $isolatedNscb.WorkingDir }
                        if ((Invoke-Tool $squirrelProc "Конвертация в XCI") -ne 0) { throw "Ошибка конвертации в XCI." }; 
                        if ($kpVal -ne "false") { Write-WorkerLog "Понижение версии ключей до Generation $kpVal успешно применено." -Type 'SUCCESS' }
                        Remove-Item -LiteralPath $packedNsp -Force;
                        $packedNsp = (Get-ChildItem -LiteralPath $tempDir -Filter "*.xci" | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName;
                        Log-Xci-Status -filePath $packedNsp -isTrimmed $trimVal
                    }
                    if ($taskData.OutFormat.ToLower() -in 'nsz', 'xcz') {
                        Write-WorkerProgress -Status "Сжатие" -Percent 95;
                        $isolatedNsz = Setup-IsolatedToolWithKeys -toolName 'nsz' -tempDir $tempDir -toolPaths $toolPaths
                        $coresArg = if ($useCores) { "-t $useCores" } else { "" };
                        $nszArgs = "$coresArg -C -l $($taskData.CompressionLevel) `"$packedNsp`" -o `"$tempDir`"";
                        $procConv = @{ Exe = $isolatedNsz.Exe; Args = $nszArgs; WorkingDir = $isolatedNsz.WorkingDir }; 
                        if ((Invoke-Tool $procConv "Сжатие") -ne 0) { throw "Ошибка сжатия." };
                        Remove-Item -LiteralPath $packedNsp -Force;
                        $finalFileInTemp = (Get-ChildItem -LiteralPath $tempDir -Filter "*.$($taskData.OutFormat.ToLower())" | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
                    } else { $finalFileInTemp = $packedNsp }
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
                
                # Конвертация в XCI или сжатие (аналогично UpdateRepack)
                if ($taskData.OutFormat.ToLower() -in 'nsz', 'xcz', 'xci') { 
                    if ($taskData.OutFormat.ToLower() -in 'xci', 'xcz') {
                        Write-WorkerProgress -Status "Конвертация в XCI" -Percent 92;
                        $isolatedNscb = Setup-IsolatedToolWithKeys -toolName 'nscb' -tempDir $tempDir -toolPaths $toolPaths; 
                        $nscbListPath = Join-Path $tempDir "list_pack.txt"; 
                        $packedNsp | Out-File -FilePath $nscbListPath -Encoding ASCII
                        $squirrelArgsArray = @('-b', '65536', '-pv', 'false', '-kp', $kpVal, '-tm', $trimVal, '--RSVcap', '268435656', '-fat', 'exfat', '-fx', 'files', '-ND', 'true', '-t', 'xci', '-o', "`"$tempDir`"", '-tfile', "`"$nscbListPath`"", '-roma', 'TRUE', '-dmul', '"calculate"')
                        $squirrelProc = @{ Exe = $isolatedNscb.Exe; Args = ($squirrelArgsArray -join ' '); WorkingDir = $isolatedNscb.WorkingDir }
                        if ((Invoke-Tool $squirrelProc "Конвертация NSP в XCI") -ne 0) { throw "Ошибка конвертации в XCI (NSC_Builder)." }
                        Remove-Item -LiteralPath $packedNsp -Force;
                        $packedNsp = (Get-ChildItem -LiteralPath $tempDir -Filter "*.xci" | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
                        if (-not $packedNsp) { throw "Не найден .xci файл." }
                    }
                    if ($taskData.OutFormat.ToLower() -in 'nsz', 'xcz') { 
                        Write-WorkerProgress -Status "Сжатие (NSZ)" -Percent 95;
                        $isolatedNsz = Setup-IsolatedToolWithKeys -toolName 'nsz' -tempDir $tempDir -toolPaths $toolPaths; 
                        $coresArg = if ($useCores) { "-t $useCores" } else { "" };
                        $nszArgs = "$coresArg -C -l $($taskData.CompressionLevel) `"$packedNsp`" -o `"$tempDir`"";
                        $procConv = @{ Exe = $isolatedNsz.Exe; Args = $nszArgs; WorkingDir = $isolatedNsz.WorkingDir }; 
                        Invoke-Tool $procConv "Сжатие" | Out-Null;
                        Remove-Item -LiteralPath $packedNsp -Force;
                        $finalFileInTemp = (Get-ChildItem -LiteralPath $tempDir -Filter "*.$($taskData.OutFormat.ToLower())" | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName 
                    } else { $finalFileInTemp = $packedNsp }
                }
            }
            'DirectStitch' {
                Write-WorkerProgress -Status "Подготовка" -Percent 2; 
                $isolatedNsz = Setup-IsolatedToolWithKeys -toolName 'nsz' -tempDir $tempDir -toolPaths $toolPaths
                $convertDir = Join-Path $tempDir 'converted_nsp'; [void](New-Item -ItemType Directory -Force $convertDir)
                $outDir = if (-not [string]::IsNullOrWhiteSpace($taskData.OutDir)) { $taskData.OutDir } else { $toolPaths.odir };
                if (-not (Test-Path -LiteralPath $outDir)) { [void](New-Item -ItemType Directory -Force $outDir) }
                
                Write-WorkerLog "Конвертация/Сбор файлов...";
                $needIso = if ($taskData.NeedIsolation) { $taskData.NeedIsolation } else { $false }
                $sourcesToStitch = [System.Collections.Generic.List[string]]::new()
                if ($taskData.Base) { $sourcesToStitch.Add((Convert-To-NspIfNeeded -filePath $taskData.Base -convertDir $convertDir -isolatedNsz $isolatedNsz -useCores $useCores -needIsolation $needIso)) }
                if ($taskData.Updates) { foreach ($file in [array]$taskData.Updates) { $sourcesToStitch.Add((Convert-To-NspIfNeeded -filePath $file -convertDir $convertDir -isolatedNsz $isolatedNsz -useCores $useCores -needIsolation $needIso)) } }
                if ($taskData.DLCs) { foreach ($file in [array]$taskData.DLCs) { $sourcesToStitch.Add((Convert-To-NspIfNeeded -filePath $file -convertDir $convertDir -isolatedNsz $isolatedNsz -useCores $useCores -needIsolation $needIso)) } }
  
                Write-WorkerProgress -Status "Сшивание" -Percent 20;
                $stitchingDir = Join-Path $tempDir 'stitching_files'; [void](New-Item -ItemType Directory -Force $stitchingDir);
                $localFilesForStitching = [System.Collections.Generic.List[string]]::new()
                foreach ($sourceFile in $sourcesToStitch) { 
                    if (-not (Test-Path -LiteralPath $sourceFile)) { Write-WorkerLog "ВНИМАНИЕ: Файл для сшивания не найден: $sourceFile" -Type 'WARN'; continue }
                    $fName = Split-Path $sourceFile -Leaf;
                    if ($fName -match "Unlocker") { Write-WorkerLog "Вшивание UNLOCKER: '$fName'" -Type 'SUCCESS' } else { Write-WorkerLog "Копирование '$fName'..." };
                    Copy-Item -LiteralPath $sourceFile -Destination $stitchingDir; 
                    $localFilesForStitching.Add((Join-Path $stitchingDir $fName)) 
                }
                
                Write-WorkerProgress -Status "Обработка (NSCB)" -Percent 25;
                $intermediateNSP = $null; $nscbListPath = Join-Path $tempDir "list.txt"; $localFilesForStitching | Out-File -FilePath $nscbListPath -Encoding ASCII
                $isolatedNscb = Setup-IsolatedToolWithKeys -toolName 'nscb' -tempDir $tempDir -toolPaths $toolPaths
                # ИСПРАВЛЕНИЕ: ВСЕГДА используем temp для вывода squirrel, чтобы избежать конфликтов
                $targetForNSCB = $tempDir
                $filesBefore = @(Get-ChildItem -LiteralPath $targetForNSCB -Filter "*.nsp" -File -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)
                $nscbTargetType = if ($taskData.OutFormat.ToLower() -in 'xci', 'xcz') { 'xci' } else { 'cnsp' };
                $nscbTargetExt = if ($nscbTargetType -eq 'xci') { '.xci' } else { '.nsp' }
                $squirrelArgsArray = @('-b', '65536', '-pv', 'false', '-kp', $kpVal);
                if ($nscbTargetType -eq 'xci') { $squirrelArgsArray += ('-tm', $trimVal) }
                if (-not [string]::IsNullOrWhiteSpace($taskData.InternalName)) { $squirrelArgsArray += ('-nm', "`"$($taskData.InternalName)`""); Write-WorkerLog "Установка внутреннего имени: $($taskData.InternalName)" -Type 'SUCCESS' }
                $squirrelArgsArray += ('--RSVcap', '268435656', '-fat', 'exfat', '-fx', 'files', '-ND', 'true', '-t', $nscbTargetType, '-o', "`"$targetForNSCB`"", '-tfile', "`"$nscbListPath`"", '-roma', 'TRUE', '-dmul', '"calculate"')
                $squirrelProc = @{ Exe = $isolatedNscb.Exe; Args = ($squirrelArgsArray -join ' '); WorkingDir = $isolatedNscb.WorkingDir }
                if ((Invoke-Tool $squirrelProc "Сшивание") -ne 0) { throw "Ошибка сшивания" }
                if ($kpVal -ne "false") { Write-WorkerLog "Понижение версии ключей до Generation $kpVal успешно применено." -Type 'SUCCESS' }
                
                $filesAfter = @(Get-ChildItem -LiteralPath $targetForNSCB -Filter "*$nscbTargetExt" -File -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)
                $intermediateNSP = $filesAfter | Where-Object { $_ -notin $filesBefore } | Select-Object -First 1
                if (-not $intermediateNSP) { throw "Не найден файл $nscbTargetExt." }
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
                Write-WorkerLog "Конвертация исходных файлов...";
                $needIso = if ($taskData.NeedIsolation) { $taskData.NeedIsolation } else { $false }
                $convertedBase = Convert-To-NspIfNeeded -filePath $taskData.Base -convertDir $convertDir -isolatedNsz $isolatedNsz -useCores $useCores -needIsolation $needIso; 
                
                # === ПРОВЕРКА НА СШИТЫЙ ФАЙЛ ===
                # Если база уже сшита (содержит update/DLC) и добавляются новые обновления/DLC,
                # нужно сначала извлечь только базовую игру, чтобы заменить старый контент на новый
                $hasNewUpdates = $null -ne $taskData.Updates -and @($taskData.Updates).Count -gt 0
                $hasNewDLCs = $null -ne $taskData.DLCs -and @($taskData.DLCs).Count -gt 0
                
                if ($hasNewUpdates -or $hasNewDLCs) {
                    $stitchedInfo = Test-IsStitchedFile -filePath $convertedBase -isolatedNsz $isolatedNsz
                    
                    if ($stitchedInfo.IsStitched) {
                        Write-WorkerLog "База является сшитым файлом (содержит: $(if ($stitchedInfo.HasUpdate) {'Update '})$(if ($stitchedInfo.HasDLC) {'DLC'})). Извлечение базовой игры для замены..." -Type 'WARN'
                        
                        $extractedBase = Extract-BaseFromStitched -stitchedFilePath $convertedBase -outputDir $convertDir -isolatedNsz $isolatedNsz -toolPaths $toolPaths -useCores $useCores
                        
                        if ($extractedBase -and (Test-Path -LiteralPath $extractedBase)) {
                            Write-WorkerLog "Старое обновление/DLC будет заменено новым" -Type 'SUCCESS'
                            $convertedBase = $extractedBase
                        } else {
                            Write-WorkerLog "Не удалось извлечь базу. Продолжаем с оригинальным файлом (контент будет добавлен, не заменён)" -Type 'WARN'
                        }
                    }
                }
                
                $convertedUpdates = [System.Collections.Generic.List[string]]::new();
                if ($taskData.Updates) { foreach ($file in [array]$taskData.Updates) { $convertedUpdates.Add((Convert-To-NspIfNeeded -filePath $file -convertDir $convertDir -isolatedNsz $isolatedNsz -useCores $useCores -needIsolation $needIso)) } };
                $convertedDLCs = [System.Collections.Generic.List[string]]::new(); 
                if ($taskData.DLCs) { foreach ($file in [array]$taskData.DLCs) { $convertedDLCs.Add((Convert-To-NspIfNeeded -filePath $file -convertDir $convertDir -isolatedNsz $isolatedNsz -useCores $useCores -needIsolation $needIso)) } }
                
                $unpackDir = Join-Path $tempDir 'unpacked'; [void](New-Item -ItemType Directory -Force $unpackDir); 
                $packDir = Join-Path $tempDir 'packed'; [void](New-Item -ItemType Directory -Force $packDir);
                $outDir = if (-not [string]::IsNullOrWhiteSpace($taskData.OutDir)) { $taskData.OutDir } else { $toolPaths.odir };
                if (-not (Test-Path -LiteralPath $outDir)) { [void](New-Item -ItemType Directory -Force $outDir) };
                
                $hasMods = ($null -ne $taskData.RomfsPaths -and $taskData.RomfsPaths.Count -gt 0) -or (-not [string]::IsNullOrEmpty($taskData.ExefsPath));
                $intermediateNSP = $null;
                $nscbTargetType = if ($taskData.OutFormat.ToLower() -in 'xci', 'xcz') { 'xci' } else { 'cnsp' };
                $nscbTargetExt = if ($nscbTargetType -eq 'xci') { '.xci' } else { '.nsp' }

                if ($hasMods) {
                    Write-WorkerLog "Обнаружены моды. Полный цикл.";
                    Write-WorkerProgress -Status "Подготовка" -Percent 5; 
                    $baseFileForUnpack = $convertedBase;
                    $updateFileForUnpack = if ($convertedUpdates.Count -gt 0) { $convertedUpdates[0] } else { $null };
                    
                    Write-WorkerProgress -Status "Распаковка" -Percent 30; 
                    $updateArgUnpack = if ($updateFileForUnpack) { " --update `"$updateFileForUnpack`"" } else { "" };
                    $unpackProcArgs = "unpack --base `"$baseFileForUnpack`"$updateArgUnpack -o `"$unpackDir`"";
                    
                    # ИЗОЛЯЦИЯ yanu-cli для BuildMulti
                    $isolatedYanuDir = Join-Path $tempDir 'isolated_yanu'
                    [void](New-Item -ItemType Directory -Force $isolatedYanuDir)
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
                    $controlNcaFile = (Get-ChildItem -LiteralPath $pdata -Filter *.nca | ForEach-Object { if (& $toolPaths.hactoolnet "-k" $toolPaths.key $_.FullName | Select-String 'Control' -Quiet) { $_; return } } | Select-Object -First 1);
                    if (-not $controlNcaFile) {
                        Write-WorkerLog "Стандартный поиск control.nca не дал результата. Ищем рекурсивно..." -Type 'WARN'
                        $controlNcaFile = (Get-ChildItem -LiteralPath $unpackDir -Filter *.nca -Recurse | ForEach-Object { if (& $toolPaths.hactoolnet "-k" $toolPaths.key $_.FullName | Select-String 'Control' -Quiet) { $_; return } } | Select-Object -First 1)
                    }
                    if (-not $controlNcaFile) { throw "Не удалось найти control.nca." }
                    
                    $titleId = (& $toolPaths.hactoolnet "-k" $toolPaths.key $controlNcaFile.FullName | Select-String 'TitleID').ToString().Split(':')[-1].Trim();
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
                    
                    $shouldRunStitching = ($convertedDLCs.Count -gt 0) -or (-not [string]::IsNullOrWhiteSpace($taskData.InternalName))
                    
                    if ($shouldRunStitching) { 
                        Write-WorkerProgress -Status "Сшивание / Переименование" -Percent 85;
                        $filesForStitchingWithMods = [System.Collections.Generic.List[string]]::new(); 
                        $filesForStitchingWithMods.Add($packedModdedNsp); 
                        $filesForStitchingWithMods.AddRange($convertedDLCs); 
                        $nscbListPath = Join-Path $tempDir "list.txt";
                        $filesForStitchingWithMods | Out-File -FilePath $nscbListPath -Encoding ASCII;
                        $isolatedNscb = Setup-IsolatedToolWithKeys -toolName 'nscb' -tempDir $tempDir -toolPaths $toolPaths;
                        $squirrelArgsArray = @('-b', '65536', '-pv', 'false', '-kp', 'false', '--RSVcap', '268435656', '-fat', 'exfat', '-fx', 'files', '-ND', 'true', '-t', $nscbTargetType, '-o', "`"$packDir`"", '-tfile', "`"$nscbListPath`"", '-roma', 'TRUE', '-dmul', '"calculate"');
                        # ПРИМЕЧАНИЕ: squirrel.exe не поддерживает аргумент -nm для установки внутреннего имени
                        $squirrelProc = @{ Exe = $isolatedNscb.Exe; Args = ($squirrelArgsArray -join ' '); WorkingDir = $isolatedNscb.WorkingDir };
                        if ((Invoke-Tool $squirrelProc "Сшивание") -ne 0) { throw "Ошибка сшивания." }; Start-Sleep -Seconds 1;
                        if ($kpVal -ne "false") { Write-WorkerLog "Понижение версии ключей до Generation $kpVal успешно применено." -Type 'SUCCESS' }
                        $intermediateNSP = Get-ChildItem -LiteralPath $packDir -Filter "*$nscbTargetExt" -File -ErrorAction SilentlyContinue | Sort-Object -Property LastWriteTime -Descending | Select-Object -First 1 | Select-Object -ExpandProperty FullName;
                        if (-not $intermediateNSP) { throw "Не найден файл $nscbTargetExt." } 
                        Log-Xci-Status -filePath $intermediateNSP -isTrimmed $trimVal
                    } else { $intermediateNSP = $packedModdedNsp }
                } else {
                    Write-WorkerProgress -Status "Сшивание" -Percent 25; 
                    $filesForStitching = [System.Collections.Generic.List[string]]::new(); 
                    $filesForStitching.Add($convertedBase); 
                    $filesForStitching.AddRange($convertedUpdates); 
                    $filesForStitching.AddRange($convertedDLCs);
                    
                    if ($filesForStitching.Count -gt 1 -or (-not [string]::IsNullOrWhiteSpace($taskData.InternalName))) { 
                        $nscbListPath = Join-Path $tempDir "list.txt"; 
                        $filesForStitching | Out-File -FilePath $nscbListPath -Encoding ASCII;
                        $isolatedNscb = Setup-IsolatedToolWithKeys -toolName 'nscb' -tempDir $tempDir -toolPaths $toolPaths;
                        $squirrelArgsArray = @('-b', '65536', '-pv', 'false', '-kp', 'false', '--RSVcap', '268435656', '-fat', 'exfat', '-fx', 'files', '-ND', 'true', '-t', $nscbTargetType, '-o', "`"$packDir`"", '-tfile', "`"$nscbListPath`"", '-roma', 'TRUE', '-dmul', '"calculate"');
                        # ПРИМЕЧАНИЕ: squirrel.exe не поддерживает аргумент -nm для установки внутреннего имени
                        $squirrelProc = @{ Exe = $isolatedNscb.Exe; Args = ($squirrelArgsArray -join ' '); WorkingDir = $isolatedNscb.WorkingDir };
                        if ((Invoke-Tool $squirrelProc "Сшивание") -ne 0) { throw "Ошибка сшивания." }; Start-Sleep -Seconds 1;
                        if ($kpVal -ne "false") { Write-WorkerLog "Понижение версии ключей до Generation $kpVal успешно применено." -Type 'SUCCESS' }
                        $intermediateNSP = Get-ChildItem -LiteralPath $packDir -Filter "*$nscbTargetExt" -File -ErrorAction SilentlyContinue | Sort-Object -Property LastWriteTime -Descending | Select-Object -First 1 | Select-Object -ExpandProperty FullName;
                        if (-not $intermediateNSP) { throw "Не найден файл $nscbTargetExt." } 
                        Log-Xci-Status -filePath $intermediateNSP -isTrimmed $trimVal
                    } else { 
                        $tempDest = Join-Path $packDir ([System.IO.Path]::GetFileName($filesForStitching[0]));
                        Copy-Item -LiteralPath $filesForStitching[0] -Destination $tempDest -Force; 
                        $intermediateNSP = $tempDest 
                    }
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
                    Write-WorkerLog "Копирование файла...";
                    $tempCopyPath = Join-Path $tempDir (Split-Path $inputFile -Leaf);
                    Copy-Item -LiteralPath $inputFile -Destination $tempCopyPath -Force;
                    $finalFileInTemp = $tempCopyPath
                }

                if (-not $finalFileInTemp -or -not (Test-Path -LiteralPath $finalFileInTemp)) { throw "Не найден рабочий файл." } 
                
                # КОНВЕРТАЦИЯ В XCI (для XCI и XCZ)
                if ($targetFormat -in 'xci', 'xcz') {
                    Write-WorkerProgress -Status "Конвертация в XCI" -Percent 70;
                    $isolatedNscb = Setup-IsolatedToolWithKeys -toolName 'nscb' -tempDir $tempDir -toolPaths $toolPaths;
                    $nscbListPath = Join-Path $tempDir "list.txt"; $finalFileInTemp | Out-File -FilePath $nscbListPath -Encoding ASCII
                    $squirrelArgsArray = @('-b', '65536', '-pv', 'false', '-kp', $kpVal, '-tm', $trimVal, '--RSVcap', '268435656', '-fat', 'exfat', '-fx', 'files', '-ND', 'true', '-t', 'xci', '-o', "`"$tempDir`"", '-tfile', "`"$nscbListPath`"", '-roma', 'TRUE', '-dmul', '"calculate"')
                    $squirrelProc = @{ Exe = $isolatedNscb.Exe; Args = ($squirrelArgsArray -join ' '); WorkingDir = $isolatedNscb.WorkingDir }
                    if ((Invoke-Tool $squirrelProc "Конвертация в XCI") -ne 0) { throw "Ошибка конвертации в XCI." }
                    if ($kpVal -ne "false") { Write-WorkerLog "Понижение версии ключей до Generation $kpVal успешно применено." -Type 'SUCCESS' }
                    Remove-Item -LiteralPath $finalFileInTemp -Force;
                    $finalFileInTemp = (Get-ChildItem -LiteralPath $tempDir -Filter "*.xci" | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName;

                    # СЖАТИЕ XCI В XCZ
                    if ($targetFormat -eq 'xcz') {
                         Write-WorkerProgress -Status "Сжатие в XCZ" -Percent 95
                         if (-not $isolatedNsz) { $isolatedNsz = Setup-IsolatedToolWithKeys -toolName 'nsz' -tempDir $tempDir -toolPaths $toolPaths }
                         $coresArg = if ($useCores) { "-t $useCores" } else { "" }
                         $nszArgs = "$coresArg -C -l $($taskData.CompressionLevel) `"$finalFileInTemp`" -o `"$tempDir`""
                         $procConv = @{ Exe = $isolatedNsz.Exe; Args = $nszArgs; WorkingDir = $isolatedNsz.WorkingDir }
                         if ((Invoke-Tool $procConv "Сжатие в XCZ") -ne 0) { throw "Ошибка сжатия в XCZ." }
                         Remove-Item -LiteralPath $finalFileInTemp -Force
                         $finalFileInTemp = (Get-ChildItem -LiteralPath $tempDir -Filter "*.xcz" | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName
                    }
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
        
        # Финализация
        if ($taskData.TaskType -ne 'Unpack' -and $null -ne $finalFileInTemp -and (Test-Path -LiteralPath $finalFileInTemp)) {
            Write-WorkerProgress -Status "Финализация" -Percent 99; 
            Write-Header -Title "ФИНАЛИЗАЦИЯ" -Subtitle "Сохранение" -AppVersion $taskData.AppVersion
            $finalFileDir = if (-not [string]::IsNullOrWhiteSpace($taskData.OutDir)) { $taskData.OutDir } else { $toolPaths.odir }
            $currentFileName = Split-Path $finalFileInTemp -Leaf
            $finalFileName = Generate-CustomFileName -taskData $taskData -originalFileName $currentFileName -toolPaths $toolPaths -InputFilePath $finalFileInTemp
            $destinationPath = Join-Path -Path $finalFileDir -ChildPath $finalFileName
            
            if ($finalFileInTemp -ne $destinationPath) {
                if ((Split-Path -Path $finalFileInTemp -Parent) -eq $finalFileDir) { Rename-Item -LiteralPath $finalFileInTemp -NewName $finalFileName -Force } else { Move-Item -LiteralPath $finalFileInTemp -Destination $destinationPath -Force }
            }
            $result.FinalPath = $destinationPath;
            Write-WorkerLog "Файл успешно сохранен: $finalFileName" -Type 'SUCCESS'
        } elseif ($taskData.TaskType -eq 'Unpack') { $result.FinalPath = $finalUnpackPath }
        
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
                    $tempLogPath = $taskInfo.LogFile + ".tmp"
                    $maxRetries = 3; $retryCount = 0; $success = $false
                    while ($retryCount -lt $maxRetries -and -not $success) {
                        try { Move-Item -LiteralPath $taskInfo.LogFile -Destination $tempLogPath -Force -ErrorAction Stop; $success = $true } 
                        catch { $retryCount++; if ($retryCount -lt $maxRetries) { Start-Sleep -Milliseconds 100 } }
                    }
                    if ($success) {
                        $logEntries = Get-Content -LiteralPath $tempLogPath -Raw | ForEach-Object { $_ -split "`n" | Where-Object { $_ } | ForEach-Object { ConvertFrom-Json $_ } }
                        Remove-Item -LiteralPath $tempLogPath -Force -ErrorAction SilentlyContinue
                        foreach ($entry in $logEntries) { Write-Log -Message $entry.Message -Type $entry.Type -TaskID $entry.TaskID }
                    }
                } catch {}
            }

            if ($process) {
                if (Test-Path -LiteralPath $taskInfo.StatusFile) {
                    try {
                        $progressRecord = Import-Clixml -Path $taskInfo.StatusFile
                        if ($progressRecord -and $taskInfo.Row.DataGridView) {
                            $taskInfo.Row.Cells['Статус'].Value = $progressRecord.StatusDescription
                            if ($progressRecord.PercentComplete -ge 0) { $taskInfo.Row.Cells['Выполнение'].Value = [int]$progressRecord.PercentComplete }
                        }
                    } catch {}
                }
            } else {
                # Процесс завершен
                $taskRow = $taskInfo.Row; $taskData = $taskRow.Tag;
                $resultFile = Join-Path $script:wdir "result-$($taskData.TaskID).xml"
                $crashLog = Get-Item -Path (Join-Path $script:wdir "crash-*.log") -ErrorAction SilentlyContinue | Where-Object { $_.Name -like "*-$($procId).log" }
                
                if ($crashLog) {
                    $crashReason = Get-Content -LiteralPath $crashLog.FullName | Out-String; Write-Log "КРАХ ВОРКЕРА! Детали: $crashReason" -TaskID $taskData.TaskID -Type 'ERROR'; $taskRow.Cells['Статус'].Value = "Ошибка (КРАХ ВОРКЕРА)"; $taskRow.Cells['Выполнение'].Value = $null
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
                                $correctExtension = "." + $taskData.OutFormat.ToLower()
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
                    if ($finalResult.Status -eq 'Готово') { Write-Log "Задача '$($taskRow.Cells['Задача'].Value)' для '$($taskRow.Cells['Обработка'].Value)' успешно завершена." -TaskID $taskRow.Tag.TaskID -Type 'SUCCESS' } else { Write-Log "Задача '$($taskRow.Cells['Задача'].Value)' для '$($taskRow.Cells['Обработка'].Value)' завершилась со статусом: $($finalResult.Status)." -TaskID $taskRow.Tag.TaskID -Type 'ERROR' }
                } else {
                    $taskRow.Cells['Статус'].Value = "Ошибка (Крах)"; $taskRow.Cells['Выполнение'].Value = $null; Write-Log "Процесс задачи завершился аварийно. Файл результата не был создан." -TaskID $taskData.TaskID -Type 'ERROR'
                }
                $temp = $null; $script:runningTasks.TryRemove($procId, [ref]$temp)
                if (-not $script:DebugMode) { if ($crashLog) { Remove-Item -LiteralPath $crashLog.FullName -Force -ErrorAction SilentlyContinue }; Remove-Item -Path (Join-Path $script:wdir "*-$($taskData.TaskID).*"), (Join-Path $script:wdir "$($taskData.TaskID)") -Recurse -Force -ErrorAction SilentlyContinue }
                ProcessTaskQueue
            }
        }
    }

    foreach ($toolName in @($script:activeDownloads.Keys)) {
        $process = $script:activeDownloads[$toolName]
        if ($process.HasExited) {
            Write-Log "Процесс загрузки для '$toolName' завершен."; [void]$script:activeDownloads.Remove($toolName)
            $tempScriptPath = Join-Path $script:wdir "temp_dl_$($toolName).ps1"
            if (Test-Path -LiteralPath $tempScriptPath) { Remove-Item -LiteralPath $tempScriptPath -Force -ErrorAction SilentlyContinue }
            $systemTab = $script:f.Controls.Find("TabPanel_System", $true)
            if ($systemTab.Count -gt 0) { $grid = $systemTab[0].Controls.Find("systemFilesGrid", $true)[0]; Update-SystemFilesStatus -GridView $grid }
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
    $grid.ColumnHeadersBorderStyle = [DataGridViewHeaderBorderStyle]::Single; $grid.GridColor = [ColorTranslator]::FromHtml('#505050');
    $grid.BackgroundColor = [ColorTranslator]::FromHtml('#2D2D30'); $grid.Font = $script:regularFont; $grid.RowTemplate.Height = 24
    $headerStyle = New-Object DataGridViewCellStyle; $headerStyle.BackColor = [ColorTranslator]::FromHtml('#3E3E42');
    $headerStyle.ForeColor = [Color]::White; $headerStyle.Font = $script:boldFont; $headerStyle.Alignment = 'MiddleCenter'
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
    $grid.add_CellContentClick({
        param($s, $e); if($e.RowIndex -lt 0){ return }; $cell = $s.Rows[$e.RowIndex].Cells[$e.ColumnIndex]; if ($cell.OwningColumn.Name -ne 'Действие') { return }; $action = $cell.Value.ToString(); $toolName = $s.Rows[$e.RowIndex].Cells['Программа'].Value
        if ($action -eq 'СКАЧАТЬ') {
            $urls = @{ 'NSZ'='https://github.com/nicoboss/nsz/releases/download/4.6.1/nsz_v4.6.1_win64_portable.zip';'NSC_Builder'='https://github.com/julesontheroad/NSC_BUILDER/releases/download/1.01b/NSCB_101bx64.zip';'yanu-cli'='https://github.com/nozwock/yanu/releases/download/0.10.1/yanu-cli-x86_64-pc-windows-msvc.exe' }; $url = $urls[$ToolName]; if(-not $url) { return }
            Write-Log "Инициализация загрузки '$toolName'..."
            $scriptBlockContent = @"
param(`$ToolName, `$Url, `$WDirPath, `$NDirPath, `$NBDirPath, `$TDirPath)
function Write-HostLog { param(`$Message, `$Type='INFO', `$Color='White'); Write-Host "[$((Get-Date).ToString('HH:mm:ss'))] [`$Type] `$Message" -ForegroundColor `$Color }
`$tempPath = Join-Path `$WDirPath "dl_`$(Get-Random)"; try { Add-Type -AssemblyName System.IO.Compression.FileSystem; [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 -bor [Net.SecurityProtocolType]::Tls13; [void](New-Item -ItemType Directory -Force `$tempPath); `$dlFile = Join-Path `$tempPath (Split-Path `$Url -Leaf); Write-HostLog "Начинаю загрузку '`$ToolName'..." -Color 'Yellow'; (New-Object System.Net.WebClient).DownloadFile(`$Url, `$dlFile); if(-not(Test-Path -LiteralPath `$dlFile)){throw "Не удалось скачать файл."}; Write-HostLog "Файл скачан." -Color 'Green'; switch(`$ToolName){ 'NSZ' { `$targetDir = `$NDirPath; `$extractPath = Join-Path `$tempPath "extract"; Write-HostLog "Распаковка NSZ..." -Color 'Cyan'; [System.IO.Compression.ZipFile]::ExtractToDirectory(`$dlFile, `$extractPath); `$nszExe = Get-ChildItem -Path `$extractPath -Filter "nsz.exe" -Recurse | Select-Object -First 1; if(`$nszExe){ Move-Item -Path (`$nszExe.Directory.FullName + "\*") -Destination `$targetDir -Force }else{throw "nsz.exe не найден."} } 'NSC_Builder' { `$targetDir = `$NBDirPath; `$extractPath = Join-Path `$tempPath "extract"; Write-HostLog "Распаковка NSC_Builder..." -Color 'Cyan'; [System.IO.Compression.ZipFile]::ExtractToDirectory(`$dlFile, `$extractPath); `$nscbBat = Get-ChildItem -Path `$extractPath -Filter "NSCB.bat" -Recurse | Select-Object -First 1; if(`$nscbBat){ Move-Item -Path (`$nscbBat.Directory.FullName + "\*") -Destination `$targetDir -Force }else{throw "NSCB.bat не найден."} } 'yanu-cli' { Move-Item -LiteralPath `$dlFile -Destination (Join-Path `$TDirPath "yanu-cli.exe") -Force } } } catch { Write-HostLog "ОШИБКА установки '`$ToolName': `$(`$_.Exception.Message)" -Type 'ERROR' -Color 'Red' } finally { if(Test-Path `$tempPath){Remove-Item `$tempPath -Recurse -Force} }
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
    $systemFiles = @( @{Name='NSZ'; Path=$script:nsz_exe}, @{Name='NSC_Builder'; Path=$script:nscb_bat; SecondaryPath=$script:squirrel_exe}, @{Name='yanu-cli'; Path=$script:yanu_cli}, @{Name='Keys'; Path=$script:key} )
    $GridView.Rows.Clear()
    $allToolsInstalled = (Test-Path -LiteralPath $script:nsz_exe) -and (Test-Path -LiteralPath $script:nscb_bat) -and (Test-Path -LiteralPath $script:squirrel_exe) -and (Test-Path -LiteralPath $script:yanu_cli)
    foreach ($file in $systemFiles) {
        $isInstalled = $false;
        if($file.Name -eq 'Keys'){
            $isInstalled = (Test-Path -LiteralPath $file.Path) -and ((Test-Path -LiteralPath (Join-Path $script:ndir "prod.keys")) -or (Test-Path -LiteralPath (Join-Path $script:ndir "keys.txt"))) -and (Test-Path -LiteralPath (Join-Path $script:nbdir "ztools\keys.txt"))
        } else {
            $isInstalled = Test-Path -LiteralPath $file.Path
            if ($file.SecondaryPath -and -not (Test-Path -LiteralPath $file.SecondaryPath)) { $isInstalled = $false }
        }
        $status=if($isInstalled){'Установлено'}else{'Отсутствует'}; $actionText = ""
        if($file.Name -eq 'Keys') { if (-not $allToolsInstalled) { $status='Сначала установите утилиты'; $actionText='' } elseif ($isInstalled) { $actionText = 'ОБНОВИТЬ' } else { $actionText = 'ОБЗОР' } } else { if ($isInstalled) { $actionText = '✔' } else { $actionText = 'СКАЧАТЬ' } }
        if($script:activeDownloads.ContainsKey($file.Name)){ $status='Смотрите консоль...'; $actionText='Выполняется...' }
        $version='-';
        if($isInstalled){ try{ $version=switch($file.Name){ 'NSZ'{'4.6.1'} 'NSC_Builder'{'1.01b'} 'yanu-cli'{((& $file.Path -V 2>&1|Out-String).Trim().Split(' ')[-1])} 'Keys'{ if($script:settings -and $script:settings.KeysVersion) {$script:settings.KeysVersion} else {'-'} } } }catch{} }
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
            if (-not $button.Enabled) { $button.BackColor = [ColorTranslator]::FromHtml('#333333') }
        }
    }
    foreach ($outputControl in $script:outputControls.Values) {
        foreach ($fmtButton in $outputControl.FormatButtons) {
            $isEnabled = $true
            if ($fmtButton.Name -in 'NSZ', 'XCZ') { if (-not $script:nsz_f) { $isEnabled = $false } }
            if ($fmtButton.Name -in 'XCI', 'XCZ') { if (-not $script:nscb_f) { $isEnabled = $false } }
            $fmtButton.Enabled = $isEnabled
            if (-not $isEnabled) { $fmtButton.BackColor = [ColorTranslator]::FromHtml('#333333') } elseif ($outputControl.SelectedFormat -ne $fmtButton.Name) { $fmtButton.BackColor = [ColorTranslator]::FromHtml('#3E3E42') }
        }
    }
}
YEStart
}