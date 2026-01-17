namespace EasyFileManager.Core.Models;

/// <summary>
/// Advanced application settings
/// </summary>
public class AdvancedSettings
{
    /// <summary>
    /// Enable file logging
    /// </summary>
    public bool EnableLogging { get; set; } = true;

    /// <summary>
    /// Log level: Debug, Info, Warning, Error
    /// </summary>
    public string LogLevel { get; set; } = "Info";

    /// <summary>
    /// Number of days to keep log files (0 = keep forever)
    /// </summary>
    public int LogRetentionDays { get; set; } = 30;

    /// <summary>
    /// Enable anonymous crash reporting
    /// </summary>
    public bool EnableCrashReports { get; set; } = true;

    /// <summary>
    /// Automatically check for updates on startup
    /// </summary>
    public bool AutoCheckUpdates { get; set; } = true;

    /// <summary>
    /// Application language (e.g., "en-US", "pl-PL")
    /// </summary>
    public string Language { get; set; } = "en-US";

    /// <summary>
    /// Enable performance monitoring
    /// </summary>
    public bool EnablePerformanceMonitoring { get; set; } = false;

    /// <summary>
    /// Hardware acceleration for UI rendering
    /// </summary>
    public bool HardwareAcceleration { get; set; } = true;
}
