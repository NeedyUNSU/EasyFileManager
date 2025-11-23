using EasyFileManager.Core.Models;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EasyFileManager.WPF.Views;

/// <summary>
/// Interaction logic for FileConflictDialog.xaml
/// </summary>
public partial class FileConflictDialog : Window
{
    public FileConflictResolution? Result { get; private set; }

    public FileConflictDialog(FileConflictInfo conflict)
    {
        InitializeComponent();

        var fileName = conflict.FileName;
        MessageTextBlock.Text = $"The file '{fileName}' already exists in the destination folder.";

        // Source info
        SourceNameTextBlock.Text = fileName;
        SourceInfoTextBlock.Text = $"{FormatFileSize(conflict.SourceSize)} • Modified: {conflict.SourceModified:yyyy-MM-dd HH:mm}";

        // Destination info
        DestinationNameTextBlock.Text = fileName;
        DestinationInfoTextBlock.Text = $"{FormatFileSize(conflict.DestinationSize)} • Modified: {conflict.DestinationModified:yyyy-MM-dd HH:mm}";

        // Suggested rename
        var newName = GetUniqueFileName(conflict.DestinationPath);
        RenameTextBox.Text = Path.GetFileName(newName);
    }

    private void ContinueButton_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine(">>> ContinueButton_Click START");
        System.Diagnostics.Debug.WriteLine($">>> SkipRadioButton.IsChecked = {SkipRadioButton.IsChecked}");
        System.Diagnostics.Debug.WriteLine($">>> OverwriteRadioButton.IsChecked = {OverwriteRadioButton.IsChecked}");
        System.Diagnostics.Debug.WriteLine($">>> RenameRadioButton.IsChecked = {RenameRadioButton.IsChecked}");

        var resolution = new FileConflictResolution
        {
            ApplyToAll = ApplyToAllCheckBox.IsChecked == true
        };

        if (SkipRadioButton.IsChecked == true)
        {
            resolution.Action = ConflictAction.Skip;
            System.Diagnostics.Debug.WriteLine(">>> User selected: SKIP");
        }
        else if (OverwriteRadioButton.IsChecked == true)
        {
            resolution.Action = ConflictAction.Overwrite;
            System.Diagnostics.Debug.WriteLine(">>> User selected: OVERWRITE");
        }
        else if (RenameRadioButton.IsChecked == true)
        {
            resolution.Action = ConflictAction.Rename;
            resolution.NewName = RenameTextBox.Text;
            System.Diagnostics.Debug.WriteLine($">>> User selected: RENAME to {resolution.NewName}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine(">>> WARNING: No RadioButton selected! Using Cancel");
            resolution.Action = ConflictAction.Cancel;
        }

        System.Diagnostics.Debug.WriteLine($">>> ApplyToAll: {resolution.ApplyToAll}");
        System.Diagnostics.Debug.WriteLine($">>> Final resolution.Action = {resolution.Action}");

        Result = resolution;

        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Result = new FileConflictResolution { Action = ConflictAction.Cancel };
        Close();
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }

    private string GetUniqueFileName(string filePath)
    {
        if (!File.Exists(filePath))
            return filePath;

        var directory = Path.GetDirectoryName(filePath) ?? string.Empty;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
        var extension = Path.GetExtension(filePath);
        var counter = 1;

        string newPath;
        do
        {
            var newFileName = $"{fileNameWithoutExtension} ({counter}){extension}";
            newPath = Path.Combine(directory, newFileName);
            counter++;
        } while (File.Exists(newPath));

        return newPath;
    }
}
