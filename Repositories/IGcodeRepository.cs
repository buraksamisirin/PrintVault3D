using PrintVault3D.Models;

namespace PrintVault3D.Repositories;

/// <summary>
/// Repository interface for Gcode-specific operations.
/// </summary>
public interface IGcodeRepository : IRepository<Gcode>
{
    /// <summary>
    /// Gets a G-code by its file path.
    /// </summary>
    Task<Gcode?> GetByFilePathAsync(string filePath);

    /// <summary>
    /// Gets a G-code by its content hash.
    /// </summary>
    Task<Gcode?> GetByFileHashAsync(string fileHash);

    /// <summary>
    /// Gets all G-codes for a specific model.
    /// </summary>
    Task<IEnumerable<Gcode>> GetByModelIdAsync(int modelId);

    /// <summary>
    /// Gets unlinked G-codes (not associated with any model).
    /// </summary>
    Task<IEnumerable<Gcode>> GetUnlinkedAsync();

    /// <summary>
    /// Finds potential model matches for a G-code based on filename similarity.
    /// </summary>
    Task<IEnumerable<(Model3D Model, int Score)>> FindPotentialMatchesAsync(string gcodeFilename);

    /// <summary>
    /// Gets all G-codes with their linked models included.
    /// </summary>
    Task<IEnumerable<Gcode>> GetAllWithModelsAsync();
}

