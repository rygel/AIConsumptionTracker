namespace AIUsageTracker.Core.Models;

public class UsageComparison
{
    public string ProviderId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public DateTime PreviousPeriodStart { get; set; }
    public DateTime PreviousPeriodEnd { get; set; }
    public double CurrentPeriodUsage { get; set; }
    public double PreviousPeriodUsage { get; set; }
    public double ChangeAbsolute { get; set; }
    public double ChangePercent { get; set; }
    public bool IsIncrease => ChangeAbsolute > 0;
    public string ChangeDirection => ChangeAbsolute switch
    {
        > 0 => "↑",
        < 0 => "↓",
        _ => "→"
    };
}
