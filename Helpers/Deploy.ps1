# ScreenShotRecipe Deployment Script
# Deploys the application to Proxmox LXC container running Docker
# Run from repository root: .\Helpers\Deploy.ps1

param(
    [string]$Server = "192.168.0.27",
    [string]$User = "root",
    [string]$RemotePath = "/opt/ScreenShotRecipe",
    [switch]$SkipBuild,
    [switch]$LogsOnly,
    [switch]$StatusOnly
)

$ErrorActionPreference = "Stop"

# Get script directory and project root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir

Write-Host "ScreenShotRecipe Deployment" -ForegroundColor Cyan
Write-Host "============================" -ForegroundColor Cyan
Write-Host "Server: $User@$Server"
Write-Host "Remote Path: $RemotePath"
Write-Host ""

# Check if we just want status or logs
if ($StatusOnly) {
    Write-Host "Checking container status..." -ForegroundColor Yellow
    ssh "$User@$Server" "cd $RemotePath && docker compose ps"
    exit 0
}

if ($LogsOnly) {
    Write-Host "Showing container logs (Ctrl+C to exit)..." -ForegroundColor Yellow
    ssh "$User@$Server" "cd $RemotePath && docker compose logs -f --tail=100"
    exit 0
}

# Step 1: Copy source files
Write-Host "[1/3] Copying source files..." -ForegroundColor Yellow

# Use scp to copy, excluding .env, data folder, and build artifacts
# Create a temporary exclude file
$ExcludeFile = Join-Path $env:TEMP "scp-exclude.txt"
@"
.env
data/
bin/
obj/
.git/
*.db
"@ | Set-Content $ExcludeFile

# Use rsync if available (better for incremental updates), otherwise scp
$rsyncAvailable = $false
try {
    rsync --version 2>$null | Out-Null
    $rsyncAvailable = $true
} catch { }

if ($rsyncAvailable) {
    Write-Host "  Using rsync for incremental sync..."
    rsync -avz --delete `
        --exclude='.env' `
        --exclude='data/' `
        --exclude='bin/' `
        --exclude='obj/' `
        --exclude='.git/' `
        --exclude='*.db' `
        "$ProjectRoot/" "$User@${Server}:$RemotePath/"
} else {
    Write-Host "  Using scp (full copy)..."
    # SCP doesn't have exclude, so copy everything
    # The .env file on server will be preserved since we don't overwrite it next
    scp -r "$ProjectRoot\*" "$User@${Server}:$RemotePath/"
}

Write-Host "  Source files copied." -ForegroundColor Green

# Step 2: Build and restart container
if (-not $SkipBuild) {
    Write-Host "[2/3] Building Docker container..." -ForegroundColor Yellow
    ssh "$User@$Server" "cd $RemotePath && docker compose build --no-cache"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "  ERROR: Docker build failed (exit code $LASTEXITCODE)." -ForegroundColor Red
        exit 1
    }
    Write-Host "  Build complete." -ForegroundColor Green
} else {
    Write-Host "[2/3] Skipping build (--SkipBuild flag set)" -ForegroundColor Yellow
}

Write-Host "[3/3] Restarting container..." -ForegroundColor Yellow
ssh "$User@$Server" "cd $RemotePath && docker compose down && docker compose up -d"
if ($LASTEXITCODE -ne 0) {
    Write-Host "  ERROR: docker compose up failed (exit code $LASTEXITCODE)." -ForegroundColor Red
    Write-Host "  Check for missing .env file or port conflicts on the server." -ForegroundColor Red
    exit 1
}
Write-Host "  Container started, waiting 5 seconds to check health..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

# Verify the container is still running (not crashed)
$runningContainers = ssh "$User@$Server" "cd $RemotePath && docker compose ps --filter status=running --format '{{.Name}}'"
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($runningContainers)) {
    Write-Host "  ERROR: Container is not running after startup. Showing logs:" -ForegroundColor Red
    ssh "$User@$Server" "cd $RemotePath && docker compose logs --tail=50"
    exit 1
}
Write-Host "  Container is running." -ForegroundColor Green

# Final status
Write-Host ""
Write-Host "Deployment complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Container status:" -ForegroundColor Cyan
ssh "$User@$Server" "cd $RemotePath && docker compose ps"

Write-Host ""
Write-Host "Access the app at: http://${Server}:8080" -ForegroundColor Cyan
Write-Host ""
Write-Host "Useful commands:" -ForegroundColor Yellow
Write-Host "  View logs:    .\Helpers\Deploy.ps1 -LogsOnly"
Write-Host "  Check status: .\Helpers\Deploy.ps1 -StatusOnly"
