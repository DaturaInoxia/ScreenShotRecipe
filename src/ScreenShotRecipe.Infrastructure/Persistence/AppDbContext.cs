using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ScreenShotRecipe.Domain.Entities;

namespace ScreenShotRecipe.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Recipe> Recipes { get; set; } = null!;
        public DbSet<ImportJob> ImportJobs { get; set; } = null!;
        public DbSet<ImageAsset> ImageAssets { get; set; } = null!;
        public DbSet<OcrResult> OcrResults { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var listComparer = new ValueComparer<List<string>>(
                (c1, c2) => c1 != null && c2 != null && c1.SequenceEqual(c2),
                c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                c => c.ToList());

            // Recipe configuration
            modelBuilder.Entity<Recipe>(eb => {
                eb.HasKey(r => r.Id);
                eb.Property(r => r.Title).IsRequired().HasMaxLength(500);
                eb.Property(r => r.OverallConfidence).HasPrecision(5, 4);
                eb.Property(r => r.ConfidenceNotes).HasMaxLength(1000);
                eb.Property(r => r.Tags)
                    .HasConversion(
                        v => string.Join(',', v),
                        v => v.Split(',', System.StringSplitOptions.RemoveEmptyEntries).ToList())
                    .Metadata
                    .SetValueComparer(listComparer);
                
                // Ingredients - regular relationship with cascade delete
                eb.HasMany(r => r.Ingredients)
                    .WithOne()
                    .HasForeignKey(i => i.RecipeId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                // Steps - regular relationship with cascade delete
                eb.HasMany(r => r.Steps)
                    .WithOne()
                    .HasForeignKey(s => s.RecipeId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                // Recipe -> ImportJob relationship
                eb.HasOne(r => r.ImportJob)
                    .WithOne(j => j.Recipe)
                    .HasForeignKey<Recipe>(r => r.ImportJobId)
                    .OnDelete(DeleteBehavior.Restrict); // Don't cascade delete recipes when ImportJob is deleted
                
                // Indexes
                eb.HasIndex(r => r.CreatedAt);
                eb.HasIndex(r => r.ImportJobId);
            });

            // Ingredient configuration
            modelBuilder.Entity<Ingredient>(eb => {
                eb.HasKey(i => i.Id);
                eb.Property(i => i.RawText).IsRequired();
                eb.HasIndex(i => i.RecipeId);
            });

            // Step configuration
            modelBuilder.Entity<Step>(eb => {
                eb.HasKey(s => s.Id);
                eb.Property(s => s.Text).IsRequired();
                eb.HasIndex(s => s.RecipeId);
            });
            
            // ImportJob configuration
            modelBuilder.Entity<ImportJob>(eb => {
                eb.HasKey(j => j.Id);
                eb.Property(j => j.Status).IsRequired();
                eb.Property(j => j.Source).HasMaxLength(50);
                eb.Property(j => j.IdempotencyKey).HasMaxLength(100);
                eb.Property(j => j.ErrorMessage).HasMaxLength(2000);
                
                // ImportJob -> ImageAssets (one-to-many)
                eb.HasMany(j => j.ImageAssets)
                    .WithOne(i => i.ImportJob)
                    .HasForeignKey(i => i.ImportJobId)
                    .OnDelete(DeleteBehavior.Cascade); // Delete images when ImportJob is deleted
                
                // Indexes
                eb.HasIndex(j => j.Status);
                eb.HasIndex(j => j.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
            });
            
            // ImageAsset configuration
            modelBuilder.Entity<ImageAsset>(eb => {
                eb.HasKey(i => i.Id);
                eb.Property(i => i.FileName).IsRequired().HasMaxLength(255);
                eb.Property(i => i.FilePath).IsRequired().HasMaxLength(1024);
                eb.Property(i => i.MediaType).IsRequired().HasMaxLength(100);
                eb.Property(i => i.StorageProviderKey).HasMaxLength(50);
                
                // ImageAsset -> OcrResult (one-to-one, optional)
                eb.HasOne(i => i.OcrResult)
                    .WithOne(o => o.ImageAsset)
                    .HasForeignKey<OcrResult>(o => o.ImageAssetId)
                    .OnDelete(DeleteBehavior.Cascade); // Delete OCR result when image is deleted
                
                // Indexes
                eb.HasIndex(i => new { i.ImportJobId, i.UploadOrder });
            });
            
            // OcrResult configuration
            modelBuilder.Entity<OcrResult>(eb => {
                eb.HasKey(o => o.Id);
                eb.Property(o => o.DetectedLanguage).HasMaxLength(10);
                eb.Property(o => o.OcrEngine).HasMaxLength(100);
                eb.Property(o => o.Confidence).HasPrecision(5, 4);
            });
        }
    }
}

