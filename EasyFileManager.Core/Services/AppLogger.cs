using EasyFileManager.Core.Interfaces;
using Microsoft.Extensions.Logging;
using System;

namespace EasyFileManager.Core.Services;

/// <summary>
/// Wrapper around ILogger<T> for structured logging
/// </summary>
public class AppLogger<T> : IAppLogger<T>
{
    private readonly ILogger<T> _logger;

    public AppLogger(ILogger<T> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void LogInformation(string message, params object[] args)
        => _logger.LogInformation(message, args);

    public void LogWarning(string message, params object[] args)
        => _logger.LogWarning(message, args);

    public void LogError(Exception exception, string message, params object[] args)
        => _logger.LogError(exception, message, args);

    public void LogDebug(string message, params object[] args)
        => _logger.LogDebug(message, args);
}