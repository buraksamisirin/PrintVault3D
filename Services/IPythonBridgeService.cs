using PrintVault3D.Models;

namespace PrintVault3D.Services;

/// <summary>
/// Result of STL thumbnail generation.
/// </summary>
public class ThumbnailResult
{
    public bool Success { get; set; }
    public string? OutputPath { get; set; }
    public string? Error { get; set; }
    public StlMetadata? Metadata { get; set; }
}

/// <summary>
/// STL file metadata extracted by Python.
/// </summary>
public class StlMetadata
{
    public double DimensionX { get; set; }
    public double DimensionY { get; set; }
    public double DimensionZ { get; set; }
    public double? Volume { get; set; }
    public int Triangles { get; set; }
}

/// <summary>
/// Result of G-code parsing.
/// </summary>
public class GcodeParseResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? SlicerName { get; set; }
    public string? SlicerVersion { get; set; }
    public int? PrintTimeSeconds { get; set; }
    public string? PrintTimeFormatted { get; set; }
    public double? FilamentUsedMm { get; set; }
    public double? FilamentUsedM { get; set; }
    public double? FilamentUsedG { get; set; }
    public double? LayerHeight { get; set; }
    public int? InfillPercentage { get; set; }
    public int? NozzleTemp { get; set; }
    public int? BedTemp { get; set; }
}

/// <summary>
/// Represents a single thumbnail generation job for batch processing.
/// </summary>
public class ThumbnailJob
{
    public string InputPath { get; set; } = string.Empty;
    public string OutputPath { get; set; } = string.Empty;
    public int ModelId { get; set; }
}

/// <summary>
/// Result of batch thumbnail generation.
/// </summary>
public class BatchThumbnailResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public int TotalCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<ThumbnailResult> Results { get; set; } = new();
}

/// <summary>
/// Service interface for calling Python scripts.
/// </summary>
public interface IPythonBridgeService
{
    /// <summary>
    /// Checks if Python is available and properly configured.
    /// </summary>
    Task<bool> IsPythonAvailableAsync();

    /// <summary>
    /// Installs required Python packages.
    /// </summary>
    Task<bool> InstallDependenciesAsync();

    /// <summary>
    /// Generates a thumbnail for an STL file.
    /// </summary>
    Task<ThumbnailResult> GenerateThumbnailAsync(string stlPath, string outputPath, int size = 256, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates thumbnails for multiple STL files in a single batch (faster).
    /// </summary>
    Task<BatchThumbnailResult> GenerateThumbnailsBatchAsync(IEnumerable<ThumbnailJob> jobs, int size = 256, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates thumbnails in batch but streams results as they complete via callback.
    /// Values are provided in real-time.
    /// </summary>
    Task<BatchThumbnailResult> GenerateThumbnailsBatchStreamAsync(
        IEnumerable<ThumbnailJob> jobs, 
        Action<ThumbnailResult> onResult,
        int size = 256, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses a G-code file to extract metadata.
    /// </summary>
    Task<GcodeParseResult> ParseGcodeAsync(string gcodePath);

    /// <summary>
    /// Gets the path to the Python scripts directory.
    /// </summary>
    string ScriptsPath { get; }
}


