@echo off
echo ==============================================
echo 1. Публикация .NET 8 проекта (win-x64)...
echo ==============================================
powershell -Command "Get-Process -Name STORM_SWITCH_BOX*, setup* -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue" 2>nul
if exist "e:\STORM SWITCH BOX\bin\Release" ( rmdir /s /q "e:\STORM SWITCH BOX\bin\Release" 2>nul )
if exist "e:\STORM SWITCH BOX\obj\Release" ( rmdir /s /q "e:\STORM SWITCH BOX\obj\Release" 2>nul )
dotnet publish "e:\STORM SWITCH BOX\StormSwitchBox.csproj" -c Release -r win-x64

echo ==============================================
echo 1.1. Цифровая подпись всех бинарных утилит в tools/...
echo ==============================================
powershell -ExecutionPolicy Bypass -Command "Get-ChildItem 'e:\STORM SWITCH BOX\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\tools' -Recurse -Filter '*.exe' | ForEach-Object { & 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.28000.0\x64\signtool.exe' sign /fd SHA256 /sha1 8D31BDFA114987A887FB3F6255D023324731CF9C $_.FullName }" 2>nul

echo ==============================================
echo 2. Очистка старого файла установки...
echo ==============================================
if exist "e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_4.1.1_Setup.exe" (
    powershell -Command "Remove-Item 'e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_4.1.1_Setup.exe' -Force -ErrorAction SilentlyContinue"
    if exist "e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_4.1.1_Setup.exe" (
        powershell -Command "Stop-Process -Name 'STORM_SWITCH_BOX*', 'setup*', 'signtool*', 'iscc*' -Force -ErrorAction SilentlyContinue"
        timeout /t 2 /nobreak >nul
        del /f /q /a "e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_4.1.1_Setup.exe" 2>nul
    )
)

echo ==============================================
echo 3. Компиляция и подпись установщика Inno Setup (ISCC SignTool)...
echo ==============================================
"C:\Program Files (x86)\Inno Setup\iscc.exe" "/Ssigntool=C:\Program Files (x86)\Windows Kits\10\bin\10.0.28000.0\x64\signtool.exe sign /fd SHA256 /sha1 8D31BDFA114987A887FB3F6255D023324731CF9C $f" "e:\STORM SWITCH BOX\installer\setup.iss"

echo ==============================================
echo 4. Упаковка портативного ZIP архива...
echo ==============================================
powershell -Command "if (Test-Path 'e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_4.1.1_win-x64.zip') { Remove-Item 'e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_4.1.1_win-x64.zip' }; Compress-Archive -Path 'e:\STORM SWITCH BOX\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\*' -DestinationPath 'e:\STORM SWITCH BOX\installer\Output\STORM_SWITCH_BOX_4.1.1_win-x64.zip'"
