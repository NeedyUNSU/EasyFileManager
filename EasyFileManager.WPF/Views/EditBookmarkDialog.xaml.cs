using EasyFileManager.Core.Models;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace EasyFileManager.WPF.Views;

/// <summary>
/// Dialog for editing an existing bookmark
/// </summary>
public partial class EditBookmarkDialog : Window
{
    private readonly Bookmark _bookmark;

    public EditBookmarkDialog(Bookmark bookmark)
    {
        InitializeComponent();

        _bookmark = bookmark;

        // Load current values
        NameTextBox.Text = bookmark.Name;
        PathTextBox.Text = bookmark.Path;

        // Select current icon
        var iconItem = IconComboBox.Items
            .Cast<ComboBoxItem>()
            .FirstOrDefault(item => item.Tag?.ToString() == bookmark.Icon);

        if (iconItem != null)
        {
            IconComboBox.SelectedItem = iconItem;
        }
        else
        {
            IconComboBox.SelectedIndex = 0; // Default to Folder
        }

        NameTextBox.Focus();
        NameTextBox.SelectAll();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var name = NameTextBox.Text.Trim();
        var path = PathTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(
                "Please enter a bookmark name.",
                "Validation",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            NameTextBox.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            MessageBox.Show(
                "Please enter a path.",
                "Validation",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            PathTextBox.Focus();
            return;
        }

        // Update bookmark
        _bookmark.Name = name;
        _bookmark.Path = path;

        if (IconComboBox.SelectedItem is ComboBoxItem selectedIcon)
        {
            _bookmark.Icon = selectedIcon.Tag?.ToString() ?? "Folder";
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
