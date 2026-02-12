using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PrintVault3D.Helpers;
using PrintVault3D.Models;
using PrintVault3D.ViewModels;
using UserControl = System.Windows.Controls.UserControl;
using DragEventArgs = System.Windows.DragEventArgs;
using DragDropEffects = System.Windows.DragDropEffects;
using DataFormats = System.Windows.DataFormats;
using MessageBox = System.Windows.MessageBox;

namespace PrintVault3D.Views;

public partial class SidebarView : UserControl
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;
    private readonly ILogger<SidebarView>? _logger;

    public SidebarView()
    {
        InitializeComponent();
        if (App.Services != null)
        {
            _logger = App.Services.GetService<ILogger<SidebarView>>();
        }
    }

    #region Drag and Drop Handlers for Sidebar

    private void CategoryItem_DragEnter(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            DragDropHelper.SetIsDragOver(element, true);
            e.Handled = true;
        }
    }

    private void CategoryItem_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            DragDropHelper.SetIsDragOver(element, false);
            e.Handled = true;
        }
    }

    private void CategoryItem_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.None;
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        e.Handled = true;
    }

    private void CategoryItem_Drop(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            DragDropHelper.SetIsDragOver(element, false);
            
            if (ViewModel != null && element.DataContext is Category category && 
                e.Data.GetDataPresent("ModelList"))
            {
                var models = e.Data.GetData("ModelList") as List<Model3D>;
                if (models != null && models.Any())
                {
                    ViewModel.AssignCategoryCommand.Execute((category, models));
                    e.Handled = true;
                }
            }
        }
    }

    private void CollectionItem_DragEnter(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            DragDropHelper.SetIsDragOver(element, true);
            e.Handled = true;
        }
    }

    private void CollectionItem_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            DragDropHelper.SetIsDragOver(element, false);
            e.Handled = true;
        }
    }

    private void CollectionItem_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.None;
        if (e.Data.GetDataPresent("ModelList"))
        {
            e.Effects = DragDropEffects.Copy;
        }
        e.Handled = true;
    }

    private void CollectionItem_Drop(object sender, DragEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            DragDropHelper.SetIsDragOver(element, false);
            
            if (ViewModel != null && element.DataContext is Collection collection && 
                e.Data.GetDataPresent("ModelList"))
            {
                var models = e.Data.GetData("ModelList") as List<Model3D>;
                if (models != null && models.Any())
                {
                    ViewModel.AssignCollectionCommand.Execute((collection, models));
                    e.Handled = true;
                }
            }
        }
    }

    #endregion

    #region Collection Context Menu

    private void RenameCollection_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is Collection collection)
        {
            var dialog = new CreateCollectionDialog(collection.Id);
            dialog.Owner = Window.GetWindow(this);
            dialog.CollectionUpdated += async (s, c) => 
            {
                if (ViewModel != null)
                {
                    await ViewModel.ReloadCollectionsCommand.ExecuteAsync(null);
                }
            };
            dialog.ShowDialog();
        }
    }

    private async void DuplicateCollection_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel != null && sender is MenuItem menuItem && menuItem.Tag is Collection collection)
            {
                await ViewModel.DuplicateCollectionAsync(collection);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error duplicating collection");
        }
    }

    private async void DeleteCollection_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ViewModel != null && sender is MenuItem menuItem && menuItem.Tag is Collection collection)
            {
                var result = MessageBox.Show(
                    $"'{collection.Name}' koleksiyonunu silmek istediğinize emin misiniz?\n\nModeller silinmeyecek, sadece koleksiyon kaldırılacak.",
                    "Koleksiyonu Sil",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    await ViewModel.DeleteCollectionAsync(collection);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting collection");
        }
    }

    #endregion
}
