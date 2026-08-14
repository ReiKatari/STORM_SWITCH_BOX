@echo off
echo ==============================================
echo 1. Publishing .NET 8 project (win-x64)...
echo ==============================================
taskkill /f /im StormSwitchBox.exe 2>nul
taskkill /f /im STORM_SWITCH_BOX* 2>nul
if exist "e:\STORM SWITCH BOX\bin\Release" ( rmdir /s /q "e:\STORM SWITCH BOX\bin\Release" 2>nul )
if exist "e:\STORM SWITCH BOX\obj\Release" ( rmdir /s /q "e:\STORM SWITCH BOX\obj\Release" 2>nul )
dotnet publish "e:\STORM SWITCH BOX\StormSwitchBox.csproj" -c Release -r win-x64

echo ==============================================
echo 1.1. Unblocking and signing tools in tools/...
echo ==============================================
powershell -ExecutionPolicy Bypass -Command "Get-ChildItem 'e:\STORM SWITCH BOX\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\tools' -Recurse -Filter *.exe | Unblock-File -ErrorAction SilentlyContinue; Get-ChildItem 'e:\STORM SWITCH BOX\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\tools' -Recurse -Filter *.exe | ForEach-Object { & 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.28000.0\x64\signtool.exe' sign /fd SHA256 /sha1 8D31BDFA114987A887FB3F6255D023324731CF9C $_.FullName }"

echo ==============================================
echo 2. Cleaning old installer file...
echo ==============================================
if exist "e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_4.3.7_Setup.exe" (
    powershell -Command "Remove-Item 'e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_4.3.7_Setup.exe' -Force -ErrorAction SilentlyContinue"
    if exist "e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_4.3.7_Setup.exe" (
        taskkill /f /im STORM_SWITCH_BOX* 2>nul
        taskkill /f /im setup* 2>nul
        taskkill /f /im signtool* 2>nul
        taskkill /f /im iscc* 2>nul
        timeout /t 2 /nobreak >nul
        del /f /q /a "e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_4.3.7_Setup.exe" 2>nul
    )
)

echo ==============================================
echo 3. Compiling and signing Inno Setup installer (ISCC SignTool)...
echo ==============================================
"C:\Program Files (x86)\Inno Setup\iscc.exe" "/Ssigntool=C:\Program Files (x86)\Windows Kits\10\bin\10.0.28000.0\x64\signtool.exe sign /fd SHA256 /sha1 8D31BDFA114987A887FB3F6255D023324731CF9C $f" "e:\STORM SWITCH BOX\installer\setup.iss"

echo ==============================================
echo 4. Packaging portable ZIP archive...
echo ==============================================
powershell -Command "if (Test-Path 'e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_4.3.7_win-x64.zip') { Remove-Item 'e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_4.3.7_win-x64.zip' }; Compress-Archive -Path 'e:\STORM SWITCH BOX\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\*' -DestinationPath 'e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_4.3.7_win-x64.zip'"
