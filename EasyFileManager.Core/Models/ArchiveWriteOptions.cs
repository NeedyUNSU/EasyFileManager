using SharpCompress.Common;

namespace EasyFileManager.Core.Models;

/// <summary>
/// Options for creating archives
/// </summary>
public class ArchiveWriteOptions
{
    /// <summary>
    /// Compression level
    /// </summary>
    public CompressionType CompressionType { get; set; } = CompressionType.Deflate;

    /// <summary>
    /// Compression level (0-9 for Deflate)
    /// </summary>
    public CompressionLevel CompressionLevel { get; set; } = CompressionLevel.Default;

    /// <summary>
    /// Optional password for encryption
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Archive comment (optional)
    /// </summary>
    public string? Comment { get; set; }
}

/// <summary>
/// Compression levels
/// </summary>
public enum CompressionLevel
{
    Store = 0,      // No compression
    Fastest = 1,    // Fast but larger
    Fast = 3,
    Default = 5,    // Balanced
    Good = 7,
    Best = 9        // Slow but smallest
}