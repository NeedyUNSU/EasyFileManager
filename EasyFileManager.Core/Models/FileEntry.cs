using System.IO;

namespace EasyFileManager.Core.Models;

public class FileEntry : FileSystemEntry
{
    public long Size { get; set; }
    public string? Hash { get; set; }
    public string Extension => Path.GetExtension(Name).ToLowerInvariant();

    public string FormattedSize => FormatFileSize(Size);
    public string FormattedDate => LastModified.ToString("yyyy-MM-dd HH:mm");
    public string Type => Path.GetExtension(Name).ToUpperInvariant().TrimStart('.');

    private static string FormatFileSize(long bytes)
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