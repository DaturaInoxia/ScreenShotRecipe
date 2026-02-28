# Implementation Plan: Import Recipe from Multiple Images

**Branch**: `001-import-recipe` | **Date**: 2026-02-27 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-import-recipe/spec.md`
**Status**: ✅ Phase 0-2 Complete - Feature Implemented

## Summary

Build a single-button workflow to import recipes from multiple screenshot/photo images. The system uses GPT-4o multimodal vision API for OCR and structured parsing, stores parsed recipes and original images in SQLite/local filesystem, and provides a Blazor Server UI for browsing, searching, viewing, editing, printing, and deleting recipes.

**Technical Approach**: GPT-4o vision model replaces traditional OCR + separate LLM parsing with a unified multimodal API call. Images are preprocessed server-side using SixLabors.ImageSharp, sent to Azure OpenAI GPT-4o deployment, and parsed into structured Recipe entities with confidence metadata.

## Technical Context

**Language/Version**: .NET 9.0  
**Primary Dependencies**: 
- Blazor Server (InteractiveServer render mode)
- Entity Framework Core 9.0 (SQLite provider)
- SixLabors.ImageSharp (image preprocessing)
- Azure.AI.OpenAI (GPT-4o multimodal client)
- Serilog (structured logging)

**Storage**: SQLite (app.db) + local filesystem (images/)  
**Testing**: xUnit + Moq  
**Target Platform**: Windows/Linux x64, Docker container  
**Project Type**: Web application (Blazor Server + Minimal APIs)  
**Performance Goals**: <30 seconds end-to-end for 6-image import  
**Constraints**: <100MB memory baseline, single-node deployment  
**Scale/Scope**: Family use (~100 recipes), single-tenant mode

## Constitution Check

*GATE: ✅ All checks passed*

| Principle | Status | Notes |
|-----------|--------|-------|
| **1. Reliability First** | ✅ PASS | Retry policies with exponential backoff implemented in GPT service |
| **2. GPT-4o Multimodal Extraction** | ✅ PASS | GPT-4o multimodal for combined OCR + parsing per updated constitution |
| **3. Pluggable LLM Parsing** | ✅ PASS | `IRecipeExtractionService` interface with DI registration |
| **4. Clean Architecture** | ✅ PASS | Domain/Application/Application.Contracts/Infrastructure/Web projects |
| **5. Strict Separation** | ✅ PASS | DTOs in Application.Contracts, interfaces in Domain |
| **6. Service-Based DI** | ✅ PASS | All external deps injected via DI container |
| **7. Blazor Server + Minimal APIs** | ✅ PASS | Primary UI is Blazor Server, APIs in Program.cs |
| **8. SQLite Persistence** | ✅ PASS | EF Core with SQLite, repository pattern |
| **9. Local Filesystem Storage** | ✅ PASS | `IImageStorage` interface with local implementation |
| **10. Docker Deployment** | ✅ PASS | Dockerfile and docker-compose provided |
| **11. Testability** | ✅ PASS | Fake services for OCR/parsing, unit tests exist |
| **12. Maintainability** | ✅ PASS | Versioned contracts, documented interfaces |

## Project Structure

### Documentation (this feature)

```text
specs/001-import-recipe/
├── plan.md               # This file (implementation plan)
├── research.md           # Phase 0: GPT-4o OCR research findings
├── data-model.md         # Phase 1: Entity definitions and relationships
├── quickstart.md         # Phase 1: Developer setup guide
├── contracts/            # Phase 1: Interface contracts
│   ├── DTOs-and-Contracts.md
│   └── IRecipeOcrService.md
├── tasks.md              # Phase 2: Implementation tasks
├── FAKE-SERVICES-GUIDE.md
├── TESTING-GUIDE.md
└── QUICK-REFERENCE.md
```

### Source Code (repository root)

```text
src/
├── ScreenShotRecipe.Domain/           # Domain entities and interfaces
│   ├── Entities/
│   │   ├── Recipe.cs
│   │   ├── Ingredient.cs
│   │   ├── Step.cs
│   │   ├── ImageAsset.cs
│   │   └── ImportJob.cs
│   └── Interfaces/
│       ├── IRecipeRepository.cs
│       ├── IImageStorage.cs
│       ├── IImagePreprocessor.cs
│       └── IImportJobRepository.cs
│
├── ScreenShotRecipe.Application/       # Application services
│   └── Services/
│       ├── ImportService.cs
│       └── RecipeService.cs
│
├── ScreenShotRecipe.Application.Contracts/  # DTOs and shared contracts
│   └── Dtos/
│       ├── RecipeDto.cs
│       ├── ImportResultDto.cs
│       └── IngredientDto.cs
│
├── ScreenShotRecipe.Infrastructure/    # External integrations
│   ├── Extraction/
│   │   ├── GptRecipeExtractionService.cs
│   │   └── FakeRecipeExtractionService.cs
│   ├── Ocr/
│   │   └── ImagePreprocessor.cs
│   ├── Persistence/
│   │   └── AppDbContext.cs
│   ├── Repositories/
│   │   ├── RecipeRepository.cs
│   │   └── ImageAssetRepository.cs
│   └── Storage/
│       └── LocalImageStorage.cs
│
└── ScreenShotRecipe.Web/               # Blazor Server + APIs
    ├── Program.cs                      # DI configuration + Minimal APIs
    ├── Pages/
    │   ├── Recipes.razor               # Recipe list
    │   ├── RecipeDetail.razor          # View/Print/Delete
    │   ├── RecipeEdit.razor            # Edit recipe
    │   └── Import.razor                # Multi-image upload
    └── Shared/
        ├── MainLayout.razor
        └── NavMenu.razor

tests/
└── ScreenShotRecipe.Tests/
    └── ImportServiceTests.cs
```

**Structure Decision**: Clean Architecture with 5 projects following constitution requirements. Domain contains entities and interfaces, Application contains use-case services, Infrastructure implements external integrations (GPT-4o, SQLite, local storage), Web hosts Blazor UI and Minimal APIs.

## Complexity Tracking

| Aspect | Justification |
|--------|---------------|
| **5 projects** | Required by constitution (Domain/Application/Contracts/Infrastructure/Web) |
| **Repository pattern** | Mandated for persistence abstraction (constitution §5, §8) |
| **GPT-4o multimodal** | Research finding: superior combined OCR + parsing accuracy, documented in research.md |

## Phase Outputs

### Phase 0: Research ✅
- **Output**: [research.md](research.md)
- **Key Decisions**:
  - GPT-4o multimodal replaces Azure Vision OCR
  - Server-side image preprocessing with SixLabors.ImageSharp
  - Cost: ~$4-5/month for typical family usage

### Phase 1: Design ✅
- **Output**: [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)
- **Key Artifacts**:
  - Entity definitions: Recipe, Ingredient, Step, ImageAsset, ImportJob
  - Interface contracts: `IRecipeExtractionService`, `IRecipeRepository`, `IImageStorage`
  - DTOs: `RecipeDto`, `ImportResultDto`, `IngredientDto`

### Phase 2: Implementation ✅
- **Output**: [tasks.md](tasks.md)
- **Status**: All core tasks complete
  - ✅ Import workflow (single-button multi-image upload)
  - ✅ GPT-4o extraction service
  - ✅ Blazor UI (list, view, edit, import)
  - ✅ Delete recipe with confirmation
  - ✅ Print recipe (clean printable output)
  - ✅ Navigation contrast fix

## Running the Application

```powershell
# Development mode (loads user secrets for Azure OpenAI)
.\Helpers\RunWeb.ps1

# Or manually:
cd src/ScreenShotRecipe.Web
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run
```

**URL**: http://localhost:5000

## Configuration

**User Secrets** (required for real GPT-4o):
```powershell
dotnet user-secrets set "AzureOpenAI:Endpoint" "https://your-resource.openai.azure.com/"
dotnet user-secrets set "AzureOpenAI:ApiKey" "your-api-key"
dotnet user-secrets set "AzureOpenAI:DeploymentName" "gpt-4o-2"
dotnet user-secrets set "ServiceImplementation:UseRealExtractionService" "true"
```

**Fake Services** (no API required):
```json
{
  "ServiceImplementation": {
    "UseRealExtractionService": false
  }
}
```
