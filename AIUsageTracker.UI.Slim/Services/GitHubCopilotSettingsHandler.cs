using System.Windows;
using System.Windows.Controls;
using AIUsageTracker.Core.Models;

namespace AIUsageTracker.UI.Slim.Services;

/// <summary>
/// Settings handler for GitHub Copilot provider.
/// Shows authentication status and username (if available).
/// </summary>
public class GitHubCopilotSettingsHandler : IProviderSettingsHandler
{
    public string ProviderId => "github-copilot";

    public bool IsInactive(ProviderConfig config, ProviderUsage? usage)
    {
        // GitHub Copilot uses default inactive logic (no API key = inactive)
        return string.IsNullOrEmpty(config.ApiKey);
    }

    public UIElement? CreateInputPanel(ProviderConfig config, ProviderUsage? usage, ProviderSettingsContext context)
    {
        var statusPanel = new StackPanel { Orientation = Orientation.Horizontal };
        
        // Get username from usage data or cached auth
        string? username = usage?.AccountName;
        if (string.IsNullOrWhiteSpace(username) || username == "Unknown" || username == "User")
        {
            username = context.GitHubAuthUsername;
        }
        
        bool hasUsername = !string.IsNullOrEmpty(username) && username != "Unknown" && username != "User";
        bool isAuthenticated = !string.IsNullOrEmpty(config.ApiKey) || !string.IsNullOrWhiteSpace(context.GitHubAuthUsername);

        string displayText;
        if (!isAuthenticated)
        {
            displayText = "Not Authenticated";
        }
        else if (!hasUsername)
        {
            displayText = "Authenticated";
        }
        else if (context.IsPrivacyMode)
        {
            displayText = $"Authenticated ({MaskAccountIdentifier(username)})";
        }
        else
        {
            displayText = $"Authenticated ({username})";
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
        return statusPanel;
    }

    private static string MaskAccountIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length <= 2)
            return name;
        
        return name[0] + new string('*', name.Length - 2) + name[^1];
    }
}
