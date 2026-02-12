using System.Text.Json.Serialization;

namespace PrintVault3D.Services;

/// <summary>
/// Application settings model.
/// </summary>
public class AppSettings
{
    // Window settings
    public double? WindowLeft { get; set; }
    public double? WindowTop { get; set; }
    public double? WindowWidth { get; set; }
    public double? WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }

    // System tray settings
    public bool MinimizeToTray { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }
    public bool ShowBalloonNotifications { get; set; } = true;

    // File watcher settings
    public bool AutoStartWatcher { get; set; } = true;
    public List<string> WatchedFolders { get; set; } = new();

    // Thumbnail settings
    public int ThumbnailSize { get; set; } = 256;
    public bool AutoGenerateThumbnails { get; set; } = true;

    // Filament cost settings
    /// <summary>
    /// Cost of filament per kilogram (in user's preferred currency).
    /// </summary>
    public decimal FilamentCostPerKg { get; set; } = 20.00m;
}

/// <summary>
/// Service interface for application settings persistence.
/// </summary>
public interface IAppSettingsService
{
    /// <summary>
    /// Gets the current settings.
    /// </summary>
    AppSettings Settings { get; }

    /// <summary>
    /// Loads settings from disk.
    /// </summary>
    Task LoadAsync();

    /// <summary>
    /// Saves settings to disk.
    /// </summary>
    Task SaveAsync();

    /// <summary>
    /// Resets settings to defaults.
    /// </summary>
    void Reset();
}
