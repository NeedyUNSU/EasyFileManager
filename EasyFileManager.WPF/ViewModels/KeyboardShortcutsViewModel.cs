using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;
using EasyFileManager.WPF.Views;

namespace EasyFileManager.WPF.ViewModels;

/// <summary>
/// ViewModel for Keyboard Shortcuts settings section
/// </summary>
public partial class KeyboardShortcutsViewModel : ObservableObject
{
    private readonly IAppLogger<SettingsViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<KeyboardShortcutViewModel> _shortcuts = new();

    [ObservableProperty]
    private KeyboardShortcutViewModel? _selectedShortcut;

    public KeyboardShortcutsViewModel(Dictionary<string, KeyboardShortcut> shortcuts, IAppLogger<SettingsViewModel> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Load shortcuts into ObservableCollection
        foreach (var kvp in shortcuts.OrderBy(x => x.Value.DisplayName))
        {
            Shortcuts.Add(new KeyboardShortcutViewModel(kvp.Value));
        }
    }

    [RelayCommand]
    private void EditShortcut()
    {
        if (SelectedShortcut == null)
        {
            MessageBox.Show("Please select a shortcut to edit.", "Edit Shortcut", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!SelectedShortcut.IsCustomizable)
        {
            MessageBox.Show("This shortcut cannot be customized.", "Edit Shortcut", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            // Get list of existing shortcuts (excluding current one)
            var existingShortcuts = Shortcuts
                .Where(s => s.CommandName != SelectedShortcut.CommandName)
                .Select(s => s.Shortcut)
                .ToList();

            var dialog = new ShortcutCaptureDialog(existingShortcuts)
            {
                Owner = Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault()
            };

            if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.CapturedShortcut))
            {
                SelectedShortcut.Shortcut = dialog.CapturedShortcut;
                _logger.LogInformation("Shortcut for {Command} changed to: {Shortcut}",
                    SelectedShortcut.CommandName, dialog.CapturedShortcut);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to edit shortcut");
            MessageBox.Show($"Failed to edit shortcut: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void ResetShortcut()
    {
        if (SelectedShortcut == null)
        {
            MessageBox.Show("Please select a shortcut to reset.", "Reset Shortcut", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"Reset shortcut for '{SelectedShortcut.DisplayName}' to default?",
            "Reset Shortcut",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            // Get default shortcut from AppSettings
            var defaultSettings = AppSettings.CreateDefault();
            if (defaultSettings.KeyboardShortcuts.TryGetValue(SelectedShortcut.CommandName, out var defaultShortcut))
            {
                SelectedShortcut.Shortcut = defaultShortcut.Shortcut;
                _logger.LogInformation("Reset shortcut {Command} to default: {Shortcut}",
                    SelectedShortcut.CommandName, defaultShortcut.Shortcut);
            }
        }
    }

    [RelayCommand]
    private void ResetAllShortcuts()
    {
        var result = MessageBox.Show(
            "Reset all keyboard shortcuts to defaults?",
            "Reset All Shortcuts",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            var defaultSettings = AppSettings.CreateDefault();
            foreach (var shortcutVm in Shortcuts)
            {
                if (defaultSettings.KeyboardShortcuts.TryGetValue(shortcutVm.CommandName, out var defaultShortcut))
                {
                    shortcutVm.Shortcut = defaultShortcut.Shortcut;
                }
            }
            _logger.LogInformation("Reset all shortcuts to defaults");
        }
    }

    public void ApplyChanges(Dictionary<string, KeyboardShortcut> target)
    {
        target.Clear();
        foreach (var shortcutVm in Shortcuts)
        {
            target[shortcutVm.CommandName] = new KeyboardShortcut(
                shortcutVm.CommandName,
                shortcutVm.DisplayName,
                shortcutVm.Shortcut,
                shortcutVm.Description,
                shortcutVm.IsCustomizable
            );
        }
    }
}

/// <summary>
/// ViewModel for single keyboard shortcut
/// </summary>
public partial class KeyboardShortcutViewModel : ObservableObject
{
    [ObservableProperty]
    private string _commandName;

    [ObservableProperty]
    private string _displayName;

    [ObservableProperty]
    private string _shortcut;

    [ObservableProperty]
    private string _description;

    [ObservableProperty]
    private bool _isCustomizable;

    public KeyboardShortcutViewModel(KeyboardShortcut shortcut)
    {
        _commandName = shortcut.CommandName;
        _displayName = shortcut.DisplayName;
        _shortcut = shortcut.Shortcut;
        _description = shortcut.Description;
        _isCustomizable = shortcut.IsCustomizable;
    }
}
