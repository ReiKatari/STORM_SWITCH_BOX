@echo off
echo ==============================================
echo 1. Публикация .NET 8 проекта (win-x64)...
echo ==============================================
powershell -Command "Get-Process -Name STORM_SWITCH_BOX*, setup* -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue" 2>nul
if exist "e:\STORM SWITCH BOX\bin\Release" ( rmdir /s /q "e:\STORM SWITCH BOX\bin\Release" 2>nul )
if exist "e:\STORM SWITCH BOX\obj\Release" ( rmdir /s /q "e:\STORM SWITCH BOX\obj\Release" 2>nul )
dotnet publish "e:\STORM SWITCH BOX\StormSwitchBox.csproj" -c Release -r win-x64

echo ==============================================
echo 2. Очистка старого файла установки...
echo ==============================================
if exist "e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_3.9.12_Setup.exe" (
    del /f /q /a "e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_3.9.12_Setup.exe" 2>nul
)

echo ==============================================
echo 3. Компиляция установщика Inno Setup...
echo ==============================================
"C:\Program Files (x86)\Inno Setup\iscc.exe" "e:\STORM SWITCH BOX\installer\setup.iss"

echo ==============================================
echo 4. Упаковка портативного ZIP архива...
echo ==============================================
powershell -Command "if (Test-Path 'e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_3.9.12_win-x64.zip') { Remove-Item 'e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_3.9.12_win-x64.zip' }; Compress-Archive -Path 'e:\STORM SWITCH BOX\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\*' -DestinationPath 'e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_3.9.12_win-x64.zip'"
