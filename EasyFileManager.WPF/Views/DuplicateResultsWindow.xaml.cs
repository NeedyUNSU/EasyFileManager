using EasyFileManager.WPF.ViewModels;
using System.Windows;
using System.Windows.Input;

namespace EasyFileManager.WPF.Views;

public partial class DuplicateResultsWindow : Window
{
    public DuplicateResultsWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void FileItem_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is DuplicateFileViewModel fileVm)
        {
            // Set as selected file in preview panel
            if (DataContext is DuplicateResultsViewModel vm)
            {
                vm.SelectedFile = fileVm;

                // Load preview
                await fileVm.LoadPreviewCommand.ExecuteAsync(null);
            }
        }
    }
}