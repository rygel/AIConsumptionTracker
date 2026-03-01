namespace AIUsageTracker.Core.Models;

public static class ProviderPlanClassifier
{
    private static readonly HashSet<string> CodingPlanProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "antigravity",
        "synthetic",
        "zai-coding-plan",
        "github-copilot",
        "gemini-cli",
        "kimi",
        "openai",
        "codex"
    };

    public static bool IsCodingPlanProvider(string providerId)
    {
        if (string.IsNullOrWhiteSpace(providerId))
        {
            return false;
        }

        if (CodingPlanProviders.Contains(providerId))
        {
            return true;
        }

        var separatorIndex = providerId.IndexOf('.');
        if (separatorIndex <= 0)
        {
            return false;
        }

        var rootProviderId = providerId[..separatorIndex];
        return CodingPlanProviders.Contains(rootProviderId);
    }
}

