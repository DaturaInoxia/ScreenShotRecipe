using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ScreenShotRecipe.Domain.Entities;

namespace ScreenShotRecipe.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Recipe> Recipes { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var listComparer = new ValueComparer<List<string>>(
                (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList());

            modelBuilder.Entity<Recipe>(eb => {
                eb.HasKey(r => r.Id);
                eb.Property(r => r.Title).IsRequired();
                eb.Property(r => r.Tags)
                    .HasConversion(
                        v => string.Join(',', v),
                        v => v.Split(',', System.StringSplitOptions.RemoveEmptyEntries).ToList())
                    .Metadata
                    .SetValueComparer(listComparer);
                eb.OwnsMany(r => r.Ingredients);
                eb.OwnsMany(r => r.Steps);
            });
        }
    }
}

