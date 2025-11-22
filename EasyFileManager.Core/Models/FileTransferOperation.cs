using System;

namespace EasyFileManager.Core.Models;

public enum FileTransferType
{
    Copy,
    Move
}

public enum FileTransferStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Cancelled
}

public class FileTransferOperation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public FileTransferType Type { get; set; }
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public long TotalBytes { get; set; }
    public long TransferredBytes { get; set; }
    public FileTransferStatus Status { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string? ErrorMessage { get; set; }

    public int ProgressPercentage => TotalBytes > 0
        ? (int)((double)TransferredBytes / TotalBytes * 100)
        : 0;

    public TimeSpan Elapsed => EndTime.HasValue
        ? EndTime.Value - StartTime
        : DateTime.Now - StartTime;
}

public class FileTransferProgress
{
    public string CurrentFile { get; set; } = string.Empty;
    public long CurrentFileBytes { get; set; }
    public long CurrentFileTotalBytes { get; set; }
    public int TotalFiles { get; set; }
    public int ProcessedFiles { get; set; }
    public long TotalBytes { get; set; }
    public long TransferredBytes { get; set; }

    public int OverallProgress => TotalBytes > 0
        ? (int)((double)TransferredBytes / TotalBytes * 100)
        : 0;

    public int CurrentFileProgress => CurrentFileTotalBytes > 0
        ? (int)((double)CurrentFileBytes / CurrentFileTotalBytes * 100)
        : 0;
}