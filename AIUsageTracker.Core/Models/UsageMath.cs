namespace AIUsageTracker.Core.Models;

public static class UsageMath
{
    private const double MinimumDropRatioForReset = 0.2;
    private const double MinimumElapsedHours = 1.0;

    public static double ClampPercent(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0;
        }

        return Math.Clamp(value, 0d, 100d);
    }

    public static double CalculateUsedPercent(double used, double total)
    {
        if (total <= 0)
        {
            return 0;
        }

        return ClampPercent((used / total) * 100d);
    }

    public static double CalculateRemainingPercent(double used, double total)
    {
        if (total <= 0)
        {
            return 100;
        }

        return ClampPercent(((total - used) / total) * 100d);
    }

    public static double GetEffectiveUsedPercent(ProviderUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);

        var percentage = ClampPercent(usage.RequestsPercentage);
        var isQuota = usage.IsQuotaBased || usage.PlanType == PlanType.Coding;
        return isQuota ? ClampPercent(100 - percentage) : percentage;
    }

    public static BurnRateForecast CalculateBurnRateForecast(IEnumerable<ProviderUsage> history)
    {
        ArgumentNullException.ThrowIfNull(history);

        var samples = history
            .Where(x => x.FetchedAt != default && x.RequestsAvailable > 0 && !double.IsNaN(x.RequestsUsed))
            .OrderBy(x => x.FetchedAt)
            .ToList();

        if (samples.Count < 2)
        {
            return BurnRateForecast.Unavailable("Insufficient history");
        }

        var cycleSamples = TrimToLatestCycle(samples);
        if (cycleSamples.Count < 2)
        {
            return BurnRateForecast.Unavailable("Insufficient cycle history");
        }

        var first = cycleSamples[0];
        var last = cycleSamples[^1];
        var elapsedDays = (last.FetchedAt - first.FetchedAt).TotalDays;
        if (elapsedDays <= 0 || (last.FetchedAt - first.FetchedAt).TotalHours < MinimumElapsedHours)
        {
            return BurnRateForecast.Unavailable("Insufficient time window");
        }

        double positiveIncrease = 0;
        for (var i = 1; i < cycleSamples.Count; i++)
        {
            var delta = cycleSamples[i].RequestsUsed - cycleSamples[i - 1].RequestsUsed;
            if (delta > 0)
            {
                positiveIncrease += delta;
            }
        }

        if (positiveIncrease <= 0)
        {
            return BurnRateForecast.Unavailable("No consumption trend");
        }

        var burnRatePerDay = positiveIncrease / elapsedDays;
        if (burnRatePerDay <= 0 || double.IsNaN(burnRatePerDay) || double.IsInfinity(burnRatePerDay))
        {
            return BurnRateForecast.Unavailable("Invalid burn rate");
        }

        var remaining = Math.Max(0, last.RequestsAvailable - last.RequestsUsed);
        var daysRemaining = remaining <= 0 ? 0 : remaining / burnRatePerDay;
        if (double.IsNaN(daysRemaining) || double.IsInfinity(daysRemaining))
        {
            return BurnRateForecast.Unavailable("Invalid forecast");
        }

        return new BurnRateForecast
        {
            IsAvailable = true,
            BurnRatePerDay = burnRatePerDay,
            RemainingUnits = remaining,
            DaysUntilExhausted = daysRemaining,
            EstimatedExhaustionUtc = last.FetchedAt.ToUniversalTime().AddDays(daysRemaining),
            SampleCount = cycleSamples.Count
        };
    }

    private static List<ProviderUsage> TrimToLatestCycle(List<ProviderUsage> orderedSamples)
    {
        if (orderedSamples.Count < 2)
        {
            return orderedSamples;
        }

        var cycleStart = 0;
        for (var i = 1; i < orderedSamples.Count; i++)
        {
            var previous = orderedSamples[i - 1];
            var current = orderedSamples[i];
            var drop = previous.RequestsUsed - current.RequestsUsed;
            if (drop <= 0)
            {
                continue;
            }

            var reference = Math.Max(previous.RequestsUsed, previous.RequestsAvailable);
            var dropRatio = reference <= 0 ? 0 : drop / reference;
            if (dropRatio >= MinimumDropRatioForReset)
            {
                cycleStart = i;
            }
        }

        return orderedSamples.Skip(cycleStart).ToList();
    }
}

public sealed class BurnRateForecast
{
    public bool IsAvailable { get; init; }
    public double BurnRatePerDay { get; init; }
    public double RemainingUnits { get; init; }
    public double DaysUntilExhausted { get; init; }
    public DateTime? EstimatedExhaustionUtc { get; init; }
    public int SampleCount { get; init; }
    public string? Reason { get; init; }

    public static BurnRateForecast Unavailable(string reason)
    {
        return new BurnRateForecast
        {
            IsAvailable = false,
            Reason = reason
        };
    }
}

