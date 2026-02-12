namespace PrintVault3D.Services;

/// <summary>
/// Service interface for learning from user corrections to improve auto-tagging.
/// </summary>
public interface ITagLearningService
{
    /// <summary>
    /// Records a user correction for learning.
    /// </summary>
    /// <param name="filename">The original filename.</param>
    /// <param name="categoryName">The category assigned by user.</param>
    /// <param name="tags">The tags assigned by user (comma-separated).</param>
    Task RecordCorrectionAsync(string filename, string? categoryName, string? tags);

    /// <summary>
    /// Gets learned suggestions for a filename.
    /// </summary>
    /// <param name="filename">The filename to get suggestions for.</param>
    /// <returns>Learned category and tags if available.</returns>
    Task<LearnedSuggestion?> GetLearnedSuggestionAsync(string filename);

    /// <summary>
    /// Increments the use count for a pattern, indicating it was correctly suggested.
    /// </summary>
    Task ConfirmPatternAsync(string pattern);
}

/// <summary>
/// Result of a learned suggestion lookup.
/// </summary>
public class LearnedSuggestion
{
    public string? CategoryName { get; set; }
    public string? Tags { get; set; }
    public int Confidence { get; set; }
    public string MatchedPattern { get; set; } = string.Empty;
}
