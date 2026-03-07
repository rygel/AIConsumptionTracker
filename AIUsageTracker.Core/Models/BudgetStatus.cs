namespace AIUsageTracker.Core.Models;

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
