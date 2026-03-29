// <copyright file="GroupedUsageDisplayAdapter.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using AIUsageTracker.Core.Models;
using AIUsageTracker.Core.MonitorClient;
using AIUsageTracker.Infrastructure.Providers;

namespace AIUsageTracker.UI.Slim;

internal static class GroupedUsageDisplayAdapter
{
    public static IReadOnlyList<ProviderUsage> Expand(AgentGroupedUsageSnapshot? snapshot)
    {
        if (snapshot?.Providers == null || snapshot.Providers.Count == 0)
        {
            return Array.Empty<ProviderUsage>();
        }

        var usages = new List<ProviderUsage>(snapshot.Providers.Count * 2);
        foreach (var provider in snapshot.Providers
                     .Where(provider => !string.IsNullOrWhiteSpace(provider.ProviderId))
                     .OrderBy(provider => provider.ProviderId, StringComparer.OrdinalIgnoreCase))
        {
            // Each quota window or model card becomes an independent top-level card — no parent
            // aggregate, no child rows.
            if (provider.Models.Count > 0)
            {
                usages.AddRange(BuildFlatWindowCards(provider));
                continue;
            }

            var windowCards = provider.ProviderDetails
                .Where(d => d.WindowKind != WindowKind.None)
                .ToList();

            var parentUsage = new ProviderUsage
            {
                ProviderId = provider.ProviderId,
                ProviderName = ProviderMetadataCatalog.GetConfiguredDisplayName(provider.ProviderId),
                AccountName = provider.AccountName,
                IsAvailable = provider.IsAvailable,
                PlanType = provider.PlanType,
                IsQuotaBased = provider.IsQuotaBased,
                RequestsUsed = provider.RequestsUsed,
                RequestsAvailable = provider.RequestsAvailable,
                UsedPercent = provider.UsedPercent,
                Description = provider.Description,
                FetchedAt = provider.FetchedAt,
                NextResetTime = provider.NextResetTime,
                PeriodDuration = ResolvePeriodDuration(provider.ProviderId),
                WindowCards = windowCards.Count > 0 ? windowCards : null,
            };

            usages.Add(parentUsage);
        }

        return usages;
    }

    private static IReadOnlyList<ProviderUsage> BuildFlatWindowCards(AgentGroupedProviderUsage provider)
    {
        var cards = new List<ProviderUsage>(provider.Models.Count);
        foreach (var model in provider.Models)
        {
            var flatProviderId = $"{provider.ProviderId}.{model.ModelId}";
            var modelState = AgentGroupedUsageValueResolver.ResolveModelEffectiveState(model, provider.IsQuotaBased);

            cards.Add(new ProviderUsage
            {
                ProviderId = flatProviderId,
                ProviderName = model.ModelName,
                AccountName = provider.AccountName,
                IsAvailable = provider.IsAvailable,
                PlanType = provider.PlanType,
                IsQuotaBased = provider.IsQuotaBased,
                RequestsUsed = modelState.UsedPercentage,
                UsedPercent = modelState.UsedPercentage,
                Description = modelState.Description,
                FetchedAt = provider.FetchedAt,
                NextResetTime = modelState.NextResetTime,
                PeriodDuration = ResolvePeriodDuration(flatProviderId),
            });
        }

        return cards;
    }

    private static TimeSpan? ResolvePeriodDuration(string providerId)
    {
        if (!ProviderMetadataCatalog.TryGet(providerId, out var definition))
        {
            return null;
        }

        if (string.Equals(providerId, definition.ProviderId, StringComparison.OrdinalIgnoreCase))
        {
            return definition.QuotaWindows
                .FirstOrDefault(window => window.Kind == WindowKind.Rolling && window.PeriodDuration.HasValue)
                ?.PeriodDuration;
        }

        // Derived child provider (e.g. "claude-code.sonnet"): try explicit ChildProviderId match first,
        // then fall back to the parent's Rolling window duration so pace/headroom is computed correctly.
        var fromChildProviderId = definition.QuotaWindows
            .FirstOrDefault(window =>
                window.PeriodDuration.HasValue &&
                !string.IsNullOrWhiteSpace(window.ChildProviderId) &&
                string.Equals(window.ChildProviderId, providerId, StringComparison.OrdinalIgnoreCase))
            ?.PeriodDuration;

        return fromChildProviderId
            ?? definition.QuotaWindows
                .FirstOrDefault(window => window.Kind == WindowKind.Rolling && window.PeriodDuration.HasValue)
                ?.PeriodDuration;
    }
}
