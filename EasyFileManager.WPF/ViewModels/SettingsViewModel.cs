using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;

namespace EasyFileManager.WPF.ViewModels;

/// <summary>
/// Main ViewModel for Settings window
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly IInputBindingService _inputBindingService;
    private readonly IAppLogger<SettingsViewModel> _logger;

    // Working copy of settings (not saved until user clicks Save)
    private AppSettings _workingSettings;

    [ObservableProperty]
    private AppearanceSettingsViewModel _appearance;

    [ObservableProperty]
    private BehaviorSettingsViewModel _behavior;

    [ObservableProperty]
    private TabSettingsViewModel _tabs;

    [ObservableProperty]
    private FileOperationSettingsViewModel _fileOperations;

    [ObservableProperty]
    private KeyboardShortcutsViewModel _keyboardShortcuts;

    [ObservableProperty]
    private AdvancedSettingsViewModel _advanced;

    [ObservableProperty]
    private bool _hasUnsavedChanges = false;

    public SettingsViewModel(
        ISettingsService settingsService,
        IThemeService themeService,
        IInputBindingService inputBindingService,
        IAppLogger<SettingsViewModel> logger)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
        _inputBindingService = inputBindingService ?? throw new ArgumentNullException(nameof(inputBindingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Create working copy
        _workingSettings = CloneSettings(_settingsService.Settings);

        // Initialize sub-ViewModels with working copy
        _appearance = new AppearanceSettingsViewModel(_workingSettings.Appearance);
        _behavior = new BehaviorSettingsViewModel(_workingSettings.Behavior);
        _tabs = new TabSettingsViewModel(_workingSettings.Tabs);
        _fileOperations = new FileOperationSettingsViewModel(_workingSettings.FileOperations);
        _keyboardShortcuts = new KeyboardShortcutsViewModel(_workingSettings.KeyboardShortcuts, logger);
        _advanced = new AdvancedSettingsViewModel(_workingSettings.Advanced);

        // Subscribe to changes in sub-ViewModels
        _appearance.PropertyChanged += (s, e) => HasUnsavedChanges = true;
        _behavior.PropertyChanged += (s, e) => HasUnsavedChanges = true;
        _tabs.PropertyChanged += (s, e) => HasUnsavedChanges = true;
        _fileOperations.PropertyChanged += (s, e) => HasUnsavedChanges = true;
        _keyboardShortcuts.PropertyChanged += (s, e) => HasUnsavedChanges = true;
        _advanced.PropertyChanged += (s, e) => HasUnsavedChanges = true;

        _logger.LogInformation("SettingsViewModel initialized");
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        try
        {
            _logger.LogInformation("Saving settings");

            // Apply changes from sub-ViewModels to working settings
            _appearance.ApplyChanges(_workingSettings.Appearance);
            _behavior.ApplyChanges(_workingSettings.Behavior);
            _tabs.ApplyChanges(_workingSettings.Tabs);
            _fileOperations.ApplyChanges(_workingSettings.FileOperations);
            _keyboardShortcuts.ApplyChanges(_workingSettings.KeyboardShortcuts);
            _advanced.ApplyChanges(_workingSettings.Advanced);

            // Save to service
            await _settingsService.SaveAsync(_workingSettings);

            // Apply theme changes immediately
            _themeService.ApplyAppearanceSettings(_workingSettings.Appearance);

            // Reload keyboard shortcuts immediately
            _inputBindingService.ReloadShortcuts();

            HasUnsavedChanges = false;
            _logger.LogInformation("Settings saved successfully");

            MessageBox.Show(
                "Settings saved successfully!\n\nKeyboard shortcuts have been updated.",
                "Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            MessageBox.Show(
                $"Failed to save settings: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _logger.LogInformation("Settings cancelled");
        // Working copy is discarded, window will close
    }

    [RelayCommand]
    private async Task ResetToDefaultsAsync()
    {
        var result = MessageBox.Show(
            "Are you sure you want to reset all settings to defaults?\n\nThis action cannot be undone.",
            "Reset Settings",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            _logger.LogInformation("Resetting settings to defaults");

            await _settingsService.ResetToDefaultsAsync();

            // Reload from service
            _workingSettings = CloneSettings(_settingsService.Settings);

            // Update sub-ViewModels
            Appearance = new AppearanceSettingsViewModel(_workingSettings.Appearance);
            Behavior = new BehaviorSettingsViewModel(_workingSettings.Behavior);
            Tabs = new TabSettingsViewModel(_workingSettings.Tabs);
            FileOperations = new FileOperationSettingsViewModel(_workingSettings.FileOperations);
            KeyboardShortcuts = new KeyboardShortcutsViewModel(_workingSettings.KeyboardShortcuts, _logger);
            Advanced = new AdvancedSettingsViewModel(_workingSettings.Advanced);

            HasUnsavedChanges = false;

            MessageBox.Show(
                "Settings have been reset to defaults.",
                "Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset settings");
            MessageBox.Show(
                $"Failed to reset settings: {ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static AppSettings CloneSettings(AppSettings source)
    {
        // Deep clone using JSON serialization
        var json = System.Text.Json.JsonSerializer.Serialize(source);
        return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json) ?? AppSettings.CreateDefault();
    }
}
