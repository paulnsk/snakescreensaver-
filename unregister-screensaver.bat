@echo off
setlocal enableextensions
title SnakeScreensaver uninstaller

net session >nul 2>&1
if %errorlevel% neq 0 goto elevate
goto main

:elevate
echo [*] Need admin rights. Re-launching elevated via UAC...
powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
exit /b 0

:main
echo [*] Disabling screensaver in registry...
reg add "HKCU\Control Panel\Desktop" /v ScreenSaveActive /t REG_SZ /d 0 /f >nul
reg delete "HKCU\Control Panel\Desktop" /v SCRNSAVE.EXE /f >nul 2>&1

echo [*] Removing SnakeScreensaver.scr from System32...
if exist "%SystemRoot%\System32\SnakeScreensaver.scr" del /f /q "%SystemRoot%\System32\SnakeScreensaver.scr"

echo [*] Restoring Windows lock-screen (removing NoLockScreen policy^)...
reg delete "HKLM\SOFTWARE\Policies\Microsoft\Windows\Personalization" /v NoLockScreen /f >nul 2>&1
rundll32.exe user32.dll,UpdatePerUserSystemParameters ,1 ,True >nul 2>&1

echo [+] DONE. Screensaver disabled, lock-screen restored, .scr removed.
echo     Settings file left intact: %APPDATA%\SnakeScreensaver\settings.json
pause
