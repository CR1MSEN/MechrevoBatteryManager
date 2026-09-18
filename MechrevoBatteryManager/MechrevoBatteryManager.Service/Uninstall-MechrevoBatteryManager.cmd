@echo off
setlocal
net session >nul 2>&1
if not "%errorlevel%"=="0" (
  echo Please run this script as Administrator.
  pause
  exit /b 1
)
sc.exe query MechrevoBatteryManager >nul 2>&1
set "serviceResult=%errorlevel%"
if "%serviceResult%"=="1060" goto missing
if not "%serviceResult%"=="0" goto failed
sc.exe stop MechrevoBatteryManager >nul 2>&1
set "serviceResult=%errorlevel%"
if "%serviceResult%"=="1060" goto missing
if not "%serviceResult%"=="0" if not "%serviceResult%"=="1062" goto failed
sc.exe delete MechrevoBatteryManager >nul 2>&1
set "serviceResult=%errorlevel%"
if "%serviceResult%"=="1060" goto missing
if not "%serviceResult%"=="0" goto failed
echo Service removed. Removing the entire installation directory...
start "" /b powershell.exe -NoProfile -WindowStyle Hidden -Command "$root='C:\Program Files\OEM\BatteryManager'; Start-Sleep -Seconds 2; Remove-Item -LiteralPath $root -Recurse -Force"
exit /b 0
:missing
echo Service does not exist; no uninstall is needed.
pause
exit /b 0
:failed
echo Service operation failed. Error code: %serviceResult%. Installation files were not removed.
pause
exit /b %serviceResult%
