using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace EasyFileManager.WPF.ViewModels;

/// <summary>
/// ViewModel for the file preview panel
/// Handles preview generation for images, text, and metadata
/// </summary>
public partial class PreviewPanelViewModel : ViewModelBase
{
    private readonly IPreviewService _previewService;
    private readonly IAppLogger<PreviewPanelViewModel> _logger;
    private CancellationTokenSource? _currentPreviewCts;

    [ObservableProperty]
    private PreviewContent? _currentPreview;
    partial void OnCurrentPreviewChanged(PreviewContent? value)
    {
        System.Diagnostics.Debug.WriteLine($"@@@ OnCurrentPreviewChanged: Type={value?.Type}, FileName={value?.FileName}");
    }

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private string _statusMessage = "No file selected";
    partial void OnStatusMessageChanged(string value)
    {
        System.Diagnostics.Debug.WriteLine($"@@@ OnStatusMessageChanged: {value}");
    }

    [ObservableProperty]
    private BitmapImage? _imagePreview;
    partial void OnImagePreviewChanged(BitmapImage? value)
    {
        System.Diagnostics.Debug.WriteLine($"@@@ OnImagePreviewChanged: {value != null} (Width={value?.PixelWidth ?? 0})");
    }

    [ObservableProperty]
    private bool _isCalculatingHash;

    public PreviewPanelViewModel(
        IPreviewService previewService,
        IAppLogger<PreviewPanelViewModel> logger)
    {
        _previewService = previewService ?? throw new ArgumentNullException(nameof(previewService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        System.Diagnostics.Debug.WriteLine("@@@ PreviewPanelViewModel CONSTRUCTOR called");
    }

    /// <summary>
    /// Generates preview for a selected file
    /// </summary>
    [RelayCommand]
    public async Task LoadPreviewAsync(FileSystemEntry? entry)
    {
        System.Diagnostics.Debug.WriteLine($"@@@ LoadPreviewAsync CALLED - Entry: {entry?.Name ?? "NULL"}");

        // Cancel previous preview generation
        _currentPreviewCts?.Cancel();
        _currentPreviewCts?.Dispose();
        _currentPreviewCts = new CancellationTokenSource();

        System.Diagnostics.Debug.WriteLine($"@@@ CancellationToken created");

        if (entry == null)
        {
            System.Diagnostics.Debug.WriteLine($"@@@ Entry is null");
            CurrentPreview = PreviewContent.Empty();
            ImagePreview = null;
            StatusMessage = "No file selected";
            return;
        }

        if (entry is DirectoryEntry directoryEntry)
        {
            System.Diagnostics.Debug.WriteLine($"@@@ Entry is Directory - generating directory preview");
            try
            {
                IsLoading = true;
                StatusMessage = $"Loading directory info for {directoryEntry.Name}...";

                var preview = await _previewService.GenerateDirectoryPreviewAsync(
                    directoryEntry.FullPath,
                    _currentPreviewCts.Token);

                CurrentPreview = preview;
                ImagePreview = null;

                StatusMessage = preview.HasError
                    ? $"Error: {preview.ErrorMessage}"
                    : $"Directory: {directoryEntry.Name}";

                System.Diagnostics.Debug.WriteLine($"@@@ Directory preview loaded successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"@@@ Directory preview EXCEPTION: {ex.Message}");
                CurrentPreview = PreviewContent.Error(directoryEntry.FullPath, ex.Message);
                StatusMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
            return;
        }


        System.Diagnostics.Debug.WriteLine($"@@@ Entry is FileEntry");

        var fileEntry = (FileEntry)entry;

        try
        {
            System.Diagnostics.Debug.WriteLine($"@@@ Setting IsLoading = true");
            IsLoading = true;

            System.Diagnostics.Debug.WriteLine($"@@@ Setting StatusMessage");
            StatusMessage = $"Loading preview for {fileEntry.Name}...";

            System.Diagnostics.Debug.WriteLine($"@@@ About to call PreviewService.GeneratePreviewAsync for: {fileEntry.FullPath}");

            _logger.LogDebug("Loading preview for: {Path}", fileEntry.FullPath);

            var preview = await _previewService.GeneratePreviewAsync(
                fileEntry.FullPath,
                _currentPreviewCts.Token);

            System.Diagnostics.Debug.WriteLine($"@@@ PreviewService returned: Type={preview.Type}, HasError={preview.HasError}");

            CurrentPreview = preview;

            // Load image into BitmapImage for WPF
            if (preview.Type == PreviewType.Image && preview.ImageData != null)
            {
                System.Diagnostics.Debug.WriteLine($"@@@ Loading image, data length: {preview.ImageData.Length}");
                await LoadImageAsync(preview.ImageData);
                System.Diagnostics.Debug.WriteLine($"@@@ Image loaded successfully");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"@@@ Not an image or no data, clearing ImagePreview");
                ImagePreview = null;
            }

            StatusMessage = preview.HasError
                ? $"Error: {preview.ErrorMessage}"
                : $"Preview: {fileEntry.Name}";

            System.Diagnostics.Debug.WriteLine($"@@@ LoadPreviewAsync completed successfully");
            _logger.LogDebug("Preview loaded successfully: {Type}", preview.Type);
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"@@@ Preview loading CANCELLED");
            _logger.LogDebug("Preview loading cancelled");
            StatusMessage = "Preview cancelled";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"@@@ LoadPreviewAsync EXCEPTION: {ex.GetType().Name} - {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"@@@ StackTrace: {ex.StackTrace}");
            _logger.LogError(ex, "Failed to load preview");
            CurrentPreview = PreviewContent.Error(fileEntry.FullPath, ex.Message);
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            System.Diagnostics.Debug.WriteLine($"@@@ Setting IsLoading = false");
            IsLoading = false;
        }
    }

    /// <summary>
    /// Calculates MD5 hash for current file
    /// </summary>
    [RelayCommand]
    private async Task CalculateMd5HashAsync()
    {
        await CalculateHashAsync(HashAlgorithmType.MD5);
    }

    /// <summary>
    /// Calculates SHA256 hash for current file
    /// </summary>
    [RelayCommand]
    private async Task CalculateSha256HashAsync()
    {
        await CalculateHashAsync(HashAlgorithmType.SHA256);
    }

    /// <summary>
    /// Opens the current file in the default external application
    /// </summary>
    [RelayCommand]
    private void OpenInExternalViewer()
    {
        if (CurrentPreview == null || string.IsNullOrEmpty(CurrentPreview.FilePath))
        {
            _logger.LogWarning("Cannot open file - no file selected");
            return;
        }

        if (!System.IO.File.Exists(CurrentPreview.FilePath))
        {
            _logger.LogWarning("Cannot open file - file not found: {Path}", CurrentPreview.FilePath);
            System.Windows.MessageBox.Show(
                $"File not found:\n{CurrentPreview.FilePath}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
            return;
        }

        try
        {
            _logger.LogInformation("Opening file in external viewer: {Path}", CurrentPreview.FilePath);

            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = CurrentPreview.FilePath,
                UseShellExecute = true
            };

            System.Diagnostics.Process.Start(processInfo);

            StatusMessage = $"Opened {CurrentPreview.FileName} in external viewer";
            _logger.LogDebug("File opened successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open file in external viewer");
            System.Windows.MessageBox.Show(
                $"Failed to open file:\n{ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            StatusMessage = $"Failed to open file: {ex.Message}";
        }
    }

    private async Task CalculateHashAsync(HashAlgorithmType algorithm)
    {
        if (CurrentPreview == null || string.IsNullOrEmpty(CurrentPreview.FilePath))
            return;

        try
        {
            IsCalculatingHash = true;
            StatusMessage = $"Calculating {algorithm} hash...";

            _logger.LogInformation("Calculating {Algorithm} hash for: {Path}",
                algorithm, CurrentPreview.FilePath);

            var hash = await _previewService.CalculateHashAsync(
                CurrentPreview.FilePath,
                algorithm);

            if (algorithm == HashAlgorithmType.MD5)
            {
                CurrentPreview.Md5Hash = hash;
            }
            else
            {
                CurrentPreview.Sha256Hash = hash;
            }

            // Force UI update
            OnPropertyChanged(nameof(CurrentPreview));

            StatusMessage = $"{algorithm} hash calculated";
            _logger.LogInformation("{Algorithm} hash: {Hash}", algorithm, hash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate hash");
            StatusMessage = $"Hash calculation failed: {ex.Message}";
        }
        finally
        {
            IsCalculatingHash = false;
        }
    }

    /// <summary>
    /// Toggles preview panel visibility
    /// </summary>
    [RelayCommand]
    private void ToggleVisibility()
    {
        IsVisible = !IsVisible;
        _logger.LogDebug("Preview panel visibility toggled: {IsVisible}", IsVisible);
    }

    /// <summary>
    /// Clears current preview
    /// </summary>
    [RelayCommand]
    private void ClearPreview()
    {
        _currentPreviewCts?.Cancel();
        CurrentPreview = PreviewContent.Empty();
        ImagePreview = null;
        StatusMessage = "No file selected";
        _logger.LogDebug("Preview cleared");
    }

    private async Task LoadImageAsync(byte[] imageData)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"@@@ LoadImageAsync START - {imageData.Length} bytes");

            await Task.Run(() =>
            {
                using var stream = new MemoryStream(imageData);
                var bitmap = new BitmapImage();

                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze(); // For cross-thread access

                System.Diagnostics.Debug.WriteLine($"@@@ BitmapImage created: {bitmap.PixelWidth}x{bitmap.PixelHeight}");

                // Must set on UI thread
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"@@@ Setting ImagePreview on UI thread");
                    ImagePreview = bitmap;
                    System.Diagnostics.Debug.WriteLine($"@@@ ImagePreview set successfully");
                });
            });

            _logger.LogDebug("Image loaded successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"@@@ LoadImageAsync EXCEPTION: {ex.Message}");
            _logger.LogError(ex, "Failed to load image into BitmapImage");
            throw;
        }
    }

    /// <summary>
    /// Formats file size for display
    /// </summary>
    public static string FormatFileSize(long bytes)
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