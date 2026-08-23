@echo off
setlocal EnableExtensions

cd /d "%~dp0"

call "%~dp0godot-env.bat"
if errorlevel 1 (
    pause
    exit /b 1
)

set "DOTNET_CLI_HOME=%~dp0"
set "DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "APPDATA=%~dp0.appdata"
set "NUGET_PACKAGES=%~dp0.nuget\packages"
set "NUGET_HTTP_CACHE_PATH=%~dp0.nuget\http-cache"

echo ========================================
echo   Voidling - Build
echo ========================================
echo Godot: %GODOT_EXE%
echo .NET:  %DOTNET_EXE%
echo.

if not exist "Voidling.csproj" (
    echo [ERROR] Voidling.csproj was not found in:
    echo         %CD%
    pause
    exit /b 1
)

echo [1/2] Restoring packages...
"%DOTNET_EXE%" restore "Voidling.csproj" --configfile "%~dp0NuGet.Config"
if errorlevel 1 goto :failed

echo.
echo [2/2] Building Debug configuration...
"%DOTNET_EXE%" build "Voidling.csproj" --configuration Debug --no-restore
if errorlevel 1 goto :failed

echo.
echo ========================================
echo   BUILD SUCCEEDED
echo ========================================
exit /b 0

:failed
echo.
echo ========================================
echo   BUILD FAILED
echo ========================================
pause
exit /b 1
