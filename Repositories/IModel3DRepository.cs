using PrintVault3D.Models;

namespace PrintVault3D.Repositories;

/// <summary>
/// Repository interface for Model3D-specific operations.
/// </summary>
public interface IModel3DRepository : IRepository<Model3D>
{
    /// <summary>
    /// Gets a model by its file path.
    /// </summary>
    Task<Model3D?> GetByFilePathAsync(string filePath);

    /// <summary>
    /// Gets all models with their category and G-code information.
    /// </summary>
    Task<IEnumerable<Model3D>> GetAllWithDetailsAsync();

    /// <summary>
    /// Gets models by category ID.
    /// </summary>
    Task<IEnumerable<Model3D>> GetByCategoryAsync(int categoryId);

    /// <summary>
    /// Gets models that don't have thumbnails generated yet.
    /// </summary>
    Task<IEnumerable<Model3D>> GetPendingThumbnailsAsync();

    /// <summary>
    /// Gets all favorite models.
    /// </summary>
    Task<IEnumerable<Model3D>> GetFavoritesAsync();

    /// <summary>
    /// Searches models by name, tags, or notes.
    /// </summary>
    Task<IEnumerable<Model3D>> SearchAsync(string searchTerm);

    /// <summary>
    /// Gets recently added models.
    /// </summary>
    Task<IEnumerable<Model3D>> GetRecentAsync(int count = 10);

    /// <summary>
    /// Gets paginated models with details.
    /// </summary>
    Task<PagedResult<Model3D>> GetPagedAsync(int page, int pageSize);

    /// <summary>
    /// Gets paginated models by collection ID.
    /// </summary>
    Task<PagedResult<Model3D>> GetPagedByCollectionAsync(int collectionId, int page, int pageSize);

    /// <summary>
    /// Gets paginated models by category.
    /// </summary>
    Task<PagedResult<Model3D>> GetPagedByCategoryAsync(int categoryId, int page, int pageSize);

    /// <summary>
    /// Gets paginated search results.
    /// </summary>
    Task<PagedResult<Model3D>> SearchPagedAsync(string searchTerm, int page, int pageSize);

    /// <summary>
    /// Gets all file hashes that have duplicates (appear more than once).
    /// </summary>
    Task<HashSet<string>> GetDuplicateHashesAsync();

    /// <summary>
    /// Gets all models with a specific file hash.
    /// </summary>
    Task<IEnumerable<Model3D>> GetByFileHashAsync(string fileHash);

    /// <summary>
    /// Gets duplicate hashes with their count.
    /// </summary>
    Task<Dictionary<string, int>> GetDuplicateHashesWithCountAsync();

    /// <summary>
    /// Gets paginated list of duplicate models only.
    /// </summary>
    Task<PagedResult<Model3D>> GetDuplicatesOnlyPagedAsync(int page, int pageSize);

    /// <summary>
    /// Gets specific models by their IDs.
    /// </summary>
    Task<IEnumerable<Model3D>> GetByIdsAsync(IEnumerable<int> ids);

    /// <summary>
    /// Gets total count of duplicate models.
    /// </summary>
    Task<int> GetDuplicatesCountAsync();
}

