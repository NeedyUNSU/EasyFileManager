using System;
using System.IO;

namespace EasyFileManager.Core.Models;

/// <summary>
/// Base class for file system items (files, directories, links)
/// </summary>
public abstract class FileSystemEntry
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public FileAttributes Attributes { get; set; }
}