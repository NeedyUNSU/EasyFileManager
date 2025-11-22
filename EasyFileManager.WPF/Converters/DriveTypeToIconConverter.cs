using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace EasyFileManager.WPF.Converters;

/// <summary>
/// Converts drive type to Material Design icon
/// </summary>
public class DriveTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string driveType)
        {
            return driveType switch
            {
                "Fixed" => PackIconKind.Harddisk,
                "Removable" => PackIconKind.Usb,
                "CDRom" => PackIconKind.Disc,
                "Network" => PackIconKind.CloudOutline,
                "Ram" => PackIconKind.Memory,
                _ => PackIconKind.Harddisk
            };
        }
        return PackIconKind.Harddisk;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}