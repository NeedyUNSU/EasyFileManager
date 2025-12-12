using EasyFileManager.Core.Models;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace EasyFileManager.WPF.Views;

public partial class CreateArchiveDialog : Window
{
    public string ArchivePath { get; private set; } = string.Empty;
    public CompressionLevel CompressionLevel { get; private set; } = CompressionLevel.Default;
    public string? Password { get; private set; }
    public bool Confirmed { get; private set; }

    private readonly int _itemCount;
    private readonly string _suggestedName;

    public string ItemCountText => $"Adding {_itemCount} item(s) to archive";

    public CreateArchiveDialog(int itemCount, string suggestedName)
    {
        InitializeComponent();

        _itemCount = itemCount;
        _suggestedName = suggestedName;

        DataContext = this;

        // Set suggested archive name
        ArchiveNameTextBox.Text = _suggestedName + ".zip";
        ArchiveNameTextBox.Focus();
        ArchiveNameTextBox.SelectAll();
    }

    private void CreateButton_Click(object sender, RoutedEventArgs e)
    {
        var archiveName = ArchiveNameTextBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(archiveName))
        {
            MessageBox.Show(
                "Please enter an archive name.",
                "Invalid Name",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        // Ensure .zip extension
        if (!archiveName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            archiveName += ".zip";
        }

        // Get compression level from combobox
        if (CompressionLevelComboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            CompressionLevel = selectedItem.Tag?.ToString() switch
            {
                "Store" => CompressionLevel.Store,
                "Fastest" => CompressionLevel.Fastest,
                "Default" => CompressionLevel.Default,
                "Best" => CompressionLevel.Best,
                _ => CompressionLevel.Default
            };
        }

        // Get password (if implemented)
        // Password = PasswordTextBox.Text;

        ArchivePath = archiveName;
        Confirmed = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }
}