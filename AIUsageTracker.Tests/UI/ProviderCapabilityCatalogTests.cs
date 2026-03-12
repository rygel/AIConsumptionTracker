// <copyright file="ProviderCapabilityCatalogTests.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using AIUsageTracker.Core.MonitorClient;
using AIUsageTracker.UI.Slim;

namespace AIUsageTracker.Tests.UI;

public sealed class ProviderCapabilityCatalogTests
{
    [Fact]
    public void ShouldShowInSettings_UsesProviderMetadata_WhenSnapshotTriesToOverride()
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

        Assert.True(result);
    }

    [Fact]
    public void ShouldShowInSettings_UsesProviderMetadata_WhenSnapshotMissing()
    {
        var result = ProviderCapabilityCatalog.ShouldShowInSettings("codex", snapshot: null);

        Assert.True(result);
    }

    [Fact]
    public void GetCanonicalProviderId_UsesProviderMetadata()
    {
        var canonical = ProviderCapabilityCatalog.GetCanonicalProviderId(
            "antigravity.gpt-5",
            snapshot: null);

        Assert.Equal("antigravity", canonical);
    }

    [Fact]
    public void GetDefaultSettingsProviderIds_UsesProviderMetadata_WhenSnapshotProvided()
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

        Assert.Contains("codex", providerIds);
        Assert.Contains("codex.spark", providerIds);
    }

    [Fact]
    public void GetDisplayName_PreservesDerivedProviderName_WhenProvided()
    {
        var snapshot = new AgentProviderCapabilitiesSnapshot
        {
            Providers =
            [
                new AgentProviderCapabilityDefinition
                {
                    ProviderId = "antigravity",
                    DisplayName = "Google Antigravity",
                    SupportsChildProviderIds = true,
                    HandledProviderIds = ["antigravity"],
                },
            ],
        };

        var result = ProviderCapabilityCatalog.GetDisplayName(
            "antigravity.gpt-oss",
            "GPT OSS (Anti-Gravity)",
            snapshot);

        Assert.Equal("GPT OSS (Anti-Gravity)", result);
    }

    [Fact]
    public void SupportsAccountIdentity_UsesProviderMetadata_WhenSnapshotTriesToOverride()
    {
        var snapshot = new AgentProviderCapabilitiesSnapshot
        {
            Providers =
            [
                new AgentProviderCapabilityDefinition
                {
                    ProviderId = "github-copilot",
                    SupportsAccountIdentity = false,
                    HandledProviderIds = ["github-copilot"],
                },
            ],
        };

        var result = ProviderCapabilityCatalog.SupportsAccountIdentity("github-copilot", snapshot);

        Assert.True(result);
    }

    [Fact]
    public void SupportsAccountIdentity_UsesProviderMetadata_WhenSnapshotMissing()
    {
        var result = ProviderCapabilityCatalog.SupportsAccountIdentity("github-copilot", snapshot: null);

        Assert.True(result);
    }
}
