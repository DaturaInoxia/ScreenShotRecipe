using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenShotRecipe.Domain.Interfaces
{
    /// <summary>
    /// Multimodal OCR service for recipe image text extraction.
    /// Processes multiple images in a single batch operation.
    /// Implementation: GPT-4o, Azure Vision, or test/fake implementations.
    /// </summary>
    public interface IRecipeOcrService
    {
        /// <summary>
        /// Extracts text from multiple images in reading order via a single batch call.
        /// </summary>
        /// <param name="images">Collection of images to process (1-6 recommended)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Ordered OCR results with extracted text and confidence metadata</returns>
        /// <exception cref="InvalidOperationException">Thrown if OCR service is unavailable</exception>
        /// <exception cref="OperationCanceledException">Thrown if request is cancelled</exception>
        Task<IReadOnlyList<RecipeOcrResult>> ExtractTextAsync(
            IReadOnlyList<ImageData> images,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Image data packet for OCR processing.
    /// </summary>
    public record ImageData(
        byte[] Content,           // Image binary data
        string MediaType,         // MIME type: "image/jpeg", "image/png", etc.
        string FileName,          // Original filename for logging/traceability
        int PageOrder = 0         // Upload order (for multi-image batches)
    );

    /// <summary>
    /// OCR result for a single image.
    /// </summary>
    public record RecipeOcrResult(
        string ExtractedText,     // Raw text extracted from image
        string DetectedLanguage,  // ISO 639-1 language code (e.g., "en", "es")
        decimal Confidence,       // 0.0-1.0 average confidence score
        string ImageFileName,     // Reference to source image
        int PageOrder             // Original page order (preserved from input)
    );
}
