@echo off
chcp 65001 >nul
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build_Release.ps1"
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ❌ Ошибка при сборке!
    pause
    exit /b %ERRORLEVEL%
)
