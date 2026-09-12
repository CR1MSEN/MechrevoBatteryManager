@echo off
setlocal
net session >nul 2>&1
if not "%errorlevel%"=="0" (
  echo Please run this script as Administrator.
  pause
  exit /b 1
)
sc.exe stop MechrevoBatteryManager >nul 2>&1
sc.exe delete MechrevoBatteryManager >nul 2>&1
echo Service removed. Removing the entire installation directory...
start "" /b powershell.exe -NoProfile -WindowStyle Hidden -Command "$root='%~dp0'; Start-Sleep -Seconds 2; Remove-Item -LiteralPath $root -Recurse -Force"
exit /b 0
