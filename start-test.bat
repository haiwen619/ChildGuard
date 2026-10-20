@echo off
chcp 65001 >nul
title 海豆·童伴守护 - 启动测试
cd /d "%~dp0"

echo ========================================================
echo   海豆·童伴守护 (Haidou ChildGuard) - 启动测试
echo ========================================================
echo.
echo [1/3] 正在清理已运行的旧进程...
taskkill /f /im Haidou.ChildGuard.Wpf.exe /im Haidou.Watchdog.exe >nul 2>&1
timeout /t 1 /nobreak >nul 2>&1

echo [2/3] 正在编译构建最新代码...
dotnet build src/Haidou.ChildGuard.Wpf/Haidou.ChildGuard.Wpf.csproj -c Debug
if %ERRORLEVEL% NEQ 0 (
    echo.
    echo ❌ 编译构建失败，请检查上方错误输出。
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo [3/3] 正在启动程序并激活系统托盘与界面...
set "BIN_DIR=%~dp0src\Haidou.ChildGuard.Wpf\bin\Debug\net10.0-windows10.0.19041.0"
cd /d "%BIN_DIR%"
start "" "Haidou.ChildGuard.Wpf.exe"

echo.
echo ✅ 程序已成功启动！
echo    - 窗口已前置显示；
echo    - 随手最小化或关闭窗口将自动驻留在任务栏最右侧托盘；
echo    - 双击右下角翡翠守护星托盘图标可随时呼出控制面板。
echo.
timeout /t 3 >nul
exit /b 0
