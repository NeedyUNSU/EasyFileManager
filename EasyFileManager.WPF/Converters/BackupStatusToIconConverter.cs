using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using EasyFileManager.Core.Models;

namespace EasyFileManager.WPF.Converters;

/// <summary>
/// Converts (IsEnabled, LastRunStatus) to status icon name
/// </summary>
public class BackupStatusToIconConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length != 2 || values[0] is not bool isEnabled || values[1] is not BackupStatus status)
            return "HelpCircle";

        // Priority: Running > Disabled > Failed > CompletedWithWarnings > Completed > NeverRun
        if (status == BackupStatus.Running)
            return "ProgressClock";

        if (!isEnabled)
            return "Cancel";

        if (status == BackupStatus.Failed)
            return "CloseCircle";

        if (status == BackupStatus.CompletedWithWarnings)
            return "AlertCircle";

        if (status == BackupStatus.Completed)
            return "CheckCircle";

        return "HelpCircle";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts (IsEnabled, LastRunStatus) to status color
/// </summary>
public class BackupStatusToColorConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length != 2 || values[0] is not bool isEnabled || values[1] is not BackupStatus status)
            return new SolidColorBrush(Color.FromRgb(117, 117, 117)); // Gray

        // Priority: Running > Disabled > Failed > CompletedWithWarnings > Completed > NeverRun
        if (status == BackupStatus.Running)
            return new SolidColorBrush(Color.FromRgb(33, 150, 243)); // Blue

        if (!isEnabled)
            return new SolidColorBrush(Color.FromRgb(158, 158, 158)); // Gray (Disabled)

        if (status == BackupStatus.Failed)
            return new SolidColorBrush(Color.FromRgb(244, 67, 54)); // Red

        if (status == BackupStatus.CompletedWithWarnings)
            return new SolidColorBrush(Color.FromRgb(255, 152, 0)); // Orange

        if (status == BackupStatus.Completed)
            return new SolidColorBrush(Color.FromRgb(76, 175, 80)); // Green

        return new SolidColorBrush(Color.FromRgb(117, 117, 117)); // Dark Gray
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts (IsEnabled, LastRunStatus) to tooltip text
/// </summary>
public class BackupStatusToTooltipConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length != 2 || values[0] is not bool isEnabled || values[1] is not BackupStatus status)
            return "Unknown status";

        if (!isEnabled)
            return "Disabled";

        return status switch
        {
            BackupStatus.Running => "Backup in progress",
            BackupStatus.Completed => "Last backup completed successfully",
            BackupStatus.CompletedWithWarnings => "Completed with warnings",
            BackupStatus.Failed => "Last backup failed",
            BackupStatus.Cancelled => "Last backup was cancelled",
            BackupStatus.NeverRun => "Never executed",
            _ => "Unknown status"
        };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
