@echo off
echo ==============================================
echo 1. Публикация .NET 8 проекта (win-x64)...
echo ==============================================
dotnet publish "e:\STORM SWITCH BOX\StormSwitchBox.csproj" -c Release -r win-x64

echo ==============================================
echo 2. Очистка старого файла установки...
echo ==============================================
if exist "e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_3.9.1_Setup.exe" (
    del /f /q "e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_3.9.1_Setup.exe" 2>nul
)

echo ==============================================
echo 3. Компиляция установщика Inno Setup...
echo ==============================================
"C:\Program Files (x86)\Inno Setup\iscc.exe" "e:\STORM SWITCH BOX\installer\setup.iss"

echo ==============================================
echo 4. Упаковка портативного ZIP архива...
echo ==============================================
powershell -Command "if (Test-Path 'e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_3.9.1_win-x64.zip') { Remove-Item 'e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_3.9.1_win-x64.zip' }; Compress-Archive -Path 'e:\STORM SWITCH BOX\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\*' -DestinationPath 'e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_3.9.1_win-x64.zip'"
