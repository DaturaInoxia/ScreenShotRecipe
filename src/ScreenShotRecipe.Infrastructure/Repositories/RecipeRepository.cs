using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ScreenShotRecipe.Domain.Entities;
using ScreenShotRecipe.Domain.Interfaces;
using ScreenShotRecipe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ScreenShotRecipe.Infrastructure.Repositories
{
    public class RecipeRepository : IRecipeRepository
    {
        private readonly AppDbContext _db;

        public RecipeRepository(AppDbContext db) => _db = db;

        public async Task AddAsync(Recipe recipe)
        {
            _db.Recipes.Add(recipe);
            await _db.SaveChangesAsync();
        }

        public async Task<Recipe?> GetAsync(Guid id)
        {
            return await _db.Recipes.Include(r => r.Ingredients).Include(r => r.Steps).FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<List<Recipe>> GetAllAsync()
        {
            return await _db.Recipes.Include(r => r.Ingredients).Include(r => r.Steps).ToListAsync();
        }
    }
}
