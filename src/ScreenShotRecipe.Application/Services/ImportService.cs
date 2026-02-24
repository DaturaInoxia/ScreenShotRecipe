using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ScreenShotRecipe.Application.Contracts.Dtos;
using ScreenShotRecipe.Domain.Entities;
using ScreenShotRecipe.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ScreenShotRecipe.Application.Services
{
    public class ImportService
    {
        private readonly IOcrClient _ocr;
        private readonly ILLMParser _parser;
        private readonly IStorage _storage;
        private readonly IRecipeRepository _repo;
        private readonly ILogger<ImportService> _logger;

        public ImportService(IOcrClient ocr, ILLMParser parser, IStorage storage, IRecipeRepository repo, ILogger<ImportService> logger)
        {
            _ocr = ocr;
            _parser = parser;
            _storage = storage;
            _repo = repo;
            _logger = logger;
        }

        public async Task<RecipeDto> ImportImagesAsync(List<(byte[] Bytes, string FileName)> images)
        {
            try
            {
                var importJobId = Guid.NewGuid();
                _logger.LogInformation("Starting import job {ImportJobId} with {ImageCount} image(s)", importJobId, images.Count);
                
                // Save images to storage and run OCR per image
                var ocrOutputs = new List<string>();

                foreach (var img in images)
                {
                    try
                    {
                        var path = await _storage.SaveImageAsync(img.Bytes, img.FileName);
                        _logger.LogInformation("Image saved to storage: {ImagePath}", path);
                        
                        var res = await _ocr.RecognizeAsync(img.Bytes);
                        ocrOutputs.Add(res.RawText);
                        _logger.LogInformation("OCR processing completed for image: {FileName}, Text length: {TextLength}", img.FileName, res.RawText?.Length ?? 0);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing image: {FileName}", img.FileName);
                        throw;
                    }
                }

                // Combine OCR outputs in order
                var combined = string.Join("\n\n", ocrOutputs.Where(s => !string.IsNullOrWhiteSpace(s)));
                _logger.LogInformation("OCR outputs combined, total text length: {TextLength}", combined.Length);

                // Parse combined text
                _logger.LogInformation("Starting LLM parsing for combined OCR text");
                var parseResult = await _parser.ParseAsync(combined);
                _logger.LogInformation("LLM parsing completed. Parse result confidence: {Confidence}, Recipe title: {RecipeTitle}", 
                    parseResult.Confidence, parseResult.Recipe?.Title ?? "N/A");

                // Persist recipe
                var recipe = parseResult.Recipe;
                recipe.ImportJobId = importJobId;

                await _repo.AddAsync(recipe);
                _logger.LogInformation("Recipe persisted to repository. Recipe ID: {RecipeId}, Title: {Title}, Ingredients: {IngredientCount}, Steps: {StepCount}", 
                    recipe.Id, recipe.Title, recipe.Ingredients.Count, recipe.Steps.Count);

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

                _logger.LogInformation("Import job {ImportJobId} completed successfully", importJobId);
                return dto;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Import job failed");
                throw;
            }
        }
    }
}
