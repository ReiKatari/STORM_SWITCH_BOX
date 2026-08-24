@echo off
title STORM SWITCH BOX 4.7.1 — Установка
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-ChildItem -Path . -Recurse | Unblock-File -ErrorAction SilentlyContinue"
if exist "STORM_Certificate.cer" (
    certutil.exe -user -addstore -f "Root" "STORM_Certificate.cer" >nul 2>&1
    certutil.exe -user -addstore -f "TrustedPublisher" "STORM_Certificate.cer" >nul 2>&1
    certutil.exe -addstore -f "Root" "STORM_Certificate.cer" >nul 2>&1
    certutil.exe -addstore -f "TrustedPublisher" "STORM_Certificate.cer" >nul 2>&1
)
if exist "STORM_SWITCH_BOX_4.7.1_Setup.exe" (
    start "" "STORM_SWITCH_BOX_4.7.1_Setup.exe"
)
