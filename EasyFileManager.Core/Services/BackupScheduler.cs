using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;

namespace EasyFileManager.Core.Services;

/// <summary>
/// Scheduler for automatic backup execution based on schedule
/// </summary>
public class BackupScheduler : IBackupScheduler, IDisposable
{
    private readonly IBackupService _backupService;
    private readonly IBackupStorage _storage;
    private readonly IAppLogger<BackupScheduler> _logger;

    private Timer? _schedulerTimer;
    private readonly List<ScheduledJob> _scheduledJobs = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _isRunning;
    private bool _disposed;

    public bool IsRunning => _isRunning;

    public event EventHandler<BackupJob>? BackupStarted;
    public event EventHandler<BackupHistory>? BackupCompleted;
    public event EventHandler<(BackupJob Job, Exception Exception)>? BackupFailed;

    private class ScheduledJob
    {
        public BackupJob Job { get; set; } = null!;
        public DateTime NextRun { get; set; }
    }

    public BackupScheduler(
        IBackupService backupService,
        IBackupStorage storage,
        IAppLogger<BackupScheduler> logger)
    {
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task StartAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_isRunning)
            {
                _logger.LogWarning("Scheduler already running");
                return;
            }

            _logger.LogInformation("Starting backup scheduler");

            // Load all enabled jobs
            var jobs = await _storage.LoadJobsAsync();
            var enabledJobs = jobs.Where(j => j.IsEnabled && j.Schedule.Frequency != BackupFrequency.Manual).ToList();

            _scheduledJobs.Clear();
            foreach (var job in enabledJobs)
            {
                var nextRun = CalculateNextRunTime(job);
                if (nextRun.HasValue)
                {
                    _scheduledJobs.Add(new ScheduledJob
                    {
                        Job = job,
                        NextRun = nextRun.Value
                    });

                    job.NextRunTime = nextRun.Value;
                    await _storage.SaveJobAsync(job);

                    _logger.LogInformation("Scheduled job {JobName} for {NextRun}", job.Name, nextRun.Value);
                }
            }

            // Start timer - check every minute
            _schedulerTimer = new Timer(OnSchedulerTick, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
            _isRunning = true;

            _logger.LogInformation("Backup scheduler started with {Count} jobs", _scheduledJobs.Count);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task StopAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (!_isRunning)
            {
                _logger.LogWarning("Scheduler not running");
                return;
            }

            _logger.LogInformation("Stopping backup scheduler");

            _schedulerTimer?.Dispose();
            _schedulerTimer = null;
            _scheduledJobs.Clear();
            _isRunning = false;

            _logger.LogInformation("Backup scheduler stopped");
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ScheduleJobAsync(BackupJob job)
    {
        await _lock.WaitAsync();
        try
        {
            if (job.Schedule.Frequency == BackupFrequency.Manual)
            {
                _logger.LogDebug("Job {JobName} is manual, not scheduling", job.Name);
                return;
            }

            var nextRun = CalculateNextRunTime(job);
            if (!nextRun.HasValue)
            {
                _logger.LogWarning("Could not calculate next run time for job: {JobName}", job.Name);
                return;
            }

            // Remove existing schedule
            _scheduledJobs.RemoveAll(sj => sj.Job.Id == job.Id);

            // Add new schedule
            _scheduledJobs.Add(new ScheduledJob
            {
                Job = job,
                NextRun = nextRun.Value
            });

            job.NextRunTime = nextRun.Value;
            await _storage.SaveJobAsync(job);

            _logger.LogInformation("Scheduled job {JobName} for {NextRun}", job.Name, nextRun.Value);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UnscheduleJobAsync(Guid jobId)
    {
        await _lock.WaitAsync();
        try
        {
            var removed = _scheduledJobs.RemoveAll(sj => sj.Job.Id == jobId);
            if (removed > 0)
            {
                _logger.LogInformation("Unscheduled job: {JobId}", jobId);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RescheduleJobAsync(BackupJob job)
    {
        await UnscheduleJobAsync(job.Id);
        await ScheduleJobAsync(job);
    }

    public DateTime? GetNextRunTime(BackupJob job)
    {
        return CalculateNextRunTime(job);
    }

    private void OnSchedulerTick(object? state)
    {
        _ = CheckAndExecuteDueJobsAsync();
    }

    private async Task CheckAndExecuteDueJobsAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var now = DateTime.Now;
            var dueJobs = _scheduledJobs.Where(sj => sj.NextRun <= now).ToList();

            if (dueJobs.Count == 0)
                return;

            _logger.LogInformation("Found {Count} due backup jobs", dueJobs.Count);

            foreach (var scheduledJob in dueJobs)
            {
                _ = ExecuteScheduledJobAsync(scheduledJob);
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task ExecuteScheduledJobAsync(ScheduledJob scheduledJob)
    {
        try
        {
            _logger.LogInformation("Executing scheduled backup: {JobName}", scheduledJob.Job.Name);

            BackupStarted?.Invoke(this, scheduledJob.Job);

            var history = await _backupService.ExecuteBackupAsync(scheduledJob.Job);

            BackupCompleted?.Invoke(this, history);

            // Calculate next run time
            var nextRun = CalculateNextRunTime(scheduledJob.Job);
            if (nextRun.HasValue)
            {
                await _lock.WaitAsync();
                try
                {
                    scheduledJob.NextRun = nextRun.Value;
                    scheduledJob.Job.NextRunTime = nextRun.Value;
                    await _storage.SaveJobAsync(scheduledJob.Job);

                    _logger.LogInformation("Next run for {JobName}: {NextRun}", scheduledJob.Job.Name, nextRun.Value);
                }
                finally
                {
                    _lock.Release();
                }
            }
            else
            {
                // Remove from scheduled jobs if no next run
                await _lock.WaitAsync();
                try
                {
                    _scheduledJobs.Remove(scheduledJob);
                    _logger.LogWarning("Job {JobName} removed from schedule (no next run time)", scheduledJob.Job.Name);
                }
                finally
                {
                    _lock.Release();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduled backup failed: {JobName}", scheduledJob.Job.Name);
            BackupFailed?.Invoke(this, (scheduledJob.Job, ex));
        }
    }

    private DateTime? CalculateNextRunTime(BackupJob job)
    {
        var now = DateTime.Now;
        var schedule = job.Schedule;

        switch (schedule.Frequency)
        {
            case BackupFrequency.Manual:
                return null;

            case BackupFrequency.EveryMinutes:
                return now.AddMinutes(schedule.IntervalValue);

            case BackupFrequency.EveryHours:
                return now.AddHours(schedule.IntervalValue);

            case BackupFrequency.Daily:
                var dailyNext = now.Date.Add(schedule.DailyTime);
                if (dailyNext <= now)
                    dailyNext = dailyNext.AddDays(1);
                return dailyNext;

            case BackupFrequency.Weekly:
                var daysUntilNext = ((int)schedule.WeeklyDay - (int)now.DayOfWeek + 7) % 7;
                if (daysUntilNext == 0 && now.TimeOfDay >= schedule.WeeklyTime)
                    daysUntilNext = 7;

                var weeklyNext = now.Date.AddDays(daysUntilNext).Add(schedule.WeeklyTime);
                return weeklyNext;

            case BackupFrequency.Monthly:
                var monthlyNext = new DateTime(now.Year, now.Month, 1)
                    .AddDays(schedule.MonthlyDay - 1)
                    .Add(schedule.MonthlyTime);

                if (monthlyNext <= now)
                    monthlyNext = monthlyNext.AddMonths(1);

                // Handle months with fewer days
                if (monthlyNext.Day != schedule.MonthlyDay)
                {
                    monthlyNext = new DateTime(monthlyNext.Year, monthlyNext.Month, 1)
                        .AddMonths(1)
                        .AddDays(-1)
                        .Add(schedule.MonthlyTime);
                }

                return monthlyNext;

            default:
                return null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _schedulerTimer?.Dispose();
        _lock.Dispose();
        _disposed = true;

        _logger.LogDebug("BackupScheduler disposed");
    }
}
