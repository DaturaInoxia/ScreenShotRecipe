# Fake Service Implementations Guide

**Purpose**: Enable end-to-end testing of the recipe import pipeline without external AI dependencies  
**Location**: Infrastructure/Ocr/ and Infrastructure/Parsing/  
**Configuration**: appsettings.json and environment variables

---

## Overview

The ScreenShotRecipe application includes fake/mock implementations of:

1. **IRecipeOcrService** (`FakeRecipeOcrService`)
   - Returns deterministic OCR results from predefined test recipes
   - Supports batch processing (1-6 images)
   - Configurable via DI

2. **ILLMParser** (`FakeLLMParser`)
   - Parses OCR text into structured Recipe entities
   - Deterministic parsing logic (no external API calls)
   - Configurable confidence levels for different test scenarios

These enable full end-to-end workflow testing without incurring API costs or requiring external service credentials.

---

## Configuration

### Option 1: appsettings.json

**File**: `src/ScreenShotRecipe.Web/appsettings.json`

```json
{
  "ServiceImplementation": {
    "UseRealOcr": false,
    "UseRealLlmParser": false,
    "FakeLLMParserConfidenceOverride": null
  }
}
```

| Setting | Type | Default | Purpose |
|---------|------|---------|---------|
| `UseRealOcr` | bool | false | Use real OCR service (GPT-4o) vs fake |
| `UseRealLlmParser` | bool | false | Use real LLM parser vs fake |
| `FakeLLMParserConfidenceOverride` | int? | null | Override confidence for fake parser (1-100) |

### Option 2: Environment Variables

Override any setting via environment variable:

```bash
# Use fake OCR (default)
export OCR_USE_REAL=false

# Use real LLM parser
export PARSER_USE_REAL=true

# Override fake parser confidence to 75%
export PARSER_CONFIDENCE_OVERRIDE=75
```

**Windows (PowerShell)**:
```powershell
$env:OCR_USE_REAL = "false"
$env:PARSER_USE_REAL = "true"
$env:PARSER_CONFIDENCE_OVERRIDE = "75"
```

Environment variables take precedence over appsettings.json.

---

## Use Cases

### Use Case 1: Local Development (All Fake)

**Goal**: Full end-to-end testing without any external dependencies

**Configuration**:
```json
{
  "ServiceImplementation": {
    "UseRealOcr": false,
    "UseRealLlmParser": false
  }
}
```

**Behavior**:
- ✅ Upload multi-image recipes
- ✅ Get deterministic OCR results
- ✅ Parse into structured recipes
- ✅ Store in local SQLite
- ✅ No API calls, no costs
- ⏱️ Fast processing (100-200ms per import)

**Test Data**: FakeRecipeOcrService includes 3 predefined recipes:
- Chocolate Chip Cookies (2 pages)
- Pasta Marinara (2 pages)
- Vegetable Stir-Fry (2 pages)

### Use Case 2: Testing with Real OCR, Fake Parser

**Goal**: Validate GPT-4o OCR accuracy without LLM parsing cost

**Configuration**:
```json
{
  "ServiceImplementation": {
    "UseRealOcr": true,
    "UseRealLlmParser": false
  }
}
```

**Behavior**:
- ✅ Real GPT-4o OCR ($0.17 per image)
- ✅ Fake parser (deterministic parsing)
- ✅ Fast validation of OCR quality
- ⚠️ Incurs OCR costs

### Use Case 3: Full Real Pipeline

**Goal**: Production-like testing with real external services

**Configuration**:
```json
{
  "ServiceImplementation": {
    "UseRealOcr": true,
    "UseRealLlmParser": true
  }
}
```

**Behavior**:
- ✅ Real GPT-4o OCR
- ✅ Real LLM Parser
- ✅ Production-identical behavior
- ⚠️ Full cost per import (~$0.30 + $0.05 = ~$0.35)

### Use Case 4: Testing Different Parsing Confidence Levels

**Goal**: Simulate low-confidence OCR/parsing scenarios for UI testing

**Configuration**:
```json
{
  "ServiceImplementation": {
    "UseRealOcr": false,
    "UseRealLlmParser": false,
    "FakeLLMParserConfidenceOverride": 65
  }
}
```

**Behavior**:
- ✅ Fake OCR with deterministic results
- ✅ Parser returns 65% confidence (below normal threshold)
- ✅ UI can test "low-confidence" warning UX
- ⏱️ Fast (no API calls)

---

## FakeRecipeOcrService Details

### Predefined Test Recipes

The service cycles through three recipes based on batch size:

#### Recipe 1: Chocolate Chip Cookies
- **Pages**: 2
- **Content**: Title, ingredients (11 items), instructions (10 steps)
- **Used for**: Batch sizes with count % 3 == 1

#### Recipe 2: Pasta Marinara
- **Pages**: 2
- **Content**: Title, ingredients (7 items), instructions (8 steps)
- **Used for**: Batch sizes with count % 3 == 2

#### Recipe 3: Vegetable Stir-Fry
- **Pages**: 2
- **Content**: Title, ingredients (9 items), instructions (7 steps)
- **Used for**: Batch sizes with count % 3 == 0

### Return Values

Each recipe page returns a `RecipeOcrResult`:

```csharp
{
    ExtractedText: "<OCR text from page>",
    DetectedLanguage: "en",
    Confidence: 0.92m,  // Always high confidence
    ImageFileName: "<original filename>",
    PageOrder: 0        // Page index
}
```

### Batch Size Behavior

| Batch Size | Recipe Selected | Behavior |
|-----------|---|---|
| 1 | Cookies | Returns page 1 of Cookies recipe |
| 2 | Minara | Returns both pages of Marinara |
| 3 | Stir-Fry | Returns page 1 of Stir-Fry (cycle repeats) |
| 4 | Cookies | Returns page 1 of Cookies (recipe repeats) |
| 5 | Marinara | Returns both pages, then page 1 again |
| 6 | Stir-Fry | Returns both pages, then page 1 again |

---

## FakeLLMParser Details

### Parsing Logic

The fake parser:

1. **Extracts Title**: First non-empty line from OCR text
2. **Extracts Ingredients**: Lines between "INGREDIENTS:" and "INSTRUCTIONS:"
   - Parses quantity and unit (e.g., "2 1/4 cups" → quantity: 2.25, unit: "cups")
   - Returns `Ingredient` entity with confidence: 0.90
3. **Extracts Steps**: Numbered lines after "INSTRUCTIONS:"
   - Removes step numbers ("1. Preheat..." → "Preheat...")
   - Returns `Step` entity with confidence: 0.92
4. **Extracts Tags**: Scans text for keywords
   - Examples: "chocolate", "pasta", "baked", "gluten-free"
5. **Calculates Confidence**: 
   - Base: 0.85
   - +0.08 if well-structured
   - +0.05 if >= 5 ingredients and steps
   - -0.10 per missing section
   - Result: 0.5 to 0.95 (configurable via override)

### Confidence Override

For testing UI behavior with low-confidence results:

```csharp
var parser = new FakeLLMParser(forcedConfidenceLevel: 65);
var result = await parser.ParseAsync(combinedText);
// result.Confidence == 0.65
```

Or via configuration:
```json
{
  "ServiceImplementation": {
    "FakeLLMParserConfidenceOverride": 65
  }
}
```

---

## Integration with Dependency Injection

### Program.cs Registration

The `Program.cs` automatically registers the appropriate implementations:

```csharp
// Load service implementation options
var serviceImplOptions = new ServiceImplementationOptions();
builder.Configuration.GetSection(ServiceImplementationOptions.SectionName).Bind(serviceImplOptions);
serviceImplOptions.ApplyEnvironmentOverrides();

// Register OCR service
if (serviceImplOptions.UseRealOcr)
{
    builder.Services.AddScoped<IRecipeOcrService, GptFourOOcrClient>(); // TODO: When implemented
}
else
{
    builder.Services.AddScoped<IRecipeOcrService, FakeRecipeOcrService>();
}

// Register LLM parser
if (serviceImplOptions.UseRealLlmParser)
{
    builder.Services.AddSingleton<ILLMParser, AzureOpenAIParser>(); // TODO: When implemented
}
else
{
    builder.Services.AddSingleton<ILLMParser>(_ => 
        new FakeLLMParser(serviceImplOptions.FakeLLMParserConfidenceOverride));
}
```

### Using in Application Services

The `ImportService` receives implementations via constructor injection:

```csharp
public class ImportService
{
    private readonly IRecipeOcrService _ocrService;
    private readonly ILLMParser _parser;

    public ImportService(IRecipeOcrService ocrService, ILLMParser parser)
    {
        _ocrService = ocrService;  // Real or Fake
        _parser = parser;            // Real or Fake
    }

    public async Task<RecipeDto> ImportImagesAsync(List<(byte[], string)> images)
    {
        // Extract text (real GPT-4o or fake)
        var ocrResults = await _ocrService.ExtractTextAsync(imageDataList);
        
        // Parse recipe (real LLM or fake)
        var parseResult = await _parser.ParseAsync(combinedText);
        
        // Rest of workflow...
    }
}
```

No changes needed in `ImportService` — it works with both real and fake implementations!

---

## Testing Scenarios

### Scenario 1: Happy Path (All Fake)

```bash
# Run with default fake implementations
dotnet run

# Upload 2-3 recipe screenshots
# Result: Parsed recipe with high confidence (0.92)
```

### Scenario 2: Low-Confidence OCR

```bash
# Configure for low confidence
export PARSER_CONFIDENCE_OVERRIDE=60
dotnet run

# Upload recipe images
# Result: Recipe with 60% confidence, UI shows "low confidence" warning
```

### Scenario 3: Batch Processing

```bash
# Default fake OCR
dotnet run

# Upload 6 images in one request
# Result: All 6 images processed in single batch call, OCR results combined
```

### Scenario 4: Cost-Free Load Testing

```bash
# Multiple fake OCR + fake parser iterations
# No API costs, fast execution
for i in {1..100}; do
  curl -X POST http://localhost:5000/api/import \
    -F "image=@sample_recipes/recipe_$i.jpg"
done

# Result: 100 imports processed without any API calls
```

---

## Tips & Troubleshooting

### Tip 1: Switch Between Real and Fake at Runtime

```bash
# Start with fake
dotnet run

# In another terminal, modify environment variable
export OCR_USE_REAL=true

# Restart application (or manually update config)
# Application now uses real OCR
```

### Tip 2: Test UI with Various Confidence Levels

```bash
# Test high confidence (happy path)
export PARSER_CONFIDENCE_OVERRIDE=95
dotnet run
# ... test UI

# Test low confidence (warning display)
export PARSER_CONFIDENCE_OVERRIDE=65
dotnet run
# ... test UI warning

# Test very low confidence (manual review scenario)
export PARSER_CONFIDENCE_OVERRIDE=30
dotnet run
# ... test UI critical warning
```

### Tip 3: Validate Fake Parser Output

```csharp
[Fact]
public async Task FakeLLMParser_WithValidOcrText_ReturnsStructuredRecipe()
{
    var parser = new FakeLLMParser();
    var ocrText = new FakeRecipeOcrService().ExtractTextAsync(...);
    
    var result = await parser.ParseAsync(ocrText);
    
    Assert.NotNull(result.Recipe);
    Assert.True(result.Recipe.Ingredients.Count > 0);
    Assert.True(result.Recipe.Steps.Count > 0);
    Assert.InRange(result.Confidence, 0.5, 0.95);
}
```

### Issue: Configuration Not Being Applied

**Solution**: Ensure environment variables are set BEFORE starting the application:

```bash
# Wrong (variable set after app starts)
dotnet run
export OCR_USE_REAL=true

# Correct (variable set before app starts)
export OCR_USE_REAL=true
dotnet run
```

### Issue: Test Data Mismatch

**Solution**: The FakeRecipeOcrService cycles recipes based on batch size. For predictable tests, always use batch size 1:

```csharp
var images = new[] { image1 };  // Batch size 1 → always Cookies recipe
var results = await ocrService.ExtractTextAsync(images);
```

---

## Summary

| Feature | Fake Services | Real Services |
|---------|---|---|
| **External Dependencies** | None | GPT-4o API key required |
| **Cost** | Free | $0.30-0.50 per import |
| **Latency** | ~100ms | ~10-30 seconds |
| **Deterministic Results** | ✅ Yes | ❌ Varies |
| **End-to-End Testing** | ✅ YES | ⚠️ Slow & Costly |
| **Production Validation** | ❌ No | ✅ YES |
| **UI/UX Testing** | ✅ YES | ⚠️ Slow feedback loop |
| **Configuration** | appsettings.json + env vars | appsettings.json + env vars |

**Recommendation**: 
- 👨‍💻 **Development**: Use fake services (appsettings.json default)
- 🧪 **Unit/Integration Tests**: Use fake services
- 🔍 **Validation Tests**: Use fake OCR + real parser
- 🚀 **Production Smoke Tests**: Use real services (staged environment)
