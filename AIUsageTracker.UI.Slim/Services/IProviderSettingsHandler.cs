using System.Windows;
using AIUsageTracker.Core.Models;

namespace AIUsageTracker.UI.Slim.Services;

/// <summary>
/// Interface for provider-specific settings UI handling.
/// Each provider can implement custom logic for determining inactive status
/// and creating specialized input panels.
/// </summary>
public interface IProviderSettingsHandler
{
    /// <summary>
    /// The provider ID this handler is responsible for.
    /// </summary>
    string ProviderId { get; }

    /// <summary>
    /// Determines if the provider should be shown as inactive in the settings.
    /// </summary>
    /// <param name="config">Provider configuration</param>
    /// <param name="usage">Current usage data (may be null)</param>
    /// <returns>True if the provider should be marked as inactive</returns>
    bool IsInactive(ProviderConfig config, ProviderUsage? usage);

    /// <summary>
    /// Creates the provider-specific input panel for the settings window.
    /// Returns null if the default API key input should be used.
    /// </summary>
    /// <param name="config">Provider configuration</param>
    /// <param name="usage">Current usage data (may be null)</param>
    /// <param name="context">Settings context with cached auth data</param>
    /// <returns>UI element for the input panel, or null for default behavior</returns>
    UIElement? CreateInputPanel(ProviderConfig config, ProviderUsage? usage, ProviderSettingsContext context);
}
