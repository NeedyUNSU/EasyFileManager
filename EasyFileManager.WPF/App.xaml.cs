using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Services;
using EasyFileManager.WPF.Services;
using EasyFileManager.WPF.ViewModels;
using EasyFileManager.WPF.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using System.IO;
using System.Windows;

namespace EasyFileManager.WPF;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    public IServiceProvider ServiceProvider => _serviceProvider!;

    protected async override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Konfiguracja Serilog
        var logsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "EasyFileManager", "Logs");

        Directory.CreateDirectory(logsPath);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(
                Path.Combine(logsPath, "app-.log"),
                rollingInterval: Serilog.RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("=== Application starting ===");

        // Konfiguracja DI Container
        var services = new ServiceCollection();

        // Logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog();
        });

        // Core Services
        services.AddSingleton(typeof(IAppLogger<>), typeof(AppLogger<>));
        services.AddSingleton<IFileSystemService, AsyncFileSystemService>();
        services.AddSingleton<IFileTransferService, FileTransferService>();
        services.AddSingleton<IBookmarkService, BookmarkService>();
        services.AddSingleton<IPreviewService, PreviewService>();
        services.AddSingleton<ITabPersistenceService, TabPersistenceService>();
        services.AddSingleton<EasyFileManager.Core.Services.Plugins.ZipPlugin>();
        services.AddSingleton<FilePreviewService>();
        services.AddSingleton<DuplicateFinderService>();
        services.AddSingleton<IArchiveService>(sp =>
        {
            var logger = sp.GetRequiredService<IAppLogger<ArchiveService>>();
            var zipPlugin = sp.GetRequiredService<EasyFileManager.Core.Services.Plugins.ZipPlugin>();
            var plugins = new[] { zipPlugin };
            return new ArchiveService(plugins, logger);
        });

        // Backup Services
        services.AddSingleton<IBackupStorage, BackupStorage>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IBackupScheduler, BackupScheduler>();

        // Settings Service
        services.AddSingleton<ISettingsService, SettingsService>();

        // Theme Service
        services.AddSingleton<IThemeService, ThemeService>();

        // Input Binding Service
        services.AddSingleton<IInputBindingService, InputBindingService>();

        // ViewModels
        services.AddTransient<TabViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<FileExplorerViewModel>();
        services.AddSingleton<BookmarksViewModel>();
        services.AddSingleton<PreviewPanelViewModel>();
        services.AddTransient<BackupViewModel>();
        services.AddTransient<SettingsViewModel>();


        _serviceProvider = services.BuildServiceProvider();

        try
        {
            // Load settings
            var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
            var settings = await settingsService.LoadAsync();
            Log.Information("Settings loaded");

            // Apply theme settings
            var themeService = _serviceProvider.GetRequiredService<IThemeService>();
            themeService.ApplyAppearanceSettings(settings.Appearance);
            Log.Information("Theme applied");

            // MainWindow with DI
            var mainWindow = new MainWindow
            {
                DataContext = _serviceProvider.GetRequiredService<MainViewModel>()
            };

            // Apply window position from settings
            if (settings.Behavior.RestoreWindowPosition)
            {
                // Validate window position (must be within screen bounds)
                var screenWidth = SystemParameters.VirtualScreenWidth;
                var screenHeight = SystemParameters.VirtualScreenHeight;

                if (settings.Behavior.WindowLeft >= 0 &&
                    settings.Behavior.WindowLeft < screenWidth &&
                    settings.Behavior.WindowTop >= 0 &&
                    settings.Behavior.WindowTop < screenHeight &&
                    settings.Behavior.WindowWidth > 0 &&
                    settings.Behavior.WindowWidth <= screenWidth &&
                    settings.Behavior.WindowHeight > 0 &&
                    settings.Behavior.WindowHeight <= screenHeight)
                {
                    mainWindow.Left = settings.Behavior.WindowLeft;
                    mainWindow.Top = settings.Behavior.WindowTop;
                    mainWindow.Width = settings.Behavior.WindowWidth;
                    mainWindow.Height = settings.Behavior.WindowHeight;

                    if (settings.Behavior.WindowState == "Maximized")
                    {
                        mainWindow.WindowState = WindowState.Maximized;
                    }

                    Log.Information("Window position restored: {Left},{Top} {Width}x{Height}",
                        mainWindow.Left, mainWindow.Top, mainWindow.Width, mainWindow.Height);
                }
                else
                {
                    Log.Warning("Invalid window position in settings, using defaults");
                }
            }

            Log.Information("MainWindow created successfully");

            mainWindow.Show();

            Log.Information("MainWindow shown successfully");

            var mainViewModel = (MainViewModel)mainWindow.DataContext;
            _ = mainViewModel.InitializeCommand.ExecuteAsync(null);

            var backupViewModel = _serviceProvider?.GetService<BackupViewModel>();
            _ = backupViewModel?.InitializeCommand.ExecuteAsync(null);

            Log.Information("Application started successfully");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "CRITICAL ERROR during MainWindow initialization");
            MessageBox.Show(
                $"Fatal error during startup:\n\n{ex.Message}\n\nStack Trace:\n{ex.StackTrace}",
                "Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            throw;
        }

        Log.Information("Application started successfully");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("=== Application exiting ===");

        try
        {
            // Save window position to settings
            var settingsService = _serviceProvider?.GetService<ISettingsService>();
            if (settingsService != null && Application.Current.MainWindow != null)
            {
                var mainWindow = Application.Current.MainWindow;
                var settings = settingsService.Settings;

                // Only save if window is not minimized
                if (mainWindow.WindowState != WindowState.Minimized)
                {
                    // Save actual position before maximization
                    if (mainWindow.WindowState == WindowState.Maximized)
                    {
                        settings.Behavior.WindowState = "Maximized";
                        // Don't update position/size for maximized window
                        // (RestoreBounds not accessible in OnExit)
                    }
                    else
                    {
                        settings.Behavior.WindowLeft = mainWindow.Left;
                        settings.Behavior.WindowTop = mainWindow.Top;
                        settings.Behavior.WindowWidth = mainWindow.Width;
                        settings.Behavior.WindowHeight = mainWindow.Height;
                        settings.Behavior.WindowState = "Normal";
                    }

                    _ = settingsService.SaveAsync().Wait(TimeSpan.FromSeconds(2));
                    Log.Information("Window position saved");
                }
            }

            // Stop backup scheduler
            var scheduler = _serviceProvider?.GetService<IBackupScheduler>();
            if (scheduler != null && scheduler.IsRunning)
            {
                _ = scheduler.StopAsync().Wait(TimeSpan.FromSeconds(5));
            }

            var backupStorage = _serviceProvider?.GetService<IBackupStorage>();
            if (backupStorage != null)
            {
                _ = backupStorage.SaveBackupToFileAsync(60).Wait(TimeSpan.FromSeconds(5));
            }

            // Save all tab sessions
            var mainViewModel = _serviceProvider?.GetService<MainViewModel>();
            if (mainViewModel != null)
            {
                _ = mainViewModel.LeftPanel.TabBar?.SaveSessionAsync().Wait(TimeSpan.FromSeconds(2));
                _ = mainViewModel.RightPanel.TabBar?.SaveSessionAsync().Wait(TimeSpan.FromSeconds(2));
                Log.Information("Tab sessions saved");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during application shutdown");
        }

        Log.CloseAndFlush();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
