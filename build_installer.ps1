# STORM SWITCH BOX - Automated Production Build and Release Pipeline (STORM ALL PROJECTS FORMAT)
$ErrorActionPreference = "Stop"

$baseDir = $PSScriptRoot
if (-not $baseDir) { $baseDir = "E:\STORM SWITCH BOX" }
$sourcesDir = $baseDir
$appProjDir = $baseDir
$installerProjDir = Join-Path $baseDir "installer\StormInstaller"
$assemblingDir = Join-Path $baseDir "Assembling"
$filesDir = Join-Path $baseDir "Files"
$outputDir = Join-Path $baseDir "installer\Output"

if (-not (Test-Path $assemblingDir)) { New-Item -ItemType Directory -Path $assemblingDir | Out-Null }
if (-not (Test-Path $filesDir)) { New-Item -ItemType Directory -Path $filesDir | Out-Null }
if (-not (Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir | Out-Null }

$appVersion = "5.0.16"
try {
    [xml]$appProjXml = Get-Content (Join-Path $appProjDir "StormSwitchBox.csproj")
    $verFromProj = $appProjXml.Project.PropertyGroup.Version
    if ($verFromProj) { $appVersion = $verFromProj }
} catch { }

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "   STORM SWITCH BOX $appVersion - STORM ALL PROJECTS FORMAT" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

$setupExeName = "STORM_SWITCH_BOX_${appVersion}_Setup.exe"
$setupExePath = Join-Path $filesDir $setupExeName
$outputSetupExePath = Join-Path $outputDir $setupExeName
$portableZipPath = Join-Path $outputDir "STORM_SWITCH_BOX_${appVersion}_win-x64.zip"
$bundleZipPath = Join-Path $outputDir "STORM_SWITCH_BOX_${appVersion}_Setup_Bundle.zip"

# Step 0: Terminate running instances
Write-Host "[0/6] Closing running instances to release file locks..." -ForegroundColor Yellow
cmd.exe /c "taskkill /F /IM StormSwitchBox.exe /T >nul 2>&1"
cmd.exe /c "taskkill /F /IM StormInstaller.exe /T >nul 2>&1"
Get-Process "StormSwitchBox", "StormInstaller", "XamlCompiler", "VBCSCompiler", "msbuild" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

# Step 1: Clean build outputs
Write-Host "[1/6] Cleaning build directories..." -ForegroundColor Yellow
if (Test-Path "$appProjDir\bin") { Remove-Item "$appProjDir\bin" -Recurse -Force -ErrorAction SilentlyContinue }
if (Test-Path "$appProjDir\obj") { Remove-Item "$appProjDir\obj" -Recurse -Force -ErrorAction SilentlyContinue }
if (Test-Path "$installerProjDir\bin") { Remove-Item "$installerProjDir\bin" -Recurse -Force -ErrorAction SilentlyContinue }
if (Test-Path "$installerProjDir\obj") { Remove-Item "$installerProjDir\obj" -Recurse -Force -ErrorAction SilentlyContinue }
if (Test-Path $assemblingDir) { Remove-Item $assemblingDir -Recurse -Force -ErrorAction SilentlyContinue }
if (Test-Path $portableZipPath) { Remove-Item $portableZipPath -Force -ErrorAction SilentlyContinue }
if (Test-Path $bundleZipPath) { Remove-Item $bundleZipPath -Force -ErrorAction SilentlyContinue }
if (Test-Path $outputSetupExePath) { Remove-Item $outputSetupExePath -Force -ErrorAction SilentlyContinue }

# Step 2: Build App to Assembling and Publish folder
Write-Host "[2/6] Building StormSwitchBox (.NET 8 WinUI 3 win-x64)..." -ForegroundColor Yellow
$publishDir = Join-Path $appProjDir "bin\Release\net8.0-windows10.0.19041.0\win-x64"
dotnet build "$appProjDir\StormSwitchBox.csproj" -c Release

if (-not (Test-Path "$publishDir\StormSwitchBox.exe")) {
    throw "Error: Build failed - StormSwitchBox.exe was not created in $publishDir!"
}

# Step 3: Digital Signature with STORM TEAM Master Certificate and RFC 3161 Timestamp
Write-Host "[3/6] Applying digital signature (STORM TEAM Authenticode SHA-256 + RFC 3161)..." -ForegroundColor Yellow
$signtool = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.28000.0\x64\signtool.exe"
$tsUrl = "http://timestamp.digicert.com"

# Strictly enforce STORM TEAM Master Certificate (Publisher displayed in UAC: STORM TEAM)
$cert = $null
try {
    $store = [System.Security.Cryptography.X509Certificates.X509Store]::new('My', 'CurrentUser')
    $store.Open('ReadOnly')
    $cert = $store.Certificates | Where-Object { $_.HasPrivateKey -and $_.Subject -like "*CN=STORM TEAM*" } | Select-Object -First 1
    $store.Close()
} catch { }

if (-not $cert) {
    try {
        $store = [System.Security.Cryptography.X509Certificates.X509Store]::new('My', 'LocalMachine')
        $store.Open('ReadOnly')
        $cert = $store.Certificates | Where-Object { $_.HasPrivateKey -and $_.Subject -like "*CN=STORM TEAM*" } | Select-Object -First 1
        $store.Close()
    } catch { }
}

if (-not $cert) {
    throw "CRITICAL ERROR: STORM TEAM Master Certificate (CN=STORM TEAM) not found in certificate store! UAC Publisher requires STORM TEAM."
}

$certThumb = $cert.Thumbprint
Write-Host "  -> Master Certificate: $($cert.Subject) [$certThumb]" -ForegroundColor Green

# Export CER and copy PFX to root, Files, installer
$cerRoot = Join-Path $baseDir "STORM_Certificate.cer"
$cerFiles = Join-Path $filesDir "STORM_Certificate.cer"
$cerInstaller = Join-Path $baseDir "installer\STORM_Certificate.cer"
$cerOutput = Join-Path $outputDir "STORM_Certificate.cer"

[System.IO.File]::WriteAllBytes($cerRoot, $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert))
Copy-Item $cerRoot $cerFiles -Force
Copy-Item $cerRoot $cerInstaller -Force
Copy-Item $cerRoot $cerOutput -Force
Copy-Item $cerRoot (Join-Path $publishDir "STORM_Certificate.cer") -Force

$pfxSource = "E:\STORM SYSTEM OPTIMIZER\Files\STORM_CodeSign.pfx"
if (Test-Path $pfxSource) {
    Copy-Item $pfxSource (Join-Path $filesDir "STORM_CodeSign.pfx") -Force
    Copy-Item $pfxSource (Join-Path $baseDir "STORM_CodeSign.pfx") -Force
}

# Unblock published files
Get-ChildItem $publishDir -Recurse | Unblock-File -ErrorAction SilentlyContinue

# Sign binaries
& $signtool sign /fd SHA256 /tr $tsUrl /td SHA256 /d "STORM SWITCH BOX $appVersion" /du "https://github.com/ReiKatari/STORM_SWITCH_BOX" /sha1 $certThumb "$publishDir\StormSwitchBox.exe"
& $signtool sign /fd SHA256 /tr $tsUrl /td SHA256 /d "STORM SWITCH BOX $appVersion" /du "https://github.com/ReiKatari/STORM_SWITCH_BOX" /sha1 $certThumb "$publishDir\StormSwitchBox.dll"

Get-ChildItem "$publishDir\tools" -Recurse -Filter *.exe | ForEach-Object {
    & $signtool sign /fd SHA256 /tr $tsUrl /td SHA256 /d "STORM SWITCH BOX $appVersion" /du "https://github.com/ReiKatari/STORM_SWITCH_BOX" /sha1 $certThumb $_.FullName
}

# Copy to Assembling
Write-Host "[4/6] Synchronizing Assembling directory..." -ForegroundColor Yellow
if (-not (Test-Path $assemblingDir)) { New-Item -ItemType Directory -Path $assemblingDir | Out-Null }
Copy-Item "$publishDir\*" $assemblingDir -Recurse -Force

# Package portable zip
Write-Host "  -> Packaging Portable ZIP..." -ForegroundColor Yellow
if (Test-Path $portableZipPath) { Remove-Item $portableZipPath -Force -ErrorAction SilentlyContinue }
if (Test-Path "$baseDir\tools\7z.exe") {
    & "$baseDir\tools\7z.exe" a -tzip -mx=7 -mmt=on $portableZipPath "$publishDir\*"
} else {
    Compress-Archive -Path "$publishDir\*" -DestinationPath $portableZipPath -Force
}
Copy-Item $portableZipPath $filesDir -Force
Copy-Item $portableZipPath (Join-Path $outputDir "STORM_SWITCH_BOX_${appVersion}.zip") -Force
Copy-Item $portableZipPath (Join-Path $filesDir "STORM_SWITCH_BOX_${appVersion}.zip") -Force

# Step 4: Build Custom StormInstaller
Write-Host "[5/6] Building and Signing StormInstaller (Cyber Dark UI)..." -ForegroundColor Yellow
dotnet publish "$installerProjDir\StormInstaller.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true

$publishedInstaller = "$installerProjDir\bin\Release\net8.0-windows\win-x64\publish\StormInstaller.exe"
if (-not (Test-Path $publishedInstaller)) {
    throw "Error: StormInstaller.exe was not created!"
}

# Copy to Files and Output
Copy-Item $publishedInstaller $setupExePath -Force
Copy-Item $publishedInstaller $outputSetupExePath -Force

# Sign Installers
& $signtool sign /fd SHA256 /tr $tsUrl /td SHA256 /d "STORM SWITCH BOX $appVersion" /du "https://github.com/ReiKatari/STORM_SWITCH_BOX" /sha1 $certThumb $setupExePath
& $signtool sign /fd SHA256 /tr $tsUrl /td SHA256 /d "STORM SWITCH BOX $appVersion" /du "https://github.com/ReiKatari/STORM_SWITCH_BOX" /sha1 $certThumb $outputSetupExePath

# Install certificate locally into TrustedPublisher for seamless local execution
try {
    Import-Certificate -FilePath $cerRoot -CertStoreLocation "Cert:\CurrentUser\TrustedPublisher" -ErrorAction SilentlyContinue | Out-Null
    Import-Certificate -FilePath $cerRoot -CertStoreLocation "Cert:\CurrentUser\Root" -ErrorAction SilentlyContinue | Out-Null
} catch { }

# Removed Setup Bundle generation as per user request.

# Step 6: Unblock output files
Get-ChildItem -Path $outputDir, $filesDir -Recurse -Include *.exe, *.dll, *.bat, *.cmd, *.ps1, *.cer -ErrorAction SilentlyContinue | ForEach-Object {
    Unblock-File -Path $_.FullName -ErrorAction SilentlyContinue
}

Write-Host "============================================================" -ForegroundColor Green
Write-Host "BUILD AND PACKAGING COMPLETED ACCORDING TO STORM STANDARDS!" -ForegroundColor Green
Write-Host "1. Installer (Files):     $setupExePath" -ForegroundColor Green
Write-Host "2. Installer (Output):    $outputSetupExePath" -ForegroundColor Green
Write-Host "4. Portable Archive:      $portableZipPath" -ForegroundColor Green
Write-Host "5. Certificate:           $cerOutput" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green



