using EasyFileManager.Core.Models;
using EasyFileManager.Core.Services;
using EasyFileManager.Tests.Helpers;
using FluentAssertions;

namespace EasyFileManager.Tests.Services;

/// <summary>
/// Unit tests for DuplicateFinderService
/// Tests cover: duplicate detection by name, size, hash, deletion of duplicates
/// </summary>
public class DuplicateFinderServiceTests : IDisposable
{
    private readonly TestAppLogger<DuplicateFinderService> _logger;
    private readonly DuplicateFinderService _service;
    private readonly TestFileSystemHelper _fileSystem;

    public DuplicateFinderServiceTests()
    {
        _logger = TestLoggerFactory.CreateTest<DuplicateFinderService>();
        _service = new DuplicateFinderService(_logger);
        _fileSystem = new TestFileSystemHelper();
    }

    #region FindDuplicatesAsync Tests - By Name

    [Fact]
    public async Task FindDuplicatesAsync_ByName_FindsDuplicatesWithSameName()
    {
        // Arrange
        _fileSystem.CreateFile("dir1/document.txt", "Content A");
        _fileSystem.CreateFile("dir2/document.txt", "Content B");
        _fileSystem.CreateFile("dir3/different.txt", "Content C");

        var options = new DuplicateScanOptions
        {
            CompareMode = DuplicateCompareMode.NameOnly,
            IncludeSubfolders = true
        };

        // Act
        var results = await _service.FindDuplicatesAsync(
            new[] { _fileSystem.RootPath }, 
            options);

        // Assert
        results.Should().HaveCount(1);
        results[0].Files.Should().HaveCount(2);
        results[0].Files.Should().AllSatisfy(f => f.FileName.Should().Be("document.txt"));
    }

    [Fact]
    public async Task FindDuplicatesAsync_ByName_IgnoresSingleFiles()
    {
        // Arrange
        _fileSystem.CreateFile("unique1.txt", "Content 1");
        _fileSystem.CreateFile("unique2.txt", "Content 2");
        _fileSystem.CreateFile("unique3.txt", "Content 3");

        var options = new DuplicateScanOptions
        {
            CompareMode = DuplicateCompareMode.NameOnly,
            IncludeSubfolders = true
        };

        // Act
        var results = await _service.FindDuplicatesAsync(
            new[] { _fileSystem.RootPath }, 
            options);

        // Assert
        results.Should().BeEmpty("No duplicates should be found");
    }

    #endregion

    #region FindDuplicatesAsync Tests - By Name and Size

    [Fact]
    public async Task FindDuplicatesAsync_ByNameAndSize_FindsExactMatches()
    {
        // Arrange
        _fileSystem.CreateFile("dir1/data.bin", 1000);
        _fileSystem.CreateFile("dir2/data.bin", 1000);
        _fileSystem.CreateFile("dir3/data.bin", 2000); // Different size

        var options = new DuplicateScanOptions
        {
            CompareMode = DuplicateCompareMode.NameAndSize,
            IncludeSubfolders = true
        };

        // Act
        var results = await _service.FindDuplicatesAsync(
            new[] { _fileSystem.RootPath }, 
            options);

        // Assert
        results.Should().HaveCount(1);
        results[0].Files.Should().HaveCount(2);
    }

    #endregion

    #region FindDuplicatesAsync Tests - By Hash

    [Fact]
    public async Task FindDuplicatesAsync_ByHash_FindsIdenticalContent()
    {
        // Arrange
        var identicalContent = "This is identical content in all files";
        _fileSystem.CreateDuplicateFiles(identicalContent, 
            "folder1/file.txt", 
            "folder2/different_name.txt",
            "folder3/another.txt");
        _fileSystem.CreateFile("unique.txt", "Unique content");

        var options = new DuplicateScanOptions
        {
            CompareMode = DuplicateCompareMode.ContentHash,
            HashAlgorithm = HashAlgorithmType.MD5,
            IncludeSubfolders = true
        };

        // Act
        var results = await _service.FindDuplicatesAsync(
            new[] { _fileSystem.RootPath }, 
            options);

        // Assert
        results.Should().HaveCount(1);
        results[0].Files.Should().HaveCount(3);
    }

    [Fact]
    public async Task FindDuplicatesAsync_ByHash_SHA256_WorksCorrectly()
    {
        // Arrange
        var content = "Test content for SHA256 hashing";
        _fileSystem.CreateDuplicateFiles(content, "a/file1.txt", "b/file2.txt");

        var options = new DuplicateScanOptions
        {
            CompareMode = DuplicateCompareMode.ContentHash,
            HashAlgorithm = HashAlgorithmType.SHA256,
            IncludeSubfolders = true
        };

        // Act
        var results = await _service.FindDuplicatesAsync(
            new[] { _fileSystem.RootPath }, 
            options);

        // Assert
        results.Should().HaveCount(1);
        results[0].Files.Should().HaveCount(2);
        results[0].Files.Should().AllSatisfy(f => 
            f.Hash.Should().NotBeNullOrEmpty());
    }

    [Fact]
    public async Task FindDuplicatesAsync_ByHash_CalculatesCorrectWastedSpace()
    {
        // Arrange
        var content = new string('X', 1000); // 1000 bytes
        _fileSystem.CreateDuplicateFiles(content, "a.txt", "b.txt", "c.txt");

        var options = new DuplicateScanOptions
        {
            CompareMode = DuplicateCompareMode.ContentHash,
            IncludeSubfolders = true
        };

        // Act
        var results = await _service.FindDuplicatesAsync(
            new[] { _fileSystem.RootPath }, 
            options);

        // Assert
        results.Should().HaveCount(1);
        results[0].TotalWastedSpace.Should().Be(2000); // 2 duplicates * 1000 bytes
    }

    #endregion

    #region FindDuplicatesAsync Tests - Filtering

    [Fact]
    public async Task FindDuplicatesAsync_WithMinSize_FiltersSmallerFiles()
    {
        // Arrange
        var smallContent = "A";
        var largeContent = new string('B', 2000);
        _fileSystem.CreateDuplicateFiles(smallContent, "small1.txt", "small2.txt");
        _fileSystem.CreateDuplicateFiles(largeContent, "large1.txt", "large2.txt");

        var options = new DuplicateScanOptions
        {
            CompareMode = DuplicateCompareMode.ContentHash,
            IncludeSubfolders = true
        };

        // Act
        var results = await _service.FindDuplicatesAsync(
            new[] { _fileSystem.RootPath }, 
            options);

        // Assert
        results.Should().HaveCount(2);
        results[0].Files.Should().AllSatisfy(f => 
            f.FileName.Should().StartWith("large"));
    }

    [Fact]
    public async Task FindDuplicatesAsync_WithoutSubfolders_OnlyScansRootLevel()
    {
        // Arrange
        _fileSystem.CreateDuplicateFiles("content", "file.txt", "subdir/file.txt");

        var options = new DuplicateScanOptions
        {
            CompareMode = DuplicateCompareMode.NameOnly,
            IncludeSubfolders = false
        };

        // Act
        var results = await _service.FindDuplicatesAsync(
            new[] { _fileSystem.RootPath }, 
            options);

        // Assert
        results.Should().BeEmpty("Should not find duplicate in subfolder");
    }

    #endregion

    #region FindDuplicatesAsync Tests - Progress

    [Fact]
    public async Task FindDuplicatesAsync_ReportsProgress()
    {
        // Arrange
        _fileSystem.CreateComplexStructure(3, 10);
        var progressReports = new List<DuplicateScanProgress>();
        var progress = new Progress<DuplicateScanProgress>(p => progressReports.Add(p));

        var options = new DuplicateScanOptions
        {
            CompareMode = DuplicateCompareMode.ContentHash,
            IncludeSubfolders = true
        };

        // Act
        await _service.FindDuplicatesAsync(
            new[] { _fileSystem.RootPath }, 
            options, 
            progress);
        
        await Task.Delay(100); // Wait for progress reports

        // Assert
        progressReports.Should().NotBeEmpty();
        progressReports.Should().Contain(p => p.Status == "Scan complete");
    }

    #endregion

    #region FindDuplicatesAsync Tests - Cancellation

    [Fact]
    public async Task FindDuplicatesAsync_WithCancellation_StopsOperation()
    {
        // Arrange
        _fileSystem.CreateComplexStructure(10, 100);
        using var cts = new CancellationTokenSource();

        var options = new DuplicateScanOptions
        {
            CompareMode = DuplicateCompareMode.ContentHash,
            IncludeSubfolders = true
        };

        // Cancel after short delay
        _ = Task.Run(async () =>
        {
            await Task.Delay(5);
            cts.Cancel();
        });

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _service.FindDuplicatesAsync(
                new[] { _fileSystem.RootPath }, 
                options, 
                null, 
                cts.Token));
    }

    #endregion

    #region DeleteDuplicatesAsync Tests

    [Fact]
    public async Task DeleteDuplicatesAsync_DeletesSpecifiedFiles()
    {
        // Arrange
        var file1 = _fileSystem.CreateFile("file1.txt", "content");
        var file2 = _fileSystem.CreateFile("file2.txt", "content");
        var file3 = _fileSystem.CreateFile("file3.txt", "content");

        var filesToDelete = new[]
        {
            new DuplicateFile 
            { 
                FullPath = file2, 
                FileName = "file2.txt" 
            },
            new DuplicateFile 
            { 
                FullPath = file3, 
                FileName = "file3.txt" 
            }
        };

        // Act
        await _service.DeleteDuplicatesAsync(filesToDelete);

        // Assert
        File.Exists(file1).Should().BeTrue("Original should remain");
        File.Exists(file2).Should().BeFalse("Should be deleted");
        File.Exists(file3).Should().BeFalse("Should be deleted");
    }

    [Fact]
    public async Task DeleteDuplicatesAsync_ReportsProgress()
    {
        // Arrange
        var files = Enumerable.Range(0, 5)
            .Select(i => _fileSystem.CreateFile($"file{i}.txt", "content"))
            .Select(path => new DuplicateFile 
            { 
                FullPath = path, 
                FileName = Path.GetFileName(path) 
            })
            .ToList();

        var progressReports = new List<int>();
        var progress = new Progress<int>(p => progressReports.Add(p));

        // Act
        await _service.DeleteDuplicatesAsync(files, progress);
        await Task.Delay(100);

        // Assert
        progressReports.Should().NotBeEmpty();
        progressReports.Last().Should().Be(100);
    }

    #endregion

    #region Multiple Search Paths Tests

    [Fact]
    public async Task FindDuplicatesAsync_MultiplePaths_FindsDuplicatesAcrossPaths()
    {
        // Arrange
        var path1 = _fileSystem.CreateDirectory("path1");
        var path2 = _fileSystem.CreateDirectory("path2");
        
        _fileSystem.CreateFile("path1/shared.txt", "Identical content");
        _fileSystem.CreateFile("path2/shared.txt", "Identical content");

        var options = new DuplicateScanOptions
        {
            CompareMode = DuplicateCompareMode.ContentHash,
            IncludeSubfolders = true
        };

        // Act
        var results = await _service.FindDuplicatesAsync(
            new[] { path1, path2 }, 
            options);

        // Assert
        results.Should().HaveCount(1);
        results[0].Files.Should().HaveCount(2);
        results[0].Files.Should().Contain(f => f.FullPath.Contains("path1"));
        results[0].Files.Should().Contain(f => f.FullPath.Contains("path2"));
    }

    #endregion

    public void Dispose()
    {
        _fileSystem.Dispose();
    }
}
