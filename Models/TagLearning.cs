namespace PrintVault3D.Models;

/// <summary>
/// Represents a learned association between filename patterns and categories/tags.
/// Used by the learning system to improve auto-categorization based on user corrections.
/// </summary>
public class TagLearning
{
    public int Id { get; set; }

    /// <summary>
    /// The pattern extracted from filename (e.g., "gridfinity", "baby_yoda", "benchy").
    /// </summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>
    /// The category name that was assigned by the user for this pattern.
    /// </summary>
    public string? LearnedCategory { get; set; }

    /// <summary>
    /// Comma-separated list of tags assigned by the user.
    /// </summary>
    public string? LearnedTags { get; set; }

    /// <summary>
    /// Number of times this pattern has been used/confirmed.
    /// Higher count = higher confidence in the suggestion.
    /// </summary>
    public int UseCount { get; set; } = 1;

    /// <summary>
    /// When this learning was last applied or updated.
    /// </summary>
    public DateTime LastUsed { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this learning was first created.
    /// </summary>
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}
