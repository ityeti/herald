# Setup auto-start for Herald via Startup folder shortcut
# Run this script as Administrator
#
# Creates a shortcut in the Windows Startup folder that launches the app
# with a UAC prompt at each login for administrator privileges.

#Requires -RunAsAdministrator

$ShortcutName = "Herald TTS.lnk"
$ProjectPath = Split-Path -Parent $PSScriptRoot  # Go up from scripts/ to project root
$LauncherPath = Join-Path $ProjectPath "Launch_Herald.bat"
$StartupFolder = [Environment]::GetFolderPath('Startup')
$ShortcutPath = Join-Path $StartupFolder $ShortcutName

# Check if launcher exists
if (-not (Test-Path $LauncherPath)) {
    Write-Host "ERROR: Launch_Herald.bat not found." -ForegroundColor Red
    Write-Host "Expected location: $LauncherPath"
    exit 1
}

# Check if shortcut already exists
if (Test-Path $ShortcutPath) {
    Write-Host "Auto-start shortcut already exists." -ForegroundColor Yellow
    $response = Read-Host "Do you want to replace it? (y/n)"
    if ($response -ne 'y') {
        Write-Host "Cancelled." -ForegroundColor Red
        exit 0
    }
    Remove-Item $ShortcutPath -Force
    Write-Host "Existing shortcut removed." -ForegroundColor Green
}

# Create the shortcut pointing to the launcher bat file
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut($ShortcutPath)
$Shortcut.TargetPath = $LauncherPath
$Shortcut.WorkingDirectory = $ProjectPath
$Shortcut.Description = "Herald - Text-to-Speech Utility"
$Shortcut.Save()

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Auto-Start Setup Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Shortcut created in: $StartupFolder"
Write-Host "Target: $LauncherPath"
Write-Host ""
Write-Host "At each login, Windows will run the launcher which requests"
Write-Host "administrator privileges via UAC prompt."
Write-Host ""
Write-Host "To disable: Run Autostart_Disable.bat" -ForegroundColor Yellow
Write-Host "Or delete the shortcut from your Startup folder" -ForegroundColor Yellow
