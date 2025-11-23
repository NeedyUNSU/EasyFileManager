using System.IO;
using System.Windows;

namespace EasyFileManager.WPF.Views;

/// <summary>
/// Dialog for adding a new bookmark
/// </summary>
public partial class AddBookmarkDialog : Window
{
    public string BookmarkName { get; private set; } = string.Empty;

    public AddBookmarkDialog(string path)
    {
        InitializeComponent();

        PathTextBox.Text = path;

        // Auto-suggest name from path
        var dirName = Path.GetFileName(path.TrimEnd('\\', '/'));
        if (string.IsNullOrEmpty(dirName))
        {
            // Root drive
            dirName = path.TrimEnd('\\');
        }

        NameTextBox.Text = dirName;
        NameTextBox.Focus();
        NameTextBox.SelectAll();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        BookmarkName = NameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(BookmarkName))
        {
            MessageBox.Show(
                "Please enter a bookmark name.",
                "Validation",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            NameTextBox.Focus();
            return;
        }

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
