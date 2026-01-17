using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EasyFileManager.Core.Interfaces;
using EasyFileManager.Core.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace EasyFileManager.WPF.ViewModels;

/// <summary>
/// ViewModel for managing a collection of tabs in one panel
/// Handles tab creation, closing, switching, reordering, and persistence
/// </summary>
public partial class TabBarViewModel : ViewModelBase
{
    private readonly IAppLogger<TabBarViewModel> _logger;
    private readonly ISettingsService _settingsService;
    private readonly ITabPersistenceService _tabPersistenceService;
    private readonly Func<TabViewModel> _tabViewModelFactory;
    public readonly string _panelId;
    private Action<string>? _onPathChanged;

    [ObservableProperty]
    private ObservableCollection<TabViewModel> _tabs = new();

    [ObservableProperty]
    private TabViewModel? _activeTab;

    [ObservableProperty]
    private int _activeTabIndex = -1;

    /// <summary>
    /// Whether tabs are enabled (more than 1 tab exists)
    /// </summary>
    public bool HasMultipleTabs => Tabs.Count > 1;

    public TabBarViewModel(
        IAppLogger<TabBarViewModel> logger,
        ISettingsService settingsService,
        ITabPersistenceService tabPersistenceService,
        Func<TabViewModel> tabViewModelFactory,
        string panelId)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _tabPersistenceService = tabPersistenceService ?? throw new ArgumentNullException(nameof(tabPersistenceService));
        _tabViewModelFactory = tabViewModelFactory ?? throw new ArgumentNullException(nameof(tabViewModelFactory));
        _panelId = panelId ?? throw new ArgumentNullException(nameof(panelId));

        _logger.LogInformation("TabBarViewModel created for panel: {PanelId}", _panelId);

        // Subscribe to collection changes
        Tabs.CollectionChanged += (s, e) => OnPropertyChanged(nameof(HasMultipleTabs));
    }

    /// <summary>
    /// Initializes tabs - either loads from persistence or creates default tab
    /// </summary>
    public async Task InitializeAsync(string defaultPath)
    {
        _logger.LogInformation("Initializing tabs for panel {PanelId}", _panelId);

        if(!_settingsService.Settings.Behavior.RememberLastSession)
        {
            return;
        }

        // Try to load saved session
        var session = await _tabPersistenceService.LoadSessionAsync(_panelId);

        if (session != null && session.Tabs.Count > 0)
        {
            _logger.LogInformation("Restoring {Count} tabs from saved session", session.Tabs.Count);

            foreach (var tabModel in session.Tabs.OrderBy(t => t.Order))
            {
                var tabVm = _tabViewModelFactory();
                tabVm.Id = tabModel.Id;
                tabVm.Title = tabModel.Title;
                tabVm.Path = tabModel.Path;
                tabVm.SelectedItemPath = tabModel.SelectedItemPath;
                tabVm.SortColumn = tabModel.SortColumn;
                tabVm.SortDirection = tabModel.SortDirection;
                tabVm.FilterText = tabModel.FilterText;
                tabVm.IsPinned = tabModel.IsPinned;
                tabVm.Order = tabModel.Order;

                Tabs.Add(tabVm);
            }

            // Restore active tab
            if (session.ActiveTabId.HasValue)
            {
                var activeTab = Tabs.FirstOrDefault(t => t.Id == session.ActiveTabId.Value);
                if (activeTab != null)
                {
                    await SwitchToTabAsync(activeTab);
                }
                else
                {
                    await SwitchToTabAsync(Tabs[0]);
                }
            }
            else
            {
                await SwitchToTabAsync(Tabs[0]);
            }
        }
        else
        {
            _logger.LogInformation("No saved session found, creating default tab");
            await NewTabAsync();
        }
    }

    /// <summary>
    /// Creates a new tab with the specified path
    /// </summary>
    [RelayCommand]
    private async Task NewTabAsync(string? path = null)
    {
        var newTab = _tabViewModelFactory();
        newTab.Path = path ?? (_panelId == "left" ?
            (String.IsNullOrWhiteSpace(_settingsService.Settings.Behavior.DefaultLeftPanelPath) ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop) : _settingsService.Settings.Behavior.DefaultLeftPanelPath) :
            (String.IsNullOrWhiteSpace(_settingsService.Settings.Behavior.DefaultRightPanelPath) ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop) : _settingsService.Settings.Behavior.DefaultRightPanelPath));
        newTab.Order = Tabs.Count;
        newTab.UpdateTitle();

        Tabs.Add(newTab);
        await SwitchToTabAsync(newTab);

        _logger.LogInformation("Created new tab: {Title} at {Path}", newTab.Title, newTab.Path);

        await SaveSessionAsync();
    }

    /// <summary>
    /// Creates a new tab in the current directory of active tab
    /// </summary>
    [RelayCommand]
    private async Task NewTabInCurrentDirectoryAsync()
    {
        var path = ActiveTab?.Path ?? (_panelId == "left" ?
            (String.IsNullOrWhiteSpace(_settingsService.Settings.Behavior.DefaultLeftPanelPath) ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop) : _settingsService.Settings.Behavior.DefaultLeftPanelPath) :
            (String.IsNullOrWhiteSpace(_settingsService.Settings.Behavior.DefaultRightPanelPath) ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop) : _settingsService.Settings.Behavior.DefaultRightPanelPath));
        await NewTabAsync(path);
    }

    /// <summary>
    /// Duplicates the active tab
    /// </summary>
    [RelayCommand]
    private async Task DuplicateActiveTabAsync()
    {
        if (ActiveTab == null)
            return;

        var duplicateTab = _tabViewModelFactory();
        duplicateTab.Path = ActiveTab.Path;
        duplicateTab.SelectedItemPath = ActiveTab.SelectedItemPath;
        duplicateTab.SortColumn = ActiveTab.SortColumn;
        duplicateTab.SortDirection = ActiveTab.SortDirection;
        duplicateTab.FilterText = ActiveTab.FilterText;
        duplicateTab.Order = ActiveTab.Order + 1;
        duplicateTab.UpdateTitle();

        // Insert after active tab
        var index = Tabs.IndexOf(ActiveTab);
        Tabs.Insert(index + 1, duplicateTab);

        // Update order for tabs after insertion point
        UpdateTabOrders();

        await SwitchToTabAsync(duplicateTab);

        _logger.LogInformation("Duplicated tab: {Title}", duplicateTab.Title);

        await SaveSessionAsync();
    }

    /// <summary>
    /// Closes the specified tab
    /// </summary>
    [RelayCommand]
    private async Task CloseTabAsync(TabViewModel? tab)
    {
        if (tab == null)
            return;

        // Prevent closing pinned tabs
        if (tab.IsPinned)
        {
            _logger.LogWarning("Attempted to close pinned tab: {Title}", tab.Title);
            MessageBox.Show(
                $"Cannot close pinned tab '{tab.Title}'. Unpin it first.",
                "Pinned Tab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        // Prevent closing last tab
        if (Tabs.Count == 1)
        {
            _logger.LogWarning("Attempted to close last tab");
            MessageBox.Show(
                "Cannot close the last tab.",
                "Close Tab",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var index = Tabs.IndexOf(tab);
        var wasActive = tab.IsActive;

        Tabs.Remove(tab);

        _logger.LogInformation("Closed tab: {Title}", tab.Title);

        // If we closed the active tab, activate another one
        if (wasActive)
        {
            // Try to activate the tab to the right, or left if we closed the last tab
            var newIndex = Math.Min(index, Tabs.Count - 1);
            if (newIndex >= 0)
            {
                await SwitchToTabAsync(Tabs[newIndex]);
            }
        }

        UpdateTabOrders();
        await SaveSessionAsync();
    }

    /// <summary>
    /// Closes all tabs except the specified one
    /// </summary>
    [RelayCommand]
    private async Task CloseOtherTabsAsync(TabViewModel? tab)
    {
        if (tab == null)
            return;

        var tabsToClose = Tabs.Where(t => t != tab && !t.IsPinned).ToList();

        foreach (var tabToClose in tabsToClose)
        {
            Tabs.Remove(tabToClose);
        }

        _logger.LogInformation("Closed {Count} other tabs", tabsToClose.Count);

        await SwitchToTabAsync(tab);
        UpdateTabOrders();
        await SaveSessionAsync();
    }

    /// <summary>
    /// Closes all tabs to the right of the specified tab
    /// </summary>
    [RelayCommand]
    private async Task CloseTabsToRightAsync(TabViewModel? tab)
    {
        if (tab == null)
            return;

        var index = Tabs.IndexOf(tab);
        var tabsToClose = Tabs.Skip(index + 1).Where(t => !t.IsPinned).ToList();

        foreach (var tabToClose in tabsToClose)
        {
            Tabs.Remove(tabToClose);
        }

        _logger.LogInformation("Closed {Count} tabs to the right", tabsToClose.Count);

        UpdateTabOrders();
        await SaveSessionAsync();
    }

    /// <summary>
    /// Toggles pin status for the specified tab
    /// </summary>
    [RelayCommand]
    private async Task TogglePinAsync(TabViewModel? tab)
    {
        if (tab == null)
            return;

        tab.IsPinned = !tab.IsPinned;

        _logger.LogInformation("Tab {Title} pinned: {IsPinned}", tab.Title, tab.IsPinned);

        await SaveSessionAsync();
    }

    /// <summary>
    /// Switches to the specified tab
    /// </summary>
    public async Task SwitchToTabAsync(TabViewModel tab)
    {
        if (tab == null || tab == ActiveTab)
            return;

        // Deactivate current tab
        if (ActiveTab != null)
        {
            ActiveTab.IsActive = false;
        }

        // Activate new tab
        ActiveTab = tab;
        ActiveTab.IsActive = true;
        ActiveTabIndex = Tabs.IndexOf(tab);

        _logger.LogDebug("Switched to tab: {Title}", tab.Title);

        _onPathChanged?.Invoke(tab.Path);

        await Task.CompletedTask;
    }

    /// <summary>
    /// Switches to next tab (Ctrl+Tab)
    /// </summary>
    [RelayCommand]
    private async Task NextTabAsync()
    {
        if (Tabs.Count <= 1)
            return;

        var currentIndex = ActiveTabIndex;
        var nextIndex = (currentIndex + 1) % Tabs.Count;

        await SwitchToTabAsync(Tabs[nextIndex]);
    }

    /// <summary>
    /// Switches to previous tab (Ctrl+Shift+Tab)
    /// </summary>
    [RelayCommand]
    private async Task PreviousTabAsync()
    {
        if (Tabs.Count <= 1)
            return;

        var currentIndex = ActiveTabIndex;
        var prevIndex = currentIndex - 1;
        if (prevIndex < 0)
            prevIndex = Tabs.Count - 1;

        await SwitchToTabAsync(Tabs[prevIndex]);
    }

    /// <summary>
    /// Switches to the specified tab (Command version)
    /// </summary>
    [RelayCommand]
    private async Task SwitchToTab(TabViewModel? tab)
    {
        if (tab == null)
            return;

        await SwitchToTabAsync(tab);
    }

    /// <summary>
    /// Switches to tab by index (Ctrl+1-9)
    /// </summary>
    public async Task SwitchToTabByIndexAsync(int index)
    {
        if (index >= 0 && index < Tabs.Count)
        {
            await SwitchToTabAsync(Tabs[index]);
        }
    }

    /// <summary>
    /// Updates the Order property for all tabs based on their position
    /// </summary>
    private void UpdateTabOrders()
    {
        for (int i = 0; i < Tabs.Count; i++)
        {
            Tabs[i].Order = i;
        }
    }

    /// <summary>
    /// Saves the current tab session to persistence
    /// </summary>
    public async Task SaveSessionAsync()
    {
        if (!_settingsService.Settings.Behavior.RememberLastSession)
        {
            return;
        }

        try
        {
            var session = new TabSession
            {
                Tabs = Tabs.Select(t => t.ToModel()).ToList(),
                ActiveTabId = ActiveTab?.Id
            };

            await _tabPersistenceService.SaveSessionAsync(session, _panelId);

            _logger.LogDebug("Saved tab session for panel {PanelId}", _panelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save tab session");
            // Don't throw - saving session is not critical
        }
    }

    /// <summary>
    /// Updates the current tab's path (called when FileExplorerViewModel navigates)
    /// </summary>
    public void UpdateActiveTabPath(string path)
    {
        if (ActiveTab != null)
        {
            ActiveTab.Path = path;
            _ = SaveSessionAsync();
        }
    }

    /// <summary>
    /// Updates the current tab's selection
    /// </summary>
    public void UpdateActiveTabSelection(string? selectedItemPath)
    {
        if (ActiveTab != null)
        {
            ActiveTab.SelectedItemPath = selectedItemPath;
            _ = SaveSessionAsync();
        }
    }

    partial void OnActiveTabChanged(TabViewModel? value)
    {
        if (value != null)
        {
            ActiveTabIndex = Tabs.IndexOf(value);
        }
    }

    /// <summary>
    /// Sets callback for when active tab path changes
    /// Called by FileExplorerViewModel to sync navigation
    /// </summary>
    public void SetPathChangedCallback(Action<string> callback)
    {
        _onPathChanged = callback;
    }
}