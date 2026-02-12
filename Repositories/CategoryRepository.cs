using Microsoft.EntityFrameworkCore;
using PrintVault3D.Data;
using PrintVault3D.Models;

namespace PrintVault3D.Repositories;

/// <summary>
/// Repository implementation for Category-specific operations.
/// </summary>
public class CategoryRepository : Repository<Category>, ICategoryRepository
{
    public CategoryRepository(PrintVaultDbContext context) : base(context)
    {
    }

    public async Task<Category?> GetByNameAsync(string name)
    {
        // Use EF.Functions.Like for case-insensitive comparison in SQL
        return await _dbSet.FirstOrDefaultAsync(c => 
            EF.Functions.Like(c.Name, name));
    }

    public async Task<IEnumerable<(Category Category, int ModelCount)>> GetAllWithModelCountsAsync()
    {
        // Use projection to count models without loading them into memory
        var result = await _dbSet
            .Select(c => new 
            { 
                Category = c, 
                ModelCount = c.Models.Count 
            })
            .ToListAsync();

        return result.Select(x => (x.Category, x.ModelCount));
    }

    public async Task<IEnumerable<Category>> GetAllWithModelsAsync()
    {
        return await _dbSet
            .Include(c => c.Models)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<Category?> FindBestMatchAsync(string filename)
    {
        var normalizedFilename = filename.ToLowerInvariant();
        var categories = await _dbSet.ToListAsync();

        Category? bestMatch = null;
        int bestScore = 0;

        foreach (var category in categories)
        {
            if (string.IsNullOrWhiteSpace(category.AutoKeywords))
                continue;

            var keywords = category.AutoKeywords
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(k => k.Trim().ToLowerInvariant());

            int score = 0;
            foreach (var keyword in keywords)
            {
                if (normalizedFilename.Contains(keyword))
                {
                    // Longer keyword matches are worth more
                    score += keyword.Length;
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestMatch = category;
            }
        }

        return bestMatch; 
    }
}

