namespace EasyFileManager.Core.Models;

/// <summary>
/// Appearance and UI settings
/// </summary>
public class AppearanceSettings
{
    /// <summary>
    /// Application theme: Light, Dark, System
    /// </summary>
    public string Theme { get; set; } = "System";

    /// <summary>
    /// Accent color in hex format (e.g., "#2196F3")
    /// </summary>
    public string AccentColor { get; set; } = "#2196F3";

    /// <summary>
    /// Font size: Small, Medium, Large
    /// </summary>
    public string FontSize { get; set; } = "Medium";

    /// <summary>
    /// Show file extensions in file list
    /// </summary>
    public bool ShowFileExtensions { get; set; } = true;

    /// <summary>
    /// Show hidden files and folders
    /// </summary>
    public bool ShowHiddenFiles { get; set; } = false;

    /// <summary>
    /// Show system files
    /// </summary>
    public bool ShowSystemFiles { get; set; } = false;

    /// <summary>
    /// Use compact view for file list
    /// </summary>
    public bool CompactView { get; set; } = false;

    /// <summary>
    /// Icon size: Small (16), Medium (24), Large (32)
    /// </summary>
    public int IconSize { get; set; } = 24;
}
