# View Docker Logs from Proxmox LXC
# Run from repository root: .\Helpers\ViewLogs.ps1

param(
    [int]$Tail = 100,
    [switch]$Follow
)

# ============================================
# CONFIGURATION - Edit these values as needed
# ============================================
$Server = "192.168.0.27"
$User = "root"
$RemotePath = "/opt/ScreenShotRecipe"
# ============================================

if ($Follow) {
    Write-Host "Following logs (Ctrl+C to exit)..." -ForegroundColor Yellow
    ssh "$User@$Server" "cd $RemotePath && docker compose logs -f --tail=$Tail"
} else {
    ssh "$User@$Server" "cd $RemotePath && docker compose logs --tail=$Tail"
}
