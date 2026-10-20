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
$isccPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (-not (Test-Path $isccPath)) {
    $isccPath = "C:\Program Files\Inno Setup 6\ISCC.exe"
}
if (Test-Path $isccPath) {
    & $isccPath "$PSScriptRoot\installer\ChildGuard.iss"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[WARN] Inno Setup compilation failed or returned non-zero." -ForegroundColor Yellow
    }
} else {
    Write-Host "[SKIP] Inno Setup compiler not found in default paths." -ForegroundColor Yellow
}

# 6. 计算 SHA256 与文件信息
Write-Host ""
Write-Host "[5/5] Release Packages Summary:" -ForegroundColor Green
Write-Host "========================================================" -ForegroundColor Cyan
Get-Item $setupExe, $portableZip -ErrorAction SilentlyContinue | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
    $sizeMb = [math]::Round($_.Length / 1MB, 2)
    Write-Host "File : $($_.Name) ($sizeMb MB)" -ForegroundColor White
    Write-Host "SHA256: $hash" -ForegroundColor Gray
    Write-Host ""
}
Write-Host "[SUCCESS] Release build completed!" -ForegroundColor Green
