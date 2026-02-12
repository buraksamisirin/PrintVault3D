using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using PrintVault3D.Models;
using PrintVault3D.Repositories;

namespace PrintVault3D.ViewModels;

public partial class CreateCollectionViewModel : ViewModelBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateCollectionViewModel>? _logger;
    
    private int? _editingCollectionId;
    
    public event EventHandler? RequestClose;
    public event EventHandler<Collection>? CollectionCreated;
    public event EventHandler<Collection>? CollectionUpdated;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;
    
    [ObservableProperty]
    private string _selectedColor = "#00D9FF"; // Default cyan
    
    [ObservableProperty]
    private bool _isPinned;
    
    [ObservableProperty]
    private bool _isEditMode;
    
    [ObservableProperty]
    private int _modelCount;

    public CreateCollectionViewModel(IUnitOfWork unitOfWork, ILogger<CreateCollectionViewModel>? logger = null)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }
    
    public async Task LoadCollectionAsync(int collectionId)
    {
        try
        {
            var collection = await _unitOfWork.Collections.GetDetailsAsync(collectionId);
            if (collection != null)
            {
                _editingCollectionId = collectionId;
                Name = collection.Name;
                Description = collection.Description ?? string.Empty;
                SelectedColor = collection.Color ?? "#00D9FF";
                IsPinned = collection.IsPinned;
                ModelCount = collection.Models?.Count ?? 0;
                IsEditMode = true;
                
                _logger?.LogInformation("Loaded collection for editing: {Name} (ID: {Id})", collection.Name, collectionId);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading collection {Id}", collectionId);
        }
    }

    private bool CanSave() => !string.IsNullOrWhiteSpace(Name);

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name)) return;

        try
        {
            if (_editingCollectionId.HasValue)
            {
                // Update existing collection
                var collection = await _unitOfWork.Collections.GetDetailsAsync(_editingCollectionId.Value);
                if (collection != null)
                {
                    collection.Name = Name.Trim();
                    collection.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
                    collection.Color = SelectedColor;
                    collection.IsPinned = IsPinned;
                    collection.LastModifiedDate = DateTime.UtcNow;
                    
                    await _unitOfWork.SaveChangesAsync();
                    
                    _logger?.LogInformation("Collection updated: {Name}", collection.Name);
                    CollectionUpdated?.Invoke(this, collection);
                }
            }
            else
            {
                // Create new collection
                _logger?.LogInformation("Creating new collection: {Name}", Name);

                var collection = new Collection
                {
                    Name = Name.Trim(),
                    Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                    Color = SelectedColor,
                    IsPinned = IsPinned,
                    CreatedDate = DateTime.UtcNow,
                    LastModifiedDate = DateTime.UtcNow
                };

                await _unitOfWork.Collections.AddAsync(collection);
                await _unitOfWork.SaveChangesAsync();
                
                _logger?.LogInformation("Collection created with ID: {Id}", collection.Id);
                CollectionCreated?.Invoke(this, collection);
            }
            
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving collection");
        }
    }
    
    public async Task DeleteCollectionAsync(int collectionId)
    {
        try
        {
            var collection = await _unitOfWork.Collections.GetDetailsAsync(collectionId);
            if (collection != null)
            {
                await _unitOfWork.Collections.DeleteAsync(collection);
                await _unitOfWork.SaveChangesAsync();
                _logger?.LogInformation("Collection deleted: {Name}", collection.Name);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting collection {Id}", collectionId);
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
