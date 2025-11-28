using EasyFileManager.WPF.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace EasyFileManager.WPF.Views;

/// <summary>
/// Interaction logic for TabBar.xaml
/// </summary>
public partial class TabBar : UserControl
{
    public TabBar()
    {
        InitializeComponent();
    }

    private void Border_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.FrameworkElement element &&
            element.DataContext is TabViewModel tab &&
            DataContext is TabBarViewModel tabBarVm)
        {
            _ = tabBarVm.SwitchToTabAsync(tab);
        }
    }
}