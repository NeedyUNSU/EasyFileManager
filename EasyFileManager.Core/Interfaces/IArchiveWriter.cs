using EasyFileManager.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EasyFileManager.Core.Interfaces;

/// <summary>
/// Interface for writing/creating archives
/// </summary>
public interface IArchiveWriter : IDisposable
{
    /// <summary>
    /// Archive path being created
    /// </summary>
    string ArchivePath { get; }

    /// <summary>
    /// Add files/directories to archive
    /// </summary>
    Task AddAsync(
        IEnumerable<string> sourcePaths,
        string baseDirectory,
        IProgress<ArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finalize and close the archive
    /// </summary>
    Task FinalizeAsync();
}