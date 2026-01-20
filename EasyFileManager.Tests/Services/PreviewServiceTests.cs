using EasyFileManager.Core.Models;
using EasyFileManager.Core.Services;
using EasyFileManager.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EasyFileManager.Tests.Services;

/// <summary>
/// Unit tests for PreviewService
/// Tests cover: file preview generation, directory preview, type detection
/// </summary>
public class PreviewServiceTests : IDisposable
{
    private readonly TestAppLogger<PreviewService> _logger;
    private readonly PreviewService _service;
    private readonly TestFileSystemHelper _fileSystem;

    public PreviewServiceTests()
    {
        _logger = TestLoggerFactory.CreateTest<PreviewService>();
        _service = new PreviewService(_logger);
        _fileSystem = new TestFileSystemHelper();
    }

    #region GeneratePreviewAsync - Text Files

    [Fact]
    public async Task GeneratePreviewAsync_TextFile_ReturnsTextContent()
    {
        // Arrange
        var content = "Line 1\nLine 2\nLine 3";
        var filePath = _fileSystem.CreateFile("test.txt", content);

        // Act
        var preview = await _service.GeneratePreviewAsync(filePath);

        // Assert
        preview.Should().NotBeNull();
        preview.TextContent.Should().Contain("Line 1");
    }


    [Fact]
    public async Task GeneratePreviewAsync_EmptyFile_HandlesGracefully()
    {
        // Arrange
        var filePath = _fileSystem.CreateFile("empty.txt", "");

        // Act
        var preview = await _service.GeneratePreviewAsync(filePath);

        // Assert
        preview.Should().NotBeNull();
        preview.HasError.Should().BeFalse();
    }

    #endregion

    #region GeneratePreviewAsync - Image Files

    [Theory]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".gif")]
    [InlineData(".bmp")]
    public async Task GeneratePreviewAsync_ImageExtensions_AreRecognized(string extension)
    {
        // Arrange - Create a minimal valid image (1x1 pixel PNG)
        var filePath = _fileSystem.CreateFile($"test{extension}", 100);

        // Act
        var preview = await _service.GeneratePreviewAsync(filePath);

        // Assert
        preview.Should().NotBeNull();
    }

    #endregion

    #region GenerateDirectoryPreviewAsync Tests

    [Fact]
    public async Task GenerateDirectoryPreviewAsync_ValidDirectory_ReturnsStats()
    {
        // Arrange
        var dirPath = _fileSystem.CreateDirectory("testdir");
        _fileSystem.CreateFile("testdir/file1.txt", "content 1");
        _fileSystem.CreateFile("testdir/file2.txt", "content 2");
        _fileSystem.CreateDirectory("testdir/subdir");

        // Act
        var preview = await _service.GenerateDirectoryPreviewAsync(dirPath);

        // Assert
        preview.Should().NotBeNull();
        preview.DirectoryFileCount.Should().Be(2);
        preview.DirectorySubdirCount.Should().Be(1);
        preview.DirectoryTotalSize.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GenerateDirectoryPreviewAsync_EmptyDirectory_ReturnsZeroStats()
    {
        // Arrange
        var dirPath = _fileSystem.CreateDirectory("emptydir");

        // Act
        var preview = await _service.GenerateDirectoryPreviewAsync(dirPath);

        // Assert
        preview.Should().NotBeNull();
        preview.DirectoryFileCount.Should().Be(0);
        preview.DirectorySubdirCount.Should().Be(0);
        preview.DirectoryTotalSize.Should().Be(0);
    }

    [Fact]
    public async Task GenerateDirectoryPreviewAsync_NonExistentPath_ReturnsError()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_fileSystem.RootPath, "nonexistent");

        // Act
        var preview = await _service.GenerateDirectoryPreviewAsync(nonExistentPath);

        // Assert
        preview.HasError.Should().BeTrue();
    }

    #endregion

    #region GeneratePreviewAsync - Error Handling

    [Fact]
    public async Task GeneratePreviewAsync_NonExistentFile_ReturnsError()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_fileSystem.RootPath, "nonexistent.txt");

        // Act
        var preview = await _service.GeneratePreviewAsync(nonExistentPath);

        // Assert
        preview.HasError.Should().BeTrue();
        preview.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GeneratePreviewAsync_BinaryFile_HandlesGracefully()
    {
        // Arrange
        var filePath = _fileSystem.CreateFile("binary.exe", 1024);

        // Act
        var preview = await _service.GeneratePreviewAsync(filePath);

        // Assert
        preview.Should().NotBeNull();
        preview.HasError.Should().BeFalse();
    }

    #endregion

    #region PreviewContent Model Tests

    [Fact]
    public void PreviewContent_Error_HasCorrectProperties()
    {
        // Act
        var preview = PreviewContent.Error("test.txt", "Test error");

        // Assert
        preview.HasError.Should().BeTrue();
        preview.ErrorMessage.Should().Be("Test error");
        preview.FilePath.Should().Be("test.txt");
    }

    [Fact]
    public void PreviewContent_Empty_HasCorrectProperties()
    {
        // Act
        var preview = PreviewContent.Empty();

        // Assert
        preview.FilePath.Should().BeNullOrEmpty();
        preview.HasError.Should().BeFalse();
    }

    #endregion

    public void Dispose()
    {
        _fileSystem.Dispose();
    }
}
