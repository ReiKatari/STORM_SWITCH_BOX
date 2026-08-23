$ErrorActionPreference = "Stop"
$root = "e:\STORM SWITCH BOX"
Set-Location $root

Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "1. Publishing STORM SWITCH BOX (.NET 8 WinUI 3 win-x64)..." -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan

Get-Process | Where-Object { $_.ProcessName -match 'XamlCompiler|VBCSCompiler|msbuild|StormSwitchBox|StormInstaller' } | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

if (Test-Path "$root\bin\Release") { Remove-Item "$root\bin\Release" -Recurse -Force -ErrorAction SilentlyContinue }
if (Test-Path "$root\obj\Release") { Remove-Item "$root\obj\Release" -Recurse -Force -ErrorAction SilentlyContinue }
if (Test-Path "$root\installer\StormInstaller\bin") { Remove-Item "$root\installer\StormInstaller\bin" -Recurse -Force -ErrorAction SilentlyContinue }
if (Test-Path "$root\installer\StormInstaller\obj") { Remove-Item "$root\installer\StormInstaller\obj" -Recurse -Force -ErrorAction SilentlyContinue }

dotnet publish "$root\StormSwitchBox.csproj" -c Release -r win-x64 -p:UseSharedCompilation=false -p:NodeReuse=false

Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "2. Unblocking and signing published application and tools with RFC 3161 Timestamp..." -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan

$signtool = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.28000.0\x64\signtool.exe"
$certThumb = "8D31BDFA114987A887FB3F6255D023324731CF9C"
$tsUrl = "http://timestamp.digicert.com"

Get-ChildItem "$root\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish" -Recurse | Unblock-File -ErrorAction SilentlyContinue

& $signtool sign /fd SHA256 /tr $tsUrl /td SHA256 /d "STORM SWITCH BOX 4.7.0" /du "https://github.com/ReiKatari/STORM_SWITCH_BOX" /sha1 $certThumb "$root\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\StormSwitchBox.exe"
& $signtool sign /fd SHA256 /tr $tsUrl /td SHA256 /d "STORM SWITCH BOX 4.7.0" /du "https://github.com/ReiKatari/STORM_SWITCH_BOX" /sha1 $certThumb "$root\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\StormSwitchBox.dll"

Get-ChildItem "$root\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\tools" -Recurse -Filter *.exe | ForEach-Object {
    & $signtool sign /fd SHA256 /tr $tsUrl /td SHA256 /d "STORM SWITCH BOX 4.7.0" /du "https://github.com/ReiKatari/STORM_SWITCH_BOX" /sha1 $certThumb $_.FullName
}

Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "3. Packaging portable ZIP archive..." -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan

$outputDir = "$root\installer\Output"
if (-not (Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir | Out-Null }
if (Test-Path "$root\installer\STORM_Certificate.cer") { Copy-Item "$root\installer\STORM_Certificate.cer" "$root\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\" -Force }

$portableZip = "$outputDir\STORM_SWITCH_BOX_4.7.0_win-x64.zip"
if (Test-Path $portableZip) { Remove-Item $portableZip -Force }

if (Test-Path "$root\tools\7z.exe") {
    & "$root\tools\7z.exe" a -tzip -mx=7 -mmt=on $portableZip "$root\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\."
} else {
    Compress-Archive -Path "$root\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\*" -DestinationPath $portableZip -Force
}

Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "4. Compiling custom StormInstaller (Cyber Dark UI standard)..." -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan

dotnet publish "$root\installer\StormInstaller\StormInstaller.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true

$installerExe = "$root\installer\StormInstaller\bin\Release\net8.0-windows\win-x64\publish\StormInstaller.exe"
$setupExe = "$outputDir\STORM_SWITCH_BOX_4.7.0_Setup.exe"

if (Test-Path $installerExe) {
    Copy-Item $installerExe $setupExe -Force
} else {
    throw "Error: StormInstaller.exe was not created!"
}

Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "5. Signing STORM_SWITCH_BOX_4.7.0_Setup.exe with RFC 3161 Timestamp..." -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan

& $signtool sign /fd SHA256 /tr $tsUrl /td SHA256 /d "STORM SWITCH BOX 4.7.0" /du "https://github.com/ReiKatari/STORM_SWITCH_BOX" /sha1 $certThumb $setupExe

Get-ChildItem $outputDir -Recurse | Unblock-File -ErrorAction SilentlyContinue

if (Test-Path "$root\installer\STORM_Certificate.cer") {
    Copy-Item "$root\installer\STORM_Certificate.cer" "$outputDir\STORM_Certificate.cer" -Force
    & certutil.exe -user -addstore -f "TrustedPublisher" "$root\installer\STORM_Certificate.cer" | Out-Null
    & certutil.exe -addstore -f "TrustedPublisher" "$root\installer\STORM_Certificate.cer" | Out-Null
    & certutil.exe -addstore -f "Root" "$root\installer\STORM_Certificate.cer" | Out-Null
}

Get-ChildItem "$root\installer" -Filter "*.cmd" | ForEach-Object { Copy-Item $_.FullName "$outputDir\" -Force }
Get-ChildItem "$root\installer" -Filter "*.bat" | ForEach-Object { Copy-Item $_.FullName "$outputDir\" -Force }

Write-Host "==============================================" -ForegroundColor Cyan
Write-Host "6. Packaging Smart App Control Setup Bundle..." -ForegroundColor Cyan
Write-Host "==============================================" -ForegroundColor Cyan

$bundleZip = "$outputDir\STORM_SWITCH_BOX_4.7.0_Setup_Bundle.zip"
if (Test-Path $bundleZip) { Remove-Item $bundleZip -Force }

if (Test-Path "$root\tools\7z.exe") {
    $bundleItems = @(
        $setupExe,
        "$outputDir\STORM_Certificate.cer"
    )
    Get-ChildItem $outputDir -Filter "*.cmd" | ForEach-Object { $bundleItems += $_.FullName }
    Get-ChildItem $outputDir -Filter "*.bat" | ForEach-Object { $bundleItems += $_.FullName }
    
    & "$root\tools\7z.exe" a -tzip -mx=7 -mmt=on $bundleZip $bundleItems
}

Write-Host "==============================================" -ForegroundColor Green
Write-Host "BUILD AND PACKAGING COMPLETED SUCCESSFULLY!" -ForegroundColor Green
Write-Host "1. Installer:      $setupExe" -ForegroundColor Green
Write-Host "2. Setup Bundle:   $bundleZip" -ForegroundColor Green
Write-Host "3. Portable:       $portableZip" -ForegroundColor Green
Write-Host "==============================================" -ForegroundColor Green
