using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintVault3D.Models;

/// <summary>
/// Represents a category for organizing 3D models.
/// </summary>
public class Category
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Keywords used for auto-categorization (comma-separated).
    /// Example: "benchy,calibration,test"
    /// </summary>
    [MaxLength(1000)]
    public string? AutoKeywords { get; set; }

    // Navigation property
    public virtual ICollection<Model3D> Models { get; set; } = new List<Model3D>();

    [NotMapped]
    public int ModelCount => Models?.Count ?? 0;
}

