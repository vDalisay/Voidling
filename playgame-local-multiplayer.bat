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

echo.
echo Starting LAN host "%PLAYER_NAME%" on UDP %PORT% using save profile "%PROFILE%"...
echo.
call "%~dp0playgame.bat" "--voidling-lan-host" "--voidling-lan-name=%PLAYER_NAME%" "--voidling-dev-profile=%PROFILE%" "--voidling-lan-port=%PORT%"
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

echo.
echo Joining %HOST_IP%:%PORT% as "%PLAYER_NAME%" using save profile "%PROFILE%"...
echo.
call "%~dp0playgame.bat" "--voidling-lan-join=%HOST_IP%" "--voidling-lan-name=%PLAYER_NAME%" "--voidling-dev-profile=%PROFILE%" "--voidling-lan-port=%PORT%"
goto finish

:finish
set "GAME_EXIT=%ERRORLEVEL%"
if not "%GAME_EXIT%"=="0" (
    echo.
    echo [ERROR] Local multiplayer launch exited with code %GAME_EXIT%.
    pause
)
exit /b %GAME_EXIT%
