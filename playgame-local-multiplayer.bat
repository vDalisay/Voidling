@echo off
setlocal EnableExtensions

cd /d "%~dp0"

echo.
echo ========================================
echo   Voidling - Local Multiplayer
echo ========================================
echo Development-only ENet/LAN launcher.
echo.
echo IMPORTANT: If you run two instances on one PC, give each instance a DIFFERENT save profile.
echo.
echo [1] Host a local multiplayer session
echo [2] Join a local multiplayer session
echo.
set "MODE="
set /p "MODE=Choose mode [1/2]: "

if "%MODE%"=="1" goto host
if "%MODE%"=="2" goto join

echo.
echo [ERROR] Invalid mode. Choose 1 for Host or 2 for Join.
pause
exit /b 1

:host
set "PLAYER_NAME=Host"
set /p "PLAYER_NAME=Player name [Host]: "
if not defined PLAYER_NAME set "PLAYER_NAME=Host"

set "PROFILE=Host"
set /p "PROFILE=Development save profile [Host]: "
if not defined PROFILE set "PROFILE=Host"

set "PORT=27181"
set /p "PORT=UDP port [27181]: "
if not defined PORT set "PORT=27181"

rem ENet's CantCreate error on Windows is most commonly a local UDP bind failure.
rem Perform a real IPv4 bind test before starting Godot. If the requested port is
rem occupied or excluded by Windows, use the next bindable port and print it clearly
rem so the joining laptop can use the same value.
set "REQUESTED_PORT=%PORT%"
set "AVAILABLE_PORT="
for /f "usebackq delims=" %%P in (`powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\find-free-udp-port.ps1" -StartPort %PORT% 2^>nul`) do set "AVAILABLE_PORT=%%P"

if not defined AVAILABLE_PORT (
    echo.
    echo [ERROR] Windows could not bind UDP %PORT% or any of the next 20 ports.
    echo Close stale Godot/Voidling processes, or rerun this launcher and choose another port.
    echo You can inspect UDP %PORT% with:
    echo   netstat -ano -p udp ^| findstr :%PORT%
    pause
    exit /b 1
)

if not "%AVAILABLE_PORT%"=="%PORT%" (
    echo.
    echo [WARN] UDP %PORT% is currently occupied, reserved, or otherwise unavailable.
    echo [INFO] Using the next bindable UDP port instead: %AVAILABLE_PORT%
    set "PORT=%AVAILABLE_PORT%"
)

set "BUILD_CHOICE=Y"
set /p "BUILD_CHOICE=Build before launch? [Y/n]: "
set "BUILD_FLAG="
if /I "%BUILD_CHOICE%"=="N" set "BUILD_FLAG=--no-build"

echo.
echo Starting LAN host "%PLAYER_NAME%" on UDP %PORT% using save profile "%PROFILE%"...
echo.
echo ========================================
echo   JOIN SETTINGS FOR THE OTHER LAPTOP
echo ========================================
echo   UDP port: %PORT%
echo   Host IP:  use this laptop's LAN IPv4 address from ipconfig
if not "%REQUESTED_PORT%"=="%PORT%" echo   NOTE: requested %REQUESTED_PORT%, automatically switched to %PORT%
echo ========================================
echo.
call "%~dp0playgame.bat" %BUILD_FLAG% "--voidling-lan-host" "--voidling-lan-name=%PLAYER_NAME%" "--voidling-dev-profile=%PROFILE%" "--voidling-lan-port=%PORT%"
goto finish

:join
set "PLAYER_NAME=Client"
set /p "PLAYER_NAME=Player name [Client]: "
if not defined PLAYER_NAME set "PLAYER_NAME=Client"

set "PROFILE=Client"
set /p "PROFILE=Development save profile [Client]: "
if not defined PROFILE set "PROFILE=Client"

set "HOST_IP=127.0.0.1"
set /p "HOST_IP=Host address [127.0.0.1]: "
if not defined HOST_IP set "HOST_IP=127.0.0.1"

set "PORT=27181"
set /p "PORT=UDP port [27181]: "
if not defined PORT set "PORT=27181"

set "BUILD_CHOICE=N"
set /p "BUILD_CHOICE=Build before launch? [y/N]: "
set "BUILD_FLAG=--no-build"
if /I "%BUILD_CHOICE%"=="Y" set "BUILD_FLAG="

echo.
echo Joining %HOST_IP%:%PORT% as "%PLAYER_NAME%" using save profile "%PROFILE%"...
echo.
call "%~dp0playgame.bat" %BUILD_FLAG% "--voidling-lan-join=%HOST_IP%" "--voidling-lan-name=%PLAYER_NAME%" "--voidling-dev-profile=%PROFILE%" "--voidling-lan-port=%PORT%"
goto finish

:finish
set "GAME_EXIT=%ERRORLEVEL%"
if not "%GAME_EXIT%"=="0" (
    echo.
    echo [ERROR] Local multiplayer launch exited with code %GAME_EXIT%.
    pause
)
exit /b %GAME_EXIT%
