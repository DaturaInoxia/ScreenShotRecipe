using System.Threading.Tasks;
using ScreenShotRecipe.Domain.Entities;

namespace ScreenShotRecipe.Domain.Interfaces
{
    public record OcrResult(string RawText, double Confidence);

    public interface IOcrClient
    {
        /// <summary>
        /// Runs OCR on provided image bytes and returns raw text and confidence metadata.
        /// </summary>
        Task<OcrResult> RecognizeAsync(byte[] imageBytes);
    }
}
