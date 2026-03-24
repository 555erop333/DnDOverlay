@echo off
cd /d "%~dp0"

if not exist "publish\Release\win-x64\DnDOverlay.App.exe" (
    echo Application not found! Please run 1_Build.bat first.
    pause
    exit /b 1
)

start "" "publish\Release\win-x64\DnDOverlay.App.exe"
