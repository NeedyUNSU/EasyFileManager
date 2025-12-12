using EasyFileManager.Core.Interfaces;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

namespace EasyFileManager.Core.Services;

public class FilePreviewService
{
    private readonly IAppLogger<FilePreviewService> _logger;

    // Supported formats
    private static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".ico" };
    private static readonly string[] TextExtensions = { ".txt", ".log", ".xml", ".json", ".csv", ".md", ".ini", ".cfg", ".config" };
    private static readonly string[] AudioExtensions = { ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".wma" };
    private static readonly string[] VideoExtensions = { ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".m4v" };

    public FilePreviewService(IAppLogger<FilePreviewService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public FilePreviewType GetPreviewType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        if (ImageExtensions.Contains(extension)) return FilePreviewType.Image;
        if (TextExtensions.Contains(extension)) return FilePreviewType.Text;
        if (AudioExtensions.Contains(extension)) return FilePreviewType.Audio;
        if (VideoExtensions.Contains(extension)) return FilePreviewType.Video;

        return FilePreviewType.Generic;
    }

    /// <summary>
    /// Load image preview with metadata
    /// </summary>
    public async Task<ImagePreviewData?> LoadImagePreviewAsync(string filePath)
    {
        try
        {
            return await Task.Run(() =>
            {
                using var image = System.Drawing.Image.FromFile(filePath);

                var data = new ImagePreviewData
                {
                    Width = image.Width,
                    Height = image.Height,
                    Resolution = $"{image.Width} × {image.Height}",
                    Format = image.RawFormat.ToString(),
                    PixelFormat = image.PixelFormat.ToString(),
                    HorizontalResolution = image.HorizontalResolution,
                    VerticalResolution = image.VerticalResolution
                };

                // Try to extract EXIF data
                try
                {
                    // Date Taken
                    if (image.PropertyIdList.Contains(0x9003)) // DateTimeOriginal
                    {
                        var prop = image.GetPropertyItem(0x9003);
                        var dateStr = Encoding.ASCII.GetString(prop.Value).TrimEnd('\0');
                        if (DateTime.TryParseExact(dateStr, "yyyy:MM:dd HH:mm:ss", null,
                            System.Globalization.DateTimeStyles.None, out var dateTaken))
                        {
                            data.DateTaken = dateTaken;
                        }
                    }

                    // Camera Make
                    if (image.PropertyIdList.Contains(0x010F))
                    {
                        var prop = image.GetPropertyItem(0x010F);
                        data.CameraMake = Encoding.ASCII.GetString(prop.Value).TrimEnd('\0');
                    }

                    // Camera Model
                    if (image.PropertyIdList.Contains(0x0110))
                    {
                        var prop = image.GetPropertyItem(0x0110);
                        data.CameraModel = Encoding.ASCII.GetString(prop.Value).TrimEnd('\0');
                    }
                }
                catch
                {
                    // EXIF data not available or corrupted
                }

                return data;
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.ToString(), "Failed to load image preview: {Path}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Load text file preview
    /// </summary>
    public async Task<TextPreviewData?> LoadTextPreviewAsync(string filePath, int maxChars = 10000)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);

            // Don't load huge files
            if (fileInfo.Length > 1024 * 1024) // 1 MB
            {
                return new TextPreviewData
                {
                    Content = $"[File too large to preview: {FormatBytes(fileInfo.Length)}]",
                    Encoding = "N/A",
                    LineCount = 0,
                    IsTruncated = true
                };
            }

            var content = await File.ReadAllTextAsync(filePath);
            var isTruncated = content.Length > maxChars;

            if (isTruncated)
            {
                content = content.Substring(0, maxChars) + "\n\n[... content truncated ...]";
            }

            var lines = content.Split('\n').Length;

            return new TextPreviewData
            {
                Content = content,
                Encoding = "UTF-8", // Simplified
                LineCount = lines,
                IsTruncated = isTruncated
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.ToString(), "Failed to load text preview: {Path}", filePath);
            return null;
        }
    }

    /// <summary>
    /// Get basic file info for any file type
    /// </summary>
    public FileInfoData GetFileInfo(string filePath)
    {
        var fileInfo = new FileInfo(filePath);

        return new FileInfoData
        {
            FileName = fileInfo.Name,
            Extension = fileInfo.Extension,
            Size = fileInfo.Length,
            SizeFormatted = FormatBytes(fileInfo.Length),
            Created = fileInfo.CreationTime,
            Modified = fileInfo.LastWriteTime,
            Accessed = fileInfo.LastAccessTime,
            Attributes = fileInfo.Attributes.ToString(),
            IsReadOnly = fileInfo.IsReadOnly
        };
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}