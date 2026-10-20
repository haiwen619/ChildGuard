@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo [1/2] Terminating old processes if running...
taskkill /f /im Haidou.ChildGuard.Wpf.exe /im Haidou.Watchdog.exe >nul 2>&1
echo [2/2] Building latest Haidou.ChildGuard.Wpf and Haidou.Watchdog...
dotnet build src/Haidou.ChildGuard.Wpf/Haidou.ChildGuard.Wpf.csproj -c Debug
if %ERRORLEVEL% EQU 0 (
    echo Starting Haidou.ChildGuard.Wpf in its binary directory...
    cd /d "%~dp0src\Haidou.ChildGuard.Wpf\bin\Debug\net10.0-windows"
    start "" "Haidou.ChildGuard.Wpf.exe"
) else (
    echo Build failed.
    pause
)