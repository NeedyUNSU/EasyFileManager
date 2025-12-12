using EasyFileManager.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace EasyFileManager.Core.Interfaces;

/// <summary>
/// High-level service for archive operations
/// Manages plugins, caching, and provides unified API
/// </summary>
public interface IArchiveService
{
    // ===== Reading Operations =====

    /// <summary>
    /// Loads directory listing from inside an archive
    /// </summary>
    /// <param name="archivePath">Physical path to archive file (e.g., "C:\archive.zip")</param>
    /// <param name="innerPath">Path inside archive (empty for root)</param>
    /// <param name="password">Optional password for encrypted archives</param>
    /// <returns>DirectoryEntry containing archive entries</returns>
    Task<DirectoryEntry> LoadArchiveAsync(
        string archivePath,
        string innerPath = "",
        string? password = null);

    /// <summary>
    /// Reads a file from inside an archive as a stream
    /// </summary>
    /// <param name="archivePath">Physical path to archive</param>
    /// <param name="innerPath">Path to file inside archive</param>
    /// <returns>Stream with file content</returns>
    Task<Stream> ReadFileFromArchiveAsync(string archivePath, string innerPath);

    /// <summary>
    /// Extracts entries from an archive to a destination folder
    /// </summary>
    Task ExtractAsync(
        string archivePath,
        IEnumerable<ArchiveEntry> entries,
        string destinationPath,
        IProgress<ArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Create new archive from files/directories
    /// </summary>
    Task CreateAsync(
        string archivePath,
        IEnumerable<string> sourcePaths,
        string baseDirectory,
        ArchiveWriteOptions options,
        IProgress<ArchiveProgress>? progress = null,
        CancellationToken cancellationToken = default);

    // ===== Utility Methods =====

    /// <summary>
    /// Checks if a path is an archive path (contains "::" separator)
    /// </summary>
    /// <param name="path">Path to check</param>
    /// <returns>True if path is archive path</returns>
    bool IsArchivePath(string path);

    /// <summary>
    /// Checks if a file extension is supported by any plugin
    /// </summary>
    bool IsArchiveFile(string filePath);

    /// <summary>
    /// Parses an archive path into components
    /// </summary>
    /// <param name="virtualPath">Virtual path (e.g., "C:\archive.zip::folder\file.txt")</param>
    /// <returns>Tuple of (archivePath, innerPath)</returns>
    (string archivePath, string innerPath) ParseArchivePath(string virtualPath);

    /// <summary>
    /// Gets the plugin that supports a given file extension
    /// </summary>
    IArchivePlugin? GetPluginForExtension(string extension);

    /// <summary>
    /// Closes and removes an archive from cache
    /// </summary>
    void CloseArchive(string archivePath);

    /// <summary>
    /// Clears all cached archives
    /// </summary>
    void ClearCache();
}