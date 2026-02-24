@echo off
REM Kill ScreenShotRecipe.Web process
REM This script terminates any running instances of ScreenShotRecipe.Web to free up file locks

echo Searching for ScreenShotRecipe.Web processes...

tasklist | findstr /i "ScreenShotRecipe.Web" >nul
if %errorlevel% equ 0 (
    echo Found ScreenShotRecipe.Web process. Terminating...
    taskkill /IM ScreenShotRecipe.Web.exe /F
    if %errorlevel% equ 0 (
        echo Successfully terminated ScreenShotRecipe.Web process(es)
    ) else (
        echo Error terminating process
    )
) else (
    echo No running ScreenShotRecipe.Web processes found
)

pause
