namespace ScreenShotRecipe.Application.Contracts.Dtos
{
    public class IngredientDto
    {
        public string RawText { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Quantity { get; set; }
        public string? Unit { get; set; }
    }
}
