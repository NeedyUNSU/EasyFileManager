using EasyFileManager.Core.Models;

namespace EasyFileManager.Core.Interfaces;

/// <summary>
/// Service for applying theme changes
/// </summary>
public interface IThemeService
{
    /// <summary>
    /// Applies appearance settings to the application
    /// </summary>
    void ApplyAppearanceSettings(AppearanceSettings settings);

    /// <summary>
    /// Sets theme (Light/Dark/System)
    /// </summary>
    void SetTheme(string theme);

    /// <summary>
    /// Sets accent color
    /// </summary>
    void SetAccentColor(string hexColor);
}
