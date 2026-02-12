using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PrintVault3D.Services;

/// <summary>
/// JSON-based application settings service.
/// </summary>
public class AppSettingsService : IAppSettingsService
{
    private readonly string _settingsPath;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly ILogger<AppSettingsService>? _logger;
    private AppSettings _settings = new();

    public AppSettings Settings => _settings;

    public AppSettingsService(ILogger<AppSettingsService>? logger = null)
    {
        _logger = logger;
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var vaultPath = Path.Combine(appDataPath, "PrintVault3D");
        Directory.CreateDirectory(vaultPath);
        _settingsPath = Path.Combine(vaultPath, "AppSettings.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = await File.ReadAllTextAsync(_settingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
                if (loaded != null)
                {
                    _settings = loaded;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load settings from {SettingsPath}", _settingsPath);
            _settings = new AppSettings();
        }
    }

    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, _jsonOptions);
            await File.WriteAllTextAsync(_settingsPath, json);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save settings to {SettingsPath}", _settingsPath);
        }
    }

    public void Reset()
    {
        _settings = new AppSettings();
    }
}

