param(
    [string]$Version = "1.0.1"
)

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  Haidou ChildGuard - Building Release v$Version" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

# 1. 杀除可能残留的旧进程
Write-Host "[1/6] Terminating old processes..." -ForegroundColor Yellow
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
Write-Host "[2/6] Publishing Self-Contained Release (win-x64)..." -ForegroundColor Yellow
dotnet publish "$PSScriptRoot\src\Haidou.ChildGuard.Wpf\Haidou.ChildGuard.Wpf.csproj" -c Release -r win-x64 --self-contained true -o $publishDir
if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Dotnet publish failed." -ForegroundColor Red
    exit 1
}

# 4. 捆绑关键 Visual C++ 原生运行库 (彻底解决目标机缺少 VC++ Redist 导致的 0x80131506 崩溃)
Write-Host "[3/6] Bundling Visual C++ native runtime dependencies..." -ForegroundColor Yellow
$vcDlls = @("vcruntime140.dll", "vcruntime140_1.dll", "msvcp140.dll", "msvcp140_1.dll", "msvcp140_2.dll")
foreach ($dll in $vcDlls) {
    $srcDll = Join-Path "$env:SystemRoot\System32" $dll
    if (Test-Path $srcDll) {
        Copy-Item -Path $srcDll -Destination $publishDir -Force
        Write-Host "  -> Bundled: $dll" -ForegroundColor DarkGray
    }
}

# 5. 生成便携版压缩包
Write-Host "[4/6] Creating Portable ZIP package..." -ForegroundColor Yellow
if (Test-Path $portableZip) { Remove-Item -Force $portableZip }
Compress-Archive -Path "$publishDir\*" -DestinationPath $portableZip -Force

# 6. 生成 Inno Setup 安装包
Write-Host "[5/6] Compiling Inno Setup Installer..." -ForegroundColor Yellow
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

# 7. 生成发布说明 Release Notes
$notesContent = @"
# 🌟 童伴守护 (ChildGuard / 守护星) v$Version 稳定性加固发布

> 100% 纯本地、无商业广告、零隐私上云的开源公益级 Windows 青少年数字健康与终端监护系统。

---

### 🚀 v$Version 关键修复与兼容性加固

- **全面修复 `0x80131506` 启动闪退问题**：
  - **捆绑原生 VC++ 运行库**：安装包与便携版已内置 `vcruntime140.dll`、`vcruntime140_1.dll` 与 `msvcp140.dll`，在未安装 VC++ 2015-2022 的裸机 Windows 10/11 上开箱即用；
  - **硬件 Gamma P/Invoke 内存安全加固**：彻底消除非托管内存越界与堆破坏风险，增加无显卡/虚拟机/老显卡环境优雅降级；
  - **Windows 10/11 视效自适应**：取消 Win10 下强启 Mica 导致的 DWM 崩溃风险，支持自适应动态启用；
  - **内置全局崩溃日志保护机制**：遇异常自动拦截并记录于本地 `%LocalAppData%\HaidouChildGuard\logs`，彻底杜绝无声闪退。

---

### 📦 下载推荐 (Downloads)

- **Windows 官方安装包（推荐广大用户）**：`ChildGuard-v$Version-Setup.exe`
  - 标准 Windows 安装向导，自动创建桌面与开始菜单快捷方式，支持 Windows 控制面板一键卸载。
  - 内置独立完整运行时，无需预先安装任何 .NET 环境。

- **绿色便携版（免安装）**：`ChildGuard-v$Version-win-x64-Portable.zip`
  - 解压即用，适合喜欢便携绿色的用户或放入 U 盘使用。

---

### 🔐 校验和信息 (SHA256)

- 见下方构建生成清单
"@
Set-Content -Path $notesFile -Value $notesContent -Encoding UTF8

# 8. 计算 SHA256 与文件信息
Write-Host ""
Write-Host "[6/6] Release Packages Summary:" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Cyan
Get-Item $setupExe, $portableZip -ErrorAction SilentlyContinue | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
    $sizeMb = [math]::Round($_.Length / 1MB, 2)
    Write-Host "File  : $($_.Name) ($sizeMb MB)" -ForegroundColor White
    Write-Host "SHA256: $hash" -ForegroundColor Gray
    Write-Host ""
}
Write-Host "[SUCCESS] Release build completed!" -ForegroundColor Green
