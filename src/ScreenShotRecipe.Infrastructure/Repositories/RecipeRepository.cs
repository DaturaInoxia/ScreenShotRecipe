using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ScreenShotRecipe.Domain.Entities;
using ScreenShotRecipe.Domain.Interfaces;
using ScreenShotRecipe.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ScreenShotRecipe.Infrastructure.Repositories
{
    public class RecipeRepository : IRecipeRepository
    {
        private readonly AppDbContext _db;
        private readonly ILogger<RecipeRepository> _logger;

        public RecipeRepository(AppDbContext db, ILogger<RecipeRepository> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task AddAsync(Recipe recipe)
        {
            try
            {
                _logger.LogInformation("Adding recipe to database: {RecipeId}, Title: {Title}", recipe.Id, recipe.Title);
                _db.Recipes.Add(recipe);
                await _db.SaveChangesAsync();
                _logger.LogInformation("Recipe successfully saved to database: {RecipeId}", recipe.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving recipe to database: {RecipeId}", recipe.Id);
                throw;
            }
        }

        public async Task<Recipe?> GetAsync(Guid id)
        {
            try
            {
                _logger.LogInformation("Retrieving recipe from database: {RecipeId}", id);
                var recipe = await _db.Recipes.Include(r => r.Ingredients).Include(r => r.Steps).FirstOrDefaultAsync(r => r.Id == id);
                if (recipe == null)
                {
                    _logger.LogWarning("Recipe not found in database: {RecipeId}", id);
                }
                else
                {
                    _logger.LogInformation("Recipe retrieved successfully: {RecipeId}, Title: {Title}", id, recipe.Title);
                }
                return recipe;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recipe from database: {RecipeId}", id);
                throw;
            }
        }

        public async Task<List<Recipe>> GetAllAsync()
        {
            try
            {
                _logger.LogInformation("Retrieving all recipes from database");
                var recipes = await _db.Recipes.Include(r => r.Ingredients).Include(r => r.Steps).ToListAsync();
                _logger.LogInformation("Retrieved {RecipeCount} recipes from database", recipes.Count);
                return recipes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recipes from database");
                throw;
            }
        }
    }
}
