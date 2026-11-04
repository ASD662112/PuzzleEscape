@echo off
rem PuzzleEscape one-click build: Windows player + installer
rem Double-click to run. To rebuild only the installer: BuildInstaller.cmd -SkipUnity
setlocal
cd /d "%~dp0"

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0BuildInstaller.ps1" %*

set EXITCODE=%ERRORLEVEL%
echo.
if not "%EXITCODE%"=="0" (
    echo BUILD FAILED, exit code %EXITCODE%
) else (
    echo BUILD SUCCEEDED
)
pause
exit /b %EXITCODE%
