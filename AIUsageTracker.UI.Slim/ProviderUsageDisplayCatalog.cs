// <copyright file="ProviderUsageDisplayCatalog.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using AIUsageTracker.Core.Models;
using AIUsageTracker.Core.MonitorClient;

namespace AIUsageTracker.UI.Slim;

internal static class ProviderUsageDisplayCatalog
{
    public static ProviderRenderPreparation PrepareForMainWindow(
        IReadOnlyCollection<ProviderUsage> usages,
        AgentProviderCapabilitiesSnapshot? capabilities = null)
    {
        var filteredUsages = usages
            .Where(usage => ProviderCapabilityCatalog.ShouldShowInMainWindow(usage.ProviderId ?? string.Empty, capabilities))
            .ToList();
        var hasAntigravityParent = filteredUsages.Any(usage => IsAntigravityParent(usage, capabilities));
        var collapsedParentProviderIds = ResolveCollapsedParentProviderIds(filteredUsages, capabilities);

        filteredUsages = filteredUsages
            .Where(ShouldDisplayUsage(collapsedParentProviderIds, capabilities))
            .GroupBy(usage => usage.ProviderId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        return new ProviderRenderPreparation(filteredUsages, hasAntigravityParent);
    }

    public static IReadOnlyList<ProviderUsage> CreateAntigravityModelUsages(ProviderUsage parentUsage)
    {
        if (parentUsage.Details?.Any() != true)
        {
            return Array.Empty<ProviderUsage>();
        }

        return parentUsage.Details
            .Where(detail => !string.IsNullOrWhiteSpace(detail.Name) && !detail.Name.StartsWith("[", StringComparison.Ordinal))
            .GroupBy(detail => detail.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(detail => detail.Name, StringComparer.OrdinalIgnoreCase)
            .Select(detail => CreateAntigravityModelUsage(detail, parentUsage))
            .ToList();
    }

    private static HashSet<string> ResolveCollapsedParentProviderIds(
        IEnumerable<ProviderUsage> usages,
        AgentProviderCapabilitiesSnapshot? capabilities)
    {
        return usages
            .Where(usage =>
            {
                var providerId = usage.ProviderId ?? string.Empty;
                var canonicalProviderId = ProviderCapabilityCatalog.GetCanonicalProviderId(providerId, capabilities);
                return string.Equals(providerId, canonicalProviderId, StringComparison.OrdinalIgnoreCase) &&
                       ProviderCapabilityCatalog.ShouldCollapseDerivedChildrenInMainWindow(providerId, capabilities);
            })
            .Select(usage => usage.ProviderId ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static Func<ProviderUsage, bool> ShouldDisplayUsage(
        IReadOnlySet<string> collapsedParentProviderIds,
        AgentProviderCapabilitiesSnapshot? capabilities)
    {
        return usage =>
        {
            var providerId = usage.ProviderId ?? string.Empty;
            var canonicalProviderId = ProviderCapabilityCatalog.GetCanonicalProviderId(providerId, capabilities);
            var isDerivedChild = !string.Equals(providerId, canonicalProviderId, StringComparison.OrdinalIgnoreCase);
            return !isDerivedChild || !collapsedParentProviderIds.Contains(canonicalProviderId);
        };
    }

    private static bool IsAntigravityParent(ProviderUsage usage, AgentProviderCapabilitiesSnapshot? capabilities)
    {
        return ProviderCapabilityCatalog.ShouldRenderAggregateDetailsInMainWindow(usage.ProviderId ?? string.Empty, capabilities);
    }

    private static ProviderUsage CreateAntigravityModelUsage(ProviderUsageDetail detail, ProviderUsage parentUsage)
    {
        var remainingPercent = UsageMath.ParsePercent(detail.Used);
        var hasRemainingPercent = remainingPercent.HasValue;
        var effectiveRemaining = remainingPercent ?? 0;

        return new ProviderUsage
        {
            ProviderId = $"antigravity.{detail.Name.ToLowerInvariant().Replace(" ", "-", StringComparison.Ordinal)}",
            ProviderName = $"{detail.Name} [Antigravity]",
            RequestsPercentage = effectiveRemaining,
            RequestsUsed = 100.0 - effectiveRemaining,
            RequestsAvailable = 100,
            UsageUnit = "Quota %",
            IsQuotaBased = true,
            PlanType = PlanType.Coding,
            Description = hasRemainingPercent ? $"{effectiveRemaining:F0}% Remaining" : "Usage unknown",
            NextResetTime = detail.NextResetTime,
            IsAvailable = parentUsage.IsAvailable,
            AuthSource = parentUsage.AuthSource,
        };
    }
}
