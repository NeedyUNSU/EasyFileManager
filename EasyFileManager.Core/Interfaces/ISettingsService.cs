using EasyFileManager.Core.Models;
using System;
using System.Threading.Tasks;

namespace EasyFileManager.Core.Interfaces;

/// <summary>
/// Service for managing application settings
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Current application settings
    /// </summary>
    AppSettings Settings { get; }

    /// <summary>
    /// Event raised when settings are changed
    /// </summary>
    event EventHandler<AppSettings>? SettingsChanged;

    /// <summary>
    /// Loads settings from storage
    /// </summary>
    Task<AppSettings> LoadAsync();

    /// <summary>
    /// Saves current settings to storage
    /// </summary>
    Task SaveAsync();

    /// <summary>
    /// Saves specific settings to storage
    /// </summary>
    Task SaveAsync(AppSettings settings);

    /// <summary>
    /// Resets all settings to default values
    /// </summary>
    Task ResetToDefaultsAsync();

    /// <summary>
    /// Gets the path to the settings file
    /// </summary>
    string GetSettingsFilePath();
}
