using EasyFileManager.Core.Models;
using EasyFileManager.WPF.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace EasyFileManager.WPF.Views;

/// <summary>
/// Flyout panel for bookmarks management
/// </summary>
public partial class BookmarksFlyout : UserControl
{
    public BookmarksFlyout()
    {
        InitializeComponent();
    }

    private void ActionsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        // Get the bookmark from Tag
        if (button.Tag is not Bookmark bookmark)
            return;

        // Get the ViewModel
        if (DataContext is not BookmarksViewModel viewModel)
            return;

        // Create context menu
        var contextMenu = new ContextMenu
        {
            PlacementTarget = button,
            Placement = PlacementMode.Bottom
        };

        // Edit
        var editMenuItem = new MenuItem
        {
            Header = "Edit",
            Command = viewModel.EditBookmarkCommand,
            CommandParameter = bookmark
        };
        editMenuItem.Icon = new MaterialDesignThemes.Wpf.PackIcon
        {
            Kind = MaterialDesignThemes.Wpf.PackIconKind.Pencil
        };
        contextMenu.Items.Add(editMenuItem);

        contextMenu.Items.Add(new Separator());

        // Move Up
        var moveUpMenuItem = new MenuItem
        {
            Header = "Move Up",
            Command = viewModel.MoveBookmarkUpCommand,
            CommandParameter = bookmark
        };
        moveUpMenuItem.Icon = new MaterialDesignThemes.Wpf.PackIcon
        {
            Kind = MaterialDesignThemes.Wpf.PackIconKind.ArrowUp
        };
        contextMenu.Items.Add(moveUpMenuItem);

        // Move Down
        var moveDownMenuItem = new MenuItem
        {
            Header = "Move Down",
            Command = viewModel.MoveBookmarkDownCommand,
            CommandParameter = bookmark
        };
        moveDownMenuItem.Icon = new MaterialDesignThemes.Wpf.PackIcon
        {
            Kind = MaterialDesignThemes.Wpf.PackIconKind.ArrowDown
        };
        contextMenu.Items.Add(moveDownMenuItem);

        contextMenu.Items.Add(new Separator());

        // Delete
        var deleteMenuItem = new MenuItem
        {
            Header = "Delete",
            Command = viewModel.DeleteBookmarkCommand,
            CommandParameter = bookmark
        };
        deleteMenuItem.Icon = new MaterialDesignThemes.Wpf.PackIcon
        {
            Kind = MaterialDesignThemes.Wpf.PackIconKind.Delete,
            Foreground = System.Windows.Media.Brushes.Red
        };
        contextMenu.Items.Add(deleteMenuItem);

        // Open the menu
        contextMenu.IsOpen = true;
    }
}