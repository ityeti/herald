@echo off
:: Disable Herald auto-start (removes Startup folder shortcut)

:: Check if already running as admin
net session >nul 2>&1
if %errorlevel% == 0 (
    goto :main
) else (
    :: Request admin privileges
    echo Requesting administrator privileges...
    powershell -Command "Start-Process -Verb RunAs -FilePath '%~f0'"
    exit /b
)

:main
cd /d "%~dp0"
echo.
echo ========================================
echo   Herald - Disable Auto-Start
echo ========================================
echo.

:: Run the PowerShell script
powershell -ExecutionPolicy Bypass -File "%~dp0scripts\task_scheduler_remove.ps1"

echo.
pause
