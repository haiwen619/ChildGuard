param(
    [string]$Version = "1.0.6",
    [string]$CertPassword = "HaiwenNaAI@2026"
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
$templateFile = "$PSScriptRoot\release-notes.template.md"
$certPfx = "$PSScriptRoot\certificates\HaiwenNa-CodeSigning.pfx"
$certCer = "$PSScriptRoot\certificates\HaiwenNa-CodeSigning.cer"
$certBat = "$PSScriptRoot\certificates\一键安装海文娜AI受信任证书.bat"

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

# 4. 代码签名：海文娜AI (HaiwenNa AI) 数字签名注入
Write-Host "[3/6] Signing binaries with HaiwenNa AI Authenticode certificate..." -ForegroundColor Yellow
$codeCert = $null
if (Test-Path $certPfx) {
    try {
        $secPwd = ConvertTo-SecureString $CertPassword -AsPlainText -Force
        $codeCert = [System.Security.Cryptography.X509Certificates.X509Certificate2]::new($certPfx, $secPwd)
        Write-Host "  -> Loaded Code Signing Certificate: $($codeCert.Subject)" -ForegroundColor Green
    } catch {
        Write-Host "  -> [WARN] Failed to load certificate: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

if ($codeCert) {
    $filesToSign = @(
        Join-Path $publishDir "Haidou.ChildGuard.Wpf.exe",
        Join-Path $publishDir "Haidou.Watchdog.exe"
    )
    foreach ($file in $filesToSign) {
        if (Test-Path $file) {
            try {
                Set-AuthenticodeSignature -FilePath $file -Certificate $codeCert -HashAlgorithm SHA256 -TimestampServer "http://timestamp.digicert.com" -ErrorAction Stop | Out-Null
                Write-Host "  -> Signed (with Timestamp): $(Split-Path $file -Leaf)" -ForegroundColor DarkGray
            } catch {
                Set-AuthenticodeSignature -FilePath $file -Certificate $codeCert -HashAlgorithm SHA256 -ErrorAction SilentlyContinue | Out-Null
                Write-Host "  -> Signed (offline): $(Split-Path $file -Leaf)" -ForegroundColor DarkGray
            }
        }
    }
}

# 随包附带受信任证书安装批处理工具
if (Test-Path $certCer) { Copy-Item -Path $certCer -Destination $publishDir -Force }
if (Test-Path $certBat) { Copy-Item -Path $certBat -Destination $publishDir -Force }

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
    & $isccPath "/DMyAppVersion=$Version" "/DMyAppSourceDir=$publishDir" "/DMyOutputBaseFilename=ChildGuard-v$Version-Setup" "$PSScriptRoot\installer\ChildGuard.iss"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "[WARN] Inno Setup compilation failed or returned non-zero." -ForegroundColor Yellow
    }
} else {
    Write-Host "[SKIP] Inno Setup compiler not found in system." -ForegroundColor Yellow
}

# 对生成的 Setup.exe 执行海文娜AI数字签名
if ($codeCert -and (Test-Path $setupExe)) {
    try {
        Set-AuthenticodeSignature -FilePath $setupExe -Certificate $codeCert -HashAlgorithm SHA256 -TimestampServer "http://timestamp.digicert.com" -ErrorAction Stop | Out-Null
        Write-Host "  -> Signed Setup Package (with Timestamp): $(Split-Path $setupExe -Leaf)" -ForegroundColor Green
    } catch {
        Set-AuthenticodeSignature -FilePath $setupExe -Certificate $codeCert -HashAlgorithm SHA256 -ErrorAction SilentlyContinue | Out-Null
        Write-Host "  -> Signed Setup Package (offline): $(Split-Path $setupExe -Leaf)" -ForegroundColor Green
    }
}

# 7. 生成发布说明 Release Notes
if (Test-Path $templateFile) {
    $templateContent = Get-Content $templateFile -Raw -Encoding UTF8
    $notesContent = $templateContent.Replace("__VERSION__", $Version)
    Set-Content -Path $notesFile -Value $notesContent -Encoding UTF8
}

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