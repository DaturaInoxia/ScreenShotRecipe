using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ScreenShotRecipe.Domain.Entities;

namespace ScreenShotRecipe.Domain.Interfaces
{
    public interface IRecipeRepository
    {
        Task AddAsync(Recipe recipe);
        Task<Recipe?> GetAsync(Guid id);
        Task<List<Recipe>> GetAllAsync();
        
        /// <summary>
        /// Search recipes by title, ingredient names, or tags.
        /// Returns matching recipes ordered by relevance.
        /// </summary>
        Task<List<Recipe>> SearchAsync(string query, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Update an existing recipe with optimistic concurrency.
        /// </summary>
        /// <returns>True if update succeeded, false if version mismatch (optimistic concurrency failure).</returns>
        Task<bool> UpdateAsync(Recipe recipe, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Delete a recipe by ID.
        /// </summary>
        Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
