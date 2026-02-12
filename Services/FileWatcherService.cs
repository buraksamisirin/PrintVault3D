using System.Collections.Concurrent;
using System.IO;
using Microsoft.Extensions.Logging;

namespace PrintVault3D.Services;

/// <summary>
/// FileSystemWatcher-based service for monitoring directories for new 3D printing files.
/// </summary>
public class FileWatcherService : IFileWatcherService
{
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly List<string> _watchedDirectories = new();
    private readonly ConcurrentDictionary<string, DateTime> _processingFiles = new();
    private readonly object _lock = new();
    private readonly ILogger<FileWatcherService>? _logger;
    private readonly System.Threading.Timer _cleanupTimer;
    private bool _isRunning;
    private bool _disposed;
    
    // Cleanup interval for processed files cache
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan FileEntryMaxAge = TimeSpan.FromMinutes(3);
    
    // Security: Maximum entries to prevent memory exhaustion
    private const int MaxProcessingEntries = 1000;

    // File extensions to monitor
    private static readonly string[] ModelExtensions = { ".stl", ".3mf" };
    private static readonly string[] GcodeExtensions = { ".gcode", ".gco", ".g" };

    // Delay before processing file (to ensure write is complete)
    private const int ProcessingDelayMs = 1000;

    public event EventHandler<FileDetectedEventArgs>? ModelFileDetected;
    public event EventHandler<FileDetectedEventArgs>? GcodeFileDetected;
    public event EventHandler<Exception>? ErrorOccurred;

    public bool IsRunning => _isRunning;
    public IReadOnlyList<string> WatchedDirectories => _watchedDirectories.AsReadOnly();

    public FileWatcherService(ILogger<FileWatcherService>? logger = null)
    {
        _logger = logger;
        
        // Add default Downloads folder
        var downloadsPath = GetDownloadsFolder();
        if (!string.IsNullOrEmpty(downloadsPath) && Directory.Exists(downloadsPath))
        {
            _watchedDirectories.Add(downloadsPath);
            _logger?.LogInformation("Added default Downloads folder to watch: {Path}", downloadsPath);
        }
        
        // Initialize cleanup timer to prevent memory leak from _processingFiles
        _cleanupTimer = new System.Threading.Timer(CleanupOldEntries, null, CleanupInterval, CleanupInterval);
        _logger?.LogDebug("Cleanup timer initialized with interval: {Interval}", CleanupInterval);
    }
    
    /// <summary>
    /// Cleans up old entries from the processing files dictionary to prevent memory leaks.
    /// </summary>
    private void CleanupOldEntries(object? state)
    {
        var threshold = DateTime.UtcNow - FileEntryMaxAge;
        var oldEntries = _processingFiles
            .Where(kv => kv.Value < threshold)
            .Select(kv => kv.Key)
            .ToList();
        
        foreach (var key in oldEntries)
        {
            _processingFiles.TryRemove(key, out _);
        }
        
        if (oldEntries.Count > 0)
        {
            _logger?.LogDebug("Cleaned up {Count} old entries from processing files cache", oldEntries.Count);
        }
    }

    public void Start()
    {
        if (_isRunning)
        {
            _logger?.LogWarning("File watcher is already running");
            return;
        }

        lock (_lock)
        {
            foreach (var directory in _watchedDirectories)
            {
                CreateWatcher(directory);
            }
            _isRunning = true;
            _logger?.LogInformation("File watcher started. Monitoring {Count} directories", _watchedDirectories.Count);
        }
    }

    public void Stop()
    {
        if (!_isRunning)
        {
            _logger?.LogWarning("File watcher is not running");
            return;
        }

        lock (_lock)
        {
            foreach (var watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            _watchers.Clear();
            _isRunning = false;
            _logger?.LogInformation("File watcher stopped");
        }
    }

    public void AddWatchDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return;

        lock (_lock)
        {
            if (_watchedDirectories.Contains(path, StringComparer.OrdinalIgnoreCase))
                return;

            _watchedDirectories.Add(path);

            if (_isRunning)
            {
                CreateWatcher(path);
            }
        }
    }

    public void RemoveWatchDirectory(string path)
    {
        lock (_lock)
        {
            _watchedDirectories.RemoveAll(d => 
                d.Equals(path, StringComparison.OrdinalIgnoreCase));

            var watcherToRemove = _watchers.FirstOrDefault(w => 
                w.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

            if (watcherToRemove != null)
            {
                watcherToRemove.EnableRaisingEvents = false;
                watcherToRemove.Dispose();
                _watchers.Remove(watcherToRemove);
            }
        }
    }

    private void CreateWatcher(string directory)
    {
        try
        {
            var watcher = new FileSystemWatcher(directory)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.LastWrite,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };

            // Watch for all relevant extensions
            watcher.Created += OnFileCreated;
            watcher.Renamed += OnFileRenamed;
            watcher.Error += OnWatcherError;

            _watchers.Add(watcher);
            _logger?.LogInformation("Created file watcher for directory: {Directory}", directory);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create file watcher for directory: {Directory}", directory);
            ErrorOccurred?.Invoke(this, ex);
        }
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        // Fire-and-forget pattern with proper error handling
        _ = ProcessFileAsync(e.FullPath).ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                _logger?.LogError(task.Exception?.GetBaseException(), 
                    "Error processing file created event: {FilePath}", e.FullPath);
                ErrorOccurred?.Invoke(this, task.Exception?.GetBaseException() ?? new Exception("Unknown error"));
            }
        }, TaskScheduler.Default);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        // Fire-and-forget pattern with proper error handling
        _ = ProcessFileAsync(e.FullPath).ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                _logger?.LogError(task.Exception?.GetBaseException(), 
                    "Error processing file renamed event: {FilePath}", e.FullPath);
                ErrorOccurred?.Invoke(this, task.Exception?.GetBaseException() ?? new Exception("Unknown error"));
            }
        }, TaskScheduler.Default);
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        var exception = e.GetException();
        _logger?.LogError(exception, "File watcher error occurred");
        ErrorOccurred?.Invoke(this, exception);
    }

    private async Task ProcessFileAsync(string filePath)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger?.LogWarning("ProcessFileAsync called with null or empty file path");
            return;
        }

        try
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            // Check if this is a file type we care about
            var isModel = ModelExtensions.Contains(extension);
            var isGcode = GcodeExtensions.Contains(extension);

            if (!isModel && !isGcode)
                return;

            // Security: Prevent memory exhaustion by limiting cache size
            if (_processingFiles.Count >= MaxProcessingEntries)
            {
                _logger?.LogWarning("Processing files cache limit reached ({Max}), forcing cleanup", MaxProcessingEntries);
                CleanupOldEntries(null);
            }

            // Avoid processing the same file multiple times
            if (!_processingFiles.TryAdd(filePath, DateTime.UtcNow))
                return;

            try
            {
                // Wait for the file to be fully written
                await WaitForFileReadyAsync(filePath);

                // Determine file type and raise appropriate event
                if (isModel)
                {
                    var fileType = extension == ".stl" ? FileType.STL : FileType.ThreeMF;
                    _logger?.LogInformation("Model file detected: {FileName} ({FileType})", Path.GetFileName(filePath), fileType);
                    ModelFileDetected?.Invoke(this, new FileDetectedEventArgs(filePath, fileType));
                }
                else if (isGcode)
                {
                    _logger?.LogInformation("G-code file detected: {FileName}", Path.GetFileName(filePath));
                    GcodeFileDetected?.Invoke(this, new FileDetectedEventArgs(filePath, FileType.GCode));
                }
            }
            finally
            {
                // Remove from processing after a delay
                _ = Task.Delay(5000).ContinueWith(t => 
                {
                    _processingFiles.TryRemove(filePath, out DateTime _);
                });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing file: {FilePath}", filePath);
            ErrorOccurred?.Invoke(this, ex);
        }
    }

    private static async Task WaitForFileReadyAsync(string filePath, int maxAttempts = 10)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                await Task.Delay(ProcessingDelayMs);

                // Try to open the file exclusively
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.None);
                return; // File is ready
            }
            catch (IOException)
            {
                // File is still being written
                if (i == maxAttempts - 1)
                    throw;
            }
        }
    }

    private string GetDownloadsFolder()
    {
        // Try to get the Downloads folder from Known Folders
        try
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                "Downloads");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to get Downloads folder path");
            return string.Empty;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        Stop();
        _cleanupTimer.Dispose();
        _processingFiles.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
