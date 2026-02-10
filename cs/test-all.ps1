<#
.SYNOPSIS
    Herald C# build and test pipeline.
.DESCRIPTION
    Builds the solution and runs test suites in order:
    Unit → Integration → Audio → Publish → Smoke test.
.PARAMETER Suite
    Which test suite(s) to run: all, unit, integration, audio, smoke.
    Default: all
.EXAMPLE
    .\cs\test-all.ps1
    .\cs\test-all.ps1 -Suite unit
    .\cs\test-all.ps1 -Suite smoke
#>
param(
    [ValidateSet("all", "unit", "integration", "audio", "smoke")]
    [string]$Suite = "all"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$solutionDir = $scriptDir
$slnPath = Join-Path $solutionDir "Herald.sln"
$testProj = Join-Path $solutionDir "Herald.Tests\Herald.Tests.csproj"
$appProj = Join-Path $solutionDir "Herald\Herald.csproj"
$failures = 0

function Write-Step($msg) {
    Write-Host "`n=== $msg ===" -ForegroundColor Cyan
}

function Write-Pass($msg) {
    Write-Host "  PASS: $msg" -ForegroundColor Green
}

function Write-Fail($msg) {
    Write-Host "  FAIL: $msg" -ForegroundColor Red
    $script:failures++
}

# --- Build ---
Write-Step "Building solution"
dotnet build $slnPath --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Fail "Build failed"
    exit 1
}
Write-Pass "Build succeeded"

# --- Unit Tests ---
if ($Suite -eq "all" -or $Suite -eq "unit") {
    Write-Step "Running Unit tests"
    dotnet test $testProj --nologo --filter "Category=Unit" -v q
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "Unit tests failed"
    } else {
        Write-Pass "Unit tests passed"
    }
}

# --- Integration Tests ---
if ($Suite -eq "all" -or $Suite -eq "integration") {
    Write-Step "Running Integration tests"
    dotnet test $testProj --nologo --filter "Category=Integration" -v q
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "Integration tests failed"
    } else {
        Write-Pass "Integration tests passed"
    }
}

# --- Audio Tests ---
if ($Suite -eq "all" -or $Suite -eq "audio") {
    Write-Step "Running Audio verification tests"
    dotnet test $testProj --nologo --filter "Category=Audio" -v q
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "Audio tests failed"
    } else {
        Write-Pass "Audio tests passed"
    }
}

# --- Smoke Test ---
if ($Suite -eq "all" -or $Suite -eq "smoke") {
    Write-Step "Publishing release binary"
    dotnet publish $appProj -c Release -r win-x64 --self-contained --nologo -v q
    if ($LASTEXITCODE -ne 0) {
        Write-Fail "Publish failed"
    } else {
        Write-Pass "Publish succeeded"

        $publishDir = Join-Path $solutionDir "Herald\bin\Release\net8.0-windows\win-x64\publish"
        $exe = Join-Path $publishDir "Herald.exe"

        Write-Step "Running smoke test on published binary"
        & $exe --test-audio
        if ($LASTEXITCODE -ne 0) {
            Write-Fail "Smoke test failed (exit code $LASTEXITCODE)"
        } else {
            Write-Pass "Smoke test passed"
        }
    }
}

# --- Summary ---
Write-Host ""
if ($failures -eq 0) {
    Write-Host "ALL PASSED" -ForegroundColor Green
    exit 0
} else {
    Write-Host "$failures FAILURE(S)" -ForegroundColor Red
    exit 1
}
