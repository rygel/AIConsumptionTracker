namespace AIUsageTracker.Core.Models;

public enum ProviderUsageDetailType
{
    Unknown = 0,
    QuotaWindow = 1,
    Credit = 2,
    Model = 3,
    Other = 4
}

public enum WindowKind
{
    None = 0,
    Primary = 1,
    Secondary = 2,
    Spark = 3
}

public class ProviderUsage
{
    public string ProviderId { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public double RequestsUsed { get; set; }
    public double RequestsAvailable { get; set; }
    public double RequestsPercentage { get; set; }
    public PlanType PlanType { get; set; } = PlanType.Usage;

    public string UsageUnit { get; set; } = "USD";
    public bool IsQuotaBased { get; set; }
    public bool DisplayAsFraction { get; set; } // Explicitly request "X / Y" display format
    public bool IsAvailable { get; set; } = true;
    public string Description { get; set; } = string.Empty;
    public string AuthSource { get; set; } = string.Empty;
    public List<ProviderUsageDetail>? Details { get; set; }
    
    // Temporary property for database serialization - not serialized to JSON
    [System.Text.Json.Serialization.JsonIgnore]
    public string? DetailsJson { get; set; }
    
    public string AccountName { get; set; } = string.Empty;
    public string ConfigKey { get; set; } = string.Empty;
    public DateTime? NextResetTime { get; set; }
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
    public double ResponseLatencyMs { get; set; }
    public string? RawJson { get; set; }
    public int HttpStatus { get; set; } = 200;

    public string GetFriendlyName()
    {
        // Straight Line Architecture: Prefer the name provided by the Provider Class
        if (!string.IsNullOrWhiteSpace(ProviderName) && 
            !string.Equals(ProviderName, ProviderId, StringComparison.OrdinalIgnoreCase))
        {
            return ProviderName;
        }

        if (string.IsNullOrWhiteSpace(ProviderId))
        {
            return "Unknown Provider";
        }

        // Clean fallback: TitleCase the ID (e.g. "github-copilot" -> "Github Copilot")
        var name = ProviderId.Replace("_", " ").Replace("-", " ");
        
        // Handle child IDs (e.g. "codex.primary" -> "Codex Primary")
        if (name.Contains('.'))
        {
            name = name.Replace(".", " ");
        }

        return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
    }
}

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

