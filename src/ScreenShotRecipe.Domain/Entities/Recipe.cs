using System;
using System.Collections.Generic;

namespace ScreenShotRecipe.Domain.Entities
{
    public class Recipe
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Title { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public List<Ingredient> Ingredients { get; set; } = new();
        public List<Step> Steps { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        
        /// <summary>
        /// Foreign key to the ImportJob that created this recipe.
        /// </summary>
        public Guid ImportJobId { get; set; }
        
        /// <summary>
        /// Navigation property to the ImportJob.
        /// </summary>
        public ImportJob? ImportJob { get; set; }
        
        /// <summary>
        /// Overall confidence score from OCR/LLM extraction (0.0-1.0).
        /// </summary>
        public decimal OverallConfidence { get; set; }
        
        /// <summary>
        /// Notes about confidence or parsing issues (e.g., "Low on ingredients list").
        /// </summary>
        public string? ConfidenceNotes { get; set; }
        
        /// <summary>
        /// Version number for optimistic concurrency, incremented on each edit.
        /// </summary>
        public int Version { get; set; } = 1;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
