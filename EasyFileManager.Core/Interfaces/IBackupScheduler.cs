using System;
using System.Threading.Tasks;
using EasyFileManager.Core.Models;

namespace EasyFileManager.Core.Interfaces;

/// <summary>
/// Service for scheduling automatic backups
/// </summary>
public interface IBackupScheduler
{
    /// <summary>
    /// Start the scheduler
    /// </summary>
    Task StartAsync();

    /// <summary>
    /// Stop the scheduler
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Check if scheduler is running
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Schedule a backup job
    /// </summary>
    Task ScheduleJobAsync(BackupJob job);

    /// <summary>
    /// Unschedule a backup job
    /// </summary>
    Task UnscheduleJobAsync(Guid jobId);

    /// <summary>
    /// Reschedule a backup job (update schedule)
    /// </summary>
    Task RescheduleJobAsync(BackupJob job);

    /// <summary>
    /// Get next run time for a job
    /// </summary>
    DateTime? GetNextRunTime(BackupJob job);

    /// <summary>
    /// Event raised when a scheduled backup starts
    /// </summary>
    event EventHandler<BackupJob>? BackupStarted;

    /// <summary>
    /// Event raised when a scheduled backup completes
    /// </summary>
    event EventHandler<BackupHistory>? BackupCompleted;

    /// <summary>
    /// Event raised when a scheduled backup fails
    /// </summary>
    event EventHandler<(BackupJob Job, Exception Exception)>? BackupFailed;
}
