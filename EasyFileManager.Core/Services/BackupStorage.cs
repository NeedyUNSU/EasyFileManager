using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;
using SharpCompress.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

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
    private List<BackupJob>? _jobs = null;

    public List<BackupJob>? Jobs
    {
        get { return _jobs; }
        set { _jobs = value; }
    }

    private List<BackupHistory>? _backupHistories = null;

    public List<BackupHistory>? BackupHistories
    {
        get { return _backupHistories; }
        set { _backupHistories = value; }
    }


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
        if (Jobs != null) return Jobs;

        try
        {
            if (!File.Exists(_jobsFilePath))
            {
                _logger.LogInformation("No jobs file found, returning empty list");
                Jobs = new List<BackupJob>();
                return new List<BackupJob>();
            }

            var json = await File.ReadAllTextAsync(_jobsFilePath);
            var jobs = JsonSerializer.Deserialize<List<BackupJob>>(json, JsonOptions) ?? new List<BackupJob>();

            _logger.LogInformation("Loaded {Count} backup jobs", jobs.Count);
            Jobs = jobs;
            return jobs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load backup jobs");
            Jobs = new List<BackupJob>();
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

            Jobs = jobs;
            //var json = JsonSerializer.Serialize(jobs, JsonOptions);
            //await File.WriteAllTextAsync(_jobsFilePath, json);
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
                Jobs = jobs;

                //var json = JsonSerializer.Serialize(jobs, JsonOptions);
                //await File.WriteAllTextAsync(_jobsFilePath, json);
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
        if (BackupHistories != null)
        {
            var result = BackupHistories
                .OrderByDescending(h => h.StartTime)
                .Take(limit)
                .ToList();

            return result;
        }

        try
        {
            if (!File.Exists(_historyFilePath))
            {
                _logger.LogInformation("No history file found, returning empty list");
                BackupHistories = new List<BackupHistory>();
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
            BackupHistories = result;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load backup history");
            BackupHistories = new List<BackupHistory>();
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

            BackupHistories = allHistory;
            //var json = JsonSerializer.Serialize(allHistory, JsonOptions);
            //await File.WriteAllTextAsync(_historyFilePath, json);

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
                BackupHistories = filteredHistory;
                //var json = JsonSerializer.Serialize(filteredHistory, JsonOptions);
                //await File.WriteAllTextAsync(_historyFilePath, json);

                _logger.LogInformation("Cleaned up {Count} old history entries", removedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup backup history");
        }
    }

    public async Task<(bool resultJobs, bool resultHistory)> SaveBackupToFileAsync(int maxWaitSeconds = 60)
    {
        var savingJobs = WaitAndSaveAsJson(_jobsFilePath, Jobs, maxWaitSeconds);
        var savingHistory = WaitAndSaveAsJson(_historyFilePath, BackupHistories, maxWaitSeconds);

        await Task.WhenAll(savingJobs, savingHistory);

        if (savingJobs.Result)
        {
            _logger.LogInformation("Jobs sessions saved");
        }
        else
        {
            _logger.LogWarning("Failed to save Jobs session");
        }


        if (savingHistory.Result)
        {
            _logger.LogInformation("History sessions saved");
        }
        else
        {
            _logger.LogWarning("Failed to save History session");
        }


        return (savingJobs.Result, savingHistory.Result);
    }

    ///
    /// Helpers
    ///

    private async Task<bool> WaitAndSaveAsJson<T>(string filePath,
    T objectToSave,
    int maxWaitSeconds = 30)
    {
        var startTime = DateTime.Now;
        var timeout = TimeSpan.FromSeconds(maxWaitSeconds);

        while (DateTime.Now - startTime < timeout)
        {
            if (await TrySaveJson(filePath, objectToSave))
            {
                _logger.LogInformation($"{nameof(objectToSave)} sessions saved");
                return true;
            }

            await Task.Delay(500);
        }

        _logger.LogWarning($"Failed to save {nameof(objectToSave)} session");
        return false;
    }

    private async Task<bool> TrySaveJson<T>(string filePath, T objectToSave)
    {
        try
        {
            var json = JsonSerializer.Serialize(objectToSave, JsonOptions);
            await File.WriteAllTextAsync(filePath, json);

            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }
}
