namespace AIUsageTracker.Core.Models;

public class ProviderUsageDetail
{
    public string Name { get; set; } = string.Empty;
    public string ModelName { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string Used { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? NextResetTime { get; set; }
    public ProviderUsageDetailType DetailType { get; set; } = ProviderUsageDetailType.Unknown;
    public WindowKind WindowKind { get; set; } = WindowKind.None;

    public bool IsPrimaryQuotaDetail()
    {
        return DetailType == ProviderUsageDetailType.QuotaWindow && WindowKind == WindowKind.Primary;
    }
    public bool IsSecondaryQuotaDetail()
    {
        return DetailType == ProviderUsageDetailType.QuotaWindow && WindowKind == WindowKind.Secondary;
    }
    public bool IsWindowQuotaDetail()
    {
        return DetailType == ProviderUsageDetailType.QuotaWindow;
    }
    public bool IsCreditDetail()
    {
        return DetailType == ProviderUsageDetailType.Credit;
    }
    public bool IsDisplayableSubProviderDetail()
    {
        return DetailType == ProviderUsageDetailType.Model || DetailType == ProviderUsageDetailType.Other;
    }
}
