// <copyright file="ProviderCapabilityCatalog.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using AIUsageTracker.Core.MonitorClient;
using AIUsageTracker.Infrastructure.Providers;

namespace AIUsageTracker.UI.Slim;

internal static class ProviderCapabilityCatalog
{
    public static bool ShouldShowInMainWindow(string providerId, AgentProviderCapabilitiesSnapshot? snapshot)
    {
        return ProviderMetadataCatalog.ShouldShowInMainWindow(providerId);
    }

    public static bool ShouldShowInSettings(string providerId, AgentProviderCapabilitiesSnapshot? snapshot)
    {
        return ProviderMetadataCatalog.ShouldShowInSettings(providerId);
    }

    public static bool SupportsAccountIdentity(string providerId, AgentProviderCapabilitiesSnapshot? snapshot)
    {
        return ProviderMetadataCatalog.SupportsAccountIdentity(providerId);
    }

    public static bool IsVisibleDerivedProviderId(string providerId, AgentProviderCapabilitiesSnapshot? snapshot)
    {
        return ProviderMetadataCatalog.IsVisibleDerivedProviderId(providerId);
    }

    public static IReadOnlyList<string> GetDefaultSettingsProviderIds(AgentProviderCapabilitiesSnapshot? snapshot)
    {
        return ProviderMetadataCatalog.GetDefaultSettingsProviderIds();
    }

    public static string GetCanonicalProviderId(string providerId, AgentProviderCapabilitiesSnapshot? snapshot)
    {
        return ProviderMetadataCatalog.GetCanonicalProviderId(providerId);
    }

    public static string GetDisplayName(
        string providerId,
        string? providerName,
        AgentProviderCapabilitiesSnapshot? snapshot)
    {
        return ProviderMetadataCatalog.GetDisplayName(providerId, providerName);
    }

    public static string GetDisplayName(string providerId, AgentProviderCapabilitiesSnapshot? snapshot)
    {
        return GetDisplayName(providerId, providerName: null, snapshot);
    }

    public static bool ShouldCollapseDerivedChildrenInMainWindow(
        string providerId,
        AgentProviderCapabilitiesSnapshot? snapshot)
    {
        return ProviderMetadataCatalog.ShouldCollapseDerivedChildrenInMainWindow(providerId);
    }

    public static bool ShouldRenderAggregateDetailsInMainWindow(
        string providerId,
        AgentProviderCapabilitiesSnapshot? snapshot)
    {
        return ProviderMetadataCatalog.ShouldRenderAggregateDetailsInMainWindow(providerId);
    }

    public static bool ShouldUseSharedSubDetailCollapsePreference(
        string providerId,
        AgentProviderCapabilitiesSnapshot? snapshot)
    {
        var canonicalProviderId = GetCanonicalProviderId(providerId, snapshot);
        return ShouldCollapseDerivedChildrenInMainWindow(canonicalProviderId, snapshot);
    }

    public static bool ShouldRenderAsSettingsSubItem(string providerId, AgentProviderCapabilitiesSnapshot? snapshot)
    {
        var canonicalProviderId = GetCanonicalProviderId(providerId, snapshot);
        var isCanonicalChild = !string.Equals(canonicalProviderId, providerId, StringComparison.OrdinalIgnoreCase);
        return isCanonicalChild && ShouldUseSharedSubDetailCollapsePreference(canonicalProviderId, snapshot);
    }

    public static bool HasVisibleDerivedProviders(string providerId, AgentProviderCapabilitiesSnapshot? snapshot)
    {
        return ProviderMetadataCatalog.TryGet(providerId, out var definition) &&
               definition.VisibleDerivedProviderIds.Count > 0;
    }
}
