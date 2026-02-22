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
        public Guid ImportJobId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
