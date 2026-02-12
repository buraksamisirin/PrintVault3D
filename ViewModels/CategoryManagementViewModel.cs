using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PrintVault3D.Models;
using PrintVault3D.Repositories;

namespace PrintVault3D.ViewModels;

/// <summary>
/// ViewModel for Category Management dialog.
/// </summary>
public partial class CategoryManagementViewModel : ViewModelBase
{
    private readonly IUnitOfWork _unitOfWork;

    [ObservableProperty]
    private ObservableCollection<CategoryItem> _categories = new();

    [ObservableProperty]
    private CategoryItem? _selectedCategory;

    [ObservableProperty]
    private string _newCategoryName = string.Empty;

    [ObservableProperty]
    private string _newCategoryKeywords = string.Empty;

    [ObservableProperty]
    private string _editName = string.Empty;

    [ObservableProperty]
    private string _editKeywords = string.Empty;

    [ObservableProperty]
    private string _editDescription = string.Empty;

    [ObservableProperty]
    private bool _isEditing;

    public event EventHandler? CloseRequested;
    public event EventHandler? CategoriesChanged;

    public CategoryManagementViewModel(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task LoadCategoriesAsync()
    {
        var categoriesWithCounts = await _unitOfWork.Categories.GetAllWithModelCountsAsync();
        Categories = new ObservableCollection<CategoryItem>(
            categoriesWithCounts.Select(c => new CategoryItem
            {
                Category = c.Category,
                ModelCount = c.ModelCount
            }));
    }

    [RelayCommand]
    private async Task AddCategoryAsync()
    {
        if (string.IsNullOrWhiteSpace(NewCategoryName))
            return;

        var category = new Category
        {
            Name = NewCategoryName.Trim(),
            AutoKeywords = NewCategoryKeywords.Trim(),
            Description = string.Empty
        };

        await _unitOfWork.Categories.AddAsync(category);
        await _unitOfWork.SaveChangesAsync();

        Categories.Add(new CategoryItem { Category = category, ModelCount = 0 });

        NewCategoryName = string.Empty;
        NewCategoryKeywords = string.Empty;

        CategoriesChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void StartEdit()
    {
        if (SelectedCategory == null) return;

        EditName = SelectedCategory.Category.Name;
        EditKeywords = SelectedCategory.Category.AutoKeywords ?? string.Empty;
        EditDescription = SelectedCategory.Category.Description ?? string.Empty;
        IsEditing = true;
    }

    [RelayCommand]
    private async Task SaveEditAsync()
    {
        if (SelectedCategory == null || string.IsNullOrWhiteSpace(EditName))
            return;

        SelectedCategory.Category.Name = EditName.Trim();
        SelectedCategory.Category.AutoKeywords = EditKeywords.Trim();
        SelectedCategory.Category.Description = EditDescription.Trim();

        await _unitOfWork.Categories.UpdateAsync(SelectedCategory.Category);
        await _unitOfWork.SaveChangesAsync();

        IsEditing = false;
        
        // Refresh the list
        await LoadCategoriesAsync();
        CategoriesChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
        EditName = string.Empty;
        EditKeywords = string.Empty;
        EditDescription = string.Empty;
    }

    [RelayCommand]
    private async Task DeleteCategoryAsync()
    {
        if (SelectedCategory == null) return;

        // Don't allow deleting "Uncategorized"
        if (SelectedCategory.Category.Name == "Uncategorized")
            return;

        await _unitOfWork.Categories.DeleteAsync(SelectedCategory.Category);
        await _unitOfWork.SaveChangesAsync();

        Categories.Remove(SelectedCategory);
        SelectedCategory = null;

        CategoriesChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Wrapper for Category with model count.
/// </summary>
public class CategoryItem : ObservableObject
{
    public Category Category { get; set; } = null!;
    public int ModelCount { get; set; }
}

