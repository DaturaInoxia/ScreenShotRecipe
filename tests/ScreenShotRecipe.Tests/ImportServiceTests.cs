using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ScreenShotRecipe.Application.Services;
using ScreenShotRecipe.Domain.Entities;
using ScreenShotRecipe.Domain.Interfaces;
using Xunit;

namespace ScreenShotRecipe.Tests
{
    /// <summary>
    /// Unit tests for ImportService orchestration layer.
    /// Tests the complete import pipeline: images → storage → OCR → parsing → persistence
    /// </summary>
    public class ImportServiceTests
    {
        [Fact]
        public async Task ImportImagesAsync_WithValidImages_ReturnsRecipeDtoWithParsedData()
        {
            // Arrange
            var imageBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header
            var fileName = "recipe.png";
            var images = new List<(byte[] Bytes, string FileName)> { (imageBytes, fileName) };

            var ocrClient = new MockOcrClient();
            var parser = new MockLLMParser();
            var storage = new MockStorage();
            var repository = new MockRecipeRepository();

            var service = new ImportService(ocrClient, parser, storage, repository);

            // Act
            var result = await service.ImportImagesAsync(images);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Chocolate Chip Cookies", result.Title);
            Assert.NotEmpty(result.Ingredients);
            Assert.NotEmpty(result.Steps);
            Assert.True(result.Ingredients.Count >= 2, "Should have multiple ingredients");
            Assert.True(result.Steps.Count >= 2, "Should have multiple steps");
        }

        [Fact]
        public async Task ImportImagesAsync_WithMultipleImages_CombinesOCRResults()
        {
            // Arrange
            var images = new List<(byte[] Bytes, string FileName)>
            {
                (new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "page1.png"),
                (new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "page2.jpg"),
            };

            var ocrClient = new MockOcrClient();
            var parser = new MockLLMParser();
            var storage = new MockStorage();
            var repository = new MockRecipeRepository();

            var service = new ImportService(ocrClient, parser, storage, repository);

            // Act
            var result = await service.ImportImagesAsync(images);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result.Ingredients);
            Assert.NotEmpty(result.Steps);
            Assert.Equal(2, storage.SavedImageCount); // Both images should be saved
        }


        [Fact]
        public async Task ImportImagesAsync_PersistsRecipeToRepository()
        {
            // Arrange
            var images = new List<(byte[] Bytes, string FileName)>
            {
                (new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "recipe.png"),
            };

            var ocrClient = new MockOcrClient();
            var parser = new MockLLMParser();
            var storage = new MockStorage();
            var repository = new MockRecipeRepository();
            var service = new ImportService(ocrClient, parser, storage, repository);

            // Act
            var result = await service.ImportImagesAsync(images);

            // Assert
            Assert.True(repository.AddAsyncCalled, "Repository.AddAsync should have been called");
            Assert.NotNull(repository.AddedRecipe);
            Assert.Equal("Chocolate Chip Cookies", repository.AddedRecipe.Title);
        }

        [Fact]
        public async Task ImportImagesAsync_SavesImagesToStorage()
        {
            // Arrange
            var images = new List<(byte[] Bytes, string FileName)>
            {
                (new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "recipe1.png"),
                (new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "recipe2.jpg"),
            };

            var ocrClient = new MockOcrClient();
            var parser = new MockLLMParser();
            var storage = new MockStorage();
            var repository = new MockRecipeRepository();
            var service = new ImportService(ocrClient, parser, storage, repository);

            // Act
            await service.ImportImagesAsync(images);

            // Assert
            Assert.Equal(2, storage.SavedImageCount);
            Assert.Contains("recipe1.png", storage.SavedFileNames);
            Assert.Contains("recipe2.jpg", storage.SavedFileNames);
        }

        [Fact]
        public async Task ImportImagesAsync_CallsOCRForEachImage()
        {
            // Arrange
            var images = new List<(byte[] Bytes, string FileName)>
            {
                (new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "img1.png"),
                (new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "img2.jpg"),
                (new byte[] { 0x47, 0x49, 0x46, 0x38 }, "img3.gif"),
            };

            var ocrClient = new MockOcrClient();
            var parser = new MockLLMParser();
            var storage = new MockStorage();
            var repository = new MockRecipeRepository();
            var service = new ImportService(ocrClient, parser, storage, repository);

            // Act
            await service.ImportImagesAsync(images);

            // Assert
            Assert.Equal(3, ocrClient.RecognizeCallCount);
        }

        [Fact]
        public async Task ImportImagesAsync_CallsLLMParserOnce()
        {
            // Arrange
            var images = new List<(byte[] Bytes, string FileName)>
            {
                (new byte[] { 0x89, 0x50, 0x4E, 0x47 }, "img1.png"),
                (new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "img2.jpg"),
            };

            var ocrClient = new MockOcrClient();
            var parser = new MockLLMParser();
            var storage = new MockStorage();
            var repository = new MockRecipeRepository();
            var service = new ImportService(ocrClient, parser, storage, repository);

            // Act
            await service.ImportImagesAsync(images);

            // Assert
            Assert.Equal(1, parser.ParseAsyncCallCount);
            Assert.Contains("Chocolate Chip Cookies", parser.LastParsedText);
        }
    }

    // ============================================================================
    // Mock Implementations for Testing
    // ============================================================================

    /// <summary>
    /// Mock OCR client that returns deterministic recipe text
    /// </summary>
    public class MockOcrClient : IOcrClient
    {
        public int RecognizeCallCount { get; private set; }

        public Task<OcrResult> RecognizeAsync(byte[] imageBytes)
        {
            RecognizeCallCount++;
            var mockText = @"Chocolate Chip Cookies

INGREDIENTS:
2 1/4 cups all-purpose flour
1 tsp baking soda
1 tsp salt
1 cup butter, softened
3/4 cup granulated sugar
3/4 cup packed brown sugar
2 large eggs
2 tsp vanilla extract
2 cups chocolate chips

INSTRUCTIONS:
1. Preheat oven to 375°F
2. Mix flour, baking soda, and salt
3. Beat butter and sugars
4. Add eggs and vanilla
5. Blend in flour mixture
6. Stir in chocolate chips
7. Drop onto baking sheets
8. Bake 9-11 minutes";

            return Task.FromResult(new OcrResult(mockText, 0.95));
        }
    }

    /// <summary>
    /// Mock LLM parser that returns structured recipe
    /// </summary>
    public class MockLLMParser : ILLMParser
    {
        public int ParseAsyncCallCount { get; private set; }
        public string LastParsedText { get; private set; } = string.Empty;

        public Task<ParseResult> ParseAsync(string combinedText)
        {
            ParseAsyncCallCount++;
            LastParsedText = combinedText;

            var recipe = new Recipe
            {
                Id = Guid.NewGuid(),
                Title = "Chocolate Chip Cookies",
                Notes = "Classic homemade cookies",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };

            recipe.Ingredients.Add(new Ingredient { RawText = "2 1/4 cups all-purpose flour", Name = "all-purpose flour", Quantity = "2 1/4", Unit = "cups" });
            recipe.Ingredients.Add(new Ingredient { RawText = "1 tsp baking soda", Name = "baking soda", Quantity = "1", Unit = "tsp" });
            recipe.Ingredients.Add(new Ingredient { RawText = "2 cups chocolate chips", Name = "chocolate chips", Quantity = "2", Unit = "cups" });

            recipe.Steps.Add(new Step { Ordinal = 1, Text = "Preheat oven to 375°F" });
            recipe.Steps.Add(new Step { Ordinal = 2, Text = "Mix flour, baking soda, and salt" });
            recipe.Steps.Add(new Step { Ordinal = 3, Text = "Beat butter and sugars" });
            recipe.Steps.Add(new Step { Ordinal = 4, Text = "Add eggs and vanilla" });

            recipe.Tags.Add("cookies");
            recipe.Tags.Add("chocolate");
            recipe.Tags.Add("baked");

            return Task.FromResult(new ParseResult(recipe, 0.88));
        }
    }

    /// <summary>
    /// Mock storage provider
    /// </summary>
    public class MockStorage : IStorage
    {
        public int SavedImageCount { get; private set; }
        public List<string> SavedFileNames { get; } = new();

        public Task<string> SaveImageAsync(byte[] bytes, string filename)
        {
            SavedImageCount++;
            SavedFileNames.Add(filename);
            return Task.FromResult($"./data/images/{Guid.NewGuid()}_{filename}");
        }

        public Task<byte[]?> ReadImageAsync(string path)
        {
            return Task.FromResult<byte[]?>(new byte[] { 0x89, 0x50, 0x4E, 0x47 });
        }
    }

    /// <summary>
    /// Mock recipe repository
    /// </summary>
    public class MockRecipeRepository : IRecipeRepository
    {
        public bool AddAsyncCalled { get; private set; }
        public Recipe? AddedRecipe { get; private set; }
        private Dictionary<Guid, Recipe> _recipes = new();

        public Task AddAsync(Recipe recipe)
        {
            AddAsyncCalled = true;
            AddedRecipe = recipe;
            _recipes[recipe.Id] = recipe;
            return Task.CompletedTask;
        }

        public Task<Recipe?> GetAsync(Guid id)
        {
            _recipes.TryGetValue(id, out var recipe);
            return Task.FromResult(recipe);
        }

        public Task<List<Recipe>> GetAllAsync()
        {
            return Task.FromResult(new List<Recipe>(_recipes.Values));
        }
    }
}

