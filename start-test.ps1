Write-Host "Building Haidou.ChildGuard.Wpf..."
dotnet build src/Haidou.ChildGuard.Wpf/Haidou.ChildGuard.Wpf.csproj -c Debug
if ($LASTEXITCODE -eq 0) {
    Write-Host "Launching Haidou.ChildGuard.Wpf.exe..." -ForegroundColor Green
    Start-Process "src\Haidou.ChildGuard.Wpf\bin\Debug\net10.0-windows10.0.19041.0\Haidou.ChildGuard.Wpf.exe"
} else {
    Write-Host "Build failed." -ForegroundColor Red
}
