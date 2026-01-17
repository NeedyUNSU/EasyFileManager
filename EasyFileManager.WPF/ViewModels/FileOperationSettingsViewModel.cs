using CommunityToolkit.Mvvm.ComponentModel;
using EasyFileManager.Core.Models;

namespace EasyFileManager.WPF.ViewModels;

/// <summary>
/// ViewModel for File Operations settings section
/// </summary>
public partial class FileOperationSettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _confirmDelete;

    [ObservableProperty]
    private bool _confirmOverwrite;

    [ObservableProperty]
    private bool _useRecycleBin;

    [ObservableProperty]
    private int _copyBufferSizeKB;

    [ObservableProperty]
    private bool _showProgressDialog;

    [ObservableProperty]
    private bool _verifyAfterCopy;

    [ObservableProperty]
    private bool _preserveTimestamps;

    [ObservableProperty]
    private bool _preserveAttributes;

    public FileOperationSettingsViewModel(FileOperationSettings settings)
    {
        _confirmDelete = settings.ConfirmDelete;
        _confirmOverwrite = settings.ConfirmOverwrite;
        _useRecycleBin = settings.UseRecycleBin;
        _copyBufferSizeKB = settings.CopyBufferSizeKB;
        _showProgressDialog = settings.ShowProgressDialog;
        _verifyAfterCopy = settings.VerifyAfterCopy;
        _preserveTimestamps = settings.PreserveTimestamps;
        _preserveAttributes = settings.PreserveAttributes;
    }

    public void ApplyChanges(FileOperationSettings target)
    {
        target.ConfirmDelete = ConfirmDelete;
        target.ConfirmOverwrite = ConfirmOverwrite;
        target.UseRecycleBin = UseRecycleBin;
        target.CopyBufferSizeKB = CopyBufferSizeKB;
        target.ShowProgressDialog = ShowProgressDialog;
        target.VerifyAfterCopy = VerifyAfterCopy;
        target.PreserveTimestamps = PreserveTimestamps;
        target.PreserveAttributes = PreserveAttributes;
    }
}
