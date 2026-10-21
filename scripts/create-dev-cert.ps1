param(
    [string]$Password = "HaiwenNaAI@2026",
    [string]$Subject = "CN=海文娜AI, O=海文娜AI, C=CN",
    [string]$OutputDir = "$PSScriptRoot\..\certificates"
)

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  海文娜AI (HaiwenNa AI) - 代码签名证书生成工具" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
}

$cerPath = Join-Path $OutputDir "HaiwenNa-CodeSigning.cer"
$pfxPath = Join-Path $OutputDir "HaiwenNa-CodeSigning.pfx"

Write-Host "[1/3] 生成 SHA256 代码签名自签名证书..." -ForegroundColor Yellow
$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $Subject `
    -FriendlyName "海文娜AI 代码签名证书" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -HashAlgorithm "SHA256" `
    -KeyLength 2048 `
    -KeyUsage DigitalSignature `
    -NotAfter (Get-Date).AddYears(5)

Write-Host "  -> 指纹: $($cert.Thumbprint)" -ForegroundColor DarkGray
Write-Host "  -> 主题: $($cert.Subject)" -ForegroundColor DarkGray

Write-Host "[2/3] 导出公开证书 (.cer，供用户/测试机导入信任)..." -ForegroundColor Yellow
Export-Certificate -Cert $cert -FilePath $cerPath -Force | Out-Null
Write-Host "  -> 已生成: $cerPath" -ForegroundColor Green

Write-Host "[3/3] 导出带私钥的代码签名证书 (.pfx，用于构建自动签名)..." -ForegroundColor Yellow
$securePwd = ConvertTo-SecureString $Password -AsPlainText -Force
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $securePwd -Force | Out-Null
Write-Host "  -> 已生成: $pfxPath" -ForegroundColor Green

Write-Host ""
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host " [成功] 海文娜AI 代码签名证书已生成完毕！" -ForegroundColor Green
Write-Host " 密码: $Password" -ForegroundColor Yellow
Write-Host "========================================================" -ForegroundColor Cyan