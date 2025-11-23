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
                System.Diagnostics.Debug.WriteLine($">>> ListView_SelectionChanged: {listView.SelectedItems.Count} items selected");

                if (listView.DataContext is FileExplorerViewModel vm)
                {
                    vm.SelectedItems.Clear();
                    foreach (FileSystemEntry item in listView.SelectedItems)
                    {
                        vm.SelectedItems.Add(item);
                    }

                    System.Diagnostics.Debug.WriteLine($">>> ViewModel SelectedItems updated: {vm.SelectedItems.Count}");

                    // Update Preview Panel
                    if (DataContext is MainViewModel mainVm)
                    {
                        System.Diagnostics.Debug.WriteLine($">>> MainViewModel found");

                        if (listView.SelectedItem is FileSystemEntry selectedEntry)
                        {
                            System.Diagnostics.Debug.WriteLine($">>> Selected entry: {selectedEntry.Name} (Type: {selectedEntry.GetType().Name})");

                            var previewVm = mainVm.PreviewPanelViewModel;
                            System.Diagnostics.Debug.WriteLine($">>> PreviewPanelViewModel: {previewVm != null}");

                            var command = previewVm.LoadPreviewCommand;
                            System.Diagnostics.Debug.WriteLine($">>> LoadPreviewCommand exists: {command != null}");
                            System.Diagnostics.Debug.WriteLine($">>> CanExecute: {command?.CanExecute(selectedEntry)}");

                            if (previewVm != null)
                            {
                                System.Diagnostics.Debug.WriteLine($">>> Calling LoadPreviewAsync directly...");
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        await previewVm.LoadPreviewAsync(selectedEntry);
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine($">>> LoadPreviewAsync EXCEPTION: {ex.Message}");
                                    }
                                });
                            }
                        }
                        else if (listView.SelectedItem == null)
                        {
                            System.Diagnostics.Debug.WriteLine($">>> No item selected - clearing preview");
                            var previewVm = mainVm.PreviewPanelViewModel;
                            if (previewVm != null)
                            {
                                _ = previewVm.LoadPreviewCommand.ExecuteAsync(null); // Przekaż null
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($">>> Selected item is not FileSystemEntry: {listView.SelectedItem?.GetType().Name}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($">>> MainViewModel NOT found! DataContext type: {DataContext?.GetType().Name}");
                    }
                }
            }
        }

        private void ListView_GotFocus(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($">>> ListView_MouseDown");

            if (sender is ListView listView)
            {
                // Check if clicked on empty space (not on item)
                var clickedElement = e.OriginalSource as DependencyObject;

                // Find if we clicked on a ListViewItem
                var listViewItem = FindAncestor<ListViewItem>(clickedElement);

                if (listViewItem == null)
                {
                    // Clicked on empty space - deselect all
                    System.Diagnostics.Debug.WriteLine($">>> Clicked on empty space - deselecting");
                    listView.SelectedItem = null;
                    listView.SelectedItems.Clear();

                    // Trigger selection changed manually
                    if (DataContext is MainViewModel mainVm)
                    {
                        var previewVm = mainVm.PreviewPanelViewModel;
                        _ = Task.Run(async () =>
                        {
                            await previewVm.LoadPreviewAsync(null);
                        });
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($">>> Clicked on ListViewItem");
                }
            }
        }

        private T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
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