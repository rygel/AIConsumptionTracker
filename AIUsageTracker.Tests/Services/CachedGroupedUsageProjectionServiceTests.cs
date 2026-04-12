// <copyright file="CachedGroupedUsageProjectionServiceTests.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using AIUsageTracker.Core.Models;
using AIUsageTracker.Monitor.Services;
using Moq;

namespace AIUsageTracker.Tests.Services;

public sealed class CachedGroupedUsageProjectionServiceTests
{
    [Fact]
    public async Task GetGroupedUsage_ExcludesUnconfiguredStandardApiKeyProviders_FromSnapshot()
    {
        // Providers that are StandardApiKey mode with no API key (e.g. OpenRouter, Xiaomi when
        // unconfigured) must not appear in the main-window snapshot even if they have stale
        // history rows in the database, because the database never persists State and those
        // rows always deserialise with State=Available.
        var dbUsages = new List<ProviderUsage>
        {
            // openrouter row with no state info (as it comes from the DB)
            new() { ProviderId = "openrouter", ProviderName = "OpenRouter", IsAvailable = false, Description = "API Key missing." },
            // A configured provider that should appear
            new() { ProviderId = "mistral", ProviderName = "Mistral", IsAvailable = true, UsedPercent = 20 },
        };

        var configs = new List<ProviderConfig>
        {
            // openrouter has no API key → unconfigured StandardApiKey provider
            new() { ProviderId = "openrouter", ApiKey = string.Empty },
            // mistral has a key → should appear
            new() { ProviderId = "mistral", ApiKey = "sk-test-key" },
        };

        var mockDb = new Mock<IUsageDatabase>();
        mockDb.Setup(d => d.GetLatestHistoryAsync()).ReturnsAsync(dbUsages);

        var mockConfig = new Mock<IConfigService>();
        mockConfig.Setup(c => c.GetConfigsAsync()).ReturnsAsync(configs);

        var service = new CachedGroupedUsageProjectionService(mockDb.Object, mockConfig.Object);

        var snapshot = await service.GetGroupedUsageAsync().ConfigureAwait(false);

        // OpenRouter must not be in the snapshot
        Assert.DoesNotContain(snapshot.Providers, p =>
            string.Equals(p.ProviderId, "openrouter", StringComparison.OrdinalIgnoreCase));

        // Mistral must still be present
        Assert.Contains(snapshot.Providers, p =>
            string.Equals(p.ProviderId, "mistral", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetGroupedUsage_IncludesStandardApiKeyProviders_WhenApiKeyIsConfigured()
    {
        // A StandardApiKey provider WITH a key should appear in the snapshot normally.
        var dbUsages = new List<ProviderUsage>
        {
            new() { ProviderId = "openrouter", ProviderName = "OpenRouter", IsAvailable = true, UsedPercent = 50 },
        };

        var configs = new List<ProviderConfig>
        {
            new() { ProviderId = "openrouter", ApiKey = "sk-or-real-key" },
        };

        var mockDb = new Mock<IUsageDatabase>();
        mockDb.Setup(d => d.GetLatestHistoryAsync()).ReturnsAsync(dbUsages);

        var mockConfig = new Mock<IConfigService>();
        mockConfig.Setup(c => c.GetConfigsAsync()).ReturnsAsync(configs);

        var service = new CachedGroupedUsageProjectionService(mockDb.Object, mockConfig.Object);

        var snapshot = await service.GetGroupedUsageAsync().ConfigureAwait(false);

        Assert.Contains(snapshot.Providers, p =>
            string.Equals(p.ProviderId, "openrouter", StringComparison.OrdinalIgnoreCase));
    }
}
