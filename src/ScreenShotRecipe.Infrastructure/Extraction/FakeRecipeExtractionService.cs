using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ScreenShotRecipe.Domain.Entities;
using ScreenShotRecipe.Domain.Interfaces;

namespace ScreenShotRecipe.Infrastructure.Extraction
{
    /// <summary>
    /// Fake implementation of IRecipeExtractionService for testing and offline scenarios.
    /// Returns predictable test data without calling any external API.
    /// </summary>
    public class FakeRecipeExtractionService : IRecipeExtractionService
    {
        private readonly ILogger<FakeRecipeExtractionService> _logger;
        private readonly decimal? _confidenceOverride;
        private readonly int _simulatedDelayMs;

        public FakeRecipeExtractionService(
            ILogger<FakeRecipeExtractionService> logger,
            decimal? confidenceOverride = null,
            int simulatedDelayMs = 100)
        {
            _logger = logger;
            _confidenceOverride = confidenceOverride;
            _simulatedDelayMs = simulatedDelayMs;
            
            _logger.LogInformation(
                "FakeRecipeExtractionService initialized (testing mode). " +
                "Confidence override: {ConfidenceOverride}, Simulated delay: {DelayMs}ms",
                confidenceOverride?.ToString("P0") ?? "none",
                simulatedDelayMs);
        }

        public async Task<RecipeExtractionResult> ExtractRecipeAsync(
            IReadOnlyList<RecipeImageInput> images,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation(
                "FakeRecipeExtractionService: Extracting recipe from {ImageCount} image(s)",
                images.Count);

            if (images.Count == 0)
            {
                throw new RecipeExtractionException(
                    "No images provided for extraction",
                    imagesAttempted: 0);
            }

            // Log image details
            for (int i = 0; i < images.Count; i++)
            {
                var img = images[i];
                _logger.LogDebug(
                    "  Image {Index}: {FileName} ({MediaType}, {Size} bytes, order: {Order})",
                    i + 1, img.FileName, img.MediaType, img.Content.Length, img.PageOrder);
            }

            // Simulate API delay
            if (_simulatedDelayMs > 0)
            {
                await Task.Delay(_simulatedDelayMs, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // Generate fake recipe based on image count
            var recipe = GenerateFakeRecipe(images.Count);
            var confidence = _confidenceOverride ?? 0.95m;

            stopwatch.Stop();

            _logger.LogInformation(
                "FakeRecipeExtractionService: Extraction complete. " +
                "Recipe: '{Title}', Ingredients: {IngredientCount}, Steps: {StepCount}, " +
                "Confidence: {Confidence:P0}, Duration: {Duration}ms",
                recipe.Title,
                recipe.Ingredients.Count,
                recipe.Steps.Count,
                confidence,
                stopwatch.ElapsedMilliseconds);

            return new RecipeExtractionResult(
                Recipe: recipe,
                Confidence: confidence,
                RawResponse: GenerateFakeRawResponse(recipe),
                ImagesProcessed: images.Count,
                ProcessingTime: stopwatch.Elapsed);
        }

        private Recipe GenerateFakeRecipe(int imageCount)
        {
            return new Recipe
            {
                Id = Guid.NewGuid(),
                Title = "Chocolate Chip Cookies",
                Notes = $"Extracted from {imageCount} image(s) using fake extraction service.",
                Ingredients = new List<Ingredient>
                {
                    new() { RawText = "2 1/4 cups all-purpose flour", Name = "all-purpose flour", Quantity = "2 1/4", Unit = "cups" },
                    new() { RawText = "1 tsp baking soda", Name = "baking soda", Quantity = "1", Unit = "tsp" },
                    new() { RawText = "1 tsp salt", Name = "salt", Quantity = "1", Unit = "tsp" },
                    new() { RawText = "1 cup (2 sticks) butter, softened", Name = "butter, softened", Quantity = "1", Unit = "cup" },
                    new() { RawText = "3/4 cup granulated sugar", Name = "granulated sugar", Quantity = "3/4", Unit = "cup" },
                    new() { RawText = "3/4 cup packed brown sugar", Name = "packed brown sugar", Quantity = "3/4", Unit = "cup" },
                    new() { RawText = "2 large eggs", Name = "eggs", Quantity = "2", Unit = "large" },
                    new() { RawText = "1 tsp vanilla extract", Name = "vanilla extract", Quantity = "1", Unit = "tsp" },
                    new() { RawText = "2 cups chocolate chips", Name = "chocolate chips", Quantity = "2", Unit = "cups" }
                },
                Steps = new List<Step>
                {
                    new() { Ordinal = 1, Text = "Preheat oven to 375°F (190°C)." },
                    new() { Ordinal = 2, Text = "Combine flour, baking soda and salt in a small bowl." },
                    new() { Ordinal = 3, Text = "Beat butter, granulated sugar, brown sugar and vanilla extract in a large mixer bowl until creamy." },
                    new() { Ordinal = 4, Text = "Add eggs, one at a time, beating well after each addition." },
                    new() { Ordinal = 5, Text = "Gradually beat in flour mixture." },
                    new() { Ordinal = 6, Text = "Stir in chocolate chips." },
                    new() { Ordinal = 7, Text = "Drop rounded tablespoon of dough onto ungreased baking sheets." },
                    new() { Ordinal = 8, Text = "Bake for 9 to 11 minutes or until golden brown." },
                    new() { Ordinal = 9, Text = "Cool on baking sheets for 2 minutes." },
                    new() { Ordinal = 10, Text = "Remove to wire racks to cool completely." }
                },
                Tags = new List<string> { "cookies", "chocolate", "dessert", "baking" },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private string GenerateFakeRawResponse(Recipe recipe)
        {
            return $@"{{
  ""title"": ""{recipe.Title}"",
  ""ingredients"": [{string.Join(",", recipe.Ingredients.ConvertAll(i => $@"
    {{""raw"": ""{i.RawText}"", ""name"": ""{i.Name}"", ""quantity"": ""{i.Quantity}"", ""unit"": ""{i.Unit}""}}"))}
  ],
  ""steps"": [{string.Join(",", recipe.Steps.ConvertAll(s => $@"
    {{""ordinal"": {s.Ordinal}, ""text"": ""{s.Text}""}}"))}
  ],
  ""notes"": ""{recipe.Notes}"",
  ""tags"": [{string.Join(", ", recipe.Tags.ConvertAll(t => $@"""{t}"""))}]
}}";
        }
    }
}
