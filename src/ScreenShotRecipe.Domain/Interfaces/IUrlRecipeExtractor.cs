using System.Threading;
using System.Threading.Tasks;

namespace ScreenShotRecipe.Domain.Interfaces
{
    /// <summary>
    /// Service for extracting recipes from URLs.
    /// Supports both structured data (JSON-LD Schema.org) and HTML parsing.
    /// </summary>
    public interface IUrlRecipeExtractor
    {
        /// <summary>
        /// Extracts a recipe from a given URL.
        /// First attempts to parse JSON-LD structured data, then falls back to HTML parsing.
        /// </summary>
        /// <param name="url">The URL of the recipe page</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Extraction result containing the parsed recipe and confidence metadata</returns>
        /// <exception cref="RecipeExtractionException">Thrown if extraction fails</exception>
        Task<RecipeExtractionResult> ExtractFromUrlAsync(
            string url,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Checks if a URL is valid and potentially contains a recipe.
        /// </summary>
        /// <param name="url">The URL to validate</param>
        /// <returns>True if the URL is valid and accessible</returns>
        bool IsValidUrl(string url);
    }
}
