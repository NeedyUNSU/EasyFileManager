using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace EasyFileManager.WPF.Converters;

/// <summary>
/// Converts enum value to Visibility based on parameter match
/// </summary>
public class EnumToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value == null || parameter == null)
            return Visibility.Collapsed;

        var enumValue = value.ToString();
        var targetValue = parameter.ToString();

        return string.Equals(enumValue, targetValue, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}