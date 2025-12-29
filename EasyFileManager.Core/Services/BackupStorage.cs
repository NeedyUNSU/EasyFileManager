using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;

namespace EasyFileManager.Core.Services;

/// <summary>
/// JSON-based storage for backup jobs and history
/// </summary>
public class BackupStorage : IBackupStorage
{
    private readonly IAppLogger<BackupStorage> _logger;
    private readonly string _storageDirectory;
    private readonly string _jobsFilePath;
    private readonly string _historyFilePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public BackupStorage(IAppLogger<BackupStorage> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _storageDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EasyFileManager",
            "Backups");

        _jobsFilePath = Path.Combine(_storageDirectory, "jobs.json");
        _historyFilePath = Path.Combine(_storageDirectory, "history.json");

        Directory.CreateDirectory(_storageDirectory);

        _logger.LogInformation("BackupStorage initialized at: {Path}", _storageDirectory);
    }

    public async Task<List<BackupJob>> LoadJobsAsync()
    {
        try
        {
            if (!File.Exists(_jobsFilePath))
            {
                _logger.LogInformation("No jobs file found, returning empty list");
                return new List<BackupJob>();
            }

            var json = await File.ReadAllTextAsync(_jobsFilePath);
            var jobs = JsonSerializer.Deserialize<List<BackupJob>>(json, JsonOptions) ?? new List<BackupJob>();

            _logger.LogInformation("Loaded {Count} backup jobs", jobs.Count);
            return jobs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load backup jobs");
            return new List<BackupJob>();
        }
    }

    public async Task SaveJobAsync(BackupJob job)
    {
        try
        {
            var jobs = await LoadJobsAsync();

            // Update or add
            var existingIndex = jobs.FindIndex(j => j.Id == job.Id);
            if (existingIndex >= 0)
            {
                job.ModifiedAt = DateTime.Now;
                jobs[existingIndex] = job;
                _logger.LogInformation("Updated backup job: {Name}", job.Name);
            }
            else
            {
                job.CreatedAt = DateTime.Now;
                job.ModifiedAt = DateTime.Now;
                jobs.Add(job);
                _logger.LogInformation("Created backup job: {Name}", job.Name);
            }

            var json = JsonSerializer.Serialize(jobs, JsonOptions);
            await File.WriteAllTextAsync(_jobsFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save backup job: {Name}", job.Name);
            throw;
        }
    }

    public async Task DeleteJobAsync(Guid jobId)
    {
        try
        {
            var jobs = await LoadJobsAsync();
            var removed = jobs.RemoveAll(j => j.Id == jobId);

            if (removed > 0)
            {
                var json = JsonSerializer.Serialize(jobs, JsonOptions);
                await File.WriteAllTextAsync(_jobsFilePath, json);
                _logger.LogInformation("Deleted backup job: {JobId}", jobId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete backup job: {JobId}", jobId);
            throw;
        }
    }

    public async Task<List<BackupHistory>> LoadHistoryAsync(int limit = 100)
    {
        try
        {
            if (!File.Exists(_historyFilePath))
            {
                _logger.LogInformation("No history file found, returning empty list");
                return new List<BackupHistory>();
            }

            var json = await File.ReadAllTextAsync(_historyFilePath);
            var history = JsonSerializer.Deserialize<List<BackupHistory>>(json, JsonOptions) ?? new List<BackupHistory>();

            // Return most recent first
            var result = history
                .OrderByDescending(h => h.StartTime)
                .Take(limit)
                .ToList();

            _logger.LogInformation("Loaded {Count} history entries", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load backup history");
            return new List<BackupHistory>();
        }
    }

    public async Task<List<BackupHistory>> LoadHistoryForJobAsync(Guid jobId, int limit = 50)
    {
        try
        {
            var allHistory = await LoadHistoryAsync(int.MaxValue);
            var jobHistory = allHistory
                .Where(h => h.JobId == jobId)
                .OrderByDescending(h => h.StartTime)
                .Take(limit)
                .ToList();

            _logger.LogInformation("Loaded {Count} history entries for job {JobId}", jobHistory.Count, jobId);
            return jobHistory;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load history for job: {JobId}", jobId);
            return new List<BackupHistory>();
        }
    }

    public async Task SaveHistoryAsync(BackupHistory history)
    {
        try
        {
            var allHistory = await LoadHistoryAsync(int.MaxValue);
            allHistory.Add(history);

            var json = JsonSerializer.Serialize(allHistory, JsonOptions);
            await File.WriteAllTextAsync(_historyFilePath, json);

            _logger.LogInformation("Saved history entry for job: {JobName}", history.JobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save backup history");
            throw;
        }
    }

    public async Task CleanupHistoryAsync(int keepDays = 90)
    {
        try
        {
            var allHistory = await LoadHistoryAsync(int.MaxValue);
            var cutoffDate = DateTime.Now.AddDays(-keepDays);

            var filteredHistory = allHistory
                .Where(h => h.StartTime >= cutoffDate)
                .ToList();

            var removedCount = allHistory.Count - filteredHistory.Count;

            if (removedCount > 0)
            {
                var json = JsonSerializer.Serialize(filteredHistory, JsonOptions);
                await File.WriteAllTextAsync(_historyFilePath, json);

                _logger.LogInformation("Cleaned up {Count} old history entries", removedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup backup history");
        }
    }
}
