using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ScreenShotRecipe.Domain.Entities;

namespace ScreenShotRecipe.Domain.Interfaces
{
    /// <summary>
    /// Unified multimodal recipe extraction service.
    /// Processes multiple images in a single call and returns a structured recipe.
    /// Replaces separate OCR + LLM parser pipeline with single-step GPT-4o extraction.
    /// </summary>
    public interface IRecipeExtractionService
    {
        /// <summary>
        /// Extracts a structured recipe from one or more images using multimodal AI.
        /// </summary>
        /// <param name="images">Collection of recipe images to process (1-6 recommended)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Extraction result containing the parsed recipe and confidence metadata</returns>
        /// <exception cref="RecipeExtractionException">Thrown if extraction fails</exception>
        /// <exception cref="OperationCanceledException">Thrown if request is cancelled</exception>
        Task<RecipeExtractionResult> ExtractRecipeAsync(
            IReadOnlyList<RecipeImageInput> images,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Input image data for recipe extraction.
    /// </summary>
    public record RecipeImageInput(
        byte[] Content,           // Image binary data
        string MediaType,         // MIME type: "image/jpeg", "image/png", "image/webp", "image/gif"
        string FileName,          // Original filename for logging/traceability
        int PageOrder = 0         // Upload order for multi-page recipes
    );

    /// <summary>
    /// Result of recipe extraction from images.
    /// </summary>
    public record RecipeExtractionResult(
        Recipe Recipe,            // The extracted recipe entity
        decimal Confidence,       // 0.0-1.0 confidence score for the extraction
        string? RawResponse,      // Raw AI response (for debugging/logging)
        int ImagesProcessed,      // Number of images that were processed
        TimeSpan ProcessingTime   // Time taken for extraction
    );

    /// <summary>
    /// Exception thrown when recipe extraction fails.
    /// </summary>
    public class RecipeExtractionException : Exception
    {
        public string? RawResponse { get; }
        public int ImagesAttempted { get; }

        public RecipeExtractionException(string message, int imagesAttempted, string? rawResponse = null)
            : base(message)
        {
            ImagesAttempted = imagesAttempted;
            RawResponse = rawResponse;
        }

        public RecipeExtractionException(string message, int imagesAttempted, Exception innerException, string? rawResponse = null)
            : base(message, innerException)
        {
            ImagesAttempted = imagesAttempted;
            RawResponse = rawResponse;
        }
    }
}
