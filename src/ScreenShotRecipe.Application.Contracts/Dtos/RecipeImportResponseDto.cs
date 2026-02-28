using System;
using System.Collections.Generic;

namespace ScreenShotRecipe.Application.Contracts.Dtos
{
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
        /// Values: "Queued", "OcrInProgress", "ParsingInProgress", "Succeeded", "Failed", "PartiallyFailed"
        /// </summary>
        public required string Status { get; set; }
        
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
        
        /// <summary>
        /// IDs of stored images for retrieval via /api/images/{id}.
        /// </summary>
        public IReadOnlyList<Guid>? ImageIds { get; set; }
    }
}
