using System.Collections.Generic;
using System.Linq;

namespace EasyFileManager.Core.Models;

public class DirectoryEntry : FileSystemEntry
{
    public List<FileSystemEntry> Children { get; set; } = new();
    public int FileCount => Children.OfType<FileEntry>().Count();
    public int DirectoryCount => Children.OfType<DirectoryEntry>().Count();

    public string FormattedSize => "<DIR>";
    public string FormattedDate => LastModified.ToString("yyyy-MM-dd HH:mm");
    public string Type => "Folder";
}