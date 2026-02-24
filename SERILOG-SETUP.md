# Serilog Logging Framework Setup

## Overview
Serilog has been integrated into the ScreenShotRecipe application to provide comprehensive structured logging across all projects and components.

## Configuration Details

### Logging Output
- **Location**: `./Logs/` subdirectory relative to the application's base directory
- **File Pattern**: `app-log-.txt` with daily rolling intervals
- **Retention**: Latest 30 days of logs are retained automatically
- **Log Format**: Structured format with timestamps, log levels, messages, and exceptions

Example log file naming:
- `app-log-20260222.txt`
- `app-log-20260223.txt`
- `app-log-20260224.txt`

### Log Levels
- **Information (Info)**: Standard application events and custom business logic operations
- **Warning (Warn)**: Configuration warnings, missing resources, unusual conditions
- **Error (Error)**: Exceptions and error conditions with full stack traces

### Packages Added

#### ScreenShotRecipe.Web
- `Serilog.AspNetCore` (v8.0.1)
- `Serilog.Sinks.File` (v5.0.0)

#### ScreenShotRecipe.Application
- `Serilog` (v3.1.1)
- `Serilog.Extensions.Logging` (v8.0.0)

#### ScreenShotRecipe.Infrastructure
- `Serilog` (v3.1.1)
- `Serilog.Extensions.Logging` (v8.0.0)

## Classes with Logging Integrated

### Web Layer (Program.cs)
✅ Serilog Configuration - initialized before application build
✅ Service Configuration - logs which implementations are registered  
✅ Database Initialization - logs success/failure
✅ API Endpoints - /api/import, /api/recipes, /api/recipes/{id}
   - Logs incoming requests with content type, file count, form data
   - Logs successful processing with recipe details
   - Logs all errors with full exception details

### Application Layer
**ImportService** (`ScreenShotRecipe.Application.Services`)
✅ Logs import job initiation with job ID
✅ Logs each image processing with filename and results
✅ Logs OCR text extraction with text length and confidence
✅ Logs LLM parsing results with recipe details
✅ Logs database persistence with recipe ID, title, ingredient/step counts
✅ Logs import job completion
✅ Logs all errors with context

### Infrastructure Layer

**RecipeRepository** (`ScreenShotRecipe.Infrastructure.Repositories`)
✅ Logs recipe data operations (Add, Get, GetAll)
✅ Logs recipe details (ID, title)
✅ Logs database success and failure scenarios
✅ Logs missing recipes with ID

**FileSystemStorage** (`ScreenShotRecipe.Infrastructure.Storage`)
✅ Logs storage initialization with root path
✅ Logs image saves with original filename, saved path, file size
✅ Logs image reads with path and file size
✅ Logs all storage errors

**FakeOcrClient** (`ScreenShotRecipe.Infrastructure.Ocr`)
✅ Logs OcrClient initialization (fake mode indicator)
✅ Logs OCR processing initiation with image size
✅ Logs mock data generation with confidence level

**FakeRecipeOcrService** (`ScreenShotRecipe.Infrastructure.Ocr`)
✅ Logs service initialization
✅ Logs batch processing with image count
✅ Logs image validation with batch size checks
✅ Logs per-image processing with filename and text length
✅ Logs completion with result count
✅ Logs all errors with image counts

**FakeLLMParser** (`ScreenShotRecipe.Infrastructure.Parsing`)
✅ Logs parser initialization with confidence override info
✅ Logs parsing initiation with text length
✅ Logs recipe entity creation with ID and title
✅ Logs ingredient extraction with count
✅ Logs step extraction with count
✅ Logs tag extraction with tag list
✅ Logs confidence calculation results
✅ Logs successful parsing completion
✅ Logs all errors with context

## Log Examples

### Import Operation
```
2026-02-22 10:15:30.123 +00:00 [INF] Starting import job 5e3c2f1a-7d4a-4b23-8f11-9c6d2a1b3c4d with 2 image(s)
2026-02-22 10:15:30.245 +00:00 [INF] OCR processing initiated on image with size: 245632 bytes
2026-02-22 10:15:30.356 +00:00 [INF] Mock OCR data generated (fake service) with text length: 1245, confidence: 95%
2026-02-22 10:15:30.467 +00:00 [INF] Image saved successfully. Original filename: recipe.jpg, Saved as: 3a4b5c6d_recipe.jpg, Size: 245632 bytes
2026-02-22 10:15:30.578 +00:00 [INF] ParseAsync initiated with text length: 2490 characters
2026-02-22 10:15:30.689 +00:00 [INF] Recipe entity created. ID: 3fc8d4e1-2a9b-4c5d-8e7f-1a2b3c4d5e6f, Title: Chocolate Chip Cookies
2026-02-22 10:15:30.800 +00:00 [INF] Ingredients extracted: 9 items
2026-02-22 10:15:30.911 +00:00 [INF] Steps extracted: 10 items
2026-02-22 10:15:31.022 +00:00 [INF] Parse confidence calculated: 93%
2026-02-22 10:15:31.133 +00:00 [INF] Recipe persisted to repository. Recipe ID: 3fc8d4e1-2a9b-4c5d-8e7f-1a2b3c4d5e6f, Title: Chocolate Chip Cookies, Ingredients: 9, Steps: 10
2026-02-22 10:15:31.244 +00:00 [INF] Import job 5e3c2f1a-7d4a-4b23-8f11-9c6d2a1b3c4d completed successfully
```

### Error Scenario
```
2026-02-22 10:20:15.567 +00:00 [ERR] Error processing image: invalid_file.txt
System.IO.FileNotFoundException: Image file not found: ./data/images/5a6b7c8d_invalid_file.txt
   at ScreenShotRecipe.Infrastructure.Storage.FileSystemStorage.ReadImageAsync(String path)
```

## How to Use Logging in New Code

### In Services
```csharp
private readonly ILogger<MyService> _logger;

public MyService(ILogger<MyService> logger)
{
    _logger = logger;
}

public async Task DoSomethingAsync()
{
    _logger.LogInformation("Starting operation");
    try
    {
        // Do work...
        _logger.LogInformation("Operation completed successfully with result: {Result}", result);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Operation failed");
        throw;
    }
}
```

### Log Levels
- `LogInformation()` - Normal application flow, business events
- `LogWarning()` - Unusual conditions that don't prevent operation
- `LogError(ex, message)` - Errors with exception details

## Next Steps

1. **Build the project** to restore NuGet packages:
   ```
   dotnet build
   ```

2. **Run the application** and verify logs appear in the `./Logs/` directory

3. **Monitor logs** during testing:
   - Check log files for completeness
   - Verify structured fields for troubleshooting
   - Ensure no sensitive data is being logged

4. **Add logging to new features** by:
   - Injecting `ILogger<T>` into constructors
   - Logging at appropriate lifecycle points
   - Using structured logging with named parameters

## Configuration (appsettings.json)

The Serilog configuration in Program.cs can be extended with additional configuration from appsettings.json if needed. Current hardcoded settings:
- Minimum Level: Information
- Output Template: `{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}`
- Rolling Interval: Daily
- File Retention: 30 days

To make this configurable, migrate Serilog setup to appsettings.json and use `ReadFrom.Configuration(builder.Configuration)`.

## Troubleshooting

### Logs not appearing
1. Verify the `./Logs/` directory exists and is writable
2. Check application has appropriate file system permissions
3. Ensure Serilog packages are installed: `dotnet restore`

### Too many log entries
- Adjust minimum level to `Warning` or `Error` only
- Add filters to reduce noise from specific loggers

### Performance impact
- File sink is configured for normal performance
- Consider async file writes for very high-volume logging
- Monitor disk I/O if logging volume becomes excessive
