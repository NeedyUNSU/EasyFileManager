using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace EasyFileManager.WPF.Converters;

/// <summary>
/// Converts sort direction to Material Design icon (arrow up/down)
/// </summary>
public class SortDirectionToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is ListSortDirection direction)
        {
            return direction == ListSortDirection.Ascending
                ? PackIconKind.ArrowUp
                : PackIconKind.ArrowDown;
        }
        return PackIconKind.None;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}