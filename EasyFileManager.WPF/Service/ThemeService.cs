using System.Windows;
using System.Windows.Media;
using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;
using MaterialDesignThemes.Wpf;

namespace EasyFileManager.WPF.Services;

/// <summary>
/// Service for applying Material Design theme changes
/// </summary>
public class ThemeService : IThemeService
{
    private readonly IAppLogger<ThemeService> _logger;
    private readonly PaletteHelper _paletteHelper;

    public ThemeService(IAppLogger<ThemeService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _paletteHelper = new PaletteHelper();
    }

    public void ApplyAppearanceSettings(AppearanceSettings settings)
    {
        try
        {
            _logger.LogInformation("Applying appearance settings");

            SetTheme(settings.Theme);
            SetAccentColor(settings.AccentColor);

            _logger.LogInformation("Appearance settings applied successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply appearance settings");
        }
    }

    public void SetTheme(string theme)
    {
        try
        {
            var currentTheme = _paletteHelper.GetTheme();

            switch (theme.ToLower())
            {
                case "light":
                    currentTheme.SetBaseTheme(BaseTheme.Light);
                    _logger.LogInformation("Theme set to Light");
                    break;

                case "dark":
                    currentTheme.SetBaseTheme(BaseTheme.Dark);
                    _logger.LogInformation("Theme set to Dark");
                    break;

                case "system":
                    // Detect system theme
                    var systemTheme = GetSystemTheme();
                    currentTheme.SetBaseTheme(systemTheme == "Dark" ? BaseTheme.Dark : BaseTheme.Light);
                    _logger.LogInformation("Theme set to System: {SystemTheme}", systemTheme);
                    break;

                default:
                    _logger.LogWarning("Unknown theme: {Theme}, defaulting to System", theme);
                    currentTheme.SetBaseTheme(BaseTheme.Light);
                    break;
            }

            _paletteHelper.SetTheme(currentTheme);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set theme: {Theme}", theme);
        }
    }

    public void SetAccentColor(string hexColor)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hexColor) || !hexColor.StartsWith("#"))
            {
                _logger.LogWarning("Invalid accent color: {Color}", hexColor);
                return;
            }

            var color = (Color)ColorConverter.ConvertFromString(hexColor);
            var currentTheme = _paletteHelper.GetTheme();

            currentTheme.SetPrimaryColor(color);
            currentTheme.SetSecondaryColor(color);

            _paletteHelper.SetTheme(currentTheme);

            _logger.LogInformation("Accent color set to: {Color}", hexColor);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set accent color: {Color}", hexColor);
        }
    }

    private static string GetSystemTheme()
    {
        try
        {
            // Check Windows registry for system theme preference
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int intValue)
            {
                return intValue == 0 ? "Dark" : "Light";
            }
        }
        catch
        {
            // If registry check fails, default to Light
        }

        return "Light";
    }
}
