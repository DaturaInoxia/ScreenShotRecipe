using System.Threading.Tasks;
using ScreenShotRecipe.Domain.Interfaces;
using ScreenShotRecipe.Domain.Entities;

namespace ScreenShotRecipe.Infrastructure.Parsing
{
    // NOTE: Minimal stub for LLM parsing. Replace with Azure OpenAI or other provider integration.
    public class AzureOpenAIParser : ILLMParser
    {
        public Task<ParseResult> ParseAsync(string combinedText)
        {
            // Very naive parser placeholder: create a Recipe with the whole text as a single step
            var r = new Recipe
            {
                Title = "Parsed Recipe",
                Notes = null,
            };
            r.Steps.Add(new Step { Ordinal = 1, Text = combinedText ?? string.Empty });
            r.Ingredients.Add(new Ingredient { RawText = "(ingredients parsed later)" });

            return Task.FromResult(new ParseResult(r, 1.0));
        }
    }
}
