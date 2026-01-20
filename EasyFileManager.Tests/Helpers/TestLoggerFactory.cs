using EasyFileManager.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using System.Runtime.CompilerServices;

namespace EasyFileManager.Tests.Helpers;

/// <summary>
/// Test implementation of IAppLogger that does nothing (for unit tests)
/// </summary>
public class NullAppLogger<T> : IAppLogger<T>
{
    public void LogDebug(string message, params object[] args) { }
    public void LogError(Exception exception, string message, params object[] args) { }
    public void LogInformation(string message, params object[] args) { }
    public void LogWarning(string message, params object[] args) { }
}

/// <summary>
/// Test implementation that records log entries for verification
/// </summary>
public class TestAppLogger<T> : IAppLogger<T>
{
    public List<LogEntry> Entries { get; } = new();
    
    public void LogDebug(string message, params object[] args)
    {
        Entries.Add(new LogEntry(LogLevel.Debug, message, args, null));
    }
    
    public void LogError(Exception exception, string message, params object[] args)
    {
        Entries.Add(new LogEntry(LogLevel.Error, message, args, exception));
    }
    
    public void LogInformation(string message, params object[] args)
    {
        Entries.Add(new LogEntry(LogLevel.Information, message, args, null));
    }
    
    public void LogWarning(string message, params object[] args)
    {
        Entries.Add(new LogEntry(LogLevel.Warning, message, args, null));
    }

    public bool HasLoggedLevel(LogLevel level) => Entries.Any(e => e.Level == level);
    public bool HasLoggedMessage(string contains) => Entries.Any(e => e.Message.Contains(contains));
    public void Clear() => Entries.Clear();
}

public record LogEntry(LogLevel Level, string Message, object[] Args, Exception? Exception);

/// <summary>
/// Factory for creating test loggers
/// </summary>
public static class TestLoggerFactory
{
    public static NullAppLogger<T> CreateNull<T>() => new();
    public static TestAppLogger<T> CreateTest<T>() => new();
    
    public static Mock<IAppLogger<T>> CreateMock<T>()
    {
        var mock = new Mock<IAppLogger<T>>();
        return mock;
    }
}
