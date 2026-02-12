using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using PrintVault3D.Repositories;
using PrintVault3D.Services;
using Microsoft.EntityFrameworkCore;

namespace PrintVault3D.ViewModels;

/// <summary>
/// ViewModel for the Settings page.
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly IFileWatcherService _fileWatcherService;
    private readonly IPythonBridgeService _pythonBridge;
    private readonly IVaultService _vaultService;
    private readonly IAppSettingsService _settingsService;
    private readonly IStartupService _startupService;
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty]
    private ObservableCollection<string> _watchedFolders = new();

    [ObservableProperty]
    private string? _selectedFolder;

    [ObservableProperty]
    private bool _isPythonAvailable;

    [ObservableProperty]
    private string _pythonStatus = "Kontrol ediliyor...";

    [ObservableProperty]
    private string _vaultPath = string.Empty;

    [ObservableProperty]
    private string _databasePath = string.Empty;

    [ObservableProperty]
    private bool _autoStartWatcher = true;

    [ObservableProperty]
    private decimal _filamentCostPerKg = 20.00m;

    // New Phase 4 settings
    [ObservableProperty]
    private bool _minimizeToTray = true;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _startMinimized;

    [ObservableProperty]
    private bool _showBalloonNotifications = true;

    public event EventHandler? CloseRequested;
    public event EventHandler? DataCleared;

    public SettingsViewModel(
        IFileWatcherService fileWatcherService,
        IPythonBridgeService pythonBridge,
        IVaultService vaultService,
        IAppSettingsService settingsService,
        IStartupService startupService,
        IServiceProvider serviceProvider)
    {
        _fileWatcherService = fileWatcherService;
        _pythonBridge = pythonBridge;
        _vaultService = vaultService;
        _settingsService = settingsService;
        _startupService = startupService;
        _serviceProvider = serviceProvider;
    }

    public async Task LoadSettingsAsync()
    {
        // Load watched folders
        WatchedFolders = new ObservableCollection<string>(_fileWatcherService.WatchedDirectories);

        // Check Python
        IsPythonAvailable = await _pythonBridge.IsPythonAvailableAsync();
        PythonStatus = IsPythonAvailable 
            ? "✓ Python yüklü ve çalışıyor" 
            : "✗ Python bulunamadı - Thumbnail oluşturma devre dışı";

        // Paths
        VaultPath = _vaultService.VaultPath;
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        DatabasePath = Path.Combine(appDataPath, "PrintVault3D", "printvault.db");

        // Load Phase 4 settings
        var settings = _settingsService.Settings;
        MinimizeToTray = settings.MinimizeToTray;
        StartWithWindows = _startupService.IsStartupEnabled;
        StartMinimized = settings.StartMinimized;
        ShowBalloonNotifications = settings.ShowBalloonNotifications;
        AutoStartWatcher = settings.AutoStartWatcher;
        FilamentCostPerKg = settings.FilamentCostPerKg;
    }

    partial void OnFilamentCostPerKgChanged(decimal value)
    {
        _settingsService.Settings.FilamentCostPerKg = value;
        _ = _settingsService.SaveAsync();
    }

    partial void OnMinimizeToTrayChanged(bool value)
    {
        _settingsService.Settings.MinimizeToTray = value;
        _ = _settingsService.SaveAsync();
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        _settingsService.Settings.StartWithWindows = value;
        
        if (value)
        {
            _startupService.EnableStartup(StartMinimized);
        }
        else
        {
            _startupService.DisableStartup();
        }

        _ = _settingsService.SaveAsync();
    }

    partial void OnStartMinimizedChanged(bool value)
    {
        _settingsService.Settings.StartMinimized = value;
        
        // Update startup registration if enabled
        if (StartWithWindows)
        {
            _startupService.EnableStartup(value);
        }

        _ = _settingsService.SaveAsync();
    }

    partial void OnShowBalloonNotificationsChanged(bool value)
    {
        _settingsService.Settings.ShowBalloonNotifications = value;
        _ = _settingsService.SaveAsync();
    }

    partial void OnAutoStartWatcherChanged(bool value)
    {
        _settingsService.Settings.AutoStartWatcher = value;
        _ = _settingsService.SaveAsync();
    }

    [RelayCommand]
    private async Task AddWatchFolderAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "İzlenecek Klasörü Seç"
        };

        if (dialog.ShowDialog() == true)
        {
            var folderPath = dialog.FolderName;
            if (!WatchedFolders.Contains(folderPath))
            {
                _fileWatcherService.AddWatchDirectory(folderPath);
                WatchedFolders.Add(folderPath);

                // Save to settings
                _settingsService.Settings.WatchedFolders = WatchedFolders.ToList();
                await _settingsService.SaveAsync();
            }
        }
    }

    [RelayCommand]
    private async Task RemoveWatchFolderAsync()
    {
        if (SelectedFolder == null) return;

        _fileWatcherService.RemoveWatchDirectory(SelectedFolder);
        WatchedFolders.Remove(SelectedFolder);
        SelectedFolder = null;

        // Save to settings
        _settingsService.Settings.WatchedFolders = WatchedFolders.ToList();
        await _settingsService.SaveAsync();
    }

    [RelayCommand]
    private void OpenVaultFolder()
    {
        if (Directory.Exists(VaultPath))
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = VaultPath,
                UseShellExecute = true
            });
        }
    }

    [RelayCommand]
    private async Task InstallPythonDependenciesAsync()
    {
        await ExecuteBusyAsync(async () =>
        {
            PythonStatus = "Paketler yükleniyor...";
            var success = await _pythonBridge.InstallDependenciesAsync();
            
            IsPythonAvailable = await _pythonBridge.IsPythonAvailableAsync();
            PythonStatus = success 
                ? "✓ Paketler başarıyla yüklendi" 
                : "✗ Paket yükleme başarısız";
        }, "Python paketleri yükleniyor...");
    }

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private async Task ClearAllDataAsync()
    {
        // Get actual counts using scoped UnitOfWork
        int modelCount = 0;
        int gcodeCount = 0;
        
        using (var scope = _serviceProvider.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            modelCount = await unitOfWork.Models.CountAsync();
            gcodeCount = await unitOfWork.Gcodes.CountAsync();
        }
        
        var result = System.Windows.MessageBox.Show(
            "TÜM VERİLER SİLİNECEK!\n\n" +
            "Bu işlem şunları silecek:\n" +
            $"• Tüm modeller ({modelCount} model)\n" +
            $"• Tüm G-code dosyaları ({gcodeCount} dosya)\n" +
            "• Tüm thumbnail'lar\n" +
            "• Veritabanı kayıtları\n\n" +
            "Bu işlem GERİ ALINAMAZ!\n\n" +
            "Devam etmek istediğinize emin misiniz?",
            "Tüm Verileri Temizle",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        await ExecuteBusyAsync(async () =>
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PrintVault3D.Data.PrintVaultDbContext>();

            // 1. Get file paths (read-only, no tracking)
            var models = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking(dbContext.Models)
                .Select(m => new { m.FilePath, m.ThumbnailPath }));

            var gcodePaths = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
                Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AsNoTracking(dbContext.Gcodes)
                .Select(g => g.FilePath));

            var vaultPath = _vaultService.VaultPath;

            // 2. Delete Physical Files
            int deletedModels = 0;
            foreach (var m in models)
            {
                try
                {
                    // SAFETY: Only delete if file is inside the Vault structure
                    if (m.FilePath != null && m.FilePath.StartsWith(vaultPath, System.StringComparison.OrdinalIgnoreCase))
                    {
                         if (File.Exists(m.FilePath)) File.Delete(m.FilePath);
                    }
                    
                    if (!string.IsNullOrEmpty(m.ThumbnailPath) && File.Exists(m.ThumbnailPath)) 
                        File.Delete(m.ThumbnailPath); // Thumbnails are always in vault/appdata

                    deletedModels++;
                }
                catch { }
            }

            int deletedGcodes = 0;
            foreach (var path in gcodePaths)
            {
                try
                {
                    // SAFETY: Only delete if file is inside the Vault structure
                    if (path != null && path.StartsWith(vaultPath, System.StringComparison.OrdinalIgnoreCase))
                    {
                        if (File.Exists(path)) File.Delete(path);
                        deletedGcodes++;
                    }
                }
                catch { }
            }

            // 3. Bulk Delete from Database
            // Order matters due to Foreign Keys

            // Delete join table first (CollectionModels) - this table is hidden by EF Core, so execute raw SQL
            await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM CollectionModels");

            // Delete Gcodes (FK to Models)
            await dbContext.Gcodes.ExecuteDeleteAsync();
            
            // Delete Models (FKs to Categories, Collections via join)
            await dbContext.Models.ExecuteDeleteAsync();

            // Delete Collections
            await dbContext.Collections.ExecuteDeleteAsync();

            // Delete TagLearnings
            await dbContext.TagLearnings.ExecuteDeleteAsync();

            // Delete User Categories (Keep ID 1)
            await dbContext.Categories.Where(c => c.Id > 1).ExecuteDeleteAsync();

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                System.Windows.MessageBox.Show(
                    $"Tüm veriler temizlendi!\n\n" +
                    $"• {deletedModels} dosya silindi\n" +
                    $"• Koleksiyonlar silindi\n" +
                    $"• Kategoriler (varsayılan hariç) silindi\n" +
                    $"• Veritabanı sıfırlandı\n\n" +
                    $"Uygulamayı yeniden başlatmanız önerilir.",
                    "Temizleme Tamamlandı",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);



                DataCleared?.Invoke(this, EventArgs.Empty);
                CloseRequested?.Invoke(this, EventArgs.Empty);
            });

        }, "Veriler temizleniyor...");
    }
}

