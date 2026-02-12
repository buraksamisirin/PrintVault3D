namespace PrintVault3D.Services;

/// <summary>
/// Service interface for automatic tag generation based on file names.
/// </summary>
public interface IAutoTaggingService
{
    /// <summary>
    /// Generates a set of tags based on the file path and name.
    /// </summary>
    /// <param name="filePath">Full path to the 3D model file.</param>
    /// <returns>Set of generated tags.</returns>
    HashSet<string> GenerateTags(string filePath);

    /// <summary>
    /// Suggests a category based on the filename.
    /// </summary>
    /// <param name="filename">The filename to analyze.</param>
    /// <returns>Suggested category name or null if no match found.</returns>
    string? SuggestCategory(string filename);

}
