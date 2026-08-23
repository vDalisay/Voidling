@echo off
setlocal EnableExtensions

cd /d "%~dp0"

call "%~dp0godot-env.bat"
if errorlevel 1 (
    pause
    exit /b 1
)

set "SKIP_BUILD=0"
if /I "%~1"=="--no-build" set "SKIP_BUILD=1"
if /I "%~1"=="-n" set "SKIP_BUILD=1"

if "%SKIP_BUILD%"=="0" (
    call "%~dp0build.bat"
    if errorlevel 1 exit /b 1
)

echo.
echo ========================================
echo   Voidling - Play
echo ========================================
echo Godot: %GODOT_EXE%
echo .NET:  %DOTNET_EXE%
echo.

"%GODOT_EXE%" --path "%CD%"
set "GAME_EXIT=%ERRORLEVEL%"

if not "%GAME_EXIT%"=="0" (
    echo.
    echo [ERROR] Godot exited with code %GAME_EXIT%.
    pause
)

exit /b %GAME_EXIT%
