@echo off
setlocal
set "APP_DIR=%LOCALAPPDATA%\SuperVPN"

if not exist "%APP_DIR%\super_vpn.exe" (
  mkdir "%APP_DIR%" 2>nul
  tar -xf "%~dp0payload.zip" -C "%APP_DIR%"
)

start "" "%APP_DIR%\super_vpn.exe"
endlocal
