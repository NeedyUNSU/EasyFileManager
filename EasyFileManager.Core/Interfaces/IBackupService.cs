using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EasyFileManager.Core.Models;

namespace EasyFileManager.Core.Interfaces;

/// <summary>
/// Service for managing backup operations
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Execute backup job immediately
    /// </summary>
    Task<BackupHistory> ExecuteBackupAsync(
        BackupJob job, 
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all backup jobs
    /// </summary>
    Task<List<BackupJob>> GetAllJobsAsync();

    /// <summary>
    /// Get backup job by ID
    /// </summary>
    Task<BackupJob?> GetJobByIdAsync(Guid jobId);

    /// <summary>
    /// Create new backup job
    /// </summary>
    Task<BackupJob> CreateJobAsync(BackupJob job);

    /// <summary>
    /// Update existing backup job
    /// </summary>
    Task UpdateJobAsync(BackupJob job);

    /// <summary>
    /// Delete backup job
    /// </summary>
    Task DeleteJobAsync(Guid jobId);

    /// <summary>
    /// Get backup history for a job
    /// </summary>
    Task<List<BackupHistory>> GetHistoryAsync(Guid jobId, int limit = 50);

    /// <summary>
    /// Get all backup history
    /// </summary>
    Task<List<BackupHistory>> GetAllHistoryAsync(int limit = 100);

    /// <summary>
    /// Clean up old backups based on retention policy
    /// </summary>
    Task CleanupOldBackupsAsync(BackupJob job);

    /// <summary>
    /// Verify backup integrity
    /// </summary>
    Task<bool> VerifyBackupAsync(string backupPath);

    /// <summary>
    /// Restore from backup
    /// </summary>
    Task RestoreBackupAsync(
        string backupPath, 
        string destinationPath,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
