@echo off
:: 海文娜AI 专属数字签名证书安装工具
:: 用于消除 Windows "未知发布者" 与 SmartScreen 阻断警告
title 海文娜AI 证书信任安装器
cd /d "%~dp0"

echo ========================================================
echo   海文娜AI (HaiwenNa AI) - 证书信任安装工具
echo ========================================================
echo.

:: 检查管理员权限
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo [提示] 正在申请管理员权限以导入系统根证书...
    powershell -Command "Start-Process cmd -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
    exit /b
)

if not exist "%~dp0HaiwenNa-CodeSigning.cer" (
    echo [错误] 未在当前目录下找到 HaiwenNa-CodeSigning.cer 证书文件！
    pause
    exit /b 1
)

echo [1/2] 正在将证书导入系统受信任的根证书颁发机构 (Root)...
certutil -addstore -f "Root" "%~dp0HaiwenNa-CodeSigning.cer" >nul
if %errorLevel% equ 0 (
    echo  -> [成功] Root 根证书导入完毕
) else (
    echo  -> [警告] Root 导入返回非零代码
)

echo.
echo [2/2] 正在将证书导入受信任的发布者 (TrustedPublisher)...
certutil -addstore -f "TrustedPublisher" "%~dp0HaiwenNa-CodeSigning.cer" >nul
if %errorLevel% equ 0 (
    echo  -> [成功] TrustedPublisher 证书导入完毕
) else (
    echo  -> [警告] TrustedPublisher 导入返回非零代码
)

echo.
echo ========================================================
echo   [恭喜] 海文娜AI 证书已成功安装至此计算机！
echo   现在运行童伴守护 (ChildGuard) 安装包或主程序：
echo   - 发行商将正式显示为：“海文娜AI”
echo   - 彻底消除“未知发布者”与红色安全警告
echo ========================================================
echo.
pause
exit /b 0