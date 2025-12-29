using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;

namespace EasyFileManager.WPF.ViewModels;

/// <summary>
/// ViewModel for single backup job
/// </summary>
public partial class BackupJobViewModel : ObservableObject
{
    private readonly IAppLogger<BackupViewModel> _logger;

    // Event fired when backup count changes (for BrowseBackupsCommand.NotifyCanExecuteChanged)
    public event EventHandler? BackupCountChanged;

    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _isEnabled;

    partial void OnIsEnabledChanged(bool value)
    {
        // Refresh status icon/color when enabled state changes
        OnPropertyChanged(nameof(StatusIcon));
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(StatusTooltip));
    }

    [ObservableProperty]
    private string _sourcePaths = string.Empty;

    [ObservableProperty]
    private string _destinationPath = string.Empty;

    [ObservableProperty]
    private string _scheduleDescription = "Manual";

    [ObservableProperty]
    private DateTime? _lastRunTime;

    [ObservableProperty]
    private DateTime? _nextRunTime;

    [ObservableProperty]
    private BackupStatus _lastRunStatus = BackupStatus.NeverRun;

    partial void OnLastRunStatusChanged(BackupStatus value)
    {
        // Refresh status icon/color when status changes
        OnPropertyChanged(nameof(StatusIcon));
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(StatusTooltip));
    }

    [ObservableProperty]
    private string _lastBackupSize = "0 B";

    [ObservableProperty]
    private int _totalBackupCount;

    partial void OnTotalBackupCountChanged(int value)
    {
        // Notify that backup count changed (for Browse Backups button state)
        BackupCountChanged?.Invoke(this, EventArgs.Empty);
    }

    public string StatusIcon
    {
        get
        {
            // Priority: Running > Disabled > Failed > CompletedWithWarnings > Completed > NeverRun
            if (LastRunStatus == BackupStatus.Running)
                return "ProgressClock"; // Running (animated)

            if (!IsEnabled)
                return "Cancel"; // Disabled

            if (LastRunStatus == BackupStatus.Failed)
                return "CloseCircle"; // Failed

            if (LastRunStatus == BackupStatus.CompletedWithWarnings)
                return "AlertCircle"; // Warning

            if (LastRunStatus == BackupStatus.Completed)
                return "CheckCircle"; // Success

            return "HelpCircle"; // Never run
        }
    }

    public string StatusColor
    {
        get
        {
            // Priority: Running > Disabled > Failed > CompletedWithWarnings > Completed > NeverRun
            if (LastRunStatus == BackupStatus.Running)
                return "#2196F3"; // Blue (Running)

            if (!IsEnabled)
                return "#9E9E9E"; // Gray (Disabled)

            if (LastRunStatus == BackupStatus.Failed)
                return "#F44336"; // Red (Failed)

            if (LastRunStatus == BackupStatus.CompletedWithWarnings)
                return "#FF9800"; // Orange (Warning)

            if (LastRunStatus == BackupStatus.Completed)
                return "#4CAF50"; // Green (Success)

            return "#757575"; // Dark Gray (Never run)
        }
    }

    public string StatusTooltip
    {
        get
        {
            if (!IsEnabled)
                return "Disabled";

            return LastRunStatus switch
            {
                BackupStatus.Running => "Backup in progress",
                BackupStatus.Completed => "Last backup completed successfully",
                BackupStatus.CompletedWithWarnings => "Completed with warnings",
                BackupStatus.Failed => "Last backup failed",
                BackupStatus.Cancelled => "Last backup was cancelled",
                BackupStatus.NeverRun => "Never executed",
                _ => "Unknown status"
            };
        }
    }

    public BackupJobViewModel(BackupJob model, IAppLogger<BackupViewModel> logger)
    {
        _logger = logger;
        UpdateFromModel(model);
    }

    public void UpdateFromModel(BackupJob model)
    {
        Id = model.Id;
        Name = model.Name;
        Description = model.Description;
        IsEnabled = model.IsEnabled;
        SourcePaths = string.Join("; ", model.SourcePaths);
        DestinationPath = model.DestinationPath;
        ScheduleDescription = GetScheduleDescription(model.Schedule);
        LastRunTime = model.LastRunTime;
        NextRunTime = model.NextRunTime;
        LastRunStatus = model.LastRunStatus;
        LastBackupSize = FormatBytes(model.LastBackupSize);
        TotalBackupCount = model.TotalBackupCount;

        OnPropertyChanged(nameof(StatusIcon));
        OnPropertyChanged(nameof(StatusColor));
        OnPropertyChanged(nameof(StatusTooltip));
    }

    private static string GetScheduleDescription(BackupSchedule schedule)
    {
        return schedule.Frequency switch
        {
            BackupFrequency.Manual => "Manual",
            BackupFrequency.EveryMinutes => $"Every {schedule.IntervalValue} minute(s)",
            BackupFrequency.EveryHours => $"Every {schedule.IntervalValue} hour(s)",
            BackupFrequency.Daily => $"Daily at {schedule.DailyTime:hh\\:mm}",
            BackupFrequency.Weekly => $"Weekly on {schedule.WeeklyDay} at {schedule.WeeklyTime:hh\\:mm}",
            BackupFrequency.Monthly => $"Monthly on day {schedule.MonthlyDay} at {schedule.MonthlyTime:hh\\:mm}",
            _ => "Unknown"
        };
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes == 0) return "0 B";

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
