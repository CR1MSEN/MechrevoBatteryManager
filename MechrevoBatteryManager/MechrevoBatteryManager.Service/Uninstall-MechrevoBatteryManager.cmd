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
echo Service removed. Cleaning the installation directory...
start "" /b cmd.exe /c "timeout /t 2 /nobreak >nul & for /f \"delims=\" %%F in ('dir /b /a-d \"%~dp0\"') do del /f /q \"%~dp0%%F\" >nul 2>&1 & for /f \"delims=\" %%D in ('dir /b /ad \"%~dp0\" ^| findstr /v /i \"^log$\"') do rmdir /s /q \"%~dp0%%D\" >nul 2>&1"
exit /b 0
