## IRecipeOcrService Contract

**Location**: `ScreenShotRecipe.Domain/Interfaces/IRecipeOcrService.cs`  
**Layer**: Domain (interface)  
**Implementation**: Infrastructure/Ocr/GptFourOOcrClient.cs (GPT-4o)

### Purpose
Abstract contract for multimodal OCR service. Accepts multiple images in reading order and returns extracted text with confidence metadata.

### Interface Definition

```csharp
namespace ScreenShotRecipe.Domain.Interfaces;

/// <summary>
/// Multimodal OCR service for recipe image text extraction.
/// Implementations (e.g., GPT-4o multimodal) are provided by the Infrastructure layer.
/// </summary>
public interface IRecipeOcrService
{
    /// <summary>
    /// Extracts text from multiple images in reading order via a single batch call.
    /// </summary>
    /// <param name="images">Collection of images to process (1-6 recommended)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Ordered OCR results with extracted text and confidence metadata</returns>
    /// <exception cref="InvalidOperationException">Thrown if OCR service is unavailable</exception>
    /// <exception cref="OperationCanceledException">Thrown if request is cancelled</exception>
    Task<IReadOnlyList<OcrResult>> ExtractTextAsync(
        IReadOnlyList<ImageData> images,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Image data packet for OCR processing.
/// </summary>
public record ImageData(
    byte[] Content,           // Image binary data
    string MediaType,         // MIME type: "image/jpeg", "image/png", etc.
    string FileName,          // Original filename for logging/traceability
    int PageOrder = 0         // Upload order (for multi-image batches)
);

/// <summary>
/// OCR result for a single image.
/// </summary>
public record OcrResult(
    string ExtractedText,     // Raw text extracted from image
    string DetectedLanguage,  // ISO 639-1 language code (e.g., "en", "es")
    decimal Confidence,       // 0.0-1.0 average confidence score
    string ImageFileName,     // Reference to source image
    int PageOrder             // Original page order (preserved from input)
);
```

### Implementation Contract (GPT-4o)

**File**: `ScreenShotRecipe.Infrastructure/Ocr/GptFourOOcrClient.cs`

### Key Behaviors

1. **Multimodal Batch Processing**
   - Accepts 1-6 images per call
   - Processes images preserving upload order
   - Returns results in same order as input

2. **Error Handling**
   - Retry transient failures (network, rate limits) with exponential backoff
   - Throw `InvalidOperationException` for permanent failures (invalid image format, API misconfiguration)
   - Log detailed diagnostics for failed calls

3. **Performance**
   - Target latency: 5-15 seconds for typical 3-image batch
   - Timeout: 60 seconds per call (configurable)
   - Implement circuit breaker for cascading failures

4. **Confidence Scoring**
   - Score range: 0.0 (low confidence) – 1.0 (high confidence)
   - Confidence calculation:
     - `>= 0.9`: High confidence (well-formatted text, clear images)
     - `0.7-0.9`: Medium confidence (some OCR artifacts, handwriting)
     - `< 0.7`: Low confidence (flag for manual review)

5. **Language Detection**
   - Detect language automatically from image content
   - Return ISO 639-1 code (e.g., "en", "es", "fr")
   - Support multilingual recipes

### Testing Contract

**When implementing, ensure:**
- Mock returns consistent `OcrResult` records matching input order
- Fake client (`FakeOcrClient`) returns deterministic results
- Unit tests verify batch size validation (1-6 images)
- Integration tests mock GPT-4o API responses

### Application Usage Pattern

```csharp
// In ImportService (Application layer)
var imageData = new List<ImageData>
{
    new(imageBytes1, "image/jpeg", "recipe_1.jpg", pageOrder: 0),
    new(imageBytes2, "image/jpeg", "recipe_2.jpg", pageOrder: 1),
    new(imageBytes3, "image/jpeg", "recipe_3.jpg", pageOrder: 2),
};

var ocrResults = await _ocrService.ExtractTextAsync(imageData, cancellationToken);

// Concatenate in order
var concatenatedText = string.Join("\n---PAGE BREAK---\n",
    ocrResults.OrderBy(r => r.PageOrder).Select(r => r.ExtractedText)
);

// Pass to LLM parser for structure extraction
var recipe = await _parser.ParseRecipeAsync(concatenatedText, cancellationToken);
```

### Configuration

**appsettings.json**
```json
{
  "OpenAI": {
    "ApiKey": "${OPENAI_API_KEY}",
    "OcrModel": "gpt-4o",
    "OcrModelVersion": "gpt-4o-2024-11-20",
    "VisionDetail": "auto",
    "MaxBatchSize": 6,
    "TimeoutSeconds": 60,
    "RetryCount": 3
  }
}
```

### Related Interfaces

- `ILLMParser`: Downstream consumer of concatenated OCR text
- `IStorage`: Can store OCR results for caching/audit
- `IRecipeRepository`: Persists ImportJob with OCR metadata

---

## Summary

✅ Abstract contract supports OCR provider swapping  
✅ Multimodal batch processing reduces latency and cost  
✅ Confidence metadata enables low-confidence flagging  
✅ Language detection supports multilingual recipes  
✅ Application layer treats as injectable dependency  
✅ Infrastructure (GPT-4o) implementation details hidden  
