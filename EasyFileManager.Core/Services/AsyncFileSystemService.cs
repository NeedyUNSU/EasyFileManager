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

public class AsyncFileSystemService : IFileSystemService
{
    private readonly IAppLogger<AsyncFileSystemService> _logger;
    private readonly IArchiveService? _archiveService;

    public AsyncFileSystemService(
        IAppLogger<AsyncFileSystemService> logger,
        IArchiveService? archiveService = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _archiveService = archiveService;
    }

    public async Task<DirectoryEntry> LoadDirectoryAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (_archiveService != null && _archiveService.IsArchivePath(path))
        {
            return await LoadArchiveDirectoryAsync(path, cancellationToken);
        }

        return await LoadDirectoryAsync(path, null, cancellationToken);
    }

    public async Task<DirectoryEntry> LoadDirectoryAsync(
        string path,
        IProgress<LoadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Loading directory: {Path}", path);

        // Uruchom operację I/O na thread pool (nie blokuj UI)
        return await Task.Run(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dirInfo = new DirectoryInfo(path);
            if (!dirInfo.Exists)
            {
                _logger.LogWarning("Directory not found: {Path}", path);
                throw new DirectoryNotFoundException($"Directory not found: {path}");
            }

            var entry = new DirectoryEntry
            {
                Name = dirInfo.Name,
                FullPath = dirInfo.FullName,
                LastModified = dirInfo.LastWriteTimeUtc,
                Attributes = dirInfo.Attributes
            };

            // Załaduj pliki
            var files = dirInfo.GetFiles();
            var directories = dirInfo.GetDirectories();
            var totalItems = files.Length + directories.Length;
            var processedItems = 0;

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                entry.Children.Add(new FileEntry
                {
                    Name = file.Name,
                    FullPath = file.FullName,
                    Size = file.Length,
                    LastModified = file.LastWriteTimeUtc,
                    Attributes = file.Attributes
                });

                processedItems++;
                progress?.Report(new LoadProgress(processedItems, totalItems, file.Name));

                // Symulacja yielding (żeby UI się odświeżało)
                if (processedItems % 50 == 0)
                    await Task.Yield();
            }

            // Załaduj foldery (bez zagłębiania się - lazy loading)
            foreach (var dir in directories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                entry.Children.Add(new DirectoryEntry
                {
                    Name = dir.Name,
                    FullPath = dir.FullName,
                    LastModified = dir.LastWriteTimeUtc,
                    Attributes = dir.Attributes
                });

                processedItems++;
                progress?.Report(new LoadProgress(processedItems, totalItems, dir.Name));
            }

            _logger.LogDebug("Loaded {FileCount} files and {DirCount} directories from {Path}",
                entry.FileCount, entry.DirectoryCount, path);

            return entry;

        }, cancellationToken);
    }

    public async Task<List<DriveInfo>> GetDrivesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading available drives");

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return DriveInfo.GetDrives().Where(d => d.IsReady).ToList();
        }, cancellationToken);
    }

    public async Task<List<DriveInfoModel>> GetDrivesWithMetadataAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Loading drives with metadata");

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var drives = DriveInfo.GetDrives()
                .Select(DriveInfoModel.FromDriveInfo)
                .ToList();

            _logger.LogDebug("Loaded {Count} drives", drives.Count);

            return drives;
        }, cancellationToken);
    }

    public bool IsArchivePath(string path)
    {
        return _archiveService?.IsArchivePath(path) ?? false;
    }

    public bool IsArchiveFile(string filePath)
    {
        return _archiveService?.IsArchiveFile(filePath) ?? false;
    }

    public async Task<DirectoryEntry> LoadArchiveDirectoryAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (_archiveService == null)
            throw new NotSupportedException("Archive service is not available");

        _logger.LogInformation("Loading archive directory: {Path}", path);

        var (archivePath, innerPath) = _archiveService.ParseArchivePath(path);

        try
        {
            return await _archiveService.LoadArchiveAsync(archivePath, innerPath);
        }
        catch (PasswordRequiredException)
        {
            // Re-throw - UI layer will handle password prompt
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load archive: {Path}", path);
            throw;
        }
    }
}