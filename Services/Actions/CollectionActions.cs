using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PrintVault3D.Models;
using PrintVault3D.Repositories;

namespace PrintVault3D.Services.Actions;

public class AddModelsToCollectionAction : IUndoableAction
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly int _collectionId;
    private readonly List<int> _modelIds;
    private readonly string _collectionName;
    private readonly System.Action? _onCompleted;

    public string Description => $"Add {_modelIds.Count} models to '{_collectionName}'";

    public AddModelsToCollectionAction(IUnitOfWork unitOfWork, Collection collection, List<Model3D> models, System.Action? onCompleted = null)
    {
        _unitOfWork = unitOfWork;
        _collectionId = collection.Id;
        _collectionName = collection.Name;
        _modelIds = models.Select(m => m.Id).ToList();
        _onCompleted = onCompleted;
    }

    public async Task ExecuteAsync()
    {
        var collection = await _unitOfWork.Collections.GetDetailsAsync(_collectionId);
        if (collection == null) return;
        
        if (collection.Models == null) collection.Models = new List<Model3D>();

        var models = await _unitOfWork.Models.GetByIdsAsync(_modelIds);
        bool changed = false;

        foreach (var model in models)
        {
            if (!collection.Models.Any(m => m.Id == model.Id))
            {
                collection.Models.Add(model);
                changed = true;
            }
        }

        if (changed)
        {
            await _unitOfWork.SaveChangesAsync();
            _onCompleted?.Invoke();
        }
    }

    public async Task UndoAsync()
    {
        var collection = await _unitOfWork.Collections.GetDetailsAsync(_collectionId);
        if (collection == null || collection.Models == null) return;

        bool changed = false;
        foreach (var id in _modelIds)
        {
            var model = collection.Models.FirstOrDefault(m => m.Id == id);
            if (model != null)
            {
                collection.Models.Remove(model);
                changed = true;
            }
        }

        if (changed)
        {
            await _unitOfWork.SaveChangesAsync();
            _onCompleted?.Invoke();
        }
    }
}

public class RemoveModelsFromCollectionAction : IUndoableAction
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly int _collectionId;
    private readonly List<int> _modelIds;
    private readonly string _collectionName;
    private readonly System.Action? _onCompleted;

    public string Description => $"Remove {_modelIds.Count} models from '{_collectionName}'";

    public RemoveModelsFromCollectionAction(IUnitOfWork unitOfWork, Collection collection, List<Model3D> models, System.Action? onCompleted = null)
    {
        _unitOfWork = unitOfWork;
        _collectionId = collection.Id;
        _collectionName = collection.Name;
        _modelIds = models.Select(m => m.Id).ToList();
        _onCompleted = onCompleted;
    }

    public async Task ExecuteAsync()
    {
        // Remove logic (same as Undo of Add)
        var collection = await _unitOfWork.Collections.GetDetailsAsync(_collectionId);
        if (collection == null || collection.Models == null) return;

        bool changed = false;
        foreach (var id in _modelIds)
        {
            var model = collection.Models.FirstOrDefault(m => m.Id == id);
            if (model != null)
            {
                collection.Models.Remove(model);
                changed = true;
            }
        }

        if (changed)
        {
            await _unitOfWork.SaveChangesAsync();
            _onCompleted?.Invoke();
        }
    }

    public async Task UndoAsync()
    {
        // Add logic (same as Execute of Add)
        var collection = await _unitOfWork.Collections.GetDetailsAsync(_collectionId);
        if (collection == null) return;
        
        if (collection.Models == null) collection.Models = new List<Model3D>();

        var models = await _unitOfWork.Models.GetByIdsAsync(_modelIds);
        bool changed = false;

        foreach (var model in models)
        {
            if (!collection.Models.Any(m => m.Id == model.Id))
            {
                collection.Models.Add(model);
                changed = true;
            }
        }

        if (changed)
        {
            await _unitOfWork.SaveChangesAsync();
            _onCompleted?.Invoke();
        }
    }
}

public class AssignCategoryAction : IUndoableAction
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly List<int> _modelIds;
    private readonly int _newCategoryId;
    private readonly string _newCategoryName;
    private readonly Dictionary<int, int?> _previousCategoryIds = new();
    private readonly System.Action? _onCompleted;

    public string Description => $"Move {_modelIds.Count} models to '{_newCategoryName}'";

    public AssignCategoryAction(IUnitOfWork unitOfWork, List<Model3D> models, int newCategoryId, string newCategoryName, System.Action? onCompleted = null)
    {
        _unitOfWork = unitOfWork;
        _modelIds = models.Select(m => m.Id).ToList();
        _newCategoryId = newCategoryId;
        _newCategoryName = newCategoryName;
        _onCompleted = onCompleted;
        
        // Snapshot previous state
        foreach (var model in models)
        {
            _previousCategoryIds[model.Id] = model.CategoryId;
        }
    }

    public async Task ExecuteAsync()
    {
        var models = await _unitOfWork.Models.GetByIdsAsync(_modelIds);
        bool changed = false;
        
        foreach (var model in models)
        {
            if (model.CategoryId != _newCategoryId)
            {
                model.CategoryId = _newCategoryId;
                changed = true;
            }
        }
        
        if (changed)
        {
            await _unitOfWork.SaveChangesAsync();
            _onCompleted?.Invoke();
        }
    }

    public async Task UndoAsync()
    {
        var models = await _unitOfWork.Models.GetByIdsAsync(_modelIds);
        bool changed = false;

        foreach (var model in models)
        {
            if (_previousCategoryIds.ContainsKey(model.Id))
            {
                var prevCatId = _previousCategoryIds[model.Id];
                if (model.CategoryId != prevCatId)
                {
                    model.CategoryId = prevCatId;
                    changed = true;
                }
            }
        }
        
        if (changed)
        {
            await _unitOfWork.SaveChangesAsync();
            _onCompleted?.Invoke();
        }
    }
}
