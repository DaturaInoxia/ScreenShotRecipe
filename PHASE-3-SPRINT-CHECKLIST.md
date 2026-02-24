# Phase 3 Sprint: MVP Execution Checklist

**Duration:** 1-2 weeks  
**Priority:** CRITICAL PATH to production  
**Start Date:** 2026-02-22  
**Target Completion:** 2026-03-05

---

## Overview

This sprint completes the MVP implementation. Phase 3 consists of three completed components (Fake Services, API, UI) that now require:
1. **Unit & Integration Tests** (BLOCKING) - ~9-10 hours
2. **Smoke Testing** - ~2 hours
3. **Go-Live Validation** - ~1 hour

After completion, application will be production-ready with fake services ($0 cost, no external dependencies).

---

## CRITICAL PATH TASKS (Must Complete)

### ✅ Quick Status Check

**Current Build Status:**
```
✅ Solution compiles without errors
✅ All projects reference correctly
✅ Fake services implemented and wired
✅ API endpoints defined
✅ UI pages created
✅ Database migrations ready (EnsureCreated)
```

**Remaining Blockers:** None - Ready for testing phase!

---

## SPRINT TASKS

### TRACK 1: Unit Tests for ImportService

**Status:** ⏳ NOT STARTED  
**Priority:** P0 - CRITICAL  
**Effort:** 4-5 hours  
**File:** `tests/ScreenShotRecipe.Tests/ImportServiceTests.cs`

#### Task 1.1: Create test fixtures and setup
- [ ] Create test class with xUnit
- [ ] Setup test data: 2-3 mock images (byte arrays)
- [ ] Create mock implementations:
  - [ ] Mock `IRecipeOcrService` (return deterministic OCR results)
  - [ ] Mock `ILLMParser` (return fixed recipe structure)
  - [ ] Mock `IStorage` (track saved files without disk I/O)
  - [ ] Mock `IRecipeRepository` (track added recipes)

**Time:** 1 hour  
**Success Criteria:**
- Test class compiles
- Mocks are properly configured
- Test setup runs without errors

---

#### Task 1.2: Test successful import (happy path)
- [ ] Test method: `ImportImagesAsync_WithValidImages_ReturnsRecipeDto`
  - Setup: 3 valid images
  - Act: Call `ImportService.ImportImagesAsync(images, "test-user")`
  - Assert: 
    - Returns `RecipeDto` with title, ingredients, steps
    - Storage was called 3 times (save each image)
    - OCR was called 1 time (batch)
    - Parser was called 1 time
    - Repository was called 1 time (save recipe)

**Time:** 1.5 hours  
**Success Criteria:** Test passes, all mocks verified

---

#### Task 1.3: Test error scenarios
- [ ] Test `ImportImagesAsync_WithEmptyImageList_ThrowsArgumentException`
- [ ] Test `ImportImagesAsync_WhenOcrFails_ThrowsOcrException`
- [ ] Test `ImportImagesAsync_WhenParserFails_ThrowsParsingException`
- [ ] Test `ImportImagesAsync_WhenStorageFails_ThrowsStorageException`
- [ ] Test `ImportImagesAsync_WhenRepositoryFails_ThrowsDatabaseException`

**Time:** 1.5 hours  
**Success Criteria:**
- Each test verifies correct exception type
- Stack trace contains meaningful context
- Mocks track partial execution (e.g., storage called before parser failure)

---

#### Task 1.4: Test edge cases
- [ ] Test `ImportImagesAsync_WithMaxImages_BatchesCorrectly` (6 images)
- [ ] Test `ImportImagesAsync_WithLargeImages_HandlesSizeCorrectly` (>5MB simulated)
- [ ] Test `ImportImagesAsync_WithNullProperties_PopulatesDefaults`

**Time:** 1 hour  
**Success Criteria:**
- All edge cases validated
- No data corruption
- Reasonable defaults used

---

### TRACK 2: Integration Tests for API Endpoints

**Status:** ⏳ NOT STARTED  
**Priority:** P1 - HIGH  
**Effort:** 3-4 hours  
**File:** `tests/ScreenShotRecipe.Tests/ApiEndpointTests.cs` (new)

#### Task 2.1: Setup test server and client
- [ ] Create test class inheriting from `IAsyncLifetime` or setup fixtures
- [ ] Create in-memory test database (SQLite with `:memory:`)
- [ ] Create `HttpClient` pointing to test server
- [ ] Seed database with 2-3 test recipes

**Time:** 1 hour  
**Success Criteria:**
- Test server starts
- Database created in memory
- Can query recipes

---

#### Task 2.2: Test POST `/api/import` endpoint
- [ ] Test `PostImport_WithValidImages_Returns200AndRecipe`
  - Setup: Create multipart form with 3 test images
  - Act: POST to `/api/import`
  - Assert: 
    - Response status 200
    - Response body contains `RecipeDto` with title, ingredients, steps
    - Recipe persisted in database
    - Can retrieve via GET `/api/recipes/{id}`

**Time:** 1 hour  
**Success Criteria:** POST endpoint works end-to-end

---

#### Task 2.3: Test GET `/api/recipes` endpoint
- [ ] Test `GetRecipes_WithMultipleRecipes_ReturnsAll`
  - Assert: Returns list of all recipes
  - Assert: Each has Id, Title, ingredient/step counts
- [ ] Test `GetRecipes_WithNoRecipes_ReturnsEmptyList`

**Time:** 0.5 hour  
**Success Criteria:** List endpoint works

---

#### Task 2.4: Test GET `/api/recipes/{id}` endpoint
- [ ] Test `GetRecipeById_WithValidId_Returns200AndRecipe`
  - Assert: Returns full recipe with ingredients and steps
- [ ] Test `GetRecipeById_WithInvalidId_Returns404`

**Time:** 0.5 hour  
**Success Criteria:** Detail endpoint works

---

### TRACK 3: Manual Smoke Tests

**Status:** ⏳ NOT STARTED  
**Priority:** P1 - CRITICAL  
**Effort:** 2-3 hours (manual QA)

#### Task 3.1: Test Import Workflow
- [ ] Start application: `dotnet run`
- [ ] Navigate to `https://localhost:7290/import`
- [ ] Upload 2-3 recipe images (can use test screenshots)
- [ ] Verify:
  - [ ] Files accepted (no error message)
  - [ ] Upload button is disabled during processing
  - [ ] Success message appears with recipe title
  - [ ] "View recipe" link works

**Time:** 30 minutes  
**Success Criteria:** Import works without UI crashes

---

#### Task 3.2: Test Browse & View
- [ ] Navigate to `https://localhost:7290/recipes`
- [ ] Verify:
  - [ ] List shows imported recipe(s)
  - [ ] Recipe count displayed correctly
  - [ ] "View" button navigates to detail page
- [ ] Click View on a recipe
- [ ] Verify at `/recipe/{id}`:
  - [ ] Title displayed
  - [ ] Ingredients list showing (name, quantity, unit)
  - [ ] Steps displaying in order
  - [ ] Back link works

**Time:** 30 minutes  
**Success Criteria:** Browse/view works end-to-end

---

#### Task 3.3: Test Error Scenarios (Manual)
- [ ] Try uploading 0 files → error message
- [ ] Try uploading >10 MB file → size warning/rejection
- [ ] Try accessing invalid `/recipe/{bad-guid}` → "Recipe not found" message
- [ ] Try accessing `/recipes` with no recipes → "No recipes yet" message

**Time:** 30 minutes  
**Success Criteria:** Graceful error handling confirmed

---

#### Task 3.4: Browser Compatibility
- [ ] Test in Chrome (defaults)
- [ ] Test in Firefox (if available)
- [ ] Verify responsive layout on mobile (DevTools)
- [ ] Check console for JS errors

**Time:** 30 minutes  
**Success Criteria:** Works in major browsers, no console errors

---

### TRACK 4: Documentation & Cleanup

**Status:** ⏳ NOT STARTED  
**Priority:** P2  
**Effort:** 1-2 hours

#### Task 4.1: Update README
- [ ] Add "Quick Start" section:
  ```
  ## Quick Start
  
  1. Clone: git clone ... && cd ScreenShotRecipe
  2. Build: dotnet build
  3. Run: cd src/ScreenShotRecipe.Web && dotnet run
  4. Open: https://localhost:7290
  5. Navigate to `/import` to get started
  ```
- [ ] Add "Architecture" section pointing to `IMPLEMENTATION-ROADMAP.md`
- [ ] Add "Testing" section with test instructions

**Time:** 30 minutes

---

#### Task 4.2: Create Demo/Seed Data
- [ ] Add seed recipe (e.g., "Chocolate Chip Cookies") to AppDbContext
- [ ] Enable seeding on database initialization
- [ ] Allow users to see example recipe on first launch

**Time:** 30 minutes

---

## EXECUTION SCHEDULE

### Option A: Single Developer (Sequential)

```
Day 1 (8h):
  09:00-12:00 | Track 1.1-1.2 (Unit test setup + happy path) | 3h
  13:00-15:00 | Track 1.3 (Error scenarios) | 2h
  15:00-17:00 | Track 2.1-2.2 (Integration tests) | 2h

Day 2 (8h):
  09:00-10:00 | Track 2.3-2.4 (Remaining integrations) | 1h
  10:00-12:00 | Test refinement & debugging | 2h
  13:00-14:00 | Run all tests, fix failures | 1h
  14:00-16:00 | Track 3: Manual smoke tests | 2h
  16:00-17:00 | Track 4: Docs + cleanup | 1h

Total: 16 hours over 2 days → MVP READY ✅
```

### Option B: Two Developers (Parallel - Recommended)

```
Developer A (Testing Track):
  Day 1: T1.1-T1.4 (Unit tests) = 4-5 hours
  Day 2: T2.1-T2.4 (Integration tests) = 3-4 hours

Developer B (QA/Docs Track):
  Day 1: Manual smoke tests setup + T3.1-T3.2 = 3 hours
  Day 2: T3.3-T3.4 (Error tests + browser compat) + T4.1-T4.2 (Docs) = 3-4 hours

Result: Both complete by EOD Day 2 → Push to production ✅
```

---

## DEFINITION OF DONE: MVP Phase 3

For each task, mark COMPLETE only when:

- [ ] Code written (test or feature)
- [ ] Compiles without warnings (unless pre-existing)
- [ ] All assertions pass
- [ ] Git committed with meaningful message
- [ ] PR created and code reviewed (if team process)
- [ ] Merged to `001-import-recipe` branch

### Final Gate Before Production

Before marking COMPLETE, verify:

```
✅ 1. All unit tests pass (>85% coverage)
✅ 2. All integration tests pass
✅ 3. Smoke tests all green
✅ 4. No console errors in browser DevTools
✅ 5. Database can be recreated cleanly
✅ 6. README updated with quick start
✅ 7. No hardcoded credentials in code/config
✅ 8. Build succeeds: `dotnet build`
✅ 9. App runs: `dotnet run` from Web directory
✅ 10. Can import recipe + browse it without errors
```

**Once ALL 10 pass → MVP PRODUCTION READY ✅**

---

## Risk Mitigation

| Risk | Mitigation |
|------|-----------|
| **Integration test setup too complex** | Use in-memory SQLite; copy existing test patterns from Template Async project |
| **Mock configuration difficult** | Use Moq library (already available); refer to Microsoft docs for patterns |
| **Smoke test reveals layout bug** | Schedule 1-2 hours buffer for UI tweaks (Task 4.3 optional) |
| **Database fixture conflicts** | Use separate test database context; clean up after each test |

---

## Success Metrics

| Metric | Target | Success Criteria |
|--------|--------|-----------------|
| **Unit Test Coverage** | ≥85% | ImportService logic well-covered |
| **Integration Tests** | ≥5 tests | All CRUD paths validated |
| **Manual Test Pass Rate** | 100% | All smoke tests green |
| **Build Time** | <60 seconds | Fast feedback loop |
| **App Startup Time** | <5 seconds | Quick development iteration |

---

## Next Steps After MVP Complete

1. **Deploy to Staging:** Run on staging environment to validate
2. **User Acceptance Test:** Show fake services to stakeholders
3. **Start Phase 2-Real:** Begin GPT-4o implementation with parallel team
4. **Plan Phase 4:** Search feature (optional for MVP, recommended for full release)

---

## Quick Reference

### Run Tests
```powershell
cd D:\src\ScreenShotRecipe\tests\ScreenShotRecipe.Tests
dotnet test --logger "console;verbosity=detailed"
```

### Run Application
```powershell
cd D:\src\ScreenShotRecipe\src\ScreenShotRecipe.Web
dotnet run
# Open: https://localhost:7290
```

### Build
```powershell
cd D:\src\ScreenShotRecipe
dotnet build
```

### Check Coverage (if xunit runner installed)
```powershell
dotnet test /p:CollectCoverage=true
```

---

## Approval Gate

**Ready to start Phase 3 Sprint?**

- [ ] Lead Developer: Confirm all prerequisites complete
- [ ] QA: Confirm smoke test plan reviewed
- [ ] Product: Confirm MVP scope locked

**Signed Off By:** _________________ Date: _______

---

**Current Status:** 🚀 READY TO START PHASE 3 TESTING IMMEDIATELY
