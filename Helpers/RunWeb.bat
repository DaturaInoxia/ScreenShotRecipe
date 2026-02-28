@echo off
REM Run ScreenShotRecipe.Web with automatic cleanup
REM This script cleans up file locks and runs the application in one command

setlocal enabledelayedexpansion

echo.
echo ========================================
echo ScreenShotRecipe.Web Runner
echo ========================================
echo.

echo [1/2] Running cleanup...

taskkill /IM dotnet.exe /F >nul 2>&1
taskkill /IM ScreenShotRecipe.Web.exe /F >nul 2>&1

echo   * Cleanup complete
echo.

REM Get the path to the Web project
cd /d "%~dp0..\src\ScreenShotRecipe.Web"

echo [2/2] Starting ScreenShotRecipe.Web...
echo   Path: !cd!
echo   Environment: Development
echo.

set ASPNETCORE_ENVIRONMENT=Development
dotnet run

echo.
echo Application stopped.
pause
