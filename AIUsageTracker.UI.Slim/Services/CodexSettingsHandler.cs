using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AIUsageTracker.Core.Models;

namespace AIUsageTracker.UI.Slim.Services;

/// <summary>
/// Settings handler for Codex provider.
/// Shows authentication status, username, and available models.
/// </summary>
public class CodexSettingsHandler : IProviderSettingsHandler
{
    public string ProviderId => "codex";

    public bool IsInactive(ProviderConfig config, ProviderUsage? usage)
    {
        // Codex uses default inactive logic
        return string.IsNullOrEmpty(config.ApiKey);
    }

    public UIElement? CreateInputPanel(ProviderConfig config, ProviderUsage? usage, ProviderSettingsContext context)
    {
        var statusPanel = new StackPanel { Orientation = Orientation.Vertical };
        var isAuthenticated = !string.IsNullOrWhiteSpace(config.ApiKey) || (usage != null && usage.IsAvailable);
        
        // Get account name from usage or cached auth
        var accountName = usage?.AccountName;
        if (string.IsNullOrWhiteSpace(accountName) || accountName == "Unknown" || accountName == "User")
        {
            accountName = context.CodexAuthUsername ?? context.OpenAIAuthUsername;
        }

        string displayText;
        if (!isAuthenticated)
        {
            displayText = "Not Authenticated";
        }
        else if (!string.IsNullOrWhiteSpace(accountName))
        {
            displayText = context.IsPrivacyMode
                ? $"Authenticated ({MaskAccountIdentifier(accountName)})"
                : $"Authenticated ({accountName})";
        }
        else if (!string.IsNullOrWhiteSpace(config.ApiKey) && (usage == null || !usage.IsAvailable))
        {
            displayText = "Authenticated via OpenAI Codex - refresh to load quota";
        }
        else
        {
            displayText = "Authenticated via OpenAI Codex";
        }

        var statusText = new TextBlock
        {
            Text = displayText,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = 11
        };
        statusText.SetResourceReference(TextBlock.ForegroundProperty,
            isAuthenticated ? "ProgressBarGreen" : "TertiaryText");

        statusPanel.Children.Add(statusText);

        // Show available models if available
        var codexModels = usage?.Details?
            .Where(d => d.DetailType == ProviderUsageDetailType.Model)
            .Select(d => d.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (codexModels is { Count: > 0 })
        {
            var modelsText = new TextBlock
            {
                Text = $"Models: {string.Join(", ", codexModels)}",
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
