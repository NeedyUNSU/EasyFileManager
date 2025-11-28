using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EasyFileManager.Core.Services;

public class ArchiveService : IArchiveService
{
    private readonly IEnumerable<IArchivePlugin> _plugins;
    private readonly IAppLogger<ArchiveService> _logger;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private readonly Dictionary<string, string> _archivePasswords = new();

    public ArchiveService(
        IEnumerable<IArchivePlugin> plugins,
        IAppLogger<ArchiveService> logger)
    {
        _plugins = plugins ?? throw new ArgumentNullException(nameof(plugins));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        System.Diagnostics.Debug.WriteLine($"[ArchiveService][Constructor][START]");
        var pluginList = _plugins.ToList();
        System.Diagnostics.Debug.WriteLine($"[ArchiveService][Constructor][PluginsCount] Count={pluginList.Count}");
        foreach (var plugin in pluginList)
        {
            System.Diagnostics.Debug.WriteLine($"[ArchiveService][Constructor][Plugin] Name={plugin.Name}, Extensions={string.Join(",", plugin.SupportedExtensions)}");
        }
        System.Diagnostics.Debug.WriteLine($"[ArchiveService][Constructor][COMPLETE]");
    }

    public bool IsArchiveFile(string filePath)
    {
        System.Diagnostics.Debug.WriteLine($"[ArchiveService][IsArchiveFile][START] FilePath={filePath}");
        
        var extension = Path.GetExtension(filePath);
        var result = _plugins.Any(p => p.SupportsExtension(extension));
        
        System.Diagnostics.Debug.WriteLine($"[ArchiveService][IsArchiveFile][COMPLETE] Extension={extension}, IsArchive={result}");
        return result;
    }

    public async Task<DirectoryEntry> LoadArchiveAsync(
        string archivePath,
        string innerPath = "",
        string? password = null)
    {
        System.Diagnostics.Debug.WriteLine($"[ArchiveService][LoadArchiveAsync][START] ArchivePath={archivePath}, InnerPath={innerPath}, HasPassword={!string.IsNullOrEmpty(password)}");
        System.Diagnostics.Debug.WriteLine($"[ArchiveService][LoadArchiveAsync][ThreadInfo] ThreadId={System.Threading.Thread.CurrentThread.ManagedThreadId}");
        
        if (string.IsNullOrWhiteSpace(archivePath))
        {
            System.Diagnostics.Debug.WriteLine($"[ArchiveService][LoadArchiveAsync][ERROR] Archive path is empty");
            throw new ArgumentException("Archive path cannot be empty", nameof(archivePath));
        }

        if (!File.Exists(archivePath))
        {
            System.Diagnostics.Debug.WriteLine($"[ArchiveService][LoadArchiveAsync][ERROR] Archive file not found");
            throw new FileNotFoundException($"Archive not found: {archivePath}");
        }

        _logger.LogInformation("Loading archive: {Path}, InnerPath: {InnerPath}", archivePath, innerPath);

        System.Diagnostics.Debug.WriteLine($"[ArchiveService][LoadArchiveAsync][GetOrOpenArchive] Calling GetOrOpenArchiveAsync");
        
        // Get or open archive
        var reader = await GetOrOpenArchiveAsync(archivePath, password);
        
        System.Diagnostics.Debug.WriteLine($"[ArchiveService][LoadArchiveAsync][ReaderObtained] ReaderType={reader.GetType().Name}, IsEncrypted={reader.IsEncrypted}");

        // List entries at the specified path
        System.Diagnostics.Debug.WriteLine($"[ArchiveService][LoadArchiveAsync][ListEntries] Calling ListEntriesAsync");
        var entries = await reader.ListEntriesAsync(innerPath);
        var entryList = entries.ToList();
        System.Diagnostics.Debug.WriteLine($"[ArchiveService][LoadArchiveAsync][EntriesListed] Count={entryList.Count}");

        // Convert to DirectoryEntry
        var archiveName = Path.GetFileName(archivePath);
        var directoryEntry = new DirectoryEntry
        {
            Name = string.IsNullOrEmpty(innerPath) ? archiveName : Path.GetFileName(innerPath.TrimEnd('/', '\\')),
            FullPath = $"{archivePath}::{innerPath}",
            LastModified = File.GetLastWriteTimeUtc(archivePath),
            Attributes = FileAttributes.Directory
        };

        foreach (var entry in entryList)
        {
            directoryEntry.Children.Add(entry);
        }

        _logger.LogDebug("Loaded {Count} entries from archive", directoryEntry.Children.Count);
        
        System.Diagnostics.Debug.WriteLine($"[ArchiveService][LoadArchiveAsync][COMPLETE] TotalChildren={directoryEntry.Children.Count}");

        // Dispose reader (we don't cache - SharpCompress is not thread-safe)
        reader.Dispose();
        System.Diagnostics.Debug.WriteLine($"[ArchiveService][LoadArchiveAsync][ReaderDisposed]");

        return directoryEntry;
    }

    public async Task ExtractAsync(
        string archivePath,
        IEnumerable<ArchiveEntry> entries,
        string destinationPath,
        IProgress<ArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var entryList = entries.ToList();
        System.Diagnostics.Debug.WriteLine($"[ArchiveService][ExtractAsync][START] ArchivePath={archivePath}, Destination={destinationPath}, EntryCount={entryList.Count}");
        System.Diagnostics.Debug.WriteLine($"[ArchiveService][ExtractAsync][ThreadInfo] ThreadId={System.Threading.Thread.CurrentThread.ManagedThreadId}");
        
        if (string.IsNullOrWhiteSpace(archivePath))
        {
            System.Diagnostics.Debug.WriteLine($"[ArchiveService][ExtractAsync][ERROR] Archive path is empty");
            throw new ArgumentException("Archive path cannot be empty", nameof(archivePath));
        }

        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            System.Diagnostics.Debug.WriteLine($"[ArchiveService][ExtractAsync][ERROR] Destination path is empty");
            throw new ArgumentException("Destination path cannot be empty", nameof(destinationPath));
        }

        _logger.LogInformation("Extracting from {Archive} to {Destination}", archivePath, destinationPath);

        System.Diagnostics.Debug.WriteLine($"[ArchiveService][ExtractAsync][GetReader] Calling GetOrOpenArchiveAsync");
        var reader = await GetOrOpenArchiveAsync(archivePath, null);
        System.Diagnostics.Debug.WriteLine($"[ArchiveService][ExtractAsync][ReaderObtained] ReaderType={reader.GetType().Name}");
        
        try
        {
            System.Diagnostics.Debug.WriteLine($"[ArchiveService][ExtractAsync][CallExtract] Calling reader.ExtractAsync");
            await reader.ExtractAsync(entryList, destinationPath, progress, cancellationToken);
            System.Diagnostics.Debug.WriteLine($"[ArchiveService][ExtractAsync][ExtractionComplete]");
        }
        finally
        {
            reader.Dispose();
            System.Diagnostics.Debug.WriteLine($"[ArchiveService][ExtractAsync][ReaderDisposed]");
        }
        
        System.Diagnostics.Debug.WriteLine($"[ArchiveService][ExtractAsync][COMPLETE]");
    }

    public void CloseArchive(string archivePath)
    {
        System.Diagnostics.Debug.WriteLine($"[ArchiveService][CloseArchive][START] ArchivePath={archivePath}");
        // We no longer cache readers, only passwords - do nothing
        _logger.LogDebug("CloseArchive called for: {Path} (no-op, readers are not cached)", archivePath);
        System.Diagnostics.Debug.WriteLine($"[ArchiveService][CloseArchive][COMPLETE] No-op");
    }

    public void ClearCache()
    {
        System.Diagnostics.Debug.WriteLine($"[ArchiveService][ClearCache][START]");
        
        _cacheLock.Wait();
        try
        {
            var count = _archivePasswords.Count;
            _archivePasswords.Clear();

            _logger.LogInformation("Cleared password cache");
            System.Diagnostics.Debug.WriteLine($"[ArchiveService][ClearCache][COMPLETE] ClearedPasswords={count}");
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private async Task<IArchiveReader> GetOrOpenArchiveAsync(string archivePath, string? password)
    {
        System.Diagnostics.Debug.WriteLine($"[ArchiveService][GetOrOpenArchiveAsync][START] ArchivePath={archivePath}, HasPassword={!string.IsNullOrEmpty(password)}");
        System.Diagnostics.Debug.WriteLine($"[ArchiveService][GetOrOpenArchiveAsync][ThreadInfo] ThreadId={System.Threading.Thread.CurrentThread.ManagedThreadId}");
        
        await _cacheLock.WaitAsync();
        try
        {
            System.Diagnostics.Debug.WriteLine($"[ArchiveService][GetOrOpenArchiveAsync][LockAcquired]");
            
            // Try to get cached password if no password provided
            if (string.IsNullOrEmpty(password) && _archivePasswords.TryGetValue(archivePath, out var cachedPassword))
            {
                password = cachedPassword;
                System.Diagnostics.Debug.WriteLine($"[ArchiveService][GetOrOpenArchiveAsync][CachedPassword] Using cached password");
                _logger.LogDebug("Using cached password for: {Path}", archivePath);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ArchiveService][GetOrOpenArchiveAsync][NoCache] No cached password available");
            }

            // Get plugin for this archive type
            var extension = Path.GetExtension(archivePath);
            System.Diagnostics.Debug.WriteLine($"[ArchiveService][GetOrOpenArchiveAsync][Extension] Extension={extension}");
            
            var plugin = GetPluginForExtension(extension);

            if (plugin == null)
            {
                System.Diagnostics.Debug.WriteLine($"[ArchiveService][GetOrOpenArchiveAsync][ERROR] No plugin for extension {extension}");
                throw new NotSupportedException($"No plugin found for archive type: {extension}");
            }

            System.Diagnostics.Debug.WriteLine($"[ArchiveService][GetOrOpenArchiveAsync][Plugin] PluginName={plugin.Name}, CanRead={plugin.CanRead}");

            if (!plugin.CanRead)
            {
                System.Diagnostics.Debug.WriteLine($"[ArchiveService][GetOrOpenArchiveAsync][ERROR] Plugin does not support reading");
                throw new NotSupportedException($"Plugin {plugin.Name} does not support reading");
            }

            // Open archive (always fresh - SharpCompress is not thread-safe)
            IArchiveReader reader;
            try
            {
                System.Diagnostics.Debug.WriteLine($"[ArchiveService][GetOrOpenArchiveAsync][OpenForReading] Calling plugin.OpenForReading");
                reader = plugin.OpenForReading(archivePath, password);
                System.Diagnostics.Debug.WriteLine($"[ArchiveService][GetOrOpenArchiveAsync][ReaderCreated] IsEncrypted={reader.IsEncrypted}");
            }
            catch (PasswordRequiredException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ArchiveService][GetOrOpenArchiveAsync][PasswordRequired] Throwing PasswordRequiredException");
                throw;
            }
            catch (InvalidPasswordException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ArchiveService][GetOrOpenArchiveAsync][InvalidPassword] Removing cached password and rethrowing");
                _archivePasswords.Remove(archivePath);
                throw;
            }

            // Cache password if successful and archive is encrypted
            if (reader.IsEncrypted && !string.IsNullOrEmpty(password))
            {
                _archivePasswords[archivePath] = password;
                System.Diagnostics.Debug.WriteLine($"[ArchiveService][GetOrOpenArchiveAsync][CachePassword] Password cached for future use");
            }

            _logger.LogInformation("Opened archive: {Path} (Encrypted: {Encrypted})",
                archivePath, reader.IsEncrypted);

            System.Diagnostics.Debug.WriteLine($"[ArchiveService][GetOrOpenArchiveAsync][COMPLETE] Returning reader");
            return reader;
        }
        finally
        {
            _cacheLock.Release();
            System.Diagnostics.Debug.WriteLine($"[ArchiveService][GetOrOpenArchiveAsync][LockReleased]");
        }
    }

    private IArchivePlugin? GetPluginForExtension(string extension)
    {
        System.Diagnostics.Debug.WriteLine($"[ArchiveService][GetPluginForExtension][START] Extension={extension}");
        
        var plugin = _plugins.FirstOrDefault(p => p.SupportsExtension(extension));
        
        if (plugin != null)
        {
            System.Diagnostics.Debug.WriteLine($"[ArchiveService][GetPluginForExtension][COMPLETE] PluginFound={plugin.Name}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[ArchiveService][GetPluginForExtension][COMPLETE] NoPluginFound");
        }
        
        return plugin;
    }

    public void Dispose()
    {
        System.Diagnostics.Debug.WriteLine($"[ArchiveService][Dispose][START]");
        
        _cacheLock?.Dispose();
        
        System.Diagnostics.Debug.WriteLine($"[ArchiveService][Dispose][COMPLETE]");
    }
}
