using System.IO;
using System.Windows;

namespace EasyFileManager.WPF.Views;

/// <summary>
/// Dialog for extracting files from archives
/// </summary>
public partial class ExtractArchiveDialog : Window
{
    public string DestinationPath { get; private set; } = string.Empty;
    public bool PreserveStructure => PreserveStructureCheckBox.IsChecked == true;
    public bool Overwrite => OverwriteCheckBox.IsChecked == true;
    public bool OpenFolder => OpenFolderCheckBox.IsChecked == true;
    public bool Confirmed { get; private set; }

    public ExtractArchiveDialog(string archiveName, int itemCount, string? defaultDestination = null)
    {
        InitializeComponent();

        ArchiveNameTextBlock.Text = archiveName;
        ItemCountRun.Text = itemCount == 1 ? "1 item" : $"{itemCount} items";

        // Set default destination
        if (!string.IsNullOrEmpty(defaultDestination))
        {
            DestinationPathTextBox.Text = defaultDestination;
        }
        else
        {
            // Default to Desktop\Extracted
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            DestinationPathTextBox.Text = Path.Combine(desktop, "Extracted");
        }

        DestinationPathTextBox.Focus();
        DestinationPathTextBox.SelectAll();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        // Use native Windows folder picker
        var folderPath = ShowFolderBrowserDialog(DestinationPathTextBox.Text);

        if (!string.IsNullOrEmpty(folderPath))
        {
            DestinationPathTextBox.Text = folderPath;
        }
    }

    private void ExtractButton_Click(object sender, RoutedEventArgs e)
    {
        var destination = DestinationPathTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(destination))
        {
            MessageBox.Show(
                "Please enter a destination folder.",
                "Destination Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            DestinationPathTextBox.Focus();
            return;
        }

        // Validate path
        try
        {
            Path.GetFullPath(destination);
        }
        catch
        {
            MessageBox.Show(
                "Invalid destination path.",
                "Invalid Path",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            DestinationPathTextBox.Focus();
            return;
        }

        // Warn if overwrite is enabled and folder exists
        if (Directory.Exists(destination) && Overwrite)
        {
            var result = MessageBox.Show(
                $"The destination folder already exists:\n{destination}\n\nExisting files will be overwritten. Continue?",
                "Confirm Overwrite",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;
        }

        DestinationPath = destination;
        Confirmed = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }

    // Native Windows folder picker using Win32 API
    private static string? ShowFolderBrowserDialog(string initialFolder)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Destination Folder",
            FileName = "Folder Selection",
            Filter = "Folders|\n",
            CheckFileExists = false,
            CheckPathExists = true,
            ValidateNames = false
        };

        // Try to set initial directory
        if (!string.IsNullOrEmpty(initialFolder) && Directory.Exists(initialFolder))
        {
            dialog.InitialDirectory = initialFolder;
        }

        // Alternative: Use SaveFileDialog trick for folder selection
        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Select Destination Folder",
            FileName = "Select Folder",
            Filter = "Folder|*.folder",
            CheckPathExists = true,
            OverwritePrompt = false
        };

        if (!string.IsNullOrEmpty(initialFolder) && Directory.Exists(initialFolder))
        {
            saveDialog.InitialDirectory = initialFolder;
        }

        if (saveDialog.ShowDialog() == true)
        {
            // Get directory from selected file path
            return Path.GetDirectoryName(saveDialog.FileName);
        }

        return null;
    }
}