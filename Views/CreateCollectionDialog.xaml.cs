using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using PrintVault3D.ViewModels;
using PrintVault3D.Models;

namespace PrintVault3D.Views;

public partial class CreateCollectionDialog : Window
{
    private readonly CreateCollectionViewModel _viewModel;
    private readonly int? _editingCollectionId;

    public event EventHandler<Collection>? CollectionCreated;
    public event EventHandler<Collection>? CollectionUpdated;
    public event EventHandler<int>? CollectionDeleted;

    // Constructor for creating new collection
    public CreateCollectionDialog()
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<CreateCollectionViewModel>();
        DataContext = _viewModel;

        _viewModel.RequestClose += (s, e) => Close();
        _viewModel.CollectionCreated += (s, collection) => CollectionCreated?.Invoke(this, collection);
        
        Loaded += (s, e) => 
        {
            NameTextBox.Focus();
            UpdateColorSelection();
        };
    }
    
    // Constructor for editing existing collection
    public CreateCollectionDialog(int collectionId) : this()
    {
        _editingCollectionId = collectionId;
        
        // Update UI for edit mode
        DialogTitleText.Text = "Edit Collection";
        SaveButton.Content = "Save";
        EditModeButtons.Visibility = Visibility.Visible;
        
        // Load collection data
        Loaded += async (s, e) =>
        {
            await _viewModel.LoadCollectionAsync(collectionId);
            UpdateColorSelection();
        };
        
        _viewModel.CollectionUpdated += (s, collection) => CollectionUpdated?.Invoke(this, collection);
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
    
    private void ColorButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.Background is SolidColorBrush brush)
        {
            _viewModel.SelectedColor = brush.Color.ToString();
            UpdateColorSelection();
        }
    }
    
    private void UpdateColorSelection()
    {
        foreach (var child in ColorPanel.Children)
        {
            if (child is System.Windows.Controls.Button button && button.Background is SolidColorBrush brush)
            {
                var colorHex = brush.Color.ToString();
                button.Tag = colorHex == _viewModel.SelectedColor ? "Selected" : null;
            }
        }
    }
    
    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_editingCollectionId == null) return;
            
            var result = System.Windows.MessageBox.Show(
                "Are you sure you want to delete this collection?\n\nThe models will not be deleted, only the collection.",
                "Delete Collection",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
                
            if (result == MessageBoxResult.Yes)
            {
                await _viewModel.DeleteCollectionAsync(_editingCollectionId.Value);
                CollectionDeleted?.Invoke(this, _editingCollectionId.Value);
                Close();
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error deleting collection: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
