using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EasyFileManager.Core.Services.Plugins;

/// <summary>
/// Plugin for ZIP archive support using SharpCompress
/// Supports: .zip, .jar (Java archives are ZIP-based)
/// </summary>
public class ZipPlugin : IArchivePlugin
{
    public string Name => "ZIP";

    public string[] SupportedExtensions => new[] { ".zip", ".jar" };

    public bool CanRead => true;

    public bool CanWrite => true;

    private readonly IAppLogger<ZipPlugin> _logger;

    public ZipPlugin(IAppLogger<ZipPlugin> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool SupportsExtension(string extension)
    {
        return SupportedExtensions.Contains(extension.ToLowerInvariant());
    }

    public IArchiveReader OpenForReading(string archivePath, string? password = null)
    {
        if (!File.Exists(archivePath))
            throw new FileNotFoundException($"Archive not found: {archivePath}");

        _logger.LogInformation("Opening ZIP archive: {Path}", archivePath);

        try
        {
            var readerOptions = new ReaderOptions
            {
                Password = password
            };

            var archive = SharpCompress.Archives.Zip.ZipArchive.Open(archivePath, readerOptions);

            // Check if password is required
            if (archive.Entries.Any(e => e.IsEncrypted) && string.IsNullOrEmpty(password))
            {
                archive.Dispose();
                throw new PasswordRequiredException(archivePath);
            }

            // Validate password by attempting to read first encrypted entry
            if (!string.IsNullOrEmpty(password) && archive.Entries.Any(e => e.IsEncrypted))
            {
                var firstEncryptedEntry = archive.Entries.First(e => e.IsEncrypted);
                try
                {
                    using var testStream = firstEncryptedEntry.OpenEntryStream();
                    var buffer = new byte[1];
                    testStream.Read(buffer, 0, 1); // Try to read 1 byte to validate password
                }
                catch (CryptographicException)
                {
                    archive.Dispose();
                    throw new InvalidPasswordException(archivePath, "Invalid password");
                }
                catch (Exception ex) when (ex.Message.Contains("password") || ex.Message.Contains("decrypt"))
                {
                    archive.Dispose();
                    throw new InvalidPasswordException(archivePath, "Invalid password");
                }
            }

            return new ZipArchiveReader(archive, archivePath, _logger);
        }
        catch (InvalidFormatException ex)
        {
            _logger.LogError(ex, "Invalid ZIP format: {Path}", archivePath);
            throw new InvalidDataException($"Invalid ZIP archive: {archivePath}", ex);
        }
        catch (CryptographicException ex)
        {
            _logger.LogError(ex, "Invalid password for: {Path}", archivePath);
            throw new InvalidPasswordException(archivePath, "Invalid password");
        }
    }

    public IArchiveWriter OpenForWriting(string archivePath, ArchiveWriteOptions options)
    {
        _logger.LogInformation("Creating ZIP archive: {Path}", archivePath);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(archivePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return new ZipArchiveWriter(archivePath, options, _logger);
    }
}

/// <summary>
/// Implementation of IArchiveReader for ZIP archives
/// </summary>
internal class ZipArchiveReader : IArchiveReader
{
    private readonly ZipArchive _archive;
    private readonly IAppLogger<ZipPlugin> _logger;
    private bool _disposed;

    public bool IsEncrypted { get; }
    public bool RequiresPassword => IsEncrypted;
    public string ArchivePath { get; }

    public ZipArchiveReader(ZipArchive archive, string archivePath, IAppLogger<ZipPlugin> logger)
    {
        _archive = archive ?? throw new ArgumentNullException(nameof(archive));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArchivePath = archivePath;
        IsEncrypted = _archive.Entries.Any(e => e.IsEncrypted);
    }

    public Task<IEnumerable<ArchiveEntry>> ListEntriesAsync(string innerPath = "")
    {
        _logger.LogDebug("Listing entries in: {InnerPath}", innerPath);

        var normalizedPath = NormalizePath(innerPath);
        var entries = new List<ArchiveEntry>();

        // Get all entries that are direct children of innerPath
        var allEntries = _archive.Entries.Where(e => !e.IsDirectory).ToList();
        var processedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in allEntries)
        {
            var entryPath = NormalizePath(entry.Key);

            // Skip entries not in current path
            if (!string.IsNullOrEmpty(normalizedPath))
            {
                if (!entryPath.StartsWith(normalizedPath + "/", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Remove the base path
                entryPath = entryPath.Substring(normalizedPath.Length + 1);
            }

            // Check if this is a direct child or in a subdirectory
            var slashIndex = entryPath.IndexOf('/');

            if (slashIndex == -1)
            {
                // Direct file
                entries.Add(new ArchiveFileEntry
                {
                    Name = Path.GetFileName(entry.Key),
                    FullPath = entry.Key,
                    ArchivePath = ArchivePath,
                    InnerPath = entry.Key,
                    CompressedSize = entry.CompressedSize,
                    UncompressedSize = entry.Size,
                    LastModified = entry.LastModifiedTime ?? DateTime.UtcNow,
                    IsEncrypted = entry.IsEncrypted,
                    CompressionMethod = entry.CompressionType.ToString(),
                    Attributes = FileAttributes.Archive
                });
            }
            else
            {
                // File in subdirectory - create directory entry
                var dirName = entryPath.Substring(0, slashIndex);
                var dirPath = string.IsNullOrEmpty(normalizedPath)
                    ? dirName
                    : normalizedPath + "/" + dirName;

                if (!processedDirs.Contains(dirPath))
                {
                    processedDirs.Add(dirPath);

                    entries.Add(new ArchiveDirectoryEntry
                    {
                        Name = dirName,
                        FullPath = dirPath,
                        ArchivePath = ArchivePath,
                        InnerPath = dirPath,
                        LastModified = DateTime.UtcNow,
                        Attributes = FileAttributes.Directory
                    });
                }
            }
        }

        _logger.LogDebug("Found {Count} entries in {Path}", entries.Count, normalizedPath);

        return Task.FromResult<IEnumerable<ArchiveEntry>>(entries);
    }

    public Task<Stream> ReadFileAsync(string innerPath)
    {
        var normalizedPath = NormalizePath(innerPath);
        var entry = _archive.Entries.FirstOrDefault(e =>
            NormalizePath(e.Key).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

        if (entry == null)
            throw new FileNotFoundException($"File not found in archive: {innerPath}");

        _logger.LogDebug("Reading file from archive: {Path}", innerPath);

        // Extract to memory stream (for preview)
        var memoryStream = new MemoryStream();
        using (var entryStream = entry.OpenEntryStream())
        {
            entryStream.CopyTo(memoryStream);
        }
        memoryStream.Position = 0;

        return Task.FromResult<Stream>(memoryStream);
    }

    public async Task ExtractAsync(
        IEnumerable<ArchiveEntry> entries,
        string destinationPath,
        IProgress<ArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting {Count} entries to {Destination}",
            entries.Count(), destinationPath);

        Directory.CreateDirectory(destinationPath);

        var entriesToExtract = entries.ToList();
        var totalFiles = entriesToExtract.Count;
        var processedFiles = 0;
        var totalBytes = entriesToExtract.OfType<ArchiveFileEntry>().Sum(e => e.UncompressedSize);
        var processedBytes = 0L;

        foreach (var archiveEntry in entriesToExtract)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = archiveEntry.InnerPath;
            var outputPath = Path.Combine(destinationPath, relativePath);

            // ✅ Create directory based on entry type
            if (archiveEntry is ArchiveFileEntry)
            {
                // For files: create parent directory
                var outputDir = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDir))
                {
                    Directory.CreateDirectory(outputDir);
                }
            }
            else if (archiveEntry is ArchiveDirectoryEntry)
            {
                // For directories: create the directory itself
                Directory.CreateDirectory(outputPath);
            }

            if (archiveEntry is ArchiveFileEntry fileEntry)
            {
                // ✅ Extract file
                var normalizedPath = NormalizePath(fileEntry.InnerPath);
                var entry = _archive.Entries.FirstOrDefault(e =>
                    NormalizePath(e.Key).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

                if (entry != null)
                {
                    // Report progress
                    progress?.Report(new ArchiveProgress
                    {
                        CurrentFile = fileEntry.Name,
                        ProcessedFiles = processedFiles,
                        TotalFiles = totalFiles,
                        ProcessedBytes = processedBytes,
                        TotalBytes = totalBytes,
                        Status = ArchiveOperationStatus.Processing
                    });

                    // Extract file
                    using (var entryStream = entry.OpenEntryStream())
                    using (var outputStream = File.Create(outputPath))
                    {
                        entryStream.CopyTo(outputStream);
                    }

                    // Preserve timestamp
                    if (entry.LastModifiedTime.HasValue)
                    {
                        File.SetLastWriteTime(outputPath, entry.LastModifiedTime.Value);
                    }

                    processedBytes += fileEntry.UncompressedSize;
                }
            }
            else if (archiveEntry is ArchiveDirectoryEntry dirEntry)
            {
                // ✅ Handle directory
                _logger.LogDebug("Creating directory: {Path}", outputPath);

                // Report progress for directory
                progress?.Report(new ArchiveProgress
                {
                    CurrentFile = dirEntry.Name + "/",
                    ProcessedFiles = processedFiles,
                    TotalFiles = totalFiles,
                    ProcessedBytes = processedBytes,
                    TotalBytes = totalBytes,
                    Status = ArchiveOperationStatus.Processing
                });

                // Try to preserve directory timestamp (optional)
                var normalizedPath = NormalizePath(dirEntry.InnerPath);
                var archiveDirEntry = _archive.Entries.FirstOrDefault(e =>
                    e.IsDirectory && NormalizePath(e.Key).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

                if (archiveDirEntry?.LastModifiedTime.HasValue == true)
                {
                    try
                    {
                        Directory.SetLastWriteTime(outputPath, archiveDirEntry.LastModifiedTime.Value);
                    }
                    catch
                    {
                        // Ignore timestamp errors for directories
                        _logger.LogDebug("Could not set directory timestamp: {Path}", outputPath);
                    }
                }
            }

            processedFiles++;
        }

        progress?.Report(new ArchiveProgress
        {
            CurrentFile = "",
            ProcessedFiles = totalFiles,
            TotalFiles = totalFiles,
            ProcessedBytes = totalBytes,
            TotalBytes = totalBytes,
            Status = ArchiveOperationStatus.Completed
        });

        _logger.LogInformation("Extraction completed successfully");
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        return path.Replace('\\', '/').TrimEnd('/');
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _archive?.Dispose();
        _disposed = true;

        _logger.LogDebug("ZipArchiveReader disposed");
    }
}

/// <summary>
/// Implementation of IArchiveWriter for ZIP archives
/// </summary>
internal class ZipArchiveWriter : IArchiveWriter
{
    private readonly string _archivePath;
    private readonly ArchiveWriteOptions _options;
    private readonly IAppLogger<ZipPlugin> _logger;
    private bool _disposed;
    private readonly List<string> _addedFiles = new();

    public string ArchivePath => _archivePath;

    public ZipArchiveWriter(
        string archivePath,
        ArchiveWriteOptions options,
        IAppLogger<ZipPlugin> logger)
    {
        _archivePath = archivePath ?? throw new ArgumentNullException(nameof(archivePath));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task AddAsync(
        IEnumerable<string> sourcePaths,
        string baseDirectory,
        IProgress<ArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Adding {Count} items to archive", sourcePaths.Count());

        await Task.Run(() =>
        {
            // Collect all files to add BEFORE creating archive
            var filesToAdd = new List<(string sourcePath, string entryPath)>();

            foreach (var sourcePath in sourcePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (File.Exists(sourcePath))
                {
                    var entryPath = GetRelativePath(baseDirectory, sourcePath);
                    filesToAdd.Add((sourcePath, entryPath));
                }
                else if (Directory.Exists(sourcePath))
                {
                    AddDirectoryFiles(sourcePath, baseDirectory, filesToAdd);
                }
            }

            var totalFiles = filesToAdd.Count;
            var totalBytes = filesToAdd.Sum(f => new FileInfo(f.sourcePath).Length);
            var processedFiles = 0;
            var processedBytes = 0L;

            // ✅ Create archive in using block for proper disposal
            using (var archive = ZipArchive.Create())
            {
                // Add files to archive
                foreach (var (sourcePath, entryPath) in filesToAdd)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var fileInfo = new FileInfo(sourcePath);

                    progress?.Report(new ArchiveProgress
                    {
                        CurrentFile = Path.GetFileName(sourcePath),
                        ProcessedFiles = processedFiles,
                        TotalFiles = totalFiles,
                        ProcessedBytes = processedBytes,
                        TotalBytes = totalBytes,
                        Status = ArchiveOperationStatus.Processing
                    });

                    archive.AddEntry(entryPath, sourcePath);
                    _addedFiles.Add(entryPath);

                    processedFiles++;
                    processedBytes += fileInfo.Length;

                    _logger.LogDebug("Added to archive: {Entry}", entryPath);
                }

                // ✅ Save archive - still inside using block
                _logger.LogInformation("Saving archive to: {Path}", _archivePath);

                var compressionType = _options.CompressionLevel == CompressionLevel.Store
                    ? SharpCompress.Common.CompressionType.None
                    : SharpCompress.Common.CompressionType.Deflate;

                var writerOptions = new WriterOptions(compressionType)
                {
                    LeaveStreamOpen = false,
                    ArchiveEncoding = new ArchiveEncoding { Default = System.Text.Encoding.UTF8 }
                };

                // ✅ Use using for output stream as well
                using (var outputStream = File.Create(_archivePath))
                {
                    archive.SaveTo(outputStream, writerOptions);
                }

                // ✅ Archive is disposed here when exiting using block
            }

            progress?.Report(new ArchiveProgress
            {
                CurrentFile = "",
                ProcessedFiles = totalFiles,
                TotalFiles = totalFiles,
                ProcessedBytes = totalBytes,
                TotalBytes = totalBytes,
                Status = ArchiveOperationStatus.Completed
            });

            _logger.LogInformation("Archive created successfully: {Path} ({Count} files)",
                _archivePath, totalFiles);

        }, cancellationToken);
    }

    public Task FinalizeAsync()
    {
        // Archive is finalized in AddAsync when SaveTo is called
        _logger.LogDebug("Archive finalized: {Path}", _archivePath);
        return Task.CompletedTask;
    }

    private void AddDirectoryFiles(
        string directoryPath,
        string baseDirectory,
        List<(string sourcePath, string entryPath)> filesToAdd)
    {
        // Add all files in directory
        foreach (var file in Directory.GetFiles(directoryPath, "*", SearchOption.AllDirectories))
        {
            var entryPath = GetRelativePath(baseDirectory, file);
            filesToAdd.Add((file, entryPath));
        }
    }

    private string GetRelativePath(string baseDirectory, string fullPath)
    {
        var basePath = Path.GetFullPath(baseDirectory);
        var targetPath = Path.GetFullPath(fullPath);

        if (!targetPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
        {
            // If not relative, use filename only
            return Path.GetFileName(fullPath);
        }

        var relativePath = targetPath.Substring(basePath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return relativePath.Replace('\\', '/'); // ZIP uses forward slashes
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _logger.LogDebug("ZipArchiveWriter disposed");
    }
}