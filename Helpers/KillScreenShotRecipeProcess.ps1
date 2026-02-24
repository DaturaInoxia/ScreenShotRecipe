# Kill ScreenShotRecipe.Web process
# This script terminates any running instances of ScreenShotRecipe.Web to free up file locks

Write-Host "Searching for ScreenShotRecipe.Web processes..." -ForegroundColor Yellow

$processes = Get-Process -Name "ScreenShotRecipe.Web" -ErrorAction SilentlyContinue

if ($processes) {
    $processes | ForEach-Object {
        Write-Host "Found process: $($_.Name) (PID: $($_.Id))" -ForegroundColor Cyan
    }
    
    Write-Host "Terminating processes..." -ForegroundColor Yellow
    $processes | Stop-Process -Force -ErrorAction SilentlyContinue
    
    Write-Host "Successfully terminated ScreenShotRecipe.Web process(es)" -ForegroundColor Green
}
else {
    Write-Host "No running ScreenShotRecipe.Web processes found" -ForegroundColor Green
}
