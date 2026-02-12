namespace PrintVault3D.Services;

/// <summary>
/// Service interface for Windows startup management.
/// </summary>
public interface IStartupService
{
    /// <summary>
    /// Gets whether the app is registered to start with Windows.
    /// </summary>
    bool IsStartupEnabled { get; }

    /// <summary>
    /// Enables starting with Windows.
    /// </summary>
    /// <param name="startMinimized">Whether to start minimized to tray.</param>
    void EnableStartup(bool startMinimized = false);

    /// <summary>
    /// Disables starting with Windows.
    /// </summary>
    void DisableStartup();
}
