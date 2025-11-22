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

    public FileTransferService(IAppLogger<FileTransferService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ✅ Helper class dla tracking progress (zamiast ref parameters)
    private class TransferProgress
    {
        public long TransferredBytes { get; set; }
        public int ProcessedFiles { get; set; }
    }

    public async Task CopyAsync(
        IEnumerable<string> sourcePaths,
        string destinationDirectory,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting copy operation to {Destination}", destinationDirectory);

        var paths = sourcePaths.ToList();
        var totalBytes = await CalculateTotalSizeAsync(paths, cancellationToken);
        var totalFiles = await CountFilesAsync(paths, cancellationToken);

        var tracker = new TransferProgress();

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
                    cancellationToken);
            }
        }

        _logger.LogInformation("Copy operation completed. Transferred {Bytes} bytes", tracker.TransferredBytes);
    }

    public async Task MoveAsync(
        IEnumerable<string> sourcePaths,
        string destinationDirectory,
        IProgress<FileTransferProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting move operation to {Destination}", destinationDirectory);

        // First copy
        await CopyAsync(sourcePaths, destinationDirectory, progress, cancellationToken);

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
        CancellationToken cancellationToken)
    {
        var fileName = Path.GetFileName(sourceFile);
        var destFile = Path.Combine(destinationDirectory, fileName);

        // Handle conflicts
        destFile = GetUniqueFileName(destFile);

        Directory.CreateDirectory(destinationDirectory);

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
        CancellationToken cancellationToken)
    {
        var dirName = Path.GetFileName(sourceDirectory);
        var destDir = Path.Combine(destinationDirectory, dirName);

        Directory.CreateDirectory(destDir);

        // Copy files
        foreach (var file in Directory.GetFiles(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await CopyFileAsync(file, destDir, tracker, totalBytes, totalFiles, progress, cancellationToken);
        }

        // Copy subdirectories
        foreach (var directory in Directory.GetDirectories(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await CopyDirectoryAsync(directory, destDir, tracker, totalBytes, totalFiles, progress, cancellationToken);
        }
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