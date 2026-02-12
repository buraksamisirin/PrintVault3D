using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrintVault3D.Models;

/// <summary>
/// Represents a G-code file linked to a 3D model.
/// </summary>
public class Gcode
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Foreign key to the parent Model3D.
    /// </summary>
    public int? ModelId { get; set; }

    /// <summary>
    /// Full path to the G-code file.
    /// </summary>
    [Required]
    [MaxLength(1000)]
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// Original filename of the G-code.
    /// </summary>
    [MaxLength(255)]
    public string? OriginalFileName { get; set; }

    /// <summary>
    /// SHA256 hash of the file content for duplicate detection.
    /// </summary>
    [MaxLength(64)]
    public string? FileHash { get; set; }

    /// <summary>
    /// Estimated print time extracted from G-code header.
    /// Stored as TimeSpan ticks for easy querying.
    /// </summary>
    public long? PrintTimeTicks { get; set; }

    /// <summary>
    /// Print time as TimeSpan (computed property).
    /// </summary>
    [NotMapped]
    public TimeSpan? PrintTime
    {
        get => PrintTimeTicks.HasValue ? TimeSpan.FromTicks(PrintTimeTicks.Value) : null;
        set => PrintTimeTicks = value?.Ticks;
    }

    [NotMapped]
    public string? PrintTimeFormatted => PrintTime.HasValue 
        ? $"{(int)PrintTime.Value.TotalHours}h {PrintTime.Value.Minutes}m" 
        : null;

    /// <summary>
    /// Estimated filament weight in grams.
    /// </summary>
    public double? FilamentWeight { get; set; }

    /// <summary>
    /// Estimated filament length in meters.
    /// </summary>
    public double? FilamentLength { get; set; }

    /// <summary>
    /// Slicer software that generated the G-code.
    /// </summary>
    [MaxLength(100)]
    public string? SlicerName { get; set; }

    /// <summary>
    /// Slicer version.
    /// </summary>
    [MaxLength(50)]
    public string? SlicerVersion { get; set; }

    /// <summary>
    /// Layer height setting.
    /// </summary>
    public double? LayerHeight { get; set; }

    /// <summary>
    /// Nozzle diameter setting.
    /// </summary>
    public double? NozzleDiameter { get; set; }

    /// <summary>
    /// Infill percentage.
    /// </summary>
    public int? InfillPercentage { get; set; }

    /// <summary>
    /// Nozzle temperature.
    /// </summary>
    public int? NozzleTemp { get; set; }

    /// <summary>
    /// Bed temperature.
    /// </summary>
    public int? BedTemp { get; set; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Date the G-code was added.
    /// </summary>
    public DateTime AddedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Print status of this GCODE.
    /// </summary>
    public PrintStatus PrintStatus { get; set; } = PrintStatus.NotPrinted;

    /// <summary>
    /// User rating for the print quality (1-5 stars, null if not rated).
    /// </summary>
    public int? Rating { get; set; }

    /// <summary>
    /// Actual print time measured by user.
    /// </summary>
    public long? ActualPrintTimeTicks { get; set; }

    /// <summary>
    /// Actual print time as TimeSpan (computed property).
    /// </summary>
    [NotMapped]
    public TimeSpan? ActualPrintTime
    {
        get => ActualPrintTimeTicks.HasValue ? TimeSpan.FromTicks(ActualPrintTimeTicks.Value) : null;
        set => ActualPrintTimeTicks = value?.Ticks;
    }

    /// <summary>
    /// User notes about this specific G-code.
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Confidence score for auto-linking (0-100).
    /// </summary>
    public int? LinkConfidence { get; set; }

    // Navigation property
    [ForeignKey(nameof(ModelId))]
    public virtual Model3D? Model { get; set; }

    /// <summary>
    /// Calculates estimated cost based on filament weight.
    /// </summary>
    /// <param name="costPerKg">Cost per kilogram of filament.</param>
    /// <returns>Estimated cost or null if weight is unknown.</returns>
    public decimal? CalculateEstimatedCost(decimal costPerKg)
    {
        if (FilamentWeight.HasValue)
        {
            return (decimal)FilamentWeight.Value / 1000m * costPerKg;
        }
        return null;
    }
}

