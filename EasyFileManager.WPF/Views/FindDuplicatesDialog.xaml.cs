using EasyFileManager.Core.Models;
using System;
using System.Windows;
using System.Windows.Controls;

namespace EasyFileManager.WPF.Views;

public partial class FindDuplicatesDialog : Window
{
    public DuplicateScanOptions Options { get; private set; }
    public bool IncludeBothPanels { get; private set; }
    public bool Confirmed { get; private set; }

    private readonly string _currentPath;
    private readonly string? _otherPanelPath;

    public FindDuplicatesDialog(string currentPath, string? otherPanelPath = null)
    {
        InitializeComponent();

        _currentPath = currentPath;
        _otherPanelPath = otherPanelPath;

        CurrentPathTextBlock.Text = currentPath;

        //// Hide "Both panels" option if no other panel
        //if (string.IsNullOrEmpty(otherPanelPath))
        //{
        //    BothPanelsRadio.Visibility = Visibility.Collapsed;
        //}

        Options = new DuplicateScanOptions();
    }

    private void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        // Get compare mode
        if (CompareModeComboBox.SelectedItem is ComboBoxItem compareModeItem)
        {
            Options.CompareMode = compareModeItem.Tag?.ToString() switch
            {
                "NameOnly" => DuplicateCompareMode.NameOnly,
                "NameAndSize" => DuplicateCompareMode.NameAndSize,
                "SizeOnly" => DuplicateCompareMode.SizeOnly,
                "ContentHash" => DuplicateCompareMode.ContentHash,
                _ => DuplicateCompareMode.ContentHash
            };
        }

        // Get hash algorithm
        if (HashAlgorithmComboBox.SelectedItem is ComboBoxItem hashItem)
        {
            Options.HashAlgorithm = hashItem.Tag?.ToString() switch
            {
                "SHA256" => HashAlgorithmType.SHA256,
                _ => HashAlgorithmType.MD5
            };
        }

        // Get search scope
        if (IncludeSubfoldersRadio.IsChecked == true)
        {
            Options.IncludeSubfolders = true;
            IncludeBothPanels = false;
        }
        //else if (BothPanelsRadio.IsChecked == true)
        //{
        //    Options.IncludeSubfolders = true;
        //    IncludeBothPanels = true;
        //}
        else // CurrentFolderRadio
        {
            Options.IncludeSubfolders = false;
            IncludeBothPanels = false;
        }

        // Get options
        Options.IgnoreEmptyFiles = IgnoreEmptyFilesCheckBox.IsChecked == true;

        // Get minimum file size
        if (int.TryParse(MinFileSizeTextBox.Text, out var minSizeKB))
        {
            Options.MinimumFileSize = minSizeKB * 1024; // Convert KB to bytes
        }

        Confirmed = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }
}