using EasyFileManager.Core.Models;
using MaterialDesignThemes.Wpf;
using System.Globalization;
using System.Windows.Data;

namespace EasyFileManager.WPF.Converters;

/// <summary>
/// Converts FileSystemEntry to Material Design icon
/// </summary>
public class FileEntryToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            DirectoryEntry => PackIconKind.Folder,
            ArchiveDirectoryEntry => PackIconKind.FolderZip,
            ArchiveFileEntry => PackIconKind.FileDocumentOutline,
            FileEntry => PackIconKind.FileDocument,
            _ => PackIconKind.File
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}