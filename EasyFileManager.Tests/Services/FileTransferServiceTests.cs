using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;
using EasyFileManager.Core.Services;
using EasyFileManager.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EasyFileManager.Tests.Services;

/// <summary>
/// Unit tests for FileTransferService
/// Tests cover: copy operations, move operations, conflict resolution, progress reporting
/// </summary>
public class FileTransferServiceTests : IDisposable
{
    private readonly TestAppLogger<FileTransferService> _logger;
    private readonly FileTransferService _service;
    private readonly TestFileSystemHelper _fileSystem;

    public FileTransferServiceTests()
    {
        _logger = TestLoggerFactory.CreateTest<FileTransferService>();
        _service = new FileTransferService(_logger);
        _fileSystem = new TestFileSystemHelper();
    }

    #region CopyAsync Tests

    [Fact]
    public async Task CopyAsync_SingleFile_CopiesSuccessfully()
    {
        // Arrange
        var sourceContent = "Test file content for copy";
        var sourceFile = _fileSystem.CreateFile("source.txt", sourceContent);
        var destDir = _fileSystem.CreateDirectory("destination");

        // Act
        await _service.CopyAsync(new[] { sourceFile }, destDir);

        // Assert
        var copiedFile = Path.Combine(destDir, "source.txt");
        File.Exists(copiedFile).Should().BeTrue();
        File.ReadAllText(copiedFile).Should().Be(sourceContent);
        File.Exists(sourceFile).Should().BeTrue("Original should still exist");
    }

    [Fact]
    public async Task CopyAsync_MultipleFiles_CopiesAllSuccessfully()
    {
        // Arrange
        var files = new[]
        {
            _fileSystem.CreateFile("file1.txt", "Content 1"),
            _fileSystem.CreateFile("file2.txt", "Content 2"),
            _fileSystem.CreateFile("file3.txt", "Content 3")
        };
        var destDir = _fileSystem.CreateDirectory("destination");

        // Act
        await _service.CopyAsync(files, destDir);

        // Assert
        Directory.GetFiles(destDir).Should().HaveCount(3);
    }

    [Fact]
    public async Task CopyAsync_Directory_CopiesRecursively()
    {
        // Arrange
        var sourceDir = _fileSystem.CreateDirectory("source");
        _fileSystem.CreateFile("source/file1.txt", "Content 1");
        _fileSystem.CreateFile("source/subdir/file2.txt", "Content 2");
        var destDir = _fileSystem.CreateDirectory("destination");

        // Act
        await _service.CopyAsync(new[] { sourceDir }, destDir);

        // Assert
        var copiedDir = Path.Combine(destDir, "source");
        Directory.Exists(copiedDir).Should().BeTrue();
        File.Exists(Path.Combine(copiedDir, "file1.txt")).Should().BeTrue();
        File.Exists(Path.Combine(copiedDir, "subdir", "file2.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task CopyAsync_WithProgress_ReportsProgressCorrectly()
    {
        // Arrange
        var sourceFile = _fileSystem.CreateFile("large_file.bin", 1024 * 100); // 100KB
        var destDir = _fileSystem.CreateDirectory("destination");
        var progressReports = new List<FileTransferProgress>();
        var progress = new Progress<FileTransferProgress>(p => progressReports.Add(p));

        // Act
        await _service.CopyAsync(new[] { sourceFile }, destDir, progress);
        await Task.Delay(100); // Wait for progress reports

        // Assert
        progressReports.Should().NotBeEmpty();
        progressReports.Last().OverallProgress.Should().Be(100);
    }

    [Fact]
    public async Task CopyAsync_ConflictSkip_SkipsExistingFile()
    {
        // Arrange
        var sourceFile = _fileSystem.CreateFile("source/file.txt", "New content");
        var destDir = _fileSystem.CreateDirectory("destination");
        _fileSystem.CreateFile("destination/file.txt", "Old content");

        Func<FileConflictInfo, Task<FileConflictResolution>> resolver = 
            _ => Task.FromResult(new FileConflictResolution 
            { 
                Action = ConflictAction.Skip 
            });

        // Act
        await _service.CopyAsync(new[] { sourceFile }, destDir, conflictResolver: resolver);

        // Assert
        var destFile = Path.Combine(destDir, "file.txt");
        File.ReadAllText(destFile).Should().Be("Old content", "File should not be overwritten");
    }

    [Fact]
    public async Task CopyAsync_ConflictOverwrite_OverwritesExistingFile()
    {
        // Arrange
        var sourceFile = _fileSystem.CreateFile("source/file.txt", "New content");
        var destDir = _fileSystem.CreateDirectory("destination");
        _fileSystem.CreateFile("destination/file.txt", "Old content");

        Func<FileConflictInfo, Task<FileConflictResolution>> resolver = 
            _ => Task.FromResult(new FileConflictResolution 
            { 
                Action = ConflictAction.Overwrite 
            });

        // Act
        await _service.CopyAsync(new[] { sourceFile }, destDir, conflictResolver: resolver);

        // Assert
        var destFile = Path.Combine(destDir, "file.txt");
        File.ReadAllText(destFile).Should().Be("New content");
    }

    [Fact]
    public async Task CopyAsync_ConflictRename_CreatesNewName()
    {
        // Arrange
        var sourceFile = _fileSystem.CreateFile("source/file.txt", "New content");
        var destDir = _fileSystem.CreateDirectory("destination");
        _fileSystem.CreateFile("destination/file.txt", "Old content");

        Func<FileConflictInfo, Task<FileConflictResolution>> resolver = 
            _ => Task.FromResult(new FileConflictResolution 
            { 
                Action = ConflictAction.Rename 
            });

        // Act
        await _service.CopyAsync(new[] { sourceFile }, destDir, conflictResolver: resolver);

        // Assert
        Directory.GetFiles(destDir).Should().HaveCount(2);
        File.Exists(Path.Combine(destDir, "file (1).txt")).Should().BeTrue();
    }

    [Fact]
    public async Task CopyAsync_PreservesTimestamps()
    {
        // Arrange
        var sourceFile = _fileSystem.CreateFile("source.txt", "content");
        var sourceInfo = new FileInfo(sourceFile);
        var originalCreated = sourceInfo.CreationTime;
        var originalModified = sourceInfo.LastWriteTime;
        var destDir = _fileSystem.CreateDirectory("destination");

        // Act
        await _service.CopyAsync(new[] { sourceFile }, destDir);

        // Assert
        var copiedInfo = new FileInfo(Path.Combine(destDir, "source.txt"));
        copiedInfo.LastWriteTime.Should().BeCloseTo(originalModified, TimeSpan.FromSeconds(2));
    }

    #endregion

    #region MoveAsync Tests

    [Fact]
    public async Task MoveAsync_SingleFile_MovesSuccessfully()
    {
        // Arrange
        var sourceContent = "Test file content for move";
        var sourceFile = _fileSystem.CreateFile("source.txt", sourceContent);
        var destDir = _fileSystem.CreateDirectory("destination");

        // Act
        await _service.MoveAsync(new[] { sourceFile }, destDir);

        // Assert
        var movedFile = Path.Combine(destDir, "source.txt");
        File.Exists(movedFile).Should().BeTrue();
        File.ReadAllText(movedFile).Should().Be(sourceContent);
        File.Exists(sourceFile).Should().BeFalse("Original should be deleted");
    }

    [Fact]
    public async Task MoveAsync_Directory_MovesRecursively()
    {
        // Arrange
        var sourceDir = _fileSystem.CreateDirectory("source");
        _fileSystem.CreateFile("source/file1.txt", "Content 1");
        _fileSystem.CreateFile("source/subdir/file2.txt", "Content 2");
        var destDir = _fileSystem.CreateDirectory("destination");

        // Act
        await _service.MoveAsync(new[] { sourceDir }, destDir);

        // Assert
        var movedDir = Path.Combine(destDir, "source");
        Directory.Exists(movedDir).Should().BeTrue();
        Directory.Exists(sourceDir).Should().BeFalse("Original directory should be deleted");
    }

    #endregion

    #region CalculateTotalSizeAsync Tests

    [Fact]
    public async Task CalculateTotalSizeAsync_SingleFile_ReturnsCorrectSize()
    {
        // Arrange
        var file = _fileSystem.CreateFile("test.bin", 1024);

        // Act
        var size = await _service.CalculateTotalSizeAsync(new[] { file });

        // Assert
        size.Should().Be(1024);
    }

    [Fact]
    public async Task CalculateTotalSizeAsync_MultipleFiles_ReturnsSumOfSizes()
    {
        // Arrange
        var files = new[]
        {
            _fileSystem.CreateFile("file1.bin", 1000),
            _fileSystem.CreateFile("file2.bin", 2000),
            _fileSystem.CreateFile("file3.bin", 3000)
        };

        // Act
        var size = await _service.CalculateTotalSizeAsync(files);

        // Assert
        size.Should().Be(6000);
    }

    [Fact]
    public async Task CalculateTotalSizeAsync_Directory_ReturnsRecursiveSize()
    {
        // Arrange
        var dir = _fileSystem.CreateDirectory("testdir");
        _fileSystem.CreateFile("testdir/file1.bin", 1000);
        _fileSystem.CreateFile("testdir/subdir/file2.bin", 2000);

        // Act
        var size = await _service.CalculateTotalSizeAsync(new[] { dir });

        // Assert
        size.Should().Be(3000);
    }

    #endregion

    public void Dispose()
    {
        _fileSystem.Dispose();
    }
}
