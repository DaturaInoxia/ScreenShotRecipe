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

                // Get the actual DB version using a projection to avoid EF Core returning tracked entity
                var dbVersion = await _db.Recipes
                    .Where(r => r.Id == recipe.Id)
                    .Select(r => (int?)r.Version)
                    .FirstOrDefaultAsync(cancellationToken);

                if (dbVersion == null)
                {
                    _logger.LogWarning("Recipe not found for update: {RecipeId}", recipe.Id);
                    return false;
                }

                // Optimistic concurrency check: client's version should match DB version
                var expectedVersion = recipe.Version - 1;
                if (dbVersion.Value != expectedVersion)
                {
                    _logger.LogWarning("Version mismatch for recipe {RecipeId}: expected {Expected}, actual {Actual}", 
                        recipe.Id, expectedVersion, dbVersion.Value);
                    return false;
                }

                // Get IDs of ingredients/steps that actually exist in DB (not from tracked entity)
                var existingIngredientIds = await _db.Set<Ingredient>()
                    .Where(i => i.RecipeId == recipe.Id)
                    .Select(i => i.Id)
                    .ToListAsync(cancellationToken);

                var existingStepIds = await _db.Set<Step>()
                    .Where(s => s.RecipeId == recipe.Id)
                    .Select(s => s.Id)
                    .ToListAsync(cancellationToken);

                // Materialize new ingredients/steps from the input recipe
                var newIngredients = recipe.Ingredients.Select(i => new Ingredient
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    RawText = i.RawText,
                    Name = i.Name,
                    Quantity = i.Quantity,
                    Unit = i.Unit
                }).ToList();

                var newSteps = recipe.Steps.Select(s => new Step
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id,
                    Ordinal = s.Ordinal,
                    Text = s.Text
                }).ToList();

                // Delete old ingredients and steps by ID (direct DB operation)
                if (existingIngredientIds.Any())
                {
                    await _db.Set<Ingredient>()
                        .Where(i => existingIngredientIds.Contains(i.Id))
                        .ExecuteDeleteAsync(cancellationToken);
                }

                if (existingStepIds.Any())
                {
                    await _db.Set<Step>()
                        .Where(s => existingStepIds.Contains(s.Id))
                        .ExecuteDeleteAsync(cancellationToken);
                }

                // Update the recipe (reload to get clean tracked entity after deletes)
                var existing = await _db.Recipes.FindAsync(new object[] { recipe.Id }, cancellationToken);
                if (existing == null)
                {
                    return false;
                }

                // Update scalar properties
                existing.Title = recipe.Title;
                existing.Notes = recipe.Notes;
                existing.Tags = recipe.Tags;
                existing.OverallConfidence = recipe.OverallConfidence;
                existing.ConfidenceNotes = recipe.ConfidenceNotes;
                existing.Version = recipe.Version;

                // Add new ingredients and steps
                _db.Set<Ingredient>().AddRange(newIngredients);
                _db.Set<Step>().AddRange(newSteps);

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

        public async Task<int?> UpdateRecipeAsync(
            Guid id,
            int clientVersion,
            string title,
            string? notes,
            List<string> tags,
            List<(string RawText, string? Name, string? Quantity, string? Unit)> ingredients,
            List<(int Ordinal, string Text)> steps,
            CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Updating recipe {RecipeId} from version {Version}", id, clientVersion);

                // Load recipe without collections for version check
                var recipe = await _db.Recipes.FindAsync(new object[] { id }, cancellationToken);

                if (recipe == null)
                {
                    _logger.LogWarning("Recipe not found: {RecipeId}", id);
                    return null;
                }

                // Optimistic concurrency check
                if (recipe.Version != clientVersion)
                {
                    _logger.LogWarning("Version mismatch for recipe {RecipeId}: expected {Expected}, actual {Actual}",
                        id, clientVersion, recipe.Version);
                    return null;
                }

                // Update scalar properties
                recipe.Title = title;
                recipe.Notes = notes;
                recipe.Tags = tags;
                recipe.Version = clientVersion + 1;
                recipe.UpdatedAt = DateTime.UtcNow;

                // Delete old ingredients and steps using ExecuteDelete (bypasses change tracking)
                await _db.Set<Ingredient>()
                    .Where(i => i.RecipeId == id)
                    .ExecuteDeleteAsync(cancellationToken);

                await _db.Set<Step>()
                    .Where(s => s.RecipeId == id)
                    .ExecuteDeleteAsync(cancellationToken);

                // Add new ingredients
                var newIngredients = ingredients.Select(ing => new Ingredient
                {
                    Id = Guid.NewGuid(),
                    RecipeId = id,
                    RawText = ing.RawText,
                    Name = ing.Name,
                    Quantity = ing.Quantity,
                    Unit = ing.Unit
                }).ToList();

                // Add new steps
                var newSteps = steps.Select(s => new Step
                {
                    Id = Guid.NewGuid(),
                    RecipeId = id,
                    Ordinal = s.Ordinal,
                    Text = s.Text
                }).ToList();

                _db.Set<Ingredient>().AddRange(newIngredients);
                _db.Set<Step>().AddRange(newSteps);

                await _db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Recipe updated successfully: {RecipeId}, new version: {Version}", id, recipe.Version);
                return recipe.Version;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating recipe: {RecipeId}", id);
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
