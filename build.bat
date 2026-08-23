@echo off
setlocal EnableExtensions

cd /d "%~dp0"

echo ========================================
echo   Voidling - Build
echo ========================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [ERROR] .NET SDK was not found on PATH.
    echo Install the .NET 8 SDK, then try again.
    pause
    exit /b 1
)

if not exist "Voidling.csproj" (
    echo [ERROR] Voidling.csproj was not found in:
    echo         %CD%
    pause
    exit /b 1
)

echo [1/2] Restoring packages...
dotnet restore "Voidling.csproj"
if errorlevel 1 goto :failed

echo.
echo [2/2] Building Debug configuration...
dotnet build "Voidling.csproj" --configuration Debug --no-restore
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
