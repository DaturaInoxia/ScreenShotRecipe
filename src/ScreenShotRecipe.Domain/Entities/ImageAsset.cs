using System;

namespace ScreenShotRecipe.Domain.Entities
{
    /// <summary>
    /// Represents an image uploaded as part of a recipe import job.
    /// Stores reference to the original file and links to OCR results.
    /// </summary>
    public class ImageAsset
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        /// <summary>
        /// Foreign key to the ImportJob this image belongs to.
        /// </summary>
        public Guid ImportJobId { get; set; }
        
        /// <summary>
        /// Original filename from upload.
        /// </summary>
        public string FileName { get; set; } = string.Empty;
        
        /// <summary>
        /// Storage path: /storage/images/{guid}/{filename}
        /// </summary>
        public string FilePath { get; set; } = string.Empty;
        
        /// <summary>
        /// MIME type: "image/jpeg", "image/png", etc.
        /// </summary>
        public string MediaType { get; set; } = string.Empty;
        
        /// <summary>
        /// File size in bytes.
        /// </summary>
        public long FileSizeBytes { get; set; }
        
        /// <summary>
        /// Upload order (0-based) for OCR reading sequence.
        /// </summary>
        public int UploadOrder { get; set; }
        
        /// <summary>
        /// When the image was stored.
        /// </summary>
        public DateTime StoredAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Storage provider key: "local_fs" or "s3"
        /// </summary>
        public string StorageProviderKey { get; set; } = "local_fs";
        
        /// <summary>
        /// Foreign key to OCR result (if processing completed).
        /// </summary>
        public Guid? OcrResultId { get; set; }
        
        // Navigation properties
        public ImportJob? ImportJob { get; set; }
        public OcrResult? OcrResult { get; set; }
    }
}
