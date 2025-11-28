using System;
using System.IO;

namespace EasyFileManager.Core.Models;

/// <summary>
/// Base class for entries inside archives
/// Extends FileSystemEntry to work seamlessly with existing code
/// </summary>
public abstract class ArchiveEntry : FileSystemEntry
{
    /// <summary>
    /// Physical path to the archive file (e.g., "C:\archive.zip")
    /// </summary>
    public string ArchivePath { get; set; } = string.Empty;

    /// <summary>
    /// Path inside the archive (e.g., "folder\file.txt")
    /// </summary>
    public string InnerPath { get; set; } = string.Empty;

    /// <summary>
    /// Whether this entry is encrypted
    /// </summary>
    public bool IsEncrypted { get; set; }

    /// <summary>
    /// Full virtual path with :: separator (e.g., "C:\archive.zip::folder\file.txt")
    /// </summary>
    public string VirtualPath => $"{ArchivePath}::{InnerPath}";

    /// <summary>
    /// Display name for breadcrumb (archive filename)
    /// </summary>
    public string ArchiveDisplayName => Path.GetFileName(ArchivePath);
}

/// <summary>
/// Represents a file inside an archive
/// </summary>
public class ArchiveFileEntry : ArchiveEntry
{
    /// <summary>
    /// Compressed size in bytes
    /// </summary>
    public long CompressedSize { get; set; }

    /// <summary>
    /// Uncompressed (original) size in bytes
    /// </summary>
    public long UncompressedSize { get; set; }

    /// <summary>
    /// Compression method used (e.g., "Deflate", "LZMA")
    /// </summary>
    public string CompressionMethod { get; set; } = string.Empty;

    /// <summary>
    /// Compression ratio (0-100%)
    /// </summary>
    public double CompressionRatio => UncompressedSize > 0
        ? (1 - (double)CompressedSize / UncompressedSize) * 100
        : 0;

    /// <summary>
    /// For display - shows uncompressed size
    /// </summary>
    public long Size => UncompressedSize;
}

/// <summary>
/// Represents a directory inside an archive
/// </summary>
public class ArchiveDirectoryEntry : ArchiveEntry
{
    /// <summary>
    /// Number of files in this directory (not recursive)
    /// </summary>
    public int FileCount { get; set; }

    /// <summary>
    /// Number of subdirectories
    /// </summary>
    public int DirectoryCount { get; set; }
}