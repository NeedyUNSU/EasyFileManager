using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Controls;

namespace EasyFileManager.WPF.Behaviors;

/// <summary>
/// Focuses element when it becomes visible
/// </summary>
public class FocusOnVisibleBehavior : Behavior<TextBox>
{
    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.IsVisibleChanged += OnIsVisibleChanged;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.IsVisibleChanged -= OnIsVisibleChanged;
        base.OnDetaching();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            AssociatedObject.Dispatcher.BeginInvoke(new Action(() =>
            {
                AssociatedObject.Focus();
                AssociatedObject.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
    }
}