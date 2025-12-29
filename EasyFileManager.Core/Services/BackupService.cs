using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;

namespace EasyFileManager.Core.Services;

/// <summary>
/// Service for executing backup operations
/// </summary>
public class BackupService : IBackupService
{
    private readonly IBackupStorage _storage;
    private readonly IFileSystemService _fileSystemService;
    private readonly IAppLogger<BackupService> _logger;

    public BackupService(
        IBackupStorage storage,
        IFileSystemService fileSystemService,
        IAppLogger<BackupService> logger)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BackupHistory> ExecuteBackupAsync(
        BackupJob job,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var history = new BackupHistory
        {
            JobId = job.Id,
            JobName = job.Name,
            StartTime = DateTime.Now,
            Status = BackupStatus.Running
        };

        var backupProgress = new BackupProgress
        {
            JobId = job.Id,
            JobName = job.Name,
            StartTime = DateTime.Now,
            Status = BackupStatus.Running
        };

        try
        {
            _logger.LogInformation("Starting backup job: {JobName}", job.Name);

            // Create timestamped backup directory with JobId prefix
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var backupFolderName = $"{job.Id}_{timestamp}";
            var backupDestination = Path.Combine(job.DestinationPath, backupFolderName);
            Directory.CreateDirectory(backupDestination);

            history.DestinationPath = backupDestination;

            // Collect all files to backup
            var filesToBackup = new List<(string SourcePath, string RelativePath)>();

            foreach (var sourcePath in job.SourcePaths)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    history.Status = BackupStatus.Cancelled;
                    return history;
                }

                if (Directory.Exists(sourcePath))
                {
                    var files = await CollectFilesAsync(sourcePath, job.Options, cancellationToken);
                    filesToBackup.AddRange(files.Select(f => (f, GetRelativePath(sourcePath, f))));
                }
                else if (File.Exists(sourcePath))
                {
                    if (ShouldIncludeFile(sourcePath, job.Options))
                    {
                        filesToBackup.Add((sourcePath, Path.GetFileName(sourcePath)));
                    }
                }
                else
                {
                    var warning = $"Source path not found: {sourcePath}";
                    history.Warnings.Add(warning);
                    _logger.LogWarning(warning);
                }
            }

            backupProgress.TotalFiles = filesToBackup.Count;
            backupProgress.TotalBytes = filesToBackup.Sum(f => GetFileSize(f.SourcePath));
            progress?.Report(backupProgress);

            _logger.LogInformation("Found {Count} files to backup ({Size} bytes)",
                backupProgress.TotalFiles, backupProgress.TotalBytes);

            // Copy files
            foreach (var (sourcePath, relativePath) in filesToBackup)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    history.Status = BackupStatus.Cancelled;
                    history.EndTime = DateTime.Now;
                    return history;
                }

                try
                {
                    var destinationPath = Path.Combine(backupDestination, relativePath);
                    var destinationDir = Path.GetDirectoryName(destinationPath);

                    if (!string.IsNullOrEmpty(destinationDir))
                    {
                        Directory.CreateDirectory(destinationDir);
                    }

                    // Copy file
                    File.Copy(sourcePath, destinationPath, overwrite: true);

                    // Preserve attributes and timestamps if requested
                    if (job.Options.PreserveAttributes)
                    {
                        File.SetAttributes(destinationPath, File.GetAttributes(sourcePath));
                    }

                    if (job.Options.PreserveTimestamps)
                    {
                        File.SetCreationTime(destinationPath, File.GetCreationTime(sourcePath));
                        File.SetLastWriteTime(destinationPath, File.GetLastWriteTime(sourcePath));
                        File.SetLastAccessTime(destinationPath, File.GetLastAccessTime(sourcePath));
                    }

                    backupProgress.ProcessedFiles++;
                    backupProgress.ProcessedBytes += GetFileSize(sourcePath);
                    backupProgress.CurrentFile = relativePath;

                    progress?.Report(backupProgress);
                }
                catch (Exception ex)
                {
                    history.FailedFiles++;
                    var error = $"Failed to copy {relativePath}: {ex.Message}";
                    history.Errors.Add(error);
                    _logger.LogError(ex, "Failed to copy file: {Path}", relativePath);
                }
            }

            history.TotalFiles = backupProgress.TotalFiles;
            history.ProcessedFiles = backupProgress.ProcessedFiles;
            history.TotalBytes = backupProgress.TotalBytes;
            history.ProcessedBytes = backupProgress.ProcessedBytes;

            // Verify backup if requested
            if (job.Options.VerifyAfterBackup)
            {
                _logger.LogInformation("Verifying backup...");
                var isValid = await VerifyBackupAsync(backupDestination);
                if (!isValid)
                {
                    history.Warnings.Add("Backup verification failed");
                }
            }

            // Determine final status
            if (history.FailedFiles > 0)
            {
                history.Status = BackupStatus.CompletedWithWarnings;
                _logger.LogWarning("Backup completed with {Count} failed files", history.FailedFiles);
            }
            else
            {
                history.Status = BackupStatus.Completed;
                _logger.LogInformation("Backup completed successfully");
            }

            history.EndTime = DateTime.Now;

            // Update job statistics
            job.LastRunTime = history.StartTime;
            job.LastRunStatus = history.Status;
            job.LastBackupSize = history.ProcessedBytes;
            job.TotalBackupCount++;
            await _storage.SaveJobAsync(job);

            // Save history
            await _storage.SaveHistoryAsync(history);

            // Cleanup old backups
            await CleanupOldBackupsAsync(job);

            backupProgress.Status = history.Status;
            progress?.Report(backupProgress);

            return history;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup job failed: {JobName}", job.Name);

            history.Status = BackupStatus.Failed;
            history.ErrorMessage = ex.Message;
            history.EndTime = DateTime.Now;

            await _storage.SaveHistoryAsync(history);

            backupProgress.Status = BackupStatus.Failed;
            progress?.Report(backupProgress);

            return history;
        }
    }

    public async Task<List<BackupJob>> GetAllJobsAsync()
    {
        return await _storage.LoadJobsAsync();
    }

    public async Task<BackupJob?> GetJobByIdAsync(Guid jobId)
    {
        var jobs = await _storage.LoadJobsAsync();
        return jobs.FirstOrDefault(j => j.Id == jobId);
    }

    public async Task<BackupJob> CreateJobAsync(BackupJob job)
    {
        job.Id = Guid.NewGuid();
        job.CreatedAt = DateTime.Now;
        job.ModifiedAt = DateTime.Now;
        await _storage.SaveJobAsync(job);
        _logger.LogInformation("Created backup job: {Name}", job.Name);
        return job;
    }

    public async Task UpdateJobAsync(BackupJob job)
    {
        job.ModifiedAt = DateTime.Now;
        await _storage.SaveJobAsync(job);
        _logger.LogInformation("Updated backup job: {Name}", job.Name);
    }

    public async Task DeleteJobAsync(Guid jobId)
    {
        await _storage.DeleteJobAsync(jobId);
        _logger.LogInformation("Deleted backup job: {JobId}", jobId);
    }

    public async Task<List<BackupHistory>> GetHistoryAsync(Guid jobId, int limit = 50)
    {
        return await _storage.LoadHistoryForJobAsync(jobId, limit);
    }

    public async Task<List<BackupHistory>> GetAllHistoryAsync(int limit = 100)
    {
        return await _storage.LoadHistoryAsync(limit);
    }

    public async Task CleanupOldBackupsAsync(BackupJob job)
    {
        try
        {
            if (!job.Options.EnableRetention)
                return;

            if (!Directory.Exists(job.DestinationPath))
                return;

            // Filter only this job's backup directories (JobId_timestamp format)
            var jobPrefix = $"{job.Id}_";
            var backupDirs = Directory.GetDirectories(job.DestinationPath)
                .Where(d => Path.GetFileName(d).StartsWith(jobPrefix))
                .Select(d => new DirectoryInfo(d))
                .OrderByDescending(d => d.CreationTime)
                .ToList();

            _logger.LogInformation("Found {Count} backup directories for job {JobId}", backupDirs.Count, job.Id);

            // Delete by count
            if (job.Options.MaxBackupCount > 0 && backupDirs.Count > job.Options.MaxBackupCount)
            {
                var toDelete = backupDirs.Skip(job.Options.MaxBackupCount).ToList();
                foreach (var dir in toDelete)
                {
                    try
                    {
                        _logger.LogInformation("Deleting old backup (by count): {Path}", dir.FullName);
                        Directory.Delete(dir.FullName, recursive: true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex.ToString(), "Failed to delete backup directory: {Path}", dir.FullName);
                        // Continue with other deletions
                    }
                }
            }

            // Delete by age
            if (job.Options.RetentionDays > 0)
            {
                var cutoffDate = DateTime.Now.AddDays(-job.Options.RetentionDays);
                var toDelete = backupDirs.Where(d => d.CreationTime < cutoffDate).ToList();

                foreach (var dir in toDelete)
                {
                    try
                    {
                        _logger.LogInformation("Deleting old backup (by age): {Path}", dir.FullName);
                        Directory.Delete(dir.FullName, recursive: true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex.ToString(), "Failed to delete backup directory: {Path}", dir.FullName);
                        // Continue with other deletions
                    }
                }
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old backups for job: {JobName}", job.Name);
        }
    }

    public async Task<bool> VerifyBackupAsync(string backupPath)
    {
        try
        {
            if (!Directory.Exists(backupPath))
            {
                _logger.LogWarning("Backup path does not exist: {Path}", backupPath);
                return false;
            }

            // Simple verification: check if files exist and are readable
            var files = Directory.GetFiles(backupPath, "*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                if (!File.Exists(file))
                    return false;

                // Try to read first byte
                using var stream = File.OpenRead(file);
                _ = stream.ReadByte();
            }

            _logger.LogInformation("Backup verification passed: {Path}", backupPath);
            await Task.CompletedTask;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup verification failed: {Path}", backupPath);
            return false;
        }
    }

    public async Task RestoreBackupAsync(
        string backupPath,
        string destinationPath,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting restore from {BackupPath} to {Destination}", backupPath, destinationPath);

            if (!Directory.Exists(backupPath))
                throw new DirectoryNotFoundException($"Backup path not found: {backupPath}");

            Directory.CreateDirectory(destinationPath);

            var files = Directory.GetFiles(backupPath, "*", SearchOption.AllDirectories);
            var totalFiles = files.Length;
            var processedFiles = 0;

            var restoreProgress = new BackupProgress
            {
                JobName = "Restore",
                StartTime = DateTime.Now,
                TotalFiles = totalFiles,
                Status = BackupStatus.Running
            };

            foreach (var sourceFile in files)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var relativePath = GetRelativePath(backupPath, sourceFile);
                var destFile = Path.Combine(destinationPath, relativePath);
                var destDir = Path.GetDirectoryName(destFile);

                if (!string.IsNullOrEmpty(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                File.Copy(sourceFile, destFile, overwrite: true);

                processedFiles++;
                restoreProgress.ProcessedFiles = processedFiles;
                restoreProgress.CurrentFile = relativePath;
                progress?.Report(restoreProgress);
            }

            restoreProgress.Status = BackupStatus.Completed;
            progress?.Report(restoreProgress);

            _logger.LogInformation("Restore completed: {Count} files", processedFiles);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore failed");
            throw;
        }
    }

    // ===== PRIVATE HELPERS =====

    private async Task<List<string>> CollectFilesAsync(
        string directoryPath,
        BackupOptions options,
        CancellationToken cancellationToken)
    {
        var files = new List<string>();

        try
        {
            var searchOption = SearchOption.AllDirectories;
            var allFiles = Directory.GetFiles(directoryPath, "*", searchOption);

            foreach (var file in allFiles)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                if (ShouldIncludeFile(file, options))
                {
                    files.Add(file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect files from: {Path}", directoryPath);
        }

        await Task.CompletedTask;
        return files;
    }

    private bool ShouldIncludeFile(string filePath, BackupOptions options)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);

            // Check hidden files
            if (!options.IncludeHiddenFiles && fileInfo.Attributes.HasFlag(FileAttributes.Hidden))
                return false;

            // Check system files
            if (!options.IncludeSystemFiles && fileInfo.Attributes.HasFlag(FileAttributes.System))
                return false;

            var fileName = fileInfo.Name;

            // Check exclude patterns
            foreach (var pattern in options.ExcludePatterns)
            {
                if (MatchesPattern(fileName, pattern))
                    return false;
            }

            // Check include patterns
            if (options.IncludePatterns.Count == 0)
                return true;

            foreach (var pattern in options.IncludePatterns)
            {
                if (MatchesPattern(fileName, pattern))
                    return true;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private bool MatchesPattern(string fileName, string pattern)
    {
        try
        {
            // Convert wildcard pattern to regex
            var regexPattern = "^" + Regex.Escape(pattern)
                .Replace("\\*", ".*")
                .Replace("\\?", ".") + "$";

            return Regex.IsMatch(fileName, regexPattern, RegexOptions.IgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private string GetRelativePath(string basePath, string fullPath)
    {
        var baseUri = new Uri(basePath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
        var fullUri = new Uri(fullPath);
        var relativeUri = baseUri.MakeRelativeUri(fullUri);
        return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
    }

    private long GetFileSize(string filePath)
    {
        try
        {
            return new FileInfo(filePath).Length;
        }
        catch
        {
            return 0;
        }
    }
}
