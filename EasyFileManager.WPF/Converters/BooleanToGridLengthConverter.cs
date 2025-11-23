using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EasyFileManager.WPF.Converters;

/// <summary>
/// Converts boolean to GridLength (for column width)
/// Parameter: width value when true (e.g., "350")
/// </summary>
public class BooleanToGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isVisible && parameter is string widthStr)
        {
            if (!isVisible)
            {
                return new GridLength(0);
            }

            if (double.TryParse(widthStr, out var width))
            {
                return new GridLength(width);
            }
        }

        return new GridLength(0);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}