# Phase 2: Real GPT-4o OCR Implementation Tasks

**Feature:** 001-import-recipe  
**Status:** Phase 2 Implementation Ready  
**Created:** 2026-02-22

This document outlines the tasks required to implement the real GPT-4o OCR service and related production infrastructure. These tasks represent the transition from fake/mock implementations to production-ready components.

---

## Overview

**Goal:** Implement real GPT-4o vision-based OCR client, image preprocessing pipeline, and production configuration to enable enterprise-grade recipe image processing.

**Why Phase 2:** Users need this infrastructure BEFORE running production imports. Fake implementations (in Phase 3) are sufficient for MVP development and testing, but production deployments require real OCR with cost optimization and reliability patterns.

**Dependencies:** None - can be implemented in parallel with Phase 3a (fake implementations) and Phase 3b (UI/API endpoints).

**Independent Test Criteria:**
- [ ] GptFourOOcrClient extracts text from multi-page recipe images via GPT-4o Vision API
- [ ] ImagePreprocessor reduces image file size by 40-60% while maintaining OCR quality
- [ ] Configuration supports switching between fake/real services via environment variables
- [ ] Retry logic recovers from transient API failures (automatic 3x with exponential backoff)
- [ ] Unit tests validate OCR response parsing with mocked OpenAI client
- [ ] Integration test validates full pipeline (image → preprocess → GPT-4o → parse) with real API (optional, rate-limited run)
- [ ] Cost tracking logs API calls and token usage for monitoring

---

## Task Breakdown by Component

### Component 1: ImagePreprocessor Utility (4-6 hours)

**Purpose:** Optimize images before sending to GPT-4o Vision API to reduce token costs by 30-40% while maintaining OCR quality.

**Acceptance Criteria:**
- Supports JPEG, PNG, WebP, GIF formats
- Resizes images to 512-2048px (longest dimension)
- Compresses JPEG to Q85/WebP Q80
- Removes EXIF metadata
- Validates <20MB final size (API limit)
- Returns deterministic output (same image always produces same preprocessed result)

---

#### T001 [P] [Phase-2] Create ImagePreprocessor class in Infrastructure/Ocr

- **Description:** Implement reusable image preprocessing utility with format detection, resizing, compression
- **File Path:** `src/ScreenShotRecipe.Infrastructure/Ocr/ImagePreprocessor.cs`
- **Dependencies:** SixLabors.ImageSharp v3.1.0 (already in project.csproj)
- **Acceptance Criteria:**
  - Public method: `Task<ProcessedImage> ProcessAsync(byte[] imageData, string fileName, CancellationToken ct = default)`
  - Detects format from file extension or magic bytes
  - Resizes to configurable dimension (default: 1024px longest)
  - Compresses to JPEG Q85 as default output
  - Removes EXIF and embedded metadata
  - Returns `ProcessedImage` record with: `ProcessedData (byte[])`, `Format (string)`, `OriginalSizeBytes (long)`, `OptimizedSizeBytes (long)`, `CompressionRatio (decimal)`
- **Implementation Notes:**
  - Use `Image.Load()` from ImageSharp for format-agnostic loading
  - Implement IDisposable or use `using` for image streams
  - Add logging for compression metrics
  - Handle corrupted/invalid images gracefully (throw `ImageProcessingException`)

---

#### T002 [P] [Phase-2] Create OcrOptions configuration class

- **Description:** Configuration class for GPT-4o OCR parameters (batch size, model, detail level, timeouts)
- **File Path:** `src/ScreenShotRecipe.Infrastructure/Ocr/OcrOptions.cs`
- **Dependencies:** None
- **Acceptance Criteria:**
  - Public properties: `ApiKey (string)`, `Model (string = "gpt-4o")`, `MaxBatchSize (int = 6)`, `ImageDetailLevel (string = "low")`, `RequestTimeoutSeconds (int = 60)`, `MaxRetries (int = 3)`, `EnableImagePreprocessing (bool = true)`
  - Optional: `BatchApiEnabled (bool = false)` for future Batch API support
  - Validation: `Validate(out List<string> errors)` ensures ApiKey present, MaxBatchSize 1-6, TimeoutSeconds >= 30
  - Constructor with defaults
- **Configuration Section:** `appsettings.json` → `"OpenAI": { "OcrOptions": { ... } }`

---

#### T003 [Phase-2] Add configuration binding in Program.cs

- **Description:** Wire OcrOptions into DI container with environment variable overrides
- **File Path:** `src/ScreenShotRecipe.Web/Program.cs`
- **Dependencies:** T002, ServiceImplementationOptions.cs (already exists)
- **Acceptance Criteria:**
  - Bind `OcrOptions` from `appsettings.json` section `"OpenAI:OcrOptions"`
  - Load API key from: 1) appsettings.json, 2) environment variable `OPENAI_API_KEY`, 3) user secrets (development)
  - Validate options on startup; throw `InvalidOperationException` if invalid
  - Store in `app.Services` for injection into GptFourOOcrClient
  - Log loaded configuration (mask API key in logs)

---

#### T004 [Phase-2] Update appsettings.json with OpenAI configuration

- **Description:** Add OpenAI section with GPT-4o OCR settings
- **File Path:** `src/ScreenShotRecipe.Web/appsettings.json`
- **Dependencies:** T002, ServiceImplementationOptions exists
- **Acceptance Criteria:**
  - Include section: `"OpenAI": { "ApiKey": null, "OcrOptions": { "Model": "gpt-4o", "MaxBatchSize": 6, "ImageDetailLevel": "low", "RequestTimeoutSeconds": 60, ... } }`
  - ApiKey documented as "Use environment variable OPENAI_API_KEY in production"
  - Development uses local value or user secrets
  - Sample: all defaults commented for reference

---

### Component 2: GptFourOOcrClient Implementation (8-10 hours)

**Purpose:** Production OCR client using OpenAI GPT-4o Vision API with batch processing, retry logic, and cost optimization.

**Acceptance Criteria:**
- Accepts 1-6 images per call (batching)
- Preserves reading order (top-left to bottom-right per image)
- Returns structured `RecipeOcrResult` with confidence and language metadata
- Handles network failures with automatic retry (3x exponential backoff)
- Logs token usage for cost tracking
- Validates image constraints before API call

---

#### T005 [Phase-2] Create GptFourOOcrClient class implementing IRecipeOcrService

- **Description:** Main OCR client orchestrating image preprocessing, API calls, and response parsing
- **File Path:** `src/ScreenShotRecipe.Infrastructure/Ocr/GptFourOOcrClient.cs`
- **Dependencies:** IRecipeOcrService (Domain), OcrOptions (T002), ImagePreprocessor (T001), OpenAI NuGet v2.8.0
- **Acceptance Criteria:**
  - Constructor: `GptFourOOcrClient(OpenAIClient openAiClient, OcrOptions options, ILogger<GptFourOOcrClient> logger, ImagePreprocessor preprocessor)`
  - Public method: `Task<IReadOnlyList<RecipeOcrResult>> ExtractTextAsync(IReadOnlyList<ImageData> images, CancellationToken cancellationToken = default)`
  - Validates 1 <= images.Count <= options.MaxBatchSize (throw `InvalidOperationException` if > MaxBatchSize)
  - Preprocesses images (if options.EnableImagePreprocessing)
  - Submits images to GPT-4o with vision message and system prompt
  - Parses JSON response containing array of OCR results (each: order, text, language, confidence)
  - Returns `List<RecipeOcrResult>` matching input order
  - Logs: image count, preprocessing statistics, token usage (input_tokens, output_tokens), latency
  - Timeouts: configured via OcrOptions.RequestTimeoutSeconds

**Implementation Details:**
```csharp
// Pseudocode structure
var messages = new[]
{
    new ChatMessage(ChatMessageRole.System, SystemPromptForRecipeOcr()),
    new ChatMessage(ChatMessageRole.User, BuildVisionContent(preprocessedImages))
};

var response = await _client.CompleteStreamingChatAsync(messages, options, ct);
var jsonResponse = ParseJsonFromResponse(response);
var results = MapToRecipeOcrResults(jsonResponse);
```

---

#### T006 [Phase-2] Implement system prompt and vision message formatting

- **Description:** Design GPT-4o vision message structure to optimize for recipe OCR accuracy
- **File Path:** `src/ScreenShotRecipe.Infrastructure/Ocr/GptFourOOcrClient.cs` (helper methods)
- **Dependencies:** T005
- **Acceptance Criteria:**
  - System prompt guides model to: extract ALL text, preserve reading order, return JSON format, handle mixed languages, detect confidence (0-1 scale)
  - Sample system prompt: `"Extract all visible text from the following recipe images in reading order (left-to-right, top-to-bottom). Return JSON: {images: [{order: 1, text: '...extracted text...', language: 'en', confidence: 0.95}, ...]}. Prioritize accuracy; preserve formatting."`
  - Vision content includes: 1-6 images (HTTPS URLs or base64), optional page number hints
  - Image detail level: configurable ("low", "high") per OcrOptions
  - Response format constraint in prompt ensures consistent JSON parsing

---

#### T007 [Phase-2] Implement response parsing and error validation

- **Description:** Parse GPT-4o JSON response, validate structure, map to RecipeOcrResult records
- **File Path:** `src/ScreenShotRecipe.Infrastructure/Ocr/GptFourOOcrClient.cs` (private methods)
- **Dependencies:** T005
- **Acceptance Criteria:**
  - Parse JSON response from ChatGPT completion
  - Validate each result has: order (int), text (non-empty string), language (string), confidence (decimal 0-1)
  - Handle malformed JSON gracefully (throw `OcrParsingException` with details)
  - Sort results by order field to ensure reading order preservation
  - Map to `RecipeOcrResult(ExtractedText, DetectedLanguage, Confidence, ImageFileName, PageOrder)`
  - Assign PageOrder from order field in JSON
  - Log any warnings (e.g., language != expected, confidence < 0.5)

---

#### T008 [Phase-2] Implement retry policy with exponential backoff

- **Description:** Handle transient failures (API rate limits, timeouts) with automatic retry
- **File Path:** `src/ScreenShotRecipe.Infrastructure/Ocr/GptFourOOcrClient.cs` (wrapper method)
- **Dependencies:** T005
- **Acceptance Criteria:**
  - Wrap API call in retry logic: max 3 attempts (configurable via OcrOptions.MaxRetries)
  - Exponential backoff: 1s, 2s, 4s delays between retries
  - Transient errors: 429 (rate limit), 500-599 (server errors in retry window), TimeoutException
  - Non-transient errors fail immediately: 401 (auth), 400 (bad request), 404 (not found)
  - Log retry attempts with attempt number, delay, and reason
  - OpenAI client has built-in retry (3x); application layer adds circuit-breaker option for Phase 2b

---

#### T009 [Phase-2] Add cost tracking and monitoring

- **Description:** Log API usage for cost analysis and quota tracking
- **File Path:** `src/ScreenShotRecipe.Infrastructure/Ocr/GptFourOOcrClient.cs`
- **Dependencies:** T005
- **Acceptance Criteria:**
  - Log per call: image count, input_tokens (from API response), output_tokens (from API response), estimated cost
  - Cost calculation: (input_tokens * $0.005 / 1000) + (output_tokens * $0.015 / 1000) for gpt-4o vision
  - Track cumulative usage: total calls, total tokens, estimated monthly cost
  - Optional: Emit structured logs compatible with Application Insights / CloudWatch
  - Warn if cumulative monthly cost projection exceeds threshold (e.g., $200/month → alert DevOps)

---

#### T010 [Phase-2] Write unit tests for GptFourOOcrClient

- **Description:** Mock-based tests validating GPT-4o integration, response parsing, error handling
- **File Path:** `tests/ScreenShotRecipe.Tests/GptFourOOcrClientTests.cs`
- **Dependencies:** GptFourOOcrClient (T005), xUnit, Moq
- **Acceptance Criteria:**
  - Test successful OCR with 3-image batch
  - Test response parsing (JSON → RecipeOcrResult)
  - Test image order preservation
  - Test transient error retry logic
  - Test non-transient error (auth failure) immediate failure
  - Test image preprocessing integration
  - Test token usage logging
  - All tests mock `OpenAIClient` and `ImagePreprocessor` to avoid real API calls
  - Coverage: >= 85% of GptFourOOcrClient code paths

---

### Component 3: Configuration & Service Registration (2-3 hours)

**Purpose:** Wire real GPT-4o client into DI container with conditional registration (fake vs. real based on environment).

**Acceptance Criteria:**
- Configuration allows switching between real/fake OCR via `ServiceImplementationOptions`
- DI resolves correct implementation based on `UseRealOcr` setting
- Environment variable `OCR_USE_REAL=true` enables real GPT-4o
- Default: fake services for development/testing

---

#### T011 [Phase-2] Update Program.cs DI registration for real OCR

- **Description:** Conditional registration in DI container (fake vs. real based on config)
- **File Path:** `src/ScreenShotRecipe.Web/Program.cs`
- **Dependencies:** ServiceImplementationOptions (exists), GptFourOOcrClient (T005), OcrOptions (T002)
- **Acceptance Criteria:**
  - Load ServiceImplementationOptions and apply env var overrides
  - If `serviceImplOptions.UseRealOcr == true`:
    - Register `GptFourOOcrClient` as `IRecipeOcrService` (scoped/singleton)
    - Inject `ILogger<GptFourOOcrClient>`, OpenAIClient, OcrOptions, ImagePreprocessor
  - Else:
    - Register `FakeRecipeOcrService` as `IRecipeOcrService` (existing)
  - Validation: Log which OCR implementation is registered on startup
  - Example DI code:
    ```csharp
    if (serviceImplOptions.UseRealOcr)
    {
        builder.Services.AddScoped<IRecipeOcrService, GptFourOOcrClient>();
    }
    else
    {
        builder.Services.AddScoped<IRecipeOcrService, FakeRecipeOcrService>();
    }
    ```

---

#### T012 [Phase-2] Add environment variable documentation

- **Description:** Document all environment variables for real OCR configuration
- **File Path:** `README.md` (Environment Variables section), `src/ScreenShotRecipe.Web/appsettings.json` (comments)
- **Dependencies:** All config components (T002-T011)
- **Acceptance Criteria:**
  - Document: `OPENAI_API_KEY`, `OCR_USE_REAL`, other OcrOptions overrides
  - Example for Docker: `docker-compose override` with env vars
  - Example for local development: `dotnet user-secrets set "OpenAI:ApiKey" "sk-..."`
  - Cost warning: "Using real OCR incurs costs; see research.md for estimates"

---

### Component 4: Integration Tests & Validation (4-6 hours)

**Purpose:** End-to-end validation that real OCR works with the full pipeline.

**Acceptance Criteria:**
- Test real GPT-4o API (optional, rate-limited)
- Validate image preprocessing effectiveness
- Verify pipeline handles multi-image batches
- Validate timeout and retry behavior under failure conditions

---

#### T013 [Phase-2] Create integration test for GPT-4o pipeline

- **Description:** End-to-end test uploading images through OCR to parsed recipe (optional real API call)
- **File Path:** `tests/ScreenShotRecipe.Tests/OcrIntegrationTests.cs`
- **Dependencies:** GptFourOOcrClient, ImagePreprocessor
- **Acceptance Criteria:**
  - Test fixture: 2-3 sample multi-page recipe images (checked into tests/fixtures/)
  - Test method 1: Upload images → ImagePreprocessor → validate compression achieved
  - Test method 2 (optional - skip in CI): Real API call to GPT-4o, validate response parsing
  - Test method 3 (mocked): Full pipeline with mocked OpenAI client
  - Assertions: Text extracted, order preserved, confidence scores present
  - Timeout: 30-60 seconds per call
  - Run selectively: `[Trait("Category", "IntegrationOcr")]` to exclude from quick runs

---

#### T014 [Phase-2] Create cost tracking test

- **Description:** Validate cost calculation and logging for API usage
- **File Path:** `tests/ScreenShotRecipe.Tests/GptFourOOcrClientTests.cs` (additional test)
- **Dependencies:** GptFourOOcrClient (T005)
- **Acceptance Criteria:**
  - Mock response with known token counts (e.g., 1000 input, 500 output)
  - Assert cost calculation is correct: input $0.005, output $0.015 per 1000 tokens
  - Assert logs contain token count and estimated cost
  - Test monthly cost projection warning (e.g., if running 100 calls/day)

---

### Component 5: Documentation & Runbooks (2-3 hours)

**Purpose:** Guide operators on deploying, monitoring, and troubleshooting production GPT-4o integration.

**Acceptance Criteria:**
- Setup guide for obtaining OpenAI API key
- Troubleshooting guide for common errors
- Cost monitoring guide
- Runbook for switching between fake/real services

---

#### T015 [Phase-2] Create GPT-4o integration runbook

- **Description:** Documentation for production deployment and operations
- **File Path:** `docs/GPT4O-INTEGRATION.md`
- **Dependencies:** all above
- **Acceptance Criteria:**
  - Section 1: Prerequisites (OpenAI account, API key)
  - Section 2: Configuration (appsettings.json, environment variables, user secrets)
  - Section 3: Deployment checklist (validate OcrOptions, test with fake first, enable real via env var)
  - Section 4: Monitoring (cost tracking, token usage, API errors)
  - Section 5: Troubleshooting (rate limits, auth failures, image issues)
  - Section 6: Cost estimation (per-image, monthly, annual projections)
  - Example: `OCR_USE_REAL=true OPENAI_API_KEY=sk-... dotnet run`

---

#### T016 [Phase-2] Update README with production setup instructions

- **Description:** Add quick start for real GPT-4o setup
- **File Path:** `README.md` (new section: "Production Setup")
- **Dependencies:** T015
- **Acceptance Criteria:**
  - Add a "Production Setup" section after existing "Quick Start"
  - Steps: 1) Get OpenAI API key, 2) Set env variables, 3) Start app with `OCR_USE_REAL=true`
  - Link to `docs/GPT4O-INTEGRATION.md` for detailed guide
  - Cost warning and link to `specs/001-import-recipe/research.md#cost-analysis`

---

### Component 6: Circuit Breaker & Resilience (3-4 hours) - *Optional Phase 2b*

**Purpose:** Add application-level resilience pattern for cascading failure protection.

**Acceptance Criteria:**
- Circuit breaker prevents cascading API failures
- Graceful degradation to fake service on persistent real failures
- Metrics/telemetry for circuit breaker state

---

#### T017 [Phase-2b] Implement circuit breaker for GPT-4o client

- **Description:** Polly circuit breaker wrapper to prevent API exhaustion
- **File Path:** `src/ScreenShotRecipe.Infrastructure/Ocr/OcrCircuitBreaker.cs` (decorator)
- **Dependencies:** Polly NuGet package (new dependency)
- **Acceptance Criteria:**
  - Wraps GptFourOOcrClient with circuit breaker policy
  - Open circuit after 5 failures in 60-second window
  - Half-open: retry after 30-second wait
  - On circuit break: return fallback (FakeRecipeOcrService or throw `ServiceUnavailableException`)
  - Log circuit state changes
  - Metrics: circuit breaker state, state transitions, fallback invocations

---

#### T018 [Phase-2b] Add fallback strategy

- **Description:** Graceful fallback to fake OCR when real service unavailable
- **File Path:** `src/ScreenShotRecipe.Web/Program.cs` (DI configuration)
- **Dependencies:** T017
- **Acceptance Criteria:**
  - On circuit break or persistent failure: fallback to `FakeRecipeOcrService`
  - Log warning: "Real OCR unavailable; using fake service for recovery"
  - User-facing message: "Import processing delayed; using cached recipes"
  - Recovery: circuit breaker resets after success, returns to real service

---

## Task Summary Table

| Task ID | Component | Priority | Hours | File Path | Status |
|---------|-----------|----------|-------|-----------|--------|
| T001 | ImagePreprocessor | P1 | 4-6 | `Infrastructure/Ocr/ImagePreprocessor.cs` | ⏳ |
| T002 | Configuration | P1 | 1 | `Infrastructure/Ocr/OcrOptions.cs` | ⏳ |
| T003 | DI Wiring | P1 | 1 | `Program.cs` | ⏳ |
| T004 | Configuration | P1 | 1 | `appsettings.json` | ⏳ |
| T005 | GPT-4o Client | P1 | 8-10 | `Infrastructure/Ocr/GptFourOOcrClient.cs` | ⏳ |
| T006 | Prompt Design | P1 | 2 | `Infrastructure/Ocr/GptFourOOcrClient.cs` | ⏳ |
| T007 | Response Parsing | P1 | 2 | `Infrastructure/Ocr/GptFourOOcrClient.cs` | ⏳ |
| T008 | Retry Logic | P1 | 2 | `Infrastructure/Ocr/GptFourOOcrClient.cs` | ⏳ |
| T009 | Monitoring | P2 | 1 | `Infrastructure/Ocr/GptFourOOcrClient.cs` | ⏳ |
| T010 | Testing | P1 | 4 | `tests/ScreenShotRecipe.Tests/GptFourOOcrClientTests.cs` | ⏳ |
| T011 | DI Wiring | P1 | 1 | `Program.cs` | ⏳ |
| T012 | Documentation | P2 | 1 | `README.md, appsettings.json` | ⏳ |
| T013 | Integration Tests | P2 | 4-6 | `tests/ScreenShotRecipe.Tests/OcrIntegrationTests.cs` | ⏳ |
| T014 | Cost Tests | P3 | 1 | `tests/ScreenShotRecipe.Tests/` | ⏳ |
| T015 | Runbook | P2 | 2 | `docs/GPT4O-INTEGRATION.md` | ⏳ |
| T016 | Documentation | P2 | 1 | `README.md` | ⏳ |
| T017 | Resilience | P3 | 3-4 | `Infrastructure/Ocr/OcrCircuitBreaker.cs` | ⏳ Phase-2b |
| T018 | Fallback | P3 | 1 | `Program.cs` | ⏳ Phase-2b |

**Total Phase 2 Effort:** 40-57 hours (MVP: 32-40 hours including T001-T012 + T015-T016)

---

## Execution Strategy

### MVP Path (32-40 hours)
1. ImagePreprocessor (T001-T002) - **4-7 hours**
2. GptFourOOcrClient (T005) - **8-10 hours**  
3. Prompt & Response Parsing (T006-T007) - **4 hours**
4. Retry Logic (T008) - **2 hours**
5. DI Registration (T011) - **1 hour**
6. Unit Tests (T010) - **4 hours**
7. Documentation (T015-T016) - **3 hours**

**Total Phase 2 MVP:** ~32-40 hours, completed in parallel with Phase 3 fake implementations and UI.

### Phase 2b (Extended - 5-6 hours)
- Add circuit breaker (T017-T018) for production resilience
- Add integration tests (T013-T014) for validation

---

## Parallel Execution

- **Team A:** ImagePreprocessor (T001-T004) + GptFourOOcrClient (T005-T009)
- **Team B:** DI & Testing (T010-T011) + Documentation (T012, T015-T016)
- **Team C:** Can work on Phase 3 (UI/API) in parallel; blocks when ready to integrate real OCR

---

## Acceptance Criteria (Phase 2 Complete)

- ✅ ImagePreprocessor preprocesses recipe images, achieving 40-60% size reduction
- ✅ GptFourOOcrClient successfully calls GPT-4o Vision API for 1-6 image batches
- ✅ Retry logic handles transient failures gracefully
- ✅ Configuration allows switching between fake/real OCR via environment variable
- ✅ Unit tests validate GPT-4o integration with >85% code coverage
- ✅ Cost tracking logs API usage for monitoring
- ✅ Documentation provided for deployment and operations
- ✅ Integration test validates end-to-end pipeline (optional real API call)

---

## Definition of Done

For each Phase 2 task:
1. ✅ Code complete and compiles without warnings
2. ✅ Unit tests pass (mocked dependencies)
3. ✅ Integration tests pass (if applicable)
4. ✅ Code reviewed and approved
5. ✅ Inline documentation (XML comments on public members)
6. ✅ Runbook/operational documentation updated
7. ✅ Performance validated (latency, cost per call)
8. ✅ Merged to `001-import-recipe` branch

---

## Next Steps

1. **Phase 2 Implementation:** Assign tasks T001-T016 to development team (MVP focus)
2. **Phase 3 Parallel:** Implement fake services and UI (existing tasks in main tasks.md)
3. **Integration:** Once Phase 2 complete, wire real OCR into Phase 3 API endpoints
4. **Validation:** Run integration test (T013) with real API credentials in staging
5. **Production:** Deploy with `OCR_USE_REAL=true` in production environment
