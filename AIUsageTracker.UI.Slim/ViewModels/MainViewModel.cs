// <copyright file="MainViewModel.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using System.Collections.ObjectModel;
using AIUsageTracker.Core.Interfaces;
using AIUsageTracker.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AIUsageTracker.UI.Slim.ViewModels;

/// <summary>
/// ViewModel for the main window, managing usage data display and refresh operations.
/// </summary>
public partial class MainViewModel : BaseViewModel
{
    private readonly IMonitorService _monitorService;
    private readonly IUsageAnalyticsService _analyticsService;
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isPrivacyMode;

    [ObservableProperty]
    private string _statusMessage = "Initializing...";

    [ObservableProperty]
    private ObservableCollection<ProviderUsage> _usages = new();

    [ObservableProperty]
    private DateTime _lastRefreshTime = DateTime.MinValue;

    public MainViewModel(
        IMonitorService monitorService,
        IUsageAnalyticsService analyticsService,
        ILogger<MainViewModel> logger)
    {
        this._monitorService = monitorService;
        this._analyticsService = analyticsService;
        this._logger = logger;
        this._isPrivacyMode = false;
    }

    [RelayCommand]
    internal async Task RefreshDataAsync()
    {
        if (this.IsLoading)
        {
            return;
        }

        this.IsLoading = true;
        this.StatusMessage = "Refreshing data...";
        try
        {
            await this._monitorService.RefreshPortAsync().ConfigureAwait(true);
            var results = await this._monitorService.GetUsageAsync().ConfigureAwait(true);

            this.Usages.Clear();
            foreach (var usage in results)
            {
                this.Usages.Add(usage);
            }

            this.LastRefreshTime = DateTime.Now;
            this.StatusMessage = results.Any() ? "Data updated" : "No active providers found";
        }
        catch (Exception ex)
        {
            this._logger.LogError(ex, "Failed to refresh data in MainViewModel");
            this.StatusMessage = "Connection failed";
        }
        finally
        {
            this.IsLoading = false;
        }
    }

    [RelayCommand]
    private void TogglePrivacyMode()
    {
        this.IsPrivacyMode = !this.IsPrivacyMode;
    }

    public void SetPrivacyMode(bool enabled)
    {
        this.IsPrivacyMode = enabled;
    }
}
