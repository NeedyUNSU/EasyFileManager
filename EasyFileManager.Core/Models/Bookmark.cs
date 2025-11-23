using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EasyFileManager.Core.Models;

/// <summary>
/// Represents a bookmarked directory for quick access
/// </summary>
public class Bookmark : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _path = string.Empty;
    private string _icon = "Folder";
    private int _order;

    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged();
            }
        }
    }

    public string Path
    {
        get => _path;
        set
        {
            if (_path != value)
            {
                _path = value;
                OnPropertyChanged();
            }
        }
    }

    public string Icon
    {
        get => _icon;
        set
        {
            if (_icon != value)
            {
                _icon = value;
                OnPropertyChanged();
            }
        }
    }

    public DateTime Created { get; set; } = DateTime.UtcNow;

    public int Order
    {
        get => _order;
        set
        {
            if (_order != value)
            {
                _order = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Creates a bookmark from a directory path with auto-generated name
    /// </summary>
    public static Bookmark FromPath(string path)
    {
        var dirName = System.IO.Path.GetFileName(path.TrimEnd('\\', '/'));
        if (string.IsNullOrEmpty(dirName))
        {
            // Root drive (e.g., "C:\")
            dirName = path.TrimEnd('\\');
        }

        return new Bookmark
        {
            Name = dirName,
            Path = path,
            Icon = DetermineIcon(path)
        };
    }

    private static string DetermineIcon(string path)
    {
        // Special folders get special icons
        var specialFolders = new Dictionary<string, string>
        {
            { Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Monitor" },
            { Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FileDocument" },
            { Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Image" },
            { Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "Music" },
            { Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Video" },
            { Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Home" },
        };

        foreach (var (folderPath, icon) in specialFolders)
        {
            if (path.Equals(folderPath, StringComparison.OrdinalIgnoreCase))
                return icon;
        }

        // Drive roots get harddisk icon
        if (path.Length <= 3 && path.EndsWith(":\\"))
            return "Harddisk";

        return "Folder";
    }
}