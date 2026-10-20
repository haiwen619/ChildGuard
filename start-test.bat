@echo off
title Haidou ChildGuard - Startup Test
cd /d "%~dp0"

echo ========================================================
echo   Haidou ChildGuard - Launching Test
echo ========================================================
echo.
echo [1/3] Terminating old processes...
taskkill /f /im Haidou.ChildGuard.Wpf.exe /im Haidou.Watchdog.exe >nul 2>&1
ping 127.0.0.1 -n 2 >nul

echo [2/3] Building latest Haidou.ChildGuard.Wpf (.NET 9)...
dotnet build "src\Haidou.ChildGuard.Wpf\Haidou.ChildGuard.Wpf.csproj" -c Debug
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo [ERROR] Build failed. Please check the error message above.
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [3/3] Starting Haidou.ChildGuard.Wpf.exe...
set "BIN_DIR=%~dp0src\Haidou.ChildGuard.Wpf\bin\Debug\net9.0-windows"
cd /d "%BIN_DIR%"
start "" "Haidou.ChildGuard.Wpf.exe"

echo.
echo [SUCCESS] ChildGuard has been launched successfully!
echo   - Window is displayed in foreground.
echo   - Minimize or close to keep running in system tray.
echo   - Double-click the green shield tray icon to restore.
echo.
ping 127.0.0.1 -n 4 >nul
exit /b 0

