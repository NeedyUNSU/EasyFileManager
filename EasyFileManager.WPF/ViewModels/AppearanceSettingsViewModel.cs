using CommunityToolkit.Mvvm.ComponentModel;
using EasyFileManager.Core.Models;
using MaterialDesignThemes.Wpf;

namespace EasyFileManager.WPF.ViewModels;

/// <summary>
/// ViewModel for Appearance settings section
/// </summary>
public partial class AppearanceSettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _theme;

    [ObservableProperty]
    private string _accentColor;

    [ObservableProperty]
    private string _fontSize;

    [ObservableProperty]
    private bool _showFileExtensions;

    [ObservableProperty]
    private bool _showHiddenFiles;

    [ObservableProperty]
    private bool _showSystemFiles;

    [ObservableProperty]
    private bool _compactView;

    [ObservableProperty]
    private int _iconSize;

    public AppearanceSettingsViewModel(AppearanceSettings settings)
    {
        _theme = settings.Theme;
        _accentColor = settings.AccentColor;
        _fontSize = settings.FontSize;
        _showFileExtensions = settings.ShowFileExtensions;
        _showHiddenFiles = settings.ShowHiddenFiles;
        _showSystemFiles = settings.ShowSystemFiles;
        _compactView = settings.CompactView;
        _iconSize = settings.IconSize;
    }

    public void ApplyChanges(AppearanceSettings target)
    {
        target.Theme = Theme;
        target.AccentColor = AccentColor;
        target.FontSize = FontSize;
        target.ShowFileExtensions = ShowFileExtensions;
        target.ShowHiddenFiles = ShowHiddenFiles;
        target.ShowSystemFiles = ShowSystemFiles;
        target.CompactView = CompactView;
        target.IconSize = IconSize;
    }
}
