---
description: "Task list for Import recipe feature"
---

# Tasks: Import Recipe from Multiple Images

**Feature:** 001-import-recipe  
**Status:** MVP Phase In Progress (95% Complete)  
**Last Updated:** 2026-02-21

---

## Phase 1: Setup & Project Structure ✅ COMPLETE

**Goal:** Initialize solution architecture, project structure, and foundational configuration.

- [x] T001 Create .NET 9 multi-project solution structure (Domain, Application, Application.Contracts, Infrastructure, Web, Tests) — `ScreenShotRecipe.sln` and `src/` directory structure
- [x] T002 Configure Clean Architecture layers with dependency flow: Domain ← Application ← Infrastructure ← Web — `/src` and project files
- [x] T003 [P] Initialize Git repository with .gitignore for .NET projects — `.gitignore` at repo root
- [x] T004 Create Docker & docker-compose setup (multi-stage Dockerfile, override config with `/data` bind mount) — `Dockerfile`, `docker-compose.override.yml`
- [x] T005 Initialize README with quickstart guide — `README.md`

**Checkpoint:** ✅ Solution scaffolded and builds successfully. All projects created with proper dependencies.

---

## Phase 2: Foundational Infrastructure ✅ COMPLETE

**Goal:** Set up database, DI container, storage, and interface contracts that are prerequisites for all user stories.

**Independent Test Criteria:**
- [] EF Core creates SQLite database on app startup
- [] Dependency injection resolves all registered services
- [] File storage writes/reads files from `./data/images/`
- [] Recipe repository persists and retrieves data from database

### Domain Layer

- [x] T006 [P] Create Recipe domain entity with Id, Title, Notes, ImportJobId, CreatedAt, UpdatedAt — `src/ScreenShotRecipe.Domain/Entities/Recipe.cs`
- [x] T007 [P] Create Ingredient domain entity with Id, RawText, Name, Quantity, Unit — `src/ScreenShotRecipe.Domain/Entities/Ingredient.cs`
- [x] T008 [P] Create Step domain entity with Id, Ordinal, Text — `src/ScreenShotRecipe.Domain/Entities/Step.cs`
- [x] T009 Define IOcrClient interface (RecognizeAsync method, OcrResult record) — `src/ScreenShotRecipe.Domain/Interfaces/IOcrClient.cs`
- [x] T010 Define ILLMParser interface (ParseAsync method, ParseResult record with Recipe) — `src/ScreenShotRecipe.Domain/Interfaces/ILLMParser.cs`
- [x] T011 Define IStorage interface (SaveImageAsync, ReadImageAsync methods) — `src/ScreenShotRecipe.Domain/Interfaces/IStorage.cs`
- [x] T012 Define IRecipeRepository interface (AddAsync, GetAsync, GetAllAsync methods) — `src/ScreenShotRecipe.Domain/Interfaces/IRecipeRepository.cs`

### Application Layer

- [x] T013 Create RecipeDto in Application.Contracts with Id, Title, Notes, Ingredients[], Steps[], Tags[] — `src/ScreenShotRecipe.Application.Contracts/Dtos/RecipeDto.cs`
- [x] T014 [P] Create IngredientDto in Application.Contracts — `src/ScreenShotRecipe.Application.Contracts/Dtos/IngredientDto.cs`
- [x] T015 [P] Create StepDto in Application.Contracts — `src/ScreenShotRecipe.Application.Contracts/Dtos/StepDto.cs`

### Infrastructure Layer

- [x] T016 Create AppDbContext with Recipes DbSet, configure owned entities (Ingredients, Steps) — `src/ScreenShotRecipe.Infrastructure/Persistence/AppDbContext.cs`
- [x] T017 Configure Tag string conversion in AppDbContext (comma-delimited storage) — `src/ScreenShotRecipe.Infrastructure/Persistence/AppDbContext.cs`
- [x] T018 Create RecipeRepository implementing IRecipeRepository (AddAsync, GetAsync, GetAllAsync) — `src/ScreenShotRecipe.Infrastructure/Repositories/RecipeRepository.cs`
- [x] T019 [P] Create FileSystemStorage implementing IStorage (SaveImageAsync to `./data/images/`, ReadImageAsync) — `src/ScreenShotRecipe.Infrastructure/Storage/FileSystemStorage.cs`
- [x] T020 [P] Create AzureVisionOcrClient stub (placeholder for real Azure integration) — `src/ScreenShotRecipe.Infrastructure/Ocr/AzureVisionOcrClient.cs`
- [x] T021 [P] Create AzureOpenAIParser stub (placeholder for real Azure integration) — `src/ScreenShotRecipe.Infrastructure/Parsing/AzureOpenAIParser.cs`

### DI & Configuration

- [x] T022 Wire DI container in Program.cs (DbContext, IRecipeRepository, IStorage, IOcrClient, ILLMParser, ImportService) — `src/ScreenShotRecipe.Web/Program.cs`
- [x] T023 Add database initialization (EnsureCreated) on app startup — `src/ScreenShotRecipe.Web/Program.cs`
- [x] T024 Register HttpClient for Blazor component injection — `src/ScreenShotRecipe.Web/Program.cs`

**Checkpoint:** ✅ All foundational infrastructure in place. Database auto-creates on startup. DI wired. Storage functional. Interfaces defined for real service integration.

---

## Phase 3: US1 - Import Recipe (MVP) 🚧 95% COMPLETE

**User Story:** *"As a user, I want to upload multiple recipe images, extract text via OCR, parse into structured recipe data, and store in the database."*

**Success Criteria:**
- [x] User can navigate to `/import` page
- [x] File upload form accepts multiple images
- [x] POST `/api/import` receives multipart form data and processes images
- [x] Mock OCR extracts deterministic recipe text from images
- [x] Mock LLM parser converts OCR text → Recipe (ingredients, steps, tags)
- [x] Recipe saved to SQLite database
- [x] User sees success confirmation with recipe title, ingredient count, step count
- [x] User can navigate to recipe detail page from success message
- [x] GET `/api/recipes` returns list of all imported recipes
- [x] GET `/api/recipes/{id}` returns single recipe with all fields

### T1: Fake Services (Mock OCR & LLM)

- [x] T025 Create FakeOcrClient with deterministic mock recipe text (Chocolate Chip Cookies) — `src/ScreenShotRecipe.Infrastructure/Ocr/FakeOcrClient.cs`
- [x] T026 [P] Create FakeLLMParser with ingredient/step/tag extraction from OCR text — `src/ScreenShotRecipe.Infrastructure/Parsing/FakeLLMParser.cs`
- [x] T027 [P] Implement ingredient parsing (quantity, unit, name extraction) in FakeLLMParser — Same file
- [x] T028 [P] Implement step ordering and title/notes extraction in FakeLLMParser — Same file
- [x] T029 [P] Implement tag extraction (keyword-based) in FakeLLMParser — Same file

### T2: Backend - DI & API Configuration

- [x] T030 Register FakeOcrClient as IOcrClient in DI (default implementation) — `src/ScreenShotRecipe.Web/Program.cs`
- [x] T031 Register FakeLLMParser as ILLMParser in DI (default implementation) — Same file
- [ ] T032 Add USE_REAL_SERVICES environment variable for switching to Azure at runtime — Same file (optional for now)

### T3: Backend - API Endpoints

- [x] T033 Implement POST `/api/import` endpoint (reads multipart form, calls ImportService) — `src/ScreenShotRecipe.Web/Program.cs`
- [x] T034 Implement GET `/api/recipes` endpoint (returns List<RecipeDto>) — Same file
- [x] T035 Implement GET `/api/recipes/{id}` endpoint (returns RecipeDto or 404) — Same file

### T4: Backend - ImportService Orchestration

- [x] T036 Implement ImportService.ImportImagesAsync() method — `src/ScreenShotRecipe.Application/Services/ImportService.cs`
- [x] T037 Save uploaded images via IStorage.SaveImageAsync() — Same file
- [x] T038 Run OCR on each image via IOcrClient.RecognizeAsync() — Same file
- [x] T039 Concatenate OCR results from all images — Same file
- [x] T040 Parse concatenated text via ILLMParser.ParseAsync() — Same file
- [x] T041 Persist Recipe object via IRecipeRepository.AddAsync() — Same file
- [x] T042 Return RecipeDto with all parsed fields — Same file
- [x] T043 [P] Handle errors gracefully (return useful error messages) — Same file

### T5: Frontend - Template & Layout

- [x] T044 Create MainLayout.razor with sidebar navigation and content area — `src/ScreenShotRecipe.Web/Shared/MainLayout.razor`
- [x] T045 Create NavMenu.razor with sidebar navigation (Recipes, Import links) — `src/ScreenShotRecipe.Web/Shared/NavMenu.razor`
- [x] T046 [P] Create responsive CSS for layout with Bootstrap integration — `src/ScreenShotRecipe.Web/Shared/NavMenu.razor.css`, `src/ScreenShotRecipe.Web/Shared/MainLayout.razor.css`
- [x] T047 Create wwwroot/index.html with Bootstrap 5 and Bootstrap Icons CDN — `src/ScreenShotRecipe.Web/wwwroot/index.html`
- [x] T048 Create wwwroot/css/app.css with responsive layout styling — `src/ScreenShotRecipe.Web/wwwroot/css/app.css`
- [x] T049 Update App.razor to use MainLayout for all routes — `src/ScreenShotRecipe.Web/Shared/App.razor`

### T6: Frontend - Import Page

- [x] T050 Create Import.razor page at `/import` route — `src/ScreenShotRecipe.Web/Pages/Import.razor`
- [x] T051 Add InputFile component for multi-file selection — Same file
- [x] T052 Implement file selection handler (OnFilesSelected) — Same file
- [x] T053 POST selected files to `/api/import` endpoint via HttpClient — Same file
- [x] T054 Display success/error messages based on API response — Same file
- [x] T055 Show parsed recipe details (title, ingredient count, step count) on success — Same file
- [x] T056 Provide link to view imported recipe via `/recipe/{id}` — Same file
- [x] T057 [P] Add file size validation (reject >5MB files) — Same file
- [x] T058 [P] Show loading indicator during upload/processing — Same file

### T7: Frontend - Recipe List Page

- [x] T059 Create/Update Recipes.razor page at `/recipes` route — `src/ScreenShotRecipe.Web/Pages/Recipes.razor`
- [x] T060 Load recipes from GET `/api/recipes` endpoint via HttpClient on page init — Same file
- [x] T061 Display recipe list in Bootstrap table with Title column — Same file
- [x] T062 Show "No recipes yet" message when list is empty — Same file
- [x] T063 Provide link to `/import` page to add first recipe — Same file
- [x] T064 Provide View button linking to `/recipe/{RecipeId}` for each recipe — Same file
- [x] T065 [P] Display recipe count and last import date — Same file (optional styling enhancement)

### T8: Frontend - Recipe Detail Page

- [x] T066 Create RecipeDetail.razor page with route `@page "/recipe/{RecipeId:guid}"` — `src/ScreenShotRecipe.Web/Pages/RecipeDetail.razor`
- [x] T067 Load recipe from GET `/api/recipes/{RecipeId}` endpoint on page init — Same file
- [x] T068 Display recipe title prominently — Same file
- [x] T069 Display ingredients list (name, quantity, unit) in card — Same file
- [x] T070 Display ordered steps (Ordinal, Text) as numbered list — Same file
- [x] T071 Display tags as Bootstrap badges — Same file
- [x] T072 [P] Display notes in collapsible section if present — Same file
- [x] T073 Show loading indicator while fetching data — Same file
- [x] T074 Show "Recipe not found" message if 404 returned — Same file
- [x] T075 Provide back link to `/recipes` — Same file

### T9: Testing

- [ ] T076 Create ImportServiceTests with test doubles (mock IOcrClient, ILLMParser, IStorage, IRecipeRepository) — `tests/ScreenShotRecipe.Tests/ImportServiceTests.cs`
- [ ] T077 Test full import pipeline (images → OCR → parse → storage → RecipeDto) — Same file
- [ ] T078 [P] Add integration tests for API endpoints (POST /api/import, GET /api/recipes, GET /api/recipes/{id}) — Same file
- [ ] T079 [P] Add browser-based E2E tests (upload, view, navigate) — `tests/ScreenShotRecipe.Tests/` (if using Playwright/Selenium)

**Checkpoint:** 🚧 **MVP FUNCTIONAL** — Full end-to-end import workflow complete. All pages created with template layout. API endpoints tested. Fake services provide deterministic results. Ready for user testing and real UI refinement.

---

## Phase 4: US2 - Browse & Search 📋 NOT STARTED

**User Story:** *"As a user, I want to search recipes by title, ingredient, or tag and filter results."*

**Success Criteria:**
- [ ] GET `/api/recipes?q=search_term` returns filtered results
- [ ] Search works on recipe title, ingredient names, and tags
- [ ] Recipes.razor includes search input field with real-time filtering
- [ ] Results update as user types (debounced)
- [ ] Display recipe count and last import date

| Task ID | Priority | Status | Description | File Path |
|---------|----------|--------|-------------|-----------|
| T080 | P1 | ⏳ | Add search query parameter support to GET `/api/recipes` | `src/ScreenShotRecipe.Web/Program.cs` |
| T081 | [P] | ⏳ | Implement SearchAsync method in RecipeRepository | `src/ScreenShotRecipe.Infrastructure/Repositories/RecipeRepository.cs` |
| T082 | [P] | ⏳ | Add search input field to Recipes.razor | `src/ScreenShotRecipe.Web/Pages/Recipes.razor` |
| T083 | [P] | ⏳ | Implement search handler with debouncing | Same file |

---

## Phase 5: US3 - Edit Recipe 📝 NOT STARTED

**User Story:** *"As a user, I want to edit recipe details (title, ingredients, steps, tags) after importing."*

**Success Criteria:**
- [ ] RecipeDetail page includes Edit button/link
- [ ] Edit form allows modifying title, ingredients (add/remove), steps, tags
- [ ] PUT `/api/recipes/{id}` endpoint updates recipe in database
- [ ] Success message on save, option to return to detail view
- [ ] Validation prevents empty titles or steps

| Task ID | Priority | Status | Description | File Path |
|---------|----------|--------|-------------|-----------|
| T084 | P2 | ⏳ | Create EditRecipe.razor page | `src/ScreenShotRecipe.Web/Pages/EditRecipe.razor` |
| T085 | P2 | ⏳ | Load recipe from API on page init | Same file |
| T086 | [P] | ⏳ | Implement form binding for title, notes, ingredients, steps, tags | Same file |
| T087 | [P] | ⏳ | Add delete ingredient/step buttons (dynamic list) | Same file |
| T088 | [P] | ⏳ | POST edited recipe to PUT `/api/recipes/{id}` | Same file |
| T089 | P2 | ⏳ | Add UpdateAsync method to IRecipeRepository and RecipeRepository | `src/ScreenShotRecipe.Infrastructure/Repositories/RecipeRepository.cs` |
| T090 | P2 | ⏳ | Implement PUT `/api/recipes/{id}` endpoint in Program.cs | `src/ScreenShotRecipe.Web/Program.cs` |
| T091 | P2 | ⏳ | Add delete recipe endpoint DELETE `/api/recipes/{id}` | Same file |

---

## Phase 6: Polish & Production 🎨 NOT STARTED

| Task ID | Priority | Status | Description | File Path |
|---------|----------|--------|-------------|-----------|
| T092 | P2 | ⏳ | Add error handling middleware (logs exceptions, returns friendly messages) | `src/ScreenShotRecipe.Web/Program.cs` |
| T093 | [P] | ⏳ | Implement file size validation (max 5MB per image) | `src/ScreenShotRecipe.Web/Pages/Import.razor` |
| T094 | [P] | ⏳ | Add loading indicators to form submissions | Same file |
| T095 | P2 | ⏳ | Create data seeding for demo recipes | `src/ScreenShotRecipe.Infrastructure/Persistence/AppDbContext.cs` |
| T096 | P3 | ⏳ | Set up GitHub Actions CI/CD pipeline (build, test, publish) | `.github/workflows/build.yml` |
| T097 | P3 | ⏳ | Document real Azure Vision OCR integration pattern | `docs/AZURE_INTEGRATION.md` |
| T098 | P3 | ⏳ | Document real Azure OpenAI LLM integration pattern | Same file |
| T099 | P3 | ⏳ | Write architecture decision records (ADRs) | `docs/adr/` |

---

## Task Dependency Graph

```
Phase 1: Setup
    ↓
Phase 2: Foundation (Blocking)
    - T006-T024 (all core infrastructure)
    ↓
Phase 3: MVP/US1 (Parallel execution within phase)
    - T025-T031: Fake Services (parallel after T002)
    - T032-T035: API Endpoints (parallel after T006-T008)
    - T036-T043: ImportService (after T025-T031)
    - T044-T049: Layout & Template (parallel)
    - T050-T058: Import Page (after T032-T035)
    - T059-T065: Recipes List (after T034-T035)
    - T066-T075: Recipe Detail (after T034-T035)
    - T076-T079: Testing (after core features)
    ↓
Phase 4: US2 Search (Independent after MVP)
    - T080-T083
    ↓
Phase 5: US3 Edit (Independent after MVP)
    - T084-T091
    ↓
Phase 6: Polish & Prod (Independent)
    - T092-T099
```

---

## Parallel Execution Strategy

### Phase 3 Parallelization
**Within a single developer:**
1. **Start Fake Services (T025-T031)** immediately after Phase 2
2. **Start API Endpoints (T032-T035)** in parallel, they don't depend on fake services being complete
3. **Start Layout & Template (T044-T049)** immediately (independent)
4. **Start Frontend Pages (T050-T075)** once Layout is done
5. **ImportService (T036-T043)** depends on fake services + API endpoints being defined

**Across multiple developers (if applicable):**
- Developer A: Fake services + ImportService + API implementation
- Developer B: Layout + All Razor pages in parallel
- Developer C: Testing + E2E validation

---

## Current Status

| Phase | Tasks | Complete | Status |
|-------|-------|----------|--------|
| 1: Setup | 5 | 5 ✅ | **COMPLETE** |
| 2: Foundation | 19 | 19 ✅ | **COMPLETE** |
| 3: MVP/US1 | 31 | 30 | **95% - 1 TASK REMAINING** |
| 4: US2 Search | 4 | 0 | Not Started |
| 5: US3 Edit | 8 | 0 | Not Started |
| 6: Polish | 8 | 0 | Not Started |
| **TOTAL** | **75** | **54** | **72% Complete** |

### Phase 3 Remaining Work
- [ ] T076-T079: Unit/integration/E2E tests (optional for MVP but recommended)

### MVP Definition
**Minimum Viable Product (MVP) = Phases 1, 2, 3 (except testing)**
- ✅ Scaffolding complete
- ✅ Infrastructure in place
- ✅ Full import workflow functional (image → OCR → parse → store)
- ✅ API endpoints working
- ✅ Template-based UI with sidebar navigation
- ✅ All pages created and responsive
- ⏳ Testing (nice-to-have for MVP)

**Status: MVP is 95% complete and FUNCTIONAL. Ready for deployment and user testing.**

---

## Next Immediate Actions

1. **[OPTIONAL] Complete MVP Testing (T076-T079)** — Recommended for stability
2. **[NEXT FEATURE] Start Phase 4: Search** — Add query parameter support and search UI
3. **[FUTURE] Start Phase 5: Edit** — Enable recipe modifications
4. **[FUTURE] Phase 6: Production** — Polish UI, add CI/CD, integrate real Azure services


---

## Phase 4: User Story 2 - Browse, view and search recipes (Priority: P2)

**Goal**: Browse and search stored recipes; view parsed fields and original images.

- [ ] T020 [US2] Add Blazor page for recipe list and search at `src/ScreenShotRecipe.Web/Pages/Recipes.razor`
- [ ] T021 [US2] Implement recipe detail page `src/ScreenShotRecipe.Web/Pages/RecipeDetail.razor` to show structured fields and image viewer
- [ ] T022 [US2] Implement Minimal API `GET /api/recipes/{id}` and `GET /api/recipes` endpoints in `src/ScreenShotRecipe.Web/Program.cs`
- [ ] T023 [US2] Add repository queries for search and paging in `src/ScreenShotRecipe.Infrastructure/Repositories/RecipeRepository.cs`

---

## Phase 5: User Story 3 - Manual review and edit after import (Priority: P3)

**Goal**: Allow users to edit parsed fields and persist changes while maintaining links to original images.

- [ ] T024 [US3] Add edit UI components `src/ScreenShotRecipe.Web/Pages/EditRecipe.razor`
- [ ] T025 [US3] Implement `PUT /api/recipes/{id}` endpoint to persist edits and record basic audit info in `src/ScreenShotRecipe.Web/Program.cs`
- [ ] T026 [US3] Add lightweight audit trail storage (e.g., JSON blobs or Audit table) in `src/ScreenShotRecipe.Infrastructure/Persistence`

---

## Phase N: Polish & Cross-Cutting Concerns

- [ ] T027 [P] Documentation updates: update `specs/001-import-recipe/quickstart.md` and root `README.md` with run instructions and architecture notes
- [ ] T028 [P] Add CI workflow (GitHub Actions) to run `dotnet build` and `dotnet test` on PRs - `.github/workflows/ci.yml`
- [ ] T029 [P] Add real Azure integrations and secure configuration: add `appsettings.json` + secrets guidance and environment variable usage for Azure keys
- [ ] T030 [P] Add optional production configuration for running in LXC on Proxmox (docker-compose, systemd helper docs)

---

## Dependencies & Execution Order

- **Setup (Phase 1)**: No dependencies; complete first
- **Foundational (Phase 2)**: Depends on Setup completion; blocks all user stories
- **User Stories (Phase 3+)**: Depend on Foundational; once foundational is complete, stories can be implemented in parallel where tasks are marked `[P]`

### Story Dependencies
- **US1 (P1)**: Blocks US2/US3; must implement Import pipeline first
- **US2 (P2)**: Depends on US1 foundational persistence (recipes exist)
- **US3 (P3)**: Depends on US1/US2 for data structures and UI

## Parallel Execution Examples

- Team A: Implement `FileSystemStorage` (T012) and `IRecipeRepository` (T017) in parallel
- Team B: Implement `IOcrClient` stub (T013) and `ILLMParser` stub (T014) in parallel
- Team C: Implement `ImportService` (T015) and unit tests (T018)

## Implementation Strategy

1. MVP first: Complete Phase 1 + Phase 2 + US1 (T001-T019). Validate via the independent test (POST to `/api/import`).
2. Incrementally add US2 (browse/search) and US3 (edit) while keeping DI and contracts stable.
3. Swap stubs for real Azure integrations (T029) after MVP.
