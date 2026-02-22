using System.Threading.Tasks;
using ScreenShotRecipe.Domain.Interfaces;

namespace ScreenShotRecipe.Infrastructure.Ocr
{
    // NOTE: Minimal stub implementation. Replace with Azure Cognitive Services SDK integration.
    public class AzureVisionOcrClient : IOcrClient
    {
        public Task<OcrResult> RecognizeAsync(byte[] imageBytes)
        {
            // For now return a placeholder. Integration required.
            return Task.FromResult(new OcrResult("", 1.0));
        }
    }
}
