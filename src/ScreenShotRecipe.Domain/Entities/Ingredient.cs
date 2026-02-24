using System;

namespace ScreenShotRecipe.Domain.Entities
{
    public class Ingredient
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string RawText { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Quantity { get; set; }
        public string? Unit { get; set; }
    }
}
