using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ScreenShotRecipe.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ScreenShotRecipe.Infrastructure.Ocr
{
    /// <summary>
    /// Fake/mock OCR service for testing the full import pipeline without external AI dependencies.
    /// Returns deterministic results based on predefined test recipes.
    /// </summary>
    public class FakeRecipeOcrService : IRecipeOcrService
    {
        private readonly ILogger<FakeRecipeOcrService> _logger;

        public FakeRecipeOcrService(ILogger<FakeRecipeOcrService> logger)
        {
            _logger = logger;
            _logger.LogInformation("FakeRecipeOcrService initialized (development/testing mode)");
        }

        private static readonly Dictionary<string, List<string>> TestRecipes = new()
        {
            {
                "chocolate_chip_cookies", new List<string>
                {
                    // Page 1: Title and ingredients
                    @"Chocolate Chip Cookies

INGREDIENTS:
2 1/4 cups all-purpose flour
1 tsp baking soda
1 tsp salt
1 cup butter, softened
3/4 cup granulated sugar
3/4 cup packed brown sugar
2 large eggs
2 tsp vanilla extract
2 cups chocolate chips",
                    // Page 2: Instructions
                    @"INSTRUCTIONS:
1. Preheat oven to 375°F (190°C)
2. Mix flour, baking soda and salt in small bowl
3. Beat butter and sugars until creamy
4. Add eggs and vanilla extract
5. Gradually blend in flour mixture
6. Stir in chocolate chips
7. Drop by rounded tablespoon onto ungreased baking sheets
8. Bake for 9-11 minutes or until golden brown
9. Cool on baking sheets for 2 minutes
10. Remove to wire racks to cool completely"
                }
            },
            {
                "pasta_marinara", new List<string>
                {
                    @"Pasta Marinara

INGREDIENTS:
1 lb spaghetti
2 tbsp olive oil
4 cloves garlic, minced
1 can (28 oz) crushed tomatoes
2 tsp dried oregano
1 tsp dried basil
Salt and pepper to taste",
                    @"INSTRUCTIONS:
1. Cook spaghetti according to package directions. Drain and set aside.
2. Heat olive oil in a large saucepan over medium heat.
3. Add minced garlic and cook for 1 minute until fragrant.
4. Pour in crushed tomatoes with their juice.
5. Add oregano, basil, salt and pepper.
6. Simmer for 15-20 minutes, stirring occasionally.
7. Toss hot pasta with sauce.
8. Serve with fresh parmesan cheese if desired."
                }
            },
            {
                "vegetable_stir_fry", new List<string>
                {
                    @"Vegetable Stir-Fry

INGREDIENTS:
2 tbsp vegetable oil
2 cups broccoli florets
1 red bell pepper, sliced
1 cup snap peas
1 cup mushrooms, sliced
3 cloves garlic, minced
2 tbsp soy sauce
1 tbsp ginger, grated
1 tsp sesame oil",
                    @"INSTRUCTIONS:
1. Heat vegetable oil in a wok or large skillet over high heat.
2. Add garlic and ginger, stir-fry for 30 seconds.
3. Add broccoli and bell pepper, cook for 2-3 minutes.
4. Add snap peas and mushrooms, continue cooking for 2 minutes.
5. Pour in soy sauce and sesame oil, toss well.
6. Cook for 1 more minute until vegetables are tender-crisp.
7. Serve over rice or noodles."
                }
            }
        };

        public async Task<IReadOnlyList<RecipeOcrResult>> ExtractTextAsync(
            IReadOnlyList<ImageData> images,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("ExtractTextAsync initiated with {ImageCount} image(s)", images?.Count ?? 0);

                if (images == null || images.Count == 0)
                {
                    _logger.LogError("No images provided to ExtractTextAsync");
                    throw new ArgumentException("At least one image required.", nameof(images));
                }

                if (images.Count > 6)
                {
                    _logger.LogError("Batch size exceeds maximum. Request: {ImageCount}, Maximum: 6", images.Count);
                    throw new InvalidOperationException("Batch size exceeds maximum of 6 images.");
                }

                // Simulate some async processing time
                await Task.Delay(100, cancellationToken);
                _logger.LogInformation("OCR processing delay simulated");

                // Select a test recipe (cycle through them for variety)
                var recipes = TestRecipes.Values.ToList();
                var selectedRecipe = recipes[images.Count % recipes.Count];
                _logger.LogInformation("Selected test recipe for processing");

                var results = new List<RecipeOcrResult>();

                for (int i = 0; i < images.Count; i++)
                {
                    var image = images[i];
                    var pageText = i < selectedRecipe.Count ? selectedRecipe[i] : "Additional recipe notes and variations.";

                    results.Add(new RecipeOcrResult(
                        ExtractedText: pageText,
                        DetectedLanguage: "en",
                        Confidence: 0.92m,  // High confidence for fake data
                        ImageFileName: image.FileName,
                        PageOrder: i
                    ));
                    
                    _logger.LogInformation("Processed image: {FileName}, Page: {PageOrder}, TextLength: {TextLength}", 
                        image.FileName, i, pageText.Length);
                }

                _logger.LogInformation("ExtractTextAsync completed successfully with {ResultCount} results", results.Count);
                return results.AsReadOnly();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ExtractTextAsync with {ImageCount} image(s)", images?.Count ?? 0);
                throw;
            }
        }
    }
}
