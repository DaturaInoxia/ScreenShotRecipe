# ScreenShotRecipe Deployment & Architecture

## Overview

ScreenShotRecipe is a .NET 9 Blazor Server application that imports recipes from images using Azure OpenAI GPT-4o multimodal vision. Recipes are stored in SQLite with original images on the filesystem.

## Architecture

### Project Structure

```
src/
├── ScreenShotRecipe.Web/           # Blazor Server UI + API endpoints
├── ScreenShotRecipe.Application/   # Application services (ImportService)
├── ScreenShotRecipe.Application.Contracts/  # DTOs and contracts
├── ScreenShotRecipe.Domain/        # Domain entities and interfaces
└── ScreenShotRecipe.Infrastructure/ # Implementations
    ├── Config/                     # Azure OpenAI options
    ├── Extraction/                 # GptRecipeExtractionService
    ├── Ocr/                        # Image preprocessing
    ├── Parsing/                    # LLM parsing (legacy)
    ├── Persistence/                # EF Core DbContext
    ├── Repositories/               # Data access
    └── Storage/                    # FileSystemStorage for images
```

### Key Interfaces

| Interface | Implementation | Purpose |
|-----------|---------------|---------|
| `IRecipeExtractionService` | `GptRecipeExtractionService` | GPT-4o multimodal recipe extraction |
| `IRecipeRepository` | `RecipeRepository` | Recipe CRUD operations |
| `IImageAssetRepository` | `ImageAssetRepository` | Image metadata storage |
| `IStorage` | `FileSystemStorage` | Physical image file storage |
| `IImportJobRepository` | `ImportJobRepository` | Import job tracking |

### Pages

| Route | Component | Purpose |
|-------|-----------|---------|
| `/` | `Recipes.razor` | Recipe list with search |
| `/import` | `Import.razor` | Upload images, trigger extraction |
| `/recipes/{id}` | `RecipeDetail.razor` | View recipe with original images |
| `/recipes/{id}/edit` | `RecipeEdit.razor` | Edit recipe fields |

---

## Docker Deployment

### Container Configuration

**Dockerfile** builds a multi-stage .NET 9 container exposing port 80.

**docker-compose.yml:**
```yaml
services:
  screenshotrecipe:
    build: .
    volumes:
      - ./data:/data
    ports:
      - "8080:80"
    env_file:
      - .env
```

### Data Persistence

All persistent data lives in the `/data` volume:

| Path | Content |
|------|---------|
| `/data/app.db` | SQLite database |
| `/data/images/` | Original uploaded images |

The application auto-detects Docker vs local environment:
- If `/data` directory exists → use `/data/app.db` and `/data/images`
- Otherwise → use `app.db` and `./data/images` (relative paths)

---

## Proxmox LXC Configuration

### LXC Container Settings

| Setting | Value |
|---------|-------|
| OS | Debian 12 |
| Type | Privileged container |
| Features | nesting=1 |

**Required LXC config** (`/etc/pve/lxc/<ID>.conf`):
```
lxc.apparmor.profile: unconfined
lxc.cgroup2.devices.allow: a
lxc.cap.drop: 
lxc.mount.auto: "proc:rw sys:rw"
```

### Docker Installation

```bash
apt update && apt install -y ca-certificates curl gnupg
install -m 0755 -d /etc/apt/keyrings
curl -fsSL https://download.docker.com/linux/debian/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
chmod a+r /etc/apt/keyrings/docker.gpg
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/debian $(. /etc/os-release && echo "$VERSION_CODENAME") stable" | tee /etc/apt/sources.list.d/docker.list > /dev/null
apt update
apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
```

---

## Environment Configuration

### Required Environment Variables

Create `.env` file from `.env.example`:

```bash
# Enable real GPT-4o extraction
EXTRACTION_USE_REAL=true

# Azure OpenAI credentials
AzureOpenAI__Endpoint=https://your-resource.cognitiveservices.azure.com/
AzureOpenAI__ApiKey=your-api-key
AzureOpenAI__DeploymentName=gpt-4o
```

### Configuration Hierarchy

1. Environment variables (highest priority)
2. `.env` file
3. `appsettings.json` defaults

### Optional Settings

| Variable | Default | Description |
|----------|---------|-------------|
| `DatabasePath` | Auto-detect | Override database location |
| `ImageStoragePath` | Auto-detect | Override image storage directory |
| `AzureOpenAI__MaxTokens` | 4096 | Response token limit |
| `AzureOpenAI__Temperature` | 0.2 | Model temperature (0-2) |
| `AzureOpenAI__TimeoutSeconds` | 120 | Request timeout |

---

## Deployment

### Prerequisites

- SSH access to Proxmox LXC container
- SSH key configured for passwordless login (recommended)

### Deployment Scripts

Scripts are located in `Helpers/` directory:

| Script | Purpose |
|--------|---------|
| `Deploy.ps1` | Full deployment with options |
| `QuickDeploy.ps1` | Simple deploy (edit config inside) |
| `ViewLogs.ps1` | View Docker container logs |

### Initial Setup (First Time Only)

```powershell
# 1. Copy source to server
scp -r D:\src\ScreenShotRecipe root@192.168.0.18:/opt/

# 2. SSH to server and create .env file
ssh root@192.168.0.18
cd /opt/ScreenShotRecipe
cp .env.example .env
nano .env  # Fill in Azure credentials, set EXTRACTION_USE_REAL=true

# 3. Build and start
docker compose up -d --build
```

### Update Deployment (Subsequent Deploys)

```powershell
# From Windows - run from repository root
.\Helpers\QuickDeploy.ps1

# Or with more options
.\Helpers\Deploy.ps1 -Server 192.168.0.18 -User root
```

### Useful Commands

```powershell
# View logs
.\Helpers\ViewLogs.ps1 -Follow

# Check status only
.\Helpers\Deploy.ps1 -StatusOnly

# Deploy without rebuilding (restart only)
.\Helpers\Deploy.ps1 -SkipBuild
```

### Verify Deployment

```bash
# On the server
docker compose ps
docker compose logs -f

# Test web app
curl http://localhost:8080/
```

---

## Service Implementation Modes

The application supports fake/mock services for testing:

| Environment Variable | Effect |
|---------------------|--------|
| `EXTRACTION_USE_REAL=true` | Use GPT-4o for recipe extraction |
| `EXTRACTION_USE_REAL=false` | Use fake service returning test data |

When `EXTRACTION_USE_REAL=true`, Azure OpenAI credentials are required.

---

## Logging

Logs are written to:
- Console (docker logs)
- `Logs/app-log-{date}.txt` inside container

Uses Serilog with daily rolling files, 30-day retention.

---

## Database Schema

SQLite database managed by Entity Framework Core:

| Table | Purpose |
|-------|---------|
| `Recipes` | Recipe entities with title, notes |
| `Ingredients` | Linked to recipes |
| `Steps` | Numbered cooking steps |
| `Tags` | Recipe categorization |
| `ImageAssets` | Image metadata (path, media type) |
| `RecipeImageAsset` | Many-to-many recipe-image links |
| `ImportJobs` | Import operation tracking |
