// <copyright file="ProviderCapabilityCatalogTests.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using AIUsageTracker.Core.MonitorClient;
using AIUsageTracker.UI.Slim;

namespace AIUsageTracker.Tests.UI;

public sealed class ProviderCapabilityCatalogTests
{
    [Fact]
    public void ShouldShowInSettings_UsesSnapshotOverride_WhenPresent()
    {
        var snapshot = new AgentProviderCapabilitiesSnapshot
        {
            Providers =
            [
                new AgentProviderCapabilityDefinition
                {
                    ProviderId = "codex",
                    DisplayName = "OpenAI (Codex)",
                    ShowInSettings = false,
                    HandledProviderIds = ["codex"],
                },
            ],
        };

        var result = ProviderCapabilityCatalog.ShouldShowInSettings("codex", snapshot);

        Assert.False(result);
    }

    [Fact]
    public void ShouldShowInSettings_FallsBackToMetadata_WhenSnapshotMissing()
    {
        var result = ProviderCapabilityCatalog.ShouldShowInSettings("codex", snapshot: null);

        Assert.True(result);
    }

    [Fact]
    public void GetCanonicalProviderId_UsesSnapshotHandledChildIds()
    {
        var snapshot = new AgentProviderCapabilitiesSnapshot
        {
            Providers =
            [
                new AgentProviderCapabilityDefinition
                {
                    ProviderId = "antigravity",
                    DisplayName = "Antigravity",
                    SupportsChildProviderIds = true,
                    HandledProviderIds = ["antigravity"],
                },
            ],
        };

        var canonical = ProviderCapabilityCatalog.GetCanonicalProviderId("antigravity.gpt-5", snapshot);

        Assert.Equal("antigravity", canonical);
    }

    [Fact]
    public void GetDefaultSettingsProviderIds_UsesSnapshotPayload()
    {
        var snapshot = new AgentProviderCapabilitiesSnapshot
        {
            Providers =
            [
                new AgentProviderCapabilityDefinition
                {
                    ProviderId = "codex",
                    DisplayName = "OpenAI (Codex)",
                    ShowInSettings = true,
                    HandledProviderIds = ["codex"],
                    SettingsAdditionalProviderIds = ["codex.spark"],
                },
            ],
        };

        var providerIds = ProviderCapabilityCatalog.GetDefaultSettingsProviderIds(snapshot);

        Assert.Equal(["codex", "codex.spark"], providerIds);
    }
}
