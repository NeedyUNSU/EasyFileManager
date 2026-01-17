using EasyFileManager.Core.Models;
using System.Collections.Generic;
using System.Windows.Input;

namespace EasyFileManager.Core.Interfaces;

/// <summary>
/// Service for managing application-wide keyboard shortcuts
/// </summary>
public interface IInputBindingService
{
    /// <summary>
    /// Registers a command for a specific action name
    /// </summary>
    void RegisterCommand(string commandName, ICommand command);

    /// <summary>
    /// Registers all keyboard shortcuts from settings
    /// </summary>
    void RegisterShortcuts(Dictionary<string, KeyboardShortcut> shortcuts);

    /// <summary>
    /// Clears all registered shortcuts
    /// </summary>
    void ClearShortcuts();

    /// <summary>
    /// Reloads shortcuts from settings
    /// </summary>
    void ReloadShortcuts();
}
