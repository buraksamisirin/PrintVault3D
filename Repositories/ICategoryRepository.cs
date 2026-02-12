using PrintVault3D.Models;

namespace PrintVault3D.Repositories;

/// <summary>
/// Repository interface for Category-specific operations.
/// </summary>
public interface ICategoryRepository : IRepository<Category>
{
    /// <summary>
    /// Gets a category by its name.
    /// </summary>
    Task<Category?> GetByNameAsync(string name);

    /// <summary>
    /// Gets all categories with their model counts.
    /// </summary>
    Task<IEnumerable<(Category Category, int ModelCount)>> GetAllWithModelCountsAsync();

    /// <summary>
    /// Finds the best matching category based on filename keywords.
    /// Returns null if no match found.
    /// </summary>
    Task<Category?> FindBestMatchAsync(string filename);

    /// <summary>
    /// Gets all categories with their models included.
    /// </summary>
    Task<IEnumerable<Category>> GetAllWithModelsAsync();
}

