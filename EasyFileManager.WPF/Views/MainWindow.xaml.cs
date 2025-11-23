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

        private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Close flyout when clicking on overlay
            if (DataContext is MainViewModel vm)
            {
                vm.IsBookmarksFlyoutOpen = false;
            }
        }
    }
}