using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ScreenShotRecipe.Domain.Entities;

namespace ScreenShotRecipe.Domain.Interfaces
{
    /// <summary>
    /// Repository interface for ImageAsset entities.
    /// </summary>
    public interface IImageAssetRepository
    {
        /// <summary>
        /// Adds a new image asset to the database.
        /// </summary>
        Task<ImageAsset> AddAsync(ImageAsset imageAsset, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets an image asset by ID.
        /// </summary>
        Task<ImageAsset?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets all image assets for an import job, ordered by upload order.
        /// </summary>
        Task<IReadOnlyList<ImageAsset>> GetByImportJobIdAsync(Guid importJobId, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Updates an existing image asset.
        /// </summary>
        Task<ImageAsset> UpdateAsync(ImageAsset imageAsset, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Deletes an image asset by ID.
        /// </summary>
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
