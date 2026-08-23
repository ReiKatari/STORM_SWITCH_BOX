@echo off
echo ==============================================
echo 1. Publishing STORM SWITCH BOX (.NET 8 WinUI 3 win-x64)...
echo ==============================================
taskkill /f /im StormSwitchBox.exe 2>nul
taskkill /f /im STORM_SWITCH_BOX* 2>nul
taskkill /f /im StormInstaller.exe 2>nul
taskkill /f /im VBCSCompiler.exe 2>nul
taskkill /f /im XamlCompiler.exe 2>nul
taskkill /f /im msbuild.exe 2>nul
dotnet build-server shutdown >nul 2>nul
powershell -Command "Get-Process | Where-Object { $_.ProcessName -match 'XamlCompiler|VBCSCompiler|msbuild|StormSwitchBox|StormInstaller' } | Stop-Process -Force -ErrorAction SilentlyContinue" 2>nul
timeout /t 1 /nobreak >nul
if exist "e:\STORM SWITCH BOX\bin\Release" ( rmdir /s /q "e:\STORM SWITCH BOX\bin\Release" 2>nul )
if exist "e:\STORM SWITCH BOX\obj\Release" ( rmdir /s /q "e:\STORM SWITCH BOX\obj\Release" 2>nul )
if exist "e:\STORM SWITCH BOX\installer\StormInstaller\bin" ( rmdir /s /q "e:\STORM SWITCH BOX\installer\StormInstaller\bin" 2>nul )
if exist "e:\STORM SWITCH BOX\installer\StormInstaller\obj" ( rmdir /s /q "e:\STORM SWITCH BOX\installer\StormInstaller\obj" 2>nul )

dotnet publish "e:\STORM SWITCH BOX\StormSwitchBox.csproj" -c Release -r win-x64 -p:UseSharedCompilation=false -p:NodeReuse=false

echo ==============================================
echo 2. Unblocking and signing published application and tools...
echo ==============================================
powershell -ExecutionPolicy Bypass -Command "Get-ChildItem 'e:\STORM SWITCH BOX\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish' -Recurse | Unblock-File -ErrorAction SilentlyContinue; & 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.28000.0\x64\signtool.exe' sign /fd SHA256 /d 'STORM SWITCH BOX 4.7.0' /du 'https://github.com/ReiKatari/STORM_SWITCH_BOX' /sha1 F8A8D6D6A6954867F08F480210CA0A81F2FEF756 'e:\STORM SWITCH BOX\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\StormSwitchBox.exe'; & 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.28000.0\x64\signtool.exe' sign /fd SHA256 /d 'STORM SWITCH BOX 4.7.0' /du 'https://github.com/ReiKatari/STORM_SWITCH_BOX' /sha1 F8A8D6D6A6954867F08F480210CA0A81F2FEF756 'e:\STORM SWITCH BOX\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\StormSwitchBox.dll'; Get-ChildItem 'e:\STORM SWITCH BOX\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\tools' -Recurse -Filter *.exe | ForEach-Object { & 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.28000.0\x64\signtool.exe' sign /fd SHA256 /d 'STORM SWITCH BOX 4.7.0' /du 'https://github.com/ReiKatari/STORM_SWITCH_BOX' /sha1 F8A8D6D6A6954867F08F480210CA0A81F2FEF756 $_.FullName }"

echo ==============================================
echo 3. Packaging portable ZIP archive...
echo ==============================================
if not exist "e:\STORM SWITCH BOX\installer\Output" ( mkdir "e:\STORM SWITCH BOX\installer\Output" )
if exist "e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_4.7.0_win-x64.zip" ( del /f /q "e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_4.7.0_win-x64.zip" )
if exist "e:\STORM SWITCH BOX\tools\7z.exe" (
    "e:\STORM SWITCH BOX\tools\7z.exe" a -tzip -mx=7 -mmt=on "e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_4.7.0_win-x64.zip" "e:\STORM SWITCH BOX\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\."
) else (
    powershell -Command "Compress-Archive -Path 'e:\STORM SWITCH BOX\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\*' -DestinationPath 'e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_4.7.0_win-x64.zip' -Force"
)

echo ==============================================
echo 4. Compiling custom StormInstaller (Cyber Dark UI standard)...
echo ==============================================
dotnet publish "e:\STORM SWITCH BOX\installer\StormInstaller\StormInstaller.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true

if exist "e:\STORM SWITCH BOX\installer\StormInstaller\bin\Release\net8.0-windows\win-x64\publish\StormInstaller.exe" (
    copy /y "e:\STORM SWITCH BOX\installer\StormInstaller\bin\Release\net8.0-windows\win-x64\publish\StormInstaller.exe" "e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_4.7.0_Setup.exe" >nul
) else (
    echo Error: StormInstaller.exe was not created!
    exit /b 1
)

echo ==============================================
echo 5. Signing STORM_SWITCH_BOX_4.7.0_Setup.exe (Authenticode SHA-256)...
echo ==============================================
"C:\Program Files (x86)\Windows Kits\10\bin\10.0.28000.0\x64\signtool.exe" sign /fd SHA256 /d "STORM SWITCH BOX 4.7.0" /du "https://github.com/ReiKatari/STORM_SWITCH_BOX" /sha1 F8A8D6D6A6954867F08F480210CA0A81F2FEF756 "e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_4.7.0_Setup.exe"

powershell -ExecutionPolicy Bypass -Command "Get-ChildItem 'e:\STORM SWITCH BOX\installer\Output' -Recurse | Unblock-File -ErrorAction SilentlyContinue; if (Test-Path 'e:\STORM SWITCH BOX\installer\STORM_Certificate.cer') { & certutil.exe -user -addstore -f 'TrustedPublisher' 'e:\STORM SWITCH BOX\installer\STORM_Certificate.cer' *>$null; & certutil.exe -addstore -f 'TrustedPublisher' 'e:\STORM SWITCH BOX\installer\STORM_Certificate.cer' *>$null; & certutil.exe -addstore -f 'Root' 'e:\STORM SWITCH BOX\installer\STORM_Certificate.cer' *>$null; }"

echo ==============================================
echo BUILD AND PACKAGING COMPLETED SUCCESSFULLY!
echo 1. Installer: e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_4.7.0_Setup.exe
echo 2. Portable:  e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_4.7.0_win-x64.zip
echo ==============================================
