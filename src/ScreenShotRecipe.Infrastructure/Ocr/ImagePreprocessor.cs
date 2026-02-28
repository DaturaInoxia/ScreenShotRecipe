using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;
using ScreenShotRecipe.Domain.Interfaces;

namespace ScreenShotRecipe.Infrastructure.Ocr
{
    /// <summary>
    /// ImageSharp-based implementation of image preprocessing for OCR.
    /// </summary>
    public class ImagePreprocessor : IImagePreprocessor
    {
        private readonly ILogger<ImagePreprocessor> _logger;

        public ImagePreprocessor(ILogger<ImagePreprocessor> logger)
        {
            _logger = logger;
        }

        public async Task<ImagePreprocessResult> PreprocessAsync(
            Stream inputStream,
            string originalFileName,
            ImagePreprocessOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            using var ms = new MemoryStream();
            await inputStream.CopyToAsync(ms, cancellationToken);
            return await PreprocessAsync(ms.ToArray(), originalFileName, options, cancellationToken);
        }

        public async Task<ImagePreprocessResult> PreprocessAsync(
            byte[] imageData,
            string originalFileName,
            ImagePreprocessOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            options ??= new ImagePreprocessOptions();
            
            _logger.LogDebug("Preprocessing image {FileName}, size: {Size} bytes", 
                originalFileName, imageData.Length);

            using var image = Image.Load(imageData);
            
            var originalWidth = image.Width;
            var originalHeight = image.Height;
            var originalSize = imageData.Length;
            
            bool wasResized = false;
            bool wasCompressed = false;
            bool wasFormatConverted = false;

            // Apply grayscale if requested
            if (options.ConvertToGrayscale)
            {
                image.Mutate(x => x.Grayscale());
            }

            // Apply contrast enhancement if requested
            if (options.EnhanceContrast)
            {
                image.Mutate(x => x.Contrast(1.2f));
            }

            // Resize if needed
            if (image.Width > options.MaxWidth || image.Height > options.MaxHeight)
            {
                var ratioX = (double)options.MaxWidth / image.Width;
                var ratioY = (double)options.MaxHeight / image.Height;
                var ratio = Math.Min(ratioX, ratioY);
                
                var newWidth = (int)(image.Width * ratio);
                var newHeight = (int)(image.Height * ratio);
                
                _logger.LogDebug("Resizing image from {OldW}x{OldH} to {NewW}x{NewH}", 
                    image.Width, image.Height, newWidth, newHeight);
                
                image.Mutate(x => x.Resize(newWidth, newHeight));
                wasResized = true;
            }

            // Determine output format
            var originalExtension = Path.GetExtension(originalFileName).ToLowerInvariant();
            var outputFormat = DetermineOutputFormat(options.OutputFormat, originalExtension);
            
            if (originalExtension != outputFormat.extension)
            {
                wasFormatConverted = true;
            }

            // Encode to target format
            byte[] processedData;
            using (var outputStream = new MemoryStream())
            {
                switch (outputFormat.format)
                {
                    case "jpeg":
                        await image.SaveAsJpegAsync(outputStream, new JpegEncoder 
                        { 
                            Quality = options.JpegQuality 
                        }, cancellationToken);
                        break;
                    case "webp":
                        await image.SaveAsWebpAsync(outputStream, new WebpEncoder
                        {
                            Quality = options.JpegQuality
                        }, cancellationToken);
                        break;
                    case "png":
                    default:
                        await image.SaveAsPngAsync(outputStream, new PngEncoder
                        {
                            CompressionLevel = PngCompressionLevel.BestCompression
                        }, cancellationToken);
                        break;
                }
                processedData = outputStream.ToArray();
            }

            // If still too large, compress using JPEG
            if (processedData.Length > options.MaxFileSizeBytes && outputFormat.format != "jpeg")
            {
                _logger.LogDebug("Image still too large ({Size} bytes), converting to JPEG", processedData.Length);
                
                using var outputStream = new MemoryStream();
                var quality = options.JpegQuality;
                
                // Reduce quality iteratively if needed
                while (quality >= 50)
                {
                    outputStream.SetLength(0);
                    await image.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = quality }, cancellationToken);
                    
                    if (outputStream.Length <= options.MaxFileSizeBytes)
                        break;
                    
                    quality -= 10;
                }
                
                processedData = outputStream.ToArray();
                outputFormat = ("jpeg", ".jpg", "image/jpeg");
                wasCompressed = true;
                wasFormatConverted = true;
            }

            var suggestedFileName = Path.GetFileNameWithoutExtension(originalFileName) + outputFormat.extension;

            var result = new ImagePreprocessResult
            {
                ProcessedImageData = processedData,
                MediaType = outputFormat.mediaType,
                SuggestedFileName = suggestedFileName,
                OriginalWidth = originalWidth,
                OriginalHeight = originalHeight,
                ProcessedWidth = image.Width,
                ProcessedHeight = image.Height,
                OriginalSizeBytes = originalSize,
                ProcessedSizeBytes = processedData.Length,
                WasResized = wasResized,
                WasCompressed = wasCompressed,
                WasFormatConverted = wasFormatConverted
            };

            _logger.LogInformation(
                "Preprocessed {FileName}: {OrigW}x{OrigH} ({OrigSize}B) -> {NewW}x{NewH} ({NewSize}B), resized={Resized}, compressed={Compressed}",
                originalFileName, originalWidth, originalHeight, originalSize,
                result.ProcessedWidth, result.ProcessedHeight, result.ProcessedSizeBytes,
                wasResized, wasCompressed);

            return result;
        }

        private static (string format, string extension, string mediaType) DetermineOutputFormat(
            ImageOutputFormat preference, 
            string originalExtension)
        {
            return preference switch
            {
                ImageOutputFormat.ForceJpeg => ("jpeg", ".jpg", "image/jpeg"),
                ImageOutputFormat.ForcePng => ("png", ".png", "image/png"),
                ImageOutputFormat.WebP => ("webp", ".webp", "image/webp"),
                ImageOutputFormat.KeepOriginal => originalExtension switch
                {
                    ".jpg" or ".jpeg" => ("jpeg", ".jpg", "image/jpeg"),
                    ".webp" => ("webp", ".webp", "image/webp"),
                    _ => ("png", ".png", "image/png")
                },
                ImageOutputFormat.PreferPng or _ => ("png", ".png", "image/png")
            };
        }
    }
}
