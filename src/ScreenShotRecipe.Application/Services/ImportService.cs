using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ScreenShotRecipe.Application.Contracts.Dtos;
using ScreenShotRecipe.Domain.Entities;
using ScreenShotRecipe.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ScreenShotRecipe.Application.Services
{
    /// <summary>
    /// Service responsible for importing recipes from multiple images or URLs.
    /// Creates ImportJob, processes images, invokes extraction, and persists results.
    /// </summary>
    public class ImportService
    {
        private readonly IRecipeExtractionService _extractionService;
        private readonly IUrlRecipeExtractor? _urlExtractor;
        private readonly IStorage _storage;
        private readonly IRecipeRepository _recipeRepo;
        private readonly IImageAssetRepository _imageAssetRepo;
        private readonly IImportJobRepository _importJobRepo;
        private readonly IImagePreprocessor _imagePreprocessor;
        private readonly ILogger<ImportService> _logger;

        // Retry settings for transient failures
        private const int MaxRetryAttempts = 3;
        private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);

        public ImportService(
            IRecipeExtractionService extractionService,
            IStorage storage,
            IRecipeRepository recipeRepo,
            IImageAssetRepository imageAssetRepo,
            IImportJobRepository importJobRepo,
            IImagePreprocessor imagePreprocessor,
            ILogger<ImportService> logger,
            IUrlRecipeExtractor? urlExtractor = null)
        {
            _extractionService = extractionService;
            _urlExtractor = urlExtractor;
            _storage = storage;
            _recipeRepo = recipeRepo;
            _imageAssetRepo = imageAssetRepo;
            _importJobRepo = importJobRepo;
            _imagePreprocessor = imagePreprocessor;
            _logger = logger;
        }

        /// <summary>
        /// Import recipe from multiple images.
        /// Creates ImportJob, processes images, extracts recipe, and returns response.
        /// </summary>
        public async Task<RecipeImportResponseDto> ImportImagesAsync(
            RecipeImportRequestDto request,
            CancellationToken cancellationToken = default)
        {
            // Check idempotency - return existing job if found
            if (!string.IsNullOrEmpty(request.IdempotencyKey))
            {
                var existingJob = await _importJobRepo.GetByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken);
                if (existingJob != null)
                {
                    _logger.LogInformation("Found existing ImportJob {JobId} for idempotency key {Key}", 
                        existingJob.Id, request.IdempotencyKey);
                    return MapToResponse(existingJob);
                }
            }

            // Create import job in Queued state
            Guid? userIdGuid = null;
            if (!string.IsNullOrEmpty(request.Metadata?.UserId) && Guid.TryParse(request.Metadata.UserId, out var parsed))
            {
                userIdGuid = parsed;
            }
            
            var importJob = new ImportJob
            {
                Id = Guid.NewGuid(),
                UserId = userIdGuid,
                Status = ImportJobStatus.Queued,
                CreatedAt = DateTime.UtcNow,
                Source = request.Metadata?.Source ?? "web-upload",
                IdempotencyKey = request.IdempotencyKey,
                Version = 1
            };

            await _importJobRepo.AddAsync(importJob, cancellationToken);
            _logger.LogInformation("Created ImportJob {JobId} with {ImageCount} image(s)", 
                importJob.Id, request.Images.Count);

            try
            {
                // Process and store images
                var imageAssets = await ProcessAndStoreImagesAsync(importJob.Id, request.Images, cancellationToken);
                
                // Update status to OcrInProgress
                await _importJobRepo.UpdateStatusAsync(importJob.Id, ImportJobStatus.OcrInProgress, cancellationToken: cancellationToken);

                // Prepare images for extraction
                var extractionImages = imageAssets.Select(asset => new RecipeImageInput(
                    Content: asset.preprocessedData,
                    MediaType: asset.asset.MediaType,
                    FileName: asset.asset.FileName,
                    PageOrder: asset.asset.UploadOrder
                )).ToList();

                // Extract recipe with retry for transient failures
                RecipeExtractionResult extractionResult;
                try
                {
                    extractionResult = await ExecuteWithRetryAsync(
                        () => _extractionService.ExtractRecipeAsync(extractionImages),
                        "recipe extraction",
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    // Permanent failure - update job and return error response
                    var diagnostics = $"Extraction failed after {MaxRetryAttempts} attempts. Last error: {ex.Message}";
                    await _importJobRepo.UpdateStatusAsync(
                        importJob.Id, 
                        ImportJobStatus.Failed, 
                        ex.Message, 
                        diagnostics, 
                        cancellationToken);
                    
                    return new RecipeImportResponseDto
                    {
                        ImportJobId = importJob.Id,
                        Status = ImportJobStatus.Failed.ToString(),
                        ErrorMessage = ex.Message,
                        Diagnostics = diagnostics,
                        ImageIds = imageAssets.Select(a => a.asset.Id).ToList()
                    };
                }

                // Update status to ParsingInProgress (extraction includes parsing in multimodal)
                await _importJobRepo.UpdateStatusAsync(importJob.Id, ImportJobStatus.ParsingInProgress, cancellationToken: cancellationToken);

                _logger.LogInformation(
                    "Recipe extraction completed. Title: {Title}, Confidence: {Confidence:P0}, " +
                    "Ingredients: {IngredientCount}, Steps: {StepCount}, Duration: {Duration}ms",
                    extractionResult.Recipe.Title,
                    extractionResult.Confidence,
                    extractionResult.Recipe.Ingredients.Count,
                    extractionResult.Recipe.Steps.Count,
                    extractionResult.ProcessingTime.TotalMilliseconds);

                // Set confidence metadata on recipe
                var recipe = extractionResult.Recipe;
                recipe.ImportJobId = importJob.Id;
                recipe.OverallConfidence = extractionResult.Confidence;
                recipe.ConfidenceNotes = BuildConfidenceNotes(extractionResult);
                recipe.Version = 1;

                // Persist recipe
                await _recipeRepo.AddAsync(recipe);
                _logger.LogInformation("Recipe persisted. Recipe ID: {RecipeId}, Title: {Title}", 
                    recipe.Id, recipe.Title);

                // Update ImportJob with recipe reference and mark as succeeded
                importJob.RecipeId = recipe.Id;
                importJob.Status = ImportJobStatus.Succeeded;
                importJob.CompletedAt = DateTime.UtcNow;
                importJob.Diagnostics = $"Processed {imageAssets.Count} images in {extractionResult.ProcessingTime.TotalMilliseconds:F0}ms";
                await _importJobRepo.UpdateAsync(importJob, cancellationToken);

                // Build response
                return new RecipeImportResponseDto
                {
                    ImportJobId = importJob.Id,
                    Status = ImportJobStatus.Succeeded.ToString(),
                    Recipe = MapToRecipeDto(recipe),
                    OcrResults = null, // Multimodal extraction doesn't provide per-image OCR results
                    ImageIds = imageAssets.Select(a => a.asset.Id).ToList(),
                    Diagnostics = importJob.Diagnostics
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Import job {JobId} failed unexpectedly", importJob.Id);
                
                await _importJobRepo.UpdateStatusAsync(
                    importJob.Id, 
                    ImportJobStatus.Failed, 
                    ex.Message,
                    $"Unexpected error: {ex.GetType().Name}",
                    cancellationToken);

                return new RecipeImportResponseDto
                {
                    ImportJobId = importJob.Id,
                    Status = ImportJobStatus.Failed.ToString(),
                    ErrorMessage = ex.Message,
                    Diagnostics = $"Unexpected error: {ex.GetType().Name}: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Legacy method for backward compatibility.
        /// </summary>
        public async Task<RecipeDto> ImportImagesAsync(List<(byte[] Bytes, string FileName)> images)
        {
            var request = new RecipeImportRequestDto
            {
                Images = images.Select((img, index) => new RecipeImageFileDto
                {
                    Content = img.Bytes,
                    FileName = img.FileName,
                    PageOrder = index
                }).ToList()
            };

            var response = await ImportImagesAsync(request);

            if (response.Recipe == null)
            {
                throw new InvalidOperationException($"Import failed: {response.ErrorMessage}");
            }

            return response.Recipe;
        }

        /// <summary>
        /// Get the status of an import job.
        /// </summary>
        public async Task<RecipeImportResponseDto?> GetImportJobStatusAsync(Guid importJobId, CancellationToken cancellationToken = default)
        {
            var job = await _importJobRepo.GetByIdAsync(importJobId, cancellationToken);
            if (job == null) return null;

            return MapToResponse(job);
        }

        private async Task<List<(ImageAsset asset, byte[] preprocessedData)>> ProcessAndStoreImagesAsync(
            Guid importJobId,
            List<RecipeImageFileDto> images,
            CancellationToken cancellationToken)
        {
            var results = new List<(ImageAsset asset, byte[] preprocessedData)>();

            foreach (var image in images.OrderBy(i => i.PageOrder))
            {
                try
                {
                    // Preprocess image (resize, compress, format)
                    var preprocessResult = await _imagePreprocessor.PreprocessAsync(
                        image.Content, 
                        image.FileName, 
                        cancellationToken: cancellationToken);

                    _logger.LogDebug("Preprocessed {FileName}: {OrigW}x{OrigH} -> {NewW}x{NewH}, size {OrigSize}B -> {NewSize}B",
                        image.FileName,
                        preprocessResult.OriginalWidth, preprocessResult.OriginalHeight,
                        preprocessResult.ProcessedWidth, preprocessResult.ProcessedHeight,
                        preprocessResult.OriginalSizeBytes, preprocessResult.ProcessedSizeBytes);

                    // Save to storage
                    var storagePath = await _storage.SaveImageAsync(
                        preprocessResult.ProcessedImageData, 
                        preprocessResult.SuggestedFileName);

                    // Create ImageAsset record
                    var asset = new ImageAsset
                    {
                        Id = Guid.NewGuid(),
                        ImportJobId = importJobId,
                        FileName = image.FileName,
                        FilePath = storagePath,
                        MediaType = preprocessResult.MediaType,
                        FileSizeBytes = preprocessResult.ProcessedSizeBytes,
                        UploadOrder = image.PageOrder,
                        StoredAt = DateTime.UtcNow,
                        StorageProviderKey = "local-filesystem"
                    };

                    await _imageAssetRepo.AddAsync(asset, cancellationToken);
                    results.Add((asset, preprocessResult.ProcessedImageData));

                    _logger.LogInformation("Stored image asset {AssetId}: {FileName} at {Path}",
                        asset.Id, asset.FileName, storagePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process image {FileName} - continuing with remaining images", image.FileName);
                    // Continue with other images instead of failing the whole import
                }
            }

            if (results.Count == 0)
            {
                throw new InvalidOperationException("No images could be processed successfully");
            }

            return results;
        }

        private async Task<T> ExecuteWithRetryAsync<T>(
            Func<Task<T>> operation,
            string operationName,
            CancellationToken cancellationToken)
        {
            var delay = InitialRetryDelay;
            Exception? lastException = null;

            for (int attempt = 1; attempt <= MaxRetryAttempts; attempt++)
            {
                try
                {
                    return await operation();
                }
                catch (RecipeExtractionException ex) when (IsTransientExtractionException(ex) && attempt < MaxRetryAttempts)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "Transient error during {Operation}, attempt {Attempt}/{MaxAttempts}. Retrying in {Delay}s",
                        operationName, attempt, MaxRetryAttempts, delay.TotalSeconds);
                    
                    await Task.Delay(delay, cancellationToken);
                    delay = TimeSpan.FromTicks(delay.Ticks * 2); // Exponential backoff
                }
                catch (Exception ex) when (IsTransientException(ex) && attempt < MaxRetryAttempts)
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "Possible transient error during {Operation}, attempt {Attempt}/{MaxAttempts}. Retrying in {Delay}s",
                        operationName, attempt, MaxRetryAttempts, delay.TotalSeconds);
                    
                    await Task.Delay(delay, cancellationToken);
                    delay = TimeSpan.FromTicks(delay.Ticks * 2);
                }
                catch (Exception)
                {
                    throw; // Non-transient error, don't retry
                }
            }

            throw lastException ?? new InvalidOperationException($"{operationName} failed after {MaxRetryAttempts} attempts");
        }

        private static bool IsTransientException(Exception ex)
        {
            // Common transient error patterns
            return ex is TimeoutException
                || ex is TaskCanceledException
                || ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("429", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("503", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("temporarily unavailable", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTransientExtractionException(RecipeExtractionException ex)
        {
            // Check if the extraction exception represents a transient failure
            return IsTransientException(ex) || 
                   ex.InnerException != null && IsTransientException(ex.InnerException);
        }

        private static string? BuildConfidenceNotes(RecipeExtractionResult result)
        {
            var notes = new List<string>();

            if (result.Confidence < 0.5m)
            {
                notes.Add("Low confidence - manual review recommended");
            }
            else if (result.Confidence < 0.8m)
            {
                notes.Add("Moderate confidence - verify key fields");
            }

            // Note: Multimodal extraction doesn't provide per-image confidence metrics
            
            return notes.Count > 0 ? string.Join("; ", notes) : null;
        }

        private RecipeImportResponseDto MapToResponse(ImportJob job)
        {
            return new RecipeImportResponseDto
            {
                ImportJobId = job.Id,
                Status = job.Status.ToString(),
                Recipe = job.Recipe != null ? MapToRecipeDto(job.Recipe) : null,
                ErrorMessage = job.ErrorMessage,
                Diagnostics = job.Diagnostics,
                ImageIds = job.ImageAssets.Select(a => a.Id).ToList()
            };
        }

        private static RecipeDto MapToRecipeDto(Recipe recipe)
        {
            return new RecipeDto
            {
                Id = recipe.Id,
                Title = recipe.Title,
                Notes = recipe.Notes,
                Ingredients = recipe.Ingredients.Select(i => new IngredientDto
                {
                    RawText = i.RawText,
                    Name = i.Name,
                    Quantity = i.Quantity,
                    Unit = i.Unit
                }).ToList(),
                Steps = recipe.Steps.Select(s => new StepDto
                {
                    Ordinal = s.Ordinal,
                    Text = s.Text
                }).ToList(),
                Tags = recipe.Tags
            };
        }

        /// <summary>
        /// Import recipe from a URL.
        /// Parses structured data (JSON-LD) or HTML content from the page.
        /// </summary>
        public async Task<RecipeImportResponseDto> ImportFromUrlAsync(
            string url,
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            if (_urlExtractor == null)
            {
                throw new InvalidOperationException("URL extraction is not configured");
            }

            // Check idempotency - return existing job if found
            if (!string.IsNullOrEmpty(idempotencyKey))
            {
                var existingJob = await _importJobRepo.GetByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
                if (existingJob != null)
                {
                    _logger.LogInformation("Found existing ImportJob {JobId} for idempotency key {Key}", 
                        existingJob.Id, idempotencyKey);
                    return MapToResponse(existingJob);
                }
            }

            // Create import job in Queued state
            var importJob = new ImportJob
            {
                Id = Guid.NewGuid(),
                Status = ImportJobStatus.Queued,
                CreatedAt = DateTime.UtcNow,
                Source = "url-import",
                IdempotencyKey = idempotencyKey,
                Version = 1
            };

            await _importJobRepo.AddAsync(importJob, cancellationToken);
            _logger.LogInformation("Created ImportJob {JobId} for URL import: {Url}", importJob.Id, url);

            try
            {
                // Update status to OcrInProgress (parsing the URL)
                await _importJobRepo.UpdateStatusAsync(importJob.Id, ImportJobStatus.OcrInProgress, cancellationToken: cancellationToken);

                // Extract recipe from URL
                RecipeExtractionResult extractionResult;
                try
                {
                    extractionResult = await _urlExtractor.ExtractFromUrlAsync(url, cancellationToken);
                }
                catch (RecipeExtractionException ex)
                {
                    _logger.LogWarning(ex, "Failed to extract recipe from URL: {Url}", url);
                    
                    await _importJobRepo.UpdateStatusAsync(
                        importJob.Id, 
                        ImportJobStatus.Failed, 
                        ex.Message,
                        $"URL extraction failed: {ex.Message}",
                        cancellationToken);

                    return new RecipeImportResponseDto
                    {
                        ImportJobId = importJob.Id,
                        Status = ImportJobStatus.Failed.ToString(),
                        ErrorMessage = ex.Message,
                        Diagnostics = $"Failed to extract recipe from URL"
                    };
                }

                // Update status to ParsingInProgress
                await _importJobRepo.UpdateStatusAsync(importJob.Id, ImportJobStatus.ParsingInProgress, cancellationToken: cancellationToken);

                _logger.LogInformation(
                    "URL recipe extraction completed. Title: {Title}, Confidence: {Confidence:P0}, " +
                    "Ingredients: {IngredientCount}, Steps: {StepCount}, Duration: {Duration}ms",
                    extractionResult.Recipe.Title,
                    extractionResult.Confidence,
                    extractionResult.Recipe.Ingredients.Count,
                    extractionResult.Recipe.Steps.Count,
                    extractionResult.ProcessingTime.TotalMilliseconds);

                // Set confidence metadata on recipe
                var recipe = extractionResult.Recipe;
                recipe.ImportJobId = importJob.Id;
                recipe.OverallConfidence = extractionResult.Confidence;
                recipe.ConfidenceNotes ??= BuildConfidenceNotes(extractionResult);
                recipe.Version = 1;

                // Persist recipe
                await _recipeRepo.AddAsync(recipe);
                _logger.LogInformation("Recipe persisted. Recipe ID: {RecipeId}, Title: {Title}", 
                    recipe.Id, recipe.Title);

                // Update ImportJob with recipe reference and mark as succeeded
                importJob.RecipeId = recipe.Id;
                importJob.Status = ImportJobStatus.Succeeded;
                importJob.CompletedAt = DateTime.UtcNow;
                importJob.Diagnostics = $"Imported from URL in {extractionResult.ProcessingTime.TotalMilliseconds:F0}ms";
                await _importJobRepo.UpdateAsync(importJob, cancellationToken);

                // Build response
                return new RecipeImportResponseDto
                {
                    ImportJobId = importJob.Id,
                    Status = ImportJobStatus.Succeeded.ToString(),
                    Recipe = MapToRecipeDto(recipe),
                    Diagnostics = importJob.Diagnostics
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Import job {JobId} failed unexpectedly for URL: {Url}", importJob.Id, url);
                
                await _importJobRepo.UpdateStatusAsync(
                    importJob.Id, 
                    ImportJobStatus.Failed, 
                    ex.Message,
                    $"Unexpected error: {ex.GetType().Name}",
                    cancellationToken);

                return new RecipeImportResponseDto
                {
                    ImportJobId = importJob.Id,
                    Status = ImportJobStatus.Failed.ToString(),
                    ErrorMessage = ex.Message,
                    Diagnostics = $"Unexpected error: {ex.GetType().Name}: {ex.Message}"
                };
            }
        }
    }
}
