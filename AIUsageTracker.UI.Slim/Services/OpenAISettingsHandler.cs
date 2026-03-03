using System.Windows;
using AIUsageTracker.Core.Models;

namespace AIUsageTracker.UI.Slim.Services;

/// <summary>
/// Settings handler for OpenAI provider.
/// OpenAI is special because it can have session-based usage without API key.
/// </summary>
public class OpenAISettingsHandler : IProviderSettingsHandler
{
    public string ProviderId => "openai";

    public bool IsInactive(ProviderConfig config, ProviderUsage? usage)
    {
        // OpenAI is inactive if:
        // - No API key AND no session usage
        // Session usage exists when there's usage data that is available and quota-based
        var hasApiKey = !string.IsNullOrWhiteSpace(config.ApiKey);
        var hasSessionUsage = usage != null && usage.IsAvailable && usage.IsQuotaBased;
        return !hasApiKey && !hasSessionUsage;
    }

    public UIElement? CreateInputPanel(ProviderConfig config, ProviderUsage? usage, ProviderSettingsContext context)
    {
        // OpenAI uses default API key input, return null
        return null;
    }
}
