@echo off
setlocal EnableExtensions

cd /d "%~dp0"

set "GODOT_BIN=C:\Users\Home\Downloads\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64.exe"

set "SKIP_BUILD=0"
if /I "%~1"=="--no-build" set "SKIP_BUILD=1"
if /I "%~1"=="-n" set "SKIP_BUILD=1"

if "%SKIP_BUILD%"=="0" (
    call "%~dp0build.bat"
    if errorlevel 1 exit /b 1
)

if not exist "%GODOT_BIN%" (
    echo.
    echo ========================================
    echo   GODOT .NET NOT FOUND
    echo ========================================
    echo.
    echo Godot was not found at:
    echo   %GODOT_BIN%
    pause
    exit /b 1
)

echo.
echo ========================================
echo   Voidling - Play
echo ========================================
echo Godot: %GODOT_BIN%
echo.

"%GODOT_BIN%" --path "%CD%"
set "GAME_EXIT=%ERRORLEVEL%"

if not "%GAME_EXIT%"=="0" (
    echo.
    echo [ERROR] Godot exited with code %GAME_EXIT%.
    pause
)

exit /b %GAME_EXIT%
