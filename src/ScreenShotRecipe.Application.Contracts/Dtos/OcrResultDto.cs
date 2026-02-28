using System;

namespace ScreenShotRecipe.Application.Contracts.Dtos
{
    /// <summary>
    /// DTO representing OCR extraction result for a single image.
    /// Exposed for API responses and logging.
    /// </summary>
    public class OcrResultDto
    {
        /// <summary>
        /// Raw text extracted from the image.
        /// </summary>
        public required string ExtractedText { get; set; }
        
        /// <summary>
        /// Detected language (ISO 639-1 code: "en", "es", "fr", etc.).
        /// </summary>
        public required string DetectedLanguage { get; set; }
        
        /// <summary>
        /// Confidence score (0.0-1.0).
        /// >= 0.9: High confidence
        /// 0.7-0.9: Medium confidence
        /// &lt; 0.7: Low confidence (flag for review)
        /// </summary>
        public decimal Confidence { get; set; }
        
        /// <summary>
        /// Original filename of the image (for reference).
        /// </summary>
        public required string ImageFileName { get; set; }
        
        /// <summary>
        /// Page order in the original batch (0-based index).
        /// </summary>
        public int PageOrder { get; set; }
    }
}
