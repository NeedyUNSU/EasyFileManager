using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using EasyFileManager.Core.Models;
using EasyFileManager.Core.Services;

namespace EasyFileManager.WPF.Views;

public partial class BackupBrowseDialog : Window
{
    public class BackupInfo
    {
        public string Path { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public int FileCount { get; set; }
        public long Size { get; set; }
        public string SizeFormatted => FormatBytes(Size);

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }
    }

    private readonly BackupJob _job;
    private readonly BackupService _backupService;

    public BackupBrowseDialog(BackupJob job, BackupService backupService)
    {
        InitializeComponent();

        _job = job ?? throw new ArgumentNullException(nameof(job));
        _backupService = backupService ?? throw new ArgumentNullException(nameof(backupService));

        TitleTextBlock.Text = $"Backups for: {job.Name}";

        LoadBackups();
        SetupEventHandlers();
    }

    private void LoadBackups()
    {
        try
        {
            if (!Directory.Exists(_job.DestinationPath))
            {
                MessageBox.Show($"Backup destination not found: {_job.DestinationPath}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Find all backup directories for this job (JobId_timestamp format)
            var jobPrefix = $"{_job.Id}_";
            var backupDirs = Directory.GetDirectories(_job.DestinationPath)
                .Where(d => Path.GetFileName(d).StartsWith(jobPrefix))
                .Select(path =>
                {
                    var dirInfo = new DirectoryInfo(path);
                    var fileCount = Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length;
                    var size = Directory.GetFiles(path, "*", SearchOption.AllDirectories)
                        .Sum(f => new FileInfo(f).Length);

                    return new BackupInfo
                    {
                        Path = path,
                        CreatedDate = dirInfo.CreationTime,
                        FileCount = fileCount,
                        Size = size
                    };
                })
                .OrderByDescending(b => b.CreatedDate)
                .ToList();

            BackupsDataGrid.ItemsSource = backupDirs;

            if (backupDirs.Count == 0)
            {
                MessageBox.Show("No backups found for this job.",
                    "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load backups: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetupEventHandlers()
    {
        OpenFolderButton.Click += OpenFolderButton_Click;
        RestoreButton.Click += RestoreButton_Click;
        CloseButton.Click += (s, e) => Close();
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (BackupsDataGrid.SelectedItem is not BackupInfo backup)
        {
            MessageBox.Show("Please select a backup first.",
                "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = backup.Path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open folder: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (BackupsDataGrid.SelectedItem is not BackupInfo backup)
        {
            MessageBox.Show("Please select a backup to restore.",
                "Information", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Ask for destination
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select restore destination folder",
            CheckFileExists = false,
            FileName = "Folder Selection"
        };

        if (dialog.ShowDialog() != true)
            return;

        var restoreDestination = Path.GetDirectoryName(dialog.FileName);
        if (string.IsNullOrEmpty(restoreDestination))
            return;

        var result = MessageBox.Show(
            $"Restore backup to:\n{restoreDestination}\n\n" +
            $"This will copy {backup.FileCount} files ({backup.SizeFormatted}).\n\n" +
            $"Continue?",
            "Confirm Restore",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            RestoreButton.IsEnabled = false;
            RestoreButton.Content = "RESTORING...";

            await _backupService.RestoreBackupAsync(backup.Path, restoreDestination);

            MessageBox.Show(
                $"Backup restored successfully to:\n{restoreDestination}",
                "Restore Completed",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Restore failed: {ex.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            RestoreButton.IsEnabled = true;
            RestoreButton.Content = "RESTORE";
        }
    }
}
