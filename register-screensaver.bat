@echo off
setlocal enableextensions
set "EXE=%~dp0SnakeScreensaver\bin\Debug\net10.0-windows\SnakeScreensaver.exe"
if not exist "%EXE%" (
  set "EXE=%~dp0SnakeScreensaver\bin\Debug\net10.0\SnakeScreensaver.exe"
)
if not exist "%EXE%" (
  echo [!] Built exe not found. Run: dotnet build SnakeScreensaver\SnakeScreensaver.csproj
  pause
  exit /b 1
)

echo [*] Registering: %EXE%

reg add "HKCU\Control Panel\Desktop" /v SCRNSAVE.EXE /t REG_SZ /d "%EXE%" /f >nul
if errorlevel 1 (
  echo [!] Failed to write registry (need elevation rights?)
  pause
  exit /b 1
)
reg add "HKCU\Control Panel\Desktop" /v ScreenSaveActive /t REG_SZ /d 1 /f >nul
reg add "HKCU\Control Panel\Desktop" /v ScreenSaveTimeOut /t REG_SZ /d 300 /f >nul
reg add "HKCU\Control Panel\Desktop" /v ScreenSaverIsSecure /t REG_SZ /d 0 /f >nul

echo [+] Registered as user screensaver.
echo     Timeout: 300 sec (5 min). Change in Personalization ^> Screen saver.
echo     Exit on: any mouse move, any key, any click (Esc always works).
echo     Settings file: %%APPDATA%%\SnakeScreensaver\settings.json
echo.
echo To test now, run:
echo     "%EXE%" /s
pause
