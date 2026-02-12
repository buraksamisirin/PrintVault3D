using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PrintVault3D.Models;
using PrintVault3D.Repositories;
using PrintVault3D.Services;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;

// Avoid ambiguity between our Model3D entity and Media3D.Model3D
using ModelEntity = PrintVault3D.Models.Model3D;

namespace PrintVault3D.ViewModels;

/// <summary>
/// ViewModel for the Model Detail dialog.
/// </summary>
public partial class ModelDetailViewModel : ViewModelBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IVaultService _vaultService;
    private readonly ITagLearningService _tagLearningService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly IGcodeParserService _gcodeParserService;
    private readonly ILogger<ModelDetailViewModel>? _logger;

    [ObservableProperty]
    private ModelEntity _model = null!;

    [ObservableProperty]
    private ObservableCollection<Category> _categories = new();

    [ObservableProperty]
    private ObservableCollection<Gcode> _gcodes = new();

    [ObservableProperty]
    private Category? _selectedCategory;

    [ObservableProperty]
    private string _notes = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSourceUrl))]
    private string _tags = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSourceUrl))]
    private string _sourceUrl = string.Empty;

    public bool HasSourceUrl => !string.IsNullOrWhiteSpace(SourceUrl);

    [ObservableProperty]
    private string _modelName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _tagSuggestions = new();

    [ObservableProperty]
    private string _fileSizeFormatted = string.Empty;

    [ObservableProperty]
    private string _dimensionsFormatted = string.Empty;

    [ObservableProperty]
    private int _triangleCount;

    [ObservableProperty]
    private int _vertexCount;

    [ObservableProperty]
    private string _geometryInfo = string.Empty;

    [ObservableProperty]
    private Geometry3D? _modelGeometry;

    [ObservableProperty]
    private bool _isLoading3D;

    [ObservableProperty]
    private bool _is3DViewActive;

    [ObservableProperty]
    private string _previewButtonText = "3D Preview";

    [ObservableProperty]
    private string _previewButtonIcon = "Cuboid"; // Or suitable icon name for your font icon system if you use one, otherwise ignore logic for now

    public event EventHandler? CloseRequested;
    public event EventHandler? ModelUpdated;

    public ModelDetailViewModel(
        IUnitOfWork unitOfWork, 
        IVaultService vaultService, 
        ITagLearningService tagLearningService, 
        IAppSettingsService appSettingsService,
        IGcodeParserService gcodeParserService,
        ILogger<ModelDetailViewModel>? logger = null)
    {
        _unitOfWork = unitOfWork;
        _vaultService = vaultService;
        _tagLearningService = tagLearningService;
        _appSettingsService = appSettingsService;
        _gcodeParserService = gcodeParserService;
        _logger = logger;
        
        // Load settings
        FilamentCostPerKg = _appSettingsService.Settings.FilamentCostPerKg;

        // Initialize lists
        PrintStatusOptions = new ObservableCollection<PrintStatus>(Enum.GetValues<PrintStatus>());
        RatingOptions = new ObservableCollection<int>(Enumerable.Range(1, 5));
    }

    [ObservableProperty]
    private ObservableCollection<PrintStatus> _printStatusOptions;

    [ObservableProperty]
    private ObservableCollection<int> _ratingOptions;

    [RelayCommand]
    private async Task SaveGcodeAsync(Gcode gcode)
    {
        if (gcode == null) return;
        try
        {
            await _unitOfWork.Gcodes.UpdateAsync(gcode);
            await _unitOfWork.SaveChangesAsync();
            _logger?.LogInformation("G-code updated: {FileName}", gcode.OriginalFileName);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save G-code");
        }
    }

    [ObservableProperty]
    private decimal _filamentCostPerKg;

    public async Task LoadModelAsync(int modelId)
    {
        await ExecuteBusyAsync(async () =>
        {
            // Load model with details
            var model = await _unitOfWork.Models.GetByIdAsync(modelId);
            if (model == null) return;

            Model = model;
            ModelName = model.Name ?? string.Empty;
            Notes = model.Notes ?? string.Empty;
            Tags = model.Tags ?? string.Empty;
            SourceUrl = model.SourceUrl ?? string.Empty;
            FileSizeFormatted = FormatFileSize(model.FileSize);

            // Load categories
            var categories = await _unitOfWork.Categories.GetAllAsync();
            Categories = new ObservableCollection<Category>(categories);
            SelectedCategory = Categories.FirstOrDefault(c => c.Id == model.CategoryId);

            // Load associated G-codes
            if (model.Id > 0)
            {
                var gcodes = await _unitOfWork.Gcodes.GetByModelIdAsync(model.Id);
                Gcodes = new ObservableCollection<Gcode>(gcodes);
            }

            // Load tag suggestions from all models
            await LoadTagSuggestionsAsync();
        });
    }

    [RelayCommand]
    private async Task SaveChangesAsync()
    {
        if (Model == null) return;

        // Check if category or tags changed to record learning
        bool categoryChanged = Model.CategoryId != SelectedCategory?.Id;
        bool tagsChanged = Model.Tags != Tags;

        Model.Name = ModelName;
        Model.Notes = Notes;
        Model.Tags = Tags;
        Model.SourceUrl = string.IsNullOrWhiteSpace(SourceUrl) ? null : SourceUrl.Trim();
        Model.CategoryId = SelectedCategory?.Id;
        Model.LastModifiedDate = DateTime.UtcNow;

        await _unitOfWork.Models.UpdateAsync(Model);
        await _unitOfWork.SaveChangesAsync();

        // Learn from user corrections
        if (categoryChanged || tagsChanged)
        {
            try
            {
                await _tagLearningService.RecordCorrectionAsync(
                    Model.OriginalFileName, 
                    SelectedCategory?.Name, 
                    Tags);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to record learning correction");
            }
        }

        ModelUpdated?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void OpenSourceUrl()
    {
        if (string.IsNullOrWhiteSpace(SourceUrl))
            return;

        try
        {
            var url = SourceUrl.Trim();
            // Add https if no protocol specified
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to open source URL: {Url}", SourceUrl);
        }
    }

    /// <summary>
    /// Loads popular tag suggestions from existing models.
    /// Optimized to only fetch Tags column from database.
    /// </summary>
    private async Task LoadTagSuggestionsAsync()
    {
        try
        {
            // Only fetch the Tags column instead of full Model entities
            var allTags = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                _unitOfWork.Models.Query()
                    .Where(m => m.Tags != null && m.Tags != "")
                    .Select(m => m.Tags!));

            var topTags = allTags
                .SelectMany(tags => tags.Split(',', StringSplitOptions.RemoveEmptyEntries))
                .Select(t => t.Trim().ToLowerInvariant())
                .Where(t => !string.IsNullOrEmpty(t))
                .GroupBy(t => t)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => g.Key)
                .ToList();

            TagSuggestions = new ObservableCollection<string>(topTags);
        }
        catch
        {
            // Ignore errors in tag loading
        }
    }

    /// <summary>
    /// Adds a tag to the current tags list.
    /// </summary>
    public void AddTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return;

        var currentTags = string.IsNullOrWhiteSpace(Tags) 
            ? new List<string>() 
            : Tags.Split(',').Select(t => t.Trim().ToLowerInvariant()).ToList();

        if (!currentTags.Contains(tag.ToLowerInvariant()))
        {
            currentTags.Add(tag.Trim());
            Tags = string.Join(", ", currentTags);
        }
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        if (Model == null) return;

        Model.IsFavorite = !Model.IsFavorite;
        await _unitOfWork.Models.UpdateAsync(Model);
        await _unitOfWork.SaveChangesAsync();
        
        OnPropertyChanged(nameof(Model));
        ModelUpdated?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void OpenFileLocation()
    {
        if (Model == null || string.IsNullOrEmpty(Model.FilePath)) return;

        var directory = Path.GetDirectoryName(Model.FilePath);
        if (Directory.Exists(directory))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{Model.FilePath}\"",
                UseShellExecute = true
            });
        }
    }

    [RelayCommand]
    private void OpenInDefaultApp()
    {
        if (Model == null || !File.Exists(Model.FilePath)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Model.FilePath,
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Directly loads the 3D model without toggle logic.
    /// Called automatically when detail dialog opens.
    /// </summary>
    public async Task Load3DModelDirectAsync()
    {
        if (Model == null || string.IsNullOrEmpty(Model.FilePath) || !File.Exists(Model.FilePath))
            return;

        IsLoading3D = true;

        try
        {
            await Task.Run(() =>
            {
                var reader = new StLReader();
                try 
                {
                   var modelGroup = reader.Read(Model.FilePath);
                   var geometryModel = modelGroup.Children.FirstOrDefault() as GeometryModel3D;
                   
                   if (geometryModel != null)
                   {
                       var geometry = geometryModel.Geometry;
                       geometry.Freeze();

                       // Update UI on main thread
                       App.Current.Dispatcher.Invoke(() =>
                       {
                           ModelGeometry = geometry;
                           if (geometry is MeshGeometry3D mesh)
                           {
                               TriangleCount = mesh.TriangleIndices.Count / 3;
                               VertexCount = mesh.Positions.Count;
                               GeometryInfo = $"{TriangleCount:N0} Triangles | {VertexCount:N0} Vertices";
                           }
                       });
                   }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error reading STL file: {FilePath}", Model.FilePath);
                    // Ideally check for file type support or corruption here
                }
            });
        }
        finally
        {
            IsLoading3D = false;
        }
    }

    partial void OnSelectedCategoryChanged(Category? value)
    {
        if (Model != null && value != null)
        {
            Model.CategoryId = value.Id;
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        int order = 0;
        double size = (double)bytes;
        
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Fetches all G-codes that are not currently linked to any model.
    /// </summary>
    public async Task<List<Gcode>> GetUnlinkedGcodesAsync()
    {
        // We'll add this method to UnitOfWork/Repository next
        var allGcodes = await _unitOfWork.Gcodes.GetAllAsync();
        return allGcodes.Where(g => g.ModelId == null).OrderByDescending(g => g.AddedDate).ToList();
    }

    /// <summary>
    /// Links a G-code to the current model.
    /// </summary>
    public async Task LinkGcodeAsync(Gcode gcode)
    {
        if (Model == null || gcode == null) return;

        try
        {
            gcode.ModelId = Model.Id;
            await _unitOfWork.Gcodes.UpdateAsync(gcode);
            await _unitOfWork.SaveChangesAsync();
            
            // Refresh list
            Gcodes.Add(gcode);
            // Optionally reload to be safe
            // await LoadModelAsync(Model.Id); 
            
            _logger?.LogInformation("Linked G-code {Gcode} to Model {Model}", gcode.OriginalFileName, Model.Name);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to link G-code");
        }
    }
    [RelayCommand]
    private async Task UnlinkGcodeAsync(Gcode gcode)
    {
        if (gcode == null) return;
        
        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to unlink '{gcode.OriginalFileName}'?\n\nThis will NOT delete the file from disk.",
            "Unlink G-code",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        try 
        {
            gcode.ModelId = null;
            await _unitOfWork.Gcodes.UpdateAsync(gcode);
            await _unitOfWork.SaveChangesAsync();
            
            Gcodes.Remove(gcode);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to unlink G-code");
            System.Windows.MessageBox.Show("Failed to unlink G-code.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void CopyGcodePath(Gcode gcode)
    {
        if (gcode == null || string.IsNullOrEmpty(gcode.FilePath)) return;
        
        try
        {
            System.Windows.Clipboard.SetText(gcode.FilePath);
        }
        catch
        {
            // Ignore clipboard errors
        }
    }

    [RelayCommand]
    private void OpenGcodeFolder(Gcode gcode)
    {
        if (gcode == null || string.IsNullOrEmpty(gcode.FilePath)) return;

        try
        {
            var folder = Path.GetDirectoryName(gcode.FilePath);
            if (Directory.Exists(folder))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{gcode.FilePath}\"",
                    UseShellExecute = true
                });
            }
        }
        catch { }
    }

    [RelayCommand]
    private void OpenGcodeFile(Gcode gcode)
    {
        if (gcode == null || !File.Exists(gcode.FilePath)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = gcode.FilePath,
                UseShellExecute = true
            });
        }
        catch { }
    }

    [RelayCommand]
    private async Task RefreshGcodeMetadataAsync(Gcode gcode)
    {
        if (gcode == null || !File.Exists(gcode.FilePath)) return;

        try
        {
            var metadata = await _gcodeParserService.ParseAsync(gcode.FilePath);
            
            // Update properties
            gcode.PrintTime = metadata.PrintTime;
            gcode.FilamentLength = metadata.FilamentUsedMm; // Note: Model has FilamentLength (m) or mm? Parser returns mm. Model comment says "meters". 
            // Wait, Gcode.FilamentLength comment says "meters". Metadata.FilamentUsedMm is mm.
            // Let's check Gcode.cs again. 
            // public double? FilamentLength { get; set; } /// Estimated filament length in meters.
            // Metadata.FilamentUsedMm is in mm. So divide by 1000.
            if (metadata.FilamentUsedMm.HasValue) gcode.FilamentLength = metadata.FilamentUsedMm.Value / 1000.0;
            
            gcode.FilamentWeight = metadata.FilamentUsedGrams;
            gcode.SlicerName = metadata.SlicerName;
            gcode.SlicerVersion = metadata.SlicerVersion;
            gcode.LayerHeight = metadata.LayerHeight;
            gcode.NozzleDiameter = metadata.NozzleDiameter;
            gcode.InfillPercentage = metadata.InfillPercentage;
            gcode.NozzleTemp = metadata.NozzleTemp;
            gcode.BedTemp = metadata.BedTemp;

            await _unitOfWork.Gcodes.UpdateAsync(gcode);
            await _unitOfWork.SaveChangesAsync();
            
            // Trigger refresh
            var index = Gcodes.IndexOf(gcode);
            if (index != -1)
            {
                Gcodes[index] = gcode; // Force list update
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to refresh G-code metadata");
        }
    }
}

