namespace PrintVault3D.Services;

/// <summary>
/// Balloon notification icon types.
/// </summary>
public enum BalloonIconType
{
    None,
    Info,
    Warning,
    Error
}

/// <summary>
/// Service interface for system tray integration.
/// </summary>
public interface ISystemTrayService : IDisposable
{
    /// <summary>
    /// Event raised when restore is requested from tray.
    /// </summary>
    event EventHandler? RestoreRequested;

    /// <summary>
    /// Event raised when exit is requested from tray.
    /// </summary>
    event EventHandler? ExitRequested;

    /// <summary>
    /// Gets whether the tray icon is visible.
    /// </summary>
    bool IsVisible { get; }

    /// <summary>
    /// Initializes the tray icon.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Shows the tray icon.
    /// </summary>
    void Show();

    /// <summary>
    /// Hides the tray icon.
    /// </summary>
    void Hide();

    /// <summary>
    /// Shows a balloon notification.
    /// </summary>
    void ShowBalloon(string title, string message, BalloonIconType iconType = BalloonIconType.Info);

    /// <summary>
    /// Sets the tooltip text.
    /// </summary>
    void SetTooltip(string text);
}
