using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintVault3D.Models;

/// <summary>
/// Represents a user-defined collection or project that groups multiple models.
/// </summary>
public class Collection
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Hex color code for visual distinction (e.g., "#FF5733")
    /// </summary>
    [MaxLength(9)]
    public string? Color { get; set; }

    /// <summary>
    /// Path to cover image (auto-set from first model's thumbnail or user-selected)
    /// </summary>
    public string? CoverImagePath { get; set; }

    /// <summary>
    /// Icon name from predefined set (folder, star, box, heart, etc.)
    /// </summary>
    [MaxLength(50)]
    public string? IconName { get; set; }

    /// <summary>
    /// Pin important collections to top
    /// </summary>
    public bool IsPinned { get; set; }

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime LastModifiedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional category for this collection (auto-assigned or user-selected)
    /// </summary>
    public int? CategoryId { get; set; }
    public virtual Category? Category { get; set; }

    // Many-to-Many relationship with Model3D
    public virtual ICollection<Model3D> Models { get; set; } = new List<Model3D>();

    // Computed properties (not mapped to database)
    [NotMapped]
    public int ModelCount => Models?.Count ?? 0;

    [NotMapped]
    public long TotalFileSize => Models?.Sum(m => m.FileSize) ?? 0;

    [NotMapped]
    public string TotalFileSizeFormatted => FormatFileSize(TotalFileSize);

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}
