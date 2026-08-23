@echo off
title STORM SWITCH BOX — Регистрация сертификата
cd /d "%~dp0"
if exist "STORM_Certificate.cer" (
    echo Установка сертификата STORM TEAM...
    certutil.exe -user -addstore -f "Root" "STORM_Certificate.cer"
    certutil.exe -user -addstore -f "TrustedPublisher" "STORM_Certificate.cer"
    certutil.exe -addstore -f "Root" "STORM_Certificate.cer"
    certutil.exe -addstore -f "TrustedPublisher" "STORM_Certificate.cer"
    echo Сертификат успешно добавлен в Доверенные корневые центры.
)
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -Path . -Recurse | Unblock-File -ErrorAction SilentlyContinue"
pause
