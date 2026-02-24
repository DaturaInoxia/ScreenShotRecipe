# Run ScreenShotRecipe.Web with automatic cleanup
# This script cleans up file locks and runs the application in one command

param(
    [switch]$NoCleanup = $false
)

$webProjectPath = Join-Path (Split-Path (Split-Path $PSScriptRoot)) "src\ScreenShotRecipe.Web"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "ScreenShotRecipe.Web Runner" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Cleanup unless --NoCleanup flag is used
if (-not $NoCleanup) {
    Write-Host "[1/2] Running cleanup..." -ForegroundColor Yellow
    
    # Kill all dotnet processes
    Get-Process -Name "dotnet" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Get-Process -Name "ScreenShotRecipe*" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    
    Write-Host "  ✓ Cleanup complete" -ForegroundColor Green
} else {
    Write-Host "[1/2] Skipping cleanup (--NoCleanup flag used)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "[2/2] Starting ScreenShotRecipe.Web..." -ForegroundColor Yellow
Write-Host "  Path: $webProjectPath" -ForegroundColor Cyan
Write-Host "  Environment: Development" -ForegroundColor Cyan
Write-Host ""

# Change to the project directory and run in Development mode
Set-Location $webProjectPath
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run

Write-Host ""
Write-Host "Application stopped." -ForegroundColor Yellow
