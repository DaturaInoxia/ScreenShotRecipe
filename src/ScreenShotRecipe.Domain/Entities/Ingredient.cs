namespace ScreenShotRecipe.Domain.Entities
{
    public class Ingredient
    {
        public int Id { get; set; }
        public string RawText { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Quantity { get; set; }
        public string? Unit { get; set; }
    }
}
