using System.Collections.Concurrent;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PrintVault3D.Models;
using PrintVault3D.Repositories;

namespace PrintVault3D.Services;

/// <summary>
/// Background service for processing thumbnail generation queue.
/// </summary>
public class ThumbnailProcessingService : IThumbnailProcessingService
{
    private readonly IPythonBridgeService _pythonBridge;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ThumbnailProcessingService>? _logger;
    private readonly ConcurrentQueue<Model3D> _queue = new();
    private CancellationTokenSource _cts = new();
    private readonly string _thumbnailsPath;
    private int _processingCount; // Track items currently being processed
    private Task? _processingTask;
    private bool _isProcessing;
    private bool _disposed;
    private System.Threading.Timer? _watchdogTimer;

    // Security/Performance: Maximum queue size to prevent memory exhaustion
    private const int MaxQueueSize = 5000;

    public event EventHandler<ThumbnailProgressEventArgs>? ThumbnailProcessed;

    public int QueueCount => _queue.Count + _processingCount; // Show total pending + active
    public bool IsProcessing => _isProcessing;

    public ThumbnailProcessingService(IPythonBridgeService pythonBridge, IServiceProvider serviceProvider, ILogger<ThumbnailProcessingService>? logger = null)
    {
        _pythonBridge = pythonBridge;
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Set thumbnails path
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _thumbnailsPath = Path.Combine(appDataPath, "PrintVault3D", "Vault", "Thumbnails");
        Directory.CreateDirectory(_thumbnailsPath);
        
        // Start watchdog timer to ensure processing never gets stuck
        _watchdogTimer = new System.Threading.Timer(WatchdogCallback, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5));
        
        _logger?.LogInformation("ThumbnailProcessingService initialized. Thumbnails path: {Path}", _thumbnailsPath);
    }
    
    private void WatchdogCallback(object? state)
    {
        if (_disposed) return;
        
        // If queue has items but not processing, restart
        if (_queue.Count > 0 && !_isProcessing)
        {
            _logger?.LogWarning("Watchdog: Queue has {Count} items but not processing. Restarting...", _queue.Count);
            _ = StartAsync();
        }
    }

    public void QueueModel(Model3D model)
    {
        if (model.ThumbnailGenerated)
        {
            _logger?.LogDebug("Model already has thumbnail, skipping: {ModelName}", model.Name);
            return;
        }

        // Security: Prevent memory exhaustion
        if (_queue.Count >= MaxQueueSize)
        {
            _logger?.LogWarning("Thumbnail queue full ({Max}), rejecting: {ModelName}", MaxQueueSize, model.Name);
            return;
        }

        _queue.Enqueue(model);
        _logger?.LogInformation("Model queued for thumbnail generation: {ModelName} (Queue size: {QueueSize})", model.Name, _queue.Count);

        if (!_isProcessing)
        {
            _ = StartAsync();
        }
    }

    public async Task StartAsync()
    {
        if (_isProcessing)
        {
            _logger?.LogDebug("Thumbnail processing already running");
            return;
        }

        // Recreate CTS if it was previously cancelled
        if (_cts.IsCancellationRequested)
        {
            _cts.Dispose();
            _cts = new CancellationTokenSource();
        }

        _logger?.LogInformation("Starting thumbnail processing. Queue size: {QueueSize}", _queue.Count);
        _isProcessing = true;
        
        // Run in background but log any exceptions
        _processingTask = Task.Run(async () =>
        {
            try
            {
                await ProcessQueueAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "ProcessQueueAsync crashed unexpectedly");
            }
        });
    }

    public void Stop()
    {
        _isProcessing = false;
        _cts.Cancel();
    }

    public async Task ProcessPendingModelsAsync()
    {
        _logger?.LogInformation("ProcessPendingModelsAsync called. Current queue: {QueueCount}, IsProcessing: {IsProcessing}", _queue.Count, _isProcessing);
        
        using var scope = _serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var pendingModels = await unitOfWork.Models.GetPendingThumbnailsAsync();
        var pendingList = pendingModels.ToList();
        
        _logger?.LogInformation("Found {Count} pending models for thumbnail generation", pendingList.Count);
        
        foreach (var model in pendingList)
        {
            QueueModel(model);
        }

        _logger?.LogInformation("After queuing: QueueCount={QueueCount}, IsProcessing={IsProcessing}", _queue.Count, _isProcessing);

        if (!_isProcessing && _queue.Count > 0)
        {
            _logger?.LogInformation("Starting thumbnail processing from ProcessPendingModelsAsync");
            await StartAsync();
        }
        else if (_isProcessing)
        {
            _logger?.LogInformation("Thumbnail processing already running, skipping StartAsync");
        }
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Check if Python is available
            var pythonAvailable = await _pythonBridge.IsPythonAvailableAsync();
            if (!pythonAvailable)
            {
                _logger?.LogWarning("Python not available, cannot process thumbnails");
                return;
            }

            _logger?.LogInformation("Starting parallel thumbnail processing. Queue size: {QueueSize}", _queue.Count);
            
            // CONCURRENCY SETTINGS
            // Run 4 parallel workers (increased from 3)
            int workerCount = 4; 
            var tasks = new List<Task>();

            for (int i = 0; i < workerCount; i++)
            {
                int workerId = i + 1;
                tasks.Add(Task.Run(() => ProcessBatchWorkerAsync(workerId, cancellationToken), cancellationToken));
            }

            await Task.WhenAll(tasks);

            _logger?.LogInformation("All thumbnail processing workers completed.");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "ProcessQueueAsync failed with unhandled exception");
        }
        finally
        {
            // Reset processing count just in case
            _processingCount = 0;
            // ALWAYS reset the flag
            _isProcessing = false;
            _logger?.LogInformation("Thumbnail processing stopped. _isProcessing set to false.");
        }
    }

    private async Task ProcessBatchWorkerAsync(int workerId, CancellationToken cancellationToken)
    {
        var processedCount = 0;
        // Batch size 8 per worker (Total concurrency = 4 workers * 8 = 32 files)
        var batchSize = 8;
        
        // Local tracker for items finished in the current batch to avoid double-processing or count errors
        var finishedInBatchIds = new System.Collections.Concurrent.ConcurrentDictionary<int, bool>();

        _logger?.LogInformation("Worker {Id} started.", workerId);

        while (!cancellationToken.IsCancellationRequested)
        {
            finishedInBatchIds.Clear();
            
            // Collect a batch of models
            var batch = new List<Model3D>();
            
            // Try to fill the batch
            while (batch.Count < batchSize)
            {
                 if (_queue.TryDequeue(out var model))
                 {
                     // Increment processing count immediately as it leaves the queue
                     Interlocked.Increment(ref _processingCount);
                     
                     if ((model.FileType.Equals("STL", StringComparison.OrdinalIgnoreCase) || 
                          model.FileType.Equals("3MF", StringComparison.OrdinalIgnoreCase)) && 
                          System.IO.File.Exists(model.FilePath))
                     {
                         batch.Add(model);
                     }
                     else
                     {
                         // Mark non-STL or missing files immediately
                         await UpdateModelThumbnailStatusAsync(model.Id, null, true);
                         ThumbnailProcessed?.Invoke(this, new ThumbnailProgressEventArgs(
                             model.Id, model.Name, true, error: "Skipped (non-STL or file missing)"));
                         
                         // Done processing this item
                         Interlocked.Decrement(ref _processingCount);
                     }
                 }
                 else
                 {
                     // Queue empty
                     break;
                 }
            }

            // If no items grabbed, queue is empty, exit worker
            if (batch.Count == 0)
                break;

            _logger?.LogInformation("Worker {Id} processing batch of {Count} models", workerId, batch.Count);

            // Create thumbnail jobs
            var jobs = batch.Select(m => new ThumbnailJob
            {
                ModelId = m.Id,
                InputPath = m.FilePath,
                OutputPath = Path.Combine(_thumbnailsPath, $"{m.Id}_{Guid.NewGuid():N}.png")
            }).ToList();
            
            // Maps for quick lookup in callback
            var outputMap = jobs.ToDictionary(j => j.OutputPath ?? "", j => j);
            var modelMap = batch.ToDictionary(m => m.Id, m => m);

            try
            {
                // Process batch with STREAMING - updates UI immediately as items finish
                var batchResult = await _pythonBridge.GenerateThumbnailsBatchStreamAsync(jobs, 
                    onResult: (result) => 
                    {
                        if (result.OutputPath != null && outputMap.TryGetValue(result.OutputPath, out var job))
                        {
                            if (modelMap.TryGetValue(job.ModelId, out var model))
                            {
                                // Mark as finished for this batch so we don't fallback process it
                                finishedInBatchIds.TryAdd(model.Id, true);
                                
                                // Fire-and-forget update to UI/DB
                                _ = Task.Run(async () => 
                                {
                                    try 
                                    {
                                        if (result.Success && result.OutputPath != null && System.IO.File.Exists(result.OutputPath))
                                        {
                                            await UpdateModelThumbnailStatusAsync(model.Id, result.OutputPath, true);
                                            
                                            ThumbnailProcessed?.Invoke(this, new ThumbnailProgressEventArgs(
                                                model.Id, model.Name, true, result.OutputPath));
                                        }
                                        else
                                        {
                                            var err = result.Error ?? "Unknown error";
                                            _logger?.LogWarning("Worker {Id}: Thumbnail failed for {ModelName}: {Error}", workerId, model.Name, err);
                                            ThumbnailProcessed?.Invoke(this, new ThumbnailProgressEventArgs(
                                                model.Id, model.Name, false, error: err));
                                        }
                                    }
                                    catch (Exception ex) 
                                    {
                                        _logger?.LogError(ex, "Error processing streaming result for {ModelId}", model.Id);
                                    }
                                    finally
                                    {
                                        // Item finished fully
                                        Interlocked.Decrement(ref _processingCount);
                                    }
                                });
                            }
                        }
                    },
                    256, cancellationToken);
                
                // Track total processed for this worker
                processedCount += batchResult.SuccessCount;

                // Log final result
                if (!batchResult.Success)
                {
                    _logger?.LogWarning("Worker {Id} batch failed/incomplete: {Error}", workerId, batchResult.Error);
                }
                
                // Fallback: Check for any items that were NOT processed (e.g. if script crashed midway)
                foreach (var model in batch)
                {
                    // If not in finished list, it wasn't processed via stream
                    if (!finishedInBatchIds.ContainsKey(model.Id))
                    {
                        _logger?.LogWarning("Worker {Id}: Item {ModelName} missed in stream (likely script crash). Retrying individually.", workerId, model.Name);
                        await ProcessModelAsync(model, cancellationToken);
                        
                        // Decrement count now that retry is done
                        Interlocked.Decrement(ref _processingCount);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Worker {Id} batch crashed. Falling back to single file for this batch.", workerId);
                
                // Fallback: Process each model individually
                foreach (var model in batch)
                {
                    if (!finishedInBatchIds.ContainsKey(model.Id))
                    {
                        await ProcessModelAsync(model, cancellationToken);
                        Interlocked.Decrement(ref _processingCount);
                    }
                }
            }
        }
        
        _logger?.LogInformation("Worker {Id} finished. Processed {Count} items.", workerId, processedCount);
    }

    private async Task ProcessModelAsync(Model3D model, CancellationToken cancellationToken)
    {
        _logger?.LogInformation("Processing thumbnail for: {ModelName} ({FileType})", model.Name, model.FileType);
        
        // Allow STL and 3MF
        if (!model.FileType.Equals("STL", StringComparison.OrdinalIgnoreCase) && 
            !model.FileType.Equals("3MF", StringComparison.OrdinalIgnoreCase))
        {
            _logger?.LogWarning("Skipping unsupported file: {ModelName} ({FileType})", model.Name, model.FileType);
            // Mark as processed but without thumbnail
            await UpdateModelThumbnailStatusAsync(model.Id, null, true);
            ThumbnailProcessed?.Invoke(this, new ThumbnailProgressEventArgs(
                model.Id, model.Name, true, error: "Thumbnail generation not supported for this file type"));
            return;
        }

        if (!File.Exists(model.FilePath))
        {
            _logger?.LogWarning("Model file not found: {FilePath}", model.FilePath);
            ThumbnailProcessed?.Invoke(this, new ThumbnailProgressEventArgs(
                model.Id, model.Name, false, error: "Model file not found"));
            return;
        }

        // Generate unique thumbnail filename
        var thumbnailFileName = $"{model.Id}_{Guid.NewGuid():N}.png";
        var thumbnailPath = Path.Combine(_thumbnailsPath, thumbnailFileName);

        // Call Python script
        _logger?.LogDebug("Calling Python script for thumbnail generation: {ModelPath} -> {ThumbnailPath}", model.FilePath, thumbnailPath);
        var result = await _pythonBridge.GenerateThumbnailAsync(model.FilePath, thumbnailPath, 256, cancellationToken);

        if (result.Success && File.Exists(thumbnailPath))
        {
            _logger?.LogInformation("Thumbnail generated successfully: {ModelName} -> {ThumbnailPath}", model.Name, thumbnailPath);
            // Update database
            await UpdateModelThumbnailStatusAsync(model.Id, thumbnailPath, true);

            ThumbnailProcessed?.Invoke(this, new ThumbnailProgressEventArgs(
                model.Id, model.Name, true, thumbnailPath));
        }
        else
        {
            _logger?.LogError("Thumbnail generation failed for {ModelName}: {Error}", model.Name, result.Error ?? "Unknown error");
            ThumbnailProcessed?.Invoke(this, new ThumbnailProgressEventArgs(
                model.Id, model.Name, false, error: result.Error ?? "Unknown error"));
        }
    }

    private async Task UpdateModelThumbnailStatusAsync(int modelId, string? thumbnailPath, bool generated)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var model = await unitOfWork.Models.GetByIdAsync(modelId);
            if (model != null)
            {
                model.ThumbnailPath = thumbnailPath;
                model.ThumbnailGenerated = generated;
                await unitOfWork.Models.UpdateAsync(model);
                await unitOfWork.SaveChangesAsync();
                _logger?.LogDebug("Updated thumbnail status for model {ModelId}: Generated={Generated}", modelId, generated);
            }
            else
            {
                _logger?.LogWarning("Model {ModelId} not found for thumbnail status update", modelId);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to update thumbnail status for model {ModelId}", modelId);
            // Don't rethrow - let the processing loop continue
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Stop();
        _watchdogTimer?.Dispose();
        _watchdogTimer = null;
        _cts.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

