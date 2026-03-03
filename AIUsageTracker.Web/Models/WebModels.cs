namespace AIUsageTracker.Web.Models;

public class ProviderInfo
{
    public string ProviderId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string PlanType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? AuthSource { get; set; }
    public string? AccountName { get; set; }
    public double LatestUsage { get; set; }
    public DateTime? NextResetTime { get; set; }
}

public class UsageSummary
{
    public int ProviderCount { get; set; }
    public double AverageUsage { get; set; }
    public string? LastUpdate { get; set; }
}

public class ChartDataPoint
{
    public string ProviderId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double RequestsPercentage { get; set; }
    public double RequestsUsed { get; set; }
}
