# Phase 2-Real Sprint: GPT-4o Implementation Starter

**Duration:** 2-4 weeks (concurrent with Phase 3 testing)  
**Priority:** Production-grade OCR engine  
**Start Date:** 2026-02-25 (or immediately if parallel team available)  
**Target Completion:** 2026-03-15

---

## Quick Start Context

**Why This Matters:**
- Replaces fake OCR with real GPT-4o Vision API
- Enables production imports with high accuracy
- Estimated cost: $5,100/year for 30,000 images/month
- Superior to Azure Vision for handwritten text and complex layouts

**Prerequisite Knowledge:**
- Read `specs/001-import-recipe/research.md` (Decision summary)
- Read `specs/001-import-recipe/PHASE-2-REAL-IMPLEMENTATION.md` (Task breakdown)
- Review `IRecipeOcrService` interface in Domain/Interfaces

**Current Status:**
- ✅ Phase 1-2 foundation complete
- ✅ Fake services proven working
- ⏳ Phase 3 testing in progress (parallel track)
- ⏳ Phase 2-Real implementation starts here

---

## CRITICAL: Prerequisites Before Coding

### Checklist: Get Started

- [ ] **OpenAI Account Setup:**
  - Have you created an OpenAI account? https://platform.openai.com
  - Have you generated an API key? (Settings → API Keys → Create new)
  - Can you access: https://platform.openai.com/api/usage/overview (cost tracking)

- [ ] **Cost Awareness:**
  - Understand estimated cost: $0.17 per image (~$5,100/yr for 30k/month)
  - Read cost mitigation strategies in `research.md#2-cost-analysis`
  - Budget: Only use batch processing during dev (50% discount)

- [ ] **Environment Setup:**
  - .NET 9 SDK installed? (`dotnet --version` should show 9.x.x)
  - SixLabors.ImageSharp v3.1.0 available? (check `Infrastructure.csproj`)
  - OpenAI NuGet package v2.8.0+ available? (check `Infrastructure.csproj`)

- [ ] **Code Review:**
  - Read `IRecipeOcrService.cs` interface (what you're implementing)
  - Review `FakeRecipeOcrService.cs` (pattern to follow)
  - Understand domain model: `Recipe`, `Ingredient`, `Step` entities

---

## PHASE 2-REAL TASK BREAKDOWN

### MVP TRACK (Priority 1: 32-40 hours)

#### COMPONENT 1: Image Preprocessing (4-7 hours)  
**Responsible:** Developer A or Single Dev  
**Dependency:** None (can start immediately)

**Tasks:**
- [ ] T001: Create `ImagePreprocessor.cs` utility class
  - Input: `byte[] imageData, string fileName`
  - Output: `ProcessedImage` record with preprocessed bytes, compression ratio
  - Supports: JPEG, PNG, WebP, GIF
  - Actions: Resize to 1024px longest dimension, compress to JPEG Q85
  - Time: 2-3 hours
  
- [ ] T002: Create `OcrOptions.cs` configuration class
  - Properties: ApiKey, Model, MaxBatchSize (1-6), ImageDetailLevel, TimeoutSeconds
  - Validation: Ensure ApiKey present, MaxBatchSize in range 1-6
  - Time: 1 hour
  
- [ ] T003: Wire DI in `Program.cs`
  - Load OcrOptions from `appsettings.json` section "OpenAI:OcrOptions"
  - Load ApiKey from env var `OPENAI_API_KEY` (priority over appsettings)
  - Validate on startup; throw if invalid
  - Time: 1 hour
  
- [ ] T004: Update `appsettings.json`
  - Add section: `"OpenAI": { "ApiKey": null, "OcrOptions": {...} }`
  - ApiKey documented as "Use env var OPENAI_API_KEY in production"
  - Time: 0.5 hours

**Subtotal:** 4-5.5 hours  
**Gate Before Starting T005:** ImagePreprocessor compiles and unit-tested

---

#### COMPONENT 2: GPT-4o Client Implementation (8-12 hours)  
**Responsible:** Developer A or Single Dev  
**Dependency:** Component 1 complete

**Tasks:**
- [ ] T005: Create `GptFourOOcrClient.cs` main implementation
  - Implements: `IRecipeOcrService` interface
  - Method: `ExtractTextAsync(IReadOnlyList<ImageData> images, CancellationToken ct)`
  - Actions:
    1. Validate batch size (1 <= count <= MaxBatchSize)
    2. Preprocess images (if enabled)
    3. Build vision message with system prompt
    4. Call OpenAI ChatClient
    5. Parse JSON response
    6. Return `List<RecipeOcrResult>` in order
  - Logging: image count, preprocessing rate, tokens, latency, cost
  - Time: 4-5 hours

- [ ] T006: Implement system prompt + vision message formatting
  - Design prompt to extract all text, preserve order, return JSON
  - Create helper methods: `BuildSystemPrompt()`, `BuildVisionContent()`
  - Test output structure matches expected format
  - Time: 1-2 hours

- [ ] T007: Implement response parsing
  - Parse JSON response: `{images: [{order, text, language, confidence}, ...]}`
  - Validate: each result has required fields
  - Handle malformed JSON gracefully (throw `OcrParsingException`)
  - Map to `RecipeOcrResult` records
  - Time: 1-2 hours

- [ ] T008: Implement retry policy
  - Wrap API call in retry logic: 3x attempts, 1s/2s/4s backoff
  - Transient errors (429, 5xx, timeout): retry
  - Non-transient (401, 400): fail immediately
  - Log all retry attempts
  - Time: 1 hour

- [ ] T009: Add cost tracking + monitoring (Optional, but recommended)
  - Log: tokens (input/output), estimated cost per call
  - Track cumulative monthly projection
  - Warn if exceeds threshold ($200/month)
  - Time: 0.5-1 hour

**Subtotal:** 8-11.5 hours  
**Gate Before Starting T011:** GptFourOOcrClient compiles, no null reference warnings

---

#### COMPONENT 3: DI Configuration (2-3 hours)  
**Responsible:** Developer B or Single Dev after T005  
**Dependency:** T001-T005 complete

**Tasks:**
- [ ] T011: Update `Program.cs` for conditional registration
  - Load `ServiceImplementationOptions`
  - If `UseRealOcr == true`:
    - Register `GptFourOOcrClient` as `IRecipeOcrService` (scoped)
  - Else:
    - Register `FakeRecipeOcrService`
  - Inject: `ILogger`, `OpenAIClient`, `OcrOptions`, `ImagePreprocessor`
  - Log on startup which implementation is active
  - Time: 1 hour

- [ ] T012: Document environment variables
  - Add to `README.md` section: "Environment Variables"
  - Document: `OPENAI_API_KEY`, `OCR_USE_REAL`
  - Example: `OCR_USE_REAL=true OPENAI_API_KEY=sk-... dotnet run`
  - Time: 0.5 hour

- [ ] T016: Update README with production setup
  - Add "Production Setup" section
  - Steps to get API key, set env vars
  - Link to `docs/GPT4O-INTEGRATION.md`
  - Cost warning & link to budget analysis
  - Time: 0.5 hour

**Subtotal:** 2-2.5 hours  
**Gate Before Proceeding:** Config switches correctly between real/fake

---

#### COMPONENT 4: Testing (4-6 hours)  
**Responsible:** Developer B (test specialist) or Single Dev  
**Dependency:** T001-T009 complete

**Tasks:**
- [ ] T010: Unit tests for GptFourOOcrClient
  - Mock `OpenAIClient` to return deterministic response
  - Test successful OCR (3-image batch)
  - Test response parsing (JSON → RecipeOcrResult)
  - Test retry logic (fail 2x, succeed 3x)
  - Test error handling (auth fail, timeout, malformed response)
  - Target coverage: >85%
  - Time: 2-3 hours

- [ ] T013: Integration test for end-to-end pipeline
  - Setup: 2-3 real recipe images as test fixtures
  - Path A: Mock OpenAI (fast, part of CI)
  - Path B: Optional real API test (slow, manual, rate-limited)
  - Test: image → preprocess → GPT-4o → parse → RecipeOcrResult
  - Assert: text extracted, order preserved, confidence > 0.5
  - Time: 2-3 hours

- [ ] T014: Cost tracking test
  - Mock response with known token counts
  - Assert cost calculation correct
  - Assert logs contain token info
  - Time: 0.5-1 hour

**Subtotal:** 4.5-6.5 hours  
**Gate Before Shipping:** All tests pass, >85% coverage

---

#### COMPONENT 5: Documentation (3-5 hours)  
**Responsible:** Developer B or Tech Writer  
**Dependency:** Can start immediately (reference docs exist)

**Tasks:**
- [ ] T015: Create `docs/GPT4O-INTEGRATION.md` runbook
  - Section 1: Prerequisites (OpenAI account, API key)
  - Section 2: Configuration (appsettings, env vars, secrets)
  - Section 3: Deployment checklist
  - Section 4: Monitoring (cost, token usage, errors)
  - Section 5: Troubleshooting (rate limits, image issues)
  - Section 6: Cost estimation (per-image, monthly, annual)
  - Time: 2-3 hours

**Subtotal:** 2-3 hours

---

### TOTAL MVP PHASE 2-REAL: 32-40 hours

**Parallel Path:**
```
Team Member A: T001-T002-T003-T004 (4-5h) → T005-T006-T007-T008-T009 (8-11.5h) = 12-16.5h
Team Member B: T010-T013-T014 (4.5-6.5h) + T015 (2-3h) + T011-T012-T016 (2-2.5h) = 8.5-12h

Critical Path: A's component 1 (4-5h) → A's component 2 (8-11.5h) → B's DI + tests (10h)
Total: ~22-33 hours serial equivalent, ~18 hours if fully parallel
```

---

### OPTIONAL PHASE 2b (3-6 hours): Resilience Patterns

Only after MVP is complete and tested.

- [ ] T017: Circuit breaker with Polly
- [ ] T018: Fallback to fake service on circuit break

---

## DAY 1: Setup & Quick Start

### Morning (30 min): Read & Understand

- [ ] Read `research.md` GPT-4o decision summary
- [ ] Read `PHASE-2-REAL-IMPLEMENTATION.md` task breakdown
- [ ] Review `IRecipeOcrService.cs` interface
- [ ] Review `FakeRecipeOcrService.cs` pattern

### Mid-Morning (1 hour): Environment Setup

- [ ] Verify .NET 9 installed
- [ ] Check NuGet dependencies in `.csproj`
- [ ] Create OpenAI account and get API key
- [ ] Test API key: Call ChatClient.CompleteAsync with simple prompt (Hello)

### Late Morning (2 hours): Component 1 - Image Preprocessing

**Developer A starts T001-T004:**
- [ ] Create `ImagePreprocessor.cs` with basic implementation
- [ ] Create `OcrOptions.cs` configuration class
- [ ] Compile check (no errors)
- [ ] Write unit test for resize + compress

---

## DAY 2-3: Core Implementation

### Day 2 (8 hours): Component 2 - GPT-4o Client

**Developer A continues T005-T009:**
- [ ] Scaffold `GptFourOOcrClient.cs`
- [ ] Implement batch preprocessing
- [ ] Implement ChatClient integration
- [ ] Implement response parsing
- [ ] Add retry logic
- [ ] Add logging

**Developer B starts T010-T013:**
- [ ] Setup test infrastructure (mocks)
- [ ] Write unit tests (mocked OpenAI)
- [ ] Start integration test structure

### Day 3 (8 hours): Testing & DI Configuration

**Developer A T011-T012-T016:**
- [ ] Update `Program.cs` DI registration
- [ ] Update `appsettings.json`
- [ ] Add environment variable documentation
- [ ] Update README production section

**Developer B completes T010-T014:**
- [ ] Finish unit tests
- [ ] Finish integration tests
- [ ] Cost tracking tests
- [ ] All tests green

---

## DAY 4-5: Documentation & Validation

### Day 4 (4 hours): Documentation & Docs

**Developer B T015:**
- [ ] Create comprehensive `docs/GPT4O-INTEGRATION.md`
- [ ] Write troubleshooting section
- [ ] Cost estimation examples

**Developer A (optional):**
- [ ] Code review of T010-T014
- [ ] Performance tune (image preprocessing)

### Day 5 (2 hours): Final Validation & Gate

**All developers:**
- [ ] Run full test suite (all tests pass)
- [ ] Build succeeds without warnings
- [ ] Try real API call with test image (optional, rate limited)
- [ ] Verify switching between fake/real works
- [ ] Verify cost tracking logs as expected

---

## SUCCESS CRITERIA: PHASE 2-REAL COMPLETE

- ✅ GptFourOOcrClient successfully calls GPT-4o Vision API
- ✅ Image preprocessing reduces file size 40-60%
- ✅ Batch processing: 1-6 images per call
- ✅ Reading order preserved
- ✅ Retry logic handles transient failures
- ✅ Configuration allows switching real/fake via `OCR_USE_REAL=true`
- ✅ Cost tracking logs token usage
- ✅ Unit tests pass (>85% coverage)
- ✅ Integration tests pass (end-to-end validated)
- ✅ Documentation complete
- ✅ Build succeeds
- ✅ No hardcoded secrets in code

---

## Integration with Phase 3

**How Phase 2-Real connects:**

1. **Current State:** Phase 3 MVP uses `FakeRecipeOcrService`
2. **After Phase 2-Real:** Swap to `GptFourOOcrClient` via `OCR_USE_REAL=true`
3. **No breaking changes:** Both implement `IRecipeOcrService`
4. **Staging test:** Run integration test with real API before production

**Example Production Deployment:**
```powershell
# Staging (with real GPT-4o)
$env:OCR_USE_REAL="true"
$env:OPENAI_API_KEY="sk-..."
dotnet run

# Production (same)
docker run \
  -e OCR_USE_REAL=true \
  -e OPENAI_API_KEY=sk-... \
  screenshot-recipe:latest
```

---

## Project Files Location Reference

```
Project Root: D:\src\ScreenShotRecipe

Core Files:
- Domain/Interfaces/IRecipeOcrService.cs          (interface to implement)
- Infrastructure/Ocr/FakeRecipeOcrService.cs      (reference implementation)
- Infrastructure/Config/ServiceImplementationOptions.cs (config)

To Create:
Infrastructure/Ocr/
├── ImagePreprocessor.cs        (T001)
├── GptFourOOcrClient.cs        (T005)
├── OcrOptions.cs               (T002)
└── OcrExceptions.cs            (custom exceptions)

Tests/
└── ScreenShotRecipe.Tests/GptFourOOcrClientTests.cs  (T010+T013)

Documentation:
├── docs/GPT4O-INTEGRATION.md   (T015)
├── README.md                   (T016, T012)
└── specs/001-import-recipe/PHASE-2-REAL-IMPLEMENTATION.md (reference)
```

---

## Troubleshooting Quick Reference

| Problem | Solution |
|---------|----------|
| **API Key not found** | Set env var: `$env:OPENAI_API_KEY="sk-..."` |
| **ImageSharp not found** | Install NuGet: `dotnet add package SixLabors.ImageSharp` |
| **OpenAI client not found** | Install: `dotnet add package OpenAI` |
| **Tests fail with null mock** | Verify mock setup: `new Mock<IRecipeOcrService>().Setup(...)` |
| **Rate limited (429 error)** | Wait 1 minute, retry. Use batch API for high volume. |
| **Image too large** | ImagePreprocessor should resize to 1024px (API limit 4096px) |

---

## Communication Checklist

- [ ] Daily standup: Blockers, progress, next day plan
- [ ] Mid-sprint check-in (Day 3): Adjust estimates if needed
- [ ] Code review: All PRs reviewed before merge
- [ ] Documentation: Update roadmap as tasks complete

---

## GO-LIVE READINESS

Before switching production from fake to real:

```
✅ Phase 2-Real MVP complete (all tests passing)
✅ Integration test validates real API calls
✅ Cost tracking validates budget expectations
✅ Phase 3 MVP complete (UI, import workflow tested)
✅ Staging environment: Deploy with OCR_USE_REAL=true
✅ Staging smoke test: Import 5-10 sample recipes
✅ No errors in logs
✅ Cost per import reasonable (<$1)
✅ Speed acceptable (10-30 sec per import)
✅ Approval from Lead Dev + Product Owner
⟹ DEPLOY TO PRODUCTION
```

---

**Status:** 🚀 **READY TO START PHASE 2-REAL IMPLEMENTATION**

Questions? Check `PHASE-2-REAL-IMPLEMENTATION.md` for detailed task descriptions.
