using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScreenShotRecipe.Domain.Entities;
using ScreenShotRecipe.Domain.Interfaces;
using ScreenShotRecipe.Infrastructure.Config;

namespace ScreenShotRecipe.Infrastructure.Extraction
{
    // Note: GptExtractionOptions has been replaced by AzureOpenAIOptions in Config folder.
    // Keeping this type alias for backward compatibility.
    [Obsolete("Use AzureOpenAIOptions instead")]
    public class GptExtractionOptions : AzureOpenAIOptions
    {
        public new const string SectionName = AzureOpenAIOptions.SectionName;
    }

    /// <summary>
    /// GPT-4o implementation of IRecipeExtractionService.
    /// Uses multimodal capabilities to extract structured recipe data from images in a single call.
    /// </summary>
    public class GptRecipeExtractionService : IRecipeExtractionService
    {
        private readonly HttpClient _httpClient;
        private readonly AzureOpenAIOptions _options;
        private readonly ILogger<GptRecipeExtractionService> _logger;

        private const string SystemPrompt = @"You are a recipe extraction assistant. Analyze the provided recipe image(s) and extract structured recipe data.

Return a JSON object with this exact structure:
{
  ""title"": ""Recipe Title"",
  ""notes"": ""Any general notes about the recipe (optional, can be null)"",
  ""ingredients"": [
    {
      ""rawText"": ""Original text as shown in image"",
      ""name"": ""Ingredient name"",
      ""quantity"": ""Amount as string (e.g., '2 1/4', '1/2')"",
      ""unit"": ""Unit of measurement (e.g., 'cups', 'tsp', 'lb')""
    }
  ],
  ""steps"": [
    {
      ""ordinal"": 1,
      ""text"": ""Step instruction text""
    }
  ],
  ""tags"": [""tag1"", ""tag2""]
}

Guidelines:
- Extract ALL ingredients and steps from the image(s)
- Preserve the original text in rawText fields
- Parse quantities and units when possible; use null if unclear
- Number steps sequentially starting from 1
- Add relevant tags (cuisine type, dish category, cooking method, dietary info)
- If multiple images, combine content logically (ingredients from all, steps in order)
- Return ONLY valid JSON, no markdown formatting or additional text";

        public GptRecipeExtractionService(
            HttpClient httpClient,
            IOptions<AzureOpenAIOptions> options,
            ILogger<GptRecipeExtractionService> logger)
        {
            _httpClient = httpClient;
            _options = options.Value;
            _logger = logger;

            if (!_options.IsValid(out var errors))
            {
                throw new InvalidOperationException($"Invalid AzureOpenAIOptions: {string.Join(", ", errors)}");
            }

            _logger.LogInformation(
                "GptRecipeExtractionService initialized. {ConfigSummary}",
                _options.GetSafeConfigSummary());
        }

        public async Task<RecipeExtractionResult> ExtractRecipeAsync(
            IReadOnlyList<RecipeImageInput> images,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("Starting GPT-4o recipe extraction for {ImageCount} image(s)", images.Count);

            if (images.Count == 0)
            {
                throw new RecipeExtractionException("No images provided for extraction", imagesAttempted: 0);
            }

            try
            {
                // Build the request
                var request = BuildRequest(images);
                var requestJson = JsonSerializer.Serialize(request);
                
                _logger.LogDebug("GPT-4o request payload size: {Size} bytes", requestJson.Length);

                // Make the API call
                using var httpRequest = CreateHttpRequest(requestJson);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

                var response = await _httpClient.SendAsync(httpRequest, cts.Token);
                var responseBody = await response.Content.ReadAsStringAsync(cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "GPT-4o API error: {StatusCode} - {Response}",
                        response.StatusCode, responseBody);
                    throw new RecipeExtractionException(
                        $"GPT-4o API returned {response.StatusCode}",
                        imagesAttempted: images.Count,
                        rawResponse: responseBody);
                }

                // Parse the response
                var apiResponse = JsonSerializer.Deserialize<GptResponse>(responseBody);
                var content = apiResponse?.Choices?.FirstOrDefault()?.Message?.Content;

                if (string.IsNullOrEmpty(content))
                {
                    throw new RecipeExtractionException(
                        "GPT-4o returned empty content",
                        imagesAttempted: images.Count,
                        rawResponse: responseBody);
                }

                _logger.LogDebug("GPT-4o raw response content: {Content}", content);

                // Parse the recipe JSON
                var recipe = ParseRecipeJson(content);

                stopwatch.Stop();

                _logger.LogInformation(
                    "GPT-4o extraction complete. Recipe: '{Title}', " +
                    "Ingredients: {IngredientCount}, Steps: {StepCount}, Duration: {Duration}ms",
                    recipe.Title, recipe.Ingredients.Count, recipe.Steps.Count, stopwatch.ElapsedMilliseconds);

                return new RecipeExtractionResult(
                    Recipe: recipe,
                    Confidence: 0.95m, // GPT-4o typically has high confidence
                    RawResponse: content,
                    ImagesProcessed: images.Count,
                    ProcessingTime: stopwatch.Elapsed);
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("GPT-4o extraction cancelled after {Duration}ms", stopwatch.ElapsedMilliseconds);
                throw;
            }
            catch (RecipeExtractionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GPT-4o extraction failed after {Duration}ms", stopwatch.ElapsedMilliseconds);
                throw new RecipeExtractionException(
                    $"Recipe extraction failed: {ex.Message}",
                    imagesAttempted: images.Count,
                    innerException: ex);
            }
        }

        private object BuildRequest(IReadOnlyList<RecipeImageInput> images)
        {
            var contentParts = new List<object>
            {
                new { type = "text", text = "Extract the recipe from these image(s) and return structured JSON:" }
            };

            // Add images ordered by PageOrder
            var orderedImages = images.OrderBy(i => i.PageOrder).ToList();
            foreach (var image in orderedImages)
            {
                var base64 = Convert.ToBase64String(image.Content);
                var dataUrl = $"data:{image.MediaType};base64,{base64}";
                
                contentParts.Add(new
                {
                    type = "image_url",
                    image_url = new { url = dataUrl, detail = "high" }
                });

                _logger.LogDebug(
                    "Added image to request: {FileName} ({MediaType}, {Size} bytes)",
                    image.FileName, image.MediaType, image.Content.Length);
            }

            var messages = new List<object>
            {
                new { role = "system", content = (object)SystemPrompt },
                new { role = "user", content = (object)contentParts }
            };

            return new
            {
                model = _options.UseAzure ? _options.DeploymentName : _options.OpenAIModel,
                messages = messages,
                max_tokens = _options.MaxTokens,
                temperature = _options.Temperature
            };
        }

        private HttpRequestMessage CreateHttpRequest(string jsonBody)
        {
            string url;
            if (_options.UseAzure)
            {
                url = $"{_options.Endpoint!.TrimEnd('/')}/openai/deployments/{_options.DeploymentName}/chat/completions?api-version=2024-02-15-preview";
            }
            else
            {
                url = "https://api.openai.com/v1/chat/completions";
            }

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };

            if (_options.UseAzure)
            {
                request.Headers.Add("api-key", _options.ApiKey);
            }
            else
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.OpenAIApiKey);
            }

            return request;
        }

        private Recipe ParseRecipeJson(string jsonContent)
        {
            // Clean up potential markdown formatting
            var cleaned = jsonContent.Trim();
            if (cleaned.StartsWith("```json"))
                cleaned = cleaned.Substring(7);
            if (cleaned.StartsWith("```"))
                cleaned = cleaned.Substring(3);
            if (cleaned.EndsWith("```"))
                cleaned = cleaned.Substring(0, cleaned.Length - 3);
            cleaned = cleaned.Trim();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var parsed = JsonSerializer.Deserialize<ParsedRecipe>(cleaned, options)
                ?? throw new RecipeExtractionException("Failed to parse recipe JSON", 0, rawResponse: cleaned);

            var recipe = new Recipe
            {
                Id = Guid.NewGuid(),
                Title = parsed.Title ?? "Untitled Recipe",
                Notes = parsed.Notes,
                Tags = parsed.Tags ?? new List<string>(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            if (parsed.Ingredients != null)
            {
                foreach (var ing in parsed.Ingredients)
                {
                    recipe.Ingredients.Add(new Ingredient
                    {
                        Id = Guid.NewGuid(),
                        RawText = ing.RawText ?? $"{ing.Quantity} {ing.Unit} {ing.Name}".Trim(),
                        Name = ing.Name,
                        Quantity = ing.Quantity,
                        Unit = ing.Unit
                    });
                }
            }

            if (parsed.Steps != null)
            {
                foreach (var step in parsed.Steps)
                {
                    recipe.Steps.Add(new Step
                    {
                        Id = Guid.NewGuid(),
                        Ordinal = step.Ordinal,
                        Text = step.Text ?? ""
                    });
                }
            }

            return recipe;
        }

        // Response DTOs for JSON deserialization
        private class GptResponse
        {
            [JsonPropertyName("choices")]
            public List<GptChoice>? Choices { get; set; }
        }

        private class GptChoice
        {
            [JsonPropertyName("message")]
            public GptMessage? Message { get; set; }
        }

        private class GptMessage
        {
            [JsonPropertyName("content")]
            public string? Content { get; set; }
        }

        private class ParsedRecipe
        {
            public string? Title { get; set; }
            public string? Notes { get; set; }
            public List<ParsedIngredient>? Ingredients { get; set; }
            public List<ParsedStep>? Steps { get; set; }
            public List<string>? Tags { get; set; }
        }

        private class ParsedIngredient
        {
            public string? RawText { get; set; }
            public string? Name { get; set; }
            public string? Quantity { get; set; }
            public string? Unit { get; set; }
        }

        private class ParsedStep
        {
            public int Ordinal { get; set; }
            public string? Text { get; set; }
        }
    }
}
