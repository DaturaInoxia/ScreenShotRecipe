using System.Collections.Generic;

namespace ScreenShotRecipe.Application.Contracts.Dtos
{
    /// <summary>
    /// DTO for initiating a recipe import request (multi-image upload).
    /// </summary>
    public class RecipeImportRequestDto
    {
        /// <summary>
        /// Collection of image files (1-12 images recommended).
        /// Each file represents a page/section of the recipe.
        /// </summary>
        public required List<RecipeImageFileDto> Images { get; set; }
        
        /// <summary>
        /// Optional import metadata.
        /// </summary>
        public RecipeImportMetadataDto? Metadata { get; set; }
        
        /// <summary>
        /// Optional idempotency key to prevent duplicate processing.
        /// </summary>
        public string? IdempotencyKey { get; set; }
    }

    /// <summary>
    /// Optional metadata for recipe import.
    /// </summary>
    public class RecipeImportMetadataDto
    {
        /// <summary>
        /// User ID if authenticated.
        /// </summary>
        public string? UserId { get; set; }
        
        /// <summary>
        /// Source of the import (web-upload, mobile-app, api, etc.).
        /// </summary>
        public string? Source { get; set; }
        
        /// <summary>
        /// Additional tags to apply to the imported recipe.
        /// </summary>
        public List<string>? Tags { get; set; }
    }

    /// <summary>
    /// Represents a single image file in an import request.
    /// </summary>
    public class RecipeImageFileDto
    {
        /// <summary>
        /// Image filename as uploaded.
        /// </summary>
        public required string FileName { get; set; }
        
        /// <summary>
        /// MIME type of the image ("image/jpeg", "image/png", etc.).
        /// Optional - will be inferred from filename if not provided.
        /// </summary>
        public string? MediaType { get; set; }
        
        /// <summary>
        /// Image binary data.
        /// </summary>
        public required byte[] Content { get; set; }
        
        /// <summary>
        /// Order of this image in the sequence (0-based).
        /// Used when recipe spans multiple pages.
        /// </summary>
        public int PageOrder { get; set; }
    }
}
