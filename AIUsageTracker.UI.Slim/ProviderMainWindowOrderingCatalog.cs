// <copyright file="ProviderMainWindowOrderingCatalog.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using AIUsageTracker.Core.Models;
using AIUsageTracker.Core.MonitorClient;

namespace AIUsageTracker.UI.Slim;

internal static class ProviderMainWindowOrderingCatalog
{
    public static IEnumerable<ProviderUsage> OrderForMainWindow(
        IEnumerable<ProviderUsage> usages,
        AgentProviderCapabilitiesSnapshot? capabilities)
    {
        return usages
            .OrderByDescending(usage => usage.IsQuotaBased)
            .ThenBy(
                usage => GetFamilyDisplayName(usage, capabilities),
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(
                usage => ProviderCapabilityCatalog.GetDisplayName(
                    usage.ProviderId ?? string.Empty,
                    usage.ProviderName,
                    capabilities),
                StringComparer.OrdinalIgnoreCase)
            .ThenBy(usage => usage.ProviderId ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }

    private static string GetFamilyDisplayName(
        ProviderUsage usage,
        AgentProviderCapabilitiesSnapshot? capabilities)
    {
        var providerId = usage.ProviderId ?? string.Empty;
        var canonicalProviderId = ProviderCapabilityCatalog.GetCanonicalProviderId(providerId, capabilities);
        return ProviderCapabilityCatalog.GetDisplayName(canonicalProviderId, providerName: null, capabilities);
    }
}
