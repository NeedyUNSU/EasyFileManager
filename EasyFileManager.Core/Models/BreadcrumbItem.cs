using System;
using System.Collections.Generic;
using System.Linq;

namespace EasyFileManager.Core.Models;

/// <summary>
/// Represents a single segment in breadcrumb navigation
/// </summary>
public class BreadcrumbItem
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public bool IsLast { get; set; }

    public static List<BreadcrumbItem> FromPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new List<BreadcrumbItem>();

        var items = new List<BreadcrumbItem>();
        var segments = path.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);

        // Pierwszy segment (dysk)
        if (segments.Length > 0)
        {
            var driveLetter = segments[0];
            items.Add(new BreadcrumbItem
            {
                Name = driveLetter.EndsWith(":") ? driveLetter + "\\" : driveLetter,
                FullPath = driveLetter.EndsWith(":") ? driveLetter + "\\" : driveLetter,
                IsLast = segments.Length == 1
            });
        }

        // Pozostałe segmenty (foldery)
        for (int i = 1; i < segments.Length; i++)
        {
            var fullPath = string.Join("\\", segments.Take(i + 1));
            if (!fullPath.Contains(":"))
                fullPath = segments[0] + "\\" + fullPath;

            items.Add(new BreadcrumbItem
            {
                Name = segments[i],
                FullPath = fullPath,
                IsLast = i == segments.Length - 1
            });
        }

        return items;
    }
}