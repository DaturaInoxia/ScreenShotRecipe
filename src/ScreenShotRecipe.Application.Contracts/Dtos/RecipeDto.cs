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
    }
}
