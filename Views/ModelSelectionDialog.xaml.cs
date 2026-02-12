using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PrintVault3D.Models;

namespace PrintVault3D.Views;

public partial class ModelSelectionDialog : Window
{
    private readonly List<Model3D> _allModels;
    
    public Model3D? SelectedModel { get; private set; }

    public ModelSelectionDialog(List<Model3D> models)
    {
        InitializeComponent();
        _allModels = models.OrderBy(m => m.Name).ToList();
        ModelListView.ItemsSource = _allModels;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var searchText = SearchBox.Text?.ToLowerInvariant() ?? "";
        
        if (string.IsNullOrWhiteSpace(searchText))
        {
            ModelListView.ItemsSource = _allModels;
        }
        else
        {
            ModelListView.ItemsSource = _allModels
                .Where(m => 
                    (m.Name?.ToLowerInvariant().Contains(searchText) ?? false) ||
                    (m.OriginalFileName?.ToLowerInvariant().Contains(searchText) ?? false))
                .ToList();
        }
    }

    private void ModelListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ModelListView.SelectedItem is Model3D model)
        {
            SelectedModel = model;
            DialogResult = true;
            Close();
        }
    }

    private void Select_Click(object sender, RoutedEventArgs e)
    {
        if (ModelListView.SelectedItem is Model3D model)
        {
            SelectedModel = model;
            DialogResult = true;
            Close();
        }
        else
        {
            System.Windows.MessageBox.Show("Please select a model.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
