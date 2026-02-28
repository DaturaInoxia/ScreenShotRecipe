using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ScreenShotRecipe.Domain.Entities;
using ScreenShotRecipe.Domain.Interfaces;
using ScreenShotRecipe.Infrastructure.Persistence;

namespace ScreenShotRecipe.Infrastructure.Repositories
{
    /// <summary>
    /// EF Core implementation of IImageAssetRepository.
    /// </summary>
    public class ImageAssetRepository : IImageAssetRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ImageAssetRepository> _logger;

        public ImageAssetRepository(AppDbContext context, ILogger<ImageAssetRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ImageAsset> AddAsync(ImageAsset imageAsset, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Adding ImageAsset: {FileName} for ImportJob {ImportJobId}", 
                imageAsset.FileName, imageAsset.ImportJobId);
            
            _context.ImageAssets.Add(imageAsset);
            await _context.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("Created ImageAsset {Id}: {FileName}", imageAsset.Id, imageAsset.FileName);
            return imageAsset;
        }

        public async Task<ImageAsset?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.ImageAssets
                .Include(i => i.OcrResult)
                .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<ImageAsset>> GetByImportJobIdAsync(Guid importJobId, CancellationToken cancellationToken = default)
        {
            return await _context.ImageAssets
                .Include(i => i.OcrResult)
                .Where(i => i.ImportJobId == importJobId)
                .OrderBy(i => i.UploadOrder)
                .ToListAsync(cancellationToken);
        }

        public async Task<ImageAsset> UpdateAsync(ImageAsset imageAsset, CancellationToken cancellationToken = default)
        {
            _context.ImageAssets.Update(imageAsset);
            await _context.SaveChangesAsync(cancellationToken);
            
            _logger.LogDebug("Updated ImageAsset {Id}", imageAsset.Id);
            return imageAsset;
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var imageAsset = await _context.ImageAssets.FindAsync(new object[] { id }, cancellationToken);
            if (imageAsset != null)
            {
                _context.ImageAssets.Remove(imageAsset);
                await _context.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation("Deleted ImageAsset {Id}", id);
            }
        }
    }
}
