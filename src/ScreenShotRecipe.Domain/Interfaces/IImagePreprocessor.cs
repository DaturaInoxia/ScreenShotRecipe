using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenShotRecipe.Domain.Interfaces
{
    /// <summary>
    /// Pre-processes images before OCR to optimize quality and size.
    /// </summary>
    public interface IImagePreprocessor
    {
        /// <summary>
        /// Preprocess an image for OCR by resizing, compressing, and converting format if needed.
        /// </summary>
        Task<ImagePreprocessResult> PreprocessAsync(
            Stream inputStream,
            string originalFileName,
            ImagePreprocessOptions? options = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Preprocess an image from bytes.
        /// </summary>
        Task<ImagePreprocessResult> PreprocessAsync(
            byte[] imageData,
            string originalFileName,
            ImagePreprocessOptions? options = null,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Options for image preprocessing.
    /// </summary>
    public class ImagePreprocessOptions
    {
        /// <summary>
        /// Maximum width in pixels. Images wider than this will be resized.
        /// Default: 2000px (balances OCR quality with API limits).
        /// </summary>
        public int MaxWidth { get; set; } = 2000;

        /// <summary>
        /// Maximum height in pixels. Images taller than this will be resized.
        /// Default: 2000px.
        /// </summary>
        public int MaxHeight { get; set; } = 2000;

        /// <summary>
        /// Maximum file size in bytes. Images larger will be compressed.
        /// Default: 4MB (conservative limit for GPT-4o).
        /// </summary>
        public long MaxFileSizeBytes { get; set; } = 4 * 1024 * 1024;

        /// <summary>
        /// JPEG quality when compressing (1-100).
        /// Default: 85 (good balance of quality vs size).
        /// </summary>
        public int JpegQuality { get; set; } = 85;

        /// <summary>
        /// Output format preference.
        /// Default: PreferPng (lossless, but will use JPEG if size reduction needed).
        /// </summary>
        public ImageOutputFormat OutputFormat { get; set; } = ImageOutputFormat.PreferPng;

        /// <summary>
        /// Whether to convert to grayscale (can improve OCR for some documents).
        /// Default: false.
        /// </summary>
        public bool ConvertToGrayscale { get; set; } = false;

        /// <summary>
        /// Whether to apply contrast enhancement.
        /// Default: false.
        /// </summary>
        public bool EnhanceContrast { get; set; } = false;
    }

    /// <summary>
    /// Output format preference for preprocessing.
    /// </summary>
    public enum ImageOutputFormat
    {
        /// <summary>PNG preferred but will use JPEG if size reduction needed.</summary>
        PreferPng,
        /// <summary>Always output JPEG.</summary>
        ForceJpeg,
        /// <summary>Always output PNG.</summary>
        ForcePng,
        /// <summary>Use WebP for smaller file sizes.</summary>
        WebP,
        /// <summary>Keep original format.</summary>
        KeepOriginal
    }

    /// <summary>
    /// Result of image preprocessing.
    /// </summary>
    public class ImagePreprocessResult
    {
        public required byte[] ProcessedImageData { get; init; }
        public required string MediaType { get; init; }
        public required string SuggestedFileName { get; init; }
        public int OriginalWidth { get; init; }
        public int OriginalHeight { get; init; }
        public int ProcessedWidth { get; init; }
        public int ProcessedHeight { get; init; }
        public long OriginalSizeBytes { get; init; }
        public long ProcessedSizeBytes { get; init; }
        public bool WasResized { get; init; }
        public bool WasCompressed { get; init; }
        public bool WasFormatConverted { get; init; }
    }
}
