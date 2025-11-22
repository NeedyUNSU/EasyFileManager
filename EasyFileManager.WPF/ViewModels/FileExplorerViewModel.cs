using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;
using EasyFileManager.Core.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Data;
using static MaterialDesignThemes.Wpf.Theme.ToolBar;

namespace EasyFileManager.WPF.ViewModels;

/// <summary>
/// ViewModel for single file explorer panel (left or right)
/// </summary>
public partial class FileExplorerViewModel : ViewModelBase
{
    private readonly IFileSystemService _fileSystemService;
    private readonly IAppLogger<FileExplorerViewModel> _logger;
    private readonly IFileTransferService _fileTransferService;

    // ====== ObservableProperties (auto-generated z source generators) ======

    [ObservableProperty]
    private string _currentPath = string.Empty;

    public ObservableCollection<FileSystemEntry> Items =>
        string.IsNullOrWhiteSpace(FilterText) ? _allItems : _filteredItems;

    [ObservableProperty]
    private FileSystemEntry? _selectedItem;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private int _totalItems;

    [ObservableProperty]
    private string _totalSize = "0 B";

    [ObservableProperty]
    private string _sortColumn = "Name";

    [ObservableProperty]
    private ListSortDirection _sortDirection = ListSortDirection.Ascending;

    [ObservableProperty]
    private ObservableCollection<DriveInfoModel> _availableDrives = new();

    [ObservableProperty]
    private DriveInfoModel? _selectedDrive;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private bool _isFilterVisible;

    [ObservableProperty]
    private ObservableCollection<FileSystemEntry> _allItems = new();

    [ObservableProperty]
    private ObservableCollection<FileSystemEntry> _filteredItems = new();

    [ObservableProperty]
    private ObservableCollection<BreadcrumbItem> _breadcrumbs = new();

    [ObservableProperty]
    private bool _isPathEditable;

    // ====== Constructor with DI ======

    public FileExplorerViewModel(
        IFileSystemService fileSystemService,
        IFileTransferService fileTransferService,
        IAppLogger<FileExplorerViewModel> logger)
    {
        _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
        _fileTransferService = fileTransferService ?? throw new ArgumentNullException(nameof(fileTransferService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Domyślna ścieżka
        CurrentPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _ = LoadDrivesAsync();
        
    }

    // ====== Commands (auto-generated z [RelayCommand]) ======

    [RelayCommand]
    private async Task LoadDirectoryAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentPath))
            return;

        try
        {
            IsLoading = true;
            StatusMessage = $"Loading {CurrentPath}...";

            _logger.LogInformation("Loading directory: {Path}", CurrentPath);

            var directory = await _fileSystemService.LoadDirectoryAsync(CurrentPath, default);

            AllItems.Clear();
            var sortedItems = directory.Children
                .OrderByDescending(x => x is DirectoryEntry) // Foldery na górze
                .ThenBy(x => x.Name); // Potem alfabetycznie

            foreach (var entry in sortedItems)
            {
                AllItems.Add(entry);
            }

            ApplyFilter();

            TotalItems = Items.Count;
            TotalSize = CalculateTotalSize(AllItems);
            StatusMessage = $"Loaded {TotalItems} items";

            _logger.LogDebug("Successfully loaded {Count} items from {Path}", TotalItems, CurrentPath);
        }
        catch (DirectoryNotFoundException ex)
        {
            _logger.LogWarning(ex.ToString(), "Directory not found: {Path}", CurrentPath);
            StatusMessage = $"Directory not found: {CurrentPath}";
            MessageBox.Show($"Directory not found:\n{CurrentPath}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex.ToString(), "Access denied: {Path}", CurrentPath);
            StatusMessage = $"Access denied: {CurrentPath}";
            MessageBox.Show($"Access denied:\n{CurrentPath}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load directory: {Path}", CurrentPath);
            StatusMessage = "Error loading directory";
            MessageBox.Show($"Error loading directory:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task NavigateToAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        CurrentPath = path;
        await LoadDirectoryAsync();
    }

    [RelayCommand]
    private async Task NavigateUpAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentPath))
            return;

        var parent = Directory.GetParent(CurrentPath);
        if (parent != null)
        {
            await NavigateToAsync(parent.FullName);
        }
    }

    [RelayCommand]
    private async Task OpenItemAsync(FileSystemEntry? item)
    {
        if (item == null)
            return;

        if (item is DirectoryEntry)
        {
            await NavigateToAsync(item.FullPath);
        }
        else if (item is FileEntry)
        {
            try
            {
                _logger.LogInformation("Opening file: {Path}", item.FullPath);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = item.FullPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open file: {Path}", item.FullPath);
                MessageBox.Show($"Failed to open file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    [RelayCommand]
    private void Sort(string columnName)
    {
        if (string.IsNullOrEmpty(columnName))
            return;

        // Toggle direction if same column
        if (_sortColumn == columnName)
        {
            _sortDirection = _sortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        }
        else
        {
            _sortColumn = columnName;
            _sortDirection = ListSortDirection.Ascending;
        }

        ApplySort();

        _logger.LogDebug("Sorted by {Column} {Direction}", _sortColumn, _sortDirection);
    }

    [RelayCommand]
    private void SortBy(string? columnName)
    {
        if (string.IsNullOrEmpty(columnName))
            return;

        _logger.LogDebug("Sorting by column: {Column}", columnName);

        // Toggle direction if same column
        if (SortColumn == columnName)
        {
            SortDirection = SortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
        }
        else
        {
            SortColumn = columnName;
            SortDirection = ListSortDirection.Ascending;
        }

        ApplySorting();
    }

    [RelayCommand]
    private void CopyPath(FileSystemEntry? item)
    {
        if (item == null)
            return;

        try
        {
            System.Windows.Clipboard.SetText(item.FullPath);
            StatusMessage = $"Copied path: {item.FullPath}";
            _logger.LogDebug("Copied path to clipboard: {Path}", item.FullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy path to clipboard");
            MessageBox.Show($"Failed to copy path:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void CopyItem(FileSystemEntry? item)
    {
        if (item == null)
            return;

        try
        {
            var files = new System.Collections.Specialized.StringCollection { item.FullPath };
            System.Windows.Clipboard.SetFileDropList(files);
            StatusMessage = $"Copied to clipboard: {item.Name}";
            _logger.LogDebug("Copied file to clipboard: {Path}", item.FullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to copy file to clipboard");
            MessageBox.Show($"Failed to copy file:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void DeleteItem(FileSystemEntry? item)
    {
        if (item == null)
            return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete:\n{item.Name}?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            if (item is DirectoryEntry)
            {
                Directory.Delete(item.FullPath, recursive: true);
                _logger.LogInformation("Deleted directory: {Path}", item.FullPath);
            }
            else
            {
                File.Delete(item.FullPath);
                _logger.LogInformation("Deleted file: {Path}", item.FullPath);
            }

            // Odśwież widok
            Items.Remove(item);
            TotalItems = Items.Count;
            TotalSize = CalculateTotalSize(Items);
            StatusMessage = $"Deleted: {item.Name}";
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied when deleting: {Path}", item.FullPath);
            MessageBox.Show($"Access denied:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete: {Path}", item.FullPath);
            MessageBox.Show($"Failed to delete:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void ShowProperties(FileSystemEntry? item)
    {
        if (item == null)
            return;

        try
        {
            // Wywołaj system properties dialog (Windows shell)
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{item.FullPath}\"",
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(processInfo);

            _logger.LogDebug("Opened properties for: {Path}", item.FullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open properties for: {Path}", item.FullPath);
            MessageBox.Show($"Failed to open properties:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void RenameItem(FileSystemEntry? item)
    {
        if (item == null)
            return;

        // TODO: To zrobimy w następnym kroku (inline editing lub dialog)
        StatusMessage = $"Rename feature coming soon for: {item.Name}";
        _logger.LogDebug("Rename requested for: {Path}", item.FullPath);
    }

    [RelayCommand]
    private void ShowFilter()
    {
        IsFilterVisible = true;
        _logger.LogDebug("Filter shown");
    }

    [RelayCommand]
    private void HideFilter()
    {
        IsFilterVisible = false;
        FilterText = string.Empty;
        _logger.LogDebug("Filter hidden");
    }

    [RelayCommand]
    private void ClearFilter()
    {
        FilterText = string.Empty;
        _logger.LogDebug("Filter cleared");
    }

    [RelayCommand]
    private void NavigateToBreadcrumb(BreadcrumbItem? item)
    {
        if (item == null)
            return;

        CurrentPath = item.FullPath;
        _ = LoadDirectoryAsync();
    }

    [RelayCommand]
    private void TogglePathEdit()
    {
        IsPathEditable = !IsPathEditable;

        if (!IsPathEditable)
        {
            // Wracamy do breadcrumb - załaduj ścieżkę
            _ = LoadDirectoryAsync();
        }
    }

    // ====== Helper Methods ======

    partial void OnFilterTextChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(FilterText))
        {
            // Brak filtra - pokaż wszystko
            FilteredItems.Clear();
            OnPropertyChanged(nameof(Items));
            TotalItems = AllItems.Count;
            return;
        }

        // Filtruj case-insensitive
        var filtered = AllItems
            .Where(item => item.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase))
            .ToList();

        FilteredItems.Clear();
        foreach (var item in filtered)
        {
            FilteredItems.Add(item);
        }

        OnPropertyChanged(nameof(Items));
        TotalItems = FilteredItems.Count;

        _logger.LogDebug("Filter applied: '{Filter}' - {Count} items matched", FilterText, FilteredItems.Count);
    }

    private async Task LoadDrivesAsync()
    {
        try
        {
            var drives = await _fileSystemService.GetDrivesWithMetadataAsync();

            AvailableDrives.Clear();
            foreach (var drive in drives)
            {
                AvailableDrives.Add(drive);
            }

            string currentPath = CurrentPath;

            // Ustaw aktualny dysk na podstawie CurrentPath
            var currentDriveLetter = Path.GetPathRoot(CurrentPath)?.TrimEnd('\\');
            SelectedDrive = AvailableDrives.FirstOrDefault(d =>
                d.Name.TrimEnd('\\').Equals(currentDriveLetter, StringComparison.OrdinalIgnoreCase));

            _logger.LogDebug("Loaded {Count} drives, selected: {Selected}",
                AvailableDrives.Count, SelectedDrive?.Name);

            CurrentPath = currentPath;
            await LoadDirectoryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load drives");
        }
    }

    partial void OnSelectedDriveChanged(DriveInfoModel? value)
    {
        if (value != null && value.IsReady)
        {
            CurrentPath = value.RootDirectory;
            _ = LoadDirectoryAsync();
        }
    }

    private void ApplySorting()
    {
        var sortedList = SortColumn switch
        {
            "Name" => SortDirection == ListSortDirection.Ascending
                ? Items.OrderBy(x => x is DirectoryEntry ? 0 : 1).ThenBy(x => x.Name).ToList()
                : Items.OrderBy(x => x is DirectoryEntry ? 0 : 1).ThenByDescending(x => x.Name).ToList(),

            "Size" => SortDirection == ListSortDirection.Ascending
                ? Items.OrderBy(x => x is DirectoryEntry ? 0 : 1)
                       .ThenBy(x => x is FileEntry file ? file.Size : 0).ToList()
                : Items.OrderBy(x => x is DirectoryEntry ? 0 : 1)
                       .ThenByDescending(x => x is FileEntry file ? file.Size : 0).ToList(),

            "Type" => SortDirection == ListSortDirection.Ascending
                ? Items.OrderBy(x => x is DirectoryEntry ? 0 : 1)
                       .ThenBy(x => x is DirectoryEntry ? "Folder" : Path.GetExtension(x.Name)).ToList()
                : Items.OrderBy(x => x is DirectoryEntry ? 0 : 1)
                       .ThenByDescending(x => x is DirectoryEntry ? "Folder" : Path.GetExtension(x.Name)).ToList(),

            "LastModified" => SortDirection == ListSortDirection.Ascending
                ? Items.OrderBy(x => x is DirectoryEntry ? 0 : 1).ThenBy(x => x.LastModified).ToList()
                : Items.OrderBy(x => x is DirectoryEntry ? 0 : 1).ThenByDescending(x => x.LastModified).ToList(),

            _ => Items.ToList()
        };

        Items.Clear();
        foreach (var item in sortedList)
        {
            Items.Add(item);
        }

        _logger.LogDebug("Applied sorting: {Column} {Direction}", SortColumn, SortDirection);
    }

    private void ApplySort()
    {
        var view = CollectionViewSource.GetDefaultView(Items);
        view.SortDescriptions.Clear();

        // Zawsze najpierw foldery, potem pliki
        view.SortDescriptions.Add(new SortDescription(
            ".", // Sortuj po typie (Directory vs File)
            ListSortDirection.Descending)); // Directory > File

        // Potem sortuj według wybranej kolumny
        view.SortDescriptions.Add(new SortDescription(_sortColumn, _sortDirection));
    }

    private static string CalculateTotalSize(IEnumerable<FileSystemEntry> items)
    {
        long totalBytes = items.OfType<FileEntry>().Sum(f => f.Size);
        return FormatFileSize(totalBytes);
    }

    private static string FormatFileSize(long bytes)
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

    partial void OnCurrentPathChanged(string value)
    {
        UpdateBreadcrumbs();
        _logger.LogDebug("Current path changed to: {Path}", value);
    }

    private void UpdateBreadcrumbs()
    {
        Breadcrumbs.Clear();
        var items = BreadcrumbItem.FromPath(CurrentPath);
        foreach (var item in items)
        {
            Breadcrumbs.Add(item);
        }
    }
}