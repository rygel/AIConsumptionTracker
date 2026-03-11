// <copyright file="ProviderUsageDisplayCatalog.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using AIUsageTracker.Core.Models;
using AIUsageTracker.Infrastructure.Providers;

namespace AIUsageTracker.UI.Slim;

internal static class ProviderUsageDisplayCatalog
{
    public static ProviderRenderPreparation PrepareForMainWindow(IReadOnlyCollection<ProviderUsage> usages)
    {
        var filteredUsages = usages.ToList();
        var hasAntigravityParent = filteredUsages.Any(IsAntigravityParent);
        var hasCodexParent = filteredUsages.Any(IsCodexParent);

        filteredUsages = filteredUsages
            .Where(ShouldDisplayUsage(hasAntigravityParent, hasCodexParent))
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

    public static IReadOnlyList<ProviderUsage> CreateCodexSubUsages(IReadOnlyCollection<ProviderUsage> usages)
    {
        return usages
            .Where(usage => IsCodexChild(usage.ProviderId ?? string.Empty))
            .GroupBy(usage => usage.ProviderId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(usage => ProviderMetadataCatalog.GetDisplayName(usage.ProviderId ?? string.Empty, usage.ProviderName), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Func<ProviderUsage, bool> ShouldDisplayUsage(bool hasAntigravityParent, bool hasCodexParent)
    {
        return usage =>
        {
            var providerId = usage.ProviderId ?? string.Empty;
            return !IsUnavailableAntigravityParent(usage) &&
                   (!providerId.StartsWith("antigravity.", StringComparison.OrdinalIgnoreCase) || !hasAntigravityParent) &&
                   (!IsCodexChild(providerId) || !hasCodexParent);
        };
    }

    private static bool IsUnavailableAntigravityParent(ProviderUsage usage)
    {
        return IsAntigravityParent(usage) && !usage.IsAvailable;
    }

    private static bool IsAntigravityParent(ProviderUsage usage)
    {
        return ProviderMetadataCatalog.IsAggregateParentProviderId(usage.ProviderId ?? string.Empty);
    }

    private static bool IsCodexParent(ProviderUsage usage)
    {
        var providerId = usage.ProviderId ?? string.Empty;
        return string.Equals(
            ProviderMetadataCatalog.GetCanonicalProviderId(providerId),
            CodexProvider.StaticDefinition.ProviderId,
            StringComparison.OrdinalIgnoreCase) &&
            !providerId.Contains('.', StringComparison.Ordinal);
    }

    private static bool IsCodexChild(string providerId)
    {
        return providerId.StartsWith(
            $"{CodexProvider.StaticDefinition.ProviderId}.",
            StringComparison.OrdinalIgnoreCase);
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
