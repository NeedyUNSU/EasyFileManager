using System;
using System.Collections.Generic;

namespace EasyFileManager.Core.Models;

/// <summary>
/// Represents a backup job with schedule, sources, destination, and rules
/// </summary>
public class BackupJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;

    // Sources and Destination
    public List<string> SourcePaths { get; set; } = new();
    public string DestinationPath { get; set; } = string.Empty;

    // Schedule
    public BackupSchedule Schedule { get; set; } = new();

    // Backup Options
    public BackupOptions Options { get; set; } = new();

    // Statistics
    public DateTime? LastRunTime { get; set; }
    public DateTime? NextRunTime { get; set; }
    public BackupStatus LastRunStatus { get; set; } = BackupStatus.NeverRun;
    public long LastBackupSize { get; set; }
    public int TotalBackupCount { get; set; }

    // Metadata
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime ModifiedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Schedule configuration for backup job
/// </summary>
public class BackupSchedule
{
    public BackupFrequency Frequency { get; set; } = BackupFrequency.Manual;

    // For Interval-based (Minutes/Hours)
    public int IntervalValue { get; set; } = 1;

    // For Daily
    public TimeSpan DailyTime { get; set; } = new TimeSpan(2, 0, 0); // 2:00 AM

    // For Weekly
    public DayOfWeek WeeklyDay { get; set; } = DayOfWeek.Sunday;
    public TimeSpan WeeklyTime { get; set; } = new TimeSpan(2, 0, 0);

    // For Monthly
    public int MonthlyDay { get; set; } = 1; // 1st day of month
    public TimeSpan MonthlyTime { get; set; } = new TimeSpan(2, 0, 0);
}

/// <summary>
/// Backup frequency types
/// </summary>
public enum BackupFrequency
{
    Manual,
    EveryMinutes,
    EveryHours,
    Daily,
    Weekly,
    Monthly
}

/// <summary>
/// Backup options and rules
/// </summary>
public class BackupOptions
{
    // Retention
    public bool EnableRetention { get; set; } = true;
    public int RetentionDays { get; set; } = 30;
    public int MaxBackupCount { get; set; } = 10;

    // File Filters
    public List<string> IncludePatterns { get; set; } = new() { "*.*" };
    public List<string> ExcludePatterns { get; set; } = new();
    public bool IncludeHiddenFiles { get; set; } = false;
    public bool IncludeSystemFiles { get; set; } = false;

    // Backup Type
    public BackupType BackupType { get; set; } = BackupType.Full;

    // Compression
    public bool EnableCompression { get; set; } = false;
    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Best;

    // Advanced
    public bool VerifyAfterBackup { get; set; } = true;
    public bool PreserveAttributes { get; set; } = true;
    public bool PreserveTimestamps { get; set; } = true;
}

/// <summary>
/// Backup type
/// </summary>
public enum BackupType
{
    Full,           // Copy all files
    Incremental,    // Copy only changed files since last backup
    Differential,   // Copy only changed files since last full backup
    Mirror          // Exact copy, delete files not in source
}

///// <summary>
///// Compression level
///// </summary>
//public enum CompressionLevel
//{
//    None,
//    Fastest,
//    Normal,
//    Best
//}

/// <summary>
/// Backup status
/// </summary>
public enum BackupStatus
{
    NeverRun,
    Running,
    Completed,
    CompletedWithWarnings,
    Failed,
    Cancelled
}

/// <summary>
/// Backup history entry
/// </summary>
public class BackupHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid JobId { get; set; }
    public string JobName { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public TimeSpan Duration => EndTime.HasValue ? EndTime.Value - StartTime : TimeSpan.Zero;

    public BackupStatus Status { get; set; }
    public string? ErrorMessage { get; set; }

    // Statistics
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public int FailedFiles { get; set; }
    public long TotalBytes { get; set; }
    public long ProcessedBytes { get; set; }

    public string DestinationPath { get; set; } = string.Empty;

    // Logs
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Real-time backup progress
/// </summary>
public class BackupProgress
{
    public Guid JobId { get; set; }
    public string JobName { get; set; } = string.Empty;
    public string CurrentFile { get; set; } = string.Empty;

    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public long TotalBytes { get; set; }
    public long ProcessedBytes { get; set; }

    public BackupStatus Status { get; set; }
    public DateTime StartTime { get; set; }

    public int PercentComplete => TotalFiles > 0
        ? (int)((double)ProcessedFiles / TotalFiles * 100)
        : 0;

    public TimeSpan Elapsed => DateTime.Now - StartTime;
    public TimeSpan? EstimatedTimeRemaining
    {
        get
        {
            if (ProcessedBytes == 0 || TotalBytes == 0)
                return null;

            var bytesPerSecond = ProcessedBytes / Elapsed.TotalSeconds;
            var remainingBytes = TotalBytes - ProcessedBytes;
            var remainingSeconds = remainingBytes / bytesPerSecond;

            return TimeSpan.FromSeconds(remainingSeconds);
        }
    }
}
