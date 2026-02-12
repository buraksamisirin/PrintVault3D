using System.IO;
using System.Text.Json.Serialization;

namespace PrintVault3D.Models;

/// <summary>
/// Application settings model for JSON persistence.
/// </summary>
public class AppSettings
{
    /// <summary>
    /// List of directories to watch for new files.
    /// </summary>
    public List<string> WatchedFolders { get; set; } = new();

    /// <summary>
    /// Whether to start file watcher automatically on app launch.
    /// </summary>
    public bool AutoStartWatcher { get; set; } = true;

    /// <summary>
    /// Whether to minimize to system tray instead of taskbar.
    /// </summary>
    public bool MinimizeToTray { get; set; } = true;

    /// <summary>
    /// Whether to start the application with Windows.
    /// </summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>
    /// Whether to start minimized (to tray) on Windows startup.
    /// </summary>
    public bool StartMinimized { get; set; } = false;

    /// <summary>
    /// Whether to show balloon notifications for new files.
    /// </summary>
    public bool ShowBalloonNotifications { get; set; } = true;

    /// <summary>
    /// Filament cost per kilogram (for print cost estimation).
    /// Default: 500 TL/kg
    /// </summary>
    public decimal FilamentCostPerKg { get; set; } = 500m;

    /// <summary>
    /// Last window position X coordinate.
    /// </summary>
    public double? WindowLeft { get; set; }

    /// <summary>
    /// Last window position Y coordinate.
    /// </summary>
    public double? WindowTop { get; set; }

    /// <summary>
    /// Last window width.
    /// </summary>
    public double? WindowWidth { get; set; }

    /// <summary>
    /// Last window height.
    /// </summary>
    public double? WindowHeight { get; set; }

    /// <summary>
    /// Whether the window was maximized.
    /// </summary>
    public bool WindowMaximized { get; set; } = false;

    /// <summary>
    /// Creates default settings with Downloads folder as watched directory.
    /// </summary>
    public static AppSettings CreateDefault()
    {
        var settings = new AppSettings();
        
        // Add Downloads folder by default
        var downloadsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
        
        if (Directory.Exists(downloadsPath))
        {
            settings.WatchedFolders.Add(downloadsPath);
        }

        return settings;
    }
}
