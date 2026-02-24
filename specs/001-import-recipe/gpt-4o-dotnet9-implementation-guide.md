# GPT-4o OCR Integration - .NET 9 Implementation Guide

## Quick Reference: Core Code Patterns

### 1. Project Setup & Dependencies

```csharp
// .csproj additions for .NET 9
<ItemGroup>
    <PackageReference Include="OpenAI" Version="2.8.0" />
    <PackageReference Include="Azure.Storage.Blobs" Version="12.20.0" />
    <PackageReference Include="SixLabors.ImageSharp" Version="3.0.1" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="9.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration" Version="9.0.0" />
    <PackageReference Include="Serilog" Version="3.1.1" />
</ItemGroup>
```

### 2. Dependency Injection Setup (Program.cs)

```csharp
using Microsoft.Extensions.DependencyInjection;
using OpenAI;
using OpenAI.Chat;

var builder = WebApplication.CreateBuilder(args);

// Register OpenAI ChatClient as singleton
builder.Services.AddSingleton<ChatClient>(serviceProvider =>
{
    var apiKey = builder.Configuration["OpenAI:ApiKey"] 
        ?? throw new InvalidOperationException("OpenAI:ApiKey not configured");
    var model = builder.Configuration.GetValue("OpenAI:Model", "gpt-4o");
    
    return new ChatClient(model, new ApiKeyCredential(apiKey));
});

// Register recipe processing service
builder.Services.AddScoped<IRecipeOcrService, RecipeOcrService>();
builder.Services.AddScoped<IImagePreprocessor, ImagePreprocessor>();

var app = builder.Build();
app.Run();
```

### 3. Configuration (appsettings.json)

```json
{
  "OpenAI": {
    "ApiKey": "${OPENAI_API_KEY}",
    "Model": "gpt-4o",
    "VisionDetail": "auto",
    "Timeout": 300
  },
  "ImageProcessing": {
    "MaxDimension": 2048,
    "JpegQuality": 0.85,
    "MaxFileSizeKb": 500,
    "CloudStorageBaseUrl": "https://yourcdn.blob.core.windows.net/"
  },
  "Logging": {
    "LogLevel": {
      "OpenAI": "Debug"
    }
  }
}
```

### 4. Core Service Interfaces

```csharp
using OpenAI.Chat;

namespace ScreenShotRecipe.Application.Services;

/// <summary>
/// Service interface for OCR processing using GPT-4o vision
/// </summary>
public interface IRecipeOcrService
{
    /// <summary>
    /// Extracts recipe data from a single image
    /// </summary>
    Task<RecipeExtractionResult> ExtractRecipeFromImageAsync(
        string imageInput, 
        ImageSourceType sourceType,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Processes multiple recipe images in a single request for efficiency
    /// </summary>
    Task<List<RecipeExtractionResult>> ExtractFromMultipleImagesAsync(
        List<string> imageInputs,
        ImageSourceType sourceType,
        CancellationToken cancellationToken = default);
}

public interface IImagePreprocessor
{
    /// <summary>
    /// Preprocesses image for optimal GPT-4o processing
    /// </summary>
    Task<ProcessedImage> PrepareImageAsync(
        string sourceInput,
        ImageSourceType sourceType,
        CancellationToken cancellationToken = default);
}

public enum ImageSourceType
{
    Url,        // HTTPS URL
    Base64,     // Base64-encoded data URI
    FilePath    // Local file path (to be uploaded or converted)
}

public class ProcessedImage
{
    public string Identifier { get; set; } // URL or base64 data URI
    public ImageSourceType Type { get; set; }
    public int ApproximateTokens { get; set; }
}

public class RecipeExtractionResult
{
    public string SourceImage { get; set; }
    public bool Success { get; set; }
    public string? Title { get; set; }
    public List<string>? Ingredients { get; set; }
    public List<string>? Steps { get; set; }
    public int CookingTime { get; set; }
    public int PreparationTime { get; set; }
    public double? Servings { get; set; }
    public string? CuisineType { get; set; }
    public string? DifficultyLevel { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ProcessedAt { get; set; }
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
}
```

### 5. Image Preprocessing Implementation

```csharp
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;

namespace ScreenShotRecipe.Infrastructure.Services;

public class ImagePreprocessor : IImagePreprocessor
{
    private readonly IConfiguration _config;
    private readonly ILogger<ImagePreprocessor> _logger;
    
    private readonly int _maxDimension;
    private readonly float _jpegQuality;
    private readonly int _maxFileSizeKb;
    
    public ImagePreprocessor(IConfiguration config, ILogger<ImagePreprocessor> logger)
    {
        _config = config;
        _logger = logger;
        
        _maxDimension = _config.GetValue("ImageProcessing:MaxDimension", 2048);
        _jpegQuality = _config.GetValue("ImageProcessing:JpegQuality", 0.85f);
        _maxFileSizeKb = _config.GetValue("ImageProcessing:MaxFileSizeKb", 500);
    }
    
    public async Task<ProcessedImage> PrepareImageAsync(
        string sourceInput,
        ImageSourceType sourceType,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return sourceType switch
            {
                ImageSourceType.Url => new ProcessedImage 
                { 
                    Identifier = sourceInput,
                    Type = ImageSourceType.Url,
                    ApproximateTokens = 600 // Estimated for medium image
                },
                ImageSourceType.FilePath => await ProcessLocalFileAsync(sourceInput, cancellationToken),
                ImageSourceType.Base64 => ProcessBase64Input(sourceInput),
                _ => throw new ArgumentException("Unknown source type", nameof(sourceType))
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error preprocessing image from {SourceType}", sourceType);
            throw;
        }
    }
    
    private async Task<ProcessedImage> ProcessLocalFileAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Image file not found: {filePath}");
        
        var fileInfo = new FileInfo(filePath);
        
        using var image = await Image.LoadAsync(filePath, cancellationToken);
        
        // Resize if necessary
        var needsResize = image.Width > _maxDimension || image.Height > _maxDimension;
        if (needsResize)
        {
            var scale = Math.Min((float)_maxDimension / image.Width, (float)_maxDimension / image.Height);
            var newWidth = (int)(image.Width * scale);
            var newHeight = (int)(image.Height * scale);
            
            image.Mutate(i => i.Resize(newWidth, newHeight, KnownResamplers.Lanczos3));
            _logger.LogDebug("Resized image from {Original} to {New}", 
                $"{fileInfo.Length / 1024}KB", $"{newWidth}x{newHeight}");
        }
        
        // Compress to target size
        var jpegEncoder = new JpegEncoder { Quality = (int)(_jpegQuality * 100) };
        using var outputStream = new MemoryStream();
        await image.SaveAsJpegAsync(outputStream, jpegEncoder, cancellationToken);
        
        var compressedSize = outputStream.Length / 1024; // KB
        if (compressedSize > _maxFileSizeKb)
        {
            _logger.LogWarning("Compressed image size ({Size}KB) exceeds target ({Max}KB)", 
                compressedSize, _maxFileSizeKb);
        }
        
        // Convert to base64 only if < 50KB (otherwise use URL upload)
        if (compressedSize < 50)
        {
            outputStream.Position = 0;
            var data = Convert.ToBase64String(outputStream.ToArray());
            return new ProcessedImage
            {
                Identifier = $"data:image/jpeg;base64,{data}",
                Type = ImageSourceType.Base64,
                ApproximateTokens = 85 // Low detail for small images
            };
        }
        else
        {
            // For larger files, upload to cloud storage or use URL
            var uploadedUrl = await UploadToCloudStorageAsync(outputStream, filePath);
            return new ProcessedImage
            {
                Identifier = uploadedUrl,
                Type = ImageSourceType.Url,
                ApproximateTokens = 600
            };
        }
    }
    
    private ProcessedImage ProcessBase64Input(string base64Input)
    {
        // Validate base64 format
        if (!base64Input.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Invalid base64 image format. Expected data URI.", nameof(base64Input));
        }
        
        return new ProcessedImage
        {
            Identifier = base64Input,
            Type = ImageSourceType.Base64,
            ApproximateTokens = 100
        };
    }
    
    private async Task<string> UploadToCloudStorageAsync(
        Stream imageStream,
        string originalFileName)
    {
        // Implementation depends on your storage (Azure Blob, S3, etc.)
        // Placeholder for Azure Blob Storage
        var blobName = $"recipes/{Guid.NewGuid()}-{Path.GetFileName(originalFileName)}";
        
        _logger.LogDebug("Uploading image to cloud storage: {BlobName}", blobName);
        
        // TODO: Implement actual upload
        var baseUrl = _config.GetValue("ImageProcessing:CloudStorageBaseUrl", "");
        return $"{baseUrl}{blobName}";
    }
}
```

### 6. Main Recipe OCR Service

```csharp
using OpenAI.Chat;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ScreenShotRecipe.Application.Services;

public class RecipeOcrService : IRecipeOcrService
{
    private readonly ChatClient _chatClient;
    private readonly IImagePreprocessor _imagePreprocessor;
    private readonly IConfiguration _config;
    private readonly ILogger<RecipeOcrService> _logger;
    
    private readonly ChatImageDetailLevel _detailLevel;
    
    public RecipeOcrService(
        ChatClient chatClient,
        IImagePreprocessor imagePreprocessor,
        IConfiguration config,
        ILogger<RecipeOcrService> logger)
    {
        _chatClient = chatClient;
        _imagePreprocessor = imagePreprocessor;
        _config = config;
        _logger = logger;
        
        var detailStr = _config.GetValue("OpenAI:VisionDetail", "auto");
        _detailLevel = detailStr switch
        {
            "low" => ChatImageDetailLevel.Low,
            "high" => ChatImageDetailLevel.High,
            _ => ChatImageDetailLevel.Auto
        };
    }
    
    public async Task<RecipeExtractionResult> ExtractRecipeFromImageAsync(
        string imageInput,
        ImageSourceType sourceType,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("Starting recipe extraction from {SourceType}: {Input}", 
                sourceType, imageInput[..50]);
            
            // Preprocess image
            var processedImage = await _imagePreprocessor.PrepareImageAsync(
                imageInput, sourceType, cancellationToken);
            
            // Build message with vision content
            var userMessage = BuildVisionMessage(processedImage);
            
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(GetSystemPrompt()),
                userMessage
            };
            
            // Call GPT-4o with vision
            var response = await _chatClient.CompleteChatAsync(messages, cancellationToken: cancellationToken);
            
            // Parse response
            var result = ParseRecipeFromResponse(response, imageInput);
            result.ProcessedAt = DateTime.UtcNow;
            result.InputTokens = response.Usage?.InputTokens ?? 0;
            result.OutputTokens = response.Usage?.CompletionTokens ?? 0;
            
            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation(
                "Recipe extraction completed in {Duration}ms. " +
                "Tokens: {Input}→{Output}. Title: {Title}",
                duration.TotalMilliseconds, result.InputTokens, result.OutputTokens, result.Title);
            
            return result;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning("Recipe extraction cancelled after {Duration}ms", 
                (DateTime.UtcNow - startTime).TotalMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting recipe from image");
            throw;
        }
    }
    
    public async Task<List<RecipeExtractionResult>> ExtractFromMultipleImagesAsync(
        List<string> imageInputs,
        ImageSourceType sourceType,
        CancellationToken cancellationToken = default)
    {
        if (imageInputs.Count > 10)
        {
            _logger.LogWarning("Processing {Count} images. Consider using Batch API for {Threshold}+ images",
                imageInputs.Count, 50);
        }
        
        // For 2-10 images, process in parallel with reasonable concurrency
        var semaphore = new System.Threading.SemaphoreSlim(3); // Max 3 concurrent
        
        var tasks = imageInputs.Select(async imageInput =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await ExtractRecipeFromImageAsync(imageInput, sourceType, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });
        
        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }
    
    private ChatMessage BuildVisionMessage(ProcessedImage processedImage)
    {
        var contentParts = new List<ChatMessageContentPart>
        {
            ChatMessageContentPart.CreateTextPart(GetExtractionPrompt())
        };
        
        if (processedImage.Type == ImageSourceType.Base64)
        {
            // Base64 data URI
            contentParts.Add(ChatMessageContentPart.CreateImagePart(
                new Uri(processedImage.Identifier),
                _detailLevel));
        }
        else if (processedImage.Type == ImageSourceType.Url)
        {
            // HTTPS URL
            contentParts.Add(ChatMessageContentPart.CreateImagePart(
                new Uri(processedImage.Identifier),
                _detailLevel));
        }
        
        return new UserChatMessage(contentParts.ToArray());
    }
    
    private string GetSystemPrompt()
    {
        return @"You are an expert OCR system specialized in extracting recipe information from images.
Your task is to accurately extract and parse recipe details from photographs or scanned documents.
Always return structured data in JSON format.
When information is missing, use null.
Focus on accuracy over completeness.";
    }
    
    private string GetExtractionPrompt()
    {
        return @"Extract all recipe information from this image. Return a JSON object with these fields:
- title: string (recipe name)
- ingredients: array of strings (formatted as 'quantity unit ingredient')
- steps: array of strings (cooking instructions)
- cookingTime: number (minutes, or null if not specified)
- preparationTime: number (minutes, or null if not specified)
- servings: number (or null)
- cuisineType: string (or null)
- difficultyLevel: string ('easy'|'medium'|'hard', or null)

Return ONLY valid JSON, no other text.";
    }
    
    private RecipeExtractionResult ParseRecipeFromResponse(
        ChatCompletion response,
        string sourceImage)
    {
        var result = new RecipeExtractionResult
        {
            SourceImage = sourceImage,
            Success = false
        };
        
        try
        {
            var responseText = response.Content[0].Text;
            
            // Parse JSON from response
            using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement;
            
            result.Title = root.GetProperty("title").GetString();
            result.Ingredients = root.GetProperty("ingredients")
                .EnumerateArray()
                .Select(e => e.GetString())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            result.Steps = root.GetProperty("steps")
                .EnumerateArray()
                .Select(e => e.GetString())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            
            if (root.TryGetProperty("cookingTime", out var cookTime) && cookTime.ValueKind != JsonValueKind.Null)
                result.CookingTime = cookTime.GetInt32();
            
            if (root.TryGetProperty("preparationTime", out var prepTime) && prepTime.ValueKind != JsonValueKind.Null)
                result.PreparationTime = prepTime.GetInt32();
            
            if (root.TryGetProperty("servings", out var servings) && servings.ValueKind != JsonValueKind.Null)
                result.Servings = servings.GetDouble();
            
            if (root.TryGetProperty("cuisineType", out var cuisine))
                result.CuisineType = cuisine.GetString();
            
            if (root.TryGetProperty("difficultyLevel", out var difficulty))
                result.DifficultyLevel = difficulty.GetString();
            
            result.Success = !string.IsNullOrEmpty(result.Title) && result.Ingredients?.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing recipe JSON from response");
            result.ErrorMessage = $"Parse error: {ex.Message}";
        }
        
        return result;
    }
}
```

### 7. API Controller Usage

```csharp
using Microsoft.AspNetCore.Mvc;
using ScreenShotRecipe.Application.Services;

namespace ScreenShotRecipe.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecipeImportController : ControllerBase
{
    private readonly IRecipeOcrService _recipeOcrService;
    private readonly ILogger<RecipeImportController> _logger;
    
    public RecipeImportController(
        IRecipeOcrService recipeOcrService,
        ILogger<RecipeImportController> logger)
    {
        _recipeOcrService = recipeOcrService;
        _logger = logger;
    }
    
    /// <summary>
    /// Import recipe from image URL
    /// </summary>
    [HttpPost("from-url")]
    public async Task<ActionResult<RecipeExtractionResult>> ImportFromUrl(
        [FromQuery] string imageUrl,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _recipeOcrService.ExtractRecipeFromImageAsync(
                imageUrl,
                ImageSourceType.Url,
                cancellationToken);
            
            if (!result.Success)
                return BadRequest(new { error = "Failed to extract recipe", details = result.ErrorMessage });
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing recipe from URL");
            return StatusCode(500, new { error = "Recipe import failed" });
        }
    }
    
    /// <summary>
    /// Import recipe from uploaded file
    /// </summary>
    [HttpPost("from-file")]
    public async Task<ActionResult<RecipeExtractionResult>> ImportFromFile(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (file.Length == 0)
                return BadRequest("File is empty");
            
            if (file.Length > 20 * 1024 * 1024) // 20MB limit
                return BadRequest("File too large (max 20MB)");
            
            // Save to temp directory
            var tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}_{file.FileName}");
            using (var stream = System.IO.File.Create(tempPath))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }
            
            try
            {
                var result = await _recipeOcrService.ExtractRecipeFromImageAsync(
                    tempPath,
                    ImageSourceType.FilePath,
                    cancellationToken);
                
                if (!result.Success)
                    return BadRequest(new { error = "Failed to extract recipe", details = result.ErrorMessage });
                
                return Ok(result);
            }
            finally
            {
                System.IO.File.Delete(tempPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing recipe from file");
            return StatusCode(500, new { error = "Recipe import failed" });
        }
    }
    
    /// <summary>
    /// Batch import multiple recipes
    /// </summary>
    [HttpPost("batch")]
    public async Task<ActionResult<List<RecipeExtractionResult>>> ImportBatch(
        [FromBody] List<string> imageUrls,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (imageUrls.Count == 0)
                return BadRequest("No images provided");
            
            if (imageUrls.Count > 10)
                return BadRequest("Maximum 10 images per request. Use Batch API for larger imports.");
            
            var results = await _recipeOcrService.ExtractFromMultipleImagesAsync(
                imageUrls,
                ImageSourceType.Url,
                cancellationToken);
            
            var successCount = results.Count(r => r.Success);
            _logger.LogInformation("Batch import completed: {Success}/{Total} successful",
                successCount, results.Count);
            
            return Ok(new { results, successCount, totalCount = results.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in batch recipe import");
            return StatusCode(500, new { error = "Batch import failed" });
        }
    }
}
```

### 8. Unit Testing Example

```csharp
using Moq;
using Xunit;
using OpenAI.Chat;

namespace ScreenShotRecipe.Tests;

public class RecipeOcrServiceTests
{
    private readonly Mock<ChatClient> _mockChatClient;
    private readonly Mock<IImagePreprocessor> _mockPreprocessor;
    private readonly IRecipeOcrService _service;
    
    public RecipeOcrServiceTests()
    {
        _mockChatClient = new Mock<ChatClient>();
        _mockPreprocessor = new Mock<IImagePreprocessor>();
        
        var config = new Mock<IConfiguration>();
        config.Setup(c => c.GetValue(It.IsAny<string>(), It.IsAny<object>()))
            .Returns((string key, object defaultValue) => defaultValue);
        
        var logger = new Mock<ILogger<RecipeOcrService>>();
        
        _service = new RecipeOcrService(_mockChatClient.Object, _mockPreprocessor.Object, 
            config.Object, logger.Object);
    }
    
    [Fact]
    public async Task ExtractRecipeFromImageAsync_WithValidImage_ReturnsStructuredRecipe()
    {
        // Arrange
        var imageUrl = "https://example.com/recipe.jpg";
        var expectedJson = @"{
            ""title"": ""Chocolate Cake"",
            ""ingredients"": [""2 cups flour"", ""1 cup sugar""],
            ""steps"": [""Mix ingredients"", ""Bake at 350F""],
            ""cookingTime"": 30,
            ""preparationTime"": 15,
            ""servings"": 8,
            ""cuisineType"": ""Dessert"",
            ""difficultyLevel"": ""medium""
        }";
        
        _mockPreprocessor.Setup(p => p.PrepareImageAsync(It.IsAny<string>(), It.IsAny<ImageSourceType>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProcessedImage { Identifier = imageUrl, Type = ImageSourceType.Url });
        
        var mockResponse = new Mock<ChatCompletion>();
        mockResponse.Setup(r => r.Content).Returns(new[] { new ChatMessageContentPart { Text = expectedJson } });
        mockResponse.Setup(r => r.Usage).Returns(new CompletionTokenUsage { InputTokens = 500, CompletionTokens = 150 });
        
        // Act
        var result = await _service.ExtractRecipeFromImageAsync(imageUrl, ImageSourceType.Url);
        
        // Assert
        Assert.True(result.Success);
        Assert.Equal("Chocolate Cake", result.Title);
        Assert.Equal(2, result.Ingredients?.Count);
        Assert.Equal(30, result.CookingTime);
        Assert.Equal(500, result.InputTokens);
    }
}
```

### 9. Environment Variables (.env)

```bash
# OpenAI Configuration
OPENAI_API_KEY=sk-proj-your-actual-api-key-here
OPENAI_API_ENDPOINT=https://api.openai.com

# Image Processing
IMAGE_PROCESSING_MAX_DIMENSION=2048
IMAGE_PROCESSING_JPEG_QUALITY=0.85
IMAGE_PROCESSING_MAX_FILE_SIZE_KB=500

# Cloud Storage (if using Azure Blob)
AZURE_STORAGE_CONNECTION_STRING=DefaultEndpointsProtocol=https;AccountName=...
AZURE_STORAGE_CONTAINER=recipe-images
```

### 10. Batch API Implementation Pattern

```csharp
// For future implementation of Batch API
public class BatchRecipeProcessor
{
    private readonly OpenAIClient _client;
    
    public async Task<string> SubmitBatchAsync(List<string> imageUrls)
    {
        // 1. Create .jsonl batch file
        var batchLines = imageUrls.Select((url, idx) => JsonSerializer.Serialize(new
        {
            custom_id = $"recipe-{idx}",
            method = "POST",
            url = "/v1/chat/completions",
            body = new
            {
                model = "gpt-4o",
                messages = new[] {
                    new { role = "user", content = new object[] {
                        new { type = "text", text = "Extract recipe:" },
                        new { type = "image_url", image_url = new { url } }
                    }}
                }
            }
        }));
        
        var batchContent = string.Join("\n", batchLines);
        
        // 2. Upload batch file
        var file = await _client.Files.CreateAsync(
            System.Net.Http.StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(batchContent))),
            "batchinput.jsonl");
        
        // 3. Create batch job
        var batch = await _client.Batches.CreateAsync(
            input_file_id: file.Id,
            endpoint: "/v1/chat/completions");
        
        return batch.Id;
    }
    
    public async Task<(bool Completed, List<string> Results)> CheckBatchStatusAsync(string batchId)
    {
        var batch = await _client.Batches.RetrieveAsync(batchId);
        
        if (batch.Status != "completed")
            return (false, new List<string>());
        
        // Download results
        var results = await _client.Files.DownloadAsync(batch.OutputFileId ?? "");
        return (true, results.Split('\n').ToList());
    }
}
```

---

## Deployment Checklist

- [ ] API key securely stored in Azure Key Vault or environment
- [ ] Image preprocessing tested with various recipes
- [ ] Error handling tested (timeout, rate limits, invalid images)
- [ ] Logging configured in production environment
- [ ] Cost monitoring dashboard set up in OpenAI console
- [ ] Fallback service (Azure CV) configured as backup
- [ ] Database schema updated to store OCR metadata
- [ ] API documentation updated
- [ ] Load testing completed for concurrent recipe processing
- [ ] Batch API configured for background processing

---

## Cost Monitoring Query

Track monthly spending:
```sql
SELECT 
    DATE_TRUNC('month', processed_at) as month,
    COUNT(*) as images_processed,
    SUM(input_tokens) as total_input_tokens,
    SUM(output_tokens) as total_output_tokens,
    SUM(input_tokens * 0.000005 + output_tokens * 0.000015) as estimated_cost
FROM recipe_extractions
GROUP BY month
ORDER BY month DESC;
```

