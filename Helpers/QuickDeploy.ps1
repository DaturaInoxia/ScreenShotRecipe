# Quick Deploy Script - Simple version
# Run from repository root: .\Helpers\QuickDeploy.ps1

# ============================================
# CONFIGURATION - Edit these values as needed
# ============================================
$Server = "192.168.0.27"
$User = "root"
$RemotePath = "/opt/ScreenShotRecipe"
# ============================================

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)

Write-Host "Deploying to $User@$Server..." -ForegroundColor Cyan

# Copy source files (SCP)
Write-Host "Copying files..." -ForegroundColor Yellow
scp -r "$ProjectRoot\*" "$User@${Server}:$RemotePath/"

# Rebuild and restart on server (SSH)
Write-Host "Building and restarting container..." -ForegroundColor Yellow
ssh "$User@$Server" "cd $RemotePath && docker compose down && docker compose build --no-cache && docker compose up -d"

Write-Host ""
Write-Host "Done! App available at http://${Server}:8080" -ForegroundColor Green
