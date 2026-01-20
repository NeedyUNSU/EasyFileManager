using System.Security.Cryptography;
using System.Text;

namespace EasyFileManager.Tests.Helpers;

/// <summary>
/// Helper class for creating test file system structures
/// </summary>
public class TestFileSystemHelper : IDisposable
{
    public string RootPath { get; }
    private readonly List<string> _createdPaths = new();
    
    public TestFileSystemHelper()
    {
        RootPath = Path.Combine(Path.GetTempPath(), $"EasyFileManager_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(RootPath);
        _createdPaths.Add(RootPath);
    }

    /// <summary>
    /// Creates a directory relative to root
    /// </summary>
    public string CreateDirectory(string relativePath)
    {
        var fullPath = Path.Combine(RootPath, relativePath);
        Directory.CreateDirectory(fullPath);
        _createdPaths.Add(fullPath);
        return fullPath;
    }

    /// <summary>
    /// Creates a file with specified content
    /// </summary>
    public string CreateFile(string relativePath, string content = "")
    {
        var fullPath = Path.Combine(RootPath, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);
        
        File.WriteAllText(fullPath, content);
        _createdPaths.Add(fullPath);
        return fullPath;
    }

    /// <summary>
    /// Creates a file with specific size (for performance tests)
    /// </summary>
    public string CreateFile(string relativePath, long sizeInBytes)
    {
        var fullPath = Path.Combine(RootPath, relativePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        using var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write);
        fs.SetLength(sizeInBytes);
        
        // Write some random data to make it realistic
        var buffer = new byte[Math.Min(sizeInBytes, 8192)];
        RandomNumberGenerator.Fill(buffer);
        fs.Write(buffer, 0, buffer.Length);

        _createdPaths.Add(fullPath);
        return fullPath;
    }

    /// <summary>
    /// Creates multiple files with identical content (for duplicate detection tests)
    /// </summary>
    public List<string> CreateDuplicateFiles(string content, params string[] relativePaths)
    {
        var files = new List<string>();
        foreach (var relativePath in relativePaths)
        {
            files.Add(CreateFile(relativePath, content));
        }
        return files;
    }

    /// <summary>
    /// Creates a complex directory structure for testing
    /// </summary>
    public void CreateComplexStructure(int directories, int filesPerDirectory, int fileSizeBytes = 1024)
    {
        for (int d = 0; d < directories; d++)
        {
            var dirPath = $"Dir_{d:D4}";
            CreateDirectory(dirPath);
            
            for (int f = 0; f < filesPerDirectory; f++)
            {
                var fileName = $"{dirPath}/File_{f:D4}.txt";
                CreateFile(fileName, fileSizeBytes);
            }
        }
    }

    /// <summary>
    /// Creates nested directory structure
    /// </summary>
    public void CreateNestedStructure(int depth, int filesPerLevel = 2)
    {
        var currentPath = "";
        for (int level = 0; level < depth; level++)
        {
            currentPath = Path.Combine(currentPath, $"Level_{level}");
            CreateDirectory(currentPath);
            
            for (int f = 0; f < filesPerLevel; f++)
            {
                CreateFile(Path.Combine(currentPath, $"File_{f}.txt"), $"Content at level {level}, file {f}");
            }
        }
    }

    /// <summary>
    /// Creates a ZIP archive with test files
    /// </summary>
    public string CreateTestArchive(string archiveName, int fileCount = 5)
    {
        var archivePath = Path.Combine(RootPath, archiveName);
        var tempDir = CreateDirectory($"_temp_for_archive_{Guid.NewGuid():N}");
        
        for (int i = 0; i < fileCount; i++)
        {
            var filePath = Path.Combine(tempDir, $"ArchiveFile_{i}.txt");
            File.WriteAllText(filePath, $"Archive content for file {i}");
        }
        
        System.IO.Compression.ZipFile.CreateFromDirectory(tempDir, archivePath);
        _createdPaths.Add(archivePath);
        
        // Cleanup temp dir
        Directory.Delete(tempDir, true);
        
        return archivePath;
    }

    /// <summary>
    /// Gets the full path for a relative path
    /// </summary>
    public string GetFullPath(string relativePath) => Path.Combine(RootPath, relativePath);

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }
}

/// <summary>
/// Stopwatch wrapper for performance measurements
/// </summary>
public class PerformanceTimer : IDisposable
{
    private readonly System.Diagnostics.Stopwatch _stopwatch;
    private readonly string _operationName;
    private readonly Action<string, TimeSpan>? _onComplete;

    public PerformanceTimer(string operationName, Action<string, TimeSpan>? onComplete = null)
    {
        _operationName = operationName;
        _onComplete = onComplete;
        _stopwatch = System.Diagnostics.Stopwatch.StartNew();
    }

    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public void Dispose()
    {
        _stopwatch.Stop();
        _onComplete?.Invoke(_operationName, _stopwatch.Elapsed);
    }
}
