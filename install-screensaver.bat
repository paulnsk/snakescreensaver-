@echo off
setlocal enableextensions
title SnakeScreensaver installer

REM ============================================================
REM  Single-form installer for SnakeScreensaver.
REM  Auto-elevates; publishes a self-contained single-file .exe;
REM  copies SnakeScreensaver.scr to System32; registers it as
REM  the active Windows screensaver.
REM ============================================================

REM ---------- Check for admin rights ----------
net session >nul 2>&1
if %errorlevel% neq 0 goto elevate
goto main

:elevate
echo [*] Need admin rights. Re-launching elevated via UAC...
powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
exit /b 0

:main
set "PROJ=%~dp0SnakeScreensaver\SnakeScreensaver.csproj"
if not exist "%PROJ%" set "PROJ=%~dp0SnakeScreensaver.csproj"
if not exist "%PROJ%" (
  echo [!] Project file not found near "%~dp0".
  pause
  exit /b 1
)
echo [*] Project: %PROJ%

set "PUBDIR=%~dp0publish"
if exist "%PUBDIR%" rmdir /s /q "%PUBDIR%"

echo [*] Publishing self-contained single-file (win-x64)...
dotnet publish "%PROJ%" -c Release -p:PublishMode=screensaver -o "%PUBDIR%" -nologo
if errorlevel 1 (
  echo [!] Publish failed.
  pause
  exit /b 1
)

set "EXE=%PUBDIR%\SnakeScreensaver.exe"
if not exist "%EXE%" (
  echo [!] Published exe not found at %EXE%
  pause
  exit /b 1
)
echo [*] Published: %EXE%

REM ---------- Install .scr to System32 ----------
set "TARGET=%SystemRoot%\System32\SnakeScreensaver.scr"
echo [*] Installing to System32: %TARGET%
copy /y "%EXE%" "%TARGET%" >nul
if errorlevel 1 (
  echo [!] Failed to copy to System32.
  pause
  exit /b 1
)

REM ---------- Register in registry ----------
echo [*] Registering (HKCU\Control Panel\Desktop^)...
reg add "HKCU\Control Panel\Desktop" /v SCRNSAVE.EXE /t REG_SZ /d "%TARGET%" /f >nul
if errorlevel 1 (
  echo [!] Registry write failed.
  pause
  exit /b 1
)
reg add "HKCU\Control Panel\Desktop" /v ScreenSaveActive /t REG_SZ /d 1 /f >nul
reg add "HKCU\Control Panel\Desktop" /v ScreenSaveTimeOut /t REG_SZ /d 60 /f >nul
reg add "HKCU\Control Panel\Desktop" /v ScreenSaverIsSecure /t REG_SZ /d 0 /f >nul

REM ---------- Disable Windows lock-screen so the black backdrop behind our ----------
REM ---------- transparent screensaver does NOT appear on auto activation.       ----------
echo [*] Disabling Windows lock-screen (HKLM\...Personalization\NoLockScreen^)...
reg add "HKLM\SOFTWARE\Policies\Microsoft\Windows\Personalization" /v NoLockScreen /t REG_DWORD /d 1 /f >nul
if errorlevel 1 (
  echo [!] Could not disable lock-screen (need admin^). Black backdrop may still appear on auto activation.
) else (
  echo [*] Lock-screen disabled. Screensaver should now show the live desktop behind it.
)

REM Refresh system parameters so the new screensaver is picked up immediately.
rundll32.exe user32.dll,UpdatePerUserSystemParameters ,1 ,True >nul 2>&1

REM ---------- Verify ----------
echo.
echo [+] DONE
echo     Installed .scr : %TARGET%
echo     Timeout (sec)  : 60  (one minute; change in Control Panel)
echo     Lock-screen    : DISABLED (NoLockScreen=1^)
echo                       Win+L still LOCKS the session, just without the
echo                       fancy clock+wallpaper lock screen.
echo     Settings file  : %APPDATA%\SnakeScreensaver\settings.json
echo     Debug log      : %TEMP%\SnakeScreensaver.log
echo     Exit on        : mouse move (>=15px^), any click, any key, Esc always
echo.
echo     TEST immediately (no waiting for timeout):
echo       "%TARGET%" /s
echo.
echo     Re-run uninstall-screensaver.bat to restore the lock-screen.
echo.
pause
