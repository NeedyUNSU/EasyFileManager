using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using EasyFileManager.Core.Models;

namespace EasyFileManager.WPF.Views;

public partial class BackupJobDialog : Window
{
    private BackupJob _job;
    private readonly List<string> _sourcePaths = new();

    public bool Confirmed { get; private set; }
    public BackupJob Job => _job;

    public BackupJobDialog(BackupJob? existingJob = null)
    {
        InitializeComponent();

        _job = existingJob ?? new BackupJob
        {
            Name = "New Backup Job",
            Description = "",
            Schedule = new BackupSchedule
            {
                Frequency = BackupFrequency.Manual,
                DailyTime = new TimeSpan(2, 0, 0),
                WeeklyDay = DayOfWeek.Sunday,
                WeeklyTime = new TimeSpan(2, 0, 0),
                MonthlyDay = 1,
                MonthlyTime = new TimeSpan(2, 0, 0),
                IntervalValue = 1
            },
            Options = new BackupOptions
            {
                EnableRetention = true,
                RetentionDays = 30,
                MaxBackupCount = 10,
                IncludeHiddenFiles = false,
                IncludeSystemFiles = false,
                VerifyAfterBackup = true,
                PreserveAttributes = true,
                PreserveTimestamps = true
            }
        };

        LoadJobData();
        SetupEventHandlers();
    }

    private void LoadJobData()
    {
        // Basic info
        NameTextBox.Text = _job.Name;
        DescriptionTextBox.Text = _job.Description;

        // Sources
        _sourcePaths.Clear();
        _sourcePaths.AddRange(_job.SourcePaths);
        RefreshSourcesList();

        // Destination
        DestinationTextBox.Text = _job.DestinationPath;

        // Schedule
        var scheduleIndex = _job.Schedule.Frequency switch
        {
            BackupFrequency.Manual => 0,
            BackupFrequency.EveryMinutes => 1,
            BackupFrequency.EveryHours => 2,
            BackupFrequency.Daily => 3,
            BackupFrequency.Weekly => 4,
            BackupFrequency.Monthly => 5,
            _ => 0
        };
        FrequencyComboBox.SelectedIndex = scheduleIndex;

        IntervalValueTextBox.Text = _job.Schedule.IntervalValue.ToString();

        // Convert TimeSpan to DateTime for TimePicker
        DailyTimePicker.SelectedTime = DateTime.Today.Add(_job.Schedule.DailyTime);
        WeeklyDayComboBox.SelectedIndex = (int)_job.Schedule.WeeklyDay;
        WeeklyTimePicker.SelectedTime = DateTime.Today.Add(_job.Schedule.WeeklyTime);
        MonthlyDayTextBox.Text = _job.Schedule.MonthlyDay.ToString();
        MonthlyTimePicker.SelectedTime = DateTime.Today.Add(_job.Schedule.MonthlyTime);

        // Options
        EnableRetentionCheckBox.IsChecked = _job.Options.EnableRetention;
        RetentionDaysTextBox.Text = _job.Options.RetentionDays.ToString();
        MaxBackupCountTextBox.Text = _job.Options.MaxBackupCount.ToString();
        IncludeHiddenFilesCheckBox.IsChecked = _job.Options.IncludeHiddenFiles;
        IncludeSystemFilesCheckBox.IsChecked = _job.Options.IncludeSystemFiles;
        VerifyAfterBackupCheckBox.IsChecked = _job.Options.VerifyAfterBackup;
        PreserveAttributesCheckBox.IsChecked = _job.Options.PreserveAttributes;
    }

    private void SetupEventHandlers()
    {
        AddSourceButton.Click += AddSourceButton_Click;
        RemoveSourceButton.Click += RemoveSourceButton_Click;
        BrowseDestinationButton.Click += BrowseDestinationButton_Click;
        FrequencyComboBox.SelectionChanged += FrequencyComboBox_SelectionChanged;
        SaveButton.Click += SaveButton_Click;
        CancelButton.Click += CancelButton_Click;
    }

    private void AddSourceButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select folder to backup",
            CheckFileExists = false,
            FileName = "Folder Selection"
        };

        if (dialog.ShowDialog() == true)
        {
            var folderPath = System.IO.Path.GetDirectoryName(dialog.FileName);
            if (!string.IsNullOrEmpty(folderPath) && !_sourcePaths.Contains(folderPath))
            {
                _sourcePaths.Add(folderPath);
                RefreshSourcesList();
            }
        }
    }

    private void RemoveSourceButton_Click(object sender, RoutedEventArgs e)
    {
        if (SourcesListBox.SelectedItem is string selectedPath)
        {
            _sourcePaths.Remove(selectedPath);
            RefreshSourcesList();
        }
    }

    private void BrowseDestinationButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select backup destination folder",
            CheckFileExists = false,
            FileName = "Folder Selection"
        };

        if (dialog.ShowDialog() == true)
        {
            var folderPath = System.IO.Path.GetDirectoryName(dialog.FileName);
            if (!string.IsNullOrEmpty(folderPath))
            {
                DestinationTextBox.Text = folderPath;
            }
        }
    }

    private void FrequencyComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FrequencyComboBox.SelectedItem is not ComboBoxItem item)
            return;

        // Hide all schedule panels
        IntervalPanel.Visibility = Visibility.Collapsed;
        DailyTimePicker.Visibility = Visibility.Collapsed;
        WeeklyPanel.Visibility = Visibility.Collapsed;
        MonthlyPanel.Visibility = Visibility.Collapsed;

        switch (item.Tag as string)
        {
            case "EveryMinutes":
                IntervalPanel.Visibility = Visibility.Visible;
                IntervalUnitTextBlock.Text = "minute(s)";
                break;

            case "EveryHours":
                IntervalPanel.Visibility = Visibility.Visible;
                IntervalUnitTextBlock.Text = "hour(s)";
                break;

            case "Daily":
                DailyTimePicker.Visibility = Visibility.Visible;
                break;

            case "Weekly":
                WeeklyPanel.Visibility = Visibility.Visible;
                break;

            case "Monthly":
                MonthlyPanel.Visibility = Visibility.Visible;
                break;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        // Validate
        if (string.IsNullOrWhiteSpace(NameTextBox.Text))
        {
            MessageBox.Show("Please enter a job name.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            NameTextBox.Focus();
            return;
        }

        if (_sourcePaths.Count == 0)
        {
            MessageBox.Show("Please add at least one source folder.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(DestinationTextBox.Text))
        {
            MessageBox.Show("Please select a destination folder.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            DestinationTextBox.Focus();
            return;
        }

        // Save basic info
        _job.Name = NameTextBox.Text;
        _job.Description = DescriptionTextBox.Text;

        // Save sources and destination
        _job.SourcePaths = _sourcePaths.ToList();
        _job.DestinationPath = DestinationTextBox.Text;

        // Save schedule
        if (FrequencyComboBox.SelectedItem is ComboBoxItem freqItem)
        {
            unsafe
            {
                _job.Schedule.Frequency = (freqItem.Tag as string) switch
                {
                    "Manual" => BackupFrequency.Manual,
                    "EveryMinutes" => BackupFrequency.EveryMinutes,
                    "EveryHours" => BackupFrequency.EveryHours,
                    "Daily" => BackupFrequency.Daily,
                    "Weekly" => BackupFrequency.Weekly,
                    "Monthly" => BackupFrequency.Monthly,
                    _ => BackupFrequency.Manual
                };
            }
        }

        if (int.TryParse(IntervalValueTextBox.Text, out int intervalValue))
            _job.Schedule.IntervalValue = intervalValue;

        // FIX: Convert DateTime to TimeSpan (TimeOfDay)
        if (DailyTimePicker.SelectedTime.HasValue)
            _job.Schedule.DailyTime = DailyTimePicker.SelectedTime.Value.TimeOfDay;

        if (WeeklyDayComboBox.SelectedItem is ComboBoxItem weekdayItem)
            _job.Schedule.WeeklyDay = Enum.Parse<DayOfWeek>(weekdayItem.Tag as string ?? "Sunday");

        if (WeeklyTimePicker.SelectedTime.HasValue)
            _job.Schedule.WeeklyTime = WeeklyTimePicker.SelectedTime.Value.TimeOfDay;

        if (int.TryParse(MonthlyDayTextBox.Text, out int monthlyDay))
            _job.Schedule.MonthlyDay = Math.Clamp(monthlyDay, 1, 31);

        if (MonthlyTimePicker.SelectedTime.HasValue)
            _job.Schedule.MonthlyTime = MonthlyTimePicker.SelectedTime.Value.TimeOfDay;

        // Save options
        _job.Options.EnableRetention = EnableRetentionCheckBox.IsChecked == true;

        if (int.TryParse(RetentionDaysTextBox.Text, out int retentionDays))
            _job.Options.RetentionDays = retentionDays;

        if (int.TryParse(MaxBackupCountTextBox.Text, out int maxBackupCount))
            _job.Options.MaxBackupCount = maxBackupCount;

        _job.Options.IncludeHiddenFiles = IncludeHiddenFilesCheckBox.IsChecked == true;
        _job.Options.IncludeSystemFiles = IncludeSystemFilesCheckBox.IsChecked == true;
        _job.Options.VerifyAfterBackup = VerifyAfterBackupCheckBox.IsChecked == true;
        _job.Options.PreserveAttributes = PreserveAttributesCheckBox.IsChecked == true;
        _job.Options.PreserveTimestamps = PreserveAttributesCheckBox.IsChecked == true;

        Confirmed = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }

    private void RefreshSourcesList()
    {
        SourcesListBox.Items.Clear();
        foreach (var path in _sourcePaths)
        {
            SourcesListBox.Items.Add(path);
        }
    }
}
