using System.Linq;

namespace EasyFileManager.Core.Models;

/// <summary>
/// Keyboard shortcut configuration
/// </summary>
public class KeyboardShortcut
{
    /// <summary>
    /// Unique command identifier (e.g., "NewTab")
    /// </summary>
    public string CommandName { get; set; } = string.Empty;

    /// <summary>
    /// Display name for UI (e.g., "New Tab")
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Shortcut string (e.g., "Ctrl+T")
    /// </summary>
    public string Shortcut { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this command does
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether this shortcut can be customized
    /// </summary>
    public bool IsCustomizable { get; set; } = true;

    public KeyboardShortcut()
    {
    }

    public KeyboardShortcut(string commandName, string displayName, string shortcut, string description, bool isCustomizable = true)
    {
        CommandName = commandName;
        DisplayName = displayName;
        Shortcut = shortcut;
        Description = description;
        IsCustomizable = isCustomizable;
    }

    /// <summary>
    /// Parses shortcut string into modifiers and key
    /// Returns (modifiers, key) tuple
    /// </summary>
    public static (string modifiers, string key) ParseShortcut(string shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut))
            return (string.Empty, string.Empty);

        var parts = shortcut.Split('+');
        if (parts.Length == 1)
            return (string.Empty, parts[0].Trim());

        var key = parts[^1].Trim();
        var modifiers = string.Join("+", parts[..^1].Select(p => p.Trim()));
        return (modifiers, key);
    }

    /// <summary>
    /// Validates if shortcut string is valid format
    /// </summary>
    public static bool IsValidShortcut(string shortcut)
    {
        if (string.IsNullOrWhiteSpace(shortcut))
            return false;

        var parts = shortcut.Split('+');
        if (parts.Length == 0)
            return false;

        // At least one part should be present
        return parts.All(p => !string.IsNullOrWhiteSpace(p));
    }
}
