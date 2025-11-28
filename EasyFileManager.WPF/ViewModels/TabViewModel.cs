using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;
using System;
using System.DirectoryServices;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

namespace EasyFileManager.WPF.ViewModels;

/// <summary>
/// ViewModel for a single tab
/// Represents one tab in the tab bar with its state and behavior
/// </summary>
public partial class TabViewModel : ViewModelBase
{
    private readonly IAppLogger<TabViewModel> _logger;

    [ObservableProperty]
    private Guid _id;

    [ObservableProperty]
    private string _title = "New Tab";

    [ObservableProperty]
    private string _path = string.Empty;

    [ObservableProperty]
    private string? _selectedItemPath;

    [ObservableProperty]
    private string _sortColumn = "Name";

    [ObservableProperty]
    private string _sortDirection = "Ascending";

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private int _order;

    /// <summary>
    /// Tooltip showing full path
    /// </summary>
    public string ToolTip => Path;

    /// <summary>
    /// Shortened title for display (max 20 chars)
    /// </summary>
    public string DisplayTitle
    {
        get
        {
            if (Title.Length <= 20)
                return Title;
            return Title.Substring(0, 17) + "...";
        }
    }

    public TabViewModel(IAppLogger<TabViewModel> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Id = Guid.NewGuid();
    }

    /// <summary>
    /// Creates a TabViewModel from TabModel
    /// </summary>
    public static TabViewModel FromModel(TabModel model, IAppLogger<TabViewModel> logger)
    {
        return new TabViewModel(logger)
        {
            Id = model.Id,
            Title = model.Title,
            Path = model.Path,
            SelectedItemPath = model.SelectedItemPath,
            SortColumn = model.SortColumn,
            SortDirection = model.SortDirection,
            FilterText = model.FilterText,
            IsPinned = model.IsPinned,
            Order = model.Order
        };
    }

    /// <summary>
    /// Converts this ViewModel to TabModel for persistence
    /// </summary>
    public TabModel ToModel()
    {
        return new TabModel
        {
            Id = Id,
            Title = Title,
            Path = Path,
            SelectedItemPath = SelectedItemPath,
            SortColumn = SortColumn,
            SortDirection = SortDirection,
            FilterText = FilterText,
            IsPinned = IsPinned,
            Order = Order
        };
    }

    /// <summary>
    /// Updates the title based on current path
    /// </summary>
    public void UpdateTitle()
    {
        var dirName = System.IO.Path.GetFileName(Path.TrimEnd('\\', '/'));
        if (string.IsNullOrEmpty(dirName))
        {
            dirName = Path.TrimEnd('\\');
        }
        Title = dirName;
        OnPropertyChanged(nameof(DisplayTitle));
        OnPropertyChanged(nameof(ToolTip));
    }

    partial void OnPathChanged(string value)
    {
        UpdateTitle();
        _logger.LogDebug("Tab path changed: {Path}", value);
    }

    partial void OnIsActiveChanged(bool value)
    {
        _logger.LogDebug("Tab {Title} active state: {IsActive}", Title, value);
    }

    partial void OnIsPinnedChanged(bool value)
    {
        _logger.LogDebug("Tab {Title} pinned: {IsPinned}", Title, value);
    }
}