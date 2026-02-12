using PrintVault3D.Models;

namespace PrintVault3D.Services;

/// <summary>
/// Event args for thumbnail processing progress.
/// </summary>
public class ThumbnailProgressEventArgs : EventArgs
{
    public int ModelId { get; }
    public string ModelName { get; }
    public bool Success { get; }
    public string? Error { get; }
    public string? ThumbnailPath { get; }

    public ThumbnailProgressEventArgs(int modelId, string modelName, bool success, string? thumbnailPath = null, string? error = null)
    {
        ModelId = modelId;
        ModelName = modelName;
        Success = success;
        ThumbnailPath = thumbnailPath;
        Error = error;
    }
}

/// <summary>
/// Service interface for background thumbnail processing.
/// </summary>
public interface IThumbnailProcessingService : IDisposable
{
    /// <summary>
    /// Event raised when a thumbnail is processed (success or failure).
    /// </summary>
    event EventHandler<ThumbnailProgressEventArgs>? ThumbnailProcessed;

    /// <summary>
    /// Gets the number of items in the processing queue.
    /// </summary>
    int QueueCount { get; }

    /// <summary>
    /// Gets whether the service is currently processing.
    /// </summary>
    bool IsProcessing { get; }

    /// <summary>
    /// Queues a model for thumbnail generation.
    /// </summary>
    void QueueModel(Model3D model);

    /// <summary>
    /// Starts processing the queue.
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Stops processing.
    /// </summary>
    void Stop();

    /// <summary>
    /// Processes all models that don't have thumbnails.
    /// </summary>
    Task ProcessPendingModelsAsync();
}

