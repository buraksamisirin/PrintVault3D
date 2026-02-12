using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace PrintVault3D.Services;

/// <summary>
/// Service for calling Python scripts for STL/G-code processing.
/// </summary>
public class PythonBridgeService : IPythonBridgeService
{
    private readonly string _pythonPath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<PythonBridgeService>? _logger;
    
    // Cache for Python availability check
    private bool? _isPythonAvailableCache;
    private DateTime _lastPythonCheck = DateTime.MinValue;
    private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);
    private readonly object _cacheLock = new();

    public string ScriptsPath { get; }

    public PythonBridgeService(ILogger<PythonBridgeService>? logger = null)
    {
        _logger = logger;
        
        // Determine Python path (try python3 first, then python)
        _pythonPath = FindPythonPath();

        // Scripts are in the PythonScripts folder relative to the application
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        ScriptsPath = Path.Combine(appDir, "PythonScripts");

        // If not found in app directory, check the source directory (for development)
        if (!Directory.Exists(ScriptsPath))
        {
            // Go up from bin/Debug/net8.0-windows to find PythonScripts
            var sourceDir = Path.GetFullPath(Path.Combine(appDir, "..", "..", "..", ".."));
            var devScriptsPath = Path.Combine(sourceDir, "PythonScripts");
            if (Directory.Exists(devScriptsPath))
            {
                ScriptsPath = devScriptsPath;
            }
        }

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        _logger?.LogInformation("PythonBridgeService initialized. Python path: {PythonPath}, Scripts path: {ScriptsPath}", 
            string.IsNullOrEmpty(_pythonPath) ? "Not found" : _pythonPath, ScriptsPath);
    }

    public async Task<bool> IsPythonAvailableAsync()
    {
        // Check cache first
        lock (_cacheLock)
        {
            if (_isPythonAvailableCache.HasValue && 
                DateTime.UtcNow - _lastPythonCheck < _cacheDuration)
            {
                _logger?.LogDebug("Returning cached Python availability: {IsAvailable}", _isPythonAvailableCache.Value);
                return _isPythonAvailableCache.Value;
            }
        }

        if (string.IsNullOrEmpty(_pythonPath))
        {
            _logger?.LogWarning("Python path is not configured");
            UpdateCache(false);
            return false;
        }

        try
        {
            var result = await RunCommandAsync(_pythonPath, "--version");
            var isAvailable = result.ExitCode == 0;
            _logger?.LogInformation("Python availability check: {IsAvailable}, Version output: {Output}", isAvailable, result.Output?.Trim());
            
            UpdateCache(isAvailable);
            return isAvailable;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking Python availability");
            
            UpdateCache(false);
            return false;
        }
    }

    /// <summary>
    /// Updates the Python availability cache.
    /// </summary>
    private void UpdateCache(bool isAvailable)
    {
        lock (_cacheLock)
        {
            _isPythonAvailableCache = isAvailable;
            _lastPythonCheck = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Invalidates the Python availability cache, forcing a recheck on next call.
    /// </summary>
    public void InvalidatePythonCache()
    {
        lock (_cacheLock)
        {
            _isPythonAvailableCache = null;
            _lastPythonCheck = DateTime.MinValue;
        }
        _logger?.LogInformation("Python availability cache invalidated");
    }

    public async Task<bool> InstallDependenciesAsync()
    {
        if (string.IsNullOrEmpty(_pythonPath))
            return false;

        try
        {
            var requirementsPath = Path.Combine(ScriptsPath, "requirements.txt");
            if (!File.Exists(requirementsPath))
                return false;

            var result = await RunCommandAsync(_pythonPath, $"-m pip install -r \"{requirementsPath}\" --quiet");
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public async Task<ThumbnailResult> GenerateThumbnailAsync(string stlPath, string outputPath, int size = 256, CancellationToken cancellationToken = default)
    {
        var result = new ThumbnailResult { Success = false };

        try
        {
            if (!await IsPythonAvailableAsync())
            {
                result.Error = "Python is not available. Please install Python 3.10+";
                return result;
            }

            var scriptPath = Path.Combine(ScriptsPath, "stl_thumbnail.py");
            if (!File.Exists(scriptPath))
            {
                result.Error = $"Script not found: {scriptPath}";
                return result;
            }

            var args = $"\"{scriptPath}\" \"{stlPath}\" \"{outputPath}\" {size}";
            // Increased timeout to 180 seconds for large STL files
            var cmdResult = await RunCommandAsync(_pythonPath, args, timeoutSeconds: 180, cancellationToken: cancellationToken);

            if (string.IsNullOrWhiteSpace(cmdResult.Output))
            {
                result.Error = cmdResult.Error ?? "No output from Python script";
                return result;
            }

            // Parse JSON response
            var jsonResult = JsonSerializer.Deserialize<PythonThumbnailResponse>(cmdResult.Output, _jsonOptions);
            
            if (jsonResult == null)
            {
                result.Error = "Failed to parse Python response";
                return result;
            }

            result.Success = jsonResult.Success;
            result.Error = jsonResult.Error;
            result.OutputPath = jsonResult.OutputPath;

            if (jsonResult.Metadata != null)
            {
                result.Metadata = new StlMetadata
                {
                    DimensionX = jsonResult.Metadata.Dimensions?.X ?? 0,
                    DimensionY = jsonResult.Metadata.Dimensions?.Y ?? 0,
                    DimensionZ = jsonResult.Metadata.Dimensions?.Z ?? 0,
                    Volume = jsonResult.Metadata.Volume,
                    Triangles = jsonResult.Metadata.Triangles
                };
            }
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Generate thumbnails for multiple STL files in a single Python process (much faster).
    /// </summary>
    public async Task<BatchThumbnailResult> GenerateThumbnailsBatchAsync(
        IEnumerable<ThumbnailJob> jobs, 
        int size = 256,
        CancellationToken cancellationToken = default)
    {
        var result = new BatchThumbnailResult { Success = false, Results = new List<ThumbnailResult>() };
        var jobsList = jobs.ToList();

        if (jobsList.Count == 0)
        {
            result.Success = true;
            return result;
        }

        try
        {
            if (!await IsPythonAvailableAsync())
            {
                result.Error = "Python is not available.";
                return result;
            }

            // Prefer batch script, fallback to regular script
            var batchScriptPath = Path.Combine(ScriptsPath, "stl_thumbnail_batch.py");
            var scriptPath = File.Exists(batchScriptPath) ? batchScriptPath : Path.Combine(ScriptsPath, "stl_thumbnail.py");

            if (!File.Exists(scriptPath))
            {
                result.Error = $"Script not found: {scriptPath}";
                return result;
            }

            // Create JSON input for batch processing
            var batchInput = new
            {
                jobs = jobsList.Select(j => new
                {
                    input = j.InputPath,
                    output = j.OutputPath,
                    size = size
                }).ToArray()
            };

            var jsonInput = JsonSerializer.Serialize(batchInput, _jsonOptions);
            
            // Use --batch-stdin mode for the batch script
            var args = File.Exists(batchScriptPath) 
                ? $"\"{scriptPath}\" --batch-stdin {Math.Min(Environment.ProcessorCount, 12)}"
                : $"\"{scriptPath}\" \"{jobsList[0].InputPath}\" \"{jobsList[0].OutputPath}\" {size}";

            // For batch mode, we need to use stdin
            if (File.Exists(batchScriptPath))
            {
                // Timeout: 30 seconds per file (was 120+ which caused long stalls)
                var cmdResult = await RunCommandWithInputAsync(_pythonPath, args, jsonInput, 
                    timeoutSeconds: 30 + (jobsList.Count * 10), cancellationToken);

                if (string.IsNullOrWhiteSpace(cmdResult.Output))
                {
                    result.Error = cmdResult.Error ?? "No output from Python script";
                    return result;
                }

                // Parse batch response
                var batchResponse = JsonSerializer.Deserialize<PythonBatchResponse>(cmdResult.Output, _jsonOptions);
                if (batchResponse != null)
                {
                    result.Success = batchResponse.Success;
                    result.TotalCount = batchResponse.Total;
                    result.SuccessCount = batchResponse.Succeeded;
                    result.FailedCount = batchResponse.Failed;

                    if (batchResponse.Results != null)
                    {
                        foreach (var r in batchResponse.Results)
                        {
                            result.Results.Add(new ThumbnailResult
                            {
                                Success = r.Success,
                                Error = r.Error,
                                OutputPath = r.OutputPath
                            });
                        }
                    }
                }
            }
            else
            {
                // Fallback to sequential processing with regular script
                foreach (var job in jobsList)
                {
                    var singleResult = await GenerateThumbnailAsync(job.InputPath, job.OutputPath, size, cancellationToken);
                    result.Results.Add(singleResult);
                    if (singleResult.Success) result.SuccessCount++;
                    else result.FailedCount++;
                }
                result.Success = true;
                result.TotalCount = jobsList.Count;
            }
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    public async Task<BatchThumbnailResult> GenerateThumbnailsBatchStreamAsync(
        IEnumerable<ThumbnailJob> jobs, 
        Action<ThumbnailResult> onResult,
        int size = 256,
        CancellationToken cancellationToken = default)
    {
        var result = new BatchThumbnailResult { Success = false, Results = new List<ThumbnailResult>() };
        var jobsList = jobs.ToList();

        if (jobsList.Count == 0)
        {
            result.Success = true;
            return result;
        }

        try
        {
            if (!await IsPythonAvailableAsync())
            {
                result.Error = "Python is not available.";
                return result;
            }

            var batchScriptPath = Path.Combine(ScriptsPath, "stl_thumbnail_batch.py");
            if (!File.Exists(batchScriptPath))
            {
                // Fallback to non-streaming if batch script not found
                return await GenerateThumbnailsBatchAsync(jobs, size, cancellationToken);
            }

            // Create JSON input for batch processing
            var batchInput = new
            {
                jobs = jobsList.Select(j => new
                {
                    input = j.InputPath,
                    output = j.OutputPath,
                    size = size
                }).ToArray()
            };

            var jsonInput = JsonSerializer.Serialize(batchInput, _jsonOptions);
            
            // Use --batch-stdin --stream mode
            // We use the batch script path explicitly
            var args = $"\"{batchScriptPath}\" --batch-stdin {Math.Min(Environment.ProcessorCount, 12)} --stream";

            // Timeout: 45 seconds base + 15 seconds per file
            int timeoutSeconds = 45 + (jobsList.Count * 15);

            await RunCommandStreamAsync(_pythonPath, args, jsonInput, 
                onLine: (line) => 
                {
                    if (string.IsNullOrWhiteSpace(line)) return;

                    try 
                    {
                        using var doc = JsonDocument.Parse(line);
                        if (doc.RootElement.TryGetProperty("type", out var typeProp))
                        {
                            var type = typeProp.GetString();
                            if (type == "result")
                            {
                                if (doc.RootElement.TryGetProperty("data", out var dataProp))
                                {
                                    var itemResult = dataProp.Deserialize<PythonBatchResultItem>(_jsonOptions);
                                    if (itemResult != null)
                                    {
                                        var thumbResult = new ThumbnailResult 
                                        {
                                            Success = itemResult.Success,
                                            Error = itemResult.Error,
                                            OutputPath = itemResult.OutputPath
                                        };
                                        
                                        // Add to final list
                                        result.Results.Add(thumbResult);
                                        if (thumbResult.Success) result.SuccessCount++;
                                        else result.FailedCount++;
                                        
                                        // Callback!
                                        onResult?.Invoke(thumbResult);
                                    }
                                }
                            }
                            else if (type == "summary")
                            {
                                if (doc.RootElement.TryGetProperty("data", out var dataProp))
                                {
                                    var summary = dataProp.Deserialize<PythonBatchResponse>(_jsonOptions);
                                    if (summary != null)
                                    {
                                        result.Success = summary.Success;
                                        result.TotalCount = summary.Total;
                                    }
                                }
                            }
                        }
                    }
                    catch 
                    {
                        // Ignore parse errors for individual lines (e.g. debug prints)
                    }
                },
                timeoutSeconds, 
                cancellationToken);
                
             // If we processed items but didn't get a strict "summary" (e.g. crashed at end), 
             // calculate success manually based on results
             if (result.Results.Count > 0 && !result.Success)
             {
                 result.Success = true; 
                 result.TotalCount = jobsList.Count;
             }
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    private async Task<CommandResult> RunCommandWithInputAsync(string command, string arguments, string stdin, int timeoutSeconds = 30, CancellationToken cancellationToken = default)
    {
        var result = new CommandResult();

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            // Write to stdin
            await process.StandardInput.WriteAsync(stdin);
            await process.StandardInput.FlushAsync();
            process.StandardInput.Close();

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            var completedTask = Task.Run(() => process.WaitForExit(timeoutSeconds * 1000), cancellationToken);
            var completed = await Task.WhenAny(completedTask, Task.Delay(-1, cancellationToken)) == completedTask && completedTask.Result;

            if (!completed)
            {
                try { process.Kill(); } catch { }
                
                // Try to read whatever output was generated before timeout
                string partialOutput = "Partial output not available";
                string partialError = "";
                
                try 
                {
                    // Give a small moment to streams to flush after kill
                    await Task.WhenAny(outputTask, Task.Delay(100));
                    await Task.WhenAny(errorTask, Task.Delay(100));
                    
                    if (outputTask.IsCompleted) partialOutput = outputTask.Result;
                    if (errorTask.IsCompleted) partialError = errorTask.Result;
                } 
                catch { }

                result.Output = partialOutput;
                result.Error = $"{partialError}\n[Process timed out after {timeoutSeconds}s]";
                result.ExitCode = -1;
                return result;
            }

            result.Output = await outputTask;
            result.Error = await errorTask;
            result.ExitCode = process.ExitCode;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
            result.ExitCode = -1;
        }

        return result;
    }

    private async Task RunCommandStreamAsync(string command, string arguments, string stdin, Action<string> onLine, int timeoutSeconds = 60, CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8,
            StandardErrorEncoding = System.Text.Encoding.UTF8
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        // Write to stdin
        await process.StandardInput.WriteAsync(stdin);
        await process.StandardInput.FlushAsync();
        process.StandardInput.Close();

        // Handle stderr asynchronously to avoid blocking
        process.ErrorDataReceived += (s, e) => { /* We could log error lines here if needed */ };
        process.BeginErrorReadLine();

        // Read stdout line by line
        string? line;
        while ((line = await process.StandardOutput.ReadLineAsync(cancellationToken)) != null)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                onLine(line);
            }
        }

        // Wait for exit
        await process.WaitForExitAsync(cancellationToken).WaitAsync(TimeSpan.FromSeconds(timeoutSeconds), cancellationToken);
    }

    public async Task<GcodeParseResult> ParseGcodeAsync(string gcodePath)
    {
        var result = new GcodeParseResult { Success = false };

        try
        {
            if (!await IsPythonAvailableAsync())
            {
                result.Error = "Python is not available. Please install Python 3.10+";
                return result;
            }

            var scriptPath = Path.Combine(ScriptsPath, "gcode_parser.py");
            if (!File.Exists(scriptPath))
            {
                result.Error = $"Script not found: {scriptPath}";
                return result;
            }

            var args = $"\"{scriptPath}\" \"{gcodePath}\"";
            var cmdResult = await RunCommandAsync(_pythonPath, args, timeoutSeconds: 30);

            if (string.IsNullOrWhiteSpace(cmdResult.Output))
            {
                result.Error = cmdResult.Error ?? "No output from Python script";
                return result;
            }

            // Parse JSON response
            var jsonResult = JsonSerializer.Deserialize<PythonGcodeResponse>(cmdResult.Output, _jsonOptions);
            
            if (jsonResult == null)
            {
                result.Error = "Failed to parse Python response";
                return result;
            }

            result.Success = jsonResult.Success;
            result.Error = jsonResult.Error;
            result.SlicerName = jsonResult.SlicerName;
            result.SlicerVersion = jsonResult.SlicerVersion;
            result.PrintTimeSeconds = jsonResult.PrintTimeSeconds;
            result.PrintTimeFormatted = jsonResult.PrintTimeFormatted;
            result.FilamentUsedMm = jsonResult.FilamentUsedMm;
            result.FilamentUsedM = jsonResult.FilamentUsedM;
            result.FilamentUsedG = jsonResult.FilamentUsedG;
            result.LayerHeight = jsonResult.LayerHeight;
            result.InfillPercentage = jsonResult.InfillPercentage;
            result.NozzleTemp = jsonResult.NozzleTemp;
            result.BedTemp = jsonResult.BedTemp;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    private static string FindPythonPath()
    {
        // Common Python executable names
        var pythonNames = new[] { "python", "python3", "py" };
        
        foreach (var name in pythonNames)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = name,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    process.WaitForExit(5000);
                    if (process.ExitCode == 0)
                    {
                        return name;
                    }
                }
            }
            catch
            {
                // Try next
            }
        }

        // Try common Windows Python installation paths as fallback
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var userName = Environment.UserName;
        
        var appDir = AppDomain.CurrentDomain.BaseDirectory;

        var commonPaths = new[]
        {
            // Try explicit user paths first
            // PRIORITIZE LOCAL EMBEDDED PYTHON
            // Look for a 'Python' folder in the application directory
            Path.Combine(appDir, "Python", "python.exe"),
            Path.Combine(appDir, "python", "python.exe"),
            
            $@"C:\Users\{userName}\AppData\Local\Programs\Python\Python313\python.exe",
            $@"C:\Users\{userName}\AppData\Local\Programs\Python\Python312\python.exe",
            $@"C:\Users\{userName}\AppData\Local\Programs\Python\Python311\python.exe",
            // Then try with Environment folder
            Path.Combine(userProfile, "Programs", "Python", "Python313", "python.exe"),
            Path.Combine(userProfile, "Programs", "Python", "Python312", "python.exe"),
            Path.Combine(userProfile, "Programs", "Python", "Python311", "python.exe"),
            Path.Combine(userProfile, "Programs", "Python", "Python310", "python.exe"),
            Path.Combine(programFiles, "Python313", "python.exe"),
            Path.Combine(programFiles, "Python312", "python.exe"),
            Path.Combine(programFiles, "Python311", "python.exe"),
            Path.Combine(programFiles, "Python310", "python.exe"),
            @"C:\Python313\python.exe",
            @"C:\Python312\python.exe",
            @"C:\Python311\python.exe",
            @"C:\Python310\python.exe",
        };

        foreach (var path in commonPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        return string.Empty;
    }

    private static async Task<CommandResult> RunCommandAsync(string command, string arguments, int timeoutSeconds = 30, CancellationToken cancellationToken = default)
    {
        var result = new CommandResult();

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            var completedTask = Task.Run(() => process.WaitForExit(timeoutSeconds * 1000), cancellationToken);
            var completed = await Task.WhenAny(completedTask, Task.Delay(-1, cancellationToken)) == completedTask && completedTask.Result;

            if (!completed)
            {
                try
                {
                    process.Kill();
                }
                catch (InvalidOperationException)
                {
                    // Process may have already exited
                }
                result.Error = "Process timed out";
                result.ExitCode = -1;
                return result;
            }

            result.Output = await outputTask;
            result.Error = await errorTask;
            result.ExitCode = process.ExitCode;

            // Security: Don't log full output which may contain file paths or user data
        // Only log error messages and exit codes
        }
  catch (Exception ex)
   {
       // Security: Sanitize error message - don't expose full paths
      result.Error = SanitizeErrorMessage(ex.Message);
        result.ExitCode = -1;
        }

        return result;
    }

    /// <summary>
    /// Sanitizes error messages to prevent sensitive path disclosure.
    /// </summary>
  private static string SanitizeErrorMessage(string message)
 {
      if (string.IsNullOrEmpty(message))
            return message;

        // Replace full Windows paths with relative or generic descriptions
 // Pattern: C:\Users\username\... or similar
      var sanitized = System.Text.RegularExpressions.Regex.Replace(
         message,
            @"[A-Za-z]:\\[^\s""']+",
            "[path]");

       // Also sanitize Linux-style paths
       sanitized = System.Text.RegularExpressions.Regex.Replace(
     sanitized,
            @"/(?:home|Users)/[^\s""']+",
     "[path]");

        return sanitized;
    }

    private class CommandResult
    {
     public string Output { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    }

    // JSON response models for Python scripts
    private class PythonThumbnailResponse
    {
  public bool Success { get; set; }
   public string? Error { get; set; }
        public string? StlPath { get; set; }
        public string? OutputPath { get; set; }
      public PythonStlMetadata? Metadata { get; set; }
    }

    private class PythonStlMetadata
    {
  public PythonDimensions? Dimensions { get; set; }
        public double? Volume { get; set; }
    public int Triangles { get; set; }
    }

    private class PythonDimensions
    {
      public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
    }

    private class PythonGcodeResponse
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
     public string? SlicerName { get; set; }
        public string? SlicerVersion { get; set; }
        public int? PrintTimeSeconds { get; set; }
        public string? PrintTimeFormatted { get; set; }
        public double? FilamentUsedMm { get; set; }
        public double? FilamentUsedM { get; set; }
      public double? FilamentUsedG { get; set; }
        public double? LayerHeight { get; set; }
        public int? InfillPercentage { get; set; }
        public int? NozzleTemp { get; set; }
  public int? BedTemp { get; set; }
    }

    private class PythonBatchResponse
    {
        public bool Success { get; set; }
        public int Total { get; set; }
   public int Succeeded { get; set; }
        public int Failed { get; set; }
        public List<PythonBatchResultItem>? Results { get; set; }
    }

    private class PythonBatchResultItem
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
      public string? FilePath { get; set; }
        public string? OutputPath { get; set; }
    }
}

