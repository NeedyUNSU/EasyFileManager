using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EasyFileManager.Core.Services;

/// <summary>
/// Main service for archive operations
/// Manages plugins, caching, and provides unified API
/// </summary>
public class ArchiveService : IArchiveService
{
    private readonly List<IArchivePlugin> _plugins;
    private readonly Dictionary<string, IArchiveReader> _openArchives;
    private readonly Dictionary<string, string> _archivePasswords; // Session password cache
    private readonly IAppLogger<ArchiveService> _logger;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private const int MaxCachedArchives = 5;

    public ArchiveService(
        IEnumerable<IArchivePlugin> plugins,
        IAppLogger<ArchiveService> logger)
    {
        _plugins = plugins?.ToList() ?? throw new ArgumentNullException(nameof(plugins));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _openArchives = new Dictionary<string, IArchiveReader>(StringComparer.OrdinalIgnoreCase);
        _archivePasswords = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        _logger.LogInformation("ArchiveService initialized with {Count} plugins", _plugins.Count);
        foreach (var plugin in _plugins)
        {
            _logger.LogDebug("Loaded plugin: {Name} (Extensions: {Extensions})",
                plugin.Name, string.Join(", ", plugin.SupportedExtensions));
        }
    }

    public async Task<DirectoryEntry> LoadArchiveAsync(
        string archivePath,
        string innerPath = "",
        string? password = null)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
            throw new ArgumentException("Archive path cannot be empty", nameof(archivePath));

        if (!File.Exists(archivePath))
            throw new FileNotFoundException($"Archive not found: {archivePath}");

        _logger.LogInformation("Loading archive: {Path}, InnerPath: {InnerPath}", archivePath, innerPath);

        // Get or open archive
        var reader = await GetOrOpenArchiveAsync(archivePath, password);

        // List entries at the specified path
        var entries = await reader.ListEntriesAsync(innerPath);

        // Convert to DirectoryEntry
        var archiveName = Path.GetFileName(archivePath);
        var directoryEntry = new DirectoryEntry
        {
            Name = string.IsNullOrEmpty(innerPath) ? archiveName : Path.GetFileName(innerPath.TrimEnd('/', '\\')),
            FullPath = $"{archivePath}::{innerPath}",
            LastModified = File.GetLastWriteTimeUtc(archivePath),
            Attributes = FileAttributes.Directory
        };

        foreach (var entry in entries)
        {
            directoryEntry.Children.Add(entry);
        }

        _logger.LogDebug("Loaded {Count} entries from archive", directoryEntry.Children.Count);
        reader.Dispose();

        return directoryEntry;
    }

    public async Task<Stream> ReadFileFromArchiveAsync(string archivePath, string innerPath)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
            throw new ArgumentException("Archive path cannot be empty", nameof(archivePath));

        if (string.IsNullOrWhiteSpace(innerPath))
            throw new ArgumentException("Inner path cannot be empty", nameof(innerPath));

        _logger.LogDebug("Reading file from archive: {Archive}, File: {File}", archivePath, innerPath);

        var reader = await GetOrOpenArchiveAsync(archivePath, null);
        return await reader.ReadFileAsync(innerPath);
    }

    public async Task ExtractAsync(
        string archivePath,
        IEnumerable<ArchiveEntry> entries,
        string destinationPath,
        IProgress<ArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
            throw new ArgumentException("Archive path cannot be empty", nameof(archivePath));

        if (string.IsNullOrWhiteSpace(destinationPath))
            throw new ArgumentException("Destination path cannot be empty", nameof(destinationPath));

        _logger.LogInformation("Extracting from {Archive} to {Destination}", archivePath, destinationPath);

        var reader = await GetOrOpenArchiveAsync(archivePath, null);
        await reader.ExtractAsync(entries, destinationPath, progress, cancellationToken);
    }

    public async Task CreateAsync(
    string archivePath,
    IEnumerable<string> sourcePaths,
    string baseDirectory,
    ArchiveWriteOptions options,
    IProgress<ArchiveProgress>? progress = null,
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
            throw new ArgumentException("Archive path cannot be empty", nameof(archivePath));

        if (sourcePaths == null || !sourcePaths.Any())
            throw new ArgumentException("Source paths cannot be empty", nameof(sourcePaths));

        _logger.LogInformation("Creating archive: {Path} from {Count} source(s)",
            archivePath, sourcePaths.Count());

        var extension = Path.GetExtension(archivePath);
        var plugin = GetPluginForExtension(extension);

        if (plugin == null)
            throw new NotSupportedException($"No plugin found for archive type: {extension}");

        if (!plugin.CanWrite)
            throw new NotSupportedException($"Plugin {plugin.Name} does not support writing");

        IArchiveWriter? writer = null;
        try
        {
            writer = plugin.OpenForWriting(archivePath, options);
            await writer.AddAsync(sourcePaths, baseDirectory, progress, cancellationToken);
            await writer.FinalizeAsync();
        }
        finally
        {
            writer?.Dispose();
        }

        _logger.LogInformation("Archive created successfully: {Path}", archivePath);
    }

    public bool IsArchivePath(string path)
    {
        return !string.IsNullOrEmpty(path) && path.Contains("::");
    }

    public bool IsArchiveFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        var extension = Path.GetExtension(filePath);
        return GetPluginForExtension(extension) != null;
    }

    public (string archivePath, string innerPath) ParseArchivePath(string virtualPath)
    {
        if (string.IsNullOrWhiteSpace(virtualPath))
            throw new ArgumentException("Virtual path cannot be empty", nameof(virtualPath));

        if (!virtualPath.Contains("::"))
            throw new ArgumentException("Path is not an archive path", nameof(virtualPath));

        var parts = virtualPath.Split(new[] { "::" }, 2, StringSplitOptions.None);
        return (parts[0], parts.Length > 1 ? parts[1] : string.Empty);
    }

    public IArchivePlugin? GetPluginForExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return null;

        extension = extension.ToLowerInvariant();
        if (!extension.StartsWith("."))
            extension = "." + extension;

        return _plugins.FirstOrDefault(p => p.SupportsExtension(extension));
    }

    public void CloseArchive(string archivePath)
    {
        _cacheLock.Wait();
        try
        {
            if (_openArchives.TryGetValue(archivePath, out var reader))
            {
                reader.Dispose();
                _openArchives.Remove(archivePath);
                _archivePasswords.Remove(archivePath);

                _logger.LogDebug("Closed archive: {Path}", archivePath);
            }
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public void ClearCache()
    {
        _cacheLock.Wait();
        try
        {
            foreach (var reader in _openArchives.Values)
            {
                reader.Dispose();
            }

            _openArchives.Clear();
            _archivePasswords.Clear();

            _logger.LogInformation("Cleared archive cache");
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    // ===== Private Helper Methods =====

    private async Task<IArchiveReader> GetOrOpenArchiveAsync(string archivePath, string? password)
    {
        await _cacheLock.WaitAsync();
        try
        {
            // Check if already open
            if (_openArchives.TryGetValue(archivePath, out var existingReader))
            {
                _logger.LogDebug("Using cached archive: {Path}", archivePath);
                return existingReader;
            }

            // Get plugin for this archive type
            var extension = Path.GetExtension(archivePath);
            var plugin = GetPluginForExtension(extension);

            if (plugin == null)
                throw new NotSupportedException($"No plugin found for archive type: {extension}");

            if (!plugin.CanRead)
                throw new NotSupportedException($"Plugin {plugin.Name} does not support reading");

            // Try to get cached password if no password provided
            if (string.IsNullOrEmpty(password) && _archivePasswords.TryGetValue(archivePath, out var cachedPassword))
            {
                password = cachedPassword;
                _logger.LogDebug("Using cached password for: {Path}", archivePath);
            }

            // Open archive
            IArchiveReader reader;
            try
            {
                reader = plugin.OpenForReading(archivePath, password);
            }
            catch (PasswordRequiredException)
            {
                // Re-throw - caller needs to provide password
                throw;
            }
            catch (InvalidPasswordException)
            {
                // Remove cached password if it was wrong
                _archivePasswords.Remove(archivePath);
                throw;
            }

            // Cache password if successful and archive is encrypted
            if (reader.IsEncrypted && !string.IsNullOrEmpty(password))
            {
                _archivePasswords[archivePath] = password;
            }

            // Add to cache
            _openArchives[archivePath] = reader;

            // Enforce cache limit
            if (_openArchives.Count > MaxCachedArchives)
            {
                var oldestKey = _openArchives.Keys.First();
                var oldestReader = _openArchives[oldestKey];
                oldestReader.Dispose();
                _openArchives.Remove(oldestKey);

                _logger.LogDebug("Removed oldest cached archive: {Path}", oldestKey);
            }

            _logger.LogInformation("Opened archive: {Path} (Encrypted: {Encrypted})",
                archivePath, reader.IsEncrypted);

            return reader;
        }
        finally
        {
            _cacheLock.Release();
        }
    }
}