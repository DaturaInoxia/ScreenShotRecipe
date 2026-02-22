using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ScreenShotRecipe.Application.Contracts.Dtos;
using ScreenShotRecipe.Domain.Entities;
using ScreenShotRecipe.Domain.Interfaces;

namespace ScreenShotRecipe.Application.Services
{
    public class ImportService
    {
        private readonly IOcrClient _ocr;
        private readonly ILLMParser _parser;
        private readonly IStorage _storage;
        private readonly IRecipeRepository _repo;

        public ImportService(IOcrClient ocr, ILLMParser parser, IStorage storage, IRecipeRepository repo)
        {
            _ocr = ocr;
            _parser = parser;
            _storage = storage;
            _repo = repo;
        }

        public async Task<RecipeDto> ImportImagesAsync(List<(byte[] Bytes, string FileName)> images)
        {
            // Save images to storage and run OCR per image
            var ocrOutputs = new List<string>();
            var importJobId = Guid.NewGuid();

            foreach (var img in images)
            {
                var path = await _storage.SaveImageAsync(img.Bytes, img.FileName);
                var res = await _ocr.RecognizeAsync(img.Bytes);
                ocrOutputs.Add(res.RawText);
            }

            // Combine OCR outputs in order
            var combined = string.Join("\n\n", ocrOutputs.Where(s => !string.IsNullOrWhiteSpace(s)));

            // Parse combined text
            var parseResult = await _parser.ParseAsync(combined);

            // Persist recipe
            var recipe = parseResult.Recipe;
            recipe.ImportJobId = importJobId;

            await _repo.AddAsync(recipe);

            // Map to DTO (simple mapping)
            var dto = new RecipeDto
            {
                Id = recipe.Id,
                Title = recipe.Title,
                Notes = recipe.Notes,
                Ingredients = recipe.Ingredients.Select(i => new IngredientDto { RawText = i.RawText, Name = i.Name, Quantity = i.Quantity, Unit = i.Unit }).ToList(),
                Steps = recipe.Steps.Select(s => new StepDto { Ordinal = s.Ordinal, Text = s.Text }).ToList(),
                Tags = recipe.Tags
            };

            return dto;
        }
    }
}
