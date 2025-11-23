using EasyFileManager.Core.Models;
using System.Collections;
using System.ComponentModel;

namespace EasyFileManager.WPF.Converters;

/// <summary>
/// Comparer that ensures directories always appear before files
/// Used as primary sort in CollectionView
/// </summary>
public class FileSystemEntryComparer : IComparer
{
    private readonly ListSortDirection _direction;

    public FileSystemEntryComparer(ListSortDirection direction = ListSortDirection.Ascending)
    {
        _direction = direction;
    }

    public int Compare(object? x, object? y)
    {
        // Both null - equal
        if (x == null && y == null) return 0;
        if (x == null) return _direction == ListSortDirection.Ascending ? -1 : 1;
        if (y == null) return _direction == ListSortDirection.Ascending ? 1 : -1;

        // Both directories or both files - equal at this level
        bool xIsDir = x is DirectoryEntry;
        bool yIsDir = y is DirectoryEntry;

        if (xIsDir && !yIsDir)
            return _direction == ListSortDirection.Ascending ? -1 : 1;

        if (!xIsDir && yIsDir)
            return _direction == ListSortDirection.Ascending ? 1 : -1;

        return 0; // Same type - will be sorted by secondary criteria
    }
}