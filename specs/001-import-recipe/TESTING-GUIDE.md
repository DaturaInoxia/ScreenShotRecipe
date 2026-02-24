# Testing Guide: Fake vs Real Implementations

**Purpose**: Guide for writing tests that leverage fake implementations  
**Scope**: Unit tests, integration tests, end-to-end tests

---

## Overview

The testing strategy uses fake implementations to:
- ✅ Run tests without external APIs
- ✅ Run tests without costs
- ✅ Run tests deterministically (same input → same output)
- ✅ Run tests in CI/CD without secrets
- ✅ Simulate edge cases (low confidence, parsing errors, etc.)

---

## Unit Testing: IRecipeOcrService

### Test 1: Batch Processing with Deterministic Results

```csharp
using Xunit;
using ScreenShotRecipe.Infrastructure.Ocr;
using ScreenShotRecipe.Domain.Interfaces;

public class FakeRecipeOcrServiceTests
{
    [Fact]
    public async Task ExtractTextAsync_WithSingleImage_ReturnsOcrResult()
    {
        // Arrange
        var service = new FakeRecipeOcrService();
        var images = new[]
        {
            new ImageData(
                content: new byte[] { /* image bytes */ },
                mediaType: "image/jpeg",
                fileName: "recipe_1.jpg",
                pageOrder: 0
            )
        };

        // Act
        var results = await service.ExtractTextAsync(images);

        // Assert
        Assert.Single(results);
        Assert.NotEmpty(results[0].ExtractedText);
        Assert.Equal("en", results[0].DetectedLanguage);
        Assert.Equal(0.92m, results[0].Confidence);
        Assert.Equal("recipe_1.jpg", results[0].ImageFileName);
        Assert.Equal(0, results[0].PageOrder);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(6)]
    public async Task ExtractTextAsync_WithVariousBatchSizes_ReturnsSamePageOrder(int batchSize)
    {
        // Arrange
        var service = new FakeRecipeOcrService();
        var images = Enumerable.Range(0, batchSize)
            .Select(i => new ImageData(
                content: new byte[] { },
                mediaType: "image/jpeg",
                fileName: $"page_{i}.jpg",
                pageOrder: i
            ))
            .ToList();

        // Act
        var results = await service.ExtractTextAsync(images);

        // Assert
        Assert.Equal(batchSize, results.Count);
        for (int i = 0; i < batchSize; i++)
        {
            Assert.Equal(i, results[i].PageOrder);
            Assert.Equal($"page_{i}.jpg", results[i].ImageFileName);
        }
    }

    [Fact]
    public async Task ExtractTextAsync_WithEmptyList_ThrowsArgumentException()
    {
        // Arrange
        var service = new FakeRecipeOcrService();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => service.ExtractTextAsync(new List<ImageData>())
        );
    }

    [Fact]
    public async Task ExtractTextAsync_WithTooManyImages_ThrowsInvalidOperationException()
    {
        // Arrange
        var service = new FakeRecipeOcrService();
        var images = Enumerable.Range(0, 7)  // More than max 6
            .Select(i => new ImageData(
                content: new byte[] { },
                mediaType: "image/jpeg",
                fileName: $"page_{i}.jpg",
                pageOrder: i
            ))
            .ToList();

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.ExtractTextAsync(images)
        );
    }
}
```

---

## Unit Testing: ILLMParser

### Test 2: Parsing with Deterministic Confidence

```csharp
using Xunit;
using ScreenShotRecipe.Infrastructure.Parsing;
using ScreenShotRecipe.Domain.Interfaces;

public class FakeLLMParserTests
{
    [Fact]
    public async Task ParseAsync_WithWellFormedRecipeText_ReturnsStructuredRecipe()
    {
        // Arrange
        var parser = new FakeLLMParser();
        var ocrText = @"Chocolate Chip Cookies

INGREDIENTS:
2 1/4 cups all-purpose flour
1 tsp baking soda
1 tsp salt

INSTRUCTIONS:
1. Preheat oven to 375°F
2. Mix dry ingredients
3. Bake for 9-11 minutes";

        // Act
        var result = await parser.ParseAsync(ocrText);

        // Assert
        Assert.NotNull(result.Recipe);
        Assert.Equal("Chocolate Chip Cookies", result.Recipe.Title);
        Assert.True(result.Recipe.Ingredients.Count >= 3);
        Assert.True(result.Recipe.Steps.Count >= 3);
        Assert.InRange(result.Confidence, 0.5, 0.95);
    }

    [Fact]
    public async Task ParseAsync_WithConfidenceOverride_ReturnsConfiguredConfidence()
    {
        // Arrange
        var parser = new FakeLLMParser(forcedConfidenceLevel: 75);
        var ocrText = "Simple Recipe\nINGREDIENTS:\n1 egg\nINSTRUCTIONS:\n1. Cook it";

        // Act
        var result = await parser.ParseAsync(ocrText);

        // Assert
        Assert.Equal(0.75, result.Confidence);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(50)]
    [InlineData(90)]
    public async Task ParseAsync_WithVariousConfidenceLevels_ReturnsExpectedConfidence(int level)
    {
        // Arrange
        var parser = new FakeLLMParser(forcedConfidenceLevel: level);
        var ocrText = "Recipe\nINGREDIENTS:\nflour\nINSTRUCTIONS:\nmix";

        // Act
        var result = await parser.ParseAsync(ocrText);

        // Assert
        Assert.Equal(level / 100.0, result.Confidence);
    }

    [Fact]
    public async Task ParseAsync_WithLowQualityText_CalculatesLowerConfidence()
    {
        // Arrange
        var parser = new FakeLLMParser();  // No override - auto-calculate
        var poorQualityText = "Some text without proper structure";

        // Act
        var result = await parser.ParseAsync(poorQualityText);

        // Assert
        Assert.True(result.Confidence < 0.85);  // Lower than well-structured recipe
    }

    [Fact]
    public async Task ParseAsync_ExtractsIngredientQuantitiesCorrectly()
    {
        // Arrange
        var parser = new FakeLLMParser();
        var ocrText = @"Recipe
INGREDIENTS:
2 1/4 cups flour
1 tablespoon butter
3 large eggs";

        // Act
        var result = await parser.ParseAsync(ocrText);

        // Assert
        var flourIng = result.Recipe.Ingredients.FirstOrDefault(i => i.Name.Contains("flour"));
        Assert.NotNull(flourIng);
        Assert.Equal(2.25m, flourIng.Quantity);
        Assert.Equal("cups", flourIng.Unit);
    }
}
```

---

## Integration Testing: Full Import Workflow

### Test 3: End-to-End Fake Pipeline

```csharp
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using ScreenShotRecipe.Application.Services;
using ScreenShotRecipe.Domain.Interfaces;
using ScreenShotRecipe.Infrastructure.Ocr;
using ScreenShotRecipe.Infrastructure.Parsing;

public class ImportServiceIntegrationTests
{
    private ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        
        // Register fake implementations
        services.AddScoped<IRecipeOcrService, FakeRecipeOcrService>();
        services.AddSingleton<ILLMParser>(_ => new FakeLLMParser());
        services.AddScoped<ImportService>();
        
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task ImportImagesAsync_WithFakeServices_ReturnsCompleteRecipe()
    {
        // Arrange
        var provider = CreateServiceProvider();
        var importService = provider.GetRequiredService<ImportService>();
        
        var imageList = new List<(byte[], string)>
        {
            (new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "recipe_1.jpg"),  // PNG header
            (new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "recipe_2.jpg")
        };

        // Act
        var result = await importService.ImportImagesAsync(imageList);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Title);
        Assert.True(result.Ingredients.Count > 0);
        Assert.True(result.Steps.Count > 0);
        Assert.NotEmpty(result.Tags);
    }

    [Fact]
    public async Task ImportImagesAsync_WithLowConfidenceOverride_ReturnsWarnings()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IRecipeOcrService, FakeRecipeOcrService>();
        services.AddSingleton<ILLMParser>(_ => new FakeLLMParser(forcedConfidenceLevel: 55));
        services.AddScoped<ImportService>();
        
        var provider = services.BuildServiceProvider();
        var importService = provider.GetRequiredService<ImportService>();
        
        var imageList = new List<(byte[], string)>
        {
            (new byte[] { 0xFF, 0xD8, 0xFF }, "recipe.jpg")  // JPEG header
        };

        // Act
        var result = await importService.ImportImagesAsync(imageList);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0.55, result.Confidence);  // Low confidence
    }
}
```

---

## Configuration-Based Testing

### Test 4: Testing with Configuration

```csharp
using Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScreenShotRecipe.Infrastructure.Config;

public class ServiceImplementationConfigurationTests
{
    [Fact]
    public void ServiceImplementationOptions_FromConfiguration_LoadsCorrectly()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                { "ServiceImplementation:UseRealOcr", "false" },
                { "ServiceImplementation:UseRealLlmParser", "false" },
                { "ServiceImplementation:FakeLLMParserConfidenceOverride", "75" }
            })
            .Build();

        var options = new ServiceImplementationOptions();
        config.GetSection(ServiceImplementationOptions.SectionName).Bind(options);

        // Assert
        Assert.False(options.UseRealOcr);
        Assert.False(options.UseRealLlmParser);
        Assert.Equal(75, options.FakeLLMParserConfidenceOverride);
    }

    [Fact]
    public void ServiceImplementationOptions_EnvironmentVariableOverride_AppliesCorrectly()
    {
        // Arrange
        Environment.SetEnvironmentVariable("OCR_USE_REAL", "true");
        Environment.SetEnvironmentVariable("PARSER_CONFIDENCE_OVERRIDE", "85");
        
        var options = new ServiceImplementationOptions
        {
            UseRealOcr = false,
            FakeLLMParserConfidenceOverride = 50
        };

        // Act
        options.ApplyEnvironmentOverrides();

        // Assert
        Assert.True(options.UseRealOcr);
        Assert.Equal(85, options.FakeLLMParserConfidenceOverride);
        
        // Cleanup
        Environment.SetEnvironmentVariable("OCR_USE_REAL", null);
        Environment.SetEnvironmentVariable("PARSER_CONFIDENCE_OVERRIDE", null);
    }
}
```

---

## CI/CD Integration

### GitHub Actions Example

```yaml
name: Test with Fake Services

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      
      - name: Restore dependencies
        run: dotnet restore
      
      - name: Build
        run: dotnet build --configuration Release
      
      - name: Run tests (all fake, no external dependencies)
        run: dotnet test --configuration Release --no-build
        env:
          # All tests use fake implementations by default
          OCR_USE_REAL: false
          PARSER_USE_REAL: false
```

---

## Testing Best Practices

### 1. Always Use Fakes in Unit Tests

```csharp
// ✅ Good: Fast, deterministic, no external dependencies
[Fact]
public async Task MyTest()
{
    var ocrService = new FakeRecipeOcrService();
    // ...
}

// ❌ Avoid: Real API dependency, costly, non-deterministic
[Fact]
public async Task MyTest()
{
    var ocrService = new GptFourOOcrClient(...);  // Real
    // ...
}
```

### 2. Test Edge Cases with Configuration

```csharp
// Test low-confidence scenario
var parser = new FakeLLMParser(forcedConfidenceLevel: 30);
var result = await parser.ParseAsync(ocr Text);
Assert.True(result.Confidence < 0.5);  // Widget shows warning
```

### 3. Validate Fake Output Matches Entity Contracts

```csharp
[Fact]
public async Task FakeOcrService_OutputCompliesToContract()
{
    var service = new FakeRecipeOcrService();
    var results = await service.ExtractTextAsync(images);
    
    foreach (var result in results)
    {
        // Validate all required fields
        Assert.NotEmpty(result.ExtractedText);
        Assert.Matches("^[a-z]{2}$", result.DetectedLanguage);  // ISO 639-1
        Assert.InRange(result.Confidence, 0m, 1m);
        Assert.NotEmpty(result.ImageFileName);
        Assert.True(result.PageOrder >= 0);
    }
}
```

### 4. Document Expected Behaviors

```csharp
/// <summary>
/// Fake OCR always returns:
/// - ExtractedText: Appropriate for batch size (cycles through 3 recipes)
/// - Confidence: 0.92 (high, deterministic)
/// - Language: "en" (always English)
/// - PageOrder: Input order preserved
/// </summary>
[Fact]
public async Task DocumentedBehavior_FakeLLMParser()
{
    // ...
}
```

---

## Common Test Scenarios

### Scenario A: Complete Recipe (Happy Path)

```csharp
[Fact]
public async Task CompleteRecipe_WithAllSections_ParsesSuccessfully()
{
    var text = @"Chocolate Cake

INGREDIENTS:
2 cups flour
1 cup sugar
2 eggs

INSTRUCTIONS:
1. Mix dry ingredients
2. Add eggs
3. Bake at 350°F";

    var parser = new FakeLLMParser();
    var result = await parser.ParseAsync(text);
    
    Assert.Equal("Chocolate Cake", result.Recipe.Title);
    Assert.True(result.Confidence > 0.85);
}
```

### Scenario B: Minimal Recipe

```csharp
[Fact]
public async Task MinimalRecipe_WithBasicSections_ParsesWithLowerConfidence()
{
    var text = @"Simple

INGREDIENTS:
1 flour

INSTRUCTIONS:
1. Mix";

    var parser = new FakeLLMParser();
    var result = await parser.ParseAsync(text);
    
    Assert.NotNull(result.Recipe);
    Assert.True(result.Confidence > 0.5);  // Lower confidence
}
```

### Scenario C: Malformed Recipe

```csharp
[Fact]
public async Task MalformedRecipe_WithoutSections_StillParsesWithLowestConfidence()
{
    var text = "This is not really a recipe";

    var parser = new FakeLLMParser();
    var result = await parser.ParseAsync(text);
    
    Assert.NotNull(result.Recipe);
    Assert.Equal("This is not really a recipe", result.Recipe.Title);
    Assert.True(result.Confidence < 0.7);  // Very low confidence
}
```

---

## Summary

| Test Cost | Latency | Deterministic | Dependencies |
|-----------|---------|---|---|
| **Fake OCR + Fake Parser** | <200ms | ✅ YES | None |
| **Fake OCR + Real Parser** | ~1-2s | ⚠️ No | OpenAI API |
| **Real OCR + Real Parser** | ~30-60s | ❌ No | OpenAI API |

**Recommendation**:
- ✅ 100% of unit tests → Fake implementations
- ✅ 95%+ of integration tests → Fake implementations
- ⚠️ <5% validation tests → Real implementations (only when explicitly testing external service integration)
- 🚀 Production smoke tests → Real implementations (staged environment)
