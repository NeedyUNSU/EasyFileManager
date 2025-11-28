using EasyFileManager.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EasyFileManager.Core.Interfaces;

/// <summary>
/// Async file system operations
/// </summary>
public interface IFileSystemService
{
    /// <summary>
    /// Loads directory contents asynchronously without blocking UI
    /// </summary>
    Task<DirectoryEntry> LoadDirectoryAsync(
        string path,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads directory with progress reporting
    /// </summary>
    Task<DirectoryEntry> LoadDirectoryAsync(
        string path,
        IProgress<LoadProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available drives
    /// </summary>
    Task<List<DriveInfo>> GetDrivesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available drives with metadata
    /// </summary>
    Task<List<DriveInfoModel>> GetDrivesWithMetadataAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a path points to an archive (contains :: separator)
    /// </summary>
    bool IsArchivePath(string path);

    /// <summary>
    /// Checks if a file is a supported archive type
    /// </summary>
    bool IsArchiveFile(string filePath);

    /// <summary>
    /// Loads contents of an archive or path inside an archive
    /// </summary>
    Task<DirectoryEntry> LoadArchiveDirectoryAsync(
        string path,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Progress data for long operations
/// </summary>
public record LoadProgress(int ProcessedItems, int TotalItems, string CurrentItem);