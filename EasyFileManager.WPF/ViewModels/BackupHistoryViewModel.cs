using System;
using CommunityToolkit.Mvvm.ComponentModel;
using EasyFileManager.Core.Models;

namespace EasyFileManager.WPF.ViewModels;

/// <summary>
/// ViewModel for backup history entry
/// </summary>
public partial class BackupHistoryViewModel : ObservableObject
{
    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private Guid _jobId;

    [ObservableProperty]
    private string _jobName = string.Empty;

    [ObservableProperty]
    private DateTime _startTime;

    [ObservableProperty]
    private DateTime? _endTime;

    [ObservableProperty]
    private string _duration = string.Empty;

    [ObservableProperty]
    private BackupStatus _status;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private int _totalFiles;

    [ObservableProperty]
    private int _processedFiles;

    [ObservableProperty]
    private int _failedFiles;

    [ObservableProperty]
    private string _totalSize = "0 B";

    [ObservableProperty]
    private string _destinationPath = string.Empty;

    public string StatusIcon
    {
        get
        {
            return Status switch
            {
                BackupStatus.Completed => "CheckCircle",
                BackupStatus.CompletedWithWarnings => "AlertCircle",
                BackupStatus.Failed => "CloseCircle",
                BackupStatus.Running => "ProgressClock",
                BackupStatus.Cancelled => "Cancel",
                _ => "HelpCircle"
            };
        }
    }

    public string StatusColor
    {
        get
        {
            return Status switch
            {
                BackupStatus.Completed => "Green",
                BackupStatus.CompletedWithWarnings => "Orange",
                BackupStatus.Failed => "Red",
                BackupStatus.Running => "Blue",
                BackupStatus.Cancelled => "Gray",
                _ => "Gray"
            };
        }
    }

    public BackupHistoryViewModel(BackupHistory model)
    {
        Id = model.Id;
        JobId = model.JobId;
        JobName = model.JobName;
        StartTime = model.StartTime;
        EndTime = model.EndTime;
        Duration = model.Duration.ToString(@"hh\:mm\:ss");
        Status = model.Status;
        ErrorMessage = model.ErrorMessage;
        TotalFiles = model.TotalFiles;
        ProcessedFiles = model.ProcessedFiles;
        FailedFiles = model.FailedFiles;
        TotalSize = FormatBytes(model.ProcessedBytes);
        DestinationPath = model.DestinationPath;
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
