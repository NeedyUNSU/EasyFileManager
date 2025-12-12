using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;
using EasyFileManager.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Windows;
using static System.Net.WebRequestMethods;

namespace EasyFileManager.WPF.ViewModels;

public partial class DuplicateResultsViewModel : ViewModelBase
{
    private readonly DuplicateFinderService _duplicateFinderService;
    private readonly FilePreviewService _previewService;
    private readonly IAppLogger<DuplicateResultsViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<DuplicateGroupViewModel> _groups = new();

    [ObservableProperty]
    private int _totalGroups;

    [ObservableProperty]
    private int _totalDuplicateFiles;

    [ObservableProperty]
    private long _totalWastedSpace;

    [ObservableProperty]
    private string _totalWastedSpaceFormatted = "0 B";

    [ObservableProperty]
    private int _selectedFilesCount;

    [ObservableProperty]
    private long _selectedFilesSize;

    [ObservableProperty]
    private string _selectedFilesSizeFormatted = "0 B";

    [ObservableProperty]
    private DuplicateFileViewModel? _selectedFile;

    public DuplicateResultsViewModel(
        List<DuplicateGroup> duplicateGroups,
        DuplicateFinderService duplicateFinderService,
        FilePreviewService filePreviewService,
        IAppLogger<DuplicateResultsViewModel> logger)
    {
        _duplicateFinderService = duplicateFinderService ?? throw new ArgumentNullException(nameof(duplicateFinderService));
        _previewService = filePreviewService ?? throw new ArgumentNullException(nameof(filePreviewService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        LoadGroups(duplicateGroups, filePreviewService);
        CalculateStatistics();
    }

    private void LoadGroups(List<DuplicateGroup> duplicateGroups, FilePreviewService? previewService)
    {
        Groups.Clear();

        foreach (var group in duplicateGroups.OrderByDescending(g => g.TotalWastedSpace))
        {
            var groupVm = new DuplicateGroupViewModel(group, this, previewService);
            Groups.Add(groupVm);
        }

        _logger.LogDebug("Loaded {Count} duplicate groups", Groups.Count);
    }

    private void CalculateStatistics()
    {
        TotalGroups = Groups.Count;
        TotalDuplicateFiles = Groups.Sum(g => g.FileCount);
        TotalWastedSpace = Groups.Sum(g => g.TotalWastedSpace);
        TotalWastedSpaceFormatted = FormatBytes(TotalWastedSpace);

        _logger.LogInformation("Statistics: {Groups} groups, {Files} files, {Space} wasted",
            TotalGroups, TotalDuplicateFiles, TotalWastedSpaceFormatted);
    }

    public void UpdateSelectionStatistics()
    {
        var selectedFiles = Groups.SelectMany(g => g.Files.Where(f => f.IsSelected)).ToList();
        SelectedFilesCount = selectedFiles.Count;
        SelectedFilesSize = selectedFiles.Sum(f => f.Size);
        SelectedFilesSizeFormatted = FormatBytes(SelectedFilesSize);
    }

    [RelayCommand]
    private void SelectAllDuplicates()
    {
        foreach (var group in Groups)
        {
            group.SelectAllExceptFirst();
        }
        UpdateSelectionStatistics();
        _logger.LogDebug("Selected all duplicates except first in each group");
    }

    [RelayCommand]
    private void SelectAllExceptNewest()
    {
        foreach (var group in Groups)
        {
            group.SelectAllExceptNewest();
        }
        UpdateSelectionStatistics();
        _logger.LogDebug("Selected all duplicates except newest in each group");
    }

    [RelayCommand]
    private void SelectAllExceptOldest()
    {
        foreach (var group in Groups)
        {
            group.SelectAllExceptOldest();
        }
        UpdateSelectionStatistics();
        _logger.LogDebug("Selected all duplicates except oldest in each group");
    }

    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var group in Groups)
        {
            foreach (var file in group.Files)
            {
                file.IsSelected = false;
            }
        }
        UpdateSelectionStatistics();
        _logger.LogDebug("Deselected all files");
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        var selectedFiles = Groups.SelectMany(g => g.Files.Where(f => f.IsSelected)).ToList();

        if (selectedFiles.Count == 0)
        {
            MessageBox.Show(
                "No files selected for deletion.",
                "Nothing Selected",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to delete {selectedFiles.Count} file(s)?\n\n" +
            $"Total size: {FormatBytes(selectedFiles.Sum(f => f.Size))}\n\n" +
            "This action cannot be undone!",
            "Confirm Deletion",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        _logger.LogInformation("Deleting {Count} selected duplicate files", selectedFiles.Count);

        try
        {
            var duplicateFiles = selectedFiles.Select(f => new DuplicateFile
            {
                FullPath = f.FullPath,
                FileName = f.FileName,
                Size = f.Size,
                LastModified = f.LastModified,
                Hash = f.Hash
            });

            await _duplicateFinderService.DeleteDuplicatesAsync(duplicateFiles);

            // Remove deleted files from groups
            foreach (var group in Groups.ToList())
            {
                var filesToRemove = group.Files.Where(f => f.IsSelected).ToList();
                foreach (var file in filesToRemove)
                {
                    group.Files.Remove(file);
                }

                // Remove group if only 1 file left (no longer duplicates)
                if (group.Files.Count <= 1)
                {
                    Groups.Remove(group);
                }
                else
                {
                    group.UpdateStatistics();
                }
            }

            CalculateStatistics();
            UpdateSelectionStatistics();

            MessageBox.Show(
                $"Successfully deleted {selectedFiles.Count} file(s).",
                "Deletion Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete selected files");
            MessageBox.Show(
                $"Failed to delete some files:\n{ex.Message}",
                "Deletion Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}

/// <summary>
/// ViewModel for a single duplicate group
/// </summary>
public partial class DuplicateGroupViewModel : ViewModelBase
{
    private readonly DuplicateResultsViewModel _parent;
    private readonly FilePreviewService? _previewService;

    [ObservableProperty]
    private ObservableCollection<DuplicateFileViewModel> _files = new();

    [ObservableProperty]
    private int _fileCount;

    [ObservableProperty]
    private long _size;

    [ObservableProperty]
    private string _sizeFormatted = "0 B";

    [ObservableProperty]
    private long _totalWastedSpace;

    [ObservableProperty]
    private string _totalWastedSpaceFormatted = "0 B";

    [ObservableProperty]
    private bool _isExpanded = true;

    public DuplicateGroupViewModel(DuplicateGroup group, DuplicateResultsViewModel parent, FilePreviewService? previewService = null)
    {
        _parent = parent;
        _previewService = previewService;

        foreach (var file in group.Files)
        {
            var fileVm = new DuplicateFileViewModel(file, this, previewService);
            Files.Add(fileVm);
        }

        UpdateStatistics();
    }

    public void UpdateStatistics()
    {
        FileCount = Files.Count;
        Size = Files.FirstOrDefault()?.Size ?? 0;
        SizeFormatted = FormatBytes(Size);
        TotalWastedSpace = Size * (FileCount - 1);
        TotalWastedSpaceFormatted = FormatBytes(TotalWastedSpace);
    }

    public void SelectAllExceptFirst()
    {
        foreach (var file in Files.Skip(1))
        {
            file.IsSelected = true;
        }
    }

    public void SelectAllExceptNewest()
    {
        var newest = Files.OrderByDescending(f => f.LastModified).FirstOrDefault();
        foreach (var file in Files)
        {
            file.IsSelected = file != newest;
        }
    }

    public void SelectAllExceptOldest()
    {
        var oldest = Files.OrderBy(f => f.LastModified).FirstOrDefault();
        foreach (var file in Files)
        {
            file.IsSelected = file != oldest;
        }
    }

    public void NotifySelectionChanged()
    {
        _parent.UpdateSelectionStatistics();
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}

/// <summary>
/// ViewModel for a single duplicate file
/// </summary>
public partial class DuplicateFileViewModel : ViewModelBase
{
    private readonly DuplicateGroupViewModel _parent;
    private readonly FilePreviewService? _previewService;

    [ObservableProperty]
    private string _fullPath = string.Empty;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _directory = string.Empty;

    [ObservableProperty]
    private long _size;

    [ObservableProperty]
    private string _sizeFormatted = "0 B";

    [ObservableProperty]
    private DateTime _lastModified;

    [ObservableProperty]
    private string _lastModifiedFormatted = string.Empty;

    [ObservableProperty]
    private string _hash = string.Empty;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isPreviewLoading;

    [ObservableProperty]
    private FilePreviewType _previewType;

    [ObservableProperty]
    private ImagePreviewData? _imagePreview;

    [ObservableProperty]
    private TextPreviewData? _textPreview;

    [ObservableProperty]
    private FileInfoData? _fileInfo;


    public DuplicateFileViewModel(DuplicateFile file, DuplicateGroupViewModel parent, FilePreviewService? previewService = null)
    {
        _parent = parent;
        _previewService = previewService;

        System.Diagnostics.Debug.WriteLine($"@@@ DuplicateFileViewModel CONSTRUCTOR for {file.FileName}");
        System.Diagnostics.Debug.WriteLine($"@@@ PreviewService is null: {_previewService == null}");

        FullPath = file.FullPath;
        FileName = file.FileName;
        Directory = Path.GetDirectoryName(file.FullPath) ?? string.Empty;
        Size = file.Size;
        SizeFormatted = FormatBytes(Size);
        LastModified = file.LastModified;
        LastModifiedFormatted = file.LastModified.ToString("yyyy-MM-dd HH:mm:ss");
        Hash = file.Hash;
        IsSelected = file.IsSelected;

        // Determine preview type
        if (_previewService != null)
        {
            PreviewType = _previewService.GetPreviewType(FullPath);
            System.Diagnostics.Debug.WriteLine($"@@@ PreviewType detected: {PreviewType}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"@@@ WARNING: PreviewService is NULL - cannot detect PreviewType!");
            PreviewType = FilePreviewType.Generic;
        }
    }

    [RelayCommand]
    private async Task LoadPreviewAsync()
    {
        System.Diagnostics.Debug.WriteLine($">>> LoadPreviewAsync called for: {FileName}");
        System.Diagnostics.Debug.WriteLine($">>> PreviewService is null: {_previewService == null}");
        System.Diagnostics.Debug.WriteLine($">>> PreviewType: {PreviewType}");

        if (_previewService == null || IsPreviewLoading)
        {
            System.Diagnostics.Debug.WriteLine(">>> EXIT: PreviewService null or already loading");
            return;
        }

        IsPreviewLoading = true;

        try
        {
            // Always load file info
            FileInfo = _previewService.GetFileInfo(FullPath);
            System.Diagnostics.Debug.WriteLine($">>> FileInfo loaded: {FileInfo != null}");

            // Load type-specific preview
            switch (PreviewType)
            {
                case FilePreviewType.Image:
                    System.Diagnostics.Debug.WriteLine(">>> Loading image preview...");
                    ImagePreview = await _previewService.LoadImagePreviewAsync(FullPath);
                    System.Diagnostics.Debug.WriteLine($">>> ImagePreview loaded: {ImagePreview != null}");
                    if (ImagePreview != null)
                    {
                        System.Diagnostics.Debug.WriteLine($">>> Image resolution: {ImagePreview.Resolution}");
                    }
                    break;

                case FilePreviewType.Text:
                    System.Diagnostics.Debug.WriteLine(">>> Loading text preview...");
                    TextPreview = await _previewService.LoadTextPreviewAsync(FullPath);
                    System.Diagnostics.Debug.WriteLine($">>> TextPreview loaded: {TextPreview != null}");
                    if (TextPreview != null)
                    {
                        System.Diagnostics.Debug.WriteLine($">>> Text lines: {TextPreview.LineCount}");
                    }
                    break;

                case FilePreviewType.Audio:
                case FilePreviewType.Video:
                case FilePreviewType.Generic:
                    System.Diagnostics.Debug.WriteLine(">>> Generic preview (file info only)");
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($">>> ERROR loading preview: {ex.Message}");
            MessageBox.Show(
                $"Failed to load preview:\n{ex.Message}",
                "Preview Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            IsPreviewLoading = false;
            System.Diagnostics.Debug.WriteLine(">>> LoadPreviewAsync completed");
        }
    }

    partial void OnIsSelectedChanged(bool value)
    {
        _parent.NotifySelectionChanged();
    }

    [RelayCommand]
    private void OpenLocation()
    {
        try
        {
            Process.Start("explorer.exe", $"/select,\"{FullPath}\"");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to open location:\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void CopyPath()
    {
        try
        {
            Clipboard.SetText(FullPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Failed to copy path:\n{ex.Message}",
                "Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }
}