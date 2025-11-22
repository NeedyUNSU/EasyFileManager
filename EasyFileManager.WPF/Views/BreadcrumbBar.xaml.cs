using EasyFileManager.WPF.ViewModels;
using System.Collections.Specialized;
using System.Windows.Controls;

namespace EasyFileManager.WPF.Views;

public partial class BreadcrumbBar : UserControl
{
    public BreadcrumbBar()
    {
        InitializeComponent();

        // Subscribe to DataContext changes
        DataContextChanged += BreadcrumbBar_DataContextChanged;
    }

    private void BreadcrumbBar_DataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        // Unsubscribe from old ViewModel
        if (e.OldValue is FileExplorerViewModel oldVm)
        {
            oldVm.Breadcrumbs.CollectionChanged -= Breadcrumbs_CollectionChanged;
        }

        // Subscribe to new ViewModel
        if (e.NewValue is FileExplorerViewModel newVm)
        {
            newVm.Breadcrumbs.CollectionChanged += Breadcrumbs_CollectionChanged;
            ScrollToEnd();
        }
    }

    private void Breadcrumbs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Scroll to end when breadcrumbs change
        ScrollToEnd();
    }

    private void ScrollToEnd()
    {
        // Delay scroll to allow layout update
        Dispatcher.BeginInvoke(new Action(() =>
        {
            BreadcrumbScroller.ScrollToRightEnd();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }
}