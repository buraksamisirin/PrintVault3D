using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using PrintVault3D.Models;
using PrintVault3D.Repositories;
using PrintVault3D.Services;
using PrintVault3D.Services.Actions;

namespace PrintVault3D.ViewModels;

/// <summary>
/// Main ViewModel for the application dashboard.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IVaultService _vaultService;
    private readonly IFileWatcherService _fileWatcherService;
    private readonly IThumbnailProcessingService _thumbnailService;
    private readonly ILogger<MainViewModel>? _logger;
    private readonly IServiceProvider _serviceProvider;
  private readonly IUndoService _undoService;
    private readonly IArchiveService _archiveService;
    private readonly IAutoTaggingService _autoTaggingService;
    private readonly IGcodeParserService _gcodeParserService;
    private CancellationTokenSource? _searchDebounceTokenSource;

    [ObservableProperty]
    private ObservableCollection<Model3D> _models = new();

    [ObservableProperty]
    private ObservableCollection<Category> _categories = new();

    [ObservableProperty]
    private ObservableCollection<Collection> _collections = new();

    private ObservableCollection<Gcode> _unlinkedGcodesManual = new();
    public ObservableCollection<Gcode> UnlinkedGcodes
    {
     get => _unlinkedGcodesManual;
     set => SetProperty(ref _unlinkedGcodesManual, value);
    }

    [ObservableProperty]
    private Model3D? _selectedModel;

    [ObservableProperty]
    private Category? _selectedCategory;

    [ObservableProperty]
    private Collection? _selectedCollection;

    [ObservableProperty]
    private string _searchText = string.Empty;
    [RelayCommand]
    private void ClearAllFilters()
    {
        SelectedCategory = null;
        SelectedCollection = null;
        SearchText = string.Empty;
    }
    partial void OnSearchTextChanged(string value)
    {
        _searchDebounceTokenSource?.Cancel();
        _searchDebounceTokenSource = new CancellationTokenSource();
        var token = _searchDebounceTokenSource.Token;

        Task.Delay(300, token).ContinueWith(async t =>
        {
            if (t.IsCanceled) return;
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                await SearchAsync();
            });
        }, TaskScheduler.Default);
    }

    [ObservableProperty]
    private int _totalModels;

  [ObservableProperty]
  private int _totalGcodes;

    // GCODE Statistics
    [ObservableProperty]
    private string _totalPrintTime = "0h 0m";

    [ObservableProperty]
private string _totalFilamentUsed = "0g";

    [ObservableProperty]
    private string _mostUsedSlicer = "-";

    [ObservableProperty]
    private string _totalEstimatedCost = "0.00";

    [ObservableProperty]
    private bool _isWatcherRunning;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private int _thumbnailQueueCount;



    private bool _isPythonAvailable;
    public bool IsPythonAvailable
    {
        get => _isPythonAvailable;
        set
        {
            if (SetProperty(ref _isPythonAvailable, value))
            {
                // Property changed
            }
        }
    }

    private bool _isInitialized;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _pageSize = 50;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private bool _hasPreviousPage;

    [ObservableProperty]
    private bool _hasNextPage;

    // Multi-select support
    [ObservableProperty]
    private ObservableCollection<Model3D> _selectedModels = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedCount))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private bool _isMultiSelectMode;

    // Duplicate filter
    [ObservableProperty]
    private bool _showDuplicatesOnly;

    [ObservableProperty]
    private int _duplicatesCount;

    /// <summary>
    /// Number of selected models.
    /// </summary>
    public int SelectedCount => SelectedModels.Count;

    /// <summary>
    /// Whether any models are selected.
    /// </summary>
    public bool HasSelection => SelectedModels.Count > 0;



    // Commands
    public IRelayCommand RefreshCommand { get; }

    public IRelayCommand NextPageCommand { get; }
    public IRelayCommand PreviousPageCommand { get; }
    
    public IRelayCommand UndoCommand { get; }
    public IRelayCommand RedoCommand { get; }
    
    public IAsyncRelayCommand ScanGcodesCommand { get; }
    
    // Undo/Redo properties
    public bool CanUndo => _undoService.CanUndo;
    public bool CanRedo => _undoService.CanRedo;
    public string UndoTooltip => $"Undo {_undoService.UndoDescription}";
    public string RedoTooltip => $"Redo {_undoService.RedoDescription}";

    public MainViewModel(
        IUnitOfWork unitOfWork,
        IVaultService vaultService,
        IFileWatcherService fileWatcherService,
        IThumbnailProcessingService thumbnailService,
        ILogger<MainViewModel> logger,
        IServiceProvider serviceProvider,
        IUndoService undoService,
        IArchiveService archiveService,
        IAutoTaggingService autoTaggingService,
        IGcodeParserService gcodeParserService)
    {
        _unitOfWork = unitOfWork;
        _vaultService = vaultService;
        _fileWatcherService = fileWatcherService;
        _thumbnailService = thumbnailService;
        _logger = logger;
        _serviceProvider = serviceProvider;
        _undoService = undoService;
        _archiveService = archiveService;
        _autoTaggingService = autoTaggingService;
        _gcodeParserService = gcodeParserService;
        
        _undoService.StateChanged += OnUndoServiceStateChanged;

        RefreshCommand = new AsyncRelayCommand(RefreshModelsAsync);

        NextPageCommand = new AsyncRelayCommand(NextPageAsync);
        PreviousPageCommand = new AsyncRelayCommand(PreviousPageAsync);
        
        UndoCommand = new AsyncRelayCommand(async () => await _undoService.UndoAsync(), () => _undoService.CanUndo);
        RedoCommand = new AsyncRelayCommand(async () => await _undoService.RedoAsync(), () => _undoService.CanRedo);
        ScanGcodesCommand = new AsyncRelayCommand(ScanGcodesFolderAsync);

        // Subscribe to file watcher events
        _fileWatcherService.ModelFileDetected += OnModelFileDetected;
        _fileWatcherService.GcodeFileDetected += OnGcodeFileDetected;
        _fileWatcherService.ErrorOccurred += OnFileWatcherError;

        // Subscribe to thumbnail processing events
        _thumbnailService.ThumbnailProcessed += OnThumbnailProcessed;

        // Subscribe to selection changes
        SelectedModels.CollectionChanged += OnSelectedModelsCollectionChanged;
    }

    private void OnUndoServiceStateChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoTooltip));
        OnPropertyChanged(nameof(RedoTooltip));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private void OnSelectedModelsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
    }

    /// <summary>
    /// Unsubscribe from all events to prevent memory leaks.
    /// </summary>
    protected override void OnDispose()
    {
        _fileWatcherService.ModelFileDetected -= OnModelFileDetected;
        _fileWatcherService.GcodeFileDetected -= OnGcodeFileDetected;
        _fileWatcherService.ErrorOccurred -= OnFileWatcherError;
        _thumbnailService.ThumbnailProcessed -= OnThumbnailProcessed;
        _undoService.StateChanged -= OnUndoServiceStateChanged;
        SelectedModels.CollectionChanged -= OnSelectedModelsCollectionChanged;
        
        // Cancel any pending search debounce
        _searchDebounceTokenSource?.Cancel();
        _searchDebounceTokenSource?.Dispose();

        base.OnDispose();
    }

    /// <summary>
    /// Initializes the ViewModel by loading data from the database.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        
        try 
        {
            // Initialization logic

            _isInitialized = true;
            StatusMessage = "Initializing...";

            // Initialize vault
            await _vaultService.InitializeAsync();
            
            // Load all data
            await LoadCategoriesAsync();
            
            var collections = await _unitOfWork.Collections.GetAllWithStatsAsync();
            Collections = new ObservableCollection<Collection>(collections);
            
            var unlinked = await _unitOfWork.Gcodes.GetUnlinkedAsync();
            UnlinkedGcodes = new ObservableCollection<Gcode>(unlinked);
            
            TotalModels = await _unitOfWork.Models.CountAsync();
            TotalGcodes = await _unitOfWork.Gcodes.CountAsync();
            
            await LoadGcodeStatsAsync();
            await RefreshModelsAsync();
            
            _fileWatcherService.Start();
            IsWatcherRunning = _fileWatcherService.IsRunning;

            // Check Python availability
            // Use the service provider to get the service to avoid any disposal issues
            var pythonBridge = _serviceProvider.GetRequiredService<IPythonBridgeService>();
            IsPythonAvailable = await pythonBridge.IsPythonAvailableAsync();
            
            if (IsPythonAvailable)
            {
                _logger?.LogInformation("Python is available, starting thumbnail processing");
                await _thumbnailService.ProcessPendingModelsAsync();
                ThumbnailQueueCount = _thumbnailService.QueueCount;
            }
            else
            {
                StatusMessage += " | ⚠️ Python not found";
            }

            _logger?.LogInformation("MainViewModel initialized");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error initializing");
        }
    }

    private async Task LoadCategoriesAsync()
    {
        try 
        {
            // Use GetAllWithModelsAsync to get model counts
            var categories = await _unitOfWork.Categories.GetAllWithModelsAsync();
            
            // Preserve selection if possible
            var selectedId = SelectedCategory?.Id;
            
            Categories = new ObservableCollection<Category>(categories);
            
            if (selectedId.HasValue)
            {
                SelectedCategory = Categories.FirstOrDefault(c => c.Id == selectedId.Value);
            }
            
            _logger?.LogInformation("Loaded {Count} categories", categories.Count());
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading categories");
        }
    }

    [RelayCommand]
    private async Task RefreshModelsAsync()
    {
        PagedResult<Model3D> pagedResult;

        if (ShowDuplicatesOnly)
        {
            pagedResult = await _unitOfWork.Models.GetDuplicatesOnlyPagedAsync(CurrentPage, PageSize);
        }
        else if (SelectedCollection != null && SelectedCollection.Id > 0)
        {
             // Use the specific repository method for collections
             pagedResult = await _unitOfWork.Models.GetPagedByCollectionAsync(SelectedCollection.Id, CurrentPage, PageSize);
        }
        else if (SelectedCategory != null && SelectedCategory.Id > 0)
        {
            pagedResult = await _unitOfWork.Models.GetPagedByCategoryAsync(SelectedCategory.Id, CurrentPage, PageSize);
        }
        else if (!string.IsNullOrWhiteSpace(SearchText))
        {
            pagedResult = await _unitOfWork.Models.SearchPagedAsync(SearchText, CurrentPage, PageSize);
        }
        else
        {
            pagedResult = await _unitOfWork.Models.GetPagedAsync(CurrentPage, PageSize);
        }



        // Mark duplicates and set count
        var duplicateHashesWithCount = await _unitOfWork.Models.GetDuplicateHashesWithCountAsync();
        foreach (var model in pagedResult.Items)
        {
            if (!string.IsNullOrEmpty(model.FileHash) && duplicateHashesWithCount.TryGetValue(model.FileHash, out var count))
            {
                model.IsDuplicate = true;
                model.DuplicateCount = count;
            }
        }

        // Update duplicates count for sidebar
        DuplicatesCount = await _unitOfWork.Models.GetDuplicatesCountAsync();

        Models = new ObservableCollection<Model3D>(pagedResult.Items);
        TotalPages = pagedResult.TotalPages;
        HasPreviousPage = pagedResult.HasPreviousPage;
        HasNextPage = pagedResult.HasNextPage;

        // Update status message with pagination info
        if (ShowDuplicatesOnly)
        {
            StatusMessage = $"Showing {pagedResult.TotalCount} duplicate models";
        }
        else if (TotalPages > 1)
        {
            StatusMessage = $"Page {CurrentPage}/{TotalPages} - {pagedResult.TotalCount} models";
        }

        _logger?.LogInformation("Refreshed models. Page {Page}/{TotalPages}, {Count} items, Total: {Total}", 
            CurrentPage, TotalPages, Models.Count, pagedResult.TotalCount);
    }

    [RelayCommand]
    private void CreateCollection()
    {
        _logger?.LogInformation("CreateCollection command executed - opening dialog");
        
        try
        {
            var dialog = new Views.CreateCollectionDialog();
            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow != null && mainWindow != dialog && mainWindow.IsVisible)
            {
                dialog.Owner = mainWindow;
            }
            dialog.CollectionCreated += async (s, collection) => 
            {
                _logger?.LogInformation("Collection created: {Name}", collection.Name);
                await ReloadCollectionsAsync();
                SelectedCollection = Collections.FirstOrDefault(c => c.Id == collection.Id);
            };
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error opening CreateCollectionDialog");
        }
    }

    [RelayCommand]
    private async Task ReloadCollectionsAsync()
    {
        var collections = await _unitOfWork.Collections.GetAllWithStatsAsync();
        Collections = new ObservableCollection<Collection>(collections);
        _logger?.LogInformation("Reloaded {Count} collections", Collections.Count);
    }

    /// <summary>
    /// Duplicates a collection with all its models.
    /// </summary>
    public async Task DuplicateCollectionAsync(Collection collection)
    {
        if (collection == null) return;

        try
        {
            var newName = $"{collection.Name} (Copy)";
            var duplicate = await _unitOfWork.Collections.DuplicateAsync(collection.Id, newName);
            
            await ReloadCollectionsAsync();
            SelectedCollection = Collections.FirstOrDefault(c => c.Id == duplicate.Id);
            
            StatusMessage = $"Collection '{collection.Name}' duplicated";
            _logger?.LogInformation("Duplicated collection {OriginalName} to {NewName}", collection.Name, newName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error duplicating collection {CollectionName}", collection.Name);
            StatusMessage = "Error duplicating collection";
        }
    }

    /// <summary>
    /// Deletes a collection (models are not deleted, just unlinked).
    /// </summary>
    public async Task DeleteCollectionAsync(Collection collection)
    {
        if (collection == null) return;

        try
        {
            // Clear selection if deleting current collection
            if (SelectedCollection?.Id == collection.Id)
            {
                SelectedCollection = null;
            }

            await _unitOfWork.Collections.DeleteAsync(collection);
            await _unitOfWork.SaveChangesAsync();
            
            await ReloadCollectionsAsync();
            
            StatusMessage = $"Collection '{collection.Name}' deleted";
            _logger?.LogInformation("Deleted collection {CollectionName} (ID: {CollectionId})", collection.Name, collection.Id);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting collection {CollectionName}", collection.Name);
            StatusMessage = "Error deleting collection";
        }
    }

    partial void OnSelectedCollectionChanged(Collection? value)
    {
        if (value != null)
        {
             // Clear category selection if collection is selected
             SelectedCategory = null; 
        }
        
        CurrentPage = 1;
        _ = RefreshModelsAsync().ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                _logger?.LogError(task.Exception?.GetBaseException(), "Error refreshing models after collection change");
            }
        }, TaskScheduler.Default);
        
        StatusMessage = value != null 
            ? $"Showing models in '{value.Name}'" 
            : "Showing all models";
    }

    partial void OnSelectedCategoryChanged(Category? value)
    {
        if (value != null)
        {
            ShowDuplicatesOnly = false;
        }

        CurrentPage = 1;
        _ = RefreshModelsAsync().ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                _logger?.LogError(task.Exception?.GetBaseException(), "Error refreshing models after category change");
            }
        }, TaskScheduler.Default);

        StatusMessage = value != null 
            ? $"Showing models in '{value.Name}'" 
            : "Showing all models";
    }

    [RelayCommand]
    private void ClearSelectedCollection()
    {
        SelectedCollection = null;
    }

    // Page navigation is handled via NextPageCommand and PreviousPageCommand initialized in constructor

    [RelayCommand]
    private async Task SearchAsync()
    {
        CurrentPage = 1; // Reset to first page on search
        await RefreshModelsAsync();
        // Status message is now set in RefreshModelsAsync or here with accurate count
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            StatusMessage = $"Found {Models.Count} models matching '{SearchText}'";
        }
    }

    [RelayCommand]
    private void ShowAllCategories()
    {
        SelectedCategory = null;
        StatusMessage = "Showing all models";
    }

    [RelayCommand]
    private async Task FilterByCategoryAsync(Category? category)
    {
        if (category == null)
        {
            ShowAllCategories();
            return;
        }

        SelectedCategory = category;
        ShowDuplicatesOnly = false; // Clear duplicates filter when selecting category
        await RefreshModelsAsync();

        StatusMessage = $"Showing models in '{category.Name}'";
    }

    [RelayCommand]
    private async Task ToggleDuplicatesFilterAsync()
    {
        ShowDuplicatesOnly = !ShowDuplicatesOnly;
        SelectedCategory = null; // Clear category when showing duplicates
        CurrentPage = 1;
        await RefreshModelsAsync();
    }

    [RelayCommand]
    private async Task ShowDuplicatesAsync(Model3D? model)
    {
        if (model == null || string.IsNullOrEmpty(model.FileHash)) return;

        await ExecuteBusyAsync(async () =>
        {
            var duplicates = await _unitOfWork.Models.GetByFileHashAsync(model.FileHash);
            
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var dialog = new Views.DuplicatesDialog(model.FileHash, duplicates);
                
                var mainWindow = System.Windows.Application.Current.MainWindow;
                if (mainWindow != null && mainWindow != dialog && mainWindow.IsVisible)
                {
                    dialog.Owner = mainWindow;
                }
                
                // Refresh main list if any model was deleted in the dialog
                dialog.ModelDeleted += async (s, e) => 
                {
                    // Refresh count and list if needed
                    await RefreshModelsAsync();
                };
                
                dialog.ShowDialog();
            });
        }, "Loading duplicates...");
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(Model3D? model)
    {
        if (model == null) return;

        model.IsFavorite = !model.IsFavorite;
        await _unitOfWork.Models.UpdateAsync(model);
        await _unitOfWork.SaveChangesAsync();
    }

    [RelayCommand]
    private async Task DeleteModelAsync(Model3D? model)
    {
        if (model == null) return;

        var success = await _vaultService.DeleteModelAsync(model.Id);
        if (success)
        {
            Models.Remove(model);
            TotalModels--;
            StatusMessage = $"Deleted '{model.Name}'";
        }
    }

    [RelayCommand]
    private void OpenVaultFolder()
    {
        _vaultService.OpenVaultInExplorer();
    }

    [RelayCommand]
    private void ToggleFileWatcher()
    {
        if (_fileWatcherService.IsRunning)
        {
            _fileWatcherService.Stop();
        }
        else
        {
            _fileWatcherService.Start();
        }

        IsWatcherRunning = _fileWatcherService.IsRunning;
        StatusMessage = IsWatcherRunning ? "File watcher started" : "File watcher stopped";
    }

    [RelayCommand]
    private async Task ScanDownloadsFolderAsync()
    {
        await ExecuteBusyAsync(async () =>
        {
            var downloadsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Downloads");

            if (!Directory.Exists(downloadsPath))
            {
                StatusMessage = "Downloads folder not found!";
                return;
            }

            var extensions = new[] { "*.stl", "*.3mf", "*.zip", "*.gcode", "*.gco", "*.g" };
            var importedCount = 0;
            var importedGcodesCount = 0;
            var skippedCount = 0;

            foreach (var pattern in extensions)
            {
                var files = Directory.GetFiles(downloadsPath, pattern, SearchOption.TopDirectoryOnly);
                
                foreach (var file in files)
                {
                    StatusMessage = $"Processing: {Path.GetFileName(file)}...";

                    // Check if it's an archive
                    if (_archiveService.IsArchive(file))
                    {
                        var archiveName = Path.GetFileNameWithoutExtension(file);
                        var extractionPath = Path.Combine(_vaultService.VaultPath, "Extracted", archiveName);
                        Directory.CreateDirectory(extractionPath);

                        var extractedFiles = await _archiveService.ExtractAndFilterAsync(file, extractionPath);
                        var extractedModels = new List<Model3D>();

                        if (!extractedFiles.Any())
                        {
                            try { if (Directory.Exists(extractionPath)) Directory.Delete(extractionPath, true); } catch { }
                            continue;
                        }

                        foreach (var path in extractedFiles)
                        {
                            // If zip contains G-code, import it too
                            var ext = Path.GetExtension(path).ToLowerInvariant();
                            if (ext == ".gcode" || ext == ".gco" || ext == ".g")
                            {
                                 var gcodeResult = await _vaultService.ImportGcodeAsync(path);
                                 if (gcodeResult != null && gcodeResult.Success) importedGcodesCount++;
                                 else if (gcodeResult != null && gcodeResult.Skipped) skippedCount++;
                                 continue;
                            }

                            var result = await _vaultService.ImportModelAsync(path);
                            if (result.Success && result.Model != null)
                            {
                                extractedModels.Add(result.Model);
                                if (IsPythonAvailable) _thumbnailService.QueueModel(result.Model);
                                importedCount++;
                            }
                            else if (result.Skipped)
                            {
                                skippedCount++;
                            }
                        }

                        // Auto-Collection for ZIP
                        if (extractedModels.Count >= 2)
                        {
                            var existingCollection = await _unitOfWork.Collections.GetByNameAsync(archiveName);
                            if (existingCollection == null)
                            {
                                existingCollection = new Collection { Name = archiveName, CreatedDate = DateTime.Now, LastModifiedDate = DateTime.Now };
                                
                                // Auto-categorize the collection based on its name using JSON keywords
                                var suggestedCategoryName = _autoTaggingService.SuggestCategory(archiveName);
                                if (!string.IsNullOrEmpty(suggestedCategoryName))
                                {
                                    var matchedCategory = await _unitOfWork.Categories.GetByNameAsync(suggestedCategoryName);
                                    if (matchedCategory == null)
                                    {
                                        matchedCategory = new Category { Name = suggestedCategoryName };
                                        await _unitOfWork.Categories.AddAsync(matchedCategory);
                                        await _unitOfWork.SaveChangesAsync();
                                        StatusMessage = $"Created category '{suggestedCategoryName}'";
                                    }
                                    
                                    existingCollection.CategoryId = matchedCategory.Id;
                                    _logger.LogInformation("Collection '{Name}' auto-categorized to '{Category}'", archiveName, suggestedCategoryName);
                                }
                                
                                await _unitOfWork.Collections.AddAsync(existingCollection);
                                await _unitOfWork.SaveChangesAsync();
                            }
                            await AddToCollectionAsync(existingCollection, extractedModels);
                        }
                    }
                    else
                    {
                        var ext = Path.GetExtension(file).ToLowerInvariant();
                        if (ext == ".gcode" || ext == ".gco" || ext == ".g")
                        {
                             // Regular G-code file
                             var result = await _vaultService.ImportGcodeAsync(file);
                             if (result != null && result.Success) importedGcodesCount++;
                             else if (result != null && result.Skipped) skippedCount++;
                        }
                        else
                        {
                            // Regular model file
                            var result = await _vaultService.ImportModelAsync(file);
                            if (result.Success && result.Model != null)
                            {
                                if (IsPythonAvailable) _thumbnailService.QueueModel(result.Model);
                                importedCount++;
                            }
                            else if (result.Skipped)
                            {
                                skippedCount++;
                            }
                        }
                    }
                }
            }

            if (importedCount > 0 || importedGcodesCount > 0)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await LoadCategoriesAsync();
                    await RefreshModelsAsync();
                    
                    // Specific refresh for G-codes
                    TotalGcodes = await _unitOfWork.Gcodes.CountAsync();
                    var unlinked = await _unitOfWork.Gcodes.GetUnlinkedAsync();
                    UnlinkedGcodes = new ObservableCollection<Gcode>(unlinked);
                    await LoadGcodeStatsAsync();
                });

                if (IsPythonAvailable && importedCount > 0)
                {
                    await _thumbnailService.StartAsync();
                }
            }

            TotalModels = await _unitOfWork.Models.CountAsync();
            
            var msg = $"{importedCount} models and {importedGcodesCount} G-codes imported.";
            if (skippedCount > 0)
            {
                msg += $" ({skippedCount} items skipped as duplicates)";
            }
            if (importedCount == 0 && importedGcodesCount == 0 && skippedCount == 0)
            {
                msg = "No new files found to import.";
            }
            
            StatusMessage = msg;

        }, "Scanning Downloads folder...");
    }

    [RelayCommand]
    private async Task ImportFilesManuallyAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
       Title = "Select 3D Model or ZIP Files",
            Filter = "All Supported Files|*.stl;*.3mf;*.zip|3D Model Files|*.stl;*.3mf|ZIP Archives|*.zip|All Files|*.*",
 Multiselect = true
        };

        if (dialog.ShowDialog() == true)
    {
    // Use the new unified handler
            await HandleFilesDroppedAsync(dialog.FileNames);
        }
    }

    private void OnModelFileDetected(object? sender, FileDetectedEventArgs e)
    {
        // Fire-and-forget pattern with proper error handling
        _ = ProcessModelFileDetectedAsync(e).ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                _logger?.LogError(task.Exception?.GetBaseException(), 
                    "Error processing model file detected event: {FilePath}", e.FilePath);
                SafeDispatcher(() => StatusMessage = $"Import error: {task.Exception?.GetBaseException()?.Message ?? "Unknown error"}");
            }
        }, TaskScheduler.Default);
    }

    private async Task ProcessModelFileDetectedAsync(FileDetectedEventArgs e)
    {
        try
        {
            _logger?.LogInformation("Processing detected model file: {FileName}", e.FileName);
            var result = await _vaultService.ImportModelAsync(e.FilePath);
            
            if (result.Success && result.Model != null)
            {
                // Queue for thumbnail generation
                if (IsPythonAvailable)
                {
                    _thumbnailService.QueueModel(result.Model);
                    await _thumbnailService.StartAsync();
                }

                // Update UI on dispatcher thread
                await SafeDispatcherAsync(async () =>
                {
                    await LoadCategoriesAsync(); // Refresh categories
                    
                    // Refresh list to maintain sort/pagination order
                    await RefreshModelsAsync();
                    
                    TotalModels++;
                    ThumbnailQueueCount = _thumbnailService.QueueCount;
                    StatusMessage = $"Imported: {result.Model.Name}";

                    // Safely parse source URL
                    if (!string.IsNullOrEmpty(result.SourceUrl))
                    {
                        try
                        {
                            StatusMessage += $" ({new Uri(result.SourceUrl).Host})";
                        }
                        catch (UriFormatException)
                        {
                            // Invalid URL, ignore
                        }
                    }
                });

                _logger?.LogInformation("Successfully imported model: {ModelName} (ID: {ModelId})", result.Model.Name, result.Model.Id);
            }
            else
            {
                _logger?.LogWarning("Failed to import model file: {FileName}. Error: {Error}", 
                    e.FileName, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception while processing model file: {FilePath}", e.FilePath);
            throw; // Re-throw to be caught by ContinueWith
        }
    }

    private void OnGcodeFileDetected(object? sender, FileDetectedEventArgs e)
    {
        // Fire-and-forget pattern with proper error handling
        _ = ProcessGcodeFileDetectedAsync(e).ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                _logger?.LogError(task.Exception?.GetBaseException(), 
                    "Error processing G-code file detected event: {FilePath}", e.FilePath);
                SafeDispatcher(() => StatusMessage = $"G-code import error: {task.Exception?.GetBaseException()?.Message ?? "Unknown error"}");
            }
        }, TaskScheduler.Default);
    }

    private async Task ProcessGcodeFileDetectedAsync(FileDetectedEventArgs e)
    {
        try
        {
            _logger?.LogInformation("Processing detected G-code file: {FileName}", e.FileName);
            var result = await _vaultService.ImportGcodeAsync(e.FilePath);

            if (result.Success && result.Gcode != null)
            {
                await SafeDispatcherAsync(() =>
                {
                    TotalGcodes++;
                    
                    if (result.Gcode.ModelId == null)
                    {
                        UnlinkedGcodes.Insert(0, result.Gcode);
                        StatusMessage = $"Imported G-code: {e.FileName} (unlinked)";
                    }
                    else
                    {
                        StatusMessage = $"Imported G-code: {e.FileName} (linked)";
                    }
                });

                _logger?.LogInformation("Successfully imported G-code: {FileName} (ID: {GcodeId})", 
                    e.FileName, result.Gcode.Id);
            }
            else
            {
                _logger?.LogWarning("Failed to import G-code file: {FileName}. Error: {Error}", 
                    e.FileName, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Exception while processing G-code file: {FilePath}", e.FilePath);
            throw; // Re-throw to be caught by ContinueWith
        }
    }

    private void OnFileWatcherError(object? sender, Exception e)
    {
        _logger?.LogError(e, "File watcher error occurred");
        SafeDispatcher(() => StatusMessage = $"Watcher error: {e.Message}");
    }

    private void OnThumbnailProcessed(object? sender, ThumbnailProgressEventArgs e)
    {
        SafeDispatcher(() =>
        {
            ThumbnailQueueCount = _thumbnailService.QueueCount;

            // Find and update the model in our collection
            var model = Models.FirstOrDefault(m => m.Id == e.ModelId);
            if (model != null && e.Success && !string.IsNullOrEmpty(e.ThumbnailPath))
            {
                model.ThumbnailPath = e.ThumbnailPath;
                model.ThumbnailGenerated = true;
                
                // Force UI refresh by replacing the item
                var index = Models.IndexOf(model);
                if (index >= 0)
                {
                    Models.RemoveAt(index);
                    Models.Insert(index, model);
                }
            }

            StatusMessage = e.Success 
                ? $"Thumbnail generated: {e.ModelName}"
                : $"Thumbnail error ({e.ModelName}): {e.Error}";
        });
    }

    [RelayCommand]
    private async Task GenerateAllThumbnailsAsync()
    {
        if (!IsPythonAvailable)
        {
            StatusMessage = "Python not installed! Cannot generate thumbnails.";
            return;
        }

        await ExecuteBusyAsync(async () =>
        {
            await _thumbnailService.ProcessPendingModelsAsync();
            ThumbnailQueueCount = _thumbnailService.QueueCount;
            StatusMessage = $"Generating thumbnails for {ThumbnailQueueCount} models...";
        }, "Preparing thumbnail queue...");
    }

    [RelayCommand]
    private void OpenModelDetail(Model3D? model)
    {
        if (model == null) return;
        
        var dialog = new Views.ModelDetailDialog(model.Id);
        dialog.ModelUpdated += async (s, e) => await RefreshModelsAsync();
        
        var mainWindow = System.Windows.Application.Current.MainWindow;
        if (mainWindow != null && mainWindow != dialog && mainWindow.IsVisible)
        {
            dialog.Owner = mainWindow;
        }
        
        dialog.ShowDialog();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var dialog = new Views.SettingsDialog();
        var mainWindow = System.Windows.Application.Current.MainWindow;
        if (mainWindow != null && mainWindow != dialog && mainWindow.IsVisible)
        {
            dialog.Owner = mainWindow;
        }

        // Hook into DataCleared event
        if (dialog.DataContext is SettingsViewModel settingsVm)
        {
            settingsVm.DataCleared += async (s, e) => 
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    StatusMessage = "Data cleared. Refreshing...";
                    // Reset collections and categories
                    Categories.Clear();
                    Collections.Clear();
                    Models.Clear();
                    
                    // Reload everything
                    await InitializeAsync();
                });
            };
        }

        dialog.ShowDialog();
    }

    [RelayCommand]
    private void OpenCategoryManagement()
    {
        var dialog = new Views.CategoryManagementDialog();
        
        var mainWindow = System.Windows.Application.Current.MainWindow;
        if (mainWindow != null && mainWindow != dialog && mainWindow.IsVisible)
        {
            dialog.Owner = mainWindow;
        }

        dialog.CategoriesChanged += async (s, e) =>
        {
            var categories = await _unitOfWork.Categories.GetAllAsync();
            Categories = new ObservableCollection<Category>(categories);
        };
        dialog.Owner = System.Windows.Application.Current.MainWindow;
        dialog.ShowDialog();
    }

    [RelayCommand]
    private async Task ChangeCategoryAsync((Model3D Model, Category Category) param)
    {
        if (param.Model == null || param.Category == null) return;

        param.Model.CategoryId = param.Category.Id;
        param.Model.Category = param.Category;
        await _unitOfWork.Models.UpdateAsync(param.Model);
        await _unitOfWork.SaveChangesAsync();

        StatusMessage = $"'{param.Model.Name}' → moved to '{param.Category.Name}'";
    }

    [RelayCommand]
    private void OpenFileLocation(Model3D? model)
    {
        if (model == null || string.IsNullOrEmpty(model.FilePath)) return;

        var directory = Path.GetDirectoryName(model.FilePath);
        if (Directory.Exists(directory))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{model.FilePath}\"",
                UseShellExecute = true
            });
        }
    }

    #region Multi-Select and Bulk Commands

    /// <summary>
    /// Toggles selection of a model (for Ctrl+Click).
    /// </summary>
    [RelayCommand]
    private void ToggleModelSelection(Model3D? model)
    {
        if (model == null) return;

        if (SelectedModels.Contains(model))
        {
            SelectedModels.Remove(model);
            _logger?.LogInformation("Model deselected: {ModelName}. Selection count: {Count}", model.Name, SelectedCount);
        }
        else
        {
            SelectedModels.Add(model);
            _logger?.LogInformation("Model selected: {ModelName}. Selection count: {Count}", model.Name, SelectedCount);
        }

        IsMultiSelectMode = SelectedModels.Count > 0;
    }

    /// <summary>
    /// Clears all selected models.
    /// </summary>
    [RelayCommand]
    private void ClearSelection()
    {
        SelectedModels.Clear();
        IsMultiSelectMode = false;
        StatusMessage = "Selection cleared";
        _logger?.LogInformation("Selection cleared");
    }

    /// <summary>
    /// Selects all models on the current page.
    /// </summary>
    [RelayCommand]
    private void SelectAll()
    {
        SelectedModels.Clear();
        foreach (var model in Models)
        {
            SelectedModels.Add(model);
        }
        IsMultiSelectMode = SelectedModels.Count > 0;
        StatusMessage = $"{SelectedCount} models selected";
        _logger?.LogInformation("Selected all {Count} models on current page", SelectedCount);
    }

    /// <summary>
    /// Toggles multi-select mode. When enabled, normal clicks will select models.
    /// </summary>
    [RelayCommand]
    private void ToggleMultiSelectMode()
    {
        IsMultiSelectMode = !IsMultiSelectMode;
        if (!IsMultiSelectMode)
        {
            SelectedModels.Clear();
        }
        StatusMessage = IsMultiSelectMode 
            ? "Selection mode active - Click models to select" 
            : "Selection mode disabled";
        _logger?.LogInformation("Multi-select mode toggled: {IsEnabled}", IsMultiSelectMode);
    }

    /// <summary>
    /// Checks if a model is selected.
    /// </summary>
    public bool IsModelSelected(Model3D model)
    {
        return SelectedModels.Contains(model);
    }

    /// <summary>
    /// Selects a range of models between two models (inclusive).
    /// </summary>
    public void SelectRange(Model3D starModel, Model3D endModel)
    {
        var startIndex = Models.IndexOf(starModel);
        var endIndex = Models.IndexOf(endModel);

        if (startIndex < 0 || endIndex < 0) return;

        var start = Math.Min(startIndex, endIndex);
        var end = Math.Max(startIndex, endIndex);

        for (int i = start; i <= end; i++)
        {
            var model = Models[i];
            if (!SelectedModels.Contains(model))
            {
                SelectedModels.Add(model);
            }
        }
        
        IsMultiSelectMode = SelectedModels.Count > 0;
        StatusMessage = $"{SelectedCount} models selected";
    }


    /// <summary>
    /// Bulk delete selected models.
    /// </summary>
    [RelayCommand]
    private async Task BulkDeleteAsync()
    {
        if (SelectedModels.Count == 0) return;

        var count = SelectedModels.Count;
        var result = System.Windows.MessageBox.Show(
            $"{count} models will be deleted. This cannot be undone!\n\nDo you want to continue?",
            "Bulk Delete Confirmation",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        await ExecuteBusyAsync(async () =>
        {
            var deletedCount = 0;
            var modelsToDelete = SelectedModels.ToList();

            foreach (var model in modelsToDelete)
            {
                var success = await _vaultService.DeleteModelAsync(model.Id);
                if (success)
                {
                    await SafeDispatcherAsync(() =>
                    {
                        Models.Remove(model);
                        SelectedModels.Remove(model);
                    });
                    deletedCount++;
                }
            }

            TotalModels = await _unitOfWork.Models.CountAsync();
            IsMultiSelectMode = SelectedModels.Count > 0;
            StatusMessage = $"{deletedCount} models deleted";
            _logger?.LogInformation("Bulk deleted {Count} models", deletedCount);

        }, "Deleting models...");
    }

    /// <summary>
    /// Add tags to selected models.
    /// </summary>
    [RelayCommand]
    private async Task BulkAddTagsAsync(string? tags)
    {
        if (SelectedModels.Count == 0 || string.IsNullOrWhiteSpace(tags)) return;

        await ExecuteBusyAsync(async () =>
        {
            var updatedCount = 0;
            var newTags = tags.Trim();

            foreach (var model in SelectedModels.ToList())
            {
                // Append to existing tags
                if (string.IsNullOrWhiteSpace(model.Tags))
                {
                    model.Tags = newTags;
                }
                else
                {
                    // Avoid duplicate tags
                    var existingTags = model.Tags.Split(',').Select(t => t.Trim().ToLowerInvariant()).ToHashSet();
                    var tagsToAdd = newTags.Split(',')
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrEmpty(t) && !existingTags.Contains(t.ToLowerInvariant()));
                    
                    if (tagsToAdd.Any())
                    {
                        model.Tags = model.Tags + ", " + string.Join(", ", tagsToAdd);
                    }
                }

                await _unitOfWork.Models.UpdateAsync(model);
                updatedCount++;
            }

            await _unitOfWork.SaveChangesAsync();
            StatusMessage = $"Added tags to {updatedCount} models: {newTags}";
            _logger?.LogInformation("Added tags '{Tags}' to {Count} models", newTags, updatedCount);

        }, "Adding tags...");
    }

    /// <summary>
    /// Regenerate thumbnails for selected models.
    /// </summary>
    [RelayCommand]
    private async Task RegenerateThumbnailsAsync()
    {
        if (!IsPythonAvailable)
        {
            StatusMessage = "Python not installed! Cannot generate thumbnails.";
            return;
        }

        var modelsToProcess = SelectedModels.Count > 0 
            ? SelectedModels.ToList() 
            : Models.Where(m => !m.ThumbnailGenerated || string.IsNullOrEmpty(m.ThumbnailPath)).ToList();

        if (modelsToProcess.Count == 0)
        {
            StatusMessage = "No models found to generate thumbnails.";
            return;
        }

        await ExecuteBusyAsync(async () =>
        {
            // Reset thumbnail status for selected models
            foreach (var model in modelsToProcess)
            {
                // Delete existing thumbnail if exists
                if (!string.IsNullOrEmpty(model.ThumbnailPath) && System.IO.File.Exists(model.ThumbnailPath))
                {
                    try
                    {
                        System.IO.File.Delete(model.ThumbnailPath);
                    }
                    catch { /* ignore */ }
                }

                model.ThumbnailGenerated = false;
                model.ThumbnailPath = null;
                await _unitOfWork.Models.UpdateAsync(model);
                _thumbnailService.QueueModel(model);
            }

            await _unitOfWork.SaveChangesAsync();
            await _thumbnailService.StartAsync();

            ThumbnailQueueCount = _thumbnailService.QueueCount;
            StatusMessage = $"Generating thumbnails for {modelsToProcess.Count} models...";
            _logger?.LogInformation("Queued {Count} models for thumbnail regeneration", modelsToProcess.Count);

        }, "Preparing thumbnail queue...");
    }

    #endregion




    private ICommand? _assignCollectionCommand;
    public ICommand AssignCollectionCommand => _assignCollectionCommand ??= new AsyncRelayCommand<(Collection, List<Model3D>)>(AssignCollection);

    private async Task AssignCollection((Collection Collection, List<Model3D> Models) args)
    {
        if (args.Collection == null || args.Models == null) return;
        await AddToCollectionAsync(args.Collection, args.Models);
    }

    private ICommand? _assignCategoryCommand;
    public ICommand AssignCategoryCommand => _assignCategoryCommand ??= new AsyncRelayCommand<(Category, List<Model3D>)>(AssignCategory);

    private async Task AssignCategory((Category Category, List<Model3D> Models) args)
    {
        if (args.Category == null || args.Models == null) return;
        await AssignCategoryAsync(args.Category.Name, args.Models);
    }

    public async Task AddToCollectionAsync(Collection collection, List<Model3D> models)
    {
        try
        {
            var action = new AddModelsToCollectionAction(_unitOfWork, collection, models, async () =>
            {
                // Force full reload to ensure counts are accurate across the UI
                await ReloadCollectionsAsync();
                
                // Reselect the collection if it was selected
                if (SelectedCollection?.Id == collection.Id)
                {
                    SelectedCollection = Collections.FirstOrDefault(c => c.Id == collection.Id);
                }

                StatusMessage = $"Added {models.Count} models to '{collection.Name}'";
                _logger?.LogInformation("Added {Count} models to collection {CollectionName}", models.Count, collection.Name);
            });

            await _undoService.ExecuteActionAsync(action);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error adding models to collection");
            StatusMessage = "Error adding models to collection";
        }
    }

    public async Task AssignCategoryAsync(string categoryName, List<Model3D> models)
    {
        try
        {
            var category = Categories.FirstOrDefault(c => c.Name == categoryName);
            if (category == null)
            {
                StatusMessage = $"Category '{categoryName}' not found.";
                return;
            }

            var action = new AssignCategoryAction(_unitOfWork, models, category.Id, categoryName, async () =>
            {
                if (SelectedCategory != null)
                {
                    await RefreshModelsAsync();
                }
                StatusMessage = $"Moved {models.Count} models to '{categoryName}'";
                _logger?.LogInformation("Moved {Count} models to category {Category}", models.Count, categoryName);
            });

            await _undoService.ExecuteActionAsync(action);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error assigning category");
            StatusMessage = "Error moving models to category";
        }
    }

    private async Task NextPageAsync()
    {
        if (HasNextPage)
        {
            CurrentPage++;
            await RefreshModelsAsync();
        }
    }

    private async Task PreviousPageAsync()
    {
        if (HasPreviousPage)
        {
            CurrentPage--;
            await RefreshModelsAsync();
        }
    }

    public async Task RemoveFromCollectionAsync(Collection collection, List<Model3D> models)
    {
        try
        {
            var action = new RemoveModelsFromCollectionAction(_unitOfWork, collection, models, async () =>
            {
                // Force full reload to ensure counts are accurate across the UI
                await ReloadCollectionsAsync();

                // Reselect the collection if it was selected and refresh the grid
                if (SelectedCollection?.Id == collection.Id)
                {
                    SelectedCollection = Collections.FirstOrDefault(c => c.Id == collection.Id);
                    await RefreshModelsAsync();
                }
                
                StatusMessage = $"Removed {models.Count} models from '{collection.Name}'";
            });

            await _undoService.ExecuteActionAsync(action);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error removing models from collection");
            StatusMessage = "Error removing models from collection";
        }
    }
    public async Task HandleFilesDroppedAsync(string[] files)
    {
        if (files == null || files.Length == 0) return;

        await ExecuteBusyAsync(async () =>
        {
            var totalImported = 0;
            var archivesProcessed = 0;

            foreach (var file in files)
            {
                // Check if it's a folder (if dragged a folder)
                if (Directory.Exists(file))
                {
                    continue;
                }

                // Check if it's an archive
                if (_archiveService.IsArchive(file))
                {
                    StatusMessage = $"Extracting: {Path.GetFileName(file)}...";
                    archivesProcessed++;

                    // Create extraction path: Vault/Extracted/ArchiveName
                    var archiveName = Path.GetFileNameWithoutExtension(file);
                    var extractionPath = Path.Combine(_vaultService.VaultPath, "Extracted", archiveName);
                    Directory.CreateDirectory(extractionPath);

                    var extractedFiles = await _archiveService.ExtractAndFilterAsync(file, extractionPath);
                    var extractedModels = new List<Model3D>();
                    
                    // Cleanup if no valid 3D models found
                    if (!extractedFiles.Any())
                    {
                        try 
                        {
                            if (Directory.Exists(extractionPath)) Directory.Delete(extractionPath, true); 
                        }
                        catch { /* process lock? ignore */ }
                        
                        StatusMessage = $"Ignored: {Path.GetFileName(file)} (No 3D models found)";
                        continue;
                    }

                    if (extractedFiles.Any())
                    {
                        foreach (var path in extractedFiles)
                        {
                            var result = await _vaultService.ImportModelAsync(path);
                            if (result.Success && result.Model != null)
                            {
                                extractedModels.Add(result.Model);
                                if (IsPythonAvailable)
                                {
                                    _thumbnailService.QueueModel(result.Model);
                                    _ = _thumbnailService.StartAsync();
                                }
                                totalImported++;
                            }
                        }

                        // Auto-Collection Logic: If multiple models found (or at least 2), create a collection
                        // User specifically asked: "if there are 3 files, make a collection".
                        // Let's set the threshold to >= 2 to be smart.
                        if (extractedModels.Count >= 2)
                        {
                             var collectionName = archiveName; // Use ZIP name
                             
                             // Check if exists
                             var existingCollection = await _unitOfWork.Collections.GetByNameAsync(collectionName);
                             if (existingCollection == null)
                             {
                                 existingCollection = new Collection { Name = collectionName, CreatedDate = DateTime.Now, LastModifiedDate = DateTime.Now };
                                 
                                 // Auto-categorize the collection based on its name using JSON keywords
                                 var suggestedCategoryName = _autoTaggingService.SuggestCategory(collectionName);
                                 if (!string.IsNullOrEmpty(suggestedCategoryName))
                                 {
                                     var matchedCategory = await _unitOfWork.Categories.GetByNameAsync(suggestedCategoryName);
                                     if (matchedCategory != null)
                                     {
                                         existingCollection.CategoryId = matchedCategory.Id;
                                         StatusMessage = $"Collection '{collectionName}' auto-categorized to '{matchedCategory.Name}'";
                                     }
                                 }
                                 
                                 await _unitOfWork.Collections.AddAsync(existingCollection);
                                 await _unitOfWork.SaveChangesAsync();
                                 StatusMessage = $"Created collection '{collectionName}'";
                             }
                             
                             // Add models to collection
                             await AddToCollectionAsync(existingCollection, extractedModels);
                        }
                    }
                }
                else
                {
                    var result = await ImportSingleFileAsync(file);
                    if (result) totalImported++;
                }
            }

            if (totalImported > 0)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                   await LoadCategoriesAsync(); 
                   CurrentPage = 1;
                   await RefreshModelsAsync();
                });
            }

            if (archivesProcessed > 0)
            {
                 StatusMessage = $"Processed {archivesProcessed} archives. Imported {totalImported} models.";
            }
            else
            {
                 StatusMessage = $"{totalImported} models imported.";
            }

        }, "Processing dropped files...");
    }

    private async Task<bool> ImportSingleFileAsync(string filePath)
    {
        StatusMessage = $"Importing: {Path.GetFileName(filePath)}...";

        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".gcode" || ext == ".gco" || ext == ".g")
        {
            var gcodeResult = await _vaultService.ImportGcodeAsync(filePath);
            return gcodeResult.Success;
        }

        var result = await _vaultService.ImportModelAsync(filePath);
        
        if (result.Success && result.Model != null)
        {
            // Auto-categorize single file
            var filename = Path.GetFileNameWithoutExtension(filePath);
            var suggestedCategoryName = _autoTaggingService.SuggestCategory(filename);
            
            if (!string.IsNullOrEmpty(suggestedCategoryName))
            {
                 var matchedCategory = await _unitOfWork.Categories.GetByNameAsync(suggestedCategoryName);
                 if (matchedCategory == null)
                 {
                     matchedCategory = new Category { Name = suggestedCategoryName };
                     await _unitOfWork.Categories.AddAsync(matchedCategory);
                     await _unitOfWork.SaveChangesAsync();
                     StatusMessage = $"Created category '{suggestedCategoryName}'";
                 }

                 result.Model.CategoryId = matchedCategory.Id;
                 await _unitOfWork.Models.UpdateAsync(result.Model);
                 await _unitOfWork.SaveChangesAsync();
                 StatusMessage += $" (Auto-tagged: {matchedCategory.Name})";
                 _logger?.LogInformation("Auto-categorized model '{Model}' to '{Category}'", result.Model.Name, matchedCategory.Name);
            }

            if (IsPythonAvailable)
            {
                _thumbnailService.QueueModel(result.Model);
                _ = _thumbnailService.StartAsync();
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// Scans a folder for GCODE files and imports them.
    /// </summary>
    private async Task ScanGcodesFolderAsync()
    {
     var dialog = new Microsoft.Win32.OpenFolderDialog
        {
  Title = "Select folder containing GCODE files"
        };

        if (dialog.ShowDialog() != true)
          return;

      var folderPath = dialog.FolderName;
      
        try
  {
            StatusMessage = "Scanning GCODEs...";
    
            var gcodeExtensions = new[] { ".gcode", ".gco", ".g" };
         var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
      .Where(f => gcodeExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
       .ToList();

      if (files.Count == 0)
            {
   StatusMessage = "No GCODE files found in the selected folder.";
    return;
          }

 int imported = 0;
       int skipped = 0;

 foreach (var file in files)
            {
      try
    {
     // Use VaultService to import and link G-code
        var result = await _vaultService.ImportGcodeAsync(file);

     if (result.Success)
        {
          imported++;
        }
     else if (result.ErrorMessage != null && result.ErrorMessage.Contains("zaten içe aktarılmış"))
   {
         skipped++;
                }
 else
{
           // Log other errors but continue
         _logger?.LogWarning("Failed to import G-code: {File}. Error: {Error}", file, result.ErrorMessage);
         }
      
      StatusMessage = $"Importing GCODEs... {imported}/{files.Count}";
      }
                catch (Exception ex)
         {
  _logger?.LogWarning(ex, "Failed to import GCODE: {File}", file);
       }
     }

          // Refresh G-code stats and list
            TotalGcodes = await _unitOfWork.Gcodes.CountAsync();
    var unlinked = await _unitOfWork.Gcodes.GetUnlinkedAsync();
            UnlinkedGcodes = new ObservableCollection<Gcode>(unlinked);
  await LoadGcodeStatsAsync();

      StatusMessage = $"Imported {imported} GCODEs, skipped {skipped} existing.";
        }
        catch (Exception ex)
        {
        _logger?.LogError(ex, "Error scanning GCODEs folder");
     StatusMessage = "Error scanning GCODEs folder.";
      }
    }

    /// <summary>
    /// Loads GCODE statistics for the dashboard.
    /// </summary>
    private async Task LoadGcodeStatsAsync()
    {
      try
  {
 var allGcodes = await _unitOfWork.Gcodes.GetAllAsync();
            var gcodeList = allGcodes.ToList();

        if (gcodeList.Count == 0)
      {
        TotalPrintTime = "0h 0m";
    TotalFilamentUsed = "0g";
                MostUsedSlicer = "-";
          return;
     }

            // Calculate total print time
     var totalTicks = gcodeList
          .Where(g => g.PrintTimeTicks.HasValue)
             .Sum(g => g.PrintTimeTicks!.Value);
        var totalTime = TimeSpan.FromTicks(totalTicks);
    TotalPrintTime = $"{(int)totalTime.TotalHours}h {totalTime.Minutes}m";

// Calculate total filament used (grams)
       var totalFilament = gcodeList
       .Where(g => g.FilamentWeight.HasValue)
  .Sum(g => g.FilamentWeight!.Value);
            TotalFilamentUsed = totalFilament >= 1000 
      ? $"{totalFilament / 1000:F1}kg" 
      : $"{totalFilament:F0}g";

          // Find most used slicer
         var slicerGroups = gcodeList
      .Where(g => !string.IsNullOrEmpty(g.SlicerName))
            .GroupBy(g => g.SlicerName)
     .OrderByDescending(g => g.Count())
     .FirstOrDefault();
  
            MostUsedSlicer = slicerGroups?.Key ?? "-";

  // Calculate total estimated cost
     var settings = _serviceProvider.GetRequiredService<IAppSettingsService>();
        var totalCost = gcodeList
            .Where(g => g.FilamentWeight.HasValue)
     .Sum(g => g.CalculateEstimatedCost(settings.Settings.FilamentCostPerKg) ?? 0);
  TotalEstimatedCost = $"₺{totalCost:F2}";
    }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load GCODE statistics");
        }
    }
}

