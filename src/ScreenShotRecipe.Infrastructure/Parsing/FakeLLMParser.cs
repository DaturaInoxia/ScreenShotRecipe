using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ScreenShotRecipe.Domain.Interfaces;
using ScreenShotRecipe.Domain.Entities;

namespace ScreenShotRecipe.Infrastructure.Parsing
{
    public class FakeLLMParser : ILLMParser
    {
        public Task<ParseResult> ParseAsync(string combinedText)
        {
            var recipe = new Recipe
            {
                Title = ExtractTitle(combinedText),
                Notes = ExtractNotes(combinedText)
            };

            // Parse ingredients section
            var ingredients = ExtractIngredients(combinedText);
            foreach (var ing in ingredients)
            {
                recipe.Ingredients.Add(ing);
            }

            // Parse steps section
            var steps = ExtractSteps(combinedText);
            foreach (var step in steps)
            {
                recipe.Steps.Add(step);
            }

            // Add some tags based on content
            recipe.Tags.AddRange(ExtractTags(combinedText));

            return Task.FromResult(new ParseResult(recipe, 0.88));
        }

        private string ExtractTitle(string text)
        {
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            return lines.FirstOrDefault()?.Trim() ?? "Untitled Recipe";
        }

        private string ExtractNotes(string text)
        {
            // Extract any notes between title and INGREDIENTS
            var ingredientsIndex = text.IndexOf("INGREDIENTS", StringComparison.OrdinalIgnoreCase);
            if (ingredientsIndex > 0)
            {
                var beforeIngredients = text.Substring(0, ingredientsIndex).Trim();
                var lines = beforeIngredients.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length > 1)
                {
                    // Skip title, take rest as notes
                    return string.Join(" ", lines.Skip(1)).Trim();
                }
            }
            return null;
        }

        private List<Ingredient> ExtractIngredients(string text)
        {
            var ingredients = new List<Ingredient>();
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var inIngredientsSection = false;
            var ordinal = 1;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed.Equals("INGREDIENTS", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("INGREDIENTS:", StringComparison.OrdinalIgnoreCase))
                {
                    inIngredientsSection = true;
                    continue;
                }

                if (trimmed.Equals("INSTRUCTIONS", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("INSTRUCTIONS:", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("STEP", StringComparison.OrdinalIgnoreCase))
                {
                    inIngredientsSection = false;
                }

                if (inIngredientsSection && !string.IsNullOrWhiteSpace(trimmed) && !trimmed.EndsWith(":"))
                {
                    var ing = ParseIngredient(trimmed);
                    ingredients.Add(ing);
                }
            }

            return ingredients;
        }

        private Ingredient ParseIngredient(string line)
        {
            // Very simple parsing: "2 1/4 cups all-purpose flour" -> Quantity: "2 1/4", Unit: "cups", Name: "all-purpose flour"
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
                return new Ingredient { RawText = line };

            int quantityEndIndex = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                if (IsUnit(parts[i]))
                {
                    quantityEndIndex = i + 1;
                    break;
                }
            }

            string quantity = "";
            string unit = "";
            string name = "";

            if (quantityEndIndex > 0)
            {
                quantity = string.Join(" ", parts.Take(quantityEndIndex - 1));
                unit = parts[quantityEndIndex - 1];
                name = string.Join(" ", parts.Skip(quantityEndIndex));
            }
            else
            {
                name = line;
            }

            return new Ingredient
            {
                RawText = line,
                Quantity = quantity,
                Unit = unit,
                Name = name
            };
        }

        private bool IsUnit(string word)
        {
            var commonUnits = new[] { "cup", "cups", "tsp", "tbsp", "oz", "lb", "ml", "l", "g", "kg", "pinch", "dash", "can", "box" };
            return commonUnits.Any(u => word.Equals(u, StringComparison.OrdinalIgnoreCase));
        }

        private List<Step> ExtractSteps(string text)
        {
            var steps = new List<Step>();
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var inStepsSection = false;
            var stepNumber = 1;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed.Equals("INSTRUCTIONS", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("INSTRUCTIONS:", StringComparison.OrdinalIgnoreCase))
                {
                    inStepsSection = true;
                    continue;
                }

                if (inStepsSection && !string.IsNullOrWhiteSpace(trimmed))
                {
                    // Remove step number if present (e.g., "1. Preheat..." -> "Preheat...")
                    string stepText = trimmed;
                    if (char.IsDigit(trimmed[0]) && trimmed.Length > 2 && trimmed[1] == '.')
                    {
                        stepText = trimmed.Substring(2).Trim();
                    }

                    steps.Add(new Step
                    {
                        Ordinal = stepNumber++,
                        Text = stepText
                    });
                }
            }

            return steps;
        }

        private List<string> ExtractTags(string text)
        {
            var tags = new List<string>();

            if (text.Contains("chocolate", StringComparison.OrdinalIgnoreCase))
                tags.Add("chocolate");
            if (text.Contains("cookie", StringComparison.OrdinalIgnoreCase))
                tags.Add("cookies");
            if (text.Contains("bake", StringComparison.OrdinalIgnoreCase))
                tags.Add("baked");
            if (text.Contains("dessert", StringComparison.OrdinalIgnoreCase))
                tags.Add("dessert");

            return tags;
        }
    }
}
