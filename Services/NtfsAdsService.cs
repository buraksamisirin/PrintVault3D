using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace PrintVault3D.Services;

/// <summary>
/// Service for reading NTFS Alternate Data Streams.
/// Specifically handles Zone.Identifier for extracting download URLs.
/// </summary>
public partial class NtfsAdsService : INtfsAdsService
{
    private const string ZoneIdentifierStream = ":Zone.Identifier";
    private readonly ILogger<NtfsAdsService>? _logger;

    // Security: Allowed URL schemes
    private static readonly HashSet<string> AllowedSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
      "http", "https"
    };

    // Security: Known safe domains for 3D printing files
    private static readonly HashSet<string> TrustedDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "thingiverse.com", "www.thingiverse.com",
        "printables.com", "www.printables.com",
        "myminifactory.com", "www.myminifactory.com",
"cults3d.com", "www.cults3d.com",
   "thangs.com", "www.thangs.com",
  "makerworld.com", "www.makerworld.com",
        "prusaprinters.org", "www.prusaprinters.org",
        "yeggi.com", "www.yeggi.com",
        "stlfinder.com", "www.stlfinder.com",
        "cgtrader.com", "www.cgtrader.com",
        "turbosquid.com", "www.turbosquid.com",
        "github.com", "www.github.com",
        "drive.google.com",
        "dropbox.com", "www.dropbox.com",
"onedrive.live.com"
    };

    // Regex patterns for parsing Zone.Identifier content
    [GeneratedRegex(@"ZoneId=(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex ZoneIdRegex();

    [GeneratedRegex(@"ReferrerUrl=(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex ReferrerUrlRegex();

    [GeneratedRegex(@"HostUrl=(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex HostUrlRegex();

    public NtfsAdsService(ILogger<NtfsAdsService>? logger = null)
    {
        _logger = logger;
    }

 public bool HasZoneIdentifier(string filePath)
    {
     if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return false;

        try
        {
        return TryReadAdsContent(filePath) != null;
   }
        catch (Exception ex)
        {
   _logger?.LogDebug(ex, "Failed to check Zone.Identifier for: {FilePath}", filePath);
    return false;
        }
    }

    public ZoneIdentifierInfo? ReadZoneIdentifier(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
    return null;

        try
        {
    var content = TryReadAdsContent(filePath);
        if (string.IsNullOrEmpty(content))
       return null;

  var info = new ZoneIdentifierInfo
            {
          RawContent = content
        };

            // Parse ZoneId
       var zoneIdMatch = ZoneIdRegex().Match(content);
            if (zoneIdMatch.Success && int.TryParse(zoneIdMatch.Groups[1].Value, out int zoneId))
      {
   info.ZoneId = zoneId;
            }

            // Parse and validate ReferrerUrl
            var referrerMatch = ReferrerUrlRegex().Match(content);
            if (referrerMatch.Success)
          {
          var url = referrerMatch.Groups[1].Value.Trim();
      info.ReferrerUrl = ValidateAndSanitizeUrl(url);
 }

            // Parse and validate HostUrl
    var hostUrlMatch = HostUrlRegex().Match(content);
     if (hostUrlMatch.Success)
 {
        var url = hostUrlMatch.Groups[1].Value.Trim();
  info.HostUrl = ValidateAndSanitizeUrl(url);
            }

  return info;
  }
        catch (Exception ex)
{
          _logger?.LogDebug(ex, "Failed to read Zone.Identifier for: {FilePath}", filePath);
       return null;
        }
    }

    public string? ExtractSourceUrl(string filePath)
    {
        var zoneInfo = ReadZoneIdentifier(filePath);
        if (zoneInfo == null)
            return null;

    // Prefer ReferrerUrl as it usually contains the actual page URL
        // HostUrl is typically the direct download link
        var referrerUrl = zoneInfo.ReferrerUrl;
        var hostUrl = zoneInfo.HostUrl;

        // Check if URLs have meaningful paths (not just domain)
        bool referrerHasPath = HasMeaningfulPath(referrerUrl);
        bool hostHasPath = HasMeaningfulPath(hostUrl);

 // Priority:
 // 1. ReferrerUrl with path (actual page URL)
     // 2. HostUrl with path (download link with path)
  // 3. ReferrerUrl (even if just domain)
        // 4. HostUrl (even if just domain)
        if (referrerHasPath)
          return referrerUrl;
        if (hostHasPath)
   return hostUrl;
        if (!string.IsNullOrEmpty(referrerUrl))
            return referrerUrl;
      
        return hostUrl;
    }

    /// <summary>
    /// Validates and sanitizes a URL for security.
    /// Returns null if URL is invalid or potentially malicious.
    /// </summary>
    private string? ValidateAndSanitizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
 return null;

        try
        {
   // Trim and decode
     url = url.Trim();

            // Try to parse as URI
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
       _logger?.LogDebug("Invalid URL format: {Url}", url);
      return null;
            }

            // Security: Only allow http/https
       if (!AllowedSchemes.Contains(uri.Scheme))
      {
                _logger?.LogWarning("Blocked URL with disallowed scheme: {Scheme}", uri.Scheme);
      return null;
            }

          // Security: Check for IP addresses (potential internal network access)
            if (System.Net.IPAddress.TryParse(uri.Host, out _))
        {
  _logger?.LogWarning("Blocked URL with IP address: {Host}", uri.Host);
    return null;
          }

         // Security: Check for localhost
        if (uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
     uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
             uri.Host.StartsWith("192.168.", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.StartsWith("10.", StringComparison.OrdinalIgnoreCase))
   {
      _logger?.LogWarning("Blocked local/private URL: {Host}", uri.Host);
      return null;
        }

            // Security: Maximum URL length
            if (url.Length > 2048)
   {
         _logger?.LogWarning("Blocked oversized URL: {Length} chars", url.Length);
           return null;
            }

      // Log if domain is not in trusted list (but still allow)
 if (!IsTrustedDomain(uri.Host))
       {
        _logger?.LogDebug("URL from untrusted domain (allowed but logged): {Host}", uri.Host);
    }

       // Return the sanitized URL
 return uri.GetLeftPart(UriPartial.Query);
        }
        catch (Exception ex)
     {
     _logger?.LogDebug(ex, "URL validation failed: {Url}", url);
            return null;
  }
    }

    /// <summary>
    /// Checks if a domain is in the trusted domains list.
  /// </summary>
    private static bool IsTrustedDomain(string host)
{
      if (string.IsNullOrEmpty(host))
  return false;

        // Check exact match
 if (TrustedDomains.Contains(host))
  return true;

        // Check if it's a subdomain of a trusted domain
        foreach (var domain in TrustedDomains)
        {
            if (host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase))
     return true;
 }

        return false;
    }

    /// <summary>
    /// Checks if a URL has a meaningful path beyond just the domain.
    /// </summary>
    private static bool HasMeaningfulPath(string? url)
    {
        if (string.IsNullOrEmpty(url))
         return false;

        try
        {
       var uri = new Uri(url);
    // Check if path is more than just "/" or empty
            return !string.IsNullOrEmpty(uri.AbsolutePath) && 
     uri.AbsolutePath != "/" &&
   uri.AbsolutePath.Length > 1;
        }
        catch
        {
       return false;
        }
    }

    public bool RemoveZoneIdentifier(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
      return false;

        try
        {
         var adsPath = filePath + ZoneIdentifierStream;
     
  // Try to delete the ADS using File.Delete
       // This works because Windows treats ADS paths as regular file paths
  if (File.Exists(adsPath))
{
          File.Delete(adsPath);
      return true;
            }
        return false;
 }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to remove Zone.Identifier for: {FilePath}", filePath);
            return false;
        }
    }

    /// <summary>
    /// Attempts to read the Zone.Identifier ADS content.
    /// </summary>
    private string? TryReadAdsContent(string filePath)
    {
        try
        {
     var adsPath = filePath + ZoneIdentifierStream;
     
   // .NET can read ADS by simply appending the stream name to the path
            if (File.Exists(adsPath))
      {
         return File.ReadAllText(adsPath);
          }

  return null;
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to read ADS content for: {FilePath}", filePath);
            return null;
        }
    }
}
