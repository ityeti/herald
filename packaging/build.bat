@echo off
setlocal EnableDelayedExpansion

:: Herald Build Script
:: Builds a standalone executable using PyInstaller

echo ========================================
echo  Herald Build Script
echo ========================================
echo.

:: Navigate to project root
cd /d "%~dp0.."
set "PROJECT_ROOT=%CD%"
echo Project root: %PROJECT_ROOT%
echo.

:: Check for venv
if not exist "venv\Scripts\activate.bat" (
    echo ERROR: Virtual environment not found.
    echo Please run Launch_Herald.bat first to set up the environment.
    pause
    exit /b 1
)

:: Activate venv
echo Activating virtual environment...
call venv\Scripts\activate.bat

:: Check for PyInstaller
python -c "import PyInstaller" 2>nul
if errorlevel 1 (
    echo Installing PyInstaller...
    pip install pyinstaller
    if errorlevel 1 (
        echo ERROR: Failed to install PyInstaller.
        pause
        exit /b 1
    )
)

:: Clean previous build
echo.
echo Cleaning previous build...
if exist "build" rmdir /s /q "build"
if exist "dist" rmdir /s /q "dist"

:: Run PyInstaller
echo.
echo Building Herald...
echo.
pyinstaller packaging\herald.spec --noconfirm

if errorlevel 1 (
    echo.
    echo ========================================
    echo  BUILD FAILED
    echo ========================================
    pause
    exit /b 1
)

:: Create config and logs directories in dist
echo.
echo Setting up distribution folders...
mkdir "dist\Herald\config" 2>nul
mkdir "dist\Herald\logs" 2>nul

:: Copy settings example if it exists
if exist "config\settings.example.json" (
    copy "config\settings.example.json" "dist\Herald\config\" >nul
)

:: Create a launcher batch file for the dist folder
(
echo @echo off
echo :: Herald Launcher
echo :: Runs Herald.exe with admin privileges
echo.
echo cd /d "%%~dp0"
echo start "" "Herald.exe"
) > "dist\Herald\Launch_Herald.bat"

echo.
echo ========================================
echo  BUILD SUCCESSFUL
echo ========================================
echo.
echo Output: %PROJECT_ROOT%\dist\Herald\
echo.
echo Contents:
dir /b "dist\Herald\"
echo.
echo To run: dist\Herald\Herald.exe (or Launch_Herald.bat)
echo.
echo NOTE: The exe requests admin privileges automatically.
echo       First run may trigger Windows SmartScreen warning.
echo.
pause
