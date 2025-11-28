using System;
using System.Collections.Generic;

namespace EasyFileManager.Core.Models;

/// <summary>
/// Represents a single tab in the file explorer panel
/// Stores the complete state of a tab including path, selection, sorting, etc.
/// </summary>
public class TabModel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Display name for the tab (usually last directory name)
    /// </summary>
    public string Title { get; set; } = "New Tab";

    /// <summary>
    /// Current directory path
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Selected item path (for restoration)
    /// </summary>
    public string? SelectedItemPath { get; set; }

    /// <summary>
    /// Sort column name (Name, Size, Type, LastModified)
    /// </summary>
    public string SortColumn { get; set; } = "Name";

    /// <summary>
    /// Sort direction (Ascending/Descending)
    /// </summary>
    public string SortDirection { get; set; } = "Ascending";

    /// <summary>
    /// Filter text (if any)
    /// </summary>
    public string FilterText { get; set; } = string.Empty;

    /// <summary>
    /// Whether this tab is pinned (cannot be closed accidentally)
    /// </summary>
    public bool IsPinned { get; set; }

    /// <summary>
    /// Order position in tab bar
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime Created { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a new tab with a given path
    /// </summary>
    public static TabModel FromPath(string path, int order = 0)
    {
        var dirName = System.IO.Path.GetFileName(path.TrimEnd('\\', '/'));
        if (string.IsNullOrEmpty(dirName))
        {
            // Root drive (e.g., "C:\")
            dirName = path.TrimEnd('\\');
        }

        return new TabModel
        {
            Title = dirName,
            Path = path,
            Order = order
        };
    }

    /// <summary>
    /// Updates the title based on current path
    /// </summary>
    public void UpdateTitle()
    {
        var dirName = System.IO.Path.GetFileName(Path.TrimEnd('\\', '/'));
        if (string.IsNullOrEmpty(dirName))
        {
            dirName = Path.TrimEnd('\\');
        }
        Title = dirName;
    }
}

/// <summary>
/// Container for persisting tabs to storage
/// </summary>
public class TabSession
{
    public List<TabModel> Tabs { get; set; } = new();
    public Guid? ActiveTabId { get; set; }
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
}