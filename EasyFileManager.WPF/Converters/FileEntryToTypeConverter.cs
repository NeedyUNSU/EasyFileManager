using EasyFileManager.Core.Models;
using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace EasyFileManager.WPF.Converters;

/// <summary>
/// Converts FileSystemEntry to display type (Folder, File Extension, etc.)
/// </summary>
public class FileEntryToTypeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            DirectoryEntry => "Folder",
            FileEntry file => string.IsNullOrEmpty(Path.GetExtension(file.Name))
                ? "File"
                : Path.GetExtension(file.Name).ToUpperInvariant().TrimStart('.'),
            _ => "Unknown"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}