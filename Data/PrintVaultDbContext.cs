using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PrintVault3D.Models;

namespace PrintVault3D.Data;

/// <summary>
/// Entity Framework Core DbContext for PrintVault 3D.
/// </summary>
public class PrintVaultDbContext : DbContext
{
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<Model3D> Models { get; set; } = null!;
    public DbSet<Gcode> Gcodes { get; set; } = null!;
    public DbSet<Collection> Collections { get; set; } = null!;
    public DbSet<TagLearning> TagLearnings { get; set; } = null!;

    private readonly string _dbPath;

    public PrintVaultDbContext()
    {
        // Default database path in user's AppData
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var vaultPath = Path.Combine(appDataPath, "PrintVault3D");
        Directory.CreateDirectory(vaultPath);
        _dbPath = Path.Combine(vaultPath, "printvault.db");
    }

    public PrintVaultDbContext(DbContextOptions<PrintVaultDbContext> options) : base(options)
    {
        _dbPath = string.Empty;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite($"Data Source={_dbPath}");
            optionsBuilder.EnableSensitiveDataLogging(false);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Category configuration
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasIndex(c => c.Name).IsUnique();
            
            // Seed default categories
            entity.HasData(
                new Category { Id = 1, Name = "Uncategorized", Description = "Default category for new models" },
                new Category { Id = 2, Name = "Calibration", Description = "Calibration prints", AutoKeywords = "benchy,calibration,test,cube,temp,tower" },
                new Category { Id = 3, Name = "Functional Parts", Description = "Functional and mechanical parts", AutoKeywords = "mount,bracket,holder,clip,hook,hinge,gear" },
                new Category { Id = 4, Name = "Toys & Games", Description = "Toys, games, and figurines", AutoKeywords = "toy,game,figure,figurine,miniature,dice" },
                new Category { Id = 5, Name = "Art & Decoration", Description = "Decorative items and art pieces", AutoKeywords = "vase,art,decoration,decor,ornament,statue,bust" },
                new Category { Id = 6, Name = "Tools", Description = "Tools and utility items", AutoKeywords = "tool,wrench,screwdriver,organizer,box,tray" },
                new Category { Id = 7, Name = "Electronics", Description = "Electronics enclosures and mounts", AutoKeywords = "case,enclosure,raspberry,arduino,esp32,stand,dock" }
            );
        });

        // Model3D configuration
        modelBuilder.Entity<Model3D>(entity =>
        {
            entity.HasIndex(m => m.FilePath).IsUnique();
            entity.HasIndex(m => m.FileHash); // Optimize duplicate lookups
            entity.HasIndex(m => m.Name);
            entity.HasIndex(m => m.AddedDate);
            entity.HasIndex(m => m.IsFavorite);

            entity.HasOne(m => m.Category)
                  .WithMany(c => c.Models)
                  .HasForeignKey(m => m.CategoryId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Gcode configuration
        modelBuilder.Entity<Gcode>(entity =>
        {
            entity.HasIndex(g => g.FilePath).IsUnique();
            entity.HasIndex(g => g.FileHash); // Optimize duplicate lookups
            entity.HasIndex(g => g.AddedDate);

            entity.HasOne(g => g.Model)
                  .WithMany(m => m.Gcodes)
                  .HasForeignKey(g => g.ModelId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Collection configuration
        modelBuilder.Entity<Collection>(entity =>
        {
            entity.HasIndex(c => c.Name).IsUnique();
            entity.HasMany(c => c.Models)
                  .WithMany(m => m.Collections)
                  .UsingEntity(j => j.ToTable("CollectionModels"));
        });
    }

    /// <summary>
    /// Ensures the Collection table has all required columns.
    /// This is needed for backward compatibility when upgrading from older versions.
    /// </summary>
    public async Task EnsureCollectionColumnsExistAsync()
    {
        var connection = Database.GetDbConnection();
        await connection.OpenAsync();

        try
        {
            // Check if columns exist and add them if not
            var columnsToAdd = new Dictionary<string, string>
            {
                { "Color", "TEXT" },
                { "CoverImagePath", "TEXT" },
                { "IconName", "TEXT" },
                { "IsPinned", "INTEGER DEFAULT 0" },
                { "CategoryId", "INTEGER" }
            };

            foreach (var column in columnsToAdd)
            {
                try
                {
                    // Try to add the column - will fail if it already exists
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = $"ALTER TABLE Collections ADD COLUMN {column.Key} {column.Value}";
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (SqliteException)
                {
                    // Column already exists - this is fine
                }
            }
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    /// <summary>
    /// Ensures the TagLearnings table exists.
    /// Manually creates the table if migration fails or is not applied.
    /// </summary>
    public async Task EnsureTagLearningTableExistAsync()
    {
        var connection = Database.GetDbConnection();
        await connection.OpenAsync();

        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS ""TagLearnings"" (
                    ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_TagLearnings"" PRIMARY KEY AUTOINCREMENT,
                    ""Pattern"" TEXT NOT NULL,
                    ""LearnedCategory"" TEXT NULL,
                    ""LearnedTags"" TEXT NULL,
                    ""UseCount"" INTEGER NOT NULL,
                    ""LastUsed"" TEXT NOT NULL,
                    ""CreatedDate"" TEXT NOT NULL
                );";
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    /// <summary>
    /// Ensures the Gcodes table has all required columns for newer features.
    /// </summary>
    public async Task EnsureGcodeColumnsExistAsync()
    {
        var connection = Database.GetDbConnection();
        await connection.OpenAsync();

        try
        {
            var columnsToAdd = new Dictionary<string, string>
            {
                { "PrintStatus", "INTEGER DEFAULT 0" },
                { "Rating", "INTEGER" },
                { "ActualPrintTimeTicks", "INTEGER" },
                { "FileHash", "TEXT" }
            };

            foreach (var column in columnsToAdd)
            {
                try
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = $"ALTER TABLE Gcodes ADD COLUMN {column.Key} {column.Value}";
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (SqliteException)
                {
                    // Column already exists
                }
            }
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
}

