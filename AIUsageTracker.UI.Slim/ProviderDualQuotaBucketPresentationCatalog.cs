// <copyright file="ProviderDualQuotaBucketPresentationCatalog.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using AIUsageTracker.Core.Models;

namespace AIUsageTracker.UI.Slim;

internal static class ProviderDualQuotaBucketPresentationCatalog
{
    public static bool TryGetDualQuotaBucketUsedPercentages(ProviderUsage usage, out double primaryUsed, out double secondaryUsed)
    {
        primaryUsed = 0;
        secondaryUsed = 0;

        if (!TryGetPresentation(usage, out var presentation))
        {
            return false;
        }

        primaryUsed = presentation.PrimaryUsedPercent;
        secondaryUsed = presentation.SecondaryUsedPercent;
        return true;
    }

    public static bool TryGetPresentation(ProviderUsage usage, out ProviderDualQuotaBucketPresentation presentation)
    {
        presentation = null!;

        if (usage.Details?.Any() != true)
        {
            return false;
        }

        var quotaBuckets = usage.Details
            .Where(detail => detail.DetailType == ProviderUsageDetailType.QuotaWindow)
            .Where(detail => detail.QuotaBucketKind != WindowKind.None)
            .ToList();

        if (quotaBuckets.Count < 2)
        {
            return false;
        }

        // Find the first two distinct quota windows for dual bar display.
        // Priority: Burst (5h) > Rolling (weekly) > ModelSpecific (spark)
        // This allows dual bars for any combination: Burst+Rolling, Burst+ModelSpecific, Rolling+ModelSpecific
        var orderedBuckets = quotaBuckets
            .OrderBy(detail => GetWindowKindPriority(detail.QuotaBucketKind))
            .ToList();

        var firstDetail = orderedBuckets[0];
        var secondDetail = orderedBuckets.Skip(1).FirstOrDefault(d => d.QuotaBucketKind != firstDetail.QuotaBucketKind)
                           ?? orderedBuckets[1];

        if (firstDetail == secondDetail)
        {
            return false;
        }

        var parsedFirst = UsageMath.GetEffectiveUsedPercent(firstDetail, usage.IsQuotaBased);
        var parsedSecond = UsageMath.GetEffectiveUsedPercent(secondDetail, usage.IsQuotaBased);

        if (!parsedFirst.HasValue || !parsedSecond.HasValue)
        {
            return false;
        }

        presentation = new ProviderDualQuotaBucketPresentation(
            PrimaryLabel: SimplifyQuotaBucketLabel(firstDetail.Name, "Burst"),
            PrimaryUsedPercent: parsedFirst.Value,
            PrimaryResetTime: firstDetail.NextResetTime,
            SecondaryLabel: SimplifyQuotaBucketLabel(secondDetail.Name, "Rolling"),
            SecondaryUsedPercent: parsedSecond.Value,
            SecondaryResetTime: secondDetail.NextResetTime);
        return true;
    }

    private static int GetWindowKindPriority(WindowKind kind)
    {
        return kind switch
        {
            WindowKind.Burst => 0,
            WindowKind.Rolling => 1,
            WindowKind.ModelSpecific => 2,
            _ => 99,
        };
    }

    private static string SimplifyQuotaBucketLabel(string? rawLabel, string fallback)
    {
        if (string.IsNullOrWhiteSpace(rawLabel))
        {
            return fallback;
        }

        var label = rawLabel.Trim();
        label = label.Replace(" quota", string.Empty, StringComparison.OrdinalIgnoreCase);
        label = label.Replace(" limit", string.Empty, StringComparison.OrdinalIgnoreCase);
        return string.IsNullOrWhiteSpace(label) ? fallback : label;
    }
}
