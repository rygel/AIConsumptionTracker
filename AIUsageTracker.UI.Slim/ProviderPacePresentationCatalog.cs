// <copyright file="ProviderPacePresentationCatalog.cs" company="AIUsageTracker">
// Copyright (c) AIUsageTracker. All rights reserved.
// </copyright>

using AIUsageTracker.Core.Models;

namespace AIUsageTracker.UI.Slim;

/// <summary>
/// Centralized pace-aware presentation policy for provider cards.
/// Keeps pace logic out of view and code-behind paths.
/// </summary>
internal static class ProviderPacePresentationCatalog
{
    public static double GetColorIndicatorPercent(
        ProviderUsage usage,
        double usedPercent,
        bool enablePaceAdjustment,
        DateTime? nowUtc = null)
    {
        if (!enablePaceAdjustment || !usage.PeriodDuration.HasValue || !usage.NextResetTime.HasValue)
        {
            return usedPercent;
        }

        return UsageMath.CalculatePaceAdjustedColorPercent(
            usedPercent,
            usage.NextResetTime.Value.ToUniversalTime(),
            usage.PeriodDuration.Value,
            nowUtc);
    }

    public static string? GetPaceBadgeText(
        ProviderUsage usage,
        double usedPercent,
        bool enablePaceAdjustment,
        DateTime? nowUtc = null)
    {
        if (!enablePaceAdjustment || !usage.PeriodDuration.HasValue || !usage.NextResetTime.HasValue)
        {
            return null;
        }

        var now = nowUtc ?? DateTime.UtcNow;
        var period = usage.PeriodDuration.Value;
        var periodStart = usage.NextResetTime.Value.ToUniversalTime() - period;
        var elapsed = now - periodStart;
        var elapsedFraction = Math.Clamp(elapsed.TotalSeconds / period.TotalSeconds, 0.01, 1.0);
        var expectedPercent = elapsedFraction * 100.0;

        return usedPercent < expectedPercent * 0.95 ? "On pace" : null;
    }
}
