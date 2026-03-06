using AIUsageTracker.Core.Models;
using AIUsageTracker.Infrastructure.Providers;

namespace AIUsageTracker.UI.Slim;

internal enum ProviderInputMode
{
    StandardApiKey,
    DerivedReadOnly,
    AntigravityAutoDetected,
    GitHubCopilotAuthStatus,
    OpenAiSessionStatus
}

internal sealed record ProviderSettingsBehavior(
    ProviderInputMode InputMode,
    bool IsInactive,
    bool IsDerivedVisible,
    string? SessionProviderLabel,
    bool PreferCodexIdentity);

internal static class ProviderSettingsCatalog
{
    public static ProviderSettingsBehavior Resolve(ProviderConfig config, ProviderUsage? usage, bool isDerived)
    {
        var canonicalProviderId = ProviderMetadataCatalog.GetCanonicalProviderId(config.ProviderId);
        var hasSessionToken = IsSessionToken(config.ApiKey);

        var inputMode = isDerived
            ? ProviderInputMode.DerivedReadOnly
            : canonicalProviderId switch
            {
                "antigravity" => ProviderInputMode.AntigravityAutoDetected,
                "github-copilot" => ProviderInputMode.GitHubCopilotAuthStatus,
                "codex" => ProviderInputMode.OpenAiSessionStatus,
                "openai" when usage?.IsQuotaBased == true || hasSessionToken => ProviderInputMode.OpenAiSessionStatus,
                _ => ProviderInputMode.StandardApiKey
            };

        var isInactive = isDerived
            ? false
            : inputMode switch
            {
                ProviderInputMode.AntigravityAutoDetected => usage == null || !usage.IsAvailable,
                ProviderInputMode.OpenAiSessionStatus => string.IsNullOrWhiteSpace(config.ApiKey) && !(usage?.IsAvailable == true),
                _ => string.IsNullOrWhiteSpace(config.ApiKey)
            };

        var sessionProviderLabel = inputMode == ProviderInputMode.OpenAiSessionStatus
            ? string.Equals(canonicalProviderId, "codex", StringComparison.OrdinalIgnoreCase)
                ? "OpenAI Codex"
                : "OpenAI"
            : null;

        return new ProviderSettingsBehavior(
            InputMode: inputMode,
            IsInactive: isInactive,
            IsDerivedVisible: IsDerivedProviderVisible(config.ProviderId),
            SessionProviderLabel: sessionProviderLabel,
            PreferCodexIdentity: inputMode == ProviderInputMode.OpenAiSessionStatus &&
                                 string.Equals(canonicalProviderId, "codex", StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsDerivedProviderVisible(string? providerId)
    {
        return string.Equals(providerId, "codex.spark", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSessionToken(string? apiKey)
    {
        return !string.IsNullOrWhiteSpace(apiKey) &&
               !apiKey.StartsWith("sk-", StringComparison.OrdinalIgnoreCase);
    }
}
