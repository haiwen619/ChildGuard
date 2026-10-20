param(
    [string]$Version = "1.0.5"
)

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  Haidou ChildGuard - Building Release v$Version" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

# 1. 杀除可能残留的旧进程
Write-Host "[1/5] Terminating old processes..." -ForegroundColor Yellow
Stop-Process -Name "Haidou.ChildGuard.Wpf", "Haidou.Watchdog" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

# 2. 清理与创建输出目录
$publishDir = "$PSScriptRoot\publish\ChildGuard-v$Version-win-x64"
$portableZip = "$PSScriptRoot\publish\ChildGuard-v$Version-win-x64-Portable.zip"
$setupExe = "$PSScriptRoot\publish\ChildGuard-v$Version-Setup.exe"
$notesFile = "$PSScriptRoot\publish\release-notes.md"

if (Test-Path $publishDir) {
    Remove-Item -Recurse -Force $publishDir -ErrorAction SilentlyContinue
}
New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

# 3. 执行 dotnet publish (Self-Contained 独立运行，免装任何.NET环境)
Write-Host "[2/5] Publishing Self-Contained Release (win-x64)..." -ForegroundColor Yellow
dotnet publish "$PSScriptRoot\src\Haidou.ChildGuard.Wpf\Haidou.ChildGuard.Wpf.csproj" -c Release -r win-x64 --self-contained true -o $publishDir
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Dotnet publish failed." -ForegroundColor Red
    exit 1
}

# 4. 生成便携版压缩包 (100% 微软官方 Self-Contained 纯净打包，杜绝操作系统外来 DLL 污染)
Write-Host "[3/5] Creating Portable ZIP package..." -ForegroundColor Yellow
if (Test-Path $portableZip) { Remove-Item -Force $portableZip }
Compress-Archive -Path "$publishDir\*" -DestinationPath $portableZip -Force

# 5. 生成 Inno Setup 安装包
Write-Host "[4/5] Compiling Inno Setup Installer..." -ForegroundColor Yellow
$isccCmd = Get-Command iscc -ErrorAction SilentlyContinue
if ($isccCmd) {
    $isccPath = $isccCmd.Source
} elseif (Test-Path "C:\Program Files (x86)\Inno Setup 6\ISCC.exe") {
    $isccPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
} elseif (Test-Path "C:\Program Files\Inno Setup 6\ISCC.exe") {
    $isccPath = "C:\Program Files\Inno Setup 6\ISCC.exe"
} else {
    $isccPath = $null
}

if ($isccPath -and (Test-Path $isccPath)) {
    & $isccPath "/DMyAppVersion=$Version" "/DMyAppSourceDir=$publishDir" "/DMyOutputBaseFilename=ChildGuard-v$Version-Setup" "$PSScriptRoot\installer\ChildGuard.iss"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[WARN] Inno Setup compilation failed or returned non-zero." -ForegroundColor Yellow
    }
} else {
    Write-Host "[SKIP] Inno Setup compiler not found in system." -ForegroundColor Yellow
}

# 6. 生成发布说明 Release Notes
$notesLines = @(
    "# 🌟 童伴守护 (ChildGuard / 守护星) v$Version 纯净兼容发布",
    "",
    "> 100% 纯本地、无商业广告、零隐私上云的开源公益级 Windows 青少年数字健康与终端监护系统。",
    "",
    "---",
    "",
    "### 🚀 v$Version 关键修复与兼容性加固",
    "",
    "- **彻底根除 0xc0000602 (KERNELBASE FailFast) 崩溃**：",
    "  - **移除非托管构建机 DLL 污染**：发布包恢复微软官方纯净 Self-Contained 模式，去除从高版本 CI 系统（Windows Server 2022）强行拷贝的 System32 vcruntime DLL，杜绝与旧版 Windows 10 系统底层符号冲突；",
    "  - **移除 WinRT 强绑定**：屏幕审计与前台识别彻底解除对 Windows 10 UWP/WinRT 命名空间的依赖，采用标准 Win32/托管实现，完全消除在无包身份（Unpackaged）桌面程序上的 WinRT 激活异常；",
    "  - **标准 net9.0-windows 统一**：所有工程统一采用标准 .NET 9 桌面框架，消除 Windows SDK 10.0.19041 框架约束，全面无缝兼容 Windows 10 2004 (19041) 及以上所有版本以及 Windows 11；",
    "  - **Gamma 内存安全与视效优雅降级**：护眼滤镜采用连续非托管内存，Win10 自动回退为深色稳定视觉，杜绝 DWM 崩溃。",
    "",
    "---",
    "",
    "### 📦 下载推荐 (Downloads)",
    "",
    "- **Windows 官方安装包（推荐广大用户）**：ChildGuard-v$Version-Setup.exe",
    "  - 标准 Windows 安装向导，自动创建桌面与开始菜单快捷方式，支持 Windows 控制面板一键卸载。",
    "  - 内置独立完整运行时，无需预先安装任何 .NET 环境。",
    "",
    "- **绿色便携版（免安装）**：ChildGuard-v$Version-win-x64-Portable.zip",
    "  - 解压即用，适合喜欢便携绿色的用户或放入 U 盘使用。",
    "",
    "---",
    "",
    "### 🔐 校验和信息 (SHA256)",
    "",
    "- 见下方构建生成清单"
)
$notesLines | Set-Content -Path $notesFile -Encoding UTF8

# 7. 计算 SHA256 与文件信息
Write-Host ""
Write-Host "[5/5] Release Packages Summary:" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Cyan
Get-Item $setupExe, $portableZip -ErrorAction SilentlyContinue | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
    $sizeMb = [math]::Round($_.Length / 1MB, 2)
    Write-Host "File  : $($_.Name) ($sizeMb MB)" -ForegroundColor White
    Write-Host "SHA256: $hash" -ForegroundColor Gray
    Write-Host ""
}
Write-Host "[SUCCESS] Release build completed!" -ForegroundColor Green