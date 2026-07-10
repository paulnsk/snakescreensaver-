@echo off
setlocal enableextensions

set "MODE=user"
if /i "%~1"=="--system32" set "MODE=system32"

REM ===== Find the built exe (bat may be in project root, parent, or alongside) =====
set "EXE=%~dp0bin\Debug\net10.0-windows\SnakeScreensaver.exe"
if exist "%EXE%" goto found
set "EXE=%~dp0bin\Debug\net10.0\SnakeScreensaver.exe"
if exist "%EXE%" goto found
set "EXE=%~dp0SnakeScreensaver\bin\Debug\net10.0-windows\SnakeScreensaver.exe"
if exist "%EXE%" goto found
set "EXE=%~dp0SnakeScreensaver\bin\Debug\net10.0\SnakeScreensaver.exe"
if exist "%EXE%" goto found
set "EXE=%~dp0SnakeScreensaver.exe"
if exist "%EXE%" goto found

echo [!] Built exe not found.
echo     Run: dotnet build SnakeScreensaver\SnakeScreensaver.csproj
pause
exit /b 1

:found
echo [*] Found exe: %EXE%

REM ===== Build the local .scr copy (always; System32 install copies THAT to System32) =====
set "LOCAL_SCR=%EXE:~0,-4%.scr"
copy /y "%EXE%" "%LOCAL_SCR%" >nul
if errorlevel 1 (
  echo [!] Could not create local .scr copy.
  pause
  exit /b 1
)
echo [*] Local .scr: %LOCAL_SCR%

REM ===== Decide target registration path =====
if /i "%MODE%"=="system32" goto system32mode
set "TARGET=%LOCAL_SCR%"
goto register

:system32mode
REM Check for admin rights
net session >nul 2>&1
if %errorlevel% neq 0 goto elevate

set "TARGET=%SystemRoot%\System32\SnakeScreensaver.scr"
echo [*] Copying to System32: %TARGET%
copy /y "%LOCAL_SCR%" "%TARGET%" >nul
if errorlevel 1 (
  echo [!] Failed to copy to System32.
  pause
  exit /b 1
)
goto register

:elevate
echo [*] Need admin rights. Re-launching elevated...
powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -ArgumentList '--system32' -Verb RunAs"
exit /b 0

:register
echo [*] Registering (HKCU\Control Panel\Desktop^): %TARGET%
reg add "HKCU\Control Panel\Desktop" /v SCRNSAVE.EXE /t REG_SZ /d "%TARGET%" /f >nul
if errorlevel 1 (
  echo [!] Registry write failed.
  pause
  exit /b 1
)
reg add "HKCU\Control Panel\Desktop" /v ScreenSaveActive /t REG_SZ /d 1 /f >nul
reg add "HKCU\Control Panel\Desktop" /v ScreenSaveTimeOut /t REG_SZ /d 300 /f >nul
reg add "HKCU\Control Panel\Desktop" /v ScreenSaverIsSecure /t REG_SZ /d 0 /f >nul

rundll32.exe user32.dll,UpdatePerUserSystemParameters ,1 ,True >nul 2>&1

echo.
echo [+] DONE. SCRNSAVE.EXE = %TARGET%
echo.
if /i "%MODE%"=="system32" (
  echo     Mode: SYSTEM32 (admin^). Should now appear in Control Panel dropdown.
) else (
  echo     Mode: USER-LEVEL (no admin^). Active screensaver, but Control Panel
  echo     dropdown will show "(None)" because Windows only lists .scr in System32.
  echo     To make it appear: re-run  register-screensaver.bat --system32
)
echo.
echo     Timeout: 300 sec (5 min^). Test immediately:
echo       "%TARGET%" /s
echo     Settings file: %%APPDATA%%\SnakeScreensaver\settings.json
echo.
pause
