using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using PrintVault3D.Models;
using PrintVault3D.Repositories;

namespace PrintVault3D.Views;

public partial class GcodeManagementDialog : Window
{
    private readonly IUnitOfWork _unitOfWork;
    private List<Gcode> _allGcodes = new();
    private List<Model3D> _allModels = new();

    public GcodeManagementDialog()
    {
        InitializeComponent();
        _unitOfWork = App.Services.GetRequiredService<IUnitOfWork>();
        Loaded += async (s, e) => await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        _allGcodes = (await _unitOfWork.Gcodes.GetAllWithModelsAsync()).ToList();
        _allModels = (await _unitOfWork.Models.GetAllAsync()).ToList();
        ApplyFilter();
        UpdateStats();
    }

    private void UpdateStats()
    {
        var total = _allGcodes.Count;
        var linked = _allGcodes.Count(g => g.ModelId != null);
        var unlinked = total - linked;
        StatsText.Text = $"{total} total • {linked} linked • {unlinked} unlinked";
    }

    private void ApplyFilter()
    {
        if (GcodeListView == null) return;
        
        var searchText = SearchBox?.Text?.ToLowerInvariant() ?? "";
        
        IEnumerable<Gcode> filtered = _allGcodes;

        // Apply filter
        if (FilterLinked?.IsChecked == true)
            filtered = filtered.Where(g => g.ModelId != null);
        else if (FilterUnlinked?.IsChecked == true)
            filtered = filtered.Where(g => g.ModelId == null);

        // Apply search
        if (!string.IsNullOrWhiteSpace(searchText))
            filtered = filtered.Where(g => 
                (g.OriginalFileName?.ToLowerInvariant().Contains(searchText) ?? false) ||
                (g.SlicerName?.ToLowerInvariant().Contains(searchText) ?? false));

        GcodeListView.ItemsSource = filtered.OrderByDescending(g => g.AddedDate).ToList();
    }

    private void Filter_Changed(object sender, RoutedEventArgs e) => ApplyFilter();
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private async void LinkToModel_Click(object sender, RoutedEventArgs e)
    {
        var selectedGcodes = GcodeListView.SelectedItems.Cast<Gcode>().ToList();
        if (!selectedGcodes.Any())
        {
            System.Windows.MessageBox.Show("Please select at least one G-code to link.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Show model selection dialog
        var dialog = new ModelSelectionDialog(_allModels)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && dialog.SelectedModel != null)
        {
            foreach (var gcode in selectedGcodes)
            {
                gcode.ModelId = dialog.SelectedModel.Id;
                gcode.Model = dialog.SelectedModel;
                await _unitOfWork.Gcodes.UpdateAsync(gcode);
            }
            await _unitOfWork.SaveChangesAsync();
            await LoadDataAsync();
        }
    }

    private async void Unlink_Click(object sender, RoutedEventArgs e)
    {
        var selectedGcodes = GcodeListView.SelectedItems.Cast<Gcode>().Where(g => g.ModelId != null).ToList();
        if (!selectedGcodes.Any())
        {
            System.Windows.MessageBox.Show("Please select linked G-codes to unlink.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        foreach (var gcode in selectedGcodes)
        {
            gcode.ModelId = null;
            gcode.Model = null;
            await _unitOfWork.Gcodes.UpdateAsync(gcode);
        }
        await _unitOfWork.SaveChangesAsync();
        await LoadDataAsync();
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        var selectedGcodes = GcodeListView.SelectedItems.Cast<Gcode>().ToList();
        if (!selectedGcodes.Any())
        {
            System.Windows.MessageBox.Show("Please select G-codes to delete.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to delete {selectedGcodes.Count} G-code(s)?\nThis will only remove them from the database, not the files.",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            foreach (var gcode in selectedGcodes)
            {
                await _unitOfWork.Gcodes.DeleteAsync(gcode.Id);
            }
            await _unitOfWork.SaveChangesAsync();
            await LoadDataAsync();
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
