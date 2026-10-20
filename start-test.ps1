Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  Haidou ChildGuard - Launching Test" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "[1/3] Terminating old processes..." -ForegroundColor Yellow
Stop-Process -Name "Haidou.ChildGuard.Wpf", "Haidou.Watchdog" -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1

Write-Host "[2/3] Building latest Haidou.ChildGuard.Wpf..." -ForegroundColor Yellow
dotnet build src/Haidou.ChildGuard.Wpf/Haidou.ChildGuard.Wpf.csproj -c Debug
if ( -eq 0) {
    Write-Host "[3/3] Launching Haidou.ChildGuard.Wpf.exe..." -ForegroundColor Green
    $binDir = "$PSScriptRoot\src\Haidou.ChildGuard.Wpf\bin\Debug\net10.0-windows10.0.19041.0"
    $exe = Join-Path $binDir "Haidou.ChildGuard.Wpf.exe"
    Start-Process -FilePath $exe -WorkingDirectory $binDir
    Write-Host ""
    Write-Host "[SUCCESS] ChildGuard has been launched successfully!" -ForegroundColor Green
} else {
    Write-Host "[ERROR] Build failed." -ForegroundColor Red
}