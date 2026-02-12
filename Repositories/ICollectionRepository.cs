using PrintVault3D.Models;

namespace PrintVault3D.Repositories;

public interface ICollectionRepository : IRepository<Collection>
{
    /// <summary>
    /// Gets a collection with all its models loaded.
    /// </summary>
    Task<Collection?> GetDetailsAsync(int id);

    /// <summary>
    /// Gets all collections with model counts for display in sidebar.
    /// </summary>
    Task<IEnumerable<Collection>> GetAllWithStatsAsync();

    /// <summary>
    /// Gets a collection by name (case-insensitive).
    /// </summary>
    Task<Collection?> GetByNameAsync(string name);

    /// <summary>
    /// Searches collections by name or description.
    /// </summary>
    Task<IEnumerable<Collection>> SearchAsync(string searchTerm);

    /// <summary>
    /// Gets pinned collections first, then sorted by last modified.
    /// </summary>
    Task<IEnumerable<Collection>> GetSortedAsync();

    /// <summary>
    /// Duplicates a collection with all its models.
    /// </summary>
    Task<Collection> DuplicateAsync(int collectionId, string newName);

    /// <summary>
    /// Updates the cover image from the first model with a thumbnail.
    /// </summary>
    Task UpdateCoverImageAsync(int collectionId);

    /// <summary>
    /// Gets collections that contain a specific model.
    /// </summary>
    Task<IEnumerable<Collection>> GetByModelIdAsync(int modelId);

    /// <summary>
    /// Checks if a collection name already exists.
    /// </summary>
    Task<bool> NameExistsAsync(string name, int? excludeId = null);
}
