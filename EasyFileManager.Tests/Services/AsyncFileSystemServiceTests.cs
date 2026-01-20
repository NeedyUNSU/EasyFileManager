using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;
using EasyFileManager.Core.Services;
using EasyFileManager.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EasyFileManager.Tests.Services;

/// <summary>
/// Unit tests for AsyncFileSystemService
/// Tests cover: loading directories, file filtering, cancellation, and edge cases
/// </summary>
public class AsyncFileSystemServiceTests : IDisposable
{
    private readonly TestAppLogger<AsyncFileSystemService> _logger;
    private readonly AsyncFileSystemService _service;
    private readonly TestFileSystemHelper _fileSystem;

    public AsyncFileSystemServiceTests()
    {
        _logger = TestLoggerFactory.CreateTest<AsyncFileSystemService>();
        _service = new AsyncFileSystemService(_logger);
        _fileSystem = new TestFileSystemHelper();
    }

    #region LoadDirectoryAsync Tests

    [Fact]
    public async Task LoadDirectoryAsync_ValidPath_ReturnsDirectoryEntry()
    {
        // Arrange
        _fileSystem.CreateFile("test1.txt", "content1");
        _fileSystem.CreateFile("test2.txt", "content2");
        _fileSystem.CreateDirectory("SubFolder");

        // Act
        var result = await _service.LoadDirectoryAsync(_fileSystem.RootPath, default, true);

        // Assert
        result.Should().NotBeNull();
        result.Children.Should().HaveCount(3);
        result.Children.OfType<FileEntry>().Should().HaveCount(2);
        result.Children.OfType<DirectoryEntry>().Should().HaveCount(1);
    }

    [Fact]
    public async Task LoadDirectoryAsync_EmptyDirectory_ReturnsEmptyChildren()
    {
        // Arrange - RootPath is already empty

        // Act
        var result = await _service.LoadDirectoryAsync(_fileSystem.RootPath, default, true);

        // Assert
        result.Should().NotBeNull();
        result.Children.Should().BeEmpty();
    }

    [Fact]
    public async Task LoadDirectoryAsync_WithHiddenFiles_RespectsShowHiddenFlag()
    {
        // Arrange
        var normalFile = _fileSystem.CreateFile("normal.txt", "visible");
        var hiddenFile = _fileSystem.CreateFile("hidden.txt", "hidden");
        File.SetAttributes(hiddenFile, FileAttributes.Hidden);

        // Act - without hidden files
        var resultWithoutHidden = await _service.LoadDirectoryAsync(
            _fileSystem.RootPath, 
            cancellationToken: default,
            showFileExtension:true,
            showSystemFiles:true,
            showHiddenFiles: false);

        // Assert
        resultWithoutHidden.Children.Should().HaveCount(1);
        resultWithoutHidden.Children[0].Name.Should().Be("normal.txt");

        // Act - with hidden files
        var resultWithHidden = await _service.LoadDirectoryAsync(
            _fileSystem.RootPath, 
            cancellationToken: default,
            showFileExtension: true,
            showSystemFiles: true,
            showHiddenFiles: true);

        // Assert
        resultWithHidden.Children.Should().HaveCount(2);
    }

    [Fact]
    public async Task LoadDirectoryAsync_WithFileExtensions_RespectsShowExtensionFlag()
    {
        // Arrange
        _fileSystem.CreateFile("document.pdf", "pdf content");
        _fileSystem.CreateFile("image.png", "png content");

        // Act - with extensions
        var resultWithExt = await _service.LoadDirectoryAsync(
            _fileSystem.RootPath,
            cancellationToken: default,
            showHiddenFiles: true,
            showSystemFiles: true,
            showFileExtension: true);

        // Assert
        resultWithExt.Children.Should().AllSatisfy(c => 
            c.Name.Should().Contain("."));

        // Act - without extensions
        var resultWithoutExt = await _service.LoadDirectoryAsync(
            _fileSystem.RootPath,
            cancellationToken: default,
            showHiddenFiles: true,
            showSystemFiles: true,
            showFileExtension: false);

        // Assert - names should not contain extensions
        resultWithoutExt.Children.Select(c => c.Name)
            .Should().Contain(n => !n.Contains("."));
    }

    [Fact]
    public async Task LoadDirectoryAsync_InvalidPath_ThrowsDirectoryNotFoundException()
    {
        // Arrange
        var invalidPath = Path.Combine(_fileSystem.RootPath, "NonExistent");

        // Act & Assert
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => _service.LoadDirectoryAsync(invalidPath, default, true));
    }

    [Fact]
    public async Task LoadDirectoryAsync_WithProgress_ReportsProgress()
    {
        // Arrange
        _fileSystem.CreateFile("file1.txt", "a");
        _fileSystem.CreateFile("file2.txt", "b");
        _fileSystem.CreateFile("file3.txt", "c");
        var progressReports = new List<LoadProgress>();
        var progress = new Progress<LoadProgress>(p => progressReports.Add(p));

        // Act
        await _service.LoadDirectoryAsync(_fileSystem.RootPath, progress);

        // Allow time for progress updates
        await Task.Delay(100);

        // Assert
        progressReports.Should().NotBeEmpty();
    }

    [Fact]
    public async Task LoadDirectoryAsync_ReturnsCorrectFileMetadata()
    {
        // Arrange
        var content = "Test content for metadata check";
        var filePath = _fileSystem.CreateFile("metadata_test.txt", content);
        var fileInfo = new FileInfo(filePath);

        // Act
        var result = await _service.LoadDirectoryAsync(_fileSystem.RootPath, default, true);

        // Assert
        var fileEntry = result.Children.OfType<FileEntry>().First();
        fileEntry.Name.Should().Be("metadata_test.txt");
        fileEntry.Size.Should().Be(content.Length);
        fileEntry.LastModified.Should().BeCloseTo(fileInfo.LastWriteTimeUtc, TimeSpan.FromSeconds(2));
    }

    #endregion

    #region GetDrivesAsync Tests

    [Fact]
    public async Task GetDrivesAsync_ReturnsAvailableDrives()
    {
        // Act
        var drives = await _service.GetDrivesAsync();

        // Assert
        drives.Should().NotBeEmpty();
        drives.Should().OnlyContain(d => d.IsReady);
    }

    [Fact]
    public async Task GetDrivesWithMetadataAsync_ReturnsDetailedDriveInfo()
    {
        // Act
        var drives = await _service.GetDrivesWithMetadataAsync();

        // Assert
        drives.Should().NotBeEmpty();
        drives.Should().AllSatisfy(d =>
        {
            d.Name.Should().NotBeNullOrEmpty();
            d.UsedSpacePercentage.Should().BeGreaterThan(0);
        });
    }

    #endregion

    #region Archive Path Detection Tests

    [Fact]
    public void IsArchivePath_WithoutSeparator_ReturnsFalse()
    {
        // Arrange
        var normalPath = @"C:\test\archive.zip";

        // Act
        var result = _service.IsArchivePath(normalPath);

        // Assert
        result.Should().BeFalse();
    }

    
    #endregion

    public void Dispose()
    {
        _fileSystem.Dispose();
    }
}
