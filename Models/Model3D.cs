using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintVault3D.Models;

/// <summary>
/// Represents a 3D model file (STL or 3MF).
/// </summary>
public class Model3D
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Sanitized display name for UI presentation.
    /// Replaces underscores/plus signs with spaces and removes file extensions.
    /// </summary>
    [NotMapped]
    public string DisplayName
    {
        get
        {
            if (string.IsNullOrEmpty(Name)) return string.Empty;

            // Remove extension if present
            string name = Name;
            string ext = System.IO.Path.GetExtension(name);
            if (!string.IsNullOrEmpty(ext))
            {
                name = System.IO.Path.GetFileNameWithoutExtension(name);
            }

            // Replace common separators with spaces
            name = name.Replace("_", " ")
                       .Replace("+", " ")
                       .Replace("-", " ")
                       .Replace(".", " ");

            // Remove version numbers like v1.0, v2 etc if desired, or keep them.
            // For now just cleaning separators.

            // Title Case
            try 
            {
                // Simple title casing
                var textInfo = System.Globalization.CultureInfo.CurrentCulture.TextInfo;
                name = textInfo.ToTitleCase(name.ToLower());
            }
            catch
            {
                // Fallback if culture fails
            }

            return name.Trim();
        }
    }

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Foreign key to the Category.
    /// </summary>
    public int? CategoryId { get; set; }

    /// <summary>
    /// Source URL extracted from NTFS ADS Zone.Identifier.
    /// </summary>
    [MaxLength(2000)]
    public string? SourceUrl { get; set; }

    /// <summary>
    /// Full path to the STL/3MF file in the Vault directory.
    /// </summary>
    [Required]
    [MaxLength(1000)]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Path to the generated thumbnail PNG.
    /// </summary>
    [MaxLength(1000)]
    public string? ThumbnailPath { get; set; }

    /// <summary>
    /// Original filename before moving to Vault.
    /// </summary>
    [MaxLength(255)]
    public string? OriginalFileName { get; set; }

    /// <summary>
    /// Original source file path (e.g., Downloads folder) for deletion when model is removed.
    /// </summary>
    [MaxLength(1000)]
    public string? SourceFilePath { get; set; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// File type: STL or 3MF.
    /// </summary>
    [MaxLength(10)]
    public string FileType { get; set; } = string.Empty;

    /// <summary>
    /// SHA256 hash of the file content for duplicate detection.
    /// </summary>
    [MaxLength(44)]
    public string? FileHash { get; set; }

    /// <summary>
    /// Date the model was added to the Vault.
    /// </summary>
    public DateTime AddedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date the model was last modified/accessed.
    /// </summary>
    public DateTime? LastModifiedDate { get; set; }

    /// <summary>
    /// Whether thumbnail generation has been completed.
    /// </summary>
    public bool ThumbnailGenerated { get; set; }

    /// <summary>
    /// User-defined tags (comma-separated).
    /// </summary>
    [MaxLength(500)]
    public string? Tags { get; set; }

    /// <summary>
    /// User notes about the model.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Favorite flag for quick access.
    /// </summary>
    public bool IsFavorite { get; set; }

    // Runtime properties (not stored in database)
    
    /// <summary>
    /// Indicates if this model is a duplicate of another model (runtime only).
    /// </summary>
    [NotMapped]
    public bool IsDuplicate { get; set; }

    /// <summary>
    /// Number of duplicate copies of this file (runtime only).
    /// </summary>
    [NotMapped]
    public int DuplicateCount { get; set; }

    // Navigation properties
    [ForeignKey(nameof(CategoryId))]
    public virtual Category? Category { get; set; }

    public virtual ICollection<Gcode> Gcodes { get; set; } = new List<Gcode>();
    public virtual ICollection<Collection> Collections { get; set; } = new List<Collection>();
}

