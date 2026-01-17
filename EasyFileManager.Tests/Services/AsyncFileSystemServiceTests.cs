using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EasyFileManager.Tests.Services;

public class AsyncFileSystemServiceTests
{
    private readonly Mock<ILogger<AsyncFileSystemService>> _loggerMock;
    private readonly IAppLogger<AsyncFileSystemService> _logger;
    private readonly AsyncFileSystemService _service;

    public AsyncFileSystemServiceTests()
    {
        _loggerMock = new Mock<ILogger<AsyncFileSystemService>>();
        _logger = new AppLogger<AsyncFileSystemService>(_loggerMock.Object);
        _service = new AsyncFileSystemService(_logger);
    }

    [Fact]
    public async Task LoadDirectoryAsync_ValidPath_ReturnsDirectoryEntry()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        File.WriteAllText(Path.Combine(tempDir, "test.txt"), "content");

        try
        {
            // Act
            var result = await _service.LoadDirectoryAsync(tempDir, default, true);

            // Assert
            result.Should().NotBeNull();
            result.Children.Should().HaveCount(1);
            result.Children[0].Name.Should().Be("test.txt");
        }
        finally
        {
            // Cleanup
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task LoadDirectoryAsync_InvalidPath_ThrowsDirectoryNotFoundException()
    {
        // Arrange
        var invalidPath = @"C:\NonExistentDirectory_12345";

        // Act & Assert
        await Assert.ThrowsAsync<DirectoryNotFoundException>(()
            => _service.LoadDirectoryAsync(invalidPath, default, true));
    }

    [Fact]
    public async Task LoadDirectoryAsync_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(()
            => _service.LoadDirectoryAsync(@"C:\", cts.Token));
    }
}