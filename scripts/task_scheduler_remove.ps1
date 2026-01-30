# Remove auto-start for Herald (Startup shortcut)
# Run this script as Administrator

#Requires -RunAsAdministrator

$ShortcutName = "Herald TTS.lnk"
$StartupFolder = [Environment]::GetFolderPath('Startup')
$ShortcutPath = Join-Path $StartupFolder $ShortcutName

$removedSomething = $false

# Remove Startup folder shortcut if it exists
if (Test-Path $ShortcutPath) {
    Remove-Item $ShortcutPath -Force
    Write-Host "Startup shortcut removed." -ForegroundColor Green
    $removedSomething = $true
}

if ($removedSomething) {
    Write-Host ""
    Write-Host "Auto-start has been disabled." -ForegroundColor Cyan
    Write-Host "Herald will no longer start automatically at logon."
} else {
    Write-Host "No auto-start configuration found." -ForegroundColor Yellow
    Write-Host "Auto-start was not enabled."
}
