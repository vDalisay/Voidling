@echo off
setlocal EnableExtensions

cd /d "%~dp0"

set "SKIP_BUILD=0"
if /I "%~1"=="--no-build" set "SKIP_BUILD=1"
if /I "%~1"=="-n" set "SKIP_BUILD=1"

if "%SKIP_BUILD%"=="0" (
    call "%~dp0build.bat"
    if errorlevel 1 exit /b 1
)

call :find_godot
if not defined GODOT_BIN (
    echo.
    echo ========================================
    echo   GODOT .NET NOT FOUND
    echo ========================================
    echo.
    echo Install Godot 4.6 .NET/Mono, or set GODOT_EXE to the full path.
    echo Example:
    echo   setx GODOT_EXE "C:\Godot\Godot_v4.6.1-stable_mono_win64.exe"
    echo.
    echo After using setx, open a new terminal before running this script again.
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

:find_godot
set "GODOT_BIN="

rem 1. Explicit environment variable is the most reliable option.
if defined GODOT_EXE (
    if exist "%GODOT_EXE%" (
        set "GODOT_BIN=%GODOT_EXE%"
        goto :eof
    )
)

rem 2. Try common command names on PATH.
for %%G in (godot.exe godot4.exe Godot_v4.6.1-stable_mono_win64.exe Godot_v4.6-stable_mono_win64.exe) do (
    for /f "delims=" %%P in ('where %%G 2^>nul') do (
        set "GODOT_BIN=%%P"
        goto :eof
    )
)

rem 3. Try common install folders. Wildcards allow patch-version changes.
for %%F in (
    "C:\Godot\Godot_v4.6*-stable_mono_win64.exe"
    "C:\Program Files\Godot\Godot_v4.6*-stable_mono_win64.exe"
    "%LOCALAPPDATA%\Programs\Godot\Godot_v4.6*-stable_mono_win64.exe"
    "%USERPROFILE%\Godot\Godot_v4.6*-stable_mono_win64.exe"
) do (
    for %%G in (%%F) do (
        if exist "%%~fG" (
            set "GODOT_BIN=%%~fG"
            goto :eof
        )
    )
)

goto :eof
