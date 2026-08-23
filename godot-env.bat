@echo off

rem Shared Godot/.NET discovery for every Voidling launcher.
rem Explicit environment variables always win when they point to valid tools.

if defined GODOT_EXE if exist "%GODOT_EXE%" goto :godot_found
set "GODOT_EXE="

if exist "%USERPROFILE%\Documents\Godot\Godot_v4.6.1-stable_mono_win64.exe" set "GODOT_EXE=%USERPROFILE%\Documents\Godot\Godot_v4.6.1-stable_mono_win64.exe"
if not defined GODOT_EXE if exist "%~dp0..\Godot\Godot_v4.6.1-stable_mono_win64.exe" set "GODOT_EXE=%~dp0..\Godot\Godot_v4.6.1-stable_mono_win64.exe"
if not defined GODOT_EXE if exist "%USERPROFILE%\Downloads\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64.exe" set "GODOT_EXE=%USERPROFILE%\Downloads\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64.exe"
if not defined GODOT_EXE if exist "%USERPROFILE%\Downloads\Godot_v4.6.1-stable_mono_win64.exe" set "GODOT_EXE=%USERPROFILE%\Downloads\Godot_v4.6.1-stable_mono_win64.exe"
if not defined GODOT_EXE for /f "delims=" %%G in ('where Godot_v4.6.1-stable_mono_win64.exe 2^>nul') do if not defined GODOT_EXE set "GODOT_EXE=%%G"

if not defined GODOT_EXE (
    echo [ERROR] Godot 4.6.1 .NET could not be found.
    echo Checked Documents\Godot, the extracted Downloads folder, and PATH.
    echo Set GODOT_EXE to override automatic discovery.
    exit /b 1
)

:godot_found
for %%G in ("%GODOT_EXE%") do set "GODOT_DIR=%%~dpG"
set "GODOT_NUGET_SOURCE=%GODOT_DIR%GodotSharp\Tools\nupkgs"

if not exist "%GODOT_NUGET_SOURCE%\Godot.NET.Sdk.4.6.1.nupkg" (
    echo [ERROR] The selected Godot executable is not the .NET/Mono edition:
    echo         %GODOT_EXE%
    exit /b 1
)

set "DOTNET_EXE="
if defined DOTNET_ROOT call :consider_dotnet "%DOTNET_ROOT%\dotnet.exe"
call :consider_dotnet "%GODOT_DIR%dotnet\dotnet.exe"
call :consider_dotnet "%USERPROFILE%\Documents\Godot\dotnet\dotnet.exe"
call :consider_dotnet "%USERPROFILE%\.dotnet\dotnet.exe"
for /f "delims=" %%D in ('where dotnet 2^>nul') do call :consider_dotnet "%%D"

if not defined DOTNET_EXE (
    echo [ERROR] A .NET 8 SDK could not be found.
    echo Install the .NET 8 SDK or set DOTNET_ROOT to its installation folder.
    exit /b 1
)

for %%D in ("%DOTNET_EXE%") do set "DOTNET_ROOT=%%~dpD"
set "PATH=%DOTNET_ROOT%;%PATH%"
exit /b 0

:consider_dotnet
if defined DOTNET_EXE exit /b 0
if not exist "%~1" exit /b 0
set "VOIDLING_SDK_FOUND="
for /f "delims=" %%S in ('"%~1" --list-sdks 2^>nul') do if not defined VOIDLING_SDK_FOUND set "VOIDLING_SDK_FOUND=%%S"
if defined VOIDLING_SDK_FOUND set "DOTNET_EXE=%~1"
set "VOIDLING_SDK_FOUND="
exit /b 0
