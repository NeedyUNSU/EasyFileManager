using EasyFileManager.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EasyFileManager.Core.Interfaces;

/// <summary>
/// Plugin interface for supporting different archive formats
/// Allows extensibility - new formats can be added as plugins
/// </summary>
public interface IArchivePlugin
{
    /// <summary>
    /// Plugin name (e.g., "ZIP", "RAR", "7-Zip")
    /// </summary>
    string Name { get; }

    /// <summary>
    /// File extensions supported by this plugin (e.g., [".zip", ".jar"])
    /// </summary>
    string[] SupportedExtensions { get; }

    /// <summary>
    /// Whether this plugin can read archives
    /// </summary>
    bool CanRead { get; }

    /// <summary>
    /// Whether this plugin can create/write archives
    /// </summary>
    bool CanWrite { get; }

    /// <summary>
    /// Checks if this plugin supports a given file extension
    /// </summary>
    bool SupportsExtension(string extension);

    /// <summary>
    /// Opens an archive for reading
    /// </summary>
    /// <param name="archivePath">Physical path to archive file</param>
    /// <param name="password">Optional password for encrypted archives</param>
    /// <returns>Archive reader instance</returns>
    IArchiveReader OpenForReading(string archivePath, string? password = null);

    /// <summary>
    /// Opens an archive for writing
    /// </summary>
    /// <param name="archivePath">Physical path to archive file</param>
    /// <param name="options">Options to manage compression</param>
    /// <returns>Archive writer instance</returns>
    IArchiveWriter OpenForWriting(string archivePath, ArchiveWriteOptions options);
}

/// <summary>
/// Interface for reading from an opened archive
/// </summary>
public interface IArchiveReader : IDisposable
{
    /// <summary>
    /// Whether the archive is encrypted
    /// </summary>
    bool IsEncrypted { get; }

    /// <summary>
    /// Whether a password is required to read the archive
    /// </summary>
    bool RequiresPassword { get; }

    /// <summary>
    /// Physical path to the archive file
    /// </summary>
    string ArchivePath { get; }

    /// <summary>
    /// Lists all entries at a specific path inside the archive
    /// </summary>
    /// <param name="innerPath">Path inside archive (empty for root)</param>
    /// <returns>List of entries (files and directories)</returns>
    Task<IEnumerable<ArchiveEntry>> ListEntriesAsync(string innerPath = "");

    /// <summary>
    /// Reads a file from the archive as a stream
    /// </summary>
    /// <param name="innerPath">Path to file inside archive</param>
    /// <returns>Stream with file content</returns>
    Task<Stream> ReadFileAsync(string innerPath);

    /// <summary>
    /// Extracts entries from the archive to a destination folder
    /// </summary>
    /// <param name="entries">Entries to extract</param>
    /// <param name="destinationPath">Destination folder</param>
    /// <param name="progress">Progress reporter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ExtractAsync(
        IEnumerable<ArchiveEntry> entries,
        string destinationPath,
        IProgress<ArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default);
}