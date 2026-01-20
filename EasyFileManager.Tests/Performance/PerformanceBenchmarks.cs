using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;
using EasyFileManager.Core.Services;
using EasyFileManager.Tests.Helpers;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace EasyFileManager.Tests.Performance;

/// <summary>
/// Configuration for benchmark tests
/// </summary>
public class BenchmarkConfig : ManualConfig
{
    public BenchmarkConfig()
    {
        AddJob(Job.ShortRun
            .WithWarmupCount(3)
            .WithIterationCount(10)
            .WithId("ShortRun"));

        AddColumnProvider(DefaultColumnProviders.Instance);
        AddLogger(ConsoleLogger.Default);
        AddExporter(CsvExporter.Default);
        AddExporter(HtmlExporter.Default);
        AddExporter(MarkdownExporter.GitHub);

        WithOrderer(new DefaultOrderer(SummaryOrderPolicy.FastestToSlowest));
        WithSummaryStyle(SummaryStyle.Default.WithMaxParameterColumnWidth(50));
    }
}

/// <summary>
/// Benchmarks for AsyncFileSystemService
/// Measures directory loading performance at various scales
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
[RankColumn]
public class AsyncFileSystemServiceBenchmarks
{
    private AsyncFileSystemService _service = null!;
    private string _testDirectory = null!;
    private NullAppLogger<AsyncFileSystemService> _logger = null!;

    [Params(10, 100, 1000)]
    public int FileCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _logger = new NullAppLogger<AsyncFileSystemService>();
        _service = new AsyncFileSystemService(_logger);

        _testDirectory = Path.Combine(Path.GetTempPath(), $"Benchmark_FileSystem_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

        // Create test files
        for (int i = 0; i < FileCount; i++)
        {
            var filePath = Path.Combine(_testDirectory, $"file_{i:D6}.txt");
            File.WriteAllText(filePath, $"Content for file {i}");
        }

        // Create some subdirectories
        for (int i = 0; i < FileCount / 10; i++)
        {
            Directory.CreateDirectory(Path.Combine(_testDirectory, $"subdir_{i:D4}"));
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }

    [Benchmark(Description = "Load directory listing")]
    public async Task<DirectoryEntry> LoadDirectoryAsync()
    {
        return await _service.LoadDirectoryAsync(_testDirectory,default,true);
    }

    [Benchmark(Description = "Load directory with progress")]
    public async Task<DirectoryEntry> LoadDirectoryWithProgressAsync()
    {
        var progress = new Progress<LoadProgress>(_ => { });
        return await _service.LoadDirectoryAsync(_testDirectory, progress);
    }

    [Benchmark(Description = "Get available drives")]
    public async Task<List<DriveInfo>> GetDrivesAsync()
    {
        return await _service.GetDrivesAsync();
    }
}

/// <summary>
/// Benchmarks for FileTransferService
/// Measures copy/move performance at various file sizes
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
[RankColumn]
public class FileTransferServiceBenchmarks
{
    private FileTransferService _service = null!;
    private string _sourceDirectory = null!;
    private string _destDirectory = null!;
    private string _sourceFile = null!;
    private NullAppLogger<FileTransferService> _logger = null!;

    [Params(1024, 1024 * 100, 1024 * 1024)]  // 1KB, 100KB, 1MB
    public int FileSizeBytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _logger = new NullAppLogger<FileTransferService>();
        _service = new FileTransferService(_logger);

        _sourceDirectory = Path.Combine(Path.GetTempPath(), $"Benchmark_Source_{Guid.NewGuid():N}");
        _destDirectory = Path.Combine(Path.GetTempPath(), $"Benchmark_Dest_{Guid.NewGuid():N}");

        Directory.CreateDirectory(_sourceDirectory);
        Directory.CreateDirectory(_destDirectory);

        // Create source file with specified size
        _sourceFile = Path.Combine(_sourceDirectory, "benchmark_file.bin");
        using var fs = new FileStream(_sourceFile, FileMode.Create);
        fs.SetLength(FileSizeBytes);

        // Write some random data
        var buffer = new byte[Math.Min(FileSizeBytes, 8192)];
        RandomNumberGenerator.Fill(buffer);
        fs.Write(buffer, 0, buffer.Length);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        // Clean destination before each iteration
        foreach (var file in Directory.GetFiles(_destDirectory))
            File.Delete(file);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_sourceDirectory))
            Directory.Delete(_sourceDirectory, true);
        if (Directory.Exists(_destDirectory))
            Directory.Delete(_destDirectory, true);
    }

    [Benchmark(Description = "Copy single file")]
    public async Task CopyFileAsync()
    {
        await _service.CopyAsync(new[] { _sourceFile }, _destDirectory);
    }

    [Benchmark(Description = "Calculate total size")]
    public async Task<long> CalculateSizeAsync()
    {
        return await _service.CalculateTotalSizeAsync(new[] { _sourceFile });
    }
}

/// <summary>
/// Benchmarks for DuplicateFinderService
/// Measures duplicate detection performance with different algorithms
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
[RankColumn]
public class DuplicateFinderServiceBenchmarks
{
    private DuplicateFinderService _service = null!;
    private string _testDirectory = null!;
    private NullAppLogger<DuplicateFinderService> _logger = null!;

    [Params(50, 200)]
    public int TotalFiles { get; set; }

    [Params(10, 50)]
    public int DuplicateGroupCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _logger = new NullAppLogger<DuplicateFinderService>();
        _service = new DuplicateFinderService(_logger);

        _testDirectory = Path.Combine(Path.GetTempPath(), $"Benchmark_Duplicates_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

        var filesPerGroup = TotalFiles / DuplicateGroupCount;

        // Create duplicate groups
        for (int g = 0; g < DuplicateGroupCount; g++)
        {
            var content = $"Duplicate content for group {g} - {new string('X', 1000)}";
            for (int f = 0; f < filesPerGroup; f++)
            {
                var filePath = Path.Combine(_testDirectory, $"group_{g:D3}_file_{f:D3}.txt");
                File.WriteAllText(filePath, content);
            }
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }

    [Benchmark(Description = "Find duplicates by name")]
    public async Task<List<DuplicateGroup>> FindByNameAsync()
    {
        var options = new DuplicateScanOptions
        {
            CompareMode = DuplicateCompareMode.NameOnly,
            IncludeSubfolders = false
        };
        return await _service.FindDuplicatesAsync(new[] { _testDirectory }, options);
    }

    [Benchmark(Description = "Find duplicates by hash (MD5)")]
    public async Task<List<DuplicateGroup>> FindByHashMD5Async()
    {
        var options = new DuplicateScanOptions
        {
            CompareMode = DuplicateCompareMode.ContentHash,
            HashAlgorithm = HashAlgorithmType.MD5,
            IncludeSubfolders = false
        };
        return await _service.FindDuplicatesAsync(new[] { _testDirectory }, options);
    }

    [Benchmark(Description = "Find duplicates by hash (SHA256)")]
    public async Task<List<DuplicateGroup>> FindByHashSHA256Async()
    {
        var options = new DuplicateScanOptions
        {
            CompareMode = DuplicateCompareMode.ContentHash,
            HashAlgorithm = HashAlgorithmType.SHA256,
            IncludeSubfolders = false
        };
        return await _service.FindDuplicatesAsync(new[] { _testDirectory }, options);
    }

    [Benchmark(Description = "Find duplicates by name and size")]
    public async Task<List<DuplicateGroup>> FindByNameAndSizeAsync()
    {
        var options = new DuplicateScanOptions
        {
            CompareMode = DuplicateCompareMode.NameAndSize,
            IncludeSubfolders = false
        };
        return await _service.FindDuplicatesAsync(new[] { _testDirectory }, options);
    }
}


/// <summary>
/// Benchmarks for SettingsService
/// Measures load/save performance
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
[RankColumn]
public class SettingsServiceBenchmarks
{
    private SettingsService _service = null!;
    private NullAppLogger<SettingsService> _logger = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        _logger = new NullAppLogger<SettingsService>();
        _service = new SettingsService(_logger);
        await _service.LoadAsync();
    }

    [Benchmark(Description = "Load settings")]
    public async Task<AppSettings> LoadSettingsAsync()
    {
        return await _service.LoadAsync();
    }

    [Benchmark(Description = "Save settings")]
    public async Task SaveSettingsAsync()
    {
        await _service.SaveAsync();
    }

    [Benchmark(Description = "Reset to defaults")]
    public async Task ResetToDefaultsAsync()
    {
        await _service.ResetToDefaultsAsync();
    }
}

/// <summary>
/// Benchmarks for PreviewService
/// Measures preview generation performance
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
[RankColumn]
public class PreviewServiceBenchmarks
{
    private PreviewService _service = null!;
    private string _testDirectory = null!;
    private string _smallTextFile = null!;
    private string _largeTextFile = null!;
    private NullAppLogger<PreviewService> _logger = null!;

    [GlobalSetup]
    public void Setup()
    {
        _logger = new NullAppLogger<PreviewService>();
        _service = new PreviewService(_logger);

        _testDirectory = Path.Combine(Path.GetTempPath(), $"Benchmark_Preview_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDirectory);

        // Create small text file
        _smallTextFile = Path.Combine(_testDirectory, "small.txt");
        File.WriteAllText(_smallTextFile, string.Join("\n", Enumerable.Range(1, 100).Select(i => $"Line {i}")));

        // Create large text file
        _largeTextFile = Path.Combine(_testDirectory, "large.txt");
        File.WriteAllText(_largeTextFile, string.Join("\n", Enumerable.Range(1, 10000).Select(i => $"Line {i} with some content")));

        // Create test subdirectory with files
        var subDir = Path.Combine(_testDirectory, "subdir");
        Directory.CreateDirectory(subDir);
        for (int i = 0; i < 50; i++)
        {
            File.WriteAllText(Path.Combine(subDir, $"file_{i}.txt"), $"Content {i}");
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testDirectory))
            Directory.Delete(_testDirectory, true);
    }

    [Benchmark(Description = "Preview small text file")]
    public async Task<PreviewContent> PreviewSmallTextAsync()
    {
        return await _service.GeneratePreviewAsync(_smallTextFile);
    }

    [Benchmark(Description = "Preview large text file")]
    public async Task<PreviewContent> PreviewLargeTextAsync()
    {
        return await _service.GeneratePreviewAsync(_largeTextFile);
    }

    [Benchmark(Description = "Preview directory")]
    public async Task<PreviewContent> PreviewDirectoryAsync()
    {
        return await _service.GenerateDirectoryPreviewAsync(Path.Combine(_testDirectory, "subdir"));
    }
}

/// <summary>
/// xUnit tests to run benchmarks
/// Run with: dotnet test --filter "FullyQualifiedName~BenchmarkTests" -c Release
/// </summary>
public class BenchmarkTests
{
    /// <summary>
    /// Runs all benchmarks and generates reports
    /// WARNING: This test takes several minutes to complete!
    /// Run in Release mode for accurate results: dotnet test -c Release
    /// </summary>
    [Fact]
    public void RunAllBenchmarks()
    {
        var config = new BenchmarkConfig();

        var summaries = new List<Summary>
        {
            BenchmarkRunner.Run<AsyncFileSystemServiceBenchmarks>(config),
            BenchmarkRunner.Run<FileTransferServiceBenchmarks>(config),
            BenchmarkRunner.Run<DuplicateFinderServiceBenchmarks>(config),
            BenchmarkRunner.Run<SettingsServiceBenchmarks>(config),
            BenchmarkRunner.Run<PreviewServiceBenchmarks>(config)
        };

        // Verify all benchmarks completed
        foreach (var summary in summaries)
        {
            Assert.NotNull(summary);
            Assert.True(summary.Reports.Length > 0, $"No reports for {summary.Title}");
        }
    }

    /// <summary>
    /// Run only AsyncFileSystemService benchmarks
    /// </summary>
    [Fact]
    public void RunFileSystemBenchmarks()
    {
        var summary = BenchmarkRunner.Run<AsyncFileSystemServiceBenchmarks>(new BenchmarkConfig());
        Assert.NotNull(summary);
    }

    /// <summary>
    /// Run only FileTransferService benchmarks
    /// </summary>
    [Fact]
    public void RunFileTransferBenchmarks()
    {
        var summary = BenchmarkRunner.Run<FileTransferServiceBenchmarks>(new BenchmarkConfig());
        Assert.NotNull(summary);
    }

    /// <summary>
    /// Run only DuplicateFinderService benchmarks
    /// </summary>
    [Fact]
    public void RunDuplicateFinderBenchmarks()
    {
        var summary = BenchmarkRunner.Run<DuplicateFinderServiceBenchmarks>(new BenchmarkConfig());
        Assert.NotNull(summary);
    }

    
    /// <summary>
    /// Run only SettingsService benchmarks
    /// </summary>
    [Fact]
    public void RunSettingsBenchmarks()
    {
        var summary = BenchmarkRunner.Run<SettingsServiceBenchmarks>(new BenchmarkConfig());
        Assert.NotNull(summary);
    }

    /// <summary>
    /// Run only PreviewService benchmarks
    /// </summary>
    [Fact]
    public void RunPreviewBenchmarks()
    {
        var summary = BenchmarkRunner.Run<PreviewServiceBenchmarks>(new BenchmarkConfig());
        Assert.NotNull(summary);
    }
}