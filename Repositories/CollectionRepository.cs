using Microsoft.EntityFrameworkCore;
using PrintVault3D.Data;
using PrintVault3D.Models;

namespace PrintVault3D.Repositories;

public class CollectionRepository : Repository<Collection>, ICollectionRepository
{
    public CollectionRepository(PrintVaultDbContext context) : base(context)
    {
    }

    public async Task<Collection?> GetDetailsAsync(int id)
    {
        return await _context.Collections
            .Include(c => c.Models)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<IEnumerable<Collection>> GetAllWithStatsAsync()
    {
        return await _context.Collections
            .Include(c => c.Models)
            .OrderByDescending(c => c.IsPinned)
            .ThenByDescending(c => c.LastModifiedDate)
            .ToListAsync();
    }

    public async Task<Collection?> GetByNameAsync(string name)
    {
        return await _context.Collections
            .Include(c => c.Models)
            .FirstOrDefaultAsync(c => EF.Functions.Like(c.Name, name));
    }

    public async Task<IEnumerable<Collection>> SearchAsync(string searchTerm)
    {
        var pattern = $"%{searchTerm}%";
        
        return await _context.Collections
            .Include(c => c.Models)
            .Where(c => EF.Functions.Like(c.Name, pattern) || 
                       (c.Description != null && EF.Functions.Like(c.Description, pattern)))
            .OrderByDescending(c => c.IsPinned)
            .ThenByDescending(c => c.LastModifiedDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Collection>> GetSortedAsync()
    {
        return await _context.Collections
            .Include(c => c.Models)
            .OrderByDescending(c => c.IsPinned)
            .ThenByDescending(c => c.LastModifiedDate)
            .ToListAsync();
    }

    public async Task<Collection> DuplicateAsync(int collectionId, string newName)
    {
        var original = await GetDetailsAsync(collectionId);
        if (original == null)
            throw new ArgumentException($"Collection with ID {collectionId} not found");

        var duplicate = new Collection
        {
            Name = newName,
            Description = original.Description,
            Color = original.Color,
            IconName = original.IconName,
            CoverImagePath = original.CoverImagePath,
            IsPinned = false,
            CreatedDate = DateTime.UtcNow,
            LastModifiedDate = DateTime.UtcNow
        };

        await _context.Collections.AddAsync(duplicate);
        await _context.SaveChangesAsync();

        // Add the same models to the duplicate
        if (original.Models?.Any() == true)
        {
            var duplicateWithTracking = await GetDetailsAsync(duplicate.Id);
            if (duplicateWithTracking != null)
            {
                foreach (var model in original.Models)
                {
                    duplicateWithTracking.Models.Add(model);
                }
                await _context.SaveChangesAsync();
            }
        }

        return duplicate;
    }

    public async Task UpdateCoverImageAsync(int collectionId)
    {
        var collection = await _context.Collections
            .Include(c => c.Models)
            .FirstOrDefaultAsync(c => c.Id == collectionId);

        if (collection == null) return;

        // Find first model with a thumbnail
        var modelWithThumbnail = collection.Models?
            .FirstOrDefault(m => !string.IsNullOrEmpty(m.ThumbnailPath) && m.ThumbnailGenerated);

        if (modelWithThumbnail != null)
        {
            collection.CoverImagePath = modelWithThumbnail.ThumbnailPath;
            collection.LastModifiedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<IEnumerable<Collection>> GetByModelIdAsync(int modelId)
    {
        return await _context.Collections
            .Include(c => c.Models)
            .Where(c => c.Models.Any(m => m.Id == modelId))
            .ToListAsync();
    }

    public async Task<bool> NameExistsAsync(string name, int? excludeId = null)
    {
        var query = _context.Collections.AsQueryable();
        
        if (excludeId.HasValue)
        {
            query = query.Where(c => c.Id != excludeId.Value);
        }

        return await query.AnyAsync(c => EF.Functions.Like(c.Name, name));
    }
}
