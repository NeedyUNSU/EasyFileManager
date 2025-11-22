using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EasyFileManager.WPF.Behaviors;

/// <summary>
/// Behavior for handling GridViewColumnHeader clicks with Command binding
/// </summary>
public class GridViewColumnHeaderClickBehavior : Behavior<ListView>
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(
            nameof(Command),
            typeof(ICommand),
            typeof(GridViewColumnHeaderClickBehavior),
            new PropertyMetadata(null));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject != null)
        {
            AssociatedObject.AddHandler(
                GridViewColumnHeader.ClickEvent,
                new RoutedEventHandler(OnColumnHeaderClick));
        }
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.RemoveHandler(
                GridViewColumnHeader.ClickEvent,
                new RoutedEventHandler(OnColumnHeaderClick));
        }
        base.OnDetaching();
    }

    private void OnColumnHeaderClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is GridViewColumnHeader header &&
            header.Role != GridViewColumnHeaderRole.Padding)
        {
            var columnName = header.Tag as string;

            if (!string.IsNullOrEmpty(columnName) && Command?.CanExecute(columnName) == true)
            {
                Command.Execute(columnName);
            }
        }
    }
}