using System;

namespace EasyFileManager.Core.Models;

/// <summary>
/// Application behavior and startup settings
/// </summary>
public class BehaviorSettings
{
    /// <summary>
    /// Start application with Windows
    /// </summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>
    /// Default path for left panel (empty = Desktop)
    /// </summary>
    public string DefaultLeftPanelPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

    /// <summary>
    /// Default path for right panel (empty = Desktop)
    /// </summary>
    public string DefaultRightPanelPath { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

    /// <summary>
    /// Restore tabs from last session on startup
    /// </summary>
    public bool RememberLastSession { get; set; } = true;

    /// <summary>
    /// Remember and restore window position and size
    /// </summary>
    public bool RestoreWindowPosition { get; set; } = true;

    /// <summary>
    /// Minimize to system tray instead of taskbar
    /// </summary>
    public bool MinimizeToTray { get; set; } = false;

    /// <summary>
    /// Allow only one instance of the application
    /// </summary>
    public bool SingleInstance { get; set; } = true;

    /// <summary>
    /// Last window position X
    /// </summary>
    public double WindowLeft { get; set; } = 100;

    /// <summary>
    /// Last window position Y
    /// </summary>
    public double WindowTop { get; set; } = 100;

    /// <summary>
    /// Last window width
    /// </summary>
    public double WindowWidth { get; set; } = 1200;

    /// <summary>
    /// Last window height
    /// </summary>
    public double WindowHeight { get; set; } = 800;

    /// <summary>
    /// Window state: Normal, Maximized
    /// </summary>
    public string WindowState { get; set; } = "Normal";
}
