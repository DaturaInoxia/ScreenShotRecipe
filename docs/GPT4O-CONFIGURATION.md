# GPT-4o Configuration Guide

This document explains how to configure Azure OpenAI or OpenAI API credentials for real recipe extraction.

> **Security Note**: Never commit API keys or secrets to source control. The [.env.example](.env.example) file shows the structure, but you must create your own `.env` file with real values (which is git-ignored).

## Configuration Hierarchy

Configuration is read from multiple sources (later sources override earlier ones):

1. `appsettings.json` (non-secret defaults only)
2. `appsettings.{Environment}.json`
3. User secrets (local development)
4. Environment variables (Docker/production)
5. Command line arguments

## Quick Start: Use Fake Provider (No Secrets Required)

By default, the app uses `FakeRecipeExtractionService` which returns mock recipe data. This is perfect for:
- Local development
- UI testing
- CI/CD pipelines
- Demos

No configuration needed! The fake provider is enabled by default.

## Option 1: Local Development with User Secrets

For local development, use the .NET Secret Manager to store credentials outside your project:

```powershell
# Navigate to the Web project
cd src/ScreenShotRecipe.Web

# Initialize user secrets (already done - UserSecretsId is in .csproj)
dotnet user-secrets init

# Set Azure OpenAI credentials
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key-here"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-4o"

# OR set direct OpenAI API key
dotnet user-secrets set "AzureOpenAI:OpenAIApiKey" "sk-your-openai-key-here"

# Enable real extraction
dotnet user-secrets set "ServiceImplementation:UseRealExtractionService" "true"
```

View your secrets:
```powershell
dotnet user-secrets list
```

Remove a secret:
```powershell
dotnet user-secrets remove "AzureOpenAI:ApiKey"
```

## Option 2: Docker with Environment Variables

Create a `.env` file from the example:

```powershell
copy .env.example .env
```

Edit `.env` with your credentials:

```env
EXTRACTION_USE_REAL=true

# For Azure OpenAI:
AzureOpenAI__Endpoint=https://your-resource.openai.azure.com/
AzureOpenAI__ApiKey=your-api-key-here
AzureOpenAI__DeploymentName=gpt-4o

# OR for direct OpenAI:
AzureOpenAI__OpenAIApiKey=sk-your-openai-key-here
```

Run with Docker Compose:
```powershell
docker-compose up --build
```

## Option 3: Cloud Deployment (Azure App Service, etc.)

In Azure App Service, set these as Application Settings:

| Name | Value |
|------|-------|
| `ServiceImplementation__UseRealExtractionService` | `true` |
| `AzureOpenAI__Endpoint` | `https://your-resource.openai.azure.com/` |
| `AzureOpenAI__ApiKey` | `your-api-key` |
| `AzureOpenAI__DeploymentName` | `gpt-4o` |

Or use Azure Key Vault references for enhanced security:
```
@Microsoft.KeyVault(SecretUri=https://myvault.vault.azure.net/secrets/AzureOpenAI-ApiKey/)
```

## Configuration Options Reference

### Service Implementation Options

| Setting | Default | Description |
|---------|---------|-------------|
| `ServiceImplementation:UseRealExtractionService` | `false` | Use GPT-4o (true) or fake provider (false) |

Environment variable: `EXTRACTION_USE_REAL`

### Azure OpenAI Options

| Setting | Default | Description |
|---------|---------|-------------|
| `AzureOpenAI:Endpoint` | _(required)_ | Azure OpenAI endpoint URL |
| `AzureOpenAI:ApiKey` | _(required)_ | Azure OpenAI API key |
| `AzureOpenAI:DeploymentName` | `gpt-4o` | Model deployment name |
| `AzureOpenAI:OpenAIApiKey` | _(optional)_ | Direct OpenAI API key (alternative to Azure) |
| `AzureOpenAI:OpenAIModel` | `gpt-4o` | Direct OpenAI model name |
| `AzureOpenAI:MaxTokens` | `4096` | Max response tokens |
| `AzureOpenAI:Temperature` | `0.2` | Generation temperature (0.0-2.0) |
| `AzureOpenAI:TimeoutSeconds` | `120` | Request timeout |

Environment variables use double underscore: `AzureOpenAI__ApiKey`

## Troubleshooting

### "Invalid AzureOpenAIOptions" Error

The real extraction service validates credentials at startup. Check:
1. Is `UseRealExtractionService` set to `true`?
2. Are Azure credentials (Endpoint + ApiKey + DeploymentName) OR OpenAI API key configured?
3. Are environment variable names correct? (use `__` not `:`)

### Secrets Not Loading

1. For user secrets: Ensure `ASPNETCORE_ENVIRONMENT=Development`
2. For .env file: Ensure it's in the solution root directory
3. Check logs at startup for "Azure OpenAI configuration:" message

### Using Wrong Provider

Check startup logs:
- "Using FAKE recipe extraction service" = mock provider (no API calls)
- "Using GPT-4o multimodal recipe extraction service" = real provider
