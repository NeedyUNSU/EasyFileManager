namespace EasyFileManager.Core.Models;

/// <summary>
/// Tab behavior settings
/// </summary>
public class TabSettings
{
    /// <summary>
    /// Default path for new tabs (empty = current directory)
    /// </summary>
    public string NewTabDefaultPath { get; set; } = string.Empty;

    /// <summary>
    /// Maximum tabs per panel (5-20)
    /// </summary>
    public int MaxTabsPerPanel { get; set; } = 15;

    /// <summary>
    /// Ask for confirmation before closing tab
    /// </summary>
    public bool ConfirmTabClose { get; set; } = false;

    /// <summary>
    /// When duplicating tab, open same path or new path
    /// </summary>
    public bool DuplicateTabSamePath { get; set; } = true;

    /// <summary>
    /// Auto-save tab session interval in seconds (0 = disabled)
    /// </summary>
    public int AutoSaveIntervalSeconds { get; set; } = 0;

    /// <summary>
    /// Close tab with middle mouse button
    /// </summary>
    public bool MiddleClickClosesTab { get; set; } = true;

    /// <summary>
    /// Double-click on tab bar creates new tab
    /// </summary>
    public bool DoubleClickCreatesTab { get; set; } = true;
}
