using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFileManager.Core.Models;

namespace EasyFileManager.WPF.ViewModels;

/// <summary>
/// ViewModel for Behavior settings section
/// </summary>
public partial class BehaviorSettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private string _defaultLeftPanelPath;

    [ObservableProperty]
    private string _defaultRightPanelPath;

    [ObservableProperty]
    private bool _rememberLastSession;

    [ObservableProperty]
    private bool _restoreWindowPosition;

    [ObservableProperty]
    private bool _minimizeToTray;

    [ObservableProperty]
    private bool _singleInstance;

    public BehaviorSettingsViewModel(BehaviorSettings settings)
    {
        _startWithWindows = settings.StartWithWindows;
        _defaultLeftPanelPath = settings.DefaultLeftPanelPath;
        _defaultRightPanelPath = settings.DefaultRightPanelPath;
        _rememberLastSession = settings.RememberLastSession;
        _restoreWindowPosition = settings.RestoreWindowPosition;
        _minimizeToTray = settings.MinimizeToTray;
        _singleInstance = settings.SingleInstance;
    }

    [RelayCommand]
    private void BrowseLeftPath()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select default folder for left panel",
            CheckFileExists = false,
            FileName = "Folder Selection"
        };

        if (dialog.ShowDialog() == true)
        {
            var folder = System.IO.Path.GetDirectoryName(dialog.FileName);
            if (!string.IsNullOrEmpty(folder))
            {
                DefaultLeftPanelPath = folder;
            }
        }
    }

    [RelayCommand]
    private void BrowseRightPath()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select default folder for right panel",
            CheckFileExists = false,
            FileName = "Folder Selection"
        };

        if (dialog.ShowDialog() == true)
        {
            var folder = System.IO.Path.GetDirectoryName(dialog.FileName);
            if (!string.IsNullOrEmpty(folder))
            {
                DefaultRightPanelPath = folder;
            }
        }
    }

    public void ApplyChanges(BehaviorSettings target)
    {
        target.StartWithWindows = StartWithWindows;
        target.DefaultLeftPanelPath = DefaultLeftPanelPath;
        target.DefaultRightPanelPath = DefaultRightPanelPath;
        target.RememberLastSession = RememberLastSession;
        target.RestoreWindowPosition = RestoreWindowPosition;
        target.MinimizeToTray = MinimizeToTray;
        target.SingleInstance = SingleInstance;
    }
}
