using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ScreenShotRecipe.Application.Contracts.Dtos
{
    /// <summary>
    /// DTO for editing/updating a recipe.
    /// </summary>
    public class RecipeEditDto
    {
        /// <summary>
        /// Recipe ID to update.
        /// </summary>
        public Guid Id { get; set; }
        
        /// <summary>
        /// Recipe title (required, max 500 chars).
        /// </summary>
        [Required(ErrorMessage = "Title is required")]
        [StringLength(500, ErrorMessage = "Title must be 500 characters or less")]
        public required string Title { get; set; }
        
        /// <summary>
        /// Optional notes/description.
        /// </summary>
        [StringLength(5000, ErrorMessage = "Notes must be 5000 characters or less")]
        public string? Notes { get; set; }
        
        /// <summary>
        /// List of ingredients.
        /// </summary>
        public List<IngredientEditDto> Ingredients { get; set; } = new();
        
        /// <summary>
        /// List of steps (will be numbered by ordinal).
        /// </summary>
        public List<StepEditDto> Steps { get; set; } = new();
        
        /// <summary>
        /// Tags for categorization.
        /// </summary>
        public List<string> Tags { get; set; } = new();
        
        /// <summary>
        /// Current version for optimistic concurrency.
        /// Must match the server version for update to succeed.
        /// </summary>
        public int Version { get; set; }
    }

    /// <summary>
    /// DTO for editing an ingredient.
    /// </summary>
    public class IngredientEditDto
    {
        /// <summary>
        /// Original raw text from OCR.
        /// </summary>
        [StringLength(1000)]
        public string RawText { get; set; } = string.Empty;
        
        /// <summary>
        /// Parsed ingredient name.
        /// </summary>
        [StringLength(500)]
        public string? Name { get; set; }
        
        /// <summary>
        /// Parsed quantity (e.g., "2", "1/2").
        /// </summary>
        [StringLength(50)]
        public string? Quantity { get; set; }
        
        /// <summary>
        /// Parsed unit (e.g., "cups", "tbsp").
        /// </summary>
        [StringLength(50)]
        public string? Unit { get; set; }
    }

    /// <summary>
    /// DTO for editing a step.
    /// </summary>
    public class StepEditDto
    {
        /// <summary>
        /// Step number (1-based).
        /// </summary>
        [Range(1, 100, ErrorMessage = "Step ordinal must be between 1 and 100")]
        public int Ordinal { get; set; }
        
        /// <summary>
        /// Step instruction text.
        /// </summary>
        [Required(ErrorMessage = "Step text is required")]
        [StringLength(5000, ErrorMessage = "Step text must be 5000 characters or less")]
        public string Text { get; set; } = string.Empty;
    }
}
