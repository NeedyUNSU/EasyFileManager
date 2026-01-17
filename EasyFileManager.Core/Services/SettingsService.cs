using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace EasyFileManager.Core.Services;

/// <summary>
/// Service for managing application settings with JSON persistence
/// </summary>
public class SettingsService : ISettingsService
{
    private readonly IAppLogger<SettingsService> _logger;
    private readonly string _settingsPath;
    private AppSettings _settings;

    public AppSettings Settings => _settings;

    public event EventHandler<AppSettings>? SettingsChanged;

    public SettingsService(IAppLogger<SettingsService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Settings path: %LocalAppData%\EasyFileManager\settings.json
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = Path.Combine(localAppData, "EasyFileManager");
        Directory.CreateDirectory(appFolder);
        _settingsPath = Path.Combine(appFolder, "settings.json");

        _settings = AppSettings.CreateDefault();
        _logger.LogInformation("SettingsService initialized. Settings path: {Path}", _settingsPath);
    }

    public async Task<AppSettings> LoadAsync()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                _logger.LogInformation("Settings file not found, using defaults");
                _settings = AppSettings.CreateDefault();
                await SaveAsync();
                return _settings;
            }

            var json = await File.ReadAllTextAsync(_settingsPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };

            var loadedSettings = JsonSerializer.Deserialize<AppSettings>(json, options);
            if (loadedSettings != null)
            {
                _settings = loadedSettings;
                _logger.LogInformation("Settings loaded successfully");
            }
            else
            {
                _logger.LogWarning("Failed to deserialize settings, using defaults");
                _settings = AppSettings.CreateDefault();
            }

            return _settings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings, using defaults");
            _settings = AppSettings.CreateDefault();
            return _settings;
        }
    }

    public async Task SaveAsync()
    {
        await SaveAsync(_settings);
    }

    public async Task SaveAsync(AppSettings settings)
    {
        try
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(_settings, options);
            await File.WriteAllTextAsync(_settingsPath, json);

            _logger.LogInformation("Settings saved successfully");
            SettingsChanged?.Invoke(this, _settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            throw;
        }
    }

    public async Task ResetToDefaultsAsync()
    {
        try
        {
            _logger.LogInformation("Resetting settings to defaults");
            _settings = AppSettings.CreateDefault();
            await SaveAsync();
            _logger.LogInformation("Settings reset to defaults");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset settings");
            throw;
        }
    }

    public string GetSettingsFilePath()
    {
        return _settingsPath;
    }
}
