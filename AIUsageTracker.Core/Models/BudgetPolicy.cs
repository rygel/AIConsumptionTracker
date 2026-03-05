namespace AIUsageTracker.Core.Models;

public class BudgetPolicy
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProviderId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public BudgetPeriod Period { get; set; } = BudgetPeriod.Monthly;
    public double Limit { get; set; }
    public string Currency { get; set; } = "USD";
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum BudgetPeriod
{
    Daily,
    Weekly,
    Monthly,
    Yearly
}

public class BudgetStatus
{
    public string ProviderId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public double BudgetLimit { get; set; }
    public double CurrentSpend { get; set; }
    public double RemainingBudget { get; set; }
    public double UtilizationPercent { get; set; }
    public BudgetPeriod Period { get; set; }
    public bool IsOverBudget => CurrentSpend > BudgetLimit;
    public bool IsWarning => UtilizationPercent >= 80 && !IsOverBudget;
    public bool IsHealthy => UtilizationPercent < 80;
}

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
