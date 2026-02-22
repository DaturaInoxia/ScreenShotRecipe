namespace ScreenShotRecipe.Domain.Entities
{
    public class Step
    {
        public int Id { get; set; }
        public int Ordinal { get; set; }
        public string Text { get; set; } = string.Empty;
    }
}
