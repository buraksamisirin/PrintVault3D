using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using PrintVault3D.Models;
using PrintVault3D.Services;

namespace PrintVault3D.Views;

public partial class DuplicatesDialog : Window
{
    private readonly IVaultService _vaultService;
    private readonly ObservableCollection<Model3D> _models;
    private readonly string _fileHash;

    public event EventHandler? ModelDeleted;

    public DuplicatesDialog(string fileHash, IEnumerable<Model3D> duplicates)
    {
        InitializeComponent();
        
        _fileHash = fileHash;
        _models = new ObservableCollection<Model3D>(duplicates);
        _vaultService = App.Services.GetRequiredService<IVaultService>();

        DataContext = new
        {
            FileHash = _fileHash,
            Models = _models
        };
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.DataContext is Model3D model)
        {
            if (string.IsNullOrEmpty(model.FilePath)) return;

            var directory = Path.GetDirectoryName(model.FilePath);
            if (Directory.Exists(directory))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{model.FilePath}\"",
                    UseShellExecute = true
                });
            }
        }
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.DataContext is Model3D model)
        {
            var result = System.Windows.MessageBox.Show($"Are you sure you want to delete '{model.Name}'?\nThis cannot be undone.", 
                "Confirm Delete", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                try
                {
                    var success = await _vaultService.DeleteModelAsync(model.Id);
                    if (success)
                    {
                        _models.Remove(model);
                        ModelDeleted?.Invoke(this, EventArgs.Empty);

                        if (_models.Count == 0)
                        {
                            Close();
                        }
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("Failed to delete model.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Error deleting model: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }
    }
}
