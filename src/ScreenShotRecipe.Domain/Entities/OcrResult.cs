using System;

namespace ScreenShotRecipe.Domain.Entities
{
    /// <summary>
    /// Stores OCR extraction result for a single image.
    /// </summary>
    public class OcrResult
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        /// <summary>
        /// Foreign key to the ImageAsset this result belongs to.
        /// </summary>
        public Guid ImageAssetId { get; set; }
        
        /// <summary>
        /// Raw text extracted from the image.
        /// </summary>
        public string ExtractedText { get; set; } = string.Empty;
        
        /// <summary>
        /// Detected language (ISO 639-1 code: "en", "es", "fr", etc.).
        /// </summary>
        public string DetectedLanguage { get; set; } = "en";
        
        /// <summary>
        /// Average confidence score (0.0-1.0).
        /// >= 0.9: High confidence
        /// 0.7-0.9: Medium confidence
        /// < 0.7: Low confidence (flag for review)
        /// </summary>
        public decimal Confidence { get; set; }
        
        /// <summary>
        /// JSON containing per-line confidence details if available.
        /// </summary>
        public string? ConfidenceDetails { get; set; }
        
        /// <summary>
        /// When OCR processing completed.
        /// </summary>
        public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// OCR engine identifier for versioning (e.g., "gpt-4o-2024-11-20").
        /// </summary>
        public string OcrEngine { get; set; } = string.Empty;
        
        // Navigation property
        public ImageAsset? ImageAsset { get; set; }
    }
}
