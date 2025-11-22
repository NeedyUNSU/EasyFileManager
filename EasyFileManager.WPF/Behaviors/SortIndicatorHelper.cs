using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace EasyFileManager.WPF.Behaviors;

/// <summary>
/// Attached property for showing sort indicator in GridViewColumnHeader
/// </summary>
public static class SortIndicatorHelper
{
    public static readonly DependencyProperty SortColumnProperty =
        DependencyProperty.RegisterAttached(
            "SortColumn",
            typeof(string),
            typeof(SortIndicatorHelper),
            new PropertyMetadata(null, OnSortChanged));

    public static readonly DependencyProperty SortDirectionProperty =
        DependencyProperty.RegisterAttached(
            "SortDirection",
            typeof(ListSortDirection),
            typeof(SortIndicatorHelper),
            new PropertyMetadata(ListSortDirection.Ascending, OnSortChanged));

    public static string GetSortColumn(DependencyObject obj)
        => (string)obj.GetValue(SortColumnProperty);

    public static void SetSortColumn(DependencyObject obj, string value)
        => obj.SetValue(SortColumnProperty, value);

    public static ListSortDirection GetSortDirection(DependencyObject obj)
        => (ListSortDirection)obj.GetValue(SortDirectionProperty);

    public static void SetSortDirection(DependencyObject obj, ListSortDirection value)
        => obj.SetValue(SortDirectionProperty, value);

    private static void OnSortChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ListView listView && listView.View is GridView gridView)
        {
            var sortColumn = GetSortColumn(listView);
            var sortDirection = GetSortDirection(listView);

            foreach (var column in gridView.Columns)
            {
                if (column.Header is GridViewColumnHeader header)
                {
                    var columnName = header.Tag as string;
                    var baseContent = columnName ?? "Unknown";

                    if (columnName == sortColumn)
                    {
                        var indicator = sortDirection == ListSortDirection.Ascending ? " ▲" : " ▼";
                        header.Content = baseContent + indicator;
                    }
                    else
                    {
                        header.Content = baseContent;
                    }
                }
            }
        }
    }
}