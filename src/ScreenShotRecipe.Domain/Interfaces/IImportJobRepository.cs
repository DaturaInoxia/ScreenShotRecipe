using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ScreenShotRecipe.Domain.Entities;

namespace ScreenShotRecipe.Domain.Interfaces
{
    /// <summary>
    /// Repository interface for ImportJob entities.
    /// </summary>
    public interface IImportJobRepository
    {
        /// <summary>
        /// Creates a new import job.
        /// </summary>
        Task<ImportJob> AddAsync(ImportJob importJob, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets an import job by ID with related entities (images, recipe).
        /// </summary>
        Task<ImportJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets an import job by idempotency key.
        /// </summary>
        Task<ImportJob?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Gets all import jobs with optional status filter.
        /// </summary>
        Task<IReadOnlyList<ImportJob>> GetAllAsync(ImportJobStatus? statusFilter = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Updates an existing import job.
        /// </summary>
        Task<ImportJob> UpdateAsync(ImportJob importJob, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Updates the status of an import job.
        /// </summary>
        Task UpdateStatusAsync(Guid id, ImportJobStatus status, string? errorMessage = null, string? diagnostics = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Deletes an import job and its associated images.
        /// </summary>
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
