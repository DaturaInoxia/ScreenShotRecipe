# Data Model: Recipe Import with GPT-4o OCR

**Phase 1 Output**: 2026-02-22  
**Related Research**: [research.md](research.md)

---

## Entity Relationship Diagram

```
ImportJob (1) ──────┬──── (Many) ImageAsset
              │     └──── (Many) OcrResult
              │     └──── (1) Recipe
              │
             User ──────── (Many) ImportJob

Recipe (1) ─────┬──── (Many) Ingredient
            │   └──── (Many) RecipeStep
            │   └──── (1) ImportJob (reference)
            │
         (Many) ImageAsset
```

---

## Core Domain Entities

### 1. Recipe

**Purpose**: Structured representation of a recipe parsed from OCR output.

```csharp
public class Recipe
{
    public Guid Id { get; set; }
    
    public string Title { get; set; } // Required, non-empty
    public string Description { get; set; } // Optional notes/remarks
    public List<Ingredient> Ingredients { get; set; } = new();
    public List<RecipeStep> Steps { get; set; } = new();
    public List<string> Tags { get; set; } = new(); // e.g., ["dessert", "vegan"]
    
    // Audit Fields
    public DateTime CreatedAt { get; set; }
    public DateTime ModifiedAt { get; set; }
    
    // Traceability
    public Guid ImportJobId { get; set; } // FK to ImportJob
    public ImportJob ImportJob { get; set; } // Navigation
    
    // Confidence Metadata (from OCR/LLM)
    public decimal OverallConfidence { get; set; } // 0.0-1.0
    public string ConfidenceNotes { get; set; } // e.g., "Low on ingredients list"
    
    // Versioning
    public int Version { get; set; } = 1; // Incremented on edits
    
    // Validation
    public bool Validate(out List<string> errors)
    {
        errors = new();
        if (string.IsNullOrWhiteSpace(Title))
            errors.Add("Title is required.");
        if (Ingredients.Count == 0)
            errors.Add("At least one ingredient is required.");
        if (Steps.Count == 0)
            errors.Add("At least one step is required.");
        return errors.Count == 0;
    }
}
```

### 2. Ingredient

**Purpose**: Individual recipe ingredient with quantity and unit.

```csharp
public class Ingredient
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    
    public string Name { get; set; } // Required: "flour", "eggs", etc.
    public decimal? Quantity { get; set; } // Optional: 2.5
    public string Unit { get; set; } // Optional: "cups", "tablespoons", "ml"
    public string RawText { get; set; } // Original OCR text for reference
    
    // Order
    public int Order { get; set; } // Position in ingredients list
    
    // Confidence
    public decimal Confidence { get; set; } // 0.0-1.0 (from OCR/LLM)
    
    // Navigation
    public Recipe Recipe { get; set; }
    
    // Calculated Property
    public string FormattedQuantity => 
        Quantity.HasValue 
            ? $"{Quantity} {Unit}".Trim() 
            : Unit ?? "";
}
```

### 3. RecipeStep

**Purpose**: Sequential recipe instruction.

```csharp
public class RecipeStep
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    
    public int Order { get; set; } // 1, 2, 3, ...
    public string Text { get; set; } // Required: "Preheat oven to 350°F..."
    public int? EstimatedMinutes { get; set; } // Optional timing
    
    // Confidence
    public decimal Confidence { get; set; } // 0.0-1.0
    
    // Navigation
    public Recipe Recipe { get; set; }
}
```

### 4. ImageAsset

**Purpose**: Reference to original uploaded image and OCR result.

```csharp
public class ImageAsset
{
    public Guid Id { get; set; }
    public Guid ImportJobId { get; set; }
    
    public string FileName { get; set; } // Original filename from upload
    public string FilePath { get; set; } // Storage path: /storage/images/{guid}/{filename}
    public string MediaType { get; set; } // "image/jpeg", "image/png", etc.
    public long FileSizeBytes { get; set; }
    
    // Upload Order
    public int UploadOrder { get; set; } // Preserved order for OCR reading
    
    // Storage Metadata
    public DateTime StoredAt { get; set; }
    public string StorageProviderKey { get; set; } // "local_fs" or "s3"
    
    // Navigation
    public Guid? OcrResultId { get; set; }
    public OcrResult OcrResult { get; set; } // Navigation
    public ImportJob ImportJob { get; set; }
}
```

### 5. OcrResult

**Purpose**: Raw OCR output from GPT-4o for a single image.

```csharp
public class OcrResult
{
    public Guid Id { get; set; }
    public Guid ImageAssetId { get; set; }
    
    // Extracted Content
    public string ExtractedText { get; set; } // Raw OCR output
    public string DetectedLanguage { get; set; } // "en", "es", "fr", etc.
    
    // Confidence Metadata
    public decimal Confidence { get; set; } // 0.0-1.0 average confidence
    public string ConfidenceDetails { get; set; } // JSON: per-line confidence if available
    
    // Processing
    public DateTime ProcessedAt { get; set; }
    public string OcrEngine { get; set; } // "gpt-4o-2024-11-20" (for versioning)
    
    // Navigation
    public ImageAsset ImageAsset { get; set; }
}
```

### 6. ImportJob

**Purpose**: Orchestration record for a multi-image import request.

```csharp
public class ImportJob
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; } // Optional: for multi-user future
    
    // Status Tracking
    public ImportJobStatus Status { get; set; }
    public List<ImageAsset> ImageAssets { get; set; } = new();
    public Guid? RecipeId { get; set; } // FK to parsed Recipe (if successful)
    public Recipe Recipe { get; set; } // Navigation
    
    // Timeline
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    
    // Diagnostics
    public string ErrorMessage { get; set; } // If failed
    public string Diagnostics { get; set; } // JSON: detailed error info
    
    // Tracking
    public int Version { get; set; } // For idempotency
    public string IdempotencyKey { get; set; } // To prevent duplicate processing
    
    // Metadata
    public string Source { get; set; } // "web_ui", "api", etc.
}

public enum ImportJobStatus
{
    Queued,       // Awaiting processing
    OcrInProgress, // Extracting text from images
    ParsingInProgress, // Converting OCR to structured recipe
    Succeeded,    // Recipe created and stored
    Failed,       // OCR or parsing failed
    PartiallyFailed // Some images succeeded, others failed
}
```

---

## Data Validation Rules

### Recipe Entity
- **Title**: Required, non-empty, max 500 characters
- **Ingredients**: Minimum 1 required
- **Steps**: Minimum 1 required
- **Tags**: Max 20, each max 50 characters
- **Confidence**: 0.0 ≤ confidence ≤ 1.0

### Ingredient Entity
- **Name**: Required, non-empty, max 200 characters
- **Quantity**: If present, must be > 0 and ≤ 10000
- **Unit**: If present, max 50 characters (e.g., "cups", "tablespoons")
- **Order**: Non-negative, unique per recipe
- **Confidence**: 0.0 ≤ confidence ≤ 1.0

### RecipeStep Entity
- **Order**: Positive, unique per recipe
- **Text**: Required, non-empty, max 2000 characters
- **EstimatedMinutes**: If present, > 0 and ≤ 1440 (24 hours)
- **Confidence**: 0.0 ≤ confidence ≤ 1.0

### ImageAsset Entity
- **FileName**: Required, max 255 characters
- **FilePath**: Valid path, max 1024 characters
- **MediaType**: Must be valid MIME type (image/jpeg, image/png, etc.)
- **FileSizeBytes**: > 0 and ≤ 20,971,520 (20 MB)
- **UploadOrder**: Non-negative, unique per ImportJob

### OcrResult Entity
- **ExtractedText**: Required if processing succeeded
- **DetectedLanguage**: Valid ISO 639-1 code (e.g., "en", "es")
- **Confidence**: 0.0 ≤ confidence ≤ 1.0

### ImportJob Entity
- **Status**: Valid enum value
- **ImageAssets**: Minimum 1 required
- **Diagnostics**: JSON format (if populated)

---

## State Transitions

### ImportJob Status Flow

```
[Queued] 
   ↓
[OcrInProgress] ──→ (GPT-4o processes images)
   ↓
[ParsingInProgress] ──→ (LLM constructs Recipe from concatenated OCR text)
   ↓ (success)
[Succeeded] ──→ Recipe created, images linked
   ↓ (failure)
[Failed] ──→ Error diagnostics recorded, job logged

[PartiallyFailed] ──→ Some images succeeded, some failed (future enhancement)
```

### Recipe Modification Tracking

```
Recipe.Version increments on:
  - Title/description edit
  - Ingredient addition/modification/deletion
  - Step addition/modification/deletion
  - Tag modification

Audit trail recorded in separate AuditLog table (future enhancement)
```

---

## Database Schema Considerations

### Indexes
- `Recipe.ImportJobId` (for traceability)
- `Recipe.CreatedAt` (for sorting in UI)
- `ImportJob.Status` (for job dashboard)
- `ImageAsset.UploadOrder` (for reading order)
- `Ingredient.RecipeId, Order` (for sorted display)
- `RecipeStep.RecipeId, Order` (for sorted display)

### Constraints
- `Recipe.Title` NOT NULL
- `Ingredient.RecipeId, Order` UNIQUE (one ingredient per order)
- `RecipeStep.RecipeId, Order` UNIQUE (one step per order)
- `ImageAsset.UploadOrder` NOT NULL

### Cascading Deletes
- Delete Recipe → cascades to Ingredients, Steps
- Delete ImportJob → cascades to ImageAssets, OcrResults (but NOT Recipe)
- Delete ImageAsset → cascades to OcrResult

---

## Integration Points

### IRecipeOcrService (Domain/Infrastructure boundary)

The OCR service accepts `ImageData` (bytes) and returns `OcrResult` records.

```csharp
public interface IRecipeOcrService
{
    Task<IReadOnlyList<OcrResult>> ExtractTextAsync(
        IReadOnlyList<ImageData> images,
        CancellationToken cancellationToken = default);
}
```

### ILLMParser (Domain/Infrastructure boundary)

The LLM service accepts concatenated OCR text and returns a Recipe DTO.

```csharp
public interface ILLMParser
{
    Task<RecipeDto> ParseRecipeAsync(
        string concatenatedOcrText,
        CancellationToken cancellationToken = default);
}
```

### IStorage (Domain/Infrastructure boundary)

The storage service persists image bytes and returns file paths.

```csharp
public interface IStorage
{
    Task<string> SaveAsync(
        byte[] imageBytes,
        string fileName,
        CancellationToken cancellationToken = default);
    
    Task<byte[]> RetrieveAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
```

---

## Summary

This data model supports:
✅ Multi-image recipe imports with OCR traceability  
✅ Structured recipe entities (title, ingredients, steps, tags)  
✅ Confidence metadata for OCR/LLM results  
✅ Import job orchestration and status tracking  
✅ Audit trail and versioning (foundational)  
✅ Pluggable OCR, parsing, and storage providers (DI-based)  
✅ Query optimization via indexes and constraints  
✅ Cascading deletes for data integrity  

Ready for implementation in Phase 1.
