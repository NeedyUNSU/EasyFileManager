using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace EasyFileManager.Core.Services;

/// <summary>
/// Service for generating file previews with support for images, text, and metadata
/// </summary>
public class PreviewService : IPreviewService
{
    private readonly IAppLogger<PreviewService> _logger;

    // Supported extensions
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".ico"
    };

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".log", ".md", ".json", ".xml", ".csv", ".ini", ".cfg", ".conf",
        ".cs", ".xaml", ".html", ".css", ".js", ".ts", ".py", ".java", ".cpp", ".h"
    };

    private const int MaxTextPreviewLines = 1000;
    private const int MaxTextPreviewBytes = 1024 * 1024; // 1 MB
    private const int MaxImageSizeBytes = 10 * 1024 * 1024; // 10 MB

    public PreviewService(IAppLogger<PreviewService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PreviewContent> GeneratePreviewAsync(
    string filePath,
    CancellationToken cancellationToken = default)
    {
        System.Diagnostics.Debug.WriteLine($"### PreviewService.GeneratePreviewAsync START - Path: {filePath}");

        if (string.IsNullOrWhiteSpace(filePath))
        {
            System.Diagnostics.Debug.WriteLine($"### Path is null or empty - returning Empty");
            return PreviewContent.Empty();
        }

        if (!File.Exists(filePath))
        {
            System.Diagnostics.Debug.WriteLine($"### File not found: {filePath}");
            _logger.LogWarning("File not found for preview: {Path}", filePath);
            return PreviewContent.Error(filePath, "File not found");
        }

        try
        {
            var fileInfo = new FileInfo(filePath);
            var extension = fileInfo.Extension.ToLowerInvariant();

            System.Diagnostics.Debug.WriteLine($"### File exists, extension: {extension}, size: {fileInfo.Length} bytes");

            _logger.LogDebug("Generating preview for: {Path} ({Size} bytes)", filePath, fileInfo.Length);

            var previewType = GetPreviewType(extension);
            System.Diagnostics.Debug.WriteLine($"### Preview type determined: {previewType}");

            var content = new PreviewContent
            {
                Type = previewType,
                FilePath = filePath,
                FileName = fileInfo.Name,
                FileSize = fileInfo.Length,
                LastModified = fileInfo.LastWriteTime,
                FileExtension = extension,
                Attributes = fileInfo.Attributes
            };

            switch (previewType)
            {
                case PreviewType.Image:
                    System.Diagnostics.Debug.WriteLine($"### Generating IMAGE preview");
                    await GenerateImagePreviewAsync(content, fileInfo, cancellationToken);
                    break;

                case PreviewType.Text:
                    System.Diagnostics.Debug.WriteLine($"### Generating TEXT preview");
                    await GenerateTextPreviewAsync(content, fileInfo, cancellationToken);
                    break;

                case PreviewType.Metadata:
                case PreviewType.Unsupported:
                    System.Diagnostics.Debug.WriteLine($"### Metadata/Unsupported - no preview generation");
                    break;
            }

            System.Diagnostics.Debug.WriteLine($"### PreviewService.GeneratePreviewAsync COMPLETE - Type: {previewType}");
            _logger.LogDebug("Preview generated successfully: {Type}", previewType);
            return content;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"### PreviewService EXCEPTION: {ex.GetType().Name} - {ex.Message}");
            _logger.LogError(ex, "Failed to generate preview for: {Path}", filePath);
            return PreviewContent.Error(filePath, ex.Message);
        }
    }

    public bool IsPreviewSupported(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return false;

        extension = extension.ToLowerInvariant();
        return ImageExtensions.Contains(extension) || TextExtensions.Contains(extension);
    }

    public PreviewType GetPreviewType(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return PreviewType.Metadata;

        extension = extension.ToLowerInvariant();

        if (ImageExtensions.Contains(extension))
            return PreviewType.Image;

        if (TextExtensions.Contains(extension))
            return PreviewType.Text;

        return PreviewType.Metadata;
    }

    public async Task<string> CalculateHashAsync(
        string filePath,
        HashAlgorithmType algorithm = HashAlgorithmType.MD5,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("File not found", filePath);

        _logger.LogDebug("Calculating {Algorithm} hash for: {Path}", algorithm, filePath);

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, true);
        using HashAlgorithm hashAlgorithm = algorithm == HashAlgorithmType.MD5
    ? (HashAlgorithm)MD5.Create()
    : (HashAlgorithm)SHA256.Create();

        var hashBytes = await hashAlgorithm.ComputeHashAsync(stream, cancellationToken);
        var hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

        _logger.LogDebug("Hash calculated: {Hash}", hash);
        return hash;
    }

    private async Task GenerateImagePreviewAsync(
    PreviewContent content,
    FileInfo fileInfo,
    CancellationToken cancellationToken)
    {
        System.Diagnostics.Debug.WriteLine($"### GenerateImagePreviewAsync START");

        // Check file size
        if (fileInfo.Length > MaxImageSizeBytes)
        {
            System.Diagnostics.Debug.WriteLine($"### Image too large: {fileInfo.Length} bytes");

            content.Type = PreviewType.ImageTooLarge;
            content.ErrorMessage = $"Image is too large for preview (max {MaxImageSizeBytes / 1024 / 1024} MB)";
            content.FileSize = fileInfo.Length; // Keep file size for display

            _logger.LogWarning("Image too large: {Size} bytes", fileInfo.Length);
            return;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine($"### Reading image bytes from: {fileInfo.FullName}");

            // Read image data
            content.ImageData = await File.ReadAllBytesAsync(fileInfo.FullName, cancellationToken);

            System.Diagnostics.Debug.WriteLine($"### Image bytes read: {content.ImageData.Length} bytes");

            // Try to get image dimensions (basic implementation)
            content.ImageWidth = 0;
            content.ImageHeight = 0;

            System.Diagnostics.Debug.WriteLine($"### GenerateImagePreviewAsync COMPLETE");
            _logger.LogDebug("Image preview generated: {Size} bytes", content.ImageData.Length);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"### GenerateImagePreviewAsync EXCEPTION: {ex.Message}");
            content.HasError = true;
            content.ErrorMessage = $"Failed to load image: {ex.Message}";
            _logger.LogError(ex, "Failed to load image");
        }
    }

    private async Task GenerateTextPreviewAsync(
    PreviewContent content,
    FileInfo fileInfo,
    CancellationToken cancellationToken)
    {
        System.Diagnostics.Debug.WriteLine($"### GenerateTextPreviewAsync START");

        // Check file size
        if (fileInfo.Length > MaxTextPreviewBytes)
        {
            content.IsTruncated = true;
            System.Diagnostics.Debug.WriteLine($"### Text file too large, will truncate");
            _logger.LogDebug("Text file too large, will truncate");
        }

        try
        {
            var lines = new List<string>();
            var bytesRead = 0;

            System.Diagnostics.Debug.WriteLine($"### Reading text file: {fileInfo.FullName}");

            using (var reader = new StreamReader(fileInfo.FullName, Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
            {
                string? line;
                int lineCount = 0;
                while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    lines.Add(line);
                    bytesRead += Encoding.UTF8.GetByteCount(line);
                    lineCount++;

                    // Limit to MaxTextPreviewLines or MaxTextPreviewBytes
                    if (lines.Count >= MaxTextPreviewLines || bytesRead >= MaxTextPreviewBytes)
                    {
                        content.IsTruncated = true;
                        System.Diagnostics.Debug.WriteLine($"### Text truncated at line {lineCount}");
                        break;
                    }
                }
            }

            content.TextContent = string.Join(Environment.NewLine, lines);
            content.LineCount = lines.Count;

            System.Diagnostics.Debug.WriteLine($"### GenerateTextPreviewAsync COMPLETE - {lines.Count} lines, truncated: {content.IsTruncated}");

            _logger.LogDebug("Text preview generated: {Lines} lines, {Truncated}",
                content.LineCount,
                content.IsTruncated ? "truncated" : "complete");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"### GenerateTextPreviewAsync EXCEPTION: {ex.Message}");
            content.HasError = true;
            content.ErrorMessage = $"Failed to load text: {ex.Message}";
            _logger.LogError(ex, "Failed to load text file");
        }
    }

    /// <summary>
    /// Generates preview for a directory
    /// </summary>
    public async Task<PreviewContent> GenerateDirectoryPreviewAsync(
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        System.Diagnostics.Debug.WriteLine($"### GenerateDirectoryPreviewAsync START - Path: {directoryPath}");

        if (!Directory.Exists(directoryPath))
        {
            _logger.LogWarning("Directory not found: {Path}", directoryPath);
            return PreviewContent.Error(directoryPath, "Directory not found");
        }

        try
        {
            var dirInfo = new DirectoryInfo(directoryPath);

            var content = new PreviewContent
            {
                Type = PreviewType.Directory,
                FilePath = directoryPath,
                FileName = dirInfo.Name,
                LastModified = dirInfo.LastWriteTime,
                FileExtension = string.Empty,
                Attributes = dirInfo.Attributes,
                DirectoryCreated = dirInfo.CreationTime
            };

            // Count files and subdirectories
            await Task.Run(() =>
            {
                try
                {
                    var files = dirInfo.GetFiles();
                    var subdirs = dirInfo.GetDirectories();

                    content.DirectoryFileCount = files.Length;
                    content.DirectorySubdirCount = subdirs.Length;

                    // Calculate total size (files only, not recursive)
                    long totalSize = 0;
                    foreach (var file in files)
                    {
                        totalSize += file.Length;
                    }
                    content.DirectoryTotalSize = totalSize;

                    System.Diagnostics.Debug.WriteLine($"### Directory stats: {files.Length} files, {subdirs.Length} subdirs, {totalSize} bytes");
                }
                catch (UnauthorizedAccessException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"### Access denied to directory: {ex.Message}");
                    content.HasError = true;
                    content.ErrorMessage = "Access denied";
                }
            }, cancellationToken);

            _logger.LogDebug("Directory preview generated: {Files} files, {Subdirs} subdirs",
                content.DirectoryFileCount, content.DirectorySubdirCount);

            return content;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"### GenerateDirectoryPreviewAsync EXCEPTION: {ex.Message}");
            _logger.LogError(ex, "Failed to generate directory preview");
            return PreviewContent.Error(directoryPath, ex.Message);
        }
    }
}