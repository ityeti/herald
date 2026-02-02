@echo off
:: Complete uninstall for Herald TTS
:: Removes: virtual environment, logs, settings, temp files, and startup shortcuts

cd /d "%~dp0"
echo.
echo ========================================
echo   Herald TTS - Complete Uninstall
echo ========================================
echo.
echo This will remove:
echo   - venv\ folder (Python virtual environment)
echo   - logs\ folder (application logs)
echo   - temp\ folder (temporary audio files)
echo   - config\settings.json (your personal settings)
echo   - Startup shortcut (if enabled)
echo   - __pycache__ and .pytest_cache folders
echo.
echo The source code will remain. To fully delete, remove this folder.
echo.

set /p CONFIRM="Are you sure you want to uninstall everything? (y/n): "
if /i not "%CONFIRM%"=="y" (
    echo.
    echo Uninstall cancelled.
    pause
    exit /b 0
)

echo.

:: Remove Startup shortcut if it exists
echo [Cleanup] Checking for Startup shortcut...
set "SHORTCUT=%APPDATA%\Microsoft\Windows\Start Menu\Programs\Startup\Herald.lnk"
if exist "%SHORTCUT%" (
    echo [Cleanup] Removing Startup shortcut...
    del "%SHORTCUT%"
    echo [Cleanup] Startup shortcut removed.
) else (
    echo [Cleanup] No Startup shortcut found.
)

:: Remove venv
if exist "venv" (
    echo [Cleanup] Removing virtual environment (this may take a moment)...
    rmdir /s /q "venv"
    echo [Cleanup] Virtual environment removed.
) else (
    echo [Cleanup] No virtual environment found.
)

:: Remove logs
if exist "logs" (
    echo [Cleanup] Removing logs...
    rmdir /s /q "logs"
    echo [Cleanup] Logs removed.
) else (
    echo [Cleanup] No logs found.
)

:: Remove temp folder (TTS audio cache)
if exist "temp" (
    echo [Cleanup] Removing temporary audio files...
    rmdir /s /q "temp"
    echo [Cleanup] Temp folder removed.
) else (
    echo [Cleanup] No temp folder found.
)

:: Remove settings (but keep example)
if exist "config\settings.json" (
    echo [Cleanup] Removing settings...
    del "config\settings.json"
    echo [Cleanup] Settings removed.
) else (
    echo [Cleanup] No settings found.
)

:: Remove Python cache directories
echo [Cleanup] Removing Python cache folders...
for /d /r %%d in (__pycache__) do (
    if exist "%%d" (
        rmdir /s /q "%%d" 2>nul
    )
)
if exist ".pytest_cache" rmdir /s /q ".pytest_cache"
echo [Cleanup] Cache folders removed.

:: Remove build artifacts if they exist
if exist "build" (
    echo [Cleanup] Removing build folder...
    rmdir /s /q "build"
    echo [Cleanup] Build folder removed.
)
if exist "dist" (
    echo [Cleanup] Removing dist folder...
    rmdir /s /q "dist"
    echo [Cleanup] Dist folder removed.
)

echo.
echo ========================================
echo   Uninstall complete!
echo ========================================
echo.
echo Removed:
echo   - Virtual environment (venv)
echo   - Logs, temp files, and settings
echo   - Startup shortcuts
echo   - Python cache folders
echo   - Build artifacts (if any)
echo.
echo To reinstall, run Launch_Herald.bat again.
echo To fully delete, remove this entire folder.
echo.
pause
