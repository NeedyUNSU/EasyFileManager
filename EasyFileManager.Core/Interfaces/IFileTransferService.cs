using EasyFileManager.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EasyFileManager.Core.Interfaces;

public interface IFileTransferService
{
    /// <summary>
    /// Copies files/folders from source to destination
    /// </summary>
    Task CopyAsync(
        IEnumerable<string> sourcePaths,
        string destinationDirectory,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default,
        Func<FileConflictInfo, Task<FileConflictResolution>>? conflictResolver = null);


    /// <summary>
    /// Moves files/folders from source to destination
    /// </summary>
    Task MoveAsync(
        IEnumerable<string> sourcePaths,
        string destinationDirectory,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default,
        Func<FileConflictInfo, Task<FileConflictResolution>>? conflictResolver = null);

    /// <summary>
    /// Calculates total size of files/folders
    /// </summary>
    Task<long> CalculateTotalSizeAsync(
        IEnumerable<string> paths,
        CancellationToken cancellationToken = default);
}