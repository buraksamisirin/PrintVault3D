using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using PrintVault3D.Repositories;
using PrintVault3D.ViewModels;

namespace PrintVault3D.Views;

/// <summary>
/// Category management dialog window.
/// </summary>
public partial class CategoryManagementDialog : Window
{
    private readonly CategoryManagementViewModel _viewModel;

    public event EventHandler? CategoriesChanged;

    public CategoryManagementDialog()
    {
        InitializeComponent();

        var unitOfWork = App.Services.GetRequiredService<IUnitOfWork>();

        _viewModel = new CategoryManagementViewModel(unitOfWork);
        _viewModel.CloseRequested += (s, e) => Close();
        _viewModel.CategoriesChanged += (s, e) => CategoriesChanged?.Invoke(this, EventArgs.Empty);

        DataContext = _viewModel;

        Loaded += async (s, e) => await _viewModel.LoadCategoriesAsync();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }
}

