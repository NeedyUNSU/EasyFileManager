using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;
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

    public bool CanWrite => false;

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
        System.Diagnostics.Debug.WriteLine($"[ZipPlugin][OpenForReading][START] ArchivePath={archivePath}, HasPassword={!string.IsNullOrEmpty(password)}");
        System.Diagnostics.Debug.WriteLine($"[ZipPlugin][OpenForReading][ThreadInfo] ThreadId={System.Threading.Thread.CurrentThread.ManagedThreadId}");
        
        if (!File.Exists(archivePath))
        {
            System.Diagnostics.Debug.WriteLine($"[ZipPlugin][OpenForReading][ERROR] Archive file not found");
            throw new FileNotFoundException($"Archive not found: {archivePath}");
        }

        _logger.LogInformation("Opening ZIP archive: {Path}", archivePath);

        try
        {
            System.Diagnostics.Debug.WriteLine($"[ZipPlugin][OpenForReading][CreateOptions] Creating ReaderOptions");
            var readerOptions = new ReaderOptions
            {
                Password = password
            };

            System.Diagnostics.Debug.WriteLine($"[ZipPlugin][OpenForReading][OpenArchive] Calling ZipArchive.Open");
            var archive = ZipArchive.Open(archivePath, readerOptions);
            var entriesCount = archive.Entries.Count();
            System.Diagnostics.Debug.WriteLine($"[ZipPlugin][OpenForReading][ArchiveOpened] EntriesCount={entriesCount}");

            // Check if password is required
            var encryptedCount = archive.Entries.Count(e => e.IsEncrypted);
            System.Diagnostics.Debug.WriteLine($"[ZipPlugin][OpenForReading][EncryptionCheck] EncryptedEntries={encryptedCount}, TotalEntries={entriesCount}");
            
            if (archive.Entries.Any(e => e.IsEncrypted) && string.IsNullOrEmpty(password))
            {
                System.Diagnostics.Debug.WriteLine($"[ZipPlugin][OpenForReading][PasswordRequired] Disposing archive and throwing PasswordRequiredException");
                archive.Dispose();
                throw new PasswordRequiredException(archivePath);
            }

            if (!string.IsNullOrEmpty(password) && archive.Entries.Any(e => e.IsEncrypted))
            {
                System.Diagnostics.Debug.WriteLine($"[ZipPlugin][OpenForReading][ValidatePassword] Testing password by reading first encrypted entry");
                var firstEncryptedEntry = archive.Entries.First(e => e.IsEncrypted);
                var testEntryKey = firstEncryptedEntry.Key;
                System.Diagnostics.Debug.WriteLine($"[ZipPlugin][OpenForReading][ValidatePassword] TestEntry={testEntryKey}");
                
                try
                {
                    using var testStream = firstEncryptedEntry.OpenEntryStream();
                    var buffer = new byte[1];
                    var bytesRead = testStream.Read(buffer, 0, 1);
                    System.Diagnostics.Debug.WriteLine($"[ZipPlugin][OpenForReading][PasswordValid] Password validated successfully, BytesRead={bytesRead}");
                }
                catch (CryptographicException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ZipPlugin][OpenForReading][InvalidPassword] CryptographicException: {ex.Message}");
                    archive.Dispose();
                    throw new InvalidPasswordException(archivePath, "Invalid password");
                }
                catch (Exception ex) when (ex.Message.Contains("password") || ex.Message.Contains("decrypt"))
                {
                    System.Diagnostics.Debug.WriteLine($"[ZipPlugin][OpenForReading][InvalidPassword] Password exception: {ex.Message}");
                    archive.Dispose();
                    throw new InvalidPasswordException(archivePath, "Invalid password");
                }
            }

            System.Diagnostics.Debug.WriteLine($"[ZipPlugin][OpenForReading][CreateReader] Creating ZipArchiveReader");
            var reader = new ZipArchiveReader(archive, archivePath, _logger);
            System.Diagnostics.Debug.WriteLine($"[ZipPlugin][OpenForReading][COMPLETE] Reader created successfully");
            return reader;
        }
        catch (InvalidFormatException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ZipPlugin][OpenForReading][ERROR] InvalidFormatException: {ex.Message}");
            _logger.LogError(ex, "Invalid ZIP format: {Path}", archivePath);
            throw new InvalidDataException($"Invalid ZIP archive: {archivePath}", ex);
        }
        catch (CryptographicException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ZipPlugin][OpenForReading][ERROR] CryptographicException: {ex.Message}");
            _logger.LogError(ex, "Invalid password for: {Path}", archivePath);
            throw new InvalidPasswordException(archivePath, "Invalid password");
        }
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
        System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][Constructor][START] ArchivePath={archivePath}");
        System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][Constructor][ThreadInfo] ThreadId={System.Threading.Thread.CurrentThread.ManagedThreadId}");
        
        _archive = archive ?? throw new ArgumentNullException(nameof(archive));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        ArchivePath = archivePath;
        IsEncrypted = _archive.Entries.Any(e => e.IsEncrypted);
        
        System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][Constructor][COMPLETE] IsEncrypted={IsEncrypted}");
    }

    public Task<IEnumerable<ArchiveEntry>> ListEntriesAsync(string innerPath = "")
    {
        System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ListEntriesAsync][START] InnerPath={innerPath}");
        System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ListEntriesAsync][ThreadInfo] ThreadId={System.Threading.Thread.CurrentThread.ManagedThreadId}");
        
        _logger.LogDebug("Listing entries in: {InnerPath}", innerPath);

        var normalizedPath = NormalizePath(innerPath);
        System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ListEntriesAsync][NormalizedPath] NormalizedPath={normalizedPath}");
        
        var entries = new List<ArchiveEntry>();

        var allEntries = _archive.Entries.Where(e => !e.IsDirectory).ToList();
        System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ListEntriesAsync][AllEntries] TotalNonDirectoryEntries={allEntries.Count}");
        
        var processedDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in allEntries)
        {
            var entryPath = NormalizePath(entry.Key);

            // Skip entries not in current path
            if (!string.IsNullOrEmpty(normalizedPath))
            {
                if (!entryPath.StartsWith(normalizedPath + "/", StringComparison.OrdinalIgnoreCase))
                    continue;

                entryPath = entryPath.Substring(normalizedPath.Length + 1);
            }

            var slashIndex = entryPath.IndexOf('/');

            if (slashIndex == -1)
            {
                // Direct file
                var fileEntry = new ArchiveFileEntry
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
                };
                entries.Add(fileEntry);
                System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ListEntriesAsync][AddFile] Name={fileEntry.Name}, Size={fileEntry.UncompressedSize}");
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

                    var dirEntry = new ArchiveDirectoryEntry
                    {
                        Name = dirName,
                        FullPath = dirPath,
                        ArchivePath = ArchivePath,
                        InnerPath = dirPath,
                        LastModified = DateTime.UtcNow,
                        Attributes = FileAttributes.Directory
                    };
                    entries.Add(dirEntry);
                    System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ListEntriesAsync][AddDirectory] Name={dirEntry.Name}");
                }
            }
        }

        _logger.LogDebug("Found {Count} entries in {Path}", entries.Count, normalizedPath);
        System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ListEntriesAsync][COMPLETE] TotalEntriesFound={entries.Count}");

        return Task.FromResult<IEnumerable<ArchiveEntry>>(entries);
    }

    public Task<Stream> ReadFileAsync(string innerPath)
    {
        System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ReadFileAsync][START] InnerPath={innerPath}");
        System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ReadFileAsync][ThreadInfo] ThreadId={System.Threading.Thread.CurrentThread.ManagedThreadId}");
        
        var normalizedPath = NormalizePath(innerPath);
        System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ReadFileAsync][NormalizedPath] NormalizedPath={normalizedPath}");
        
        var entry = _archive.Entries.FirstOrDefault(e =>
            NormalizePath(e.Key).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

        if (entry == null)
        {
            System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ReadFileAsync][ERROR] File not found in archive");
            throw new FileNotFoundException($"File not found in archive: {innerPath}");
        }

        System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ReadFileAsync][EntryFound] Key={entry.Key}, Size={entry.Size}");
        _logger.LogDebug("Reading file from archive: {Path}", innerPath);

        // Extract to memory stream
        var memoryStream = new MemoryStream();
        using (var entryStream = entry.OpenEntryStream())
        {
            entryStream.CopyTo(memoryStream);
        }
        memoryStream.Position = 0;

        System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ReadFileAsync][COMPLETE] BytesRead={memoryStream.Length}");
        return Task.FromResult<Stream>(memoryStream);
    }

    public Task ExtractAsync(
        IEnumerable<ArchiveEntry> entries,
        string destinationPath,
        IProgress<ArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var entryList = entries.ToList();
        System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ExtractAsync][START] Destination={destinationPath}, EntryCount={entryList.Count}");
        System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ExtractAsync][ThreadInfo] ThreadId={System.Threading.Thread.CurrentThread.ManagedThreadId}");
        
        _logger.LogInformation("Extracting {Count} entries to {Destination}",
            entryList.Count, destinationPath);

        System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ExtractAsync][CreateDestination] Creating destination directory");
        Directory.CreateDirectory(destinationPath);

        var entriesToExtract = entryList;
        var totalFiles = entriesToExtract.Count;
        var processedFiles = 0;
        var totalBytes = entriesToExtract.OfType<ArchiveFileEntry>().Sum(e => e.UncompressedSize);
        var processedBytes = 0L;

        System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ExtractAsync][Statistics] TotalFiles={totalFiles}, TotalBytes={totalBytes}");

        foreach (var archiveEntry in entriesToExtract)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relativePath = archiveEntry.InnerPath;
            var outputPath = Path.Combine(destinationPath, relativePath);
            System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ExtractAsync][ProcessEntry] Entry={archiveEntry.Name}, OutputPath={outputPath}");

            // Create directory if needed
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            if (archiveEntry is ArchiveFileEntry fileEntry)
            {
                System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ExtractAsync][ExtractFile] File={fileEntry.Name}, Size={fileEntry.UncompressedSize}");
                
                // Extract file
                var normalizedPath = NormalizePath(fileEntry.InnerPath);
                System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ExtractAsync][FindEntry] NormalizedPath={normalizedPath}");
                
                var entry = _archive.Entries.FirstOrDefault(e =>
                    NormalizePath(e.Key).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase));

                if (entry != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ExtractAsync][EntryFound] Key={entry.Key}");
                    
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
                    System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ExtractAsync][ProgressReported] File={fileEntry.Name}");

                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ExtractAsync][OpenStream] Opening entry stream");
                        using (var entryStream = entry.OpenEntryStream())
                        using (var outputStream = File.Create(outputPath))
                        {
                            System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ExtractAsync][CopyData] Copying data");
                            entryStream.CopyTo(outputStream);
                        }
                        System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ExtractAsync][FileSaved] File saved successfully");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ExtractAsync][ERROR] Exception during extraction: {ex.GetType().Name}: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ExtractAsync][ERROR] StackTrace: {ex.StackTrace}");
                        throw;
                    }

                    // Preserve timestamp
                    if (entry.LastModifiedTime.HasValue)
                    {
                        File.SetLastWriteTime(outputPath, entry.LastModifiedTime.Value);
                        System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ExtractAsync][TimestampSet] Timestamp={entry.LastModifiedTime.Value}");
                    }

                    processedBytes += fileEntry.UncompressedSize;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ExtractAsync][WARNING] Entry not found in archive");
                }
            }
            else if (archiveEntry is ArchiveDirectoryEntry)
            {
                System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ExtractAsync][Directory] Skipping directory (already created)");
            }

            processedFiles++;
        }

        // Report completion
        progress?.Report(new ArchiveProgress
        {
            CurrentFile = "",
            ProcessedFiles = totalFiles,
            TotalFiles = totalFiles,
            ProcessedBytes = totalBytes,
            TotalBytes = totalBytes,
            Status = ArchiveOperationStatus.Completed
        });
        System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ExtractAsync][CompletionReported]");

        _logger.LogInformation("Extraction completed successfully");
        System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][ExtractAsync][COMPLETE] Success");

        return Task.CompletedTask;
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return string.Empty;

        return path.Replace('\\', '/').TrimEnd('/');
    }

    public void Dispose()
    {
        System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][Dispose][START] ArchivePath={ArchivePath}");
        
        if (_disposed)
        {
            System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][Dispose][SKIP] Already disposed");
            return;
        }

        _archive?.Dispose();
        _disposed = true;

        _logger.LogDebug("ZipArchiveReader disposed");
        System.Diagnostics.Debug.WriteLine($"[ZipArchiveReader][Dispose][COMPLETE]");
    }
}
