@echo off
REM Herald Test Runner
REM Runs pytest with various options based on arguments

setlocal

REM Activate virtual environment if it exists
if exist "%~dp0venv\Scripts\activate.bat" (
    call "%~dp0venv\Scripts\activate.bat"
)

REM Parse arguments
set "TEST_TYPE=%~1"
set "EXTRA_ARGS=%~2 %~3 %~4 %~5"

echo.
echo ========================================
echo Herald Test Runner
echo ========================================
echo.

if "%TEST_TYPE%"=="" (
    echo Running all tests...
    pytest tests/ -v
    goto :end
)

if "%TEST_TYPE%"=="unit" (
    echo Running unit tests only...
    pytest tests/ -v -m unit
    goto :end
)

if "%TEST_TYPE%"=="synthetic" (
    echo Running synthetic tests...
    pytest tests/ -v -m synthetic
    goto :end
)

if "%TEST_TYPE%"=="integration" (
    echo Running integration tests...
    pytest tests/ -v -m integration
    goto :end
)

if "%TEST_TYPE%"=="fast" (
    echo Running fast tests (excluding slow)...
    pytest tests/ -v -m "not slow"
    goto :end
)

if "%TEST_TYPE%"=="slow" (
    echo Running slow tests...
    pytest tests/ -v -m slow
    goto :end
)

if "%TEST_TYPE%"=="coverage" (
    echo Running tests with coverage...
    pytest tests/ -v --cov=src --cov-report=term-missing
    goto :end
)

echo Unknown test type: %TEST_TYPE%
echo.
echo Usage: test_runner.bat [test_type]
echo.
echo Test types:
echo   (none)      - Run all tests
echo   unit        - Run unit tests only (fast, no network)
echo   synthetic   - Run synthetic TTS tests
echo   integration - Run integration tests
echo   fast        - Run all except slow tests
echo   slow        - Run only slow tests
echo   coverage    - Run with coverage report
echo.

:end
echo.
echo ========================================
echo Test run complete
echo ========================================
pause
