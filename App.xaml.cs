using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PrintVault3D.Data;
using PrintVault3D.Repositories;
using PrintVault3D.Services;
using PrintVault3D.ViewModels;
using PrintVault3D.Views;
using Serilog;

namespace PrintVault3D;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static IAppSettingsService SettingsService => Services.GetRequiredService<IAppSettingsService>();

    private ISystemTrayService? _systemTrayService;
    private readonly string _appDataPath;

    public bool IsExiting { get; private set; }

    public App()
    {
        _appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "STLZ");
        Directory.CreateDirectory(_appDataPath);

        // Setup Serilog
        var logConfig = new LoggerConfiguration()
    .WriteTo.File(
 Path.Combine(_appDataPath, "logs", "log-.txt"), 
      rollingInterval: RollingInterval.Day,
             retainedFileCountLimit: 7,  // Keep only 7 days of logs
    fileSizeLimitBytes: 10 * 1024 * 1024,  // 10MB max per file
         rollOnFileSizeLimit: true);

#if DEBUG
        logConfig.MinimumLevel.Debug();
#else
        logConfig.MinimumLevel.Information();  // Changed from Warning to capture important events
#endif

        Log.Logger = logConfig.CreateLogger();

        // Setup DI
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        // Global Exception Handling
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder => builder.AddSerilog(dispose: true));

        // Database
        services.AddDbContext<PrintVaultDbContext>(options =>
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dbPath = Path.Combine(appDataPath, "PrintVault3D", "printvault.db");
            Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
            options.UseSqlite($"Data Source={dbPath}");
        });

        // Services
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<IVaultService, VaultService>();
        services.AddSingleton<IUndoService, UndoService>();
        services.AddSingleton<IFileWatcherService, FileWatcherService>();
        services.AddSingleton<IThumbnailProcessingService, ThumbnailProcessingService>();
        services.AddSingleton<IPythonBridgeService, PythonBridgeService>();
        services.AddSingleton<IStartupService, StartupService>();
        services.AddSingleton<ISystemTrayService, SystemTrayService>();
        services.AddSingleton<IToastService, ToastService>();
        services.AddSingleton<INtfsAdsService, NtfsAdsService>();
        services.AddSingleton<IAutoTaggingService, AutoTaggingService>();
        services.AddSingleton<IGcodeParserService, GcodeParserService>();
        services.AddSingleton<ITagLearningService, TagLearningService>();
        services.AddSingleton<IArchiveService, ArchiveService>();

        // Repositories 
        services.AddTransient<IUnitOfWork, UnitOfWork>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<CreateCollectionViewModel>();
        // Add other ViewModels as needed

        // Views
        services.AddTransient<MainWindow>();
        services.AddTransient<SplashWindow>();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        Log.Information("STLZ Starting up...");

        // 1. Show Splash Screen
        var splash = Services.GetRequiredService<SplashWindow>();
        splash.Show();

        // 2. Perform Background Initialization
        await Task.Run(async () =>
        {
            try
            {
                // Init Database
                splash.UpdateStatus("Initializing database...");
                using (var scope = Services.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<PrintVaultDbContext>();
                    await db.Database.MigrateAsync();
                    
                    // Ensure Collection table has new columns (backward compatibility)
                    await db.EnsureCollectionColumnsExistAsync();

                    // Ensure Gcodes table has new columns
                    await db.EnsureGcodeColumnsExistAsync();

                    // Ensure TagLearnings table exists
                    await db.EnsureTagLearningTableExistAsync();
                }
                
                // Init Services
                splash.UpdateStatus("Loading settings...");
                var settings = Services.GetRequiredService<IAppSettingsService>();
                await settings.LoadAsync();

                splash.UpdateStatus("Checking Python environment...");
                var pythonService = Services.GetRequiredService<IPythonBridgeService>();
                var pythonReady = await pythonService.IsPythonAvailableAsync();
                
                if (!pythonReady)
                {
                    Log.Warning("Python not found during startup check.");
                }

                splash.UpdateStatus("Starting services...");
                var startupService = Services.GetRequiredService<IStartupService>();
                
                // Artificial delay for UX
                await Task.Delay(800); 

                splash.UpdateStatus("Ready!");
                await Task.Delay(200);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during startup initialization");
            }
        });

        // 3. Launch Main Window
        var mainWindow = Services.GetRequiredService<MainWindow>();
        
        var cmdArgs = Environment.GetCommandLineArgs();
        bool startMinimized = cmdArgs.Contains("--minimized");

        if (startMinimized)
        {
            mainWindow.WindowState = WindowState.Minimized;
            mainWindow.Show();
            mainWindow.Hide(); 
        }
        else
        {
            mainWindow.Show();
        }

        // Initialize Tray Icon
        _systemTrayService = Services.GetRequiredService<ISystemTrayService>();
        _systemTrayService.Initialize();
        _systemTrayService.Show();
        _systemTrayService.RestoreRequested += (s, args) => 
        {
            mainWindow.Show();
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.Activate();
        };
        _systemTrayService.ExitRequested += (s, args) => Shutdown();

        // 4. Close Splash
        splash.Close();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        IsExiting = true;
        Log.Information("STLZ Shutting down...");
        
        _systemTrayService?.Dispose();
        
        if (Services is IDisposable disposable)
        {
            disposable.Dispose();
        }

        Log.CloseAndFlush();
        base.OnExit(e);
    }

    public void MinimizeToTray()
    {
        var mainWindow = Windows.OfType<MainWindow>().FirstOrDefault();
        mainWindow?.Hide();
        
        if (SettingsService.Settings.ShowBalloonNotifications)
        {
            _systemTrayService?.ShowBalloon("STLZ Running", "Application is minimized to system tray.");
        }
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled Dispatcher Exception");
        ShowErrorAndExit(e.Exception);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Log.Error(e.ExceptionObject as Exception, "AppDomain Unhandled Exception");
        if (e.IsTerminating)
        {
            ShowErrorAndExit(e.ExceptionObject as Exception);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Task Unobserved Exception");
        e.SetObserved();
    }

    private void ShowErrorAndExit(Exception? ex)
    {
        System.Windows.MessageBox.Show($"Fatal Error: {ex?.Message}\n\nCheck logs for details.", "STLZ Error", MessageBoxButton.OK, MessageBoxImage.Error);
        Shutdown();
    }
}
