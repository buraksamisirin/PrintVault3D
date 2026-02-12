using System.IO;
using System.Text.RegularExpressions;

namespace PrintVault3D.Services;

/// <summary>
/// Validation result for file operations.
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; }
    public string? ErrorMessage { get; }

private ValidationResult(bool isValid, string? errorMessage = null)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public static ValidationResult Valid() => new(true);
    public static ValidationResult Invalid(string errorMessage) => new(false, errorMessage);
}

/// <summary>
/// Helper methods for validating file paths and sizes.
/// </summary>
public static class ValidationHelpers
{
    private static readonly string[] AllowedExtensions = { ".stl", ".3mf", ".gcode", ".gco", ".g", ".zip" };
    private const long MaxFileSize = 500 * 1024 * 1024; // 500 MB
    private const long MinFileSize = 1; // 1 byte
    private const int MaxPathLength = 260; // Windows MAX_PATH
    private const int MaxFileNameLength = 200;

    // Security: Regex to detect suspicious patterns (compiled for performance)
    private static readonly Regex SuspiciousPatternRegex = new(
        @"[\x00-\x1F]|\.\.[\\/]|^(con|prn|aux|nul|com[1-9]|lpt[1-9])(\..*)?$",
  RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
  /// Validates a file path for import operations.
    /// </summary>
    public static ValidationResult ValidateFilePath(string filePath)
    {
  // Null or empty check
        if (string.IsNullOrWhiteSpace(filePath))
      return ValidationResult.Invalid("File path cannot be empty");

  try
        {
// Security: Check for null bytes and control characters
            if (SuspiciousPatternRegex.IsMatch(filePath))
    return ValidationResult.Invalid("File path contains invalid characters");

        // Security: Path length check before processing
    if (filePath.Length > MaxPathLength)
  return ValidationResult.Invalid("File path is too long");

      // Get full path (this also validates the path format)
var fullPath = Path.GetFullPath(filePath);

            // Security: Path traversal check (multiple methods)
        if (filePath.Contains("..") ||
    !fullPath.Equals(Path.GetFullPath(fullPath), StringComparison.OrdinalIgnoreCase))
       return ValidationResult.Invalid("Path traversal is not allowed");

         // Security: Check for alternate data streams (NTFS ADS attack)
  if (filePath.Contains(':') && filePath.IndexOf(':') != 1)
        return ValidationResult.Invalid("Alternate data streams are not allowed");

     // File exists check
            if (!File.Exists(fullPath))
         return ValidationResult.Invalid("File does not exist");

 // Filename length check
         var fileName = Path.GetFileName(fullPath);
   if (fileName.Length > MaxFileNameLength)
    return ValidationResult.Invalid($"Filename is too long (max {MaxFileNameLength} characters)");

            // Extension validation
            var extension = Path.GetExtension(fullPath).ToLowerInvariant();
     if (!AllowedExtensions.Contains(extension))
       return ValidationResult.Invalid($"Invalid file extension '{extension}'. Allowed: {string.Join(", ", AllowedExtensions)}");

            // File size validation
  var fileInfo = new FileInfo(fullPath);

 if (fileInfo.Length < MinFileSize)
      return ValidationResult.Invalid("File is empty or corrupted");

       if (fileInfo.Length > MaxFileSize)
          {
                var sizeMB = fileInfo.Length / 1024.0 / 1024.0;
   return ValidationResult.Invalid($"File is too large ({sizeMB:F1} MB). Maximum allowed size is {MaxFileSize / 1024 / 1024} MB");
    }

        // Security: Check file attributes for potential issues
   if ((fileInfo.Attributes & FileAttributes.System) != 0 ||
  (fileInfo.Attributes & FileAttributes.Hidden) != 0)
      {
   return ValidationResult.Invalid("System or hidden files are not allowed");
          }

return ValidationResult.Valid();
        }
        catch (ArgumentException)
        {
            return ValidationResult.Invalid("Invalid file path format");
        }
        catch (PathTooLongException)
  {
  return ValidationResult.Invalid("File path is too long");
      }
        catch (UnauthorizedAccessException)
     {
            return ValidationResult.Invalid("Access denied to the file");
        }
        catch (Exception ex)
        {
   return ValidationResult.Invalid($"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates if a file extension is allowed for models.
    /// </summary>
    public static bool IsValidModelExtension(string extension)
    {
        return extension.ToLowerInvariant() is ".stl" or ".3mf";
    }

    /// <summary>
    /// Validates if a file extension is allowed for G-code.
    /// </summary>
    public static bool IsValidGcodeExtension(string extension)
    {
        return extension.ToLowerInvariant() is ".gcode" or ".gco" or ".g";
    }

  /// <summary>
    /// Sanitizes a filename by removing potentially dangerous characters.
    /// </summary>
    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
         return "unnamed";

        // Remove path separators and get just the filename
   fileName = Path.GetFileName(fileName);

      // Remove invalid characters
     var invalidChars = Path.GetInvalidFileNameChars();
        foreach (var c in invalidChars)
        {
         fileName = fileName.Replace(c, '_');
        }

      // Remove leading/trailing spaces and dots
      fileName = fileName.Trim('.', ' ');

        // Check for reserved names (Windows)
     var reservedNames = new[] { "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };

        var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName).ToUpperInvariant();
        if (reservedNames.Contains(nameWithoutExt))
   {
       fileName = "_" + fileName;
        }

    // Limit length
        if (fileName.Length > MaxFileNameLength)
        {
         var ext = Path.GetExtension(fileName);
     var name = Path.GetFileNameWithoutExtension(fileName);
 fileName = name.Substring(0, MaxFileNameLength - ext.Length) + ext;
        }

        return string.IsNullOrEmpty(fileName) ? "unnamed" : fileName;
    }
}
