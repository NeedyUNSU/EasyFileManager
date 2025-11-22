using System.IO;

namespace EasyFileManager.Core.Models;

/// <summary>
/// Model representing a drive with additional metadata
/// </summary>
public class DriveInfoModel
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DriveType { get; set; } = string.Empty;
    public string RootDirectory { get; set; } = string.Empty;
    public long TotalSize { get; set; }
    public long AvailableFreeSpace { get; set; }
    public string VolumeLabel { get; set; } = string.Empty;
    public bool IsReady { get; set; }

    public string FreeSpaceFormatted => FormatBytes(AvailableFreeSpace);
    public string TotalSizeFormatted => FormatBytes(TotalSize);
    public string UsedSpaceFormatted => FormatBytes(TotalSize - AvailableFreeSpace);

    public int UsedSpacePercentage => TotalSize > 0
        ? (int)((TotalSize - AvailableFreeSpace) * 100 / TotalSize)
        : 0;

    public string DisplayIcon => DriveType switch
    {
        "Fixed" => "💾",
        "Removable" => "🔌",
        "CDRom" => "💿",
        "Network" => "☁️",
        "Ram" => "⚡",
        _ => "💾"
    };

    public string CompactDisplay => $"{DisplayIcon} {Name}";

    private static string FormatBytes(long bytes)
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

    public static DriveInfoModel FromDriveInfo(DriveInfo drive)
    {
        return new DriveInfoModel
        {
            Name = drive.Name,
            DisplayName = GetDisplayName(drive),
            DriveType = drive.DriveType.ToString(),
            RootDirectory = drive.RootDirectory.FullName,
            TotalSize = drive.IsReady ? drive.TotalSize : 0,
            AvailableFreeSpace = drive.IsReady ? drive.AvailableFreeSpace : 0,
            VolumeLabel = drive.IsReady && !string.IsNullOrWhiteSpace(drive.VolumeLabel)
                ? drive.VolumeLabel
                : "Local Disk",
            IsReady = drive.IsReady
        };
    }

    private static string GetDisplayName(DriveInfo drive)
    {
        if (!drive.IsReady)
            return $"{drive.Name} (Not Ready)";

        var label = !string.IsNullOrWhiteSpace(drive.VolumeLabel)
            ? drive.VolumeLabel
            : "Local Disk";

        return $"{drive.Name} {label}";
    }
}