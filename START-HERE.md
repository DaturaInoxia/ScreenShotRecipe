# 🚀 ScreenShotRecipe: Implementation Ready

**Project:** Import recipes from multiple images using OCR + LLM  
**Status:** Phase 1-2 Complete ✅ | Phase 3 MVP In Progress 🚧 | Phase 2-Real Ready 📋  
**Last Updated:** 2026-02-22

---

## 📊 Project Status Dashboard

| Phase | Status | Effort | Estimated<br>Completion |
|-------|--------|--------|----------|
| **Phase 1:** Setup & Project Structure | ✅ COMPLETE | 5h | 2026-02-21 |
| **Phase 2:** Foundational Infrastructure | ✅ COMPLETE | 19h | 2026-02-21 |
| **Phase 3a:** Fake Services & Config | ✅ COMPLETE | 6h | 2026-02-22 |
| **Phase 3b:** Backend API & Orchestration | ✅ COMPLETE | 7h | 2026-02-22 |
| **Phase 3c:** Frontend UI & Pages | ✅ COMPLETE | 9h | 2026-02-22 |
| **Phase 3d:** Testing (MVP) | ⏳ IN PROGRESS | 9-10h | 2026-02-25 |
| **Phase 2-Real:** Production GPT-4o | 📋 READY | 32-57h | 2026-03-15 |
| **Phase 4:** Search Feature | 📋 NOT STARTED | 4h | 2026-03-22 |
| **Phase 5:** Edit Recipe | 📋 NOT STARTED | 8h | 2026-03-29 |

**CRITICAL PATH:** Phase 3d Testing blocks MVP release ← **START HERE**

---

## 🎯 What's Working Right Now

✅ **Application compiles** without errors  
✅ **Database layer:** EF Core configured, SQLite local migration  
✅ **Dependency injection:** All services registered and testable  
✅ **Fake OCR:** FakeRecipeOcrService with 3 deterministic recipes  
✅ **Fake LLM:** FakeLLMParser extracts ingredients/steps/tags realistically  
✅ **API endpoints:** POST /api/import, GET /api/recipes, GET /api/recipes/{id}  
✅ **UI pages:** Import form, recipe list, recipe detail view  
✅ **Configuration:** ServiceImplementationOptions with env var switching  

**What You Can Do Today:**
1. Upload 1-6 recipe images via `/import` page
2. System extracts text, parses into Recipe
3. Browse all recipes at `/recipes`
4. View individual recipe details

**Cost:** $0 (uses fake services, no external APIs)

---

## 📋 What Needs to Be Done (Priority Order)

### 1️⃣ IMMEDIATE (This Week): Phase 3 MVP Completion

**Duration:** 1-2 weeks | **Effort:** 12-13 hours  
**Owner:** QA/Test Lead  
**Blocker:** Prevents MVP release

**What to Do:**
- [ ] Write unit tests for ImportService (4-5 hours)
- [ ] Write integration tests for API endpoints (3-4 hours)
- [ ] Manual smoke testing (2-3 hours)
- [ ] Fix any discovered bugs

**Expected Outcome:** MVP passes all tests, ready for production ✅

**Reference:** [PHASE-3-SPRINT-CHECKLIST.md](PHASE-3-SPRINT-CHECKLIST.md)

---

### 2️⃣ CONCURRENT (Weeks 2-3): Phase 2-Real Implementation

**Duration:** 2-3 weeks | **Effort:** 32-57 hours  
**Owner:** Backend Team  
**Blocker:** None (parallel with Phase 3d)

**What to Do:**
1. Build image preprocessing pipeline (4-7 hours)
2. Implement GPT-4o vision client (8-12 hours)
3. Add retry logic and cost tracking (2-3 hours)
4. Wire DI configuration for real/fake switching (2-3 hours)
5. Write tests and documentation (5-8 hours)

**Expected Outcome:** Production-grade GPT-4o OCR ready ✅

**Reference:** [PHASE-2-REAL-IMPLEMENTATION.md](PHASE-2-REAL-IMPLEMENTATION.md) | [PHASE-2-REAL-STARTER.md](PHASE-2-REAL-STARTER.md)

---

### 3️⃣ OPTIONAL (Week 4): Phase 3 Extensions

**Duration:** 1-2 weeks | **Effort:** 12-16 hours  
**Owner:** Frontend Team

**Features:**
- Search recipes by title/ingredient (4 hours)
- Edit recipes after import (8 hours)
- Polish UI/UX (4 hours)

**Reference:** Existing `tasks.md`

---

## 🏗️ Architecture at a Glance

```
┌─────────────────────────────────────────────────────────┐
│                  Blazor Server UI Layer                  │
│  Pages: Import, Recipes, RecipeDetail, EditRecipe (future)
└────────────────┬────────────────────────────────────────┘
                 │ HttpClient
┌────────────────V────────────────────────────────────────┐
│              Minimal API Layer (Web)                     │
│  POST /api/import, GET /api/recipes, GET /api/recipes/{id}
└────────────────┬────────────────────────────────────────┘
                 │ IRecipeOcrService
                 │ ILLMParser
                 │ IRecipeRepository
                 │ IStorage
┌────────────────V────────────────────────────────────────┐
│          Application Layer (Services)                    │
│  ImportService: orchestrates OCR → parse → store pipeline
└────────────────┬────────────────────────────────────────┘
                 │
┌────────────────V────────────────────────────────────────┐
│        Infrastructure Layer (Implementations)            │
│                                                          │
│  Ocr Clients:                                           │
│  ├─ FakeRecipeOcrService (deterministic for dev)       │
│  └─ GptFourOOcrClient (real GPT-4o, Phase 2-Real)      │
│                                                          │
│  Parsers:                                               │
│  ├─ FakeLLMParser (deterministic for dev)              │
│  └─ RealLLMParser (Phase 2-Real)                       │
│                                                          │
│  Storage:                                               │
│  ├─ FileSystemStorage (local ./data/images/)           │
│  └─ S3Storage (future)                                 │
│                                                          │
│  Database:                                              │
│  └─ AppDbContext (EF Core + SQLite)                    │
└────────────────┬────────────────────────────────────────┘
                 │
┌────────────────V────────────────────────────────────────┐
│            Domain Layer (Entities)                       │
│  Recipe, Ingredient, Step, ImportJob, ImageAsset        │
└─────────────────────────────────────────────────────────┘
```

**Key Pattern:** **All external services are injected via interfaces**
- Switching fake ↔ real is just configuration: `OCR_USE_REAL=true`
- No code changes needed to swap implementations

---

## 🗂️ Directory Structure

```
ScreenShotRecipe/
├── src/
│   ├── ScreenShotRecipe.Domain/              # Entities & Interfaces
│   │   ├── Entities/                         # Recipe, Ingredient, Step
│   │   └── Interfaces/                       # IRecipeOcrService, ILLMParser, etc.
│   │
│   ├── ScreenShotRecipe.Application/         # Business Logic
│   │   └── Services/ImportService.cs         # Main orchestration
│   │
│   ├── ScreenShotRecipe.Application.Contracts/  # DTOs for API
│   │   └── Dtos/
│   │
│   ├── ScreenShotRecipe.Infrastructure/      # Implementations
│   │   ├── Ocr/                              # FakeRecipeOcrService, GptFourOOcrClient
│   │   ├── Parsing/                          # FakeLLMParser
│   │   ├── Persistence/                      # EF Core context
│   │   ├── Repositories/                     # Recipe repository
│   │   ├── Storage/                          # FileSystemStorage
│   │   └── Config/                           # ServiceImplementationOptions
│   │
│   └── ScreenShotRecipe.Web/                 # UI & API Layer
│       ├── Program.cs                        # DI configuration
│       ├── Pages/                            # Razor pages (Import, Recipes, RecipeDetail)
│       ├── Shared/                           # MainLayout, NavMenu
│       └── wwwroot/                          # CSS, JS, static assets
│
├── tests/
│   └── ScreenShotRecipe.Tests/
│       ├── ImportServiceTests.cs             # Unit tests (Phase 3d)
│       └── GptFourOOcrClientTests.cs         # Unit tests (Phase 2-Real)
│
├── specs/001-import-recipe/
│   ├── plan.md                               # Phase 1 design overview
│   ├── spec.md                               # User stories & requirements
│   ├── research.md                           # Phase 0: GPT-4o decision + cost analysis
│   ├── data-model.md                         # Phase 1: Entity model
│   ├── contracts/                            # Phase 1: Interface specs
│   ├── quickstart.md                         # Phase 1: Getting started
│   ├── PHASE-2-REAL-IMPLEMENTATION.md        # Phase 2-Real: Full task breakdown
│   ├── tasks.md                              # Original task master list
│   └── FAKE-SERVICES-GUIDE.md              # How fake services work
│
├── IMPLEMENTATION-ROADMAP.md                 # ← START HERE
├── PHASE-3-SPRINT-CHECKLIST.md              # Next 1-2 weeks
├── PHASE-2-REAL-STARTER.md                  # Parallel track
├── README.md                                 # Quick start guide
├── Dockerfile                                # Docker support
└── docker-compose.override.yml               # Local dev environment

```

---

## 🚀 Quick Start: Run Right Now

### Prerequisites
- .NET 9 SDK
- Visual Studio Code (optional but recommended)

### Steps

```powershell
# 1. Navigate to project
cd D:\src\ScreenShotRecipe

# 2. Build
dotnet build

# 3. Run
cd src/ScreenShotRecipe.Web
dotnet run

# 4. Open browser
# https://localhost:7290

# 5. Import a recipe
# - Click "Import" in sidebar
# - Upload 1-6 images (any format: JPG, PNG, etc.)
# - See parsed recipe appear
# - Click "Recipes" to browse all

# 6. Stop (Ctrl+C)
```

**Expected:** Application launches, UI loads, fake OCR works ($0 cost)

---

## 📚 Documentation Roadmap

**Start Here:**
1. [IMPLEMENTATION-ROADMAP.md](IMPLEMENTATION-ROADMAP.md) ← Overview & schedule
2. [PHASE-3-SPRINT-CHECKLIST.md](PHASE-3-SPRINT-CHECKLIST.md) ← What to do this week
3. [README.md](README.md) ← Project quick start

**For Phase 3 Testing:**
- Existing domain/infrastructure code
- Mock patterns from `FakeRecipeOcrService.cs`

**For Phase 2-Real (GPT-4o):**
1. [research.md](specs/001-import-recipe/research.md) ← Decision + cost analysis
2. [PHASE-2-REAL-IMPLEMENTATION.md](specs/001-import-recipe/PHASE-2-REAL-IMPLEMENTATION.md) ← Detailed tasks
3. [PHASE-2-REAL-STARTER.md](PHASE-2-REAL-STARTER.md) ← Getting started guide

**Reference:**
- [data-model.md](specs/001-import-recipe/data-model.md) ← Entity definitions
- [contracts/](specs/001-import-recipe/contracts/) ← Interface specifications
- [quickstart.md](specs/001-import-recipe/quickstart.md) ← Development patterns

---

## 🎓 Key Concepts

### Dependency Injection (DI)

All external services are injected, allowing easy swapping:

```csharp
// In Program.cs
if (serviceImplOptions.UseRealOcr)
    services.AddScoped<IRecipeOcrService, GptFourOOcrClient>();
else
    services.AddScoped<IRecipeOcrService, FakeRecipeOcrService>();

// Everywhere else in code
public class ImportService
{
    public ImportService(IRecipeOcrService ocr, ILLMParser parser, ...)
    {
        _ocr = ocr;  // Could be fake or real - code doesn't care!
    }
}
```

**Benefit:** Test with fake, deploy with real - NO CODE CHANGES

### Environment Variables for Configuration

```bash
# Development (default)
# Uses fake services, SQLite in ./data/

# Staging
OCR_USE_REAL=true
OPENAI_API_KEY=sk-...
# Uses real GPT-4o, same code

# Production
OCR_USE_REAL=true
OPENAI_API_KEY=sk-...
ASPNETCORE_ENVIRONMENT=Production
# Uses real GPT-4o, database in production
```

---

## 🔐 Security Notes

### API Keys
- **Never** commit `OPENAI_API_KEY` to git
- Use environment variables or user secrets (local dev)
- Use Azure Key Vault or AWS Secrets Manager (production)

### Example Local Dev Setup
```powershell
# Store key in user secrets (encrypted, local only)
dotnet user-secrets set "OpenAI:ApiKey" "sk-..."

# Runtime picks it up automatically
dotnet run
```

---

## 📈 Success Metrics

### MVP Release Criteria (Phase 3d)
```
✅ 10/10 checklist items passing
✅ Any imported recipe can be viewed
✅ No unhandled exceptions in logs
✅ Response time < 2 seconds for UI navigation
✅ All dependencies available (no NuGet errors)
```

### Production Release Criteria (Phase 2-Real)
```
✅ GPT-4o OCR extracts text correctly
✅ Cost per import < $1 (batch processing helps)
✅ Response time < 30 seconds for import
✅ 99% uptime with retry logic
✅ Cost tracking shows expected usage
```

---

## 🐛 Common Issues & Solutions

| Issue | Solution |
|-------|----------|
| Build fails with CS0117 errors | Already fixed! Run `dotnet clean && dotnet build` |
| App won't start (port in use) | Change port in `launchSettings.json` or kill existing process |
| Fake OCR returns wrong recipe | Expected - cycles through 3 hardcoded recipes per image count |
| Tests won't run | Run from `tests/ScreenShotRecipe.Tests` directory |
| NuGet package not found | Run `dotnet restore` to re-download packages |

---

## 🤝 Team Assignments

### Suggested Breakdown

**Team A (Backend/OCR - 2 people):**
- Already done: Phase 1-3a (fake services)
- Next: Phase 2-Real (GPT-4o implementation)
- Effort: 32-57 hours over 2-3 weeks

**Team B (Testing/QA - 1 person):**
- Next: Phase 3d (unit + integration tests for MVP)
- Effort: 9-10 hours over 1 week
- Blockers: None - ready to start immediately

**Team C (Frontend - optional, 1 person):**
- Next: Phase 4-6 (search, edit, polish)
- Effort: 20-30 hours over 3+ weeks

---

## ✅ Next Steps

### For Immediate Action (Today)
1. Read [IMPLEMENTATION-ROADMAP.md](IMPLEMENTATION-ROADMAP.md)
2. Assign Phase 3d testing to QA lead
3. Assign Phase 2-Real to backend team
4. Schedule daily standup

### For This Week
1. Complete Phase 3d testing (all tests passing)
2. Manual smoke test MVP
3. Fix any discovered bugs
4. **RELEASE MVP to staging** 🎉

### For Next 2 Weeks
1. Start Phase 2-Real (parallel: GPT-4o implementation)
2. Validate real OCR in staging
3. Complete Phase 4 (search) or Phase 2b (resilience)

---

## 📞 Communication

**Questions?**
- See [IMPLEMENTATION-ROADMAP.md](IMPLEMENTATION-ROADMAP.md) for schedule
- See [PHASE-3-SPRINT-CHECKLIST.md](PHASE-3-SPRINT-CHECKLIST.md) for this week's tasks
- See [PHASE-2-REAL-STARTER.md](PHASE-2-REAL-STARTER.md) for GPT-4o setup questions
- See [research.md](specs/001-import-recipe/research.md) for architecture decisions

---

## 🎯 Project Mission

> Build an application that lets users upload recipe screenshots or photos, automatically extract text using OCR, parse into structured recipes, and store with original images - all in a simple, beautiful UI with zero external dependencies for MVP and optional GPT-4o integration for production.

**Status:** ✅ MVP READY FOR TESTING  
**Target:** 🚀 PRODUCTION by 2026-03-15

---

**Last Updated:** 2026-02-22  
**Build Status:** ✅ Passing  
**Test Status:** ⏳ In Progress  
**Deployment Status:** 📋 Staging Ready
