using System;

namespace ScreenShotRecipe.Domain.Entities
{
    public class Step
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid RecipeId { get; set; }
        public int Ordinal { get; set; }
        public string Text { get; set; } = string.Empty;
    }
}
