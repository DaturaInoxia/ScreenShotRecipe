using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ScreenShotRecipe.Domain.Interfaces;
using ScreenShotRecipe.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ScreenShotRecipe.Infrastructure.Parsing
{
    /// <summary>
    /// Fake/mock LLM parser for testing the full import pipeline without external AI dependencies.
    /// Returns deterministic results by parsing OCR text into structured recipes.
    /// Simulates realistic parsing with configurable confidence levels.
    /// </summary>
    public class FakeLLMParser : ILLMParser
    {
        private readonly int? _seedForcedConfidenceLevel;  // 1-100 for testing different confidence scenarios
        private readonly ILogger<FakeLLMParser> _logger;

        public FakeLLMParser(ILogger<FakeLLMParser> logger)
        {
            _logger = logger;
            // Default: simulate high-quality parsing results
            _logger.LogInformation("FakeLLMParser initialized (development/testing mode, no forced confidence override)");
        }

        /// <summary>
        /// Optional constructor for testing specific confidence levels.
        /// </summary>
        /// <param name="forcedConfidenceLevel">1-100 to simulate parsing quality (e.g., 90 = 90% confidence)</param>
        public FakeLLMParser(int? forcedConfidenceLevel, ILogger<FakeLLMParser> logger) : this(logger)
        {
            _seedForcedConfidenceLevel = forcedConfidenceLevel;
            if (forcedConfidenceLevel.HasValue)
            {
                _logger.LogInformation("FakeLLMParser initialized with forced confidence level: {ConfidenceLevel}%", forcedConfidenceLevel);
            }
        }

        public Task<ParseResult> ParseAsync(string combinedText)
        {
            return ParseAsync(combinedText, CancellationToken.None);
        }

        public Task<ParseResult> ParseAsync(string combinedText, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("ParseAsync initiated with text length: {TextLength} characters", combinedText?.Length ?? 0);

                if (string.IsNullOrWhiteSpace(combinedText))
                {
                    _logger.LogError("ParseAsync called with empty or null combined text");
                    throw new ArgumentException("Combined text cannot be empty.", nameof(combinedText));
                }

                // Simulate async processing
                cancellationToken.ThrowIfCancellationRequested();

                var recipe = new Recipe
                {
                    Id = Guid.NewGuid(),
                    Title = ExtractTitle(combinedText),
                    Notes = ExtractNotes(combinedText)
                };
                
                _logger.LogInformation("Recipe entity created. ID: {RecipeId}, Title: {Title}", recipe.Id, recipe.Title);

                // Parse ingredients section
                var ingredients = ExtractIngredients(combinedText);
                foreach (var ing in ingredients)
                {
                    recipe.Ingredients.Add(ing);
                }
                _logger.LogInformation("Ingredients extracted: {IngredientCount} items", ingredients.Count);

                // Parse steps section
                var steps = ExtractSteps(combinedText);
                foreach (var step in steps)
                {
                    recipe.Steps.Add(step);
                }
                _logger.LogInformation("Steps extracted: {StepCount} items", steps.Count);

                // Add tags based on content
                recipe.Tags.AddRange(ExtractTags(combinedText));
                _logger.LogInformation("Tags extracted: {TagList}", string.Join(", ", recipe.Tags));

                // Calculate realistic confidence
                var confidence = CalculateConfidence(combinedText, ingredients.Count, steps.Count);
                _logger.LogInformation("Parse confidence calculated: {Confidence:P}", confidence);

                var result = new ParseResult(recipe, confidence);
                _logger.LogInformation("ParseAsync completed successfully");
                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ParseAsync");
                throw;
            }
        }

        private string ExtractTitle(string text)
        {
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var firstLine = lines.FirstOrDefault()?.Trim() ?? "Untitled Recipe";
            
            // Return first non-empty line, stripping any markdown or extra formatting
            return firstLine.TrimStart('#', '*', '-', '_').Trim();
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
                    return string.Join(" ", lines.Skip(1).Select(l => l.Trim())).Trim();
                }
            }
            return string.Empty;
        }

        private List<Ingredient> ExtractIngredients(string text)
        {
            var ingredients = new List<Ingredient>();
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var inIngredientsSection = false;

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
                    trimmed.StartsWith("STEP", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("NOTES", StringComparison.OrdinalIgnoreCase))
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
            // Simple parsing: "2 1/4 cups all-purpose flour" -> Quantity: 2.25, Unit: "cups", Name: "all-purpose flour"
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                return new Ingredient 
                { 
                    RawText = line,
                    Name = line
                };
            }

            int quantityEndIndex = 0;
            for (int i = 0; i < parts.Length; i++)
            {
                if (IsUnit(parts[i]))
                {
                    quantityEndIndex = i + 1;
                    break;
                }
            }

            string quantityStr = "";
            string unit = "";
            string name = "";

            if (quantityEndIndex > 0)
            {
                quantityStr = string.Join(" ", parts.Take(quantityEndIndex - 1));
                unit = parts[quantityEndIndex - 1];
                name = string.Join(" ", parts.Skip(quantityEndIndex));
            }
            else
            {
                name = line;
            }

            // Try to parse quantity to decimal
            decimal? quantity = null;
            if (!string.IsNullOrWhiteSpace(quantityStr))
            {
                // Handle fractions like "2 1/4"
                if (decimal.TryParse(quantityStr.Split('/')[0].Split(' ').Last(), out var result))
                {
                    quantity = result;
                }
            }

            return new Ingredient
            {
                RawText = line,
                Quantity = quantity?.ToString(),
                Unit = string.IsNullOrWhiteSpace(unit) ? null : unit,
                Name = string.IsNullOrWhiteSpace(name) ? line : name
            };
        }

        private bool IsUnit(string word)
        {
            var commonUnits = new[] 
            { 
                "cup", "cups", "tsp", "tbsp", "teaspoon", "tablespoon", "oz", "ounce", 
                "lb", "lbs", "pound", "ml", "l", "g", "kg", "mg", "pinch", "dash", 
                "can", "cans", "box", "boxes", "clove", "cloves", "slice", "slices"
            };
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
                    trimmed.Equals("INSTRUCTIONS:", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("STEPS", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("STEPS:", StringComparison.OrdinalIgnoreCase))
                {
                    inStepsSection = true;
                    continue;
                }

                if (trimmed.Equals("NOTES", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("NOTES:", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("TIPS", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Equals("TIPS:", StringComparison.OrdinalIgnoreCase))
                {
                    inStepsSection = false;
                }

                if (inStepsSection && !string.IsNullOrWhiteSpace(trimmed))
                {
                    // Remove step number if present (e.g., "1. Preheat..." -> "Preheat...")
                    string stepText = trimmed;
                    if (trimmed.Length > 2 && char.IsDigit(trimmed[0]) && trimmed[1] == '.')
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
            var lowerText = text.ToLowerInvariant();

            // Cuisine/type tags
            if (lowerText.Contains("chocolate")) tags.Add("chocolate");
            if (lowerText.Contains("cookie") || lowerText.Contains("cookies")) tags.Add("cookies");
            if (lowerText.Contains("bake")) tags.Add("baked");
            if (lowerText.Contains("dessert")) tags.Add("dessert");
            if (lowerText.Contains("pasta")) tags.Add("pasta");
            if (lowerText.Contains("stir")) tags.Add("stir-fry");
            if (lowerText.Contains("vegetable")) tags.Add("vegetables");
            if (lowerText.Contains("vegan")) tags.Add("vegan");
            if (lowerText.Contains("gluten")) tags.Add("gluten-free");
            
            // Difficulty/time tags
            if (lowerText.Contains("quick") || lowerText.Contains("easy")) tags.Add("easy");
            if (lowerText.Contains("under 30")) tags.Add("quick");
            if (lowerText.Contains("slow")) tags.Add("slow-cooker");

            return tags.Distinct().ToList();
        }

        /// <summary>
        /// Calculate realistic confidence score based on parsing difficulty and content quality.
        /// </summary>
        private double CalculateConfidence(string text, int ingredientCount, int stepCount)
        {
            // If forced confidence for testing, use it
            if (_seedForcedConfidenceLevel.HasValue)
            {
                return _seedForcedConfidenceLevel.Value / 100.0;
            }

            double confidence = 0.85;  // Base confidence

            // Adjust based on structure quality
            if (text.Contains("INGREDIENTS", StringComparison.OrdinalIgnoreCase) && 
                text.Contains("INSTRUCTIONS", StringComparison.OrdinalIgnoreCase))
            {
                confidence += 0.08;  // Well-structured recipe
            }

            // Adjust based on content volume
            if (ingredientCount >= 5 && stepCount >= 5)
            {
                confidence += 0.05;  // More content = more reliable parsing
            }

            // Penalize for missing sections
            if (ingredientCount == 0) confidence -= 0.10;
            if (stepCount == 0) confidence -= 0.10;

            // Cap between 0.5 and 0.95 for realism
            return Math.Max(0.5, Math.Min(0.95, confidence));
        }
    }
}
