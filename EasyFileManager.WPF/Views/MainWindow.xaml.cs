using EasyFileManager.Core.Models;
using EasyFileManager.WPF;
using EasyFileManager.WPF.ViewModels;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace EasyFileManager.WPF.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void LeftPanel_GotFocus(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.ActivePanel = vm.LeftPanel;
                System.Diagnostics.Debug.WriteLine(">>> LEFT PANEL ACTIVATED <<<");
            }
        }

        private void RightPanel_GotFocus(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.ActivePanel = vm.RightPanel;
                System.Diagnostics.Debug.WriteLine(">>> RIGHT PANEL ACTIVATED <<<");
            }
        }

        private void ListView_GotFocus(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"ListView_GotFocus: {(sender as ListView)?.Name ?? "unnamed"}");

            if (sender is ListView listView)
            {
                var grid = FindParent<Grid>(listView);
                if (grid != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Parent Grid: {grid.Name}");

                    if (grid.Name == "LeftPanelGrid" && DataContext is MainViewModel vm)
                    {
                        vm.ActivePanel = vm.LeftPanel;
                        System.Diagnostics.Debug.WriteLine(">>> LEFT PANEL ACTIVATED (via ListView) <<<");
                    }
                    else if (grid.Name == "RightPanelGrid" && DataContext is MainViewModel vm2)
                    {
                        vm2.ActivePanel = vm2.RightPanel;
                        System.Diagnostics.Debug.WriteLine(">>> RIGHT PANEL ACTIVATED (via ListView) <<<");
                    }
                }
            }
        }

        private T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);

            if (parent == null)
                return null;

            if (parent is T typedParent)
                return typedParent;

            return FindParent<T>(parent);
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView)
            {
                System.Diagnostics.Debug.WriteLine($"ListView_SelectionChanged: {listView.SelectedItems.Count} items selected");

                if (listView.DataContext is FileExplorerViewModel vm)
                {
                    vm.SelectedItems.Clear();
                    foreach (FileSystemEntry item in listView.SelectedItems)
                    {
                        vm.SelectedItems.Add(item);
                    }

                    System.Diagnostics.Debug.WriteLine($"ViewModel SelectedItems updated: {vm.SelectedItems.Count}");
                }
            }
        }
    }
}