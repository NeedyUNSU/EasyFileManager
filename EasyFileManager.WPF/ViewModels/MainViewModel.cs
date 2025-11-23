using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFileManager.Core.Interfaces;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using MaterialDesignColors.ColorManipulation;

namespace EasyFileManager.WPF.ViewModels;

/// <summary>
/// Main application ViewModel managing both file explorer panels
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly IAppLogger<MainViewModel> _logger;
    private readonly IServiceProvider _serviceProvider;

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

    // ====== Constructor with DI ======

    public MainViewModel(
        IAppLogger<MainViewModel> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

        _logger.LogInformation("Initializing MainViewModel");

        var paletteHelper = new PaletteHelper();
        var theme = paletteHelper.GetTheme();
        _isDarkTheme = theme.GetBaseTheme() == BaseTheme.Dark;

        _leftPanel = _serviceProvider.GetRequiredService<FileExplorerViewModel>();
        _rightPanel = _serviceProvider.GetRequiredService<FileExplorerViewModel>();

        _leftPanel.CurrentPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _rightPanel.CurrentPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        _activePanel = _leftPanel;

        //_bookmarksViewModel = _serviceProvider.GetRequiredService<BookmarksViewModel>();

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
        System.Windows.MessageBox.Show(
            "Easy File Manager v1.0\n\nBuilt with WPF + Material Design",
            "About",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    [RelayCommand]
    private void ShowSettings()
    {
        _logger.LogInformation("Settings requested");
        System.Windows.MessageBox.Show("Settings window coming soon...", "Settings");
    }

    [RelayCommand]
    private void ToggleBookmarksFlyout()
    {
        IsBookmarksFlyoutOpen = !IsBookmarksFlyoutOpen;
        _logger.LogDebug("Bookmarks flyout toggled: {IsOpen}", IsBookmarksFlyoutOpen);
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        _logger.LogInformation("Loading initial directories");

        // Załaduj oba panele równolegle
        await Task.WhenAll(
            _leftPanel.LoadDirectoryCommand.ExecuteAsync(null),
            _rightPanel.LoadDirectoryCommand.ExecuteAsync(null)
        );

        _logger.LogInformation("Initial directories loaded");
    }

    public FileExplorerViewModel GetTargetPanel(FileExplorerViewModel sourcePanel)
    {
        return sourcePanel == LeftPanel ? RightPanel : LeftPanel;
    }

}