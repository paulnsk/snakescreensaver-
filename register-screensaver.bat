@echo off
setlocal enableextensions

REM Find the built exe (bat may be in project root, in parent, or alongside exe)
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
echo     Then run this bat again from the project folder or one above it.
pause
exit /b 1

:found
echo [*] Found exe: %EXE%

REM Create a .scr copy next to the exe. Windows screensaver convention: compiled PE with .scr.
set "SCR=%EXE:~0,-4%.scr"
copy /y "%EXE%" "%SCR%" >nul
if errorlevel 1 (
  echo [!] Could not create .scr copy. Registering .exe instead.
  set "TARGET=%EXE%"
) else (
  echo [*] Created .scr copy: %SCR%
  set "TARGET=%SCR%"
)

REM Register the .scr as the active user screensaver.
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

REM Try to refresh the system parameter so settings take effect immediately.
rundll32.exe user32.dll,UpdatePerUserSystemParameters ,1 ,True >nul 2>&1

echo.
echo [+] DONE. Registry value SCRNSAVE.EXE = %TARGET%
echo.
echo     NOTE on Control Panel dropdown:
echo       The "Screen Saver Settings" dropdown in Windows only lists .scr
echo       files located in %SystemRoot%\System32. To make the snake appear
echo       in that dropdown, run this bat AS ADMINISTRATOR with the
echo       --system32 switch (copy .scr to System32).
echo       Otherwise: the screensaver is STILL ACTIVE. It will launch after
echo       the timeout (default 5 min). Test now with:
echo         "%TARGET%" /s
echo     Settings file: %%APPDATA%%\SnakeScreensaver\settings.json
echo.
pause
