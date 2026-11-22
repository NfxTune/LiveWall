# ==============================================================================
# Script tự động Publish Release LiveWall Studio hoàn chỉnh
# ==============================================================================

$projectDir = $PSScriptRoot
$distDir = "D:\Canhan\Abu1\LiveWallpaper_Dist"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Dang dong goi LiveWall Studio (Release)..." -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Tao thu muc Dist neu chua co
if (Test-Path $distDir) {
    Write-Host "[1/4] Don dep ban build cu tai: $distDir" -ForegroundColor Yellow
    Remove-Item -Path "$distDir\*" -Recurse -Force -ErrorAction SilentlyContinue
} else {
    New-Item -ItemType Directory -Path $distDir -Force | Out-Null
}

# 2. Chay dotnet publish
Write-Host "[2/4] Bien dich sang ban Release..." -ForegroundColor Green
Set-Location $projectDir
dotnet publish -c Release --no-restore -o $distDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "[LOI] dotnet publish that bai!" -ForegroundColor Red
    exit 1
}

# 3. Copy libmpv-2.dll va Assets
Write-Host "[3/4] Dong bo libmpv-2.dll, app.ico va Assets..." -ForegroundColor Green
$mpvDll = Join-Path $projectDir "libmpv-2.dll"
if (Test-Path $mpvDll) {
    Copy-Item $mpvDll -Destination $distDir -Force
}

$icoFile = Join-Path $projectDir "app.ico"
if (Test-Path $icoFile) {
    Copy-Item $icoFile -Destination $distDir -Force
}

$assetsDir = Join-Path $projectDir "Assets"
if (Test-Path $assetsDir) {
    $targetAssets = Join-Path $distDir "Assets"
    Copy-Item $assetsDir -Destination $targetAssets -Recurse -Force
}

# 4. Hoan tat
Write-Host "[4/4] Dong goi thanh cong 100%!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Thu muc phan phoi: $distDir" -ForegroundColor Yellow
Write-Host " Chay truc tiep: $distDir\LiveWallpaper.exe" -ForegroundColor White
Write-Host "==========================================================" -ForegroundColor Cyan
