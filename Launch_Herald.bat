@echo off
:: Herald Text-to-Speech Launcher
:: Auto-setup on first run, then launches the application

:: Check if already running as admin
net session >nul 2>&1
if %errorlevel% == 0 (
    goto :main
) else (
    :: Request admin privileges
    powershell -Command "Start-Process -Verb RunAs -FilePath '%~f0'"
    exit /b
)

:main
cd /d "%~dp0"
title Herald TTS
echo.
echo ========================================
echo   Herald - Text-to-Speech
echo ========================================
echo.

:: Check for Python
where python >nul 2>&1
if %errorlevel% neq 0 (
    echo [ERROR] Python not found in PATH.
    echo.
    echo Please install Python 3.10+ from:
    echo   https://www.python.org/downloads/
    echo.
    echo Make sure to check "Add Python to PATH" during installation.
    echo.
    pause
    exit /b 1
)

:: Check if venv exists
if not exist "venv\Scripts\python.exe" (
    echo [Setup] Creating virtual environment...
    python -m venv venv
    if %errorlevel% neq 0 (
        echo [ERROR] Failed to create virtual environment.
        pause
        exit /b 1
    )
    echo [Setup] Virtual environment created.
    echo.
)

:: Check if dependencies are installed (check for pyttsx3 as marker)
if not exist "venv\Lib\site-packages\pyttsx3" (
    echo [Setup] Installing dependencies...
    echo.
    venv\Scripts\pip.exe install -r requirements.txt
    if %errorlevel% neq 0 (
        echo.
        echo [ERROR] Failed to install dependencies.
        echo         Check your internet connection and try again.
        pause
        exit /b 1
    )
    echo.
    echo [Setup] Dependencies installed successfully.
    echo.
)

:: Ready to run
echo   Alt+S   Speak selection/clipboard
echo   Alt+O   OCR region (one-time)
echo   Alt+M   Monitor region (persistent)
echo   Alt+P   Pause/resume
echo   Alt+]   Speed up  /  Alt+[  Slow down
echo   Escape  Stop speaking
echo   Alt+Q   Quit
echo ========================================
echo.

venv\Scripts\python.exe src\main.py
set EXIT_CODE=%errorlevel%

:: Pause on error OR if .debug file exists (for debugging)
if %EXIT_CODE% neq 0 (
    echo.
    echo Application exited with error code %EXIT_CODE%
    pause
) else if exist ".debug" (
    echo.
    echo [Debug mode] Press any key to close...
    pause >nul
)
