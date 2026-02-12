using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PrintVault3D.Models;
using PrintVault3D.Repositories;

namespace PrintVault3D.Services;

/// <summary>
/// Service for managing the PrintVault file storage and organization.
/// </summary>
public class VaultService : IVaultService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly INtfsAdsService _ntfsAdsService;
    private readonly IAutoTaggingService _autoTaggingService;
    private readonly ITagLearningService _tagLearningService;
    private readonly IGcodeParserService _gcodeParserService;
    private readonly ILogger<VaultService>? _logger;

    public string VaultPath { get; }
    public string ModelsPath { get; }
    public string GcodesPath { get; }
    public string ThumbnailsPath { get; }

    public VaultService(IUnitOfWork unitOfWork,
                        INtfsAdsService ntfsAdsService,
                        IAutoTaggingService autoTaggingService,
                        ITagLearningService tagLearningService,
                        IGcodeParserService gcodeParserService,
                        ILogger<VaultService>? logger = null)
    {
        _unitOfWork = unitOfWork;
        _ntfsAdsService = ntfsAdsService;
        _autoTaggingService = autoTaggingService;
        _tagLearningService = tagLearningService;
        _gcodeParserService = gcodeParserService;
        _logger = logger;

        // Set up vault paths in LocalAppData
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        VaultPath = Path.Combine(appDataPath, "PrintVault3D", "Vault");
        ModelsPath = Path.Combine(VaultPath, "Models");
        GcodesPath = Path.Combine(VaultPath, "Gcodes");
        ThumbnailsPath = Path.Combine(VaultPath, "Thumbnails");
        
        _logger?.LogInformation("VaultService initialized. Vault path: {VaultPath}", VaultPath);
    }

    public Task InitializeAsync()
    {
        // Create directory structure
        Directory.CreateDirectory(VaultPath);
        Directory.CreateDirectory(ModelsPath);
        Directory.CreateDirectory(GcodesPath);
        Directory.CreateDirectory(ThumbnailsPath);

        return Task.CompletedTask;
    }

    public async Task<ImportResult> ImportModelAsync(string sourceFilePath)
    {
        var result = new ImportResult();
        _logger?.LogInformation("Importing model from: {SourcePath}", sourceFilePath);

        try
        {
            // Validate file path
            var validation = ValidationHelpers.ValidateFilePath(sourceFilePath);
            if (!validation.IsValid)
            {
                _logger?.LogWarning("File validation failed: {Error}", validation.ErrorMessage);
                result.ErrorMessage = validation.ErrorMessage ?? "File validation failed";
                return result;
            }

            var fileName = Path.GetFileName(sourceFilePath);
            var extension = Path.GetExtension(sourceFilePath).ToLowerInvariant();

            // Additional check for model-specific extensions
            if (!ValidationHelpers.IsValidModelExtension(extension))
            {
                _logger?.LogWarning("Invalid model file type: {Extension}", extension);
                result.ErrorMessage = "Invalid file type. Only STL and 3MF files are supported.";
                return result;
            }

            // Calculate file hash for duplicate detection
            var fileHash = await CalculateFileHashAsync(sourceFilePath);
            _logger?.LogDebug("Calculated file hash: {Hash}", fileHash);

            // Check if exact source file is already imported
            var alreadyImported = await _unitOfWork.Models.FirstOrDefaultAsync(m => m.SourceFilePath == sourceFilePath);
            if (alreadyImported != null && alreadyImported.FileHash == fileHash)
            {
                 _logger?.LogInformation("File already imported from source: {Path}", sourceFilePath);
                 result.ErrorMessage = "Bu dosya zaten kütüphanenizde mevcut.";
                 return result; 
            }

            // Check for duplicate by hash
            // Check for duplicate by hash (Log warning but allow import)
            var duplicateByHash = await _unitOfWork.Models.FirstOrDefaultAsync(m => 
                m.FileHash == fileHash);
            if (duplicateByHash != null)
            {
                _logger?.LogInformation("Duplicate file detected (hash match): {ExistingFile}. Skipping import.", duplicateByHash.Name);
                result.Skipped = true;
                result.ErrorMessage = $"Skipped: duplicate of {duplicateByHash.Name}";
                return result;
            }

            // Check for true duplicate: same filename AND same hash content
            var existingModel = await _unitOfWork.Models.FirstOrDefaultAsync(m => 
                m.OriginalFileName == fileName);
            
            if (existingModel != null)
            {
                // Only block if it's an exact duplicate (same content)
                if (existingModel.FileHash == fileHash)
                {
                    _logger?.LogWarning("Exact duplicate file detected (same name and hash): {FileName}", fileName);
                    result.ErrorMessage = "Bu dosya zaten içe aktarılmış (aynı içerik).";
                    return result;
                }
                else
                {
                    // Same name but different content - this is an update, allow import
                    _logger?.LogInformation("File with same name but different content detected: {FileName}. Allowing import as updated version.", fileName);
                }
            }

            // Extract source URL before copying
            result.SourceUrl = _ntfsAdsService.ExtractSourceUrl(sourceFilePath);
            if (!string.IsNullOrEmpty(result.SourceUrl))
            {
                _logger?.LogInformation("Extracted source URL: {SourceUrl}", result.SourceUrl);
            }

            // Generate unique filename to avoid conflicts
            var uniqueFileName = GenerateUniqueFileName(ModelsPath, fileName);
            var destinationPath = Path.Combine(ModelsPath, uniqueFileName);

            // Get file info before copying
            var fileInfo = new FileInfo(sourceFilePath);
            var fileSize = fileInfo.Length;

            // Copy file to vault (keep original)
            File.Copy(sourceFilePath, destinationPath, overwrite: false);
            _logger?.LogInformation("File copied to vault: {DestinationPath}", destinationPath);

            // Find best matching category
            // Try to find category and tags using Learned System first
            Category? category = null;
            string tagsString = string.Empty;
            
            try 
            {
                var learnedSuggestion = await _tagLearningService.GetLearnedSuggestionAsync(fileName);
                if (learnedSuggestion != null && learnedSuggestion.Confidence > 20)
                {
                    if (!string.IsNullOrEmpty(learnedSuggestion.CategoryName))
                    {
                        category = await _unitOfWork.Categories.GetByNameAsync(learnedSuggestion.CategoryName);
                        if (category != null)
                            _logger?.LogInformation("Used learned category: {Category} (Confidence: {Conf}%)", category.Name, learnedSuggestion.Confidence);
                    }
                    
                    if (!string.IsNullOrEmpty(learnedSuggestion.Tags))
                    {
                        tagsString = learnedSuggestion.Tags;
                        _logger?.LogInformation("Used learned tags: {Tags}", tagsString);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to get learned suggestions");
            }

            // Fallback to Category Repository matching if no learned category
            // Fallback to Category Repository matching if no learned category
            if (category == null)
            {
                category = await _unitOfWork.Categories.FindBestMatchAsync(fileName);
                
                // If finding best match failed (returned null), try the SuggestCategory from AutoTaggingService
                if (category == null)
                {
                    var suggestedCatName = _autoTaggingService.SuggestCategory(fileName);
                    if (!string.IsNullOrEmpty(suggestedCatName))
                    {
                        var suggestedCat = await _unitOfWork.Categories.GetByNameAsync(suggestedCatName);
                        if (suggestedCat == null)
                        {
                            // Create new category if it doesn't exist
                            suggestedCat = new Category { Name = suggestedCatName };
                            await _unitOfWork.Categories.AddAsync(suggestedCat);
                            await _unitOfWork.SaveChangesAsync(); // Save to get ID
                            _logger?.LogInformation("Created new category from auto-tags: {CategoryName}", suggestedCatName);
                        }
                        
                        category = suggestedCat;
                        _logger?.LogInformation("Auto-categorized using keywords to: {CategoryName}", category.Name);
                    }
                }
            }

            // Final fallback to Uncategorized if still null
            if (category == null)
            {
                category = await _unitOfWork.Categories.GetByNameAsync("Uncategorized");
                if (category == null)
                {
                    // Create Uncategorized if not exists
                    category = new Category { Name = "Uncategorized" };
                     await _unitOfWork.Categories.AddAsync(category);
                     await _unitOfWork.SaveChangesAsync();
                }
            }

            // Fallback to AutoTaggingService if no learned tags
            if (string.IsNullOrEmpty(tagsString))
            {
                var autoTags = _autoTaggingService.GenerateTags(sourceFilePath);
                tagsString = string.Join(", ", autoTags);
                if (autoTags.Any())
                {
                    _logger?.LogInformation("Auto-generated tags: {Tags}", tagsString);
                }
            }

            // Create database entry
            var model = new Model3D
            {
                Name = Path.GetFileNameWithoutExtension(fileName),
                OriginalFileName = fileName,
                SourceFilePath = sourceFilePath, // Track original location for deletion
                FilePath = destinationPath,
                FileType = extension.TrimStart('.').ToUpperInvariant(),
                FileSize = fileSize,
                FileHash = fileHash,
                SourceUrl = result.SourceUrl,
                CategoryId = category.Id,
                AddedDate = DateTime.UtcNow,
                ThumbnailGenerated = false,
                Tags = tagsString
            };

            await _unitOfWork.Models.AddAsync(model);
            await _unitOfWork.SaveChangesAsync();

            _logger?.LogInformation("Model imported successfully: {ModelName} (ID: {ModelId})", model.Name, model.Id);
            result.Success = true;
            result.Model = model;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error importing model from: {SourcePath}", sourceFilePath);
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public async Task<ImportResult> ImportGcodeAsync(string sourceFilePath)
    {
        var result = new ImportResult();
        _logger?.LogInformation("Importing G-code from: {SourcePath}", sourceFilePath);

        try
        {
            // Validate file path
            var validation = ValidationHelpers.ValidateFilePath(sourceFilePath);
            if (!validation.IsValid)
            {
                _logger?.LogWarning("G-code validation failed: {Error}", validation.ErrorMessage);
                result.ErrorMessage = validation.ErrorMessage ?? "File validation failed";
                return result;
            }

            var fileName = Path.GetFileName(sourceFilePath);
            var extension = Path.GetExtension(sourceFilePath).ToLowerInvariant();

            // Additional check for G-code specific extensions
            if (!ValidationHelpers.IsValidGcodeExtension(extension))
            {
                _logger?.LogWarning("Invalid G-code file type: {Extension}", extension);
                result.ErrorMessage = "Invalid file type. Only G-code files are supported.";
                return result;
            }

            // Calculate file hash for duplicate detection
            var fileHash = await CalculateFileHashAsync(sourceFilePath);
            _logger?.LogDebug("Calculated G-code file hash: {Hash}", fileHash);

            // Check for duplicate by hash
            var duplicateByHash = await _unitOfWork.Gcodes.GetByFileHashAsync(fileHash);
            if (duplicateByHash != null)
            {
                _logger?.LogWarning("Duplicate G-code detected (hash match): {ExistingFile}. Skipping import.", duplicateByHash.OriginalFileName);
                result.ErrorMessage = $"Bu dosya zaten mevcut: {duplicateByHash.OriginalFileName}";
                return result; // Skip duplicate
            }

            // Generate unique filename
            var uniqueFileName = GenerateUniqueFileName(GcodesPath, fileName);
            var destinationPath = Path.Combine(GcodesPath, uniqueFileName);

            // Get file info
            var fileInfo = new FileInfo(sourceFilePath);
            var fileSize = fileInfo.Length;

            // Copy file to vault (keep original)
            File.Copy(sourceFilePath, destinationPath, overwrite: false);

            // Try to find a matching model
            var matches = await _unitOfWork.Gcodes.FindPotentialMatchesAsync(fileName);
            var bestMatch = matches.FirstOrDefault();

            // Parse G-code metadata
            var metadata = await _gcodeParserService.ParseAsync(destinationPath);

            // Create database entry
            var gcode = new Gcode
            {
                OriginalFileName = fileName,
                FilePath = destinationPath,
                FileSize = fileSize,
                ModelId = bestMatch.Model?.Id,
                LinkConfidence = bestMatch.Score,
                AddedDate = DateTime.UtcNow,
                
                // Parsed Metadata
                PrintTime = metadata.PrintTime,
                FilamentLength = metadata.FilamentUsedMm,
                FilamentWeight = metadata.FilamentUsedGrams,
                SlicerName = metadata.SlicerName,
                LayerHeight = metadata.LayerHeight,
                NozzleDiameter = metadata.NozzleDiameter,
                
                // Content Hash
                FileHash = fileHash
            };

            await _unitOfWork.Gcodes.AddAsync(gcode);
            await _unitOfWork.SaveChangesAsync();

            result.Success = true;
            result.Gcode = gcode;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error importing G-code from: {SourcePath}", sourceFilePath);
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public async Task<bool> DeleteModelAsync(int modelId)
    {
        _logger?.LogInformation("Deleting model with ID: {ModelId}", modelId);
        
        try
        {
            var model = await _unitOfWork.Models.GetByIdAsync(modelId);
            if (model == null)
            {
                _logger?.LogWarning("Model not found: {ModelId}", modelId);
                return false;
            }

            // Delete the physical file
            if (File.Exists(model.FilePath))
            {
                File.Delete(model.FilePath);
                _logger?.LogInformation("Deleted model file: {FilePath}", model.FilePath);
            }

            // Delete thumbnail if exists
            if (!string.IsNullOrEmpty(model.ThumbnailPath) && File.Exists(model.ThumbnailPath))
            {
                File.Delete(model.ThumbnailPath);
                _logger?.LogInformation("Deleted thumbnail: {ThumbnailPath}", model.ThumbnailPath);
            }

            // Delete source file from Downloads folder if exists
            if (!string.IsNullOrEmpty(model.SourceFilePath) && 
                File.Exists(model.SourceFilePath) &&
                model.SourceFilePath != model.FilePath) // Don't delete same file twice
            {
                try
                {
                    File.Delete(model.SourceFilePath);
                    _logger?.LogInformation("Deleted source file: {SourceFilePath}", model.SourceFilePath);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Could not delete source file: {SourceFilePath}", model.SourceFilePath);
                    // Continue with deletion even if source file couldn't be deleted
                }
            }

            // Delete from database
            await _unitOfWork.Models.DeleteAsync(model);
            await _unitOfWork.SaveChangesAsync();

            _logger?.LogInformation("Model deleted successfully: {ModelName} (ID: {ModelId})", model.Name, modelId);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting model: {ModelId}", modelId);
            return false;
        }
    }

    public async Task<bool> DeleteGcodeAsync(int gcodeId)
    {
        try
        {
            var gcode = await _unitOfWork.Gcodes.GetByIdAsync(gcodeId);
            if (gcode == null)
                return false;

            // Delete the physical file
            if (File.Exists(gcode.FilePath))
            {
                File.Delete(gcode.FilePath);
            }

            // Delete from database
            await _unitOfWork.Gcodes.DeleteAsync(gcode);
            await _unitOfWork.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting G-code: {GcodeId}", gcodeId);
            return false;
        }
    }

    public void OpenVaultInExplorer()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = VaultPath,
            UseShellExecute = true
        });
    }

    /// <summary>
    /// Calculates SHA256 hash of a file for duplicate detection.
    /// SHA256 is cryptographically secure unlike MD5.
    /// </summary>
    private static async Task<string> CalculateFileHashAsync(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
        var hashBytes = await sha256.ComputeHashAsync(stream);
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Generates a unique filename by appending a number if the file already exists.
    /// Uses file locking to prevent race conditions.
    /// </summary>
    private static string GenerateUniqueFileName(string directory, string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        var uniqueName = fileName;
        var counter = 1;
        const int maxAttempts = 10000;

        // Use a lock file to prevent race conditions
        var lockPath = Path.Combine(directory, ".import_lock");
      
        try
        {
            // Create lock file with exclusive access
            using var lockFile = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
   
            while (File.Exists(Path.Combine(directory, uniqueName)) && counter < maxAttempts)
            {
              uniqueName = $"{name}_{counter}{extension}";
       counter++;
            }

     if (counter >= maxAttempts)
        {
         // Fallback to GUID-based naming
     uniqueName = $"{name}_{Guid.NewGuid():N}{extension}";
   }
        }
        catch (IOException)
        {
      // If lock fails, use GUID to ensure uniqueness
      uniqueName = $"{name}_{Guid.NewGuid():N}{extension}";
 }
        finally
      {
   // Try to clean up lock file
    try { if (File.Exists(lockPath)) File.Delete(lockPath); } catch { }
    }

        return uniqueName;
    }
}

