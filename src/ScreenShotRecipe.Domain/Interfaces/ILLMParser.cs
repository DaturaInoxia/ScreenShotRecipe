using System.Threading.Tasks;
using ScreenShotRecipe.Domain.Entities;

namespace ScreenShotRecipe.Domain.Interfaces
{
    public record ParseResult(Recipe Recipe, double Confidence);

    public interface ILLMParser
    {
        /// <summary>
        /// Parse combined OCR text into a structured Recipe.
        /// </summary>
        Task<ParseResult> ParseAsync(string combinedText);
    }
}
