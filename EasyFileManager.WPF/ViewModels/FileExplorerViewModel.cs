using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;
using EasyFileManager.Core.Services;
using EasyFileManager.WPF.Views;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Data;
using static MaterialDesignThemes.Wpf.Theme.ToolBar;
using Microsoft.Extensions.DependencyInjection;

namespace EasyFileManager.WPF.ViewModels;

/// <summary>
/// ViewModel for single file explorer panel (left or right)
/// </summary>
public partial class FileExplorerViewModel : ViewModelBase
{
    private readonly IFileSystemService _fileSystemService;
    private readonly IAppLogger<FileExplorerViewModel> _logger;
    private readonly IFileTransferService _fileTransferService;
    private readonly DuplicateFinderService _duplicateFinderService;
    private readonly IArchiveService _archiveService;
    private TabBarViewModel? _tabBarViewModel;

    // ====== ObservableProperties ======

    [ObservableProperty]
    private string _currentPath = string.Empty;

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
    private ObservableCollection<BreadcrumbItem> _breadcrumbs = new();

    [ObservableProperty]
    private bool _isPathEditable;

    [ObservableProperty]
    private TabBarViewModel? _tabBar;

    [ObservableProperty]
    private ObservableCollection<FileSystemEntry> _allItems = new();

    // ICollectionView for filtering/sorting without collection manipulation
    private ListCollectionView? _itemsView;

    public ICollectionView Items
    {
        get
        {
            if (_itemsView == null)
            {
                // Rzutuj na ListCollectionView
                _itemsView = (ListCollectionView)CollectionViewSource.GetDefaultView(AllItems);
                _itemsView.Filter = FilterPredicate;
                UpdateSortDescriptions();
            }
            return _itemsView;
        }
    }

    [ObservableProperty]
    private FileSystemEntry? _selectedItem;

    [ObservableProperty]
    private ObservableCollection<FileSystemEntry> _selectedItems = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private int _totalItems;

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private bool _isFilterVisible;

    // ====== Constructor with DI ======

    public FileExplorerViewModel(
        IFileSystemService fileSystemService,
        IFileTransferService fileTransferService,
        DuplicateFinderService duplicateFinderService,
        IArchiveService archiveService,
        IAppLogger<FileExplorerViewModel> logger)
    {
        _fileSystemService = fileSystemService ?? throw new ArgumentNullException(nameof(fileSystemService));
        _fileTransferService = fileTransferService ?? throw new ArgumentNullException(nameof(fileTransferService));
        _duplicateFinderService = duplicateFinderService ?? throw new ArgumentNullException(nameof(duplicateFinderService));
        _archiveService = archiveService ?? throw new ArgumentNullException(nameof(archiveService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        CurrentPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _ = LoadDrivesAsync();
        
    }

    // ====== Commands (auto-generated [RelayCommand]) ======

    #region AutoRelayCommand

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
            DirectoryEntry directory;

            try
            {
                directory = await _fileSystemService.LoadDirectoryAsync(CurrentPath, default);
            }
            catch (PasswordRequiredException ex)
            {
                _logger.LogDebug("Archive requires password: {Path}", ex.ArchivePath);

                // Show password dialog
                var archiveName = Path.GetFileName(ex.ArchivePath);
                var passwordDialog = new ArchivePasswordDialog(archiveName)
                {
                    Owner = Application.Current.MainWindow
                };

                passwordDialog.ShowDialog();

                if (!passwordDialog.Confirmed)
                {
                    RefreshCurrentPathIfArchive();

                    StatusMessage = "Archive opening cancelled";
                    return;
                }


                // Retry with password
                try
                {
                    var (archivePath, innerPath) = ParseArchivePath(CurrentPath);
                    directory = await _archiveService.LoadArchiveAsync(archivePath, innerPath, passwordDialog.Password);
                }
                catch (InvalidPasswordException)
                {
                    _logger.LogWarning("Invalid password provided for: {Path}", ex.ArchivePath);
                    StatusMessage = "Invalid password";
                    throw;
                }
            }
            catch (InvalidPasswordException ex)
            {
                _logger.LogWarning("Invalid password for: {Path}", ex.ArchivePath);
                //MessageBox.Show(
                //    "Invalid password.",
                //    "Invalid Password",
                //    MessageBoxButton.OK,
                //    MessageBoxImage.Warning);
                RefreshCurrentPathIfArchive();
                StatusMessage = "Invalid password";
                throw;
            }

            AllItems.Clear();
            foreach (var entry in directory.Children)
            {
                AllItems.Add(entry);
            }

            // Refresh view to apply sorting/filtering
            Items.Refresh();

            TotalItems = AllItems.Count;
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
            RefreshCurrentPathIfArchive();
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RefreshCurrentPathIfArchive()
    {
        if (CurrentPath.Contains("::"))
        {
            var index = CurrentPath.IndexOf("::");
            var subPath = CurrentPath.Substring(0, index);
            var subIndex = 0;
            if (subPath.Contains('\\'))
                subIndex = subPath.LastIndexOf('\\');
            if (subPath.Contains('/'))
                subIndex = subPath.LastIndexOf('/');

            CurrentPath = subPath.Substring(0, subIndex);
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

        // Check if we're in an archive
        if (_fileSystemService.IsArchivePath(CurrentPath))
        {
            var (archivePath, innerPath) = ParseArchivePath(CurrentPath);

            if (string.IsNullOrEmpty(innerPath))
            {
                // We're at archive root (e.g., "C:\archive.zip::")
                // Navigate to folder containing the archive
                var archiveDir = Path.GetDirectoryName(archivePath);
                if (!string.IsNullOrEmpty(archiveDir))
                {
                    await NavigateToAsync(archiveDir);
                }
            }
            else
            {
                // We're in a subfolder inside archive (e.g., "C:\archive.zip::folder")
                // Navigate to parent folder inside archive
                var parentInnerPath = Path.GetDirectoryName(innerPath)?.Replace('\\', '/') ?? string.Empty;
                await NavigateToAsync($"{archivePath}::{parentInnerPath}");
            }
        }
        else
        {
            // Regular directory navigation
            var parent = Directory.GetParent(CurrentPath);
            if (parent != null)
            {
                await NavigateToAsync(parent.FullName);
            }
        }
    }

    [RelayCommand]
    private async Task OpenItemAsync(FileSystemEntry? item)
    {
        if (item == null)
            return;

        // Handle ArchiveEntry (navigate inside archive)
        if (item is ArchiveDirectoryEntry archiveDir)
        {
            await NavigateToAsync(archiveDir.VirtualPath);
            return;
        }

        // Handle ArchiveFileEntry (open for preview or external viewer)
        if (item is ArchiveFileEntry archiveFile)
        {
            await extractFromArchiveCommand.ExecuteAsync(item);
            //openItemCommand.Execute(SelectedItem);
            //// For now, just show message - Phase 2 will add preview/extract
            //MessageBox.Show(
            //    $"File inside archive: {archiveFile.Name}\n\nUse Extract (Ctrl+E) to extract this file.",
            //    "Archive File",
            //    MessageBoxButton.OK,
            //    MessageBoxImage.Information);
            return;
        }

        if (item is DirectoryEntry)
        {
            await NavigateToAsync(item.FullPath);
        }
        else if (item is FileEntry fileEntry)
        {
            // Check if file is archive
            if (_fileSystemService.IsArchiveFile(fileEntry.FullPath))
            {
                _logger.LogInformation("Opening archive: {Path}", fileEntry.FullPath);
                // Navigate into archive (virtual path with :: separator)
                await NavigateToAsync($"{fileEntry.FullPath}::");
            }
            else
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
    private async Task DeleteItem(FileSystemEntry? item)
    {
        _logger.LogDebug("DeleteItem called. Param: {Param}, SelectedItems.Count: {Count}, SelectedItem: {Selected}",
        item?.Name ?? "null",
        SelectedItems.Count,
        SelectedItem?.Name ?? "null");

        var itemsToDelete = item != null
            ? new[] { item }
            : SelectedItems.Count > 0
                ? SelectedItems.ToArray()
                : SelectedItem != null
                    ? new[] { SelectedItem }
                    : Array.Empty<FileSystemEntry>();

        if (itemsToDelete.Length == 0)
        {
            _logger.LogWarning("No items selected for delete");
            return;
        }

        var itemText = itemsToDelete.Length == 1
            ? $"'{itemsToDelete[0].Name}'"
            : $"{itemsToDelete.Length} items";

        var dialog = itemsToDelete.Length == 1
            ? new DeleteConfirmDialog(itemsToDelete[0].Name)
            : new DeleteConfirmDialog(itemsToDelete.Select(i => i.Name));

        dialog.Owner = Application.Current.MainWindow;
        dialog.ShowDialog();

        if (!dialog.Confirmed)
            return;

        try
        {
            IsLoading = true;
            StatusMessage = $"Deleting {itemText}...";

            foreach (var itemToDelete in itemsToDelete)
            {
                if (itemToDelete is DirectoryEntry)
                {
                    Directory.Delete(itemToDelete.FullPath, recursive: true);
                }
                else
                {
                    File.Delete(itemToDelete.FullPath);
                }
                _logger.LogInformation("Deleted: {Path}", itemToDelete.FullPath);
            }

            StatusMessage = $"Deleted {itemsToDelete.Length} item(s)";

            // Refresh view
            await LoadDirectoryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete items");
            MessageBox.Show($"Failed to delete:\n{ex.Message}", "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
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
        {
            _logger.LogWarning("No item selected for rename");
            return;
        }

        var dialog = new RenameDialog(item.Name)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true)
            return;

        var newName = dialog.NewName;

        if (string.IsNullOrWhiteSpace(newName) || newName == item.Name)
            return;

        try
        {
            var directory = Path.GetDirectoryName(item.FullPath);
            var newPath = Path.Combine(directory ?? string.Empty, newName);

            if (File.Exists(newPath) || Directory.Exists(newPath))
            {
                MessageBox.Show(
                    $"A file or folder with the name '{newName}' already exists.",
                    "Rename Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (item is DirectoryEntry)
            {
                Directory.Move(item.FullPath, newPath);
            }
            else
            {
                File.Move(item.FullPath, newPath);
            }

            _logger.LogInformation("Renamed {Old} to {New}", item.Name, newName);
            StatusMessage = $"Renamed to {newName}";

            // Refresh view
            _ = LoadDirectoryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to rename {Path}", item.FullPath);
            MessageBox.Show($"Failed to rename:\n{ex.Message}", "Rename Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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

    [RelayCommand]
    private async Task CopyToTargetPanel()
    {
        if (SelectedItem == null)
        {
            _logger.LogWarning("No item selected for copy");
            return;
        }

        var mainWindow = Application.Current.MainWindow;
        if (mainWindow?.DataContext is not MainViewModel mainVm)
        {
            _logger.LogError(new Exception(""), "Cannot get MainViewModel");
            return;
        }

        var targetPanel = mainVm.GetTargetPanel(this);
        var destinationPath = targetPanel.CurrentPath;

        _logger.LogInformation("Copying {Item} to {Destination}", SelectedItem.Name, destinationPath);

        // Progress dialog
        var progressDialog = new FileTransferDialog("Copying")
        {
            Owner = mainWindow
        };

        var progress = new Progress<FileTransferProgress>(p => progressDialog.UpdateProgress(p));

        // Conflict handler
        Func<FileConflictInfo, Task<FileConflictResolution>> conflictResolver = async (conflict) =>
        {
            FileConflictResolution? resolution = null;

            await progressDialog.Dispatcher.InvokeAsync(() =>
            {
                var conflictDialog = new FileConflictDialog(conflict)
                {
                    Owner = Application.Current.MainWindow
                };
                conflictDialog.ShowDialog();
                resolution = conflictDialog.Result;

                System.Diagnostics.Debug.WriteLine($">>> Dialog returned: {resolution?.Action}");
            });

            if (resolution == null)
            {
                System.Diagnostics.Debug.WriteLine(">>> Resolution is NULL - returning Cancel");
                return new FileConflictResolution { Action = ConflictAction.Cancel };
            }

            return resolution;
        };

        var transferTask = Task.Run(async () =>
        {
            try
            {
                var sourcePaths = SelectedItems.Count > 0
                    ? SelectedItems.Select(i => i.FullPath).ToArray()
                    : new[] { SelectedItem!.FullPath };

                await _fileTransferService.CopyAsync(
                    sourcePaths,
                    destinationPath,
                    progress,
                    progressDialog.CancellationToken,
                    conflictResolver);

                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Copy operation cancelled by user");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Copy failed");
                progressDialog.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Failed to copy:\n{ex.Message}", "Copy Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
                return false;
            }
        });

        progressDialog.Show();

        var success = await transferTask;

        progressDialog.SetCompleted(success);

        if (success)
        {
            var count = SelectedItems.Count > 0 ? SelectedItems.Count : 1;
            StatusMessage = $"Copied {count} item(s)";
            await targetPanel.LoadDirectoryAsync();
        }
        else
        {
            StatusMessage = "Copy cancelled or failed";
        }
    }

    [RelayCommand]
    private async Task MoveToTargetPanel()
    {
        if (SelectedItem == null)
        {
            _logger.LogWarning("No item selected for move");
            return;
        }

        var mainWindow = Application.Current.MainWindow;
        if (mainWindow?.DataContext is not MainViewModel mainVm)
        {
            _logger.LogError(new Exception(""), "Cannot get MainViewModel");
            return;
        }

        var targetPanel = mainVm.GetTargetPanel(this);
        var destinationPath = targetPanel.CurrentPath;

        var count = SelectedItems.Count > 0 ? SelectedItems.Count : 1;
        var itemText = count == 1 ? $"'{SelectedItem.Name}'" : $"{count} items";

        //var result = MessageBox.Show(
        //    $"Move {itemText} to '{destinationPath}'?",
        //    "Confirm Move",
        //    MessageBoxButton.YesNo,
        //    MessageBoxImage.Question);

        //if (result != MessageBoxResult.Yes)
        //    return;

        _logger.LogInformation("Moving {Count} item(s) to {Destination}", count, destinationPath);

        // Progress dialog
        var progressDialog = new FileTransferDialog("Moving")
        {
            Owner = Application.Current.MainWindow
        };

        var progress = new Progress<FileTransferProgress>(p => progressDialog.UpdateProgress(p));

        Func<FileConflictInfo, Task<FileConflictResolution>> conflictResolver = async (conflict) =>
        {
            FileConflictResolution? resolution = null;

            await progressDialog.Dispatcher.InvokeAsync(() =>
            {
                var conflictDialog = new FileConflictDialog(conflict)
                {
                    Owner = Application.Current.MainWindow
                };
                conflictDialog.ShowDialog();
                resolution = conflictDialog.Result;

                System.Diagnostics.Debug.WriteLine($">>> Dialog returned: {resolution?.Action}");
            });

            if (resolution == null)
            {
                System.Diagnostics.Debug.WriteLine(">>> Resolution is NULL - returning Cancel");
                return new FileConflictResolution { Action = ConflictAction.Cancel };
            }

            return resolution;
        };

        var transferTask = Task.Run(async () =>
        {
            try
            {
                var sourcePaths = SelectedItems.Count > 0
                    ? SelectedItems.Select(i => i.FullPath).ToArray()
                    : new[] { SelectedItem!.FullPath };

                await _fileTransferService.MoveAsync(
                    sourcePaths,
                    destinationPath,
                    progress,
                    progressDialog.CancellationToken,
                    conflictResolver);

                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Move operation cancelled by user");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Move failed");
                progressDialog.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Failed to move:\n{ex.Message}", "Move Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
                return false;
            }
        });

        progressDialog.Show();

        var success = await transferTask;

        progressDialog.SetCompleted(success);

        if (success)
        {
            StatusMessage = $"Moved {count} item(s)";
            await LoadDirectoryAsync();
            await targetPanel.LoadDirectoryAsync();
        }
        else
        {
            StatusMessage = "Move cancelled or failed";
        }
    }

    /// <summary>
    /// Extracts selected files from archive
    /// </summary>
    [RelayCommand]
    private async Task ExtractFromArchiveAsync()
    {
        // Check if we're inside an archive
        if (!_fileSystemService.IsArchivePath(CurrentPath))
        {
            MessageBox.Show(
                "This command is only available when browsing inside an archive.",
                "Extract",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        // Get selected items or all items
        var selectedItems = SelectedItems.Count > 0
            ? SelectedItems.Cast<ArchiveEntry>().ToList()
            : Items.Cast<ArchiveEntry>().ToList();

        _logger.LogDebug("Initial selection count: {Count}", selectedItems.Count);

        // Expand directories to include their contents
        var (archivePath, innerPath) = ParseArchivePath(CurrentPath);
        var itemsToExtract = await ExpandDirectoriesAsync(selectedItems, archivePath);

        _logger.LogDebug("Expanded to {Count} items for extraction", itemsToExtract.Count);

        if (itemsToExtract.Count == 0)
        {
            MessageBox.Show(
                "No items to extract.",
                "Extract",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        // Parse archive path
        var archiveName = Path.GetFileName(archivePath);

        // Show extract dialog
        var dialog = new ExtractArchiveDialog(archiveName, itemsToExtract.Count)
        {
            Owner = Application.Current.MainWindow
        };

        dialog.ShowDialog();

        if (!dialog.Confirmed)
            return;

        var destinationPath = dialog.DestinationPath;

        _logger.LogInformation("Extracting {Count} items from {Archive} to {Destination}",
            itemsToExtract.Count, archivePath, destinationPath);

        bool PreserveStructure = dialog.PreserveStructure;

        // Show progress dialog
        var progressDialog = new FileTransferDialog("Extracting")
        {
            Owner = Application.Current.MainWindow
        };

        var archiveProgressAdapter = new Progress<ArchiveProgress>(ap =>
        {
            var fileTransferProgress = new FileTransferProgress
            {
                CurrentFile = ap.CurrentFile,
                ProcessedFiles = ap.ProcessedFiles,
                TotalFiles = ap.TotalFiles,
                TransferredBytes = ap.ProcessedBytes,
                TotalBytes = ap.TotalBytes
            };

            // ✅ Update dialog directly instead of through intermediate Progress
            progressDialog.UpdateProgress(fileTransferProgress);
        });

        var extractTask = Task.Run(async () =>
        {
            try
            {
                var archiveService = _archiveService;

                if (archiveService == null)
                {
                    throw new InvalidOperationException("Archive service not available");
                }

                if (PreserveStructure)
                    await _archiveService.ExtractAsync(
                        archivePath,
                        itemsToExtract,
                        destinationPath,
                        archiveProgressAdapter,
                        progressDialog.CancellationToken);
                else
                    await ExtractFlattenedAsync(
                        archivePath,
                        itemsToExtract,
                        destinationPath,
                        archiveProgressAdapter,
                        progressDialog.CancellationToken);

                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Extract operation cancelled by user");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Extract failed");
                progressDialog.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Failed to extract:\n{ex.Message}", "Extract Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                });
                return false;
            }
        });

        progressDialog.Show();

        var success = await extractTask;

        progressDialog.SetCompleted(success);

        if (success)
        {
            StatusMessage = $"Extracted {itemsToExtract.Count} item(s)";

            // Open folder if requested
            if (dialog.OpenFolder && Directory.Exists(destinationPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = destinationPath,
                    UseShellExecute = true
                });
            }
        }
        else
        {
            StatusMessage = "Extract cancelled or failed";
        }
    }

    [RelayCommand]
    private async Task CreateArchiveAsync()
    {
        // Validate selection
        if (SelectedItems.Count == 0)
        {
            MessageBox.Show(
                "Please select files or folders to add to archive.",
                "No Selection",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _logger.LogInformation("Creating archive from {Count} item(s)", SelectedItems.Count);

        // Suggest archive name based on selection
        string suggestedName;
        if (SelectedItems.Count == 1)
        {
            suggestedName = SelectedItems[0].Name;
        }
        else
        {
            var parentFolder = Path.GetFileName(CurrentPath) ?? "Archive";
            suggestedName = parentFolder;
        }

        // Show create archive dialog
        var dialog = new CreateArchiveDialog(SelectedItems.Count, suggestedName)
        {
            Owner = Application.Current.MainWindow
        };

        dialog.ShowDialog();

        if (!dialog.Confirmed)
            return;

        // Determine output path (same directory as current path)
        var outputPath = Path.Combine(CurrentPath, dialog.ArchivePath);

        // Check if file exists
        if (File.Exists(outputPath))
        {
            var result = MessageBox.Show(
                $"Archive '{dialog.ArchivePath}' already exists. Overwrite?",
                "Confirm Overwrite",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;
        }

        _logger.LogInformation("Creating archive: {Path}", outputPath);

        // Show progress dialog
        var progressDialog = new FileTransferDialog("Creating Archive")
        {
            Owner = Application.Current.MainWindow
        };

        // Get source paths
        var sourcePaths = SelectedItems.Select(i => i.FullPath).ToList();
        var baseDirectory = CurrentPath;

        // Create archive options
        var options = new ArchiveWriteOptions
        {
            CompressionLevel = dialog.CompressionLevel,
            Password = dialog.Password
        };

        var createTask = Task.Run(async () =>
        {
            try
            {
                var progressAdapter = new Progress<ArchiveProgress>(archiveProgress =>
                {
                    // ✅ Use base properties, not computed ones
                    progressDialog.UpdateProgress(new FileTransferProgress
                    {
                        CurrentFile = archiveProgress.CurrentFile,
                        CurrentFileBytes = 0,
                        CurrentFileTotalBytes = 100,
                        TotalFiles = archiveProgress.TotalFiles,
                        ProcessedFiles = archiveProgress.ProcessedFiles,
                        TotalBytes = archiveProgress.TotalBytes,
                        TransferredBytes = archiveProgress.ProcessedBytes
                    });
                });

                await _archiveService.CreateAsync(
                    outputPath,
                    sourcePaths,
                    baseDirectory,
                    options,
                    progressAdapter,
                    progressDialog.CancellationToken);

                return (true, (string?)null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Archive creation failed");
                return (false, ex.Message);
            }
        });

        progressDialog.Show();

        // ✅ Explicit deconstruction
        (bool success, string? errorMessage) = await createTask;

        progressDialog.Dispatcher.Invoke(() =>
        {
            if (errorMessage != null)
            {
                MessageBox.Show(
                    $"Failed to create archive:\n{errorMessage}",
                    "Archive Creation Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            progressDialog.SetCompleted(success);
        });

        if (success)
        {
            StatusMessage = $"Created archive: {dialog.ArchivePath}";

            // Refresh to show new archive
            await LoadDirectoryAsync();
        }
        else if (errorMessage == null)
        {
            StatusMessage = "Archive creation cancelled";
        }
        else
        {
            StatusMessage = "Archive creation failed";
        }
    }

    /// <summary>
    /// Recursively expands ArchiveDirectoryEntry items to include all their contents
    /// </summary>
    private async Task<List<ArchiveEntry>> ExpandDirectoriesAsync(
        List<ArchiveEntry> selectedItems,
        string archivePath)
    {
        var result = new List<ArchiveEntry>();
        var processedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in selectedItems)
        {
            if (item is ArchiveFileEntry fileEntry)
            {
                // Add file directly
                if (!processedPaths.Contains(fileEntry.InnerPath))
                {
                    result.Add(fileEntry);
                    processedPaths.Add(fileEntry.InnerPath);
                    _logger.LogDebug("Added file: {Path}", fileEntry.InnerPath);
                }
            }
            else if (item is ArchiveDirectoryEntry dirEntry)
            {
                // Add directory itself
                if (!processedPaths.Contains(dirEntry.InnerPath))
                {
                    result.Add(dirEntry);
                    processedPaths.Add(dirEntry.InnerPath);
                    _logger.LogDebug("Added directory: {Path}", dirEntry.InnerPath);
                }

                // ✅ Recursively load and add directory contents
                await ExpandDirectoryContentsAsync(
                    archivePath,
                    dirEntry.InnerPath,
                    result,
                    processedPaths);
            }
        }

        return result;
    }

    /// <summary>
    /// Recursively loads all contents of a directory in archive
    /// </summary>
    private async Task ExpandDirectoryContentsAsync(
        string archivePath,
        string innerPath,
        List<ArchiveEntry> result,
        HashSet<string> processedPaths)
    {
        try
        {
            _logger.LogDebug("Expanding directory contents: {Path}", innerPath);

            // Load directory contents from archive
            var directoryEntry = await _archiveService.LoadArchiveAsync(
                archivePath,
                innerPath,
                password: null); // Password already cached

            foreach (var child in directoryEntry.Children)
            {
                if (child is ArchiveFileEntry fileEntry)
                {
                    if (!processedPaths.Contains(fileEntry.InnerPath))
                    {
                        result.Add(fileEntry);
                        processedPaths.Add(fileEntry.InnerPath);
                        _logger.LogDebug("Added file from directory: {Path}", fileEntry.InnerPath);
                    }
                }
                else if (child is ArchiveDirectoryEntry childDirEntry)
                {
                    if (!processedPaths.Contains(childDirEntry.InnerPath))
                    {
                        result.Add(childDirEntry);
                        processedPaths.Add(childDirEntry.InnerPath);
                        _logger.LogDebug("Added subdirectory: {Path}", childDirEntry.InnerPath);
                    }

                    // ✅ Recursively expand subdirectory
                    await ExpandDirectoryContentsAsync(
                        archivePath,
                        childDirEntry.InnerPath,
                        result,
                        processedPaths);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to expand directory: {Path}", innerPath);
            // Continue with other items even if one directory fails
        }
    }

    /// <summary>
    /// Extracts files without preserving folder structure (all files go to root)
    /// </summary>
    private async Task ExtractFlattenedAsync(
        string archivePath,
        List<ArchiveEntry> entries,
        string destinationPath,
        IProgress<ArchiveProgress> progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(destinationPath);

        var fileEntries = entries.OfType<ArchiveFileEntry>().ToList();
        var totalFiles = fileEntries.Count;
        var processedFiles = 0;
        var totalBytes = fileEntries.Sum(e => e.UncompressedSize);
        var processedBytes = 0L;

        foreach (var fileEntry in fileEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Extract to root (no subfolders)
            var fileName = Path.GetFileName(fileEntry.InnerPath);
            var outputPath = Path.Combine(destinationPath, fileName);

            // Handle duplicates
            var counter = 1;
            while (File.Exists(outputPath))
            {
                var nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
                var extension = Path.GetExtension(fileName);
                outputPath = Path.Combine(destinationPath, $"{nameWithoutExt} ({counter}){extension}");
                counter++;
            }

            progress?.Report(new ArchiveProgress
            {
                CurrentFile = fileName,
                ProcessedFiles = processedFiles,
                TotalFiles = totalFiles,
                ProcessedBytes = processedBytes,
                TotalBytes = totalBytes,
                Status = ArchiveOperationStatus.Processing
            });

            // Read and write file
            using var sourceStream = await _archiveService.ReadFileFromArchiveAsync(archivePath, fileEntry.InnerPath);
            using var destStream = File.Create(outputPath);
            await sourceStream.CopyToAsync(destStream, cancellationToken);

            processedBytes += fileEntry.UncompressedSize;
            processedFiles++;
        }

        progress?.Report(new ArchiveProgress
        {
            CurrentFile = "",
            ProcessedFiles = totalFiles,
            TotalFiles = totalFiles,
            ProcessedBytes = totalBytes,
            TotalBytes = totalBytes,
            Status = ArchiveOperationStatus.Completed
        });
    }

    /// <summary>
    /// Helper to parse archive path (archive.zip::innerPath)
    /// </summary>
    private (string archivePath, string innerPath) ParseArchivePath(string virtualPath)
    {
        if (!virtualPath.Contains("::"))
            return (virtualPath, string.Empty);

        var parts = virtualPath.Split(new[] { "::" }, 2, StringSplitOptions.None);
        return (parts[0], parts.Length > 1 ? parts[1] : string.Empty);
    }

    [RelayCommand]
    private async Task FindDuplicatesAsync()
    {
        _logger.LogInformation("Opening Find Duplicates dialog");

        // Get other panel path (for "Both panels" option)
        string? otherPanelPath = null;
        var mainWindow = Application.Current.MainWindow;
        if (mainWindow?.DataContext is MainViewModel mainVm)
        {
            var otherPanel = mainVm.GetTargetPanel(this);
            otherPanelPath = otherPanel.CurrentPath;
        }

        // Show configuration dialog
        var configDialog = new FindDuplicatesDialog(CurrentPath, otherPanelPath)
        {
            Owner = Application.Current.MainWindow
        };

        configDialog.ShowDialog();

        if (!configDialog.Confirmed)
            return;

        _logger.LogInformation("Starting duplicate scan with mode: {Mode}",
            configDialog.Options.CompareMode);

        // Determine search paths
        var searchPaths = new List<string>();

        if (configDialog.IncludeBothPanels && !string.IsNullOrEmpty(otherPanelPath))
        {
            searchPaths.Add(CurrentPath);
            searchPaths.Add(otherPanelPath);
            _logger.LogDebug("Scanning both panels: {Left} and {Right}", CurrentPath, otherPanelPath);
        }
        else
        {
            searchPaths.Add(CurrentPath);
            _logger.LogDebug("Scanning: {Path}", CurrentPath);
        }

        // Show progress dialog
        var progressDialog = new DuplicateScanProgressDialog
        {
            Owner = Application.Current.MainWindow
        };

        // ✅ Setup callback BEFORE showing dialog
        progressDialog.OnViewResults = (duplicateGroups) =>
        {
            _logger.LogInformation("Opening duplicate results window with {Count} groups",
                duplicateGroups.Count);

            // Get services from DI
            var app = (App)Application.Current;
            var logger = app.ServiceProvider.GetRequiredService<IAppLogger<DuplicateResultsViewModel>>();
            var previewService = app.ServiceProvider.GetRequiredService<FilePreviewService>();

            // Create ViewModel
            var resultsVm = new DuplicateResultsViewModel(
                duplicateGroups,
                _duplicateFinderService,
                previewService,
                logger);

            // Open results window
            var resultsWindow = new DuplicateResultsWindow
            {
                DataContext = resultsVm,
                Owner = Application.Current.MainWindow
            };

            resultsWindow.Show();
        };

        // Start scan in background
        var scanTask = Task.Run(async () =>
        {
            try
            {
                var progressAdapter = new Progress<DuplicateScanProgress>(scanProgress =>
                {
                    progressDialog.UpdateProgress(scanProgress);
                });

                var results = await _duplicateFinderService.FindDuplicatesAsync(
                    searchPaths,
                    configDialog.Options,
                    progressAdapter,
                    progressDialog.CancellationToken);

                return (true, results, (string?)null);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Duplicate scan cancelled by user");
                return (false, null, (string?)null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Duplicate scan failed");
                return (false, null, ex.Message);
            }
        });

        progressDialog.Show();

        // Wait for completion
        (bool success, List<DuplicateGroup>? results, string? errorMessage) = await scanTask;

        if (errorMessage != null)
        {
            progressDialog.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    $"Scan failed:\n{errorMessage}",
                    "Scan Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            });
        }

        var duplicateCount = results?.Count ?? 0;

        // ✅ Pass results to dialog for callback
        progressDialog.SetCompleted(success, duplicateCount, results);

        StatusMessage = success
            ? $"Scan complete: {duplicateCount} duplicate group(s) found"
            : "Scan cancelled or failed";
    }
    #endregion

    // ====== Helper Methods ======

    partial void OnFilterTextChanged(string value)
    {
        Items.Refresh();
        TotalItems = Items.Cast<object>().Count();
        _logger.LogDebug("Filter applied: '{Filter}' - {Count} items matched", FilterText, TotalItems);
    }

    partial void OnSortColumnChanged(string value)
    {
        UpdateSortDescriptions();
    }

    partial void OnSortDirectionChanged(ListSortDirection value)
    {
        UpdateSortDescriptions();
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

    private void UpdateSortDescriptions()
    {
        if (_itemsView == null) return;

        _itemsView.SortDescriptions.Clear();

        _itemsView.CustomSort = new FileSystemEntryCustomComparer(SortColumn, SortDirection);

        _logger.LogDebug("Sort updated: {Column} {Direction}", SortColumn, SortDirection);
    }

    /// <summary>
    /// Custom comparer for sorting: directories first, then by selected property
    /// </summary>
    private class FileSystemEntryCustomComparer : IComparer
    {
        private readonly string _sortColumn;
        private readonly ListSortDirection _sortDirection;

        public FileSystemEntryCustomComparer(string sortColumn, ListSortDirection sortDirection)
        {
            _sortColumn = sortColumn;
            _sortDirection = sortDirection;
        }

        public int Compare(object? x, object? y)
        {
            if (x is not FileSystemEntry entryX || y is not FileSystemEntry entryY)
                return 0;

            // 1. Primary: Directories always first
            bool xIsDir = entryX is DirectoryEntry;
            bool yIsDir = entryY is DirectoryEntry;

            if (xIsDir && !yIsDir) return -1;
            if (!xIsDir && yIsDir) return 1;

            // 2. Secondary: Sort by column
            int result = _sortColumn switch
            {
                "Name" => string.Compare(entryX.Name, entryY.Name, StringComparison.OrdinalIgnoreCase),

                "Size" => (entryX, entryY) switch
                {
                    (FileEntry fileX, FileEntry fileY) => fileX.Size.CompareTo(fileY.Size),
                    _ => 0
                },

                "Type" => (entryX, entryY) switch
                {
                    (FileEntry fileX, FileEntry fileY) =>
                        string.Compare(fileX.Extension, fileY.Extension, StringComparison.OrdinalIgnoreCase),
                    _ => 0
                },

                "LastModified" => entryX.LastModified.CompareTo(entryY.LastModified),

                _ => string.Compare(entryX.Name, entryY.Name, StringComparison.OrdinalIgnoreCase)
            };

            return _sortDirection == ListSortDirection.Ascending ? result : -result;
        }
    }

    // Helper for primary sort (Directory before File)
    private bool FilterPredicate(object obj)
    {
        if (string.IsNullOrWhiteSpace(FilterText))
            return true;

        if (obj is FileSystemEntry entry)
        {
            return entry.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase);
        }

        return false;
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

    /// <summary>
    /// Initializes the tab bar for this panel
    /// Called by MainViewModel after construction
    /// </summary>
    public void InitializeTabBar(TabBarViewModel tabBar)
    {
        _tabBarViewModel = tabBar;
        TabBar = tabBar;

        _tabBarViewModel.SetPathChangedCallback(async (path) =>
        {
            CurrentPath = path;
            await LoadDirectoryCommand.ExecuteAsync(null);
        });

        _logger.LogDebug("Tab bar initialized for FileExplorerViewModel");
    }

    /// <summary>
    /// Updates the active tab when navigation occurs
    /// </summary>
    partial void OnCurrentPathChanged(string value)
    {
        UpdateBreadcrumbs();
        _tabBarViewModel?.UpdateActiveTabPath(value);
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