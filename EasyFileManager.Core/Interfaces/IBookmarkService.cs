using EasyFileManager.Core.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EasyFileManager.Core.Interfaces;

/// <summary>
/// Service for managing bookmarked directories
/// Persists bookmarks to JSON file in user's AppData
/// </summary>
public interface IBookmarkService
{
    /// <summary>
    /// Loads all bookmarks from storage
    /// </summary>
    Task<List<Bookmark>> LoadBookmarksAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves bookmarks to storage
    /// </summary>
    Task SaveBookmarksAsync(List<Bookmark> bookmarks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new bookmark
    /// </summary>
    Task<Bookmark> AddBookmarkAsync(string path, string? name = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a bookmark by ID
    /// </summary>
    Task RemoveBookmarkAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing bookmark
    /// </summary>
    Task UpdateBookmarkAsync(Bookmark bookmark, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reorders bookmarks (for drag & drop)
    /// </summary>
    Task ReorderBookmarksAsync(List<Bookmark> reorderedBookmarks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a path is already bookmarked
    /// </summary>
    Task<bool> IsBookmarkedAsync(string path, CancellationToken cancellationToken = default);
}