using Microsoft.Xaml.Behaviors;
using System.Windows;
using EasyFileManager.WPF.ViewModels;

namespace EasyFileManager.WPF.Behaviors;

/// <summary>
/// Behavior for tracking active panel through focus events - MVVM compliant
/// </summary>
public class ActivePanelBehavior : Behavior<FrameworkElement>
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(FileExplorerViewModel),
            typeof(ActivePanelBehavior),
            new PropertyMetadata(null));

    public FileExplorerViewModel? ViewModel
    {
        get => (FileExplorerViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    public static readonly DependencyProperty MainViewModelProperty =
        DependencyProperty.Register(
            nameof(MainViewModel),
            typeof(MainViewModel),
            typeof(ActivePanelBehavior),
            new PropertyMetadata(null));

    public MainViewModel? MainViewModel
    {
        get => (MainViewModel?)GetValue(MainViewModelProperty);
        set => SetValue(MainViewModelProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();
        AssociatedObject.GotFocus += OnGotFocus;
    }

    protected override void OnDetaching()
    {
        AssociatedObject.GotFocus -= OnGotFocus;
        base.OnDetaching();
    }

    private void OnGotFocus(object sender, RoutedEventArgs e)
    {
        if (MainViewModel != null && ViewModel != null)
        {
            MainViewModel.ActivePanel = ViewModel;
            System.Diagnostics.Debug.WriteLine($"Active panel changed to: {(ViewModel == MainViewModel.LeftPanel ? "LEFT" : "RIGHT")}");
        }
    }
}