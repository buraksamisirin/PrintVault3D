using System.IO;

namespace PrintVault3D.Services;

/// <summary>
/// Event args for when a new file is detected.
/// </summary>
public class FileDetectedEventArgs : EventArgs
{
    public string FilePath { get; }
    public string FileName { get; }
    public FileType FileType { get; }
    public DateTime DetectedAt { get; }

    public FileDetectedEventArgs(string filePath, FileType fileType)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        FileType = fileType;
        DetectedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Supported file types for monitoring.
/// </summary>
public enum FileType
{
    STL,
    ThreeMF,  // 3MF
    GCode
}

/// <summary>
/// Service interface for monitoring directories for new 3D printing files.
/// </summary>
public interface IFileWatcherService : IDisposable
{
    /// <summary>
    /// Event raised when a new 3D model file (STL/3MF) is detected.
    /// </summary>
    event EventHandler<FileDetectedEventArgs>? ModelFileDetected;

    /// <summary>
    /// Event raised when a new G-code file is detected.
    /// </summary>
    event EventHandler<FileDetectedEventArgs>? GcodeFileDetected;

    /// <summary>
    /// Event raised when an error occurs during file watching.
    /// </summary>
    event EventHandler<Exception>? ErrorOccurred;

    /// <summary>
    /// Starts monitoring the configured directories.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops monitoring.
    /// </summary>
    void Stop();

    /// <summary>
    /// Gets whether the service is currently monitoring.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Adds a directory to monitor.
    /// </summary>
    void AddWatchDirectory(string path);

    /// <summary>
    /// Removes a directory from monitoring.
    /// </summary>
    void RemoveWatchDirectory(string path);

    /// <summary>
    /// Gets the list of directories being monitored.
    /// </summary>
    IReadOnlyList<string> WatchedDirectories { get; }
}

