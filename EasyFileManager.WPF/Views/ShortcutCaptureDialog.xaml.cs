using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace EasyFileManager.WPF.Views;

/// <summary>
/// Dialog for capturing keyboard shortcuts
/// </summary>
public partial class ShortcutCaptureDialog : Window
{
    private readonly HashSet<Key> _pressedKeys = new();
    private readonly List<string> _existingShortcuts;
    private string _capturedShortcut = string.Empty;

    public string CapturedShortcut => _capturedShortcut;

    public ShortcutCaptureDialog(List<string> existingShortcuts)
    {
        InitializeComponent();
        _existingShortcuts = existingShortcuts ?? new List<string>();

        // Focus the window to capture keyboard events
        Loaded += (s, e) => Focus();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignore modifier keys alone
        if (key == Key.LeftCtrl || key == Key.RightCtrl ||
            key == Key.LeftAlt || key == Key.RightAlt ||
            key == Key.LeftShift || key == Key.RightShift ||
            key == Key.LWin || key == Key.RWin)
        {
            return;
        }

        _pressedKeys.Add(key);
        UpdateDisplay();
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        // Clear on key up to prepare for next capture
        // (User might want to change combination)
    }

    private void UpdateDisplay()
    {
        var modifiers = new List<string>();

        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
            modifiers.Add("Ctrl");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
            modifiers.Add("Alt");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            modifiers.Add("Shift");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows))
            modifiers.Add("Win");

        // Get the main key (not a modifier)
        var mainKey = _pressedKeys.FirstOrDefault(k =>
            k != Key.LeftCtrl && k != Key.RightCtrl &&
            k != Key.LeftAlt && k != Key.RightAlt &&
            k != Key.LeftShift && k != Key.RightShift &&
            k != Key.LWin && k != Key.RWin);

        if (mainKey != Key.None)
        {
            var keyString = GetKeyString(mainKey);

            if (modifiers.Count > 0)
            {
                _capturedShortcut = string.Join("+", modifiers) + "+" + keyString;
            }
            else
            {
                _capturedShortcut = keyString;
            }

            ShortcutDisplay.Text = _capturedShortcut;
            OkButton.IsEnabled = true;

            // Check for conflicts
            if (_existingShortcuts.Contains(_capturedShortcut))
            {
                ConflictWarning.Text = "⚠ This shortcut is already in use";
                ConflictWarning.Visibility = Visibility.Visible;
            }
            else
            {
                ConflictWarning.Visibility = Visibility.Collapsed;
            }
        }
        else if (modifiers.Count > 0)
        {
            ShortcutDisplay.Text = string.Join("+", modifiers) + "+?";
            OkButton.IsEnabled = false;
            ConflictWarning.Visibility = Visibility.Collapsed;
        }
    }

    private static string GetKeyString(Key key)
    {
        // Special key mappings
        return key switch
        {
            Key.OemComma => "Comma",
            Key.OemPeriod => "Period",
            Key.OemQuestion => "Slash",
            Key.OemSemicolon => "Semicolon",
            Key.OemQuotes => "Quote",
            Key.OemOpenBrackets => "OpenBracket",
            Key.OemCloseBrackets => "CloseBracket",
            Key.OemBackslash => "Backslash",
            Key.OemMinus => "Minus",
            Key.OemPlus => "Plus",
            Key.Space => "Space",
            _ => key.ToString()
        };
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _pressedKeys.Clear();
        _capturedShortcut = string.Empty;
        ShortcutDisplay.Text = "Waiting for input...";
        OkButton.IsEnabled = false;
        ConflictWarning.Visibility = Visibility.Collapsed;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _capturedShortcut = string.Empty;
        DialogResult = false;
        Close();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
