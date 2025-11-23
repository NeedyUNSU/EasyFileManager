using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EasyFileManager.WPF.Converters;

/// <summary>
/// Converts count to visibility (Visible when count is 0, Collapsed otherwise)
/// Used for empty state messages
/// </summary>
public class InverseCountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int count)
        {
            return count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}