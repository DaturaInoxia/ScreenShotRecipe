## Application.Contracts DTOs

**Location**: `ScreenShotRecipe.Application.Contracts/Dtos/`  
**Layer**: Application.Contracts (shared between Application and Web layers)  
**Purpose**: Exchange objects for recipe import workflow

---

### OcrResultDto

**File**: `OcrResultDto.cs`

```csharp
namespace ScreenShotRecipe.Application.Contracts.Dtos;

/// <summary>
/// DTO representing OCR extraction result for a single image.
/// Exposed for API responses and logging.
/// </summary>
public class OcrResultDto
{
    /// <summary>
    /// Raw text extracted from the image.
    /// </summary>
    public required string ExtractedText { get; set; }
    
    /// <summary>
    /// Detected language (ISO 639-1 code: "en", "es", "fr", etc.).
    /// </summary>
    public required string DetectedLanguage { get; set; }
    
    /// <summary>
    /// Confidence score (0.0-1.0).
    /// >= 0.9: High confidence
    /// 0.7-0.9: Medium confidence
    /// < 0.7: Low confidence (flag for review)
    /// </summary>
    public decimal Confidence { get; set; }
    
    /// <summary>
    /// Original filename of the image (for reference).
    /// </summary>
    public required string ImageFileName { get; set; }
    
    /// <summary>
    /// Page order in the original batch (0-based index).
    /// </summary>
    public int PageOrder { get; set; }
}
```

### RecipeImportRequestDto

**File**: `RecipeImportRequestDto.cs`

```csharp
namespace ScreenShotRecipe.Application.Contracts.Dtos;

/// <summary>
/// DTO for initiating a recipe import request (multi-image upload).
/// </summary>
public class RecipeImportRequestDto
{
    /// <summary>
    /// Collection of image files (1-6 images recommended).
    /// Each file represents a page/section of the recipe.
    /// </summary>
    public required IReadOnlyList<RecipeImageFileDto> Images { get; set; }
    
    /// <summary>
    /// Optional user context or metadata.
    /// </summary>
    public string? Metadata { get; set; }
}

public class RecipeImageFileDto
{
    /// <summary>
    /// Image filename as uploaded.
    /// </summary>
    public required string FileName { get; set; }
    
    /// <summary>
    /// MIME type of the image ("image/jpeg", "image/png", etc.).
    /// </summary>
    public required string MediaType { get; set; }
    
    /// <summary>
    /// Image binary data (base64 encoded when transmitted via JSON).
    /// </summary>
    public required byte[] Content { get; set; }
}
```

### RecipeImportResponseDto

**File**: `RecipeImportResponseDto.cs`

```csharp
namespace ScreenShotRecipe.Application.Contracts.Dtos;

/// <summary>
/// DTO for response to a recipe import request.
/// </summary>
public class RecipeImportResponseDto
{
    /// <summary>
    /// Unique ID of the import job for tracking.
    /// </summary>
    public required Guid ImportJobId { get; set; }
    
    /// <summary>
    /// Current status of the import job.
    /// </summary>
    public required string Status { get; set; } // "Queued", "OcrInProgress", "ParsingInProgress", "Succeeded", "Failed"
    
    /// <summary>
    /// Parsed recipe (populated if Status == "Succeeded").
    /// </summary>
    public RecipeDto? Recipe { get; set; }
    
    /// <summary>
    /// OCR results for each image (for transparency/debugging).
    /// </summary>
    public IReadOnlyList<OcrResultDto>? OcrResults { get; set; }
    
    /// <summary>
    /// Error message if import failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Detailed diagnostics (JSON) if import failed or has warnings.
    /// </summary>
    public string? Diagnostics { get; set; }
}
```

### RecipeDto

**File**: `RecipeDto.cs`

```csharp
namespace ScreenShotRecipe.Application.Contracts.Dtos;

/// <summary>
/// DTO representing a parsed recipe.
/// </summary>
public class RecipeDto
{
    public Guid Id { get; set; }
    
    public required string Title { get; set; }
    
    public string? Description { get; set; }
    
    public required IReadOnlyList<IngredientDto> Ingredients { get; set; }
    
    public required IReadOnlyList<StepDto> Steps { get; set; }
    
    public IReadOnlyList<string>? Tags { get; set; }
    
    /// <summary>
    /// Overall confidence score for the recipe parsing (0.0-1.0).
    /// </summary>
    public decimal Confidence { get; set; }
    
    /// <summary>
    /// Notes about confidence or parsing issues.
    /// </summary>
    public string? ConfidenceNotes { get; set; }
    
    /// <summary>
    /// Timestamp when recipe was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// ID of the import job that produced this recipe.
    /// </summary>
    public Guid ImportJobId { get; set; }
}
```

### IngredientDto

**File**: `IngredientDto.cs`

```csharp
namespace ScreenShotRecipe.Application.Contracts.Dtos;

/// <summary>
/// DTO representing a single ingredient in a recipe.
/// </summary>
public class IngredientDto
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Ingredient name (e.g., "flour", "eggs").
    /// </summary>
    public required string Name { get; set; }
    
    /// <summary>
    /// Quantity (e.g., 2.5).
    /// </summary>
    public decimal? Quantity { get; set; }
    
    /// <summary>
    /// Unit of measure (e.g., "cups", "tablespoons").
    /// </summary>
    public string? Unit { get; set; }
    
    /// <summary>
    /// Original OCR text (for reference).
    /// </summary>
    public string? RawText { get; set; }
    
    /// <summary>
    /// Order in ingredient list (1-based).
    /// </summary>
    public int Order { get; set; }
    
    /// <summary>
    /// Confidence score for this ingredient (0.0-1.0).
    /// </summary>
    public decimal Confidence { get; set; }
}
```

### StepDto

**File**: `StepDto.cs`

```csharp
namespace ScreenShotRecipe.Application.Contracts.Dtos;

/// <summary>
/// DTO representing a single step in a recipe.
/// </summary>
public class StepDto
{
    public Guid Id { get; set; }
    
    /// <summary>
    /// Step order (1-based).
    /// </summary>
    public int Order { get; set; }
    
    /// <summary>
    /// Instruction text.
    /// </summary>
    public required string Text { get; set; }
    
    /// <summary>
    /// Estimated time in minutes (optional).
    /// </summary>
    public int? EstimatedMinutes { get; set; }
    
    /// <summary>
    /// Confidence score for this step (0.0-1.0).
    /// </summary>
    public decimal Confidence { get; set; }
}
```

---

## DTO Validation Rules

| DTO | Field | Rule |
|-----|-------|------|
| OcrResultDto | ExtractedText | Required, non-empty |
| OcrResultDto | DetectedLanguage | ISO 639-1 code (e.g., "en") |
| OcrResultDto | Confidence | 0.0 ≤ x ≤ 1.0 |
| OcrResultDto | PageOrder | >= 0 |
| RecipeImportRequestDto | Images | 1-6 items |
| RecipeImportRequestDto | Images[].Content | > 0 bytes, <= 20 MB |
| RecipeImportRequestDto | Images[].MediaType | Valid MIME type |
| RecipeDto | Title | Required, 1-500 chars |
| RecipeDto | Ingredients | >= 1 item |
| RecipeDto | Steps | >= 1 item |
| IngredientDto | Name | Required, 1-200 chars |
| IngredientDto | Quantity | If present: > 0 and <= 10,000 |
| IngredientDto | Unit | If present, max 50 chars |
| StepDto | Text | Required, 1-2000 chars |
| StepDto | EstimatedMinutes | If present: > 0 and <= 1440 |

---

## Mapping (Domain ↔ DTO)

### OcrResult → OcrResultDto
- Direct property mapping (1:1)
- Used in API responses and logs

### Recipe → RecipeDto
- Map Recipe entity to DTO with related entities
```csharp
recipeDto = new RecipeDto
{
    Id = recipe.Id,
    Title = recipe.Title,
    Description = recipe.Description,
    Ingredients = recipe.Ingredients.OrderBy(i => i.Order)
        .Select(i => MapToDto(i)).ToList(),
    Steps = recipe.Steps.OrderBy(s => s.Order)
        .Select(s => MapToDto(s)).ToList(),
    Tags = recipe.Tags,
    Confidence = recipe.OverallConfidence,
    ConfidenceNotes = recipe.ConfidenceNotes,
    CreatedAt = recipe.CreatedAt,
    ImportJobId = recipe.ImportJobId
};
```

---

## Summary

✅ Clear separation between domain entities and API/application contracts  
✅ DTOs include confidence metadata for UI review  
✅ Validation rules prevent invalid data  
✅ Mapping layer handles domain ↔ DTO transformation  
✅ Supports both synchronous and asynchronous import flows  
