using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScreenShotRecipe.Application.Services;
using ScreenShotRecipe.Domain.Entities;
using ScreenShotRecipe.Domain.Interfaces;
using ScreenShotRecipe.Infrastructure.Ocr;
using ScreenShotRecipe.Infrastructure.Parsing;
using ScreenShotRecipe.Infrastructure.Extraction;
using ScreenShotRecipe.Infrastructure.Repositories;
using ScreenShotRecipe.Infrastructure.Storage;
using ScreenShotRecipe.Infrastructure.Persistence;
using ScreenShotRecipe.Infrastructure.Config;
using ScreenShotRecipe.Application.Contracts.Dtos;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Events;
using ScreenShotRecipe.Web.Shared;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
var logsPath = Path.Combine(AppContext.BaseDirectory, "Logs");
Directory.CreateDirectory(logsPath); // Ensure Logs directory exists

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()  // Add console output for development
    .WriteTo.File(
        path: Path.Combine(logsPath, "app-log-.txt"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// Configure services
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite("Data Source=app.db"));

builder.Services.AddScoped<IRecipeRepository, RecipeRepository>();
builder.Services.AddScoped<IImageAssetRepository, ImageAssetRepository>();
builder.Services.AddScoped<IImportJobRepository, ImportJobRepository>();
builder.Services.AddScoped<IImagePreprocessor, ImagePreprocessor>();
builder.Services.AddSingleton<IStorage>(provider => 
{
    var logger = provider.GetRequiredService<ILogger<FileSystemStorage>>();
    return new FileSystemStorage("./data/images", logger);
});

// Load and configure service implementation options (real vs fake)
var serviceImplOptions = new ServiceImplementationOptions();
builder.Configuration.GetSection(ServiceImplementationOptions.SectionName).Bind(serviceImplOptions);
serviceImplOptions.ApplyEnvironmentOverrides();

if (!serviceImplOptions.IsValid(out var validationErrors))
{
    foreach (var error in validationErrors)
    {
        Log.Warning($"Configuration Warning: {error}");
    }
}

// Register new multimodal OCR service (IRecipeOcrService)
if (serviceImplOptions.UseRealOcr)
{
    // TODO: Register real OCR implementation (e.g., GptFourOOcrClient)
    // For now, fall back to fake for demonstration
    Log.Information("Using FAKE OCR service (real implementation not yet configured)");
    builder.Services.AddScoped<IRecipeOcrService, FakeRecipeOcrService>();
}
else
{
    Log.Information("Using FAKE OCR service for testing");
    builder.Services.AddScoped<IRecipeOcrService, FakeRecipeOcrService>();
}

// Register unified recipe extraction service (IRecipeExtractionService)
// This replaces the separate OCR + LLM parser pipeline
if (serviceImplOptions.UseRealExtractionService)
{
    Log.Information("Using GPT-4o multimodal recipe extraction service");
    
    // Bind AzureOpenAI configuration with environment variable support
    // Environment variables use double underscore separator: AZURE_OPENAI__APIKEY
    builder.Services.Configure<AzureOpenAIOptions>(builder.Configuration.GetSection(AzureOpenAIOptions.SectionName));
    
    // Log configuration status (no secrets)
    var azureOptions = new AzureOpenAIOptions();
    builder.Configuration.GetSection(AzureOpenAIOptions.SectionName).Bind(azureOptions);
    Log.Information("Azure OpenAI configuration: {Summary}", azureOptions.GetSafeConfigSummary());
    
    builder.Services.AddScoped<IRecipeExtractionService, GptRecipeExtractionService>();
}
else
{
    var confidenceOverride = serviceImplOptions.FakeExtractionConfidenceOverride ?? serviceImplOptions.FakeLLMParserConfidenceOverride;
    Log.Information("Using FAKE recipe extraction service for testing" +
        (confidenceOverride.HasValue ? $" (confidence override: {confidenceOverride}%)" : ""));
    builder.Services.AddScoped<IRecipeExtractionService>(provider =>
        new FakeRecipeExtractionService(
            provider.GetRequiredService<ILogger<FakeRecipeExtractionService>>(),
            confidenceOverride.HasValue ? confidenceOverride.Value / 100m : null));
}

// Register old OCR interface (IOcrClient) for backward compatibility (DEPRECATED)
if (serviceImplOptions.UseRealOcr)
{
    Log.Information("Using FAKE OCR client (real implementation not yet configured)");
    builder.Services.AddSingleton<IOcrClient>(provider => 
        new FakeOcrClient(provider.GetRequiredService<ILogger<FakeOcrClient>>()));
}
else
{
    Log.Information("Using FAKE OCR client for testing");
    builder.Services.AddSingleton<IOcrClient>(provider => 
        new FakeOcrClient(provider.GetRequiredService<ILogger<FakeOcrClient>>()));
}

// Register LLM parser (DEPRECATED - use IRecipeExtractionService instead)
if (serviceImplOptions.UseRealLlmParser)
{
    Log.Information("Using FAKE LLM parser (real implementation not yet configured)");
    builder.Services.AddSingleton<ILLMParser>(provider => 
        new FakeLLMParser(serviceImplOptions.FakeLLMParserConfidenceOverride, provider.GetRequiredService<ILogger<FakeLLMParser>>()));
}
else
{
    Log.Information("Using FAKE LLM parser for testing" + 
        (serviceImplOptions.FakeLLMParserConfidenceOverride.HasValue 
            ? $" (confidence override: {serviceImplOptions.FakeLLMParserConfidenceOverride}%)" 
            : ""));
    builder.Services.AddSingleton<ILLMParser>(provider => 
        new FakeLLMParser(serviceImplOptions.FakeLLMParserConfidenceOverride, provider.GetRequiredService<ILogger<FakeLLMParser>>()));
}

builder.Services.AddScoped<ImportService>();
builder.Services.AddHttpClient();

builder.Services.AddAntiforgery();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        logger.LogInformation("Database initialized successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database initialization failed");
        throw;
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAntiforgery();

app.MapPost("/api/import", async (HttpRequest req, ImportService importService, ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("Import request received. Content-Type: {ContentType}, HasFormContentType: {HasFormContentType}, Content-Length: {ContentLength}", 
            req.ContentType, req.HasFormContentType, req.ContentLength);
        
        if (!req.HasFormContentType) 
        {
            logger.LogWarning("Import request rejected: expected form-data but got {ContentType}", req.ContentType);
            return Results.BadRequest("Expected form-data");
        }

        var form = await req.ReadFormAsync();
        var files = form.Files;
        
        logger.LogInformation("Form processed. Files received: {FileCount}", files.Count);

        if (files.Count == 0)
        {
            return Results.BadRequest("No files provided");
        }

        if (files.Count > 6)
        {
            return Results.BadRequest("Maximum 6 images allowed per import");
        }
        
        var images = new System.Collections.Generic.List<RecipeImageFileDto>();
        var pageOrder = 0;
        foreach (var file in files)
        {
            using var ms = new System.IO.MemoryStream();
            await file.CopyToAsync(ms);
            images.Add(new RecipeImageFileDto
            {
                FileName = file.FileName,
                MediaType = file.ContentType,
                Content = ms.ToArray(),
                PageOrder = pageOrder++
            });
            logger.LogInformation("File processed: {FileName}, Size: {FileSize} bytes", file.FileName, ms.Length);
        }

        var request = new RecipeImportRequestDto
        {
            Images = images,
            IdempotencyKey = form.TryGetValue("idempotencyKey", out var key) ? key.ToString() : null,
            Metadata = new RecipeImportMetadataDto
            {
                Source = form.TryGetValue("source", out var src) ? src.ToString() : "web-upload"
            }
        };

        var response = await importService.ImportImagesAsync(request);
        logger.LogInformation("Import completed. JobId: {JobId}, Status: {Status}", response.ImportJobId, response.Status);
        
        return response.Status == "Failed" 
            ? Results.BadRequest(response) 
            : Results.Ok(response);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error processing import request");
        return Results.BadRequest(new RecipeImportResponseDto
        {
            ImportJobId = Guid.Empty,
            Status = "Failed",
            ErrorMessage = ex.Message
        });
    }
});

app.MapGet("/api/import/{id:guid}", async (Guid id, ImportService importService, ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("Fetching import job status: {JobId}", id);
        var response = await importService.GetImportJobStatusAsync(id);
        if (response == null)
        {
            return Results.NotFound();
        }
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error fetching import job: {JobId}", id);
        return Results.BadRequest($"Error: {ex.Message}");
    }
});

app.MapGet("/api/images/{id:guid}", async (Guid id, IImageAssetRepository imageAssetRepo, IStorage storage, ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("Fetching image: {ImageId}", id);
        var imageAsset = await imageAssetRepo.GetByIdAsync(id);
        if (imageAsset == null)
        {
            logger.LogWarning("Image not found: {ImageId}", id);
            return Results.NotFound();
        }

        var imageData = await storage.ReadImageAsync(imageAsset.FilePath);
        if (imageData == null)
        {
            logger.LogWarning("Image file not found at path: {Path}", imageAsset.FilePath);
            return Results.NotFound();
        }

        return Results.File(imageData, imageAsset.MediaType, imageAsset.FileName);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error fetching image: {ImageId}", id);
        return Results.BadRequest($"Error: {ex.Message}");
    }
});

app.MapGet("/api/recipes", async (IRecipeRepository repo, ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("Fetching all recipes");
        var recipes = await repo.GetAllAsync();
        var dtos = recipes.Select(r => new RecipeDto
        {
            Id = r.Id,
            Title = r.Title,
            Notes = r.Notes,
            Ingredients = r.Ingredients.Select(i => new IngredientDto
            {
                RawText = i.RawText,
                Name = i.Name,
                Quantity = i.Quantity,
                Unit = i.Unit
            }).ToList(),
            Steps = r.Steps.Select(s => new StepDto
            {
                Ordinal = s.Ordinal,
                Text = s.Text
            }).ToList(),
            Tags = r.Tags.ToList()
        }).ToList();
        logger.LogInformation("Successfully retrieved {RecipeCount} recipes", dtos.Count);
        return Results.Ok(dtos);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error fetching recipes");
        return Results.BadRequest($"Error: {ex.Message}");
    }
});

app.MapGet("/api/recipes/search", async (string? q, IRecipeRepository repo, ILogger<Program> logger) =>
{
    try
    {
        var query = q ?? "";
        logger.LogInformation("Searching recipes with query: {Query}", query);
        var recipes = await repo.SearchAsync(query);
        var dtos = recipes.Select(r => new RecipeDto
        {
            Id = r.Id,
            Title = r.Title,
            Notes = r.Notes,
            Ingredients = r.Ingredients.Select(i => new IngredientDto
            {
                RawText = i.RawText,
                Name = i.Name,
                Quantity = i.Quantity,
                Unit = i.Unit
            }).ToList(),
            Steps = r.Steps.Select(s => new StepDto
            {
                Ordinal = s.Ordinal,
                Text = s.Text
            }).ToList(),
            Tags = r.Tags.ToList()
        }).ToList();
        logger.LogInformation("Search returned {ResultCount} recipes", dtos.Count);
        return Results.Ok(dtos);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error searching recipes with query: {Query}", q);
        return Results.BadRequest($"Error: {ex.Message}");
    }
});

app.MapGet("/api/recipes/{id:guid}", async (Guid id, IRecipeRepository repo, ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("Fetching recipe with ID: {RecipeId}", id);
        var recipe = await repo.GetAsync(id);
        if (recipe == null)
        {
            logger.LogWarning("Recipe not found: {RecipeId}", id);
            return Results.NotFound();
        }

        var dto = new RecipeDto
        {
            Id = recipe.Id,
            Title = recipe.Title,
            Notes = recipe.Notes,
            Ingredients = recipe.Ingredients.Select(i => new IngredientDto
            {
                RawText = i.RawText,
                Name = i.Name,
                Quantity = i.Quantity,
                Unit = i.Unit
            }).ToList(),
            Steps = recipe.Steps.Select(s => new StepDto
            {
                Ordinal = s.Ordinal,
                Text = s.Text
            }).ToList(),
            Tags = recipe.Tags.ToList(),
            OverallConfidence = recipe.OverallConfidence,
            ConfidenceNotes = recipe.ConfidenceNotes,
            Version = recipe.Version,
            ImageIds = recipe.ImportJob?.ImageAssets?.Select(a => a.Id).ToList()
        };
        logger.LogInformation("Successfully retrieved recipe: {RecipeTitle} (ID: {RecipeId})", dto.Title, dto.Id);
        return Results.Ok(dto);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error fetching recipe with ID: {RecipeId}", id);
        return Results.BadRequest($"Error: {ex.Message}");
    }
});

app.MapPut("/api/recipes/{id:guid}", async (Guid id, RecipeEditDto editDto, IRecipeRepository repo, ILogger<Program> logger) =>
{
    try
    {
        if (id != editDto.Id)
        {
            return Results.BadRequest("Recipe ID in URL must match ID in body");
        }

        if (string.IsNullOrWhiteSpace(editDto.Title))
        {
            return Results.BadRequest("Title is required");
        }

        if (editDto.Title.Length > 500)
        {
            return Results.BadRequest("Title must be 500 characters or less");
        }

        logger.LogInformation("Updating recipe {RecipeId}, Version {Version}", id, editDto.Version);

        // Convert DTOs to tuples for repository method
        var ingredients = editDto.Ingredients
            .Select(i => (i.RawText, i.Name, i.Quantity, i.Unit))
            .ToList();

        var steps = editDto.Steps
            .Select(s => (s.Ordinal, s.Text))
            .ToList();

        // Let repository handle all entity tracking internally
        var newVersion = await repo.UpdateRecipeAsync(
            id,
            editDto.Version,
            editDto.Title,
            editDto.Notes,
            editDto.Tags,
            ingredients,
            steps);

        if (newVersion == null)
        {
            return Results.Conflict(new { error = "Version conflict - recipe was modified by another user or not found" });
        }

        logger.LogInformation("Recipe updated successfully: {RecipeId}", id);
        return Results.Ok(new { message = "Recipe updated", version = newVersion.Value });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error updating recipe: {RecipeId}", id);
        return Results.BadRequest($"Error: {ex.Message}");
    }
});

app.MapDelete("/api/recipes/{id:guid}", async (Guid id, IRecipeRepository repo, ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("Deleting recipe: {RecipeId}", id);
        var success = await repo.DeleteAsync(id);
        
        if (!success)
        {
            return Results.NotFound();
        }

        logger.LogInformation("Recipe deleted successfully: {RecipeId}", id);
        return Results.Ok(new { message = "Recipe deleted" });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error deleting recipe: {RecipeId}", id);
        return Results.BadRequest($"Error: {ex.Message}");
    }
});

app.MapGet("/health", async (AppDbContext db, ILogger<Program> logger) =>
{
    try
    {
        // Check database connectivity
        var canConnect = await db.Database.CanConnectAsync();
        if (!canConnect)
        {
            return Results.Json(new { status = "unhealthy", database = "disconnected" }, statusCode: 503);
        }

        // Get some stats
        var recipeCount = await db.Recipes.CountAsync();
        var importJobCount = await db.ImportJobs.CountAsync();

        return Results.Ok(new
        {
            status = "healthy",
            database = "connected",
            stats = new
            {
                recipes = recipeCount,
                importJobs = importJobCount
            },
            timestamp = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Health check failed");
        return Results.Json(new { status = "unhealthy", error = ex.Message }, statusCode: 503);
    }
});

app.MapGet("/", () => Results.Redirect("/recipes"));

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
