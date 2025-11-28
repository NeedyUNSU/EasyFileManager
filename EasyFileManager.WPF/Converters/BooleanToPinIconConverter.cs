using System;
using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace EasyFileManager.WPF.Converters;

/// <summary>
/// Converts boolean IsPinned to Material Design icon (Pin or PinOff)
/// </summary>
public class BooleanToPinIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isPinned)
        {
            return isPinned ? PackIconKind.PinOff : PackIconKind.Pin;
        }
        return PackIconKind.Pin;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}