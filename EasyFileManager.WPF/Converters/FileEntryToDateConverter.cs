using EasyFileManager.Core.Models;
using System.Globalization;
using System.Windows.Data;

namespace EasyFileManager.WPF.Converters;

/// <summary>
/// Converts FileSystemEntry to formatted date string
/// </summary>
public class FileEntryToDateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is FileSystemEntry entry)
        {
            return entry.LastModified.ToString("yyyy-MM-dd HH:mm");
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}