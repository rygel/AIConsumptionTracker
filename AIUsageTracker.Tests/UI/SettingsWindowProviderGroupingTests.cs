// <copyright file="SettingsWindowProviderGroupingTests.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using AIUsageTracker.Core.MonitorClient;
using AIUsageTracker.UI.Slim;

namespace AIUsageTracker.Tests.UI;

public sealed class SettingsWindowProviderGroupingTests
{
    [Fact]
    public void ShouldRenderAsSettingsSubItem_ReturnsFalse_ForCodexSparkDerived()
    {
        var result = SettingsWindow.ShouldRenderAsSettingsSubItem("codex.spark", isDerived: true);

        Assert.False(result);
    }

    [Fact]
    public void ShouldRenderAsSettingsSubItem_ReturnsTrue_ForAntigravityDerivedChild()
    {
        var result = SettingsWindow.ShouldRenderAsSettingsSubItem("antigravity.some-model", isDerived: true);

        Assert.True(result);
    }

    [Fact]
    public void ShouldRenderAsSettingsSubItem_UsesProviderMetadata_WhenSnapshotTriesToOverride()
    {
        var capabilities = new AgentProviderCapabilitiesSnapshot
        {
            Providers =
            [
                new AgentProviderCapabilityDefinition
                {
                    ProviderId = "codex",
                    DisplayName = "OpenAI (Codex)",
                    SupportsChildProviderIds = true,
                    CollapseDerivedChildrenInMainWindow = true,
                    HandledProviderIds = ["codex", "codex.spark"],
                },
            ],
        };

        var result = SettingsWindow.ShouldRenderAsSettingsSubItem(
            "codex.spark",
            isDerived: true,
            capabilities);

        Assert.False(result);
    }
}
