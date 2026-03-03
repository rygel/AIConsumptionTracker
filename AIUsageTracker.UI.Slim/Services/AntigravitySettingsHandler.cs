using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AIUsageTracker.Core.Models;

namespace AIUsageTracker.UI.Slim.Services;

/// <summary>
/// Settings handler for Antigravity provider.
/// Shows auto-detection status and discovered models.
/// </summary>
public class AntigravitySettingsHandler : IProviderSettingsHandler
{
    public string ProviderId => "antigravity";

    public bool IsInactive(ProviderConfig config, ProviderUsage? usage)
    {
        // Antigravity is inactive if not connected (no usage data or not available)
        return usage == null || !usage.IsAvailable;
    }

    public UIElement? CreateInputPanel(ProviderConfig config, ProviderUsage? usage, ProviderSettingsContext context)
    {
        var statusPanel = new StackPanel { Orientation = Orientation.Vertical };
        bool isConnected = usage != null && usage.IsAvailable;
        
        string accountInfo = usage?.AccountName ?? "Unknown";
        var displayAccount = context.IsPrivacyMode && !string.IsNullOrEmpty(accountInfo)
            ? MaskAccountIdentifier(accountInfo)
            : accountInfo;

        var statusText = new TextBlock
        {
            Text = isConnected ? $"Auto-Detected ({displayAccount})" : "Searching for local process...",
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11,
            FontStyle = isConnected ? FontStyles.Normal : FontStyles.Italic
        };
        statusText.SetResourceReference(TextBlock.ForegroundProperty, 
            isConnected ? "ProgressBarGreen" : "TertiaryText");

        statusPanel.Children.Add(statusText);

        // Show discovered models if available
        var antigravitySubmodels = usage?.Details?
            .Select(d => d.Name)
            .Where(name =>
                !string.IsNullOrWhiteSpace(name) &&
                !name.StartsWith("[", StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (antigravitySubmodels is { Count: > 0 })
        {
            var modelsText = new TextBlock
            {
                Text = $"Models: {string.Join(", ", antigravitySubmodels)}",
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 10,
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            };
            modelsText.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryText");
            statusPanel.Children.Add(modelsText);
        }

        return statusPanel;
    }

    private static string MaskAccountIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length <= 2)
            return name;
        
        return name[0] + new string('*', name.Length - 2) + name[^1];
    }
}
