using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EasyFileManager.Core.Services;

public class FileTransferService : IFileTransferService
{
    private readonly IAppLogger<FileTransferService> _logger;
    private const int BufferSize = 81920; // 80KB buffer

    // Helper class dla tracking progress
    private class TransferProgress
    {
        public long TransferredBytes { get; set; }
        public int ProcessedFiles { get; set; }
    }

    // Helper class dla conflict resolution state
    private class ConflictResolutionState
    {
        public ConflictAction? GlobalAction { get; set; }
        public bool ApplyToAll { get; set; }
    }

    public FileTransferService(IAppLogger<FileTransferService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task CopyAsync(
        IEnumerable<string> sourcePaths,
        string destinationDirectory,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default,
        Func<FileConflictInfo, Task<FileConflictResolution>>? conflictResolver = null)
    {
        _logger.LogInformation("Starting copy operation to {Destination}", destinationDirectory);

        var paths = sourcePaths.ToList();
        var totalBytes = await CalculateTotalSizeAsync(paths, cancellationToken);
        var totalFiles = await CountFilesAsync(paths, cancellationToken);

        var tracker = new TransferProgress();
        var conflictState = new ConflictResolutionState(); // ✅ Create conflict state

        foreach (var sourcePath in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(sourcePath))
            {
                await CopyFileAsync(
                    sourcePath,
                    destinationDirectory,
                    tracker,
                    totalBytes,
                    totalFiles,
                    progress,
                    conflictResolver,
                    conflictState,
                    cancellationToken);
            }
            else if (Directory.Exists(sourcePath))
            {
                await CopyDirectoryAsync(
                    sourcePath,
                    destinationDirectory,
                    tracker,
                    totalBytes,
                    totalFiles,
                    progress,
                    conflictResolver,
                    conflictState,
                    cancellationToken);
            }
        }

        _logger.LogInformation("Copy operation completed. Transferred {Bytes} bytes", tracker.TransferredBytes);
    }

    public async Task MoveAsync(
        IEnumerable<string> sourcePaths,
        string destinationDirectory,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default,
        Func<FileConflictInfo, Task<FileConflictResolution>>? conflictResolver = null)
    {
        _logger.LogInformation("Starting move operation to {Destination}", destinationDirectory);

        // First copy (with conflict handling)
        await CopyAsync(sourcePaths, destinationDirectory, progress, cancellationToken, conflictResolver);

        // Then delete originals
        foreach (var sourcePath in sourcePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (File.Exists(sourcePath))
                {
                    File.Delete(sourcePath);
                    _logger.LogDebug("Deleted source file: {Path}", sourcePath);
                }
                else if (Directory.Exists(sourcePath))
                {
                    Directory.Delete(sourcePath, recursive: true);
                    _logger.LogDebug("Deleted source directory: {Path}", sourcePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete source: {Path}", sourcePath);
                throw;
            }
        }

        _logger.LogInformation("Move operation completed");
    }

    public async Task<long> CalculateTotalSizeAsync(
        IEnumerable<string> paths,
        CancellationToken cancellationToken = default)
    {
        long totalSize = 0;

        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(path))
            {
                totalSize += new FileInfo(path).Length;
            }
            else if (Directory.Exists(path))
            {
                totalSize += await GetDirectorySizeAsync(path, cancellationToken);
            }
        }

        return totalSize;
    }

    private async Task CopyFileAsync(
        string sourceFile,
        string destinationDirectory,
        TransferProgress tracker,
        long totalBytes,
        int totalFiles,
        IProgress<FileTransferProgress>? progress,
        Func<FileConflictInfo, Task<FileConflictResolution>>? conflictResolver,
        ConflictResolutionState conflictState,
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(sourceFile);
        var destFile = Path.Combine(destinationDirectory, fileName);

        Directory.CreateDirectory(destinationDirectory);

        // ✅ Handle conflicts
        if (File.Exists(destFile))
        {
            var resolution = conflictState.ApplyToAll && conflictState.GlobalAction.HasValue
                ? conflictState.GlobalAction.Value
                : await ResolveConflictAsync(sourceFile, destFile, conflictResolver, conflictState);

            switch (resolution)
            {
                case ConflictAction.Skip:
                    _logger.LogDebug("Skipping file: {Path}", sourceFile);
                    tracker.ProcessedFiles++;
                    return;

                case ConflictAction.Overwrite:
                    _logger.LogDebug("Overwriting file: {Path}", destFile);
                    File.Delete(destFile);
                    break;

                case ConflictAction.Rename:
                    destFile = GetUniqueFileName(destFile);
                    _logger.LogDebug("Renaming to: {Path}", destFile);
                    break;

                case ConflictAction.Cancel:
                    throw new OperationCanceledException("User cancelled operation");
            }
        }

        var fileInfo = new FileInfo(sourceFile);
        var fileSize = fileInfo.Length;

        _logger.LogDebug("Copying file: {Source} -> {Dest}", sourceFile, destFile);

        await using var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, true);
        await using var destStream = new FileStream(destFile, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, true);

        var buffer = new byte[BufferSize];
        long fileBytesTransferred = 0;
        int bytesRead;

        while ((bytesRead = await sourceStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await destStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);

            fileBytesTransferred += bytesRead;
            tracker.TransferredBytes += bytesRead;

            progress?.Report(new FileTransferProgress
            {
                CurrentFile = fileName,
                CurrentFileBytes = fileBytesTransferred,
                CurrentFileTotalBytes = fileSize,
                TotalFiles = totalFiles,
                ProcessedFiles = tracker.ProcessedFiles,
                TotalBytes = totalBytes,
                TransferredBytes = tracker.TransferredBytes
            });
        }

        // Preserve timestamps
        File.SetCreationTime(destFile, fileInfo.CreationTime);
        File.SetLastWriteTime(destFile, fileInfo.LastWriteTime);

        tracker.ProcessedFiles++;
    }

    private async Task CopyDirectoryAsync(
        string sourceDirectory,
        string destinationDirectory,
        TransferProgress tracker,
        long totalBytes,
        int totalFiles,
        IProgress<FileTransferProgress>? progress,
        Func<FileConflictInfo, Task<FileConflictResolution>>? conflictResolver,
        ConflictResolutionState conflictState,
        CancellationToken cancellationToken)
    {
        var dirName = Path.GetFileName(sourceDirectory);
        var destDir = Path.Combine(destinationDirectory, dirName);

        Directory.CreateDirectory(destDir);

        // Copy files
        foreach (var file in Directory.GetFiles(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await CopyFileAsync(file, destDir, tracker, totalBytes, totalFiles, progress, conflictResolver, conflictState, cancellationToken);
        }

        // Copy subdirectories
        foreach (var directory in Directory.GetDirectories(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await CopyDirectoryAsync(directory, destDir, tracker, totalBytes, totalFiles, progress, conflictResolver, conflictState, cancellationToken);
        }
    }

    private async Task<ConflictAction> ResolveConflictAsync(
        string sourceFile,
        string destFile,
        Func<FileConflictInfo, Task<FileConflictResolution>>? conflictResolver,
        ConflictResolutionState conflictState)
    {
        // If no resolver provided, default to rename
        if (conflictResolver == null)
        {
            return ConflictAction.Rename;
        }

        var sourceInfo = new FileInfo(sourceFile);
        var destInfo = new FileInfo(destFile);

        var conflict = new FileConflictInfo
        {
            SourcePath = sourceFile,
            DestinationPath = destFile,
            FileName = Path.GetFileName(sourceFile),
            SourceSize = sourceInfo.Length,
            DestinationSize = destInfo.Length,
            SourceModified = sourceInfo.LastWriteTime,
            DestinationModified = destInfo.LastWriteTime
        };

        var resolution = await conflictResolver(conflict);

        System.Diagnostics.Debug.WriteLine($">>> ResolveConflictAsync received: {resolution.Action}");

        if (resolution.Action == ConflictAction.Cancel)
        {
            System.Diagnostics.Debug.WriteLine(">>> User cancelled - throwing OperationCanceledException");
            throw new OperationCanceledException("User cancelled operation due to conflict");
        }

        if (resolution.ApplyToAll)
        {
            conflictState.ApplyToAll = true;
            conflictState.GlobalAction = resolution.Action;
        }

        return resolution.Action;
    }

    private async Task<long> GetDirectorySizeAsync(string directoryPath, CancellationToken cancellationToken)
    {
        long size = 0;

        foreach (var file in Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            size += new FileInfo(file).Length;
        }

        return await Task.FromResult(size);
    }

    private async Task<int> CountFilesAsync(IEnumerable<string> paths, CancellationToken cancellationToken)
    {
        int count = 0;

        foreach (var path in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (File.Exists(path))
            {
                count++;
            }
            else if (Directory.Exists(path))
            {
                count += Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length;
            }
        }

        return await Task.FromResult(count);
    }

    private string GetUniqueFileName(string filePath)
    {
        if (!File.Exists(filePath) && !Directory.Exists(filePath))
            return filePath;

        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);
        var counter = 1;

        string newPath;
        do
        {
            var newFileName = $"{fileNameWithoutExtension} ({counter}){extension}";
            newPath = Path.Combine(directory, newFileName);
            counter++;
        } while (File.Exists(newPath) || Directory.Exists(newPath));

        return newPath;
    }
}