namespace PrintVault3D.Services;

/// <summary>
/// Result of reading Zone.Identifier ADS.
/// </summary>
public class ZoneIdentifierInfo
{
    /// <summary>
    /// The zone ID (0-4, where 3 = Internet).
    /// </summary>
    public int ZoneId { get; set; }

    /// <summary>
    /// The URL the file was downloaded from.
    /// </summary>
    public string? ReferrerUrl { get; set; }

    /// <summary>
    /// The host URL (usually the download page).
    /// </summary>
    public string? HostUrl { get; set; }

    /// <summary>
    /// Raw content of the Zone.Identifier stream.
    /// </summary>
    public string RawContent { get; set; } = string.Empty;
}

/// <summary>
/// Service interface for reading NTFS Alternate Data Streams.
/// Used to extract download source URLs from Zone.Identifier.
/// </summary>
public interface INtfsAdsService
{
    /// <summary>
    /// Checks if a file has a Zone.Identifier ADS.
    /// </summary>
    bool HasZoneIdentifier(string filePath);

    /// <summary>
    /// Reads the Zone.Identifier ADS from a file.
    /// </summary>
    ZoneIdentifierInfo? ReadZoneIdentifier(string filePath);

    /// <summary>
    /// Extracts the source URL from a downloaded file.
    /// Returns null if no URL information is available.
    /// </summary>
    string? ExtractSourceUrl(string filePath);

    /// <summary>
    /// Removes the Zone.Identifier ADS from a file (unblocks it).
    /// </summary>
    bool RemoveZoneIdentifier(string filePath);
}

