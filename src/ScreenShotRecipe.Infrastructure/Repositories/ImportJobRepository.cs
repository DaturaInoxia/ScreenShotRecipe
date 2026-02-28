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
    /// EF Core implementation of IImportJobRepository.
    /// </summary>
    public class ImportJobRepository : IImportJobRepository
    {
        private readonly AppDbContext _context;
        private readonly ILogger<ImportJobRepository> _logger;

        public ImportJobRepository(AppDbContext context, ILogger<ImportJobRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ImportJob> AddAsync(ImportJob importJob, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Creating ImportJob from source: {Source}", importJob.Source);
            
            _context.ImportJobs.Add(importJob);
            await _context.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation("Created ImportJob {Id} with {ImageCount} images", 
                importJob.Id, importJob.ImageAssets.Count);
            return importJob;
        }

        public async Task<ImportJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.ImportJobs
                .Include(j => j.ImageAssets)
                    .ThenInclude(i => i.OcrResult)
                .Include(j => j.Recipe)
                    .ThenInclude(r => r!.Ingredients)
                .Include(j => j.Recipe)
                    .ThenInclude(r => r!.Steps)
                .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        }

        public async Task<ImportJob?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default)
        {
            return await _context.ImportJobs
                .Include(j => j.ImageAssets)
                .Include(j => j.Recipe)
                .FirstOrDefaultAsync(j => j.IdempotencyKey == idempotencyKey, cancellationToken);
        }

        public async Task<IReadOnlyList<ImportJob>> GetAllAsync(ImportJobStatus? statusFilter = null, CancellationToken cancellationToken = default)
        {
            var query = _context.ImportJobs
                .Include(j => j.ImageAssets)
                .AsQueryable();

            if (statusFilter.HasValue)
            {
                query = query.Where(j => j.Status == statusFilter.Value);
            }

            return await query
                .OrderByDescending(j => j.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public async Task<ImportJob> UpdateAsync(ImportJob importJob, CancellationToken cancellationToken = default)
        {
            _context.ImportJobs.Update(importJob);
            await _context.SaveChangesAsync(cancellationToken);
            
            _logger.LogDebug("Updated ImportJob {Id}, Status: {Status}", importJob.Id, importJob.Status);
            return importJob;
        }

        public async Task UpdateStatusAsync(Guid id, ImportJobStatus status, string? errorMessage = null, string? diagnostics = null, CancellationToken cancellationToken = default)
        {
            var importJob = await _context.ImportJobs.FindAsync(new object[] { id }, cancellationToken);
            if (importJob != null)
            {
                importJob.Status = status;
                
                if (status == ImportJobStatus.OcrInProgress && !importJob.StartedAt.HasValue)
                {
                    importJob.StartedAt = DateTime.UtcNow;
                }
                
                if (status == ImportJobStatus.Succeeded || status == ImportJobStatus.Failed || status == ImportJobStatus.PartiallyFailed)
                {
                    importJob.CompletedAt = DateTime.UtcNow;
                }
                
                if (errorMessage != null)
                {
                    importJob.ErrorMessage = errorMessage;
                }
                
                if (diagnostics != null)
                {
                    importJob.Diagnostics = diagnostics;
                }
                
                await _context.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation("ImportJob {Id} status changed to {Status}", id, status);
            }
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var importJob = await _context.ImportJobs
                .Include(j => j.ImageAssets)
                .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
                
            if (importJob != null)
            {
                _context.ImportJobs.Remove(importJob);
                await _context.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation("Deleted ImportJob {Id}", id);
            }
        }
    }
}
