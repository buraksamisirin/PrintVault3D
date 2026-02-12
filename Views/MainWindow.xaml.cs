using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PrintVault3D.Helpers;
using PrintVault3D.Models;
using PrintVault3D.Services;
using PrintVault3D.ViewModels;
using System.Collections.Generic;
using System.Linq;

using DragEventArgs = System.Windows.DragEventArgs;
using DragDropEffects = System.Windows.DragDropEffects;
using DataFormats = System.Windows.DataFormats;
using MessageBox = System.Windows.MessageBox;

namespace PrintVault3D.Views;

/// <summary>
/// Main window for PrintVault 3D application.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ILogger<MainWindow>? _logger;
    private Model3D? _lastInteractedModel;

    public MainWindow()
    {
        InitializeComponent();
        
        _viewModel = App.Services.GetRequiredService<MainViewModel>();
        _logger = App.Services.GetService<ILogger<MainWindow>>();
        DataContext = _viewModel;

        Loaded += MainWindow_Loaded;
        StateChanged += MainWindow_StateChanged;
        Closing += MainWindow_Closing;

        // Restore window position from settings
        RestoreWindowPosition();
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Enable Mica effect for modern Windows 11 look
            // Initialize View Model
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during MainWindow initialization");
        }
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        // Handle minimize to tray
        if (WindowState == WindowState.Minimized)
        {
            var settings = App.SettingsService.Settings;
            if (settings.MinimizeToTray)
            {
                var app = (App)System.Windows.Application.Current;
                app.MinimizeToTray();
            }
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        var app = (App)System.Windows.Application.Current;
        var settings = App.SettingsService.Settings;

        // If minimize to tray is enabled and not explicitly exiting, minimize instead of close
        if (settings.MinimizeToTray && !app.IsExiting)
        {
            e.Cancel = true;
            app.MinimizeToTray();
            return;
        }

        // Unsubscribe from events to prevent memory leaks
        Loaded -= MainWindow_Loaded;
        StateChanged -= MainWindow_StateChanged;
        Closing -= MainWindow_Closing;
        
        // Dispose ViewModel to clean up event subscriptions
        _viewModel?.Dispose();

        // Save window position before closing
        SaveWindowPosition();
    }

    private void RestoreWindowPosition()
    {
        var settings = App.SettingsService.Settings;

        if (settings.WindowWidth.HasValue && settings.WindowHeight.HasValue)
        {
            Width = settings.WindowWidth.Value;
            Height = settings.WindowHeight.Value;
        }

        if (settings.WindowLeft.HasValue && settings.WindowTop.HasValue)
        {
            Left = settings.WindowLeft.Value;
            Top = settings.WindowTop.Value;
            WindowStartupLocation = WindowStartupLocation.Manual;
        }

        if (settings.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private async void SaveWindowPosition()
    {
        try
        {
            var settings = App.SettingsService.Settings;

            if (WindowState == WindowState.Normal)
            {
                settings.WindowLeft = Left;
                settings.WindowTop = Top;
                settings.WindowWidth = Width;
                settings.WindowHeight = Height;
            }

            settings.WindowMaximized = WindowState == WindowState.Maximized;

            await App.SettingsService.SaveAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error saving window position");
        }
    }

    #region Drag & Drop

    public static readonly DependencyProperty IsDragOverProperty = DependencyProperty.Register(
        "IsDragOver", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

    public bool IsDragOver
    {
        get { return (bool)GetValue(IsDragOverProperty); }
        set { SetValue(IsDragOverProperty, value); }
    }

    private void Window_DragEnter(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            IsDragOver = true;
        }
    }

    private void Window_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        IsDragOver = false;
    }

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            var validExtensions = new[] { ".stl", ".3mf", ".gcode", ".gco", ".g", ".zip" };
            
            bool hasValidFiles = files.Any(f => 
                validExtensions.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant()));

            if (hasValidFiles)
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
                IsDragOver = true;
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
                IsDragOver = false;
            }
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
            IsDragOver = false;
        }
        e.Handled = true;
    }

    private async void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        IsDragOver = false;

        try
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                await _viewModel.HandleFilesDroppedAsync(files);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error handling dropped files");
        }
    }
    
    // Internal Drag & Drop (Models -> Collection)
    private System.Windows.Point? _dragStartPoint;

    private void ModelCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is Model3D model)
        {
            _dragStartPoint = e.GetPosition(null);

            // Shift+Click for range selection
            if (Keyboard.Modifiers == ModifierKeys.Shift && _lastInteractedModel != null)
            {
                _viewModel.SelectRange(_lastInteractedModel, model);
                e.Handled = true;
                return;
            }

            // If multi-select mode is active, normal click selects
            if (_viewModel.IsMultiSelectMode)
            {
                _viewModel.ToggleModelSelectionCommand.Execute(model);
                _lastInteractedModel = model;
                e.Handled = true;
                return;
            }

            // Ctrl+Click for multi-select
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                _viewModel.ToggleModelSelectionCommand.Execute(model);
                _lastInteractedModel = model;
                e.Handled = true;
                return;
            }

            // Double-click to open detail
            if (e.ClickCount == 2)
            {
                _viewModel.OpenModelDetailCommand.Execute(model);
                e.Handled = true;
            }
        }
    }
    
    // ... existing navigation handlers ...

    private void ModelCard_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement element && element.Tag is Model3D model)
        {
            // Check drag threshold
            if (_dragStartPoint == null) return;
            
            var currentPos = e.GetPosition(null);
            var diff = _dragStartPoint.Value - currentPos;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                // Determine what to drag: single model or selection
                var dragData = new System.Windows.DataObject();
                
                // If dragging a selected item, drag all selected items
                if (_viewModel.SelectedModels.Contains(model))
                {
                     dragData.SetData("ModelList", _viewModel.SelectedModels.ToList());
                }
                else
                {
                     dragData.SetData("ModelList", new List<Model3D> { model });
                }

                _dragStartPoint = null; // Reset
                DragDrop.DoDragDrop(element, dragData, System.Windows.DragDropEffects.Copy);
            }
        }
    }


    #endregion

    #region Model Card Events



    private void OpenModelDetail_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is Model3D model)
        {
            _viewModel.OpenModelDetailCommand.Execute(model);
        }
    }

    private void ToggleFavorite_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is Model3D model)
        {
            _viewModel.ToggleFavoriteCommand.Execute(model);
        }
    }

    private void OpenFileLocation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is Model3D model)
        {
            _viewModel.OpenFileLocationCommand.Execute(model);
        }
    }

    private void DeleteModel_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is Model3D model)
        {
            var result = System.Windows.MessageBox.Show(
                $"'{model.Name}' modelini silmek istediğinize emin misiniz?\n\nBu işlem geri alınamaz.",
                "Model Sil",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _viewModel.DeleteModelCommand.Execute(model);
            }
        }
    }

    #endregion



    #region Collection Filtering

    #endregion

    #region Add to Collection Menu

    private void AddToCollectionMenu_SubmenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && menuItem.Tag is Model3D model)
        {
            // Clear existing items except the placeholder
            menuItem.Items.Clear();
            
            if (_viewModel.Collections == null || !_viewModel.Collections.Any())
            {
                var noCollectionsItem = new MenuItem
                {
                    Header = "No collections yet",
                    IsEnabled = false,
                    Foreground = System.Windows.Media.Brushes.Gray
                };
                menuItem.Items.Add(noCollectionsItem);
                
                menuItem.Items.Add(new Separator { Background = (System.Windows.Media.Brush)FindResource("BorderBrush") });
            }
            else
            {
                foreach (var collection in _viewModel.Collections)
                {
                    var collectionItem = new MenuItem
                    {
                        Header = $"{collection.Name} ({collection.Models?.Count ?? 0})",
                        Tag = new Tuple<Model3D, Collection>(model, collection)
                    };
                    collectionItem.Click += AddModelToCollection_Click;
                    menuItem.Items.Add(collectionItem);
                }
                
                menuItem.Items.Add(new Separator { Background = (System.Windows.Media.Brush)FindResource("BorderBrush") });
            }
            
            // Add "Create New Collection" option
            var createNewItem = new MenuItem
            {
                Header = "Create New Collection...",
                FontWeight = FontWeights.SemiBold,
                Tag = model
            };
            createNewItem.Click += CreateCollectionAndAddModel_Click;
            menuItem.Items.Add(createNewItem);
        }
    }

    private async void AddModelToCollection_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem menuItem && menuItem.Tag is Tuple<Model3D, Collection> data)
            {
                var (model, collection) = data;
                await _viewModel.AddToCollectionAsync(collection, new List<Model3D> { model });
                // StatusMessage is automatically updated in the ViewModel
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error adding model to collection");
        }
    }

    private void CreateCollectionAndAddModel_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is MenuItem menuItem && menuItem.Tag is Model3D model)
            {
                var dialog = new CreateCollectionDialog();
                dialog.Owner = this;
                dialog.CollectionCreated += async (s, collection) =>
                {
                    await _viewModel.ReloadCollectionsCommand.ExecuteAsync(null);
                    await _viewModel.AddToCollectionAsync(collection, new List<Model3D> { model });
                };
                dialog.ShowDialog();
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error creating collection and adding model");
        }
    }

    #endregion

    #region Window Chrome Events

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    #region Bulk Actions

    private void BulkAddTags_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new BulkTagDialog();
        dialog.Owner = this;
        
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.Tags))
        {
            _viewModel.BulkAddTagsCommand.Execute(dialog.Tags);
        }
    }
    
    private void BulkAddToCollection_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.SelectedModels.Any())
        {
            System.Windows.MessageBox.Show("No models selected.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        
        // Show collection picker popup
        var contextMenu = new ContextMenu
        {
            Background = (System.Windows.Media.Brush)FindResource("BackgroundMediumBrush"),
            BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush"),
            Foreground = (System.Windows.Media.Brush)FindResource("TextPrimaryBrush"),
            PlacementTarget = sender as System.Windows.Controls.Button,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Top,
            IsOpen = true
        };
        
        if (!_viewModel.Collections.Any())
        {
            var noCollectionsItem = new MenuItem { Header = "No collections yet", IsEnabled = false };
            contextMenu.Items.Add(noCollectionsItem);
        }
        else
        {
            foreach (var collection in _viewModel.Collections)
            {
                var menuItem = new MenuItem
                {
                    Header = $"{collection.Name} ({collection.Models?.Count ?? 0} models)",
                    Tag = collection
                };
                menuItem.Click += async (s, args) =>
                {
                    if (s is MenuItem mi && mi.Tag is Models.Collection col)
                    {
                        var models = _viewModel.SelectedModels.ToList();
                        await _viewModel.AddToCollectionAsync(col, models);
                        _viewModel.ClearSelectionCommand.Execute(null);
                    }
                };
                contextMenu.Items.Add(menuItem);
            }
        }
        
        contextMenu.Items.Add(new Separator { Background = (System.Windows.Media.Brush)FindResource("BorderBrush") });
        
        var createNewItem = new MenuItem
        {
            Header = "Create New Collection...",
            FontWeight = FontWeights.SemiBold
        };
        createNewItem.Click += (s, args) =>
        {
            var dialog = new CreateCollectionDialog();
            dialog.Owner = this;
            dialog.CollectionCreated += async (sender2, collection) =>
            {
                await _viewModel.ReloadCollectionsCommand.ExecuteAsync(null);
                var models = _viewModel.SelectedModels.ToList();
                await _viewModel.AddToCollectionAsync(collection, models);
                _viewModel.ClearSelectionCommand.Execute(null);
            };
            dialog.ShowDialog();
        };
        contextMenu.Items.Add(createNewItem);
    }

    private async void BulkRemoveFromCollection_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!_viewModel.SelectedModels.Any()) return;
            
            if (_viewModel.SelectedCollection == null)
            {
                 System.Windows.MessageBox.Show("No collection selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                 return;
            }

            var result = System.Windows.MessageBox.Show(
                $"Remove {_viewModel.SelectedModels.Count} models from '{_viewModel.SelectedCollection.Name}'?",
                "Reduce from Collection",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                var models = _viewModel.SelectedModels.ToList();
                await _viewModel.RemoveFromCollectionAsync(_viewModel.SelectedCollection, models);
                _viewModel.ClearSelectionCommand.Execute(null);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error removing models from collection");
        }
    }

    private void Checkbox_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.CheckBox checkBox && checkBox.Tag is Model3D model)
        {
            if (Keyboard.Modifiers == ModifierKeys.Shift && _lastInteractedModel != null)
            {
                _viewModel.SelectRange(_lastInteractedModel, model);
            }
            else
            {
                _viewModel.ToggleModelSelectionCommand.Execute(model);
                _lastInteractedModel = model;
            }
            e.Handled = true;
        }
    }

    #endregion

    #region G-code Management

    private void ManageGcodes_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new GcodeManagementDialog
        {
            Owner = this
        };
        dialog.ShowDialog();
    }



    #endregion
}
