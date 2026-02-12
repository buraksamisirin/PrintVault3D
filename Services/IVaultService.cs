using PrintVault3D.Models;

namespace PrintVault3D.Services;

/// <summary>
/// Result of importing a file into the vault.
/// </summary>
public class ImportResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Model3D? Model { get; set; }
    public Gcode? Gcode { get; set; }
    public bool Skipped { get; set; }
    public string? SourceUrl { get; set; }
}

/// <summary>
/// Service interface for managing the PrintVault file storage.
/// </summary>
public interface IVaultService
{
    /// <summary>
    /// Gets the root path of the vault directory.
    /// </summary>
    string VaultPath { get; }

    /// <summary>
    /// Gets the path for storing model files.
    /// </summary>
    string ModelsPath { get; }

    /// <summary>
    /// Gets the path for storing G-code files.
    /// </summary>
    string GcodesPath { get; }

    /// <summary>
    /// Gets the path for storing thumbnails.
    /// </summary>
    string ThumbnailsPath { get; }

    /// <summary>
    /// Initializes the vault directory structure.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Imports a model file (STL/3MF) into the vault.
    /// </summary>
    Task<ImportResult> ImportModelAsync(string sourceFilePath);

    /// <summary>
    /// Imports a G-code file into the vault.
    /// </summary>
    Task<ImportResult> ImportGcodeAsync(string sourceFilePath);

    /// <summary>
    /// Deletes a model and its associated files from the vault.
    /// </summary>
    Task<bool> DeleteModelAsync(int modelId);

    /// <summary>
    /// Deletes a G-code and its file from the vault.
    /// </summary>
    Task<bool> DeleteGcodeAsync(int gcodeId);

    /// <summary>
    /// Opens the vault folder in Windows Explorer.
    /// </summary>
    void OpenVaultInExplorer();
}

