using Microsoft.Extensions.Logging;
using System;

namespace EasyFileManager.Core.Interfaces;

/// <summary>
/// Abstraction over ILogger for easier mocking in tests
/// </summary>
public interface IAppLogger<T>
{
    void LogInformation(string message, params object[] args);
    void LogWarning(string message, params object[] args);
    void LogError(Exception exception, string message, params object[] args);
    void LogDebug(string message, params object[] args);
}