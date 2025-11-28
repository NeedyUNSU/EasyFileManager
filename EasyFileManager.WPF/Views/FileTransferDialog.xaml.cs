using EasyFileManager.Core.Models;
using System;
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
using System.Windows.Shapes;

namespace EasyFileManager.WPF.Views;

/// <summary>
/// Interaction logic for FileTransferDialog.xaml
/// </summary>
public partial class FileTransferDialog : Window
{
    private CancellationTokenSource? _cancellationTokenSource;
    public CancellationToken CancellationToken => _cancellationTokenSource?.Token ?? CancellationToken.None;
    public bool WasCancelled => _cancellationTokenSource?.IsCancellationRequested ?? false;

    public FileTransferDialog(string operationType)
    {
        InitializeComponent();
        TitleTextBlock.Text = $"{operationType} files...";
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public void UpdateProgress(FileTransferProgress progress)
    {
        Dispatcher.Invoke(() =>
        {
            CurrentFileTextBlock.Text = progress.CurrentFile;
            CurrentFileProgressBar.Value = progress.CurrentFileProgress;
            CurrentFileProgressTextBlock.Text = $"{progress.CurrentFileProgress}%";

            OverallProgressBar.Value = progress.OverallProgress;
            OverallProgressTextBlock.Text = $"{progress.OverallProgress}%";

            StatsTextBlock.Text = $"{progress.ProcessedFiles} / {progress.TotalFiles} files";
        });
    }

    public void SetCompleted(bool success)
    {
        System.Diagnostics.Debug.WriteLine($">>> SetCompleted called. Success: {success}");
        System.Diagnostics.Debug.WriteLine($">>> Current thread ID: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
        System.Diagnostics.Debug.WriteLine($">>> Dispatcher thread ID: {Dispatcher.Thread.ManagedThreadId}");

        try
        {
            System.Diagnostics.Debug.WriteLine(">>> About to call Dispatcher.Invoke...");
            Dispatcher.Invoke(() =>
            {
                System.Diagnostics.Debug.WriteLine(">>> Inside Dispatcher.Invoke");
                System.Diagnostics.Debug.WriteLine($">>> Setting TitleTextBlock.Text...");
                TitleTextBlock.Text = success ? "Transfer completed!" : "Transfer failed";
                System.Diagnostics.Debug.WriteLine($">>> Setting CancelButton.Visibility...");
                CancelButton.Visibility = Visibility.Collapsed;
                System.Diagnostics.Debug.WriteLine($">>> Setting CloseButton.Visibility...");
                CloseButton.Visibility = Visibility.Visible;
                System.Diagnostics.Debug.WriteLine(">>> Dispatcher.Invoke completed");
            });
            System.Diagnostics.Debug.WriteLine(">>> SetCompleted succeeded");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($">>> EXCEPTION in SetCompleted: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($">>> Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        TitleTextBlock.Text = "Cancelling...";
        CancelButton.IsEnabled = false;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_cancellationTokenSource?.IsCancellationRequested == false &&
            CloseButton.Visibility == Visibility.Collapsed)
        {
            var result = MessageBox.Show(
                "Transfer is still in progress. Do you want to cancel it?",
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
