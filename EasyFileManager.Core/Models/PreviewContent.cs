using System;

namespace EasyFileManager.Core.Models;

/// <summary>
/// Types of preview content that can be displayed
/// </summary>
public enum PreviewType
{
    None,
    Image,
    Text,
    Metadata,
    Unsupported,
    Directory,
    ImageTooLarge
}

/// <summary>
/// Represents content to be displayed in the preview panel
/// </summary>
public class PreviewContent
{
    public PreviewType Type { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime LastModified { get; set; }

    // For images
    public byte[]? ImageData { get; set; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }

    // For text
    public string? TextContent { get; set; }
    public int LineCount { get; set; }
    public bool IsTruncated { get; set; }

    // Metadata
    public string FileExtension { get; set; } = string.Empty;
    public string? Md5Hash { get; set; }
    public string? Sha256Hash { get; set; }
    public System.IO.FileAttributes Attributes { get; set; }

    // Error handling
    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }

    public int DirectoryFileCount { get; set; }
    public int DirectorySubdirCount { get; set; }
    public long DirectoryTotalSize { get; set; }
    public DateTime DirectoryCreated { get; set; }

    /// <summary>
    /// Creates a preview content indicating no file is selected
    /// </summary>
    public static PreviewContent Empty()
    {
        return new PreviewContent
        {
            Type = PreviewType.None,
        };
    }

    /// <summary>
    /// Creates a preview content with error
    /// </summary>
    public static PreviewContent Error(string filePath, string errorMessage)
    {
        return new PreviewContent
        {
            Type = PreviewType.Unsupported,
            FilePath = filePath,
            FileName = System.IO.Path.GetFileName(filePath),
            HasError = true,
            ErrorMessage = errorMessage
        };
    }
}