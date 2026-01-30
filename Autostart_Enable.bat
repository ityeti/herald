@echo off
:: Enable Herald auto-start via Windows Startup folder shortcut
:: Runs at Windows logon with administrator privileges

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
echo   Herald - Enable Auto-Start
echo ========================================
echo.

:: Run the PowerShell script
powershell -ExecutionPolicy Bypass -File "%~dp0scripts\task_scheduler_setup.ps1"

echo.
pause
