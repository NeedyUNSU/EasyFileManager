using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EasyFileManager.Core.Models;

namespace EasyFileManager.Core.Interfaces;

/// <summary>
/// Service for persisting backup jobs and history
/// </summary>
public interface IBackupStorage
{
    /// <summary>
    /// Load all backup jobs from storage
    /// </summary>
    Task<List<BackupJob>> LoadJobsAsync();

    /// <summary>
    /// Save backup job to storage
    /// </summary>
    Task SaveJobAsync(BackupJob job);

    /// <summary>
    /// Delete backup job from storage
    /// </summary>
    Task DeleteJobAsync(Guid jobId);

    /// <summary>
    /// Load backup history from storage
    /// </summary>
    Task<List<BackupHistory>> LoadHistoryAsync(int limit = 100);

    /// <summary>
    /// Load backup history for specific job
    /// </summary>
    Task<List<BackupHistory>> LoadHistoryForJobAsync(Guid jobId, int limit = 50);

    /// <summary>
    /// Save backup history entry
    /// </summary>
    Task SaveHistoryAsync(BackupHistory history);

    /// <summary>
    /// Delete old history entries
    /// </summary>
    Task CleanupHistoryAsync(int keepDays = 90);
}
