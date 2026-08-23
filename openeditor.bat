@echo off
setlocal EnableExtensions

cd /d "%~dp0"

call "%~dp0godot-env.bat"
if errorlevel 1 (
    pause
    exit /b 1
)

echo Godot: %GODOT_EXE%
echo .NET:  %DOTNET_EXE%
"%GODOT_EXE%" --editor --path "%CD%"
