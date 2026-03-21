// <copyright file="StartupPreferencesService.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using AIUsageTracker.Core.Models;
using Microsoft.Extensions.Logging;

namespace AIUsageTracker.UI.Slim.Services;

public sealed class StartupPreferencesService
{
    private readonly IUiPreferencesStore _preferencesStore;
    private readonly ILogger<StartupPreferencesService> _logger;

    public StartupPreferencesService(
        IUiPreferencesStore preferencesStore,
        ILogger<StartupPreferencesService> logger)
    {
        this._preferencesStore = preferencesStore;
        this._logger = logger;
    }

    public async Task<AppPreferences> LoadAsync()
    {
        try
        {
            return await this._preferencesStore.LoadAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            this._logger.LogWarning(ex, "Failed to load Slim preferences on startup.");
            UiDiagnosticFileLog.Write($"[DIAGNOSTIC] Failed to load preferences on startup: {ex.Message}");
            return new AppPreferences();
        }
    }
}
