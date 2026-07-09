@echo off
setlocal enableextensions
echo [*] Unregistering user screensaver...

reg add "HKCU\Control Panel\Desktop" /v ScreenSaveActive /t REG_SZ /d 0 /f >nul
reg delete "HKCU\Control Panel\Desktop" /v SCRNSAVE.EXE /f >nul 2>nul

echo [+] Disabled. ScreenSaveActive=0.
pause
