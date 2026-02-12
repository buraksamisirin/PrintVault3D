using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PrintVault3D.Services
{
    public class ArchiveService : IArchiveService
    {
        private readonly ILogger<ArchiveService>? _logger;
        private readonly string[] _supportedExtensions = { ".stl", ".obj", ".3mf", ".gcode", ".bgcode", ".ply", ".off" };
        
    // Security: Maximum file size limit for extracted files (100MB per file)
        private const long MaxExtractedFileSize = 100 * 1024 * 1024;
        // Security: Maximum total extraction size (1GB)
        private const long MaxTotalExtractionSize = 1024 * 1024 * 1024;
  // Security: Maximum number of files to extract
        private const int MaxFileCount = 1000;

     public ArchiveService(ILogger<ArchiveService>? logger = null)
        {
       _logger = logger;
        }

      public bool IsArchive(string filePath)
        {
       return Path.GetExtension(filePath).Equals(".zip", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<List<string>> ExtractAndFilterAsync(string archivePath, string destinationFolder)
        {
            var extractedFiles = new List<string>();

            if (!File.Exists(archivePath)) return extractedFiles;

 try
            {
   // Security: Normalize and validate destination folder
       var normalizedDestination = Path.GetFullPath(destinationFolder);
            if (!Directory.Exists(normalizedDestination))
      {
      Directory.CreateDirectory(normalizedDestination);
                }

              await Task.Run(() =>
       {
           long totalExtractedSize = 0;
   int fileCount = 0;

       using (var archive = ZipFile.OpenRead(archivePath))
         {
        foreach (var entry in archive.Entries)
 {
            // Skip directories and empty entries
             if (string.IsNullOrEmpty(entry.Name)) continue;

    // Security: Check file count limit
        if (fileCount >= MaxFileCount)
    {
           _logger?.LogWarning("Maximum file count ({MaxCount}) reached for archive: {Path}", MaxFileCount, archivePath);
 break;
      }

       // Security: Check individual file size
        if (entry.Length > MaxExtractedFileSize)
    {
  _logger?.LogWarning("Skipping oversized file in archive: {Entry} ({Size} bytes)", entry.FullName, entry.Length);
   continue;
 }

      // Security: Check total extraction size (ZIP bomb protection)
      if (totalExtractedSize + entry.Length > MaxTotalExtractionSize)
         {
     _logger?.LogWarning("Total extraction size limit reached for archive: {Path}", archivePath);
       break;
   }

              var ext = Path.GetExtension(entry.Name).ToLowerInvariant();
       if (_supportedExtensions.Contains(ext))
  {
          // Security: Sanitize filename - remove path components and invalid chars
        var safeName = SanitizeFileName(entry.Name);
 if (string.IsNullOrEmpty(safeName))
        {
   _logger?.LogWarning("Skipping entry with invalid filename: {Entry}", entry.FullName);
     continue;
       }

      // Security: Build full path and validate it's within destination
            var fullDestPath = Path.GetFullPath(Path.Combine(normalizedDestination, safeName));
      
   // CRITICAL: Path traversal protection
         if (!fullDestPath.StartsWith(normalizedDestination + Path.DirectorySeparatorChar) &&
    !fullDestPath.Equals(normalizedDestination, StringComparison.OrdinalIgnoreCase))
       {
         _logger?.LogWarning("Blocked path traversal attempt: {Entry} -> {FullPath}", entry.FullName, fullDestPath);
            continue;
       }

        // Handle duplicate names
        fullDestPath = GetUniqueFilePath(fullDestPath);

     try
   {
    entry.ExtractToFile(fullDestPath);
                    extractedFiles.Add(fullDestPath);
 totalExtractedSize += entry.Length;
       fileCount++;
  }
         catch (Exception ex)
       {
    _logger?.LogWarning(ex, "Failed to extract file: {Entry}", entry.FullName);
  }
    }
   }
          }
     });
}
    catch (InvalidDataException ex)
 {
            _logger?.LogError(ex, "Invalid or corrupted archive: {Path}", archivePath);
   }
            catch (Exception ex)
      {
 _logger?.LogError(ex, "Failed to extract archive: {Path}", archivePath);
 }

     return extractedFiles;
      }

        /// <summary>
  /// Sanitizes a filename by removing path components and invalid characters.
        /// </summary>
        private static string SanitizeFileName(string fileName)
        {
 if (string.IsNullOrWhiteSpace(fileName))
       return string.Empty;

      // Get only the filename, not any path components
         var name = Path.GetFileName(fileName);
 
        if (string.IsNullOrWhiteSpace(name))
        return string.Empty;

            // Remove invalid filename characters
         var invalidChars = Path.GetInvalidFileNameChars();
    foreach (var c in invalidChars)
       {
                name = name.Replace(c, '_');
            }

          // Remove leading/trailing dots and spaces (Windows restriction)
    name = name.Trim('.', ' ');

        // Limit filename length
    if (name.Length > 200)
        {
    var ext = Path.GetExtension(name);
     var baseName = Path.GetFileNameWithoutExtension(name);
  name = baseName.Substring(0, 200 - ext.Length) + ext;
      }

 return name;
        }

        /// <summary>
        /// Gets a unique file path by appending a counter if file exists.
        /// </summary>
        private static string GetUniqueFilePath(string filePath)
        {
   if (!File.Exists(filePath))
              return filePath;

            var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
       var fileName = Path.GetFileNameWithoutExtension(filePath);
   var extension = Path.GetExtension(filePath);
         
      int counter = 1;
string newPath;
       do
          {
           newPath = Path.Combine(directory, $"{fileName}_{counter}{extension}");
      counter++;
        } while (File.Exists(newPath) && counter < 10000);

          return newPath;
        }
    }
}
