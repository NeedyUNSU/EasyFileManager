using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace EasyFileManager.Core.Services;

public class DuplicateFinderService
{
    private readonly IAppLogger<DuplicateFinderService> _logger;

    public DuplicateFinderService(IAppLogger<DuplicateFinderService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Find duplicate files in specified directories
    /// </summary>
    public async Task<List<DuplicateGroup>> FindDuplicatesAsync(
        IEnumerable<string> searchPaths,
        DuplicateScanOptions options,
        IProgress<DuplicateScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting duplicate scan with mode: {Mode}", options.CompareMode);

        return await Task.Run(() =>
        {
            // Step 1: Collect all files
            var allFiles = new List<FileInfo>();
            foreach (var searchPath in searchPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (Directory.Exists(searchPath))
                {
                    var searchOption = options.IncludeSubfolders
                        ? SearchOption.AllDirectories
                        : SearchOption.TopDirectoryOnly;

                    var files = Directory.GetFiles(searchPath, "*", searchOption)
                        .Select(f => new FileInfo(f))
                        .Where(f => !options.IgnoreEmptyFiles || f.Length > 0)
                        .Where(f => f.Length >= options.MinimumFileSize);

                    allFiles.AddRange(files);
                }
            }

            _logger.LogDebug("Found {Count} files to analyze", allFiles.Count);

            var totalFiles = allFiles.Count;
            var totalBytes = allFiles.Sum(f => f.Length);
            var processedFiles = 0;
            var processedBytes = 0L;

            // Step 2: Group files based on compare mode
            Dictionary<string, List<FileInfo>> groups;

            switch (options.CompareMode)
            {
                case DuplicateCompareMode.NameOnly:
                    groups = GroupByName(allFiles);
                    break;

                case DuplicateCompareMode.NameAndSize:
                    groups = GroupByNameAndSize(allFiles);
                    break;

                case DuplicateCompareMode.SizeOnly:
                    groups = GroupBySize(allFiles);
                    break;

                case DuplicateCompareMode.ContentHash:
                    // First group by size (optimization)
                    var sizeGroups = GroupBySize(allFiles);

                    // Then hash only files with same size
                    groups = new Dictionary<string, List<FileInfo>>();

                    foreach (var sizeGroup in sizeGroups.Values.Where(g => g.Count > 1))
                    {
                        foreach (var file in sizeGroup)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            progress?.Report(new DuplicateScanProgress
                            {
                                CurrentFile = file.Name,
                                ProcessedFiles = processedFiles,
                                TotalFiles = totalFiles,
                                ProcessedBytes = processedBytes,
                                TotalBytes = totalBytes,
                                Status = "Hashing files..."
                            });

                            var hash = ComputeFileHash(file.FullName, options.HashAlgorithm);

                            if (!groups.ContainsKey(hash))
                            {
                                groups[hash] = new List<FileInfo>();
                            }

                            groups[hash].Add(file);

                            processedFiles++;
                            processedBytes += file.Length;
                        }
                    }
                    break;

                default:
                    throw new NotSupportedException($"Compare mode {options.CompareMode} not supported");
            }

            // Step 3: Convert to DuplicateGroups (only groups with 2+ files)
            var duplicateGroups = new List<DuplicateGroup>();

            foreach (var kvp in groups.Where(g => g.Value.Count > 1))
            {
                var group = new DuplicateGroup
                {
                    Key = kvp.Key,
                    Size = kvp.Value.First().Length
                };

                foreach (var file in kvp.Value)
                {
                    group.Files.Add(new DuplicateFile
                    {
                        FullPath = file.FullName,
                        FileName = file.Name,
                        Size = file.Length,
                        LastModified = file.LastWriteTime,
                        Hash = kvp.Key
                    });
                }

                duplicateGroups.Add(group);
            }

            _logger.LogInformation("Found {Count} duplicate groups, total wasted space: {Space} MB",
                duplicateGroups.Count,
                duplicateGroups.Sum(g => g.TotalWastedSpace) / 1024.0 / 1024.0);

            progress?.Report(new DuplicateScanProgress
            {
                CurrentFile = "",
                ProcessedFiles = totalFiles,
                TotalFiles = totalFiles,
                ProcessedBytes = totalBytes,
                TotalBytes = totalBytes,
                DuplicateGroupsFound = duplicateGroups.Count,
                Status = "Scan complete"
            });

            return duplicateGroups;

        }, cancellationToken);
    }

    private Dictionary<string, List<FileInfo>> GroupByName(List<FileInfo> files)
    {
        return files.GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    private Dictionary<string, List<FileInfo>> GroupByNameAndSize(List<FileInfo> files)
    {
        return files.GroupBy(f => $"{f.Name}_{f.Length}")
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    private Dictionary<string, List<FileInfo>> GroupBySize(List<FileInfo> files)
    {
        return files.GroupBy(f => f.Length.ToString())
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    private string ComputeFileHash(string filePath, HashAlgorithmType algorithmType)
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            byte[] hashBytes;

            if (algorithmType == HashAlgorithmType.SHA256)
            {

                using var hashAlgorithmSHA256 = SHA256.Create();
                hashBytes = hashAlgorithmSHA256.ComputeHash(stream);
            }
            else
            {
                using var hashAlgorithmMD5 = MD5.Create();
                hashBytes = hashAlgorithmMD5.ComputeHash(stream);
            }

                
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex.ToString(), "Failed to hash file: {Path}", filePath);
            return $"ERROR_{Guid.NewGuid()}"; // Unique key to avoid false duplicates
        }
    }

    /// <summary>
    /// Delete selected duplicate files
    /// </summary>
    public async Task DeleteDuplicatesAsync(
        IEnumerable<DuplicateFile> filesToDelete,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var files = filesToDelete.ToList();
        var totalFiles = files.Count;
        var deletedFiles = 0;

        await Task.Run(() =>
        {
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    File.Delete(file.FullPath);
                    _logger.LogDebug("Deleted duplicate: {Path}", file.FullPath);
                    deletedFiles++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete: {Path}", file.FullPath);
                }

                progress?.Report((int)((double)deletedFiles / totalFiles * 100));
            }
        }, cancellationToken);

        _logger.LogInformation("Deleted {Count} duplicate files", deletedFiles);
    }
}