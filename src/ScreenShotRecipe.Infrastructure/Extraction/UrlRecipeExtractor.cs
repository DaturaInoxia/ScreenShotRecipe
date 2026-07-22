using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using ScreenShotRecipe.Domain.Entities;
using ScreenShotRecipe.Domain.Interfaces;

namespace ScreenShotRecipe.Infrastructure.Extraction
{
    /// <summary>
    /// Extracts recipes from URLs by parsing JSON-LD structured data or falling back to HTML content.
    /// </summary>
    public class UrlRecipeExtractor : IUrlRecipeExtractor
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<UrlRecipeExtractor> _logger;

        public UrlRecipeExtractor(
            HttpClient httpClient,
            ILogger<UrlRecipeExtractor> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            
            // Set a reasonable timeout and user agent
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", 
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            }
        }

        public bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            return Uri.TryCreate(url, UriKind.Absolute, out var uri) 
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        public async Task<RecipeExtractionResult> ExtractFromUrlAsync(
            string url, 
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            
            _logger.LogInformation("Starting URL recipe extraction from: {Url}", url);

            if (!IsValidUrl(url))
            {
                throw new RecipeExtractionException(
                    "Invalid URL provided. Must be a valid HTTP or HTTPS URL.",
                    imagesAttempted: 0);
            }

            string html;
            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
                html = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("Fetched {Length} bytes from URL", html.Length);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Failed to fetch URL: {Url}", url);
                throw new RecipeExtractionException(
                    $"Failed to fetch URL: {ex.Message}",
                    0,
                    ex);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError(ex, "Request timed out for URL: {Url}", url);
                throw new RecipeExtractionException(
                    "Request timed out while fetching the URL",
                    0,
                    ex);
            }

            // Try JSON-LD structured data first (most reliable)
            var recipe = TryExtractJsonLd(html, url);
            if (recipe != null)
            {
                stopwatch.Stop();
                _logger.LogInformation(
                    "Successfully extracted recipe via JSON-LD: {Title} ({Ingredients} ingredients, {Steps} steps) in {Time}ms",
                    recipe.Title, recipe.Ingredients.Count, recipe.Steps.Count, stopwatch.ElapsedMilliseconds);

                return new RecipeExtractionResult(
                    Recipe: recipe,
                    Confidence: 0.95m,
                    RawResponse: null,
                    ImagesProcessed: 0,
                    ProcessingTime: stopwatch.Elapsed);
            }

            // Try microdata format
            recipe = TryExtractMicrodata(html, url);
            if (recipe != null)
            {
                stopwatch.Stop();
                _logger.LogInformation(
                    "Successfully extracted recipe via Microdata: {Title} ({Ingredients} ingredients, {Steps} steps) in {Time}ms",
                    recipe.Title, recipe.Ingredients.Count, recipe.Steps.Count, stopwatch.ElapsedMilliseconds);

                return new RecipeExtractionResult(
                    Recipe: recipe,
                    Confidence: 0.90m,
                    RawResponse: null,
                    ImagesProcessed: 0,
                    ProcessingTime: stopwatch.Elapsed);
            }

            // Fall back to basic HTML parsing
            recipe = TryExtractFromHtml(html, url);
            if (recipe != null && recipe.Ingredients.Count > 0)
            {
                stopwatch.Stop();
                _logger.LogInformation(
                    "Extracted recipe via HTML parsing: {Title} ({Ingredients} ingredients, {Steps} steps) in {Time}ms",
                    recipe.Title, recipe.Ingredients.Count, recipe.Steps.Count, stopwatch.ElapsedMilliseconds);

                return new RecipeExtractionResult(
                    Recipe: recipe,
                    Confidence: 0.70m,
                    RawResponse: null,
                    ImagesProcessed: 0,
                    ProcessingTime: stopwatch.Elapsed);
            }

            stopwatch.Stop();
            _logger.LogWarning("Could not extract recipe from URL: {Url}", url);
            throw new RecipeExtractionException(
                "Could not find recipe data on this page. The page may not contain a recipe or uses an unsupported format.",
                imagesAttempted: 0);
        }

        private Recipe? TryExtractJsonLd(string html, string sourceUrl)
        {
            try
            {
                // Find JSON-LD script blocks
                var matches = Regex.Matches(
                    html, 
                    @"<script[^>]+type\s*=\s*[""']application/ld\+json[""'][^>]*>(.*?)</script>",
                    RegexOptions.Singleline | RegexOptions.IgnoreCase);

                foreach (Match match in matches)
                {
                    var jsonContent = match.Groups[1].Value.Trim();
                    if (string.IsNullOrEmpty(jsonContent))
                        continue;

                    try
                    {
                        using var doc = JsonDocument.Parse(jsonContent);
                        var root = doc.RootElement;

                        // Handle @graph arrays (common pattern)
                        if (root.TryGetProperty("@graph", out var graph))
                        {
                            foreach (var item in graph.EnumerateArray())
                            {
                                var recipe = TryParseRecipeSchema(item, sourceUrl);
                                if (recipe != null) return recipe;
                            }
                        }
                        // Handle direct arrays
                        else if (root.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in root.EnumerateArray())
                            {
                                var recipe = TryParseRecipeSchema(item, sourceUrl);
                                if (recipe != null) return recipe;
                            }
                        }
                        // Handle single objects
                        else
                        {
                            var recipe = TryParseRecipeSchema(root, sourceUrl);
                            if (recipe != null) return recipe;
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogDebug("Failed to parse JSON-LD block: {Error}", ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error extracting JSON-LD: {Error}", ex.Message);
            }

            return null;
        }

        private Recipe? TryParseRecipeSchema(JsonElement element, string sourceUrl)
        {
            // Check if this is a Recipe type
            if (!element.TryGetProperty("@type", out var typeElement))
                return null;

            var type = typeElement.ValueKind == JsonValueKind.Array
                ? typeElement.EnumerateArray().Select(t => t.GetString()).FirstOrDefault(t => t == "Recipe")
                : typeElement.GetString();

            if (type != "Recipe")
                return null;

            var recipe = new Recipe
            {
                Title = GetStringProperty(element, "name") ?? "Untitled Recipe",
                Notes = GetStringProperty(element, "description"),
                OverallConfidence = 0.95m,
                ConfidenceNotes = $"Imported from URL: {sourceUrl}"
            };

            // Parse ingredients
            if (element.TryGetProperty("recipeIngredient", out var ingredients))
            {
                var ordinal = 0;
                foreach (var ing in ingredients.EnumerateArray())
                {
                    var rawText = ing.GetString();
                    if (!string.IsNullOrWhiteSpace(rawText))
                    {
                        var parsed = ParseIngredientText(rawText);
                        recipe.Ingredients.Add(new Ingredient
                        {
                            RawText = rawText,
                            Name = parsed.Name,
                            Quantity = parsed.Quantity,
                            Unit = parsed.Unit
                        });
                    }
                    ordinal++;
                }
            }

            // Parse instructions
            if (element.TryGetProperty("recipeInstructions", out var instructions))
            {
                var stepOrdinal = 1;
                foreach (var instruction in instructions.EnumerateArray())
                {
                    string? stepText = null;

                    if (instruction.ValueKind == JsonValueKind.String)
                    {
                        stepText = instruction.GetString();
                    }
                    else if (instruction.ValueKind == JsonValueKind.Object)
                    {
                        // HowToStep or HowToSection
                        stepText = GetStringProperty(instruction, "text") 
                            ?? GetStringProperty(instruction, "name");
                        
                        // Handle HowToSection with itemListElement
                        if (instruction.TryGetProperty("itemListElement", out var subSteps))
                        {
                            foreach (var subStep in subSteps.EnumerateArray())
                            {
                                var subText = subStep.ValueKind == JsonValueKind.String 
                                    ? subStep.GetString()
                                    : GetStringProperty(subStep, "text");
                                    
                                if (!string.IsNullOrWhiteSpace(subText))
                                {
                                    recipe.Steps.Add(new Step
                                    {
                                        Ordinal = stepOrdinal++,
                                        Text = CleanHtml(subText)
                                    });
                                }
                            }
                            continue;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(stepText))
                    {
                        recipe.Steps.Add(new Step
                        {
                            Ordinal = stepOrdinal++,
                            Text = CleanHtml(stepText)
                        });
                    }
                }
            }

            // Parse tags from keywords and recipeCategory
            var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            var keywords = GetStringProperty(element, "keywords");
            if (!string.IsNullOrEmpty(keywords))
            {
                foreach (var kw in keywords.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = kw.Trim();
                    if (!string.IsNullOrEmpty(trimmed) && trimmed.Length < 50)
                        tags.Add(trimmed);
                }
            }

            if (element.TryGetProperty("recipeCategory", out var categories))
            {
                if (categories.ValueKind == JsonValueKind.Array)
                {
                    foreach (var cat in categories.EnumerateArray())
                    {
                        var catStr = cat.GetString();
                        if (!string.IsNullOrEmpty(catStr))
                            tags.Add(catStr);
                    }
                }
                else if (categories.ValueKind == JsonValueKind.String)
                {
                    var catStr = categories.GetString();
                    if (!string.IsNullOrEmpty(catStr))
                        tags.Add(catStr);
                }
            }

            if (element.TryGetProperty("recipeCuisine", out var cuisines))
            {
                if (cuisines.ValueKind == JsonValueKind.Array)
                {
                    foreach (var cuisine in cuisines.EnumerateArray())
                    {
                        var cuisineStr = cuisine.GetString();
                        if (!string.IsNullOrEmpty(cuisineStr))
                            tags.Add(cuisineStr);
                    }
                }
                else if (cuisines.ValueKind == JsonValueKind.String)
                {
                    var cuisineStr = cuisines.GetString();
                    if (!string.IsNullOrEmpty(cuisineStr))
                        tags.Add(cuisineStr);
                }
            }

            recipe.Tags = tags.Take(10).ToList();

            // Only return if we have meaningful content
            if (recipe.Ingredients.Count == 0 && recipe.Steps.Count == 0)
                return null;

            return recipe;
        }

        private Recipe? TryExtractMicrodata(string html, string sourceUrl)
        {
            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // Look for microdata Recipe
                var recipeNode = doc.DocumentNode.SelectSingleNode("//*[@itemtype='http://schema.org/Recipe' or @itemtype='https://schema.org/Recipe']");
                if (recipeNode == null)
                    return null;

                var recipe = new Recipe
                {
                    Title = GetMicrodataProp(recipeNode, "name") ?? "Untitled Recipe",
                    Notes = GetMicrodataProp(recipeNode, "description"),
                    OverallConfidence = 0.90m,
                    ConfidenceNotes = $"Imported from URL (microdata): {sourceUrl}"
                };

                // Get ingredients
                var ingredientNodes = recipeNode.SelectNodes(".//*[@itemprop='recipeIngredient' or @itemprop='ingredients']");
                if (ingredientNodes != null)
                {
                    foreach (var node in ingredientNodes)
                    {
                        var rawText = CleanHtml(node.InnerText);
                        if (!string.IsNullOrWhiteSpace(rawText))
                        {
                            var parsed = ParseIngredientText(rawText);
                            recipe.Ingredients.Add(new Ingredient
                            {
                                RawText = rawText,
                                Name = parsed.Name,
                                Quantity = parsed.Quantity,
                                Unit = parsed.Unit
                            });
                        }
                    }
                }

                // Get instructions
                var instructionNodes = recipeNode.SelectNodes(".//*[@itemprop='recipeInstructions']");
                if (instructionNodes != null)
                {
                    var stepOrdinal = 1;
                    foreach (var node in instructionNodes)
                    {
                        var text = CleanHtml(node.InnerText);
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            recipe.Steps.Add(new Step
                            {
                                Ordinal = stepOrdinal++,
                                Text = text
                            });
                        }
                    }
                }

                if (recipe.Ingredients.Count == 0 && recipe.Steps.Count == 0)
                    return null;

                return recipe;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error extracting microdata: {Error}", ex.Message);
                return null;
            }
        }

        private Recipe? TryExtractFromHtml(string html, string sourceUrl)
        {
            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var recipe = new Recipe
                {
                    OverallConfidence = 0.70m,
                    ConfidenceNotes = $"Imported from URL (HTML parsing): {sourceUrl}"
                };

                // Try to get title from common patterns
                recipe.Title = GetTextFromSelectors(doc, new[]
                {
                    "//h1[contains(@class, 'recipe')]",
                    "//h1[contains(@class, 'title')]",
                    "//h1",
                    "//meta[@property='og:title']/@content",
                    "//title"
                }) ?? "Untitled Recipe";

                // Look for ingredient lists
                var ingredientNodes = doc.DocumentNode.SelectNodes(
                    "//*[contains(@class, 'ingredient')]//li | " +
                    "//ul[contains(@class, 'ingredient')]//li | " +
                    "//*[@id='ingredients']//li");

                if (ingredientNodes != null)
                {
                    foreach (var node in ingredientNodes)
                    {
                        var rawText = CleanHtml(node.InnerText);
                        if (!string.IsNullOrWhiteSpace(rawText) && rawText.Length < 500)
                        {
                            var parsed = ParseIngredientText(rawText);
                            recipe.Ingredients.Add(new Ingredient
                            {
                                RawText = rawText,
                                Name = parsed.Name,
                                Quantity = parsed.Quantity,
                                Unit = parsed.Unit
                            });
                        }
                    }
                }

                // Look for instruction lists
                var instructionNodes = doc.DocumentNode.SelectNodes(
                    "//*[contains(@class, 'instruction')]//li | " +
                    "//*[contains(@class, 'direction')]//li | " +
                    "//*[contains(@class, 'step')]//li | " +
                    "//ol[contains(@class, 'instruction')]//li | " +
                    "//ol[contains(@class, 'direction')]//li");

                if (instructionNodes != null)
                {
                    var stepOrdinal = 1;
                    foreach (var node in instructionNodes)
                    {
                        var text = CleanHtml(node.InnerText);
                        if (!string.IsNullOrWhiteSpace(text) && text.Length > 10)
                        {
                            recipe.Steps.Add(new Step
                            {
                                Ordinal = stepOrdinal++,
                                Text = text
                            });
                        }
                    }
                }

                return recipe;
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Error parsing HTML: {Error}", ex.Message);
                return null;
            }
        }

        #region Helper Methods

        private static string? GetStringProperty(JsonElement element, string propertyName)
        {
            if (element.TryGetProperty(propertyName, out var prop))
            {
                return prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
            }
            return null;
        }

        private static string? GetMicrodataProp(HtmlNode parent, string propName)
        {
            var node = parent.SelectSingleNode($".//*[@itemprop='{propName}']");
            if (node == null) return null;
            
            // Check for content attribute first (common for meta tags)
            var content = node.GetAttributeValue("content", null);
            if (!string.IsNullOrEmpty(content))
                return content;
                
            return CleanHtml(node.InnerText);
        }

        private static string? GetTextFromSelectors(HtmlDocument doc, string[] selectors)
        {
            foreach (var selector in selectors)
            {
                var node = doc.DocumentNode.SelectSingleNode(selector);
                if (node != null)
                {
                    var text = selector.Contains("@content") 
                        ? node.GetAttributeValue("content", null)
                        : CleanHtml(node.InnerText);
                        
                    if (!string.IsNullOrWhiteSpace(text))
                        return text.Trim();
                }
            }
            return null;
        }

        private static string CleanHtml(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            // Decode HTML entities
            text = System.Net.WebUtility.HtmlDecode(text);
            
            // Remove extra whitespace
            text = Regex.Replace(text, @"\s+", " ");
            
            return text.Trim();
        }

        private static (string? Name, string? Quantity, string? Unit) ParseIngredientText(string rawText)
        {
            // Simple ingredient parsing - try to extract quantity, unit, and name
            // Pattern: "2 1/2 cups all-purpose flour" -> quantity="2 1/2", unit="cups", name="all-purpose flour"
            
            var pattern = @"^([\d\s\/\.\-]+)?\s*(cup|cups|tbsp|tablespoon|tablespoons|tsp|teaspoon|teaspoons|oz|ounce|ounces|lb|lbs|pound|pounds|g|gram|grams|kg|ml|l|liter|liters|pinch|dash|can|cans|package|packages|bunch|clove|cloves|slice|slices|piece|pieces)s?\s+(.+)$";
            
            var match = Regex.Match(rawText.Trim(), pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return (
                    Name: match.Groups[3].Value.Trim(),
                    Quantity: match.Groups[1].Value.Trim(),
                    Unit: match.Groups[2].Value.ToLower()
                );
            }

            // Try simpler pattern: just number at start
            var simpleMatch = Regex.Match(rawText.Trim(), @"^([\d\s\/\.\-]+)\s+(.+)$");
            if (simpleMatch.Success)
            {
                return (
                    Name: simpleMatch.Groups[2].Value.Trim(),
                    Quantity: simpleMatch.Groups[1].Value.Trim(),
                    Unit: null
                );
            }

            // No pattern match - just use as name
            return (Name: rawText.Trim(), Quantity: null, Unit: null);
        }

        #endregion
    }
}
