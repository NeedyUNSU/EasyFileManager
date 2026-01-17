using CommunityToolkit.Mvvm.ComponentModel;
using EasyFileManager.Core.Models;

namespace EasyFileManager.WPF.ViewModels;

/// <summary>
/// ViewModel for Tab settings section
/// </summary>
public partial class TabSettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _newTabDefaultPath;

    [ObservableProperty]
    private int _maxTabsPerPanel;

    [ObservableProperty]
    private bool _confirmTabClose;

    [ObservableProperty]
    private bool _duplicateTabSamePath;

    [ObservableProperty]
    private int _autoSaveIntervalSeconds;

    [ObservableProperty]
    private bool _middleClickClosesTab;

    [ObservableProperty]
    private bool _doubleClickCreatesTab;

    public TabSettingsViewModel(TabSettings settings)
    {
        _newTabDefaultPath = settings.NewTabDefaultPath;
        _maxTabsPerPanel = settings.MaxTabsPerPanel;
        _confirmTabClose = settings.ConfirmTabClose;
        _duplicateTabSamePath = settings.DuplicateTabSamePath;
        _autoSaveIntervalSeconds = settings.AutoSaveIntervalSeconds;
        _middleClickClosesTab = settings.MiddleClickClosesTab;
        _doubleClickCreatesTab = settings.DoubleClickCreatesTab;
    }

    public void ApplyChanges(TabSettings target)
    {
        target.NewTabDefaultPath = NewTabDefaultPath;
        target.MaxTabsPerPanel = MaxTabsPerPanel;
        target.ConfirmTabClose = ConfirmTabClose;
        target.DuplicateTabSamePath = DuplicateTabSamePath;
        target.AutoSaveIntervalSeconds = AutoSaveIntervalSeconds;
        target.MiddleClickClosesTab = MiddleClickClosesTab;
        target.DoubleClickCreatesTab = DoubleClickCreatesTab;
    }
}
