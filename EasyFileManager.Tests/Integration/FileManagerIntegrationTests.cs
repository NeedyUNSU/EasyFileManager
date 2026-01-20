using EasyFileManager.Core.Models;
using EasyFileManager.Core.Services;
using EasyFileManager.Tests.Helpers;
using FluentAssertions;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace EasyFileManager.Tests.Integration;

/// <summary>
/// Integration tests for complete user scenarios
/// Tests real-world usage patterns and measures performance
/// </summary>
public class FileManagerIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly TestFileSystemHelper _fileSystem;
    private readonly AsyncFileSystemService _fileSystemService;
    private readonly FileTransferService _transferService;
    private readonly DuplicateFinderService _duplicateService;
    private readonly List<PerformanceResult> _performanceResults;

    public FileManagerIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _fileSystem = new TestFileSystemHelper();
        _performanceResults = new List<PerformanceResult>();
        
        var fsLogger = TestLoggerFactory.CreateNull<AsyncFileSystemService>();
        var ftLogger = TestLoggerFactory.CreateNull<FileTransferService>();
        var dfLogger = TestLoggerFactory.CreateNull<DuplicateFinderService>();
        
        _fileSystemService = new AsyncFileSystemService(fsLogger);
        _transferService = new FileTransferService(ftLogger);
        _duplicateService = new DuplicateFinderService(dfLogger);
    }

    #region Scenario: Browse Large Directory

    [Theory]
    [InlineData(100, "Small directory (100 files)")]
    [InlineData(500, "Medium directory (500 files)")]
    [InlineData(1000, "Large directory (1000 files)")]
    public async Task Scenario_BrowseLargeDirectory(int fileCount, string scenario)
    {
        // Arrange
        _fileSystem.CreateComplexStructure(1, fileCount);
        var dirPath = _fileSystem.GetFullPath("Dir_0000");
        
        // Act
        var sw = Stopwatch.StartNew();
        var result = await _fileSystemService.LoadDirectoryAsync(dirPath, default, true);
        sw.Stop();
        
        // Assert
        result.Children.Should().HaveCount(fileCount);
        
        // Record performance
        RecordPerformance(scenario, "LoadDirectory", sw.ElapsedMilliseconds, fileCount);
        _output.WriteLine($"{scenario}: {sw.ElapsedMilliseconds}ms for {fileCount} files ({sw.ElapsedMilliseconds / (double)fileCount:F2}ms per file)");
    }

    [Theory]
    [InlineData(5, "Shallow nesting (5 levels)")]
    [InlineData(10, "Medium nesting (10 levels)")]
    [InlineData(20, "Deep nesting (20 levels)")]
    public async Task Scenario_NavigateDeepDirectory(int depth, string scenario)
    {
        // Arrange
        _fileSystem.CreateNestedStructure(depth);
        var deepestPath = string.Join(Path.DirectorySeparatorChar.ToString(), 
            Enumerable.Range(0, depth).Select(i => $"Level_{i}"));
        var fullPath = _fileSystem.GetFullPath(deepestPath);
        
        // Act
        var sw = Stopwatch.StartNew();
        var result = await _fileSystemService.LoadDirectoryAsync(fullPath, default, true);
        sw.Stop();
        
        // Assert
        result.Should().NotBeNull();
        
        // Record performance
        RecordPerformance(scenario, "NavigateDeep", sw.ElapsedMilliseconds, depth);
        _output.WriteLine($"{scenario}: {sw.ElapsedMilliseconds}ms to navigate {depth} levels");
    }

    #endregion

    #region Scenario: Copy Files

    [Theory]
    [InlineData(1, 1024 * 1024, "Copy 1MB file")]
    [InlineData(10, 1024 * 100, "Copy 10 x 100KB files")]
    [InlineData(100, 1024 * 10, "Copy 100 x 10KB files")]
    public async Task Scenario_CopyFiles(int fileCount, int fileSizeBytes, string scenario)
    {
        // Arrange
        var sourceDir = _fileSystem.CreateDirectory("source");
        var destDir = _fileSystem.CreateDirectory("destination");
        
        var files = new List<string>();
        for (int i = 0; i < fileCount; i++)
        {
            files.Add(_fileSystem.CreateFile($"source/file_{i:D4}.bin", fileSizeBytes));
        }
        
        // Act
        var sw = Stopwatch.StartNew();
        await _transferService.CopyAsync(files, destDir);
        sw.Stop();
        
        // Assert
        Directory.GetFiles(destDir).Should().HaveCount(fileCount);
        
        // Calculate throughput
        var totalBytes = (long)fileCount * fileSizeBytes;
        var throughputMBps = totalBytes / 1024.0 / 1024.0 / (sw.ElapsedMilliseconds / 1000.0);
        
        RecordPerformance(scenario, "Copy", sw.ElapsedMilliseconds, fileCount);
        _output.WriteLine($"{scenario}: {sw.ElapsedMilliseconds}ms, Throughput: {throughputMBps:F2} MB/s");
    }

    [Fact]
    public async Task Scenario_CopyWithConflictResolution()
    {
        // Arrange
        var sourceDir = _fileSystem.CreateDirectory("source");
        var destDir = _fileSystem.CreateDirectory("destination");
        
        // Create source files
        _fileSystem.CreateFile("source/file1.txt", "New content 1");
        _fileSystem.CreateFile("source/file2.txt", "New content 2");
        
        // Create existing files in destination
        _fileSystem.CreateFile("destination/file1.txt", "Old content 1");
        
        var conflictCount = 0;
        Func<FileConflictInfo, Task<FileConflictResolution>> resolver = conflict =>
        {
            conflictCount++;
            return Task.FromResult(new FileConflictResolution { Action = ConflictAction.Overwrite });
        };
        
        var sourceFiles = Directory.GetFiles(sourceDir);
        
        // Act
        var sw = Stopwatch.StartNew();
        await _transferService.CopyAsync(sourceFiles, destDir, conflictResolver: resolver);
        sw.Stop();
        
        // Assert
        conflictCount.Should().Be(1);
        File.ReadAllText(Path.Combine(destDir, "file1.txt")).Should().Be("New content 1");
        
        _output.WriteLine($"Copy with conflict resolution: {sw.ElapsedMilliseconds}ms, {conflictCount} conflict(s) resolved");
    }

    #endregion

    #region Scenario: Find Duplicates

    [Theory]
    [InlineData(100, 10, "100 files, 10 duplicate groups")]
    [InlineData(500, 50, "500 files, 50 duplicate groups")]
    [InlineData(1000, 100, "1000 files, 100 duplicate groups")]
    public async Task Scenario_FindDuplicates(int totalFiles, int groupCount, string scenario)
    {
        // Arrange
        var filesPerGroup = totalFiles / groupCount;
        for (int g = 0; g < groupCount; g++)
        {
            var content = $"Duplicate content for group {g} - padding: {new string('X', 500)}";
            for (int f = 0; f < filesPerGroup; f++)
            {
                _fileSystem.CreateFile($"group_{g:D3}/file_{f:D3}.txt", content);
            }
        }
        
        var options = new DuplicateScanOptions
        {
            CompareMode = DuplicateCompareMode.ContentHash,
            HashAlgorithm = HashAlgorithmType.MD5,
            IncludeSubfolders = true
        };
        
        // Act
        var sw = Stopwatch.StartNew();
        var results = await _duplicateService.FindDuplicatesAsync(
            new[] { _fileSystem.RootPath }, options);
        sw.Stop();
        
        // Assert
        results.Should().HaveCount(groupCount);
        
        var totalWasted = results.Sum(g => g.TotalWastedSpace);
        RecordPerformance(scenario, "FindDuplicates", sw.ElapsedMilliseconds, totalFiles);
        _output.WriteLine($"{scenario}: {sw.ElapsedMilliseconds}ms, Found {results.Count} groups, Wasted: {totalWasted / 1024.0:F2} KB");
    }

    [Fact]
    public async Task Scenario_CompareDuplicateAlgorithms()
    {
        // Arrange
        _fileSystem.CreateComplexStructure(10, 50);
        
        // Create some duplicates
        var content = "Duplicate content for comparison test";
        _fileSystem.CreateDuplicateFiles(content, 
            "Dir_0000/dup1.txt", "Dir_0001/dup2.txt", "Dir_0002/dup3.txt");
        
        var algorithms = new[]
        {
            (DuplicateCompareMode.NameOnly, "By Name"),
            (DuplicateCompareMode.NameAndSize, "By Name+Size"),
            (DuplicateCompareMode.ContentHash, "By Hash (MD5)")
        };
        
        _output.WriteLine("Algorithm comparison:");
        
        foreach (var (mode, name) in algorithms)
        {
            var options = new DuplicateScanOptions
            {
                CompareMode = mode,
                HashAlgorithm = HashAlgorithmType.MD5,
                IncludeSubfolders = true
            };
            
            var sw = Stopwatch.StartNew();
            var results = await _duplicateService.FindDuplicatesAsync(
                new[] { _fileSystem.RootPath }, options);
            sw.Stop();
            
            _output.WriteLine($"  {name}: {sw.ElapsedMilliseconds}ms, {results.Count} groups found");
        }
    }

    #endregion

    #region Performance Summary

    [Fact]
    public void GeneratePerformanceSummary()
    {
        // This test just outputs the summary format for thesis
        _output.WriteLine("=== Performance Test Summary for Thesis ===\n");
        _output.WriteLine("| Service | Operation | Files/Items | Time (ms) | Rate |");
        _output.WriteLine("|---------|-----------|-------------|-----------|------|");
        
        foreach (var result in _performanceResults)
        {
            var rate = result.ItemCount > 0 
                ? $"{result.TimeMs / (double)result.ItemCount:F2} ms/item" 
                : "-";
            _output.WriteLine($"| {result.Service} | {result.Operation} | {result.ItemCount} | {result.TimeMs} | {rate} |");
        }
    }

    #endregion

    #region Helpers

    private void RecordPerformance(string service, string operation, long timeMs, int itemCount)
    {
        _performanceResults.Add(new PerformanceResult
        {
            Service = service,
            Operation = operation,
            TimeMs = timeMs,
            ItemCount = itemCount
        });
    }

    private class PerformanceResult
    {
        public string Service { get; set; } = "";
        public string Operation { get; set; } = "";
        public long TimeMs { get; set; }
        public int ItemCount { get; set; }
    }

    #endregion

    public void Dispose()
    {
        _fileSystem.Dispose();
    }
}
