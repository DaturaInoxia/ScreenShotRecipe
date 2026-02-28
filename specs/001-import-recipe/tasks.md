# Tasks: Import Recipe from Multiple Images

**Feature**: 001-import-recipe  
**Input**: Design documents from `/specs/001-import-recipe/`  
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

## Format: `[ID] [P?] [Story?] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

Project uses Clean Architecture with:
- `src/ScreenShotRecipe.Domain/` - Entities, Interfaces
- `src/ScreenShotRecipe.Application/` - Services
- `src/ScreenShotRecipe.Application.Contracts/` - DTOs
- `src/ScreenShotRecipe.Infrastructure/` - Implementations
- `src/ScreenShotRecipe.Web/` - Blazor Server UI
- `tests/ScreenShotRecipe.Tests/` - Unit/Integration tests

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Missing core entities and image preprocessing

- [X] T001 Create ImageAsset entity in src/ScreenShotRecipe.Domain/Entities/ImageAsset.cs
- [X] T002 [P] Create OcrResult entity in src/ScreenShotRecipe.Domain/Entities/OcrResult.cs
- [X] T003 [P] Create ImportJob entity with ImportJobStatus enum in src/ScreenShotRecipe.Domain/Entities/ImportJob.cs
- [X] T004 Update Recipe entity to add ImportJobId FK, OverallConfidence, ConfidenceNotes, Version in src/ScreenShotRecipe.Domain/Entities/Recipe.cs
- [X] T005 [P] Add SixLabors.ImageSharp package to Infrastructure project for image preprocessing

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Database schema and core infrastructure that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T006 Update AppDbContext with DbSets for ImageAsset, OcrResult, ImportJob in src/ScreenShotRecipe.Infrastructure/Persistence/AppDbContext.cs
- [X] T007 Configure entity relationships and indexes in AppDbContext (Recipe-ImportJob, ImageAsset-OcrResult, cascading deletes)
- [X] T008 [P] Create OcrResultDto in src/ScreenShotRecipe.Application.Contracts/Dtos/OcrResultDto.cs
- [X] T009 [P] Create RecipeImportRequestDto with RecipeImageFileDto in src/ScreenShotRecipe.Application.Contracts/Dtos/RecipeImportRequestDto.cs
- [X] T010 [P] Create RecipeImportResponseDto in src/ScreenShotRecipe.Application.Contracts/Dtos/RecipeImportResponseDto.cs
- [X] T011 Add IImageAssetRepository interface in src/ScreenShotRecipe.Domain/Interfaces/IImageAssetRepository.cs
- [X] T012 Add IImportJobRepository interface in src/ScreenShotRecipe.Domain/Interfaces/IImportJobRepository.cs
- [X] T013 [P] Implement ImageAssetRepository in src/ScreenShotRecipe.Infrastructure/Repositories/ImageAssetRepository.cs
- [X] T014 [P] Implement ImportJobRepository in src/ScreenShotRecipe.Infrastructure/Repositories/ImportJobRepository.cs
- [X] T015 Create ImagePreprocessor utility (resize, compress, format conversion) in src/ScreenShotRecipe.Infrastructure/Ocr/ImagePreprocessor.cs
- [X] T016 Register new repositories and ImagePreprocessor in src/ScreenShotRecipe.Web/Program.cs DI container

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Import Images and Receive Parsed Recipe (Priority: P1) 🎯 MVP

**Goal**: User uploads 1-6 images → single Import button → receives structured recipe with images stored

**Independent Test**: Upload 1-6 recipe images, confirm returned recipe has title, ingredients list (≥1), steps (≥1), and original images are retrievable via API

### Implementation for User Story 1

- [X] T017 [US1] Update ImportService to create ImportJob, store images via IStorage, track status transitions in src/ScreenShotRecipe.Application/Services/ImportService.cs
- [X] T018 [US1] Add ImportJob status tracking (Queued→OcrInProgress→ParsingInProgress→Succeeded/Failed) to ImportService
- [X] T019 [US1] Integrate ImagePreprocessor into ImportService to optimize images before extraction
- [X] T020 [US1] Add confidence metadata handling in ImportService (populate Recipe.OverallConfidence, ConfidenceNotes from extraction result)
- [X] T021 [US1] Update POST /api/import endpoint to accept multipart form data with multiple images in src/ScreenShotRecipe.Web/Program.cs
- [X] T022 [US1] Update /api/import endpoint response to return RecipeImportResponseDto (ImportJobId, status, parsed recipe)
- [X] T023 [US1] Add GET /api/images/{id} endpoint to retrieve stored original images in src/ScreenShotRecipe.Web/Program.cs
- [X] T024 [US1] Update Import.razor page to show upload progress indicator and import status
- [X] T025 [US1] Update Import.razor page to display parsed recipe result with confidence badge (green/yellow/red)
- [X] T026 [US1] Add image thumbnails to Import.razor result showing uploaded images linked to recipe
- [X] T027 [US1] Add retry logic with exponential backoff to ImportService for transient OCR/extraction failures
- [X] T028 [US1] Add error handling with ImportJob.ErrorMessage and Diagnostics for permanent failures

**Checkpoint**: User Story 1 complete - users can import recipe images and receive structured recipes

---

## Phase 4: User Story 2 - Browse, View and Search Recipes (Priority: P2)

**Goal**: User can browse saved recipes, search by title/ingredient/tag, open recipe to see details and original images

**Independent Test**: Create several recipes, search for "chocolate", verify matching recipes returned; open a recipe, verify fields and images display

### Implementation for User Story 2

- [X] T029 [US2] Add SearchAsync method to IRecipeRepository (query title, ingredient names, tags) in src/ScreenShotRecipe.Domain/Interfaces/IRecipeRepository.cs
- [X] T030 [US2] Implement SearchAsync in RecipeRepository with relevance ranking in src/ScreenShotRecipe.Infrastructure/Repositories/RecipeRepository.cs
- [X] T031 [US2] Add GET /api/recipes/search?q={query} endpoint in src/ScreenShotRecipe.Web/Program.cs
- [X] T032 [US2] Update Recipes.razor page with search input box and search button in src/ScreenShotRecipe.Web/Pages/Recipes.razor
- [X] T033 [US2] Add search results display with recipe cards (title, tags as pills, confidence badge, created date)
- [X] T034 [US2] Update RecipeDetail.razor to display all recipe fields with proper formatting (ingredients with qty/unit, numbered steps)
- [X] T035 [US2] Add image gallery to RecipeDetail.razor showing original imported images as thumbnails
- [X] T036 [US2] Add image lightbox/modal component for viewing full-size images
- [X] T037 [US2] Add loading states and empty state messages ("No recipes found") to Recipes.razor and RecipeDetail.razor

**Checkpoint**: User Story 2 complete - users can browse and search recipes, view full details with images

---

## Phase 5: User Story 3 - Manual Review and Edit After Import (Priority: P3)

**Goal**: User can edit recipe fields (title, ingredients, steps, tags) and save changes while keeping original images

**Independent Test**: Import a recipe, edit title and add an ingredient, save; verify changes persist and appear in search

### Implementation for User Story 3

- [X] T038 [US3] Add UpdateAsync method to IRecipeRepository with version increment in src/ScreenShotRecipe.Domain/Interfaces/IRecipeRepository.cs
- [X] T039 [US3] Implement UpdateAsync in RecipeRepository with optimistic concurrency check in src/ScreenShotRecipe.Infrastructure/Repositories/RecipeRepository.cs
- [X] T040 [US3] Add PUT /api/recipes/{id} endpoint for recipe updates in src/ScreenShotRecipe.Web/Program.cs
- [X] T041 [US3] Create RecipeEditDto for update requests in src/ScreenShotRecipe.Application.Contracts/Dtos/RecipeEditDto.cs
- [X] T042 [US3] Create RecipeEdit.razor page with editable form in src/ScreenShotRecipe.Web/Pages/RecipeEdit.razor
- [X] T043 [US3] Implement title editing with validation (required, max 500 chars) in RecipeEdit.razor
- [X] T044 [US3] Implement ingredients editing (add, remove, reorder, edit quantity/unit/name) with inline forms
- [X] T045 [US3] Implement steps editing (add, remove, reorder, edit text) with inline forms
- [X] T046 [US3] Implement tags editing (add tag pill, remove tag) with autocomplete
- [X] T047 [US3] Add Save and Cancel buttons with unsaved changes confirmation dialog
- [X] T048 [US3] Add low-confidence field highlighting in edit form (yellow border for confidence < 0.7)
- [X] T049 [US3] Increment Recipe.Version on save and show concurrency conflict error if version mismatch

**Checkpoint**: User Story 3 complete - users can edit and correct parsed recipes

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [X] T050 [P] Add DataAnnotations validation to all DTOs per validation rules in contracts/DTOs-and-Contracts.md
- [X] T051 [P] Add structured logging throughout ImportService, repositories, and extraction services using Serilog
- [X] T052 [P] Add GET /health endpoint for API health check in src/ScreenShotRecipe.Web/Program.cs
- [X] T056 [P] Add DELETE /api/recipes/{id} endpoint in src/ScreenShotRecipe.Web/Program.cs
- [X] T057 [P] Add delete button with confirmation modal to RecipeDetail.razor in src/ScreenShotRecipe.Web/Pages/RecipeDetail.razor
- [X] T058 [P] Add print recipe functionality with clean printable output (hide nav/buttons) in RecipeDetail.razor
- [X] T059 [P] Add print CSS styles (@media print) to hide app chrome in src/ScreenShotRecipe.Web/wwwroot/css/app.css
- [X] T060 [P] Fix NavMenu link contrast (white text, better hover states) in src/ScreenShotRecipe.Web/Shared/NavMenu.razor.css
- [X] T061 [P] Update Helpers/RunWeb.bat to set ASPNETCORE_ENVIRONMENT=Development for user secrets
- [X] T053 [P] Update quickstart.md with current fake vs real service configuration in specs/001-import-recipe/quickstart.md
- [X] T054 Run end-to-end test: upload images → verify import → search → view → edit → delete → verify persistence
- [X] T055 Performance validation: verify import of 6 images completes within 30 seconds per SC-002

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies - can start immediately
- **Phase 2 (Foundational)**: Depends on Phase 1 completion - BLOCKS all user stories
- **Phase 3-5 (User Stories)**: All depend on Phase 2 completion
  - US1 (P1): Can start immediately after Phase 2
  - US2 (P2): Can start after Phase 2, independent of US1 (can use seed data)
  - US3 (P3): Can start after Phase 2, may reuse RecipeDetail.razor patterns
- **Phase 6 (Polish)**: Depends on desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: After Phase 2 - core import flow, no cross-story dependencies
- **User Story 2 (P2)**: After Phase 2 - uses Recipe entities but independently testable with seed data
- **User Story 3 (P3)**: After Phase 2 - builds on viewing patterns from US2 but edit form is independent

### Within Each User Story

- T017-T028 (US1): ImportService → API endpoints → UI pages
- T029-T037 (US2): Repository search → API endpoint → UI components
- T038-T049 (US3): Repository update → API endpoint → Edit UI

### Parallel Opportunities

**Phase 1** (tasks marked [P]):
- T001, T002, T003 can run in parallel (different entity files)
- T004 depends on T003 (ImportJob must exist for FK)
- T005 can run in parallel with all

**Phase 2** (tasks marked [P]):
- T008, T009, T010 can run in parallel (different DTO files)
- T011, T012 can run in parallel (different interface files)
- T013, T014 can run in parallel (different repository files)
- T006, T007 must complete before T013, T014

**User Stories** (after Phase 2):
- US1, US2, US3 can proceed in parallel on separate branches
- Within each story, tasks are generally sequential (backend → frontend)

---

## Parallel Example: Phase 1 + Phase 2

```bash
# Phase 1 - Launch entity creation in parallel:
T001: Create ImageAsset entity
T002: Create OcrResult entity  
T003: Create ImportJob entity
T005: Add ImageSharp package
# Then T004 (update Recipe FK)

# Phase 2 - Launch DTOs and interfaces in parallel:
T008: Create OcrResultDto
T009: Create RecipeImportRequestDto
T010: Create RecipeImportResponseDto
T011: Create IImageAssetRepository interface
T012: Create IImportJobRepository interface

# After T006, T007 (DbContext):
T013: Implement ImageAssetRepository
T014: Implement ImportJobRepository
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T005) - ~5 tasks
2. Complete Phase 2: Foundational (T006-T016) - ~11 tasks
3. Complete Phase 3: User Story 1 (T017-T028) - ~12 tasks
4. **STOP and VALIDATE**: Test import flow end-to-end with fake service
5. Deploy/demo - users can import recipes!

### Incremental Delivery

1. Setup + Foundational → Foundation ready (16 tasks)
2. US1 (Import) → Test → **MVP deployed!** (28 tasks total)
3. US2 (Browse/Search) → Test → Deploy v2 (37 tasks total)
4. US3 (Edit) → Test → Deploy v3 (49 tasks total)
5. Polish → Final quality pass (61 tasks total)

---

## Summary

| Phase | Tasks | Count | Purpose |
|-------|-------|-------|---------|
| Phase 1 | T001-T005 | 5 | Entity setup, ImageSharp |
| Phase 2 | T006-T016 | 11 | Foundation (DB, DTOs, repos) |
| Phase 3 | T017-T028 | 12 | US1: Import images → recipe |
| Phase 4 | T029-T037 | 9 | US2: Browse, search, view |
| Phase 5 | T038-T049 | 12 | US3: Edit recipes |
| Phase 6 | T050-T061 | 12 | Polish, delete, print, UI fixes |

**Total Tasks**: 61  
**Completed**: 61  
**Remaining**: 0  
**Per User Story**: US1=12, US2=9, US3=12  
**MVP Scope**: Phases 1-3 (28 tasks)  
**Parallel Opportunities**: Phases 1-2 have high parallelism; US phases are sequential within but parallel across stories
