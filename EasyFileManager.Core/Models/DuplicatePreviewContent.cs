using System;

/// <summary>
/// Types of file previews
/// </summary>
public enum FilePreviewType
{
    Generic,
    Image,
    Text,
    Audio,
    Video
}

/// <summary>
/// Image preview data
/// </summary>
public class ImagePreviewData
{
    public int Width { get; set; }
    public int Height { get; set; }
    public string Resolution { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string PixelFormat { get; set; } = string.Empty;
    public float HorizontalResolution { get; set; }
    public float VerticalResolution { get; set; }
    public DateTime? DateTaken { get; set; }
    public string? CameraMake { get; set; }
    public string? CameraModel { get; set; }
}

/// <summary>
/// Text preview data
/// </summary>
public class TextPreviewData
{
    public string Content { get; set; } = string.Empty;
    public string Encoding { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public bool IsTruncated { get; set; }
}

/// <summary>
/// Generic file info
/// </summary>
public class FileInfoData
{
    public string FileName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public long Size { get; set; }
    public string SizeFormatted { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public DateTime Modified { get; set; }
    public DateTime Accessed { get; set; }
    public string Attributes { get; set; } = string.Empty;
    public bool IsReadOnly { get; set; }
}