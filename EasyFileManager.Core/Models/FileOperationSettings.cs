namespace EasyFileManager.Core.Models;

/// <summary>
/// File operation settings
/// </summary>
public class FileOperationSettings
{
    /// <summary>
    /// Ask for confirmation before deleting files
    /// </summary>
    public bool ConfirmDelete { get; set; } = true;

    /// <summary>
    /// Ask for confirmation before overwriting files
    /// </summary>
    public bool ConfirmOverwrite { get; set; } = true;

    /// <summary>
    /// Move deleted files to Recycle Bin instead of permanent deletion
    /// </summary>
    public bool UseRecycleBin { get; set; } = true;

    /// <summary>
    /// Copy buffer size in KB (for large file operations)
    /// </summary>
    public int CopyBufferSizeKB { get; set; } = 4096;

    /// <summary>
    /// Show progress dialog for copy/move operations
    /// </summary>
    public bool ShowProgressDialog { get; set; } = true;

    /// <summary>
    /// Verify file integrity after copy (hash verification)
    /// </summary>
    public bool VerifyAfterCopy { get; set; } = false;

    /// <summary>
    /// Preserve file timestamps (created, modified, accessed)
    /// </summary>
    public bool PreserveTimestamps { get; set; } = true;

    /// <summary>
    /// Preserve file attributes (read-only, hidden, system)
    /// </summary>
    public bool PreserveAttributes { get; set; } = true;
}
