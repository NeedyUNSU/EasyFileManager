using System;

namespace EasyFileManager.Core.Models;

/// <summary>
/// Progress information for archive operations
/// </summary>
public class ArchiveProgress
{
    public string CurrentFile { get; set; } = string.Empty;
    public int ProcessedFiles { get; set; }
    public int TotalFiles { get; set; }
    public long ProcessedBytes { get; set; }
    public long TotalBytes { get; set; }
    public ArchiveOperationStatus Status { get; set; }

    public int PercentComplete => TotalFiles > 0
        ? (int)((double)ProcessedFiles / TotalFiles * 100)
        : 0;
}

/// <summary>
/// Status of archive operation
/// </summary>
public enum ArchiveOperationStatus
{
    Preparing,
    Processing,
    Finalizing,
    Completed,
    Error,
    Cancelled
}

/// <summary>
/// Exception thrown when archive requires password
/// </summary>
public class PasswordRequiredException : Exception
{
    public string ArchivePath { get; }

    public PasswordRequiredException(string archivePath)
        : base($"Archive requires password: {archivePath}")
    {
        ArchivePath = archivePath;
    }

    public PasswordRequiredException(string archivePath, string message)
        : base(message)
    {
        ArchivePath = archivePath;
    }
}

/// <summary>
/// Exception thrown when password is incorrect
/// </summary>
public class InvalidPasswordException : Exception
{
    public string ArchivePath { get; }

    public InvalidPasswordException(string archivePath)
        : base($"Invalid password for archive: {archivePath}")
    {
        ArchivePath = archivePath;
    }

    public InvalidPasswordException(string archivePath, string message)
        : base(message)
    {
        ArchivePath = archivePath;
    }
}