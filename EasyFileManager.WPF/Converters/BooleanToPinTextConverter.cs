using System;
using System.Globalization;
using System.Windows.Data;

namespace EasyFileManager.WPF.Converters;

/// <summary>
/// Converts boolean IsPinned to menu text "Pin Tab" or "Unpin Tab"
/// </summary>
public class BooleanToPinTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isPinned)
        {
            return isPinned ? "Unpin Tab" : "Pin Tab";
        }
        return "Pin Tab";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}