using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using Microsoft.Extensions.DependencyInjection;
using PrintVault3D.Repositories;
using PrintVault3D.Services;
using PrintVault3D.ViewModels;

namespace PrintVault3D.Views;

/// <summary>
/// Model detail dialog window.
/// </summary>
public partial class ModelDetailDialog : Window
{
    private readonly ModelDetailViewModel _viewModel;
    
    // Model rotation tracking
    private System.Windows.Point _lastMousePosition;
    private bool _isRotating;

    public event EventHandler? ModelUpdated;

    public ModelDetailDialog(int modelId)
    {
        InitializeComponent();

        var unitOfWork = App.Services.GetRequiredService<IUnitOfWork>();
        var vaultService = App.Services.GetRequiredService<IVaultService>();
        var tagLearningService = App.Services.GetRequiredService<ITagLearningService>();
        var appSettingsService = App.Services.GetRequiredService<IAppSettingsService>();
        var gcodeParserService = App.Services.GetRequiredService<IGcodeParserService>();

        _viewModel = new ModelDetailViewModel(unitOfWork, vaultService, tagLearningService, appSettingsService, gcodeParserService);
        _viewModel.CloseRequested += (s, e) => Close();
        _viewModel.ModelUpdated += (s, e) => ModelUpdated?.Invoke(this, EventArgs.Empty);
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        DataContext = _viewModel;

        Loaded += async (s, e) => 
        {
            WindowBackdropService.EnableAcrylic(this, darkTheme: true);
            await _viewModel.LoadModelAsync(modelId);
            // Auto-load 3D model when dialog opens
            await _viewModel.Load3DModelDirectAsync();
        };

        Closing += (s, e) =>
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            _viewModel.Dispose();
        };
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // When 3D geometry is loaded, reset camera to show the whole model from front
        if (e.PropertyName == nameof(ModelDetailViewModel.ModelGeometry) && _viewModel.ModelGeometry != null)
        {
            Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    // Zoom to fit the model
                    Viewport3D.ZoomExtents(0);
                    
                    // Set up camera to look at model from front
                    if (Viewport3D.Camera is PerspectiveCamera camera && _viewModel.ModelGeometry is MeshGeometry3D mesh)
                    {
                        var bounds = mesh.Bounds;
                        if (!bounds.IsEmpty)
                        {
                            // Calculate model center
                            var center = new Point3D(
                                bounds.X + bounds.SizeX / 2,
                                bounds.Y + bounds.SizeY / 2,
                                bounds.Z + bounds.SizeZ / 2);

                            // Set up rotation around model center (not origin)
                            // Step 1: Move model center to origin
                            TranslateToOrigin.OffsetX = -center.X;
                            TranslateToOrigin.OffsetY = -center.Y;
                            TranslateToOrigin.OffsetZ = -center.Z;
                            
                            // Step 2: After rotation, move back to original position
                            TranslateBack.OffsetX = center.X;
                            TranslateBack.OffsetY = center.Y;
                            TranslateBack.OffsetZ = center.Z;

                            // Distance based on model size
                            var maxSize = Math.Max(bounds.SizeX, Math.Max(bounds.SizeY, bounds.SizeZ));
                            var distance = maxSize * 2.5;

                            // Position camera in front of model (negative Y, looking at positive Y)
                            camera.Position = new Point3D(center.X, center.Y - distance, center.Z + distance * 0.3);
                            camera.LookDirection = new Vector3D(0, 1, -0.3);
                            camera.LookDirection.Normalize();
                            camera.UpDirection = new Vector3D(0, 0, 1);
                            
                            // Reset model rotation
                            RotationX.Angle = 0;
                            RotationZ.Angle = 0;
                        }
                    }
                }
                catch { /* viewport may not be ready */ }
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    #region 3D View Controls

    private void ResetView_Click(object sender, RoutedEventArgs e)
    {
        // Reset model rotation to initial position
        RotationX.Angle = 0;
        RotationZ.Angle = 0;
    }

    #endregion

    #region Tag Suggestions

    private void TagSuggestion_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.Tag is string tag)
        {
            _viewModel.AddTag(tag);
        }
    }

    #endregion

    private async void LinkGcode_Click(object sender, RoutedEventArgs e)
    {
        // Get unlinked G-codes
        var unlinkedGcodes = await _viewModel.GetUnlinkedGcodesAsync();
        
        var dialog = new LinkGcodeDialog(unlinkedGcodes)
        {
            Owner = this
        };

        if (dialog.ShowDialog() == true && dialog.SelectedGcode != null)
        {
            await _viewModel.LinkGcodeAsync(dialog.SelectedGcode);
        }
    }

    #region Model Rotation with Mouse

    private void GcodeOptionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.ContextMenu != null)
        {
            element.ContextMenu.PlacementTarget = element;
            element.ContextMenu.IsOpen = true;
        }
    }

    private void Viewport3D_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isRotating = true;
        _lastMousePosition = e.GetPosition(Viewport3D);
        Viewport3D.CaptureMouse();
        e.Handled = true;
    }

    private void Viewport3D_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isRotating = false;
        Viewport3D.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void Viewport3D_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isRotating) return;

        var currentPosition = e.GetPosition(Viewport3D);
        var delta = currentPosition - _lastMousePosition;

        // Rotate model based on mouse movement (reduced sensitivity)
        // Left/Right drag = rotate around Z axis (vertical axis in STL)
        // Up/Down drag = rotate around X axis (tilt)
        RotationZ.Angle += delta.X * 0.25;
        RotationX.Angle -= delta.Y * 0.25;

        // Clamp X rotation to prevent flipping
        RotationX.Angle = Math.Clamp(RotationX.Angle, -80, 80);

        _lastMousePosition = currentPosition;
        e.Handled = true;
    }

    #endregion

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized 
                ? WindowState.Normal 
                : WindowState.Maximized;
        }
        else
        {
            DragMove();
        }
    }
}

