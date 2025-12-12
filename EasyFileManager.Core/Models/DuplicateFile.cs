using System;
using System.Collections.Generic;

namespace EasyFileManager.Core.Models;

/// <summary>
/// Represents a file that is part of a duplicate group
/// </summary>
public class DuplicateFile
{
    public string FullPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public string Hash { get; set; } = string.Empty;
    public bool IsSelected { get; set; } // For deletion
}

/// <summary>
/// Group of duplicate files
/// </summary>
public class DuplicateGroup
{
    public string Key { get; set; } = string.Empty; // Hash or name+size
    public long Size { get; set; }
    public int FileCount => Files.Count;
    public long TotalWastedSpace => Size * (FileCount - 1);
    public List<DuplicateFile> Files { get; set; } = new();
}

/// <summary>
/// Duplicate scan options
/// </summary>
public class DuplicateScanOptions
{
    public DuplicateCompareMode CompareMode { get; set; } = DuplicateCompareMode.ContentHash;
    public bool IncludeSubfolders { get; set; } = true;
    public bool IgnoreEmptyFiles { get; set; } = true;
    public long MinimumFileSize { get; set; } = 0; // bytes
    public HashAlgorithmType HashAlgorithm { get; set; } = HashAlgorithmType.MD5;
}

/// <summary>
/// How to compare files
/// </summary>
public enum DuplicateCompareMode
{
    NameOnly,           // Same filename (fastest)
    NameAndSize,        // Same name + same size
    SizeOnly,           // Same size (different names)
    ContentHash         // Hash-based comparison (slowest, most accurate)
}

/// <summary>
/// Hash algorithm to use
/// </summary>
public enum HashAlgorithmType
{
    MD5,        // Fast, good enough for duplicates
    SHA256      // Slower, more secure
}

/// <summary>
/// Progress for duplicate scan
/// </summary>
public class DuplicateScanProgress
{
    public string CurrentFile { get; set; } = string.Empty;
    public int ProcessedFiles { get; set; }
    public int TotalFiles { get; set; }
    public long ProcessedBytes { get; set; }
    public long TotalBytes { get; set; }
    public int DuplicateGroupsFound { get; set; }
    public string Status { get; set; } = string.Empty;
}