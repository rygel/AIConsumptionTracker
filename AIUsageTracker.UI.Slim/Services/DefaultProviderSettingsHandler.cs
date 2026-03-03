using System.Windows;
using AIUsageTracker.Core.Models;

namespace AIUsageTracker.UI.Slim.Services;

/// <summary>
/// Default settings handler for providers without special UI requirements.
/// Uses standard API key input and default inactive logic.
/// </summary>
public class DefaultProviderSettingsHandler : IProviderSettingsHandler
{
    public string ProviderId => "default";

    public bool IsInactive(ProviderConfig config, ProviderUsage? usage)
    {
        // Default: inactive if no API key configured
        return string.IsNullOrEmpty(config.ApiKey);
    }

    public UIElement? CreateInputPanel(ProviderConfig config, ProviderUsage? usage, ProviderSettingsContext context)
    {
        // Default: use standard API key input (return null)
        return null;
    }
}
