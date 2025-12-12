using EasyFileManager.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace EasyFileManager.Core.Interfaces;

/// <summary>
/// Service for generating file previews
/// Supports images, text files, and metadata extraction
/// </summary>
public interface IPreviewService
{
    /// <summary>
    /// Generates preview content for a file
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Preview content or error</returns>
    Task<PreviewContent> GeneratePreviewAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a file type is supported for preview
    /// </summary>
    /// <param name="extension">File extension (e.g., ".txt", ".jpg")</param>
    /// <returns>True if preview is supported</returns>
    bool IsPreviewSupported(string extension);

    /// <summary>
    /// Generates preview content for a directory
    /// </summary>
    Task<PreviewContent> GenerateDirectoryPreviewAsync(
        string directoryPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the preview type for a file extension
    /// </summary>
    /// <param name="extension">File extension</param>
    /// <returns>Type of preview that will be generated</returns>
    PreviewType GetPreviewType(string extension);

    /// <summary>
    /// Calculates file hash (MD5 or SHA256)
    /// This is expensive operation - use only when needed
    /// </summary>
    Task<string> CalculateHashAsync(
        string filePath,
        HashAlgorithmType algorithm = HashAlgorithmType.MD5,
        CancellationToken cancellationToken = default);
}