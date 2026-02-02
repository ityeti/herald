# Herald Release Build Script
# Builds the executable and generates SHA256 checksums for verification
#
# Usage: .\build_release.ps1 [-Version "0.2.1"]
#
# Output:
#   - dist\Herald\Herald.exe (the application)
#   - dist\Herald\checksums.sha256 (verification hashes)
#   - release_notes.md (copy-paste into GitHub release)

param(
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Herald Release Build" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Navigate to project root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir
Set-Location $ProjectRoot

Write-Host "Project root: $ProjectRoot"
Write-Host ""

# Get version from updater.py if not provided
if (-not $Version) {
    $UpdaterContent = Get-Content "src\updater.py" -Raw
    if ($UpdaterContent -match 'VERSION\s*=\s*"([^"]+)"') {
        $Version = $Matches[1]
        Write-Host "Detected version: $Version" -ForegroundColor Green
    } else {
        Write-Host "ERROR: Could not detect version. Please specify with -Version" -ForegroundColor Red
        exit 1
    }
}

Write-Host "Building Herald v$Version..." -ForegroundColor Yellow
Write-Host ""

# Run the existing build script
Write-Host "Running build.bat..." -ForegroundColor Yellow
$BuildResult = Start-Process -FilePath "cmd.exe" -ArgumentList "/c", "packaging\build.bat" -Wait -PassThru -NoNewWindow

if ($BuildResult.ExitCode -ne 0) {
    Write-Host "ERROR: Build failed!" -ForegroundColor Red
    exit 1
}

# Check if build succeeded
$DistDir = Join-Path $ProjectRoot "dist\Herald"
$ExePath = Join-Path $DistDir "Herald.exe"

if (-not (Test-Path $ExePath)) {
    Write-Host "ERROR: Herald.exe not found at $ExePath" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Build successful. Generating checksums..." -ForegroundColor Green
Write-Host ""

# Generate SHA256 checksums
$ChecksumFile = Join-Path $DistDir "checksums.sha256"
$Checksums = @()

# Hash the main executable
$ExeHash = (Get-FileHash -Path $ExePath -Algorithm SHA256).Hash
$Checksums += "$ExeHash  Herald.exe"
Write-Host "Herald.exe: $ExeHash" -ForegroundColor Gray

# Hash any DLLs in the dist folder
Get-ChildItem -Path $DistDir -Filter "*.dll" | ForEach-Object {
    $Hash = (Get-FileHash -Path $_.FullName -Algorithm SHA256).Hash
    $Checksums += "$Hash  $($_.Name)"
}

# Write checksums file
$Checksums | Out-File -FilePath $ChecksumFile -Encoding UTF8
Write-Host ""
Write-Host "Checksums written to: $ChecksumFile" -ForegroundColor Green

# Calculate total size
$TotalSize = (Get-ChildItem -Path $DistDir -Recurse | Measure-Object -Property Length -Sum).Sum
$SizeMB = [math]::Round($TotalSize / 1MB, 1)

# Create zip archive for release
$ZipName = "Herald-v$Version-win64.zip"
$ZipPath = Join-Path $ProjectRoot "dist\$ZipName"
if (Test-Path $ZipPath) { Remove-Item $ZipPath }

Write-Host ""
Write-Host "Creating release archive: $ZipName..." -ForegroundColor Yellow
Compress-Archive -Path "$DistDir\*" -DestinationPath $ZipPath
$ZipHash = (Get-FileHash -Path $ZipPath -Algorithm SHA256).Hash
Write-Host "Archive hash: $ZipHash" -ForegroundColor Gray

# Generate release notes markdown
$ReleaseNotesPath = Join-Path $ProjectRoot "dist\release_notes.md"
$ReleaseNotes = @"
## Herald v$Version

### Downloads

| File | SHA256 |
|------|--------|
| $ZipName | ``$ZipHash`` |

### Verification

To verify your download:
``````powershell
# PowerShell
(Get-FileHash -Path "$ZipName" -Algorithm SHA256).Hash
``````

``````bash
# Linux/macOS/Git Bash
sha256sum "$ZipName"
``````

Expected hash: ``$ZipHash``

### Included Files

- Herald.exe - Main application ($SizeMB MB total)
- checksums.sha256 - SHA256 hashes for all files
- Launch_Herald.bat - Launcher script

### Installation

1. Download and extract the zip file
2. Run ``Herald.exe`` or ``Launch_Herald.bat``
3. The app will appear in your system tray

### Notes

- Requires administrator privileges for global hotkeys
- Windows SmartScreen may warn about unrecognized publisher (click "More info" → "Run anyway")
"@

$ReleaseNotes | Out-File -FilePath $ReleaseNotesPath -Encoding UTF8

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  RELEASE BUILD COMPLETE" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Outputs:" -ForegroundColor Green
Write-Host "  - $ZipPath"
Write-Host "  - $ChecksumFile"
Write-Host "  - $ReleaseNotesPath"
Write-Host ""
Write-Host "ZIP SHA256: $ZipHash" -ForegroundColor Yellow
Write-Host ""
Write-Host "Copy the contents of release_notes.md into your GitHub release."
Write-Host ""
