using CommunityToolkit.Mvvm.ComponentModel;
using EasyFileManager.Core.Models;
using Microsoft.Extensions.Logging;

namespace EasyFileManager.WPF.ViewModels;

/// <summary>
/// ViewModel for Advanced settings section
/// </summary>
public partial class AdvancedSettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _enableLogging;

    [ObservableProperty]
    private string _logLevel;

    [ObservableProperty]
    private int _logRetentionDays;

    [ObservableProperty]
    private bool _enableCrashReports;

    [ObservableProperty]
    private bool _autoCheckUpdates;

    [ObservableProperty]
    private string _language;

    [ObservableProperty]
    private bool _enablePerformanceMonitoring;

    [ObservableProperty]
    private bool _hardwareAcceleration;

    public AdvancedSettingsViewModel(AdvancedSettings settings)
    {
        _enableLogging = settings.EnableLogging;
        _logLevel = settings.LogLevel;
        _logRetentionDays = settings.LogRetentionDays;
        _enableCrashReports = settings.EnableCrashReports;
        _autoCheckUpdates = settings.AutoCheckUpdates;
        _language = settings.Language;
        _enablePerformanceMonitoring = settings.EnablePerformanceMonitoring;
        _hardwareAcceleration = settings.HardwareAcceleration;
    }

    public void ApplyChanges(AdvancedSettings target)
    {
        target.EnableLogging = EnableLogging;
        target.LogLevel = LogLevel;
        target.LogRetentionDays = LogRetentionDays;
        target.EnableCrashReports = EnableCrashReports;
        target.AutoCheckUpdates = AutoCheckUpdates;
        target.Language = Language;
        target.EnablePerformanceMonitoring = EnablePerformanceMonitoring;
        target.HardwareAcceleration = HardwareAcceleration;
    }
}
