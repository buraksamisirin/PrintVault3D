using PrintVault3D.Models;

namespace PrintVault3D.Services;

public interface IGcodeParserService
{
    /// <summary>
    /// Parses a G-code file to extract metadata like print time, filament usage, and slicer name.
    /// </summary>
    /// <param name="filePath">Path to the G-code file.</param>
    /// <returns>A GcodeMetadata object containing the extracted info.</returns>
    Task<GcodeMetadata> ParseAsync(string filePath);
}

public class GcodeMetadata
{
    public TimeSpan? PrintTime { get; set; }
    public double? FilamentUsedMm { get; set; }
    public double? FilamentUsedGrams { get; set; }
    public string? SlicerName { get; set; }
    public double? LayerHeight { get; set; }
    public double? NozzleDiameter { get; set; }
    public int? InfillPercentage { get; set; }
    public int? NozzleTemp { get; set; }
    public int? BedTemp { get; set; }
    public string? SlicerVersion { get; set; }
}
