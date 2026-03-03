namespace AIUsageTracker.UI.Slim.Services;

/// <summary>
/// Context object that provides cached authentication data for provider settings handlers.
/// This avoids repeated auth lookups and decouples handlers from SettingsWindow state.
/// </summary>
public class ProviderSettingsContext
{
    /// <summary>
    /// Cached GitHub username from authentication (if available)
    /// </summary>
    public string? GitHubAuthUsername { get; set; }

    /// <summary>
    /// Cached OpenAI username from authentication (if available)
    /// </summary>
    public string? OpenAIAuthUsername { get; set; }

    /// <summary>
    /// Cached Codex username from authentication (if available)
    /// </summary>
    public string? CodexAuthUsername { get; set; }

    /// <summary>
    /// Whether privacy mode is currently enabled
    /// </summary>
    public bool IsPrivacyMode { get; set; }
}
