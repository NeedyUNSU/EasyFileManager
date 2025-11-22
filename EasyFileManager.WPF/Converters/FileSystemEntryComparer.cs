using EasyFileManager.Core.Models;
using System.Collections;

namespace EasyFileManager.WPF.Converters;

/// <summary>
/// Comparer that ensures directories always appear before files
/// </summary>
public class FileSystemEntryComparer : IComparer
{
    public int Compare(object? x, object? y)
    {
        if (x is DirectoryEntry && y is FileEntry) return -1;
        if (x is FileEntry && y is DirectoryEntry) return 1;
        return 0; // Same type - will be sorted by other criteria
    }
}