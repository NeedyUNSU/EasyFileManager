using EasyFileManager.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace EasyFileManager.Core.Interfaces;

/// <summary>
/// Service for saving and loading tab sessions
/// Persists tabs to JSON storage for session restoration
/// </summary>
public interface ITabPersistenceService
{
    /// <summary>
    /// Saves the current tab session to storage
    /// </summary>
    /// <param name="session">Tab session to save</param>
    /// <param name="panelId">Panel identifier (e.g., "left", "right")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task SaveSessionAsync(
        TabSession session,
        string panelId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the tab session from storage
    /// </summary>
    /// <param name="panelId">Panel identifier (e.g., "left", "right")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Loaded session or null if no session exists</returns>
    Task<TabSession?> LoadSessionAsync(
        string panelId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears the saved session for a panel
    /// </summary>
    /// <param name="panelId">Panel identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task ClearSessionAsync(
        string panelId,
        CancellationToken cancellationToken = default);
}