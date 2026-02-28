# Quickstart: ScreenShotRecipe

**Purpose**: Get the recipe import application running  
**Duration**: ~5 min (fake services) | ~15 min (real GPT-4o)

---

## Prerequisites

- .NET 9 SDK installed
- Git repository cloned: `DaturaInoxia/ScreenShotRecipe`
- Azure OpenAI resource (optional, only for real extraction)

---

## Option 1: Fake Services (No API Required)

**Time: 5 minutes** | **Cost: Free**

Fake services return deterministic test data for UI development and testing.

### Step 1: Configure Fake Mode

Edit `src/ScreenShotRecipe.Web/appsettings.json`:

```json
{
  "ServiceImplementation": {
    "UseRealExtractionService": false
  }
}
```

### Step 2: Build & Run

```powershell
cd src/ScreenShotRecipe.Web
dotnet build
dotnet run
```

### Step 3: Test Import

Navigate to `http://localhost:5000` and upload any images. The fake service returns a sample "Chocolate Chip Cookies" recipe.

**Fake Response Example**:
```json
{
  "title": "Chocolate Chip Cookies",
  "ingredients": [
    {"name": "all-purpose flour", "quantity": "2.25", "unit": "cups"},
    {"name": "butter", "quantity": "1", "unit": "cup"}
  ],
  "steps": [
    {"ordinal": 1, "text": "Preheat oven to 375°F"},
    {"ordinal": 2, "text": "Cream butter and sugars"}
  ],
  "confidence": 0.92
}
```

### Simulate Low Confidence

Set environment variable before running:
```powershell
$env:FAKE_EXTRACTION_CONFIDENCE = "65"
dotnet run
```

---

## Option 2: Real GPT-4o (Azure OpenAI)

**Time: 15 minutes** | **Cost: ~$4-5/month for typical family use**

### Step 1: Azure OpenAI Setup

1. Create an Azure OpenAI resource in [Azure Portal](https://portal.azure.com)
2. Deploy the `gpt-4o` model (recommended: `gpt-4o-2` deployment name)
3. Note the **Endpoint** and **API Key** from the resource

### Step 2: Configure User Secrets

User secrets keep API keys out of source control:

```powershell
cd src/ScreenShotRecipe.Web

# Initialize user secrets (if not already done)
dotnet user-secrets init

# Set Azure OpenAI credentials
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key-here"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-4o-2"

# Enable real extraction service
dotnet user-secrets set "ServiceImplementation:UseRealExtractionService" "true"
```

### Step 3: Run in Development Mode

User secrets only load in Development environment:

```powershell
# Using helper script (recommended)
.\Helpers\RunWeb.ps1

# Or manually
cd src/ScreenShotRecipe.Web
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run
```

### Step 4: Verify Configuration

Check logs for confirmation:
```
[INF] Using GPT-4o multimodal recipe extraction service
[INF] Azure OpenAI configuration: Endpoint=https://...openai.azure.com/, Deployment=gpt-4o-2, ApiKey=********
```

---

## Docker Deployment

For production deployment:

```bash
# Build image
docker build -t screenshotrecipe:latest .

# Run with environment variables (no user secrets in containers)
docker run -d \
  -e AzureOpenAI__Endpoint="https://your-resource.openai.azure.com/" \
  -e AzureOpenAI__ApiKey="your-api-key" \
  -e AzureOpenAI__DeploymentName="gpt-4o-2" \
  -e ServiceImplementation__UseRealExtractionService="true" \
  -v $(pwd)/data:/app/data \
  -p 5000:80 \
  screenshotrecipe:latest
```

Or use docker-compose with `.env` file:

```bash
# .env file (gitignored)
AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/
AZURE_OPENAI_API_KEY=your-api-key
AZURE_OPENAI_DEPLOYMENT=gpt-4o-2

docker-compose up -d
```

---

## Configuration Reference

### appsettings.json (safe to commit)

```json
{
  "ServiceImplementation": {
    "UseRealExtractionService": false
  },
  "AzureOpenAI": {
    "Endpoint": "",
    "ApiKey": "",
    "DeploymentName": "gpt-4o",
    "MaxTokens": 4096,
    "Temperature": 0.2,
    "TimeoutSeconds": 120
  }
}
```

### User Secrets (local development)

```powershell
# View current secrets
dotnet user-secrets list

# Clear all secrets
dotnet user-secrets clear
```

### Environment Variables (production)

| Variable | Description |
|----------|-------------|
| `AzureOpenAI__Endpoint` | Azure OpenAI resource URL |
| `AzureOpenAI__ApiKey` | API key (keep secret!) |
| `AzureOpenAI__DeploymentName` | Model deployment name |
| `ServiceImplementation__UseRealExtractionService` | `true` for GPT-4o |

---

## Switching Between Modes

| Mode | UseRealExtractionService | API Required | Use Case |
|------|--------------------------|--------------|----------|
| Fake | `false` | No | UI development, testing |
| Real | `true` | Yes | Production, real extraction |

**Quick toggle**:
```powershell
# Switch to fake
dotnet user-secrets set "ServiceImplementation:UseRealExtractionService" "false"

# Switch to real
dotnet user-secrets set "ServiceImplementation:UseRealExtractionService" "true"
```

---

## Troubleshooting

### "Using FAKE recipe extraction service" when expecting real
- Verify `ASPNETCORE_ENVIRONMENT=Development` is set
- Check user secrets are configured: `dotnet user-secrets list`
- Ensure `UseRealExtractionService` is `true`

### "Azure OpenAI API key not configured"
- User secrets only load in Development mode
- For Docker/production, use environment variables with double underscore syntax

### Port 5000 already in use
```powershell
Get-Process dotnet -ErrorAction SilentlyContinue | Stop-Process -Force
```

---

## Next Steps

- [FAKE-SERVICES-GUIDE.md](FAKE-SERVICES-GUIDE.md) - Detailed fake service scenarios
- [TESTING-GUIDE.md](TESTING-GUIDE.md) - Test coverage and strategies
- [plan.md](plan.md) - Full implementation plan
