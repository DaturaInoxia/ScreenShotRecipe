# Quickstart: Recipe OCR Integration

**Purpose**: Get the recipe import pipeline running (fast with fakes, or with real GPT-4o)  
**Target Audience**: Backend developers, DevOps  
**Duration**: ~5 min (fake), ~30 min (real GPT-4o setup)

---

## Prerequisites

- .NET 9 SDK installed
- Visual Studio Code or Visual Studio 2022
- Git repository cloned: `DaturaInoxia/ScreenShotRecipe`
- OpenAI API account (optional, only needed for real OCR)

---

## FAST START: Fake Services (No External Dependencies)

**Time: 5 minutes**

The application includes fake OCR and parser implementations for testing without costs or external APIs.

### Step 1: Build & Run

```bash
cd src/ScreenShotRecipe.Web
dotnet build
dotnet run
```

### Step 2: Upload a Recipe

Navigate to `http://localhost:5000/api/import` and upload multiple recipe images.

```powershell
# Example: Upload 2 test images
curl -X POST http://localhost:5000/api/import \
  -F "image1=@sample_recipe_page1.jpg" \
  -F "image2=@sample_recipe_page2.jpg"
```

### Step 3: View Result

Application returns structured recipe from fake OCR + fake parser:

```json
{
  "title": "Chocolate Chip Cookies",
  "ingredients": [
    {"name": "all-purpose flour", "quantity": 2.25, "unit": "cups"},
    {"name": "baking soda", "quantity": 1, "unit": "tsp"}
  ],
  "steps": [
    {"ordinal": 1, "text": "Preheat oven to 375°F"},
    {"ordinal": 2, "text": "Mix flour, baking soda and salt"}
  ],
  "tags": ["cookies", "chocolate", "baked"],
  "confidence": 0.92
}
```

✅ **Full pipeline working end-to-end without API costs!**

### Configuration: Fake Services

**File**: `appsettings.json`

```json
{
  "ServiceImplementation": {
    "UseRealOcr": false,
    "UseRealLlmParser": false,
    "FakeLLMParserConfidenceOverride": null
  }
}
```

**Test Different Scenarios**:

```powershell
# Simulate low-confidence parsing (for UI testing)
$env:PARSER_CONFIDENCE_OVERRIDE = "65"
dotnet run
```

→ For more fake service scenarios, see [FAKE-SERVICES-GUIDE.md](FAKE-SERVICES-GUIDE.md)

---

## PRODUCTION PATH: Real GPT-4o OCR

**Time: 30 minutes + setup**

### Step 1: Create OpenAI API Key

1. Navigate to [OpenAI Platform](https://platform.openai.com/account/api-keys)
2. Sign in or create account (free tier available)
3. Click "Create new secret key"
4. Copy the API key
5. Store securely: **Never commit to git**

### Step 2: Set Environment Variable

**Windows (PowerShell)**
```powershell
[Environment]::SetEnvironmentVariable("OPENAI_API_KEY", "sk-...", "User")
$env:OPENAI_API_KEY = "sk-..."
```

**macOS/Linux**
```bash
export OPENAI_API_KEY="sk-..."
echo 'export OPENAI_API_KEY="sk-..."' >> ~/.bashrc
```

### Step 3: Verify Access

```powershell
# In PowerShell
Write-Host $env:OPENAI_API_KEY  # Should display your key
```

---

## DETAILED SETUP: Real GPT-4o OCR Implementation## 2️⃣ NuGet Dependencies

Add required packages to `ScreenShotRecipe.Infrastructure.csproj`:

```xml
<ItemGroup>
    <!-- OpenAI client for GPT-4o vision -->
    <PackageReference Include="OpenAI" Version="2.8.0" />
    
    <!-- Image preprocessing and compression -->
    <PackageReference Include="SixLabors.ImageSharp" Version="3.1.0" />
    
    <!-- Existing dependencies -->
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="9.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.0" />
</ItemGroup>
```

**Install via CLI**:
```bash
cd src/ScreenShotRecipe.Infrastructure
dotnet add package OpenAI --version 2.8.0
dotnet add package SixLabors.ImageSharp --version 3.1.0
```

---

## 3️⃣ Configuration Setup

### Update `appsettings.json`

Add GPT-4o configuration to `src/ScreenShotRecipe.Web/appsettings.json`:

```json
{
  "OpenAI": {
    "ApiKey": "",
    "OcrModel": "gpt-4o",
    "OcrModelVersion": "gpt-4o-2024-11-20",
    "VisionDetail": "auto",
    "MaxBatchSize": 6,
    "TimeoutSeconds": 60,
    "RetryCount": 3,
    "MaxImageSizeBytes": 20971520
  },
  "ImageProcessing": {
    "MaxWidth": 2048,
    "MaxHeight": 2048,
    "JpegQuality": 85,
    "EnableCompression": true
  },
  "Logging": {
    "LogLevel": {
      "ScreenShotRecipe.Infrastructure.Ocr": "Information"
    }
  }
}
```

### Create Options Classes

**File**: `src/ScreenShotRecipe.Infrastructure/Ocr/OcrOptions.cs`

```csharp
namespace ScreenShotRecipe.Infrastructure.Ocr;

public class OcrOptions
{
    public string ApiKey { get; set; }
    public string OcrModel { get; set; } = "gpt-4o";
    public string OcrModelVersion { get; set; } = "gpt-4o-2024-11-20";
    public string VisionDetail { get; set; } = "auto";
    public int MaxBatchSize { get; set; } = 6;
    public int TimeoutSeconds { get; set; } = 60;
    public int RetryCount { get; set; } = 3;
    public long MaxImageSizeBytes { get; set; } = 20_971_520; // 20 MB
}

public class ImageProcessingOptions
{
    public int MaxWidth { get; set; } = 2048;
    public int MaxHeight { get; set; } = 2048;
    public int JpegQuality { get; set; } = 85;
    public bool EnableCompression { get; set; } = true;
}
```

---

## 4️⃣ Implement IRecipeOcrService

### Create `GptFourOOcrClient.cs`

**File**: `src/ScreenShotRecipe.Infrastructure/Ocr/GptFourOOcrClient.cs`

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using ScreenShotRecipe.Domain.Interfaces;

namespace ScreenShotRecipe.Infrastructure.Ocr;

public class GptFourOOcrClient : IRecipeOcrService
{
    private readonly ChatClient _client;
    private readonly OcrOptions _options;
    private readonly ILogger<GptFourOOcrClient> _logger;
    private readonly ImagePreprocessor _preprocessor;

    public GptFourOOcrClient(
        ChatClient client,
        IOptions<OcrOptions> options,
        IOptions<ImageProcessingOptions> processingOptions,
        ILogger<GptFourOOcrClient> logger)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _preprocessor = new ImagePreprocessor(processingOptions?.Value ?? new ImageProcessingOptions());
    }

    public async Task<IReadOnlyList<OcrResult>> ExtractTextAsync(
        IReadOnlyList<ImageData> images,
        CancellationToken cancellationToken = default)
    {
        if (images == null || images.Count == 0)
            throw new ArgumentException("At least one image required.", nameof(images));
        
        if (images.Count > _options.MaxBatchSize)
            throw new InvalidOperationException(
                $"Batch size ({images.Count}) exceeds maximum ({_options.MaxBatchSize}).");

        _logger.LogInformation("Starting OCR for {ImageCount} images via GPT-4o", images.Count);

        try
        {
            // Preprocess images
            var preprocessedImages = new List<(byte[], ImageData)>();
            foreach (var image in images)
            {
                var processed = await _preprocessor.PreprocessAsync(image.Content);
                preprocessedImages.Add((processed, image));
            }

            // Build vision message
            var messages = new List<ChatMessage>();
            
            // System prompt
            messages.Add(new SystemChatMessage(
                "You are an expert OCR assistant. Extract all text from the provided recipe images. " +
                "Return results as JSON: {\"images\": [{\"order\": 1, \"text\": \"...\", \"language\": \"en\", \"confidence\": 0.95}]}"));
            
            // User message with images
            var userContent = new List<ChatMessageContentPart>();
            userContent.Add(new TextChatMessageContentPart(
                "Extract text from all images in order, preserving formatting."));
            
            for (int i = 0; i < preprocessedImages.Count; i++)
            {
                var (processedBytes, originalImage) = preprocessedImages[i];
                userContent.Add(new ImageChatMessageContentPart(
                    new BinaryData(processedBytes),
                    originalImage.MediaType));
            }
            
            messages.Add(new UserChatMessage(userContent));

            // Call GPT-4o
            var response = await _client.CompleteChartAsync(
                new ChatCompletionOptions
                {
                    Model = _options.OcrModelVersion,
                    Temperature = 0,  // Deterministic OCR
                    MaxTokens = 4000,
                    ResponseFormat = ChatCompletionResponseFormat.Json,
                },
                messages,
                cancellationToken);

            _logger.LogInformation("GPT-4o OCR completed: {TokenUsage}", response.Usage?.ToString() ?? "unknown");

            // Parse and validate response
            var results = ParseOcrResponse(response.Content[0].Text, images);
            return results;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("GPT-4o OCR request cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GPT-4o OCR failed");
            throw new InvalidOperationException("OCR extraction failed.", ex);
        }
    }

    private IReadOnlyList<OcrResult> ParseOcrResponse(string responseText, IReadOnlyList<ImageData> images)
    {
        try
        {
            // Parse JSON response (pseudo-code; use actual JSON library)
            var results = new List<OcrResult>();
            
            // TODO: Parse JSON and create OcrResult records
            // Expected format: {images: [{order: 0, text: "...", language: "en", confidence: 0.95}]}
            
            return results.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse OCR response");
            throw new InvalidOperationException("OCR response parsing failed.", ex);
        }
    }
}
```

### Create `ImagePreprocessor.cs`

**File**: `src/ScreenShotRecipe.Infrastructure/Ocr/ImagePreprocessor.cs`

```csharp
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace ScreenShotRecipe.Infrastructure.Ocr;

public class ImagePreprocessor
{
    private readonly ImageProcessingOptions _options;

    public ImagePreprocessor(ImageProcessingOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<byte[]> PreprocessAsync(byte[] imageBytes)
    {
        if (imageBytes == null || imageBytes.Length == 0)
            throw new ArgumentException("Image data cannot be empty.", nameof(imageBytes));
        
        if (imageBytes.Length > _options.MaxImageSizeBytes)
            throw new InvalidOperationException(
                $"Image size ({imageBytes.Length}) exceeds maximum ({_options.MaxImageSizeBytes}).");

        using var image = Image.Load(imageBytes);

        // Resize if necessary
        if (image.Width > _options.MaxWidth || image.Height > _options.MaxHeight)
        {
            image.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new Size(_options.MaxWidth, _options.MaxHeight),
                Mode = ResizeMode.Max
            }));
        }

        // Encode as JPEG with compression
        using var output = new MemoryStream();
        await image.SaveAsJpegAsync(output, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder
        {
            Quality = _options.JpegQuality
        });

        return output.ToArray();
    }
}
```

---

## 5️⃣ Register in Dependency Injection

### Update `Program.cs`

**File**: `src/ScreenShotRecipe.Web/Program.cs`

```csharp
using ScreenShotRecipe.Infrastructure.Ocr;
using ScreenShotRecipe.Domain.Interfaces;

var builder = WebApplicationBuilder.CreateBuilder(args);

// Configure options
builder.Services.Configure<OcrOptions>(builder.Configuration.GetSection("OpenAI"));
builder.Services.Configure<ImageProcessingOptions>(builder.Configuration.GetSection("ImageProcessing"));

// Add OpenAI client
var openAiKey = builder.Configuration["OpenAI:ApiKey"] 
    ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("OpenAI API key not configured.");

builder.Services.AddSingleton(new ChatClient("gpt-4o", openAiKey));

// Register OCR service
builder.Services.AddScoped<IRecipeOcrService, GptFourOOcrClient>();

// Rest of configuration...
var app = builder.Build();
app.Run();
```

---

## 6️⃣ Use in ImportService

### Update `ImportService.cs`

**File**: `src/ScreenShotRecipe.Application/Services/ImportService.cs`

```csharp
using ScreenShotRecipe.Domain.Interfaces;

public class ImportService
{
    private readonly IRecipeOcrService _ocrService;
    private readonly ILLMParser _parser;
    private readonly IStorage _storage;
    private readonly IRecipeRepository _recipeRepository;
    private readonly ILogger<ImportService> _logger;

    public ImportService(
        IRecipeOcrService ocrService,
        ILLMParser parser,
        IStorage storage,
        IRecipeRepository recipeRepository,
        ILogger<ImportService> logger)
    {
        _ocrService = ocrService;
        _parser = parser;
        _storage = storage;
        _recipeRepository = recipeRepository;
        _logger = logger;
    }

    public async Task<RecipeDto> ImportRecipeAsync(
        IReadOnlyList<byte[]> imageBytes,
        CancellationToken cancellationToken = default)
    {
        // 1. Prepare images for OCR
        var images = new List<ImageData>();
        for (int i = 0; i < imageBytes.Count; i++)
        {
            images.Add(new ImageData(
                imageBytes[i],
                "image/jpeg",
                $"recipe_{i}.jpg",
                i
            ));
        }

        // 2. Extract text via GPT-4o OCR
        _logger.LogInformation("Starting OCR extraction for {ImageCount} images", images.Count);
        var ocrResults = await _ocrService.ExtractTextAsync(images, cancellationToken);

        // 3. Concatenate OCR results in order
        var concatenatedText = string.Join("\n---PAGE BREAK---\n",
            ocrResults.OrderBy(r => r.PageOrder).Select(r => r.ExtractedText));

        _logger.LogInformation("OCR extraction complete. Parsing recipe...");

        // 4. Parse via LLM
        var recipeDto = await _parser.ParseRecipeAsync(concatenatedText, cancellationToken);

        // 5. Persist recipe
        var recipe = MapDtoToEntity(recipeDto);
        await _recipeRepository.SaveAsync(recipe, cancellationToken);

        _logger.LogInformation("Recipe import completed: {RecipeTitle}", recipe.Title);
        return recipeDto;
    }
}
```

---

## 7️⃣ Testing

### Mock in Unit Tests

**File**: `tests/ScreenShotRecipe.Tests/ImportServiceTests.cs`

```csharp
using Moq;
using Xunit;

public class ImportServiceTests
{
    [Fact]
    public async Task ImportRecipeAsync_WithValidImages_ReturnsRecipe()
    {
        // Arrange
        var mockOcr = new Mock<IRecipeOcrService>();
        mockOcr
            .Setup(x => x.ExtractTextAsync(It.IsAny<IReadOnlyList<ImageData>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<OcrResult>
            {
                new("2 cups flour\n1 egg", "en", 0.95m, "recipe_0.jpg", 0),
                new("1. Mix ingredients\n2. Bake", "en", 0.92m, "recipe_1.jpg", 1)
            });

        var mockParser = new Mock<ILLMParser>();
        mockParser
            .Setup(x => x.ParseRecipeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RecipeDto { Title = "Test Recipe" });

        var service = new ImportService(mockOcr.Object, mockParser.Object, /* ... */);

        // Act
        var result = await service.ImportRecipeAsync(new[] { imageBytes }, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Recipe", result.Title);
        mockOcr.Verify(x => x.ExtractTextAsync(It.IsAny<IReadOnlyList<ImageData>>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
```

---

## 8️⃣ Troubleshooting

| Issue | Cause | Solution |
|-------|-------|----------|
| "API key not found" | Missing OPENAI_API_KEY env var | Set `$env:OPENAI_API_KEY = "sk-..."` |
| 401 Unauthorized | Invalid or expired API key | Regenerate key in OpenAI Platform |
| 429 Too Many Requests | Rate limit exceeded | Increase delay between requests or upgrade OpenAI plan |
| Timeout | Image too large or network slow | Reduce image size via preprocessing |
| JSON parse error | Invalid GPT-4o response | Check response format; add error logging |

---

## ✅ Verification Checklist

- [ ] OpenAI API key configured and verified
- [ ] NuGet packages installed (OpenAI, ImageSharp)
- [ ] OcrOptions and ImageProcessingOptions configured in appsettings.json
- [ ] GptFourOOcrClient implemented in Infrastructure/Ocr/
- [ ] ImagePreprocessor handles image compression
- [ ] IRecipeOcrService registered in DI (Program.cs)
- [ ] ImportService updated to use _ocrService
- [ ] Unit tests mock IRecipeOcrService
- [ ] Integration test calls real GPT-4o API (optional, costs money)
- [ ] README.md updated with GPT-4o setup instructions

---

## Next Steps

1. Implement `ILLMParser` if not already done (can be same OpenAI client with different system prompt)
2. Add Batch API support for deferred processing (Phase 2)
3. Implement caching for duplicate images
4. Add monitoring/telemetry for API call costs
5. Create CI/CD pipeline with integration tests

**Estimated Implementation Time**:
- Basic OCR integration: ~4 hours
- With preprocessing & error handling: ~6 hours
- With tests and documentation: ~8 hours

---

## Resources

- [OpenAI API Documentation](https://platform.openai.com/docs)
- [OpenAI .NET GitHub](https://github.com/openai/openai-dotnet)
- [GPT-4O Vision Guide](https://platform.openai.com/docs/guides/vision)
- [SixLabors ImageSharp](https://github.com/SixLabors/ImageSharp)
- [Clean Architecture in .NET](https://www.microsoft.com/en-us/research/publication/clean-architecture-guidelines)
