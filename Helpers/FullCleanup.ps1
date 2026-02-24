# Complete cleanup script - kills all dotnet processes and cleans build artifacts
# Useful when you have file lock issues or stale build files

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "ScreenShotRecipe Full Cleanup" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Kill all dotnet processes
Write-Host "[1/3] Terminating all dotnet processes..." -ForegroundColor Yellow
$dotnetProcesses = Get-Process -Name "dotnet" -ErrorAction SilentlyContinue
if ($dotnetProcesses) {
    $dotnetProcesses | Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Host "  ✓ Terminated $(($dotnetProcesses | Measure-Object).Count) dotnet process(es)" -ForegroundColor Green
} else {
    Write-Host "  - No dotnet processes found" -ForegroundColor Green
}

# Kill ScreenShotRecipe processes
Write-Host "[2/3] Terminating ScreenShotRecipe processes..." -ForegroundColor Yellow
$appProcesses = Get-Process -Name "ScreenShotRecipe*" -ErrorAction SilentlyContinue
if ($appProcesses) {
    $appProcesses | Stop-Process -Force -ErrorAction SilentlyContinue
    Write-Host "  ✓ Terminated $(($appProcesses | Measure-Object).Count) ScreenShotRecipe process(es)" -ForegroundColor Green
} else {
    Write-Host "  - No ScreenShotRecipe processes found" -ForegroundColor Green
}

# Clean build artifacts
Write-Host "[3/3] Cleaning build artifacts..." -ForegroundColor Yellow
$binDirs = @(
    "d:\src\ScreenShotRecipe\src\ScreenShotRecipe.Web\bin",
    "d:\src\ScreenShotRecipe\src\ScreenShotRecipe.Web\obj",
    "d:\src\ScreenShotRecipe\src\BlazorServer\bin",
    "d:\src\ScreenShotRecipe\src\BlazorServer\obj"
)

foreach ($dir in $binDirs) {
    if (Test-Path $dir) {
        Remove-Item $dir -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "  ✓ Removed $dir" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Cleanup complete! You can now run:" -ForegroundColor Green
Write-Host "  cd src\ScreenShotRecipe.Web" -ForegroundColor Cyan
Write-Host "  dotnet run" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Green
