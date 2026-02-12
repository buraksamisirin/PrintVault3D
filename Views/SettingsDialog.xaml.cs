using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using PrintVault3D.Services;
using PrintVault3D.ViewModels;

namespace PrintVault3D.Views;

/// <summary>
/// Settings dialog window.
/// </summary>
public partial class SettingsDialog : Window
{
    private readonly SettingsViewModel _viewModel;

    public SettingsDialog()
    {
        InitializeComponent();

        var fileWatcher = App.Services.GetRequiredService<IFileWatcherService>();
        var pythonBridge = App.Services.GetRequiredService<IPythonBridgeService>();
        var vaultService = App.Services.GetRequiredService<IVaultService>();
        var settingsService = App.Services.GetRequiredService<IAppSettingsService>();
        var startupService = App.Services.GetRequiredService<IStartupService>();
        var serviceProvider = App.Services;

        _viewModel = new SettingsViewModel(fileWatcher, pythonBridge, vaultService, settingsService, startupService, serviceProvider);
        _viewModel.CloseRequested += (s, e) => Close();

        DataContext = _viewModel;

        Loaded += async (s, e) => 
        {
            WindowBackdropService.EnableAcrylic(this, darkTheme: true);
            await _viewModel.LoadSettingsAsync();
        };
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }
}

