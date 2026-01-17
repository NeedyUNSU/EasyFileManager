using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EasyFileManager.Core.Models;

/// <summary>
/// Main application settings container
/// </summary>
public class AppSettings
{
    public AppearanceSettings Appearance { get; set; } = new();
    public BehaviorSettings Behavior { get; set; } = new();
    public TabSettings Tabs { get; set; } = new();
    public FileOperationSettings FileOperations { get; set; } = new();
    public Dictionary<string, KeyboardShortcut> KeyboardShortcuts { get; set; } = new();
    public AdvancedSettings Advanced { get; set; } = new();

    /// <summary>
    /// Creates default settings
    /// </summary>
    public static AppSettings CreateDefault()
    {
        var settings = new AppSettings();
        settings.InitializeDefaultShortcuts();
        return settings;
    }

    private void InitializeDefaultShortcuts()
    {
        KeyboardShortcuts = new Dictionary<string, KeyboardShortcut>
        {
            //["SwitchPanel"] = new KeyboardShortcut("SwitchPanel", "Switch Panel", "Tab", "Switches focus between left and right panel"),
            ["FindDuplicates"] = new KeyboardShortcut("FindDuplicates", "Find Duplicates", "Ctrl+Shift+D", "Opens panel to find duplicates"),
            ["ExtractFromArchive"] = new KeyboardShortcut("ExtractFromArchive", "Extract From Archive", "Ctrl+E", "Opens panel to Extract files From Archive"),
            ["CreateArchive"] = new KeyboardShortcut("CreateArchive", "Create Archive", "Alt+A", "Opens panel to Create Archive"),
            ["CopyToTargetPanel"] = new KeyboardShortcut("CopyToTargetPanel", "Copy To Target Panel", "F5", "Copy files To Target Panel"),
            ["MoveToTargetPanel"] = new KeyboardShortcut("MoveToTargetPanel", "Move To Target Panel", "F6", "Move files To Target Panel"),
            ["DuplicateTab"] = new KeyboardShortcut("DuplicateTab", "Duplicate Tab", "Ctrl+Shift+T", "Duplicate current tab"),
            ["CloseOtherTab"] = new KeyboardShortcut("CloseOtherTab", "Close Other Tabs", "Ctrl+Shift+W", "Close Other Tabs in tab bar"),
            ["CloseTabToRight"] = new KeyboardShortcut("CloseTabToRight", "Close Tabs", "Ctrl+Shift+Q", "Close All Tabs To The Right"),
            ["PinTab"] = new KeyboardShortcut("PinTab", "Pin Tab", "Ctrl+Shift+P", "Pin current tab"),
            ["NewTab"] = new KeyboardShortcut("NewTab", "New Tab", "Ctrl+T", "Opens a new tab in current panel"),
            ["CloseTab"] = new KeyboardShortcut("CloseTab", "Close Tab", "Ctrl+W", "Closes the active tab"),
            ["NextTab"] = new KeyboardShortcut("NextTab", "Next Tab", "Ctrl+Tab", "Goes to parent directory"),
            ["PreviousTab"] = new KeyboardShortcut("PreviousTab", "Previous Tab", "Ctrl+Shift+Tab", "Goes to parent directory"),
            ["BookmarksPanel"] = new KeyboardShortcut("BookmarksPanel", "Bookmarks Panel", "Ctrl+D", "Switches Bookmarks panel to shown or hide"),
            ["PreviewPanel"] = new KeyboardShortcut("PreviewPanel", "Preview Panel", "F3", "Switches preview panel to shown or hide"),
            ["Refresh"] = new KeyboardShortcut("Refresh", "Refresh", "Ctrl+R", "Refreshes current directory"),
            ["Search"] = new KeyboardShortcut("Search", "Search", "Ctrl+F", "Opens search dialog"),
            ["HideSearch"] = new KeyboardShortcut("HideSearch", "Hide Search", "Escape", "Clears search dialog and deny filter"),
            ["Copy"] = new KeyboardShortcut("Copy", "Copy", "Ctrl+C", "Copies selected items"),
            ["Cut"] = new KeyboardShortcut("Cut", "Cut", "Ctrl+X", "Cuts selected items"),
            ["Paste"] = new KeyboardShortcut("Paste", "Paste", "Ctrl+V", "Pastes items from clipboard"),
            ["Delete"] = new KeyboardShortcut("Delete", "Delete", "Delete", "Deletes selected items"),
            ["Rename"] = new KeyboardShortcut("Rename", "Rename", "F2", "Renames selected item"),
            ["Properties"] = new KeyboardShortcut("Properties", "Properties", "Alt+Enter", "Shows item properties"),
            ["SelectAll"] = new KeyboardShortcut("SelectAll", "Select All", "Ctrl+A", "Selects all items"),
            ["BackupManager"] = new KeyboardShortcut("BackupManager", "Backup Manager", "Ctrl+B", "Opens Backup Manager"),
            ["Settings"] = new KeyboardShortcut("Settings", "Settings", "Ctrl+Comma", "Opens Settings window"),
            ["Quit"] = new KeyboardShortcut("Quit", "Quit", "Ctrl+Q", "Exits the application")
        };
    }
}
