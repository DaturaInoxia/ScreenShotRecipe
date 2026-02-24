using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ScreenShotRecipe.Application.Services;
using ScreenShotRecipe.Domain.Interfaces;
using ScreenShotRecipe.Infrastructure.Ocr;
using ScreenShotRecipe.Infrastructure.Parsing;
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

// Register old OCR interface (IOcrClient) for backward compatibility
if (serviceImplOptions.UseRealOcr)
{
    // TODO: Uncomment when AzureVisionOcrClient is available
    // builder.Services.AddSingleton<IOcrClient, AzureVisionOcrClient>();
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

// Register LLM parser
if (serviceImplOptions.UseRealLlmParser)
{
    // TODO: Uncomment when real parser is available
    // builder.Services.AddSingleton<ILLMParser, AzureOpenAIParser>();
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
        
        var list = new System.Collections.Generic.List<(byte[] Bytes, string FileName)>();
        foreach (var file in files)
        {
            using var ms = new System.IO.MemoryStream();
            await file.CopyToAsync(ms);
            list.Add((ms.ToArray(), file.FileName));
            logger.LogInformation("File processed: {FileName}, Size: {FileSize} bytes", file.FileName, ms.Length);
        }

        var dto = await importService.ImportImagesAsync(list);
        logger.LogInformation("Import completed successfully. Recipe created with Title: {RecipeTitle}", dto.Title);
        return Results.Ok(dto);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error processing import request");
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
            Tags = recipe.Tags.ToList()
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

app.MapGet("/", () => Results.Redirect("/recipes"));

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
