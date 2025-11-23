using System;

namespace EasyFileManager.Core.Models;

public enum ConflictAction
{
    Cancel,
    Skip,
    Overwrite,
    Rename,
}

public class FileConflictInfo
{
    public string SourcePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long SourceSize { get; set; }
    public long DestinationSize { get; set; }
    public DateTime SourceModified { get; set; }
    public DateTime DestinationModified { get; set; }
}

public class FileConflictResolution
{
    public ConflictAction Action { get; set; }
    public bool ApplyToAll { get; set; }
    public string? NewName { get; set; }
}