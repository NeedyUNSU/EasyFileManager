using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;
using EasyFileManager.WPF.Models;
using EasyFileManager.WPF.Services;
using EasyFileManager.WPF.Views;
using MaterialDesignColors.ColorManipulation;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using SharpCompress;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using ZstdSharp.Unsafe;

namespace EasyFileManager.WPF.ViewModels;

/// <summary>
/// Main application ViewModel managing both file explorer panels
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IAppLogger<MainViewModel> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly ITabPersistenceService _tabPersistenceService;
    private readonly IInputBindingService _inputBindingService;
    private readonly ISettingsService _settingsService;

    // ====== Two independent panels ======

    [ObservableProperty]
    private FileExplorerViewModel _leftPanel;

    [ObservableProperty]
    private FileExplorerViewModel _rightPanel;

    [ObservableProperty]
    private string _applicationTitle = "Easy File Manager";

    [ObservableProperty]
    private bool _isDarkTheme;

    [ObservableProperty]
    private FileExplorerViewModel? _activePanel;

    [ObservableProperty]
    private bool _isBookmarksFlyoutOpen;

    private BookmarksViewModel? _bookmarksViewModel;

    public BookmarksViewModel BookmarksViewModel
    {
        get
        {
            if (_bookmarksViewModel == null)
            {
                _bookmarksViewModel = _serviceProvider.GetRequiredService<BookmarksViewModel>();
            }
            return _bookmarksViewModel;
        }
    }

    [ObservableProperty]
    private bool _isPreviewPanelVisible = false;
    partial void OnIsPreviewPanelVisibleChanged(bool value)
    {
        System.Diagnostics.Debug.WriteLine($"@@@ MainViewModel.IsPreviewPanelVisible changed to: {value}");
        if (_previewPanelViewModel != null)
        {
            _previewPanelViewModel.IsVisible = value;
        }
    }

    private PreviewPanelViewModel? _previewPanelViewModel;
    public PreviewPanelViewModel PreviewPanelViewModel
    {
        get
        {
            if (_previewPanelViewModel == null)
            {
                System.Diagnostics.Debug.WriteLine("@@@ Creating PreviewPanelViewModel via DI...");
                _previewPanelViewModel = _serviceProvider.GetRequiredService<PreviewPanelViewModel>();
                System.Diagnostics.Debug.WriteLine("@@@ PreviewPanelViewModel created successfully");
            }
            return _previewPanelViewModel;
        }
    }

    // ====== Backup ViewModel ======
    private BackupViewModel? _backupViewModel;
    public BackupViewModel BackupViewModel
    {
        get
        {
            if (_backupViewModel == null)
            {
                _logger.LogInformation("Creating BackupViewModel via DI...");
                _backupViewModel = _serviceProvider.GetRequiredService<BackupViewModel>();
                _ = _backupViewModel.InitializeCommand.ExecuteAsync(null);
            }
            return _backupViewModel;
        }
    }

    public bool IsLeftPanelActive
    {
        get
        {
            return (ActivePanel == LeftPanel);
        }
    }

    // ====== Constructor with DI ======

    public MainViewModel(
        IAppLogger<MainViewModel> logger,
        IServiceProvider serviceProvider,
        IInputBindingService inputBindingService,
        ITabPersistenceService tabPersistenceService,
        ISettingsService settingsService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _tabPersistenceService = tabPersistenceService ?? throw new ArgumentNullException(nameof(tabPersistenceService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        _logger.LogInformation("Initializing MainViewModel");

        #region testy
        CustomMessageBox.Show("Operacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnieOperacja zakończona pomyślnie!");

        // Z tytułem
        CustomMessageBox.Show("Plik został zapisany", "Sukces");

        // Standardowe przyciski - OK/Cancel
        var result = CustomMessageBox.Show(
            "Czy chcesz kontynuować?",
            "Potwierdzenie",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.OK)
        {
            // Kontynuuj
            CustomMessageBox.Show("kontynułuj!", "pierdolenie", MessageBoxButton.OK);
        }

        // Standardowe przyciski - Yes/No
        var deleteResult = CustomMessageBox.Show(
            "Czy na pewno chcesz usunąć ten plik?",
            "Usuwanie",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (deleteResult == MessageBoxResult.Yes)
        {
            // Usuń plik

            CustomMessageBox.Show("yesy!", "pierdolenie", MessageBoxButton.OK);
        }

        // Standardowe przyciski - Yes/No/Cancel
        var saveResult = CustomMessageBox.Show(
            "Czy zapisać zmiany przed zamknięciem?",
            "Niezapisane zmiany",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        switch (saveResult)
        {
            case MessageBoxResult.Yes:
                // Zapisz i zamknij
                CustomMessageBox.Show("yes!", "pierdolenie", MessageBoxButton.OK);
                break;
            case MessageBoxResult.No:
                // Zamknij bez zapisywania

                CustomMessageBox.Show("no!", "pierdolenie", MessageBoxButton.OK);
                break;
            case MessageBoxResult.Cancel:
                // Anuluj zamykanie

                CustomMessageBox.Show("cancel!", "pierdolenie", MessageBoxButton.OK);
                break;
        }

        // Różne ikony
        CustomMessageBox.Show("Operacja udana!", "Sukces", MessageBoxButton.OK, MessageBoxImage.Information);
        CustomMessageBox.Show("Uwaga! To może być niebezpieczne", "Ostrzeżenie", MessageBoxButton.OK, MessageBoxImage.Warning);
        CustomMessageBox.Show("Wystąpił błąd!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);

        // Custom przyciski z użyciem standardowych wyników
        var customModel = new CustomMessageBoxModel
        {
            Title = "Wybierz akcję",
            Message = "Co chcesz zrobić z tym plikiem?",
            Image = MessageBoxImage.Question,
            CustomButtons = new List<DialogButton>
    {
        new DialogButton("OTWÓRZ", MessageBoxResult.Yes, true, false),
        new DialogButton("EDYTUJ", MessageBoxResult.No, false, false),
        new DialogButton("ANULUJ", MessageBoxResult.Cancel, false, true)
    }
        };

        var customResult = CustomMessageBox.ShowCustom(customModel);

        switch (customResult)
        {
            case MessageBoxResult.Yes: // OTWÓRZ

                CustomMessageBox.Show("open!", "pierdolenie", MessageBoxButton.OK);           // Otwórz plik
                break;
            case MessageBoxResult.No: // EDYTUJ

                CustomMessageBox.Show("edit!", "pierdolenie", MessageBoxButton.OK);                     // Edytuj plik
                break;
            case MessageBoxResult.Cancel:
                // Anulowano

                CustomMessageBox.Show("cancel!", "pierdolenie", MessageBoxButton.OK);
                break;
        }

        // Można też użyć innych standardowych wyników dla custom buttonów
        var advancedModel = new CustomMessageBoxModel
        {
            Title = "Operacja na pliku",
            Message = "Wybierz operację do wykonania",
            Image = MessageBoxImage.Question,
            CustomButtons = new List<DialogButton>
    {
        new DialogButton("KOPIUJ", MessageBoxResult.Yes, true, false),
        new DialogButton("PRZENIEŚ", MessageBoxResult.No, false, false),
        new DialogButton("ANULUJ", MessageBoxResult.Cancel, false, true)
    }
        };

        var advancedResult = CustomMessageBox.ShowCustom(advancedModel);
        #endregion 

        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();
        _isDarkTheme = theme.GetBaseTheme() == BaseTheme.Dark;

        _leftPanel = _serviceProvider.GetRequiredService<FileExplorerViewModel>();
        _rightPanel = _serviceProvider.GetRequiredService<FileExplorerViewModel>();

        var leftTabBar = new TabBarViewModel(
            _serviceProvider.GetRequiredService<IAppLogger<TabBarViewModel>>(),
            _settingsService,
            _tabPersistenceService,
            () => _serviceProvider.GetRequiredService<TabViewModel>(),
            "left");

        var rightTabBar = new TabBarViewModel(
            _serviceProvider.GetRequiredService<IAppLogger<TabBarViewModel>>(),
            _settingsService,
            _tabPersistenceService,
            () => _serviceProvider.GetRequiredService<TabViewModel>(),
            "right");

        _leftPanel.InitializeTabBar(leftTabBar);
        _rightPanel.InitializeTabBar(rightTabBar);

        _activePanel = _leftPanel;

        _inputBindingService = inputBindingService ?? throw new ArgumentNullException(nameof(inputBindingService));

        _logger.LogInformation("MainViewModel initialized with theme: {Theme}", _isDarkTheme ? "Dark" : "Light");
    }

    // ====== Commands ======

    [RelayCommand]
    private void ToggleTheme()
    {
        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();
        IsDarkTheme = theme.GetBaseTheme() == BaseTheme.Dark;
        IsDarkTheme = !IsDarkTheme;
        theme.SetBaseTheme(IsDarkTheme ? BaseTheme.Dark : BaseTheme.Light);
        paletteHelper.SetTheme(theme);
        _logger.LogInformation("Theme toggled to: {Theme}", IsDarkTheme ? "Dark" : "Light");
    }

    [RelayCommand]
    private void ShowAbout()
    {
        CustomMessageBox.Show(
            "Easy File Manager v1.0\n\nBuilt with WPF + Material Design",
            "About",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    [RelayCommand]
    private void ToggleBookmarksFlyout()
    {
        IsBookmarksFlyoutOpen = !IsBookmarksFlyoutOpen;
        _logger.LogDebug("Bookmarks flyout toggled: {IsOpen}", IsBookmarksFlyoutOpen);
    }

    [RelayCommand]
    private void TogglePreviewPanel()
    {
        System.Diagnostics.Debug.WriteLine($"@@@ TogglePreviewPanel CALLED - Current: {IsPreviewPanelVisible}");
        IsPreviewPanelVisible = !IsPreviewPanelVisible;
        System.Diagnostics.Debug.WriteLine($"@@@ New value: {IsPreviewPanelVisible}");

        PreviewPanelViewModel.IsVisible = IsPreviewPanelVisible;
        _logger.LogDebug("Preview panel toggled: {IsVisible}", IsPreviewPanelVisible);
    }

    [RelayCommand]
    private void ShowBackup()
    {
        _logger.LogInformation("Opening Backup Manager");

        var backupWindow = new System.Windows.Window
        {
            Title = "Backup Manager",
            Width = 1200,
            Height = 800,
            Content = new BackupPanel
            {
                DataContext = BackupViewModel
            },
            Owner = System.Windows.Application.Current.MainWindow,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner
        };

        backupWindow.Show();
    }

    [RelayCommand]
    private void ShowSettings()
    {
        try
        {
            _logger.LogInformation("Opening Settings window");

            var settingsVm = ((App)Application.Current).ServiceProvider.GetRequiredService<SettingsViewModel>();

            var settingsWindow = new SettingsWindow
            {
                Owner = Application.Current.MainWindow,
                DataContext = settingsVm
            };

            settingsWindow.ShowDialog();

            _logger.LogInformation("Settings window closed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open Settings window");
            CustomMessageBox.Show($"Failed to open Settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        _logger.LogInformation("Loading initial directories and tabs");

        // Initialize tabs first
        var leftDefaultPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var rightDefaultPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

#pragma warning disable MVVMTK0034 // Direct field reference to [ObservableProperty] backing field

        await Task.WhenAll(
            _leftPanel.TabBar!.InitializeAsync(leftDefaultPath),
            _rightPanel.TabBar!.InitializeAsync(rightDefaultPath)
        );

        // Load active tabs
        if (_leftPanel.TabBar.ActiveTab != null)
        {
            _leftPanel.CurrentPath = _leftPanel.TabBar.ActiveTab.Path;
        }

        if (_rightPanel.TabBar.ActiveTab != null)
        {
            _rightPanel.CurrentPath = _rightPanel.TabBar.ActiveTab.Path;
        }

        await Task.WhenAll(
            _leftPanel.LoadDirectoryCommand.ExecuteAsync(null),
            _rightPanel.LoadDirectoryCommand.ExecuteAsync(null)
        );

#pragma warning restore MVVMTK0034 // Direct field reference to [ObservableProperty] backing field

        // Register keyboard shortcuts
        RegisterKeyboardShortcuts();
        _inputBindingService.ReloadShortcuts();

        _logger.LogInformation("Initial directories and tabs loaded");
    }

    public FileExplorerViewModel GetTargetPanel(FileExplorerViewModel sourcePanel)
    {
        return sourcePanel == LeftPanel ? RightPanel : LeftPanel;
    }

    private void RegisterKeyboardShortcuts()
    {
        _logger.LogInformation("Registering keyboard shortcut commands");

        _inputBindingService.RegisterCommand("FindDuplicates", new RelayCommand(() =>
        {
            try
            {
                ActivePanel?.FindDuplicatesCommand.Execute(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute FindDuplicates");
            }
        }));

        _inputBindingService.RegisterCommand("ExtractFromArchive", new RelayCommand(() =>
        {
            try
            {
                ActivePanel?.ExtractFromArchiveCommand.Execute(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute ExtractFromArchive");
            }
        }));

        _inputBindingService.RegisterCommand("CreateArchive", new RelayCommand(() =>
        {
            try
            {
                ActivePanel?.CreateArchiveCommand.Execute(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute CreateArchive");
            }
        }));

        _inputBindingService.RegisterCommand("CopyToTargetPanel", new RelayCommand(() =>
        {
            try
            {
                ActivePanel?.CopyToTargetPanelCommand.Execute(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute CopyToTargetPanel");
            }
        }));

        _inputBindingService.RegisterCommand("MoveToTargetPanel", new RelayCommand(() =>
        {
            try
            {
                ActivePanel?.MoveToTargetPanelCommand.Execute(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute MoveToTargetPanel");
            }
        }));

        _inputBindingService.RegisterCommand("DuplicateTab", new RelayCommand(() =>
        {
            try
            {
                ActivePanel?.TabBar?.DuplicateActiveTabCommand?.Execute(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute DuplicateTab");
            }
        }));

        _inputBindingService.RegisterCommand("CloseOtherTab", new RelayCommand(() =>
        {
            try
            {
                ActivePanel?.TabBar?.CloseOtherTabsCommand?.Execute(ActivePanel.TabBar.ActiveTab);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute CloseOtherTab");
            }
        }));

        _inputBindingService.RegisterCommand("CloseTabToRight", new RelayCommand(() =>
        {
            try
            {
                ActivePanel?.TabBar?.CloseTabsToRightCommand?.Execute(ActivePanel.TabBar.ActiveTab);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute CloseTabToRight");
            }
        }));

        _inputBindingService.RegisterCommand("PinTab", new RelayCommand(() =>
        {
            try
            {
                ActivePanel?.TabBar?.TogglePinCommand?.Execute(ActivePanel.TabBar.ActiveTab);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute PinTab");
            }
        }));

        _inputBindingService.RegisterCommand("NewTab", new RelayCommand(() =>
        {
            try
            {
                ActivePanel?.TabBar?.NewTabCommand?.Execute(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute NewTab");
            }
        }));

        _inputBindingService.RegisterCommand("CloseTab", new RelayCommand(() =>
        {
            try
            {
                ActivePanel?.TabBar?.CloseTabCommand?.Execute(ActivePanel?.TabBar?.ActiveTab);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute CloseTab");
            }
        }));

        _inputBindingService.RegisterCommand("NextTab", new RelayCommand(() =>
        {
            try
            {
                ActivePanel?.TabBar?.NextTabCommand.Execute(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute NextTab");
            }
        }));

        _inputBindingService.RegisterCommand("PreviousTab", new RelayCommand(() =>
        {
            try
            {
                ActivePanel?.TabBar?.PreviousTabCommand.Execute(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute PreviousTab");
            }
        }));

        _inputBindingService.RegisterCommand("PreviewPanel", new RelayCommand(() =>
        {
            try
            {
                TogglePreviewPanelCommand.Execute(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute PreviewPanel");
            }
        }));

        _inputBindingService.RegisterCommand("BookmarksPanel", new RelayCommand(() =>
        {
            try
            {
                ToggleBookmarksFlyoutCommand.Execute(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute PreviewPanel");
            }
        }));

        _inputBindingService.RegisterCommand("Refresh", new RelayCommand(() =>
        {
            try
            {
                ActivePanel?.LoadDirectoryCommand.ExecuteAsync(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute Refresh");
            }
        }));

        _inputBindingService.RegisterCommand("NavigateUp", new RelayCommand(() =>
        {
            try
            {
                ActivePanel?.NavigateUpCommand?.Execute(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute NavigateUp");
            }
        }));

        _inputBindingService.RegisterCommand("Copy", new RelayCommand(() =>
        {
            try
            {
                if (ActivePanel != null && ActivePanel.SelectedItems.Count > 0)
                {
                    var files = new System.Collections.Specialized.StringCollection();
                    foreach (var item in ActivePanel.SelectedItems)
                    {
                        files.Add(item.FullPath);
                    }
                    System.Windows.Clipboard.SetFileDropList(files);
                    _logger.LogDebug("Copied {Count} items to clipboard", ActivePanel.SelectedItems.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute Copy");
            }
        }));

        _inputBindingService.RegisterCommand("Cut", new RelayCommand(() =>
        {
            try
            {
                if (ActivePanel != null && ActivePanel.SelectedItems.Count > 0)
                {
                    var files = new System.Collections.Specialized.StringCollection();
                    foreach (var item in ActivePanel.SelectedItems)
                    {
                        files.Add(item.FullPath);
                    }

                    var dataObject = new System.Windows.DataObject();
                    dataObject.SetFileDropList(files);

                    var dropEffect = new MemoryStream();
                    var writer = new BinaryWriter(dropEffect);
                    writer.Write((int)System.Windows.DragDropEffects.Move);
                    dropEffect.Position = 0;
                    dataObject.SetData("Preferred DropEffect", dropEffect);

                    System.Windows.Clipboard.SetDataObject(dataObject, true);
                    _logger.LogDebug("Cut {Count} items to clipboard", ActivePanel.SelectedItems.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute Cut");
            }
        }));

        _inputBindingService.RegisterCommand("Paste", new RelayCommand(() =>
        {
            try
            {
                if (System.Windows.Clipboard.ContainsFileDropList())
                {
                    var files = System.Windows.Clipboard.GetFileDropList();

                    if (ActivePanel != null && files.Count > 0)
                    {
                        var destinationPath = ActivePanel.CurrentPath;

                        // Check if it's a Move (Cut) or Copy operation
                        var dataObject = System.Windows.Clipboard.GetDataObject();
                        var isMove = false;

                        if (dataObject?.GetDataPresent("Preferred DropEffect") == true)
                        {
                            var dropEffect = dataObject.GetData("Preferred DropEffect") as MemoryStream;
                            if (dropEffect != null)
                            {
                                var reader = new BinaryReader(dropEffect);
                                var effect = (System.Windows.DragDropEffects)reader.ReadInt32();
                                isMove = effect == System.Windows.DragDropEffects.Move;
                            }
                        }

                        // Execute paste operation
                        _ = Task.Run(async () =>
                        {
                            try
                            {
#pragma warning disable CS8600 // Konwertowanie literału null lub możliwej wartości null na nienullowalny typ.
                                foreach (string sourcePath in files)
                                {
                                    var fileName = Path.GetFileName(sourcePath);
#pragma warning disable CS8604 // Możliwy argument odwołania o wartości null.
                                    var destPath = Path.Combine(destinationPath, fileName);
#pragma warning restore CS8604 // Możliwy argument odwołania o wartości null.

                                    if (File.Exists(sourcePath))
                                    {
                                        if (isMove)
                                            File.Move(sourcePath, destPath);
                                        else
                                            File.Copy(sourcePath, destPath);
                                    }
                                    else if (Directory.Exists(sourcePath))
                                    {
                                        CopyDirectory(sourcePath, destPath);
                                        if (isMove)
                                            Directory.Delete(sourcePath, true);
                                    }
                                }
#pragma warning restore CS8600 // Konwertowanie literału null lub możliwej wartości null na nienullowalny typ.

                                // Refresh after paste
                                await Application.Current.Dispatcher.InvokeAsync(() =>
                                {
                                    ActivePanel.LoadDirectoryCommand?.Execute(null);
                                });

                                _logger.LogInformation("{Operation} {Count} items to {Dest}",
                                    isMove ? "Moved" : "Copied", files.Count, destinationPath);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed during paste operation");
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute Paste");
            }
        }));

        _inputBindingService.RegisterCommand("Delete", new RelayCommand(() =>
        {
            try
            {
                if (ActivePanel != null && ActivePanel.SelectedItems.Count > 0)
                {
                    _ = ActivePanel.DeleteItemCommand?.ExecuteAsync(null);
                    _logger.LogDebug("Delete executed on {Count} items", ActivePanel.SelectedItems.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute Delete");
            }
        }));


        _inputBindingService.RegisterCommand("Rename", new RelayCommand(() =>
        {
            try
            {
                ActivePanel?.RenameItemCommand.Execute(ActivePanel.SelectedItem);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute Rename");
            }
        }));

        // TODO:
        // View + Command to show properties
        _inputBindingService.RegisterCommand("Properties", new RelayCommand(() =>
        {
            try
            {
                if (ActivePanel != null && ActivePanel.SelectedItem != null)
                {
                    var item = ActivePanel.SelectedItem;
                    var info = new StringBuilder();
                    info.AppendLine($"Name: {item.Name}");
                    info.AppendLine($"Path: {item.FullPath}");
                    info.AppendLine($"Type: {(item is DirectoryEntry ? "Folder" : "File")}");
                    info.AppendLine($"Size: {(item is FileEntry file ? file.Size : 0)}");
                    info.AppendLine($"Modified: {(item is FileEntry filed ? filed.LastModified : DateTime.MinValue)}");

                    if (File.Exists(item.FullPath))
                    {
                        var fileInfo = new FileInfo(item.FullPath);
                        info.AppendLine($"Created: {fileInfo.CreationTime}");
                        info.AppendLine($"Accessed: {fileInfo.LastAccessTime}");
                        info.AppendLine($"Attributes: {fileInfo.Attributes}");
                    }

                    CustomMessageBox.Show(info.ToString(), "Properties",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    _logger.LogDebug("Showed properties for: {Name}", item.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute Properties");
            }
        }));

        _inputBindingService.RegisterCommand("SelectAll", new RelayCommand(() =>
        {
            try
            {
                if (ActivePanel != null)
                {
                    ActivePanel.SelectedItems.Clear();
                    foreach (var item in ActivePanel.AllItems)
                    {
                        ActivePanel.SelectedItems.Add(item);
                    }
                    _logger.LogDebug("Selected all {Count} items", ActivePanel.AllItems.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute SelectAll");
            }
        }));

        _inputBindingService.RegisterCommand("Search", new RelayCommand(() =>
        {
            try
            {
                if (ActivePanel != null)
                {
                    ActivePanel.IsFilterVisible = !ActivePanel.IsFilterVisible;
                    _logger.LogDebug("Toggled search filter: {Visible}", ActivePanel.IsFilterVisible);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute Search");
            }
        }));

        _inputBindingService.RegisterCommand("HideSearch", new RelayCommand(() =>
        {
            try
            {
                ActivePanel?.HideFilterCommand.Execute(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute Search");
            }
        }));

        _inputBindingService.RegisterCommand("BackupManager", new RelayCommand(() =>
        {
            try
            {
                ShowBackupCommand?.Execute(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute BackupManager");
            }
        }));

        _inputBindingService.RegisterCommand("Settings", new RelayCommand(() =>
        {
            try
            {
                ShowSettingsCommand?.Execute(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute Settings");
            }
        }));

        _inputBindingService.RegisterCommand("Quit", new RelayCommand(() =>
        {
            try
            {
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute Quit");
            }
        }));


        _logger.LogInformation("Keyboard shortcut commands registered");
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            File.Copy(file, Path.Combine(destDir, fileName));
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(dir);
            CopyDirectory(dir, Path.Combine(destDir, dirName));
        }
    }
}
