// <copyright file="SettingsWindowFactory.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using AIUsageTracker.Core.Interfaces;
using AIUsageTracker.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace AIUsageTracker.UI.Slim.Services;

public sealed class SettingsWindowFactory : ISettingsWindowFactory
{
    private readonly IMonitorService _monitorService;
    private readonly IMonitorLifecycleService _monitorLifecycleService;
    private readonly ILogger<SettingsWindow> _logger;
    private readonly UiPreferencesStore _preferencesStore;
    private readonly IAppPathProvider _pathProvider;
    private readonly DisplayPreferencesService _displayPreferences;

    public SettingsWindowFactory(
        IMonitorService monitorService,
        IMonitorLifecycleService monitorLifecycleService,
        ILogger<SettingsWindow> logger,
        UiPreferencesStore preferencesStore,
        IAppPathProvider pathProvider,
        DisplayPreferencesService displayPreferences)
    {
        this._monitorService = monitorService;
        this._monitorLifecycleService = monitorLifecycleService;
        this._logger = logger;
        this._preferencesStore = preferencesStore;
        this._pathProvider = pathProvider;
        this._displayPreferences = displayPreferences;
    }

    public SettingsWindow Create()
    {
        return new SettingsWindow(
            this._monitorService,
            this._monitorLifecycleService,
            this._logger,
            this._preferencesStore,
            this._pathProvider,
            this._displayPreferences);
    }
}
