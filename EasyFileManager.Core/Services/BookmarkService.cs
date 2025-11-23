using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EasyFileManager.Core.Services;

/// <summary>
/// Service for managing bookmarks with JSON persistence
/// Stores bookmarks in: %LOCALAPPDATA%\EasyFileManager\bookmarks.json
/// </summary>
public class BookmarkService : IBookmarkService
{
    private readonly IAppLogger<BookmarkService> _logger;
    private readonly string _bookmarksFilePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public BookmarkService(IAppLogger<BookmarkService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EasyFileManager");

        Directory.CreateDirectory(appDataPath);
        _bookmarksFilePath = Path.Combine(appDataPath, "bookmarks.json");

        _logger.LogDebug("BookmarkService initialized. Storage: {Path}", _bookmarksFilePath);
    }

    public async Task<List<Bookmark>> LoadBookmarksAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_bookmarksFilePath))
            {
                _logger.LogDebug("No bookmarks file found. Returning empty list.");
                return new List<Bookmark>();
            }

            var json = await File.ReadAllTextAsync(_bookmarksFilePath, cancellationToken);
            var bookmarks = JsonSerializer.Deserialize<List<Bookmark>>(json, JsonOptions) ?? new List<Bookmark>();

            _logger.LogInformation("Loaded {Count} bookmarks", bookmarks.Count);
            return bookmarks.OrderBy(b => b.Order).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load bookmarks");
            return new List<Bookmark>();
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveBookmarksAsync(List<Bookmark> bookmarks, CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken);
        try
        {
            // Ensure Order property is set correctly
            for (int i = 0; i < bookmarks.Count; i++)
            {
                bookmarks[i].Order = i;
            }

            var json = JsonSerializer.Serialize(bookmarks, JsonOptions);
            await File.WriteAllTextAsync(_bookmarksFilePath, json, cancellationToken);

            _logger.LogInformation("Saved {Count} bookmarks", bookmarks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save bookmarks");
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<Bookmark> AddBookmarkAsync(string path, string? name = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty", nameof(path));

        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Directory not found: {path}");

        var bookmarks = await LoadBookmarksAsync(cancellationToken);

        // Check if already bookmarked
        if (bookmarks.Any(b => b.Path.Equals(path, StringComparison.OrdinalIgnoreCase)))
        {
            _logger.LogWarning("Path already bookmarked: {Path}", path);
            throw new InvalidOperationException($"Path is already bookmarked: {path}");
        }

        var bookmark = Bookmark.FromPath(path);
        if (!string.IsNullOrWhiteSpace(name))
        {
            bookmark.Name = name;
        }

        bookmark.Order = bookmarks.Count;
        bookmarks.Add(bookmark);

        await SaveBookmarksAsync(bookmarks, cancellationToken);

        _logger.LogInformation("Added bookmark: {Name} -> {Path}", bookmark.Name, bookmark.Path);
        return bookmark;
    }

    public async Task RemoveBookmarkAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var bookmarks = await LoadBookmarksAsync(cancellationToken);
        var bookmark = bookmarks.FirstOrDefault(b => b.Id == id);

        if (bookmark == null)
        {
            _logger.LogWarning("Bookmark not found: {Id}", id);
            return;
        }

        bookmarks.Remove(bookmark);
        await SaveBookmarksAsync(bookmarks, cancellationToken);

        _logger.LogInformation("Removed bookmark: {Name}", bookmark.Name);
    }

    public async Task UpdateBookmarkAsync(Bookmark bookmark, CancellationToken cancellationToken = default)
    {
        if (bookmark == null)
            throw new ArgumentNullException(nameof(bookmark));

        var bookmarks = await LoadBookmarksAsync(cancellationToken);
        var existing = bookmarks.FirstOrDefault(b => b.Id == bookmark.Id);

        if (existing == null)
        {
            _logger.LogWarning("Bookmark not found for update: {Id}", bookmark.Id);
            throw new InvalidOperationException($"Bookmark not found: {bookmark.Id}");
        }

        existing.Name = bookmark.Name;
        existing.Path = bookmark.Path;
        existing.Icon = bookmark.Icon;

        await SaveBookmarksAsync(bookmarks, cancellationToken);

        _logger.LogInformation("Updated bookmark: {Name}", bookmark.Name);
    }

    public async Task ReorderBookmarksAsync(List<Bookmark> reorderedBookmarks, CancellationToken cancellationToken = default)
    {
        if (reorderedBookmarks == null || reorderedBookmarks.Count == 0)
            return;

        await SaveBookmarksAsync(reorderedBookmarks, cancellationToken);

        _logger.LogInformation("Reordered {Count} bookmarks", reorderedBookmarks.Count);
    }

    public async Task<bool> IsBookmarkedAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var bookmarks = await LoadBookmarksAsync(cancellationToken);
        return bookmarks.Any(b => b.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
    }
}