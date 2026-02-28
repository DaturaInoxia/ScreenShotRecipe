using System;
using System.Collections.Generic;

namespace ScreenShotRecipe.Application.Contracts.Dtos
{
    public class RecipeDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public List<IngredientDto> Ingredients { get; set; } = new();
        public List<StepDto> Steps { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        
        /// <summary>
        /// Overall confidence of the OCR/extraction (0.0 to 1.0).
        /// </summary>
        public decimal? OverallConfidence { get; set; }
        
        /// <summary>
        /// Notes about confidence issues or areas needing review.
        /// </summary>
        public string? ConfidenceNotes { get; set; }
        
        /// <summary>
        /// IDs of the original uploaded images associated with this recipe.
        /// </summary>
        public List<Guid>? ImageIds { get; set; }
        
        /// <summary>
        /// Version number for optimistic concurrency.
        /// </summary>
        public int Version { get; set; }
    }
}
