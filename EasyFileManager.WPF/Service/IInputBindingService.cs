using System.Windows;
using System.Windows.Input;
using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;

namespace EasyFileManager.WPF.Services;

/// <summary>
/// Service for managing application-wide keyboard shortcuts via InputBindings
/// </summary>
public class InputBindingService : IInputBindingService
{
    private readonly IAppLogger<InputBindingService> _logger;
    private readonly ISettingsService _settingsService;
    private readonly Dictionary<string, ICommand> _commandMap;

    public InputBindingService(
        IAppLogger<InputBindingService> logger,
        ISettingsService settingsService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        _commandMap = new Dictionary<string, ICommand>();
    }

    /// <summary>
    /// Registers a command for a specific action
    /// </summary>
    public void RegisterCommand(string commandName, ICommand command)
    {
        _commandMap[commandName] = command;
        _logger.LogDebug("Registered command: {CommandName}", commandName);
    }

    public void RegisterShortcuts(Dictionary<string, KeyboardShortcut> shortcuts)
    {
        try
        {
            _logger.LogInformation("Registering keyboard shortcuts");

            if (Application.Current?.MainWindow == null)
            {
                _logger.LogWarning("MainWindow not available, cannot register shortcuts");
                return;
            }

            ClearShortcuts();

            foreach (var kvp in shortcuts)
            {
                var commandName = kvp.Key;
                var shortcut = kvp.Value;

                if (!_commandMap.TryGetValue(commandName, out var command))
                {
                    _logger.LogWarning("No command registered for: {CommandName}", commandName);
                    continue;
                }

                var (key, modifiers) = ParseShortcut(shortcut.Shortcut);
                if (key == Key.None)
                {
                    _logger.LogWarning("Invalid shortcut for {CommandName}: {Shortcut}",
                        commandName, shortcut.Shortcut);
                    continue;
                }

                var keyBinding = new KeyBinding(command, key, modifiers);
                Application.Current.MainWindow.InputBindings.Add(keyBinding);

                _logger.LogDebug("Registered shortcut: {Shortcut} → {Command}",
                    shortcut.Shortcut, commandName);
            }

            _logger.LogInformation("Registered {Count} keyboard shortcuts", shortcuts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register keyboard shortcuts");
        }
    }

    public void ClearShortcuts()
    {
        try
        {
            if (Application.Current?.MainWindow?.InputBindings != null)
            {
                Application.Current.MainWindow.InputBindings.Clear();
                _logger.LogInformation("Cleared all keyboard shortcuts");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear keyboard shortcuts");
        }
    }

    public void ReloadShortcuts()
    {
        try
        {
            _logger.LogInformation("Reloading keyboard shortcuts from settings");
            var shortcuts = _settingsService.Settings.KeyboardShortcuts;
            RegisterShortcuts(shortcuts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reload keyboard shortcuts");
        }
    }

    private static (Key key, ModifierKeys modifiers) ParseShortcut(string shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut))
            return (Key.None, ModifierKeys.None);

        var parts = shortcut.Split('+');
        if (parts.Length == 0)
            return (Key.None, ModifierKeys.None);

        var modifiers = ModifierKeys.None;
        var keyString = parts[^1].Trim();

        // Parse modifiers
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var modifier = parts[i].Trim();
            switch (modifier.ToLower())
            {
                case "ctrl":
                case "control":
                    modifiers |= ModifierKeys.Control;
                    break;
                case "alt":
                    modifiers |= ModifierKeys.Alt;
                    break;
                case "shift":
                    modifiers |= ModifierKeys.Shift;
                    break;
                case "win":
                case "windows":
                    modifiers |= ModifierKeys.Windows;
                    break;
            }
        }

        // Parse main key
        var key = ParseKey(keyString);
        return (key, modifiers);
    }

    private static Key ParseKey(string keyString)
    {
        // Special key mappings (reverse of ShortcutCaptureDialog.GetKeyString)
        return keyString switch
        {
            "Comma" => Key.OemComma,
            "Period" => Key.OemPeriod,
            "Slash" => Key.OemQuestion,
            "Semicolon" => Key.OemSemicolon,
            "Quote" => Key.OemQuotes,
            "OpenBracket" => Key.OemOpenBrackets,
            "CloseBracket" => Key.OemCloseBrackets,
            "Backslash" => Key.OemBackslash,
            "Minus" => Key.OemMinus,
            "Plus" => Key.OemPlus,
            "Space" => Key.Space,
            _ => Enum.TryParse<Key>(keyString, out var key) ? key : Key.None
        };
    }
}
