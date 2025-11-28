using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;
using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EasyFileManager.Core.Services;

/// <summary>
/// Persists tab sessions to JSON files in AppData
/// </summary>
public class TabPersistenceService : ITabPersistenceService
{
    private readonly IAppLogger<TabPersistenceService> _logger;
    private readonly string _storageDirectory;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public TabPersistenceService(IAppLogger<TabPersistenceService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _storageDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EasyFileManager");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        // Ensure directory exists
        Directory.CreateDirectory(_storageDirectory);

        _logger.LogDebug("TabPersistenceService initialized. Storage: {Path}", _storageDirectory);
    }

    public async Task SaveSessionAsync(
        TabSession session,
        string panelId,
        CancellationToken cancellationToken = default)
    {
        if (session == null)
            throw new ArgumentNullException(nameof(session));

        if (string.IsNullOrWhiteSpace(panelId))
            throw new ArgumentException("Panel ID cannot be empty", nameof(panelId));

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var filePath = GetFilePath(panelId);
            session.SavedAt = DateTime.UtcNow;

            var json = JsonSerializer.Serialize(session, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);

            _logger.LogInformation("Saved tab session for panel '{PanelId}': {Count} tabs",
                panelId, session.Tabs.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save tab session for panel '{PanelId}'", panelId);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<TabSession?> LoadSessionAsync(
        string panelId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(panelId))
            throw new ArgumentException("Panel ID cannot be empty", nameof(panelId));

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var filePath = GetFilePath(panelId);

            if (!File.Exists(filePath))
            {
                _logger.LogDebug("No saved session found for panel '{PanelId}'", panelId);
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var session = JsonSerializer.Deserialize<TabSession>(json, _jsonOptions);

            if (session != null)
            {
                _logger.LogInformation("Loaded tab session for panel '{PanelId}': {Count} tabs",
                    panelId, session.Tabs.Count);
            }

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load tab session for panel '{PanelId}'", panelId);
            // Return null instead of throwing - we can create a new session
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task ClearSessionAsync(
        string panelId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(panelId))
            throw new ArgumentException("Panel ID cannot be empty", nameof(panelId));

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var filePath = GetFilePath(panelId);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Cleared tab session for panel '{PanelId}'", panelId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear tab session for panel '{PanelId}'", panelId);
            throw;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private string GetFilePath(string panelId)
    {
        var safeFileName = $"tabs-{panelId}.json";
        return Path.Combine(_storageDirectory, safeFileName);
    }
}