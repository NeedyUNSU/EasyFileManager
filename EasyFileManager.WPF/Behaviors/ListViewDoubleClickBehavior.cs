using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace EasyFileManager.WPF.Behaviors;

/// <summary>
/// Behavior for handling ListView double-click with MVVM Command.
/// Attaches to ListView and executes bound Command with SelectedItem as parameter.
/// </summary>
public class ListViewDoubleClickBehavior : Behavior<ListView>
{
    #region Dependency Properties

    /// <summary>
    /// Command to execute on double-click
    /// </summary>
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.Register(
            nameof(Command),
            typeof(ICommand),
            typeof(ListViewDoubleClickBehavior),
            new PropertyMetadata(null));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>
    /// Optional: Command parameter (if null, uses SelectedItem)
    /// </summary>
    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.Register(
            nameof(CommandParameter),
            typeof(object),
            typeof(ListViewDoubleClickBehavior),
            new PropertyMetadata(null));

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    #endregion

    #region Behavior Lifecycle

    protected override void OnAttached()
    {
        base.OnAttached();

        if (AssociatedObject != null)
        {
            AssociatedObject.MouseDoubleClick += OnMouseDoubleClick;
        }
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.MouseDoubleClick -= OnMouseDoubleClick;
        }

        base.OnDetaching();
    }

    #endregion

    #region Event Handlers

    private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Sprawdź czy kliknięto na item (nie na padding/scrollbar)
        if (e.OriginalSource is FrameworkElement element)
        {
            // Znajdź ListViewItem w drzewie wizualnym
            var listViewItem = FindAncestor<ListViewItem>(element);

            if (listViewItem == null)
            {
                // Kliknięto poza itemem (np. padding)
                return;
            }
        }

        if (Command == null)
            return;

        // Użyj CommandParameter jeśli jest ustawiony, w przeciwnym razie SelectedItem
        var parameter = CommandParameter ?? AssociatedObject.SelectedItem;

        if (Command.CanExecute(parameter))
        {
            Command.Execute(parameter);
        }
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Finds ancestor of specific type in visual tree
    /// </summary>
    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T ancestor)
            {
                return ancestor;
            }
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    #endregion
}