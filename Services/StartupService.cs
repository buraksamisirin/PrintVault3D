using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace PrintVault3D.Services;

/// <summary>
/// Windows startup management service using Registry.
/// </summary>
public class StartupService : IStartupService
{
    private const string RegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "STLIE";
    private readonly ILogger<StartupService>? _logger;

    public StartupService(ILogger<StartupService>? logger = null)
    {
        _logger = logger;
    }

    public bool IsStartupEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, false);
                return key?.GetValue(AppName) != null;
            }
            catch
            {
                return false;
            }
        }
    }

    public void EnableStartup(bool startMinimized = false)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
            if (key == null) return;

            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath)) return;

            var command = startMinimized 
                ? $"\"{exePath}\" --minimized" 
                : $"\"{exePath}\"";

            key.SetValue(AppName, command);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to enable startup registration");
        }
    }

    public void DisableStartup()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
            key?.DeleteValue(AppName, false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to disable startup registration");
        }
    }
}

