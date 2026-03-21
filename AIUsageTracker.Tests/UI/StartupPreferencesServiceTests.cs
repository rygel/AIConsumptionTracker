// <copyright file="StartupPreferencesServiceTests.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using AIUsageTracker.Core.Models;
using AIUsageTracker.UI.Slim;
using AIUsageTracker.UI.Slim.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AIUsageTracker.Tests.UI;

public class StartupPreferencesServiceTests
{
    [Fact]
    public async Task LoadAsync_WhenStoreSucceeds_ReturnsPreferencesAsync()
    {
        var expected = new AppPreferences
        {
            Theme = AppTheme.Nord,
            IsPrivacyMode = true,
        };
        var store = new FakePreferencesStore(() => Task.FromResult(expected));
        var sut = new StartupPreferencesService(
            store,
            NullLogger<StartupPreferencesService>.Instance);

        var result = await sut.LoadAsync();

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task LoadAsync_WhenStoreThrows_ReturnsDefaultsAsync()
    {
        var store = new FakePreferencesStore(() => throw new InvalidOperationException("boom"));
        var sut = new StartupPreferencesService(
            store,
            NullLogger<StartupPreferencesService>.Instance);

        var result = await sut.LoadAsync();

        Assert.NotNull(result);
        Assert.False(result.IsPrivacyMode);
    }

    private sealed class FakePreferencesStore : IUiPreferencesStore
    {
        private readonly Func<Task<AppPreferences>> _loadFunc;

        public FakePreferencesStore(Func<Task<AppPreferences>> loadFunc)
        {
            this._loadFunc = loadFunc;
        }

        public Task<AppPreferences> LoadAsync()
        {
            return this._loadFunc();
        }

        public Task<bool> SaveAsync(AppPreferences preferences)
        {
            return Task.FromResult(false);
        }
    }
}
