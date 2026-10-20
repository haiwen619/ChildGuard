param(
    [string]$Version = "1.0.0"
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

# 4. 生成便携版压缩包
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
    & $isccPath "$PSScriptRoot\installer\ChildGuard.iss"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[WARN] Inno Setup compilation failed or returned non-zero." -ForegroundColor Yellow
    }
} else {
    Write-Host "[SKIP] Inno Setup compiler not found in system." -ForegroundColor Yellow
}

# 6. 生成发布说明 Release Notes
$notesContent = @"
# 🌟 童伴守护 (ChildGuard / 守护星) v$Version 正式版发布

> 100% 纯本地、无商业广告、零隐私上云的开源公益级 Windows 青少年数字健康与终端监护系统。

---

### 📦 下载推荐 (Downloads)

- **Windows 官方安装包（推荐广大用户）**：`ChildGuard-v$Version-Setup.exe`
  - 标准 Windows 安装向导，自动创建桌面与开始菜单快捷方式，支持 Windows 控制面板一键卸载。
  - 内置独立完整运行时，无需预先安装任何 .NET 环境。

- **绿色便携版（免安装）**：`ChildGuard-v$Version-win-x64-Portable.zip`
  - 解压即用，适合喜欢便携绿色的用户或放入 U 盘使用。

---

### ✨ 核心功能与亮点

1. **🛡️ 全系统级不良网站离线屏蔽**
   - 内置敏感站点黑名单，通过系统底层拦截（Hosts / 本地回环），对 Edge、Chrome、Firefox 等全浏览器及客户端生效；
   - 敏感网址动态加密存储，界面与运行中严格脱敏，不扩散不良信息；
   - 严格家长密码防护，防止未成年人绕过或关闭。

2. **👁️ 腾讯电脑管家风格·科学护眼与防沉迷**
   - **Win32 GDI 硬件级防蓝光色温调节**：3200K（暖柔）至 6500K（自然）可调，退出时色彩 100% 还原；
   - **健康用眼定时提醒**：连续使用达标（默认 45 分钟）自动唤起 4 步护眼操远涕休息全屏视窗；
   - **夜间就寝断屏锁屏**：设定就寝时段沉浸全屏锁定，引导良好作息习惯（支持家长密码放行）。

3. **🔍 WinRT 原生离线 OCR 屏幕内容审计**
   - 调用 Windows 10/11 本地硬件 OCR 引擎，无第三方外挂大模型，0 网络请求；
   - Aho-Corasick 算法极速检测五大敏感维度（色情低俗、涉赌彩票、代充私服外挂、转账欺诈等）；
   - 离线 SQLite 安全留存，100% 本地留痕，零隐私泄露。

4. **⚡ 伴生自愈看门狗双进程守护**
   - ``Haidou.Watchdog.exe`` 与前台协同保活，防止意外退出，守护更持久稳定。
"@
Set-Content -Path $notesFile -Value $notesContent -Encoding UTF8

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
