# ScreenShotRecipe Helper Scripts

Utility scripts to manage the ScreenShotRecipe development environment.

## Quick Start: Run the App

### Option 1: VS Code Task (EASIEST) ⭐
Press `Ctrl+Shift+B` to run **"Run Web (with cleanup)"** task, or:
1. Press `Ctrl+Shift+P` to open Command Palette
2. Type "Run Task"
3. Select "Run Web (with cleanup)"

This automatically handles cleanup and runs your app!

### Option 2: PowerShell Script
```powershell
.\Helpers\RunWeb.ps1
```

### Option 3: Batch File (Double-click)
Just double-click `Helpers\RunWeb.bat`

---

## All Scripts

### RunWeb.ps1 / RunWeb.bat
Runs the ScreenShotRecipe.Web application with automatic cleanup.

**Features:**
- ✓ Automatically kills lingering processes
- ✓ Cleans up file locks before running
- ✓ Navigates to correct directory
- ✓ Starts `dotnet run`

**Usage:**
```powershell
# PowerShell
.\Helpers\RunWeb.ps1

# Or skip cleanup if you want:
.\Helpers\RunWeb.ps1 -NoCleanup

# Batch file (Windows CMD)
# Just double-click RunWeb.bat
```

### KillScreenShotRecipeProcess.ps1 / .bat
Terminates the running ScreenShotRecipe.Web application process.

**Usage:**
```powershell
.\KillScreenShotRecipeProcess.ps1
```

**When to use:**
- Before running `dotnet run` to avoid file lock issues
- When the app crashes but the process is still lingering
- To free up port 5000 if it's in use

### FullCleanup.ps1
Comprehensive cleanup script that:
- Terminates all dotnet processes
- Terminates all ScreenShotRecipe processes
- Removes build artifacts (bin and obj folders)

**Usage:**
```powershell
.\FullCleanup.ps1
```

**When to use:**
- After encountering persistent file lock errors
- Before rebuilding the entire solution
- When switching between projects (BlazorServer ↔ ScreenShotRecipe.Web)
- To ensure a clean build state

---

## Deployment Scripts (Proxmox)

Scripts for deploying to Proxmox LXC Docker container.

### Deploy.ps1
Full deployment script with options for deploying to Proxmox.

**Usage:**
```powershell
# Full deploy (copy, build, restart)
.\Helpers\Deploy.ps1

# With custom server settings
.\Helpers\Deploy.ps1 -Server 192.168.0.18 -User root

# Skip build (just restart container)
.\Helpers\Deploy.ps1 -SkipBuild

# Check container status only
.\Helpers\Deploy.ps1 -StatusOnly

# View logs only
.\Helpers\Deploy.ps1 -LogsOnly
```

### QuickDeploy.ps1
Simple deployment script - edit the configuration variables inside the script.

**Usage:**
```powershell
.\Helpers\QuickDeploy.ps1
```

### ViewLogs.ps1
View Docker container logs from the Proxmox server.

**Usage:**
```powershell
# View last 100 lines
.\Helpers\ViewLogs.ps1

# Follow logs (Ctrl+C to exit)
.\Helpers\ViewLogs.ps1 -Follow

# View last 50 lines
.\Helpers\ViewLogs.ps1 -Tail 50
```

---

## PowerShell Execution Policy

If you get an error like "cannot be loaded because running scripts is disabled", run PowerShell as Administrator and execute:

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

Then try running the script again.

## VS Code Keyboard Shortcuts

The `.vscode/tasks.json` file provides quick access to common tasks:

| Task | Shortcut |
|------|----------|
| **Run Web (with cleanup)** | `Ctrl+Shift+B` ⭐ |
| Other tasks | `Ctrl+Shift+P` → "Run Task" → select task |

## Quick Reference

| Task | Command |
|------|---------|
| **Run app (easiest)** | Press `Ctrl+Shift+B` in VS Code |
| Run app (PowerShell) | `.\Helpers\RunWeb.ps1` |
| Run app (double-click) | `Helpers\RunWeb.bat` |
| Kill app process | `.\Helpers\KillScreenShotRecipeProcess.ps1` |
| Full cleanup | `.\Helpers\FullCleanup.ps1` |
| **Deploy to Proxmox** | `.\Helpers\QuickDeploy.ps1` |
| View Proxmox logs | `.\Helpers\ViewLogs.ps1 -Follow` |
| Check Proxmox status | `.\Helpers\Deploy.ps1 -StatusOnly` |

## Available VS Code Tasks

Run any of these from `Ctrl+Shift+P` → "Run Task":
- **Run Web (with cleanup)** - Recommended! Cleans up first then runs
- **Run Web (no cleanup)** - Just runs dotnet (faster if no issues)
- **Kill ScreenShotRecipe processes** - Terminate the app
- **Full Cleanup** - Remove all build artifacts and processes
