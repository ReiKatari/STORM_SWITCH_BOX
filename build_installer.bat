@echo off
echo ==============================================
echo 1. Публикация .NET 8 проекта (win-x64)...
echo ==============================================
dotnet publish "e:\STORM SWITCH BOX\StormSwitchBox.csproj" -c Release -r win-x64

echo ==============================================
echo 2. Компиляция установщика Inno Setup...
echo ==============================================
"C:\Program Files (x86)\Inno Setup\iscc.exe" "e:\STORM SWITCH BOX\installer\setup.iss"

echo ==============================================
echo 3. Упаковка портативного ZIP архива...
echo ==============================================
powershell -Command "if (Test-Path 'e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_3.9.0_win-x64.zip') { Remove-Item 'e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_3.9.0_win-x64.zip' }; Compress-Archive -Path 'e:\STORM SWITCH BOX\publish\*' -DestinationPath 'e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_3.9.0_win-x64.zip'"
