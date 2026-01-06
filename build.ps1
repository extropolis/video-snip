# VideoSnip Build Script
param(
    [switch]$Publish,
    [switch]$Clean,
    [switch]$Test
)

$ErrorActionPreference = "Stop"

Write-Host "VideoSnip Build Script" -ForegroundColor Cyan
Write-Host "======================" -ForegroundColor Cyan

# Clean if requested
if ($Clean) {
    Write-Host "`nCleaning..." -ForegroundColor Yellow
    dotnet clean VideoSnip.sln -c Release -p:Platform=x64 --verbosity minimal
    if (Test-Path "./publish") { Remove-Item -Recurse -Force "./publish" }
}

# Build
Write-Host "`nBuilding..." -ForegroundColor Yellow
dotnet build VideoSnip.sln -c Release -p:Platform=x64

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nBuild failed!" -ForegroundColor Red
    exit 1
}

Write-Host "`nBuild succeeded!" -ForegroundColor Green

# Run tests if requested
if ($Test) {
    Write-Host "`nRunning tests..." -ForegroundColor Yellow
    dotnet test VideoSnip.Tests/VideoSnip.Tests.csproj -c Release -p:Platform=x64 --no-build --filter "Category!=RequiresDisplay"

    if ($LASTEXITCODE -ne 0) {
        Write-Host "`nTests failed!" -ForegroundColor Red
        exit 1
    }
    Write-Host "`nTests passed!" -ForegroundColor Green
}

# Publish if requested
if ($Publish) {
    Write-Host "`nPublishing self-contained build..." -ForegroundColor Yellow
    dotnet publish VideoSnip/VideoSnip.csproj `
        -c Release `
        -r win-x64 `
        --self-contained `
        -p:Platform=x64 `
        -o ./publish

    if ($LASTEXITCODE -ne 0) {
        Write-Host "`nPublish failed!" -ForegroundColor Red
        exit 1
    }

    $size = (Get-ChildItem -Path "./publish" -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
    Write-Host "`nPublished to ./publish ($([math]::Round($size, 1)) MB)" -ForegroundColor Green
}

Write-Host "`nDone!" -ForegroundColor Cyan
