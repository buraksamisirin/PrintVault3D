using Microsoft.EntityFrameworkCore;
using PrintVault3D.Data;
using PrintVault3D.Models;

namespace PrintVault3D.Repositories;

/// <summary>
/// Repository implementation for Model3D-specific operations.
/// </summary>
public class Model3DRepository : Repository<Model3D>, IModel3DRepository
{
    public Model3DRepository(PrintVaultDbContext context) : base(context)
    {
    }

    public async Task<Model3D?> GetByFilePathAsync(string filePath)
    {
        return await _dbSet
            .Include(m => m.Category)
            .Include(m => m.Gcodes)
            .FirstOrDefaultAsync(m => m.FilePath == filePath);
    }

    public async Task<IEnumerable<Model3D>> GetAllWithDetailsAsync()
    {
        return await _dbSet
            .Include(m => m.Category)
            .Include(m => m.Gcodes)
            .AsNoTracking()
            .OrderByDescending(m => m.AddedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Model3D>> GetByCategoryAsync(int categoryId)
    {
        return await _dbSet
            .Include(m => m.Category)
            .Include(m => m.Gcodes)
            .Where(m => m.CategoryId == categoryId)
            .AsNoTracking()
            .OrderByDescending(m => m.AddedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Model3D>> GetPendingThumbnailsAsync()
    {
        return await _dbSet
            .Where(m => !m.ThumbnailGenerated)
            .OrderBy(m => m.AddedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Model3D>> GetFavoritesAsync()
    {
        return await _dbSet
            .Include(m => m.Category)
            .Where(m => m.IsFavorite)
            .OrderByDescending(m => m.AddedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Model3D>> SearchAsync(string searchTerm)
    {
        // Use EF.Functions.Like for case-insensitive search that translates to SQL
        var pattern = $"%{searchTerm}%";
        
        return await _dbSet
            .Include(m => m.Category)
            .Include(m => m.Gcodes)
            .AsNoTracking()
            .Where(m => 
                EF.Functions.Like(m.Name, pattern) ||
                (m.Tags != null && EF.Functions.Like(m.Tags, pattern)) ||
                (m.Notes != null && EF.Functions.Like(m.Notes, pattern)) ||
                (m.Category != null && EF.Functions.Like(m.Category.Name, pattern)))
            .OrderByDescending(m => m.AddedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Model3D>> GetRecentAsync(int count = 10)
    {
        return await _dbSet
            .Include(m => m.Category)
            .AsNoTracking()
            .OrderByDescending(m => m.AddedDate)
            .Take(count)
            .ToListAsync();
    }

    public async Task<PagedResult<Model3D>> GetPagedAsync(int page, int pageSize)
    {
        var total = await _dbSet.CountAsync();
        var items = await _dbSet
            .Include(m => m.Category)
            .Include(m => m.Gcodes)
            .AsNoTracking()
            .OrderByDescending(m => m.AddedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Model3D>(items, total, page, pageSize);
    }

    public async Task<PagedResult<Model3D>> GetPagedByCollectionAsync(int collectionId, int page, int pageSize)
    {
        var query = _dbSet
            .Include(m => m.Category)
            .Include(m => m.Gcodes)
            .AsNoTracking()
            .Where(m => m.Collections.Any(c => c.Id == collectionId));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(m => m.AddedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Model3D>(items, total, page, pageSize);
    }

    public async Task<PagedResult<Model3D>> GetPagedByCategoryAsync(int categoryId, int page, int pageSize)
    {
        var query = _dbSet
            .Include(m => m.Category)
            .Include(m => m.Gcodes)
            .AsNoTracking()
            .Where(m => m.CategoryId == categoryId);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(m => m.AddedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Model3D>(items, total, page, pageSize);
    }

    public async Task<PagedResult<Model3D>> SearchPagedAsync(string searchTerm, int page, int pageSize)
    {
        // Use EF.Functions.Like for case-insensitive search that translates to SQL
        var pattern = $"%{searchTerm}%";
        
        var query = _dbSet
            .Include(m => m.Category)
            .Include(m => m.Gcodes)
            .AsNoTracking()
            .Where(m => 
                EF.Functions.Like(m.Name, pattern) ||
                (m.Tags != null && EF.Functions.Like(m.Tags, pattern)) ||
                (m.Notes != null && EF.Functions.Like(m.Notes, pattern)) ||
                (m.Category != null && EF.Functions.Like(m.Category.Name, pattern)));

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(m => m.AddedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Model3D>(items, total, page, pageSize);
    }

    public async Task<HashSet<string>> GetDuplicateHashesAsync()
    {
        // Use SQL GROUP BY HAVING COUNT > 1 for better performance
        var duplicateHashes = await _dbSet
            .Where(m => m.FileHash != null)
            .GroupBy(m => m.FileHash!)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToListAsync();

        return duplicateHashes.ToHashSet();
    }

    public async Task<IEnumerable<Model3D>> GetByFileHashAsync(string fileHash)
    {
        return await _dbSet
            .Include(m => m.Category)
            .AsNoTracking()
            .Where(m => m.FileHash == fileHash)
            .OrderByDescending(m => m.AddedDate)
            .ToListAsync();
    }

    public async Task<Dictionary<string, int>> GetDuplicateHashesWithCountAsync()
    {
        // Use SQL GROUP BY HAVING COUNT > 1 for better performance
        var duplicates = await _dbSet
            .Where(m => m.FileHash != null)
            .GroupBy(m => m.FileHash!)
            .Where(g => g.Count() > 1)
            .Select(g => new { Hash = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Hash, g => g.Count);

        return duplicates;
    }

    public async Task<PagedResult<Model3D>> GetDuplicatesOnlyPagedAsync(int page, int pageSize)
    {
        // Use SQL GROUP BY HAVING COUNT > 1 to get duplicate hashes efficiently
        var duplicateHashes = await _dbSet
            .Where(m => m.FileHash != null)
            .GroupBy(m => m.FileHash!)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToListAsync();

        // Now query models with those hashes
        var query = _dbSet
            .Include(m => m.Category)
            .AsNoTracking()
            .Where(m => m.FileHash != null && duplicateHashes.Contains(m.FileHash));

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(m => m.FileHash)
            .ThenByDescending(m => m.AddedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Model3D>(items, total, page, pageSize);
    }

    public async Task<IEnumerable<Model3D>> GetByIdsAsync(IEnumerable<int> ids)
    {
        return await _dbSet
            .Include(m => m.Category)
            .Include(m => m.Gcodes)
            .Where(m => ids.Contains(m.Id))
            .ToListAsync();
    }

    public async Task<int> GetDuplicatesCountAsync()
    {
        // Use SQL GROUP BY HAVING COUNT > 1 for better performance
        var duplicateHashes = await _dbSet
            .Where(m => m.FileHash != null)
            .GroupBy(m => m.FileHash!)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToListAsync();

        return await _dbSet
            .Where(m => m.FileHash != null && duplicateHashes.Contains(m.FileHash))
            .CountAsync();
    }
}

