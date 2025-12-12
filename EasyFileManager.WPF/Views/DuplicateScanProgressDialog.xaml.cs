using EasyFileManager.Core.Models;
using System;
using System.Threading;
using System.Windows;

namespace EasyFileManager.WPF.Views;

public partial class DuplicateScanProgressDialog : Window
{
    private CancellationTokenSource? _cancellationTokenSource;
    public CancellationToken CancellationToken => _cancellationTokenSource?.Token ?? CancellationToken.None;
    public bool WasCancelled => _cancellationTokenSource?.IsCancellationRequested ?? false;
    public bool ViewResults { get; private set; }

    public Action<List<DuplicateGroup>>? OnViewResults { get; set; }
    private List<DuplicateGroup>? _duplicateGroups;

    public DuplicateScanProgressDialog()
    {
        InitializeComponent();
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public void UpdateProgress(DuplicateScanProgress progress)
    {
        Dispatcher.Invoke(() =>
        {
            // Status
            StatusTextBlock.Text = progress.Status;
            CurrentFileTextBlock.Text = progress.CurrentFile;

            // Overall progress
            var percentage = progress.TotalFiles > 0
                ? (int)((double)progress.ProcessedFiles / progress.TotalFiles * 100)
                : 0;

            OverallProgressBar.Value = percentage;
            OverallProgressTextBlock.Text = $"{percentage}%";

            // Statistics
            FilesProcessedTextBlock.Text = $"{progress.ProcessedFiles:N0} / {progress.TotalFiles:N0}";

            var processedMB = progress.ProcessedBytes / 1024.0 / 1024.0;
            var totalMB = progress.TotalBytes / 1024.0 / 1024.0;
            DataProcessedTextBlock.Text = $"{processedMB:F1} MB / {totalMB:F1} MB";

            DuplicatesFoundTextBlock.Text = progress.DuplicateGroupsFound.ToString("N0");
        });
    }

    public void SetCompleted(bool success, int duplicateGroupsFound, List<DuplicateGroup>? results = null)
    {
        Dispatcher.Invoke(() =>
        {
            _duplicateGroups = results;

            if (success)
            {
                TitleTextBlock.Text = "Scan completed!";
                StatusTextBlock.Text = duplicateGroupsFound > 0
                    ? $"Found {duplicateGroupsFound} group(s) of duplicate files"
                    : "No duplicates found";

                CancelButton.Visibility = Visibility.Collapsed;
                ViewResultsButton.Visibility = duplicateGroupsFound > 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                // If no duplicates, show Close button instead
                if (duplicateGroupsFound == 0)
                {
                    var closeButton = new System.Windows.Controls.Button
                    {
                        Content = "CLOSE",
                        Style = (Style)FindResource("MaterialDesignRaisedButton")
                    };
                    closeButton.Click += (s, e) => Close();

                    var panel = (System.Windows.Controls.StackPanel)ViewResultsButton.Parent;
                    panel.Children.Add(closeButton);
                }
            }
            else
            {
                TitleTextBlock.Text = "Scan failed";
                StatusTextBlock.Text = "An error occurred during scanning";
                CancelButton.Content = "CLOSE";
            }
        });
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        TitleTextBlock.Text = "Cancelling...";
        CancelButton.IsEnabled = false;
    }

    private void ViewResultsButton_Click(object sender, RoutedEventArgs e)
    {
        ViewResults = true;

        if (OnViewResults != null && _duplicateGroups != null)
        {
            OnViewResults(_duplicateGroups);
        }

        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_cancellationTokenSource?.IsCancellationRequested == false &&
            ViewResultsButton.Visibility == Visibility.Collapsed &&
            CancelButton.Visibility == Visibility.Visible)
        {
            var result = MessageBox.Show(
                "Scan is still in progress. Do you want to cancel it?",
                "Confirm Cancel",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _cancellationTokenSource?.Cancel();
            }
            else
            {
                e.Cancel = true;
                return;
            }
        }

        base.OnClosing(e);
    }
}