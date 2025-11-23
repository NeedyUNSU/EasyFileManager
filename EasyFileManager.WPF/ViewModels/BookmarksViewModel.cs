using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;
using EasyFileManager.WPF.Views;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace EasyFileManager.WPF.ViewModels;

/// <summary>
/// ViewModel for Bookmarks management
/// Handles CRUD operations and navigation
/// </summary>
public partial class BookmarksViewModel : ViewModelBase
{
    private readonly IBookmarkService _bookmarkService;
    private readonly IAppLogger<BookmarksViewModel> _logger;
    private readonly MainViewModel _mainViewModel;

    [ObservableProperty]
    private ObservableCollection<Bookmark> _bookmarks = new();

    [ObservableProperty]
    private Bookmark? _selectedBookmark;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public BookmarksViewModel(
        IBookmarkService bookmarkService,
        IAppLogger<BookmarksViewModel> logger,
        MainViewModel mainViewModel)
    {
        _bookmarkService = bookmarkService ?? throw new ArgumentNullException(nameof(bookmarkService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _mainViewModel = mainViewModel ?? throw new ArgumentNullException(nameof(mainViewModel));

        _ = LoadBookmarksAsync();
    }

    [RelayCommand]
    private async Task LoadBookmarksAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Loading bookmarks...";

            var bookmarks = await _bookmarkService.LoadBookmarksAsync();

            Bookmarks.Clear();
            foreach (var bookmark in bookmarks)
            {
                Bookmarks.Add(bookmark);
            }

            StatusMessage = $"Loaded {Bookmarks.Count} bookmarks";
            _logger.LogDebug("Loaded {Count} bookmarks", Bookmarks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load bookmarks");
            StatusMessage = "Failed to load bookmarks";
            MessageBox.Show($"Failed to load bookmarks:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task AddCurrentPathAsync()
    {
        var activePanel = _mainViewModel.ActivePanel;
        if (activePanel == null)
        {
            MessageBox.Show("No active panel selected", "Add Bookmark", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var currentPath = activePanel.CurrentPath;
        if (string.IsNullOrWhiteSpace(currentPath))
        {
            MessageBox.Show("No path selected", "Add Bookmark", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            // Check if already bookmarked
            if (await _bookmarkService.IsBookmarkedAsync(currentPath))
            {
                MessageBox.Show(
                    $"Path is already bookmarked:\n{currentPath}",
                    "Add Bookmark",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            // Show dialog to customize name
            var dialog = new AddBookmarkDialog(currentPath)
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                var bookmark = await _bookmarkService.AddBookmarkAsync(currentPath, dialog.BookmarkName);
                Bookmarks.Add(bookmark);

                StatusMessage = $"Added bookmark: {bookmark.Name}";
                _logger.LogInformation("Added bookmark: {Name} -> {Path}", bookmark.Name, bookmark.Path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add bookmark");
            MessageBox.Show($"Failed to add bookmark:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task NavigateToBookmarkAsync(Bookmark? bookmark)
    {
        if (bookmark == null)
        {
            _logger.LogWarning("No bookmark selected for navigation");
            return;
        }

        var activePanel = _mainViewModel.ActivePanel;
        if (activePanel == null)
        {
            MessageBox.Show("No active panel selected", "Navigate", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _logger.LogInformation("Navigating to bookmark: {Name} -> {Path}", bookmark.Name, bookmark.Path);
            await activePanel.NavigateToCommand.ExecuteAsync(bookmark.Path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate to bookmark: {Path}", bookmark.Path);
            MessageBox.Show($"Failed to navigate to:\n{bookmark.Path}\n\nError: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task EditBookmarkAsync(Bookmark? bookmark)
    {
        if (bookmark == null)
        {
            _logger.LogWarning("No bookmark selected for editing");
            return;
        }

        try
        {
            var dialog = new EditBookmarkDialog(bookmark)
            {
                Owner = Application.Current.MainWindow
            };

            if (dialog.ShowDialog() == true)
            {
                await _bookmarkService.UpdateBookmarkAsync(bookmark);
                StatusMessage = $"Updated bookmark: {bookmark.Name}";
                _logger.LogInformation("Updated bookmark: {Name}", bookmark.Name);

                var index = Bookmarks.IndexOf(bookmark);
                if (index >= 0)
                {
                    Bookmarks.RemoveAt(index);
                    Bookmarks.Insert(index, bookmark);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to edit bookmark");
            MessageBox.Show($"Failed to edit bookmark:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task DeleteBookmarkAsync(Bookmark? bookmark)
    {
        if (bookmark == null)
        {
            _logger.LogWarning("No bookmark selected for deletion");
            return;
        }

        var result = MessageBox.Show(
            $"Delete bookmark '{bookmark.Name}'?",
            "Delete Bookmark",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            await _bookmarkService.RemoveBookmarkAsync(bookmark.Id);
            Bookmarks.Remove(bookmark);

            StatusMessage = $"Deleted bookmark: {bookmark.Name}";
            _logger.LogInformation("Deleted bookmark: {Name}", bookmark.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete bookmark");
            MessageBox.Show($"Failed to delete bookmark:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task MoveBookmarkUpAsync(Bookmark? bookmark)
    {
        if (bookmark == null || Bookmarks.Count < 2)
            return;

        var index = Bookmarks.IndexOf(bookmark);
        if (index <= 0)
            return;

        try
        {
            Bookmarks.Move(index, index - 1);
            await _bookmarkService.ReorderBookmarksAsync(Bookmarks.ToList());

            _logger.LogDebug("Moved bookmark up: {Name}", bookmark.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reorder bookmarks");
        }
    }

    [RelayCommand]
    private async Task MoveBookmarkDownAsync(Bookmark? bookmark)
    {
        if (bookmark == null || Bookmarks.Count < 2)
            return;

        var index = Bookmarks.IndexOf(bookmark);
        if (index < 0 || index >= Bookmarks.Count - 1)
            return;

        try
        {
            Bookmarks.Move(index, index + 1);
            await _bookmarkService.ReorderBookmarksAsync(Bookmarks.ToList());

            _logger.LogDebug("Moved bookmark down: {Name}", bookmark.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reorder bookmarks");
        }
    }
}