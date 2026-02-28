using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
                var recipe = await _db.Recipes
                    .Include(r => r.Ingredients)
                    .Include(r => r.Steps)
                    .Include(r => r.ImportJob)
                        .ThenInclude(j => j!.ImageAssets)
                    .FirstOrDefaultAsync(r => r.Id == id);
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
                var recipes = await _db.Recipes
                    .Include(r => r.Ingredients)
                    .Include(r => r.Steps)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();
                _logger.LogInformation("Retrieved {RecipeCount} recipes from database", recipes.Count);
                return recipes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recipes from database");
                throw;
            }
        }

        public async Task<List<Recipe>> SearchAsync(string query, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return await GetAllAsync();
            }

            try
            {
                _logger.LogInformation("Searching recipes with query: {Query}", query);
                
                var queryLower = query.ToLowerInvariant();
                var terms = queryLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                // EF Core SQLite doesn't support complex full-text search, so we do basic LIKE matching
                // Load recipes with ingredients for in-memory search
                var recipes = await _db.Recipes
                    .Include(r => r.Ingredients)
                    .Include(r => r.Steps)
                    .ToListAsync(cancellationToken);

                // Score and filter recipes based on query terms
                var scoredRecipes = recipes
                    .Select(r => new
                    {
                        Recipe = r,
                        Score = CalculateRelevanceScore(r, terms)
                    })
                    .Where(x => x.Score > 0)
                    .OrderByDescending(x => x.Score)
                    .ThenByDescending(x => x.Recipe.CreatedAt)
                    .Select(x => x.Recipe)
                    .ToList();

                _logger.LogInformation("Search found {ResultCount} recipes for query: {Query}", scoredRecipes.Count, query);
                return scoredRecipes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching recipes with query: {Query}", query);
                throw;
            }
        }

        private static int CalculateRelevanceScore(Recipe recipe, string[] terms)
        {
            var score = 0;
            var titleLower = recipe.Title.ToLowerInvariant();
            var tagsLower = recipe.Tags.Select(t => t.ToLowerInvariant()).ToList();
            var ingredientsLower = recipe.Ingredients.Select(i => i.Name?.ToLowerInvariant() ?? i.RawText.ToLowerInvariant()).ToList();

            foreach (var term in terms)
            {
                // Title match (highest weight)
                if (titleLower.Contains(term))
                {
                    score += 10;
                    if (titleLower.StartsWith(term)) score += 5; // Bonus for prefix match
                }

                // Tag match (high weight)
                if (tagsLower.Any(t => t.Contains(term)))
                {
                    score += 8;
                }

                // Ingredient name match
                if (ingredientsLower.Any(i => i.Contains(term)))
                {
                    score += 5;
                }

                // Notes match (low weight)
                if (!string.IsNullOrEmpty(recipe.Notes) && recipe.Notes.ToLowerInvariant().Contains(term))
                {
                    score += 2;
                }
            }

            return score;
        }

        public async Task<bool> UpdateAsync(Recipe recipe, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Updating recipe: {RecipeId}, Version: {Version}", recipe.Id, recipe.Version);

                // Load existing recipe to check version
                var existing = await _db.Recipes
                    .Include(r => r.Ingredients)
                    .Include(r => r.Steps)
                    .FirstOrDefaultAsync(r => r.Id == recipe.Id, cancellationToken);

                if (existing == null)
                {
                    _logger.LogWarning("Recipe not found for update: {RecipeId}", recipe.Id);
                    return false;
                }

                // Optimistic concurrency check
                var expectedVersion = recipe.Version - 1;
                if (existing.Version != expectedVersion)
                {
                    _logger.LogWarning("Version mismatch for recipe {RecipeId}: expected {Expected}, actual {Actual}", 
                        recipe.Id, expectedVersion, existing.Version);
                    return false;
                }

                // Update scalar properties
                existing.Title = recipe.Title;
                existing.Notes = recipe.Notes;
                existing.Tags = recipe.Tags;
                existing.OverallConfidence = recipe.OverallConfidence;
                existing.ConfidenceNotes = recipe.ConfidenceNotes;
                existing.Version = recipe.Version;

                // Update ingredients - remove old, add new
                _db.RemoveRange(existing.Ingredients);
                foreach (var ing in recipe.Ingredients)
                {
                    ing.RecipeId = recipe.Id;
                    existing.Ingredients.Add(ing);
                }

                // Update steps - remove old, add new
                _db.RemoveRange(existing.Steps);
                foreach (var step in recipe.Steps)
                {
                    step.RecipeId = recipe.Id;
                    existing.Steps.Add(step);
                }

                await _db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Recipe updated successfully: {RecipeId}, new Version: {Version}", recipe.Id, recipe.Version);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating recipe: {RecipeId}", recipe.Id);
                throw;
            }
        }

        public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Deleting recipe: {RecipeId}", id);
                
                var recipe = await _db.Recipes.FindAsync(new object[] { id }, cancellationToken);
                if (recipe == null)
                {
                    _logger.LogWarning("Recipe not found for deletion: {RecipeId}", id);
                    return false;
                }

                _db.Recipes.Remove(recipe);
                await _db.SaveChangesAsync(cancellationToken);
                
                _logger.LogInformation("Recipe deleted successfully: {RecipeId}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting recipe: {RecipeId}", id);
                throw;
            }
        }
    }
}
