using System;
using System.Collections.Generic;

namespace ScreenShotRecipe.Domain.Entities
{
    /// <summary>
    /// Import job status enumeration.
    /// </summary>
    public enum ImportJobStatus
    {
        /// <summary>Awaiting processing.</summary>
        Queued,
        
        /// <summary>Extracting text from images via OCR.</summary>
        OcrInProgress,
        
        /// <summary>Converting OCR text to structured recipe.</summary>
        ParsingInProgress,
        
        /// <summary>Recipe created and stored successfully.</summary>
        Succeeded,
        
        /// <summary>OCR or parsing failed.</summary>
        Failed,
        
        /// <summary>Some images succeeded, others failed (future enhancement).</summary>
        PartiallyFailed
    }
    
    /// <summary>
    /// Orchestration record for a multi-image recipe import request.
    /// </summary>
    public class ImportJob
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        /// <summary>
        /// Optional user ID for multi-user scenarios (future).
        /// </summary>
        public Guid? UserId { get; set; }
        
        /// <summary>
        /// Current import status.
        /// </summary>
        public ImportJobStatus Status { get; set; } = ImportJobStatus.Queued;
        
        /// <summary>
        /// Images being processed in this import.
        /// </summary>
        public List<ImageAsset> ImageAssets { get; set; } = new();
        
        /// <summary>
        /// Foreign key to the resulting Recipe (if successful).
        /// </summary>
        public Guid? RecipeId { get; set; }
        
        /// <summary>
        /// Navigation to the parsed Recipe.
        /// </summary>
        public Recipe? Recipe { get; set; }
        
        /// <summary>
        /// When the import job was created.
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// When processing started.
        /// </summary>
        public DateTime? StartedAt { get; set; }
        
        /// <summary>
        /// When processing completed (success or failure).
        /// </summary>
        public DateTime? CompletedAt { get; set; }
        
        /// <summary>
        /// Error message if failed.
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// JSON containing detailed error diagnostics.
        /// </summary>
        public string? Diagnostics { get; set; }
        
        /// <summary>
        /// Version for idempotency checks.
        /// </summary>
        public int Version { get; set; } = 1;
        
        /// <summary>
        /// Client-provided key to prevent duplicate processing.
        /// </summary>
        public string? IdempotencyKey { get; set; }
        
        /// <summary>
        /// Source of the import request: "web_ui", "api", etc.
        /// </summary>
        public string Source { get; set; } = "web_ui";
    }
}
