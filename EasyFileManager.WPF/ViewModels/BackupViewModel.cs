using Accessibility;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;
using EasyFileManager.Core.Services;
using EasyFileManager.WPF.Views;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace EasyFileManager.WPF.ViewModels;

/// <summary>
/// Main ViewModel for Backup panel
/// </summary>
public partial class BackupViewModel : ViewModelBase
{
    private readonly IBackupService _backupService;
    private readonly IBackupScheduler _scheduler;
    private readonly IAppLogger<BackupViewModel> _logger;
    private CancellationTokenSource? _currentBackupCancellation;

    [ObservableProperty]
    private ObservableCollection<BackupJobViewModel> _backupJobs = new();

    [ObservableProperty]
    private BackupJobViewModel? _selectedJob;

    partial void OnSelectedJobChanged(BackupJobViewModel? value)
    {
        // Notify CanExecute for all commands that depend on SelectedJob
        EditJobCommand.NotifyCanExecuteChanged();
        DeleteJobCommand.NotifyCanExecuteChanged();
        RunBackupCommand.NotifyCanExecuteChanged();
        ToggleJobEnabledCommand.NotifyCanExecuteChanged();
        if (SelectedJob != null) CanBrowseBackups = true; else CanBrowseBackups = false;

            _logger.LogDebug("Selected job changed: {Name}", value?.Name ?? "None");
    }

    [ObservableProperty]
    private bool _canBrowseBackups = false;


    [ObservableProperty]
    private ObservableCollection<BackupHistoryViewModel> _recentHistory = new();

    [ObservableProperty]
    private BackupJobViewModel? _runningJob;

    [ObservableProperty]
    private BackupProgress? _currentProgress;

    [ObservableProperty]
    private bool _isBackupRunning;

    partial void OnIsBackupRunningChanged(bool value)
    {
        // Notify CanExecute for commands that depend on backup state
        RunBackupCommand.NotifyCanExecuteChanged();
        EditJobCommand.NotifyCanExecuteChanged();
        DeleteJobCommand.NotifyCanExecuteChanged();
        CancelBackupCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty]
    private bool _isSchedulerRunning;

    partial void OnIsSchedulerRunningChanged(bool value)
    {
        // Toggle scheduler when property changes
        _ = Task.Run(async () =>
        {
            try
            {
                if (value && !_scheduler.IsRunning)
                {
                    await _scheduler.StartAsync();
                    _logger.LogInformation("Scheduler started via toggle");

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = "Scheduler started";
                    });
                }
                else if (!value && _scheduler.IsRunning)
                {
                    await _scheduler.StopAsync();
                    _logger.LogInformation("Scheduler stopped via toggle");

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        StatusMessage = "Scheduler stopped";
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to toggle scheduler");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    // Revert toggle state on error
                    IsSchedulerRunning = _scheduler.IsRunning;
                    MessageBox.Show($"Failed to toggle scheduler: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        });
    }

    [ObservableProperty]
    private string _statusMessage = "Ready";

    public BackupViewModel(
        IBackupService backupService,
        IBackupScheduler scheduler,
        IAppLogger<BackupViewModel> logger)
    {
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Subscribe to scheduler events
        _scheduler.BackupStarted += OnScheduledBackupStarted;
        _scheduler.BackupCompleted += OnScheduledBackupCompleted;
        _scheduler.BackupFailed += OnScheduledBackupFailed;

        IsSchedulerRunning = _scheduler.IsRunning;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing BackupViewModel");

            await LoadBackupJobsAsync();
            await LoadRecentHistoryAsync();

            // Start scheduler if not running
            if (!_scheduler.IsRunning)
            {
                await _scheduler.StartAsync();
                IsSchedulerRunning = true;
            }

            StatusMessage = $"Loaded {BackupJobs.Count} backup jobs";
            _logger.LogInformation("BackupViewModel initialized");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize BackupViewModel");
            StatusMessage = $"Initialization failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadBackupJobsAsync()
    {
        try
        {
            var jobs = await _backupService.GetAllJobsAsync();

            BackupJobs.Clear();
            foreach (var job in jobs.OrderBy(j => j.Name))
            {
                BackupJobs.Add(new BackupJobViewModel(job, _logger));
            }

            _logger.LogInformation("Loaded {Count} backup jobs", BackupJobs.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load backup jobs");
            MessageBox.Show($"Failed to load backup jobs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task LoadRecentHistoryAsync()
    {
        try
        {
            var history = await _backupService.GetAllHistoryAsync(20);

            RecentHistory.Clear();
            foreach (var entry in history)
            {
                RecentHistory.Add(new BackupHistoryViewModel(entry));
            }

            _logger.LogDebug("Loaded {Count} history entries", RecentHistory.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load backup history");
        }
    }

    [RelayCommand]
    private async Task CreateNewJobAsync()
    {
        try
        {
            var dialog = new BackupJobDialog
            {
                Owner = Application.Current.MainWindow
            };

            dialog.ShowDialog();

            if (!dialog.Confirmed)
                return;

            var newJob = await _backupService.CreateJobAsync(dialog.Job);

            var jobVm = new BackupJobViewModel(newJob, _logger);
            BackupJobs.Add(jobVm);
            SelectedJob = jobVm;

            // Schedule if enabled
            if (newJob.IsEnabled && newJob.Schedule.Frequency != BackupFrequency.Manual)
            {
                await _scheduler.ScheduleJobAsync(newJob);
            }

            StatusMessage = $"Created new job: {newJob.Name}";
            _logger.LogInformation("Created new backup job: {Name}", newJob.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create backup job");
            MessageBox.Show($"Failed to create backup job: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand(CanExecute = nameof(CanEditJob))]
    private async Task EditJobAsync()
    {
        if (SelectedJob == null)
            return;

        try
        {
            var job = await _backupService.GetJobByIdAsync(SelectedJob.Id);
            if (job == null)
            {
                MessageBox.Show("Job not found", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var dialog = new Views.BackupJobDialog(job)
            {
                Owner = Application.Current.MainWindow
            };

            dialog.ShowDialog();

            if (dialog.Confirmed)
            {
                await _backupService.UpdateJobAsync(dialog.Job);

                // Update ViewModel
                SelectedJob.UpdateFromModel(dialog.Job);

                // Reschedule if enabled
                if (dialog.Job.IsEnabled && dialog.Job.Schedule.Frequency != BackupFrequency.Manual)
                {
                    await _scheduler.RescheduleJobAsync(dialog.Job);
                }

                StatusMessage = $"Job updated: {dialog.Job.Name}";
                _logger.LogInformation("Job updated: {Name}", dialog.Job.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to edit job");
            MessageBox.Show($"Failed to edit job: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanEditJob() => SelectedJob != null && !IsBackupRunning;

    [RelayCommand(CanExecute = nameof(CanDeleteJob))]
    private async Task DeleteJobAsync()
    {
        if (SelectedJob == null)
            return;

        try
        {
            var result = MessageBox.Show(
                $"Are you sure you want to delete backup job '{SelectedJob.Name}'?",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            await _backupService.DeleteJobAsync(SelectedJob.Id);
            await _scheduler.UnscheduleJobAsync(SelectedJob.Id);

            BackupJobs.Remove(SelectedJob);
            SelectedJob = null;

            StatusMessage = "Backup job deleted";
            _logger.LogInformation("Deleted backup job");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete job");
            MessageBox.Show($"Failed to delete job: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanDeleteJob() => SelectedJob != null && !IsBackupRunning;

    [RelayCommand(CanExecute = nameof(CanRunBackup))]
    private async Task RunBackupAsync()
    {
        if (SelectedJob == null)
            return;

        try
        {
            IsBackupRunning = true;
            RunningJob = SelectedJob;
            _currentBackupCancellation = new CancellationTokenSource();

            StatusMessage = $"Running backup: {SelectedJob.Name}";
            _logger.LogInformation("Starting manual backup: {Name}", SelectedJob.Name);

            var progress = new Progress<BackupProgress>(p =>
            {
                CurrentProgress = p;
                StatusMessage = $"Backing up: {p.CurrentFile} ({p.PercentComplete}%)";
            });

            var job = await _backupService.GetJobByIdAsync(SelectedJob.Id);
            if (job == null)
            {
                MessageBox.Show("Job not found", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var history = await _backupService.ExecuteBackupAsync(job, progress, _currentBackupCancellation.Token);

            // Update job in list
            SelectedJob.UpdateFromModel(job);

            // Add to history
            RecentHistory.Insert(0, new BackupHistoryViewModel(history));
            if (RecentHistory.Count > 20)
                RecentHistory.RemoveAt(RecentHistory.Count - 1);

            if (history.Status == BackupStatus.Completed)
            {
                StatusMessage = $"Backup completed: {SelectedJob.Name}";
                MessageBox.Show(
                    $"Backup completed successfully!\n\n" +
                    $"Files: {history.ProcessedFiles}/{history.TotalFiles}\n" +
                    $"Size: {FormatBytes(history.ProcessedBytes)}\n" +
                    $"Duration: {history.Duration:hh\\:mm\\:ss}",
                    "Backup Completed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else if (history.Status == BackupStatus.CompletedWithWarnings)
            {
                StatusMessage = $"Backup completed with warnings: {SelectedJob.Name}";
                MessageBox.Show(
                    $"Backup completed with {history.FailedFiles} failed files.\n\n" +
                    $"Check history for details.",
                    "Backup Warning",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else if (history.Status == BackupStatus.Failed)
            {
                StatusMessage = $"Backup failed: {SelectedJob.Name}";
                MessageBox.Show(
                    $"Backup failed:\n\n{history.ErrorMessage}",
                    "Backup Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Backup execution failed");
            StatusMessage = "Backup failed";
            MessageBox.Show($"Backup failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBackupRunning = false;
            RunningJob = null;
            CurrentProgress = null;
            _currentBackupCancellation?.Dispose();
            _currentBackupCancellation = null;
        }
    }

    private bool CanRunBackup() => SelectedJob != null && !IsBackupRunning;

    [RelayCommand(CanExecute = nameof(CanCancelBackup))]
    private void CancelBackup()
    {
        _currentBackupCancellation?.Cancel();
        StatusMessage = "Cancelling backup...";
        _logger.LogInformation("Backup cancelled by user");
    }

    private bool CanCancelBackup() => IsBackupRunning;

    [RelayCommand(CanExecute = nameof(CanToggleJobEnabled))]
    private async Task ToggleJobEnabledAsync()
    {
        if (SelectedJob == null)
            return;

        try
        {
            var job = await _backupService.GetJobByIdAsync(SelectedJob.Id);
            if (job != null)
            {
                // Set to the value from ViewModel (already changed by checkbox)
                job.IsEnabled = SelectedJob.IsEnabled;
                await _backupService.UpdateJobAsync(job);

                if (job.IsEnabled && job.Schedule.Frequency != BackupFrequency.Manual)
                {
                    await _scheduler.ScheduleJobAsync(job);
                    StatusMessage = $"Job enabled and scheduled: {job.Name}";
                    _logger.LogInformation("Job enabled and scheduled: {Name}", job.Name);
                }
                else
                {
                    await _scheduler.UnscheduleJobAsync(job.Id);
                    StatusMessage = $"Job {(job.IsEnabled ? "enabled" : "disabled")}: {job.Name}";
                    _logger.LogInformation("Job {State}: {Name}", job.IsEnabled ? "enabled" : "disabled", job.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle job enabled");

            // Revert on error
            if (SelectedJob != null)
            {
                SelectedJob.IsEnabled = !SelectedJob.IsEnabled;
            }

            MessageBox.Show($"Failed to update job: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool CanToggleJobEnabled() => SelectedJob != null;

    [RelayCommand]
    private async Task BrowseBackupsAsync()
    {
        if (SelectedJob == null)
            return;

        try
        {
            var job = await _backupService.GetJobByIdAsync(SelectedJob.Id);
            if (job == null)
            {
                MessageBox.Show("Job not found", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var dialog = new BackupBrowseDialog(job, (BackupService)_backupService)
            {
                Owner = Application.Current.MainWindow
            };

            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to browse backups");
            MessageBox.Show($"Failed to browse backups: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    //private bool CanBrowseBackups() => SelectedJob != null;



    // ===== EVENT HANDLERS =====

    private void OnScheduledBackupStarted(object? sender, BackupJob job)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            StatusMessage = $"Scheduled backup started: {job.Name}";
            _logger.LogInformation("Scheduled backup started: {Name}", job.Name);
        });
    }

    private void OnScheduledBackupCompleted(object? sender, BackupHistory history)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            StatusMessage = $"Scheduled backup completed: {history.JobName}";

            // Update job in list
            var jobVm = BackupJobs.FirstOrDefault(j => j.Id == history.JobId);
            if (jobVm != null)
            {
                _ = Task.Run(async () =>
                {
                    var job = await _backupService.GetJobByIdAsync(jobVm.Id);
                    if (job != null)
                    {
                        Application.Current.Dispatcher.Invoke(() => jobVm.UpdateFromModel(job));
                    }
                });
            }

            // Add to history
            RecentHistory.Insert(0, new BackupHistoryViewModel(history));
            if (RecentHistory.Count > 20)
                RecentHistory.RemoveAt(RecentHistory.Count - 1);

            _logger.LogInformation("Scheduled backup completed: {Name}", history.JobName);
        });
    }

    private void OnScheduledBackupFailed(object? sender, (BackupJob Job, Exception Exception) args)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            StatusMessage = $"Scheduled backup failed: {args.Job.Name}";
            _logger.LogError(args.Exception, "Scheduled backup failed: {Name}", args.Job.Name);

            MessageBox.Show(
                $"Scheduled backup failed: {args.Job.Name}\n\n{args.Exception.Message}",
                "Backup Failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        });
    }

    // ===== HELPERS =====

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}
