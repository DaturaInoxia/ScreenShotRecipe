# Rebuild and Restart Docker Container on Proxmox
# Run from repository root: .\Helpers\RestartContainer.ps1

# ============================================
# CONFIGURATION - Edit these values as needed
# ============================================
$Server = "192.168.0.27"
$User = "root"
$RemotePath = "/opt/ScreenShotRecipe"
# ============================================

Write-Host "Rebuilding and restarting container on $User@$Server..." -ForegroundColor Cyan

ssh "$User@$Server" "cd $RemotePath && docker compose down && docker compose build --no-cache && docker compose up -d"

Write-Host ""
Write-Host "Done! App available at http://${Server}:8080" -ForegroundColor Green
