using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ScreenShotRecipe.Domain.Entities;

namespace ScreenShotRecipe.Domain.Interfaces
{
    public interface IRecipeRepository
    {
        Task AddAsync(Recipe recipe);
        Task<Recipe?> GetAsync(Guid id);
        Task<List<Recipe>> GetAllAsync();
    }
}
