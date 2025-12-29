using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Services;
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

    protected override void OnStartup(StartupEventArgs e)
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

        // Backup Services - NOWE!
        services.AddSingleton<IBackupStorage, BackupStorage>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<IBackupScheduler, BackupScheduler>();

        // ViewModels
        services.AddTransient<TabViewModel>();
        services.AddSingleton<MainViewModel>();
        services.AddTransient<FileExplorerViewModel>();
        services.AddSingleton<BookmarksViewModel>();
        services.AddSingleton<PreviewPanelViewModel>();
        services.AddTransient<BackupViewModel>(); // NOWY!


        _serviceProvider = services.BuildServiceProvider();

        try
        {
            // MainWindow with DI
            var mainWindow = new MainWindow
            {
                DataContext = _serviceProvider.GetRequiredService<MainViewModel>()
            };

            Log.Information("MainWindow created successfully");

            mainWindow.Show();

            Log.Information("MainWindow shown successfully");

            var mainViewModel = (MainViewModel)mainWindow.DataContext;
            _ = mainViewModel.InitializeCommand.ExecuteAsync(null);

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
            // Stop backup scheduler
            var scheduler = _serviceProvider?.GetService<IBackupScheduler>();
            if (scheduler != null && scheduler.IsRunning)
            {
                _ = scheduler.StopAsync().Wait(TimeSpan.FromSeconds(5));
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
